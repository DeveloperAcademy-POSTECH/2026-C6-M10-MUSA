using System;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>Injected Host timestamps test the 180-second formula; these are not real-time waits or device evidence.</summary>
    public sealed class HostBattleClockTests
    {
        [Test]
        public void BootCannotStartCountDownAcceptHitsOrEnterConnectionResult()
        {
            var clock = new HostBattleClock(180, 1);
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Boot));
            Assert.That(clock.CanStart || clock.IsTerminal, Is.False);
            Assert.That(clock.Remaining, Is.Zero); Assert.That(clock.TeamHp, Is.Zero);
            Assert.That(clock.Start(10) || clock.Advance(10) || clock.CanApplyHit(10), Is.False);
            Assert.That(clock.ObserveAppliedHit(10, 0) || clock.NetworkError(10), Is.False);
            Assert.That(clock.SetParticipants(2), Is.False);
            Assert.That(clock.Matches(string.Empty, 0), Is.False);
        }

        [TestCase(0, false, BattlePhase.Lobby, false)]
        [TestCase(1, false, BattlePhase.Lobby, false)]
        [TestCase(2, false, BattlePhase.Ready, true)]
        [TestCase(0, true, BattlePhase.Lobby, false)]
        [TestCase(1, true, BattlePhase.Ready, true)]
        [TestCase(2, true, BattlePhase.Ready, true)]
        public void OrdinaryStartRequiresTwoAndOneParticipantRequiresExplicitDevelopmentMode(
            int participants, bool solo, BattlePhase expected, bool canStart)
        {
            var clock = NewLobby(participants, solo);
            Assert.That(clock.Phase, Is.EqualTo(expected));
            Assert.That(clock.CanStart, Is.EqualTo(canStart));
            Assert.That(clock.DevelopmentSolo, Is.EqualTo(solo));
            Assert.That(clock.RequiredParticipants, Is.EqualTo(solo ? 1 : 2));
            Assert.That(clock.Start(50), Is.EqualTo(canStart));
            Assert.That(clock.Phase, Is.EqualTo(canStart ? BattlePhase.Playing : expected));
            Assert.That(clock.Remaining, Is.EqualTo(180));
        }

        [Test]
        public void ParticipantReadinessChangesOnlyInLobbyAndReady()
        {
            var clock = NewLobby(1, false);
            Assert.That(clock.SetParticipants(2), Is.True); Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Ready));
            Assert.That(clock.SetParticipants(1), Is.True); Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Lobby));
            Assert.That(clock.SetParticipants(-1) || clock.SetParticipants(3), Is.False);
            Assert.That(clock.Participants, Is.EqualTo(1));
            Assert.That(clock.SetParticipants(2), Is.True); Assert.That(clock.Start(0), Is.True);
            Assert.That(clock.SetParticipants(1), Is.False, "Active connection loss uses NetworkError with its Host timestamp.");
            Assert.That(clock.Participants, Is.EqualTo(2));
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Playing));
        }

        [Test]
        public void LobbyReadyAndResultStayFrozenWhileOnlyPlayingUsesAbsoluteDeadline()
        {
            var clock = NewLobby(1, false);
            Assert.That(clock.Advance(100), Is.True); Assert.That(clock.Advance(500), Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(180)); Assert.That(clock.TeamHp, Is.EqualTo(180));
            Assert.That(clock.SetParticipants(2), Is.True); Assert.That(clock.Advance(600), Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(180)); Assert.That(clock.Deadline, Is.Zero);
            Assert.That(clock.Start(599), Is.False, "Older start time cannot bypass the monotonic Host sequence.");
            Assert.That(clock.Start(650), Is.True);
            Assert.That(clock.StartedAt, Is.EqualTo(650)); Assert.That(clock.Deadline, Is.EqualTo(830));
            Assert.That(clock.Advance(660), Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(170)); Assert.That(clock.ObservedMonsterHp, Is.EqualTo(100));
            Assert.That(clock.ObserveAppliedHit(660, 0), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(clock.Advance(10000), Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(170)); Assert.That(clock.TeamHp, Is.EqualTo(170));
            Assert.That(clock.Deadline, Is.EqualTo(830));
        }

        [Test]
        public void Full180SecondFormulaExpiresExactlyAtDeadlineWithoutFrameAccumulation()
        {
            var clock = Playing(1000);
            Assert.That(clock.DurationSeconds, Is.EqualTo(180)); Assert.That(clock.TeamHpDecayPerSecond, Is.EqualTo(1));
            Assert.That(clock.Deadline, Is.EqualTo(1180));
            Assert.That(clock.Advance(1000.25), Is.True); Assert.That(clock.Remaining, Is.EqualTo(179.75));
            Assert.That(clock.Advance(1060), Is.True); Assert.That(clock.Remaining, Is.EqualTo(120));
            Assert.That(clock.Advance(1179.999), Is.True); Assert.That(clock.Remaining, Is.EqualTo(.001).Within(1e-9));
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Playing));
            Assert.That(clock.Advance(1180), Is.True); Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(clock.Remaining, Is.Zero); Assert.That(clock.TeamHp, Is.Zero);
            Assert.That(clock.ObservedMonsterHp, Is.EqualTo(100), "Time expiry is not monster damage.");
        }

        [Test]
        public void PlayingSuspensionCountsElapsedHostTimeOnReturn()
        {
            var clock = Playing(300);
            Assert.That(clock.Advance(320), Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(160));
            // No Update calls for this gap: host suspension does not extend the absolute end time.
            Assert.That(clock.Advance(700), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(clock.Deadline, Is.EqualTo(480));
            Assert.That(clock.Remaining, Is.Zero);
        }

        [TestCase(179.999999, true, BattlePhase.Victory)]
        [TestCase(180, false, BattlePhase.Defeat)]
        [TestCase(180.000001, false, BattlePhase.Defeat)]
        public void FinalHitJustBeforeDeadlineWinsButExactOrLateHitCannotChangeMonsterHp(
            double processingTime, bool accepted, BattlePhase expected)
        {
            var clock = Playing();
            Assert.That(clock.CanApplyHit(processingTime), Is.EqualTo(accepted));
            Assert.That(clock.ObserveAppliedHit(processingTime, 0), Is.EqualTo(accepted));
            Assert.That(clock.Phase, Is.EqualTo(expected));
            Assert.That(clock.ObservedMonsterHp, Is.EqualTo(accepted ? 0 : 100));
            Assert.That(clock.TeamHp > 0, Is.EqualTo(accepted));
        }

        [TestCase(179.999, BattlePhase.Victory)]
        [TestCase(180, BattlePhase.Defeat)]
        [TestCase(181, BattlePhase.Defeat)]
        public void ClockBeforeHitAndHitBeforeClockAtSameTimestampProduceIdenticalResult(double now, BattlePhase expected)
        {
            var clockFirst = Playing(); var hitFirst = Playing();
            Assert.That(clockFirst.Advance(now), Is.True);
            bool firstGate = clockFirst.CanApplyHit(now);
            if (firstGate) Assert.That(clockFirst.ObserveAppliedHit(now, 0), Is.True);
            bool secondGate = hitFirst.CanApplyHit(now);
            if (secondGate) Assert.That(hitFirst.ObserveAppliedHit(now, 0), Is.True);
            Assert.That(hitFirst.Advance(now), Is.True);
            Assert.That(firstGate, Is.EqualTo(secondGate));
            Assert.That(clockFirst.Phase, Is.EqualTo(expected));
            Assert.That(Snapshot(clockFirst), Is.EqualTo(Snapshot(hitFirst)));
        }

        [Test]
        public void NonlethalHitsMirrorAlreadyAppliedHpWithoutAdditionalClockDamage()
        {
            var clock = Playing();
            Assert.That(clock.CanApplyHit(10), Is.True);
            Assert.That(clock.ObserveAppliedHit(10, 80), Is.True);
            Assert.That(clock.TeamHp, Is.EqualTo(170));
            Assert.That(clock.ObservedMonsterHp, Is.EqualTo(80));
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Playing));
            Assert.That(clock.CanApplyHit(10), Is.True);
            Assert.That(clock.ObserveAppliedHit(10, 60), Is.True);
            Assert.That(clock.TeamHp, Is.EqualTo(170));
            Assert.That(clock.ObserveAppliedHit(10, 60), Is.False, "A duplicate HP mirror event cannot imply another hit.");
            Assert.That(clock.ObservedMonsterHp, Is.EqualTo(60));
        }

        [TestCase(-1)]
        [TestCase(100)]
        [TestCase(101)]
        public void InvalidOrNondecreasingHpMirrorCannotAdvanceTimeOrMutateState(int hpAfter)
        {
            var clock = Playing(); var before = Snapshot(clock);
            Assert.That(clock.ObserveAppliedHit(25, hpAfter), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(before));
            Assert.That(clock.CanApplyHit(20), Is.True, "An invalid HP observation did not retire its proposed timestamp.");
        }

        [Test]
        public void RetroactiveCallbackCannotTurnAlreadyProcessedExpiryIntoVictory()
        {
            var clock = Playing();
            Assert.That(clock.Advance(180), Is.True); var frozen = Snapshot(clock);
            Assert.That(clock.CanApplyHit(179.9), Is.False);
            Assert.That(clock.ObserveAppliedHit(179.9, 0), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(frozen));
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Defeat));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1)]
        [TestCase(19.999)]
        public void InvalidAndBackwardTimesAreRejectedWithoutAnyPublicStateMutation(double invalid)
        {
            var clock = Playing(); Assert.That(clock.Advance(20), Is.True); var before = Snapshot(clock);
            Assert.That(clock.Advance(invalid), Is.False);
            Assert.That(clock.CanApplyHit(invalid), Is.False);
            Assert.That(clock.ObserveAppliedHit(invalid, 80), Is.False);
            Assert.That(clock.NetworkError(invalid), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(before));
            Assert.That(clock.Advance(20), Is.True, "Equal processing timestamps remain legal.");
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1)]
        [TestCase(double.MaxValue)]
        public void InvalidStartOrUnrepresentableDeadlineKeepsReadyRoundFull(double now)
        {
            var clock = NewLobby(2, false); var before = Snapshot(clock);
            Assert.That(clock.Start(now), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(before));
            Assert.That(clock.Start(0), Is.True);
        }

        [Test]
        public void VictoryIsFrozenAgainstTimeLateHitStartReadinessAndConnectionFailure()
        {
            var clock = Playing(); Assert.That(clock.ObserveAppliedHit(2, 0), Is.True); var frozen = Snapshot(clock);
            Assert.That(clock.Advance(500), Is.True);
            Assert.That(clock.CanApplyHit(500) || clock.ObserveAppliedHit(500, 0), Is.False);
            Assert.That(clock.Start(500) || clock.SetParticipants(1) || clock.NetworkError(500), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(frozen));
            Assert.That(clock.TeamHp, Is.EqualTo(178));
        }

        [Test]
        public void DefeatIsFrozenAgainstLaterClockHitAndConnectionFailure()
        {
            var clock = Playing(); Assert.That(clock.Advance(180), Is.True); var frozen = Snapshot(clock);
            Assert.That(clock.Advance(1000), Is.True);
            Assert.That(clock.CanApplyHit(1000) || clock.ObserveAppliedHit(1000, 0), Is.False);
            Assert.That(clock.Start(1000) || clock.SetParticipants(1) || clock.NetworkError(1000), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(frozen));
        }

        [TestCase(10, 170)]
        [TestCase(180, 0)]
        [TestCase(300, 0)]
        public void DisconnectFromUnresolvedPlayingIsNetworkErrorInsteadOfManufacturingDefeat(double now, double remaining)
        {
            var clock = Playing();
            Assert.That(clock.NetworkError(now), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.NetworkError));
            Assert.That(clock.Remaining, Is.EqualTo(remaining));
            Assert.That(clock.ObservedMonsterHp, Is.EqualTo(100));
            var frozen = Snapshot(clock);
            Assert.That(clock.Advance(1000), Is.True);
            Assert.That(clock.ObserveAppliedHit(1000, 0) || clock.NetworkError(1000), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(frozen));
        }

        [Test]
        public void LobbyConnectionFailureNeverStartsClock()
        {
            var clock = NewLobby(1, false);
            Assert.That(clock.NetworkError(500), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.NetworkError));
            Assert.That(clock.Remaining, Is.EqualTo(180));
            Assert.That(clock.StartedAt, Is.Zero); Assert.That(clock.Deadline, Is.Zero);
            Assert.That(clock.Start(501), Is.False);
        }

        [Test]
        public void ResetUsesIncreasingRoundFullClockAndRejectsOldRoundCallbacksWithoutMutation()
        {
            var clock = Playing(); Assert.That(clock.ObserveAppliedHit(50, 0), Is.True);
            Assert.Throws<InvalidOperationException>(() => clock.BeginLobby("session-a", 1, 1, true, 100));
            clock.BeginLobby("session-a", 2, 1, true, 100);
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Ready));
            Assert.That(clock.Remaining, Is.EqualTo(180)); Assert.That(clock.ObservedMonsterHp, Is.EqualTo(100));
            Assert.That(clock.StartedAt, Is.Zero); Assert.That(clock.Deadline, Is.Zero);
            Assert.That(clock.Start(51), Is.True); var fresh = Snapshot(clock);
            Assert.That(clock.Matches("session-a", 1), Is.False);
            Assert.That(clock.Advance("session-a", 1, 1000), Is.False);
            Assert.That(clock.CanApplyHit("session-a", 1, 52), Is.False);
            Assert.That(clock.ObserveAppliedHit("session-a", 1, 52, 0), Is.False);
            Assert.That(clock.NetworkError("session-a", 1, 52), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(fresh));
            Assert.That(clock.CanApplyHit("session-a", 2, 52), Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(179));
        }

        [Test]
        public void FreshSessionMayStartAtRoundOneButPreviousSessionCannotMutateIt()
        {
            var clock = Playing(); Assert.That(clock.NetworkError(2), Is.True);
            clock.BeginLobby("session-b", 1, 2, false, 100);
            Assert.That(clock.Matches("session-b", 1), Is.True);
            Assert.That(clock.Start(3), Is.True); var before = Snapshot(clock);
            Assert.That(clock.ObserveAppliedHit("session-a", 1, 4, 0) || clock.Advance("session-a", 1, 500), Is.False);
            Assert.That(clock.NetworkError("session-a", 1, 500), Is.False);
            Assert.That(Snapshot(clock), Is.EqualTo(before));
        }

        [Test]
        public void RebindingSameOrOlderRoundCannotResetResourcesImplicitlyThroughClock()
        {
            var clock = NewLobby(2, false); clock.BeginLobby("session-a", 3, 2, false, 100); clock.Start(20); clock.Advance(30);
            var before = Snapshot(clock);
            Assert.Throws<InvalidOperationException>(() => clock.BeginLobby("session-a", 3, 1, true, 100));
            Assert.Throws<InvalidOperationException>(() => clock.BeginLobby("session-a", 2, 1, true, 100));
            Assert.That(Snapshot(clock), Is.EqualTo(before));
        }

        [Test]
        public void NonDefaultTuningIsExplicitAndTeamHpTracksRateWithoutChangingDuration()
        {
            var clock = new HostBattleClock(5, 2); clock.BeginLobby("short-test", 1, 1, true, 100); clock.Start(10);
            Assert.That(clock.Remaining, Is.EqualTo(5)); Assert.That(clock.TeamHp, Is.EqualTo(10));
            Assert.That(clock.Advance(12), Is.True);
            Assert.That(clock.Remaining, Is.EqualTo(3)); Assert.That(clock.TeamHp, Is.EqualTo(6));
            Assert.That(clock.Deadline, Is.EqualTo(15));
            Assert.That(clock.Advance(15), Is.True); Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Defeat));
        }

        [TestCase(0, 1)]
        [TestCase(-1, 1)]
        [TestCase(double.NaN, 1)]
        [TestCase(double.PositiveInfinity, 1)]
        [TestCase(180, 0)]
        [TestCase(180, -1)]
        [TestCase(180, double.NaN)]
        [TestCase(180, double.PositiveInfinity)]
        [TestCase(double.MaxValue, 2)]
        public void InvalidClockTuningCannotCreateAuthority(double duration, double decay)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new HostBattleClock(duration, decay)); }

        [Test]
        public void InvalidLobbyBindingsDoNotReplaceAnExistingRound()
        {
            var clock = Playing(); var before = Snapshot(clock);
            Assert.Throws<ArgumentException>(() => clock.BeginLobby(" ", 2, 2, false, 100));
            Assert.Throws<ArgumentException>(() => clock.BeginLobby(new string('x', 129), 2, 2, false, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.BeginLobby("session-a", 0, 2, false, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.BeginLobby("session-a", 2, 3, false, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.BeginLobby("session-a", 2, -1, false, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.BeginLobby("session-a", 2, 2, false, 0));
            Assert.That(Snapshot(clock), Is.EqualTo(before));
        }

        [TestCase(179.999, true)]
        [TestCase(180, false)]
        [TestCase(181, false)]
        public void ClockGateProtectsExistingAttackAuthorityAndOnlyMirrorsAcceptedDamage(double now, bool allowed)
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("session-a", 1);
            var orb = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            var attack = new AttackAuthority(registry, 100, 100); attack.BeginDevelopmentRound();
            var clock = Playing();
            Assert.That(attack.RequestLaunch(0, new OrbActionRequest("session-a", 1, "launch", orb.OrbId, null,
                OrbActionKind.Launch, 1, Vector2.one * .5f)).Accepted, Is.True);
            Assert.That(attack.MarkProjectileSpawned("session-a", 1, orb.OrbId), Is.True);
            bool gate = clock.CanApplyHit("session-a", 1, now);
            Assert.That(gate, Is.EqualTo(allowed));
            if (gate)
            {
                var hit = attack.ProcessHostHit("session-a", 1, orb.OrbId);
                Assert.That(hit.Applied, Is.True);
                Assert.That(clock.ObserveAppliedHit("session-a", 1, now, hit.HpAfter), Is.True);
            }
            Assert.That(attack.MonsterHp, Is.EqualTo(allowed ? 0 : 100));
            Assert.That(attack.ValidHitCount, Is.EqualTo(allowed ? 1 : 0));
            Assert.That(clock.ObservedMonsterHp, Is.EqualTo(attack.MonsterHp));
            Assert.That(clock.Phase, Is.EqualTo(allowed ? BattlePhase.Victory : BattlePhase.Defeat));
            if (!allowed) attack.EndDevelopmentRound();
            Assert.That(registry.TryGet(orb.OrbId, out var finished), Is.True);
            Assert.That(finished.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
        }

        private static HostBattleClock NewLobby(int participants, bool solo)
        {
            var clock = new HostBattleClock(180, 1);
            clock.BeginLobby("session-a", 1, participants, solo, 100);
            return clock;
        }
        private static HostBattleClock Playing(double start = 0)
        {
            var clock = NewLobby(2, false);
            Assert.That(clock.Start(start), Is.True);
            return clock;
        }
        private static object[] Snapshot(HostBattleClock clock) => new object[] { clock.Phase, clock.SessionId, clock.RoundId,
            clock.Participants, clock.DevelopmentSolo, clock.MonsterMaxHp, clock.ObservedMonsterHp, clock.StartedAt,
            clock.Deadline, clock.Remaining, clock.TeamHp, clock.CanStart, clock.IsTerminal };
    }
}
