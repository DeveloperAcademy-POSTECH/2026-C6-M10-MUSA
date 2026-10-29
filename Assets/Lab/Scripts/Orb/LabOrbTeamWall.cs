using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281): the closed outer edge of this player's board. Only the teammate's side passes,
    /// so the other side shows a calm ink-coloured wall for the whole battle, without a countdown, and it
    /// brightens for a moment when something bounces off it. Presentation only; the board does the bouncing.
    /// The look follows the swipe block (#215) so players read it the same way, in ink rather than red.
    /// </summary>
    public sealed class LabOrbTeamWall : MonoBehaviour
    {
        private const float GlowShare = .07f, LineShare = .012f;
        private const float GlowAlpha = .32f, LineAlpha = .75f;
        /// <summary>How long a bounce lights the wall and how much it adds to the glow by default (#293 tunes both).</summary>
        public const float DefaultFlashSeconds = .35f, DefaultFlashBoost = .4f;
        private const int GlowOrder = -20, LineOrder = -19;
        private static readonly Color Ink = new Color(.24f, .2f, .16f);
        private static Sprite glowSprite, lineSprite;

        private SpriteRenderer glow, line;
        private float flash, flashSeconds = DefaultFlashSeconds, flashBoost = DefaultFlashBoost;

        public bool Visible => glow != null && glow.gameObject.activeSelf;
        public bool Right { get; private set; }
        /// <summary>The glow's opacity now: the calm wall, or brighter just after a bounce.</summary>
        public float Brightness => glow != null ? glow.color.a : 0f;
        public int GlowSortingOrder => GlowOrder;

        public static LabOrbTeamWall Create(Transform board)
        {
            var wall = new GameObject("Team Wall Edge");
            wall.layer = board.gameObject.layer;
            wall.transform.SetParent(board, false);
            return wall.AddComponent<LabOrbTeamWall>();
        }

        /// <summary>Lays the wall along the edge at edgeX (world), from bottom to top, reaching inward.</summary>
        public void Show(bool rightEdge, float edgeX, float bottom, float top, float boardWidth)
        {
            EnsureParts();
            Right = rightEdge;
            float inward = rightEdge ? -1f : 1f;
            float height = Mathf.Max(.01f, top - bottom), middle = (bottom + top) * .5f;
            glow.transform.position = new Vector3(edgeX, middle, 0f);
            glow.transform.localScale = new Vector3(inward * GlowShare * boardWidth, height, 1f);
            line.transform.position = new Vector3(edgeX, middle, 0f);
            line.transform.localScale = new Vector3(inward * LineShare * boardWidth, height, 1f);
            SetShown(true);
            Pose();
        }

        /// <summary>Something just bounced off the wall: it lights up for seconds, the glow raised by boost at first.</summary>
        public void Flash(float seconds = DefaultFlashSeconds, float boost = DefaultFlashBoost)
        {
            flashSeconds = Mathf.Max(0f, seconds);
            flashBoost = Mathf.Clamp(boost, 0f, 1f - GlowAlpha);
            flash = flashSeconds;
        }

        public void Hide()
        {
            flash = 0f;
            SetShown(false);
        }

        private void Update()
        {
            if (!Visible) return;
            flash = Mathf.Max(0f, flash - Time.deltaTime);
            Pose();
        }

        private void Pose()
        {
            float lift = flashSeconds > 0f ? flash / flashSeconds : 0f;
            // The line brightens with the glow, at the same 0.25 : 0.4 share as before.
            glow.color = new Color(Ink.r, Ink.g, Ink.b, Mathf.Min(1f, GlowAlpha + flashBoost * lift));
            line.color = new Color(Ink.r, Ink.g, Ink.b, Mathf.Min(1f, LineAlpha + flashBoost * .625f * lift));
        }

        private void EnsureParts()
        {
            if (glow != null) return;
            glow = CreateSprite("Team Wall Glow", GlowSprite(), GlowOrder);
            line = CreateSprite("Team Wall Line", LineSprite(), LineOrder);
            SetShown(false);
        }

        private void SetShown(bool shown)
        {
            if (glow == null) return;
            glow.gameObject.SetActive(shown);
            line.gameObject.SetActive(shown);
        }

        private SpriteRenderer CreateSprite(string label, Sprite sprite, int order)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite GlowSprite() =>
            glowSprite != null ? glowSprite : glowSprite = CreateEdgeSprite("Lab team wall glow", true);

        private static Sprite LineSprite() =>
            lineSprite != null ? lineSprite : lineSprite = CreateEdgeSprite("Lab team wall line", false);

        /// <summary>1 unit square, pivot on its left edge; the glow fades inward, both fade at the ends.</summary>
        private static Sprite CreateEdgeSprite(string label, bool fadeInward)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size, v = (y + .5f) / size;
                    float across = fadeInward ? Mathf.Pow(1f - u, 1.8f) : 1f;
                    float ends = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(v, 1f - v) / .1f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(across * ends * 255f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = label, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, .5f), size);
            sprite.name = label;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
