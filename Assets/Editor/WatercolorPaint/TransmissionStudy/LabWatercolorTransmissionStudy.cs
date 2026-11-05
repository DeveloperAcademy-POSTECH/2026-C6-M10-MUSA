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
    /// <summary>Disposable offline Editor appearance study; never installs a game default.</summary>
    public static class LabWatercolorTransmissionStudy
    {
        public const string ShaderPath = "Assets/Editor/WatercolorPaint/TransmissionStudy/WatercolorTransmission.shader";

        [MenuItem("C6 Lab/Experiments/Watercolor Transmission Comparison")]
        private static void ExportMenu()
        {
            string directory = EditorUtility.OpenFolderPanel("Watercolor transmission comparison", "", "");
            if (string.IsNullOrEmpty(directory)) return;
            try { Export(directory); EditorUtility.RevealInFinder(Path.Combine(directory, "transmission-study.json")); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>Returns and writes evidence only after the scoped preservation assertions pass.</summary>
        public static string Export(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("An absolute evidence directory is required.", nameof(directory));
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling,
                "Run this isolated appearance export while the Editor is stopped and compilation is complete.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "An Editor graphics device is required.");
            var profile = LabWatercolorV3Baker.LoadProfile();
            var snapshot = profile.Snapshot(.85f);
            snapshot.Validate();
            var settings = snapshot.BaseStampSettings; settings.enabled = true; settings.Validate();
            Shader baseline = AssetDatabase.LoadAssetAtPath<Shader>(LabWatercolorV3Study.StampShaderPath);
            Shader improved = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Shader surface = AssetDatabase.LoadAssetAtPath<Shader>(LabWatercolorV3Study.SurfaceShaderPath);
            Require(baseline != null && improved != null && surface != null && baseline.isSupported
                && improved.isSupported && surface.isSupported, "Supported comparison shaders are required.");

            Scene active = SceneManager.GetActiveScene();
            var sceneStates = Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
                Scene scene = SceneManager.GetSceneAt(i);
                return new SceneState { scene = scene, path = scene.path, dirty = scene.isDirty };
            }).ToArray();
            var preserved = CapturePreserved(profile, sceneStates);
            Directory.CreateDirectory(directory);
            var report = new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                scope = "Offline Editor appearance comparison on disposable copies of the saved gourd. Each row shares one sealed source journal, authority, camera and V3 surface. Assertions cover source color/P bytes, source GPU maps, journal identity, repeat/fresh final-map replay, saved asset bytes and selected authoring objects. Separate surface diagnostic keeps the baseline map fixed while disabling paper color/relief and grain/fiber independently; shell diffuse remains. No gameplay, network, mobile, performance or physical pigment-model validation.",
                columns = new[] { "baseline-v3-cap085", "density-transmission", "overlap-glazing" },
                surfaceModes = new[] { "actual-v3-paper", "neutral-paper-diffuse-retained" },
                palette = new[] { "Fire #DE2D26", "Metal #FEE5D9", "Wood #FCAE91", "Water #FB6A4A", "Earth #A50F15" },
                singleHitPalette = "All source tints overridden to #DE2D26 for BlueRehits fixtures only.",
                tintEncoding = "sRGB swatch converted to linear once, then SetVector numerical pigment values; no source journal or authority colors changed.",
                snapshotIdentity = snapshot.Identity, sourceStyleVersion = settings.Version,
                baselineShader = LabWatercolorV3Study.StampShaderPath, improvedShader = ShaderPath,
                surfaceShader = LabWatercolorV3Study.SurfaceShaderPath,
                sourceSettings = settings.Clone(), preservedFiles = preserved,
                shaderHashes = new[] { Hash(File.ReadAllBytes(LabWatercolorV3Study.StampShaderPath)),
                    Hash(File.ReadAllBytes(ShaderPath)), Hash(File.ReadAllBytes(LabWatercolorV3Study.SurfaceShaderPath)) }
            };
            var fixtures = new[] { LabWatercolorStampFixture.BlueRehits, LabWatercolorStampFixture.BlueRehits,
                LabWatercolorStampFixture.TwoColorOverlap, LabWatercolorStampFixture.ThreeColorCross,
                LabWatercolorStampFixture.FiveColorComplete };
            int[] repeats = { 0, 3, 2, 1, 0 };
            for (int f = 0; f < fixtures.Length; f++)
            using (var source = new LabWatercolorStampStudy())
            {
                source.Prepare(fixtures[f], repeats[f], 123u, settings);
                string authority = LabWatercolorStampStudy.Hash(source.Surface);
                string colorMap = Hash(Read(source.HighResolutionMap));
                string painterMap = Hash(Read(source.HighResolutionPainterMap));
                string journal = source.Journal.PrefixHash;
                ulong sequence = source.Journal.LastSequence;
                bool singleRed = fixtures[f] == LabWatercolorStampFixture.BlueRehits;
                string stem = fixtures[f].ToString().ToLowerInvariant() + "-first-plus-" + repeats[f];
                var entry = new Entry { fixture = fixtures[f].ToString(), repeats = repeats[f],
                    sourceAuthorityHash = authority, sourceColorMapHash = colorMap, sourcePainterMapHash = painterMap,
                    sourceJournalHash = journal, sourceLastSequence = sequence,
                    coveredCells = source.Surface.CoveredCount, coverage = source.Surface.Coverage,
                    triptychs = new[] { stem + "-actual-triptych.png", stem + "-neutral-triptych.png" } };
                var actualFrames = new Texture2D[3]; var neutralFrames = new Texture2D[3];
                try
                {
                    for (int column = 0; column < 3; column++)
                    {
                        int variant = column;
                        Action<MaterialPropertyBlock> configure = block => {
                            snapshot.ApplyStamp(block);
                            if (variant > 0) block.SetFloat("_V3LayerOpacityCap", .95f);
                            block.SetFloat("_StudyPigmentLoad", 1.8f);
                            block.SetFloat("_StudyGlaze", variant == 2 ? 1f : 0f);
                            block.SetFloat("_StudyWashOpacity", .02f);
                            block.SetFloat("_StudyFlowStrength", .2f);
                            block.SetFloat("_StudyOverlapDose", .55f);
                            block.SetVector("_Tint", RemapTint(block.GetVector("_Tint"), singleRed));
                        };
                        Shader shader = column == 0 ? baseline : improved;
                        string identity = snapshot.Identity + "/transmission-study-r1/" + report.columns[column]
                            + (singleRed ? "/single-red" : "/reds5");
                        using (var runtime = new LabWatercolorStampRuntime(shader, snapshot.Brushes, configure, identity))
                        {
                            LabWatercolorStampStudy.Finish(runtime, source.Journal, source.HighResolutionMap, settings);
                            Color32[] expected = Read(runtime.PaintTexture);
                            LabWatercolorStampStudy.Finish(runtime, source.Journal, source.HighResolutionMap, settings);
                            Require(expected.SequenceEqual(Read(runtime.PaintTexture)), "Repeated settled replay changed final map bytes.");
                            using (var replay = new LabWatercolorStampRuntime(shader, snapshot.Brushes, configure, identity))
                            {
                                LabWatercolorStampStudy.Finish(replay, source.Journal, source.HighResolutionMap, settings);
                                Require(expected.SequenceEqual(Read(replay.PaintTexture)), "Fresh replay changed final map bytes.");
                            }
                            entry.variants.Add(new Variant { name = report.columns[column], identity = identity,
                                mapHash = Hash(expected), repeatReplayEqual = true, freshReplayEqual = true });
                            SaveMap(directory, stem + "-" + report.columns[column] + "-paintmap.png", expected,
                                runtime.PaintTexture.width, runtime.PaintTexture.height);
                            using (var renderer = new LabPaintStudyRenderer(true, surface, snapshot.ApplySurface))
                            {
                                actualFrames[column] = renderer.RenderVisual(runtime.PaintTexture, 0f, 1f, 0f, settings);
                                if (fixtures[f] == LabWatercolorStampFixture.FiveColorComplete && column == 0)
                                    SaveSurfaceDiagnostics(directory, surface, snapshot, runtime.PaintTexture, settings);
                                if (fixtures[f] == LabWatercolorStampFixture.FiveColorComplete && column == 2)
                                    entry.sixViews = SaveSixViews(directory, stem, renderer, runtime.PaintTexture, settings);
                            }
                            using (var renderer = new LabPaintStudyRenderer(true, surface, material => {
                                snapshot.ApplySurface(material); NeutralSurface(material);
                            })) neutralFrames[column] = renderer.RenderVisual(runtime.PaintTexture, 0f, 1f, 0f, settings);
                            Require(expected.SequenceEqual(Read(runtime.PaintTexture)), "Image capture changed a private paint map.");
                        }
                    }
                    if (fixtures[f] == LabWatercolorStampFixture.BlueRehits && repeats[f] == 0)
                    {
                        Require(entry.variants[1].mapHash == entry.variants[2].mapHash,
                            "Gentler overlap must not change a first stamp.");
                        entry.firstStampImprovedVariantsEqual = true;
                    }
                    SaveTriptych(directory, stem + "-actual", entry.triptychs[0], actualFrames, report.columns);
                    SaveTriptych(directory, stem + "-neutral", entry.triptychs[1], neutralFrames, report.columns);
                }
                finally { Release(actualFrames); Release(neutralFrames); }
                Require(authority == LabWatercolorStampStudy.Hash(source.Surface)
                    && colorMap == Hash(Read(source.HighResolutionMap)) && painterMap == Hash(Read(source.HighResolutionPainterMap))
                    && journal == source.Journal.PrefixHash && sequence == source.Journal.LastSequence,
                    "The appearance study changed its source authority, maps or sealed journal.");
                entry.sourcePreserved = true; report.entries.Add(entry);
            }
            Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == sceneStates.Length,
                "The appearance study changed loaded or active scenes.");
            for (int i = 0; i < sceneStates.Length; i++)
            {
                Scene now = SceneManager.GetSceneAt(i); SceneState before = sceneStates[i];
                Require(now == before.scene && now.path == before.path && now.isDirty == before.dirty,
                    "The appearance study changed a loaded scene identity or dirty flag.");
            }
            foreach (Preserved item in preserved)
            {
                item.afterSha256 = File.Exists(item.asset) ? Hash(File.ReadAllBytes(item.asset)) : "missing";
                item.bytesEqual = item.beforeSha256 == item.afterSha256;
                item.authoringEqual = item.authoringBeforeSha256 == null || item.authoringBeforeSha256 ==
                    Hash(System.Text.Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(AssetDatabase.LoadMainAssetAtPath(item.asset))));
                Require(item.bytesEqual && item.authoringEqual, "A saved or authoring asset changed: " + item.asset);
            }
            report.loadedScenesPreserved = report.savedAssetsPreserved = report.authoringPreserved = true;
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "transmission-study.json"), json);
            return json;
        }

        private static Vector4 RemapTint(Vector4 tint, bool single)
        {
            LabOrbElement[] elements = { LabOrbElement.Fire, LabOrbElement.Metal, LabOrbElement.Wood, LabOrbElement.Water, LabOrbElement.Earth };
            Color32[] reds = { new Color32(222,45,38,255), new Color32(254,229,217,255), new Color32(252,174,145,255),
                new Color32(251,106,74,255), new Color32(165,15,21,255) };
            int selected = 0; float best = float.PositiveInfinity;
            if (!single) for (int i = 0; i < elements.Length; i++)
            {
                Color c = LabPaintPalette.ColorFor(elements[i]);
                float distance = (new Vector3(tint.x-c.r, tint.y-c.g, tint.z-c.b)).sqrMagnitude;
                if (distance < best) { best = distance; selected = i; }
            }
            Require(single || best < .0001f, "Source tint does not match the authored palette; do not guess a replacement.");
            Color red = ((Color)reds[selected]).linear;
            return new Vector4(red.r, red.g, red.b, 1f);
        }
        private static void NeutralSurface(Material material)
        {
            material.SetVector("_BaseColor", Vector4.one); material.SetFloat("_PaperContrast", 0f);
            material.SetFloat("_PaperLightContrast", 0f); material.SetFloat("_PaperBrightness", 1f);
            material.SetFloat("_V3PaperRelief", 0f); material.SetFloat("_V3PigmentGrain", 0f);
            material.SetFloat("_V3PaperFiberInfluence", 0f);
        }
        private static void SaveSurfaceDiagnostics(string directory, Shader surface,
            LabWatercolorV3Snapshot snapshot, Texture map, LabWatercolorStampSettings settings)
        {
            // One input map; only the named private surface contribution changes.
            string[] names = { "current", "paper-neutral", "grain-off", "paper-neutral-grain-off" };
            var images = new Texture2D[names.Length];
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    int mode = i;
                    using (var renderer = new LabPaintStudyRenderer(true, surface, material => {
                        snapshot.ApplySurface(material);
                        if (mode == 1 || mode == 3)
                        {
                            material.SetVector("_BaseColor", Vector4.one);
                            material.SetFloat("_PaperContrast", 0f);
                            material.SetFloat("_PaperLightContrast", 0f);
                            material.SetFloat("_PaperBrightness", 1f);
                            material.SetFloat("_V3PaperRelief", 0f);
                        }
                        if (mode == 2 || mode == 3)
                        {
                            material.SetFloat("_V3PigmentGrain", 0f);
                            material.SetFloat("_V3PaperFiberInfluence", 0f);
                        }
                    })) images[i] = renderer.RenderVisual(map, 0, 1, 0, settings);
                    File.WriteAllBytes(Path.Combine(directory, "surface-" + names[i] + ".png"), images[i].EncodeToPNG());
                }
                Save(directory, "surface-diagnostic-four-views.png", LabWatercolorStampStudy.CombineGrid(images, 2));
            }
            finally { Release(images); }
        }
        private static string SaveSixViews(string directory, string stem, LabPaintStudyRenderer renderer,
            Texture map, LabWatercolorStampSettings settings)
        {
            Vector2[] views = { Vector2.zero, new Vector2(90,0), new Vector2(180,0), new Vector2(-90,0), new Vector2(45,0), new Vector2(45,45) };
            var images = new Texture2D[views.Length];
            string name = stem + "-overlap-glazing-actual-six-views.png";
            try {
                for (int i = 0; i < views.Length; i++) {
                    images[i] = renderer.RenderVisual(map, views[i].x, 1f, views[i].y, settings);
                    File.WriteAllBytes(Path.Combine(directory, stem + "-glaze-" + LabWatercolorStampStudy.ViewNames[i] + ".png"), images[i].EncodeToPNG());
                }
                Save(directory, name, LabWatercolorStampStudy.CombineGrid(images, 3)); return name;
            }
            finally { Release(images); }
        }
        private static List<Preserved> CapturePreserved(LabWatercolorV3Profile profile, SceneState[] scenes)
        {
            var paths = new HashSet<string>(AssetDatabase.GetDependencies(LabPaintStudyRenderer.PrefabPath, true), StringComparer.Ordinal);
            foreach (string path in new[] { AssetDatabase.GetAssetPath(profile), AssetDatabase.GetAssetPath(Resources.Load<LabConfig>("LabConfig")),
                "Assets/Lab/Resources/GourdPaint/WatercolorStampProfile.asset", "Assets/Lab/Scenes/Lab.unity", "Assets/Hapio/Scenes/Battle.unity",
                LabWatercolorV3Study.StampShaderPath, LabWatercolorV3Study.SurfaceShaderPath, ShaderPath }) if (!string.IsNullOrEmpty(path)) paths.Add(path);
            foreach (SceneState scene in scenes) if (!string.IsNullOrEmpty(scene.path)) paths.Add(scene.path);
            foreach (string folder in new[] { LabWatercolorBrushBaker.MasterDirectory, LabWatercolorBrushBaker.OutputDirectory, LabWatercolorV3Baker.DataDirectory })
                if (Directory.Exists(folder)) foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) paths.Add(path);
            foreach (string path in paths.ToArray()) if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
            return paths.Where(File.Exists).OrderBy(p => p, StringComparer.Ordinal).Select(path => {
                Object asset = path.EndsWith(".mat", StringComparison.Ordinal) || path.EndsWith(".asset", StringComparison.Ordinal)
                    ? AssetDatabase.LoadMainAssetAtPath(path) : null;
                return new Preserved { asset = path, beforeSha256 = Hash(File.ReadAllBytes(path)),
                    authoringBeforeSha256 = asset == null ? null : Hash(System.Text.Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(asset))) };
            }).ToList();
        }
        private static Color32[] Read(Texture source)
        {
            Require(source != null, "A private GPU map is missing.");
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D image = null;
            try { Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                image.ReadPixels(new Rect(0,0,source.width,source.height),0,0); image.Apply(false,false); return image.GetPixels32(); }
            finally { if (image != null) Object.DestroyImmediate(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }
        private static void SaveMap(string directory, string name, Color32[] pixels, int width, int height)
        { var image = new Texture2D(width,height,TextureFormat.RGBA32,false,true); image.SetPixels32(pixels); image.Apply(false,false); Save(directory,name,image); }
        private static void SaveTriptych(string directory, string stem, string name, Texture2D[] images, string[] columns)
        { for (int i=0;i<images.Length;i++) File.WriteAllBytes(Path.Combine(directory,stem+"-"+columns[i]+".png"),images[i].EncodeToPNG()); Save(directory,name,LabPaintStudyRenderer.Combine(images)); }
        private static void Save(string directory, string name, Texture2D image)
        { try { File.WriteAllBytes(Path.Combine(directory,name),image.EncodeToPNG()); } finally { if(image!=null) Object.DestroyImmediate(image); } }
        private static void Release(Texture2D[] images)
        { foreach(Texture2D image in images) if(image!=null) Object.DestroyImmediate(image); }
        private static string Hash(Color32[] pixels)
        { var bytes=new byte[pixels.Length*4]; for(int i=0;i<pixels.Length;i++){int p=i*4;bytes[p]=pixels[i].r;bytes[p+1]=pixels[i].g;bytes[p+2]=pixels[i].b;bytes[p+3]=pixels[i].a;} return Hash(bytes); }
        private static string Hash(byte[] bytes)
        { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }

        private sealed class SceneState { public Scene scene; public string path; public bool dirty; }
        [Serializable] private sealed class Preserved { public string asset,beforeSha256,afterSha256,authoringBeforeSha256; public bool bytesEqual,authoringEqual; }
        [Serializable] private sealed class Variant { public string name,identity,mapHash; public bool repeatReplayEqual,freshReplayEqual; }
        [Serializable] private sealed class Entry {
            public string fixture,sourceAuthorityHash,sourceColorMapHash,sourcePainterMapHash,sourceJournalHash,sixViews;
            public string[] triptychs; public int repeats,coveredCells; public float coverage; public ulong sourceLastSequence;
            public bool sourcePreserved,firstStampImprovedVariantsEqual; public List<Variant> variants=new List<Variant>();
        }
        [Serializable] private sealed class Report {
            public string unity,gpu,scope,singleHitPalette,tintEncoding,snapshotIdentity,sourceStyleVersion,baselineShader,improvedShader,surfaceShader;
            public string[] columns,surfaceModes,palette,shaderHashes;
            public float baselineLayerOpacityCap=.85f,candidateLayerOpacityCap=.95f,studyPigmentLoad=1.8f,studyWashOpacity=.02f,studyFlowStrength=.2f,studyOverlapDose=.55f;
            public LabWatercolorStampSettings sourceSettings; public List<Preserved> preservedFiles;
            public bool loadedScenesPreserved,savedAssetsPreserved,authoringPreserved; public List<Entry> entries=new List<Entry>();
        }
    }
}
