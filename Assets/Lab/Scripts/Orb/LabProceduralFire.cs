using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #540 지귀 불 그림을 그림 파일 없이 코드로 그린다(야광귀 연출처럼). 불꽃 혀(아래가 둥글고 위가 뾰족한 물방울 꼴,
    /// 하얀 노랑 속 → 주황 → 붉은 테두리, 가장자리는 들쭉날쭉)와 부드러운 빛 무리를 만든다. 모두 한 번 만들어 같이 쓴다.
    /// 불꽃 그림은 아래 가운데가 기준점이라 그 자리에서 위로 솟는다.
    /// </summary>
    public static class LabProceduralFire
    {
        public const int FlameVariants = 4;
        private const int FlameWidth = 48, FlameHeight = 80;
        private static readonly Sprite[] flames = new Sprite[FlameVariants];
        private static Sprite glow;
        private static readonly Color CoreColor = new Color(1f, .97f, .78f, 1f);
        private static readonly Color MiddleColor = new Color(1f, .78f, .18f, 1f);
        private static readonly Color OuterColor = new Color(1f, .42f, .06f, 1f);
        private static readonly Color RimColor = new Color(.72f, .1f, .03f, 1f);

        /// <summary>불꽃 혀 하나 (variant마다 모양이 조금씩 다르다). 아래 가운데 기준.</summary>
        public static Sprite Flame(int variant)
        {
            variant = ((variant % FlameVariants) + FlameVariants) % FlameVariants;
            if (flames[variant] != null) return flames[variant];
            var texture = new Texture2D(FlameWidth, FlameHeight, TextureFormat.RGBA32, false)
            { name = "Jigwi Procedural Flame " + variant + " (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[FlameWidth * FlameHeight];
            float seed = 7.3f + variant * 19.1f;
            float lean = (variant - 1.5f) * .06f;
            for (int y = 0; y < FlameHeight; y++)
            {
                float v = (y + .5f) / FlameHeight;
                // Round bulb at the bottom, tapering to a tip that leans a little per variant.
                float half = v < .3f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((.3f - v) / .3f, 2f)))
                    : Mathf.Pow(Mathf.Max(0f, 1f - (v - .3f) / .7f), 1.25f);
                half *= .92f * (.88f + .24f * Mathf.PerlinNoise(seed, v * 5f));
                float center = lean * v * v * 2f + (Mathf.PerlinNoise(seed + 3f, v * 3f) - .5f) * .18f * v;
                for (int x = 0; x < FlameWidth; x++)
                {
                    float u = (x + .5f) / FlameWidth * 2f - 1f - center;
                    float d = half > .001f ? Mathf.Abs(u) / half : 2f;
                    float alpha = Mathf.Clamp01((1f - d) * 9f) * Mathf.Clamp01((1f - v) * 12f);
                    Color c;
                    if (d > .84f) c = RimColor;
                    else
                    {
                        float heat = Mathf.Clamp01((1f - d) * 1.4f - v * .55f + .1f);
                        c = heat > .62f ? Color.Lerp(MiddleColor, CoreColor, (heat - .62f) / .38f)
                            : Color.Lerp(OuterColor, MiddleColor, heat / .62f);
                    }
                    c.a = alpha;
                    pixels[y * FlameWidth + x] = c;
                }
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, FlameWidth, FlameHeight), new Vector2(.5f, .04f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return flames[variant] = sprite;
        }

        /// <summary>가운데가 밝고 가장자리로 부드럽게 사라지는 둥근 빛 (흰색, 색은 곱해서 쓴다).</summary>
        public static Sprite Glow()
        {
            if (glow != null) return glow;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Jigwi Procedural Glow (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            glow = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            glow.hideFlags = HideFlags.DontSave;
            return glow;
        }
    }
}
