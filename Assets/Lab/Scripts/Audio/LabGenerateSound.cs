using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내가 생성한 구슬이 떠오르는 순간 장구 소리를 하나 무작위로 재생한다.
    /// 소리 파일은 Resources/Audio/Generate 폴더에 넣으면 자동으로 쓰이고, 같은 소리는 연속으로 나오지 않는다.
    /// 생성이 거절되면 구슬이 생기지 않으므로 소리도 나지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGenerateSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/Generate";

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Generate Sound Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = 1f;

        private AudioClip[] clips;
        private AudioSource source;
        private LabOrbBoard board;
        private LabNetwork network;
        private int lastIndex = -1;

        public int ClipCount => clips == null ? 0 : clips.Length;

        /// <summary>크기 설정(LabConfig)을 읽을 네트워크를 연결한다.</summary>
        public void BindConfig(LabNetwork target) => network = target;

        private float BaseVolume => network != null && network.config != null ? network.config.GenerateSoundVolume : volume;

        public void Bind(LabOrbBoard target)
        {
            if (board != null) board.SpawnRiseStarted -= OnSpawnRiseStarted;
            board = target;
            if (board != null) board.SpawnRiseStarted += OnSpawnRiseStarted;
        }

        private void Awake()
        {
            clips = Resources.LoadAll<AudioClip>(ClipFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 UI 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnSpawnRiseStarted(string orbId) => PlayRandom();

        public void PlayRandom()
        {
            if (clips == null || clips.Length == 0 || source == null) return;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex)
                index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            lastIndex = index;
            source.PlayOneShot(clips[index], BaseVolume * LabAudioSettings.SfxVolume);
        }

        private void OnDestroy()
        {
            if (board != null) board.SpawnRiseStarted -= OnSpawnRiseStarted;
        }
    }
}
