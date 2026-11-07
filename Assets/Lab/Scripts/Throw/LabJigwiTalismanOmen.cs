using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// #540 지귀 A 예고 (3D). 야광귀 섬광 예고처럼 전투 화면(박이 보이는 3D)에서 무언가 온다는 걸 먼저 보여 준다.
    /// 예고(2초) 동안 이 자리 차례인 사람 화면에만:
    /// 1) 박 위로 한지 부적 한 장이 "퐁" 튀어나오고(살짝 커졌다 돌아옴),
    /// 2) 박 위에 떠서 빙글빙글 돌며 가장자리에 지귀 불이 붙어 일렁이고 불티가 오르고,
    /// 3) 마지막에 카메라 쪽으로 휙 날아와 화면 아래(내 조합판)로 빠져나간다. 그 순간 판 위에 부적이 놓인다.
    /// 시각은 Host 스냅샷의 예고 시각(`LabJigwiTalismanOverlay.OmenLevel`, 0 → 1)만 쓴다. 화면 표시만 하며 판정·물리와 무관하다.
    /// </summary>
    public sealed class LabJigwiTalismanOmen : MonoBehaviour
    {
        /// <summary>예고 비율(0~1)에서 튀어나오기가 끝나는 곳 / 날아오기가 시작하는 곳.</summary>
        public const float PopEnd = .15f, SwoopAt = .68f;
        private const int EmberCount = 10;
        private static readonly Color Paper = new Color(.98f, .9f, .62f, 1f), Seal = new Color(.78f, .09f, .07f, 1f);
        private static readonly Color Fire = new Color(1f, .45f, .08f, 1f), FireCore = new Color(1f, .82f, .35f, 1f);
        private SpriteRenderer paper, glow, seal, emblem;
        private readonly SpriteRenderer[] embers = new SpriteRenderer[EmberCount];
        private readonly Vector3[] emberFrom = new Vector3[EmberCount];
        private readonly float[] emberPhase = new float[EmberCount];
        private Camera view;
        private float level;
        private Vector3 spin;

        /// <summary>지금 보이는지 (테스트·디버그용).</summary>
        public bool Showing => paper != null && paper.enabled;

        public static LabJigwiTalismanOmen Create()
        {
            var root = new GameObject("Jigwi Talisman Omen (#540)");
            var omen = root.AddComponent<LabJigwiTalismanOmen>();
            omen.glow = omen.Part("Omen Fire Glow", GlowSprite(), Fire, 38);
            omen.paper = omen.Part("Omen Paper", PaperSprite(), Paper, 39);
            omen.seal = omen.Part("Omen Seal", LabJigwiTalismanOverlay.StampSprite(), Seal, 40);
            omen.emblem = omen.Part("Omen Seal Emblem", LabJigwiTalismanOverlay.StampEmblemSprite(), Seal, 41);
            for (int i = 0; i < EmberCount; i++)
            {
                omen.embers[i] = omen.Part("Omen Ember", GlowSprite(), FireCore, 42);
                omen.emberPhase[i] = (i * .61803398875f) % 1f;
            }
            RenderPipelineManager.beginCameraRendering += omen.Face;
            omen.Hide();
            return omen;
        }

        private SpriteRenderer Part(string label, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }

        public void Hide()
        {
            level = 0f;
            if (paper == null) return;
            paper.enabled = glow.enabled = seal.enabled = emblem.enabled = false;
            foreach (SpriteRenderer ember in embers) if (ember != null) ember.enabled = false;
        }

        /// <summary>예고 비율(0~1, 0이면 숨김)과 박으로 한 프레임 그린다.</summary>
        public void Show(float omenLevel, LabTarget gourd, float time)
        {
            if (omenLevel <= 0f || omenLevel >= 1f || gourd == null || gourd.Hitbox == null) { Hide(); return; }
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            if (view == null) { Hide(); return; }
            level = omenLevel;
            gameObject.layer = gourd.gameObject.layer;
            foreach (Transform child in transform) child.gameObject.layer = gourd.gameObject.layer;

            Bounds box = gourd.Hitbox.bounds;
            Vector3 center = LabHangingTarget.DrawnPoint(gourd.Hitbox, box.center);
            float size = Mathf.Max(box.extents.y, Mathf.Max(box.extents.x, box.extents.z));
            Vector3 mouth = center + Vector3.up * box.extents.y * .7f;
            Vector3 hover = center + Vector3.up * (box.extents.y + size * .9f);
            float width = size * .75f;

            float pop = Mathf.Clamp01(level / PopEnd);
            float swoop = Mathf.Clamp01((level - SwoopAt) / (1f - SwoopAt));
            Vector3 position;
            float scale;
            if (swoop <= 0f)
            {
                // Pops out of the gourd with a little overshoot, then bobs above it.
                float rise = 1f - (1f - pop) * (1f - pop);
                position = Vector3.Lerp(mouth, hover, rise) + Vector3.up * (Mathf.Sin(time * 5f) * size * .05f * pop);
                scale = pop < 1f ? Mathf.Lerp(.1f, 1.18f, rise) : 1f + .18f * Mathf.Exp(-(level - PopEnd) * 18f);
            }
            else
            {
                // Swoops at the camera and out the bottom of the battle view, toward the player's board.
                float distance = Mathf.Max(view.nearClipPlane * 4f, Vector3.Distance(view.transform.position, hover) * .22f);
                Vector3 exit = view.ViewportToWorldPoint(new Vector3(.5f, -.25f, distance));
                Vector3 bend = Vector3.Lerp(hover, exit, .5f) + Vector3.up * size * 1.2f;
                float e = swoop * swoop;
                position = Vector3.Lerp(Vector3.Lerp(hover, bend, e), Vector3.Lerp(bend, exit, e), e);
                scale = Mathf.Lerp(1f, 1.6f, e);
            }
            transform.position = position;
            transform.localScale = Vector3.one * scale;

            // Spins around while it hovers (fast at first, slower), then lies flat toward the camera as it flies.
            float turns = level < SwoopAt ? 2.5f * (1f - Mathf.Pow(1f - level / SwoopAt, 2f)) : 2.5f;
            float flat = 1f - Mathf.SmoothStep(0f, 1f, swoop * 3f);
            spin = new Vector3(Mathf.Sin(time * 3.1f) * 12f * flat, turns * 360f * flat, Mathf.Sin(time * 2.3f) * 8f);

            float fade = 1f - Mathf.SmoothStep(.85f, 1f, level);
            float alpha = Mathf.Clamp01(pop * 2f) * fade;
            SetSize(paper, width, width * 1.6f, 0f);
            Tint(paper, Paper, alpha);
            SetSize(seal, width * .5f, width * .5f, -.01f, -width * .4f);
            SetSize(emblem, width * .4f, width * .4f, -.012f, -width * .4f);
            Tint(seal, Seal, alpha); Tint(emblem, Seal, alpha);

            // Jigwi's fire catches the edges: a flickering glow behind, growing until it flies.
            float burn = Mathf.Clamp01((level - PopEnd * .5f) / (SwoopAt - PopEnd * .5f));
            float flicker = .8f + .2f * Mathf.PerlinNoise(time * 9f, 3.7f);
            SetSize(glow, width * (1.25f + .25f * burn) * flicker, width * 1.6f * (1.18f + .2f * burn) * flicker, .02f);
            Tint(glow, Color.Lerp(Fire, FireCore, .3f * flicker), alpha * Mathf.Lerp(.25f, .9f, burn));

            for (int i = 0; i < EmberCount; i++)
            {
                SpriteRenderer ember = embers[i];
                float t = (time * .9f + emberPhase[i]) % 1f;
                if (t < .05f || emberFrom[i] == Vector3.zero)
                {
                    float side = (i % 2 == 0 ? -1f : 1f) * width * .5f;
                    float along = (Mathf.PerlinNoise(i * 1.7f, time) - .5f) * width * 1.5f;
                    emberFrom[i] = i % 3 == 0 ? new Vector3(along * .6f, width * .8f, 0f) : new Vector3(side, along, 0f);
                }
                Vector3 local = emberFrom[i] + new Vector3(Mathf.Sin(t * 6f + i) * width * .08f, t * width * .9f, -.015f);
                ember.transform.localPosition = local;
                float s = width * .09f * (1f - t);
                ember.transform.localScale = Vector3.one * s / Mathf.Max(.0001f, ember.sprite.bounds.size.x);
                Tint(ember, Color.Lerp(FireCore, Fire, t), alpha * burn * (1f - t));
            }
        }

        private static void Tint(SpriteRenderer renderer, Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            renderer.color = color;
            renderer.enabled = color.a > .002f;
        }

        private static void SetSize(SpriteRenderer renderer, float width, float height, float z, float y = 0f)
        {
            Vector2 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(width / Mathf.Max(.0001f, size.x), height / Mathf.Max(.0001f, size.y), 1f);
            renderer.transform.localPosition = new Vector3(0f, y, z);
        }

        private void Face(ScriptableRenderContext context, Camera camera)
        {
            if (this == null || camera == null || camera != view) return;
            transform.rotation = camera.transform.rotation * Quaternion.Euler(spin);
        }

        private static Sprite paperSprite;
        /// <summary>A soft-edged hanji sheet (runtime).</summary>
        private static Sprite PaperSprite()
        {
            if (paperSprite != null) return paperSprite;
            const int width = 64, height = 102;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Talisman Slap Paper (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x + .5f, width - x - .5f), Mathf.Min(y + .5f, height - y - .5f));
                    float fiber = .85f + .15f * Mathf.PerlinNoise(x * .2f + 3f, y * .2f + 11f);
                    float a = Mathf.Clamp01((edge - 1f - 2f * Mathf.PerlinNoise(x * .3f, y * .3f)) / 2f);
                    pixels[y * width + x] = new Color(fiber, fiber, fiber, a);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            paperSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 100f);
            paperSprite.hideFlags = HideFlags.DontSave;
            return paperSprite;
        }

        private static Sprite glowSprite;
        /// <summary>A soft round glow (runtime).</summary>
        private static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Omen Glow (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            glowSprite.hideFlags = HideFlags.DontSave;
            return glowSprite;
        }

        private void OnDestroy() => RenderPipelineManager.beginCameraRendering -= Face;
    }
}
