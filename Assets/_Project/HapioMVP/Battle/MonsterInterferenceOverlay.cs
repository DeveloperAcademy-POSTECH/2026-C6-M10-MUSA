using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// Presentation-only warning for a monster transfer interference. The restricted board edge is red,
    /// while the message is centered over the monster viewport. Transfer authority remains Host-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterInterferenceOverlay : MonoBehaviour
    {
        public const string DefaultMessage = "요괴의 방해로 구슬을 자유롭게 전달하기 어려워졌다...";

        [SerializeField] private Graphic leftEdge;
        [SerializeField] private Graphic rightEdge;
        [SerializeField] private Text message;
        [SerializeField] private Color blockedColor = new Color(1f, .08f, .05f, .88f);
        [SerializeField] private Color messageColor = new Color(1f, .80f, .39f, 1f);
        [SerializeField, Min(1f)] private float edgeWidth = 18f;
        [SerializeField, Min(.01f)] private float messageFadeSeconds = .8f;
        [SerializeField, Min(1f)] private float messageHeight = 68f;
        [SerializeField, Min(1f)] private float messageHorizontalPadding = 24f;

        private RectTransform root;
        private Canvas canvas;

        public bool Visible { get; private set; }
        public MonsterTransferDirection BlockedDirection { get; private set; }

        private void Awake()
        {
            root = transform as RectTransform;
            canvas = GetComponentInParent<Canvas>();
            Hide();
        }

        public void Present(MonsterTransferDirection blockedDirection, Rect boardScreenRect,
            Rect monsterScreenRect, double? hostNow, double endsAt)
        {
            if (blockedDirection == MonsterTransferDirection.None || !Valid(boardScreenRect)
                || !Valid(monsterScreenRect) || hostNow.HasValue && hostNow.Value >= endsAt)
            {
                Hide();
                return;
            }

            if (root == null) root = transform as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (root == null || canvas == null || !TryLocalRect(boardScreenRect, out var board)
                || !TryLocalRect(monsterScreenRect, out var monster))
            {
                Hide();
                return;
            }

            float alpha = hostNow.HasValue ? FadeAlpha(hostNow.Value, endsAt, messageFadeSeconds) : 1f;
            PlaceEdge(leftEdge, LeftEdgeRect(board, edgeWidth));
            PlaceEdge(rightEdge, RightEdgeRect(board, edgeWidth));
            SetGraphic(leftEdge, blockedDirection == MonsterTransferDirection.Left, blockedColor, alpha);
            SetGraphic(rightEdge, blockedDirection == MonsterTransferDirection.Right, blockedColor, alpha);

            if (message != null)
            {
                float width = Mathf.Max(1f, monster.width - messageHorizontalPadding * 2f);
                Place(message.rectTransform, new Rect(monster.center.x - width * .5f,
                    monster.center.y - messageHeight * .5f, width, messageHeight));
                message.text = DefaultMessage;
                message.color = WithAlpha(messageColor, alpha);
                message.enabled = true;
            }

            BlockedDirection = blockedDirection;
            Visible = true;
        }

        public void Hide()
        {
            if (leftEdge != null) leftEdge.enabled = false;
            if (rightEdge != null) rightEdge.enabled = false;
            if (message != null) message.enabled = false;
            BlockedDirection = MonsterTransferDirection.None;
            Visible = false;
        }

        public static float FadeAlpha(double hostNow, double endsAt, float fadeSeconds)
        {
            if (double.IsNaN(hostNow) || double.IsInfinity(hostNow) || double.IsNaN(endsAt)
                || double.IsInfinity(endsAt) || fadeSeconds <= 0f) return 0f;
            return Mathf.Clamp01((float)((endsAt - hostNow) / fadeSeconds));
        }

        public static Rect LeftEdgeRect(Rect board, float width)
        {
            float actual = Mathf.Clamp(width, 0f, board.width);
            return new Rect(board.xMin, board.yMin, actual, board.height);
        }

        public static Rect RightEdgeRect(Rect board, float width)
        {
            float actual = Mathf.Clamp(width, 0f, board.width);
            return new Rect(board.xMax - actual, board.yMin, actual, board.height);
        }

        private bool TryLocalRect(Rect screen, out Rect local)
        {
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            bool gotMin = RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                new Vector2(screen.xMin, screen.yMin), camera, out var min);
            bool gotMax = RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                new Vector2(screen.xMax, screen.yMax), camera, out var max);
            local = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return gotMin && gotMax && Valid(local);
        }

        private static void PlaceEdge(Graphic graphic, Rect rect)
        {
            if (graphic != null) Place(graphic.rectTransform, rect);
        }

        private static void Place(RectTransform rect, Rect bounds)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = bounds.center;
            rect.sizeDelta = bounds.size;
        }

        private static void SetGraphic(Graphic graphic, bool enabled, Color color, float alpha)
        {
            if (graphic == null) return;
            graphic.color = WithAlpha(color, alpha);
            graphic.enabled = enabled;
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(alpha));

        private static bool Valid(Rect rect) =>
            Finite(rect.xMin) && Finite(rect.yMin) && Finite(rect.xMax) && Finite(rect.yMax)
            && rect.width > 0f && rect.height > 0f;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnDisable() => Hide();
    }
}
