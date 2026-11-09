using System.Collections.Generic;
using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>#557: which local IPv4 addresses a Host shows for Direct IP. Real iPhone interfaces are a device check.</summary>
    public sealed class LabLocalAddressesTests
    {
        private const uint Live = LabLocalAddresses.InterfaceUp | LabLocalAddresses.InterfaceRunning;
        private static readonly byte[] Lan = { 192, 168, 0, 12 };

        [TestCase("en0"), TestCase("en5"), TestCase("bridge100"), TestCase("ap1"), TestCase("somethingNew0")]
        public void ReachableInterfacesAreShown(string name) => Assert.That(LabLocalAddresses.IsShown(name, Live, Lan), Is.True);

        [TestCase("pdp_ip0"), TestCase("utun3"), TestCase("ipsec0"), TestCase("awdl0"), TestCase("llw0"), TestCase("anpi0")]
        public void CellularVpnAndPeerLinksAreHidden(string name) => Assert.That(LabLocalAddresses.IsShown(name, Live, Lan), Is.False);

        [Test]
        public void DownLoopbackAndUnusableAddressesAreHidden()
        {
            Assert.That(LabLocalAddresses.IsShown("en0", LabLocalAddresses.InterfaceUp, Lan), Is.False);
            Assert.That(LabLocalAddresses.IsShown("en0", LabLocalAddresses.InterfaceRunning, Lan), Is.False);
            Assert.That(LabLocalAddresses.IsShown("lo0", Live | LabLocalAddresses.InterfaceLoopback, new byte[] { 127, 0, 0, 1 }), Is.False);
            Assert.That(LabLocalAddresses.IsShown("en0", Live, new byte[] { 127, 0, 0, 1 }), Is.False);
            Assert.That(LabLocalAddresses.IsShown("en0", Live, new byte[] { 169, 254, 3, 4 }), Is.False);
            Assert.That(LabLocalAddresses.IsShown("en0", Live, new byte[] { 0, 0, 0, 0 }), Is.False);
            Assert.That(LabLocalAddresses.IsShown("en0", Live, null), Is.False);
        }

        [Test]
        public void WifiAndEthernetComeBeforeHotspotAndEachAddressOnce()
        {
            string[] arranged = LabLocalAddresses.Arrange(new[]
            {
                new KeyValuePair<string, string>("other9", "10.0.0.9"),
                new KeyValuePair<string, string>("bridge100", "172.20.10.1"),
                new KeyValuePair<string, string>("en5", "192.168.0.20"),
                new KeyValuePair<string, string>("en0", "192.168.0.12"),
                new KeyValuePair<string, string>("en1", "192.168.0.12"),
            });
            Assert.That(arranged, Is.EqualTo(new[] { "192.168.0.12", "192.168.0.20", "172.20.10.1", "10.0.0.9" }));
        }

        [Test]
        public void ReadingThisMachineReturnsOnlyWellFormedAddresses()
        {
            foreach (string address in LabLocalAddresses.Read())
                Assert.That(address, Does.Match(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"));
        }
    }
}
