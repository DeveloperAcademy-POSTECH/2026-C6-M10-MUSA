using System.Linq;
using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>Bulk retries follow the measured RTT, a repeated ACK resends a gap at once, and a backlog limit replaces the any-pending skip (#437).</summary>
    public sealed class LabBulkAdaptiveRetransmitTests
    {
        [Test]
        public void WithoutATimedAckTheRetryStaysAtTheFixedQuarterSecond()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("a", 0d);
            sender.GetDue(0d, 100);
            Assert.That(sender.Ack(1), Is.True, "The untimed overload still acknowledges.");
            Assert.That(sender.HasRttSample, Is.False);
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(LabBulkSender.InitialRetrySeconds));
            sender.Enqueue("b", 1d);
            sender.GetDue(1d, 100);
            Assert.That(sender.GetDue(1.249d, 100), Is.Empty);
            Assert.That(sender.GetDue(1.25d, 100).Length, Is.EqualTo(1));
        }

        [Test]
        public void AFirstSampleSetsTheTimeoutToRttPlusTwiceItselfAndRetriesStillDouble()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("a", 0d);
            sender.GetDue(0d, 100);
            Assert.That(sender.Ack(1, .05d), Is.True);
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(.05d).Within(1e-9));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.15d).Within(1e-9), "srtt .05 + 4 x rttvar .025");
            sender.Enqueue("b", 1d);
            sender.GetDue(1d, 100);
            Assert.That(sender.GetDue(1.149d, 100), Is.Empty);
            Assert.That(sender.GetDue(1.151d, 100).Length, Is.EqualTo(1), "The quarter-second wait is gone.");
            // The oldest chunk timed out: the shared backoff doubles the timeout to .3, and its own second wait doubles again.
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.3d).Within(1e-9));
            Assert.That(sender.GetDue(1.75d, 100), Is.Empty);
            Assert.That(sender.GetDue(1.752d, 100).Length, Is.EqualTo(1));
        }

        [Test]
        public void TheMeasuredTimeoutIsClampedBetweenTheFloorAndOneSecond()
        {
            var fast = new LabBulkSender();
            fast.Enqueue("a", 0d);
            fast.GetDue(0d, 100);
            fast.Ack(1, .005d);
            Assert.That(fast.RetryTimeoutSeconds, Is.EqualTo(LabBulkSender.MinimumRetrySeconds));

            var slow = new LabBulkSender();
            slow.Enqueue("a", 0d);
            slow.GetDue(0d, 100);
            slow.Ack(1, .6d);
            Assert.That(slow.RetryTimeoutSeconds, Is.EqualTo(LabBulkSender.MaximumRetrySeconds));
        }

        [Test]
        public void AResentChunkGivesNoSampleButLaterSamplesSmoothTheEstimate()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("a", 0d);
            sender.GetDue(0d, 100);
            sender.GetDue(.25d, 100);
            Assert.That(sender.Ack(1, .3d), Is.True);
            Assert.That(sender.HasRttSample, Is.False, "Karn: the ACK may answer either send.");

            sender.Enqueue("b", 1d);
            sender.GetDue(1d, 100);
            sender.Ack(2, 1.1d);
            sender.Enqueue("c", 2d);
            sender.GetDue(2d, 100);
            sender.Ack(3, 2.02d);
            // rttvar = .75 x .05 + .25 x |.1 - .02| = .0575, srtt = .875 x .1 + .125 x .02 = .09
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(.09d).Within(1e-9));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.09d + 4d * .0575d).Within(1e-9));
        }

        [Test]
        public void TheThirdRepeatedAckResendsOnlyTheFirstMissingChunkOncePerGap()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 5; i++) sender.Enqueue("chunk" + i, 0d);
            Assert.That(sender.GetDue(0d, 10000).Length, Is.EqualTo(5));
            // Chunk 1 is lost; chunks 2..5 each make the receiver repeat ACK 0.
            Assert.That(sender.Ack(0, .02d), Is.False);
            Assert.That(sender.Ack(0, .02d), Is.False);
            Assert.That(sender.GetDue(.02d, 10000), Is.Empty, "Two repeats are not enough.");
            sender.Ack(0, .03d);
            Assert.That(sender.GetDue(.03d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 1 }));
            sender.Ack(0, .04d); sender.Ack(0, .04d); sender.Ack(0, .04d); sender.Ack(0, .04d);
            Assert.That(sender.GetDue(.04d, 10000), Is.Empty, "One fast resend per gap; the timeout covers the rest.");

            Assert.That(sender.Ack(2, .06d), Is.True, "The resend filled the gap up to a later loss of chunk 3.");
            Assert.That(sender.HasRttSample, Is.False, "The ACK waited on resent chunk 1, so it times the recovery, not the path.");
            // Chunk 1's lost original cannot echo, but its one extra copy is assumed in flight and discounted first.
            sender.Ack(2, .07d); sender.Ack(2, .07d); sender.Ack(2, .07d);
            Assert.That(sender.GetDue(.07d, 10000), Is.Empty);
            sender.Ack(2, .08d);
            Assert.That(sender.GetDue(.08d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 3 }),
                "A new gap can be resent fast again.");
        }

        [Test]
        public void RepeatsCausedByEchoesOfSpuriousResendsDoNotTriggerAFastResend()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 3; i++) sender.Enqueue("early" + i, 0d);
            sender.GetDue(0d, 10000);
            Assert.That(sender.GetDue(.25d, 10000).Length, Is.EqualTo(3), "A receiver hitch makes all three time out.");
            Assert.That(sender.Ack(3, .3d), Is.True, "The originals arrive after all.");
            sender.Enqueue("next0", .3d); sender.Enqueue("next1", .3d);
            Assert.That(sender.GetDue(.3d, 10000).Length, Is.EqualTo(2));
            // The three resent copies now arrive and each repeats ACK 3, though nothing after 3 is missing.
            sender.Ack(3, .32d); sender.Ack(3, .32d); sender.Ack(3, .32d);
            Assert.That(sender.GetDue(.32d, 10000), Is.Empty);
        }

        [Test]
        public void AfterTheRttJumpsAboveAStaleTimeoutTheSharedBackoffLetsANewSampleThrough()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("a", 0d);
            sender.GetDue(0d, 100);
            sender.Ack(1, .01d);
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(LabBulkSender.MinimumRetrySeconds));
            // The RTT jumps to .15: the first send is resent at the stale .08 before its ACK can return.
            sender.Enqueue("b", 1d);
            sender.GetDue(1d, 100);
            Assert.That(sender.GetDue(1.081d, 100).Length, Is.EqualTo(1));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.16d).Within(1e-9), "The oldest chunk's timeout doubles the shared timer.");
            sender.Ack(2, 1.15d);
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(.01d).Within(1e-9), "Karn: no sample from the resent chunk.");
            // A new chunk now waits .16 and its ACK at .15 is a clean sample.
            sender.Enqueue("c", 2d);
            sender.GetDue(2d, 100);
            Assert.That(sender.GetDue(2.15d, 100), Is.Empty);
            Assert.That(sender.Ack(3, 2.15d), Is.True);
            // rttvar = .75 x .005 + .25 x .14 = .03875, srtt = .875 x .01 + .125 x .15 = .0275; the backoff resets.
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(.0275d).Within(1e-9));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.0275d + 4d * .03875d).Within(1e-9));
        }

        [Test]
        public void RetriesOfOneChunkStillReachTheOneSecondCapFromAShortMeasuredTimeout()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("a", 0d);
            sender.GetDue(0d, 100);
            sender.Ack(1, .005d);
            sender.Enqueue("stalled", 1d);
            sender.GetDue(1d, 100);
            Assert.That(sender.GetDue(1.081d, 100).Length, Is.EqualTo(1), "First resend after .08.");
            Assert.That(sender.GetDue(1.4d, 100), Is.Empty);
            Assert.That(sender.GetDue(1.402d, 100).Length, Is.EqualTo(1), "Then after .32.");
            Assert.That(sender.GetDue(2.401d, 100), Is.Empty);
            Assert.That(sender.GetDue(2.403d, 100).Length, Is.EqualTo(1), "Then every second.");
            Assert.That(sender.GetDue(3.402d, 100), Is.Empty);
            Assert.That(sender.GetDue(3.404d, 100).Length, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedAcksWithNothingSentAfterTheFirstChunkDoNotResend()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("only", 0d);
            sender.GetDue(0d, 100);
            for (int i = 0; i < 5; i++) sender.Ack(0, .01d);
            Assert.That(sender.GetDue(.01d, 100), Is.Empty, "No later chunk can have produced these repeats.");
            Assert.That(sender.GetDue(.25d, 100).Length, Is.EqualTo(1), "The timeout still resends it.");
        }

        [Test]
        public void ClearForgetsTheRttEstimate()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("a", 0d);
            sender.GetDue(0d, 100);
            sender.Ack(1, .05d);
            sender.Clear();
            Assert.That(sender.HasRttSample, Is.False);
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(LabBulkSender.InitialRetrySeconds));
        }

        [Test]
        public void TheSenderReportsTheLastQueuedAndAcknowledgedSequencesForCheckpointTracking()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("a", 0d); sender.Enqueue("b", 0d);
            Assert.That(sender.LastEnqueued, Is.EqualTo(2));
            Assert.That(sender.Acknowledged, Is.Zero);
            sender.GetDue(0d, 100);
            sender.Ack(1);
            Assert.That(sender.Acknowledged, Is.EqualTo(1), "A checkpoint queued through 2 is still in flight.");
            sender.Ack(2);
            Assert.That(sender.Acknowledged, Is.EqualTo(2));
        }

        [Test]
        public void OnlyAPartialFrameWithoutNewFragmentsForTheWholeIntervalCountsAsStalled()
        {
            var watch = new LabStallWatch(1d);
            Assert.That(watch.Stalled(0, 0, 0d), Is.False, "Nothing pending.");
            Assert.That(watch.Stalled(7, 1, 1d), Is.False);
            Assert.That(watch.Stalled(8, 1, 1.9d), Is.False, "Frames that keep completing never stall.");
            Assert.That(watch.Stalled(8, 2, 2.5d), Is.False, "A large frame still receiving fragments is arriving, not stuck.");
            Assert.That(watch.Stalled(8, 3, 3.4d), Is.False);
            Assert.That(watch.Stalled(8, 3, 4.39d), Is.False);
            Assert.That(watch.Stalled(8, 3, 4.4d), Is.True, "No new fragment for a second is stuck.");
            Assert.That(watch.Stalled(0, 0, 4.5d), Is.False);
            Assert.That(watch.Stalled(8, 3, 4.6d), Is.False, "A value seen again after a gap starts a new interval.");
            watch.Clear();
            Assert.That(watch.Stalled(8, 3, 5d), Is.False);
        }

        [Test]
        public void ATimeoutOfANewerChunkAlsoBacksOffButAWindowExpiringTogetherCountsOnce()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("s", 0d);
            sender.GetDue(0d, 100);
            sender.Ack(1, .05d);
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.15d).Within(1e-9));
            sender.Enqueue("a", 1d); sender.Enqueue("b", 1d);
            sender.GetDue(1d, 10000);
            // A gap repeat resends a fast; that is not a timeout and does not back off.
            sender.Ack(1, 1.02d); sender.Ack(1, 1.02d); sender.Ack(1, 1.02d);
            Assert.That(sender.GetDue(1.02d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 2 }));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.15d).Within(1e-9));
            // b times out while a, resent, is still the oldest: it backs off the shared timer too.
            Assert.That(sender.GetDue(1.151d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 3 }));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.3d).Within(1e-9));
            sender.Enqueue("c", 1.2d); sender.Enqueue("d", 1.2d);
            sender.GetDue(1.2d, 10000);
            // a, c and d expire in one flush: the timer doubles once, not three times.
            Assert.That(sender.GetDue(1.501d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 2, 4, 5 }));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.6d).Within(1e-9));
        }

        [Test]
        public void TheEchoDiscountAccumulatesAcrossAdvancesAndExpiresAfterATimeout()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 4; i++) sender.Enqueue("early" + i, 0d);
            sender.GetDue(0d, 10000);
            Assert.That(sender.GetDue(.25d, 10000).Length, Is.EqualTo(4), "All four time out once.");
            // The originals are acknowledged one by one; each advance adds its chunk's extra copy.
            sender.Ack(1, .3d); sender.Ack(2, .3d); sender.Ack(3, .3d); sender.Ack(4, .3d);
            sender.Enqueue("next0", .3d); sender.Enqueue("next1", .3d);
            sender.GetDue(.3d, 10000);
            sender.Ack(4, .31d); sender.Ack(4, .31d); sender.Ack(4, .31d); sender.Ack(4, .31d);
            Assert.That(sender.GetDue(.31d, 10000), Is.Empty, "Four echoes from four separate advances are all discounted.");

            var late = new LabBulkSender();
            late.Enqueue("x", 0d);
            late.GetDue(0d, 100);
            late.GetDue(.25d, 100);
            late.Ack(1, .3d);
            late.Enqueue("y", .3d); late.Enqueue("z", .3d);
            late.GetDue(.3d, 10000);
            // The resent copy of x was lost; after its deadline (.25 + .25) a real gap counts in full.
            late.Ack(1, .52d); late.Ack(1, .52d); late.Ack(1, .52d);
            Assert.That(late.GetDue(.52d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 2 }));
        }

        [Test]
        public void TheBacklogLimitLeavesRoomForAStageStartPaintMapUnderTheQueueLimit()
        {
            Assert.That(LabNetwork.BulkBacklogLimitBytes, Is.EqualTo(64 * 1024));
            // A full 256 x 128 paint map is about 160 KB of Base64; backlog + map + snapshot stay far below 4 MiB.
            Assert.That(LabNetwork.BulkBacklogLimitBytes + 512 * 1024, Is.LessThan(LabBulkSender.DefaultMaximumQueuedBytes));
        }
    }
}
