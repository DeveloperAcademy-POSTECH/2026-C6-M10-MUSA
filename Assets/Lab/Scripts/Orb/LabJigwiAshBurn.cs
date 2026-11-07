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
        private SpriteMask mask;
        private SpriteRenderer lineHalo, lineCore;
        /// <summary>
        /// 구슬이 타는 가장자리 그림 (부적이 탈 때와 같은 그림을 구슬 모양에 맞춰): 타 들어가는 줄은 밝은 불씨(깜빡임),
        /// 그 바로 위는 검게 그을린 띠, 줄 둘레로 주황 빛. 줄 아래는 비어 있다(가림막이 구슬을 지움).
        /// </summary>
        private SpriteRenderer edge;
        private Texture2D edgeTexture;
        private Color32[] edgePixels;
        private const int EdgeSize = 64, FlameCount = 4;
        private const float EdgeBand = .09f, EdgeChar = .3f, EdgeHalo = .12f;
        private readonly SpriteRenderer[] flames = new SpriteRenderer[FlameCount];
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[SparkCount], ash = new SpriteRenderer[AshCount];
        private SpriteRenderer[] masked = Array.Empty<SpriteRenderer>();
        private Color[] maskedColors = Array.Empty<Color>();
        private int[] maskedOrders = Array.Empty<int>();
        /// <summary>
        /// 이 구슬만의 정렬 번호 칸. 가림막(SpriteMask)은 원래 그 아래 모든 구슬 그림을 가리므로, 타는 동안 이 구슬 그림만
        /// 이 칸으로 옮기고 가림막도 이 칸에만 듣게 한다 (여러 구슬이 함께 탈 때 옆 구슬이 잘려 보이지 않게).
        /// </summary>
        private const int SlotBase = 200, SlotStride = 40, SlotCount = 200;
        private static int nextSlot;
        private int slotOrder, partOrder;
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
            var maskObject = new GameObject("Ash Burn Mask") { layer = gameObject.layer };
            maskObject.transform.SetParent(transform, false);
            mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = LabProceduralFire.BurnMask();
            mask.enabled = false;
            lineHalo = Part("Ash Burn Line Halo", LabProceduralFire.Glow(), EmberColor, 10);
            lineCore = Part("Ash Burn Line Core", LabProceduralFire.Glow(), HotColor, 11);
            for (int i = 0; i < AshCount; i++) ash[i] = Part("Ash Burn Ash " + i, LabProceduralFire.Glow(), AshColor, 11);
            for (int i = 0; i < SparkCount; i++) sparks[i] = Part("Ash Burn Spark " + i, LabProceduralFire.Glow(), HotColor, 12);
            edgeTexture = new Texture2D(EdgeSize, EdgeSize, TextureFormat.RGBA32, false)
            { name = "Jigwi Orb Burn Edge (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            edgePixels = new Color32[EdgeSize * EdgeSize];
            var edgeSprite = Sprite.Create(edgeTexture, new Rect(0f, 0f, EdgeSize, EdgeSize), new Vector2(.5f, .5f), EdgeSize);
            edgeSprite.hideFlags = HideFlags.DontSave;
            edge = Part("Ash Burn Edge", edgeSprite, Color.white, 10);
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

        /// <summary>구슬 그림(스프라이트)이 가림막 밖에서만 보이게 한다. 끄면 원래대로.</summary>
        private void Mask(Transform visuals, bool on)
        {
            if (on)
            {
                masked = visuals != null ? visuals.GetComponentsInChildren<SpriteRenderer>(true) : Array.Empty<SpriteRenderer>();
                maskedColors = new Color[masked.Length];
                maskedOrders = new int[masked.Length];
                int low = int.MaxValue, high = int.MinValue, layer = 0;
                foreach (SpriteRenderer part in masked) { low = Mathf.Min(low, part.sortingOrder); high = Mathf.Max(high, part.sortingOrder); layer = part.sortingLayerID; }
                if (masked.Length == 0) { low = high = 0; }
                slotOrder = SlotBase + (nextSlot++ % SlotCount) * SlotStride;
                int span = Mathf.Clamp(high - low, 0, SlotStride - 8);
                for (int i = 0; i < masked.Length; i++)
                {
                    maskedColors[i] = masked[i].color;
                    maskedOrders[i] = masked[i].sortingOrder;
                    // Keep the orb's own layering, just moved into its private slot.
                    masked[i].sortingOrder = slotOrder + Mathf.Clamp(masked[i].sortingOrder - low, 0, span);
                    masked[i].maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                }
                if (mask != null)
                {
                    mask.isCustomRangeActive = true;
                    mask.frontSortingLayerID = mask.backSortingLayerID = layer;
                    mask.backSortingOrder = slotOrder - 1;
                    mask.frontSortingOrder = slotOrder + span;
                    mask.enabled = true;
                }
                // The burning line, sparks and ash draw just above this orb's slot.
                partOrder = slotOrder + span + 1;
                SortParts(layer);
                return;
            }
            for (int i = 0; i < masked.Length; i++)
                if (masked[i] != null)
                {
                    masked[i].maskInteraction = SpriteMaskInteraction.None;
                    masked[i].color = maskedColors[i];
                    masked[i].sortingOrder = maskedOrders[i];
                }
            masked = Array.Empty<SpriteRenderer>();
            if (mask != null) mask.enabled = false;
        }

        private void SortParts(int layer)
        {
            void Put(SpriteRenderer part, int extra) { if (part != null) { part.sortingLayerID = layer; part.sortingOrder = partOrder + extra; } }
            Put(edge, 0); Put(lineHalo, 1); Put(lineCore, 2);
            foreach (SpriteRenderer flake in ash) Put(flake, 2);
            foreach (SpriteRenderer flame in flames) Put(flame, 3);
            foreach (SpriteRenderer spark in sparks) Put(spark, 4);
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
            // Like the talisman's edge: a glowing line climbs from just under the ball to just over it. Below the line the
            // ball is gone (the mask); right above it a charred band; small flames lick up along it; sparks fly, ash drops.
            float lineAt = Mathf.Lerp(-1.1f, 1.1f, p); // in ball radii
            float y = lineAt * r;
            float half = Mathf.Sqrt(Mathf.Max(0f, r * r - y * y));
            float flicker = .8f + .2f * Mathf.PerlinNoise(age * 9f, .3f);
            float fade = 1f - Mathf.SmoothStep(.94f, 1f, p);
            float front = -r * 1.1f; // in front of the orb picture (it sits one radius toward the camera)

            mask.transform.localPosition = new Vector3(0f, y - r * EdgeBand * .5f, 0f);
            mask.transform.localScale = new Vector3(r * 2.6f, r * 1.2f, 1f);
            // The whole ball darkens a little as the fire climbs.
            float charring = Mathf.SmoothStep(0f, 1f, p) * .4f;
            for (int i = 0; i < masked.Length; i++)
            {
                if (masked[i] == null) continue;
                Color c = maskedColors[i];
                masked[i].color = Color.Lerp(c, new Color(c.r * .3f, c.g * .2f, c.b * .16f, c.a), charring);
            }

            if (lastPaint < 0f || age - lastPaint >= .03f) { lastPaint = age; PaintEdge(lineAt, fade); }
            edge.transform.localPosition = new Vector3(0f, 0f, front);
            edge.transform.localScale = Vector3.one * (r * 2f);
            edge.enabled = fade > .002f;

            float width = half * 2.15f + r * .12f;
            Stretch(lineHalo, width * 1.2f, r * .5f, new Vector3(0f, y, front - .005f));
            Tint(lineHalo, EmberColor, .6f * flicker * fade);
            Tint(lineCore, HotColor, 0f);
            if (scorch != null) scorch.enabled = false;

            for (int i = 0; i < FlameCount; i++)
            {
                // Small flames lick up along the burning line, like the talisman's edge flames.
                SpriteRenderer flame = flames[i];
                flame.sprite = LabProceduralFire.Flame((int)(age * 12f) + i * 2);
                float along = (i + .5f) / FlameCount * 2f - 1f;
                float lick = .75f + .35f * Mathf.Sin(age * (17f + i * 3f) + i);
                float height = r * .55f * lick * Mathf.Clamp01(half / (r * .35f));
                Vector2 size = flame.sprite.bounds.size;
                flame.transform.localScale = new Vector3(height * .7f / Mathf.Max(.0001f, size.x), height / Mathf.Max(.0001f, size.y), 1f);
                flame.transform.localPosition = new Vector3(along * half * .8f, y + height * .35f, front - .015f);
                Tint(flame, Color.white, .95f * fade * (half > r * .08f ? 1f : 0f));
            }
            for (int i = 0; i < SparkCount; i++)
            {
                // Sparks flick up off the burning line.
                float rate = 1f / .6f, cycle = age * rate + i * .37f, t = Mathf.Repeat(cycle, 1f);
                float x = (Hash(i, Mathf.Floor(cycle)) * 2f - 1f) * half;
                Place(sparks[i], new Vector3(x + Mathf.Sin(t * 6f + i) * r * .1f, y + t * r * 1.1f, front - .02f), r * .14f * (1f - .7f * t));
                Tint(sparks[i], Color.Lerp(HotColor, EmberColor, t), (1f - t) * fade * (half > r * .05f ? 1f : 0f));
            }
            for (int i = 0; i < AshCount; i++)
            {
                // Grey ash flakes drop away under the line.
                float cycle = age / .7f + i * .53f, t = Mathf.Repeat(cycle, 1f);
                float x = (Hash(i + 17, Mathf.Floor(cycle)) * 2f - 1f) * half;
                Place(ash[i], new Vector3(x + Mathf.Sin(t * 4f + i) * r * .08f, y - t * r * .7f, front - .02f), r * .1f);
                Tint(ash[i], AshColor, (1f - t) * fade * (half > r * .05f ? 1f : 0f));
            }
        }

        /// <summary>
        /// 부적이 탈 때와 같은 그림을 구슬 모양(원) 안에 그린다. lineAt은 타 들어가는 줄 높이(구슬 반지름 단위, 아래 -1 ~ 위 1).
        /// 줄은 조금 들쭉날쭉하고 깜빡인다. 줄 아래는 비우고, 줄은 밝은 불씨, 그 위는 그을린 띠와 주황 빛.
        /// </summary>
        private void PaintEdge(float lineAt, float fade)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < EdgeSize; y++)
                for (int x = 0; x < EdgeSize; x++)
                {
                    int i = y * EdgeSize + x;
                    float u = (x + .5f) / EdgeSize * 2f - 1f, v = (y + .5f) / EdgeSize * 2f - 1f;
                    if (u * u + v * v > 1f) { edgePixels[i] = clear; continue; }
                    float jag = .045f * Mathf.Sin(u * 9f + age * 5f) + .03f * Mathf.Sin(u * 21f - age * 7f);
                    float d = v - (lineAt + jag); // height above the burning line
                    Color c;
                    if (d < -EdgeBand * .5f) { edgePixels[i] = clear; continue; }
                    if (d < EdgeBand)
                    {
                        float glow = .75f + .25f * Mathf.Sin(u * 13f + age * 11f);
                        c = Color.Lerp(EdgeHot, EmberColor, Mathf.Clamp01((d + EdgeBand * .5f) / (EdgeBand * 1.5f)));
                        c.r *= glow; c.g *= glow; c.b *= glow;
                    }
                    else if (d < EdgeBand + EdgeChar)
                    {
                        float k = (d - EdgeBand) / EdgeChar;
                        float shade = .85f + .15f * ((i * 7919) % 97) / 97f;
                        c = Color.Lerp(CharColor, EmberColor, Mathf.Max(0f, .35f - k)); c.r *= shade; c.g *= shade; c.b *= shade;
                        c.a = .92f * (1f - k * k);
                    }
                    else if (d < EdgeBand + EdgeChar + EdgeHalo) { c = EmberColor; c.a = .3f * (1f - (d - EdgeBand - EdgeChar) / EdgeHalo); }
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
            scorch.transform.localPosition = new Vector3(0f, 0f, -.01f);
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
                var from = new Vector3(Mathf.Cos(angle) * inner, Mathf.Sin(angle) * inner, -.03f);
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
