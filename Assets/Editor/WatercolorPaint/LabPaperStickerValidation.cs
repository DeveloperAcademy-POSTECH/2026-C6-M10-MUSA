using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <summary>Real saved-gourd renders and chronological growth replay; no live session is changed.</summary>
    public static class LabPaperStickerValidation
    {
        [MenuItem("C6 Lab/Experiments/Paper Sticker Validation")]
        static void ExportMenu()
        {
            string directory = EditorUtility.OpenFolderPanel("Paper sticker evidence outside Assets", "", "");
            if (!string.IsNullOrEmpty(directory)) Export(directory);
        }

        public static void Export(string directory)
        {
            Require(!string.IsNullOrWhiteSpace(directory) && Path.IsPathRooted(directory), "Choose an absolute evidence directory.");
            directory = Path.GetFullPath(directory);
            string assets = Path.GetFullPath(Application.dataPath);
            Require(directory != assets && !directory.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.Ordinal),
                "Validation evidence must remain outside Assets.");
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling, "Run this comparison in the stopped, compiled Editor.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A real GPU is required.");
            var settings = LabWatercolorStampSettings.CaptureDefault();
            Require(settings.UsesPaperStickers && settings.enabled, "Select the approved opaque paper-sticker default before validating it.");
            settings.Validate();
            Shader shader = Resources.Load<Shader>(LabPaperStickerSettings.ShaderResource);
            Require(shader != null && shader.isSupported, "The actual paper-sticker shader is missing or unsupported.");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity.ToString() != "Error", "Paper-sticker shader error: " + message.message);

            Scene active = SceneManager.GetActiveScene();
            Scene[] loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            string[] sceneStates = loaded.Select(State).ToArray();
            string randomState = JsonUtility.ToJson(UnityEngine.Random.state);
            LabConfig config = Resources.Load<LabConfig>("LabConfig");
            Require(config != null, "The saved LabConfig is missing.");
            string configState = EditorJsonUtility.ToJson(config);
            var preserved = CaptureInputs(loaded);
            var report = new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(), profile = settings.Version,
                scope = "Actual Unity GPU renders of disposable saved WhiteGourd copies. First hit and first+3 re-hits use real RegisterHit plus Advance. FiveColorComplete uses the existing Fibonacci hit fixture with Advance after EVERY hit, including re-hit growth, not an initial-stamps-only synthetic completion. Private still fixture disables drying only. No iOS, Mac Player, live network, performance or aesthetic approval is implied.",
                comparison = "Left legacy Watercolor mode on its retained CPU Splatter contour; right PaperSticker on its approved new CPU contour. Gameplay area budgets are compared, while cell placement and authority hashes intentionally differ. Do not interpret cross-column hash difference as a regression or claim the two shapes share the same cell map.",
                fixtureSourceSha256 = Hash(File.ReadAllBytes("Assets/Editor/WatercolorPaint/LabWatercolorStampStudy.cs")),
                shaderSha256 = Hash(File.ReadAllBytes(AssetDatabase.GetAssetPath(shader))),
                preservedInputs = preserved
            };
            Directory.CreateDirectory(directory);
            var normal = new Texture2D[4]; var detail = new Texture2D[4];
            try
            {
                for (int row = 0; row < 2; row++)
                {
                    int repeats = row == 0 ? 0 : 3;
                    var legacy = new LabWatercolorStampSettings { mode = LabPaintStampMode.Watercolor, revealSeconds = 0f };
                    using (var old = new LabWatercolorStampStudy())
                    using (var candidate = new LabWatercolorStampStudy())
                    {
                        old.Prepare(LabWatercolorStampFixture.BlueRehits, repeats, 123u, legacy);
                        candidate.Prepare(LabWatercolorStampFixture.BlueRehits, repeats, 123u, settings);
                        Require(candidate.Surface.UsesPaperStickerContour, "The new contour was not installed in the actual candidate fixture.");
                        Require(old.Surface.CoveredCount == candidate.Surface.CoveredCount
                            && old.Surface.PaintCount == candidate.Surface.PaintCount
                            && old.Surface.PendingCoverage == candidate.Surface.PendingCoverage,
                            "The approved contour changed the configured first/re-hit budget.");
                        var entry = Validate(candidate, LabWatercolorStampFixture.BlueRehits, repeats, settings, directory,
                            "first-plus-" + repeats);
                        entry.budgetMatchesRetainedContour = true;
                        entry.legacyAuthorityHash = LabWatercolorStampStudy.Hash(old.Surface);
                        entry.geometryIntentionallyChanged = entry.legacyAuthorityHash != entry.surfaceHashBefore;
                        Require(entry.geometryIntentionallyChanged, "The new paper sticker contour did not change the selected cells.");
                        report.entries.Add(entry);
                        normal[row * 2] = CandidateFrame(old, LabWatercolorStampFixture.BlueRehits, repeats, legacy, 1f);
                        normal[row * 2 + 1] = CandidateFrame(candidate, LabWatercolorStampFixture.BlueRehits, repeats, settings, 1f);
                        detail[row * 2] = CandidateFrame(old, LabWatercolorStampFixture.BlueRehits, repeats, legacy, 1.55f);
                        detail[row * 2 + 1] = CandidateFrame(candidate, LabWatercolorStampFixture.BlueRehits, repeats, settings, 1.55f);
                        Save(directory, "first-plus-" + repeats + "-legacy.png", normal[row * 2]);
                        Save(directory, "first-plus-" + repeats + "-paper-sticker.png", normal[row * 2 + 1]);
                    }
                }
                SaveGrid(directory, "first-and-rehits-comparison.png", normal, 2);
                SaveGrid(directory, "first-and-rehits-detail.png", detail, 2);

                using (var full = new LabWatercolorStampStudy())
                {
                    full.Prepare(LabWatercolorStampFixture.FiveColorComplete, 0, 123u, settings);
                    Require(full.Surface.IsComplete && full.Surface.CoveredCount == full.Surface.CellCount,
                        "The actual advanced five-color surface did not reach 100%.");
                    var entry = Validate(full, LabWatercolorStampFixture.FiveColorComplete, 0, settings, directory, "five-color-complete");
                    Require(entry.translucentPixels == 0 && entry.opaquePixels == entry.outputPixels,
                        "A complete opaque surface still has transparent pixels.");
                    entry.allCompletePixelsOpaque = true;
                    Texture2D[] six = null;
                    Texture2D png = null;
                    try
                    {
                        six = full.RenderSixViews(true, LabWatercolorStampFixture.FiveColorComplete, 0, 123u, settings);
                        for (int i = 0; i < six.Length; i++) Save(directory, "full-" + LabWatercolorStampStudy.ViewNames[i] + ".png", six[i]);
                        SaveGrid(directory, "full-six-views.png", six, 3);
                        string authorityBefore = LabWatercolorStampStudy.Hash(full.Surface);
                        Color32[] imageBefore = Read(full.CandidateMap);
                        string journalBefore = full.Journal.PrefixHash; ulong sequenceBefore = full.Journal.LastSequence;
                        png = full.CaptureProductionPng(settings, Path.Combine(directory, "png-diagnostics"));
                        Require(png != null && png.GetPixels32().Any(pixel => pixel.a != 0), "The production PNG is empty.");
                        Require(Equal(imageBefore, Read(full.CandidateMap)) && authorityBefore == LabWatercolorStampStudy.Hash(full.Surface)
                            && journalBefore == full.Journal.PrefixHash && sequenceBefore == full.Journal.LastSequence,
                            "Production PNG capture changed the source artwork or gameplay map.");
                        Save(directory, "full-production-export.png", png);
                        entry.productionPngSourcePreserved = entry.productionPngCarrierVerified = true;
                    }
                    finally { Release(six); if (png != null) Object.DestroyImmediate(png); }
                    report.entries.Add(entry);
                }

                Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == loaded.Length,
                    "Validation changed the active/loaded Scenes.");
                for (int i = 0; i < loaded.Length; i++) Require(State(SceneManager.GetSceneAt(i)) == sceneStates[i], "Loaded Scene changed.");
                Require(randomState == JsonUtility.ToJson(UnityEngine.Random.state), "Validation changed Unity's global gameplay random state.");
                Require(configState == EditorJsonUtility.ToJson(config), "Validation changed the saved LabConfig object.");
                foreach (var item in preserved)
                {
                    item.after = Hash(File.ReadAllBytes(item.path)); item.equal = item.before == item.after;
                    Require(item.equal, "Validation changed a saved input: " + item.path);
                }
                report.scenePreserved = report.configPreserved = report.randomStatePreserved = report.savedInputsPreserved = true;
                report.complete = true;
            }
            finally
            {
                Release(normal); Release(detail);
                File.WriteAllText(Path.Combine(directory, "paper-sticker-validation.json"), JsonUtility.ToJson(report, true));
            }
        }

        static Entry Validate(LabWatercolorStampStudy study, LabWatercolorStampFixture fixture, int repeats,
            LabWatercolorStampSettings settings, string directory, string stem)
        {
            var timer = Stopwatch.StartNew();
            var entry = new Entry {
                fixture = fixture.ToString(), repeats = repeats, surfaceHashBefore = LabWatercolorStampStudy.Hash(study.Surface),
                coveredCells = study.Surface.CoveredCount, totalCells = study.Surface.CellCount,
                coverage = study.Surface.Coverage, paintCount = study.Surface.PaintCount,
                operationsBefore = study.Journal.LastSequence, journalHashBefore = study.Journal.PrefixHash,
                actualAdvanceCallsInFixture = fixture == LabWatercolorStampFixture.FiveColorComplete ? study.Surface.PaintCount : repeats,
                rawJournalBytes = study.Journal.RawBytes, pages = study.Journal.PageCount,
                pageCacheBytes = study.Journal.CachedPageBytes
            };
            Require(study.Journal.IsSealed && study.Journal.Error == null && study.Error == null, "Actual source journal is not sealed/healthy.");
            Require(study.AppliedThrough == study.Journal.LastSequence, "Some recorded cells have not been submitted to the GPU.");
            Color32[] candidate = Read(study.CandidateMap);
            Require(candidate.Length == 1024 * 512, "Unexpected output resolution.");
            entry.outputPixels = candidate.Length;
            foreach (Color32 pixel in candidate)
            {
                Require(pixel.r <= pixel.a + 1 && pixel.g <= pixel.a + 1 && pixel.b <= pixel.a + 1, "Invalid premultiplied output.");
                if (pixel.a == 255) entry.opaquePixels++;
                else if (pixel.a != 0) entry.translucentPixels++;
            }
            Require(entry.opaquePixels > 0, "No opaque sticker pixels were rendered.");
            entry.outputHash = Hash(candidate);
            SaveLinearMap(directory, stem + "-linear-map.png", candidate, study.CandidateMap.width, study.CandidateMap.height);
            var authority = new Color32[study.Surface.CellCount]; study.Surface.CopyPixels(authority);
            int scale = study.CandidateMap.width / study.Surface.Columns;
            for (int cell = 0; cell < authority.Length; cell++)
            {
                if (authority[cell].a == 0) continue;
                int x = cell % study.Surface.Columns, y = cell / study.Surface.Columns;
                int pixel = (y * scale + scale / 2) * study.CandidateMap.width + x * scale + scale / 2;
                Require(candidate[pixel].a != 0, "An actual paid growth cell has no pigment: " + cell);
            }
            entry.everyPaidCellVisible = true;
            using (var replay = new LabWatercolorStampRuntime())
            {
                LabWatercolorStampStudy.Finish(replay, study.Journal, study.HighResolutionMap, settings);
                Require(Equal(candidate, Read(replay.PaintTexture)), "Fresh full replay changed sticker output bytes.");
                entry.freshReplayExact = true;
            }
            using (var incremental = new LabWatercolorStampRuntime())
            {
                int updates = 0;
                while (incremental.AppliedThrough < study.Journal.LastSequence)
                {
                    ulong before = incremental.AppliedThrough;
                    incremental.Update(study.Journal, study.HighResolutionMap, 1024, settings, updates * .01d, false);
                    Require(incremental.Error == null && incremental.AppliedThrough > before,
                        "Bounded actual-journal replay did not progress: " + incremental.Error);
                    Require(++updates < 4096, "Bounded Editor replay exceeded its explicit limit.");
                }
                incremental.Update(study.Journal, study.HighResolutionMap, 1024, settings, 30d, true);
                Require(incremental.Error == null && Equal(candidate, Read(incremental.PaintTexture)),
                    "Update/page batching changed the retained opaque image.");
                entry.boundedReplayExact = true; entry.boundedUpdates = updates;
            }
            entry.surfaceHashAfter = LabWatercolorStampStudy.Hash(study.Surface);
            entry.operationsAfter = study.Journal.LastSequence; entry.journalHashAfter = study.Journal.PrefixHash;
            Require(entry.surfaceHashAfter == entry.surfaceHashBefore && entry.operationsAfter == entry.operationsBefore
                && entry.journalHashAfter == entry.journalHashBefore, "Rendering/replay mutated actual gameplay facts.");
            timer.Stop(); entry.validationMilliseconds = timer.ElapsedMilliseconds;
            entry.sourcePreserved = true;
            return entry;
        }

        static Texture2D CandidateFrame(LabWatercolorStampStudy study, LabWatercolorStampFixture fixture,
            int repeats, LabWatercolorStampSettings settings, float zoom)
        {
            Texture2D[] pair = study.Render(fixture, repeats, 123u, 0f, 0f, zoom, settings);
            Object.DestroyImmediate(pair[0]); return pair[1];
        }
        static string State(Scene scene) => scene.handle + ":" + scene.path + ":" + scene.isDirty;
        static List<Preserved> CaptureInputs(Scene[] loaded)
        {
            var paths = new HashSet<string>(AssetDatabase.GetDependencies(LabPaintStudyRenderer.PrefabPath, true), StringComparer.Ordinal);
            paths.Add("Assets/Lab/Resources/LabConfig.asset");
            paths.Add("Assets/Lab/Resources/GourdPaint/WatercolorStampProfile.asset");
            paths.Add("Assets/Lab/Resources/" + LabPaperStickerSettings.ShaderResource + ".shader");
            foreach (string path in Directory.GetFiles("Assets/Lab/Resources/GourdPaint/PaperSticker", "*", SearchOption.AllDirectories)) paths.Add(path);
            foreach (Scene scene in loaded) if (!string.IsNullOrEmpty(scene.path)) paths.Add(scene.path);
            foreach (string path in paths.ToArray()) if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
            return paths.Where(File.Exists).OrderBy(path => path, StringComparer.Ordinal).Select(path => new Preserved { path = path, before = Hash(File.ReadAllBytes(path)) }).ToList();
        }
        static void SaveGrid(string directory, string name, Texture2D[] frames, int columns)
        { Texture2D combined = null; try { combined = LabWatercolorStampStudy.CombineGrid(frames, columns); Save(directory, name, combined); } finally { if (combined != null) Object.DestroyImmediate(combined); } }
        static void Save(string directory, string name, Texture2D image) => File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
        static void SaveLinearMap(string directory, string name, Color32[] pixels, int width, int height)
        {
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try { image.SetPixels32(pixels); image.Apply(false, false); Save(directory, name, image); }
            finally { Object.DestroyImmediate(image); }
        }
        static Color32[] Read(Texture source)
        {
            Require(source != null, "A GPU map is missing.");
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            bool priorSrgb = GL.sRGBWrite; Texture2D copy = null;
            try
            {
                GL.sRGBWrite = false; Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally { GL.sRGBWrite = priorSrgb; if (copy != null) Object.DestroyImmediate(copy); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }
        static bool Equal(Color32[] a, Color32[] b) => a.Length == b.Length && a.SequenceEqual(b);
        static string Hash(Color32[] pixels)
        { var bytes = new byte[pixels.Length * 4]; for (int i = 0; i < pixels.Length; i++) { int offset = i * 4; bytes[offset] = pixels[i].r; bytes[offset + 1] = pixels[i].g; bytes[offset + 2] = pixels[i].b; bytes[offset + 3] = pixels[i].a; } return Hash(bytes); }
        static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Release(Texture2D[] images)
        { if (images != null) foreach (Texture2D image in images) if (image != null) Object.DestroyImmediate(image); }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [Serializable] sealed class Report
        {
            public string unity, gpu, profile, scope, comparison, fixtureSourceSha256, shaderSha256;
            public bool complete, scenePreserved, configPreserved, randomStatePreserved, savedInputsPreserved;
            public List<Preserved> preservedInputs;
            public List<Entry> entries = new List<Entry>();
        }
        [Serializable] sealed class Preserved { public string path, before, after; public bool equal; }
        [Serializable] sealed class Entry
        {
            public string fixture, surfaceHashBefore, surfaceHashAfter, journalHashBefore, journalHashAfter, outputHash, legacyAuthorityHash;
            public int repeats, coveredCells, totalCells, paintCount, actualAdvanceCallsInFixture, pages, pageCacheBytes, outputPixels, opaquePixels, translucentPixels, boundedUpdates;
            public float coverage;
            public ulong operationsBefore, operationsAfter;
            public long rawJournalBytes, validationMilliseconds;
            public bool sourcePreserved, everyPaidCellVisible, freshReplayExact, boundedReplayExact, budgetMatchesRetainedContour,
                geometryIntentionallyChanged, allCompletePixelsOpaque, productionPngSourcePreserved, productionPngCarrierVerified;
        }
    }
}
