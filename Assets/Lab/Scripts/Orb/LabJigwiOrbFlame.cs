using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Only A's presentation. It neither changes expiry nor replaces the independent bomb fire.
    /// #479: no flame sprite any more. In the last <see cref="CharSeconds"/> before the Host deadline the orb
    /// chars like paper from the bottom up: dark ash below a glowing, ragged ember line that rises until the
    /// whole orb is black when the Host removes it. Drawn as a circle over the orb, so the shared orb
    /// art/shader is untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiOrbFlame : MonoBehaviour
    {
        /// <summary>One orb chars during the same second the next one still waits (staggered 1 s apart).</summary>
        public const float CharSeconds = 1f;
        private const int Size = 64;
        private static readonly Color32 Clear32 = new Color32(0, 0, 0, 0);
        private Mesh mesh;
        private MeshRenderer flame;
        private Material material;
        private Texture2D texture;
        private Color32[] pixels;
        private float[] edge;
        private MaterialPropertyBlock properties;
        private float left, age, radius = .32f, drawn = -1f;
        private bool held;
        private static int nextSeed;
        /// <summary>A Host deadline is scheduled for this orb (it may not be charring yet).</summary>
        public bool Burning => left > 0f && flame != null;
        /// <summary>The bottom-up char is on screen (only the last second).</summary>
        public bool Charring => flame != null && flame.enabled;
        /// <summary>0 = untouched, 1 = fully charred.</summary>
        public float Progress => CharProgress(left);
        public float SecondsLeft => Mathf.Max(0f, left);
        public float AnimationTime => age;
        public bool Held => held;
        public MeshRenderer FlameRenderer => flame;
        public Texture2D CharTexture => texture;

        public static LabJigwiOrbFlame Create(Transform orb)
        {
            var go = new GameObject("Jigwi A Orb Char");
            go.layer = orb.gameObject.layer;
            go.transform.SetParent(orb, false);
            return go.AddComponent<LabJigwiOrbFlame>();
        }

        public static float CharProgress(float secondsLeft) =>
            !(secondsLeft > 0f) || float.IsInfinity(secondsLeft) ? 0f : Mathf.Clamp01(1f - secondsLeft / CharSeconds);

        public void Show(float secondsLeft, float orbRadius, bool clockHeld = false)
        {
            held = clockHeld;
            radius = Mathf.Max(.01f, orbRadius);
            if (!(secondsLeft > 0f) || float.IsInfinity(secondsLeft))
            { Clear(); return; }
            if (left <= 0f) age = 0f;
            // The caller supplies Host deadline minus its battle clock. Never create a new deadline here.
            left = secondsLeft;
            EnsureArt();
            Pose();
        }

        public void SetRadius(float orbRadius)
        {
            radius = Mathf.Max(.01f, orbRadius);
            if (flame != null) Pose();
        }

        private void Update()
        {
            if (left <= 0f || flame == null) return;
            if (!held)
            {
                float step = Time.deltaTime;
                left = Mathf.Max(0f, left - step);
                age += step;
            }
            Pose();
        }

        private void EnsureArt()
        {
            if (flame != null) return;
            properties = new MaterialPropertyBlock();
            var art = new GameObject("Jigwi A Char Art");
            art.layer = gameObject.layer;
            art.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Jigwi A char quad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f),
                new Vector3(.5f, .5f, 0f), new Vector3(-.5f, .5f, 0f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            art.AddComponent<MeshFilter>().sharedMesh = mesh;
            flame = art.AddComponent<MeshRenderer>();
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Jigwi A char (runtime)", hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            pixels = new Color32[Size * Size];
            edge = RaggedEdge(nextSeed = (nextSeed + 37) % 997); // each orb its own ragged front
            Material source = Resources.Load<Material>("Jigwi/SurfaceFlame");
            if (source != null) material = new Material(source);
            else
            {
                Debug.LogWarning("Jigwi A orb char material is missing; using a transparent unlit fallback.", this);
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                material = new Material(shader);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            material.name = "Jigwi A orb char (runtime)";
            material.hideFlags = HideFlags.DontSave;
            flame.sharedMaterial = material;
            flame.shadowCastingMode = ShadowCastingMode.Off;
            flame.receiveShadows = false;
            flame.lightProbeUsage = LightProbeUsage.Off;
            flame.reflectionProbeUsage = ReflectionProbeUsage.Off;
            flame.enabled = false;
        }

        private void Pose()
        {
            if (flame == null) return;
            float progress = Progress;
            flame.enabled = left > 0f && progress > 0f;
            if (!flame.enabled) return;
            flame.transform.localPosition = new Vector3(0f, 0f, -radius * 1.05f);
            flame.transform.localScale = new Vector3(radius * 2.04f, radius * 2.04f, 1f);
            if (!held || Mathf.Abs(progress - drawn) > .004f) Paint(progress, age);
            // The ink flame shader with no sway/speed simply draws the texture.
            properties.SetColor("_BaseColor", Color.white); properties.SetColor("_Color", Color.white);
            properties.SetFloat("_FlameTime", 0f);
            properties.SetFloat("_FlameSpeed", 0f);
            properties.SetFloat("_Sway", 0f);
            properties.SetFloat("_Seed", 0f);
            flame.SetPropertyBlock(properties);
        }

        /// <summary>Paper burn: ash below a ragged front, a thin ember line on it, a faint scorch just above.</summary>
        private void Paint(float progress, float time)
        {
            drawn = progress;
            // The front starts just under the orb and passes its top as the Host removes it.
            float front = Mathf.Lerp(-.12f, 1.14f, progress);
            float flicker = .85f + .15f * Mathf.Sin(time * 23f);
            for (int y = 0; y < Size; y++)
            {
                float v = (y + .5f) / Size;
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + .5f) / Size;
                    float dx = u - .5f, dy = v - .5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    int i = y * Size + x;
                    if (r > .5f) { pixels[i] = Clear32; continue; }
                    float rim = Mathf.Clamp01((.5f - r) / .03f);
                    float d = front + edge[x] * .09f - v; // > 0: already burnt
                    Color c;
                    if (d > .07f)
                    {
                        float speck = Hash(x, y) * .12f;
                        c = new Color(.13f + speck, .09f + speck * .6f, .07f, .94f);
                    }
                    else if (d > 0f)
                    {
                        float t = d / .07f; // 0 at the front, 1 into the ash
                        Color ember = Color.Lerp(new Color(1f, .86f, .36f, 1f), new Color(.95f, .32f, .05f, 1f), t);
                        c = Color.Lerp(ember, new Color(.16f, .1f, .07f, .94f), t * t);
                        c.r *= flicker; c.g *= flicker;
                    }
                    else if (d > -.06f)
                    {
                        float t = 1f + d / .06f; // the scorch darkens towards the front
                        c = new Color(.45f, .22f, .06f, .35f * t * t);
                    }
                    else { pixels[i] = Clear32; continue; }
                    c.a *= rim;
                    pixels[i] = c;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private static float[] RaggedEdge(int seed)
        {
            var values = new float[Size];
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                values[x] = .55f * Mathf.Sin(u * 13.1f + seed) + .3f * Mathf.Sin(u * 29.7f + seed * 1.7f)
                    + .15f * (Hash(x, seed) * 2f - 1f);
            }
            return values;
        }

        private static float Hash(int x, int y)
        {
            uint h = unchecked((uint)(x * 374761393 + y * 668265263));
            h = unchecked((h ^ (h >> 13)) * 1274126177u);
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        public void Clear()
        {
            left = age = 0f; drawn = -1f;
            if (flame != null) flame.enabled = false;
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (mesh != null) Dispose(mesh);
            if (material != null) Dispose(material);
            if (texture != null) Dispose(texture);
        }
        private static void Dispose(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
