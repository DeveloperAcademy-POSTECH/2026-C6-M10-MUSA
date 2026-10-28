using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Explicit, disposable GPU checks for the offline blue study; never a live-game or device test.</summary>
    public static class LabWatercolorPaintValidation
    {
        public static void Export(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute evidence directory is required.", nameof(directory));
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Watercolor validation requires a real graphics device.");
            Scene active = SceneManager.GetActiveScene();
            int sceneCount = SceneManager.sceneCount, resources = OwnedResources(), roots = PreviewRoots();
            bool dirty = active.isDirty;
            LabConfig config = Resources.Load<LabConfig>("LabConfig");
            if (config == null) throw new InvalidOperationException("The saved LabConfig is missing.");
            string configBefore = JsonUtility.ToJson(config);
            string[] protectedPaths = {
                "Assets/Lab/Resources/LabConfig.asset", LabPaintStudyRenderer.PrefabPath,
                "Assets/Lab/Resources/GourdPaint/WhiteGourd.mat", LabPaintStudyRenderer.ShaderPath,
                "Assets/Lab/Resources/GourdPaint/Patterns/Splatter.png", "Assets/Hapio/Scenes/Battle.unity"
            };
            string[] originals = protectedPaths.Select(FileDigest).ToArray();
            Directory.CreateDirectory(directory);
            var report = new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                classification = "Offline blue static study on the saved gourd. No live battle, network, animation, iPhone or performance validation.",
                columns = 1024, rows = 512
            };
            var settings = new LabWatercolorPaintSettings();
            using (var study = new LabWatercolorPaintStudy())
            {
                foreach (HighResolutionHitLocation location in Enum.GetValues(typeof(HighResolutionHitLocation)))
                {
                    int[] repeats = location == HighResolutionHitLocation.Front ? new[] { 0, 1, 3 } : new[] { 3 };
                    study.HitDirection = LabHighResolutionPaintWindow.Direction(location);
                    foreach (int repeat in repeats)
                    {
                        study.Prepare(repeat, 123u, settings);
                        string authority = AuthorityState(study.Surface);
                        Color32[] current = Read(study.CurrentMap), candidate = Read(study.CandidateMap);
                        Entry entry = CheckMaps(current, candidate);
                        entry.location = location.ToString(); entry.rehits = repeat;
                        entry.coveredCells = study.Surface.CoveredCount;
                        entry.coverage = study.Surface.Coverage;
                        study.Prepare(repeat, 123u, settings);
                        Require(Equal(candidate, Read(study.CandidateMap)), "Identical replay changed the candidate GPU bytes.");
                        Require(Equal(current, Read(study.CurrentMap)), "Candidate replay changed the production baseline.");
                        Require(authority == AuthorityState(study.Surface), "Replay changed authoritative bytes, counters or descriptors.");
                        var alternate = new LabWatercolorPaintSettings { density = .7f, edgeStrength = 0f };
                        study.Prepare(repeat, 123u, alternate);
                        Require(authority == AuthorityState(study.Surface), "Appearance controls changed the authoritative surface.");
                        Require(Equal(current, Read(study.CurrentMap)), "Appearance controls changed the production baseline.");
                        Require(!Equal(candidate, Read(study.CandidateMap)), "Appearance controls did not change the candidate.");
                        study.Prepare(repeat, 123u, settings);
                        Require(Equal(candidate, Read(study.CandidateMap)), "Restoring controls did not restore the same GPU result.");
                        if (location == HighResolutionHitLocation.Front && repeat == 3)
                        {
                            var noEdge = new LabWatercolorPaintSettings { edgeStrength = 0f };
                            study.Prepare(repeat, 123u, noEdge);
                            Color32[] withoutEdge = Read(study.CandidateMap);
                            entry.edgeControlChangedPixels = candidate.Where((p, i) => !p.Equals(withoutEdge[i])).Count();
                            Require(entry.edgeControlChangedPixels > 100, "Edge control had no measurable optical contribution.");
                            study.Prepare(repeat, 123u, settings);
                        }
                        float yaw = location == HighResolutionHitLocation.LongitudeSeam ? 90f : 0f;
                        float pitch = location == HighResolutionHitLocation.NorthPole ? 90f : 0f;
                        Texture2D[] frames = null; Texture2D comparison = null;
                        try
                        {
                            frames = study.Render(repeat, 123u, yaw, pitch, 1f, settings);
                            Require(frames.Length == 2 && frames.All(frame => frame != null && frame.width == 512 && frame.height == 512),
                                "Comparison did not return two equally sized saved-gourd views.");
                            comparison = LabPaintStudyRenderer.Combine(frames);
                            string stem = location.ToString().ToLowerInvariant() + "-first-plus-" + repeat;
                            entry.image = stem + "-comparison.png";
                            File.WriteAllBytes(Path.Combine(directory, entry.image), comparison.EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(directory, stem + "-current.png"), frames[0].EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(directory, stem + "-candidate.png"), frames[1].EncodeToPNG());
                            Require(authority == AuthorityState(study.Surface), "Rendering modified the authoritative surface.");
                        }
                        finally
                        {
                            if (comparison != null) Object.DestroyImmediate(comparison);
                            if (frames != null) foreach (Texture2D frame in frames) if (frame != null) Object.DestroyImmediate(frame);
                        }
                        entry.replayDeterministic = entry.authorityPreserved = true;
                        report.entries.Add(entry);
                    }
                }
            }
            Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == sceneCount && active.isDirty == dirty,
                "The study changed the active/loaded Scene state.");
            Require(JsonUtility.ToJson(config) == configBefore, "The study changed the saved configuration.");
            for (int i = 0; i < protectedPaths.Length; i++)
                Require(FileDigest(protectedPaths[i]) == originals[i], "The study changed a protected source asset: " + protectedPaths[i]);
            Require(OwnedResources() == resources && PreviewRoots() == roots, "The study leaked an owned GPU resource or preview root.");
            report.configPreserved = report.scenePreserved = report.sourceAssetsPreserved = report.resourcesReleased = true;
            report.visualApproval = "PENDING: screenshots must be reviewed; numeric checks do not establish artistic quality.";
            File.WriteAllText(Path.Combine(directory, "validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_WATERCOLOR_STATIC_VALIDATED cases=" + report.entries.Count + " classification=offline_GPU_only");
        }

        private static Entry CheckMaps(Color32[] current, Color32[] candidate)
        {
            Require(current.Length == candidate.Length && current.Length == 1024 * 512, "Unexpected comparison map dimensions.");
            var entry = new Entry(); var alphaLevels = new HashSet<byte>();
            for (int i = 0; i < current.Length; i++)
            {
                Color32 source = current[i], p = candidate[i];
                Require(p.r <= p.a + 1 && p.g <= p.a + 1 && p.b <= p.a + 1, "Candidate pigment is not premultiplied.");
                Require((source.a == 0) == (p.a == 0), "Candidate changed the current optical support or introduced an alpha hole.");
                if (p.a == 0) { Require(p.r == 0 && p.g == 0 && p.b == 0, "Pigment exists outside current support."); continue; }
                entry.paintedPixels++; alphaLevels.Add(p.a);
                if (!p.Equals(source)) entry.changedPixels++;
                if (p.a < 255) entry.translucentPixels++;
                if (p.a > 32 && source.a > 64)
                {
                    float sourceMax = Math.Max(source.r, Math.Max(source.g, source.b));
                    float max = Math.Max(p.r, Math.Max(p.g, p.b));
                    if (sourceMax < 8 || max < 8) continue;
                    Require(Math.Abs(p.r / max - source.r / sourceMax) < .09f
                        && Math.Abs(p.g / max - source.g / sourceMax) < .09f
                        && Math.Abs(p.b / max - source.b / sourceMax) < .09f,
                        "Candidate changed the baseline pigment hue beyond quantization tolerance.");
                    entry.hueSamples++;
                }
            }
            entry.alphaLevels = alphaLevels.Count;
            Require(entry.paintedPixels > 100 && entry.changedPixels > 100 && entry.translucentPixels > 100,
                "Insufficient visible, changed and translucent pigment samples.");
            Require(entry.alphaLevels > 8 && entry.hueSamples > 100, "Insufficient density variation or reliable hue samples.");
            return entry;
        }

        private static string AuthorityState(LabPaintSurface surface)
        {
            var colors = new Color32[surface.CellCount]; var painters = new byte[surface.CellCount];
            surface.CopyPixels(colors); surface.CopyPainterSlots(painters);
            return LabPaintPublisher.Hash(colors, painters) + ":" + surface.Revision + ":" + surface.VisualVersion
                + ":" + surface.CoveredCount + ":" + surface.PaintCount + ":" + surface.PendingCoverage
                + ":" + LabPaintVisualStamp.Hash(LabPaintVisualStamp.Encode(surface.CopyVisualStamps()));
        }
        private static Color32[] Read(Texture texture)
        {
            var target = texture as RenderTexture;
            if (target == null || !target.IsCreated()) throw new InvalidOperationException("A live comparison RenderTexture is required.");
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true)
                { name = "Watercolor validation readback", hideFlags = HideFlags.HideAndDontSave };
            try
            {
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(copy); }
        }
        private static string FileDigest(string path) => LabPaintVisualStamp.Hash(File.ReadAllBytes(path));
        private static bool Equal(Color32[] left, Color32[] right) => left.SequenceEqual(right);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static int PreviewRoots() => Resources.FindObjectsOfTypeAll<GameObject>().Count(o => o.name == "Paint study — saved gourd copy");
        private static int OwnedResources() => Resources.FindObjectsOfTypeAll<Texture>().Count(o => o.name.StartsWith("Watercolor study ")
                || o.name == "Gourd continuous brush cache" || o.name == "Gourd high-resolution committed paint" || o.name == "Gourd high-resolution committed P numbers")
            + Resources.FindObjectsOfTypeAll<Material>().Count(o => o.name == "Watercolor study material");

        [Serializable] private sealed class Entry
        {
            public string location, image;
            public int rehits, coveredCells, paintedPixels, changedPixels, translucentPixels, alphaLevels, hueSamples, edgeControlChangedPixels;
            public float coverage;
            public bool replayDeterministic, authorityPreserved;
        }
        [Serializable] private sealed class Report
        {
            public string unity, gpu, classification, visualApproval;
            public int columns, rows;
            public bool configPreserved, scenePreserved, sourceAssetsPreserved, resourcesReleased;
            public List<Entry> entries = new List<Entry>();
        }
    }
}
