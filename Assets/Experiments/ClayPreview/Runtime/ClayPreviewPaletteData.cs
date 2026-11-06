using System;
using UnityEngine;

namespace Hapio.Experiments
{
    /// <summary>Frozen presentation dependencies; never a game palette or authority asset.</summary>
    [Serializable]
    public sealed class ClayPreviewPalettePreset
    {
        public Texture2D[] brushes;
        public Shader compositor;
        public float minimumDensity, maximumDensity;
        public string sampleMetadataHash, colorRawHash, heightRawHash;
    }

    /// <summary>One sealed source journal and the four immutable comparison presets.</summary>
    public sealed class ClayPreviewPaletteData : ScriptableObject
    {
        public const string Format = "clay-preview-palette-v1";
        public string format = Format;
        public string epoch, styleVersion, recordHash, journalSha256, settingsJson;
        public int columns, rows, pageCount;
        public ulong lastSequence;
        public TextAsset journal;
        // Fire, Water, Wood, Metal, Earth in sRGB bytes, not linear map bytes.
        public Color32[] sourceSrgb;
        public ClayPreviewPalettePreset[] presets;
    }
}
