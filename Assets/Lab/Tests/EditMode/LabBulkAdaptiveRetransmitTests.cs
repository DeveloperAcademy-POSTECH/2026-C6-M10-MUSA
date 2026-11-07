using System.Linq;
using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>
    /// Bulk retries follow the measured RTT and a backlog limit replaces the any-pending skip (#437). The selective
    /// ACK resends exactly the missing chunks, every gap at once, and never the ones the receiver holds.
    /// </summary>
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
        public void AGapIsResentAsSoonAsALaterChunkIsHeldAndRepeatsOfTheSameAckResendNothing()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 5; i++) sender.Enqueue("chunk" + i, 0d);
            Assert.That(sender.GetDue(0d, 10000).Length, Is.EqualTo(5));
            // Chunk 1 is lost. The ACK for the first of 2..5 to arrive already shows the gap: ack 0, holding 2 (bit 0).
            Assert.That(sender.Ack(0, 1L << 0, .02d), Is.True);
            Assert.That(sender.GetDue(.02d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 1 }),
                "No three repeats needed, and the held chunk is not sent again.");
            Assert.That(sender.HasRttSample, Is.True, "Chunk 2 was sent once, so its arrival times the path.");
            Assert.That(sender.Ack(0, 0b1111L, .03d), Is.True, "3..5 arrive too.");
            sender.Ack(0, 0b1111L, .04d); sender.Ack(0, 0b1111L, .04d);
            Assert.That(sender.GetDue(.04d, 10000), Is.Empty,
                "They were sent before the resend of 1, so they say nothing about whether that resend arrived.");
            double sampled = sender.SmoothedRttSeconds;
            Assert.That(sender.Ack(5, 0L, .06d), Is.True, "The resend filled the gap.");
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(sampled), "The ACK answered resent chunk 1: no sample (Karn).");
            Assert.That(sender.Count, Is.Zero);
            Assert.That(sender.QueuedBytes, Is.Zero);
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
            // Nothing arrives: a and b expire in one flush, and the timer doubles once, not twice.
            Assert.That(sender.GetDue(1.151d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 2, 3 }));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.3d).Within(1e-9));
            // c is newer than both, which still wait for their second timeout; its own timeout backs off too.
            sender.Enqueue("c", 1.2d);
            sender.GetDue(1.2d, 10000);
            Assert.That(sender.GetDue(1.501d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 4 }));
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(.6d).Within(1e-9));
        }

        [Test]
        public void EchoesOfResentCopiesSayNothingNewButALaterArrivalStillRevealsAGapAtOnce()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 4; i++) sender.Enqueue("early" + i, 0d);
            sender.GetDue(0d, 10000);
            Assert.That(sender.GetDue(.25d, 10000).Length, Is.EqualTo(4), "All four time out once.");
            sender.Ack(1, .3d); sender.Ack(2, .3d); sender.Ack(3, .3d); sender.Ack(4, .3d);
            sender.Enqueue("next0", .3d); sender.Enqueue("next1", .3d);
            sender.GetDue(.3d, 10000);
            // The four resent copies arrive now; each only repeats ACK 4 with nothing held after it.
            for (int i = 0; i < 4; i++) Assert.That(sender.Ack(4, 0L, .31d), Is.False);
            Assert.That(sender.GetDue(.31d, 10000), Is.Empty, "Repeats of the same state resend nothing.");
            // next0 (5) was lost: next1 (6, bit 0) was sent after it and arrives, which shows the gap at once.
            Assert.That(sender.Ack(4, 1L << 0, .33d), Is.True);
            Assert.That(sender.GetDue(.33d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 5 }));
        }

        [Test]
        public void HeldChunksAreNeverResentAndReleaseTheirBytesAtOnce()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 5; i++) sender.Enqueue("chunk" + i, 0d);
            sender.GetDue(0d, 10000);
            Assert.That(sender.QueuedBytes, Is.EqualTo(5 * 6));
            // Chunk 1 is lost; the receiver holds 2..5 (bits 0..3).
            Assert.That(sender.Ack(0, 0b1111L, .02d), Is.True);
            Assert.That(sender.QueuedBytes, Is.EqualTo(6), "Only the missing chunk still counts toward the backlog limit.");
            Assert.That(sender.Count, Is.EqualTo(5), "The window still starts at the gap.");
            Assert.That(sender.Acknowledged, Is.Zero);
            Assert.That(sender.GetDue(.02d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 1 }));
            Assert.That(sender.GetDue(5d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 1 }),
                "When the gap times out, the held chunks still do not go out again.");
            Assert.That(sender.Ack(5, 0L, 5.05d), Is.True);
            Assert.That(sender.QueuedBytes, Is.Zero, "Held bytes are not released twice.");
            Assert.That(sender.Count, Is.Zero);
        }

        [Test]
        public void EveryGapInTheWindowIsResentAtOnceWithoutBackingOff()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 8; i++) sender.Enqueue("chunk" + i, 0d);
            sender.GetDue(0d, 10000);
            Assert.That(sender.Ack(1, 0L, .05d), Is.True, "A timed sample: a timeout resend would now back off.");
            // 2, 5 and 6 are lost; 3, 4, 7 and 8 arrive (bit i = chunk 1 + 2 + i).
            long held = (1L << 0) | (1L << 1) | (1L << 4) | (1L << 5);
            Assert.That(sender.Ack(1, held, .05d), Is.True);
            double timeout = sender.RetryTimeoutSeconds;
            Assert.That(sender.GetDue(.05d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 2, 5, 6 }),
                "Not only the first gap: every chunk sent before an arrival and still missing is lost.");
            Assert.That(sender.RetryTimeoutSeconds, Is.EqualTo(timeout), "A resend after a later arrival is not a timeout.");
            Assert.That(sender.GetDue(.06d, 10000), Is.Empty);
        }

        [Test]
        public void ALostResendIsSentAgainOnlyAfterAChunkSentLaterArrives()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 3; i++) sender.Enqueue("a" + i, 0d);
            sender.GetDue(0d, 10000);
            sender.Ack(0, 0b11L, .02d);
            Assert.That(sender.GetDue(.02d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 1 }));
            // Another copy of an old ACK: nothing sent after the resend has arrived yet.
            Assert.That(sender.Ack(0, 0b11L, .03d), Is.False);
            Assert.That(sender.GetDue(.03d, 10000), Is.Empty, "No burst of resends while the resend is in flight.");
            sender.Enqueue("later", .05d);
            Assert.That(sender.GetDue(.05d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 4 }));
            // The resend of 1 was lost too, but 4, sent after it, arrived (bit 2).
            Assert.That(sender.Ack(0, 0b111L, .07d), Is.True);
            Assert.That(sender.GetDue(.07d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 1 }));
        }

        [Test]
        public void AnOlderAckStillAddsWhatItHeldAndAckingAnUnsentChunkChangesNothing()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 6; i++) sender.Enqueue("c" + i, 0d);
            sender.GetDue(0d, 10000);
            Assert.That(sender.Ack(2, 0L, .02d), Is.True);
            // An older ACK arrives late: through 1, holding 4 (bit 1) and 6 (bit 3).
            Assert.That(sender.Ack(1, (1L << 1) | (1L << 3), .03d), Is.True);
            Assert.That(sender.Acknowledged, Is.EqualTo(2), "The cumulative point never moves back.");
            Assert.That(sender.QueuedBytes, Is.EqualTo(2 * 2), "3 and 5 are still missing.");
            Assert.That(sender.Ack(7, 0L, .04d), Is.False, "7 was never sent.");
            Assert.That(sender.Ack(2, 1L << 10, .04d), Is.False, "A bit for chunk 14, never sent, is ignored.");
            Assert.That(sender.QueuedBytes, Is.EqualTo(2 * 2));
            Assert.That(sender.GetDue(.04d, 10000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 3, 5 }),
                "4 and 6, sent after them, arrived.");
        }

        [Test]
        public void AHeldChunkSentOnceGivesAnRttSampleEvenWhenTheGapBeforeItWasResent()
        {
            var sender = new LabBulkSender();
            for (int i = 0; i < 3; i++) sender.Enqueue("x" + i, 0d);
            sender.GetDue(0d, 10000);
            sender.Ack(0, 0b11L, .04d);
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(.04d).Within(1e-9), "3 was sent once at 0.");
            sender.GetDue(.04d, 10000);
            sender.Ack(3, 0L, .08d);
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(.04d).Within(1e-9), "Filling the gap answers a resend.");
            sender.Enqueue("y0", 1d); sender.Enqueue("y1", 1d);
            sender.GetDue(1d, 10000);
            // 4 is lost and 5 arrives. A cumulative-only ACK would wait for the resend of 4 and give no sample.
            sender.Ack(3, 1L << 0, 1.03d);
            Assert.That(sender.SmoothedRttSeconds, Is.EqualTo(.875d * .04d + .125d * .03d).Within(1e-9));
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
