using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내가 던진 구슬의 결과 소리. 던진 사람 기기에서만 난다.
    /// - 요괴에 맞으면: 박 모드가 Paint면 Resources/Audio/Hit(물감 터지는 소리), Crack이면 Resources/Audio/HitCrack 폴더의
    ///   소리 중 하나 (여러 개면 무작위, 연속 같은 소리 X). 한쪽 폴더가 비어 있으면 다른 쪽 소리를 쓴다.
    ///   Paint에서 박의 옆을 스쳐 맞으면(LabConfig Glancing Hit Offset 이상) Resources/Audio/HitGlance 소리를 쓴다.
    ///   콤보 실패 페널티(SMALL, #222) 중에 던진 내 공이 맞으면, 같은 타격음을 Inspector 반음(기본 +6)만큼 높여 작은 공처럼 낸다 (#398).
    ///   콤보 성공 보상(BIG) 중에 던진 큰 공이 맞으면 물감 소리 대신 징(Resources/Audio/FiveColorHit)을 낸다 (#398). 징은 길어서
    ///   새로 맞으면 앞 징을 끊고 다시 친다. 오색구슬이면 오색구슬 징(LabFlashRewardSound)이 따로 나므로 겹쳐 내지 않는다.
    ///   작은 공/큰 공인지는 맞은 때가 아니라 던질 때 정해진 공 크기로 본다 (LabNetwork.LastLocalHitSmall/Big).
    /// - 빗나가면: Resources/Audio/Miss 폴더의 소리 중 하나 (같은 방식)
    /// 소리 파일이 없으면 조용히 넘어간다. 크기는 기본 크기 × 일시정지 메뉴 SFX 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabThrowSound : MonoBehaviour
    {
        public const string HitFolder = "Audio/Hit";
        public const string CrackHitFolder = "Audio/HitCrack";
        // Audio/Hit 아래에 두면 LoadAll이 하위 폴더까지 읽어 일반 타격음에 섞이므로 옆 폴더로 둔다.
        public const string GlanceHitFolder = "Audio/HitGlance";
        // 박 원기둥(Unity 기본 Cylinder)의 로컬 반지름.
        private const float HitboxLocalRadius = .5f;
        public const string MissFolder = "Audio/Miss";
        /// <summary>BIG 중 던진 큰 공의 소리 (#398 징, #464부터 25현 가야금). 오색구슬 명중과 같은 폴더를 쓴다.</summary>
        public const string BigHitFolder = LabFlashRewardSound.FiveColorHitFolder;

        // LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Hit/Miss Sound Volume을 쓴다.
        [Range(0f, 1f)] public float hitVolume = .9f;
        [Range(0f, 1f)] public float missVolume = .7f;

        private LabConfig Config => network != null ? network.config : null;
        private float HitBase => Config != null ? Config.HitSoundVolume : hitVolume;
        private float MissBase => Config != null ? Config.MissSoundVolume : missVolume;

        private AudioClip[] hitClips;
        private AudioClip[] crackHitClips;
        private AudioClip[] glanceHitClips;
        private int lastGlanceIndex = -1;
        private AudioClip[] missClips;
        private AudioClip[] bigHitClips;
        private int lastBigIndex = -1;
        private AudioSource bigSource;
        private int lastMissIndex = -1;
        private AudioSource source;
        private LabNetwork network;
        private int lastHitIndex = -1;
        private AudioClip[] lastHitClips;

        public int HitClipCount => hitClips == null ? 0 : hitClips.Length;
        public int CrackHitClipCount => crackHitClips == null ? 0 : crackHitClips.Length;
        public int GlanceHitClipCount => glanceHitClips == null ? 0 : glanceHitClips.Length;
        /// <summary>마지막 타격음이 스친 타격 소리였는지 (테스트·확인용).</summary>
        public bool LastHitWasGlance { get; private set; }
        /// <summary>마지막 타격음이 SMALL 중 던진 작은 공 소리였는지 (테스트·확인용).</summary>
        public bool LastHitWasSmall { get; private set; }
        /// <summary>마지막 타격음이 BIG 중 던진 큰 공의 징이었는지 (테스트·확인용).</summary>
        public bool LastHitWasBig { get; private set; }
        public int BigHitClipCount { get { EnsureBigClips(); return bigHitClips.Length; } }

        // Awake 전(에디터 테스트 등)에 불려도 징 파일을 읽을 수 있게 처음 쓸 때 읽는다.
        private void EnsureBigClips()
        {
            if (bigHitClips == null) bigHitClips = Resources.LoadAll<AudioClip>(BigHitFolder);
        }
        /// <summary>마지막 타격음의 재생 높이 (1 = 원래 높이, 테스트·확인용).</summary>
        public float LastHitPitch { get; private set; } = 1f;
        public int MissClipCount => missClips == null ? 0 : missClips.Length;

        public void Bind(LabNetwork target)
        {
            if (network != null)
            {
                network.LocalThrowHit -= OnLocalThrowHit;
                network.LocalThrowMissed -= PlayMiss;
            }
            network = target;
            if (network != null)
            {
                network.LocalThrowHit += OnLocalThrowHit;
                network.LocalThrowMissed += PlayMiss;
            }
        }

        private void Awake()
        {
            hitClips = Resources.LoadAll<AudioClip>(HitFolder);
            crackHitClips = Resources.LoadAll<AudioClip>(CrackHitFolder);
            glanceHitClips = Resources.LoadAll<AudioClip>(GlanceHitFolder);
            missClips = Resources.LoadAll<AudioClip>(MissFolder);
            EnsureBigClips();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
            // 큰 공 징은 3초쯤 길어서 따로 두고, 새로 맞으면 앞 징을 끊는다.
            bigSource = gameObject.AddComponent<AudioSource>();
            bigSource.playOnAwake = false;
            bigSource.spatialBlend = 0f;
        }

        private void OnLocalThrowHit(int damage) => PlayHit(network != null ? network.LastLocalHitOffset : 0f,
            network != null && network.LastLocalHitSmall,
            network != null && network.LastLocalHitBig && !network.LastLocalHitFiveColor);

        /// <summary>던질 때 공 크기 배율이 콤보 보상(BIG)처럼 1보다 크면 큰 공.</summary>
        public static bool IsBigThrow(float sizeScale) => sizeScale > 1.001f;

        /// <summary>던질 때 공 크기 배율(sizeScale)이 콤보 페널티(SMALL)처럼 1보다 작으면 작은 공. 0은 옛 패킷이라 보통 공.</summary>
        public static bool IsSmallThrow(float sizeScale) => sizeScale > 0f && sizeScale < .999f;

        /// <summary>작은 공 타격음 높이: semitones 반음만큼 높인 재생 속도 (0~12반음).</summary>
        public static float SmallHitPitch(float semitones) => Mathf.Pow(2f, Mathf.Clamp(semitones, 0f, 12f) / 12f);

        /// <summary>
        /// 맞은 자리가 박 중심축에서 날아온 방향 기준으로 얼마나 옆으로 벗어났는지. 0 = 정중앙, 1 = 가장자리를 스침.
        /// 박 원기둥의 로컬 좌표로 받은 맞은 자리와 구슬 속도를 위에서 내려다본 평면(x, z)에서 계산한다.
        /// </summary>
        public static float HitOffset(Vector3 localPoint, Vector3 localVelocity)
        {
            Vector2 point = new Vector2(localPoint.x, localPoint.z);
            Vector2 direction = new Vector2(localVelocity.x, localVelocity.z);
            if (!IsFinite(point) || !IsFinite(direction) || direction.sqrMagnitude < 1e-6f) return 0f;
            direction.Normalize();
            float sideways = Mathf.Abs(point.x * direction.y - point.y * direction.x);
            return Mathf.Clamp01(sideways / HitboxLocalRadius);
        }

        public static bool IsGlancing(float offset, float threshold) => offset >= threshold;

        private static bool IsFinite(Vector2 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y);

        /// <summary>페인트 타격음. 옆을 스치면 스친 소리, SMALL 중 던진 공이면 같은 소리를 높여서 낸다.</summary>
        public void PlayHit(float offset = 0f, bool small = false, bool big = false)
        {
            if (big) EnsureBigClips();
            LastHitWasBig = big && !small && bigHitClips != null && bigHitClips.Length > 0;
            if (LastHitWasBig)
            {
                LastHitWasSmall = LastHitWasGlance = false;
                LastHitPitch = 1f;
                PlayBig();
                return;
            }
            LastHitWasSmall = small;
            LastHitPitch = small ? SmallHitPitch(Config != null ? Config.SmallHitPitchSemitones : 6f) : 1f;
            if (source != null) source.pitch = LastHitPitch;
            float threshold = Config != null ? Config.GlancingHitOffset : .7f;
            LastHitWasGlance = glanceHitClips != null && glanceHitClips.Length > 0
                && IsGlancing(offset, threshold);
            if (LastHitWasGlance)
            {
                PlayRandom(glanceHitClips, ref lastGlanceIndex, HitBase);
                return;
            }
            AudioClip[] clips = hitClips;
            if (clips == null || clips.Length == 0) clips = crackHitClips;
            if (clips != lastHitClips) lastHitIndex = -1;
            lastHitClips = clips;
            PlayRandom(clips, ref lastHitIndex, HitBase);
        }
        public void PlayMiss()
        {
            if (source != null) source.pitch = 1f;
            PlayRandom(missClips, ref lastMissIndex, MissBase);
        }

        private void PlayBig()
        {
            if (bigSource == null) return;
            int index = Random.Range(0, bigHitClips.Length);
            if (bigHitClips.Length > 1 && index == lastBigIndex)
                index = (index + 1 + Random.Range(0, bigHitClips.Length - 1)) % bigHitClips.Length;
            lastBigIndex = index;
            bigSource.Stop();
            bigSource.clip = bigHitClips[index];
            bigSource.volume = (Config != null ? Config.BigHitSoundVolume : .8f) * LabAudioSettings.SfxVolume;
            bigSource.Play();
        }

        private void PlayRandom(AudioClip[] clips, ref int lastIndex, float volume)
        {
            if (clips == null || clips.Length == 0 || source == null) return;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex)
                index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            lastIndex = index;
            source.PlayOneShot(clips[index], volume * LabAudioSettings.SfxVolume);
        }

        private void OnDestroy()
        {
            if (network == null) return;
            network.LocalThrowHit -= OnLocalThrowHit;
            network.LocalThrowMissed -= PlayMiss;
        }
    }
}
