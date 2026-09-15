using System;
using System.Collections.Generic;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using C6.Prototype.Resources;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Combination
{
    /// <summary>Combines only through the actual authenticated Host and the existing shared orb registry.</summary>
    [DisallowMultipleComponent]
    public sealed class CombinationSession : MonoBehaviour
    {
        private const string RequestMessage = "C6.T08.Combine.v1";
        private const string QueryMessage = "C6.T08.Query.v1";
        private const string ReplyMessage = "C6.T08.Reply.v1";
        private sealed class SubmittedRequest
        {
            public ulong Sender;
            public CombinationPacket Packet;
        }
        private readonly Dictionary<string, SubmittedRequest> received = new Dictionary<string, SubmittedRequest>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, int> requestCounts = new Dictionary<ulong, int>();
        private AttackSession attack;
        private ResourceSession resources;
        private ScreenLayoutConfig config;
        private Func<bool> gameplayGate;
        public void ConfigureGameplayGate(Func<bool> predicate)
        {
            if (attack != null && attack.Connected)
                throw new InvalidOperationException("Configure the battle gate before connecting.");
            gameplayGate = predicate;
        }
        private bool GameplayAllowed => gameplayGate == null || gameplayGate();
        public bool ClearPendingForConfirmedRoundEnd(string expectedSession, uint expectedRound)
        {
            if (!Connected || sessionId != expectedSession || roundId != expectedRound
                || gameplayGate == null || GameplayAllowed) return false;
            if (pending != null)
            {
                pending = null;
                Status = "Confirmed round ended; pending actions cannot resume in a later round.";
                Changed?.Invoke();
            }
            return true;
        }

        private NetworkManager manager;
        private CustomMessagingManager messaging;
        private string sessionId;
        private uint roundId;
        private bool refreshing;
        private bool confirmingInventory;
        private bool applicationPaused;
        private CombinationPacket pending;

        public HostCombinationAuthority Authority { get; private set; }
        public CombinationReply LastResult { get; private set; }
        public bool HasPending => pending != null;
        public bool AwaitingInventoryConfirmation => pending != null && LastResult != null && LastResult.known
            && LastResult.accepted && !CombinationWire.InventoryConfirmed(LastResult, attack?.Snapshot);
        public bool Connected => isActiveAndEnabled && attack != null && attack.Connected && resources != null && resources.Connected
            && sessionId == attack.Snapshot.sessionId && roundId == attack.Snapshot.roundId;
        public bool IsHost => Connected && manager != null && manager.IsHost && Authority != null;
        public string Status { get; private set; } = "Drop one Raw orb onto the opposite polarity.";
        public event Action Changed;
        public event Action<CombinationReply> RequestResolved;
        private bool Playing => Connected && !applicationPaused && GameplayAllowed && resources.Snapshot.playing
            && attack.Snapshot.state == AttackBattleState.Playing.ToString();

        public void Configure(AttackSession attackSession, ResourceSession resourceSession, ScreenLayoutConfig sharedConfig)
        {
            if (Connected) throw new InvalidOperationException("End the current connection before reconfiguring combination.");
            Unsubscribe();
            ResetBinding();
            attack = attackSession;
            resources = resourceSession;
            config = sharedConfig;
            if (isActiveAndEnabled) Subscribe();
            RefreshBinding();
        }

        private void OnEnable() { Subscribe(); RefreshBinding(); }
        private void Subscribe()
        {
            if (attack != null) attack.Changed += OnDependencyChanged;
            if (resources != null) resources.Changed += OnDependencyChanged;
        }
        private void Unsubscribe()
        {
            if (attack != null) attack.Changed -= OnDependencyChanged;
            if (resources != null) resources.Changed -= OnDependencyChanged;
        }
        // Used after all aggregate component snapshots have been installed without events.
        public void RefreshApprovedBinding() => RefreshBinding();
        public void NotifyAggregateChanged() { RefreshBinding(); ConfirmPendingInventory(); Changed?.Invoke(); }
        private void OnDependencyChanged() { RefreshBinding(); ConfirmPendingInventory(); Changed?.Invoke(); }
        private void Update() { RefreshBinding(); ConfirmPendingInventory(); }

        private void ConfirmPendingInventory()
        {
            if (refreshing || confirmingInventory || !Connected || pending == null || LastResult == null
                || !CombinationWire.MatchesReply(pending, LastResult, attack.LocalPlayerId)
                || LastResult.sourcePending || LastResult.targetPending
                || !CombinationWire.InventoryConfirmed(LastResult, attack.Snapshot)) return;
            confirmingInventory = true;
            try { DeliverReply(LastResult); }
            finally { confirmingInventory = false; }
        }

        private void RefreshBinding()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                var candidate = attack?.Connection?.OwnedManager;
                if (!isActiveAndEnabled || config == null || attack == null || !attack.Connected || resources == null || !resources.Connected
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
                }
                var context = attack.Snapshot;
                if (sessionId != context.sessionId || roundId != context.roundId)
                {
                    Authority?.EndRound();
                    Authority = null;
                    sessionId = context.sessionId;
                    roundId = context.roundId;
                    received.Clear();
                    requestCounts.Clear();
                    pending = null;
                    LastResult = null;
                    if (manager.IsHost)
                    {
                        Authority = new HostCombinationAuthority(attack.Registry);
                        Authority.BeginRound();
                    }
                    Status = "Ready: Yin + Yang consumes two Raw orbs and creates one new Combined ID.";
                    Debug.Log($"C6_T08_READY role={(manager.IsHost ? "Host" : "Client")} session={sessionId} round={roundId} sharedRegistry=true");
                }
                Authority?.SetPlaying(Playing);
            }
            finally { refreshing = false; }
        }

        public bool Submit(CombinationRequest request)
        {
            RefreshBinding();
            if (!Playing || pending != null || request == null) return false;
            var packet = CombinationPacket.FromRequest(attack.Snapshot.nonce, request);
            if (!CurrentContext(packet)) return false;
            // Install the pending payload before local processing, which can reply synchronously.
            pending = packet;
            // An explicit duplicate Submit still waits for its own reply. Do not let the previous
            // completed receipt resolve this new pending send during a resource/snapshot callback.
            LastResult = null;
            Status = "Waiting for Host confirmation; both materials remain locked.";
            bool sent;
            if (IsHost) { ProcessRequest(attack.LocalPlayerId, packet, true); sent = true; }
            else sent = Send(RequestMessage, NetworkManager.ServerClientId, packet);
            if (!sent) Status = "Send failed; query the same request instead of recreating either orb.";
            Changed?.Invoke();
            return sent;
        }

        public bool QueryPending(CombinationRequest request)
        {
            RefreshBinding();
            if (!Connected || pending == null || request == null
                || !CombinationWire.Matches(pending, CombinationPacket.FromRequest(attack.Snapshot.nonce, request))) return false;
            if (IsHost)
            {
                DeliverReply(MakeReply(attack.LocalPlayerId, pending, true));
                attack.PublishInventoryChange("t08-query-confirmed-inventory");
                return true;
            }
            // Queries are a separate named operation. They never invoke Combine or manufacture a replacement ID.
            return Send(QueryMessage, NetworkManager.ServerClientId, pending);
        }

        private bool CurrentContext(CombinationPacket request) => CombinationWire.ValidRequest(request)
            && request.sessionId == sessionId && request.roundId == roundId;

        private void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            RefreshBinding();
            if (!IsHost || sender == manager.LocalClientId || !CombinationWire.TryRead<CombinationPacket>(reader, out var request)
                || !CurrentContext(request) || !attack.IsAuthenticatedPlayer(sender, request.nonce)) return;
            ProcessRequest(sender, request, false);
        }

        private void ProcessRequest(ulong sender, CombinationPacket request, bool local)
        {
            if (!IsHost || !CurrentContext(request) || !attack.IsAuthenticatedPlayer(sender, request.nonce)) return;
            CombinationReply reply;
            if (received.TryGetValue(request.requestId, out var previous)
                && (previous.Sender != sender || !CombinationWire.Matches(previous.Packet, request)))
                reply = MakeRejection(sender, request, "REQUEST_ID_CONFLICT");
            else
            {
                requestCounts.TryGetValue(sender, out int count);
                if (previous == null && count >= HostCombinationAuthority.MaximumRequestsPerPlayer)
                    reply = MakeRejection(sender, request, "REQUEST_LIMIT");
                else
                {
                    Authority.SetPlaying(Playing);
                    var result = Authority.Combine(sender, request.ToRequest());
                    if (previous == null)
                    {
                        received.Add(request.requestId, new SubmittedRequest { Sender = sender, Packet = request });
                        requestCounts[sender] = count + 1;
                    }
                    if (result.Accepted && !result.IsDuplicate) attack.PublishInventoryChange("t08-combination");
                    reply = MakeReply(sender, request, result.IsDuplicate);
                    Debug.Log($"C6_T08_COMBINE session={sessionId} round={roundId} sender={sender} request={request.requestId} source={request.sourceOrbId} target={request.targetOrbId} sequence={request.sequence} accepted={result.Accepted} duplicate={result.IsDuplicate} reason={result.Reason} combined={result.Combined?.OrbId ?? "none"}");
                }
            }
            if (local) DeliverReply(reply); else Send(ReplyMessage, sender, reply);
        }

        private CombinationReply MakeBase(CombinationPacket request, bool known, string reason) => new CombinationReply
        {
            nonce = request.nonce, sessionId = request.sessionId, roundId = request.roundId, requestId = request.requestId,
            sourceOrbId = request.sourceOrbId, targetOrbId = request.targetOrbId, sequence = request.sequence,
            sourcePosition = request.sourcePosition, targetPosition = request.targetPosition, known = known, reason = reason,
            inventoryRevision = attack.Snapshot.revision
        };

        private OrbWire Owned(ulong sender, string id) => attack.Registry.TryGet(id, out var orb) && orb.OwnerPlayerId == sender
            ? OrbWire.FromRecord(orb) : null;

        private CombinationReply MakeRejection(ulong sender, CombinationPacket request, string reason)
        {
            var reply = MakeBase(request, true, reason);
            reply.currentSource = Owned(sender, request.sourceOrbId);
            reply.currentTarget = Owned(sender, request.targetOrbId);
            reply.sourcePending = reply.currentSource != null && attack.Registry.IsPending(request.sourceOrbId);
            reply.targetPending = reply.currentTarget != null && attack.Registry.IsPending(request.targetOrbId);
            return reply;
        }

        private CombinationReply MakeReply(ulong sender, CombinationPacket request, bool duplicate)
        {
            // The pure query uses IDs; the transport additionally binds every coordinate and sequence bit.
            if (!received.TryGetValue(request.requestId, out var previous) || previous.Sender != sender
                || !CombinationWire.Matches(previous.Packet, request))
            {
                var unknown = MakeBase(request, false, "REQUEST_UNKNOWN");
                unknown.currentSource = Owned(sender, request.sourceOrbId);
                unknown.currentTarget = Owned(sender, request.targetOrbId);
                unknown.sourcePending = unknown.currentSource != null && attack.Registry.IsPending(request.sourceOrbId);
                unknown.targetPending = unknown.currentTarget != null && attack.Registry.IsPending(request.targetOrbId);
                return unknown;
            }
            var status = Authority.Query(sender, request.sessionId, request.roundId, request.requestId, request.sourceOrbId, request.targetOrbId);
            var reply = MakeBase(request, status.Known, status.Receipt?.Reason ?? status.Reason);
            reply.accepted = status.Receipt?.Accepted ?? false;
            reply.duplicate = duplicate;
            reply.originalCombined = OrbWire.FromRecord(status.Receipt?.Combined);
            reply.currentSource = OrbWire.FromRecord(status.Source);
            reply.currentTarget = OrbWire.FromRecord(status.Target);
            reply.currentCombined = OrbWire.FromRecord(status.Combined);
            reply.sourcePending = status.SourcePending;
            reply.targetPending = status.TargetPending;
            return reply;
        }

        private void ReceiveQuery(ulong sender, FastBufferReader reader)
        {
            RefreshBinding();
            if (!IsHost || sender == manager.LocalClientId || !CombinationWire.TryRead<CombinationPacket>(reader, out var request)
                || !CurrentContext(request) || !attack.IsAuthenticatedPlayer(sender, request.nonce)) return;
            Send(ReplyMessage, sender, MakeReply(sender, request, true));
            attack.PublishInventoryChange("t08-query-confirmed-inventory");
        }

        private void ReceiveReply(ulong sender, FastBufferReader reader)
        {
            if (manager == null || manager.IsHost || sender != NetworkManager.ServerClientId || !Connected
                || !CombinationWire.TryRead<CombinationReply>(reader, out var reply)) return;
            DeliverReply(reply);
        }

        private void DeliverReply(CombinationReply reply)
        {
            if (!Connected || !CombinationWire.MatchesReply(pending, reply, attack.LocalPlayerId)) return;
            if (LastResult != null && LastResult.requestId == reply.requestId && reply.inventoryRevision < LastResult.inventoryRevision) return;
            LastResult = reply;
            bool inventoryConfirmed = !reply.accepted || CombinationWire.InventoryConfirmed(reply, attack.Snapshot);
            bool conclusive = reply.known && !reply.sourcePending && !reply.targetPending && inventoryConfirmed;
            if (conclusive) pending = null;
            Status = conclusive ? reply.accepted ? "Host confirmed Yin + Yang: one new Combined orb." : "Host rejected: " + reply.reason
                : reply.accepted ? "Host approved; keep both materials locked until its inventory snapshot is confirmed."
                : reply.known ? "Host rejected; a material is still reserved. Keep both locked while querying current state."
                : "Host has no matching receipt; both materials remain locked until confirmation or session end.";
            Debug.Log($"C6_T08_REPLY session={reply.sessionId} round={reply.roundId} request={reply.requestId} source={reply.sourceOrbId} target={reply.targetOrbId} accepted={reply.accepted} known={reply.known} duplicate={reply.duplicate} inventoryRevision={reply.inventoryRevision} inventoryConfirmed={inventoryConfirmed} reason={reply.reason} combined={reply.currentCombined?.id ?? "none"}");
            RequestResolved?.Invoke(reply);
            Changed?.Invoke();
        }

        private bool Send<T>(string message, ulong receiver, T packet)
        {
            if (messaging == null || manager == null || !manager.IsListening || manager.ShutdownInProgress || !Connected) return false;
            try
            {
                using (var writer = CombinationWire.Write(packet))
                    messaging.SendNamedMessage(message, receiver, writer, NetworkDelivery.ReliableFragmentedSequenced);
                return true;
            }
            catch (Exception exception)
            { Debug.LogWarning("C6_T08_SEND_FAILED type=" + exception.GetType().Name); return false; }
        }

        private void ResetBinding()
        {
            Authority?.EndRound();
            Authority = null;
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(RequestMessage);
                messaging.UnregisterNamedMessageHandler(QueryMessage);
                messaging.UnregisterNamedMessageHandler(ReplyMessage);
            }
            messaging = null;
            manager = null;
            sessionId = null;
            roundId = 0;
            received.Clear();
            requestCounts.Clear();
            pending = null;
            LastResult = null;
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            Authority?.SetPlaying(Playing);
            Changed?.Invoke();
        }
        private void OnDisable()
        {
            Unsubscribe();
            // Re-enabling must not discard a receipt ledger while leaving the same round running.
            if (attack != null && attack.Connected) attack.EndDevelopmentTest();
            ResetBinding();
            Changed?.Invoke();
        }
        private void OnDestroy() { Unsubscribe(); ResetBinding(); Changed = null; RequestResolved = null; }
    }
}
