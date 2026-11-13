using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #719 콤보 예고 동안 던지기 막기: 내 판 위쪽 가장자리를 따라 금줄(새끼줄에 흰 한지와 숯을 끼운 줄)이 쳐진다.
    /// 그동안 박으로 던지려던 구슬은 위로 미끄러져 튕겨 돌아오고, 금줄이 출렁인다. 생성/합치기/넘기기는 그대로 된다.
    /// 콤보 중 오색구슬을 던지려 해도 금줄이 잠깐 나타나 출렁인다. 표시만 하고 터치는 막지 않는다 (막는 건 LabNetwork.ThrowAllowed).
    /// 구미호 매혹의 분홍 벽과 겹치지 않게 짚 색/먹색만 쓴다.
    /// </summary>
    public sealed class LabComboThrowSeal : MonoBehaviour
    {
        public const int Segments = 18, Tabs = 6, Order = 40;
        public const float AppearSeconds = .25f, HideSeconds = .2f, KnockSeconds = .45f, BriefSeconds = .9f;
        private static readonly Color Straw = new Color(.87f, .76f, .5f), KnockTint = new Color(1f, .55f, .45f);
        private static Sprite ropeSprite, paperSprite, coalSprite;

        private LabOrbBoard board;
        private SpriteRenderer[] rope, tabs;
        private bool held;
        private float shownFor, alpha, knock, briefUntil = -1f;

        public bool Visible => alpha > .001f;
        public int Knocks { get; private set; }

        public static LabComboThrowSeal For(LabOrbBoard board)
        {
            if (board == null) return null;
            LabComboThrowSeal seal = board.GetComponentInChildren<LabComboThrowSeal>(true);
            if (seal != null) return seal;
            var item = new GameObject("Combo Throw Seal");
            item.layer = board.gameObject.layer;
            item.transform.SetParent(board.transform, false);
            seal = item.AddComponent<LabComboThrowSeal>();
            seal.board = board;
            return seal;
        }

        /// <summary>Kept up while the combo warning is on.</summary>
        public void SetHeld(bool value) => held = value;

        /// <summary>A throw was just turned away: the rope jolts (and shows for a moment if it was not up).</summary>
        public void Knock()
        {
            Knocks++;
            knock = KnockSeconds;
            if (!held) briefUntil = Time.unscaledTime + BriefSeconds;
        }

        private void LateUpdate()
        {
            bool want = held || Time.unscaledTime < briefUntil;
            float dt = Time.unscaledDeltaTime;
            if (want) shownFor += dt; else shownFor = 0f;
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, dt / (want ? AppearSeconds : HideSeconds));
            knock = Mathf.Max(0f, knock - dt);
            if (alpha <= .001f)
            {
                if (rope != null && rope[0].gameObject.activeSelf) SetActive(false);
                return;
            }
            if (board == null) board = GetComponentInParent<LabOrbBoard>();
            if (board == null || board.AreaShape == null) return;
            EnsureParts();
            SetActive(true);
            Pose();
        }

        private void Pose()
        {
            Rect bounds = board.CenterBounds;
            float width = Mathf.Max(.01f, bounds.width);
            float thickness = width * .035f;
            float drop = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shownFor / AppearSeconds))) * thickness * 3f;
            float k = knock / KnockSeconds;
            float now = Time.unscaledTime;
            Color tint = Color.Lerp(Color.white, KnockTint, k * k);
            tint.a = alpha;

            Vector2 Point(float u)
            {
                float x = Mathf.Lerp(bounds.xMin + width * .03f, bounds.xMax - width * .03f, u);
                float top = board.AreaShape.VerticalRange(x, 0f).y - thickness * .6f;
                // The rope sags a little in the middle, and jolts (a travelling wave) when a throw hits it.
                float sag = Mathf.Sin(u * Mathf.PI) * thickness * .5f;
                float jolt = Mathf.Sin(u * Mathf.PI * 3f - now * 30f) * Mathf.Sin(u * Mathf.PI) * thickness * 1.2f * k;
                return new Vector2(x, top - sag - jolt + drop);
            }

            for (int i = 0; i < Segments; i++)
            {
                Vector2 a = Point(i / (float)Segments), b = Point((i + 1f) / Segments);
                Vector2 d = b - a;
                Transform part = rope[i].transform;
                part.position = new Vector3((a.x + b.x) * .5f, (a.y + b.y) * .5f, 0f);
                part.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                part.localScale = new Vector3(d.magnitude * 1.12f, thickness * 4f, 1f); // the rope sprite is 1 x 0.25
                rope[i].color = tint;
            }
            for (int i = 0; i < Tabs; i++)
            {
                float u = (i + .5f) / Tabs;
                Vector2 at = Point(u);
                Transform part = tabs[i].transform;
                part.position = new Vector3(at.x, at.y - thickness * .3f, 0f);
                float swing = Mathf.Sin(now * 2.1f + i * 1.7f) * 5f + Mathf.Sin(now * 22f + i) * 28f * k;
                part.rotation = Quaternion.Euler(0f, 0f, swing);
                bool paper = i % 2 == 0;
                part.localScale = paper ? new Vector3(thickness * 1.5f, thickness * 1.6f, 1f) : Vector3.one * thickness * 1.3f;
                tabs[i].color = tint;
            }
        }

        private void SetActive(bool value)
        {
            if (rope == null) return;
            foreach (SpriteRenderer part in rope) part.gameObject.SetActive(value);
            foreach (SpriteRenderer part in tabs) part.gameObject.SetActive(value);
        }

        private void EnsureParts()
        {
            if (rope != null) return;
            rope = new SpriteRenderer[Segments];
            for (int i = 0; i < Segments; i++) rope[i] = Part("Rope " + i, RopeSprite(), Order);
            tabs = new SpriteRenderer[Tabs];
            for (int i = 0; i < Tabs; i++) tabs[i] = Part(i % 2 == 0 ? "Paper " + i : "Coal " + i, i % 2 == 0 ? PaperSprite() : CoalSprite(), Order - 1);
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

        // ---- 그려 만든 그림: 꼰 새끼줄 한 토막(1 x 1), 지그재그 한지(위쪽 가운데가 축), 숯 ----

        private static Sprite RopeSprite()
        {
            if (ropeSprite != null) return ropeSprite;
            const int w = 64, h = 16;
            var pixels = new Color[w * h];
            Color dark = new Color(.55f, .42f, .24f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = (y + .5f) / h * 2f - 1f;
                    float edge = Mathf.Clamp01((1f - Mathf.Abs(v)) * 4f);
                    // Twisted strands: diagonal grooves, darker toward the rope's edges.
                    float strand = Mathf.Repeat((x + y * 1.4f) / 9f, 1f);
                    float groove = Mathf.SmoothStep(0f, 1f, Mathf.Abs(strand - .5f) * 2f);
                    Color c = Color.Lerp(Straw, dark, .55f * groove + .3f * Mathf.Abs(v));
                    c.a = edge;
                    pixels[y * w + x] = c;
                }
            ropeSprite = Make("Lab combo seal rope", w, h, pixels, new Vector2(.5f, .5f), TextureWrapMode.Repeat);
            return ropeSprite;
        }

        private static Sprite PaperSprite()
        {
            if (paperSprite != null) return paperSprite;
            const int w = 16, h = 32;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fromTop = (h - y - .5f) / h;
                    // A folded zigzag strip: the strip shifts side to side in four steps going down.
                    float step = Mathf.Floor(fromTop * 4f) % 2f == 0f ? .15f : -.15f;
                    float u = (x + .5f) / w - .5f - step * Mathf.Clamp01(fromTop * 4f);
                    float inside = Mathf.Clamp01((.3f - Mathf.Abs(u)) * w * .5f);
                    Color c = Color.Lerp(new Color(.82f, .8f, .76f), Color.white, Mathf.Repeat(fromTop * 4f, 1f) < .15f ? 0f : 1f);
                    c.a = inside;
                    pixels[y * w + x] = c;
                }
            paperSprite = Make("Lab combo seal paper", w, h, pixels, new Vector2(.5f, 1f), TextureWrapMode.Clamp);
            return paperSprite;
        }

        private static Sprite CoalSprite()
        {
            if (coalSprite != null) return coalSprite;
            const int s = 24;
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + .5f) / s * 2f - 1f, v = (y + .5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(u * u * 1.3f + (v + .1f) * (v + .1f));
                    float a = Mathf.Clamp01((.85f - r) * s * .4f);
                    Color c = Color.Lerp(new Color(.08f, .07f, .07f), new Color(.25f, .22f, .2f), Mathf.Clamp01(-u - v) * .6f);
                    c.a = a;
                    pixels[y * s + x] = c;
                }
            coalSprite = Make("Lab combo seal coal", s, s, pixels, new Vector2(.5f, 1f), TextureWrapMode.Clamp);
            return coalSprite;
        }

        private static Sprite Make(string name, int w, int h, Color[] pixels, Vector2 pivot, TextureWrapMode wrap)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = wrap, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            // One unit across the sprite's width so scale = world size.
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), pivot, w);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
