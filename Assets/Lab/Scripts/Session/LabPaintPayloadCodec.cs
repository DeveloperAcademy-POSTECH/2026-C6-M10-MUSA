using System;
using System.IO;
using System.IO.Compression;

namespace C6Lab
{
    /// <summary>How a paint frame's publisher encodes its body (#461).</summary>
    public enum LabPaintEncodingPolicy
    {
        /// <summary>Always the raw cell bytes (encoding 0).</summary>
        RawOnly,
        /// <summary>Deflate when it is shorter than the raw bytes (encoding 1), raw otherwise.</summary>
        Auto
    }

    /// <summary>
    /// Deflate for paint frame bodies (#461). An empty or mostly unpainted 256×128 map of 160 KB becomes a few hundred
    /// bytes, so a stage start sends one frame instead of a hundred. Inflation is bounded by a size the frame header
    /// fixes, so a corrupt or hostile body can never grow past what a valid frame could carry.
    /// </summary>
    public static class LabPaintPayloadCodec
    {
        public const int Raw = 0;
        public const int Deflate = 1;

        public static byte[] Compress(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true))
                    deflate.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        /// <summary>
        /// Inflate <paramref name="length"/> bytes of <paramref name="source"/> into <paramref name="buffer"/> (grown as
        /// needed and kept by the caller). False when the stream is invalid or would exceed <paramref name="maximumBytes"/>.
        /// </summary>
        public static bool TryInflate(byte[] source, int length, int maximumBytes, ref byte[] buffer, out int written)
        {
            written = 0;
            if (source == null || length < 0 || length > source.Length || maximumBytes < 0) return false;
            if (buffer == null || buffer.Length < maximumBytes + 1) buffer = new byte[maximumBytes + 1];
            try
            {
                using (var input = new MemoryStream(source, 0, length, false))
                using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
                {
                    // One byte past the limit proves the stream is too long without inflating the rest.
                    while (written <= maximumBytes)
                    {
                        int read = inflate.Read(buffer, written, maximumBytes + 1 - written);
                        if (read <= 0) break;
                        written += read;
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is IOException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                written = 0;
                return false;
            }
            return written <= maximumBytes;
        }
    }
}
