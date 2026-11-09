using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Tests
{
    /// <summary>The saved result cards show red only on the rows this player earned; no prefab stores that state.</summary>
    public sealed class StoryRecordHighlightTests
    {
        private const string Views = "Assets/Hapio/Story/StageStories/Views/";

        private static StoryCardRecords.Record[] Rows(params int[] mine)
        {
            var rows = new StoryCardRecords.Record[5];
            for (int i = 0; i < 5; i++)
                rows[i] = new StoryCardRecords.Record { player = "P" + (i + 1) + (mine.Contains(i) ? "(나)" : ""), value = (i + 1) * 10 + "회", mine = mine.Contains(i) };
            return rows;
        }

        private static float CenterY(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0].y + corners[2].y) * .5f;
        }

        private static Text Title(GameObject view, int row) =>
            view.GetComponentsInChildren<Text>(true).First(text => text.text == StoryRecordHighlight.Titles[row]);

        private static Image[] Highlights(GameObject view) => view.GetComponentsInChildren<Image>(true)
            .Where(image => image.name == StoryRecordHighlight.HighlightName).ToArray();

        [TestCase("Gumiho_SealCard"), TestCase("Yagwanggui_SealCard"), TestCase("Jigwi_SealCard"), TestCase("Failure_Dashboard")]
        public void OnlyEarnedRowsAreRedAndReapplyingDoesNotStack(string prefab)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Views + prefab + ".prefab");
            Assert.That(source, Is.Not.Null);
            GameObject view = Object.Instantiate(source);
            try
            {
                StoryRecordHighlight.Apply(view.transform, Rows(0), true);
                StoryRecordHighlight.Apply(view.transform, Rows(1, 4), true);
                Image[] highlights = Highlights(view);
                Assert.That(highlights.Count(item => item.gameObject.activeSelf), Is.EqualTo(2));
                Assert.That(view.GetComponentsInChildren<Image>(true).Any(image => image.name.StartsWith("Rectangle 12") && image.gameObject.activeSelf), Is.False,
                    "The design's sample strips on fixed rows are off.");
                foreach (int row in new[] { 1, 4 })
                {
                    Assert.That(highlights.Any(item => item.gameObject.activeSelf
                        && Mathf.Abs(CenterY(item.rectTransform) - CenterY(Title(view, row).rectTransform)) < .5f), Is.True, "row " + row);
                    Assert.That(Title(view, row).color, Is.EqualTo(StoryRecordHighlight.MineText));
                }
                foreach (int row in new[] { 0, 2, 3 }) Assert.That(Title(view, row).color, Is.EqualTo(StoryRecordHighlight.OtherText));
                StoryRecordHighlight.Apply(view.transform, Rows(), true);
                Assert.That(Highlights(view).Where(item => item != null).Count(item => item.gameObject.activeSelf), Is.Zero, "No title, no red.");
            }
            finally { Object.DestroyImmediate(view); }
        }

        [Test]
        public void TheFailureDashboardShowsRealRecordsInsteadOfTheDesignSample()
        {
            GameObject view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Views + "Failure_Dashboard.prefab"));
            try
            {
                StoryRecordHighlight.Apply(view.transform, Rows(1), true);
                string[] texts = view.GetComponentsInChildren<Text>(true).Select(text => text.text).ToArray();
                Assert.That(texts, Does.Not.Contain("P5(나)"));
                Assert.That(texts, Does.Not.Contain("440(20회 타격)"));
                Assert.That(texts, Does.Contain("P2(나)"));
                Assert.That(texts, Does.Contain("50회"));
            }
            finally { Object.DestroyImmediate(view); }
        }
    }
}
