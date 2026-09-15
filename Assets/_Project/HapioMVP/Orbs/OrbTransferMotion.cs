using System;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    /// <summary>
    /// Linear handoff velocity in board widths per second on both axes, sampled on NGO server time.
    /// This is a handoff receipt, not a continuously synchronized position or an extra physics step.
    /// </summary>
    public readonly struct OrbTransferMotion : IEquatable<OrbTransferMotion>
    {
        // DEMO_TUNING_VALUE: bounds protect the transfer request envelope without adding energy.
        public const double MaximumSampleAge = 8d;
        public const double FutureClockTolerance = .25d;
        public const float MaximumReleaseSpeedMultiplier = 16f;
        public Vector2 Velocity { get; }
        public double ServerTime { get; }

        public OrbTransferMotion(Vector2 velocity, double serverTime)
        { Velocity = velocity; ServerTime = serverTime; }

        public static bool IsValid(OrbTransferMotion sample, float maximumSpeed) => Finite(sample.Velocity.x)
            && Finite(sample.Velocity.y) && Finite(sample.ServerTime) && sample.ServerTime >= 0d
            && Finite(maximumSpeed) && maximumSpeed > 0f
            && (double)sample.Velocity.x * sample.Velocity.x + (double)sample.Velocity.y * sample.Velocity.y
                <= (double)maximumSpeed * maximumSpeed;

        public static bool IsFresh(OrbTransferMotion sample, double now) => Finite(now) && now >= 0d
            && Finite(sample.ServerTime) && sample.ServerTime >= 0d
            && sample.ServerTime <= now + FutureClockTolerance && now - sample.ServerTime <= MaximumSampleAge;

        /// <summary>Advance only friction. Delay can reduce speed or stop it; it cannot reverse or increase it.</summary>
        public static OrbTransferMotion Decay(OrbTransferMotion sample, double now, float deceleration, float stopSpeed)
        {
            if (!Finite(sample.Velocity.x) || !Finite(sample.Velocity.y) || !Finite(sample.ServerTime)
                || sample.ServerTime < 0d || !Finite(now) || now < 0d || !Finite(deceleration)
                || deceleration < 0f || !Finite(stopSpeed) || stopSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(sample), "Finite nonnegative motion timing and friction are required.");
            double speed = Math.Sqrt((double)sample.Velocity.x * sample.Velocity.x
                + (double)sample.Velocity.y * sample.Velocity.y);
            double remaining = Math.Max(0d, speed - deceleration * Math.Max(0d, now - sample.ServerTime));
            Vector2 velocity = speed == 0d || remaining <= stopSpeed ? Vector2.zero : sample.Velocity * (float)(remaining / speed);
            return new OrbTransferMotion(velocity, now);
        }

        // Equality includes rejected NaN samples so retry/query identity cannot turn a rejection into a new request.
        public bool Equals(OrbTransferMotion other) => Velocity.x.Equals(other.Velocity.x)
            && Velocity.y.Equals(other.Velocity.y) && ServerTime.Equals(other.ServerTime);
        public override bool Equals(object obj) => obj is OrbTransferMotion other && Equals(other);
        public override int GetHashCode()
        { unchecked { return (Velocity.x.GetHashCode() * 397 ^ Velocity.y.GetHashCode()) * 397 ^ ServerTime.GetHashCode(); } }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
