using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Board-only paint drops. Samples real passed points, then lets each footprint fall and fade
    /// independently. No flight profile, particle physics, ownership or gameplay random state is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbBoardTrail : MonoBehaviour
    {
        private const int Capacity = 96, MaxEmissionsPerStep = 24;
        [SerializeField] private LabOrbDripProfile profile;
        private readonly Drop[] drops = new Drop[Capacity];
        private readonly Vector3[] vertices = new Vector3[Capacity * 4];
        private readonly Vector2[] uv = new Vector2[Capacity * 4];
        private readonly Color[] colors = new Color[Capacity * 4];
        private readonly int[] triangles = new int[Capacity * 6];
        private LabOrbView view;
        private string orbId;
        private LabOrbElement element;
        private Mesh mesh;
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private Material appliedMaterial;
        private Texture appliedTexture;
        private Vector3 lastPosition;
        private Color pigment;
        private float radius = .32f, intensity, remainder;
        private int count, drawnCount;
        private uint randomState = 1;
        private bool on, held, hasLast;

        private struct Drop
        {
            public Vector3 Origin, Position;
            public float Age, Lifetime, Diameter, FallDistance, Opacity;
        }

        public LabOrbDripProfile Profile => profile != null ? profile
            : profile = Resources.Load<LabOrbDripProfile>(LabOrbDripProfile.ResourcesPath);
        public bool On => on;
        public LabOrbElement Element => element;
        public Color Pigment => pigment;
        public float Intensity => intensity;
        public float Strength => Profile != null ? Profile.Strength : 0f;
        public float DropDiameter => Profile != null ? radius * 2f * Profile.DiameterScale : 0f;
        public int DropCount => count;
        public Vector3 LastSourcePosition => lastPosition;
        public Mesh Mesh => mesh;
        public Vector3 GetDropPosition(int index) => At(index).Position;
        public float GetDropDiameter(int index) => At(index).Diameter;
        public float GetDropOpacity(int index)
        {
            Drop drop = At(index);
            return drop.Opacity * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, drop.Age / drop.Lifetime)));
        }
        private Drop At(int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return drops[index];
        }

        /// <summary>artRadius already includes the board's small/big visual target exactly once.</summary>
        public void Sync(LabOrbView orb, bool enabledByDev, float artRadius, bool clockHeld = false)
        {
            LabOrbDripProfile look = Profile;
            bool wanted = enabledByDev && orb != null && orb.gameObject.activeInHierarchy
                && orb.Kind == LabOrbKind.Combined && orb.Element != LabOrbElement.None
                && look != null && look.DropTexture != null && look.Material != null;
            if (!wanted) { Clear(); return; }
            if (!on || view != orb || orbId != orb.OrbId || element != orb.Element)
            {
                Clear();
                view = orb; orbId = orb.OrbId; element = orb.Element;
                pigment = LabPaintPalette.ColorFor(element);
                randomState = StableSeed(orbId);
            }
            radius = float.IsNaN(artRadius) || float.IsInfinity(artRadius) ? .32f : Mathf.Max(.001f, artRadius);
            held = clockHeld; on = true;
            EnsureSurface(); ApplyMaterial();
        }

        public float IntensityFor(float radiiPerSecond)
        {
            LabOrbDripProfile look = Profile;
            if (look == null || float.IsNaN(radiiPerSecond)) return 0f;
            float u = Mathf.InverseLerp(look.FadeInSpeed, look.FullSpeed, radiiPerSecond);
            return look.Strength * u * u * (3f - 2f * u);
        }

        /// <summary>Scaled local presentation time only. Pending/DEV reset never bridges a hidden position.</summary>
        public void Step(float deltaTime)
        {
            if (!on || view == null || Profile == null) return;
            Vector3 position = view.transform.position;
            if (!Finite(position)) { Clear(); return; }
            if (held || !(deltaTime > 0f) || float.IsInfinity(deltaTime))
            {
                lastPosition = position; hasLast = true; intensity = 0f;
                BuildMesh(); return;
            }
            AdvanceDrops(deltaTime);
            if (!hasLast)
            {
                lastPosition = position; hasLast = true; intensity = 0f;
                BuildMesh(); return;
            }
            Vector3 from = lastPosition, moved = position - from;
            moved.z = 0f; lastPosition = position;
            float distance = moved.magnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            {
                remainder = 0f; intensity = 0f; BuildMesh(); return;
            }
            intensity = IntensityFor(distance / deltaTime / radius);
            if (distance > .00001f && intensity > .001f)
                EmitDrops(from, position, distance, deltaTime);
            else remainder = 0f;
            BuildMesh();
        }
        private void LateUpdate() => Step(Time.deltaTime);

        private void AdvanceDrops(float deltaTime)
        {
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                Drop drop = drops[i]; drop.Age += deltaTime;
                if (drop.Age >= drop.Lifetime) continue;
                Pose(ref drop); drops[kept++] = drop;
            }
            count = kept;
            int excess = Mathf.Max(0, count - Profile.MaxDrops);
            if (excess > 0)
            {
                Array.Copy(drops, excess, drops, 0, count - excess); count -= excess;
            }
        }
        private void EmitDrops(Vector3 from, Vector3 to, float distance, float deltaTime)
        {
            float spacing = radius * Profile.SpacingRadii;
            double total = remainder + (double)distance;
            double requested = Math.Floor(total / spacing);
            if (requested < 1d) { remainder = (float)total; return; }
            int emit = (int)Math.Min(requested, Math.Min(MaxEmissionsPerStep, Profile.MaxDrops));
            // Keep the newest real passed points on a long frame, without allocating a huge history.
            double offset = spacing - remainder + (requested - emit) * spacing;
            remainder = (float)(total % spacing);
            for (int i = 0; i < emit; i++, offset += spacing)
            {
                float t = Mathf.Clamp01((float)(offset / distance));
                float age = deltaTime * (1f - t);
                if (age >= Profile.Lifetime) continue;
                if (count >= Profile.MaxDrops)
                {
                    Array.Copy(drops, 1, drops, 0, count - 1); count--;
                }
                var drop = new Drop
                {
                    Origin = Vector3.Lerp(from, to, t) + Vector3.back * .025f,
                    Age = age, Lifetime = Profile.Lifetime,
                    Diameter = DropDiameter * Mathf.Lerp(.85f, 1.15f, Random01()),
                    FallDistance = radius * Profile.FallDistanceRadii * Mathf.Lerp(.85f, 1.15f, Random01()),
                    Opacity = intensity
                };
                Pose(ref drop); drops[count++] = drop;
            }
        }
        private static void Pose(ref Drop drop)
        {
            float t = Mathf.Clamp01(drop.Age / drop.Lifetime);
            drop.Position = drop.Origin + Vector3.down * (drop.FallDistance * t * t);
        }

        private void EnsureSurface()
        {
            if (surface != null) { surface.gameObject.layer = gameObject.layer; return; }
            var go = new GameObject("Board Paint Drops");
            go.transform.SetParent(transform, false); go.layer = gameObject.layer;
            mesh = new Mesh { name = "Board paint-drop quads", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface = go.AddComponent<MeshRenderer>(); surface.enabled = false;
            surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            surface.lightProbeUsage = LightProbeUsage.Off; surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right;
                uv[v + 2] = Vector2.up; uv[v + 3] = Vector2.one;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
        }
        private void ApplyMaterial()
        {
            if (surface == null || Profile == null) return;
            if (appliedMaterial == Profile.Material && appliedTexture == Profile.DropTexture) return;
            properties.Clear(); properties.SetTexture("_BaseMap", Profile.DropTexture);
            properties.SetTexture("_MainTex", Profile.DropTexture);
            properties.SetColor("_BaseColor", Color.white); properties.SetFloat("_UvSwap", 0f);
            surface.sharedMaterial = Profile.Material; surface.SetPropertyBlock(properties);
            appliedMaterial = Profile.Material; appliedTexture = Profile.DropTexture;
        }
        private void BuildMesh()
        {
            if (mesh == null || surface == null) return;
            if (count == 0)
            {
                if (drawnCount > 0) mesh.Clear(false);
                drawnCount = 0; surface.enabled = false; return;
            }
            Transform mount = surface.transform;
            for (int i = 0; i < count; i++)
            {
                Drop drop = drops[i];
                float phase = drop.Age / drop.Lifetime;
                float stretch = 1f + Mathf.Sin(phase * Mathf.PI) * .35f;
                Vector3 right = Vector3.right * (drop.Diameter * .5f / Mathf.Sqrt(stretch));
                Vector3 up = Vector3.up * (drop.Diameter * .75f * stretch);
                int v = i * 4;
                vertices[v] = mount.InverseTransformPoint(drop.Position - right - up);
                vertices[v + 1] = mount.InverseTransformPoint(drop.Position + right - up);
                vertices[v + 2] = mount.InverseTransformPoint(drop.Position - right + up);
                vertices[v + 3] = mount.InverseTransformPoint(drop.Position + right + up);
                Color color = pigment; color.a *= GetDropOpacity(i);
                for (int j = 0; j < 4; j++) colors[v + j] = color;
            }
            if (count < drawnCount) mesh.Clear(false);
            mesh.SetVertices(vertices, 0, count * 4); mesh.SetUVs(0, uv, 0, count * 4);
            mesh.SetColors(colors, 0, count * 4);
            mesh.SetIndices(triangles, 0, count * 6, MeshTopology.Triangles, 0, true);
            drawnCount = count; surface.enabled = true;
        }

        public void Clear()
        {
            on = held = hasLast = false; view = null; orbId = null;
            count = drawnCount = 0; intensity = remainder = 0f;
            if (mesh != null && mesh.vertexCount > 0) mesh.Clear(false);
            if (surface != null) surface.enabled = false;
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
        private float Random01()
        {
            randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
            return (randomState & 0xFFFFFF) / 16777216f;
        }
        private static uint StableSeed(string id)
        {
            uint seed = 2166136261;
            foreach (char value in id ?? string.Empty) seed = unchecked((seed ^ value) * 16777619);
            return seed == 0 ? 1u : seed;
        }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
