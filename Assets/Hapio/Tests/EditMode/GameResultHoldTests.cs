using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameResultHoldTests
    {
        [TearDown]
        public void RestoreDefaults() => GameResultHold.Reset();

        [Test]
        public void TheGameUsesTheApprovedLimits()
        {
            GameResultHold.Reset();
            Assert.That(GameResultHold.ResultReadySeconds, Is.EqualTo(30d));
            Assert.That(GameResultHold.NoticeSeconds, Is.EqualTo(45d));
            Assert.That(GameResultHold.OverrideSeconds, Is.EqualTo(120d));
            Assert.That(GameResultHold.IdleCloseSeconds, Is.EqualTo(60d));
        }

        [Test]
        public void AHoldNamesItsCauseAt45SecondsAndLetsTheHostStartAt120()
        {
            Assert.That(GameResultHold.HoldStage(-1d, 1000d), Is.EqualTo(GameResultHold.Stage.None), "No hold started.");
            Assert.That(GameResultHold.HoldStage(10d, 10d), Is.EqualTo(GameResultHold.Stage.None));
            Assert.That(GameResultHold.HoldStage(10d, 54.999d), Is.EqualTo(GameResultHold.Stage.None));
            Assert.That(GameResultHold.HoldStage(10d, 55d), Is.EqualTo(GameResultHold.Stage.Notice));
            Assert.That(GameResultHold.HoldStage(10d, 129.999d), Is.EqualTo(GameResultHold.Stage.Notice));
            Assert.That(GameResultHold.HoldStage(10d, 130d), Is.EqualTo(GameResultHold.Stage.Override));
            Assert.That(GameResultHold.HoldStage(10d, double.NaN), Is.EqualTo(GameResultHold.Stage.None));
        }

        [Test]
        public void AMissingResultExpiresAt30SecondsAndAnIdlePopupAt60()
        {
            Assert.That(GameResultHold.ResultReadyExpired(-1d, 1000d), Is.False, "The Host has not reported its own result.");
            Assert.That(GameResultHold.ResultReadyExpired(5d, 34.999d), Is.False);
            Assert.That(GameResultHold.ResultReadyExpired(5d, 35d), Is.True);
            Assert.That(GameResultHold.IdleExpired(-1d, 1000d), Is.False, "Nothing is open.");
            Assert.That(GameResultHold.IdleExpired(2d, 61.999d), Is.False);
            Assert.That(GameResultHold.IdleExpired(2d, 62d), Is.True);
        }

        [Test]
        public void TestsCanShortenTheLimitsOnlyInOrder()
        {
            GameResultHold.Configure(.5d, 1d, 2d, 3d);
            Assert.That(GameResultHold.HoldStage(0d, 2d), Is.EqualTo(GameResultHold.Stage.Override));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => GameResultHold.Configure(1d, 3d, 2d, 1d),
                "The override can never come before the notice.");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => GameResultHold.Configure(0d, 1d, 2d, 1d));
            Assert.That(GameResultHold.ResultReadySeconds, Is.EqualTo(.5d), "A refused change keeps the previous limits.");
        }
    }
}
