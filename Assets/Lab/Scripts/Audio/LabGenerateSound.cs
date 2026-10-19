using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내가 생성한 구슬이 떠오르는 순간 소리를 낸다. 평소에는 장구, 간간이 추임새("얼쑤"·"얼씨구")가 난다 (#201).
    /// 추임새는 생성할 때마다 LabConfig의 Chuimsae Chance(%) 확률로 나오되, 한 번 나온 뒤에는
    /// Chuimsae Min Gap 번만큼 장구만 내서 연달아 나오지 않는다.
    /// 장구는 Resources/Audio/Generate, 추임새는 Resources/Audio/GenerateVoice 폴더의 파일을 쓰고,
    /// 각 종류 안에서는 무작위로 고르되 같은 소리는 연속으로 나오지 않는다. 목소리 파일이 없으면 장구만 낸다.
    /// 생성이 거절되면 구슬이 생기지 않으므로 소리도 나지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGenerateSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/Generate";
        public const string VoiceFolder = "Audio/GenerateVoice";

        // LabConfig를 읽을 수 없을 때만 쓰는 기본값. 평소에는 LabConfig의 Chuimsae Chance / Min Gap을 쓴다.
        private const int FallbackChancePercent = 15;
        private const int FallbackMinGap = 5;

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Generate Sound Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = 1f;

        private AudioClip[] clips;
        private AudioClip[] voices;
        private int lastVoiceIndex = -1;
        private int sinceVoice; // 마지막 추임새 뒤로 장구가 몇 번 났는지
        private AudioSource source;
        private LabOrbBoard board;
        private LabNetwork network;
        private int lastIndex = -1;

        public int ClipCount => clips == null ? 0 : clips.Length;
        public int VoiceCount => voices == null ? 0 : voices.Length;
        /// <summary>마지막으로 낸 소리가 "얼쑤"였는지 (테스트용)</summary>
        public bool LastWasVoice { get; private set; }

        /// <summary>
        /// 이번 생성에서 추임새를 낼지. 마지막 추임새 뒤로 장구가 minGap 번 이상 났고,
        /// 0~1 사이 무작위 값(roll)이 확률(chancePercent %)보다 작으면 추임새다.
        /// </summary>
        public static bool ShouldPlayVoice(int sinceVoice, int minGap, int chancePercent, float roll) =>
            chancePercent > 0 && sinceVoice >= Mathf.Max(0, minGap) && roll * 100f < chancePercent;

        /// <summary>크기 설정(LabConfig)을 읽을 네트워크를 연결한다.</summary>
        public void BindConfig(LabNetwork target) => network = target;

        private float BaseVolume => network != null && network.config != null ? network.config.GenerateSoundVolume : volume;

        public void Bind(LabOrbBoard target)
        {
            if (board != null) board.SpawnStarted -= OnSpawnStarted;
            board = target;
            if (board != null) board.SpawnStarted += OnSpawnStarted;
        }

        private void Awake()
        {
            clips = Resources.LoadAll<AudioClip>(ClipFolder);
            voices = Resources.LoadAll<AudioClip>(VoiceFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 UI 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnSpawnStarted(string orbId) => PlayNext();

        /// <summary>다음 생성 소리: 평소 장구, 간간이 추임새</summary>
        public void PlayNext()
        {
            LabConfig config = network != null ? network.config : null;
            int chance = config != null ? config.ChuimsaeChancePercent : FallbackChancePercent;
            int minGap = config != null ? config.ChuimsaeMinGap : FallbackMinGap;
            bool voice = voices != null && voices.Length > 0
                && ShouldPlayVoice(sinceVoice, minGap, chance, Random.value);
            sinceVoice = voice ? 0 : sinceVoice + 1;
            LastWasVoice = voice;
            if (voice) Play(voices, ref lastVoiceIndex);
            else Play(clips, ref lastIndex);
        }

        /// <summary>예전 이름. PlayNext와 같다.</summary>
        public void PlayRandom() => PlayNext();

        private void Play(AudioClip[] pool, ref int last)
        {
            if (pool == null || pool.Length == 0 || source == null) return;
            int index = Random.Range(0, pool.Length);
            if (pool.Length > 1 && index == last)
                index = (index + 1 + Random.Range(0, pool.Length - 1)) % pool.Length;
            last = index;
            source.PlayOneShot(pool[index], BaseVolume * LabAudioSettings.SfxVolume);
        }

        private void OnDestroy()
        {
            if (board != null) board.SpawnStarted -= OnSpawnStarted;
        }
    }
}
