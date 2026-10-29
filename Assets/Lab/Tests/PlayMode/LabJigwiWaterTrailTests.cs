using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabJigwiWaterTrailTests
    {
        private readonly List<Object> owned = new List<Object>();
        private LabConfig config;
        private LabElementTrailProfile profile;
        private GameObject root;

        [SetUp]
        public void Prepare()
        {
            root = Own(new GameObject("B Water trail fixture"));
            config = Own(Object.Instantiate(Resources.Load<LabConfig>("LabConfig")));
            profile = config.OrbAppearance.ElementTrailFor(LabOrbElement.Water);
            Assert.That(profile, Is.Not.Null);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            foreach (Object item in owned) if (item != null) Object.Destroy(item);
            owned.Clear(); yield return null;
            Assert.That(LabJigwiWaterFlightPath.ActivePathCount, Is.Zero, "Scene/projectile cancellation cannot leak global histories.");
        }
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private GameObject Child(string name)
        {
            var value = new GameObject(name); value.transform.SetParent(root.transform, false); return value;
        }
        private LabJigwiWaterFlightPath Path(ulong owner, string id, out Transform source)
        {
            source = Child("Actually moved source").transform;
            var path = Child("Water world history").AddComponent<LabJigwiWaterFlightPath>();
            path.Begin(source, owner, id, profile); return path;
        }
        private Camera WorldCamera(RenderTexture target = null)
        {
            var camera = Child("Real battle projection camera").AddComponent<Camera>();
            camera.enabled = false; camera.transform.position = new Vector3(0f, 0f, -8f);
            camera.orthographic = true; camera.orthographicSize = 4f;
            camera.targetTexture = target; return camera;
        }
        private Canvas CanvasRoot(Camera uiCamera = null)
        {
            var go = new GameObject("Existing test Canvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(root.transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = uiCamera == null ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera; canvas.planeDistance = 10f; return canvas;
        }
        private static LabJigwiWaterTrailGraphic Graphic(Canvas canvas)
        {
            var go = new GameObject("B Water passive trace", typeof(RectTransform), typeof(CanvasRenderer), typeof(LabJigwiWaterTrailGraphic));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go.GetComponent<LabJigwiWaterTrailGraphic>();
        }
        private static IEnumerator Until(Func<bool> condition, string reason)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, reason);
        }

        [UnityTest]
        public IEnumerator AcceptedRewardKeepsWaterAndMetalInkAddsSeparateFireAndStampsBothDetachedResidues()
        {
            var target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 0f, 50f), .8f, 2f);
            Vector3 velocity = new Vector3(.6f, 3f, 8f);
            var host = LabProjectile.Create(Vector3.zero, config, root.transform, LabOrbElement.Water);
            host.AuthorityRevision = 77; host.SetRadiusScale(1.3f); host.SetJigwiABReward(true);
            Assert.That(host.Launch("reward-water", 42, velocity, config, target, _ => { }), Is.True);
            var predicted = LabProjectile.Create(Vector3.right * 2f, config, root.transform, LabOrbElement.Metal);
            predicted.AuthorityRevision = 78; predicted.SetRadiusScale(1.3f); predicted.SetJigwiABReward(true);
            Assert.That(predicted.LaunchVisual(velocity, config, target), Is.True);
            foreach (var shot in new[] { host, predicted })
            {
                Assert.That(shot.JigwiABRewardActive, Is.True);
                Assert.That(shot.ElementFlightTrail.Element,
                    Is.EqualTo(shot == host ? LabOrbElement.Water : LabOrbElement.Metal));
                Assert.That(shot.JigwiRewardFireTrail, Is.Not.Null);
                Assert.That(shot.JigwiRewardFireTrail.IsEmitting, Is.True);
                Assert.That(shot.InitialVelocity, Is.EqualTo(velocity));
                Assert.That(shot.GravityAcceleration, Is.EqualTo(config.EffectiveThrowGravity));
                Assert.That(shot.GetComponent<SphereCollider>().radius, Is.EqualTo(config.ThrowRadius * 1.3f).Within(.00001f));
                Assert.That(shot.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
                Assert.That(shot.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            }
            var actualElement = typeof(LabProjectile).GetField("element", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(actualElement.GetValue(host), Is.EqualTo(LabOrbElement.Water));
            Assert.That(actualElement.GetValue(predicted), Is.EqualTo(LabOrbElement.Metal), "Fire is a reward trail, not a pigment conversion.");
            yield return new WaitForFixedUpdate(); yield return null;
            var trail = predicted.ElementFlightTrail;
            var rewardTrail = predicted.JigwiRewardFireTrail;
            Assert.That(trail.PathCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(rewardTrail.PathCount, Is.GreaterThanOrEqualTo(2));
            int count = trail.PathCount, rewardCount = rewardTrail.PathCount;
            Vector3 position = predicted.transform.position;
            predicted.ReconcileVisual(LabOrbElement.Metal, 1f, 1.3f);
            predicted.SetJigwiABReward(true);
            Assert.That(predicted.ElementFlightTrail, Is.SameAs(trail));
            Assert.That(trail.PathCount, Is.EqualTo(count), "Repeated accepted reward flags cannot restart the actual flight history.");
            Assert.That(predicted.transform.position, Is.EqualTo(position));
            Assert.That(trail.Element, Is.EqualTo(LabOrbElement.Metal));
            Assert.That(predicted.JigwiRewardFireTrail, Is.SameAs(rewardTrail));
            Assert.That(rewardTrail.PathCount, Is.EqualTo(rewardCount),
                "There is no live reward clock or repeated approval that truncates an already approved reward history.");
            // Exercise the existing replica-resolution lifecycle, without claiming a physical target hit.
            predicted.SendMessage("FinishVisual");
            yield return null;
            var residue = root.GetComponentsInChildren<LabProjectileRevisionStamp>(true);
            Assert.That(Array.Exists(residue, stamp => stamp.Revision == 78), Is.True,
                "Detached flight history keeps the accepted revision for bounded failure cleanup.");
            Assert.That(rewardTrail.GetComponent<LabProjectileRevisionStamp>().Revision, Is.EqualTo(78UL));
            Assert.That(rewardTrail.transform.parent, Is.EqualTo(root.transform));
            var hostRewardTrail = host.JigwiRewardFireTrail;
            host.gameObject.SetActive(false);
            Assert.That(host.JigwiABRewardActive, Is.False);
            Assert.That(host.ElementFlightTrail.PathCount, Is.Zero, "Cancelled/reused objects cannot join old ink histories.");
            Assert.That(hostRewardTrail == null || !hostRewardTrail.IsEmitting && hostRewardTrail.PathCount == 0, Is.True,
                "Cancellation clears the dedicated reward history as well as the original elemental ink.");
        }

        [UnityTest]
        public IEnumerator AuthoritativeAndPredictedWaterKeepTheirPhysicalProfilesAndOnlyBOwnerPathsAreOptedIn()
        {
            var target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 0f, 50f), .8f, 2f);
            var curtain = LabJigwiFireSurface.CreateCurtain(root.transform, 42, "b-owner", "b-owner/42/curtain",
                new Vector3(0f, 0f, 20f), Quaternion.identity, Vector2.one, true);
            Vector3 velocity = new Vector3(.6f, 3f, 8f);
            var host = LabProjectile.Create(Vector3.zero, config, root.transform, LabOrbElement.Water);
            host.SetFireSurfaces(new[] { curtain });
            Assert.That(host.Launch("host-water", 42, velocity, config, target, _ => { }), Is.True);
            var predicted = LabProjectile.Create(Vector3.right * 2f, config, root.transform, LabOrbElement.Water);
            predicted.SetFireSurfaces(new[] { curtain });
            Assert.That(predicted.LaunchVisual(velocity, config, target), Is.True);
            foreach (var shot in new[] { host, predicted })
            {
                Assert.That(shot.JigwiWaterFlightPath, Is.Not.Null);
                Assert.That(shot.JigwiWaterFlightPath.Owner, Is.EqualTo(42ul), "Visual shots infer owner from their existing owner-scoped curtain list.");
                Assert.That(shot.JigwiWaterFlightPath.FireEventId, Is.EqualTo("b-owner"));
                Assert.That(shot.ElementFlightTrail, Is.Not.Null, "The existing 3D ink is retained.");
                Assert.That(shot.ElementFlightTrail.Profile, Is.SameAs(profile));
                Assert.That(shot.InitialVelocity, Is.EqualTo(velocity));
                Assert.That(shot.GravityAcceleration, Is.EqualTo(config.EffectiveThrowGravity));
                Assert.That(shot.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
                Assert.That(shot.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            }
            var originalPath = predicted.JigwiWaterFlightPath;
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(originalPath.Count, Is.GreaterThanOrEqualTo(2));
            int before = originalPath.Count;
            predicted.ReconcileVisual(LabOrbElement.Water, 1f, 1f);
            Assert.That(predicted.JigwiWaterFlightPath, Is.SameAs(originalPath));
            Assert.That(originalPath.Count, Is.EqualTo(before), "Approval cannot restart or duplicate the predicted trace.");
            var foreign = LabProjectile.Create(Vector3.left * 2f, config, root.transform, LabOrbElement.Water);
            foreign.SetFireSurfaces(new[] { curtain }); foreign.Launch("foreign", 7, velocity, config, target, _ => { });
            Assert.That(foreign.JigwiWaterFlightPath, Is.Null, "A foreign curtain cannot label an authoritative shot as its owner.");
            var ordinary = LabProjectile.Create(Vector3.left * 3f, config, root.transform, LabOrbElement.Water);
            ordinary.LaunchVisual(velocity, config, target);
            Assert.That(ordinary.JigwiWaterFlightPath, Is.Null, "Normal Water outside B keeps its existing effect only.");
            var fire = LabProjectile.Create(Vector3.right * 3f, config, root.transform, LabOrbElement.Fire);
            fire.SetFireSurfaces(new[] { curtain }); fire.LaunchVisual(velocity, config, target);
            Assert.That(fire.JigwiWaterFlightPath, Is.Null);
            predicted.ReconcileVisual(LabOrbElement.Fire, 1f, 1f);
            Assert.That(predicted.JigwiWaterFlightPath, Is.Null, "A rejected Water prediction cannot keep a blue trace after element reconciliation.");
        }

        [UnityTest]
        public IEnumerator WorldHistoryKeepsTheVisitedCornerAndPauseReuseAndCancellationDoNotJoinOldFlights()
        {
            var path = Path(42, "corner", out Transform source);
            Vector3 corner = new Vector3(.5f, 0f, 0f), end = new Vector3(.5f, .5f, 0f);
            source.position = corner;
            yield return Until(() => path.Count >= 2, "Record the real first movement.");
            source.position = end;
            yield return Until(() => path.Count >= 3, "Record the actual corner, not an extended aiming line.");
            Assert.That(path.GetPosition(1), Is.EqualTo(corner));
            Assert.That(path.GetPosition(path.Count - 1), Is.EqualTo(end));
            Time.timeScale = 0f;
            int count = path.Count; Vector3 oldest = path.GetPosition(0);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(path.Count, Is.EqualTo(count)); Assert.That(path.GetPosition(0), Is.EqualTo(oldest));
            var next = Child("Reused far-away source").transform; next.position = Vector3.right * 15f;
            path.Begin(next, 7, "new-event", profile);
            Assert.That(path.Count, Is.EqualTo(1)); Assert.That(path.GetPosition(0), Is.EqualTo(next.position));
            Assert.That(path.Owner, Is.EqualTo(7ul)); Assert.That(path.FireEventId, Is.EqualTo("new-event"));
            next.gameObject.SetActive(false);
            Assert.That(path.Count, Is.Zero); Assert.That(LabJigwiWaterFlightPath.ActivePathCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CanvasFiltersOtherOwnersAndOldEventsAndFreezesOnPauseWhileActualPositionsAreProjected()
        {
            Camera camera = WorldCamera(); Canvas canvas = CanvasRoot(); var graphic = Graphic(canvas);
            var path = Path(42, "visible-event", out Transform source);
            Path(7, "visible-event", out _); Path(42, "old-event", out _);
            source.position = new Vector3(.6f, .8f, 0f);
            yield return Until(() => path.Count >= 2, "Wait for actual source history.");
            Canvas.ForceUpdateCanvases(); graphic.Present(canvas, camera, 42, "visible-event", 1d, false);
            Assert.That(graphic.VisibleTraceCount, Is.EqualTo(1));
            Assert.That(graphic.ProjectedPointCount, Is.EqualTo(path.Count));
            for (int i = 0; i < path.Count; i++)
            {
                Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform,
                    camera.WorldToScreenPoint(path.GetPosition(i)), null, out Vector2 expected), Is.True);
                Assert.That(Vector2.Distance(graphic.GetProjectedPoint(i), expected), Is.LessThan(.001f));
            }
            Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(graphic.GetComponentsInChildren<Collider>(true), Is.Empty);
            Time.timeScale = 0f; graphic.Present(canvas, camera, 42, "visible-event", 1d, true);
            int count = graphic.ProjectedPointCount; Vector2 frozen = graphic.GetProjectedPoint(count - 1);
            yield return new WaitForSecondsRealtime(.08f);
            graphic.ContinueFading(true);
            Assert.That(graphic.ProjectedPointCount, Is.EqualTo(count)); Assert.That(graphic.GetProjectedPoint(count - 1), Is.EqualTo(frozen));
            graphic.Hide(); Assert.That(graphic.VisibleTraceCount, Is.Zero); Assert.That(graphic.ProjectedPointCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LastIgnitionCompletionKeepsTheRealWaterTraceBrieflyThenFadesAndPhaseExitClearsIt()
        {
            Camera camera = WorldCamera(); Canvas canvas = CanvasRoot();
            var overlayObject = new GameObject("Saved-style B presentation", typeof(RectTransform), typeof(CanvasGroup), typeof(LabJigwiScreenPresentation));
            overlayObject.transform.SetParent(canvas.transform, false);
            var view = overlayObject.GetComponent<LabJigwiScreenPresentation>();
            view.Bind(null, canvas, camera); view.enabled = false; // Manually feed snapshots; no live network fixture clock.
            var group = new LabJigwiABRound { id = "complete-b", kind = LabJigwiABKind.GroupFire,
                warningAt = 0d, activeAt = 0d, endsAt = 100d,
                players = new[] { new LabJigwiABPlayerFire { playerId = 42,
                    targets = new[] { new LabJigwiABFireTarget { id = "one" } } } } };
            var snapshot = new LabSnapshot { sessionId = "water-tail-same-session", round = 1, phase = LabPhase.Playing,
                serverTime = 1d, players = new[] { new LabPlayerState { id = 42, seat = 0 } },
                jigwiAB = new LabJigwiABSnapshot { enabled = true, startedAt = 0d, cutoffAt = 100d, rounds = new[] { group } } };
            view.ApplySnapshot(snapshot, 42, 1d);
            var path = Path(42, group.id, out Transform source);
            source.position = new Vector3(.4f, .5f, 0f);
            yield return Until(() => path.Count >= 2, "Record the final actual flight before completion.");
            view.ApplySnapshot(snapshot, 42, 1d);
            Assert.That(view.waterFlightTrail.VisibleTraceCount, Is.EqualTo(1));
            float lifetime = path.Lifetime; path.StopAndFade(root.transform);
            group.resolution = LabJigwiABResolution.Completed; group.resolvedAt = 1.1d;
            group.players[0].targets[0].lit = false;
            view.ApplySnapshot(snapshot, 42, 1.1d);
            Assert.That(view.waterFlightTrail.VisibleTraceCount, Is.EqualTo(1), "B completion removes the curtain but cannot erase the final Water tail at once.");
            yield return new WaitForSeconds(lifetime + .06f); yield return null;
            Assert.That(view.waterFlightTrail.VisibleTraceCount, Is.Zero);
            Assert.That(path == null, Is.True);
            var fresh = Path(42, group.id, out _);
            view.waterFlightTrail.Present(canvas, camera, 42, group.id, 1.2d, false);
            Assert.That(view.waterFlightTrail.VisibleTraceCount, Is.EqualTo(1));
            snapshot.phase = LabPhase.Victory; view.ApplySnapshot(snapshot, 42, 1.2d);
            Assert.That(view.waterFlightTrail.ProjectedPointCount, Is.Zero);
            fresh.Clear();
        }

        [UnityTest]
        public IEnumerator ResolvedRealWaterFlightLeavesAFadingPathAndCancellingItsProjectileRootRemovesIt()
        {
            var target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 0f, 50f), .8f, 2f);
            var curtain = LabJigwiFireSurface.CreateCurtain(root.transform, 42, "actual-b", "actual-b/42/curtain",
                new Vector3(0f, 0f, .9f), Quaternion.identity, Vector2.one, true);
            var shot = LabProjectile.Create(Vector3.zero, config, root.transform, LabOrbElement.Water);
            shot.SetFireSurfaces(new[] { curtain }); bool hit = false;
            shot.Launch("actual-hit", 42, new Vector3(0f, 2f, 8f), config, target, _ => hit = true);
            var history = shot.JigwiWaterFlightPath;
            yield return Until(() => hit, "A real projectile must resolve against its B curtain.");
            Assert.That(history, Is.Not.Null); Assert.That(history.IsEmitting, Is.False);
            Assert.That(history.transform.parent, Is.SameAs(root.transform)); Assert.That(history.Count, Is.GreaterThanOrEqualTo(1));
            Time.timeScale = 0f; int count = history.Count;
            yield return new WaitForSecondsRealtime(.08f); Assert.That(history.Count, Is.EqualTo(count));
            root.SetActive(false);
            Assert.That(history.Count, Is.Zero); Assert.That(LabJigwiWaterFlightPath.ActivePathCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ARegistryFloodRemainsBoundedAndRetiresOldDetachedVisualRoots()
        {
            var first = Path(0, "flood", out _); first.StopAndFade(root.transform);
            for (int i = 0; i < 64; i++) Path((ulong)i, "flood-" + i, out _);
            Assert.That(LabJigwiWaterFlightPath.ActivePathCount, Is.EqualTo(64));
            yield return null;
            Assert.That(first == null, Is.True, "Evicted detached histories cannot become empty roots whose fade timer was cancelled.");
            root.SetActive(false); Assert.That(LabJigwiWaterFlightPath.ActivePathCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ActualWaterInkAndHeadRenderOnTopOfAnOpaqueRedCurtainWithoutChangingWorldMotion()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("A GPU is required for real B water pixels.");
            const int width = 390, height = 844, layer = 31;
            var renderTarget = Own(new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32));
            Assert.That(renderTarget.Create(), Is.True);
            Camera world = WorldCamera(renderTarget);
            var ui = Child("B Water temporary UI camera").AddComponent<Camera>(); ui.enabled = false;
            ui.transform.position = new Vector3(0f, 0f, -20f); ui.orthographic = true; ui.orthographicSize = height * .5f;
            ui.nearClipPlane = .01f; ui.farClipPlane = 100f; ui.clearFlags = CameraClearFlags.SolidColor;
            ui.backgroundColor = Color.black; ui.cullingMask = 1 << layer; ui.targetTexture = renderTarget;
            Canvas canvas = CanvasRoot(ui);
            var curtainObject = new GameObject("Opaque B curtain fixture", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var curtain = curtainObject.GetComponent<UnityEngine.UI.Image>(); curtain.transform.SetParent(canvas.transform, false);
            var rect = curtain.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; curtain.color = new Color(.7f, .015f, .005f, 1f); curtain.raycastTarget = false;
            var graphic = Graphic(canvas);
            var path = Path(42, "gpu-b", out Transform source);
            source.position = new Vector3(-.35f, -.7f, 0f);
            yield return Until(() => path.Count >= 2, "Record first real water position.");
            source.position = new Vector3(.45f, .65f, 0f);
            yield return Until(() => path.Count >= 3, "Record an actual corner before rendering.");
            graphic.Present(canvas, world, 42, "gpu-b", 1d, false);
            foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = layer;
            Canvas.ForceUpdateCanvases(); ui.Render();
            var readback = Own(new Texture2D(width, height, TextureFormat.RGBA32, false));
            RenderTexture previous = RenderTexture.active;
            try { RenderTexture.active = renderTarget; readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0); readback.Apply(); }
            finally { RenderTexture.active = previous; }
            Color32[] pixels = readback.GetPixels32();
            int blue = 0, red = 0, magenta = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.b > 120 && pixel.g > 65 && pixel.b > pixel.r * 1.25f) blue++;
                if (pixel.r > 80 && pixel.r > pixel.b * 3) red++;
                if (pixel.r > 220 && pixel.b > 180 && pixel.g < 60) magenta++;
            }
            Assert.That(red, Is.GreaterThan(width * height / 2), "The opaque curtain really rendered behind the trace.");
            Assert.That(blue, Is.GreaterThan(48), "The 3D water's real path and head must be visible above the opaque Canvas curtain.");
            Vector3 cornerPixel = world.WorldToScreenPoint(new Vector3(-.35f, -.7f, 0f));
            int cornerBlue = 0;
            for (int y = Mathf.Max(0, Mathf.RoundToInt(cornerPixel.y) - 7); y <= Mathf.Min(height - 1, Mathf.RoundToInt(cornerPixel.y) + 7); y++)
                for (int x = Mathf.Max(0, Mathf.RoundToInt(cornerPixel.x) - 7); x <= Mathf.Min(width - 1, Mathf.RoundToInt(cornerPixel.x) + 7); x++)
                {
                    Color32 pixel = pixels[y * width + x];
                    if (pixel.b > 120 && pixel.g > 65 && pixel.b > pixel.r * 1.25f) cornerBlue++;
                }
            Assert.That(cornerBlue, Is.GreaterThan(6), "Blue must also render at an older visited corner, well away from the current orb head.");
            Assert.That(magenta, Is.LessThan(4));
            Assert.That(graphic.ProjectedPointCount, Is.EqualTo(path.Count));
            Assert.That(source.position, Is.EqualTo(new Vector3(.45f, .65f, 0f)));
            Assert.That(graphic.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jigwi341-b-water-trail-gpu.png"), readback.EncodeToPNG());
            TestContext.WriteLine("JIGWI_B_WATER_GPU bluePixels=" + blue + " opaqueCurtainPixels=" + red + " pathPoints=" + path.Count);
        }
    }
}
