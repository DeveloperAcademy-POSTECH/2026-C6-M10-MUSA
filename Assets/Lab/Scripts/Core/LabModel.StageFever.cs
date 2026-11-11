using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 세 스테이지 피버 통일 (#441). 피버가 시작되는 시각은 스테이지마다 지금 그대로다.
    /// - 구미호: 남은 시간이 피버 시간(기본 20초)이 된 때 (#334, feverTimeAt)
    /// - 야광귀: 시간표의 방해가 끝나는 때 (#343, 기본 100초)
    /// - 지귀: 방해가 끝나는 100초 (#332, jigwiStageCutoffAt)
    /// 그 시각부터 세 스테이지 모두 같은 규칙을 쓴다: 북에서 나오는 것은 모두 바로 던질 수 있는 오색구슬(#329)이고,
    /// 요괴 방해는 더 없고 걸려 있던 방해(넘기기 막기, 매혹과 그 예고, 여우 구슬)도 그 순간 풀린다.
    /// 스태미나 자동 회복 배율은 스테이지마다 원래 있던 설정을 그대로 쓴다(기본 모두 2배). 2:2와 Lab 단판에는 없다.
    /// </summary>
    public sealed partial class LabModel
    {
        /// <summary>이 라운드의 스테이지 피버가 시작되는(된) Host 시각. 피버가 없는 라운드면 -1.</summary>
        public double StageFeverStartsAt
        {
            get
            {
                if (rules == null || teamMode) return -1d;
                double yaguang = YaguangFeverStartsAt;
                if (yaguang >= 0d) return yaguang;
                if (rules.FeverTimeSeconds > 0d && !double.IsInfinity(feverTimeAt)) return feverTimeAt;
                if (JigwiStageEnabled && !double.IsInfinity(jigwiStageCutoffAt)) return jigwiStageCutoffAt;
                return -1d;
            }
        }

        /// <summary>at이 스테이지 피버 안인지 (전투 중일 때만)</summary>
        public bool StageFeverOn(double at)
        {
            double feverAt = StageFeverStartsAt;
            return feverAt >= 0d && phase == LabPhase.Playing && at >= feverAt;
        }

        /// <summary>
        /// 스테이지 피버 중 북에서 나온 것(음·양 구슬, 아이템 모두)을 바로 던질 수 있는 오색구슬로 바꾼다.
        /// 2:2 팀 피버(#281)의 결합 구슬은 건드리지 않는다.
        /// </summary>
        private void ApplyStageFeverGeneration(LabOrbState orb, double now)
        {
            if (orb == null || orb.fever || !StageFeverOn(now)) return;
            orb.kind = LabOrbKind.Combined;
            orb.element = LabOrbElement.None;
            orb.fiveColor = true;
            orb.lockSwipes = 0;
            orb.relayStage = 0;
        }

        /// <summary>
        /// 피버가 시작되는 순간 걸려 있던 공용 방해를 푼다: 넘기기 막기, 매혹과 그 예고, 여우 구슬(연기로 사라짐).
        /// 구미호 피버 타임(#334)이 하던 것을 야광귀/지귀 피버에도 똑같이 쓴다. 박 폭탄은 스테이지마다 원래 하던 대로 둔다.
        /// </summary>
        private void ReleaseInterferenceForFever(double at)
        {
            if (BlockOn(at)) swipeBlockedUntil = at;
            if (CharmComingOrOn(at))
            {
                charmWarnedAt = -1d;
                charmStartedAt = System.Math.Min(charmStartedAt, at);
                EndAllCharms(at);
            }
            // #664: a fox round on now ends with no reward or penalty (one already over before the fever is settled first).
            AdvanceFoxRound(at);
            CancelFoxRound();
            foreach (LabOrbState orb in orbs.Values.Where(orb => !orb.inFlight && orb.kind == LabOrbKind.FoxFire).ToArray())
            {
                // 수명이 다한 구슬처럼 가진 사람 화면에서 연기로 사라진다 (#235).
                orbs.Remove(orb.id);
                orbExpiries.Add(new LabOrbExpiry(orb.owner, orb.id));
            }
        }
    }
}
