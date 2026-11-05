using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public enum LabWatercolorStampFixture { BlueRehits, TwoColorOverlap, ThreeColorCross, FiveColorComplete }

    /// <summary>Private Host-rule fixture. No active Scene, shared config or connected session is changed.</summary>
    public sealed class LabWatercolorStampStudy : IDisposable
    {
        private readonly LabHighResolutionPaintRuntime baseline = new LabHighResolutionPaintRuntime();
        private readonly LabWatercolorPaintRuntime previous = new LabWatercolorPaintRuntime();
        private readonly LabWatercolorStampRuntime candidate = new LabWatercolorStampRuntime();
        private LabPaintStudyRenderer renderer;
        private Texture2D colors, owners;
        private LabPaintArtJournal journal;
        private string fixtureKey;
        private bool disposed;
        private Texture2D appearancePattern;

        public LabPaintSurface Surface { get; private set; }
        public LabPaintArtJournal Journal => journal;
        public Texture CurrentMap => previous.PaintTexture;
        public Texture HighResolutionMap => baseline.PaintTexture;
        public Texture HighResolutionPainterMap => baseline.PainterTexture;
        public Texture CandidateMap => candidate.PaintTexture;
        public bool Ready => candidate.Ready;
        public string Error => candidate.Error ?? journal?.Error;
        public ulong AppliedThrough => candidate.AppliedThrough;
        public ulong RenderedThrough => candidate.RenderedThrough;
        public Vector3 HitDirection { get; set; } = Vector3.forward;
        public static readonly string[] ViewNames = { "front", "right", "back", "left", "diagonal", "upper-diagonal" };
        private static readonly Vector2[] Views = {
            Vector2.zero, new Vector2(90f, 0f), new Vector2(180f, 0f), new Vector2(-90f, 0f),
            new Vector2(45f, 0f), new Vector2(45f, 45f)
        };

        public LabWatercolorStampStudy()
        {
            try { renderer = new LabPaintStudyRenderer(true); }
            catch { Dispose(); throw; }
        }

        /// <summary>Old watercolor and new stamps, same ledger, camera, lighting and body.</summary>
        public Texture2D[] Render(LabWatercolorStampFixture fixture, int repeats, uint seed,
            float yaw, float pitch, float zoom, LabWatercolorStampSettings settings)
        {
            Prepare(fixture, repeats, seed, settings);
            Texture2D[] frames = new Texture2D[2];
            try
            {
                frames[0] = renderer.RenderVisual(CurrentMap, yaw, zoom, pitch);
                frames[1] = renderer.RenderVisual(CandidateMap, yaw, zoom, pitch, settings);
                return frames;
            }
            catch
            {
                foreach (Texture2D frame in frames) if (frame != null) Object.DestroyImmediate(frame);
                throw;
            }
        }

        /// <summary>Front/right/back on the top row, left/diagonal/upper-diagonal below.</summary>
        public Texture2D[] RenderSixViews(bool useCandidate, LabWatercolorStampFixture fixture, int repeats,
            uint seed, LabWatercolorStampSettings settings)
        {
            Prepare(fixture, repeats, seed, settings);
            var frames = new Texture2D[Views.Length];
            try
            {
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = renderer.RenderVisual(useCandidate ? CandidateMap : CurrentMap, Views[i].x, 1f, Views[i].y,
                        useCandidate ? settings : null);
                return frames;
            }
            catch
            {
                foreach (Texture2D frame in frames) if (frame != null) Object.DestroyImmediate(frame);
                throw;
            }
        }

        /// <summary>V1 appearance under the V2 implementation; neutral added controls retain old stamp arithmetic.</summary>
        public static LabWatercolorStampSettings V1EquivalentSettings()
        {
            var value = new LabWatercolorStampSettings { mode = LabPaintStampMode.Watercolor };
            value.enabled = true; value.density = .8f; value.overlap = .72f;
            value.edgeStrength = .35f; value.edgeWidth = .04f; value.revealSeconds = .35f;
            value.pigmentContrast = 1f; value.pigmentSaturation = 1f;
            value.rimBreakup = 0f; value.granulationStrength = 0f;
            return value;
        }

        /// <summary>Same seeded Host rules and saved camera, before V1 appearance / after V2.</summary>
        public Texture2D[] RenderRefinement(LabWatercolorStampFixture fixture, int repeats, uint seed,
            float yaw, float pitch, float zoom, LabWatercolorStampSettings settings)
        {
            var images = new Texture2D[2];
            try
            {
                var before = V1EquivalentSettings();
                Prepare(fixture, repeats, seed, before);
                string authority = Hash(Surface);
                images[0] = renderer.RenderVisual(CandidateMap, yaw, zoom, pitch, before);
                Prepare(fixture, repeats, seed, settings);
                if (Hash(Surface) != authority)
                    throw new InvalidOperationException("Changing the appearance profile changed the Host color/P fixture.");
                images[1] = renderer.RenderVisual(CandidateMap, yaw, zoom, pitch, settings);
                return images;
            }
            catch
            {
                foreach (var frame in images) if (frame != null) Object.DestroyImmediate(frame);
                throw;
            }
        }

        /// <summary>Production transparent PNG export of the already prepared private visual fixture.</summary>
        public Texture2D CaptureProductionPng(LabWatercolorStampSettings settings, string diagnosticDirectory = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabWatercolorStampStudy));
            // Ready additionally waits for an asynchronous runtime callback, which
            // a synchronous Editor export need not have pumped yet. The fixture must
            // have submitted every operation with no reveal in flight; production PNG
            // capture then renders and reads back the image synchronously itself.
            if (journal == null || !journal.IsSealed || CandidateMap == null || candidate.Error != null
                || candidate.IsAnimating || candidate.AppliedThrough != journal.LastSequence)
                throw new InvalidOperationException("Prepare the private watercolor fixture before PNG capture: " + candidate.Error);
            return renderer.CaptureProductionPng(CandidateMap, settings, diagnosticDirectory);
        }

        /// <summary>Offline fixture override only. Changing this input invalidates the cached fixture.</summary>
        public void SetPatternForAppearanceStudy(Texture2D source)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabWatercolorStampStudy));
            appearancePattern = source;
            fixtureKey = null;
        }

        public void Prepare(LabWatercolorStampFixture fixture, int repeats, uint seed, LabWatercolorStampSettings settings)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabWatercolorStampStudy));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            if (!settings.enabled) throw new ArgumentException("Enable stamps for the new-renderer comparison.");
            if (repeats < 0 || repeats > 10) throw new ArgumentOutOfRangeException(nameof(repeats));
            Vector3 d = HitDirection;
            if (!float.IsFinite(d.x) || !float.IsFinite(d.y) || !float.IsFinite(d.z)
                || !float.IsFinite(d.sqrMagnitude) || d.sqrMagnitude < .00001f)
                throw new ArgumentException("A finite, nonzero hit direction is required.");
            d.Normalize();
            string key = fixture + ":" + repeats + ":" + seed + ":" + d.ToString("R") + ":" + settings.Version;
            if (Surface == null || fixtureKey != key) BuildSurface(fixture, repeats, seed, d, settings.Version);
            if (!baseline.Update(Surface, colors, owners, 1024, true))
                throw new InvalidOperationException(baseline.Error ?? "The high-resolution fixture is not ready.");
            if (!previous.Update(Surface, baseline.PaintTexture, baseline.PainterTexture,
                new LabWatercolorPaintSettings(), 0d, true))
                throw new InvalidOperationException(previous.Error ?? "The previous watercolor fixture is not ready.");
            Finish(candidate, journal, baseline.PaintTexture, settings);
            if (candidate.Error != null || candidate.PaintTexture == null || candidate.AppliedThrough != journal.LastSequence)
                throw new InvalidOperationException(candidate.Error ?? "The stamp journal has not been applied.");
            fixtureKey = key;
        }

        /// <summary>Bounded synchronous loop for this isolated Editor still fixture only, never the live frame loop.</summary>
        public static void Finish(LabWatercolorStampRuntime runtime, LabPaintArtJournal journal, Texture authority,
            LabWatercolorStampSettings settings)
        {
            ulong before;
            int iterations = 0;
            do
            {
                before = runtime.AppliedThrough;
                runtime.Update(journal, authority, 1024, settings, 1d, true);
                if (runtime.Error != null) throw new InvalidOperationException(runtime.Error);
                if (++iterations > 4096 || runtime.AppliedThrough == before && before < journal.LastSequence)
                    throw new InvalidOperationException("The private still-image journal replay did not progress.");
            }
            while (runtime.AppliedThrough < journal.LastSequence);
        }

        private void BuildSurface(LabWatercolorStampFixture fixture, int repeats, uint seed, Vector3 d, string version)
        {
            Release(ref colors); Release(ref owners);
            journal?.Dispose(); journal = null;
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            if (source == null) throw new InvalidOperationException("The saved LabConfig is missing.");
            LabPaintGrowthSettings rules = source.PaintGrowthSettings;
            rules.DryingEnabled = false; // Isolated static fixture, never the shared game configuration.
            var surface = new LabPaintSurface(rules, seed);
            if (version.StartsWith(LabWatercolorStampSettings.PaperStickerStyleVersion + "/", StringComparison.Ordinal))
                surface.SetPaperStickerContour();
            if (appearancePattern != null) surface.SetPatternForAppearanceStudy(appearancePattern);
            journal = new LabPaintArtJournal(Path.Combine(Path.GetTempPath(), "c6-watercolor-study-" + Guid.NewGuid().ToString("N")),
                "watercolor-study-" + fixture, surface.Columns, surface.Rows, version);
            if (journal.Error != null || !surface.AttachArtJournal(journal))
                throw new InvalidOperationException(journal.Error ?? "Cannot attach the private fixture journal.");
            if (fixture == LabWatercolorStampFixture.BlueRehits)
            {
                Hit(surface, d, LabOrbElement.Water, 1);
                for (int i = 0; i < repeats; i++) { Hit(surface, d, LabOrbElement.Water, 1); surface.Advance(1d); }
            }
            else if (fixture == LabWatercolorStampFixture.TwoColorOverlap)
            {
                Hit(surface, d, LabOrbElement.Water, 1);
                Hit(surface, Quaternion.AngleAxis(12f, Vector3.up) * d, LabOrbElement.Fire, 2);
                surface.Advance(1d);
                for (int i = 0; i < repeats; i++)
                { Hit(surface, d, i % 2 == 0 ? LabOrbElement.Fire : LabOrbElement.Water, i % 2 + 1); surface.Advance(1d); }
            }
            else if (fixture == LabWatercolorStampFixture.ThreeColorCross)
            {
                LabOrbElement[] elements = { LabOrbElement.Water, LabOrbElement.Fire, LabOrbElement.Wood };
                for (int i = 0; i < 3; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis((i - 1) * 15f, Vector3.up) * d;
                    Hit(surface, direction, elements[i], i + 1); surface.Advance(1d);
                }
                for (int i = 0; i < repeats; i++) { Hit(surface, d, elements[i % 3], i % 3 + 1); surface.Advance(1d); }
            }
            else if (fixture == LabWatercolorStampFixture.FiveColorComplete)
            {
                LabOrbElement[] elements = {
                    LabOrbElement.Fire, LabOrbElement.Metal, LabOrbElement.Wood, LabOrbElement.Water, LabOrbElement.Earth
                };
                // Host rules still choose every newly paid cell. Fibonacci hit directions
                // distribute five colors around the entire body, including the hidden back.
                for (int i = 0; i < 240 && !surface.IsComplete; i++)
                {
                    double y = 1d - 2d * ((i * 37 % 239) + .5d) / 239d;
                    double radius = Math.Sqrt(Math.Max(0d, 1d - y * y)), angle = i * 2.399963229728653d;
                    var direction = new Vector3((float)(Math.Cos(angle) * radius), (float)y, (float)(Math.Sin(angle) * radius));
                    Hit(surface, direction, elements[i % 5], i % 5 + 1);
                    surface.Advance(1d);
                }
                if (!surface.IsComplete) throw new InvalidOperationException("The private five-color fixture did not reach 100%.");
            }
            else throw new ArgumentOutOfRangeException(nameof(fixture));
            colors = Upload(surface, false); owners = Upload(surface, true);
            string hash = Hash(surface);
            if (!journal.Commit(surface.Revision, hash) || !journal.Seal(surface.Revision, hash))
                throw new InvalidOperationException(journal.Error ?? "Cannot seal the private fixture.");
            Surface = surface;
        }

        private static void Hit(LabPaintSurface surface, Vector3 direction, LabOrbElement element, int slot)
        { if (!surface.RegisterHit(direction, LabPaintPalette.ColorFor(element), (ulong)slot, slot))
            throw new InvalidOperationException("The private fixture hit was rejected."); }

        public static string Hash(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; var slots = new byte[surface.CellCount];
            surface.CopyPixels(pixels); surface.CopyPainterSlots(slots);
            return LabPaintPublisher.Hash(pixels, slots);
        }

        private static Texture2D Upload(LabPaintSurface surface, bool painter)
        {
            var texture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
            { name = "Watercolor stamp study authority", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
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

        public static Texture2D CombineGrid(Texture2D[] frames, int columns = 3)
        {
            if (frames == null || frames.Length == 0 || columns < 1 || frames[0] == null)
                throw new ArgumentException("A nonempty equal-size image array is required.");
            int width = frames[0].width, height = frames[0].height, rows = (frames.Length + columns - 1) / columns;
            var pixels = new Color32[width * columns * height * rows];
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null || frames[i].width != width || frames[i].height != height)
                    throw new ArgumentException("Image sizes differ.");
                Color32[] data = frames[i].GetPixels32();
                int xOffset = i % columns * width, yOffset = (rows - i / columns - 1) * height;
                for (int y = 0; y < height; y++)
                    Array.Copy(data, y * width, pixels, (yOffset + y) * width * columns + xOffset, width);
            }
            var combined = new Texture2D(width * columns, height * rows, TextureFormat.RGBA32, false, false)
                { hideFlags = HideFlags.HideAndDontSave };
            combined.SetPixels32(pixels); combined.Apply(false, false); return combined;
        }

        private static void Release(ref Texture2D value)
        { if (value != null) Object.DestroyImmediate(value); value = null; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            candidate.Dispose(); previous.Dispose(); baseline.Dispose(); renderer?.Dispose(); renderer = null;
            journal?.Dispose(); journal = null; Surface = null;
            Release(ref colors); Release(ref owners);
        }
    }
}
