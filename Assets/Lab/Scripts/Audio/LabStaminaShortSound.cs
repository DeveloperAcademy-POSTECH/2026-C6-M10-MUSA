using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 스태미나 부족 소리 (#418). 북을 눌렀는데 스태미나가 모자라 Host가 생성을 거절하면 누른 사람 기기에서만 난다.
    /// 연달아 누르면 앞 소리를 끊고 처음부터 다시 내서 겹쳐 커지지 않는다. 소리 파일은 Resources/Audio/StaminaShort 폴더의
    /// 첫 파일이고, 없으면 조용히 넘어간다. 크기는 LabConfig의 Stamina Short Sound Volume × 일시정지 메뉴 SFX 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabStaminaShortSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/StaminaShort";

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Stamina Short Sound Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = 1f;

        private AudioClip clip;
        private AudioSource source;
        private LabNetwork network;

        public bool HasClip => clip != null;
        /// <summary>소리를 낸 횟수 (테스트·확인용)</summary>
        public int PlayCount { get; private set; }

        private float BaseVolume => network != null && network.config != null ? network.config.StaminaShortSoundVolume : volume;

        public void Bind(LabNetwork target)
        {
            if (network != null) network.LocalStaminaShort -= Play;
            network = target;
            if (network != null) network.LocalStaminaShort += Play;
        }

        private void Awake()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(ClipFolder);
            clip = clips.Length > 0 ? clips[0] : null;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        public void Play()
        {
            if (clip == null || source == null) return;
            PlayCount++;
            source.Stop();
            source.clip = clip;
            source.volume = BaseVolume * LabAudioSettings.SfxVolume;
            source.Play();
        }

        private void OnDestroy()
        {
            if (network != null) network.LocalStaminaShort -= Play;
        }
    }
}
