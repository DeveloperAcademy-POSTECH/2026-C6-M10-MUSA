using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// #573 타이틀 등장음: 이 오브젝트(타이틀 그림)가 화면에 나타날 때 《합이오》 로고 등장음을 한 번 낸다.
    /// 앱을 켠 뒤 처음 한 번만 낼지(onlyFirstTime), 타이틀로 돌아올 때마다 낼지 고른다. 효과음 크기 설정(LabAudioSettings.SfxVolume)을 따른다.
    /// 타이틀 코드와 따로 돈다: 이 컴포넌트를 떼면 소리만 사라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleRevealSound : MonoBehaviour
    {
        [Tooltip("로고 등장음 (Art/Story/Opening/Audio/OP_Title).")]
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("타이틀이 나타난 뒤 소리까지(초). 로고 애니메이션에 맞춘다.")]
        [Range(0f, 2f)] public float delaySeconds = .1f;
        [Tooltip("켜면 앱을 켠 뒤 처음 타이틀에서만 낸다. 끄면 타이틀로 돌아올 때마다 낸다.")]
        public bool onlyFirstTime = true;

        private static bool played;
        private AudioSource source;
        private float playAt = -1f;

        /// <summary>테스트용: 소리를 낸 횟수(파일이 없어도 센다).</summary>
        public int PlayCount { get; private set; }

        private void OnEnable()
        {
            if (onlyFirstTime && played) return;
            playAt = Time.unscaledTime + delaySeconds;
        }

        private void OnDisable() => playAt = -1f;

        private void Update()
        {
            if (playAt < 0f || Time.unscaledTime < playAt) return;
            playAt = -1f;
            played = true;
            PlayCount++;
            if (clip == null || volume <= 0f) return;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.PlayOneShot(clip, volume * LabAudioSettings.SfxVolume);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => played = false;
    }
}
