using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// #507: one continuous rope from the fixed anchor above the pivot to the gourd. Presentation only: the
    /// original ropes keep their transforms, the swing and hitbox are unchanged, and the default stays Segmented.
    /// </summary>
    public sealed class LabContinuousRopeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Extension = 4f;
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
        public IEnumerator SegmentedStaysTheDefaultWithTheOriginalTwoRopes()
        {
            LabTarget target = Create(out LabHangingTarget hanging);
            yield return new WaitForFixedUpdate();
            Assert.That(hanging.RopeStyle, Is.EqualTo(LabRopeStyle.Segmented));
            Assert.That(hanging.ContinuousRope, Is.Null, "No continuous rope is built for the default style.");
            Assert.That(Rope(target).GetComponent<Renderer>().enabled, Is.True);
            Assert.That(Upper(target).GetComponent<Renderer>().enabled, Is.True);
            Assert.That(Upper(target).gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator ACurvedRopeJoinsTheAnchorAndTheGourdInEveryPose()
        {
            LabTarget target = Create(out LabHangingTarget hanging);
            hanging.SetRopeStyle(LabRopeStyle.Curved);
            Transform rope = Rope(target), upper = Upper(target);
            Assert.That(rope.GetComponent<Renderer>().enabled, Is.False, "The original lower rope is hidden.");
            Assert.That(upper.GetComponent<Renderer>().enabled, Is.False, "The original upper rope is hidden.");
            Assert.That(hanging.ContinuousRope.parent, Is.SameAs(target.transform), "The rope hangs from the fixed root.");
            Assert.That(Segments(hanging), Has.Length.EqualTo(LabHangingTarget.CurvedRopeSegments));
            AssertJoined(target, hanging, "At rest");

            hanging.Kick(new Vector3(6f, 0f, -9f));
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            // Primitive colliders are removed with Destroy, which completes at the end of the creating frame.
            Assert.That(hanging.ContinuousRope.GetComponentsInChildren<Collider>(true), Is.Empty,
                "The visible rope must not add a projectile collider.");
            Assume.That(hanging.SwingAngles.magnitude, Is.GreaterThan(5f), "The test needs a real swing.");
            AssertJoined(target, hanging, "Fixed pose while swinging");
            AssertTangents(target, hanging);

            Invoke(hanging, "ApplyRenderPose", .5f);
            Field(hanging, "drawingInterpolated", true);
            AssertJoined(target, hanging, "Drawn (interpolated) pose");
            Invoke(hanging, "OnEndRendering", default(ScriptableRenderContext), null);
            AssertJoined(target, hanging, "Fixed pose restored after rendering");

            hanging.ResetSwing();
            AssertJoined(target, hanging, "After reset");
            Assert.That(Vector3.Distance(rope.TransformPoint(Vector3.down), BodyTop(target)), Is.LessThan(.001f),
                "The hidden original rope keeps its transform, so its structure checks still hold.");
        }

        [UnityTest]
        public IEnumerator AStraightRopeIsOnePieceAndSwitchingBackRestoresTheOriginalRopes()
        {
            LabTarget target = Create(out LabHangingTarget hanging);
            hanging.SetRopeStyle(LabRopeStyle.Straight);
            hanging.Kick(new Vector3(-6f, 0f, -9f));
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Transform[] segments = Segments(hanging);
            Assert.That(segments, Has.Length.EqualTo(1));
            Assert.That(Vector3.Distance(Start(segments[0]), Anchor(target, hanging)), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(End(segments[0]), RopeEnd(target)), Is.LessThan(.01f));

            hanging.SetRopeStyle(LabRopeStyle.Segmented);
            Assert.That(hanging.ContinuousRope.gameObject.activeSelf, Is.False);
            Assert.That(Rope(target).GetComponent<Renderer>().enabled, Is.True);
            Assert.That(Upper(target).GetComponent<Renderer>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ARopeSocketInsideTheBodyIsWhereTheRopeEnds()
        {
            LabTarget target = Create(out LabHangingTarget hanging);
            hanging.SetRopeStyle(LabRopeStyle.Curved);
            var socket = new GameObject("Rope Socket").transform;
            socket.SetParent(target.VisualMount, false);
            socket.position = BodyTop(target) + new Vector3(.2f, .1f, 0f);
            hanging.SetRopeSocket(socket);
            hanging.ApplyRemoteImpact(new Vector3(2f, 0f, -4f), 300f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Transform[] segments = Segments(hanging);
            Assert.That(Vector3.Distance(End(segments[segments.Length - 1]), socket.position), Is.LessThan(.01f),
                "An off-axis socket turns and swings with the body.");
            Assert.That(Vector3.Distance(Start(segments[0]), Anchor(target, hanging)), Is.LessThan(.001f));
        }

        [Test]
        public void TheFinishCopyTakesTheContinuousRopeAndLeavesTheHiddenOriginalsOut()
        {
            LabTarget target = Create(out LabHangingTarget hanging);
            hanging.SetRopeStyle(LabRopeStyle.Curved);
            var finish = target.gameObject.AddComponent<LabPaintFinishPresentation>();
            var copy = new GameObject("Finish copy").transform;
            owned.Add(copy.gameObject);
            typeof(LabPaintFinishPresentation).GetMethod("CopyMeshes", Private).Invoke(finish, new object[] { target.transform, copy });
            MeshRenderer[] copied = copy.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(copied.Count(renderer => renderer.name.StartsWith("Rope Segment")),
                Is.EqualTo(LabHangingTarget.CurvedRopeSegments));
            Assert.That(copied.Any(renderer => renderer.name.StartsWith("Rope ·") || renderer.name.StartsWith("Upper Rope")), Is.False,
                "The disabled original ropes are not copied.");
        }

        private LabTarget Create(out LabHangingTarget hanging)
        {
            LabTarget target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            owned.Add(target.gameObject);
            hanging = target.gameObject.AddComponent<LabHangingTarget>();
            Field(hanging, "ropeLength", 1.5f);
            Field(hanging, "suspensionVisualExtension", Extension);
            hanging.ResetSwing();
            return target;
        }

        private static void AssertJoined(LabTarget target, LabHangingTarget hanging, string when)
        {
            Transform[] segments = Segments(hanging);
            Assert.That(Vector3.Distance(Start(segments[0]), Anchor(target, hanging)), Is.LessThan(.001f),
                when + ": the rope starts at the fixed anchor above the pivot.");
            Assert.That(Vector3.Distance(End(segments[segments.Length - 1]), RopeEnd(target)), Is.LessThan(.01f),
                when + ": the rope ends where the original lower rope meets the gourd.");
            for (int i = 1; i < segments.Length; i++)
                Assert.That(Vector3.Distance(End(segments[i - 1]), Start(segments[i])), Is.LessThan(.01f),
                    when + ": the pieces are joined.");
        }

        private static void AssertTangents(LabTarget target, LabHangingTarget hanging)
        {
            Transform[] segments = Segments(hanging);
            Vector3 first = End(segments[0]) - Start(segments[0]);
            Vector3 last = End(segments[segments.Length - 1]) - Start(segments[segments.Length - 1]);
            Vector3 gourdDown = target.transform.Find("Suspension Pivot").TransformDirection(Vector3.down);
            Assert.That(Vector3.Angle(first, target.transform.TransformDirection(Vector3.down)), Is.LessThan(3f),
                "Vertical where it leaves the fixed anchor.");
            Assert.That(Vector3.Angle(last, gourdDown), Is.LessThan(3f), "Along the gourd axis where it meets the gourd: no bend.");
        }

        private static Transform[] Segments(LabHangingTarget hanging)
            => (Transform[])typeof(LabHangingTarget).GetField("ropeSegments", Private).GetValue(hanging);
        // A cylinder runs from local y=-1 to y=+1; the pieces point from the anchor towards the gourd.
        private static Vector3 Start(Transform segment) => segment.position - segment.up * (segment.lossyScale.y - .004f);
        private static Vector3 End(Transform segment) => segment.position + segment.up * (segment.lossyScale.y - .004f);
        private static Transform Rope(LabTarget target) => target.transform.Find("Suspension Pivot/Rope");
        private static Transform Upper(LabTarget target) => target.transform.Find("Upper Rope");
        private static Vector3 RopeEnd(LabTarget target) => Rope(target).TransformPoint(Vector3.down);
        private static Vector3 Anchor(LabTarget target, LabHangingTarget hanging)
            => target.transform.TransformPoint(target.transform.Find("Suspension Pivot").localPosition + Vector3.up * Extension);

        private static Vector3 BodyTop(LabTarget target)
        {
            Bounds bounds = target.Hitbox.sharedMesh.bounds;
            return target.Hitbox.transform.TransformPoint(bounds.center + Vector3.up * bounds.extents.y);
        }

        private static void Field(object owner, string name, object value)
            => owner.GetType().GetField(name, Private).SetValue(owner, value);

        private static void Invoke(object owner, string name, params object[] arguments)
            => owner.GetType().GetMethod(name, Private).Invoke(owner, arguments);
    }
}
