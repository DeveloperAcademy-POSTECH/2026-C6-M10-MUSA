using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Spoken A/B forewarning on the shared battle clock. One component lives on the
    /// persistent network so UI Hide/rebind cannot repeat a phrase. No Host rules are driven by audio.</summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiPatternAudio : MonoBehaviour
    {
        [SerializeField] private LabJigwiPatternAudioProfile profile;
        private LabJigwiPatternAudioProfile fallback;
        private LabNetwork network;
        private AudioSource voice;
        private Transform voiceRoot;
        private string sessionId;
        private uint round;
        private double stageStartedAt = -1d;
        private bool hasScope, applicationPaused, applicationFocused = true, nativePaused;
        private byte consumedSlots;
        private readonly bool[] warnedMissingClip = new bool[2];
        private int currentSlot = -1;
        private double voiceStartAt, activeAt, voiceEndAt, pausedBattleTime, lastObservedNow = -1d;
        private const float SeekToleranceSeconds = .075f;

        public AudioSource Source { get { EnsureSource(); return voice; } }
        public LabJigwiPatternAudioProfile Profile
        {
            get
            {
                if (profile != null) return profile;
                profile = Resources.Load<LabJigwiPatternAudioProfile>(LabJigwiPatternAudioProfile.ResourcesPath);
                if (profile != null) return profile;
                if (fallback == null) fallback = LabJigwiPatternAudioProfile.MakeFallback();
                return fallback;
            }
        }
        public int CurrentSlot => currentSlot;
        public bool IsPaused => currentSlot >= 0 && nativePaused;
        public int PlaybackStartCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int SeekCount { get; private set; }
        public int MissingClipCount { get; private set; }
        public int ConsumedSlotCount
        {
            get { int count = 0; for (int i = 0; i < LabJigwiABTimeline.SlotCount; i++) if (Consumed(i)) count++; return count; }
        }
        public double CurrentWordAt => currentSlot < 0 ? -1d : activeAt;
        public double CurrentVoiceStartAt => currentSlot < 0 ? -1d : voiceStartAt;

        public static LabJigwiPatternAudio Attach(LabNetwork target)
        {
            if (target == null) return null;
            var value = target.GetComponent<LabJigwiPatternAudio>();
            if (value == null) value = target.gameObject.AddComponent<LabJigwiPatternAudio>();
            value.Bind(target);
            return value;
        }
        public void Bind(LabNetwork target)
        {
            if (network != target) StopCurrent();
            network = target;
            EnsureSource();
        }
        public void SetProfile(LabJigwiPatternAudioProfile value)
        {
            if (profile != value) StopCurrent();
            profile = value;
        }
        public void EnsureSource()
        {
            if (voice != null) return;
            var child = new GameObject("Jigwi Pattern Voice"); child.transform.SetParent(transform, false);
            voiceRoot = child.transform;
            voice = child.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f; voice.pitch = 1f;
        }
        private void Awake() => EnsureSource();
        private void OnEnable()
        {
            applicationPaused = false; applicationFocused = Application.isFocused;
            LabAudioSettings.Changed += ApplyVolume;
        }
        private void Update()
        {
            if (network == null || !network.HasBattleReferences) { StopCurrent(); return; }
            LabSnapshot snapshot = network.Snapshot;
            // Read the time first: it updates whether a Guest's clock has stalled (#451).
            double now = network.JigwiABPresentationTime;
            Observe(snapshot, now, network.GameStartHeld || network.BattlePresentationHeld);
        }

        /// <summary>Explicit shared-clock entry point for the live observer and isolated audio tests.
        /// Late observers join the current clip cursor before the word, never restart an old phrase.</summary>
        public void Observe(LabSnapshot snapshot, double now, bool held = false)
        {
            if (snapshot?.phase != LabPhase.Playing || snapshot.jigwiAB?.enabled != true
                || string.IsNullOrEmpty(snapshot.sessionId) || !Finite(now)
                || !Finite(snapshot.jigwiAB.startedAt) || !Finite(snapshot.jigwiAB.cutoffAt) || snapshot.jigwiAB.startedAt < 0d
                || now < snapshot.jigwiAB.startedAt || now >= snapshot.jigwiAB.cutoffAt || snapshot.jigwiAB.fever)
            { StopCurrent(); return; }
            LabJigwiABSnapshot state = snapshot.jigwiAB;
            if (!hasScope || sessionId != snapshot.sessionId || round != snapshot.round || stageStartedAt != state.startedAt)
            {
                StopCurrent(); consumedSlots = 0; hasScope = true;
                sessionId = snapshot.sessionId; round = snapshot.round; stageStartedAt = state.startedAt;
                lastObservedNow = -1d;
            }
            EnsureSource();
            // A corrected snapshot may move the presentation clock backwards. Never rewind
            // a spoken word across that correction; the fixed six-slot ledger still consumes it.
            if (lastObservedNow >= 0d && now < lastObservedNow - SeekToleranceSeconds) StopCurrent();
            lastObservedNow = now;
            bool suspended = held || applicationPaused || !applicationFocused;
            ReconcileCurrent(state, now, suspended);
            for (int slot = 0; slot < LabJigwiABTimeline.SlotCount; slot++)
            {
                if (Consumed(slot)) continue;
                double activation = stageStartedAt + LabJigwiABTimeline.ActiveOffset(slot);
                LabJigwiABRound actual = FindRound(state, slot);
                if (now >= activation || actual != null && actual.resolution != LabJigwiABResolution.None)
                { Consume(slot); continue; }
                LabJigwiABKind kind = LabJigwiABTimeline.KindForSlot(slot);
                AudioClip clip = Profile.ClipFor(kind);
                float cue = Profile.CueSecondsFor(kind);
                double begins = activation - cue;
                if (now < begins) continue;
                // Do not accumulate a queue during Pause/focus loss. If the clock stays held,
                // this same window remains eligible on resume; if it advances past the word it is consumed above.
                if (suspended) continue;
                Consume(slot);
                if (clip == null)
                {
                    int index = kind == LabJigwiABKind.PersonalBurn ? 0 : 1;
                    if (!warnedMissingClip[index])
                    {
                        warnedMissingClip[index] = true; MissingClipCount++;
                        Debug.LogWarning("Jigwi pattern voice clip is missing: " + kind, this);
                    }
                    continue;
                }
                StartVoice(slot, clip, begins, activation, now);
            }
        }
        private void StartVoice(int slot, AudioClip clip, double begins, double activation, double now)
        {
            StopCurrent(); currentSlot = slot;
            voiceStartAt = begins; activeAt = activation; voiceEndAt = begins + clip.length;
            voice.clip = clip; ApplyVolume();
            Seek(now); voice.Play(); PlaybackStartCount++;
        }
        private void ReconcileCurrent(LabJigwiABSnapshot state, double now, bool suspended)
        {
            if (currentSlot < 0) return;
            LabJigwiABRound actual = FindRound(state, currentSlot);
            if (now >= voiceEndAt || now < voiceStartAt
                || actual != null && (actual.resolution != LabJigwiABResolution.None || now >= actual.endsAt))
            { StopCurrent(); return; }
            ApplyVolume();
            if (suspended)
            {
                if (!nativePaused)
                {
                    PauseCurrent(now);
                }
                return;
            }
            if (nativePaused)
            {
                // Control Center can keep the battle running. Never resume an unheard terminal
                // word after its actual effect has started. A true battle Pause does not move now.
                if (now > pausedBattleTime + .001d && now >= activeAt) { StopCurrent(); return; }
                Seek(now); voice.UnPause(); nativePaused = false; ResumeCount++;
            }
            else
            {
                float expected = Cursor(now);
                if (Mathf.Abs(voice.time - expected) > SeekToleranceSeconds) Seek(now);
            }
        }
        private void Seek(double now)
        {
            if (voice == null || voice.clip == null) return;
            float cursor = Cursor(now);
            // Set the sample cursor before Play/UnPause so a late observer never emits the opening twice.
            voice.timeSamples = Mathf.Clamp((int)(cursor * voice.clip.frequency), 0, Mathf.Max(0, voice.clip.samples - 1));
            SeekCount++;
        }
        private float Cursor(double now) => voice == null || voice.clip == null ? 0f
            : Mathf.Clamp((float)(now - voiceStartAt), 0f, Mathf.Max(0f, voice.clip.length - 1f / Mathf.Max(1, voice.clip.frequency)));
        private void ApplyVolume()
        {
            if (voice == null) return;
            voice.volume = currentSlot >= 0
                ? Profile.VolumeFor(LabJigwiABTimeline.KindForSlot(currentSlot)) * LabAudioSettings.SfxVolume : 0f;
        }
        private static LabJigwiABRound FindRound(LabJigwiABSnapshot state, int slot)
        {
            foreach (var item in state.rounds ?? Array.Empty<LabJigwiABRound>()) if (item != null && item.slot == slot) return item;
            return null;
        }
        private bool Consumed(int slot) => (consumedSlots & 1 << slot) != 0;
        private void Consume(int slot) => consumedSlots |= (byte)(1 << slot);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public void StopCurrent()
        {
            if (voice != null && (currentSlot >= 0 || nativePaused || voice.clip != null))
            { voice.Stop(); voice.clip = null; }
            currentSlot = -1; nativePaused = false;
            voiceStartAt = activeAt = voiceEndAt = pausedBattleTime = -1d;
        }
        private void PauseCurrent(double now)
        {
            if (currentSlot < 0 || nativePaused) return;
            pausedBattleTime = now;
            Seek(now); voice.Pause(); nativePaused = true; PauseCount++;
        }
        private void OnApplicationPause(bool value)
        {
            applicationPaused = value;
            if (value) PauseCurrent(lastObservedNow);
        }
        private void OnApplicationFocus(bool value)
        {
            applicationFocused = value;
            if (!value) PauseCurrent(lastObservedNow);
        }
        private void OnDisable() { LabAudioSettings.Changed -= ApplyVolume; StopCurrent(); }
        private void OnDestroy()
        {
            LabAudioSettings.Changed -= ApplyVolume;
            StopCurrent();
            if (voiceRoot != null)
            {
                voiceRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(voiceRoot.gameObject); else DestroyImmediate(voiceRoot.gameObject);
            }
            if (fallback != null)
            {
                if (Application.isPlaying) Destroy(fallback); else DestroyImmediate(fallback);
            }
        }
    }
}
