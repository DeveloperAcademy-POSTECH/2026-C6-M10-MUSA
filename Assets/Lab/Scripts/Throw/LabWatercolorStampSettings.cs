using System;
using System.Globalization;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Versioned, presentation-only parameters captured once for each round.</summary>
    [Serializable]
    public sealed class LabWatercolorStampSettings
    {
        public const string BrushVersion = "round-growth-overlap-data-v1-d77e74f2074e5e312b";
        public const string StyleVersion = "watercolor-stamps-v2";
        public const string PaperStickerStyleVersion = "paper-stickers-v1";
        public LabPaintStampMode mode = LabPaintStampMode.Watercolor;
        public LabPaperStickerSettings paperSticker = new LabPaperStickerSettings();
        public LabClayStickerSettings claySticker = new LabClayStickerSettings();
        public bool UsesClayStickers => mode == LabPaintStampMode.ClaySticker;
        public bool UsesPaperStickers => mode == LabPaintStampMode.PaperSticker;
        public string CurrentBrushVersion => UsesClayStickers ? LabClayStickerSettings.BakedBrushVersion : UsesPaperStickers ? LabPaperStickerSettings.BakedBrushVersion : BakedBrushVersion;
        [NonSerialized] private string pinnedBrushVersion;
        public bool enabled = true;
        [Range(.05f, 1f)] public float density = .9f;
        [Tooltip("Opacity supplied when a new brush crosses already painted surface. This never changes coverage or the P map.")]
        [Range(0f, 1f)] public float overlap = .78f;
        [Range(0f, 1f)] public float edgeStrength = .55f;
        [Range(.002f, .15f)] public float edgeWidth = .025f;
        [Tooltip("Contrast inside each original brush imprint; does not change its funded coverage.")]
        [Range(1f, 2.5f)] public float pigmentContrast = 1.6f;
        [Tooltip("Appearance-only pigment chroma; authoritative element colors remain unchanged.")]
        [Range(0f, 1.5f)] public float pigmentSaturation = 1.15f;
        [Tooltip("Fine pigment texture applied on the gourd after cell-boundary reconstruction.")]
        [Range(0f, .6f)] public float granulationStrength = .25f;
        [Tooltip("Object-local pigment detail frequency. Unresolved detail is filtered by the surface shader.")]
        [Range(48f, 240f)] public float granulationScale = 120f;
        [Tooltip("Seeded interruptions in the dark watercolor rim. Zero gives an uninterrupted rim.")]
        [Range(0f, 1f)] public float rimBreakup = .45f;
        [Tooltip("Visual reveal only, measured on the shared battle presentation clock. Zero settles immediately.")]
        [Range(0f, 2f)] public float revealSeconds = .35f;

        public LabWatercolorStampSettings SanitizedCopy() => new LabWatercolorStampSettings
        {
            enabled = enabled,
            mode = mode == LabPaintStampMode.PaperSticker || mode == LabPaintStampMode.ClaySticker ? mode : LabPaintStampMode.Watercolor,
            paperSticker = (paperSticker ?? new LabPaperStickerSettings()).SanitizedCopy(),
            claySticker = (claySticker ?? new LabClayStickerSettings()).SanitizedCopy(),
            density = Safe(density, .9f, .05f, 1f),
            overlap = Safe(overlap, .78f, 0f, 1f),
            edgeStrength = Safe(edgeStrength, .55f, 0f, 1f),
            edgeWidth = Safe(edgeWidth, .025f, .002f, .15f),
            revealSeconds = Safe(revealSeconds, .35f, 0f, 2f),
            pigmentContrast = Safe(pigmentContrast, 1.6f, 1f, 2.5f),
            pigmentSaturation = Safe(pigmentSaturation, 1.15f, 0f, 1.5f),
            granulationStrength = Safe(granulationStrength, .25f, 0f, .6f),
            granulationScale = Safe(granulationScale, 120f, 48f, 240f),
            rimBreakup = Safe(rimBreakup, .45f, 0f, 1f),
            pinnedBrushVersion = this.pinnedBrushVersion
        };

        public LabWatercolorStampSettings Clone()
        {
            var copy = SanitizedCopy();
            // Never pin the authoring asset while capturing a round. The copy owns its
            // immutable brush identity, and later sanitization preserves that identity.
            copy.pinnedBrushVersion ??= copy.CurrentBrushVersion;
            return copy;
        }
        public string PinnedBrushVersion => pinnedBrushVersion ??= CurrentBrushVersion;
        public int Fingerprint
        {
            get
            {
                LabWatercolorStampSettings s = SanitizedCopy();
                unchecked
                {
                    int h = s.enabled ? 1 : 0;
                    h = Mix(h, s.density); h = Mix(h, s.overlap); h = Mix(h, s.edgeStrength);
                    h = Mix(h, s.edgeWidth); h = Mix(h, s.revealSeconds);
                    h = Mix(h, s.pigmentContrast); h = Mix(h, s.pigmentSaturation);
                    h = Mix(h, s.granulationStrength); h = Mix(h, s.granulationScale);
                    h = Mix(h, s.rimBreakup);
                    if(s.UsesClayStickers) return (h * 397 ^ (int)s.mode) * 397 ^ s.claySticker.Fingerprint;
                    return s.UsesPaperStickers ? (h * 397 ^ (int)s.mode) * 397 ^ s.paperSticker.Fingerprint : h;
                }
            }
        }
        public static LabWatercolorStampSettings CaptureDefault() => LabWatercolorStampProfile.LoadDefault();
        public string Identity => Version;
        public static string BakedBrushVersion
        {
            get
            {
                var version = Resources.Load<TextAsset>("GourdPaint/WatercolorBrushes/Version");
                string hash = version != null ? version.text.Trim() : null;
                if (hash != null && hash.Length == 64)
                {
                    bool valid = true;
                    foreach (char c in hash) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) { valid = false; break; }
                    if (valid) return hash;
                }
                return BrushVersion;
            }
        }
        public string Version => (UsesClayStickers ? LabClayStickerSettings.StyleVersion : UsesPaperStickers ? PaperStickerStyleVersion : StyleVersion) + "/" + PinnedBrushVersion + "/"
            + unchecked((uint)Fingerprint).ToString("x8", CultureInfo.InvariantCulture);

        public void Validate()
        {
            if (mode != LabPaintStampMode.Watercolor && mode != LabPaintStampMode.PaperSticker && mode != LabPaintStampMode.ClaySticker)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (UsesClayStickers) { if(claySticker == null) throw new ArgumentNullException(nameof(claySticker)); claySticker.Validate(); }
            if (UsesPaperStickers)
            { if (paperSticker == null) throw new ArgumentNullException(nameof(paperSticker)); paperSticker.Validate(); }
            Check(density, .05f, 1f); Check(overlap, 0f, 1f); Check(edgeStrength, 0f, 1f);
            Check(edgeWidth, .002f, .15f); Check(revealSeconds, 0f, 2f);
            Check(pigmentContrast, 1f, 2.5f); Check(pigmentSaturation, 0f, 1.5f);
            Check(granulationStrength, 0f, .6f); Check(granulationScale, 48f, 240f);
            Check(rimBreakup, 0f, 1f);
        }
        private static int Mix(int h, float f) => unchecked(h * 397 ^ BitConverter.SingleToInt32Bits(f));
        private static float Safe(float f, float fallback, float min, float max)
            => Mathf.Clamp(float.IsFinite(f) ? f : fallback, min, max);
        private static void Check(float f, float min, float max)
        { if (!float.IsFinite(f) || f < min || f > max) throw new ArgumentOutOfRangeException(nameof(f)); }
    }
}
