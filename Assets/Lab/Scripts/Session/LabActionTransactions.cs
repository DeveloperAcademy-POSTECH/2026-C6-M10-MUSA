using System;
using System.Collections.Generic;

namespace C6Lab
{
    /// <summary>Identity of one logical action. Retransmission retains all four fields.</summary>
    public readonly struct LabActionIdentity : IEquatable<LabActionIdentity>
    {
        public readonly string sessionId;
        public readonly uint round;
        public readonly ulong sender;
        public readonly long requestId;

        public LabActionIdentity(string sessionId, uint round, ulong sender, long requestId)
        {
            this.sessionId = sessionId;
            this.round = round;
            this.sender = sender;
            this.requestId = requestId;
        }

        public bool Equals(LabActionIdentity other) => round == other.round && sender == other.sender &&
            requestId == other.requestId && string.Equals(sessionId, other.sessionId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LabActionIdentity other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = sessionId == null ? 0 : StringComparer.Ordinal.GetHashCode(sessionId);
                hash = hash * 397 ^ round.GetHashCode();
                hash = hash * 397 ^ sender.GetHashCode();
                return hash * 397 ^ requestId.GetHashCode();
            }
        }
    }

    /// <summary>Small authoritative action result, independent of large paint checkpoints.</summary>
    [Serializable]
    public sealed class LabActionReceipt
    {
        public string sessionId;
        public uint round;
        public ulong sender;
        public long requestId;
        public bool accepted;
        public string reason;
        public double serverTime;
        public ulong modelRevision;
        /// <summary>Host board-control stream sequence shared with board delta frames.</summary>
        public long sequence;
        public string[] removedIds = Array.Empty<string>();
        public LabOrbState[] states = Array.Empty<LabOrbState>();
        public LabPlayerState[] players = Array.Empty<LabPlayerState>();
        public string provisionalId;
        public string outputId;
        /// <summary>Accepted attempt consumed its inputs without producing a provisional output (Jigwi overheat).</summary>
        public bool consumedWithoutOutput;

        public LabActionIdentity Identity => new LabActionIdentity(sessionId, round, sender, requestId);

        public LabActionReceipt Copy()
        {
            var copy = (LabActionReceipt)MemberwiseClone();
            copy.removedIds = removedIds == null ? Array.Empty<string>() : (string[])removedIds.Clone();
            copy.states = new LabOrbState[states?.Length ?? 0];
            for (int i = 0; i < copy.states.Length; i++) copy.states[i] = states[i]?.Copy();
            copy.players = new LabPlayerState[players?.Length ?? 0];
            for (int i = 0; i < copy.players.Length; i++) copy.players[i] = players[i]?.Copy();
            return copy;
        }
    }

    public static class LabActionValidation
    {
        public static bool Identity(LabActionIdentity identity, string activeSession, uint activeRound,
            ulong actualSender, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(identity.sessionId) || identity.sessionId.Length > 128 ||
                identity.requestId <= 0)
                reason = "INVALID_ACTION_IDENTITY";
            else if (!string.Equals(identity.sessionId, activeSession, StringComparison.Ordinal) ||
                     identity.round != activeRound)
                reason = "STALE_ACTION_ROUND";
            else if (identity.sender != actualSender) reason = "ACTION_SENDER_MISMATCH";
            return reason == null;
        }

        /// <summary>Use a stable ownership revision, rather than a pose revision, for this check.</summary>
        public static bool Ownership(ulong currentOwner, ulong currentOwnershipRevision, ulong actualSender,
            ulong expectedOwnershipRevision, out string reason)
        {
            reason = currentOwner != actualSender ? "NOT_OWNER" :
                currentOwnershipRevision != expectedOwnershipRevision ? "STALE_OWNERSHIP" : null;
            return reason == null;
        }
    }

    public enum LabActionExecution { Executed, Replayed, Rejected }

    /// <summary>
    /// Host-only per-round exactly-once execution cache. Full rejects new actions instead of evicting
    /// results whose retransmission could otherwise execute a second time. Reset only at a round boundary.
    /// </summary>
    public sealed class LabActionResultLedger
    {
        private sealed class Entry
        {
            public string fingerprint;
            public LabActionReceipt receipt;
        }

        public const int DefaultCapacity = 4096;
        private readonly int capacity;
        private readonly Dictionary<LabActionIdentity, Entry> entries = new Dictionary<LabActionIdentity, Entry>();
        private string session;
        private uint round;
        public int Count => entries.Count;

        public LabActionResultLedger(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public void SetActive(string sessionId, uint activeRound)
        {
            if (session == sessionId && round == activeRound) return;
            session = sessionId;
            round = activeRound;
            entries.Clear();
        }

        public void Reset()
        {
            session = null;
            round = 0;
            entries.Clear();
        }

        public LabActionExecution Execute(LabActionIdentity identity, string payloadFingerprint,
            Func<LabActionReceipt> execute, out LabActionReceipt receipt, out string reason)
        {
            receipt = null;
            if (!LabActionValidation.Identity(identity, session, round, identity.sender, out reason))
                return LabActionExecution.Rejected;
            if (string.IsNullOrEmpty(payloadFingerprint) || execute == null)
            {
                reason = "INVALID_ACTION_PAYLOAD";
                return LabActionExecution.Rejected;
            }
            if (entries.TryGetValue(identity, out var previous))
            {
                if (!string.Equals(previous.fingerprint, payloadFingerprint, StringComparison.Ordinal))
                {
                    reason = "CONFLICTING_ACTION_REPLAY";
                    return LabActionExecution.Rejected;
                }
                if (previous.receipt == null)
                {
                    reason = "ACTION_IN_PROGRESS";
                    return LabActionExecution.Rejected;
                }
                receipt = previous.receipt.Copy();
                reason = receipt.reason;
                return LabActionExecution.Replayed;
            }
            if (entries.Count >= capacity)
            {
                reason = "ACTION_LEDGER_FULL";
                return LabActionExecution.Rejected;
            }

            var entry = new Entry { fingerprint = payloadFingerprint };
            entries.Add(identity, entry);
            try
            {
                var result = execute();
                if (result == null) throw new InvalidOperationException("Action execution must return a receipt.");
                result.sessionId = identity.sessionId;
                result.round = identity.round;
                result.sender = identity.sender;
                result.requestId = identity.requestId;
                entry.receipt = result.Copy();
            }
            catch
            {
                // A handler may already have changed the model before throwing. Never execute it
                // again on a retry; request authoritative recovery instead.
                entry.receipt = new LabActionReceipt
                {
                    sessionId = identity.sessionId, round = identity.round, sender = identity.sender,
                    requestId = identity.requestId, accepted = false, reason = "ACTION_RECOVERY_REQUIRED"
                };
                throw;
            }
            receipt = entry.receipt.Copy();
            reason = receipt.reason;
            return LabActionExecution.Executed;
        }

        public bool TryGet(LabActionIdentity identity, out LabActionReceipt receipt)
        {
            receipt = null;
            if (!LabActionValidation.Identity(identity, session, round, identity.sender, out _) ||
                !entries.TryGetValue(identity, out var entry) || entry.receipt == null) return false;
            receipt = entry.receipt.Copy();
            return true;
        }
    }

    public enum LabPendingActionState { WaitingForDependencies, Ready, AwaitingReceipt, RecoveryRequired, Accepted, Rejected }

    public readonly struct LabPendingActionInfo
    {
        public readonly long requestId;
        public readonly int attempts;
        public readonly LabPendingActionState state;
        public readonly string reason;
        public LabPendingActionInfo(long requestId, int attempts, LabPendingActionState state, string reason)
        { this.requestId = requestId; this.attempts = attempts; this.state = state; this.reason = reason; }
    }

    public sealed class LabLocalActionResolution
    {
        public long requestId;
        public bool accepted;
        public long[] rejectedDependents = Array.Empty<long>();
    }

    /// <summary>
    /// Local action bookkeeping only; never simulates physics or invents authoritative results. The
    /// caller retains the request payload and reuses it for retries. Exhausted retries require recovery,
    /// not an automatic rollback that might duplicate an action already committed by the Host.
    /// </summary>
    public sealed class LabLocalActionCoordinator
    {
        private sealed class Pending
        {
            public long id;
            public string[] inputs;
            public string provisional;
            public int attempts;
            public double due;
            public LabPendingActionState state;
            public string reason;
        }

        private readonly Dictionary<long, Pending> pending = new Dictionary<long, Pending>();
        private readonly Dictionary<string, long> producers = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly int maxAttempts;
        private readonly int capacity;
        private string session;
        private uint round;
        private ulong sender;
        public int Count => pending.Count;

        public LabLocalActionCoordinator(int maxAttempts = 6, int capacity = LabActionResultLedger.DefaultCapacity)
        {
            if (maxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.maxAttempts = maxAttempts;
            this.capacity = capacity;
        }

        public void SetActive(string sessionId, uint activeRound, ulong localSender)
        {
            if (session == sessionId && round == activeRound && sender == localSender) return;
            session = sessionId;
            round = activeRound;
            sender = localSender;
            pending.Clear();
            producers.Clear();
            mappings.Clear();
        }

        public void Reset() => SetActive(null, 0, 0);

        public bool TryRegister(long requestId, string[] inputIds, string provisionalOutputId, double now,
            out string reason)
        {
            reason = null;
            if (!LabActionValidation.Identity(new LabActionIdentity(session, round, sender, requestId),
                session, round, sender, out reason)) return false;
            if (!Finite(now)) reason = "INVALID_ACTION_TIME";
            else if (pending.ContainsKey(requestId)) reason = "DUPLICATE_LOCAL_ACTION";
            else if (pending.Count >= capacity) reason = "LOCAL_ACTION_LEDGER_FULL";
            else if (!string.IsNullOrEmpty(provisionalOutputId) && producers.ContainsKey(provisionalOutputId))
                reason = "DUPLICATE_PROVISIONAL_ID";
            if (reason != null) return false;
            var inputs = inputIds == null ? Array.Empty<string>() : (string[])inputIds.Clone();
            for (int i = 0; i < inputs.Length; i++)
            {
                if (string.IsNullOrEmpty(inputs[i]) || inputs[i] == provisionalOutputId)
                    reason = "INVALID_ACTION_INPUT";
                else if (producers.TryGetValue(inputs[i], out long parent) &&
                         (pending[parent].state == LabPendingActionState.Rejected
                          || pending[parent].state == LabPendingActionState.Accepted && !mappings.ContainsKey(inputs[i])))
                    reason = "DEPENDENCY_REJECTED";
                if (reason != null) return false;
            }
            var action = new Pending
            {
                id = requestId, inputs = inputs, provisional = provisionalOutputId, due = now,
                state = LabPendingActionState.Ready
            };
            pending.Add(requestId, action);
            if (!string.IsNullOrEmpty(provisionalOutputId)) producers.Add(provisionalOutputId, requestId);
            if (!InputsResolved(action, out _)) action.state = LabPendingActionState.WaitingForDependencies;
            return true;
        }

        public bool TryResolveInputs(long requestId, out string[] canonicalIds)
        {
            canonicalIds = null;
            return pending.TryGetValue(requestId, out var action) &&
                action.state != LabPendingActionState.Rejected && InputsResolved(action, out canonicalIds);
        }

        public bool TryGetMappedId(string provisionalId, out string outputId)
        {
            outputId = null;
            return provisionalId != null && mappings.TryGetValue(provisionalId, out outputId);
        }

        public long[] GetDueRequestIds(double now, bool connected)
        {
            if (!connected || !Finite(now)) return Array.Empty<long>();
            var due = new List<long>();
            foreach (var pair in pending)
            {
                var action = pair.Value;
                if (action.state == LabPendingActionState.Accepted || action.state == LabPendingActionState.Rejected ||
                    action.state == LabPendingActionState.RecoveryRequired || !InputsResolved(action, out _)) continue;
                if (now < action.due) continue;
                if (action.attempts >= maxAttempts)
                {
                    action.state = LabPendingActionState.RecoveryRequired;
                    action.reason = "ACTION_RECEIPT_TIMEOUT";
                    continue;
                }
                if (action.state == LabPendingActionState.WaitingForDependencies)
                    action.state = LabPendingActionState.Ready;
                due.Add(action.id);
            }
            due.Sort();
            return due.ToArray();
        }

        public bool MarkSent(long requestId, double now)
        {
            if (!Finite(now) || !pending.TryGetValue(requestId, out var action) ||
                action.state == LabPendingActionState.Accepted || action.state == LabPendingActionState.Rejected ||
                action.state == LabPendingActionState.RecoveryRequired || action.attempts >= maxAttempts ||
                !InputsResolved(action, out _)) return false;
            action.attempts++;
            action.state = LabPendingActionState.AwaitingReceipt;
            action.due = now + Math.Min(2d, .25d * Math.Pow(2d, action.attempts - 1));
            return true;
        }

        public bool ApplyReceipt(LabActionReceipt receipt, out LabLocalActionResolution resolution)
        {
            resolution = null;
            if (receipt == null || !LabActionValidation.Identity(receipt.Identity, session, round, sender, out _) ||
                !pending.TryGetValue(receipt.requestId, out var action) ||
                action.state == LabPendingActionState.Accepted || action.state == LabPendingActionState.Rejected)
                return false;
            // A response must match the real transport sender before the caller reaches this method.
            if (!string.IsNullOrEmpty(action.provisional) && receipt.accepted &&
                (receipt.provisionalId != action.provisional || (!receipt.consumedWithoutOutput && string.IsNullOrEmpty(receipt.outputId)))) return false;
            resolution = new LabLocalActionResolution { requestId = action.id, accepted = receipt.accepted };
            action.reason = receipt.reason;
            action.state = receipt.accepted ? LabPendingActionState.Accepted : LabPendingActionState.Rejected;
            if (receipt.accepted && !receipt.consumedWithoutOutput)
            {
                if (!string.IsNullOrEmpty(action.provisional)) mappings[action.provisional] = receipt.outputId;
            }
            else
            {
                var rejected = new List<long>();
                RejectDependents(action.id, rejected);
                resolution.rejectedDependents = rejected.ToArray();
            }
            return true;
        }

        public bool TryGetPending(long requestId, out LabPendingActionInfo info)
        {
            info = default;
            if (!pending.TryGetValue(requestId, out var action)) return false;
            info = new LabPendingActionInfo(action.id, action.attempts, action.state, action.reason);
            return true;
        }

        public long[] GetRecoveryRequiredRequestIds()
        {
            var result = new List<long>();
            foreach (var pair in pending)
                if (pair.Value.state == LabPendingActionState.RecoveryRequired) result.Add(pair.Key);
            result.Sort();
            return result.ToArray();
        }

        private bool InputsResolved(Pending action, out string[] ids)
        {
            ids = new string[action.inputs.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                string id = action.inputs[i];
                if (producers.ContainsKey(id))
                {
                    if (!mappings.TryGetValue(id, out ids[i])) { ids = null; return false; }
                }
                else ids[i] = id;
            }
            return true;
        }

        private void RejectDependents(long parentId, List<long> rejected)
        {
            var parent = pending[parentId];
            if (string.IsNullOrEmpty(parent.provisional)) return;
            foreach (var pair in pending)
            {
                var child = pair.Value;
                if (child.state == LabPendingActionState.Accepted || child.state == LabPendingActionState.Rejected ||
                    Array.IndexOf(child.inputs, parent.provisional) < 0) continue;
                child.state = LabPendingActionState.Rejected;
                child.reason = "DEPENDENCY_REJECTED";
                rejected.Add(child.id);
                RejectDependents(child.id, rejected);
            }
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
