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
        [Header("Generate drum (drawn facing the board camera)")]
        [SerializeField] private Sprite drumSprite;
        [Header("Locked item (presentation only)")]
        [SerializeField] private Texture2D lockIcon;
        [Tooltip("Longer side of the padlock image, in orb radii.")]
        [SerializeField, Min(.1f)] private float lockIconSize = 1.2f;
        [Tooltip("Item icon opacity while the padlock covers it.")]
        [SerializeField, Range(0f, 1f)] private float lockedIconAlpha = .4f;
        [Header("Unlock effect (presentation only)")]
        [Tooltip("Seconds for the padlock to rise and fade while the icon sharpens.")]
        [SerializeField, Min(.01f)] private float unlockLockSeconds = .3f;
        [Tooltip("How far the padlock rises while it fades, in orb radii.")]
        [SerializeField, Min(0f)] private float unlockLockRise = .8f;
        [Tooltip("Ring diameter when it appears, in orb diameters.")]
        [SerializeField, Min(1f)] private float unlockRingStartScale = 1.5f;
        [Tooltip("Seconds after the unlock before the ring appears.")]
        [SerializeField, Min(0f)] private float unlockRingDelay = .1f;
        [Tooltip("Seconds for the ring to shrink onto the orb, slowing as it lands.")]
        [SerializeField, Min(.01f)] private float unlockRingSeconds = .4f;
        [Tooltip("Seconds of the single glow as the ring lands.")]
        [SerializeField, Min(.01f)] private float unlockGlowSeconds = .1f;
        [SerializeField] private Color unlockRingColor = new Color(1f, .8f, .25f, 1f);
        [Tooltip("Glow tint; its alpha is the peak strength.")]
        [SerializeField] private Color unlockGlowColor = new Color(1f, .95f, .7f, .85f);
        [Header("Board contact shadow (presentation only)")]
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, .22f);
        [SerializeField] private Vector2 shadowScale = new Vector2(1.4f, .8f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(.06f, -.08f);
        [SerializeField] private float shadowDepth = .4f;
        [Header("Thrown orb trail (presentation only)")]
        [SerializeField] private float throwTrailSeconds = .28f;
        [SerializeField] private float throwTrailWidthScale = .75f;
        [SerializeField] private float throwTrailMinVertexDistance = .035f;
        [SerializeField] private Material throwTrailMaterial;

        public GameObject VisualPrefab => visualPrefab;
        public Material YinMaterial => yinMaterial;
        public Material YangMaterial => yangMaterial;
        public Material CombinedMaterial => combinedMaterial;
        public Sprite DrumSprite => drumSprite;
        public Texture2D LockIcon => lockIcon;
        public float LockIconSize => lockIconSize > 0f ? lockIconSize : 1.2f;
        public float LockedIconAlpha => Mathf.Clamp01(lockedIconAlpha);
        public float UnlockLockSeconds => unlockLockSeconds > 0f ? unlockLockSeconds : .3f;
        public float UnlockLockRise => Mathf.Max(0f, unlockLockRise);
        public float UnlockRingStartScale => Mathf.Max(1f, unlockRingStartScale);
        public float UnlockRingDelay => Mathf.Max(0f, unlockRingDelay);
        public float UnlockRingSeconds => unlockRingSeconds > 0f ? unlockRingSeconds : .4f;
        public float UnlockGlowSeconds => unlockGlowSeconds > 0f ? unlockGlowSeconds : .1f;
        public Color UnlockRingColor => unlockRingColor;
        public Color UnlockGlowColor => unlockGlowColor;
        /// <summary>The whole unlock presentation: the padlock lift, or the ring landing and its glow.</summary>
        public float UnlockEffectSeconds =>
            Mathf.Max(UnlockLockSeconds, UnlockRingDelay + UnlockRingSeconds + UnlockGlowSeconds);
        public Material ShadowMaterial => shadowMaterial;
        public Color ShadowColor => shadowColor;
        public Vector2 ShadowScale => shadowScale;
        public Vector2 ShadowOffset => shadowOffset;
        public float ShadowDepth => shadowDepth;
        public float ThrowTrailSeconds => throwTrailSeconds > 0f ? throwTrailSeconds : .28f;
        public float ThrowTrailWidthScale => throwTrailWidthScale > 0f ? throwTrailWidthScale : .75f;
        public float ThrowTrailMinVertexDistance => throwTrailMinVertexDistance > 0f
            ? throwTrailMinVertexDistance : .035f;
        public Material ThrowTrailMaterial => throwTrailMaterial;

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

        /// <summary>Shared fallback tint for the orb palette and approved elemental hit paint.</summary>
        public static Color DefaultElementColor(LabOrbElement element)
            => LabOrbVisualFactory.TemporaryElementColor(element);

        public Color ElementColorFor(LabOrbElement element)
        {
            Material material = ElementMaterialFor(element);
            if (material != null && material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material != null && material.HasProperty("_Color")) return material.GetColor("_Color");
            return DefaultElementColor(element);
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
