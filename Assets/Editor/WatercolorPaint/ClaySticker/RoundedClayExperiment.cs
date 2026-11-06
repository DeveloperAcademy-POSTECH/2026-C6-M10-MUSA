using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hapio.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Opt-in, isolated #527 artwork comparison. Never changes a live game profile or authority.</summary>
    public static class RoundedClayExperiment
    {
        public const string Root = ClayPreviewBuild.Root + "/Rounded";
        public const string ScenePath = Root + "/Scenes/RoundedClayPreview.unity";
        public const string SurfaceShaderPath = ClayPreviewBuild.Root + "/Runtime/Shaders/RoundedClayStickerSurface.shader";
        public const string Version = "clay-stickers-v3";
        static readonly string[] Names = { "Broad", "Long", "Cluster" };
        static readonly string[] Keys = { "OriginalV2", "OriginalV3", "RoundedV3" };
        static readonly string[] Labels = { "Original v2", "Original outline + enhanced clay", "Rounded outline + enhanced clay" };
        const float FullViewZoom = .68f, DetailViewZoom = 1.45f;
        static float ThicknessFor(int variant) => variant == 0 ? ClayPreviewController.DefaultThickness : ClayPreviewController.EnhancedThickness;
        static float GlossFor(int variant) => variant == 0 ? ClayPreviewController.DefaultGloss : ClayPreviewController.EnhancedGloss;

        [Serializable] public sealed class Sample : ClayPreviewSampleInfo
        {
            public string preset, brushVersion, sourceJournalStyle;
            public float minimumDensity, maximumDensity, bevelPixels;
            public double replayMilliseconds, warmRenderMeanMilliseconds;
            public long estimatedRenderTextureBytes, measuredRenderTextureByteDelta;
            public bool gpuFrameTimeMeasured;
            public string timingScope = "Measured stopped-Editor CPU wall time including bounded GPU completion waits; not a GPU profiler/frame-time measurement.";
            public string memoryScope = "Estimate: six renderer RT payloads. Measured delta: Unity Profiler-reported sizes of created RenderTextures before/after replay, including any new temporary pool textures; not a physical GPU-resident-memory measurement.";
            public string warmRenderScope = "Mean of five stopped-Editor synchronous camera render/readback wall times after one discarded warm render; not gameplay CPU/GPU frame timing.";
        }
        [Serializable] public sealed class MaskReport
        {
            public string name, sourceHash, whiteMaskHash;
            public int retainedPixels, removedTinyComponents;
            public float alphaThreshold = .9f, transparentPadding = .1f;
            public int closingRadius = 1, minimumComponentPixels = 24;
        }
        [Serializable] public sealed class PreparedValidation
        { public bool valid; public int presets; public string recordHash, authorityHash; }
        [Serializable] public sealed class Report
        {
            public string unity, gpu, scope, recordHash, authorityHash;
            public string[] presetColumns = Labels;
            public float[] presetThickness = { ClayPreviewController.DefaultThickness, ClayPreviewController.EnhancedThickness, ClayPreviewController.EnhancedThickness };
            public float[] presetGloss = { ClayPreviewController.DefaultGloss, ClayPreviewController.EnhancedGloss, ClayPreviewController.EnhancedGloss };
            public float fullViewZoom = FullViewZoom, detailViewZoom = DetailViewZoom;
            public string framing = "Completed three-preset/six-view images use the full-object frame. First/re-hit/overlap rows are intentionally enlarged detail crops. Each column uses its standalone app preset defaults, not identical control values.";
            public string[] fixtureRows = { "first", "first-plus-three", "same-color-overlap", "two-color-overlap" };
            public bool sourceConfigPreserved, randomPreserved, scenesPreserved, originalSamplePreserved;
            public List<Sample> samples = new List<Sample>();
            public MaskReport[] masks;
            public List<LabClayVisualStudy.Entry> fixtures = new List<LabClayVisualStudy.Entry>();
        }

        /// <summary>Explicit source import. The three generated PNG originals are copied byte-for-byte.</summary>
        public static string Prepare(string[] transparentPngSources)
        {
            ClayPreviewBuild.RequireStopped();
            RequireSavedExperimentScene();
            Require(transparentPngSources != null && transparentPngSources.Length == 3, "Supply Broad, Long and Cluster transparent PNG originals.");
            for (int i = 0; i < Names.Length; i++)
            {
                Require(File.Exists(transparentPngSources[i]), "A generated transparent PNG original is missing.");
                string destination = Root + "/Sources/" + Names[i] + ".png";
                ClayPreviewBuild.EnsureFolder(Root + "/Sources");
                if (Path.GetFullPath(transparentPngSources[i]) != Path.GetFullPath(destination))
                    File.Copy(transparentPngSources[i], destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            }
            return PrepareImported();
        }
        [MenuItem("C6 Lab/Experiments/Rounded Clay/Prepare Imported Comparison")]
        public static void PrepareMenu() => Debug.Log("ROUNDED_CLAY_PREPARED " + PrepareImported());

        public static string PrepareImported()
        {
            ClayPreviewBuild.RequireStopped(); RequireSavedExperimentScene();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "The isolated comparison needs an actual graphics device.");
            string oldSample = ExistingSampleHash(), scenes = ClayPreviewBuild.SceneState();
            BakeBrushSet("OriginalBevel", Names.Select(n => LabSurfaceStickerPrototype.Folder + "/Assets/" + n + "Paper.png").ToArray(), 20);
            PrepareWhiteMasks();
            BakeBrushSet("RoundedBevel", Names.Select(n => Root + "/Masks/" + n + "WhiteMask.png").ToArray(), 20);
            string json = BakeAndCompare(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp", "rounded-clay-comparison"));
            CreateComparisonScene();
            Require(oldSample == ExistingSampleHash(), "The original v2 sample was changed by comparison preparation.");
            Require(scenes == ClayPreviewBuild.SceneState(), "The comparison changed an already loaded Scene.");
            ValidatePrepared(); return json;
        }
        static void PrepareWhiteMasks()
        {
            string folder = Root + "/Masks"; ClayPreviewBuild.EnsureFolder(folder);
            foreach (string name in Names)
            {
                string sourcePath = Root + "/Sources/" + name + ".png";
                Require(File.Exists(sourcePath), "Import the three generated transparent PNG originals first.");
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); Texture2D maskTexture = null;
                try
                {
                    byte[] original = File.ReadAllBytes(sourcePath);
                    Require(source.LoadImage(original, false), "Could not decode a generated PNG original.");
                    const int size = 512; var mask = new bool[size * size];
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                        mask[y * size + x] = source.GetPixelBilinear((x + .5f) / size, (y + .5f) / size).a >= .9f;
                    // Close one-pixel cracks, smooth only the immediate binary contour,
                    // then remove tiny disconnected noise. Real rounded droplets remain.
                    mask = Morph(Morph(mask, size, true), size, false);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        var smooth = new bool[mask.Length];
                        for (int y = 1; y < size - 1; y++) for (int x = 1; x < size - 1; x++)
                        {
                            int neighbors = 0;
                            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (mask[(y + dy) * size + x + dx]) neighbors++;
                            smooth[y * size + x] = neighbors >= 5;
                        }
                        mask = smooth;
                    }
                    int removed = RemoveTinyComponents(mask, size, 24);
                    int left = size, right = -1, bottom = size, top = -1;
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if (mask[y * size + x])
                    { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y); }
                    Require(right >= left && top >= bottom, "Alpha cleanup removed the entire generated silhouette.");
                    int width = right - left + 1, height = top - bottom + 1;
                    float scale = (size * .8f - 2) / Math.Max(width, height);
                    var pixels = new Color32[mask.Length]; int retained = 0;
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        float sx = (x + .5f - size * .5f) / scale + (left + right) * .5f;
                        float sy = (y + .5f - size * .5f) / scale + (bottom + top) * .5f;
                        int ix = Mathf.FloorToInt(sx), iy = Mathf.FloorToInt(sy); float fx = sx - ix, fy = sy - iy;
                        float alpha = Mathf.Lerp(Mathf.Lerp(Bit(ix, iy), Bit(ix + 1, iy), fx), Mathf.Lerp(Bit(ix, iy + 1), Bit(ix + 1, iy + 1), fx), fy);
                        byte a = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0, 1, alpha) * 255);
                        pixels[y * size + x] = new Color32(255, 255, 255, a); if (a != 0) retained++;
                    }
                    maskTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true); maskTexture.SetPixels32(pixels); maskTexture.Apply();
                    string path = folder + "/" + name + "WhiteMask.png"; byte[] bytes = maskTexture.EncodeToPNG(); File.WriteAllBytes(path, bytes); ImportBrush(path);
                    Require(pixels.All(p => p.r == 255 && p.g == 255 && p.b == 255), "The generated technical mask is not pure white.");
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                        if (x < 51 || y < 51 || x >= size - 51 || y >= size - 51) Require(pixels[y * size + x].a == 0, "The cleaned mask lost its ten-percent transparent margin.");
                    var info = new MaskReport { name = name, sourceHash = ClayPreviewBuild.Hash(original), whiteMaskHash = ClayPreviewBuild.Hash(bytes), retainedPixels = retained, removedTinyComponents = removed };
                    string metadata = folder + "/" + name + "MaskInfo.json"; File.WriteAllText(metadata, JsonUtility.ToJson(info, true)); AssetDatabase.ImportAsset(metadata, ImportAssetOptions.ForceSynchronousImport);
                    float Bit(int x, int y) => x >= 0 && y >= 0 && x < size && y < size && mask[y * size + x] ? 1 : 0;
                }
                finally { Object.DestroyImmediate(source); if (maskTexture != null) Object.DestroyImmediate(maskTexture); }
            }
        }
        static bool[] Morph(bool[] source, int size, bool dilate)
        {
            var result = new bool[source.Length];
            for (int y = 1; y < size - 1; y++) for (int x = 1; x < size - 1; x++)
            {
                bool value = !dilate;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    if (dilate) value |= source[(y + dy) * size + x + dx]; else value &= source[(y + dy) * size + x + dx];
                result[y * size + x] = value;
            }
            return result;
        }
        static int RemoveTinyComponents(bool[] mask, int size, int minimum)
        {
            var seen = new bool[mask.Length]; var queue = new Queue<int>(); var component = new List<int>(); int removed = 0;
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || seen[start]) continue;
                queue.Enqueue(start); seen[start] = true; component.Clear();
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue(); component.Add(cell); int x = cell % size, y = cell / size;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                        int next = ny * size + nx; if (seen[next] || !mask[next]) continue; seen[next] = true; queue.Enqueue(next);
                    }
                }
                if (component.Count >= minimum) continue; foreach (int cell in component) mask[cell] = false; removed++;
            }
            return removed;
        }
        static void BakeBrushSet(string set, string[] sources, int bevelPixels)
        {
            string folder = Root + "/" + set; ClayPreviewBuild.EnsureFolder(folder);
            using (var bytes = new MemoryStream())
            {
                byte[] descriptor = Encoding.UTF8.GetBytes("rounded-clay-data-v1-alpha-only-bevel-" + bevelPixels);
                bytes.Write(descriptor, 0, descriptor.Length);
                for (int i = 0; i < Names.Length; i++)
                {
                    Require(File.Exists(sources[i]), "The original silhouette is missing: " + sources[i]);
                    Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true), data = null;
                    try
                    {
                        Require(source.LoadImage(File.ReadAllBytes(sources[i]), false), "Could not decode the PNG original.");
                        const int size = 512; var pixels = new Color32[size * size]; var distance = new float[pixels.Length];
                        int ink = 0;
                        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                        {
                            // Coverage is alpha only. The generated original's white/gray RGB never becomes coverage.
                            float alpha = source.GetPixelBilinear((x + .5f) / size, (y + .5f) / size).a;
                            byte a = (byte)Mathf.RoundToInt(alpha * 255); int k = y * size + x;
                            pixels[k] = new Color32(a, 0, 0, a); distance[k] = alpha >= .3f ? 2048 : 0;
                            if (alpha >= .3f) ink++;
                            if ((x < 2 || y < 2 || x >= size - 2 || y >= size - 2) && alpha > .02f)
                                throw new InvalidOperationException("The source silhouette needs transparent margin: " + Names[i]);
                        }
                        Require(ink > 512 && ink < pixels.Length * .85f, "The PNG alpha does not describe an isolated sticker silhouette.");
                        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) Relax(x, y, -1, -1);
                        for (int y = size - 1; y >= 0; y--) for (int x = size - 1; x >= 0; x--) Relax(x, y, 1, 1);
                        for (int k = 0; k < pixels.Length; k++)
                        {
                            Color32 p = pixels[k]; p.g = (byte)Mathf.RoundToInt(Mathf.Clamp01(distance[k] / 32) * 255);
                            p.b = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance[k] / bevelPixels)) * 255); pixels[k] = p;
                        }
                        data = new Texture2D(size, size, TextureFormat.RGBA32, false, true); data.SetPixels32(pixels); data.Apply();
                        byte[] png = data.EncodeToPNG(); string path = folder + "/" + Names[i] + "Data.png"; File.WriteAllBytes(path, png);
                        bytes.Write(png, 0, png.Length); ImportBrush(path);
                        void Relax(int x, int y, int dx, int dy)
                        {
                            int index = y * size + x;
                            Update(x + dx, y, 1); Update(x, y + dy, 1); Update(x + dx, y + dy, 1.41421356f); Update(x - dx, y + dy, 1.41421356f);
                            void Update(int nx, int ny, float cost)
                            { if (nx >= 0 && nx < size && ny >= 0 && ny < size) distance[index] = Mathf.Min(distance[index], distance[ny * size + nx] + cost); }
                        }
                    }
                    finally { Object.DestroyImmediate(source); if (data != null) Object.DestroyImmediate(data); }
                }
                File.WriteAllText(folder + "/Version.txt", ClayPreviewBuild.Hash(bytes.ToArray()) + "\n");
                AssetDatabase.ImportAsset(folder + "/Version.txt", ImportAssetOptions.ForceSynchronousImport);
            }
        }
        static void ImportBrush(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false; importer.isReadable = false; importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 512;
            var platform = importer.GetDefaultPlatformTextureSettings(); platform.overridden = false;
            platform.format = TextureImporterFormat.RGBA32; platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = 512; importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
        }
        internal static Texture2D[] Brushes(string set) => Names.Select(n => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + set + "/" + n + "Data.png")).ToArray();
        internal static LabWatercolorStampSettings Settings()
        { var settings = new LabWatercolorStampSettings { mode = LabPaintStampMode.ClaySticker, revealSeconds = 0 }; settings.Validate(); return settings; }
        static LabClayStickerRuntime NewVariant(int variant)
            => new LabClayStickerRuntime(Brushes(variant == 1 ? "OriginalBevel" : "RoundedBevel"),
                new LabClayStickerSettings { minimumDensity = .9f, maximumDensity = 1 });
        internal static void Finish(LabClayStickerRuntime runtime, LabPaintArtJournal journal, LabWatercolorStampSettings settings)
        {
            for (int i = 0; i < 4096; i++)
            {
                runtime.Update(journal, null, null, 1024, settings, i * .016, true);
                Require(runtime.Error == null, runtime.Error ?? "Comparison replay failed."); if (runtime.Ready) return;
                AsyncGPUReadback.WaitAllRequests();
            }
            throw new InvalidOperationException("Comparison replay exceeded its bounded Editor budget.");
        }
        internal static void Complete(LabClayVisualStudy.Fixture fixture)
        {
            const int samples = 256;
            for (int i = 0; i < samples && !fixture.Surface.IsComplete; i++)
            {
                float y = 1 - 2 * ((i + .5f) / samples), angle = i * 2.39996323f, r = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
                fixture.Hit(new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r), (LabOrbElement)(1 + i % 5), 1 + i % 5, .3f + (i % 4) * .2f, true);
            }
            Require(fixture.Surface.IsComplete, "The preserved real-rule fixture did not complete."); fixture.Seal();
        }
        public static string BakeAndCompare(string evidenceDirectory)
        {
            ClayPreviewBuild.RequireStopped(); Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A real GPU is required.");
            string evidence = Path.GetFullPath(evidenceDirectory), assets = Path.GetFullPath(Application.dataPath);
            Require(Path.IsPathRooted(evidenceDirectory) && evidence != assets && !evidence.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Keep evidence outside Assets.");
            Directory.CreateDirectory(evidence);
            string scenes = ClayPreviewBuild.SceneState(), random = JsonUtility.ToJson(UnityEngine.Random.state), oldSample = ExistingSampleHash();
            var source = Resources.Load<LabConfig>("LabConfig"); Require(source != null, "Saved LabConfig missing.");
            string sourceJson = EditorJsonUtility.ToJson(source), sourceHash = ClayPreviewBuild.Hash(Encoding.UTF8.GetBytes(sourceJson));
            var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                masks = Names.Select(n => JsonUtility.FromJson<MaskReport>(File.ReadAllText(Root + "/Masks/" + n + "MaskInfo.json"))).ToArray(),
                scope = "Isolated Editor comparison; three presentation dependencies replay one unchanged real-rule journal. No live session, game-default change, iOS or device/per-frame performance claim." };
            var settings = Settings(); var frames = new List<Texture2D>();
            try
            {
                using (var full = new LabClayVisualStudy.Fixture(source, settings, "standalone-frozen-five-color"))
                using (var originalV3 = NewVariant(1)) using (var roundedV3 = NewVariant(2))
                {
                    Complete(full); string authority = SurfaceHash(full.Surface), record = full.Journal.PrefixHash;
                    Require(full.Surface.PaintCount == 67, "The comparison's actual source fixture no longer has 67 hits; review instead of silently replacing its design.");
                    LabClayStickerRuntime[] runtimes = { full.Runtime, originalV3, roundedV3 };
                    for (int i = 0; i < runtimes.Length; i++)
                    {
                        long beforeBytes = RenderTextureBytes();
                        var timer = Stopwatch.StartNew(); if (i == 0) full.Finish(); else Finish(runtimes[i], full.Journal, settings); timer.Stop();
                        Require(SurfaceHash(full.Surface) == authority && full.Journal.PrefixHash == record, "Presentation changed the shared source map/journal.");
                        report.samples.Add(SaveSample(i, runtimes[i], settings, sourceHash, authority, record, full.Surface, timer.Elapsed.TotalMilliseconds,
                            RenderTextureBytes() - beforeBytes));
                        if (i == 0)
                        {
                            var original = JsonUtility.FromJson<ClayPreviewSampleInfo>(File.ReadAllText(ClayPreviewBuild.SampleInfoPath));
                            Require(original.authorityHash == authority && original.colorRawHash == report.samples[0].colorRawHash
                                && original.heightRawHash == report.samples[0].heightRawHash,
                                "The untouched v2 fixture no longer matches the preserved original sample; review instead of silently replacing it.");
                        }
                    }
                    report.authorityHash = authority; report.recordHash = record;
                    for (int i = 0; i < runtimes.Length; i++) using (var preview = Preview(i, runtimes[i]))
                    {
                        Texture2D warm = preview.Render(ThicknessFor(i), GlossFor(i), 0, FullViewZoom); Object.DestroyImmediate(warm);
                        var timer = Stopwatch.StartNew();
                        for (int frame = 0; frame < 5; frame++) { Texture2D sample = preview.Render(ThicknessFor(i), GlossFor(i), 0, FullViewZoom); Object.DestroyImmediate(sample); }
                        timer.Stop(); report.samples[i].warmRenderMeanMilliseconds = timer.Elapsed.TotalMilliseconds / 5;
                        float[] yaw = { 0, 90, 180, -90, 45, 45 };
                        for (int v = 0; v < yaw.Length; v++) frames.Add(preview.Render(ThicknessFor(i), GlossFor(i), yaw[v], FullViewZoom, v == 5 ? 40 : 0));
                        LabClayVisualStudy.Grid(evidence, "rounded-clay-six-views-" + Keys[i] + ".png", frames, 3); DestroyFrames(frames);
                    }
                    foreach (int i in Enumerable.Range(0, runtimes.Length)) using (var preview = Preview(i, runtimes[i])) frames.Add(preview.Render(ThicknessFor(i), GlossFor(i), 0, FullViewZoom));
                    LabClayVisualStudy.Grid(evidence, "rounded-clay-completed-three-presets.png", frames, 3); DestroyFrames(frames);
                    report.fixtures.Add(full.Check());
                }
                for (int f = 0; f < 4; f++)
                using (var fixture = new LabClayVisualStudy.Fixture(source, settings, report.fixtureRows[f]))
                using (var originalV3 = NewVariant(1)) using (var roundedV3 = NewVariant(2))
                {
                    fixture.Hit(Vector3.forward, LabOrbElement.Water, 1, .5f);
                    if (f == 1) for (int i = 0; i < 3; i++) fixture.Hit(Vector3.forward, LabOrbElement.Water, 1, .5f, true);
                    if (f >= 2) fixture.Hit(Quaternion.AngleAxis(12, Vector3.up) * Vector3.forward, f == 2 ? LabOrbElement.Water : LabOrbElement.Fire, 2, .5f, true);
                    fixture.Finish(); Finish(originalV3, fixture.Journal, settings); Finish(roundedV3, fixture.Journal, settings);
                    LabClayStickerRuntime[] runtimes = { fixture.Runtime, originalV3, roundedV3 };
                    for (int i = 0; i < runtimes.Length; i++) using (var preview = Preview(i, runtimes[i])) frames.Add(preview.Render(ThicknessFor(i), GlossFor(i), 0, DetailViewZoom));
                    report.fixtures.Add(fixture.Check());
                }
                LabClayVisualStudy.Grid(evidence, "rounded-clay-first-rehits-overlaps.png", frames, 3); DestroyFrames(frames);
                report.sourceConfigPreserved = sourceJson == EditorJsonUtility.ToJson(source);
                report.randomPreserved = random == JsonUtility.ToJson(UnityEngine.Random.state);
                report.scenesPreserved = scenes == ClayPreviewBuild.SceneState(); report.originalSamplePreserved = oldSample == ExistingSampleHash();
                Require(report.sourceConfigPreserved && report.randomPreserved && report.scenesPreserved && report.originalSamplePreserved, "The isolated experiment changed shared source state.");
                foreach (Sample sample in report.samples)
                {
                    sample.sourceConfigPreserved = report.sourceConfigPreserved; sample.randomPreserved = report.randomPreserved; sample.scenesPreserved = report.scenesPreserved;
                    SaveMetadata(sample);
                }
                string json = JsonUtility.ToJson(report, true); File.WriteAllText(Path.Combine(evidence, "rounded-clay-report.json"), json); return json;
            }
            finally { DestroyFrames(frames); }
        }
        static Sample SaveSample(int variant, LabClayStickerRuntime runtime, LabWatercolorStampSettings settings, string sourceHash,
            string authority, string record, LabPaintSurface surface, double elapsed, long measuredBytes)
        {
            string folder = Root + "/" + Keys[variant]; ClayPreviewBuild.EnsureFolder(folder);
            string colorPath = folder + "/Color.png", heightPath = folder + "/Height.exr";
            var info = new Sample { preset = Keys[variant], format = variant == 0 ? ClayPreviewSampleInfo.FormatVersion : "clay-preview-sample-v3",
                styleVersion = variant == 0 ? settings.Version : Version, sourceJournalStyle = settings.Version,
                unity = Application.unityVersion, createdUtc = DateTime.UtcNow.ToString("O"), sourceConfigHash = sourceHash,
                authorityHash = authority, recordHash = record, hits = surface.PaintCount, cells = surface.CoveredCount, totalCells = surface.CellCount,
                complete = surface.IsComplete,
                minimumDensity = variant == 0 ? .65f : .9f, maximumDensity = 1, bevelPixels = variant == 0 ? 10 : 20,
                replayMilliseconds = elapsed, measuredRenderTextureByteDelta = measuredBytes, estimatedRenderTextureBytes = 1024L * 512 * (3 * 4 + 3 * (runtime.HeightTexture.format == RenderTextureFormat.ARGBHalf ? 8 : 4)),
                brushVersion = variant == 0 ? LabClayStickerSettings.BakedBrushVersion : AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/" + (variant == 1 ? "OriginalBevel" : "RoundedBevel") + "/Version.txt").text.Trim() };
            Texture2D color = null, height = null;
            try
            {
                color = ClayPreviewBuild.ReadMap(runtime.PaintTexture, TextureFormat.RGBA32); height = ClayPreviewBuild.ReadMap(runtime.HeightTexture, TextureFormat.RGBAHalf);
                Require(color.GetPixels32().All(p => p.a == 255), "The completed comparison color has holes.");
                info.colorRawHash = ClayPreviewBuild.Hash(color.GetRawTextureData()); byte[] raw = height.GetRawTextureData();
                info.heightRawHash = ClayPreviewBuild.Hash(raw); var r = new byte[height.width * height.height * 2];
                for (int i = 0; i < r.Length / 2; i++) { r[i * 2] = raw[i * 8]; r[i * 2 + 1] = raw[i * 8 + 1]; }
                info.physicalHeightRawHash = ClayPreviewBuild.Hash(r);
                File.WriteAllBytes(colorPath, color.EncodeToPNG()); File.WriteAllBytes(heightPath, height.EncodeToEXR(Texture2D.EXRFlags.None));
                ClayPreviewBuild.ImportMap(colorPath, false, true); ClayPreviewBuild.ImportMap(heightPath, true, true);
                info.colorRoundtripMaxError = ClayPreviewBuild.Difference(color, AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
                info.heightRoundtripMaxError = ClayPreviewBuild.Difference(height, AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath));
                Require(info.colorRoundtripMaxError <= .000001f && info.heightRoundtripMaxError <= .00001f, "Comparison import changed GPU map precision.");
                ClayPreviewBuild.ImportMap(colorPath, false, false); ClayPreviewBuild.ImportMap(heightPath, true, false);
                info.colorFileHash = ClayPreviewBuild.Hash(File.ReadAllBytes(colorPath)); info.heightFileHash = ClayPreviewBuild.Hash(File.ReadAllBytes(heightPath));
                return info;
            }
            finally { if (color != null) Object.DestroyImmediate(color); if (height != null) Object.DestroyImmediate(height); }
        }
        static LabClayVisualStudy.Preview Preview(int variant, LabClayStickerRuntime runtime)
            => new LabClayVisualStudy.Preview(runtime.PaintTexture, runtime.HeightTexture,
                variant == 0 ? Resources.Load<Shader>(LabClayStickerRuntime.SurfaceShaderResource) : AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath));

        public static void CreateComparisonScene()
        {
            ClayPreviewBuild.RequireStopped(); RequireSavedExperimentScene();
            ClayPreviewBuild.EnsureFolder(Root + "/Scenes");
            Scene previous = SceneManager.GetActiveScene(), scene = default;
            string before = ClayPreviewBuild.SceneState(), build = ClayPreviewBuild.BuildScenesState();
            bool opened = false;
            try
            {
                scene = SceneManager.GetSceneByPath(ScenePath); opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene); // New temporary objects must never dirty the user's previously active Scene.
                var controllers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ClayPreviewController>(true)).ToArray();
                Require(controllers.Length <= 1, "The rounded experiment has multiple controllers.");
                ClayPreviewController controller;
                if (controllers.Length == 0)
                {
                    var root = new GameObject("Rounded Clay Comparison"); SceneManager.MoveGameObjectToScene(root, scene);
                    var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(ClayPreviewBuild.PanelSettingsPath);
                    Require(panel != null, "The preserved original preview panel settings are missing.");
                    root.AddComponent<UIDocument>().panelSettings = panel;
                    controller = root.AddComponent<ClayPreviewController>();
                }
                else controller = controllers[0];
                controller.sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPaintStudyRenderer.PrefabPath);
                controller.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
                controller.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ClayPreviewBuild.StylePath);
                Require(controller.sourcePrefab != null && controller.uiFont != null && controller.styleSheet != null, "The preserved preview model/font/style is missing.");
                controller.presets = Enumerable.Range(0, 3).Select(LoadPreset).ToArray(); controller.defaultPreset = 2;
                ClayPreviewPreset selected = controller.presets[2]; controller.colorMap = selected.colorMap; controller.heightMap = selected.heightMap;
                controller.sampleInfo = selected.sampleInfo; controller.sampleMetadataHash = selected.sampleMetadataHash; controller.clayShader = selected.shader;
                EditorUtility.SetDirty(controller); Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save the isolated rounded comparison Scene.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Require(before == ClayPreviewBuild.SceneState() && build == ClayPreviewBuild.BuildScenesState(), "Comparison Scene creation changed unrelated Scene/build state.");
        }
        internal static ClayPreviewPreset LoadPreset(int index)
        {
            string folder = Root + "/" + Keys[index]; var text = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/SampleInfo.json");
            Require(text != null, "Prepare the three frozen comparison maps first.");
            return new ClayPreviewPreset { label = Labels[index], colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Color.png"),
                heightMap = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Height.exr"), sampleInfo = text,
                sampleMetadataHash = ClayPreviewSampleInfo.ContentHash(text.text), improvedMaterial = index > 0,
                shader = index == 0 ? Resources.Load<Shader>(LabClayStickerRuntime.SurfaceShaderResource) : AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath) };
        }
        public static string ValidatePrepared()
        {
            ClayPreviewBuild.RequireStopped(); Require(File.Exists(ScenePath), "Prepare the separate rounded comparison Scene first.");
            Sample first = null;
            for (int i = 0; i < 3; i++)
            {
                ClayPreviewPreset preset = LoadPreset(i);
                Require(ClayPreviewSampleInfo.TryValidate(preset.sampleInfo.text, preset.sampleMetadataHash, out _, out string error), error);
                var sample = JsonUtility.FromJson<Sample>(preset.sampleInfo.text); string folder = Root + "/" + Keys[i];
                Require(sample.colorFileHash == ClayPreviewBuild.Hash(File.ReadAllBytes(folder + "/Color.png"))
                    && sample.heightFileHash == ClayPreviewBuild.Hash(File.ReadAllBytes(folder + "/Height.exr")), "The frozen comparison map changed after preparation.");
                ClayPreviewBuild.ValidateMap(folder + "/Color.png", false); ClayPreviewBuild.ValidateMap(folder + "/Height.exr", true);
                Require(preset.shader != null && preset.shader.isSupported && (i == 0 || preset.shader.name == "C6Lab/Experiments/RoundedClayStickerSurface"), "The comparison surface shader is missing.");
                Require(sample.hits == 67 && sample.seed == 123, "The preserved fixed hit fixture differs.");
                if (first == null) first = sample;
                else Require(sample.recordHash == first.recordHash && sample.authorityHash == first.authorityHash && sample.sourceConfigHash == first.sourceConfigHash,
                    "The three presentations do not share the exact same authority/journal.");
            }
            string[] normal = EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path)).Select(s => s.path).ToArray();
            Require(!EditorBuildSettings.scenes.Any(s => s.path.StartsWith(ClayPreviewBuild.Root + "/", StringComparison.Ordinal))
                && !AssetDatabase.GetDependencies(normal, true).Any(s => s.StartsWith(ClayPreviewBuild.Root + "/", StringComparison.Ordinal)), "Normal game Scenes reference the experiment.");
            ValidateSceneReferences();
            return JsonUtility.ToJson(new PreparedValidation { valid = true, presets = 3, recordHash = first.recordHash, authorityHash = first.authorityHash });
        }
        static void ValidateSceneReferences()
        {
            Scene previous = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded; string before = ClayPreviewBuild.SceneState();
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var controllers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ClayPreviewController>(true)).ToArray();
                Require(controllers.Length == 1 && controllers[0].presets != null && controllers[0].presets.Length == 3 && controllers[0].defaultPreset == 2, "The separate Scene must bind three presets and select the rounded one.");
                for (int i = 0; i < 3; i++)
                {
                    ClayPreviewPreset actual = controllers[0].presets[i], expected = LoadPreset(i);
                    Require(actual != null && actual.colorMap == expected.colorMap && actual.heightMap == expected.heightMap
                        && actual.sampleInfo == expected.sampleInfo && actual.sampleMetadataHash == expected.sampleMetadataHash
                        && actual.shader == expected.shader && actual.improvedMaterial == expected.improvedMaterial, "A Scene preset no longer matches its frozen data.");
                }
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Require(before == ClayPreviewBuild.SceneState(), "Read-only comparison validation changed an already loaded Scene.");
        }
        [MenuItem("C6 Lab/Experiments/Rounded Clay/Build Mac Comparison App")]
        public static void ScheduleBuild() { ClayPreviewBuild.RequireStopped(); EditorApplication.delayCall += BuildScheduled; }
        static void BuildScheduled()
        { try { Debug.Log("ROUNDED_CLAY_BUILD " + BuildMac()); } catch (Exception exception) { Debug.LogException(exception); } }
        public static string BuildMac() => ClayPreviewBuild.BuildMac(ScenePath, ValidatePrepared);
        static long RenderTextureBytes() => Resources.FindObjectsOfTypeAll<RenderTexture>()
            .Where(t => t != null && t.IsCreated()).Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
        static void SaveMetadata(Sample sample)
        {
            string path = Root + "/" + sample.preset + "/SampleInfo.json";
            File.WriteAllText(path, JsonUtility.ToJson(sample, true)); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        static void RequireSavedExperimentScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                Require(!open.isLoaded || !string.IsNullOrEmpty(open.path),
                    "Open an existing saved Scene (for example Assets/Experiments/ClayPreview/Scenes/ClayPreview.unity) before preparing the comparison. Unity cannot add a Scene while an Untitled Scene is open. Its unsaved contents were left untouched.");
            }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Require(!scene.IsValid() || !scene.isLoaded || !scene.isDirty, "Save/preserve unsaved rounded-preview Scene edits before explicit preparation.");
        }
        static string ExistingSampleHash() => ClayPreviewBuild.Hash(File.ReadAllBytes(ClayPreviewBuild.ColorPath)
            .Concat(File.ReadAllBytes(ClayPreviewBuild.HeightPath)).Concat(File.ReadAllBytes(ClayPreviewBuild.SampleInfoPath)).ToArray());
        internal static string SurfaceHash(LabPaintSurface surface)
        {
            var colors = new Color32[surface.CellCount]; var owners = new byte[surface.CellCount]; surface.CopyPixels(colors); surface.CopyPainterSlots(owners);
            return LabPaintPublisher.Hash(colors, owners);
        }
        static void DestroyFrames(List<Texture2D> frames) { foreach (var frame in frames) if (frame != null) Object.DestroyImmediate(frame); frames.Clear(); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
