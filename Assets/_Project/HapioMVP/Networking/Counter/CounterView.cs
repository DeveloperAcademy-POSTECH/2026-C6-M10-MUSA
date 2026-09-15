using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.Networking.Counter
{
    /// <summary>Development UI for the shared counter, using the existing connection screen.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DirectConnectionSession), typeof(DirectConnectionView), typeof(CounterSync))]
    public sealed class CounterView : MonoBehaviour
    {
        public const int DevelopmentBatchSize = 50;
        private const float DevelopmentBatchIntervalSeconds = 0.02f;
        private static readonly Color SecondaryColor = new Color(0.7f, 0.78f, 0.87f, 1f);
        private DirectConnectionView connectionView;
        private Font font;
        private RectTransform panel;
        private Text batchProgressLabel;
        private Text statusLabel;
        private Coroutine batchRoutine;
        private string batchSessionId;
        private int batchSent;
        private bool sendingBatchRequest;
        private string deferredStopReason;
        private string batchProgress = "DEV batch is optional. It sends automated requests.";
        private bool subscribed;

        public CounterSync Sync { get; private set; }
        public Text NumberLabel { get; private set; }
        public Text StatsLabel { get; private set; }
        public Text SessionLabel { get; private set; }
        public Button RequestButton { get; private set; }
        public Button BatchButton { get; private set; }
        public Button ReplayButton { get; private set; }
        public bool IsBatchRunning { get; private set; }

        private void Awake()
        {
            Sync = GetComponent<CounterSync>();
            connectionView = GetComponent<DirectConnectionView>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Start()
        {
            // DirectConnectionView builds its Canvas, Safe Area and ScrollRect in Awake.
            // Start avoids depending on the relative Awake order of these components.
            if (panel == null)
                CreateUI();
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopBatch("view disabled");
        }

        private void OnDestroy()
        {
            Unsubscribe();
            StopBatch("view destroyed");
            if (RequestButton != null)
                RequestButton.onClick.RemoveListener(RequestOne);
            if (BatchButton != null)
                BatchButton.onClick.RemoveListener(StartBatch);
            if (ReplayButton != null)
                ReplayButton.onClick.RemoveListener(ReplayLast);
        }

        private void Subscribe()
        {
            if (Sync != null && !subscribed)
            {
                Sync.Changed += Refresh;
                subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (Sync != null && subscribed)
                Sync.Changed -= Refresh;
            subscribed = false;
        }

        private void RequestOne()
        {
            if (isActiveAndEnabled && !IsBatchRunning && Sync.IsReady)
                Sync.RequestIncrement();
            Refresh();
        }

        private void ReplayLast()
        {
            if (isActiveAndEnabled && !IsBatchRunning && Sync.IsReady)
                Sync.ResendLastRequest();
            Refresh();
        }

        private void StartBatch()
        {
            if (!isActiveAndEnabled || IsBatchRunning || !Sync.IsReady)
                return;

            batchSent = 0;
            deferredStopReason = null;
            batchSessionId = Sync.Snapshot.SessionId;
            IsBatchRunning = true;
            batchProgress = $"DEV automated requests: 0 / {DevelopmentBatchSize} sent";
            // The coroutine yields before sending so its handle exists if a synchronous
            // connection change asks Refresh to stop the batch during RequestIncrement.
            batchRoutine = StartCoroutine(SendBatch());
            Refresh();
        }

        private IEnumerator SendBatch()
        {
            yield return null;
            var interval = new WaitForSecondsRealtime(DevelopmentBatchIntervalSeconds);
            while (batchSent < DevelopmentBatchSize)
            {
                if (!Sync.IsReady || !SameBatchSession())
                {
                    FinishBatch("connection or session changed");
                    yield break;
                }

                // RequestIncrement can notify listeners synchronously. Defer a stop
                // until its result is known so the displayed sent count remains exact.
                bool sent;
                sendingBatchRequest = true;
                try
                {
                    sent = Sync.RequestIncrement();
                }
                finally
                {
                    sendingBatchRequest = false;
                }
                if (sent)
                    batchSent++;
                if (deferredStopReason != null || !sent)
                {
                    FinishBatch(deferredStopReason ?? "request was not sent");
                    yield break;
                }

                batchProgress = $"DEV automated requests: {batchSent} / {DevelopmentBatchSize} sent";
                Refresh();
                if (!IsBatchRunning)
                    yield break;
                yield return interval;
            }

            FinishBatch(null);
        }

        private bool SameBatchSession() =>
            string.Equals(Sync.Snapshot.SessionId, batchSessionId, StringComparison.Ordinal);

        private void FinishBatch(string stoppedReason)
        {
            IsBatchRunning = false;
            batchRoutine = null;
            batchProgress = stoppedReason == null
                ? $"DEV batch complete: {batchSent} / {DevelopmentBatchSize} requests sent.\nCheck acknowledgements below."
                : $"DEV batch stopped: {batchSent} / {DevelopmentBatchSize} requests sent ({stoppedReason}).";
            Refresh();
        }

        private void StopBatch(string reason)
        {
            if (!IsBatchRunning)
                return;
            if (sendingBatchRequest)
            {
                deferredStopReason = reason;
                return;
            }
            if (batchRoutine != null)
                StopCoroutine(batchRoutine);
            FinishBatch(reason);
        }

        private void Refresh()
        {
            if (Sync == null || panel == null)
                return;

            if (IsBatchRunning && (!Sync.IsReady || !SameBatchSession()))
                StopBatch("connection or session changed");

            var snapshot = Sync.Snapshot;
            string sessionId = snapshot.SessionId;
            SessionLabel.text = string.IsNullOrEmpty(sessionId)
                ? "Session: waiting for host"
                : $"Session: {sessionId.Substring(0, Math.Min(12, sessionId.Length))}";
            NumberLabel.text = snapshot.Value.ToString(CultureInfo.InvariantCulture);
            StatsLabel.text = $"Host approved: {snapshot.ApprovedCount}   Rejected: {snapshot.RejectedCount}\n" +
                              $"Revision: {snapshot.Revision}\n" +
                              $"Local sent: {Sync.SentCount}   Acknowledged: {Sync.AcknowledgedCount}\n" +
                              $"Pending: {Sync.PendingCount}   Duplicate receipts: {Sync.DuplicateReceiptCount}";
            statusLabel.text = $"Counter state: {Sync.Status}";
            batchProgressLabel.text = batchProgress;
            bool canRequest = isActiveAndEnabled && Sync.IsReady && !IsBatchRunning;
            RequestButton.interactable = canRequest;
            BatchButton.interactable = canRequest;
            ReplayButton.interactable = canRequest && Sync.SentCount > 0;
        }

        private void CreateUI()
        {
            if (connectionView == null || connectionView.Scroll == null || connectionView.Scroll.content == null)
                throw new InvalidOperationException("CounterView requires the existing connection ScrollRect.");

            connectionView.BuildLabel.text = "C6-T03\nShared counter";
            Debug.Log($"C6_T03_DEVICE model={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} ipv4=[{string.Join(",", DirectConnectionSession.GetLocalIPv4Addresses())}] screen={Screen.width}x{Screen.height} safeArea={Screen.safeArea}");
            panel = Rect("CounterPanel", connectionView.Scroll.content);
            panel.SetSiblingIndex(connectionView.BuildLabel.transform.GetSiblingIndex() + 1);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var caption = Label("CounterCaption", panel, 18, 26);
            caption.text = "Shared value (host approved)";
            caption.color = SecondaryColor;
            NumberLabel = Label("SharedNumber", panel, 42, 56);
            SessionLabel = Label("CounterSession", panel, 17, 24);
            SessionLabel.color = SecondaryColor;
            RequestButton = MakeButton("IncrementButton", panel, "+1", 26, 54, false);
            RequestButton.onClick.AddListener(RequestOne);
            BatchButton = MakeButton("BatchButton", panel, "DEV: Send 50 requests", 20, 50, true);
            BatchButton.onClick.AddListener(StartBatch);
            ReplayButton = MakeButton("ReplayButton", panel, "DEV: Replay last request", 18, 46, true);
            ReplayButton.onClick.AddListener(ReplayLast);
            batchProgressLabel = Label("BatchProgress", panel, 16, 44);
            batchProgressLabel.color = SecondaryColor;
            StatsLabel = Label("CounterStats", panel, 17, 94);
            statusLabel = Label("CounterStatus", panel, 17, 44);
            statusLabel.color = SecondaryColor;
        }

        private Text Label(string objectName, Transform parent, int size, float minHeight)
        {
            var text = MakeText(objectName, parent, size);
            var layout = text.gameObject.AddComponent<LayoutElement>();
            // Text preferred height may exceed the minimum on a narrow screen. Let the
            // existing ScrollRect grow instead of clipping stats or overlapping rows.
            layout.minHeight = minHeight;
            return text;
        }

        private Button MakeButton(string objectName, Transform parent, string caption, int size,
            float minHeight, bool developmentControl)
        {
            var rect = Rect(objectName, parent);
            var height = rect.gameObject.AddComponent<LayoutElement>();
            height.minHeight = minHeight;
            height.preferredHeight = minHeight;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = developmentControl
                ? new Color(0.21f, 0.27f, 0.36f, 1f)
                : new Color(0.12f, 0.42f, 0.77f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.65f, 0.73f, 0.86f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            var text = MakeText("Label", rect, size);
            text.text = caption;
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.offsetMin = new Vector2(8f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            return button;
        }

        private Text MakeText(string objectName, Transform parent, int size)
        {
            var rect = Rect(objectName, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.supportRichText = false;
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
    }
}
