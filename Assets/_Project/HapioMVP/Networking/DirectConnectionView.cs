using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace C6.Prototype.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DirectConnectionSession))]
    public sealed class DirectConnectionView : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0.045f, 0.065f, 0.1f, 1f);
        private static readonly Color SecondaryColor = new Color(0.7f, 0.78f, 0.87f, 1f);
        private Font font;
        private bool subscribed;

        public DirectConnectionSession Session { get; private set; }
        public RectTransform SafeArea { get; private set; }
        public Canvas Canvas { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public Text BuildLabel { get; private set; }
        public Text HostAddressesLabel { get; private set; }
        public InputField AddressInput { get; private set; }
        public InputField PortInput { get; private set; }
        public Button HostButton { get; private set; }
        public Button JoinButton { get; private set; }
        public Button StopButton { get; private set; }
        public Text StopButtonLabel { get; private set; }
        public Text StateLabel { get; private set; }
        public Text RoleLabel { get; private set; }
        public Text ParticipantsLabel { get; private set; }
        public Text MessageLabel { get; private set; }
        public Text GuidanceLabel { get; private set; }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Session = GetComponent<DirectConnectionSession>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateUI();
            EnsureEventSystem();
            Refresh();
        }

        private void Start() => Refresh();

        private void OnEnable()
        {
            if (Session != null && !subscribed)
            {
                Session.Changed += Refresh;
                subscribed = true;
                Refresh();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (HostButton != null)
                HostButton.onClick.RemoveListener(StartHost);
            if (JoinButton != null)
                JoinButton.onClick.RemoveListener(Join);
            if (StopButton != null)
                StopButton.onClick.RemoveListener(Stop);
        }

        private void Unsubscribe()
        {
            if (Session != null && subscribed)
                Session.Changed -= Refresh;
            subscribed = false;
        }

        private void StartHost()
        {
            AddressInput.DeactivateInputField();
            PortInput.DeactivateInputField();
            Session.StartHost(PortInput.text);
            Refresh();
        }

        private void Join()
        {
            AddressInput.DeactivateInputField();
            PortInput.DeactivateInputField();
            Session.Join(AddressInput.text, PortInput.text);
            Refresh();
        }

        private void Stop()
        {
            Session.Stop();
            Refresh();
        }

        private void Refresh()
        {
            if (StateLabel == null || Session == null)
                return;

            HostButton.interactable = Session.CanStart;
            JoinButton.interactable = Session.CanStart;
            AddressInput.interactable = Session.CanStart;
            PortInput.interactable = Session.CanStart;
            bool starting = Session.State == DirectConnectionState.StartingHost ||
                            Session.State == DirectConnectionState.Connecting;
            StopButton.interactable = starting || Session.State == DirectConnectionState.Connected;
            StopButtonLabel.text = starting ? "Cancel" : "End connection";

            StateLabel.text = $"State: {StateText(Session.State)}";
            StateLabel.color = Session.State == DirectConnectionState.Failed
                ? new Color(1f, 0.69f, 0.49f, 1f)
                : Color.white;
            string localId = Session.LocalClientId.HasValue
                ? Session.LocalClientId.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
            RoleLabel.text = $"Role: {Session.Role}  |  My ID: {localId}";

            var participants = Session.ParticipantIds;
            string participantIds = participants.Count == 0
                ? "-"
                : string.Join(", ", participants.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            ParticipantsLabel.text = $"Participants: {participants.Count} / 2\nIDs: {participantIds}";
            MessageLabel.text = Session.Message;

            var addresses = DirectConnectionSession.GetLocalIPv4Addresses();
            HostAddressesLabel.text = addresses.Length == 0
                ? "This device IPv4: unavailable\nCheck Wi-Fi settings for the host address."
                : $"This device IPv4:\n{string.Join("  /  ", addresses)}";

            GuidanceLabel.text = Session.State == DirectConnectionState.Failed
                ? "Connection checks\n1. Confirm host IPv4 and port.\n2. Put both devices on the same Wi-Fi.\n3. Check router client isolation.\n4. Check iOS Local Network permission.\nCorrect the issue, then tap Host or Join again."
                : "Use the same Wi-Fi on both devices.\nOn one device, tap Host. On the other, enter the host IPv4 and matching port, then Join.\nIf there are several addresses, use the host's Wi-Fi address.";
        }

        private static string StateText(DirectConnectionState state)
        {
            switch (state)
            {
                case DirectConnectionState.StartingHost: return "Starting host";
                case DirectConnectionState.Connecting: return "Connecting";
                case DirectConnectionState.Connected: return "Connected";
                case DirectConnectionState.Stopping: return "Stopping";
                case DirectConnectionState.Failed: return "Failed";
                default: return "Ready";
            }
        }

        private void CreateUI()
        {
            var canvasRect = Rect("DirectConnectionCanvas", transform);
            Canvas = canvasRect.gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();

            var background = Rect("Background", canvasRect);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = BackgroundColor;
            backgroundImage.raycastTarget = false;

            SafeArea = Rect("SafeArea", canvasRect);
            SafeArea.gameObject.AddComponent<UISafeArea>();

            var scrollRoot = Rect("Scroll", SafeArea);
            Scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.scrollSensitivity = 24f;

            var viewport = Rect("Viewport", scrollRoot);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            Scroll.viewport = viewport;

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 28);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Scroll.content = content;

            BuildLabel = Label("BuildId", content, 27, 66);
            BuildLabel.text = "C6-T02\nDirect IP connection";
            HostAddressesLabel = Label("HostAddresses", content, 18, 62);
            HostAddressesLabel.color = SecondaryColor;

            var addressCaption = Label("HostAddressCaption", content, 17, 22);
            addressCaption.text = "Host IPv4";
            AddressInput = Input("HostIPv4", content, "e.g. 192.168.1.10", 64);
            AddressInput.contentType = InputField.ContentType.Standard;
            AddressInput.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;

            var portCaption = Label("PortCaption", content, 17, 22);
            portCaption.text = "Port";
            PortInput = Input("Port", content, "7777", 5);
            PortInput.contentType = InputField.ContentType.IntegerNumber;
            PortInput.keyboardType = TouchScreenKeyboardType.NumberPad;
            PortInput.text = DirectConnectionSession.DefaultPort.ToString(CultureInfo.InvariantCulture);

            var startRow = Rect("StartButtons", content);
            PreferredHeight(startRow, 54);
            var rowLayout = startRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = true;
            HostButton = Button("HostButton", startRow, "Host", out _);
            JoinButton = Button("JoinButton", startRow, "Join", out _);
            StopButton = Button("StopButton", content, "End connection", out var stopLabel);
            StopButtonLabel = stopLabel;
            PreferredHeight(StopButton.GetComponent<RectTransform>(), 48);
            StopButton.targetGraphic.color = new Color(0.21f, 0.27f, 0.36f, 1f);
            HostButton.onClick.AddListener(StartHost);
            JoinButton.onClick.AddListener(Join);
            StopButton.onClick.AddListener(Stop);

            StateLabel = Label("State", content, 24, 34);
            RoleLabel = Label("Role", content, 18, 26);
            ParticipantsLabel = Label("Participants", content, 20, 54);
            MessageLabel = Label("Message", content, 17, 62);
            MessageLabel.color = SecondaryColor;
            GuidanceLabel = Label("Guidance", content, 16, 168);
            GuidanceLabel.color = SecondaryColor;
            GuidanceLabel.alignment = TextAnchor.UpperLeft;
        }

        private InputField Input(string objectName, Transform parent, string hint, int characterLimit)
        {
            var rect = Rect(objectName, parent);
            PreferredHeight(rect, 50);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.11f, 0.15f, 0.21f, 1f);
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;
            input.shouldHideMobileInput = false;

            var text = Text("Text", rect, 21);
            Pad(text.rectTransform, 12f, 5f);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            input.textComponent = text;
            var placeholder = Text("Placeholder", rect, 19);
            Pad(placeholder.rectTransform, 12f, 5f);
            placeholder.color = SecondaryColor;
            placeholder.text = hint;
            input.placeholder = placeholder;
            return input;
        }

        private Button Button(string objectName, Transform parent, string caption, out Text label)
        {
            var rect = Rect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.42f, 0.77f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.65f, 0.73f, 0.86f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            label = Text("Label", rect, 23);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = caption;
            return button;
        }

        private Text Label(string objectName, Transform parent, int size, float height)
        {
            var text = Text(objectName, parent, size);
            // Keep enough room for ordinary labels, while allowing long errors or
            // several local addresses to grow instead of overlapping the next row.
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            return text;
        }

        private Text Text(string objectName, Transform parent, int size)
        {
            var rect = Rect(objectName, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RectTransform Rect(string objectName, Transform parent)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Pad(RectTransform rect, float horizontal, float vertical)
        {
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void PreferredHeight(RectTransform rect, float height)
        {
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private void EnsureEventSystem()
        {
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventObject = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
                eventSystem = eventObject.GetComponent<EventSystem>();
            }

            // Reuse a scene-owned EventSystem. Its lifetime belongs to that scene.
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
