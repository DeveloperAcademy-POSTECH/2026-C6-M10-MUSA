using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    [Serializable]
    public sealed class LabWatercolorPaintSettings
    {
        public float density = .62f, broadVariation = .55f, mottle = .38f, grain = .35f;
        public float grainScale = 1f, edgeStrength = .6f, edgeWidth = .022f, textureScale = 1f;

        internal void Validate()
        {
            Check(density, .05f, 1f); Check(broadVariation, 0f, 1f);
            Check(mottle, 0f, 1f); Check(grain, 0f, 1f);
            Check(grainScale, .25f, 4f); Check(edgeStrength, 0f, 1f);
            Check(edgeWidth, .002f, .12f); Check(textureScale, .25f, 4f);
        }

        private static void Check(float value, float min, float max)
        {
            if (!float.IsFinite(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(nameof(value), "Watercolor settings must be finite and within their slider range.");
        }
    }

    /// <summary>
    /// Phase-one, Editor-only appearance study. Both images use the production surface,
    /// retained descriptors, committed high-resolution support and saved opaque gourd.
    /// Nothing is installed into a live LabNetwork, model, Scene, material or configuration.
    /// </summary>
    public sealed class LabWatercolorPaintStudy : IDisposable
    {
        private const int Columns = 1024;
        private readonly LabHighResolutionPaintRuntime baseline = new LabHighResolutionPaintRuntime();
        private LabPaintStudyRenderer renderer;
        private Material material;
        private Texture2D brush, distance, colors, owners;
        private RenderTexture optics, candidate;
        private int cachedRepeats = -1;
        private uint cachedSeed;
        private Vector3 cachedDirection;
        private bool disposed;

        public LabPaintSurface Surface { get; private set; }
        public Vector3 HitDirection { get; set; } = Vector3.forward;
        public Texture CurrentMap => baseline.PaintTexture;
        public Texture CandidateMap => candidate;

        public LabWatercolorPaintStudy()
        {
            try
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                    "Assets/Editor/WatercolorPaint/LabWatercolorPaint.shader");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || shader == null || !shader.isSupported)
                    throw new InvalidOperationException("Watercolor comparison requires the supported shader and a real graphics device.");
                brush = Resources.Load<Texture2D>("GourdPaint/Patterns/Splatter");
                if (brush == null || !brush.isReadable)
                    throw new InvalidOperationException("The saved readable Splatter brush is missing.");
                material = new Material(shader) { name = "Watercolor study material", hideFlags = HideFlags.HideAndDontSave };
                distance = CreateInteriorDistance(brush);
                optics = Allocate("Watercolor study optical history");
                candidate = Allocate("Watercolor study candidate");
                renderer = new LabPaintStudyRenderer(true);
            }
            catch { Dispose(); throw; }
        }

        /// <summary>The caller owns the two returned images: current runtime, then candidate.</summary>
        public Texture2D[] Render(int repeats, uint seed, float yaw, float pitch, float zoom,
            LabWatercolorPaintSettings settings)
        {
            Prepare(repeats, seed, settings);
            var frames = new Texture2D[2];
            try
            {
                frames[0] = renderer.RenderVisual(CurrentMap, yaw, zoom, pitch);
                frames[1] = renderer.RenderVisual(CandidateMap, yaw, zoom, pitch);
                return frames;
            }
            catch
            {
                foreach (Texture2D frame in frames) if (frame != null) Object.DestroyImmediate(frame);
                throw;
            }
        }

        /// <summary>Rebuild from descriptors, never accumulate against a previous rendered frame.</summary>
        public void Prepare(int repeats, uint seed, LabWatercolorPaintSettings settings)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabWatercolorPaintStudy));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            if (repeats < 0 || repeats > 3) throw new ArgumentOutOfRangeException(nameof(repeats));
            Vector3 d = HitDirection;
            if (!float.IsFinite(d.x) || !float.IsFinite(d.y) || !float.IsFinite(d.z)
                || !float.IsFinite(d.sqrMagnitude) || d.sqrMagnitude < .00001f)
                throw new ArgumentException("A finite, nonzero hit direction is required.");
            d.Normalize();
            if (Surface == null || cachedRepeats != repeats || cachedSeed != seed || cachedDirection != d)
                BuildSurface(repeats, seed, d);
            if (!baseline.Update(Surface, colors, owners, Columns, true))
                throw new InvalidOperationException(baseline.Error ?? "The current production paint maps are not ready.");

            var commands = new CommandBuffer { name = "Offline watercolor optical replay" };
            try
            {
                commands.SetRenderTarget(optics);
                commands.ClearRenderTarget(false, true, Color.clear);
                foreach (LabPaintVisualStamp stamp in Surface.CopyVisualStamps())
                {
                    var block = new MaterialPropertyBlock();
                    block.SetTexture("_Brush", brush); block.SetTexture("_InteriorDistance", distance);
                    block.SetVector("_Center", stamp.center); block.SetVector("_Right", stamp.right);
                    block.SetVector("_Up", stamp.Up); block.SetFloat("_Radius", stamp.radius);
                    block.SetVector("_Seed", SeedVector(stamp.seed, stamp.sequence));
                    block.SetFloat("_TextureScale", settings.textureScale);
                    block.SetFloat("_EdgeWidth", settings.edgeWidth);
                    commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, block);
                }
                var composite = new MaterialPropertyBlock();
                composite.SetTexture("_CurrentPaint", baseline.PaintTexture);
                composite.SetTexture("_OpticalHistory", optics);
                composite.SetFloat("_Density", settings.density);
                composite.SetFloat("_BroadVariation", settings.broadVariation);
                composite.SetFloat("_Mottle", settings.mottle);
                composite.SetFloat("_Grain", settings.grain);
                composite.SetFloat("_GrainScale", settings.grainScale);
                composite.SetFloat("_EdgeStrength", settings.edgeStrength);
                composite.SetFloat("_TextureScale", settings.textureScale);
                commands.SetRenderTarget(candidate);
                commands.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3, 1, composite);
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { commands.Release(); }
        }

        private void BuildSurface(int repeats, uint seed, Vector3 d)
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            if (source == null) throw new InvalidOperationException("The saved LabConfig is missing.");
            LabPaintGrowthSettings rules = source.PaintGrowthSettings;
            if (rules.Pattern != LabPaintPattern.Splatter)
                throw new InvalidOperationException("This first watercolor study requires the saved Splatter growth pattern.");
            rules.DryingEnabled = false; // Isolated still-frame comparison, never sourceConfig.
            var next = new LabPaintSurface(rules, seed);
            Color blue = LabPaintPalette.ColorFor(LabOrbElement.Water);
            next.RegisterHit(d, blue, 1, 1);
            for (int i = 0; i < repeats; i++) { next.RegisterHit(d, blue, 1, 1); next.Advance(1d); }
            Release(ref colors); Release(ref owners);
            colors = Upload(next, false); owners = Upload(next, true);
            Surface = next; cachedRepeats = repeats; cachedSeed = seed; cachedDirection = d;
        }

        private static Vector4 SeedVector(uint seed, uint sequence)
        {
            uint value = seed ^ unchecked(sequence * 0x9E3779B9u);
            return new Vector4((value & 1023) * .017f, ((value >> 10) & 1023) * .019f,
                ((value >> 20) & 1023) * .023f, 0f);
        }

        private static Texture2D Upload(LabPaintSurface surface, bool painter)
        {
            var texture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Watercolor study authority copy", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            var pixels = new Color32[surface.CellCount];
            if (painter)
            {
                var slots = new byte[surface.CellCount]; surface.CopyPainterSlots(slots);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(slots[i], 0, 0, 255);
            }
            else surface.CopyPixels(pixels);
            texture.SetPixels32(pixels); texture.Apply(false, false); return texture;
        }

        // A private chamfer-distance copy of the original silhouette, not a new gameplay mask.
        // R encodes interior distance in brush UV units times eight; bilinear sampling softens
        // the pixel grid. Only the optical rim uses this texture; support stays production-owned.
        private static Texture2D CreateInteriorDistance(Texture2D source)
        {
            int w = source.width, h = source.height;
            Color32[] pixels = source.GetPixels32();
            var distances = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                float ink = (.2126f * p.r + .7152f * p.g + .0722f * p.b) * p.a / (255f * 255f);
                int x = i % w, y = i / w;
                distances[i] = ink >= .08f && x > 0 && y > 0 && x < w - 1 && y < h - 1 ? w + h : 0f;
            }
            for (int y = 1; y < h; y++) for (int x = 1; x < w; x++)
            {
                int i = y * w + x;
                distances[i] = Mathf.Min(distances[i], Mathf.Min(distances[i - 1] + 1f,
                    Mathf.Min(distances[i - w] + 1f, distances[i - w - 1] + 1.41421356f)));
                if (x + 1 < w) distances[i] = Mathf.Min(distances[i], distances[i - w + 1] + 1.41421356f);
            }
            for (int y = h - 2; y >= 0; y--) for (int x = w - 2; x >= 0; x--)
            {
                int i = y * w + x;
                distances[i] = Mathf.Min(distances[i], Mathf.Min(distances[i + 1] + 1f,
                    Mathf.Min(distances[i + w] + 1f, distances[i + w + 1] + 1.41421356f)));
                if (x > 0) distances[i] = Mathf.Min(distances[i], distances[i + w - 1] + 1.41421356f);
            }
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(distances[i] * 8f / Mathf.Min(w, h)) * 255f), 0, 0, 255);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                name = "Watercolor study interior distance", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        private static RenderTexture Allocate(string name)
        {
            var texture = new RenderTexture(Columns, Columns / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false, antiAliasing = 1
            };
            if (!texture.Create()) { Object.DestroyImmediate(texture); throw new InvalidOperationException("Watercolor render texture allocation failed."); }
            return texture;
        }
        private static void Release<T>(ref T value) where T : Object
        {
            if (value != null) { if (value is RenderTexture target) target.Release(); Object.DestroyImmediate(value); }
            value = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; baseline.Dispose(); renderer?.Dispose(); renderer = null;
            Release(ref material); Release(ref distance); Release(ref colors); Release(ref owners);
            Release(ref optics); Release(ref candidate); Surface = null;
            // brush belongs to Resources; never destroy or edit the shared asset.
            brush = null;
        }
    }
}
