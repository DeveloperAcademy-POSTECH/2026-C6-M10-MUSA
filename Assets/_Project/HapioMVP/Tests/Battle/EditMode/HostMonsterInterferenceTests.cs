using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace C6.Prototype.Battle.Tests
{
    public sealed class HostMonsterInterferenceTests
    {
        private static readonly ulong[] Roster = { 0, 1, 2, 3, 4 };

        private static HostMonsterInterference Started(double startedAt = 100, int seed = 7)
        {
            var interference = new HostMonsterInterference(
                HostMonsterInterference.DefaultIntervalSeconds,
                HostMonsterInterference.DefaultDurationSeconds,
                seed);
            interference.Begin(startedAt);
            return interference;
        }

        [Test]
        public void StartsEveryThirtySecondsAndLastsEightSeconds()
        {
            var interference = Started(100);

            Assert.That(interference.Tick(129.9, Roster), Is.False);
            Assert.That(interference.Sequence, Is.Zero);

            Assert.That(interference.Tick(130, Roster), Is.True);
            Assert.That(interference.Active, Is.True);
            Assert.That(interference.Sequence, Is.EqualTo(1));
            Assert.That(interference.StartsAt, Is.EqualTo(130));
            Assert.That(interference.EndsAt, Is.EqualTo(138));

            Assert.That(interference.Tick(137.9, Roster), Is.False);
            Assert.That(interference.Tick(138, Roster), Is.True);
            Assert.That(interference.Active, Is.False);

            Assert.That(interference.Tick(159.9, Roster), Is.False);
            Assert.That(interference.Tick(160, Roster), Is.True);
            Assert.That(interference.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void RandomEffectsUseOnlyKnownKindsDirectionsAndParticipants()
        {
            var interference = Started(0);
            var kinds = new HashSet<MonsterInterferenceKind>();
            var directions = new HashSet<MonsterTransferDirection>();

            for (double now = 30; now <= 30 * 100; now += 30)
            {
                interference.Tick(now, Roster);
                kinds.Add(interference.Kind);
                directions.Add(interference.Direction);

                Assert.That(interference.Tag, Is.Not.EqualTo(MonsterInterferenceTags.None));
                Assert.That(
                    interference.Direction == MonsterTransferDirection.Left ||
                    interference.Direction == MonsterTransferDirection.Right,
                    Is.True);
                if (interference.Kind == MonsterInterferenceKind.SinglePlayerDirectionBlock)
                {
                    Assert.That(interference.HasTarget, Is.True);
                    Assert.That(Roster.Contains(interference.Target), Is.True);
                }
                else
                {
                    Assert.That(interference.Kind, Is.EqualTo(MonsterInterferenceKind.AllPlayersDirectionRestriction));
                    Assert.That(interference.HasTarget, Is.False);
                }

                interference.Tick(now + HostMonsterInterference.DefaultDurationSeconds, Roster);
            }

            Assert.That(kinds, Is.EquivalentTo(new[]
            {
                MonsterInterferenceKind.SinglePlayerDirectionBlock,
                MonsterInterferenceKind.AllPlayersDirectionRestriction
            }));
            Assert.That(directions, Is.EquivalentTo(new[]
            {
                MonsterTransferDirection.Left,
                MonsterTransferDirection.Right
            }));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(19)]
        [TestCase(31)]
        public void BothEffectKindsAppearWithinEveryTwoActivations(int seed)
        {
            var interference = Started(0, seed);

            Assert.That(interference.Tick(30, Roster), Is.True);
            MonsterInterferenceKind first = interference.Kind;
            Assert.That(interference.Tick(38, Roster), Is.True);
            Assert.That(interference.Tick(60, Roster), Is.True);
            MonsterInterferenceKind second = interference.Kind;

            Assert.That(first, Is.Not.EqualTo(MonsterInterferenceKind.None));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(new[] { first, second }, Is.EquivalentTo(new[]
            {
                MonsterInterferenceKind.SinglePlayerDirectionBlock,
                MonsterInterferenceKind.AllPlayersDirectionRestriction
            }));
        }

        [Test]
        public void HostIdZeroCanBeAnExplicitTarget()
        {
            var interference = Started(0, 11);
            bool foundHostTarget = false;

            for (double now = 30; now <= 30 * 500; now += 30)
            {
                interference.Tick(now, Roster);
                if (interference.Kind == MonsterInterferenceKind.SinglePlayerDirectionBlock &&
                    interference.HasTarget &&
                    interference.Target == 0)
                {
                    foundHostTarget = true;
                    break;
                }

                interference.Tick(now + HostMonsterInterference.DefaultDurationSeconds, Roster);
            }

            Assert.That(foundHostTarget, Is.True, "Client ID 0 is a valid Host target, not a missing-target sentinel.");
        }

        [Test]
        public void SinglePlayerEffectBlocksOnlyItsTargetAndBlockedDirection()
        {
            var interference = Started(0, 19);
            double now = 30;
            while (interference.Kind != MonsterInterferenceKind.SinglePlayerDirectionBlock)
            {
                interference.Tick(now, Roster);
                if (interference.Kind == MonsterInterferenceKind.SinglePlayerDirectionBlock)
                    break;
                interference.Tick(now + HostMonsterInterference.DefaultDurationSeconds, Roster);
                now += HostMonsterInterference.DefaultIntervalSeconds;
            }

            ulong target = interference.Target;
            ulong other = Roster.First(id => id != target);
            MonsterTransferDirection opposite = interference.Direction == MonsterTransferDirection.Left
                ? MonsterTransferDirection.Right
                : MonsterTransferDirection.Left;

            Assert.That(interference.BlocksTransfer(target, interference.Direction), Is.True);
            Assert.That(interference.BlocksTransfer(target, opposite), Is.False);
            Assert.That(interference.BlocksTransfer(other, interference.Direction), Is.False);
            Assert.That(interference.BlocksTransfer(target, MonsterTransferDirection.None), Is.False);
        }

        [Test]
        public void AllPlayerEffectAllowsOnlyItsSelectedDirection()
        {
            var interference = Started(0, 23);
            double now = 30;
            while (interference.Kind != MonsterInterferenceKind.AllPlayersDirectionRestriction)
            {
                interference.Tick(now, Roster);
                if (interference.Kind == MonsterInterferenceKind.AllPlayersDirectionRestriction)
                    break;
                interference.Tick(now + HostMonsterInterference.DefaultDurationSeconds, Roster);
                now += HostMonsterInterference.DefaultIntervalSeconds;
            }

            MonsterTransferDirection forbidden = interference.Direction == MonsterTransferDirection.Left
                ? MonsterTransferDirection.Right
                : MonsterTransferDirection.Left;

            Assert.That(interference.HasTarget, Is.False);
            foreach (ulong participant in Roster)
            {
                Assert.That(interference.BlocksTransfer(participant, interference.Direction), Is.False);
                Assert.That(interference.BlocksTransfer(participant, forbidden), Is.True);
            }

            interference.Tick(now + HostMonsterInterference.DefaultDurationSeconds, Roster);
            Assert.That(interference.Active, Is.False);
            Assert.That(interference.BlocksTransfer(Roster[0], forbidden), Is.False);
        }

        [Test]
        public void LongHostStallDoesNotReplayMissedEffectsInABurst()
        {
            var interference = Started(0);

            Assert.That(interference.Tick(95, Roster), Is.True);
            Assert.That(interference.Sequence, Is.EqualTo(1));
            Assert.That(interference.Tick(96, Roster), Is.False);
            Assert.That(interference.Sequence, Is.EqualTo(1));

            Assert.That(interference.Tick(103, Roster), Is.True);
            Assert.That(interference.Active, Is.False);
            Assert.That(interference.Tick(124.9, Roster), Is.False);
            Assert.That(interference.Tick(125, Roster), Is.True);
            Assert.That(interference.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void BeginClearsPreviousRoundAndEmptyRosterDoesNotStart()
        {
            var interference = Started(0);
            interference.Tick(30, Roster);

            interference.Begin(500);

            Assert.That(interference.Active, Is.False);
            Assert.That(interference.Sequence, Is.Zero);
            Assert.That(interference.Kind, Is.EqualTo(MonsterInterferenceKind.None));
            Assert.That(interference.Direction, Is.EqualTo(MonsterTransferDirection.None));
            Assert.That(interference.HasTarget, Is.False);
            Assert.That(interference.Tick(600, Array.Empty<ulong>()), Is.False);
            Assert.That(interference.Sequence, Is.Zero);
        }

        [TestCase(0, 8)]
        [TestCase(30, 0)]
        [TestCase(30, 31)]
        [TestCase(double.NaN, 8)]
        [TestCase(30, double.PositiveInfinity)]
        public void InvalidTimingIsRejected(double interval, double duration) =>
            Assert.That(
                () => new HostMonsterInterference(interval, duration, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());

        [Test]
        public void StableTagsMatchEveryKnownKind()
        {
            Assert.That(
                MonsterInterferenceTags.FromKind(MonsterInterferenceKind.None),
                Is.EqualTo("none"));
            Assert.That(
                MonsterInterferenceTags.FromKind(MonsterInterferenceKind.SinglePlayerDirectionBlock),
                Is.EqualTo("single-player-direction-block"));
            Assert.That(
                MonsterInterferenceTags.FromKind(MonsterInterferenceKind.AllPlayersDirectionRestriction),
                Is.EqualTo("all-players-direction-restriction"));
        }
    }
}
