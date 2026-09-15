using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    /// <summary>
    /// Pure host-owned state. The calling service must authenticate sender and require NGO IsHost.
    /// T05 creates explicit development fixtures and reserves actions only; no gameplay transition occurs.
    /// </summary>
    public sealed class HostOrbRegistry
    {
        private sealed class Receipt
        {
            public readonly ulong Sender;
            public readonly OrbActionRequest Request;
            public readonly OrbReservationResult Result;
            public Receipt(ulong sender, OrbActionRequest request, OrbReservationResult result)
            {
                Sender = sender;
                Request = request;
                Result = result;
            }
        }

        private readonly Dictionary<string, OrbRecord> orbs = new Dictionary<string, OrbRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, OrbReservation> pending = new Dictionary<string, OrbReservation>(StringComparer.Ordinal);
        private readonly Dictionary<string, ulong> lastSequences = new Dictionary<string, ulong>(StringComparer.Ordinal);
        private readonly Dictionary<string, Receipt> receipts = new Dictionary<string, Receipt>(StringComparer.Ordinal);

        public bool DevelopmentTestMode { get; }
        public bool HasSession => !string.IsNullOrEmpty(SessionId);
        public string SessionId { get; private set; } = string.Empty;
        public uint RoundId { get; private set; }

        public HostOrbRegistry(bool developmentTestMode)
        {
            DevelopmentTestMode = developmentTestMode;
        }

        public void BeginSession(string sessionId, uint roundId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("A session ID is required.", nameof(sessionId));
            if (HasSession && string.Equals(SessionId, sessionId, StringComparison.Ordinal))
                throw new InvalidOperationException("An active session must advance through ResetRound, not reinitialize its locks.");
            ClearSession();
            SessionId = sessionId;
            RoundId = roundId;
        }

        public void ResetRound(uint newRoundId)
        {
            RequireSession();
            if (newRoundId <= RoundId) throw new ArgumentOutOfRangeException(nameof(newRoundId), "Round IDs must increase.");
            ClearRound();
            RoundId = newRoundId;
        }

        public void ClearSession()
        {
            ClearRound();
            SessionId = string.Empty;
            RoundId = 0;
        }

        public OrbRecord RegisterDevelopmentOrb(ulong owner, OrbKind kind, OrbPolarity polarity, Vector2 position)
        {
            RequireSession();
            if (!DevelopmentTestMode) throw new InvalidOperationException("Development fixtures require explicit test mode.");
            if (!IsValidKindAndPolarity(kind, polarity)) throw new ArgumentException("Raw requires Yin or Yang; Combined requires None.");
            if (!IsNormalized(position)) throw new ArgumentOutOfRangeException(nameof(position), "Position must be finite and within [0, 1].");
            string id;
            do { id = Guid.NewGuid().ToString("N"); } while (orbs.ContainsKey(id));
            var orb = new OrbRecord(id, kind, polarity, owner, OrbAuthorityState.Idle, position, EntrySide.None, 0);
            orbs.Add(id, orb);
            lastSequences.Add(id, 0);
            return orb;
        }

        /// <summary>
        /// Production Raw creation after the Host resource service approves sender, cost and capacity.
        /// No development fixture flag or client-provided identity is used on this path.
        /// </summary>
        public OrbRecord RegisterGeneratedRaw(string sessionId, uint roundId, ulong owner,
            OrbPolarity polarity, Vector2 position)
        {
            RequireSession();
            if (!string.Equals(SessionId, sessionId, StringComparison.Ordinal) || RoundId != roundId)
                throw new InvalidOperationException("Generated Raw must belong to the active Host round.");
            if (polarity != OrbPolarity.Yin && polarity != OrbPolarity.Yang)
                throw new ArgumentException("Generated Raw requires Yin or Yang.", nameof(polarity));
            if (!IsNormalized(position)) throw new ArgumentOutOfRangeException(nameof(position));
            string id;
            do { id = Guid.NewGuid().ToString("N"); } while (orbs.ContainsKey(id));
            var orb = new OrbRecord(id, OrbKind.Raw, polarity, owner, OrbAuthorityState.Idle,
                position, EntrySide.None, 0);
            orbs.Add(id, orb);
            lastSequences.Add(id, 0);
            return orb;
        }

        public IReadOnlyList<OrbRecord> Snapshot()
        {
            var snapshot = new List<OrbRecord>(orbs.Values);
            snapshot.Sort((left, right) => string.CompareOrdinal(left.OrbId, right.OrbId));
            return snapshot.AsReadOnly();
        }

        public bool TryGet(string orbId, out OrbRecord orb)
        {
            orb = null;
            return !string.IsNullOrEmpty(orbId) && orbs.TryGetValue(orbId, out orb);
        }

        public bool IsPending(string orbId)
        {
            return !string.IsNullOrEmpty(orbId) && pending.ContainsKey(orbId);
        }

        public bool TryGetPending(string orbId, out OrbReservation reservation)
        {
            reservation = null;
            return !string.IsNullOrEmpty(orbId) && pending.TryGetValue(orbId, out reservation);
        }

        public OrbReservationResult Reserve(ulong sender, OrbActionRequest request)
        {
            if (!HasSession) return Reject("NO_ACTIVE_SESSION");
            if (request == null) return Reject("MISSING_REQUEST");
            if (!string.Equals(SessionId, request.SessionId, StringComparison.Ordinal)) return Reject("SESSION_MISMATCH");
            if (RoundId != request.RoundId) return Reject("ROUND_MISMATCH");
            if (string.IsNullOrWhiteSpace(request.RequestId)) return Reject("MISSING_REQUEST_ID");

            if (receipts.TryGetValue(request.RequestId, out var existing))
            {
                if (existing.Sender == sender && existing.Request.HasSamePayload(request))
                    return existing.Result.AsDuplicate();
                return Reject("REQUEST_ID_CONFLICT");
            }

            var result = ValidateAndReserve(sender, request);
            receipts.Add(request.RequestId, new Receipt(sender, request, result));
            return result;
        }

        /// <summary>
        /// T06 host service confirms a validated launch reservation. T05 Reserve itself stays reservation-only.
        /// The exact pending object must belong to this registry; a stale or fabricated receipt cannot transition an orb.
        /// </summary>
        public bool TryBeginReservedLaunch(OrbReservation reservation, out OrbRecord launching)
        {
            launching = null;
            if (!HasSession || reservation == null) return false;
            var request = reservation.Request;
            if (request == null || request.Kind != OrbActionKind.Launch
                || !string.Equals(SessionId, request.SessionId, StringComparison.Ordinal)
                || RoundId != request.RoundId
                || !TryGet(request.OrbId, out var orb)
                || orb.Kind != OrbKind.Combined || orb.Polarity != OrbPolarity.None
                || orb.AuthorityState != OrbAuthorityState.Idle
                || orb.OwnerPlayerId != reservation.SenderPlayerId
                || !pending.TryGetValue(orb.OrbId, out var held)
                || !ReferenceEquals(held, reservation)) return false;

            launching = new OrbRecord(orb.OrbId, orb.Kind, orb.Polarity, orb.OwnerPlayerId,
                OrbAuthorityState.Launching, request.NormalizedPosition, orb.EntrySide, request.SequenceNumber, orb.TransferCount, orb.LastTransferSequence, orb.RightTransferCount, orb.TransferMotion);
            orbs[orb.OrbId] = launching;
            pending.Remove(orb.OrbId);
            return true;
        }

        /// <summary>
        /// T08 completes an exact dual reservation atomically. Both local Drop positions are finite
        /// normalized coordinates; the result is their midpoint. No events or resource changes occur here.
        /// </summary>
        public bool TryCompleteReservedCombination(OrbReservation reservation, Vector2 targetPosition,
            out OrbRecord sourceConsumed, out OrbRecord targetConsumed, out OrbRecord combined)
        {
            sourceConsumed = null; targetConsumed = null; combined = null;
            if (!HasSession || reservation == null || !IsNormalized(targetPosition)) return false;
            var request = reservation.Request;
            if (request == null || request.Kind != OrbActionKind.Combine
                || !string.Equals(SessionId, request.SessionId, StringComparison.Ordinal) || RoundId != request.RoundId
                || !IsNormalized(request.NormalizedPosition)
                || string.Equals(request.OrbId, request.OtherOrbId, StringComparison.Ordinal)
                || !TryGet(request.OrbId, out var source) || !TryGet(request.OtherOrbId, out var target)
                || source.OwnerPlayerId != reservation.SenderPlayerId || target.OwnerPlayerId != reservation.SenderPlayerId
                || source.Kind != OrbKind.Raw || target.Kind != OrbKind.Raw
                || !IsValidKindAndPolarity(source.Kind, source.Polarity) || !IsValidKindAndPolarity(target.Kind, target.Polarity)
                || source.Polarity == target.Polarity
                || source.AuthorityState != OrbAuthorityState.Idle || target.AuthorityState != OrbAuthorityState.Idle
                || reservation.ReservedOrbIds.Count != 2
                || !pending.TryGetValue(source.OrbId, out var sourceHeld) || !ReferenceEquals(sourceHeld, reservation)
                || !pending.TryGetValue(target.OrbId, out var targetHeld) || !ReferenceEquals(targetHeld, reservation)) return false;

            // Prepare every immutable record before touching either material. No failure guard follows
            // the first mutation, and no observer is invoked between the two consumed states and result.
            string id;
            do { id = Guid.NewGuid().ToString("N"); } while (orbs.ContainsKey(id));
            sourceConsumed = new OrbRecord(source.OrbId, source.Kind, source.Polarity, source.OwnerPlayerId,
                OrbAuthorityState.Consumed, request.NormalizedPosition, source.EntrySide, request.SequenceNumber, source.TransferCount, source.LastTransferSequence, source.RightTransferCount, source.TransferMotion);
            targetConsumed = new OrbRecord(target.OrbId, target.Kind, target.Polarity, target.OwnerPlayerId,
                OrbAuthorityState.Consumed, targetPosition, target.EntrySide, request.SequenceNumber, target.TransferCount, target.LastTransferSequence, target.RightTransferCount, target.TransferMotion);
            combined = new OrbRecord(id, OrbKind.Combined, OrbPolarity.None, reservation.SenderPlayerId,
                OrbAuthorityState.Idle, (request.NormalizedPosition + targetPosition) * .5f, EntrySide.None, 0);
            orbs.Add(id, combined); lastSequences.Add(id, 0);
            orbs[source.OrbId] = sourceConsumed; orbs[target.OrbId] = targetConsumed;
            pending.Remove(source.OrbId); pending.Remove(target.OrbId);
            return true;
        }

        /// <summary>Commits one exact transfer reservation with no callback or allocation after the ownership swap.</summary>
        public bool TryCompleteReservedTransfer(OrbReservation reservation, ulong receiver, int storageLimit,
            float edgeInset, out OrbRecord transferred, OrbTransferMotion? approvedMotion = null)
        {
            transferred = null;
            if (!HasSession || reservation == null || !TransferTuningValid(storageLimit, edgeInset)) return false;
            var request = reservation.Request;
            if (request == null || !IsTransfer(request.Kind) || request.SessionId != SessionId || request.RoundId != RoundId
                || !IsNormalized(request.NormalizedPosition) || !string.IsNullOrEmpty(request.OtherOrbId)
                || request.TransferMotion.HasValue != approvedMotion.HasValue
                || approvedMotion.HasValue && !OrbTransferMotion.IsValid(approvedMotion.Value, float.MaxValue)
                || receiver == reservation.SenderPlayerId || !TryGet(request.OrbId, out var orb)
                || orb.OwnerPlayerId != reservation.SenderPlayerId || orb.AuthorityState != OrbAuthorityState.Idle
                || !IsValidKindAndPolarity(orb.Kind, orb.Polarity) || orb.TransferCount == ulong.MaxValue || orb.RightTransferCount > orb.TransferCount
                || reservation.ReservedOrbIds.Count != 1 || !pending.TryGetValue(orb.OrbId, out var held)
                || !ReferenceEquals(held, reservation) || CountStoredOrbs(receiver) >= storageLimit) return false;
            var entry = request.Kind == OrbActionKind.TransferLeft ? EntrySide.Right : EntrySide.Left;
            var position = new Vector2(entry == EntrySide.Left ? edgeInset : 1f - edgeInset, request.NormalizedPosition.y);
            transferred = new OrbRecord(orb.OrbId, orb.Kind, orb.Polarity, receiver, OrbAuthorityState.Idle,
                position, entry, request.SequenceNumber, orb.TransferCount + 1, request.SequenceNumber,
                orb.RightTransferCount + (request.Kind == OrbActionKind.TransferRight ? 1UL : 0UL), approvedMotion);
            orbs[orb.OrbId] = transferred;
            pending.Remove(orb.OrbId);
            return true;
        }

        // Abort only an exact still-owned reservation when a guarded transfer commit failed.
        // The request receipt and its retired sequence remain, so it cannot be replayed as new.
        public bool ReleaseUncommittedTransfer(OrbReservation reservation)
        {
            if (reservation == null || reservation.Request == null || !IsTransfer(reservation.Request.Kind)
                || !HasSession || reservation.Request.SessionId != SessionId || reservation.Request.RoundId != RoundId
                || !pending.TryGetValue(reservation.Request.OrbId, out var held) || !ReferenceEquals(held, reservation)) return false;
            return pending.Remove(reservation.Request.OrbId);
        }
        public int CountStoredOrbs(ulong owner)
        {
            int count = 0;
            foreach (var orb in orbs.Values)
                if (orb.OwnerPlayerId == owner && (orb.AuthorityState == OrbAuthorityState.Idle || orb.AuthorityState == OrbAuthorityState.Launching)) count++;
            return count;
        }
        public static bool TransferTuningValid(int storageLimit, float edgeInset) => storageLimit >= 1 && storageLimit <= 20
            && !float.IsNaN(edgeInset) && !float.IsInfinity(edgeInset) && edgeInset > 0 && edgeInset < .5f;
        public static bool IsTransfer(OrbActionKind kind) => kind == OrbActionKind.TransferLeft || kind == OrbActionKind.TransferRight;

        /// <summary>Only the monotonic launch lifecycle can advance; identity and owner never change.</summary>
        public bool TryAdvanceLaunch(string sessionId, uint roundId, string orbId,
            OrbAuthorityState expectedState, OrbAuthorityState nextState, out OrbRecord advanced)
        {
            advanced = null;
            bool validStep = (expectedState == OrbAuthorityState.Launching
                    && (nextState == OrbAuthorityState.Projectile || nextState == OrbAuthorityState.Consumed))
                || (expectedState == OrbAuthorityState.Projectile && nextState == OrbAuthorityState.Consumed);
            if (!validStep || !HasSession || !string.Equals(SessionId, sessionId, StringComparison.Ordinal)
                || RoundId != roundId || !TryGet(orbId, out var orb)
                || orb.Kind != OrbKind.Combined || orb.Polarity != OrbPolarity.None
                || orb.AuthorityState != expectedState) return false;
            advanced = new OrbRecord(orb.OrbId, orb.Kind, orb.Polarity, orb.OwnerPlayerId,
                nextState, orb.NormalizedPosition, orb.EntrySide, orb.SequenceNumber, orb.TransferCount, orb.LastTransferSequence, orb.RightTransferCount, orb.TransferMotion);
            orbs[orb.OrbId] = advanced;
            return true;
        }

        private OrbReservationResult ValidateAndReserve(ulong sender, OrbActionRequest request)
        {
            if (!IsActionKind(request.Kind)) return Reject("INVALID_ACTION");
            if (request.TransferMotion.HasValue && !IsTransfer(request.Kind)) return Reject("UNEXPECTED_TRANSFER_MOTION");
            if (!IsNormalized(request.NormalizedPosition)) return Reject("INVALID_POSITION");
            if (!TryGet(request.OrbId, out var source)) return Reject("ORB_NOT_FOUND");
            if (source.OwnerPlayerId != sender) return Reject("OWNER_MISMATCH");
            if (!IsValidKindAndPolarity(source.Kind, source.Polarity)) return Reject("INVALID_ORB");
            if (source.AuthorityState != OrbAuthorityState.Idle) return Reject("ORB_NOT_IDLE");
            if (pending.ContainsKey(source.OrbId)) return Reject("ORB_PENDING");
            if (request.SequenceNumber <= lastSequences[source.OrbId]) return Reject("STALE_SEQUENCE");

            OrbRecord target = null;
            if (request.Kind == OrbActionKind.Launch)
            {
                if (source.Kind != OrbKind.Combined) return Reject("RAW_CANNOT_LAUNCH");
                if (!string.IsNullOrEmpty(request.OtherOrbId)) return Reject("UNEXPECTED_SECOND_ORB");
            }
            else if (request.Kind == OrbActionKind.Combine)
            {
                if (string.Equals(source.OrbId, request.OtherOrbId, StringComparison.Ordinal)) return Reject("SAME_ORB");
                if (!TryGet(request.OtherOrbId, out target)) return Reject("OTHER_ORB_NOT_FOUND");
                if (target.OwnerPlayerId != sender) return Reject("OTHER_OWNER_MISMATCH");
                if (source.Kind != OrbKind.Raw || target.Kind != OrbKind.Raw
                    || !IsValidKindAndPolarity(target.Kind, target.Polarity)
                    || source.Polarity == target.Polarity) return Reject("INVALID_COMBINATION");
                if (target.AuthorityState != OrbAuthorityState.Idle) return Reject("OTHER_ORB_NOT_IDLE");
                if (pending.ContainsKey(target.OrbId)) return Reject("OTHER_ORB_PENDING");
                if (request.SequenceNumber <= lastSequences[target.OrbId]) return Reject("OTHER_STALE_SEQUENCE");
            }
            else if (!string.IsNullOrEmpty(request.OtherOrbId)) return Reject("UNEXPECTED_SECOND_ORB");

            // All guards run before either lock changes. Confirmed records retain their identity,
            // ownership, position, and Idle state; the requested action exists only in this reservation.
            var ids = target == null ? new[] { source.OrbId } : new[] { source.OrbId, target.OrbId };
            var reservation = new OrbReservation(sender, request, ids);
            foreach (var id in ids)
            {
                pending.Add(id, reservation);
                lastSequences[id] = request.SequenceNumber;
            }
            return new OrbReservationResult(true, false, "RESERVED_ONLY", reservation);
        }

        private static OrbReservationResult Reject(string reason) => new OrbReservationResult(false, false, reason);

        private void ClearRound()
        {
            orbs.Clear();
            pending.Clear();
            lastSequences.Clear();
            receipts.Clear();
        }

        private void RequireSession()
        {
            if (!HasSession) throw new InvalidOperationException("Begin a host session first.");
        }

        private static bool IsActionKind(OrbActionKind kind)
        {
            return kind == OrbActionKind.Launch || kind == OrbActionKind.TransferLeft
                || kind == OrbActionKind.TransferRight || kind == OrbActionKind.Combine;
        }

        private static bool IsValidKindAndPolarity(OrbKind kind, OrbPolarity polarity)
        {
            return (kind == OrbKind.Raw && (polarity == OrbPolarity.Yin || polarity == OrbPolarity.Yang))
                || (kind == OrbKind.Combined && polarity == OrbPolarity.None);
        }

        private static bool IsNormalized(Vector2 position)
        {
            return !float.IsNaN(position.x) && !float.IsInfinity(position.x)
                && !float.IsNaN(position.y) && !float.IsInfinity(position.y)
                && position.x >= 0f && position.x <= 1f && position.y >= 0f && position.y <= 1f;
        }
    }
}
