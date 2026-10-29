using C6Lab;

namespace Hapio
{
    public sealed partial class GameSession
    {
        // 2:2 (#293): the Host's snapshot only says when the current team fever began, so each device counts the fevers
        // it sees start during the battle for the result table. A new battle (session or round) starts again from zero.
        private string feverCountBattle;
        private readonly double[] lastFeverStart = { -1d, -1d, -1d };
        private readonly int[] feverCounts = new int[3];

        /// <summary>How many fevers this team started in the latest 2:2 battle, as this device saw them.</summary>
        public int TerritoryFeverCount(LabTeam team) =>
            team == LabTeam.Blue || team == LabTeam.Red ? feverCounts[(int)team] : 0;

        private void ObserveTerritoryFevers()
        {
            LabSnapshot snapshot = IsTerritory && network != null ? network.Snapshot : null;
            if (snapshot == null || !snapshot.teamTerritory || snapshot.phase != LabPhase.Playing) return;
            string battle = snapshot.sessionId + ":" + snapshot.round;
            if (battle != feverCountBattle)
            {
                feverCountBattle = battle;
                for (int i = 0; i < feverCounts.Length; i++) { feverCounts[i] = 0; lastFeverStart[i] = -1d; }
            }
            CountFever(LabTeam.Blue, snapshot.blueFeverStartedAt);
            CountFever(LabTeam.Red, snapshot.redFeverStartedAt);
        }

        private void CountFever(LabTeam team, double startedAt)
        {
            int index = (int)team;
            if (startedAt < 0d || startedAt == lastFeverStart[index]) return;
            lastFeverStart[index] = startedAt;
            feverCounts[index]++;
        }
    }
}
