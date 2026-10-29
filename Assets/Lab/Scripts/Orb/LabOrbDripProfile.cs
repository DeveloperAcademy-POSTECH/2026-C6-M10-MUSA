using UnityEngine;

namespace C6Lab
{
    /// <summary>Local board paint-drop artwork and motion only; no Host rule, shared setting or orb physics.</summary>
    [CreateAssetMenu(fileName = "BoardPaintDrips", menuName = "C6 Physics Lab/Orb Board Paint Drips")]
    public sealed class LabOrbDripProfile : ScriptableObject
    {
        public const string ResourcesPath = "OrbBoard/BoardPaintDrips";
        public const string ResourcePath = ResourcesPath;
        [Tooltip("Drop width as a fraction of the source orb's displayed diameter.")]
        [SerializeField, Range(.05f, 1.5f)] private float diameterScale = .36f;
        [Tooltip("Actual travelled distance between drops, measured in source orb radii.")]
        [SerializeField, Range(.1f, 8f)] private float spacingRadii = .8f;
        [Tooltip("Scaled seconds each drop remains; board/session pause pauses its age.")]
        [SerializeField, Range(.05f, 4f)] private float lifetime = .7f;
        [Tooltip("Decorative fall over one drop's lifetime, measured in source orb radii. Does not move the orb.")]
        [SerializeField, Range(0f, 12f)] private float fallDistanceRadii = 2f;
        [SerializeField, Range(0f, 1f)] private float strength = .85f;
        [Tooltip("Displayed board movement speed where paint-drop strength starts to fade in.")]
        [SerializeField, Range(0f, 50f)] private float fadeInSpeed = 1.5f;
        [Tooltip("Displayed board movement speed for full strength. Effective value always exceeds Fade In Speed.")]
        [SerializeField, Range(.1f, 100f)] private float fullSpeed = 10f;
        [Tooltip("Bounded live drops per orb; no unbounded trail history.")]
        [SerializeField, Range(1, 96)] private int maxDrops = 48;
        [Tooltip("Transparent white wash and black ink artwork; vertex colour supplies the current element pigment.")]
        [SerializeField] private Texture2D dropTexture;
        [Tooltip("Shared C6Lab/Element Ink Trail material. The runtime uses property blocks, never edits this asset.")]
        [SerializeField] private Material material;

        public float DiameterScale => Safe(diameterScale, .05f, 1.5f, .36f);
        public float SpacingRadii => Safe(spacingRadii, .1f, 8f, .8f);
        public float Lifetime => Safe(lifetime, .05f, 4f, .7f);
        public float FallDistanceRadii => Safe(fallDistanceRadii, 0f, 12f, 2f);
        public float Strength => Safe(strength, 0f, 1f, .85f);
        public float FadeInSpeed => Safe(fadeInSpeed, 0f, 50f, 1.5f);
        public float FullSpeed => Mathf.Max(FadeInSpeed + .01f, Safe(fullSpeed, .1f, 100f, 10f));
        public int MaxDrops => Mathf.Clamp(maxDrops, 1, 96);
        public Texture2D DropTexture => dropTexture;
        public Material Material => material;
        private static float Safe(float value, float min, float max, float fallback) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
    }
}
