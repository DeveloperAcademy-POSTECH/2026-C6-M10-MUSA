using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #696 지귀 연기 파편 그림. 검은 구슬 하나가 북/서/남/동 네 조각으로 깨진 모습이라, 보자마자 "합치면 동그라미가 되겠다"가 보인다.
    /// - 조각은 늘 제 쪽 자리에 그린다 (북 조각은 동그라미의 위쪽). 그래서 이웃 조각을 붙이면 제자리에 맞물려 반달 → 4분의 3이 된다.
    /// - 깨진 면은 경계마다 모양이 정해져 있어, 이웃 조각의 깨진 면과 퍼즐처럼 꼭 맞는다. 붙은 자리에는 보랏빛 실금이 남는다.
    /// - 빠진 자리는 흐린 점선 테두리로 남아 "여기가 비었다"를 보여 준다.
    /// - 몸통은 검정(먹) + 왼쪽 위 윤기 + 보랏빛 가장자리. 네 쪽이 다 모이면 검정 구슬(LabFiveColorSparkle.SootTones)이 된다.
    /// 그림은 코드로 만들어 한 번만 굽는다 (이어진 덩어리 조합 12가지).
    /// </summary>
    public static class LabSootShardArt
    {
        private const int Size = 128;
        private static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

        /// <summary>
        /// sootPieces(비트: 1 북 / 2 서 / 4 남 / 8 동)가 그리는 조각 그림. 이어지지 않은 조합이면 북 조각 하나로 그린다.
        /// </summary>
        public static Sprite SpriteFor(int pieces) => SpriteFor(pieces, true);

        /// <summary>
        /// #724: ghost false leaves out the faint dashed outline of the missing part (the shards on the board and the pieces in
        /// the empty orb: the empty orb itself shows where they go).
        /// </summary>
        public static Sprite SpriteFor(int pieces, bool ghost) => SpriteFor(pieces, ghost, false);

        /// <summary>
        /// #724 QA: seamless draws the piece with no gap and no light band on its broken faces, so pieces laid side by side in
        /// the empty orb join into one black orb with no pale lines between them.
        /// </summary>
        public static Sprite SpriteFor(int pieces, bool ghost, bool seamless)
        {
            int start = LabModel.SootArcStart(pieces);
            if (start < 0 || pieces == LabModel.SootWholePieces) { pieces = 1; start = 0; }
            int key = pieces | (ghost ? 0 : 256) | (seamless ? 512 : 0);
            if (cache.TryGetValue(key, out Sprite found) && found != null) return found;
            Sprite made = Build(LabModel.SootPieceCount(pieces), start, ghost, seamless);
            cache[key] = made;
            return made;
        }

        /// <summary>
        /// #724: where the drawn piece sits in its picture, in half-widths from the centre (north piece: up). The board draws a
        /// shard bigger and shifted by this so the piece itself sits on (and is as big as) the shard you touch.
        /// </summary>
        public static Vector2 Centroid(int pieces)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int q = 0; q < 4; q++)
            {
                if ((pieces & (1 << q)) == 0) continue;
                float angle = (90f + 90f * q) * Mathf.Deg2Rad;
                sum += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                count++;
            }
            // A 90-degree slice of the disc has its centre of area about 0.6 of the radius out.
            return count == 0 ? Vector2.zero : sum / count * (.6f * Disc);
        }

        private const float Disc = .94f;      // 동그라미 반지름 (그림 반 폭 기준)
        private const float Gap = .035f;      // 깨진 면이 안쪽으로 물러난 폭
        private const float Bevel = .07f;     // 깨진 면을 따라 밝은 띠
        private const float Seam = .02f;      // 합친 자리의 금 두께

        private static readonly Color Ink = new Color(.07f, .065f, .09f, 1f);
        private static readonly Color Lit = new Color(.30f, .28f, .36f, 1f);
        private static readonly Color Edge = new Color(.58f, .52f, .74f, 1f);
        private static readonly Color SeamGlow = new Color(.62f, .48f, .95f, 1f);
        private static readonly Color Ghost = new Color(.20f, .18f, .24f, 1f);

        private static Sprite Build(int level, int turn, bool ghost = true, bool seamless = false)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Lab soot shard " + level + "-" + turn, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[Size * Size];
            // 조각 쪽 번호 q는 (45 + 90q)도에서 시작하는 4분의 1: 0 북, 1 서, 2 남, 3 동 (그림 위쪽이 북).
            float start = 45f + 90f * turn;
            float span = 90f * level;
            float aa = 2.2f / Size;
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                float x = (px + .5f) / Size * 2f - 1f, y = (py + .5f) / Size * 2f - 1f;
                Color pixel = Pixel(x, y, start, span, level, turn, aa, seamless);
                // #724: without the ghost, the missing part (drawn faint and dark) is left fully clear.
                if (!ghost && pixel.a < .5f && pixel.r == Ghost.r && pixel.g == Ghost.g && pixel.b == Ghost.b) pixel = Color.clear;
                pixels[py * Size + px] = pixel;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size);
        }

        /// <summary>
        /// 깨진 면이 곧은 선에서 비켜난 각도(도). 도자기 금처럼 지그재그로 꺾이고, 가운데 꼭짓점 근처는 곧게 모인다.
        /// 면 번호는 동그라미 위의 경계 자리(0 북서 / 1 남서 / 2 남동 / 3 북동, 조각 시작 쪽 기준 절대 번호)라,
        /// 이웃한 두 조각이 같은 경계에서 같은 모양으로 깨져 꼭 맞물린다.
        /// </summary>
        /// <summary>
        /// #724: the broken edge at boundary face (0 북서 / 1 남서 / 2 남동 / 3 북동) at distance r (half-widths) from the
        /// centre, as an angle in degrees: the empty orb draws its cracks on exactly the shards' zigzag edges.
        /// </summary>
        public static float CrackAngle(float r, int face) => 45f + 90f * (face & 3) + Jag(r, face & 3);

        private static float Jag(float r, int face)
        {
            float t = r * 2.4f + face * .31f;
            float zigzag = 2f * Mathf.Abs(2f * (t - Mathf.Floor(t)) - 1f) - 1f;
            float offset = (.05f * zigzag + .012f * Mathf.Sin(r * 19f + face * 2.1f)) * Mathf.Clamp01(r / .3f);
            return Mathf.Rad2Deg * offset / Mathf.Max(r, .05f);
        }

        private static Color Pixel(float x, float y, float start, float span, int level, int turn, float aa, bool seamless = false)
        {
            // Seamless pieces reach a hair past their broken faces so neighbours overlap instead of leaving a pale line.
            float gap = seamless ? -.012f : Gap;
            float r = Mathf.Sqrt(x * x + y * y);
            if (r > Disc + aa) return Color.clear;
            float discCover = Mathf.Clamp01((Disc - r) / aa + .5f);
            float angle = Mathf.Repeat(Mathf.Atan2(y, x) * Mathf.Rad2Deg - start, 360f);
            // 깨진 두 면 (조각의 시작과 끝). 면 번호는 절대 경계 자리라 이웃 조각의 면과 맞물린다.
            float lo = Jag(r, turn & 3), hi = span + Jag(r, (turn + level) & 3);
            float arc = Mathf.Deg2Rad * Mathf.Max(r, .05f);
            float inside = Mathf.Min(angle - lo, hi - angle) * arc;
            // 한 바퀴 넘어간 쪽 (angle이 360 근처인데 lo가 음수)도 조각 안으로 본다.
            if (lo < 0f) inside = Mathf.Max(inside, Mathf.Min(angle - 360f - lo, hi - (angle - 360f)) * arc);
            float cover = Mathf.Clamp01((inside - gap) / aa + .5f) * discCover;
            // 가운데 꼭짓점도 살짝 깎는다.
            if (!seamless) cover *= Mathf.Clamp01((r - .02f) / aa + .5f);
            else if (r < .03f) cover = discCover; // the very middle belongs to every piece

            if (cover <= 0f)
            {
                // 빠진 자리: 흐린 점선 테두리 + 아주 옅은 바탕.
                float rimBand = Mathf.Clamp01(1f - Mathf.Abs(r - (Disc - .04f)) / .025f);
                float dash = Mathf.Repeat(Mathf.Atan2(y, x) * Mathf.Rad2Deg, 14f) < 8f ? 1f : 0f;
                float a = .07f * discCover + .42f * rimBand * dash * discCover;
                return new Color(Ghost.r, Ghost.g, Ghost.b, a);
            }

            // 공처럼 보이게: 왼쪽 위에서 빛.
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - (r / Disc) * (r / Disc)));
            Vector3 n = new Vector3(x / Disc, y / Disc, z);
            float light = Mathf.Clamp01(Vector3.Dot(n, new Vector3(-.45f, .55f, .70f).normalized));
            Color color = Color.Lerp(Ink, Lit, light * light * .8f);
            float spec = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(-.36f, .40f)) / .22f), 2f);
            color = Color.Lerp(color, Color.white, spec * .55f);
            // 동그라미 가장자리 보랏빛 윤광.
            float rim = Mathf.Clamp01(1f - (Disc - r) / .10f);
            color = Color.Lerp(color, Edge, rim * .45f);
            // 깨진 면을 따라 밝은 띠 (돌이 깨진 단면처럼).
            float broken = seamless ? 0f : Mathf.Clamp01(1f - (inside - Gap) / Bevel);
            color = Color.Lerp(color, Edge, broken * broken * .6f);
            // 합친 자리의 금: 조각 안쪽 경계(90°, 180°, 270°)마다 보랏빛 실금.
            for (int face = 1; face < level && !seamless; face++)
            {
                float seamAngle = 90f * face + Jag(r, (turn + face) & 3);
                float d = Mathf.Abs(angle - seamAngle) * arc;
                float glow = Mathf.Clamp01(1f - d / Seam);
                float halo = Mathf.Clamp01(1f - d / (Seam * 4f));
                color = Color.Lerp(color, SeamGlow, Mathf.Max(glow, halo * halo * .35f));
            }
            // 먹선 테두리.
            float outline = Mathf.Clamp01(1f - (Disc - r) / .025f);
            color = Color.Lerp(color, new Color(.03f, .02f, .04f, 1f), outline * .8f);
            color.a = cover;
            return color;
        }
    }
}
