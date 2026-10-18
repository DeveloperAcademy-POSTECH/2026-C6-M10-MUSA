using System;
using UnityEngine;

namespace C6Lab.Experiments
{
    /// <summary>Immutable CPU copy of a black-background, white-ink comparison mask.</summary>
    public sealed class LabPaintStudyMask
    {
        public const float FootprintThreshold = .15f;
        private readonly float[] luminance;
        public int Width { get; }
        public int Height { get; }
        internal float FootprintFraction { get; }

        public LabPaintStudyMask(int width, int height, Color32[] pixels)
        {
            if (width < 1 || width > 8192) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1 || height > 8192 || (long)width * height > 16777216)
                throw new ArgumentOutOfRangeException(nameof(height), "A study mask must contain at most 16,777,216 pixels.");
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != width * height)
                throw new ArgumentException("Mask pixels must contain exactly width * height entries.", nameof(pixels));
            Width = width; Height = height;
            luminance = new float[pixels.Length];
            int footprint = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 color = pixels[i];
                float value = (.2126f * color.r + .7152f * color.g + .0722f * color.b) * color.a / 65025f;
                luminance[i] = value;
                if (value >= FootprintThreshold) footprint++;
            }
            if (footprint == 0)
                throw new ArgumentException("The mask has no visible ink at the 0.15 footprint threshold.", nameof(pixels));
            FootprintFraction = footprint / (float)pixels.Length;
        }

        /// <summary>Bilinear alpha-weighted luma. UV follows Texture2D: v=0 is the bottom.</summary>
        public float Sample(float u, float v)
        {
            if (float.IsNaN(u) || float.IsNaN(v) || u < 0f || u > 1f || v < 0f || v > 1f) return 0f;
            float x = u * (Width - 1), y = v * (Height - 1);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            int x1 = Math.Min(x0 + 1, Width - 1), y1 = Math.Min(y0 + 1, Height - 1);
            float tx = x - x0, ty = y - y0;
            float bottom = Mathf.LerpUnclamped(luminance[y0 * Width + x0], luminance[y0 * Width + x1], tx);
            float top = Mathf.LerpUnclamped(luminance[y1 * Width + x0], luminance[y1 * Width + x1], tx);
            return Mathf.LerpUnclamped(bottom, top, ty);
        }
    }
}
