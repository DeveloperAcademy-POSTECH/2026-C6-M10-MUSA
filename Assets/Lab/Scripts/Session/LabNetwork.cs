using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace C6Lab
{
    [Serializable]
    internal sealed class LabMotionSample
    {
        public string id;
        public float x, y, vx, vy;
    }

    [Serializable]
    internal sealed class LabNetworkPacket
    {
        public int protocol = 2;
        public string type;
        public string sessionId;
        public ulong[] seats;
        public LabSnapshot snapshot;
        public long requestId;
        public string firstId;
        public string secondId;
        public uint round;
        public int damage;
        public bool right;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public float vz;
        public float z;
        public float wx;
        public float wy;
        public float wz;
        public string reason;
        public ulong owner;
        public int element;
        public int targetParticipants;
        public int configRevision;
        public string configFingerprint;
        public float[] tuning;
        public long snapshotSequence;
        public int chunkIndex;
        public int chunkCount;
        public string chunkData;
        public LabMotionSample[] motionSamples;
    }

    /// <summary>
    /// Client-side gate for authoritative impact presentation. A shot can display once in the
    /// current Host round; stale, malformed, and repeated messages never replay the effect.
    /// </summary>
    public sealed class LabImpactReceiptGate
    {
        private readonly HashSet<string> seenProjectileIds = new HashSet<string>(StringComparer.Ordinal);
        private string activeSession;
        private uint activeRound;

        public void SetActive(string session, uint round)
        {
            if (activeSession == session && activeRound == round) return;
            activeSession = session;
            activeRound = round;
            seenProjectileIds.Clear();
        }

        public void Reset() => SetActive(null, 0);

        public bool TryAccept(string session, uint round, string projectileId, LabOrbElement element,
            int damage, Vector3 targetLocalPoint, Vector3 impactVelocity)
        {
            if (string.IsNullOrEmpty(activeSession) || activeRound == 0 || session != activeSession
                || round != activeRound || string.IsNullOrEmpty(projectileId)
                || element < LabOrbElement.Fire || element > LabOrbElement.Earth || damage <= 0
                || !Finite(targetLocalPoint) || !Finite(impactVelocity)) return false;
            return seenProjectileIds.Add(projectileId);
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    /// <summary>
    /// Local Host/Join experiment. The host alone owns LabModel and session tuning.
    /// Only owner motion is simulated locally; requests and damage are accepted by the host.
    /// </summary>
    public sealed class LabNetwork : MonoBehaviour
    {
        private const string MessageName = "C6Lab.Packet.2";
        // Relay stages, bomb explosions and item effect settings are new snapshot/settings data;
        // reject older clients rather than mixing rooms with different item rules.
        private const ushort LabProtocolVersion = 5610;
        private const string BombExplodedNotice = "BOMB! Every orb on your board was lost.";
        private static readonly byte[] LabConnectionToken = Encoding.ASCII.GetBytes("C6PhysicsLab/5");
        private const int MaximumPacketBytes = 32768;
        private const double JoinAttemptSeconds = 12d;
        private const double JoinBudgetSeconds = 60d;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public LabConfig config;
        public LabOrbBoard board;
        public LabTarget target;
        public LabImpactFeedback impactFeedback;
        public Collider battleFloor;
        public Transform projectileRoot;
        public NetworkManager manager;
        public UnityTransport transport;
        public ushort port = 7777;
        public string hostAddress = "127.0.0.1";

        private readonly List<ulong> order = new List<ulong>(5);
        private readonly HashSet<ulong> configAcknowledgements = new HashSet<ulong>();
        private readonly Dictionary<ulong, double> pendingApprovals = new Dictionary<ulong, double>();
        private readonly HashSet<string> visibleOrbIds = new HashSet<string>();
        private readonly LabImpactReceiptGate impactReceipts = new LabImpactReceiptGate();
        private readonly List<string> joinCandidates = new List<string>();
        private LabModel model;
        private double nextSnapshotAt;
        private double nextMotionAt;
        private long requestSequence;
        private bool handlerRegistered;
        private bool autoStart;
        private bool autoGenerate;
        private bool autoTransfer;
        private bool developerSolo;
        private string autoGeneratedSession;
        private string autoTransferredSession;
        private bool stopping;
        private bool stopAfterDisconnect;
        private string sessionId;
        private LabSnapshot latest;
        private LabConfig sourceConfig;
        private LabConfig sessionConfig;
        private LabBonjourDiscovery discovery;
        private string advertisedName;
        private IReadOnlyList<LabDiscoveredRoom> discoveredRooms = Array.Empty<LabDiscoveredRoom>();
        private bool roomSearchActive;
        private int targetParticipantCount = 3;
        private int configRevision;
        private string configFingerprint;
        private int joinCandidateIndex;
        private double joinStartedAt;
        private double joinAttemptAt;
        private bool joinInProgress;
        private bool joinRetryPending;
        private bool discoveryJoinActive;
        private string discoveryRoomKey;
        private bool clientConfigReady;
        private long snapshotSequence;
        private long incomingSequence;
        private int incomingChunkCount;
        private string[] incomingChunks;
        private int incomingChunkReceived;

        public event Action Changed;
        public event Action<LabConfig> ConfigApplied;
        public string Status { get; private set; } = "Choose HOST or JOIN.";
        public string Notice { get; private set; } = string.Empty;
        public string LastShotReport { get; private set; } = string.Empty;
        public int ParticipantCount => order.Count;
        public int TargetParticipantCount => targetParticipantCount;
        public bool IsSearchingRooms => roomSearchActive && discovery != null && discovery.IsBrowsing;
        public IReadOnlyList<LabDiscoveredRoom> DiscoveredRooms => discoveredRooms;
        // Bonjour may rename a duplicate service. Show only its confirmed registered name.
        public string HostRoomName => IsHost && discovery != null && discovery.AdvertisingConfirmed
            ? discovery.RegisteredServiceName ?? string.Empty : string.Empty;
        public bool HostRoomNamePending => IsHost && discovery != null && discovery.IsAdvertising
            && !discovery.AdvertisingConfirmed;
        public bool SessionConfigReady => IsHost
            ? order.Count > 0 && order.All(id => configAcknowledgements.Contains(id))
            : clientConfigReady;
        public bool IsHost => manager != null && manager.IsHost;
        public bool IsDeveloperSolo => developerSolo;
        public bool DeveloperBuildAvailable => Application.isEditor || Debug.isDebugBuild;
        public bool IsConnected => manager != null && manager.IsListening && (manager.IsHost || manager.IsConnectedClient);
        private bool CanBeginConnection => !IsConnected && !joinInProgress && !joinRetryPending
            && (manager == null || (!manager.IsListening && !manager.ShutdownInProgress));
        public bool CanChooseConnection => CanBeginConnection && !roomSearchActive;
        public ulong LocalPlayerId => IsConnected ? manager.LocalClientId : ulong.MaxValue;
        public LabSnapshot Snapshot => latest;
        public bool CanStartBattle => IsHost && !developerSolo && order.Count == targetParticipantCount
            && SessionConfigReady && (latest == null || latest.phase != LabPhase.Playing);

        private void Awake()
        {
            sourceConfig = config;
            if (manager == null) manager = GetComponent<NetworkManager>();
            if (transport == null) transport = GetComponent<UnityTransport>();
            if (impactFeedback == null) impactFeedback = FindFirstObjectByType<LabImpactFeedback>();
            if (board != null)
            {
                board.EdgeCrossed += OnEdgeCrossed;
                board.CombineRequested += OnCombineRequested;
                board.ThrowRequested += OnThrowRequested;
                board.UseRequested += OnUseRequested;
            }
            if (manager != null)
            {
                manager.OnClientConnectedCallback += OnConnected;
                manager.OnClientDisconnectCallback += OnDisconnected;
                manager.OnTransportFailure += OnTransportFailure;
            }
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--lab-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out var chosenPort)) port = chosenPort;
                else if (args[i] == "--lab-join" && i + 1 < args.Length) hostAddress = args[i + 1];
                else if (args[i] == "--lab-auto-start") autoStart = true;
                else if (args[i] == "--lab-auto-generate") autoGenerate = true;
                else if (args[i] == "--lab-auto-transfer") autoTransfer = true;
            }
            if (Array.IndexOf(args, "--lab-host") >= 0) Host();
            else if (Array.IndexOf(args, "--lab-join") >= 0) Join(hostAddress);
        }

        /// <summary>The Host chooses 2–5 network participants before creating the room.</summary>
        public bool SetTargetParticipantCount(int count)
        {
            if (!CanChooseConnection || count < 2 || count > 5) return false;
            targetParticipantCount = count;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Install a validated, session-only copy before Host. Never edit the saved asset.</summary>
        public bool ConfigureHostSession(LabConfig candidate, int targetParticipants)
        {
            if (!CanChooseConnection || targetParticipants < 2 || targetParticipants > 5)
            {
                SetNotice("Choose 2–5 players before creating a room.");
                return false;
            }
            if (!TryInstallSessionConfig(candidate, out string error))
            {
                SetNotice(error);
                return false;
            }
            targetParticipantCount = targetParticipants;
            SetNotice("Room settings prepared for " + targetParticipants + " players.");
            return true;
        }

        /// <summary>Host lobby edits invalidate every old ACK and send a new revision.</summary>
        public bool ApplyHostLobbyConfig(LabConfig candidate)
        {
            if (!IsHost || developerSolo || model != null)
            {
                SetNotice("Settings can change only in the Host lobby.");
                return false;
            }
            if (!TryInstallSessionConfig(candidate, out string error))
            {
                SetNotice(error);
                return false;
            }
            configRevision++;
            configFingerprint = LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), targetParticipantCount,
                LabProtocolVersion);
            configAcknowledgements.Clear();
            configAcknowledgements.Add(LocalPlayerId);
            BroadcastLobby();
            foreach (ulong id in order) if (id != LocalPlayerId) SendSessionConfig(id);
            SetNotice("Settings updated; waiting for all players to confirm.");
            return true;
        }

        private bool TryInstallSessionConfig(LabConfig candidate, out string error)
        {
            error = "A valid session config is required.";
            if (candidate == null) return false;
            return TryInstallValues(candidate.CaptureDeveloperValues(), out error);
        }

        private bool TryInstallValues(float[] values, out string error)
        {
            error = "A valid session config is required.";
            if (sourceConfig == null) return false;
            LabConfig replacement = Instantiate(sourceConfig);
            replacement.name = sourceConfig.name + " (Session Runtime)";
            replacement.hideFlags = HideFlags.DontSave;
            if (!replacement.TryApplyDeveloperValues(values, out error))
            {
                Destroy(replacement);
                return false;
            }
            if (sessionConfig != null) Destroy(sessionConfig);
            sessionConfig = replacement;
            config = replacement;
            ConfigApplied?.Invoke(config);
            error = string.Empty;
            return true;
        }

        private void RestoreSourceConfig()
        {
            if (sourceConfig != null && config != sourceConfig)
            {
                config = sourceConfig;
                ConfigApplied?.Invoke(config);
            }
            if (sessionConfig != null)
            {
                Destroy(sessionConfig);
                sessionConfig = null;
            }
        }

        public void Host()
        {
            if (!CanChooseConnection || manager == null || transport == null || config == null) return;
            DisposeDiscovery();
            ResetLocal();
            configRevision = 1;
            configFingerprint = LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), targetParticipantCount,
                LabProtocolVersion);
            ConfigureManager();
            try
            {
                if (transport is LabDualStackTransport dual) dual.ConfigureHost(port);
                else transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            }
            catch (Exception e) { SetStatus("Host endpoint failed: " + e.Message); return; }
            if (!manager.StartHost())
            {
                string code = transport is LabDualStackTransport failed ? failed.LastFailureCode : string.Empty;
                SetStatus("Host failed: " + (string.IsNullOrEmpty(code) ? "check port and network" : code) + ".");
                return;
            }
            RegisterHandler();
            if (!order.Contains(manager.LocalClientId)) order.Add(manager.LocalClientId);
            configAcknowledgements.Add(manager.LocalClientId);
            StartAdvertising();
            SetStatus("HOST listening on port " + port + "; waiting for " + (targetParticipantCount - 1) + " peers.");
            Debug.Log("C6_LAB_HOST port=" + port + " protocol=" + LabProtocolVersion);
            BroadcastLobby();
        }

        public void Join(string address)
        {
            if (!CanChooseConnection || manager == null || transport == null || config == null) return;
            if (string.IsNullOrWhiteSpace(address)) { SetStatus("Enter the Host IP address."); return; }
            DisposeDiscovery();
            discoveryJoinActive = false;
            discoveryRoomKey = null;
            hostAddress = address.Trim();
            var addresses = new List<string>();
            try
            {
                if (IPAddress.TryParse(hostAddress, out var literal)) addresses.Add(literal.ToString());
                else addresses.AddRange(Dns.GetHostAddresses(hostAddress).Select(ip => ip.ToString()));
            }
            catch (Exception e) { SetStatus("Host address lookup failed: " + e.GetType().Name); return; }
            BeginJoinCandidates(addresses);
        }

        /// <summary>Browse without joining. A player must explicitly select a visible room.</summary>
        public bool StartRoomSearch()
        {
            if (!CanBeginConnection || manager == null || transport == null || config == null) return false;
            DisposeDiscovery();
            if (!LabBonjourDiscovery.IsSupported)
            {
                SetStatus("Room discovery is available on macOS and iOS. Use direct IP here.");
                return false;
            }
            discovery = new LabBonjourDiscovery();
            discovery.Diagnostic += message => Debug.Log("C6_LAB_DISCOVERY " + message);
            discovery.Changed += OnDiscoveryChanged;
            roomSearchActive = true;
            if (!discovery.StartBrowse())
            {
                string error = discovery.LastError ?? "Room search could not start. Use direct IP.";
                DisposeDiscovery();
                SetStatus(error);
                return false;
            }
            discoveryJoinActive = false;
            discoveryRoomKey = null;
            SetStatus("Searching local rooms; keep the Host app open...");
            return true;
        }

        public void CancelRoomSearch()
        {
            if (!roomSearchActive) return;
            DisposeDiscovery();
            SetStatus("Room search canceled. Choose HOST or JOIN.");
        }

        /// <summary>Use only the selected room's current IPv4/IPv6 routes.</summary>
        public bool JoinDiscoveredRoom(string roomKey)
        {
            if (!IsSearchingRooms || !CanBeginConnection) return false;
            LabDiscoveredRoom room = discoveredRooms.FirstOrDefault(item =>
                string.Equals(item.Key, roomKey, StringComparison.OrdinalIgnoreCase));
            LabBonjourTarget[] targets = LabRoomCatalog.RoutesForRoom(discovery.Targets, roomKey);
            if (room == null || targets.Length == 0)
            {
                discoveredRooms = LabRoomCatalog.GroupTargets(discovery.Targets);
                SetStatus("That room is no longer available. Refresh and choose another room.");
                return false;
            }
            port = room.Port;
            roomSearchActive = false;
            discoveredRooms = Array.Empty<LabDiscoveredRoom>();
            discoveryJoinActive = true;
            discoveryRoomKey = room.Key;
            BeginJoinCandidates(targets.Select(item => item.Address));
            return joinInProgress;
        }

        private void BeginJoinCandidates(IEnumerable<string> addresses)
        {
            ResetLocal();
            joinCandidates.Clear();
            joinCandidates.AddRange(LabConnectionCandidates.Initial(addresses, port));
            if (joinCandidates.Count == 0) { SetStatus("No usable numeric IPv4/IPv6 address. Use direct IP."); return; }
            joinCandidateIndex = 0;
            joinStartedAt = Time.realtimeSinceStartupAsDouble;
            joinInProgress = true;
            StartJoinAttempt();
        }

        private void StartJoinAttempt()
        {
            if (!joinInProgress || joinCandidateIndex >= joinCandidates.Count || manager == null || transport == null) return;
            string address = joinCandidates[joinCandidateIndex];
            ConfigureManager();
            try
            {
                if (transport is LabDualStackTransport dual) dual.ConfigureClient(address, port);
                else transport.SetConnectionData(address, port);
                if (!manager.StartClient()) throw new InvalidOperationException("Client start failed.");
            }
            catch (Exception e)
            {
                string code = transport is LabDualStackTransport failed ? failed.LastFailureCode : string.Empty;
                SetStatus("Cannot connect via " + address + ": "
                    + (string.IsNullOrEmpty(code) ? e.GetType().Name : code));
                AdvanceJoinCandidate();
                return;
            }
            RegisterHandler();
            joinAttemptAt = Time.realtimeSinceStartupAsDouble;
            SetStatus("Connecting to " + address + ":" + port + " (" + (joinCandidateIndex + 1)
                + "/" + joinCandidates.Count + ")...");
            Debug.Log("C6_LAB_JOIN address=" + address + " port=" + port);
        }

        private void AdvanceJoinCandidate()
        {
            if (!joinInProgress || joinRetryPending) return;
            string rejection = manager == null ? string.Empty : manager.DisconnectReason;
            if (!string.IsNullOrEmpty(rejection))
            {
                joinInProgress = false;
                discoveryJoinActive = false;
                DisposeDiscovery();
                UnregisterHandler();
                if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
                SetStatus("Join rejected: " + rejection);
                return;
            }
            if (++joinCandidateIndex >= joinCandidates.Count
                || Time.realtimeSinceStartupAsDouble - joinStartedAt > JoinBudgetSeconds)
            {
                if (discoveryJoinActive && Time.realtimeSinceStartupAsDouble - joinStartedAt <= JoinBudgetSeconds)
                {
                    joinRetryPending = true;
                    UnregisterHandler();
                    if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
                    SetStatus("Waiting for another local route to the discovered Host...");
                    return;
                }
                joinInProgress = false;
                DisposeDiscovery();
                UnregisterHandler();
                if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
                SetStatus("Connection failed on all local routes. Check Host, port, and network.");
                return;
            }
            joinRetryPending = true;
            UnregisterHandler();
            if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
        }

        public void Stop()
        {
            if (stopping) return;
            stopping = true;
            joinInProgress = false;
            joinRetryPending = false;
            discoveryJoinActive = false;
            discoveryRoomKey = null;
            DisposeDiscovery();
            UnregisterHandler();
            if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
            ResetLocal();
            RestoreSourceConfig();
            SetStatus("Disconnected. Choose HOST or JOIN.");
            stopping = false;
        }

        public void StartBattle()
        {
            if (!CanStartBattle)
            {
                SetNotice("Wait for " + targetParticipantCount + " players and matching settings confirmations.");
                return;
            }
            sessionId = Guid.NewGuid().ToString("N");
            impactFeedback?.Clear();
            target?.GetComponent<LabHangingTarget>()?.ResetSwing();
            model = new LabModel();
            model.Begin(sessionId, order.ToArray(), Time.realtimeSinceStartupAsDouble, config);
            impactReceipts.SetActive(sessionId, model.Round);
            SetNotice(string.Empty);
            PublishSnapshot(true);
            SetStatus("PLAYING: " + order.Count + " / " + targetParticipantCount);
            Debug.Log("C6_LAB_START session=" + sessionId + " seats=" + string.Join(",", order));
        }

        /// <summary>Start a one-player Host session only in the Editor or a Development Build.</summary>
        public bool StartDeveloperSolo()
        {
            if (!DeveloperBuildAvailable)
            {
                SetNotice("Developer Solo requires a Development Build.");
                return false;
            }
            if (IsConnected)
            {
                SetNotice("Leave the current session before starting Developer Solo.");
                return false;
            }
            Host();
            if (!IsHost || order.Count != 1) return false;
            developerSolo = true;
            DisposeDiscovery();
            BeginDeveloperSoloRound();
            return true;
        }

        /// <summary>Apply new runtime tuning by starting a fresh one-player round.</summary>
        public bool RestartDeveloperSolo()
        {
            if (!DeveloperBuildAvailable || !developerSolo || !IsHost || order.Count != 1 || config == null)
            {
                SetNotice("Start Developer Solo before applying a new round.");
                return false;
            }
            ClearProjectiles();
            if (board != null) board.Clear();
            visibleOrbIds.Clear();
            latest = null;
            BeginDeveloperSoloRound();
            return true;
        }

        private void BeginDeveloperSoloRound()
        {
            LastShotReport = string.Empty;
            sessionId = Guid.NewGuid().ToString("N");
            impactFeedback?.Clear();
            target?.GetComponent<LabHangingTarget>()?.ResetSwing();
            model = new LabModel();
            model.BeginSolo(sessionId, LocalPlayerId, Time.realtimeSinceStartupAsDouble, config);
            impactReceipts.SetActive(sessionId, model.Round);
            SetNotice(string.Empty);
            PublishSnapshot(true);
            SetStatus("DEVELOPER SOLO: 1 / 1");
            Debug.Log("C6_LAB_SOLO_START session=" + sessionId + " player=" + LocalPlayerId);
        }

        public void Generate()
        {
            if (!IsConnected || latest == null || latest.phase != LabPhase.Playing) return;
            Request(new LabNetworkPacket { type = "generate", requestId = ++requestSequence });
        }

        private void Update()
        {
            if (stopAfterDisconnect)
            {
                stopAfterDisconnect = false;
                string explanation = Status;
                Stop();
                SetStatus(explanation);
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (ulong stale in pendingApprovals.Where(item => now - item.Value > 15d)
                         .Select(item => item.Key).ToArray()) pendingApprovals.Remove(stale);
            LabBonjourDiscovery activeDiscovery = discovery;
            if (activeDiscovery != null)
            {
                activeDiscovery.Tick(now);
                if (discovery == activeDiscovery && roomSearchActive && !activeDiscovery.IsBrowsing)
                {
                    string error = activeDiscovery.LastError ?? "Room search ended. Use direct IP or search again.";
                    DisposeDiscovery();
                    SetStatus(error);
                }
                if (discovery == activeDiscovery && discoveryJoinActive)
                {
                    foreach (LabBonjourTarget target in activeDiscovery.Targets)
                    {
                        if (!string.Equals(LabRoomCatalog.Key(target.ServiceName, target.Port),
                                discoveryRoomKey, StringComparison.OrdinalIgnoreCase)) continue;
                        if (LabConnectionCandidates.AppendDiscovered(joinCandidates, target.Address, port))
                            Debug.Log("C6_LAB_DISCOVERY_ROUTE_ADDED family=" + LabBonjourAddressCodec.Family(target.Address));
                    }
                }
            }
            if (joinRetryPending && manager != null && !manager.IsListening && !manager.ShutdownInProgress)
            {
                if (joinCandidateIndex < joinCandidates.Count)
                {
                    joinRetryPending = false;
                    StartJoinAttempt();
                }
                else if (now - joinStartedAt > JoinBudgetSeconds)
                {
                    joinInProgress = false;
                    joinRetryPending = false;
                    DisposeDiscovery();
                    SetStatus("No reachable route found. Use direct IP or check the Host network.");
                }
            }
            if (joinInProgress && !joinRetryPending && joinAttemptAt > 0
                && now - joinAttemptAt >= JoinAttemptSeconds) AdvanceJoinCandidate();
            if (IsHost && model != null)
            {
                model.Tick(now);
                if (now >= nextSnapshotAt) PublishSnapshot(false);
            }
            // Command-line smoke mode is intentionally separate from ordinary UI play.
            if (IsConnected && latest != null && latest.phase == LabPhase.Playing)
            {
                if (autoGenerate && autoGeneratedSession != latest.sessionId)
                {
                    autoGeneratedSession = latest.sessionId;
                    Generate();
                }
                if (IsHost && autoTransfer && autoTransferredSession != latest.sessionId)
                {
                    LabOrbState candidate = null;
                    foreach (var orb in latest.orbs ?? Array.Empty<LabOrbState>())
                        if (orb.owner == LocalPlayerId && !orb.inFlight) { candidate = orb; break; }
                    if (candidate != null)
                    {
                        autoTransferredSession = latest.sessionId;
                        Request(new LabNetworkPacket { type = "motion", firstId = candidate.id, x = 1f, y = .5f, vx = 3f, vy = 0f });
                        Request(new LabNetworkPacket { type = "transfer", firstId = candidate.id, requestId = ++requestSequence,
                            right = true, y = .5f, vx = 3f, vy = 0f });
                        Debug.Log("C6_LAB_SMOKE_EDGE orb=" + candidate.id);
                    }
                }
            }
            if (IsConnected && board != null && latest != null && latest.phase == LabPhase.Playing && now >= nextMotionAt)
            {
                nextMotionAt = now + 0.1;
                var samples = new List<LabMotionSample>();
                foreach (var orb in latest.orbs ?? Array.Empty<LabOrbState>())
                {
                    if (orb.owner != LocalPlayerId || orb.inFlight) continue;
                    if (board.TryGetMotion(orb.id, out var position, out var velocity))
                        samples.Add(new LabMotionSample { id = orb.id, x = position.x, y = position.y,
                            vx = velocity.x, vy = velocity.y });
                }
                if (samples.Count > 0) Request(new LabNetworkPacket { type = "motion_batch", motionSamples = samples.ToArray() });
            }
        }

        private void ConfigureManager()
        {
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ProtocolVersion = LabProtocolVersion,
                ConnectionData = (byte[])LabConnectionToken.Clone(),
                EnableSceneManagement = false,
                ConnectionApproval = true,
                PlayerPrefab = null,
                ClientConnectionBufferTimeout = 10
            };
            manager.ConnectionApprovalCallback = Approve;
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool alreadyPending = pendingApprovals.ContainsKey(request.ClientNetworkId);
            bool allowed = manager.IsHost && order.Count + pendingApprovals.Count - (alreadyPending ? 1 : 0)
                    < targetParticipantCount && model == null
                && request.Payload != null && request.Payload.SequenceEqual(LabConnectionToken);
            if (allowed) pendingApprovals[request.ClientNetworkId] = Time.realtimeSinceStartupAsDouble;
            response.Approved = allowed;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = allowed ? string.Empty : "Lab version mismatch, room full, or battle started.";
        }

        private void RegisterHandler()
        {
            if (handlerRegistered || manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, Receive);
            handlerRegistered = true;
        }

        private void UnregisterHandler()
        {
            if (handlerRegistered && manager != null && manager.CustomMessagingManager != null)
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            handlerRegistered = false;
        }

        private void OnConnected(ulong id)
        {
            if (stopping || manager == null) return;
            RegisterHandler();
            if (manager.IsHost)
            {
                pendingApprovals.Remove(id);
                if (!order.Contains(id) && order.Count < targetParticipantCount) order.Add(id);
                else if (!order.Contains(id))
                {
                    manager.DisconnectClient(id);
                    return;
                }
                Debug.Log("C6_LAB_PEER_JOIN id=" + id + " participants=" + order.Count);
                BroadcastLobby();
                if (id != LocalPlayerId) SendSessionConfig(id);
                if (autoStart && CanStartBattle) StartBattle();
            }
            else if (id == manager.LocalClientId)
            {
                joinInProgress = false;
                joinRetryPending = false;
                discoveryJoinActive = false;
                DisposeDiscovery();
                SetStatus("Connected; waiting for Host settings.");
                Debug.Log("C6_LAB_CONNECTED local=" + id);
            }
        }

        private void OnDisconnected(ulong id)
        {
            if (stopping) return;
            pendingApprovals.Remove(id);
            if (manager != null && manager.IsHost)
            {
                if (!order.Remove(id))
                {
                    Debug.Log("C6_LAB_JOIN_REJECTED id=" + id);
                    return;
                }
                Debug.LogWarning("C6_LAB_PEER_LEFT id=" + id);
                if (model != null) { model = null; latest = null; if (board != null) board.Clear(); }
                configAcknowledgements.Remove(id);
                BroadcastLobby();
                SetStatus("A player left. Connect " + targetParticipantCount + " before starting.");
            }
            else
            {
                if (joinInProgress) { AdvanceJoinCandidate(); return; }
                stopAfterDisconnect = true;
                string reason = manager == null ? string.Empty : manager.DisconnectReason;
                SetStatus(string.IsNullOrEmpty(reason) ? "Connection ended. Rejoin the Host." : "Join failed: " + reason);
                Debug.LogWarning("C6_LAB_DISCONNECTED reason=" + reason);
            }
        }

        private void OnTransportFailure()
        {
            if (joinInProgress) { AdvanceJoinCandidate(); return; }
            SetStatus("Network transport failed. Check IP, port, and same network.");
            Debug.LogWarning("C6_LAB_TRANSPORT_FAILURE");
        }

        private void BroadcastLobby()
        {
            if (!IsHost) return;
            var packet = new LabNetworkPacket { type = "lobby", seats = order.ToArray(),
                targetParticipants = targetParticipantCount, configRevision = configRevision,
                configFingerprint = configFingerprint };
            foreach (ulong id in order) if (id != manager.LocalClientId) Send(id, packet);
            SetStatus("LOBBY: " + order.Count + " / " + targetParticipantCount
                + " · settings " + configAcknowledgements.Count + "/" + order.Count);
            Changed?.Invoke();
        }

        private void SendSessionConfig(ulong id)
        {
            if (!IsHost || config == null || id == LocalPlayerId) return;
            Send(id, new LabNetworkPacket { type = "config", targetParticipants = targetParticipantCount,
                configRevision = configRevision, configFingerprint = configFingerprint,
                tuning = config.CaptureDeveloperValues() });
        }

        private void PublishSnapshot(bool immediate)
        {
            if (!IsHost || model == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            latest = model.Snapshot(now);
            AnnounceBombExplosions();
            // Large experimental states are deliberately updated less often instead of queuing
            // many reliable fragments per peer until the network becomes unusable.
            nextSnapshotAt = now + ((latest.orbs?.Length ?? 0) > 100 ? .5d : .1d);
            SyncBoard();
            string[] pieces;
            try { pieces = LabSessionSettings.ChunkSnapshot(latest); }
            catch (ArgumentException e)
            {
                AbortOversizedSnapshot(e.Message);
                return;
            }
            long sequence = ++snapshotSequence;
            foreach (ulong id in order)
            {
                if (id == manager.LocalClientId) continue;
                for (int i = 0; i < pieces.Length; i++)
                {
                    bool sent = Send(id, new LabNetworkPacket { type = "snapshot_chunk", sessionId = sessionId,
                        configRevision = configRevision, configFingerprint = configFingerprint,
                        snapshotSequence = sequence, chunkIndex = i, chunkCount = pieces.Length,
                        chunkData = pieces[i] });
                    if (!sent)
                    {
                        AbortOversizedSnapshot("Could not send the complete battle state to all players.");
                        return;
                    }
                }
            }
            if (immediate) Debug.Log("C6_LAB_STATE phase=" + latest.phase + " hp=" + latest.hp + " remaining=" + latest.remaining.ToString("F1"));
            Changed?.Invoke();
        }

        private void AnnounceBombExplosions()
        {
            foreach (ulong owner in model.DrainBombExplosions())
            {
                Debug.Log("C6_LAB_BOMB_EXPLODED owner=" + owner);
                if (owner == LocalPlayerId) SetNotice(BombExplodedNotice);
                else Send(owner, new LabNetworkPacket { type = "notice", sessionId = sessionId, reason = BombExplodedNotice });
            }
        }

        private void AbortOversizedSnapshot(string reason)
        {
            Debug.LogError("C6_LAB_SNAPSHOT_ABORT " + reason);
            Stop();
            SetStatus("Room ended: state exceeded the safe network size.");
        }

        private void Request(LabNetworkPacket packet)
        {
            packet.sessionId = sessionId;
            if (IsHost) HandleRequest(LocalPlayerId, packet);
            else Send(NetworkManager.ServerClientId, packet);
        }

        private void HandleRequest(ulong sender, LabNetworkPacket packet)
        {
            if (!IsHost || model == null || packet == null || !order.Contains(sender)) return;
            double now = Time.realtimeSinceStartupAsDouble;
            bool accepted = false;
            string reason = "Rejected";
            if (packet.sessionId != sessionId) return;
            LabOrbState result = null;
            string uniqueRequestId = sender + ":" + packet.requestId;
            switch (packet.type)
            {
                case "generate":
                    accepted = model.TryGenerate(sender, uniqueRequestId, now, out result, out reason);
                    break;
                case "use":
                    accepted = model.TryUseItem(sender, packet.firstId, uniqueRequestId, now, out result, out reason);
                    break;
                case "combine":
                    accepted = model.TryCombine(sender, packet.firstId, packet.secondId, uniqueRequestId, now, out result, out reason);
                    break;
                case "transfer":
                    accepted = model.TryTransfer(sender, packet.firstId, packet.right, packet.y, new Vector2(packet.vx, packet.vy), uniqueRequestId, now, out result, out reason);
                    break;
                case "launch":
                    Vector2 swipe = new Vector2(packet.vx, packet.vy);
                    if (target == null || target.Hitbox == null || battleFloor == null)
                    {
                        reason = "Battle target or floor is unavailable.";
                        break;
                    }
                    if (!Finite(packet.x) || packet.x < 0f || packet.x > 1f
                        || !LabThrowMath.IsThrowGesture(swipe, packet.y, config))
                    {
                        reason = "Swipe the Combined orb upward to throw.";
                        break;
                    }
                    accepted = model.TryLaunch(sender, packet.firstId, uniqueRequestId, now, out result, out reason);
                    if (accepted) SpawnHostProjectile(result, swipe, packet.x);
                    break;
                case "motion":
                    if (Finite(packet.x) && Finite(packet.y) && Finite(packet.vx) && Finite(packet.vy))
                        accepted = model.TryUpdateMotion(sender, packet.firstId, new Vector2(packet.x, packet.y), new Vector2(packet.vx, packet.vy), now);
                    // Frequent or late motion samples are best-effort; never unlock an orb from them.
                    return;
                case "motion_batch":
                    if (packet.motionSamples == null || packet.motionSamples.Length > 100) return;
                    foreach (LabMotionSample motion in packet.motionSamples)
                    {
                        if (motion == null || !Finite(motion.x) || !Finite(motion.y)
                            || !Finite(motion.vx) || !Finite(motion.vy)) continue;
                        model.TryUpdateMotion(sender, motion.id, new Vector2(motion.x, motion.y),
                            new Vector2(motion.vx, motion.vy), now);
                    }
                    return;
            }
            if (accepted)
            {
                Debug.Log("C6_LAB_ACTION type=" + packet.type + " sender=" + sender + " orb=" + (result == null ? "" : result.id)
                    + (result == null ? "" : " owner=" + result.owner + " vx=" + result.vx.ToString("F2")));
                PublishSnapshot(true);
            }
            else if (sender == LocalPlayerId) RejectLocally(packet.firstId, reason);
            else Send(sender, new LabNetworkPacket { type = "reject", firstId = packet.firstId, reason = reason });
        }

        private void SpawnHostProjectile(LabOrbState orb, Vector2 releaseVelocity, float releaseX01)
        {
            if (orb == null || target == null || config == null) return;
            int seat = latest != null && latest.players != null ? Array.Find(latest.players, p => p.id == orb.owner)?.seat ?? order.IndexOf(orb.owner) : order.IndexOf(orb.owner);
            int seatCount = latest?.players?.Length ?? order.Count;
            Vector3 targetCenter = target.transform.position;
            Vector3 seatAnchor = LabThrowMath.SeatPosition(seat, seatCount, targetCenter,
                config.ThrowOriginRadius, config.ThrowOriginHeightOffset);
            Vector3 launchPosition = LabThrowMath.LaunchPosition(seat, seatCount, targetCenter, releaseX01, config);
            Vector3 velocity = LabThrowMath.LaunchVelocity(releaseVelocity, seatAnchor, targetCenter, config);
            var projectile = LabProjectile.Create(launchPosition, config, projectileRoot, orb.element);
            projectile.name = "Projectile " + orb.id;
            projectile.SetMissSurface(battleFloor);
            projectile.Launch(orb.id, orb.owner, velocity, config, target, OnProjectileResolved);
            var notice = new LabNetworkPacket { type = "projectile", firstId = orb.id, owner = orb.owner, element = (int)orb.element, x = launchPosition.x, y = launchPosition.y, z = launchPosition.z, wx = velocity.x, wy = velocity.y, wz = velocity.z };
            foreach (ulong id in order) if (id != LocalPlayerId) Send(id, notice);
            Debug.Log("C6_LAB_THROW orb=" + orb.id + " seat=" + seat
                + " swipe=" + releaseVelocity + " launch=" + velocity
                + " exponent=" + config.ThrowPowerExponent.ToString("F2", CultureInfo.InvariantCulture)
                + " loft=" + config.ThrowLoftOffsetDegrees.ToString("F1", CultureInfo.InvariantCulture)
                + " damping=" + config.ThrowAirDamping.ToString("F2", CultureInfo.InvariantCulture));
        }

        private void OnProjectileResolved(LabProjectileResult result)
        {
            if (!IsHost || model == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (result.Hit)
            {
                if (model.ConfirmHit(result.ProjectileId, now, out var element, out int actualDamage)
                    && target != null)
                {
                    Transform hitbox = target.Hitbox != null ? target.Hitbox.transform : target.transform;
                    Vector3 targetLocalPoint = hitbox.InverseTransformPoint(result.HitPoint);
                    target.GetComponent<LabHangingTarget>()?.Kick(result.ImpactVelocity);
                    impactFeedback?.PlayHit(target, targetLocalPoint, element, actualDamage);
                    var impact = new LabNetworkPacket { type = "targetImpact", sessionId = sessionId,
                        round = model.Round, firstId = result.ProjectileId,
                        element = (int)element, damage = actualDamage,
                        x = targetLocalPoint.x, y = targetLocalPoint.y, z = targetLocalPoint.z,
                        wx = result.ImpactVelocity.x, wy = result.ImpactVelocity.y, wz = result.ImpactVelocity.z };
                    foreach (ulong id in order) if (id != LocalPlayerId) Send(id, impact);
                }
            }
            else model.ConfirmMiss(result.ProjectileId, now);
            LastShotReport = string.Format(CultureInfo.InvariantCulture,
                "SHOT {0} | {1:F2}s | peak +{2:F2}m | range {3:F2}m",
                result.Hit ? "HIT" : "MISS", result.FlightSeconds,
                result.PeakRise, result.HorizontalDistance);
            Debug.Log("C6_LAB_PROJECTILE id=" + result.ProjectileId + " hit=" + result.Hit
                + " flight=" + result.FlightSeconds.ToString("F2", CultureInfo.InvariantCulture)
                + " peak=" + result.PeakRise.ToString("F2", CultureInfo.InvariantCulture)
                + " range=" + result.HorizontalDistance.ToString("F2", CultureInfo.InvariantCulture)
                + " contact=" + result.HitPoint);
            if (developerSolo) SetNotice(LastShotReport);
            PublishSnapshot(true);
        }

        private void SyncBoard()
        {
            if (board == null || latest == null) return;
            var current = new HashSet<string>();
            foreach (var orb in latest.orbs ?? Array.Empty<LabOrbState>())
            {
                if (orb.owner != LocalPlayerId || orb.inFlight) continue;
                current.Add(orb.id);
                bool showRise = orb.origin == LabOrbOrigin.Generated && orb.originOwner == LocalPlayerId
                    && orb.kind != LabOrbKind.Combined;
                // Every screen derives a bomb's fuse from the shared Host clock in the snapshot.
                float fuse = orb.kind == LabOrbKind.Bomb && config != null
                    ? Mathf.Max(0f, (float)(orb.createdAt + config.ItemLifetimeSeconds - latest.serverTime)) : -1f;
                board.UpsertOrb(orb.id, orb.kind, new Vector2(orb.x, orb.y),
                    new Vector2(orb.vx, orb.vy), true, showRise, orb.element, orb.lockSwipes, orb.relayStage, fuse);
            }
            foreach (string id in visibleOrbIds) if (!current.Contains(id)) board.RemoveOrb(id);
            visibleOrbIds.Clear();
            foreach (string id in current) visibleOrbIds.Add(id);
            board.SetInteractionEnabled(latest.phase == LabPhase.Playing);
        }

        private void OnEdgeCrossed(LabOrbEdgeCrossing edge)
        {
            if (latest == null || latest.phase != LabPhase.Playing) return;
            if (developerSolo)
            {
                // The single player has no neighbor. Restore the stopped edge orb immediately.
                board.RejectEdge(edge.OrbId);
                return;
            }
            board.SetOrbLocked(edge.OrbId, true);
            // The Host must receive the boundary sample before the ownership request.
            Request(new LabNetworkPacket { type = "motion", firstId = edge.OrbId, x = edge.ToRight ? 1f : 0f, y = edge.Height01, vx = edge.VelocityInBoardWidthsPerSecond.x, vy = edge.VelocityInBoardWidthsPerSecond.y });
            Request(new LabNetworkPacket { type = "transfer", requestId = ++requestSequence, firstId = edge.OrbId, right = edge.ToRight, y = edge.Height01, vx = edge.VelocityInBoardWidthsPerSecond.x, vy = edge.VelocityInBoardWidthsPerSecond.y });
        }

        private void OnCombineRequested(string firstId, string secondId, Vector2 center)
        {
            if (latest == null || latest.phase != LabPhase.Playing) return;
            Request(new LabNetworkPacket { type = "combine", requestId = ++requestSequence, firstId = firstId, secondId = secondId, x = center.x, y = center.y });
        }

        private void OnUseRequested(string orbId)
        {
            if (latest == null || latest.phase != LabPhase.Playing) return;
            Request(new LabNetworkPacket { type = "use", requestId = ++requestSequence, firstId = orbId });
        }

        private void OnThrowRequested(LabOrbThrowGesture gesture)
        {
            if (latest == null || latest.phase != LabPhase.Playing) return;
            board.SetOrbLocked(gesture.OrbId, true);
            Request(new LabNetworkPacket { type = "launch", requestId = ++requestSequence,
                firstId = gesture.OrbId, x = gesture.ReleaseX01,
                y = gesture.DistanceInBoardWidths, vx = gesture.Velocity.x, vy = gesture.Velocity.y });
        }

        private void Receive(ulong sender, FastBufferReader reader)
        {
            if (!TryRead(reader, out var packet)) return;
            if (IsHost)
            {
                if (sender == LocalPlayerId || !order.Contains(sender)) return;
                if (packet.type == "config_ack")
                {
                    if (packet.configRevision == configRevision
                        && packet.configFingerprint == configFingerprint)
                    {
                        configAcknowledgements.Add(sender);
                        BroadcastLobby();
                        if (autoStart && CanStartBattle) StartBattle();
                    }
                    return;
                }
                HandleRequest(sender, packet);
                return;
            }
            if (sender != NetworkManager.ServerClientId) return;
            switch (packet.type)
            {
                case "lobby":
                    if (packet.targetParticipants < 2 || packet.targetParticipants > 5
                        || packet.seats == null || packet.seats.Length > packet.targetParticipants) return;
                    order.Clear();
                    latest = null;
                    impactReceipts.Reset();
                    impactFeedback?.Clear();
                    visibleOrbIds.Clear();
                    if (board != null) board.Clear();
                    order.AddRange(packet.seats);
                    targetParticipantCount = packet.targetParticipants;
                    if (packet.configRevision != configRevision || packet.configFingerprint != configFingerprint)
                        clientConfigReady = false;
                    SetStatus("LOBBY: " + order.Count + " / " + targetParticipantCount
                        + (clientConfigReady ? " · settings confirmed" : " · waiting for settings"));
                    Debug.Log("C6_LAB_LOBBY participants=" + order.Count + " seats=" + string.Join(",", order));
                    break;
                case "config":
                    ReceiveSessionConfig(packet);
                    break;
                case "snapshot_chunk":
                    ReceiveSnapshotChunk(packet);
                    break;
                case "reject":
                    RejectLocally(packet.firstId, packet.reason);
                    break;
                case "notice":
                    if (packet.sessionId == sessionId) SetNotice(packet.reason);
                    break;
                case "targetImpact":
                    if (target != null && impactReceipts.TryAccept(packet.sessionId, packet.round,
                        packet.firstId, (LabOrbElement)packet.element, packet.damage,
                        new Vector3(packet.x, packet.y, packet.z),
                        new Vector3(packet.wx, packet.wy, packet.wz)))
                    {
                        target.GetComponent<LabHangingTarget>()?.Kick(new Vector3(packet.wx, packet.wy, packet.wz));
                        impactFeedback?.PlayHit(target, new Vector3(packet.x, packet.y, packet.z),
                            (LabOrbElement)packet.element, packet.damage);
                    }
                    break;
                case "projectile":
                    if (target != null && config != null && Finite(packet.x) && Finite(packet.y)
                        && Finite(packet.z) && Finite(packet.wx) && Finite(packet.wy) && Finite(packet.wz))
                    {
                        Vector3 position = new Vector3(packet.x, packet.y, packet.z);
                        Vector3 velocity = new Vector3(packet.wx, packet.wy, packet.wz);
                        LabOrbElement element = packet.element >= (int)LabOrbElement.None && packet.element <= (int)LabOrbElement.Earth
                            ? (LabOrbElement)packet.element : LabOrbElement.None;
                        LabProjectile visual = LabProjectile.Create(position, config, projectileRoot, element);
                        visual.SetMissSurface(battleFloor);
                        visual.LaunchVisual(velocity, config, target);
                    }
                    break;
            }
        }

        private void ReceiveSessionConfig(LabNetworkPacket packet)
        {
            if (packet.configRevision < 1 || packet.targetParticipants < 2 || packet.targetParticipants > 5
                || packet.tuning == null || packet.tuning.Length != LabConfig.DeveloperFields.Count) return;
            string expected;
            try { expected = LabSessionSettings.Fingerprint(packet.tuning, packet.targetParticipants, LabProtocolVersion); }
            catch (ArgumentException) { return; }
            if (expected != packet.configFingerprint)
            {
                SetStatus("Host settings failed integrity check. Rejoin the room.");
                return;
            }
            if (clientConfigReady && packet.configRevision == configRevision
                && packet.configFingerprint == configFingerprint)
            {
                Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "config_ack",
                    configRevision = configRevision, configFingerprint = configFingerprint });
                return;
            }
            if (packet.configRevision < configRevision) return;
            if (!TryInstallValues(packet.tuning, out string error))
            {
                SetStatus("Host settings rejected: " + error);
                return;
            }
            targetParticipantCount = packet.targetParticipants;
            configRevision = packet.configRevision;
            configFingerprint = packet.configFingerprint;
            clientConfigReady = true;
            Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "config_ack",
                configRevision = configRevision, configFingerprint = configFingerprint });
            SetStatus("Settings confirmed; waiting for " + targetParticipantCount + " players.");
            Debug.Log("C6_LAB_CONFIG_READY revision=" + configRevision + " fingerprint=" + configFingerprint);
        }

        private void ReceiveSnapshotChunk(LabNetworkPacket packet)
        {
            if (!clientConfigReady || packet.configRevision != configRevision
                || packet.configFingerprint != configFingerprint
                || string.IsNullOrEmpty(packet.sessionId) || packet.snapshotSequence < 1
                || packet.chunkCount < 1 || packet.chunkCount > 256
                || packet.chunkIndex < 0 || packet.chunkIndex >= packet.chunkCount
                || string.IsNullOrEmpty(packet.chunkData)
                || packet.chunkData.Length > LabSessionSettings.SnapshotChunkCharacters) return;
            if (packet.snapshotSequence < incomingSequence) return;
            if (packet.snapshotSequence > incomingSequence)
            {
                incomingSequence = packet.snapshotSequence;
                incomingChunkCount = packet.chunkCount;
                incomingChunks = new string[packet.chunkCount];
                incomingChunkReceived = 0;
            }
            if (packet.chunkCount != incomingChunkCount || incomingChunks == null) return;
            if (incomingChunks[packet.chunkIndex] == null)
            {
                incomingChunks[packet.chunkIndex] = packet.chunkData;
                incomingChunkReceived++;
            }
            else if (incomingChunks[packet.chunkIndex] != packet.chunkData) return;
            if (incomingChunkReceived != incomingChunkCount) return;
            string json = string.Concat(incomingChunks);
            incomingChunks = null;
            incomingChunkReceived = 0;
            if (json.Length > LabSessionSettings.MaximumSnapshotCharacters) return;
            try { if (Utf8.GetByteCount(json) > LabSessionSettings.MaximumSnapshotBytes) return; }
            catch (Exception) { return; }
            LabSnapshot snapshot;
            try { snapshot = JsonUtility.FromJson<LabSnapshot>(json); }
            catch (Exception) { return; }
            if (snapshot == null || snapshot.sessionId != packet.sessionId
                || snapshot.players == null || snapshot.players.Length != targetParticipantCount) return;
            if (latest != null && latest.sessionId == snapshot.sessionId && latest.round > snapshot.round) return;
            bool logTransition = latest == null || latest.sessionId != snapshot.sessionId || latest.phase != snapshot.phase;
            if (sessionId != snapshot.sessionId || latest == null || latest.round != snapshot.round)
            {
                target?.GetComponent<LabHangingTarget>()?.ResetSwing();
                impactFeedback?.Clear();
            }
            sessionId = snapshot.sessionId;
            latest = snapshot;
            impactReceipts.SetActive(snapshot.sessionId, snapshot.round);
            SyncBoard();
            if (logTransition && latest.phase == LabPhase.Playing) SetNotice(string.Empty);
            SetStatus(latest.phase + ": " + targetParticipantCount + " / " + targetParticipantCount);
            if (logTransition) Debug.Log("C6_LAB_SNAPSHOT phase=" + latest.phase + " hp=" + latest.hp
                + " remaining=" + latest.remaining.ToString("F1") + " orbs=" + (latest.orbs?.Length ?? 0));
        }

        private void RejectLocally(string orbId, string reason)
        {
            if (!string.IsNullOrEmpty(orbId) && board != null) board.RejectEdge(orbId);
            SetNotice(reason ?? "Request rejected.");
            Debug.LogWarning("C6_LAB_REJECT orb=" + orbId + " reason=" + reason);
        }

        private bool Send(ulong receiver, LabNetworkPacket packet)
        {
            if (manager == null || !manager.IsListening || manager.ShutdownInProgress || manager.CustomMessagingManager == null) return false;
            try
            {
                byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(packet));
                if (bytes.Length < 1 || bytes.Length > MaximumPacketBytes - 8) return false;
                using (var writer = new FastBufferWriter(bytes.Length + 8, Allocator.Temp))
                {
                    writer.WriteValueSafe(packet.protocol);
                    writer.WriteValueSafe(bytes.Length);
                    writer.WriteBytesSafe(bytes);
                    manager.CustomMessagingManager.SendNamedMessage(MessageName, receiver, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("C6_LAB_SEND_FAILED " + e.GetType().Name);
                return false;
            }
        }

        private static bool TryRead(FastBufferReader reader, out LabNetworkPacket packet)
        {
            packet = null;
            int left = reader.Length - reader.Position;
            if (left < 9 || left > MaximumPacketBytes || !reader.TryBeginRead(left)) return false;
            try
            {
                reader.ReadValueSafe(out int version);
                reader.ReadValueSafe(out int count);
                if (version != 2 || count < 2 || count != reader.Length - reader.Position) return false;
                var bytes = new byte[count];
                reader.ReadBytesSafe(ref bytes, count);
                packet = JsonUtility.FromJson<LabNetworkPacket>(Utf8.GetString(bytes));
                return packet != null && packet.protocol == 2 && !string.IsNullOrEmpty(packet.type);
            }
            catch (Exception) { return false; }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void ResetLocal()
        {
            ClearProjectiles();
            order.Clear();
            configAcknowledgements.Clear();
            pendingApprovals.Clear();
            visibleOrbIds.Clear();
            impactReceipts.Reset();
            impactFeedback?.Clear();
            model = null;
            developerSolo = false;
            latest = null;
            sessionId = null;
            configRevision = 0;
            configFingerprint = null;
            clientConfigReady = false;
            snapshotSequence = 0;
            incomingSequence = 0;
            incomingChunks = null;
            incomingChunkCount = 0;
            incomingChunkReceived = 0;
            LastShotReport = string.Empty;
            requestSequence = 0;
            if (board != null) board.Clear();
            Changed?.Invoke();
        }

        private void StartAdvertising()
        {
            DisposeDiscovery();
            if (!LabBonjourDiscovery.IsSupported) return;
            discovery = new LabBonjourDiscovery();
            discovery.Diagnostic += message => Debug.Log("C6_LAB_DISCOVERY " + message);
            discovery.Changed += OnDiscoveryChanged;
            advertisedName = "C6Lab-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            bool ipv4 = !(transport is LabDualStackTransport dual) || dual.HasIpv4Listener;
            bool ipv6 = transport is LabDualStackTransport second && second.HasIpv6Listener;
            if (!discovery.StartAdvertise(advertisedName, port, ipv4, ipv6))
            {
                Debug.LogWarning("C6_LAB_DISCOVERY_UNAVAILABLE " + discovery.LastError);
                DisposeDiscovery();
            }
            else Debug.Log("C6_LAB_LISTEN_FAMILIES ipv4=" + ipv4 + " ipv6=" + ipv6);
        }

        private void DisposeDiscovery()
        {
            if (discovery != null)
            {
                discovery.Changed -= OnDiscoveryChanged;
                discovery.Dispose();
                discovery = null;
            }
            roomSearchActive = false;
            discoveredRooms = Array.Empty<LabDiscoveredRoom>();
            discoveryRoomKey = null;
            advertisedName = null;
            Changed?.Invoke();
        }

        private void OnDiscoveryChanged()
        {
            if (roomSearchActive && discovery != null)
            {
                discoveredRooms = LabRoomCatalog.GroupTargets(discovery.Targets);
                Debug.Log("C6_LAB_ROOM_LIST count=" + discoveredRooms.Count);
            }
            Changed?.Invoke();
        }

        private void ClearProjectiles()
        {
            if (projectileRoot == null) return;
            for (int i = 0; i < projectileRoot.childCount; i++)
            {
                GameObject projectile = projectileRoot.GetChild(i).gameObject;
                projectile.SetActive(false);
                Destroy(projectile);
            }
        }

        private void SetStatus(string status) { Status = status; Changed?.Invoke(); }
        private void SetNotice(string notice) { Notice = notice; Changed?.Invoke(); }

        private void OnDestroy()
        {
            if (board != null)
            {
                board.EdgeCrossed -= OnEdgeCrossed;
                board.CombineRequested -= OnCombineRequested;
                board.ThrowRequested -= OnThrowRequested;
                board.UseRequested -= OnUseRequested;
            }
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnConnected;
                manager.OnClientDisconnectCallback -= OnDisconnected;
                manager.OnTransportFailure -= OnTransportFailure;
            }
            Stop();
        }
    }
}
