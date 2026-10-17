using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내가 던진 구슬의 결과 소리. 던진 사람 기기에서만 난다.
    /// - 요괴에 맞으면: 박 모드가 Paint면 Resources/Audio/Hit(물감 터지는 소리), Crack이면 Resources/Audio/HitCrack 폴더의
    ///   소리 중 하나 (여러 개면 무작위, 연속 같은 소리 X). 한쪽 폴더가 비어 있으면 다른 쪽 소리를 쓴다.
    ///   Paint에서 박의 옆을 스쳐 맞으면(LabConfig Glancing Hit Offset 이상) Resources/Audio/HitGlance 소리를 쓴다.
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
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnLocalThrowHit(int damage) => PlayHit(network != null ? network.LastLocalHitOffset : 0f);

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

        /// <summary>박 모드에 맞는 타격음: Paint는 물감 소리(옆을 스치면 스친 소리), Crack은 기존 타격음.</summary>
        public void PlayHit(float offset = 0f)
        {
            bool crack = Config != null && Config.GourdMode == LabGourdMode.Crack;
            float threshold = Config != null ? Config.GlancingHitOffset : .7f;
            LastHitWasGlance = !crack && glanceHitClips != null && glanceHitClips.Length > 0
                && IsGlancing(offset, threshold);
            if (LastHitWasGlance)
            {
                PlayRandom(glanceHitClips, ref lastGlanceIndex, HitBase);
                return;
            }
            AudioClip[] clips = crack ? crackHitClips : hitClips;
            if (clips == null || clips.Length == 0) clips = crack ? hitClips : crackHitClips;
            if (clips != lastHitClips) lastHitIndex = -1;
            lastHitClips = clips;
            PlayRandom(clips, ref lastHitIndex, HitBase);
        }
        public void PlayMiss() => PlayRandom(missClips, ref lastMissIndex, MissBase);

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
