using System;
using System.Globalization;
using C6.Prototype.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace C6.Prototype.Combination
{
    /// <summary>Combination UI; displays confirmed resources and explicit mixed fixture controls.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplitScreenLayout))]
    public sealed class T08Hud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.055f, 0.10f, 0.14f, 0.94f);
        private static readonly Color White = new Color(0.93f, 0.96f, 0.95f, 1f);
        private static readonly Color Muted = new Color(0.58f, 0.70f, 0.72f, 1f);
        private static readonly Color Teal = new Color(0.39f, 0.80f, 0.74f, 1f);
        private static readonly Color Gold = new Color(0.86f, 0.73f, 0.47f, 1f);

        [SerializeField] private SplitScreenLayout layout;
        private Font font;
        private RectTransform topZone;
        private RectTransform bottomZone;
        private RectTransform topContent;
        private RectTransform bottomContent;
        private RectTransform divider;
        private RectTransform footer;
        private RectTransform networkFields;
        private bool networkFieldsVisible = true;
        private RectTransform attackBand;
        private RectTransform attackCaption;
        private Rect attackScreenRect;
        private readonly Vector3[] footerCorners = new Vector3[4];
        private string networkStatus = "OFFLINE";
        private string actionStatus = "Start a dev host or join";
        private string detailStatus = "Normal start: 0 orbs / Stamina 100";
        private bool canHost = true;
        private bool canJoin = true;
        private bool canReset;
        private bool canEnd;
        private bool canGenerate, canDebugFixture;
        private RectTransform staminaFill;
        private Text generateCaption;
        private Rect lastSafeArea;
        private Rect lastTop;
        private Rect lastBottom;
        private Vector2Int lastScreen;
        private float lastCanvasScale;
        private SplitScreenLayout subscribedLayout;

        public Canvas Canvas { get; private set; }
        public UISafeArea SafeArea { get; private set; }
        public Button HostButton { get; private set; }
        public Button JoinButton { get; private set; }
        public InputField IPv4Input { get; private set; }
        public InputField PortInput { get; private set; }
        public string HostAddress => IPv4Input != null ? IPv4Input.text : string.Empty;
        public string Port => PortInput != null ? PortInput.text : "7777";
        public Button ResetButton { get; private set; }
        public Button EndButton { get; private set; }
        public Text ActionLabel { get; private set; }
        public Text DetailLabel { get; private set; }
        public Text HpLabel { get; private set; }
        public Text ProgressLabel { get; private set; }
        public Text StaminaLabel { get; private set; }
        public Text RecoveryLabel { get; private set; }
        public Text ResourceModeLabel { get; private set; }
        public Text StorageLabel { get; private set; }
        public Button GenerateButton { get; private set; }
        public Button DebugFixtureButton { get; private set; }
        public Text RoundLabel { get; private set; }
        public Text ConnectionLabel { get; private set; }
        public SplitScreenLayout Layout => layout;

        /// <summary>Visual clamp area only. Gesture tests still use original pointer coordinates.</summary>
        public Rect OrbWorkspaceScreenRect
        {
            get
            {
                if (layout == null) return UnityEngine.Rect.zero;
                var area = Intersection(layout.BottomPixelRect, Screen.safeArea);
                if (footer == null || Canvas == null) return area;
                footer.GetWorldCorners(footerCorners);
                float footerTop = RectTransformUtility.WorldToScreenPoint(null, footerCorners[1]).y;
                float minY = Mathf.Clamp(footerTop, area.yMin, area.yMax);
                return new Rect(area.xMin, minY, area.width, Mathf.Max(0f, area.yMax - minY));
            }
        }

        public void SetStatus(string network, string action, string detail)
        {
            networkStatus = network ?? string.Empty;
            actionStatus = action ?? string.Empty;
            detailStatus = detail ?? string.Empty;
            if (ConnectionLabel != null) ConnectionLabel.text = networkStatus;
            if (ActionLabel != null) ActionLabel.text = actionStatus;
            if (DetailLabel != null) DetailLabel.text = detailStatus;
        }

        public void SetProgress(int hp, int maxHp, int totalHits, uint round, int resets)
        {
            if (HpLabel != null) HpLabel.text = hp + " / " + maxHp;
            if (ProgressLabel != null) ProgressLabel.text = "VALID HITS " + totalHits;
            if (RoundLabel != null) RoundLabel.text = "ROUND " + round + "  /  RESETS " + resets;
        }

        /// <summary>Continuous Host value. Markers divide the bar visually and do not quantize resources.</summary>
        public void SetResources(double value, double max, double cost, double rate,
            int stored, int cap, bool pending, bool isDebugMode)
        {
            double maximum = Finite(max) && max > 0 ? max : 100d;
            double confirmed = Finite(value) ? Math.Max(0d, Math.Min(maximum, value)) : 0d;
            double fraction = confirmed / maximum;
            if (StaminaLabel != null)
                StaminaLabel.text = Number(confirmed) + " / " + Number(maximum);
            if (staminaFill != null) staminaFill.anchorMax = new Vector2((float)fraction, 1f);
            if (RecoveryLabel != null)
            {
                string recovery = Finite(rate) && rate > 0d && Finite(cost) && cost > 0d
                    ? "AUTO +" + Number(cost) + " / " + Number(cost / rate) + "s" : "RECOVERY PAUSED";
                RecoveryLabel.text = (confirmed >= maximum ? "FULL / " : string.Empty) + recovery;
            }
            if (StorageLabel != null) StorageLabel.text = "ORBS " + stored + " / " + cap;
            if (ResourceModeLabel != null)
            {
                ResourceModeLabel.text = isDebugMode ? "DEBUG_TEST_MODE / FIXTURE" : "NORMAL / EMPTY START";
                ResourceModeLabel.color = isDebugMode ? Gold : Muted;
            }
            if (generateCaption != null)
                generateCaption.text = pending ? "WAITING FOR HOST" : "GENERATE  /  " + Number(cost);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        public void SetNetworkFieldsVisible(bool visible)
        {
            if (networkFieldsVisible == visible) return;
            networkFieldsVisible = visible;
            if (networkFields != null) networkFields.gameObject.SetActive(visible);
            if (footer != null) Bottom(footer, 0f, 0f, 0f, visible ? 190f : 144f);
        }

        public void SetControls(bool host, bool join, bool reset, bool end, bool generate,
            bool debugFixture)
        {
            canHost = host; canJoin = join; canReset = reset; canEnd = end;
            canGenerate = generate; canDebugFixture = debugFixture;
            if (HostButton != null) HostButton.interactable = canHost;
            if (JoinButton != null) JoinButton.interactable = canJoin;
            if (IPv4Input != null) IPv4Input.interactable = canJoin;
            if (PortInput != null) PortInput.interactable = canHost || canJoin;
            if (ResetButton != null) ResetButton.interactable = canReset;
            if (EndButton != null) EndButton.interactable = canEnd;
            if (GenerateButton != null) GenerateButton.interactable = canGenerate;
            if (DebugFixtureButton != null) DebugFixtureButton.interactable = canDebugFixture;
        }

        public void SetAttackZone(Rect screenRect)
        {
            if (attackScreenRect == screenRect) return;
            attackScreenRect = screenRect;
            RefreshAttackZone();
        }


        public void Configure(SplitScreenLayout splitLayout)
        {
            Unsubscribe();
            layout = splitLayout;
            if (isActiveAndEnabled)
                Subscribe();
            RefreshRegions();
        }

        private void Awake()
        {
            if (layout == null)
                layout = GetComponent<SplitScreenLayout>();
            font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateUI();
            SetStatus(networkStatus, actionStatus, detailStatus);
            SetControls(canHost, canJoin, canReset, canEnd, canGenerate, canDebugFixture);
            SetResources(100d, 100d, 20d, 20d / 3d, 0, 20, false, false);
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshRegions();
        }

        private void Start() => RefreshRegions();

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void LateUpdate()
        {
            if (layout != null && (lastScreen.x != Screen.width || lastScreen.y != Screen.height ||
                                   lastSafeArea != Screen.safeArea || lastTop != layout.TopPixelRect ||
                                   lastBottom != layout.BottomPixelRect ||
                                   (Canvas != null && !Mathf.Approximately(lastCanvasScale, Canvas.scaleFactor))))
                RefreshRegions();
        }

        private void Subscribe()
        {
            if (layout == null || subscribedLayout == layout)
                return;
            Unsubscribe();
            subscribedLayout = layout;
            subscribedLayout.Changed += RefreshRegions;
        }

        private void Unsubscribe()
        {
            if (subscribedLayout != null)
                subscribedLayout.Changed -= RefreshRegions;
            subscribedLayout = null;
        }

        private void CreateUI()
        {
            var canvasRect = Rect("T08Overlay", transform);
            Canvas = canvasRect.gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();

            var safeRect = Rect("SafeArea", canvasRect);
            SafeArea = safeRect.gameObject.AddComponent<UISafeArea>();
            topZone = Rect("UpperSafeViewport", safeRect);
            bottomZone = Rect("LowerSafeViewport", safeRect);
            topZone.gameObject.AddComponent<RectMask2D>();
            bottomZone.gameObject.AddComponent<RectMask2D>();
            topContent = Rect("UpperHudContent", topZone);
            bottomContent = Rect("LowerHudContent", bottomZone);

            attackBand = Image("AttackZoneBand", canvasRect, new Color(0.25f, 0.61f, 0.57f, 0.16f)).rectTransform;
            attackBand.SetAsFirstSibling();
            attackCaption = Text("AttackZoneCaption", safeRect, "ATTACK ZONE  /  COMBINED ONLY", 11, Teal,
                TextAnchor.MiddleCenter).rectTransform;
            attackBand.gameObject.SetActive(false);
            attackCaption.gameObject.SetActive(false);
            CreateUpperHud();
            CreateLowerHud();
            divider = Rect("ViewportDivider", canvasRect);
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
            dividerImage.raycastTarget = false;

            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var events = new GameObject("T08EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        private void CreateUpperHud()
        {
            var header = Rect("Header", topContent);
            Top(header, 20f, 20f, 16f, 56f);
            var brand = Text("Brand", header, "HAPIO", 27, White, TextAnchor.UpperLeft);
            Top(brand.rectTransform, 0f, 128f, 0f, 33f);
            var preview = Text("PreviewCaption", header, "T08  /  YIN + YANG COMBINATION", 10, Muted, TextAnchor.LowerLeft);
            Top(preview.rectTransform, 1f, 0f, 34f, 16f);

            var status = Rect("ConnectionStatus", header);
            status.anchorMin = new Vector2(1f, 1f);
            status.anchorMax = Vector2.one;
            status.pivot = Vector2.one;
            status.anchoredPosition = new Vector2(0f, -5f);
            status.sizeDelta = new Vector2(103f, 25f);
            var dot = Image("StatusDot", status, Gold);
            dot.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(5f, 5f);
            dot.rectTransform.anchoredPosition = new Vector2(6f, 0f);
            ConnectionLabel = Text("ConnectionValue", status, "OFFLINE", 11, Gold, TextAnchor.MiddleRight);

            var stats = Rect("BattleStats", topContent);
            Top(stats, 20f, 20f, 76f, 80f);
            var hp = Image("MonsterHpPanel", stats, Ink).rectTransform;
            hp.anchorMax = new Vector2(0.46f, 1f);
            hp.offsetMax = new Vector2(-5f, 0f);
            var hpCaption = Text("MonsterHpCaption", hp, "MONSTER HP", 10, Muted, TextAnchor.UpperLeft);
            Top(hpCaption.rectTransform, 13f, 10f, 8f, 12f);
            HpLabel = Text("MonsterHpValue", hp, "100 / 100", 24, White, TextAnchor.MiddleLeft);
            Top(HpLabel.rectTransform, 13f, 10f, 22f, 27f);
            ProgressLabel = Text("HitProgressValue", hp, "VALID HITS 0", 10, Muted, TextAnchor.MiddleLeft);
            Bottom(ProgressLabel.rectTransform, 13f, 10f, 5f, 14f);

            var stamina = Image("PersonalStaminaPanel", stats, Ink).rectTransform;
            stamina.anchorMin = new Vector2(0.46f, 0f);
            stamina.offsetMin = new Vector2(5f, 0f);
            var staminaCaption = Text("StaminaCaption", stamina, "YOUR STAMINA", 10, Muted, TextAnchor.UpperLeft);
            Top(staminaCaption.rectTransform, 12f, 10f, 8f, 12f);
            StaminaLabel = Text("StaminaValue", stamina, "100 / 100", 24, White, TextAnchor.MiddleLeft);
            Top(StaminaLabel.rectTransform, 12f, 10f, 22f, 27f);
            var track = Image("ContinuousStaminaTrack", stamina, new Color(.16f, .25f, .28f, 1f)).rectTransform;
            Bottom(track, 12f, 12f, 20f, 7f);
            staminaFill = Image("ConfirmedStaminaFill", track, Teal).rectTransform;
            // Five visual sections, each worth twenty. Fill remains continuous through them.
            for (int i = 1; i < 5; i++)
            {
                var tick = Image("TwentyMarker" + i, track, new Color(.03f, .08f, .10f, .75f)).rectTransform;
                tick.anchorMin = new Vector2(i / 5f, 0f);
                tick.anchorMax = new Vector2(i / 5f, 1f);
                tick.anchoredPosition = Vector2.zero;
                tick.sizeDelta = new Vector2(1f, 0f);
            }
            RecoveryLabel = Text("RecoveryStatus", stamina, "FULL  /  AUTO +20 / 3s", 9, Muted, TextAnchor.MiddleLeft);
            Bottom(RecoveryLabel.rectTransform, 12f, 10f, 5f, 12f);

            ResourceModeLabel = Text("ResourceMode", topContent, "NORMAL / EMPTY START", 10, Muted, TextAnchor.MiddleLeft);
            Top(ResourceModeLabel.rectTransform, 20f, 125f, 160f, 16f);
            StorageLabel = Text("PersonalStorage", topContent, "ORBS 0 / 20", 10, White, TextAnchor.MiddleRight);
            Top(StorageLabel.rectTransform, 250f, 20f, 160f, 16f);
            RoundLabel = Text("ResourceProgress", topContent, "ROUND 0  /  RESETS 0", 10, Muted, TextAnchor.MiddleCenter);
            Bottom(RoundLabel.rectTransform, 20f, 20f, 12f, 18f);
        }

        private void CreateLowerHud()
        {
            footer = Image("ResourceControls", bottomContent, Ink).rectTransform;
            Bottom(footer, 0f, 0f, 0f, networkFieldsVisible ? 190f : 144f);
            var marker = Image("FooterTopRule", footer, new Color(Teal.r, Teal.g, Teal.b, .25f));
            Top(marker.rectTransform, 20f, 20f, 0f, 1f);
            ActionLabel = Text("ActionStatus", footer, actionStatus, 12, White, TextAnchor.MiddleLeft);
            Bottom(ActionLabel.rectTransform, 20f, 20f, 120f, 18f);
            DetailLabel = Text("ActionDetail", footer, detailStatus, 10, Muted, TextAnchor.UpperLeft);
            Bottom(DetailLabel.rectTransform, 20f, 20f, 90f, 26f);

            networkFields = Rect("NetworkFields", footer);
            Bottom(networkFields, 20f, 20f, 146f, 36f);
            IPv4Input = CreateInput("HostIPv4", networkFields, "Host IPv4", string.Empty, 0f, .73f);
            PortInput = CreateInput("Port", networkFields, "Port", "7777", .73f, 1f);
            PortInput.contentType = InputField.ContentType.IntegerNumber;
            PortInput.characterLimit = 5;
            IPv4Input.characterLimit = 15;
            networkFields.gameObject.SetActive(networkFieldsVisible);

            var resourceButtons = Rect("ResourceButtons", footer);
            Bottom(resourceButtons, 12f, 12f, 44f, 40f);
            GenerateButton = CreateButton("GenerateButton", resourceButtons, "GENERATE  /  20", 0f, .68f);
            generateCaption = GenerateButton.GetComponentInChildren<Text>();
            DebugFixtureButton = CreateButton("DebugFixtureButton", resourceButtons, "MIXED\nFIXTURE", .68f, 1f);
            var buttons = Rect("SessionButtons", footer);
            Bottom(buttons, 12f, 12f, 6f, 31f);
            HostButton = CreateButton("DevHostButton", buttons, "HOST", 0f, .21f);
            JoinButton = CreateButton("JoinButton", buttons, "JOIN", .21f, .42f);
            ResetButton = CreateButton("ResetResourceButton", buttons, "RESET EMPTY", .42f, .79f);
            EndButton = CreateButton("EndTestButton", buttons, "END", .79f, 1f);
        }

        private InputField CreateInput(string objectName, RectTransform parent, string placeholder,
            string value, float left, float right)
        {
            var background = Image(objectName, parent, new Color(.12f, .22f, .27f, 1f));
            background.raycastTarget = true;
            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(left > 0f ? 4f : 0f, 0f);
            rect.offsetMax = new Vector2(right < 1f ? -4f : 0f, 0f);
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            var valueText = Text("Value", rect, value, 13, White, TextAnchor.MiddleLeft);
            valueText.rectTransform.offsetMin = new Vector2(10f, 3f);
            valueText.rectTransform.offsetMax = new Vector2(-10f, -3f);
            input.textComponent = valueText;
            var prompt = Text("Placeholder", rect, placeholder, 12, Muted, TextAnchor.MiddleLeft);
            prompt.rectTransform.offsetMin = new Vector2(10f, 3f);
            prompt.rectTransform.offsetMax = new Vector2(-10f, -3f);
            input.placeholder = prompt;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Standard;
            input.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            input.text = value;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        private Button CreateButton(string objectName, RectTransform parent, string caption, float left, float right)
        {
            var background = Image(objectName, parent, new Color(0.17f, 0.32f, 0.34f, 1f));
            background.raycastTarget = true;
            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(left > 0f ? 4f : 0f, 0f);
            rect.offsetMax = new Vector2(right < 1f ? -4f : 0f, 0f);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.50f, 0.55f, 0.56f, 0.9f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Text("Caption", rect, caption, 11, White, TextAnchor.MiddleCenter);
            label.rectTransform.offsetMin = new Vector2(4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -2f);
            return button;
        }

        private void RefreshAttackZone()
        {
            if (Canvas == null || attackBand == null || attackCaption == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            var zone = Intersection(attackScreenRect, screen);
            // The band follows the camera's full pixel rect; only its caption is protected by Safe Area.
            ApplyRegion(attackBand, zone, screen);
            ApplyRegion(attackCaption, Intersection(zone, Screen.safeArea), Screen.safeArea);
        }

        private void RefreshRegions()
        {
            if (Canvas == null || layout == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            lastCanvasScale = Canvas.scaleFactor;
            lastSafeArea = Screen.safeArea;
            lastTop = layout.TopPixelRect;
            lastBottom = layout.BottomPixelRect;
            var safe = lastSafeArea;
            var top = Intersection(safe, lastTop);
            var bottom = Intersection(safe, lastBottom);
            ApplyRegion(topZone, top, safe);
            ApplyRegion(bottomZone, bottom, safe);
            // Safe Area protects the HUD inside each actual viewport. It never moves either camera.
            FitContent(topContent, top, 250f);
            FitContent(bottomContent, bottom, 280f);
            RefreshAttackZone();
            float boundary = Mathf.Clamp01(lastBottom.yMax / Screen.height);
            divider.anchorMin = new Vector2(0f, boundary);
            divider.anchorMax = new Vector2(1f, boundary);
            divider.pivot = new Vector2(0.5f, 0.5f);
            divider.anchoredPosition = Vector2.zero;
            divider.sizeDelta = new Vector2(0f, 1f);
        }

        private void FitContent(RectTransform content, Rect region, float minimumHeight)
        {
            float canvasScale = Mathf.Max(0.0001f, Canvas.scaleFactor);
            float width = Mathf.Min(region.width / canvasScale, 580f);
            float height = region.height / canvasScale;
            float scale = Mathf.Max(0.01f, Mathf.Min(1f, width / 320f, height / minimumHeight));
            content.anchorMin = new Vector2(0.5f, 0f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(width / scale, height / scale - height);
            content.localScale = new Vector3(scale, scale, 1f);
        }

        private static void ApplyRegion(RectTransform panel, Rect region, Rect safe)
        {
            panel.gameObject.SetActive(region.width > 0f && region.height > 0f);
            float width = Mathf.Max(1f, safe.width);
            float height = Mathf.Max(1f, safe.height);
            panel.anchorMin = new Vector2((region.xMin - safe.xMin) / width, (region.yMin - safe.yMin) / height);
            panel.anchorMax = new Vector2((region.xMax - safe.xMin) / width, (region.yMax - safe.yMin) / height);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }

        private static Rect Intersection(Rect a, Rect b)
        {
            float x = Mathf.Max(a.xMin, b.xMin);
            float y = Mathf.Max(a.yMin, b.yMin);
            return new Rect(x, y, Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - x),
                Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - y));
        }

        private Text Text(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(size, 9);
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Top(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Bottom(RectTransform rect, float left, float right, float bottom, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }
    }
}
