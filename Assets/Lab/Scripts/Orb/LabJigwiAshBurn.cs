using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #540 지귀 A 실패 뒤 불티 (부적 가장자리가 타 들어갈 때와 같은 느낌, 큰 불꽃 없음). 코드로만 그린다.
    /// - 구슬: 아래에서 위로 타 들어가는 선(밝은 불씨 줄 + 주황 빛)이 올라가며, 선 아래는 사라지고(가림막) 선 위는 점점 그을린다.
    ///   선에서 불티가 튀어 오르고 재가 떨어진다. 남은 초가 0이 되는 순간 맨 위까지 다 타 없어진다.
    /// - 북: 부적이 탈 때와 같이 겉 테두리부터 안쪽으로 그을림과 불씨 줄이 번져 들어오다 테두리 쪽 일부에서 멈추고,
    ///   패널티 동안 그 모습(그을린 테두리 + 깜빡이는 불씨 줄 + 오르는 불티)이 그대로 유지된다. 북은 타 없어지지 않는다.
    /// Host가 준 남은 초만 따른다(그림만, 판정 없음). 구슬 뿌리에 붙어 공이 굴러도 돌지 않는다. 일시정지(시간 0)면 멈춘다.
    /// </summary>
    public sealed class LabJigwiAshBurn : MonoBehaviour
    {
        private const int SparkCount = 9, AshCount = 6;
        private const float FadeSeconds = .3f;
        private static readonly Color EmberColor = new Color(1f, .5f, .12f, 1f), HotColor = new Color(1f, .9f, .5f, 1f);
        private static readonly Color AshColor = new Color(.32f, .3f, .3f, .85f);
        /// <summary>부적이 탈 때와 같은 색 (그을린 자리, 불 테두리 안쪽의 밝은 불씨).</summary>
        private static readonly Color CharColor = new Color(.13f, .07f, .05f, .92f), EdgeHot = new Color(1f, .88f, .45f, 1f);
        /// <summary>북: 겉에서 안쪽으로 이만큼(반지름 비율)까지 타 들어오는 데 걸리는 시간과 멈추는 깊이, 불씨 줄 두께.</summary>
        private const float DrumCatchSeconds = 1.2f, DrumDepth = .24f, DrumBand = .07f, DrumHalo = .05f, PaintGap = .05f;
        private const int ScorchSize = 64;
        private SpriteRenderer scorch;
        private Texture2D scorchTexture;
        private Color32[] scorchPixels;
        private float[] scorchDepth, scorchAngle;
        private float lastPaint = -1f;
        private SpriteRenderer lineHalo, lineCore;
        /// <summary>
        /// 구슬이 타는 가장자리 그림 (부적이 탈 때와 같은 그림을 구슬 모양에 맞춰): 타 들어가는 줄은 밝은 불씨(깜빡임),
        /// 그 바로 위는 검게 그을린 띠, 줄 둘레로 주황 빛. 줄 아래는 비어 있다(가림막이 구슬을 지움).
        /// </summary>
        private SpriteRenderer edge;
        private Texture2D edgeTexture;
        private Color32[] edgePixels;
        private const int EdgeSize = 64, FlameCount = 6;
        private const float EdgeBand = .08f, EdgeChar = .12f, EdgeHalo = .1f;
        private readonly SpriteRenderer[] flames = new SpriteRenderer[FlameCount];
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[SparkCount], ash = new SpriteRenderer[AshCount];
        private SpriteRenderer[] masked = Array.Empty<SpriteRenderer>();
        private Transform shrinking;
        private Vector3 shrinkFrom = Vector3.one;
        private Color[] maskedColors = Array.Empty<Color>();

        private float left, total = 3f, radius = .3f, age;
        private bool consume, shown;

        public bool Burning => left > 0f;
        /// <summary>구슬이 타 들어간 정도 (0 = 아래 끝, 1 = 다 탐). 북은 0.</summary>
        public float Front { get; private set; }
        public bool Consumes => consume;

        public static LabJigwiAshBurn Create(Transform orbRoot)
        {
            var go = new GameObject("Jigwi Ash Burn (#540)");
            go.layer = orbRoot.gameObject.layer;
            go.transform.SetParent(orbRoot, false);
            return go.AddComponent<LabJigwiAshBurn>();
        }

        /// <summary>
        /// 남은 초(0이면 멈춤). consumeOrb면 totalSeconds 동안 아래부터 타 없어지는 구슬(visuals 아래 그림을 가린다), 아니면 불티가 붙은 북.
        /// </summary>
        public void Show(float secondsLeft, float totalSeconds, float orbRadius, bool consumeOrb, Transform visuals)
        {
            if (orbRadius > 0f) radius = orbRadius;
            if (!(secondsLeft > 0f) || float.IsInfinity(secondsLeft) || float.IsNaN(secondsLeft))
            {
                // A drum's embers die down; an orb that finished burning stays gone until it is removed.
                if (left > 0f && !consume) left = Mathf.Min(left, FadeSeconds);
                return;
            }
            Ensure();
            if (left <= 0f) { age = 0f; lastPaint = -1f; consume = consumeOrb; if (consume) Mask(visuals, true); }
            total = Mathf.Max(.01f, totalSeconds);
            if (left <= 0f || Mathf.Abs(left - secondsLeft) > .3f) left = secondsLeft;
            SetShown(true);
            Pose();
        }

        private void Ensure()
        {
            if (lineHalo != null) return;
            lineHalo = Part("Ash Burn Line Halo", LabProceduralFire.Glow(), EmberColor, 10);
            lineCore = Part("Ash Burn Line Core", LabProceduralFire.Glow(), HotColor, 11);
            for (int i = 0; i < AshCount; i++) ash[i] = Part("Ash Burn Ash " + i, LabProceduralFire.Glow(), AshColor, 11);
            for (int i = 0; i < SparkCount; i++) sparks[i] = Part("Ash Burn Spark " + i, LabProceduralFire.Glow(), HotColor, 12);
            edgeTexture = new Texture2D(EdgeSize, EdgeSize, TextureFormat.RGBA32, false)
            { name = "Jigwi Orb Burn Edge (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            edgePixels = new Color32[EdgeSize * EdgeSize];
            var edgeSprite = Sprite.Create(edgeTexture, new Rect(0f, 0f, EdgeSize, EdgeSize), new Vector2(.5f, .5f), EdgeSize);
            edgeSprite.hideFlags = HideFlags.DontSave;
            edge = Part("Ash Burn Edge", edgeSprite, Color.white, 9);
            for (int i = 0; i < FlameCount; i++) flames[i] = Part("Ash Burn Flame " + i, LabProceduralFire.Flame(i), Color.white, 12);
        }

        private SpriteRenderer Part(string label, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(label) { layer = gameObject.layer };
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>
        /// #540 타기 시작할 때 구슬 그림(visuals)을 잡아 둔다: 그림 전체가 둘레부터 줄어들게(크기) 하고, 색을 조금씩 그을린다.
        /// 끄면 원래 크기·색으로 되돌린다. (가림막 SpriteMask는 구슬 그림 재질에 따라 듣지 않아 쓰지 않는다.)
        /// </summary>
        private void Mask(Transform visuals, bool on)
        {
            if (on)
            {
                shrinking = visuals;
                shrinkFrom = visuals != null ? visuals.localScale : Vector3.one;
                masked = visuals != null ? visuals.GetComponentsInChildren<SpriteRenderer>(true) : Array.Empty<SpriteRenderer>();
                maskedColors = new Color[masked.Length];
                for (int i = 0; i < masked.Length; i++) maskedColors[i] = masked[i].color;
                return;
            }
            for (int i = 0; i < masked.Length; i++)
                if (masked[i] != null) masked[i].color = maskedColors[i];
            masked = Array.Empty<SpriteRenderer>();
            if (shrinking != null) shrinking.localScale = shrinkFrom;
            shrinking = null;
        }

        private void SetShown(bool on)
        {
            if (shown == on || lineHalo == null) return;
            shown = on;
            if (!on)
                foreach (SpriteRenderer part in GetComponentsInChildren<SpriteRenderer>(true)) part.enabled = false;
        }

        private void Update()
        {
            if (left <= 0f) return;
            float step = Time.deltaTime;
            left = Mathf.Max(0f, left - step);
            age += step;
            if (left <= 0f && !consume) { SetShown(false); return; }
            Pose();
        }

        private void Pose()
        {
            if (lineHalo == null) return;
            if (consume) PoseOrb(); else PoseDrum();
        }

        private void PoseOrb()
        {
            float p = Mathf.Clamp01(1f - left / total);
            Front = p;
            float r = radius;
            // #540: the same burn as the talisman's edge — the fire catches all round the rim and eats in to the middle.
            // Behind the charred band the ball is gone (the mask); the burning ring glows, small flames stand on it,
            // sparks fly up and ash drops.
            float front = Mathf.Lerp(0f, 1.15f, p); // how far in from the rim (ball radii)
            float cut = front - EdgeBand - EdgeChar; // burnt through up to here
            float remaining = Mathf.Max(0f, 1f - cut) * r;
            float ring = Mathf.Max(0f, 1f - front) * r; // where the burning line is
            float flicker = .8f + .2f * Mathf.PerlinNoise(age * 9f, .3f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.94f, 1f, p)); // fades out over the last 6%
            // The board camera looks from -z and the orb picture sits one radius toward it (z = -radius), so the
            // burning edge goes a little further toward the camera or the picture hides it.
            float depth = -r * 1.15f;

            // The ball itself shrinks from the rim in as it burns (no mask needed), and darkens a little.
            float share = Mathf.Clamp01(remaining / Mathf.Max(.0001f, r));
            if (shrinking != null) shrinking.localScale = shrinkFrom * share;
            float charring = Mathf.SmoothStep(0f, 1f, p) * .4f;
            for (int i = 0; i < masked.Length; i++)
            {
                if (masked[i] == null) continue;
                Color c = maskedColors[i];
                masked[i].color = Color.Lerp(c, new Color(c.r * .3f, c.g * .2f, c.b * .16f, c.a), charring);
            }

            if (lastPaint < 0f || age - lastPaint >= .03f) { lastPaint = age; PaintEdge(front, fade); }
            edge.transform.localPosition = new Vector3(0f, 0f, depth);
            edge.transform.localScale = Vector3.one * (r * 2f);
            edge.enabled = fade > .002f;
            Tint(lineHalo, EmberColor, 0f); Tint(lineCore, HotColor, 0f);
            if (scorch != null) scorch.enabled = false;

            bool burning = ring > r * .06f;
            for (int i = 0; i < FlameCount; i++)
            {
                // Small flames stand on the burning ring, like the talisman's edge flames.
                SpriteRenderer flame = flames[i];
                flame.sprite = LabProceduralFire.Flame((int)(age * 12f) + i * 2);
                float angle = (i + .5f) / FlameCount * Mathf.PI * 2f + age * .6f;
                float lick = .75f + .35f * Mathf.Sin(age * (17f + i * 3f) + i);
                float height = r * .4f * lick * Mathf.Clamp01(ring / (r * .3f));
                Vector2 size = flame.sprite.bounds.size;
                flame.transform.localScale = new Vector3(height * .7f / Mathf.Max(.0001f, size.x), height / Mathf.Max(.0001f, size.y), 1f);
                flame.transform.localPosition = new Vector3(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring + height * .3f, depth - .015f);
                Tint(flame, Color.white, .95f * fade * (burning ? 1f : 0f));
            }
            for (int i = 0; i < SparkCount; i++)
            {
                // Sparks flick up off the burning ring.
                float cycle = age / .6f + i * .37f, t = Mathf.Repeat(cycle, 1f);
                float angle = Hash(i, Mathf.Floor(cycle)) * Mathf.PI * 2f;
                var from = new Vector3(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring, depth - .02f);
                Place(sparks[i], from + new Vector3(Mathf.Sin(t * 6f + i) * r * .08f, t * r * .8f, 0f), r * .13f * (1f - .7f * t));
                Tint(sparks[i], Color.Lerp(HotColor, EmberColor, t), (1f - t) * fade * (burning ? 1f : 0f));
            }
            for (int i = 0; i < AshCount; i++)
            {
                // Grey ash flakes drop away from the burnt rim.
                float cycle = age / .7f + i * .53f, t = Mathf.Repeat(cycle, 1f);
                float angle = Hash(i + 17, Mathf.Floor(cycle)) * Mathf.PI * 2f;
                var from = new Vector3(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring, depth - .02f);
                Place(ash[i], from + new Vector3(Mathf.Sin(t * 4f + i) * r * .06f, -t * r * .6f, 0f), r * .1f);
                Tint(ash[i], AshColor, (1f - t) * fade * (burning ? 1f : 0f));
            }
        }

        /// <summary>
        /// 부적 가장자리가 탈 때와 같은 그림을 구슬 원 안에 그린다. front는 가장자리에서 안쪽으로 타 들어간 정도(구슬 반지름 단위).
        /// 불 줄은 조금 들쭉날쭉하고 깜빡인다. 줄 바깥은 그을린 띠(그 바깥은 가림막이 지움), 줄은 밝은 불씨, 줄 안쪽으로 옅은 주황 빛.
        /// </summary>
        private void PaintEdge(float front, float fade)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < EdgeSize; y++)
                for (int x = 0; x < EdgeSize; x++)
                {
                    int i = y * EdgeSize + x;
                    float u = (x + .5f) / EdgeSize * 2f - 1f, v = (y + .5f) / EdgeSize * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    if (d > 1f) { edgePixels[i] = clear; continue; }
                    float a = Mathf.Atan2(v, u);
                    float jag = .04f * Mathf.Sin(a * 7f + age * 3f) + .025f * Mathf.Sin(a * 13f - age * 5f);
                    float k = (1f - d) + jag - front; // < 0: outside the burning line (burnt side)
                    Color c;
                    if (k < -(EdgeBand + EdgeChar)) { edgePixels[i] = clear; continue; }
                    if (k < -EdgeBand)
                    {
                        float shade = .85f + .15f * ((i * 7919) % 97) / 97f;
                        c = CharColor; c.r *= shade; c.g *= shade; c.b *= shade;
                    }
                    else if (k < 0f)
                    {
                        float glow = .75f + .25f * Mathf.Sin(a * 5f + age * 11f);
                        c = Color.Lerp(EdgeHot, EmberColor, -k / EdgeBand);
                        c.r *= glow; c.g *= glow; c.b *= glow;
                    }
                    else if (k < EdgeHalo) { c = EmberColor; c.a = .45f * (1f - k / EdgeHalo); }
                    else { edgePixels[i] = clear; continue; }
                    c.a *= fade;
                    edgePixels[i] = c;
                }
            edgeTexture.SetPixels32(edgePixels);
            edgeTexture.Apply(false, false);
        }

        private void PoseDrum()
        {
            Front = 0f;
            float r = radius;
            float fade = Mathf.Clamp01(left / FadeSeconds);
            // Like the talisman: the burn creeps in from the rim, then holds at a charred band near the edge.
            float caught = Mathf.Clamp01(age / DrumCatchSeconds);
            float front = DrumDepth * (1f - (1f - caught) * (1f - caught)) + .015f * Mathf.Sin(age * 2.3f) * caught;
            EnsureScorch();
            if (lastPaint < 0f || age - lastPaint >= PaintGap) { lastPaint = age; PaintScorch(front, fade); }
            // The drum art sits at z = -1.02 × radius (toward the board camera at -z); the scorch goes just in front of it.
            float near = -r * 1.1f;
            scorch.transform.localPosition = new Vector3(0f, 0f, near);
            scorch.transform.localScale = Vector3.one * Scale(scorch, r * 2.04f);
            scorch.enabled = fade > .002f;
            if (edge != null) edge.enabled = false;
            foreach (SpriteRenderer flame in flames) Tint(flame, Color.white, 0f);
            Tint(lineHalo, EmberColor, 0f); Tint(lineCore, HotColor, 0f);
            for (int i = 0; i < AshCount; i++) Tint(ash[i], AshColor, 0f);
            float inner = r * (1f - front);
            for (int i = 0; i < SparkCount; i++)
            {
                // Sparks flick off the burning line all round the drum and drift up.
                float cycle = age / .85f + i * .41f, t = Mathf.Repeat(cycle, 1f);
                float angle = Hash(i, Mathf.Floor(cycle)) * Mathf.PI * 2f;
                var from = new Vector3(Mathf.Cos(angle) * inner, Mathf.Sin(angle) * inner, near - .02f);
                Place(sparks[i], from + new Vector3(Mathf.Sin(t * 5f + i) * r * .08f, t * r * .55f, 0f), r * .13f * (1f - .7f * t));
                Tint(sparks[i], Color.Lerp(HotColor, EmberColor, t), (1f - t) * fade * caught);
            }
        }

        private void EnsureScorch()
        {
            if (scorch != null) return;
            scorchTexture = new Texture2D(ScorchSize, ScorchSize, TextureFormat.RGBA32, false)
            { name = "Jigwi Drum Scorch (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            scorchPixels = new Color32[ScorchSize * ScorchSize];
            scorchDepth = new float[ScorchSize * ScorchSize];
            scorchAngle = new float[ScorchSize * ScorchSize];
            for (int y = 0; y < ScorchSize; y++)
                for (int x = 0; x < ScorchSize; x++)
                {
                    float dx = (x + .5f) / ScorchSize * 2f - 1f, dy = (y + .5f) / ScorchSize * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    int i = y * ScorchSize + x;
                    scorchAngle[i] = a;
                    // How far in from the rim, with a ragged burnt-paper edge. Outside the drum stays clear.
                    scorchDepth[i] = d > 1f ? -1f : 1f - d + .025f * (Mathf.Sin(a * 7f) + .6f * Mathf.Sin(a * 13f + 1.7f));
                }
            var sprite = Sprite.Create(scorchTexture, new Rect(0f, 0f, ScorchSize, ScorchSize), new Vector2(.5f, .5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            scorch = Part("Ash Burn Drum Scorch", sprite, Color.white, 9);
        }

        /// <summary>부적이 탈 때와 같은 그림: 테두리~불 줄 안쪽은 그을림, 줄은 밝은 불씨(깜빡임), 줄 안쪽으로 옅은 주황 빛.</summary>
        private void PaintScorch(float front, float fade)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < scorchDepth.Length; i++)
            {
                float d = scorchDepth[i];
                Color c;
                if (d < 0f) { scorchPixels[i] = clear; continue; }
                if (d < front - DrumBand)
                {
                    float shade = .85f + .15f * ((i * 7919) % 97) / 97f;
                    c = CharColor; c.r *= shade; c.g *= shade; c.b *= shade;
                }
                else if (d < front)
                {
                    float glow = .7f + .3f * Mathf.Sin(scorchAngle[i] * 5f + age * 6f) * Mathf.Sin(scorchAngle[i] * 3f - age * 4f);
                    c = Color.Lerp(EdgeHot, EmberColor, (front - d) / DrumBand);
                    c.r *= glow; c.g *= glow; c.b *= glow;
                }
                else if (d < front + DrumHalo) { c = EmberColor; c.a = .45f * (1f - (d - front) / DrumHalo); }
                else { scorchPixels[i] = clear; continue; }
                c.a *= fade;
                scorchPixels[i] = c;
            }
            scorchTexture.SetPixels32(scorchPixels);
            scorchTexture.Apply(false, false);
        }

        private static float Hash(int i, float n) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + n * 78.233f) * 43758.5453f, 1f);

        private static void Place(SpriteRenderer renderer, Vector3 at, float width)
        {
            renderer.transform.localPosition = at;
            renderer.transform.localScale = Vector3.one * Scale(renderer, width);
        }

        private static void Stretch(SpriteRenderer renderer, float width, float height, Vector3 at)
        {
            Vector2 size = renderer.sprite.bounds.size;
            renderer.transform.localPosition = at;
            renderer.transform.localScale = new Vector3(width / Mathf.Max(.0001f, size.x), height / Mathf.Max(.0001f, size.y), 1f);
        }

        private static float Scale(SpriteRenderer renderer, float width) =>
            width / Mathf.Max(.0001f, renderer.sprite.bounds.size.x);

        private static void Tint(SpriteRenderer renderer, Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            renderer.color = color;
            renderer.enabled = color.a > .002f;
        }

        private void OnDestroy()
        {
            Mask(null, false);
            if (scorchTexture != null) Destroy(scorchTexture);
            if (edgeTexture != null) Destroy(edgeTexture);
        }
    }
}
