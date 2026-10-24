using System.Linq;
using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>2:2 청홍 땅따먹기 (#281): screen-side seat rules read from a snapshot.</summary>
    public sealed class LabTeamScreenRulesTests
    {
        private static LabSnapshot Team(int count = 4) => new LabSnapshot
        {
            teamTerritory = true,
            players = Enumerable.Range(0, count).Select(seat => new LabPlayerState { id = (ulong)(seat + 1) * 10, seat = seat }).ToArray()
        };

        [Test]
        public void TheRingAlternatesTeamsOnlyInAFourPlayerTeamBattle()
        {
            Assert.That(Enumerable.Range(0, 4).Select(seat => LabTeams.RingSeatFor(Team(), seat)), Is.EqualTo(new[] { 0, 2, 1, 3 }));
            LabSnapshot story = Team(); story.teamTerritory = false;
            Assert.That(Enumerable.Range(0, 4).Select(seat => LabTeams.RingSeatFor(story, seat)), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(LabTeams.RingSeatFor(Team(1), 0), Is.Zero);
            Assert.That(LabTeams.RingSeatFor(null, 2), Is.EqualTo(2));
        }

        [Test]
        public void EachBoardClosesItsOuterEdgeAndShowsOnlyTheTeammateBeside()
        {
            LabSnapshot snapshot = Team();
            Assert.That(new ulong[] { 10, 20, 30, 40 }.Select(id => LabTeams.ClosedSide(snapshot, id)), Is.EqualTo(new[] { -1, 1, -1, 1 }));
            Assert.That(LabTeams.ClosedSide(Team(1), 10), Is.Zero, "Solo has no neighbour and no wall.");
            string[] left = new string[4], right = new string[4];
            for (int i = 0; i < 4; i++) LabHud.GetNeighborLabels(snapshot, (ulong)(i + 1) * 10, out left[i], out right[i]);
            Assert.That(left, Is.EqualTo(new[] { "P—", "P1", "P—", "P3" }));
            Assert.That(right, Is.EqualTo(new[] { "P2", "P—", "P4", "P—" }));
            Assert.That(LabTeams.TeamOf(snapshot, 30), Is.EqualTo(LabTeam.Red));
            Assert.That(LabTeams.TeamOf(Team(1), 10), Is.EqualTo(LabTeam.Blue));
            Assert.That(LabTeams.VisibleTeammates(snapshot, 40).Select(player => player.id), Is.EqualTo(new ulong[] { 30, 40 }));
            LabSnapshot story = Team(); story.teamTerritory = false;
            Assert.That(LabTeams.VisibleTeammates(story, 40), Has.Length.EqualTo(4));
        }

        [Test]
        public void TheBarSharesAndResultWordsFollowTheSnapshot()
        {
            Assert.That(LabTeamBar.Share(512, 2048), Is.EqualTo(.25f));
            Assert.That(LabTeamBar.Share(5, 0), Is.Zero);
            var ended = new LabSnapshot { phase = LabPhase.Victory, teamTerritory = true, teamWinner = LabTeam.Blue };
            Assert.That(LabTeamBar.ResultText(ended), Is.EqualTo("청팀 승리!"));
            ended.teamWinner = LabTeam.Red;
            Assert.That(LabTeamBar.ResultText(ended), Is.EqualTo("홍팀 승리!"));
            ended.teamWinner = LabTeam.None;
            Assert.That(LabTeamBar.ResultText(ended), Is.EqualTo("무승부"));
            ended.phase = LabPhase.Playing;
            Assert.That(LabTeamBar.ResultText(ended), Is.Empty);
        }
    }
}
