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

        private static Sprite spirit, alarm;
        private static readonly Color Ink = new Color32(43, 36, 32, 255);

        /// <summary>
        /// #540 지귀 예고 말풍선 그림: 먹색 테두리를 두른 불꽃 하나에 성난 두 눈. 아래 가운데가 둥글고 위가 세 갈래로 솟는다.
        /// </summary>
        public static Sprite Spirit()
        {
            if (spirit != null) return spirit;
            const int size = 96, samples = 3;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float outer = 0f, inner = 0f, eye = 0f, heatSum = 0f;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + .5f) / samples) / size * 2f - 1f, v = (y + (sy + .5f) / samples) / size;
                            float w = SpiritHalf(u, v);
                            if (Mathf.Abs(u) <= w) outer++;
                            if (Mathf.Abs(u) <= w - .09f && v > .07f) { inner++; heatSum += Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(.01f, w) * .9f - v * .6f + .25f); }
                            // Two angry eyes, slanting down to the middle.
                            for (int side = -1; side <= 1; side += 2)
                            {
                                float ex = u - side * .2f, ey = v - .36f;
                                bool inEye = ex * ex / (.1f * .1f) + ey * ey / (.075f * .075f) <= 1f;
                                bool brow = ey > .03f - side * ex * .9f; // cut off by a slanted brow
                                if (inEye && !brow) eye++;
                            }
                        }
                    float n = samples * samples;
                    float a = outer / n;
                    float fill = outer > 0f ? inner / outer : 0f;
                    float heat = inner > 0f ? heatSum / inner : 0f;
                    Color hot = heat > .55f ? Color.Lerp(MiddleColor, CoreColor, (heat - .55f) / .45f) : Color.Lerp(OuterColor, MiddleColor, heat / .55f);
                    Color c = Color.Lerp(Ink, hot, fill);
                    c = Color.Lerp(c, Ink, Mathf.Clamp01(eye / n));
                    c.a = a;
                    pixels[y * size + x] = c;
                }
            spirit = Make(pixels, size, size, "Jigwi Bubble Spirit");
            return spirit;
        }

        // Half-width of the spirit flame at height v (0 bottom → 1 top): a round bulb with three licking tips.
        private static float SpiritHalf(float u, float v)
        {
            if (v < .05f || v > .98f) return -1f;
            float bulb = v < .42f ? .62f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((.42f - v) / .37f, 2f))) : .62f * (1f - (v - .42f) / .58f);
            // Three tips: the middle tallest, notches between them.
            float tips = Mathf.Abs(Mathf.Sin((u * 1.6f + .5f) * Mathf.PI * 1.5f));
            float top = v > .55f ? Mathf.Lerp(1f, .55f + .45f * tips, Mathf.Clamp01((v - .55f) / .3f)) : 1f;
            return bulb * top + (v > .55f && Mathf.Abs(u) < .15f ? .08f * (1f - v) : 0f);
        }

        /// <summary>
        /// #540 공격 영역 경보 빛: 가장자리가 붉게 짙고 가운데로 옅어지는 사각 빛 (늘여서 쓴다).
        /// </summary>
        public static Sprite Alarm()
        {
            if (alarm != null) return alarm;
            const int width = 64, height = 64;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / width * 2f - 1f), dy = Mathf.Abs((y + .5f) / height * 2f - 1f);
                    float edge = Mathf.Max(dx, dy);
                    float a = .25f + .75f * Mathf.SmoothStep(.35f, 1f, edge);
                    pixels[y * width + x] = new Color(1f, .08f, .02f, a);
                }
            alarm = Make(pixels, width, height, "Jigwi Alarm Glow");
            return alarm;
        }

        private static Sprite Make(Color[] pixels, int width, int height, string label)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = label + " (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels); texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
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
