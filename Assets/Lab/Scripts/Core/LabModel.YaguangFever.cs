namespace C6Lab
{
    /// <summary>
    /// 야광귀 피버타임 (#343). 야광귀 시간표의 방해가 끝나는 때(기본 100초)부터 전투가 끝날 때까지 스태미나 자동 회복이
    /// 빨라지고(Inspector 배율, 기본 2배), 북을 눌러 나오는 것은 모두 바로 던질 수 있는 오색구슬(#329)이 된다. 지귀 120초(#332)·구미호 피버 타임(#334)과 따로 돌아가며, 야광귀 스테이지에서만 켜진다. 시각은 Host의 전투 시계(일시정지 제외)다.
    /// </summary>
    public sealed partial class LabModel
    {
        /// <summary>야광귀 피버가 시작되는(된) 전투 시각. 야광귀 시간표가 아니면 -1.</summary>
        public double YaguangFeverStartsAt => rules != null && rules.YaguangTimeline && rules.YaguangTimelineEndSeconds > 0d
            && !double.IsInfinity(yaguangTimelineEndAt) ? yaguangTimelineEndAt : -1d;

        /// <summary>[from, to] 가운데 야광귀 피버에 든 초. 그만큼만 빠르게 충전한다 (구미호 피버 타임 #334와 같은 계산).</summary>
        private double YaguangFeverSecondsBetween(double from, double to)
        {
            double feverAt = YaguangFeverStartsAt;
            return feverAt < 0d ? 0d : System.Math.Max(0d, to - System.Math.Max(from, feverAt));
        }

        /// <summary>
        /// 야광귀 피버 중 북에서 나온 것(음·양 구슬, 아이템 모두)을 바로 던질 수 있는 오색구슬로 바꾼다.
        /// 구미호 피버 타임(#334)의 결합 구슬은 건드리지 않는다.
        /// </summary>
        private void ApplyYaguangFeverGeneration(LabOrbState orb, double now)
        {
            double feverAt = YaguangFeverStartsAt;
            if (orb == null || orb.fever || feverAt < 0d || now < feverAt) return;
            orb.kind = LabOrbKind.Combined;
            orb.element = LabOrbElement.None;
            orb.fiveColor = true;
            orb.lockSwipes = 0;
            orb.relayStage = 0;
        }

        /// <summary>at 시각의 스태미나 자동 회복 배율: 야광귀 피버 중이면 Inspector 배율, 아니면 1.</summary>
        public float YaguangFeverRecoveryMultiplier(double at)
        {
            double feverAt = YaguangFeverStartsAt;
            return feverAt >= 0d && phase == LabPhase.Playing && at >= feverAt ? rules.YaguangFeverStaminaMultiplier : 1f;
        }
    }
}
