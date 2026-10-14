using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내가 던진 구슬의 결과 소리. 던진 사람 기기에서만 난다.
    /// - 요괴에 맞으면: Resources/Audio/Hit 폴더의 소리 중 하나 (여러 개면 무작위, 연속 같은 소리 X)
    /// 소리 파일이 없으면 조용히 넘어간다. 크기는 기본 크기 × 일시정지 메뉴 SFX 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabThrowSound : MonoBehaviour
    {
        public const string HitFolder = "Audio/Hit";

        [Range(0f, 1f)] public float hitVolume = .9f;

        private AudioClip[] hitClips;
        private AudioSource source;
        private LabNetwork network;
        private int lastHitIndex = -1;

        public int HitClipCount => hitClips == null ? 0 : hitClips.Length;

        public void Bind(LabNetwork target)
        {
            if (network != null) network.LocalThrowHit -= OnLocalThrowHit;
            network = target;
            if (network != null) network.LocalThrowHit += OnLocalThrowHit;
        }

        private void Awake()
        {
            hitClips = Resources.LoadAll<AudioClip>(HitFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnLocalThrowHit(int damage) => PlayHit();

        public void PlayHit() => PlayRandom(hitClips, ref lastHitIndex, hitVolume);

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
            if (network != null) network.LocalThrowHit -= OnLocalThrowHit;
        }
    }
}
