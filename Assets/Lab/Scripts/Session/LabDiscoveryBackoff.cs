using System;
namespace C6Lab
{
    /// <summary>What to do with a resolved room address whose local TTL has passed (#476).</summary>
    public enum LabAddressExpiry { Keep, Renew, Remove }

    /// <summary>
    /// Bounded automatic recovery for room discovery and the Host's advertisement (#476). Pure decisions only, so every
    /// retry waits at least a second and stops after a fixed number of attempts; user actions and app resume reset it.
    /// </summary>
    public static class LabDiscoveryBackoff
    {
        public const double InitialDelaySeconds = 1d;
        public const double MaximumDelaySeconds = 30d;
        /// <summary>Browse restarts and Host re-registrations: 1, 2, 4, 8, 16, 30 s, then stop.</summary>
        public const int MaximumAttempts = 6;
        /// <summary>Re-resolves of one room that failed while the browse is still alive.</summary>
        public const int ServiceRetryLimit = 3;
        /// <summary>A browse alive this long has recovered; its next failure starts again from the first delay.</summary>
        public const double StableSeconds = 30d;

        public static double Delay(int attempt) =>
            Math.Min(MaximumDelaySeconds, InitialDelaySeconds * Math.Pow(2d, Math.Max(0, Math.Min(attempt, 30))));

        public static bool CanRetry(int attempt, int limit) => attempt >= 0 && attempt < limit;

        /// <summary>
        /// mDNSResponder refreshes a cached address without a new callback, so the first local expiry asks again
        /// instead of dropping a live room; a record still unanswered at its renewal deadline is removed.
        /// </summary>
        public static LabAddressExpiry NextAddressState(double expiresAt, bool renewing, double now) =>
            now < expiresAt ? LabAddressExpiry.Keep : renewing ? LabAddressExpiry.Remove : LabAddressExpiry.Renew;
    }
}
