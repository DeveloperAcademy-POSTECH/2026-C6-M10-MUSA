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
        /// <summary>The battle character chosen with the seat: 1..5 in artwork order (불이, 솔이, 묵이, 금옥, 만복); 0 = none.</summary>
        public int character;
    }

    /// <summary>
    /// The room has P1..PN, where N is fixed when the Host creates it. The battle seat for
    /// Pk is k-1, so the existing N-seat geometry and wraparound use an unbroken ring.
    /// </summary>
    public sealed class LabPlayerSlots
    {
        /// <summary>
        /// P numbers run clockwise when viewed from above. Looking in toward the target,
        /// the next P number is on the left; the previous one is on the right.
        /// Two-player rooms therefore have the same neighbour on both sides.
        /// </summary>
        public static int NeighborSeat(int seat, int count, bool right)
        {
            if (count < 1 || seat < 0 || seat >= count) throw new ArgumentOutOfRangeException(nameof(seat));
            return (seat + (right ? count - 1 : 1)) % count;
        }

        private readonly Dictionary<ulong, int> slotsByPlayer = new Dictionary<ulong, int>();
        private readonly Dictionary<int, ulong> playersBySlot = new Dictionary<int, ulong>();
        private readonly Dictionary<ulong, int> charactersByPlayer = new Dictionary<ulong, int>();

        /// <summary>Battle characters a seat can be taken with (story rooms): 1..CharacterCount, each by one player only.</summary>
        public const int CharacterCount = 5;
        /// <summary>The order a picker shows them in, as character numbers: 불이, 묵이, 솔이, 금옥, 만복.</summary>
        public static readonly int[] CharacterOrder = { 1, 3, 2, 4, 5 };
        /// <summary>
        /// A seat is taken only together with a character (Hapio story rooms): a request without one is refused, and
        /// the room is not complete until every seat has one. Off (land grab, standalone Lab): seats have no character.
        /// </summary>
        public bool RequireCharacter { get; set; }

        /// <summary>The first character nobody holds, in the picker's order (0 if none); for scripted automatic claims.</summary>
        public int FirstFreeCharacter()
        {
            foreach (int character in CharacterOrder)
                if (GetCharacterOwner(character) == ulong.MaxValue) return character;
            return 0;
        }

        public int Capacity { get; }
        public int SelectedCount => slotsByPlayer.Count;

        public LabPlayerSlots(int capacity)
        {
            // 6 seats only for a 3:3 territory room (#394); the room decides which sizes it allows.
            if (capacity < LabModel.MinimumParticipants || capacity > LabModel.TerritoryMaximumParticipants)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int GetSlot(ulong playerId) => slotsByPlayer.TryGetValue(playerId, out int slot) ? slot : 0;

        public int GetCharacter(ulong playerId) => charactersByPlayer.TryGetValue(playerId, out int character) ? character : 0;

        public ulong GetCharacterOwner(int character)
        {
            foreach (var entry in charactersByPlayer) if (entry.Value == character) return entry.Key;
            return ulong.MaxValue;
        }

        public ulong GetOwner(int slotNumber) => slotNumber >= 1 && slotNumber <= Capacity
            && playersBySlot.TryGetValue(slotNumber, out ulong owner) ? owner : ulong.MaxValue;

        /// <summary>0 releases the player's slot. An occupied slot cannot be stolen. The player's character is kept.</summary>
        public bool TrySelect(ulong playerId, int slotNumber, out string reason) =>
            TrySelect(playerId, slotNumber, GetCharacter(playerId), out reason);

        /// <summary>
        /// Take a seat together with a battle character (0 = none). Releasing the seat (0) releases the character too.
        /// A character another player holds cannot be taken, and nothing changes when the request is refused.
        /// </summary>
        public bool TrySelect(ulong playerId, int slotNumber, int character, out string reason) =>
            TrySelect(playerId, slotNumber, character, false, out reason);

        /// <summary>
        /// Give a returning player its seat back when its character went to someone else meanwhile: the seat is kept
        /// without a character, and the player picks one again before the room can start.
        /// </summary>
        public bool TryRestoreSeat(ulong playerId, int slotNumber) =>
            slotNumber > 0 && TrySelect(playerId, slotNumber, 0, true, out _);

        private bool TrySelect(ulong playerId, int slotNumber, int character, bool seatOnly, out string reason)
        {
            if (playerId == ulong.MaxValue || slotNumber < 0 || slotNumber > Capacity)
            {
                reason = "INVALID_SLOT";
                return false;
            }
            if (slotNumber == 0) character = 0;
            if (character < 0 || character > CharacterCount)
            {
                reason = "INVALID_CHARACTER";
                return false;
            }
            if (slotNumber > 0 && playersBySlot.TryGetValue(slotNumber, out ulong owner) && owner != playerId)
            {
                reason = "SLOT_TAKEN";
                return false;
            }
            if (slotNumber > 0 && character == 0 && RequireCharacter && !seatOnly)
            {
                reason = "CHARACTER_REQUIRED";
                return false;
            }
            if (character > 0)
            {
                ulong holder = GetCharacterOwner(character);
                if (holder != ulong.MaxValue && holder != playerId)
                {
                    reason = "CHARACTER_TAKEN";
                    return false;
                }
            }
            int previous = GetSlot(playerId);
            if (previous == slotNumber && GetCharacter(playerId) == character)
            {
                reason = "SLOT_UNCHANGED";
                return true;
            }
            if (previous > 0 && previous != slotNumber)
            {
                slotsByPlayer.Remove(playerId);
                playersBySlot.Remove(previous);
            }
            if (slotNumber > 0 && previous != slotNumber)
            {
                slotsByPlayer.Add(playerId, slotNumber);
                playersBySlot.Add(slotNumber, playerId);
            }
            if (character > 0) charactersByPlayer[playerId] = character;
            else charactersByPlayer.Remove(playerId);
            reason = slotNumber == 0 ? "SLOT_CLEARED" : "SLOT_SELECTED";
            return true;
        }

        public bool Release(ulong playerId)
        {
            int slot = GetSlot(playerId);
            if (slot == 0) return false;
            slotsByPlayer.Remove(playerId);
            playersBySlot.Remove(slot);
            charactersByPlayer.Remove(playerId);
            return true;
        }

        public bool IsComplete(IEnumerable<ulong> connectedPlayers)
        {
            if (connectedPlayers == null || SelectedCount != Capacity) return false;
            ulong[] connected = connectedPlayers.ToArray();
            return connected.Length == Capacity && connected.Distinct().Count() == Capacity
                && connected.All(player => GetSlot(player) > 0 && (!RequireCharacter || GetCharacter(player) > 0));
        }

        /// <summary>Each seat's character in P order (0 = none), for the battle model.</summary>
        public int[] CharactersInSlotOrder()
        {
            var characters = new int[Capacity];
            for (int slot = 1; slot <= Capacity; slot++)
                if (playersBySlot.TryGetValue(slot, out ulong owner)) characters[slot - 1] = GetCharacter(owner);
            return characters;
        }

        public ulong[] PlayersInSlotOrder()
        {
            if (SelectedCount != Capacity) throw new InvalidOperationException("Every P slot must be selected.");
            var ordered = new ulong[Capacity];
            for (int slot = 1; slot <= Capacity; slot++) ordered[slot - 1] = playersBySlot[slot];
            return ordered;
        }

        public LabPlayerSlotAssignment[] Snapshot() => playersBySlot.OrderBy(entry => entry.Key)
            .Select(entry => new LabPlayerSlotAssignment { playerId = entry.Value, slotNumber = entry.Key,
                character = GetCharacter(entry.Value) })
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
                    || !replacement.TrySelect(assignment.playerId, assignment.slotNumber, assignment.character, out _)) return false;
            }
            slotsByPlayer.Clear();
            playersBySlot.Clear();
            charactersByPlayer.Clear();
            foreach (LabPlayerSlotAssignment assignment in assignments)
            {
                slotsByPlayer.Add(assignment.playerId, assignment.slotNumber);
                playersBySlot.Add(assignment.slotNumber, assignment.playerId);
                if (assignment.character > 0) charactersByPlayer.Add(assignment.playerId, assignment.character);
            }
            return true;
        }
    }
}
