using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// Marks the award rows this player earned on the imported result cards. The cards carry the design's two sample
    /// red strips on fixed rows, so those are switched off and one copy is placed on each row the player actually won.
    /// Rows are found from the saved title labels; the saved strips and colours stay as imported. The player and record
    /// values are centred under their column headers ("견습생", "기록").
    /// </summary>
    public static class StoryRecordHighlight
    {
        public static readonly string[] Titles = { "딜왕", "명중왕", "구슬패스왕", "북 패스왕", "북 독점왕" };
        public const string PlayerHeader = "견습생", RecordHeader = "기록";
        public const string HighlightName = "My Award Highlight";
        private const string StripPrefix = "Rectangle 12", SampleStrip = "Rectangle 124";
        public static readonly Color MineText = new Color(.982f, .965f, .945f, 1f), OtherText = new Color(.364f, .329f, .29f, 1f);

        /// <summary>Does nothing on a view without the award table. Safe to call again on the same view.</summary>
        public static void Apply(Transform view, IReadOnlyList<StoryCardRecords.Record> records, bool writeCells)
        {
            if (view == null || records == null) return;
            Image[] strips = view.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith(StripPrefix, StringComparison.Ordinal)).ToArray();
            Image sample = strips.FirstOrDefault(image => image.name.StartsWith(SampleStrip, StringComparison.Ordinal));
            if (sample == null) return;
            Transform parent = sample.transform.parent;
            // Only the face holding the table: the card's front has lettering that can animate through a row's height.
            Text[] texts = parent.GetComponentsInChildren<Text>(true);
            Text[] titles = Titles.Select(title => texts.FirstOrDefault(text => text.text == title)).ToArray();
            if (titles.Any(title => title == null)) return;

            foreach (Transform child in parent.Cast<Transform>().Where(item => item.name == HighlightName).ToArray())
                Remove(child.gameObject);
            foreach (Image strip in strips) strip.gameObject.SetActive(false);

            Text playerHeader = texts.FirstOrDefault(text => text.text == PlayerHeader);
            Text recordHeader = texts.FirstOrDefault(text => text.text == RecordHeader);
            float[] rows = titles.Select(title => CenterY(title.rectTransform, parent)).ToArray();
            float reach = Mathf.Abs(rows[1] - rows[0]) * .45f;
            for (int row = 0; row < rows.Length; row++)
            {
                StoryCardRecords.Record record = row < records.Count ? records[row] : default;
                float y = rows[row];
                Text[] cells = texts.Where(text => !Titles.Contains(text.text) && !ReferenceEquals(text, titles[row])
                    && Mathf.Abs(CenterY(text.rectTransform, parent) - y) < reach)
                    .OrderBy(text => parent.InverseTransformPoint(text.rectTransform.position).x).ToArray();
                if (writeCells && cells.Length >= 2)
                {
                    cells[0].text = string.IsNullOrEmpty(record.player) ? "—" : record.player;
                    cells[1].text = string.IsNullOrEmpty(record.value) ? "—" : record.value;
                }
                // The imported cells are left-aligned boxes as wide as the design's sample text, so a shorter or
                // longer value drifted off its column. Centre each value under its header instead.
                if (cells.Length >= 2)
                {
                    CenterUnder(cells[0], playerHeader, parent);
                    CenterUnder(cells[1], recordHeader, parent);
                }
                Color color = record.mine ? MineText : OtherText;
                titles[row].color = color;
                foreach (Text cell in cells) cell.color = color;
                if (!record.mine) continue;

                Image highlight = UnityEngine.Object.Instantiate(sample, parent, false);
                highlight.name = HighlightName;
                highlight.gameObject.SetActive(true);
                Vector3 position = highlight.transform.localPosition;
                position.y += y - CenterY(sample.rectTransform, parent);
                highlight.transform.localPosition = position;
                highlight.transform.SetSiblingIndex(cells.Append(titles[row])
                    .Select(text => SiblingUnder(text.transform, parent)).Min());
            }
        }

        /// <summary>Centre the cell's text on the header's drawn text; repeated calls keep it there.</summary>
        private static void CenterUnder(Text cell, Text header, Transform space)
        {
            if (cell == null || header == null) return;
            cell.alignment = (TextAnchor)((int)cell.alignment / 3 * 3 + 1);
            float target = DrawnCenterX(header, space);
            RectEdges(cell.rectTransform, space, out float left, out float right, out float y);
            Vector3 from = space.TransformPoint(new Vector3((left + right) * .5f, y, 0f));
            Vector3 to = space.TransformPoint(new Vector3(target, y, 0f));
            cell.rectTransform.position += to - from;
        }

        /// <summary>The horizontal centre of a label's drawn text (not its box) in <paramref name="space"/>.</summary>
        public static float DrawnCenterX(Text text, Transform space)
        {
            RectEdges(text.rectTransform, space, out float left, out float right, out _);
            float width = text.rectTransform.rect.width;
            float drawn = width > 0f ? Mathf.Min(right - left, text.preferredWidth * (right - left) / width) : 0f;
            switch ((int)text.alignment % 3)
            {
                case 0: return left + drawn * .5f;
                case 2: return right - drawn * .5f;
                default: return (left + right) * .5f;
            }
        }

        private static void RectEdges(RectTransform rect, Transform space, out float left, out float right, out float centerY)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 a = space.InverseTransformPoint(corners[0]), b = space.InverseTransformPoint(corners[2]);
            left = Mathf.Min(a.x, b.x); right = Mathf.Max(a.x, b.x); centerY = (a.y + b.y) * .5f;
        }

        private static int SiblingUnder(Transform item, Transform parent)
        {
            while (item.parent != parent && item.parent != null) item = item.parent;
            return item.GetSiblingIndex();
        }

        private static float CenterY(RectTransform rect, Transform space)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return space.InverseTransformPoint((corners[0] + corners[2]) * .5f).y;
        }

        private static void Remove(GameObject item)
        {
            item.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
