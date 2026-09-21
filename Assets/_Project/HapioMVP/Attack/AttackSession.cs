using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Attack
{
    /// <summary>Explicit T06 development session: actual NGO host authority and real collision only.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(DirectConnectionSession))]
    public sealed class AttackSession : MonoBehaviour
    {
        private string HelloMessage => continuousTransfersEnabled ? "C6.CE24.Hello.v1" : releaseThrowsEnabled ? "C6.P2.Hello.v1" : "C6.T06.Hello.v1";
        private string RequestMessage => continuousTransfersEnabled ? "C6.CE24.Transfer.v1" : releaseThrowsEnabled ? "C6.P2.Launch.v1" : "C6.T06.Launch.v1";
        private string QueryMessage => continuousTransfersEnabled ? "C6.CE24.Query.v1" : releaseThrowsEnabled ? "C6.P2.Query.v1" : "C6.T06.Query.v1";
        private string ReplyMessage => continuousTransfersEnabled ? "C6.CE24.Reply.v1" : releaseThrowsEnabled ? "C6.P2.Reply.v1" : "C6.T06.Reply.v1";
        private string SnapshotMessage => continuousTransfersEnabled ? "C6.CE24.Snapshot.v1" : releaseThrowsEnabled ? "C6.P2.Snapshot.v1" : "C6.T06.Snapshot.v1";
        private const int MaximumRequestsPerRoundPerParticipant = 256;
        private readonly Dictionary<ulong, string> participantNonces = new Dictionary<ulong, string>();
        private readonly HashSet<ulong> fixtureOwners = new HashSet<ulong>();
        private readonly Dictionary<ulong, HashSet<string>> seenRequests = new Dictionary<ulong, HashSet<string>>();
        private readonly Dictionary<string, OrbActionRequest> submitted = new Dictionary<string, OrbActionRequest>(StringComparer.Ordinal);
        private readonly Dictionary<string, HostProjectile3D> projectiles = new Dictionary<string, HostProjectile3D>(StringComparer.Ordinal);
        private DirectConnectionSession connection;
        private ScreenLayoutConfig config;
        private AttackLaunchFrame launchFrame;
        private MonsterHitTarget target;
        private Material projectileMaterial;
        private NetworkManager manager;
        private CustomMessagingManager messaging;
        private bool explicitDevelopmentRequested;
        private bool intentionalEnd;
        private bool resetting;
        private string localNonce;
        private float nextHelloAt;
        private int helloAttempts;
        private float nextPoseAt;
        private ulong revision;
        private int totalHits;
        private int resets;
        private bool useT06Fixtures = true;
        private int maximumSnapshotOrbs = 12;
        private int maximumSnapshotBytes = AttackWire.MaximumBytes;
        private readonly Dictionary<string, AttackRequestReply> aggregateReplies = new Dictionary<string, AttackRequestReply>(StringComparer.Ordinal);
        private bool confirmingAggregateReplies;
        private bool transfersEnabled;
        private int transferStorageLimit;
        private float transferEdgeInset;
        private bool releaseThrowsEnabled;
        private bool continuousTransfersEnabled;
        public bool ContinuousTransfersEnabled => continuousTransfersEnabled;
        public double MotionServerTime => manager != null && manager.IsListening ? manager.ServerTime.Time : 0d;
        public void ConfigureContinuousTransfers(bool enabled)
        {
            if (explicitDevelopmentRequested || manager != null)
                throw new InvalidOperationException("Configure continuous transfer motion before gameplay attaches.");
            continuousTransfersEnabled = enabled;
        }
        private ulong[] approvedRoster;
        public bool MultiplayerRosterEnabled => approvedRoster != null;
        public int ParticipantCapacity => MultiplayerRosterEnabled ? ParticipantRing.MaximumPlayers : 2;
        public IReadOnlyList<ulong> OrderedParticipantIds => approvedRoster == null ? Array.Empty<ulong>() : Array.AsReadOnly(approvedRoster);
        public void ConfigureRoster(ulong[] ids)
        {
            if (explicitDevelopmentRequested || manager != null) throw new InvalidOperationException("Configure the frozen roster before gameplay attaches.");
            if (!ParticipantRing.Validate(ids) || ids[0] != NetworkManager.ServerClientId)
                throw new ArgumentException("A Host-first ordered roster of two to five unique participants is required.", nameof(ids));
            if (config != null && config.OrbStorageLimit > ParticipantRing.MaximumStoredPerPlayer)
                throw new InvalidOperationException("Multiplayer storage cannot exceed the bounded per-player protocol capacity.");
            approvedRoster = (ulong[])ids.Clone();
            useT06Fixtures = false;
            maximumSnapshotOrbs = ParticipantRing.MaximumLiveOrbs;
            maximumSnapshotBytes = AttackWire.MaximumMultiplayerInventoryBytes;
        }
        public bool ReleaseThrowsEnabled => releaseThrowsEnabled;
        public void ConfigureReleaseThrows(bool enabled)
        {
            if (explicitDevelopmentRequested || manager != null)
                throw new InvalidOperationException("Configure release throws before gameplay attaches.");
            releaseThrowsEnabled = enabled;
        }
        public bool TransfersEnabled => transfersEnabled;
        public void ConfigureTransfers(bool enabled, int storageLimit, float edgeInset)
        {
            if (explicitDevelopmentRequested || manager != null) throw new InvalidOperationException("Configure transfers before gameplay attaches.");
            if (!HostOrbRegistry.TransferTuningValid(storageLimit, edgeInset)) throw new ArgumentOutOfRangeException(nameof(storageLimit));
            transfersEnabled = enabled; transferStorageLimit = storageLimit; transferEdgeInset = edgeInset;
        }
        private bool aggregateMode;
        private string approvedSessionId;
        private uint approvedRoundId;
        public string LocalNonce => localNonce;
        public bool AggregateMode => aggregateMode;
        public void SetAggregateMode(bool enabled)
        {
            if (explicitDevelopmentRequested || manager != null) throw new InvalidOperationException("Choose snapshot mode before binding gameplay.");
            aggregateMode = enabled;
        }
        // Opt-in T10-B attachment keeps the already approved Lobby transport alive.
        public bool BeginApprovedConnection(string approvedSession, uint approvedRound)
        {
            if (!aggregateMode || explicitDevelopmentRequested || !AttackWire.ValidId(approvedSession) || approvedRound == 0
                || !isActiveAndEnabled || connection == null || connection.State != DirectConnectionState.Connected
                || config == null || launchFrame == null || target == null || !target.isActiveAndEnabled) return false;
            BeginLocalAttempt();
            approvedSessionId = approvedSession; approvedRoundId = approvedRound;
            Status = "Attaching the confirmed room to gameplay.";
            RefreshBinding();
            return CanProcess;
        }
        public bool ApplyAggregateSnapshot(AttackSnapshot incoming, bool notify = false)
        {
            if (!aggregateMode || !CanProcess || manager.IsHost || incoming == null || incoming.nonce != localNonce
                || !AttackWire.ValidSnapshot(incoming, maximumSnapshotOrbs) || !ValidMotionMode(incoming) || incoming.sessionId != approvedSessionId
                || incoming.roundId < approvedRoundId) return false;
            if (Snapshot != null)
            {
                if (incoming.sessionId != Snapshot.sessionId || incoming.roundId < Snapshot.roundId
                    || incoming.revision < Snapshot.revision || incoming.totalHits < Snapshot.totalHits || incoming.resets < Snapshot.resets) return false;
                if (incoming.revision == Snapshot.revision)
                    return JsonUtility.ToJson(incoming) == JsonUtility.ToJson(Snapshot);
            }
            else if (incoming.roundId != approvedRoundId) return false;
            bool changedRound = Snapshot == null || incoming.roundId != Snapshot.roundId;
            if (changedRound) { submitted.Clear(); aggregateReplies.Clear(); LastResult = null; }
            Snapshot = incoming;
            Status = "Confirmed room game state synchronized.";
            if (notify) NotifyAggregateChanged();
            return true;
        }
        public void NotifyAggregateChanged()
        {
            if (!aggregateMode) return;
            // Every component of the aggregate is already installed before any observer runs.
            Changed?.Invoke();
            if (confirmingAggregateReplies) return;
            confirmingAggregateReplies = true;
            try
            {
                foreach (var reply in aggregateReplies.Values.ToArray())
                    if (AggregateReplyConfirmed(reply)) DeliverReply(reply);
            }
            finally { confirmingAggregateReplies = false; }
        }
        private bool AggregateReplyConfirmed(AttackRequestReply reply)
        {
            if (!aggregateMode || !reply.known) return true;
            if (reply.pending || Snapshot == null || reply.inventoryRevision == 0
                || Snapshot.revision < reply.inventoryRevision) return false;
            var orb = Snapshot.orbs.FirstOrDefault(value => value.id == reply.orbId);
            // Active records omit Consumed; a later authoritative inventory plus its accepted
            // receipt confirms removal without inventing another orb or reusing a local Idle view.
            OrbActionRequest request = null;
            bool transfer = transfersEnabled && submitted.TryGetValue(reply.requestId, out request) && HostOrbRegistry.IsTransfer(request.Kind);
            if (transfer)
            {
                if (orb == null) return true; // A later snapshot may already have consumed it.
                if (!IsParticipant(orb.owner)) return false;
                // The receiver may have transferred the same ID back before this reply arrives.
                // Count/sequence history, rather than owner != sender, proves the original handoff.
                return !reply.accepted || orb.transferCount > 0 && orb.lastTransferSequence >= request.SequenceNumber;
            }
            if (reply.accepted) return orb == null || orb.owner == LocalPlayerId && orb.state != (int)OrbAuthorityState.Idle;
            return orb == null || orb.owner == LocalPlayerId;
        }

        private Func<bool> canAct;
        private Func<double, bool> beforeHostHit;
        private Action<double, AttackHitResult> afterRewardedHit;

        // Optional T09 hooks. The preserved T06-T08 scenes configure none and keep their original behavior.
        public void ConfigureBattleHooks(Func<bool> gameplayGate, Func<double, bool> beforeHit,
            Action<double, AttackHitResult> afterHit)
        {
            if (Connected) throw new InvalidOperationException("Configure battle hooks before starting a connection.");
            canAct = gameplayGate; beforeHostHit = beforeHit; afterRewardedHit = afterHit;
        }

        public void EndGameplayRound()
        {
            if (!IsHost) return;
            Authority.EndDevelopmentRound();
            CancelAllProjectiles();
            PublishHostSnapshot("t09-gameplay-ended");
        }

        // Configure before an attempt; the preserved T06 scene keeps its original fixture and bounds.
        public void ConfigureInventory(bool useT06Fixtures, int maximumSnapshotOrbs = 12)
        {
            if (explicitDevelopmentRequested || manager != null)
                throw new InvalidOperationException("Configure inventory before starting a connection.");
            if (maximumSnapshotOrbs < 1 || maximumSnapshotOrbs > 64)
                throw new ArgumentOutOfRangeException(nameof(maximumSnapshotOrbs));
            this.useT06Fixtures = useT06Fixtures;
            this.maximumSnapshotOrbs = maximumSnapshotOrbs;
            maximumSnapshotBytes = maximumSnapshotOrbs > 12 ? AttackWire.MaximumInventoryBytes : AttackWire.MaximumBytes;
        }
        public IReadOnlyList<ulong> AuthenticatedPlayerIds => !IsHost ? Array.Empty<ulong>() : approvedRoster != null
            ? approvedRoster.Where(value => fixtureOwners.Contains(value) && IsParticipant(value)).ToArray()
            : fixtureOwners.Where(IsParticipant).OrderBy(value => value).ToArray();
        public string NonceForPlayer(ulong playerId) => !IsHost ? null : playerId == LocalPlayerId
            ? localNonce : participantNonces.TryGetValue(playerId, out var nonce) ? nonce : null;
        public bool IsAuthenticatedPlayer(ulong sender, string nonce) => IsHost && IsParticipant(sender)
            && AttackWire.ValidNonce(nonce) && (sender == LocalPlayerId
                ? string.Equals(localNonce, nonce, StringComparison.Ordinal) : HasNonce(sender, nonce));
        public void PublishInventoryChange(string stage)
        {
            if (IsHost) PublishHostSnapshot(stage);
        }

        public DirectConnectionSession Connection => connection;
        public bool IsHost => CanProcess && manager.IsHost && Authority != null;
        public bool Connected => CanProcess && Snapshot != null && !string.IsNullOrEmpty(Snapshot.sessionId);
        public ulong LocalPlayerId => manager != null ? manager.LocalClientId : 0;
        public AttackSnapshot Snapshot { get; private set; }
        public AttackAuthority Authority { get; private set; }
        public HostOrbRegistry Registry { get; private set; }
        public AttackRequestReply LastResult { get; private set; }
        public string Status { get; private set; } = "Start a DEV host or join manually.";
        public int ActiveProjectileCount => projectiles.Count;
        /// <summary>Host presentation only: the live physics body transform of an in-flight orb, or null.</summary>
        public Transform ProjectileTransform(string orbId) => IsHost && orbId != null && projectiles.TryGetValue(orbId, out var projectile)
            && projectile != null && !projectile.HasCompleted ? projectile.transform : null;
        public event Action Changed;
        public event Action<AttackRequestReply> RequestResolved;
        public event Action<AttackHitResult> ValidHit;
        /// <summary>Host presentation only: the applied hit with the real physics contact position.</summary>
        public event Action<AttackHitResult, Vector3> ValidHitAt;
        /// <summary>Host presentation only: a launched orb ended by its lifetime without a valid hit.</summary>
        public event Action<ProjectileOutcome> ProjectileMissed;

        public void Configure(DirectConnectionSession session, ScreenLayoutConfig sharedConfig,
            AttackLaunchFrame frame, MonsterHitTarget hitTarget, Material material)
        {
            if (connection != null) connection.Changed -= RefreshBinding;
            connection = session;
            config = sharedConfig;
            launchFrame = frame;
            target = hitTarget;
            projectileMaterial = material;
            if (isActiveAndEnabled && connection != null) connection.Changed += RefreshBinding;
            RefreshBinding();
        }

        private void Awake() => connection = GetComponent<DirectConnectionSession>();
        private void OnEnable()
        {
            if (connection == null) connection = GetComponent<DirectConnectionSession>();
            if (connection != null) connection.Changed += RefreshBinding;
            RefreshBinding();
        }

        public bool Host(string port)
        {
            if (aggregateMode || !CanStartDevelopment()) return false;
            BeginLocalAttempt();
            bool result = connection.StartHost(port);
            if (!result) explicitDevelopmentRequested = false;
            RefreshBinding();
            return result;
        }

        public bool Join(string address, string port)
        {
            if (aggregateMode || !CanStartDevelopment()) return false;
            BeginLocalAttempt();
            bool result = connection.Join(address, port);
            if (!result) explicitDevelopmentRequested = false;
            RefreshBinding();
            return result;
        }

        private bool CanStartDevelopment() => (Application.isEditor || Debug.isDebugBuild)
            && isActiveAndEnabled && connection != null && connection.CanStart
            && config != null && launchFrame != null && target != null && target.isActiveAndEnabled;

        private void BeginLocalAttempt()
        {
            ResetBinding(AttackBattleState.Ended, false);
            explicitDevelopmentRequested = true;
            intentionalEnd = false;
            localNonce = Guid.NewGuid().ToString("N");
            Snapshot = null;
            LastResult = null;
            Status = "Connecting to the explicit DEV physics fixture...";
        }

        public bool ResetDevelopmentRound()
        {
            if (!IsHost || Registry == null || Registry.RoundId == uint.MaxValue) return false;
            Authority.EndDevelopmentRound();
            CancelAllProjectiles();
            Registry.ResetRound(Registry.RoundId + 1);
            Authority.BeginDevelopmentRound();
            resets++;
            seenRequests.Clear();
            submitted.Clear();
            aggregateReplies.Clear();
            LastResult = null;
            foreach (var owner in fixtureOwners.OrderBy(value => value)) SupplyFixture(owner);
            Status = useT06Fixtures ? "DEV target reset: HP restored and five Combined fixtures per participant." : "Normal round reset: HP restored and empty inventory.";
            Debug.Log($"C6_T06_RESET session={Registry.SessionId} round={Registry.RoundId} resets={resets} totalHits={totalHits}");
            PublishHostSnapshot("reset");
            return true;
        }

        public void EndDevelopmentTest()
        {
            intentionalEnd = true;
            if (IsHost)
            {
                Authority.EndDevelopmentRound();
                CancelAllProjectiles();
                PublishHostSnapshot("ended");
            }
            explicitDevelopmentRequested = false;
            if (connection != null) connection.Stop();
            ResetBinding(AttackBattleState.Ended, true);
        }

        public void FailUnconfirmedRequest() => FailNetwork("Host confirmation did not arrive; the unresolved request was not replayed.");

        public void FailNetwork(string reason)
        {
            intentionalEnd = false;
            if (IsHost)
            {
                Authority.EndDevelopmentRound(AttackBattleState.NetworkError);
                CancelAllProjectiles();
                PublishHostSnapshot("network-error");
            }
            explicitDevelopmentRequested = false;
            if (connection != null) connection.Stop();
            ResetBinding(AttackBattleState.NetworkError, false);
            Status = "NETWORK_ERROR: " + (reason ?? "End this connection and retry manually.");
            Debug.Log("C6_T06_NETWORK_ERROR unresolvedRequestsAreNotReplayed=true");
            Changed?.Invoke();
        }

        public bool Submit(OrbActionRequest request)
        {
            if (!Connected || !ValidRequestShape(request)) return false;
            if (submitted.TryGetValue(request.RequestId, out var previous) && !AttackAuthority.SamePayload(previous, request))
                return false;
            submitted[request.RequestId] = request;
            if (IsHost)
            {
                ProcessLaunch(LocalPlayerId, localNonce, request, true);
                return true;
            }
            bool sent = Send(RequestMessage, NetworkManager.ServerClientId, AttackRequestPacket.FromRequest(localNonce, request));
            Status = sent ? "Launch request pending Host confirmation." : "Send failed; checking Host state is required.";
            Changed?.Invoke();
            return sent;
        }

        public bool QueryPending(OrbActionRequest request)
        {
            if (!Connected || !ValidRequestShape(request)) return false;
            if (submitted.TryGetValue(request.RequestId, out var previous) && !AttackAuthority.SamePayload(previous, request))
                return false;
            if (!submitted.ContainsKey(request.RequestId)) submitted.Add(request.RequestId, request);
            if (IsHost)
            {
                DeliverReply(MakeReply(LocalPlayerId, localNonce, request));
                return true;
            }
            // A query never sends another launch or creates a replacement OrbId.
            return Send(QueryMessage, NetworkManager.ServerClientId, AttackRequestPacket.FromRequest(localNonce, request));
        }

        private bool CanProcess => !resetting && explicitDevelopmentRequested && isActiveAndEnabled
            && connection != null && connection.State == DirectConnectionState.Connected
            && manager != null && manager.IsListening && !manager.ShutdownInProgress && messaging != null;

        private void Update()
        {
            RefreshBinding();
            if (!CanProcess) return;
            if (!manager.IsHost && Snapshot == null && helloAttempts < 8 && Time.unscaledTime >= nextHelloAt) SendHello();
            if (IsHost && projectiles.Count > 0 && Time.unscaledTime >= nextPoseAt)
            {
                nextPoseAt = Time.unscaledTime + 1f / config.AttackSnapshotRateHz;
                PublishHostSnapshot(null);
            }
        }

        private void RefreshBinding()
        {
            if (resetting || connection == null) return;
            var candidate = connection.OwnedManager;
            bool active = explicitDevelopmentRequested && isActiveAndEnabled && config != null
                && launchFrame != null && target != null && connection.State == DirectConnectionState.Connected
                && candidate != null && candidate.IsListening && !candidate.ShutdownInProgress;
            if (!active)
            {
                if (messaging != null || Authority != null)
                {
                    var terminal = intentionalEnd ? AttackBattleState.Ended : AttackBattleState.NetworkError;
                    ResetBinding(terminal, true);
                }
                return;
            }
            var candidateMessaging = candidate.CustomMessagingManager;
            if (candidateMessaging == null || candidate == manager && ReferenceEquals(messaging, candidateMessaging)) return;
            ResetBinding(AttackBattleState.Ended, false);
            manager = candidate;
            messaging = candidateMessaging;
            messaging.RegisterNamedMessageHandler(HelloMessage, ReceiveHello);
            messaging.RegisterNamedMessageHandler(RequestMessage, ReceiveRequest);
            messaging.RegisterNamedMessageHandler(QueryMessage, ReceiveQuery);
            messaging.RegisterNamedMessageHandler(ReplyMessage, ReceiveReply);
            messaging.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
            if (string.IsNullOrEmpty(localNonce)) localNonce = Guid.NewGuid().ToString("N");
            if (manager.IsHost)
            {
                Registry = new HostOrbRegistry(true);
                Registry.BeginSession(aggregateMode ? approvedSessionId : Guid.NewGuid().ToString("N"), aggregateMode ? approvedRoundId : 1);
                // #29: a frozen multiplayer roster picks the per-player HP; legacy 2-player mode keeps MonsterMaxHp.
                Authority = new AttackAuthority(Registry, config.MonsterMaxHpFor(OrderedParticipantIds.Count), config.BaseDamage);

                if (releaseThrowsEnabled)
                {
                    Authority.ConfigureReleaseThrows(
                        launchFrame.Basis,
                        CurrentThrowTuning()
                    );

                    if (MultiplayerRosterEnabled)
                    {
                        Authority.ConfigureParticipantThrowFrames(
                            approvedRoster
                        );
                    }
                }
                if (MultiplayerRosterEnabled) Authority.ConfigureFlightCapacity(ParticipantRing.MaximumFlyingPerPlayer);
                if (continuousTransfersEnabled) Authority.ConfigureContinuousTransfers(config.OrbFloorDeceleration,
                    config.OrbStopSpeed, config.OrbMaxReleaseSpeed * OrbTransferMotion.MaximumReleaseSpeedMultiplier);
                Authority.BeginDevelopmentRound();
                totalHits = resets = 0;
                revision = 0;
                fixtureOwners.Add(manager.LocalClientId);
                SupplyFixture(manager.LocalClientId);
                Status = "DEV Host ready. Real Rigidbody / Collider hits only.";
                PublishHostSnapshot("host-ready");
            }
            else
            {
                Snapshot = null;
                Status = "Waiting for Host fixture state...";
                SendHello();
            }
        }

        private void SupplyFixture(ulong owner)
        {
            if (!useT06Fixtures) return;
            for (int index = 0; index < 5; index++)
                Registry.RegisterDevelopmentOrb(owner, OrbKind.Combined, OrbPolarity.None, new Vector2((index + .5f) / 6f, .6f));
            Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, OrbPolarity.Yin, new Vector2(5.5f / 6f, .6f));
            Debug.Log($"C6_T06_FIXTURE session={Registry.SessionId} round={Registry.RoundId} owner={owner} combined=5 raw=1 mode=DEV_PHYSICS");
        }

        private void SendHello()
        {
            if (!CanProcess || manager.IsHost) return;
            helloAttempts++;
            nextHelloAt = Time.unscaledTime + 1f;
            Send(HelloMessage, NetworkManager.ServerClientId, new AttackHello { nonce = localNonce });
            Changed?.Invoke();
        }

        private bool IsParticipant(ulong sender) => connection != null && connection.ParticipantIds.Contains(sender)
            && (approvedRoster == null || Array.IndexOf(approvedRoster, sender) >= 0);
        private bool IsRemoteParticipant(ulong sender) => IsHost && sender != manager.LocalClientId && IsParticipant(sender);
        private bool HasNonce(ulong sender, string nonce) => participantNonces.TryGetValue(sender, out var expected)
            && string.Equals(nonce, expected, StringComparison.Ordinal);

        private void ReceiveHello(ulong sender, FastBufferReader reader)
        {
            if (!IsRemoteParticipant(sender) || !AttackWire.TryRead<AttackHello>(reader, out var hello)
                || !AttackWire.ValidNonce(hello.nonce)) return;
            if (participantNonces.TryGetValue(sender, out var existing) && !string.Equals(existing, hello.nonce, StringComparison.Ordinal))
            { Drop("HELLO_NONCE_CHANGED"); return; }
            participantNonces[sender] = hello.nonce;
            if (fixtureOwners.Add(sender))
            {
                SupplyFixture(sender);
                PublishHostSnapshot("participant-fixture");
            }
            else SendSnapshot(sender, hello.nonce, Snapshot);
        }

        private void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (!IsRemoteParticipant(sender) || !AttackWire.TryRead<AttackRequestPacket>(reader, out var packet)
                || !HasNonce(sender, packet.nonce) || !ValidPacketShape(packet)) return;
            // The transport callback sender is authoritative; the payload has no claimed sender field.
            ProcessLaunch(sender, packet.nonce, packet.ToRequest(), false);
        }

        private void ReceiveQuery(ulong sender, FastBufferReader reader)
        {
            if (!IsRemoteParticipant(sender) || !AttackWire.TryRead<AttackRequestPacket>(reader, out var packet)
                || !HasNonce(sender, packet.nonce) || !ValidPacketShape(packet)) return;
            Send(ReplyMessage, sender, MakeReply(sender, packet.nonce, packet.ToRequest()));
            SendSnapshot(sender, packet.nonce, Snapshot);
        }

        private void ProcessLaunch(ulong sender, string nonce, OrbActionRequest request, bool local)
        {
            if (!IsHost || !IsParticipant(sender)) return;
            // A stale request must not be rewrapped as a reply for the current session or round.
            if (request.SessionId != Registry.SessionId || request.RoundId != Registry.RoundId)
            { Drop("LAUNCH_CONTEXT"); return; }
            if (!seenRequests.TryGetValue(sender, out var requests))
                seenRequests[sender] = requests = new HashSet<string>(StringComparer.Ordinal);
            if (!requests.Contains(request.RequestId) && requests.Count >= MaximumRequestsPerRoundPerParticipant)
            { Drop("REQUEST_LIMIT"); return; }
            requests.Add(request.RequestId);
            bool transfer = transfersEnabled && HostOrbRegistry.IsTransfer(request.Kind);
            AttackLaunchResult result;
            if (transfer)
            {
                ulong receiver = sender;
                bool readyPeer;
                if (approvedRoster != null)
                    readyPeer = ParticipantRing.TryNeighbour(approvedRoster, sender, request.Kind == OrbActionKind.TransferRight, out receiver)
                        && AuthenticatedPlayerIds.Contains(sender) && AuthenticatedPlayerIds.Contains(receiver) && IsParticipant(receiver);
                else
                {
                    var peers = AuthenticatedPlayerIds.Where(value => value != sender).ToArray();
                    readyPeer = AuthenticatedPlayerIds.Contains(sender) && peers.Length == 1 && IsParticipant(peers[0]);
                    if (readyPeer) receiver = peers[0];
                }
                result = Authority.RequestTransfer(sender, request, receiver, readyPeer,
                    transferStorageLimit, transferEdgeInset, canAct?.Invoke() ?? true, MotionServerTime);
                Debug.Log($"C6_T11_TRANSFER session={Registry.SessionId} round={Registry.RoundId} sender={sender} request={request.RequestId} orb={request.OrbId} direction={request.Kind} accepted={result.Accepted} duplicate={result.IsDuplicate} reason={result.Reason} receiver={result.Orb?.OwnerPlayerId} transferCount={result.Orb?.TransferCount}");
            }
            else result = Authority.RequestLaunch(sender, request, canAct?.Invoke() ?? true);
            Debug.Log($"C6_T06_LAUNCH session={Registry.SessionId} round={Registry.RoundId} sender={sender} request={request.RequestId} orb={request.OrbId} accepted={result.Accepted} duplicate={result.IsDuplicate} reason={result.Reason}");
            if (result.SpawnRequired) SpawnProjectile(result.Orb, result.BallisticLaunch);
            PublishHostSnapshot(transfer ? "transfer-result" : "launch-result");
            var reply = MakeReply(sender, nonce, request);
            reply.duplicate = result.IsDuplicate;
            reply.reason = result.Reason;
            if (local) DeliverReply(reply); else Send(ReplyMessage, sender, reply);
        }

        private ThrowTuning CurrentThrowTuning() => new ThrowTuning(config.ThrowMinUpSpeed, config.ThrowMaxInputSpeed,
            config.ThrowForwardGain, config.ThrowUpGain, config.ThrowLateralGain, config.ThrowMaxWorldSpeed,
            config.ThrowGravity, config.ThrowBounce, config.ThrowLifetime, config.ProjectileRadius,
            config.ThrowSampleWindow, config.ThrowMinDuration);

        private void SpawnProjectile(OrbRecord orb, BallisticLaunch? ballisticLaunch = null)
        {
            string sessionId = Registry.SessionId;
            uint roundId = Registry.RoundId;
            try
            {
                HostProjectile3D projectile;
                if (releaseThrowsEnabled)
                {
                    if (!ballisticLaunch.HasValue) throw new InvalidOperationException("An approved release launch is required.");
                    projectile = HostProjectile3D.SpawnBallistic(IsHost, orb.OrbId, orb.OwnerPlayerId, ballisticLaunch.Value,
                        outcome => ProcessOutcome(sessionId, roundId, outcome), transform, projectileMaterial, target.gameObject.layer);
                }
                else
                {
                    var pose = LaunchMapping.Calculate(orb.NormalizedPosition, launchFrame.Basis);
                    var tuning = new ProjectilePhysicsTuning(config.ProjectileSpeed, config.ProjectileLifetime, config.ProjectileRadius);
                    projectile = HostProjectile3D.Spawn(IsHost, orb.OrbId, orb.OwnerPlayerId, pose, tuning,
                        outcome => ProcessOutcome(sessionId, roundId, outcome), transform, projectileMaterial, target.gameObject.layer);
                }
                if (!Authority.MarkProjectileSpawned(sessionId, roundId, orb.OrbId))
                {
                    projectile.CancelAndDestroy();
                    Authority.ExpireProjectile(sessionId, roundId, orb.OrbId);
                    return;
                }
                projectiles.Add(orb.OrbId, projectile);
                Debug.Log($"C6_T06_PROJECTILE session={sessionId} round={roundId} orb={orb.OrbId} attacker={orb.OwnerPlayerId} state=Projectile position={projectile.transform.position} speed={projectile.InitialVelocity.magnitude} lifetime={projectile.Lifetime} ballistic={projectile.BallisticActive}");
            }
            catch (Exception exception)
            {
                Authority.ExpireProjectile(sessionId, roundId, orb.OrbId);
                Status = "Host projectile setup failed; the confirmed orb was consumed without a hit.";
                Debug.LogError("C6_T06_SPAWN_FAILED type=" + exception.GetType().Name);
            }
        }

        private void ProcessOutcome(string sessionId, uint roundId, ProjectileOutcome outcome)
        {
            if (!IsHost || Registry.SessionId != sessionId || Registry.RoundId != roundId
                || !projectiles.Remove(outcome.OrbId) || !Registry.TryGet(outcome.OrbId, out var orb)
                || orb.OwnerPlayerId != outcome.AttackerPlayerId) return;
            AttackHitResult appliedHit = null;
            double processingNow = Time.realtimeSinceStartupAsDouble;
            bool usedBattleHitHook = false;
            try
            {
                int hitsBefore = Authority.ValidHitCount;
                try
                {
                    bool validTarget = outcome.Kind == ProjectileOutcomeKind.Hit && target != null && target.isActiveAndEnabled
                        && string.Equals(outcome.TargetId, target.TargetId, StringComparison.Ordinal);
                    bool timeAllowed = true;
                    if (validTarget && beforeHostHit != null)
                    {
                        usedBattleHitHook = true;
                        timeAllowed = beforeHostHit(processingNow);
                    }
                    if (validTarget && timeAllowed)
                    {
                        var hit = Authority.ProcessHostHit(sessionId, roundId, outcome.OrbId);
                        if (hit.Applied)
                        {
                            appliedHit = hit;
                            Status = "Confirmed real Host collision.";
                            Debug.Log($"C6_T06_HIT session={sessionId} round={roundId} orb={hit.OrbId} attacker={hit.AttackerPlayerId} damage={hit.Damage} hpBefore={hit.HpBefore} hpAfter={hit.HpAfter} totalHits={totalHits + 1} physicsElapsed={outcome.ElapsedPhysicsTime} fixedTime={outcome.ObservedFixedTime}");
                        }
                    }
                    else
                    {
                        Authority.ExpireProjectile(sessionId, roundId, outcome.OrbId);
                        Status = timeAllowed ? "Projectile expired or its target was rejected; no damage or recovery."
                            : "Host clock rejected this collision; no damage or recovery.";
                        Debug.Log($"C6_T06_EXPIRED session={sessionId} round={roundId} orb={outcome.OrbId} attacker={outcome.AttackerPlayerId} kind={outcome.Kind} physicsElapsed={outcome.ElapsedPhysicsTime} battleTimeAllowed={timeAllowed}");
                    }
                }
                finally
                {
                    // HP/Consumed and physics commit before resource recovery and the battle result.
                    totalHits += Mathf.Max(0, Authority.ValidHitCount - hitsBefore);
                    if (Authority.State == AttackBattleState.TargetCleared) CancelAllProjectiles();
                    PublishHostSnapshot("physics-result");
                }
                if (appliedHit != null)
                {
                    try { ValidHit?.Invoke(appliedHit); }
                    catch (Exception exception) { Debug.LogWarning("C6_T06_HOOK_FAILED type=" + exception.GetType().Name); }
                    try { ValidHitAt?.Invoke(appliedHit, outcome.Position); }
                    catch (Exception exception) { Debug.LogWarning("C6_T06_HOOK_FAILED type=" + exception.GetType().Name); }
                }
                else if (outcome.Kind == ProjectileOutcomeKind.Expired)
                {
                    try { ProjectileMissed?.Invoke(outcome); }
                    catch (Exception exception) { Debug.LogWarning("C6_T06_HOOK_FAILED type=" + exception.GetType().Name); }
                }
            }
            finally
            {
                if (usedBattleHitHook && afterRewardedHit != null)
                {
                    try { afterRewardedHit(processingNow, appliedHit); }
                    catch (Exception exception) { Debug.LogWarning("C6_T09_AFTER_HIT_FAILED type=" + exception.GetType().Name); }
                }
            }
        }

        private AttackRequestReply MakeReply(ulong sender, string nonce, OrbActionRequest request)
        {
            var current = transfersEnabled || releaseThrowsEnabled ? Authority.QueryRequest(sender, request)
                : Authority.QueryRequest(sender, request.SessionId, request.RoundId, request.RequestId, request.OrbId);
            return new AttackRequestReply
            {
                nonce = nonce, sessionId = request.SessionId, roundId = request.RoundId,
                requestId = request.RequestId, orbId = request.OrbId, accepted = current.Receipt?.Accepted ?? false,
                duplicate = current.Receipt?.IsDuplicate ?? false, known = current.Known, pending = current.IsPending,
                reason = current.Reason, confirmedOrb = OrbWire.FromRecord(current.ConfirmedOrb), inventoryRevision = Snapshot?.revision ?? 0
            };
        }

        private void ReceiveReply(ulong sender, FastBufferReader reader)
        {
            if (!CanProcess || manager.IsHost || sender != NetworkManager.ServerClientId
                || !AttackWire.TryRead<AttackRequestReply>(reader, out var reply)) return;
            DeliverReply(reply);
        }

        private void DeliverReply(AttackRequestReply reply)
        {
            if (Snapshot == null || reply == null || reply.nonce != localNonce || reply.sessionId != Snapshot.sessionId
                || reply.roundId != Snapshot.roundId || !AttackWire.ValidId(reply.requestId)
                || !submitted.TryGetValue(reply.requestId, out var request) || reply.orbId != request.OrbId
                || request.SessionId != reply.sessionId || request.RoundId != reply.roundId
                || reply.confirmedOrb != null && (!AttackWire.ValidOrb(reply.confirmedOrb) || !ValidOrbMotionMode(reply.confirmedOrb)
                    || reply.confirmedOrb.id != request.OrbId || reply.confirmedOrb.owner != LocalPlayerId
                        && !(transfersEnabled && reply.known && HostOrbRegistry.IsTransfer(request.Kind) && IsParticipant(reply.confirmedOrb.owner))))
            { Drop("REPLY_CONTEXT"); return; }
            if (aggregateMode && reply.known && !AggregateReplyConfirmed(reply))
            {
                if (!aggregateReplies.TryGetValue(reply.requestId, out var previous) || reply.inventoryRevision >= previous.inventoryRevision)
                    aggregateReplies[reply.requestId] = reply;
                Status = "Host receipt received; waiting for its confirmed inventory.";
                return;
            }
            aggregateReplies.Remove(reply.requestId);
            if (aggregateMode && reply.known && !reply.accepted)
                reply.confirmedOrb = Snapshot.orbs.FirstOrDefault(value => value.id == reply.orbId
                    && (value.owner == LocalPlayerId || transfersEnabled && HostOrbRegistry.IsTransfer(request.Kind) && IsParticipant(value.owner)));
            LastResult = reply;
            Status = reply.known ? (reply.accepted ? HostOrbRegistry.IsTransfer(request.Kind) ? "Host transfer confirmed." : "Host launch confirmed." : "Host rejected: " + reply.reason)
                : "Host state queried: " + reply.reason;
            Debug.Log($"C6_T06_REPLY session={reply.sessionId} round={reply.roundId} request={reply.requestId} orb={reply.orbId} accepted={reply.accepted} known={reply.known} pending={reply.pending} confirmedState={reply.confirmedOrb?.state.ToString() ?? "none"}");
            RequestResolved?.Invoke(reply);
            Changed?.Invoke();
        }

        private void PublishHostSnapshot(string stage)
        {
            if (!IsHost || Registry == null) return;
            revision++;
            Snapshot = new AttackSnapshot
            {
                nonce = localNonce, sessionId = Registry.SessionId, roundId = Registry.RoundId, revision = revision,
                hp = Authority.MonsterHp, maxHp = Authority.MonsterMaxHp, totalHits = totalHits,
                roundHits = Authority.ValidHitCount, resets = resets, state = Authority.State.ToString(),
                orbs = Registry.Snapshot().Where(orb => useT06Fixtures || orb.AuthorityState != OrbAuthorityState.Consumed)
                    .Select(OrbWire.FromRecord).ToArray(),
                projectiles = projectiles.Values.Where(value => value != null && !value.HasCompleted)
                    .OrderBy(value => value.OrbId, StringComparer.Ordinal).Select(value => new ProjectileWire
                    {
                        id = value.OrbId, owner = value.AttackerPlayerId, position = value.transform.position, radius = value.Radius,
                        ballistic = value.BallisticActive,
                        velocity = value.BallisticActive ? value.Body.linearVelocity : Vector3.zero,
                        gravity = value.BallisticActive ? value.GravityVector : Vector3.zero,
                        elapsed = value.BallisticActive ? value.ElapsedPhysicsTime : 0f,
                        lifetime = value.BallisticActive ? value.Lifetime : 0f
                    }).ToArray()
            };
            foreach (var participant in participantNonces)
                if (IsParticipant(participant.Key)) SendSnapshot(participant.Key, participant.Value, Snapshot);
            if (!string.IsNullOrEmpty(stage))
                Debug.Log($"C6_T06_STATE stage={stage} role=Host session={Snapshot.sessionId} round={Snapshot.roundId} revision={Snapshot.revision} state={Snapshot.state} hp={Snapshot.hp} totalHits={totalHits} roundHits={Snapshot.roundHits} resets={resets} orbs={Snapshot.orbs.Length} projectiles={Snapshot.projectiles.Length}");
            Changed?.Invoke();
        }

        private void SendSnapshot(ulong receiver, string nonce, AttackSnapshot snapshot)
        {
            if (aggregateMode || snapshot == null) return;
            var packet = new AttackSnapshot
            {
                nonce = nonce, sessionId = snapshot.sessionId, roundId = snapshot.roundId, revision = snapshot.revision,
                hp = snapshot.hp, maxHp = snapshot.maxHp, totalHits = snapshot.totalHits, roundHits = snapshot.roundHits,
                resets = snapshot.resets, state = snapshot.state, orbs = snapshot.orbs, projectiles = snapshot.projectiles
            };
            Send(SnapshotMessage, receiver, packet);
        }

        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (aggregateMode || !CanProcess || manager.IsHost || sender != NetworkManager.ServerClientId
                || !AttackWire.TryRead<AttackSnapshot>(reader, out var incoming, maximumSnapshotBytes) || incoming.nonce != localNonce
                || !AttackWire.ValidSnapshot(incoming, maximumSnapshotOrbs) || !ValidMotionMode(incoming)) return;
            if (Snapshot != null && (incoming.sessionId != Snapshot.sessionId || incoming.roundId < Snapshot.roundId
                || incoming.revision <= Snapshot.revision || incoming.totalHits < Snapshot.totalHits || incoming.resets < Snapshot.resets)) return;
            bool changedRound = Snapshot == null || incoming.roundId != Snapshot.roundId;
            bool meaningful = Snapshot == null || changedRound || incoming.hp != Snapshot.hp || incoming.state != Snapshot.state;
            if (changedRound) { submitted.Clear(); LastResult = null; }
            Snapshot = incoming;
            Status = "Host physics state synchronized. Client displays results only.";
            if (meaningful)
                Debug.Log($"C6_T06_STATE stage=snapshot role=Client session={Snapshot.sessionId} round={Snapshot.roundId} revision={Snapshot.revision} state={Snapshot.state} hp={Snapshot.hp} totalHits={Snapshot.totalHits} roundHits={Snapshot.roundHits} resets={Snapshot.resets} orbs={Snapshot.orbs.Length} projectiles={Snapshot.projectiles.Length}");
            Changed?.Invoke();
        }

        private bool Send<T>(string messageName, ulong receiver, T packet)
        {
            if (!CanProcess) return false;
            try
            {
                using (var writer = AttackWire.Write(packet, packet is AttackSnapshot ? maximumSnapshotBytes : AttackWire.MaximumBytes))
                    messaging.SendNamedMessage(messageName, receiver, writer, NetworkDelivery.ReliableFragmentedSequenced);
                return true;
            }
            catch (Exception exception)
            {
                Status = "Attack data could not be sent. Check the confirmed Host state before retrying.";
                Debug.LogWarning("C6_T06_SEND_FAILED type=" + exception.GetType().Name);
                return false;
            }
        }

        private bool ValidMotionMode(AttackSnapshot snapshot) => snapshot != null && snapshot.projectiles != null
            && snapshot.projectiles.All(projectile => projectile.ballistic == releaseThrowsEnabled)
            && snapshot.orbs != null && snapshot.orbs.All(ValidOrbMotionMode);

        private bool ValidOrbMotionMode(OrbWire orb) => orb != null && (continuousTransfersEnabled
            ? orb.hasTransferMotion == (orb.transferCount > 0)
                && (!orb.hasTransferMotion || config != null && OrbTransferMotion.IsValid(orb.ToRecord().TransferMotion.Value,
                    config.OrbMaxReleaseSpeed * OrbTransferMotion.MaximumReleaseSpeedMultiplier))
            : !orb.hasTransferMotion);

        private static bool ValidRequestShape(OrbActionRequest request) => request != null
            && AttackWire.ValidId(request.SessionId) && AttackWire.ValidId(request.RequestId) && AttackWire.ValidId(request.OrbId)
            && (request.OtherOrbId == null || request.OtherOrbId.Length <= 128);
        private static bool ValidPacketShape(AttackRequestPacket packet) => packet != null
            && AttackWire.ValidId(packet.sessionId) && AttackWire.ValidId(packet.requestId) && AttackWire.ValidId(packet.orbId)
            && (packet.otherOrbId == null || packet.otherOrbId.Length <= 128)
            // The absent variant has exactly one canonical representation. Numeric validation
            // for a present sample belongs to the Host receipt path before any reservation.
            && (packet.hasThrowInput || packet.throwDelta.Equals(Vector2.zero) && packet.throwDuration == 0f)
            && (packet.hasTransferMotion || packet.transferVelocityX == 0f && packet.transferVelocityY == 0f && packet.transferServerTime == 0d);
        private static void Drop(string reason) => Debug.LogWarning("C6_T06_DROPPED reason=" + reason);

        private void CancelAllProjectiles()
        {
            foreach (var projectile in projectiles.Values) if (projectile != null) projectile.CancelAndDestroy();
            projectiles.Clear();
        }

        private void ResetBinding(AttackBattleState terminal, bool notify)
        {
            if (resetting) return;
            resetting = true;
            try
            {
                Authority?.EndDevelopmentRound(terminal);
                CancelAllProjectiles();
                Registry?.ClearSession();
                if (messaging != null)
                {
                    messaging.UnregisterNamedMessageHandler(HelloMessage);
                    messaging.UnregisterNamedMessageHandler(RequestMessage);
                    messaging.UnregisterNamedMessageHandler(QueryMessage);
                    messaging.UnregisterNamedMessageHandler(ReplyMessage);
                    messaging.UnregisterNamedMessageHandler(SnapshotMessage);
                }
                messaging = null;
                manager = null;
                Registry = null;
                Authority = null;
                participantNonces.Clear();
                fixtureOwners.Clear();
                seenRequests.Clear();
                submitted.Clear();
                aggregateReplies.Clear();
                helloAttempts = 0;
                nextHelloAt = nextPoseAt = 0;
                LastResult = null;
                if (Snapshot != null)
                {
                    Snapshot.state = terminal.ToString();
                    Snapshot.orbs = Array.Empty<OrbWire>();
                    Snapshot.projectiles = Array.Empty<ProjectileWire>();
                }
                Status = terminal == AttackBattleState.NetworkError ? "Network error ended the DEV trial. Connect again manually."
                    : "DEV trial ended. Start a host or join manually.";
            }
            finally { resetting = false; }
            if (notify) Changed?.Invoke();
        }

        private void OnDisable()
        {
            if (connection != null) connection.Changed -= RefreshBinding;
            intentionalEnd = true;
            explicitDevelopmentRequested = false;
            if (connection != null && connection.State != DirectConnectionState.Idle && connection.State != DirectConnectionState.Failed)
                connection.Stop();
            ResetBinding(AttackBattleState.Ended, true);
        }

        private void OnDestroy()
        {
            ResetBinding(AttackBattleState.Ended, false);
            Changed = null;
            RequestResolved = null;
            ValidHit = null;
        }
    }
}
