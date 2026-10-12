using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Default orb colors. Yin/Yang/Combined surfaces take their color from the appearance
    /// materials; these defaults cover element and item tints, and the fallback sphere
    /// used when no appearance asset is assigned.
    /// </summary>
    public static class LabOrbPalette
    {
        public static readonly Color Yin = new Color(.1f, .1f, .12f);
        public static readonly Color Yang = new Color(.96f, .96f, .94f);
        public static readonly Color Fire = new Color(.9f, .17f, .14f);
        public static readonly Color Water = new Color(.13f, .42f, .95f);
        public static readonly Color Wood = new Color(.2f, .7f, .28f);
        public static readonly Color Metal = new Color(.8f, .66f, .46f);
        public static readonly Color Earth = new Color(.4f, .25f, .12f);
        public static readonly Color Combined = new Color(.55f, .29f, .81f);
        public static readonly Color Item = new Color(.53f, .34f, .84f);

        public static Color For(LabOrbKind kind, LabOrbElement element)
        {
            if (kind.IsItem()) return Item;
            return kind switch
            {
                LabOrbKind.Yin => Yin,
                LabOrbKind.Yang => Yang,
                _ => ElementOrNull(element) ?? Combined
            };
        }

        public static Color? ElementOrNull(LabOrbElement element) => element switch
        {
            LabOrbElement.Fire => Fire,
            LabOrbElement.Water => Water,
            LabOrbElement.Wood => Wood,
            LabOrbElement.Metal => Metal,
            LabOrbElement.Earth => Earth,
            _ => (Color?)null
        };
    }

    /// <summary>
    /// Inspector-editable orb presentation shared by board orbs and thrown projectiles.
    /// The optional prefab contains art only, authored at unit diameter. Physics, orb ID,
    /// ownership and gameplay radius always remain on each runtime object's root.
    /// </summary>
    [CreateAssetMenu(fileName = "LabOrbAppearance", menuName = "C6 Physics Lab/Orb Appearance")]
    public sealed class LabOrbAppearance : ScriptableObject
    {
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private Material yinMaterial;
        [SerializeField] private Material yangMaterial;
        [SerializeField] private Material combinedMaterial;
        [Header("Combined element tints (drawn over the combined material)")]
        [SerializeField] private Color fireColor = LabOrbPalette.Fire;
        [SerializeField] private Color waterColor = LabOrbPalette.Water;
        [SerializeField] private Color woodColor = LabOrbPalette.Wood;
        [SerializeField] private Color metalColor = LabOrbPalette.Metal;
        [SerializeField] private Color earthColor = LabOrbPalette.Earth;
        [Header("Item orbs (temporary tint until item icons)")]
        [SerializeField] private Color itemColor = LabOrbPalette.Item;
        [Header("Board contact shadow (presentation only)")]
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, .22f);
        [SerializeField] private Vector2 shadowScale = new Vector2(1.4f, .8f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(.06f, -.08f);
        [SerializeField] private float shadowDepth = .4f;

        public GameObject VisualPrefab => visualPrefab;
        public Material YinMaterial => yinMaterial;
        public Material YangMaterial => yangMaterial;
        public Material CombinedMaterial => combinedMaterial;
        public Material ShadowMaterial => shadowMaterial;
        public Color ShadowColor => shadowColor;
        public Vector2 ShadowScale => shadowScale;
        public Vector2 ShadowOffset => shadowOffset;
        public float ShadowDepth => shadowDepth;

        public Material MaterialFor(LabOrbKind kind)
        {
            return kind switch
            {
                LabOrbKind.Yin => yinMaterial,
                LabOrbKind.Yang => yangMaterial,
                _ => combinedMaterial
            };
        }

        /// <summary>A color drawn over the kind's material, or null to keep the material's own color.</summary>
        public Color? ColorOverrideFor(LabOrbKind kind, LabOrbElement element = LabOrbElement.None)
        {
            if (kind.IsItem()) return itemColor;
            if (kind != LabOrbKind.Combined) return null;
            return element switch
            {
                LabOrbElement.Fire => fireColor,
                LabOrbElement.Water => waterColor,
                LabOrbElement.Wood => woodColor,
                LabOrbElement.Metal => metalColor,
                LabOrbElement.Earth => earthColor,
                _ => (Color?)null
            };
        }

        /// <summary>
        /// Applies the kind's material only to the primary sphere. A separate roll marker
        /// keeps its own neutral material and stays readable while the orb rotates.
        /// A future art prefab can nest its renderer differently: the first renderer is
        /// used when a direct child named Sphere is absent.
        /// </summary>
        public void ApplyKindMaterial(GameObject artRoot, LabOrbKind kind, LabOrbElement element = LabOrbElement.None)
        {
            if (artRoot == null) return;
            Material material = MaterialFor(kind);
            if (material == null) return;
            Transform sphere = artRoot.transform.Find("Sphere");
            Renderer surface = sphere != null ? sphere.GetComponent<Renderer>() : null;
            if (surface == null) surface = artRoot.GetComponentInChildren<Renderer>(true);
            if (surface == null) return;
            LabOrbVisualFactory.ApplyAppearanceMaterial(surface, material, ColorOverrideFor(kind, element));
        }
    }
}
