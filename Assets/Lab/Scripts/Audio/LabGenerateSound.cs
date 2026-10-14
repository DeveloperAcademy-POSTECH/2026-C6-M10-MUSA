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

        [Range(0f, 1f)] public float volume = .8f;

        private AudioClip[] clips;
        private AudioSource source;
        private LabOrbBoard board;
        private int lastIndex = -1;

        public int ClipCount => clips == null ? 0 : clips.Length;

        public void Bind(LabOrbBoard target)
        {
            if (board != null) board.SpawnStarted -= OnSpawnStarted;
            board = target;
            if (board != null) board.SpawnStarted += OnSpawnStarted;
        }

        private void Awake()
        {
            clips = Resources.LoadAll<AudioClip>(ClipFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 UI 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnSpawnStarted(string orbId) => PlayRandom();

        public void PlayRandom()
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
            if (board != null) board.SpawnStarted -= OnSpawnStarted;
        }
    }
}
