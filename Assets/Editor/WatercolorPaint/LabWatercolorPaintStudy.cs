using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>
    /// Isolated saved-gourd comparison of the original high-resolution renderer and the
    /// production watercolor renderer. Never installs its private surface in a live session.
    /// </summary>
    public sealed class LabWatercolorPaintStudy : IDisposable
    {
        private readonly LabHighResolutionPaintRuntime baseline = new LabHighResolutionPaintRuntime();
        private readonly LabWatercolorPaintRuntime watercolor = new LabWatercolorPaintRuntime();
        private LabPaintStudyRenderer renderer;
        private Texture2D colors, owners;
        private int cachedRepeats = -1;
        private uint cachedSeed;
        private Vector3 cachedDirection;
        private bool disposed;

        public LabPaintSurface Surface { get; private set; }
        public Vector3 HitDirection { get; set; } = Vector3.forward;
        public Texture CurrentMap => baseline.PaintTexture;
        public Texture CandidateMap => watercolor.PaintTexture;

        public LabWatercolorPaintStudy()
        {
            try { renderer = new LabPaintStudyRenderer(true); }
            catch { Dispose(); throw; }
        }

        /// <summary>The caller owns both images: original runtime and approved watercolor.</summary>
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
            if (!baseline.Update(Surface, colors, owners, 1024, true))
                throw new InvalidOperationException(baseline.Error ?? "The production high-resolution maps are not ready.");
            // Static comparison always finishes the local visual animation. The same runtime
            // renderer is used by LabGourdPaint with normal incremental updates in the game.
            if (!watercolor.Update(Surface, baseline.PaintTexture, baseline.PainterTexture, settings, 0d, true))
                throw new InvalidOperationException(watercolor.Error ?? "The watercolor maps are not ready.");
        }

        private void BuildSurface(int repeats, uint seed, Vector3 d)
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            if (source == null) throw new InvalidOperationException("The saved LabConfig is missing.");
            LabPaintGrowthSettings rules = source.PaintGrowthSettings;
            if (rules.Pattern != LabPaintPattern.Splatter)
                throw new InvalidOperationException("The blue comparison requires the saved Splatter growth pattern.");
            rules.DryingEnabled = false; // This private still-frame fixture only.
            var next = new LabPaintSurface(rules, seed);
            Color blue = LabPaintPalette.ColorFor(LabOrbElement.Water);
            next.RegisterHit(d, blue, 1, 1);
            for (int i = 0; i < repeats; i++) { next.RegisterHit(d, blue, 1, 1); next.Advance(1d); }
            Release(ref colors); Release(ref owners);
            colors = Upload(next, false); owners = Upload(next, true);
            Surface = next; cachedRepeats = repeats; cachedSeed = seed; cachedDirection = d;
        }

        private static Texture2D Upload(LabPaintSurface surface, bool painter)
        {
            var texture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Watercolor study authority copy", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            try
            {
                var pixels = new Color32[surface.CellCount];
                if (painter)
                {
                    var slots = new byte[surface.CellCount]; surface.CopyPainterSlots(slots);
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(slots[i], 0, 0, 255);
                }
                else surface.CopyPixels(pixels);
                texture.SetPixels32(pixels); texture.Apply(false, false); return texture;
            }
            catch { Object.DestroyImmediate(texture); throw; }
        }

        private static void Release(ref Texture2D value)
        { if (value != null) Object.DestroyImmediate(value); value = null; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; watercolor.Dispose(); baseline.Dispose(); renderer?.Dispose(); renderer = null;
            Release(ref colors); Release(ref owners); Surface = null;
        }
    }
}
