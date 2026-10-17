using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내가 생성한 구슬이 떠오르는 순간 소리를 낸다. 정해진 순서로 돌아간다: 장구 → 장구 → "얼쑤" → 장구 → 장구 → "얼쑤" …
    /// 장구는 Resources/Audio/Generate, "얼쑤" 목소리(추임새)는 Resources/Audio/GenerateVoice 폴더의 파일을 쓰고,
    /// 각 차례 안에서는 무작위로 고르되 같은 소리는 연속으로 나오지 않는다. 목소리 파일이 없으면 장구만 낸다.
    /// 생성이 거절되면 구슬이 생기지 않으므로 소리도 나지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGenerateSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/Generate";
        public const string VoiceFolder = "Audio/GenerateVoice";

        [Tooltip("몇 번째 생성마다 \"얼쑤\"를 낼지. 3이면 장구 → 장구 → 얼쑤 순서")]
        [Min(1)] public int voiceEvery = 3;

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Generate Sound Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = 1f;

        private AudioClip[] clips;
        private AudioClip[] voices;
        private int lastVoiceIndex = -1;
        private int playCount;
        private AudioSource source;
        private LabOrbBoard board;
        private LabNetwork network;
        private int lastIndex = -1;

        public int ClipCount => clips == null ? 0 : clips.Length;
        public int VoiceCount => voices == null ? 0 : voices.Length;
        /// <summary>마지막으로 낸 소리가 "얼쑤"였는지 (테스트용)</summary>
        public bool LastWasVoice { get; private set; }

        /// <summary>playIndex(0부터) 번째 생성이 "얼쑤" 차례인지. every = 3이면 2, 5, 8 … 번째(세 번째마다)</summary>
        public static bool IsVoiceTurn(int playIndex, int every) => every > 0 && (playIndex + 1) % every == 0;

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

        /// <summary>순서에 맞는 다음 소리 (장구 → 장구 → 얼쑤 …)</summary>
        public void PlayNext()
        {
            bool voice = IsVoiceTurn(playCount, voiceEvery) && voices != null && voices.Length > 0;
            playCount++;
            LastWasVoice = voice;
            if (voice) Play(voices, ref lastVoiceIndex);
            else Play(clips, ref lastIndex);
        }

        /// <summary>예전 이름. 순서대로 재생한다.</summary>
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
