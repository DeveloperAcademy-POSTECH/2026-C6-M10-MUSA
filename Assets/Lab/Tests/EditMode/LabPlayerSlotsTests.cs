using System;
using System.Linq;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabPlayerSlotsTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ChosenPNumberDefinesAnUnbrokenRoomSizedRing(int capacity)
        {
            var slots = new LabPlayerSlots(capacity);
            ulong[] connectedInReversePOrder = Enumerable.Range(1, capacity)
                .Reverse().Select(number => (ulong)(100 + number)).ToArray();
            foreach (ulong player in connectedInReversePOrder)
                Assert.That(slots.TrySelect(player, (int)(player - 100), out _), Is.True);

            Assert.That(slots.IsComplete(connectedInReversePOrder), Is.True);
            Assert.That(slots.PlayersInSlotOrder(), Is.EqualTo(Enumerable.Range(1, capacity)
                .Select(number => (ulong)(100 + number))));
            Assert.That(slots.GetOwner(1), Is.EqualTo(101ul));
            Assert.That(slots.GetOwner(capacity), Is.EqualTo((ulong)(100 + capacity)));
            Assert.That(slots.GetOwner(capacity + 1), Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void HostMayChooseAnySlotAndAnOccupiedSlotCannotBeStolen()
        {
            var slots = new LabPlayerSlots(3);
            Assert.That(slots.TrySelect(0ul, 3, out _), Is.True);
            Assert.That(slots.TrySelect(2ul, 3, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("SLOT_TAKEN"));
            Assert.That(slots.GetSlot(0ul), Is.EqualTo(3));
            Assert.That(slots.GetSlot(2ul), Is.Zero);
            Assert.That(slots.TrySelect(0ul, 1, out _), Is.True);
            Assert.That(slots.GetOwner(3), Is.EqualTo(ulong.MaxValue));
            Assert.That(slots.GetOwner(1), Is.Zero);
            Assert.That(slots.TrySelect(0ul, 0, out _), Is.True);
            Assert.That(slots.GetSlot(0ul), Is.Zero);
        }

        [Test]
        public void ConcurrentClaimsForTheSamePNumberHaveExactlyOneWinner()
        {
            var slots = new LabPlayerSlots(2);
            Assert.That(slots.TrySelect(21ul, 1, out _), Is.True);
            Assert.That(slots.TrySelect(22ul, 1, out string rejected), Is.False);
            Assert.That(rejected, Is.EqualTo("SLOT_TAKEN"));
            Assert.That(slots.GetOwner(1), Is.EqualTo(21ul));
            Assert.That(slots.GetSlot(22ul), Is.Zero);
        }

        [Test]
        public void LobbyReplacementRejectsMalformedMappingsWithoutChangingThePreviousOne()
        {
            var slots = new LabPlayerSlots(3);
            Assert.That(slots.TrySelect(10ul, 2, out _), Is.True);
            ulong[] connected = { 10ul, 20ul, 30ul };
            LabPlayerSlotAssignment[] malformed =
            {
                new LabPlayerSlotAssignment { playerId = 10ul, slotNumber = 1 },
                new LabPlayerSlotAssignment { playerId = 20ul, slotNumber = 1 }
            };
            Assert.That(slots.TryReplace(malformed, connected), Is.False);
            Assert.That(slots.GetSlot(10ul), Is.EqualTo(2));
            malformed[1].playerId = 10ul;
            malformed[1].slotNumber = 3;
            Assert.That(slots.TryReplace(malformed, connected), Is.False);
            malformed[1].playerId = 20ul;
            malformed[1].slotNumber = 4;
            Assert.That(slots.TryReplace(malformed, connected), Is.False);
            malformed[1].slotNumber = 3;
            malformed[1].playerId = 99ul;
            Assert.That(slots.TryReplace(malformed, connected), Is.False);
            Assert.That(slots.GetSlot(10ul), Is.EqualTo(2));

            malformed[1].playerId = 20ul;
            Assert.That(slots.TryReplace(malformed, connected), Is.True);
            Assert.That(slots.GetSlot(10ul), Is.EqualTo(1));
            Assert.That(slots.GetSlot(20ul), Is.EqualTo(3));
            Assert.That(slots.IsComplete(connected), Is.False);
        }

        [Test]
        public void LeaveReleasesSlotAndPreventsStartUntilAnotherPlayerClaimsIt()
        {
            var slots = new LabPlayerSlots(2);
            Assert.That(slots.TrySelect(10ul, 1, out _), Is.True);
            Assert.That(slots.TrySelect(20ul, 2, out _), Is.True);
            Assert.That(slots.IsComplete(new[] { 10ul, 20ul }), Is.True);
            Assert.That(slots.Release(20ul), Is.True);
            Assert.That(slots.IsComplete(new[] { 10ul, 30ul }), Is.False);
            Assert.That(slots.TrySelect(30ul, 2, out _), Is.True);
            Assert.That(slots.IsComplete(new[] { 10ul, 30ul }), Is.True);
        }

        [Test]
        public void OnlyRoomSizedPNumbersAreAccepted()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabPlayerSlots(1));
            // Six seats exist for a 3:3 territory room (#394); the room decides which sizes it allows.
            Assert.That(new LabPlayerSlots(6).Capacity, Is.EqualTo(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabPlayerSlots(7));
            var slots = new LabPlayerSlots(2);
            Assert.That(slots.TrySelect(10ul, 3, out _), Is.False);
            Assert.That(slots.TrySelect(ulong.MaxValue, 1, out _), Is.False);
            Assert.That(slots.SelectedCount, Is.Zero);
        }

        [Test]
        public void ASeatIsTakenWithOneOfFiveCharactersAndACharacterBelongsToOnePlayer()
        {
            var slots = new LabPlayerSlots(3) { RequireCharacter = true };
            Assert.That(slots.TrySelect(10ul, 2, 4, out string reason), Is.True, reason);
            Assert.That(slots.GetCharacter(10ul), Is.EqualTo(4));
            Assert.That(slots.GetCharacterOwner(4), Is.EqualTo(10ul));

            Assert.That(slots.TrySelect(20ul, 1, 4, out reason), Is.False, "A character another player holds cannot be taken.");
            Assert.That(reason, Is.EqualTo("CHARACTER_TAKEN"));
            Assert.That(slots.GetSlot(20ul), Is.Zero, "A refused request changes nothing.");
            Assert.That(slots.TrySelect(20ul, 1, 6, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_CHARACTER"));
            Assert.That(slots.TrySelect(20ul, 1, 5, out _), Is.True);

            // Moving seats keeps the character; changing only the character keeps the seat.
            Assert.That(slots.TrySelect(10ul, 3, out _), Is.True);
            Assert.That(slots.GetCharacter(10ul), Is.EqualTo(4));
            Assert.That(slots.TrySelect(10ul, 3, 1, out _), Is.True);
            Assert.That(slots.GetCharacter(10ul), Is.EqualTo(1));
            Assert.That(slots.GetCharacterOwner(4), Is.EqualTo(ulong.MaxValue));
            Assert.That(slots.TrySelect(10ul, 3, 1, out reason), Is.True);
            Assert.That(reason, Is.EqualTo("SLOT_UNCHANGED"));

            // A seat cannot be taken without a character; scripted claims ask for the first free one in the picker's order.
            Assert.That(slots.TrySelect(30ul, 2, 0, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("CHARACTER_REQUIRED"));
            Assert.That(slots.TrySelect(30ul, 2, out reason), Is.False, "The seat-only request has no character either.");
            Assert.That(slots.GetSlot(30ul), Is.Zero);
            Assert.That(slots.FirstFreeCharacter(), Is.EqualTo(3), "불이(1) is taken, so 묵이(3) comes next.");
            Assert.That(slots.IsComplete(new[] { 10ul, 20ul, 30ul }), Is.False);
            // A returning player whose character was taken keeps the seat alone and must pick again before the start.
            Assert.That(slots.TryRestoreSeat(30ul, 2), Is.True);
            Assert.That(slots.GetSlot(30ul), Is.EqualTo(2));
            Assert.That(slots.GetCharacter(30ul), Is.Zero);
            Assert.That(slots.IsComplete(new[] { 10ul, 20ul, 30ul }), Is.False, "A seat without a character cannot start.");
            Assert.That(slots.TrySelect(30ul, 2, 1, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("CHARACTER_TAKEN"));
            Assert.That(slots.TrySelect(30ul, 2, 3, out _), Is.True, "Picking a character keeps the seat.");
            Assert.That(slots.IsComplete(new[] { 10ul, 20ul, 30ul }), Is.True);
            Assert.That(slots.CharactersInSlotOrder(), Is.EqualTo(new[] { 5, 3, 1 }));

            // Releasing the seat releases the character.
            Assert.That(slots.TrySelect(20ul, 0, out _), Is.True);
            Assert.That(slots.GetCharacter(20ul), Is.Zero);
            Assert.That(slots.GetCharacterOwner(5), Is.EqualTo(ulong.MaxValue));
            Assert.That(slots.Release(30ul), Is.True);
            Assert.That(slots.GetCharacterOwner(3), Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void RoomsWithoutCharactersKeepSeatsOnlyAndTheLobbyCopyCarriesCharacters()
        {
            var plain = new LabPlayerSlots(2);
            Assert.That(plain.TrySelect(10ul, 1, out _), Is.True);
            Assert.That(plain.GetCharacter(10ul), Is.Zero, "Land grab and standalone Lab seats have no character.");
            Assert.That(plain.Snapshot().Single().character, Is.Zero);

            var host = new LabPlayerSlots(3) { RequireCharacter = true };
            host.TrySelect(10ul, 1, 2, out _);
            host.TrySelect(20ul, 3, 5, out _);
            var guest = new LabPlayerSlots(3);
            Assert.That(guest.TryReplace(host.Snapshot(), new[] { 10ul, 20ul, 30ul }), Is.True);
            Assert.That(guest.GetCharacter(10ul), Is.EqualTo(2));
            Assert.That(guest.GetCharacter(20ul), Is.EqualTo(5));
            Assert.That(guest.GetCharacterOwner(5), Is.EqualTo(20ul));

            var duplicated = new[]
            {
                new LabPlayerSlotAssignment { playerId = 10ul, slotNumber = 1, character = 2 },
                new LabPlayerSlotAssignment { playerId = 20ul, slotNumber = 2, character = 2 },
            };
            Assert.That(guest.TryReplace(duplicated, new[] { 10ul, 20ul, 30ul }), Is.False, "Two seats cannot share a character.");
            Assert.That(guest.GetCharacter(20ul), Is.EqualTo(5), "The last valid lobby stays.");
        }
    }
}
