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
