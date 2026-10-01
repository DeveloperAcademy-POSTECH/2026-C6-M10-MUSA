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
    }
}
