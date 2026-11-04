using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>The Guest's presentation clock never steps back by jitter, and still accepts real corrections (#451).</summary>
    public sealed class LabGuestBattleClockTests
    {
        [Test]
        public void ItExtrapolatesAndHoldsWhenALaterSampleIsSlightlyBehind()
        {
            var clock = new LabGuestBattleClock();
            Assert.That(clock.Sample("s", 1, 10d, false, 0d), Is.EqualTo(10d));
            Assert.That(clock.Sample("s", 1, 10d, false, .3d), Is.EqualTo(10.3d).Within(1e-9));
            Assert.That(clock.Sample("s", 1, 10.25d, false, .3d), Is.EqualTo(10.3d).Within(1e-9), "A late sample does not rewind.");
            Assert.That(clock.Sample("s", 1, 10.25d, false, .34d), Is.EqualTo(10.3d).Within(1e-9), "It waits for real time to catch up.");
            Assert.That(clock.Sample("s", 1, 10.25d, false, .5d), Is.EqualTo(10.45d).Within(1e-9), "Rebased on the late sample at .3.");
        }

        [Test]
        public void ARepeatedSampleWithTheSameValuesKeepsExtrapolating()
        {
            var clock = new LabGuestBattleClock();
            clock.Sample("s", 1, 10d, false, 0d);
            // The old estimator restarted from serverTime whenever a new snapshot object arrived, stepping back .3 s.
            Assert.That(clock.Sample("s", 1, 10d, false, .3d), Is.EqualTo(10.3d).Within(1e-9));
            Assert.That(clock.Sample("s", 1, 10d, false, .3d), Is.EqualTo(10.3d).Within(1e-9));
        }

        [Test]
        public void ExtrapolationStopsAfterOneSecondWithoutANewSample()
        {
            var clock = new LabGuestBattleClock();
            clock.Sample("s", 1, 10d, false, 0d);
            Assert.That(clock.Sample("s", 1, 10d, false, 5d), Is.EqualTo(10d + LabGuestBattleClock.MaximumExtrapolationSeconds));
            Assert.That(clock.Sample("s", 1, 11.2d, false, 5d), Is.EqualTo(11.2d), "A new sample moves it on.");
        }

        [Test]
        public void ReachingTheCapReportsAStallUntilANewSampleOrAFrozenOne()
        {
            var clock = new LabGuestBattleClock();
            clock.Sample("s", 1, 10d, false, 0d);
            Assert.That(clock.Stalled, Is.False);
            clock.Sample("s", 1, 10d, false, .99d);
            Assert.That(clock.Stalled, Is.False);
            clock.Sample("s", 1, 10d, false, 1d);
            Assert.That(clock.Stalled, Is.True, "No Host time for a second: the clock stands still on its own.");
            clock.Sample("s", 1, 11.1d, false, 1.2d);
            Assert.That(clock.Stalled, Is.False);
            clock.Sample("s", 1, 11.1d, true, 5d);
            Assert.That(clock.Stalled, Is.False, "A pause is held, not stalled.");
            clock.Sample("s", 1, 11.1d, false, 5d);
            clock.Sample("s", 1, 11.1d, false, 7d);
            Assert.That(clock.Stalled, Is.True);
            clock.Reset();
            Assert.That(clock.Stalled, Is.False);
        }

        [Test]
        public void AFrozenSampleHoldsTheClockAndResumingRebasesWithoutRewinding()
        {
            var clock = new LabGuestBattleClock();
            clock.Sample("s", 1, 10d, false, 0d);
            Assert.That(clock.Sample("s", 1, 10d, false, .2d), Is.EqualTo(10.2d).Within(1e-9));
            Assert.That(clock.Sample("s", 1, 10.1d, true, .25d), Is.EqualTo(10.2d).Within(1e-9), "Paused just behind the extrapolation: held.");
            Assert.That(clock.Sample("s", 1, 10.1d, true, 3d), Is.EqualTo(10.2d).Within(1e-9), "No time passes while frozen.");
            Assert.That(clock.Sample("s", 1, 10.1d, false, 3d), Is.EqualTo(10.2d).Within(1e-9), "Resume rebases at the Host time.");
            Assert.That(clock.Sample("s", 1, 10.1d, false, 3.2d), Is.EqualTo(10.3d).Within(1e-9));

            var paused = new LabGuestBattleClock();
            Assert.That(paused.Sample("s", 1, 7d, true, 0d), Is.EqualTo(7d));
            Assert.That(paused.Sample("s", 1, 7d, true, 2d), Is.EqualTo(7d));
            Assert.That(paused.Sample("s", 1, 7d, false, 2d), Is.EqualTo(7d));
            Assert.That(paused.Sample("s", 1, 7d, false, 2.4d), Is.EqualTo(7.4d).Within(1e-9));
        }

        [Test]
        public void ALargeRewindANewRoundOrANewSessionIsAccepted()
        {
            var clock = new LabGuestBattleClock();
            clock.Sample("s", 1, 10d, false, 0d);
            Assert.That(clock.Sample("s", 1, 10d, false, .3d), Is.EqualTo(10.3d).Within(1e-9));
            Assert.That(clock.Sample("s", 1, 9.8d, false, .3d), Is.EqualTo(9.8d).Within(1e-9), "Exactly half a second back is a correction.");
            Assert.That(clock.Sample("s", 1, 9.8d, false, .4d), Is.EqualTo(9.9d).Within(1e-9));
            Assert.That(clock.Sample("s", 2, 0d, false, .4d), Is.EqualTo(0d), "A new round starts again.");
            Assert.That(clock.Sample("s", 2, 0d, false, .6d), Is.EqualTo(.2d).Within(1e-9));
            Assert.That(clock.Sample("t", 2, 0d, false, .6d), Is.EqualTo(0d), "So does a new session.");
        }

        [Test]
        public void AnInvalidSampleKeepsTheLastTimeAndResetForgetsEverything()
        {
            var clock = new LabGuestBattleClock();
            Assert.That(clock.Sample("s", 1, double.NaN, false, 0d), Is.Zero);
            clock.Sample("s", 1, 10d, false, 0d);
            Assert.That(clock.Sample("s", 1, double.PositiveInfinity, false, .2d), Is.EqualTo(10d));
            Assert.That(clock.Sample("s", 1, 10d, false, .2d), Is.EqualTo(10.2d).Within(1e-9), "The invalid sample did not move the base.");
            clock.Reset();
            Assert.That(clock.Sample("s", 1, 9.9d, false, .2d), Is.EqualTo(9.9d), "After Reset a small rewind is accepted.");
        }
    }
}
