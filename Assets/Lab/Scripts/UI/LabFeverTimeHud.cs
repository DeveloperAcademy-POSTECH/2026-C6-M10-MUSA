using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 피버 타임 (#334) "두배" 말풍선: 피버 타임이 시작되면 스태미나 막대 오른쪽 끝 위에서 연기가 퍼지고, 그 안에서 말풍선이 톡
    /// 튀어나온다. 피버 동안 커졌다 작아졌다를 되풀이하고, 전투가 끝나면 사라진다. 늦게 들어와 이미 한참 지난 피버는 연기 없이
    /// 바로 보인다. 화면 표시만 하며 터치를 막지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabFeverTimeHud : MonoBehaviour
    {
        public const string DoublePath = "Fever/FeverDouble";
        // 말풍선은 막대 높이의 이만큼 높고, 가운데가 막대 오른쪽 위 모서리에서 (막대 높이 비율로) 이만큼 안쪽·위에 온다.
        private const float BubbleHeightScale = 1.3f;
        private static readonly Vector2 BubbleOffset = new Vector2(-.35f, .45f);
        // 숨쉬기: 0.9~1.1배를 1초에 두 번.
        private const float PulseAmount = .1f, PulseHz = 2f;
        // 등장: 연기가 먼저 퍼지고, 이만큼 뒤에 말풍선이 작게 시작해 살짝 넘쳤다가 제 크기로 돌아온다.
        private const float SmokeSeconds = .7f, PopDelay = .12f, PopSeconds = .3f;
        // A fever seen later than this after it began, for example after a reconnect, appears without the puff.
        private const double LateSeconds = 1d;
        // 연기 덩이: 말풍선 가운데에서의 자리(말풍선 높이 비율)와 크기(말풍선 높이 비율).
        private static readonly Vector3[] Puffs =
        {
            new Vector3(0f, 0f, 1.1f), new Vector3(-.55f, -.1f, .8f), new Vector3(.55f, -.05f, .8f),
            new Vector3(-.3f, .35f, .7f), new Vector3(.3f, .4f, .7f), new Vector3(0f, -.35f, .75f)
        };
        private static readonly Color SmokeColor = new Color(.96f, .93f, .86f, .95f);

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform staminaBar;
        private RectTransform layer;
        private RectTransform bubble;
        private Image bubbleImage;
        private readonly Image[] puffs = new Image[Puffs.Length];
        private float age = -1f;
        private bool puffing;
        private double seenServerTime = -1d;
        private float seenAt;
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>The bubble is on screen (#334).</summary>
        public bool Showing => bubble != null && bubble.gameObject.activeInHierarchy;
        public RectTransform Bubble => bubble;
        /// <summary>The puff it appears in is still drifting.</summary>
        public bool SmokeShowing => puffs[0] != null && puffs[0].gameObject.activeInHierarchy;
        public float Scale => bubble != null ? bubble.localScale.x : 0f;

        /// <summary>스냅샷을 읽을 네트워크, 말풍선을 그릴 Canvas, 말풍선이 붙을 스태미나 막대를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas, RectTransform bar)
        {
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
            staminaBar = bar;
        }

        private void LateUpdate()
        {
            if (network == null) return;
            LabSnapshot snapshot = network.Snapshot;
            Present(snapshot, snapshot != null ? ServerNow(snapshot) : 0d);
        }

        /// <summary>이 스냅샷의 이 Host 시각에 맞춰 말풍선을 보여 주거나 감춘다.</summary>
        public void Present(LabSnapshot snapshot, double now)
        {
            bool on = snapshot != null && snapshot.FeverTimeOn(now) && !snapshot.paused;
            if (!on)
            {
                // A pause keeps the fever's place; the bubble comes back as it was without another puff.
                if (snapshot == null || snapshot.phase != LabPhase.Playing || !snapshot.FeverTimeOn(now)) age = -1f;
                if (layer != null) layer.gameObject.SetActive(false);
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();
            if (age < 0f)
            {
                age = 0f;
                puffing = now - snapshot.feverTimeAt <= LateSeconds;
            }
            else age += Time.deltaTime;
            Vector2 center = Place();
            DrawSmoke(center);
            DrawBubble(center);
        }

        // 말풍선 가운데를 막대 오른쪽 위 모서리 근처에 둔다. 막대는 움직일 수 있으니 매번 맞춘다.
        private Vector2 Place()
        {
            float barHeight = 44f;
            Vector2 corner = layer.rect.max - new Vector2(40f, 40f);
            if (staminaBar != null)
            {
                staminaBar.GetWorldCorners(Corners);
                Vector2 min = layer.InverseTransformPoint(Corners[0]), max = layer.InverseTransformPoint(Corners[2]);
                barHeight = Mathf.Max(1f, max.y - min.y);
                corner = max;
            }
            float height = barHeight * BubbleHeightScale;
            Sprite sprite = bubbleImage.sprite;
            float aspect = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1.47f;
            bubble.sizeDelta = new Vector2(height * aspect, height);
            Vector2 center = corner + BubbleOffset * barHeight;
            // Kept inside the screen even at the top of its swell.
            Vector2 half = bubble.sizeDelta * .5f * (1f + PulseAmount);
            Rect area = layer.rect;
            center.x = Mathf.Clamp(center.x, area.xMin + half.x, Mathf.Max(area.xMin + half.x, area.xMax - half.x));
            center.y = Mathf.Clamp(center.y, area.yMin + half.y, Mathf.Max(area.yMin + half.y, area.yMax - half.y));
            bubble.anchoredPosition = center;
            return center;
        }

        // 연기가 말풍선 자리에서 짙게 퍼졌다가 부풀며 옅어진다.
        private void DrawSmoke(Vector2 center)
        {
            bool shown = puffing && age < SmokeSeconds;
            float t = Mathf.Clamp01(age / SmokeSeconds);
            float height = bubble.sizeDelta.y;
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i].gameObject.SetActive(shown);
                if (!shown) continue;
                Vector3 puff = Puffs[i];
                float grow = Mathf.Lerp(.4f, 1.25f, Mathf.Sqrt(t));
                RectTransform rect = puffs[i].rectTransform;
                rect.anchoredPosition = center + new Vector2(puff.x * (1f + .5f * t), puff.y + .25f * t) * height;
                rect.sizeDelta = Vector2.one * (puff.z * height * grow);
                puffs[i].color = new Color(SmokeColor.r, SmokeColor.g, SmokeColor.b, SmokeColor.a * Mathf.Pow(1f - t, 1.2f));
            }
        }

        // 연기 속에서 작게 시작해 살짝 넘쳤다가 제 크기가 되고, 그 뒤로는 숨 쉬듯 커졌다 작아진다.
        private void DrawBubble(Vector2 center)
        {
            float since = puffing ? age - PopDelay : PopSeconds + age;
            bubble.gameObject.SetActive(since >= 0f);
            if (since < 0f) return;
            float scale;
            if (since < PopSeconds)
            {
                float t = since / PopSeconds;
                scale = t < .6f ? Mathf.Lerp(.2f, 1.15f, t / .6f) : Mathf.Lerp(1.15f, 1f, (t - .6f) / .4f);
            }
            else scale = 1f + PulseAmount * Mathf.Sin((since - PopSeconds) * PulseHz * 2f * Mathf.PI);
            bubble.localScale = Vector3.one * scale;
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
            layer = NewRect("Fever Time", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            bubble = NewRect("Fever Double", layer);
            bubbleImage = bubble.gameObject.AddComponent<Image>();
            bubbleImage.sprite = Resources.Load<Sprite>(DoublePath);
            bubbleImage.preserveAspect = true;
            bubbleImage.raycastTarget = false;
            bubble.gameObject.SetActive(false);
            // The puff is drawn over the bubble, so the bubble seems to come out of it as it thins.
            Sprite puffSprite = LabOrbVanishSmoke.PuffSprite();
            for (int i = 0; i < puffs.Length; i++)
            {
                RectTransform rect = NewRect("Fever Smoke " + i, layer);
                puffs[i] = rect.gameObject.AddComponent<Image>();
                puffs[i].sprite = puffSprite;
                puffs[i].raycastTarget = false;
                rect.gameObject.SetActive(false);
            }
            return true;
        }

        // Children of a full-screen layer are placed from its centre, as anchoredPosition in its local space.
        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            return rect;
        }

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
