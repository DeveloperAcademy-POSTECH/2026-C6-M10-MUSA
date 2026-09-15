using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using C6.Prototype.Lobby.Discovery;
using NUnit.Framework;

namespace C6.Prototype.Lobby.Tests
{
    public sealed class DiscoveryRoomTests
    {
        private static RoomAdvertisement Room(string id = "room-a") => new RoomAdvertisement
        {
            RoomId = id, Name = "합이오 방", ProtocolVersion = 10, Build = "0.1.0+14",
            ConfigHash = new string('a', 64), Status = RoomAdvertisementStatus.Lobby, Participants = 1, Port = 7777
        };

        [Test]
        public void TxtRoundTripPreservesEveryAdvertisedFieldAndSrvPort()
        {
            var source = Room(); source.Status = RoomAdvertisementStatus.Playing; source.Participants = 2;
            Assert.That(RoomAdvertisementCodec.TryDecode(RoomAdvertisementCodec.Encode(source, 1234), 7777, out var actual, out var error), Is.True, error);
            Assert.That(actual.RoomId, Is.EqualTo(source.RoomId));
            Assert.That(actual.Name, Is.EqualTo(source.Name));
            Assert.That(actual.ProtocolVersion, Is.EqualTo(source.ProtocolVersion));
            Assert.That(actual.Build, Is.EqualTo(source.Build));
            Assert.That(actual.ConfigHash, Is.EqualTo(source.ConfigHash));
            Assert.That(actual.Status, Is.EqualTo(source.Status));
            Assert.That(actual.Participants, Is.EqualTo(source.Participants));
            Assert.That(actual.Port, Is.EqualTo(source.Port));
        }

        [TestCase(0)] [TestCase(3)] [TestCase(-1)]
        public void ImpossibleParticipantCountsCannotBeAdvertised(int count)
        {
            var room = Room(); room.Participants = count;
            Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False);
            Assert.Throws<ArgumentException>(() => RoomAdvertisementCodec.Encode(room));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(65536)]
        public void InvalidProtocolCannotBeAdvertised(int version)
        { var room = Room(); room.ProtocolVersion = version; Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False); }

        [TestCase("")] [TestCase("abc")]
        [TestCase("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
        public void InvalidConfigHashIsRejected(string value)
        { var room = Room(); room.ConfigHash = value; Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False); }

        [TestCase("contains\nnewline")] [TestCase("\0")] [TestCase(" ")]
        public void InvalidHumanNameIsRejected(string value)
        { var room = Room(); room.Name = value; Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False); }

        [Test]
        public void UnpairedSurrogateNameIsRejectedBeforeEncoding()
        {
            var room = Room(); room.Name = new string(new[] { (char)0xd800 });
            Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False);
        }

        [Test]
        public void NameLimitCountsUtf8Bytes()
        {
            var room = Room(); room.Name = new string('한', 26);
            Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.True);
            room.Name += "한"; Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False);
        }

        [TestCase("room|interface")] [TestCase("room/name")] [TestCase("")]
        public void InvalidRoomIdentityIsRejected(string id)
        { var room = Room(id); Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False); }

        [Test]
        public void UnknownStatusAndZeroSrvPortAreRejected()
        {
            var room = Room(); room.Status = (RoomAdvertisementStatus)8;
            Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False);
            Assert.That(RoomAdvertisementCodec.TryDecode(RoomAdvertisementCodec.Encode(Room()), 0, out _, out _), Is.False);
        }

        [Test]
        public void HeartbeatChangesTxtWithoutChangingLobbyMetadata()
        {
            byte[] first = RoomAdvertisementCodec.Encode(Room(), 1), second = RoomAdvertisementCodec.Encode(Room(), 2);
            Assert.That(first.SequenceEqual(second), Is.False);
            Assert.That(RoomAdvertisementCodec.TryDecode(second, 7777, out var actual, out _), Is.True);
            Assert.That(actual.RoomId, Is.EqualTo("room-a"));
        }

        [Test]
        public void TruncatedTxtFieldIsRejected()
        {
            byte[] valid = RoomAdvertisementCodec.Encode(Room()); Array.Resize(ref valid, valid.Length - 1);
            Assert.That(RoomAdvertisementCodec.TryDecode(valid, 7777, out _, out _), Is.False);
        }

        [Test]
        public void DuplicateCaseInsensitiveKeyCannotOverrideMetadata()
        {
            var bytes = new List<byte>(RoomAdvertisementCodec.Encode(Room()));
            byte[] duplicate = Encoding.UTF8.GetBytes("ID=other-room"); bytes.Add((byte)duplicate.Length); bytes.AddRange(duplicate);
            Assert.That(RoomAdvertisementCodec.TryDecode(bytes.ToArray(), 7777, out _, out _), Is.False);
        }

        [Test]
        public void MalformedUtf8CannotBeSilentlyReplaced()
        {
            var bytes = new List<byte>(RoomAdvertisementCodec.Encode(Room()));
            bytes.AddRange(new byte[] { 4, (byte)'x', (byte)'=', 0xc3, 0x28 });
            Assert.That(RoomAdvertisementCodec.TryDecode(bytes.ToArray(), 7777, out _, out _), Is.False);
        }

        [Test]
        public void MissingMetadataAndOversizedPayloadAreRejected()
        {
            Assert.That(RoomAdvertisementCodec.TryDecode(new byte[] { 4, (byte)'i', (byte)'d', (byte)'=', (byte)'x' }, 7777, out _, out _), Is.False);
            Assert.That(RoomAdvertisementCodec.TryDecode(new byte[RoomAdvertisementCodec.MaximumRecordBytes + 1], 7777, out _, out _), Is.False);
            Assert.That(RoomAdvertisementCodec.TryDecode(null, 7777, out _, out _), Is.False);
        }

        [TestCase("172.30.1.22", true)] [TestCase("192.168.0.5", true)] [TestCase("169.254.1.9", true)]
        [TestCase("127.0.0.1", false)] [TestCase("0.0.0.0", false)] [TestCase("224.0.0.251", false)]
        [TestCase("255.255.255.255", false)] [TestCase("::1", false)] [TestCase("192.168.0.05", false)] [TestCase("3232235525", false)]
        public void OnlyCanonicalUsableIpv4MayEnterCatalog(string address, bool expected)
        { Assert.That(RoomAdvertisementCodec.IsUsableIPv4(address), Is.EqualTo(expected)); }

        [Test]
        public void GoodbyeRemovesOnlyThatInterfaceAndKeepsOtherAddressForSameRoom()
        {
            var catalog = new DiscoveryRoomCatalog();
            Assert.That(catalog.Upsert("wifi|room", Room(), "172.30.1.22", 0), Is.True);
            Assert.That(catalog.Upsert("ethernet|room", Room(), "192.168.1.22", 1), Is.True);
            Assert.That(catalog.Rooms.Count, Is.EqualTo(1)); Assert.That(catalog.Rooms[0].Address, Is.EqualTo("192.168.1.22"));
            Assert.That(catalog.Remove("ethernet|room"), Is.True); Assert.That(catalog.Rooms.Count, Is.EqualTo(1));
            Assert.That(catalog.Rooms[0].Address, Is.EqualTo("172.30.1.22"));
            Assert.That(catalog.Remove("wifi|room"), Is.True); Assert.That(catalog.Rooms, Is.Empty);
        }

        [Test]
        public void StaleRoomExpiresExactlyAtDeadlineAndFreshHeartbeatExtendsIt()
        {
            var catalog = new DiscoveryRoomCatalog(); catalog.Upsert("wifi|room", Room(), "172.30.1.22", 0);
            Assert.That(catalog.Expire(11.999), Is.Zero);
            catalog.Upsert("wifi|room", Room(), "172.30.1.22", 11.999);
            Assert.That(catalog.Expire(12), Is.Zero); Assert.That(catalog.Expire(catalog.Rooms[0].ExpiresAtSeconds), Is.EqualTo(1));
            Assert.That(catalog.Rooms, Is.Empty);
        }

        [Test]
        public void CatalogSnapshotAndOriginalCannotMutateLiveRoom()
        {
            var original = Room(); var catalog = new DiscoveryRoomCatalog();
            catalog.Upsert("wifi|room", original, "172.30.1.22", 0); original.Participants = 2;
            var snapshot = catalog.Rooms; snapshot[0].Advertisement.Participants = 2;
            Assert.That(catalog.Rooms[0].Participants, Is.EqualTo(1)); catalog.Remove("wifi|room");
            Assert.That(snapshot.Count, Is.EqualTo(1)); Assert.That(snapshot[0].Name, Is.EqualTo("합이오 방"));
        }

        [Test]
        public void FullIncompatibleAndPlayingRoomsRemainVisibleForAnExplicitJoinFailure()
        {
            var catalog = new DiscoveryRoomCatalog();
            var room = Room(); room.Status = RoomAdvertisementStatus.Playing; room.Participants = 2; room.ProtocolVersion = 999;
            Assert.That(catalog.Upsert("wifi|room", room, "172.30.1.22", 0), Is.True);
            Assert.That(catalog.Rooms[0].Status, Is.EqualTo(RoomAdvertisementStatus.Playing));
            Assert.That(catalog.Rooms[0].Participants, Is.EqualTo(2)); Assert.That(catalog.Rooms[0].ProtocolVersion, Is.EqualTo(999));
        }

        [Test]
        public void BrowseCancellationClearsEveryRoomAndAllowsFreshSessionClock()
        {
            var catalog = new DiscoveryRoomCatalog();
            catalog.Upsert("wifi|a", Room("a"), "172.30.1.22", 100); catalog.Upsert("wifi|b", Room("b"), "172.30.1.23", 100);
            Assert.That(catalog.Clear(), Is.True); Assert.That(catalog.Rooms, Is.Empty); Assert.That(catalog.Clear(), Is.False);
            Assert.That(catalog.Upsert("wifi|a", Room("a"), "172.30.1.22", 0), Is.True);
        }

        [Test]
        public void ServiceCapacityCannotBeExceededButExistingRoomCanUpdate()
        {
            var catalog = new DiscoveryRoomCatalog();
            for (int i = 0; i < DiscoveryRoomCatalog.MaximumServices; i++)
                Assert.That(catalog.Upsert("wifi|" + i, Room("room" + i), "172.30.1.22", 0), Is.True);
            Assert.That(catalog.Upsert("overflow", Room("overflow"), "172.30.1.22", 1), Is.False);
            var update = Room("room0"); update.Participants = 2;
            Assert.That(catalog.Upsert("wifi|0", update, "172.30.1.22", 1), Is.True);
            Assert.That(catalog.Rooms.Single(x => x.RoomId == "room0").Participants, Is.EqualTo(2));
        }

        [Test]
        public void ClockRollbackOrNonfiniteClockCannotExtendStaleRoom()
        {
            var catalog = new DiscoveryRoomCatalog(); catalog.Upsert("wifi|room", Room(), "172.30.1.22", 10);
            Assert.That(catalog.Upsert("wifi|room", Room(), "172.30.1.22", 9), Is.False);
            Assert.That(catalog.Upsert("wifi|room", Room(), "172.30.1.22", double.NaN), Is.False);
            Assert.That(catalog.Expire(double.PositiveInfinity), Is.Zero); Assert.That(catalog.Expire(22), Is.EqualTo(1));
        }

        [Test]
        public void InvalidMetadataCannotEnterTheCatalog()
        {
            var catalog = new DiscoveryRoomCatalog(); var room = Room(); room.Port = 0;
            Assert.That(catalog.Upsert("wifi|room", room, "172.30.1.22", 0), Is.False);
            Assert.That(catalog.Upsert("", Room(), "172.30.1.22", 0), Is.False);
            Assert.That(catalog.Upsert("wifi|room", Room(), "::1", 0), Is.False); Assert.That(catalog.Rooms, Is.Empty);
        }

        [Test]
        public void IdleLifecycleDisposesTwiceWithoutNativeOperationsAndRejectsReuse()
        {
            var discovery = new BonjourRoomDiscovery();
            discovery.Tick(0);
            Assert.That(discovery.ActiveNativeOperations, Is.Zero);
            discovery.Dispose(); discovery.Dispose();
            Assert.Throws<ObjectDisposedException>(() => discovery.Tick(1));
            Assert.Throws<ObjectDisposedException>(() => discovery.StartBrowse());
        }

        [Test]
        public void DiscoveryTickRejectsClockRollbackAndNonfiniteTime()
        {
            using (var discovery = new BonjourRoomDiscovery())
            {
                discovery.Tick(10);
                Assert.Throws<ArgumentOutOfRangeException>(() => discovery.Tick(9));
                Assert.Throws<ArgumentOutOfRangeException>(() => discovery.Tick(double.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => discovery.Tick(double.PositiveInfinity));
            }
        }

        [Test]
        public void CrossThreadCallsAreRejectedBeforeAnyNativeOperation()
        {
            using (var discovery = new BonjourRoomDiscovery())
            {
                Exception caught = null;
                var thread = new System.Threading.Thread(() =>
                {
                    try { discovery.StartBrowse(); } catch (Exception error) { caught = error; }
                });
                thread.Start(); Assert.That(thread.Join(1000), Is.True);
                Assert.That(caught, Is.TypeOf<InvalidOperationException>());
                Assert.That(discovery.ActiveNativeOperations, Is.Zero);
            }
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidCatalogLifetimeIsRejected(double lifetime)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryRoomCatalog(lifetime)); }
    }
}
