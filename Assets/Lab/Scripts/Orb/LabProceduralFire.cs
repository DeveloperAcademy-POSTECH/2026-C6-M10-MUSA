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
        /// #540 지귀 예고 말풍선 그림 (야광귀 눈처럼 만화풍): 굵은 먹색 테두리를 두른 큰 불꽃 하나와 양옆 작은 불꽃 둘 — 주황 겉불 안에
        /// 노란 속불, 끝이 살짝 휘어 오르고 — 가운데 성난 두 눈(먹색 눈, 흰 반짝임, 가운데로 내려간 눈썹). 색은 면으로 또렷하게 칠한다.
        /// </summary>
        public static Sprite Spirit()
        {
            if (spirit != null) return spirit;
            const int size = 128, samples = 4;
            Color outerFill = new Color32(255, 112, 28, 255), innerFill = new Color32(255, 214, 64, 255);
            Color shine = new Color32(255, 246, 200, 255), white = Color.white;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + .5f) / samples) / size * 2f - 1f;
                            float v = (y + (sy + .5f) / samples) / size;
                            sum += SpiritSample(u, v, outerFill, innerFill, shine, white);
                        }
                    Color c = sum / (samples * samples);
                    // Un-premultiply so soft edges keep their colour.
                    if (c.a > .001f) { c.r /= c.a; c.g /= c.a; c.b /= c.a; }
                    pixels[y * size + x] = c;
                }
            spirit = Make(pixels, size, size, "Jigwi Bubble Spirit");
            return spirit;
        }

        // One premultiplied sample of the spirit at (u: -1..1, v: 0 bottom..1 top).
        private static Color SpiritSample(float u, float v, Color outerFill, Color innerFill, Color shine, Color white)
        {
            const float ink = .065f;
            u /= .9f; v = (v - .07f) / .9f; // a margin all round
            // A big flame with two small ones licking up at its sides.
            float body = Mathf.Min(FlameDistance(u, v, 0f, .4f, .35f, .9f, .16f),
                Mathf.Min(FlameDistance(u, v, -.36f, .24f, .15f, .6f, -.1f), FlameDistance(u, v, .36f, .24f, .15f, .6f, .1f)));
            if (body > ink) return Color.clear;
            Color c = Ink;
            if (body <= 0f)
            {
                c = outerFill;
                if (FlameDistance(u, v, .03f, .3f, .2f, .68f, .1f) <= 0f) c = innerFill;
                // A soft shine low on the left.
                float sx = u + .16f, sy = v - .3f;
                if (sx * sx / (.05f * .05f) + sy * sy / (.12f * .12f) <= 1f) c = shine;
                for (int side = -1; side <= 1; side += 2)
                {
                    // Eyes: dark ovals with a white glint.
                    float ex = u - side * .12f, ey = v - .38f;
                    if (ex * ex / (.055f * .055f) + ey * ey / (.08f * .08f) <= 1f)
                    {
                        c = Ink;
                        float gx = ex + .02f, gy = ey - .035f;
                        if (gx * gx + gy * gy <= .022f * .022f) c = white;
                    }
                    // Angry brows slanting down toward the middle.
                    float bx = u - side * .13f, by = v - .51f;
                    if (Mathf.Abs(bx) <= .07f && Mathf.Abs(by - side * bx * .55f) < .022f) c = Ink;
                }
            }
            c.r *= c.a; c.g *= c.a; c.b *= c.a;
            return c;
        }

        /// <summary>
        /// Signed distance-ish to a cartoon flame: a round bulb (centre cy, radius r) rising to a tip at height top that
        /// curls toward +x by curl. Negative inside.
        /// </summary>
        private static float FlameDistance(float u, float v, float cx, float cy, float r, float top, float curl)
        {
            if (v <= cy)
            {
                float dx = u - cx, dy = v - cy;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            }
            float t = Mathf.Clamp01((v - cy) / Mathf.Max(.001f, top - cy));
            if (v > top) return Mathf.Sqrt((u - cx - curl) * (u - cx - curl) + (v - top) * (v - top));
            float half = r * Mathf.Pow(1f - t, 1.3f) * (1f + .35f * Mathf.Sin(t * Mathf.PI));
            float center = cx + curl * t * t;
            return (Mathf.Abs(u - center) - half) * (1f - .5f * t);
        }

        private static readonly Sprite[] cartoonFlames = new Sprite[FlameVariants];
        private static Sprite cartoonSpark, burnMask;

        /// <summary>
        /// #540 박 위 불덩이 (말풍선 그림과 같은 만화풍 2D, 얼굴 없음): 굵은 먹색 테두리, 주황 겉불 안에 노란 속불, 양옆 작은 불꽃 둘.
        /// 프레임마다 끝이 휘는 방향과 높이가 조금씩 달라 번갈아 보이면 일렁인다.
        /// </summary>
        public static Sprite CartoonFlame(int variant)
        {
            variant = ((variant % FlameVariants) + FlameVariants) % FlameVariants;
            if (cartoonFlames[variant] != null) return cartoonFlames[variant];
            const int size = 128, samples = 3;
            float curl = new[] { .16f, -.1f, .12f, -.14f }[variant], top = new[] { .92f, .86f, .96f, .88f }[variant];
            float left = new[] { .6f, .7f, .52f, .66f }[variant], right = new[] { .66f, .54f, .72f, .58f }[variant];
            Color outerFill = new Color32(255, 112, 28, 255), innerFill = new Color32(255, 214, 64, 255), shine = new Color32(255, 246, 200, 255);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + .5f) / samples) / size * 2f - 1f;
                            float v = (y + (sy + .5f) / samples) / size;
                            sum += CartoonFlameSample(u, v, curl, top, left, right, outerFill, innerFill, shine);
                        }
                    Color c = sum / (samples * samples);
                    if (c.a > .001f) { c.r /= c.a; c.g /= c.a; c.b /= c.a; }
                    pixels[y * size + x] = c;
                }
            cartoonFlames[variant] = Make(pixels, size, size, "Jigwi Cartoon Flame " + variant);
            return cartoonFlames[variant];
        }

        private static Color CartoonFlameSample(float u, float v, float curl, float top, float left, float right,
            Color outerFill, Color innerFill, Color shine)
        {
            const float ink = .06f;
            u /= .9f; v = (v - .05f) / .92f;
            float body = Mathf.Min(FlameDistance(u, v, 0f, .38f, .36f, top, curl),
                Mathf.Min(FlameDistance(u, v, -.37f, .24f, .15f, left, -.1f), FlameDistance(u, v, .37f, .24f, .15f, right, .1f)));
            if (body > ink) return Color.clear;
            Color c = Ink;
            if (body <= 0f)
            {
                c = outerFill;
                if (FlameDistance(u, v, .02f, .3f, .21f, top * .72f, curl * .6f) <= 0f) c = innerFill;
                float sx = u + .15f, sy = v - .28f;
                if (sx * sx / (.05f * .05f) + sy * sy / (.12f * .12f) <= 1f) c = shine;
            }
            c.r *= c.a; c.g *= c.a; c.b *= c.a;
            return c;
        }

        /// <summary>#540 만화풍 불티: 먹색 테두리를 두른 노란 점 (불덩이 둘레에 튄다).</summary>
        public static Sprite CartoonSpark()
        {
            if (cartoonSpark != null) return cartoonSpark;
            const int size = 32;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = d <= .62f ? (Color)new Color32(255, 214, 64, 255) : Ink;
                    c.a = Mathf.Clamp01((.9f - d) * size * .5f);
                    pixels[y * size + x] = c;
                }
            cartoonSpark = Make(pixels, size, size, "Jigwi Cartoon Spark");
            return cartoonSpark;
        }

        /// <summary>
        /// #540 구슬이 아래부터 타 없어질 때 쓰는 가림막 (SpriteMask): 위 가장자리가 불탄 종이처럼 들쭉날쭉한 세로로 긴 판.
        /// 피벗이 위 가운데라 위치가 곧 타 들어가는 선이다. 1×2 단위.
        /// </summary>
        public static Sprite BurnMask()
        {
            if (burnMask != null) return burnMask;
            const int width = 64, height = 128;
            var pixels = new Color[width * height];
            for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width;
                float edge = 1f - .035f - .025f * (Mathf.Sin(u * 31f) * .6f + Mathf.Sin(u * 67f + 1.3f) * .4f);
                for (int y = 0; y < height; y++)
                    pixels[y * width + x] = new Color(1f, 1f, 1f, (y + .5f) / height <= edge ? 1f : 0f);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Jigwi Burn Mask (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels); texture.Apply(false, true);
            burnMask = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, 1f), width);
            burnMask.hideFlags = HideFlags.DontSave;
            return burnMask;
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
