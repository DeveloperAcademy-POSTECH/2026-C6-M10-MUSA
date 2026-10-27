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
        private readonly List<GameObject> instances = new List<GameObject>();
        private readonly List<LabResultRow> displayed = new List<LabResultRow>();

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
