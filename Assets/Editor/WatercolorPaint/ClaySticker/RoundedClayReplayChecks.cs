using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Stopped-Editor regression checks for the two opt-in presentation dependencies.</summary>
    public static class RoundedClayReplayChecks
    {
        static readonly string[] Sets = { "OriginalBevel", "RoundedBevel" };
        static readonly string[] Brushes = { "Broad", "Long", "Cluster" };

        [Serializable] public sealed class Variant
        {
            public string set, authorityHash, recordHash, colorHash, packedHeightHash;
            public int firstCells, threeRehitCells, boundedUpdates, captureUpdates, restoreUpdates;
            public ulong finalSequence;
        }

        [Serializable] public sealed class Result
        {
            public string unity, gpu;
            public string scope = "Actual stopped-Editor GPU replay contracts; not gameplay/device timing or visual approval.";
            public int checksPassed;
            public string[] checks;
            public List<Variant> variants = new List<Variant>();
        }

        public static string Run() => RunVariants(false);
        public static string RunCoating() => RunVariants(true);
        static string RunVariants(bool includeCoating)
        {
            ClayPreviewBuild.RequireStopped();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Rounded replay checks need a real graphics device.");
            var config = Resources.Load<LabConfig>("LabConfig");
            Require(config != null, "The saved LabConfig is missing.");
            string sourceConfig = EditorJsonUtility.ToJson(config), random = JsonUtility.ToJson(UnityEngine.Random.state);
            string scenes = ClayPreviewBuild.SceneState();
            var settings = new LabWatercolorStampSettings { mode = LabPaintStampMode.ClaySticker, revealSeconds = 0 };
            settings.Validate();
            string version = settings.Version, profile = JsonUtility.ToJson(settings);
            var checks = new List<string>();
            var result = new Result { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString() };

            using (var fixture = new LabClayVisualStudy.Fixture(config, settings, "rounded-replay-three-rehits"))
            {
                fixture.Hit(Vector3.forward, LabOrbElement.Water, 1, .5f);
                int firstCells = fixture.Surface.CoveredCount;
                for (int i = 0; i < 3; i++) fixture.Hit(Vector3.forward, LabOrbElement.Water, 1, .5f, true);
                Require(fixture.Surface.PaintCount == 4 && firstCells > 0 && fixture.Surface.CoveredCount > firstCells,
                    "The actual fixture must contain its first hit and three effective growth re-hits.");
                fixture.Seal(); fixture.Finish();
                string authority = SurfaceHash(fixture.Surface), record = fixture.Journal.PrefixHash;

                foreach (string set in includeCoating ? Sets.Concat(new[] { "CoatingV4" }) : Sets)
                {
                    string brushSet = set == "CoatingV4" ? "RoundedBevel" : set;
                    string[] paths = Brushes.Select(name => RoundedClayExperiment.Root + "/" + brushSet + "/" + name + "Data.png").ToArray();
                    Shader compositor = null;
                    if (set == "CoatingV4")
                    {
                        compositor = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Experiments/ClayPreview/Runtime/Shaders/StickerCoatingRuntime.shader");
                        Require(compositor != null && compositor.isSupported, "The v4 compositor is missing or unsupported.");
                    }
                    Texture2D[] brushes = paths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();
                    Require(brushes.All(texture => texture != null && texture.width == 512 && texture.height == 512),
                        "Prepare all three presentation brushes for " + set + " before running replay checks.");
                    string[] assetHashes = paths.Select(path => AssetDatabase.GetAssetDependencyHash(path).ToString()).ToArray();
                    Func<LabClayStickerRuntime> factory = () => new LabClayStickerRuntime(brushes,
                        new LabClayStickerSettings { minimumDensity = .9f, maximumDensity = 1f }, compositor);
                    var inherited = new List<string>();
                    LabClayRuntimeChecks.CheckErase(settings, inherited, factory);
                    LabClayRuntimeChecks.CheckOldStrokes(settings, inherited, factory);
                    checks.AddRange(inherited.Select(check => set + ": " + check));

                    using (var bounded = factory()) using (var capture = factory())
                    {
                        var entry = new Variant { set = set, firstCells = firstCells,
                            threeRehitCells = fixture.Surface.CoveredCount, authorityHash = authority, recordHash = record,
                            finalSequence = fixture.Journal.LastSequence };
                        entry.boundedUpdates = Finish(bounded, fixture.Journal, settings, false);
                        entry.captureUpdates = Finish(capture, fixture.Journal, settings, true);
                        byte[] color = Read(bounded.PaintTexture, TextureFormat.RGBA32);
                        byte[] packedHeight = Read(bounded.HeightTexture, TextureFormat.RGBAHalf);
                        Require(color.SequenceEqual(Read(capture.PaintTexture, TextureFormat.RGBA32)),
                            set + ": first-plus-three growth color RGBA differs between bounded and capture replay.");
                        Require(packedHeight.SequenceEqual(Read(capture.HeightTexture, TextureFormat.RGBAHalf)),
                            set + ": first-plus-three growth height/normal/coverage RGBA differs between replay budgets.");
                        Require(bounded.AppliedThrough == fixture.Journal.LastSequence && bounded.RenderedThrough == fixture.Journal.LastSequence
                            && capture.AppliedThrough == fixture.Journal.LastSequence && capture.RenderedThrough == fixture.Journal.LastSequence,
                            set + ": receipt/application/GPU completion must reach the same sealed sequence.");
                        checks.Add(set + ": actual first-plus-three re-hits match both replay budgets in full pigment/height RGBA");

                        bounded.HeightTexture.Release();
                        Require(!bounded.Ready, set + ": a lost packed height texture must invalidate readiness.");
                        entry.restoreUpdates = Finish(bounded, fixture.Journal, settings, false);
                        Require(color.SequenceEqual(Read(bounded.PaintTexture, TextureFormat.RGBA32))
                            && packedHeight.SequenceEqual(Read(bounded.HeightTexture, TextureFormat.RGBAHalf)),
                            set + ": resource reconstruction changed color, displacement, normals or coverage.");
                        Require(bounded.AppliedThrough == fixture.Journal.LastSequence && bounded.RenderedThrough == fixture.Journal.LastSequence,
                            set + ": reconstructed GPU artwork did not reach the sealed sequence.");
                        checks.Add(set + ": height-only resource loss restores identical pigment and all packed height channels");
                        entry.colorHash = ClayPreviewBuild.Hash(color); entry.packedHeightHash = ClayPreviewBuild.Hash(packedHeight);
                        result.variants.Add(entry);
                    }
                    Require(SurfaceHash(fixture.Surface) == authority && fixture.Journal.PrefixHash == record,
                        set + ": injected rendering changed the real source authority or journal.");
                    Require(paths.Select(path => AssetDatabase.GetAssetDependencyHash(path).ToString()).SequenceEqual(assetHashes),
                        set + ": replay mutated its imported presentation assets.");
                    fixture.Check();
                }
                checks.Add("both presentation overrides preserve the exact shared authority, source journal and brush assets");
            }
            Require(sourceConfig == EditorJsonUtility.ToJson(config) && random == JsonUtility.ToJson(UnityEngine.Random.state)
                && scenes == ClayPreviewBuild.SceneState() && profile == JsonUtility.ToJson(settings) && version == settings.Version,
                "Rounded replay checks changed saved configuration, source profile/version, Unity RNG or loaded Scenes.");
            checks.Add("saved config, round profile/version, Unity RNG and loaded Scenes remain unchanged");
            result.checks = checks.ToArray(); result.checksPassed = checks.Count;
            return JsonUtility.ToJson(result, true);
        }

        static int Finish(LabClayStickerRuntime runtime, LabPaintArtJournal journal, LabWatercolorStampSettings settings, bool capture)
        {
            for (int i = 0; i < 4096; i++)
            {
                RenderTexture previous = RenderTexture.active;
                try { runtime.Update(journal, null, null, 1024, settings, i * .016, capture); }
                finally { RenderTexture.active = previous; }
                Require(runtime.Error == null, runtime.Error ?? "Injected clay replay failed.");
                if (runtime.Ready) return i + 1;
                AsyncGPUReadback.WaitAllRequests();
            }
            throw new InvalidOperationException("Injected clay replay exceeded its bounded stopped-Editor budget.");
        }

        static byte[] Read(RenderTexture target, TextureFormat format)
        {
            Require(target != null && target.IsCreated(), "The injected renderer's completed GPU map is unavailable.");
            var pixels = new Texture2D(target.width, target.height, format, false, true) { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                pixels.Apply(false, false);
                return pixels.GetRawTextureData();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
        }

        static string SurfaceHash(LabPaintSurface surface)
        {
            var colors = new Color32[surface.CellCount]; var owners = new byte[surface.CellCount];
            surface.CopyPixels(colors); surface.CopyPainterSlots(owners); return LabPaintPublisher.Hash(colors, owners);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
