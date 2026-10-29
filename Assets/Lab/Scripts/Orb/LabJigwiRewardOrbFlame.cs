using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Local B-reward ring around existing board artwork. It follows the Visual mount's hover/scale,
    /// but never changes the orb state, collider, sprite, ownership or ordinary A/bomb fire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiRewardOrbFlame : MonoBehaviour
    {
        private const int Segments = 48, VertexCount = (Segments + 1) * 2;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap"),
            MainTex = Shader.PropertyToID("_MainTex"), BaseColor = Shader.PropertyToID("_BaseColor"),
            UvSwap = Shader.PropertyToID("_UvSwap");
        private readonly Vector3[] vertices = new Vector3[VertexCount];
        private readonly Vector2[] uv = new Vector2[VertexCount];
        private readonly Color[] colors = new Color[VertexCount];
        private readonly int[] triangles = new int[Segments * 6];
        private Transform source;
        private LabJigwiRewardFireProfile profile, ownedFallbackProfile;
        private Mesh mesh;
        private MeshRenderer ring;
        private MaterialPropertyBlock properties;
        private Material fallbackMaterial, appliedMaterial;
        private Texture appliedTexture;
        private float radius = .32f, age;
        private bool shown, held;

        public bool Shown => shown;
        public bool Held => held;
        public float AnimationTime => age;
        public Mesh Mesh => mesh;
        public LabJigwiRewardFireProfile Profile => profile;

        public static LabJigwiRewardOrbFlame Create(Transform visual)
        {
            if (visual == null) return null;
            var go = new GameObject("Jigwi B Reward Orb Flame");
            go.layer = visual.gameObject.layer;
            go.transform.SetParent(visual, false);
            var effect = go.AddComponent<LabJigwiRewardOrbFlame>();
            effect.source = visual;
            return effect;
        }

        /// <summary>artRadius is local artwork radius before the Visual mount's hover/size scale.</summary>
        public void Show(float artRadius, bool clockHeld = false, LabJigwiRewardFireProfile profile = null) =>
            Show(source != null ? source : transform.parent, artRadius, clockHeld, profile);

        public void Show(Transform visual, float artRadius, bool clockHeld = false,
            LabJigwiRewardFireProfile profile = null)
        {
            if (visual == null || visual == transform || visual.IsChildOf(transform)) { Clear(); return; }
            bool starting = !shown || source != visual;
            source = visual;
            if (this.profile == null || profile != null)
                this.profile = profile != null ? profile
                    : Resources.Load<LabJigwiRewardFireProfile>(LabJigwiRewardFireProfile.ResourcesPath);
            if (this.profile == null)
            {
                if (ownedFallbackProfile == null)
                {
                    ownedFallbackProfile = ScriptableObject.CreateInstance<LabJigwiRewardFireProfile>();
                    ownedFallbackProfile.hideFlags = HideFlags.HideAndDontSave;
                }
                this.profile = ownedFallbackProfile;
            }
            if (transform.parent != source) transform.SetParent(source, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.layer = source.gameObject.layer;
            SetRadius(artRadius); held = clockHeld;
            if (starting) age = 0f;
            EnsureMesh(); ApplyMaterial();
            shown = true; ring.enabled = true;
            if (!enabled) enabled = true;
        }

        public void SetRadius(float artRadius) => radius = float.IsNaN(artRadius) || float.IsInfinity(artRadius)
            ? .32f : Mathf.Clamp(artRadius, .001f, 100f);
        public void SetHeld(bool clockHeld) => held = clockHeld;

        public void Clear()
        {
            shown = held = false; age = 0f;
            if (ring != null) ring.enabled = false;
            if (mesh != null && mesh.vertexCount > 0) mesh.Clear(false);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
            Camera.onPreCull += BeforeCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
            Camera.onPreCull -= BeforeCamera;
            Clear();
        }
        private void OnDestroy()
        {
            Release(mesh); Release(fallbackMaterial); Release(ownedFallbackProfile);
        }
        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void Update()
        {
            if (!shown) return;
            if (source == null) { Clear(); return; }
            if (!held) age += Mathf.Max(0f, Time.deltaTime);
        }
        private void BeforePipelineCamera(ScriptableRenderContext context, Camera camera) => BeforeCamera(camera);
        private void BeforeCamera(Camera camera)
        {
            if (!shown || source == null || profile == null || mesh == null || camera == null || !isActiveAndEnabled
                || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            Vector3 scale = source.lossyScale;
            float worldRadius = radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (!(worldRadius > .000001f) || float.IsInfinity(worldRadius)) { ring.enabled = false; return; }
            Vector3 view = camera.orthographic ? -camera.transform.forward : camera.transform.position - source.position;
            if (view.sqrMagnitude < .000001f) view = -camera.transform.forward;
            // Board sprites sit roughly one radius in front of their Visual mount.
            // Bring only the hollow ring forward; no central geometry masks the original pigment.
            Vector3 centre = source.position + view.normalized * (worldRadius * 1.02f);
            Vector3 right = camera.transform.right, up = camera.transform.up;
            float inner = worldRadius * .98f, outer = worldRadius * profile.RimScale;
            int frame = Mathf.FloorToInt(age * profile.FlameAnimationFps) % 4;
            float tileX = (frame % 2) * .5f, tileY = (1 - frame / 2) * .5f;
            Color color = profile.RimColor;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / Segments), x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                Vector3 direction = right * x + up * y;
                int vertex = i * 2;
                vertices[vertex] = transform.InverseTransformPoint(centre + direction * inner);
                vertices[vertex + 1] = transform.InverseTransformPoint(centre + direction * outer);
                uv[vertex] = new Vector2(tileX + .5f * (.5f + x * inner / (2f * outer)),
                    tileY + .5f * (.5f + y * inner / (2f * outer)));
                uv[vertex + 1] = new Vector2(tileX + .5f * (.5f + x * .5f), tileY + .5f * (.5f + y * .5f));
                colors[vertex] = colors[vertex + 1] = color;
            }
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.SetIndices(triangles, MeshTopology.Triangles, 0, true);
            ring.enabled = true;
        }
        private void EnsureMesh()
        {
            if (mesh != null) { ring.gameObject.layer = gameObject.layer; return; }
            mesh = new Mesh { name = "Jigwi reward board hollow rim", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            for (int i = 0; i < Segments; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            ring = GetComponent<MeshRenderer>();
            if (ring == null) ring = gameObject.AddComponent<MeshRenderer>();
            ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
            ring.lightProbeUsage = LightProbeUsage.Off; ring.reflectionProbeUsage = ReflectionProbeUsage.Off;
            properties = new MaterialPropertyBlock();
        }
        private void ApplyMaterial()
        {
            Material material = profile.Material != null ? profile.Material : FallbackMaterial();
            Texture texture = profile.RimTexture != null ? profile.RimTexture : Texture2D.whiteTexture;
            if (appliedMaterial == material && appliedTexture == texture) return;
            properties.Clear(); properties.SetTexture(BaseMap, texture); properties.SetTexture(MainTex, texture);
            properties.SetColor(BaseColor, Color.white); properties.SetFloat(UvSwap, 0f);
            ring.sharedMaterial = material; ring.SetPropertyBlock(properties);
            appliedMaterial = material; appliedTexture = texture;
        }
        private Material FallbackMaterial()
        {
            if (fallbackMaterial != null) return fallbackMaterial;
            Shader shader = Shader.Find("C6Lab/Element Ink Trail") ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            fallbackMaterial = new Material(shader) { name = "Jigwi reward board fallback", hideFlags = HideFlags.HideAndDontSave };
            return fallbackMaterial;
        }
    }
}
