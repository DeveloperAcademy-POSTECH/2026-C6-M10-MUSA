using System;
using System.Collections.Generic;

namespace C6.Prototype.Networking
{
    /// <summary>Frozen Host-approved join order. Seat order never depends on network ID sorting.</summary>
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
    }
}
