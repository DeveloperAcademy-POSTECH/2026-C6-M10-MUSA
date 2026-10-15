using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 승리/패배 결과 화면에 이번 라운드 칭호(딜왕·패스왕·막타·북 독점)를 칭호별로 한 줄씩 보여주는 대시보드.
    /// 줄마다 칭호 · 받은 사람(동점이면 모두) · 기록. 기록이 0인 칭호는 줄을 만들지 않는다.
    /// 장면 수정 없이 코드로 만들고, 기존 결과 카드(VICTORY/DEFEAT·버튼) 바로 위에 놓는다.
    /// 값은 Host가 보낸 스냅샷의 플레이어 기록(LabPlayerState)을 그대로 쓰므로 모든 기기가 같다.
    /// 디자인이 정해지면 이 화면만 바꾸고, 계산은 LabResultStats를 그대로 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabResultDashboard : MonoBehaviour
    {
        public float width = 284f;
        public float gapAboveResult = 12f;
        public float headerHeight = 26f;
        public float rowHeight = 30f;
        public int fontSize = 14;

        // 결과 카드와 같은 색
        public Color borderColor = new Color(.867f, .733f, .565f, .98f);
        public Color innerColor = new Color(.973f, .914f, .804f, 1f);
        public Color headerColor = new Color(.925f, .855f, .725f, 1f);
        /// <summary>내 줄 배경 (버튼과 같은 갈색). 글자는 myRowTextColor.</summary>
        public Color myRowColor = new Color(.565f, .337f, .247f, 1f);
        public Color myRowTextColor = Color.white;
        public Color lineColor = new Color(.867f, .733f, .565f, .7f);
        public Color textColor = new Color(.169f, .137f, .153f, 1f);

        /// <summary>받은 칭호가 하나도 없을 때 한 줄</summary>
        public const string NoRecordLabel = "기록 없음";

        private static readonly string[] Headers = { "칭호", "누구", "기록" };
        private static readonly float[] Columns = { 62f, 84f, 122f }; // 합 268 + 왼쪽 여백 6 = 안쪽 폭 274

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
            terminal = terminal && !LabGourdFinish.DelayVictory(network, snapshot);
            if (!terminal)
            {
                if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
                shownKey = null;
                return;
            }
            string key = snapshot.sessionId + ":" + snapshot.round + ":" + snapshot.phase;
            if (key == shownKey && panel != null && panel.gameObject.activeSelf) return;
            shownKey = key;
            Show(snapshot.players, snapshot.phase == LabPhase.Victory, snapshot.playedSeconds);
        }

        /// <summary>칭호별 목록을 다시 그린다. (테스트에서도 직접 부를 수 있다)</summary>
        public void Show(LabPlayerState[] players, bool victory, double playedSeconds = 0d)
        {
            EnsurePanel();
            if (panel == null) return;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

            var rows = (players ?? new LabPlayerState[0]).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            ulong local = network != null ? network.LocalPlayerId : ulong.MaxValue;
            IReadOnlyList<LabResultAward> awards = LabResultStats.Awards(rows, victory);

            float inner = width - 10f;
            float y = 0f;
            // 머리줄
            AddBox(0f, y, inner, headerHeight, headerColor);
            AddCells(Headers, y, headerHeight, FontStyle.Normal, null, fontSize - 2, textColor);
            y += headerHeight;
            if (awards.Count == 0)
            {
                AddBox(0f, y, inner, 1f, lineColor);
                AddText(NoRecordLabel, 6f, y, inner - 12f, rowHeight, fontSize, FontStyle.Normal,
                    TextAnchor.MiddleCenter, textColor);
                y += rowHeight;
            }
            // 칭호마다 한 줄: 칭호 · 누구 · 기록. 내가 받은 칭호 줄은 갈색 배경 + 흰 글자.
            foreach (var award in awards)
            {
                AddBox(0f, y, inner, 1f, lineColor);
                bool mine = award.Winners.Any(w => w.id == local);
                if (mine) AddBox(0f, y + 1f, inner, rowHeight - 1f, myRowColor);
                string[] cells =
                {
                    LabResultStats.Label(award.Title),
                    LabResultStats.WinnerNames(award, local),
                    LabResultStats.Record(award, playedSeconds)
                };
                bool[] bold = { true, false, false };
                AddCells(cells, y, rowHeight, FontStyle.Normal, bold, fontSize, mine ? myRowTextColor : textColor);
                y += rowHeight;
            }

            panel.sizeDelta = new Vector2(width, y + 10f);
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
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
            textsRect.offsetMin = new Vector2(5f, 5f);
            textsRect.offsetMax = new Vector2(-5f, -5f);
            content = textsRect;
            panel.gameObject.SetActive(false);
        }

        private void AddCells(string[] cells, float y, float h, FontStyle style, bool[] bold, int size, Color color)
        {
            float x = 6f;
            for (int i = 0; i < cells.Length && i < Columns.Length; i++)
            {
                bool strong = bold != null && i < bold.Length && bold[i];
                // 누구·기록 칸은 길면(동점 여러 명 등) 두 줄로 접는다.
                AddText(cells[i], x, y, Columns[i] - 2f, h, size,
                    strong ? FontStyle.Bold : style, TextAnchor.MiddleLeft, color, i > 0);
                x += Columns[i];
            }
        }

        private void AddBox(float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private void AddText(string value, float x, float y, float w, float h, int size, FontStyle style,
            TextAnchor anchor, Color color, bool wrap = false)
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
            // 칭호 칸은 길면 두 줄로 접는다.
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = .9f;
            text.text = value;
        }
    }
}
