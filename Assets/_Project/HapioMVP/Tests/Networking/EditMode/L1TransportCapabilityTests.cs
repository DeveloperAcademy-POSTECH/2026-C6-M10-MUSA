using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace C6.Prototype.Networking.Tests
{
    /// <summary>Real local UDP sockets and stock UTP drivers; these are not gameplay/device tests.</summary>
    public sealed class L1TransportCapabilityTests
    {
        private const int PayloadBytes = 257;
        private const int MaximumSteps = 400;

        [TestCase(NetworkFamily.Ipv4)]
        [TestCase(NetworkFamily.Ipv6)]
        public void StockDriverExchangesReliablePayloadOnLoopback(NetworkFamily family)
        {
            RequireIpv6IfNeeded(family);
            var server = CreateDriver();
            try
            {
                Assert.That(server.Bind(Any(family)), Is.Zero);
                Assert.That(server.Listen(), Is.Zero);
                Assert.That(L1TransportEndpoint.TryParse(family == NetworkFamily.Ipv4 ? "127.0.0.1" : "::1",
                    server.GetLocalEndpoint().Port, out var target, out var error), Is.True, error);
                Assert.That(L1TransportEndpoint.GetScopeId(target), Is.Zero);
                ExchangePayload(ref server, target);
            }
            finally { server.Dispose(); }
        }

        [TestCase(NetworkFamily.Ipv4, NetworkFamily.Ipv6)]
        [TestCase(NetworkFamily.Ipv6, NetworkFamily.Ipv4)]
        public void StockListenerDoesNotAcceptTheOtherAddressFamily(NetworkFamily hostFamily, NetworkFamily clientFamily)
        {
            RequireIpv6IfNeeded(NetworkFamily.Ipv6);
            var server = CreateDriver();
            var client = CreateDriver();
            try
            {
                Assert.That(server.Bind(Any(hostFamily)), Is.Zero);
                Assert.That(server.Listen(), Is.Zero);
                Assert.That(client.Bind(Any(clientFamily)), Is.Zero);
                var connection = client.Connect(Loopback(clientFamily).WithPort(server.GetLocalEndpoint().Port));
                var accepted = 0;
                var connected = 0;
                var disconnected = 0;
                for (var step = 0; step < MaximumSteps; step++)
                {
                    client.ScheduleUpdate().Complete();
                    server.ScheduleUpdate().Complete();
                    while (server.Accept().IsCreated) accepted++;
                    DrainWithoutData(ref server, ref connected, ref disconnected);
                    DrainWithoutData(ref client, ref connected, ref disconnected);
                    if (client.GetConnectionState(connection) == NetworkConnection.State.Disconnected && disconnected > 0) break;
                    Thread.Sleep(1);
                }
                Assert.That(accepted, Is.Zero, "The selected stock listener accepted an unexpected address family.");
                Assert.That(connected, Is.Zero);
                Assert.That(disconnected, Is.GreaterThan(0), "No timeout event was observed; absence of data alone is not a complete negative test.");
            }
            finally { client.Dispose(); server.Dispose(); }
        }

        [Test]
        public void SeparateStockIpv4AndIpv6ListenersCanUseOnePortAndReply()
        {
            RequireIpv6IfNeeded(NetworkFamily.Ipv6);
            var ipv4 = CreateDriver();
            var ipv6 = CreateDriver();
            try
            {
                Assert.That(ipv4.Bind(NetworkEndpoint.AnyIpv4), Is.Zero);
                var port = ipv4.GetLocalEndpoint().Port;
                Assert.That(ipv6.Bind(NetworkEndpoint.AnyIpv6.WithPort(port)), Is.Zero);
                Assert.That(ipv4.Listen(), Is.Zero);
                Assert.That(ipv6.Listen(), Is.Zero);
                ExchangePayload(ref ipv4, NetworkEndpoint.LoopbackIpv4.WithPort(port));
                ExchangePayload(ref ipv6, NetworkEndpoint.LoopbackIpv6.WithPort(port));
                Assert.That(ipv4.GetLocalEndpoint().Port, Is.EqualTo(ipv6.GetLocalEndpoint().Port));
            }
            finally { ipv6.Dispose(); ipv4.Dispose(); }
        }

        [Test]
        public void NativeParserDoesNotPreserveNumericIpv6Zone()
        {
            Assert.That(NetworkEndpoint.TryParse("fe80::1234", 12345, out var bare, NetworkFamily.Ipv6), Is.True);
            Assert.That(L1TransportEndpoint.GetScopeId(bare), Is.Zero);
            var accepted = NetworkEndpoint.TryParse("fe80::1234%7", 12345, out var scoped, NetworkFamily.Ipv6);
            Assert.That(!accepted || L1TransportEndpoint.GetScopeId(scoped) == 0, Is.True,
                "Stock Encode now preserves scope: reconsider whether this diagnostic adapter is necessary.");
        }

        [Test]
        public void ScopedHelperPreservesInterfaceIndexPortAndEndpointIdentity()
        {
            Assert.That(L1TransportEndpoint.TryParse("fe80::1234%7", 12345, out var first, out var error), Is.True, error);
            Assert.That(L1TransportEndpoint.TryParse("fe80::1234%8", 12345, out var second, out error), Is.True, error);
            Assert.That(first.Port, Is.EqualTo(12345));
            Assert.That(first.Family, Is.EqualTo(NetworkFamily.Ipv6));
            Assert.That(L1TransportEndpoint.GetScopeId(first), Is.EqualTo(7));
            Assert.That(first.Equals(second), Is.False, "Different interfaces must not be the same UTP remote endpoint.");
            Assert.That(L1TransportEndpoint.TryParse(L1TransportEndpoint.GetAddress(first), first.Port,
                out var roundTrip, out error), Is.True, error);
            Assert.That(roundTrip, Is.EqualTo(first));
            Assert.That(L1TransportEndpoint.GetScopeId(L1TransportEndpoint.WithScope(first, uint.MaxValue)), Is.EqualTo(uint.MaxValue));
        }

        [TestCase("fe80::1234")]
        [TestCase("fe80::1234%0")]
        [TestCase("fe80::1234%-1")]
        [TestCase("fe80::1234%4294967296")]
        [TestCase("fe80::1234%en0")]
        [TestCase("fe80::1234%7%8")]
        [TestCase("127.0.0.1%7")]
        [TestCase("[::1]")]
        [TestCase("localhost")]
        [TestCase("https://example.invalid")]
        public void ScopedHelperRejectsMissingOrMalformedInterface(string address)
        {
            Assert.That(L1TransportEndpoint.TryParse(address, 12345, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void StockDriverExchangesDataUsingScopedLocalIpv6Interface()
        {
            RequireIpv6IfNeeded(NetworkFamily.Ipv6);
            var address = FindOwnLinkLocalAddress();
            if (address == null) Assert.Ignore("No local IPv6 link-local interface is available; scoped socket round trip is NOT_RUN.");
            var server = CreateDriver();
            try
            {
                Assert.That(server.Bind(NetworkEndpoint.AnyIpv6), Is.Zero);
                Assert.That(server.Listen(), Is.Zero);
                Assert.That(L1TransportEndpoint.TryParse(address, server.GetLocalEndpoint().Port,
                    out var target, out var error), Is.True, error);
                Assert.That(L1TransportEndpoint.GetScopeId(target), Is.GreaterThan(0));
                ExchangePayload(ref server, target);
            }
            finally { server.Dispose(); }
        }

        private static NetworkDriver CreateDriver()
        {
            var settings = new NetworkSettings(Allocator.Temp);
            try
            {
                // Fixed simulated time makes a rejected connection reach its stock timeout in
                // bounded steps. Every update still polls real kernel UDP sockets.
                settings.WithNetworkConfigParameters(connectTimeoutMS: 50, maxConnectAttempts: 10,
                    disconnectTimeoutMS: 10000, heartbeatTimeoutMS: 500, fixedFrameTimeMS: 10);
                return NetworkDriver.Create(new UDPNetworkInterface(), settings);
            }
            finally { settings.Dispose(); }
        }

        private static void ExchangePayload(ref NetworkDriver server, NetworkEndpoint target)
        {
            var client = CreateDriver();
            var serverPipeline = server.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            var clientPipeline = client.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            try
            {
                Assert.That(client.Bind(Any(target.Family)), Is.Zero);
                var connection = client.Connect(target);
                var accepted = 0;
                var requestSent = false;
                var requestCount = 0;
                var replyCount = 0;
                var settledSteps = 0;
                for (var step = 0; step < MaximumSteps; step++)
                {
                    client.ScheduleUpdate().Complete();
                    server.ScheduleUpdate().Complete();
                    while (server.Accept().IsCreated) accepted++;
                    NetworkEvent.Type kind;
                    while ((kind = server.PopEvent(out var peer, out var payload)) != NetworkEvent.Type.Empty)
                    {
                        Assert.That(kind, Is.Not.EqualTo(NetworkEvent.Type.Disconnect), "Server disconnected during local payload validation.");
                        if (kind != NetworkEvent.Type.Data) continue;
                        CheckPayload(payload, 29);
                        requestCount++;
                        WritePayload(ref server, serverPipeline, peer, 193);
                    }
                    while ((kind = client.PopEvent(out _, out var clientPayload)) != NetworkEvent.Type.Empty)
                    {
                        Assert.That(kind, Is.Not.EqualTo(NetworkEvent.Type.Disconnect), "Client disconnected during local payload validation.");
                        if (kind != NetworkEvent.Type.Data) continue;
                        CheckPayload(clientPayload, 193);
                        replyCount++;
                    }
                    if (!requestSent && client.GetConnectionState(connection) == NetworkConnection.State.Connected)
                    {
                        Assert.That(client.GetRemoteEndpoint(connection), Is.EqualTo(target), "UTP discarded or changed the selected endpoint/scope.");
                        WritePayload(ref client, clientPipeline, connection, 29);
                        requestSent = true;
                    }
                    if (replyCount > 0 && ++settledSteps >= 30) break;
                    Thread.Sleep(1);
                }
                Assert.That(accepted, Is.EqualTo(1));
                Assert.That(requestSent, Is.True);
                Assert.That(requestCount, Is.EqualTo(1), "Request was missing or duplicated.");
                Assert.That(replyCount, Is.EqualTo(1), "Reply was missing or duplicated.");
            }
            finally { client.Dispose(); }
        }

        private static void WritePayload(ref NetworkDriver driver, NetworkPipeline pipeline, NetworkConnection peer, int seed)
        {
            Assert.That(driver.BeginSend(pipeline, peer, out var writer, PayloadBytes), Is.Zero);
            for (var i = 0; i < PayloadBytes; i++) writer.WriteByte((byte)((i * 31 + seed) & 255));
            Assert.That(driver.EndSend(writer), Is.EqualTo(PayloadBytes));
        }

        private static void CheckPayload(DataStreamReader reader, int seed)
        {
            Assert.That(reader.Length, Is.EqualTo(PayloadBytes), "The UDP/UTP payload was truncated or extended.");
            for (var i = 0; i < PayloadBytes; i++) Assert.That(reader.ReadByte(), Is.EqualTo((byte)((i * 31 + seed) & 255)));
        }

        private static void DrainWithoutData(ref NetworkDriver driver, ref int connected, ref int disconnected)
        {
            NetworkEvent.Type kind;
            while ((kind = driver.PopEvent(out _, out _)) != NetworkEvent.Type.Empty)
            {
                Assert.That(kind, Is.Not.EqualTo(NetworkEvent.Type.Data));
                if (kind == NetworkEvent.Type.Connect) connected++;
                if (kind == NetworkEvent.Type.Disconnect) disconnected++;
            }
        }

        private static NetworkEndpoint Any(NetworkFamily family) => family == NetworkFamily.Ipv4 ? NetworkEndpoint.AnyIpv4 : NetworkEndpoint.AnyIpv6;
        private static NetworkEndpoint Loopback(NetworkFamily family) => family == NetworkFamily.Ipv4 ? NetworkEndpoint.LoopbackIpv4 : NetworkEndpoint.LoopbackIpv6;

        private static void RequireIpv6IfNeeded(NetworkFamily family)
        {
            if (family == NetworkFamily.Ipv6 && !Socket.OSSupportsIPv6)
                Assert.Ignore("This OS does not expose IPv6; IPv6 socket validation is NOT_RUN.");
        }

        private static string FindOwnLinkLocalAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
                var properties = networkInterface.GetIPProperties();
                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetworkV6 || !unicast.Address.IsIPv6LinkLocal) continue;
                    var index = unicast.Address.ScopeId;
                    if (index == 0) index = properties.GetIPv6Properties()?.Index ?? 0;
                    if (index <= 0 || index > uint.MaxValue) continue;
                    return new IPAddress(unicast.Address.GetAddressBytes(), index).ToString();
                }
            }
            return null;
        }
    }
}
