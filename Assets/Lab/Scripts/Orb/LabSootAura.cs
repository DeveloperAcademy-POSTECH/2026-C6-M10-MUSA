using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #696 지귀 검정 구슬의 기운: 판 위에 놓여만 있어도 구슬 둘레에서 먹빛 연기가 천천히 피어올라 감돌고,
    /// 뒤에서 보랏빛 빛무리가 숨 쉬듯 커졌다 작아진다. "연기를 모아 만든 구슬"로 보이게 한다. 표시만 한다.
    /// 오색구슬 그림(LabFiveColorSparkle) 밑에 붙어 같이 카메라를 보고, 그 그림보다 뒤(sortingOrder가 낮게)에 그린다.
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
            if (!shown) return;
            age += Time.deltaTime;
            Pose();
        }

        private void Pose()
        {
            if (glow == null) return;
            // 뒤의 보랏빛 빛무리: 천천히 숨 쉰다.
            float breath = .5f + .5f * Mathf.Sin(age * GlowHz * 2f * Mathf.PI);
            glow.transform.localPosition = new Vector3(0f, 0f, .05f * radius);
            glow.transform.localScale = Vector3.one * (radius * Mathf.Lerp(2.9f, 3.5f, breath));
            glow.color = new Color(Violet.r, Violet.g, Violet.b, Mathf.Lerp(.22f, .42f, breath));

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
                wisp.color = new Color(tint.r, tint.g, tint.b, .75f * fade);
            }
        }
    }
}
