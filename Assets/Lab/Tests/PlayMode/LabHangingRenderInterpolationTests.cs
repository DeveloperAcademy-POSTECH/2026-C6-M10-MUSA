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
        private float timeScale;

        [SetUp]
        public void SetUp() { timeScale = Time.timeScale; Time.timeScale = 1f; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in owned) if (item != null) Object.Destroy(item);
            owned.Clear();
            yield return null;
            Time.timeScale = timeScale;
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

        [UnityTest]
        public IEnumerator OutsideRenderingTheTransformsAlwaysHoldTheFixedPose()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null; // After Update, before LateUpdate.
                AssertDrawn(target, Pose(hanging, "currentPose"), "Update readers (the Host's paint-flow sample).");
                yield return new WaitForEndOfFrame(); // After LateUpdate and rendering.
                AssertDrawn(target, Pose(hanging, "currentPose"), "End-of-frame readers.");
            }
        }

        [UnityTest]
        public IEnumerator DuringRealRenderingTheDrawnPoseLiesBetweenTheTwoFixedPoses()
        {
            LabTarget target = CreateMovingTarget(out LabHangingTarget hanging);
            var camera = new GameObject("Interpolation camera").AddComponent<Camera>();
            owned.Add(camera.gameObject);
            camera.transform.position = new Vector3(0f, 1f, -8f);
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
            // Subscribed after the target, so this runs once the drawn pose is in place.
            RenderPipelineManager.beginContextRendering += Observe;
            try
            {
                for (int frame = 0; frame < 40; frame++) yield return null;
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
