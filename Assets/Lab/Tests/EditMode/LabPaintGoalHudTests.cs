using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaintGoalHudTests
    {
        [TestCase(.7999f, "PAINT 79.9% / 80%")]
        [TestCase(.8f, "PAINT 80.0% / 80%")]
        [TestCase(0f, "PAINT 0.0% / 80%")]
        [TestCase(-.1f, "PAINT 0.0% / 80%")]
        [TestCase(1.2f, "PAINT 100.0% / 80%")]
        [TestCase(float.NaN, "PAINT 0.0% / 80%")]
        [TestCase(float.PositiveInfinity, "PAINT 0.0% / 80%")]
        public void CurrentPaintPercentageIsFlooredToOneDecimal(float coverage, string expected)
            => Assert.That(LabHud.FormatPaintGoalProgress(coverage, 80), Is.EqualTo(expected));

        [TestCase(.4f, 80, .5f)]
        [TestCase(.8f, 80, 1f)]
        [TestCase(.999f, 100, .999f)]
        [TestCase(.01f, 1, 1f)]
        [TestCase(1f, 80, 1f)]
        [TestCase(-.2f, 80, 0f)]
        [TestCase(float.NaN, 80, 0f)]
        public void FillUsesTheRoundsActualGoalRatherThanTheFormerConstant(float coverage, int goal, float expected)
            => Assert.That(LabHud.PaintGoalFill(coverage, goal), Is.EqualTo(expected).Within(.00001f));

        [Test]
        public void SnapshotGoalOverridesConfigAndMissingLegacyGoalUsesConfig()
        {
            var tuning = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = tuning.CaptureDeveloperValues();
                values[87] = 45f;
                Assert.That(tuning.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var snapshot = new LabSnapshot { paintGoalPercent = 65 };
                Assert.That(LabHud.PaintGoalPercentFor(snapshot, tuning), Is.EqualTo(65));
                Assert.That(LabHud.FormatPaintGoalProgress(.5f, LabHud.PaintGoalPercentFor(snapshot, tuning)),
                    Is.EqualTo("PAINT 50.0% / 65%"));
                snapshot.paintGoalPercent = 0;
                Assert.That(LabHud.PaintGoalPercentFor(snapshot, tuning), Is.EqualTo(45));
                Assert.That(LabHud.PaintGoalPercentFor(null, tuning), Is.EqualTo(45));
                Assert.That(LabHud.PaintGoalPercentFor(new LabSnapshot { paintGoalPercent = 100 }, null), Is.EqualTo(100));
                Assert.That(LabHud.PaintGoalPercentFor(new LabSnapshot(), null), Is.EqualTo(80));
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        [Test]
        public void PaintAndCrackUseDistinctTitles()
        {
            Assert.That(LabHud.GourdGoalTitle(LabGourdMode.Paint), Is.EqualTo("GOAL"));
            Assert.That(LabHud.GourdGoalTitle(LabGourdMode.Crack), Is.EqualTo("YOKAI HP"));
        }
    }
}
