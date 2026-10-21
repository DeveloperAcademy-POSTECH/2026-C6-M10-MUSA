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
    /// 콤보 패널 → 결과 문구 → 배지가 한 번에 하나씩만 보인다 (겹치지 않음). 예고 중에는 배지도 잠시 숨는다.
    /// 앞 콤보의 결과 문구가 떠 있는 동안 예고가 시작되면 결과 문구가 끝난 뒤 남은 카운트다운을 보여준다.
    /// 실험이 꺼져 있으면 스냅샷에 콤보가 없으므로 아무것도 그리지 않는다. 화면 표시만 하며 터치를 막지 않는다.
    /// 일시정지 중이거나 전투가 끝나면 숨는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdComboHud : MonoBehaviour
    {
        public const string ClearText = "CLEAR!";
        public const string WrongColorText = "WRONG COLOR";
        public const string TimeUpText = "TIME UP";
        public const string PenaltyLabel = "SMALL";
        public const string RewardLabel = "BIG";
        public const string WarningText = "COMBO!";
        /// <summary>야광귀 셈법 (#271): 야광귀는 넷까지만 센다. 넷 다음은 다시 하나.</summary>
        public static readonly string[] CountWords = { "하나", "둘", "셋", "넷" };
        /// <summary>수 n(1부터)을 야광귀식으로. 넷을 넘으면 넷까지밖에 몰라 다시 하나·둘…로 돈다(예고에서는 넷 → 하나로 거꾸로 센다).</summary>
        public static string CountWord(int n) => CountWords[(Mathf.Max(1, n) - 1) % CountWords.Length];

        private const int TextureSize = 96;
        private const float ResultSeconds = 1.6f;
        private static readonly Color32 InkColor = new Color32(43, 36, 32, 255);
        private static readonly Color PanelColor = new Color(1f, .98f, .93f, .92f);
        private static readonly Color ClearColor = new Color(.98f, .78f, .16f);
        private static readonly Color FailColor = new Color(.91f, .16f, .1f);
        // 배지 배경: 결과 문구보다 살짝 차분한 빨강/노랑 (흰 글자가 잘 읽히도록)
        private static readonly Color PenaltyBadgeColor = new Color(.84f, .25f, .2f, 1f);
        private static readonly Color RewardBadgeColor = new Color(.95f, .69f, .13f, 1f);
        private static Sprite discSprite, checkSprite, roundedSprite, pillSprite;

        [Tooltip("화면에서 콤보 패널 가운데 위치 (0~1, 왼쪽 아래가 0)")]
        [SerializeField] private Vector2 panelAnchor = new Vector2(.5f, .8f);
        [Tooltip("색 칸 하나의 크기 (Canvas 기준 픽셀). 패널/글자/막대가 모두 이 크기에 비례한다.")]
        [SerializeField, Min(16f)] private float slotSize = 40f;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer, panel, timeBar;
        private readonly RectTransform[] slots = new RectTransform[LabGourdCombo.Length];
        private readonly Image[] slotFills = new Image[LabGourdCombo.Length];
        private readonly Image[] slotChecks = new Image[LabGourdCombo.Length];
        private Image timeBarFill;
        private Text resultText;
        private Image flash;
        private float unit = 1f;
        private RectTransform effectBadge, effectGauge, effectIcon;
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
        private readonly Text[] slotWords = new Text[LabGourdCombo.Length];
        private RectTransform questionSlot;
        private float warningPopElapsed = -1f;

        private LabComboRewardStars stars;
        private int boardLook;
        private bool starsWereOn;

        private LabSnapshot seenSnapshot;
        private double seenAtRealtime;
        private uint shownResultCount;
        private float resultElapsed = -1f;
        private LabComboResult shownResult;
        private int lastProgress;
        private uint lastComboCount;
        private float popElapsed = -1f;
        private int popSlot = -1;

        public bool PanelVisible => panel != null && panel.gameObject.activeSelf;
        public bool EffectVisible => effectBadge != null && effectBadge.gameObject.activeSelf;
        public string EffectLabel => effectText != null ? effectText.text : string.Empty;
        /// <summary>보상(BIG) 별이 지금 쏟아지고 있는지 (#257)</summary>
        public bool StarsEmitting => stars != null && stars.Emitting;
        public int StarCount => stars != null ? stars.ActiveCount : 0;
        public float StarsPerSecond => stars != null ? stars.StarsPerSecondNow : 0f;
        /// <summary>조합판에 알린 이 기기 플레이어의 콤보 효과: -1 SMALL, 1 BIG, 0 없음 (#257)</summary>
        public int BoardLook => boardLook;
        public bool ResultVisible => resultText != null && resultText.gameObject.activeSelf;
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
        public bool SlotChecked(int index) => slotChecks[index] != null && slotChecks[index].gameObject.activeSelf;
        public Color SlotColor(int index) => slotFills[index] != null ? slotFills[index].color : Color.clear;

        /// <summary>스냅샷을 읽을 네트워크와, 패널을 그릴 Canvas를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool battle = snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused;
            double serverNow = battle ? EstimateServerNow(snapshot) : 0d;
            // 조합판 구슬 크기(#257)는 콤보 화면이 숨어 있어도 정한다. 일시정지 중에는 그대로 둔다.
            if (snapshot == null || snapshot.phase != LabPhase.Playing) SetBoardLook(0);
            else if (!snapshot.paused) SetBoardLook(LocalEffect(snapshot, serverNow, out _));
            // 첫 콤보 예고 (#241)는 아직 콤보가 한 번도 안 나왔을 때 시작하므로 따로 본다.
            bool warning = battle && snapshot.ComboWarningOn(serverNow);
            bool anyCombo = snapshot != null && (snapshot.comboCount > 0 || warning);
            if (!battle || !anyCombo)
            {
                if (layer != null) layer.gameObject.SetActive(false);
                if (snapshot == null || snapshot.phase != LabPhase.Playing) ResetRound();
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();

            if (snapshot.comboCount < lastComboCount) ResetRound();
            DetectChanges(snapshot);
            DrawCombo(snapshot, serverNow);
            DrawResult();
            DrawWarning(snapshot, serverNow, warning);
            DrawEffect(snapshot, serverNow);
        }

        /// <summary>
        /// 콤보 예고 (#241): 패널 자리에 COMBO! 와 남은 초(3 → 2 → 1). 숫자가 바뀔 때마다 톡 튀어나오고,
        /// 0이 되는 순간(4칸이 나오는 시각) Host 스냅샷의 새 콤보로 바뀐다. 색은 예고 중에 보여주지 않는다.
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
            int count = Mathf.Max(1, Mathf.CeilToInt((float)(snapshot.comboNextAt - serverNow)));
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
            double totalTime = until - snapshot.comboResultAt;
            float remaining = kind != 0 && totalTime > 0d ? Mathf.Clamp01((float)((until - serverNow) / totalTime)) : 0f;
            // 보상은 배지 대신 부적 비로 보여준다. CLEAR!가 뜨는 순간부터 바로 쏟아진다 (#271).
            DrawStars(kind > 0, remaining);
            // 결과 문구나 다음 콤보 예고(#241)가 떠 있는 동안에는 숨겨 두었다가, 끝나면 톡 튀어나온다.
            if (kind >= 0 || resultElapsed >= 0f || warningActive)
            {
                effectKind = 0;
                effectBadge.gameObject.SetActive(false);
                return;
            }
            // 콤보 패널이 있으면 패널 아래 가장자리에, 없으면 패널 자리 가운데에 놓는다.
            effectBadge.anchoredPosition = panel.gameObject.activeSelf
                ? new Vector2(0f, -panel.sizeDelta.y * .5f) : Vector2.zero;
            if (kind != effectKind) effectPopElapsed = 0f;
            effectKind = kind;
            effectBadge.gameObject.SetActive(true);

            double left = until - serverNow;
            double total = until - snapshot.comboResultAt;
            float fraction = total > 0d ? Mathf.Clamp01((float)(left / total)) : 1f;
            effectGauge.anchorMax = new Vector2(fraction, 1f);
            bool reward = kind > 0;
            effectBack.color = reward ? RewardBadgeColor : PenaltyBadgeColor;
            effectIcon.localScale = Vector3.one * (reward ? 1f : .55f);
            // 남은 시간은 숫자 없이 게이지로만 (#257).
            effectText.text = reward ? RewardLabel : PenaltyLabel;

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

        /// <summary>스냅샷 사이에도 남은 시간이 매끄럽게 줄도록, 받은 뒤 흐른 실제 시간을 더한다.</summary>
        private double EstimateServerNow(LabSnapshot snapshot)
        {
            double realtime = Time.realtimeSinceStartupAsDouble;
            if (!ReferenceEquals(snapshot, seenSnapshot))
            {
                seenSnapshot = snapshot;
                seenAtRealtime = realtime;
            }
            // 스냅샷이 늦게 와도 최대 1초까지만 앞당겨 본다.
            return snapshot.serverTime + System.Math.Min(1d, realtime - seenAtRealtime);
        }

        private void DetectChanges(LabSnapshot snapshot)
        {
            if (snapshot.comboResultCount != shownResultCount && snapshot.comboResult != LabComboResult.None)
            {
                shownResultCount = snapshot.comboResultCount;
                shownResult = snapshot.comboResult;
                resultElapsed = 0f;
            }
            if (snapshot.comboCount != lastComboCount)
            {
                lastComboCount = snapshot.comboCount;
                lastProgress = 0;
            }
            if (snapshot.comboProgress > lastProgress)
            {
                popSlot = snapshot.comboProgress - 1;
                popElapsed = 0f;
            }
            lastProgress = snapshot.comboProgress;
        }

        private void DrawCombo(LabSnapshot snapshot, double serverNow)
        {
            // 결과 문구가 떠 있는 동안에는 패널을 숨긴다 (콤보 → 결과 → 배지가 겹치지 않게).
            bool showPanel = snapshot.comboActive && resultElapsed < 0f && snapshot.comboColors != null
                && snapshot.comboColors.Length == LabGourdCombo.Length;
            panel.gameObject.SetActive(showPanel);
            if (!showPanel) return;
            bool yaguang = YaguangCount;
            questionSlot.gameObject.SetActive(yaguang);
            if (yaguang)
                questionSlot.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(Time.unscaledTime * 2.3f));

            float bob = Mathf.Sin(Time.unscaledTime * 8f) * .08f;
            if (popElapsed >= 0f) popElapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < LabGourdCombo.Length; i++)
            {
                slotFills[i].color = LabPaintPalette.SrgbColorFor(snapshot.comboColors[i]);
                bool hit = i < snapshot.comboProgress;
                slotChecks[i].gameObject.SetActive(hit);
                float scale = 1f;
                if (snapshot.comboActive && i == snapshot.comboProgress) scale += bob;
                if (i == popSlot && popElapsed >= 0f && popElapsed < .25f)
                    scale += .3f * Mathf.Sin(popElapsed / .25f * Mathf.PI);
                slots[i].localScale = Vector3.one * scale;
                // 야광귀 셈법 (#271): 맞힌 칸 위에 그 칸의 수가 톡 튀어나와 남는다.
                Text word = slotWords[i];
                word.gameObject.SetActive(yaguang && hit);
                if (!(yaguang && hit)) continue;
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
                timeBarFill.color = left <= 3d ? FailColor : (Color)InkColor;
                timeBar.parent.gameObject.SetActive(true);
            }
            else timeBar.parent.gameObject.SetActive(false);
        }

        private void DrawResult()
        {
            if (resultElapsed < 0f)
            {
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
            resultText.text = clear ? ClearText : shownResult == LabComboResult.Timeout ? TimeUpText : WrongColorText;
            resultText.color = clear ? ClearColor : FailColor;
            float t = resultElapsed;
            float leave = Mathf.Clamp01((t - (ResultSeconds - .2f)) / .2f);
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
            resultElapsed = -1f;
            lastProgress = 0;
            lastComboCount = 0;
            popElapsed = -1f;
            popSlot = -1;
            effectKind = 0;
            effectPopElapsed = -1f;
            starsWereOn = false;
            if (stars != null) stars.Clear();
            shownWarningCount = -1;
            warningPopElapsed = -1f;
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
            Text[] existing = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < existing.Length && font == null; i++) font = existing[i].font;
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

            for (int i = 0; i < LabGourdCombo.Length; i++)
            {
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
            effectBadge = NewRect("Combo Effect Badge", layer);
            effectBadge.anchorMin = effectBadge.anchorMax = panelAnchor;
            effectBadge.sizeDelta = new Vector2(slotSize * 4.2f, badgeHeight);
            effectBadge.anchoredPosition = new Vector2(0f, -panel.sizeDelta.y * .5f);
            effectBack = effectBadge.gameObject.AddComponent<Image>();
            effectBack.sprite = PillSprite();
            effectBack.type = Image.Type.Sliced;
            effectBack.raycastTarget = false;

            // bar 홈: 배지 오른쪽 절반의 진한 알약. 그 안에서 테두리 있는 흰 bar가 오른쪽에서 왼쪽으로 줄어든다.
            RectTransform gaugeArea = NewRect("Gauge Track", effectBadge);
            gaugeArea.anchorMin = new Vector2(.5f, 0f);
            gaugeArea.anchorMax = Vector2.one;
            gaugeArea.offsetMin = new Vector2(0f, badgeHeight * .26f);
            gaugeArea.offsetMax = new Vector2(-badgeHeight * .42f, -badgeHeight * .26f);
            Image track = gaugeArea.gameObject.AddComponent<Image>();
            track.sprite = PillSprite();
            track.type = Image.Type.Sliced;
            track.color = new Color(.25f, .06f, .05f, .55f);
            track.raycastTarget = false;
            effectGauge = NewRect("Gauge", gaugeArea);
            effectGauge.anchorMin = Vector2.zero;
            effectGauge.anchorMax = Vector2.one;
            effectGauge.offsetMin = effectGauge.offsetMax = Vector2.zero;
            Image gauge = effectGauge.gameObject.AddComponent<Image>();
            gauge.sprite = PillSprite();
            gauge.type = Image.Type.Sliced;
            gauge.color = Color.white;
            gauge.raycastTarget = false;

            effectIcon = NewRect("Ball Icon", effectBadge);
            effectIcon.anchorMin = effectIcon.anchorMax = new Vector2(0f, .5f);
            effectIcon.sizeDelta = new Vector2(badgeHeight * .7f, badgeHeight * .7f);
            effectIcon.anchoredPosition = new Vector2(badgeHeight * .62f, 0f);
            Image icon = effectIcon.gameObject.AddComponent<Image>();
            icon.sprite = DiscSprite();
            icon.raycastTarget = false;

            effectText = NewText("Effect Label", effectBadge, Mathf.Max(10, Mathf.RoundToInt(20f * u)), TextAnchor.MiddleCenter);
            effectText.rectTransform.anchorMin = Vector2.zero;
            effectText.rectTransform.anchorMax = new Vector2(.5f, 1f);
            effectText.rectTransform.offsetMin = new Vector2(badgeHeight * 1.05f, 0f);
            effectText.rectTransform.offsetMax = new Vector2(-badgeHeight * .12f, 0f);
            effectText.color = Color.white;
            Outline effectOutline = effectText.gameObject.AddComponent<Outline>();
            effectOutline.effectColor = new Color32(43, 36, 32, 160);
            effectOutline.effectDistance = new Vector2(1.5f, -1.5f);
            effectBadge.gameObject.SetActive(false);

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

        private static Sprite RoundedSprite()
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

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
