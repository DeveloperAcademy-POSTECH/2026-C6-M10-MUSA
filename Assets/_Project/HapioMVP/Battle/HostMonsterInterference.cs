using System;
using System.Collections.Generic;

namespace C6.Prototype.Battle
{
    /// <summary>Stable classification used by game logic, synchronization, UI, and logging.</summary>
    public enum MonsterInterferenceKind
    {
        None = 0,
        SinglePlayerDirectionBlock = 1,
        AllPlayersDirectionRestriction = 2
    }

    public enum MonsterTransferDirection
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    public static class MonsterInterferenceTags
    {
        public const string None = "none";
        public const string SinglePlayerDirectionBlock = "single-player-direction-block";
        public const string AllPlayersDirectionRestriction = "all-players-direction-restriction";

        public static string FromKind(MonsterInterferenceKind kind)
        {
            switch (kind)
            {
                case MonsterInterferenceKind.None:
                    return None;
                case MonsterInterferenceKind.SinglePlayerDirectionBlock:
                    return SinglePlayerDirectionBlock;
                case MonsterInterferenceKind.AllPlayersDirectionRestriction:
                    return AllPlayersDirectionRestriction;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown monster interference kind.");
            }
        }
    }

    /// <summary>
    /// Host-only schedule for reusable monster interference effects.
    /// The first effect begins 30 seconds after battle start, lasts 8 seconds, and subsequent effects
    /// begin every 30 seconds. A delayed Host tick never emits missed effects in a burst.
    /// </summary>
    public sealed class HostMonsterInterference
    {
        public const double DefaultIntervalSeconds = 30;
        public const double DefaultDurationSeconds = 8;
        public const string TransferBlockedReason = "MONSTER_INTERFERENCE_DIRECTION_BLOCKED";

        private readonly Random random;
        private double nextInterferenceAt;
        private MonsterInterferenceKind previousKind;

        public double IntervalSeconds { get; }
        public double DurationSeconds { get; }
        public int Sequence { get; private set; }
        public MonsterInterferenceKind Kind { get; private set; }
        public string Tag => MonsterInterferenceTags.FromKind(Kind);
        public bool HasTarget { get; private set; }
        public ulong Target { get; private set; }

        /// <summary>
        /// For SinglePlayerDirectionBlock this is the blocked direction.
        /// For AllPlayersDirectionRestriction this is the only allowed direction.
        /// </summary>
        public MonsterTransferDirection Direction { get; private set; }

        public double StartsAt { get; private set; }
        public double EndsAt { get; private set; }
        public bool Active { get; private set; }

        public HostMonsterInterference(double intervalSeconds, double durationSeconds, int seed)
        {
            if (!Positive(intervalSeconds) || !Positive(durationSeconds) || durationSeconds > intervalSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Finite positive interference timing is required, and duration cannot exceed interval.");
            }

            IntervalSeconds = intervalSeconds;
            DurationSeconds = durationSeconds;
            random = new Random(seed);
        }

        /// <summary>Call when a round starts Playing. Clears the previous round's effect.</summary>
        public void Begin(double startedAt)
        {
            Sequence = 0;
            previousKind = MonsterInterferenceKind.None;
            ClearActiveEffect();
            nextInterferenceAt = startedAt + IntervalSeconds;
        }

        /// <summary>Advances with Host time. Returns true when an effect starts or ends.</summary>
        public bool Tick(double now, IReadOnlyList<ulong> participants)
        {
            bool changed = false;
            if (Active && now >= EndsAt)
            {
                ClearActiveEffect();
                changed = true;
            }

            if (!Active && now >= nextInterferenceAt && participants != null && participants.Count > 0)
            {
                StartRandomEffect(now, participants);
                changed = true;

                nextInterferenceAt += IntervalSeconds;
                if (nextInterferenceAt <= now)
                    nextInterferenceAt = now + IntervalSeconds;
            }

            return changed;
        }

        /// <summary>
        /// Host transfer policy for the current effect. A single-player effect blocks only its target's
        /// matching direction. Other effect kinds are handled by their own policy branch.
        /// </summary>
        public bool BlocksTransfer(ulong sender, MonsterTransferDirection requestedDirection)
        {
            if (!Active || requestedDirection == MonsterTransferDirection.None)
                return false;

            if (Kind == MonsterInterferenceKind.SinglePlayerDirectionBlock)
            {
                return HasTarget && Target == sender && Direction == requestedDirection;
            }

            if (Kind == MonsterInterferenceKind.AllPlayersDirectionRestriction)
            {
                return requestedDirection != Direction;
            }

            return false;
        }

        private void StartRandomEffect(double now, IReadOnlyList<ulong> participants)
        {
            Sequence++;
            var rolledKind = random.Next(2) == 0
                ? MonsterInterferenceKind.SinglePlayerDirectionBlock
                : MonsterInterferenceKind.AllPlayersDirectionRestriction;
            // Keep the first effect random, then prevent an identical effect from repeating forever.
            // With two implemented effects this guarantees that both appear in every two activations.
            Kind = previousKind != MonsterInterferenceKind.None && rolledKind == previousKind
                ? OtherKind(previousKind)
                : rolledKind;
            previousKind = Kind;
            Direction = random.Next(2) == 0
                ? MonsterTransferDirection.Left
                : MonsterTransferDirection.Right;
            HasTarget = Kind == MonsterInterferenceKind.SinglePlayerDirectionBlock;
            Target = HasTarget ? participants[random.Next(participants.Count)] : 0;
            StartsAt = now;
            EndsAt = now + DurationSeconds;
            Active = true;
        }

        private static MonsterInterferenceKind OtherKind(MonsterInterferenceKind kind) =>
            kind == MonsterInterferenceKind.SinglePlayerDirectionBlock
                ? MonsterInterferenceKind.AllPlayersDirectionRestriction
                : MonsterInterferenceKind.SinglePlayerDirectionBlock;

        private void ClearActiveEffect()
        {
            Kind = MonsterInterferenceKind.None;
            HasTarget = false;
            Target = 0;
            Direction = MonsterTransferDirection.None;
            StartsAt = 0;
            EndsAt = 0;
            Active = false;
        }

        private static bool Positive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
