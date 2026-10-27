using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Owner-local action presentation and small Host commits, independent of paint upload.</summary>
    public sealed partial class LabNetwork
    {
        private readonly LabActionResultLedger actionResults = new LabActionResultLedger();
        private readonly LabLocalActionCoordinator localActions = new LabLocalActionCoordinator();
        private readonly Dictionary<long, LabNetworkPacket> actionRequests = new Dictionary<long, LabNetworkPacket>();
        private readonly Dictionary<long, LabNetworkPacket> actionWireRequests = new Dictionary<long, LabNetworkPacket>();
        private readonly Dictionary<long, double> actionStartedAt = new Dictionary<long, double>();
        private readonly Dictionary<long, LabProjectile> predictedShots = new Dictionary<long, LabProjectile>();
        private readonly HashSet<long> presentedShots = new HashSet<long>();
        private readonly HashSet<string> presentedRemoteShots = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, long> boardSequences = new Dictionary<ulong, long>();
        private readonly LabBoardPublisher boardPublisher = new LabBoardPublisher();
        private readonly LabBoardReplica boardReplica = new LabBoardReplica();
        private LabBoardFrame deferredBoardFrame;
        private string actionSession;
        private uint actionRound;
        private double nextActionRecoveryAt, nextBoardCheckpointAt;
        private bool flushingActions;

        /// <summary>Last local request/receipt round trip, not a cross-device one-way estimate.</summary>
        public double LastActionRoundTripMilliseconds { get; private set; }
        public long ActionRequestTransmissions { get; private set; }
        public long ActionReplayedResults { get; private set; }
        public long ReceivedBoardFrames { get; private set; }

        // "fox": a fox orb touched a Yin or Yang orb and takes it away (#251).
        private static bool IsBoardAction(string type) => type == "generate" || type == "combine"
            || type == "transfer" || type == "launch" || type == "use" || type == "fox" || type == "jigwi_burn";
        private static bool TakesTwoOrbs(LabNetworkPacket packet) => packet.type == "combine" || packet.type == "fox"
            || packet.type == "jigwi_burn" && !string.IsNullOrEmpty(packet.secondId);

        private void EnsureActionRound()
        {
            if (latest == null || latest.sessionId == actionSession && latest.round == actionRound) return;
            ResetActionTransport();
            if (board != null) board.Clear();
            visibleOrbIds.Clear();
            actionSession = latest.sessionId;
            actionRound = latest.round;
            actionResults.SetActive(actionSession, actionRound);
            localActions.SetActive(actionSession, actionRound, LocalPlayerId);
            boardReplica.SetActive(actionSession, actionRound, LocalPlayerId);
        }

        private void ResetActionTransport()
        {
            ClearJigwiFireSurfaces();
            predictedJigwiBurns.Clear();
            foreach (var shot in predictedShots.Values) if (shot != null) Destroy(shot.gameObject);
            predictedShots.Clear(); presentedShots.Clear(); presentedRemoteShots.Clear();
            pendingBlastReports.Clear();
            boardChunks = null; boardChunkSession = null; boardChunkRound = 0; boardChunkSequence = 0;
            displayedPaintSession = null; displayedPaintCount = -1;
            actionRequests.Clear(); actionWireRequests.Clear(); actionStartedAt.Clear();
            boardSequences.Clear(); boardPublisher.Reset();
            actionResults.Reset(); localActions.Reset();
            boardReplica.SetActive(null, 0, 0);
            actionSession = null; actionRound = 0;
            deferredBoardFrame = null;
            nextActionRecoveryAt = nextBoardCheckpointAt = 0d;
        }

        private long NextBoardSequence(ulong recipient)
        {
            boardSequences.TryGetValue(recipient, out long previous);
            return boardSequences[recipient] = previous + 1;
        }

        private string TemporaryOutput(long requestId) => "pending/" + actionRound + "/" + LocalPlayerId + "/" + requestId;

        private void SubmitBoardAction(LabNetworkPacket packet)
        {
            if (!CanProcessBattleActions || latest == null || latest.phase != LabPhase.Playing || latest.paused)
            {
                RejectBoardActionLocally(packet, "BATTLE_NOT_PLAYING");
                return;
            }
            EnsureActionRound();
            if (!IsHost && boardReplica.NeedsCheckpoint)
            {
                RejectBoardActionLocally(packet, "Restoring board state. Try again in a moment.");
                return;
            }
            packet.sessionId = actionSession;
            packet.round = actionRound;
            packet.configRevision = configRevision;
            packet.configFingerprint = configFingerprint;
            string[] inputs = packet.type == "generate" ? new[] { packet.secondId }
                : TakesTwoOrbs(packet) ? new[] { packet.firstId, packet.secondId } : new[] { packet.firstId };
            if (!IsHost && (packet.type == "combine" || packet.type == "generate"))
                packet.provisionalId = TemporaryOutput(packet.requestId);
            double now = Time.realtimeSinceStartupAsDouble;
            if (!localActions.TryRegister(packet.requestId, inputs, packet.provisionalId, now, out string reason))
            {
                RejectBoardActionLocally(packet, reason);
                return;
            }
            actionRequests.Add(packet.requestId, packet);
            actionStartedAt[packet.requestId] = now;
            if (!IsHost && board != null)
            {
                bool overheated = latest.JigwiFor(LocalPlayerId)?.Overheated(latest.serverTime) == true;
                if (packet.type == "jigwi_burn" || overheated && (packet.type == "combine" && !packet.passiveCombine || packet.type == "launch"))
                {
                    board.PlayJigwiBurnPreview(packet.firstId, TakesTwoOrbs(packet) ? packet.secondId : null);
                    predictedJigwiBurns.Add(packet.requestId);
                }
                else if (overheated && packet.type == "generate")
                {
                    board.PlayJigwiGenerationBurn(new Vector2(packet.x, packet.y));
                    predictedJigwiBurns.Add(packet.requestId);
                }
                else if (packet.type == "combine")
                    board.TryCreateCombinePreview(packet.provisionalId, packet.firstId, packet.secondId,
                        new Vector2(packet.x, packet.y), new Vector2(packet.vx, packet.vy));
                else if (packet.type == "generate")
                    board.TryCreateGenerationPreview(packet.provisionalId, new Vector2(packet.x, packet.y));
                else if (packet.type == "launch")
                {
                    board.BeginPendingLaunch(packet.firstId);
                    BeginPredictedShot(packet);
                }
                else if (packet.type == "transfer" || packet.type == "use")
                    board.BeginPendingLaunch(packet.firstId);
            }
            FlushBoardActions(now);
        }

        private void RejectBoardActionLocally(LabNetworkPacket packet, string reason)
        {
            // A Jigwi release can already have hidden both bodies before the request reaches here.
            // Restore only a primed pending partner. A raw request may name a freely rolling
            // second orb; RejectEdge would zero that unrelated body's passive velocity.
            if (TakesTwoOrbs(packet) && board != null) board.RejectPendingLaunch(packet.secondId);
            RejectLocally(packet.firstId, reason);
        }

        private void FlushBoardActions(double now)
        {
            if (flushingActions || !IsConnected || !CanProcessBattleActions) return;
            flushingActions = true;
            try
            {
                // A synchronous Host receipt may make a dependent action ready in this pass.
                for (int pass = 0; pass < 2; pass++)
                foreach (long id in localActions.GetDueRequestIds(now, true))
                {
                    if (!actionRequests.TryGetValue(id, out var original)) continue;
                    if (!actionWireRequests.TryGetValue(id, out var wire))
                    {
                        if (!localActions.TryResolveInputs(id, out string[] inputs)) continue;
                        wire = JsonUtility.FromJson<LabNetworkPacket>(JsonUtility.ToJson(original));
                        if (wire.type == "generate") wire.secondId = inputs[0];
                        else
                        {
                            wire.firstId = inputs[0];
                            if (TakesTwoOrbs(wire)) wire.secondId = inputs[1];
                        }
                        string ownerId = wire.type == "generate" ? wire.secondId : wire.firstId;
                        if (TryKnownState(ownerId, out var first)) wire.ownershipVersion = first.ownershipVersion;
                        if (TakesTwoOrbs(wire) && TryKnownState(wire.secondId, out var second))
                            wire.secondOwnershipVersion = second.ownershipVersion;
                        // Freeze every field once sent. A retry must not recapture a different pose/version.
                        actionWireRequests.Add(id, wire);
                    }
                    if (!localActions.MarkSent(id, now)) continue;
                    ActionRequestTransmissions++;
                    if (IsHost) HandleRequest(LocalPlayerId, wire);
                    else Send(NetworkManager.ServerClientId, wire);
                }
            }
            finally { flushingActions = false; }
            if (now >= nextActionRecoveryAt)
            {
                nextActionRecoveryAt = now + 2d;
                foreach (long id in localActions.GetRecoveryRequiredRequestIds())
                    Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "action_query",
                        sessionId = actionSession, round = actionRound, requestId = id });
            }
        }

        private bool ReceiveActionRetry(LabNetworkPacket packet)
        {
            if (!CanProcessBattleActions || packet == null || packet.sessionId != actionSession || packet.round != actionRound
                || !localActions.TryGetPending(packet.requestId, out var pending)
                || pending.state != LabPendingActionState.RecoveryRequired
                || !actionWireRequests.TryGetValue(packet.requestId, out var originalRequest)) return false;
            ActionRequestTransmissions++;
            // The Host has no receipt. Resending the same immutable command is safe even if its original arrives later.
            Send(NetworkManager.ServerClientId, originalRequest);
            return true;
        }

        private bool TryKnownState(string id, out LabOrbState state)
        {
            state = null;
            if (id == null) return false;
            if (model != null && IsHost) return model.TryGetMovableState(id, out state);
            if (boardReplica.TryGetState(id, out state)) return true;
            state = (latest?.orbs ?? Array.Empty<LabOrbState>())
                .Concat(latest?.drums ?? Array.Empty<LabOrbState>()).FirstOrDefault(item => item.id == id);
            return state != null;
        }

        private void HandleBoardAction(ulong sender, LabNetworkPacket packet)
        {
            if (!IsHost || model == null || !order.Contains(sender)) return;
            EnsureActionRound();
            var identity = new LabActionIdentity(packet.sessionId, packet.round, sender, packet.requestId);
            string fingerprint = JsonUtility.ToJson(packet);
            LabActionExecution execution = actionResults.Execute(identity, fingerprint,
                () => ExecuteBoardAction(sender, packet), out var receipt, out string reason);
            if (execution == LabActionExecution.Rejected)
            {
                // An invalid round/replay never mutates the model. Same-round rejects can still release UI.
                if (packet.sessionId != actionSession || packet.round != actionRound) return;
                receipt = new LabActionReceipt { sessionId = actionSession, round = actionRound,
                    sender = sender, requestId = packet.requestId, reason = reason,
                    serverTime = battleClock.Now(Time.realtimeSinceStartupAsDouble),
                    sequence = NextBoardSequence(sender) };
            }
            else if (execution == LabActionExecution.Replayed) ActionReplayedResults++;
            DeliverReceipt(sender, receipt);
            if (execution == LabActionExecution.Executed)
            {
                latest = model.Snapshot(battleClock.Now(Time.realtimeSinceStartupAsDouble));
                latest.paused = battleClock.IsPaused && !GameStartHeld && latest.phase == LabPhase.Playing;
                AnnounceBombExplosions();
                AnnounceOrbExpiries();
                AnnounceGourdBombs();
                SyncBoard();
                // Publish small board state before paint checkpoints; accepted actions need no full snapshot.
                PublishOwnerBoards(latest);
                nextSnapshotAt = Math.Min(nextSnapshotAt, Time.realtimeSinceStartupAsDouble + .1d);
            }
        }

        private LabActionReceipt ExecuteBoardAction(ulong sender, LabNetworkPacket packet)
        {
            double now = battleClock.Now(Time.realtimeSinceStartupAsDouble);
            var before = model.CopyOwnedStates(sender);
            bool accepted = false;
            string reason = "INVALID_ACTION";
            LabOrbState output = null;
            string entityId = packet.type == "generate" ? packet.secondId : packet.firstId;
            if (packet.configRevision != configRevision || packet.configFingerprint != configFingerprint)
                reason = "SETTINGS_CHANGED";
            else if (battleClock.IsPaused) reason = "BATTLE_PAUSED";
            else if (!model.TryGetMovableState(entityId, out var input)) reason = "ORB_NOT_FOUND";
            else if (!LabActionValidation.Ownership(input.owner, input.ownershipVersion, sender,
                packet.ownershipVersion, out reason)) { }
            else if (TakesTwoOrbs(packet) && (!model.TryGetMovableState(packet.secondId, out var partner)
                || !LabActionValidation.Ownership(partner.owner, partner.ownershipVersion, sender,
                    packet.secondOwnershipVersion, out reason))) reason = reason ?? "ORB_NOT_FOUND";
            else
            {
                string unique = sender + ":" + packet.requestId;
                switch (packet.type)
                {
                    case "generate":
                        accepted = model.TryGenerate(sender, packet.secondId, new Vector2(packet.x, packet.y),
                            unique, now, out output, out reason);
                        break;
                    case "combine":
                        if (packet.passiveCombine && !config.ContactCombineEnabled)
                        { reason = "CONTACT_COMBINE_DISABLED"; break; }
                        accepted = model.TryCombineAt(sender, packet.firstId, packet.secondId, unique, now,
                            new Vector2(packet.x, packet.y), new Vector2(packet.vx, packet.vy), out output, out reason,
                            passiveContact: packet.passiveCombine);
                        break;
                    case "jigwi_burn":
                        accepted = model.TryJigwiBurn(sender, packet.firstId,
                            string.IsNullOrEmpty(packet.secondId) ? null : packet.secondId, unique, now, out reason);
                        break;
                    case "fox":
                        accepted = model.TryFoxFireAt(sender, packet.firstId, packet.secondId, unique, now, out reason);
                        break;
                    case "use":
                        accepted = model.TryUseItemAt(sender, packet.firstId, unique, now,
                            new Vector2(packet.x, packet.y), out output, out reason);
                        break;
                    case "transfer":
                        if (packet.crossing < 0 || packet.crossing > (int)LabEdgeCrossingKind.Pushed)
                        { reason = "INVALID_CROSSING"; break; }
                        accepted = model.TryTransferAt(sender, packet.firstId, packet.right,
                            new Vector2(packet.x, packet.y), new Vector2(packet.vx, packet.vy), unique, now,
                            (LabEdgeCrossingKind)packet.crossing, out output, out reason);
                        break;
                    case "launch":
                        Vector2 swipe = new Vector2(packet.vx, packet.vy);
                        if (target == null || target.Hitbox == null || battleFloor == null)
                        { reason = "Battle target or floor is unavailable."; break; }
                        if (!Finite(packet.x) || packet.x < 0f || packet.x > 1f
                            || !LabThrowMath.IsThrowGesture(swipe, packet.y, config))
                        { reason = "Swipe the Combined orb upward to throw."; break; }
                        accepted = model.TryLaunch(sender, packet.firstId, unique, now, out output, out reason);
                        if (accepted && output != null) SpawnHostProjectile(output, swipe, packet.x, packet.requestId);
                        break;
                }
            }
            // Expiry may have exploded a bomb while processing this command. Announce before removing its local body.
            AnnounceBombExplosions();
            AnnounceOrbExpiries();
            AnnounceGourdBombs();
            LabOrbState[] after = model.CopyOwnedStates(sender);
            var afterIds = new HashSet<string>(after.Select(item => item.id), StringComparer.Ordinal);
            var states = after.Where(item => !before.Any(old => old.id == item.id && old.revision == item.revision))
                .Select(item => item.Copy()).ToList();
            if (accepted && output != null && !afterIds.Contains(output.id)) states.Add(output.Copy());
            var receipt = new LabActionReceipt { accepted = accepted, reason = reason,
                serverTime = now, modelRevision = model.Revision, sequence = NextBoardSequence(sender),
                removedIds = before.Where(item => !afterIds.Contains(item.id)).Select(item => item.id).ToArray(),
                states = states.ToArray(), players = model.CopyPlayers(),
                provisionalId = packet.provisionalId, outputId = accepted ? output?.id : null,
                consumedWithoutOutput = accepted && reason == "JIGWI_BURNED" };
            Debug.Log("C6_LAB_ACTION type=" + packet.type + " sender=" + sender + " accepted=" + accepted
                + " request=" + packet.requestId + " reason=" + reason);
            return receipt;
        }

        private void DeliverReceipt(ulong sender, LabActionReceipt receipt)
        {
            if (sender == LocalPlayerId) ReceiveActionReceipt(receipt);
            else Send(sender, new LabNetworkPacket { type = "action_result", sessionId = actionSession,
                round = actionRound, actionReceipt = receipt });
        }

        private void ReceiveActionReceipt(LabActionReceipt receipt)
        {
            if (!CanProcessBattleActions || receipt == null || !localActions.ApplyReceipt(receipt, out var resolution)
                || !actionRequests.TryGetValue(receipt.requestId, out var request)) return;
            if (actionStartedAt.TryGetValue(receipt.requestId, out double at))
                LastActionRoundTripMilliseconds = (Time.realtimeSinceStartupAsDouble - at) * 1000d;
            foreach (long child in resolution.rejectedDependents) RollBackAction(child);
            if (board != null)
            {
                if (request.type == "generate") board.RemoveGenerationPreview(request.provisionalId);
                if (receipt.accepted && receipt.consumedWithoutOutput)
                    CommitJigwiBurn(request);
                else if (receipt.accepted)
                {
                    // The local heat display can end a frame before the Host accepts a normal combination.
                    if (request.type == "combine" && predictedJigwiBurns.Contains(request.requestId))
                    {
                        board.RejectPendingLaunch(ResolveLocalId(request.firstId));
                        board.RejectPendingLaunch(ResolveLocalId(request.secondId));
                    }
                    if (request.type == "combine" && !string.IsNullOrEmpty(request.provisionalId))
                    {
                        LabOrbState result = receipt.states.FirstOrDefault(item => item.id == receipt.outputId);
                        if (result != null) board.CommitCombinePreview(request.provisionalId, result);
                    }
                    if (request.type == "launch" || request.type == "transfer" || request.type == "use"
                        || request.type == "fox")
                        board.CommitPendingLaunch(ResolveLocalId(request.firstId));
                    if (request.type == "fox") board.CommitPendingLaunch(ResolveLocalId(request.secondId));
                }
                else
                {
                    // Restore dependent pending views before removing their provisional parent.
                    RollBackAction(receipt.requestId);
                }
            }
            if (receipt.accepted && !receipt.consumedWithoutOutput && request.type == "combine") LocalCombined?.Invoke();
            predictedJigwiBurns.Remove(receipt.requestId);
            if (!receipt.accepted) SetNotice(receipt.reason ?? "Request rejected.");
            if (!IsHost)
            {
                boardReplica.ApplyReceipt(receipt);
                latest = boardReplica.Overlay(latest);
                SyncBoard();
            }
            Debug.Log("C6_LAB_ACTION_RESULT type=" + request.type + " request=" + receipt.requestId
                + " accepted=" + receipt.accepted + " rttMs="
                + LastActionRoundTripMilliseconds.ToString("F1", CultureInfo.InvariantCulture));
            Changed?.Invoke();
            FlushPendingBlastReports(Time.realtimeSinceStartupAsDouble);
            FlushBoardActions(Time.realtimeSinceStartupAsDouble);
        }

        private string ResolveLocalId(string id) => localActions.TryGetMappedId(id, out string mapped) ? mapped : id;

        private void RollBackAction(long requestId)
        {
            if (!actionRequests.TryGetValue(requestId, out var request)) return;
            if (predictedShots.TryGetValue(requestId, out var shot) && shot != null) Destroy(shot.gameObject);
            predictedShots.Remove(requestId);
            if (board == null) return;
            if (request.type == "generate") board.RemoveGenerationPreview(request.provisionalId);
            else if (request.type == "combine")
            {
                if (!board.RejectCombinePreview(request.provisionalId))
                {
                    board.RejectPendingLaunch(ResolveLocalId(request.firstId));
                    board.RejectPendingLaunch(ResolveLocalId(request.secondId));
                }
            }
            else
            {
                string id = ResolveLocalId(request.firstId);
                board.RejectPendingLaunch(id);
                board.RejectEdge(id);
                if (TakesTwoOrbs(request))
                {
                    string partner = ResolveLocalId(request.secondId);
                    board.RejectPendingLaunch(partner);
                    board.RejectEdge(partner);
                }
            }
        }

        private void PublishOwnerBoards(LabSnapshot snapshot, bool force = false, ulong? only = null)
        {
            if (snapshot == null || !IsHost || model == null) return;
            AnnounceBombExplosions();
            AnnounceOrbExpiries();
            AnnounceGourdBombs();
            CaptureTargetPose(snapshot);
            foreach (ulong peer in order)
            {
                if (peer == LocalPlayerId || only.HasValue && only.Value != peer) continue;
                var frame = boardPublisher.Build(snapshot, peer, NextBoardSequence(peer), model.Revision, force);
                SendBoardFrame(peer, frame);
            }
        }

        private void ReceiveBoardFrame(LabBoardFrame frame)
        {
            if (frame == null) return;
            if (latest == null || latest.sessionId != frame.sessionId || latest.round != frame.round)
            {
                if (latest == null || frame.sessionId == latest.sessionId && frame.round > latest.round)
                    deferredBoardFrame = frame;
                return;
            }
            EnsureActionRound();
            if (boardReplica.TryApply(frame, out _))
            {
                ReceivedBoardFrames++;
                latest = boardReplica.Overlay(latest);
                ApplyLocalPause(CanProcessBattleActions && frame.paused && frame.phase == LabPhase.Playing);
                ApplyFastTargetPose();
                SyncBoard();
                Changed?.Invoke();
            }
            else if (boardReplica.NeedsCheckpoint && Time.realtimeSinceStartupAsDouble >= nextBoardCheckpointAt)
            {
                nextBoardCheckpointAt = Time.realtimeSinceStartupAsDouble + .5d;
                SyncBoard();
                Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "board_checkpoint",
                    sessionId = actionSession, round = actionRound });
            }
        }

        private void BeginPredictedShot(LabNetworkPacket request)
        {
            if (IsHost || target == null || config == null || latest?.players == null) return;
            LabPlayerState player = latest.players.FirstOrDefault(item => item.id == LocalPlayerId);
            if (player == null) return;
            string id = ResolveLocalId(request.firstId);
            TryKnownState(id, out var orb);
            Vector3 center = target.transform.position;
            int ring = LabTeams.RingSeatFor(latest, player.seat);
            Vector3 anchor = LabThrowMath.SeatPosition(ring, latest.players.Length, center,
                config.ThrowOriginRadius, config.ThrowOriginHeightOffset, !LabTeams.FourPlayerTeams(latest));
            Vector3 position = LabThrowMath.LaunchPosition(ring, latest.players.Length, center, request.x, config);
            Vector3 velocity = LabThrowMath.LaunchVelocity(new Vector2(request.vx, request.vy), anchor, center, config);
            bool small = orb != null && orb.small;
            LabOrbElement element = orb?.element ?? LabOrbElement.None;
            if (orb == null && board != null) board.TryGetLocalAppearance(request.firstId, out element, out small);
            LabProjectile visual = LabProjectile.Create(position, config, projectileRoot,
                element, small ? config.BlastShrinkScale : 1f);
            bool fiveColor = orb != null ? orb.fiveColor : board != null && board.IsFiveColor(request.firstId);
            if (fiveColor) LabFiveColorSparkle.AttachArt(visual.transform, config.OrbRadius * LabOrbSpriteArt.SizeScaleFor(LabOrbKind.Combined), true);
            float scale = player.comboPenaltyUntil > latest.serverTime ? config.ComboPenaltySizeScale
                : player.comboRewardUntil > latest.serverTime ? config.ComboRewardSizeScale : 1f;
            if (config.GourdColorComboEnabled) visual.SetRadiusScale(scale);
            visual.SetMissSurface(battleFloor);
            SyncJigwiFireSurfaces();
            visual.SetFireSurfaces(JigwiFireSurfacesFor(LocalPlayerId));
            visual.LaunchVisual(velocity, config, target);
            predictedShots[request.requestId] = visual;
            presentedShots.Add(request.requestId);
        }

        private bool HasPresentedProjectile(LabNetworkPacket packet)
        {
            if (packet.round != actionRound || packet.sessionId != actionSession) return true;
            if (packet.owner == LocalPlayerId && presentedShots.Contains(packet.requestId))
            {
                if (predictedShots.TryGetValue(packet.requestId, out var visual) && visual != null)
                    visual.ReconcileVisual((LabOrbElement)packet.element,
                        packet.small ? config.BlastShrinkScale : 1f, packet.sizeScale);
                return true;
            }
            return !presentedRemoteShots.Add(packet.firstId ?? string.Empty);
        }
    }
}
