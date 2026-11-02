using UnityEngine;

namespace C6Lab
{
    /// <summary>Local stamina presentation only; no Host rules or shared game tuning.</summary>
    [CreateAssetMenu(fileName = "StaminaFeedbackProfile", menuName = "C6 Physics Lab/Stamina Feedback")]
    public sealed class LabStaminaFeedbackProfile : ScriptableObject
    {
        public const string ResourcesPath = "Stamina/StaminaFeedbackProfile";
        [SerializeField] private bool consumptionEnabled = true;
        [SerializeField, Range(.05f, .5f)] private float consumptionSeconds = .18f;
        [SerializeField] private Color consumptionColor = new Color(.65f, .95f, 1f, .9f);
        [SerializeField] private bool personalFireEnabled = true;
        [SerializeField, Range(.05f, 1f)] private float ignitionSeconds = .4f;
        [Tooltip("Fire height relative to the existing fill rectangle; it does not resize that rectangle.")]
        [SerializeField, Range(.2f, 1.5f)] private float flameHeightRatio = .85f;
        [SerializeField, Range(.025f, .2f)] private float borderWidthRatio = .11f;
        [SerializeField, Range(1f, 24f)] private float animationFps = 12f;
        [SerializeField, Range(0f, 1f)] private float fireOpacity = .95f;
        [Tooltip("Optional override. By default reuse the approved Jigwi B reward flame atlas.")]
        [SerializeField] private Texture2D flameAtlas;

        public bool ConsumptionEnabled => consumptionEnabled;
        public float ConsumptionSeconds => Safe(consumptionSeconds, .05f, .5f, .18f);
        public Color ConsumptionColor => ColorSafe(consumptionColor);
        public bool PersonalFireEnabled => personalFireEnabled;
        public float IgnitionSeconds => Safe(ignitionSeconds, .05f, 1f, .4f);
        public float FlameHeightRatio => Safe(flameHeightRatio, .2f, 1.5f, .85f);
        public float BorderWidthRatio => Safe(borderWidthRatio, .025f, .2f, .11f);
        public float AnimationFps => Safe(animationFps, 1f, 24f, 12f);
        public float FireOpacity => Safe(fireOpacity, 0f, 1f, .95f);
        public Texture2D FlameAtlas => flameAtlas;
        private static float Safe(float value, float min, float max, float fallback) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
        private static Color ColorSafe(Color value) => new Color(Safe(value.r, 0, 1, .65f),
            Safe(value.g, 0, 1, .95f), Safe(value.b, 0, 1, 1f), Safe(value.a, 0, 1, .9f));
    }
}
