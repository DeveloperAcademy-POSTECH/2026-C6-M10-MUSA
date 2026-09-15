using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;
using NetEvent = Unity.Netcode.NetworkEvent;

namespace C6.Prototype.Networking.Tests
{
    /// <summary>Real kernel UDP and NGO transport pipelines, without a gameplay admission bypass.</summary>
    public sealed class DualStackTransportTests
    {
        [TestCase("127.0.0.1", 0u)]
        [TestCase("::1", 0u)]
        [TestCase("fe80::1234%7", 7u)]
        [TestCase("fe80::1234%4294967295", uint.MaxValue)]
        public void SocketEndpointRoundTripRetainsIdentityAndScope(string address, uint scope)
        {
            Assert.That(DualStackEndpoint.TryParse(address, 24567, out var endpoint, out var error), Is.True, error);
            var restored = DualStackEndpoint.FromIPEndPoint(DualStackEndpoint.ToIPEndPoint(endpoint));
            Assert.That(restored, Is.EqualTo(endpoint));
            Assert.That(restored.Port, Is.EqualTo(24567));
            Assert.That(DualStackEndpoint.GetScopeId(restored), Is.EqualTo(scope));
            if (scope != 0)
            {
                Assert.That(DualStackEndpoint.TryParse("fe80::1234%6", 24567, out var other, out _), Is.True);
                Assert.That(restored, Is.Not.EqualTo(other));
            }
        }

        [Test]
        public void SocketHostRejectsUnicastAndCanBindAfterFailure()
        {
            var net = new DualStackUdpNetworkInterface();
            var settings = new NetworkSettings(Allocator.Temp);
            int padding = 0;
            try
            {
                Assert.That(net.Initialize(ref settings, ref padding), Is.Zero);
                Assert.That(net.Bind(NetworkEndpoint.LoopbackIpv4), Is.Not.Zero);
                Assert.That(net.HasIpv4Socket || net.HasIpv6Socket, Is.False);
                Assert.That(net.Listen(), Is.Not.Zero);
                Assert.That(net.Bind(NetworkEndpoint.AnyIpv4), Is.Zero);
                Assert.That(net.LocalEndpoint.Port, Is.GreaterThan(0));
            }
            finally { net.Dispose(); net.Dispose(); settings.Dispose(); }
        }

        [Test]
        public void SocketHostDoesNotStealAnOccupiedPortAndDisposesBothFamilies()
        {
            RequireIpv6();
            var first = new DualStackUdpNetworkInterface();
            var second = new DualStackUdpNetworkInterface();
            var settings = new NetworkSettings(Allocator.Temp);
            int padding = 0;
            try
            {
                first.Initialize(ref settings, ref padding); second.Initialize(ref settings, ref padding);
                Assert.That(first.Bind(NetworkEndpoint.AnyIpv4), Is.Zero);
                var endpoint = NetworkEndpoint.AnyIpv4.WithPort(first.LocalEndpoint.Port);
                Assert.That(first.HasIpv4Socket && first.HasIpv6Socket, Is.True);
                Assert.That(second.Bind(endpoint), Is.Not.Zero);
                Assert.That(second.HasIpv4Socket || second.HasIpv6Socket, Is.False);
                first.Dispose();
                Assert.That(second.Bind(endpoint), Is.Zero, "Disposal must release both family sockets.");
                Assert.That(second.HasIpv4Socket && second.HasIpv6Socket, Is.True);
            }
            finally { first.Dispose(); second.Dispose(); settings.Dispose(); }
        }

        [Test]
        public void NgoFactoryUsesInstanceDispatchWithoutReplacingStaticConstructor()
        {
            var go = new GameObject("L2 Factory Verification");
            NetworkDriver driver = default;
            var previous = UnityTransport.s_DriverConstructor;
            try
            {
                Assert.That(previous, Is.Null, "Another transport factory is active in the test environment.");
                var transport = go.AddComponent<DualStackUnityTransport>();
                transport.ConfigureHost(24569);
                Assert.That(transport.DriverConstructor, Is.SameAs(transport));
                transport.DriverConstructor.CreateDriver(transport, out driver, out var unreliable, out var sequenced, out var reliable);
                Assert.That(unreliable, Is.Not.EqualTo(default(NetworkPipeline)));
                Assert.That(sequenced, Is.Not.EqualTo(default(NetworkPipeline)));
                Assert.That(reliable, Is.Not.EqualTo(default(NetworkPipeline)));
                Assert.That(driver.Bind(NetworkEndpoint.AnyIpv4), Is.Zero);
                Assert.That(driver.Listen(), Is.Zero);
                Assert.That(UnityTransport.s_DriverConstructor, Is.SameAs(previous));
            }
            finally { if (driver.IsCreated) driver.Dispose(); Object.DestroyImmediate(go); }
        }

        [Test]
        public void ExternalFactoryConflictIsRejectedWithoutChangingItsOwner()
        {
            var go = new GameObject("L2 Factory Conflict");
            var previous = UnityTransport.s_DriverConstructor;
            try
            {
                var transport = go.AddComponent<DualStackUnityTransport>();
                transport.ConfigureHost(24570);
                var foreign = new ForeignFactory();
                UnityTransport.s_DriverConstructor = foreign;
                Assert.That(transport.StartServer(), Is.False);
                Assert.That(transport.LastFailureCode, Is.EqualTo("DRIVER_CONSTRUCTOR_CONFLICT"));
                Assert.That(UnityTransport.s_DriverConstructor, Is.SameAs(foreign));
            }
            finally { UnityTransport.s_DriverConstructor = previous; Object.DestroyImmediate(go); }
        }

        [Test]
        public void OneNgoHostExchangesOrderedLargeReliableMessagesWithIpv4AndIpv6Together()
        { RequireIpv6(); ExchangeNgo(new[] { "127.0.0.1", "::1" }); }

        [Test]
        public void OneNgoHostExchangesWithScopedIpv6AndIpv4Together()
        {
            RequireIpv6();
            var address = FindOwnLinkLocal();
            if (address == null) Assert.Ignore("No local IPv6 link-local interface; scoped network round trip is NOT_RUN.");
            ExchangeNgo(new[] { "127.0.0.1", address });
        }

        static void ExchangeNgo(string[] addresses)
        {
            var objects = new List<GameObject>();
            var all = new List<DualStackUnityTransport>();
            var hostCounts = new Dictionary<ulong, int>();
            var received = new int[addresses.Length];
            var connected = new bool[addresses.Length];
            var sent = new bool[addresses.Length];
            var errors = new List<string>();
            bool cleaning = false;
            try
            {
                var host = MakeTransport(objects, all);
                ushort port = AvailablePort();
                host.ConfigureHost(port);
                host.OnTransportEvent += (kind, id, payload, time) =>
                {
                    if (cleaning) return;
                    if (kind == NetEvent.Connect) hostCounts.Add(id, 0);
                    if (kind == NetEvent.Disconnect || kind == NetEvent.TransportFailure) errors.Add("Host " + kind);
                    if (kind != NetEvent.Data) return;
                    Assert.That(payload.Count, Is.EqualTo(12000));
                    int ordinal = hostCounts[id]++;
                    CheckPayload(payload, ordinal);
                    host.Send(id, new ArraySegment<byte>(payload.ToArray()), NetworkDelivery.ReliableSequenced);
                };
                Assert.That(host.StartServer(), Is.True, host.LastFailureCode);
                Assert.That(host.HasIpv4Listener && host.HasIpv6Listener, Is.True);
                for (int i = 0; i < addresses.Length; i++)
                {
                    int index = i;
                    var client = MakeTransport(objects, all);
                    client.ConfigureClient(addresses[i], port);
                    client.OnTransportEvent += (kind, id, payload, time) =>
                    {
                        if (cleaning) return;
                        if (kind == NetEvent.Connect) connected[index] = true;
                        if (kind == NetEvent.Disconnect || kind == NetEvent.TransportFailure) errors.Add("Client " + kind);
                        if (kind == NetEvent.Data) CheckPayload(payload, received[index]++);
                    };
                    Assert.That(client.StartClient(), Is.True, client.LastFailureCode);
                }
                var deadline = DateTime.UtcNow.AddSeconds(8);
                int settled = 0;
                while (DateTime.UtcNow < deadline)
                {
                    foreach (var transport in all) transport.EarlyUpdate();
                    for (int i = 0; i < addresses.Length; i++)
                    {
                        if (!connected[i] || sent[i]) continue;
                        for (int ordinal = 0; ordinal < 3; ordinal++)
                            all[i + 1].Send(all[i + 1].ServerClientId, new ArraySegment<byte>(Payload(ordinal)), NetworkDelivery.ReliableSequenced);
                        sent[i] = true;
                    }
                    foreach (var transport in all) transport.PostLateUpdate();
                    Assert.That(errors, Is.Empty);
                    if (received.All(count => count == 3) && ++settled >= 30) break;
                    Thread.Sleep(1);
                }
                Assert.That(hostCounts.Count, Is.EqualTo(addresses.Length));
                Assert.That(hostCounts.Values.All(count => count == 3), Is.True);
                Assert.That(received, Is.All.EqualTo(3));
                Assert.That(UnityTransport.s_DriverConstructor, Is.Null);
            }
            finally
            {
                cleaning = true;
                foreach (var transport in all) transport.Shutdown();
                foreach (var go in objects) Object.DestroyImmediate(go);
            }
        }

        static DualStackUnityTransport MakeTransport(List<GameObject> objects, List<DualStackUnityTransport> transports)
        {
            var go = new GameObject("L2 Real UDP Transport"); objects.Add(go);
            var transport = go.AddComponent<DualStackUnityTransport>(); transports.Add(transport);
            transport.Initialize(); transport.ConnectTimeoutMS = 100; transport.MaxConnectAttempts = 30;
            return transport;
        }
        static byte[] Payload(int ordinal) => Enumerable.Range(0, 12000).Select(i => (byte)((i * 31 + ordinal) & 255)).ToArray();
        static void CheckPayload(ArraySegment<byte> value, int ordinal)
        {
            Assert.That(value.Count, Is.EqualTo(12000));
            Assert.That(value.ToArray(), Is.EqualTo(Payload(ordinal)), "Reliable payload or sequence changed.");
        }
        static ushort AvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            { socket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); return (ushort)((IPEndPoint)socket.LocalEndPoint).Port; }
        }
        static void RequireIpv6()
        { if (!Socket.OSSupportsIPv6) Assert.Ignore("IPv6 unavailable; actual IPv6 verification is NOT_RUN."); }
        static string FindOwnLinkLocal()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                var properties = nic.GetIPProperties();
                foreach (var entry in properties.UnicastAddresses)
                {
                    var address = entry.Address;
                    if (address.AddressFamily != AddressFamily.InterNetworkV6 || !address.IsIPv6LinkLocal) continue;
                    long scope = address.ScopeId != 0 ? address.ScopeId : properties.GetIPv6Properties()?.Index ?? 0;
                    if (scope > 0 && scope <= uint.MaxValue) return new IPAddress(address.GetAddressBytes(), scope).ToString();
                }
            }
            return null;
        }
        sealed class ForeignFactory : INetworkStreamDriverConstructor
        {
            public void CreateDriver(UnityTransport transport, out NetworkDriver driver, out NetworkPipeline first,
                out NetworkPipeline second, out NetworkPipeline third) => throw new InvalidOperationException("Must not be invoked.");
        }
    }
}
