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
