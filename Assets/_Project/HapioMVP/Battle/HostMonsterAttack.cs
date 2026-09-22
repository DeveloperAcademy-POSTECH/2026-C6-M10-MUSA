using System;
using System.Collections.Generic;

namespace C6.Prototype.Battle
{
    public enum MonsterAttackResult { None, Defended, Hit }

    /// <summary>
    /// #28 Host-only schedule for the monster's single-target attack. Pure logic: the battle session feeds
    /// Host time and the frozen roster, publishes this state, and applies the time penalty on a Hit.
    /// The first attack starts at start + firstDelay, then every interval (start to start). Each attack
    /// warns its target for warningSeconds; the result is decided after a short report grace.
    /// </summary>
    public sealed class HostMonsterAttack
    {
        /// <summary>Network allowance for a defense report sent just before the warning ended.</summary>
        public const double ReportGraceSeconds = .25; // DEMO_TUNING_VALUE
        private readonly Random random;
        private double nextAttackAt;

        public double FirstDelaySeconds { get; }
        public double IntervalSeconds { get; }
        public double WarningSeconds { get; }
        public int Sequence { get; private set; }
        public ulong Target { get; private set; }
        public double WarningStartsAt { get; private set; }
        public double WarningEndsAt { get; private set; }
        public bool Active { get; private set; }
        public bool Defended { get; private set; }
        public int ResolvedSequence { get; private set; }
        public MonsterAttackResult LastResult { get; private set; }

        public HostMonsterAttack(double firstDelaySeconds, double intervalSeconds, double warningSeconds, int seed)
        {
            if (!Positive(firstDelaySeconds) || !Positive(intervalSeconds) || !Positive(warningSeconds))
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Finite positive attack timing is required.");
            FirstDelaySeconds = firstDelaySeconds; IntervalSeconds = intervalSeconds; WarningSeconds = warningSeconds;
            random = new Random(seed);
        }

        /// <summary>Call when a round starts Playing. Clears every previous attack.</summary>
        public void Begin(double startedAt)
        {
            Sequence = 0; Target = 0; WarningStartsAt = WarningEndsAt = 0;
            Active = Defended = false; ResolvedSequence = 0; LastResult = MonsterAttackResult.None;
            nextAttackAt = startedAt + FirstDelaySeconds;
        }

        /// <summary>Advances with Host time. Returns the result decided during this call, or None.</summary>
        public MonsterAttackResult Tick(double now, IReadOnlyList<ulong> participants)
        {
            var decided = MonsterAttackResult.None;
            if (Active && now >= WarningEndsAt + ReportGraceSeconds)
            {
                decided = LastResult = Defended ? MonsterAttackResult.Defended : MonsterAttackResult.Hit;
                ResolvedSequence = Sequence; Active = false;
            }
            if (!Active && now >= nextAttackAt && participants != null && participants.Count > 0)
            {
                Sequence++;
                Target = participants[random.Next(participants.Count)];
                WarningStartsAt = now; WarningEndsAt = now + WarningSeconds;
                Defended = false; Active = true;
                nextAttackAt += IntervalSeconds;
                if (nextAttackAt <= now) nextAttackAt = now + IntervalSeconds; // no burst after a long Host stall
            }
            return decided;
        }

        /// <summary>Host judgment of a defense report: only the current target, for the current attack, in time.</summary>
        public bool AcceptDefense(ulong sender, int sequence, double now)
        {
            if (!Active || sequence != Sequence || sender != Target || now > WarningEndsAt + ReportGraceSeconds) return false;
            Defended = true;
            return true;
        }

        private static bool Positive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}