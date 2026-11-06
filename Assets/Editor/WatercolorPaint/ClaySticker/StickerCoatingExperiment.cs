using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hapio.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Append-only v4 experiment: preserves the already prepared v2/v3 comparison files.</summary>
    public static class StickerCoatingExperiment
    {
        public const string Folder = RoundedClayExperiment.Root + "/CoatingV4";
        public const string ScenePath = RoundedClayExperiment.Root + "/Scenes/CoatingClayPreview.unity";
        public const string RuntimeShaderPath = ClayPreviewBuild.Root + "/Runtime/Shaders/StickerCoatingRuntime.shader";
        public const string SurfaceShaderPath = ClayPreviewBuild.Root + "/Runtime/Shaders/StickerCoatingSurface.shader";
        public const string HeightChannels = "R:physical-height;GB:oct-object-normal;A:actual-coverage";
        const float Thickness = .014f, FullZoom = .68f, DetailZoom = 1.45f, CompleteDetailZoom = 1.3f;
        static readonly float[] GlossLevels = { 0, .35f, 1 };
        static readonly string[] OriginalKeys = { "OriginalV2", "OriginalV3", "RoundedV3" };
        [Serializable] public sealed class CoatingSample : ClayPreviewSampleInfo
        {
            public string preset, brushVersion, sourceJournalStyle, compositorSourceHash, surfaceSourceHash;
            public float minimumDensity, maximumDensity, bevelPixels;
            public double replayMilliseconds, warmRenderMeanMilliseconds;
            public long estimatedRenderTextureBytes, measuredRenderTextureByteDelta;
            public bool gpuFrameTimeMeasured;
            public string timingScope = "Measured stopped-Editor CPU wall time including bounded GPU completion waits; not a GPU profiler/frame-time measurement.";
            public string memoryScope = "Estimate: six renderer RT payloads. Measured delta: Unity Profiler-reported created-RT sizes; not a physical GPU-resident-memory measurement.";
            public string warmRenderScope = "Mean of five stopped-Editor synchronous render/readback wall times after one warm render; not gameplay CPU/GPU frame timing.";
        }
        [Serializable] public sealed class LightingMetadataRefresh
        {
            public string previousSurfaceSourceHash, currentSurfaceSourceHash;
            public bool metadataUpdated, mapsAndOriginalFilesPreserved, paletteMetadataUpdated;
            public bool renderingOrPerformanceRerun;
            public string scope = "Explicit surface-source rebind only. No map replay/bake or render/performance measurement was run; existing timing fields retain their original measurement scope.";
        }
        [Serializable] public sealed class Entry
        {
            public string fixture, authorityHash, recordHash, colorHash, reliefCoverageHash;
            public bool exactColor, exactReliefCoverage;
            public int hits, cells;
        }
        [Serializable] public sealed class ReflectionEntry
        { public string fixture, material; public float gloss, maximumLinear, meanLinear, mostNegativeLinear; }
        [Serializable] public sealed class PerformanceEntry
        { public string material; public double replayMilliseconds, warmRenderMeanMilliseconds; public long measuredRenderTextureByteDelta, estimatedRenderTextureBytes; public bool gpuFrameTimeMeasured; }
        [Serializable] public sealed class Report
        {
            public string unity, gpu, scope;
            public bool sourceConfigPreserved, randomPreserved, scenesPreserved, originalFilesPreserved;
            public float thickness = Thickness;
            public float[] glossColumns = GlossLevels;
            public string[] materialRows = { "Rounded v3", "Sticker-local coating v4" };
            public string reflectionMethod = "Subtraction in RGBAFloat readbacks from linear Half render targets. Both v3/v4 comparison rows use total(G)-total(0), cancelling unchanged model/rope/background. A separate v4 debug sheet uses SpecularOnly(G)-SpecularOnly(0). No exposure/brightness gain is applied. PNG encoding explicitly converts linear RGB to sRGB. Numeric comparisons use linear float/raw map data.";
            public string framing = "First/re-hit/separated/overlap rows are intentionally enlarged detail views. Completed front and six-view outputs retain full-object framing at zoom .68. The additional complete-detail sheets are fixed surface/upper-body crops at zoom 1.3; rope/base clipping there is intentional.";
            public float completeDetailZoom = CompleteDetailZoom;
            public CoatingSample sample;
            public List<Entry> fixtures = new List<Entry>();
            public List<ReflectionEntry> reflections = new List<ReflectionEntry>();
            public List<PerformanceEntry> performance = new List<PerformanceEntry>();
            public string performanceScope = "Both variants use equal RT dimensions/formats. Replay and five warm render/readback CPU wall times are measured in a stopped Editor; RT deltas use Unity Profiler-reported sizes. Payload byte counts are estimates. GPU frame timing is not measured.";
        }
        [MenuItem("C6 Lab/Experiments/Rounded Clay/Prepare Coating Comparison")]
        public static void PrepareMenu() => Debug.Log("STICKER_COATING_PREPARED " + PrepareCoatingComparison());
        public static string PrepareCoatingComparison()
        {
            ClayPreviewBuild.RequireStopped(); Preflight();
            RoundedClayExperiment.ValidatePrepared(); // Read-only; never invokes the three-sample baker.
            var preserved = PreservedFiles();
            string output = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp", "sticker-coating-comparison");
            string report = Export(output);
            CreateScene(); RequirePreserved(preserved); ValidatePrepared(); return report;
        }
        [MenuItem("C6 Lab/Experiments/Rounded Clay/Refresh Preview Lighting Metadata")]
        public static void RefreshPreviewLightingMetadataMenu()
            => Debug.Log("STICKER_COATING_LIGHTING_METADATA " + RefreshPreviewLightingMetadata());

        /// <summary>Explicit display-only rebind; Build remains strict and can never call this path.</summary>
        public static string RefreshPreviewLightingMetadata()
        {
            ClayPreviewBuild.RequireStopped(); Preflight();
            ValidatePreparedCore(false); // Only the surface source pin may be stale here.
            string path = Folder + "/SampleInfo.json";
            byte[] originalMetadata = File.ReadAllBytes(path), originalScene = File.ReadAllBytes(ScenePath);
            string originalJson = Encoding.UTF8.GetString(originalMetadata), oldMetadataHash = ClayPreviewSampleInfo.ContentHash(originalJson);
            var sample = JsonUtility.FromJson<CoatingSample>(originalJson);
            string surfaceHash = SourceHash(SurfaceShaderPath);
            var result = new LightingMetadataRefresh { previousSurfaceSourceHash = sample.surfaceSourceHash, currentSurfaceSourceHash = surfaceHash };
            var preserved = PreservedFiles();
            foreach (string key in OriginalKeys.Concat(new[] { "CoatingV4" }))
            foreach (string file in new[] { "Color.png", "Height.exr" })
            {
                string map = RoundedClayExperiment.Root + "/" + key + "/" + file;
                Snapshot(preserved, map); if (File.Exists(map + ".meta")) Snapshot(preserved, map + ".meta");
            }
            RefreshSceneMetadataPins(oldMetadataHash, oldMetadataHash, false);
            ClayPreviewPaletteBake.ValidateBoundData(); // Validate the old palette pin before changing V4 metadata.
            if (surfaceHash == sample.surfaceSourceHash)
            {
                ValidatePrepared(); RequirePreserved(preserved); result.mapsAndOriginalFilesPreserved = true;
                return JsonUtility.ToJson(result, true);
            }
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath);
            Require(!ShaderUtil.GetShaderMessages(shader).Any(message => message.severity.ToString() == "Error"), "The updated preview surface has shader compilation errors.");
            Material defaults = null;
            try
            {
                defaults = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                Require(defaults.HasProperty("_ClayPreviewLighting") && defaults.GetFloat("_ClayPreviewLighting") == 0,
                    "The display-only refresh requires preview lighting to be opt-in and OFF in the shader's production defaults.");
            }
            finally { if (defaults != null) Object.DestroyImmediate(defaults); }
            MatchCollection pins = Regex.Matches(originalJson, "\"surfaceSourceHash\"\\s*:\\s*\"(?<hash>[0-9a-f]{64})\"");
            Require(pins.Count == 1 && pins[0].Groups["hash"].Value == sample.surfaceSourceHash, "The existing surface pin is missing or ambiguous; explicitly prepare the sample instead.");
            Group pin = pins[0].Groups["hash"];
            string updatedJson = originalJson.Remove(pin.Index, pin.Length).Insert(pin.Index, surfaceHash);
            string nextMetadataHash = ClayPreviewSampleInfo.ContentHash(updatedJson);
            DateTime metadataTime = File.GetLastWriteTimeUtc(path), sceneTime = File.GetLastWriteTimeUtc(ScenePath);
            using (var paletteRebind = ClayPreviewPaletteBake.BeginLightingMetadataRebind(oldMetadataHash))
            try
            {
                // Replace the one hash value only: leave sample dates, measurements and all other JSON bytes alone.
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes(updatedJson));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                RefreshSceneMetadataPins(oldMetadataHash, nextMetadataHash, true);
                paletteRebind?.Apply(nextMetadataHash);
                ValidatePrepared(); RequirePreserved(preserved);
                result.metadataUpdated = true; result.mapsAndOriginalFilesPreserved = true;
                result.paletteMetadataUpdated = paletteRebind != null;
                string report = JsonUtility.ToJson(result, true);
                paletteRebind?.Complete();
                return report;
            }
            catch
            {
                // Restore owned files and only our two in-memory assignments; never save an unrelated Scene.
                File.WriteAllBytes(path, originalMetadata); File.WriteAllBytes(ScenePath, originalScene);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                File.SetLastWriteTimeUtc(path, metadataTime); File.SetLastWriteTimeUtc(ScenePath, sceneTime);
                Scene loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.IsValid() && loaded.isLoaded)
                {
                    var controller = loaded.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ClayPreviewController>(true)).SingleOrDefault();
                    if (controller != null && controller.presets != null && controller.presets.Length == 4)
                    { controller.presets[3].sampleMetadataHash = oldMetadataHash; controller.sampleMetadataHash = oldMetadataHash; }
                }
                paletteRebind?.Rollback();
                throw;
            }
        }
        static void Snapshot(Dictionary<string, Fingerprint> files, string path)
            => files[path] = new Fingerprint { hash = ClayPreviewBuild.Hash(File.ReadAllBytes(path)), writeTicks = File.GetLastWriteTimeUtc(path).Ticks };
        static void RefreshSceneMetadataPins(string previousHash, string nextHash, bool write)
        {
            Scene active = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            string before = ClayPreviewBuild.SceneState(), build = ClayPreviewBuild.BuildScenesState();
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                Require(!scene.isDirty, "Preserve unsaved coating Scene changes before refreshing the display pin.");
                var controller = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ClayPreviewController>(true)).Single();
                var metadata = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/SampleInfo.json");
                Require(controller.presets != null && controller.presets.Length == 4 && controller.defaultPreset == 3
                    && controller.presets[3].sampleInfo == metadata && controller.sampleInfo == metadata
                    && controller.presets[3].sampleMetadataHash == previousHash && controller.sampleMetadataHash == previousHash,
                    "The coating Scene's two existing metadata pins differ; explicitly prepare it instead of replacing other settings.");
                if (write)
                {
                    controller.presets[3].sampleMetadataHash = nextHash; controller.sampleMetadataHash = nextHash;
                    EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(scene);
                    Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save the coating Scene metadata pins.");
                }
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Require(before == ClayPreviewBuild.SceneState() && build == ClayPreviewBuild.BuildScenesState(), "Refreshing the pin changed unrelated loaded Scene/build state.");
        }

        public static string Export(string evidenceDirectory)
        {
            ClayPreviewBuild.RequireStopped();
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "A real graphics device is required.");
            string evidence = Path.GetFullPath(evidenceDirectory), assets = Path.GetFullPath(Application.dataPath);
            Require(Path.IsPathRooted(evidenceDirectory) && evidence != assets && !evidence.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Keep comparison evidence outside Assets.");
            Directory.CreateDirectory(evidence);
            var preserved = PreservedFiles(); string scenes = ClayPreviewBuild.SceneState(), random = JsonUtility.ToJson(UnityEngine.Random.state);
            var source = Resources.Load<LabConfig>("LabConfig"); Require(source != null, "Saved LabConfig is missing.");
            string sourceJson = EditorJsonUtility.ToJson(source); var expected = ExistingV3(); var settings = RoundedClayExperiment.Settings();
            Require(settings.Version == expected.sourceJournalStyle && ClayPreviewBuild.Hash(Encoding.UTF8.GetBytes(sourceJson)) == expected.sourceConfigHash,
                "The preserved sample's source settings changed; review instead of silently rebaking it.");
            var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                scope = "Stopped-Editor v3/v4 comparison on one unchanged real-rule journal. Existing sample files and three-preset Scene are immutable. No gameplay default, live session or device performance claim." };
            using (var full = new LabClayVisualStudy.Fixture(source, settings, "standalone-frozen-five-color"))
            using (var v3 = NewRuntime(expected, false)) using (var v4 = NewRuntime(expected, true))
            {
                RoundedClayExperiment.Complete(full);
                Require(full.Surface.PaintCount == 67 && RoundedClayExperiment.SurfaceHash(full.Surface) == expected.authorityHash
                    && full.Journal.PrefixHash == expected.recordHash, "The preserved seed123/67-hit authority or journal differs.");
                long oldMemory = RenderTextureBytes(); var oldTimer = Stopwatch.StartNew(); RoundedClayExperiment.Finish(v3, full.Journal, settings); oldTimer.Stop();
                report.performance.Add(new PerformanceEntry { material = "v3", replayMilliseconds = oldTimer.Elapsed.TotalMilliseconds,
                    measuredRenderTextureByteDelta = RenderTextureBytes() - oldMemory, estimatedRenderTextureBytes = EstimatedRtBytes(v3) });
                long memory = RenderTextureBytes(); var timer = Stopwatch.StartNew(); RoundedClayExperiment.Finish(v4, full.Journal, settings); timer.Stop();
                report.performance.Add(new PerformanceEntry { material = "v4", replayMilliseconds = timer.Elapsed.TotalMilliseconds,
                    measuredRenderTextureByteDelta = RenderTextureBytes() - memory, estimatedRenderTextureBytes = EstimatedRtBytes(v4) });
                Require(v3.PaintTexture.width == v4.PaintTexture.width && v3.PaintTexture.format == v4.PaintTexture.format
                    && v3.HeightTexture.format == v4.HeightTexture.format, "The v3/v4 RT payload formats differ.");
                Entry entry = Compare("complete", full, v3, v4); report.fixtures.Add(entry);
                Require(entry.colorHash == expected.colorRawHash, "V3 replay no longer matches the frozen v3 color map.");
                Texture2D oldHeight = ClayPreviewBuild.ReadMap(v3.HeightTexture, TextureFormat.RGBAHalf);
                try { Require(ClayPreviewBuild.Hash(oldHeight.GetRawTextureData()) == expected.heightRawHash, "V3 replay no longer matches the frozen v3 height map."); }
                finally { Object.DestroyImmediate(oldHeight); }
                report.sample = SaveSample(v4, expected, settings, full, timer.Elapsed.TotalMilliseconds, RenderTextureBytes() - memory);
                for (int variant = 0; variant < 2; variant++) using (var preview = MakePreview(variant == 0 ? v3 : v4, variant == 1))
                {
                    Texture2D warm = preview.Render(Thickness, .08f, 0, FullZoom); Object.DestroyImmediate(warm); timer.Restart();
                    for (int i = 0; i < 5; i++) { Texture2D frame = preview.Render(Thickness, .08f, 0, FullZoom); Object.DestroyImmediate(frame); }
                    timer.Stop(); report.performance[variant].warmRenderMeanMilliseconds = timer.Elapsed.TotalMilliseconds / 5;
                    if (variant == 1) report.sample.warmRenderMeanMilliseconds = report.performance[variant].warmRenderMeanMilliseconds;
                }
                ExportPair(evidence, "complete", v3, v4, FullZoom, report.reflections);
                ExportPair(evidence, "complete-detail", v3, v4, CompleteDetailZoom, report.reflections);
                for (int variant = 0; variant < 2; variant++) using (var preview = MakePreview(variant == 0 ? v3 : v4, variant == 1))
                foreach (float gloss in GlossLevels)
                {
                    var images = new List<Texture2D>();
                    try
                    {
                        float[] yaw = { 0, 90, 180, -90, 45, 45 };
                        for (int view = 0; view < 6; view++)
                        {
                            Texture2D linear = preview.Render(Thickness, gloss, yaw[view], FullZoom, view == 5 ? 40 : 0);
                            try { images.Add(Srgb(linear)); } finally { Object.DestroyImmediate(linear); }
                        }
                        LabClayVisualStudy.Grid(evidence, "coating-six-views-" + (variant == 0 ? "v3" : "v4") + "-gloss-" + GlossName(gloss) + ".png", images, 3);
                    }
                    finally { DestroyFrames(images); }
                }
                Require(RoundedClayExperiment.SurfaceHash(full.Surface) == expected.authorityHash && full.Journal.PrefixHash == expected.recordHash, "Rendering changed source authority/journal.");
            }
            string[] names = { "first", "first-plus-three", "two-separated", "same-color-overlap", "two-color-overlap" };
            for (int fixture = 0; fixture < names.Length; fixture++)
            using (var study = new LabClayVisualStudy.Fixture(source, settings, "coating-" + names[fixture]))
            using (var v3 = NewRuntime(expected, false)) using (var v4 = NewRuntime(expected, true))
            {
                study.Hit(fixture == 2 ? Quaternion.AngleAxis(-32, Vector3.up) * Vector3.forward : Vector3.forward, LabOrbElement.Water, 1, .5f);
                if (fixture == 1) for (int i = 0; i < 3; i++) study.Hit(Vector3.forward, LabOrbElement.Water, 1, .5f, true);
                if (fixture >= 2) study.Hit(Quaternion.AngleAxis(fixture == 2 ? 32 : 12, Vector3.up) * Vector3.forward,
                    fixture == 4 ? LabOrbElement.Fire : LabOrbElement.Water, 2, .5f, true);
                RoundedClayExperiment.Finish(v3, study.Journal, settings); RoundedClayExperiment.Finish(v4, study.Journal, settings);
                report.fixtures.Add(Compare(names[fixture], study, v3, v4)); ExportPair(evidence, names[fixture], v3, v4, DetailZoom, report.reflections);
            }
            report.sourceConfigPreserved = sourceJson == EditorJsonUtility.ToJson(source); report.randomPreserved = random == JsonUtility.ToJson(UnityEngine.Random.state);
            report.scenesPreserved = scenes == ClayPreviewBuild.SceneState(); RequirePreserved(preserved); report.originalFilesPreserved = true;
            Require(report.sourceConfigPreserved && report.randomPreserved && report.scenesPreserved, "The isolated comparison changed shared state.");
            report.sample.sourceConfigPreserved = true; report.sample.randomPreserved = true; report.sample.scenesPreserved = true;
            RequireShaderPins(report.sample);
            SaveMetadata(report.sample); string json = JsonUtility.ToJson(report, true); File.WriteAllText(Path.Combine(evidence, "sticker-coating-report.json"), json); return json;
        }
        static RoundedClayExperiment.Sample ExistingV3() => JsonUtility.FromJson<RoundedClayExperiment.Sample>(File.ReadAllText(RoundedClayExperiment.Root + "/RoundedV3/SampleInfo.json"));
        static LabClayStickerRuntime NewRuntime(RoundedClayExperiment.Sample sample, bool coating)
        {
            Shader compositor = coating ? AssetDatabase.LoadAssetAtPath<Shader>(RuntimeShaderPath) : null;
            Require(!coating || compositor != null && compositor.isSupported, "The v4 experiment compositor is unavailable.");
            return new LabClayStickerRuntime(RoundedClayExperiment.Brushes("RoundedBevel"), new LabClayStickerSettings
                { minimumDensity = sample.minimumDensity, maximumDensity = sample.maximumDensity }, compositor);
        }
        static LabClayVisualStudy.Preview MakePreview(LabClayStickerRuntime runtime, bool coating)
        {
            var preview = new LabClayVisualStudy.Preview(runtime.PaintTexture, runtime.HeightTexture,
                AssetDatabase.LoadAssetAtPath<Shader>(coating ? SurfaceShaderPath : RoundedClayExperiment.SurfaceShaderPath), true);
            preview.SetBackground(Color.black); return preview;
        }
        static Entry Compare(string name, LabClayVisualStudy.Fixture source, LabClayStickerRuntime v3, LabClayStickerRuntime v4)
        {
            Texture2D a = null, b = null, h3 = null, h4 = null;
            try
            {
                a = ClayPreviewBuild.ReadMap(v3.PaintTexture, TextureFormat.RGBA32); b = ClayPreviewBuild.ReadMap(v4.PaintTexture, TextureFormat.RGBA32);
                h3 = ClayPreviewBuild.ReadMap(v3.HeightTexture, TextureFormat.RGBAHalf); h4 = ClayPreviewBuild.ReadMap(v4.HeightTexture, TextureFormat.RGBAHalf);
                byte[] color = a.GetRawTextureData(), height = h3.GetRawTextureData();
                bool colorEqual = color.SequenceEqual((byte[])b.GetRawTextureData()), reliefEqual = ReliefCoverage(height).SequenceEqual(ReliefCoverage(h4.GetRawTextureData()));
                Require(colorEqual && reliefEqual, "V4 changed v3 color RGBA or physical-height/coverage bits at " + name);
                return new Entry { fixture = name, authorityHash = RoundedClayExperiment.SurfaceHash(source.Surface), recordHash = source.Journal.PrefixHash,
                    colorHash = ClayPreviewBuild.Hash(color), reliefCoverageHash = ClayPreviewBuild.Hash(ReliefCoverage(height)), exactColor = colorEqual,
                    exactReliefCoverage = reliefEqual, hits = source.Surface.PaintCount, cells = source.Surface.CoveredCount };
            }
            finally { foreach (var image in new[] { a, b, h3, h4 }) if (image != null) Object.DestroyImmediate(image); }
        }
        static byte[] ReliefCoverage(byte[] height)
        {
            var bytes = new byte[height.Length / 2];
            for (int i = 0; i < height.Length / 8; i++) { bytes[i * 4] = height[i * 8]; bytes[i * 4 + 1] = height[i * 8 + 1]; bytes[i * 4 + 2] = height[i * 8 + 6]; bytes[i * 4 + 3] = height[i * 8 + 7]; }
            return bytes;
        }
        static void ExportPair(string directory, string name, LabClayStickerRuntime v3, LabClayStickerRuntime v4, float zoom, List<ReflectionEntry> measured)
        {
            var colorFrames = new List<Texture2D>(); var reflections = new List<Texture2D>(); var debug = new List<Texture2D>();
            try
            {
                for (int variant = 0; variant < 2; variant++) using (var preview = MakePreview(variant == 0 ? v3 : v4, variant == 1))
                {
                    foreach (float gloss in GlossLevels)
                    {
                        Texture2D frame = preview.Render(Thickness, gloss, 0, zoom);
                        try { colorFrames.Add(Srgb(frame)); } finally { Object.DestroyImmediate(frame); }
                    }
                    preview.SetSpecularOnly(false); Texture2D zero = preview.Render(Thickness, 0, 0, zoom);
                    try
                    {
                        foreach (float gloss in GlossLevels)
                        {
                            Texture2D frame = preview.Render(Thickness, gloss, 0, zoom);
                            try
                            {
                                reflections.Add(DifferenceSrgb(frame, zero, out float maximum, out float mean, out float negative));
                                Require(gloss != 0 || maximum <= .000001f, "Zero gloss generated a nonzero reflection difference.");
                                measured.Add(new ReflectionEntry { fixture = name, material = variant == 0 ? "v3" : "v4", gloss = gloss,
                                    maximumLinear = maximum, meanLinear = mean, mostNegativeLinear = negative });
                            }
                            finally { Object.DestroyImmediate(frame); }
                        }
                    }
                    finally { Object.DestroyImmediate(zero); }
                    if (variant == 1)
                    {
                        preview.SetSpecularOnly(true); zero = preview.Render(Thickness, 0, 0, zoom);
                        try
                        {
                            foreach (float gloss in GlossLevels)
                            {
                                Texture2D frame = preview.Render(Thickness, gloss, 0, zoom);
                                try { debug.Add(DifferenceSrgb(frame, zero, out _, out _, out _)); }
                                finally { Object.DestroyImmediate(frame); }
                            }
                        }
                        finally { Object.DestroyImmediate(zero); }
                    }
                }
                LabClayVisualStudy.Grid(directory, "coating-" + name + "-color.png", colorFrames, 3);
                LabClayVisualStudy.Grid(directory, "coating-" + name + "-reflection-only.png", reflections, 3);
                LabClayVisualStudy.Grid(directory, "coating-" + name + "-v4-debug-reflection.png", debug, 3);
            }
            finally { DestroyFrames(colorFrames); DestroyFrames(reflections); DestroyFrames(debug); }
        }
        static Texture2D DifferenceSrgb(Texture2D a, Texture2D zero, out float maximum, out float mean, out float negative)
        {
            Color[] pixels = a.GetPixels(), baseline = zero.GetPixels(); maximum = 0; negative = 0; double total = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                RequireFinite(pixels[i]); RequireFinite(baseline[i]);
                Color delta = pixels[i] - baseline[i];
                negative = Math.Min(negative, Math.Min(delta.r, Math.Min(delta.g, delta.b)));
                maximum = Math.Max(maximum, Math.Max(delta.r, Math.Max(delta.g, delta.b)));
                pixels[i] = new Color(Math.Max(0, delta.r), Math.Max(0, delta.g), Math.Max(0, delta.b), 1);
                total += pixels[i].r + pixels[i].g + pixels[i].b;
            }
            mean = (float)(total / (pixels.Length * 3)); return Srgb(pixels, a.width, a.height);
        }
        static Texture2D Srgb(Texture2D linear) => Srgb(linear.GetPixels(), linear.width, linear.height);
        static Texture2D Srgb(Color[] linear, int width, int height)
        {
            for (int i = 0; i < linear.Length; i++)
            { RequireFinite(linear[i]); linear[i] = new Color(Mathf.LinearToGammaSpace(Math.Max(0, linear[i].r)), Mathf.LinearToGammaSpace(Math.Max(0, linear[i].g)), Mathf.LinearToGammaSpace(Math.Max(0, linear[i].b)), 1); }
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
            image.SetPixels(linear); image.Apply(); return image;
        }
        static CoatingSample SaveSample(LabClayStickerRuntime runtime, RoundedClayExperiment.Sample previous,
            LabWatercolorStampSettings settings, LabClayVisualStudy.Fixture source, double elapsed, long memory)
        {
            ClayPreviewBuild.EnsureFolder(Folder); var info = new CoatingSample
            {
                preset = "CoatingV4", format = "clay-preview-sample-v4", styleVersion = "clay-stickers-v4", heightChannels = HeightChannels,
                compositorSourceHash = SourceHash(RuntimeShaderPath), surfaceSourceHash = SourceHash(SurfaceShaderPath),
                sourceJournalStyle = settings.Version, sourceConfigHash = previous.sourceConfigHash, authorityHash = previous.authorityHash, recordHash = previous.recordHash,
                unity = Application.unityVersion, createdUtc = DateTime.UtcNow.ToString("O"), seed = previous.seed, hits = source.Surface.PaintCount, cells = source.Surface.CoveredCount,
                totalCells = source.Surface.CellCount, complete = source.Surface.IsComplete, minimumDensity = previous.minimumDensity, maximumDensity = previous.maximumDensity,
                brushVersion = previous.brushVersion, bevelPixels = previous.bevelPixels, replayMilliseconds = elapsed, measuredRenderTextureByteDelta = memory,
                estimatedRenderTextureBytes = EstimatedRtBytes(runtime)
            };
            Texture2D color = null, height = null;
            try
            {
                color = ClayPreviewBuild.ReadMap(runtime.PaintTexture, TextureFormat.RGBA32); height = ClayPreviewBuild.ReadMap(runtime.HeightTexture, TextureFormat.RGBAHalf);
                byte[] raw = height.GetRawTextureData(), r = new byte[raw.Length / 4];
                for (int i = 0; i < raw.Length / 8; i++) { r[i * 2] = raw[i * 8]; r[i * 2 + 1] = raw[i * 8 + 1]; }
                info.colorRawHash = ClayPreviewBuild.Hash(color.GetRawTextureData()); info.heightRawHash = ClayPreviewBuild.Hash(raw); info.physicalHeightRawHash = ClayPreviewBuild.Hash(r);
                Require(info.colorRawHash == previous.colorRawHash && info.physicalHeightRawHash == previous.physicalHeightRawHash, "V4 changed the preserved v3 color or physical height.");
                string colorPath = Folder + "/Color.png", heightPath = Folder + "/Height.exr";
                File.WriteAllBytes(colorPath, color.EncodeToPNG()); File.WriteAllBytes(heightPath, height.EncodeToEXR(Texture2D.EXRFlags.None));
                ClayPreviewBuild.ImportMap(colorPath, false, true); ClayPreviewBuild.ImportMap(heightPath, true, true);
                info.colorRoundtripMaxError = ClayPreviewBuild.Difference(color, AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
                info.heightRoundtripMaxError = ClayPreviewBuild.Difference(height, AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath));
                Require(info.colorRoundtripMaxError <= .000001f && info.heightRoundtripMaxError <= .00001f, "V4 sample import lost precision.");
                ClayPreviewBuild.ImportMap(colorPath, false, false); ClayPreviewBuild.ImportMap(heightPath, true, false);
                info.colorFileHash = ClayPreviewBuild.Hash(File.ReadAllBytes(colorPath)); info.heightFileHash = ClayPreviewBuild.Hash(File.ReadAllBytes(heightPath)); return info;
            }
            finally { if (color != null) Object.DestroyImmediate(color); if (height != null) Object.DestroyImmediate(height); }
        }
        static void SaveMetadata(CoatingSample sample)
        { string path = Folder + "/SampleInfo.json"; File.WriteAllText(path, JsonUtility.ToJson(sample, true)); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
        static ClayPreviewPreset LoadPreset()
        {
            var metadata = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/SampleInfo.json"); Require(metadata != null, "Prepare the coating sample explicitly first.");
            return new ClayPreviewPreset { label = "Sticker-local coating", colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Color.png"),
                heightMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Height.exr"), sampleInfo = metadata,
                sampleMetadataHash = ClayPreviewSampleInfo.ContentHash(metadata.text), shader = AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath), improvedMaterial = true };
        }
        static void CreateScene()
        {
            Preflight(); Scene previous = SceneManager.GetActiveScene(), scene = default; bool opened = false;
            string before = ClayPreviewBuild.SceneState(), build = ClayPreviewBuild.BuildScenesState();
            try
            {
                scene = SceneManager.GetSceneByPath(ScenePath); opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var controllers = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ClayPreviewController>(true)).ToArray(); Require(controllers.Length <= 1, "The coating Scene has multiple controllers.");
                ClayPreviewController controller;
                if (controllers.Length == 0)
                {
                    var root = new GameObject("Sticker-local Coating Comparison"); SceneManager.MoveGameObjectToScene(root, scene);
                    var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(ClayPreviewBuild.PanelSettingsPath); Require(panel != null, "The preserved preview panel settings are missing.");
                    root.AddComponent<UIDocument>().panelSettings = panel; controller = root.AddComponent<ClayPreviewController>();
                }
                else controller = controllers[0];
                controller.sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPaintStudyRenderer.PrefabPath);
                controller.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf"); controller.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ClayPreviewBuild.StylePath);
                Require(controller.sourcePrefab != null && controller.uiFont != null && controller.styleSheet != null, "The preserved model/font/style is missing.");
                controller.presets = Enumerable.Range(0, 3).Select(RoundedClayExperiment.LoadPreset).Concat(new[] { LoadPreset() }).ToArray(); controller.defaultPreset = 3;
                var selected = controller.presets[3]; controller.colorMap = selected.colorMap; controller.heightMap = selected.heightMap;
                controller.sampleInfo = selected.sampleInfo; controller.sampleMetadataHash = selected.sampleMetadataHash; controller.clayShader = selected.shader;
                EditorUtility.SetDirty(controller); Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save the separate coating Scene.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Require(before == ClayPreviewBuild.SceneState() && build == ClayPreviewBuild.BuildScenesState(), "Coating Scene preparation changed unrelated Scene/build state.");
        }
        public static string ValidatePrepared()
        {
            string validation = ValidatePreparedCore(true);
            ClayPreviewPaletteBake.ValidateBoundData();
            return validation;
        }
        // Explicitly rebuilding palette data must not depend on its previous, possibly stale pins.
        // The public build path still validates the optional bound dataset and never rebakes it.
        internal static string ValidatePreparedForPaletteBaking() => ValidatePreparedCore(true);
        static string ValidatePreparedCore(bool requireCurrentSurfacePin)
        {
            ClayPreviewBuild.RequireStopped(); RoundedClayExperiment.ValidatePrepared(); Require(File.Exists(ScenePath), "Prepare the separate coating Scene first.");
            var preset = LoadPreset(); Require(ClayPreviewSampleInfo.TryValidate(preset.sampleInfo.text, preset.sampleMetadataHash, out _, out string error), error);
            Require(preset.shader != null && preset.shader.isSupported && preset.shader.name == "C6Lab/Experiments/StickerCoatingSurface", "The v4 coating surface shader is missing or mismatched.");
            var sample = JsonUtility.FromJson<CoatingSample>(preset.sampleInfo.text); var original = ExistingV3();
            RequireShaderPins(sample, requireCurrentSurfacePin);
            Require(sample.recordHash == original.recordHash && sample.authorityHash == original.authorityHash && sample.sourceConfigHash == original.sourceConfigHash
                && sample.seed == original.seed && sample.hits == original.hits && sample.minimumDensity == original.minimumDensity && sample.maximumDensity == original.maximumDensity
                && sample.colorRawHash == original.colorRawHash && sample.physicalHeightRawHash == original.physicalHeightRawHash, "The coating sample does not preserve the frozen v3 design.");
            Require(sample.colorFileHash == ClayPreviewBuild.Hash(File.ReadAllBytes(Folder + "/Color.png")) && sample.heightFileHash == ClayPreviewBuild.Hash(File.ReadAllBytes(Folder + "/Height.exr")), "The v4 frozen files changed.");
            ClayPreviewBuild.ValidateMap(Folder + "/Color.png", false); ClayPreviewBuild.ValidateMap(Folder + "/Height.exr", true);
            ValidateScene(); return JsonUtility.ToJson(new RoundedClayExperiment.PreparedValidation { valid = true, presets = 4, recordHash = sample.recordHash, authorityHash = sample.authorityHash });
        }
        static void ValidateScene()
        {
            Scene previous = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
            string before = ClayPreviewBuild.SceneState();
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var controller = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ClayPreviewController>(true)).Single();
                Require(controller.presets != null && controller.presets.Length == 4 && controller.defaultPreset == 3, "The coating Scene must have four presets, defaulting to slot four.");
                for (int i = 0; i < 4; i++)
                {
                    var actual = controller.presets[i]; var expected = i < 3 ? RoundedClayExperiment.LoadPreset(i) : LoadPreset();
                    Require(actual != null && actual.colorMap == expected.colorMap && actual.heightMap == expected.heightMap && actual.sampleInfo == expected.sampleInfo
                        && actual.sampleMetadataHash == expected.sampleMetadataHash && actual.shader == expected.shader && actual.improvedMaterial == expected.improvedMaterial, "A coating Scene preset binding changed.");
                }
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Require(before == ClayPreviewBuild.SceneState(), "Read-only coating validation changed a loaded Scene.");
        }
        [MenuItem("C6 Lab/Experiments/Rounded Clay/Build Coating Mac App")]
        public static void ScheduleBuild() { ClayPreviewBuild.RequireStopped(); EditorApplication.delayCall += BuildScheduled; }
        static void BuildScheduled() { try { Debug.Log("STICKER_COATING_BUILD " + BuildMac()); } catch (Exception exception) { Debug.LogException(exception); } }
        public static string BuildMac() => ClayPreviewBuild.BuildMac(ScenePath, ValidatePrepared);
        static void Preflight()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isLoaded || !string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path), "Open a saved Scene before preparing the coating experiment; unsaved Untitled content is preserved.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath); Require(!scene.IsValid() || !scene.isLoaded || !scene.isDirty, "Save/preserve unsaved coating Scene changes before explicit preparation.");
        }
        sealed class Fingerprint { public string hash; public long writeTicks; }
        static Dictionary<string, Fingerprint> PreservedFiles()
        {
            var paths = OriginalKeys.SelectMany(key => new[] { "Color.png", "Height.exr", "SampleInfo.json" }.Select(file => RoundedClayExperiment.Root + "/" + key + "/" + file))
                .Concat(new[] { RoundedClayExperiment.ScenePath, ClayPreviewBuild.ColorPath, ClayPreviewBuild.HeightPath, ClayPreviewBuild.SampleInfoPath, ClayPreviewBuild.ScenePath });
            return paths.ToDictionary(path => path, path => new Fingerprint { hash = ClayPreviewBuild.Hash(File.ReadAllBytes(path)), writeTicks = File.GetLastWriteTimeUtc(path).Ticks });
        }
        static void RequirePreserved(Dictionary<string, Fingerprint> files)
        { foreach (var file in files) Require(file.Value.hash == ClayPreviewBuild.Hash(File.ReadAllBytes(file.Key)) && file.Value.writeTicks == File.GetLastWriteTimeUtc(file.Key).Ticks, "An original comparison file was changed: " + file.Key); }
        static string SourceHash(string path)
        { Require(File.Exists(path), "A coating shader source file is missing."); return ClayPreviewBuild.Hash(File.ReadAllBytes(path)); }
        static void RequireShaderPins(CoatingSample sample, bool requireCurrentSurfacePin = true)
        {
            Require(sample != null && sample.compositorSourceHash == SourceHash(RuntimeShaderPath)
                && sample.surfaceSourceHash != null && Regex.IsMatch(sample.surfaceSourceHash, "^[0-9a-f]{64}$")
                && (!requireCurrentSurfacePin || sample.surfaceSourceHash == SourceHash(SurfaceShaderPath)),
                "The coating shader source changed or is not pinned. Run Prepare Coating Comparison again; building never rebakes samples.");
        }
        static long EstimatedRtBytes(LabClayStickerRuntime runtime) => 1024L * 512 * (3 * 4 + 3 * (runtime.HeightTexture.format == RenderTextureFormat.ARGBHalf ? 8 : 4));
        static long RenderTextureBytes() => Resources.FindObjectsOfTypeAll<RenderTexture>().Where(t => t != null && t.IsCreated()).Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
        static string GlossName(float value) => value == 0 ? "0" : value == 1 ? "1" : "035";
        static void RequireFinite(Color value)
        { if (!float.IsFinite(value.r) || !float.IsFinite(value.g) || !float.IsFinite(value.b) || !float.IsFinite(value.a)) throw new InvalidOperationException("Non-finite linear reflection pixels; PNG clamping cannot hide an invalid render."); }
        static void DestroyFrames(List<Texture2D> frames) { foreach (var frame in frames) if (frame != null) Object.DestroyImmediate(frame); frames.Clear(); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
