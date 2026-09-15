using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace C6.Prototype.Presentation
{
    /// <summary>Unconnected presentation placeholders; no gameplay or network state is fabricated.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplitScreenLayout))]
    public sealed class T04Hud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.055f, 0.10f, 0.14f, 0.94f);
        private static readonly Color Panel = new Color(0.095f, 0.16f, 0.20f, 0.93f);
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
        private Rect lastSafeArea;
        private Rect lastTop;
        private Rect lastBottom;
        private Vector2Int lastScreen;
        private float lastCanvasScale;
        private SplitScreenLayout subscribedLayout;

        public Canvas Canvas { get; private set; }
        public UISafeArea SafeArea { get; private set; }
        public Button GenerateButton { get; private set; }
        public Text HpLabel { get; private set; }
        public Text TimerLabel { get; private set; }
        public Text StaminaLabel { get; private set; }
        public Text ConnectionLabel { get; private set; }
        public SplitScreenLayout Layout => layout;

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
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateUI();
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
            var canvasRect = Rect("T04Overlay", transform);
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

            CreateUpperHud();
            CreateLowerHud();
            divider = Rect("ViewportDivider", canvasRect);
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
            dividerImage.raycastTarget = false;

            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var events = new GameObject("T04EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        private void CreateUpperHud()
        {
            var header = Rect("Header", topContent);
            Top(header, 20f, 20f, 16f, 56f);
            var brand = Text("Brand", header, "HAPIO", 27, White, TextAnchor.UpperLeft);
            Top(brand.rectTransform, 0f, 128f, 0f, 33f);
            var preview = Text("PreviewCaption", header, "T04  /  LAYOUT PREVIEW", 10, Muted, TextAnchor.LowerLeft);
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
            Top(stats, 20f, 20f, 85f, 72f);
            var hp = Image("MonsterHpPanel", stats, Ink).rectTransform;
            hp.anchorMax = new Vector2(0.64f, 1f);
            hp.offsetMax = new Vector2(-5f, 0f);
            var hpCaption = Text("MonsterHpCaption", hp, "MONSTER HP", 10, Muted, TextAnchor.UpperLeft);
            Top(hpCaption.rectTransform, 13f, 10f, 10f, 14f);
            HpLabel = Text("MonsterHpValue", hp, "-- / --", 25, White, TextAnchor.MiddleLeft);
            Top(HpLabel.rectTransform, 13f, 10f, 28f, 30f);
            var hpTrack = Image("InactiveHpTrack", hp, new Color(0.32f, 0.43f, 0.47f, 0.6f));
            Bottom(hpTrack.rectTransform, 13f, 13f, 7f, 2f);

            var timer = Image("TeamTimePanel", stats, Ink).rectTransform;
            timer.anchorMin = new Vector2(0.64f, 0f);
            timer.offsetMin = new Vector2(5f, 0f);
            var timerCaption = Text("TeamTimeCaption", timer, "TEAM TIME", 10, Muted, TextAnchor.UpperLeft);
            Top(timerCaption.rectTransform, 12f, 10f, 10f, 14f);
            TimerLabel = Text("TeamTimeValue", timer, "--:--", 25, White, TextAnchor.MiddleLeft);
            Top(TimerLabel.rectTransform, 12f, 10f, 28f, 30f);

            var marker = Text("UnconnectedMarker", topContent, "HUD NOT CONNECTED", 10, Muted, TextAnchor.MiddleCenter);
            Bottom(marker.rectTransform, 20f, 20f, 12f, 18f);
        }

        private void CreateLowerHud()
        {
            var title = Text("WorkspaceTitle", bottomContent, "Orb workspace", 20, White, TextAnchor.MiddleLeft);
            Top(title.rectTransform, 20f, 20f, 18f, 30f);
            var subtitle = Text("WorkspaceCaption", bottomContent, "Empty area preview", 11, Muted, TextAnchor.MiddleLeft);
            Top(subtitle.rectTransform, 21f, 20f, 49f, 18f);

            var controls = Rect("ResourceControls", bottomContent);
            Bottom(controls, 20f, 20f, 19f, 73f);
            var stamina = Image("StaminaPanel", controls, Panel).rectTransform;
            stamina.anchorMax = new Vector2(0.48f, 1f);
            stamina.offsetMax = new Vector2(-6f, 0f);
            var staminaCaption = Text("StaminaCaption", stamina, "LOCAL STAMINA", 10, Muted, TextAnchor.UpperLeft);
            Top(staminaCaption.rectTransform, 13f, 10f, 12f, 14f);
            StaminaLabel = Text("StaminaValue", stamina, "-- / --", 24, White, TextAnchor.MiddleLeft);
            Top(StaminaLabel.rectTransform, 13f, 10f, 32f, 30f);

            var generateImage = Image("GenerateButton", controls, new Color(0.16f, 0.29f, 0.30f, 1f));
            var generate = generateImage.rectTransform;
            generate.anchorMin = new Vector2(0.48f, 0f);
            generate.offsetMin = new Vector2(6f, 0f);
            GenerateButton = generate.gameObject.AddComponent<Button>();
            GenerateButton.targetGraphic = generateImage;
            GenerateButton.transition = Selectable.Transition.None;
            GenerateButton.interactable = false;
            var caption = Text("GenerateCaption", generate, "Generate", 19, Teal, TextAnchor.MiddleCenter);
            Top(caption.rectTransform, 8f, 8f, 12f, 29f);
            var notWired = Text("GenerateStatus", generate, "NOT WIRED", 10, Muted, TextAnchor.MiddleCenter);
            Bottom(notWired.rectTransform, 8f, 8f, 12f, 16f);
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
            FitContent(topContent, top, 235f);
            FitContent(bottomContent, bottom, 215f);
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
