using System;
using System.Collections.Generic;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.Combination
{
    /// <summary>
    /// Actual Host service authenticates the sender and battle state before entering this pure authority.
    /// Combination shares registry reservations with launch/transfer and never owns or edits resources.
    /// </summary>
    public sealed class HostCombinationAuthority
    {
        public const int MaximumRequestsPerPlayer = 256;
        private sealed class Receipt
        {
            public readonly ulong Sender;
            public readonly CombinationRequest Request;
            public readonly CombinationResult Result;
            public Receipt(ulong sender, CombinationRequest request, CombinationResult result)
            { Sender = sender; Request = request; Result = result; }
        }
        private readonly HostOrbRegistry registry;
        private readonly Dictionary<string, Receipt> receipts = new Dictionary<string, Receipt>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, int> requestCounts = new Dictionary<ulong, int>();
        private bool started;

        public string SessionId { get; private set; } = string.Empty;
        public uint RoundId { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsEnded { get; private set; }

        public HostCombinationAuthority(HostOrbRegistry registry)
        { this.registry = registry ?? throw new ArgumentNullException(nameof(registry)); }

        public void BeginRound()
        {
            if (!registry.HasSession) throw new InvalidOperationException("Begin a Host registry session first.");
            if (started && string.Equals(SessionId, registry.SessionId, StringComparison.Ordinal) && RoundId == registry.RoundId)
                throw new InvalidOperationException("Advance the registry round before restarting combination.");
            receipts.Clear(); requestCounts.Clear();
            SessionId = registry.SessionId; RoundId = registry.RoundId;
            started = true; IsPlaying = true; IsEnded = false;
        }

        public void SetPlaying(bool playing) => IsPlaying = started && !IsEnded && playing;
        public void EndRound() { IsEnded = true; IsPlaying = false; }

        public CombinationResult Combine(ulong authenticatedSender, CombinationRequest request)
        {
            if (request == null) return Reject("MISSING_REQUEST");
            string contextError = ContextError(request.SessionId, request.RoundId);
            if (contextError != null) return Reject(contextError);
            if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128)
                return Reject("INVALID_REQUEST_ID");
            if (receipts.TryGetValue(request.RequestId, out var previous))
                return previous.Sender == authenticatedSender && previous.Request.SamePayload(request)
                    ? previous.Result.AsDuplicate() : Reject("REQUEST_ID_CONFLICT");
            requestCounts.TryGetValue(authenticatedSender, out int count);
            if (count >= MaximumRequestsPerPlayer) return Reject("REQUEST_LIMIT");

            CombinationResult result;
            if (!IsPlaying || IsEnded) result = Reject("BATTLE_NOT_PLAYING");
            else if (!Normalized(request.SourcePosition) || !Normalized(request.TargetPosition)) result = Reject("INVALID_POSITION");
            else
            {
                var action = new OrbActionRequest(request.SessionId, request.RoundId, request.RequestId,
                    request.SourceOrbId, request.TargetOrbId, OrbActionKind.Combine, request.SequenceNumber, request.SourcePosition);
                var reservation = registry.Reserve(authenticatedSender, action);
                if (!reservation.Accepted) result = Reject(reservation.Reason);
                else if (!registry.TryCompleteReservedCombination(reservation.Reservation, request.TargetPosition,
                    out var source, out var target, out var combined)) result = Reject("COMBINATION_TRANSITION_REJECTED");
                else result = new CombinationResult(true, false, "COMBINATION_APPROVED", source, target, combined);
            }
            receipts.Add(request.RequestId, new Receipt(authenticatedSender, request, result));
            requestCounts[authenticatedSender] = count + 1;
            return result;
        }

        public CombinationRequestStatus Query(ulong authenticatedSender, string sessionId, uint roundId,
            string requestId, string sourceOrbId, string targetOrbId)
        {
            string contextError = ContextError(sessionId, roundId);
            if (contextError != null) return new CombinationRequestStatus(false, contextError);
            if (string.IsNullOrWhiteSpace(requestId)) return new CombinationRequestStatus(false, "INVALID_REQUEST_ID");
            if (receipts.TryGetValue(requestId, out var receipt))
            {
                if (receipt.Sender != authenticatedSender) return new CombinationRequestStatus(false, "REQUEST_OWNER_MISMATCH");
                if (!string.Equals(receipt.Request.SourceOrbId, sourceOrbId, StringComparison.Ordinal)
                    || !string.Equals(receipt.Request.TargetOrbId, targetOrbId, StringComparison.Ordinal))
                    return new CombinationRequestStatus(false, "REQUEST_ORB_MISMATCH");
                var source = Owned(authenticatedSender, sourceOrbId);
                var target = Owned(authenticatedSender, targetOrbId);
                var combined = Owned(authenticatedSender, receipt.Result.Combined?.OrbId);
                return new CombinationRequestStatus(true, "REQUEST_KNOWN", receipt.Result.AsDuplicate(),
                    source, target, combined, source != null && registry.IsPending(sourceOrbId), target != null && registry.IsPending(targetOrbId));
            }
            // Unknown does not authorize unlocking or recreating either material.
            var unknownSource = Owned(authenticatedSender, sourceOrbId);
            var unknownTarget = Owned(authenticatedSender, targetOrbId);
            return new CombinationRequestStatus(false, "REQUEST_UNKNOWN", null, unknownSource, unknownTarget, null,
                unknownSource != null && registry.IsPending(sourceOrbId), unknownTarget != null && registry.IsPending(targetOrbId));
        }

        private OrbRecord Owned(ulong sender, string orbId) => registry.TryGet(orbId, out var orb)
            && orb.OwnerPlayerId == sender ? orb : null;

        private string ContextError(string sessionId, uint roundId)
        {
            if (!registry.HasSession) return "NO_ACTIVE_SESSION";
            if (!string.Equals(registry.SessionId, sessionId, StringComparison.Ordinal)) return "SESSION_MISMATCH";
            if (registry.RoundId != roundId) return "ROUND_MISMATCH";
            if (!started || !string.Equals(SessionId, sessionId, StringComparison.Ordinal) || RoundId != roundId)
                return "ROUND_NOT_STARTED";
            return null;
        }

        private static bool Normalized(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && value.x >= 0 && value.x <= 1 && value.y >= 0 && value.y <= 1;
        private static CombinationResult Reject(string reason) => new CombinationResult(false, false, reason);
    }
}
