using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The gourd's swipe block on this player's board (#215): as the block begins, the blocked side edge
    /// flashes in Hapio fire-red three times, wider than its glow, so the player sees it at once; then it settles
    /// into a light red that breathes slowly until the block ends. A bright line runs down the edge
    /// itself, and the whole seconds left sit in the edge's vertical middle. It brightens for a moment
    /// when something bounces off it. Presentation only;
    /// the board does the bouncing. The glow lies in the orbs' middle plane, so the balls pass in
    /// front of it; the seconds stay on top so they can always be read.
    /// </summary>
    public sealed class LabOrbEdgeBlock : MonoBehaviour
    {
        private const float GlowShare = .28f, LineShare = .012f;
        private const float GlowAlpha = 1f, LineAlpha = 0f;
        private const float BreatheHz = .6f, FlashSeconds = .35f;
        /// <summary>The opening flashes as a block begins: three of this length.</summary>
        private const float BlinkSeconds = .35f;
        private const int Blinks = 3;
        /// <summary>How much wider than its usual glow the edge flashes.</summary>
        private const float FlashWiden = 0f;
        private const int GlowOrder = -20, LineOrder = -19, TextOrder = 30, FontSize = 64;
        // Hap-io / 5OrbsColor/Firered/600 (#A92511), from Hapio.tokens.json.
        public static readonly Color BlockInk = new Color32(169, 37, 17, 255);
        // User-specified countdown ink; keep the half-board tint on Fire red/600.
        public static readonly Color CountdownInk = new Color32(232, 210, 185, 255);
        public const string CountdownFontPath = "Sabotage/DrumPassBlock/ShillaCultureBold";
        private static readonly Color Red = BlockInk;
        private static Sprite glowSprite;
        private static Sprite lineSprite;

        private LabOrbAreaShape cachedShape;
        private bool cachedRight;
        private float cachedBottom = float.NaN;
        private Sprite halfSprite;
        private Texture2D halfTexture;
        private SpriteRenderer glow;
        private SpriteRenderer line;
        private TextMesh seconds;
        private readonly System.Collections.Generic.List<SpriteRenderer> digitViews = new System.Collections.Generic.List<SpriteRenderer>();
        private float digitHeight;
        private bool right;
        private bool secondsShown = true;
        private float age;
        private float flash;
        private Vector3 glowScale = Vector3.one;

        public bool Visible => glow != null && glow.gameObject.activeSelf;
        /// <summary>The right edge is blocked; otherwise the left.</summary>
        public bool Right => right;
        public string SecondsText => seconds != null && seconds.gameObject.activeSelf ? seconds.text : string.Empty;

        /// <summary>
        /// Whether the seconds left are written on the edge. The tutorial's block (#477) holds until the player passes
        /// the other way, so it shows no countdown; the glow, flashes and bounces stay the same.
        /// </summary>
        public bool SecondsShown
        {
            get => secondsShown;
            set
            {
                secondsShown = value;
                SetShown(Visible);
            }
        }
        /// <summary>Where the seconds and the glow's edge sit, in world space.</summary>
        public Vector3 SecondsPosition => seconds != null ? seconds.transform.position : Vector3.zero;
        public Vector3 GlowPosition => glow != null ? glow.transform.position : Vector3.zero;
        /// <summary>The glow's current opacity: full in the opening flashes, then a light red, brighter for a moment after a bounce.</summary>
        public float Brightness => glow != null ? glow.color.a : 0f;
        /// <summary>How long the opening flashes last.</summary>
        public const float OpeningSeconds = BlinkSeconds * Blinks;
        /// <summary>Still in the opening flashes of this block.</summary>
        public bool Opening => Visible && age < OpeningSeconds;
        /// <summary>How far the glow reaches in from the edge now, in world units.</summary>
        public float GlowReach => glow != null ? Mathf.Abs(glow.transform.lossyScale.x) : 0f;
        /// <summary>The glow's sorting order; the board's paper plate must stay below it.</summary>
        public int GlowSortingOrder => GlowOrder;

        public static LabOrbEdgeBlock Create(Transform board)
        {
            var edge = new GameObject("Swipe Block Edge");
            edge.layer = board.gameObject.layer;
            edge.transform.SetParent(board, false);
            return edge.AddComponent<LabOrbEdgeBlock>();
        }

        /// <summary>Lays the glow along the edge at edgeX (world), from bottom to top, reaching inward.</summary>
        public void Show(bool rightEdge, float edgeX, float bottom, float top, float boardWidth, float orbRadius, LabOrbAreaShape shape = null, float visualBottom = float.NaN)
        {
            EnsureParts();
            // A new block, or one moved to the other edge, opens with its flashes again.
            if (!Visible || rightEdge != right) age = 0f;
            right = rightEdge;
            float inward = right ? -1f : 1f;
            if (shape != null)
            {
                bottom = float.PositiveInfinity;
                top = float.NegativeInfinity;
                foreach (Vector2 point in shape.LowerBoundary) bottom = Mathf.Min(bottom, point.y);
                foreach (Vector2 point in shape.UpperBoundary) top = Mathf.Max(top, point.y);
            }
            float secondsMiddle = (bottom + top) * .5f;
            // The physics silhouette stops above the bottom HUD. The tint must continue to the viewport edge.
            if (!float.IsNaN(visualBottom) && !float.IsInfinity(visualBottom))
                bottom = Mathf.Min(bottom, visualBottom);
            float height = Mathf.Max(.01f, top - bottom);
            float middle = (bottom + top) * .5f;
            float glowWidth = GlowShare * boardWidth;
            if (halfSprite == null || cachedShape != shape || cachedRight != right || !Mathf.Approximately(cachedBottom, bottom))
            {
                ReleaseHalfSprite();
                cachedShape = shape;
                cachedRight = right;
                cachedBottom = bottom;
                halfSprite = CreateHalfSprite(shape, edgeX, bottom, top, glowWidth, inward);
                glow.sprite = halfSprite;
            }
            // The sprites grow inward from their pivot on the edge; a negative width mirrors them.
            glow.transform.position = new Vector3(edgeX, middle, 0f);
            glowScale = new Vector3(inward * glowWidth, height * .5f, 1f);
            glow.transform.localScale = glowScale;
            line.transform.position = new Vector3(edgeX, middle, 0f);
            line.transform.localScale = new Vector3(inward * LineShare * boardWidth, height, 1f);

            float textHeight = 1.2f * orbRadius;
            float size = textHeight / (FontSize * .1f);
            Vector3 at = new Vector3(edgeX + inward * Mathf.Max(boardWidth * .065f, textHeight * .8f), secondsMiddle, -2f * orbRadius);
            seconds.characterSize = size;
            seconds.transform.position = at;
            digitHeight = textHeight;
            DrawDigits(seconds.text);
            SetShown(true);
            Pose();
        }

        /// <summary>Shows the whole seconds left, rounded up.</summary>
        public void SetSeconds(float secondsLeft)
        {
            if (seconds == null) return;
            string text = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)).ToString();
            if (seconds.text == text) return;
            seconds.text = text;
            DrawDigits(text);
        }

        private void DrawDigits(string value)
        {
            if (seconds == null || digitHeight <= 0f) return;
            for (int i = 0; i < value.Length; i++)
            {
                if (i >= digitViews.Count)
                    digitViews.Add(CreateSprite("Block Countdown Digit", null, TextOrder + 1));
                SpriteRenderer digit = digitViews[i];
                digit.sprite = Resources.Load<Sprite>("Sabotage/DrumPassBlock/Digits/" + value[i]);
                digit.color = CountdownInk;
                digit.transform.position = seconds.transform.position
                    + Vector3.right * ((i - (value.Length - 1) * .5f) * digitHeight * .7f);
                digit.transform.localScale = Vector3.one * (digitHeight * 1.5f);
                digit.gameObject.SetActive(seconds.gameObject.activeSelf && secondsShown);
            }
            for (int i = value.Length; i < digitViews.Count; i++) digitViews[i].gameObject.SetActive(false);
        }

        /// <summary>Something just bounced off the edge.</summary>
        public void Flash() => flash = FlashSeconds;

        public void Hide()
        {
            flash = 0f;
            SetShown(false);
        }

        private void Update()
        {
            if (!Visible) return;
            age += Time.deltaTime;
            flash = Mathf.Max(0f, flash - Time.deltaTime);
            Pose();
        }

        private void Pose()
        {
            float breathe = .75f + .25f * (.5f + .5f * Mathf.Sin(age * BreatheHz * 2f * Mathf.PI));
            float lift = flash / FlashSeconds;
            // The opening flashes: each blink jumps to full red at once and fades, three times over.
            float opening = 0f;
            if (age < OpeningSeconds)
            {
                float blink = (age % BlinkSeconds) / BlinkSeconds;
                opening = 1f - blink * blink;
            }
            Color red = Red; // Keep the RGB fixed; only opacity/width breathe and flash.
            glow.color = new Color(red.r, red.g, red.b,
                Mathf.Min(1f, Mathf.Lerp(GlowAlpha * breathe, 1f, opening) + .05f * lift));
            line.color = new Color(red.r, red.g, red.b,
                Mathf.Min(1f, Mathf.Lerp(LineAlpha * breathe, 1f, opening) + .2f * lift));
            glow.transform.localScale = new Vector3(glowScale.x * (1f + FlashWiden * opening), glowScale.y, 1f);
        }

        private void EnsureParts()
        {
            // Remove legacy offset copies retained by an existing runtime hierarchy after a script reload.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "Swipe Block Seconds Outline")
                {
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
            if (glow != null) return;
            glow = CreateSprite("Swipe Block Glow", GlowSprite(), GlowOrder);
            line = CreateSprite("Swipe Block Line", LineSprite(), LineOrder);
            Font font = Resources.Load<Font>(CountdownFontPath);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            seconds = CreateText("Swipe Block Seconds", font, CountdownInk, TextOrder + 1);
            seconds.GetComponent<MeshRenderer>().enabled = false;
            SetShown(false);
        }

        private void SetShown(bool shown)
        {
            if (glow == null) return;
            glow.gameObject.SetActive(shown);
            line.gameObject.SetActive(false);
            seconds.gameObject.SetActive(shown && secondsShown);
            for (int i = 0; i < digitViews.Count; i++)
                digitViews[i].gameObject.SetActive(shown && secondsShown && i < seconds.text.Length);
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

        private TextMesh CreateText(string label, Font font, Color color, int order)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var text = part.AddComponent<TextMesh>();
            if (font != null)
            {
                text.font = font;
                part.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            text.fontSize = FontSize;
            text.fontStyle = FontStyle.Normal;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            part.GetComponent<MeshRenderer>().sortingOrder = order;
            return text;
        }

        // #763: a linear tint covers only the blocked half, clipped to the actual curved board.
        private Sprite CreateHalfSprite(LabOrbAreaShape shape, float edgeX, float bottom, float top,
            float width, float inward)
        {
            const int columns = 128, rows = 256;
            var pixels = new Color32[columns * rows];
            for (int x = 0; x < columns; x++)
            {
                float u = (x + .5f) / columns;
                float worldX = edgeX + inward * u * width;
                Vector2 range = shape != null ? shape.VerticalRange(worldX, 0f) : new Vector2(bottom, top);
                for (int y = 0; y < rows; y++)
                {
                    float worldY = Mathf.Lerp(bottom, top, (y + .5f) / rows);
                    // Keep the outermost band fully saturated; soften across the rest of the blocked half.
                    float gradient = Mathf.Pow(1f - Mathf.InverseLerp(.08f, 1f, u), .7f);
                    float alpha = worldY >= bottom && worldY <= range.y ? gradient : 0f;
                    pixels[y * columns + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            halfTexture = new Texture2D(columns, rows, TextureFormat.RGBA32, false)
            { name = "Blocked half curved mask", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            halfTexture.SetPixels32(pixels);
            halfTexture.Apply(false, true);
            return Sprite.Create(halfTexture, new Rect(0, 0, columns, rows), new Vector2(0f, .5f), columns);
        }

        private void ReleaseHalfSprite()
        {
            if (Application.isPlaying)
            {
                if (halfSprite != null) Destroy(halfSprite);
                if (halfTexture != null) Destroy(halfTexture);
            }
            else
            {
                if (halfSprite != null) DestroyImmediate(halfSprite);
                if (halfTexture != null) DestroyImmediate(halfTexture);
            }
            halfSprite = null;
            halfTexture = null;
        }

        private void OnDestroy() => ReleaseHalfSprite();

        /// <summary>1 unit square, pivot on its left edge: strongest there, fading inward and at both ends.</summary>
        private static Sprite GlowSprite() =>
            glowSprite != null ? glowSprite : glowSprite = CreateEdgeSprite("Lab swipe block glow", true);

        /// <summary>The same square, solid across, fading only at both ends.</summary>
        private static Sprite LineSprite() =>
            lineSprite != null ? lineSprite : lineSprite = CreateEdgeSprite("Lab swipe block line", false);

        private static Sprite CreateEdgeSprite(string label, bool fadeInward)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size;
                    float v = (y + .5f) / size;
                    float across = fadeInward ? Mathf.Pow(1f - u, 1.8f) : 1f;
                    float ends = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(v, 1f - v) / .1f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(across * ends * 255f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = label,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
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
