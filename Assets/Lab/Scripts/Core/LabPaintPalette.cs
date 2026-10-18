using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Paint-only authored palette. Orb materials and hit feedback retain their existing appearance.
    /// The authoritative map stores linear RGB, so convert sRGB swatches before writing its pixels.
    /// </summary>
    public static class LabPaintPalette
    {
        public static Color32 SrgbColorFor(LabOrbElement element)
        {
            switch (element)
            {
                case LabOrbElement.Fire: return new Color32(216, 87, 54, 255); // #D85736
                case LabOrbElement.Water: return new Color32(20, 113, 167, 255); // #1471A7
                case LabOrbElement.Wood: return new Color32(61, 130, 88, 255); // #3D8258
                case LabOrbElement.Metal: return new Color32(248, 183, 42, 255); // #F8B72A
                case LabOrbElement.Earth: return new Color32(111, 74, 50, 255); // #6F4A32
                default: return new Color32(0, 0, 0, 0);
            }
        }

        public static Color ColorFor(LabOrbElement element) => ((Color)SrgbColorFor(element)).linear;
    }
}
