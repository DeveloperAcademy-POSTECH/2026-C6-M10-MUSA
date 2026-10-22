using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>칭호 표 한 줄: 칭호 · 누구 · 기록. Mine이면 내 줄 색(갈색 배경 + 흰 글자)으로 칠한다.</summary>
    public readonly struct LabResultRow
    {
        public readonly string Title;
        public readonly string Who;
        public readonly string Record;
        public readonly bool Mine;

        public LabResultRow(string title, string who, string record, bool mine)
        {
            Title = title;
            Who = who;
            Record = record;
            Mine = mine;
        }
    }

    /// <summary>
    /// 칭호 표 생김새. 결과 카드와 같은 색이 기본값이다. 표를 쓰는 화면마다 Inspector에서 바꿀 수 있다.
    /// </summary>
    [Serializable]
    public sealed class LabResultTableStyle
    {
        public float width = 284f;
        public float headerHeight = 24f;
        public float rowHeight = 27f;
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
    }

    /// <summary>
    /// 결과 화면들이 함께 쓰는 칭호 표. 전투 결과 카드(LabResultDashboard)와 STAGE CLEAR 화면이
    /// 같은 생김새를 갖도록 표를 만드는 일만 맡는다. 무엇을 적을지는 LabResultStats가 정한다.
    /// 장면 수정 없이 코드로 만든다.
    /// </summary>
    public static class LabResultTable
    {
        /// <summary>받은 칭호가 하나도 없을 때 한 줄</summary>
        public const string NoRecordLabel = "기록 없음";

        private static readonly string[] Headers = { "칭호", "누구", "기록" };
        private static readonly float[] Columns = { 62f, 84f, 122f }; // 합 268 + 왼쪽 여백 6 = 안쪽 폭 274
        private const float Border = 5f;
        private const string InnerName = "Inner";
        private const string TextsName = "Texts";

        /// <summary>
        /// 칭호 목록을 표의 줄로 바꾼다. 내가 받은 칭호(동점 포함)는 Mine으로 표시한다.
        /// </summary>
        public static List<LabResultRow> RowsFor(IEnumerable<LabResultAward> awards, ulong localPlayerId,
            double playedSeconds)
        {
            var rows = new List<LabResultRow>();
            if (awards == null) return rows;
            foreach (LabResultAward award in awards)
                rows.Add(new LabResultRow(LabResultStats.Label(award.Title), LabResultStats.WinnerNames(award),
                    LabResultStats.Record(award, playedSeconds),
                    award.Winners != null && award.Winners.Any(w => w != null && w.id == localPlayerId)));
            return rows;
        }

        /// <summary>
        /// 빈 표(테두리·안쪽 판·글자 칸)를 parent 아래에 만든다. 기준점은 위 가운데라서
        /// anchoredPosition으로 표 윗변 위치를 정하면 된다. 처음에는 꺼져 있다.
        /// </summary>
        public static RectTransform Create(Transform parent, string name = "ResultDashboard")
        {
            var border = new GameObject(name, typeof(RectTransform), typeof(Image));
            var panel = (RectTransform)border.transform;
            panel.SetParent(parent, false);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f);
            panel.pivot = new Vector2(.5f, 1f);
            border.GetComponent<Image>().raycastTarget = false;

            var inner = new GameObject(InnerName, typeof(RectTransform), typeof(Image));
            Inset((RectTransform)inner.transform, panel);
            inner.GetComponent<Image>().raycastTarget = false;
            // 내용은 Inner 위에 그린다.
            var texts = new GameObject(TextsName, typeof(RectTransform));
            Inset((RectTransform)texts.transform, panel);
            border.SetActive(false);
            return panel;
        }

        /// <summary>
        /// 표 내용을 다시 그리고 켠다. 머리줄(칭호 · 누구 · 기록) 밑에 줄마다 한 칭호.
        /// 줄이 없으면 "기록 없음" 한 줄. 표 전체 높이를 돌려준다.
        /// </summary>
        public static float Fill(RectTransform panel, IReadOnlyList<LabResultRow> rows, LabResultTableStyle style,
            Font font)
        {
            if (panel == null) return 0f;
            style = style ?? new LabResultTableStyle();
            font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var inner = panel.Find(InnerName);
            var content = panel.Find(TextsName) as RectTransform;
            if (content == null) return 0f;
            for (int i = content.childCount - 1; i >= 0; i--) Remove(content.GetChild(i).gameObject);
            panel.GetComponent<Image>().color = style.borderColor;
            if (inner != null) inner.GetComponent<Image>().color = style.innerColor;

            float width = style.width - Border * 2f;
            float y = 0f;
            // 머리줄
            AddBox(content, 0f, y, width, style.headerHeight, style.headerColor);
            AddCells(content, Headers, y, style.headerHeight, null, style.fontSize - 2, style.textColor, font);
            y += style.headerHeight;
            if (rows == null || rows.Count == 0)
            {
                AddBox(content, 0f, y, width, 1f, style.lineColor);
                AddText(content, NoRecordLabel, 6f, y, width - 12f, style.rowHeight, style.fontSize, FontStyle.Normal,
                    TextAnchor.MiddleCenter, style.textColor, font);
                y += style.rowHeight;
            }
            // 칭호마다 한 줄: 칭호 · 누구 · 기록. 내 줄은 갈색 배경 + 흰 글자.
            bool[] bold = { true, false, false };
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                LabResultRow row = rows[i];
                AddBox(content, 0f, y, width, 1f, style.lineColor);
                if (row.Mine) AddBox(content, 0f, y + 1f, width, style.rowHeight - 1f, style.myRowColor);
                AddCells(content, new[] { row.Title, row.Who, row.Record }, y, style.rowHeight, bold, style.fontSize,
                    row.Mine ? style.myRowTextColor : style.textColor, font);
                y += style.rowHeight;
            }

            float height = y + Border * 2f;
            panel.sizeDelta = new Vector2(style.width, height);
            panel.gameObject.SetActive(true);
            return height;
        }

        private static void Inset(RectTransform rect, RectTransform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(Border, Border);
            rect.offsetMax = new Vector2(-Border, -Border);
        }

        private static void Remove(GameObject child)
        {
            // 같은 프레임에 새 줄을 그리므로 옛 줄은 바로 떼어 낸다.
            child.transform.SetParent(null, false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(child);
            else UnityEngine.Object.DestroyImmediate(child);
        }

        private static void AddCells(RectTransform content, string[] cells, float y, float h, bool[] bold, int size,
            Color color, Font font)
        {
            float x = 6f;
            for (int i = 0; i < cells.Length && i < Columns.Length; i++)
            {
                bool strong = bold != null && i < bold.Length && bold[i];
                // 누구·기록 칸은 길면(동점 여러 명 등) 두 줄로 접는다.
                AddText(content, cells[i] ?? string.Empty, x, y, Columns[i] - 2f, h, size,
                    strong ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft, color, font, i > 0);
                x += Columns[i];
            }
        }

        private static void AddBox(RectTransform content, float x, float y, float w, float h, Color color)
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

        private static void AddText(RectTransform content, string value, float x, float y, float w, float h, int size,
            FontStyle style, TextAnchor anchor, Color color, Font font, bool wrap = false)
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
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = .9f;
            text.text = value;
        }
    }
}
