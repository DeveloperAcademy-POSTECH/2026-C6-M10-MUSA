using UnityEngine;

namespace C6Lab
{
    /// <summary>폭탄 소리 종류. Boom: 일반 폭발음, Ppeong: 뻥튀기 기계 소리</summary>
    public enum LabBombSoundStyle { Boom = 0, Ppeong = 1 }

    /// <summary>
    /// 내 판에서 폭탄이 터질 때 "뻥이요"(또는 폭발음)를 낸다. 터진 판의 주인 기기에서만 난다
    /// (LabNetwork.BombBlasted는 내 판에서 터졌을 때만 알린다).
    /// 소리 종류는 LabConfig의 Bomb Sound(Inspector · DEV 토글 BOOM/PPEONG)로 고르고,
    /// 각각 Resources/Audio/Bomb/Boom, Resources/Audio/Bomb/Ppeong 폴더의 파일을 쓴다(여러 개면 무작위, 연속 같은 소리 X).
    /// 고른 쪽 파일이 없으면 다른 쪽을, 둘 다 없으면 조용히 넘어간다. 크기는 Bomb Sound Volume × 일시정지 메뉴 SFX 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBombSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/Bomb";
        public const string BoomFolder = "Audio/Bomb/Boom";
        public const string PpeongFolder = "Audio/Bomb/Ppeong";

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Bomb Sound Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = .9f;

        private AudioClip[] boomClips;
        private AudioClip[] ppeongClips;
        private AudioClip[] lastClips;
        private AudioSource source;
        private LabNetwork network;
        private int lastIndex = -1;

        public int ClipCount => (boomClips == null ? 0 : boomClips.Length) + (ppeongClips == null ? 0 : ppeongClips.Length);
        /// <summary>마지막으로 낸 소리 이름 (테스트·확인용)</summary>
        public string LastClipName { get; private set; }

        /// <summary>설정에 맞는 소리 묶음. 고른 쪽이 비어 있으면 다른 쪽.</summary>
        public static AudioClip[] Choose(LabBombSoundStyle style, AudioClip[] boom, AudioClip[] ppeong)
        {
            AudioClip[] chosen = style == LabBombSoundStyle.Boom ? boom : ppeong;
            if (chosen != null && chosen.Length > 0) return chosen;
            return style == LabBombSoundStyle.Boom ? ppeong : boom;
        }
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
            boomClips = Resources.LoadAll<AudioClip>(BoomFolder);
            ppeongClips = Resources.LoadAll<AudioClip>(PpeongFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnBombBlasted(string bombId, Vector2 position) => PlayRandom();

        public void PlayRandom()
        {
            LabBombSoundStyle style = network != null && network.config != null
                ? network.config.BombSoundStyle : LabBombSoundStyle.Ppeong;
            AudioClip[] clips = Choose(style, boomClips, ppeongClips);
            if (clips == null || clips.Length == 0 || source == null) return;
            if (clips != lastClips) lastIndex = -1;
            lastClips = clips;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex)
                index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            lastIndex = index;
            PlayCount++;
            LastClipName = clips[index].name;
            source.PlayOneShot(clips[index], BaseVolume * LabAudioSettings.SfxVolume);
        }

        private void OnDestroy()
        {
            if (network != null) network.BombBlasted -= OnBombBlasted;
        }
    }
}
