using UnityEngine;

namespace C6Lab
{
    /// <summary>One local B-curtain loop. Presentation state never changes the battle or the shared clock.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class LabJigwiGroupFireAudio : MonoBehaviour
    {
        public const string ClipResource = "Jigwi/GroupFireLoop";
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = .6f;
        [SerializeField] private AudioSource loopSource;
        private string eventId;
        private bool wanted, held, started, paused;
        private bool applicationPaused, applicationFocused = true;

        public AudioSource Source => loopSource;
        public bool WantsLoop => wanted;
        public bool IsPaused => wanted && (held || applicationPaused || !applicationFocused);
        /// <summary>Native Play calls, independent of whether an audio output device is available.</summary>
        public int PlaybackStartCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public string FireEventId => eventId;

        /// <summary>Also called by the prefab builder to save the passive source and imported clip.</summary>
        public void EnsureSource()
        {
            if (loopSource == null || loopSource.gameObject != gameObject) loopSource = GetComponent<AudioSource>();
            if (loopSource == null) loopSource = gameObject.AddComponent<AudioSource>();
            if (clip == null) clip = Resources.Load<AudioClip>(ClipResource);
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.spatialBlend = 0f;
            loopSource.dopplerLevel = 0f;
            loopSource.pitch = 1f;
            loopSource.volume = SafeVolume() * LabAudioSettings.SfxVolume;
            if (loopSource.clip != clip)
            {
                loopSource.Stop();
                loopSource.clip = clip;
                started = paused = false;
            }
        }

        public void Apply(string fireEventId, bool visible, bool pauseHeld, int visualLevel)
        {
            EnsureSource();
            if (!isActiveAndEnabled || !visible || visualLevel <= 0 || string.IsNullOrEmpty(fireEventId) || clip == null)
            { Stop(); return; }
            if (eventId != fireEventId)
            {
                Stop();
                eventId = fireEventId;
            }
            wanted = true;
            held = pauseHeld;
            SynchronizePlayback();
        }

        public void Stop()
        {
            if (loopSource != null) loopSource.Stop(); // Also releases a natively paused loop.
            eventId = null;
            wanted = held = started = paused = false;
        }

        private void SynchronizePlayback()
        {
            if (loopSource == null || !wanted || clip == null) return;
            loopSource.volume = SafeVolume() * LabAudioSettings.SfxVolume;
            if (held || applicationPaused || !applicationFocused)
            {
                if (started && !paused) { loopSource.Pause(); PauseCount++; }
                paused = true;
                return;
            }
            if (!started)
            {
                loopSource.Play(); PlaybackStartCount++;
                started = true; paused = false;
            }
            else if (paused)
            {
                loopSource.UnPause(); ResumeCount++;
                paused = false;
            }
        }

        private float SafeVolume() => float.IsNaN(volume) || float.IsInfinity(volume) ? .6f : Mathf.Clamp01(volume);
        private void Awake() => EnsureSource();
        private void OnEnable()
        {
            applicationPaused = false;
            applicationFocused = Application.isFocused;
            LabAudioSettings.Changed += OnAudioSettingsChanged;
        }
        private void OnDisable() { LabAudioSettings.Changed -= OnAudioSettingsChanged; Stop(); }
        private void OnDestroy() => Stop();
        private void OnAudioSettingsChanged() { if (loopSource != null) loopSource.volume = SafeVolume() * LabAudioSettings.SfxVolume; }
        private void OnApplicationPause(bool value) { applicationPaused = value; SynchronizePlayback(); }
        private void OnApplicationFocus(bool value) { applicationFocused = value; SynchronizePlayback(); }
    }
}
