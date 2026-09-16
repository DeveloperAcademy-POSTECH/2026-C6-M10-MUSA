using System;
using System.Collections.Generic;
using C6.Prototype.Orbs;
using C6.Prototype.Networking;

namespace C6.Prototype.Attack
{
    // Explicit development-only lifecycle. This is not the later shared battle clock or victory system.
    public enum AttackBattleState { NotStarted, Playing, TargetCleared, Ended, NetworkError }

    public sealed class AttackLaunchResult
    {
        public bool Accepted { get; }
        public bool IsDuplicate { get; }
        public bool SpawnRequired => Accepted && !IsDuplicate && Kind == OrbActionKind.Launch;
        public OrbActionKind Kind { get; }
        public string Reason { get; }
        public OrbRecord Orb { get; }
        public BallisticLaunch? BallisticLaunch { get; }

        internal AttackLaunchResult(bool accepted, bool duplicate, string reason, OrbRecord orb = null, OrbActionKind kind = OrbActionKind.Launch,
            BallisticLaunch? ballisticLaunch = null)
        {
            Accepted = accepted;
            IsDuplicate = duplicate;
            Reason = reason;
            Orb = orb;
            Kind = kind;
            BallisticLaunch = ballisticLaunch;
        }

        internal AttackLaunchResult AsDuplicate() => new AttackLaunchResult(Accepted, true, Reason, Orb, Kind, BallisticLaunch);
    }

    public sealed class AttackHitResult
    {
        public bool Applied { get; }
        public string Reason { get; }
        public string SessionId { get; }
        public uint RoundId { get; }
        public string OrbId { get; }
        public ulong AttackerPlayerId { get; }
        public int Damage { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }

        internal AttackHitResult(bool applied, string reason, string sessionId, uint roundId,
            string orbId, ulong attacker, int damage, int hpBefore, int hpAfter)
        {
            Applied = applied;
            Reason = reason;
            SessionId = sessionId;
            RoundId = roundId;
            OrbId = orbId;
            AttackerPlayerId = attacker;
            Damage = damage;
            HpBefore = hpBefore;
            HpAfter = hpAfter;
        }
    }

    public sealed class AttackRequestStatus
    {
        public bool Known { get; }
        public string Reason { get; }
        public AttackLaunchResult Receipt { get; }
        public OrbRecord ConfirmedOrb { get; }
        public bool IsPending { get; }

        internal AttackRequestStatus(bool known, string reason, AttackLaunchResult receipt = null,
            OrbRecord confirmedOrb = null, bool isPending = false)
        {
            Known = known;
            Reason = reason;
            Receipt = receipt;
            ConfirmedOrb = confirmedOrb;
            IsPending = isPending;
        }
    }

    /// <summary>
    /// Pure state owned only by the actual NGO host service. Authenticated sender IDs come from that
    /// service, and ProcessHostHit is called only by its owned Rigidbody/Collider callback, never a client RPC.
    /// It contains no timer-based or immediate-hit shortcut and no stamina/resource implementation.
    /// </summary>
    public sealed class AttackAuthority
    {
        private sealed class Receipt
        {
            public readonly ulong Sender;
            public readonly OrbActionRequest Request;
            public readonly AttackLaunchResult Result;

            public Receipt(ulong sender, OrbActionRequest request, AttackLaunchResult result)
            {
                Sender = sender;
                Request = request;
                Result = result;
            }
        }

        private readonly HostOrbRegistry registry;
        private readonly Dictionary<string, Receipt> receipts = new Dictionary<string, Receipt>(StringComparer.Ordinal);
        private readonly HashSet<string> launched = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> processedHits = new HashSet<string>(StringComparer.Ordinal);
        private string boundSession = string.Empty;
        private uint boundRound;
        private ProjectileLaunchBasis throwBasis;
        private ThrowTuning throwTuning;
        private ulong[] orderedThrowParticipantIds = Array.Empty<ulong>();
        private int maximumFlyingPerPlayer;
        private float transferDeceleration, transferStopSpeed, maximumTransferSpeed;
        public bool ContinuousTransfersEnabled { get; private set; }
        public void ConfigureContinuousTransfers(float deceleration, float stopSpeed, float maximumSpeed)
        {
            if (boundRound != 0 || State != AttackBattleState.NotStarted)
                throw new InvalidOperationException("Configure continuous transfer motion before the first round.");
            if (!Finite(deceleration) || deceleration < 0 || !Finite(stopSpeed) || stopSpeed < 0
                || !Finite(maximumSpeed) || maximumSpeed <= 0 || stopSpeed > maximumSpeed)
                throw new ArgumentOutOfRangeException(nameof(deceleration));
            transferDeceleration = deceleration; transferStopSpeed = stopSpeed; maximumTransferSpeed = maximumSpeed;
            ContinuousTransfersEnabled = true;
        }
        public void ConfigureFlightCapacity(int maximumPerPlayer)
        {
            if (boundRound != 0 || State != AttackBattleState.NotStarted)
                throw new InvalidOperationException("Configure flight capacity before the first round.");
            if (maximumPerPlayer < 1 || maximumPerPlayer > ParticipantRing.MaximumFlyingPerPlayer) throw new ArgumentOutOfRangeException(nameof(maximumPerPlayer));
            maximumFlyingPerPlayer = maximumPerPlayer;
        }
        private bool FlightCapacityReached(ulong owner)
        {
            if (maximumFlyingPerPlayer == 0) return false;
            int count = 0;
            foreach (var orb in registry.Snapshot())
                if (orb.OwnerPlayerId == owner && (orb.AuthorityState == OrbAuthorityState.Launching
                    || orb.AuthorityState == OrbAuthorityState.Projectile)) count++;
            return count >= maximumFlyingPerPlayer;
        }
        public bool ReleaseThrowsEnabled { get; private set; }

        public void ConfigureReleaseThrows(ProjectileLaunchBasis basis, ThrowTuning tuning)
        {
            if (boundRound != 0 || State != AttackBattleState.NotStarted)
                throw new InvalidOperationException("Configure release throws before the first round starts.");
            if (!tuning.IsValid) throw new ArgumentException("Initialized throw tuning required.", nameof(tuning));
            // A valid representative swipe checks the complete basis without creating game state.
            var sample = new OrbThrowInput(UnityEngine.Vector2.up * tuning.MaxInputSpeed * tuning.MinDuration, tuning.MinDuration);
            if (!ThrowMapping.TryCalculate(UnityEngine.Vector2.one * .5f, sample, basis, tuning, out _, out var error))
                throw new ArgumentException("Invalid release throw setup: " + error);
            throwBasis = basis; throwTuning = tuning; ReleaseThrowsEnabled = true;
        }
        public void ConfigureParticipantThrowFrames(ulong[] orderedParticipantIds)
        {
            if (boundRound != 0 || State != AttackBattleState.NotStarted)
                throw new InvalidOperationException(
                    "Configure participant throw frames before the first round starts.");

            if (!ReleaseThrowsEnabled)
                throw new InvalidOperationException(
                    "Configure release throws before participant throw frames.");

            if (!ParticipantRing.Validate(orderedParticipantIds))
                throw new ArgumentException(
                    "Two to five ordered participants are required.",
                    nameof(orderedParticipantIds));

            orderedThrowParticipantIds = (ulong[])orderedParticipantIds.Clone();
        }

        public AttackBattleState State { get; private set; } = AttackBattleState.NotStarted;
        public int MonsterMaxHp { get; }
        public int BaseDamage { get; }
        public int MonsterHp { get; private set; }
        public int ValidHitCount { get; private set; }
        public string SessionId => boundSession;
        public uint RoundId => boundRound;

        /// <summary>Once-per-projectile future T07 hook, after processed/Consumed/HP are committed.</summary>
        public event Action<AttackHitResult> ValidHit;

        public AttackAuthority(HostOrbRegistry registry, int monsterMaxHp, int baseDamage)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (monsterMaxHp <= 0) throw new ArgumentOutOfRangeException(nameof(monsterMaxHp));
            if (baseDamage <= 0) throw new ArgumentOutOfRangeException(nameof(baseDamage));
            MonsterMaxHp = monsterMaxHp;
            BaseDamage = baseDamage;
            MonsterHp = monsterMaxHp;
        }

        public void BeginDevelopmentRound()
        {
            if (!registry.DevelopmentTestMode || !registry.HasSession)
                throw new InvalidOperationException("An active, explicit development host session is required.");
            if (string.Equals(boundSession, registry.SessionId, StringComparison.Ordinal) && boundRound == registry.RoundId)
                throw new InvalidOperationException("Reset the registry to a new round before starting another target trial.");

            boundSession = registry.SessionId;
            boundRound = registry.RoundId;
            receipts.Clear();
            launched.Clear();
            processedHits.Clear();
            ValidHitCount = 0;
            MonsterHp = MonsterMaxHp;
            State = AttackBattleState.Playing;
        }

        public AttackLaunchResult RequestLaunch(ulong authenticatedSender, OrbActionRequest request, bool gameplayEnabled = true)
        {
            if (request == null) return Reject("MISSING_REQUEST");
            string contextError = ContextError(request.SessionId, request.RoundId);
            if (contextError != null) return Reject(contextError);
            if (string.IsNullOrWhiteSpace(request.RequestId)) return Reject("MISSING_REQUEST_ID");
            if (receipts.TryGetValue(request.RequestId, out var previous))
                return previous.Sender == authenticatedSender && SamePayload(previous.Request, request)
                    ? previous.Result.AsDuplicate() : Reject("REQUEST_ID_CONFLICT");

            AttackLaunchResult result;
            BallisticLaunch? calculated = null;
            string throwError = null;

            if (request.Kind == OrbActionKind.Launch && ReleaseThrowsEnabled && request.ThrowInput.HasValue)
            {
                ProjectileLaunchBasis participantBasis = throwBasis;

                if (orderedThrowParticipantIds.Length > 0)
                {
                    int participantIndex = Array.IndexOf(
                        orderedThrowParticipantIds,
                        authenticatedSender);

                    if (participantIndex < 0)
                        throwError = "THROW_PARTICIPANT_NOT_FOUND";
                    else
                    {
                        participantBasis = ParticipantLaunchFrame.Calculate(
                            throwBasis,
                            participantIndex + 1,
                            orderedThrowParticipantIds.Length);
                    }
                }

                if (throwError == null)
                {
                    if (ThrowMapping.TryCalculate(
                        request.NormalizedPosition,
                        request.ThrowInput.Value,
                        participantBasis,
                        throwTuning,
                        out var launch,
                        out var error)) calculated = launch;
                    else
                        throwError = error.ToUpperInvariant().Replace('-', '_');
                }
            }

            if (!gameplayEnabled || State != AttackBattleState.Playing) result = Reject("BATTLE_NOT_PLAYING");
            else if (request.Kind != OrbActionKind.Launch) result = Reject("NOT_A_LAUNCH");
            else if (request.TransferMotion.HasValue) result = Reject("UNEXPECTED_TRANSFER_MOTION");
            else if (ReleaseThrowsEnabled && !request.ThrowInput.HasValue) result = Reject("THROW_INPUT_REQUIRED");
            else if (!ReleaseThrowsEnabled && request.ThrowInput.HasValue) result = Reject("THROW_MODE_DISABLED");
            else if (throwError != null) result = Reject(throwError);
            else if (FlightCapacityReached(authenticatedSender)) result = Reject("FLIGHT_CAPACITY_FULL");
            else
            {
                // Reuses all T05 sender, ownership, ID, sequence, position and lock validation.
                var reservation = registry.Reserve(authenticatedSender, request);
                if (!reservation.Accepted) result = Reject(reservation.Reason);
                else if (!registry.TryBeginReservedLaunch(reservation.Reservation, out var orb))
                    result = Reject("LAUNCH_TRANSITION_REJECTED");
                else
                {
                    launched.Add(orb.OrbId);
                    result = new AttackLaunchResult(true, false, "LAUNCH_APPROVED", orb, OrbActionKind.Launch, calculated);
                }
            }
            receipts.Add(request.RequestId, new Receipt(authenticatedSender, request, result));
            return result;
        }

        public AttackLaunchResult RequestTransfer(ulong authenticatedSender, OrbActionRequest request,
            ulong receiver, bool receiverConnected, int storageLimit, float edgeInset, bool gameplayEnabled = true,
            double motionServerTime = 0d)
        {
            if (request == null) return Reject("MISSING_REQUEST");
            string contextError = ContextError(request.SessionId, request.RoundId);
            if (contextError != null) return Reject(contextError);
            if (string.IsNullOrWhiteSpace(request.RequestId)) return Reject("MISSING_REQUEST_ID");
            if (receipts.TryGetValue(request.RequestId, out var previous))
                return previous.Sender == authenticatedSender && SamePayload(previous.Request, request)
                    ? previous.Result.AsDuplicate() : Reject("REQUEST_ID_CONFLICT");
            AttackLaunchResult result;
            string motionError = ValidateTransferMotion(request, edgeInset, motionServerTime);
            if (!gameplayEnabled || State != AttackBattleState.Playing) result = Reject("BATTLE_NOT_PLAYING");
            else if (!HostOrbRegistry.IsTransfer(request.Kind)) result = Reject("NOT_A_TRANSFER");
            else if (request.ThrowInput.HasValue) result = Reject("UNEXPECTED_THROW_INPUT");
            else if (!HostOrbRegistry.TransferTuningValid(storageLimit, edgeInset)) result = Reject("INVALID_TRANSFER_TUNING");
            else if (motionError != null) result = Reject(motionError);
            else if (!receiverConnected || receiver == authenticatedSender) result = Reject("RECEIVER_NOT_CONNECTED");
            else if (registry.TryGet(request.OrbId, out var before) && before.TransferCount == ulong.MaxValue)
                result = Reject("TRANSFER_COUNT_EXHAUSTED");
            else if (registry.CountStoredOrbs(receiver) >= storageLimit) result = Reject("RECEIVER_STORAGE_FULL");
            else
            {
                var reservation = registry.Reserve(authenticatedSender, request);
                if (!reservation.Accepted) result = Reject(reservation.Reason);
                else if (!registry.TryCompleteReservedTransfer(reservation.Reservation, receiver, storageLimit, edgeInset, out var moved,
                    request.TransferMotion.HasValue ? OrbTransferMotion.Decay(request.TransferMotion.Value, motionServerTime,
                        transferDeceleration, transferStopSpeed) : (OrbTransferMotion?)null))
                {
                    registry.ReleaseUncommittedTransfer(reservation.Reservation);
                    result = Reject("TRANSFER_TRANSITION_REJECTED");
                }
                else result = new AttackLaunchResult(true, false, "TRANSFER_APPROVED", moved, request.Kind);
            }
            receipts.Add(request.RequestId, new Receipt(authenticatedSender, request, result));
            return result;
        }

        private string ValidateTransferMotion(OrbActionRequest request, float edgeInset, double now)
        {
            if (!ContinuousTransfersEnabled) return request.TransferMotion.HasValue ? "TRANSFER_MOTION_DISABLED" : null;
            if (!request.TransferMotion.HasValue) return "TRANSFER_MOTION_REQUIRED";
            var motion = request.TransferMotion.Value;
            if (!OrbTransferMotion.IsValid(motion, maximumTransferSpeed)) return "INVALID_TRANSFER_MOTION";
            if (!OrbTransferMotion.IsFresh(motion, now)) return "TRANSFER_MOTION_TIME";
            bool right = request.Kind == OrbActionKind.TransferRight;
            if (right ? motion.Velocity.x <= 0f : motion.Velocity.x >= 0f) return "TRANSFER_MOTION_DIRECTION";
            // The source center reaches the inset at the physical edge. Input tolerance is geometric,
            // not a launch speed boost, and the target uses the same opposite-side center inset.
            if (right ? request.NormalizedPosition.x < 1f - edgeInset - .0001f
                : request.NormalizedPosition.x > edgeInset + .0001f) return "TRANSFER_MOTION_EDGE";
            return null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public bool MarkProjectileSpawned(string sessionId, uint roundId, string orbId)
        {
            return ContextError(sessionId, roundId) == null && State == AttackBattleState.Playing
                && !string.IsNullOrEmpty(orbId) && launched.Contains(orbId)
                && registry.TryAdvanceLaunch(sessionId, roundId, orbId,
                    OrbAuthorityState.Launching, OrbAuthorityState.Projectile, out _);
        }

        public AttackHitResult ProcessHostHit(string sessionId, uint roundId, string orbId)
        {
            string contextError = ContextError(sessionId, roundId);
            if (contextError != null) return HitRejected(contextError, sessionId, roundId, orbId);
            if (State != AttackBattleState.Playing) return HitRejected("BATTLE_NOT_PLAYING", sessionId, roundId, orbId);
            if (string.IsNullOrEmpty(orbId) || !launched.Contains(orbId))
                return HitRejected("UNKNOWN_PROJECTILE", sessionId, roundId, orbId);
            if (processedHits.Contains(orbId)) return HitRejected("HIT_ALREADY_PROCESSED", sessionId, roundId, orbId);
            if (!registry.TryGet(orbId, out var orb) || orb.AuthorityState != OrbAuthorityState.Projectile)
                return HitRejected("ORB_NOT_PROJECTILE", sessionId, roundId, orbId);

            // No observer can receive a hit or HP change before the once-only guard and final state.
            if (!registry.TryAdvanceLaunch(sessionId, roundId, orbId,
                    OrbAuthorityState.Projectile, OrbAuthorityState.Consumed, out _))
                return HitRejected("HIT_TRANSITION_REJECTED", sessionId, roundId, orbId);
            processedHits.Add(orbId);
            int before = MonsterHp;
            MonsterHp = Math.Max(0, MonsterHp - BaseDamage);
            ValidHitCount++;
            var result = new AttackHitResult(true, "VALID_HOST_COLLISION", sessionId, roundId,
                orbId, orb.OwnerPlayerId, before - MonsterHp, before, MonsterHp);
            if (MonsterHp == 0)
            {
                State = AttackBattleState.TargetCleared;
                ConsumeRemainingProjectiles();
            }
            ValidHit?.Invoke(result);
            return result;
        }

        public bool ExpireProjectile(string sessionId, uint roundId, string orbId)
        {
            if (ContextError(sessionId, roundId) != null || string.IsNullOrEmpty(orbId)
                || !launched.Contains(orbId) || !registry.TryGet(orbId, out var orb)) return false;
            return registry.TryAdvanceLaunch(sessionId, roundId, orbId, orb.AuthorityState,
                OrbAuthorityState.Consumed, out _);
        }

        public void EndDevelopmentRound(AttackBattleState terminalState = AttackBattleState.Ended)
        {
            if (terminalState != AttackBattleState.Ended && terminalState != AttackBattleState.NetworkError)
                throw new ArgumentOutOfRangeException(nameof(terminalState));
            State = terminalState;
            ConsumeRemainingProjectiles();
        }

        // T11 queries bind every original action bit before returning an accepted receipt.
        // The historical IDs-only diagnostic overload remains unchanged for earlier scenes.
        public AttackRequestStatus QueryRequest(ulong authenticatedSender, OrbActionRequest request)
        {
            if (request == null) return new AttackRequestStatus(false, "MISSING_REQUEST");
            var status = QueryRequest(authenticatedSender, request.SessionId, request.RoundId, request.RequestId, request.OrbId);
            if (!status.Known) return status;
            return receipts.TryGetValue(request.RequestId, out var receipt) && SamePayload(receipt.Request, request)
                ? status : new AttackRequestStatus(false, "REQUEST_PAYLOAD_MISMATCH");
        }

        public AttackRequestStatus QueryRequest(ulong authenticatedSender, string sessionId,
            uint roundId, string requestId, string orbId)
        {
            string contextError = ContextError(sessionId, roundId);
            if (contextError != null) return new AttackRequestStatus(false, contextError);
            if (string.IsNullOrWhiteSpace(requestId)) return new AttackRequestStatus(false, "MISSING_REQUEST_ID");
            if (receipts.TryGetValue(requestId, out var receipt))
            {
                if (receipt.Sender != authenticatedSender) return new AttackRequestStatus(false, "REQUEST_OWNER_MISMATCH");
                if (!string.Equals(receipt.Request.OrbId, orbId, StringComparison.Ordinal))
                    return new AttackRequestStatus(false, "REQUEST_ORB_MISMATCH");
                registry.TryGet(orbId, out var confirmed);
                return new AttackRequestStatus(true, "REQUEST_KNOWN", receipt.Result.AsDuplicate(),
                    confirmed != null && (confirmed.OwnerPlayerId == authenticatedSender || HostOrbRegistry.IsTransfer(receipt.Request.Kind)) ? confirmed : null,
                    registry.IsPending(orbId));
            }

            if (!registry.TryGet(orbId, out var unknownRequestOrb))
                return new AttackRequestStatus(false, "REQUEST_UNKNOWN_ORB_MISSING");
            if (unknownRequestOrb.OwnerPlayerId != authenticatedSender)
                return new AttackRequestStatus(false, "OWNER_MISMATCH");
            return new AttackRequestStatus(false, "REQUEST_UNKNOWN_CONFIRMED_ORB", null,
                unknownRequestOrb, registry.IsPending(orbId));
        }

        private void ConsumeRemainingProjectiles()
        {
            if (ContextError(boundSession, boundRound) != null) return;
            foreach (var orbId in launched)
                if (registry.TryGet(orbId, out var orb))
                    registry.TryAdvanceLaunch(boundSession, boundRound, orbId,
                        orb.AuthorityState, OrbAuthorityState.Consumed, out _);
        }

        private string ContextError(string sessionId, uint roundId)
        {
            if (!registry.HasSession) return "NO_ACTIVE_SESSION";
            if (!string.Equals(registry.SessionId, sessionId, StringComparison.Ordinal)) return "SESSION_MISMATCH";
            if (registry.RoundId != roundId) return "ROUND_MISMATCH";
            if (!string.Equals(boundSession, sessionId, StringComparison.Ordinal) || boundRound != roundId)
                return "ROUND_NOT_STARTED";
            return null;
        }

        private AttackHitResult HitRejected(string reason, string sessionId, uint roundId, string orbId)
            => new AttackHitResult(false, reason, sessionId, roundId, orbId, 0, 0, MonsterHp, MonsterHp);

        private static AttackLaunchResult Reject(string reason) => new AttackLaunchResult(false, false, reason);

        internal static bool SamePayload(OrbActionRequest left, OrbActionRequest right)
        {
            return string.Equals(left.SessionId, right.SessionId, StringComparison.Ordinal)
                && left.RoundId == right.RoundId
                && string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal)
                && string.Equals(left.OrbId, right.OrbId, StringComparison.Ordinal)
                && string.Equals(left.OtherOrbId, right.OtherOrbId, StringComparison.Ordinal)
                && left.Kind == right.Kind && left.SequenceNumber == right.SequenceNumber
                && left.NormalizedPosition.Equals(right.NormalizedPosition)
                && left.ThrowInput.HasValue == right.ThrowInput.HasValue
                && (!left.ThrowInput.HasValue || left.ThrowInput.Value.Equals(right.ThrowInput.Value))
                && left.TransferMotion.HasValue == right.TransferMotion.HasValue
                && (!left.TransferMotion.HasValue || left.TransferMotion.Value.Equals(right.TransferMotion.Value));
        }
    }
}
