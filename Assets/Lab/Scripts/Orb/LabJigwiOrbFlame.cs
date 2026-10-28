using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>Only A's presentation. It neither changes expiry nor replaces the independent bomb fire.</summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiOrbFlame : MonoBehaviour
    {
        private Mesh mesh;
        private MeshRenderer flame;
        private Material material;
        private MaterialPropertyBlock properties;
        private float left, age, radius = .32f;
        private bool held;
        public bool Burning => left > 0f && flame != null && flame.enabled;
        public float SecondsLeft => Mathf.Max(0f, left);
        public float AnimationTime => age;
        public bool Held => held;
        public MeshRenderer FlameRenderer => flame;

        public static LabJigwiOrbFlame Create(Transform orb)
        {
            var go = new GameObject("Jigwi A Orb Flame");
            go.layer = orb.gameObject.layer;
            go.transform.SetParent(orb, false);
            return go.AddComponent<LabJigwiOrbFlame>();
        }

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
            var art = new GameObject("Jigwi A Flame Art");
            art.layer = gameObject.layer;
            art.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Jigwi A flame quad", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-.5f, -.08f, 0f), new Vector3(.5f, -.08f, 0f),
                new Vector3(.5f, .92f, 0f), new Vector3(-.5f, .92f, 0f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            art.AddComponent<MeshFilter>().sharedMesh = mesh;
            flame = art.AddComponent<MeshRenderer>();
            Material source = Resources.Load<Material>("Jigwi/SurfaceFlame");
            if (source != null) material = new Material(source);
            else
            {
                Debug.LogWarning("Jigwi A orb flame material is missing; using its transparent texture fallback.", this);
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                material = new Material(shader);
                Texture2D texture = Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash");
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            material.name = "Jigwi A orb flame (runtime)";
            material.hideFlags = HideFlags.DontSave;
            flame.sharedMaterial = material;
            flame.shadowCastingMode = ShadowCastingMode.Off;
            flame.receiveShadows = false;
            flame.lightProbeUsage = LightProbeUsage.Off;
            flame.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void Pose()
        {
            if (flame == null) return;
            flame.enabled = left > 0f;
            flame.transform.localPosition = new Vector3(0f, radius * .05f, -radius * 1.05f);
            flame.transform.localScale = new Vector3(radius * 2.3f, radius * 3f, 1f);
            Color tint = Color.white; tint.a = .9f * Mathf.Clamp01(left / .3f);
            properties.SetColor("_BaseColor", tint); properties.SetColor("_Color", tint);
            properties.SetFloat("_FlameTime", age);
            properties.SetFloat("_FlameSpeed", 2.4f);
            properties.SetFloat("_Sway", .03f);
            properties.SetFloat("_Seed", 2.31f);
            flame.SetPropertyBlock(properties);
        }

        public void Clear()
        {
            left = age = 0f;
            if (flame != null) flame.enabled = false;
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (mesh != null) Dispose(mesh);
            if (material != null) Dispose(material);
        }
        private static void Dispose(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
