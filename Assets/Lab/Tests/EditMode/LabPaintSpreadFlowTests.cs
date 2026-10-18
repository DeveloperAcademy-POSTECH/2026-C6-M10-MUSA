using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaintSpreadFlowTests
    {
        private static LabPaintGrowthSettings Settings(int resolution = 0) => new LabPaintGrowthSettings
        {
            Resolution = resolution, Style = 1, InitialPercent = 2f, RepeatPercent = 37f,
            GrowthPercentPerSecond = 6f, Raggedness = .55f, FlowRatio = .35f,
            DripWidthDegrees = 3f, DripLengthDegrees = 60f, GravityWeight = 3f
        };

        // Captured by executing the unchanged a26811b2 surface and this implementation
        // against the same fixture. Preserve the historical RNG order and opaque RGBA map.
        [TestCase(0, "4273982525467f9bc010a2268509c7fbdd39cbbc78b0c34a2593cd7fb9b98bab")]
        [TestCase(1, "707515ff7b960a3bf0a7261a1e0e49d92ffc347c3394c2fe51681ac118b9ee99")]
        [TestCase(2, "6e4a8fca6a46d335e91ef789e80e1891dc35df6a59513a0553d234a37dfa3b23")]
        public void OriginalStylePreservesTheHistoricalMainMapHashAtEveryResolution(int resolution, string expectedHash)
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = resolution, Style = 0 }, 198u);
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = new Vector3(Mathf.Sin(i * 1.7f), .6f - i * .17f, Mathf.Cos(i * 1.7f));
                Color color = i % 2 == 0 ? Color.red : Color.blue;
                surface.RegisterHit(point, color, 1);
                surface.RegisterHit(point, Color.green, 2);
                surface.Advance(.17d);
            }
            surface.Advance(4d);
            Assert.That(LabPaintPublisher.Hash(Pixels(surface)), Is.EqualTo(expectedHash));
        }

        [Test]
        public void LegacyIgnoresAllNewStyleControlsAndFlowDirectionWithoutChangingTheMapOrCredits()
        {
            var ordinary = Settings();
            ordinary.Style = 0;
            var changed = Settings();
            changed.Style = 0;
            changed.Raggedness = 1f;
            changed.FlowRatio = 1f;
            changed.DripWidthDegrees = 12f;
            changed.DripLengthDegrees = 135f;
            changed.GravityWeight = 8f;
            var first = new LabPaintSurface(ordinary, 729u);
            var second = new LabPaintSurface(changed, 729u);
            Assert.That(second.SetFlowDirection(Vector3.right), Is.True);
            foreach (LabPaintSurface surface in new[] { first, second })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 7);
                surface.RegisterHit(Vector3.forward, Color.blue, 9);
                surface.Advance(1d);
                surface.RegisterHit(Vector3.up, Color.green, 11);
                surface.Advance(.5d);
                surface.RegisterHit(Vector3.forward, Color.yellow, 12);
                surface.Advance(2d);
            }
            Assert.That(Pixels(second), Is.EqualTo(Pixels(first)));
            Assert.That(second.PendingCoverage, Is.EqualTo(first.PendingCoverage));
            Assert.That(second.Revision, Is.EqualTo(first.Revision));
            Assert.That(second.LastPainter, Is.EqualTo(first.LastPainter));
        }

        [Test]
        public void DirectionInputNormalizesAtomicallyAndCannotAdvanceOrRepaintTheSurface()
        {
            var surface = new LabPaintSurface(Settings(), 21);
            Assert.That(surface.FlowDirection, Is.EqualTo(Vector3.down));
            surface.RegisterHit(Vector3.forward, Color.red, 3);
            var before = Pixels(surface);
            uint revision = surface.Revision;
            double pending = surface.PendingCoverage;
            Assert.That(surface.SetFlowDirection(new Vector3(-3, -4, 0)), Is.True);
            Assert.That(Vector3.Distance(surface.FlowDirection, new Vector3(-.6f, -.8f, 0)), Is.LessThan(.00001f));
            foreach (Vector3 invalid in new[] { Vector3.zero, Vector3.one * .00001f,
                new Vector3(float.NaN, 0, 0), new Vector3(0, float.PositiveInfinity, 0),
                new Vector3(float.MaxValue, float.MaxValue, 0) })
            {
                Assert.That(surface.SetFlowDirection(invalid), Is.False);
                Assert.That(Vector3.Distance(surface.FlowDirection, new Vector3(-.6f, -.8f, 0)), Is.LessThan(.00001f));
            }
            Assert.That(Pixels(surface), Is.EqualTo(before));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.PendingCoverage, Is.EqualTo(pending));
            surface.Clear();
            Assert.That(surface.FlowDirection, Is.EqualTo(Vector3.down));
            surface.SetFlowDirection(Vector3.right);
            surface.Reset(Settings(), 22);
            Assert.That(surface.FlowDirection, Is.EqualTo(Vector3.down));
        }

        [TestCase("Style", -1f)]
        [TestCase("Style", 2f)]
        [TestCase("Raggedness", -.1f)]
        [TestCase("Raggedness", 1.1f)]
        [TestCase("Raggedness", float.NaN)]
        [TestCase("FlowRatio", -.1f)]
        [TestCase("FlowRatio", 1.1f)]
        [TestCase("FlowRatio", float.PositiveInfinity)]
        [TestCase("DripWidthDegrees", .49f)]
        [TestCase("DripWidthDegrees", 12.1f)]
        [TestCase("DripWidthDegrees", float.NaN)]
        [TestCase("DripLengthDegrees", 4.9f)]
        [TestCase("DripLengthDegrees", 135.1f)]
        [TestCase("DripLengthDegrees", float.NegativeInfinity)]
        [TestCase("GravityWeight", .9f)]
        [TestCase("GravityWeight", 8.1f)]
        [TestCase("GravityWeight", float.NaN)]
        public void InvalidSettingsCannotPartiallyResetAnExistingMap(string fieldName, float value)
        {
            var surface = new LabPaintSurface(Settings(), 91);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            surface.SetFlowDirection(Vector3.left);
            var before = Pixels(surface);
            uint revision = surface.Revision;
            double pending = surface.PendingCoverage;
            LabPaintGrowthSettings invalid = Settings(2);
            var field = typeof(LabPaintGrowthSettings).GetField(fieldName);
            field.SetValue(invalid, field.FieldType == typeof(int) ? (object)(int)value : value);
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.Reset(invalid, 999));
            Assert.That(surface.Columns, Is.EqualTo(64));
            Assert.That(Pixels(surface), Is.EqualTo(before));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.PendingCoverage, Is.EqualTo(pending));
            Assert.That(surface.FlowDirection, Is.EqualTo(Vector3.left));
        }

        [TestCase(0, 0)]
        [TestCase(0, 1)]
        [TestCase(0, 2)]
        [TestCase(0, 3)]
        [TestCase(2, 0)]
        [TestCase(2, 1)]
        [TestCase(2, 2)]
        [TestCase(2, 3)]
        public void InitialPatchIsCompactConnectedAndSpendsOnlyItsInitialAreaAtSeamsAndPoles(int resolution, int position)
        {
            Vector3 direction = new[] { Vector3.forward, Vector3.left, Vector3.up, Vector3.down }[position];
            var surface = new LabPaintSurface(Settings(resolution), 771);
            Assert.That(surface.RegisterHit(direction, Color.red, 7), Is.True);
            Assert.That(surface.CoveredCount, Is.EqualTo((int)decimal.Floor(2m * surface.CellCount / 100m)));
            AssertConnected(surface);
            var pixels = Pixels(surface);
            float maximumArc = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0) continue;
                Assert.That(pixels[i], Is.EqualTo((Color32)Color.red));
                maximumArc = Mathf.Max(maximumArc, Vector3.Angle(direction, Direction(surface, i)));
            }
            Assert.That(maximumArc, Is.LessThan(50f), "The initial two-percent patch must remain near the actual hit.");
            Assert.That(surface.RegionCount, Is.EqualTo(1));
        }

        [Test]
        public void RaggednessChangesInitialGeometryWithoutChangingAreaOrConnectivity()
        {
            var smooth = Settings(2);
            smooth.Raggedness = 0;
            var rough = Settings(2);
            rough.Raggedness = 1;
            var first = new LabPaintSurface(smooth, 88);
            var second = new LabPaintSurface(rough, 88);
            first.RegisterHit(Vector3.forward, Color.red, 1);
            second.RegisterHit(Vector3.forward, Color.red, 1);
            Assert.That(second.CoveredCount, Is.EqualTo(first.CoveredCount));
            Assert.That(LabPaintPublisher.Hash(Pixels(second)), Is.Not.EqualTo(LabPaintPublisher.Hash(Pixels(first))));
            AssertConnected(first);
            AssertConnected(second);
        }

        [Test]
        public void ConstantDirectionAndSeedHaveIdenticalResultsAcrossTimePartitionsAndRegionMerges()
        {
            var first = new LabPaintSurface(Settings(), 729);
            var second = new LabPaintSurface(Settings(), 729);
            foreach (LabPaintSurface surface in new[] { first, second })
            {
                surface.SetFlowDirection(new Vector3(.6f, -.8f, 0));
                surface.RegisterHit(Vector3.forward, Color.red, 1);
                surface.RegisterHit(Vector3.back, Color.green, 3);
                surface.RegisterHit(Vector3.forward, Color.blue, 2);
                surface.RegisterHit(Vector3.back, Color.yellow, 4);
            }
            first.Advance(6d);
            for (int i = 0; i < 60; i++) second.Advance(.1d);
            Assert.That(Pixels(second), Is.EqualTo(Pixels(first)));
            Assert.That(second.PendingCoverage, Is.EqualTo(first.PendingCoverage));
            Assert.That(second.Revision, Is.EqualTo(first.Revision));
            Assert.That(second.LastPainter, Is.EqualTo(first.LastPainter));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void FullyFlowingWideShortDripsStillReachTheLastCellAndCompleteFromOneStationaryHit(int resolution)
        {
            var settings = Settings(resolution);
            settings.InitialPercent = .1f;
            settings.RepeatPercent = 100f;
            settings.GrowthPercentPerSecond = 100f;
            settings.FlowRatio = 1f;
            settings.DripWidthDegrees = 12f;
            settings.DripLengthDegrees = 5f;
            settings.GravityWeight = 8f;
            var surface = new LabPaintSurface(settings, 107);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            var original = Pixels(surface);
            surface.RegisterHit(Vector3.forward, Color.blue, 2);
            double untilLast = (surface.CellCount - surface.CoveredCount - 1) / (double)surface.CellCount;
            Assert.That(surface.Advance(untilLast), Is.EqualTo(-1d));
            Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount - 1));
            Assert.That(surface.IsComplete, Is.False);
            surface.SetFlowDirection(Vector3.up);
            Assert.That(surface.Advance(1d / surface.CellCount), Is.GreaterThanOrEqualTo(0d));
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.Coverage, Is.EqualTo(1f));
            Assert.That(surface.PendingCoverage, Is.Zero);
            Assert.That(Pixels(surface).All(pixel => pixel.a == 255), Is.True);
            for (int i = 0; i < original.Length; i++)
                if (original[i].a != 0) Assert.That(Pixels(surface)[i], Is.EqualTo(original[i]));
            Assert.That(surface.LastPainter, Is.EqualTo(2));
        }

        [Test]
        public void NewCellsFollowTheSuppliedLocalGravityWhilePreviouslyPaintedCellsRemainFixed()
        {
            double downHeight = 0, upHeight = 0;
            for (uint seed = 1; seed <= 8; seed++)
            {
                foreach (bool up in new[] { false, true })
                {
                    var settings = Settings();
                    settings.InitialPercent = .1f;
                    settings.RepeatPercent = 100;
                    settings.GrowthPercentPerSecond = 100;
                    settings.FlowRatio = 1;
                    settings.DripWidthDegrees = .5f;
                    settings.DripLengthDegrees = 135;
                    settings.GravityWeight = 8;
                    var surface = new LabPaintSurface(settings, seed);
                    surface.RegisterHit(Vector3.forward, Color.red, 1);
                    Color32[] before = Pixels(surface);
                    surface.SetFlowDirection(up ? Vector3.up : Vector3.down);
                    surface.RegisterHit(Vector3.forward, Color.blue, 2);
                    surface.Advance(80d / surface.CellCount);
                    Color32[] after = Pixels(surface);
                    for (int i = 0; i < after.Length; i++)
                    {
                        if (before[i].a != 0) Assert.That(after[i], Is.EqualTo(before[i]));
                        else if (after[i].a != 0)
                        {
                            if (up) upHeight += Direction(surface, i).y;
                            else downHeight += Direction(surface, i).y;
                        }
                    }
                    AssertConnected(surface);
                }
            }
            Assert.That(downHeight / 640d, Is.LessThan(-.1d));
            Assert.That(upHeight / 640d, Is.GreaterThan(.1d));
        }

        [Test]
        public void BottomCapUsesGeneralSpreadingInsteadOfStartingAnUphillDrip()
        {
            var settings = Settings();
            settings.Raggedness = 0f;
            settings.RepeatPercent = 100f;
            settings.GrowthPercentPerSecond = 100f;
            settings.FlowRatio = 1f;
            settings.DripWidthDegrees = .5f;
            settings.DripLengthDegrees = 135f;
            var surface = new LabPaintSurface(settings, 202);
            surface.RegisterHit(Vector3.down, Color.red, 1);
            Color32[] before = Pixels(surface);
            for (int cell = 0; cell < before.Length; cell++)
                if (before[cell].a != 0)
                    Assert.That(cell / surface.Columns, Is.EqualTo(surface.Rows - 1),
                        "This regression begins with a connected lowest-latitude patch.");
            int count = surface.CoveredCount;
            surface.RegisterHit(Vector3.down, Color.blue, 2);
            surface.Advance(1d / surface.CellCount);
            Assert.That(surface.CoveredCount, Is.EqualTo(count + 1),
                "Blocking a drip cannot stall the ordinary spreading fallback.");
            Assert.That(ReadFlow(surface), Is.Null,
                "A polar cap's lateral/uphill frontier is spreading, not a new descending drip.");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PersistentDripsNeverRunUphillAndEveryReseedHasAnUpstreamPaintedSource(int axis)
        {
            Vector3 gravity = new[] { Vector3.down, Vector3.up, Vector3.right }[axis];
            int checkedStarts = 0, checkedContinuations = 0;
            for (uint seed = 1; seed <= 4; seed++)
            {
                var settings = Settings();
                settings.InitialPercent = .1f;
                settings.RepeatPercent = 100f;
                settings.GrowthPercentPerSecond = 100f;
                settings.FlowRatio = 1f;
                settings.DripWidthDegrees = .5f;
                settings.DripLengthDegrees = 135f;
                var surface = new LabPaintSurface(settings, seed);
                surface.SetFlowDirection(gravity);
                surface.RegisterHit(Vector3.forward, Color.red, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2);
                for (int tick = 0; tick < 200; tick++)
                {
                    object previousFlow = ReadFlow(surface);
                    int previousCell = previousFlow != null ? ReadFlowCell(previousFlow) : -1;
                    Color32[] before = Pixels(surface);
                    surface.Advance(1d / surface.CellCount);
                    object currentFlow = ReadFlow(surface);
                    if (currentFlow == null) continue;
                    int currentCell = ReadFlowCell(currentFlow);
                    if (ReferenceEquals(previousFlow, currentFlow))
                    {
                        if (previousCell == currentCell) continue;
                        checkedContinuations++;
                        float descent = Vector3.Dot(Direction(surface, currentCell)
                            - Direction(surface, previousCell), gravity);
                        Assert.That(descent, Is.GreaterThanOrEqualTo(-.0000011f),
                            "An existing drip cannot climb after reaching the bottom or being redirected.");
                    }
                    else
                    {
                        checkedStarts++;
                        bool upstream = Neighbors(surface, currentCell).Any(cell => cell >= 0
                            && before[cell].a == 255
                            && Vector3.Dot(Direction(surface, currentCell) - Direction(surface, cell), gravity) > .000001f);
                        Assert.That(upstream, Is.True,
                            "Selecting a low absolute frontier alone cannot justify an uphill reseed.");
                    }
                }
            }
            Assert.That(checkedStarts, Is.GreaterThan(0));
            Assert.That(checkedContinuations, Is.GreaterThan(0));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AngularWidthAndMaximumRunLengthChangeShapeWhilePreservingTheSameAreaBudget(bool varyWidth)
        {
            var firstSettings = Settings(2);
            firstSettings.InitialPercent = .1f;
            firstSettings.FlowRatio = 1f;
            firstSettings.GrowthPercentPerSecond = 100f;
            firstSettings.DripWidthDegrees = .5f;
            firstSettings.DripLengthDegrees = 135f;
            var secondSettings = Settings(2);
            secondSettings.InitialPercent = .1f;
            secondSettings.FlowRatio = 1f;
            secondSettings.GrowthPercentPerSecond = 100f;
            secondSettings.DripWidthDegrees = varyWidth ? 12f : .5f;
            secondSettings.DripLengthDegrees = varyWidth ? 135f : 5f;
            var first = new LabPaintSurface(firstSettings, 17);
            var second = new LabPaintSurface(secondSettings, 17);
            foreach (LabPaintSurface surface in new[] { first, second })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2);
                surface.Advance(200d / surface.CellCount);
                AssertConnected(surface);
            }
            Assert.That(second.CoveredCount, Is.EqualTo(first.CoveredCount));
            Assert.That(second.PendingCoverage, Is.EqualTo(first.PendingCoverage));
            Assert.That(LabPaintPublisher.Hash(Pixels(second)), Is.Not.EqualTo(LabPaintPublisher.Hash(Pixels(first))));
        }

        [Test]
        public void FractionalSupplyColorsAndFiniteInkRemainOpaqueWithoutRecoloringOldCells()
        {
            var settings = Settings();
            settings.InitialPercent = .1f;
            settings.RepeatPercent = .1f;
            var surface = new LabPaintSurface(settings, 101);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Color32[] first = Pixels(surface);
            surface.RegisterHit(Vector3.forward, Color.blue, 2);
            surface.Advance(100);
            Color32[] next = Pixels(surface);
            Assert.That(next.Count(pixel => pixel.Equals((Color32)Color.red)), Is.EqualTo(2));
            Assert.That(next.Count(pixel => pixel.Equals((Color32)Color.blue)), Is.EqualTo(2));
            Assert.That(surface.CoveredCount, Is.EqualTo(4));
            Assert.That(surface.LastPainter, Is.EqualTo(2));
            Assert.That(surface.PendingCoverage * surface.CellCount, Is.EqualTo(.096d).Within(.000001d));
            for (int i = 0; i < first.Length; i++)
                if (first[i].a != 0) Assert.That(next[i], Is.EqualTo(first[i]));
            Assert.That(next.All(pixel => pixel.a == 0 || pixel.a == 255), Is.True);
            surface.Advance(1000000d);
            Assert.That(Pixels(surface), Is.EqualTo(next), "No retained flow tip can grow after its finite ink is exhausted.");
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var result = new Color32[surface.CellCount];
            surface.CopyPixels(result);
            return result;
        }

        private static Vector3 Direction(LabPaintSurface surface, int cell)
        {
            int x = cell % surface.Columns, y = cell / surface.Columns;
            float height = 1f - 2f * (y + .5f) / surface.Rows;
            float ring = Mathf.Sqrt(1f - height * height);
            float angle = ((x + .5f) / surface.Columns - .5f) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * ring, height, Mathf.Sin(angle) * ring);
        }

        // Read-only observations distinguish descending flow from the deliberately permitted
        // all-direction completion fallback; the test drives growth only through public methods.
        private static object ReadFlow(LabPaintSurface surface)
        {
            var regions = (IList)typeof(LabPaintSurface).GetField("regions", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(surface);
            return regions[0].GetType().GetField("Flow", BindingFlags.Instance | BindingFlags.Public).GetValue(regions[0]);
        }

        private static int ReadFlowCell(object flow) => (int)flow.GetType()
            .GetField("LastCell", BindingFlags.Instance | BindingFlags.Public).GetValue(flow);

        private static int[] Neighbors(LabPaintSurface surface, int cell)
        {
            int x = cell % surface.Columns, y = cell / surface.Columns;
            return new[] { y * surface.Columns + (x + surface.Columns - 1) % surface.Columns,
                y * surface.Columns + (x + 1) % surface.Columns,
                y > 0 ? cell - surface.Columns : -1,
                y + 1 < surface.Rows ? cell + surface.Columns : -1 };
        }

        private static void AssertConnected(LabPaintSurface surface)
        {
            Color32[] pixels = Pixels(surface);
            int start = Array.FindIndex(pixels, pixel => pixel.a == 255);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            var seen = new HashSet<int> { start };
            var pending = new Queue<int>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                int cell = pending.Dequeue(), x = cell % surface.Columns, y = cell / surface.Columns;
                int[] neighbors = { y * surface.Columns + (x + surface.Columns - 1) % surface.Columns,
                    y * surface.Columns + (x + 1) % surface.Columns,
                    y > 0 ? cell - surface.Columns : -1,
                    y + 1 < surface.Rows ? cell + surface.Columns : -1 };
                foreach (int next in neighbors)
                    if (next >= 0 && pixels[next].a == 255 && seen.Add(next)) pending.Enqueue(next);
            }
            Assert.That(seen.Count, Is.EqualTo(surface.CoveredCount), "Growth cannot skip intervening unpainted cells.");
        }
    }
}
