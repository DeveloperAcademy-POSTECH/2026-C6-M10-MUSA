using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    [Serializable]
    public sealed class LabElementReleaseSettings
    {
        [Min(.25f)] public float lifetime = .86f;
        [Range(.4f, 2f)] public float size = 1f;
        [Range(0, 1f)] public float highlight = .72f;
        [Range(2, 3)] public int ribbons = 2;
        [Range(2, 3)] public int forms = 3;
        [Range(.2f, 2f)] public float gravity = 1f;
        [Range(.4f, 1.6f)] public float escapeSpeed = 1f;
        [Range(8, 20)] public int grainsPerClump = 17;
        [Range(.16f, .42f)] public float earthGatherEnd = .31f;
        [Range(.38f, .72f)] public float earthCrumbleStart = .48f;
        [Range(.06f, .24f)] public float earthEdgeLead = .18f;
    }

    /// <summary>Bounded, seekable mesh particles. Local +Y is the hit normal and one unit is one orb diameter.</summary>
    [DisallowMultipleComponent]
    public sealed partial class LabElementRelease : MonoBehaviour
    {
        private static readonly LabElementReleaseSettings Defaults = new LabElementReleaseSettings();
        private static Material sharedMaterial;
        private static int materialUsers;

        private readonly List<Vector3> positions = new List<Vector3>(1800);
        private readonly List<Color32> colors = new List<Color32>(1800);
        private readonly List<int> triangles = new List<int>(8700);
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private bool usesMaterial;
        private LabElementReleaseSettings settings;
        private LabOrbElement element;
        private Color body, mid, light, dark;
        private Vector3 rise, depth, fall;
        private float scale, width, speed, gloss;
        private int forms;
        private bool lowDetail;

        public void Begin(LabOrbElement kind, Color pigment, float strength, bool monochrome,
            LabElementReleaseSettings settings = null, bool lowDetail = false)
        {
            Clear();
            if (kind == LabOrbElement.None) return;
            EnsureRenderer();
            this.settings = settings ?? Defaults;
            this.lowDetail = lowDetail;
            element = kind;
            strength = Mathf.Clamp01(strength);
            scale = Mathf.Clamp(this.settings.size, .4f, 2f) * Mathf.Lerp(.91f, 1.13f, strength)
                * (lowDetail ? .90f : 1f);
            width = Mathf.Lerp(.87f, 1.20f, strength);
            speed = Mathf.Lerp(1f, 1.13f, strength);
            forms = Mathf.Clamp(this.settings.forms, 2, 3);
            SetPalette(pigment, monochrome);
        }

        public void Sample(float secondsSinceImpact)
        {
            if (element == LabOrbElement.None || !meshRenderer) return;
            float t = secondsSinceImpact / Mathf.Max(.25f, settings.lifetime) * speed;
            if (t < .10f || t >= 1f)
            {
                meshRenderer.enabled = false;
                return;
            }
            Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
            // Wall energy rises along the wall as well as away from it. Gravity for loose sand stays world-down.
            rise = (Vector3.up + Vector3.forward * (3.8f * localUp.z)).normalized;
            depth = Vector3.Cross(Vector3.right, rise);
            fall = new Vector3(-localUp.x, -Vector3.Dot(localUp, rise), -Vector3.Dot(localUp, depth));
            positions.Clear();
            colors.Clear();
            triangles.Clear();
            DrawBands(t);
            switch (element)
            {
                case LabOrbElement.Earth: Earth(t); break;
                case LabOrbElement.Fire: Fire(t); break;
                case LabOrbElement.Water: Water(t); break;
                case LabOrbElement.Wood: Wood(t); break;
                case LabOrbElement.Metal: Metal(t); break;
            }

            meshRenderer.enabled = triangles.Count > 0;
            if (!meshRenderer.enabled) return;
            mesh.Clear();
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (7f * scale));
        }

        public void Clear()
        {
            element = LabOrbElement.None;
            if (meshRenderer) meshRenderer.enabled = false;
        }

        private void EnsureRenderer()
        {
            if (meshRenderer) return;
            mesh = new Mesh { name = "Five Elements / Release", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (!sharedMaterial)
            {
                var shader = Resources.Load<Shader>("FiveElements/LabElementRelease");
                if (!shader) throw new InvalidOperationException("Missing FiveElements/LabElementRelease shader");
                sharedMaterial = new Material(shader) { name = "Element release pigments", hideFlags = HideFlags.DontSave };
            }
            materialUsers++;
            usesMaterial = true;
            meshRenderer.sharedMaterial = sharedMaterial;
            meshRenderer.enabled = false;
        }

        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (usesMaterial && --materialUsers == 0 && sharedMaterial)
            {
                Destroy(sharedMaterial);
                sharedMaterial = null;
            }
        }

        private void SetPalette(Color pigment, bool mono)
        {
            float gray = pigment.grayscale;
            body = mono ? new Color(gray, gray, gray) * .73f : pigment;
            Color.RGBToHSV(body, out float hue, out float saturation, out float value);
            // Shift the chosen pigment towards its illuminated edge. Recolouring an element in the
            // profile recolours the entire form, including the flame core and the edge of a leaf.
            float hueShift = 0f, saturationScale = .84f;
            switch (element)
            {
                case LabOrbElement.Fire: hueShift = .078f; gloss = .12f; break;
                case LabOrbElement.Water: hueShift = -.075f; saturationScale = .76f; gloss = .65f; break;
                case LabOrbElement.Wood: hueShift = -.090f; gloss = .23f; break;
                case LabOrbElement.Metal: saturationScale = .60f; gloss = .92f; break;
                default: saturationScale = .66f; gloss = .03f; break;
            }
            Color accent = Color.HSVToRGB(Mathf.Repeat(hue + hueShift, 1f), saturation * saturationScale,
                Mathf.Lerp(value, 1f, .83f));
            mid = Color.Lerp(body, mono ? Color.white : accent, mono ? .20f : .84f);
            light = Color.Lerp(mid, Color.white, Mathf.Lerp(.18f, .78f, Mathf.Clamp01(settings.highlight)));
            dark = body * .55f;
            if (element == LabOrbElement.Earth)
            {
                // The energy carries loose dry soil; give sand an umber-to-straw range separate from the wet ochre.
                dark = mono ? body * .46f : Color.HSVToRGB(Mathf.Repeat(hue - .024f, 1f), saturation * .80f, value * .39f);
                mid = mono ? Color.Lerp(body, Color.white, .32f)
                    : Color.HSVToRGB(Mathf.Repeat(hue - .016f, 1f), saturation * .64f, Mathf.Lerp(value, 1f, .57f));
            }
        }

        private static float Phase(float t, float a, float b) => Mathf.InverseLerp(a, b, t);
        private static float Ease(float t) => t * t * (3f - 2f * t);
        private static float Open(float t, float a, float b) => Ease(Phase(t, a, b));
        private static float Sin01(float t) => Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
        private static Vector3 P(float x, float y, float z = 0) => new Vector3(x, y, z);
        private static float Hash(int id)
        {
            uint x = (uint)id + 0x9e3779b9u;
            x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
            return (x & 0x00ffffffu) / 16777216f;
        }

        private int Vertex(Vector3 p, Color c)
        {
            int index = positions.Count;
            positions.Add((Vector3.right * (p.x * width) + rise * p.y + depth * p.z) * scale);
            c.a = gloss;
            colors.Add(c);
            return index;
        }

        private void Tri(int a, int b, int c)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        // A folded cross-section supplies a painted ridge and distinguishable faces even on an unlit backdrop.
        private void StripRow(ref int last, Vector3 p, Vector3 across, float ridge, Color left, Color vein, Color right)
        {
            int a = Vertex(p - across, left);
            Vertex(p + P(0, 0, ridge), vein);
            Vertex(p + across, right);
            if (last >= 0)
            {
                Tri(last, a, last + 1); Tri(a, a + 1, last + 1);
                Tri(last + 1, a + 1, last + 2); Tri(a + 1, a + 2, last + 2);
            }
            last = a;
        }

        private void DrawBands(float t)
        {
            int count = Mathf.Clamp(settings.ribbons, 2, 3);
            for (int i = 0; i < count; i++)
            {
                float start = .10f + i * .021f;
                float end = Open(t, start, start + .20f);
                float from = Open(t, start + .19f, start + .43f);
                if (end <= from + .002f) continue;
                float side = (i & 1) == 0 ? 1 : -1;
                float x = i == 0 ? .52f : i == 1 ? .41f : .12f;
                float y = i == 0 ? .94f : i == 1 ? .65f : .75f;
                int row = -1;
                for (int j = 0; j <= 12; j++)
                {
                    float u = Mathf.Lerp(from, end, j / 12f);
                    float twist = Mathf.Sin(u * 2.3f * Mathf.PI + i * .65f) * .065f;
                    Vector3 p = P(side * (.05f + x * u + twist), .15f + u * y, .025f * i + .12f * Sin01(u));
                    float r = (.028f + .024f * Sin01(u)) * (1f - from) * (.20f + .80f * Sin01(j / 12f));
                    StripRow(ref row, p, P(r, -side * r * .35f, .009f), r * .30f, body, light, mid);
                }
            }
        }
    }
}
