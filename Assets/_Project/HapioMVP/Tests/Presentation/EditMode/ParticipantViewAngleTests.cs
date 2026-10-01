using System;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Presentation.Tests
{
    public sealed class ParticipantViewAngleTests
    {
        [TestCase(1, 5, 0f)]
        [TestCase(2, 5, 72f)]
        [TestCase(3, 5, 144f)]
        [TestCase(4, 5, 216f)]
        [TestCase(5, 5, 288f)]
        [TestCase(2, 2, 180f)]
        [TestCase(3, 3, 240f)]
        public void CalculateYawReturnsExpectedAngle(
            int playerNumber,
            int participantCount,
            float expectedYaw)
        {
            float actualYaw = ParticipantViewAngle.CalculateYaw(
                playerNumber,
                participantCount
            );

            Assert.That(
                actualYaw,
                Is.EqualTo(expectedYaw).Within(0.0001f)
            );
        }

        [TestCase(1)]
        [TestCase(6)]
        public void CalculateYawRejectsInvalidParticipantCount(
            int participantCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ParticipantViewAngle.CalculateYaw(1, participantCount)
            );
        }

        [TestCase(0, 3)]
        [TestCase(4, 3)]
        public void CalculateYawRejectsInvalidPlayerNumber(
            int playerNumber,
            int participantCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ParticipantViewAngle.CalculateYaw(
                    playerNumber,
                    participantCount
                )
            );
        }
    }

    public sealed class SplitScreenLayoutProjectionTests
    {
        [Test]
        public void PositiveGrabOffsetBeyondRightEdgeProjectsOnlyThroughUnclampedDragPath()
        {
            var config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            var root = new GameObject("Projection test layout");
            var upper = new GameObject("Upper camera");
            var lower = new GameObject("Lower camera");
            RenderTexture target = null;
            try
            {
                upper.transform.SetParent(root.transform, false);
                lower.transform.SetParent(root.transform, false);
                var upperCamera = upper.AddComponent<Camera>();
                var lowerCamera = lower.AddComponent<Camera>();
                lowerCamera.transform.position = new Vector3(0f, 0f, -10f);
                lowerCamera.orthographic = true;
                lowerCamera.orthographicSize = 5f;
                target = new RenderTexture(400, 300, 0);
                lowerCamera.targetTexture = target;

                var layout = root.AddComponent<SplitScreenLayout>();
                layout.Configure(config, upperCamera, lowerCamera);
                Rect rect = lowerCamera.pixelRect;
                Assert.That(rect.width, Is.GreaterThan(40f));
                Assert.That(rect.height, Is.GreaterThan(0f));

                Vector2 finger = new Vector2(rect.xMax - 2f, rect.center.y);
                Vector2 grabbedCenter = finger + new Vector2(20f, 0f);
                Assert.That(layout.ContainsBottomScreenPoint(finger), Is.True);
                Assert.That(layout.TryScreenToOrbPlane(grabbedCenter, out _), Is.False,
                    "Ordinary hit tests must reject a target beyond the camera edge.");
                Assert.That(layout.TryScreenToOrbPlaneUnclamped(grabbedCenter, out var projected), Is.True);
                Assert.That(layout.TryScreenToOrbPlane(new Vector2(rect.xMax - 1f, rect.center.y),
                    out var inside), Is.True);
                Assert.That(projected.x, Is.GreaterThan(inside.x),
                    "The drag sample must retain outward travel despite the grab offset.");
                Assert.That(layout.TryScreenToOrbPlaneUnclamped(
                    new Vector2(float.NaN, rect.center.y), out _), Is.False);
            }
            finally
            {
                lower.GetComponent<Camera>().targetTexture = null;
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
