using System;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class AttackAuthorityTests
    {
        private HostOrbRegistry registry;
        private AttackAuthority authority;

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true);
            registry.BeginSession("attack-session", 1);
            authority = new AttackAuthority(registry, 100, 20);
        }

        [Test]
        public void ActiveNetworkSessionAloneDoesNotEnableAttack()
        {
            var orb = Add();
            Assert.That(authority.State, Is.EqualTo(AttackBattleState.NotStarted));
            AssertRejected(authority.RequestLaunch(7, Request(orb)), "ROUND_NOT_STARTED");
            AssertOrb(orb.OrbId, OrbAuthorityState.Idle, 7);
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            authority.BeginDevelopmentRound();
            Assert.That(authority.State, Is.EqualTo(AttackBattleState.Playing));
            Assert.That(authority.RequestLaunch(7, Request(orb)).Accepted, Is.True);
        }

        [Test]
        public void DevelopmentBattleCannotStartWithoutExplicitModeAndSession()
        {
            var ordinary = new HostOrbRegistry(false);
            ordinary.BeginSession("normal", 1);
            Assert.Throws<InvalidOperationException>(() => new AttackAuthority(ordinary, 100, 20).BeginDevelopmentRound());
            var inactive = new HostOrbRegistry(true);
            Assert.Throws<InvalidOperationException>(() => new AttackAuthority(inactive, 100, 20).BeginDevelopmentRound());
            Assert.Throws<ArgumentNullException>(() => new AttackAuthority(null, 100, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttackAuthority(registry, 0, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AttackAuthority(registry, 100, -1));
        }

        [Test]
        public void StartingSameRoundAgainCannotRestoreHpOrClearLocks()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            Spawn(orb);
            Assert.That(Hit(orb).Applied, Is.True);
            Assert.Throws<InvalidOperationException>(() => authority.BeginDevelopmentRound());
            Assert.That(authority.MonsterHp, Is.EqualTo(80));
            AssertOrb(orb.OrbId, OrbAuthorityState.Consumed, 7);
        }

        [TestCase(OrbPolarity.Yin)]
        [TestCase(OrbPolarity.Yang)]
        public void RawAttackIsRejectedWithoutRemovingOrSpendingItsSequence(OrbPolarity polarity)
        {
            authority.BeginDevelopmentRound();
            var raw = registry.RegisterDevelopmentOrb(7, OrbKind.Raw, polarity, Vector2.one * .5f);
            AssertRejected(authority.RequestLaunch(7, Request(raw)), "RAW_CANNOT_LAUNCH");
            AssertOrb(raw.OrbId, OrbAuthorityState.Idle, 7);
            Assert.That(registry.IsPending(raw.OrbId), Is.False);
            Assert.That(registry.TryGet(raw.OrbId, out var unchanged), Is.True);
            Assert.That(unchanged, Is.SameAs(raw));
            var transfer = new OrbActionRequest("attack-session", 1, "valid-reservation", raw.OrbId,
                null, OrbActionKind.TransferLeft, 1, Vector2.one * .5f);
            Assert.That(registry.Reserve(7, transfer).Accepted, Is.True);
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
        }

        [TestCase("sender", "OWNER_MISMATCH")]
        [TestCase("session", "SESSION_MISMATCH")]
        [TestCase("old-round", "ROUND_MISMATCH")]
        [TestCase("future-round", "ROUND_MISMATCH")]
        [TestCase("sequence", "STALE_SEQUENCE")]
        [TestCase("unknown-orb", "ORB_NOT_FOUND")]
        [TestCase("request-id", "MISSING_REQUEST_ID")]
        [TestCase("position", "INVALID_POSITION")]
        [TestCase("extra-orb", "UNEXPECTED_SECOND_ORB")]
        [TestCase("transfer", "NOT_A_LAUNCH")]
        public void MalformedOrUnauthorizedLaunchCannotMutateTheOrb(string scenario, string reason)
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            var request = new OrbActionRequest(scenario == "session" ? "old-session" : "attack-session",
                scenario == "old-round" ? 0u : scenario == "future-round" ? 2u : 1u,
                scenario == "request-id" ? " " : "launch",
                scenario == "unknown-orb" ? "missing-orb" : orb.OrbId,
                scenario == "extra-orb" ? "extra" : null,
                scenario == "transfer" ? OrbActionKind.TransferLeft : OrbActionKind.Launch,
                scenario == "sequence" ? 0ul : 1ul,
                scenario == "position" ? new Vector2(float.NaN, .9f) : new Vector2(.5f, .9f));
            AssertRejected(authority.RequestLaunch(scenario == "sender" ? 8ul : 7ul, request), reason);
            AssertOrb(orb.OrbId, OrbAuthorityState.Idle, 7);
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
            Assert.That(authority.ValidHitCount, Is.Zero);
        }

        [Test]
        public void LaunchKeepsOneIdentityOwnerAndActionCoordinatesUntilActualHit()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            var request = Request(orb, sequence: 17, position: new Vector2(.2f, .95f));
            var accepted = authority.RequestLaunch(7, request);
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.SpawnRequired, Is.True);
            Assert.That(accepted.IsDuplicate, Is.False);
            AssertOrb(orb.OrbId, OrbAuthorityState.Launching, 7);
            Assert.That(registry.TryGet(orb.OrbId, out var launching), Is.True);
            Assert.That(launching.NormalizedPosition, Is.EqualTo(request.NormalizedPosition));
            Assert.That(launching.SequenceNumber, Is.EqualTo(17));
            Assert.That(launching.EntrySide, Is.EqualTo(orb.EntrySide));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
            Assert.That(authority.MarkProjectileSpawned("attack-session", 1, orb.OrbId), Is.True);
            AssertOrb(orb.OrbId, OrbAuthorityState.Projectile, 7);
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
        }

        [Test]
        public void ExactDuplicateReplaysApprovalButNeverRequestsASecondSpawn()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            var request = Request(orb);
            var first = authority.RequestLaunch(7, request);
            Assert.That(first.SpawnRequired, Is.True);
            Assert.That(authority.MarkProjectileSpawned("attack-session", 1, orb.OrbId), Is.True);
            var duplicate = authority.RequestLaunch(7, Request(orb));
            Assert.That(duplicate.Accepted, Is.True);
            Assert.That(duplicate.IsDuplicate, Is.True);
            Assert.That(duplicate.SpawnRequired, Is.False);
            Assert.That(duplicate.Orb, Is.SameAs(first.Orb));
            Assert.That(duplicate.Reason, Is.EqualTo(first.Reason));
            Assert.That(authority.MarkProjectileSpawned("attack-session", 1, orb.OrbId), Is.False);
            Assert.That(Hit(orb).Applied, Is.True);
            var afterHit = authority.RequestLaunch(7, request);
            Assert.That(afterHit.Accepted, Is.True);
            Assert.That(afterHit.SpawnRequired, Is.False);
            AssertOrb(orb.OrbId, OrbAuthorityState.Consumed, 7);
            Assert.That(authority.MonsterHp, Is.EqualTo(80));
        }

        [TestCase("sender")]
        [TestCase("orb")]
        [TestCase("position")]
        [TestCase("sequence")]
        [TestCase("kind")]
        [TestCase("second-orb")]
        public void ReusedRequestIdCannotChangeItsPayloadOrAuthenticatedSender(string field)
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            var other = Add();
            var first = authority.RequestLaunch(7, Request(orb));
            var changed = new OrbActionRequest("attack-session", 1, "launch",
                field == "orb" ? other.OrbId : orb.OrbId,
                field == "second-orb" ? other.OrbId : null,
                field == "kind" ? OrbActionKind.TransferRight : OrbActionKind.Launch,
                field == "sequence" ? 2ul : 1ul,
                field == "position" ? new Vector2(.7f, .9f) : new Vector2(.5f, .9f));
            AssertRejected(authority.RequestLaunch(field == "sender" ? 8ul : 7ul, changed), "REQUEST_ID_CONFLICT");
            AssertOrb(other.OrbId, OrbAuthorityState.Idle, 7);
            Assert.That(authority.RequestLaunch(7, Request(orb)).Orb, Is.SameAs(first.Orb));
        }

        [Test]
        public void NewRequestCannotRelaunchAnApprovedOrConsumedOrb()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            Spawn(orb);
            AssertRejected(authority.RequestLaunch(7, Request(orb, "another", 2)), "ORB_NOT_IDLE");
            Hit(orb);
            AssertRejected(authority.RequestLaunch(7, Request(orb, "third", 3)), "ORB_NOT_IDLE");
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectedReceiptIsStableAndDoesNotSpendSequence()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            var bad = Request(orb, sequence: 0);
            AssertRejected(authority.RequestLaunch(7, bad), "STALE_SEQUENCE");
            var repeated = authority.RequestLaunch(7, bad);
            Assert.That(repeated.Accepted, Is.False);
            Assert.That(repeated.IsDuplicate, Is.True);
            Assert.That(repeated.SpawnRequired, Is.False);
            Assert.That(repeated.Reason, Is.EqualTo("STALE_SEQUENCE"));
            Assert.That(authority.RequestLaunch(7, Request(orb, "corrected", 1)).Accepted, Is.True);
        }

        [Test]
        public void LostApprovalCanBeQueriedWithoutRecreatingAConsumedOrb()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            Spawn(orb);
            var flying = authority.QueryRequest(7, "attack-session", 1, "launch", orb.OrbId);
            Assert.That(flying.Known, Is.True);
            Assert.That(flying.Receipt.Accepted, Is.True);
            Assert.That(flying.Receipt.SpawnRequired, Is.False);
            Assert.That(flying.ConfirmedOrb.AuthorityState, Is.EqualTo(OrbAuthorityState.Projectile));
            Assert.That(flying.IsPending, Is.False);
            Hit(orb);
            var hit = authority.QueryRequest(7, "attack-session", 1, "launch", orb.OrbId);
            Assert.That(hit.Known, Is.True);
            Assert.That(hit.ConfirmedOrb.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(hit.Receipt.SpawnRequired, Is.False);
            Assert.That(authority.MonsterHp, Is.EqualTo(80));
        }

        [Test]
        public void UnknownRequestReturnsOnlyConfirmedOwnedStateAndDoesNotUnlockExistingPending()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            var idle = authority.QueryRequest(7, "attack-session", 1, "not-received", orb.OrbId);
            Assert.That(idle.Known, Is.False);
            Assert.That(idle.ConfirmedOrb, Is.SameAs(orb));
            Assert.That(idle.IsPending, Is.False);
            var reservation = new OrbActionRequest("attack-session", 1, "other-pending", orb.OrbId,
                null, OrbActionKind.TransferLeft, 1, Vector2.one * .5f);
            Assert.That(registry.Reserve(7, reservation).Accepted, Is.True);
            var pending = authority.QueryRequest(7, "attack-session", 1, "not-received", orb.OrbId);
            Assert.That(pending.Known, Is.False);
            Assert.That(pending.IsPending, Is.True);
            Assert.That(registry.IsPending(orb.OrbId), Is.True);
            var outsider = authority.QueryRequest(8, "attack-session", 1, "not-received", orb.OrbId);
            Assert.That(outsider.ConfirmedOrb, Is.Null);
            Assert.That(outsider.Reason, Is.EqualTo("OWNER_MISMATCH"));
        }

        [Test]
        public void RequestStatusChecksSessionRoundRequestOwnerAndOrbAssociation()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            Spawn(orb);
            Assert.That(authority.QueryRequest(7, "old", 1, "launch", orb.OrbId).Reason, Is.EqualTo("SESSION_MISMATCH"));
            Assert.That(authority.QueryRequest(7, "attack-session", 0, "launch", orb.OrbId).Reason, Is.EqualTo("ROUND_MISMATCH"));
            Assert.That(authority.QueryRequest(8, "attack-session", 1, "launch", orb.OrbId).Reason, Is.EqualTo("REQUEST_OWNER_MISMATCH"));
            Assert.That(authority.QueryRequest(7, "attack-session", 1, "launch", "other").Reason, Is.EqualTo("REQUEST_ORB_MISMATCH"));
            Assert.That(authority.QueryRequest(7, "attack-session", 1, " ", orb.OrbId).Receipt, Is.Null);
            Assert.That(authority.QueryRequest(7, "attack-session", 1, "missing", "other").ConfirmedOrb, Is.Null);
        }

        [Test]
        public void HitRequiresAnActuallySpawnedProjectileAndMatchingRound()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            Assert.That(Hit(orb).Applied, Is.False);
            Assert.That(authority.RequestLaunch(7, Request(orb)).Accepted, Is.True);
            Assert.That(Hit(orb).Reason, Is.EqualTo("ORB_NOT_PROJECTILE"));
            Assert.That(authority.MarkProjectileSpawned("wrong", 1, orb.OrbId), Is.False);
            Assert.That(authority.MarkProjectileSpawned("attack-session", 2, orb.OrbId), Is.False);
            Assert.That(authority.MarkProjectileSpawned("attack-session", 1, orb.OrbId), Is.True);
            Assert.That(authority.ProcessHostHit("wrong", 1, orb.OrbId).Applied, Is.False);
            Assert.That(authority.ProcessHostHit("attack-session", 0, orb.OrbId).Applied, Is.False);
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
            AssertOrb(orb.OrbId, OrbAuthorityState.Projectile, 7);
        }

        [Test]
        public void FirstHitCommitsProcessedAndConsumedBeforeHpObserversAndRecoveryHook()
        {
            authority.BeginDevelopmentRound();
            var orb = Add(owner: 42);
            Spawn(orb);
            int events = 0;
            authority.ValidHit += result =>
            {
                events++;
                AssertOrb(orb.OrbId, OrbAuthorityState.Consumed, 42);
                Assert.That(authority.MonsterHp, Is.EqualTo(80));
                Assert.That(authority.ValidHitCount, Is.EqualTo(1));
                Assert.That(result.AttackerPlayerId, Is.EqualTo(42));
                Assert.That(result.OrbId, Is.EqualTo(orb.OrbId));
                Assert.That(result.SessionId, Is.EqualTo("attack-session"));
                Assert.That(result.RoundId, Is.EqualTo(1));
                Assert.That(result.Damage, Is.EqualTo(20));
                Assert.That(Hit(orb).Applied, Is.False, "Reentrant callback must not damage or emit recovery twice.");
            };
            var first = Hit(orb);
            Assert.That(first.Applied, Is.True);
            Assert.That(first.HpBefore, Is.EqualTo(100));
            Assert.That(first.HpAfter, Is.EqualTo(80));
            for (int i = 0; i < 3; i++) Assert.That(Hit(orb).Applied, Is.False);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(authority.MonsterHp, Is.EqualTo(80));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissOrSpawnFailureExpiresOnceWithoutDamageOrRecovery(bool wasSpawned)
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            authority.RequestLaunch(7, Request(orb));
            if (wasSpawned) authority.MarkProjectileSpawned("attack-session", 1, orb.OrbId);
            int recoveryHooks = 0;
            authority.ValidHit += _ => recoveryHooks++;
            Assert.That(authority.ExpireProjectile("attack-session", 1, orb.OrbId), Is.True);
            Assert.That(authority.ExpireProjectile("attack-session", 1, orb.OrbId), Is.False);
            Assert.That(Hit(orb).Applied, Is.False);
            AssertOrb(orb.OrbId, OrbAuthorityState.Consumed, 7);
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
            Assert.That(authority.ValidHitCount, Is.Zero);
            Assert.That(recoveryHooks, Is.Zero);
            Assert.That(authority.RequestLaunch(7, Request(orb)).SpawnRequired, Is.False);
        }

        [TestCase(AttackBattleState.Ended)]
        [TestCase(AttackBattleState.NetworkError)]
        public void EndingDevelopmentRoundConsumesFlyingOrbsAndBlocksLateHits(AttackBattleState terminal)
        {
            authority.BeginDevelopmentRound();
            var launching = Add();
            var flying = Add();
            var idle = Add();
            authority.RequestLaunch(7, Request(launching, "a"));
            Spawn(flying, "b");
            authority.EndDevelopmentRound(terminal);
            Assert.That(authority.State, Is.EqualTo(terminal));
            AssertOrb(launching.OrbId, OrbAuthorityState.Consumed, 7);
            AssertOrb(flying.OrbId, OrbAuthorityState.Consumed, 7);
            AssertOrb(idle.OrbId, OrbAuthorityState.Idle, 7);
            Assert.That(Hit(flying).Applied, Is.False);
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
            AssertRejected(authority.RequestLaunch(7, Request(idle, "c")), "BATTLE_NOT_PLAYING");
            Assert.That(authority.RequestLaunch(7, Request(flying, "b")).SpawnRequired, Is.False);
            Assert.Throws<InvalidOperationException>(() => authority.BeginDevelopmentRound());
        }

        [Test]
        public void FiveDefaultHitsClearTargetAndBlockRemainingProjectilesWithoutCallingItVictory()
        {
            authority.BeginDevelopmentRound();
            var leftover = Add();
            Spawn(leftover, "leftover");
            int events = 0;
            authority.ValidHit += _ => events++;
            for (int i = 0; i < 5; i++)
            {
                var orb = Add();
                Spawn(orb, "hit-" + i);
                Assert.That(Hit(orb).Applied, Is.True);
                Assert.That(authority.MonsterHp, Is.EqualTo(100 - (i + 1) * 20));
            }
            Assert.That(authority.State, Is.EqualTo(AttackBattleState.TargetCleared));
            AssertOrb(leftover.OrbId, OrbAuthorityState.Consumed, 7);
            Assert.That(Hit(leftover).Applied, Is.False);
            Assert.That(authority.ValidHitCount, Is.EqualTo(5));
            Assert.That(events, Is.EqualTo(5));
            var unused = Add();
            AssertRejected(authority.RequestLaunch(7, Request(unused, "sixth")), "BATTLE_NOT_PLAYING");
        }

        [Test]
        public void ConfigValuesDriveDamageAndHpClampsAtZero()
        {
            authority = new AttackAuthority(registry, 33, 12);
            authority.BeginDevelopmentRound();
            for (int i = 0; i < 3; i++)
            {
                var orb = Add();
                Spawn(orb, "hit-" + i);
                var hit = Hit(orb);
                Assert.That(hit.Damage, Is.EqualTo(i == 2 ? 9 : 12));
            }
            Assert.That(authority.MonsterHp, Is.Zero);
            Assert.That(authority.State, Is.EqualTo(AttackBattleState.TargetCleared));
        }

        [Test]
        public void ExplicitNewRoundClearsOldReceiptsProjectilesAndHpButOldMessagesStayInvalid()
        {
            authority.BeginDevelopmentRound();
            var old = Add();
            var oldRequest = Request(old);
            Spawn(old);
            Hit(old);
            authority.EndDevelopmentRound();
            registry.ResetRound(2);
            Assert.That(registry.Snapshot(), Is.Empty);
            authority.BeginDevelopmentRound();
            Assert.That(authority.MonsterHp, Is.EqualTo(100));
            Assert.That(authority.ValidHitCount, Is.Zero);
            AssertRejected(authority.RequestLaunch(7, oldRequest), "ROUND_MISMATCH");
            Assert.That(Hit(old).Applied, Is.False);
            Assert.That(authority.ExpireProjectile("attack-session", 1, old.OrbId), Is.False);
            Assert.That(authority.QueryRequest(7, "attack-session", 1, "launch", old.OrbId).ConfirmedOrb, Is.Null);
            var fresh = Add();
            Assert.That(fresh.OrbId, Is.Not.EqualTo(old.OrbId));
            var newRequest = new OrbActionRequest("attack-session", 2, "launch", fresh.OrbId,
                null, OrbActionKind.Launch, 1, new Vector2(.5f, .9f));
            Assert.That(authority.RequestLaunch(7, newRequest).SpawnRequired, Is.True);
            Assert.That(authority.MarkProjectileSpawned("attack-session", 2, fresh.OrbId), Is.True);
            Assert.That(authority.ProcessHostHit("attack-session", 2, fresh.OrbId).Applied, Is.True);
        }

        [Test]
        public void RegistrySessionChangeCannotSilentlyKeepOldBattlePlaying()
        {
            authority.BeginDevelopmentRound();
            var orb = Add();
            Spawn(orb);
            registry.ClearSession();
            Assert.That(Hit(orb).Reason, Is.EqualTo("NO_ACTIVE_SESSION"));
            AssertRejected(authority.RequestLaunch(7, Request(orb)), "NO_ACTIVE_SESSION");
            registry.BeginSession("new-session", 1);
            var fresh = Add();
            var request = new OrbActionRequest("new-session", 1, "launch", fresh.OrbId,
                null, OrbActionKind.Launch, 1, Vector2.one * .9f);
            AssertRejected(authority.RequestLaunch(7, request), "ROUND_NOT_STARTED");
            authority.BeginDevelopmentRound();
            Assert.That(authority.RequestLaunch(7, request).Accepted, Is.True);
        }

        [Test]
        public void RegistryLifecycleRequiresItsCurrentLaunchReservationAndOnlyMovesForward()
        {
            var orb = Add();
            var reserved = registry.Reserve(7, Request(orb));
            AssertOrb(orb.OrbId, OrbAuthorityState.Idle, 7);
            Assert.That(registry.TryAdvanceLaunch("attack-session", 1, orb.OrbId,
                OrbAuthorityState.Idle, OrbAuthorityState.Consumed, out _), Is.False);
            Assert.That(registry.TryBeginReservedLaunch(reserved.Reservation, out var launching), Is.True);
            Assert.That(launching.OrbId, Is.EqualTo(orb.OrbId));
            Assert.That(registry.TryBeginReservedLaunch(reserved.Reservation, out _), Is.False);
            Assert.That(registry.TryAdvanceLaunch("attack-session", 1, orb.OrbId,
                OrbAuthorityState.Launching, OrbAuthorityState.Idle, out _), Is.False);
            Assert.That(registry.TryAdvanceLaunch("attack-session", 1, orb.OrbId,
                OrbAuthorityState.Launching, OrbAuthorityState.Projectile, out _), Is.True);
            Assert.That(registry.TryAdvanceLaunch("attack-session", 1, orb.OrbId,
                OrbAuthorityState.Projectile, OrbAuthorityState.Consumed, out _), Is.True);
            Assert.That(registry.TryAdvanceLaunch("attack-session", 1, orb.OrbId,
                OrbAuthorityState.Consumed, OrbAuthorityState.Projectile, out _), Is.False);
            registry.ResetRound(2);
            Assert.That(registry.TryBeginReservedLaunch(reserved.Reservation, out _), Is.False);
        }

        [Test]
        public void TransferReservationCannotBeConfirmedAsAnAttack()
        {
            var orb = Add();
            var transfer = new OrbActionRequest("attack-session", 1, "transfer", orb.OrbId, null,
                OrbActionKind.TransferLeft, 1, Vector2.one * .5f);
            var reserved = registry.Reserve(7, transfer);
            Assert.That(registry.TryBeginReservedLaunch(reserved.Reservation, out _), Is.False);
            Assert.That(registry.TryBeginReservedLaunch(null, out _), Is.False);
            AssertOrb(orb.OrbId, OrbAuthorityState.Idle, 7);
            Assert.That(registry.IsPending(orb.OrbId), Is.True);
        }

        private OrbRecord Add(ulong owner = 7) => registry.RegisterDevelopmentOrb(owner,
            OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);

        private static OrbActionRequest Request(OrbRecord orb, string id = "launch", ulong sequence = 1,
            Vector2? position = null) => new OrbActionRequest("attack-session", 1, id, orb.OrbId,
                null, OrbActionKind.Launch, sequence, position ?? new Vector2(.5f, .9f));

        private void Spawn(OrbRecord orb, string id = "launch")
        {
            var accepted = authority.RequestLaunch(orb.OwnerPlayerId, Request(orb, id));
            Assert.That(accepted.SpawnRequired, Is.True, accepted.Reason);
            Assert.That(authority.MarkProjectileSpawned("attack-session", 1, orb.OrbId), Is.True);
        }

        private AttackHitResult Hit(OrbRecord orb) => authority.ProcessHostHit("attack-session", 1, orb.OrbId);

        private void AssertOrb(string id, OrbAuthorityState state, ulong owner)
        {
            Assert.That(registry.TryGet(id, out var orb), Is.True);
            Assert.That(orb.OrbId, Is.EqualTo(id));
            Assert.That(orb.AuthorityState, Is.EqualTo(state));
            Assert.That(orb.OwnerPlayerId, Is.EqualTo(owner));
        }

        private static void AssertRejected(AttackLaunchResult result, string reason)
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.SpawnRequired, Is.False);
            Assert.That(result.IsDuplicate, Is.False);
            Assert.That(result.Reason, Is.EqualTo(reason));
            Assert.That(result.Orb, Is.Null);
        }
    }
}
