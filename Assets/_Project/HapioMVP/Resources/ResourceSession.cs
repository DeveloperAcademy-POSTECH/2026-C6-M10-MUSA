using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Resources
{
    /// <summary>T07 resources share the authenticated T06 NGO connection and Host orb registry.</summary>
    [DisallowMultipleComponent]
    public sealed class ResourceSession : MonoBehaviour
    {
        private const string RequestMessage = "C6.T07.Generate.v1";
        private const string QueryMessage = "C6.T07.Query.v1";
        private const string ReplyMessage = "C6.T07.Reply.v1";
        private const string SnapshotMessage = "C6.T07.Resources.v1";
        private const string SyncMessage = "C6.T07.Sync.v1";
        private const int MaximumRequestsPerParticipant = 256;

        private sealed class Receipt
        {
            public ulong Sender;
            public ResourceRequestPacket Request;
            public bool Accepted;
            public string Reason;
            public string OrbId;
            public double Before;
            public double After;
        }

        private readonly Dictionary<string, Receipt> receipts = new Dictionary<string, Receipt>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, int> requestCounts = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, ulong> lastRequestSequences = new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> debugFixtureOwners = new HashSet<ulong>();
        private AttackSession attack;
        private ScreenLayoutConfig config;
        private bool aggregateMode;
        private uint? approvedSeed;
        private bool confirmingAggregateReceipt;
        public void ConfigureApprovedSeed(uint value)
        {
            if (attack != null && attack.Connected) throw new InvalidOperationException("Apply the approved seed before gameplay attaches.");
            approvedSeed = value;
        }
        public void SetAggregateMode(bool enabled)
        {
            if (attack != null && attack.Connected) throw new InvalidOperationException("Choose snapshot mode before gameplay attaches.");
            aggregateMode = enabled;
        }
        public bool ApplyAggregateSnapshot(ResourceSnapshot incoming, bool notify = false)
        {
            if (!aggregateMode || attack == null || !attack.Connected || attack.IsHost || incoming == null
                || !ResourceWire.ValidSnapshot(incoming, attack.ParticipantCapacity) || incoming.nonce != attack.Snapshot.nonce
                || incoming.sessionId != attack.Snapshot.sessionId || incoming.roundId != attack.Snapshot.roundId
                || approvedSeed.HasValue && incoming.seed != approvedSeed.Value
                || !incoming.players.Any(value => value.playerId == attack.LocalPlayerId)) return false;
            RefreshBinding();
            if (Snapshot != null)
            {
                if (incoming.revision < Snapshot.revision) return false;
                if (incoming.revision == Snapshot.revision) return JsonUtility.ToJson(incoming) == JsonUtility.ToJson(Snapshot);
            }
            Snapshot = incoming;
            Status = "Approved aggregate resource state synchronized.";
            if (notify) NotifyAggregateChanged();
            return true;
        }
        public void NotifyAggregateChanged()
        {
            if (!aggregateMode) return;
            ConfirmAggregateReceipt();
            Changed?.Invoke();
        }
        private bool AcceptedAggregateReceiptConfirmed(ResourceRequestReply reply)
        {
            if (!aggregateMode || !reply.accepted) return true;
            var inventory = attack?.Snapshot;
            var player = LocalPlayer;
            if (Snapshot == null || inventory == null || player == null || reply.confirmedOrb == null
                || reply.resourceRevision == 0 || reply.inventoryRevision == 0
                || Snapshot.revision < reply.resourceRevision || inventory.revision < reply.inventoryRevision
                || reply.operation == (int)ResourceRequestKind.Generate && player.lastSequence < reply.sequence) return false;
            var confirmed = inventory.orbs.FirstOrDefault(value => value.id == reply.confirmedOrb.id);
            return confirmed == null || confirmed.owner == attack.LocalPlayerId;
        }
        private void ConfirmAggregateReceipt()
        {
            if (!aggregateMode || confirmingAggregateReceipt || pending == null || LastResult == null
                || !LastResult.known || !LastResult.accepted || !AcceptedAggregateReceiptConfirmed(LastResult)) return;
            confirmingAggregateReceipt = true;
            try { DeliverReply(LastResult); }
            finally { confirmingAggregateReceipt = false; }
        }

        private Func<bool> gameplayGate;
        private Func<double> hostNowProvider;
        // Optional T09 lifecycle: earlier scenes retain their original resource behavior.
        public void ConfigureGameplayGate(Func<bool> predicate, Func<double> hostNow = null)
        {
            if (attack != null && attack.Connected)
                throw new InvalidOperationException("Configure the battle gate before connecting.");
            gameplayGate = predicate; hostNowProvider = hostNow;
        }
        private bool GameplayAllowed => gameplayGate == null || gameplayGate();
        public bool ClearPendingForConfirmedRoundEnd(string expectedSession, uint expectedRound)
        {
            if (!Connected || sessionId != expectedSession || roundId != expectedRound
                || gameplayGate == null || GameplayAllowed) return false;
            // Publish frozen resources even if terminal entry re-enters RefreshBinding at the deadline.
            // Battle invokes this after the final accepted hit reward and before its result snapshot.
            if (IsHost && Authority != null)
            {
                Authority.EndRound(Now);
                PublishSnapshot("t09-confirmed-round-ended");
            }
            if (pending != null)
            {
                pending = null;
                queriedPending = false;
                Status = "Confirmed round ended; pending actions cannot resume in a later round.";
                Changed?.Invoke();
            }
            return true;
        }

        private NetworkManager manager;
        private CustomMessagingManager messaging;
        private string sessionId;
        private uint roundId;
        private uint seed;
        private ulong revision;
        private ulong nextSequence;
        private double nextPublishAt;
        private double nextSyncAt;
        private bool refreshing;
        private bool debugTestMode;
        private bool mixedDebugFixture;
        private bool changingRound;
        private bool applicationPaused;
        private ResourceRequestPacket pending;
        private double pendingSince;
        private bool queriedPending;

        public AttackSession Attack => attack;
        public HostResourceAuthority Authority { get; private set; }
        public ResourceSnapshot Snapshot { get; private set; }
        public ResourceRequestReply LastResult { get; private set; }
        public bool Connected => attack != null && attack.Connected && Snapshot != null
            && Snapshot.sessionId == attack.Snapshot.sessionId && Snapshot.roundId == attack.Snapshot.roundId;
        public bool IsHost => attack != null && attack.IsHost && Authority != null;
        public bool HasPending => pending != null;
        public string PendingRequestId => pending?.requestId;
        public ResourceRequestKind? PendingRequestKind => pending == null ? (ResourceRequestKind?)null : (ResourceRequestKind)pending.operation;
        public bool DebugTestMode => Connected && Snapshot.debugTestMode;
        public string Status { get; private set; } = "Normal start: full stamina, no orbs.";
        public ResourcePlayerWire LocalPlayer => Snapshot?.players?.FirstOrDefault(value => value.playerId == attack.LocalPlayerId);
        private bool Playing => !applicationPaused && Connected && GameplayAllowed && Snapshot.playing
            && attack.Snapshot.state == AttackBattleState.Playing.ToString();
        public bool CanGenerate => Playing && !HasPending && LocalPlayer != null
            && LocalPlayer.stamina >= Snapshot.generateCost && LocalPlayer.storedOrbs < Snapshot.storageLimit;
        public bool CanRequestDebugCombined => Playing && Snapshot.debugTestMode
            && Snapshot.debugToolsEnabled && !HasPending;
        public event Action Changed;
        public event Action<ResourceRequestReply> GenerationResolved;
        public event Action<ResourceRecoveryResult> RecoveryResolved;

        private double Now => hostNowProvider != null ? hostNowProvider() : Time.realtimeSinceStartupAsDouble;

        public void Configure(AttackSession session, ScreenLayoutConfig sharedConfig)
        {
            if (attack != null && attack.Connected)
                throw new InvalidOperationException("End the current connection before reconfiguring its resource authority.");
            if (attack != null) { attack.Changed -= OnAttackChanged; attack.ValidHit -= OnValidHit; }
            ResetBinding();
            attack = session;
            config = sharedConfig;
            if (isActiveAndEnabled && attack != null)
            { attack.Changed += OnAttackChanged; attack.ValidHit += OnValidHit; }
            RefreshBinding();
        }

        private void OnEnable()
        {
            if (attack != null)
            { attack.Changed += OnAttackChanged; attack.ValidHit += OnValidHit; }
            RefreshBinding();
        }

        private void OnAttackChanged()
        {
            RefreshBinding();
            if (IsHost && !refreshing && !changingRound) PublishSnapshot(null);
            Changed?.Invoke();
        }

        private void RefreshBinding()
        {
            if (refreshing || changingRound) return;
            refreshing = true;
            try
            {
                var candidate = attack?.Connection?.OwnedManager;
                if (!isActiveAndEnabled || config == null || attack == null || !attack.Connected
                    || candidate == null || !candidate.IsListening || candidate.ShutdownInProgress)
                { ResetBinding(); return; }
                if (manager != candidate || !ReferenceEquals(messaging, candidate.CustomMessagingManager))
                {
                    ResetBinding();
                    manager = candidate;
                    messaging = candidate.CustomMessagingManager;
                    if (messaging == null) return;
                    messaging.RegisterNamedMessageHandler(RequestMessage, ReceiveRequest);
                    messaging.RegisterNamedMessageHandler(QueryMessage, ReceiveQuery);
                    messaging.RegisterNamedMessageHandler(ReplyMessage, ReceiveReply);
                    messaging.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
                    messaging.RegisterNamedMessageHandler(SyncMessage, ReceiveSync);
                }

                var context = attack.Snapshot;
                bool newSession = sessionId != context.sessionId;
                bool newRound = newSession || roundId != context.roundId;
                if (newRound)
                {
                    Authority?.EndRound(Now);
                    Authority = null;
                    sessionId = context.sessionId;
                    roundId = context.roundId;
                    nextSequence = 0;
                    pending = null;
                    queriedPending = false;
                    LastResult = null;
                    Snapshot = null;
                    receipts.Clear();
                    requestCounts.Clear();
                    lastRequestSequences.Clear();
                    debugFixtureOwners.Clear();
                    debugTestMode = false;
                    mixedDebugFixture = false;
                    if (newSession) { seed = aggregateMode && approvedSeed.HasValue ? approvedSeed.Value : BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0); revision = 0; }
                    if (manager.IsHost)
                    {
                        var tuning = new ResourceTuning(config.StaminaMax, config.StaminaStart, config.GenerateCost,
                            config.StaminaRecoveryPerSecond, config.StaminaHitRecovery, config.OrbStorageLimit);
                        Authority = new HostResourceAuthority(attack.Registry, tuning, seed, attack.ParticipantCapacity);
                        Authority.BeginRound(attack.AuthenticatedPlayerIds, Now);
                        Status = "NORMAL: empty inventory, full stamina; Host generates one Raw orb per 20.";
                        Debug.Log($"C6_T07_ROUND session={sessionId} round={roundId} seed={seed} mode=NORMAL initialOrbs=0 stamina={config.StaminaStart}");
                    }
                    else { nextSyncAt = 0; Status = "Waiting for Host resource state."; }
                }
                if (manager.IsHost && Authority != null)
                {
                    bool addedDebugFixture = false;
                    foreach (var participant in attack.AuthenticatedPlayerIds)
                    {
                        if (Authority.GetPlayer(participant) != null) continue;
                        Authority.AddParticipant(participant, Now);
                        if (debugTestMode) addedDebugFixture |= SupplyDebugFixture(participant);
                    }
                    if (addedDebugFixture) attack.PublishInventoryChange("t07-debug-joined-participant");
                    bool playing = !applicationPaused && context.state == AttackBattleState.Playing.ToString() && GameplayAllowed;
                    if (!Authority.IsEnded) Authority.Advance(Now, playing);
                    if (context.state == AttackBattleState.Ended.ToString() || context.state == AttackBattleState.NetworkError.ToString())
                        Authority.EndRound(Now);
                    if (newRound) PublishSnapshot("round-ready");
                }
            }
            finally { refreshing = false; }
        }

        private void Update()
        {
            RefreshBinding();
            if (messaging == null || attack == null || !attack.Connected) return;
            double now = Now;
            if (IsHost && now >= nextPublishAt)
            {
                nextPublishAt = now + 1d / config.AttackSnapshotRateHz;
                PublishSnapshot(null);
            }
            else if (!aggregateMode && !manager.IsHost && !Connected && now >= nextSyncAt)
            {
                nextSyncAt = now + 1d;
                Send(SyncMessage, NetworkManager.ServerClientId, new ResourceSyncPacket
                { nonce = attack.Snapshot.nonce, sessionId = sessionId, roundId = roundId });
            }
            if (pending == null) return;
            if (!queriedPending && now - pendingSince >= 3d) QueryPending();
            if (pending != null && now - pendingSince >= 8d)
            {
                Status = "Host confirmation missing; generation was not replayed.";
                attack.FailUnconfirmedRequest();
            }
        }

        public bool RequestGenerate() => BeginRequest(ResourceRequestKind.Generate);
        public bool RequestDebugCombined() => CanRequestDebugCombined && BeginRequest(ResourceRequestKind.DebugCombined);

        private bool BeginRequest(ResourceRequestKind kind)
        {
            if (!Playing || pending != null || nextSequence == ulong.MaxValue) return false;
            pending = new ResourceRequestPacket
            {
                nonce = attack.Snapshot.nonce, sessionId = sessionId, roundId = roundId,
                requestId = Guid.NewGuid().ToString("N"), sequence = ++nextSequence, operation = (int)kind
            };
            pendingSince = Now;
            if (aggregateMode) LastResult = null;
            queriedPending = false;
            Status = "Waiting for Host approval; no local stamina deduction or orb creation.";
            bool sent;
            if (IsHost) { ProcessRequest(attack.LocalPlayerId, pending, true); sent = true; }
            else sent = Send(RequestMessage, NetworkManager.ServerClientId, pending);
            Changed?.Invoke();
            return sent;
        }

        public bool QueryPending()
        {
            if (!Connected || pending == null) return false;
            queriedPending = true;
            if (IsHost) { DeliverReply(MakeReply(attack.LocalPlayerId, pending, true)); return true; }
            return Send(QueryMessage, NetworkManager.ServerClientId, pending);
        }

        public bool ResetNormalRound()
        {
            if (!IsHost || HasPending) return false;
            // Attack reset is observed by RefreshBinding and always creates NORMAL / empty resources.
            return attack.ResetDevelopmentRound();
        }

        public bool BeginDebugFixtureRound() => BeginDebugFixtureRound(false);

        public bool BeginMixedDebugFixtureRound() => BeginDebugFixtureRound(true);

        private bool BeginDebugFixtureRound(bool mixed)
        {
            if (!IsHost || HasPending || !DebugAvailable) return false;
            changingRound = true;
            bool reset;
            try { reset = attack.ResetDevelopmentRound(); }
            finally { changingRound = false; }
            if (!reset) return false;
            RefreshBinding();
            if (!IsHost) return false;
            debugTestMode = true;
            mixedDebugFixture = mixed;
            foreach (var participant in attack.AuthenticatedPlayerIds) SupplyDebugFixture(participant);
            attack.PublishInventoryChange("t07-debug-fixture");
            Status = mixed ? "DEBUG_TEST_MODE: five mixed Raw fixtures per participant; not normal generation."
                : "DEBUG_TEST_MODE: five fixed Raw fixtures per participant; not normal generation.";
            PublishSnapshot("debug-fixture");
            return true;
        }

        // T09 opt-in Raw supply in the current battle. It neither resets the clock nor creates Combined.
        public bool SupplyMixedDebugFixtureCurrentRound()
        {
            if (!IsHost || HasPending || !Playing || !DebugAvailable) return false;
            var owners = attack.AuthenticatedPlayerIds.ToArray();
            if (owners.Length == 0 || owners.Any(owner => ActiveOrbCount(owner) + 5 > config.OrbStorageLimit)) return false;
            var positions = new Dictionary<ulong, Vector2[]>();
            foreach (var owner in owners)
            {
                var occupied = new HashSet<int>(attack.Registry.Snapshot()
                    .Where(orb => orb.OwnerPlayerId == owner && orb.AuthorityState != OrbAuthorityState.Consumed)
                    .Select(orb => Math.Min(4, (int)(orb.NormalizedPosition.x * 5)) + 5 * Math.Min(3, (int)(orb.NormalizedPosition.y * 4))));
                var free = new System.Collections.Generic.List<Vector2>();
                for (int column = 0; column < 5; column++)
                    for (int row = 0; row < 4; row++)
                        if (!occupied.Contains(column + row * 5)) free.Add(new Vector2((column + .5f) / 5f, (row + .5f) / 4f));
                if (free.Count < 5) return false;
                positions.Add(owner, free.Take(5).ToArray());
            }
            debugTestMode = true; mixedDebugFixture = true;
            foreach (var owner in owners)
            {
                for (int index = 0; index < 5; index++)
                    attack.Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, index % 2 == 0 ? OrbPolarity.Yin : OrbPolarity.Yang, positions[owner][index]);
                debugFixtureOwners.Add(owner);
                Debug.Log($"C6_T09_MIXED_RAW session={sessionId} round={roundId} owner={owner} raw=5 cost=0 mode=DEBUG_TEST_MODE clockReset=false combinedSupplied=0");
            }
            attack.PublishInventoryChange("t09-explicit-mixed-raw");
            Status = "DEBUG_TEST_MODE: mixed Raw supply in the current battle; normal costs unchanged.";
            PublishSnapshot("t09-current-round-fixture");
            return true;
        }

        private bool DebugAvailable => config != null && config.ResourceDebugToolsEnabled && (Application.isEditor || Debug.isDebugBuild);

        private bool SupplyDebugFixture(ulong owner)
        {
            if (!IsHost || !debugTestMode || !DebugAvailable || debugFixtureOwners.Contains(owner)) return false;
            int active = ActiveOrbCount(owner);
            if (active + 5 > config.OrbStorageLimit) return false;
            var polarity = owner == manager.LocalClientId ? OrbPolarity.Yin : OrbPolarity.Yang;
            for (int index = 0; index < 5; index++)
            {
                var chosen = mixedDebugFixture ? (index % 2 == 0 ? OrbPolarity.Yin : OrbPolarity.Yang) : polarity;
                var position = mixedDebugFixture ? new Vector2(.1f + .2f * (index / 2), index % 2 == 0 ? .125f : .375f) : FindDebugSpawnPosition(owner);
                attack.Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, chosen, position);
            }
            debugFixtureOwners.Add(owner);
            Debug.Log($"C6_T07_DEBUG_FIXTURE session={sessionId} round={roundId} owner={owner} raw=5 polarity={(mixedDebugFixture ? "MixedYinYang" : polarity.ToString())} cost=0 mode=DEBUG_TEST_MODE");
            return true;
        }

        private void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || sender == manager.LocalClientId || !ResourceWire.TryRead<ResourceRequestPacket>(reader, out var request)
                || !ResourceWire.ValidRequest(request) || !attack.IsAuthenticatedPlayer(sender, request.nonce)) return;
            ProcessRequest(sender, request, false);
        }

        private bool CurrentContext(ResourceRequestPacket request) => ResourceWire.ValidRequest(request)
            && request.sessionId == sessionId && request.roundId == roundId;

        private void ProcessRequest(ulong sender, ResourceRequestPacket request, bool local)
        {
            if (!IsHost || !CurrentContext(request) || !attack.IsAuthenticatedPlayer(sender, request.nonce)) return;
            if (gameplayGate != null) RefreshBinding();
            if (!IsHost || !CurrentContext(request)) return;
            bool duplicate = false;
            string specialReason = null;
            if (receipts.TryGetValue(request.requestId, out var existing))
            {
                if (!Matches(existing, sender, request)) specialReason = "REQUEST_ID_CONFLICT";
                else duplicate = true;
            }
            else
            {
                requestCounts.TryGetValue(sender, out int count);
                if (count >= MaximumRequestsPerParticipant) specialReason = "REQUEST_LIMIT";
                else
                {
                    requestCounts[sender] = count + 1;
                    Receipt receipt;
                    lastRequestSequences.TryGetValue(sender, out ulong lastSequence);
                    if (request.sequence <= lastSequence)
                    {
                        double stamina = Authority.GetPlayer(sender)?.Stamina ?? 0;
                        receipt = new Receipt { Sender = sender, Request = request, Accepted = false,
                            Reason = "STALE_SEQUENCE", Before = stamina, After = stamina };
                    }
                    else if (request.operation == (int)ResourceRequestKind.Generate)
                    {
                        var result = Authority.Generate(sender, new GenerateRequest(request.sessionId, request.roundId,
                            request.requestId, request.sequence), Now);
                        receipt = new Receipt { Sender = sender, Request = request, Accepted = result.Accepted,
                            Reason = result.Reason, OrbId = result.Orb?.OrbId, Before = result.StaminaBefore, After = result.StaminaAfter };
                    }
                    else receipt = CreateDebugCombined(sender, request);
                    if (request.sequence > lastSequence) lastRequestSequences[sender] = request.sequence;
                    receipts.Add(request.requestId, receipt);
                    Debug.Log($"C6_T07_REQUEST session={sessionId} round={roundId} sender={sender} request={request.requestId} sequence={request.sequence} operation={(ResourceRequestKind)request.operation} accepted={receipt.Accepted} reason={receipt.Reason} orb={receipt.OrbId ?? "none"} staminaBefore={receipt.Before:R} staminaAfter={receipt.After:R} mode={(debugTestMode ? "DEBUG_TEST_MODE" : "NORMAL")}");
                    if (receipt.Accepted) attack.PublishInventoryChange("t07-generation");
                }
            }
            PublishSnapshot("request-result");
            var reply = specialReason == null ? MakeReply(sender, request, duplicate) : MakeRejection(sender, request, specialReason);
            if (local) DeliverReply(reply); else Send(ReplyMessage, sender, reply);
        }

        private Receipt CreateDebugCombined(ulong sender, ResourceRequestPacket request)
        {
            var player = Authority.GetPlayer(sender);
            var receipt = new Receipt { Sender = sender, Request = request, Before = player?.Stamina ?? 0,
                After = player?.Stamina ?? 0, Reason = "DEBUG_MODE_REQUIRED" };
            if (player == null) { receipt.Reason = "UNKNOWN_PARTICIPANT"; return receipt; }
            if (!debugTestMode || !DebugAvailable) return receipt;
            if (!Authority.IsPlaying || Authority.IsEnded) { receipt.Reason = "NOT_PLAYING"; return receipt; }
            if (ActiveOrbCount(sender) >= config.OrbStorageLimit) { receipt.Reason = "STORAGE_FULL"; return receipt; }
            var orb = attack.Registry.RegisterDevelopmentOrb(sender, OrbKind.Combined, OrbPolarity.None, FindDebugSpawnPosition(sender));
            receipt.Accepted = true;
            receipt.Reason = "DEBUG_COMBINED_FIXTURE";
            receipt.OrbId = orb.OrbId;
            return receipt;
        }

        private static bool Matches(Receipt receipt, ulong sender, ResourceRequestPacket request) => receipt.Sender == sender
            && receipt.Request.sessionId == request.sessionId && receipt.Request.roundId == request.roundId
            && receipt.Request.sequence == request.sequence && receipt.Request.operation == request.operation;

        private ResourceRequestReply MakeRejection(ulong sender, ResourceRequestPacket request, string reason)
        {
            double stamina = Authority.GetPlayer(sender)?.Stamina ?? 0;
            return new ResourceRequestReply { nonce = request.nonce, sessionId = request.sessionId, roundId = request.roundId,
                requestId = request.requestId, sequence = request.sequence, operation = request.operation, known = true,
                accepted = false, duplicate = false, reason = reason, staminaBefore = stamina, staminaAfter = stamina,
                inventoryRevision = attack.Snapshot?.revision ?? 0, resourceRevision = Snapshot?.revision ?? 0 };
        }

        private ResourceRequestReply MakeReply(ulong sender, ResourceRequestPacket request, bool duplicate)
        {
            var reply = MakeRejection(sender, request, "UNKNOWN_REQUEST");
            if (!receipts.TryGetValue(request.requestId, out var receipt) || !Matches(receipt, sender, request))
            { reply.known = false; return reply; }
            reply.known = true;
            reply.accepted = receipt.Accepted;
            reply.duplicate = duplicate;
            reply.reason = receipt.Reason;
            reply.staminaBefore = receipt.Before;
            reply.staminaAfter = receipt.After;
            if (receipt.OrbId != null && attack.Registry.TryGet(receipt.OrbId, out var orb)) reply.confirmedOrb = OrbWire.FromRecord(orb);
            return reply;
        }

        private void ReceiveQuery(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || sender == manager.LocalClientId || !ResourceWire.TryRead<ResourceRequestPacket>(reader, out var request)
                || !CurrentContext(request) || !attack.IsAuthenticatedPlayer(sender, request.nonce)) return;
            Send(ReplyMessage, sender, MakeReply(sender, request, true));
            SendSnapshot(sender, request.nonce);
            attack.PublishInventoryChange("t07-query-confirmed-inventory");
        }

        private void ReceiveSync(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || sender == manager.LocalClientId || !ResourceWire.TryRead<ResourceSyncPacket>(reader, out var packet)
                || packet.sessionId != sessionId || packet.roundId != roundId || !attack.IsAuthenticatedPlayer(sender, packet.nonce)) return;
            SendSnapshot(sender, packet.nonce);
        }

        private void ReceiveReply(ulong sender, FastBufferReader reader)
        {
            if (manager == null || manager.IsHost || sender != NetworkManager.ServerClientId || !attack.Connected
                || !ResourceWire.TryRead<ResourceRequestReply>(reader, out var reply)) return;
            DeliverReply(reply);
        }

        private void DeliverReply(ResourceRequestReply reply)
        {
            if (!Connected || pending == null || !ResourceWire.ValidReply(reply, Snapshot.maximum)
                || reply.nonce != pending.nonce || reply.sessionId != pending.sessionId || reply.roundId != pending.roundId
                || reply.requestId != pending.requestId || reply.sequence != pending.sequence || reply.operation != pending.operation
                || reply.confirmedOrb != null && reply.confirmedOrb.owner != attack.LocalPlayerId) return;
            LastResult = reply;
            if (aggregateMode && reply.known && reply.accepted && !AcceptedAggregateReceiptConfirmed(reply))
            {
                Status = "Host approved generation; waiting for its confirmed inventory and resources.";
                Changed?.Invoke();
                return;
            }
            if (reply.known) pending = null;
            Status = reply.known ? (reply.accepted ? "Host confirmed one orb: " + reply.reason : "Host rejected: " + reply.reason)
                : "Host has no confirmed receipt; request remains locked.";
            Debug.Log($"C6_T07_REPLY session={reply.sessionId} round={reply.roundId} request={reply.requestId} operation={(ResourceRequestKind)reply.operation} accepted={reply.accepted} known={reply.known} duplicate={reply.duplicate} reason={reply.reason} staminaBefore={reply.staminaBefore:R} staminaAfter={reply.staminaAfter:R} orb={reply.confirmedOrb?.id ?? "none"}");
            GenerationResolved?.Invoke(reply);
            Changed?.Invoke();
        }

        private void OnValidHit(AttackHitResult hit)
        {
            if (!IsHost || hit == null || !GameplayAllowed) return;
            var recovery = Authority.ApplyValidHit(hit, Now);
            Debug.Log($"C6_T07_HIT_RECOVERY session={sessionId} round={roundId} attacker={recovery.PlayerId} orb={hit.OrbId} accepted={recovery.Accepted} duplicate={recovery.IsDuplicate} added={recovery.Added:R} staminaBefore={recovery.StaminaBefore:R} staminaAfter={recovery.StaminaAfter:R} reason={recovery.Reason}");
            try { RecoveryResolved?.Invoke(recovery); }
            catch (Exception exception) { Debug.LogWarning("C6_T07_RECOVERY_OBSERVER_FAILED type=" + exception.GetType().Name); }
            PublishSnapshot("real-hit-recovery");
        }

        private int StoredOrbCount(ulong owner) => attack.Registry.Snapshot().Count(orb => orb.OwnerPlayerId == owner
            && (orb.AuthorityState == OrbAuthorityState.Idle || orb.AuthorityState == OrbAuthorityState.Launching));
        private int ActiveOrbCount(ulong owner) => attack.Registry.Snapshot().Count(orb => orb.OwnerPlayerId == owner
            && orb.AuthorityState != OrbAuthorityState.Consumed);

        private Vector2 FindDebugSpawnPosition(ulong owner)
        {
            int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(config.OrbStorageLimit)));
            int rows = (config.OrbStorageLimit + columns - 1) / columns;
            var occupied = new HashSet<int>();
            foreach (var orb in attack.Registry.Snapshot())
            {
                if (orb.OwnerPlayerId != owner || orb.AuthorityState == OrbAuthorityState.Consumed) continue;
                int column = Math.Min(columns - 1, (int)(orb.NormalizedPosition.x * columns));
                int row = Math.Min(rows - 1, (int)(orb.NormalizedPosition.y * rows));
                occupied.Add(row * columns + column);
            }
            for (int slot = 0; slot < config.OrbStorageLimit; slot++)
                if (!occupied.Contains(slot)) return new Vector2((slot % columns + .5f) / columns, (slot / columns + .5f) / rows);
            throw new InvalidOperationException("No free debug fixture slot remains within capacity.");
        }

        private void PublishSnapshot(string stage)
        {
            if (!IsHost || Authority == null || Authority.IsEnded && gameplayGate == null) return;
            Snapshot = new ResourceSnapshot
            {
                nonce = attack.NonceForPlayer(manager.LocalClientId), sessionId = sessionId, roundId = roundId, revision = ++revision,
                seed = seed, playing = Authority.IsPlaying, debugTestMode = debugTestMode, debugToolsEnabled = DebugAvailable,
                maximum = config.StaminaMax, generateCost = config.GenerateCost, regenerationRate = config.StaminaRecoveryPerSecond,
                hitRecovery = config.StaminaHitRecovery, storageLimit = config.OrbStorageLimit,
                players = Authority.Snapshot().Select(player => new ResourcePlayerWire
                { playerId = player.PlayerId, stamina = player.Stamina, generatedTotal = player.GeneratedTotal,
                    lastSequence = player.LastSequence, storedOrbs = StoredOrbCount(player.PlayerId) }).ToArray()
            };
            foreach (ulong participant in attack.AuthenticatedPlayerIds)
                if (participant != manager.LocalClientId) SendSnapshot(participant, attack.NonceForPlayer(participant));
            if (!string.IsNullOrEmpty(stage))
                Debug.Log($"C6_T07_STATE stage={stage} role=Host session={sessionId} round={roundId} revision={revision} playing={Snapshot.playing} mode={(debugTestMode ? "DEBUG_TEST_MODE" : "NORMAL")} players={Snapshot.players.Length}");
            Changed?.Invoke();
        }

        private void SendSnapshot(ulong receiver, string nonce)
        {
            if (aggregateMode || Snapshot == null || !ResourceWire.ValidNonce(nonce)) return;
            var copy = new ResourceSnapshot { nonce = nonce, sessionId = Snapshot.sessionId, roundId = Snapshot.roundId,
                revision = Snapshot.revision, seed = Snapshot.seed, playing = Snapshot.playing,
                debugTestMode = Snapshot.debugTestMode, debugToolsEnabled = Snapshot.debugToolsEnabled,
                maximum = Snapshot.maximum, generateCost = Snapshot.generateCost, regenerationRate = Snapshot.regenerationRate,
                hitRecovery = Snapshot.hitRecovery, storageLimit = Snapshot.storageLimit, players = Snapshot.players };
            Send(SnapshotMessage, receiver, copy);
        }

        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (aggregateMode || manager == null || manager.IsHost || sender != NetworkManager.ServerClientId || !attack.Connected
                || !ResourceWire.TryRead<ResourceSnapshot>(reader, out var incoming) || !ResourceWire.ValidSnapshot(incoming, attack.ParticipantCapacity)
                || incoming.nonce != attack.Snapshot.nonce || incoming.sessionId != sessionId || incoming.roundId != roundId
                || !incoming.players.Any(value => value.playerId == attack.LocalPlayerId)
                || Snapshot != null && incoming.revision <= Snapshot.revision) return;
            bool first = Snapshot == null;
            Snapshot = incoming;
            Status = "Host resource values synchronized; Client does not regenerate or spend independently.";
            if (first) Debug.Log($"C6_T07_STATE stage=snapshot role=Client session={sessionId} round={roundId} revision={incoming.revision} playing={incoming.playing} mode={(incoming.debugTestMode ? "DEBUG_TEST_MODE" : "NORMAL")} players={incoming.players.Length}");
            Changed?.Invoke();
        }

        private bool Send<T>(string message, ulong receiver, T packet)
        {
            if (messaging == null || manager == null || !manager.IsListening || manager.ShutdownInProgress || !attack.Connected) return false;
            try
            {
                using (var writer = ResourceWire.Write(packet))
                    messaging.SendNamedMessage(message, receiver, writer, NetworkDelivery.ReliableFragmentedSequenced);
                return true;
            }
            catch (Exception exception)
            { Debug.LogWarning("C6_T07_SEND_FAILED type=" + exception.GetType().Name); return false; }
        }

        private void ResetBinding()
        {
            Authority?.EndRound(Now);
            Authority = null;
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(RequestMessage);
                messaging.UnregisterNamedMessageHandler(QueryMessage);
                messaging.UnregisterNamedMessageHandler(ReplyMessage);
                messaging.UnregisterNamedMessageHandler(SnapshotMessage);
                messaging.UnregisterNamedMessageHandler(SyncMessage);
            }
            messaging = null;
            manager = null;
            Snapshot = null;
            LastResult = null;
            sessionId = null;
            roundId = 0;
            pending = null;
            queriedPending = false;
            debugTestMode = false;
                    mixedDebugFixture = false;
            nextSequence = revision = 0;
            receipts.Clear();
            requestCounts.Clear();
            lastRequestSequences.Clear();
            debugFixtureOwners.Clear();
        }

        private void OnDisable()
        {
            if (attack != null) { attack.Changed -= OnAttackChanged; attack.ValidHit -= OnValidHit; }
            // A disabled resource service cannot leave gameplay running without its authoritative timer.
            // Explicit reconnection starts empty/full; re-enabling never grants fresh resources mid-round.
            if (attack != null && attack.Connected) attack.EndDevelopmentTest();
            ResetBinding();
            Changed?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            if (applicationPaused == paused) return;
            applicationPaused = paused;
            if (IsHost && !Authority.IsEnded)
            {
                // Settle the interval before entering pause. On resume, Advance settles the paused
                // interval at zero recovery before restoring Playing; no wall-clock catch-up grant.
                bool playing = !paused && attack.Snapshot != null
                    && attack.Snapshot.state == AttackBattleState.Playing.ToString() && GameplayAllowed;
                Authority.Advance(Now, playing);
                PublishSnapshot(paused ? "application-paused" : "application-resumed");
            }
            else Changed?.Invoke();
        }

        private void OnDestroy()
        {
            ResetBinding();
            Changed = null;
            GenerationResolved = null;
            RecoveryResolved = null;
        }
    }
}
