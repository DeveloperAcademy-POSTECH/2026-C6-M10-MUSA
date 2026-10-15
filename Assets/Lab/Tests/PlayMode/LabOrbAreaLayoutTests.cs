using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabOrbAreaLayoutTests
    {
        [UnityTearDown]
        public IEnumerator UnloadSavedBoardBeforeOtherPhysicsFixtures()
        {
            Scene lab = SceneManager.GetSceneByName("Lab");
            if (!lab.IsValid() || !lab.isLoaded) yield break;
            Scene empty = SceneManager.CreateScene("Area test cleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(lab);
        }

        [UnityTest]
        public IEnumerator SavedAreaKeepsOrbsAndDrumAboveTheActualLowerHud()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabSceneController>(true)).Single();
            Assert.That(controller.orbInteractionArea, Is.Not.Null);
            Assert.That(controller.lowerHudBoundary, Is.Not.Null);
            Assert.That(controller.orbInteractionArea.GetComponent<UnityEngine.UI.Graphic>(), Is.Null);
            Canvas.ForceUpdateCanvases();
            controller.ReconfigureBoard();
            Rect bounds = controller.board.CenterBounds;
            Canvas canvas = controller.orbInteractionArea.GetComponentInParent<Canvas>(true);
            var corners = new Vector3[4];
            Rect hud = LabOrbAreaGeometry.ScreenRect(controller.lowerHudBoundary, null, corners);
            Rect area = LabOrbAreaGeometry.ScreenRect(controller.orbInteractionArea, null, corners);
            float radius = controller.config.OrbRadius;
            Vector3 bottom = controller.orbCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.yMin - radius, 0f));
            Vector3 top = controller.orbCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.yMax + radius, 0f));
            Assert.That(bottom.y, Is.GreaterThan(hud.yMax + controller.lowerHudGap * canvas.scaleFactor));
            Assert.That(top.y, Is.LessThan(area.yMax));
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight, radius,
                controller.config.DrumRadius, out string error), Is.True, error);
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight, radius,
                controller.config.BoardHeight, out _), Is.False);

            // Moving the editable guide changes the physical area, without reinitialising the board.
            var orb = controller.board.UpsertOrb("area-resize-orb", LabOrbKind.Yin, new Vector2(.5f, .5f));
            controller.orbInteractionArea.anchorMax = new Vector2(1f, .38f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(controller.board.CenterBounds.yMax, Is.LessThan(bounds.yMax));
            Assert.That(controller.board.Count, Is.EqualTo(1));
            Assert.That(orb.OrbId, Is.EqualTo("area-resize-orb"));
            controller.board.Clear();
        }
    }
}
