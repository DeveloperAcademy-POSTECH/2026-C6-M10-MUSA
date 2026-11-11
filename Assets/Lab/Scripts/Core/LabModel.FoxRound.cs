using System;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 여우구슬 팀 판정 (#664). 여우불이 떨어지면 그 순간부터 정해 둔 시간(Rules.FoxRoundSeconds, 기본 15초) 동안 팀이 목표치만큼
    /// 여우구슬을 지워야 한다. 여우구슬은 그동안 저절로 사라지지 않는다(사라지는 시각이 판정 끝으로 맞춰진다).
    /// - 판 하나에 떨어지는 수 N은 여우불마다 범위가 커진다: k번째(0부터) 여우불은 (최소 + k × 최소 증가) ~ (최대 + k × 최대 증가),
    ///   기본 2~5, 3~7, 4~9, 5~11. 모든 판이 같은 N을 받고, 판 빈자리만큼만 떨어진다. 이 단계는 판(라운드)마다 처음부터 센다.
    /// - 목표치 = 이번에 실제로 떨어진 팀 전체 수 − x (x는 Rules.FoxRoundSpareMin~Max, 기본 0~3에서 뽑음). 최소 1개.
    /// - 시간 안에 팀이 목표치만큼 지우면 모두에게 BIG (야광귀 콤보 성공과 같은 보상 시간·배율). 남은 여우구슬은 바로 연기로 사라진다.
    /// - 시간이 다 되면 남은 여우구슬이 연기로 사라지고 모두에게 SMALL (야광귀 콤보 실패와 같은 벌칙 시간·배율).
    /// - 피버가 시작되면 판정은 보상·벌칙 없이 취소된다(여우구슬은 피버 정리로 사라진다).
    /// - 판정이 진행 중인 동안에는 매혹을 걸지 않는다(DEV로 고유 기믹 간격을 판정 시간보다 짧게 줄였을 때의 안전장치).
    ///   그때 떨어진 여우불은 진행 중인 판정에 더해진다(떨어진 수와 목표치가 함께 는다).
    /// 지우는 방법은 그대로다: 빠르게 두 번 누르기, 음·양 구슬에 닿으면 함께 사라짐. 지운 수는 이 두 가지만 센다.
    /// FoxRoundSeconds가 0이면 예전처럼 여우구슬은 하나하나 구슬 수명만큼 있다가 사라지고 판정은 없다(개수 증가는 그대로).
    /// </summary>
    public sealed partial class LabModel
    {
        private const uint FoxSpareSalt = 0x5A4EF0C5u;

        private double foxRoundStartedAt = -1d, foxRoundEndsAt = -1d;
        private int foxRoundTotal, foxRoundResult, foxRoundCount;
        private int foxRoundGoal, foxRoundCleared;
        private double foxRoundResultAt = -1d;
        private bool foxRoundRunning;
        // 이번 판(라운드)에 떨어진 여우불 수: 다음 여우불의 개수 범위를 정한다.
        private int foxFireDrops;

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

        /// <summary>
        /// How many fox orbs each board gets from the drop-th fox fire of this round (0 = the first): the DEV minimum and
        /// maximum, each raised by its own step per fox fire before it (2~5, 3~7, 4~9, 5~11 by default).
        /// </summary>
        public (int min, int max) FoxFireCountRange(int drop)
        {
            int step = Math.Max(0, drop);
            int min = rules.FoxFireCountMin + step * rules.FoxFireCountStepMin;
            int max = Math.Max(min, rules.FoxFireCountMax + step * rules.FoxFireCountStepMax);
            return (min, max);
        }

        private void ResetFoxRound()
        {
            foxRoundStartedAt = foxRoundEndsAt = foxRoundResultAt = -1d;
            foxRoundTotal = foxRoundResult = foxRoundCount = foxRoundGoal = foxRoundCleared = foxFireDrops = 0;
            foxRoundRunning = false;
        }

        /// <summary>
        /// The fox orbs just dropped (dropped of them on every board together) start a round, or join the one already
        /// running (only with a short DEV interval). The goal grows by what fell less a few the team may leave (x, at least 1).
        /// </summary>
        private void StartFoxRound(double at, int dropped, uint index)
        {
            if (!FoxRoundRules || dropped <= 0) return;
            if (!FoxRoundOn(at))
            {
                foxRoundStartedAt = at;
                foxRoundEndsAt = at + rules.FoxRoundSeconds;
                foxRoundRunning = true;
                foxRoundTotal = foxRoundGoal = foxRoundCleared = 0;
            }
            // Every fox orb stays until the round ends; the team has to clear them, nothing fades by itself.
            foreach (LabOrbState fox in orbs.Values.Where(orb => orb.kind == LabOrbKind.FoxFire))
            {
                fox.expiresAt = foxRoundEndsAt;
                fox.revision = ++revision;
            }
            int spanSpare = rules.FoxRoundSpareMax - rules.FoxRoundSpareMin + 1;
            int spare = rules.FoxRoundSpareMin + (int)(DrawFor(seed ^ FoxSpareSalt, SabotageDrawKey, index) % (ulong)Math.Max(1, spanSpare));
            foxRoundTotal += dropped;
            foxRoundGoal += Math.Max(1, dropped - spare);
        }

        /// <summary>A player just cleared a fox orb (a double tap, or yin/yang touching it): reaching the goal wins the round now.</summary>
        private void CountFoxCleared(double now)
        {
            if (!FoxRoundOn(now)) return;
            foxRoundCleared++;
            if (foxRoundCleared >= foxRoundGoal) SettleFoxRound(now, true);
        }

        /// <summary>At the round's end, whatever is left goes in smoke and the round is lost.</summary>
        private void AdvanceFoxRound(double now)
        {
            if (FoxRoundSettled || phase != LabPhase.Playing) return;
            if (foxRoundCleared >= foxRoundGoal) { SettleFoxRound(Math.Min(now, foxRoundEndsAt), true); return; }
            if (now < foxRoundEndsAt) return;
            SettleFoxRound(foxRoundEndsAt, false);
        }

        /// <summary>The fox orbs left on every board go in smoke on their holder's screen, like an orb at the end of its life (#235).</summary>
        private void SmokeFoxOrbs()
        {
            foreach (LabOrbState fox in orbs.Values.Where(orb => orb.kind == LabOrbKind.FoxFire).ToArray())
            {
                orbs.Remove(fox.id);
                orbExpiries.Add(new LabOrbExpiry(fox.owner, fox.id));
            }
        }

        /// <summary>
        /// The team's result: the goal reached, everyone BIG for the colour combo's reward time; timed out, everyone SMALL for
        /// its penalty time (the same values as the 야광귀 combo). Either way the fox orbs left go in smoke. A running reward
        /// ends with a penalty and the other way round.
        /// </summary>
        private void SettleFoxRound(double at, bool cleared)
        {
            SmokeFoxOrbs();
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
            foxRoundTotal = foxRoundGoal = foxRoundCleared = 0;
        }
    }
}
