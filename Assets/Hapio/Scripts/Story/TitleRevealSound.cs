using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// #573 타이틀 등장음: 이 오브젝트(타이틀 그림)가 화면에 나타날 때 《합이오》 로고 등장음을 한 번 낸다.
    /// 앱을 켠 뒤 처음 한 번만 낼지(onlyFirstTime), 타이틀로 돌아올 때마다 낼지 고른다. 효과음 크기 설정(LabAudioSettings.SfxVolume)을 따른다.
    /// 타이틀 코드와 따로 돈다: 이 컴포넌트를 떼면 소리만 사라진다.
    /// Home에는 일부러 AudioListener가 없다(오프닝이 보일 때만 오프닝 쪽에 하나 생김). 그래서 소리를 낼 때 켜진 리스너가 하나도 없으면
    /// 이 소리가 끝날 때까지만 이 오브젝트에 리스너를 잠깐 붙이고, 끝나거나 다른 리스너가 켜지면 바로 뗀다(두 개가 동시에 있지 않게).
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
        private float playAt = -1f, listenUntil = -1f;
        private AudioListener borrowed;

        /// <summary>테스트용: 소리를 낸 횟수(파일이 없어도 센다).</summary>
        public int PlayCount { get; private set; }

        private void OnEnable()
        {
            if (onlyFirstTime && played) return;
            playAt = Time.unscaledTime + delaySeconds;
        }

        private void OnDisable() { playAt = -1f; ReturnListener(); }

        private void Update()
        {
            if (borrowed != null && (Time.unscaledTime >= listenUntil || AnotherListener())) ReturnListener();
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
            if (!AnotherListener() && borrowed == null) borrowed = gameObject.AddComponent<AudioListener>();
            if (borrowed != null) listenUntil = Time.unscaledTime + clip.length + .1f;
            source.PlayOneShot(clip, volume * LabAudioSettings.SfxVolume);
        }

        /// <summary>이 오브젝트가 잠깐 붙인 것 말고 켜진 AudioListener가 있는지.</summary>
        private bool AnotherListener()
        {
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener != borrowed && listener.isActiveAndEnabled) return true;
            return false;
        }

        private void ReturnListener()
        {
            if (borrowed != null) Destroy(borrowed);
            borrowed = null; listenUntil = -1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => played = false;
    }
}
