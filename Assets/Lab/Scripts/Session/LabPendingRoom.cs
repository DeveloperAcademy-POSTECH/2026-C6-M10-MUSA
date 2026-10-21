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
        public ushort Port { get; private set; }
        public string Name => advertisement?.RegisteredServiceName ?? string.Empty;
        public string Key => LabRoomCatalog.Key(Name, Port);
        public bool Ready => advertisement != null && advertisement.IsAdvertising && advertisement.AdvertisingConfirmed;
        public bool Failed => registrationStarted && (advertisement == null || !advertisement.IsAdvertising);

        private LabPendingRoom() { }

        public static bool TryCreate(int capacity, out LabPendingRoom room, out string error)
        {
            room = null; error = string.Empty;
            if (capacity < 3 || capacity > 5) { error = "함께할 인원을 골라주시오."; return false; }
            if (!LabBonjourDiscovery.IsSupported) { error = "이 기기에서는 방을 만들 수 없소."; return false; }
            var pending = new LabPendingRoom { Capacity = capacity };
            try
            {
                pending.ReservePort();
                pending.advertisement = new LabBonjourDiscovery();
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

        private void ReservePort()
        {
            // Keep each advertised port exclusively reserved until its creator enters.
            // No game sockets, NetworkManagers or player slots are shared between room cards.
            for (int attempt = 0; attempt < 16; attempt++)
            {
                try
                {
                    ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    Port = checked((ushort)((IPEndPoint)ipv4.LocalEndPoint).Port);
                    if (Socket.OSSupportsIPv6)
                    {
                        ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                        ipv6.DualMode = false;
                        ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));
                    }
                    return;
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                { ReleasePort(); }
            }
            throw new InvalidOperationException("No free room port.");
        }

        public void Tick(bool allowRegistration = true, int firstNumber = 1)
        {
            if (!registrationStarted && allowRegistration && advertisement != null)
            {
                registrationStarted = true;
                candidateNumber = Math.Max(candidateNumber, firstNumber);
                string requestedName = LabRoomNames.ServiceBase + (candidateNumber == 1 ? string.Empty
                    : " (" + candidateNumber.ToString(CultureInfo.InvariantCulture) + ")");
                registrationAttempts++;
                if (!advertisement.StartAdvertise(requestedName, Port, true, ipv6 != null, 1, Capacity, false, true, false)
                    && !RetryNameConflict()) { Dispose(); return; }
            }
            advertisement?.Tick();
            RetryNameConflict();
        }

        private bool RetryNameConflict()
        {
            if (!registrationStarted || advertisement == null || !advertisement.RegistrationNameConflict
                || registrationAttempts >= LabBonjourDiscovery.MaximumServices) return false;
            registrationStarted = false;
            candidateNumber++;
            return true;
        }

        public bool EnterAsCreator(LabNetwork network)
        {
            if (!Ready || network == null || !network.CanChooseConnection) return false;
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
