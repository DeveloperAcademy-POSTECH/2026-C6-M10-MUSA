using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 박 색상 콤보 실험 (#222)의 전투 화면 표시. 모든 기기가 Host 스냅샷만 읽어서 같은 콤보를 보여준다.
    /// - 예고 (#241): 4칸이 나오기 직전 같은 자리에 COMBO! 와 3, 2, 1 카운트다운. 색은 아직 숨긴다.
    /// - 진행 중: 4칸 색 (맞힌 칸은 체크, 다음 칸은 통통 튐), 줄어드는 남은 시간 막대 (숫자 없음)
    /// - 끝남: 콤보 패널이 바로 사라지고 같은 자리에 CLEAR! (성공) / WRONG COLOR, TIME UP (실패) 문구가
    ///   튀어나옴. 실패는 크게 튀고 흔들리며 화면이 빨갛게 번쩍인다.
    /// - 페널티 중: 결과 문구가 끝난 뒤에 빨간 알약 배지 (SMALL 글자, 작은 공 아이콘, 줄어드는 게이지)가
    ///   나온다. 남은 초 숫자는 없다(#257). 이 기기 플레이어 기준이다.
    /// - 보상 중: 배지 대신 전투 화면 위에서 노란 별이 쏟아지고, 남은 시간이 줄수록 별이 줄어든다(#257).
    /// - 이 기기 플레이어의 SMALL/BIG을 조합판에도 알려 판 구슬 그림이 작아지거나 커지게 한다(#257).
    /// - #301: 칸을 맞힐 때마다 패널 아래에 "N COMBO!"(주황 숫자 + 흰 테두리 + 분홍 COMBO!)가 튀어나오고, 4칸을
    ///   다 맞히면 패널 자리에 "4 COMBO!!"가 가장 크게 나온 뒤 CLEAR!가 이어진다. 틀린 색 실패는 누가 틀렸는지
    ///   ("P2 색 틀림", 내가 틀렸으면 "내가 색 틀림")를 모든 기기에 같이 보여준다. 페널티(SMALL)는 게임을 가리지
    ///   않게 공격 영역 왼쪽 아래 구석에 "작게" 글자와 그 밑 세로 사각형으로만 보이고, 사각형 안 빨간 칸이 위에서
    ///   아래로 줄어든다. 보상(BIG)도 같은 자리에 금색 "크게" 글자와 금색 칸으로 보인다(부적 비는 그대로).
    /// - #414: 예고 동안 화면 가장자리가 천천히 어두워지고(박 쪽은 밝게) 4칸이 나오면 걷힌다. N 콤보!는 내 판 북 크기에 맞춰
    ///   작게 보인다(ComboPopFit).
    /// - #411: 화면 문구는 모두 한글이고, 글꼴은 피버타임 배너와 같은 신라문화체 굵은 글씨(LabYaguangFeverHud.ShillaFont)다.
    /// - #664: 구미호 여우구슬 판정도 같은 결과 문구(다 지우면 성공!, 시간이 다 되면 시간 끝!)·실패 번쩍임·BIG/SMALL 표시·부적 비를
    ///   쓴다. 판정 중 화면 위 남은 여우구슬 수와 시간 막대는 LabFoxRoundHud가 같은 자리에 그린다.
    /// 콤보 패널 → 결과 문구 → 배지가 한 번에 하나씩만 보인다 (겹치지 않음). 예고 중에는 배지도 잠시 숨는다.
    /// 앞 콤보의 결과 문구가 떠 있는 동안 예고가 시작되면 결과 문구가 끝난 뒤 남은 카운트다운을 보여준다.
    /// 실험이 꺼져 있으면 스냅샷에 콤보가 없으므로 아무것도 그리지 않는다. 화면 표시만 하며 터치를 막지 않는다.
    /// 일시정지 중이거나 전투가 끝나면 숨는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdComboHud : MonoBehaviour
    {
        public const string ClearText = "성공!";
        public const string WrongColorText = "색 틀림";
        public const string TimeUpText = "시간 끝!";
        public const string PenaltyLabel = "작게";
        public const string RewardLabel = "크게";
        public const string WarningText = "콤보!";
        /// <summary>칸을 맞힐 때 숫자 옆 글자, 다 맞혔을 때 글자 (#301, #411 한글)</summary>
        public const string ComboHitText = "콤보!", ComboClearText = "콤보 완성!!";
        /// <summary>틀린 색 실패에서 내가 틀렸을 때 이름 자리 (#301, #411 한글)</summary>
        public const string YouText = "내가";
        /// <summary>야광귀 셈법 (#271): 야광귀는 넷까지만 센다. 넷 다음은 다시 하나.</summary>
        public static readonly string[] CountWords = { "하나", "둘", "셋", "넷" };
        /// <summary>수 n(1부터)을 야광귀식으로. 넷을 넘으면 넷까지밖에 몰라 다시 하나·둘…로 돈다(예고에서는 넷 → 하나로 거꾸로 센다).</summary>
        public static string CountWord(int n) => CountWords[(Mathf.Max(1, n) - 1) % CountWords.Length];

        private const int TextureSize = 96;
        /// <summary>콤보 결과 문구가 떠 있는 초. 이 뒤에 SMALL/BIG 배지가 나온다 (#398 효과음도 이 시각에 맞춘다).</summary>
        public const float ResultSeconds = 1.6f;
        /// <summary>#668/#695: 콤보 성공 도장이 크게 내려와 박에 찍히는 때 (결과 문구가 시작된 때부터 초). 이때 쾅 소리가 난다.</summary>
        public const float StampLandSeconds = .16f;
        // #664 LabFoxRoundHud도 같은 먹색·패널색·빨강·둥근 패널을 쓴다.
        internal static readonly Color32 InkColor = new Color32(43, 36, 32, 255);
        internal static readonly Color PanelColor = new Color(1f, .98f, .93f, .92f);
        private static readonly Color ClearColor = new Color(.98f, .78f, .16f);
        internal static readonly Color FailColor = new Color(.91f, .16f, .1f);
        // 배지 배경: 결과 문구보다 살짝 차분한 빨강/노랑 (흰 글자가 잘 읽히도록)
        private static readonly Color PenaltyBadgeColor = new Color(.84f, .25f, .2f, 1f);
        private static readonly Color RewardBadgeColor = new Color(.95f, .69f, .13f, 1f);
        // BIG 글자: 밝은 바탕에서도 읽히게 사각형보다 진한 금색 (#301)
        private static readonly Color RewardLabelColor = new Color(.82f, .5f, .04f, 1f);
        private static Sprite discSprite, checkSprite, roundedSprite, pillSprite, vignetteSprite;
        /// <summary>#414 예고 그늘의 밝은 가운데 높이 (화면 아래 0 ~ 위 1). 박이 있는 쪽.</summary>
        public const float ShadeCenterY = .68f;

        [Tooltip("화면에서 콤보 패널 가운데 위치 (0~1, 왼쪽 아래가 0)")]
        [SerializeField] private Vector2 panelAnchor = new Vector2(.5f, .8f);
        [Tooltip("색 칸 하나의 크기 (Canvas 기준 픽셀). 패널/글자/막대가 모두 이 크기에 비례한다.")]
        [SerializeField, Min(16f)] private float slotSize = 40f;
        [Header("N COMBO! 피드백 (#301)")]
        [Tooltip("콤보 숫자 색 (참고 이미지의 주황)")]
        [SerializeField] private Color comboNumberColor = new Color(1f, .55f, .08f, 1f);
        [Tooltip("COMBO! 글자 색")]
        [SerializeField] private Color comboWordColor = new Color(1f, .42f, .62f, 1f);
        [Tooltip("콤보 숫자 크기 (칸 크기의 배수). 다 맞혔을 때는 이것의 1.5배.")]
        [SerializeField, Range(.5f, 4f)] private float comboNumberSize = 1.6f;
        [Tooltip("칸을 맞혔을 때 N COMBO!가 떠 있는 시간 (초)")]
        [SerializeField, Range(.2f, 3f)] private float comboHitSeconds = .8f;
        [Tooltip("다 맞혔을 때 4 COMBO!!가 떠 있는 시간 (초). 그 뒤 CLEAR!가 나온다.")]
        [SerializeField, Range(.2f, 3f)] private float comboClearSeconds = 1f;
        [Header("SMALL 세로 사각형 (#301)")]
        [Tooltip("SMALL 남은 시간 세로 사각형의 폭과 높이 (기준 40px × 칸 크기 비율의 배수)")]
        [SerializeField] private Vector2 penaltyBarSize = new Vector2(1.1f, 2.4f);
        [Tooltip("SMALL 글자와 사각형 사이 간격 (같은 기준의 배수)")]
        [SerializeField, Range(0f, 2f)] private float penaltyBarGap = .15f;
        [Tooltip("공격 영역 왼쪽 아래 구석에서 띄우는 거리 (Canvas 기준 픽셀: 왼쪽, 조합판 위)")]
        [SerializeField] private Vector2 penaltyCornerMargin = new Vector2(16f, 16f);

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer, panel, timeBar;
        private readonly RectTransform[] slots = new RectTransform[LabGourdCombo.Length];
        private readonly Image[] slotFills = new Image[LabGourdCombo.Length];
        private readonly Image[] slotChecks = new Image[LabGourdCombo.Length];
        /// <summary>#719: the soft glow behind the slot to hit now.</summary>
        private readonly Image[] slotGlows = new Image[LabGourdCombo.Length];
        private static Sprite turnGlowSprite;
        /// <summary>#719 시험용: 지금 강조하는 칸 (없으면 -1).</summary>
        public int TurnSlot { get; private set; } = -1;
        private Image timeBarFill;
        private RectTransform gaugeFrame, gaugeContainer;
        private CanvasGroup gaugeGroup, ownedGaugeGroup, panelGroup;
        private readonly Vector3[] gaugeCorners = new Vector3[4];
        private float gaugeAlphaBeforeCover, panelShownAt;
        private bool gaugeCovered, panelWasShown;
        private readonly CanvasGroup[] headingGroups = new CanvasGroup[2];
        private readonly bool[] ownedHeadingGroups = new bool[2];
        private readonly float[] headingAlphas = new float[2];
        private bool headingCovered;
        private bool figmaPanel;
        private readonly LabOrbElement[] slotElements = new LabOrbElement[LabGourdCombo.Length];
        private Text resultText;
        private Image successStamp;
        // #668 도장은 야광귀 콤보 성공에만 찍는다. #664 구미호 여우구슬 성공도 같은 결과 문구를 쓰지만 도장 없이 성공!을 띄운다.
        private bool StampSuccess => YaguangCount && !resultIsFoxRound && successStamp != null && successStamp.sprite != null;
        private Image flash;
        private float unit = 1f;
        private RectTransform effectBadge, effectGauge, effectIcon;
        private Image effectGaugeFill;
        private Image effectBack;
        private Text effectText;
        private int effectKind; // 0 없음, -1 페널티, 1 보상
        private float effectPopElapsed = -1f;
        private Font font;
        private RectTransform warningPanel;
        private Text warningLabel, warningNumber;
        private int shownWarningCount = -1;
        private bool warningYaguang;
        private bool warningActive;
        // 야광귀 셈법 (#271): 칸 위의 수, 넷 다음의 흐린 ? 칸
        private readonly Text[] slotNumbers = new Text[LabGourdCombo.Length];
        private readonly bool[] wasChecked = new bool[LabGourdCombo.Length];
        private readonly float[] checkAges = new float[LabGourdCombo.Length];
        private readonly Text[] slotWords = new Text[LabGourdCombo.Length];
        private RectTransform questionSlot;
        private float warningPopElapsed = -1f;
        // #414 예고 그늘, 빈 칸 미리 보기, 봐준 틀림 경고
        private Image warningShade;
        private float shadeAlpha;

        private LabComboRewardStars stars;
        private int boardLook;
        private bool starsWereOn;

        private uint shownResultCount;
        private int shownFoxRoundCount;
        // #664: the result words on screen are the fox round's (they sit at panelAnchor, not over the paint gauge).
        private bool resultIsFoxRound;
        private float resultElapsed = -1f;
        private LabComboResult shownResult;
        private int lastProgress;
        private uint lastComboCount;
        private float popElapsed = -1f;
        private int popSlot = -1;
        /// <summary>#719: how many of the next combo's colours the warning has shown so far.</summary>
        private int previewRevealed;
        /// <summary>#719 시험용: 예고에서 지금 보이는 다음 콤보 색 칸 수 (0이면 미리 보기 없음).</summary>
        public int PreviewRevealed => previewRevealed;
        private ulong shownFailedBy;
        private bool shownFailedKnown;
        private int shownFailedSeat = -1;
        private RectTransform comboPop;
        private Text comboPopNumber, comboPopWord;
        private float comboPopElapsed = -1f;
        private int comboPopCount;
        private bool comboPopFinal;
        private uint comboPopCombo;
        private float resultDelay;
        private float comboPopFit = 1f;

        /// <summary>
        /// #414 N 콤보! 크기 배율: "N 콤보!" 전체 높이(숫자 + 글자)가 내 판 북 지름 × LabConfig Combo Pop Drum Ratio(기본 0.35)가 되게 한다.
        /// 원래 크기보다 커지지는 않는다(최대 1). 북을 못 찾으면 원래 크기의 0.6배.
        /// </summary>
        private float ComboPopFit()
        {
            const float Fallback = .6f;
            float ratio = network != null && network.config != null ? network.config.ComboPopDrumRatio : .35f;
            if (ratio <= 0f) return 1f;
            if (canvas == null || network == null || network.board == null) return Fallback;
            if (!network.board.TryGetDrumScreenDiameter(out float pixels)) return Fallback;
            float canvasUnits = pixels / Mathf.Max(.01f, canvas.scaleFactor);
            float popHeight = comboPop != null ? comboPop.sizeDelta.y : slotSize * comboNumberSize * 1.3f;
            float fit = popHeight > 0f ? Mathf.Clamp(canvasUnits * ratio / popHeight, .15f, 1f) : Fallback;
            return fit;
        }

        public bool PanelVisible => panel != null && panel.gameObject.activeSelf;
        /// <summary>#414 예고 그늘의 지금 진하기 (0~1, 테스트·확인용)</summary>
        public float WarningShadeAlpha => shadeAlpha;

        public bool EffectVisible => effectBadge != null && effectBadge.gameObject.activeSelf;
        public string EffectLabel => effectText != null ? effectText.text : string.Empty;
        /// <summary>보상(BIG) 별이 지금 쏟아지고 있는지 (#257)</summary>
        public bool StarsEmitting => stars != null && stars.Emitting;
        public int StarCount => stars != null ? stars.ActiveCount : 0;
        public float StarsPerSecond => stars != null ? stars.StarsPerSecondNow : 0f;
        /// <summary>조합판에 알린 이 기기 플레이어의 콤보 효과: -1 SMALL, 1 BIG, 0 없음 (#257)</summary>
        public int BoardLook => boardLook;
        public bool ResultVisible => resultText != null && resultText.gameObject.activeSelf;
        /// <summary>#695: 이 화면에서 성공 도장이 박에 찍힌 횟수. 도장 쾅 소리(LabYaguangSound)가 이 수가 늘 때 난다.</summary>
        public int StampsLanded { get; private set; }
        private uint stampLandedFor;
        /// <summary>COMBO! 카운트다운 예고가 보이는지 (#241)</summary>
        public bool WarningVisible => warningPanel != null && warningPanel.gameObject.activeSelf;
        /// <summary>예고에 보이는 남은 초 (3, 2, 1). 예고가 없으면 빈 문자열.</summary>
        /// <summary>패널의 예고 남은 초. 야광귀 셈법이면 수는 말풍선에만 있으므로 빈 문자열.</summary>
        public string WarningCount => WarningVisible ? warningNumber.text : string.Empty;
        /// <summary>콤보 예고 중인지 (야광귀 셈법이면 패널 없이 말풍선만 센다, #271).</summary>
        public bool WarningCounting => warningActive;
        /// <summary>칸 위에 뜬 야광귀의 수 (#271). 안 떠 있으면 빈 문자열.</summary>
        public string SlotWord(int index) =>
            slotWords[index] != null && slotWords[index].gameObject.activeInHierarchy ? slotWords[index].text : string.Empty;
        /// <summary>넷 다음의 흐린 ? 칸이 보이는지 (#271)</summary>
        public bool QuestionSlotVisible => questionSlot != null && questionSlot.gameObject.activeInHierarchy;
        /// <summary>야광귀 셈법으로 보여주는지 (#271, DEV `야광귀 셈법`)</summary>
        public bool YaguangCount => network != null && network.config != null && network.config.ComboYaguangCountEnabled;
        public string ResultLabel => resultText != null ? resultText.text : string.Empty;
        /// <summary>지금 떠 있는 N COMBO! 숫자 (#301). 안 떠 있으면 0.</summary>
        public int ComboPopCount => comboPop != null && comboPop.gameObject.activeSelf ? comboPopCount : 0;
        /// <summary>지금 떠 있는 것이 다 맞힌 "4 COMBO!!"인지 (#301)</summary>
        public bool ComboPopFinal => ComboPopCount > 0 && comboPopFinal;
        /// <summary>SMALL 세로 막대의 남은 비율 (0~1, #301)</summary>
        public float PenaltyBarFill => effectGauge != null ? effectGauge.anchorMax.y : 0f;
        /// <summary>SMALL 남은 시간 세로 사각형 (테두리 포함, #301)</summary>
        public RectTransform PenaltyBar => effectGauge != null && effectGauge.parent != null
            ? effectGauge.parent.parent as RectTransform : null;
        /// <summary>SMALL 표시 전체 (글자 + 사각형, #301). 왼쪽 아래 모서리가 기준점이다.</summary>
        public RectTransform EffectBadge => effectBadge;

        /// <summary>
        /// 콤보 결과 문구 (#301). 성공은 CLEAR!, 시간 초과는 TIME UP. 틀린 색은 틀린 사람을 앞에 붙인다:
        /// 이 기기 플레이어면 "내가 색 틀림", 아니면 좌석 번호로 "P2 색 틀림". 누군지 모르면 이름 없이.
        /// </summary>
        public static string ResultTextFor(LabComboResult result, bool failedKnown, bool failedIsMe, int failedSeat)
        {
            if (result == LabComboResult.Success) return ClearText;
            if (result == LabComboResult.Timeout) return TimeUpText;
            if (!failedKnown) return WrongColorText;
            if (failedIsMe) return YouText + " " + WrongColorText;
            return failedSeat >= 0 ? "P" + (failedSeat + 1) + " " + WrongColorText : WrongColorText;
        }
        public bool SlotChecked(int index) => slotChecks[index] != null && slotChecks[index].gameObject.activeSelf;
        public Color SlotColor(int index) => figmaPanel ? (Color)LabPaintPalette.SrgbColorFor(slotElements[index])
            : slotFills[index] != null ? slotFills[index].color : Color.clear;

        /// <summary>스냅샷을 읽을 네트워크와, 패널을 그릴 Canvas를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            ReleaseGauge();
            if (network != null) network.ComboThrowHeld -= OnComboThrowHeld;
            network = target;
            if (network != null) network.ComboThrowHeld += OnComboThrowHeld;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
        }

        /// <summary>#719: a throw was turned away by the combo (warning, or a five-colour orb mid-combo): the rope jolts.</summary>
        private void OnComboThrowHeld(bool warning)
        {
            LabComboThrowSeal seal = network != null ? LabComboThrowSeal.For(network.board) : null;
            if (seal != null) seal.Knock();
        }

        /// <summary>#719: the rope across the top of my board is up for the whole combo warning (no throws then).</summary>
        private void HoldThrowSeal(bool held)
        {
            if (network == null || network.board == null) return;
            LabComboThrowSeal seal = held ? LabComboThrowSeal.For(network.board)
                : network.board.GetComponentInChildren<LabComboThrowSeal>(true);
            if (seal != null) seal.SetHeld(held);
        }

        private float tickBeat;

        // ---- #719 틀린 색 X: 박에 틀린 색이 맞으면 모두의 화면에서 맞혀야 했던 칸에 빨간 X가 쾅 찍히고 칸이 흔들린다 ----
        /// <summary>봐준 틀린 색(실패 아님)의 X가 떠 있는 시간, 진짜 실패 때 결과 문구 전에 패널과 X를 붙잡아 두는 시간.</summary>
        public const float WrongForgivenSeconds = .9f, WrongFailHoldSeconds = .65f;
        private bool wrongSeenInit;
        private uint seenWrongCount;
        private float wrongElapsed = -1f;
        private int wrongSlot;
        private bool wrongFails;
        private Image wrongMark, edgeFlash;
        private static Sprite wrongSprite;
        public bool WrongMarkShown => wrongMark != null && wrongMark.gameObject.activeInHierarchy;
        public int WrongMarkSlot => wrongElapsed >= 0f ? wrongSlot : -1;
        public bool WrongMarkFails => wrongElapsed >= 0f && wrongFails;

        /// <summary>#719: a new wrong colour hit the gourd (forgiven or failing) → stamp an X on the slot it missed.</summary>
        private void DetectWrong(LabSnapshot snapshot)
        {
            if (!wrongSeenInit || snapshot.comboWrongCount < seenWrongCount)
            {
                wrongSeenInit = true;
                seenWrongCount = snapshot.comboWrongCount;
                return;
            }
            if (snapshot.comboWrongCount == seenWrongCount) return;
            seenWrongCount = snapshot.comboWrongCount;
            wrongElapsed = 0f;
            wrongSlot = Mathf.Clamp(snapshot.comboWrongSlot, 0, LabGourdCombo.Length - 1);
            // The same snapshot carries a new WrongColor result when this wrong colour ended the combo.
            wrongFails = snapshot.comboResult == LabComboResult.WrongColor && snapshot.comboResultCount != shownResultCount;
            // A real failure already buzzes long (LabHaptics); a forgiven one gets a short sharp knock.
            if (!wrongFails && network != null) LabHaptics.PlayDirect(network.config, LabHapticStyle.Rigid, 1f, .18f, .8f, .7f);
        }

        /// <summary>#719: how far the missed slot is shaken sideways right now.</summary>
        private float WrongShake(int slot, float slotWidth)
        {
            if (wrongElapsed < 0f || slot != wrongSlot) return 0f;
            float k = Mathf.Clamp01(1f - wrongElapsed / .45f);
            return Mathf.Sin(wrongElapsed * 80f) * slotWidth * (wrongFails ? .28f : .18f) * k * k;
        }

        private void DrawWrong()
        {
            if (wrongElapsed >= 0f) wrongElapsed += Time.unscaledDeltaTime;
            float duration = wrongFails ? WrongFailHoldSeconds : WrongForgivenSeconds;
            if (wrongElapsed >= duration) wrongElapsed = -1f;
            bool show = wrongElapsed >= 0f && panel.gameObject.activeSelf;
            EnsureWrongParts();
            wrongMark.gameObject.SetActive(show);
            float t = Mathf.Max(0f, wrongElapsed);
            float edge = wrongElapsed >= 0f ? Mathf.Clamp01(1f - t / .45f) : 0f;
            edgeFlash.gameObject.SetActive(edge > 0f);
            edgeFlash.color = new Color(FailColor.r, FailColor.g, FailColor.b, (wrongFails ? .85f : .6f) * edge * edge);
            if (wrongElapsed >= 0f && wrongFails)
            {
                // The big one: the whole screen blinks red once while the X lands (the result words blink again after).
                float blink = t < .12f ? 1f - t / .12f : 0f;
                flash.gameObject.SetActive(blink > 0f);
                flash.color = new Color(FailColor.r, FailColor.g, FailColor.b, .38f * blink);
            }
            if (!show) return;

            RectTransform slot = slots[wrongSlot];
            float alpha = Mathf.Clamp01(t / .05f) * (wrongFails ? 1f : Mathf.Clamp01((duration - t) / .25f));
            // X: slams down from big onto the slot, tilted, then sits.
            var mark = wrongMark.rectTransform;
            if (mark.parent != slot) mark.SetParent(slot, false);
            mark.SetAsLastSibling();
            mark.anchorMin = mark.anchorMax = new Vector2(.5f, .5f);
            mark.sizeDelta = slot.sizeDelta * 1.25f;
            mark.anchoredPosition = Vector2.zero;
            float land = Mathf.Clamp01(t / .11f);
            float slam = Mathf.Lerp(wrongFails ? 2.8f : 2.1f, 1f, 1f - (1f - land) * (1f - land) * (1f - land));
            mark.localScale = Vector3.one * slam * (wrongFails ? 1.2f : 1f);
            mark.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-22f, -6f, land));
            wrongMark.color = new Color(1f, 1f, 1f, alpha);
        }

        private void EnsureWrongParts()
        {
            if (wrongMark != null) return;
            wrongMark = NewRect("Combo Wrong X", panel).gameObject.AddComponent<Image>();
            wrongMark.sprite = WrongSprite();
            wrongMark.raycastTarget = false;
            wrongMark.gameObject.SetActive(false);
            // Red screen edges (centre stays clear so the board is still readable).
            RectTransform edgeRect = NewRect("Combo Wrong Edge", layer);
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = edgeRect.offsetMax = Vector2.zero;
            edgeFlash = edgeRect.gameObject.AddComponent<Image>();
            edgeFlash.sprite = VignetteSprite();
            edgeFlash.color = Color.clear;
            edgeFlash.raycastTarget = false;
            edgeRect.gameObject.SetActive(false);
        }

        /// <summary>#719: a thick hand-drawn red X with a dark ink edge.</summary>
        private static Sprite WrongSprite()
        {
            if (wrongSprite != null) return wrongSprite;
            float s = TextureSize;
            Vector2 a = new Vector2(s * .2f, s * .2f), b = new Vector2(s * .8f, s * .8f);
            Vector2 c2 = new Vector2(s * .2f, s * .8f), d = new Vector2(s * .8f, s * .2f);
            wrongSprite = Draw("Lab combo wrong", (p, c) =>
            {
                float distance = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, c2, d));
                float core = Mathf.Clamp01(s * .08f + .5f - distance);
                float edge = Mathf.Clamp01(s * .08f + 4f + .5f - distance);
                Color color = Color.Lerp((Color)InkColor, FailColor, core);
                color.a = edge;
                return color;
            }, Vector4.zero);
            return wrongSprite;
        }

        /// <summary>#719: 1 right on each combo clock tick (the same ticks LabYaguangSound plays), fading to 0 before the next.
        /// The combo's start (the 깽) counts as the first beat.</summary>
        private float TickBeat(LabSnapshot snapshot, double serverNow)
        {
            LabConfig config = network != null ? network.config : null;
            double fastFor = config != null ? config.YaguangComboTickFastSeconds : LabYaguangSound.DefaultTickFastSeconds;
            double every = config != null ? config.YaguangComboTickIntervalSeconds : LabYaguangSound.DefaultTickInterval;
            double fastEvery = config != null ? config.YaguangComboTickFastIntervalSeconds : LabYaguangSound.DefaultTickFastInterval;
            double tick = LabYaguangSound.LatestTickAt(snapshot.comboStartedAt, snapshot.comboEndsAt, serverNow,
                fastFor, every, fastEvery, out bool fast);
            if (double.IsNegativeInfinity(tick)) tick = snapshot.comboStartedAt;
            double since = System.Math.Max(0d, serverNow - tick);
            double fade = (fast ? fastEvery : every) * .45d;
            return Mathf.Clamp01((float)System.Math.Exp(-since / System.Math.Max(.03d, fade)));
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool battle = snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused;
            double serverNow = battle ? network.BattlePresentationTime : 0d;
            // #719: my combined orbs of the colour the combo needs now beat in that colour (one glance = what to throw).
            LabOrbElement needed = snapshot != null && snapshot.phase == LabPhase.Playing && snapshot.comboActive
                && snapshot.comboColors != null && snapshot.comboProgress >= 0 && snapshot.comboProgress < snapshot.comboColors.Length
                ? snapshot.comboColors[snapshot.comboProgress] : LabOrbElement.None;
            // #719: the glow beats on the clock's tick (똑딱), not on its own rhythm.
            tickBeat = needed != LabOrbElement.None ? TickBeat(snapshot, serverNow) : 0f;
            LabOrbComboHint.Beat = tickBeat;
            if (network != null && network.board != null) network.board.SetComboHint(needed);
            HoldThrowSeal(snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused && !snapshot.comboActive
                && snapshot.ComboWarningOn(network.BattlePresentationTime));
            // 조합판 구슬 크기(#257)는 콤보 화면이 숨어 있어도 정한다. 일시정지 중에는 그대로 둔다.
            if (snapshot == null || snapshot.phase != LabPhase.Playing) SetBoardLook(0);
            else if (!snapshot.paused) SetBoardLook(LocalEffect(snapshot, serverNow, out _));
            // 첫 콤보 예고 (#241)는 아직 콤보가 한 번도 안 나왔을 때 시작하므로 따로 본다.
            bool warning = battle && snapshot.ComboWarningOn(serverNow);
            // #664: 여우구슬 판정 결과가 하나라도 나왔으면 결과 문구와 BIG/SMALL 표시를 그린다.
            bool anyCombo = snapshot != null && (snapshot.comboCount > 0 || warning || snapshot.foxRoundCount > 0);
            if (!battle || !anyCombo)
            {
                CoverGauge(false);
                panelWasShown = false;
                if (panelGroup != null) panelGroup.alpha = 0f;
                if (layer != null) layer.gameObject.SetActive(false);
                if (snapshot == null || snapshot.phase != LabPhase.Playing) ResetRound();
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();

            if (snapshot.comboCount < lastComboCount || snapshot.foxRoundCount < shownFoxRoundCount) ResetRound();
            DetectWrong(snapshot);
            DetectChanges(snapshot);
            DetectFoxRound(snapshot, serverNow);
            DrawCombo(snapshot, serverNow, warning);
            DrawResult();
            DrawWrong();
            DrawWarning(snapshot, serverNow, warning);
            DrawShade(warning);
            DrawEffect(snapshot, serverNow);
            // #664: the fox round's result words never sit over the gauge, so they leave it showing.
            CoverGauge(figmaPanel && (panel.gameObject.activeSelf || resultText.gameObject.activeSelf && !resultIsFoxRound
                || comboPopFinal && comboPop.gameObject.activeSelf));
        }

        /// <summary>#414 예고 그늘: 예고 동안 천천히 진해지고, 4칸이 나오면 빨리 걷힌다.</summary>
        private void DrawShade(bool warning)
        {
            float maxShade = network != null && network.config != null ? network.config.YaguangComboWarningShade : .5f;
            float target = warning ? maxShade : 0f;
            float speed = warning ? maxShade / .8f : maxShade / .3f;
            shadeAlpha = Mathf.MoveTowards(shadeAlpha, target, Mathf.Max(.01f, speed) * Time.unscaledDeltaTime);
            warningShade.gameObject.SetActive(shadeAlpha > .001f);
            warningShade.color = new Color(0f, 0f, 0f, shadeAlpha);
        }

        /// <summary>
        /// 콤보 예고 (#241): 패널 자리에 COMBO! 와 남은 초(3 → 2 → 1). 숫자가 바뀔 때마다 톡 튀어나오고,
        /// 0이 되는 순간(4칸이 나오는 시각) Host 스냅샷의 새 콤보로 바뀐다. #719부터 야광귀 예고는 패널에 다음 콤보 색을 한 칸씩 미리 보여 준다.
        /// 앞 콤보의 결과 문구가 떠 있으면 그것부터 보여준다.
        /// </summary>
        private void DrawWarning(LabSnapshot snapshot, double serverNow, bool warning)
        {
            bool show = warning && resultElapsed < 0f;
            bool yaguang = YaguangCount;
            warningActive = show;
            // 야광귀 셈법 (#271): 박 옆 말풍선만 하나·둘·셋·넷으로 세고, 패널 자리의 COMBO! 예고는 띄우지 않는다.
            warningPanel.gameObject.SetActive(show && !yaguang);
            if (!show)
            {
                shownWarningCount = -1;
                warningPopElapsed = -1f;
                return;
            }
            // #719: the warning is split in four (넷 → 하나), so a longer warning counts more slowly.
            int count = Mathf.Max(1, snapshot.ComboWarningCount(serverNow));
            if (yaguang != warningYaguang) shownWarningCount = -1;
            warningYaguang = yaguang;
            // 말풍선은 남은 초를 야광귀식으로 넷 → 셋 → 둘 → 하나 센다(넷까지밖에 몰라서 더 길면 다시 넷부터).
            if (count != shownWarningCount)
            {
                shownWarningCount = count;
                warningPopElapsed = 0f;
                warningNumber.text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                warningLabel.text = WarningText;
                if (yaguang && network != null)
                {
                    LabGourdAlert alert = GetComponent<LabGourdAlert>();
                    // 말풍선 하나가 4칸이 나올 때까지 떠 있고 글자만 바뀐다.
                    if (alert != null)
                        alert.ShowWord(network.target, CountWord(count) + "!", Mathf.Max(.3f, (float)(snapshot.comboNextAt - serverNow)));
                }
            }
            if (yaguang) return;
            // 숫자: 1.7배로 쾅 나왔다가 1배로. 마지막 1은 빨갛다.
            float pop = 1f;
            if (warningPopElapsed >= 0f)
            {
                warningPopElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(warningPopElapsed / .25f);
                pop = Mathf.Lerp(1.7f, 1f, 1f - (1f - t) * (1f - t));
                if (warningPopElapsed >= .25f) warningPopElapsed = -1f;
            }
            warningNumber.rectTransform.localScale = Vector3.one * pop;
            warningNumber.color = count <= 1 ? FailColor : (Color)InkColor;
            warningLabel.rectTransform.localScale = Vector3.one;
            warningLabel.color = ClearColor;
            // COMBO! 글자는 살짝 기울며 들썩인다. 남은 시간이 줄수록 빨라진다.
            float speed = 6f + 4f * (3 - Mathf.Min(count, 3));
            warningLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(Time.unscaledTime * speed));
            warningPanel.localScale = Vector3.one * (1f + .04f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * speed * .5f)));
        }

        /// <summary>
        /// 이 기기 플레이어의 페널티(우선) 또는 보상 배지. 게이지는 효과가 시작된 시각(마지막 콤보 결과)부터
        /// 끝 시각까지를 1 → 0으로 줄인다. 새로 생기거나 종류가 바뀌면 톡 튀어나오고, 마지막 3초는 두근거린다.
        /// </summary>
        /// <summary>이 기기 플레이어의 콤보 효과: -1 페널티(SMALL, 우선), 1 보상(BIG), 0 없음과 그 끝 시각</summary>
        private int LocalEffect(LabSnapshot snapshot, double serverNow, out double until)
        {
            until = 0d;
            if (snapshot == null || snapshot.players == null || network == null) return 0;
            foreach (LabPlayerState player in snapshot.players)
            {
                if (player == null || player.id != network.LocalPlayerId) continue;
                if (player.comboPenaltyUntil > serverNow) { until = player.comboPenaltyUntil; return -1; }
                if (player.comboRewardUntil > serverNow) { until = player.comboRewardUntil; return 1; }
            }
            return 0;
        }

        private void SetBoardLook(int look)
        {
            boardLook = look;
            if (network != null && network.board != null) network.board.SetComboLook(look);
        }

        /// <summary>
        /// 보상(BIG) 별 (#257). 결과 문구가 끝난 뒤부터 보상이 끝날 때까지 쏟아지고, 남은 시간 비율만큼 줄어든다.
        /// 별은 화면 위에서 조합판 위쪽 가장자리까지 떨어진다.
        /// </summary>
        private void DrawStars(bool on, float fraction)
        {
            if (stars == null) return;
            float bottom = StarsBottom();
            if (on && !starsWereOn) stars.Burst(bottom);
            starsWereOn = on;
            stars.Step(on, fraction, Time.unscaledDeltaTime, bottom);
        }

        /// <summary>
        /// SMALL 표시의 왼쪽 아래 모서리 위치 (#301): 화면 왼쪽 가장자리, 조합판 위쪽 가장자리 바로 위(공격 영역의 왼쪽 아래).
        /// 조합판 위치를 모르면 화면 높이의 45% 지점. 레이어 가운데 기준 좌표.
        /// </summary>
        private Vector2 PenaltyCorner()
        {
            Rect area = layer.rect;
            float bottom = Mathf.Lerp(area.yMin, area.yMax, .45f);
            if (network != null && network.board != null && network.board.TryGetInputScreenRect(out Rect screen))
            {
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, new Vector2(screen.xMin, screen.yMax),
                    uiCamera, out Vector2 local)) bottom = local.y;
            }
            return new Vector2(area.xMin + penaltyCornerMargin.x, bottom + penaltyCornerMargin.y);
        }

        /// <summary>별이 사라지는 높이: 조합판 위쪽 가장자리(레이어 로컬 y). 모르면 화면 높이의 45% 지점.</summary>
        private float StarsBottom()
        {
            Rect area = layer.rect;
            float fallback = Mathf.Lerp(area.yMin, area.yMax, .45f);
            if (network == null || network.board == null || !network.board.TryGetInputScreenRect(out Rect screen))
                return fallback;
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(stars.Area,
                new Vector2(screen.center.x, screen.yMax), uiCamera, out Vector2 local) ? local.y : fallback;
        }

        private void DrawEffect(LabSnapshot snapshot, double serverNow)
        {
            int kind = LocalEffect(snapshot, serverNow, out double until);
            // 효과는 마지막 결과(콤보 또는 #664 여우구슬 판정) 시각부터 끝 시각까지 줄어든다.
            snapshot.LastEffectResult(out double effectFrom);
            double totalTime = until - effectFrom;
            float remaining = kind != 0 && totalTime > 0d ? Mathf.Clamp01((float)((until - serverNow) / totalTime)) : 0f;
            // 보상은 부적 비로도 보여준다. CLEAR!가 뜨는 순간부터 바로 쏟아진다 (#271).
            DrawStars(kind > 0, remaining);
            // SMALL과 BIG 모두 왼쪽 아래 구석의 글자 + 세로 사각형으로 남은 시간을 보여준다 (#301).
            // 결과 문구나 다음 콤보 예고(#241)가 떠 있는 동안에는 숨겨 두었다가, 끝나면 톡 튀어나온다.
            if (kind == 0 || resultElapsed >= 0f || warningActive)
            {
                effectKind = 0;
                effectBadge.gameObject.SetActive(false);
                return;
            }
            // 게임을 가리지 않게 공격 영역(조합판 위 전투 화면)의 왼쪽 아래 구석에 둔다 (#301).
            effectBadge.anchoredPosition = PenaltyCorner();
            if (kind != effectKind) effectPopElapsed = 0f;
            effectKind = kind;
            effectBadge.gameObject.SetActive(true);

            double left = until - serverNow;
            double total = totalTime;
            float fraction = total > 0d ? Mathf.Clamp01((float)(left / total)) : 1f;
            effectGauge.anchorMax = new Vector2(1f, fraction); // 세로 사각형: 위에서 아래로 줄어든다 (#301)
            bool reward = kind > 0;
            effectBack.color = Color.clear;
            // 남은 시간은 숫자 없이 게이지로만 (#257). SMALL은 빨강, BIG은 금색 (#301).
            effectText.text = reward ? RewardLabel : PenaltyLabel;
            effectText.color = reward ? RewardLabelColor : PenaltyBadgeColor;
            effectGaugeFill.color = reward ? RewardBadgeColor : PenaltyBadgeColor;

            float scale = 1f;
            if (effectPopElapsed >= 0f)
            {
                effectPopElapsed += Time.unscaledDeltaTime;
                float t = effectPopElapsed;
                scale = t < .16f ? 1.2f * (1f - Mathf.Pow(1f - t / .16f, 3f))
                    : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - .16f) / .14f));
                if (t >= .3f) effectPopElapsed = -1f;
            }
            else if (left <= 3d)
                scale += .06f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f));
            effectBadge.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// #664 여우구슬 판정 결과: 판정 순번이 늘면 콤보 결과와 같은 자리에 성공!(다 지움) 또는 시간 끝!(못 지움)을 띄운다.
        /// 결과 문구가 다 사라질 만큼 지난 결과(늦게 들어옴)는 띄우지 않는다.
        /// </summary>
        private void DetectFoxRound(LabSnapshot snapshot, double serverNow)
        {
            if (snapshot.foxRoundCount == shownFoxRoundCount) return;
            shownFoxRoundCount = snapshot.foxRoundCount;
            if (snapshot.foxRoundResult == 0 || snapshot.foxRoundResultAt < 0d
                || serverNow - snapshot.foxRoundResultAt > ResultSeconds) return;
            shownResult = snapshot.foxRoundResult > 0 ? LabComboResult.Success : LabComboResult.Timeout;
            resultElapsed = Mathf.Max(0f, (float)(serverNow - snapshot.foxRoundResultAt));
            resultDelay = 0f;
            resultIsFoxRound = true;
            resultText.text = ResultTextFor(shownResult, false, false, -1);
        }

        private void DetectChanges(LabSnapshot snapshot)
        {
            if (snapshot.comboResultCount != shownResultCount && snapshot.comboResult != LabComboResult.None)
            {
                shownResultCount = snapshot.comboResultCount;
                shownResult = snapshot.comboResult;
                resultIsFoxRound = false;
                resultElapsed = 0f;
                RememberFailer(snapshot);
                resultText.text = ResultTextFor(shownResult, shownFailedKnown,
                    network != null && shownFailedBy == network.LocalPlayerId, shownFailedSeat);
                // 다 맞혔으면(#301) 4 COMBO!!를 먼저 크게 보여준 다음 CLEAR!.
                resultDelay = 0f;
                if (shownResult == LabComboResult.Success && StampSuccess)
                {
                    comboPopElapsed = -1f;
                    comboPop.gameObject.SetActive(false);
                    resultDelay = .28f; // Let the fourth check land before the success seal.
                }
                if (shownResult == LabComboResult.Success && !StampSuccess)
                {
                    ShowComboPop(LabGourdCombo.Length, true, snapshot.comboCount);
                    resultDelay = comboClearSeconds;
                }
                // #719: a wrong colour first stamps its X on the missed slot (panel held), then the result words.
                if (shownResult == LabComboResult.WrongColor && wrongFails && wrongElapsed >= 0f)
                    resultDelay = WrongFailHoldSeconds;
            }
            if (snapshot.comboCount != lastComboCount)
            {
                lastComboCount = snapshot.comboCount;
                lastProgress = 0;
                System.Array.Clear(wasChecked, 0, wasChecked.Length);
            }
            if (snapshot.comboProgress > lastProgress)
            {
                popSlot = snapshot.comboProgress - 1;
                popElapsed = 0f;
                // 칸을 맞힐 때마다 N COMBO! (#301). 마지막 칸은 성공 결과에서 크게 보여준다.
                if (snapshot.comboProgress < LabGourdCombo.Length)
                    ShowComboPop(snapshot.comboProgress, false, snapshot.comboCount);
            }
            lastProgress = snapshot.comboProgress;
        }

        private void DrawCombo(LabSnapshot snapshot, double serverNow, bool warning)
        {
            // 결과 문구가 떠 있는 동안에는 패널을 숨긴다 (콤보 → 결과 → 배지가 겹치지 않게).
            bool finalCheck = StampSuccess && shownResult == LabComboResult.Success && resultDelay > 0f;
            // #719: a failing wrong colour keeps the panel up a moment so its X lands on the missed slot.
            bool wrongHold = shownResult == LabComboResult.WrongColor && resultDelay > 0f;
            bool yaguang = YaguangCount;
            // #719: during the warning the panel shows the next combo's real colours, one more on each count (넷 → 하나),
            // so the order can be read (and the orbs made) before it starts — without hearing anything.
            bool preview = yaguang && warning && !snapshot.comboActive && resultElapsed < 0f && !finalCheck && !wrongHold
                && snapshot.comboNextColors != null && snapshot.comboNextColors.Length == LabGourdCombo.Length;
            int revealed = preview ? Mathf.Clamp(LabGourdCombo.Length + 1 - snapshot.ComboWarningCount(serverNow), 0, LabGourdCombo.Length) : 0;
            if (revealed > previewRevealed) { popSlot = revealed - 1; popElapsed = 0f; }
            previewRevealed = revealed;
            LabOrbElement[] colors = preview ? snapshot.comboNextColors : snapshot.comboColors;
            bool showPanel = preview || (snapshot.comboActive && resultElapsed < 0f || finalCheck || wrongHold) && snapshot.comboColors != null
                && snapshot.comboColors.Length == LabGourdCombo.Length;
            figmaPanel = yaguang && LabYaguangComboArt.Load("Panel") != null;
            ApplyPanelArtwork(figmaPanel);
            panel.gameObject.SetActive(showPanel);
            if (showPanel && !panelWasShown) panelShownAt = Time.unscaledTime;
            panelWasShown = showPanel;
            panelGroup.alpha = showPanel ? Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - panelShownAt) / .22f) : 0f;
            if (!showPanel) { TurnSlot = -1; return; }
            questionSlot.gameObject.SetActive(yaguang && !figmaPanel);
            if (yaguang)
                questionSlot.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(Time.unscaledTime * 2.3f));

            if (popElapsed >= 0f) popElapsed += Time.unscaledDeltaTime;
            TurnSlot = -1;
            for (int i = 0; i < LabGourdCombo.Length; i++)
            {
                slotElements[i] = colors[i];
                Sprite art = figmaPanel ? LabYaguangComboArt.Orb(slotElements[i]) : null;
                slotFills[i].sprite = art != null ? art : DiscSprite();
                slotFills[i].color = art != null ? Color.white : (Color)LabPaintPalette.SrgbColorFor(slotElements[i]);
                bool hit = !preview && i < snapshot.comboProgress;
                bool shown = !preview || i < revealed; // #719: not yet counted = an empty numbered slot
                slotChecks[i].gameObject.SetActive(hit);
                slotFills[i].enabled = shown && (!figmaPanel || !hit);
                slotNumbers[i].gameObject.SetActive(figmaPanel && !hit);
                if (figmaPanel)
                {
                    if (hit && !wasChecked[i]) checkAges[i] = 0f;
                    if (hit) checkAges[i] += Time.unscaledDeltaTime;
                    float age = checkAges[i];
                    float stampScale = age < .12f ? Mathf.Lerp(.35f, 1.25f, Mathf.Clamp01(age / .12f))
                        : Mathf.Lerp(1.25f, 1f, Mathf.SmoothStep(0f, 1f, (age - .12f) / .16f));
                    slotChecks[i].rectTransform.localScale = Vector3.one * stampScale;
                    slotChecks[i].rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-18f, 0f, Mathf.Clamp01(age / .18f)));
                    slotChecks[i].color = new Color(1, 1, 1, Mathf.Clamp01(age / .07f));
                }
                else
                {
                    slotChecks[i].rectTransform.localScale = Vector3.one;
                    slotChecks[i].rectTransform.localRotation = Quaternion.identity;
                    slotChecks[i].color = Color.white;
                }
                wasChecked[i] = hit;
                float scale = 1f;
                // #719: the slot to hit now is bigger and glows in its colour, beating like a heart; later slots are faded,
                // so one glance at the panel shows which colour to throw.
                bool turn = !preview && snapshot.comboActive && i == snapshot.comboProgress;
                bool later = !preview && snapshot.comboActive && i > snapshot.comboProgress;
                float beat = tickBeat;
                if (turn) scale = 1.18f + .07f * beat;
                Color fill = slotFills[i].color;
                slotFills[i].color = new Color(fill.r, fill.g, fill.b, later ? .4f : 1f);
                Image glowImage = slotGlows[i];
                if (glowImage != null)
                {
                    glowImage.gameObject.SetActive(turn);
                    if (turn)
                    {
                        Color tint = LabPaintPalette.SrgbColorFor(slotElements[i]);
                        glowImage.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(.45f, .95f, beat));
                        glowImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(.9f, 1.2f, beat);
                    }
                }
                if (turn) TurnSlot = i;
                if ((!figmaPanel || preview) && i == popSlot && popElapsed >= 0f && popElapsed < .25f)
                    scale += .3f * Mathf.Sin(popElapsed / .25f * Mathf.PI);
                slots[i].localScale = Vector3.one * scale;
                slots[i].anchoredPosition += new Vector2(WrongShake(i, slots[i].sizeDelta.x), 0f); // #719 X
                // 야광귀 셈법 (#271): 맞힌 칸 위에 그 칸의 수가 톡 튀어나와 남는다.
                Text word = slotWords[i];
                word.gameObject.SetActive(yaguang && hit && !figmaPanel);
                if (!(yaguang && hit) || figmaPanel) continue;
                word.text = CountWord(i + 1);
                float rise = 0f, wordScale = 1f;
                if (i == popSlot && popElapsed >= 0f && popElapsed < .35f)
                {
                    float u = popElapsed / .35f;
                    wordScale = Mathf.Lerp(1.7f, 1f, 1f - (1f - u) * (1f - u));
                    rise = (1f - u) * 10f * unit;
                }
                word.rectTransform.localScale = Vector3.one * wordScale / Mathf.Max(.01f, scale);
                word.rectTransform.anchoredPosition = new Vector2(0f, 2f * unit + rise);
            }

            if (snapshot.comboActive)
            {
                // 남은 시간은 숫자 없이 막대로만 보여준다. 3초 이하면 막대가 빨개진다.
                double left = System.Math.Max(0d, snapshot.comboEndsAt - serverNow);
                double limit = snapshot.comboEndsAt - snapshot.comboStartedAt;
                timeBar.anchorMax = new Vector2(limit > 0d ? Mathf.Clamp01((float)(left / limit)) : 0f, 1f);
                timeBarFill.color = figmaPanel ? Color.white : left <= 3d ? FailColor : (Color)InkColor;
                timeBar.parent.gameObject.SetActive(true);
            }
            else if (preview)
            {
                // #719: during the warning the bar sits full (not draining yet) so the panel's bottom isn't empty.
                timeBar.anchorMax = Vector2.one;
                timeBarFill.color = figmaPanel ? Color.white : (Color)InkColor;
                timeBar.parent.gameObject.SetActive(true);
            }
            else timeBar.parent.gameObject.SetActive(false);
        }

        private void ApplyPanelArtwork(bool authored)
        {
            Vector2 viewport = ((RectTransform)canvas.transform).rect.size;
            float fit = LabYaguangComboArt.Fit(viewport);
            Vector2 authoredSize = LabYaguangComboArt.PanelReference.size * fit;
            Vector2 authoredAnchor = LabYaguangComboArt.PanelAnchor(viewport);
            float gap = slotSize * .22f;
            Vector2 anchor = authored ? authoredAnchor : panelAnchor;
            panel.anchorMin = panel.anchorMax = anchor;
            resultText.rectTransform.anchorMin = resultText.rectTransform.anchorMax = anchor;
            comboPop.anchorMin = comboPop.anchorMax = anchor;
            warningPanel.anchorMin = warningPanel.anchorMax = anchor;
            Image back = panel.GetComponent<Image>();
            back.sprite = authored ? LabYaguangComboArt.Load("Panel") : RoundedSprite();
            back.type = authored ? Image.Type.Simple : Image.Type.Sliced;
            back.color = authored ? Color.white : PanelColor;
            panel.sizeDelta = authored ? authoredSize
                : new Vector2(slotSize * LabGourdCombo.Length + gap * (LabGourdCombo.Length + 1), slotSize + gap * 3f + 10f * unit);
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].sizeDelta = authored ? new Vector2(38f, 38.79167f) * fit : Vector2.one * slotSize;
                slots[i].anchoredPosition = authored
                    ? new Vector2(38f + i * 51f, -33f) * fit
                    : new Vector2(gap + slotSize * .5f + i * (slotSize + gap), -gap - slotSize * .5f);
                slotNumbers[i].fontSize = Mathf.Max(1, Mathf.RoundToInt(29f * fit));
                slotNumbers[i].GetComponent<Outline>().effectDistance = new Vector2(1.4f, -1.4f) * fit;
                var check = slotChecks[i].rectTransform;
                slotChecks[i].sprite = authored ? LabYaguangComboArt.Load("Check") : CheckSprite();
                if (authored)
                {
                    check.anchorMin = check.anchorMax = new Vector2(.5f, .5f);
                    check.sizeDelta = Vector2.one * (38f * fit);
                    check.anchoredPosition = new Vector2(0f, -.27368f * fit);
                }
                else
                {
                    check.anchorMin = Vector2.zero; check.anchorMax = Vector2.one;
                    check.offsetMin = check.offsetMax = Vector2.zero;
                }
            }
            var track = (RectTransform)timeBar.parent;
            track.offsetMin = authored ? new Vector2(19f, 19f) * fit : new Vector2(gap, gap);
            track.offsetMax = authored ? new Vector2(-19f, 24f) * fit : new Vector2(-gap, gap + 10f * unit);
            var trackImage = track.GetComponent<Image>();
            trackImage.sprite = authored ? LabYaguangComboArt.Load("BarFill") : null;
            trackImage.color = authored ? new Color(1, 1, 1, .15f) : new Color(0, 0, 0, .15f);
            timeBarFill.sprite = authored ? LabYaguangComboArt.Load("BarFill") : null;
        }

        private void DrawResult()
        {
            DrawComboPop();
            if (successStamp != null) successStamp.gameObject.SetActive(false);
            resultText.enabled = true;
            if (resultElapsed < 0f)
            {
                resultText.gameObject.SetActive(false);
                flash.gameObject.SetActive(false);
                return;
            }
            if (resultDelay > 0f)
            {
                // 4 COMBO!!가 떠 있는 동안 CLEAR!는 기다린다. 패널·배지는 결과 중이라 숨어 있다.
                resultDelay -= Time.unscaledDeltaTime;
                resultText.gameObject.SetActive(false);
                flash.gameObject.SetActive(false);
                return;
            }
            resultElapsed += Time.unscaledDeltaTime;
            if (resultElapsed >= ResultSeconds)
            {
                resultElapsed = -1f;
                resultText.gameObject.SetActive(false);
                flash.gameObject.SetActive(false);
                return;
            }
            resultText.gameObject.SetActive(true);
            bool clear = shownResult == LabComboResult.Success;
            resultText.color = clear ? ClearColor : FailColor;
            float t = resultElapsed;
            float leave = Mathf.Clamp01((t - (ResultSeconds - .2f)) / .2f);
            if (clear && StampSuccess)
            {
                // PNG lands once, rebounds briefly, holds, then dissolves; no duplicate success text.
                resultText.enabled = false;
                resultText.rectTransform.localScale = Vector3.one;
                resultText.rectTransform.localRotation = Quaternion.identity;
                resultText.rectTransform.anchoredPosition = Vector2.zero;
                successStamp.gameObject.SetActive(true);
                var rect = successStamp.rectTransform;
                float diameter = Mathf.Min(layer.rect.width * .54f, layer.rect.height * .28f);
                rect.sizeDelta = Vector2.one * diameter;
                float approach = Mathf.Clamp01(t / StampLandSeconds);
                float rebound = Mathf.Clamp01((t - StampLandSeconds) / .22f);
                float scale = t < StampLandSeconds ? Mathf.Lerp(2.4f, .92f, approach * approach)
                    : 1f - .08f * Mathf.Cos(rebound * Mathf.PI * 3f) * (1f - rebound);
                rect.localScale = Vector3.one * scale;
                float kick = t >= StampLandSeconds ? Mathf.Exp(-(t - StampLandSeconds) * 22f) : 0f;
                // Position relative to the full canvas, below the order panel and over the gourd.
                rect.anchoredPosition = new Vector2(Mathf.Sin(t * 95f) * diameter * .018f * kick,
                    layer.rect.height * (.65f - resultText.rectTransform.anchorMin.y)
                    + diameter * .3f * (1f - approach));
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-14f, -7f, approach));
                successStamp.color = new Color(1, 1, 1, Mathf.Clamp01(t / .045f) * (1f - leave));
                flash.gameObject.SetActive(false);
                // #695: the moment it is pressed into the gourd (smallest, before the rebound), once per result. The sound follows this.
                if (t >= StampLandSeconds && stampLandedFor != shownResultCount)
                {
                    stampLandedFor = shownResultCount;
                    StampsLanded++;
                }
                return;
            }
            if (clear)
            {
                float pop = t < .18f ? 1.3f * (1f - Mathf.Pow(1f - t / .18f, 3f)) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((t - .18f) / .15f));
                resultText.rectTransform.localScale = Vector3.one * pop * (1f - leave);
                resultText.rectTransform.localRotation = Quaternion.identity;
                resultText.rectTransform.anchoredPosition = Vector2.zero;
                flash.gameObject.SetActive(false);
                return;
            }
            // 실패: 크게 쾅 튀어나온 뒤(1.8배 → 1배), 처음 0.7초 동안 세게 흔들리고 기울며,
            // 시작 순간 화면 전체가 빨갛게 두 번 번쩍인다.
            float slam = t < .12f ? 1.8f * (1f - Mathf.Pow(1f - t / .12f, 3f))
                : Mathf.Lerp(1.8f, 1f, Mathf.Clamp01((t - .12f) / .2f));
            float thump = 1f + .12f * Mathf.Max(0f, Mathf.Sin((t - .45f) * Mathf.PI / .2f)) * (t > .45f && t < .65f ? 1f : 0f);
            float shake = Mathf.Clamp01(1f - t / .7f);
            shake *= shake;
            resultText.rectTransform.localScale = Vector3.one * slam * thump * (1f - leave);
            resultText.rectTransform.anchoredPosition = new Vector2(
                Mathf.Sin(t * 95f) * 16f * unit * shake, Mathf.Cos(t * 71f) * 8f * unit * shake);
            resultText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f * Mathf.Sin(t * 47f) * shake);
            float blink = t < .12f ? 1f - t / .12f : t < .3f ? .6f * Mathf.Clamp01(1f - (t - .18f) / .12f) * (t > .18f ? 1f : 0f) : 0f;
            flash.gameObject.SetActive(blink > 0f);
            Color flashColor = FailColor;
            flashColor.a = .38f * blink;
            flash.color = flashColor;
        }

        private void ResetRound()
        {
            shownResultCount = 0;
            shownFoxRoundCount = 0;
            resultElapsed = -1f;
            lastProgress = 0;
            lastComboCount = 0;
            popElapsed = -1f;
            popSlot = -1;
            previewRevealed = 0;
            wrongSeenInit = false;
            wrongElapsed = -1f;
            System.Array.Clear(wasChecked, 0, wasChecked.Length);
            effectKind = 0;
            effectPopElapsed = -1f;
            starsWereOn = false;
            if (stars != null) stars.Clear();
            shownWarningCount = -1;
            warningPopElapsed = -1f;
            resultDelay = 0f;
            comboPopElapsed = -1f;
            comboPopCombo = 0;
            if (comboPop != null) comboPop.gameObject.SetActive(false);
        }

        /// <summary>틀린 색 실패면 누가 틀렸는지 기억한다 (#301). Host의 ID 0도 유효하므로 좌석 목록에서 찾는다.</summary>
        private void RememberFailer(LabSnapshot snapshot)
        {
            shownFailedKnown = false;
            shownFailedSeat = -1;
            shownFailedBy = snapshot.comboFailedBy;
            if (snapshot.comboResult != LabComboResult.WrongColor || snapshot.players == null) return;
            foreach (LabPlayerState player in snapshot.players)
                if (player != null && player.id == snapshot.comboFailedBy)
                {
                    shownFailedKnown = true;
                    shownFailedSeat = player.seat;
                    return;
                }
        }

        /// <summary>N COMBO!를 띄운다 (#301). 같은 콤보에서 더 작은 수로 돌아가지 않는다.</summary>
        private void ShowComboPop(int count, bool final, uint comboCount)
        {
            if (comboPop == null) return;
            if (comboCount == comboPopCombo && count < comboPopCount && comboPopElapsed >= 0f) return;
            comboPopCombo = comboCount;
            comboPopCount = count;
            comboPopFinal = final;
            comboPopElapsed = 0f;
            comboPopNumber.text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            comboPopFit = ComboPopFit();
            comboPopWord.text = final ? ComboClearText : ComboHitText;
        }

        /// <summary>
        /// N COMBO!: 1.7배로 쾅 나왔다가 제자리, 살짝 기울어 들썩이다 마지막에 사라진다. 칸을 맞혔을 때는 패널 아래,
        /// 다 맞혔을 때는 사라진 패널 자리 가운데에 1.5배 크기로.
        /// </summary>
        private void DrawComboPop()
        {
            if (comboPop == null) return;
            if (comboPopElapsed < 0f) { comboPop.gameObject.SetActive(false); return; }
            comboPopElapsed += Time.unscaledDeltaTime;
            float life = comboPopFinal ? comboClearSeconds : comboHitSeconds;
            if (comboPopElapsed >= life) { comboPopElapsed = -1f; comboPop.gameObject.SetActive(false); return; }
            comboPop.gameObject.SetActive(true);
            comboPop.SetAsLastSibling();
            float t = comboPopElapsed;
            float pop = t < .14f ? Mathf.Lerp(.3f, 1.7f, t / .14f) : Mathf.Lerp(1.7f, 1f, Mathf.Clamp01((t - .14f) / .16f));
            float leave = Mathf.Clamp01((t - (life - .2f)) / .2f);
            float size = (comboPopFinal ? 1.5f : 1f) * comboPopFit;
            comboPop.localScale = Vector3.one * pop * size * (1f - leave);
            comboPop.localRotation = Quaternion.Euler(0f, 0f, -6f + 3f * Mathf.Sin(t * 14f));
            comboPop.anchoredPosition = comboPopFinal ? Vector2.zero
                : new Vector2(0f, -panel.sizeDelta.y * 1.05f + (1f - Mathf.Clamp01(t / .3f)) * 8f * unit);
            comboPopNumber.color = comboNumberColor;
            comboPopWord.color = comboWordColor;
        }

        private bool EnsureLayer()
        {
            if (layer != null) return true;
            if (canvas == null)
            {
                Canvas found = GetComponentInParent<Canvas>();
                canvas = found != null ? found.rootCanvas : null;
            }
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null) return false;
            // #411: 피버타임 배너와 같은 신라문화체 굵은 글씨 (없으면 화면의 첫 글꼴, 그것도 없으면 기본 글꼴).
            font = LabYaguangFeverHud.ShillaFont(canvas);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            layer = NewRect("Gourd Combo", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;

            // 모든 크기를 칸 크기에 비례시킨다 (처음 만든 64px 칸 기준 값 × u).
            float u = slotSize / 64f;
            unit = u;

            // 실패할 때 화면 전체가 빨갛게 번쩍이는 막. 맨 뒤에 두고 터치를 막지 않는다.
            RectTransform flashRect = NewRect("Combo Fail Flash", layer);
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            flash = flashRect.gameObject.AddComponent<Image>();
            flash.color = Color.clear;
            flash.raycastTarget = false;
            flashRect.gameObject.SetActive(false);

            // #414 예고 그늘: 박 쪽(ShadeCenterY)이 밝은 둥근 그늘. 화면을 다 덮게 크게 둔다.
            warningShade = NewShade("Combo Warning Shade", layer);

            // 보상 별(#257): 번쩍임 바로 앞, 콤보 패널·결과 문구·배지 뒤에 그린다.
            stars = layer.gameObject.AddComponent<LabComboRewardStars>();
            stars.Initialize(layer, u);
            float gap = slotSize * .22f;
            float width = slotSize * LabGourdCombo.Length + gap * (LabGourdCombo.Length + 1);
            panel = NewRect("Gourd Combo Panel", layer);
            panel.anchorMin = panel.anchorMax = panelAnchor;
            panel.sizeDelta = new Vector2(width, slotSize + gap * 3f + 10f * u);
            Image back = panel.gameObject.AddComponent<Image>();
            back.sprite = RoundedSprite();
            back.type = Image.Type.Sliced;
            back.color = PanelColor;
            back.raycastTarget = false;
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f; panelGroup.blocksRaycasts = false; panelGroup.interactable = false;

            for (int i = 0; i < LabGourdCombo.Length; i++)
            {
                // #719: a glow behind the slot whose turn it is (a sibling placed first, so it sits under the slot).
                RectTransform glowRect = NewRect("Combo Turn Glow " + (i + 1), panel);
                glowRect.anchorMin = glowRect.anchorMax = new Vector2(0f, 1f);
                glowRect.pivot = new Vector2(.5f, .5f);
                glowRect.sizeDelta = new Vector2(slotSize * 2f, slotSize * 2f);
                glowRect.anchoredPosition = new Vector2(gap + slotSize * .5f + i * (slotSize + gap), -gap - slotSize * .5f);
                Image glow = glowRect.gameObject.AddComponent<Image>();
                glow.sprite = TurnGlowSprite();
                glow.raycastTarget = false;
                glowRect.gameObject.SetActive(false);
                slotGlows[i] = glow;
                RectTransform slot = NewRect("Combo Slot " + (i + 1), panel);
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                slot.pivot = new Vector2(.5f, .5f);
                slot.sizeDelta = new Vector2(slotSize, slotSize);
                slot.anchoredPosition = new Vector2(gap + slotSize * .5f + i * (slotSize + gap), -gap - slotSize * .5f);
                Image fill = slot.gameObject.AddComponent<Image>();
                fill.sprite = DiscSprite();
                fill.raycastTarget = false;
                RectTransform check = NewRect("Check", slot);
                check.anchorMin = Vector2.zero;
                check.anchorMax = Vector2.one;
                check.offsetMin = check.offsetMax = Vector2.zero;
                Image mark = check.gameObject.AddComponent<Image>();
                mark.sprite = CheckSprite();
                mark.raycastTarget = false;
                check.gameObject.SetActive(false);
                slots[i] = slot;
                slotFills[i] = fill;
                slotChecks[i] = mark;

                // 야광귀 셈법 (#271): 칸 바로 위에 뜨는 수 (하나·둘·셋·넷)
                Text word = NewText("Combo Slot Word " + (i + 1), slot, Mathf.Max(10, Mathf.RoundToInt(18f * u)), TextAnchor.LowerCenter);
                word.rectTransform.anchorMin = word.rectTransform.anchorMax = new Vector2(.5f, 1f);
                word.rectTransform.pivot = new Vector2(.5f, 0f);
                word.rectTransform.sizeDelta = new Vector2(slotSize * 1.4f, 22f * u);
                Outline wordOutline = word.gameObject.AddComponent<Outline>();
                wordOutline.effectColor = new Color(1f, 1f, 1f, .9f);
                wordOutline.effectDistance = new Vector2(1.5f, -1.5f);
                word.gameObject.SetActive(false);
                slotWords[i] = word;
                Text number = NewText("Order Number", slot, 29, TextAnchor.MiddleCenter);
                number.text = (i + 1).ToString();
                number.color = PanelColor;
                number.rectTransform.anchorMin = Vector2.zero;
                number.rectTransform.anchorMax = Vector2.one;
                number.rectTransform.offsetMin = number.rectTransform.offsetMax = Vector2.zero;
                Outline orderOutline = number.gameObject.AddComponent<Outline>();
                orderOutline.effectColor = InkColor;
                orderOutline.effectDistance = new Vector2(1.4f, -1.4f);
                number.gameObject.SetActive(false);
                slotNumbers[i] = number;
            }

            // 야광귀 셈법 (#271): 넷 다음 칸. 야광귀가 셀 수 없어 패널 밖에 흐리게 매달린 ?.
            questionSlot = NewRect("Combo Slot ?", panel);
            questionSlot.anchorMin = questionSlot.anchorMax = new Vector2(1f, 1f);
            questionSlot.pivot = new Vector2(.5f, .5f);
            questionSlot.sizeDelta = new Vector2(slotSize * .72f, slotSize * .72f);
            questionSlot.anchoredPosition = new Vector2(gap * .4f + slotSize * .36f, -gap - slotSize * .5f);
            Image questionDisc = questionSlot.gameObject.AddComponent<Image>();
            questionDisc.sprite = DiscSprite();
            questionDisc.color = new Color(.6f, .58f, .55f, .38f);
            questionDisc.raycastTarget = false;
            Text questionMark = NewText("?", questionSlot, Mathf.Max(10, Mathf.RoundToInt(26f * u)), TextAnchor.MiddleCenter);
            questionMark.rectTransform.anchorMin = Vector2.zero;
            questionMark.rectTransform.anchorMax = Vector2.one;
            questionMark.rectTransform.offsetMin = questionMark.rectTransform.offsetMax = Vector2.zero;
            questionMark.text = "?";
            questionMark.color = new Color(.17f, .14f, .12f, .55f);
            questionSlot.gameObject.SetActive(false);


            RectTransform barBack = NewRect("Combo Time Bar", panel);
            barBack.anchorMin = new Vector2(0f, 0f);
            barBack.anchorMax = new Vector2(1f, 0f);
            barBack.pivot = new Vector2(.5f, 0f);
            barBack.offsetMin = new Vector2(gap, gap);
            barBack.offsetMax = new Vector2(-gap, gap + 10f * u);
            Image barBackImage = barBack.gameObject.AddComponent<Image>();
            barBackImage.color = new Color(0f, 0f, 0f, .15f);
            barBackImage.raycastTarget = false;
            timeBar = NewRect("Fill", barBack);
            timeBar.anchorMin = Vector2.zero;
            timeBar.anchorMax = Vector2.one;
            timeBar.offsetMin = timeBar.offsetMax = Vector2.zero;
            timeBarFill = timeBar.gameObject.AddComponent<Image>();
            timeBarFill.color = InkColor;
            timeBarFill.raycastTarget = false;

            resultText = NewText("Combo Result", layer, Mathf.Max(14, Mathf.RoundToInt(54f * u)), TextAnchor.MiddleCenter);
            resultText.rectTransform.anchorMin = resultText.rectTransform.anchorMax = panelAnchor;
            resultText.rectTransform.sizeDelta = new Vector2(width * 1.6f, 80f * u);
            float badgeHeight = 34f * u;
            // 결과 문구는 사라진 콤보 패널 자리 가운데에 뜬다.
            resultText.rectTransform.anchoredPosition = Vector2.zero;
            Outline resultOutline = resultText.gameObject.AddComponent<Outline>();
            resultOutline.effectColor = InkColor;
            resultOutline.effectDistance = new Vector2(3f, -3f) * Mathf.Max(.5f, u);
            resultText.gameObject.SetActive(false);
            var stampRect = NewRect("Success Stamp", resultText.rectTransform);
            stampRect.anchorMin = stampRect.anchorMax = new Vector2(.5f, .5f);
            successStamp = stampRect.gameObject.AddComponent<Image>();
            successStamp.sprite = LabYaguangComboArt.Load("SuccessStamp");
            successStamp.preserveAspect = true;
            successStamp.raycastTarget = false;
            stampRect.gameObject.SetActive(false);

            // 콤보 예고 (#241): 패널과 같은 자리·크기에 COMBO! (왼쪽)와 남은 초 (오른쪽).
            warningPanel = NewRect("Combo Warning", layer);
            warningPanel.anchorMin = warningPanel.anchorMax = panelAnchor;
            warningPanel.sizeDelta = panel.sizeDelta;
            Image warningBack = warningPanel.gameObject.AddComponent<Image>();
            warningBack.sprite = RoundedSprite();
            warningBack.type = Image.Type.Sliced;
            warningBack.color = PanelColor;
            warningBack.raycastTarget = false;
            warningLabel = NewText("Warning Label", warningPanel, Mathf.Max(12, Mathf.RoundToInt(44f * u)), TextAnchor.MiddleCenter);
            warningLabel.rectTransform.anchorMin = Vector2.zero;
            warningLabel.rectTransform.anchorMax = new Vector2(.68f, 1f);
            warningLabel.rectTransform.offsetMin = warningLabel.rectTransform.offsetMax = Vector2.zero;
            warningLabel.text = WarningText;
            warningLabel.color = ClearColor;
            Outline warningOutline = warningLabel.gameObject.AddComponent<Outline>();
            warningOutline.effectColor = InkColor;
            warningOutline.effectDistance = new Vector2(2f, -2f) * Mathf.Max(.5f, u);
            warningNumber = NewText("Warning Count", warningPanel, Mathf.Max(14, Mathf.RoundToInt(64f * u)), TextAnchor.MiddleCenter);
            warningNumber.rectTransform.anchorMin = new Vector2(.68f, 0f);
            warningNumber.rectTransform.anchorMax = Vector2.one;
            warningNumber.rectTransform.offsetMin = warningNumber.rectTransform.offsetMax = Vector2.zero;
            warningPanel.gameObject.SetActive(false);

            // 페널티 배지: 패널 아래 가장자리에 반쯤 걸친 알약 모양. 패널이 숨어도 같은 자리에 남는다.
            // 왼쪽은 공 아이콘 + SMALL, 오른쪽은 남은 시간 bar (#257: 숫자 대신 bar라 글자와 겹치지 않게 나눈다).
            badgeHeight = 40f * u;
            // SMALL (#301): 전투 화면(공격 영역) 왼쪽 아래 구석에, "SMALL" 글자 밑에 세운 세로 사각형 하나.
            // 먹색 테두리 안의 빨간 칸이 남은 시간만큼 아래부터 차 있고 위에서 아래로 줄어든다. 위치는 DrawEffect가 정한다.
            float barWidth = badgeHeight * penaltyBarSize.x, barHeight = badgeHeight * penaltyBarSize.y;
            float labelHeight = badgeHeight * .7f, labelGap = badgeHeight * penaltyBarGap;
            effectBadge = NewRect("Combo Effect Badge", layer);
            effectBadge.anchorMin = effectBadge.anchorMax = new Vector2(.5f, .5f);
            effectBadge.pivot = Vector2.zero;
            effectBadge.sizeDelta = new Vector2(Mathf.Max(barWidth, badgeHeight * 2.4f), barHeight + labelGap + labelHeight);
            effectBack = effectBadge.gameObject.AddComponent<Image>();
            effectBack.color = Color.clear;
            effectBack.raycastTarget = false;

            RectTransform gaugeArea = NewRect("Gauge Track", effectBadge);
            gaugeArea.anchorMin = gaugeArea.anchorMax = Vector2.zero;
            gaugeArea.pivot = Vector2.zero;
            gaugeArea.sizeDelta = new Vector2(barWidth, barHeight);
            gaugeArea.anchoredPosition = Vector2.zero;
            Image track = gaugeArea.gameObject.AddComponent<Image>();
            track.color = InkColor;
            track.raycastTarget = false;
            float border = Mathf.Max(2f, 3f * u);
            RectTransform gaugeInner = NewRect("Gauge Inside", gaugeArea);
            gaugeInner.anchorMin = Vector2.zero;
            gaugeInner.anchorMax = Vector2.one;
            gaugeInner.offsetMin = new Vector2(border, border);
            gaugeInner.offsetMax = new Vector2(-border, -border);
            Image inside = gaugeInner.gameObject.AddComponent<Image>();
            inside.color = PanelColor;
            inside.raycastTarget = false;
            effectGauge = NewRect("Gauge", gaugeInner);
            effectGauge.anchorMin = Vector2.zero;
            effectGauge.anchorMax = Vector2.one;
            effectGauge.offsetMin = effectGauge.offsetMax = Vector2.zero;
            Image gauge = effectGauge.gameObject.AddComponent<Image>();
            gauge.color = PenaltyBadgeColor;
            gauge.raycastTarget = false;
            effectGaugeFill = gauge;

            // 공 아이콘은 더 쓰지 않는다 (글자와 사각형만).
            effectIcon = NewRect("Ball Icon", effectBadge);
            effectIcon.gameObject.SetActive(false);

            // 사각형 바로 위 가운데에 SMALL 글자만.
            effectText = NewText("Effect Label", effectBadge, Mathf.Max(10, Mathf.RoundToInt(20f * u)), TextAnchor.LowerCenter);
            effectText.rectTransform.anchorMin = effectText.rectTransform.anchorMax = Vector2.zero;
            effectText.rectTransform.pivot = new Vector2(.5f, 0f);
            effectText.rectTransform.sizeDelta = new Vector2(badgeHeight * 2.4f, labelHeight);
            effectText.rectTransform.anchoredPosition = new Vector2(barWidth * .5f, barHeight + labelGap);
            effectText.color = PenaltyBadgeColor;
            Outline effectOutline = effectText.gameObject.AddComponent<Outline>();
            effectOutline.effectColor = Color.white;
            effectOutline.effectDistance = new Vector2(2f, -2f) * Mathf.Max(.5f, u);
            effectBadge.gameObject.SetActive(false);

            // N COMBO! (#301): 주황 숫자 + 두꺼운 흰 테두리 + 진한 그림자, 오른쪽 아래에 분홍 COMBO!.
            comboPop = NewRect("Combo Count Pop", layer);
            comboPop.anchorMin = comboPop.anchorMax = panelAnchor;
            float numberSize = slotSize * comboNumberSize;
            comboPop.sizeDelta = new Vector2(numberSize * 2.4f, numberSize * 1.3f);
            comboPopNumber = NewText("Combo Count", comboPop, Mathf.Max(14, Mathf.RoundToInt(numberSize)), TextAnchor.MiddleCenter);
            comboPopNumber.rectTransform.anchorMin = comboPopNumber.rectTransform.anchorMax = new Vector2(.5f, .6f);
            comboPopNumber.rectTransform.sizeDelta = new Vector2(numberSize * 2.4f, numberSize * 1.2f);
            Shadow numberShadow = comboPopNumber.gameObject.AddComponent<Shadow>();
            numberShadow.effectColor = new Color32(120, 30, 10, 200);
            numberShadow.effectDistance = new Vector2(3f, -4f) * Mathf.Max(.5f, u);
            Outline numberOutline = comboPopNumber.gameObject.AddComponent<Outline>();
            numberOutline.effectColor = Color.white;
            numberOutline.effectDistance = new Vector2(4f, -4f) * Mathf.Max(.5f, u);
            // #414: 한글 "콤보!"가 숫자보다 커 보여서 영어 때(.36)보다 줄였다.
            comboPopWord = NewText("Combo Word", comboPop, Mathf.Max(10, Mathf.RoundToInt(numberSize * .26f)), TextAnchor.MiddleCenter);
            comboPopWord.rectTransform.anchorMin = comboPopWord.rectTransform.anchorMax = new Vector2(.62f, .12f);
            comboPopWord.rectTransform.sizeDelta = new Vector2(numberSize * 2f, numberSize * .5f);
            Outline wordOutlineWhite = comboPopWord.gameObject.AddComponent<Outline>();
            wordOutlineWhite.effectColor = Color.white;
            wordOutlineWhite.effectDistance = new Vector2(2.5f, -2.5f) * Mathf.Max(.5f, u);
            comboPop.gameObject.SetActive(false);

            panel.gameObject.SetActive(false);
            return true;
        }

        private Text NewText(string name, Transform parent, int size, TextAnchor alignment)
        {
            Text text = NewRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = InkColor;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        // ---- 손그림 느낌의 단순 도형: 진한 테두리 원, 흰 체크, 둥근 패널 ----

        /// <summary>#414 화면을 다 덮는 둥근 그늘 (가운데는 투명, 가장자리로 갈수록 진함). 터치를 막지 않는다.</summary>
        private static Image NewShade(string name, RectTransform parent)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = new Vector2(-.25f, ShadeCenterY - .8f);
            rect.anchorMax = new Vector2(1.25f, ShadeCenterY + .8f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = VignetteSprite();
            image.color = Color.clear;
            image.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return image;
        }

        private static Sprite VignetteSprite()
        {
            if (vignetteSprite != null) return vignetteSprite;
            vignetteSprite = Draw("Lab combo vignette", (p, c) =>
            {
                float r = Vector2.Distance(p, c) / (TextureSize * .5f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, .9f, r));
                return new Color(1f, 1f, 1f, a);
            }, Vector4.zero);
            return vignetteSprite;
        }

        /// <summary>#719: a soft round glow (white, tinted by the slot's colour).</summary>
        private static Sprite TurnGlowSprite()
        {
            if (turnGlowSprite != null) return turnGlowSprite;
            turnGlowSprite = Draw("Lab combo turn glow", (p, c) =>
            {
                float r = Vector2.Distance(p, c) / (TextureSize * .5f);
                float a = Mathf.Clamp01(1f - r);
                return new Color(1f, 1f, 1f, a * a);
            }, Vector4.zero);
            return turnGlowSprite;
        }

        private static Sprite DiscSprite()
        {
            if (discSprite != null) return discSprite;
            discSprite = Draw("Lab combo disc", (p, c) =>
            {
                float distance = Vector2.Distance(p, c) - (TextureSize * .5f - 2f);
                float alpha = Mathf.Clamp01(.5f - distance);
                float ink = Mathf.Clamp01(distance + 6f + .5f);
                Color color = Color.Lerp(Color.white, (Color)InkColor, ink);
                color.a = alpha;
                return color;
            }, Vector4.zero);
            return discSprite;
        }

        private static Sprite CheckSprite()
        {
            if (checkSprite != null) return checkSprite;
            float s = TextureSize;
            Vector2 a = new Vector2(s * .26f, s * .52f), b = new Vector2(s * .43f, s * .33f), d = new Vector2(s * .76f, s * .7f);
            checkSprite = Draw("Lab combo check", (p, c) =>
            {
                float distance = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, d));
                float core = Mathf.Clamp01(s * .07f + .5f - distance);
                float edge = Mathf.Clamp01(s * .07f + 4f + .5f - distance);
                Color color = Color.Lerp((Color)InkColor, Color.white, core);
                color.a = edge;
                return color;
            }, Vector4.zero);
            return checkSprite;
        }

        /// <summary>양 끝이 완전히 둥근 알약 모양 (진한 테두리). 9-slice로 늘린다.</summary>
        private static Sprite PillSprite()
        {
            if (pillSprite != null) return pillSprite;
            float radius = TextureSize * .5f;
            pillSprite = Draw("Lab combo pill", (p, c) =>
            {
                float distance = Vector2.Distance(p, c) - (radius - 1f);
                float alpha = Mathf.Clamp01(.5f - distance);
                float ink = Mathf.Clamp01(distance + 4f + .5f);
                Color color = Color.Lerp(Color.white, (Color)InkColor, ink);
                color.a = alpha;
                return color;
            }, new Vector4(radius - 1f, radius - 1f, radius - 1f, radius - 1f));
            return pillSprite;
        }

        internal static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            float radius = TextureSize * .3f;
            roundedSprite = Draw("Lab combo panel", (p, c) =>
            {
                Vector2 q = new Vector2(Mathf.Max(Mathf.Abs(p.x - c.x) - (c.x - radius), 0f),
                    Mathf.Max(Mathf.Abs(p.y - c.y) - (c.y - radius), 0f));
                float distance = q.magnitude - radius + 1f;
                float alpha = Mathf.Clamp01(.5f - distance);
                float ink = Mathf.Clamp01(distance + 4f + .5f);
                Color color = Color.Lerp(Color.white, (Color)InkColor, ink);
                color.a = alpha;
                return color;
            }, new Vector4(radius, radius, radius, radius));
            return roundedSprite;
        }

        private static Sprite Draw(string name, System.Func<Vector2, Vector2, Color> shade, Vector4 border)
        {
            var pixels = new Color[TextureSize * TextureSize];
            Vector2 center = new Vector2(TextureSize * .5f, TextureSize * .5f);
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                    pixels[y * TextureSize + x] = shade(new Vector2(x + .5f, y + .5f), center);
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private bool FindGauge()
        {
            if (gaugeFrame != null && gaugeContainer != null) return true;
            if (canvas == null) return false;
            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "Paint Progress" && rect.Find("Frame") is RectTransform frame)
                { gaugeContainer = rect; gaugeFrame = frame; return true; }
            return false;
        }
        private void CoverGauge(bool cover)
        {
            CoverHeading(cover);
            if (cover)
            {
                if (!FindGauge()) return;
                if (gaugeGroup == null)
                {
                    gaugeGroup = gaugeContainer.GetComponent<CanvasGroup>();
                    if (gaugeGroup == null)
                    {
                        gaugeGroup = ownedGaugeGroup = gaugeContainer.gameObject.AddComponent<CanvasGroup>();
                        gaugeGroup.blocksRaycasts = false; gaugeGroup.interactable = false;
                    }
                }
                if (!gaugeCovered) gaugeAlphaBeforeCover = gaugeGroup.alpha;
                gaugeCovered = true; gaugeGroup.alpha = 0f;
            }
            else
            {
                if (gaugeCovered && gaugeGroup != null) gaugeGroup.alpha = gaugeAlphaBeforeCover;
                gaugeCovered = false;
            }
        }
        // The stage caption and its brush underline share the gauge's overlay lifetime.
        private void CoverHeading(bool cover)
        {
            if (cover && canvas != null)
            {
                string[] names = { "StageLabel", "Stage Underline" };
                for (int i = 0; i < names.Length; i++)
                {
                    if (headingGroups[i] == null)
                        foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                            if (rect.name == names[i])
                            {
                                var group = rect.GetComponent<CanvasGroup>();
                                if (group == null)
                                {
                                    group = rect.gameObject.AddComponent<CanvasGroup>();
                                    group.blocksRaycasts = false; group.interactable = false;
                                    ownedHeadingGroups[i] = true;
                                }
                                headingGroups[i] = group;
                                headingAlphas[i] = group.alpha;
                                break;
                            }
                    if (headingGroups[i] == null) continue;
                    if (!headingCovered) headingAlphas[i] = headingGroups[i].alpha;
                    headingGroups[i].alpha = 0f;
                }
                headingCovered = true;
            }
            else
            {
                if (headingCovered)
                    for (int i = 0; i < headingGroups.Length; i++)
                        if (headingGroups[i] != null) headingGroups[i].alpha = headingAlphas[i];
                headingCovered = false;
            }
        }
        private void ReleaseGauge()
        {
            CoverGauge(false);
            for (int i = 0; i < headingGroups.Length; i++)
            {
                if (ownedHeadingGroups[i] && headingGroups[i] != null)
                {
                    if (Application.isPlaying) Destroy(headingGroups[i]); else DestroyImmediate(headingGroups[i]);
                }
                headingGroups[i] = null; ownedHeadingGroups[i] = false;
            }
            if (ownedGaugeGroup != null)
            {
                if (Application.isPlaying) Destroy(ownedGaugeGroup); else DestroyImmediate(ownedGaugeGroup);
            }
            gaugeGroup = ownedGaugeGroup = null; gaugeFrame = gaugeContainer = null;
        }
        private void OnDisable()
        {
            CoverGauge(false); panelWasShown = false;
            if (panelGroup != null) panelGroup.alpha = 0f;
            if (layer != null) layer.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (network != null) network.ComboThrowHeld -= OnComboThrowHeld;
            ReleaseGauge();
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
