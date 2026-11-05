using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Presentation data only. Never rewrites a CPU gameplay pattern or a brush master.</summary>
    public static class LabWatercolorBrushBaker
    {
        public const int Size = 512, ContentSize = 384, Margin = (Size - ContentSize) / 2;
        public const string MasterDirectory = "Assets/Editor/WatercolorPaint/BrushMasters";
        public const string OutputDirectory = "Assets/Lab/Resources/GourdPaint/WatercolorBrushes";
        public const string DataVersion = "watercolor-stamp-data-v1";
        public static readonly string[] BrushNames = { "Round", "Growth", "Overlap" };

        [MenuItem("C6 Lab/Experiments/Bake Watercolor Brushes")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(OutputDirectory);
            foreach (string name in BrushNames)
            {
                string source = MasterDirectory + "/" + name + ".png";
                byte[] master = File.ReadAllBytes(source);
                Texture2D decoded = null, output = null;
                try
                {
                    decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                    decoded.hideFlags = HideFlags.HideAndDontSave;
                    if (!decoded.LoadImage(master, false)) throw new InvalidDataException("Cannot decode " + source);
                    Color32[] data = Bake(decoded.GetPixels32(), decoded.width, decoded.height);
                    output = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
                        { hideFlags = HideFlags.HideAndDontSave };
                    output.SetPixels32(data); output.Apply(false, false);
                    string path = OutputDirectory + "/" + name + ".png";
                    File.WriteAllBytes(path, output.EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    ConfigureImporter(path, false);
                    ConfigureImporter(source, true);
                    Require(master.SequenceEqual(File.ReadAllBytes(source)), "A master was modified by the bake.");
                }
                finally
                {
                    if (decoded != null) Object.DestroyImmediate(decoded);
                    if (output != null) Object.DestroyImmediate(output);
                }
            }
            File.WriteAllText(OutputDirectory + "/Version.txt", DataHash() + "\n");
            AssetDatabase.ImportAsset(OutputDirectory + "/Version.txt", ImportAssetOptions.ForceSynchronousImport);
            CreateProfileIfMissing();
            Validate();
            Debug.Log("C6_WATERCOLOR_BRUSH_BAKE count=3 size=512 content=384 margin=64 version=" + DataVersion);
        }

        /// <summary>
        /// R=pigment density A*(.28+1.7*(1-linear-data luminance)); G/A=coverage;
        /// B=distance inside coverage&gt;.02, normalized to 64 texels. Entire master fits the padded square.
        /// Area sampling preserves faint dry-brush marks that point sampling could lose.
        /// </summary>
        public static Color32[] Bake(Color32[] source, int width, int height)
        {
            if (source == null || width <= 0 || height <= 0 || source.Length != width * height)
                throw new ArgumentException("A complete RGBA source image is required.");
            var result = new Color32[Size * Size];
            var distance = new float[result.Length];
            float scale = ContentSize / (float)Math.Max(width, height);
            int targetWidth = Math.Max(1, (int)Math.Round(width * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(height * scale));
            int xOffset = (Size - targetWidth) / 2, yOffset = (Size - targetHeight) / 2;
            for (int y = 0; y < targetHeight; y++)
                for (int x = 0; x < targetWidth; x++)
                {
                    float x0 = x * width / (float)targetWidth, x1 = (x + 1) * width / (float)targetWidth;
                    float y0 = y * height / (float)targetHeight, y1 = (y + 1) * height / (float)targetHeight;
                    double coverage = 0d, density = 0d, area = (x1 - x0) * (y1 - y0);
                    for (int sy = (int)y0; sy < Math.Min(height, (int)Math.Ceiling(y1)); sy++)
                        for (int sx = (int)x0; sx < Math.Min(width, (int)Math.Ceiling(x1)); sx++)
                        {
                            double weight = Math.Max(0d, Math.Min(sx + 1d, x1) - Math.Max(sx, x0))
                                * Math.Max(0d, Math.Min(sy + 1d, y1) - Math.Max(sy, y0));
                            Color32 p = source[sy * width + sx];
                            double a = p.a / 255d;
                            double luminance = (.2126d * p.r + .7152d * p.g + .0722d * p.b) / 255d;
                            coverage += a * weight;
                            density += a * Math.Min(1d, Math.Max(0d, .28d + 1.7d * (1d - luminance))) * weight;
                        }
                    byte aByte = Quantize(coverage / area);
                    result[(y + yOffset) * Size + x + xOffset] = new Color32(Quantize(density / area), aByte, 0, aByte);
                }
            // Two-pass chamfer distance. The padded edge is always exterior, including sources
            // with tiny nonzero alpha at their original borders. No runtime CPU pixels are needed.
            for (int i = 0; i < result.Length; i++) distance[i] = result[i].g > 5 ? Size * 2 : 0f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    if (x > 0) distance[i] = Math.Min(distance[i], distance[i - 1] + 1f);
                    if (y > 0) distance[i] = Math.Min(distance[i], distance[i - Size] + 1f);
                    if (x > 0 && y > 0) distance[i] = Math.Min(distance[i], distance[i - Size - 1] + 1.41421356f);
                    if (x + 1 < Size && y > 0) distance[i] = Math.Min(distance[i], distance[i - Size + 1] + 1.41421356f);
                }
            for (int y = Size - 1; y >= 0; y--)
                for (int x = Size - 1; x >= 0; x--)
                {
                    int i = y * Size + x;
                    if (x + 1 < Size) distance[i] = Math.Min(distance[i], distance[i + 1] + 1f);
                    if (y + 1 < Size) distance[i] = Math.Min(distance[i], distance[i + Size] + 1f);
                    if (x + 1 < Size && y + 1 < Size) distance[i] = Math.Min(distance[i], distance[i + Size + 1] + 1.41421356f);
                    if (x > 0 && y + 1 < Size) distance[i] = Math.Min(distance[i], distance[i + Size - 1] + 1.41421356f);
                    Color32 p = result[i]; p.b = Quantize(Math.Min(1f, distance[i] / Margin)); result[i] = p;
                }
            return result;
        }

        public static void Validate()
        {
            ValidateDataContract();
            Require(File.Exists(OutputDirectory + "/Version.txt")
                && File.ReadAllText(OutputDirectory + "/Version.txt").Trim() == DataHash(),
                "The watercolor brush version is stale. Re-bake the brush data before building.");
            foreach (string name in BrushNames)
            {
                string path = OutputDirectory + "/" + name + ".png";
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Require(texture != null && texture.width == Size && texture.height == Size, "Missing 512 brush " + name);
                Require(importer != null && !importer.sRGBTexture && !importer.mipmapEnabled && !importer.isReadable
                    && importer.filterMode == FilterMode.Bilinear && importer.wrapMode == TextureWrapMode.Clamp
                    && importer.textureCompression == TextureImporterCompression.Uncompressed,
                    "Incorrect data import settings for " + name);
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    decoded.LoadImage(File.ReadAllBytes(path));
                    Color32[] pixels = decoded.GetPixels32();
                    int visible = 0, differentDensity = 0, deep = 0;
                    for (int y = 0; y < Size; y++)
                        for (int x = 0; x < Size; x++)
                        {
                            Color32 p = pixels[y * Size + x];
                            Require(p.a == p.g, "Coverage/alpha channel disagreement.");
                            Require(p.r <= p.g, "Density exceeds coverage.");
                            if (x < Margin || x >= Size - Margin || y < Margin || y >= Size - Margin)
                                Require(p.Equals(new Color32()), "Brush has no transparent safety margin.");
                            if (p.a > 8) { visible++; if (p.r + 8 < p.g) differentDensity++; if (p.b > 32) deep++; }
                        }
                    Require(visible > 10000 && differentDensity > 1000 && deep > 1000,
                        "Insufficient coverage, independent pigment density or interior distance for " + name);
                }
                finally { Object.DestroyImmediate(decoded); }
            }
        }

        public static string DataHash()
        {
            using (var buffer = new MemoryStream())
            {
                foreach (string name in BrushNames)
                {
                    byte[] bytes = File.ReadAllBytes(OutputDirectory + "/" + name + ".png");
                    buffer.Write(bytes, 0, bytes.Length);
                }
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static void ValidateDataContract()
        {
            var source = new[] {
                new Color32(255, 255, 255, 128), new Color32(0, 0, 0, 64),
                new Color32(255, 255, 255, 0), new Color32(0, 0, 0, 0)
            };
            Color32[] first = Bake(source, 2, 2), again = Bake(source, 2, 2);
            Require(first.SequenceEqual(again), "Identical master data did not bake identically.");
            Color32 light = first[(Margin + 96) * Size + Margin + 96];
            Color32 dark = first[(Margin + 96) * Size + Margin + 288];
            Require(light.a == 128 && light.g == 128 && light.r == 36,
                "White source pigment/coverage channels changed.");
            Require(dark.a == 64 && dark.g == 64 && dark.r == 64,
                "Dark source pigment no longer uses RGB together with alpha.");
            Require(first[0].Equals(new Color32()) && first[first.Length - 1].Equals(new Color32()),
                "The padded exterior is not fully transparent.");
        }

        public static string SourceHash(string name)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(MasterDirectory + "/" + name + ".png")))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void ConfigureImporter(string path, bool master)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); importer = (TextureImporter)AssetImporter.GetAtPath(path); }
            if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = master ? 2048 : Size;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.overridden = false; platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
        }

        public static void CreateProfileIfMissing()
        {
            const string path = "Assets/Lab/Resources/GourdPaint/WatercolorStampProfile.asset";
            if (File.Exists(path)) return;
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("C6Lab.LabWatercolorStampProfile"))
                .FirstOrDefault(t => t != null);
            if (type == null) return; // Runtime scripts may not yet be imported during an isolated bake.
            var profile = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static byte Quantize(double value) => (byte)Math.Max(0, Math.Min(255, Math.Round(value * 255d)));
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }

    /// <summary>Version checks are read-only: building never silently regenerates artwork.</summary>
    public sealed class LabWatercolorBrushBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { LabWatercolorBrushBaker.Validate(); }
            catch (Exception e) { throw new BuildFailedException("Watercolor brush data: " + e.Message); }
        }
    }
}
