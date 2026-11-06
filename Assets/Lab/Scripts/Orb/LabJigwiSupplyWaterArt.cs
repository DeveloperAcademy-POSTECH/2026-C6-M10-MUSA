using UnityEngine;

namespace C6Lab
{
    /// <summary>Water-drop glyph and a small ripple on Jigwi Water (talisman reward). #509: B's special Water adds a
    /// deep-blue body tint, a pulsing glow and three orbiting sparkles so it reads as precious. Presentation only;
    /// ordinary Water keeps its existing art, identity, collider and throwing physics.</summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiSupplyWaterArt : MonoBehaviour
    {
        private const int Segments = 48, SparkleCount = 3;
        private static readonly Color SpecialTint = new Color(.05f, .22f, .78f, .55f);
        private readonly Vector3[] points = new Vector3[Segments + 1];
        private SpriteRenderer drop, tint, glow, seal, emblem;
        private static readonly Color TalismanTint = new Color(.98f, .86f, .5f, .96f), SealRed = new Color(.78f, .09f, .07f, .95f);
        private readonly SpriteRenderer[] sparkles = new SpriteRenderer[SparkleCount];
        private static Sprite discSprite, glowSprite, starSprite;
        private LineRenderer ripple;
        private Sprite ownedSprite;
        private Material fallback;
        private Texture texture;
        private float radius = .32f, age;
        private bool shown, held, special, talisman;
        public bool Shown => shown;
        /// <summary>#509: B's special Water look (darker body, glow, sparkles) is on.</summary>
        public bool Special => shown && special;
        /// <summary>#540: the talisman orb look (hanji body, red seal, gold glow and sparkles) is on.</summary>
        public bool Talisman => shown && talisman;
        public int VisibleSparkles { get; private set; }
        public float AnimationTime => age;
        public int AddedColliderCount => GetComponentsInChildren<Collider>(true).Length;

        public static LabJigwiSupplyWaterArt Create(Transform visual)
        {
            if (visual == null) return null;
            var root = new GameObject("Jigwi B extinguishing water");
            root.layer = visual.gameObject.layer; root.transform.SetParent(visual, false);
            return root.AddComponent<LabJigwiSupplyWaterArt>();
        }
        public void Show(float artRadius, bool clockHeld, LabElementTrailProfile profile, bool specialWater = false, bool talismanOrb = false)
        {
            radius = float.IsNaN(artRadius) || float.IsInfinity(artRadius) ? .32f : Mathf.Max(.001f, artRadius);
            held = clockHeld;
            if (!shown) age = 0f;
            Ensure(profile); shown = true; special = specialWater && !talismanOrb; talisman = talismanOrb;
            EnsureSpecial();
            // #540: the talisman orb shows the folded talisman's red seal instead of the water drop.
            drop.enabled = ownedSprite != null && !talisman; ripple.enabled = true;
            if (ownedSprite != null)
            {
                drop.transform.localPosition = new Vector3(0f, radius * .05f, -.04f);
                drop.transform.localScale = Vector3.one * (radius * 1.25f / Mathf.Max(.01f, ownedSprite.bounds.size.y));
            }
            // #509: the special drop is a deeper, richer blue than ordinary supplied Water.
            drop.color = special ? new Color(.45f, .78f, 1f, 1f) : new Color(.77f, .97f, 1f, .97f);
            Draw();
        }

        private void EnsureSpecial()
        {
            if (tint == null)
            {
                discSprite = discSprite != null ? discSprite : RuntimeSprite("Jigwi special disc (runtime)", 64, (d) => d <= .48f ? 1f : Mathf.Clamp01((.5f - d) * 50f));
                glowSprite = glowSprite != null ? glowSprite : RuntimeSprite("Jigwi special glow (runtime)", 64, (d) => Mathf.Pow(Mathf.Clamp01(1f - d * 2f), 2.2f));
                starSprite = starSprite != null ? starSprite : RuntimeStar();
                tint = Child("Special body tint", discSprite, 8);
                glow = Child("Special glow", glowSprite, 7);
                for (int i = 0; i < SparkleCount; i++) sparkles[i] = Child("Special sparkle " + (i + 1), starSprite, 11);
            }
            if (seal == null)
            {
                seal = Child("Talisman seal", LabJigwiTalismanOverlay.StampSprite(), 12);
                emblem = Child("Talisman seal emblem", LabJigwiTalismanOverlay.StampEmblemSprite(), 13);
            }
            bool fancy = special || talisman;
            tint.enabled = glow.enabled = fancy;
            seal.enabled = emblem.enabled = talisman;
            foreach (var sparkle in sparkles) sparkle.enabled = fancy;
            if (!fancy) VisibleSparkles = 0;
        }
        private SpriteRenderer Child(string name, Sprite sprite, int order)
        {
            var child = new GameObject(name); child.layer = gameObject.layer; child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
            if (fallback == null) fallback = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            renderer.sharedMaterial = fallback; renderer.enabled = false;
            return renderer;
        }
        private static Sprite RuntimeSprite(string name, int size, System.Func<float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = name, hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size - .5f, dy = (y + .5f) / size - .5f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(Mathf.Sqrt(dx * dx + dy * dy)));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave; return sprite;
        }
        /// <summary>A soft four-point twinkle.</summary>
        private static Sprite RuntimeStar()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Jigwi special sparkle (runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / size - .5f) * 2f, dy = Mathf.Abs((y + .5f) / size - .5f) * 2f;
                    float arm = Mathf.Max(Mathf.Clamp01(1f - dx * 6f) * Mathf.Clamp01(1f - dy), Mathf.Clamp01(1f - dy * 6f) * Mathf.Clamp01(1f - dx));
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(arm + core));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave; return sprite;
        }
        private void Ensure(LabElementTrailProfile profile)
        {
            if (drop == null)
            {
                var icon = new GameObject("Water drop glyph"); icon.layer = gameObject.layer;
                icon.transform.SetParent(transform, false); drop = icon.AddComponent<SpriteRenderer>();
                drop.sortingOrder = 10; drop.color = new Color(.77f, .97f, 1f, .97f);
                var circle = new GameObject("Water ripple"); circle.layer = gameObject.layer;
                circle.transform.SetParent(transform, false); ripple = circle.AddComponent<LineRenderer>();
                ripple.useWorldSpace = false; ripple.loop = false; ripple.positionCount = Segments + 1;
                ripple.numCornerVertices = 1; ripple.numCapVertices = 1; ripple.sortingOrder = 9;
                ripple.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ripple.receiveShadows = false;
            }
            Material material = profile != null ? profile.Material : null;
            if (material == null)
            {
                if (fallback == null) fallback = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
                material = fallback;
            }
            drop.sharedMaterial = ripple.sharedMaterial = material;
            Texture2D next = profile != null ? profile.ParticleTexture : null;
            if (texture != next)
            {
                if (ownedSprite != null) Destroy(ownedSprite);
                ownedSprite = next != null ? Sprite.Create(next, new Rect(0, 0, next.width, next.height),
                    Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect) : null;
                if (ownedSprite != null) ownedSprite.hideFlags = HideFlags.DontSave;
                texture = next; drop.sprite = ownedSprite;
                var ink = new MaterialPropertyBlock(); ink.SetTexture("_BaseMap", next != null ? next : Texture2D.whiteTexture);
                ink.SetTexture("_MainTex", next != null ? next : Texture2D.whiteTexture);
                ink.SetColor("_BaseColor", Color.white); ink.SetFloat("_UvSwap", 0f); drop.SetPropertyBlock(ink);
                var white = new MaterialPropertyBlock(); white.SetTexture("_BaseMap", Texture2D.whiteTexture);
                white.SetTexture("_MainTex", Texture2D.whiteTexture); white.SetColor("_BaseColor", Color.white);
                white.SetFloat("_UvSwap", 0f); ripple.SetPropertyBlock(white);
            }
        }
        private void LateUpdate()
        {
            if (!shown) return;
            if (!held) age += Time.deltaTime;
            Draw();
        }
        private void Draw()
        {
            if (ripple == null) return;
            float phase = Mathf.Repeat(age, 1.1f) / 1.1f;
            float r = radius * Mathf.Lerp(1.03f, 1.22f, phase);
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                points[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -.025f);
            }
            ripple.SetPositions(points); ripple.widthMultiplier = radius * (special || talisman ? .1f : .07f);
            ripple.startColor = ripple.endColor = talisman ? new Color(1f, .84f, .3f, Mathf.Lerp(.95f, .2f, phase))
                : special ? new Color(.35f, .7f, 1f, Mathf.Lerp(.95f, .2f, phase))
                : new Color(.33f, .88f, 1f, Mathf.Lerp(.85f, .15f, phase));
            DrawSpecial();
        }
        /// <summary>#509: darker body, a breathing glow and three sparkles circling and twinkling.</summary>
        private void DrawSpecial()
        {
            if (tint == null || !(special || talisman)) { VisibleSparkles = 0; return; }
            tint.transform.localPosition = new Vector3(0f, 0f, -.02f);
            tint.transform.localScale = Vector3.one * radius * 2.02f;
            tint.color = talisman ? TalismanTint : SpecialTint;
            float breath = .5f + .5f * Mathf.Sin(age * Mathf.PI * 2f / 1.4f);
            glow.transform.localPosition = new Vector3(0f, 0f, .01f);
            glow.transform.localScale = Vector3.one * radius * Mathf.Lerp(3.1f, 3.6f, breath);
            glow.color = talisman ? new Color(1f, .82f, .3f, Mathf.Lerp(.4f, .8f, breath))
                : new Color(.35f, .65f, 1f, Mathf.Lerp(.35f, .7f, breath));
            if (talisman && seal != null)
            {
                // The red seal sits square on the orb, slightly tilted like the stamp on the talisman.
                float sealScale = radius * 1.25f / Mathf.Max(.01f, seal.sprite.bounds.size.x);
                seal.transform.localPosition = emblem.transform.localPosition = new Vector3(0f, 0f, -.045f);
                seal.transform.localRotation = emblem.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
                seal.transform.localScale = Vector3.one * sealScale;
                emblem.transform.localScale = Vector3.one * sealScale * .8f;
                seal.color = emblem.color = SealRed;
            }
            VisibleSparkles = 0;
            for (int i = 0; i < SparkleCount; i++)
            {
                float angle = age * 1.6f + i * Mathf.PI * 2f / SparkleCount;
                float twinkle = Mathf.Clamp01(Mathf.Sin(age * 5.3f + i * 2.1f) * .5f + .5f);
                sparkles[i].transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * 1.08f
                    + new Vector3(0f, 0f, -.05f);
                sparkles[i].transform.localScale = Vector3.one * radius * Mathf.Lerp(.25f, .55f, twinkle);
                sparkles[i].transform.localRotation = Quaternion.Euler(0f, 0f, age * 90f + i * 30f);
                sparkles[i].color = talisman ? new Color(1f, .93f, .6f, Mathf.Lerp(.35f, 1f, twinkle))
                    : new Color(.9f, .97f, 1f, Mathf.Lerp(.35f, 1f, twinkle));
                VisibleSparkles++;
            }
        }
        public void Clear()
        {
            shown = false; age = 0f; VisibleSparkles = 0;
            if (drop != null) drop.enabled = false;
            if (ripple != null) ripple.enabled = false;
            if (tint != null) tint.enabled = false;
            if (glow != null) glow.enabled = false;
            if (seal != null) seal.enabled = false;
            if (emblem != null) emblem.enabled = false;
            talisman = false;
            foreach (var sparkle in sparkles) if (sparkle != null) sparkle.enabled = false;
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (ownedSprite != null) Destroy(ownedSprite);
            if (fallback != null) Destroy(fallback);
        }
    }
}
