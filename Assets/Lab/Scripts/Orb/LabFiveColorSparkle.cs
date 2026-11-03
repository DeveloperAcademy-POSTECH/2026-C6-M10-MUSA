using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// 섬광 파훼 보상 오색구슬 (#329)의 모습. 판·투사체 구슬 위에 다섯 색(노랑·초록·파랑·빨강·갈색) 사이를 쉬지 않고
    /// 부드럽게 건너가는 구슬을 얹는다. 몸통 색이 바뀌는 동안 안쪽에서 다음 색 빛이 천천히 돌아 물들고, 둘레에는 같은 색
    /// 빛무리, 가장자리를 따라 도는 윤광, 둘레에서 피었다 지는 별빛이 있어 영롱하게 보인다. 박에 맞은 자리에서는
    /// 다섯 색 가루 먼지가 털리고 그 위로 별가루가 함께 터진다 (#464). 화면 표시만 하며 규칙·물리에는 쓰지 않는다.
    /// 3D로 날아가는 오색구슬(투사체)은 납작한 그림을 얹지 않고, 구슬(3D 공) 자체의 색이 다섯 색 사이를 건너간다 (#426).
    /// 그림은 카메라 쪽으로 돌려 세워야 하는데, 자리마다 전투 카메라 방향이 달라 옆자리에서는 그림이 납작하게 공 옆으로 비켜 보였다.
    /// 먹선·그늘·하이라이트 그림은 Resources/Yaguang/FiveColorOrb, 별은 FiveColorSparkle에 있고, 없으면 코드로 그린 것을 쓴다.
    /// </summary>
    public sealed class LabFiveColorSparkle : MonoBehaviour
    {
        public const string ArtSpritePath = "Yaguang/FiveColorOrb";
        public const string SparkleSpritePath = "Yaguang/FiveColorSparkle";
        private const string ArtName = "Five Color Orb";
        private const int HaloOrder = 10, BodyOrder = 11, SwirlOrder = 12, ShadeOrder = 13, RimOrder = 14, TwinkleOrder = 15,
            BurstOrder = 30;
        private const int BurstCount = 10, TwinkleCount = 5, SwirlCount = 2;
        // 먹선 그림 폭 = 구슬 지름 / 그림 속 원 비율(0.92). 결합 구슬 그림과 같은 크기가 된다.
        private const float ArtWidth = 2f / .92f, BodySize = .97f, BurstSeconds = .65f, BurstReach = 3.2f;
        // 몸통: ColorSeconds마다 다음 색으로 부드럽게 건너간다 (다섯 색 한 바퀴 = 5 × ColorSeconds).
        private const float ColorSeconds = .9f;
        // 안쪽 빛: 다음 색들이 몸통 안을 SwirlDegreesPerSecond로 돌며 물든다.
        private const float SwirlDegreesPerSecond = 55f, SwirlSize = 1.15f, SwirlOffset = .36f, SwirlAlpha = .6f, SwirlWhite = .25f;
        // 빛무리: 구슬 지름의 1.6배, 몸통과 같은 색으로 HaloPulseHz마다 숨 쉰다.
        private const float HaloSize = 1.6f, HaloPulseHz = .8f, HaloMinAlpha = .3f, HaloMaxAlpha = .65f;
        // 윤광: 가장자리를 따라 초당 RimDegreesPerSecond 도는 흰 빛줄기.
        private const float RimDegreesPerSecond = 140f, RimAlpha = .85f;
        // 별빛: 둘레 다섯 자리에서 TwinkleSeconds 동안 피었다 지고, 다음엔 조금 옮긴 자리에서 핀다.
        private const float TwinkleSeconds = 1.3f, TwinkleSize = .7f;

        /// <summary>
        /// 구슬·별가루의 다섯 색 (원색에 가깝게), 건너가는 순서대로 노랑 → 초록 → 파랑 → 빨강 → 갈색 → 다시 노랑.
        /// 이웃한 색 사이가 연두·청록·보라·적갈·황토처럼 맑게 이어지도록 고른 순서다 (초록↔빨강처럼 섞으면 탁해진다).
        /// 박에 칠하는 색은 다른 구슬과 같은 원소 칠 색이다 (LabModel.FiveColorPaintOrder).
        /// </summary>
        public static readonly Color32[] FiveColors =
        {
            new Color32(255, 206, 0, 255), new Color32(30, 178, 76, 255), new Color32(18, 98, 232, 255),
            new Color32(232, 38, 38, 255), new Color32(140, 82, 38, 255)
        };
        private static Sprite shadeSprite, sparkleSprite, glowSprite, rimSprite, discSprite;
        private static int attachCount;

        private SpriteRenderer body, shade, halo, rim;
        private readonly SpriteRenderer[] swirls = new SpriteRenderer[SwirlCount];
        private readonly SpriteRenderer[] twinkles = new SpriteRenderer[TwinkleCount];
        private SpriteRenderer[] burstStars;
        private float radius = .3f, age, dim;
        private bool burst;
        private Vector3[] burstDirections;
        // 3D로 날아가는 구슬: 그림 대신 이 구슬의 3D 공 렌더러 색을 바꾼다.
        private Transform flightRoot;
        private MaterialPropertyBlock tintBlock;
        private readonly System.Collections.Generic.List<MeshRenderer> tinted = new System.Collections.Generic.List<MeshRenderer>();
        // 날아가는 오색구슬의 꼬리 (#469): 다섯 색 그라데이션이 시간에 따라 꼬리를 타고 흘러간다.
        private const float TrailFlowSpeed = 2f, TrailHeadAlpha = .9f, TrailMiddleAlpha = .6f;
        // 기본 꼬리(원소 없는 공용)는 폭 ≈ 공 반지름 × 0.75, 0.28초라 공 뒤에 가려 거의 안 보인다.
        // 원소 먹선 꼬리(폭 0.6, 0.42초)와 비슷하게 보이도록 폭과 길이를 늘린다 (#469).
        private const float TrailWidthScale = 5f, TrailSeconds = .45f;
        private static readonly AnimationCurve TrailWidthCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(.55f, .65f), new Keyframe(1f, 0f));
        private float trailWrittenWidth = -1f;
        private TrailRenderer flightTrail;
        private Gradient trailGradient;
        private readonly GradientColorKey[] trailColors = new GradientColorKey[5];
        private static readonly GradientAlphaKey[] TrailAlphas =
        {
            new GradientAlphaKey(TrailHeadAlpha, 0f), new GradientAlphaKey(TrailMiddleAlpha, .5f), new GradientAlphaKey(0f, 1f)
        };
        // 먼지 털림 (오색구슬 명중 비교용): 부드러운 가루 뭉게구름 + 작은 알갱이.
        private const int DustPuffCount = 7, DustSpeckCount = 14;
        private const float DustSeconds = .9f, DustReach = 2.4f, SpeckReach = 3.6f;
        /// <summary>먼지 뭉게구름 크기 배율 기본값 (#464에서 시험해 고른 값)</summary>
        public const float DefaultDustPuffScale = 2f;
        private static readonly Color DustTone = new Color(.93f, .88f, .78f, 1f);
        private bool dust;
        private SpriteRenderer[] dustPuffs, dustSpecks;
        private Vector3[] puffDirections, speckDirections;
        private float[] puffSizes, speckSizes;
        private float puffScale = 1f;

        /// <summary>오색구슬 그림이 보이는지</summary>
        public bool Shown => body != null && body.gameObject.activeSelf;

        /// <summary>지금 몸통 색 (다섯 색 사이를 건너가는 중의 색)</summary>
        public Color BodyColor => ColorAt(age);

        /// <summary>
        /// 이 구슬에 오색구슬 그림을 얹는다(이미 있으면 그것을 쓴다). faceCamera는 투사체처럼 3D로 날아가는 구슬용으로,
        /// 이때는 그림을 얹지 않고 구슬의 3D 공 색을 다섯 색 사이로 바꾼다.
        /// </summary>
        public static LabFiveColorSparkle AttachArt(Transform orbRoot, float orbRadius, bool faceCamera)
        {
            if (orbRoot == null) return null;
            Transform found = orbRoot.Find(ArtName);
            LabFiveColorSparkle look = found != null ? found.GetComponent<LabFiveColorSparkle>() : null;
            if (look == null)
            {
                var root = new GameObject(ArtName);
                root.layer = orbRoot.gameObject.layer;
                root.transform.SetParent(orbRoot, false);
                look = root.AddComponent<LabFiveColorSparkle>();
                // 구슬마다 색이 다른 데서 시작해 여러 개가 한꺼번에 같은 색으로 바뀌지 않게 한다.
                look.age = Mathf.Repeat(++attachCount * .618f, 1f) * ColorSeconds * FiveColors.Length;
                if (faceCamera) look.flightRoot = orbRoot;
                else look.Build();
            }
            look.SetRadius(orbRadius);
            look.Show(true);
            return look;
        }

        /// <summary>박에 맞은 자리에서 다섯 색 별가루를 한 번 터뜨린다. 스스로 사라진다.</summary>
        public static LabFiveColorSparkle Burst(Vector3 worldPoint, float orbRadius)
        {
            var root = new GameObject("Five Color Sparkle Burst");
            root.transform.position = worldPoint;
            var look = root.AddComponent<LabFiveColorSparkle>();
            look.burst = true;
            look.radius = orbRadius > 0f ? orbRadius : .3f;
            RenderPipelineManager.beginCameraRendering += look.FaceRenderingCamera;
            look.burstDirections = new Vector3[BurstCount];
            look.burstStars = new SpriteRenderer[BurstCount];
            for (int i = 0; i < BurstCount; i++)
            {
                float angle = (i + .37f * (i % 3)) / BurstCount * Mathf.PI * 2f;
                look.burstDirections[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (.75f + .25f * ((i * 7) % 4) / 3f);
                look.burstStars[i] = look.Part("Five Color Burst Star " + i, SparkleSprite(), StarColor(i), BurstOrder);
            }
            look.Update();
            return look;
        }

        /// <summary>
        /// 박에 맞은 자리에서 다섯 색 가루 먼지가 털리듯 퍼진다. 부드러운 뭉게구름이 확 퍼졌다 느려지며 커지고 살짝 떠오르며
        /// 흐려지고, 작은 알갱이는 더 멀리 튀었다 떨어진다. 스스로 사라진다.
        /// </summary>
        public static LabFiveColorSparkle DustBurst(Vector3 worldPoint, float orbRadius, float puffScale = DefaultDustPuffScale)
        {
            var root = new GameObject("Five Color Dust Burst");
            root.transform.position = worldPoint;
            var look = root.AddComponent<LabFiveColorSparkle>();
            look.dust = true;
            look.radius = orbRadius > 0f ? orbRadius : .3f;
            look.puffScale = puffScale > 0f && !float.IsInfinity(puffScale) ? puffScale : DefaultDustPuffScale;
            RenderPipelineManager.beginCameraRendering += look.FaceRenderingCamera;
            look.dustPuffs = new SpriteRenderer[DustPuffCount];
            look.puffDirections = new Vector3[DustPuffCount];
            look.puffSizes = new float[DustPuffCount];
            for (int i = 0; i < DustPuffCount; i++)
            {
                float angle = (i + Random.value * .6f) / DustPuffCount * Mathf.PI * 2f;
                look.puffDirections[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .75f + .15f, 0f) * Random.Range(.55f, 1f);
                look.puffSizes[i] = Random.Range(.9f, 1.4f);
                look.dustPuffs[i] = look.Part("Five Color Dust Puff " + i, GlowSprite(),
                    Color.Lerp(StarColor(i), DustTone, .55f), BurstOrder - 2); // 별가루 아래
            }
            look.dustSpecks = new SpriteRenderer[DustSpeckCount];
            look.speckDirections = new Vector3[DustSpeckCount];
            look.speckSizes = new float[DustSpeckCount];
            for (int i = 0; i < DustSpeckCount; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                look.speckDirections[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(.6f, 1.1f);
                look.speckSizes[i] = Random.Range(.08f, .16f);
                look.dustSpecks[i] = look.Part("Five Color Dust Speck " + i, DiscSprite(),
                    Color.Lerp(StarColor(i), DustTone, .3f), BurstOrder - 1);
            }
            look.Update();
            return look;
        }

        public void Show(bool shown)
        {
            if (body == null) return;
            foreach (SpriteRenderer part in new[] { body, shade, halo, rim })
                if (part != null && part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
            foreach (SpriteRenderer part in swirls) if (part != null) part.gameObject.SetActive(shown);
            foreach (SpriteRenderer part in twinkles) if (part != null) part.gameObject.SetActive(shown);
        }

        /// <summary>
        /// 섬광으로 구슬이 하얗게 덮인 정도 (0~1). 덮인 만큼 오색구슬 그림과 빛을 모두 거둬, 아래의 평범한 결합 구슬이
        /// 하얀 덮개에 가려 보이게 한다 (오색구슬인 걸 들키지 않는다).
        /// </summary>
        public void SetGlare(float amount) => dim = float.IsNaN(amount) ? 0f : Mathf.Clamp01(amount);

        public void SetRadius(float orbRadius)
        {
            if (orbRadius > 0f && !float.IsInfinity(orbRadius)) radius = orbRadius;
            Pose();
        }

        private void Build()
        {
            halo = Part("Five Color Halo", GlowSprite(), Color.white, HaloOrder);
            body = Part("Five Color Body", DiscSprite(), Color.white, BodyOrder);
            for (int i = 0; i < SwirlCount; i++) swirls[i] = Part("Five Color Swirl " + i, GlowSprite(), Color.white, SwirlOrder);
            shade = Part("Five Color Orb Art", ShadeSprite(), Color.white, ShadeOrder);
            rim = Part("Five Color Rim Shine", RimSprite(), Color.white, RimOrder);
            for (int i = 0; i < TwinkleCount; i++)
                twinkles[i] = Part("Five Color Twinkle " + i, SparkleSprite(), Color.white, TwinkleOrder);
            Pose();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (dust)
            {
                float u = Mathf.Clamp01(age / DustSeconds);
                float ease = 1f - (1f - u) * (1f - u) * (1f - u);
                // 처음 아주 짧게 피어나고, 끝으로 갈수록 흐려진다.
                float fade = Mathf.Pow(1f - u, 1.5f) * Mathf.Clamp01(u * 8f + .2f);
                for (int i = 0; i < DustPuffCount; i++)
                {
                    SpriteRenderer puff = dustPuffs[i];
                    // 크게 할수록 조금 더 멀리 퍼지게 한다 (뭉게구름끼리 한 덩어리로 뭉치지 않게).
                    float spread = Mathf.Lerp(1f, puffScale, .5f);
                    puff.transform.localPosition = puffDirections[i] * (DustReach * radius * spread * ease) + Vector3.up * (.35f * radius * puffScale * u);
                    puff.transform.localScale = Vector3.one * (radius * puffScale * puffSizes[i] * (.5f + 1.7f * ease));
                    Color color = puff.color;
                    color.a = .75f * fade;
                    puff.color = color;
                }
                for (int i = 0; i < DustSpeckCount; i++)
                {
                    SpriteRenderer speck = dustSpecks[i];
                    speck.transform.localPosition = speckDirections[i] * (SpeckReach * radius * ease) + Vector3.down * (1.2f * radius * u * u);
                    speck.transform.localScale = Vector3.one * (radius * speckSizes[i] * 2f * (1f - .5f * u));
                    Color color = speck.color;
                    color.a = 1f - u;
                    speck.color = color;
                }
                if (u >= 1f) Destroy(gameObject);
            }
            else if (burst)
            {
                float u = Mathf.Clamp01(age / BurstSeconds);
                for (int i = 0; i < BurstCount; i++)
                {
                    SpriteRenderer star = burstStars[i];
                    float ease = 1f - (1f - u) * (1f - u);
                    star.transform.localPosition = burstDirections[i] * (BurstReach * radius * ease);
                    star.transform.localScale = Vector3.one * (radius * (1.1f - .7f * u));
                    Color color = StarColor(i);
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
            if (flightRoot != null) { Tint(); TintTrail(); }
        }

        /// <summary>날아가는 구슬의 3D 공(굴림 표시 점 제외)을 지금 몸통 색으로 칠한다. 공이 바뀌어도 매번 다시 찾는다.</summary>
        public void Tint()
        {
            if (flightRoot == null) return;
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
            Color color = ColorAt(age);
            flightRoot.GetComponentsInChildren(false, tinted);
            foreach (MeshRenderer surface in tinted)
            {
                if (surface == null || surface.name == "Roll Marker") continue;
                surface.GetPropertyBlock(tintBlock);
                tintBlock.SetColor("_BaseColor", color); // URP Lit
                tintBlock.SetColor("_Color", color);     // Standard shader fallback
                surface.SetPropertyBlock(tintBlock);
            }
        }

        /// <summary>
        /// 날아가는 구슬의 꼬리(TrailRenderer)를 다섯 색 그라데이션으로 칠한다. 머리부터 꼬리 끝까지 다섯 색이 한 번씩 놓이고,
        /// 시간이 지나면 색이 꼬리를 타고 뒤로 흘러간다. 투명도는 머리 쪽이 진하고 끝으로 갈수록 사라진다. 꼬리가 없으면 넘어간다.
        /// 기본 꼬리는 너무 얇고 짧아 원소 꼬리만큼 보이도록 폭과 길이도 키운다.
        /// </summary>
        public void TintTrail()
        {
            if (flightRoot == null) return;
            if (flightTrail == null)
            {
                // 공의 기본 꼬리를 쓴다 (지귀 보상 불꽃 꼬리 같은 다른 꼬리는 건드리지 않는다).
                LabProjectile projectile = flightRoot.GetComponentInParent<LabProjectile>();
                flightTrail = projectile != null ? projectile.FlightTrail : null;
                if (flightTrail == null)
                {
                    Transform found = flightRoot.Find("Flight Trail");
                    if (found != null) flightTrail = found.GetComponent<TrailRenderer>();
                }
            }
            if (flightTrail == null) return;
            // 공이 크기/꼬리를 다시 정하면(던질 때, 예측 보정 때) 그 폭을 기준으로 다시 키운다.
            if (!Mathf.Approximately(flightTrail.widthMultiplier, trailWrittenWidth))
            {
                trailWrittenWidth = flightTrail.widthMultiplier * TrailWidthScale;
                flightTrail.widthMultiplier = trailWrittenWidth;
                flightTrail.time = TrailSeconds;
                flightTrail.widthCurve = TrailWidthCurve;
            }
            if (trailGradient == null) trailGradient = new Gradient();
            float flow = age * TrailFlowSpeed;
            for (int i = 0; i < trailColors.Length; i++)
                trailColors[i] = new GradientColorKey(ColorAt(flow + (trailColors.Length - 1 - i) * ColorSeconds), i / (trailColors.Length - 1f));
            trailGradient.SetKeys(trailColors, TrailAlphas);
            flightTrail.colorGradient = trailGradient;
        }

        // 별가루는 그리는 카메라마다 그 카메라 쪽으로 돌려 세운다 (전투 카메라에 MainCamera 태그가 없어도 된다).
        private void FaceRenderingCamera(ScriptableRenderContext context, Camera view)
        {
            if (this != null && view != null) transform.rotation = view.transform.rotation;
        }

        private void OnDestroy()
        {
            if (burst || dust) RenderPipelineManager.beginCameraRendering -= FaceRenderingCamera;
        }

        private void Pose()
        {
            if (body == null) return;
            float shine = 1f - dim;

            // 몸통: 다섯 색 사이를 쉬지 않고 건너간다.
            Color bodyColor = ColorAt(age);
            bodyColor.a = shine;
            body.color = bodyColor;
            body.transform.localScale = Vector3.one * (2f * BodySize * radius);
            body.transform.localPosition = new Vector3(0f, 0f, -1.03f * radius);

            // 안쪽 빛: 곧 올 색이 몸통 안을 돌며 미리 물든다. 조금 밝게 섞어 맑게 비친다 (먼 색을 섞으면 탁해진다).
            for (int i = 0; i < SwirlCount; i++)
            {
                float angle = (SwirlDegreesPerSecond * age + 180f * i) * Mathf.Deg2Rad;
                Color swirlColor = Color.Lerp(ColorAt(age + ColorSeconds * (.6f + .5f * i)), Color.white, SwirlWhite);
                swirlColor.a = SwirlAlpha * shine;
                swirls[i].color = swirlColor;
                swirls[i].transform.localScale = Vector3.one * (SwirlSize * radius);
                swirls[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * SwirlOffset * radius,
                    Mathf.Sin(angle) * SwirlOffset * radius, -1.04f * radius);
            }

            // 먹선·그늘·하이라이트: 색 위에 얹는다.
            Vector2 size = shade.sprite != null ? (Vector2)shade.sprite.bounds.size : Vector2.one;
            float scale = size.x > 0f ? ArtWidth * radius / size.x : radius;
            shade.transform.localScale = new Vector3(scale, scale, 1f);
            shade.transform.localPosition = new Vector3(0f, 0f, -1.05f * radius);
            shade.color = new Color(1f, 1f, 1f, shine);

            // 빛무리: 몸통과 같은 색으로 숨 쉰다.
            Color haloColor = ColorAt(age);
            haloColor.a = shine * Mathf.Lerp(HaloMinAlpha, HaloMaxAlpha, .5f + .5f * Mathf.Sin(age * HaloPulseHz * Mathf.PI * 2f));
            halo.color = haloColor;
            halo.transform.localScale = Vector3.one * (2f * HaloSize * radius);
            halo.transform.localPosition = new Vector3(0f, 0f, -1.0f * radius);

            // 윤광: 가장자리를 따라 돈다.
            rim.transform.localScale = Vector3.one * (2.04f * radius);
            rim.transform.localPosition = new Vector3(0f, 0f, -1.07f * radius);
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, -RimDegreesPerSecond * age);
            rim.color = new Color(1f, 1f, 1f, RimAlpha * shine);

            // 별빛: 다섯 자리에서 차례로 피었다 지고, 필 때마다 자리를 조금 옮긴다.
            for (int i = 0; i < TwinkleCount; i++)
            {
                float cycle = age / TwinkleSeconds + i / (float)TwinkleCount;
                int round = Mathf.FloorToInt(cycle);
                float phase = cycle - round;
                float glow = Mathf.Sin(phase * Mathf.PI);
                glow *= glow;
                float angle = (90f + 72f * i + 37f * round) * Mathf.Deg2Rad;
                float reach = (.78f + .22f * Mathf.Repeat(round * .618f + i * .3f, 1f)) * radius;
                Transform twinkle = twinkles[i].transform;
                twinkle.localPosition = new Vector3(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach, -1.09f * radius);
                twinkle.localScale = Vector3.one * (TwinkleSize * radius * glow);
                twinkle.localRotation = Quaternion.Euler(0f, 0f, 90f * phase);
                Color color = Color.Lerp(Color.white, StarColor(i + round), .35f);
                color.a = glow * shine;
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

        /// <summary>seconds 시각의 몸통 색: ColorSeconds마다 다음 색으로, 사이는 부드럽게 섞는다.</summary>
        public static Color ColorAt(float seconds)
        {
            float step = Mathf.Max(0f, seconds) / ColorSeconds;
            int from = Mathf.FloorToInt(step);
            return Color.Lerp(StarColor(from), StarColor(from + 1), Mathf.SmoothStep(0f, 1f, step - from));
        }

        /// <summary>별가루 별 하나의 색: 다섯 색이 차례로 돈다.</summary>
        private static Color StarColor(int index) => FiveColors[((index % FiveColors.Length) + FiveColors.Length) % FiveColors.Length];

        /// <summary>가운데가 밝고 바깥으로 부드럽게 사라지는 흰 원 (빛무리·안쪽 빛용, 색은 코드에서 입힌다).</summary>
        private static Sprite GlowSprite() => glowSprite != null ? glowSprite : glowSprite = Generated("Lab five color glow", 64, (u, v) =>
        {
            float fade = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) / .5f);
            return new Color(1f, 1f, 1f, fade * fade);
        });

        /// <summary>가장자리가 매끈한 흰 원 (몸통용, 색은 코드에서 입힌다).</summary>
        private static Sprite DiscSprite() => discSprite != null ? discSprite : discSprite = Generated("Lab five color disc", 128, (u, v) =>
            new Color(1f, 1f, 1f, Mathf.Clamp01((.5f - Mathf.Sqrt(u * u + v * v)) / .012f)));

        /// <summary>구슬 가장자리를 따라 한쪽만 밝은 흰 고리 (윤광용). 돌리면 빛줄기가 가장자리를 따라 돈다.</summary>
        private static Sprite RimSprite() => rimSprite != null ? rimSprite : rimSprite = Generated("Lab five color rim", 128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float band = Mathf.Clamp01(1f - Mathf.Abs(d - .44f) / .045f);
            float arc = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(Mathf.Atan2(v, u))), 5f);
            return new Color(1f, 1f, 1f, band * band * arc);
        });

        /// <summary>먹선 테두리·아래 그늘·왼쪽 위 하이라이트만 있고 안은 비친 그림. 몸통 색 위에 얹는다.</summary>
        private static Sprite ShadeSprite()
        {
            if (shadeSprite != null) return shadeSprite;
            shadeSprite = Resources.Load<Sprite>(ArtSpritePath);
            return shadeSprite != null ? shadeSprite : shadeSprite = Generated("Lab five color shade", 64, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01((.5f - d) / .03f);
                return d > .42f ? new Color(.11f, .09f, .09f, a) : new Color(1f, 1f, 1f, 0f);
            });
        }

        private static Sprite SparkleSprite()
        {
            if (sparkleSprite != null) return sparkleSprite;
            sparkleSprite = Resources.Load<Sprite>(SparkleSpritePath);
            return sparkleSprite != null ? sparkleSprite : sparkleSprite = Generated("Lab five color sparkle", 32, (u, v) =>
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
