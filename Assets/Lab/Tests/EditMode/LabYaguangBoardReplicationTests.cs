using System;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabYaguangBoardReplicationTests
    {
        private static LabSnapshot Snapshot(double time = 10d) => new LabSnapshot
        {
            sessionId = "flash-board", round = 1, phase = LabPhase.Playing, serverTime = time,
            remaining = 170d, playedSeconds = 10d,
            players = new[] { new LabPlayerState { id = 0, seat = 0, stamina = 100f } },
            flashWarningAt = 9d, flashAt = 12d, flashTarget = 0, flashTargetAll = false,
            flashCount = 1, flashLastAt = 8d, flashLastTarget = 0, flashLastTargetAll = false,
            jigwi = new[] { new LabJigwiState { playerId = 0, generationBurnUntil = 15d } }
        };

        private static LabBoardReplica Replica()
        {
            var replica = new LabBoardReplica(); replica.SetActive("flash-board", 1, 0); return replica;
        }

        [Test]
        public void FastBoardHeaderPreservesFlashTargetHostZeroAndJigwiWhilePaintSnapshotWaits()
        {
            var frame = new LabBoardPublisher().Build(Snapshot(), 0, 1, 1);
            var replica = Replica();
            Assert.That(replica.TryApply(frame, out string reason), Is.True, reason);
            var stale = Snapshot(5d);
            stale.flashWarningAt = stale.flashAt = stale.flashLastAt = -1d;
            stale.flashCount = 0;
            stale.jigwi = Array.Empty<LabJigwiState>();
            var current = replica.Overlay(stale);
            Assert.That(current.flashWarningAt, Is.EqualTo(9d));
            Assert.That(current.flashAt, Is.EqualTo(12d));
            Assert.That(current.FlashTargets(0), Is.True, "NGO's Host is a legitimate ONE target.");
            Assert.That(current.FlashTargets(20), Is.False, "Host zero is not the ALL sentinel.");
            Assert.That(current.LastFlashHit(0), Is.True);
            Assert.That(current.LastFlashHit(20), Is.False);
            Assert.That(current.JigwiFor(0).GenerationBurnActive(current.serverTime), Is.True);
            Assert.That(stale.flashCount, Is.Zero, "Overlay must not mutate the shared/paint snapshot.");
        }

        [Test]
        public void ExplicitAllFlagsSurvivePublisherSnapshotCopyAndReplicaOverlay()
        {
            var source = Snapshot();
            source.flashTargetAll = source.flashLastTargetAll = true;
            var copy = LabBoardReplica.CopySnapshot(source);
            Assert.That(copy.FlashTargets(20) && copy.LastFlashHit(20), Is.True);
            var frame = new LabBoardPublisher().Build(copy, 0, 1, 1);
            var replica = Replica();
            Assert.That(replica.TryApply(frame, out _), Is.True);
            var overlaid = replica.Overlay(Snapshot(5d));
            Assert.That(overlaid.flashTargetAll && overlaid.flashLastTargetAll, Is.True);
            Assert.That(overlaid.FlashTargets(0) && overlaid.FlashTargets(20), Is.True);
            Assert.That(overlaid.LastFlashHit(0) && overlaid.LastFlashHit(20), Is.True);
        }

        [Test]
        public void MalformedFlashClockIsRejectedBeforeBoardSequenceAdvances()
        {
            var source = Snapshot();
            var frame = new LabBoardPublisher().Build(source, 0, 1, 1);
            frame.flashWarningAt = double.NaN;
            var replica = Replica();
            Assert.That(replica.TryApply(frame, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.Zero);
            source.flashLastAt = double.PositiveInfinity;
            Assert.That(LabFlashNetworkValidation.Valid(source), Is.False);
        }


        [Test]
        public void NewerComboWarningAndActiveStateSuppressStaleFlashWithoutWaitingForPaint()
        {
            var publisher = new LabBoardPublisher();
            var source = Snapshot();
            source.comboNextAt = 11d; source.comboWarningAt = 9d;
            var first = publisher.Build(source, 0, 1, 1);
            Assert.That(first.hasComboState, Is.True);
            var replica = Replica(); Assert.That(replica.TryApply(first, out _), Is.True);
            var waitingPaint = Snapshot(5d);
            waitingPaint.comboNextAt = waitingPaint.comboWarningAt = -1d;
            var warning = replica.Overlay(waitingPaint);
            Assert.That(warning.ComboWarningOn(warning.serverTime), Is.True);
            Assert.That(warning.FlashWarningOn(warning.serverTime), Is.False,
                "A newer combo control window must suppress the previous flash warning.");

            source.serverTime = 14d;
            source.comboActive = true; source.comboCount = 1; source.comboStartedAt = 11d; source.comboEndsAt = 20d;
            source.comboNextAt = source.comboWarningAt = -1d;
            source.comboColors = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Earth };
            source.flashWarningAt = 13d; source.flashAt = 16d;
            var active = publisher.Build(source, 0, 2, 2);
            source.comboColors[0] = LabOrbElement.Metal;
            Assert.That(active.comboColors[0], Is.EqualTo(LabOrbElement.Fire), "Published controls own their array.");
            Assert.That(replica.TryApply(active, out _), Is.True);
            active.comboColors[0] = LabOrbElement.Metal;
            var playing = replica.Overlay(waitingPaint);
            Assert.That(playing.comboActive, Is.True);
            Assert.That(playing.comboColors[0], Is.EqualTo(LabOrbElement.Fire));
            Assert.That(playing.FlashWarningOn(playing.serverTime), Is.False);
            playing.comboColors[0] = LabOrbElement.Metal;
            Assert.That(replica.Overlay(waitingPaint).comboColors[0], Is.EqualTo(LabOrbElement.Fire));
        }

        [Test]
        public void MalformedExplicitComboControlDoesNotAdvanceTheBoardLedger()
        {
            var source = Snapshot();
            source.comboActive = true;
            source.comboColors = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood };
            var frame = new LabBoardPublisher().Build(source, 0, 1, 1);
            var replica = Replica();
            Assert.That(replica.TryApply(frame, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.Zero);
            frame.hasComboState = false;
            Assert.That(replica.TryApply(frame, out _), Is.True,
                "An omitted combo control section must leave the matched shared snapshot intact.");
            var matched = Snapshot(); matched.comboNextAt = 12d; matched.comboWarningAt = 9d;
            Assert.That(replica.Overlay(matched).ComboWarningOn(10d), Is.True);
        }

        [Test]
        public void OlderBoardHeaderCannotOverwriteNewerSharedFlashResult()
        {
            var frame = new LabBoardPublisher().Build(Snapshot(), 0, 1, 1);
            var replica = Replica(); Assert.That(replica.TryApply(frame, out _), Is.True);
            var newer = Snapshot(13d);
            newer.flashCount = 2; newer.flashLastAt = 12d;
            newer.flashLastTarget = 20; newer.flashLastTargetAll = false;
            var current = replica.Overlay(newer);
            Assert.That(current.flashCount, Is.EqualTo(2));
            Assert.That(current.flashLastAt, Is.EqualTo(12d));
            Assert.That(current.LastFlashHit(20), Is.True);
            Assert.That(current.LastFlashHit(0), Is.False);
        }
    }
}
