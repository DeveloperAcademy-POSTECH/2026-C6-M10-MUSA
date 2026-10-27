using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        private const uint YaguangCommonSalt = 0x31400C0Du;
        private const ulong YaguangDrawKey = ulong.MaxValue - 7UL;

        private readonly struct YaguangUniqueSlot
        {
            public readonly double Seconds;
            public readonly bool IsCombo;
            public YaguangUniqueSlot(double seconds, bool isCombo) { Seconds = seconds; IsCombo = isCombo; }
        }

        // This is schedule policy/state for CastGimmicks, not another timer/execution loop.
        private double yaguangTimelineStartAt, yaguangTimelineEndAt;
        private bool yaguangTimelineEnded, yaguangBombFirst;
        private readonly Queue<YaguangUniqueSlot> yaguangUniquePending = new Queue<YaguangUniqueSlot>();

        private void ResetYaguangTimeline(double now)
        {
            yaguangTimelineStartAt = now;
            yaguangTimelineEndAt = rules.YaguangTimeline && rules.YaguangTimelineEndSeconds > 0d
                ? now + rules.YaguangTimelineEndSeconds : double.PositiveInfinity;
            yaguangTimelineEnded = false;
            yaguangUniquePending.Clear();
            yaguangBombFirst = (DrawFor(seed ^ YaguangCommonSalt, YaguangDrawKey, 0u) & 1UL) == 0UL;
            if (!rules.YaguangTimeline) return;
            nextCommonGimmickAt = NextYaguangCommonAt(0u);
            nextUniqueGimmickAt = NextYaguangUniqueAt(0u);
            // Effects accept occurrences from the group's schedule; their old repeat intervals are unused.
            nextComboAt = nextFlashAt = double.PositiveInfinity;
        }

        private double NextYaguangCommonAt(uint index) => index < rules.YaguangTimelineCommonSeconds.Length
            ? YaguangOccurrenceAt(rules.YaguangTimelineCommonSeconds[index]) : double.PositiveInfinity;

        private double NextYaguangUniqueAt(uint index) => index < rules.YaguangTimelineUniqueSchedule.Length
            ? YaguangOccurrenceAt(rules.YaguangTimelineUniqueSchedule[index].Seconds) : double.PositiveInfinity;

        private double YaguangOccurrenceAt(double seconds)
        {
            double at = yaguangTimelineStartAt + seconds;
            return at < yaguangTimelineEndAt ? at : double.PositiveInfinity;
        }

        /// <summary>Scheduled common kind in chronological slot order, after applying the per-kind switches.</summary>
        public bool TimelineCommonIsBomb(int slot)
        {
            if (rules == null || !rules.YaguangTimeline || slot < 0
                || slot >= rules.YaguangTimelineCommonSeconds.Length) return false;
            bool bomb = rules.GimmickEnabled[(int)LabSabotageKind.GourdBomb];
            bool block = rules.GimmickEnabled[(int)LabSabotageKind.SwipeBlock];
            return bomb && (!block || (slot % 2 == 0) == yaguangBombFirst);
        }

        private bool CastYaguangCommon(double at, uint index)
        {
            bool bomb = rules.GimmickEnabled[(int)LabSabotageKind.GourdBomb];
            bool block = rules.GimmickEnabled[(int)LabSabotageKind.SwipeBlock];
            if (!bomb && !block) return false;
            LabSabotageKind kind = TimelineCommonIsBomb((int)index)
                ? LabSabotageKind.GourdBomb : LabSabotageKind.SwipeBlock;
            // Keep #321's target exclusions and no consecutive target within the common group.
            // Yaguang's stage policy alone allows its common and unique groups to overlap.
            List<ulong> targets = GimmickTargets(kind, at, lastCommonTarget);
            if (targets.Count == 0) return false; // Consume this occurrence; never queue a blocked common cast.
            ulong target = targets[(int)(DrawFor(seed ^ SabotageTargetSalt, SabotageDrawKey, index) % (ulong)targets.Count)];
            lastCommonTarget = target;
            DispatchGimmick(kind, target, at, index);
            return true;
        }

        private bool YaguangUniqueBusy(double at)
        {
            if (combo.Active || comboWarned || flashWarned) return true;
            if (flashCount > 0 && at < lastFlashAt + rules.FlashGlareSeconds + rules.FlashRecoverySeconds) return true;
            return players.Values.Any(player => player.comboPenaltyUntil > at || player.comboRewardUntil > at);
        }

        private void StartWaitingYaguangUnique(double at)
        {
            while (!yaguangTimelineEnded && yaguangUniquePending.Count > 0 && !YaguangUniqueBusy(at))
            {
                YaguangUniqueSlot slot = yaguangUniquePending.Dequeue();
                // Admission begins the complete warning, even if the previous unique effect delayed it.
                // Common effects never delay a unique warning in this stage's timetable.
                if (slot.IsCombo)
                {
                    nextComboAt = at + rules.ComboWarningSeconds;
                    comboWarned = true;
                    comboHeld = comboWasHeld = false;
                    comboQueuedAt = -1d;
                }
                else
                {
                    nextFlashAt = at + rules.FlashWarningSeconds;
                    SettleFlashTarget(at);
                    flashWarned = true;
                    flashHeld = flashWasHeld = false;
                    flashQueuedAt = -1d;
                }
                RecordYaguangUnique(slot.IsCombo, at);
                // Zero-length flash warning/effects may admit another same-time slot without a frame delay.
                AdvanceCombo(at);
                AdvanceFlash(at);
            }
        }

        private void EndYaguangTimeline(double now)
        {
            if (rules == null || !rules.YaguangTimeline || yaguangTimelineEnded || now < yaguangTimelineEndAt) return;
            yaguangTimelineEnded = true;
            double at = yaguangTimelineEndAt;
            nextCommonGimmickAt = nextUniqueGimmickAt = nextComboAt = nextFlashAt = double.PositiveInfinity;
            yaguangUniquePending.Clear();
            comboWarned = flashWarned = comboHeld = comboWasHeld = flashHeld = flashWasHeld = false;
            comboQueuedAt = flashQueuedAt = -1d;
            combo.Stop(at);
            bool flashOn = rules.FlashEnabled && flashCount > 0
                && at < lastFlashAt + rules.FlashGlareLimitSeconds + rules.FlashRecoverySeconds;
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
