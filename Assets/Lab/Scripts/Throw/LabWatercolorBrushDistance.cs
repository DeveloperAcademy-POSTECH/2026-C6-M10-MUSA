using System;
using UnityEngine;

namespace C6Lab
{
    public static class LabWatercolorBrushDistance
    {
        /// <summary>
        /// Private optical rim data derived from a readable brush. The source asset and its
        /// gameplay threshold stay unchanged. R stores the interior distance in UV units times eight.
        /// The caller owns and must destroy the returned texture.
        /// </summary>
        public static Texture2D Create(Texture2D source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.isReadable || source.width < 2 || source.height < 2)
                throw new ArgumentException("A readable brush of at least 2 by 2 pixels is required.", nameof(source));
            int w = source.width, h = source.height;
            Color32[] pixels = source.GetPixels32();
            var distances = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                float ink = (.2126f * p.r + .7152f * p.g + .0722f * p.b) * p.a / (255f * 255f);
                int x = i % w, y = i / w;
                distances[i] = ink >= .08f && x > 0 && y > 0 && x < w - 1 && y < h - 1 ? w + h : 0f;
            }
            for (int y = 1; y < h; y++) for (int x = 1; x < w; x++)
            {
                int i = y * w + x;
                distances[i] = Mathf.Min(distances[i], Mathf.Min(distances[i - 1] + 1f,
                    Mathf.Min(distances[i - w] + 1f, distances[i - w - 1] + 1.41421356f)));
                if (x + 1 < w) distances[i] = Mathf.Min(distances[i], distances[i - w + 1] + 1.41421356f);
            }
            for (int y = h - 2; y >= 0; y--) for (int x = w - 2; x >= 0; x--)
            {
                int i = y * w + x;
                distances[i] = Mathf.Min(distances[i], Mathf.Min(distances[i + 1] + 1f,
                    Mathf.Min(distances[i + w] + 1f, distances[i + w + 1] + 1.41421356f)));
                if (x > 0) distances[i] = Mathf.Min(distances[i], distances[i + w - 1] + 1.41421356f);
            }
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(distances[i] * 8f / Mathf.Min(w, h)) * 255f), 0, 0, 255);
            Texture2D result = null;
            try
            {
                result = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
                {
                    name = "Gourd watercolor interior distance", hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                result.SetPixels32(pixels);
                result.Apply(false, true);
                return result;
            }
            catch
            {
                if (result != null) LabVisualSafety.DestroyVisualObject(result);
                throw;
            }
        }
    }
}
