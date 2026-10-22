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
