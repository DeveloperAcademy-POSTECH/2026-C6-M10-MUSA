using System;
using UnityEngine;

namespace C6Lab
{
    public enum LabPaintStampMode { Watercolor = 0, PaperSticker = 1, ClaySticker = 2 }

    /// <summary>Opaque paper appearance only. Gameplay growth amounts remain in LabConfig.</summary>
    [Serializable]
    public sealed class LabPaperStickerSettings
    {
        public const string ResourceFolder = "GourdPaint/PaperSticker/";
        public const string ShaderResource = ResourceFolder + "OpaquePaperStickerRuntime";
        public const string MaskResource = ResourceFolder + "SplatterMask";
        [Range(0f, .3f)] public float grainStrength = .12f;
        [Range(0f, .3f)] public float edgeStrength = .08f;
        [Range(1f, 8f)] public float grainScale = 2f;

        public LabPaperStickerSettings SanitizedCopy() => new LabPaperStickerSettings
        {
            grainStrength = Safe(grainStrength, .12f, 0f, .3f),
            edgeStrength = Safe(edgeStrength, .08f, 0f, .3f),
            grainScale = Safe(grainScale, 2f, 1f, 8f)
        };
        public void Validate()
        {
            Check(grainStrength, 0f, .3f); Check(edgeStrength, 0f, .3f); Check(grainScale, 1f, 8f);
        }
        public int Fingerprint
        {
            get { var s = SanitizedCopy(); unchecked {
                int h = BitConverter.SingleToInt32Bits(s.grainStrength);
                h = h * 397 ^ BitConverter.SingleToInt32Bits(s.edgeStrength);
                return h * 397 ^ BitConverter.SingleToInt32Bits(s.grainScale);
            } }
        }
        public static string BakedBrushVersion
        {
            get
            {
                var file = Resources.Load<TextAsset>(ResourceFolder + "Version");
                string value = file != null ? file.text.Trim() : null;
                if (value != null && value.Length == 64)
                {
                    bool valid = true;
                    foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) { valid = false; break; }
                    if (valid) return value;
                }
                return "paper-sticker-data-v1-unbaked";
            }
        }
        private static float Safe(float value, float fallback, float min, float max)
            => Mathf.Clamp(float.IsFinite(value) ? value : fallback, min, max);
        private static void Check(float value, float min, float max)
        { if (!float.IsFinite(value) || value < min || value > max) throw new ArgumentOutOfRangeException(nameof(value)); }
    }
}
