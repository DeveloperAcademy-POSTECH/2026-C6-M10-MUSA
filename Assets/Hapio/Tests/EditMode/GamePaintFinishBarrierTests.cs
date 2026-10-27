using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GamePaintFinishBarrierTests
    {
        [Test]
        public void EveryParticipantMustInstallTheFinalMapBeforeScheduling()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 0, 7, 9 }, 0d);
            Assert.That(barrier.Prepared(42), Is.False);
            Assert.That(barrier.Prepared(0), Is.True);
            Assert.That(barrier.Prepared(0), Is.False);
            Assert.That(barrier.Prepared(7), Is.True);
            Assert.That(barrier.Schedule(100d), Is.False);
            Assert.That(barrier.Prepared(9), Is.True);
            Assert.That(barrier.Schedule(100d), Is.True);
            Assert.That(barrier.Generation, Is.EqualTo(1u));
            Assert.That(barrier.StartsAt, Is.EqualTo(100d + GamePaintFinishBarrier.LeadSeconds));
        }
        [Test]
        public void LateScheduleReceiptsCauseANewGenerationAndOldReceiptsCannotCommitIt()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 0, 7 }, 0d);
            barrier.Prepared(0); barrier.Prepared(7); barrier.Schedule(10d);
            Assert.That(barrier.Scheduled(0, 1), Is.True);
            Assert.That(barrier.Commit(10.1d), Is.False);
            Assert.That(barrier.Schedule(10.6d), Is.True);
            Assert.That(barrier.Generation, Is.EqualTo(2u));
            Assert.That(barrier.Scheduled(7, 1), Is.False);
            Assert.That(barrier.Scheduled(0, 2), Is.True);
            Assert.That(barrier.Scheduled(7, 2), Is.True);
            Assert.That(barrier.Commit(10.7d), Is.True);
            Assert.That(barrier.Schedule(12d), Is.False, "A committed shared start cannot be replaced.");
        }
        [Test]
        public void ReceiptsInsideTheLastQuarterSecondCommitWithoutDiscardingTheGeneration()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 0, 7 }, 0d);
            barrier.Prepared(0); barrier.Prepared(7); barrier.Schedule(10d);
            barrier.Scheduled(0, 1); barrier.Scheduled(7, 1);
            double lastMoment = barrier.StartsAt - .01d;
            Assert.That(barrier.Schedule(lastMoment), Is.False, "A fully acknowledged reservation remains valid until its deadline.");
            Assert.That(barrier.Generation, Is.EqualTo(1u));
            Assert.That(barrier.Commit(lastMoment), Is.True);
        }
        [Test]
        public void AlreadyMissedScheduleIsReplacedEvenWhenEveryLateReceiptArrived()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 0, 7 }, 0d);
            barrier.Prepared(0); barrier.Prepared(7); barrier.Schedule(10d);
            barrier.Scheduled(0, 1); barrier.Scheduled(7, 1);
            Assert.That(barrier.Commit(barrier.StartsAt), Is.False);
            Assert.That(barrier.Schedule(barrier.StartsAt), Is.True);
            Assert.That(barrier.Generation, Is.EqualTo(2u));
            Assert.That(barrier.EveryoneScheduled, Is.False);
        }
        [Test]
        public void ResultRequiresTheFullRealDurationAndEveryFinishedReceipt()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 0, 7 }, 0d);
            barrier.Prepared(0); barrier.Prepared(7); barrier.Schedule(10d);
            barrier.Scheduled(0, 1); barrier.Scheduled(7, 1); barrier.Commit(10.1d);
            Assert.That(barrier.Completed(0, 1), Is.True);
            Assert.That(barrier.TryRelease(30d, 1.2d), Is.False);
            Assert.That(barrier.Completed(7, 2), Is.False);
            Assert.That(barrier.Completed(7, 1), Is.True);
            Assert.That(barrier.TryRelease(11d, 1.2d), Is.False);
            Assert.That(barrier.TryRelease(12d, 1.2d), Is.True);
            Assert.That(barrier.Skipped, Is.False);
        }
        [Test]
        public void MissingPeerHasAFinitePresentationFallbackWithoutManufacturingACompletedReceipt()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 0, 7 }, 50d);
            barrier.Prepared(0);
            Assert.That(barrier.Expire(61.99d), Is.False);
            Assert.That(barrier.Expire(62d), Is.True);
            Assert.That(barrier.Released && barrier.Skipped, Is.True);
            Assert.That(barrier.EveryoneCompleted, Is.False);
            Assert.That(barrier.Prepared(7), Is.False);
            Assert.That(barrier.Schedule(100d), Is.False);
        }
        [Test]
        public void SoloUsesTheSamePrepareScheduleCommitAndCompletionBarrier()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 4 }, 0d);
            barrier.Prepared(4); barrier.Schedule(1d); barrier.Scheduled(4, 1);
            Assert.That(barrier.Commit(1.1d), Is.True);
            Assert.That(barrier.Completed(4, 1), Is.True);
            Assert.That(barrier.TryRelease(3d, 1.2d), Is.True);
            Assert.That(barrier.Expire(100d), Is.False);
        }
        [Test]
        public void InvalidClockCannotAuthorizeOrReleaseAnEffect()
        {
            var barrier = new GamePaintFinishBarrier(new ulong[] { 0 }, 0d);
            barrier.Prepared(0);
            Assert.That(barrier.Schedule(double.NaN), Is.False);
            Assert.That(barrier.Schedule(double.PositiveInfinity), Is.False);
            barrier.Schedule(1d); barrier.Scheduled(0, 1);
            Assert.That(barrier.Commit(barrier.StartsAt), Is.False);
            Assert.That(barrier.Expire(double.NaN), Is.False);
        }
    }
}
