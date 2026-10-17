using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The warning drawn on the orb board where a gourd bomb is about to land: a red badge with a
    /// bold white "!" that pops in and throbs, and a ring that closes in on the spot as the bomb
    /// falls, so the moment it lands is easy to read. It pops away once the bomb lands.
    /// Presentation only: it owns no collider, never touches physics, and removes itself when done.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabDropWarning : MonoBehaviour
    {
        public const string Text = "!";
        // In front of every orb, drum and item marker on the board plane (z = 0), behind a bomb blast.
        private const float FrontZ = -1.4f;
        private const int FontSize = 96;
        private const float PopSeconds = .15f;
        private const float LeaveSeconds = .15f;
        private const float ThrobHz = 4f;
        private static readonly Color MarkColor = Color.white;
        private static readonly Color OutlineColor = new Color(.35f, .03f, .02f);
        private static readonly Color RimColor = Color.white;
        // The white rim around the badge, as a share of the badge's size, like a sticker's edge.
        private const float RimScale = 1.16f;
        private static Sprite discSprite;
        private static Sprite ringSprite;

        private SpriteRenderer badge;
        private SpriteRenderer rim;
        private SpriteRenderer ring;
        private TextMesh mark;
        private TextMesh markShadow;
        private Color color;
        private float radius;
        private float landsAfter;
        private float elapsed;

        public string Label => mark != null ? mark.text : string.Empty;
        /// <summary>Seconds from the warning appearing to the bomb landing on it.</summary>
        public float LandsAfter => landsAfter;
        /// <summary>The closing ring's diameter now; it reaches the bomb's size as the bomb lands.</summary>
        public float RingDiameter => ring != null ? ring.transform.localScale.x : 0f;

        /// <summary>Starts a warning at a board point (world XY) for a bomb of this radius landing after landsAfter seconds.</summary>
        public static LabDropWarning Play(Transform board, Vector2 center, float bombRadius, float landsAfter, Color color)
        {
            var root = new GameObject("Gourd Bomb Drop Warning");
            root.layer = board.gameObject.layer;
            root.transform.SetParent(board, false);
            root.transform.position = new Vector3(center.x, center.y, FrontZ);
            var warning = root.AddComponent<LabDropWarning>();
            warning.Build(Mathf.Max(.01f, bombRadius), Mathf.Max(0f, landsAfter), color);
            return warning;
        }

        private void Build(float bombRadius, float seconds, Color badgeColor)
        {
            radius = bombRadius;
            landsAfter = seconds;
            color = badgeColor;
            ring = CreateSprite("Closing Ring", Ring(), color, 25);
            rim = CreateSprite("Warning Badge Rim", Disc(), RimColor, 26);
            badge = CreateSprite("Warning Badge", Disc(), color, 27);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                markShadow = CreateText("! Outline", font, OutlineColor, 28);
                mark = CreateText("!", font, MarkColor, 29);
            }
            Animate();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Animate();
            if (elapsed >= landsAfter + LeaveSeconds) Destroy(gameObject);
        }

        private void Animate()
        {
            // The badge pops in past full size, throbs while the bomb is coming, then pops away as it lands.
            float pop = elapsed < PopSeconds ? EaseOut(elapsed / PopSeconds) * 1.2f
                : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((elapsed - PopSeconds) / .1f));
            float throb = 1f + .1f * Mathf.Sin(elapsed * ThrobHz * Mathf.PI * 2f);
            float leave = landsAfter > 0f ? Mathf.Clamp01((elapsed - landsAfter) / LeaveSeconds) : 1f;
            float size = 1.3f * radius * pop * throb * (1f - .6f * leave);
            badge.transform.localScale = Vector3.one * size;
            badge.color = WithAlpha(color, 1f - leave);
            rim.transform.localScale = Vector3.one * (size * RimScale);
            rim.color = WithAlpha(RimColor, 1f - leave);

            // The ring closes from three times the bomb's size onto it exactly as the bomb lands.
            float closing = landsAfter > 0f ? Mathf.Clamp01(elapsed / landsAfter) : 1f;
            ring.transform.localScale = Vector3.one * (2f * radius * Mathf.Lerp(3f, 1f, closing * closing));
            ring.color = WithAlpha(color, .85f * Mathf.Clamp01(elapsed / PopSeconds) * (1f - leave));

            if (mark == null) return;
            mark.characterSize = markShadow.characterSize = .9f * size / (FontSize * .1f);
            mark.transform.localPosition = new Vector3(0f, 0f, -.02f);
            markShadow.transform.localPosition = new Vector3(.06f * size, -.06f * size, -.01f);
            mark.color = WithAlpha(MarkColor, 1f - leave);
            markShadow.color = WithAlpha(OutlineColor, 1f - leave);
        }

        private SpriteRenderer CreateSprite(string partName, Sprite sprite, Color tint, int order)
        {
            var part = new GameObject(partName);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = order;
            return renderer;
        }

        private TextMesh CreateText(string partName, Font font, Color tint, int order)
        {
            var part = new GameObject(partName);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var text = part.AddComponent<TextMesh>();
            text.font = font;
            text.text = Text;
            text.fontSize = FontSize;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = tint;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = order;
            return text;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        private static Color WithAlpha(Color tint, float alpha) => new Color(tint.r, tint.g, tint.b, tint.a * alpha);

        /// <summary>A solid white disc, one unit wide, tinted by its renderer.</summary>
        private static Sprite Disc()
        {
            if (discSprite != null) return discSprite;
            const int size = 96;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float alpha = Mathf.Clamp01(size * .5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            discSprite = CreateGeneratedSprite("Lab drop warning badge", size, pixels);
            return discSprite;
        }

        /// <summary>A thin ring whose outer edge is the sprite edge.</summary>
        private static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            const float thickness = 6f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(size * .5f - distance) * Mathf.Clamp01(distance - (size * .5f - thickness));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            ringSprite = CreateGeneratedSprite("Lab drop warning ring", size, pixels);
            return ringSprite;
        }

        private static Sprite CreateGeneratedSprite(string spriteName, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
