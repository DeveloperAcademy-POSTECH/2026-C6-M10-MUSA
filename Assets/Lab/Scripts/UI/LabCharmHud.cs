using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 매혹 (#251): 구미호에게 홀린 사람 자신의 화면. 남은 시간을 북 미니맵 바로 위의 세로 게이지(임시)로 보여 주고,
    /// 풀리면 "정신 차림!"을 잠깐 띄운다. 매혹 예고 (#305): 걸리기 전 예고 동안에는 모든 화면의 같은 자리에
    /// "구미호가 P2에게 매혹을 걸려고 한다!"(걸릴 사람 자신에게는 "나에게")를 띄운다. 화면 표시만 하며 터치를 막지 않는다.
    /// 소리 (#351): 매혹이 걸리고 풀리는 소리는 LabGumihoSound가 낸다. 이 화면은 내 매혹이 풀린 순간만 알린다(CharmReleased).
    /// 안내 (#328): 홀린 동안에는 같은 자리에 "옆 사람이 풀어 줄 때까지 기다려야 한다..."를 띄운다 (풀어 줄 옆 사람이 있을 때).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabCharmHud : MonoBehaviour
    {
        public const string AwakeText = "정신 차림!";
        public const string WaitText = "옆 사람이 풀어 줄 때까지\n기다려야 한다...";
        public static string WarningText(string who) => "요괴가 " + who + "에게\n매혹을 걸려고 한다!";
        private const float WarningPopSeconds = .2f, WarningPulseHz = 2f;
        private const float AwakeSeconds = 1.2f;
        private static readonly Color GaugeBackColor = new Color(.17f, .14f, .12f, .55f);

        [Tooltip("게이지 크기 (Canvas 기준 픽셀)")]
        [SerializeField] private Vector2 gaugeSize = new Vector2(16f, 140f);
        [Tooltip("게이지 맨 아래와 북 미니맵 맨 위 사이 (Canvas 기준 픽셀)")]
        [SerializeField, Min(0f)] private float gaugeGap = 10f;
        [Tooltip("미니맵이 없을 때 게이지의 자리 (Canvas 비율)와 화면 오른쪽 끝에서 떨어진 거리 (픽셀)")]
        [SerializeField] private Vector2 fallbackAnchor = new Vector2(1f, .5f);
        [SerializeField, Min(0f)] private float fallbackMargin = 18f;
        [Tooltip("\"정신 차림!\"이 뜨는 곳 (Canvas 비율)")]
        [SerializeField] private Vector2 awakeAnchor = new Vector2(.5f, .6f);
        [Tooltip("\"정신 차림!\" 글자 크기 (Canvas 기준 픽셀)")]
        [SerializeField, Min(10f)] private float awakeSize = 30f;
        [Tooltip("매혹 예고 문구가 뜨는 곳 (Canvas 비율: x는 왼쪽 0 ~ 오른쪽 1, y는 아래 0 ~ 위 1). 기본은 \"정신 차림!\" 자리 (#305).")]
        [SerializeField] private Vector2 warningAnchor = new Vector2(.5f, .6f);
        [Tooltip("매혹 예고 문구 글자 크기 (Canvas 기준 픽셀). 두 줄로 뜬다 (#305). Play 중에 바꾸면 바로 보인다.")]
        [SerializeField, Min(10f)] private float warningSize = 20f;

        private static readonly Vector3[] Corners = new Vector3[4];
        private LabNetwork network;
        private LabDrumMinimap minimap;
        private Canvas canvas;
        private Font font;
        private RectTransform layer;
        private RectTransform gauge;
        private RectTransform gaugeFill;
        private Text awake;
        private Text warning;
        private float warningAge = -1f;
        private LabGuideText waitGuide;
        private float awakeLeft = -1f;
        private bool wasCharmed;
        private double seenServerTime = -1d;
        private float seenAt;

        /// <summary>The charm's time-left gauge is showing on this screen.</summary>
        public bool GaugeVisible => gauge != null && gauge.gameObject.activeInHierarchy;
        public RectTransform Gauge => gauge;
        /// <summary>How full the gauge is, 1 as the charm begins and 0 as it ends.</summary>
        public float GaugeFill => gaugeFill != null ? gaugeFill.anchorMax.y : 0f;
        public bool AwakeShowing => awake != null && awake.gameObject.activeInHierarchy;
        /// <summary>The charm warning showing on this screen (#305), and what it says.</summary>
        public bool WarningShowing => warning != null && warning.gameObject.activeInHierarchy;
        public string WarningShown => warning != null ? warning.text : string.Empty;
        /// <summary>The charmed player's own guide (#328): wait for the players beside.</summary>
        public bool WaitShowing => waitGuide != null && waitGuide.Showing;
        public string WaitShown => waitGuide != null ? waitGuide.Shown : string.Empty;
        /// <summary>
        /// 내게 걸린 매혹이 전투 중에 풀렸다 (#351): 양옆이 다 눌렀거나, 시간이 다 되었거나, 피버타임이 시작되었다.
        /// 라운드가 끝나 사라질 때는 알리지 않는다.
        /// </summary>
        public event System.Action CharmReleased;

        /// <summary>스냅샷을 읽을 네트워크와, 게이지를 그릴 Canvas를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            if (snapshot == null || snapshot.phase != LabPhase.Playing)
            {
                wasCharmed = false;
                awakeLeft = -1f;
                if (layer != null) layer.gameObject.SetActive(false);
                return;
            }
            if (snapshot.paused)
            {
                if (layer != null) layer.gameObject.SetActive(false);
                return;
            }
            double now = ServerNow(snapshot);
            bool charmed = snapshot.charmedPlayer == network.LocalPlayerId
                && snapshot.charmUntil > now && now >= snapshot.charmStartedAt;
            bool warned = snapshot.CharmWarningAt(now);
            if (wasCharmed && !charmed)
            {
                awakeLeft = AwakeSeconds;
                CharmReleased?.Invoke();
            }
            wasCharmed = charmed;
            if (awakeLeft >= 0f) awakeLeft -= Time.deltaTime;
            if (!charmed && awakeLeft < 0f && !warned)
            {
                warningAge = -1f;
                if (layer != null) layer.gameObject.SetActive(false);
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();

            gauge.gameObject.SetActive(charmed);
            if (charmed)
            {
                PlaceGauge();
                double total = snapshot.charmUntil - snapshot.charmStartedAt;
                float left = total > 0d ? Mathf.Clamp01((float)((snapshot.charmUntil - now) / total)) : 0f;
                gaugeFill.anchorMax = new Vector2(1f, left);
            }
            DrawAwake();
            DrawWarning(snapshot, warned);
            // Only when someone beside can wake them: solo, or with rescue taps off, the charm just wears off.
            bool rescuable = network.config != null && network.config.CharmRescueTaps > 0
                && snapshot.charmHelpers != null && snapshot.charmHelpers.Length > 0;
            waitGuide.Draw(charmed && rescuable, WaitText);
        }

        // 매혹 예고 (#305): 걸릴 사람이 정해지면 모두에게 알린다. 톡 튀어나와 살짝 두근거리다가 매혹이 시작되면 사라진다.
        private void DrawWarning(LabSnapshot snapshot, bool warned)
        {
            warning.gameObject.SetActive(warned);
            if (!warned)
            {
                warningAge = -1f;
                return;
            }
            warningAge = warningAge < 0f ? 0f : warningAge + Time.deltaTime;
            LabPlayerState target = System.Array.Find(snapshot.players ?? new LabPlayerState[0],
                player => player != null && player.id == snapshot.charmedPlayer);
            warning.text = WarningText(snapshot.charmedPlayer == network.LocalPlayerId ? "나" : LabResultStats.PlayerName(target));
            PlaceWarning();
            float pop = warningAge < WarningPopSeconds ? Mathf.Lerp(.5f, 1f, warningAge / WarningPopSeconds)
                : 1f + .04f * Mathf.Sin((warningAge - WarningPopSeconds) * WarningPulseHz * 2f * Mathf.PI);
            warning.rectTransform.localScale = Vector3.one * pop;
        }

        // 게이지 맨 아래를 북 미니맵 바로 위에 둔다. 미니맵은 움직일 수 있으니 매번 맞춘다.
        private void PlaceGauge()
        {
            if (minimap == null) minimap = GetComponent<LabDrumMinimap>();
            RectTransform below = minimap != null && minimap.IsShowing ? minimap.Panel : null;
            if (below == null)
            {
                gauge.anchorMin = gauge.anchorMax = fallbackAnchor;
                gauge.pivot = new Vector2(1f, .5f);
                gauge.anchoredPosition = new Vector2(-fallbackMargin, 0f);
                return;
            }
            below.GetWorldCorners(Corners);
            Vector3 top = layer.InverseTransformPoint((Corners[1] + Corners[2]) * .5f);
            gauge.anchorMin = gauge.anchorMax = new Vector2(.5f, .5f);
            gauge.pivot = new Vector2(.5f, 0f);
            gauge.anchoredPosition = new Vector2(top.x, top.y + gaugeGap);
        }

        private double ServerNow(LabSnapshot snapshot)
        {
            float realtime = Time.realtimeSinceStartup;
            if (snapshot.serverTime != seenServerTime)
            {
                seenServerTime = snapshot.serverTime;
                seenAt = realtime;
            }
            return snapshot.serverTime + System.Math.Min(1d, realtime - seenAt);
        }

        // "정신 차림!"이 톡 튀어나왔다가 사라진다.
        private void DrawAwake()
        {
            bool showing = awakeLeft >= 0f;
            awake.gameObject.SetActive(showing);
            if (!showing) return;
            float t = 1f - awakeLeft / AwakeSeconds;
            float pop = t < .15f ? Mathf.Lerp(.4f, 1.2f, t / .15f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - .15f) / .15f));
            awake.rectTransform.localScale = Vector3.one * pop;
            awake.color = new Color(.18f, .14f, .12f, Mathf.Clamp01((1f - t) * 4f));
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

            layer = NewRect("Charm", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;

            // 임시 게이지: 어두운 바탕 위에 하트색이 위에서부터 줄어든다.
            gauge = NewRect("Charm Gauge", layer);
            gauge.sizeDelta = gaugeSize;
            var back = gauge.gameObject.AddComponent<Image>();
            back.color = GaugeBackColor;
            back.raycastTarget = false;
            gaugeFill = NewRect("Charm Gauge Fill", gauge);
            gaugeFill.anchorMin = Vector2.zero;
            gaugeFill.anchorMax = Vector2.one;
            gaugeFill.offsetMin = new Vector2(3f, 3f);
            gaugeFill.offsetMax = new Vector2(-3f, -3f);
            var fill = gaugeFill.gameObject.AddComponent<Image>();
            fill.color = LabCharmEdgeFlash.HeartColor;
            fill.raycastTarget = false;

            RectTransform awakeRect = NewRect("Charm Awake", layer);
            awakeRect.anchorMin = awakeRect.anchorMax = awakeAnchor;
            awakeRect.sizeDelta = new Vector2(awakeSize * 8f, awakeSize * 1.6f);
            awake = awakeRect.gameObject.AddComponent<Text>();
            awake.font = font;
            awake.fontSize = Mathf.RoundToInt(awakeSize);
            awake.fontStyle = FontStyle.Bold;
            awake.alignment = TextAnchor.MiddleCenter;
            awake.raycastTarget = false;
            awake.text = AwakeText;
            var outline = awakeRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, .9f);
            outline.effectDistance = new Vector2(2f, -2f);

            // The warning sits above the gourd (the awake spot by default), in two lines so it fits a phone's width.
            RectTransform warningRect = NewRect("Charm Warning", layer);
            warning = warningRect.gameObject.AddComponent<Text>();
            warning.font = font;
            PlaceWarning();
            warning.fontStyle = FontStyle.Bold;
            warning.alignment = TextAnchor.MiddleCenter;
            warning.lineSpacing = 1.1f;
            warning.raycastTarget = false;
            warning.color = new Color(.55f, .1f, .16f);
            var warningOutline = warningRect.gameObject.AddComponent<Outline>();
            warningOutline.effectColor = new Color(1f, 1f, 1f, .95f);
            warningOutline.effectDistance = new Vector2(2f, -2f);
            warningRect.gameObject.SetActive(false);
            waitGuide = LabGuideText.Create(layer, font, "Charm Wait Guide");
            return true;
        }

        // Read every frame it shows, so the Inspector's place and size can be tried out during Play.
        private void PlaceWarning()
        {
            RectTransform rect = warning.rectTransform;
            rect.anchorMin = rect.anchorMax = warningAnchor;
            rect.sizeDelta = new Vector2(warningSize * 14f, warningSize * 3f);
            warning.fontSize = Mathf.RoundToInt(warningSize);
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
