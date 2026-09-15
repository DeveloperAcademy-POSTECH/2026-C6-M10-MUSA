using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.Networking.EditModeTests")]

namespace C6.Prototype.Networking.Counter
{
    [DisallowMultipleComponent, RequireComponent(typeof(DirectConnectionSession))]
    public sealed class CounterSync : MonoBehaviour
    {
        private const string RequestMessage = "C6.T03.Request.v1";
        private const string SnapshotRequestMessage = "C6.T03.SnapshotRequest.v1";
        private const string SnapshotMessage = "C6.T03.Snapshot.v1";
        private const string ReceiptMessage = "C6.T03.Receipt.v1";
        public const int MaximumUniqueRequestsPerParticipant = CounterAuthority.DefaultMaxTrackedRequests;
        private const float BootstrapRetrySeconds = 1f;
        private const int MaximumBootstrapAttempts = 8;

        private readonly HashSet<ulong> pending = new HashSet<ulong>();
        private readonly HashSet<ulong> acknowledged = new HashSet<ulong>();
        private readonly Dictionary<ulong, Guid> participantNonces = new Dictionary<ulong, Guid>();
        private DirectConnectionSession session;
        private NetworkManager boundManager;
        private CustomMessagingManager messaging;
        private CounterAuthority authority;
        private Guid bootstrapNonce;
        private ulong nextRequestId;
        private ulong lastRequestId;
        private float nextBootstrapAt;
        private int bootstrapAttempts;

        public bool IsReady { get; private set; }
        public CounterSnapshot Snapshot { get; private set; }
        // Counts unique local request IDs; explicit duplicate sends never increase it.
        public ulong SentCount { get; private set; }
        public ulong AcknowledgedCount { get; private set; }
        public ulong PendingCount => (ulong)pending.Count;
        public ulong DuplicateReceiptCount { get; private set; }
        public string Status { get; private set; } = "Connect to begin counter synchronization.";
        public CounterReceipt? LastReceipt { get; private set; }
        public event Action Changed;

        private void Awake() => session = GetComponent<DirectConnectionSession>();

        private void OnEnable()
        {
            if (session == null) session = GetComponent<DirectConnectionSession>();
            session.Changed += RefreshBinding;
            RefreshBinding();
        }

        private void Update()
        {
            RefreshBinding();
            if (messaging != null && !boundManager.IsServer && !IsReady
                && Time.unscaledTime >= nextBootstrapAt && bootstrapAttempts < MaximumBootstrapAttempts)
                RequestInitialSnapshot();
        }

        private void RefreshBinding()
        {
            var candidate = session.OwnedManager;
            var active = isActiveAndEnabled && session.State == DirectConnectionState.Connected
                && candidate != null && candidate.IsListening && !candidate.ShutdownInProgress;
            if (!active)
            {
                if (messaging != null || IsReady || nextRequestId != 0) ResetSession();
                return;
            }
            var candidateMessaging = candidate.CustomMessagingManager;
            if (candidateMessaging == null) return;
            if (candidate != boundManager || !ReferenceEquals(messaging, candidateMessaging))
            {
                ResetSession(false);
                boundManager = candidate;
                messaging = candidateMessaging;
                messaging.RegisterNamedMessageHandler(RequestMessage, ReceiveRequest);
                messaging.RegisterNamedMessageHandler(SnapshotRequestMessage, ReceiveSnapshotRequest);
                messaging.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
                messaging.RegisterNamedMessageHandler(ReceiptMessage, ReceiveReceipt);
                if (candidate.IsHost)
                {
                    authority = new CounterAuthority(Guid.NewGuid(), NetworkManager.ServerClientId);
                    Snapshot = authority.GetSnapshot();
                    IsReady = true;
                    Status = "Host authority ready.";
                    AddCurrentParticipants();
                    PublishChange("host-ready");
                }
                else
                {
                    bootstrapNonce = Guid.NewGuid();
                    Status = "Waiting for the host's full counter state...";
                    nextBootstrapAt = 0;
                    RequestInitialSnapshot();
                }
            }
            if (authority != null) AddCurrentParticipants();
        }

        private void AddCurrentParticipants()
        {
            foreach (var participant in session.ParticipantIds) authority.AddParticipant(participant);
        }

        private bool CanProcess => messaging != null && boundManager != null
            && boundManager.IsListening && !boundManager.ShutdownInProgress
            && session.State == DirectConnectionState.Connected;

        private bool IsParticipant(ulong sender)
        {
            foreach (var participant in session.ParticipantIds)
                if (participant == sender) return true;
            return false;
        }

        private void RequestInitialSnapshot()
        {
            if (!CanProcess || boundManager.IsServer || IsReady) return;
            ++bootstrapAttempts;
            nextBootstrapAt = Time.unscaledTime + BootstrapRetrySeconds;
            using (var writer = CounterWire.CreateWriter())
            {
                CounterWire.WriteNonce(writer, bootstrapNonce);
                if (!Send(SnapshotRequestMessage, NetworkManager.ServerClientId, writer))
                {
                    PublishChange("snapshot-send-failed");
                    return;
                }
            }
            Status = bootstrapAttempts == MaximumBootstrapAttempts
                ? "Waiting for full state. If this continues, end the connection and join again manually."
                : "Waiting for the host's full counter state...";
            PublishChange("snapshot-request");
        }

        public bool RequestIncrement()
        {
            if (!CanProcess || !IsReady || !session.LocalClientId.HasValue) return false;
            if (nextRequestId >= MaximumUniqueRequestsPerParticipant)
            {
                Status = "The development request limit was reached. Start a new session manually.";
                PublishChange("request-limit");
                return false;
            }
            lastRequestId = ++nextRequestId;
            ++SentCount;
            pending.Add(lastRequestId);
            Debug.Log($"C6_T03_REQUEST role={session.Role} session={Snapshot.SessionId} sender={session.LocalClientId.Value} request={lastRequestId} sent={SentCount}");
            SendIncrement(lastRequestId);
            return true;
        }

        // Explicit development duplicate; neither the request ID nor the unique send counter advances.
        public bool ResendLastRequest()
        {
            if (!CanProcess || !IsReady || lastRequestId == 0 || !session.LocalClientId.HasValue) return false;
            Debug.Log($"C6_T03_RESEND role={session.Role} session={Snapshot.SessionId} sender={session.LocalClientId.Value} request={lastRequestId}");
            SendIncrement(lastRequestId);
            return true;
        }

        private void SendIncrement(ulong requestId)
        {
            if (authority != null)
            {
                var receipt = authority.Apply(session.LocalClientId.Value, Snapshot.SessionId, requestId);
                RecordApplied(receipt);
                AcceptReceipt(receipt);
                PublishHostSnapshot();
            }
            else
            {
                using (var writer = CounterWire.CreateWriter())
                {
                    CounterWire.WriteRequest(writer, Snapshot.SessionId, requestId);
                    if (Send(RequestMessage, NetworkManager.ServerClientId, writer))
                        Status = "Request sent. Waiting for host approval.";
                }
                PublishChange("request-sent");
            }
        }

        private void ReceiveSnapshotRequest(ulong sender, FastBufferReader reader)
        {
            if (!CanProcess || authority == null || !IsParticipant(sender) || sender == NetworkManager.ServerClientId) return;
            if (!CounterWire.TryReadNonce(reader, out var nonce)) { Drop("snapshot-request-format"); return; }
            participantNonces[sender] = nonce;
            SendSnapshot(sender, nonce, authority.GetSnapshot());
        }

        private void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (!CanProcess || authority == null || !IsParticipant(sender) || sender == NetworkManager.ServerClientId) return;
            if (!CounterWire.TryReadRequest(reader, out var requestedSession, out var requestId)) { Drop("request-format"); return; }
            // Identity is the transport callback sender, never an identity claimed by the packet.
            var receipt = authority.Apply(sender, requestedSession, requestId);
            RecordApplied(receipt);
            // A stale request's rejection carries the authority's current snapshot. Do not
            // wrap that old request in this connection's fresh nonce: its reused ID could
            // otherwise acknowledge an unrelated pending request in the new session.
            if (!string.Equals(requestedSession, receipt.Snapshot.SessionId, StringComparison.Ordinal))
            {
                Drop("request-session");
                return;
            }
            if (participantNonces.TryGetValue(sender, out var nonce))
            {
                using (var writer = CounterWire.CreateWriter())
                {
                    CounterWire.WriteReceipt(writer, nonce, receipt);
                    Send(ReceiptMessage, sender, writer);
                }
            }
            PublishHostSnapshot();
        }

        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (!CanProcess || boundManager.IsServer || sender != NetworkManager.ServerClientId) return;
            if (!CounterWire.TryReadSnapshot(reader, out var nonce, out var snapshot)) { Drop("snapshot-format"); return; }
            if (nonce != bootstrapNonce) { Drop("snapshot-nonce"); return; }
            if (!AcceptSnapshot(snapshot)) return;
            IsReady = true;
            Status = "Counter state synchronized.";
            PublishChange("snapshot");
        }

        private void ReceiveReceipt(ulong sender, FastBufferReader reader)
        {
            if (!CanProcess || boundManager.IsServer || !IsReady || sender != NetworkManager.ServerClientId) return;
            if (!CounterWire.TryReadReceipt(reader, out var nonce, out var receipt)) { Drop("receipt-format"); return; }
            if (nonce != bootstrapNonce) { Drop("receipt-nonce"); return; }
            AcceptReceipt(receipt);
        }

        private void AcceptReceipt(CounterReceipt receipt)
        {
            if (!session.LocalClientId.HasValue || receipt.SenderId != session.LocalClientId.Value
                || !string.Equals(receipt.Snapshot.SessionId, Snapshot.SessionId, StringComparison.Ordinal)
                || (!pending.Contains(receipt.RequestId) && !acknowledged.Contains(receipt.RequestId)))
            {
                Drop("receipt-identity-session-or-request");
                return;
            }
            var first = pending.Remove(receipt.RequestId);
            if (first)
            {
                acknowledged.Add(receipt.RequestId);
                ++AcknowledgedCount;
            }
            if (!first || receipt.Duplicate) ++DuplicateReceiptCount;
            LastReceipt = receipt;
            // A replay returns the original receipt, whose snapshot can legitimately be older.
            AcceptSnapshot(receipt.Snapshot);
            Status = receipt.Approved ? "Host approved the request." : "Host rejected the request: " + receipt.Reason;
            PublishChange(receipt.Duplicate ? "duplicate-receipt" : "receipt");
        }

        private bool AcceptSnapshot(CounterSnapshot incoming)
        {
            if (IsReady && !string.Equals(incoming.SessionId, Snapshot.SessionId, StringComparison.Ordinal))
            {
                Drop("snapshot-session");
                return false;
            }
            if (IsReady && incoming.Revision < Snapshot.Revision) return false;
            if (IsReady && (incoming.Value < Snapshot.Value || incoming.ApprovedCount < Snapshot.ApprovedCount
                || incoming.RejectedCount < Snapshot.RejectedCount))
            {
                Drop("snapshot-regression");
                return false;
            }
            if (IsReady && incoming.Revision == Snapshot.Revision
                && (incoming.Value != Snapshot.Value || incoming.ApprovedCount != Snapshot.ApprovedCount
                    || incoming.RejectedCount != Snapshot.RejectedCount))
            {
                Drop("snapshot-conflict");
                return false;
            }
            Snapshot = incoming;
            return true;
        }

        private void PublishHostSnapshot()
        {
            Snapshot = authority.GetSnapshot();
            foreach (var participant in participantNonces)
                if (IsParticipant(participant.Key)) SendSnapshot(participant.Key, participant.Value, Snapshot);
            PublishChange("host-state");
        }

        private void SendSnapshot(ulong receiver, Guid nonce, CounterSnapshot snapshot)
        {
            using (var writer = CounterWire.CreateWriter())
            {
                CounterWire.WriteSnapshot(writer, nonce, snapshot);
                Send(SnapshotMessage, receiver, writer);
            }
        }

        private bool Send(string messageName, ulong receiver, FastBufferWriter writer)
        {
            try
            {
                messaging.SendNamedMessage(messageName, receiver, writer, NetworkDelivery.ReliableSequenced);
                return true;
            }
            catch (Exception exception)
            {
                Status = "Could not send counter data. End this connection and retry manually.";
                Debug.LogWarning($"C6_T03_SEND_FAILED type={exception.GetType().Name}");
                return false;
            }
        }

        private static void RecordApplied(CounterReceipt receipt)
        {
            Debug.Log($"C6_T03_APPLIED session={receipt.Snapshot.SessionId} sender={receipt.SenderId} request={receipt.RequestId} approved={receipt.Approved} duplicate={receipt.Duplicate} reason={receipt.Reason} value={receipt.Snapshot.Value} revision={receipt.Snapshot.Revision} approvals={receipt.Snapshot.ApprovedCount} rejections={receipt.Snapshot.RejectedCount}");
        }

        private void PublishChange(string stage)
        {
            Debug.Log($"C6_T03_STATE stage={stage} role={session.Role} session={Snapshot.SessionId ?? "none"} ready={IsReady} value={Snapshot.Value} revision={Snapshot.Revision} approvals={Snapshot.ApprovedCount} rejections={Snapshot.RejectedCount} sent={SentCount} acknowledged={AcknowledgedCount} pending={PendingCount} duplicates={DuplicateReceiptCount}");
            Changed?.Invoke();
        }

        private static void Drop(string reason) => Debug.LogWarning("C6_T03_DROPPED reason=" + reason);

        private void ResetSession(bool notify = true)
        {
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(RequestMessage);
                messaging.UnregisterNamedMessageHandler(SnapshotRequestMessage);
                messaging.UnregisterNamedMessageHandler(SnapshotMessage);
                messaging.UnregisterNamedMessageHandler(ReceiptMessage);
            }
            authority?.EndSession();
            authority = null;
            messaging = null;
            boundManager = null;
            bootstrapNonce = Guid.Empty;
            bootstrapAttempts = 0;
            nextBootstrapAt = 0;
            nextRequestId = lastRequestId = 0;
            SentCount = AcknowledgedCount = DuplicateReceiptCount = 0;
            pending.Clear();
            acknowledged.Clear();
            participantNonces.Clear();
            LastReceipt = null;
            Snapshot = default;
            IsReady = false;
            Status = "Connect to begin counter synchronization.";
            if (notify) Changed?.Invoke();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.Changed -= RefreshBinding;
                // A fresh counter authority must always coincide with a fresh connection.
                if (session.State == DirectConnectionState.Connected
                    || session.State == DirectConnectionState.Connecting
                    || session.State == DirectConnectionState.StartingHost) session.Stop();
            }
            ResetSession();
        }

        private void OnDestroy()
        {
            ResetSession();
            Changed = null;
        }
    }

    // Four fixed-purpose T03 packets; no object spawning or reusable message framework.
    internal static class CounterWire
    {
        private const byte Version = 1;
        private const int MaximumBytes = 192;
        private const int MaximumReasonBytes = 96;
        private const int SnapshotBodyBytes = 48;
        internal static FastBufferWriter CreateWriter() => new FastBufferWriter(MaximumBytes, Allocator.Temp);

        internal static void WriteNonce(FastBufferWriter writer, Guid nonce)
        {
            writer.WriteValueSafe(Version);
            WriteGuid(writer, nonce);
        }

        internal static bool TryReadNonce(FastBufferReader reader, out Guid nonce)
        {
            nonce = Guid.Empty;
            if (!ReadHeader(ref reader, 17, 17)) return false;
            nonce = ReadGuid(ref reader);
            return nonce != Guid.Empty;
        }

        internal static void WriteRequest(FastBufferWriter writer, string session, ulong requestId)
        {
            writer.WriteValueSafe(Version);
            WriteGuid(writer, Guid.Parse(session));
            writer.WriteValueSafe(requestId);
        }

        internal static bool TryReadRequest(FastBufferReader reader, out string session, out ulong requestId)
        {
            session = null;
            requestId = 0;
            if (!ReadHeader(ref reader, 25, 25)) return false;
            var id = ReadGuid(ref reader);
            reader.ReadValueSafe(out requestId);
            session = id.ToString("D");
            return true; // Empty/stale session and request zero are explicit authority rejections.
        }

        internal static void WriteSnapshot(FastBufferWriter writer, Guid nonce, CounterSnapshot snapshot)
        {
            WriteNonce(writer, nonce);
            WriteSnapshotBody(writer, snapshot);
        }

        internal static bool TryReadSnapshot(FastBufferReader reader, out Guid nonce, out CounterSnapshot snapshot)
        {
            nonce = Guid.Empty;
            snapshot = default;
            if (!ReadHeader(ref reader, 17 + SnapshotBodyBytes, 17 + SnapshotBodyBytes)) return false;
            nonce = ReadGuid(ref reader);
            return nonce != Guid.Empty && ReadSnapshotBody(ref reader, out snapshot);
        }

        internal static void WriteReceipt(FastBufferWriter writer, Guid nonce, CounterReceipt receipt)
        {
            WriteSnapshot(writer, nonce, receipt.Snapshot);
            writer.WriteValueSafe(receipt.SenderId);
            writer.WriteValueSafe(receipt.RequestId);
            writer.WriteValueSafe((byte)((receipt.Approved ? 1 : 0) | (receipt.Duplicate ? 2 : 0)));
            var reason = Encoding.ASCII.GetBytes(receipt.Reason ?? string.Empty);
            if (reason.Length > MaximumReasonBytes) throw new ArgumentException("T03 receipt reason is too long.");
            writer.WriteValueSafe((byte)reason.Length);
            writer.WriteBytesSafe(reason);
        }

        internal static bool TryReadReceipt(FastBufferReader reader, out Guid nonce, out CounterReceipt receipt)
        {
            nonce = Guid.Empty;
            receipt = default;
            const int fixedBytes = 17 + SnapshotBodyBytes + 18;
            if (!ReadHeader(ref reader, fixedBytes, fixedBytes + MaximumReasonBytes)) return false;
            nonce = ReadGuid(ref reader);
            if (nonce == Guid.Empty || !ReadSnapshotBody(ref reader, out var snapshot)) return false;
            reader.ReadValueSafe(out ulong sender);
            reader.ReadValueSafe(out ulong request);
            reader.ReadValueSafe(out byte flags);
            reader.ReadValueSafe(out byte reasonLength);
            if (flags > 3 || reasonLength > MaximumReasonBytes || reader.Length - reader.Position != reasonLength) return false;
            var reasonBytes = new byte[reasonLength];
            reader.ReadBytesSafe(ref reasonBytes, reasonLength);
            for (var index = 0; index < reasonBytes.Length; ++index)
                if (reasonBytes[index] < 32 || reasonBytes[index] > 126) return false;
            receipt = new CounterReceipt((flags & 1) != 0, (flags & 2) != 0,
                Encoding.ASCII.GetString(reasonBytes), sender, request, snapshot);
            return true;
        }

        private static bool ReadHeader(ref FastBufferReader reader, int minimum, int maximum)
        {
            var remaining = reader.Length - reader.Position;
            if (remaining < minimum || remaining > maximum || !reader.TryBeginRead(remaining)) return false;
            reader.ReadValueSafe(out byte version);
            return version == Version;
        }

        private static void WriteGuid(FastBufferWriter writer, Guid value) => writer.WriteBytesSafe(value.ToByteArray());

        private static Guid ReadGuid(ref FastBufferReader reader)
        {
            var bytes = new byte[16];
            reader.ReadBytesSafe(ref bytes, bytes.Length);
            return new Guid(bytes);
        }

        private static void WriteSnapshotBody(FastBufferWriter writer, CounterSnapshot snapshot)
        {
            WriteGuid(writer, Guid.Parse(snapshot.SessionId));
            writer.WriteValueSafe(snapshot.Value);
            writer.WriteValueSafe(snapshot.Revision);
            writer.WriteValueSafe(snapshot.ApprovedCount);
            writer.WriteValueSafe(snapshot.RejectedCount);
        }

        private static bool ReadSnapshotBody(ref FastBufferReader reader, out CounterSnapshot snapshot)
        {
            var session = ReadGuid(ref reader);
            reader.ReadValueSafe(out long value);
            reader.ReadValueSafe(out ulong revision);
            reader.ReadValueSafe(out ulong approvals);
            reader.ReadValueSafe(out ulong rejections);
            snapshot = default;
            if (session == Guid.Empty || value < 0 || (ulong)value != approvals
                || approvals > ulong.MaxValue - rejections || revision != approvals + rejections) return false;
            snapshot = new CounterSnapshot(session.ToString("D"), value, revision, approvals, rejections);
            return true;
        }
    }
}
