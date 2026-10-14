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
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabPlayerSlots(6));
            var slots = new LabPlayerSlots(2);
            Assert.That(slots.TrySelect(10ul, 3, out _), Is.False);
            Assert.That(slots.TrySelect(ulong.MaxValue, 1, out _), Is.False);
            Assert.That(slots.SelectedCount, Is.Zero);
        }
    }
}
