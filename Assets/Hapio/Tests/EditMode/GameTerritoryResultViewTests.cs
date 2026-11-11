using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    public sealed class GameTerritoryResultViewTests
    {
        private Scene scene;
        private GameTerritoryResultView view;

        [SetUp]
        public void Open()
        {
            scene = EditorSceneManager.OpenScene(GameScenePaths.StageResult, OpenSceneMode.Additive);
            view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameTerritoryResultView>(true)).Single();
        }

        [TearDown]
        public void Close() => EditorSceneManager.CloseScene(scene, true);

        [TestCase(LabTeam.Blue, 76, 24)]
        [TestCase(LabTeam.Red, 37, 63)]
        [TestCase(LabTeam.None, 40, 40)]
        [TestCase(LabTeam.Blue, 100, 0)]
        [TestCase(LabTeam.Red, 0, 100)]
        public void FinalSharesAndWinnerFollowSnapshot(LabTeam winner, int blue, int red)
        {
            view.Present(new LabSnapshot { teamWinner = winner, paintCellCount = 100, blueCells = blue, redCells = red }, 7);
            Assert.That(view.blueFill.sizeDelta.x, Is.EqualTo(336 * blue / 100f).Within(.001));
            Assert.That(view.redFill.sizeDelta.x, Is.EqualTo(336 * red / 100f).Within(.001));
            Assert.That(view.blueWinnerFlag.activeSelf, Is.EqualTo(winner == LabTeam.Blue));
            Assert.That(view.redWinnerFlag.activeSelf, Is.EqualTo(winner == LabTeam.Red));
            Assert.That(view.blueDecoration.activeSelf, Is.EqualTo(winner == LabTeam.Blue));
            Assert.That(view.redDecoration.activeSelf, Is.EqualTo(winner == LabTeam.Red));
            Assert.That(view.bluePercent.text, Is.EqualTo(blue + "%"));
            Assert.That(view.redPercent.text, Is.EqualTo(red + "%"));
            Assert.That(view.blueFill.GetComponentInChildren<GameFigmaSurface>(true).SurfaceName, Is.EqualTo("ResultFill"));
            Assert.That(view.redFill.GetComponentInChildren<GameFigmaSurface>(true).SurfaceName, Is.EqualTo("ResultFill"));
            Assert.That(view.table.design.Find("TerritoryBarFrame/Brush Outline").GetComponent<GameFigmaSurface>().SurfaceName,
                Is.EqualTo("TeamPaint"));
            Assert.That(view.blueFill.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
            var clip=view.blueFill.GetComponentInParent<UnityEngine.UI.Mask>(true);
            Assert.That(clip, Is.Not.Null, "Centred fill strokes must stay inside the rounded track.");
            Assert.That(clip.GetComponent<LabRoundedGraphic>().radius, Is.EqualTo(8));
            var blueFlag=(RectTransform)view.blueWinnerFlag.transform;
            var redFlag=(RectTransform)view.redWinnerFlag.transform;
            Assert.That(Vector2.Distance(blueFlag.anchoredPosition,new Vector2(52.60f,-240.87f)),Is.LessThan(.01f));
            Assert.That(Vector2.Distance(redFlag.anchoredPosition,new Vector2(348.56f,-238.59f)),Is.LessThan(.01f));
        }

        [Test]
        public void TiedAwardHighlightsLocalPlayerWithoutMarkingOtherPlayer()
        {
            view.Present(new LabSnapshot { paintCellCount = 100, players = new[] {
                new LabPlayerState { id = 7, seat = 0, damageTotal = 100, lastHit = true },
                new LabPlayerState { id = 8, seat = 1, damageTotal = 100 }
            } }, 7);
            var row = view.table.rows.Find("Award_0");
            Assert.That(row.Find("Highlight").gameObject.activeSelf, Is.True);
            string players = row.Find("Player").GetComponent<UnityEngine.UI.Text>().text;
            Assert.That(players, Does.Contain("P1(나)"));
            Assert.That(players, Does.Not.Contain("P2(나)"));
            Assert.That(view.table.rows.GetComponentsInChildren<UnityEngine.UI.Text>().Any(text => text.text == "막타"), Is.False);
        }

        [Test]
        public void WaitingResultClearsWinnerAndAllArtworkIsLocal()
        {
            view.Present(new LabSnapshot { teamWinner = LabTeam.Red, paintCellCount = 100, redCells = 70 }, 7);
            view.Present(null, 7);
            Assert.That(view.redWinnerFlag.activeSelf, Is.False);
            Assert.That(view.redDecoration.activeSelf, Is.False);
            Assert.That(view.bluePercent.text, Is.EqualTo("—"));
            foreach (var image in view.GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
                Assert.That(image.texture, Is.Not.Null, image.name);
            Assert.That(view.table.homeButton, Is.Not.Null);
            Assert.That(view.table.viewerButton, Is.Not.Null);
            Assert.That(view.table.nextButton, Is.Not.Null);
        }
    }
}
