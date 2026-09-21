using NUnit.Framework;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>#28 presentation rules only. Target, timing, and result stay in the Host snapshot.</summary>
    public sealed class MonsterAttackPresentationTests
    {
        private static readonly ulong[] Roster = { 0, 41, 57, 88, 99 };

        [Test]
        public void ClawImpactLandsWhenTheWarningEnds()
        {
            Assert.That(MonsterMotion.ClawImpactSeconds, Is.EqualTo(73f / 30f), "Slash_Impact frame 74 of 1..136 at 30 fps");
            double start = MonsterAttackPresenter.ClawStartsAt(20, 23, MonsterMotion.ClawImpactSeconds);
            Assert.That(start + MonsterMotion.ClawImpactSeconds, Is.EqualTo(23).Within(1e-6));
            Assert.That(start, Is.EqualTo(20.5667).Within(.001));
            Assert.That(MonsterAttackPresenter.ClawStartsAt(20, 21, MonsterMotion.ClawImpactSeconds), Is.EqualTo(20), "a short warning starts at once");
        }

        [Test]
        public void TheMonsterFacesTheTargetsSeat()
        {
            Assert.That(MonsterAttackPresenter.TargetYaw(Roster, 0), Is.EqualTo(0f));
            Assert.That(MonsterAttackPresenter.TargetYaw(Roster, 57), Is.EqualTo(144f));
            Assert.That(MonsterAttackPresenter.TargetYaw(new ulong[] { 0, 41, 57 }, 57), Is.EqualTo(240f));
            Assert.That(MonsterAttackPresenter.TargetYaw(Roster, 12345), Is.Null, "not a participant");
            Assert.That(MonsterAttackPresenter.TargetYaw(new ulong[] { 0 }, 0), Is.Null, "no multiparty roster");
            Assert.That(MonsterAttackPresenter.TargetYaw(null, 0), Is.Null);
        }

        [Test]
        public void OnlyTheTargetIsWarnedAndOnlyDuringTheWarning()
        {
            var state = Attacking();
            Assert.That(MonsterAttackPresenter.Warns(state, 41, 21), Is.True);
            Assert.That(MonsterAttackPresenter.Warns(state, 57, 21), Is.False, "another player");
            Assert.That(MonsterAttackPresenter.Warns(state, 41, 23), Is.False, "warning over");
            state.phase = BattlePhase.Defeat.ToString(); state.attackActive = false;
            Assert.That(MonsterAttackPresenter.Warns(state, 41, 21), Is.False, "round ended");
        }

        [Test]
        public void TheAttackIsShownUntilTheClawRecoversUnlessTheRoundEndedFirst()
        {
            var state = Attacking();
            double recovered = 23d + MonsterMotion.ClawAttackSeconds - MonsterMotion.ClawImpactSeconds;
            Assert.That(MonsterAttackPresenter.Presents(state, 20), Is.True);
            state.attackActive = false; state.attackResolvedSequence = 1; state.attackResult = (int)MonsterAttackResult.Defended;
            Assert.That(MonsterAttackPresenter.Presents(state, recovered - .01), Is.True, "resolved attacks finish their claw");
            Assert.That(MonsterAttackPresenter.Presents(state, recovered), Is.False, "then the monster faces forward");
            state = Attacking(); state.attackActive = false; state.phase = BattlePhase.Victory.ToString();
            Assert.That(MonsterAttackPresenter.Presents(state, 21), Is.False, "cancelled by the round end");
            Assert.That(MonsterAttackPresenter.Presents(new BattleSnapshot(), 21), Is.False, "no attack yet");
        }

        [Test]
        public void WarningStartsBrightAndPulses()
        {
            Assert.That(MonsterAttackWarning.PulseAlpha(0, 2.5f, .35f), Is.EqualTo(1f).Within(1e-5));
            Assert.That(MonsterAttackWarning.PulseAlpha(.2, 2.5f, .35f), Is.EqualTo(.35f).Within(1e-5), "half a pulse");
            Assert.That(MonsterAttackWarning.PulseAlpha(.4, 2.5f, .35f), Is.EqualTo(1f).Within(1e-5));
        }

        private static BattleSnapshot Attacking() => new BattleSnapshot
        {
            phase = BattlePhase.Playing.ToString(), roundId = 1, startedAt = 0, attackSequence = 1, attackTarget = 41,
            attackActive = true, attackWarningStartsAt = 20, attackWarningEndsAt = 23
        };
    }
}
