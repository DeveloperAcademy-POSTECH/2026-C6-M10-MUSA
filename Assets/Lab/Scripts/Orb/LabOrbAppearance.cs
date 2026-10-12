using UnityEngine;

namespace C6Lab
{
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
        [Header("Combined orb by element (empty = combined material tinted with a temporary color)")]
        [SerializeField] private Material fireMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material woodMaterial;
        [SerializeField] private Material metalMaterial;
        [SerializeField] private Material earthMaterial;
        [Header("Item icons (drawn on a disc facing the board camera)")]
        [SerializeField] private Texture2D relayIcon;
        [SerializeField] private Texture2D secretBoxIcon;
        [SerializeField] private Texture2D potionIcon;
        [SerializeField] private Texture2D bombIcon;
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

        public Material MaterialFor(LabOrbKind kind, LabOrbElement element = LabOrbElement.None)
        {
            if (kind == LabOrbKind.Combined)
            {
                Material elementMaterial = ElementMaterialFor(element);
                if (elementMaterial != null) return elementMaterial;
            }
            return kind switch
            {
                LabOrbKind.Yin => yinMaterial,
                LabOrbKind.Yang => yangMaterial,
                _ => combinedMaterial
            };
        }

        public Material ElementMaterialFor(LabOrbElement element)
        {
            return element switch
            {
                LabOrbElement.Fire => fireMaterial,
                LabOrbElement.Water => waterMaterial,
                LabOrbElement.Wood => woodMaterial,
                LabOrbElement.Metal => metalMaterial,
                LabOrbElement.Earth => earthMaterial,
                _ => null
            };
        }

        public Texture2D IconFor(LabOrbKind kind) => kind switch
        {
            LabOrbKind.Relay => relayIcon,
            LabOrbKind.SecretBox => secretBoxIcon,
            LabOrbKind.Potion => potionIcon,
            LabOrbKind.Bomb => bombIcon,
            _ => null
        };

        /// <summary>속성 에셋이 아직 없는 결합 구슬은 임시 색으로 구분한다.</summary>
        public bool NeedsTemporaryElementColor(LabOrbKind kind, LabOrbElement element)
        {
            return kind == LabOrbKind.Combined && element != LabOrbElement.None
                && ElementMaterialFor(element) == null;
        }

        /// <summary>
        /// Applies the kind's material only to the primary sphere. A separate roll marker
        /// keeps its own neutral material and stays readable while the orb rotates.
        /// A future art prefab can nest its renderer differently: the first renderer is
        /// used when a direct child named Sphere is absent.
        /// </summary>
        public void ApplyKindMaterial(GameObject artRoot, LabOrbKind kind,
            LabOrbElement element = LabOrbElement.None)
        {
            if (artRoot == null) return;
            Material material = MaterialFor(kind, element);
            if (material == null) return;
            Transform sphere = artRoot.transform.Find("Sphere");
            Renderer surface = sphere != null ? sphere.GetComponent<Renderer>() : null;
            if (surface == null) surface = artRoot.GetComponentInChildren<Renderer>(true);
            if (surface == null) return;
            LabOrbVisualFactory.ApplyAppearanceMaterial(surface, material);
            if (NeedsTemporaryElementColor(kind, element))
                LabOrbVisualFactory.ApplyElementColor(surface, element);
        }
    }
}
