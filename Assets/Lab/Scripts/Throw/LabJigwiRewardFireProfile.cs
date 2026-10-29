using UnityEngine;

namespace C6Lab
{
    /// <summary>Local B-success artwork and timing only; never part of Host combat or shared tuning.</summary>
    [CreateAssetMenu(fileName = "RewardFireProfile", menuName = "C6 Physics Lab/Jigwi Reward Fire")]
    public sealed class LabJigwiRewardFireProfile : ScriptableObject
    {
        public const string ResourcesPath = "Jigwi/RewardFire/RewardFireProfile";
        public const string ResourcePath = ResourcesPath;
        [Tooltip("Overall ribbon width relative to the accepted projectile diameter.")]
        [SerializeField, Range(.1f, 3f)] private float widthScale = 1.3f;
        [Tooltip("Maximum retained real flight length, measured in projectile diameters.")]
        [SerializeField, Range(.5f, 16f)] private float maxLengthDiameters = 5f;
        [Tooltip("Scaled seconds each passed position remains visible; session pause pauses this effect.")]
        [SerializeField, Range(.05f, 2f)] private float lifetime = .45f;
        [SerializeField, Range(1f, 30f)] private float flameAnimationFps = 12f;
        [Tooltip("Distance along the actual path between sparks, measured in projectile diameters.")]
        [SerializeField, Range(.1f, 4f)] private float emberSpacingDiameters = .65f;
        [SerializeField, Range(.05f, 2f)] private float emberLifetime = .35f;
        [SerializeField, Range(0, 128)] private int maxParticles = 32;
        [Tooltip("White preserves authored warm core, vermilion wash and black ink; alpha controls strength.")]
        [SerializeField] private Color flameColor = Color.white;
        [SerializeField] private Color emberColor = new Color(1f, .8f, .48f, .9f);
        [Tooltip("Transparent 2x2 atlas: frame 0 top left, then top right, bottom left, bottom right.")]
        [SerializeField] private Texture2D flameTexture;
        [SerializeField] private Texture2D emberTexture;
        [Tooltip("Transparent vertical ribbon: top points toward the orb, bottom toward its old path.")]
        [SerializeField] private Texture2D ribbonTexture;
        [Tooltip("Existing C6Lab/Element Ink Trail material. No new render pipeline or post-processing required.")]
        [SerializeField] private Material material;

        public float WidthScale => Safe(widthScale, .1f, 3f, 1.3f);
        public float MaxLengthDiameters => Safe(maxLengthDiameters, .5f, 16f, 5f);
        public float Lifetime => Safe(lifetime, .05f, 2f, .45f);
        public float FlameAnimationFps => Safe(flameAnimationFps, 1f, 30f, 12f);
        public float EmberSpacingDiameters => Safe(emberSpacingDiameters, .1f, 4f, .65f);
        public float EmberLifetime => Safe(emberLifetime, .05f, 2f, .35f);
        public int MaxParticles => Mathf.Clamp(maxParticles, 0, 128);
        public Color FlameColor => SafeColor(flameColor);
        public Color EmberColor => SafeColor(emberColor);
        public Texture2D FlameTexture => flameTexture;
        public Texture2D EmberTexture => emberTexture;
        public Texture2D RibbonTexture => ribbonTexture;
        public Material Material => material;
        private static float Safe(float value, float min, float max, float fallback) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
        private static Color SafeColor(Color color) => new Color(Safe(color.r, 0f, 4f, 1f),
            Safe(color.g, 0f, 4f, 1f), Safe(color.b, 0f, 4f, 1f), Safe(color.a, 0f, 1f, 1f));
    }
}
