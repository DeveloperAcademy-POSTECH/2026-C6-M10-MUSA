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
    /// <summary>Disposable, flat single-stamp inspection. Never installs a gameplay default.</summary>
    public static class LabWatercolorFlatTextureStudy
    {
        public const string ShaderPath = "Assets/Editor/WatercolorPaint/TextureStudy/WatercolorFlatTexture.shader";
        private const int Size = 1024, DetailSize = 512;
        private static readonly string[] Columns = { "current-transmission-v3-bake", "faithful-original", "enhanced-original" };

        [MenuItem("C6 Lab/Experiments/Watercolor Flat Texture Comparison")]
        private static void ExportMenu()
        {
            string directory = EditorUtility.OpenFolderPanel("Flat watercolor texture comparison", "", "");
            if (string.IsNullOrEmpty(directory)) return;
            try { Export(directory); EditorUtility.RevealInFinder(Path.Combine(directory, "flat-texture-study.json")); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static string Export(string directory)
        {
            Require(!string.IsNullOrWhiteSpace(directory) && Path.IsPathRooted(directory), "An absolute evidence directory is required.");
            directory = Path.GetFullPath(directory);
            Require(!directory.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && directory != Path.GetFullPath(Application.dataPath), "Evidence must be outside Assets to protect authoring inputs.");
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling, "Use the stopped Editor after compilation completes.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
                && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat), "An Editor GPU with ARGBFloat rendering is required.");
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i)).ToArray();
            var dirty = scenes.Select(scene => scene.isDirty).ToArray();
            Scene active = SceneManager.GetActiveScene();
            var preserved = CapturePreserved(scenes);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null && shader.isSupported, "The flat study shader must be installed and supported.");
            Directory.CreateDirectory(directory);
            var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                projectColorSpace = QualitySettings.activeColorSpace.ToString(), shader = ShaderPath, shaderSha256 = Hash(File.ReadAllBytes(ShaderPath)),
                scope = "Steps 1-2 only: source/channel/bake diagnostics and one isolated flat Round stamp. Opaque composition on uniform neutral paper; no scene objects, lighting, gloss, geometry, gameplay, network, mobile or performance validation.",
                numericalData = "Decode PNG bytes into RGBA32(false mipmaps, true linear). Source numerical grayscale and alpha are data, never gamma-decoded. Shader data RGB=A*(1-sourceRGB) componentwise; scalar diagnostic density=A*(1-L), L=.2126R+.7152G+.0722B. Raw maps use numerical bytes; source PNG is a separate unmodified reference, not a render.",
                displayEncoding = "Shader emits linear RGB into ARGBFloat/Linear with GL.sRGBWrite=false. Read back RGBAFloat/linear; apply Color.gamma exactly once to PNG RGB, opaque alpha. Raw .rgba-f32 files retain GPU linear float32 RGBA, native endianness, bottom-left first.",
                registration = "Each render is 1024x1024. All variants share shader UV registration and identical numerical pigment/paper vectors. Current formula uses a fixed seed, V3 data and the explicit first-hit Transmission arithmetic; no paid-cell mask or final-shell effects. Its .02 minimum wash therefore affects the whole quad and is not a claim about the gameplay silhouette. The source modes share SourceGain6; source-derived medium contrast is enhanced while the finest residual is reduced15%. Detail is a centered 512x512 crop of each rendered frame without resampling.",
                resolution = Size, cropX = (Size - DetailSize) / 2, cropY = (Size - DetailSize) / 2, cropSize = DetailSize,
                columns = Columns, rawFloatLittleEndian = BitConverter.IsLittleEndian, preservedFiles = preserved };
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            Texture2D master = null, v2 = null, v3 = null, darkness = null, bands = null;
            try
            {
                Configure(material, report);
                foreach (string name in LabWatercolorBrushBaker.BrushNames)
                    ExportDiagnostics(directory, name, report);
                master = Decode(LabWatercolorBrushBaker.MasterDirectory + "/Round.png");
                v2 = Decode(LabWatercolorBrushBaker.OutputDirectory + "/Round.png");
                material.SetTexture("_CurrentBrushV2", v2);
                v3 = Decode(LabWatercolorV3Baker.BrushDirectory + "/Round.png");
                BuildSourceMaps(master, out darkness, out bands);
                material.SetTexture("_CurrentBrush", v3);
                material.SetTexture("_SourceDarkness", darkness); material.SetTexture("_SourceBands", bands);
                File.WriteAllBytes(Path.Combine(directory, "round-source-darkness.rgba-f32"), darkness.GetRawTextureData());
                File.WriteAllBytes(Path.Combine(directory, "round-source-bands.rgba-f32"), bands.GetRawTextureData());
                report.darknessSha256 = Hash(darkness.GetRawTextureData()); report.bandsSha256 = Hash(bands.GetRawTextureData());
                VerifyFidelity(directory, master, material, report);
                var diagnostic = new Color32[3][];
                for(int mode=3;mode<=5;mode++) { material.SetFloat("_Mode", mode); byte[] ignored; diagnostic[mode-3]=Render(master,material,out ignored); }
                SaveStrip(directory,"round-density-path-comparison.png",diagnostic,Size,Size,0,0,Size);
                SaveStrip(directory,"round-density-path-detail-1to1.png",diagnostic,Size,Size,report.cropX,report.cropY,DetailSize);
                var images = new Color32[Columns.Length][];
                for (int mode = 0; mode < Columns.Length; mode++)
                {
                    material.SetFloat("_Mode", mode);
                    byte[] raw, replayRaw;
                    images[mode] = Render(master, material, out raw);
                    Color32[] replay = Render(master, material, out replayRaw);
                    Require(raw.SequenceEqual(replayRaw) && images[mode].SequenceEqual(replay), "Repeated flat render changed GPU or display bytes: " + Columns[mode]);
                    string stem = "round-" + Columns[mode];
                    SavePixels(directory, stem + ".png", images[mode], Size, Size);
                    File.WriteAllBytes(Path.Combine(directory, stem + ".rgba-f32"), raw);
                    report.variants.Add(new Variant { name = Columns[mode], linearFloatSha256 = Hash(raw),
                        pngSha256 = Hash(File.ReadAllBytes(Path.Combine(directory, stem + ".png"))), repeatedRenderExact = true });
                }
                SaveStrip(directory, "round-flat-comparison.png", images, Size, Size, 0, 0, Size);
                SaveStrip(directory, "round-flat-detail-1to1.png", images, Size, Size, report.cropX, report.cropY, DetailSize);
            }
            finally
            {
                if (master != null) Object.DestroyImmediate(master);
                if (v2 != null) Object.DestroyImmediate(v2);
                if (v3 != null) Object.DestroyImmediate(v3);
                if (darkness != null) Object.DestroyImmediate(darkness);
                if (bands != null) Object.DestroyImmediate(bands);
                Object.DestroyImmediate(material);
                Require(SceneManager.GetActiveScene() == active && SceneManager.sceneCount == scenes.Length, "Loaded/active scene identity changed.");
                for (int i = 0; i < scenes.Length; i++)
                    Require(SceneManager.GetSceneAt(i) == scenes[i] && scenes[i].isDirty == dirty[i], "Loaded scene or dirty state changed.");
                foreach (Preserved item in preserved)
                {
                    item.afterSha256 = File.Exists(item.path) ? Hash(File.ReadAllBytes(item.path)) : "missing";
                    item.bytesEqual = item.beforeSha256 == item.afterSha256;
                    item.authoringEqual = item.authoringBeforeSha256 == AuthoringHash(item.path);
                    item.importerEqual = item.importerBeforeSha256 == ImporterHash(item.path);
                    Require(item.bytesEqual && item.authoringEqual && item.importerEqual, "Authoring input changed: " + item.path);
                }
            }
            report.loadedScenesPreserved = report.savedAssetsPreserved = report.authoringAndImportersPreserved = true;
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "flat-texture-study.json"), json, new UTF8Encoding(false));
            return json;
        }

        private static void Configure(Material material, Report report)
        {
            material.SetVector("_TintDisplay", new Vector4(222f/255f, 45f/255f, 38f/255f, 1f));
            material.SetVector("_PaperDisplay", Vector4.one);
            material.SetFloat("_SourceGain", 6f); material.SetFloat("_Enhancement", 1f);
            material.SetFloat("_MediumGain", 1.4f); material.SetFloat("_FineGain", .85f);
            report.parameters = "Display tint sRGB #DE2D26, white paper. Both source modes share SourceGain=6 as an explicit display-dose adjustment, not an absolute source-preservation claim. Enhancement=1, MediumGain=1.4, FineGain=.85. Fixed current Transmission first-hit controls: cap=.95, load=1.8, density=.9, contrast=1.6, saturation=1.15, overlap=0, flowStrength=.2, flowScale=4, edgeStrength=.55, edgeWidth=.025, poolStrength=.5. SourceDarkness RGB=A*(1-sourceRGB), A=sourceA. SourceBands scalarD=1-luminance(numericalRGB); alpha-aware clipped box blur B_r=sum(A*scalarD)/sum(A), zero when no alpha; R=B18,G=B4-B18,B=scalarD-B4,A=smoothstep(.1,.9,A).";
        }

        private static void VerifyFidelity(string directory, Texture2D master, Material material, Report report)
        {
            material.SetFloat("_Mode", 1f); material.SetFloat("_SourceGain", 1f); material.SetVector("_TintDisplay", new Vector4(0,0,0,1));
            byte[] nativeRaw;
            Color32[] native = Render(master, material, out nativeRaw, master.width, master.height), source = master.GetPixels32();
            int maximum = 0;
            for (int i = 0; i < source.Length; i++)
            {
                Color p = source[i]; Color32 expected = new Color(1f-p.a*(1f-p.r), 1f-p.a*(1f-p.g), 1f-p.a*(1f-p.b), 1f);
                maximum = Math.Max(maximum, Math.Max(Math.Abs(native[i].r-expected.r), Math.Max(Math.Abs(native[i].g-expected.g), Math.Abs(native[i].b-expected.b))));
            }
            Require(maximum <= 1, "Native black/gain1 source reproduction exceeded one display byte of conversion error.");
            report.sourceReferenceMaxByteError = maximum;
            SavePixels(directory, "round-faithful-black-gain1-native-render.png", native, master.width, master.height);
            File.WriteAllBytes(Path.Combine(directory, "round-faithful-black-gain1-native-render.rgba-f32"), nativeRaw);
            Configure(material, report); material.SetFloat("_Mode", 1f);
            byte[] faithfulRaw, disabledRaw; Color32[] faithful = Render(master, material, out faithfulRaw);
            material.SetFloat("_Mode", 2f); material.SetFloat("_Enhancement", 0f);
            Color32[] disabled = Render(master, material, out disabledRaw);
            Require(faithfulRaw.SequenceEqual(disabledRaw) && faithful.SequenceEqual(disabled), "Enhancement=0 must exactly reproduce faithful mode.");
            report.enhancementZeroExact = true; Configure(material, report);
            material.SetFloat("_Mode",2f);
            byte[] supportRaw; Color32[] enhancedNative=Render(master,material,out supportRaw,master.width,master.height);
            int supportChecked=0, leaks=0;
            for(int i=0;i<source.Length;i++)
            {
                Color32 p=source[i];
                if(p.a!=0 && (p.r!=255 || p.g!=255 || p.b!=255)) continue;
                supportChecked++;
                if(enhancedNative[i].r!=255 || enhancedNative[i].g!=255 || enhancedNative[i].b!=255) leaks++;
            }
            Require(supportChecked>0 && leaks==0,"The enhanced source creates pigment in transparent or pure white original pixels.");
            report.emptyOrWhiteSourcePixelsChecked=supportChecked; report.pigmentLeakPixels=leaks;
            Configure(material,report);
        }

        internal static void BuildSourceMaps(Texture2D master, out Texture2D darkness, out Texture2D bands)
        {
            int width = master.width, height = master.height, stride = width + 1;
            Color32[] source = master.GetPixels32(); var colors = new Color[source.Length]; var values = new float[source.Length];
            var summedAlpha = new double[(width+1)*(height+1)]; var summedDensity = new double[summedAlpha.Length];
            for (int y = 0; y < height; y++)
            {
                double rowAlpha = 0, rowDensity = 0;
                for (int x = 0; x < width; x++)
                {
                    int i = y*width+x; Color p = source[i]; float d = 1f-(.2126f*p.r+.7152f*p.g+.0722f*p.b); values[i] = d;
                    colors[i] = new Color(p.a*(1f-p.r), p.a*(1f-p.g), p.a*(1f-p.b), p.a);
                    rowAlpha += p.a; rowDensity += p.a*d;
                    summedAlpha[(y+1)*stride+x+1] = summedAlpha[y*stride+x+1]+rowAlpha;
                    summedDensity[(y+1)*stride+x+1] = summedDensity[y*stride+x+1]+rowDensity;
                }
            }
            darkness = FloatMap(colors, width, height);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y*width+x; float coarse = Blur(summedAlpha, summedDensity, width, height, x, y, 18);
                float medium = Blur(summedAlpha, summedDensity, width, height, x, y, 4), t = Mathf.Clamp01((source[i].a/255f-.1f)/.8f);
                colors[i] = new Color(coarse, medium-coarse, values[i]-medium, t*t*(3f-2f*t));
            }
            bands = FloatMap(colors, width, height);
        }
        private static float Blur(double[] alpha, double[] density, int width, int height, int x, int y, int radius)
        {
            int stride = width+1, x0 = Math.Max(0,x-radius), x1 = Math.Min(width,x+radius+1), y0 = Math.Max(0,y-radius), y1 = Math.Min(height,y+radius+1);
            double a = alpha[y1*stride+x1]-alpha[y0*stride+x1]-alpha[y1*stride+x0]+alpha[y0*stride+x0];
            double d = density[y1*stride+x1]-density[y0*stride+x1]-density[y1*stride+x0]+density[y0*stride+x0];
            return a > 0 ? (float)(d/a) : 0f;
        }
        private static Texture2D FloatMap(Color[] values, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(values); texture.Apply(false, false); return texture;
        }

        private static void ExportDiagnostics(string directory, string name, Report report)
        {
            string stem = name.ToLowerInvariant();
            string[] paths = { LabWatercolorBrushBaker.MasterDirectory + "/" + name + ".png",
                LabWatercolorBrushBaker.OutputDirectory + "/" + name + ".png", LabWatercolorV3Baker.BrushDirectory + "/" + name + ".png" };
            string[] labels = { "original-reference", "v2-data-raw", "v3-data-raw" };
            Color32[] source = null; int sourceWidth = 0, sourceHeight = 0;
            for (int input = 0; input < paths.Length; input++)
            {
                byte[] bytes = File.ReadAllBytes(paths[input]);
                File.WriteAllBytes(Path.Combine(directory, stem + "-" + labels[input] + ".png"), bytes);
                Texture2D texture = Decode(paths[input]);
                try
                {
                    Color32[] pixels = texture.GetPixels32();
                    if (input == 0) { source = pixels; sourceWidth = texture.width; sourceHeight = texture.height; }
                    bool matchesPureBake = input == 0 || pixels.SequenceEqual(input == 1
                        ? LabWatercolorBrushBaker.Bake(source, sourceWidth, sourceHeight) : LabWatercolorV3Baker.Bake(source, sourceWidth, sourceHeight));
                    var alpha = new Color32[pixels.Length]; var density = new Color32[pixels.Length];
                    for (int p = 0; p < pixels.Length; p++)
                    {
                        Color32 v = pixels[p]; byte d = input == 0 ? (byte)Mathf.RoundToInt(v.a * (1f - (.2126f*v.r + .7152f*v.g + .0722f*v.b)/255f)) : v.r;
                        alpha[p] = new Color32(v.a, v.a, v.a, 255); density[p] = new Color32(d, d, d, 255);
                    }
                    string prefix = stem + "-" + (input == 0 ? "original" : input == 1 ? "v2" : "v3");
                    SavePixels(directory, prefix + "-alpha-numerical.png", alpha, texture.width, texture.height);
                    SavePixels(directory, prefix + "-density-numerical.png", density, texture.width, texture.height);
                    report.inputs.Add(new Input { brush = name, role = labels[input], path = paths[input], sha256 = Hash(bytes),
                        decodedRgbaSha256 = Hash(texture.GetRawTextureData()), width = texture.width, height = texture.height,
                        pureBakeComparisonApplicable = input != 0, matchesPureBake = matchesPureBake });
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }

        private static Color32[] Render(Texture2D master, Material material, out byte[] raw, int width = Size, int height = Size)
        {
            RenderTexture previous = RenderTexture.active; bool previousSrgbWrite = GL.sRGBWrite;
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1 };
            var read = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                target.Create(); GL.sRGBWrite = false; Graphics.Blit(master, target, material, 0);
                RenderTexture.active = target; read.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); read.Apply(false, false);
                raw = read.GetRawTextureData(); Color[] linear = read.GetPixels(); var display = new Color32[linear.Length];
                for (int i = 0; i < display.Length; i++)
                {
                    Color c = linear[i];
                    Require(float.IsFinite(c.r) && float.IsFinite(c.g) && float.IsFinite(c.b) && Mathf.Abs(c.a - 1f) < .00001f, "Render must be finite opaque linear color.");
                    Color encoded = c.gamma; encoded.a = 1f; display[i] = encoded;
                }
                return display;
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite; RenderTexture.active = previous;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(read);
            }
        }

        internal static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (texture.LoadImage(File.ReadAllBytes(path), false)) return texture;
            Object.DestroyImmediate(texture); throw new InvalidDataException("Cannot decode numerical texture: " + path);
        }
        private static void SavePixels(string directory, string name, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try { texture.SetPixels32(pixels); texture.Apply(false, false); File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }
        private static void SaveStrip(string directory, string name, Color32[][] images, int width, int height, int x0, int y0, int crop)
        {
            Require(x0 >= 0 && y0 >= 0 && x0 + crop <= width && y0 + crop <= height, "Crop is outside rendered pixels.");
            var strip = new Color32[crop * crop * images.Length];
            for (int i = 0; i < images.Length; i++) for (int y = 0; y < crop; y++)
                Array.Copy(images[i], (y0 + y)*width + x0, strip, y*crop*images.Length + i*crop, crop);
            SavePixels(directory, name, strip, crop * images.Length, crop);
        }

        private static List<Preserved> CapturePreserved(Scene[] scenes)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string folder in new[] { "Assets/Editor/WatercolorPaint", "Assets/Lab/Resources/GourdPaint" })
                if (Directory.Exists(folder)) foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) paths.Add(path);
            foreach (string path in new[] { "Assets/Lab/Scenes/Lab.unity", "Assets/Hapio/Scenes/Battle.unity",
                AssetDatabase.GetAssetPath(Resources.Load<LabConfig>("LabConfig")) }.Concat(scenes.Select(scene => scene.path)))
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            foreach (string path in paths.ToArray()) if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
            return paths.Where(File.Exists).OrderBy(path => path, StringComparer.Ordinal).Select(path => new Preserved {
                path = path, beforeSha256 = Hash(File.ReadAllBytes(path)), authoringBeforeSha256 = AuthoringHash(path), importerBeforeSha256 = ImporterHash(path) }).ToList();
        }
        private static string AuthoringHash(string path)
        {
            if (!path.EndsWith(".asset", StringComparison.Ordinal) && !path.EndsWith(".mat", StringComparison.Ordinal)) return null;
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            return asset == null ? null : Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(asset)));
        }
        private static string ImporterHash(string path)
        {
            if (path.EndsWith(".meta", StringComparison.Ordinal)) return null;
            AssetImporter importer = AssetImporter.GetAtPath(path);
            return importer == null ? null : Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(importer)));
        }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        [Serializable] private sealed class Report
        {
            public string unity, gpu, projectColorSpace, shader, shaderSha256, scope, numericalData, displayEncoding, registration, parameters, darknessSha256, bandsSha256;
            public int resolution, cropX, cropY, cropSize, sourceReferenceMaxByteError, emptyOrWhiteSourcePixelsChecked, pigmentLeakPixels;
            public string[] columns;
            public bool loadedScenesPreserved, savedAssetsPreserved, authoringAndImportersPreserved, enhancementZeroExact, rawFloatLittleEndian;
            public List<Preserved> preservedFiles; public List<Input> inputs = new List<Input>(); public List<Variant> variants = new List<Variant>();
        }
        [Serializable] private sealed class Input { public string brush, role, path, sha256, decodedRgbaSha256; public int width, height; public bool pureBakeComparisonApplicable, matchesPureBake; }
        [Serializable] private sealed class Variant { public string name, linearFloatSha256, pngSha256; public bool repeatedRenderExact; }
        [Serializable] private sealed class Preserved
        {
            public string path, beforeSha256, afterSha256, authoringBeforeSha256, importerBeforeSha256;
            public bool bytesEqual, authoringEqual, importerEqual;
        }
    }
}
