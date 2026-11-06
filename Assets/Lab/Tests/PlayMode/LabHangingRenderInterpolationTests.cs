using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Physics keeps the fixed-step gourd pose; only the drawn pose is interpolated between fixed steps (#454).</summary>
    public sealed class LabHangingRenderInterpolationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly List<RenderTexture> targets = new List<RenderTexture>();
        private float timeScale;

        [SetUp]
        public void SetUp() { timeScale = Time.timeScale; Time.timeScale = 1f; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in owned) if (item != null) Object.Destroy(item);
            owned.Clear();
            foreach (RenderTexture texture in targets) if (texture != null) { texture.Release(); Object.Destroy(texture); }
            targets.Clear();
            yield return null;
            Time.timeScale = timeScale;
        }

        /// <summary>A camera this test renders itself: batch mode never renders the player loop.</summary>
        private Camera RenderCamera()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device (-nographics): no render pass can run.");
            var texture = new RenderTexture(64, 64, 16);
            Assert.That(texture.Create(), Is.True);
            targets.Add(texture);
            var camera = new GameObject("Interpolation camera").AddComponent<Camera>();
            owned.Add(camera.gameObject);
            camera.enabled = false;
            camera.targetTexture = texture;
            camera.transform.position = new Vector3(0f, 1f, -8f);
            return camera;
        }

        [UnityTest]
        public IEnumerator RenderingDrawsTheMidpointAndEveryScriptReadsTheFixedPose()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            LabHangingTarget.RigState previous = Pose(hanging, "previousPose"), current = Pose(hanging, "currentPose");
            Assume.That(Mathf.Abs(current.Yaw - previous.Yaw), Is.GreaterThan(.5f), "The gourd must be turning.");
            AssertDrawn(target, current, "After a fixed step the transforms hold the fixed pose.");
            Assert.That(current.Yaw, Is.EqualTo(hanging.YawAngleDegrees), "The fixed pose is the simulated state.");
            Assert.That(current.Swing, Is.EqualTo(hanging.SwingAngles));

            Invoke(hanging, "ApplyRenderPose", .5f);
            Field(hanging, "drawingInterpolated", true);
            AssertDrawn(target, LabHangingTarget.RigState.Lerp(previous, current, .5f), "Drawn half-way between the fixed poses.");
            Invoke(hanging, "OnEndRendering", default(ScriptableRenderContext), null);
            AssertDrawn(target, current, "Rendering ends by restoring the fixed pose.");

            // A FixedUpdate that does not integrate (held) still writes the fixed pose before anything reads it.
            Invoke(hanging, "ApplyRenderPose", .5f);
            hanging.SetSimulationHeld(true);
            Invoke(hanging, "FixedUpdate");
            AssertDrawn(target, current, "Physics and later FixedUpdate readers see the fixed pose.");
            hanging.SetSimulationHeld(false);
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator OutsideRenderingTheTransformsAlwaysHoldTheFixedPose()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            Camera camera = RenderCamera();
            int interpolated = 0;
            // Subscribed after the target, so this sees the pose the render pass draws.
            void Drawn(ScriptableRenderContext context, List<Camera> cameras)
            { if (!Draws(target, Pose(hanging, "currentPose"))) interpolated++; }
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate(); // Two different fixed poses to blend.
            RenderPipelineManager.beginContextRendering += Drawn;
            try
            {
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null; // After Update, before LateUpdate.
                    AssertDrawn(target, Pose(hanging, "currentPose"), "Update readers (the Host's paint-flow sample).");
                    // Batch mode renders no player loop and never reaches WaitForEndOfFrame (the Test Framework fails
                    // that yield there), so this frame's real URP pass is driven here and must end on the fixed pose.
                    camera.Render();
                    AssertDrawn(target, Pose(hanging, "currentPose"), "Readers after a render pass.");
                    if (Application.isBatchMode) continue;
                    yield return new WaitForEndOfFrame(); // After LateUpdate and the Editor's own rendering.
                    AssertDrawn(target, Pose(hanging, "currentPose"), "End-of-frame readers.");
                }
            }
            finally { RenderPipelineManager.beginContextRendering -= Drawn; }
            Assert.That(interpolated, Is.GreaterThan(0),
                "The render passes drew an interpolated pose, so the restores checked above actually happened.");
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator DuringRealRenderingTheDrawnPoseLiesBetweenTheTwoFixedPoses()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            Camera camera = RenderCamera();
            Transform twist = target.transform.Find("Suspension Pivot/Body Twist Pivot");
            int renders = 0, turning = 0, between = 0;
            void Observe(ScriptableRenderContext context, List<Camera> cameras)
            {
                renders++;
                LabHangingTarget.RigState from = Pose(hanging, "previousPose"), to = Pose(hanging, "currentPose");
                if (Mathf.Abs(to.Yaw - from.Yaw) < .5f) return;
                turning++;
                float drawn = twist.localEulerAngles.y, low = Mathf.Min(from.Yaw, to.Yaw), high = Mathf.Max(from.Yaw, to.Yaw);
                // Compare on the circle: the transform reports 0..360, the poses are unwrapped.
                float t = Mathf.InverseLerp(from.Yaw, to.Yaw, from.Yaw + Mathf.DeltaAngle(from.Yaw, drawn));
                if (Mathf.Abs(Mathf.DeltaAngle(LabHangingTarget.RigState.Lerp(from, to, t).Yaw, drawn)) < .05f
                    && t > .001f && t < .999f) between++;
            }
            // Batch frames are ~0.1 ms, so 40 of them can pass between two fixed steps: start from two kept poses.
            for (int i = 0; i < 2; i++) yield return new WaitForFixedUpdate();
            // Subscribed after the target, so this runs once the drawn pose is in place.
            RenderPipelineManager.beginContextRendering += Observe;
            try
            {
                for (int frame = 0; frame < 40; frame++)
                {
                    yield return null;
                    // The Editor renders its views on its own; batch mode renders nothing, so each frame also
                    // drives one real URP pass. Its alpha is this frame's Time.time, as in the player loop.
                    camera.Render();
                }
            }
            finally { RenderPipelineManager.beginContextRendering -= Observe; }
            if (renders == 0) Assert.Inconclusive("No render pass ran in this test environment.");
            Assert.That(turning, Is.GreaterThan(0), "Rendering ran with two different fixed poses kept (a pinned pose would hide interpolation).");
            Assert.That(between, Is.GreaterThan(0), "Some rendered frames drew a pose strictly between the fixed ones.");
            AssertDrawn(target, Pose(hanging, "currentPose"), "And outside rendering the fixed pose is back.");
        }

        [UnityTest]
        public IEnumerator DrawnPointMapsAPointOnTheBodyToWhereRenderingDrawsIt()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            yield return null; // A frame part-way between fixed steps.
            Vector3 local = new Vector3(.3f, .5f, -.2f);
            Vector3 fixedPoint = target.Hitbox.transform.TransformPoint(local);
            Vector3 mapped = hanging.DrawnPoint(fixedPoint);
            float alpha = (float)typeof(LabHangingTarget).GetMethod("RenderAlpha", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Invoke(hanging, "ApplyRenderPose", alpha);
            Vector3 drawn = target.Hitbox.transform.TransformPoint(local);
            Invoke(hanging, "WritePose", Pose(hanging, "currentPose"));
            Assert.That(Vector3.Distance(mapped, drawn), Is.LessThan(1e-4f), "Screen UI lands on the drawn gourd.");
            Assert.That(LabHangingTarget.DrawnPoint(target.Hitbox, fixedPoint), Is.EqualTo(mapped), "Found from any part of the target.");
            hanging.SetSimulationHeld(true);
            Assert.That(hanging.DrawnPoint(fixedPoint), Is.EqualTo(fixedPoint), "Held: nothing is interpolated.");
            hanging.SetSimulationHeld(false);
        }

        [UnityTest]
        public IEnumerator AHeldTargetDrawsItsFixedPoseAndCannotStepBackOnResume()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            LabHangingTarget.RigState current = Pose(hanging, "currentPose");
            Assume.That(Pose(hanging, "previousPose").Yaw, Is.Not.EqualTo(current.Yaw));
            hanging.SetSimulationHeld(true);
            Invoke(hanging, "OnBeginRendering", default(ScriptableRenderContext), null);
            AssertDrawn(target, current, "Held: the start barrier and pause show one pose.");
            Assert.That(Pose(hanging, "previousPose").Yaw, Is.EqualTo(current.Yaw),
                "Both poses are pinned, so resuming cannot draw an older pose than the paused one.");
            hanging.SetSimulationHeld(false);
        }

        [UnityTest]
        public IEnumerator ARunningCorrectionMovesBothPosesAndAPausedOnePinsThem()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            LabHangingTarget.RigState previous = Pose(hanging, "previousPose"), current = Pose(hanging, "currentPose");
            float step = current.Yaw - previous.Yaw;
            hanging.ReconcileYaw(current.Yaw + 120f, hanging.YawSpeedDegreesPerSecond);
            LabHangingTarget.RigState shiftedPrevious = Pose(hanging, "previousPose"), shifted = Pose(hanging, "currentPose");
            Assert.That(shifted.Yaw, Is.EqualTo(current.Yaw + 120f).Within(1e-3f));
            Assert.That(shifted.Yaw - shiftedPrevious.Yaw, Is.EqualTo(step).Within(1e-3f),
                "The interpolation step is kept, so a 10 Hz reconcile does not make the drawn pose jump.");
            hanging.Kick(new Vector3(0f, 0f, -3f));
            Assert.That(Pose(hanging, "currentPose").Yaw - Pose(hanging, "previousPose").Yaw, Is.EqualTo(step).Within(1e-3f));

            hanging.ReconcileYaw(40f, 0f, true);
            Assert.That(Pose(hanging, "previousPose").Yaw, Is.EqualTo(40f));
            Assert.That(Pose(hanging, "currentPose").Yaw, Is.EqualTo(40f), "A paused sample pins one pose.");
            Invoke(hanging, "ApplyRenderPose", .5f);
            AssertDrawn(target, Pose(hanging, "currentPose"), "Nothing left to interpolate.");
        }

        [UnityTest]
        public IEnumerator TheFrameAlphaStaysBetweenTheFixedStepsAndVaries()
        {
            MethodInfo alpha = typeof(LabHangingTarget).GetMethod("RenderAlpha", BindingFlags.Static | BindingFlags.NonPublic);
            var seen = new HashSet<float>();
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                float value = (float)alpha.Invoke(null, null);
                Assert.That(value, Is.InRange(0f, 1f));
                seen.Add(Mathf.Round(value * 100f));
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "Time.time − Time.fixedTime moves between fixed steps in this Editor.");
        }

        [Test]
        public void CorrectingAPresentedImpactEqualsTheHostImpactAndUndoingRemovesItEvenWhenClamped()
        {
            LabTarget predicted = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f); owned.Add(predicted.gameObject);
            LabTarget host = LabTarget.CreateCylinder(null, Vector3.right * 20f, .6f, 2.7f); owned.Add(host.gameObject);
            LabHangingTarget a = predicted.gameObject.AddComponent<LabHangingTarget>();
            LabHangingTarget b = host.gameObject.AddComponent<LabHangingTarget>();
            Vector3 offset = Vector3.right * .4f; // Off-axis, so the impact also spins the gourd.
            Vector3 guess = new Vector3(1f, 0f, -4f), actual = new Vector3(2f, .5f, -5f);
            float predictedSpin = a.ApplyPredictedImpact(predicted.Hitbox.bounds.center + offset, guess, out var applied);
            Assert.That(applied.Yaw, Is.EqualTo(predictedSpin).Within(1e-3f));
            Assert.That(Mathf.Abs(applied.Yaw), Is.GreaterThan(.1f), "The test needs a real spin.");
            float hostSpin = b.ApplyHostImpact(host.Hitbox.bounds.center + offset, actual);
            a.CorrectPredictedImpact(applied, actual, hostSpin);
            Assert.That(Vector2.Distance(SwingVelocity(a), SwingVelocity(b)), Is.LessThan(1e-3f), "#459: the Host's impact replaces the guess.");
            Assert.That(a.VerticalVelocity, Is.EqualTo(b.VerticalVelocity).Within(1e-4f));
            Assert.That(a.YawSpeedDegreesPerSecond, Is.EqualTo(b.YawSpeedDegreesPerSecond).Within(1e-3f));

            // Near the swing limit the presented kick is clamped; undoing removes exactly what it added.
            a.ResetSwing();
            for (int i = 0; i < 6; i++) a.Kick(new Vector3(0f, 0f, -30f));
            Vector2 before = SwingVelocity(a); float bounceBefore = a.VerticalVelocity, yawBefore = a.YawSpeedDegreesPerSecond;
            a.ApplyPredictedImpact(predicted.Hitbox.bounds.center + offset, new Vector3(0f, 0f, -30f), out var clamped);
            a.UndoPredictedImpact(clamped);
            Assert.That(Vector2.Distance(SwingVelocity(a), before), Is.LessThan(1e-3f), "A Host miss takes the presented impact back out.");
            Assert.That(a.VerticalVelocity, Is.EqualTo(bounceBefore).Within(1e-4f));
            Assert.That(a.YawSpeedDegreesPerSecond, Is.EqualTo(yawBefore).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator ALateVerdictTakesOutWhatThePresentedImpactHasBecomeNotACounterKick()
        {
            LabTarget predicted = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f); owned.Add(predicted.gameObject);
            LabTarget untouched = LabTarget.CreateCylinder(null, Vector3.right * 20f, .6f, 2.7f); owned.Add(untouched.gameObject);
            LabTarget host = LabTarget.CreateCylinder(null, Vector3.right * 40f, .6f, 2.7f); owned.Add(host.gameObject);
            LabHangingTarget a = predicted.gameObject.AddComponent<LabHangingTarget>();
            LabHangingTarget quiet = untouched.gameObject.AddComponent<LabHangingTarget>();
            LabHangingTarget b = host.gameObject.AddComponent<LabHangingTarget>();
            yield return new WaitForFixedUpdate();
            Vector3 offset = Vector3.right * .4f, guess = new Vector3(1f, 0f, -6f), actual = new Vector3(2f, .5f, -5f);
            a.ApplyPredictedImpact(predicted.Hitbox.bounds.center + offset, guess, out var applied);
            float hostSpin = b.ApplyHostImpact(host.Hitbox.bounds.center + offset, actual);
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate(); // About 0.8 s of swinging.

            // Correcting late matches the gourd the Host's impact alone would have moved.
            a.CorrectPredictedImpact(applied, actual, hostSpin);
            Assert.That(Vector2.Distance(a.SwingAngles, b.SwingAngles), Is.LessThan(.6f), "Swing angle follows the Host's impact.");
            Assert.That(Vector2.Distance(SwingVelocity(a), SwingVelocity(b)), Is.LessThan(2f));
            Assert.That(a.YawSpeedDegreesPerSecond, Is.EqualTo(b.YawSpeedDegreesPerSecond).Within(Mathf.Abs(hostSpin) * .05f + .5f));

            // Undoing late returns to the gourd nothing hit, not a reversed swing.
            LabHangingTarget.AppliedImpact second;
            a.ApplyPredictedImpact(predicted.Hitbox.bounds.center + offset, guess, out second);
            float swingAfterKick = SwingVelocity(a).magnitude;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            a.UndoPredictedImpact(second);
            Vector2 left = SwingVelocity(a) - SwingVelocity(b);
            Assert.That(left.magnitude, Is.LessThan(swingAfterKick * .1f + 1f), "Only the Host's motion is left after the undo.");
            Assert.That(quiet.SwingAngles.magnitude, Is.LessThan(.001f), "The untouched gourd is the reference for rest.");
        }

        private static Vector2 SwingVelocity(LabHangingTarget hanging)
            => (Vector2)typeof(LabHangingTarget).GetField("velocity", Private).GetValue(hanging);

        private LabTarget CreateMovingTarget(out LabHangingTarget hanging)
        {
            LabTarget target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            owned.Add(target.gameObject);
            hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureRopeMotion(.45f, 3f, .32f, 42f, 2f, .65f);
            hanging.ApplyRemoteImpact(new Vector3(2f, 0f, -4f), 300f);
            return target;
        }

        private static LabHangingTarget.RigState Pose(LabHangingTarget hanging, string field)
            => (LabHangingTarget.RigState)typeof(LabHangingTarget).GetField(field, Private).GetValue(hanging);

        private static void Field(LabHangingTarget hanging, string field, object value)
            => typeof(LabHangingTarget).GetField(field, Private).SetValue(hanging, value);

        private static void Invoke(LabHangingTarget hanging, string method, params object[] arguments)
            => typeof(LabHangingTarget).GetMethod(method, Private).Invoke(hanging, arguments);

        private static bool Draws(LabTarget target, LabHangingTarget.RigState pose)
        {
            Transform pivot = target.transform.Find("Suspension Pivot");
            Transform twist = pivot.Find("Body Twist Pivot");
            return Quaternion.Angle(pivot.localRotation, Quaternion.Euler(pose.Swing.x, 0f, pose.Swing.y)) < .01f
                && Quaternion.Angle(twist.localRotation, Quaternion.Euler(0f, pose.Yaw, 0f)) < .01f;
        }

        private static void AssertDrawn(LabTarget target, LabHangingTarget.RigState pose, string message)
        {
            Transform pivot = target.transform.Find("Suspension Pivot");
            Transform twist = pivot.Find("Body Twist Pivot");
            Assert.That(Quaternion.Angle(pivot.localRotation, Quaternion.Euler(pose.Swing.x, 0f, pose.Swing.y)), Is.LessThan(.01f), message);
            Assert.That(Quaternion.Angle(twist.localRotation, Quaternion.Euler(0f, pose.Yaw, 0f)), Is.LessThan(.01f), message);
            Assert.That(twist.localPosition.y, Is.EqualTo(-(pose.PivotHeight + pose.RopeOffset)).Within(1e-4f), message);
            Assert.That(target.Hitbox.transform.IsChildOf(twist), Is.True, "The hitbox stays in the hanging frame.");
        }
    }
}
