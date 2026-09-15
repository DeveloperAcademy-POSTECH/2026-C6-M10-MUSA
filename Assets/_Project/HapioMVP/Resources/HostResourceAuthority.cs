using System;
using System.Collections.Generic;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Networking;
using UnityEngine;

namespace C6.Prototype.Resources
{
    /// <summary>
    /// Pure state owned by the actual Host service. All times are trusted monotonic Host seconds;
    /// transport sender IDs are authenticated before these methods run. No client timer or polarity is accepted.
    /// </summary>
    public sealed class HostResourceAuthority
    {
        private sealed class Player
        {
            public readonly ulong Id;
            public double Stamina;
            public int Generated;
            public ulong LastSequence;
            public Player(ulong id, double start) { Id = id; Stamina = start; }
        }
        private sealed class Receipt
        {
            public readonly ulong Sender;
            public readonly GenerateRequest Request;
            public readonly GenerateResult Result;
            public Receipt(ulong sender, GenerateRequest request, GenerateResult result)
            { Sender = sender; Request = request; Result = result; }
        }

        private readonly HostOrbRegistry registry;
        private readonly Dictionary<ulong, Player> players = new Dictionary<ulong, Player>();
        private readonly Dictionary<string, Receipt> receipts = new Dictionary<string, Receipt>(StringComparer.Ordinal);
        private readonly HashSet<string> rewardedHits = new HashSet<string>(StringComparer.Ordinal);
        private double lastHostTime;
        private bool started;
        public int MaximumParticipants { get; }

        public ResourceTuning Tuning { get; }
        public uint Seed { get; }
        public string SessionId { get; private set; } = string.Empty;
        public uint RoundId { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsEnded { get; private set; }

        public HostResourceAuthority(HostOrbRegistry registry, ResourceTuning tuning, uint seed, int maximumParticipants = 2)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (maximumParticipants < 2 || maximumParticipants > ParticipantRing.MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(maximumParticipants));
            MaximumParticipants = maximumParticipants;
            // A default struct must not bypass the tuning constructor's validation.
            Tuning = new ResourceTuning(tuning.Max, tuning.Start, tuning.GenerateCost,
                tuning.RegenerationRate, tuning.HitRecovery, tuning.StorageLimit);
            Seed = seed;
        }

        public void BeginRound(IEnumerable<ulong> participantIds, double hostNow)
        {
            ValidateTime(hostNow, false);
            if (!registry.HasSession) throw new InvalidOperationException("Begin a Host registry session first.");
            if (started && string.Equals(SessionId, registry.SessionId, StringComparison.Ordinal) && RoundId == registry.RoundId)
                throw new InvalidOperationException("Advance the registry round before restarting resources.");
            if (registry.Snapshot().Count != 0)
                throw new InvalidOperationException("A normal resource round starts with no pre-generated orbs.");
            if (participantIds == null) throw new ArgumentNullException(nameof(participantIds));
            var ids = new HashSet<ulong>();
            foreach (var id in participantIds)
                if (!ids.Add(id) || ids.Count > MaximumParticipants) throw new ArgumentException("Distinct participants within the configured capacity are required.", nameof(participantIds));
            if (ids.Count == 0) throw new ArgumentException("At least the Host participant is required.", nameof(participantIds));

            players.Clear(); receipts.Clear(); rewardedHits.Clear();
            foreach (var id in ids) players.Add(id, new Player(id, Tuning.Start));
            SessionId = registry.SessionId; RoundId = registry.RoundId;
            lastHostTime = hostNow; started = true; IsPlaying = true; IsEnded = false;
        }

        public bool AddParticipant(ulong playerId, double hostNow)
        {
            RequireCurrentRound();
            if (IsEnded) return false;
            if (players.ContainsKey(playerId)) return false;
            if (players.Count >= MaximumParticipants) return false;
            Advance(hostNow, IsPlaying);
            players.Add(playerId, new Player(playerId, Tuning.Start));
            return true;
        }

        /// <summary>
        /// Settle elapsed time under the previous state, then enter the supplied state. A pause/resume
        /// therefore excludes the paused interval. Clamping each update discards recovery at maximum.
        /// </summary>
        public void Advance(double hostNow, bool playing)
        {
            ValidateTime(hostNow, started);
            if (!started) throw new InvalidOperationException("Begin a resource round first.");
            double elapsed = hostNow - lastHostTime;
            bool sameRound = ContextError(SessionId, RoundId) == null;
            if (!IsEnded && IsPlaying && sameRound && elapsed > 0)
                foreach (var player in players.Values)
                    player.Stamina = Math.Min(Tuning.Max, player.Stamina + elapsed * Tuning.RegenerationRate);
            lastHostTime = hostNow;
            IsPlaying = !IsEnded && sameRound && playing;
        }

        public void EndRound(double hostNow)
        {
            Advance(hostNow, false);
            IsEnded = true;
        }

        public GenerateResult Generate(ulong authenticatedSender, GenerateRequest request, double hostNow)
        {
            if (request == null) return Reject("MISSING_REQUEST");
            string contextError = ContextError(request.SessionId, request.RoundId);
            if (contextError != null) return Reject(contextError);
            if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128)
                return Reject("INVALID_REQUEST_ID");
            Advance(hostNow, IsPlaying);
            if (receipts.TryGetValue(request.RequestId, out var previous))
                return previous.Sender == authenticatedSender && previous.Request.SamePayload(request)
                    ? previous.Result.AsDuplicate() : Reject("REQUEST_ID_CONFLICT");
            if (!players.TryGetValue(authenticatedSender, out var player)) return Reject("UNKNOWN_PLAYER");

            GenerateResult result;
            if (IsEnded || !IsPlaying) result = Reject("BATTLE_NOT_PLAYING", player.Stamina);
            else if (request.SequenceNumber == 0 || request.SequenceNumber <= player.LastSequence)
                result = Reject("STALE_SEQUENCE", player.Stamina);
            else
            {
                // First-seen valid sequence IDs are retired even on rejection. Failed generation has no
                // resource/RNG mutation, but an older reordered command must not become payable later.
                player.LastSequence = request.SequenceNumber;
                double eligibilityEpsilon = Math.Max(1, Tuning.GenerateCost) * 0.0000001;
                if (player.Stamina + eligibilityEpsilon < Tuning.GenerateCost) result = Reject("INSUFFICIENT_STAMINA", player.Stamina);
                else if (CountStoredOrbs(authenticatedSender) >= Tuning.StorageLimit)
                    result = Reject("STORAGE_FULL", player.Stamina);
                else if (player.Generated == int.MaxValue) result = Reject("GENERATION_LIMIT", player.Stamina);
                else
                {
                    var polarity = PolarityFor(Seed, player.Id, (uint)player.Generated);
                    var position = FindSpawnPosition(player.Id);
                    double before = player.Stamina;
                    // Registry insertion is validated before cost and successful-generation counter change.
                    var orb = registry.RegisterGeneratedRaw(SessionId, RoundId, player.Id, polarity, position);
                    player.Stamina = Math.Max(0, player.Stamina - Tuning.GenerateCost);
                    player.Generated++;
                    result = new GenerateResult(true, false, "GENERATED_ONE_RAW", orb, before, player.Stamina);
                }
            }
            receipts.Add(request.RequestId, new Receipt(authenticatedSender, request, result));
            return result;
        }

        public GenerateRequestStatus Query(ulong authenticatedSender, string sessionId, uint roundId, string requestId)
        {
            string contextError = ContextError(sessionId, roundId);
            if (contextError != null) return new GenerateRequestStatus(false, contextError);
            if (string.IsNullOrWhiteSpace(requestId)) return new GenerateRequestStatus(false, "INVALID_REQUEST_ID");
            if (!receipts.TryGetValue(requestId, out var receipt)) return new GenerateRequestStatus(false, "REQUEST_UNKNOWN");
            if (receipt.Sender != authenticatedSender) return new GenerateRequestStatus(false, "REQUEST_OWNER_MISMATCH");
            return new GenerateRequestStatus(true, "REQUEST_KNOWN", receipt.Result.AsDuplicate());
        }

        /// <summary>
        /// An accepted final hit may arrive after the attack service publishes TargetCleared. It may
        /// still reward once before EndRound; paused ticking never fabricates a hit. The service only
        /// supplies its owned AttackAuthority's actual collision result, never a client message.
        /// </summary>
        public ResourceRecoveryResult ApplyValidHit(AttackHitResult hit, double hostNow)
        {
            if (hit == null || !hit.Applied) return RecoveryRejected("NOT_A_VALID_HIT");
            string contextError = ContextError(hit.SessionId, hit.RoundId);
            if (contextError != null) return RecoveryRejected(contextError);
            Advance(hostNow, IsPlaying);
            if (IsEnded) return RecoveryRejected("ROUND_ENDED");
            if (string.IsNullOrEmpty(hit.OrbId) || !registry.TryGet(hit.OrbId, out var orb)
                || orb.Kind != OrbKind.Combined || orb.AuthorityState != OrbAuthorityState.Consumed
                || orb.OwnerPlayerId != hit.AttackerPlayerId || hit.Damage <= 0
                || hit.HpBefore <= hit.HpAfter || hit.HpBefore - hit.HpAfter != hit.Damage)
                return RecoveryRejected("INVALID_HIT_RECORD");
            if (!players.TryGetValue(hit.AttackerPlayerId, out var player)) return RecoveryRejected("UNKNOWN_ATTACKER");
            if (!rewardedHits.Add(hit.OrbId))
                return new ResourceRecoveryResult(false, true, "HIT_ALREADY_REWARDED", player.Id, player.Stamina, player.Stamina);
            double before = player.Stamina;
            player.Stamina = Math.Min(Tuning.Max, before + Tuning.HitRecovery);
            return new ResourceRecoveryResult(true, false, "VALID_HIT_RECOVERY", player.Id, before, player.Stamina);
        }

        public ResourcePlayerState GetPlayer(ulong playerId) => players.TryGetValue(playerId, out var player)
            ? new ResourcePlayerState(player.Id, player.Stamina, player.Generated, player.LastSequence, Tuning.RegenerationRate) : null;

        public IReadOnlyList<ResourcePlayerState> Snapshot()
        {
            var snapshot = new List<ResourcePlayerState>();
            foreach (var player in players.Values) snapshot.Add(GetPlayer(player.Id));
            snapshot.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            return snapshot.AsReadOnly();
        }

        public int CountStoredOrbs(ulong owner)
        {
            int count = 0;
            foreach (var orb in registry.Snapshot())
                if (orb.OwnerPlayerId == owner && IsStored(orb)) count++;
            return count;
        }

        private Vector2 FindSpawnPosition(ulong owner)
        {
            int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Tuning.StorageLimit)));
            int rows = (Tuning.StorageLimit + columns - 1) / columns;
            var occupied = new HashSet<int>();
            foreach (var orb in registry.Snapshot())
            {
                if (orb.OwnerPlayerId != owner || !IsStored(orb)) continue;
                int column = Math.Min(columns - 1, (int)(orb.NormalizedPosition.x * columns));
                int row = Math.Min(rows - 1, (int)(orb.NormalizedPosition.y * rows));
                occupied.Add(row * columns + column);
            }
            for (int slot = 0; slot < Tuning.StorageLimit; slot++)
                if (!occupied.Contains(slot)) return new Vector2((slot % columns + .5f) / columns, (slot / columns + .5f) / rows);
            throw new InvalidOperationException("The validated storage capacity has no free spawn position.");
        }

        private static bool IsStored(OrbRecord orb) => orb.AuthorityState == OrbAuthorityState.Idle
            || orb.AuthorityState == OrbAuthorityState.Launching;

        // SplitMix64 counter mapping: each successful local generation advances its own counter.
        // One output bit selects Yin/Yang with equal bit partition; failures and the other player's
        // command ordering cannot change this player's sequence. This is not cryptographic randomness.
        private static OrbPolarity PolarityFor(uint seed, ulong playerId, uint successfulIndex)
        {
            unchecked
            {
                ulong value = ((ulong)seed << 32) ^ playerId;
                value += 0x9E3779B97F4A7C15UL * (successfulIndex + 1UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (value & 1UL) == 0 ? OrbPolarity.Yin : OrbPolarity.Yang;
            }
        }

        private string ContextError(string sessionId, uint roundId)
        {
            if (!registry.HasSession) return "NO_ACTIVE_SESSION";
            if (!started) return "ROUND_NOT_STARTED";
            if (!string.Equals(registry.SessionId, sessionId, StringComparison.Ordinal)
                || !string.Equals(SessionId, sessionId, StringComparison.Ordinal)) return "SESSION_MISMATCH";
            if (registry.RoundId != roundId || RoundId != roundId) return "ROUND_MISMATCH";
            return null;
        }

        private void RequireCurrentRound()
        {
            string error = ContextError(SessionId, RoundId);
            if (error != null) throw new InvalidOperationException(error);
        }

        private void ValidateTime(double hostNow, bool requireMonotonic)
        {
            if (!ResourceTuning.Finite(hostNow) || hostNow < 0 || (requireMonotonic && hostNow < lastHostTime))
                throw new ArgumentOutOfRangeException(nameof(hostNow), "Host time must be finite, non-negative, and monotonic.");
        }

        private static GenerateResult Reject(string reason, double stamina = 0)
            => new GenerateResult(false, false, reason, null, stamina, stamina);
        private static ResourceRecoveryResult RecoveryRejected(string reason)
            => new ResourceRecoveryResult(false, false, reason);
    }
}
