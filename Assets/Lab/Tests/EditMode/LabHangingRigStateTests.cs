using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>The drawn gourd pose between two fixed steps (#454).</summary>
    public sealed class LabHangingRigStateTests
    {
        private static readonly LabHangingTarget.RigState From = new LabHangingTarget.RigState
        { Swing = new Vector2(-4f, 10f), Yaw = 350f, RopeOffset = .1f, PivotHeight = 2f };
        private static readonly LabHangingTarget.RigState To = new LabHangingTarget.RigState
        { Swing = new Vector2(6f, 20f), Yaw = 370f, RopeOffset = .3f, PivotHeight = 2.5f };

        [Test]
        public void TheMidpointIsHalfWayAndYawStaysUnwrapped()
        {
            LabHangingTarget.RigState mid = LabHangingTarget.RigState.Lerp(From, To, .5f);
            Assert.That(mid.Swing.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(mid.Swing.y, Is.EqualTo(15f).Within(1e-5f));
            Assert.That(mid.Yaw, Is.EqualTo(360f).Within(1e-4f), "350 to 370 passes 360, it does not go back through 0.");
            Assert.That(mid.RopeOffset, Is.EqualTo(.2f).Within(1e-6f));
            Assert.That(mid.PivotHeight, Is.EqualTo(2.5f), "The pivot height is the newer pose's, never blended.");
        }

        [TestCase(0f, 350f)]
        [TestCase(1f, 370f)]
        [TestCase(-3f, 350f)]
        [TestCase(7f, 370f)]
        [TestCase(float.NaN, 370f)]
        public void AlphaIsClampedToTheTwoFixedPoses(float alpha, float yaw)
        {
            Assert.That(LabHangingTarget.RigState.Lerp(From, To, alpha).Yaw, Is.EqualTo(yaw).Within(1e-4f));
        }
    }
}
