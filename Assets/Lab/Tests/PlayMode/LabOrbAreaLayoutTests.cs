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
        public IEnumerator SavedPaperUsesItsCurvedSilhouetteAndStaysAboveTheLowerHud()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = Controller();
            LabOrbAreaProfile profile = controller.curvedAreaProfile;
            Assert.That(controller.requireCurvedAreaProfile, Is.True);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.transform, Is.SameAs(controller.board.transform.Find("OrbBoardPlate")));
            Assert.That(profile.SourceTexture, Is.SameAs(profile.GetComponent<Renderer>().sharedMaterial.mainTexture));
            Assert.That(profile.GetComponent<Collider>(), Is.Null, "The paper itself stays visual-only.");
            Assert.That(controller.orbInteractionArea.GetComponent<UnityEngine.UI.Graphic>(), Is.Null);
            Assert.That(controller.orbInteractionArea.GetComponent<CanvasRenderer>(), Is.Null);
            Assert.That(profile.LowerBoundary.Count, Is.EqualTo(65));
            Assert.That(profile.UpperBoundary.Count, Is.EqualTo(65));
            Assert.That(profile.UpperBoundary.Max(p => p.y) - profile.UpperBoundary.Min(p => p.y),
                Is.GreaterThan(.005f), "The saved top must retain the image's curve instead of a safe rectangle.");
            Assert.That(profile.LowerBoundary.Max(p => p.y) - profile.LowerBoundary.Min(p => p.y),
                Is.GreaterThan(.005f), "The saved bottom must retain the image's curve.");
            controller.ReconfigureBoard();
            LabOrbAreaShape shape = controller.board.AreaShape;
            Assert.That(shape, Is.Not.Null);
            Assert.That(shape.IsCurved, Is.True);
            var corners = new Vector3[4];
            Canvas canvas = controller.orbInteractionArea.GetComponentInParent<Canvas>(true);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Rect hud = LabOrbAreaGeometry.ScreenRect(controller.lowerHudBoundary, uiCamera, corners);
            for (int i = 0; i < profile.LowerBoundary.Count; i++)
            {
                Vector3 lower = profile.transform.TransformPoint(profile.LocalPoint(profile.LowerBoundary[i]));
                Vector3 upper = profile.transform.TransformPoint(profile.LocalPoint(profile.UpperBoundary[i]));
                Assert.That(Vector2.Distance(shape.LowerBoundary[i], new Vector2(lower.x, lower.y)), Is.LessThan(.0001f));
                Assert.That(Vector2.Distance(shape.UpperBoundary[i], new Vector2(upper.x, upper.y)), Is.LessThan(.0001f));
                Assert.That(controller.orbCamera.WorldToScreenPoint(lower).y,
                    Is.GreaterThan(hud.yMax + controller.lowerHudGap * canvas.scaleFactor - .1f));
            }
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.DrumRadius, out string error), Is.True, error);
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.BoardHeight, out _), Is.False);
            Rect profileRect = Rect.MinMaxRect(profile.transform.position.x - profile.transform.lossyScale.x * .5f,
                profile.transform.position.y - profile.transform.lossyScale.y * .5f,
                profile.transform.position.x + profile.transform.lossyScale.x * .5f,
                profile.transform.position.y + profile.transform.lossyScale.y * .5f);
            Assert.That(profile.TryBuildShape(profileRect, controller.config.OrbRadius,
                controller.config.DrumRadius, out LabOrbAreaShape cachedFirst, out error), Is.True, error);
            Assert.That(profile.TryBuildShape(profileRect, controller.config.OrbRadius,
                controller.config.DrumRadius, out LabOrbAreaShape cachedSecond, out error), Is.True, error);
            Assert.That(cachedSecond, Is.SameAs(cachedFirst), "Unchanged layout must reuse its validated shape without rebaking every frame.");
        }

        [UnityTest]
        public IEnumerator ResizingThePaperAndEditingACurveKeepsOrbIdentityAndColliderSize()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = Controller();
            LabOrbBoard board = controller.board;
            LabOrbAreaShape previous = board.AreaShape;
            float cameraSize = controller.orbCamera.orthographicSize;
            Rect cameraViewport = controller.orbCamera.rect;
            var orb = board.UpsertOrb("curve-resize-orb", LabOrbKind.Yin, new Vector2(.5f, .5f));
            Rigidbody body = orb.Body;
            float radius = orb.HitSphere.radius;
            controller.orbInteractionArea.anchorMax = new Vector2(1f,
                controller.orbInteractionArea.anchorMax.y - .015f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(board.AreaShape.ApproximatelyEquals(previous), Is.False);
            Assert.That(board.AreaShape.IsCurved, Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(orb.OrbId, Is.EqualTo("curve-resize-orb"));
            Assert.That(orb.Body, Is.SameAs(body));
            Assert.That(orb.HitSphere.radius, Is.EqualTo(radius));
            Assert.That(controller.orbCamera.orthographicSize, Is.EqualTo(cameraSize));
            Assert.That(controller.orbCamera.rect, Is.EqualTo(cameraViewport));
            Assert.That(board.AreaShape.Contains(new Vector2(body.position.x, body.position.y), radius), Is.True);

            LabOrbAreaProfile profile = controller.curvedAreaProfile;
            int middle = profile.UpperBoundary.Count / 2;
            float before = board.AreaShape.UpperBoundary[middle].y;
            Vector2 point = profile.UpperBoundary[middle];
            profile.SetControlPoint(true, middle, new Vector2(point.x, point.y - .02f));
            yield return null;
            Assert.That(board.AreaShape.UpperBoundary[middle].y, Is.LessThan(before));
            Assert.That(board.Count, Is.EqualTo(1));
            board.Clear();
        }

        [UnityTest]
        public IEnumerator SavedSceneWithAMissingCurveReferenceRejectsTuningInsteadOfRectangularFallback()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = Controller();
            controller.curvedAreaProfile = null;
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.DrumRadius, out string error), Is.False);
            StringAssert.Contains("requires its baked paper curve profile", error);
            // Disable this test instance before LateUpdate so the intentionally invalid reference
            // is asserted here, rather than producing a repeated runtime log in unrelated fixtures.
            controller.enabled = false;
        }

        private static LabSceneController Controller() => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LabSceneController>(true)).Single();
    }
}
