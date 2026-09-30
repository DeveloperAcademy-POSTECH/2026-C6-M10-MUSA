using System;
using System.Collections.Generic;
using System.Linq;

namespace C6.Prototype.GameSync
{
    /// <summary>Physical seats never replace the admission/Host roster used for authentication.</summary>
    public static class RoundSeatLayout
    {
        public static bool Valid(IReadOnlyList<ulong> seats, IReadOnlyList<ulong> participants)
            => seats != null && participants != null && seats.Count == participants.Count
                && seats.Count >= 2 && seats.Count <= 5 && seats.Distinct().Count() == seats.Count
                && seats.All(participants.Contains);

        public static ulong[] Shuffle(IReadOnlyList<ulong> participants, Random random)
        {
            if (!Valid(participants, participants) || random == null) throw new ArgumentException("A unique participant roster and random source are required.");
            var result = participants.ToArray();
            for (int i = result.Length - 1; i > 0; i--)
            { int other = random.Next(i + 1); ulong saved = result[i]; result[i] = result[other]; result[other] = saved; }
            return result;
        }
    }
}
