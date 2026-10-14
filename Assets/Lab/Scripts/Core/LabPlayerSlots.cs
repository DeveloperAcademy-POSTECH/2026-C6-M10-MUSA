using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    /// <summary>A Host-approved P number, independent of Netcode's connection ID.</summary>
    [Serializable]
    public sealed class LabPlayerSlotAssignment
    {
        public ulong playerId;
        public int slotNumber;
    }

    /// <summary>
    /// The room has P1..PN, where N is fixed when the Host creates it. The battle seat for
    /// Pk is k-1, so the existing N-seat geometry and wraparound use an unbroken ring.
    /// </summary>
    public sealed class LabPlayerSlots
    {
        private readonly Dictionary<ulong, int> slotsByPlayer = new Dictionary<ulong, int>();
        private readonly Dictionary<int, ulong> playersBySlot = new Dictionary<int, ulong>();

        public int Capacity { get; }
        public int SelectedCount => slotsByPlayer.Count;

        public LabPlayerSlots(int capacity)
        {
            if (capacity < LabModel.MinimumParticipants || capacity > LabModel.MaximumParticipants)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int GetSlot(ulong playerId) => slotsByPlayer.TryGetValue(playerId, out int slot) ? slot : 0;

        public ulong GetOwner(int slotNumber) => slotNumber >= 1 && slotNumber <= Capacity
            && playersBySlot.TryGetValue(slotNumber, out ulong owner) ? owner : ulong.MaxValue;

        /// <summary>0 releases the player's slot. An occupied slot cannot be stolen.</summary>
        public bool TrySelect(ulong playerId, int slotNumber, out string reason)
        {
            if (playerId == ulong.MaxValue || slotNumber < 0 || slotNumber > Capacity)
            {
                reason = "INVALID_SLOT";
                return false;
            }
            if (slotNumber > 0 && playersBySlot.TryGetValue(slotNumber, out ulong owner) && owner != playerId)
            {
                reason = "SLOT_TAKEN";
                return false;
            }
            int previous = GetSlot(playerId);
            if (previous == slotNumber)
            {
                reason = "SLOT_UNCHANGED";
                return true;
            }
            if (previous > 0)
            {
                slotsByPlayer.Remove(playerId);
                playersBySlot.Remove(previous);
            }
            if (slotNumber > 0)
            {
                slotsByPlayer.Add(playerId, slotNumber);
                playersBySlot.Add(slotNumber, playerId);
            }
            reason = slotNumber == 0 ? "SLOT_CLEARED" : "SLOT_SELECTED";
            return true;
        }

        public bool Release(ulong playerId)
        {
            int slot = GetSlot(playerId);
            if (slot == 0) return false;
            slotsByPlayer.Remove(playerId);
            playersBySlot.Remove(slot);
            return true;
        }

        public bool IsComplete(IEnumerable<ulong> connectedPlayers)
        {
            if (connectedPlayers == null || SelectedCount != Capacity) return false;
            ulong[] connected = connectedPlayers.ToArray();
            return connected.Length == Capacity && connected.Distinct().Count() == Capacity
                && connected.All(player => GetSlot(player) > 0);
        }

        public ulong[] PlayersInSlotOrder()
        {
            if (SelectedCount != Capacity) throw new InvalidOperationException("Every P slot must be selected.");
            var ordered = new ulong[Capacity];
            for (int slot = 1; slot <= Capacity; slot++) ordered[slot - 1] = playersBySlot[slot];
            return ordered;
        }

        public LabPlayerSlotAssignment[] Snapshot() => playersBySlot.OrderBy(entry => entry.Key)
            .Select(entry => new LabPlayerSlotAssignment { playerId = entry.Value, slotNumber = entry.Key })
            .ToArray();

        /// <summary>Reject malformed Host lobby data without changing a prior valid state.</summary>
        public bool TryReplace(LabPlayerSlotAssignment[] assignments, IEnumerable<ulong> connectedPlayers)
        {
            if (assignments == null || connectedPlayers == null || assignments.Length > Capacity) return false;
            var connected = new HashSet<ulong>(connectedPlayers);
            if (connected.Count > Capacity || connected.Contains(ulong.MaxValue)) return false;
            var replacement = new LabPlayerSlots(Capacity);
            foreach (LabPlayerSlotAssignment assignment in assignments)
            {
                if (assignment == null || !connected.Contains(assignment.playerId)
                    || assignment.slotNumber == 0
                    || replacement.GetSlot(assignment.playerId) != 0
                    || !replacement.TrySelect(assignment.playerId, assignment.slotNumber, out _)) return false;
            }
            slotsByPlayer.Clear();
            playersBySlot.Clear();
            foreach (LabPlayerSlotAssignment assignment in assignments)
            {
                slotsByPlayer.Add(assignment.playerId, assignment.slotNumber);
                playersBySlot.Add(assignment.slotNumber, assignment.playerId);
            }
            return true;
        }
    }
}
