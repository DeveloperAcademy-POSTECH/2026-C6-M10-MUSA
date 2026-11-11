using System;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 여우구슬 팀 판정 (#664). 여우불이 떨어지면 그 순간부터 정해 둔 시간(Rules.FoxRoundSeconds, 기본 15초) 동안 팀이 모든
    /// 판의 여우구슬을 다 지워야 한다. 여우구슬은 그동안 저절로 사라지지 않는다(사라지는 시각이 판정 끝으로 맞춰진다).
    /// - 시간 안에 팀 전체 여우구슬이 0이 되면 모두에게 BIG (야광귀 콤보 성공과 같은 보상 시간·배율).
    /// - 시간이 다 되면 남은 여우구슬이 연기로 사라지고 모두에게 SMALL (야광귀 콤보 실패와 같은 벌칙 시간·배율).
    /// - 피버가 시작되면 판정은 보상·벌칙 없이 취소된다(여우구슬은 피버 정리로 사라진다).
    /// - 판정이 진행 중인 동안에는 매혹을 걸지 않는다(DEV로 고유 기믹 간격을 판정 시간보다 짧게 줄였을 때의 안전장치).
    /// 지우는 방법은 그대로다: 빠르게 두 번 누르기, 음·양 구슬에 닿으면 함께 사라짐.
    /// FoxRoundSeconds가 0이면 예전처럼 여우구슬은 하나하나 구슬 수명만큼 있다가 사라지고 판정은 없다.
    /// </summary>
    public sealed partial class LabModel
    {
        private double foxRoundStartedAt = -1d, foxRoundEndsAt = -1d;
        private int foxRoundTotal, foxRoundResult, foxRoundCount;
        private double foxRoundResultAt = -1d;
        private bool foxRoundRunning;

        /// <summary>The fox round rules are on: a fox orb drop starts a timed team round.</summary>
        private bool FoxRoundRules => rules != null && rules.FoxRoundSeconds > 0f
            && Array.IndexOf(rules.UniqueGimmicks, LabSabotageKind.FoxFire) >= 0;

        /// <summary>A fox round is running at this Host time: dropped, not yet cleared, timed out or cancelled.</summary>
        public bool FoxRoundOn(double now) => phase == LabPhase.Playing && foxRoundEndsAt >= 0d && now >= foxRoundStartedAt
            && now < foxRoundEndsAt && !FoxRoundSettled;

        /// <summary>
        /// The latest round is over (or there is none). The last result stays as it was until the next one is settled, so the
        /// last BIG/SMALL keeps its badge while a new round runs (only with a short DEV interval). A drop at the very moment
        /// the last round is settled starts a new one.
        /// </summary>
        private bool FoxRoundSettled => !foxRoundRunning || foxRoundEndsAt < 0d;

        /// <summary>Fox orbs on every board now (the team's count).</summary>
        public int FoxOrbsLeft() => orbs.Values.Count(orb => orb.kind == LabOrbKind.FoxFire);

        private void ResetFoxRound()
        {
            foxRoundStartedAt = foxRoundEndsAt = foxRoundResultAt = -1d;
            foxRoundTotal = foxRoundResult = foxRoundCount = 0;
            foxRoundRunning = false;
        }

        /// <summary>The fox orbs just dropped start a round, or join the one already running (only with a short DEV interval).</summary>
        private void StartFoxRound(double at)
        {
            if (!FoxRoundRules) return;
            if (!FoxRoundOn(at))
            {
                foxRoundStartedAt = at;
                foxRoundEndsAt = at + rules.FoxRoundSeconds;
                foxRoundRunning = true;
            }
            // Every fox orb stays until the round ends; the team has to clear them, nothing fades by itself.
            foreach (LabOrbState fox in orbs.Values.Where(orb => orb.kind == LabOrbKind.FoxFire))
            {
                fox.expiresAt = foxRoundEndsAt;
                fox.revision = ++revision;
            }
            foxRoundTotal = FoxOrbsLeft();
            if (foxRoundTotal == 0) SettleFoxRound(at, true);
        }

        /// <summary>A fox orb was just cleared: the last one wins the round now.</summary>
        private void SettleFoxRoundIfCleared(double now)
        {
            if (FoxRoundOn(now) && FoxOrbsLeft() == 0) SettleFoxRound(now, true);
        }

        /// <summary>At the round's end, whatever is left goes in smoke and the round is lost.</summary>
        private void AdvanceFoxRound(double now)
        {
            if (FoxRoundSettled || phase != LabPhase.Playing) return;
            if (FoxOrbsLeft() == 0) { SettleFoxRound(Math.Min(now, foxRoundEndsAt), true); return; }
            if (now < foxRoundEndsAt) return;
            foreach (LabOrbState fox in orbs.Values.Where(orb => orb.kind == LabOrbKind.FoxFire).ToArray())
            {
                // Like an orb at the end of its life (#235): smoke on its holder's screen.
                orbs.Remove(fox.id);
                orbExpiries.Add(new LabOrbExpiry(fox.owner, fox.id));
            }
            SettleFoxRound(foxRoundEndsAt, false);
        }

        /// <summary>
        /// The team's result: cleared, everyone BIG for the colour combo's reward time; timed out, everyone SMALL for its
        /// penalty time (the same values as the 야광귀 combo). A running reward ends with a penalty and the other way round.
        /// </summary>
        private void SettleFoxRound(double at, bool cleared)
        {
            foxRoundRunning = false;
            foxRoundResult = cleared ? 1 : -1;
            foxRoundResultAt = at;
            foxRoundCount++;
            if (cleared)
            {
                if (rules.ComboRewardDurationSeconds <= 0f) return;
                double until = at + rules.ComboRewardDurationSeconds;
                foreach (LabPlayerState player in players.Values)
                {
                    player.comboRewardUntil = Math.Max(player.comboRewardUntil, until);
                    player.comboPenaltyUntil = Math.Min(player.comboPenaltyUntil, at);
                }
                return;
            }
            if (rules.ComboPenaltyDurationSeconds <= 0f) return;
            double penaltyUntil = at + rules.ComboPenaltyDurationSeconds;
            foreach (LabPlayerState player in players.Values)
            {
                player.comboPenaltyUntil = Math.Max(player.comboPenaltyUntil, penaltyUntil);
                player.comboRewardUntil = Math.Min(player.comboRewardUntil, at);
            }
        }

        /// <summary>The fever start (or anything else that clears the fox orbs) ends a running round with no result.</summary>
        private void CancelFoxRound()
        {
            if (FoxRoundSettled) return;
            foxRoundRunning = false;
            foxRoundEndsAt = -1d;
            foxRoundStartedAt = -1d;
            foxRoundTotal = 0;
        }
    }
}
