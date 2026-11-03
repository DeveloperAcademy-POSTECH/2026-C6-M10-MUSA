using System;

namespace C6Lab
{
    /// <summary>
    /// The Guest's battle clock for presentation (#451): the latest Host <c>serverTime</c> extrapolated by real time.
    /// The base is keyed by the sample's values (session, round, serverTime, frozen), not by which snapshot object
    /// carried them, so a repeated header does not restart the extrapolation. Within a round it never runs backwards
    /// by less than <see cref="RewindToleranceSeconds"/>: a sample that arrives a little behind the extrapolation holds
    /// the clock until real time catches up. A larger rewind, a new round or a new session is taken as a correction.
    /// Extrapolation stops after <see cref="MaximumExtrapolationSeconds"/> without a new sample, and a frozen sample
    /// (pause or start hold) holds the clock.
    /// </summary>
    public sealed class LabGuestBattleClock
    {
        public const double MaximumExtrapolationSeconds = 1d;
        public const double RewindToleranceSeconds = .5d;
        private string session;
        private uint round;
        private double baseServerTime, baseAt;
        private bool frozen, hasBase;
        private double last = double.NaN;

        /// <summary>
        /// True after the last sample reached <see cref="MaximumExtrapolationSeconds"/> without a new Host time while not
        /// frozen. The clock is then standing still on its own, and real-time media (the jigwi voice) should hold
        /// rather than seek back to it over and over.
        /// </summary>
        public bool Stalled { get; private set; }

        public double Sample(string sampleSession, uint sampleRound, double serverTime, bool sampleFrozen, double now)
        {
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime)) return double.IsNaN(last) ? 0d : last;
            bool newRound = !hasBase || session != sampleSession || round != sampleRound;
            if (newRound) last = double.NaN;
            if (newRound || serverTime != baseServerTime || sampleFrozen != frozen)
            {
                session = sampleSession; round = sampleRound;
                baseServerTime = serverTime; baseAt = now; frozen = sampleFrozen; hasBase = true;
            }
            double elapsed = frozen || double.IsNaN(now) || double.IsInfinity(now) ? 0d
                : Math.Min(MaximumExtrapolationSeconds, Math.Max(0d, now - baseAt));
            Stalled = !frozen && elapsed >= MaximumExtrapolationSeconds;
            double time = baseServerTime + elapsed;
            if (!double.IsNaN(last) && time < last && last - time < RewindToleranceSeconds) time = last;
            last = time;
            return time;
        }

        public void Reset()
        {
            session = null; round = 0; baseServerTime = baseAt = 0d; frozen = hasBase = false; last = double.NaN;
            Stalled = false;
        }
    }
}
