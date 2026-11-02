using UnityEngine;

namespace C6Lab
{
    /// <summary>Water-drop glyph and a small ripple on free B ammunition. Presentation only;
    /// ordinary Water keeps its existing art, identity, collider and throwing physics.</summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiSupplyWaterArt : MonoBehaviour
    {
        private const int Segments = 48;
        private readonly Vector3[] points = new Vector3[Segments + 1];
        private SpriteRenderer drop;
        private LineRenderer ripple;
        private Sprite ownedSprite;
        private Material fallback;
        private Texture texture;
        private float radius = .32f, age;
        private bool shown, held;
        public bool Shown => shown;
        public float AnimationTime => age;
        public int AddedColliderCount => GetComponentsInChildren<Collider>(true).Length;

        public static LabJigwiSupplyWaterArt Create(Transform visual)
        {
            if (visual == null) return null;
            var root = new GameObject("Jigwi B extinguishing water");
            root.layer = visual.gameObject.layer; root.transform.SetParent(visual, false);
            return root.AddComponent<LabJigwiSupplyWaterArt>();
        }
        public void Show(float artRadius, bool clockHeld, LabElementTrailProfile profile)
        {
            radius = float.IsNaN(artRadius) || float.IsInfinity(artRadius) ? .32f : Mathf.Max(.001f, artRadius);
            held = clockHeld;
            if (!shown) age = 0f;
            Ensure(profile); shown = true;
            drop.enabled = ownedSprite != null; ripple.enabled = true;
            if (ownedSprite != null)
            {
                drop.transform.localPosition = new Vector3(0f, radius * .05f, -.04f);
                drop.transform.localScale = Vector3.one * (radius * 1.25f / Mathf.Max(.01f, ownedSprite.bounds.size.y));
            }
            Draw();
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
            ripple.SetPositions(points); ripple.widthMultiplier = radius * .07f;
            ripple.startColor = ripple.endColor = new Color(.33f, .88f, 1f, Mathf.Lerp(.85f, .15f, phase));
        }
        public void Clear()
        {
            shown = false; age = 0f;
            if (drop != null) drop.enabled = false;
            if (ripple != null) ripple.enabled = false;
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (ownedSprite != null) Destroy(ownedSprite);
            if (fallback != null) Destroy(fallback);
        }
    }
}
