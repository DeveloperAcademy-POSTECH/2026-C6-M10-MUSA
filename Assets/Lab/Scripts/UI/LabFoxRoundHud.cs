using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 여우구슬 팀 판정 (#664)의 화면 위 표시. 여우불이 떨어진 순간부터 판정이 끝날 때까지, 야광귀 콤보 패널과 같은 자리·모양의
    /// 패널에 여우구슬 그림과 팀 전체 남은 여우구슬 수, 그 아래 줄어드는 남은 시간 막대를 보여 준다. 남은 시간은 콤보처럼
    /// 숫자 없이 막대로만 보이고, 째깍 소리(LabYaguangSound)가 함께 난다.
    /// - 수가 줄 때마다 숫자가 톡 튄다. 남은 시간이 3초 이하면 막대가 빨개지고 패널이 두근거린다.
    /// - 다 지우거나 시간이 다 되면 바로 사라지고, 같은 자리에 LabGourdComboHud가 성공!/시간 끝!과 BIG/SMALL 표시를 띄운다.
    /// 모든 기기가 Host 스냅샷만 읽는다. 화면 표시만 하며 터치를 막지 않는다. 일시정지 중이거나 전투가 끝나면 숨는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabFoxRoundHud : MonoBehaviour
    {
        public const string CountLabel = "여우구슬";
        /// <summary>남은 시간이 이 초 이하면 막대가 빨개지고 패널이 두근거린다 (콤보 막대와 같음).</summary>
        public const double UrgentSeconds = 3d;

        [Tooltip("화면에서 패널 가운데 위치 (0~1, 왼쪽 아래가 0). 야광귀 콤보 패널과 같은 자리.")]
        [SerializeField] private Vector2 panelAnchor = new Vector2(.5f, .8f);
        [Tooltip("여우구슬 그림 크기 (Canvas 기준 픽셀). 패널·글자·막대가 모두 이 크기에 비례한다 (콤보 칸 크기와 같음).")]
        [SerializeField, Min(16f)] private float iconSize = 40f;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer, panel, timeBar;
        private Image timeBarFill, icon;
        private Text label, number;
        private int shownCount = -1;
        private double shownRound = double.NaN;
        private float popElapsed = -1f;

        /// <summary>판정 패널이 보이는지</summary>
        public bool Visible => panel != null && panel.gameObject.activeInHierarchy;
        /// <summary>보이는 남은 여우구슬 수 (안 보이면 빈 문자열)</summary>
        public string CountText => Visible ? number.text : string.Empty;
        /// <summary>남은 시간 막대 비율 (1 → 0)</summary>
        public float TimeFill => timeBar != null ? timeBar.anchorMax.x : 0f;
        /// <summary>남은 시간이 짧아 막대가 빨간지</summary>
        public bool Urgent { get; private set; }
        public bool AnyImageTakesTouches
        {
            get
            {
                if (layer == null) return false;
                foreach (Graphic graphic in layer.GetComponentsInChildren<Graphic>(true))
                    if (graphic.raycastTarget) return true;
                return false;
            }
        }
        /// <summary>여우구슬 그림 (Resources/Sabotage/FoxOrb, 판의 여우구슬과 같은 그림)</summary>
        public Sprite IconSprite => icon != null ? icon.sprite : null;

        /// <summary>스냅샷을 읽을 네트워크와 패널을 그릴 Canvas를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool battle = snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused;
            Present(snapshot, battle ? network.BattlePresentationTime : 0d, battle, Time.unscaledDeltaTime);
        }

        /// <summary>그 시각의 판정을 그린다 (검사에서 시각을 정해 부를 수 있게 공개한다).</summary>
        public void Present(LabSnapshot snapshot, double now, bool battle, float deltaTime)
        {
            bool on = battle && snapshot != null && snapshot.FoxRoundOn(now);
            if (!on)
            {
                if (layer != null) layer.gameObject.SetActive(false);
                shownCount = -1;
                popElapsed = -1f;
                Urgent = false;
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();

            // 수: 새 판정이면 처음 수 그대로, 같은 판정에서 줄면 톡 튄다.
            int count = Mathf.Max(0, snapshot.foxRoundRemaining);
            if (shownRound != snapshot.foxRoundStartedAt) shownCount = -1;
            shownRound = snapshot.foxRoundStartedAt;
            if (count != shownCount)
            {
                if (shownCount >= 0 && count < shownCount) popElapsed = 0f;
                shownCount = count;
                number.text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            float pop = 1f;
            if (popElapsed >= 0f)
            {
                popElapsed += deltaTime;
                float t = Mathf.Clamp01(popElapsed / .25f);
                pop = Mathf.Lerp(1.7f, 1f, 1f - (1f - t) * (1f - t));
                if (popElapsed >= .25f) popElapsed = -1f;
            }
            number.rectTransform.localScale = Vector3.one * pop;

            // 남은 시간 막대: 숫자 없이 줄어들기만. 3초 이하면 빨갛다.
            double left = System.Math.Max(0d, snapshot.foxRoundEndsAt - now);
            double limit = snapshot.foxRoundEndsAt - snapshot.foxRoundStartedAt;
            timeBar.anchorMax = new Vector2(limit > 0d ? Mathf.Clamp01((float)(left / limit)) : 0f, 1f);
            Urgent = left <= UrgentSeconds;
            timeBarFill.color = Urgent ? LabGourdComboHud.FailColor : (Color)LabGourdComboHud.InkColor;
            number.color = Urgent ? LabGourdComboHud.FailColor : (Color)LabGourdComboHud.InkColor;
            float beat = Urgent ? .05f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f)) : 0f;
            panel.localScale = Vector3.one * (1f + beat);
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(Time.unscaledTime * 3f));
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
            Font font = LabYaguangFeverHud.ShillaFont(canvas);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            layer = NewRect("Fox Round", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;

            // 콤보 패널과 같은 크기 (색 칸 4개 + 간격 5개 너비, 칸 하나 + 간격 3개 높이).
            float u = iconSize / 64f;
            float gap = iconSize * .22f;
            float width = iconSize * LabGourdCombo.Length + gap * (LabGourdCombo.Length + 1);
            panel = NewRect("Fox Round Panel", layer);
            panel.anchorMin = panel.anchorMax = panelAnchor;
            panel.sizeDelta = new Vector2(width, iconSize + gap * 3f + 10f * u);
            Image back = panel.gameObject.AddComponent<Image>();
            back.sprite = LabGourdComboHud.RoundedSprite();
            back.type = Image.Type.Sliced;
            back.color = LabGourdComboHud.PanelColor;
            back.raycastTarget = false;

            // 왼쪽 여우구슬 그림, 가운데 "여우구슬", 오른쪽 남은 수.
            RectTransform iconRect = NewRect("Fox Orb Icon", panel);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(gap + iconSize * .5f, -gap - iconSize * .5f);
            icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = Resources.Load<Sprite>(LabOrbAppearance.FoxFireSpritePath);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (icon.sprite == null) icon.color = new Color(.95f, .45f, .2f, 1f);

            float rowY = -gap - iconSize * .5f;
            label = NewText("Fox Round Label", panel, font, Mathf.Max(10, Mathf.RoundToInt(30f * u)), TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, .5f);
            label.rectTransform.sizeDelta = new Vector2(width * .5f, iconSize);
            label.rectTransform.anchoredPosition = new Vector2(gap * 2f + iconSize, rowY);
            label.text = CountLabel;

            number = NewText("Fox Round Count", panel, font, Mathf.Max(14, Mathf.RoundToInt(60f * u)), TextAnchor.MiddleCenter);
            number.rectTransform.anchorMin = number.rectTransform.anchorMax = new Vector2(1f, 1f);
            number.rectTransform.sizeDelta = new Vector2(iconSize * 1.4f, iconSize * 1.2f);
            number.rectTransform.anchoredPosition = new Vector2(-gap - iconSize * .7f, rowY);
            Outline numberOutline = number.gameObject.AddComponent<Outline>();
            numberOutline.effectColor = Color.white;
            numberOutline.effectDistance = new Vector2(2f, -2f) * Mathf.Max(.5f, u);

            // 남은 시간 막대 (콤보 막대와 같은 자리·굵기)
            RectTransform barBack = NewRect("Fox Round Time Bar", panel);
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
            timeBarFill.color = LabGourdComboHud.InkColor;
            timeBarFill.raycastTarget = false;
            return true;
        }

        private static Text NewText(string name, Transform parent, Font font, int size, TextAnchor alignment)
        {
            Text text = NewRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = LabGourdComboHud.InkColor;
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

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
