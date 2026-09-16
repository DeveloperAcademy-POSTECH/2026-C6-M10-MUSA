using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Networking.Tests
{
    public sealed class DualStackLifecyclePlayTests
    {
        [UnityTest]
        public IEnumerator SameTransportCanStopAndRebindBothFamiliesWithoutDestroyingItsOwner()
        {
            if (!Socket.OSSupportsIPv6) Assert.Ignore("IPv6 unavailable; dual-family lifecycle is NOT_RUN.");
            ushort port;
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                port = (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
            }
            var go = new GameObject("L2 Host Lifecycle");
            var transport = go.AddComponent<DualStackUnityTransport>();
            try
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    transport.Initialize();
                    transport.ConfigureHost(port);
                    Assert.That(transport.StartServer(), Is.True, transport.LastFailureCode);
                    Assert.That(transport.HasIpv4Listener && transport.HasIpv6Listener, Is.True);
                    transport.EarlyUpdate(); transport.PostLateUpdate();
                    yield return null;
                    transport.Shutdown();
                    Assert.That(transport.HasIpv4Listener || transport.HasIpv6Listener, Is.False);
                    yield return null;
                }
            }
            finally { transport.Shutdown(); Object.DestroyImmediate(go); }
        }
    }
}
