using System.Collections.Generic;
using System.Globalization;
using C6Lab;

namespace Hapio
{
    /// <summary>One line of the 2:2 result table (#293): what is compared, each team's value, and who leads it.</summary>
    public readonly struct GameTerritoryRow
    {
        public readonly string Title, Blue, Red;
        public readonly LabTeam Leader;

        public GameTerritoryRow(string title, string blue, string red, LabTeam leader)
        {
            Title = title; Blue = blue; Red = red; Leader = leader;
        }
    }

    /// <summary>
    /// 2:2 청홍 땅따먹기 결과표 (#293). 사람별 칭호 대신 팀끼리 비교한다: 차지한 땅, 빼앗은 땅, 던진 횟수, 명중 횟수,
    /// 피버 횟수. 땅은 박 전체 칸 대비 비율이고, 나머지는 팀원 두 명의 합이다. 피버 횟수는 각 기기가 전투 중에 본 것이다
    /// (GameSession.TerritoryFeverCount).
    /// </summary>
    public static class GameTerritoryResult
    {
        public static List<GameTerritoryRow> Rows(LabSnapshot result, int blueFevers, int redFevers)
        {
            var rows = new List<GameTerritoryRow>();
            if (result == null) return rows;
            int cells = result.paintCellCount > 0 ? result.paintCellCount : 1;
            int blueStolen = 0, redStolen = 0, blueThrows = 0, redThrows = 0, blueHits = 0, redHits = 0;
            foreach (LabPlayerState player in result.players ?? new LabPlayerState[0])
            {
                if (player == null) continue;
                LabTeam team = LabTeams.TeamOfSeat(player.seat);
                if (team == LabTeam.Blue) { blueStolen += player.stolenCells; blueThrows += player.throwTotal; blueHits += player.hitTotal; }
                else if (team == LabTeam.Red) { redStolen += player.stolenCells; redThrows += player.throwTotal; redHits += player.hitTotal; }
            }
            rows.Add(Row("차지한 땅", result.blueCells, result.redCells, value => Percent(value, cells)));
            rows.Add(Row("빼앗은 땅", blueStolen, redStolen, value => Percent(value, cells)));
            rows.Add(Row("던진 횟수", blueThrows, redThrows, value => value + "회"));
            rows.Add(Row("명중 횟수", blueHits, redHits, value => value + "회"));
            rows.Add(Row("피버 횟수", blueFevers, redFevers, value => value + "번"));
            return rows;
        }

        private static GameTerritoryRow Row(string title, int blue, int red, System.Func<int, string> format) =>
            new GameTerritoryRow(title, format(blue), format(red),
                blue > red ? LabTeam.Blue : red > blue ? LabTeam.Red : LabTeam.None);

        private static string Percent(int count, int cells) =>
            (count * 100f / cells).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}
