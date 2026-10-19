using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator EnabledLegacyHudKeepsPaperCompositionAndPhysicsFollowsTheVisibleCompositeCurve()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = Controller();
            controller.lowerHudBoundary.gameObject.SetActive(true);
            controller.ReconfigureBoard();
            LabOrbAreaProfile profile = controller.curvedAreaProfile;
            Assert.That(controller.requireCurvedAreaProfile, Is.True);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.transform, Is.SameAs(controller.board.transform.Find("OrbBoardPlate")));
            Assert.That(profile.SourceTexture, Is.SameAs(profile.GetComponent<Renderer>().sharedMaterial.mainTexture));
            Assert.That(profile.GetComponent<Collider>(), Is.Null);
            Assert.That(profile.LowerBoundary.Count, Is.EqualTo(65));
            Assert.That(profile.UpperBoundary.Count, Is.EqualTo(65));
            Assert.That(profile.LowerOccluderBoundary.Count, Is.EqualTo(65));
            Assert.That(profile.LowerOccluderTexture,
                Is.SameAs(controller.lowerHudBoundary.GetComponent<UnityEngine.UI.Image>().sprite.texture));
            Assert.That(controller.lowerHudGap, Is.Zero, "The real alpha contour needs no conservative rectangular gap.");
            Assert.That(controller.orbInteractionArea.GetComponent<UnityEngine.UI.Graphic>(), Is.Null);
            Assert.That(controller.orbInteractionArea.GetComponent<CanvasRenderer>(), Is.Null);

            Rect hudScreen = HudScreenRect(controller);
            Vector2 hudAnchorMin = controller.lowerHudBoundary.anchorMin;
            Vector2 hudAnchorMax = controller.lowerHudBoundary.anchorMax;
            controller.ReconfigureBoard();
            Assert154Paper(controller);
            Assert.That(HudScreenRect(controller), Is.EqualTo(hudScreen));
            Assert.That(controller.lowerHudBoundary.anchorMin, Is.EqualTo(hudAnchorMin));
            Assert.That(controller.lowerHudBoundary.anchorMax, Is.EqualTo(hudAnchorMax));
            LabOrbAreaShape shape = controller.board.AreaShape;
            Assert.That(shape.IsCurved, Is.True);
            Rect paper = PaperWorldRect(profile);
            Rect hud = ProjectRect(controller.orbCamera, hudScreen);
            for (int i = 0; i <= 64; i++)
            {
                float u = i / 64f;
                float x = Mathf.Lerp(paper.xMin, paper.xMax, u);
                float paperLower = Mathf.Lerp(paper.yMin, paper.yMax, Evaluate(profile.LowerBoundary, u));
                float paperUpper = Mathf.Lerp(paper.yMin, paper.yMax, Evaluate(profile.UpperBoundary, u));
                float hudU = (x - hud.xMin) / hud.width;
                float hudTop = Mathf.Lerp(hud.yMin, hud.yMax, Evaluate(profile.LowerOccluderBoundary, hudU));
                Vector2 physical = shape.VerticalRange(x, 0f);
                Assert.That(physical.x, Is.EqualTo(Mathf.Max(paperLower, hudTop)).Within(.0001f));
                Assert.That(physical.y, Is.EqualTo(paperUpper).Within(.0001f));
            }
            float centreX = shape.CenterBounds.center.x;
            Vector2 centreRange = shape.VerticalRange(centreX, 0f);
            Assert.That(centreRange.x, Is.LessThan(hud.yMax - .001f),
                "The bottom follows the low middle of the HUD curve, not the old rectangle's top.");
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.DrumRadius, out string error), Is.True, error);
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.BoardHeight, out _), Is.False);
            Assert.That(profile.TryBuildShape(paper, hud, controller.config.OrbRadius,
                controller.config.DrumRadius, out LabOrbAreaShape first, out error), Is.True, error);
            Assert.That(profile.TryBuildShape(paper, hud, controller.config.OrbRadius,
                controller.config.DrumRadius, out LabOrbAreaShape second, out error), Is.True, error);
            Assert.That(second, Is.SameAs(first), "Unchanged paper/HUD mapping must reuse its validated shape.");
        }

        [UnityTest]
        public IEnumerator HudOrCurveChangesUpdateOnlyPhysicsAndPreserveOrbIdentityAndSize()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = Controller();
            controller.lowerHudBoundary.gameObject.SetActive(true);
            controller.ReconfigureBoard();
            LabOrbBoard board = controller.board;
            LabOrbAreaShape previous = board.AreaShape;
            Vector3 platePosition = controller.curvedAreaProfile.transform.localPosition;
            Vector3 plateScale = controller.curvedAreaProfile.transform.localScale;
            float cameraSize = controller.orbCamera.orthographicSize;
            Rect cameraViewport = controller.orbCamera.rect;
            var orb = board.UpsertOrb("curve-resize-orb", LabOrbKind.Yin, new Vector2(.5f, .5f));
            Rigidbody body = orb.Body;
            float radius = orb.HitSphere.radius;
            controller.lowerHudBoundary.anchorMax += new Vector2(0f, .006f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(board.AreaShape.ApproximatelyEquals(previous), Is.False);
            Assert.That(board.AreaShape.VerticalRange(board.CenterBounds.center.x, 0f).x,
                Is.GreaterThan(previous.VerticalRange(previous.CenterBounds.center.x, 0f).x));
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(orb.OrbId, Is.EqualTo("curve-resize-orb"));
            Assert.That(orb.Body, Is.SameAs(body));
            Assert.That(orb.HitSphere.radius, Is.EqualTo(radius));
            Assert.That(controller.curvedAreaProfile.transform.localPosition, Is.EqualTo(platePosition));
            Assert.That(controller.curvedAreaProfile.transform.localScale, Is.EqualTo(plateScale));
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
            Assert154Paper(controller);
            board.Clear();
        }

        [UnityTest]
        public IEnumerator SavedSceneRejectsMissingPaperOrHudCurvesInsteadOfRectangularFallback()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = Controller();
            controller.enabled = false;
            LabOrbAreaProfile profile = controller.curvedAreaProfile;
            controller.curvedAreaProfile = null;
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.DrumRadius, out string error), Is.False);
            StringAssert.Contains("requires its baked paper curve profile", error);
            controller.curvedAreaProfile = profile;
            controller.lowerHudBoundary = null;
            Assert.That(controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.DrumRadius, out error), Is.False);
            StringAssert.Contains("lower HUD references", error);
        }

        [UnityTest]
        public IEnumerator RemovedHpBackgroundLeavesThePaperBoundaryWithoutAnInvisibleHudWall()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSceneController controller = Controller();
            Assert.That(controller.lowerHudBoundary.gameObject.activeSelf, Is.False);
            var hud = Object.FindAnyObjectByType<LabHud>();
            Assert.That(hud.playerHpFill.transform.parent.gameObject.activeSelf, Is.False);
            Rect paper = PaperWorldRect(controller.curvedAreaProfile);
            LabOrbAreaShape shape = controller.board.AreaShape;
            foreach (Vector2 uv in controller.curvedAreaProfile.LowerBoundary)
            {
                float x = Mathf.Lerp(paper.xMin, paper.xMax, uv.x);
                Assert.That(shape.VerticalRange(x, 0f).x,
                    Is.EqualTo(Mathf.Lerp(paper.yMin, paper.yMax, uv.y)).Within(.0001f));
            }
            controller.lowerHudBoundary.anchorMax += new Vector2(0f, .1f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(controller.board.AreaShape.ApproximatelyEquals(shape), Is.True,
                "The hidden artwork must not restrict the new combination surface.");
            Assert154Paper(controller);
        }

        private static void Assert154Paper(LabSceneController controller)
        {
            Transform paper = controller.curvedAreaProfile.transform;
            Assert.That(paper.localPosition, Is.EqualTo(new Vector3(0f, 0f, .55f)));
            Assert.That(paper.localScale.x, Is.EqualTo(controller.config.BoardHeight * controller.orbCamera.aspect).Within(.0001f));
            Assert.That(paper.localScale.y, Is.EqualTo(controller.config.BoardHeight).Within(.0001f));
            Assert.That(paper.localScale.z, Is.EqualTo(1f));
            Rect rect = PaperWorldRect(controller.curvedAreaProfile);
            Vector3 bottom = controller.orbCamera.WorldToScreenPoint(new Vector3(rect.center.x, rect.yMin, 0f));
            Vector3 top = controller.orbCamera.WorldToScreenPoint(new Vector3(rect.center.x, rect.yMax, 0f));
            Assert.That(bottom.y, Is.EqualTo(controller.orbCamera.pixelRect.yMin).Within(.001f));
            Assert.That(top.y, Is.EqualTo(controller.orbCamera.pixelRect.yMin
                + controller.orbCamera.pixelHeight).Within(.001f));
        }

        private static Rect PaperWorldRect(LabOrbAreaProfile profile)
        {
            Vector3 min = profile.transform.TransformPoint(new Vector3(-.5f, -.5f, 0f));
            Vector3 max = profile.transform.TransformPoint(new Vector3(.5f, .5f, 0f));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect HudScreenRect(LabSceneController controller)
        {
            Canvas canvas = controller.lowerHudBoundary.GetComponentInParent<Canvas>(true);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return LabOrbAreaGeometry.ScreenRect(controller.lowerHudBoundary, uiCamera, new Vector3[4]);
        }

        private static Rect ProjectRect(Camera camera, Rect rect)
        {
            var plane = new Plane(Vector3.forward, Vector3.zero);
            Vector3 Point(Vector2 screen)
            {
                Ray ray = camera.ScreenPointToRay(screen);
                Assert.That(plane.Raycast(ray, out float distance), Is.True);
                return ray.GetPoint(distance);
            }
            Vector3 min = Point(rect.min), max = Point(rect.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static float Evaluate(IReadOnlyList<Vector2> points, float x)
        {
            for (int i = 1; i < points.Count; i++)
                if (x <= points[i].x)
                    return Mathf.Lerp(points[i - 1].y, points[i].y,
                        Mathf.InverseLerp(points[i - 1].x, points[i].x, x));
            return points[points.Count - 1].y;
        }

        private static LabSceneController Controller() => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LabSceneController>(true)).Single();
    }
}
