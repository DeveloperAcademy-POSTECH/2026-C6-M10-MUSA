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
            Set(hanging, "ropeLength", 2f);
            yield return new WaitForFixedUpdate();
            Transform pivot = target.transform.Find("Suspension Pivot");
            Transform rope = pivot.Find("Rope");
            Assert.That(rope.localScale.y * 2f, Is.EqualTo(2f).Within(.001f));
            Assert.That(pivot.localPosition.y, Is.EqualTo(2.9f).Within(.001f));
            Assert.That(Vector3.Distance(centre, target.Hitbox.transform.position), Is.LessThan(.001f));
            Vector3 ropeBottom = rope.TransformPoint(Vector3.down);
            Vector3 bodyTop = target.Hitbox.transform.TransformPoint(Vector3.up);
            Assert.That(Vector3.Distance(ropeBottom, bodyTop), Is.LessThan(.001f));
            hanging.Kick(new Vector3(0, 0, -9));
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(rope.TransformPoint(Vector3.down),
                target.Hitbox.transform.TransformPoint(Vector3.up)), Is.LessThan(.001f));
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

        private static void Set(LabHangingTarget hanging, string name, float value)
        {
            typeof(LabHangingTarget).GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(hanging, value);
        }
    }
}
