using System;
using System.Globalization;
using C6.Prototype.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace C6.Prototype.Battle
{
    /// <summary>Battle UI with explicit Host Start, authoritative clock/team HP, and a frozen result panel.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplitScreenLayout))]
    public sealed class T09Hud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.055f, 0.10f, 0.14f, 0.94f);
        private static readonly Color White = new Color(0.93f, 0.96f, 0.95f, 1f);
        private static readonly Color Muted = new Color(0.58f, 0.70f, 0.72f, 1f);
        private static readonly Color Teal = new Color(0.39f, 0.80f, 0.74f, 1f);
        private static readonly Color Gold = new Color(0.86f, 0.73f, 0.47f, 1f);

        [SerializeField] private SplitScreenLayout layout;
        [SerializeField, HideInInspector] private bool useSceneHierarchy;
        public bool UseSceneHierarchy => useSceneHierarchy;
        private Font font;
        [SerializeField] private RectTransform topZone;
        [SerializeField] private RectTransform bottomZone;
        [SerializeField] private RectTransform topContent;
        [SerializeField] private RectTransform bottomContent;
        [SerializeField] private RectTransform divider;
        [SerializeField] private RectTransform footer;
        [SerializeField] private RectTransform networkFields;
        [SerializeField] private bool networkFieldsVisible = true;
        private readonly Vector3[] footerCorners = new Vector3[4];
        private string networkStatus = "OFFLINE";
        private string actionStatus = "Start a dev host or join";
        private string detailStatus = "Normal start: 0 orbs / Stamina 100";
        private bool canHost = true;
        private bool canJoin = true;
        private bool canStart;
        private bool canSolo = true;
        private int lastMonsterMaximum = 100;
        private double lastStaminaMaximum = 100d;
        private bool canEnd;
        private bool canGenerate, canDebugFixture;
        [SerializeField] private bool minimalBattlePresentation;
        [SerializeField] private RectTransform monsterHpFill;
        [SerializeField] private RectTransform teamTimeFill;
        [SerializeField] private Text teamTimeValue;
        [SerializeField] private RectTransform staminaFill;
        [SerializeField] private Text generateCaption;
        private Rect lastSafeArea;
        private Rect lastTop;
        private Rect lastBottom;
        private Vector2Int lastScreen;
        private float lastCanvasScale;
        private SplitScreenLayout subscribedLayout;

        public bool CoordinatedGame { get; set; }
        public int ParticipantCapacity { get; set; } = 2;
        [field: SerializeField] public Canvas Canvas { get; private set; }
        [field: SerializeField] public UISafeArea SafeArea { get; private set; }
        [field: SerializeField] public Button HostButton { get; private set; }
        [field: SerializeField] public Button JoinButton { get; private set; }
        [field: SerializeField] public InputField IPv4Input { get; private set; }
        [field: SerializeField] public InputField PortInput { get; private set; }
        public string HostAddress => IPv4Input != null ? IPv4Input.text : string.Empty;
        public string Port => PortInput != null ? PortInput.text : "7777";
        [field: SerializeField] public Button StartButton { get; private set; }
        [field: SerializeField] public Button SoloModeButton { get; private set; }
        [field: SerializeField] public Button RetryButton { get; private set; }
        [field: SerializeField] public Button LobbyButton { get; private set; }
        [field: SerializeField] public Button ResultEndButton { get; private set; }
        [field: SerializeField] public GameObject ResultOverlay { get; private set; }
        [field: SerializeField] public Text ResultTitle { get; private set; }
        [field: SerializeField] public Text ResultSummary { get; private set; }
        [field: SerializeField] public Text ResultStatus { get; private set; }
        [field: SerializeField] public Text ClockLabel { get; private set; }
        [field: SerializeField] public Text TeamHpLabel { get; private set; }
        [field: SerializeField] public Text PhaseLabel { get; private set; }
        [field: SerializeField] public Button EndButton { get; private set; }
        [field: SerializeField] public Text ActionLabel { get; private set; }
        [field: SerializeField] public Text DetailLabel { get; private set; }
        [field: SerializeField] public Text HpLabel { get; private set; }
        [field: SerializeField] public Text ProgressLabel { get; private set; }
        [field: SerializeField] public Text StaminaLabel { get; private set; }
        [field: SerializeField] public Text RecoveryLabel { get; private set; }
        [field: SerializeField] public Text ResourceModeLabel { get; private set; }
        [field: SerializeField] public Text StorageLabel { get; private set; }
        [field: SerializeField] public Button GenerateButton { get; private set; }
        [field: SerializeField] public Button DebugFixtureButton { get; private set; }
        [field: SerializeField] public Text RoundLabel { get; private set; }
        [field: SerializeField] public Text ConnectionLabel { get; private set; }
        public SplitScreenLayout Layout => layout;
        public bool MinimalBattlePresentation => minimalBattlePresentation;
        public RectTransform MonsterHpFill => monsterHpFill;
        public RectTransform TeamTimeFill => teamTimeFill;
        public Text TeamTimeValue => teamTimeValue;
        public RectTransform StaminaFill => staminaFill;

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
            lastMonsterMaximum = maxHp;
            if (HpLabel != null) HpLabel.text = hp + " / " + maxHp;
            if (ProgressLabel != null) ProgressLabel.text = "VALID HITS " + totalHits;
            if (RoundLabel != null) RoundLabel.text = "ROUND " + round + "  /  RESETS " + resets;
            SetHorizontalFill(monsterHpFill, hp, maxHp);
        }

        /// <summary>Continuous Host value. Markers divide the bar visually and do not quantize resources.</summary>
        public void SetResources(double value, double max, double cost, double rate,
            int stored, int cap, bool pending, bool isDebugMode)
        {
            double maximum = Finite(max) && max > 0 ? max : 100d;
            lastStaminaMaximum = maximum;
            double confirmed = Finite(value) ? Math.Max(0d, Math.Min(maximum, value)) : 0d;
            if (StaminaLabel != null)
                StaminaLabel.text = Number(confirmed) + " / " + Number(maximum);
            SetHorizontalFill(staminaFill, confirmed, maximum);
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

        private static void SetHorizontalFill(RectTransform fill, double value, double maximum)
        {
            if (fill == null) return;
            float fraction = Finite(value) && Finite(maximum) && maximum > 0d
                ? Mathf.Clamp01((float)(value / maximum)) : 0f;
            Vector2 anchorMax = fill.anchorMax;
            anchorMax.x = fraction;
            fill.anchorMax = anchorMax;
        }

        /// <summary>Updates the authoritative team time presentation without changing authored layout.</summary>
        public void SetClockPresentation(double remaining, double teamHp, double duration)
        {
            double left = Finite(remaining) ? Math.Max(0d, remaining) : 0d;
            double team = Finite(teamHp) ? Math.Max(0d, teamHp) : 0d;
            if (ClockLabel != null)
                ClockLabel.text = "TIME " + left.ToString("0.0", CultureInfo.InvariantCulture) + "s";
            if (TeamHpLabel != null)
                TeamHpLabel.text = "TEAM HP " + team.ToString("0.0", CultureInfo.InvariantCulture);
            if (teamTimeValue != null)
                teamTimeValue.text = "TEAM HP " + team.ToString("0.0", CultureInfo.InvariantCulture)
                    + "  /  TIME " + left.ToString("0.0", CultureInfo.InvariantCulture) + "s";
            SetHorizontalFill(teamTimeFill, left, duration);
        }

        public void SetNetworkFieldsVisible(bool visible)
        {
            networkFieldsVisible = visible;
            bool showSetupControls = visible && !minimalBattlePresentation;
            if (networkFields != null) networkFields.gameObject.SetActive(showSetupControls);
            if (SoloModeButton != null) SoloModeButton.gameObject.SetActive(showSetupControls);
            if (!useSceneHierarchy && footer != null) Bottom(footer, 0f, 0f, 0f, visible ? 224f : 144f);
        }

        public void SetControls(bool host, bool join, bool start, bool end, bool generate,
            bool debugFixture, bool solo)
        {
            canHost = host; canJoin = join; canStart = start; canEnd = end; canSolo = solo;
            canGenerate = generate; canDebugFixture = debugFixture;
            if (HostButton != null) HostButton.interactable = canHost;
            if (JoinButton != null) JoinButton.interactable = canJoin;
            if (IPv4Input != null) IPv4Input.interactable = canJoin;
            if (PortInput != null) PortInput.interactable = canHost || canJoin;
            if (StartButton != null) StartButton.interactable = canStart;
            if (SoloModeButton != null) SoloModeButton.interactable = canSolo;
            if (EndButton != null) EndButton.interactable = canEnd;
            if (GenerateButton != null) GenerateButton.interactable = canGenerate;
            if (DebugFixtureButton != null) DebugFixtureButton.interactable = canDebugFixture;
        }

        public void Configure(SplitScreenLayout splitLayout)
        {
            Unsubscribe();
            layout = splitLayout;
            if (isActiveAndEnabled)
                Subscribe();
            RefreshRegions();
        }


        /// <summary>Checks persistent bindings without recreating or repositioning authored controls.</summary>
        public bool ValidateSceneHierarchy(out string error)
        {
            if (layout == null) { error = "layout"; return false; }
            if (Canvas == null) { error = "Canvas"; return false; }
            if (SafeArea == null) { error = "SafeArea"; return false; }
            if (HostButton == null) { error = "HostButton"; return false; }
            if (JoinButton == null) { error = "JoinButton"; return false; }
            if (IPv4Input == null) { error = "IPv4Input"; return false; }
            if (PortInput == null) { error = "PortInput"; return false; }
            if (StartButton == null) { error = "StartButton"; return false; }
            if (SoloModeButton == null) { error = "SoloModeButton"; return false; }
            if (RetryButton == null) { error = "RetryButton"; return false; }
            if (LobbyButton == null) { error = "LobbyButton"; return false; }
            if (ResultEndButton == null) { error = "ResultEndButton"; return false; }
            if (ResultOverlay == null) { error = "ResultOverlay"; return false; }
            if (ResultTitle == null) { error = "ResultTitle"; return false; }
            if (ResultSummary == null) { error = "ResultSummary"; return false; }
            if (ResultStatus == null) { error = "ResultStatus"; return false; }
            if (ClockLabel == null) { error = "ClockLabel"; return false; }
            if (TeamHpLabel == null) { error = "TeamHpLabel"; return false; }
            if (PhaseLabel == null) { error = "PhaseLabel"; return false; }
            if (EndButton == null) { error = "EndButton"; return false; }
            if (ActionLabel == null) { error = "ActionLabel"; return false; }
            if (DetailLabel == null) { error = "DetailLabel"; return false; }
            if (HpLabel == null) { error = "HpLabel"; return false; }
            if (ProgressLabel == null) { error = "ProgressLabel"; return false; }
            if (StaminaLabel == null) { error = "StaminaLabel"; return false; }
            if (RecoveryLabel == null) { error = "RecoveryLabel"; return false; }
            if (ResourceModeLabel == null) { error = "ResourceModeLabel"; return false; }
            if (StorageLabel == null) { error = "StorageLabel"; return false; }
            if (GenerateButton == null) { error = "GenerateButton"; return false; }
            if (DebugFixtureButton == null) { error = "DebugFixtureButton"; return false; }
            if (RoundLabel == null) { error = "RoundLabel"; return false; }
            if (ConnectionLabel == null) { error = "ConnectionLabel"; return false; }
            if (topZone == null) { error = "topZone"; return false; }
            if (bottomZone == null) { error = "bottomZone"; return false; }
            if (topContent == null) { error = "topContent"; return false; }
            if (bottomContent == null) { error = "bottomContent"; return false; }
            if (divider == null) { error = "divider"; return false; }
            if (footer == null) { error = "footer"; return false; }
            if (networkFields == null) { error = "networkFields"; return false; }
            if (staminaFill == null) { error = "staminaFill"; return false; }
            if (generateCaption == null) { error = "generateCaption"; return false; }
            if (minimalBattlePresentation)
            {
                if (monsterHpFill == null) { error = "monsterHpFill"; return false; }
                if (teamTimeFill == null) { error = "teamTimeFill"; return false; }
                if (teamTimeValue == null) { error = "teamTimeValue"; return false; }
            }
            if (Canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            { error = "Canvas must use ScreenSpaceOverlay for existing gesture coordinates"; return false; }
            foreach (var button in new[] { HostButton, JoinButton, StartButton, SoloModeButton,
                RetryButton, LobbyButton, ResultEndButton, EndButton, GenerateButton, DebugFixtureButton })
            {
                if (!button.transform.IsChildOf(Canvas.transform))
                { error = button.name + " must remain inside the battle Canvas"; return false; }
            }
            if (SoloModeButton.GetComponentInChildren<Text>(true) == null)
            { error = "SoloModeButton requires its caption Text"; return false; }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>One-time Editor migration. Existing authored UI is validated and never rebuilt.</summary>
        public void PrepareSceneHierarchy()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Prepare UI outside Play mode.");
            if (useSceneHierarchy)
            {
                if (!ValidateSceneHierarchy(out var error)) throw new InvalidOperationException(error);
                return;
            }
            if (Canvas != null || transform.Find("T09Overlay") != null)
                throw new InvalidOperationException("Existing UI found; preserve it instead of rebuilding.");
            if (layout == null) layout = GetComponent<SplitScreenLayout>();
            font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateUI();
            SetNetworkFieldsVisible(false);
            RefreshRegions();
            useSceneHierarchy = true;
            if (!ValidateSceneHierarchy(out var validationError)) throw new InvalidOperationException(validationError);
        }
#endif

        private void Awake()
        {
            // Editor mode only previews a saved hierarchy; never construct legacy UI while editing.
            if (!Application.isPlaying) return;
            if (layout == null)
                layout = GetComponent<SplitScreenLayout>();
            if (useSceneHierarchy)
            {
                if (!ValidateSceneHierarchy(out var error))
                    throw new InvalidOperationException("Editable battle UI has missing/invalid references: " + error);
                ResultOverlay.SetActive(false);
                EnsureEventSystem();
            }
            else
            {
                // Historical diagnostic scenes still construct their original UI.
                font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                CreateUI();
            }
            SetStatus(networkStatus, actionStatus, detailStatus);
            SetControls(canHost, canJoin, canStart, canEnd, canGenerate, canDebugFixture, canSolo);
            SetResources(100d, 100d, 20d, 20d / 3d, 0, 20, false, false);
        }

        private void OnEnable()
        {
            // The saved scene is the authoring source. Runtime-only responsive layout must not
            // rewrite a designer's RectTransforms when the Editor Game view changes size.
            if (!Application.isPlaying) return;
            Subscribe();
            RefreshRegions();
        }

        private void Start()
        {
            if (Application.isPlaying) RefreshRegions();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
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
            var canvasRect = Rect("T09Overlay", transform);
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

            CreateResultOverlay(canvasRect);

            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var events = new GameObject("T09EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        private void CreateUpperHud()
        {
            var header = Rect("Header", topContent);
            Top(header, 20f, 20f, 16f, 56f);
            var brand = Text("Brand", header, "HAPIO", 27, White, TextAnchor.UpperLeft);
            Top(brand.rectTransform, 0f, 128f, 0f, 33f);
            PhaseLabel = Text("BattlePhase", header, "T09 / BOOT / 2 PLAYERS", 10, Muted, TextAnchor.LowerLeft);
            Top(PhaseLabel.rectTransform, 1f, 0f, 34f, 16f);

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

            ClockLabel = Text("BattleClock", topContent, "TIME 180.0s", 10, White, TextAnchor.MiddleLeft);
            Top(ClockLabel.rectTransform, 20f, 248f, 160f, 16f);
            TeamHpLabel = Text("TeamHp", topContent, "TEAM HP 180.0", 10, White, TextAnchor.MiddleCenter);
            Top(TeamHpLabel.rectTransform, 139f, 125f, 160f, 16f);
            ResourceModeLabel = Text("ResourceMode", topContent, "NORMAL / EMPTY START", 9, Muted, TextAnchor.MiddleCenter);
            Bottom(ResourceModeLabel.rectTransform, 20f, 20f, 33f, 14f);
            StorageLabel = Text("PersonalStorage", topContent, "ORBS 0 / 20", 10, White, TextAnchor.MiddleRight);
            Top(StorageLabel.rectTransform, 250f, 20f, 160f, 16f);
            RoundLabel = Text("ResourceProgress", topContent, "ROUND 0  /  RESETS 0", 10, Muted, TextAnchor.MiddleCenter);
            Bottom(RoundLabel.rectTransform, 20f, 20f, 12f, 18f);
        }

        private void CreateLowerHud()
        {
            footer = Image("ResourceControls", bottomContent, Ink).rectTransform;
            Bottom(footer, 0f, 0f, 0f, networkFieldsVisible ? 224f : 144f);
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
            DebugFixtureButton = CreateButton("DebugFixtureButton", resourceButtons, "DEBUG\nMIXED RAW", .68f, 1f);
            var soloRow = Rect("DevelopmentSoloControl", footer);
            Bottom(soloRow, 20f, 20f, 190f, 26f);
            SoloModeButton = CreateButton("DevelopmentSoloButton", soloRow, "DEV SOLO: OFF / TWO PLAYERS", 0f, 1f);
            SoloModeButton.gameObject.SetActive(networkFieldsVisible);
            var buttons = Rect("SessionButtons", footer);
            Bottom(buttons, 12f, 12f, 6f, 31f);
            HostButton = CreateButton("DevHostButton", buttons, "HOST", 0f, .21f);
            JoinButton = CreateButton("JoinButton", buttons, "JOIN", .21f, .42f);
            StartButton = CreateButton("HostStartButton", buttons, "HOST START", .42f, .79f);
            EndButton = CreateButton("EndTestButton", buttons, "END", .79f, 1f);
        }

        private void CreateResultOverlay(RectTransform canvasRect)
        {
            var overlay = Image("ConfirmedBattleResult", canvasRect, new Color(.025f, .055f, .07f, .94f));
            overlay.raycastTarget = true;
            ResultOverlay = overlay.gameObject;
            var safe = Rect("ResultSafeArea", overlay.transform);
            safe.gameObject.AddComponent<UISafeArea>();
            var panel = Image("ResultPanel", safe, new Color(.055f, .12f, .16f, .99f)).rectTransform;
            panel.anchorMin = new Vector2(.07f, .20f);
            panel.anchorMax = new Vector2(.93f, .80f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            ResultTitle = Text("ResultTitle", panel, "VICTORY", 30, Teal, TextAnchor.MiddleCenter);
            Top(ResultTitle.rectTransform, 14f, 14f, 24f, 44f);
            var caption = Text("ResultCaption", panel, "CONFIRMED BY HOST", 10, Muted, TextAnchor.MiddleCenter);
            Top(caption.rectTransform, 14f, 14f, 73f, 18f);
            ResultSummary = Text("FinalBattleValues", panel, "", 17, White, TextAnchor.MiddleCenter);
            Top(ResultSummary.rectTransform, 18f, 18f, 99f, 124f);
            ResultStatus = Text("ResultNextStep", panel, "", 11, Muted, TextAnchor.MiddleCenter);
            Bottom(ResultStatus.rectTransform, 16f, 16f, 73f, 48f);
            var buttons = Rect("ResultActions", panel);
            Bottom(buttons, 14f, 14f, 21f, 40f);
            RetryButton = CreateButton("HostRetry", buttons, "RETRY", 0f, .34f);
            LobbyButton = CreateButton("HostLobby", buttons, "LOBBY", .34f, .68f);
            ResultEndButton = CreateButton("CloseSession", buttons, "CLOSE", .68f, 1f);
            ResultOverlay.SetActive(false);
        }

        public void SetBattle(string phase, double remaining, double teamHp, double duration,
            int participants, bool developmentSolo, bool shortDuration, bool canChooseSolo,
            int observedMonsterHp, double stamina, bool canHostResultActions)
        {
            double left = Finite(remaining) ? Math.Max(0, remaining) : 0;
            double team = Finite(teamHp) ? Math.Max(0, teamHp) : 0;
            SetClockPresentation(left, team, duration);
            string mode = developmentSolo ? "DEV SOLO" : participants + " / " + ParticipantCapacity;
            string shortLabel = shortDuration ? " / SHORT " + Number(duration) + "s" : "";
            if (PhaseLabel != null) PhaseLabel.text = (CoordinatedGame ? "C6 / " : "T09 / ") + (phase ?? "Boot").ToUpperInvariant() + " / " + mode + shortLabel;
            if (SoloModeButton != null)
            {
                SoloModeButton.interactable = canChooseSolo && canSolo;
                SoloModeButton.GetComponentInChildren<Text>().text = developmentSolo
                    ? "DEV SOLO: ON / ONE-DEVICE TEST" : "DEV SOLO: OFF / TWO PLAYERS";
            }
            bool terminal = phase == "Victory" || phase == "Defeat";
            if (ResultOverlay == null) return;
            ResultOverlay.SetActive(terminal);
            if (!terminal) return;
            ResultTitle.text = phase == "Victory" ? "VICTORY" : "DEFEAT";
            ResultTitle.color = phase == "Victory" ? Teal : Gold;
            ResultSummary.text = "MONSTER HP   " + observedMonsterHp + " / " + lastMonsterMaximum
                + "\nTEAM HP   " + team.ToString("0.0", CultureInfo.InvariantCulture)
                + "\nTIME LEFT   " + left.ToString("0.0", CultureInfo.InvariantCulture) + "s"
                + "\nYOUR STAMINA   " + Number(stamina) + " / " + Number(lastStaminaMaximum);
            ResultStatus.text = canHostResultActions
                ? "Retry returns to Ready. Press Host Start to begin again."
                : "Waiting for Host to Retry or return to Lobby.";
            RetryButton.gameObject.SetActive(canHostResultActions);
            LobbyButton.gameObject.SetActive(canHostResultActions);
            RetryButton.interactable = LobbyButton.interactable = canHostResultActions;
            // Scene-authored button positions are owned by the designer, including Client results.
            if (!useSceneHierarchy)
            {
                var closeRect = ResultEndButton.GetComponent<RectTransform>();
                closeRect.anchorMin = new Vector2(canHostResultActions ? .68f : 0f, 0f);
                closeRect.anchorMax = Vector2.one;
                closeRect.offsetMin = new Vector2(canHostResultActions ? 4f : 0f, 0f);
                closeRect.offsetMax = Vector2.zero;
            }
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
