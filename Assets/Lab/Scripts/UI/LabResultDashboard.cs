using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 승리/패배 결과 화면 위에 이번 라운드 기록 표(타격·딜·결합·아이템·구슬 전달·칭호)를 보여주는 대시보드.
    /// 장면 수정 없이 코드로 만들고, 기존 결과 카드(VICTORY/DEFEAT·버튼) 바로 위에 놓는다.
    /// 값은 Host가 보낸 스냅샷의 플레이어 기록(LabPlayerState)을 그대로 쓰므로 모든 기기가 같다.
    /// 디자인이 정해지면 이 화면만 바꾸고, 계산은 LabResultStats를 그대로 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabResultDashboard : MonoBehaviour
    {
        public float width = 362f;
        public float gapAboveResult = 12f;
        public float headerHeight = 28f;
        public float rowHeight = 32f;
        public int fontSize = 14;

        // 결과 카드와 같은 색
        public Color borderColor = new Color(.867f, .733f, .565f, .98f);
        public Color innerColor = new Color(.973f, .914f, .804f, 1f);
        public Color headerColor = new Color(.925f, .855f, .725f, 1f);
        public Color myRowColor = new Color(.945f, .878f, .757f, 1f);
        public Color lineColor = new Color(.867f, .733f, .565f, .7f);
        public Color textColor = new Color(.169f, .137f, .153f, 1f);

        private static readonly string[] Headers = { "플레이어", "타격", "딜", "결합", "아이템", "구슬 전달", "칭호" };
        private static readonly float[] Columns = { 46f, 38f, 44f, 38f, 48f, 58f, 74f }; // 합 346 + 왼쪽 여백 6 = 안쪽 폭 352

        /// <summary>요괴를 쓰러뜨린 마지막 한 방을 친 사람 표시 (승리했을 때만)</summary>
        public const string LastHitLabel = "막타";

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
            Show(snapshot.players, snapshot.phase == LabPhase.Victory);
        }

        /// <summary>기록 표를 다시 그린다. (테스트에서도 직접 부를 수 있다)</summary>
        public void Show(LabPlayerState[] players, bool victory)
        {
            EnsurePanel();
            if (panel == null) return;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

            var rows = (players ?? new LabPlayerState[0]).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            ulong local = network != null ? network.LocalPlayerId : ulong.MaxValue;
            IReadOnlyList<LabResultAward> awards = LabResultStats.Awards(rows);

            float inner = width - 10f;
            float y = 0f;
            // 머리줄
            AddBox(0f, y, inner, headerHeight, headerColor);
            AddCells(Headers, y, headerHeight, FontStyle.Normal, null, fontSize - 2);
            y += headerHeight;
            foreach (var p in rows)
            {
                AddBox(0f, y, inner, 1f, lineColor);
                if (p.id == local) AddBox(0f, y + 1f, inner, rowHeight - 1f, myRowColor);
                string[] cells =
                {
                    LabResultStats.PlayerName(p),
                    p.hitTotal.ToString(),
                    p.damageTotal.ToString(),
                    p.combinedTotal.ToString(),
                    p.ItemsUsed.ToString(),
                    p.passedTotal.ToString(),
                    TitlesOf(p, awards, victory)
                };
                // 각 항목 1등 숫자는 굵게 (칭호와 같은 기준)
                bool[] bold =
                {
                    false,
                    false,
                    false,
                    IsWinner(p, awards, LabResultTitle.MostCombined),
                    IsWinner(p, awards, LabResultTitle.MostItems),
                    IsWinner(p, awards, LabResultTitle.MostPassed),
                    false
                };
                AddCells(cells, y, rowHeight, FontStyle.Normal, bold, fontSize);
                y += rowHeight;
            }

            panel.sizeDelta = new Vector2(width, y + 10f);
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }

        private static bool IsWinner(LabPlayerState p, IReadOnlyList<LabResultAward> awards, LabResultTitle title) =>
            awards.Any(a => a.Title == title && a.Winners.Any(w => w.id == p.id));

        /// <summary>이 플레이어가 받은 칭호들 (예: "결합왕 · 패스왕"). 승리했고 막타를 쳤으면 "막타"도 붙인다. 없으면 빈칸.</summary>
        private static string TitlesOf(LabPlayerState p, IReadOnlyList<LabResultAward> awards, bool victory)
        {
            var labels = awards.Where(a => a.Winners.Any(w => w.id == p.id))
                .Select(a => LabResultStats.Label(a.Title)).ToList();
            if (victory && p.lastHit) labels.Add(LastHitLabel);
            return string.Join(" · ", labels);
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

        private void AddCells(string[] cells, float y, float h, FontStyle style, bool[] bold, int size)
        {
            float x = 6f;
            for (int i = 0; i < cells.Length && i < Columns.Length; i++)
            {
                bool strong = bold != null && i < bold.Length && bold[i];
                bool last = i == Columns.Length - 1;
                // 칭호 칸은 조금 작게, 길면 두 줄로 접는다.
                AddText(cells[i], x, y, Columns[i] - 2f, h, last ? size - 1 : size,
                    strong ? FontStyle.Bold : style, TextAnchor.MiddleLeft, textColor, last);
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
            text.lineSpacing = .9f;
            text.text = value;
        }
    }
}
