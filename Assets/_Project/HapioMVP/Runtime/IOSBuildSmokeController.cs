using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace C6.Prototype
{
    [DisallowMultipleComponent]
    public sealed class IOSBuildSmokeController : MonoBehaviour
    {
        [SerializeField] private IOSBuildStamp buildStamp;

        public IOSBuildStamp BuildStamp => buildStamp;
        public int Count { get; private set; }
        public Text BuildLabel { get; private set; }
        public Text CounterLabel { get; private set; }
        public Button TapButton { get; private set; }
        public RectTransform SafeArea { get; private set; }

        public void Configure(IOSBuildStamp stamp)
        {
            buildStamp = stamp;
            RefreshLabels();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Count = 0;
            CreateUI();
            EnsureEventSystem();
            RefreshLabels();
            Debug.Log($"C6_T01_SMOKE_READY build={buildStamp?.BuildId} revision={buildStamp?.Revision} count={Count}");
        }

        private void OnDestroy()
        {
            if (TapButton != null)
                TapButton.onClick.RemoveListener(IncrementCount);
        }

        private void IncrementCount()
        {
            Count++;
            RefreshLabels();
            Debug.Log($"C6_T01_TAP build={buildStamp?.BuildId} count={Count}");
        }

        private void RefreshLabels()
        {
            if (BuildLabel != null)
                BuildLabel.text = buildStamp != null
                    ? $"Build {buildStamp.BuildId}\nRevision {buildStamp.Revision}"
                    : "Build NOT CONFIGURED";
            if (CounterLabel != null)
                CounterLabel.text = $"Clicks: {Count}";
        }

        private void CreateUI()
        {
            var canvasObject = new GameObject("SmokeCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateRect("Background", canvasObject.transform, Vector2.zero, Vector2.one);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.045f, 0.065f, 0.1f, 1f);
            backgroundImage.raycastTarget = false;

            SafeArea = CreateRect("SafeArea", canvasObject.transform, Vector2.zero, Vector2.one);
            SafeArea.gameObject.AddComponent<UISafeArea>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildLabel = CreateText("BuildId", SafeArea, font, 26,
                new Vector2(0.08f, 0.7f), new Vector2(0.92f, 0.88f));
            BuildLabel.color = new Color(0.8f, 0.87f, 0.96f, 1f);
            CounterLabel = CreateText("ClickCount", SafeArea, font, 38,
                new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.66f));

            var buttonRect = CreateRect("TapButton", SafeArea,
                new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.4f));
            var buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(0.12f, 0.42f, 0.77f, 1f);
            TapButton = buttonRect.gameObject.AddComponent<Button>();
            TapButton.targetGraphic = buttonImage;
            var colors = TapButton.colors;
            colors.pressedColor = new Color(0.6f, 0.7f, 0.82f, 1f);
            TapButton.colors = colors;
            TapButton.onClick.AddListener(IncrementCount);
            var buttonText = CreateText("Label", buttonRect, font, 27, Vector2.zero, Vector2.one);
            buttonText.text = "Tap +1";
        }

        private void EnsureEventSystem()
        {
            // The smoke scene owns one EventSystem. Reuse one when this component is
            // instantiated by an Editor test, instead of creating a second input loop.
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventObject = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
                eventSystem = eventObject.GetComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private static RectTransform CreateRect(string objectName, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text CreateText(string objectName, Transform parent, Font font, int size,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = CreateRect(objectName, parent, anchorMin, anchorMax);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = size;
            return label;
        }
    }
}
