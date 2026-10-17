using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 박이 누군가에게 폭탄을 떨어뜨릴 때(#177) 전투 화면의 박 옆에 뜨는 "!" 말풍선. 모든 기기에서 보인다.
    /// 흰 말풍선에 굵은 테두리, 박을 가리키는 꼬리, 빨간 "!"가 그려진 그림체이며, 톡 튀어나와 흔들리다
    /// 들어간다. 박이 흔들리고 돌아도 화면에서 박 위를 따라간다. 화면 표시만 하며 터치를 막지 않는다.
    /// 일시정지 중이거나 전투가 끝나면 사라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdAlert : MonoBehaviour
    {
        public const string Text = "!";
        private const int TextureSize = 150;
        // 말풍선 그림 안의 위치 (픽셀, 아래가 0): 몸통 타원과 박을 가리키는 꼬리 끝.
        private static readonly Vector2 BodyCenter = new Vector2(88f, 94f);
        private static readonly Vector2 BodyRadii = new Vector2(64f, 56f);
        private static readonly Vector2 TailTip = new Vector2(16f, 8f);
        private static readonly Vector2 TailBaseA = new Vector2(44f, 70f);
        private static readonly Vector2 TailBaseB = new Vector2(70f, 46f);
        private const float OutlinePixels = 7f;
        private static readonly Color32 FillColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 InkColor = new Color32(43, 36, 32, 255);
        private static readonly Color MarkColor = new Color(.91f, .16f, .1f);
        private const float PopSeconds = .18f;
        private const float LeaveSeconds = .15f;
        private const float WiggleDegrees = 9f;
        private const float WiggleHz = 3f;
        private static Sprite bubbleSprite;

        [Tooltip("말풍선 크기 (Canvas 기준 픽셀)")]
        [SerializeField, Min(10f)] private float bubbleSize = 110f;
        [Tooltip("말풍선이 떠 있는 시간 (초)")]
        [SerializeField, Min(.3f)] private float showSeconds = 1.4f;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform canvasRect;
        private Camera battleCamera;
        private LabTarget target;
        private RectTransform layer;
        private RectTransform bubble;
        private UnityEngine.UI.Text mark;
        private float elapsed = -1f;

        /// <summary>박 폭탄 알림이 진행 중인지 (박이 화면 밖이라 안 보이는 순간도 포함).</summary>
        public bool Active => elapsed >= 0f;
        /// <summary>말풍선이 지금 화면에 그려지고 있는지.</summary>
        public bool Visible => bubble != null && bubble.gameObject.activeSelf;
        public RectTransform Bubble => bubble;
        public string Label => mark != null ? mark.text : string.Empty;
        public float ShowSeconds => showSeconds;

        /// <summary>박 폭탄 신호를 받을 네트워크와, 말풍선을 그릴 Canvas·전투 카메라를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas, Camera camera)
        {
            if (network != null) network.GourdBombDropped -= OnGourdBombDropped;
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
            battleCamera = camera;
            if (network != null) network.GourdBombDropped += OnGourdBombDropped;
        }

        private void OnGourdBombDropped(ulong targetPlayer, string bombId, Vector2 landing)
        {
            if (network != null) Show(network.target);
        }

        /// <summary>박 옆에 말풍선을 띄운다. 이미 떠 있으면 처음부터 다시 튀어나온다.</summary>
        public void Show(LabTarget gourd)
        {
            if (gourd == null || !EnsureBubble()) return;
            target = gourd;
            elapsed = 0f;
            Place();
            Animate();
        }

        public void Hide()
        {
            elapsed = -1f;
            target = null;
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!Active) return;
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool battle = network == null
                || (snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused);
            elapsed += Time.deltaTime;
            if (!battle || target == null || elapsed >= showSeconds)
            {
                Hide();
                return;
            }
            Place();
            Animate();
        }

        /// <summary>꼬리 끝을 화면에서 박의 오른쪽 위 어깨에 맞춘다.</summary>
        private void Place()
        {
            Collider hitbox = target != null ? target.Hitbox : null;
            if (battleCamera == null) battleCamera = Camera.main;
            if (hitbox == null || battleCamera == null)
            {
                bubble.gameObject.SetActive(false);
                return;
            }
            Bounds gourd = hitbox.bounds;
            Vector3 shoulder = gourd.center + Vector3.up * (gourd.extents.y * .8f)
                + battleCamera.transform.right * (Mathf.Max(gourd.extents.x, gourd.extents.z) * .55f);
            Vector3 screen = battleCamera.WorldToScreenPoint(shoulder);
            bool visible = screen.z > 0f && battleCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            visible &= RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                new Vector2(screen.x, screen.y), uiCamera, out Vector2 canvasPoint);
            bubble.gameObject.SetActive(visible);
            if (visible) bubble.anchoredPosition = canvasPoint;
        }

        /// <summary>톡 튀어나와(살짝 크게 갔다 돌아옴) 흔들리다가, 끝에 작아지며 들어간다.</summary>
        private void Animate()
        {
            float t = Mathf.Max(0f, elapsed);
            float pop = t < PopSeconds ? EaseOut(t / PopSeconds) * 1.15f
                : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - PopSeconds) / .12f));
            float leave = Mathf.Clamp01((t - (showSeconds - LeaveSeconds)) / LeaveSeconds);
            bubble.localScale = Vector3.one * (pop * (1f - leave));
            float wiggle = WiggleDegrees * Mathf.Sin(t * WiggleHz * Mathf.PI * 2f) * (1f - t / showSeconds);
            bubble.localRotation = Quaternion.Euler(0f, 0f, wiggle);
        }

        private bool EnsureBubble()
        {
            if (bubble != null) return true;
            if (canvas == null)
            {
                Canvas found = GetComponentInParent<Canvas>();
                canvas = found != null ? found.rootCanvas : null;
            }
            if (canvas == null) return false;
            canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null) return false;

            layer = NewRect("Gourd Alert", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetAsLastSibling();

            bubble = NewRect("Gourd Alert Bubble", layer);
            bubble.anchorMin = bubble.anchorMax = new Vector2(.5f, .5f);
            bubble.sizeDelta = new Vector2(bubbleSize, bubbleSize);
            // 꼬리 끝이 기준점이라, 위치를 박에 맞추면 말풍선은 그 오른쪽 위로 펼쳐진다.
            bubble.pivot = TailTip / TextureSize;
            var image = bubble.gameObject.AddComponent<Image>();
            image.sprite = BubbleSprite();
            image.raycastTarget = false;

            RectTransform markRect = NewRect("Gourd Alert Mark", bubble);
            Vector2 center = BodyCenter / TextureSize;
            markRect.anchorMin = markRect.anchorMax = center;
            markRect.sizeDelta = new Vector2(bubbleSize, bubbleSize);
            mark = markRect.gameObject.AddComponent<UnityEngine.UI.Text>();
            UnityEngine.UI.Text[] existing = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            for (int i = 0; i < existing.Length && mark.font == null; i++)
                if (existing[i] != mark) mark.font = existing[i].font;
            if (mark.font == null) mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mark.text = Text;
            mark.fontSize = Mathf.RoundToInt(bubbleSize * .5f);
            mark.fontStyle = FontStyle.Bold;
            mark.alignment = TextAnchor.MiddleCenter;
            mark.horizontalOverflow = HorizontalWrapMode.Overflow;
            mark.verticalOverflow = VerticalWrapMode.Overflow;
            mark.color = MarkColor;
            mark.raycastTarget = false;
            var outline = markRect.gameObject.AddComponent<Outline>();
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            bubble.gameObject.SetActive(false);
            return true;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>
        /// 손그림 느낌의 말풍선: 흰 타원 몸통과 왼쪽 아래로 뻗은 꼬리를 하나로 합친 모양에 굵은
        /// 테두리를 두른다. 두 모양을 거리값으로 합쳐 이음매 없이 테두리가 이어진다.
        /// </summary>
        private static Sprite BubbleSprite()
        {
            if (bubbleSprite != null) return bubbleSprite;
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    Vector2 q = (p - BodyCenter) / BodyRadii;
                    float body = (q.magnitude - 1f) * Mathf.Min(BodyRadii.x, BodyRadii.y);
                    float tail = TriangleDistance(p, TailBaseA, TailTip, TailBaseB);
                    float distance = Mathf.Min(body, tail); // negative inside the bubble
                    float alpha = Mathf.Clamp01(.5f - distance);
                    float ink = Mathf.Clamp01(distance + OutlinePixels + .5f);
                    Color32 color = Color32.Lerp(FillColor, InkColor, ink);
                    color.a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * TextureSize + x] = color;
                }
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Lab gourd alert bubble",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            bubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(.5f, .5f), 100f);
            bubbleSprite.name = texture.name;
            bubbleSprite.hideFlags = HideFlags.DontSave;
            return bubbleSprite;
        }

        /// <summary>점에서 삼각형 테두리까지의 거리. 안쪽이면 음수.</summary>
        private static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float distance = Mathf.Min(SegmentDistance(p, a, b), Mathf.Min(SegmentDistance(p, b, c), SegmentDistance(p, c, a)));
            float ab = Cross(b - a, p - a), bc = Cross(c - b, p - b), ca = Cross(a - c, p - c);
            bool inside = (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
            return inside ? -distance : distance;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (network != null) network.GourdBombDropped -= OnGourdBombDropped;
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
