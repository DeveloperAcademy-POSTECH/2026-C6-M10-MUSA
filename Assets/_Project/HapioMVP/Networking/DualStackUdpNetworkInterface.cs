using System;
using System.Net;
using System.Net.Sockets;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEngine;

namespace C6.Prototype.Networking
{
    /// <summary>
    /// One UTP connection table over IPv4 and IPv6 UDP sockets. WrapToUnmanaged is UTP's
    /// public adapter for managed struct interfaces. UTP retains all reliability/fragmentation.
    /// The Host path targets macOS/iOS; WebGL and other targets are outside this validation scope.
    /// </summary>
    public struct DualStackUdpNetworkInterface : INetworkInterface
    {
        State state;
        // Construct shared state before wrapping so the transport can inspect listener families.
        public DualStackUdpNetworkInterface(bool initializeState) { state = new State(); }
        public bool HasIpv4Socket => state != null && state.Ipv4 != null;
        public bool HasIpv6Socket => state != null && state.Ipv6 != null;
        public int DroppedDatagrams => state?.Dropped ?? 0;
        public NetworkEndpoint LocalEndpoint => state?.Local ?? default;

        public int Initialize(ref NetworkSettings settings, ref int packetPadding)
        {
            DualStackEndpoint.ValidateEnvironment();
            if (state == null) state = new State();
            state.Budget = Math.Min(512, Math.Max(1, settings.GetNetworkConfigParameters().receiveQueueCapacity));
            return state.Disposed ? -1 : 0;
        }
        public int Bind(NetworkEndpoint endpoint) => state?.Bind(endpoint) ?? -1;
        public int Listen() => HasIpv4Socket || HasIpv6Socket ? 0 : -1;
        public void Dispose() => state?.Dispose();
        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            dependency.Complete();
            state?.Receive(ref arguments);
            return default;
        }
        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency)
        {
            dependency.Complete();
            state?.Send(ref arguments);
            return default;
        }

        sealed class State : IDisposable
        {
            const int DatagramCapacity = 65536;
            readonly byte[] receiveBuffer = new byte[DatagramCapacity];
            readonly byte[] sendBuffer = new byte[DatagramCapacity];
            internal Socket Ipv4, Ipv6;
            internal NetworkEndpoint Local;
            internal int Budget = 128, Dropped;
            internal bool Disposed;
            bool nextIpv6, loggedIoError;

            internal int Bind(NetworkEndpoint endpoint)
            {
                if (Disposed || Ipv4 != null || Ipv6 != null) return -1;
                if (endpoint != NetworkEndpoint.AnyIpv4.WithPort(endpoint.Port) &&
                    endpoint != NetworkEndpoint.AnyIpv6.WithPort(endpoint.Port)) return -1;
                try
                {
                    Ipv4 = Open(AddressFamily.InterNetwork, endpoint.Port);
                    int port = Ipv4 == null ? endpoint.Port : ((IPEndPoint)Ipv4.LocalEndPoint).Port;
                    Ipv6 = Open(AddressFamily.InterNetworkV6, port);
                    if (Ipv4 == null && Ipv6 == null) return -1;
                    Local = DualStackEndpoint.FromIPEndPoint((IPEndPoint)(Ipv4 ?? Ipv6).LocalEndPoint);
                    Debug.Log("C6_L2_UDP_BIND backend=managed_socket ipv4=" + (Ipv4 != null) + " ipv6=" + (Ipv6 != null));
                    return 0;
                }
                catch (Exception error)
                {
                    CloseSockets();
                    Debug.LogWarning("C6_L2_UDP_BIND_FAILED error=" + ErrorName(error));
                    return -1;
                }
            }

            static Socket Open(AddressFamily family, int port)
            {
                Socket socket = null;
                try
                {
                    socket = new Socket(family, SocketType.Dgram, ProtocolType.Udp);
                    if (family == AddressFamily.InterNetworkV6) socket.DualMode = false;
                    socket.Blocking = false;
                    socket.ReceiveBufferSize = 256 * 1024;
                    socket.SendBufferSize = 256 * 1024;
                    // No port reuse: another Host already using either family must fail visibly.
                    socket.Bind(new IPEndPoint(family == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, port));
                    return socket;
                }
                catch (SocketException error)
                {
                    socket?.Dispose();
                    if (error.SocketErrorCode == SocketError.AddressFamilyNotSupported ||
                        error.SocketErrorCode == SocketError.ProtocolNotSupported || error.SocketErrorCode == SocketError.SocketNotSupported)
                    {
                        Debug.LogWarning("C6_L2_UDP_FAMILY_UNAVAILABLE family=" + family + " error=" + error.SocketErrorCode);
                        return null;
                    }
                    throw;
                }
                catch { socket?.Dispose(); throw; }
            }

            internal unsafe void Receive(ref ReceiveJobArguments arguments)
            {
                if (Disposed) return;
                int idle = 0;
                for (int attempt = 0; attempt < Budget && idle < 2; attempt++)
                {
                    nextIpv6 = !nextIpv6;
                    var socket = nextIpv6 ? Ipv6 : Ipv4;
                    if (socket == null) { idle++; continue; }
                    EndPoint sender = new IPEndPoint(nextIpv6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                    int count;
                    try { count = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref sender); }
                    catch (SocketException error)
                    {
                        if (!Temporary(error.SocketErrorCode)) ReportIo(error);
                        idle++; continue;
                    }
                    catch (ObjectDisposedException) { return; }
                    idle = 0;
                    NetworkEndpoint remote;
                    try { remote = DualStackEndpoint.FromIPEndPoint((IPEndPoint)sender); }
                    catch (ArgumentException) { Dropped++; continue; }
                    if (count == 0 || !arguments.ReceiveQueue.EnqueuePacket(out var packet)) { Dropped++; continue; }
                    // Read the complete datagram first so oversized input is rejected, never silently truncated into UTP.
                    if (count > packet.BytesAvailableAtEnd) { packet.Drop(); Dropped++; continue; }
                    packet.EndpointRef = remote;
                    fixed (byte* bytes = receiveBuffer) packet.AppendToPayload(bytes, count);
                }
            }

            internal unsafe void Send(ref SendJobArguments arguments)
            {
                if (Disposed) return;
                for (int i = 0; i < arguments.SendQueue.Count; i++)
                {
                    var packet = arguments.SendQueue[i];
                    if (packet.Length <= 0) continue;
                    var socket = packet.EndpointRef.Family == NetworkFamily.Ipv4 ? Ipv4 : Ipv6;
                    if (socket == null || packet.Length > sendBuffer.Length) { packet.Drop(); Dropped++; continue; }
                    int length = packet.Length;
                    fixed (byte* bytes = sendBuffer) packet.CopyPayload(bytes, sendBuffer.Length);
                    try
                    {
                        var endpoint = DualStackEndpoint.ToIPEndPoint(packet.EndpointRef);
                        if (socket.SendTo(sendBuffer, 0, length, SocketFlags.None, endpoint) != length) Dropped++;
                    }
                    catch (SocketException error)
                    { Dropped++; if (!Temporary(error.SocketErrorCode)) ReportIo(error); }
                    catch (ObjectDisposedException) { Dropped++; }
                    catch (ArgumentException) { Dropped++; }
                    // Local backpressure is bounded packet loss. The unchanged reliable UTP stage owns retransmission.
                    packet.Drop();
                }
            }

            static bool Temporary(SocketError error) => error == SocketError.WouldBlock || error == SocketError.IOPending ||
                error == SocketError.NoBufferSpaceAvailable || error == SocketError.Interrupted;
            static string ErrorName(Exception error) => error is SocketException socket ? socket.SocketErrorCode.ToString() : error.GetType().Name;
            void ReportIo(Exception error)
            {
                if (loggedIoError) return;
                loggedIoError = true;
                Debug.LogWarning("C6_L2_UDP_IO error=" + ErrorName(error) + " action=UTP_TIMEOUT_OR_RETRY");
            }
            void CloseSockets()
            {
                Ipv4?.Dispose(); Ipv6?.Dispose();
                Ipv4 = Ipv6 = null; Local = default;
            }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true; CloseSockets();
            }
        }
    }
}
