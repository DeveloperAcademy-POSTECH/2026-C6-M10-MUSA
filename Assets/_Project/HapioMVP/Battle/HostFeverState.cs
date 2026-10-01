using System;
using System.Collections.Generic;

namespace C6.Prototype.Battle
{
    public enum FeverUpdateResult
    {
        None = 0,
        Charged = 1,
        Started = 2,
        Ended = 3
    }

    /// <summary>
    /// Host-only team fever clock. The battle service supplies only collisions already accepted by
    /// AttackAuthority; the per-round hit ID set prevents a replay from charging the gauge twice.
    /// </summary>
    public sealed class HostFeverState
    {
        public const int MaximumGaugePercent = 100;
        public const int DefaultChargePerHit = 20;
        public const double DefaultDurationSeconds = 10;

        private readonly HashSet<string> chargedHitIds = new HashSet<string>(StringComparer.Ordinal);

        public int ChargePerHit { get; }
        public double DurationSeconds { get; }
        public int GaugePercent { get; private set; }
        public int Sequence { get; private set; }
        public bool Active { get; private set; }
        public double StartsAt { get; private set; }
        public double EndsAt { get; private set; }

        public HostFeverState(int chargePerHit = DefaultChargePerHit,
            double durationSeconds = DefaultDurationSeconds)
        {
            if (chargePerHit < 1 || chargePerHit > MaximumGaugePercent)
                throw new ArgumentOutOfRangeException(nameof(chargePerHit));
            if (!Finite(durationSeconds) || durationSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            ChargePerHit = chargePerHit;
            DurationSeconds = durationSeconds;
        }

        public void BeginRound()
        {
            chargedHitIds.Clear();
            GaugePercent = 0;
            Sequence = 0;
            Active = false;
            StartsAt = EndsAt = 0;
        }

        public FeverUpdateResult RecordValidHit(string hitId, double now)
        {
            if (string.IsNullOrWhiteSpace(hitId) || !Finite(now) || now < 0 || Active
                || !chargedHitIds.Add(hitId)) return FeverUpdateResult.None;

            GaugePercent = Math.Min(MaximumGaugePercent, GaugePercent + ChargePerHit);
            if (GaugePercent < MaximumGaugePercent) return FeverUpdateResult.Charged;

            Sequence++;
            Active = true;
            StartsAt = now;
            EndsAt = now + DurationSeconds;
            return FeverUpdateResult.Started;
        }

        public FeverUpdateResult Tick(double now)
        {
            if (!Active || !Finite(now) || now < EndsAt) return FeverUpdateResult.None;
            Active = false;
            GaugePercent = 0;
            StartsAt = EndsAt = 0;
            return FeverUpdateResult.Ended;
        }

        /// <summary>Round result clears live gameplay state while retaining the monotonic round sequence.</summary>
        public void EndRound()
        {
            Active = false;
            GaugePercent = 0;
            StartsAt = EndsAt = 0;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
