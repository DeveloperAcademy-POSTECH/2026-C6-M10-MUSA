using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>#28 Host attack schedule, random target, and defense judgment. Injected Host times only.</summary>
    public sealed class HostMonsterAttackTests
    {
        private static readonly ulong[] Roster = { 0, 1, 2 };
        private static HostMonsterAttack Started(double startedAt = 100, int seed = 7)
        {
            var attack = new HostMonsterAttack(20, 15, 3, seed);
            attack.Begin(startedAt);
            return attack;
        }

        [Test]
        public void FirstAttackAtTwentySecondsThenEveryFifteen()
        {
            var attack = Started(100);
            Assert.That(attack.Tick(119.9, Roster), Is.EqualTo(MonsterAttackResult.None));
            Assert.That(attack.Sequence, Is.Zero);
            attack.Tick(120, Roster);
            Assert.That(attack.Sequence, Is.EqualTo(1));
            Assert.That(attack.Active, Is.True);
            Assert.That(attack.WarningStartsAt, Is.EqualTo(120));
            Assert.That(attack.WarningEndsAt, Is.EqualTo(123));
            Assert.That(attack.Tick(134.9, Roster), Is.EqualTo(MonsterAttackResult.Hit), "undefended attack resolves after the grace");
            Assert.That(attack.Sequence, Is.EqualTo(1));
            attack.Tick(135, Roster);
            Assert.That(attack.Sequence, Is.EqualTo(2));
            Assert.That(attack.WarningEndsAt, Is.EqualTo(138));
        }

        [Test]
        public void ResultWaitsForTheReportGraceAfterTheWarning()
        {
            var attack = Started(0);
            attack.Tick(20, Roster);
            Assert.That(attack.Tick(23, Roster), Is.EqualTo(MonsterAttackResult.None));
            Assert.That(attack.Tick(23 + HostMonsterAttack.ReportGraceSeconds, Roster), Is.EqualTo(MonsterAttackResult.Hit));
            Assert.That(attack.ResolvedSequence, Is.EqualTo(1));
            Assert.That(attack.Active, Is.False);
        }

        [Test]
        public void TargetsAreAlwaysParticipantsAndSpreadAcrossTheRoster()
        {
            var attack = Started(0);
            var targets = new List<ulong>();
            for (double now = 20; now < 20 + 15 * 40; now += 15)
            {
                attack.Tick(now, Roster);
                targets.Add(attack.Target);
                attack.Tick(now + 5, Roster);
            }
            Assert.That(targets, Has.All.Matches<ulong>(id => Roster.Contains(id)));
            Assert.That(targets.Distinct().Count(), Is.EqualTo(Roster.Length));
        }

        [Test]
        public void OnlyTheCurrentTargetCanDefendTheCurrentAttackInTime()
        {
            var attack = Started(0);
            attack.Tick(20, Roster);
            ulong target = attack.Target;
            ulong other = Roster.First(id => id != target);
            Assert.That(attack.AcceptDefense(other, 1, 21), Is.False, "someone else");
            Assert.That(attack.AcceptDefense(target, 2, 21), Is.False, "wrong attack");
            Assert.That(attack.AcceptDefense(target, 1, 23 + HostMonsterAttack.ReportGraceSeconds + .01), Is.False, "too late");
            Assert.That(attack.AcceptDefense(target, 1, 22.9), Is.True);
            Assert.That(attack.Tick(24, Roster), Is.EqualTo(MonsterAttackResult.Defended));
            Assert.That(attack.AcceptDefense(target, 1, 24.1), Is.False, "already resolved");
        }

        [Test]
        public void AReportArrivingWithinTheGraceStillCounts()
        {
            var attack = Started(0);
            attack.Tick(20, Roster);
            Assert.That(attack.AcceptDefense(attack.Target, 1, 23.2), Is.True);
            Assert.That(attack.Tick(23.3, Roster), Is.EqualTo(MonsterAttackResult.Defended));
        }

        [Test]
        public void BeginClearsThePreviousRoundAndAnEmptyRosterNeverAttacks()
        {
            var attack = Started(0);
            attack.Tick(20, Roster);
            attack.Begin(500);
            Assert.That(attack.Active || attack.Sequence != 0 || attack.LastResult != MonsterAttackResult.None, Is.False);
            attack.Tick(600, Array.Empty<ulong>());
            Assert.That(attack.Sequence, Is.Zero);
        }

        [TestCase(0, 15, 3)]
        [TestCase(20, 0, 3)]
        [TestCase(20, 15, double.NaN)]
        public void InvalidTimingIsRejected(double first, double interval, double warning) =>
            Assert.That(() => new HostMonsterAttack(first, interval, warning, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}