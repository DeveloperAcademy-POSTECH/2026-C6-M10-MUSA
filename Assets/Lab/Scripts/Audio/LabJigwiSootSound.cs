using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #713 지귀 연기 파편 / 검정 구슬 소리.
    /// - 기운: 내 판에 검정 구슬이 있는 동안 웅웅 반복 (가진 사람 기기만). 던지거나 일시정지면 천천히 줄어든다.
    /// - 명중: 검정 구슬이 박에 맞을 때 (모든 기기, LabNetwork.SootHit). 이때 오색구슬 가야금은 나지 않는다.
    /// - 드러남: 연기를 닦다가 밑에 깔린 파편이 드러날 때 (내 기기, LabNetwork.SootShardRevealed). 드러날수록 조금씩 높게.
    /// - 붙음 / 완성: 조각 둘이 붙을 때, 네 조각이 모여 검정 구슬이 될 때 (판 주인 기기, LabOrbBoard.SootJoined).
    /// 소리 파일은 Resources/Audio/JigwiSoot* 폴더의 파일을 쓰고(여러 개면 무작위), 없으면 조용히 넘어간다.
    /// 크기는 LabConfig(Inspector) × 일시정지 메뉴 효과음 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiSootSound : MonoBehaviour
    {
        public const string AuraFolder = "Audio/JigwiSootAura";
        public const string HitFolder = "Audio/JigwiSootHit";
        public const string RevealFolder = "Audio/JigwiSootReveal";
        public const string JoinFolder = "Audio/JigwiSootJoin";
        public const string WholeFolder = "Audio/JigwiSootWhole";
        public static readonly string[] ClipFolders = { AuraFolder, HitFolder, RevealFolder, JoinFolder, WholeFolder };

        /// <summary>기운이 차오르고 / 사라지는 데 걸리는 초.</summary>
        private const float AuraFadeSeconds = .5f;
        /// <summary>한 연기에서 드러날 때마다 높아지는 음 (반음 비율), 최대 네 번째까지.</summary>
        private static readonly float[] RevealPitches = { 1f, 1.12f, 1.26f, 1.34f };

        private AudioClip[] auraClips, hitClips, revealClips, joinClips, wholeClips;
        private AudioSource oneShots, revealSource, aura;
        private LabNetwork network;
        private LabOrbBoard board;
        private float auraLevel;
        private int revealsThisSmoke;
        private float lastRevealAt = -10f;

        /// <summary>확인용: 알아챈 횟수 (소리 파일이 없어도 센다).</summary>
        public int HitCount { get; private set; }
        public int RevealCount { get; private set; }
        public int JoinCount { get; private set; }
        public int WholeCount { get; private set; }
        /// <summary>지금 기운 소리 크기 비율 (0~1).</summary>
        public float AuraLevel => auraLevel;

        public void Bind(LabNetwork targetNetwork, LabOrbBoard targetBoard)
        {
            Unhook();
            network = targetNetwork;
            board = targetBoard;
            if (isActiveAndEnabled) Hook();
        }

        private void Awake()
        {
            auraClips = Resources.LoadAll<AudioClip>(AuraFolder);
            hitClips = Resources.LoadAll<AudioClip>(HitFolder);
            revealClips = Resources.LoadAll<AudioClip>(RevealFolder);
            joinClips = Resources.LoadAll<AudioClip>(JoinFolder);
            wholeClips = Resources.LoadAll<AudioClip>(WholeFolder);
            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            oneShots.spatialBlend = 0f;
            // The reveal climbs in pitch, so it has its own source (a source's pitch bends every sound it is playing).
            revealSource = gameObject.AddComponent<AudioSource>();
            revealSource.playOnAwake = false;
            revealSource.spatialBlend = 0f;
            aura = gameObject.AddComponent<AudioSource>();
            aura.playOnAwake = false;
            aura.loop = true;
            aura.spatialBlend = 0f;
            aura.volume = 0f;
        }

        private void OnEnable() => Hook();
        private void OnDisable() { Unhook(); auraLevel = 0f; if (aura != null) aura.Stop(); }

        private void Hook()
        {
            if (network != null) { network.SootHit += OnSootHit; network.SootShardRevealed += OnSootRevealed; }
            if (board != null) board.SootJoined += OnSootJoined;
        }

        private void Unhook()
        {
            if (network != null) { network.SootHit -= OnSootHit; network.SootShardRevealed -= OnSootRevealed; }
            if (board != null) board.SootJoined -= OnSootJoined;
        }

        private LabConfig Config => network != null ? network.config : null;

        private void Update()
        {
            bool want = HoldsBlackOrb(network);
            auraLevel = Mathf.MoveTowards(auraLevel, want ? 1f : 0f, Time.unscaledDeltaTime / AuraFadeSeconds);
            if (aura == null) return;
            if (auraLevel <= 0f) { if (aura.isPlaying) aura.Stop(); return; }
            if (!aura.isPlaying)
            {
                if (auraClips == null || auraClips.Length == 0) return;
                aura.clip = auraClips[Random.Range(0, auraClips.Length)];
                aura.time = 0f;
                aura.Play();
            }
            float volume = Config != null ? Config.JigwiSootAuraVolume : .6f;
            aura.volume = volume * auraLevel * LabAudioSettings.SfxVolume;
        }

        /// <summary>내 판에 던지지 않은 검정 구슬이 있고, 전투가 진행 중인지.</summary>
        public static bool HoldsBlackOrb(LabNetwork source)
        {
            LabSnapshot snapshot = source != null ? source.Snapshot : null;
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.paused || snapshot.orbs == null) return false;
            foreach (LabOrbState orb in snapshot.orbs)
                if (orb != null && orb.soot && !orb.inFlight && orb.owner == source.LocalPlayerId) return true;
            return false;
        }

        private void OnSootHit()
        {
            HitCount++;
            Play(oneShots, hitClips, Config != null ? Config.JigwiSootHitVolume : 1f, 1f);
        }

        private void OnSootRevealed(int count)
        {
            // 새 연기(이전 드러남에서 10초 넘게 지남)면 음 높이를 처음부터.
            if (Time.unscaledTime - lastRevealAt > 10f) revealsThisSmoke = 0;
            lastRevealAt = Time.unscaledTime;
            RevealCount += count;
            float pitch = RevealPitches[Mathf.Min(revealsThisSmoke, RevealPitches.Length - 1)];
            revealsThisSmoke += count;
            Play(revealSource, revealClips, Config != null ? Config.JigwiSootRevealVolume : .8f, pitch);
        }

        private void OnSootJoined(bool whole)
        {
            if (whole) { WholeCount++; Play(oneShots, wholeClips, Config != null ? Config.JigwiSootWholeVolume : 1f, 1f); }
            else { JoinCount++; Play(oneShots, joinClips, Config != null ? Config.JigwiSootJoinVolume : .8f, 1f); }
        }

        private static void Play(AudioSource source, AudioClip[] clips, float volume, float pitch)
        {
            if (clips == null || clips.Length == 0 || source == null || volume <= 0f) return;
            source.pitch = pitch;
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
        }
    }
}
