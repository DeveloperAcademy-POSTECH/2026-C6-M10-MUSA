using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaintGoalHudTests
    {
        [TestCase(.7999f, "PAINT 79.9% / 100%")]
        [TestCase(.8f, "PAINT 80.0% / 100%")]
        [TestCase(.9f, "PAINT 90.0% / 100%")]
        [TestCase(.999f, "PAINT 99.9% / 100%")]
        [TestCase(.99999f, "PAINT 99.9% / 100%")]
        [TestCase(0f, "PAINT 0.0% / 100%")]
        [TestCase(-.1f, "PAINT 0.0% / 100%")]
        [TestCase(1.2f, "PAINT 100.0% / 100%")]
        [TestCase(float.NaN, "PAINT 0.0% / 100%")]
        [TestCase(float.PositiveInfinity, "PAINT 0.0% / 100%")]
        public void CurrentPaintPercentageIsFlooredToOneDecimalWithTheFixedGoal(float coverage, string expected)
            => Assert.That(LabHud.FormatPaintGoalProgress(coverage, 80), Is.EqualTo(expected));

        [TestCase(.4f, 80, .4f)]
        [TestCase(.8f, 80, .8f)]
        [TestCase(.999f, 100, .999f)]
        [TestCase(.01f, 1, .01f)]
        [TestCase(1f, 80, 1f)]
        [TestCase(-.2f, 80, 0f)]
        [TestCase(float.NaN, 80, 0f)]
        public void FillAlwaysMeasuresTheFullSurfaceEvenForLegacyGoalArguments(float coverage, int goal, float expected)
            => Assert.That(LabHud.PaintGoalFill(coverage, goal), Is.EqualTo(expected).Within(.00001f));

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(80)]
        [TestCase(100)]
        public void SnapshotAndLegacyConfigGoalsAlwaysResolveToOneHundred(int legacyGoal)
        {
            var tuning = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"paintVictoryPercent\":80}", tuning);
                var snapshot = new LabSnapshot { paintGoalPercent = legacyGoal };
                Assert.That(LabHud.PaintGoalPercentFor(snapshot, tuning), Is.EqualTo(100));
                Assert.That(LabHud.FormatPaintGoalProgress(.5f, LabHud.PaintGoalPercentFor(snapshot, tuning)),
                    Is.EqualTo("PAINT 50.0% / 100%"));
                Assert.That(LabHud.PaintGoalPercentFor(null, tuning), Is.EqualTo(100));
                Assert.That(LabHud.PaintGoalPercentFor(snapshot, null), Is.EqualTo(100));
                Assert.That(LabHud.PaintGoalPercentFor(null, null), Is.EqualTo(100));
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        [TestCase(2047, 2048, "PAINT 99.9% / 100%")]
        [TestCase(8191, 8192, "PAINT 99.9% / 100%")]
        [TestCase(32767, 32768, "PAINT 99.9% / 100%")]
        [TestCase(32768, 32768, "PAINT 100.0% / 100%")]
        [TestCase(8192, 32768, "PAINT 25.0% / 100%")]
        [TestCase(-1, 32768, "PAINT 0.0% / 100%")]
        [TestCase(40000, 32768, "PAINT 100.0% / 100%")]
        [TestCase(int.MaxValue - 1, int.MaxValue, "PAINT 99.9% / 100%")]
        public void CellCountsTakePrecedenceAndCannotDisplayPrematureCompletion(int covered, int total, string expected)
        {
            var snapshot = new LabSnapshot
            {
                paintCoverage = covered < total ? 1f : 0f,
                paintCoveredCount = covered,
                paintCellCount = total
            };
            Assert.That(LabHud.FormatPaintGoalProgress(snapshot), Is.EqualTo(expected));
            Assert.That(LabHud.FormatPaintPercentage(snapshot),
                Is.EqualTo(expected.Replace("PAINT ", "").Replace(" / 100%", "")));
            float fill = LabHud.PaintGoalFill(snapshot);
            if (covered < total) Assert.That(fill, Is.LessThan(1f));
            else Assert.That(fill, Is.EqualTo(1f));
            Assert.That(fill, Is.InRange(0f, 1f));
        }

        [Test]
        public void MissingCellCountsUseTheLegacyCoverageAndNullSnapshotIsEmpty()
        {
            var snapshot = new LabSnapshot { paintCoverage = .5f, paintCellCount = 0 };
            Assert.That(LabHud.FormatPaintGoalProgress(snapshot), Is.EqualTo("PAINT 50.0% / 100%"));
            Assert.That(LabHud.PaintGoalFill(snapshot), Is.EqualTo(.5f));
            snapshot.paintCellCount = -1;
            Assert.That(LabHud.FormatPaintGoalProgress(snapshot), Is.EqualTo("PAINT 50.0% / 100%"));
            Assert.That(LabHud.FormatPaintGoalProgress(null), Is.EqualTo("PAINT 0.0% / 100%"));
            Assert.That(LabHud.PaintGoalFill(null), Is.Zero);
        }

        [Test]
        public void PaintAndCrackUseDistinctTitles()
        {
            Assert.That(LabHud.GourdGoalTitle(LabGourdMode.Paint), Is.EqualTo("GOAL"));
            Assert.That(LabHud.GourdGoalTitle(LabGourdMode.Crack), Is.EqualTo("YOKAI HP"));
        }
    }
}
