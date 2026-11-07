using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// #540 지귀 부적 구슬이 박에 맞은 자리: 한지 부적 한 장이 크게 날아와 "철썩" 붙고, 그 위에 붉은 도장이 "쾅" 찍힌 뒤
    /// 잠깐 붙어 있다가 물기에 번지듯 흐려진다. 카메라를 향해 세워 그린다. 화면 표시만 하며 스스로 사라진다.
    /// </summary>
    public sealed class LabTalismanSealSlap : MonoBehaviour
    {
        private const float SlapSeconds = .14f, StampAt = .2f, StampSeconds = .12f, HoldSeconds = 1.1f, FadeSeconds = .5f;
        private static readonly Color Paper = new Color(.98f, .9f, .62f, .95f), Seal = new Color(.78f, .09f, .07f, .95f);
        private SpriteRenderer paper, seal, emblem;
        private float radius = .3f, age;
        private Vector3 tilt;

        public float Age => age;
        public bool StampLanded => age >= StampAt;

        public static LabTalismanSealSlap Create(Vector3 worldPoint, float orbRadius)
        {
            var root = new GameObject("Talisman Seal Slap");
            root.transform.position = worldPoint;
            var look = root.AddComponent<LabTalismanSealSlap>();
            look.radius = orbRadius > 0f ? orbRadius : .3f;
            look.tilt = new Vector3(0f, 0f, Random.Range(-14f, 14f));
            look.paper = look.Part("Slap Paper", PaperSprite(), Paper, 40);
            look.seal = look.Part("Slap Seal", LabJigwiTalismanOverlay.StampSprite(), Seal, 41);
            look.emblem = look.Part("Slap Seal Emblem", LabJigwiTalismanOverlay.StampEmblemSprite(), Seal, 42);
            RenderPipelineManager.beginCameraRendering += look.Face;
            look.Pose();
            return look;
        }

        private SpriteRenderer Part(string label, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(label) { layer = gameObject.layer };
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }

        private void Face(ScriptableRenderContext context, Camera view)
        {
            if (this != null && view != null) transform.rotation = view.transform.rotation * Quaternion.Euler(tilt);
        }

        private void Update()
        {
            age += Time.deltaTime;
            Pose();
            if (age >= StampAt + StampSeconds + HoldSeconds + FadeSeconds) Destroy(gameObject);
        }

        private void Pose()
        {
            if (paper == null) return;
            // The talisman flies in big and slaps flat onto the gourd.
            float slap = Mathf.Clamp01(age / SlapSeconds);
            float paperScale = Mathf.Lerp(2.2f, 1f, slap * slap) * (1f + .08f * Mathf.Sin(Mathf.Clamp01((age - SlapSeconds) / .15f) * Mathf.PI));
            float fade = Mathf.Clamp01((age - StampAt - StampSeconds - HoldSeconds) / FadeSeconds);
            float width = radius * 3.2f;
            SetSize(paper, width * paperScale, width * 1.6f * paperScale);
            Color p = Paper; p.a *= Mathf.Lerp(.4f, 1f, slap) * (1f - fade); paper.color = p;
            // Then the seal comes down on it.
            float stamp = Mathf.Clamp01((age - StampAt) / StampSeconds);
            bool down = age >= StampAt;
            seal.enabled = emblem.enabled = down;
            float sealScale = Mathf.Lerp(1.8f, 1f, stamp * stamp) * (1f - .06f * Mathf.Sin(Mathf.Clamp01((age - StampAt - StampSeconds) / .15f) * Mathf.PI));
            SetSize(seal, width * .62f * sealScale, width * .62f * sealScale);
            SetSize(emblem, width * .5f * sealScale, width * .5f * sealScale);
            seal.transform.localPosition = emblem.transform.localPosition = new Vector3(0f, -width * .25f, -.01f);
            Color c = Seal; c.a *= Mathf.Lerp(.3f, 1f, stamp) * (1f - fade); seal.color = emblem.color = c;
        }

        private static void SetSize(SpriteRenderer renderer, float width, float height)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector2 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(width / Mathf.Max(.0001f, size.x), height / Mathf.Max(.0001f, size.y), 1f);
        }

        private static Sprite paperSprite;
        /// <summary>A soft-edged hanji sheet (runtime).</summary>
        internal static Sprite PaperSprite()
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

        private void OnDestroy() => RenderPipelineManager.beginCameraRendering -= Face;
    }
}
