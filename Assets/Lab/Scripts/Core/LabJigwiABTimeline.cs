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
        /// <summary>#509: B lasts 15 s (was 10).</summary>
        public const double GroupMissionSeconds = 15d;
        /// <summary>#509: every picked player's special Water must hit the boss within this many seconds of the first hit.</summary>
        public const double GroupSyncWindowSeconds = 1d;
        /// <summary>#509: one synchronized hit by every picked seat is the whole B (one chance; a miss fails it).</summary>
        public const int GroupSyncRequired = 1;

        public static double ActiveOffset(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return ActivationOffsets[slot];
        }

        /// <summary>#509: how many seats B picks — 1 solo (test), 2 for two or three players, 3 for four or five.</summary>
        public static int GroupPickCount(int seats) => seats <= 0 ? 0 : seats == 1 ? 1 : seats <= 3 ? 2 : 3;

        public static LabJigwiABKind KindForSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return slot % 2 == 0 ? LabJigwiABKind.PersonalBurn : LabJigwiABKind.GroupFire;
        }
    }
}
