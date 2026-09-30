using System;
using System.Collections.Generic;

namespace C6.Prototype.Networking
{
    /// <summary>Host-approved circular seat order, separate from admission order and player identifiers.</summary>
    public static class ParticipantRing
    {
        public const int MaximumPlayers = 5;
        public const int MaximumStoredPerPlayer = 20;
        public const int MaximumFlyingPerPlayer = 20;
        public const int MaximumFlying = MaximumPlayers * MaximumFlyingPerPlayer;
        public const int MaximumLiveOrbs = MaximumPlayers * MaximumStoredPerPlayer + MaximumFlying;
        public const int MaximumSnapshotBytes = 262144;

        public static bool Validate(ulong[] ids)
        {
            if (ids == null || ids.Length < 2 || ids.Length > MaximumPlayers) return false;
            var unique = new HashSet<ulong>();
            foreach (ulong id in ids) if (!unique.Add(id)) return false;
            return true;
        }

        public static bool TryNeighbour(ulong[] ids, ulong sender, bool right, out ulong receiver)
        {
            receiver = sender;
            if (!Validate(ids)) return false;
            int seat = Array.IndexOf(ids, sender);
            if (seat < 0) return false;
            receiver = ids[(seat + (right ? 1 : ids.Length - 1)) % ids.Length];
            return true;
        }

        public static bool ValidateSeatOrder(IReadOnlyList<ulong> seats, IReadOnlyList<ulong> participants)
        {
            if (seats == null || participants == null || seats.Count < 2 || seats.Count > MaximumPlayers
                || seats.Count != participants.Count) return false;
            var expected = new HashSet<ulong>();
            foreach (ulong id in participants) if (!expected.Add(id)) return false;
            foreach (ulong id in seats) if (!expected.Remove(id)) return false;
            return expected.Count == 0;
        }

        public static bool TrySeatIndex(IReadOnlyList<ulong> seats, ulong participant, out int seatIndex)
        {
            seatIndex = -1;
            if (!ValidateSeatOrder(seats, seats)) return false;
            for (int i = 0; i < seats.Count; i++)
                if (seats[i] == participant) { seatIndex = i; return true; }
            return false;
        }
    }
}
