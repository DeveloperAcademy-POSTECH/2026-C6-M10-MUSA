using System;
using NUnit.Framework;

namespace C6.Prototype.Presentation.Tests
{
    public sealed class ParticipantViewAngleTests
    {
        [TestCase(1, 5, 0f)]
        [TestCase(2, 5, 72f)]
        [TestCase(3, 5, 144f)]
        [TestCase(4, 5, 216f)]
        [TestCase(5, 5, 288f)]
        [TestCase(2, 2, 180f)]
        [TestCase(3, 3, 240f)]
        public void CalculateYawReturnsExpectedAngle(
            int playerNumber,
            int participantCount,
            float expectedYaw)
        {
            float actualYaw = ParticipantViewAngle.CalculateYaw(
                playerNumber,
                participantCount
            );

            Assert.That(
                actualYaw,
                Is.EqualTo(expectedYaw).Within(0.0001f)
            );
        }

        [TestCase(1)]
        [TestCase(6)]
        public void CalculateYawRejectsInvalidParticipantCount(
            int participantCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ParticipantViewAngle.CalculateYaw(1, participantCount)
            );
        }

        [TestCase(0, 3)]
        [TestCase(4, 3)]
        public void CalculateYawRejectsInvalidPlayerNumber(
            int playerNumber,
            int participantCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ParticipantViewAngle.CalculateYaw(
                    playerNumber,
                    participantCount
                )
            );
        }
    }
}
