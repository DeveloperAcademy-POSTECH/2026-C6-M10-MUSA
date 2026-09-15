using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace C6.Prototype.Networking
{
    public enum DirectConnectionState { Idle, StartingHost, Connecting, Connected, Stopping, Failed }

    [DisallowMultipleComponent]
    public sealed class DirectConnectionSession : MonoBehaviour
    {
        // DEMO_ASSUMPTION: T02 local prototype defaults. These are the canonical tuning values.
        public const int DefaultPort = 7777;
        public const float ConnectionTimeoutSeconds = 8f;
        public const int ConnectAttemptIntervalMilliseconds = 500;
        public const int ConnectAttemptCount = 16;
        public const int DisconnectTimeoutMilliseconds = 8000;
        public const int ApprovalTimeoutSeconds = 8;
        public const ushort ProtocolVersion = 2;

        private const string Troubleshooting = "Check the host address and port, same Wi-Fi, network isolation, and Local Network access in Settings.";
        private const string RoomFullReason = "C6_T02_ROOM_FULL";
        private static DirectConnectionSession owner;
        private readonly List<ulong> participants = new List<ulong>();
        private TwoParticipantAdmissionPolicy admission = new TwoParticipantAdmissionPolicy();
        private ReadOnlyCollection<ulong> participantView;
        private GameObject managerObject;
        private NetworkManager manager;
        private UnityTransport transport;
        private bool ownsSession;
        private bool destroying;
        private float connectionStartedAt;
        private DirectConnectionState stoppedState;
        private string stoppedMessage;
        private ushort selectedProtocol = ProtocolVersion;
        private byte[] connectionPayload = Array.Empty<byte>();
        private Func<ulong, byte[], string> applicationAdmission;
        private Func<bool> preservePeerDisconnect;
        public int MaximumParticipants => admission.MaximumParticipants;
        public event Action<ulong> ParticipantDisconnected;
        public string LastApprovalReason { get; private set; } = string.Empty;

        // Opt-in T10 admission. Earlier scenes retain the original protocol and capacity policy.
        public bool ConfigureConnection(ushort protocol, byte[] payload, Func<ulong, byte[], string> admissionCheck = null,
            int maximumParticipants = TwoParticipantAdmissionPolicy.Capacity, Func<bool> keepLobbyOnPeerDisconnect = null)
        {
            if (!CanStart || payload == null || payload.Length > 512) return false;
            if (maximumParticipants != 2 && maximumParticipants != 5) return false;
            admission = new TwoParticipantAdmissionPolicy(maximumParticipants);
            preservePeerDisconnect = keepLobbyOnPeerDisconnect;
            selectedProtocol = protocol; connectionPayload = (byte[])payload.Clone(); applicationAdmission = admissionCheck;
            if (manager != null)
            {
                manager.NetworkConfig.ProtocolVersion = selectedProtocol;
                manager.NetworkConfig.ConnectionData = (byte[])connectionPayload.Clone();
            }
            return true;
        }

        public DirectConnectionState State { get; private set; } = DirectConnectionState.Idle;
        public string Role { get; private set; } = "None";
        public string Message { get; private set; } = "Ready. Start a host or enter its IPv4 address to join.";
        public ulong? LocalClientId { get; private set; }
        public IReadOnlyList<ulong> ParticipantIds => participantView ??= participants.AsReadOnly();
        public bool CanStart => ownsSession && isActiveAndEnabled
            && (State == DirectConnectionState.Idle || State == DirectConnectionState.Failed)
            && (manager == null || (!manager.IsListening && !manager.ShutdownInProgress));
        // Read-only access for the counter service; this session retains lifecycle ownership.
        public NetworkManager OwnedManager => manager;
        public event Action Changed;

        private void Awake()
        {
            if (owner != null && owner != this)
            {
                SetState(DirectConnectionState.Failed, "Another connection controller already exists.");
                enabled = false;
                return;
            }
            owner = this;
            ownsSession = true;
        }

        public bool StartHost(string portText)
        {
            if (!CanStart) return false;
            if (!DirectConnectionValidation.TryParsePort(portText, out var port))
                return RejectInput("Enter a port from 1 to 65535.");
            if (!EnsureManager()) return false;
            ResetAttempt("Host");
            admission.BeginHostSession();
            transport.SetConnectionData(true, "127.0.0.1", port, "0.0.0.0");
            SetState(DirectConnectionState.StartingHost, "Starting host...");
            try
            {
                if (manager.StartHost()) return true;
                BeginStop(DirectConnectionState.Failed, "Could not start host. The port may be in use. " + Troubleshooting);
            }
            catch (Exception exception)
            {
                LogExceptionType(exception);
                BeginStop(DirectConnectionState.Failed, "Could not start host. " + Troubleshooting);
            }
            return false;
        }

        public bool Join(string address, string portText)
        {
            if (!CanStart) return false;
            if (!DirectConnectionValidation.TryParseAddress(address, out var parsedAddress))
                return RejectInput("Enter a dotted IPv4 unicast address, for example 192.168.1.20.");
            if (!DirectConnectionValidation.TryParsePort(portText, out var port))
                return RejectInput("Enter a port from 1 to 65535.");
            if (!EnsureManager()) return false;
            ResetAttempt("Client");
            transport.SetConnectionData(true, parsedAddress, port);
            SetState(DirectConnectionState.Connecting, "Connecting to host...");
            try
            {
                if (manager.StartClient()) return true;
                BeginStop(DirectConnectionState.Failed, "Could not start the connection. " + Troubleshooting);
            }
            catch (Exception exception)
            {
                LogExceptionType(exception);
                BeginStop(DirectConnectionState.Failed, "Could not start the connection. " + Troubleshooting);
            }
            return false;
        }

        public void Stop()
        {
            if (!ownsSession || State == DirectConnectionState.Stopping) return;
            if (State == DirectConnectionState.Idle || State == DirectConnectionState.Failed)
            {
                CompleteStop(DirectConnectionState.Idle, "Ready. Start a host or join manually.");
                return;
            }
            BeginStop(DirectConnectionState.Idle, "Session ended. Start a host or join manually.");
        }

        private bool RejectInput(string reason)
        {
            SetState(DirectConnectionState.Failed, reason);
            return false;
        }

        private bool EnsureManager()
        {
            if (manager != null) return true;
            if (FindAnyObjectByType<NetworkManager>() != null)
                return RejectInput("Another NetworkManager already exists. End that session first.");
            managerObject = new GameObject("T02 Owned NetworkManager");
            transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ProtocolVersion = selectedProtocol,
                ConnectionData = (byte[])connectionPayload.Clone(),
                ConnectionApproval = true,
                EnableSceneManagement = false,
                PlayerPrefab = null,
                ClientConnectionBufferTimeout = ApprovalTimeoutSeconds
            };
            transport.ConnectTimeoutMS = ConnectAttemptIntervalMilliseconds;
            transport.MaxConnectAttempts = ConnectAttemptCount;
            transport.DisconnectTimeoutMS = DisconnectTimeoutMilliseconds;
            manager.ConnectionApprovalCallback = ApproveConnection;
            manager.OnConnectionEvent += OnConnectionEvent;
            manager.OnTransportFailure += OnTransportFailure;
            return true;
        }

        private void ResetAttempt(string role)
        {
            Role = role;
            LastApprovalReason = string.Empty;
            LocalClientId = null;
            participants.Clear();
            admission.EndSession();
            connectionStartedAt = Time.realtimeSinceStartup;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Reserve immediately: NGO can queue multiple approvals before updating ConnectedClientsIds.
            bool approved = State != DirectConnectionState.Stopping && admission.TryReserve(request.ClientNetworkId);
            string reason = approved ? string.Empty : RoomFullReason;
            if (approved && applicationAdmission != null)
            {
                try { reason = applicationAdmission(request.ClientNetworkId, request.Payload) ?? "C6_INVALID_APPROVAL"; }
                catch (Exception exception) { LogExceptionType(exception); reason = "C6_INVALID_APPROVAL"; }
                approved = string.IsNullOrEmpty(reason);
                if (!approved) admission.Release(request.ClientNetworkId);
            }
            response.Approved = approved;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = approved ? string.Empty : reason;
        }

        private void OnConnectionEvent(NetworkManager source, ConnectionEventData data)
        {
            if (destroying || source != manager || State == DirectConnectionState.Stopping) return;
            switch (data.EventType)
            {
                case ConnectionEvent.ClientConnected:
                    if (Role == "Host")
                    {
                        LocalClientId = NetworkManager.ServerClientId;
                        AddParticipant(NetworkManager.ServerClientId);
                        AddParticipant(data.ClientId);
                    }
                    else
                    {
                        LocalClientId = source.LocalClientId;
                        AddParticipant(source.LocalClientId);
                        // This temporary NativeArray is only valid during the callback.
                        if (data.PeerClientIds.IsCreated)
                            foreach (var peer in data.PeerClientIds) AddParticipant(peer);
                    }
                    SetState(DirectConnectionState.Connected, participants.Count >= 2
                        ? "Connected. " + participants.Count + " participants are present."
                        : "Host ready. Waiting for participants.");
                    break;
                case ConnectionEvent.PeerConnected:
                    if (!participants.Contains(data.ClientId))
                    {
                        AddParticipant(data.ClientId);
                        SetState(DirectConnectionState.Connected, "Connected. " + participants.Count + " participants are present.");
                    }
                    break;
                case ConnectionEvent.ClientDisconnected:
                    if (Role == "Host")
                    {
                        var wasParticipant = participants.Contains(data.ClientId);
                        admission.Release(data.ClientId);
                        if (data.ClientId != NetworkManager.ServerClientId)
                        {
                            ParticipantDisconnected?.Invoke(data.ClientId);
                            if (MaximumParticipants > 2 && preservePeerDisconnect?.Invoke() == true)
                            { participants.Remove(data.ClientId); Changed?.Invoke(); }
                            else if (wasParticipant)
                                BeginStop(DirectConnectionState.Failed, "Participant left. Session ended; connect again manually.");
                        }
                    }
                    else
                    {
                        var reason = source.DisconnectReason;
                        LastApprovalReason = reason ?? string.Empty;
                        BeginStop(DirectConnectionState.Failed, reason == RoomFullReason
                            ? "This room is full. Wait for a free seat or choose another room."
                            : "Disconnected or connection failed. " + Troubleshooting);
                    }
                    break;
                case ConnectionEvent.PeerDisconnected:
                    if (participants.Contains(data.ClientId))
                    {
                        ParticipantDisconnected?.Invoke(data.ClientId);
                        if (MaximumParticipants > 2 && preservePeerDisconnect?.Invoke() == true)
                        { participants.Remove(data.ClientId); Changed?.Invoke(); }
                        else BeginStop(DirectConnectionState.Failed, "Participant left. Session ended; connect again manually.");
                    }
                    break;
            }
        }

        private void AddParticipant(ulong clientId)
        {
            if (participants.Contains(clientId)) return;
            participants.Add(clientId);
            participants.Sort();
        }

        private void OnTransportFailure()
        {
            if (!destroying && State != DirectConnectionState.Stopping)
                BeginStop(DirectConnectionState.Failed, "Network transport failed. " + Troubleshooting);
        }

        private void Update()
        {
            if ((State == DirectConnectionState.Connecting || State == DirectConnectionState.StartingHost)
                && Time.realtimeSinceStartup - connectionStartedAt >= ConnectionTimeoutSeconds)
                BeginStop(DirectConnectionState.Failed, "Connection timed out. " + Troubleshooting);

            // Complete outside NGO callbacks so a new manual start cannot re-enter its shutdown code.
            if (State == DirectConnectionState.Stopping
                && (manager == null || (!manager.IsListening && !manager.ShutdownInProgress)))
                CompleteStop(stoppedState, stoppedMessage);
        }

        private void BeginStop(DirectConnectionState finalState, string reason)
        {
            if (State == DirectConnectionState.Stopping) return;
            stoppedState = finalState;
            stoppedMessage = reason;
            SetState(DirectConnectionState.Stopping, reason);
            if (manager != null && !manager.ShutdownInProgress) manager.Shutdown();
        }

        private void CompleteStop(DirectConnectionState finalState, string reason)
        {
            participants.Clear();
            admission.EndSession();
            LocalClientId = null;
            Role = "None";
            SetState(finalState, reason);
        }

        private void SetState(DirectConnectionState state, string message)
        {
            State = state;
            Message = message;
            Debug.Log($"C6_T02_STATE role={Role} status={State} local={LocalClientId?.ToString() ?? "none"} ids=[{string.Join(",", participants)}] reason={Message}");
            Changed?.Invoke();
        }

        private static void LogExceptionType(Exception exception)
        {
            // Avoid leaking arbitrary external exception text into shared evidence.
            Debug.LogWarning($"C6_T02_EXCEPTION type={exception.GetType().Name}");
        }

        public static string[] GetLocalIPv4Addresses()
        {
            var addresses = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.NetworkInterfaceType == NetworkInterfaceType.Loopback
                        || network.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var entry in network.GetIPProperties().UnicastAddresses)
                    {
                        if (entry.Address.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(entry.Address)
                            && DirectConnectionValidation.TryParseAddress(entry.Address.ToString(), out var address))
                            addresses.Add(address);
                    }
                }
            }
            catch (Exception exception)
            {
                LogExceptionType(exception);
                // An empty result means the UI must point to the device's Wi-Fi address in Settings.
            }
            var result = new string[addresses.Count];
            addresses.CopyTo(result);
            return result;
        }

        private void OnDisable()
        {
            if (ownsSession && !destroying && manager != null && manager.IsListening)
                BeginStop(DirectConnectionState.Idle, "Connection screen disabled. Connect again manually.");
        }

        private void OnDestroy()
        {
            destroying = true;
            if (!ownsSession) return;
            if (manager != null)
            {
                manager.OnConnectionEvent -= OnConnectionEvent;
                manager.OnTransportFailure -= OnTransportFailure;
                manager.ConnectionApprovalCallback = null;
                if (!manager.ShutdownInProgress) manager.Shutdown();
            }
            if (managerObject != null) Destroy(managerObject);
            admission.EndSession();
            Changed = null;
            ParticipantDisconnected = null;
            if (owner == this) owner = null;
            ownsSession = false;
        }
    }
}
