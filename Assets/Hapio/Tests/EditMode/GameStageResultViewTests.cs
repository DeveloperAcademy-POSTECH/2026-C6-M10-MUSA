using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class GameStageResultViewTests
    {
        [TestCase(GameScenePaths.StageResult, true)]
        [TestCase(GameScenePaths.GameOver, false)]
        public void SavedResultsHaveEditableWidgetsAndRealBackgrounds(string path, bool victory)
        {
            WithView(path, view =>
            {
                Assert.That(view.background.texture, Is.Not.Null);
                Assert.That(view.background.texture.width, Is.GreaterThan(1000));
                Assert.That(view.background.raycastTarget, Is.False);
                Assert.That(view.viewerButton, Is.Not.SameAs(view.homeButton));
                Assert.That(view.nextButton.gameObject.activeSelf, Is.False);
                if (victory) Assert.That(view.viewerButton, Is.Not.Null);
                else Assert.That(view.viewerButton, Is.Null);
                Assert.That(view.yokai.font.name, Is.EqualTo("ShillaCultureBold"));
                Assert.That(view.GetComponentInParent<GameScreen>().legacyResultContent.activeSelf, Is.False);
                Assert.That(view.scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            });
        }

        [Test]
        public void LongTiedAwardsCanScrollAndReturnToAnEmptyResultWithoutStaleRows()
        {
            WithView(GameScenePaths.StageResult, view =>
            {
                var awards = Enumerable.Range(0, 7).Select(i => new LabResultRow("칭호 " + i,
                    "P1(나), P2, P3, P4, P5", "12345회", i == 0)).ToArray();
                view.SetRows(awards);
                Assert.That(view.scroll.vertical, Is.True);
                Assert.That(view.rows.rect.height, Is.EqualTo(280));
                Assert.That(view.rows.Cast<Transform>().Count(child => child.gameObject.activeSelf), Is.EqualTo(7));
                Assert.That(view.rows.Find("Award_0/Highlight").gameObject.activeSelf, Is.True);
                Assert.That(view.rows.Find("Award_1/Highlight").gameObject.activeSelf, Is.False);
                view.SetRows(new LabResultRow[0]);
                Assert.That(view.emptyLabel.gameObject.activeSelf, Is.True);
                Assert.That(view.scroll.vertical, Is.False);
                Assert.That(view.rows.Cast<Transform>().Any(child => child.gameObject.activeSelf), Is.False);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PresentationUsesSnapshotValuesWithoutChangingTheAuthoritativeResult(bool victory)
        {
            WithView(victory ? GameScenePaths.StageResult : GameScenePaths.GameOver, view =>
            {
                var snapshot = new LabSnapshot { paintCoverage = .823f, remaining = 15.2f };
                string before = JsonUtility.ToJson(snapshot);
                view.Present(victory, 2, "야광귀", snapshot, new LabResultRow[0], "안내");
                Assert.That(view.stage.text, Is.EqualTo("STAGE 2"));
                Assert.That(view.yokai.text, Is.EqualTo("야광귀"));
                Assert.That(view.judgment.text, Is.EqualTo(victory ? "봉인 성공 !" : "봉인 실패..."));
                Assert.That(view.paint.text, Is.EqualTo("PAINT 82.3%"));
                Assert.That(view.timeLeft.text, Is.EqualTo("TIME LEFT 16s"));
                Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
            });
        }

        private static void WithView(string path, System.Action<GameStageResultView> action)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameStageResultView>(true)).Single();
                action(view);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
