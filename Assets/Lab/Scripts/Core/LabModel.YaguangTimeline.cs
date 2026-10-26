using System;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 시간표 (#314). 켜 두면 방해가 간격·랜덤 주기 대신 DEV에 적어 둔 시각에만 나온다.
    /// - 고유(섬광·콤보): 칸의 시각에 예고가 시작된다. 섬광은 예고 뒤 번쩍이고, 콤보는 예고 뒤 4칸이 나온다.
    /// - 공용(박 폭탄·스와이프 금지): 칸의 시각에 둘 중 하나가 나온다. 첫 칸만 seed로 고르고, 같은 것이 연달아
    ///   나오지 않으므로 그 뒤는 번갈아 나온다.
    /// - 방해 끝 시각이 되면 남은 방해 효과(SMALL/BIG·섬광·스와이프 금지·박 폭탄)를 모두 끝낸다. 그 뒤는 피버 자리다.
    /// 시각이 정해져 있으므로 화면별로 차례를 기다리는 규칙(#271)은 쓰지 않는다. 고유끼리 겹치지 않게 시각을 고르는 것은
    /// 시간표의 몫이고, 그래도 겹치면 섬광·콤보는 원래처럼 먼저 시작한 쪽이 끝나기를 기다린다.
    /// </summary>
    public sealed partial class LabModel
    {
        private const uint TimelineCommonSalt = 0x31400C0Du;
        private const ulong TimelineDrawKey = ulong.MaxValue - 7UL;

        private double timelineStartAt;
        private int timelineFlashIndex, timelineComboIndex, timelineBombIndex, timelineSwipeIndex;
        private double[] timelineBombSeconds = Array.Empty<double>(), timelineSwipeSeconds = Array.Empty<double>();
        private double timelineEndAt = double.PositiveInfinity;
        private bool timelineEnded;
        private bool timelineBombFirst;

        /// <summary>
        /// 이번 판 공용 칸 i(시각 순서)가 박 폭탄인지, 아니면 스와이프 금지인지. 시간표가 꺼져 있으면 언제나 false.
        /// </summary>
        public bool TimelineCommonIsBomb(int slot) => rules != null && rules.Timeline && slot >= 0
            && slot < rules.TimelineCommonSeconds.Length && (slot % 2 == 0) == timelineBombFirst;

        private void ResetTimeline(double now)
        {
            timelineStartAt = now;
            timelineFlashIndex = timelineComboIndex = timelineBombIndex = timelineSwipeIndex = 0;
            timelineEnded = false;
            timelineEndAt = rules.Timeline && rules.TimelineSabotageEndSeconds > 0d
                ? now + rules.TimelineSabotageEndSeconds : double.PositiveInfinity;
            if (!rules.Timeline)
            {
                timelineBombSeconds = timelineSwipeSeconds = Array.Empty<double>();
                return;
            }
            // 공용은 같은 것이 두 번 연달아 나오지 않는다. 종류가 둘뿐이라 첫 칸만 뽑고 그 뒤는 번갈아 간다.
            timelineBombFirst = (DrawFor(seed ^ TimelineCommonSalt, TimelineDrawKey, 0u) & 1UL) == 0UL;
            double[] commons = rules.TimelineCommonSeconds;
            timelineBombSeconds = commons.Where((_, i) => TimelineCommonIsBomb(i)).ToArray();
            timelineSwipeSeconds = commons.Where((_, i) => !TimelineCommonIsBomb(i)).ToArray();
        }

        private double TimelineAt(double[] seconds, ref int index, double offset = 0d)
        {
            while (index < seconds.Length)
            {
                double at = timelineStartAt + seconds[index++] + offset;
                if (at < timelineEndAt) return at;
            }
            return double.PositiveInfinity;
        }

        /// <summary>다음 섬광이 번쩍이는 시각: 칸의 시각(예고 시작) + 예고</summary>
        private double NextTimelineFlash() =>
            TimelineAt(rules.TimelineFlashSeconds, ref timelineFlashIndex, rules.FlashWarningSeconds);

        /// <summary>다음 콤보 4칸이 나오는 시각: 칸의 시각(예고 시작) + 예고</summary>
        private double NextTimelineCombo() => rules.ComboEnabled
            ? TimelineAt(rules.TimelineComboSeconds, ref timelineComboIndex, rules.ComboWarningSeconds)
            : double.PositiveInfinity;

        private double NextTimelineBomb() => TimelineAt(timelineBombSeconds, ref timelineBombIndex);

        private double NextTimelineSwipe() => TimelineAt(timelineSwipeSeconds, ref timelineSwipeIndex);

        /// <summary>
        /// 방해 끝 (#314): 이 시각이 지나면 새 방해를 내지 않고, 남은 효과를 그 시각에 끝낸다.
        /// 진행 중인 콤보는 결과 없이 멈추고(보상·페널티 없음), SMALL/BIG·스와이프 금지는 여기서 끝나며,
        /// 섬광에 눈부신 사람은 풀린 것으로 하고(#301 해제와 같은 표시), 판에 남은 박 폭탄은 조용히 사라진다.
        /// </summary>
        private void EndTimelineSabotage(double now)
        {
            if (rules == null || !rules.Timeline || timelineEnded || now < timelineEndAt) return;
            timelineEnded = true;
            double at = timelineEndAt;
            nextGourdBombAt = nextSwipeBlockAt = nextComboAt = nextFlashAt = double.PositiveInfinity;
            comboWarned = flashWarned = false;
            comboQueuedAt = flashQueuedAt = -1d;
            if (combo.Active) combo.Stop(at);
            bool flashOn = rules.FlashEnabled && flashCount > 0
                && at < lastFlashAt + rules.FlashGlareSeconds + rules.FlashRecoverySeconds;
            foreach (LabPlayerState player in players.Values)
            {
                player.comboPenaltyUntil = Math.Min(player.comboPenaltyUntil, at);
                player.comboRewardUntil = Math.Min(player.comboRewardUntil, at);
                if (flashOn && player.flashReleasedAt < lastFlashAt) player.flashReleasedAt = at;
            }
            if (BlockOn(at)) swipeBlockedUntil = at;
            foreach (LabOrbState bomb in orbs.Values
                .Where(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd && !orb.inFlight).ToArray())
            {
                orbs.Remove(bomb.id);
                orbExpiries.Add(new LabOrbExpiry(bomb.owner, bomb.id));
            }
        }
    }
}
