using System;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.Combination
{
    /// <summary>Local positions are supplied only on Drop, not replicated while dragging.</summary>
    public sealed class CombinationRequest
    {
        public string SessionId { get; }
        public uint RoundId { get; }
        public string RequestId { get; }
        public string SourceOrbId { get; }
        public string TargetOrbId { get; }
        public ulong SequenceNumber { get; }
        public Vector2 SourcePosition { get; }
        public Vector2 TargetPosition { get; }

        public CombinationRequest(string sessionId, uint roundId, string requestId, string sourceOrbId,
            string targetOrbId, ulong sequenceNumber, Vector2 sourcePosition, Vector2 targetPosition)
        {
            SessionId = sessionId; RoundId = roundId; RequestId = requestId;
            SourceOrbId = sourceOrbId; TargetOrbId = targetOrbId; SequenceNumber = sequenceNumber;
            SourcePosition = sourcePosition; TargetPosition = targetPosition;
        }

        internal bool SamePayload(CombinationRequest other) => other != null
            && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) && RoundId == other.RoundId
            && string.Equals(RequestId, other.RequestId, StringComparison.Ordinal)
            && string.Equals(SourceOrbId, other.SourceOrbId, StringComparison.Ordinal)
            && string.Equals(TargetOrbId, other.TargetOrbId, StringComparison.Ordinal)
            && SequenceNumber == other.SequenceNumber && SourcePosition.Equals(other.SourcePosition)
            && TargetPosition.Equals(other.TargetPosition);
    }

    public sealed class CombinationResult
    {
        public bool Accepted { get; }
        public bool IsDuplicate { get; }
        public string Reason { get; }
        public OrbRecord Source { get; }
        public OrbRecord Target { get; }
        public OrbRecord Combined { get; }
        internal CombinationResult(bool accepted, bool duplicate, string reason, OrbRecord source = null,
            OrbRecord target = null, OrbRecord combined = null)
        { Accepted = accepted; IsDuplicate = duplicate; Reason = reason; Source = source; Target = target; Combined = combined; }
        internal CombinationResult AsDuplicate() => new CombinationResult(Accepted, true, Reason, Source, Target, Combined);
    }

    /// <summary>Receipt reports the original decision; records report the current confirmed state.</summary>
    public sealed class CombinationRequestStatus
    {
        public bool Known { get; }
        public string Reason { get; }
        public CombinationResult Receipt { get; }
        public OrbRecord Source { get; }
        public OrbRecord Target { get; }
        public OrbRecord Combined { get; }
        public bool SourcePending { get; }
        public bool TargetPending { get; }
        internal CombinationRequestStatus(bool known, string reason, CombinationResult receipt = null,
            OrbRecord source = null, OrbRecord target = null, OrbRecord combined = null,
            bool sourcePending = false, bool targetPending = false)
        {
            Known = known; Reason = reason; Receipt = receipt; Source = source; Target = target;
            Combined = combined; SourcePending = sourcePending; TargetPending = targetPending;
        }
    }
}
