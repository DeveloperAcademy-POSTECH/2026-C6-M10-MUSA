using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Seeded Host-model growth fixtures, not projectile approval or device tests.</summary>
    public sealed class LabPaintFlowClockTests
    {
        private LabConfig config;
        [TearDown] public void Cleanup() { if (config != null) UnityEngine.Object.DestroyImmediate(config); }

        [Test]
        public void DirectionSampleConsumesOldIntervalBeforeInstallingTheNewOneAndAllRequestsUseIt()
        {
            config = Create(10f, 2f, 50f, 6f);
            LabModel sampled = Begin("gravity-timeline"), reference = Begin("gravity-timeline");
            Seed(sampled); Seed(reference);
            Assert.That(sampled.SetPaintFlowDirection(Vector3.right, .25d), Is.True);
            reference.Tick(.25d);
            AssertMaps(sampled, reference);
            Assert.That(sampled.PaintSurface.FlowDirection, Is.EqualTo(Vector3.right));
            Assert.That(reference.PaintSurface.FlowDirection, Is.EqualTo(Vector3.down));
            Assert.That(reference.SetPaintFlowDirection(Vector3.right, .25d), Is.True);
            sampled.Tick(1d);
            reference.Snapshot(.5d);
            Assert.That(reference.TryGenerate(1ul, "request-advances-time", .75d, out _, out string error), Is.True, error);
            reference.Tick(1d);
            AssertMaps(sampled, reference);
            Assert.That(sampled.Phase, Is.EqualTo(reference.Phase));
        }

        [Test]
        public void InvalidDirectionAndTimeCannotAdvanceOrReplaceTheSample()
        {
            config = Create(10f, 2f, 50f, 6f);
            LabModel room = Begin("invalid-gravity"); Seed(room);
            Color32[] before = Pixels(room);
            foreach (Vector3 invalid in new[] { Vector3.zero, new Vector3(.001f, 0, 0), new Vector3(float.NaN, 1, 0),
                new Vector3(float.PositiveInfinity, 0, 0), new Vector3(float.MaxValue, float.MaxValue, 0) })
            {
                Assert.That(room.SetPaintFlowDirection(invalid, .5d), Is.False);
                Assert.That(Pixels(room), Is.EqualTo(before));
                Assert.That(room.PaintSurface.FlowDirection, Is.EqualTo(Vector3.down));
            }
            room.Tick(.1d); // Invalid sample timestamps did not move lastTime to .5.
            before = Pixels(room);
            Assert.That(room.SetPaintFlowDirection(Vector3.right, .05d), Is.False);
            Assert.That(room.SetPaintFlowDirection(Vector3.right, double.NaN), Is.False);
            Assert.That(Pixels(room), Is.EqualTo(before));
            Assert.That(room.PaintSurface.FlowDirection, Is.EqualTo(Vector3.down));
        }

        [Test]
        public void PausedClockKeepsCellsBudgetAndTimeFixedAndResumeContinues()
        {
            config = Create(10f, 2f, 50f, 6f);
            var clock = new LabBattleClock();
            LabModel room = Begin("paused-flow"); Seed(room);
            Assert.That(clock.Pause(.25d), Is.True);
            room.Tick(clock.Now(.25d));
            Color32[] before = Pixels(room);
            double budget = room.PaintSurface.PendingCoverage;
            double played = room.Snapshot(clock.Now(.25d)).playedSeconds;
            for (int i = 0; i < 12; i++)
            {
                Assert.That(room.SetPaintFlowDirection(Vector3.right, clock.Now(1000d + i)), Is.True);
                Assert.That(Pixels(room), Is.EqualTo(before));
                Assert.That(room.PaintSurface.PendingCoverage, Is.EqualTo(budget));
                Assert.That(room.Snapshot(clock.Now(1000d + i)).playedSeconds, Is.EqualTo(played));
            }
            Assert.That(clock.Resume(1011d), Is.True);
            room.Tick(clock.Now(1011.25d));
            Assert.That(Pixels(room), Is.Not.EqualTo(before));
            Assert.That(room.PaintSurface.PendingCoverage, Is.LessThan(budget));
        }

        [TestCase(1f, LabPhase.Defeat)]
        [TestCase(1.25f, LabPhase.Victory)]
        public void FlowCompletionPreservesTheExactTimeoutPriority(float duration, LabPhase expected)
        {
            config = Create(duration, 50f, 50f, 50f);
            LabModel room = Begin("flow-deadline"); Seed(room);
            Assert.That(room.SetPaintFlowDirection(Vector3.right, 2d), Is.True);
            Assert.That(room.Phase, Is.EqualTo(expected));
            Assert.That(room.PaintSurface.IsComplete, Is.EqualTo(expected == LabPhase.Victory));
            Assert.That(room.PaintSurface.CoveredCount,
                Is.EqualTo(room.PaintSurface.CellCount - (expected == LabPhase.Victory ? 0 : 1)));
        }

        [Test]
        public void NewRoundResetsCellsAndThePreviousGravityDirection()
        {
            config = Create(1f, 2f, 4f, 6f);
            LabModel room = Begin("flow-reset"); Seed(room);
            room.SetPaintFlowDirection(Vector3.right, 0d);
            room.Tick(2d);
            room.BeginSolo("flow-reset", 1ul, 2d, config);
            Assert.That(room.PaintSurface.FlowDirection, Is.EqualTo(Vector3.down));
            Assert.That(room.PaintSurface.CoveredCount, Is.Zero);
            Assert.That(room.PaintSurface.PendingCoverage, Is.Zero);
        }

        private static LabConfig Create(float duration, float initial, float repeat, float speed)
        {
            var source = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = source.CaptureDeveloperValues();
            values[2] = duration; values[79] = 0; values[104] = 0;
            values[105] = initial; values[106] = repeat; values[107] = speed;
            values[132] = 1; values[134] = 0;
            Assert.That(source.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return source;
        }
        private LabModel Begin(string id)
        {
            var room = new LabModel(); room.BeginSolo(id, 1ul, 0d, config); return room;
        }
        private static void Seed(LabModel room)
        {
            Assert.That(room.PaintSurface.RegisterHit(Vector3.forward, Color.red, 1ul), Is.True);
            Assert.That(room.PaintSurface.RegisterHit(Vector3.forward, Color.blue, 1ul), Is.True);
        }
        private static Color32[] Pixels(LabModel room)
        { var pixels = new Color32[room.PaintSurface.CellCount]; room.PaintSurface.CopyPixels(pixels); return pixels; }
        private static void AssertMaps(LabModel left, LabModel right)
        {
            Assert.That(Pixels(left), Is.EqualTo(Pixels(right)));
            Assert.That(left.PaintSurface.CoveredCount, Is.EqualTo(right.PaintSurface.CoveredCount));
            Assert.That(left.PaintSurface.PendingCoverage, Is.EqualTo(right.PaintSurface.PendingCoverage));
        }
    }
}
