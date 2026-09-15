using System;
using System.Collections.Generic;

namespace C6.Prototype.Networking
{
    public sealed class TwoParticipantAdmissionPolicy
    {
        public const int Capacity = 2;
        public const ulong HostId = 0;
        private readonly HashSet<ulong> reservations = new HashSet<ulong>();
        public int ReservedCount => reservations.Count;
        public int MaximumParticipants { get; }
        // The name/default preserve T02. P3 explicitly opts into a bounded five-seat reservation pool.
        public TwoParticipantAdmissionPolicy(int maximumParticipants = Capacity)
        {
            if (maximumParticipants != Capacity && maximumParticipants != 5) throw new ArgumentOutOfRangeException(nameof(maximumParticipants));
            MaximumParticipants = maximumParticipants;
        }

        public void BeginHostSession()
        {
            reservations.Clear();
            reservations.Add(HostId);
        }

        public bool TryReserve(ulong clientId)
        {
            if (!reservations.Contains(HostId)) return false;
            if (reservations.Contains(clientId)) return true;
            if (reservations.Count >= MaximumParticipants) return false;
            reservations.Add(clientId);
            return true;
        }

        public void Release(ulong clientId)
        {
            if (clientId != HostId) reservations.Remove(clientId);
        }

        public void EndSession() => reservations.Clear();
    }
}
