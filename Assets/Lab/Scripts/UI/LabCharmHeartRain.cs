using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 매혹 화면 효과 (#664): 매혹에 걸린 사람 자신의 화면에만, 가장자리부터 분홍빛이 은은하게 숨 쉬고 하트
    /// (Resources/Sabotage/CharmHeart, 같은 이름으로 그림을 바꾸면 그대로 쓰인다)가 화면 아래에서 뿅뿅 튀어나와 흔들리며
    /// 위로 올라간다. 하트 그림이 없으면 매혹 말풍선 그림 + 하트 눈 얼굴을 대신 쓴다. 풀리면 분홍빛이 사라지고 남은 하트는
    /// 올라가던 대로 흐려져 없어진다. 터치는 막지 않는다(판은 매혹이 따로 멈춘다). 매혹 게이지·"정신 차림!" 바로 아래에 그린다.
    /// 값은 Play 중 Inspector에서 바로 바꿔 볼 수 있다.
    /// </summary>
    public sealed class LabCharmHeartRain : MonoBehaviour
    {
        [Header("분홍빛")]
        [Tooltip("화면 가장자리 분홍빛의 색")]
        [SerializeField] private Color tint = new Color(1f, .45f, .7f, 1f);
        [Tooltip("가장자리 분홍빛의 진하기 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float edgeStrength = .55f;
        [Tooltip("화면 전체에 깔리는 옅은 분홍빛의 진하기 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float wholeStrength = .1f;
        [Tooltip("분홍빛이 숨 쉬는 빠르기 (1초에 몇 번)")]
        [SerializeField, Min(0f)] private float breatheHz = .8f;
        [Tooltip("걸릴 때 나타나고 풀릴 때 사라지는 시간 (초)")]
        [SerializeField, Min(.01f)] private float fadeSeconds = .35f;

        [Header("올라오는 하트")]
        [Tooltip("1초에 튀어나오는 하트 수")]
        [SerializeField, Min(0f)] private float heartsPerSecond = 3.5f;
        [Tooltip("하트가 화면 아래에서 위까지 올라가는 시간 (초, 최소~최대)")]
        [SerializeField] private Vector2 riseSeconds = new Vector2(2f, 2.8f);
        [Tooltip("하트 크기 (화면 너비 비율, 최소~최대)")]
        [SerializeField] private Vector2 heartWidth = new Vector2(.07f, .13f);
        [Tooltip("올라가며 좌우로 흔들리는 폭 (화면 너비 비율)")]
        [SerializeField, Min(0f)] private float swayWidth = .04f;
        [Tooltip("튀어나올 때 커졌다 돌아오는 시간 (초)")]
        [SerializeField, Min(.01f)] private float popSeconds = .18f;
        [Tooltip("한 번에 보이는 하트 수의 한도")]
        [SerializeField, Min(1)] private int maxHearts = 14;

        public const string HeartPath = "Sabotage/CharmHeart";
        private const string LayerName = "Charm Heart Rain";
        private const int VignetteSize = 128;
        private static Texture2D vignetteTexture;

        private sealed class Heart
        {
            public RectTransform Root;
            public Image Bubble, Face;
            public float Age, Rise, StartX, Sway, SwayPhase, Width;
            public bool Live;
        }

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer;
        private RawImage vignette;
        private Image wash;
        private Sprite heartArt, leftBubble, rightBubble, face;
        private readonly List<Heart> hearts = new List<Heart>();
        private float shown, breathe, spawnDebt;
        private bool charmed;
        private System.Random random = new System.Random(664);

        /// <summary>The pink shows on this screen now (0 none, 1 full).</summary>
        public float PinkShown => shown;
        /// <summary>How many hearts are rising now.</summary>
        public int HeartsRising { get { int live = 0; foreach (Heart heart in hearts) if (heart.Live) live++; return live; } }
        public RectTransform Layer => layer;
        /// <summary>Every live heart's height on the screen (0 bottom, 1 top), for checks.</summary>
        public IEnumerable<float> HeartHeights()
        {
            if (layer == null) yield break;
            float height = Mathf.Max(1f, layer.rect.height);
            foreach (Heart heart in hearts)
                if (heart.Live) yield return heart.Root.anchoredPosition.y / height;
        }
        public bool AnyImageTakesTouches
        {
            get
            {
                if (layer == null) return false;
                foreach (Graphic graphic in layer.GetComponentsInChildren<Graphic>(true)) if (graphic.raycastTarget) return true;
                return false;
            }
        }

        public void Bind(LabNetwork source, Canvas target, System.Random seed = null)
        {
            network = source;
            canvas = target != null ? target.rootCanvas : null;
            if (seed != null) random = seed;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool on = snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused
                && snapshot.CharmOn(network.LocalPlayerId, network.BattlePresentationTime);
            Present(on, Time.deltaTime);
        }

        /// <summary>One frame of the effect: charmed or not, and how much time passed.</summary>
        public void Present(bool isCharmed, float deltaTime)
        {
            charmed = isCharmed;
            if (!charmed && shown <= 0f && HeartsRising == 0)
            {
                if (layer != null) layer.gameObject.SetActive(false);
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            KeepUnderCharmHud();
            float step = Mathf.Max(0f, deltaTime);
            shown = Mathf.MoveTowards(shown, charmed ? 1f : 0f, step / fadeSeconds);
            breathe += step * breatheHz * 2f * Mathf.PI;
            float pulse = .85f + .15f * Mathf.Sin(breathe);
            vignette.color = new Color(tint.r, tint.g, tint.b, edgeStrength * shown * pulse);
            wash.color = new Color(tint.r, tint.g, tint.b, wholeStrength * shown * pulse);
            if (charmed && heartsPerSecond > 0f)
            {
                spawnDebt += step * heartsPerSecond;
                while (spawnDebt >= 1f)
                {
                    spawnDebt -= 1f;
                    Spawn();
                }
            }
            else spawnDebt = 0f;
            foreach (Heart heart in hearts) if (heart.Live) Move(heart, step);
        }

        private void Spawn()
        {
            Heart heart = null;
            int live = 0;
            foreach (Heart candidate in hearts)
            {
                if (candidate.Live) live++;
                else if (heart == null) heart = candidate;
            }
            if (live >= Mathf.Max(1, maxHearts)) return;
            if (heart == null) { heart = NewHeart(); hearts.Add(heart); }
            float width = layer.rect.width;
            heart.Width = width * Mathf.Lerp(Mathf.Min(heartWidth.x, heartWidth.y), Mathf.Max(heartWidth.x, heartWidth.y), Next());
            heart.StartX = Mathf.Lerp(.08f, .92f, Next());
            heart.Rise = Mathf.Lerp(Mathf.Min(riseSeconds.x, riseSeconds.y), Mathf.Max(riseSeconds.x, riseSeconds.y), Next());
            heart.Sway = swayWidth * width * Mathf.Lerp(.5f, 1f, Next());
            heart.SwayPhase = Next() * 2f * Mathf.PI;
            heart.Age = 0f;
            bool right = Next() < .5f;
            heart.Bubble.sprite = heartArt != null ? heartArt : right ? rightBubble : leftBubble;
            heart.Face.gameObject.SetActive(heartArt == null);
            heart.Root.sizeDelta = new Vector2(heart.Width, heart.Width);
            heart.Live = true;
            heart.Root.gameObject.SetActive(true);
            Move(heart, 0f);
        }

        private void Move(Heart heart, float step)
        {
            heart.Age += step;
            float t = heart.Age / Mathf.Max(.1f, heart.Rise);
            if (t >= 1f)
            {
                heart.Live = false;
                heart.Root.gameObject.SetActive(false);
                return;
            }
            Rect area = layer.rect;
            float y = Mathf.Lerp(-heart.Width * .5f, area.height + heart.Width * .5f, t);
            float x = heart.StartX * area.width + heart.Sway * Mathf.Sin(heart.SwayPhase + t * 2f * Mathf.PI * 1.2f);
            heart.Root.anchoredPosition = new Vector2(x, y);
            heart.Root.localEulerAngles = new Vector3(0f, 0f, 8f * Mathf.Sin(heart.SwayPhase + t * 6f));
            // 뿅: grows past its size for a moment as it pops out, then rises at its size.
            float pop = heart.Age < popSeconds ? Mathf.Sin(Mathf.Clamp01(heart.Age / popSeconds) * Mathf.PI * .75f) / Mathf.Sin(Mathf.PI * .75f)
                : 1f;
            heart.Root.localScale = Vector3.one * Mathf.Max(0f, pop);
            float alpha = Mathf.Clamp01(t / .1f) * Mathf.Clamp01((1f - t) / .3f);
            heart.Bubble.color = heart.Face.color = new Color(1f, 1f, 1f, alpha);
        }

        private float Next() => (float)random.NextDouble();

        private Heart NewHeart()
        {
            var root = new GameObject("Rising Heart", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(layer, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(.5f, .5f);
            // The heart art (or, without it, the charm bubble art) fills the heart; the face only goes with the bubble.
            Image bubble = NewImage("Heart Art", root, Vector2.zero, Vector2.one);
            // The heart-eyed face sits in the bubble's round part, as on the bubbles beside a charmed player.
            Image faceImage = NewImage("Face", root, new Vector2(.2f, .2f), new Vector2(.8f, .8f));
            faceImage.sprite = face;
            faceImage.preserveAspect = true;
            bubble.preserveAspect = true;
            return new Heart { Root = root, Bubble = bubble, Face = faceImage };
        }

        private static Image NewImage(string name, RectTransform parent, Vector2 min, Vector2 max)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.raycastTarget = false;
            return image;
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
            heartArt = Resources.Load<Sprite>(HeartPath);
            leftBubble = Resources.Load<Sprite>(LabCharmBubbles.LeftBubblePath);
            rightBubble = Resources.Load<Sprite>(LabCharmBubbles.RightBubblePath);
            face = Resources.Load<Sprite>(LabCharmBubbles.FacePath);
            layer = new GameObject(LayerName, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(canvasRect, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            wash = NewImage("Pink Wash", layer, Vector2.zero, Vector2.one);
            vignette = new GameObject("Pink Edges", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            RectTransform edges = vignette.rectTransform;
            edges.SetParent(layer, false);
            edges.anchorMin = Vector2.zero;
            edges.anchorMax = Vector2.one;
            edges.offsetMin = edges.offsetMax = Vector2.zero;
            vignette.texture = Vignette();
            vignette.raycastTarget = false;
            wash.color = vignette.color = Color.clear;
            return true;
        }

        /// <summary>Right under the charm's own gauge and "정신 차림!" (LabCharmHud's layer), over everything else.</summary>
        private void KeepUnderCharmHud()
        {
            Transform parent = layer.parent;
            Transform hud = parent != null ? parent.Find("Charm") : null;
            if (hud == null) { layer.SetAsLastSibling(); return; }
            int hudIndex = hud.GetSiblingIndex(), mine = layer.GetSiblingIndex();
            if (mine == hudIndex - 1) return;
            // Taking the gauge's place pushes it up one; moving down from above leaves it one lower: either way, just under it.
            layer.SetSiblingIndex(mine > hudIndex ? hudIndex : hudIndex - 1);
        }

        /// <summary>White at the edges fading to clear in the middle; the tint colours it.</summary>
        internal static Texture2D Vignette()
        {
            if (vignetteTexture != null) return vignetteTexture;
            vignetteTexture = new Texture2D(VignetteSize, VignetteSize, TextureFormat.RGBA32, false)
            {
                name = "Charm Pink Edges",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[VignetteSize * VignetteSize];
            for (int y = 0; y < VignetteSize; y++)
                for (int x = 0; x < VignetteSize; x++)
                {
                    float u = (x + .5f) / VignetteSize * 2f - 1f, v = (y + .5f) / VignetteSize * 2f - 1f;
                    // Rounded-square distance from the middle: 0 inside, 1 at the edge.
                    float edge = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), .25f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1.02f, edge));
                    pixels[y * VignetteSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            vignetteTexture.SetPixels32(pixels);
            vignetteTexture.Apply(false, true);
            return vignetteTexture;
        }

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
