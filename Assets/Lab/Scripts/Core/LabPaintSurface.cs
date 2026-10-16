using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// A cumulative, equal-area sphere map shared by Host rules and local rendering.
    /// Each texel covers the same solid angle. Its stored alpha, rather than a separate
    /// approximation or a finite list of decals, determines whether that cell is covered.
    /// </summary>
    public sealed class LabPaintSurface
    {
        public const int Width = 256;
        public const int Height = 128;
        public const int PixelCount = Width * Height;
        public const float DefaultRadius = .15f;

        private static readonly Vector3[] points = MakePoints();
        private readonly Color32[] pixels = new Color32[PixelCount];

        public int CoveredCount { get; private set; }
        public int PaintCount { get; private set; }
        public uint Revision { get; private set; }
        public float Coverage => CoveredCount / (float)PixelCount;

        /// <summary>
        /// Accumulate one sphere-local stamp. Repeated stamps consume no storage and count no
        /// already-covered cell twice; later colors composite over earlier colors. The incoming
        /// alpha modulates the original soft-edged 93%-opaque paint. Invalid input changes nothing.
        /// </summary>
        public bool Stamp(Vector3 direction, Color color, float radius = DefaultRadius)
        {
            if (!Finite(direction) || !Finite(color) || !Finite(radius) || radius <= 0f
                || direction.sqrMagnitude < .00001f || !Finite(direction.sqrMagnitude)
                || color.a <= 0f)
                return false;

            Vector3 center = direction.normalized * .5f;
            // A radius >= 2 already puts the entire diameter-1 sphere inside the opaque core.
            // Clipping only this redundant range avoids overflow for otherwise finite input.
            float safeRadius = Mathf.Min(radius, 2f);
            float outer = safeRadius * 1.1f;
            float outerSquared = outer * outer;
            float red = Mathf.Clamp01(color.r), green = Mathf.Clamp01(color.g), blue = Mathf.Clamp01(color.b);
            float opacity = Mathf.Clamp01(color.a) * .93f;
            for (int i = 0; i < pixels.Length; i++)
            {
                Vector3 delta = points[i] - center;
                float distanceSquared = delta.sqrMagnitude;
                // The irregular edge can extend at most 10%. Avoid trigonometry for all other cells.
                if (distanceSquared >= outerSquared) continue;
                float ripple = 1f + .1f * Mathf.Sin(delta.x * 135f + Mathf.Sin(delta.y * 91f))
                    * Mathf.Sin(delta.z * 119f + delta.y * 57f);
                float edge = safeRadius * ripple;
                if (distanceSquared >= edge * edge) continue;
                float t = Mathf.InverseLerp(edge * .83f, edge, Mathf.Sqrt(distanceSquared));
                float alpha = (1f - t * t * (3f - 2f * t)) * opacity;
                Color32 old = pixels[i];
                float inverse = 1f - alpha;
                Color32 next = new Color(
                    red * alpha + old.r / 255f * inverse,
                    green * alpha + old.g / 255f * inverse,
                    blue * alpha + old.b / 255f * inverse,
                    alpha + old.a / 255f * inverse);
                // Counting the actual quantized texture alpha also excludes imperceptibly small
                // edges which round to transparent. Premultiplied RGB must stay zero there.
                if (next.a == 0) next = default;
                if (old.a == 0 && next.a != 0) CoveredCount++;
                pixels[i] = next;
            }
            if (PaintCount < int.MaxValue) PaintCount++;
            unchecked { Revision++; }
            return true;
        }

        public void Clear()
        {
            Array.Clear(pixels, 0, pixels.Length);
            CoveredCount = 0;
            PaintCount = 0;
            unchecked { Revision++; }
        }

        /// <summary>Copy premultiplied linear RGBA bytes into the matching no-mipmap texture.</summary>
        public void CopyPixels(Color32[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length != PixelCount)
                throw new ArgumentException("A paint map needs exactly Width * Height pixels.", nameof(destination));
            Array.Copy(pixels, destination, PixelCount);
        }

        private static Vector3[] MakePoints()
        {
            var result = new Vector3[PixelCount];
            for (int y = 0; y < Height; y++)
            {
                // v = .5 - normalizedY*.5; uniform normalizedY gives equal spherical areas.
                float vertical = .5f - (y + .5f) / Height;
                float ring = Mathf.Sqrt(.25f - vertical * vertical);
                for (int x = 0; x < Width; x++)
                {
                    float angle = ((x + .5f) / Width - .5f) * (2f * Mathf.PI);
                    result[y * Width + x] = new Vector3(Mathf.Cos(angle) * ring, vertical,
                        Mathf.Sin(angle) * ring);
                }
            }
            return result;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b) && Finite(value.a);
    }
}
