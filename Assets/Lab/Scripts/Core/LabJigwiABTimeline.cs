using System;

namespace C6Lab
{
    /// <summary>Authoritative fixed Jigwi activation times, also read by local voice anticipation.
    /// Audio never decides when a Host mission starts.</summary>
    public static class LabJigwiABTimeline
    {
        public const int SlotCount = 6;
        public const double WarningSeconds = 2d; // #509: 2초 예고(지귀 도발 문구를 읽을 시간)
        /// <summary>#509: A2 moved 40 → 42 so it never starts with the common bomb at 40.</summary>
        private static readonly double[] ActivationOffsets = { 5d, 25d, 42d, 55d, 70d, 85d };
        /// <summary>#509: B lasts 15 s (was 10) so the picked players have time to sync three times.</summary>
        public const double GroupMissionSeconds = 15d;
        /// <summary>#509: every picked player must hit their own pillar within this many seconds of the first hit.</summary>
        public const double GroupSyncWindowSeconds = 1d;
        /// <summary>#509: synchronized clears needed for success.</summary>
        public const int GroupSyncRequired = 3;

        public static double ActiveOffset(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return ActivationOffsets[slot];
        }

        /// <summary>#509: how many seats B picks — 1 for one or two players, 2 for three, 3 for four or five.</summary>
        public static int GroupPickCount(int seats) => seats <= 0 ? 0 : seats <= 2 ? 1 : seats == 3 ? 2 : 3;

        public static LabJigwiABKind KindForSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return slot % 2 == 0 ? LabJigwiABKind.PersonalBurn : LabJigwiABKind.GroupFire;
        }
    }
}
