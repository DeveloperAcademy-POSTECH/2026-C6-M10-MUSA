using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Experiments
{
    /// <summary>Read-only local image loading for the isolated comparison app. Call on Unity's main thread.</summary>
    public static class ClayPreviewImageImport
    {
        public const int MaximumDimension = 2048;
        public const int MaximumFileBytes = 16 * 1024 * 1024;
        public enum Purpose { Background, SurfaceTexture, ShapeMask }

        /// <summary>
        /// A successful result owns its texture. Keep it while displayed, then Dispose it after
        /// replacing the material reference. Failed/cancelled results own no texture and never
        /// change an earlier result. No source file or project asset is modified.
        /// </summary>
        public sealed class Result : IDisposable
        {
            public Texture2D Texture { get; private set; }
            public string FileName { get; private set; }
            public string Error { get; private set; }
            public bool Cancelled { get; private set; }
            public bool Succeeded => Texture != null && Error == null && !Cancelled;
            /// <summary>Original alpha-weighted linear luminance; diagnostic only, not a shader setting.</summary>
            public float MeanLuminance { get; private set; }
            public byte[] SourceBytes { get; private set; }
            internal Result(Texture2D texture, string name, string error, bool cancelled, float mean = .5f)
                : this(texture, name, error, cancelled, mean, null) { }
            internal Result(Texture2D texture, string name, string error, bool cancelled, float mean, byte[] sourceBytes)
            { Texture = texture; FileName = name; Error = error; Cancelled = cancelled; MeanLuminance = mean; SourceBytes = sourceBytes; }
            public void Dispose()
            { Texture2D owned = Texture; Texture = null; SourceBytes = null; DestroyTexture(owned); }
        }

        static readonly float[] LinearChannel = MakeLinearChannels();

        public static Result PickAndLoad(Purpose purpose)
        {
            try
            {
                string path;
#if UNITY_EDITOR
                path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                    purpose == Purpose.Background ? "Choose preview background" : purpose == Purpose.ShapeMask ? "Choose transparent PNG sticker outline" : "Choose clay surface texture",
                    string.Empty, purpose == Purpose.ShapeMask ? new[] { "Transparent PNG", "png" } : new[] { "PNG or JPEG images", "png,jpg,jpeg" });
#elif UNITY_STANDALONE_OSX
                var selected = new byte[16384]; var nativeError = new byte[1024];
                int status = HapioClayChooseImage(selected, selected.Length, nativeError, nativeError.Length);
                if (status == 0) return new Result(null, null, null, true);
                if (status != 1) return Failed(Utf8(nativeError) ?? "The native image picker could not open.");
                path = Utf8(selected);
#else
                return Failed("Local image selection is available in the Unity Editor and the Mac comparison app only.");
#endif
#if UNITY_EDITOR || UNITY_STANDALONE_OSX
                return string.IsNullOrEmpty(path) ? new Result(null, null, null, true) : Load(path, purpose);
#endif
            }
            catch (DllNotFoundException) { return Failed("The Mac picker plug-in is missing. Rebuild the comparison app with its native picker stage."); }
            catch (EntryPointNotFoundException) { return Failed("The Mac picker plug-in has an incompatible version. Rebuild the comparison app."); }
            catch (Exception exception) { return Failed("Image selection failed (" + exception.GetType().Name + ")."); }
        }

        public static Result Load(string path, Purpose purpose)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return Failed("No image file was selected.");
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 1 || stream.Length > MaximumFileBytes) return Failed("Choose an image of 16 MiB or less.");
                    var bytes = new byte[(int)stream.Length]; int received = 0;
                    while (received < bytes.Length)
                    {
                        int read = stream.Read(bytes, received, bytes.Length - received);
                        if (read == 0) return Failed("The selected image was truncated while reading.");
                        received += read;
                    }
                    if (stream.ReadByte() != -1) return Failed("The selected image changed while reading. Try again.");
                    return LoadBytes(bytes, Path.GetFileName(path), purpose);
                }
            }
            catch (Exception exception) { return Failed("The image could not be read (" + exception.GetType().Name + ")."); }
        }

        /// <summary>Built-in samples and local files share the same limits and conversion path.</summary>
        public static Result LoadBytes(byte[] bytes, string displayName, Purpose purpose)
        {
            Texture2D decoded = null, converted = null;
            try
            {
                if (purpose != Purpose.Background && purpose != Purpose.SurfaceTexture && purpose != Purpose.ShapeMask) return Failed("Unknown image purpose.");
                if (purpose == Purpose.ShapeMask && !PngSignature(bytes ?? Array.Empty<byte>()))
                    return Failed("Sticker outlines require a PNG with transparent background.");
                if (!TryGetDimensions(bytes, out int width, out int height, out string error)) return Failed(error);
                string name = string.IsNullOrWhiteSpace(displayName) ? "Imported image" : Path.GetFileName(displayName);
                decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                    { name = "Preview imported " + name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                bool surface = purpose == Purpose.SurfaceTexture;
                if (!ImageConversion.LoadImage(decoded, bytes, !surface && purpose != Purpose.ShapeMask)) return Failed("This PNG/JPEG could not be decoded.");
                if (decoded.width != width || decoded.height != height)
                    return Failed("The decoded image dimensions differ from its header.");
                if (!surface)
                {
                    Texture2D result = decoded; decoded = null;
                    return new Result(result, name, null, false, .5f, sourceBytes: purpose == Purpose.ShapeMask ? (byte[])bytes.Clone() : null);
                }
                Color32[] pixels = decoded.GetPixels32(); double weighted = 0, alphaTotal = 0;
                foreach (Color32 pixel in pixels)
                { double alpha = pixel.a / 255d; weighted += Luminance(pixel) * alpha; alphaTotal += alpha; }
                float mean = alphaTotal > 0 ? (float)(weighted / alphaTotal) : .5f;
                // Half data represents neutral .5 exactly; RGBA8 would introduce a persistent 128/255 bias.
                var data = new ushort[pixels.Length * 4];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    float neutral = pixel.a == 0 ? .5f : Mathf.Clamp01(Luminance(pixel) - mean + .5f);
                    ushort gray = Mathf.FloatToHalf(neutral); int at = i * 4;
                    data[at] = data[at + 1] = data[at + 2] = gray;
                    data[at + 3] = Mathf.FloatToHalf(pixel.a / 255f);
                }
                converted = new Texture2D(width, height, TextureFormat.RGBAHalf, true, true)
                    { name = "Preview neutral texture " + name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat };
                converted.SetPixelData(data, 0); converted.Apply(true, true);
                Texture2D finalTexture = converted; converted = null;
                return new Result(finalTexture, name, null, false, mean);
            }
            catch (Exception exception) { return Failed("The image could not be loaded (" + exception.GetType().Name + ")."); }
            finally { DestroyTexture(decoded); DestroyTexture(converted); }
        }

        /// <summary>Checks bytes and dimensions before Unity's image decoder can allocate the full image.</summary>
        public static bool TryGetDimensions(byte[] bytes, out int width, out int height, out string error)
        {
            width = height = 0; error = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumFileBytes)
            { error = "Choose a nonempty image of 16 MiB or less."; return false; }
            if (PngSignature(bytes))
            {
                if (bytes.Length < 33 || Big32(bytes, 8) != 13 || bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
                { error = "The PNG header is missing or invalid."; return false; }
                uint w = Big32(bytes, 16), h = Big32(bytes, 20);
                if (w == 0 || h == 0 || w > MaximumDimension || h > MaximumDimension)
                { error = "Image width and height must each be between 1 and 2048 pixels."; return false; }
                width = (int)w; height = (int)h; return true;
            }
            if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8)
            {
                int at = 2; bool found = false;
                while (at < bytes.Length)
                {
                    if (bytes[at++] != 0xff) { error = "The JPEG marker header is invalid."; return false; }
                    while (at < bytes.Length && bytes[at] == 0xff) at++;
                    if (at >= bytes.Length) break;
                    int marker = bytes[at++];
                    if (marker == 0xda) // Scan data starts only after a validated frame header.
                    { if (found) return true; break; }
                    if (marker == 0xd9 || marker == 0x00) break;
                    if (marker == 0x01 || marker >= 0xd0 && marker <= 0xd7) continue;
                    if (bytes.Length - at < 2) break;
                    int length = Big16(bytes, at);
                    if (length < 2 || length > bytes.Length - at) break;
                    bool frame = marker >= 0xc0 && marker <= 0xcf && marker != 0xc4 && marker != 0xc8 && marker != 0xcc;
                    if (frame)
                    {
                        if (found || length < 8 || bytes[at + 7] < 1 || bytes[at + 7] > 4 || length != 8 + 3 * bytes[at + 7])
                        { error = "The JPEG frame header is invalid or ambiguous."; return false; }
                        height = Big16(bytes, at + 3); width = Big16(bytes, at + 5); found = true;
                        if (width < 1 || height < 1 || width > MaximumDimension || height > MaximumDimension)
                        { error = "Image width and height must each be between 1 and 2048 pixels."; return false; }
                    }
                    at += length;
                }
                error = "The JPEG frame or scan header is missing or truncated."; width = height = 0; return false;
            }
            error = "Only PNG and JPEG image data is supported."; return false;
        }

        static float Luminance(Color32 pixel) => .2126f * LinearChannel[pixel.r] + .7152f * LinearChannel[pixel.g] + .0722f * LinearChannel[pixel.b];
        static float[] MakeLinearChannels()
        { var result = new float[256]; for (int i = 0; i < result.Length; i++) result[i] = Mathf.GammaToLinearSpace(i / 255f); return result; }
        static bool PngSignature(byte[] b) => b.Length >= 8 && b[0] == 137 && b[1] == 80 && b[2] == 78 && b[3] == 71
            && b[4] == 13 && b[5] == 10 && b[6] == 26 && b[7] == 10;
        static uint Big32(byte[] b, int at) => ((uint)b[at] << 24) | ((uint)b[at + 1] << 16) | ((uint)b[at + 2] << 8) | b[at + 3];
        static int Big16(byte[] b, int at) => b[at] * 256 + b[at + 1];
        static Result Failed(string error) => new Result(null, null, error, false);
        static void DestroyTexture(Texture2D texture)
        { if (texture == null) return; if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture); }
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("ClayPreviewMacImagePicker", CallingConvention = CallingConvention.Cdecl)]
        static extern int HapioClayChooseImage([Out] byte[] path, int pathCapacity, [Out] byte[] error, int errorCapacity);
        static string Utf8(byte[] bytes)
        { int length = Array.IndexOf(bytes, (byte)0); return Encoding.UTF8.GetString(bytes, 0, length < 0 ? bytes.Length : length); }
#endif
    }
}
