#if UNITY_EDITOR
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
    /// <summary>Private Plan 3 contour prototype. Neither mask nor appearance becomes a game default.</summary>
    public static class LabWatercolorContourStudy
    {
        public const string ShaderPath = LabWatercolorSelectedGourdStudy.ShaderPath;
        private const float MaskThreshold = .15f; // Existing LabPaintPatternMask threshold, unchanged.
        private const uint Seed = 123u;
        private static readonly string[] Columns = { "old-splatter-mask", "round-alpha-mask" };
        private static readonly string[] Views = { "normal", "detail" };
        private static readonly Vector4 TintDisplay = new Vector4(222f / 255f, 45f / 255f, 38f / 255f, 1f);

        [MenuItem("C6 Lab/Experiments/Watercolor Brush Contour Comparison")]
        private static void ExportMenu()
        {
            string directory = EditorUtility.OpenFolderPanel("Watercolor brush contour comparison", "", "");
            if (string.IsNullOrEmpty(directory)) return;
            try { Export(directory); EditorUtility.RevealInFinder(Path.Combine(directory, "contour-study.json")); }
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
            var snapshot = LabWatercolorV3Baker.LoadProfile().Snapshot(.95f); snapshot.Validate();
            var settings = snapshot.BaseStampSettings; settings.enabled = true; settings.Validate();
            Shader selected = LoadShader(ShaderPath), surface = LoadShader(LabWatercolorV3Study.SurfaceShaderPath);
            Scene active = SceneManager.GetActiveScene();
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i)).ToArray();
            bool[] dirty = scenes.Select(scene => scene.isDirty).ToArray();
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            var preserved = CapturePreserved(scenes);
            var report = new Report {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(), projectColorSpace = QualitySettings.activeColorSpace.ToString(),
                scope = "Plan 3 mask prototype, not shipped. Two disposable saved-gourd copies per row compare the existing CPU Splatter contour with a CPU mask derived from the original selected Round alpha. All game defaults remain unchanged. No mobile, network, performance or physical pigment-model approval is implied.",
                comparisonContract = "Both columns use the same selected shader, native Round source maps and settings, fixed red display tint, V3 grain-off surface, seed123 and saved Host rules. Both columns use logical paid-cell silhouette reconstruction and explicitly bypass the older intermediate visual silhouette; the only cross-column input difference is the fixture-local CPU mask override before its first hit. Each column replays its own sealed source journal; cross-column journal and authority equality is intentionally not required because the contour changes. CoveredCount must match, and cross-column authority hashes must differ.",
                maskContract = "Original Round PNG bytes decoded as numerical RGBA32, linear, readable, no mipmaps. Derived CPU texture is white RGB plus unchanged source alpha, RGBA32, linear, readable, no mipmaps, with no erosion, dilation or resampling. Existing bilinear CPU sampling and threshold.15 remain unchanged. Threshold pixel fraction is a source-mask diagnostic, not a claim of rendered gourd coverage. Each surface receives an immutable local byte copy; no global pattern cache is replaced.",
                sourceContract = "Both columns: SourceDarkness=A*(1-RGB), A=sourceAlpha; exact flat-study source bands; private RGBAFloat mip clones with identical native base pixels, trilinear/clamp. Round in all three private brush slots. SourceGain6, Enhancement1, MediumGain1.4, FineGain.85, display tint #DE2D26. No transmission baseline or synthetic grain.",
                surfaceContract = "Both columns use the identical V3 surface on a saved WhiteGourd copy with pigment grain, paper fiber coupling and relief zero. Identical saved shell diffuse, camera and lighting. Normal zoom1 and detail zoom1.6. Grid top row=first hit, bottom row=first hit plus three rehits; left=old Splatter, right=Round alpha.",
                snapshotIdentity = snapshot.Identity, sourceStyleVersion = settings.Version, sourceSettings = settings.Clone(),
                sourceGain = 6f, enhancement = 1f, mediumGain = 1.4f, fineGain = .85f, tintDisplay = TintDisplay,
                maskThreshold = MaskThreshold, columns = Columns, views = Views, preservedFiles = preserved,
                shaderPaths = new[] { ShaderPath, LabWatercolorV3Study.SurfaceShaderPath },
                shaderHashes = new[] { Hash(File.ReadAllBytes(ShaderPath)), Hash(File.ReadAllBytes(LabWatercolorV3Study.SurfaceShaderPath)) }
            };
            var frames = Views.Select(_ => new Texture2D[4]).ToArray();
            Texture2D master = null, mask = null, nativeDarkness = null, nativeBands = null, darkness = null, bands = null;
            Directory.CreateDirectory(directory);
            try
            {
                string masterPath = LabWatercolorBrushBaker.MasterDirectory + "/Round.png";
                master = LabWatercolorFlatTextureStudy.Decode(masterPath);
                mask = BuildAlphaMask(master, out int thresholdPixels);
                LabWatercolorFlatTextureStudy.BuildSourceMaps(master, out nativeDarkness, out nativeBands);
                darkness = MipCopy(nativeDarkness); bands = MipCopy(nativeBands);
                report.masterSha256 = Hash(File.ReadAllBytes(masterPath)); report.sourcePixelsSha256 = Hash(master.GetPixels32());
                report.sourceWidth = master.width; report.sourceHeight = master.height;
                report.derivedMaskSha256 = Hash(mask.GetPixels32()); report.maskThresholdPixels = thresholdPixels;
                report.maskPixelCount = master.width * master.height; report.maskThresholdPixelFraction = thresholdPixels / (float)report.maskPixelCount;
                report.darknessSha256 = Hash(nativeDarkness.GetRawTextureData()); report.bandsSha256 = Hash(nativeBands.GetRawTextureData());
                report.darknessMipChainSha256 = Hash(darkness.GetRawTextureData()); report.bandsMipChainSha256 = Hash(bands.GetRawTextureData());
                string visualIdentity = "selected-round-logical-contour-g6-m1.4-f.85-mips-r1/" + report.masterSha256 + "/" + report.darknessMipChainSha256 + "/" + report.bandsMipChainSha256 + "/" + report.shaderHashes[0];
                report.visualIdentity = visualIdentity;
                File.WriteAllBytes(Path.Combine(directory, "derived-round-alpha-mask.png"), mask.EncodeToPNG());
                var pack = new[] { darkness, darkness, darkness };
                Action<MaterialPropertyBlock> configure = block => {
                    block.SetTexture("_SourceBands", bands); block.SetVector("_TintDisplay", TintDisplay);
                    block.SetFloat("_SourceGain", 6f); block.SetFloat("_Enhancement", 1f);
                    block.SetFloat("_MediumGain", 1.4f); block.SetFloat("_FineGain", .85f);
                };
                for (int row = 0; row < 2; row++)
                using (var oldSource = new LabWatercolorStampStudy())
                using (var roundSource = new LabWatercolorStampStudy())
                {
                    int repeats = row == 0 ? 0 : 3;
                    roundSource.SetPatternForAppearanceStudy(mask);
                    oldSource.Prepare(LabWatercolorStampFixture.BlueRehits, repeats, Seed, settings);
                    roundSource.Prepare(LabWatercolorStampFixture.BlueRehits, repeats, Seed, settings);
                    Require(oldSource.Surface.Pattern == LabPaintPattern.Splatter && roundSource.Surface.Pattern == LabPaintPattern.Splatter,
                        "This comparison requires the saved Splatter fixture rule in both columns.");
                    var entry = new Entry { repeats = repeats, seed = Seed };
                    var sources = new[] { oldSource, roundSource };
                    for (int column = 0; column < 2; column++)
                    {
                        LabWatercolorStampStudy source = sources[column];
                        var variant = new Variant { name = Columns[column], visualIdentity = visualIdentity,
                            maskIdentity = column == 0 ? "default-splatter" : "derived-round-alpha/" + report.derivedMaskSha256,
                            authorityHashBefore = LabWatercolorStampStudy.Hash(source.Surface),
                            colorMapHashBefore = Hash(Read(source.HighResolutionMap)), painterMapHashBefore = Hash(Read(source.HighResolutionPainterMap)),
                            journalHashBefore = source.Journal.PrefixHash, lastSequenceBefore = source.Journal.LastSequence,
                            coveredCellsBefore = source.Surface.CoveredCount, coverage = source.Surface.Coverage };
                        string stem = "first-plus-" + repeats + "-" + Columns[column];
                        using (var runtime = new LabWatercolorStampRuntime(selected, pack, configure, visualIdentity, material => material.SetFloat("_StudyLegacySilhouette",0f)))
                        {
                            LabWatercolorStampStudy.Finish(runtime, source.Journal, source.HighResolutionMap, settings);
                            Color32[] expected = Read(runtime.PaintTexture);
                            LabWatercolorStampStudy.Finish(runtime, source.Journal, source.HighResolutionMap, settings);
                            Require(expected.SequenceEqual(Read(runtime.PaintTexture)), "Repeated settled replay changed map bytes: " + stem);
                            using (var replay = new LabWatercolorStampRuntime(selected, pack, configure, visualIdentity, material => material.SetFloat("_StudyLegacySilhouette",0f)))
                            {
                                LabWatercolorStampStudy.Finish(replay, source.Journal, source.HighResolutionMap, settings);
                                Require(expected.SequenceEqual(Read(replay.PaintTexture)), "Fresh replay changed map bytes: " + stem);
                            }
                            variant.paintMapHash = Hash(expected); variant.repeatedReplayExact = variant.freshReplayExact = true;
                            SaveMap(directory, stem + "-paintmap.png", expected, runtime.PaintTexture.width, runtime.PaintTexture.height);
                            using (var renderer = new LabPaintStudyRenderer(true, surface, material => ApplySurface(material, snapshot)))
                            for (int view = 0; view < Views.Length; view++)
                            {
                                Texture2D image = renderer.RenderVisual(runtime.PaintTexture, 0f, view == 0 ? 1f : 1.6f, 0f, settings);
                                frames[view][row * 2 + column] = image;
                                File.WriteAllBytes(Path.Combine(directory, stem + "-" + Views[view] + ".png"), image.EncodeToPNG());
                            }
                            Require(expected.SequenceEqual(Read(runtime.PaintTexture)), "Image capture changed a private paint map: " + stem);
                        }
                        variant.authorityHashAfter = LabWatercolorStampStudy.Hash(source.Surface);
                        variant.colorMapHashAfter = Hash(Read(source.HighResolutionMap)); variant.painterMapHashAfter = Hash(Read(source.HighResolutionPainterMap));
                        variant.journalHashAfter = source.Journal.PrefixHash; variant.lastSequenceAfter = source.Journal.LastSequence;
                        variant.coveredCellsAfter = source.Surface.CoveredCount;
                        Require(variant.authorityHashBefore == variant.authorityHashAfter && variant.colorMapHashBefore == variant.colorMapHashAfter
                            && variant.painterMapHashBefore == variant.painterMapHashAfter && variant.journalHashBefore == variant.journalHashAfter
                            && variant.lastSequenceBefore == variant.lastSequenceAfter && variant.coveredCellsBefore == variant.coveredCellsAfter,
                            "Contour preview changed its source authority, maps or sealed journal: " + stem);
                        variant.sourcePreserved = true; entry.variants.Add(variant);
                    }
                    Require(entry.variants[0].coveredCellsBefore == entry.variants[1].coveredCellsBefore,
                        "A local contour change changed the bounded paid cell amount: first-plus-" + repeats);
                    Require(entry.variants[0].authorityHashBefore != entry.variants[1].authorityHashBefore,
                        "The Round-alpha override did not change the CPU contour: first-plus-" + repeats);
                    entry.coveredCountEqual = entry.authorityHashesIntentionallyDifferent = true;
                    for (int view = 0; view < Views.Length; view++)
                        Save(directory, "first-plus-" + repeats + "-" + Views[view] + "-comparison.png",
                            LabPaintStudyRenderer.Combine(new[] { frames[view][row * 2], frames[view][row * 2 + 1] }));
                    report.entries.Add(entry);
                }
                for (int view = 0; view < Views.Length; view++)
                    Save(directory, Views[view] + "-first-and-rehits-grid.png", LabWatercolorStampStudy.CombineGrid(frames[view], 2));
                Require(report.sourcePixelsSha256 == Hash(master.GetPixels32()) && report.derivedMaskSha256 == Hash(mask.GetPixels32()),
                    "Preview changed its private source or derived mask pixels.");
                report.sourceAndDerivedMaskPreserved = true;
            }
            finally
            {
                foreach (Texture2D[] group in frames) foreach (Texture2D image in group) if (image != null) Object.DestroyImmediate(image);
                foreach (Texture2D texture in new[] { master, mask, nativeDarkness, nativeBands, darkness, bands }) if (texture != null) Object.DestroyImmediate(texture);
                Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == scenes.Length, "Loaded or active scene identity changed.");
                for (int i = 0; i < scenes.Length; i++) Require(SceneManager.GetSceneAt(i) == scenes[i] && scenes[i].isDirty == dirty[i], "Loaded scene or dirty state changed.");
                Require(UnityEngine.Random.state.Equals(randomState), "The private preview changed Unity's global random state.");
                foreach (Preserved item in preserved)
                {
                    item.afterSha256 = File.Exists(item.path) ? Hash(File.ReadAllBytes(item.path)) : "missing";
                    item.bytesEqual = item.beforeSha256 == item.afterSha256;
                    item.authoringEqual = item.authoringBeforeSha256 == AuthoringHash(item.path);
                    item.importerEqual = item.importerBeforeSha256 == ImporterHash(item.path);
                    Require(item.bytesEqual && item.authoringEqual && item.importerEqual, "Saved asset, authoring object or importer changed: " + item.path);
                }
            }
            report.loadedScenesPreserved = report.savedAssetsPreserved = report.authoringAndImportersPreserved = report.globalRandomStatePreserved = true;
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "contour-study.json"), json, new UTF8Encoding(false));
            return json;
        }

        private static Texture2D BuildAlphaMask(Texture2D source, out int thresholdPixels)
        {
            Color32[] original = source.GetPixels32(), pixels = new Color32[original.Length]; thresholdPixels = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, original[i].a);
                if (original[i].a / 255f >= MaskThreshold) thresholdPixels++;
            }
            Require(thresholdPixels > 0, "The selected Round alpha has no ink at the existing CPU threshold.");
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
                { name = "Private selected Round alpha CPU contour", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            try { texture.SetPixels32(pixels); texture.Apply(false, false);
                Require(pixels.SequenceEqual(texture.GetPixels32()), "Derived mask upload changed alpha or white RGB bytes."); return texture; }
            catch { Object.DestroyImmediate(texture); throw; }
        }
        private static Texture2D MipCopy(Texture2D source)
        {
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, true, true)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            try { Color[] pixels = source.GetPixels(); texture.SetPixels(pixels, 0); texture.Apply(true, false);
                Require(pixels.SequenceEqual(texture.GetPixels(0)), "Gourd mip clone changed native source base pixels."); return texture; }
            catch { Object.DestroyImmediate(texture); throw; }
        }
        private static void ApplySurface(Material material, LabWatercolorV3Snapshot snapshot)
        {
            snapshot.ApplySurface(material); material.SetFloat("_V3PigmentGrain", 0f);
            material.SetFloat("_V3PaperFiberInfluence", 0f); material.SetFloat("_V3PaperRelief", 0f);
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
        {
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try { image.SetPixels32(pixels); image.Apply(false, false); File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG()); }
            finally { Object.DestroyImmediate(image); }
        }
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
            public string unity, gpu, projectColorSpace, scope, comparisonContract, maskContract, sourceContract, surfaceContract, snapshotIdentity, sourceStyleVersion, visualIdentity;
            public string masterSha256, sourcePixelsSha256, derivedMaskSha256, darknessSha256, bandsSha256, darknessMipChainSha256, bandsMipChainSha256;
            public int sourceWidth, sourceHeight, maskThresholdPixels, maskPixelCount;
            public float maskThreshold, maskThresholdPixelFraction, sourceGain, enhancement, mediumGain, fineGain; public Vector4 tintDisplay;
            public string[] columns, views, shaderPaths, shaderHashes; public LabWatercolorStampSettings sourceSettings;
            public bool loadedScenesPreserved, savedAssetsPreserved, authoringAndImportersPreserved, sourceAndDerivedMaskPreserved, globalRandomStatePreserved;
            public List<Preserved> preservedFiles; public List<Entry> entries = new List<Entry>();
        }
        [Serializable] private sealed class Entry {
            public int repeats; public uint seed; public bool coveredCountEqual, authorityHashesIntentionallyDifferent;
            public List<Variant> variants = new List<Variant>();
        }
        [Serializable] private sealed class Variant {
            public string name, visualIdentity, maskIdentity, paintMapHash, authorityHashBefore, authorityHashAfter,
                colorMapHashBefore, colorMapHashAfter, painterMapHashBefore, painterMapHashAfter, journalHashBefore, journalHashAfter;
            public int coveredCellsBefore, coveredCellsAfter; public float coverage; public ulong lastSequenceBefore, lastSequenceAfter;
            public bool repeatedReplayExact, freshReplayExact, sourcePreserved;
        }
        [Serializable] private sealed class Preserved {
            public string path, beforeSha256, afterSha256, authoringBeforeSha256, importerBeforeSha256;
            public bool bytesEqual, authoringEqual, importerEqual;
        }
    }
}
#endif
