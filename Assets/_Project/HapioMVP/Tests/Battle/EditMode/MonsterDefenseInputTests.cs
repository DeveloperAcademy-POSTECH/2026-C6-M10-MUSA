using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>#28 local two-hand defense input. The Host still judges every report.</summary>
    public sealed class MonsterDefenseInputTests
    {
        // Screen pixels: the orb area below 400, the battle area above it, the TEAM HP row at its bottom.
        private static readonly Rect LeftZone = new Rect(0, 400, 195, 70);
        private static readonly Rect RightZone = new Rect(195, 400, 195, 70);

        [Test]
        public void TouchesCountOnlyInTheZoneWhereTheyBegan()
        {
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 430), LeftZone, RightZone), Is.EqualTo(DefenseZone.Left));
            Assert.That(MonsterDefenseInput.Classify(new Vector2(350, 430), LeftZone, RightZone), Is.EqualTo(DefenseZone.Right));
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 600), LeftZone, RightZone), Is.EqualTo(DefenseZone.None), "touching the monster area");
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 200), LeftZone, RightZone), Is.EqualTo(DefenseZone.None), "orb drags and throws begin below");
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 430), Rect.zero, Rect.zero), Is.EqualTo(DefenseZone.None), "no zones yet");
        }

        [Test]
        public void ScenesWithoutZonesSplitTheBattleAreaInHalf()
        {
            MonsterDefenseInput.SplitHalves(new Rect(0, 400, 390, 444), out var left, out var right);
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 600), left, right), Is.EqualTo(DefenseZone.Left));
            Assert.That(MonsterDefenseInput.Classify(new Vector2(350, 600), left, right), Is.EqualTo(DefenseZone.Right));
            Assert.That(MonsterDefenseInput.Classify(new Vector2(195, 600), left, right), Is.EqualTo(DefenseZone.Right), "the shared edge belongs to one side");
            Assert.That(MonsterDefenseInput.Classify(new Vector2(40, 200), left, right), Is.EqualTo(DefenseZone.None));
        }

        [Test]
        public void TwoSecondsOfBothHandsDuringTheWarningReachesTheStanceOnce()
        {
            var hold = new DefenseHoldTracker();
            Assert.That(Step(hold, 1, true, true, 1.0), Is.False);
            Assert.That(hold.Holding, Is.True);
            Assert.That(Step(hold, 1, true, true, 1.0), Is.True, "reached 2 s: one report, one haptic");
            Assert.That(hold.Holding, Is.False);
            Assert.That(hold.Reported, Is.True, "stance stays until the next attack");
            Assert.That(Step(hold, 1, true, true, 1.0), Is.False, "one report per attack");
            Assert.That(Step(hold, 1, true, false, 1.0), Is.False);
            Assert.That(hold.Reported, Is.True, "lifting after the stance keeps it");
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
        public void StanceGlowIsSteadyLightGreenAndTheWarningStillPulses()
        {
            Assert.That(MonsterAttackWarning.PulseAlpha(.2, 2.5f, .35f), Is.EqualTo(.35f).Within(1e-5), "plain warning pulses");
            var go = new GameObject("warning", typeof(RectTransform));
            try
            {
                var edge = new GameObject("edge", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                edge.transform.SetParent(go.transform, false);
                edge.color = new Color(1f, .08f, .05f, .85f);
                var warning = go.AddComponent<MonsterAttackWarning>();
                var serialized = new UnityEditor.SerializedObject(warning);
                var edges = serialized.FindProperty("edges"); edges.arraySize = 1;
                edges.GetArrayElementAtIndex(0).objectReferenceValue = edge;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                warning.Show(.2, WarningLook.Holding);
                Assert.That(edge.color, Is.EqualTo(new Color(1f, .08f, .05f, .85f)), "holding: steady red");
                warning.Show(.2, WarningLook.Stance);
                Assert.That(edge.color.g, Is.GreaterThan(edge.color.r), "stance: green");
                Assert.That(edge.color.a, Is.EqualTo(.85f).Within(1e-5), "stance: steady");
                warning.Show(.2, WarningLook.Pulse);
                Assert.That(edge.color.r, Is.EqualTo(1f), "the next warning is red again");
                Assert.That(edge.color.a, Is.EqualTo(.85f * .35f).Within(1e-5));
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static bool Step(DefenseHoldTracker hold, int attack, bool warning, bool bothHeld, double delta) =>
            hold.Update(1, attack, warning, bothHeld, delta, 2.0);
    }
}
