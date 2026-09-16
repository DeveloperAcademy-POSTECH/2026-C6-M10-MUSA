using System;
using System.Net;
using System.Runtime.InteropServices;
using C6.Prototype.Lobby.Discovery;
using NUnit.Framework;

namespace C6.Prototype.Lobby.Tests
{
    public sealed class L1BonjourProbeTests
    {
        [TestCase("192.168.10.4", 0, 0U, "192.168.10.4")]
        [TestCase("192.0.0.2", 0, 0U, "192.0.0.2")]
        [TestCase("fd12:3456::4", 0, 8U, "fd12:3456::4")]
        [TestCase("fe80::4", 8, 12U, "fe80::4%8")]
        [TestCase("fe80::4", 0, 12U, "fe80::4%12")]
        public void ProbeDecodesBothFamiliesAndKeepsDarwinRoutingScope(string input, int scope, uint callbackInterface, string expected)
        {
            IntPtr pointer = SocketAddress(input, scope);
            try
            {
                Assert.That(L1BonjourAddressCodec.TryDecode(pointer, callbackInterface, out string actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }

        [TestCase("0.0.0.0", 0, 0U)]
        [TestCase("127.0.0.1", 0, 0U)]
        [TestCase("224.0.0.251", 0, 0U)]
        [TestCase("::", 0, 0U)]
        [TestCase("::1", 0, 0U)]
        [TestCase("ff02::1", 0, 8U)]
        [TestCase("::ffff:192.168.10.4", 0, 0U)]
        [TestCase("fe80::4", 0, 0U)]
        [TestCase("fe80::4", 0, 0xffffffffU)]
        public void ProbeDoesNotPublishUnusableUnicastRoutes(string input, int scope, uint callbackInterface)
        {
            IntPtr pointer = SocketAddress(input, scope);
            try { Assert.That(L1BonjourAddressCodec.TryDecode(pointer, callbackInterface, out _), Is.False); }
            finally { Marshal.FreeHGlobal(pointer); }
        }

        [Test]
        public void TruncatedAndUnknownDarwinSocketAddressesAreRejected()
        {
            IntPtr pointer = SocketAddress("fd12::4", 0);
            try
            {
                Marshal.WriteByte(pointer, 0, 27);
                Assert.That(L1BonjourAddressCodec.TryDecode(pointer, 8, out _), Is.False);
                Marshal.WriteByte(pointer, 0, 28); Marshal.WriteByte(pointer, 1, 99);
                Assert.That(L1BonjourAddressCodec.TryDecode(pointer, 8, out _), Is.False);
                Assert.That(L1BonjourAddressCodec.TryDecode(IntPtr.Zero, 8, out _), Is.False);
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }

        [Test]
        public void IdleProbeOwnsNoNativeHandlesAndDisposeRejectsReuse()
        {
            var probe = new L1BonjourDiscovery(); probe.Tick(0);
            Assert.That(probe.ActiveNativeOperations, Is.Zero);
            Assert.That(probe.Targets, Is.Empty);
            probe.Dispose(); probe.Dispose();
            Assert.Throws<ObjectDisposedException>(() => probe.Tick(1));
            Assert.Throws<ObjectDisposedException>(() => probe.StartBrowse());
        }

        [Test]
        public void MonotonicProbeDeadlineCannotBeExtendedByClockRollback()
        {
            using (var probe = new L1BonjourDiscovery())
            {
                probe.Tick(10);
                Assert.Throws<ArgumentOutOfRangeException>(() => probe.Tick(9));
                Assert.Throws<ArgumentOutOfRangeException>(() => probe.Tick(double.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => probe.Tick(double.PositiveInfinity));
            }
        }

        [Test]
        public void ProbeServiceCannotBeConfusedWithProductionGameAdvertisement()
        {
            Assert.That(L1BonjourDiscovery.ServiceType, Is.EqualTo("_c6l1._udp"));
            Assert.That(L1BonjourDiscovery.ServiceType, Is.Not.EqualTo(BonjourRoomDiscovery.ServiceType));
        }

        private static IntPtr SocketAddress(string input, int scope)
        {
            byte[] address = IPAddress.Parse(input).GetAddressBytes(); bool ipv6 = address.Length == 16;
            var bytes = new byte[28]; bytes[0] = (byte)(ipv6 ? 28 : 16); bytes[1] = (byte)(ipv6 ? 30 : 2);
            Array.Copy(address, 0, bytes, ipv6 ? 8 : 4, address.Length);
            Array.Copy(BitConverter.GetBytes(scope), 0, bytes, 24, 4);
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length); Marshal.Copy(bytes, 0, pointer, bytes.Length); return pointer;
        }
    }
}
