using System;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 피버 타임 (#334): 전투가 정해 둔 초만큼 남으면 바로 시작해 끝날 때까지 이어진다. 요괴는 더 방해하지 않고 걸려 있던
    /// 방해도 그 순간 끝나며(넘기기 막기 해제, 매혹과 그 예고 풀림, 여우 구슬·잠긴 박 폭탄은 연기로 사라짐), 스태미나
    /// 자동 충전만 배율만큼 빨라진다. 명중 보상과 구슬 보유 한도는 그대로다. 지금은 구미호 스테이지만 쓴다.
    /// 시작 시각은 Host 시계로 정해 스냅샷에 실리므로 모든 화면이 같은 순간에 연출을 시작한다.
    /// </summary>
    public sealed partial class LabModel
    {
        // The Host time fever time starts; never for a round without it.
        private double feverTimeAt = double.PositiveInfinity;
        private bool feverTimeStarted;

        /// <summary>Whether fever time is on at this Host time (#334).</summary>
        public bool FeverTimeOn(double now) => phase == LabPhase.Playing && now >= feverTimeAt;

        private void ResetFeverTime(double now)
        {
            feverTimeStarted = false;
            // A battle shorter than the fever time is in it from the start.
            feverTimeAt = rules.FeverTimeSeconds > 0d ? Math.Max(now, deadline - rules.FeverTimeSeconds) : double.PositiveInfinity;
        }

        /// <summary>The part of [from, to] that falls in fever time, which refills the faster rate.</summary>
        private double FeverSecondsBetween(double from, double to) => Math.Max(0d, to - Math.Max(from, feverTimeAt));

        /// <summary>
        /// The moment fever time starts: no gimmick is cast from now on, including one due at this very time, and the ones
        /// still going end here. A gourd bomb that is already unlocked still opens into its two attack orbs.
        /// </summary>
        private void StartFeverTime(double now)
        {
            if (feverTimeStarted || now < feverTimeAt) return;
            feverTimeStarted = true;
            double at = feverTimeAt;
            nextCommonGimmickAt = nextUniqueGimmickAt = double.PositiveInfinity;
            if (BlockOn(at)) swipeBlockedUntil = at;
            if (CharmComingOrOn(at))
            {
                charmWarnedAt = -1d;
                charmStartedAt = Math.Min(charmStartedAt, at);
                charmUntil = at;
            }
            foreach (LabOrbState orb in orbs.Values.Where(orb => !orb.inFlight && (orb.kind == LabOrbKind.FoxFire
                || orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd && orb.lockSwipes > 0)).ToArray())
            {
                // Each goes up in its smoke on its holder's screen, as when an orb's lifetime runs out (#235).
                orbs.Remove(orb.id);
                orbExpiries.Add(new LabOrbExpiry(orb.owner, orb.id));
            }
        }
    }
}
