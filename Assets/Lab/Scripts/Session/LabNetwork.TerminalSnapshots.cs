using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    public sealed partial class LabNetwork
    {
        private readonly HashSet<ulong> deferredTerminalSnapshotPeers = new HashSet<ulong>();
        private string deferredTerminalSnapshotSession;
        private uint deferredTerminalSnapshotRound;

        /// <summary>
        /// Normal snapshots coalesce while Bulk is full. A frozen game result has no future
        /// gameplay publish, so retain that last transaction until every skipped peer can queue it.
        /// </summary>
        private void PublishSharedSnapshots(long sequence)
        {
            bool terminal = gameFlowEnabled && latest != null
                && (latest.phase == LabPhase.Defeat || latest.phase == LabPhase.Victory);
            if (!terminal || deferredTerminalSnapshotSession != latest.sessionId
                || deferredTerminalSnapshotRound != latest.round)
            {
                ClearDeferredTerminalSnapshots();
                if (terminal)
                {
                    deferredTerminalSnapshotSession = latest.sessionId;
                    deferredTerminalSnapshotRound = latest.round;
                }
            }
            foreach (ulong id in order)
            {
                if (id == manager.LocalClientId) continue;
                if (BulkBacklogFull(id))
                {
                    if (terminal) deferredTerminalSnapshotPeers.Add(id);
                    continue;
                }
                if (!QueueLatestSharedSnapshot(id, sequence)) return;
                deferredTerminalSnapshotPeers.Remove(id);
            }
        }

        private void FlushDeferredTerminalSnapshots()
        {
            if (deferredTerminalSnapshotPeers.Count == 0) return;
            if (!IsHost || model == null || latest == null
                || latest.sessionId != deferredTerminalSnapshotSession || latest.round != deferredTerminalSnapshotRound
                || (latest.phase != LabPhase.Defeat && latest.phase != LabPhase.Victory))
            { ClearDeferredTerminalSnapshots(); return; }
            foreach (ulong id in deferredTerminalSnapshotPeers.ToArray())
            {
                if (!order.Contains(id)) { deferredTerminalSnapshotPeers.Remove(id); continue; }
                if (BulkBacklogFull(id)) continue;
                if (!QueueLatestSharedSnapshot(id, ++snapshotSequence)) return;
                deferredTerminalSnapshotPeers.Remove(id);
            }
        }

        // The exact same frame/chunk encoding and ordered Bulk channel serve live publishes and recovery.
        private bool QueueLatestSharedSnapshot(ulong id, long sequence)
        {
            var shared = LabBoardReplica.CopySnapshot(latest);
            string roundKey = latest.sessionId + "/" + latest.round;
            if (peerSharedRounds.TryGetValue(id, out string sentRound) && sentRound == roundKey)
            {
                shared.orbs = Array.Empty<LabOrbState>();
                shared.drums = Array.Empty<LabOrbState>();
            }
            string[] pieces;
            try { pieces = LabSessionSettings.ChunkSnapshot(shared); }
            catch (ArgumentException exception) { AbortOversizedSnapshot(exception.Message); return false; }
            LabPaintFrame[] paintFrames = PeerPaintPublisher(id).Publish(model.PaintSurface, sessionId, latest.round, sequence);
            if (!SendPaintFrames(id, paintFrames))
            { AbortOversizedSnapshot("Could not queue the complete paint transaction."); return false; }
            if (paintFrames.Length > 0 && paintFrames[0].full) MarkPaintFullQueued(id);
            for (int i = 0; i < pieces.Length; i++)
                if (!QueueBulk(id, new LabNetworkPacket { type = "snapshot_chunk", sessionId = sessionId,
                    round = latest.round, configRevision = configRevision, configFingerprint = configFingerprint,
                    snapshotSequence = sequence, chunkIndex = i, chunkCount = pieces.Length, chunkData = pieces[i] }))
                { AbortOversizedSnapshot("Could not queue the complete battle state."); return false; }
            peerSharedRounds[id] = roundKey;
            return true;
        }

        private void ClearDeferredTerminalSnapshots()
        {
            deferredTerminalSnapshotPeers.Clear();
            deferredTerminalSnapshotSession = null;
            deferredTerminalSnapshotRound = 0;
        }
    }
}
