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
        [Header("스태미나 부족 (#718): 게이지가 커졌다 작아짐, 가운데 경고, 화면 가장자리 빨간빛")]
        [Tooltip("흔들리는 동안 게이지가 잠깐 커지는 크기 (1 = 그대로). 흔들림 시간 안에 커졌다가 원래 크기로 돌아온다.")]
        [SerializeField, Range(1f, 1.5f)] private float shortGaugePopScale = 1.15f;
        [SerializeField] private bool shortWarningEnabled = true;
        [Tooltip("\"기운이 부족하다\"와 빨간 느낌표가 떠 있는 시간 (초). 끝 0.2초 동안 사라진다.")]
        [SerializeField, Range(.3f, 2f)] private float shortWarningSeconds = .8f;
        [Tooltip("경고 글자 크기 (Canvas 기준)")]
        [SerializeField, Range(12f, 48f)] private float shortWarningFontSize = 22f;
        [Tooltip("경고 글자 색 (\"기운이 부족하다\"). 느낌표 그림의 색은 그대로다.")]
        [SerializeField] private Color shortWarningTextColor = new Color(.1f, .08f, .07f, 1f);
        [Tooltip("경고 자리 (화면 비율, 왼쪽 아래 0). 기본은 \"시작!\"과 같은 화면 가운데: 고유 기믹 안내 글(0.6)과 겹치지 않게 그보다 아래.")]
        [SerializeField] private Vector2 shortWarningAnchor = new Vector2(.5f, .5f);
        [Tooltip("경고가 게이지와 함께 흔들리는 거리 (Canvas 기준)")]
        [SerializeField, Range(0f, 20f)] private float shortWarningShakeDistance = 5f;
        [Tooltip("화면 가장자리 빨간빛이 번쩍하는 시간 (초)")]
        [SerializeField, Range(.05f, 1f)] private float shortEdgeSeconds = .4f;
        [Tooltip("화면 가장자리 빨간빛의 색 (알파 = 가장 진할 때)")]
        [SerializeField] private Color shortEdgeColor = new Color(1f, .12f, .08f, .6f);

        public bool ConsumptionEnabled => consumptionEnabled;
        public float ConsumptionSeconds => Safe(consumptionSeconds, .05f, .5f, .18f);
        public Color ConsumptionColor => ColorSafe(consumptionColor);
        public bool ShortEnabled => shortEnabled;
        public float ShortShakeSeconds => Safe(shortShakeSeconds, .05f, 1f, .32f);
        public float ShortShakeRatio => Safe(shortShakeRatio, 0f, 1f, .3f);
        public float ShortShakeFrequency => Safe(shortShakeFrequency, 5f, 60f, 32f);
        public float ShortFlashSeconds => Safe(shortFlashSeconds, .05f, 1f, .4f);
        public float ShortGaugePopScale => Safe(shortGaugePopScale, 1f, 1.5f, 1.15f);
        public bool ShortWarningEnabled => shortWarningEnabled;
        public float ShortWarningSeconds => Safe(shortWarningSeconds, .3f, 2f, .8f);
        public float ShortWarningFontSize => Safe(shortWarningFontSize, 12f, 48f, 22f);
        public Color ShortWarningTextColor => new Color(Safe(shortWarningTextColor.r, 0, 1, .1f), Safe(shortWarningTextColor.g, 0, 1, .08f),
            Safe(shortWarningTextColor.b, 0, 1, .07f), Safe(shortWarningTextColor.a, 0, 1, 1f));
        public Vector2 ShortWarningAnchor => new Vector2(Safe(shortWarningAnchor.x, 0f, 1f, .5f), Safe(shortWarningAnchor.y, 0f, 1f, .5f));
        public float ShortWarningShakeDistance => Safe(shortWarningShakeDistance, 0f, 20f, 5f);
        public float ShortEdgeSeconds => Safe(shortEdgeSeconds, .05f, 1f, .4f);
        public Color ShortEdgeColor => new Color(Safe(shortEdgeColor.r, 0, 1, 1f), Safe(shortEdgeColor.g, 0, 1, .12f),
            Safe(shortEdgeColor.b, 0, 1, .08f), Safe(shortEdgeColor.a, 0, 1, .6f));
        public Color ShortFlashColor => new Color(Safe(shortFlashColor.r, 0, 1, 1f), Safe(shortFlashColor.g, 0, 1, .16f),
            Safe(shortFlashColor.b, 0, 1, .12f), Safe(shortFlashColor.a, 0, 1, .95f));
        private static float Safe(float value, float min, float max, float fallback) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
        private static Color ColorSafe(Color value) => new Color(Safe(value.r, 0, 1, .65f),
            Safe(value.g, 0, 1, .95f), Safe(value.b, 0, 1, 1f), Safe(value.a, 0, 1, .9f));
    }
}
