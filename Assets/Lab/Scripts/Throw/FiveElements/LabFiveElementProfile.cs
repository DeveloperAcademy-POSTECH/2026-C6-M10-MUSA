using UnityEngine;

namespace C6Lab
{
    /// <summary>Local art direction, shared by the gallery, Lab and Hapio. Never enters network state.</summary>
    [CreateAssetMenu(fileName = "Profile", menuName = "C6 Physics Lab/Five Element Clay Profile")]
    public sealed class LabFiveElementProfile : ScriptableObject
    {
        [Header("3D orb surfaces (Fire, Water, Wood, Metal, Earth)")]
        [SerializeField] private Material fireOrb, waterOrb, woodOrb, metalOrb, earthOrb;
        [SerializeField] private Material monochromeOrb;

        [Header("Wet clay and flight trail pigments")]
        public Color fire = new Color32(222, 74, 33, 255);
        public Color water = new Color32(20, 88, 150, 255);
        public Color wood = new Color32(63, 102, 66, 255);
        public Color metal = new Color32(236, 170, 47, 255);
        public Color earth = new Color32(116, 77, 50, 255);
        public Color monochrome = new Color(.66f, .66f, .66f);
        [Range(0f, 1f)] public float wetGloss = .65f;

        [Header("Flight and impact • distances are in ball diameters")]
        [Range(.5f, 2.5f)] public float trailDiameters = 1.6f;
        [Min(.01f)] public float launchStretchSeconds = .14f;
        [Min(.01f)] public float pressSeconds = .052f;
        [Min(.02f)] public float spreadSeconds = .12f;
        public AnimationCurve spread = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Range(.7f, 2f)] public float normalSpread = 1.06f;
        [Range(1f, 2.6f)] public float strongSpread = 1.51f;
        [Range(0f, 1f)] public float normalStrength = .25f;
        [Range(0f, 1f)] public float strongStrength = 1f;
        [Range(3, 5)] public int clayDrops = 4;
        [Range(.2f, 1.3f)] public float dropLifetime = .56f;
        [Range(.2f, 2f)] public float dropSpeed = 1f;
        [Range(1f, 30f)] public float gravityDiameters = 13f;
        [Range(0f, .35f)] public float tipSag = .115f;
        [Tooltip("Minimum clay contact duration before the persistent paint takes over; adjusted by impact strength.")]
        [Min(.25f)] public float contactSeconds = .86f;
        [Min(0f)] public float markSeconds = 2f;

        [Header("Five-color reward • presentation only")]
        [Tooltip("Adds a heavier clay tail and five-color splash. Does not change ball size, force or paint area.")]
        public bool fiveColorBoostEnabled = true;
        [Range(.5f, 3.5f)] public float fiveColorTrailDiameters = 2.4f;
        [Range(1f, 2.5f)] public float fiveColorTrailWidthScale = 1.4f;
        [Range(1f, 2f)] public float fiveColorDropSizeScale = 1.3f;
        [Tooltip("Linear width/depth multiplier of the temporary clay splash, not painted surface area.")]
        [Range(1f, 1.8f)] public float fiveColorSpreadScale = 1.35f;
        [Range(1f, 1.5f)] public float fiveColorPressWidthScale = 1.2f;
        [Range(.025f, .15f)] public float fiveColorPressSeconds = .06f;
        [Range(.04f, .25f)] public float fiveColorSpreadSeconds = .10f;
        [Range(3, 5)] public int fiveColorClayDrops = 5;
        [Range(.2f, 1.3f)] public float fiveColorDropLifetime = .65f;
        [Range(.25f, 1.5f)] public float fiveColorContactSeconds = .95f;

        [Header("Maximum visible slots per scene")]
        [Range(5, 48)] public int maxFlights = 16;
        [Range(5, 64)] public int maxImpacts = 24;
        [Range(5, 24)] public int lowDetailImpacts = 10;

        public Material OrbSurface(LabOrbElement element, bool singleTone)
        {
            if (singleTone) return monochromeOrb;
            return element switch
            {
                LabOrbElement.Fire => fireOrb,
                LabOrbElement.Water => waterOrb,
                LabOrbElement.Wood => woodOrb,
                LabOrbElement.Metal => metalOrb,
                LabOrbElement.Earth => earthOrb,
                _ => null
            };
        }

        public Color Pigment(LabOrbElement element, bool singleTone)
        {
            if (singleTone) return monochrome;
            switch (element)
            {
                case LabOrbElement.Fire: return fire;
                case LabOrbElement.Water: return water;
                case LabOrbElement.Wood: return wood;
                case LabOrbElement.Metal: return metal;
                case LabOrbElement.Earth: return earth;
                default: return monochrome;
            }
        }
    }
}
