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
        [Header("스태미나 부족 (#418): 북을 눌렀는데 스태미나가 모자라 생성이 거절될 때")]
        [SerializeField] private bool shortEnabled = true;
        [Tooltip("게이지가 지지직 흔들리는 시간 (초)")]
        [SerializeField, Range(.05f, 1f)] private float shortShakeSeconds = .32f;
        [Tooltip("흔들리는 거리 (게이지 높이 대비). 시간이 지날수록 줄어든다.")]
        [SerializeField, Range(0f, 1f)] private float shortShakeRatio = .3f;
        [Tooltip("1초에 몇 번 흔들리는지. 클수록 잘게 떨린다.")]
        [SerializeField, Range(5f, 60f)] private float shortShakeFrequency = 32f;
        [Tooltip("충전 중인 칸이 빨갛게 빛나는 시간 (초)")]
        [SerializeField, Range(.05f, 1f)] private float shortFlashSeconds = .4f;
        [SerializeField] private Color shortFlashColor = new Color(1f, .16f, .12f, .95f);

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
        public bool ShortEnabled => shortEnabled;
        public float ShortShakeSeconds => Safe(shortShakeSeconds, .05f, 1f, .32f);
        public float ShortShakeRatio => Safe(shortShakeRatio, 0f, 1f, .3f);
        public float ShortShakeFrequency => Safe(shortShakeFrequency, 5f, 60f, 32f);
        public float ShortFlashSeconds => Safe(shortFlashSeconds, .05f, 1f, .4f);
        public Color ShortFlashColor => new Color(Safe(shortFlashColor.r, 0, 1, 1f), Safe(shortFlashColor.g, 0, 1, .16f),
            Safe(shortFlashColor.b, 0, 1, .12f), Safe(shortFlashColor.a, 0, 1, .95f));
        private static float Safe(float value, float min, float max, float fallback) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
        private static Color ColorSafe(Color value) => new Color(Safe(value.r, 0, 1, .65f),
            Safe(value.g, 0, 1, .95f), Safe(value.b, 0, 1, 1f), Safe(value.a, 0, 1, .9f));
    }
}
