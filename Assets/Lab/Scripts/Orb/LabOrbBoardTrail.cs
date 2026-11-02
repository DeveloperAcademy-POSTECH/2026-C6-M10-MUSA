using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>Bounded board-only ink stamps, born on actual passed points. They briefly fall,
    /// land at a fixed point, reveal authored wash, and fade. Never alters orb physics or gameplay RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbBoardTrail : MonoBehaviour
    {
        private const int Capacity = 96, MaxEmissionsPerStep = 24, QuadCapacity = Capacity * 2;
        private static readonly string[] TextureNames = { "_StainA", "_StainB", "_StainC", "_SpatterA", "_SpatterB" };
        [SerializeField] private LabOrbDripProfile profile;
        private readonly Drop[] drops = new Drop[Capacity];
        private readonly Vector3[] vertices = new Vector3[QuadCapacity * 4];
        private readonly Vector2[] uv = new Vector2[QuadCapacity * 4];
        private readonly Vector4[] stamps = new Vector4[QuadCapacity * 4];
        private readonly Color[] colors = new Color[QuadCapacity * 4];
        private readonly int[] triangles = new int[QuadCapacity * 6];
        private readonly Texture[] appliedTextures = new Texture[5];
        private LabOrbView view;
        private LabOrbBoard board;
        private string orbId;
        private LabOrbElement element;
        private Mesh mesh;
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private Material appliedMaterial;
        private Vector3 lastPosition;
        private Color pigment;
        private float radius = .32f, intensity, remainder;
        private int count, drawnQuads;
        private uint randomState = 1;
        private bool on, held, hasLast;

        private struct Drop
        {
            public Vector3 Origin, Landing, Position;
            public float Age, Lifetime, Fall, Spread, Hold, Fade, Diameter, Opacity, Rotation, Seed;
            public int Variant, Spatter;
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
        public float GetDropAge(int index) => At(index).Age;
        public int GetDropVariant(int index) => At(index).Variant;
        public bool GetDropLanded(int index) { Drop drop = At(index); return drop.Age >= drop.Fall; }
        public float GetDropReveal(int index) { Drop drop = At(index); return Mathf.Clamp01((drop.Age - drop.Fall) / drop.Spread); }
        public float GetDropOpacity(int index)
        {
            Drop drop = At(index);
            return drop.Opacity * (1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((drop.Age - drop.Fall - drop.Spread - drop.Hold) / drop.Fade)));
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
                && look != null && look.HasArtwork;
            if (!wanted) { Clear(); return; }
            if (!on || view != orb || orbId != orb.OrbId || element != orb.Element)
            {
                Clear(); view = orb; orbId = orb.OrbId; element = orb.Element;
                board = orb.GetComponentInParent<LabOrbBoard>();
                pigment = LabPaintPalette.ColorFor(element); randomState = StableSeed(orbId);
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
        /// <summary>Local presentation time only. A held/hidden source never bridges a teleport.</summary>
        public void Step(float deltaTime)
        {
            if (!on || view == null) return;
            if (Profile == null || !Profile.HasArtwork) { Clear(); return; }
            Vector3 position = view.transform.position;
            if (!Finite(position)) { Clear(); return; }
            if (held || !(deltaTime > 0f) || float.IsInfinity(deltaTime))
            {
                lastPosition = position; hasLast = true; intensity = 0f; BuildMesh(); return;
            }
            AdvanceDrops(deltaTime);
            if (!hasLast)
            {
                lastPosition = position; hasLast = true; intensity = 0f; BuildMesh(); return;
            }
            Vector3 from = lastPosition, moved = position - from;
            moved.z = 0f; lastPosition = position;
            float distance = moved.magnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            { remainder = 0f; intensity = 0f; BuildMesh(); return; }
            intensity = IntensityFor(distance / deltaTime / radius);
            if (distance > .00001f && intensity > .001f) EmitDrops(from, position, distance, deltaTime);
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
            if (excess > 0) { Array.Copy(drops, excess, drops, 0, count - excess); count -= excess; }
        }
        private void EmitDrops(Vector3 from, Vector3 to, float distance, float deltaTime)
        {
            float spacing = radius * Profile.SpacingRadii;
            double total = remainder + (double)distance, requested = Math.Floor(total / spacing);
            if (requested < 1d) { remainder = (float)total; return; }
            int emit = (int)Math.Min(requested, Math.Min(MaxEmissionsPerStep, Profile.MaxDrops));
            double offset = spacing - remainder + (requested - emit) * spacing;
            remainder = (float)(total % spacing);
            for (int i = 0; i < emit; i++, offset += spacing)
            {
                float t = Mathf.Clamp01((float)(offset / distance)), age = deltaTime * (1f - t);
                if (age >= Profile.Lifetime) continue;
                if (count >= Profile.MaxDrops) { Array.Copy(drops, 1, drops, 0, count - 1); count--; }
                var drop = new Drop
                {
                    Origin = Vector3.Lerp(from, to, t) + Vector3.back * .025f,
                    Age = age, Lifetime = Profile.Lifetime, Fall = Profile.FallDuration,
                    Spread = Profile.SpreadDuration, Hold = Profile.HoldDuration, Fade = Profile.FadeDuration,
                    Diameter = DropDiameter * Mathf.Lerp(.85f, 1.15f, Random01()),
                    Opacity = Strength * Mathf.Lerp(Profile.MinimumOpacity, 1f, intensity / Mathf.Max(.0001f, Strength)),
                    Variant = Mathf.Min(2, (int)(Random01() * 3f)), Rotation = Random01() * Mathf.PI * 2f,
                    Seed = Random01(), Spatter = Random01() < Profile.SpatterChance ? (Random01() < .5f ? 0 : 1) : -1
                };
                drop.Landing = drop.Origin + Vector3.down * (radius * Profile.FallDistanceRadii * Mathf.Lerp(.85f, 1.15f, Random01()));
                // Gameplay side planes are intentionally open. Only the decorative ink is inset.
                Vector3 origin = drop.Origin, landing = drop.Landing;
                bool fits = false;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float footprint = drop.Diameter * .72f;
                    if (TryConfine(origin, footprint, out drop.Origin)
                        && TryConfine(landing, footprint, out drop.Landing)) { fits = true; break; }
                    // Very large art settings can exceed a narrow curved band. Reduce only
                    // this decorative stamp, never the source collider or gameplay radius.
                    drop.Diameter *= .5f;
                }
                if (!fits) continue;
                Pose(ref drop); drops[count++] = drop;
            }
        }
        private bool TryConfine(Vector3 point, float footprint, out Vector3 confined)
        {
            confined = point;
            LabOrbAreaShape shape = board != null ? board.AreaShape : null;
            if (shape == null) return true;
            Rect bounds = shape.CenterBounds;
            if (footprint * 2f >= bounds.width) return false;
            float x = Mathf.Clamp(point.x, bounds.xMin + footprint, bounds.xMax - footprint);
            Vector2 range = shape.VerticalRange(x, footprint);
            if (range.x > range.y) return false;
            // Vertical projection preserves the decorative side inset. The gameplay
            // Clamp normal projection intentionally keeps ownership openings open.
            confined = new Vector3(x, Mathf.Clamp(point.y, range.x, range.y), point.z);
            return true;
        }
        private static void Pose(ref Drop drop)
        {
            float t = Mathf.Clamp01(drop.Age / drop.Fall);
            drop.Position = Vector3.Lerp(drop.Origin, drop.Landing, t * t);
        }
        private void EnsureSurface()
        {
            if (surface != null) { surface.gameObject.layer = gameObject.layer; return; }
            var go = new GameObject("Board Paint Stains");
            go.transform.SetParent(transform, false); go.layer = gameObject.layer;
            mesh = new Mesh { name = "Bounded board paint stamps", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic(); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface = go.AddComponent<MeshRenderer>(); surface.enabled = false;
            surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            surface.lightProbeUsage = LightProbeUsage.Off; surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < QuadCapacity; i++)
            {
                int v = i * 4, t = i * 6;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.up; uv[v + 3] = Vector2.one;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
        }
        private Texture Artwork(int index) => index < 3 ? Profile.StainTexture(index) : Profile.SpatterTexture(index - 3);
        private void ApplyMaterial()
        {
            bool changed = appliedMaterial != Profile.Material;
            for (int i = 0; i < 5; i++) changed |= appliedTextures[i] != Artwork(i);
            if (!changed) return;
            properties.Clear();
            for (int i = 0; i < 5; i++)
            { appliedTextures[i] = Artwork(i); properties.SetTexture(TextureNames[i], appliedTextures[i]); }
            surface.sharedMaterial = Profile.Material; surface.SetPropertyBlock(properties); appliedMaterial = Profile.Material;
        }
        private void BuildMesh()
        {
            if (mesh == null || surface == null) return;
            int quads = 0;
            for (int i = 0; i < count; i++)
            {
                Drop drop = drops[i];
                if (drop.Age < drop.Fall)
                    WriteQuad(quads++, drop.Position, drop.Diameter * .28f, 1f, 0f, -1, 1f, drop.Seed, GetDropOpacity(i));
                else
                {
                    float reveal = GetDropReveal(i);
                    Texture2D art = Profile.StainTexture(drop.Variant);
                    WriteQuad(quads++, drop.Position, drop.Diameter, (float)art.width / art.height,
                        drop.Rotation, drop.Variant, reveal, drop.Seed, GetDropOpacity(i));
                    if (drop.Spatter >= 0)
                    {
                        Vector3 offset = new Vector3(Mathf.Cos(drop.Rotation), Mathf.Sin(drop.Rotation), 0f) * (drop.Diameter * .1f);
                        WriteQuad(quads++, drop.Position + offset, drop.Diameter * .8f, 1f,
                            -drop.Rotation, 3 + drop.Spatter, reveal, drop.Seed, GetDropOpacity(i) * .8f);
                    }
                }
            }
            if (quads < drawnQuads) mesh.Clear(false);
            if (quads == 0) { drawnQuads = 0; surface.enabled = false; return; }
            mesh.SetVertices(vertices, 0, quads * 4); mesh.SetUVs(0, uv, 0, quads * 4);
            mesh.SetUVs(1, stamps, 0, quads * 4); mesh.SetColors(colors, 0, quads * 4);
            mesh.SetIndices(triangles, 0, quads * 6, MeshTopology.Triangles, 0, true);
            drawnQuads = quads; surface.enabled = true;
        }
        private void WriteQuad(int quad, Vector3 position, float diameter, float aspect, float rotation,
            int variant, float reveal, float seed, float opacity)
        {
            float halfWidth = diameter * .5f * Mathf.Min(1f, aspect);
            float halfHeight = diameter * .5f / Mathf.Max(1f, aspect);
            Vector3 right = new Vector3(Mathf.Cos(rotation), Mathf.Sin(rotation), 0f) * halfWidth;
            Vector3 up = new Vector3(-Mathf.Sin(rotation), Mathf.Cos(rotation), 0f) * halfHeight;
            Transform mount = surface.transform;
            int v = quad * 4;
            vertices[v] = mount.InverseTransformPoint(position - right - up);
            vertices[v + 1] = mount.InverseTransformPoint(position + right - up);
            vertices[v + 2] = mount.InverseTransformPoint(position - right + up);
            vertices[v + 3] = mount.InverseTransformPoint(position + right + up);
            Color color = pigment; color.a *= opacity;
            for (int j = 0; j < 4; j++) { colors[v + j] = color; stamps[v + j] = new Vector4(variant, reveal, seed, 0f); }
        }
        public void Clear()
        {
            on = held = hasLast = false; view = null; board = null; orbId = null;
            count = drawnQuads = 0; intensity = remainder = 0f;
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
