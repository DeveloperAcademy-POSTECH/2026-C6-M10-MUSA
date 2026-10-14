using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 승리/패배 결과 화면 위에 이번 라운드 기록과 MVP 칭호를 보여주는 임시 대시보드.
    /// 장면 수정 없이 코드로 만들고, 기존 결과 카드(VICTORY/DEFEAT·버튼) 바로 위에 놓는다.
    /// 값은 Host가 보낸 스냅샷의 플레이어 기록(LabPlayerState)을 그대로 쓰므로 모든 기기가 같다.
    /// 디자인이 정해지면 이 화면만 바꾸고, 계산은 LabResultStats를 그대로 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabResultDashboard : MonoBehaviour
    {
        public float width = 340f;
        public float gapAboveResult = 12f;
        public float rowHeight = 24f;
        public int fontSize = 14;

        // 결과 카드와 같은 색
        public Color borderColor = new Color(.867f, .733f, .565f, .98f);
        public Color innerColor = new Color(.973f, .914f, .804f, 1f);
        public Color textColor = new Color(.169f, .137f, .153f, 1f);
        public Color accentColor = new Color(.565f, .337f, .247f, 1f);

        private static readonly string[] Headers = { "", "GEN", "COMBINE", "ITEMS", "PASS" };
        private static readonly float[] Columns = { 46f, 52f, 66f, 112f, 54f };

        private LabNetwork network;
        private GameObject resultControls;
        private RectTransform panel;
        private RectTransform content;
        private Font font;
        private string shownKey;

        public bool IsShowing => panel != null && panel.gameObject.activeSelf;

        public void Bind(LabNetwork target, GameObject result, Font textFont)
        {
            network = target;
            resultControls = result;
            font = textFont != null ? textFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool terminal = snapshot != null && (snapshot.phase == LabPhase.Victory || snapshot.phase == LabPhase.Defeat);
            if (!terminal)
            {
                if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
                shownKey = null;
                return;
            }
            string key = snapshot.sessionId + ":" + snapshot.round + ":" + snapshot.phase;
            if (key == shownKey && panel != null && panel.gameObject.activeSelf) return;
            shownKey = key;
            Show(snapshot.players, snapshot.phase == LabPhase.Victory);
        }

        /// <summary>기록 표와 MVP를 다시 그린다. (테스트에서도 직접 부를 수 있다)</summary>
        public void Show(LabPlayerState[] players, bool victory)
        {
            EnsurePanel();
            if (panel == null) return;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

            var rows = (players ?? new LabPlayerState[0]).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            ulong local = network != null ? network.LocalPlayerId : ulong.MaxValue;
            float y = 10f;

            AddText(victory ? "TEAM RECORD · VICTORY" : "TEAM RECORD · DEFEAT", 12f, y, width - 24f, 22f,
                fontSize + 2, FontStyle.Bold, TextAnchor.MiddleLeft, textColor);
            y += 26f;
            AddRow(Headers, y, FontStyle.Bold, accentColor);
            y += rowHeight;
            foreach (var p in rows)
            {
                bool me = p.id == local;
                AddRow(new[]
                {
                    LabResultStats.PlayerName(p) + (me ? "*" : ""),
                    p.generatedTotal.ToString(),
                    p.combinedTotal.ToString(),
                    ItemCell(p),
                    p.passedTotal.ToString()
                }, y, me ? FontStyle.Bold : FontStyle.Normal, textColor);
                y += rowHeight;
            }

            y += 6f;
            AddText("MVP", 12f, y, width - 24f, 20f, fontSize, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor);
            y += 22f;
            IReadOnlyList<LabResultAward> awards = LabResultStats.Awards(rows);
            if (awards.Count == 0)
            {
                AddText("No records this round.", 12f, y, width - 24f, 20f, fontSize, FontStyle.Normal,
                    TextAnchor.MiddleLeft, textColor);
                y += 20f;
            }
            foreach (var award in awards)
            {
                string names = string.Join(", ", award.Winners.Select(LabResultStats.PlayerName));
                AddText(LabResultStats.Label(award.Title), 12f, y, 170f, 20f, fontSize, FontStyle.Normal,
                    TextAnchor.MiddleLeft, textColor);
                AddText(names + " · " + award.Value, 182f, y, width - 194f, 20f, fontSize, FontStyle.Bold,
                    TextAnchor.MiddleRight, textColor);
                y += 20f;
            }
            if (rows.Any(p => p.id == local))
            {
                AddText("* me", 12f, y + 2f, width - 24f, 16f, fontSize - 3, FontStyle.Italic,
                    TextAnchor.MiddleLeft, accentColor);
                y += 18f;
            }

            panel.sizeDelta = new Vector2(width, y + 10f);
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }

        /// <summary>아이템 칸: 합계와 0이 아닌 종류 (R 릴레이 · B 비밀상자 · P 포션 · X 폭탄)</summary>
        private static string ItemCell(LabPlayerState p)
        {
            var parts = new List<string>();
            if (p.relayUsed > 0) parts.Add("R" + p.relayUsed);
            if (p.secretBoxUsed > 0) parts.Add("B" + p.secretBoxUsed);
            if (p.potionUsed > 0) parts.Add("P" + p.potionUsed);
            if (p.bombUsed > 0) parts.Add("X" + p.bombUsed);
            return parts.Count == 0 ? p.ItemsUsed.ToString() : p.ItemsUsed + "  " + string.Join(" ", parts);
        }

        private void EnsurePanel()
        {
            if (panel != null) return;
            if (resultControls == null) return;
            var resultRect = resultControls.transform as RectTransform;
            Transform canvas = resultControls.transform.parent;
            if (resultRect == null || canvas == null) return;

            var border = new GameObject("ResultDashboard", typeof(RectTransform), typeof(Image));
            panel = (RectTransform)border.transform;
            panel.SetParent(canvas, false);
            // 결과 카드 윗변 가운데에 붙여 그 위로 쌓는다.
            float top = resultRect.anchorMax.y;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, top);
            panel.pivot = new Vector2(.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, gapAboveResult);
            border.GetComponent<Image>().color = borderColor;

            var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            content = (RectTransform)inner.transform;
            content.SetParent(panel, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(5f, 5f);
            content.offsetMax = new Vector2(-5f, -5f);
            inner.GetComponent<Image>().color = innerColor;
            // 내용은 Inner 위에 그린다.
            var texts = new GameObject("Texts", typeof(RectTransform));
            var textsRect = (RectTransform)texts.transform;
            textsRect.SetParent(panel, false);
            textsRect.anchorMin = Vector2.zero;
            textsRect.anchorMax = Vector2.one;
            textsRect.offsetMin = textsRect.offsetMax = Vector2.zero;
            content = textsRect;
            panel.gameObject.SetActive(false);
        }

        private void AddRow(string[] cells, float y, FontStyle style, Color color)
        {
            float x = 12f;
            for (int i = 0; i < cells.Length && i < Columns.Length; i++)
            {
                AddText(cells[i], x, y, Columns[i], rowHeight, fontSize, style,
                    i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, color);
                x += Columns[i];
            }
        }

        private void AddText(string value, float x, float y, float w, float h, int size, FontStyle style,
            TextAnchor anchor, Color color)
        {
            var go = new GameObject("Cell", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = value;
        }
    }
}
