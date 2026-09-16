using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
        // Preserve the legacy game-response constant above. Connection establishment has a separate
        // budget covering transport attempts, NGO approval, and a small scheduling allowance.
        public const float CandidateConnectionTimeoutSeconds = ConnectionTimeoutSeconds + ApprovalTimeoutSeconds + 2f;

        private const string Troubleshooting = "Check the host address and port, same Wi-Fi, network isolation, and Local Network access in Settings.";
        private const string RoomFullReason = "C6_T02_ROOM_FULL";
        private static DirectConnectionSession owner;
        private readonly List<ulong> participants = new List<ulong>();
        private TwoParticipantAdmissionPolicy admission = new TwoParticipantAdmissionPolicy();
        private ReadOnlyCollection<ulong> participantView;
        private GameObject managerObject;
        private NetworkManager manager;
        private DualStackUnityTransport transport;
        private bool ownsSession;
        private bool destroying;
        private float connectionStartedAt;
        private DirectConnectionState stoppedState;
        private string stoppedMessage;
        private ConnectionCandidatePlan candidates;
        private ushort candidatePort;
        private bool retryCandidate;
        private bool retryManagerReleased;
        private int retryBarrierFrame;
        private uint managerGeneration;
        private Action transportFailureHandler;
        private NetworkTransport.TransportEventDelegate transportEventHandler;
        public string ConnectionStage { get; private set; } = "Idle";
        public string FailureStage { get; private set; } = string.Empty;
        public string FailureCode { get; private set; } = string.Empty;
        public bool JoinInProgress { get; private set; }
        public int CandidateAttempt => candidates == null ? 0 : candidates.Index + 1;
        public int CandidateCount => candidates?.Count ?? 0;
        public uint AttemptId { get; private set; }
        public string AttemptAddressFamily { get; private set; } = "NONE";
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
        public string Message { get; private set; } = "Ready. Start a host or enter its IPv4 or IPv6 address to join.";
        public ulong? LocalClientId { get; private set; }
        public IReadOnlyList<ulong> ParticipantIds => participantView ??= participants.AsReadOnly();
        public bool CanStart => ownsSession && isActiveAndEnabled
            && !JoinInProgress && (State == DirectConnectionState.Idle || State == DirectConnectionState.Failed)
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
                return RejectInput("Enter a port from 1 to 65535.", "INVALID_PORT");
            if (!EnsureManager()) return false;
            candidates = null;
            ResetAttempt("Host");
            admission.BeginHostSession();
            SetState(DirectConnectionState.StartingHost, "Starting host...");
            try
            {
                transport.ConfigureHost(port);
                if (manager.StartHost()) return true;
                FailureCode = "HOST_START_FAILED"; FailureStage = "TRANSPORT";
                BeginStop(DirectConnectionState.Failed, "Could not start host. The port may be in use. " + Troubleshooting);
            }
            catch (Exception exception)
            {
                LogExceptionType(exception);
                FailureCode = "HOST_START_FAILED"; FailureStage = "TRANSPORT";
                BeginStop(DirectConnectionState.Failed, "Could not start host. " + Troubleshooting);
            }
            return false;
        }

        public bool Join(string address, string portText) => JoinCandidates(new[] { address }, portText);

        public bool JoinCandidates(IEnumerable<string> addresses, string portText)
        {
            if (!CanStart) return false;
            if (!DirectConnectionValidation.TryParsePort(portText, out candidatePort))
                return RejectInput("Enter a port from 1 to 65535.", "INVALID_PORT");
            if (!ConnectionCandidatePlan.TryCreate(addresses, Time.realtimeSinceStartupAsDouble, out var plan))
                return RejectInput("Enter a valid IPv4 or IPv6 address; link-local IPv6 also needs its numeric interface scope.", "INVALID_ADDRESS");
            candidates = plan;
            JoinInProgress = true;
            retryCandidate = retryManagerReleased = false;
            return StartCandidate();
        }

        private bool StartCandidate()
        {
            if (!EnsureManager()) { JoinInProgress = false; Changed?.Invoke(); return false; }
            ResetAttempt("Client");
            string address = candidates.Current;
            AttemptAddressFamily = address.Contains(":") ? "IPv6" : "IPv4";
            SetState(DirectConnectionState.Connecting, $"Connecting to host... ({CandidateAttempt}/{CandidateCount})");
            try
            {
                transport.ConfigureClient(address, candidatePort);
                if (manager.StartClient()) return true;
                AttemptFailed("CONNECT_START_FAILED", "Could not start the connection. " + Troubleshooting);
            }
            catch (Exception exception)
            {
                LogExceptionType(exception);
                AttemptFailed("CONNECT_START_FAILED", "Could not start the connection. " + Troubleshooting);
            }
            return JoinInProgress;
        }

        public void Stop()
        {
            if (!ownsSession) return;
            JoinInProgress = false;
            retryCandidate = false;
            FailureCode = "USER_CANCELLED";
            FailureStage = string.Empty;
            if (State == DirectConnectionState.Stopping)
            {
                // Cancellation includes the shutdown barrier between candidates.
                stoppedState = DirectConnectionState.Idle;
                stoppedMessage = "Connection cancelled. Start a host or join manually.";
                Changed?.Invoke();
                return;
            }
            if (State == DirectConnectionState.Idle || State == DirectConnectionState.Failed)
            {
                CompleteStop(DirectConnectionState.Idle, "Ready. Start a host or join manually.");
                return;
            }
            BeginStop(DirectConnectionState.Idle, "Session ended. Start a host or join manually.");
        }

        private bool RejectInput(string reason, string code = "CONNECTION_MANAGER_CONFLICT")
        {
            FailureCode = code;
            FailureStage = "INPUT";
            SetState(DirectConnectionState.Failed, reason);
            return false;
        }

        private bool EnsureManager()
        {
            if (manager != null) return true;
            if (FindAnyObjectByType<NetworkManager>() != null)
                return RejectInput("Another NetworkManager already exists. End that session first.");
            managerObject = new GameObject("T02 Owned NetworkManager");
            transport = managerObject.AddComponent<DualStackUnityTransport>();
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
            var eventManager = manager;
            uint generation = ++managerGeneration;
            transportFailureHandler = () =>
            {
                if (generation == managerGeneration && eventManager == manager) OnTransportFailure();
            };
            manager.OnTransportFailure += transportFailureHandler;
            transportEventHandler = (kind, client, payload, receivedAt) =>
            {
                if (generation != managerGeneration || eventManager != manager || destroying
                    || Role != "Client" || State != DirectConnectionState.Connecting) return;
                if (kind == Unity.Netcode.NetworkEvent.Connect)
                {
                    ConnectionStage = "HostApproval";
                    Message = "Host found. Checking admission...";
                    Debug.Log($"C6_L2_STAGE attempt={AttemptId} stage=HostApproval elapsed={Time.realtimeSinceStartup-connectionStartedAt:F3}");
                    Changed?.Invoke();
                }
            };
            transport.OnTransportEvent += transportEventHandler;
            return true;
        }

        private void ResetAttempt(string role)
        {
            Role = role;
            AttemptId++;
            FailureCode = string.Empty;
            FailureStage = string.Empty;
            AttemptAddressFamily = role == "Host" ? "DUAL" : "NONE";
            LastApprovalReason = string.Empty;
            LocalClientId = null;
            participants.Clear();
            admission.EndSession();
            connectionStartedAt = Time.realtimeSinceStartup;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Reserve immediately: NGO can queue multiple approvals before updating ConnectedClientsIds.
            bool approved = !destroying && Role == "Host" && (State == DirectConnectionState.StartingHost || State == DirectConnectionState.Connected) && admission.TryReserve(request.ClientNetworkId);
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
            if (destroying || source != manager || !AcceptConnectionCallbacks) return;
            switch (data.EventType)
            {
                case ConnectionEvent.ClientConnected:
                    JoinInProgress = false;
                    retryCandidate = false;
                    FailureCode = string.Empty; FailureStage = string.Empty;
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
                            { FailureCode = "PARTICIPANT_LEFT"; FailureStage = "SESSION"; BeginStop(DirectConnectionState.Failed, "Participant left. Session ended; connect again manually."); }
                        }
                    }
                    else
                    {
                        // NGO 2.13.1 also returns locally generated transport information here.
                        // Only an actual server denial must stop a pre-admission candidate retry.
                        LastApprovalReason = GetExplicitApprovalReason(source.DisconnectReason,
                            transport.DisconnectEvent, transport.DisconnectEventMessage);
                        bool alreadyConnected = State == DirectConnectionState.Connected;
                        AttemptFailed(alreadyConnected ? "CONNECTION_LOST" : string.IsNullOrEmpty(LastApprovalReason)
                            ? "CONNECT_NO_RESPONSE"
                            : "CONNECTION_REJECTED", LastApprovalReason == RoomFullReason
                            ? "This room is full. Wait for a free seat or choose another room."
                            : alreadyConnected ? "Connection ended after joining. Connect again to enter a room."
                            : "The host did not complete admission. " + Troubleshooting,
                            alreadyConnected ? string.Empty : LastApprovalReason);
                    }
                    break;
                case ConnectionEvent.PeerDisconnected:
                    if (participants.Contains(data.ClientId))
                    {
                        ParticipantDisconnected?.Invoke(data.ClientId);
                        if (MaximumParticipants > 2 && preservePeerDisconnect?.Invoke() == true)
                        { participants.Remove(data.ClientId); Changed?.Invoke(); }
                        else { FailureCode = "PARTICIPANT_LEFT"; FailureStage = "SESSION"; BeginStop(DirectConnectionState.Failed, "Participant left. Session ended; connect again manually."); }
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

        private bool AcceptConnectionCallbacks => State == DirectConnectionState.Connecting
            || State == DirectConnectionState.StartingHost || State == DirectConnectionState.Connected;

        /// <summary>
        /// Separates NGO 2.13.1's locally generated disconnect information from server denial text.
        /// Unknown text remains a denial: do not retry around future or unrecognized admission rules.
        /// This is classification only; a connected session never retries regardless of its reason.
        /// </summary>
        public static string GetExplicitApprovalReason(string reason, NetworkTransport.DisconnectEvents disconnectEvent,
            string transportMessage)
        {
            if (string.IsNullOrEmpty(reason)) return string.Empty;
            int cursor = 0;
            // NetworkConnectionManager.GenerateDisconnectInformation builds exactly this header.
            // Checking both IDs and the current local event/message avoids treating arbitrary
            // server text that happens to start with "[Disconnect Event]" as a transport timeout.
            if (!ConsumeNgoDisconnectId(reason, "[Disconnect Event][Client-", ref cursor)
                || !ConsumeNgoDisconnectId(reason, "[TransportClientId-", ref cursor)) return reason;
            string tail = reason.Substring(cursor);
            string eventPrefix = "[" + disconnectEvent + "] ";
            string message = transportMessage ?? string.Empty;
            if (tail == eventPrefix + message
                || tail == eventPrefix + "NetworkConnectionManager was shutdown. " + message) return string.Empty;
            return reason;
        }

        private static bool ConsumeNgoDisconnectId(string value, string prefix, ref int cursor)
        {
            if (value.IndexOf(prefix, cursor, StringComparison.Ordinal) != cursor) return false;
            int start = cursor + prefix.Length;
            int end = value.IndexOf(']', start);
            if (end <= start || end - start > 20
                || !ulong.TryParse(value.Substring(start, end - start), NumberStyles.None,
                    CultureInfo.InvariantCulture, out _)) return false;
            cursor = end + 1;
            return true;
        }

        private void OnTransportFailure()
        {
            if (!destroying && AcceptConnectionCallbacks)
                AttemptFailed("TRANSPORT_FAILURE", "Network transport failed. " + Troubleshooting);
        }

        private void AttemptFailed(string code, string message, string approvalReason = "")
        {
            if (!AcceptConnectionCallbacks) return;
            bool canRetry = Role == "Client" && State == DirectConnectionState.Connecting && JoinInProgress
                && candidates != null && candidates.CanRetry(false, approvalReason, Time.realtimeSinceStartupAsDouble);
            FailureCode = code;
            FailureStage = State == DirectConnectionState.Connected ? "SESSION"
                : !string.IsNullOrEmpty(approvalReason) || ConnectionStage == "HostApproval" ? "APPROVAL" : "TRANSPORT";
            if (canRetry)
            {
                retryCandidate = true;
                retryManagerReleased = false;
                BeginStop(DirectConnectionState.Failed, "Address did not connect. Trying another available address...");
                return;
            }
            if (Role == "Client" && State == DirectConnectionState.Connecting && string.IsNullOrEmpty(approvalReason) && candidates != null)
            {
                if (candidates.BudgetExpired(Time.realtimeSinceStartupAsDouble)) FailureCode = "CONNECT_BUDGET_EXHAUSTED";
                else if (candidates.Count > 1) FailureCode = "CONNECT_CANDIDATES_EXHAUSTED";
            }
            JoinInProgress = false;
            retryCandidate = false;
            BeginStop(DirectConnectionState.Failed, message);
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (State == DirectConnectionState.Connecting && JoinInProgress && candidates != null)
            {
                if (candidates.BudgetExpired(now))
                    AttemptFailed("CONNECT_BUDGET_EXHAUSTED", "Connection attempts timed out. " + Troubleshooting);
                else if (now - connectionStartedAt >= CandidateConnectionTimeoutSeconds)
                    AttemptFailed("CONNECT_ATTEMPT_TIMEOUT", "Connection timed out. " + Troubleshooting);
            }
            else if (State == DirectConnectionState.StartingHost && now - connectionStartedAt >= ConnectionTimeoutSeconds)
            {
                FailureCode = "HOST_START_TIMEOUT"; FailureStage = "TRANSPORT";
                BeginStop(DirectConnectionState.Failed, "Host startup timed out. " + Troubleshooting);
            }

            // Never start the next candidate inside an NGO callback, or before its old manager is gone.
            if (State != DirectConnectionState.Stopping || (manager != null && (manager.IsListening || manager.ShutdownInProgress))) return;
            if (retryCandidate && JoinInProgress)
            {
                if (candidates.BudgetExpired(now))
                {
                    FailureCode = "CONNECT_BUDGET_EXHAUSTED";
                    retryCandidate = JoinInProgress = false;
                    stoppedState = DirectConnectionState.Failed;
                    stoppedMessage = "Connection attempts timed out. " + Troubleshooting;
                    ReleaseManager();
                    retryManagerReleased = true;
                    retryBarrierFrame = Time.frameCount;
                    return;
                }
                if (!retryManagerReleased)
                {
                    ReleaseManager();
                    retryManagerReleased = true;
                    retryBarrierFrame = Time.frameCount;
                    return;
                }
                if (Time.frameCount <= retryBarrierFrame) return;
                retryCandidate = retryManagerReleased = false;
                if (!candidates.MoveNext(now))
                {
                    JoinInProgress = false;
                    FailureCode = "CONNECT_CANDIDATES_EXHAUSTED";
                    CompleteStop(DirectConnectionState.Failed, "No available address connected. " + Troubleshooting);
                    return;
                }
                StartCandidate();
                return;
            }
            // A manually restarted failed/cancelled client also receives a fresh manager. Successful
            // manual Host cycles retain their existing manager and subscription contract.
            if (Role == "Client" && !retryManagerReleased)
            {
                ReleaseManager();
                retryManagerReleased = true;
                retryBarrierFrame = Time.frameCount;
                return;
            }
            if (retryManagerReleased && Time.frameCount <= retryBarrierFrame) return;
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
            JoinInProgress = retryCandidate = retryManagerReleased = false;
            participants.Clear();
            admission.EndSession();
            LocalClientId = null;
            Role = "None";
            SetState(finalState, reason);
        }

        private void SetState(DirectConnectionState state, string message)
        {
            State = state;
            ConnectionStage = state == DirectConnectionState.Idle ? "Idle"
                : state == DirectConnectionState.StartingHost ? "HostStarting"
                : state == DirectConnectionState.Connecting ? "TransportConnecting"
                : state == DirectConnectionState.Connected ? "Connected"
                : state == DirectConnectionState.Stopping ? (retryCandidate && JoinInProgress ? "Retrying" : "Stopping")
                : "Failed";
            Message = message;
            Debug.Log($"C6_NET_CONNECTION attempt={AttemptId} candidate={CandidateAttempt}/{CandidateCount} family={AttemptAddressFamily} stage={ConnectionStage} failureStage={FailureStage} code={FailureCode} joining={JoinInProgress} elapsed={Time.realtimeSinceStartup-connectionStartedAt:F3}");
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
            if (ownsSession && !destroying && (JoinInProgress || (manager != null && manager.IsListening))) Stop();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && ownsSession && !destroying && JoinInProgress) Stop();
        }

        private void ReleaseManager()
        {
            managerGeneration++;
            if (manager != null)
            {
                manager.OnConnectionEvent -= OnConnectionEvent;
                if (transportFailureHandler != null) manager.OnTransportFailure -= transportFailureHandler;
                manager.ConnectionApprovalCallback = null;
                if (manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
            }
            if (transport != null && transportEventHandler != null) transport.OnTransportEvent -= transportEventHandler;
            transportFailureHandler = null;
            transportEventHandler = null;
            if (managerObject != null) Destroy(managerObject);
            managerObject = null;
            manager = null;
            transport = null;
        }

        private void OnDestroy()
        {
            destroying = true;
            JoinInProgress = retryCandidate = false;
            if (!ownsSession) return;
            ReleaseManager();
            admission.EndSession();
            Changed = null;
            ParticipantDisconnected = null;
            if (owner == this) owner = null;
            ownsSession = false;
        }
    }
}
