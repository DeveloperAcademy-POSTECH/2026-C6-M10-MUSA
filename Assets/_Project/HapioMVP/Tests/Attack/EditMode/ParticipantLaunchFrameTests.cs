using NUnit.Framework;
using UnityEngine;

// 플레이어 각도에 따른 계산 테스트 용도 
namespace C6.Prototype.Attack.Tests
{
    public sealed class ParticipantLaunchFrameTests
    {
        private static readonly ProjectileLaunchBasis Baseline =
            new ProjectileLaunchBasis(
                new Vector3(0f, 1f, -5f),
                Vector3.right,
                3f,
                new Vector3(0f, 1f, 0f)
            );

        [Test]
        public void P1KeepsBaselineLaunchFrame()
        {
            ProjectileLaunchBasis result =
                ParticipantLaunchFrame.Calculate(Baseline, 1, 2);

            Assert.That(
                Vector3.Distance(result.Origin, Baseline.Origin),
                Is.LessThan(0.0001f)
            );

            Assert.That(
                Vector3.Distance(result.HorizontalAxis, Vector3.right),
                Is.LessThan(0.0001f)
            );
        }

        [Test]
        public void P2OfTwoPlayersUsesOppositeLaunchFrame()
        {
            ProjectileLaunchBasis result =
                ParticipantLaunchFrame.Calculate(Baseline, 2, 2);

            Assert.That(
                Vector3.Distance(
                    result.Origin,
                    new Vector3(0f, 1f, 5f)
                ),
                Is.LessThan(0.0001f)
            );

            Assert.That(
                Vector3.Distance(
                    result.HorizontalAxis,
                    Vector3.left
                ),
                Is.LessThan(0.0001f)
            );
        }

        [Test]
        public void FivePlayersKeepEqualAngleSpacing()
        {
            Vector3 previousDirection =
                ParticipantLaunchFrame
                    .Calculate(Baseline, 1, 5)
                    .Origin
                - Baseline.AimPoint;

            for (int playerNumber = 2; playerNumber <= 5; playerNumber++)
            {
                ProjectileLaunchBasis current =
                    ParticipantLaunchFrame.Calculate(
                        Baseline,
                        playerNumber,
                        5
                    );

                Vector3 currentDirection =
                    current.Origin - Baseline.AimPoint;

                Assert.That(
                    Vector3.Angle(
                        previousDirection,
                        currentDirection
                    ),
                    Is.EqualTo(72f).Within(0.0001f)
                );

                Assert.That(
                    currentDirection.magnitude,
                    Is.EqualTo(previousDirection.magnitude)
                        .Within(0.0001f)
                );

                previousDirection = currentDirection;
            }
        }
    }
}