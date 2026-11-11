using System;
using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>여우구슬 팀 판정 (#664): 판 헤더가 판정 시각·떨어진 수·목표치·지운 수·지난 결과를 그대로 나른다.</summary>
    public sealed class LabFoxRoundReplicationTests
    {
        private static LabSnapshot Snapshot(double serverTime) => new LabSnapshot
        {
            sessionId = "fox", round = 1, phase = LabPhase.Playing, serverTime = serverTime,
            remaining = 99d, playedSeconds = serverTime,
            players = new[] { new LabPlayerState { id = 0, seat = 0, stamina = 100f } },
            orbs = Array.Empty<LabOrbState>()
        };

        [Test]
        public void TheBoardHeaderCarriesTheFoxRoundItsGoalAndTheLastResult()
        {
            LabSnapshot source = Snapshot(21d);
            source.foxRoundStartedAt = 20d; source.foxRoundEndsAt = 35d; source.foxRoundRunning = true;
            source.foxRoundTotal = 12; source.foxRoundRemaining = 9; source.foxRoundGoal = 9; source.foxRoundCleared = 3;
            source.foxRoundResult = -1; source.foxRoundResultAt = 8d; source.foxRoundCount = 1;
            LabBoardFrame frame = new LabBoardPublisher().Build(source, 0, 1, 1);
            var replica = new LabBoardReplica();
            replica.SetActive("fox", 1, 0);
            Assert.That(replica.TryApply(frame, out string reason), Is.True, reason);

            LabSnapshot seen = replica.Overlay(Snapshot(.5d));
            Assert.That((seen.foxRoundStartedAt, seen.foxRoundEndsAt, seen.foxRoundRunning), Is.EqualTo((20d, 35d, true)));
            Assert.That((seen.foxRoundTotal, seen.foxRoundRemaining, seen.foxRoundGoal, seen.foxRoundCleared),
                Is.EqualTo((12, 9, 9, 3)));
            Assert.That((seen.foxRoundResult, seen.foxRoundResultAt, seen.foxRoundCount), Is.EqualTo((-1, 8d, 1)),
                "The last SMALL stays while the next round runs.");
            Assert.That(seen.FoxRoundOn(21d), Is.True);
            Assert.That(seen.FoxRoundToGo, Is.EqualTo(6), "Nine to clear, three cleared.");

            LabBoardFrame bad = new LabBoardPublisher().Build(source, 0, 1, 1);
            bad.foxRoundGoal = -1;
            var other = new LabBoardReplica();
            other.SetActive("fox", 1, 0);
            Assert.That(other.TryApply(bad, out _), Is.False, "A negative goal is refused.");
        }
    }
}
