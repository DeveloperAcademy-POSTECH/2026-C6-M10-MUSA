using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 섬광 효과음 (#338).
    /// - 섬광 해결: 이번 섬광에서 내가 흔든 횟수가 DEV 최대 횟수(기본 10번, 오색구슬 보상까지)를 채운 순간, 내 기기에서만
    ///   (LabYaguangFlashHud.ShakeCount). 눈부심이 먼저 풀려도 이어서 끝까지 흔들어야 난다.
    ///   다음 섬광·방해 종료·라운드 종료로 풀릴 때는 나지 않는다.
    /// - 오색구슬 피격: 내가 던진 오색구슬(#329)이 박에 맞았을 때, 던진 사람 기기에서만 (LabNetwork.LocalFiveColorHit, 진동과 같은 때).
    ///   #395에서 국립국악원 징으로 바꿨고, 여음이 길어 앞 징이 울리는 중에 또 맞으면 끊고 새로 친다.
    /// 소리 파일은 Resources/Audio/FlashClear(여러 개면 무작위), Resources/Audio/FiveColorHit(여러 개면 이름 순서대로
    /// 번갈아) 폴더의 파일을 쓰고, 없으면 조용히 넘어간다. 크기는 volume × 일시정지 메뉴 효과음 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabFlashRewardSound : MonoBehaviour
    {
        public const string FlashClearFolder = "Audio/FlashClear";
        public const string FiveColorHitFolder = "Audio/FiveColorHit";

        [Tooltip("섬광 해결 소리 크기")]
        [Range(0f, 1f)] public float flashClearVolume = 1f;
        [Tooltip("오색구슬 피격 소리 크기")]
        [Range(0f, 1f)] public float fiveColorHitVolume = 1f;

        private AudioClip[] flashClearClips, fiveColorHitClips;
        private AudioSource source;
        // 오색구슬 명중 징 (#395): 여음이 길어서 따로 둔다. 다 울리기 전에 또 맞으면 앞 소리를 끊고 새로 친다.
        private AudioSource hitSource;
        private LabNetwork network;
        private int fiveColorHitNext;
        private LabYaguangFlashHud flashHud;
        private int seenShakes;

        public int FlashClearClipCount => flashClearClips == null ? 0 : flashClearClips.Length;
        public int FiveColorHitClipCount => fiveColorHitClips == null ? 0 : fiveColorHitClips.Length;
        /// <summary>섬광 해결을 알아챈 횟수 (테스트·확인용). 소리 파일이 없어도 센다.</summary>
        public int FlashClearCount { get; private set; }
        /// <summary>오색구슬 피격을 알아챈 횟수 (테스트·확인용). 소리 파일이 없어도 센다.</summary>
        public int FiveColorHitCount { get; private set; }

        public void Bind(LabNetwork target)
        {
            if (network != null) network.LocalFiveColorHit -= HandleFiveColorHit;
            network = target;
            if (network != null && isActiveAndEnabled) network.LocalFiveColorHit += HandleFiveColorHit;
        }

        private void Awake()
        {
            flashClearClips = Resources.LoadAll<AudioClip>(FlashClearFolder);
            fiveColorHitClips = Resources.LoadAll<AudioClip>(FiveColorHitFolder);
            // 번갈아 낼 순서가 기기마다 같도록 이름 순으로 둔다 (FiveColorHit_1, FiveColorHit_2, ...).
            System.Array.Sort(fiveColorHitClips, (a, b) => string.CompareOrdinal(a.name, b.name));
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리
            hitSource = gameObject.AddComponent<AudioSource>();
            hitSource.playOnAwake = false;
            hitSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (network != null) network.LocalFiveColorHit += HandleFiveColorHit;
        }

        private void OnDisable()
        {
            if (network != null) network.LocalFiveColorHit -= HandleFiveColorHit;
        }

        private void Update()
        {
            if (flashHud == null) flashHud = GetComponent<LabYaguangFlashHud>();
            if (flashHud == null || network == null || network.config == null) return;
            if (ObserveShakes(flashHud.ShakeCount, network.config.YaguangFlashShakeCount)) OnFlashCleared();
        }

        /// <summary>
        /// 이번 섬광에서 흔든 수(count)가 최대 횟수(needed)에 막 닿았으면 true (한 번만). 새 섬광이라 수가 0으로
        /// 돌아가면 다시 셀 수 있다.
        /// </summary>
        public bool ObserveShakes(int count, int needed)
        {
            bool reached = needed > 0 && seenShakes < needed && count >= needed;
            seenShakes = count;
            return reached;
        }

        /// <summary>이번 섬광에서 최대 횟수까지 흔들었다. 소리를 냈으면 true.</summary>
        public bool OnFlashCleared()
        {
            FlashClearCount++;
            return Play(flashClearClips, flashClearVolume);
        }

        /// <summary>오색구슬 피격 소리 중 다음에 낼 차례 (0부터). 소리가 여러 개면 맞을 때마다 번갈아 낸다.</summary>
        public int NextFiveColorHitIndex => fiveColorHitNext;

        /// <summary>
        /// 내가 던진 오색구슬이 박에 맞았다. 소리를 냈으면 true. 소리가 여러 개면 이름 순서대로 번갈아 낸다.
        /// 징(#395)이 다 울리기 전에 또 맞으면 앞 소리를 끊고 새 소리를 처음부터 낸다.
        /// </summary>
        public bool OnFiveColorHit()
        {
            FiveColorHitCount++;
            int count = FiveColorHitClipCount;
            int index = count > 0 ? fiveColorHitNext % count : 0;
            fiveColorHitNext = count > 0 ? (index + 1) % count : 0;
            if (count == 0 || hitSource == null) return false;
            // 앞 징이 아직 울려도 끊고 처음부터 새로 친다 (#395).
            hitSource.Stop();
            hitSource.clip = fiveColorHitClips[index];
            hitSource.volume = fiveColorHitVolume * LabAudioSettings.SfxVolume;
            hitSource.Play();
            return true;
        }

        private void HandleFiveColorHit() => OnFiveColorHit();

        private bool Play(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0 || source == null) return false;
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
            return true;
        }
    }
}
