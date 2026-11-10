using C6Lab;
using UnityEngine;

namespace Hapio
{
    public sealed class GameTerritoryResultView : MonoBehaviour
    {
        public GameStageResultView table;
        public RectTransform blueFill, redFill;
        public UnityEngine.UI.Text bluePercent, redPercent;
        public GameObject blueDecoration, redDecoration;
        public GameObject blueWinnerFlag, redWinnerFlag, blueSmallFlag, redSmallFlag;

        public void Present(LabSnapshot result, ulong localPlayerId)
        {
            LabTeam winner = result != null ? result.teamWinner : LabTeam.None;
            table.judgment.text = result == null ? "결과를 정리하고 있소" : winner == LabTeam.Blue
                ? "<color=#0E6192>청팀</color> 승리!" : winner == LabTeam.Red
                ? "<color=#D32E15>홍팀</color> 승리!" : "무승부!";
            blueDecoration.SetActive(winner == LabTeam.Blue);
            redDecoration.SetActive(winner == LabTeam.Red);
            blueWinnerFlag.SetActive(winner == LabTeam.Blue);
            redWinnerFlag.SetActive(winner == LabTeam.Red);
            blueSmallFlag.SetActive(winner != LabTeam.Blue);
            redSmallFlag.SetActive(winner != LabTeam.Red);
            float cells = result != null ? Mathf.Max(1, result.paintCellCount) : 1;
            float blue = result != null ? Mathf.Clamp01(result.blueCells / cells) : 0;
            float red = result != null ? Mathf.Clamp01(result.redCells / cells) : 0;
            blueFill.sizeDelta = new Vector2(336 * blue, 21);
            redFill.sizeDelta = new Vector2(336 * red, 21);
            redFill.anchoredPosition = new Vector2(336 * (1 - red), 0);
            bluePercent.text = result == null ? "—" : Mathf.RoundToInt(blue * 100) + "%";
            redPercent.text = result == null ? "—" : Mathf.RoundToInt(red * 100) + "%";
            bluePercent.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(336 * blue - 55, 55, 226), 1);
            redPercent.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(336 * (1 - red) + 3, 113, 287), 1);
            bluePercent.color = winner == LabTeam.Blue ? new Color32(250, 246, 241, 255) : new Color32(159, 192, 211, 255);
            redPercent.color = winner == LabTeam.Red ? new Color32(250, 246, 241, 255) : new Color32(237, 171, 161, 255);
            var awards = result == null ? new System.Collections.Generic.List<LabResultRow>()
                : LabResultTable.RowsFor(LabResultStats.Awards(result.players, false), localPlayerId, result.playedSeconds);
            string localName = null;
            if (result?.players != null)
                foreach (var player in result.players)
                    if (player != null && player.id == localPlayerId) localName = LabResultStats.PlayerName(player);
            for (int index = 0; index < awards.Count; index++)
            {
                var row = awards[index];
                awards[index] = new LabResultRow(row.Title == "북 독점" ? "북 독점왕" : row.Title,
                    row.Mine && localName != null ? row.Who.Replace(localName, localName + "(나)") : row.Who, row.Record, row.Mine);
            }
            table.SetRows(awards);
        }
    }
}
