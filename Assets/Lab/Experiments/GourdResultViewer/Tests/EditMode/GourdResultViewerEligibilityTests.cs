using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Experiments.Tests
{
    public sealed class GourdResultViewerEligibilityTests
    {
        [Test]
        public void OnlyACompleteCommittedVictoryIsEligible()
        {
            LabPaintSurface map = CompletedMap();
            Assert.That(GourdResultViewerExperiment.IsEligible(Snapshot(map), map), Is.True);
            Assert.That(GourdResultViewerExperiment.IsEligible(null, map), Is.False);
            Assert.That(GourdResultViewerExperiment.IsEligible(Snapshot(map), null), Is.False);
        }

        [TestCase("phase")]
        [TestCase("covered")]
        [TestCase("cell-count")]
        [TestCase("columns")]
        [TestCase("rows")]
        [TestCase("revision")]
        [TestCase("paint-count")]
        [TestCase("incomplete-map")]
        public void AnyResultMapMismatchPreventsTheViewer(string mismatch)
        {
            LabPaintSurface map = CompletedMap();
            LabSnapshot snapshot = Snapshot(map);
            switch (mismatch)
            {
                case "phase": snapshot.phase = LabPhase.Defeat; break;
                case "covered": snapshot.paintCoveredCount--; break;
                case "cell-count": snapshot.paintCellCount++; break;
                case "columns": snapshot.paintColumns++; break;
                case "rows": snapshot.paintRows++; break;
                case "revision": snapshot.paintRevision++; break;
                case "paint-count": snapshot.paintCount++; break;
                case "incomplete-map":
                    map = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0 }, 10);
                    map.RegisterHit(Vector3.forward, Color.red, 1);
                    snapshot = Snapshot(map);
                    break;
            }
            Assert.That(GourdResultViewerExperiment.IsEligible(snapshot, map), Is.False, mismatch);
        }

        [TestCase(LabPhase.Idle)]
        [TestCase(LabPhase.Playing)]
        [TestCase(LabPhase.Defeat)]
        public void FullPaintWithoutVictoryDoesNotEnableEntry(LabPhase phase)
        {
            LabPaintSurface map = CompletedMap();
            LabSnapshot snapshot = Snapshot(map);
            snapshot.phase = phase;
            Assert.That(GourdResultViewerExperiment.IsEligible(snapshot, map), Is.False);
        }

        [Test]
        public void AResultNeedsASessionRoundAndFixedHundredPercentGoal()
        {
            LabPaintSurface map = CompletedMap();
            LabSnapshot snapshot = Snapshot(map);
            snapshot.sessionId = null;
            Assert.That(GourdResultViewerExperiment.IsEligible(snapshot, map), Is.False);
            snapshot = Snapshot(map); snapshot.round = 0;
            Assert.That(GourdResultViewerExperiment.IsEligible(snapshot, map), Is.False);
            snapshot = Snapshot(map); snapshot.paintGoalPercent = 80;
            Assert.That(GourdResultViewerExperiment.IsEligible(snapshot, map), Is.False);
            snapshot = Snapshot(map); snapshot.paintCellCount = snapshot.paintCoveredCount = 0;
            Assert.That(GourdResultViewerExperiment.IsEligible(snapshot, map), Is.False);
        }

        private static LabPaintSurface CompletedMap()
        {
            var map = new LabPaintSurface(new LabPaintGrowthSettings
                { Resolution = 0, InitialPercent = 100 }, 42);
            Assert.That(map.RegisterHit(Vector3.forward, new Color(.1f, .7f, .2f, 1), 9), Is.True);
            return map;
        }

        private static LabSnapshot Snapshot(LabPaintSurface map) => new LabSnapshot
        {
            sessionId = "viewer-eligibility", round = 1, phase = LabPhase.Victory,
            paintGoalPercent = 100, paintCoverage = map.Coverage,
            paintColumns = map.Columns, paintRows = map.Rows, paintRevision = map.Revision,
            paintCellCount = map.CellCount, paintCoveredCount = map.CoveredCount, paintCount = map.PaintCount
        };
    }
}
