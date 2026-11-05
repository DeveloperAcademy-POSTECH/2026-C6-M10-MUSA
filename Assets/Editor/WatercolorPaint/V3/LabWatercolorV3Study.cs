using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>
    /// Private Editor appearance experiment. V3 is an immutable rendering overlay on a
    /// source V2 journal, never a round style version or a claim of gameplay readiness.
    /// </summary>
    public sealed class LabWatercolorV3Study : IDisposable
    {
        public const string StampShaderPath = "Assets/Editor/WatercolorPaint/V3/Shaders/WatercolorStampV3.shader";
        public const string SurfaceShaderPath = "Assets/Editor/WatercolorPaint/V3/Shaders/GourdWatercolorV3.shader";
        public static readonly string[] ColumnNames = { "v2", "v3-cap-075", "v3-cap-085" };
        private static readonly Vector2[] Views = {
            Vector2.zero, new Vector2(90f, 0f), new Vector2(180f, 0f),
            new Vector2(-90f, 0f), new Vector2(45f, 0f), new Vector2(45f, 45f)
        };
        private static readonly LabOrbElement[] Palette = {
            LabOrbElement.Fire, LabOrbElement.Metal, LabOrbElement.Wood, LabOrbElement.Water, LabOrbElement.Earth
        };
        private readonly LabWatercolorStampStudy source;
        private readonly LabWatercolorStampSettings sourceSettings;
        private readonly LabWatercolorV3Snapshot[] variants;
        private readonly LabWatercolorStampRuntime[] runtimes = new LabWatercolorStampRuntime[2];
        private readonly LabPaintStudyRenderer[] renderers = new LabPaintStudyRenderer[3];
        private readonly Shader stampShader, surfaceShader;
        private bool disposed;

        public LabPaintSurface Surface => source.Surface;
        public LabPaintArtJournal SourceJournal => source.Journal;
        public Texture AuthorityMap => source.HighResolutionMap;
        public Texture AuthorityPainterMap => source.HighResolutionPainterMap;
        public Texture V2Map => source.CandidateMap;
        public LabWatercolorStampSettings SourceSettings => sourceSettings.Clone();

        /// <summary>Read-only display handles for the temporary Battle fixture. Column 0 is V2.</summary>
        public Texture GetDisplayMap(int column)
        {
            CheckPrepared();
            if (column < 0 || column > 2) throw new ArgumentOutOfRangeException(nameof(column));
            return Map(column);
        }
        public LabWatercolorV3Snapshot GetSnapshot(int column)
        {
            CheckDisposed();
            if (column < 0 || column > 2) throw new ArgumentOutOfRangeException(nameof(column));
            return column == 0 ? null : variants[column - 1];
        }

        public LabWatercolorV3Study(LabWatercolorV3Profile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            variants = new[] { profile.Snapshot(.75f), profile.Snapshot(.85f) };
            sourceSettings = variants[0].BaseStampSettings;
            sourceSettings.enabled = true;
            sourceSettings.Validate();
            stampShader = AssetDatabase.LoadAssetAtPath<Shader>(StampShaderPath);
            surfaceShader = AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath);
            if (stampShader == null || surfaceShader == null || !stampShader.isSupported || !surfaceShader.isSupported)
                throw new InvalidOperationException("Supported private V3 stamp and surface shaders are required.");
            try
            {
                source = new LabWatercolorStampStudy();
                renderers[0] = new LabPaintStudyRenderer(true);
                for (int i = 0; i < variants.Length; i++)
                {
                    LabWatercolorV3Snapshot value = variants[i];
                    runtimes[i] = CreateRuntime(value);
                    renderers[i + 1] = new LabPaintStudyRenderer(true, surfaceShader, value.ApplySurface);
                }
            }
            catch { Dispose(); throw; }
        }

        public void Prepare(LabWatercolorStampFixture fixture, int repeats, uint seed = 123u)
        {
            CheckDisposed();
            source.Prepare(fixture, repeats, seed, sourceSettings);
            string authority = LabWatercolorStampStudy.Hash(Surface);
            foreach (LabWatercolorStampRuntime runtime in runtimes)
                LabWatercolorStampStudy.Finish(runtime, SourceJournal, AuthorityMap, sourceSettings);
            Require(authority == LabWatercolorStampStudy.Hash(Surface), "V3 rendering changed source authority.");
        }

        public Texture2D[] RenderTriptych(float yaw = 0f, float pitch = 0f, float zoom = 1f)
        {
            CheckPrepared();
            var images = new Texture2D[3];
            try
            {
                for (int i = 0; i < images.Length; i++)
                    images[i] = renderers[i].RenderVisual(Map(i), yaw, zoom, pitch, sourceSettings);
                return images;
            }
            catch { Release(images); throw; }
        }

        public Texture2D[] RenderSixViews(int column)
        {
            CheckPrepared();
            if (column < 0 || column > 2) throw new ArgumentOutOfRangeException(nameof(column));
            var images = new Texture2D[Views.Length];
            try
            {
                for (int i = 0; i < images.Length; i++)
                    images[i] = renderers[column].RenderVisual(Map(column), Views[i].x, 1f, Views[i].y, sourceSettings);
                return images;
            }
            catch { Release(images); throw; }
        }

        public Texture2D CaptureStudyPng(int column)
        {
            CheckPrepared();
            if (column < 0 || column > 2) throw new ArgumentOutOfRangeException(nameof(column));
            return renderers[column].CaptureStudyPng(Map(column), sourceSettings);
        }

        private Texture Map(int column) => column == 0 ? source.CandidateMap : runtimes[column - 1].PaintTexture;
        private LabWatercolorStampRuntime CreateRuntime(LabWatercolorV3Snapshot value)
            => new LabWatercolorStampRuntime(stampShader, value.Brushes, value.ApplyStamp, value.Identity);

        [MenuItem("C6 Lab/Experiments/Watercolor V3 Comparison")]
        private static void ExportMenu()
        {
            string directory = EditorUtility.OpenFolderPanel("Watercolor V3 comparison evidence", "", "");
            if (string.IsNullOrEmpty(directory)) return;
            try
            {
                Export(directory);
                EditorUtility.RevealInFinder(Path.Combine(directory, "v3-study.json"));
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>Bounded saved-gourd comparisons. Every image comes from Unity, with no generated artwork.</summary>
        public static void Export(string directory, LabWatercolorV3Profile profile = null, bool includePalette = true)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute evidence directory is required.", nameof(directory));
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("The saved-gourd V3 study requires an Editor graphics device.");
            if (profile == null) profile = AssetDatabase.LoadAssetAtPath<LabWatercolorV3Profile>(LabWatercolorV3Baker.ProfilePath);
            if (profile == null) throw new InvalidOperationException("Bake the private V3 study profile before exporting comparisons.");
            Scene active = SceneManager.GetActiveScene();
            int sceneCount = SceneManager.sceneCount;
            bool dirty = active.isDirty;
            LabConfig config = Resources.Load<LabConfig>("LabConfig");
            string configBefore = JsonUtility.ToJson(config), profileBefore = EditorJsonUtility.ToJson(profile);
            const string defaultProfilePath = "Assets/Lab/Resources/GourdPaint/WatercolorStampProfile.asset";
            var defaultProfile = AssetDatabase.LoadAssetAtPath<LabWatercolorStampProfile>(defaultProfilePath);
            string defaultProfileBefore = EditorJsonUtility.ToJson(defaultProfile);
            List<FilePreservation> originalFiles = CapturePreservedFiles(config, defaultProfilePath);
            Directory.CreateDirectory(directory);
            var report = new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                classification = "Offline Editor GPU appearance study on a private copy of the saved gourd. V2 source journal and authority are shared by all columns. V3 identities describe private immutable rendering overlays only. No live-play, network, iOS or device-performance claim.",
                columnOrder = ColumnNames, sixViewOrder = LabWatercolorStampStudy.ViewNames,
                profileAsset = AssetDatabase.GetAssetPath(profile), stampShader = StampShaderPath, surfaceShader = SurfaceShaderPath,
                stampShaderSha256 = Hash(File.ReadAllBytes(StampShaderPath)), surfaceShaderSha256 = Hash(File.ReadAllBytes(SurfaceShaderPath)),
                authoringProfileSnapshot = profileBefore, defaultSourceProfileAsset = defaultProfilePath,
                preservedAssetFiles = originalFiles
            };
            using (var study = new LabWatercolorV3Study(profile))
            {
                report.sourceStyleVersion = study.sourceSettings.Version;
                report.sourceStampSettings = study.sourceSettings.Clone();
                report.variantVersions = study.variants.Select(v => v.Version).ToArray();
                report.bakedDataVersions = study.variants.Select(v => v.DataVersion).ToArray();
                report.variantConfigurations = study.variants.Select(v => new VariantConfiguration {
                    identity = v.Identity, bakedDataVersion = v.DataVersion, fingerprint = v.Fingerprint,
                    layerOpacityCap = v.LayerOpacityCap, washOpacity = v.WashOpacity, pigmentDepth = v.PigmentDepth,
                    poolStrength = v.PoolStrength, flowScale = v.FlowScale, rimWobble = v.RimWobble,
                    paperRelief = v.PaperRelief, paperFiberInfluence = v.PaperFiberInfluence,
                    paperBrightness = v.PaperBrightness, paperContrast = v.PaperContrast,
                    pigmentGrain = v.PigmentGrain, pigmentGrainScale = v.PigmentGrainScale,
                    paperTexture = AssetDatabase.GetAssetPath(v.PaperTexture),
                    brushes = v.Brushes.Select(AssetDatabase.GetAssetPath).ToArray()
                }).ToArray();
                var fixtures = new[] {
                    LabWatercolorStampFixture.BlueRehits, LabWatercolorStampFixture.BlueRehits,
                    LabWatercolorStampFixture.BlueRehits, LabWatercolorStampFixture.TwoColorOverlap,
                    LabWatercolorStampFixture.ThreeColorCross, LabWatercolorStampFixture.FiveColorComplete
                };
                int[] repeats = { 0, 3, 10, 3, 3, 0 };
                for (int i = 0; i < fixtures.Length; i++)
                {
                    study.Prepare(fixtures[i], repeats[i]);
                    string stem = fixtures[i].ToString().ToLowerInvariant() + "-first-plus-" + repeats[i];
                    var entry = new Entry { fixture = fixtures[i].ToString(), repeats = repeats[i],
                        sourceAuthorityHash = LabWatercolorStampStudy.Hash(study.Surface),
                        journalPrefixHash = study.SourceJournal.PrefixHash, sourceStyleVersion = study.SourceJournal.StyleVersion,
                        operations = study.SourceJournal.LastSequence, pages = study.SourceJournal.PageCount,
                        coveredCells = study.Surface.CoveredCount, coverage = study.Surface.Coverage,
                        rawJournalBytes = study.SourceJournal.RawBytes, triptych = stem + "-v2-v3-075-v3-085.png" };
                    entry.replays = study.VerifyReplays(directory, stem);
                    SaveTriptych(directory, stem, entry.triptych, study.RenderTriptych());
                    if (i == 0 || fixtures[i] == LabWatercolorStampFixture.FiveColorComplete)
                    {
                        entry.productionPngs = new string[3];
                        for (int column = 0; column < 3; column++)
                        {
                            Color32[] before = ReadRgba(study.Map(column));
                            string name = stem + "-production-" + ColumnNames[column] + ".png";
                            SaveAndRelease(directory, name, study.CaptureStudyPng(column));
                            Require(Equal(before, ReadRgba(study.Map(column))), "Production capture changed a study map.");
                            entry.productionPngs[column] = name;
                        }
                        entry.productionCapturePreservedMaps = true;
                    }
                    if (fixtures[i] == LabWatercolorStampFixture.FiveColorComplete)
                    {
                        entry.sixViews = new string[2];
                        for (int column = 1; column <= 2; column++)
                        {
                            string name = stem + "-" + ColumnNames[column] + "-six-views.png";
                            SaveGrid(directory, name, study.RenderSixViews(column), 3);
                            entry.sixViews[column - 1] = name;
                        }
                        entry.ablation = stem + "-v2paint-flatpaper-v3paint-originalpaper-v3paint-flatpaper.png";
                        SaveGrid(directory, entry.ablation, study.RenderAblations(), 3);
                    }
                    Require(entry.sourceAuthorityHash == LabWatercolorStampStudy.Hash(study.Surface),
                        "Comparison or PNG capture changed source colors/P bytes.");
                    entry.sourceAuthorityPreserved = true;
                    report.entries.Add(entry);
                }
                if (includePalette) study.ExportPalette(directory, report);
            }
            Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == sceneCount && active.isDirty == dirty,
                "The V3 study changed the active or loaded Scene state.");
            Require(JsonUtility.ToJson(config) == configBefore, "The V3 study changed saved LabConfig.");
            Require(EditorJsonUtility.ToJson(profile) == profileBefore, "The V3 study changed its authoring profile.");
            Require(EditorJsonUtility.ToJson(defaultProfile) == defaultProfileBefore, "The V3 study changed the default V2 source profile.");
            foreach (FilePreservation file in originalFiles)
            {
                file.afterSha256 = File.Exists(file.asset) ? Hash(File.ReadAllBytes(file.asset)) : "missing";
                file.equal = file.beforeSha256 == file.afterSha256;
                Require(file.equal, "The V3 comparison changed a preserved source asset: " + file.asset);
            }
            report.scenePreserved = report.configPreserved = report.profilePreserved = true;
            report.defaultSourceProfileUnchanged = report.fullAssetHashChecksEqual = true;
            File.WriteAllText(Path.Combine(directory, "v3-study.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_WATERCOLOR_V3_STUDY_EXPORTED cases=" + report.entries.Count + " palette=" + report.palette.Count
                + " classification=offline_Editor_GPU");
        }

        /// <summary>Focused exact-replay check. A mismatch retains byte maps and detailed evidence before throwing.</summary>
        public static void ExportReplayCase(string directory,
            LabWatercolorStampFixture fixture = LabWatercolorStampFixture.FiveColorComplete, int repeats = 0)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute evidence directory is required.", nameof(directory));
            Directory.CreateDirectory(directory);
            using (var study = new LabWatercolorV3Study(LabWatercolorV3Baker.LoadProfile()))
            {
                study.Prepare(fixture, repeats);
                string stem = fixture.ToString().ToLowerInvariant() + "-first-plus-" + repeats;
                ReplayEntry[] results = study.VerifyReplays(directory, stem);
                File.WriteAllText(Path.Combine(directory, stem + "-replay.json"),
                    JsonUtility.ToJson(new ReplayReport { fixture = stem, replays = results }, true));
            }
        }

        private ReplayEntry[] VerifyReplays(string directory, string fixture)
        {
            var results = new ReplayEntry[2];
            for (int i = 0; i < variants.Length; i++)
            {
                Color32[] expected = ReadRgba(runtimes[i].PaintTexture);
                var result = new ReplayEntry { identity = variants[i].Identity, rgbaHash = Hash(expected) };
                using (LabWatercolorStampRuntime replay = CreateRuntime(variants[i]))
                {
                    LabWatercolorStampStudy.Finish(replay, SourceJournal, AuthorityMap, sourceSettings);
                    RequireReplayEqual(expected, ReadRgba(replay.PaintTexture), directory, fixture,
                        variants[i], "full", replay, 0);
                    result.fullReplayRgbaEqual = true;
                }
                using (LabWatercolorStampRuntime incremental = CreateRuntime(variants[i]))
                {
                    int updates = 0;
                    while (incremental.AppliedThrough < SourceJournal.LastSequence)
                    {
                        ulong before = incremental.AppliedThrough;
                        incremental.Update(SourceJournal, AuthorityMap, 1024, sourceSettings, updates * .01d, false);
                        Require(incremental.Error == null && incremental.AppliedThrough > before,
                            "V3 incremental replay did not progress: " + incremental.Error);
                        Require(++updates <= 4096, "V3 incremental replay exceeded its private still-image bound.");
                    }
                    incremental.Update(SourceJournal, AuthorityMap, 1024, sourceSettings, updates * .01d + 3d, true);
                    RequireReplayEqual(expected, ReadRgba(incremental.PaintTexture), directory, fixture,
                        variants[i], "incremental", incremental, updates);
                    result.incrementalReplayRgbaEqual = true; result.incrementalUpdates = updates;
                }
                results[i] = result;
            }
            return results;
        }

        private void RequireReplayEqual(Color32[] expected, Color32[] actual, string directory,
            string fixture, LabWatercolorV3Snapshot variant, string mode, LabWatercolorStampRuntime runtime, int updates)
        {
            if (Equal(expected, actual)) return;
            var diagnostic = new ReplayDiagnostic {
                fixture = fixture, mode = mode, identity = variant.Identity, layerOpacityCap = variant.LayerOpacityCap,
                sourceAuthorityHash = LabWatercolorStampStudy.Hash(Surface), sourceJournalPrefixHash = SourceJournal.PrefixHash,
                sourceLastSequence = SourceJournal.LastSequence, appliedThrough = runtime.AppliedThrough,
                renderedThrough = runtime.RenderedThrough, incrementalUpdates = updates, drawCount = runtime.DrawCount,
                animating = runtime.IsAnimating, runtimeError = runtime.Error, columns = runtime.PaintTexture.width,
                rows = runtime.PaintTexture.height, expectedRgbaHash = Hash(expected), actualRgbaHash = Hash(actual),
                expectedPixels = expected.Length, actualPixels = actual.Length, firstChangedPixel = -1
            };
            for (int i = 0; i < Math.Min(expected.Length, actual.Length); i++)
            {
                int dr = Math.Abs(expected[i].r - actual[i].r), dg = Math.Abs(expected[i].g - actual[i].g);
                int db = Math.Abs(expected[i].b - actual[i].b), da = Math.Abs(expected[i].a - actual[i].a);
                int delta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                if (delta > 0)
                {
                    diagnostic.changedPixels++;
                    if (diagnostic.firstChangedPixel < 0)
                    {
                        diagnostic.firstChangedPixel = i;
                        diagnostic.firstExpected = expected[i].ToString(); diagnostic.firstActual = actual[i].ToString();
                    }
                }
                if (da > 0) diagnostic.alphaChangedPixels++;
                diagnostic.maxChannelDelta = Math.Max(diagnostic.maxChannelDelta, delta);
                diagnostic.maxAlphaDelta = Math.Max(diagnostic.maxAlphaDelta, da);
                diagnostic.totalChannelDelta += dr + dg + db + da;
            }
            string stem = fixture + "-cap-" + (variant.LayerOpacityCap * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                + "-" + mode + "-mismatch";
            diagnostic.expectedPng = stem + "-expected.png";
            diagnostic.actualPng = stem + "-actual.png";
            WriteRgba(directory, diagnostic.expectedPng, expected, diagnostic.columns, diagnostic.rows);
            WriteRgba(directory, diagnostic.actualPng, actual, diagnostic.columns, diagnostic.rows);
            File.WriteAllText(Path.Combine(directory, stem + ".json"), JsonUtility.ToJson(diagnostic, true));
            throw new InvalidOperationException("V3 " + mode + " replay changed final RGBA bytes: fixture=" + fixture
                + " cap=" + variant.LayerOpacityCap + " changed=" + diagnostic.changedPixels + "/" + expected.Length
                + " maxChannelDelta=" + diagnostic.maxChannelDelta + " alphaChanged=" + diagnostic.alphaChangedPixels
                + " maxAlphaDelta=" + diagnostic.maxAlphaDelta + " applied=" + runtime.AppliedThrough + "/" + SourceJournal.LastSequence
                + " updates=" + updates + ". Evidence: " + Path.Combine(directory, stem + ".json"));
        }

        private static void WriteRgba(string directory, string pngName, Color32[] pixels, int width, int height)
        {
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                image.SetPixels32(pixels); image.Apply(false, false);
                File.WriteAllBytes(Path.Combine(directory, pngName), image.EncodeToPNG());
                var bytes = new byte[pixels.Length * 4];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int p = i * 4; bytes[p] = pixels[i].r; bytes[p + 1] = pixels[i].g;
                    bytes[p + 2] = pixels[i].b; bytes[p + 3] = pixels[i].a;
                }
                File.WriteAllBytes(Path.Combine(directory, Path.GetFileNameWithoutExtension(pngName) + ".rgba"), bytes);
            }
            finally { Object.DestroyImmediate(image); }
        }

        private Texture2D[] RenderAblations()
        {
            var images = new Texture2D[3];
            try
            {
                // A/B panels keep the painted journal, full map readiness, camera and
                // source surface controls. Only the named paint/paper treatment changes.
                using (var renderer = new LabPaintStudyRenderer(true, null, material => {
                    material.SetTexture("_PaperTex", variants[0].PaperTexture);
                    material.SetFloat("_PaperBump", variants[0].PaperRelief);
                })) images[0] = renderer.RenderVisual(Map(0), 0f, 1f, 0f, sourceSettings);
                using (var renderer = new LabPaintStudyRenderer(true))
                    images[1] = renderer.RenderVisual(Map(1), 0f, 1f, 0f, sourceSettings);
                using (var renderer = new LabPaintStudyRenderer(true, surfaceShader, variants[0].ApplySurface))
                    images[2] = renderer.RenderVisual(Map(1), 0f, 1f, 0f, sourceSettings);
                return images;
            }
            catch { Release(images); throw; }
        }

        private void ExportPalette(string directory, Report report)
        {
            var sheets = new List<Texture2D>[] { new List<Texture2D>(), new List<Texture2D>(), new List<Texture2D>() };
            try
            {
                for (int first = 0; first < Palette.Length; first++)
                {
                    ExportPaletteTile(Palette[first], null, sheets, report);
                    for (int second = 0; second < Palette.Length; second++)
                        if (first != second) ExportPaletteTile(Palette[first], Palette[second], sheets, report);
                }
                report.paletteSheets = new string[3];
                for (int i = 0; i < sheets.Length; i++)
                {
                    string name = "palette-five-singles-twenty-ordered-pairs-" + ColumnNames[i] + ".png";
                    Texture2D sheet = LabWatercolorStampStudy.CombineGrid(sheets[i].ToArray(), 5);
                    SaveAndRelease(directory, name, sheet);
                    report.paletteSheets[i] = name;
                }
            }
            finally { foreach (List<Texture2D> frames in sheets) Release(frames.ToArray()); }
        }

        private void ExportPaletteTile(LabOrbElement first, LabOrbElement? second, List<Texture2D>[] sheets, Report report)
        {
            using (var fixture = new PaletteFixture(first, second, sourceSettings))
            {
                string before = LabWatercolorStampStudy.Hash(fixture.Surface);
                var entry = new PaletteEntry { tile = report.palette.Count, first = first.ToString(),
                    second = second?.ToString() ?? "none", sourceAuthorityHash = before,
                    operations = fixture.Journal.LastSequence, sourceStyleVersion = fixture.Journal.StyleVersion };
                sheets[0].Add(renderers[0].RenderVisual(fixture.V2.PaintTexture, 0f, 1f, 0f, sourceSettings));
                entry.rgbaHashes = new string[3]; entry.rgbaHashes[0] = Hash(ReadRgba(fixture.V2.PaintTexture));
                for (int i = 0; i < variants.Length; i++)
                using (LabWatercolorStampRuntime runtime = CreateRuntime(variants[i]))
                {
                    LabWatercolorStampStudy.Finish(runtime, fixture.Journal, fixture.Authority.PaintTexture, sourceSettings);
                    sheets[i + 1].Add(renderers[i + 1].RenderVisual(runtime.PaintTexture, 0f, 1f, 0f, sourceSettings));
                    entry.rgbaHashes[i + 1] = Hash(ReadRgba(runtime.PaintTexture));
                }
                Require(before == LabWatercolorStampStudy.Hash(fixture.Surface), "Palette rendering changed Host authority.");
                entry.sourceAuthorityPreserved = true;
                report.palette.Add(entry);
            }
        }

        private sealed class PaletteFixture : IDisposable
        {
            public readonly LabPaintSurface Surface;
            public readonly LabPaintArtJournal Journal;
            public readonly LabHighResolutionPaintRuntime Authority = new LabHighResolutionPaintRuntime();
            public readonly LabWatercolorStampRuntime V2 = new LabWatercolorStampRuntime();
            private Texture2D colors, owners;

            public PaletteFixture(LabOrbElement first, LabOrbElement? second, LabWatercolorStampSettings settings)
            {
                try
                {
                    LabConfig config = Resources.Load<LabConfig>("LabConfig");
                    if (config == null) throw new InvalidOperationException("Saved LabConfig is missing.");
                    LabPaintGrowthSettings rules = config.PaintGrowthSettings; rules.DryingEnabled = false;
                    Surface = new LabPaintSurface(rules, 123u);
                    Journal = new LabPaintArtJournal(Path.Combine(Path.GetTempPath(), "c6-v3-palette-" + Guid.NewGuid().ToString("N")),
                        "v3-palette-" + first + "-" + second, Surface.Columns, Surface.Rows, settings.Version);
                    Require(Journal.Error == null && Surface.AttachArtJournal(Journal), "Cannot attach the private palette journal.");
                    Require(Surface.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(first), 1, 1), "Palette hit was rejected.");
                    Surface.Advance(1d);
                    if (second.HasValue)
                    {
                        Require(Surface.RegisterHit(Quaternion.AngleAxis(12f, Vector3.up) * Vector3.forward,
                            LabPaintPalette.ColorFor(second.Value), 2, 2), "Palette overlap hit was rejected.");
                        Surface.Advance(1d);
                    }
                    colors = Upload(Surface, false); owners = Upload(Surface, true);
                    string hash = LabWatercolorStampStudy.Hash(Surface);
                    Require(Journal.Commit(Surface.Revision, hash) && Journal.Seal(Surface.Revision, hash), "Cannot seal palette journal.");
                    Require(Authority.Update(Surface, colors, owners, 1024, true), "Palette authority map is unavailable: " + Authority.Error);
                    LabWatercolorStampStudy.Finish(V2, Journal, Authority.PaintTexture, settings);
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                V2.Dispose(); Authority.Dispose(); Journal?.Dispose();
                if (colors != null) Object.DestroyImmediate(colors);
                if (owners != null) Object.DestroyImmediate(owners);
            }
        }

        private static Texture2D Upload(LabPaintSurface surface, bool painter)
        {
            var image = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true) {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            var pixels = new Color32[surface.CellCount];
            if (painter) {
                var slots = new byte[surface.CellCount]; surface.CopyPainterSlots(slots);
                for (int i = 0; i < slots.Length; i++) pixels[i] = new Color32(slots[i], 0, 0, 255);
            }
            else surface.CopyPixels(pixels);
            image.SetPixels32(pixels); image.Apply(false, false); return image;
        }

        private static Color32[] ReadRgba(Texture source)
        {
            if (source == null) throw new InvalidOperationException("A study GPU map is missing.");
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D copy = null;
            try {
                Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally {
                if (copy != null) Object.DestroyImmediate(copy);
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static string Hash(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++) {
                int p = i * 4; bytes[p] = pixels[i].r; bytes[p + 1] = pixels[i].g;
                bytes[p + 2] = pixels[i].b; bytes[p + 3] = pixels[i].a;
            }
            return Hash(bytes);
        }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static List<FilePreservation> CapturePreservedFiles(LabConfig config, string defaultProfilePath)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            Action<string> add = path => {
                if (File.Exists(path)) paths.Add(path);
                if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
            };
            foreach (string path in AssetDatabase.GetDependencies(LabPaintStudyRenderer.PrefabPath, true)) add(path);
            add(LabPaintStudyRenderer.ShaderPath);
            add("Assets/Lab/Scenes/Lab.unity");
            add(AssetDatabase.GetAssetPath(config));
            add(defaultProfilePath);
            foreach (string folder in new[] { LabWatercolorBrushBaker.MasterDirectory, LabWatercolorBrushBaker.OutputDirectory })
                if (Directory.Exists(folder))
                    foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) add(path);
            return paths.OrderBy(path => path, StringComparer.Ordinal).Select(path => new FilePreservation {
                asset = path, beforeSha256 = Hash(File.ReadAllBytes(path))
            }).ToList();
        }
        private static bool Equal(Color32[] a, Color32[] b) => a.Length == b.Length && a.SequenceEqual(b);
        private static void SaveTriptych(string directory, string stem, string comparisonName, Texture2D[] frames)
        {
            try {
                for (int i = 0; i < frames.Length; i++)
                    File.WriteAllBytes(Path.Combine(directory, stem + "-" + ColumnNames[i] + ".png"), frames[i].EncodeToPNG());
                SaveAndRelease(directory, comparisonName, LabPaintStudyRenderer.Combine(frames));
            }
            finally { Release(frames); }
        }
        private static void SaveGrid(string directory, string name, Texture2D[] frames, int columns)
        {
            try { SaveAndRelease(directory, name, LabWatercolorStampStudy.CombineGrid(frames, columns)); }
            finally { Release(frames); }
        }
        private static void SaveAndRelease(string directory, string name, Texture2D image)
        {
            try { File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG()); }
            finally { if (image != null) Object.DestroyImmediate(image); }
        }
        private static void Release(Texture2D[] images)
        { if (images != null) foreach (Texture2D image in images) if (image != null) Object.DestroyImmediate(image); }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private void CheckDisposed()
        { if (disposed) throw new ObjectDisposedException(nameof(LabWatercolorV3Study)); }
        private void CheckPrepared()
        {
            CheckDisposed();
            Require(SourceJournal != null && SourceJournal.IsSealed && SourceJournal.Error == null && V2Map != null,
                "Prepare a private sealed source fixture before rendering.");
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (LabWatercolorStampRuntime runtime in runtimes) runtime?.Dispose();
            foreach (LabPaintStudyRenderer renderer in renderers) renderer?.Dispose();
            source?.Dispose();
        }

        [Serializable] private sealed class Report
        {
            public string unity, gpu, classification, profileAsset, sourceStyleVersion, stampShader, surfaceShader;
            public string stampShaderSha256, surfaceShaderSha256;
            public string authoringProfileSnapshot, defaultSourceProfileAsset;
            public string[] columnOrder, sixViewOrder, variantVersions, bakedDataVersions, paletteSheets;
            public LabWatercolorStampSettings sourceStampSettings;
            public VariantConfiguration[] variantConfigurations;
            public string[] ablationOrder = { "v2paint-flatpaper", "v3paint-originalpaper", "v3paint-flatpaper" };
            public string ablationControls = "All panels share the same five-color journal, authority, camera, map readiness=1 and saved gourd geometry. First: V2 paint + V2 surface with V3 fine paper and PaperBump=V3 relief; second: V3 cap .75 paint + original V2 surface/paper; third: V3 cap .75 paint + V3 surface/fine paper. Other V2 controls remain original.";
            public string paletteOrder = "5x5 row-major. Each row starts with its single color, followed by its four ordered overlaps in Fire/Metal/Wood/Water/Earth order, excluding itself.";
            public bool scenePreserved, configPreserved, profilePreserved;
            public bool defaultSourceProfileUnchanged, fullAssetHashChecksEqual;
            public List<FilePreservation> preservedAssetFiles;
            public List<Entry> entries = new List<Entry>();
            public List<PaletteEntry> palette = new List<PaletteEntry>();
        }
        [Serializable] private sealed class FilePreservation
        {
            public string asset, beforeSha256, afterSha256;
            public bool equal;
        }
        [Serializable] private sealed class VariantConfiguration
        {
            public string identity, bakedDataVersion, paperTexture;
            public string[] brushes;
            public int fingerprint;
            public float layerOpacityCap, washOpacity, pigmentDepth, poolStrength, flowScale, rimWobble;
            public float paperRelief, paperFiberInfluence, pigmentGrain, pigmentGrainScale;
            public float paperBrightness, paperContrast;
        }
        [Serializable] private sealed class Entry
        {
            public string fixture, sourceAuthorityHash, journalPrefixHash, sourceStyleVersion, triptych, ablation;
            public string[] sixViews, productionPngs;
            public int repeats, coveredCells, pages;
            public float coverage;
            public ulong operations;
            public long rawJournalBytes;
            public bool sourceAuthorityPreserved, productionCapturePreservedMaps;
            public ReplayEntry[] replays;
        }
        [Serializable] private sealed class ReplayEntry
        {
            public string identity, rgbaHash;
            public bool fullReplayRgbaEqual, incrementalReplayRgbaEqual;
            public int incrementalUpdates;
        }
        [Serializable] private sealed class ReplayReport
        {
            public string fixture;
            public ReplayEntry[] replays;
        }
        [Serializable] private sealed class ReplayDiagnostic
        {
            public string fixture, mode, identity, sourceAuthorityHash, sourceJournalPrefixHash, runtimeError;
            public string expectedRgbaHash, actualRgbaHash, firstExpected, firstActual, expectedPng, actualPng;
            public float layerOpacityCap;
            public ulong sourceLastSequence, appliedThrough, renderedThrough;
            public int incrementalUpdates, drawCount, columns, rows, expectedPixels, actualPixels;
            public int changedPixels, alphaChangedPixels, maxChannelDelta, maxAlphaDelta, firstChangedPixel;
            public long totalChannelDelta;
            public bool animating;
        }
        [Serializable] private sealed class PaletteEntry
        {
            public int tile;
            public string first, second, sourceAuthorityHash, sourceStyleVersion;
            public ulong operations;
            public bool sourceAuthorityPreserved;
            public string[] rgbaHashes;
        }
    }
}
