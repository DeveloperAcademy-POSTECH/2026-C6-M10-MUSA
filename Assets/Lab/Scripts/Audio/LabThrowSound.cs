using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내가 던진 구슬의 결과 소리. 던진 사람 기기에서만 난다.
    /// - 요괴에 맞으면: Resources/Audio/Hit 폴더의 소리 중 하나 (여러 개면 무작위, 연속 같은 소리 X)
    /// - 빗나가면: Resources/Audio/Miss 폴더의 소리 중 하나 (같은 방식)
    /// 소리 파일이 없으면 조용히 넘어간다. 크기는 기본 크기 × 일시정지 메뉴 SFX 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabThrowSound : MonoBehaviour
    {
        public const string HitFolder = "Audio/Hit";
        public const string MissFolder = "Audio/Miss";

        // LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Hit/Miss Sound Volume을 쓴다.
        [Range(0f, 1f)] public float hitVolume = .9f;
        [Range(0f, 1f)] public float missVolume = .7f;

        private LabConfig Config => network != null ? network.config : null;
        private float HitBase => Config != null ? Config.HitSoundVolume : hitVolume;
        private float MissBase => Config != null ? Config.MissSoundVolume : missVolume;

        private AudioClip[] hitClips;
        private AudioClip[] missClips;
        private int lastMissIndex = -1;
        private AudioSource source;
        private LabNetwork network;
        private int lastHitIndex = -1;

        public int HitClipCount => hitClips == null ? 0 : hitClips.Length;
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
            missClips = Resources.LoadAll<AudioClip>(MissFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
        }

        private void OnLocalThrowHit(int damage) => PlayHit();

        public void PlayHit() => PlayRandom(hitClips, ref lastHitIndex, HitBase);
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
