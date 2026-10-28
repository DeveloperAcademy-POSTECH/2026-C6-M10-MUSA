using System;
using System.Collections.Generic;

namespace Hapio
{
    /// <summary>A bounded presentation barrier. Victory and paint remain owned by LabModel.</summary>
    public sealed class GamePaintFinishBarrier
    {
        public const double LeadSeconds = .75d;
        public const double TimeoutSeconds = 12d;
        readonly HashSet<ulong> participants = new HashSet<ulong>();
        readonly HashSet<ulong> prepared = new HashSet<ulong>();
        readonly HashSet<ulong> scheduled = new HashSet<ulong>();
        readonly HashSet<ulong> completed = new HashSet<ulong>();
        readonly double beganAt;
        public uint Generation { get; private set; }
        public double StartsAt { get; private set; }
        public bool Committed { get; private set; }
        public bool Released { get; private set; }
        public bool Skipped { get; private set; }
        public bool EveryonePrepared => All(prepared);
        public bool EveryoneScheduled => All(scheduled);
        public bool EveryoneCompleted => All(completed);

        public GamePaintFinishBarrier(IEnumerable<ulong> playerIds, double realNow)
        {
            if (playerIds == null || !Finite(realNow)) throw new ArgumentException("A finite clock and roster are required.");
            foreach (ulong id in playerIds) participants.Add(id);
            if (participants.Count == 0) throw new ArgumentException("The finish roster cannot be empty.");
            beganAt = realNow;
        }
        public bool Prepared(ulong player) => !Released && participants.Contains(player) && prepared.Add(player);
        public bool Scheduled(ulong player, uint generation) => !Released && !Committed && Generation > 0
            && Generation == generation && participants.Contains(player) && scheduled.Add(player);
        /// <summary>One receipt proves both installation of the final map and this reservation.</summary>
        public bool ReadyForSchedule(ulong player, uint generation)
        {
            if (Released || Committed || Generation == 0 || Generation != generation
                || !participants.Contains(player)) return false;
            prepared.Add(player);
            return scheduled.Add(player);
        }
        public bool Completed(ulong player, uint generation) => !Released && Committed && Generation == generation
            && participants.Contains(player) && completed.Add(player);
        public bool Schedule(double serverNow)
        {
            if (Released || Committed || !Finite(serverNow) || serverNow < 0d) return false;
            if (Generation > 0 && (serverNow < StartsAt - .25d
                || EveryoneScheduled && serverNow < StartsAt)) return false;
            if (Generation == uint.MaxValue) { Skip(); return false; }
            Generation++;
            StartsAt = serverNow + LeadSeconds;
            scheduled.Clear();
            return true;
        }
        public bool Commit(double serverNow)
        {
            if (Released || Committed || !EveryonePrepared || !EveryoneScheduled
                || !Finite(serverNow) || serverNow >= StartsAt) return false;
            Committed = true;
            return true;
        }
        /// <summary>No remote clock receipt is needed when the only participant is the local Host.</summary>
        public bool BeginLocal(ulong player, double serverNow)
        {
            if (Released || Committed || participants.Count != 1 || !participants.Contains(player)
                || !prepared.Contains(player) || !Finite(serverNow) || serverNow < 0d) return false;
            Generation = 1;
            StartsAt = serverNow;
            scheduled.Add(player);
            Committed = true;
            return true;
        }
        public bool TryRelease(double serverNow, double duration)
        {
            if (Released || !Committed || !EveryoneCompleted || !Finite(serverNow) || !Finite(duration)
                || duration <= 0d || serverNow < StartsAt + duration) return false;
            Released = true;
            return true;
        }
        public bool Expire(double realNow)
        {
            if (Released || !Finite(realNow) || realNow - beganAt < TimeoutSeconds) return false;
            Skip();
            return true;
        }
        void Skip() { Skipped = Released = true; }
        bool All(HashSet<ulong> values)
        {
            if (participants.Count == 0) return false;
            foreach (ulong id in participants) if (!values.Contains(id)) return false;
            return true;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
