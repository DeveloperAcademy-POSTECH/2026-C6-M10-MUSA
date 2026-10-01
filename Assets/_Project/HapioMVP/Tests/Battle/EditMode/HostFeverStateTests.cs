using NUnit.Framework;

namespace C6.Prototype.Battle.Tests
{
    public sealed class HostFeverStateTests
    {
        private HostFeverState fever;

        [SetUp]
        public void SetUp()
        {
            fever = new HostFeverState();
            fever.BeginRound();
        }

        [Test]
        public void FiveUniqueValidHitsStartOneTenSecondFever()
        {
            for (int index = 1; index < 5; index++)
            {
                Assert.That(fever.RecordValidHit("hit-" + index, index), Is.EqualTo(FeverUpdateResult.Charged));
                Assert.That(fever.GaugePercent, Is.EqualTo(index * 20));
                Assert.That(fever.Active, Is.False);
            }

            Assert.That(fever.RecordValidHit("hit-5", 5), Is.EqualTo(FeverUpdateResult.Started));
            Assert.That(fever.Active, Is.True);
            Assert.That(fever.Sequence, Is.EqualTo(1));
            Assert.That(fever.GaugePercent, Is.EqualTo(100));
            Assert.That(fever.StartsAt, Is.EqualTo(5));
            Assert.That(fever.EndsAt, Is.EqualTo(15));
        }

        [Test]
        public void DuplicateAndHitsDuringFeverNeverChargeOrRetrigger()
        {
            for (int index = 1; index <= 5; index++) fever.RecordValidHit("hit-" + index, index);
            Assert.That(fever.RecordValidHit("hit-5", 6), Is.EqualTo(FeverUpdateResult.None));
            Assert.That(fever.RecordValidHit("hit-6", 6), Is.EqualTo(FeverUpdateResult.None));
            Assert.That(fever.Sequence, Is.EqualTo(1));
            Assert.That(fever.GaugePercent, Is.EqualTo(100));
        }

        [Test]
        public void EndResetsGaugeAndLaterUniqueHitsCanStartAnotherFever()
        {
            for (int index = 1; index <= 5; index++) fever.RecordValidHit("first-" + index, index);
            Assert.That(fever.Tick(14.999), Is.EqualTo(FeverUpdateResult.None));
            Assert.That(fever.Tick(15), Is.EqualTo(FeverUpdateResult.Ended));
            Assert.That(fever.Active, Is.False);
            Assert.That(fever.GaugePercent, Is.Zero);
            Assert.That(fever.StartsAt, Is.Zero);
            Assert.That(fever.EndsAt, Is.Zero);

            for (int index = 1; index <= 5; index++) fever.RecordValidHit("second-" + index, 20 + index);
            Assert.That(fever.Active, Is.True);
            Assert.That(fever.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void NewRoundClearsActiveStateSequenceAndDuplicateMemory()
        {
            fever.RecordValidHit("same-hit", 1);
            fever.BeginRound();
            Assert.That(fever.RecordValidHit("same-hit", 2), Is.EqualTo(FeverUpdateResult.Charged));
            Assert.That(fever.Sequence, Is.Zero);
            Assert.That(fever.GaugePercent, Is.EqualTo(20));
        }

        [Test]
        public void ActiveFeverPresentationCountsDownFromFullToEmpty()
        {
            Assert.That(T09Hud.FeverDisplayFraction(80, false, null, 0d, 0d), Is.EqualTo(.8f));
            Assert.That(T09Hud.FeverDisplayFraction(100, true, 10d, 10d, 20d), Is.EqualTo(1f));
            Assert.That(T09Hud.FeverDisplayFraction(100, true, 15d, 10d, 20d), Is.EqualTo(.5f));
            Assert.That(T09Hud.FeverDisplayFraction(100, true, 19d, 10d, 20d), Is.EqualTo(.1f).Within(.0001f));
            Assert.That(T09Hud.FeverDisplayFraction(100, true, 20d, 10d, 20d), Is.Zero);
        }

        [Test]
        public void FeverPulseAlternatesBetweenDimAndBrightWithoutLeavingValidRange()
        {
            Assert.That(T09Hud.FeverPulse01(.125d), Is.EqualTo(1f).Within(.0001f));
            Assert.That(T09Hud.FeverPulse01(.375d), Is.EqualTo(0f).Within(.0001f));
            Assert.That(T09Hud.FeverPulse01(double.NaN), Is.Zero);
        }
    }
}
