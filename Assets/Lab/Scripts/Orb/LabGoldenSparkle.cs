using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 섬광 파훼 보상 황금알 (#329)의 모습. 판·투사체 구슬 위에 반짝이는 황금알 그림을 얹고 별빛이 반짝이게 하며,
    /// 박에 맞은 자리에서 금빛 별가루를 터뜨린다. 화면 표시만 하며 규칙·물리에는 쓰지 않는다.
    /// 그림은 Resources/Yaguang/GoldenEgg, GoldenSparkle에 있고, 없으면 코드로 그린 금빛 원·별을 쓴다.
    /// </summary>
    public sealed class LabGoldenSparkle : MonoBehaviour
    {
        public const string EggSpritePath = "Yaguang/GoldenEgg";
        public const string SparkleSpritePath = "Yaguang/GoldenSparkle";
        private const string EggName = "Golden Egg";
        private const int EggOrder = 11, TwinkleOrder = 12, BurstOrder = 30;
        private const int TwinkleCount = 3, BurstCount = 12;
        private const float EggWidth = 2.6f, TwinkleHz = 1.6f, BurstSeconds = .65f, BurstReach = 3.2f;
        private static readonly Color Twinkle = new Color(1f, .97f, .8f, 1f);
        private static Sprite eggSprite, sparkleSprite;

        private SpriteRenderer egg;
        private readonly SpriteRenderer[] twinkles = new SpriteRenderer[TwinkleCount];
        private float radius = .3f, age;
        private bool faceCamera, burst;
        private Vector3[] burstDirections;
        private Color burstColor;

        /// <summary>황금알 그림이 보이는지</summary>
        public bool Shown => egg != null && egg.gameObject.activeSelf;

        /// <summary>
        /// 이 구슬에 황금알 그림을 얹는다(이미 있으면 그것을 쓴다). faceCamera는 투사체처럼 3D로 날아가는 구슬용이다.
        /// </summary>
        public static LabGoldenSparkle AttachEgg(Transform orbRoot, float orbRadius, bool faceCamera)
        {
            if (orbRoot == null) return null;
            Transform found = orbRoot.Find(EggName);
            LabGoldenSparkle look = found != null ? found.GetComponent<LabGoldenSparkle>() : null;
            if (look == null)
            {
                var root = new GameObject(EggName);
                root.layer = orbRoot.gameObject.layer;
                root.transform.SetParent(orbRoot, false);
                look = root.AddComponent<LabGoldenSparkle>();
                look.Build();
            }
            look.faceCamera = faceCamera;
            look.SetRadius(orbRadius);
            look.Show(true);
            return look;
        }

        /// <summary>박에 맞은 자리에서 금빛 별가루를 한 번 터뜨린다. 스스로 사라진다.</summary>
        public static LabGoldenSparkle Burst(Vector3 worldPoint, float orbRadius, Color color)
        {
            var root = new GameObject("Golden Sparkle Burst");
            root.transform.position = worldPoint;
            var look = root.AddComponent<LabGoldenSparkle>();
            look.burst = true;
            look.faceCamera = true;
            look.radius = orbRadius > 0f ? orbRadius : .3f;
            look.burstColor = color;
            look.burstDirections = new Vector3[BurstCount];
            for (int i = 0; i < BurstCount; i++)
            {
                float angle = (i + .37f * (i % 3)) / BurstCount * Mathf.PI * 2f;
                look.burstDirections[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (.75f + .25f * ((i * 7) % 4) / 3f);
                look.AddBurstStar(i);
            }
            look.Update();
            return look;
        }

        public void Show(bool shown)
        {
            if (egg == null) return;
            if (egg.gameObject.activeSelf != shown) egg.gameObject.SetActive(shown);
            foreach (SpriteRenderer twinkle in twinkles) if (twinkle != null) twinkle.gameObject.SetActive(shown);
        }

        public void SetRadius(float orbRadius)
        {
            if (orbRadius > 0f && !float.IsInfinity(orbRadius)) radius = orbRadius;
            Pose();
        }

        private void Build()
        {
            egg = Part("Golden Egg Art", EggSprite(), Color.white, EggOrder);
            for (int i = 0; i < TwinkleCount; i++) twinkles[i] = Part("Golden Twinkle " + i, SparkleSprite(), Twinkle, TwinkleOrder);
            Pose();
        }

        // 터질 때 퍼지는 별가루.
        private SpriteRenderer[] burstStars;
        private void AddBurstStar(int index)
        {
            if (burstStars == null) burstStars = new SpriteRenderer[BurstCount];
            burstStars[index] = Part("Golden Burst Star " + index, SparkleSprite(), burstColor, BurstOrder);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (burst)
            {
                float u = Mathf.Clamp01(age / BurstSeconds);
                for (int i = 0; i < BurstCount; i++)
                {
                    SpriteRenderer star = burstStars[i];
                    float ease = 1f - (1f - u) * (1f - u);
                    star.transform.localPosition = burstDirections[i] * (BurstReach * radius * ease);
                    star.transform.localScale = Vector3.one * (radius * (1.1f - .7f * u));
                    Color color = i % 2 == 0 ? burstColor : Twinkle;
                    color.a = 1f - u * u;
                    star.color = color;
                    star.transform.localRotation = Quaternion.Euler(0f, 0f, 180f * u + i * 30f);
                }
                if (u >= 1f) Destroy(gameObject);
            }
            else Pose();
        }

        private void LateUpdate()
        {
            if (!faceCamera) return;
            Camera view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;
        }

        private void Pose()
        {
            if (egg == null) return;
            Vector2 size = egg.sprite != null ? (Vector2)egg.sprite.bounds.size : Vector2.one;
            float scale = size.x > 0f ? EggWidth * radius / size.x : radius;
            egg.transform.localScale = new Vector3(scale, scale, 1f);
            // 구슬 앞면 바로 앞, 섬광의 하얀 덮개보다는 뒤 (덮개가 황금알도 가린다).
            egg.transform.localPosition = new Vector3(0f, 0f, -1.05f * radius);
            for (int i = 0; i < TwinkleCount; i++)
            {
                float phase = Mathf.Repeat(age * TwinkleHz + i / (float)TwinkleCount, 1f);
                float glow = Mathf.Sin(phase * Mathf.PI);
                float angle = (90f + 120f * i) * Mathf.Deg2Rad;
                Transform twinkle = twinkles[i].transform;
                twinkle.localPosition = new Vector3(Mathf.Cos(angle) * .62f * radius, Mathf.Sin(angle) * .7f * radius, -1.08f * radius);
                twinkle.localScale = Vector3.one * (.55f * radius * glow);
                twinkle.localRotation = Quaternion.Euler(0f, 0f, 45f * phase);
                Color color = Twinkle;
                color.a = glow;
                twinkles[i].color = color;
            }
        }

        private SpriteRenderer Part(string label, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite EggSprite()
        {
            if (eggSprite != null) return eggSprite;
            eggSprite = Resources.Load<Sprite>(EggSpritePath);
            return eggSprite != null ? eggSprite : eggSprite = Generated("Lab golden egg", 64, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u / .7f + v * v / .9f);
                float a = Mathf.Clamp01((.5f - d) / .03f);
                return new Color(1f, .78f - .2f * Mathf.Clamp01(v + .3f), .2f, a);
            });
        }

        private static Sprite SparkleSprite()
        {
            if (sparkleSprite != null) return sparkleSprite;
            sparkleSprite = Resources.Load<Sprite>(SparkleSpritePath);
            return sparkleSprite != null ? sparkleSprite : sparkleSprite = Generated("Lab golden sparkle", 32, (u, v) =>
            {
                float arm = Mathf.Min(Mathf.Abs(u), Mathf.Abs(v));
                float reach = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                float a = Mathf.Clamp01((.08f - arm * (1f + 6f * reach)) / .03f) * Mathf.Clamp01((.5f - reach) / .05f);
                return new Color(1f, 1f, 1f, Mathf.Max(a, Mathf.Clamp01(.12f - Mathf.Sqrt(u * u + v * v)) * 8f));
            });
        }

        private static Sprite Generated(string label, int size, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = label, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + .5f) / size - .5f, (y + .5f) / size - .5f);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            sprite.name = label;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
