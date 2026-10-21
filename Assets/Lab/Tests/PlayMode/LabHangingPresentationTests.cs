using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace C6Lab.Tests
{
    public class LabHangingPresentationTests
    {
        [UnityTest]
        public IEnumerator ImpactMovesActualHitboxAndDampsBackToRest()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var swing = target.gameObject.AddComponent<LabHangingTarget>();
            Vector3 initial = target.Hitbox.transform.position;
            swing.Kick(new Vector3(6, 0, -9));
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(initial, target.Hitbox.transform.position), Is.GreaterThan(.1f));
            Assert.That(target.Hitbox.attachedRigidbody.isKinematic, Is.True);
            Assert.That(target.Hitbox.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(target.Hitbox.sharedMesh));
            float early = swing.SwingAngles.magnitude;
            for (int i = 0; i < 300; i++) yield return new WaitForFixedUpdate();
            Assert.That(swing.SwingAngles.magnitude, Is.LessThan(early * .2f));
            swing.ResetSwing();
            Assert.That(Vector3.Distance(initial, target.Hitbox.transform.position), Is.LessThan(.001f));
            Object.Destroy(target.gameObject);
        }
        [UnityTest]
        public IEnumerator InspectorTuningChangesSwingStrengthAndClampsItsAngle()
        {
            var weak = LabTarget.CreateCylinder(null, new Vector3(-4, 0, 0), .6f, 2.7f).gameObject;
            var strong = LabTarget.CreateCylinder(null, new Vector3(4, 0, 0), .6f, 2.7f).gameObject;
            var a = weak.AddComponent<LabHangingTarget>();
            var b = strong.AddComponent<LabHangingTarget>();
            Set(a, "impactStrength", .5f);
            Set(b, "impactStrength", 4f);
            Set(b, "maximumAngle", 6f);
            a.Kick(new Vector3(6, 0, -9));
            b.Kick(new Vector3(6, 0, -9));
            yield return new WaitForFixedUpdate();
            Assert.That(b.SwingAngles.magnitude, Is.GreaterThan(a.SwingAngles.magnitude * 3f));
            for (int i = 0; i < 70; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(b.SwingAngles.magnitude, Is.LessThanOrEqualTo(6.001f));
            }
            Object.Destroy(weak); Object.Destroy(strong);
        }

        [UnityTest]
        public IEnumerator FasterSideHitTiltsTheUndersideFurtherWithinSharedAngleLimit()
        {
            var slow = LabTarget.CreateCylinder(null, new Vector3(-4, 0, 0), .6f, 2.7f);
            var fast = LabTarget.CreateCylinder(null, new Vector3(4, 0, 0), .6f, 2.7f);
            var a = slow.gameObject.AddComponent<LabHangingTarget>();
            var b = fast.gameObject.AddComponent<LabHangingTarget>();
            a.ConfigureSwing(12f, 40f, 10f, 1.4f);
            b.ConfigureSwing(12f, 40f, 10f, 1.4f);
            a.Kick(Vector3.right * 5f);
            b.Kick(Vector3.right * 12.5f); // Saved config's maximum throw speed.
            float slowPeak = 0f, fastPeak = 0f, undersideHorizontal = 0f;
            for (int i = 0; i < 80; i++)
            {
                yield return new WaitForFixedUpdate();
                slowPeak = Mathf.Max(slowPeak, a.SwingAngles.magnitude);
                fastPeak = Mathf.Max(fastPeak, b.SwingAngles.magnitude);
                undersideHorizontal = Mathf.Max(undersideHorizontal,
                    Mathf.Abs(fast.Hitbox.transform.TransformDirection(Vector3.down).x));
                Assert.That(b.SwingAngles.magnitude, Is.LessThanOrEqualTo(40.001f));
            }
            Assert.That(fastPeak, Is.GreaterThan(slowPeak * 2f));
            Assert.That(fastPeak, Is.GreaterThan(30f));
            Assert.That(undersideHorizontal, Is.GreaterThan(.5f));
            Object.Destroy(slow.gameObject); Object.Destroy(fast.gameObject);
        }

        [UnityTest]
        public IEnumerator DampingAndReturnForceCanBeTunedIndependently()
        {
            var free = LabTarget.CreateCylinder(null, new Vector3(-4, 0, 0), .6f, 2.7f).gameObject;
            var damped = LabTarget.CreateCylinder(null, new Vector3(4, 0, 0), .6f, 2.7f).gameObject;
            var a = free.AddComponent<LabHangingTarget>();
            var b = damped.AddComponent<LabHangingTarget>();
            Set(a, "returnStrength", 0f); Set(b, "returnStrength", 0f);
            Set(a, "swingDamping", 0f); Set(b, "swingDamping", 10f);
            a.Kick(new Vector3(0, 0, -2)); b.Kick(new Vector3(0, 0, -2));
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            Assert.That(a.SwingAngles.magnitude, Is.GreaterThan(b.SwingAngles.magnitude * 3f));
            Set(a, "returnStrength", 40f); Set(a, "swingDamping", 5f);
            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate();
            Assert.That(a.SwingAngles.magnitude, Is.LessThan(.1f));
            Object.Destroy(free); Object.Destroy(damped);
        }

        [UnityTest]
        public IEnumerator RopeLengthUpdatesTheRigAndPreservesTheRestingAimPoint()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            Vector3 centre = target.Hitbox.transform.position;
            Bounds meshBounds = target.Hitbox.sharedMesh.bounds;
            Vector3 localTop = meshBounds.center + Vector3.up * meshBounds.extents.y;
            float topInMount = target.VisualMount.InverseTransformPoint(
                target.Hitbox.transform.TransformPoint(localTop)).y;
            Set(hanging, "ropeLength", 2f);
            Set(hanging, "suspensionVisualExtension", 4f);
            yield return new WaitForFixedUpdate();
            Transform pivot = target.transform.Find("Suspension Pivot");
            Transform rope = pivot.Find("Rope");
            Assert.That(rope.localScale.y * 2f, Is.EqualTo(2f).Within(.001f));
            Assert.That(pivot.localPosition.y, Is.EqualTo(hanging.RopeLength + topInMount).Within(.001f),
                "The suspension must use the actual mesh top, without shortening the collider.");
            Transform upper = target.transform.Find("Upper Rope");
            Assert.That(upper.gameObject.activeSelf, Is.True);
            Assert.That(upper.localScale.y * 2f, Is.EqualTo(4f).Within(.001f));
            Assert.That(Vector3.Distance(upper.TransformPoint(Vector3.down), pivot.position), Is.LessThan(.001f));
            Assert.That(upper.GetComponent<Collider>(), Is.Null,
                "The visible extension must not add a projectile collider.");
            Assert.That(Vector3.Distance(centre, target.Hitbox.transform.position), Is.LessThan(.001f));
            Vector3 ropeBottom = rope.TransformPoint(Vector3.down);
            Vector3 bodyTop = target.Hitbox.transform.TransformPoint(localTop);
            Assert.That(Vector3.Distance(ropeBottom, bodyTop), Is.LessThan(.001f));
            hanging.Kick(new Vector3(0, 0, -9));
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(rope.TransformPoint(Vector3.down),
                target.Hitbox.transform.TransformPoint(localTop)), Is.LessThan(.001f));
            hanging.ResetSwing();
            Assert.That(Vector3.Distance(centre, target.Hitbox.transform.position), Is.LessThan(.001f));
            Object.Destroy(target.gameObject);
        }

        [UnityTest]
        public IEnumerator ImpactBouncesUpAndDownWithTheRealHitboxAndThenSettles()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            Set(hanging, "impactStrength", 0f);
            Vector3 rest = target.Hitbox.transform.position;
            hanging.Kick(new Vector3(6, 0, -9));
            float largestDrop = 0f;
            bool roseAboveRest = false;
            for (int i = 0; i < 170; i++)
            {
                yield return new WaitForFixedUpdate();
                largestDrop = Mathf.Max(largestDrop, rest.y - target.Hitbox.transform.position.y);
                roseAboveRest |= target.Hitbox.transform.position.y > rest.y + .01f;
                Transform rope = target.transform.Find("Suspension Pivot/Rope");
                Assert.That(Vector3.Distance(rope.TransformPoint(Vector3.down),
                    target.Hitbox.transform.TransformPoint(Vector3.up)), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(target.VisualMount.position,
                    target.Hitbox.transform.position), Is.LessThan(.001f));
            }
            Assert.That(largestDrop, Is.GreaterThan(.1f));
            Assert.That(roseAboveRest, Is.True, "A spring should rebound above its resting length.");
            Assert.That(Mathf.Abs(hanging.VerticalOffset), Is.LessThan(.01f));
            hanging.ResetSwing();
            Assert.That(Vector3.Distance(rest, target.Hitbox.transform.position), Is.LessThan(.001f));
            Object.Destroy(target.gameObject);
        }

        [UnityTest]
        public IEnumerator RepeatedHitsRespectExtensionLimitsAndZeroDisablesBouncing()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            Set(hanging, "maximumExtension", .12f);
            Set(hanging, "bounceStrength", 2f);
            for (int i = 0; i < 50; i++)
            {
                hanging.Kick(new Vector3(6, 0, -9));
                yield return new WaitForFixedUpdate();
                Assert.That(hanging.VerticalOffset, Is.InRange(-.0601f, .1201f));
            }
            Set(hanging, "maximumExtension", 0f);
            yield return new WaitForFixedUpdate();
            Assert.That(hanging.VerticalOffset, Is.EqualTo(0f));
            Assert.That(target.transform.Find("Suspension Pivot/Rope").localScale.y * 2f,
                Is.EqualTo(hanging.RopeLength).Within(.001f));
            Object.Destroy(target.gameObject);
        }

        [UnityTest]
        public IEnumerator SharedRopeCycleMovesHitboxAboveAndBelowAimPointWithAttachedCord()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureRopeMotion(.5f, 1.6f, .4f, 42f, 2f, .65f);
            float restingY = target.Hitbox.transform.position.y;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(target.Hitbox.transform.position.y, Is.LessThan(restingY - .4f));
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            Assert.That(target.Hitbox.transform.position.y, Is.GreaterThan(restingY + .4f));
            Transform rope = target.transform.Find("Suspension Pivot/Rope");
            Assert.That(rope.localScale.y * 2f, Is.EqualTo(hanging.RopeLength + hanging.CurrentRopeOffset).Within(.001f));
            Assert.That(Vector3.Distance(rope.TransformPoint(Vector3.down),
                target.Hitbox.transform.TransformPoint(Vector3.up)), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(target.VisualMount.position,
                target.Hitbox.transform.position), Is.LessThan(.001f));
            hanging.ResetSwing();
            Assert.That(target.Hitbox.transform.position.y, Is.EqualTo(restingY).Within(.001f));
            Object.Destroy(target.gameObject);
        }

        [UnityTest]
        public IEnumerator SideHitsSpinInOppositeDirectionsWhileCentreHitDoesNot()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureSpin(80f, 1.2f, 720f);
            Vector3 centre = target.Hitbox.transform.position;
            Vector3 incoming = Vector3.forward * 12.5f;
            Assert.That(hanging.ApplyHostImpact(centre, incoming), Is.EqualTo(0f).Within(.001f));
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(0f).Within(.001f));

            float rightImpulse = hanging.ApplyHostImpact(centre + Vector3.right * .6f, incoming);
            Assert.That(rightImpulse, Is.LessThan(-100f));
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(rightImpulse).Within(.001f));
            hanging.ResetSwing();
            float leftImpulse = hanging.ApplyHostImpact(centre - Vector3.right * .6f, incoming);
            Assert.That(leftImpulse, Is.EqualTo(-rightImpulse).Within(.001f));
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(leftImpulse).Within(.001f));
            Assert.That(target.Hitbox.attachedRigidbody.isKinematic, Is.True);
            Assert.That(target.Hitbox.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(target.Hitbox.sharedMesh));
            Object.Destroy(target.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SideHitUsesTheTiltedLocalSpinAxis()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureSpin(80f, 1.2f, 720f);
            Transform swing = target.transform.Find("Suspension Pivot");
            Transform twist = swing.Find("Body Twist Pivot");
            swing.localRotation = Quaternion.Euler(35f, 0f, 0f);
            Vector3 hit = target.Hitbox.transform.position + twist.right * .6f;
            Vector3 incoming = Vector3.forward * 12.5f;
            Vector3 lever = Vector3.ProjectOnPlane(hit - target.Hitbox.transform.position, twist.up);
            float expected = Vector3.Dot(twist.up, Vector3.Cross(lever, incoming)) * 80f;
            Assert.That(hanging.ApplyHostImpact(hit, incoming), Is.EqualTo(expected).Within(.01f));
            Assert.That(Mathf.Abs(expected), Is.LessThan(600f),
                "The swing-tilted axis must alter the torque compared with world Y.");
            Object.Destroy(target.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TangentialHitMakesFullTurnThenDampsAndResetRestoresHitboxAndArt()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureSpin(80f, 1.2f, 720f);
            Vector3 centre = target.Hitbox.transform.position;
            Vector3 localBodyPosition = target.Hitbox.transform.localPosition;
            Quaternion localBodyRotation = target.Hitbox.transform.localRotation;
            Vector3 localBodyScale = target.Hitbox.transform.localScale;
            Vector3 incoming = Vector3.forward * 12.5f;
            float impulse = hanging.ApplyHostImpact(centre + Vector3.right * .6f, incoming);
            Assert.That(Mathf.Abs(impulse), Is.EqualTo(600f).Within(.01f));
            float initialSpeed = Mathf.Abs(hanging.YawSpeedDegreesPerSecond);
            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(hanging.YawAngleDegrees), Is.GreaterThan(360f));
            Assert.That(Mathf.Abs(hanging.YawSpeedDegreesPerSecond), Is.LessThan(initialSpeed * .1f));
            Transform twist = target.transform.Find("Suspension Pivot/Body Twist Pivot");
            Assert.That(target.Hitbox.transform.parent, Is.SameAs(target.VisualMount));
            Assert.That(target.VisualMount.parent, Is.SameAs(twist));
            Assert.That(target.transform.Find("Suspension Pivot/Rope").parent,
                Is.SameAs(twist.parent), "The rope must not rotate with the body.");

            hanging.ResetSwing();
            Assert.That(hanging.YawAngleDegrees, Is.EqualTo(0f));
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(0f));
            Assert.That(twist.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(Vector3.Distance(target.Hitbox.transform.position, centre), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(target.Hitbox.transform.localPosition, localBodyPosition), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(target.Hitbox.transform.localRotation, localBodyRotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(target.Hitbox.transform.localScale, localBodyScale), Is.LessThan(.0001f),
                "Spin/reset must preserve the shell-to-collider alignment and its original size.");
            Object.Destroy(target.gameObject);
        }

        [UnityTest]
        public IEnumerator RepeatedImpactsRespectSpeedLimitAndRemoteSnapshotConverges()
        {
            var host = LabTarget.CreateCylinder(null, new Vector3(-4, 0, 0), .6f, 2.7f);
            var client = LabTarget.CreateCylinder(null, new Vector3(4, 0, 0), .6f, 2.7f);
            var hostHanging = host.gameObject.AddComponent<LabHangingTarget>();
            var clientHanging = client.gameObject.AddComponent<LabHangingTarget>();
            hostHanging.ConfigureSpin(200f, .8f, 450f);
            clientHanging.ConfigureSpin(200f, .8f, 450f);
            Vector3 velocity = Vector3.forward * 20f;
            for (int i = 0; i < 6; i++)
            {
                float impulse = hostHanging.ApplyHostImpact(
                    host.Hitbox.transform.position + Vector3.right * .6f, velocity);
                clientHanging.ApplyRemoteImpact(velocity, impulse);
            }
            Assert.That(Mathf.Abs(hostHanging.YawSpeedDegreesPerSecond), Is.EqualTo(450f).Within(.001f));
            Assert.That(clientHanging.YawSpeedDegreesPerSecond,
                Is.EqualTo(hostHanging.YawSpeedDegreesPerSecond).Within(.001f));
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            float expectedAngle = hostHanging.YawAngleDegrees;
            float expectedSpeed = hostHanging.YawSpeedDegreesPerSecond;
            clientHanging.ResetSwing(); // Simulate a lost impact before the next host snapshot.
            clientHanging.ReconcileYaw(expectedAngle, expectedSpeed);
            Assert.That(clientHanging.YawAngleDegrees, Is.EqualTo(expectedAngle).Within(.001f),
                "A large divergence must be corrected by the host snapshot.");
            Assert.That(clientHanging.YawSpeedDegreesPerSecond,
                Is.EqualTo(expectedSpeed).Within(.001f));
            Object.Destroy(host.gameObject);
            Object.Destroy(client.gameObject);
        }

        [UnityTest]
        public IEnumerator DelayedHostSnapshotDoesNotPullAHealthyFastSpinBackward()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureSpin(200f, 1.2f, 720f);
            hanging.ApplyHostImpact(target.Hitbox.transform.position + Vector3.right * .6f,
                Vector3.forward * 12.5f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            float angleBefore = hanging.YawAngleDegrees;
            float speedBefore = hanging.YawSpeedDegreesPerSecond;
            hanging.ReconcileYaw(angleBefore - speedBefore * .1f, speedBefore * 1.1f);
            Assert.That(hanging.YawAngleDegrees, Is.EqualTo(angleBefore).Within(.001f));
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(speedBefore).Within(.001f));
            Object.Destroy(target.gameObject);
        }

        [UnityTest]
        public IEnumerator RuntimeHitFlashPreservesSceneAssignedTexture()
        {
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var texture = new Texture2D(2, 2);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", texture);
            target.Hitbox.GetComponent<Renderer>().sharedMaterial = material;
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            var runtimeMaterial = target.Hitbox.GetComponent<Renderer>().sharedMaterial;
            Assert.That(runtimeMaterial, Is.Not.SameAs(material),
                "Impact flashes must not edit the saved material asset.");
            Assert.That(runtimeMaterial.GetTexture("_BaseMap"), Is.SameAs(texture));
            hanging.ApplyHostImpact(target.Hitbox.transform.position + Vector3.right * .6f,
                Vector3.forward * 12.5f);
            yield return null;
            Assert.That(runtimeMaterial.GetTexture("_BaseMap"), Is.SameAs(texture));
            Object.Destroy(target.gameObject);
            Object.Destroy(material);
            Object.Destroy(texture);
        }

        private static void Set(LabHangingTarget hanging, string name, float value)
        {
            typeof(LabHangingTarget).GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(hanging, value);
        }
    }
}
