using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// P numbers are the fixed visual identity, independent of connection ID, element,
    /// creation owner or Host role. Keep this ordering aligned with GourdPaint/PainterInk.
    /// Badge textures are generated once per P, shared read-only and never saved as assets.
    /// </summary>
    public static class LabPlayerPaintPatterns
    {
        private const int BadgeSize = 96;
        private static readonly Sprite[] Badges = new Sprite[LabPaintSurface.MaxPainterSlot + 1];
        // P6 (3:3 땅따먹기, #394) is the diagonal the other way from P2.
        private static readonly string[] Names = { "None", "Dots", "Diagonal", "Horizontal", "Waves", "Grid", "Back diagonal" };

        public static string NameFor(int playerNumber) =>
            playerNumber >= 1 && playerNumber <= LabPaintSurface.MaxPainterSlot ? Names[playerNumber] : Names[0];

        public static Sprite SpriteFor(int playerNumber)
        {
            if (playerNumber < 1 || playerNumber > LabPaintSurface.MaxPainterSlot) return null;
            if (Badges[playerNumber] != null) return Badges[playerNumber];
            var pixels = new Color32[BadgeSize * BadgeSize];
            Color paper = new Color(.96f, .93f, .85f);
            Color ink = new Color(.18f, .15f, .12f);
            for (int y = 0; y < BadgeSize; y++)
                for (int x = 0; x < BadgeSize; x++)
                {
                    Vector2 uv = new Vector2((x + .5f) / BadgeSize, (y + .5f) / BadgeSize);
                    float distance = (uv - new Vector2(.5f, .5f)).magnitude;
                    float alpha = 1f - Smooth(.478f, .495f, distance);
                    float mark = Ink(playerNumber, uv * 3f);
                    // A plain circular rim makes the sample readable over both the light
                    // lobby panel and battlefield. It is a legend, not another element color.
                    mark = Mathf.Max(mark, Smooth(.45f, .468f, distance));
                    Color color = Color.Lerp(paper, ink, mark);
                    color.a = alpha;
                    pixels[y * BadgeSize + x] = color;
                }
            var texture = new Texture2D(BadgeSize, BadgeSize, TextureFormat.RGBA32, false)
            {
                name = $"P{playerNumber} paint pattern ({NameFor(playerNumber)})",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, BadgeSize, BadgeSize), new Vector2(.5f, .5f), BadgeSize);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Badges[playerNumber] = sprite;
            return sprite;
        }

        private static float Smooth(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Line(float coordinate, float width) =>
            1f - Smooth(width - .025f, width + .025f, Mathf.Abs(Mathf.Repeat(coordinate + .5f, 1f) - .5f));

        private static float Ink(int playerNumber, Vector2 q)
        {
            switch (playerNumber)
            {
                case 1:
                    float distance = new Vector2(Mathf.Repeat(q.x, 1f) - .5f, Mathf.Repeat(q.y, 1f) - .5f).magnitude;
                    return 1f - Smooth(.145f, .195f, distance);
                case 2: return Line(q.x + q.y, .115f);
                case 3: return Line(q.y, .115f);
                case 4: return Line(q.y + .2f * Mathf.Sin(q.x * Mathf.PI), .115f);
                case 5: return Mathf.Max(Line(q.x, .075f), Line(q.y, .075f));
                case 6: return Line(q.x - q.y, .115f);
                default: return 0f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReleaseCachedBadges()
        {
            for (int i = 1; i < Badges.Length; i++)
            {
                if (Badges[i] == null) continue;
                Texture2D texture = Badges[i].texture;
                LabVisualSafety.DestroyVisualObject(Badges[i]);
                LabVisualSafety.DestroyVisualObject(texture);
                Badges[i] = null;
            }
        }
    }
}
