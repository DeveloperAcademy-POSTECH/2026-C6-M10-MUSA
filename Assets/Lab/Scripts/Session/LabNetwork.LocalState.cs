using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabNetwork
    {
        private string displayedPaintSession;
        private uint displayedPaintRound, displayedPaintRevision;
        private int displayedPaintCount = -1;
        private readonly List<LabNetworkPacket> pendingBlastReports = new List<LabNetworkPacket>();
        private string boardChunkSession;
        private uint boardChunkRound;
        private long boardChunkSequence;
        private string[] boardChunks;
        private int receivedBoardChunkCount;

        private bool SendBoardFrame(ulong recipient, LabBoardFrame frame)
        {
            string json = JsonUtility.ToJson(frame);
            if (json.Length > LabSessionSettings.MaximumSnapshotCharacters
                || Utf8.GetByteCount(json) > LabSessionSettings.MaximumSnapshotBytes) return false;
            const int characters = 4096;
            int count = Math.Max(1, (json.Length + characters - 1) / characters);
            for (int i = 0; i < count; i++)
            {
                int start = i * characters;
                if (!Send(recipient, new LabNetworkPacket { type = "board_delta", sessionId = frame.sessionId,
                    round = frame.round, configRevision = configRevision, configFingerprint = configFingerprint,
                    snapshotSequence = frame.sequence, chunkIndex = i, chunkCount = count,
                    chunkData = json.Substring(start, Math.Min(characters, json.Length - start)) })) return false;
            }
            return true;
        }

        private void ReceiveBoardChunk(LabNetworkPacket packet)
        {
            if (!clientConfigReady || packet.configRevision != configRevision
                || packet.configFingerprint != configFingerprint || string.IsNullOrEmpty(packet.sessionId)
                || packet.round == 0 || packet.snapshotSequence <= 0 || packet.chunkCount < 1 || packet.chunkCount > 128
                || packet.chunkIndex < 0 || packet.chunkIndex >= packet.chunkCount || packet.chunkData == null
                || packet.chunkData.Length > 4096) return;
            if (packet.sessionId != boardChunkSession || packet.round != boardChunkRound
                || packet.snapshotSequence != boardChunkSequence)
            {
                if (packet.sessionId == boardChunkSession && packet.round == boardChunkRound
                    && packet.snapshotSequence < boardChunkSequence) return;
                boardChunkSession = packet.sessionId; boardChunkRound = packet.round;
                boardChunkSequence = packet.snapshotSequence;
                boardChunks = new string[packet.chunkCount]; receivedBoardChunkCount = 0;
            }
            if (boardChunks == null || packet.chunkCount != boardChunks.Length) return;
            if (boardChunks[packet.chunkIndex] == null)
            { boardChunks[packet.chunkIndex] = packet.chunkData; receivedBoardChunkCount++; }
            else if (boardChunks[packet.chunkIndex] != packet.chunkData) return;
            if (receivedBoardChunkCount != boardChunks.Length) return;
            string json = string.Concat(boardChunks);
            boardChunks = null;
            if (json.Length > LabSessionSettings.MaximumSnapshotCharacters
                || Utf8.GetByteCount(json) > LabSessionSettings.MaximumSnapshotBytes) return;
            var frame = JsonUtility.FromJson<LabBoardFrame>(json);
            if (frame == null || frame.sequence != packet.snapshotSequence || frame.sessionId != packet.sessionId
                || frame.round != packet.round) return;
            ReceiveBoardFrame(frame);
        }

        private bool DeferBlastReport(LabNetworkPacket packet)
        {
            if (packet.ids == null) return false;
            string[] resolved = new string[packet.ids.Length];
            bool waiting = false;
            for (int i = 0; i < resolved.Length; i++)
            {
                resolved[i] = ResolveLocalId(packet.ids[i]);
                waiting |= resolved[i] == packet.ids[i] && board != null && board.IsProvisional(packet.ids[i]);
            }
            if (!waiting) { packet.ids = resolved; return false; }
            pendingBlastReports.Add(packet);
            return true;
        }

        private void FlushPendingBlastReports(double now)
        {
            if (pendingBlastReports.Count == 0 || !IsConnected || !CanProcessBattleActions) return;
            foreach (var packet in pendingBlastReports.ToArray())
            {
                var resolved = new List<string>();
                bool waiting = false;
                foreach (string id in packet.ids ?? Array.Empty<string>())
                {
                    if (localActions.TryGetMappedId(id, out string canonical)) resolved.Add(canonical);
                    else if (board != null && board.IsProvisional(id)) waiting = true;
                    else if (TryKnownState(id, out _)) resolved.Add(id);
                }
                if (waiting) continue;
                pendingBlastReports.Remove(packet);
                packet.ids = resolved.ToArray();
                if (IsHost) HandleRequest(LocalPlayerId, packet);
                else Send(Unity.Netcode.NetworkManager.ServerClientId, packet);
            }
        }

        private void CaptureTargetPose(LabSnapshot snapshot)
        {
            if (snapshot == null) return;
            snapshot.paused = battleClock.IsPaused && !GameStartHeld && snapshot.phase == LabPhase.Playing;
            var hanging = target != null ? target.GetComponent<LabHangingTarget>() : null;
            if (hanging == null) return;
            snapshot.targetYawDegrees = hanging.YawAngleDegrees;
            snapshot.targetYawSpeedDegreesPerSecond = hanging.YawSpeedDegreesPerSecond;
            snapshot.targetRopeCycleElapsed = hanging.CycleElapsed;
            snapshot.targetRopeHitOffset = hanging.VerticalOffset;
            snapshot.targetRopeHitVelocity = hanging.VerticalVelocity;
        }

        private void ApplyFastTargetPose() => ReconcileTargetPose(latest);

        // The newest Host pose sample this receiver blended toward, per session/round.
        private string reconciledPoseSession;
        private uint reconciledPoseRound;
        private double reconciledPoseServerTime = double.NegativeInfinity;
        /// <summary>
        /// Real time until which running samples leave the rope offset and yaw alone, so a header frame
        /// captured before a predicted local impact cannot undo that impact while the Host verdict is in flight.
        /// Nothing holds yet; the own-throw presentation will.
        /// </summary>
        private double predictedImpactHoldUntil = double.NegativeInfinity;

        /// <summary>Leave the rope offset and yaw to local physics until the real time given (or an earlier release).</summary>
        internal void HoldTargetPoseForPredictedImpact(double untilRealtime) => predictedImpactHoldUntil = untilRealtime;
        internal void ReleaseTargetPoseHold() => predictedImpactHoldUntil = double.NegativeInfinity;

        private void ResetTargetPoseReconcile()
        {
            reconciledPoseSession = null;
            reconciledPoseRound = 0;
            reconciledPoseServerTime = double.NegativeInfinity;
            predictedImpactHoldUntil = double.NegativeInfinity;
        }

        /// <summary>
        /// Blend the hanging target toward a Host pose sample once. A bulk snapshot repeats the header pose
        /// the board frame already applied (Overlay keeps the header when it is at least as new), and
        /// blending the same stale sample twice jerked the gourd backwards after every hit.
        /// Paused samples always apply: a paused target has one authoritative pose.
        /// </summary>
        private void ReconcileTargetPose(LabSnapshot snapshot)
        {
            if (snapshot == null || target == null) return;
            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            if (hanging == null) return;
            bool paused = snapshot.paused && snapshot.phase == LabPhase.Playing;
            bool sameRound = snapshot.sessionId == reconciledPoseSession && snapshot.round == reconciledPoseRound;
            if (!paused && sameRound && snapshot.serverTime <= reconciledPoseServerTime) return;
            bool hold = !paused && Time.realtimeSinceStartupAsDouble < predictedImpactHoldUntil;
            if (!hold && Finite(snapshot.targetYawDegrees) && Finite(snapshot.targetYawSpeedDegreesPerSecond))
                hanging.ReconcileYaw(snapshot.targetYawDegrees, snapshot.targetYawSpeedDegreesPerSecond, paused);
            hanging.ReconcileRope(snapshot.targetRopeCycleElapsed, snapshot.targetRopeHitOffset,
                snapshot.targetRopeHitVelocity, paused, hold);
            reconciledPoseSession = snapshot.sessionId;
            reconciledPoseRound = snapshot.round;
            reconciledPoseServerTime = snapshot.serverTime;
        }
    }
}
