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
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Explicit Editor GPU checks and saved-gourd pictures; not a device or live-network validation.</summary>
    public static class LabWatercolorStampValidation
    {
        public static void Export(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute evidence directory is required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Saved-gourd visual checks require a graphics device.");
            Scene active = SceneManager.GetActiveScene();
            int count = SceneManager.sceneCount; bool dirty = active.isDirty;
            LabConfig config = Resources.Load<LabConfig>("LabConfig");
            string configBefore = JsonUtility.ToJson(config);
            LabWatercolorBrushBaker.Validate();
            Directory.CreateDirectory(directory);
            var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                classification = "Offline Editor GPU fixture only. Before is V1-equivalent neutral stamp controls, after is V2; same saved gourd, camera, light, Host rules and full journal. Drying disabled only in the private still fixture. No iOS, live network or device performance claim." };
            var settings = LabWatercolorStampSettings.CaptureDefault(); settings.enabled = true;
            report.profile = settings.Version;
            foreach (LabWatercolorStampFixture fixture in Enum.GetValues(typeof(LabWatercolorStampFixture)))
            {
                int[] repeatCounts = fixture == LabWatercolorStampFixture.BlueRehits ? new[] { 0, 1, 3 } : new[] { 3 };
                foreach (int repeats in repeatCounts)
                {
                    var timer = Stopwatch.StartNew();
                    using (var study = new LabWatercolorStampStudy())
                    {
                        study.Prepare(fixture, repeats, 123u, settings);
                        string authorityBefore = LabWatercolorStampStudy.Hash(study.Surface);
                        Color32[] candidate = Read(study.CandidateMap);
                        var entry = new Entry { fixture = fixture.ToString(), repeats = repeats,
                            coveredCells = study.Surface.CoveredCount, coverage = study.Surface.Coverage,
                            operations = study.Journal.LastSequence, rawJournalBytes = study.Journal.RawBytes,
                            pages = study.Journal.PageCount, cacheBytes = study.Journal.CachedPageBytes,
                            surfaceHash = authorityBefore, outputHash = Hash(candidate) };
                        Require(study.Journal.IsSealed && study.Journal.Error == null, "The private journal was not sealed.");
                        Require(study.AppliedThrough == study.Journal.LastSequence, "The final operation was not applied.");
                        Require(candidate.Length == 1024 * 512, "Incorrect output dimensions.");
                        int visible = 0, translucent = 0;
                        foreach (Color32 p in candidate)
                        {
                            Require(p.r <= p.a + 1 && p.g <= p.a + 1 && p.b <= p.a + 1, "Non-premultiplied stamp output.");
                            if (p.a > 0) { visible++; if (p.a < 254) translucent++; }
                        }
                        Require(visible > 100 && translucent > 100, "Missing visible translucent watercolor.");
                        entry.visiblePixels = visible; entry.translucentPixels = translucent;
                        using (var replay = new LabWatercolorStampRuntime())
                        {
                            LabWatercolorStampStudy.Finish(replay, study.Journal, study.HighResolutionMap, settings);
                            Require(replay.Error == null && replay.AppliedThrough == study.Journal.LastSequence,
                                "Complete journal replay failed: " + replay.Error);
                            Require(Equal(candidate, Read(replay.PaintTexture)), "Full journal replay changed GPU bytes.");
                        }
                        using (var incremental = new LabWatercolorStampRuntime())
                        {
                            int updates = 0;
                            while (incremental.AppliedThrough < study.Journal.LastSequence)
                            {
                                ulong before = incremental.AppliedThrough;
                                incremental.Update(study.Journal, study.HighResolutionMap, 1024, settings, updates * .01d, false);
                                Require(incremental.Error == null && incremental.AppliedThrough > before,
                                    "Incremental fixture replay did not progress: " + incremental.Error);
                                Require(++updates <= 4096, "Incremental fixture replay exceeded its explicit Editor limit.");
                            }
                            incremental.Update(study.Journal, study.HighResolutionMap, 1024, settings, updates * .01d + 3d, true);
                            Require(Equal(candidate, Read(incremental.PaintTexture)),
                                "Different per-update operation/draw batching changed the final GPU bytes.");
                            entry.incrementalUpdates = updates; entry.batchReplayBytes = true;
                        }
                        Require(LabWatercolorStampStudy.Hash(study.Surface) == authorityBefore,
                            "Presentation changed Host colors or painter/P map bytes.");
                        Texture2D[] frames = null; Texture2D combined = null;
                        try
                        {
                            frames = study.RenderRefinement(fixture, repeats, 123u, 0f, 0f, 1f, settings);
                            combined = LabPaintStudyRenderer.Combine(frames);
                            string stem = fixture.ToString().ToLowerInvariant() + "-first-plus-" + repeats;
                            entry.image = stem + "-v1-v2-comparison.png";
                            File.WriteAllBytes(Path.Combine(directory, entry.image), combined.EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(directory, stem + "-v1-equivalent.png"), frames[0].EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(directory, stem + "-v2.png"), frames[1].EncodeToPNG());
                        }
                        finally { Release(frames); if (combined != null) Object.DestroyImmediate(combined); }
                        if (fixture != LabWatercolorStampFixture.BlueRehits || repeats == 3)
                        {
                            frames = null; combined = null;
                            try
                            {
                                frames = study.RenderSixViews(true, fixture, repeats, 123u, settings);
                                combined = LabWatercolorStampStudy.CombineGrid(frames);
                                entry.sixViews = fixture.ToString().ToLowerInvariant() + "-six-views.png";
                                File.WriteAllBytes(Path.Combine(directory, entry.sixViews), combined.EncodeToPNG());
                            }
                            finally { Release(frames); if (combined != null) Object.DestroyImmediate(combined); }
                        }
                        if (fixture == LabWatercolorStampFixture.FiveColorComplete)
                        {
                            Color32[] beforePng = Read(study.CandidateMap);
                            Texture2D png = null;
                            try
                            {
                                png = study.CaptureProductionPng(settings, directory);
                                Require(Equal(beforePng, Read(study.CandidateMap)), "Production PNG export changed the accumulated watercolor texture bytes.");
                                Require(LabWatercolorStampStudy.Hash(study.Surface) == authorityBefore,
                                    "Production PNG export changed the Host color/P map.");
                                entry.productionPng = "fivecolorcomplete-production-export.png";
                                entry.pngDiagnostics = "png-clone-diagnostic.txt";
                                File.WriteAllBytes(Path.Combine(directory, entry.productionPng), png.EncodeToPNG());
                                entry.pngClonePreservedV2SlotBlocks = entry.pngSourcePreserved = true;
                            }
                            finally { if (png != null) Object.DestroyImmediate(png); }
                        }
                        timer.Stop(); entry.fixtureAndRenderMilliseconds = timer.ElapsedMilliseconds;
                        entry.exactReplayBytes = entry.authorityPreserved = true;
                        report.entries.Add(entry);
                    }
                }
            }
            Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == count && active.isDirty == dirty,
                "Comparison changed the active or loaded Scene state.");
            Require(JsonUtility.ToJson(config) == configBefore, "Comparison changed the saved LabConfig.");
            report.scenePreserved = report.configPreserved = true;
            File.WriteAllText(Path.Combine(directory, "validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_WATERCOLOR_STAMPS_VALIDATED cases=" + report.entries.Count + " classification=offline_Editor_GPU");
        }

        /// <summary>Focused production PNG path check, without repeating the six-case replay suite.</summary>
        public static void ExportPngOnly(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute evidence directory is required.");
            Directory.CreateDirectory(directory);
            var settings = LabWatercolorStampSettings.CaptureDefault(); settings.enabled = true;
            using (var study = new LabWatercolorStampStudy())
            {
                study.Prepare(LabWatercolorStampFixture.FiveColorComplete, 3, 123u, settings);
                Color32[] before = Read(study.CandidateMap);
                string authority = LabWatercolorStampStudy.Hash(study.Surface);
                Texture2D png = null;
                try
                {
                    png = study.CaptureProductionPng(settings, directory);
                    Require(Equal(before, Read(study.CandidateMap)), "Production PNG changed the accumulated texture.");
                    Require(authority == LabWatercolorStampStudy.Hash(study.Surface), "Production PNG changed the Host map.");
                    File.WriteAllBytes(Path.Combine(directory, "fivecolorcomplete-production-export.png"), png.EncodeToPNG());
                    File.WriteAllText(Path.Combine(directory, "png-validation.txt"), "Production GourdPngCapture warmed material/per-slot V2 RGBA exact; source maps preserved. Initial-capture/repeat difference recorded separately in png-clone-diagnostic.txt.");
                }
                finally { if (png != null) Object.DestroyImmediate(png); }
            }
        }

        private static Color32[] Read(Texture source)
        {
            if (source == null) throw new InvalidOperationException("Missing GPU image.");
            RenderTexture old = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally { if (copy != null) Object.DestroyImmediate(copy); RenderTexture.active = old; RenderTexture.ReleaseTemporary(temporary); }
        }
        private static string Hash(Color32[] pixels)
        {
            var data = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            { int p = i * 4; data[p] = pixels[i].r; data[p + 1] = pixels[i].g; data[p + 2] = pixels[i].b; data[p + 3] = pixels[i].a; }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
        }
        private static bool Equal(Color32[] a, Color32[] b) => a.Length == b.Length && a.SequenceEqual(b);
        private static void Release(Texture2D[] frames)
        { if (frames != null) foreach (Texture2D image in frames) if (image != null) Object.DestroyImmediate(image); }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        [Serializable] private sealed class Report
        {
            public string unity, gpu, classification, profile;
            public int columns = 1024, rows = 512;
            public bool scenePreserved, configPreserved;
            public List<Entry> entries = new List<Entry>();
        }
        [Serializable] private sealed class Entry
        {
            public string fixture, surfaceHash, outputHash, image, sixViews, productionPng, pngDiagnostics;
            public int repeats, coveredCells, pages, cacheBytes, visiblePixels, translucentPixels, incrementalUpdates;
            public float coverage;
            public ulong operations;
            public long rawJournalBytes, fixtureAndRenderMilliseconds;
            public bool exactReplayBytes, batchReplayBytes, authorityPreserved, pngClonePreservedV2SlotBlocks, pngSourcePreserved;
        }
    }
}
