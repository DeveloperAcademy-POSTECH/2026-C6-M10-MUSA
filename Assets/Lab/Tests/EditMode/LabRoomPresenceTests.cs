using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabRoomPresenceTests
    {
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void WaitingCreatorAndFullBattleRoundTripForEverySupportedCapacity(int capacity)
        {
            string protocol = LabBonjourDiscovery.Protocol;
            byte[] waiting = LabRoomPresence.Encode(protocol, true, true, 1, capacity, false, true);
            Assert.That(LabRoomPresence.TryDecode(waiting, protocol, out bool v4, out bool v6,
                out int count, out int target, out bool playing, out bool pending), Is.True);
            Assert.That(v4 && v6 && pending, Is.True);
            Assert.That(count, Is.EqualTo(1), "One reserved creator is not an active game connection.");
            Assert.That(target, Is.EqualTo(capacity));
            Assert.That(playing, Is.False);

            byte[] active = LabRoomPresence.Encode(protocol, false, true, capacity, capacity, true);
            Assert.That(LabRoomPresence.TryDecode(active, protocol, out v4, out v6,
                out count, out target, out playing, out pending), Is.True);
            Assert.That(v4, Is.False); Assert.That(v6 && playing, Is.True); Assert.That(pending, Is.False);
            Assert.That(count, Is.EqualTo(capacity));
        }

        [TestCase("f=46;n=0;c=2;b=0")]
        [TestCase("f=46;n=3;c=2;b=0")]
        [TestCase("f=46;n=1;c=6;b=0")]
        [TestCase("f=;n=1;c=2;b=0")]
        [TestCase("f=46;n=1;c=2;b=1;w=1")]
        [TestCase("f=46;n=2;c=2;b=0;w=1")]
        [TestCase("f=46;n=1;c=2;b=0;n=2")]
        public void MalformedPresenceNeverBecomesAJoinCandidate(string fields)
        {
            byte[] record = Record("p=" + LabBonjourDiscovery.Protocol + ";" + fields);
            Assert.That(LabRoomPresence.TryDecode(record, LabBonjourDiscovery.Protocol,
                out _, out _, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void TeamRoomMarkerRoundTripsAndOlderRecordsAreStoryRooms()
        {
            string protocol = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
            byte[] team = LabRoomPresence.Encode(protocol, true, true, 1, 4, false, true, true);
            Assert.That(LabRoomPresence.TryDecode(team, protocol, out _, out _, out int count, out int capacity,
                out _, out bool waiting, out bool isTeam), Is.True);
            Assert.That((count, capacity, waiting, isTeam), Is.EqualTo((1, 4, true, true)));
            byte[] active = LabRoomPresence.Encode(protocol, true, false, 3, 4, false, false, true);
            Assert.That(LabRoomPresence.TryDecode(active, protocol, out _, out _, out _, out _, out _, out waiting, out isTeam), Is.True);
            Assert.That(waiting, Is.False); Assert.That(isTeam, Is.True);
            byte[] story = LabRoomPresence.Encode(protocol, true, true, 2, 3, true);
            Assert.That(LabRoomPresence.TryDecode(story, protocol, out _, out _, out _, out _, out _, out _, out isTeam), Is.True);
            Assert.That(isTeam, Is.False);
        }

        [TestCase("f=46;n=1;c=4;b=0;t=1;t=1")]
        [TestCase("f=46;n=1;c=4;b=0;t=1;w=1")]
        [TestCase("f=46;n=1;c=4;b=0;t=0")]
        [TestCase("f=46;n=2;c=4;b=0;w=1;t=1")]
        public void MalformedTeamMarkerNeverBecomesAJoinCandidate(string fields)
        {
            byte[] record = Record("p=" + LabBonjourDiscovery.Protocol + ";" + fields);
            Assert.That(LabRoomPresence.TryDecode(record, LabBonjourDiscovery.Protocol,
                out _, out _, out _, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void WrongProtocolTruncatedLengthAndInvalidUtf8AreRejected()
        {
            byte[] record = LabRoomPresence.Encode(LabBonjourDiscovery.Protocol, true, false, 1, 2, false);
            Assert.That(LabRoomPresence.TryDecode(record, "C6-LAB-old", out _, out _, out _, out _, out _), Is.False);
            record[0]--;
            Assert.That(LabRoomPresence.TryDecode(record, LabBonjourDiscovery.Protocol,
                out _, out _, out _, out _, out _), Is.False);
            Assert.That(LabRoomPresence.TryDecode(new byte[] { 2, 0xc3, 0x28 }, LabBonjourDiscovery.Protocol,
                out _, out _, out _, out _, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LabRoomPresence.Encode("bad;protocol", true, true, 1, 2, false));
        }

        [Test]
        public void PendingTwoPlayerRoomReservesUniquePortsUntilDisposedWithoutStartingAHost()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Pending room advertisement supports macOS/iOS.");
            Assert.That(LabPendingRoom.TryCreate(2, out var first, out string error), Is.True, error);
            LabPendingRoom second = null;
            ushort firstPort = first.Port;
            try
            {
                Assert.That(LabPendingRoom.TryCreate(2, out second, out error), Is.True, error);
                Assert.That(first.Port, Is.Not.Zero); Assert.That(second.Port, Is.Not.EqualTo(first.Port));
                Assert.That(first.Capacity, Is.EqualTo(2));
                Assert.That(first.Ready || first.Failed, Is.False, "Creation reserves a port before native registration or room entry.");
                Assert.That(first.EnterAsCreator(null), Is.False);
                using (var contender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    Assert.Throws<SocketException>(() => contender.Bind(new IPEndPoint(IPAddress.Any, firstPort)));
                first.Dispose();
                using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, firstPort)));
            }
            finally { first.Dispose(); second?.Dispose(); }
        }

        [TestCase(1)] [TestCase(6)]
        public void PendingRoomRejectsUnsupportedCapacityBeforeAllocating(int capacity)
        {
            Assert.That(LabPendingRoom.TryCreate(capacity, out var pending, out string error), Is.False);
            Assert.That(pending, Is.Null); Assert.That(error, Is.Not.Empty);
        }

        [TestCase(AddressFamily.InterNetwork)]
        [TestCase(AddressFamily.InterNetworkV6)]
        public void UnsupportedFamilyStillReservesTheOtherFamily(AddressFamily unsupported)
        {
            if (unsupported == AddressFamily.InterNetwork && !Socket.OSSupportsIPv6)
                Assert.Ignore("The fallback IPv6 socket is not supported on this test host.");
            if (unsupported == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv4)
                Assert.Ignore("The fallback IPv4 socket is not supported on this test host.");
            var pending = (LabPendingRoom)Activator.CreateInstance(typeof(LabPendingRoom), true);
            try
            {
                Func<AddressFamily, Socket> factory = family => family == unsupported
                    ? throw new SocketException((int)SocketError.AddressFamilyNotSupported)
                    : new Socket(family, SocketType.Dgram, ProtocolType.Udp);
                Reserve(pending, factory);
                Assert.That(pending.Port, Is.Not.Zero);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.That(typeof(LabPendingRoom).GetField(unsupported == AddressFamily.InterNetwork ? "ipv4" : "ipv6", fields)
                    .GetValue(pending), Is.Null);
                Assert.That(typeof(LabPendingRoom).GetField(unsupported == AddressFamily.InterNetwork ? "ipv6" : "ipv4", fields)
                    .GetValue(pending), Is.Not.Null);
            }
            finally { pending.Dispose(); }
        }

        [Test]
        public void ReservationDoesNotHidePermissionFailuresAsSingleFamilySuccess()
        {
            var pending = (LabPendingRoom)Activator.CreateInstance(typeof(LabPendingRoom), true);
            try
            {
                Func<AddressFamily, Socket> denied = _ => throw new SocketException((int)SocketError.AccessDenied);
                var error = Assert.Throws<TargetInvocationException>(() => Reserve(pending, denied));
                Assert.That(error.InnerException, Is.TypeOf<SocketException>());
                Assert.That(((SocketException)error.InnerException).SocketErrorCode, Is.EqualTo(SocketError.AccessDenied));
                Assert.That(pending.Port, Is.Zero);
            }
            finally { pending.Dispose(); }
        }

        private static void Reserve(LabPendingRoom pending, Func<AddressFamily, Socket> factory) =>
            typeof(LabPendingRoom).GetMethod("ReservePort", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pending, new object[] { factory });

        private static byte[] Record(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text), result = new byte[bytes.Length + 1];
            result[0] = (byte)bytes.Length;
            Buffer.BlockCopy(bytes, 0, result, 1, bytes.Length);
            return result;
        }
    }
}
