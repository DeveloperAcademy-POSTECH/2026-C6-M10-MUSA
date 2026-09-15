using System;
using C6.Prototype.Orbs;

namespace C6.Prototype.Resources
{
    public readonly struct ResourceTuning
    {
        public double Max { get; }
        public double Start { get; }
        public double GenerateCost { get; }
        public double RegenerationRate { get; }
        public double HitRecovery { get; }
        public int StorageLimit { get; }

        public ResourceTuning(double max, double start, double cost, double regenRate, double hitBonus, int capacity)
        {
            if (!Finite(max) || max <= 0 || !Finite(start) || start < 0 || start > max
                || !Finite(cost) || cost <= 0 || cost > max || !Finite(regenRate) || regenRate < 0
                || !Finite(hitBonus) || hitBonus < 0 || capacity < 1 || capacity > 1000)
                throw new ArgumentOutOfRangeException(nameof(max), "Resource values must be finite and internally consistent.");
            Max = max; Start = start; GenerateCost = cost; RegenerationRate = regenRate;
            HitRecovery = hitBonus; StorageLimit = capacity;
        }

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class GenerateRequest
    {
        public string SessionId { get; }
        public uint RoundId { get; }
        public string RequestId { get; }
        public ulong SequenceNumber { get; }
        public GenerateRequest(string sessionId, uint roundId, string requestId, ulong sequenceNumber)
        {
            SessionId = sessionId; RoundId = roundId; RequestId = requestId; SequenceNumber = sequenceNumber;
        }
        internal bool SamePayload(GenerateRequest other) => other != null
            && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) && RoundId == other.RoundId
            && string.Equals(RequestId, other.RequestId, StringComparison.Ordinal)
            && SequenceNumber == other.SequenceNumber;
    }

    public sealed class ResourcePlayerState
    {
        public ulong PlayerId { get; }
        public double Stamina { get; }
        public int GeneratedTotal { get; }
        public ulong LastSequence { get; }
        public double RegenerationRate { get; }
        internal ResourcePlayerState(ulong id, double stamina, int generated, ulong sequence, double rate)
        { PlayerId = id; Stamina = stamina; GeneratedTotal = generated; LastSequence = sequence; RegenerationRate = rate; }
    }

    public sealed class GenerateResult
    {
        public bool Accepted { get; }
        public bool IsDuplicate { get; }
        public string Reason { get; }
        public OrbRecord Orb { get; }
        public double StaminaBefore { get; }
        public double StaminaAfter { get; }
        internal GenerateResult(bool accepted, bool duplicate, string reason, OrbRecord orb = null,
            double before = 0, double after = 0)
        { Accepted = accepted; IsDuplicate = duplicate; Reason = reason; Orb = orb; StaminaBefore = before; StaminaAfter = after; }
        internal GenerateResult AsDuplicate() => new GenerateResult(Accepted, true, Reason, Orb, StaminaBefore, StaminaAfter);
    }

    public sealed class GenerateRequestStatus
    {
        public bool Known { get; }
        public string Reason { get; }
        public GenerateResult Receipt { get; }
        internal GenerateRequestStatus(bool known, string reason, GenerateResult receipt = null)
        { Known = known; Reason = reason; Receipt = receipt; }
    }

    public sealed class ResourceRecoveryResult
    {
        public bool Accepted { get; }
        public bool Applied => Accepted && Added > 0;
        public bool IsDuplicate { get; }
        public string Reason { get; }
        public ulong PlayerId { get; }
        public double Added => StaminaAfter - StaminaBefore;
        public double StaminaBefore { get; }
        public double StaminaAfter { get; }
        internal ResourceRecoveryResult(bool accepted, bool duplicate, string reason, ulong playerId = 0,
            double before = 0, double after = 0)
        { Accepted = accepted; IsDuplicate = duplicate; Reason = reason; PlayerId = playerId; StaminaBefore = before; StaminaAfter = after; }
    }
}
