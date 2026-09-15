using System;
using System.Collections.Generic;

namespace C6.Prototype.Networking.Counter
{
    public readonly struct CounterSnapshot
    {
        public string SessionId { get; }
        public long Value { get; }
        public ulong Revision { get; }
        public ulong ApprovedCount { get; }
        public ulong RejectedCount { get; }

        public CounterSnapshot(string sessionId, long value, ulong revision,
            ulong approvedCount, ulong rejectedCount)
        {
            SessionId = sessionId;
            Value = value;
            Revision = revision;
            ApprovedCount = approvedCount;
            RejectedCount = rejectedCount;
        }
    }

    public readonly struct CounterRequest
    {
        public string SessionId { get; }
        public ulong RequestId { get; }

        public CounterRequest(string sessionId, ulong requestId)
        {
            SessionId = sessionId;
            RequestId = requestId;
        }
    }

    public readonly struct CounterReceipt
    {
        public bool Approved { get; }
        public bool Duplicate { get; }
        public string Reason { get; }
        public ulong SenderId { get; }
        public ulong RequestId { get; }
        public CounterSnapshot Snapshot { get; }

        public CounterReceipt(bool approved, bool duplicate, string reason,
            ulong senderId, ulong requestId, CounterSnapshot snapshot)
        {
            Approved = approved;
            Duplicate = duplicate;
            Reason = reason;
            SenderId = senderId;
            RequestId = requestId;
            Snapshot = snapshot;
        }

        internal CounterReceipt AsDuplicate() => new CounterReceipt(
            Approved, true, Reason, SenderId, RequestId, Snapshot);
    }

    /// <summary>
    /// One host-owned session. The transport supplies actual connected sender IDs;
    /// a request payload cannot choose its sender. Create a new authority for a new session.
    /// </summary>
    public sealed class CounterAuthority
    {
        // DEMO_ASSUMPTION: refuse further unique requests at this limit. Never evict
        // a receipt while the session is active, because eviction permits a replay.
        public const int DefaultMaxTrackedRequests = 10000;

        private readonly object sync = new object();
        private readonly ulong hostSenderId;
        private readonly int maxTrackedRequests;
        private readonly HashSet<ulong> participants = new HashSet<ulong>();
        private readonly Dictionary<RequestKey, CounterReceipt> receipts =
            new Dictionary<RequestKey, CounterReceipt>();
        private bool isActive;
        private CounterSnapshot snapshot;

        public CounterAuthority(Guid sessionId, ulong hostSenderId,
            int maxTrackedRequests = DefaultMaxTrackedRequests)
        {
            if (sessionId == Guid.Empty)
                throw new ArgumentException("A fresh nonempty session ID is required.", nameof(sessionId));
            if (maxTrackedRequests < 1)
                throw new ArgumentOutOfRangeException(nameof(maxTrackedRequests));

            this.hostSenderId = hostSenderId;
            this.maxTrackedRequests = maxTrackedRequests;
            snapshot = new CounterSnapshot(sessionId.ToString("D"), 0, 0, 0, 0);
            participants.Add(hostSenderId);
            isActive = true;
        }

        public bool IsActive
        {
            get { lock (sync) return isActive; }
        }

        public int TrackedRequestCount
        {
            get { lock (sync) return receipts.Count; }
        }

        public CounterSnapshot GetSnapshot()
        {
            lock (sync) return snapshot;
        }

        public bool AddParticipant(ulong senderId)
        {
            lock (sync)
            {
                if (!isActive) return false;
                if (participants.Contains(senderId)) return true;
                if (participants.Count >= 2) return false;
                return participants.Add(senderId);
            }
        }

        public bool RemoveParticipant(ulong senderId)
        {
            lock (sync)
            {
                if (!isActive || senderId == hostSenderId) return false;
                return participants.Remove(senderId);
            }
        }

        public CounterReceipt Apply(ulong senderId, string sessionId, ulong requestId)
        {
            lock (sync)
            {
                // Outsiders and old sessions never change the active session's state,
                // revision, counters, or deduplication ledger.
                if (!isActive) return RejectUntracked("session-ended", senderId, requestId);
                if (!participants.Contains(senderId)) return RejectUntracked("unknown-sender", senderId, requestId);
                if (!string.Equals(sessionId, snapshot.SessionId, StringComparison.Ordinal))
                    return RejectUntracked("stale-session", senderId, requestId);

                var key = new RequestKey(senderId, sessionId, requestId);
                if (receipts.TryGetValue(key, out var previous)) return previous.AsDuplicate();
                if (receipts.Count >= maxTrackedRequests)
                    return RejectUntracked("ledger-full", senderId, requestId);

                bool approved = requestId != 0;
                // Revision describes every authoritative snapshot change. A unique
                // active-member request with reserved ID 0 changes only RejectedCount.
                snapshot = new CounterSnapshot(snapshot.SessionId,
                    snapshot.Value + (approved ? 1 : 0), snapshot.Revision + 1,
                    snapshot.ApprovedCount + (approved ? 1UL : 0UL),
                    snapshot.RejectedCount + (approved ? 0UL : 1UL));
                var receipt = new CounterReceipt(approved, false,
                    approved ? "approved" : "invalid-request-id", senderId, requestId, snapshot);
                receipts.Add(key, receipt);
                return receipt;
            }
        }

        public void EndSession()
        {
            lock (sync)
            {
                isActive = false;
                participants.Clear();
                receipts.Clear();
                snapshot = new CounterSnapshot(string.Empty, 0, 0, 0, 0);
            }
        }

        private CounterReceipt RejectUntracked(string reason, ulong senderId, ulong requestId) =>
            new CounterReceipt(false, false, reason, senderId, requestId, snapshot);

        private readonly struct RequestKey : IEquatable<RequestKey>
        {
            private readonly ulong senderId;
            private readonly string sessionId;
            private readonly ulong requestId;

            public RequestKey(ulong senderId, string sessionId, ulong requestId)
            {
                this.senderId = senderId;
                this.sessionId = sessionId;
                this.requestId = requestId;
            }

            public bool Equals(RequestKey other) => senderId == other.senderId &&
                requestId == other.requestId && string.Equals(sessionId, other.sessionId, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is RequestKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = senderId.GetHashCode();
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(sessionId);
                    return (hash * 397) ^ requestId.GetHashCode();
                }
            }
        }
    }
}
