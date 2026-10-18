using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabRopeReconciliationTests
    {
        private readonly List<GameObject> ownedObjects = new List<GameObject>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // A receiver can own the global pause. Destroy it before restoring the test clock.
            for (int i = ownedObjects.Count - 1; i >= 0; i--)
                if (ownedObjects[i] != null) Object.Destroy(ownedObjects[i]);
            ownedObjects.Clear();
            yield return null;
            Time.timeScale = originalTimeScale;
        }

        [UnityTest]
        public IEnumerator RepeatedPausedSnapshotsPinColliderArtAndRopeAfterTheFirstPacket()
        {
            LabTarget target = CreateTarget(out LabHangingTarget hanging);
            LabNetwork receiver = CreateReceiver(target);
            ApplySnapshot(receiver, Snapshot(false));
            hanging.Kick(new Vector3(0f, 0f, -4f));
            yield return new WaitForFixedUpdate();
            Assert.That(hanging.CycleElapsed, Is.LessThan(.2f));
            Assert.That(hanging.SwingAngles.magnitude, Is.GreaterThan(0f));
            AssertYaw(hanging, 0f, 0f);

            // A small yaw mismatch must resolve exactly on the FIRST paused packet,
            // even though the running policy would blend it by 35% per packet.
            LabSnapshot paused = Snapshot(true, .2f, .24f, 1.6f, 10f, 0f);
            ApplySnapshot(receiver, paused);
            Assert.That(receiver.IsBattlePaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            AssertState(hanging, .2f, .24f, 1.6f);
            AssertYaw(hanging, 10f, 0f);
            // Reproduce a fixed callback queued before NetworkEarlyUpdate pinned the pause.
            typeof(LabHangingTarget).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hanging, null);
            AssertState(hanging, .2f, .24f, 1.6f);
            AssertYaw(hanging, 10f, 0f);
            RigPose pinned = Capture(target);

            for (int i = 0; i < 12; i++)
            {
                yield return null; // Snapshot commits can continue while FixedUpdate is stopped.
                ApplySnapshot(receiver, paused);
                AssertState(hanging, .2f, .24f, 1.6f);
                AssertYaw(hanging, 10f, 0f);
                AssertPose(target, pinned); // Includes collider/art quaternion invariance.
            }
            AssertCordAttached(target);
        }

        [UnityTest]
        public IEnumerator ResumeContinuesThePinnedStateAndRetryRoundClearsMotion()
        {
            LabTarget target = CreateTarget(out LabHangingTarget hanging);
            LabNetwork receiver = CreateReceiver(target);
            Vector3 restingCentre = target.Hitbox.transform.position;
            AssertYaw(hanging, 0f, 0f);

            LabSnapshot stopped = Snapshot(true, .2f, .12f, .8f, 10f, 80f);
            ApplySnapshot(receiver, stopped);
            Assert.That(receiver.IsBattlePaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            AssertState(hanging, .2f, .12f, .8f);
            AssertYaw(hanging, 10f, 80f);
            typeof(LabHangingTarget).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hanging, null);
            AssertState(hanging, .2f, .12f, .8f);
            AssertYaw(hanging, 10f, 80f);
            RigPose paused = Capture(target);

            // Pausing must retain a valid Host angular velocity without integrating it.
            for (int i = 0; i < 12; i++)
            {
                yield return null;
                ApplySnapshot(receiver, stopped);
                AssertState(hanging, .2f, .12f, .8f);
                AssertYaw(hanging, 10f, 80f);
                AssertPose(target, paused);
            }

            ApplySnapshot(receiver, Snapshot(false, .2f, .12f, .8f, 10f, 80f));
            Assert.That(receiver.IsBattlePaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            AssertState(hanging, .2f, .12f, .8f);
            AssertYaw(hanging, 10f, 80f);
            AssertPose(target, paused);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(hanging.CycleElapsed, Is.GreaterThan(.2f));
            Assert.That(hanging.YawAngleDegrees, Is.GreaterThan(10f));
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.GreaterThan(0f));
            Assert.That(Quaternion.Angle(target.Hitbox.transform.rotation, paused.bodyRotation),
                Is.GreaterThan(.01f));
            Assert.That(Vector3.Distance(target.Hitbox.transform.position, paused.bodyPosition),
                Is.GreaterThan(.001f));
            AssertCordAttached(target);

            hanging.ApplyRemoteImpact(new Vector3(3f, 0f, -4f), 120f);
            // Retry is delivered as a new round and uses the receiver's ResetSwing boundary.
            LabSnapshot retry = Snapshot(false);
            retry.round++;
            ApplySnapshot(receiver, retry);
            AssertState(hanging, 0f, 0f, 0f);
            Assert.That(hanging.SwingAngles, Is.EqualTo(Vector2.zero));
            AssertYaw(hanging, 0f, 0f);
            Assert.That(hanging.CurrentRopeOffset, Is.EqualTo(0f));
            Assert.That(Vector3.Distance(target.Hitbox.transform.position, restingCentre), Is.LessThan(.00001f));
            AssertCordAttached(target);
        }

        [Test]
        public void UnpausedSnapshotsKeepBlendingSmallErrorsAndSnappingLargePhaseErrors()
        {
            CreateTarget(out LabHangingTarget hanging);
            hanging.ReconcileRope(.2f, .24f, 2f);
            AssertState(hanging, .05f, .12f, 1f);

            hanging.ReconcileRope(.07f, .16f, 1.25f);
            AssertState(hanging, .05f, .12f, 1f);
            hanging.ReconcileRope(1.1f, .12f, 1f);
            AssertState(hanging, 1.1f, .12f, 1f);
        }

        [Test]
        public void NonFiniteSnapshotsAreIgnoredAtomicallyForPausedAndRunningReceivers()
        {
            LabTarget target = CreateTarget(out LabHangingTarget hanging);
            hanging.ReconcileRope(.35f, .12f, .8f, true);
            RigPose expected = Capture(target);
            var invalidStates = new[]
            {
                new Vector3(float.NaN, .24f, 1.6f),
                new Vector3(float.PositiveInfinity, .24f, 1.6f),
                new Vector3(.2f, float.NaN, 1.6f),
                new Vector3(.2f, float.NegativeInfinity, 1.6f),
                new Vector3(.2f, .24f, float.NaN),
                new Vector3(.2f, .24f, float.PositiveInfinity)
            };
            foreach (bool paused in new[] { false, true })
                foreach (Vector3 invalid in invalidStates)
                {
                    hanging.ReconcileRope(invalid.x, invalid.y, invalid.z, paused);
                    AssertState(hanging, .35f, .12f, .8f);
                    AssertPose(target, expected);
                }
        }

        [Test]
        public void PausedSnapshotsWrapPhaseAndRetainTheExistingPhysicalExtensionLimits()
        {
            LabTarget target = CreateTarget(out LabHangingTarget hanging);
            hanging.ReconcileRope(hanging.CyclePeriod * 3f + .2f, 2f, 1f, true);
            AssertState(hanging, .2f, hanging.MaximumExtension, -.2f);
            AssertCordAttached(target);

            hanging.ReconcileRope(-.2f, -2f, -1f, true);
            float minimumOffset = -Mathf.Min(hanging.RopeLength * .45f, hanging.MaximumExtension * .5f);
            AssertState(hanging, hanging.CyclePeriod - .2f, minimumOffset, .2f);
            Assert.That(target.transform.Find("Suspension Pivot/Rope").localScale.y * 2f,
                Is.GreaterThanOrEqualTo(.09999f));
            AssertCordAttached(target);
        }

        private LabTarget CreateTarget(out LabHangingTarget hanging)
        {
            LabTarget target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            ownedObjects.Add(target.gameObject);
            hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureRopeMotion(.45f, 3f, .32f, 42f, 2f, .65f);
            return target;
        }

        private LabNetwork CreateReceiver(LabTarget target)
        {
            var receiverObject = new GameObject("Rope snapshot receiver");
            ownedObjects.Add(receiverObject);
            LabNetwork receiver = receiverObject.AddComponent<LabNetwork>();
            receiver.enabled = false; // Exercise snapshot commit without opening a transport or discovery.
            receiver.target = target;
            receiver.impactFeedback = null;
            return receiver;
        }

        private static LabSnapshot Snapshot(bool paused, float phase = 0f, float offset = 0f,
            float velocity = 0f, float yaw = 0f, float yawSpeed = 0f)
            => new LabSnapshot
            {
                sessionId = "rope-pause-regression", round = 1, phase = LabPhase.Playing, paused = paused,
                targetRopeCycleElapsed = phase, targetRopeHitOffset = offset, targetRopeHitVelocity = velocity,
                targetYawDegrees = yaw, targetYawSpeedDegreesPerSecond = yawSpeed
            };

        private static void AssertYaw(LabHangingTarget hanging, float yaw, float speed)
        {
            Assert.That(hanging.YawAngleDegrees, Is.EqualTo(yaw).Within(.00001f));
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(speed).Within(.00001f));
        }

        private static void ApplySnapshot(LabNetwork receiver, LabSnapshot snapshot)
        {
            // This is the commit boundary used after the paint map and network snapshot match.
            MethodInfo apply = typeof(LabNetwork).GetMethod("ApplyPaintSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            apply.Invoke(receiver, new object[] { snapshot });
        }

        private static void AssertState(LabHangingTarget hanging, float phase, float offset, float velocity)
        {
            Assert.That(hanging.CycleElapsed, Is.EqualTo(phase).Within(.00001f));
            Assert.That(hanging.VerticalOffset, Is.EqualTo(offset).Within(.00001f));
            Assert.That(hanging.VerticalVelocity, Is.EqualTo(velocity).Within(.00001f));
        }

        private static void AssertCordAttached(LabTarget target)
        {
            Transform rope = target.transform.Find("Suspension Pivot/Rope");
            Assert.That(Vector3.Distance(rope.TransformPoint(Vector3.down),
                target.Hitbox.transform.TransformPoint(Vector3.up)), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(target.VisualMount.position, target.Hitbox.transform.position),
                Is.LessThan(.00001f));
        }

        private static RigPose Capture(LabTarget target)
        {
            Physics.SyncTransforms();
            Transform rope = target.transform.Find("Suspension Pivot/Rope");
            return new RigPose
            {
                bodyPosition = target.Hitbox.transform.position,
                bodyRotation = target.Hitbox.transform.rotation,
                colliderCentre = target.Hitbox.bounds.center,
                colliderSize = target.Hitbox.bounds.size,
                artPosition = target.VisualMount.position,
                artRotation = target.VisualMount.rotation,
                ropePosition = rope.position, ropeRotation = rope.rotation, ropeScale = rope.localScale
            };
        }

        private static void AssertPose(LabTarget target, RigPose expected)
        {
            RigPose actual = Capture(target);
            Assert.That(Vector3.Distance(actual.bodyPosition, expected.bodyPosition), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(actual.bodyRotation, expected.bodyRotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(actual.colliderCentre, expected.colliderCentre), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(actual.colliderSize, expected.colliderSize), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(actual.artPosition, expected.artPosition), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(actual.artRotation, expected.artRotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(actual.ropePosition, expected.ropePosition), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(actual.ropeRotation, expected.ropeRotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(actual.ropeScale, expected.ropeScale), Is.LessThan(.00001f));
        }

        private struct RigPose
        {
            public Vector3 bodyPosition, colliderCentre, colliderSize, artPosition, ropePosition, ropeScale;
            public Quaternion bodyRotation, artRotation, ropeRotation;
        }
    }
}
