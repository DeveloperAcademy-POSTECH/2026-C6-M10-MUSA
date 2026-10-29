using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// A separate B-reward fire envelope, real world-space ribbon and sparks. The original element
    /// artwork stays underneath; this component never adds physics or changes the projectile's element.
    /// </summary>
    public sealed class LabJigwiRewardFireTrail : MonoBehaviour
    {
        private const int SparkCapacity = 128, AuraCards = 4;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap"),
            BaseColor = Shader.PropertyToID("_BaseColor"), UvSwap = Shader.PropertyToID("_UvSwap"),
            MainTex = Shader.PropertyToID("_MainTex");
        private readonly LabFlightTrailPath path = new LabFlightTrailPath();
        private readonly Spark[] sparks = new Spark[SparkCapacity];
        private readonly Surface ribbon = new Surface(LabFlightTrailPath.Capacity * 2);
        private readonly Surface aura = new Surface(AuraCards * 4);
        private readonly Surface embers = new Surface(SparkCapacity * 4);
        private Transform source;
        private LabJigwiRewardFireProfile profile, ownedFallback;
        private Material fallbackMaterial;
        private Texture2D fallbackAtlas, fallbackEmber, fallbackRibbon;
        private Vector3 lastPosition;
        private float radius = .165f, lastTime, beganAt, distanceRemainder, travelledDistance, fadeDeadline;
        private int sparkCount, sparkSerial;
        private bool emitting, fading;

        private struct Spark { public Vector3 Position; public float Born, Phase, Distance; }
        private sealed class Surface
        {
            public readonly Vector3[] Vertices;
            public readonly Vector2[] UV;
            public readonly Color[] Colors;
            public readonly int[] Indices;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public int PreviousCount;
            public Surface(int capacity)
            {
                Vertices = new Vector3[capacity]; UV = new Vector2[capacity]; Colors = new Color[capacity];
                Indices = new int[capacity * 3];
            }
            public void Clear()
            {
                if (Mesh != null && PreviousCount > 0) Mesh.Clear(false);
                PreviousCount = 0;
                if (Renderer != null) Renderer.enabled = false;
            }
        }

        public LabJigwiRewardFireProfile Profile => profile;
        public bool IsEmitting => emitting;
        public int PathCount => path.Count;
        public float PathLength => path.Length;
        public int EmberCount => sparkCount;
        public Vector3 GetPathPosition(int index) => path.GetPosition(index);

        /// <summary>Repeated approval of the same live source preserves its recorded history and animation phase.</summary>
        public void Begin(Transform source, float radius, LabJigwiRewardFireProfile profile = null)
        {
            if (source == null || source == transform || source.IsChildOf(transform)) { Clear(); return; }
            if (emitting && this.source == source)
            {
                if (profile != null && this.profile != profile) { this.profile = profile; ApplyMaterials(); }
                Reconfigure(radius);
                return;
            }
            Clear();
            this.source = source;
            this.profile = profile != null ? profile : Resources.Load<LabJigwiRewardFireProfile>(LabJigwiRewardFireProfile.ResourcesPath);
            if (this.profile == null)
            {
                if (ownedFallback == null)
                {
                    ownedFallback = ScriptableObject.CreateInstance<LabJigwiRewardFireProfile>();
                    ownedFallback.hideFlags = HideFlags.HideAndDontSave;
                }
                this.profile = ownedFallback;
            }
            Reconfigure(radius);
            transform.SetParent(source, false);
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
            gameObject.layer = source.gameObject.layer;
            lastPosition = source.position; lastTime = beganAt = Time.time;
            path.Record(lastPosition, lastTime);
            EnsureSurfaces(); ApplyMaterials();
            emitting = true;
            if (!enabled) enabled = true;
        }

        /// <summary>Changes future width/length without clearing passed positions or restarting animation.</summary>
        public void Reconfigure(float radius)
        {
            this.radius = float.IsNaN(radius) || float.IsInfinity(radius) ? .165f : Mathf.Clamp(radius, .001f, 100f);
        }

        public void StopAndFade(Transform parent)
        {
            if (!emitting || profile == null) return;
            RecordSource(Time.time);
            emitting = false; source = null; fading = true;
            fadeDeadline = Time.time + Mathf.Max(profile.Lifetime, profile.EmberLifetime);
            aura.Clear();
            transform.SetParent(parent, true);
        }

        public void Clear()
        {
            emitting = fading = false; source = null;
            path.Clear(); sparkCount = sparkSerial = 0; distanceRemainder = travelledDistance = 0f;
            ribbon.Clear(); aura.Clear(); embers.Clear();
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
            Release(ribbon.Mesh); Release(aura.Mesh); Release(embers.Mesh);
            Release(fallbackMaterial); Release(ownedFallback);
            Release(fallbackAtlas); Release(fallbackEmber); Release(fallbackRibbon);
        }
        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void LateUpdate()
        {
            if (profile == null) return;
            if (emitting)
            {
                if (source == null) { StopAndFade(transform.parent); }
                else RecordSource(Time.time);
            }
            path.Trim(Time.time, profile.Lifetime, radius * 2f * profile.MaxLengthDiameters);
            PruneSparks(Time.time);
            if (path.Count < 2) ribbon.Clear();
            if (sparkCount == 0) embers.Clear();
            if (fading && Time.time >= fadeDeadline) { fading = false; Destroy(gameObject); }
        }
        private void RecordSource(float now)
        {
            if (source == null || now <= lastTime) return;
            Vector3 position = source.position;
            float distance = Vector3.Distance(lastPosition, position);
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return;
            if (distance < .0001f) { lastTime = now; return; }
            if (path.Count == 0) path.Record(lastPosition, lastTime);
            if (!path.Record(position, now)) return;
            EmitSparks(lastPosition, position, distance, lastTime, now);
            travelledDistance += distance;
            lastPosition = position; lastTime = now;
        }
        private void EmitSparks(Vector3 from, Vector3 to, float distance, float then, float now)
        {
            PruneSparks(now);
            int capacity = profile.MaxParticles;
            if (capacity == 0) { distanceRemainder = 0f; return; }
            float spacing = radius * 2f * profile.EmberSpacingDiameters;
            float total = distanceRemainder + distance;
            int requested = Mathf.Min(100000, Mathf.FloorToInt(total / spacing));
            if (requested <= 0) { distanceRemainder = total; return; }
            int count = Mathf.Min(requested, capacity);
            float offset = spacing - distanceRemainder + (requested - count) * spacing;
            distanceRemainder = Mathf.Repeat(total, spacing);
            for (int i = 0; i < count; i++, offset += spacing)
            {
                float fraction = Mathf.Clamp01(offset / distance), born = Mathf.Lerp(then, now, fraction);
                if (born < now - profile.EmberLifetime) continue;
                if (sparkCount >= capacity)
                {
                    for (int j = 1; j < sparkCount; j++) sparks[j - 1] = sparks[j];
                    sparkCount--;
                }
                // Stable local sequence, independent of gameplay's random stream.
                sparks[sparkCount++] = new Spark { Position = Vector3.Lerp(from, to, fraction), Born = born,
                    Phase = (++sparkSerial * .61803398875f) % 1f, Distance = travelledDistance + distance * fraction };
            }
        }
        private void PruneSparks(float now)
        {
            int kept = 0;
            for (int i = 0; i < sparkCount; i++)
                if (now - sparks[i].Born < profile.EmberLifetime
                    && sparks[i].Distance >= travelledDistance - radius * 2f * profile.MaxLengthDiameters)
                    sparks[kept++] = sparks[i];
            int excess = Mathf.Max(0, kept - profile.MaxParticles);
            for (int i = excess; i < kept; i++) sparks[i - excess] = sparks[i];
            sparkCount = kept - excess;
        }

        private void BeforePipelineCamera(ScriptableRenderContext context, Camera camera) => BeforeCamera(camera);
        private void BeforeCamera(Camera camera)
        {
            if (profile == null || camera == null || !isActiveAndEnabled
                || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            BuildRibbon(camera); BuildAura(camera); BuildEmbers(camera);
        }
        private void BuildRibbon(Camera camera)
        {
            if (path.Count < 2 || path.Length <= .000001f) { ribbon.Clear(); return; }
            float along = 0f; Vector3 previousSide = Vector3.zero;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 point = path.GetPosition(i);
                Vector3 tangent = i == 0 ? path.GetPosition(1) - point : point - path.GetPosition(i - 1);
                Vector3 view = camera.orthographic ? -camera.transform.forward : camera.transform.position - point;
                Vector3 side = Vector3.Cross(tangent, view);
                side = side.sqrMagnitude > .000001f ? side.normalized : camera.transform.right;
                if (previousSide.sqrMagnitude > .01f && Vector3.Dot(previousSide, side) < 0f) side = -side;
                previousSide = side;
                if (i > 0) along += Vector3.Distance(path.GetPosition(i - 1), point);
                float u = along / path.Length;
                float taper = Mathf.Lerp(.025f, 1f, Mathf.Sqrt(u));
                float flicker = .88f + .12f * Mathf.Sin(u * 21f - (Time.time - beganAt) * 19f);
                float width = radius * 2f * profile.WidthScale * taper * flicker;
                int v = i * 2;
                ribbon.Vertices[v] = transform.InverseTransformPoint(point - side * width * .5f);
                ribbon.Vertices[v + 1] = transform.InverseTransformPoint(point + side * width * .5f);
                ribbon.UV[v] = new Vector2(u, 0f); ribbon.UV[v + 1] = new Vector2(u, 1f);
                Color color = profile.FlameColor;
                color.a *= Mathf.Pow(Mathf.Clamp01(1f - (Time.time - path.GetTime(i)) / profile.Lifetime), 1.4f) * .8f;
                ribbon.Colors[v] = ribbon.Colors[v + 1] = color;
                if (i == path.Count - 1) continue;
                int t = i * 6;
                ribbon.Indices[t] = v; ribbon.Indices[t + 1] = v + 2; ribbon.Indices[t + 2] = v + 1;
                ribbon.Indices[t + 3] = v + 1; ribbon.Indices[t + 4] = v + 2; ribbon.Indices[t + 5] = v + 3;
            }
            Commit(ribbon, path.Count * 2, (path.Count - 1) * 6);
        }
        private void BuildAura(Camera camera)
        {
            if (!emitting || source == null) { aura.Clear(); return; }
            Vector3 right = camera.transform.right, up = camera.transform.up;
            Vector3 backward = path.Count > 1 ? path.GetPosition(path.Count - 2) - source.position : up;
            Vector3 planeBack = Vector3.ProjectOnPlane(backward, camera.transform.forward);
            Vector3 fireUp = planeBack.sqrMagnitude > .000001f ? planeBack.normalized : up;
            int frame = Mathf.FloorToInt((Time.time - beganAt) * profile.FlameAnimationFps) % 4;
            for (int i = 0; i < AuraCards; i++)
            {
                float angle = (i + .5f) * Mathf.PI * .5f;
                Vector3 edge = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                Vector3 point = source.position + edge * radius * 1.1f;
                Quad(aura, i, point, Vector3.Cross(fireUp, camera.transform.forward).normalized,
                    fireUp, radius * 1.05f * profile.WidthScale, radius * 1.9f,
                    profile.FlameColor, (frame + i) % 4, true);
            }
            Commit(aura, AuraCards * 4, AuraCards * 6);
        }
        private void BuildEmbers(Camera camera)
        {
            if (sparkCount == 0) { embers.Clear(); return; }
            for (int i = 0; i < sparkCount; i++)
            {
                Spark spark = sparks[i];
                float life = Mathf.Clamp01((Time.time - spark.Born) / profile.EmberLifetime);
                float size = radius * Mathf.Lerp(.2f, .4f, spark.Phase) * (1f - life * .65f);
                // Sparks start exactly on the visited path; only their local decorative lift is animated.
                Vector3 point = spark.Position + Vector3.up * (radius * life * .5f);
                Color color = profile.EmberColor; color.a *= (1f - life) * (1f - life);
                float angle = spark.Phase * Mathf.PI * 2f;
                Vector3 right = camera.transform.right * Mathf.Cos(angle) + camera.transform.up * Mathf.Sin(angle);
                Vector3 up = Vector3.Cross(camera.transform.forward, right);
                Quad(embers, i, point, right, up, size, size * 1.7f, color, 0, false);
            }
            Commit(embers, sparkCount * 4, sparkCount * 6);
        }
        private void Quad(Surface surface, int index, Vector3 centre, Vector3 right, Vector3 up,
            float width, float height, Color color, int frame, bool atlas)
        {
            int v = index * 4, t = index * 6;
            float x = atlas ? frame % 2 * .5f : 0f, y = atlas ? (1 - frame / 2) * .5f : 0f;
            float span = atlas ? .5f : 1f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? 0f : 1f, sy = i < 2 ? 0f : 1f;
                surface.Vertices[v + i] = transform.InverseTransformPoint(centre + right * ((sx - .5f) * width) + up * ((sy - .5f) * height));
                surface.UV[v + i] = new Vector2(x + span * sx, y + span * sy);
                surface.Colors[v + i] = color;
            }
            surface.Indices[t] = v; surface.Indices[t + 1] = v + 2; surface.Indices[t + 2] = v + 1;
            surface.Indices[t + 3] = v + 1; surface.Indices[t + 4] = v + 2; surface.Indices[t + 5] = v + 3;
        }
        private static void Commit(Surface surface, int vertices, int indices)
        {
            if (surface.Mesh == null || vertices == 0) { surface.Clear(); return; }
            if (vertices < surface.PreviousCount) surface.Mesh.Clear(false);
            surface.Mesh.SetVertices(surface.Vertices, 0, vertices);
            surface.Mesh.SetUVs(0, surface.UV, 0, vertices);
            surface.Mesh.SetColors(surface.Colors, 0, vertices);
            surface.Mesh.SetIndices(surface.Indices, 0, indices, MeshTopology.Triangles, 0, true);
            surface.PreviousCount = vertices; surface.Renderer.enabled = true;
        }
        private void EnsureSurfaces()
        {
            Ensure(ribbon, "Reward fire real path"); Ensure(aura, "Reward fire envelope"); Ensure(embers, "Reward fire sparks");
        }
        private void Ensure(Surface surface, string title)
        {
            if (surface.Renderer != null) { surface.Renderer.gameObject.layer = gameObject.layer; return; }
            var go = new GameObject(title); go.transform.SetParent(transform, false); go.layer = gameObject.layer;
            surface.Mesh = new Mesh { name = title, hideFlags = HideFlags.DontSave }; surface.Mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = surface.Mesh;
            surface.Renderer = go.AddComponent<MeshRenderer>(); surface.Renderer.enabled = false;
            surface.Renderer.shadowCastingMode = ShadowCastingMode.Off; surface.Renderer.receiveShadows = false;
            surface.Renderer.lightProbeUsage = LightProbeUsage.Off; surface.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        private void ApplyMaterials()
        {
            if (profile == null) return;
            Material material = profile.Material != null ? profile.Material : FallbackMaterial();
            SetMaterial(ribbon, material, profile.RibbonTexture != null ? profile.RibbonTexture : FallbackTexture(0), true);
            SetMaterial(aura, material, profile.FlameTexture != null ? profile.FlameTexture : FallbackTexture(1), false);
            SetMaterial(embers, material, profile.EmberTexture != null ? profile.EmberTexture : FallbackTexture(2), false);
        }
        private static void SetMaterial(Surface surface, Material material, Texture texture, bool swap)
        {
            if (surface.Renderer == null) return;
            var block = new MaterialPropertyBlock(); block.SetTexture(BaseMap, texture); block.SetTexture(MainTex, texture);
            block.SetColor(BaseColor, Color.white); block.SetFloat(UvSwap, swap ? 1f : 0f);
            surface.Renderer.sharedMaterial = material; surface.Renderer.SetPropertyBlock(block);
        }
        private Material FallbackMaterial()
        {
            if (fallbackMaterial != null) return fallbackMaterial;
            Shader shader = Shader.Find("C6Lab/Element Ink Trail") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            fallbackMaterial = new Material(shader) { name = "Reward fire fallback", hideFlags = HideFlags.HideAndDontSave };
            return fallbackMaterial;
        }
        private Texture2D FallbackTexture(int kind)
        {
            Texture2D old = kind == 0 ? fallbackRibbon : kind == 1 ? fallbackAtlas : fallbackEmber;
            if (old != null) return old;
            const int size = 64; var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = kind == 1 ? (x % 32) / 31f : x / 63f;
                float v = kind == 1 ? (y % 32) / 31f : y / 63f;
                int frame = kind == 1 ? (1 - y / 32) * 2 + x / 32 : 0;
                float centre = .5f + .1f * Mathf.Sin(v * 8f + frame) * v;
                float width = kind == 2 ? .45f * Mathf.Sin(Mathf.PI * v)
                    : kind == 0 ? .4f * Mathf.Pow(Mathf.Max(.001f, v), .45f)
                    : .44f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * v)), .7f) * (1f - .25f * v);
                float distance = Mathf.Abs(u - centre) / Mathf.Max(.001f, width);
                Color color = distance > .88f ? new Color(.2f, .055f, .025f)
                    : Color.Lerp(new Color(1f, .94f, .5f), new Color(1f, .22f, .035f), Mathf.Clamp01(distance * 1.7f));
                color.a = Mathf.Clamp01((1f - distance) * 12f);
                pixels[y * size + x] = color;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "Reward fire fallback " + kind, hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            if (kind == 0) fallbackRibbon = texture; else if (kind == 1) fallbackAtlas = texture; else fallbackEmber = texture;
            return texture;
        }
    }
}
