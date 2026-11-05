using UnityEngine;

namespace C6Lab
{
    /// <summary>Local authoring input. A network round pins a copy and requires the same Version on every peer.</summary>
    [CreateAssetMenu(menuName = "C6 Lab/Watercolor Brush Stamp Profile")]
    public sealed class LabWatercolorStampProfile : ScriptableObject
    {
        public const string ResourcePath = "GourdPaint/WatercolorStampProfile";
        public LabWatercolorStampSettings settings = new LabWatercolorStampSettings();
        public static LabWatercolorStampSettings LoadDefault()
        {
            var asset = Resources.Load<LabWatercolorStampProfile>(ResourcePath);
            return (asset != null && asset.settings != null ? asset.settings : new LabWatercolorStampSettings()).Clone();
        }
    }
}
