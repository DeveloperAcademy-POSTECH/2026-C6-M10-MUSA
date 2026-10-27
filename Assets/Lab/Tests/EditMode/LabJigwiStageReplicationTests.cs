using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Owner-board headers and shared-checkpoint data, not physical-device transport.</summary>
    public sealed class LabJigwiStageReplicationTests
    {
        private static LabJigwiStageGroup Group(string id = "stage/1/curtain/1") => new LabJigwiStageGroup
        {
            id = id, slot = 1, pattern = LabJigwiStagePattern.Curtain,
            affectedPlayer = 0, helperPlayer = 20,
            warningAt = 10d, activeAt = 11d, expiresAt = 18d,
            rootDirection = Vector3.forward,
            patchDirections = new[] { Vector3.forward, Vector3.right, Vector3.up },
            rootAngleDegrees = 18f, fireAngleDegrees = 30f, spreadSeconds = 1f,
            geometryConfigured = true
        };

        private static LabSnapshot Snapshot(double now = 10.5d) => new LabSnapshot
        {
            sessionId = "jigwi-stage-replication", round = 1, phase = LabPhase.Playing,
            serverTime = now, remaining = 120d - now, playedSeconds = now,
            hp = 123, paintCoverage = .25f, paintRevision = 7,
            players = new[]
            {
                new LabPlayerState { id = 0, seat = 0, stamina = 100f, personalHp = 300 },
                new LabPlayerState { id = 20, seat = 1, stamina = 80f, personalHp = 300 }
            },
            jigwiStage = new LabJigwiStageSnapshot
            {
                enabled = true, startedAt = 0d, cutoffAt = 100d,
                groups = new[] { Group() }
            }
        };

        private static LabBoardReplica Replica()
        {
            var result = new LabBoardReplica();
            result.SetActive("jigwi-stage-replication", 1, 0);
            return result;
        }

        [Test]
        public void SmallHeaderCarriesWarningActiveExtinguishedAndFeverWithoutPaintCheckpoint()
        {
            var publisher = new LabBoardPublisher();
            var replica = Replica();
            LabSnapshot source = Snapshot();
            var frame = publisher.Build(source, 0, 1, 1);
            Assert.That(frame.states, Is.Empty);
            Assert.That(replica.TryApply(frame, out string reason), Is.True, reason);
            LabSnapshot oldPaint = Snapshot(.5d);
            oldPaint.jigwiStage = null;
            LabSnapshot warning = replica.Overlay(oldPaint);
            Assert.That(warning.jigwiStage.groups.Single().Warning(warning.serverTime), Is.True);
            Assert.That(warning.hp, Is.EqualTo(123));
            Assert.That(warning.paintRevision, Is.EqualTo(7));
            Assert.That(warning.paintCoverage, Is.EqualTo(.25f));

            source.serverTime = 12d;
            frame = publisher.Build(source, 0, 2, 2);
            Assert.That(frame.checkpoint, Is.False, "Pattern control does not need to wait for a bulk/checkpoint refresh.");
            Assert.That(replica.TryApply(frame, out reason), Is.True, reason);
            LabSnapshot active = replica.Overlay(oldPaint);
            Assert.That(active.jigwiStage.groups.Single().Active(active.serverTime), Is.True);
            Assert.That(active.jigwiStage.groups.Single().VisiblePatchCount(active.serverTime), Is.EqualTo(2));

            source.serverTime = 13d;
            source.jigwiStage.groups[0].resolution = LabJigwiStageResolution.Extinguished;
            source.jigwiStage.groups[0].resolvedAt = 13d;
            Assert.That(replica.TryApply(publisher.Build(source, 0, 3, 3), out reason), Is.True, reason);
            LabSnapshot cleared = replica.Overlay(oldPaint);
            Assert.That(cleared.jigwiStage.groups.Single().Active(cleared.serverTime), Is.False);
            Assert.That(cleared.jigwiStage.groups.Single().resolution, Is.EqualTo(LabJigwiStageResolution.Extinguished));

            source.serverTime = 100d;
            source.jigwiStage.fever = true;
            Assert.That(replica.TryApply(publisher.Build(source, 0, 4, 4), out reason), Is.True, reason);
            LabSnapshot fever = replica.Overlay(oldPaint);
            Assert.That(fever.jigwiStage.fever, Is.True);
            Assert.That(fever.jigwiStage.groups.Single().resolution, Is.EqualTo(LabJigwiStageResolution.Extinguished));
            Assert.That(fever.hp, Is.EqualTo(oldPaint.hp));
            Assert.That(fever.paintRevision, Is.EqualTo(oldPaint.paintRevision));
        }

        [Test]
        public void PublisherReplicaAndOverlayEachOwnNestedGroupAndPatchArrays()
        {
            var publisher = new LabBoardPublisher();
            var source = Snapshot();
            LabBoardFrame frame = publisher.Build(source, 0, 1, 1);
            source.jigwiStage.groups[0].patchDirections[0] = Vector3.back;
            source.jigwiStage.groups[0].id = "mutated-source";
            Assert.That(frame.jigwiStage.groups[0].patchDirections[0], Is.EqualTo(Vector3.forward));
            Assert.That(frame.jigwiStage.groups[0].id, Is.EqualTo("stage/1/curtain/1"));

            var replica = Replica();
            Assert.That(replica.TryApply(frame, out string reason), Is.True, reason);
            frame.jigwiStage.groups[0].patchDirections[0] = Vector3.left;
            frame.jigwiStage.groups[0].helperPlayer = 99;
            var old = Snapshot(.5d);
            var visible = replica.Overlay(old);
            Assert.That(visible.jigwiStage.groups[0].helperPlayer, Is.EqualTo(20));
            Assert.That(visible.jigwiStage.groups[0].patchDirections[0], Is.EqualTo(Vector3.forward));
            visible.jigwiStage.groups[0].patchDirections[0] = Vector3.down;
            visible.jigwiStage.groups[0].resolution = LabJigwiStageResolution.Cancelled;
            var next = replica.Overlay(old);
            Assert.That(next.jigwiStage.groups[0].patchDirections[0], Is.EqualTo(Vector3.forward));
            Assert.That(next.jigwiStage.groups[0].resolution, Is.EqualTo(LabJigwiStageResolution.None));
        }

        [Test]
        public void SharedSnapshotCopyOwnsGroupArrayAndItsIndividualPatchArrays()
        {
            var source = Snapshot();
            var copy = LabBoardReplica.CopySnapshot(source);
            Assert.That(copy.jigwiStage, Is.Not.SameAs(source.jigwiStage));
            Assert.That(copy.jigwiStage.groups, Is.Not.SameAs(source.jigwiStage.groups));
            Assert.That(copy.jigwiStage.groups[0], Is.Not.SameAs(source.jigwiStage.groups[0]));
            Assert.That(copy.jigwiStage.groups[0].patchDirections, Is.Not.SameAs(source.jigwiStage.groups[0].patchDirections));
            copy.jigwiStage.groups[0].patchDirections[1] = Vector3.back;
            copy.jigwiStage.groups[0].affectedPlayer = 20;
            Assert.That(source.jigwiStage.groups[0].patchDirections[1], Is.EqualTo(Vector3.right));
            Assert.That(source.jigwiStage.groups[0].affectedPlayer, Is.Zero);
        }

        [Test]
        public void OlderControlHeaderCannotOverwriteNewerSharedStateOrTerminalResult()
        {
            var replica = Replica();
            var publisher = new LabBoardPublisher();
            Assert.That(replica.TryApply(publisher.Build(Snapshot(), 0, 1, 1), out string reason), Is.True, reason);
            var newer = Snapshot(14d);
            newer.jigwiStage.groups[0].id = "newer-shared-state";
            newer.jigwiStage.groups[0].resolution = LabJigwiStageResolution.Extinguished;
            newer.jigwiStage.groups[0].resolvedAt = 13d;
            var visible = replica.Overlay(newer);
            Assert.That(visible.serverTime, Is.EqualTo(14d));
            Assert.That(visible.jigwiStage.groups[0].id, Is.EqualTo("newer-shared-state"));
            Assert.That(visible.jigwiStage.groups[0].resolution, Is.EqualTo(LabJigwiStageResolution.Extinguished));
            var terminal = Snapshot(.5d);
            terminal.phase = LabPhase.Defeat;
            terminal.jigwiStage = new LabJigwiStageSnapshot();
            Assert.That(replica.Overlay(terminal).jigwiStage.enabled, Is.False);
            Assert.That(replica.Overlay(terminal).phase, Is.EqualTo(LabPhase.Defeat));
        }

        [Test]
        public void HeaderJsonRoundTripRetainsCrossPlayerRelationshipAndSurfaceGeometry()
        {
            var publisher = new LabBoardPublisher();
            var frame = publisher.Build(Snapshot(12d), 0, 1, 1);
            var decoded = JsonUtility.FromJson<LabBoardFrame>(JsonUtility.ToJson(frame));
            var replica = Replica();
            Assert.That(replica.TryApply(decoded, out string reason), Is.True, reason);
            var group = replica.Overlay(Snapshot(.5d)).jigwiStage.groups.Single();
            Assert.That(group.affectedPlayer, Is.Zero, "Host player zero is a real affected participant.");
            Assert.That(group.helperPlayer, Is.EqualTo(20));
            Assert.That(group.patchDirections, Is.EqualTo(new[] { Vector3.forward, Vector3.right, Vector3.up }));
            Assert.That(group.Active(12d), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyOrNonJigwiAbsentStateRemainsCompatible(bool useNull)
        {
            var source = Snapshot();
            source.jigwiStage = useNull ? null : new LabJigwiStageSnapshot();
            Assert.That(LabJigwiStageNetworkValidation.Valid(source.jigwiStage, source.players), Is.True);
            var frame = new LabBoardPublisher().Build(source, 0, 1, 1);
            var replica = Replica();
            Assert.That(replica.TryApply(frame, out string reason), Is.True, reason);
            var overlay = replica.Overlay(Snapshot(.5d));
            Assert.That(overlay.jigwiStage, Is.Not.Null);
            Assert.That(overlay.jigwiStage.enabled, Is.False);
            Assert.That(overlay.jigwiStage.groups, Is.Empty);
            Assert.That(overlay.jigwiStage.startedAt, Is.EqualTo(-1d));
            Assert.That(overlay.jigwiStage.cutoffAt, Is.EqualTo(-1d));
        }

        [TestCase("foreign-helper")]
        [TestCase("foreign-affected")]
        [TestCase("nan-start")]
        [TestCase("nan-root-angle")]
        [TestCase("nan-root")]
        [TestCase("too-many-groups")]
        [TestCase("duplicate-group")]
        [TestCase("bad-pattern")]
        [TestCase("bad-resolution")]
        [TestCase("active-before-warning")]
        [TestCase("expires-after-cutoff")]
        [TestCase("unresolved-has-time")]
        [TestCase("empty-patches")]
        [TestCase("too-many-patches")]
        [TestCase("zero-root")]
        [TestCase("nonunit-root")]
        [TestCase("zero-patch")]
        [TestCase("infinite-spread")]
        [TestCase("disabled-has-groups")]
        [TestCase("bad-phase")]
        [TestCase("nan-frame-time")]
        [TestCase("too-long-id")]
        [TestCase("null-group")]
        [TestCase("null-groups")]
        [TestCase("self-curtain")]
        [TestCase("other-ignition")]
        [TestCase("resolved-before-warning")]
        [TestCase("negative-slot")]
        [TestCase("too-large-slot")]
        [TestCase("bad-cutoff-span")]
        [TestCase("zero-root-angle")]
        [TestCase("large-fire-angle")]
        [TestCase("zero-spread")]
        public void MalformedStageMetadataIsRejectedBeforeAdvancingOrderedReplica(string corruption)
        {
            var replica = Replica();
            var publisher = new LabBoardPublisher();
            Assert.That(replica.TryApply(publisher.Build(Snapshot(), 0, 1, 1), out string reason), Is.True, reason);
            var frame = publisher.Build(Snapshot(12d), 0, 2, 2);
            LabJigwiStageSnapshot state = frame.jigwiStage;
            LabJigwiStageGroup group = state.groups[0];
            switch (corruption)
            {
                case "foreign-helper": group.helperPlayer = 999; break;
                case "foreign-affected": group.affectedPlayer = 999; break;
                case "nan-start": state.startedAt = double.NaN; break;
                case "nan-root-angle": group.rootAngleDegrees = float.NaN; break;
                case "nan-root": group.rootDirection = new Vector3(float.NaN, 0f, 1f); break;
                case "too-many-groups": state.groups = Enumerable.Range(0, LabJigwiStageNetworkValidation.MaximumGroups + 1)
                    .Select(index => Group("group/" + index)).ToArray(); break;
                case "duplicate-group": state.groups = new[] { group, group.Copy() }; break;
                case "bad-pattern": group.pattern = (LabJigwiStagePattern)99; break;
                case "bad-resolution": group.resolution = (LabJigwiStageResolution)99; break;
                case "active-before-warning": group.activeAt = 9d; break;
                case "expires-after-cutoff": group.expiresAt = 101d; break;
                case "unresolved-has-time": group.resolvedAt = 12d; break;
                case "empty-patches": group.patchDirections = Array.Empty<Vector3>(); break;
                case "too-many-patches": group.patchDirections = Enumerable.Repeat(Vector3.forward,
                    LabJigwiStageNetworkValidation.MaximumPatches + 1).ToArray(); break;
                case "zero-root": group.rootDirection = Vector3.zero; break;
                case "nonunit-root": group.rootDirection = Vector3.forward * 2f; break;
                case "zero-patch": group.patchDirections[0] = Vector3.zero; break;
                case "infinite-spread": group.spreadSeconds = float.PositiveInfinity; break;
                case "disabled-has-groups": state.enabled = false; break;
                case "bad-phase": frame.phase = (LabPhase)99; break;
                case "nan-frame-time": frame.serverTime = double.NaN; break;
                case "too-long-id": group.id = new string('x', 161); break;
                case "null-group": state.groups[0] = null; break;
                case "null-groups": state.groups = null; break;
                case "self-curtain": group.helperPlayer = group.affectedPlayer; break;
                case "other-ignition": group.pattern = LabJigwiStagePattern.Ignition; break;
                case "resolved-before-warning": group.resolution = LabJigwiStageResolution.Extinguished; group.resolvedAt = 9d; break;
                case "negative-slot": group.slot = -1; break;
                case "too-large-slot": group.slot = 8; break;
                case "bad-cutoff-span": state.cutoffAt = 99d; break;
                case "zero-root-angle": group.rootAngleDegrees = 0f; break;
                case "large-fire-angle": group.fireAngleDegrees = 90f; break;
                case "zero-spread": group.spreadSeconds = 0f; break;
                default: Assert.Fail("Unknown corruption: " + corruption); break;
            }
            Assert.That(replica.TryApply(frame, out reason), Is.False, corruption);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.EqualTo(1), "Rejected metadata must not consume the ordered sequence.");
            var retained = replica.Overlay(Snapshot(.5d));
            Assert.That(retained.jigwiStage.groups.Single().id, Is.EqualTo("stage/1/curtain/1"));
            Assert.That(retained.jigwiStage.groups.Single().rootDirection, Is.EqualTo(Vector3.forward));
        }
    }
}
