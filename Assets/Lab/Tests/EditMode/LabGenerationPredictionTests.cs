using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>Only a drum tap the Host is certain to approve plays its feedback before the receipt (#457).</summary>
    public sealed class LabGenerationPredictionTests
    {
        private const float Cost = 20f;
        private const int Limit = 20;
        private const int Reserve = 10; // 2 neighbours + fox fire 5 + five-colour 3, nothing being opened.

        [Test]
        public void EnoughStaminaForEveryPendingTapAndRoomForEveryPossibleArrivalIsCertain()
        {
            Assert.That(Certain(stamina: 20f, pending: 1, stored: 0), Is.True);
            Assert.That(Certain(stamina: 60f, pending: 3, stored: 7), Is.True, "7 + 3 + 10 fills the limit exactly.");
            Assert.That(Certain(stamina: 59.999995f, pending: 3, stored: 0), Is.True, "The Host's own tolerance.");
        }

        [Test]
        public void StaminaThatCannotCoverEveryPendingTapWaitsForTheReceipt()
        {
            Assert.That(Certain(stamina: 19f, pending: 1, stored: 0), Is.False);
            Assert.That(Certain(stamina: 59f, pending: 3, stored: 0), Is.False, "The earlier taps spend it first.");
        }

        [Test]
        public void ABoardWithoutRoomForTheWorstUnseenArrivalsWaitsForTheReceipt()
        {
            Assert.That(Certain(stamina: 120f, pending: 1, stored: 9), Is.True);
            Assert.That(Certain(stamina: 120f, pending: 1, stored: 10), Is.False,
                "A fox fire of 5, a five-colour bonus of 3 and two passes could fill it before the Host reaches the tap.");
            Assert.That(Certain(stamina: 120f, pending: 2, stored: 9), Is.False);
            Assert.That(LabGenerationPrediction.Certain(120f, Cost, 1, 9, Limit,
                LabGenerationPrediction.ArrivalReserve(5, 3, 1), true, false, false), Is.False, "An item being opened adds one more.");
        }

        [Test]
        public void TheReserveCoversBothNeighboursFoxFireTheFiveColourBonusAndItemOpens()
        {
            Assert.That(LabGenerationPrediction.ArrivalReserve(5, 3, 0), Is.EqualTo(Reserve));
            Assert.That(LabGenerationPrediction.ArrivalReserve(5, 3, 2), Is.EqualTo(Reserve + 2));
            Assert.That(LabGenerationPrediction.ArrivalReserve(-1, -1, -1), Is.EqualTo(2));
        }

        [TestCase(false, false, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        public void NotPlayingHeldOrAJigwiRiskWaitsForTheReceipt(bool playing, bool held, bool jigwiRisk)
        {
            Assert.That(LabGenerationPrediction.Certain(120f, Cost, 1, 0, Limit, Reserve, playing, held, jigwiRisk), Is.False);
        }

        [Test]
        public void InvalidInputsAreNeverCertain()
        {
            Assert.That(Certain(stamina: float.NaN, pending: 1, stored: 0), Is.False);
            Assert.That(Certain(stamina: 120f, pending: 0, stored: 0), Is.False);
            Assert.That(Certain(stamina: 120f, pending: 1, stored: -1), Is.False);
            Assert.That(LabGenerationPrediction.Certain(120f, 0f, 1, 0, Limit, Reserve, true, false, false), Is.False);
            Assert.That(LabGenerationPrediction.Certain(120f, Cost, 1, 0, 0, Reserve, true, false, false), Is.False);
            Assert.That(LabGenerationPrediction.Certain(120f, Cost, 1, 0, Limit, -1, true, false, false), Is.False);
        }

        private static bool Certain(float stamina, int pending, int stored)
            => LabGenerationPrediction.Certain(stamina, Cost, pending, stored, Limit, Reserve, true, false, false);
    }
}
