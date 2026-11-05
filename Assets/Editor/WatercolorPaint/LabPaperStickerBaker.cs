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
    /// <summary>
    /// Bakes one shared logical/visual silhouette for the approved opaque paper splash.
    /// The analytic union is a numerical mask, not a replacement for the approved art.
    /// Existing watercolor masters, CPU patterns, profiles and importers are never edited.
    /// </summary>
    public static class LabPaperStickerBaker
    {
        public const int Size = 512;
        public const int SafetyMargin = 24;
        public const float CpuThreshold = .15f;
        public const string DataVersion = "opaque-paper-splatter-data-v1";
        public const string OutputDirectory = "Assets/Lab/Resources/GourdPaint/PaperSticker";
        public const string MaskPath = OutputDirectory + "/SplatterMask.png";
        public const string DataPath = OutputDirectory + "/SplatterData.png";
        public const string BroadDataPath = OutputDirectory + "/BroadData.png";
        public const string FlowDataPath = OutputDirectory + "/FlowData.png";
        public const string BroadMaskPath = "Assets/Lab/Resources/GourdPaint/Patterns/Broad.png";
        public const string FlowMaskPath = "Assets/Lab/Resources/GourdPaint/Patterns/Flow.png";
        public const string VersionPath = OutputDirectory + "/Version.txt";
        public const string PaperSourcePath = "Assets/Editor/WatercolorPaint/V3/Data/V3FineFiberPaper.png";

        // End radius, angle and two radii make thick asymmetric fingers with rounded ends.
        // Their roots overlap the central lobe widely; isolated droplets are listed separately.
        private static readonly Finger[] Fingers = {
            new Finger(-172f, .340f, .035f, .039f),
            new Finger(-138f, .275f, .050f, .045f),
            new Finger(-108f, .414f, .031f, .042f),
            new Finger( -70f, .306f, .041f, .050f),
            new Finger( -39f, .389f, .029f, .039f),
            new Finger(  -7f, .400f, .030f, .035f),
            new Finger(  22f, .300f, .040f, .047f),
            new Finger(  59f, .400f, .033f, .043f),
            new Finger(  84f, .375f, .027f, .038f),
            new Finger( 126f, .290f, .047f, .052f),
            new Finger( 155f, .400f, .028f, .035f)
        };
        private static readonly Vector3[] Droplets = {
            new Vector3(.882f, .730f, .016f),
            new Vector3(.763f, .886f, .010f),
            new Vector3(.374f, .925f, .013f),
            new Vector3(.145f, .797f, .017f),
            new Vector3(.074f, .577f, .012f),
            new Vector3(.109f, .317f, .011f),
            new Vector3(.246f, .120f, .018f),
            new Vector3(.578f, .068f, .010f),
            new Vector3(.890f, .335f, .014f)
        };

        [MenuItem("C6 Lab/Experiments/Bake Opaque Paper Sticker")]
        public static void BakeAll()
        {
            byte[] originalPaper = File.ReadAllBytes(PaperSourcePath);
            AssetImporter sourceImporter = AssetImporter.GetAtPath(PaperSourcePath);
            string originalImporter = sourceImporter != null ? EditorJsonUtility.ToJson(sourceImporter) : null;
            Texture2D paper = null;
            try
            {
                paper = Decode(originalPaper);
                Build(paper, out Color32[] mask, out Color32[] data);
                EnsureFolder(OutputDirectory);
                SavePixels(MaskPath, mask, true);
                SavePixels(DataPath, data, false);
                BakeUnchangedPattern(BroadMaskPath, BroadDataPath, paper);
                BakeUnchangedPattern(FlowMaskPath, FlowDataPath, paper);
                File.WriteAllText(VersionPath, DataHash() + "\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(VersionPath, ImportAssetOptions.ForceSynchronousImport);
                Validate();
                Require(originalPaper.SequenceEqual(File.ReadAllBytes(PaperSourcePath)), "Sticker baking modified the source paper bytes.");
                Require(originalImporter == (sourceImporter != null ? EditorJsonUtility.ToJson(sourceImporter) : null),
                    "Sticker baking modified the source paper importer.");
            }
            finally { if (paper != null) Object.DestroyImmediate(paper); }
            Debug.Log("C6_PAPER_STICKER_BAKE size=512 channels=coverage,paper,distance,coverage version=" + DataVersion);
        }

        /// <summary>
        /// CPU: white RGB plus coverage Alpha. GPU: R/A=same coverage bytes,
        /// G=.5-centred paper grain over the ENTIRE square, B=interior distance/64 pixels.
        /// There is no historical .75 content scaling: both use the complete source UV.
        /// </summary>
        public static void Build(Texture2D paper, out Color32[] mask, out Color32[] data)
        {
            if (paper == null || !paper.isReadable || paper.width < 2 || paper.height < 2)
                throw new ArgumentException("A readable paper source is required.", nameof(paper));
            mask = BuildMask();
            var grain = BuildGrain(paper.GetPixels32(), paper.width, paper.height, Size, Size);
            var distance = new float[mask.Length];
            for (int i = 0; i < distance.Length; i++) distance[i] = mask[i].a / 255f >= CpuThreshold ? Size * 2f : 0f;
            InteriorDistance(distance, Size, Size);
            data = new Color32[mask.Length];
            for (int i = 0; i < data.Length; i++)
                data[i] = new Color32(mask[i].a, grain[i], Quantize(Math.Min(1f, distance[i] / 64f)), mask[i].a);
        }

        public static Color32[] BuildMask()
        {
            var result = new Color32[Size * Size];
            // Four deterministic subpixel samples retain smooth rounded finger ends and
            // circular satellite drops without changing the runtime threshold convention.
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (SignedInk(new Vector2((x + .25f + sx * .5f) / Size,
                                (y + .25f + sy * .5f) / Size)) >= 0f) inside++;
                    result[y * Size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 4));
                }
            return result;
        }

        private static float SignedInk(Vector2 uv)
        {
            Vector2 p = uv - new Vector2(.5f, .5f);
            float angle = Mathf.Atan2(p.y, p.x);
            float radius = .249f + .024f * Mathf.Sin(angle * 3f + .65f)
                + .015f * Mathf.Sin(angle * 5f - .4f) + .010f * Mathf.Sin(angle * 7f + 1.2f);
            float ink = radius - new Vector2(p.x / 1.07f, p.y / .96f).magnitude;
            for (int i = 0; i < Fingers.Length; i++)
            {
                Finger finger = Fingers[i];
                Vector2 direction = finger.Direction;
                Vector2 start = direction * .190f, end = direction * finger.Length;
                // Slightly offset the root to avoid a radial emblem / perfect starburst.
                Vector2 tangent = new Vector2(-direction.y, direction.x);
                start += tangent * ((i % 3 - 1) * .018f);
                float projected = Mathf.Clamp01(Vector2.Dot(p - start, end - start) / (end - start).sqrMagnitude);
                Vector2 point = Vector2.LerpUnclamped(start, end, projected);
                float stemRadius = Mathf.Lerp(finger.StemRadius * 1.35f, finger.StemRadius, projected);
                ink = Mathf.Max(ink, stemRadius - Vector2.Distance(p, point));
                ink = Mathf.Max(ink, finger.TipRadius - Vector2.Distance(p, end));
            }
            for (int i = 0; i < Droplets.Length; i++)
            {
                Vector3 drop = Droplets[i];
                ink = Mathf.Max(ink, drop.z - Vector2.Distance(uv, new Vector2(drop.x, drop.y)));
            }
            return ink;
        }

        private static byte[] BuildGrain(Color32[] source, int width, int height, int targetWidth, int targetHeight)
        {
            var values = new double[targetWidth * targetHeight];
            double sum = 0d;
            // Original source is numerical paper data. Preserve its fine texture with
            // an area average rather than sampling a display/gamma converted importer.
            for (int y = 0; y < targetHeight; y++)
                for (int x = 0; x < targetWidth; x++)
                {
                    double x0 = x * width / (double)targetWidth, x1 = (x + 1d) * width / targetWidth;
                    double y0 = y * height / (double)targetHeight, y1 = (y + 1d) * height / targetHeight;
                    double luma = 0d, weightSum = 0d;
                    for (int sy = (int)y0; sy < Math.Min(height, (int)Math.Ceiling(y1)); sy++)
                        for (int sx = (int)x0; sx < Math.Min(width, (int)Math.Ceiling(x1)); sx++)
                        {
                            double weight = Math.Max(0d, Math.Min(sx + 1d, x1) - Math.Max(sx, x0))
                                * Math.Max(0d, Math.Min(sy + 1d, y1) - Math.Max(sy, y0));
                            Color32 p = source[sy * width + sx];
                            luma += (.2126d * p.r + .7152d * p.g + .0722d * p.b) / 255d * weight;
                            weightSum += weight;
                        }
                    values[y * targetWidth + x] = luma / Math.Max(weightSum, 1e-12d);
                    sum += values[y * targetWidth + x];
                }
            double mean = sum / values.Length, variance = 0d;
            for (int i = 0; i < values.Length; i++) variance += (values[i] - mean) * (values[i] - mean);
            double standardDeviation = Math.Max(.015d, Math.Sqrt(variance / values.Length));
            var result = new byte[values.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = Quantize(Math.Max(.24d, Math.Min(.76d, .5d + (values[i] - mean) / standardDeviation * .07d)));
            return result;
        }

        private static void InteriorDistance(float[] distance, int width, int height)
        {
            const float diagonal = 1.41421356f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (x > 0) distance[i] = Math.Min(distance[i], distance[i - 1] + 1f);
                    if (y > 0) distance[i] = Math.Min(distance[i], distance[i - width] + 1f);
                    if (x > 0 && y > 0) distance[i] = Math.Min(distance[i], distance[i - width - 1] + diagonal);
                    if (x + 1 < width && y > 0) distance[i] = Math.Min(distance[i], distance[i - width + 1] + diagonal);
                }
            for (int y = height - 1; y >= 0; y--)
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = y * width + x;
                    if (x + 1 < width) distance[i] = Math.Min(distance[i], distance[i + 1] + 1f);
                    if (y + 1 < height) distance[i] = Math.Min(distance[i], distance[i + width] + 1f);
                    if (x + 1 < width && y + 1 < height) distance[i] = Math.Min(distance[i], distance[i + width + 1] + diagonal);
                    if (x > 0 && y + 1 < height) distance[i] = Math.Min(distance[i], distance[i + width - 1] + diagonal);
                }
        }

        private static void BakeUnchangedPattern(string sourcePath, string outputPath, Texture2D paper)
        {
            byte[] original = File.ReadAllBytes(sourcePath);
            AssetImporter importer = AssetImporter.GetAtPath(sourcePath);
            string importerBefore = importer != null ? EditorJsonUtility.ToJson(importer) : null;
            Texture2D source = null;
            try
            {
                source = Decode(original);
                Color32[] data = BuildUnchangedPattern(source, paper);
                SavePixels(outputPath, data, false, source.width, source.height);
                Require(original.SequenceEqual(File.ReadAllBytes(sourcePath)), "Sticker baking changed an existing CPU pattern.");
                Require(importerBefore == (importer != null ? EditorJsonUtility.ToJson(importer) : null),
                    "Sticker baking changed an existing CPU pattern importer.");
            }
            finally { if (source != null) Object.DestroyImmediate(source); }
        }

        public static Color32[] BuildUnchangedPattern(Texture2D source, Texture2D paper)
        {
            if (source == null || !source.isReadable || paper == null || !paper.isReadable)
                throw new ArgumentException("Readable CPU-mask and paper sources are required.");
            Color32[] mask = source.GetPixels32();
            byte[] grain = BuildGrain(paper.GetPixels32(), paper.width, paper.height, source.width, source.height);
            var coverage = new byte[mask.Length]; var distance = new float[mask.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                Color32 p = mask[i];
                coverage[i] = (byte)Mathf.RoundToInt((.2126f * p.r + .7152f * p.g + .0722f * p.b) * p.a / 255f);
                distance[i] = coverage[i] / 255f >= CpuThreshold ? Math.Max(source.width, source.height) * 2f : 0f;
            }
            InteriorDistance(distance, source.width, source.height);
            var result = new Color32[mask.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = new Color32(coverage[i], grain[i], Quantize(Math.Min(1f, distance[i] / 64f)), coverage[i]);
            return result;
        }

        public static void Validate()
        {
            Require(File.Exists(MaskPath) && File.Exists(DataPath), "Paper sticker data is missing. Bake it before building.");
            Require(File.Exists(VersionPath) && File.ReadAllText(VersionPath).Trim() == DataHash(), "The paper sticker data version is stale.");
            CheckImporter(MaskPath, true); CheckImporter(DataPath, false);
            ValidateUnchangedPattern(BroadMaskPath, BroadDataPath);
            ValidateUnchangedPattern(FlowMaskPath, FlowDataPath);
            Texture2D mask = null, data = null;
            try
            {
                mask = Decode(File.ReadAllBytes(MaskPath)); data = Decode(File.ReadAllBytes(DataPath));
                Require(mask.width == Size && mask.height == Size && data.width == Size && data.height == Size, "Paper sticker data must be 512 square.");
                Color32[] maskPixels = mask.GetPixels32(), dataPixels = data.GetPixels32();
                Require(maskPixels.SequenceEqual(BuildMask()), "The saved common mask differs from this baker's fixed geometry.");
                int visible = 0, deep = 0, nonneutral = 0;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        int i = y * Size + x; Color32 p = maskPixels[i], q = dataPixels[i];
                        Require(p.r == 255 && p.g == 255 && p.b == 255 && p.a == q.r && p.a == q.a,
                            "CPU luminance-alpha and GPU coverage bytes disagree.");
                        Require(q.g >= 61 && q.g <= 194, "Paper grain escaped its bounded half-centred data range.");
                        if (q.g < 124 || q.g > 132) nonneutral++;
                        if (p.a / 255f >= CpuThreshold) { visible++; if (q.b > 32) deep++; }
                        else Require(q.b == 0, "Distance data is nonzero outside the logical contour.");
                        if (x < SafetyMargin || y < SafetyMargin || x >= Size - SafetyMargin || y >= Size - SafetyMargin)
                            Require(p.a == 0, "The paper sticker mask has clipped a finger or a drop against the square border.");
                    }
                Require(visible > 40000 && visible < 140000 && deep > 10000 && nonneutral > 10000,
                    "Paper sticker core, distance or full-square paper texture is missing.");
                Require(maskPixels[(Size / 2) * Size + Size / 2].a == 255, "The sticker requires an opaque impact center.");
                Require(ComponentCount(maskPixels) >= 4, "The shared silhouette lost its detached splash droplets.");
                // CPU samples texel endpoints as (width-1)*uv; GPU uses normalized texture
                // centres. Callers must use ((uv*(Size-1))+.5)/Size for the GPU mask lookup.
                // At each texel centre the bytes are exactly equal; midpoint interpolation
                // agrees once that coordinate convention is applied.
                for (int n = 0; n < 4096; n++)
                {
                    float u = (n * 683 % 4093) / 4092f, v = (n * 937 % 4091) / 4090f;
                    float cpu = Bilinear(maskPixels, u, v, false);
                    float gpu = Bilinear(dataPixels, u, v, true);
                    Require(Mathf.Abs(cpu - gpu) < .000001f, "Bilinear CPU/GPU mask samples differ.");
                }
            }
            finally { if (mask != null) Object.DestroyImmediate(mask); if (data != null) Object.DestroyImmediate(data); }
        }

        private static float Bilinear(Color32[] pixels, float u, float v, bool red)
        {
            float x = u * (Size - 1), y = v * (Size - 1);
            int x0 = (int)x, y0 = (int)y, x1 = Math.Min(Size - 1, x0 + 1), y1 = Math.Min(Size - 1, y0 + 1);
            float a = red ? pixels[y0 * Size + x0].r : pixels[y0 * Size + x0].a;
            float b = red ? pixels[y0 * Size + x1].r : pixels[y0 * Size + x1].a;
            float c = red ? pixels[y1 * Size + x0].r : pixels[y1 * Size + x0].a;
            float d = red ? pixels[y1 * Size + x1].r : pixels[y1 * Size + x1].a;
            return Mathf.Lerp(Mathf.Lerp(a, b, x - x0), Mathf.Lerp(c, d, x - x0), y - y0) / 255f;
        }

        private static int ComponentCount(Color32[] pixels)
        {
            var seen = new bool[pixels.Length]; var queue = new Queue<int>(); int components = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (seen[i] || pixels[i].a / 255f < CpuThreshold) continue;
                components++; queue.Enqueue(i); seen[i] = true;
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue(), x = cell % Size, y = cell / Size;
                    if (x > 0) Visit(cell - 1); if (x + 1 < Size) Visit(cell + 1);
                    if (y > 0) Visit(cell - Size); if (y + 1 < Size) Visit(cell + Size);
                }
            }
            return components;

            void Visit(int cell)
            {
                if (seen[cell] || pixels[cell].a / 255f < CpuThreshold) return;
                seen[cell] = true; queue.Enqueue(cell);
            }
        }

        public static string DataHash()
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
            {
                writer.Write(DataVersion); writer.Write(Size); writer.Write(CpuThreshold);
                foreach (string path in new[] { MaskPath, DataPath, BroadDataPath, FlowDataPath, PaperSourcePath, BroadMaskPath, FlowMaskPath })
                { byte[] bytes = File.ReadAllBytes(path); writer.Write(bytes.Length); writer.Write(bytes); }
                writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void SavePixels(string path, Color32[] pixels, bool readable, int width = Size, int height = Size)
        {
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            try { output.SetPixels32(pixels); output.Apply(false, false); File.WriteAllBytes(path, output.EncodeToPNG()); }
            finally { Object.DestroyImmediate(output); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Texture importer is missing: " + path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false; importer.isReadable = readable; importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = Mathf.NextPowerOfTwo(Math.Max(width, height));
            var platform = importer.GetDefaultPlatformTextureSettings(); platform.overridden = false;
            platform.format = TextureImporterFormat.RGBA32; platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
        }

        private static void CheckImporter(string path, bool readable, int maximumSize = Size)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null && importer.isReadable == readable && !importer.sRGBTexture && !importer.mipmapEnabled
                && !importer.alphaIsTransparency && importer.alphaSource == TextureImporterAlphaSource.FromInput
                && importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Bilinear
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.npotScale == TextureImporterNPOTScale.None && importer.maxTextureSize == maximumSize,
                "Invalid linear/uncompressed paper sticker import settings: " + path);
        }
        private static void ValidateUnchangedPattern(string sourcePath, string outputPath)
        {
            Texture2D source = null, data = null;
            try
            {
                source = Decode(File.ReadAllBytes(sourcePath)); data = Decode(File.ReadAllBytes(outputPath));
                Require(source.width == data.width && source.height == data.height,
                    "An existing CPU pattern was resampled for its sticker data.");
                CheckImporter(outputPath, false, Mathf.NextPowerOfTwo(Math.Max(source.width, source.height)));
                Color32[] original = source.GetPixels32(), pixels = data.GetPixels32();
                for (int i = 0; i < original.Length; i++)
                {
                    Color32 p = original[i], q = pixels[i];
                    byte coverage = (byte)Mathf.RoundToInt((.2126f * p.r + .7152f * p.g + .0722f * p.b) * p.a / 255f);
                    Require(q.r == coverage && q.a == coverage && q.g >= 61 && q.g <= 194,
                        "An existing CPU pattern's numerical coverage differs from its GPU sticker data.");
                    if (coverage / 255f < CpuThreshold) Require(q.b == 0, "Exterior distance is not zero.");
                }
            }
            finally { if (source != null) Object.DestroyImmediate(source); if (data != null) Object.DestroyImmediate(data); }
        }
        private static Texture2D Decode(byte[] bytes)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            if (image.LoadImage(bytes, false)) return image;
            Object.DestroyImmediate(image); throw new InvalidDataException("Cannot decode a paper sticker source PNG.");
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static byte Quantize(double value) => (byte)Math.Max(0, Math.Min(255, Math.Round(value * 255d)));
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private readonly struct Finger
        {
            public readonly float Angle, Length, StemRadius, TipRadius;
            public readonly Vector2 Direction;
            public Finger(float angleDegrees, float length, float stemRadius, float tipRadius)
            { Angle = angleDegrees * Mathf.Deg2Rad; Direction = new Vector2(Mathf.Cos(Angle), Mathf.Sin(Angle)); Length = length; StemRadius = stemRadius; TipRadius = tipRadius; }
        }
    }
}
