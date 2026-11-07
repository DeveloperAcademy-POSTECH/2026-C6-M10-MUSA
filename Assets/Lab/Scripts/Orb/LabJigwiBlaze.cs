using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #540 지귀 A 실패 때 구슬·북이 타오르는 불 (코드로 그린 불꽃 `LabProceduralFire`). 불덩이가 판에 닿는 순간 "확" 붙어
    /// 크게 일렁이고, 구슬은 사라지기 직전에 한 번 더 확 치솟으며 검게 그을린다. 북은 꺼질 때 잦아든다.
    /// Host가 준 남은 초만 따른다(그림만, 판정 없음). 구슬 뿌리에 붙어 공이 굴러도 돌지 않는다. 일시정지(시간 0)면 멈춘다.
    /// </summary>
    public sealed class LabJigwiBlaze : MonoBehaviour
    {
        private const int BackCount = 5, FrontCount = 4, EmberCount = 6;
        private const float CatchSeconds = .18f, DieSeconds = .35f, CharSeconds = .35f;
        private static readonly Color HaloColor = new Color(1f, .5f, .08f, .55f), CharColor = new Color(.12f, .05f, .03f, 1f);
        private SpriteRenderer halo, scorch;
        private readonly SpriteRenderer[] back = new SpriteRenderer[BackCount], front = new SpriteRenderer[FrontCount];
        private readonly SpriteRenderer[] embers = new SpriteRenderer[EmberCount];
        private float left, age, radius = .3f;
        private bool burnsAway, shown;

        public bool Burning => left > 0f;

        public static LabJigwiBlaze Create(Transform orbRoot)
        {
            var go = new GameObject("Jigwi Blaze (#540)");
            go.layer = orbRoot.gameObject.layer;
            go.transform.SetParent(orbRoot, false);
            return go.AddComponent<LabJigwiBlaze>();
        }

        /// <summary>남은 초(0이면 잦아듦). burnsAway면 끝에 검게 그을리며 사라진다(구슬), 아니면 잦아든다(북).</summary>
        public void Show(float secondsLeft, float orbRadius, bool burnsAwayAtEnd)
        {
            if (orbRadius > 0f) radius = orbRadius;
            burnsAway = burnsAwayAtEnd;
            if (!(secondsLeft > 0f) || float.IsInfinity(secondsLeft))
            {
                if (left > 0f) left = Mathf.Min(left, DieSeconds);
                return;
            }
            Ensure();
            if (left <= 0f) age = 0f;
            if (left <= 0f || Mathf.Abs(left - secondsLeft) > .3f) left = secondsLeft;
            SetShown(true);
            Pose();
        }

        private void Ensure()
        {
            if (halo != null) return;
            halo = Part("Blaze Halo", LabProceduralFire.Glow(), HaloColor, 0);
            for (int i = 0; i < BackCount; i++) back[i] = Part("Blaze Back " + i, LabProceduralFire.Flame(i), Color.white, 1);
            for (int i = 0; i < EmberCount; i++) embers[i] = Part("Blaze Ember " + i, LabProceduralFire.Glow(), new Color(1f, .85f, .3f, 1f), 4);
            scorch = Part("Blaze Char", LabProceduralFire.Glow(), CharColor, 9);
            for (int i = 0; i < FrontCount; i++) front[i] = Part("Blaze Front " + i, LabProceduralFire.Flame(i + 2), Color.white, 10);
        }

        private SpriteRenderer Part(string label, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(label) { layer = gameObject.layer };
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }

        private void SetShown(bool on)
        {
            if (shown == on || halo == null) return;
            shown = on;
            foreach (SpriteRenderer part in GetComponentsInChildren<SpriteRenderer>(true)) part.enabled = on;
        }

        private void Update()
        {
            if (left <= 0f) return;
            float step = Time.deltaTime;
            left = Mathf.Max(0f, left - step);
            age += step;
            if (left <= 0f) { SetShown(false); return; }
            Pose();
        }

        private void Pose()
        {
            if (halo == null) return;
            float t = age;
            // Catches with a whoosh (overshoot), then rages; a burning orb flares once more as it chars away.
            float caught = Mathf.Clamp01(t / CatchSeconds);
            float flare = caught < 1f ? Mathf.Lerp(.2f, 1.35f, caught) : 1f + .35f * Mathf.Exp(-(t - CatchSeconds) * 7f);
            float charring = burnsAway ? Mathf.Clamp01(1f - left / CharSeconds) : 0f;
            float dying = burnsAway ? 1f : Mathf.Clamp01(left / DieSeconds);
            float size = radius * flare * (1f + .35f * charring) * dying;
            float flicker = .85f + .15f * Mathf.PerlinNoise(t * 9f, .4f);

            halo.transform.localPosition = new Vector3(0f, radius * .3f, 0f);
            halo.transform.localScale = Vector3.one * Scale(halo, size * 3.4f * flicker);
            Color h = HaloColor; h.a *= Mathf.Clamp01(caught * 2f); halo.color = h;

            for (int i = 0; i < BackCount; i++)
            {
                // Tall tongues rising from behind the ball, fanned across its top.
                float spread = (i - (BackCount - 1) * .5f) / ((BackCount - 1) * .5f);
                Tongue(back[i], new Vector2(spread * radius * .75f, -radius * .15f), spread * -14f, size * (1.9f - .5f * Mathf.Abs(spread)), t, i);
            }
            for (int i = 0; i < FrontCount; i++)
            {
                // Short licks over the front of the ball, low so its colour stays readable until it chars.
                float spread = (i - (FrontCount - 1) * .5f) / ((FrontCount - 1) * .5f);
                Tongue(front[i], new Vector2(spread * radius * .7f, -radius * .55f + Mathf.Abs(spread) * radius * .2f), spread * -22f,
                    size * (.75f + .5f * charring), t, i + 7);
            }
            scorch.transform.localScale = Vector3.one * Scale(scorch, radius * 2.3f);
            Color c = CharColor; c.a = charring * .92f; scorch.color = c;

            for (int i = 0; i < EmberCount; i++)
            {
                float cycle = Mathf.Repeat(t * 1.4f + i * .37f, 1f);
                float x = Mathf.Sin(i * 2.1f + Mathf.Floor(t * 1.4f + i * .37f) * 1.7f) * radius * .8f;
                embers[i].transform.localPosition = new Vector3(x + Mathf.Sin(cycle * 6f + i) * radius * .15f, radius * (.3f + 2.4f * cycle), 0f);
                embers[i].transform.localScale = Vector3.one * Scale(embers[i], radius * .22f * (1f - cycle));
                Color e = Color.Lerp(new Color(1f, .9f, .4f, 1f), new Color(1f, .35f, .05f, 1f), cycle);
                e.a = (1f - cycle) * caught * dying; embers[i].color = e;
            }
        }

        private static void Tongue(SpriteRenderer tongue, Vector2 at, float tilt, float height, float t, int seed)
        {
            // Licks: each tongue stretches, sways and swaps shape on its own beat.
            tongue.sprite = LabProceduralFire.Flame(seed + (int)(t * 12f + seed * .5f));
            float stretch = 1f + .28f * Mathf.Sin(t * (13f + seed) + seed) + .12f * (Mathf.PerlinNoise(t * 7f, seed) - .5f);
            float sway = tilt + 9f * Mathf.Sin(t * (5f + seed * .3f) + seed * 1.3f);
            Vector2 sprite = tongue.sprite.bounds.size;
            float s = height / Mathf.Max(.0001f, sprite.y);
            tongue.transform.localPosition = new Vector3(at.x, at.y, 0f);
            tongue.transform.localRotation = Quaternion.Euler(0f, 0f, sway);
            tongue.transform.localScale = new Vector3(s * (1f - .12f * (stretch - 1f)), s * stretch, 1f);
        }

        private static float Scale(SpriteRenderer renderer, float width) =>
            width / Mathf.Max(.0001f, renderer.sprite.bounds.size.x);
    }
}
