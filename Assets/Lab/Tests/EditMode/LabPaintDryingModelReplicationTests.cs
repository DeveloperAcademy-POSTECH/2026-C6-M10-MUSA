using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// Fake-clock Host/packet fixtures, not live transport or projectile-collision evidence.
    /// Only projectile creation is injected; ConfirmHit, battle timing and replication are production paths.
    /// Zero repeat/growth in the surface fixture isolates drying from simultaneous growth.
    /// </summary>
    public sealed class LabPaintDryingModelReplicationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private LabConfig config;

        [TearDown]
        public void Cleanup() { if (config != null) Object.DestroyImmediate(config); }

        [Test]
        public void ApprovedNearbyHitProtectsExistingLocalPaintButNotTheOppositeSide()
        {
            LabModel model = Begin();
            Hit(model, "front", Vector3.forward, 0d, LabOrbElement.Fire);
            Hit(model, "back", Vector3.back, 0d, LabOrbElement.Water);
            Color32[] initial = Pixels(model.PaintSurface);
            var points = (Vector3[])typeof(LabPaintSurface).GetField("points", Private).GetValue(model.PaintSurface);
            int[] front = Enumerable.Range(0, initial.Length)
                .Where(i => initial[i].Equals((Color32)model.PaintColorFor(LabOrbElement.Fire))
                    && Vector3.Angle(points[i], Vector3.forward) < 10f).ToArray();
            int[] back = Enumerable.Range(0, initial.Length)
                .Where(i => initial[i].Equals((Color32)model.PaintColorFor(LabOrbElement.Water))).ToArray();
            Assert.That(front, Is.Not.Empty); Assert.That(back, Is.Not.Empty);
            Hit(model, "refresh", Vector3.forward, 4d, LabOrbElement.Wood);
            model.Tick(6d);
            Color32[] after = Pixels(model.PaintSurface);
            Assert.That(front.All(i => after[i].Equals(initial[i])), Is.True,
                "A nearby impact refreshes existing pigment without recoloring it.");
            Assert.That(back.Count(i => after[i].a == 0), Is.GreaterThan(0),
                "The same refresh cannot protect the far side of the shared surface.");
            Assert.That(model.Phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(model.Snapshot(6d).paintCoveredCount,
                Is.EqualTo(after.Count(pixel => pixel.a != 0)));
        }

        [Test]
        public void RejectedDuplicateImpactCannotKeepAnOldPatchWet()
        {
            LabModel model = Begin(.1f); // Two adjacent cells are wholly inside the refresh radius.
            Hit(model, "first", Vector3.forward, 0d);
            Hit(model, "accepted-refresh", Vector3.forward, 4d);
            int protectedCount = model.PaintSurface.CoveredCount;
            Assert.That(model.ConfirmHit("accepted-refresh", 8d, Vector3.forward, out _, out _, out _), Is.False,
                "A collision for an already consumed projectile cannot refresh drying.");
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(protectedCount));
            model.Tick(10d);
            Assert.That(model.PaintSurface.CoveredCount, Is.LessThan(protectedCount),
                "The accepted impact expired at t=9; a rejected duplicate at t=8 must not extend it to t=13.");
            Assert.That(model.PaintSurface.PaintCount, Is.EqualTo(2));
        }

        [Test]
        public void PausedBattleClockFreezesDryingAndRetryStartsWithNoOldProtectionOrPaint()
        {
            LabModel model = Begin(); Hit(model, "initial", Vector3.forward, 0d);
            var clock = new LabBattleClock();
            Assert.That(clock.Pause(4d), Is.True);
            model.Tick(clock.Now(4d));
            Color32[] before = Pixels(model.PaintSurface);
            for (int i = 0; i < 5; i++) model.Tick(clock.Now(1000d + i));
            Assert.That(Pixels(model.PaintSurface), Is.EqualTo(before));
            Assert.That(model.Snapshot(clock.Now(1004d)).playedSeconds, Is.EqualTo(4d));
            Assert.That(clock.Resume(1004d), Is.True);
            model.Tick(clock.Now(1006d));
            Assert.That(model.PaintSurface.CoveredCount, Is.LessThan(before.Count(pixel => pixel.a != 0)));
            model.Tick(21d);
            Assert.That(model.Phase, Is.EqualTo(LabPhase.Defeat));
            model.BeginSolo("drying-model", 1ul, 21d, config);
            ResetSurfaceWithoutGrowth(model);
            Assert.That(model.Round, Is.EqualTo(2u));
            Assert.That(model.PaintSurface.CoveredCount, Is.Zero);
            Hit(model, "new-round", Vector3.forward, 21d);
            int fresh = model.PaintSurface.CoveredCount;
            model.Tick(25d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(fresh));
            model.Tick(27d);
            Assert.That(model.PaintSurface.CoveredCount, Is.LessThan(fresh));
        }

        [Test]
        public void DryingDeletesBothColorAndPainterThroughDeltasAndCheckpointRecovery()
        {
            LabModel model = Begin(); Hit(model, "initial", Vector3.forward, 0d);
            var publisher = new LabPaintPublisher();
            var replica = new LabPaintSurface(); var receiver = new LabPaintReceiver(replica);
            LabSnapshot initial = model.Snapshot(0d);
            Receive(receiver, publisher.Publish(model.PaintSurface, initial.sessionId, initial.round, 1));
            Color32[] before = Pixels(replica);
            model.Tick(6d); LabSnapshot dried = model.Snapshot(6d);
            LabPaintFrame[] delta = publisher.Publish(model.PaintSurface, dried.sessionId, dried.round, 2);
            Assert.That(delta, Is.Not.Empty);
            Assert.That(delta.All(frame => !frame.full), Is.True);
            Assert.That(receiver.Matches(dried), Is.False);
            Receive(receiver, delta);
            AssertReplica(model.PaintSurface, replica);
            Assert.That(receiver.Matches(dried), Is.True);
            byte[] owners = Slots(replica); Color32[] after = Pixels(replica);
            int erased = 0;
            for (int i = 0; i < after.Length; i++)
            {
                if (before[i].a == 0 || after[i].a != 0) continue;
                erased++;
                Assert.That(after[i], Is.EqualTo(new Color32(0, 0, 0, 0)));
                Assert.That(owners[i], Is.Zero, "Erased paint cannot retain a visible player pattern.");
            }
            Assert.That(erased, Is.GreaterThan(0));
            Assert.That(LabHud.PaintGoalFill(dried), Is.LessThan(LabHud.PaintGoalFill(initial)));
            Assert.That(LabHud.FormatPaintGoalProgress(dried),
                Is.EqualTo(LabHud.FormatPaintGoalProgress(replica.Coverage, 100)));

            model.Tick(7d); publisher.Publish(model.PaintSurface, dried.sessionId, dried.round, 3); // deliberately lost
            model.Tick(8d);
            LabPaintFrame[] missingBase = publisher.Publish(model.PaintSurface, dried.sessionId, dried.round, 4);
            Assert.That(receiver.Receive(missingBase[0]), Is.EqualTo(LabPaintReceive.NeedCheckpoint));
            Receive(receiver, publisher.Checkpoint());
            AssertReplica(model.PaintSurface, replica);
            var fresh = new LabPaintSurface(); var freshReceiver = new LabPaintReceiver(fresh);
            Receive(freshReceiver, publisher.Checkpoint());
            AssertReplica(model.PaintSurface, fresh);
        }

        [Test]
        public void OneHundredPercentVictoryFreezesTheActualMapEvenWhenDryingIsEnabled()
        {
            LabModel model = Begin(100f);
            Hit(model, "complete", Vector3.forward, 0d);
            Assert.That(model.Phase, Is.EqualTo(LabPhase.Victory));
            Color32[] completed = Pixels(model.PaintSurface); byte[] owners = Slots(model.PaintSurface);
            uint revision = model.PaintSurface.Revision;
            LabSnapshot result = model.Snapshot(1000d);
            Assert.That(result.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(result.paintCoveredCount, Is.EqualTo(result.paintCellCount));
            Assert.That(LabHud.FormatPaintGoalProgress(result), Is.EqualTo("PAINT 100.0% / 100%"));
            Assert.That(Pixels(model.PaintSurface), Is.EqualTo(completed));
            Assert.That(Slots(model.PaintSurface), Is.EqualTo(owners));
            Assert.That(model.PaintSurface.Revision, Is.EqualTo(revision));
        }

        private LabModel Begin(float initial = 2f)
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[2] = 20f; values[79] = 0f; values[104] = 0f; values[105] = initial;
            values[134] = 0f; values[148] = 1f; values[149] = 5f; values[150] = .2f; values[151] = 20f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var model = new LabModel(); model.BeginSolo("drying-model", 1ul, 0d, config);
            ResetSurfaceWithoutGrowth(model);
            return model;
        }
        private void ResetSurfaceWithoutGrowth(LabModel model)
        {
            LabPaintGrowthSettings settings = config.PaintGrowthSettings;
            settings.RepeatPercent = 0f; settings.GrowthPercentPerSecond = 0f;
            model.PaintSurface.Reset(settings, 225u);
        }
        private static void Hit(LabModel model, string id, Vector3 direction, double time,
            LabOrbElement element = LabOrbElement.Fire)
        {
            var orbs = (Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", Private).GetValue(model);
            orbs.Add(id, new LabOrbState { id = id, owner = 1ul, kind = LabOrbKind.Combined,
                element = element, inFlight = true, createdAt = time });
            Assert.That(model.ConfirmHit(id, time, direction, out _, out _, out _), Is.True);
        }
        private static void Receive(LabPaintReceiver receiver, LabPaintFrame[] frames)
        {
            Assert.That(frames, Is.Not.Empty);
            LabPaintReceive result = LabPaintReceive.Pending;
            foreach (LabPaintFrame frame in frames.Reverse()) result = receiver.Receive(frame);
            Assert.That(result, Is.EqualTo(LabPaintReceive.Applied));
        }
        private static Color32[] Pixels(LabPaintSurface surface)
        { var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels; }
        private static byte[] Slots(LabPaintSurface surface)
        { var slots = new byte[surface.CellCount]; surface.CopyPainterSlots(slots); return slots; }
        private static void AssertReplica(LabPaintSurface source, LabPaintSurface replica)
        {
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)));
            Assert.That(Slots(replica), Is.EqualTo(Slots(source)));
            Assert.That(replica.CoveredCount, Is.EqualTo(source.CoveredCount));
            Assert.That(replica.PaintCount, Is.EqualTo(source.PaintCount));
            Assert.That(replica.Revision, Is.EqualTo(source.Revision));
        }
    }
}
