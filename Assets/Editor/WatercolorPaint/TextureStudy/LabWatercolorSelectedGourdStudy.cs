using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Private Editor projection of the selected Round texture; never installs a game default.</summary>
    public static class LabWatercolorSelectedGourdStudy
    {
        public const string ShaderPath = "Assets/Editor/WatercolorPaint/TextureStudy/WatercolorSelectedGourd.shader";
        private static readonly string[] Columns = { "transmission-round-control", "selected-enhanced-round" };
        private static readonly string[] Views = { "actual-normal", "actual-detail", "neutral-normal", "neutral-detail" };
        private static readonly Vector4 TintDisplay = new Vector4(222f / 255f, 45f / 255f, 38f / 255f, 1f);

        [MenuItem("C6 Lab/Experiments/Selected Watercolor Gourd Comparison")]
        private static void ExportMenu()
        {
            string directory = EditorUtility.OpenFolderPanel("Selected watercolor on gourd", "", "");
            if (string.IsNullOrEmpty(directory)) return;
            try { Export(directory); EditorUtility.RevealInFinder(Path.Combine(directory, "selected-gourd-study.json")); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static string Export(string directory)
        {
            Require(!string.IsNullOrWhiteSpace(directory) && Path.IsPathRooted(directory), "An absolute evidence directory is required.");
            directory = Path.GetFullPath(directory);
            string assets = Path.GetFullPath(Application.dataPath);
            Require(directory != assets && !directory.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.Ordinal),
                "Evidence must be outside Assets to protect authoring inputs.");
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling, "Use the stopped Editor after compilation completes.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "An Editor GPU is required.");
            var profile = LabWatercolorV3Baker.LoadProfile();
            var snapshot = profile.Snapshot(.95f); snapshot.Validate();
            var settings = snapshot.BaseStampSettings; settings.enabled = true; settings.Validate();
            Shader baseline = LoadShader(LabWatercolorTransmissionStudy.ShaderPath), selected = LoadShader(ShaderPath);
            Shader surface = LoadShader(LabWatercolorV3Study.SurfaceShaderPath);
            Scene active = SceneManager.GetActiveScene();
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i)).ToArray();
            bool[] dirty = scenes.Select(scene => scene.isDirty).ToArray();
            var preserved = CapturePreserved(scenes);
            var report = new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(), projectColorSpace = QualitySettings.activeColorSpace.ToString(),
                scope = "Editor-only appearance preview on disposable saved WhiteGourd copies. Only the selected Round master is projected; no full-surface or multicolor approval is implied. Shared original BlueRehits journals, CPU cells, color/P maps and seeds are unchanged. No game defaults, network, mobile, performance or physical pigment-model validation.",
                sourceContract = "Native numerical RGBA source; SourceDarkness=A*(1-RGB), A=sourceAlpha. Signed source bands use the exact flat-study helper. Gourd-only RGBAFloat mip clones retain identical base pixels, generate mips once, and use trilinear/clamp sampling; native helper textures are not modified. Candidate: SourceGain6, Enhancement1, MediumGain1.4, FineGain.85, no minimum wash or synthetic grain. Fixed display tint #DE2D26. All three private brush slots intentionally reference Round; journal seeds and geometry are retained.",
                baselineContract = "Transmission control also uses Round in all three private slots: V3 cap.95, load1.8, glaze1, overlapDose.55, wash.02, flowStrength.2. Fixed #DE2D26 converted to linear once. It is a control, not the approved source texture.",
                surfaceContract = "Both columns use identical V3 surface, diffuse lighting and saved gourd. Pigment grain, fiber coupling and paper relief are zero. Neutral variant uses white paper with paper contrast/shape contrast zero; existing shell diffuse remains. Normal zoom1, detail zoom1.6; detail is camera magnification, not a pixel crop.",
                snapshotIdentity = snapshot.Identity, sourceStyleVersion = settings.Version, sourceSettings = settings.Clone(),
                columns = Columns, views = Views, preservedFiles = preserved,
                shaderPaths = new[] { LabWatercolorTransmissionStudy.ShaderPath, ShaderPath, LabWatercolorV3Study.SurfaceShaderPath },
                shaderHashes = new[] { Hash(File.ReadAllBytes(LabWatercolorTransmissionStudy.ShaderPath)), Hash(File.ReadAllBytes(ShaderPath)), Hash(File.ReadAllBytes(LabWatercolorV3Study.SurfaceShaderPath)) }
            };
            var frames = Views.Select(_ => new Texture2D[4]).ToArray();
            Texture2D master = null, nativeDarkness = null, nativeBands = null, darkness = null, bands = null, v3Round = null;
            Directory.CreateDirectory(directory);
            try
            {
                string masterPath = LabWatercolorBrushBaker.MasterDirectory + "/Round.png";
                master = LabWatercolorFlatTextureStudy.Decode(masterPath);
                LabWatercolorFlatTextureStudy.BuildSourceMaps(master, out nativeDarkness, out nativeBands);
                darkness = MipCopy(nativeDarkness); bands = MipCopy(nativeBands);
                v3Round = LabWatercolorFlatTextureStudy.Decode(LabWatercolorV3Baker.BrushDirectory + "/Round.png");
                report.masterSha256 = Hash(File.ReadAllBytes(masterPath)); report.sourceWidth = master.width; report.sourceHeight = master.height;
                report.darknessSha256 = Hash(nativeDarkness.GetRawTextureData()); report.bandsSha256 = Hash(nativeBands.GetRawTextureData());
                report.darknessMipChainSha256 = Hash(darkness.GetRawTextureData()); report.bandsMipChainSha256 = Hash(bands.GetRawTextureData());
                string sourceIdentity = "selected-round-g6-m1.4-f.85-mips-r1/" + report.masterSha256 + "/" + report.darknessMipChainSha256 + "/" + report.bandsMipChainSha256 + "/" + report.shaderHashes[1];
                var sourcePack = new[] { darkness, darkness, darkness }; var baselinePack = new[] { v3Round, v3Round, v3Round };
                for (int row = 0; row < 2; row++)
                using (var source = new LabWatercolorStampStudy())
                {
                    int repeats = row == 0 ? 0 : 3;
                    source.Prepare(LabWatercolorStampFixture.BlueRehits, repeats, 123u, settings);
                    var entry = new Entry { repeats = repeats, seed = 123u, authorityHash = LabWatercolorStampStudy.Hash(source.Surface),
                        colorMapHash = Hash(Read(source.HighResolutionMap)), painterMapHash = Hash(Read(source.HighResolutionPainterMap)),
                        journalHash = source.Journal.PrefixHash, lastSequence = source.Journal.LastSequence,
                        coveredCells = source.Surface.CoveredCount, coverage = source.Surface.Coverage };
                    string stem = "first-plus-" + repeats;
                    for (int column = 0; column < 2; column++)
                    {
                        bool candidate = column == 1;
                        Action<MaterialPropertyBlock> configure = candidate ? (Action<MaterialPropertyBlock>)(block => {
                            block.SetTexture("_SourceBands", bands); block.SetVector("_TintDisplay", TintDisplay);
                            block.SetFloat("_SourceGain", 6f); block.SetFloat("_Enhancement", 1f);
                            block.SetFloat("_MediumGain", 1.4f); block.SetFloat("_FineGain", .85f);
                        }) : block => {
                            snapshot.ApplyStamp(block); block.SetFloat("_V3LayerOpacityCap", .95f);
                            block.SetFloat("_StudyPigmentLoad", 1.8f); block.SetFloat("_StudyGlaze", 1f);
                            block.SetFloat("_StudyOverlapDose", .55f); block.SetFloat("_StudyWashOpacity", .02f);
                            block.SetFloat("_StudyFlowStrength", .2f);
                            Color tint = new Color(TintDisplay.x, TintDisplay.y, TintDisplay.z, 1f).linear;
                            block.SetVector("_Tint", new Vector4(tint.r, tint.g, tint.b, 1f));
                        };
                        Shader shader = candidate ? selected : baseline;
                        Texture2D[] pack = candidate ? sourcePack : baselinePack;
                        string identity = candidate ? sourceIdentity : snapshot.Identity + "/selected-round-transmission-r1/" + report.shaderHashes[0];
                        using (var runtime = new LabWatercolorStampRuntime(shader, pack, configure, identity))
                        {
                            LabWatercolorStampStudy.Finish(runtime, source.Journal, source.HighResolutionMap, settings);
                            Color32[] expected = Read(runtime.PaintTexture);
                            LabWatercolorStampStudy.Finish(runtime, source.Journal, source.HighResolutionMap, settings);
                            Require(expected.SequenceEqual(Read(runtime.PaintTexture)), "Repeated settled replay changed map bytes: " + Columns[column]);
                            using (var replay = new LabWatercolorStampRuntime(shader, pack, configure, identity))
                            {
                                LabWatercolorStampStudy.Finish(replay, source.Journal, source.HighResolutionMap, settings);
                                Require(expected.SequenceEqual(Read(replay.PaintTexture)), "Fresh replay changed map bytes: " + Columns[column]);
                            }
                            entry.variants.Add(new Variant { name = Columns[column], identity = identity, mapHash = Hash(expected), repeatedReplayExact = true, freshReplayExact = true });
                            SaveMap(directory, stem + "-" + Columns[column] + "-paintmap.png", expected, runtime.PaintTexture.width, runtime.PaintTexture.height);
                            for (int paper = 0; paper < 2; paper++)
                            using (var renderer = new LabPaintStudyRenderer(true, surface, material => ApplySurface(material, snapshot, paper == 1)))
                            for (int detail = 0; detail < 2; detail++)
                            {
                                int view = paper * 2 + detail;
                                Texture2D image = renderer.RenderVisual(runtime.PaintTexture, 0f, detail == 0 ? 1f : 1.6f, 0f, settings);
                                frames[view][row * 2 + column] = image;
                                File.WriteAllBytes(Path.Combine(directory, stem + "-" + Views[view] + "-" + Columns[column] + ".png"), image.EncodeToPNG());
                            }
                            Require(expected.SequenceEqual(Read(runtime.PaintTexture)), "Image capture changed a private paint map.");
                        }
                    }
                    for (int view = 0; view < Views.Length; view++)
                        Save(directory, stem + "-" + Views[view] + "-comparison.png", LabPaintStudyRenderer.Combine(new[] { frames[view][row * 2], frames[view][row * 2 + 1] }));
                    Require(entry.authorityHash == LabWatercolorStampStudy.Hash(source.Surface)
                        && entry.colorMapHash == Hash(Read(source.HighResolutionMap)) && entry.painterMapHash == Hash(Read(source.HighResolutionPainterMap))
                        && entry.journalHash == source.Journal.PrefixHash && entry.lastSequence == source.Journal.LastSequence,
                        "The appearance preview changed its authority, color/P maps or sealed journal.");
                    entry.sourcePreserved = true; report.entries.Add(entry);
                }
                for (int view = 0; view < Views.Length; view++)
                    Save(directory, Views[view] + "-first-and-rehits-grid.png", LabWatercolorStampStudy.CombineGrid(frames[view], 2));
            }
            finally
            {
                foreach (Texture2D[] group in frames) foreach (Texture2D image in group) if (image != null) Object.DestroyImmediate(image);
                foreach (Texture2D texture in new[] { master, nativeDarkness, nativeBands, darkness, bands, v3Round }) if (texture != null) Object.DestroyImmediate(texture);
                Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == scenes.Length, "Loaded or active scene identity changed.");
                for (int i = 0; i < scenes.Length; i++) Require(SceneManager.GetSceneAt(i) == scenes[i] && scenes[i].isDirty == dirty[i], "Loaded scene or dirty state changed.");
                foreach (Preserved item in preserved)
                {
                    item.afterSha256 = File.Exists(item.path) ? Hash(File.ReadAllBytes(item.path)) : "missing";
                    item.bytesEqual = item.beforeSha256 == item.afterSha256;
                    item.authoringEqual = item.authoringBeforeSha256 == AuthoringHash(item.path);
                    item.importerEqual = item.importerBeforeSha256 == ImporterHash(item.path);
                    Require(item.bytesEqual && item.authoringEqual && item.importerEqual, "Saved asset, authoring object or importer changed: " + item.path);
                }
            }
            report.loadedScenesPreserved = report.savedAssetsPreserved = report.authoringAndImportersPreserved = true;
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "selected-gourd-study.json"), json, new UTF8Encoding(false));
            return json;
        }

        private static Texture2D MipCopy(Texture2D source)
        {
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, true, true)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            try {
                Color[] pixels = source.GetPixels(); texture.SetPixels(pixels, 0); texture.Apply(true, false);
                Require(pixels.SequenceEqual(texture.GetPixels(0)), "Gourd mip clone changed native source base pixels."); return texture;
            }
            catch { Object.DestroyImmediate(texture); throw; }
        }
        private static void ApplySurface(Material material, LabWatercolorV3Snapshot snapshot, bool neutral)
        {
            snapshot.ApplySurface(material); material.SetFloat("_V3PigmentGrain", 0f);
            material.SetFloat("_V3PaperFiberInfluence", 0f); material.SetFloat("_V3PaperRelief", 0f);
            if (!neutral) return;
            material.SetVector("_BaseColor", Vector4.one); material.SetFloat("_PaperContrast", 0f);
            material.SetFloat("_PaperLightContrast", 0f); material.SetFloat("_PaperBrightness", 1f);
        }
        private static Shader LoadShader(string path)
        { Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path); Require(shader != null && shader.isSupported, "Supported comparison shader required: " + path); return shader; }
        private static Color32[] Read(Texture source)
        {
            Require(source != null, "A private GPU map is missing.");
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D image = null; bool srgb = GL.sRGBWrite;
            try { GL.sRGBWrite = false; Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply(false, false); return image.GetPixels32(); }
            finally { GL.sRGBWrite = srgb; if (image != null) Object.DestroyImmediate(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }
        private static void SaveMap(string directory, string name, Color32[] pixels, int width, int height)
        { var image = new Texture2D(width, height, TextureFormat.RGBA32, false, true); image.SetPixels32(pixels); image.Apply(false, false); Save(directory, name, image); }
        private static void Save(string directory, string name, Texture2D image)
        { try { File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG()); } finally { if (image != null) Object.DestroyImmediate(image); } }
        private static List<Preserved> CapturePreserved(Scene[] scenes)
        {
            var paths = new HashSet<string>(AssetDatabase.GetDependencies(LabPaintStudyRenderer.PrefabPath, true), StringComparer.Ordinal);
            foreach (string folder in new[] { "Assets/Editor/WatercolorPaint", "Assets/Lab/Resources/GourdPaint" })
                if (Directory.Exists(folder)) foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) paths.Add(path);
            foreach (string path in new[] { "Assets/Lab/Scenes/Lab.unity", "Assets/Hapio/Scenes/Battle.unity",
                AssetDatabase.GetAssetPath(Resources.Load<LabConfig>("LabConfig")) }.Concat(scenes.Select(scene => scene.path))) if (!string.IsNullOrEmpty(path)) paths.Add(path);
            foreach (string path in paths.ToArray()) if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
            return paths.Where(File.Exists).OrderBy(path => path, StringComparer.Ordinal).Select(path => new Preserved {
                path = path, beforeSha256 = Hash(File.ReadAllBytes(path)), authoringBeforeSha256 = AuthoringHash(path), importerBeforeSha256 = ImporterHash(path) }).ToList();
        }
        private static string AuthoringHash(string path)
        {
            if (!path.EndsWith(".asset", StringComparison.Ordinal) && !path.EndsWith(".mat", StringComparison.Ordinal)) return null;
            Object asset = AssetDatabase.LoadMainAssetAtPath(path); return asset == null ? null : Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(asset)));
        }
        private static string ImporterHash(string path)
        {
            if (path.EndsWith(".meta", StringComparison.Ordinal)) return null;
            AssetImporter importer = AssetImporter.GetAtPath(path); return importer == null ? null : Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(importer)));
        }
        private static string Hash(Color32[] pixels)
        { var bytes = new byte[pixels.Length * 4]; for (int i = 0; i < pixels.Length; i++) { int p = i * 4; bytes[p] = pixels[i].r; bytes[p + 1] = pixels[i].g; bytes[p + 2] = pixels[i].b; bytes[p + 3] = pixels[i].a; } return Hash(bytes); }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        [Serializable] private sealed class Report {
            public string unity, gpu, projectColorSpace, scope, sourceContract, baselineContract, surfaceContract, snapshotIdentity, sourceStyleVersion;
            public string masterSha256, darknessSha256, bandsSha256, darknessMipChainSha256, bandsMipChainSha256; public int sourceWidth, sourceHeight;
            public string[] columns, views, shaderPaths, shaderHashes; public LabWatercolorStampSettings sourceSettings;
            public bool loadedScenesPreserved, savedAssetsPreserved, authoringAndImportersPreserved;
            public List<Preserved> preservedFiles; public List<Entry> entries = new List<Entry>();
        }
        [Serializable] private sealed class Entry {
            public int repeats, coveredCells; public uint seed; public float coverage; public ulong lastSequence;
            public string authorityHash, colorMapHash, painterMapHash, journalHash; public bool sourcePreserved;
            public List<Variant> variants = new List<Variant>();
        }
        [Serializable] private sealed class Variant { public string name, identity, mapHash; public bool repeatedReplayExact, freshReplayExact; }
        [Serializable] private sealed class Preserved {
            public string path, beforeSha256, afterSha256, authoringBeforeSha256, importerBeforeSha256;
            public bool bytesEqual, authoringEqual, importerEqual;
        }
    }
}
