using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6Lab
{
    [Serializable]
    internal sealed class LabBulkEnvelope
    {
        public long sequence, ack;
        /// <summary>Guest ACKs only: <see cref="LabBulkReceiver.SelectiveAck"/>, the chunks held after the gap at ack + 1.</summary>
        public long sack;
        public string payload;
    }

    // Keep large traffic compact; the general control packet has many irrelevant default fields.
    [Serializable]
    internal sealed class LabBulkPayload
    {
        public string type, sessionId, configFingerprint, chunkData;
        public uint round;
        public int configRevision, chunkIndex, chunkCount;
        public long snapshotSequence;
        /// <summary>The paint frame as JSON text, present only on paint_cells (#428).</summary>
        public string payload;
    }

    public sealed partial class LabNetwork
    {
        private const string BulkMessageName = "C6Lab.Bulk.1";
        private readonly Dictionary<ulong, LabBulkSender> bulkSenders = new Dictionary<ulong, LabBulkSender>();
        private readonly Dictionary<ulong, LabPaintPublisher> peerPaintPublishers = new Dictionary<ulong, LabPaintPublisher>();
        /// <summary>One surface copy, hash and encoding per publish pass, shared by every peer's publisher (#439).</summary>
        private readonly LabPaintPublishCache paintPublishCache = new LabPaintPublishCache();
        private readonly Dictionary<ulong, string> peerSharedRounds = new Dictionary<ulong, string>();
        private readonly LabBulkReceiver bulkReceiver = new LabBulkReceiver();
        private readonly LabBulkMessageAssembler bulkMessages = new LabBulkMessageAssembler();
        /// <summary>The frame of the Guest's latest Bulk ACK, repeated once on the first frame without Bulk data.</summary>
        private int bulkAckRepeatFrame = -1;
        public long SentControlBytes { get; private set; }
        public long SentBulkBytes { get; private set; }

        /// <summary>
        /// Queued bytes (unsent or unacknowledged) at which the Host stops adding snapshots and paint for a peer
        /// until it drains. Far below the 4 MiB queue limit, so one stage-start paint map plus a snapshot always fits.
        /// </summary>
        public const int BulkBacklogLimitBytes = 64 * 1024;

        /// <summary>
        /// A slow peer is skipped only once its backlog is this large, not whenever any chunk awaits its ACK:
        /// that skipped every 100 ms snapshot for a whole round trip even without loss (#437). A skipped peer
        /// still coalesces to the newest state on its next publish.
        /// </summary>
        private bool BulkBacklogFull(ulong peer) => bulkSenders.TryGetValue(peer, out var sender)
            && sender.QueuedBytes >= BulkBacklogLimitBytes;
        /// <summary>The last Bulk sequence of the most recent full paint map queued to each peer (round start or checkpoint).</summary>
        private readonly Dictionary<ulong, long> paintCheckpointThrough = new Dictionary<ulong, long>();

        /// <summary>A full map is several hundred KB; another one before the last is acknowledged repairs nothing.</summary>
        private bool PaintCheckpointInFlight(ulong peer) => paintCheckpointThrough.TryGetValue(peer, out long through)
            && bulkSenders.TryGetValue(peer, out var sender) && sender.Acknowledged < through;

        private void MarkPaintFullQueued(ulong peer)
        {
            if (bulkSenders.TryGetValue(peer, out var sender)) paintCheckpointThrough[peer] = sender.LastEnqueued;
        }

        private LabPaintPublisher PeerPaintPublisher(ulong peer)
        {
            if (!peerPaintPublishers.TryGetValue(peer, out var publisher))
                peerPaintPublishers.Add(peer, publisher = new LabPaintPublisher(paintPublishCache, LabPaintEncodingPolicy.Auto));
            return publisher;
        }

        private bool QueueBulk(ulong peer, LabNetworkPacket packet)
        {
            var body = new LabBulkPayload { type = packet.type, sessionId = packet.sessionId, round = packet.round,
                configRevision = packet.configRevision, configFingerprint = packet.configFingerprint,
                snapshotSequence = packet.snapshotSequence, chunkIndex = packet.chunkIndex,
                chunkCount = packet.chunkCount, chunkData = packet.chunkData, payload = packet.payload };
            string encoded = Convert.ToBase64String(Utf8.GetBytes(JsonUtility.ToJson(body)));
            if (!bulkSenders.TryGetValue(peer, out var sender)) bulkSenders.Add(peer, sender = new LabBulkSender());
            string[] fragments;
            try { fragments = LabBulkMessageChunks.Split(encoded); }
            catch (ArgumentException) { return false; }
            int queuedBytes = 0;
            foreach (string fragment in fragments) queuedBytes += Utf8.GetByteCount(fragment);
            if (queuedBytes > LabBulkSender.DefaultMaximumQueuedBytes - sender.QueuedBytes) return false;
            foreach (string fragment in fragments)
                if (!sender.Enqueue(fragment, Time.realtimeSinceStartupAsDouble)) return false;
            return true;
        }

        private void FlushBulkPackets()
        {
            if (!IsConnected) return;
            if (!IsHost) { RepeatBulkAck(); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (ulong peer in order)
            {
                if (!bulkSenders.TryGetValue(peer, out var sender)) continue;
                foreach (LabBulkFrame frame in sender.GetDue(now, 16384))
                    SendBulkWire(peer, new LabBulkEnvelope { sequence = frame.sequence, payload = frame.payload });
            }
        }

        /// <summary>
        /// NGO sends one frame's messages together, so losing that packet loses every Bulk ACK of the frame. While chunks
        /// keep arriving, the next frame's ACKs carry the same state. After the last frame of a burst nothing does until
        /// the next publish, which is later than the Host's timeout, so the first frame without Bulk data repeats it once.
        /// </summary>
        private void RepeatBulkAck()
        {
            if (bulkAckRepeatFrame < 0 || Time.frameCount <= bulkAckRepeatFrame) return;
            bulkAckRepeatFrame = -1;
            SendBulkAck();
        }

        private void SendBulkAck() => SendBulkWire(NetworkManager.ServerClientId, new LabBulkEnvelope
            { ack = bulkReceiver.AckThrough, sack = bulkReceiver.SelectiveAck });

        private bool SendBulkWire(ulong receiver, LabBulkEnvelope envelope)
        {
            if (manager == null || !manager.IsListening || manager.ShutdownInProgress) return false;
            byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(envelope));
            // NGO named Unreliable messages must fit its 1,296-byte non-fragmented limit, including headers.
            if (bytes.Length > 1200) return false;
            try
            {
                using (var writer = new FastBufferWriter(bytes.Length + 4, Allocator.Temp))
                {
                    writer.WriteValueSafe(bytes.Length);
                    writer.WriteBytesSafe(bytes);
                    manager.CustomMessagingManager.SendNamedMessage(BulkMessageName, receiver, writer,
                        NetworkDelivery.Unreliable);
                }
                SentBulkBytes += bytes.Length + 4;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("C6_LAB_BULK_SEND_FAILED " + exception.GetType().Name);
                return false;
            }
        }

        private void ReceiveBulk(ulong sender, FastBufferReader reader)
        {
            if (joinFinishPending || reader.Length - reader.Position < 4 || !reader.TryBeginRead(4)) return;
            try
            {
                reader.ReadValueSafe(out int length);
                if (length < 2 || length > 1200 || reader.Length - reader.Position != length
                    || !reader.TryBeginRead(length)) return;
                byte[] bytes = new byte[length];
                reader.ReadBytesSafe(ref bytes, length);
                var wire = JsonUtility.FromJson<LabBulkEnvelope>(Utf8.GetString(bytes));
                if (wire == null) return;
                if (IsHost)
                {
                    if (order.Contains(sender) && string.IsNullOrEmpty(wire.payload)
                        && bulkSenders.TryGetValue(sender, out var outgoing))
                        outgoing.Ack(wire.ack, wire.sack, Time.realtimeSinceStartupAsDouble);
                    return;
                }
                if (sender != NetworkManager.ServerClientId || !clientConfigReady) return;
                if (bulkReceiver.Receive(wire.sequence, wire.payload, out string[] ready))
                {
                    foreach (string fragment in ready)
                    {
                        if (!bulkMessages.Receive(fragment, out string encoded)) continue;
                        var body = JsonUtility.FromJson<LabBulkPayload>(Utf8.GetString(Convert.FromBase64String(encoded)));
                        if (body == null) continue;
                        var packet = new LabNetworkPacket { type = body.type, sessionId = body.sessionId, round = body.round,
                            configRevision = body.configRevision, configFingerprint = body.configFingerprint,
                            snapshotSequence = body.snapshotSequence, chunkIndex = body.chunkIndex,
                            chunkCount = body.chunkCount, chunkData = body.chunkData, payload = body.payload };
                        if (packet.type == "paint_art") ReceivePaintArtPage(packet);
                        else if (packet.type == "paint_cells") ReceivePaintFrame(packet);
                        else if (packet.type == "snapshot_chunk") ReceiveSnapshotChunk(packet);
                    }
                }
                // A duplicate may mean the previous acknowledgement was lost. Always repeat the cumulative ACK and
                // the chunks held after its gap, so the Host resends only what is missing.
                SendBulkAck();
                bulkAckRepeatFrame = Time.frameCount;
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                Debug.LogWarning("C6_LAB_BULK_INVALID " + exception.GetType().Name);
            }
        }

        private void ResetBulkTransport()
        {
            ClearDeferredTerminalSnapshots();
            bulkSenders.Clear(); peerPaintPublishers.Clear(); paintPublishCache.Clear();
            peerSharedRounds.Clear(); bulkReceiver.Clear(); bulkMessages.Clear();
            paintCheckpointThrough.Clear(); bulkAckRepeatFrame = -1;
        }
    }
}
