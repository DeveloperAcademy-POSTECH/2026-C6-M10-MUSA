using System;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabStraightThrowMathTests
    {
        private LabConfig config;
        private static readonly Vector3 Center = new Vector3(2f, 1.35f, -1f);

        [SetUp]
        public void SetUp() => config = ScriptableObject.CreateInstance<LabConfig>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [TestCase(0f, 1.5f, 0f, 3.15f, -6.75f)]
        [TestCase(1f, 1.5f, -2.5f, 3.15f, -6.75f)]
        [TestCase(-.8f, 3f, 2f, 6.3f, -13.5f)]
        public void ArcRetainsTheExistingLinearGoldenShots(float x, float y,
            float expectedX, float expectedY, float expectedZ)
        {
            float[] values = config.CaptureDeveloperValues();
            values[28] = 2f;
            values[29] = 2.5f;
            values[30] = 2.1f;
            values[31] = 4.5f;
            values[32] = 18f;
            values[36] = 1f;
            values[37] = 0f;
            values[112] = (float)LabThrowForm.Arc;
            Apply(values);

            Vector3 actual = LabThrowMath.LaunchVelocity(new Vector2(x, y), Anchor(0, 3), Center, config);
            AssertVector(actual, new Vector3(expectedX, expectedY, expectedZ));
        }

        [TestCase(-.5f, 1f)]
        [TestCase(0f, 1.5f)]
        [TestCase(.9f, 4f)]
        [TestCase(2f, 6f)]
        public void StraightKeepsTheSameSwipePowerAndHorizontalAimAfterTheSpeedCap(float x, float y)
        {
            float[] values = config.CaptureDeveloperValues();
            values[32] = 12.5f;
            values[36] = 1.2f;
            Apply(values);
            Vector2 swipe = new Vector2(x, y);
            for (int seats = 2; seats <= 5; seats++)
            for (int seat = 0; seat < seats; seat++)
            {
                Vector3 anchor = Anchor(seat, seats);
                SetForm(LabThrowForm.Arc);
                Vector3 arc = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
                SetForm(LabThrowForm.Straight);
                Vector3 straight = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
                Assert.That(straight.magnitude, Is.EqualTo(arc.magnitude).Within(.00001f));
                Assert.That(straight.magnitude, Is.LessThanOrEqualTo(config.ThrowMaxWorldSpeed + .00001f));
                AssertVector(Vector3.ProjectOnPlane(straight, Vector3.up).normalized,
                    Vector3.ProjectOnPlane(arc, Vector3.up).normalized);
            }
        }

        [Test]
        public void DefaultStraightCandidateIsLowerWithoutChangingItsInitialSpeed()
        {
            Vector3 anchor = Anchor(0, 3);
            Vector2 swipe = new Vector2(0f, 1.5f);
            Vector3 arc = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
            SetForm(LabThrowForm.Straight);
            Vector3 straight = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
            Assert.That(straight.magnitude, Is.EqualTo(arc.magnitude).Within(.00001f));
            Assert.That(Pitch(straight), Is.InRange(3f, 10f));
            Assert.That(Pitch(straight), Is.LessThan(Pitch(arc)));
        }

        [Test]
        public void ZeroLoftCanProduceAnExactlyLevelStraightShot()
        {
            float[] values = config.CaptureDeveloperValues();
            values[112] = (float)LabThrowForm.Straight;
            values[116] = 0f;
            values[117] = 0f;
            Apply(values);
            Vector3 shot = LabThrowMath.LaunchVelocity(new Vector2(.6f, 2f), Anchor(1, 3), Center, config);
            Assert.That(shot.y, Is.EqualTo(0f).Within(.00001f));
            Assert.That(shot.magnitude, Is.GreaterThan(0f));
        }

        [Test]
        public void PitchIsIndependentFromSwipePowerAndLoftCanRestoreTheOldElevation()
        {
            Vector3 anchor = Anchor(0, 3);
            Vector2 swipe = new Vector2(.7f, 2f);
            Vector3 arc = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
            float[] values = config.CaptureDeveloperValues();
            values[112] = (float)LabThrowForm.Straight;
            values[116] = 12f;
            values[117] = 0f;
            Apply(values);
            Vector3 tilted = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
            Assert.That(Pitch(tilted), Is.EqualTo(12f).Within(.0001f));
            Assert.That(tilted.magnitude, Is.EqualTo(arc.magnitude).Within(.00001f));

            values[116] = 0f;
            values[117] = 1f;
            Apply(values);
            AssertVector(LabThrowMath.LaunchVelocity(swipe, anchor, Center, config), arc);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void OriginOffsetsUseEachOwnersRightUpAndForwardWithoutChangingReleaseX(int seats)
        {
            for (int seat = 0; seat < seats; seat++)
            {
                SetForm(LabThrowForm.Arc);
                Vector3 anchor = Anchor(seat, seats);
                Vector3 arcLeft = LabThrowMath.LaunchPosition(seat, seats, Center, 0f, config);
                Vector3 arcRight = LabThrowMath.LaunchPosition(seat, seats, Center, 1f, config);
                Vector3 forward = Vector3.ProjectOnPlane(Center - anchor, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                float[] values = config.CaptureDeveloperValues();
                values[112] = (float)LabThrowForm.Straight;
                values[113] = .4f;
                values[114] = .2f;
                values[115] = .7f;
                Apply(values);
                Vector3 straightLeft = LabThrowMath.LaunchPosition(seat, seats, Center, 0f, config);
                Vector3 straightRight = LabThrowMath.LaunchPosition(seat, seats, Center, 1f, config);
                Vector3 delta = straightLeft - arcLeft;
                Assert.That(Vector3.Dot(delta, right), Is.EqualTo(.4f).Within(.00001f));
                Assert.That(delta.y, Is.EqualTo(.2f).Within(.00001f));
                Assert.That(Vector3.Dot(delta, forward), Is.EqualTo(.7f).Within(.00001f));
                AssertVector(straightRight - straightLeft, arcRight - arcLeft);
            }
        }

        [Test]
        public void AShiftedReleaseChangesTheOriginButNeverBendsTheVelocityTowardTheTarget()
        {
            SetForm(LabThrowForm.Straight);
            Vector3 anchor = Anchor(0, 3);
            Vector2 swipe = new Vector2(0f, 1.5f);
            Vector3 leftPosition = LabThrowMath.LaunchPosition(0, 3, Center, 0f, config);
            Vector3 rightPosition = LabThrowMath.LaunchPosition(0, 3, Center, 1f, config);
            Assert.That(Vector3.Distance(leftPosition, rightPosition), Is.GreaterThan(1f));
            Vector3 velocity = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
            Assert.That(velocity.x, Is.EqualTo(0f).Within(.00001f));
            float[] values = config.CaptureDeveloperValues();
            values[113] = -1.5f;
            values[114] = 1.5f;
            values[115] = -.5f;
            Apply(values);
            AssertVector(LabThrowMath.LaunchVelocity(swipe, anchor, Center, config), velocity);
        }

        [Test]
        public void ArcIgnoresTheStraightOnlyControls()
        {
            Vector3 anchor = Anchor(2, 5);
            Vector2 swipe = new Vector2(-.7f, 2.5f);
            Vector3 oldPosition = LabThrowMath.LaunchPosition(2, 5, Center, .25f, config);
            Vector3 oldVelocity = LabThrowMath.LaunchVelocity(swipe, anchor, Center, config);
            float[] values = config.CaptureDeveloperValues();
            values[113] = 1.5f;
            values[114] = -1.5f;
            values[115] = .5f;
            values[116] = -25f;
            values[117] = 1f;
            values[118] = 0f;
            Apply(values);
            Assert.That(LabThrowMath.LaunchPosition(2, 5, Center, .25f, config), Is.EqualTo(oldPosition));
            Assert.That(LabThrowMath.LaunchVelocity(swipe, anchor, Center, config), Is.EqualTo(oldVelocity));
            Assert.That(config.EffectiveThrowGravity, Is.EqualTo(config.ThrowGravity));
        }

        [Test]
        public void FormSelectionDoesNotChangeTheIntentionalUpwardGestureGateOrCameraSettings()
        {
            Vector2[] inputs = { new Vector2(0f, 1.5f), new Vector2(0f, .3f),
                new Vector2(2f, .9f), new Vector2(float.NaN, 1f) };
            bool[] original = new bool[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
                original[i] = LabThrowMath.IsThrowGesture(inputs[i], .2f, config);
            float radius = config.CameraRadius;
            float height = config.CameraHeightOffset;
            float look = config.CameraLookHeightOffset;
            SetForm(LabThrowForm.Straight);
            for (int i = 0; i < inputs.Length; i++)
                Assert.That(LabThrowMath.IsThrowGesture(inputs[i], .2f, config), Is.EqualTo(original[i]));
            Assert.That(config.CameraRadius, Is.EqualTo(radius));
            Assert.That(config.CameraHeightOffset, Is.EqualTo(height));
            Assert.That(config.CameraLookHeightOffset, Is.EqualTo(look));
        }

        [TestCase(-30f, 0f)]
        [TestCase(-30f, 1f)]
        [TestCase(30f, 0f)]
        [TestCase(30f, 1f)]
        public void ExtremeElevationControlsStayFiniteAndPointIntoTheArena(float pitch, float loft)
        {
            float[] values = config.CaptureDeveloperValues();
            values[112] = (float)LabThrowForm.Straight;
            values[116] = pitch;
            values[117] = loft;
            Apply(values);
            for (int seats = 2; seats <= 5; seats++)
            for (int seat = 0; seat < seats; seat++)
            {
                Vector3 anchor = Anchor(seat, seats);
                Vector3 shot = LabThrowMath.LaunchVelocity(new Vector2(.1f, .8f), anchor, Center, config);
                Assert.That(float.IsNaN(shot.sqrMagnitude) || float.IsInfinity(shot.sqrMagnitude), Is.False);
                Assert.That(Pitch(shot), Is.InRange(-30.0001f, 60.0001f));
                Assert.That(Vector3.Dot(shot, Vector3.ProjectOnPlane(Center - anchor, Vector3.up)), Is.GreaterThan(0f));
            }
        }

        [Test]
        public void StraightRetainsReleasePositionValidation()
        {
            SetForm(LabThrowForm.Straight);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LabThrowMath.LaunchPosition(0, 3, Center, float.NaN, config));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LabThrowMath.LaunchPosition(0, 3, Center, -1f, config));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LabThrowMath.LaunchPosition(0, 3, Center, 1.01f, config));
        }

        private Vector3 Anchor(int seat, int seats) => LabThrowMath.SeatPosition(seat, seats,
            Center, config.ThrowOriginRadius, config.ThrowOriginHeightOffset);

        private void SetForm(LabThrowForm form)
        {
            float[] values = config.CaptureDeveloperValues();
            values[112] = (float)form;
            Apply(values);
        }

        private void Apply(float[] values) =>
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

        private static float Pitch(Vector3 velocity) => Mathf.Atan2(velocity.y,
            Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude) * Mathf.Rad2Deg;

        private static void AssertVector(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.00002f));
    }
}
