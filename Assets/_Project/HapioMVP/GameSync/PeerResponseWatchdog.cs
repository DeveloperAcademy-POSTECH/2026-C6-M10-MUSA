namespace C6.Prototype.GameSync
{
    /// <summary>Tracks receipt time after the caller has validated the peer and game context.</summary>
    public sealed class PeerResponseWatchdog
    {
        public double LastResponseAt { get; private set; }
        public bool IsArmed { get; private set; }

        public bool Begin(double now)
        {
            // A duplicate initialization must not extend an existing response deadline.
            if (IsArmed || !ValidTime(now)) return false;
            LastResponseAt = now;
            IsArmed = true;
            return true;
        }

        public bool Observe(double now)
        {
            if (!IsArmed || !ValidTime(now) || now < LastResponseAt) return false;
            LastResponseAt = now;
            return true;
        }

        public bool IsExpired(double now, double timeoutSeconds)
        {
            if (!IsArmed || !ValidTime(now) || now < LastResponseAt
                || !Finite(timeoutSeconds) || timeoutSeconds <= 0) return false;
            return now - LastResponseAt >= timeoutSeconds;
        }

        public void Reset()
        {
            IsArmed = false;
            LastResponseAt = 0;
        }

        private static bool ValidTime(double value) => Finite(value) && value >= 0;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
