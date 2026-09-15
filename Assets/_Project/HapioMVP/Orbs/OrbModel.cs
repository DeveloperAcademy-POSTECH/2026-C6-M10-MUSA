using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    public enum OrbKind { Raw, Combined }
    public enum OrbPolarity { Yin, Yang, None }
    public enum OrbAuthorityState { Idle, Launching, Projectile, Consumed }
    public enum LocalOrbState { Idle, Dragging, Pending }
    public enum EntrySide { None, Left, Right }
    public enum OrbActionKind { Launch, TransferLeft, TransferRight, Combine }

    /// <summary>Confirmed host data. Dragging and pending belong to separate local/reservation state.</summary>
    public sealed class OrbRecord
    {
        public string OrbId { get; }
        public OrbKind Kind { get; }
        public OrbPolarity Polarity { get; }
        // Kind capabilities are independent of availability; authority state and pending still gate actions.
        public bool CanAttack => Kind == OrbKind.Combined;
        public bool CanCombine => Kind == OrbKind.Raw;
        public ulong OwnerPlayerId { get; }
        public OrbAuthorityState AuthorityState { get; }
        public Vector2 NormalizedPosition { get; }
        public EntrySide EntrySide { get; }
        public ulong SequenceNumber { get; }
        public ulong TransferCount { get; }
        public ulong LastTransferSequence { get; }
        public ulong RightTransferCount { get; }
        public OrbTransferMotion? TransferMotion { get; }

        public OrbRecord(string orbId, OrbKind kind, OrbPolarity polarity, ulong ownerPlayerId,
            OrbAuthorityState authorityState, Vector2 normalizedPosition, EntrySide entrySide,
            ulong sequenceNumber, ulong transferCount = 0, ulong lastTransferSequence = 0, ulong rightTransferCount = 0,
            OrbTransferMotion? transferMotion = null)
        {
            OrbId = orbId;
            Kind = kind;
            Polarity = polarity;
            OwnerPlayerId = ownerPlayerId;
            AuthorityState = authorityState;
            NormalizedPosition = normalizedPosition;
            EntrySide = entrySide;
            SequenceNumber = sequenceNumber;
            TransferCount = transferCount;
            LastTransferSequence = lastTransferSequence;
            RightTransferCount = rightTransferCount;
            TransferMotion = transferMotion;
        }
    }

    /// <summary>An immutable request, suitable for validation after receiving an authenticated sender ID.</summary>
    public sealed class OrbActionRequest
    {
        public string SessionId { get; }
        public uint RoundId { get; }
        public string RequestId { get; }
        public string OrbId { get; }
        public string OtherOrbId { get; }
        public OrbActionKind Kind { get; }
        public ulong SequenceNumber { get; }
        public Vector2 NormalizedPosition { get; }
        public OrbThrowInput? ThrowInput { get; }
        public OrbTransferMotion? TransferMotion { get; }

        public OrbActionRequest(string sessionId, uint roundId, string requestId, string orbId,
            string otherOrbId, OrbActionKind kind, ulong sequenceNumber, Vector2 normalizedPosition,
            OrbThrowInput? throwInput = null, OrbTransferMotion? transferMotion = null)
        {
            SessionId = sessionId;
            RoundId = roundId;
            RequestId = requestId;
            OrbId = orbId;
            OtherOrbId = otherOrbId ?? string.Empty;
            Kind = kind;
            SequenceNumber = sequenceNumber;
            NormalizedPosition = normalizedPosition;
            ThrowInput = throwInput;
            TransferMotion = transferMotion;
        }

        internal bool HasSamePayload(OrbActionRequest other)
        {
            return other != null && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                && RoundId == other.RoundId
                && string.Equals(RequestId, other.RequestId, StringComparison.Ordinal)
                && string.Equals(OrbId, other.OrbId, StringComparison.Ordinal)
                && string.Equals(OtherOrbId, other.OtherOrbId, StringComparison.Ordinal)
                && Kind == other.Kind && SequenceNumber == other.SequenceNumber
                && NormalizedPosition.Equals(other.NormalizedPosition)
                && ThrowInput.HasValue == other.ThrowInput.HasValue
                && (!ThrowInput.HasValue || ThrowInput.Value.Equals(other.ThrowInput.Value))
                && TransferMotion.HasValue == other.TransferMotion.HasValue
                && (!TransferMotion.HasValue || TransferMotion.Value.Equals(other.TransferMotion.Value));
        }
    }

    /// <summary>A lock on confirmed orb IDs, not approval to launch, transfer, or combine them.</summary>
    public sealed class OrbReservation
    {
        public ulong SenderPlayerId { get; }
        public OrbActionRequest Request { get; }
        public IReadOnlyList<string> ReservedOrbIds { get; }

        internal OrbReservation(ulong senderPlayerId, OrbActionRequest request, string[] orbIds)
        {
            SenderPlayerId = senderPlayerId;
            Request = request;
            ReservedOrbIds = Array.AsReadOnly((string[])orbIds.Clone());
        }
    }

    public sealed class OrbReservationResult
    {
        public bool Accepted { get; }
        public bool IsDuplicate { get; }
        public string Reason { get; }
        public OrbReservation Reservation { get; }

        internal OrbReservationResult(bool accepted, bool isDuplicate, string reason,
            OrbReservation reservation = null)
        {
            Accepted = accepted;
            IsDuplicate = isDuplicate;
            Reason = reason;
            Reservation = reservation;
        }

        internal OrbReservationResult AsDuplicate()
        {
            return new OrbReservationResult(Accepted, true, Reason, Reservation);
        }
    }
}
