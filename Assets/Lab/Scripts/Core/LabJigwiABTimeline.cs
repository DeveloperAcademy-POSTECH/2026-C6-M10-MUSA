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
        /// <summary>
        /// #595 연기 흐름: 연기(B) 10 / 부적(A, 공용) 45 / 연기 66 / 부적 83. 초반 40초는 연기 하나, 공통 기믹은 25초부터.
        /// 연기는 양동이 없이 모두의 화면을 같은 때에 덮고, 부적은 모두가 써야 보상.
        /// </summary>
        public const int SmokeFlowSchedule = 3;
        private static readonly double[] SmokeFlowOffsets = { 10d, 45d, 66d, 83d };
        /// <summary>#595 시험용: 부적(A)만 4초부터 17초마다 (4 / 21 / 38 / 55 / 72), 연기와 공통 기믹 없음.</summary>
        public const int ATestSchedule = 4;
        /// <summary>
        /// #696 촘촘한 연기 흐름: 연기 6 / 부적 30 / 연기 44 / 부적 68 / 연기 82. #595 연기 흐름과 같은 규칙에 미션 5개.
        /// 연기마다 다음 부적까지 24초라, 닦는 동안 드러난 검은 파편을 모아 검정 구슬로 던질 틈이 있다
        /// (부적이 끝나면 4초 안에 다음 연기, 마지막 검정 구슬은 피버 직전에 던진다).
        /// </summary>
        public const int DenseSmokeFlowSchedule = 5;
        private static readonly double[] DenseSmokeFlowOffsets = { 6d, 30d, 44d, 68d, 82d };
        /// <summary>
        /// #713 이어달리기 연기 흐름: 첫 연기만 6초에 정해진 시각에 오고, 그 뒤로는 앞 미션(벌칙/보상 연출까지)이 끝나고
        /// RelayGap초 뒤(지귀 예고 2초 포함)에 다음 미션이 온다. 연기 → 부적 → 연기 … 피버 전에 끝낼 수 있는 만큼 (최대 SlotCount개).
        /// </summary>
        public const int RelaySmokeFlowSchedule = 6;
        private static readonly double[] RelaySmokeFlowOffsets = { 6d, 6d, 6d, 6d, 6d, 6d }; // only slot 0 is a fixed time
        /// <summary>
        /// #737 부적 먼저 이어달리기: 첫 부적만 10초에 정해진 시각에 오고, 그 뒤로는 #713과 같이 앞 미션이 끝나고 간격 뒤에
        /// 다음 미션이 온다. 부적 → 연기 → 부적 … (연기는 양동이 없이 모두의 화면, 부적은 공용 — 연기 흐름과 같은 규칙)
        /// </summary>
        public const int RelayTalismanFirstSchedule = 7;
        private static readonly double[] RelayTalismanFirstOffsets = { 10d, 10d, 10d, 10d, 10d, 10d }; // only slot 0 is a fixed time
        public static bool IsRelay(int schedule) => schedule == RelaySmokeFlowSchedule || schedule == RelayTalismanFirstSchedule;
        /// <summary>#595/#696/#713/#737: 연기(양동이 없음)와 공용 부적이 번갈아 오는 연기 흐름인지 (4개, 촘촘한 5개, 이어달리기 두 가지).</summary>
        public static bool IsSmokeFlow(int schedule) => schedule == SmokeFlowSchedule || schedule == DenseSmokeFlowSchedule
            || schedule == RelaySmokeFlowSchedule || schedule == RelayTalismanFirstSchedule;
        /// <summary>#595: 연기 B가 시작되고(지귀 예고 2초 뒤) 박이 달아오르다 연기가 뿜어져 나오기까지(초). 모든 자리가 같다.</summary>
        public const double SmokeLeadSeconds = 1.5d;
        /// <summary>
        /// #610 공용 부적: 마지막 사람이 다 쓴 뒤 위의 작은 부적들이 빛나기 시작하기까지(초). 모든 기기의 화면과 Host의 폭포 칠이
        /// 이 시각을 함께 본다. #626 QA: 마지막 음이 다 울릴 때까지(4.5초) 기다리면 길어서, 마지막 "둥!"(도장, 약 2.2초) 직후로 당겼다.
        /// </summary>
        public const double CommonTalismanRevealSeconds = 2.5d;
        /// <summary>#610 공용 부적: 작은 부적이 빛나고(1초) 박으로 날아가(0.4초) 폭포가 박에 닿기까지(초).</summary>
        public const double CommonTalismanPourSeconds = 1.4d;
        public const double WarningSeconds = 2d; // #509: 2초 예고(지귀 도발 문구를 읽을 시간)
        /// <summary>#509: A2 moved 40 → 42 so it never starts with the common bomb at 40.</summary>
        private static readonly double[] ActivationOffsets = { 5d, 25d, 42d, 55d, 70d, 85d };
        private static readonly double[] FlowOffsets = { 10d, 45d, 66d, 83d };
        private static double[] Offsets(int schedule) => schedule == FlowSchedule ? FlowOffsets
            : schedule == BTestSchedule || schedule == ATestSchedule ? BTestOffsets
            : schedule == SmokeFlowSchedule ? SmokeFlowOffsets
            : schedule == DenseSmokeFlowSchedule ? DenseSmokeFlowOffsets
            : schedule == RelaySmokeFlowSchedule ? RelaySmokeFlowOffsets
            : schedule == RelayTalismanFirstSchedule ? RelayTalismanFirstOffsets : ActivationOffsets;
        public static bool ValidSchedule(int schedule) => schedule == LegacySchedule || schedule == FlowSchedule || schedule == BTestSchedule
            || schedule == SmokeFlowSchedule || schedule == ATestSchedule || schedule == DenseSmokeFlowSchedule
            || schedule == RelaySmokeFlowSchedule || schedule == RelayTalismanFirstSchedule;
        /// <summary>#551: the kind of a slot in this schedule (the B test schedule is all B).</summary>
        public static LabJigwiABKind KindForSlot(int slot, int schedule) => schedule == BTestSchedule
            ? (slot >= 0 && slot < SlotCount ? LabJigwiABKind.GroupFire : throw new ArgumentOutOfRangeException(nameof(slot)))
            // #595: 연기(B)가 먼저, 부적(A)이 다음.
            : schedule == ATestSchedule
                ? (slot >= 0 && slot < SlotCount ? LabJigwiABKind.PersonalBurn : throw new ArgumentOutOfRangeException(nameof(slot)))
            // #737: 부적 먼저 이어달리기는 부적(A)이 먼저, 연기(B)가 다음.
            : schedule == RelayTalismanFirstSchedule ? KindForSlot(slot)
            : IsSmokeFlow(schedule) ? (KindForSlot(slot) == LabJigwiABKind.PersonalBurn ? LabJigwiABKind.GroupFire : LabJigwiABKind.PersonalBurn)
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
