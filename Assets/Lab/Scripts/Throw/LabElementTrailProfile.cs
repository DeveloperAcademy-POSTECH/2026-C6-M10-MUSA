using UnityEngine;

namespace C6Lab
{
    public enum LabElementTrailShape { Flame, Stream, Leaves, ParallelInk, DryClumps }

    /// <summary>Presentation only. None of these values participate in throwing or hit resolution.</summary>
    [CreateAssetMenu(fileName = "ElementFlightTrail", menuName = "C6 Physics Lab/Element Flight Trail")]
    public sealed class LabElementTrailProfile : ScriptableObject
    {
        [SerializeField] private LabOrbElement element = LabOrbElement.Fire;
        [SerializeField] private LabElementTrailShape shape = LabElementTrailShape.Flame;
        [SerializeField] private Color color = new Color(.89f, .29f, .14f, 1f);
        [Tooltip("Maximum length of the retained path, in world units; independent of its lifetime.")]
        [SerializeField, Min(.01f)] private float maxLength = 1.8f;
        [Tooltip("Overall cross width in world units: ribbon width (all three Metal strands) and brush-stamp width.")]
        [SerializeField, Min(.005f)] private float width = .28f;
        [Tooltip("Scaled seconds a passed point remains visible. Session pause also pauses the trail.")]
        [SerializeField, Min(.01f)] private float lifetime = .4f;
        [Tooltip("Distance along the actual flight between brush stamps, in world units.")]
        [SerializeField, Min(.01f)] private float particleSpacing = .2f;
        [Tooltip("Length of each brush/drop/leaf stamp along its own painted axis, in world units.")]
        [SerializeField, Min(.005f)] private float particleSize = .3f;
        [Tooltip("Transparent ink/wash artwork: top points toward the projectile, bottom toward the tail.")]
        [SerializeField] private Texture2D ribbonTexture;
        [Tooltip("Transparent brush/drop/leaf artwork: top points along the recorded flight direction.")]
        [SerializeField] private Texture2D particleTexture;
        [SerializeField] private Material material;

        public LabOrbElement Element => element >= LabOrbElement.Fire && element <= LabOrbElement.Earth
            ? element : LabOrbElement.None;
        public LabElementTrailShape Shape => shape >= LabElementTrailShape.Flame
            && shape <= LabElementTrailShape.DryClumps ? shape : LabElementTrailShape.Flame;
        public Color Color => new Color(Finite(color.r, 1f), Finite(color.g, 1f),
            Finite(color.b, 1f), Mathf.Clamp01(Finite(color.a, 1f)));
        public float MaxLength => Clamp(maxLength, .01f, 128f, 1.8f);
        public float Width => Clamp(width, .005f, 10f, .28f);
        public float Lifetime => Clamp(lifetime, .01f, 10f, .4f);
        public float ParticleSpacing => Clamp(particleSpacing, .01f, 16f, .2f);
        public float ParticleSize => Clamp(particleSize, .005f, 10f, .3f);
        public Texture2D RibbonTexture => ribbonTexture;
        public Texture2D ParticleTexture => particleTexture;
        public Material Material => material;

        private static float Finite(float value, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        private static float Clamp(float value, float min, float max, float fallback)
            => Mathf.Clamp(Finite(value, fallback), min, max);
    }
}
