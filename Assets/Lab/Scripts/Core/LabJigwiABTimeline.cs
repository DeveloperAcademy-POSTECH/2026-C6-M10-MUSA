using System;

namespace C6Lab
{
    /// <summary>Authoritative fixed Jigwi activation times, also read by local voice anticipation.
    /// Audio never decides when a Host mission starts.</summary>
    public static class LabJigwiABTimeline
    {
        public const int SlotCount = 6;
        public const double WarningSeconds = 1d;
        private static readonly double[] ActivationOffsets = { 5d, 25d, 40d, 55d, 70d, 85d };

        public static double ActiveOffset(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return ActivationOffsets[slot];
        }

        public static LabJigwiABKind KindForSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return slot % 2 == 0 ? LabJigwiABKind.PersonalBurn : LabJigwiABKind.GroupFire;
        }
    }
}
