namespace C6Lab
{
    /// <summary>
    /// 야광귀 피버타임 (#343). 야광귀 시간표의 방해가 끝나는 때(기본 100초)부터 전투가 끝날 때까지 스태미나 자동 회복이
    /// 빨라진다(Inspector 배율, 기본 2배). 지귀 120초(#332)의 피버와 같은 방식이다. 시각은 Host의 전투 시계(일시정지 제외)다.
    /// </summary>
    public sealed partial class LabModel
    {
        /// <summary>야광귀 피버가 시작되는(된) 전투 시각. 야광귀 시간표가 아니면 -1.</summary>
        public double YaguangFeverStartsAt => rules != null && rules.YaguangTimeline && rules.YaguangTimelineEndSeconds > 0d
            && !double.IsInfinity(yaguangTimelineEndAt) ? yaguangTimelineEndAt : -1d;

        /// <summary>at 시각의 스태미나 자동 회복 배율: 야광귀 피버 중이면 Inspector 배율, 아니면 1.</summary>
        public float YaguangFeverRecoveryMultiplier(double at)
        {
            double feverAt = YaguangFeverStartsAt;
            return feverAt >= 0d && phase == LabPhase.Playing && at >= feverAt ? rules.YaguangFeverStaminaMultiplier : 1f;
        }
    }
}
