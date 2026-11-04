using System;
using UnityEngine;

namespace C6Lab
{
    public static class LabWatercolorBrushDistance
    {
        // One field per pattern for the app's life (#482): the runtime is rebuilt every round, and rebuilding this on the
        // first hit of each stage stalled that frame. A 1024² brush keeps about 4 MB per pattern, at most three.
        private static readonly Texture2D[] Shared = new Texture2D[4];

        /// <summary>The shared field of a pattern's brush; built on first request and never destroyed by a runtime.</summary>
        public static Texture2D GetOrCreate(LabPaintPattern pattern, Texture2D brush)
        {
            int index = (int)pattern;
            if (index < 1 || index >= Shared.Length) throw new ArgumentOutOfRangeException(nameof(pattern));
            if (Shared[index] == null) Shared[index] = Create(brush);
            return Shared[index];
        }

        /// <summary>Whether a pattern's shared field already exists; tests and measurements only.</summary>
        public static bool IsShared(LabPaintPattern pattern)
        {
            int index = (int)pattern;
            return index >= 1 && index < Shared.Length && Shared[index] != null;
        }

#if UNITY_EDITOR
        // DontSave fields outlive a script reload and edit-mode study renders; free them before the array is lost.
        [UnityEditor.InitializeOnLoadMethod]
        private static void ReleaseInEditor()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseShared;
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            { if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) ReleaseShared(); };
        }
#endif

        /// <summary>Drop the shared fields (Editor play sessions without a domain reload, and tests).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ReleaseShared()
        {
            for (int i = 0; i < Shared.Length; i++)
            {
                if (Shared[i] != null) LabVisualSafety.DestroyVisualObject(Shared[i]);
                Shared[i] = null;
            }
        }

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
            using (LabFirstUse.Measure(LabFirstUseKind.BrushDistance)) return Build(source);
        }

        private static Texture2D Build(Texture2D source)
        {
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
