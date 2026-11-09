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
    /// Rows are found from the saved title labels; the saved strips and colours stay as imported.
    /// </summary>
    public static class StoryRecordHighlight
    {
        public static readonly string[] Titles = { "딜왕", "명중왕", "구슬패스왕", "북 패스왕", "북 독점왕" };
        public const string HighlightName = "My Award Highlight";
        private const string StripPrefix = "Rectangle 12", SampleStrip = "Rectangle 124";
        public static readonly Color MineText = new Color(.982f, .965f, .945f, 1f), OtherText = new Color(.364f, .329f, .29f, 1f);

        /// <summary>Does nothing on a view without the award table. Safe to call again on the same view.</summary>
        public static void Apply(Transform view, IReadOnlyList<StoryCardRecords.Record> records, bool writeCells)
        {
            if (view == null || records == null) return;
            Text[] texts = view.GetComponentsInChildren<Text>(true);
            Text[] titles = Titles.Select(title => texts.FirstOrDefault(text => text.text == title)).ToArray();
            Image[] strips = view.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith(StripPrefix, StringComparison.Ordinal)).ToArray();
            Image sample = strips.FirstOrDefault(image => image.name.StartsWith(SampleStrip, StringComparison.Ordinal));
            if (sample == null || titles.Any(title => title == null)) return;

            Transform parent = sample.transform.parent;
            foreach (Transform child in parent.Cast<Transform>().Where(item => item.name == HighlightName).ToArray())
                Remove(child.gameObject);
            foreach (Image strip in strips) strip.gameObject.SetActive(false);

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
