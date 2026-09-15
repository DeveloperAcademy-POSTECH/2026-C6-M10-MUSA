using System;
using NUnit.Framework;

namespace C6.Prototype.Battle.Tests
{
    public sealed class MultiplayerBattleTests
    {
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void SharedClockNeedsAtLeastTwoAndFreezesResultForEveryRosterSize(int count)
        {
            var clock = new HostBattleClock(180, 1, 5);
            clock.BeginLobby("multiplayer-clock", 1, 1, false, 100);
            Assert.That(clock.Start(1), Is.False);
            Assert.That(clock.SetParticipants(count), Is.True);
            Assert.That(clock.Start(2), Is.True);
            Assert.That(clock.Participants, Is.EqualTo(count));
            Assert.That(clock.SetParticipants(2), Is.False, "Battle seats are frozen.");
            Assert.That(clock.Advance(100), Is.True); Assert.That(clock.Remaining, Is.EqualTo(82));
            Assert.That(clock.Advance(182), Is.True); Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(clock.Advance(200), Is.True); Assert.That(clock.Remaining, Is.Zero);
            Assert.That(clock.ObserveAppliedHit(200, 80), Is.False);
            clock.BeginLobby("multiplayer-clock", 2, count, false, 100);
            Assert.That(clock.Phase, Is.EqualTo(BattlePhase.Ready));
            Assert.That(clock.Remaining, Is.EqualTo(180)); Assert.That(clock.ObservedMonsterHp, Is.EqualTo(100));
        }
        [Test]
        public void ClockRejectsSixAndPreservesLegacyTwoPlayerCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HostBattleClock(180, 1, 6));
            var legacy = new HostBattleClock(180, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => legacy.BeginLobby("legacy", 1, 3, false, 100));
            var clock = new HostBattleClock(180, 1, 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.BeginLobby("five", 1, 6, false, 100));
            clock.BeginLobby("five", 1, 2, false, 100); Assert.That(clock.SetParticipants(6), Is.False);
            Assert.That(clock.Participants, Is.EqualTo(2));
        }
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void WireAllowsOnlyExplicitCapacityAndRequiresTwoForReady(int count)
        {
            var snapshot = new BattleSnapshot { nonce = Guid.NewGuid().ToString("N"), sessionId = "wire", roundId = 1, revision = 1,
                phase = BattlePhase.Ready.ToString(), remaining = 180, duration = 180, teamHp = 180, teamHpDecayPerSecond = 1,
                monsterMaxHp = 100, observedMonsterHp = 100, participants = count };
            Assert.That(BattleWire.ValidSnapshot(snapshot, 5), Is.True);
            Assert.That(BattleWire.ValidSnapshot(snapshot), Is.EqualTo(count == 2));
            snapshot.participants = 1; Assert.That(BattleWire.ValidSnapshot(snapshot, 5), Is.False);
            snapshot.participants = 6; Assert.That(BattleWire.ValidSnapshot(snapshot, 5), Is.False);
        }
    }
}
