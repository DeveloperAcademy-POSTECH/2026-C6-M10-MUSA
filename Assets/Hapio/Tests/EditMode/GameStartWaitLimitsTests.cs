using NUnit.Framework;

namespace Hapio.Tests
{
    /// <summary>Boundaries of a Guest's own battle-start limits (GameStartWaitLimits).</summary>
    public sealed class GameStartWaitLimitsTests
    {
        [Test]
        public void TheStartHoldEndsExactlyAtTenForegroundSeconds()
        {
            Assert.That(GameStartWaitLimits.StartHeldLimitSeconds, Is.EqualTo(10d));
            double elapsed = 0d;
            // 40 frames of .25 s reach 10 s on the last one, and not a frame earlier.
            for (int frame = 1; frame < 40; frame++)
                Assert.That(GameStartWaitLimits.Advance(ref elapsed, true, .25d, 10d), Is.False, "frame " + frame);
            Assert.That(elapsed, Is.EqualTo(9.75d));
            Assert.That(GameStartWaitLimits.Advance(ref elapsed, true, .25d, 10d), Is.True);
            Assert.That(elapsed, Is.EqualTo(10d));
        }

        [Test]
        public void TheCounterAddsNothingWhileNotCountingAndKeepsItsTotal()
        {
            double elapsed = 9.9d;
            Assert.That(GameStartWaitLimits.Advance(ref elapsed, false, .25d, 10d), Is.False);
            Assert.That(elapsed, Is.EqualTo(9.9d));
            Assert.That(GameStartWaitLimits.Advance(ref elapsed, true, .05d, 10d), Is.False);
            Assert.That(GameStartWaitLimits.Advance(ref elapsed, true, .05d, 10d), Is.True);
        }

        [Test]
        public void ABackgroundLengthFrameAddsOnlyOneClampedFrame()
        {
            // #502: a resume after 29 s in the background must not count those seconds.
            Assert.That(GameStartWaitLimits.MaximumFrameSeconds, Is.EqualTo(.25d));
            Assert.That(GameStartWaitLimits.ForegroundDelta(29d), Is.EqualTo(.25d));
            Assert.That(GameStartWaitLimits.ForegroundDelta(.016d), Is.EqualTo(.016d));
            Assert.That(GameStartWaitLimits.ForegroundDelta(-1d), Is.Zero);
            Assert.That(GameStartWaitLimits.ForegroundDelta(double.NaN), Is.Zero);
            Assert.That(GameStartWaitLimits.ForegroundDelta(double.PositiveInfinity), Is.EqualTo(.25d));
            double elapsed = 0d;
            Assert.That(GameStartWaitLimits.Advance(ref elapsed, true, 600d, 10d), Is.False);
            Assert.That(elapsed, Is.EqualTo(.25d));
        }

        [Test]
        public void ThePreparationLimitIsFortyFiveSecondsWithTheDefaultsAndFollowsTheStartLengths()
        {
            Assert.That(GameStartWaitLimits.PreparationLimitSeconds(30d, 3d, 5d), Is.EqualTo(45d));
            // The PlayMode suite's shortened start (GameTestStartTiming: 0.3 s countdown, 5 s lead cap).
            Assert.That(GameStartWaitLimits.PreparationLimitSeconds(30d, .3d, 5d), Is.EqualTo(42.3d).Within(1e-9));
            // It always stays above the Host's longest normal preparation: budget + countdown + maximum lead.
            Assert.That(GameStartWaitLimits.PreparationLimitSeconds(30d, 3d, 5d), Is.GreaterThan(30d + 3d + 5d));
            double elapsed = 0d;
            bool expired = false;
            int frames = 0;
            while (!expired && frames < 1000) { expired = GameStartWaitLimits.Advance(ref elapsed, true, .25d, 45d); frames++; }
            Assert.That(frames, Is.EqualTo(180));
        }

        [Test]
        public void TheHostRetriesItsOwnReleaseOnlyForHalfASecondAfterTheSharedStart()
        {
            Assert.That(GameStartWaitLimits.HostReleaseRetrySeconds, Is.EqualTo(.5d));
            Assert.That(GameStartWaitLimits.HostMayRetryRelease(100d, 100d), Is.True);
            Assert.That(GameStartWaitLimits.HostMayRetryRelease(100.499d, 100d), Is.True);
            Assert.That(GameStartWaitLimits.HostMayRetryRelease(100.5d, 100d), Is.False);
            Assert.That(GameStartWaitLimits.HostMayRetryRelease(101d, 100d), Is.False);
        }
    }
}
