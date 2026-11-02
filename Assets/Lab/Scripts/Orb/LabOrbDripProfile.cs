using UnityEngine;

namespace C6Lab
{
    /// <summary>Local board ink artwork and timing only; never shared gameplay or physics settings.</summary>
    [CreateAssetMenu(fileName = "BoardPaintDrips", menuName = "C6 Physics Lab/Orb Board Paint Drips")]
    public sealed class LabOrbDripProfile : ScriptableObject
    {
        public const string ResourcesPath = "OrbBoard/BoardPaintDrips";
        public const string ResourcePath = ResourcesPath;
        [SerializeField, Range(.05f, 1.5f)] private float diameterScale = .7f;
        [Tooltip("Actual travelled distance between stamps, in displayed orb radii.")]
        [SerializeField, Range(.1f, 8f)] private float spacingRadii = 1.4f;
        [Header("Fall → fixed landing → reveal → fade (scaled seconds)")]
        [SerializeField, Range(.01f, .5f)] private float fallDuration = .08f;
        [SerializeField, Range(.01f, 1f)] private float spreadDuration = .16f;
        [SerializeField, Range(0f, 2f)] private float holdDuration = .12f;
        [SerializeField, Range(.01f, 2f)] private float fadeDuration = .44f;
        [Tooltip("Short decorative fall in displayed orb radii; never moves the orb.")]
        [SerializeField, Range(0f, 4f)] private float fallDistanceRadii = .8f;
        [SerializeField, Range(0f, 1f)] private float strength = .88f;
        [Tooltip("Minimum relative opacity of an emitted stamp above the movement threshold.")]
        [SerializeField, Range(0f, 1f)] private float minimumOpacity = .45f;
        [SerializeField, Range(0f, 50f)] private float fadeInSpeed = .5f;
        [SerializeField, Range(.1f, 100f)] private float fullSpeed = 4f;
        [SerializeField, Range(1, 96)] private int maxDrops = 24;
        [SerializeField, Range(0f, 1f)] private float spatterChance = .45f;
        [Header("Transparent neutral wash; source aspect ratio is preserved")]
        [SerializeField] private Texture2D[] stainTextures = new Texture2D[3];
        [SerializeField] private Texture2D[] spatterTextures = new Texture2D[2];
        [SerializeField] private Material material;

        public float DiameterScale => Safe(diameterScale, .05f, 1.5f, .7f);
        public float SpacingRadii => Safe(spacingRadii, .1f, 8f, 1.4f);
        public float FallDuration => Safe(fallDuration, .01f, .5f, .08f);
        public float SpreadDuration => Safe(spreadDuration, .01f, 1f, .16f);
        public float HoldDuration => Safe(holdDuration, 0f, 2f, .12f);
        public float FadeDuration => Safe(fadeDuration, .01f, 2f, .44f);
        public float Lifetime => FallDuration + SpreadDuration + HoldDuration + FadeDuration;
        public float FallDistanceRadii => Safe(fallDistanceRadii, 0f, 4f, .8f);
        public float Strength => Safe(strength, 0f, 1f, .88f);
        public float MinimumOpacity => Safe(minimumOpacity, 0f, 1f, .45f);
        public float FadeInSpeed => Safe(fadeInSpeed, 0f, 50f, .5f);
        public float FullSpeed => Mathf.Max(FadeInSpeed + .01f, Safe(fullSpeed, .1f, 100f, 4f));
        public int MaxDrops => Mathf.Clamp(maxDrops, 1, 96);
        public float SpatterChance => Safe(spatterChance, 0f, 1f, .45f);
        public Texture2D StainTexture(int variant) => Texture(stainTextures, variant);
        public Texture2D SpatterTexture(int variant) => Texture(spatterTextures, variant);
        public Material Material => material;
        public bool HasArtwork => material != null && StainTexture(0) != null && StainTexture(1) != null
            && StainTexture(2) != null && SpatterTexture(0) != null && SpatterTexture(1) != null;
        private static Texture2D Texture(Texture2D[] textures, int index) =>
            textures != null && index >= 0 && index < textures.Length ? textures[index] : null;
        private static float Safe(float value, float min, float max, float fallback) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
    }
}
