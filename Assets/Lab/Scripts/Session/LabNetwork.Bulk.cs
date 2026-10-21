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
        public LabPaintFrame paintFrame;
    }

    public sealed partial class LabNetwork
    {
        private const string BulkMessageName = "C6Lab.Bulk.1";
        private readonly Dictionary<ulong, LabBulkSender> bulkSenders = new Dictionary<ulong, LabBulkSender>();
        private readonly Dictionary<ulong, LabPaintPublisher> peerPaintPublishers = new Dictionary<ulong, LabPaintPublisher>();
        private readonly Dictionary<ulong, string> peerSharedRounds = new Dictionary<ulong, string>();
        private readonly LabBulkReceiver bulkReceiver = new LabBulkReceiver();
        private readonly LabBulkMessageAssembler bulkMessages = new LabBulkMessageAssembler();
        public long SentControlBytes { get; private set; }
        public long SentBulkBytes { get; private set; }

        private bool HasPendingBulk(ulong peer) => bulkSenders.TryGetValue(peer, out var sender) && sender.Count > 0;
        private LabPaintPublisher PeerPaintPublisher(ulong peer)
        {
            if (!peerPaintPublishers.TryGetValue(peer, out var publisher))
                peerPaintPublishers.Add(peer, publisher = new LabPaintPublisher());
            return publisher;
        }

        private bool QueueBulk(ulong peer, LabNetworkPacket packet)
        {
            var body = new LabBulkPayload { type = packet.type, sessionId = packet.sessionId, round = packet.round,
                configRevision = packet.configRevision, configFingerprint = packet.configFingerprint,
                snapshotSequence = packet.snapshotSequence, chunkIndex = packet.chunkIndex,
                chunkCount = packet.chunkCount, chunkData = packet.chunkData, paintFrame = packet.paintFrame };
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
            if (!IsHost || !IsConnected) return;
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (ulong peer in order)
            {
                if (!bulkSenders.TryGetValue(peer, out var sender)) continue;
                foreach (LabBulkFrame frame in sender.GetDue(now, 16384))
                    SendBulkWire(peer, new LabBulkEnvelope { sequence = frame.sequence, payload = frame.payload });
            }
        }

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
                        && bulkSenders.TryGetValue(sender, out var outgoing)) outgoing.Ack(wire.ack);
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
                            chunkCount = body.chunkCount, chunkData = body.chunkData, paintFrame = body.paintFrame };
                        if (packet.type == "paint_cells") ReceivePaintFrame(packet);
                        else if (packet.type == "snapshot_chunk") ReceiveSnapshotChunk(packet);
                    }
                }
                // A duplicate may mean the previous acknowledgement was lost. Always repeat the cumulative ACK.
                SendBulkWire(NetworkManager.ServerClientId, new LabBulkEnvelope { ack = bulkReceiver.AckThrough });
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                Debug.LogWarning("C6_LAB_BULK_INVALID " + exception.GetType().Name);
            }
        }

        private void ResetBulkTransport()
        {
            bulkSenders.Clear(); peerPaintPublishers.Clear(); peerSharedRounds.Clear(); bulkReceiver.Clear(); bulkMessages.Clear();
        }
    }
}
