using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내 판에서 폭탄이 터질 때 "뻥이요"(또는 폭발음)를 낸다. 터진 판의 주인 기기에서만 난다
    /// (LabNetwork.BombBlasted는 내 판에서 터졌을 때만 알린다).
    /// 소리 파일은 Resources/Audio/Bomb 폴더에 넣으면 자동으로 쓰이고(여러 개면 무작위, 연속 같은 소리 X),
    /// 파일이 없으면 조용히 넘어간다. 크기는 LabConfig의 Bomb Sound Volume × 일시정지 메뉴 SFX 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBombSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/Bomb";

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Bomb Sound Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = .9f;

        private AudioClip[] clips;
        private AudioSource source;
        private LabNetwork network;
        private int lastIndex = -1;

        public int ClipCount => clips == null ? 0 : clips.Length;
        public int PlayCount { get; private set; }

        private float BaseVolume => network != null && network.config != null ? network.config.BombSoundVolume : volume;

        public void Bind(LabNetwork target)
        {
            if (network != null) network.BombBlasted -= OnBombBlasted;
            network = target;
            if (network != null) network.BombBlasted += OnBombBlasted;
        }

        private void Awake()
        {
            clips = Resources.LoadAll<AudioClip>(ClipFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnBombBlasted(string bombId, Vector2 position) => PlayRandom();

        public void PlayRandom()
        {
            if (clips == null || clips.Length == 0 || source == null) return;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex)
                index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            lastIndex = index;
            PlayCount++;
            source.PlayOneShot(clips[index], BaseVolume * LabAudioSettings.SfxVolume);
        }

        private void OnDestroy()
        {
            if (network != null) network.BombBlasted -= OnBombBlasted;
        }
    }
}
