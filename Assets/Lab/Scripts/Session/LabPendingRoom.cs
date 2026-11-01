using System;
using System.Net;
using System.Net.Sockets;
using System.Globalization;

namespace C6Lab
{
    /// <summary>A creator-owned advertised room, independent of the one active game connection.</summary>
    public sealed class LabPendingRoom : IDisposable
    {
        private LabBonjourDiscovery advertisement;
        private Socket ipv4, ipv6;
        private bool registrationStarted;
        private int candidateNumber = 1, registrationAttempts;
        public int Capacity { get; private set; }
        /// <summary>A 2:2 team territory room (#281), advertised before its creator enters.</summary>
        public bool Team { get; private set; }
        public LabRoomFamily Family { get; }
        public ushort Port { get; private set; }
        public string Name => advertisement?.RegisteredServiceName ?? string.Empty;
        public string Key => LabRoomCatalog.Key(Name, Port);
        public bool Ready => advertisement != null && advertisement.IsAdvertising && advertisement.AdvertisingConfirmed;
        public bool Failed => registrationStarted && (advertisement == null || !advertisement.IsAdvertising);

        private LabPendingRoom() { Family = LabRoomFamily.Lab; }
        private LabPendingRoom(LabRoomFamily family) { Family = family; }

        public static bool TryCreate(int capacity, out LabPendingRoom room, out string error)
            => TryCreate(capacity, LabRoomFamily.Lab, out room, out error);

        public static bool TryCreate(int capacity, LabRoomFamily family, out LabPendingRoom room, out string error)
            => TryCreate(capacity, family, false, out room, out error);

        public static bool TryCreate(int capacity, LabRoomFamily family, bool team, out LabPendingRoom room, out string error)
        {
            room = null; error = string.Empty;
            if (!LabModel.ValidRoomSize(capacity, team)) { error = "함께할 인원을 골라주시오."; return false; }
            if (!Enum.IsDefined(typeof(LabRoomFamily), family)) { error = "방 종류를 확인해주시오."; return false; }
            if (!LabBonjourDiscovery.IsSupported) { error = "이 기기에서는 방을 만들 수 없소."; return false; }
            var pending = new LabPendingRoom(family) { Capacity = capacity, Team = team };
            try
            {
                pending.ReservePort();
                pending.advertisement = new LabBonjourDiscovery(family) { Team = team };
                room = pending;
                return true;
            }
            catch (Exception)
            {
                pending.Dispose();
                error = "방을 만들지 못했소. 네트워크 권한을 확인하고 다시 눌러주시오.";
                return false;
            }
        }

        private void ReservePort(Func<AddressFamily, Socket> createSocket = null)
        {
            // Keep each advertised port exclusively reserved until its creator enters.
            // No game sockets, NetworkManagers or player slots are shared between room cards.
            for (int attempt = 0; attempt < 16; attempt++)
            {
                try
                {
                    ipv4 = OpenReservation(AddressFamily.InterNetwork, 0, createSocket);
                    int requestedPort = ipv4 != null ? ((IPEndPoint)ipv4.LocalEndPoint).Port : 0;
                    ipv6 = OpenReservation(AddressFamily.InterNetworkV6, requestedPort, createSocket);
                    if (ipv4 == null && ipv6 == null) throw new InvalidOperationException("No supported room address family.");
                    Port = checked((ushort)((IPEndPoint)(ipv4 ?? ipv6).LocalEndPoint).Port);
                    return;
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                { ReleasePort(); }
            }
            throw new InvalidOperationException("No free room port.");
        }

        private static Socket OpenReservation(AddressFamily family, int port, Func<AddressFamily, Socket> createSocket)
        {
            Socket socket = null;
            try
            {
                socket = createSocket != null ? createSocket(family)
                    : new Socket(family, SocketType.Dgram, ProtocolType.Udp);
                if (family == AddressFamily.InterNetworkV6) socket.DualMode = false;
                socket.Bind(new IPEndPoint(family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, port));
                return socket;
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressFamilyNotSupported
                || error.SocketErrorCode == SocketError.ProtocolNotSupported || error.SocketErrorCode == SocketError.SocketNotSupported)
            { socket?.Dispose(); return null; }
            catch { socket?.Dispose(); throw; }
        }

        public void Tick(bool allowRegistration = true, int firstNumber = 1)
        {
            if (!registrationStarted && allowRegistration && advertisement != null)
            {
                registrationStarted = true;
                candidateNumber = Math.Max(candidateNumber, firstNumber);
                string requestedName = LabRoomNames.ServiceBaseFor(Family) + (candidateNumber == 1 ? string.Empty
                    : " (" + candidateNumber.ToString(CultureInfo.InvariantCulture) + ")");
                bool accepted = advertisement.StartAdvertise(requestedName, Port, ipv4 != null, ipv6 != null, 1, Capacity, false, true, false);
                // Local owners are known immediately and did not spend a native conflict attempt.
                if (!advertisement.LocalRegistrationNameConflict) registrationAttempts++;
                if (!accepted && !RetryNameConflict()) { Dispose(); return; }
            }
            advertisement?.Tick();
            RetryNameConflict();
        }

        private bool RetryNameConflict()
        {
            if (!registrationStarted || advertisement == null || !advertisement.RegistrationNameConflict
                || registrationAttempts >= LabBonjourDiscovery.MaximumServices || candidateNumber == int.MaxValue) return false;
            registrationStarted = false;
            candidateNumber++;
            return true;
        }

        public bool EnterAsCreator(LabNetwork network)
        {
            if (network == null || Family != network.RoomFamily || !Ready || !network.CanChooseConnection) return false;
            ReleasePort();
            if (!network.HostRegisteredRoom(advertisement, Port, Capacity)) { Dispose(); return false; }
            // Transfer the original registration, preserving its confirmed name and join key.
            advertisement = null;
            return true;
        }

        private void ReleasePort()
        {
            ipv4?.Dispose(); ipv4 = null;
            ipv6?.Dispose(); ipv6 = null;
        }

        public void Dispose()
        {
            advertisement?.Dispose(); advertisement = null;
            ReleasePort();
        }
    }
}
