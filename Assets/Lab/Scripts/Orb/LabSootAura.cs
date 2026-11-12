using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #696 지귀 검정 구슬의 기운: 판 위에 놓여만 있어도 구슬 둘레에서 먹빛 연기가 천천히 피어올라 감돌고,
    /// 뒤에서 보랏빛 빛무리가 숨 쉬듯 커졌다 작아진다. "연기를 모아 만든 구슬"로 보이게 한다. 표시만 한다.
    /// 오색구슬 그림(LabFiveColorSparkle) 밑에 붙어 같이 카메라를 보고, 그 그림보다 뒤(sortingOrder가 낮게)에 그린다.
    /// 같은 컴포넌트가 파편이 붙는 순간의 터짐(PlayJoin)도 그린다 (그때는 그림 앞에, 한 번만).
    /// </summary>
    public sealed class LabSootAura : MonoBehaviour
    {
        private const string AuraName = "Soot Aura";
        private const int WispCount = 7;
        private const int GlowOrder = 8, WispOrder = 9;
        /// <summary>연기 한 가닥이 피어올라 사라지는 데 걸리는 초.</summary>
        private const float WispSeconds = 2.4f;
        /// <summary>연기가 구슬 둘레를 도는 빠르기 (도/초).</summary>
        private const float SpinDegreesPerSecond = 22f;
        private const float GlowHz = .45f;

        private static readonly Color Ink = new Color(.06f, .05f, .09f, 1f);
        private static readonly Color Violet = new Color(.42f, .26f, .78f, 1f);

        private readonly SpriteRenderer[] wisps = new SpriteRenderer[WispCount];
        private SpriteRenderer glow;
        private float radius = .3f;
        private float age;
        private bool shown = true;

        // #696 붙는 순간 터짐 (PlayJoin): 한 번 퍼지고 스스로 사라진다.
        private const int JoinPuffCount = 10;
        private bool join, joinWhole;
        private float joinSeconds;
        private readonly Vector3[] joinDirections = new Vector3[JoinPuffCount];

        /// <summary>
        /// #696 파편이 붙는 순간의 터짐: 가운데 보랏빛이 번쩍이고, 먹빛 연기 덩이가 사방으로 확 퍼졌다 옅어진다.
        /// whole(검정 구슬 완성)이면 더 크고 길게, 빛도 한 번 더 크게 번진다. parent(구슬)를 따라다니다 스스로 사라진다.
        /// </summary>
        public static void PlayJoin(Transform parent, float orbRadius, bool whole)
        {
            if (parent == null || !(orbRadius > 0f)) return;
            var root = new GameObject(whole ? "Soot Join Burst (Whole)" : "Soot Join Burst");
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<LabSootAura>();
            burst.join = true;
            burst.joinWhole = whole;
            burst.joinSeconds = whole ? 1.1f : .6f;
            burst.radius = orbRadius;
            Sprite puff = LabOrbVanishSmoke.PuffSprite();
            burst.glow = burst.Part("Soot Join Flash", puff, 20);
            for (int i = 0; i < JoinPuffCount; i++)
            {
                float angle = (i + Random.Range(-.3f, .3f)) * (360f / JoinPuffCount) * Mathf.Deg2Rad;
                burst.joinDirections[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(.8f, 1.15f);
            }
            burst.joinPuffs = new SpriteRenderer[JoinPuffCount];
            for (int i = 0; i < JoinPuffCount; i++) burst.joinPuffs[i] = burst.Part("Soot Join Puff " + i, puff, 21);
            burst.PoseJoin();
        }

        private SpriteRenderer[] joinPuffs;

        // #713 박 명중 기운 (PlayHit): 3D 세상에 떠서 카메라를 보고, 연기 기운이 잠시 감돌다 사라진다.
        private const float HitSeconds = 1.9f;
        private bool hitHolder;
        private LabSootAura hitAura;
        /// <summary>기운 전체 진하기 (명중 기운이 사라질 때 줄인다).</summary>
        private float alphaScale = 1f;

        /// <summary>
        /// #713 검정 구슬 명중: 맞은 자리에서 판 위 기운과 같은 검정 기운이 터진다.
        /// 보랏빛이 번쩍이며 먹빛 연기가 사방으로 확 퍼지고(완성 터짐과 같은 모양, 더 크게), 그 자리에 연기 기운이 잠시 감돌다 사라진다.
        /// 카메라를 보며 스스로 사라진다.
        /// </summary>
        public static void PlayHit(Vector3 worldPoint, float orbRadius, int layer)
        {
            if (!(orbRadius > 0f) || !float.IsFinite(worldPoint.x) || !float.IsFinite(worldPoint.y) || !float.IsFinite(worldPoint.z)) return;
            var root = new GameObject("Soot Hit Aura");
            root.layer = layer;
            root.transform.position = worldPoint;
            var holder = root.AddComponent<LabSootAura>();
            holder.hitHolder = true;
            holder.FaceCamera();
            PlayJoin(root.transform, orbRadius * 1.6f, true);
            holder.hitAura = Attach(root.transform, orbRadius * 1.3f);
            holder.hitAura.alphaScale = 0f;
        }

        private void FaceCamera()
        {
            Camera view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;
        }

        private void UpdateHit()
        {
            age += Time.deltaTime;
            FaceCamera();
            float u = Mathf.Clamp01(age / HitSeconds);
            // 터짐 뒤에 연기 기운이 차오르고(0.25초), 끝으로 갈수록 옅어진다.
            if (hitAura != null) hitAura.alphaScale = Mathf.Clamp01((age - .15f) / .25f) * (1f - u * u);
            if (u >= 1f) Destroy(gameObject);
        }

        private void PoseJoin()
        {
            float u = Mathf.Clamp01(age / joinSeconds);
            float ease = 1f - (1f - u) * (1f - u) * (1f - u);
            float size = joinWhole ? 1.5f : 1f;
            // 가운데 번쩍임: 아주 빨리 커지고 천천히 사라진다. 검정 구슬이면 끝무렵 한 번 더 크게 번진다.
            float flash = Mathf.Clamp01(1f - u * (joinWhole ? 1.2f : 1.8f));
            float second = joinWhole ? Mathf.Clamp01(1f - Mathf.Abs(u - .55f) / .25f) : 0f;
            glow.transform.localPosition = new Vector3(0f, 0f, -.02f * radius);
            glow.transform.localScale = Vector3.one * (radius * size * (1.2f + 2.6f * ease + 1.5f * second));
            glow.color = new Color(Violet.r * 1.3f, Violet.g * 1.2f, Violet.b, Mathf.Max(.85f * flash * flash, .6f * second));
            for (int i = 0; i < JoinPuffCount; i++)
            {
                SpriteRenderer puff = joinPuffs[i];
                puff.transform.localPosition = joinDirections[i] * (radius * size * (.3f + 1.9f * ease)) + new Vector3(0f, 0f, -.03f * radius);
                puff.transform.localScale = Vector3.one * (radius * size * (.55f + .9f * ease));
                Color tint = Color.Lerp(Ink, Violet, .25f * (1f - u));
                puff.color = new Color(tint.r, tint.g, tint.b, .85f * Mathf.Pow(1f - u, 1.4f) * Mathf.Clamp01(u * 10f + .3f));
            }
            if (u >= 1f) Destroy(gameObject);
        }

        /// <summary>parent(보통 검정 구슬의 오색구슬 그림) 밑에 기운을 붙인다. 이미 있으면 그것을 쓴다.</summary>
        public static LabSootAura Attach(Transform parent, float orbRadius)
        {
            if (parent == null) return null;
            Transform found = parent.Find(AuraName);
            LabSootAura aura = found != null ? found.GetComponent<LabSootAura>() : null;
            if (aura == null)
            {
                var root = new GameObject(AuraName);
                root.layer = parent.gameObject.layer;
                root.transform.SetParent(parent, false);
                aura = root.AddComponent<LabSootAura>();
                aura.Build();
            }
            aura.SetRadius(orbRadius);
            aura.Show(true);
            return aura;
        }

        public void SetRadius(float orbRadius)
        {
            if (float.IsFinite(orbRadius) && orbRadius > 0f) radius = orbRadius;
        }

        public void Show(bool value)
        {
            shown = value;
            if (glow != null) glow.enabled = value;
            for (int i = 0; i < WispCount; i++) if (wisps[i] != null) wisps[i].enabled = value;
        }

        public bool Shown => shown;

        private void Build()
        {
            Sprite puff = LabOrbVanishSmoke.PuffSprite();
            glow = Part("Soot Aura Glow", puff, GlowOrder);
            for (int i = 0; i < WispCount; i++) wisps[i] = Part("Soot Aura Wisp " + i, puff, WispOrder);
            // 처음부터 가닥마다 다른 때에 있게 해 한꺼번에 피어나지 않는다.
            age = Random.value * WispSeconds;
            Pose();
        }

        private SpriteRenderer Part(string label, Sprite sprite, int order)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Update()
        {
            if (hitHolder) { UpdateHit(); return; }
            if (join) { age += Time.deltaTime; PoseJoin(); return; }
            if (!shown) return;
            age += Time.deltaTime;
            Pose();
        }

        private void Pose()
        {
            if (glow == null || join) return;
            // 뒤의 보랏빛 빛무리: 천천히 숨 쉰다.
            float breath = .5f + .5f * Mathf.Sin(age * GlowHz * 2f * Mathf.PI);
            glow.transform.localPosition = new Vector3(0f, 0f, .05f * radius);
            glow.transform.localScale = Vector3.one * (radius * Mathf.Lerp(2.9f, 3.5f, breath));
            glow.color = new Color(Violet.r, Violet.g, Violet.b, Mathf.Lerp(.22f, .42f, breath) * alphaScale);

            // 먹빛 연기 가닥: 구슬 가장자리에서 생겨 돌면서 바깥/위로 번지고, 커지며 옅어진다.
            float spin = age * SpinDegreesPerSecond;
            for (int i = 0; i < WispCount; i++)
            {
                SpriteRenderer wisp = wisps[i];
                float u = Mathf.Repeat(age / WispSeconds + i / (float)WispCount, 1f);
                float angle = (spin + i * (360f / WispCount) + u * 40f) * Mathf.Deg2Rad;
                float reach = radius * Mathf.Lerp(.75f, 1.35f, u);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * reach + Vector3.up * (radius * .35f * u * u);
                wisp.transform.localPosition = offset + new Vector3(0f, 0f, .02f * radius);
                wisp.transform.localScale = Vector3.one * (radius * Mathf.Lerp(.7f, 1.5f, u));
                float fade = Mathf.Clamp01(u * 5f) * (1f - u);
                Color tint = Color.Lerp(Ink, Violet, .18f + .12f * Mathf.Sin(i * 1.7f + age));
                wisp.color = new Color(tint.r, tint.g, tint.b, .75f * fade * alphaScale);
            }
        }
    }
}
