using System;
using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>Periodic board checkpoints are spread over the 5 s grid per recipient and Host-side requests are rate limited (#435).</summary>
    public sealed class LabBoardCheckpointPhaseTests
    {
        [Test]
        public void RecipientsWithDifferentPhasesCheckpointOnDifferentTicksAndKeepTheFiveSecondPeriod()
        {
            var publisher = new LabBoardPublisher();
            var snapshot = Snapshot(0d);
            Assert.That(publisher.Build(snapshot, 10ul, 1, 1ul, false, 0d).checkpoint, Is.True, "Every first frame is a checkpoint.");
            Assert.That(publisher.Build(snapshot, 20ul, 1, 1ul, false, 2.5d).checkpoint, Is.True);

            snapshot.serverTime = 2.5d;
            Assert.That(publisher.Build(snapshot, 10ul, 2, 2ul).checkpoint, Is.False);
            Assert.That(publisher.Build(snapshot, 20ul, 2, 2ul).checkpoint, Is.True, "Phase 2.5 fires half a period after the round start.");
            snapshot.serverTime = 5d;
            Assert.That(publisher.Build(snapshot, 10ul, 3, 3ul).checkpoint, Is.True);
            Assert.That(publisher.Build(snapshot, 20ul, 3, 3ul).checkpoint, Is.False);
            snapshot.serverTime = 7.5d;
            Assert.That(publisher.Build(snapshot, 10ul, 4, 4ul).checkpoint, Is.False);
            Assert.That(publisher.Build(snapshot, 20ul, 4, 4ul).checkpoint, Is.True);
        }

        [Test]
        public void AForcedCheckpointKeepsTheRecipientsGridAndALateTickDoesNotDriftIt()
        {
            var publisher = new LabBoardPublisher();
            var snapshot = Snapshot(0d);
            publisher.Build(snapshot, 20ul, 1, 1ul, false, 2.5d);
            snapshot.serverTime = 1d;
            Assert.That(publisher.Build(snapshot, 20ul, 2, 2ul, true).checkpoint, Is.True, "A Guest's board_checkpoint request forces one.");
            snapshot.serverTime = 2.4d;
            Assert.That(publisher.Build(snapshot, 20ul, 3, 3ul).checkpoint, Is.False);
            snapshot.serverTime = 2.57d;
            Assert.That(publisher.Build(snapshot, 20ul, 4, 4ul).checkpoint, Is.True, "The forced frame did not move the 2.5 s grid.");
            snapshot.serverTime = 7.49d;
            Assert.That(publisher.Build(snapshot, 20ul, 5, 5ul).checkpoint, Is.False, "A late tick is snapped back to the grid, so the next one is still at 7.5.");
            snapshot.serverTime = 7.5d;
            Assert.That(publisher.Build(snapshot, 20ul, 6, 6ul).checkpoint, Is.True);
        }

        [Test]
        public void ThePhaseIsReadOnlyOnTheFirstFrameOfARoundAndMustStayInsideThePeriod()
        {
            var publisher = new LabBoardPublisher();
            var snapshot = Snapshot(0d);
            publisher.Build(snapshot, 20ul, 1, 1ul, false, 2.5d);
            snapshot.serverTime = 2.5d;
            Assert.That(publisher.Build(snapshot, 20ul, 2, 2ul, false, 0d).checkpoint, Is.True, "A later phase argument is ignored.");
            snapshot.round = 2; snapshot.serverTime = 0d;
            Assert.That(publisher.Build(snapshot, 20ul, 3, 1ul, false, 1d).checkpoint, Is.True);
            snapshot.serverTime = 4d;
            Assert.That(publisher.Build(snapshot, 20ul, 4, 2ul).checkpoint, Is.True, "The new round took the new phase.");
            Assert.Throws<ArgumentOutOfRangeException>(() => publisher.Build(snapshot, 30ul, 1, 1ul, false, LabBoardPublisher.CheckpointIntervalSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => publisher.Build(snapshot, 30ul, 1, 1ul, false, -.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => publisher.Build(snapshot, 30ul, 1, 1ul, false, double.NaN));
        }

        [Test]
        public void ThrottleAcceptsOncePerIntervalPerPeerAndRecordsOnlyAcceptedRequests()
        {
            var throttle = new LabPeerRequestThrottle();
            Assert.That(throttle.TryAccept(10ul, 100d, .5d), Is.True);
            Assert.That(throttle.TryAccept(10ul, 100.2d, .5d), Is.False);
            Assert.That(throttle.TryAccept(10ul, 100.49d, .5d), Is.False, "A refused request does not push the window back.");
            Assert.That(throttle.TryAccept(10ul, 100.5d, .5d), Is.True);
            Assert.That(throttle.TryAccept(20ul, 100.2d, .5d), Is.True, "Peers are independent.");
            Assert.That(throttle.Count, Is.EqualTo(2));
            throttle.Forget(10ul);
            Assert.That(throttle.TryAccept(10ul, 100.6d, .5d), Is.True);
            throttle.Clear();
            Assert.That(throttle.Count, Is.Zero);
            Assert.That(throttle.TryAccept(20ul, 100.3d, .5d), Is.True);
        }

        private static LabSnapshot Snapshot(double at) => new LabSnapshot
        {
            sessionId = "checkpoint-phase", round = 1u, serverTime = at, phase = LabPhase.Playing,
            remaining = 180d - at, playedSeconds = at,
            orbs = new[] { new LabOrbState { id = "mine", owner = 10ul, kind = LabOrbKind.Combined, x = .5f, y = .5f, ownershipVersion = 1ul },
                new LabOrbState { id = "theirs", owner = 20ul, kind = LabOrbKind.Yin, x = .5f, y = .5f, ownershipVersion = 1ul } }
        };
    }
}
