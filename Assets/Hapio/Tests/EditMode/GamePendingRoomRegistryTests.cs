using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GamePendingRoomRegistryTests
    {
        [Test]
        public void InvalidOrDisposedRegistryCannotCreateOrEnterARoom()
        {
            var registry = new GamePendingRoomRegistry();
            Assert.That(registry.TryCreate(1, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(registry.TryCreate(6, out error), Is.False);
            Assert.That(registry.Rooms, Is.Empty);
            Assert.That(registry.TryEnter("missing", null, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            registry.Dispose();
            Assert.That(registry.TryCreate(2, out error), Is.False);
            Assert.That(registry.CanEnter("missing"), Is.False);
            Assert.DoesNotThrow(() => registry.Tick(out _));
            Assert.DoesNotThrow(registry.Dispose);
        }

        [Test]
        public void CreationHandleCanCancelOnlyItsOwnUnconfirmedRoomAndReleasesItsPort()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Game pending rooms require macOS/iOS Bonjour.");
            using (var registry = new GamePendingRoomRegistry())
            using (var other = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreate(2, GameMode.Story, out GamePendingRoomHandle first, out string error), Is.True, error);
                Assert.That(registry.TryCreate(5, GameMode.Story, out GamePendingRoomHandle second, out error), Is.True, error);
                Assert.That(registry.TryGet(first, out GamePendingRoomInfo original), Is.True);
                Assert.That(original.Ready, Is.False, "A creation handle is usable before a discovery name exists.");
                Assert.That(original.Name, Is.Empty);
                Assert.That(other.TryGet(first, out _), Is.False);
                Assert.That(other.Cancel(first), Is.False, "A different registry cannot dispose this request.");
                Assert.That(registry.Cancel(first), Is.True);
                Assert.That(registry.TryGet(first, out _), Is.False);
                Assert.That(registry.Cancel(first), Is.False);
                Assert.That(registry.TryGet(second, out GamePendingRoomInfo retained), Is.True);
                Assert.That(retained.Capacity, Is.EqualTo(5));
                Assert.That(registry.Rooms.Count, Is.EqualTo(1));
                using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, original.Port)));
                using (var reserved = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    Assert.Throws<SocketException>(() => reserved.Bind(new IPEndPoint(IPAddress.Any, retained.Port)));
                registry.Dispose();
                Assert.That(registry.TryGet(second, out _), Is.False);
                Assert.That(registry.Cancel(second), Is.False);
            }
        }

        [Test]
        public void TerritoryCreationHandleKeepsFourPlayerCapacityAndInvalidCreationHasNoHandle()
        {
            using (var registry = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreate(1, GameMode.Story, out GamePendingRoomHandle invalid, out string error), Is.False);
                Assert.That(invalid, Is.Null);
                Assert.That(error, Is.Not.Empty);
                if (!LabBonjourDiscovery.IsSupported) return;
                Assert.That(registry.TryCreate(2, GameMode.Territory, out GamePendingRoomHandle handle, out error), Is.True, error);
                Assert.That(registry.TryGet(handle, out GamePendingRoomInfo room), Is.True);
                Assert.That(room.Capacity, Is.EqualTo(LabTeams.Players));
                Assert.That(room.Mode, Is.EqualTo(GameMode.Territory));
            }
        }

        [Test]
        public void PendingCardsReserveIndependentPortsAndDisposeReleasesEveryReservation()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Game pending rooms require macOS/iOS Bonjour.");
            var registry = new GamePendingRoomRegistry();
            try
            {
                Assert.That(registry.TryCreate(2, out string error), Is.True, error);
                Assert.That(registry.TryCreate(5, out error), Is.True, error);
                var cards = registry.Rooms.ToArray();
                Assert.That(cards.Select(card => card.Capacity), Is.EqualTo(new[] { 2, 5 }));
                Assert.That(cards.Select(card => card.Port).Distinct().Count(), Is.EqualTo(2));
                Assert.That(cards.All(card => !card.Ready && !card.Failed), Is.True,
                    "Creation reserves ports; the persistent session later ticks registration.");
                foreach (var card in cards)
                {
                    Assert.That(registry.CanEnter(card.Key), Is.False);
                    using (var contender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                        Assert.Throws<SocketException>(() => contender.Bind(new IPEndPoint(IPAddress.Any, card.Port)));
                }
                registry.Dispose();
                Assert.That(registry.Rooms, Is.Empty);
                Assert.That(cards.Length, Is.EqualTo(2), "A screen's value snapshot is independent of native ownership.");
                foreach (var card in cards)
                    using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                        Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, card.Port)));
            }
            finally { registry.Dispose(); }
        }
    }
}
