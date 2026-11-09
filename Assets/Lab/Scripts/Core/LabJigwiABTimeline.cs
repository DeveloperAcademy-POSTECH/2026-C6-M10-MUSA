using System;

namespace C6Lab
{
    /// <summary>Authoritative fixed Jigwi activation times, also read by local voice anticipation.
    /// Audio never decides when a Host mission starts.</summary>
    public static class LabJigwiABTimeline
    {
        /// <summary>The most slots any schedule has (the wire limit).</summary>
        public const int SlotCount = 6;
        /// <summary>#509 schedule: A 5 / B 25 / A 42 / B 55 / A 70 / B 85 (B3 cut at 100).</summary>
        public const int LegacySchedule = 0;
        /// <summary>
        /// #540 schedule, like Yaguang: the first 40 s carry one unique gimmick (A1 10 s), the middle 60 s overlay
        /// B-A-B on the common gimmicks (B1 45, A2 66, B2 83), the last 20 s are fever.
        /// </summary>
        public const int FlowSchedule = 1;
        /// <summary>#551 시험용: 지귀 B만 4초부터 17초마다 (4 / 21 / 38 / 55 / 72), A와 공통 기믹 없음.</summary>
        public const int BTestSchedule = 2;
        private static readonly double[] BTestOffsets = { 4d, 21d, 38d, 55d, 72d };
        public const double WarningSeconds = 2d; // #509: 2초 예고(지귀 도발 문구를 읽을 시간)
        /// <summary>#509: A2 moved 40 → 42 so it never starts with the common bomb at 40.</summary>
        private static readonly double[] ActivationOffsets = { 5d, 25d, 42d, 55d, 70d, 85d };
        private static readonly double[] FlowOffsets = { 10d, 45d, 66d, 83d };
        private static double[] Offsets(int schedule) => schedule == FlowSchedule ? FlowOffsets
            : schedule == BTestSchedule ? BTestOffsets : ActivationOffsets;
        public static bool ValidSchedule(int schedule) => schedule == LegacySchedule || schedule == FlowSchedule || schedule == BTestSchedule;
        /// <summary>#551: the kind of a slot in this schedule (the B test schedule is all B).</summary>
        public static LabJigwiABKind KindForSlot(int slot, int schedule) => schedule == BTestSchedule
            ? (slot >= 0 && slot < SlotCount ? LabJigwiABKind.GroupFire : throw new ArgumentOutOfRangeException(nameof(slot)))
            : KindForSlot(slot);
        public static int SlotCountFor(int schedule) => Offsets(schedule).Length;
        /// <summary>The next slot's activation, or +∞ after the last one (an A never runs into the next mission).</summary>
        public static double NextOffset(int slot, int schedule) => slot + 1 < SlotCountFor(schedule)
            ? ActiveOffset(slot + 1, schedule) : double.PositiveInfinity;
        /// <summary>#509: B lasts 15 s (was 10).</summary>
        public const double GroupMissionSeconds = 15d;
        /// <summary>#509: every picked player's special Water must hit the boss within this many seconds of the first hit.</summary>
        public const double GroupSyncWindowSeconds = 1d;
        /// <summary>#551 B countdown: 3-2-1 beats from the moment every picked seat holds its special Water to "지금!".</summary>
        public const double CountdownSeconds = 1.8d;
        /// <summary>#551: after "던져!" every picked seat must throw within this long, or B fails.</summary>
        public const double ReleaseSeconds = 1d;
        /// <summary>#551: with the countdown, every picked hit must land within this long of the first.</summary>
        public const float CountdownSyncWindowSeconds = 1.5f;
        /// <summary>
        /// #551 나무 물통: the first bucket arrives this long after B starts, so Jigwi's taunt (from the 2 s warning) is heard and the
        /// boss has heated up (threat lead) before any bucket shows. Lead + stagger × 4 + seat time stays inside the 15 s B.
        /// </summary>
        public const double BucketLeadSeconds = 3.5d;
        /// <summary>#551 나무 물통 (개별, 여우구슬처럼): each seat's bucket arrives this long after the previous seat's (random order).</summary>
        public const double BucketStaggerSeconds = 1d;
        /// <summary>#551 나무 물통: each seat has this long from its bucket's arrival to pour it; after that the screen smokes over.</summary>
        public const double BucketSeatSeconds = 7d;
        /// <summary>#551: 쏟은 물은 위에서 떨어지니 박 윗부분(그 사람 쪽으로 기운 위쪽)에 칠한다. 적도 0°, 꼭대기 90°.</summary>
        public const float BucketPourPitch = 58f;
        /// <summary>#509: one synchronized hit by every picked seat is the whole B (one chance; a miss fails it).</summary>
        public const int GroupSyncRequired = 1;

        public static double ActiveOffset(int slot) => ActiveOffset(slot, LegacySchedule);

        public static double ActiveOffset(int slot, int schedule)
        {
            double[] offsets = Offsets(schedule);
            if (slot < 0 || slot >= offsets.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            return offsets[slot];
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
