using NUnit.Framework;

namespace C6.Prototype.GameSync.Tests
{
    public sealed class PeerResponseWatchdogTests
    {
        [Test]
        public void NoResponseExpiresAtTheExactEightSecondBoundary()
        {
            var watch = new PeerResponseWatchdog();
            Assert.That(watch.IsExpired(100, 8), Is.False, "An unattached game has no peer deadline.");
            Assert.That(watch.Begin(10), Is.True);
            Assert.That(watch.IsArmed, Is.True);
            Assert.That(watch.IsExpired(17.999, 8), Is.False);
            Assert.That(watch.IsExpired(18, 8), Is.True);
            Assert.That(watch.IsExpired(100, 8), Is.True);
        }

        [Test]
        public void VerifiedResponseStartsANewDeadlineWithoutCountingUnobservedTimeAsAResponse()
        {
            var watch = new PeerResponseWatchdog();
            watch.Begin(10);
            Assert.That(watch.Observe(17), Is.True);
            Assert.That(watch.LastResponseAt, Is.EqualTo(17));
            Assert.That(watch.IsExpired(18, 8), Is.False);
            Assert.That(watch.IsExpired(24.999, 8), Is.False);
            Assert.That(watch.IsExpired(25, 8), Is.True);
        }

        [Test]
        public void BackwardAndEqualResponseTimesCannotExtendTheCurrentDeadline()
        {
            var watch = new PeerResponseWatchdog();
            watch.Begin(10);
            watch.Observe(17);
            Assert.That(watch.Observe(16), Is.False);
            Assert.That(watch.Observe(17), Is.True, "Several validated messages may arrive in the same frame.");
            Assert.That(watch.LastResponseAt, Is.EqualTo(17));
            Assert.That(watch.IsExpired(25, 8), Is.True);
        }

        [Test]
        public void RepeatedBeginDoesNotExtendAnArmedDeadline()
        {
            var watch = new PeerResponseWatchdog();
            watch.Begin(10);
            Assert.That(watch.Begin(17), Is.False);
            Assert.That(watch.Begin(9), Is.False);
            Assert.That(watch.LastResponseAt, Is.EqualTo(10));
            Assert.That(watch.IsExpired(18, 8), Is.True);
        }

        [Test]
        public void AResponseBeforeInitializationCannotArmTheWatchdog()
        {
            var watch = new PeerResponseWatchdog();
            Assert.That(watch.Observe(100), Is.False);
            Assert.That(watch.IsArmed, Is.False);
            Assert.That(watch.LastResponseAt, Is.Zero);
            Assert.That(watch.IsExpired(200, 8), Is.False);
        }

        [Test]
        public void ResetDisablesOldDeadlineAndPermitsANewClockOrigin()
        {
            var watch = new PeerResponseWatchdog();
            watch.Begin(100);
            watch.Observe(104);
            watch.Reset();
            Assert.That(watch.IsArmed, Is.False);
            Assert.That(watch.LastResponseAt, Is.Zero);
            Assert.That(watch.IsExpired(1000, 8), Is.False);
            Assert.That(watch.Observe(1000), Is.False, "A late old-session callback cannot rearm tracking.");
            Assert.That(watch.Begin(0), Is.True);
            Assert.That(watch.IsExpired(8, 8), Is.True);
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidInitialTimeLeavesTrackingInactive(double invalid)
        {
            var watch = new PeerResponseWatchdog();
            Assert.That(watch.Begin(invalid), Is.False);
            Assert.That(watch.IsArmed, Is.False);
            Assert.That(watch.LastResponseAt, Is.Zero);
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidResponseTimeDoesNotPoisonOrExtendTheDeadline(double invalid)
        {
            var watch = new PeerResponseWatchdog();
            watch.Begin(10);
            Assert.That(watch.Observe(invalid), Is.False);
            Assert.That(watch.LastResponseAt, Is.EqualTo(10));
            Assert.That(watch.IsArmed, Is.True);
            Assert.That(watch.IsExpired(18, 8), Is.True);
        }

        [TestCase(-1d)]
        [TestCase(9d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidOrEarlierClockObservationDoesNotManufactureAConnectionFailure(double invalid)
        {
            var watch = new PeerResponseWatchdog();
            watch.Begin(10);
            Assert.That(watch.IsExpired(invalid, 8), Is.False);
            Assert.That(watch.LastResponseAt, Is.EqualTo(10));
            Assert.That(watch.IsExpired(18, 8), Is.True, "A rejected observation does not move the deadline.");
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidTimeoutCannotCreateOrResetADeadline(double invalid)
        {
            var watch = new PeerResponseWatchdog();
            watch.Begin(10);
            Assert.That(watch.IsExpired(100, invalid), Is.False);
            Assert.That(watch.IsArmed, Is.True);
            Assert.That(watch.LastResponseAt, Is.EqualTo(10));
            Assert.That(watch.IsExpired(18, 8), Is.True);
        }
    }
}
