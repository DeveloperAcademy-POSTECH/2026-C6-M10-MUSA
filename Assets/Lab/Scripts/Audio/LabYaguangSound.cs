using UnityEngine;

namespace C6Lab
{
    /// <summary>야광귀 스테이지 효과음의 종류 (#395~). 숫자는 폴더·크기 순서다. 새 소리는 뒤에 붙인다.</summary>
    public enum LabYaguangSfx { FlashWarn, FlashGlow, FlashCount, FlashBurst, FlashTaunt, ShakeBell, ShakeBellFinal, FlashCaught,
        ComboCountOne, ComboCountTwo, ComboCountThree, ComboCountFour, ComboStart, ComboTick, ComboTickFast,
        ComboTaunt, ComboSmallStart, ComboSmallEnd, ComboBigStart, ComboBigEnd, ComboClearedFirst, ComboClearedSecond,
        ComboGuideRule, ComboGuideHit1, ComboGuideHit2, ComboGuideHit3, ComboGuideClear, ComboGuideFail, ComboStamp, ComboGuideReady, ComboGuideRetry }

    /// <summary>
    /// 야광귀 섬광 예고와 번쩍 효과음 (#395). Host 스냅샷의 섬광 시각만 보고 소리를 낸다(화면 코드와 따로 돈다).
    /// - 예고 시작 (Audio/YaguangFlashWarn): 박 옆 말풍선에 야광귀 눈이 뜨는 순간, 모든 기기
    /// - 눈이 빛나기 시작 (Audio/YaguangFlashGlow): 예고 30% 지점, 눈이 노랗게 빛나고 가장자리 빛이 차오를 때, 맞을 사람 기기
    /// - 3/2/1 (Audio/YaguangFlashCount): 화면 가운데 숫자가 바뀔 때마다, 맞을 사람 기기. 숫자가 줄수록 음이 높아진다.
    /// - 번쩍 (Audio/YaguangFlashBurst): 섬광이 터진 순간, 맞은 사람 기기 (진동과 같은 때)
    /// #396 섬광 반응:
    /// - "안보이지롱~" 대사 (Audio/YaguangFlashTaunt): 번쩍 뒤 Inspector 지연(기본 0.35초)만큼 늦게, 맞은 사람 기기 (징 뒤에 들리게)
    /// - 흔들기 1~(최대-1)번 (Audio/YaguangShakeBell): 흔든 수가 하나 오를 때마다 같은 무당 방울 (Inspector로 횟수마다 높일 수 있음). 흔든 사람 기기
    /// - 마지막 흔들기 (Audio/YaguangShakeBellFinal): 최대 횟수(기본 10번)를 채운 순간 더 큰 무당 방울. 흔든 사람 기기
    /// - "으악 어지러워~" 대사 (Audio/YaguangFlashCaught): Host가 내 눈부심을 푼 뒤 Inspector 지연(기본 0.2초)만큼 늦게, 푼 사람 기기
    /// #397 박 색상 콤보 예고와 진행 (모든 기기):
    /// - 예고 카운트 (Audio/YaguangComboCount1~4): 남은 초가 바뀔 때마다 야광귀 목소리 "하나/둘/셋/넷" (박 옆 말풍선과 같은 수)
    /// - 콤보 시작 (Audio/YaguangComboStart): 4칸 색이 공개되는 순간 꽹과리 "깽!"
    /// - 제한 시간 (Audio/YaguangComboTick): 콤보가 나온 뒤 Inspector 간격(기본 0.5초)마다 시계 째깍. 남은 시간이 Inspector 초
    ///   (기본 4초) 이하면 (Audio/YaguangComboTickFast, 같은 높이의 째깍) 더 짧은 간격(기본 0.25초)마다 빠른 째깍. 콤보가 끝나면(성공/실패) 바로 멈춘다.
    /// #398 박 색상 콤보 결과 (모든 기기, 콤보는 팀이 같이 하므로 누가 받든 모두 듣는다):
    /// - "메롱~" 대사 (Audio/YaguangComboTaunt): 콤보 실패(틀린 색/시간 초과) 뒤 Inspector 지연(기본 0.3초), 실패 소리 다음에
    /// - SMALL 시작 (Audio/YaguangComboSmallStart): 결과 문구가 사라지고 SMALL 배지가 나오는 때 (결과 + LabGourdComboHud.ResultSeconds)
    /// - SMALL 끝 (Audio/YaguangComboSmallEnd): 페널티가 끝나 공이 원래 크기로 돌아올 때 (가장 늦게 끝나는 사람 기준)
    /// - BIG 시작 (Audio/YaguangComboBigStart): 성공 뒤 BIG 배지와 부적 비가 시작될 때
    /// - BIG 끝 (Audio/YaguangComboBigEnd): 보상이 끝날 때
    /// - 성공 대사 (Audio/YaguangComboClearedFirst "이런!", Audio/YaguangComboClearedSecond "이럴 수가!"): 콤보 성공 뒤
    ///   Inspector 지연(기본 0.8초). 이번 라운드 첫 성공은 "이런!", 그 뒤 성공은 "이럴 수가!" (이 기기에서 본 성공 수 기준).
    ///   "이럴 수가!" 폴더가 비어 있으면 "이런!" 파일을 쓴다.
    ///   다음 콤보 결과가 먼저 나오면 앞 결과의 남은 소리는 내지 않는다.
    /// #695 금옥 콤보 안내 (모든 기기, 금옥 목소리, 야광귀 목소리와 따로):
    /// - 규칙 (Audio/YaguangComboGuideRule): 라운드의 처음 Inspector 번(기본 1 = 첫 콤보만) 예고에서, 야광귀가 "넷"을 센 뒤
    ///   Inspector 지연(기본 0.9초)에. 그 예고의 넷/셋/둘/하나는 점점 작아진다(넷 원래 크기, 한 칸마다 Inspector 배율, 기본 0.6).
    /// - 준비 (Audio/YaguangComboGuideReady "자.. 다들 준비 됐지?"): 규칙 안내 다음 콤보(기본 두 번째 콤보) 예고에서 같은 때에.
    ///   Inspector로 끌 수 있다. 숫자도 규칙 때처럼 점점 작아진다.
    /// - 칭찬 (Audio/YaguangComboGuideHit1 "좋아!" / Hit2 "잘했어!" / Hit3 "하나만 더..!" / Clear "그렇지!!"):
    ///   칸을 맞힐 때마다 Inspector 지연(기본 0.3초) 뒤에. 새로 말하면 앞의 안내는 끊는다.
    /// - 다시 (Audio/YaguangComboGuideRetry, 여러 개를 이름 순서대로 돌아가며): 라운드 첫 콤보의 첫 칸 전에 틀린 색을 봐줄 때마다
    ///   (스냅샷 comboForgivenCount가 늘 때) 칭찬과 같은 지연 뒤에 "한 번만 다시 해보자" 같은 말.
    /// - 실패 (Audio/YaguangComboGuideFail "아이고"): 콤보 실패 뒤 Inspector 지연(기본 0.1초). 야광귀 "메롱~"은 이 말이 끝나고
    ///   Inspector 간격(기본 0.1초) 뒤에 나온다 (원래 메롱 지연보다 이르지는 않다).
    /// #695 콤보 성공 도장 쾅 (Audio/YaguangComboStamp): 이 기기 화면에서 성공 도장(#668)이 박에 찍히는 순간
    ///   (LabGourdComboHud.StampsLanded가 늘 때). 도장이 안 나오면(야광귀 셈법이 꺼짐) 소리도 없다.
    /// #664 구미호 여우구슬 판정 (모든 기기, 야광귀 콤보와 같은 소리):
    /// - 제한 시간 째깍: 여우구슬이 떨어진 순간부터 콤보와 같은 간격으로 째깍, 남은 시간이 짧으면 빠른 째깍. 다 지우면 바로 멈춘다.
    /// - BIG/SMALL 시작·끝: 콤보 결과와 같은 때 (결과 + LabGourdComboHud.ResultSeconds, 가장 늦게 끝나는 사람 기준).
    ///   야광귀 목소리(메롱~, 이런!)는 내지 않는다.
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
        public const string FlashTauntFolder = "Audio/YaguangFlashTaunt";
        public const string ShakeBellFolder = "Audio/YaguangShakeBell";
        public const string ShakeBellFinalFolder = "Audio/YaguangShakeBellFinal";
        public const string FlashCaughtFolder = "Audio/YaguangFlashCaught";
        public const string ComboCountFolder = "Audio/YaguangComboCount";
        public const string ComboStartFolder = "Audio/YaguangComboStart";
        public const string ComboTickFolder = "Audio/YaguangComboTick";
        public const string ComboTickFastFolder = "Audio/YaguangComboTickFast";
        public const string ComboTauntFolder = "Audio/YaguangComboTaunt";
        public const string ComboSmallStartFolder = "Audio/YaguangComboSmallStart";
        public const string ComboSmallEndFolder = "Audio/YaguangComboSmallEnd";
        public const string ComboBigStartFolder = "Audio/YaguangComboBigStart";
        public const string ComboBigEndFolder = "Audio/YaguangComboBigEnd";
        public const string ComboClearedFirstFolder = "Audio/YaguangComboClearedFirst";
        public const string ComboClearedSecondFolder = "Audio/YaguangComboClearedSecond";
        public const string ComboGuideFolder = "Audio/YaguangComboGuide";
        public const string ComboStampFolder = "Audio/YaguangComboStamp";
        private static readonly string[] Folders = { FlashWarnFolder, FlashGlowFolder, FlashCountFolder, FlashBurstFolder,
            FlashTauntFolder, ShakeBellFolder, ShakeBellFinalFolder, FlashCaughtFolder,
            ComboCountFolder + "1", ComboCountFolder + "2", ComboCountFolder + "3", ComboCountFolder + "4",
            ComboStartFolder, ComboTickFolder, ComboTickFastFolder,
            ComboTauntFolder, ComboSmallStartFolder, ComboSmallEndFolder, ComboBigStartFolder, ComboBigEndFolder,
            ComboClearedFirstFolder, ComboClearedSecondFolder,
            ComboGuideFolder + "Rule", ComboGuideFolder + "Hit1", ComboGuideFolder + "Hit2", ComboGuideFolder + "Hit3",
            ComboGuideFolder + "Clear", ComboGuideFolder + "Fail", ComboStampFolder, ComboGuideFolder + "Ready",
            ComboGuideFolder + "Retry" };
        /// <summary>The folders this component loads, for the battle audio preload (#492).</summary>
        public static System.Collections.Generic.IReadOnlyList<string> ClipFolders => Folders;
        /// <summary>The ones the 구미호 fox round plays too (#664): its clock and the BIG/SMALL start and end.</summary>
        public static readonly string[] FoxRoundClipFolders = { ComboTickFolder, ComboTickFastFolder,
            ComboSmallStartFolder, ComboSmallEndFolder, ComboBigStartFolder, ComboBigEndFolder };
        /// <summary>째깍은 그 박자보다 이 초 넘게 늦게 알아채면 건너뛴다 (스냅샷이 늦게 와도 몰아서 울리지 않게).</summary>
        public const double TickLateSeconds = .3d;
        public const double DefaultTickInterval = .5d;
        public const double DefaultTickFastInterval = .25d;
        public const double DefaultTickFastSeconds = 4d;
        /// <summary>그 순간보다 이 초 넘게 늦게 알아채면 내지 않는다.</summary>
        public const double LateSeconds = .5d;

        private AudioClip[][] clips;
        private readonly int[] counts = new int[Folders.Length];
        private AudioSource source, countSource, bellSource, guideSource;
        private LabNetwork network;

        private string session;
        private uint round;
        private bool started;
        private uint seenFlashCount;
        private double warnFor = double.NaN, glowFor = double.NaN, countFor = double.NaN;
        private int lastCount;
        // #396: 대사를 낼 섬광(섬광 수)과 그 시각(Host 시각). 0이면 기다리는 대사 없음.
        private uint tauntFor, caughtFor, caughtSeen;
        private double tauntAt, caughtAt;
        private int seenShakes;
        // #397 콤보: 예고 카운트를 센 콤보(4칸이 나올 시각)와 마지막 수, 시작음을 낸 콤보 수, 마지막 째깍 시각
        private double comboCountFor = double.NaN;
        private int lastComboCount;
        private uint seenComboCount;
        private double lastTickAt = double.NegativeInfinity;
        // #398 콤보 결과: 마지막으로 본 결과 순번, 그 결과 시각과 효과(-1 SMALL, 1 BIG, 0 없음), 아직 낼 소리
        private uint seenResultCount;
        private double resultAt;
        private int resultEffect;
        private bool tauntDue, effectStartDue, effectEndDue, clearedDue;
        // 이번 라운드에 본 콤보 성공 수 (첫 성공 "이런!", 그 뒤 "이럴 수가!")
        private int clearedCount;
        // #664 여우구슬 판정: 째깍을 센 판정(떨어진 시각)과 마지막 째깍, 본 판정 결과 수, 그 결과 시각과 효과, 아직 낼 소리
        private double foxTickFor = double.NaN, lastFoxTickAt = double.NegativeInfinity;
        private int seenFoxRoundCount;
        private double foxResultAt;
        private int foxEffect;
        private bool foxStartDue, foxEndDue;
        private LabYaguangFlashHud flashHud;
        private LabGourdComboHud comboHud;
        private int seenStamps;
        // #695 금옥 안내: 규칙을 낸 예고(4칸이 나올 시각), 색을 부를 콤보와 맞힌 칸 수, 부를 칸과 그 시각, 안내가 끝나는 시각
        private double guideRuleFor = double.NaN;
        // 이번 예고에 금옥 말(규칙/준비)이 있는지(있으면 넷→하나가 점점 작아짐), 아직 낼 말과 그 시각
        private bool guideRuleWarning, guideRuleDue;
        private LabYaguangSfx guideRuleLine;
        private double guideRuleAt;
        private uint guideComboFor;
        private int guideProgress;
        private int guideSlot = -1;
        // #695 봐준 틀린 색: 마지막으로 본 수, 낼 시각, 지금까지 낸 "다시" 말 수(돌아가며 고르기)
        private uint seenForgiven;
        private bool retryDue;
        private double retryAt;
        private int retrySaid;
        private double guideSlotAt;
        // 콤보 실패 "아이고"를 아직 낼지, 그 말이 끝나는 시각 (메롱은 그 뒤에)
        private bool guideFailDue;
        private double guideFailEndsAt = double.NegativeInfinity;

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
            // #695 "다시" 말은 이름 순서대로 돌아가므로 이름으로 정렬해 둔다.
            System.Array.Sort(clips[(int)LabYaguangSfx.ComboGuideRetry], (a, b) => string.CompareOrdinal(a.name, b.name));
            source = NewSource();
            // 3/2/1은 숫자마다 높이가 달라서 따로 둔다 (다른 소리의 높이가 바뀌지 않게).
            countSource = NewSource();
            // 흔들기 방울도 횟수마다 높이가 달라서 따로 둔다.
            bellSource = NewSource();
            // #695 금옥 안내는 새로 부를 때 앞 안내만 끊을 수 있게 따로 둔다.
            guideSource = NewSource();
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
            ObserveFlash(snapshot, network.BattlePresentationTime, network.LocalPlayerId);
            if (comboHud == null) comboHud = GetComponent<LabGourdComboHud>();
            if (comboHud != null) ObserveStamp(comboHud.StampsLanded);
            if (flashHud == null) flashHud = GetComponent<LabYaguangFlashHud>();
            if (flashHud != null && network.config != null && snapshot.phase == LabPhase.Playing)
                ObserveShakes(flashHud.ShakeCount, network.config.YaguangFlashShakeCount);
        }

        /// <summary>
        /// 이번 섬광에서 흔든 수(count)가 늘었으면 방울을 낸다 (#396). 최대 횟수(needed)에 닿은 흔들기는 큰 방울, 그 전은
        /// 같은 방울(Inspector 반음 간격이 0보다 크면 횟수마다 높아짐). 한 번에 여러 번 늘었으면 마지막 횟수의 소리 하나만 낸다. 새 섬광이라 수가 줄면 다시 센다.
        /// 낸 소리 종류를 돌려준다 (없으면 null).
        /// </summary>
        public LabYaguangSfx? ObserveShakes(int count, int needed)
        {
            if (count < seenShakes) seenShakes = count;
            if (count <= seenShakes) return null;
            seenShakes = count;
            if (needed > 0 && count >= needed)
            {
                Play(LabYaguangSfx.ShakeBellFinal);
                return LabYaguangSfx.ShakeBellFinal;
            }
            LabConfig config = network != null ? network.config : null;
            float step = config != null ? config.YaguangShakeBellStepSemitones : 0f;
            Play(LabYaguangSfx.ShakeBell, bellSource, BellPitch(count, step));
            return LabYaguangSfx.ShakeBell;
        }

        /// <summary>
        /// #695: 화면의 성공 도장이 찍힌 수(landed)가 늘었으면 쾅 한 번. 화면이 도장을 그리는 바로 그 프레임에 맞춘다.
        /// 낸 소리가 있으면 true.
        /// </summary>
        public bool ObserveStamp(int landed)
        {
            if (landed < seenStamps) seenStamps = landed;
            if (landed <= seenStamps) return false;
            seenStamps = landed;
            Play(LabYaguangSfx.ComboStamp);
            return true;
        }

        /// <summary>count번째 흔들기 방울의 재생 속도. 1번이 원래 높이이고 한 번 늘 때마다 step 반음씩 높다 (최대 +12반음).</summary>
        public static float BellPitch(int count, float step)
        {
            float up = Mathf.Min(12f, Mathf.Max(0, count - 1) * Mathf.Clamp(step, 0f, 4f));
            return Mathf.Pow(2f, up / 12f);
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
                // 접속 직후 이미 지난 섬광의 대사는 내지 않는다.
                tauntFor = caughtFor = 0;
                caughtSeen = snapshot.flashCount;
                comboCountFor = double.NaN;
                lastComboCount = 0;
                seenComboCount = snapshot.comboCount;
                lastTickAt = double.NegativeInfinity;
                // 접속 직후 이미 지난 콤보 결과의 소리는 내지 않는다.
                seenResultCount = snapshot.comboResultCount;
                tauntDue = effectStartDue = effectEndDue = clearedDue = false;
                clearedCount = 0;
                foxTickFor = double.NaN;
                lastFoxTickAt = double.NegativeInfinity;
                seenFoxRoundCount = snapshot.foxRoundCount;
                foxStartDue = foxEndDue = false;
                guideRuleFor = double.NaN;
                guideRuleWarning = guideRuleDue = false;
                guideComboFor = snapshot.comboCount;
                guideProgress = snapshot.comboProgress;
                guideSlot = -1;
                seenForgiven = snapshot.comboForgivenCount;
                retryDue = false;
                retrySaid = 0;
                guideFailDue = false;
                guideFailEndsAt = double.NegativeInfinity;
            }
            ObserveCombo(snapshot, now);
            ObserveComboResult(snapshot, now);
            ObserveFoxRound(snapshot, now);

            // 번쩍: 섬광 수가 늘었고 이번에 내가 맞았으면 (진동과 같은 기준, 1초 넘게 지난 섬광은 내지 않음).
            if (snapshot.flashCount < seenFlashCount) seenFlashCount = snapshot.flashCount;
            if (snapshot.flashCount > seenFlashCount)
            {
                seenFlashCount = snapshot.flashCount;
                if (snapshot.LastFlashHit(me) && now - snapshot.flashLastAt <= 1d)
                {
                    Play(LabYaguangSfx.FlashBurst);
                    // "안보이지롱~"은 징 뒤에 들리게 조금 늦게 낸다.
                    tauntFor = snapshot.flashCount;
                    tauntAt = snapshot.flashLastAt + TauntDelay;
                }
            }
            ObserveCaught(snapshot, now, me);
            if (!snapshot.paused)
            {
                if (tauntFor != 0 && tauntFor == snapshot.flashCount && now >= tauntAt)
                {
                    tauntFor = 0;
                    if (now - tauntAt <= LateSeconds) Play(LabYaguangSfx.FlashTaunt);
                }
                if (caughtFor != 0 && caughtFor == snapshot.flashCount && now >= caughtAt)
                {
                    caughtFor = 0;
                    if (now - caughtAt <= LateSeconds) Play(LabYaguangSfx.FlashCaught);
                }
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

        /// <summary>
        /// 박 색상 콤보 예고·시작·제한 시간 소리 (#397). 모든 기기에서 같은 Host 시각으로 낸다. 일시정지 중에는 내지 않는다.
        /// </summary>
        private void ObserveCombo(LabSnapshot snapshot, double now)
        {
            // 시작: 콤보 수가 늘었다 = 4칸이 나왔다.
            if (snapshot.comboCount < seenComboCount) seenComboCount = snapshot.comboCount;
            if (snapshot.comboCount > seenComboCount)
            {
                seenComboCount = snapshot.comboCount;
                lastTickAt = snapshot.comboStartedAt;
                if (!snapshot.paused && snapshot.comboStartedAt >= 0d && now - snapshot.comboStartedAt <= LateSeconds)
                    Play(LabYaguangSfx.ComboStart);
            }
            if (snapshot.paused) return;

            ObserveGuideFeedback(snapshot, now);

            // 예고: 남은 초(올림)가 바뀔 때마다 그 수를 야광귀가 센다. 4초보다 길면 넷부터 다시 (말풍선과 같음).
            if (snapshot.ComboWarningOn(now))
            {
                // #695: 라운드의 처음 몇 번 콤보(이번 예고 = 지금까지 나온 콤보 수 + 1번째)는 야광귀가 "넷"을 센 뒤
                // Inspector 지연만큼 지나 금옥이 규칙을 말한다. 그 예고의 넷/셋/둘/하나는 점점 작아져 금옥 목소리가 잘 들린다.
                if (guideRuleFor != snapshot.comboNextAt)
                {
                    guideRuleFor = snapshot.comboNextAt;
                    LabYaguangSfx? line = WarningLine(snapshot.comboCount, GuideRuleCombos, GuideReadyOn);
                    guideRuleWarning = guideRuleDue = line.HasValue;
                    guideRuleLine = line ?? LabYaguangSfx.ComboGuideRule;
                    guideRuleAt = snapshot.comboWarningAt + GuideRuleDelay;
                }
                if (guideRuleDue && now >= guideRuleAt)
                {
                    guideRuleDue = false;
                    if (now - guideRuleAt <= LateSeconds) PlayGuide(guideRuleLine);
                }
                if (comboCountFor != snapshot.comboNextAt)
                {
                    comboCountFor = snapshot.comboNextAt;
                    lastComboCount = 0;
                }
                int count = Mathf.Max(1, Mathf.CeilToInt((float)(snapshot.comboNextAt - now)));
                if (count != lastComboCount)
                {
                    lastComboCount = count;
                    if (now - (snapshot.comboNextAt - count) <= LateSeconds)
                        Play(CountSfx(count), source, 1f, guideRuleWarning ? GuideCountGain(count, GuideCountFade) : 1f);
                }
            }

            // 제한 시간 째깍
            if (!snapshot.comboActive || snapshot.comboStartedAt < 0d || snapshot.comboEndsAt <= snapshot.comboStartedAt) return;
            LabConfig config = network != null ? network.config : null;
            double fastFor = config != null ? config.YaguangComboTickFastSeconds : DefaultTickFastSeconds;
            double every = config != null ? config.YaguangComboTickIntervalSeconds : DefaultTickInterval;
            double fastEvery = config != null ? config.YaguangComboTickFastIntervalSeconds : DefaultTickFastInterval;
            double tick = LatestTickAt(snapshot.comboStartedAt, snapshot.comboEndsAt, now, fastFor, every, fastEvery, out bool fast);
            if (tick <= lastTickAt) return;
            lastTickAt = tick;
            if (now - tick <= TickLateSeconds) Play(fast ? LabYaguangSfx.ComboTickFast : LabYaguangSfx.ComboTick);
        }

        /// <summary>
        /// #695 금옥 칭찬: 같은 콤보 안에서 칸을 맞힐 때마다 Inspector 지연 뒤에 1칸 "좋아!", 2칸 "잘했어!", 3칸 "하나만 더..!",
        /// 4칸(성공) "그렇지!!". 1~3칸은 그 사이 콤보가 끝났으면(실패) 내지 않는다. 실패는 야광귀 "메롱~"이 맡는다.
        /// 너무 늦게 알아챘으면 내지 않는다.
        /// </summary>
        private void ObserveGuideFeedback(LabSnapshot snapshot, double now)
        {
            ObserveGuideRetry(snapshot, now);
            if (snapshot.comboCount != guideComboFor)
            {
                guideComboFor = snapshot.comboCount;
                guideProgress = snapshot.comboProgress;
                guideSlot = -1;
            }
            else if (snapshot.comboProgress > guideProgress)
            {
                // 칸을 맞혔다 (맞힌 소리 뒤에). 한 번에 여러 칸이 늘었으면 마지막 칸의 말만.
                guideProgress = snapshot.comboProgress;
                guideSlot = Mathf.Min(snapshot.comboProgress, LabGourdCombo.Length);
                guideSlotAt = now + GuideFeedbackDelay;
            }
            if (guideSlot < 1 || now < guideSlotAt) return;
            int hits = guideSlot;
            guideSlot = -1;
            if (now - guideSlotAt > LateSeconds) return;
            bool cleared = hits >= LabGourdCombo.Length;
            if (!cleared && !snapshot.comboActive) return;
            PlayGuide(GuideFeedbackSfx(hits));
        }

        /// <summary>
        /// #695 봐준 틀린 색: Host가 라운드 첫 콤보의 첫 칸 전 틀린 색을 봐줄 때마다(comboForgivenCount가 늘 때) 칭찬과 같은 지연 뒤에
        /// "다시" 말 하나. 파일 여러 개를 이름 순서대로 돌아가며 낸다. 그 사이 콤보가 끝났거나 너무 늦게 알아챘으면 내지 않는다.
        /// </summary>
        private void ObserveGuideRetry(LabSnapshot snapshot, double now)
        {
            if (snapshot.comboForgivenCount < seenForgiven) seenForgiven = snapshot.comboForgivenCount;
            if (snapshot.comboForgivenCount > seenForgiven)
            {
                seenForgiven = snapshot.comboForgivenCount;
                retryDue = true;
                retryAt = now + GuideFeedbackDelay;
            }
            if (!retryDue || now < retryAt) return;
            retryDue = false;
            if (!snapshot.comboActive || now - retryAt > LateSeconds) return;
            PlayGuide(LabYaguangSfx.ComboGuideRetry, RetryClip(retrySaid++, ClipCount(LabYaguangSfx.ComboGuideRetry)));
        }

        /// <summary>#695 n번째(0부터) "다시" 말에 쓸 파일 번호: 이름 순서대로 돌아간다. 파일이 없으면 -1.</summary>
        public static int RetryClip(int said, int clipCount) => clipCount <= 0 ? -1 : said % clipCount;

        /// <summary>#695 맞힌 칸 수에 따른 금옥 칭찬 (1 좋아! / 2 잘했어! / 3 하나만 더..! / 4 그렇지!!).</summary>
        public static LabYaguangSfx GuideFeedbackSfx(int hits) =>
            hits >= LabGourdCombo.Length ? LabYaguangSfx.ComboGuideClear
            : hits <= 1 ? LabYaguangSfx.ComboGuideHit1 : hits == 2 ? LabYaguangSfx.ComboGuideHit2 : LabYaguangSfx.ComboGuideHit3;

        /// <summary>금옥 안내 하나를 낸다. 앞 안내가 아직 나오고 있으면 끊고 새로 낸다. 낸 소리의 길이(초)를 돌려준다 (파일이 없으면 0).</summary>
        private float PlayGuide(LabYaguangSfx sfx, int pick = -1)
        {
            counts[(int)sfx]++;
            AudioClip[] set = clips != null ? clips[(int)sfx] : null;
            if (set == null || set.Length == 0 || guideSource == null) return 0f;
            AudioClip clip = set[pick >= 0 && pick < set.Length ? pick : Random.Range(0, set.Length)];
            guideSource.Stop();
            guideSource.pitch = 1f;
            guideSource.PlayOneShot(clip, Volume(sfx) * LabAudioSettings.SfxVolume);
            return clip.length;
        }

        /// <summary>
        /// 박 색상 콤보 결과 소리 (#398): 실패 뒤 "메롱~", SMALL/BIG 배지가 나올 때와 끝날 때. 모든 기기에서 같은 Host 시각으로 낸다.
        /// </summary>
        private void ObserveComboResult(LabSnapshot snapshot, double now)
        {
            if (snapshot.comboResultCount < seenResultCount) seenResultCount = snapshot.comboResultCount;
            if (snapshot.comboResultCount > seenResultCount)
            {
                seenResultCount = snapshot.comboResultCount;
                resultAt = snapshot.comboResultAt;
                bool failed = snapshot.comboResult == LabComboResult.WrongColor || snapshot.comboResult == LabComboResult.Timeout;
                resultEffect = failed ? -1 : snapshot.comboResult == LabComboResult.Success ? 1 : 0;
                tauntDue = failed && resultAt >= 0d;
                guideFailDue = tauntDue; // #695 금옥 "아이고" 다음에 야광귀 "메롱~"
                guideFailEndsAt = double.NegativeInfinity;
                foxStartDue = foxEndDue = false; // 새 결과가 앞 결과의 남은 소리를 대신한다.
                clearedDue = resultEffect > 0 && resultAt >= 0d;
                if (clearedDue) clearedCount++;
                effectStartDue = effectEndDue = resultEffect != 0 && resultAt >= 0d
                    && EffectUntil(snapshot, resultEffect, resultAt) > resultAt + LabGourdComboHud.ResultSeconds;
            }
            if (snapshot.paused) return;

            double failAt = resultAt + GuideFailDelay;
            if (guideFailDue && now >= failAt)
            {
                guideFailDue = false;
                if (now - failAt <= LateSeconds) guideFailEndsAt = now + PlayGuide(LabYaguangSfx.ComboGuideFail);
            }
            double tauntAt = System.Math.Max(resultAt + ComboTauntDelay, guideFailEndsAt + GuideFailToTaunt);
            if (tauntDue && !guideFailDue && now >= tauntAt)
            {
                tauntDue = false;
                if (now - tauntAt <= LateSeconds) Play(LabYaguangSfx.ComboTaunt);
            }
            double clearedAt = resultAt + ClearedDelay;
            if (clearedDue && now >= clearedAt)
            {
                clearedDue = false;
                if (now - clearedAt <= LateSeconds)
                {
                    // "이럴 수가!" 파일이 없으면 "이런!"을 그대로 쓴다 (지금은 한 파일로 통일).
                    LabYaguangSfx line = ClearedSfx(clearedCount);
                    if (clips != null && ClipCount(line) == 0) line = LabYaguangSfx.ComboClearedFirst;
                    Play(line);
                }
            }
            double startAt = resultAt + LabGourdComboHud.ResultSeconds;
            if (effectStartDue && now >= startAt)
            {
                effectStartDue = false;
                if (now - startAt <= LateSeconds) Play(resultEffect < 0 ? LabYaguangSfx.ComboSmallStart : LabYaguangSfx.ComboBigStart);
            }
            if (!effectEndDue) return;
            double endAt = EffectUntil(snapshot, resultEffect, resultAt);
            if (now < endAt) return;
            effectEndDue = false;
            if (now - endAt <= LateSeconds && endAt > startAt)
                Play(resultEffect < 0 ? LabYaguangSfx.ComboSmallEnd : LabYaguangSfx.ComboBigEnd);
        }

        /// <summary>
        /// 구미호 여우구슬 판정 소리 (#664): 판정 중 째깍(콤보와 같은 간격·소리), 결과 뒤 BIG/SMALL 시작·끝(콤보 결과와 같은 때).
        /// </summary>
        private void ObserveFoxRound(LabSnapshot snapshot, double now)
        {
            if (snapshot.foxRoundCount < seenFoxRoundCount) seenFoxRoundCount = snapshot.foxRoundCount;
            if (snapshot.foxRoundCount > seenFoxRoundCount)
            {
                seenFoxRoundCount = snapshot.foxRoundCount;
                foxResultAt = snapshot.foxRoundResultAt;
                foxEffect = System.Math.Sign(snapshot.foxRoundResult);
                foxStartDue = foxEndDue = foxEffect != 0 && foxResultAt >= 0d
                    && EffectUntil(snapshot, foxEffect, foxResultAt) > foxResultAt + LabGourdComboHud.ResultSeconds;
                if (foxStartDue) effectStartDue = effectEndDue = false; // 콤보 결과의 남은 소리는 이 결과가 대신한다.
            }
            if (snapshot.paused) return;

            double startAt = foxResultAt + LabGourdComboHud.ResultSeconds;
            if (foxStartDue && now >= startAt)
            {
                foxStartDue = false;
                if (now - startAt <= LateSeconds) Play(foxEffect < 0 ? LabYaguangSfx.ComboSmallStart : LabYaguangSfx.ComboBigStart);
            }
            if (foxEndDue)
            {
                double endAt = EffectUntil(snapshot, foxEffect, foxResultAt);
                if (now >= endAt)
                {
                    foxEndDue = false;
                    if (now - endAt <= LateSeconds && endAt > startAt)
                        Play(foxEffect < 0 ? LabYaguangSfx.ComboSmallEnd : LabYaguangSfx.ComboBigEnd);
                }
            }

            // 제한 시간 째깍: 판정이 진행 중일 때만. 떨어진 순간부터 센다.
            if (!snapshot.FoxRoundOn(now)) return;
            if (foxTickFor != snapshot.foxRoundStartedAt)
            {
                foxTickFor = snapshot.foxRoundStartedAt;
                lastFoxTickAt = snapshot.foxRoundStartedAt;
            }
            LabConfig config = network != null ? network.config : null;
            double fastFor = config != null ? config.YaguangComboTickFastSeconds : DefaultTickFastSeconds;
            double every = config != null ? config.YaguangComboTickIntervalSeconds : DefaultTickInterval;
            double fastEvery = config != null ? config.YaguangComboTickFastIntervalSeconds : DefaultTickFastInterval;
            double tick = LatestTickAt(snapshot.foxRoundStartedAt, snapshot.foxRoundEndsAt, now, fastFor, every, fastEvery, out bool fast);
            if (tick <= lastFoxTickAt) return;
            lastFoxTickAt = tick;
            if (now - tick <= TickLateSeconds) Play(fast ? LabYaguangSfx.ComboTickFast : LabYaguangSfx.ComboTick);
        }

        /// <summary>
        /// 결과 시각(at) 뒤에 효과(-1 SMALL, 1 BIG)가 가장 늦게 끝나는 시각. 그 효과를 받은 사람이 없으면 at.
        /// </summary>
        public static double EffectUntil(LabSnapshot snapshot, int effect, double at)
        {
            double until = at;
            if (snapshot == null || snapshot.players == null || effect == 0) return until;
            foreach (LabPlayerState player in snapshot.players)
            {
                if (player == null) continue;
                double end = effect < 0 ? player.comboPenaltyUntil : player.comboRewardUntil;
                if (end > until) until = end;
            }
            return until;
        }

        /// <summary>이번 라운드 count번째 콤보 성공의 대사. 1번째는 "이런!", 그 뒤는 "이럴 수가!".</summary>
        public static LabYaguangSfx ClearedSfx(int count) =>
            count <= 1 ? LabYaguangSfx.ComboClearedFirst : LabYaguangSfx.ComboClearedSecond;

        /// <summary>남은 초 count를 야광귀식으로 센 소리 (1 하나, 2 둘, 3 셋, 4 넷, 5부터는 다시 하나부터).</summary>
        public static LabYaguangSfx CountSfx(int count) => LabYaguangSfx.ComboCountOne + (Mathf.Max(1, count) - 1) % 4;

        /// <summary>
        /// 콤보 제한 시간 [start, end]에서 now 이하의 가장 늦은 째깍 시각. 처음에는 start에서 every초마다, 남은 시간이 fastFor초
        /// 이하가 되면 end - fastFor부터 fastEvery초마다 (fast = true). 끝나는 순간(end)에는 울리지 않는다. 아직 없으면 -무한.
        /// </summary>
        public static double LatestTickAt(double start, double end, double now, double fastFor, double every, double fastEvery, out bool fast)
        {
            fast = false;
            if (now <= start || end <= start) return double.NegativeInfinity;
            every = System.Math.Max(.05d, every);
            fastEvery = System.Math.Max(.05d, fastEvery);
            double fastFrom = System.Math.Max(start, end - System.Math.Max(0d, fastFor));
            double t = System.Math.Min(now, end - 1e-6);
            if (t >= fastFrom && fastFor > 0d)
            {
                fast = true;
                return fastFrom + System.Math.Floor((t - fastFrom) / fastEvery + 1e-9) * fastEvery;
            }
            double k = System.Math.Floor((t - start) / every + 1e-9);
            return k >= 1d ? start + k * every : double.NegativeInfinity;
        }

        /// <summary>
        /// Host가 이번 섬광의 내 눈부심을 풀었으면 "으악 어지러워~"를 예약한다 (#396). 섬광마다 한 번, 푼 지 1초 넘게 지나 알아채면
        /// 내지 않는다 (말풍선 LabYaguangFlashHud.CheckCaught와 같은 기준).
        /// </summary>
        private void ObserveCaught(LabSnapshot snapshot, double now, ulong me)
        {
            double released = snapshot.FlashReleasedAt(me);
            if (released < 0d || caughtSeen == snapshot.flashCount) return;
            caughtSeen = snapshot.flashCount;
            if (now - released > 1d) return;
            caughtFor = snapshot.flashCount;
            caughtAt = released + CaughtDelay;
        }

        private double TauntDelay => network != null && network.config != null ? network.config.YaguangFlashTauntDelaySeconds : .35d;
        private double ComboTauntDelay => network != null && network.config != null ? network.config.YaguangComboTauntDelaySeconds : .3d;
        private double ClearedDelay => network != null && network.config != null ? network.config.YaguangComboClearedDelaySeconds : .8d;
        private int GuideRuleCombos => network != null && network.config != null ? network.config.YaguangComboGuideRuleCombos : 1;
        private double GuideFeedbackDelay => network != null && network.config != null ? network.config.YaguangComboGuideFeedbackDelaySeconds : .3d;
        private double GuideRuleDelay => network != null && network.config != null ? network.config.YaguangComboGuideRuleDelaySeconds : .9d;
        private bool GuideReadyOn => network == null || network.config == null || network.config.YaguangComboGuideReadyOn;
        private double GuideFailDelay => network != null && network.config != null ? network.config.YaguangComboGuideFailDelaySeconds : .1d;
        private double GuideFailToTaunt => network != null && network.config != null ? network.config.YaguangComboGuideFailToTauntSeconds : .1d;
        private float GuideCountFade => network != null && network.config != null ? network.config.YaguangComboGuideCountFade : .6f;

        /// <summary>
        /// #695 이번 예고(지금까지 나온 콤보 수 = shown)에 금옥이 할 말. 처음 ruleCombos번은 규칙, 그다음 한 번은 준비(readyOn일 때), 그 뒤는 없음.
        /// </summary>
        public static LabYaguangSfx? WarningLine(uint shown, int ruleCombos, bool readyOn)
        {
            if (shown < ruleCombos) return LabYaguangSfx.ComboGuideRule;
            if (readyOn && shown == ruleCombos) return LabYaguangSfx.ComboGuideReady;
            return null;
        }

        /// <summary>
        /// #695 규칙 안내가 있는 예고에서 야광귀 숫자 count의 크기 배율. 넷이 원래 크기이고 셋, 둘, 하나로 갈 때마다 fade를 곱한다
        /// (넷 1 / 셋 fade / 둘 fade² / 하나 fade³).
        /// </summary>
        public static float GuideCountGain(int count, float fade) =>
            Mathf.Pow(Mathf.Clamp01(fade), Mathf.Clamp(4 - count, 0, 3));
        private double CaughtDelay => network != null && network.config != null ? network.config.YaguangFlashCaughtDelaySeconds : .2d;

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

        private bool Play(LabYaguangSfx sfx, AudioSource player, float pitch, float gain = 1f)
        {
            counts[(int)sfx]++;
            AudioClip[] set = clips != null ? clips[(int)sfx] : null;
            if (set == null || set.Length == 0 || player == null) return false;
            player.pitch = pitch;
            player.PlayOneShot(set[Random.Range(0, set.Length)], Volume(sfx) * gain * LabAudioSettings.SfxVolume);
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
                case LabYaguangSfx.FlashTaunt: return config.YaguangFlashTauntSoundVolume;
                case LabYaguangSfx.ShakeBell: return config.YaguangShakeBellSoundVolume;
                case LabYaguangSfx.ShakeBellFinal: return config.YaguangShakeBellFinalSoundVolume;
                case LabYaguangSfx.FlashCaught: return config.YaguangFlashCaughtSoundVolume;
                case LabYaguangSfx.ComboCountOne:
                case LabYaguangSfx.ComboCountTwo:
                case LabYaguangSfx.ComboCountThree:
                case LabYaguangSfx.ComboCountFour: return config.YaguangComboCountSoundVolume;
                case LabYaguangSfx.ComboStart: return config.YaguangComboStartSoundVolume;
                case LabYaguangSfx.ComboTick: return config.YaguangComboTickSoundVolume;
                case LabYaguangSfx.ComboTickFast: return config.YaguangComboTickFastSoundVolume;
                case LabYaguangSfx.ComboTaunt: return config.YaguangComboTauntSoundVolume;
                case LabYaguangSfx.ComboSmallStart: return config.YaguangComboSmallStartSoundVolume;
                case LabYaguangSfx.ComboSmallEnd: return config.YaguangComboSmallEndSoundVolume;
                case LabYaguangSfx.ComboBigStart: return config.YaguangComboBigStartSoundVolume;
                case LabYaguangSfx.ComboBigEnd: return config.YaguangComboBigEndSoundVolume;
                case LabYaguangSfx.ComboClearedFirst:
                case LabYaguangSfx.ComboClearedSecond: return config.YaguangComboClearedSoundVolume;
                case LabYaguangSfx.ComboGuideRule:
                case LabYaguangSfx.ComboGuideHit1:
                case LabYaguangSfx.ComboGuideHit2:
                case LabYaguangSfx.ComboGuideHit3:
                case LabYaguangSfx.ComboGuideClear:
                case LabYaguangSfx.ComboGuideFail:
                case LabYaguangSfx.ComboGuideReady:
                case LabYaguangSfx.ComboGuideRetry: return config.YaguangComboGuideSoundVolume;
                case LabYaguangSfx.ComboStamp: return config.YaguangComboStampSoundVolume;
                default: return config.YaguangFlashBurstSoundVolume;
            }
        }
    }
}
