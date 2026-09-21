using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>#28 local two-hand defense input. The Host still judges every report.</summary>
    public sealed class MonsterDefenseInputTests
    {
        private static readonly Rect Upper = new Rect(0, 400, 390, 444);

        [Test]
        public void TouchesCountOnlyByTheUpperHalfWhereTheyBegan()
        {
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 600), Upper), Is.EqualTo(DefenseZone.Left));
            Assert.That(MonsterDefenseInput.Classify(new Vector2(350, 600), Upper), Is.EqualTo(DefenseZone.Right));
            Assert.That(MonsterDefenseInput.Classify(new Vector2(195, 600), Upper), Is.EqualTo(DefenseZone.Right), "the center line belongs to one side");
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 200), Upper), Is.EqualTo(DefenseZone.None), "orb drags and throws begin below");
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 600), Rect.zero), Is.EqualTo(DefenseZone.None), "no battle view yet");
        }

        [Test]
        public void TwoSecondsOfBothHandsDuringTheWarningReportsOnce()
        {
            var hold = new DefenseHoldTracker();
            Assert.That(Step(hold, 1, true, true, 1.0), Is.False);
            Assert.That(hold.Holding, Is.True);
            Assert.That(Step(hold, 1, true, true, 1.0), Is.True, "reached 2 s");
            Assert.That(hold.Holding, Is.False);
            Assert.That(Step(hold, 1, true, true, 1.0), Is.False, "one report per attack");
            Assert.That(Step(hold, 2, true, true, 2.0), Is.True, "the next attack is a new defense");
        }

        [Test]
        public void LiftingEitherHandOrLeavingTheWarningRestartsTheHold()
        {
            var hold = new DefenseHoldTracker();
            Step(hold, 1, true, true, 1.5);
            Assert.That(Step(hold, 1, true, false, .1), Is.False);
            Assert.That(hold.HeldSeconds, Is.Zero, "one hand lifted");
            Step(hold, 1, true, true, 1.5);
            Assert.That(Step(hold, 1, false, true, .1), Is.False);
            Assert.That(hold.HeldSeconds, Is.Zero, "the warning ended");
            Assert.That(Step(hold, 1, true, true, 1.99), Is.False);
        }

        [Test]
        public void NoAttackOrANewRoundNeverCarriesAHold()
        {
            var hold = new DefenseHoldTracker();
            Assert.That(Step(hold, 0, true, true, 5), Is.False, "no attack yet");
            Step(hold, 1, true, true, 1.5);
            Assert.That(hold.Update(2, 1, true, true, 1.0, 2.0), Is.False, "same attack number in a new round starts over");
            Assert.That(hold.HeldSeconds, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void HapticCuesMarkTheStartEveryHalfSecondAndCompletion()
        {
            var hold = new DefenseHoldTracker();
            var cues = new System.Collections.Generic.List<DefenseHoldCue>();
            for (int frame = 0; frame < 9; frame++) { Step(hold, 1, true, true, .25); cues.Add(hold.Cue); }
            Assert.That(cues, Is.EqualTo(new[]
            {
                DefenseHoldCue.Started, DefenseHoldCue.Progress, DefenseHoldCue.None, DefenseHoldCue.Progress, DefenseHoldCue.None,
                DefenseHoldCue.Progress, DefenseHoldCue.None, DefenseHoldCue.Completed, DefenseHoldCue.None
            }), "0.25 start / 0.5 / 1.0 / 1.5 ticks / 2.0 done / then silent");

            Step(hold, 2, true, true, .3);
            Assert.That(hold.Cue, Is.EqualTo(DefenseHoldCue.Started), "next attack");
            Step(hold, 2, true, false, .3);
            Assert.That(hold.Cue, Is.EqualTo(DefenseHoldCue.None), "no tap when a hand lifts");
            Step(hold, 2, true, true, .3);
            Assert.That(hold.Cue, Is.EqualTo(DefenseHoldCue.Started), "holding again starts over");
        }

        private static bool Step(DefenseHoldTracker hold, int attack, bool warning, bool bothHeld, double delta) =>
            hold.Update(1, attack, warning, bothHeld, delta, 2.0);
    }
}
