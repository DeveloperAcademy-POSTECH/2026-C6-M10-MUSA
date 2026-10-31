using UnityEngine;

namespace C6Lab
{
    /// <summary>야광귀 스테이지 효과음의 종류 (#395~). 숫자는 폴더·크기 순서다. 새 소리는 뒤에 붙인다.</summary>
    public enum LabYaguangSfx { FlashWarn, FlashGlow, FlashCount, FlashBurst }

    /// <summary>
    /// 야광귀 섬광 예고와 번쩍 효과음 (#395). Host 스냅샷의 섬광 시각만 보고 소리를 낸다(화면 코드와 따로 돈다).
    /// - 예고 시작 (Audio/YaguangFlashWarn): 박 옆 말풍선에 야광귀 눈이 뜨는 순간, 모든 기기
    /// - 눈이 빛나기 시작 (Audio/YaguangFlashGlow): 예고 30% 지점, 눈이 노랗게 빛나고 가장자리 빛이 차오를 때, 맞을 사람 기기
    /// - 3/2/1 (Audio/YaguangFlashCount): 화면 가운데 숫자가 바뀔 때마다, 맞을 사람 기기. 숫자가 줄수록 음이 높아진다.
    /// - 번쩍 (Audio/YaguangFlashBurst): 섬광이 터진 순간, 맞은 사람 기기 (진동과 같은 때)
    /// 그 순간보다 LateSeconds 넘게 늦게 알아챈 기기(늦게 들어옴, 일시정지 뒤)는 내지 않는다. 일시정지 중·시작 대기 중에는
    /// 새로 내지 않는다. 폴더가 비어 있으면 그 소리만 조용히 넘어간다(여러 개면 무작위).
    /// 크기는 LabConfig Inspector의 소리별 크기 × 일시정지 메뉴 효과음 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabYaguangSound : MonoBehaviour
    {
        public const string FlashWarnFolder = "Audio/YaguangFlashWarn";
        public const string FlashGlowFolder = "Audio/YaguangFlashGlow";
        public const string FlashCountFolder = "Audio/YaguangFlashCount";
        public const string FlashBurstFolder = "Audio/YaguangFlashBurst";
        private static readonly string[] Folders = { FlashWarnFolder, FlashGlowFolder, FlashCountFolder, FlashBurstFolder };
        /// <summary>그 순간보다 이 초 넘게 늦게 알아채면 내지 않는다.</summary>
        public const double LateSeconds = .5d;

        private AudioClip[][] clips;
        private readonly int[] counts = new int[Folders.Length];
        private AudioSource source, countSource;
        private LabNetwork network;

        private string session;
        private uint round;
        private bool started;
        private uint seenFlashCount;
        private double warnFor = double.NaN, glowFor = double.NaN, countFor = double.NaN;
        private int lastCount;
        private string clockSession;
        private uint clockRound;
        private double seenServerTime = -1d;
        private float seenAt;

        public static string Folder(LabYaguangSfx sfx) => Folders[(int)sfx];
        public int ClipCount(LabYaguangSfx sfx) => clips == null ? 0 : clips[(int)sfx].Length;
        /// <summary>이 소리를 낼 순간을 알아챈 횟수 (테스트·확인용). 소리 파일이 없어도 센다.</summary>
        public int PlayCount(LabYaguangSfx sfx) => counts[(int)sfx];
        /// <summary>마지막으로 낸 3/2/1 숫자 (테스트·확인용). 아직 없으면 0.</summary>
        public int LastCount { get; private set; }

        public void Bind(LabNetwork target) => network = target;

        private void Awake()
        {
            clips = new AudioClip[Folders.Length][];
            for (int i = 0; i < Folders.Length; i++) clips[i] = Resources.LoadAll<AudioClip>(Folders[i]);
            source = NewSource();
            // 3/2/1은 숫자마다 높이가 달라서 따로 둔다 (다른 소리의 높이가 바뀌지 않게).
            countSource = NewSource();
        }

        private AudioSource NewSource()
        {
            AudioSource made = gameObject.AddComponent<AudioSource>();
            made.playOnAwake = false;
            made.spatialBlend = 0f; // 화면 기준 소리
            return made;
        }

        private void Update()
        {
            if (network == null || network.GameStartHeld) return;
            LabSnapshot snapshot = network.Snapshot;
            if (snapshot == null) return;
            ObserveFlash(snapshot, ServerNow(snapshot), network.LocalPlayerId);
        }

        /// <summary>
        /// 스냅샷의 섬광 상태를 보고 낼 소리를 낸다 (테스트에서는 now를 직접 준다). 새 세션·라운드면 처음부터 다시 센다.
        /// </summary>
        public void ObserveFlash(LabSnapshot snapshot, double now, ulong me)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing) return;
            if (!started || snapshot.sessionId != session || snapshot.round != round)
            {
                started = true;
                session = snapshot.sessionId;
                round = snapshot.round;
                seenFlashCount = snapshot.flashCount;
                warnFor = glowFor = countFor = double.NaN;
                lastCount = 0;
            }

            // 번쩍: 섬광 수가 늘었고 이번에 내가 맞았으면 (진동과 같은 기준, 1초 넘게 지난 섬광은 내지 않음).
            if (snapshot.flashCount < seenFlashCount) seenFlashCount = snapshot.flashCount;
            if (snapshot.flashCount > seenFlashCount)
            {
                seenFlashCount = snapshot.flashCount;
                if (snapshot.LastFlashHit(me) && now - snapshot.flashLastAt <= 1d) Play(LabYaguangSfx.FlashBurst);
            }

            if (snapshot.paused || !snapshot.FlashWarningOn(now)) return;
            double key = snapshot.flashAt;

            // 예고 시작: 모든 기기
            if (warnFor != key)
            {
                warnFor = key;
                if (now - snapshot.flashWarningAt <= LateSeconds) Play(LabYaguangSfx.FlashWarn);
            }
            if (!snapshot.FlashTargets(me)) return;

            // 눈이 빛나기 시작: 화면의 눈 빛(LabYaguangFlashHud.EyeGlowAt)이 켜지는 지점
            double glowAt = snapshot.flashWarningAt
                + (snapshot.flashAt - snapshot.flashWarningAt) * LabYaguangFlashHud.EyesLightFrom;
            if (glowFor != key && now >= glowAt)
            {
                glowFor = key;
                if (now - glowAt <= LateSeconds) Play(LabYaguangSfx.FlashGlow);
            }

            // 3/2/1: 화면 숫자와 같은 계산. 숫자가 바뀔 때마다 한 번.
            if (countFor != key)
            {
                countFor = key;
                lastCount = 0;
            }
            int count = LabYaguangFlashHud.CountdownAt(snapshot, now, me);
            if (count > 0 && count != lastCount)
            {
                lastCount = count;
                if (now - (snapshot.flashAt - count) <= LateSeconds) PlayCount(count);
            }
        }

        /// <summary>이 소리를 한 번 낸다. 소리를 냈으면 true (파일이 없으면 세기만 하고 false).</summary>
        public bool Play(LabYaguangSfx sfx) => Play(sfx, source, 1f);

        /// <summary>3/2/1 숫자 소리. 3이 원래 높이이고 2, 1로 줄수록 Inspector 반음만큼 높아진다.</summary>
        public bool PlayCount(int count)
        {
            LastCount = count;
            LabConfig config = network != null ? network.config : null;
            float step = config != null ? config.YaguangFlashCountStepSemitones : 2f;
            return Play(LabYaguangSfx.FlashCount, countSource, CountPitch(count, step));
        }

        /// <summary>
        /// 숫자 count의 재생 속도. 3 이상은 원래 높이, 그 아래로 한 칸 줄 때마다 step 반음씩 높다(2는 +step, 1은 +2×step).
        /// </summary>
        public static float CountPitch(int count, float step)
        {
            int up = Mathf.Clamp(3 - count, 0, 2);
            return Mathf.Pow(2f, up * Mathf.Clamp(step, 0f, 12f) / 12f);
        }

        private bool Play(LabYaguangSfx sfx, AudioSource player, float pitch)
        {
            counts[(int)sfx]++;
            AudioClip[] set = clips != null ? clips[(int)sfx] : null;
            if (set == null || set.Length == 0 || player == null) return false;
            player.pitch = pitch;
            player.PlayOneShot(set[Random.Range(0, set.Length)], Volume(sfx) * LabAudioSettings.SfxVolume);
            return true;
        }

        private float Volume(LabYaguangSfx sfx)
        {
            LabConfig config = network != null ? network.config : null;
            if (config == null) return 1f;
            switch (sfx)
            {
                case LabYaguangSfx.FlashWarn: return config.YaguangFlashWarnSoundVolume;
                case LabYaguangSfx.FlashGlow: return config.YaguangFlashGlowSoundVolume;
                case LabYaguangSfx.FlashCount: return config.YaguangFlashCountSoundVolume;
                default: return config.YaguangFlashBurstSoundVolume;
            }
        }

        // 스냅샷 사이에도 Host 시각을 이어서 어림한다 (LabYaguangFlashHud와 같은 방식, 최대 1초, 일시정지면 멈춤).
        private double ServerNow(LabSnapshot snapshot)
        {
            float realtime = Time.realtimeSinceStartup;
            if (snapshot.paused || snapshot.sessionId != clockSession || snapshot.round != clockRound
                || snapshot.serverTime != seenServerTime)
            {
                clockSession = snapshot.sessionId;
                clockRound = snapshot.round;
                seenServerTime = snapshot.serverTime;
                seenAt = realtime;
            }
            return snapshot.paused ? snapshot.serverTime : snapshot.serverTime + System.Math.Min(1d, realtime - seenAt);
        }
    }
}
