using System;
using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Saved Figma result artwork and editable widgets; game/session rules stay in GameScreen.</summary>
    public sealed class GameStageResultView : MonoBehaviour
    {
        public RectTransform design;
        public RawImage background;
        public Text stage, yokai, judgment, paint, timeLeft, guidance, emptyLabel;
        public Button viewerButton, homeButton, nextButton;
        public RectTransform rows;
        public ScrollRect scroll;
        public GameObject rowTemplate;
        public Color ink = new Color32(63, 61, 56, 255);
        public Color secondaryInk = new Color32(104, 100, 92, 255);
        [Header("2:2 땅따먹기 결과 (#293)")]
        [Tooltip("청팀이 이겼을 때 배경 (파란 수채 모서리)")]
        public Texture blueTeamBackground;
        [Tooltip("홍팀이 이겼을 때 배경 (빨간 수채 모서리). 무승부는 장면에 저장된 배경을 쓴다.")]
        public Texture redTeamBackground;
        /// <summary>The team inks of the 2:2 seat screen, for the title and the table.</summary>
        public static readonly Color BlueTeamInk = new Color32(0x2F, 0x62, 0xA2, 255), RedTeamInk = new Color32(0xD8, 0x58, 0x36, 255);
        private static readonly string[] HeaderNames = { "TitleHeader", "PlayerHeader", "RecordHeader" };
        private readonly List<GameObject> instances = new List<GameObject>();
        private readonly List<LabResultRow> displayed = new List<LabResultRow>();
        private Texture savedBackground;
        private Text[] headers;
        private string[] savedHeaders;
        private Color[] savedHeaderColors;

        private void LateUpdate()
        {
            if (design == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            float factor = canvas != null ? canvas.scaleFactor : 1f;
            Rect safe = Screen.safeArea;
            design.localScale = Vector3.one * Mathf.Min(safe.width / factor / 402f, safe.height / factor / 874f);
        }

        public void Present(bool victory, int stageIndex, string stageName, LabSnapshot snapshot,
            IReadOnlyList<LabResultRow> awards, string message, bool overall = false, bool finalVictory = false)
        {
            string name = string.IsNullOrWhiteSpace(stageName) ? "요괴" : stageName;
            UseBackground(null);
            SetHeaders(null);
            stage.text = finalVictory ? "ALL STAGES CLEAR" : "STAGE " + stageIndex;
            yokai.text = finalVictory ? "모든 요괴" : name;
            judgment.text = finalVictory ? "최종 승리 !" : victory ? "봉인 성공 !" : "봉인 실패...";
            paint.text = overall ? "전체 스테이지 기록" : snapshot == null ? "PAINT —"
                : "PAINT " + (snapshot.paintCoverage * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            timeLeft.text = overall ? string.Empty : snapshot == null ? "TIME LEFT —"
                : "TIME LEFT " + Math.Ceiling(snapshot.remaining).ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
            guidance.text = message;
            Label(viewerButton, name + " 봉인 박 확인");
            SetRows(awards);
        }

        /// <summary>
        /// 2:2 땅따먹기 결과 (#293): 이긴 팀 이름을 팀 색으로 띄우고 배경도 이긴 팀 색(청 파랑, 홍 빨강)으로 바꾼다. 표는 칭호
        /// 대신 항목 · 청팀 · 홍팀으로 두 팀을 비교하고, 앞선 팀의 값을 그 팀 색으로 칠한다.
        /// </summary>
        public void PresentTerritory(LabTeam winner, LabSnapshot snapshot, IReadOnlyList<GameTerritoryRow> records, string message)
        {
            UseBackground(winner == LabTeam.Blue ? blueTeamBackground : winner == LabTeam.Red ? redTeamBackground : null);
            stage.text = "2 : 2";
            yokai.text = "땅따먹기 결과";
            judgment.supportRichText = true;
            judgment.text = snapshot == null ? "결과를 정리하고 있소"
                : winner == LabTeam.Blue ? Colored("청팀", BlueTeamInk) + " 승리~!"
                : winner == LabTeam.Red ? Colored("홍팀", RedTeamInk) + " 승리~!" : "무승부~!";
            paint.text = snapshot == null ? "PAINT —"
                : "PAINT " + (snapshot.paintCoverage * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            timeLeft.text = snapshot == null ? "TIME LEFT —"
                : "TIME LEFT " + Math.Ceiling(snapshot.remaining).ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
            guidance.text = message;
            Label(viewerButton, "박 확인");
            SetHeaders(new[] { "항목", "청팀", "홍팀" });
            int count = records != null ? records.Count : 0;
            var rows = new LabResultRow[count];
            for (int i = 0; i < count; i++) rows[i] = new LabResultRow(records[i].Title, records[i].Blue, records[i].Red, false);
            SetRows(rows);
            for (int i = 0; i < count && i < instances.Count; i++)
            {
                Tint(instances[i], "Player", records[i].Leader == LabTeam.Blue ? BlueTeamInk : secondaryInk);
                Tint(instances[i], "Record", records[i].Leader == LabTeam.Red ? RedTeamInk : secondaryInk);
            }
        }

        private static string Colored(string value, Color color) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + value + "</color>";

        private static void Tint(GameObject row, string name, Color color) =>
            row.transform.Find(name).GetComponent<Text>().color = color;

        /// <summary>The 2:2 team background, or the one the scene was saved with.</summary>
        private void UseBackground(Texture texture)
        {
            if (background == null) return;
            if (savedBackground == null) savedBackground = background.texture;
            background.texture = texture != null ? texture : savedBackground;
        }

        /// <summary>The table's three headings for 2:2, or the saved 칭호 · 견습생 · 기록 (null).</summary>
        private void SetHeaders(string[] values)
        {
            if (headers == null)
            {
                headers = new Text[HeaderNames.Length];
                savedHeaders = new string[HeaderNames.Length];
                savedHeaderColors = new Color[HeaderNames.Length];
                Transform table = design != null ? design.Find("AwardTable") : null;
                for (int i = 0; i < HeaderNames.Length; i++)
                {
                    Transform header = table != null ? table.Find(HeaderNames[i]) : null;
                    headers[i] = header != null ? header.GetComponent<Text>() : null;
                    if (headers[i] == null) continue;
                    savedHeaders[i] = headers[i].text;
                    savedHeaderColors[i] = headers[i].color;
                }
            }
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i] == null) continue;
                headers[i].text = values != null ? values[i] : savedHeaders[i];
                headers[i].color = values == null ? savedHeaderColors[i]
                    : i == 1 ? BlueTeamInk : i == 2 ? RedTeamInk : savedHeaderColors[i];
            }
        }

        public void SetRows(IReadOnlyList<LabResultRow> awards)
        {
            int count = awards != null ? awards.Count : 0;
            bool same = displayed.Count == count;
            for (int i = 0; same && i < count; i++)
                same = displayed[i].Title == awards[i].Title && displayed[i].Who == awards[i].Who
                    && displayed[i].Record == awards[i].Record && displayed[i].Mine == awards[i].Mine;
            if (same && (count == 0 || instances.Count >= count)) return;
            displayed.Clear();
            for (int i = 0; i < count; i++)
            {
                LabResultRow row = awards[i];
                displayed.Add(row);
                if (instances.Count <= i) instances.Add(Instantiate(rowTemplate, rows));
                GameObject item = instances[i];
                item.name = "Award_" + i;
                item.SetActive(true);
                RectTransform rect = (RectTransform)item.transform;
                rect.anchoredPosition = new Vector2(0f, -40f * i);
                item.transform.Find("Highlight").gameObject.SetActive(row.Mine);
                string who = row.Who;
                SetCell(item, "Title", row.Title, row.Mine);
                SetCell(item, "Player", who, row.Mine);
                SetCell(item, "Record", row.Record, row.Mine);
            }
            for (int i = count; i < instances.Count; i++) instances[i].SetActive(false);
            emptyLabel.gameObject.SetActive(count == 0);
            rows.sizeDelta = new Vector2(rows.sizeDelta.x, Mathf.Max(200f, count * 40f));
            scroll.vertical = count > 5;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void SetCell(GameObject row, string name, string value, bool mine)
        {
            Text text = row.transform.Find(name).GetComponent<Text>();
            text.text = value;
            text.color = mine ? Color.white : secondaryInk;
        }

        public static void Label(Button button, string value)
        {
            if (button != null) button.GetComponentInChildren<Text>(true).text = value;
        }
    }
}
