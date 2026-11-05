using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Explicit Editor-only comparison bake. Masters and every V2 file remain read-only.</summary>
    public static class LabWatercolorV3Baker
    {
        public const int Size = 512, ContentSize = 384, Margin = 64, PaperSize = 1024;
        public const uint PaperSeed = 0x0C6A31F5u;
        public const string DataDirectory = "Assets/Editor/WatercolorPaint/V3/Data";
        public const string BrushDirectory = DataDirectory + "/Brushes";
        public const string PaperPath = DataDirectory + "/V3FineFiberPaper.png";
        public const string VersionPath = DataDirectory + "/Version.txt";
        public const string ProfilePath = DataDirectory + "/V3StudyProfile.asset";
        public const string DataVersion = "watercolor-v3-comparison-data-v2";
        public const string Encoding = "RGBA8 linear brush data; 512x512; centered 384x384 content; 64px transparent margin. "
            + "A=G=area-averaged original alpha. B=interior chamfer distance of G>5/255, normalized by 64px. "
            + "L=(.2126R+.7152G+.0722B)/255 from source numerical RGB; I=area-average(A*(1-L))/area-average(A). "
            + "P=box-filter(area-average(A*(1-L)),radius=18px)/box-filter(A,radius=18px). "
            + "R=A*saturate(.65*I+.35*P*P), with I=P=0 for zero coverage; no fixed density floor. "
            + "Paper=1024x1024 tileable warm RGB246/235/211, 42000 crossing fibers2-12px, detail[-10,+10] with channel clamping, alpha255, seed0x0C6A31F5; no baked shading or wrinkles. "
            + "Brush import=linear/no-mips/clamp/bilinear/RGBA32; paper import=sRGB/mips/repeat/trilinear/RGBA32.";
        public static readonly string[] BrushNames = { "Round", "Growth", "Overlap" };

        /// <summary>The only entry point that writes/imports V3 data. It never configures a master importer.</summary>
        [MenuItem("C6 Lab/Experiments/Watercolor V3/Bake Comparison Data")]
        public static void BakeAll()
        {
            Dictionary<string, string> immutable = CaptureOriginalHashes();
            Directory.CreateDirectory(BrushDirectory);
            foreach (string name in BrushNames)
            {
                string sourcePath = LabWatercolorBrushBaker.MasterDirectory + "/" + name + ".png";
                var source = Decode(sourcePath);
                try { WritePng(BrushDirectory + "/" + name + ".png", Bake(source.GetPixels32(), source.width, source.height), Size, false); }
                finally { Object.DestroyImmediate(source); }
            }
            WritePng(PaperPath, BakePaper(), PaperSize, true);
            File.WriteAllText(VersionPath, DataHash() + "\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(VersionPath, ImportAssetOptions.ForceSynchronousImport);
            CreateProfileIfMissing();
            Validate();
            RequireUnchanged(immutable);
            Debug.Log("C6_WATERCOLOR_V3_BAKE comparisonOnly=true brushes=3 content=384 margin=64 paper=1024 version=" + ReadBakedVersion());
        }

        /// <summary>
        /// The V2 area sampler and distance field supply G/A/B unchanged. R comes only from source
        /// grayscale pigment, with an alpha-aware 37x37 low-frequency pool; transparent and white
        /// source data have no fixed pigment floor. This never supplies a gameplay coverage pattern.
        /// </summary>
        public static Color32[] Bake(Color32[] source, int width, int height)
        {
            if (source == null || width <= 0 || height <= 0 || source.Length != width * height)
                throw new ArgumentException("A complete RGBA source image is required.");
            Color32[] result = LabWatercolorBrushBaker.Bake(source, width, height);
            var coverage = new double[result.Length];
            var pigment = new double[result.Length];
            float scale = ContentSize / (float)Math.Max(width, height);
            int targetWidth = Math.Max(1, (int)Math.Round(width * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(height * scale));
            int xOffset = (Size - targetWidth) / 2, yOffset = (Size - targetHeight) / 2;
            for (int y = 0; y < targetHeight; y++)
                for (int x = 0; x < targetWidth; x++)
                {
                    float x0 = x * width / (float)targetWidth, x1 = (x + 1) * width / (float)targetWidth;
                    float y0 = y * height / (float)targetHeight, y1 = (y + 1) * height / (float)targetHeight;
                    double a = 0d, ink = 0d, area = (x1 - x0) * (y1 - y0);
                    for (int sy = (int)y0; sy < Math.Min(height, (int)Math.Ceiling(y1)); sy++)
                        for (int sx = (int)x0; sx < Math.Min(width, (int)Math.Ceiling(x1)); sx++)
                        {
                            double weight = Math.Max(0d, Math.Min(sx + 1d, x1) - Math.Max(sx, x0))
                                * Math.Max(0d, Math.Min(sy + 1d, y1) - Math.Max(sy, y0));
                            Color32 p = source[sy * width + sx];
                            double alpha = p.a / 255d;
                            double light = (.2126d * p.r + .7152d * p.g + .0722d * p.b) / 255d;
                            a += alpha * weight; ink += alpha * (1d - light) * weight;
                        }
                    int i = (y + yOffset) * Size + x + xOffset;
                    coverage[i] = a / area; pigment[i] = ink / area;
                }
            double[] summedCoverage = Integral(coverage), summedPigment = Integral(pigment);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x; Color32 p = result[i];
                    if (p.a == 0) { p.r = 0; result[i] = p; continue; }
                    double ink = coverage[i] > 0d ? pigment[i] / coverage[i] : 0d;
                    double coveredPool = Box(summedCoverage, x, y, 18);
                    double pool = coveredPool > 0d ? Box(summedPigment, x, y, 18) / coveredPool : 0d;
                    // Quantized coverage bounds R as it does in the V2 data contract.
                    p.r = Quantize(p.a / 255d * Math.Clamp(.65d * ink + .35d * pool * pool, 0d, 1d));
                    result[i] = p;
                }
            return result;
        }

        /// <summary>Deterministic, tileable, fine fibers only; no low-frequency wrinkle or directional lighting field.</summary>
        public static Color32[] BakePaper()
        {
            var fine = new float[PaperSize * PaperSize];
            uint random = PaperSeed;
            // Densely crossing short fibers are only 2-12 texels long. Wrapping the splats makes the texture repeatable.
            for (int fiber = 0; fiber < 42000; fiber++)
            {
                float cx = Next(ref random) * PaperSize, cy = Next(ref random) * PaperSize;
                float angle = Next(ref random) * Mathf.PI * 2f;
                float length = 2f + Next(ref random) * 10f, dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
                float strength = (Next(ref random) - .65f) * 5f;
                int steps = Mathf.CeilToInt(length * 2f);
                for (int step = 0; step <= steps; step++)
                {
                    float t = step / (float)steps, weight = Mathf.Sin(t * Mathf.PI) * strength;
                    float x = cx + (t - .5f) * length * dx, y = cy + (t - .5f) * length * dy;
                    int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                    float fx = x - ix, fy = y - iy;
                    AddFiber(fine, ix, iy, (1f - fx) * (1f - fy) * weight);
                    AddFiber(fine, ix + 1, iy, fx * (1f - fy) * weight);
                    AddFiber(fine, ix, iy + 1, (1f - fx) * fy * weight);
                    AddFiber(fine, ix + 1, iy + 1, fx * fy * weight);
                }
            }
            var result = new Color32[fine.Length];
            for (int i = 0; i < result.Length; i++)
            {
                float speckle = (Mix((uint)i ^ PaperSeed) & 1023) / 1023f - .5f;
                int detail = Mathf.RoundToInt(Mathf.Clamp(fine[i] + speckle * 2.3f, -10f, 10f));
                result[i] = new Color32((byte)Mathf.Clamp(246 + detail, 0, 255),
                    (byte)(235 + detail), (byte)(211 + detail), 255);
            }
            return result;
        }

        public static LabWatercolorV3Profile LoadProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LabWatercolorV3Profile>(ProfilePath);
            if (profile == null) throw new InvalidOperationException("V3 study profile is missing. Run LabWatercolorV3Baker.BakeAll explicitly first.");
            return profile;
        }

        public static string ReadBakedVersion()
        {
            if (!File.Exists(VersionPath)) return "unbaked";
            string value = File.ReadAllText(VersionPath).Trim();
            return value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f') ? value : "unbaked";
        }

        /// <summary>SHA256 covers the exact channel/import encoding and every generated texture byte.</summary>
        public static string DataHash()
        {
            using (var buffer = new MemoryStream())
            {
                byte[] contract = EncodingBytes(); buffer.Write(contract, 0, contract.Length);
                foreach (string name in BrushNames)
                {
                    byte[] bytes = File.ReadAllBytes(BrushDirectory + "/" + name + ".png"); buffer.Write(bytes, 0, bytes.Length);
                }
                byte[] paper = File.ReadAllBytes(PaperPath); buffer.Write(paper, 0, paper.Length);
                return Hash(buffer.ToArray());
            }
        }

        public static void Validate()
        {
            ValidateDataContract();
            Require(ReadBakedVersion() != "unbaked" && ReadBakedVersion() == DataHash(), "V3 texture/encoding hash is stale. Re-bake the comparison data.");
            foreach (string name in BrushNames)
            {
                string path = BrushDirectory + "/" + name + ".png";
                RequireImport(path, false);
                var decoded = Decode(path);
                try
                {
                    Require(decoded.width == Size && decoded.height == Size, "V3 brush is not 512x512: " + name);
                    Color32[] pixels = decoded.GetPixels32(); int visible = 0, varied = 0, distance = 0;
                    for (int y = 0; y < Size; y++)
                        for (int x = 0; x < Size; x++)
                        {
                            Color32 p = pixels[y * Size + x];
                            Require(p.a == p.g && p.r <= p.g, "Invalid V3 coverage/density channels: " + name);
                            if (x < Margin || x >= Size - Margin || y < Margin || y >= Size - Margin)
                                Require(p.Equals(new Color32()), "V3 brush lacks a 64px transparent margin: " + name);
                            if (p.a > 8) { visible++; if (p.r + 8 < p.a) varied++; if (p.b > 32) distance++; }
                        }
                    Require(visible > 10000 && varied > 1000 && distance > 1000, "Insufficient V3 pigment/coverage/distance structure: " + name);
                    var source = Decode(LabWatercolorBrushBaker.MasterDirectory + "/" + name + ".png");
                    try { Require(pixels.SequenceEqual(Bake(source.GetPixels32(), source.width, source.height)), "V3 data differs from its deterministic bake: " + name); }
                    finally { Object.DestroyImmediate(source); }
                }
                finally { Object.DestroyImmediate(decoded); }
            }
            RequireImport(PaperPath, true);
            var paper = Decode(PaperPath);
            try
            {
                Require(paper.width == PaperSize && paper.height == PaperSize, "V3 paper must be 1024x1024.");
                Color32[] pixels = paper.GetPixels32();
                Require(pixels.SequenceEqual(BakePaper()), "V3 fine-fiber paper is not its deterministic, unshaded bake.");
                int minimum = 255, maximum = 0;
                foreach (Color32 p in pixels)
                {
                    Require(p.a == 255 && p.r >= p.g && p.g > p.b, "V3 paper has alpha holes or is not warm neutral RGB.");
                    minimum = Math.Min(minimum, p.r); maximum = Math.Max(maximum, p.r);
                }
                Require(maximum - minimum >= 2 && maximum - minimum <= 20, "V3 paper must keep a subtle fine-fiber range without baked shading.");
            }
            finally { Object.DestroyImmediate(paper); }
            LabWatercolorV3Profile profile = LoadProfile(); profile.Validate();
            Require(profile.roundBrush != null && profile.growthBrush != null && profile.overlapBrush != null && profile.paperTexture != null,
                "V3 study profile is missing a brush or paper reference. Rebaking preserves user edits; repair its references in the Inspector.");
        }

        public static void ValidateDataContract()
        {
            var source = new[] {
                new Color32(255,255,255,128), new Color32(0,0,0,64),
                new Color32(255,255,255,0), new Color32(0,0,0,0)
            };
            Color32[] result = Bake(source, 2, 2), repeat = Bake(source, 2, 2);
            Require(result.SequenceEqual(repeat), "V3 bake is not deterministic.");
            Color32 light = result[(Margin + 96) * Size + Margin + 96];
            Color32 dark = result[(Margin + 96) * Size + Margin + 288];
            Require(light.a == 128 && light.g == 128 && light.r == 0, "V3 white pigment still has a fixed density floor.");
            Require(dark.a == 64 && dark.g == 64 && dark.r == 64, "V3 black pigment/coverage contract changed.");
            Color32[] v2 = LabWatercolorBrushBaker.Bake(source, 2, 2);
            for (int i = 0; i < result.Length; i++)
                Require(result[i].g == v2[i].g && result[i].b == v2[i].b && result[i].a == v2[i].a,
                    "V3 changed the inherited G/B/A coverage or distance field.");
        }

        private static void CreateProfileIfMissing()
        {
            if (File.Exists(ProfilePath)) return; // Preserve every authored value and reference across rebakes.
            var profile = ScriptableObject.CreateInstance<LabWatercolorV3Profile>();
            profile.roundBrush = AssetDatabase.LoadAssetAtPath<Texture2D>(BrushDirectory + "/Round.png");
            profile.growthBrush = AssetDatabase.LoadAssetAtPath<Texture2D>(BrushDirectory + "/Growth.png");
            profile.overlapBrush = AssetDatabase.LoadAssetAtPath<Texture2D>(BrushDirectory + "/Overlap.png");
            profile.paperTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PaperPath);
            AssetDatabase.CreateAsset(profile, ProfilePath); AssetDatabase.SaveAssetIfDirty(profile);
        }
        private static void WritePng(string path, Color32[] pixels, int size, bool paper)
        {
            var output = new Texture2D(size, size, TextureFormat.RGBA32, false, !paper) { hideFlags = HideFlags.HideAndDontSave };
            try { output.SetPixels32(pixels); output.Apply(false, false); File.WriteAllBytes(path, output.EncodeToPNG()); }
            finally { Object.DestroyImmediate(output); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Missing V3 texture importer: " + path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = paper;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.mipmapEnabled = paper; importer.isReadable = false;
            importer.filterMode = paper ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.wrapMode = paper ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = size;
            var platform = importer.GetDefaultPlatformTextureSettings(); platform.overridden = false;
            platform.format = TextureImporterFormat.RGBA32; platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
        }
        private static void RequireImport(string path, bool paper)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(texture != null && importer != null && importer.sRGBTexture == paper && importer.mipmapEnabled == paper
                && !importer.isReadable && importer.alphaSource == TextureImporterAlphaSource.FromInput && !importer.alphaIsTransparency
                && importer.filterMode == (paper ? FilterMode.Trilinear : FilterMode.Bilinear)
                && importer.wrapMode == (paper ? TextureWrapMode.Repeat : TextureWrapMode.Clamp)
                && importer.textureCompression == TextureImporterCompression.Uncompressed && importer.npotScale == TextureImporterNPOTScale.None,
                "Incorrect V3 comparison texture settings: " + path);
            Require(texture.mipmapCount == (paper ? 11 : 1), "Incorrect V3 texture mip count: " + path);
        }
        private static Texture2D Decode(string path)
        {
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            if (decoded.LoadImage(File.ReadAllBytes(path), false)) return decoded;
            Object.DestroyImmediate(decoded); throw new InvalidDataException("Cannot decode V3/source texture: " + path);
        }
        private static Dictionary<string, string> CaptureOriginalHashes()
        {
            var result = new Dictionary<string, string>();
            foreach (string name in BrushNames)
            {
                string path = LabWatercolorBrushBaker.MasterDirectory + "/" + name + ".png";
                result[path] = Hash(File.ReadAllBytes(path));
            }
            if (Directory.Exists(LabWatercolorBrushBaker.OutputDirectory))
                foreach (string path in Directory.GetFiles(LabWatercolorBrushBaker.OutputDirectory, "*", SearchOption.AllDirectories))
                    result[path] = Hash(File.ReadAllBytes(path));
            const string profile = "Assets/Lab/Resources/GourdPaint/WatercolorStampProfile.asset";
            if (File.Exists(profile)) result[profile] = Hash(File.ReadAllBytes(profile));
            return result;
        }
        private static void RequireUnchanged(Dictionary<string, string> hashes)
        {
            foreach (var pair in hashes)
                Require(File.Exists(pair.Key) && Hash(File.ReadAllBytes(pair.Key)) == pair.Value, "V3 bake changed an original or V2 file: " + pair.Key);
        }
        private static double[] Integral(double[] source)
        {
            int stride = Size + 1; var result = new double[stride * stride];
            for (int y = 0; y < Size; y++)
            {
                double row = 0d;
                for (int x = 0; x < Size; x++) { row += source[y * Size + x]; result[(y + 1) * stride + x + 1] = result[y * stride + x + 1] + row; }
            }
            return result;
        }
        private static double Box(double[] integral, int x, int y, int radius)
        {
            int stride = Size + 1, x0 = Math.Max(0, x - radius), y0 = Math.Max(0, y - radius);
            int x1 = Math.Min(Size, x + radius + 1), y1 = Math.Min(Size, y + radius + 1);
            return integral[y1 * stride + x1] - integral[y0 * stride + x1] - integral[y1 * stride + x0] + integral[y0 * stride + x0];
        }
        private static void AddFiber(float[] values, int x, int y, float value)
            => values[((y % PaperSize + PaperSize) % PaperSize) * PaperSize + (x % PaperSize + PaperSize) % PaperSize] += value;
        private static uint Mix(uint x) { x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; return x ^ x >> 16; }
        private static float Next(ref uint state) { state = Mix(state + 0x9E3779B9u); return (state & 0xFFFFFFu) / 16777216f; }
        private static byte Quantize(double value) => (byte)Math.Max(0, Math.Min(255, Math.Round(value * 255d)));
        private static byte[] EncodingBytes() => System.Text.Encoding.UTF8.GetBytes(DataVersion + "\n" + Encoding + "\n");
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
