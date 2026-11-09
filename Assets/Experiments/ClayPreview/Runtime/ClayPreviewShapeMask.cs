using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Experiments
{
    /// <summary>Alpha-only artwork for the isolated comparison app; never a gameplay brush.</summary>
    public static class ClayPreviewShapeMask
    {
        public const int Size = 512;
        public const int PaddingPixels = 32;
        public const int BevelPixels = 20;
        public const string BakeVersion = "clay-preview-alpha-outline-v1-512-pad32-bevel20";

        public sealed class Result : IDisposable
        {
            public Texture2D Brush { get; private set; }
            public Texture2D Preview { get; private set; }
            public byte[] SourceBytes { get; private set; }
            public string SourceSha256 { get; private set; }
            public string FileName { get; private set; }
            public string Error { get; private set; }
            public bool Cancelled { get; private set; }
            public int SourceWidth { get; private set; }
            public int SourceHeight { get; private set; }
            public bool Succeeded => Brush != null && Preview != null && Error == null && !Cancelled;

            internal Result(Texture2D brush = null, Texture2D preview = null, byte[] source = null,
                string name = null, int width = 0, int height = 0, string error = null, bool cancelled = false)
            {
                Brush = brush; Preview = preview; SourceBytes = source;
                FileName = name; SourceWidth = width; SourceHeight = height;
                SourceSha256 = source == null ? null : Hash(source); Error = error; Cancelled = cancelled;
            }

            public void Dispose()
            {
                Texture2D brush = Brush, preview = Preview;
                Brush = Preview = null; SourceBytes = null;
                Destroy(brush); Destroy(preview);
            }
        }

        public static Result PickAndLoad()
        {
            using (ClayPreviewImageImport.Result imported = ClayPreviewImageImport.PickAndLoad(ClayPreviewImageImport.Purpose.ShapeMask))
                return Bake(imported);
        }

        public static Result LoadFile(string path)
        {
            using (ClayPreviewImageImport.Result imported = ClayPreviewImageImport.Load(path, ClayPreviewImageImport.Purpose.ShapeMask))
                return Bake(imported);
        }

        public static Result LoadBytes(byte[] png, string displayName)
        {
            using (ClayPreviewImageImport.Result imported = ClayPreviewImageImport.LoadBytes(png, displayName, ClayPreviewImageImport.Purpose.ShapeMask))
                return Bake(imported);
        }

        static Result Bake(ClayPreviewImageImport.Result imported)
        {
            if (imported.Cancelled) return new Result(cancelled: true);
            if (!imported.Succeeded) return new Result(error: imported.Error ?? "The outline image could not be loaded.");
            Texture2D brush = null, preview = null;
            try
            {
                Texture2D original = imported.Texture;
                Color32[] originalPixels = original.GetPixels32();
                byte minimum = 255, maximum = 0;
                foreach (Color32 pixel in originalPixels)
                { minimum = Math.Min(minimum, pixel.a); maximum = Math.Max(maximum, pixel.a); }
                if (maximum == 0) return new Result(error: "The PNG is fully transparent; there is no sticker outline.");
                if (minimum > 3) return new Result(error: "Use a PNG with transparent background. Opaque RGB images are not converted into outlines.");
                if (maximum < 32) return new Result(error: "The PNG alpha is too faint for the existing sticker compositor.");

                // The full original canvas, including intentional transparent gaps, has a fixed fit rule.
                // Never crop its opaque bounds or normalize from the longest branch.
                float available = Size - PaddingPixels * 2;
                float scale = available / Mathf.Max(original.width, original.height);
                float width = original.width * scale, height = original.height * scale;
                float left = (Size - width) * .5f, bottom = (Size - height) * .5f;
                var pixels = new Color32[Size * Size];
                var display = new Color32[pixels.Length];
                var distance = new float[pixels.Length];
                bool hasVisibleCoverage = false;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + .5f - left) / width, v = (y + .5f - bottom) / height;
                        float alpha = u >= 0 && u <= 1 && v >= 0 && v <= 1 ? original.GetPixelBilinear(u, v).a : 0;
                        byte coverage = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255);
                        hasVisibleCoverage |= coverage / 255f > .12f; // Existing compositor starts coverage above .12.
                        int at = y * Size + x;
                        pixels[at] = new Color32(coverage, 0, 0, coverage);
                        display[at] = new Color32(25, 35, 245, coverage);
                        distance[at] = alpha >= .3f ? 4096 : 0;
                    }
                if (!hasVisibleCoverage)
                    return new Result(error: "No outline pixels remain above the sticker compositor threshold after fitting to 512 px. Use larger source features.");
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++) Relax(x, y, -1, -1);
                for (int y = Size - 1; y >= 0; y--)
                    for (int x = Size - 1; x >= 0; x--) Relax(x, y, 1, 1);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    pixel.g = (byte)Mathf.RoundToInt(Mathf.Clamp01(distance[i] / 32f) * 255);
                    pixel.b = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance[i] / BevelPixels)) * 255);
                    pixels[i] = pixel;
                }
                brush = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
                { name = "Imported alpha outline data", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                brush.SetPixels32(pixels); brush.Apply(false, false);
                preview = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false)
                { name = "Imported alpha outline preview", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                preview.SetPixels32(display); preview.Apply(false, false);
                var result = new Result(brush, preview, (byte[])imported.SourceBytes.Clone(), imported.FileName, original.width, original.height);
                brush = preview = null;
                return result;

                void Relax(int x, int y, int dx, int dy)
                {
                    int index = y * Size + x;
                    Update(x + dx, y, 1); Update(x, y + dy, 1);
                    Update(x + dx, y + dy, 1.41421356f); Update(x - dx, y + dy, 1.41421356f);
                    void Update(int nx, int ny, float cost)
                    {
                        if (nx >= 0 && nx < Size && ny >= 0 && ny < Size)
                            distance[index] = Mathf.Min(distance[index], distance[ny * Size + nx] + cost);
                    }
                }
            }
            catch (Exception exception) { return new Result(error: "The outline could not be prepared (" + exception.GetType().Name + ")."); }
            finally { Destroy(brush); Destroy(preview); }
        }

        public static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        static void Destroy(Texture2D texture)
        { if (texture == null) return; if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture); }
    }
}
