using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public static class LabHighResolutionPaintValidation
    {
        public static void RunFromCommandLine()
        {
            string directory = Environment.GetEnvironmentVariable("C6_HIRES_EVIDENCE_DIR");
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("C6_HIRES_EVIDENCE_DIR must be an absolute output directory.");
            Export(directory);
        }

        public static void Export(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory)) throw new ArgumentException("An absolute output directory is required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("GPU validation requires a graphics device.");
            int previewRoots = PreviewRoots(), paintTargets = PaintTargets();
            Scene active = SceneManager.GetActiveScene(); int sceneCount = SceneManager.sceneCount; bool dirty = active.isDirty;
            var config = Resources.Load<LabConfig>("LabConfig"); string configBefore = JsonUtility.ToJson(config);
            var settings = config.PaintGrowthSettings; settings.DryingEnabled = false;
            Directory.CreateDirectory(directory);
            var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                classification = "Offline saved-prefab GPU fixtures, not live gameplay, network, device or performance validation.",
                seed = 123, initialPercent = settings.InitialPercent, repeatPercent = settings.RepeatPercent };
            using (var baker = new LabHighResolutionPaintBaker())
            using (var renderer = new LabPaintStudyRenderer(true))
            {
                foreach (HighResolutionHitLocation location in Enum.GetValues(typeof(HighResolutionHitLocation)))
                {
                    var fixture = new LabHighResolutionPaintFixture(settings, baker.Mask,
                        LabHighResolutionPaintWindow.Direction(location), LabPaintPalette.ColorFor(LabOrbElement.Water));
                    for (int stage = 0; stage <= 3; stage++)
                    {
                        if (location != HighResolutionHitLocation.Front && stage != 3) continue;
                        RenderTexture medium = null, large = null; Texture2D sheet = null;
                        var frames = new Texture2D[3];
                        var before = LabHighResolutionPaintFixture.Pixels(fixture.Stages[stage]);
                        try
                        {
                            medium = baker.Bake(fixture, stage, 1024); large = baker.Bake(fixture, stage, 2048);
                            ValidateMap(large, fixture, stage, location, report);
                            float yaw = location == HighResolutionHitLocation.LongitudeSeam ? 90 : 0;
                            float pitch = location == HighResolutionHitLocation.NorthPole ? 90 : 0;
                            frames[0] = renderer.RenderSurface(fixture.Stages[stage], false, yaw, 1f, pitch);
                            frames[1] = renderer.RenderVisual(medium, yaw, 1f, pitch);
                            frames[2] = renderer.RenderVisual(large, yaw, 1f, pitch);
                            sheet = LabPaintStudyRenderer.Combine(frames);
                            string filename = $"{location.ToString().ToLowerInvariant()}-first-plus-{stage}.png";
                            File.WriteAllBytes(Path.Combine(directory, filename), sheet.EncodeToPNG());
                            report.entries.Add($"{filename}: left native / middle1024 / right2048; native cells={fixture.Stages[stage].CoveredCount}");
                            if (!Equal(before, LabHighResolutionPaintFixture.Pixels(fixture.Stages[stage]))) throw new InvalidOperationException("Baking changed game-rule pixels.");
                        }
                        finally
                        {
                            if (sheet != null) Object.DestroyImmediate(sheet);
                            foreach (Texture2D frame in frames) if (frame != null) Object.DestroyImmediate(frame);
                            LabHighResolutionPaintBaker.Release(medium); LabHighResolutionPaintBaker.Release(large);
                        }
                    }
                }
                // Off-axis first mark detects a GPU Y flip that a front-centered mark hides.
                var offAxis = new LabHighResolutionPaintFixture(settings, baker.Mask,
                    new Vector3(.35f,.3f,1).normalized, LabPaintPalette.ColorFor(LabOrbElement.Water));
                var texture = baker.Bake(offAxis, 0, 1024);
                try { ValidateMap(texture, offAxis, 0, HighResolutionHitLocation.Front, report); }
                finally { LabHighResolutionPaintBaker.Release(texture); }
            }
            if (active != SceneManager.GetActiveScene() || sceneCount != SceneManager.sceneCount || active.isDirty != dirty)
                throw new InvalidOperationException("Preview changed the active/loaded scene state.");
            if (configBefore != JsonUtility.ToJson(config)) throw new InvalidOperationException("Preview changed the saved config.");
            if (PreviewRoots() != previewRoots || PaintTargets() != paintTargets)
                throw new InvalidOperationException("Comparison leaked a preview root or paint RenderTexture.");
            report.activeScenePreserved = report.configPreserved = report.resourcesReleased = true;
            File.WriteAllText(Path.Combine(directory, "validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_HIRES_PAINT_VALIDATED renders=" + report.entries.Count);
        }

        private static void ValidateMap(RenderTexture target, LabHighResolutionPaintFixture fixture, int stage,
            HighResolutionHitLocation location, Report report)
        {
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); int soft = 0, covered = 0, softEdge = 0;
                foreach (Color32 p in pixels)
                {
                    if (p.a > 0) covered++;
                    if (p.a > 0 && p.a < 255) soft++;
                    if (p.a > 0 && p.a < 32) softEdge++;
                    if (p.r > p.a + 1 || p.g > p.a + 1 || p.b > p.a + 1) throw new InvalidOperationException("Paint RGB is not premultiplied.");
                }
                Vector3 d = fixture.HitDirection;
                float u = Mathf.Atan2(d.z, d.x) / (2 * Mathf.PI) + .5f, v = .5f - d.y * .5f;
                int x = Mathf.Clamp((int)(u * target.width), 0, target.width - 1);
                int y = Mathf.Clamp((int)(v * target.height), 0, target.height - 1);
                if (pixels[y * target.width + x].a < 80) throw new InvalidOperationException("Paint missing at the hit direction; inspect GPU projection/orientation.");
                if (soft < 100 || covered < 100 || softEdge < 20) throw new InvalidOperationException("Continuous pigment alpha was lost.");
                Color32 centerPixel = pixels[y * target.width + x];
                Color expected = fixture.Stamps[0].Color;
                if (centerPixel.b > 20 && Math.Abs(centerPixel.g / (float)centerPixel.b - expected.g / expected.b) > .06f)
                    throw new InvalidOperationException("Linear pigment was converted again or its hue changed.");
                if (stage == 0) ValidateProjectionSamples(pixels, target.width, target.height, fixture);
                if (location == HighResolutionHitLocation.LongitudeSeam)
                {
                    int row = target.height / 2;
                    if (Math.Abs(pixels[row * target.width].a - pixels[row * target.width + target.width - 1].a) > 25)
                        throw new InvalidOperationException("Longitude seam is discontinuous.");
                }
                report.entries.Add($"map {location}+{stage}: {target.width}x{target.height}; visible={covered}; intermediate alpha={soft}; soft edge={softEdge}; visual coverage is not gameplay coverage");
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        private static void ValidateProjectionSamples(Color32[] pixels, int columns, int rows, LabHighResolutionPaintFixture fixture)
        {
            // Sample an asymmetric footprint throughout the atlas, not just its center.
            Texture2D source = Resources.Load<Texture2D>("GourdPaint/Patterns/Splatter");
            var mask = new LabPaintStudyMask(source.width, source.height, source.GetPixels32());
            int inside = 0, outside = 0;
            for (int y = 4; y < rows - 4; y += Math.Max(1, rows / 64))
                for (int x = 4; x < columns - 4; x += Math.Max(1, columns / 128))
                {
                    int cell = y * columns + x;
                    float ink = LabHighResolutionPaintFixture.Sample(mask, fixture.Stamps[0],
                        LabHighResolutionPaintFixture.Direction(cell, columns, rows));
                    if (ink > .5f)
                    {
                        inside++;
                        if (pixels[cell].a < 100) throw new InvalidOperationException("CPU/GPU brush interior projection differs.");
                    }
                    else if (ink < .001f)
                    {
                        bool clear = true;
                        foreach (int offset in new[] { -4, 4, -columns * 4, columns * 4 })
                            clear &= LabHighResolutionPaintFixture.Sample(mask, fixture.Stamps[0],
                                LabHighResolutionPaintFixture.Direction(cell + offset, columns, rows)) < .001f;
                        if (!clear) continue;
                        outside++;
                        if (pixels[cell].a > 15) throw new InvalidOperationException("CPU/GPU brush exterior projection differs.");
                    }
                }
            if (inside < 5 || outside < 100) throw new InvalidOperationException("Insufficient projection samples.");
        }

        private static int PreviewRoots() => Resources.FindObjectsOfTypeAll<GameObject>()
            .Count(o => o.name == "Paint study — saved gourd copy");
        private static int PaintTargets() => Resources.FindObjectsOfTypeAll<RenderTexture>()
            .Count(o => o.name == "High-resolution visual fixture");

        private static bool Equal(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) return false;
            return true;
        }
        [Serializable] private sealed class Report
        {
            public string unity, gpu, classification;
            public uint seed;
            public float initialPercent, repeatPercent;
            public bool activeScenePreserved, configPreserved, resourcesReleased;
            public List<string> entries = new List<string>();
        }
    }
}
