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
            snapshot.paused = battleClock.IsPaused && snapshot.phase == LabPhase.Playing;
            var hanging = target != null ? target.GetComponent<LabHangingTarget>() : null;
            if (hanging == null) return;
            snapshot.targetYawDegrees = hanging.YawAngleDegrees;
            snapshot.targetYawSpeedDegreesPerSecond = hanging.YawSpeedDegreesPerSecond;
            snapshot.targetRopeCycleElapsed = hanging.CycleElapsed;
            snapshot.targetRopeHitOffset = hanging.VerticalOffset;
            snapshot.targetRopeHitVelocity = hanging.VerticalVelocity;
        }

        private void ApplyFastTargetPose()
        {
            if (latest == null || target == null) return;
            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            if (hanging == null) return;
            bool paused = latest.paused && latest.phase == LabPhase.Playing;
            hanging.ReconcileYaw(latest.targetYawDegrees, latest.targetYawSpeedDegreesPerSecond, paused);
            hanging.ReconcileRope(latest.targetRopeCycleElapsed, latest.targetRopeHitOffset,
                latest.targetRopeHitVelocity, paused);
        }
    }
}
