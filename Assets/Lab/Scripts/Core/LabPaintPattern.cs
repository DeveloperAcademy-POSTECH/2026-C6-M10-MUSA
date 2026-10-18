using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Round-boundary choice. The legacy growth-style option is independent of this enum.</summary>
    public enum LabPaintPattern { Legacy = 0, Broad = 1, Splatter = 2, Flow = 3 }

    /// <summary>
    /// Immutable runtime mask, loaded once per pattern. It has no dependency on the Editor
    /// comparison fixture. CPU bytes avoid repeated texture readback during hits or growth.
    /// </summary>
    internal sealed class LabPaintPatternMask
    {
        internal const float Threshold = .15f;
        private static readonly LabPaintPatternMask[] Cache = new LabPaintPatternMask[4];
        private readonly byte[] luminance;
        private readonly int width, height;
        internal readonly float FootprintFraction;

        private LabPaintPatternMask(Texture2D source)
        {
            width = source.width; height = source.height;
            Color32[] colors = source.GetPixels32();
            luminance = new byte[colors.Length];
            int covered = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                Color32 color = colors[i];
                byte value = (byte)Mathf.RoundToInt((.2126f * color.r + .7152f * color.g
                    + .0722f * color.b) * color.a / 255f);
                luminance[i] = value;
                if (value / 255f >= Threshold) covered++;
            }
            if (covered == 0 || Sample(.5f, .5f) < Threshold)
                throw new InvalidOperationException("A runtime paint pattern needs visible ink at its center.");
            FootprintFraction = covered / (float)colors.Length;
        }

        internal static LabPaintPatternMask Load(LabPaintPattern pattern)
        {
            if (pattern < LabPaintPattern.Broad || pattern > LabPaintPattern.Flow)
                throw new ArgumentOutOfRangeException(nameof(pattern));
            int index = (int)pattern;
            if (Cache[index] != null) return Cache[index];
            string path = "GourdPaint/Patterns/" + pattern;
            Texture2D source = Resources.Load<Texture2D>(path);
            if (source == null || !source.isReadable)
                throw new InvalidOperationException("Missing readable runtime paint mask: Resources/" + path);
            var mask = new LabPaintPatternMask(source);
            Cache[index] = mask;
            return mask;
        }

        /// <summary>Bilinear alpha-weighted luminance. V=0 is the texture's bottom edge.</summary>
        internal float Sample(float u, float v)
        {
            if (!(u >= 0f && u <= 1f && v >= 0f && v <= 1f)) return 0f;
            float x = u * (width - 1), y = v * (height - 1);
            int x0 = (int)x, y0 = (int)y;
            int x1 = Math.Min(x0 + 1, width - 1), y1 = Math.Min(y0 + 1, height - 1);
            float a = Mathf.LerpUnclamped(luminance[y0 * width + x0], luminance[y0 * width + x1], x - x0);
            float b = Mathf.LerpUnclamped(luminance[y1 * width + x0], luminance[y1 * width + x1], x - x0);
            return Mathf.LerpUnclamped(a, b, y - y0) / 255f;
        }
    }
}
