using System;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class ThrowMappingTests
    {
        private static ThrowTuning Tuning(float maxWorldSpeed = 30f) =>
            new ThrowTuning(.35f, 8f, 8f, 2.8f, 6f, maxWorldSpeed, 9.81f, .45f, 4f, .165f, .12f, .02f);
        private static ProjectileLaunchBasis Basis(Vector3? aim = null) =>
            new ProjectileLaunchBasis(new Vector3(0, 1.08f, -4.5f), Vector3.right, 3f, aim ?? new Vector3(0, 1.4f, 0));
        private static bool Calculate(Vector2 delta, float duration, out BallisticLaunch launch) =>
            ThrowMapping.TryCalculate(new Vector2(.5f, 1f), new OrbThrowInput(delta, duration), Basis(), Tuning(), out launch, out _);

        [Test]
        public void NominalSwipeSetsIndependentOriginVelocityAndGravity()
        {
            Assert.That(Calculate(new Vector2(0, .125f), .1f, out var launch), Is.True);
            Assert.That(launch.Position, Is.EqualTo(new Vector3(0, 1.08f, -4.5f)));
            Assert.That(Vector3.Distance(launch.InitialVelocity, new Vector3(0, 3.5f, 10)), Is.LessThan(.00001f));
            Assert.That(launch.GravityVector, Is.EqualTo(new Vector3(0, -9.81f, 0)));
            Assert.That(launch.Bounce, Is.EqualTo(.45f));
            Assert.That(launch.Radius, Is.EqualTo(.165f));
            Assert.That(launch.Lifetime, Is.EqualTo(4f));
        }

        [TestCase(-.1f, -6f)]
        [TestCase(.1f, 6f)]
        public void LateralGestureChangesDirectionWithoutSeekingTheTarget(float deltaX, float expectedX)
        {
            Assert.That(Calculate(new Vector2(deltaX, .125f), .1f, out var launch), Is.True);
            Assert.That(launch.InitialVelocity.x, Is.EqualTo(expectedX).Within(.00001f));
            Assert.That(launch.InitialVelocity.z, Is.EqualTo(10f).Within(.00001f));
        }

        [Test]
        public void ReleaseXOnlyChangesOriginAndAimPointHasNoInfluence()
        {
            var input = new OrbThrowInput(new Vector2(0, .125f), .1f);
            Assert.That(ThrowMapping.TryCalculate(new Vector2(0, 1), input, Basis(), Tuning(), out var left, out _), Is.True);
            Assert.That(ThrowMapping.TryCalculate(new Vector2(1, 0), input,
                Basis(new Vector3(-100, 200, 300)), Tuning(), out var right, out _), Is.True);
            Assert.That(right.Position.x - left.Position.x, Is.EqualTo(3f));
            Assert.That(right.InitialVelocity, Is.EqualTo(left.InitialVelocity));
            Assert.That(right.InitialVelocity.x, Is.Zero, "Off-center launches are not corrected toward the dummy.");
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, -.1f)]
        [TestCase(.1f, 0f)]
        [TestCase(0f, .034f)]
        public void StationaryDownwardSidewaysAndInsufficientUpwardSwipesReject(float x, float y)
        { Assert.That(Calculate(new Vector2(x, y), .1f, out _), Is.False); }

        [Test]
        public void PlausibleFastGestureClampsInputBeforeClampingWorldSpeed()
        {
            Assert.That(ThrowMapping.TryCalculate(new Vector2(.5f, 1), new OrbThrowInput(new Vector2(.2f, .4f), .02f),
                Basis(), Tuning(12f), out var launch, out _), Is.True);
            Assert.That(launch.InitialVelocity.magnitude, Is.EqualTo(12f).Within(.00001f));
            Assert.That(launch.InitialVelocity.x / launch.InitialVelocity.z, Is.EqualTo(.375f).Within(.00001f));
            Assert.That(Calculate(new Vector2(0, .97f), .12f, out _), Is.False, "Displacement beyond the full window's input envelope is malformed.");
        }

        [TestCase(0f)]
        [TestCase(-.1f)]
        [TestCase(.01f)]
        [TestCase(.121f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDurationRejectsWithoutThrowing(float duration)
        { Assert.That(Calculate(new Vector2(0, .125f), duration, out _), Is.False); }

        [TestCase(float.NaN, .1f)]
        [TestCase(.1f, float.PositiveInfinity)]
        [TestCase(float.MaxValue, float.MaxValue)]
        public void NonfiniteOrOverflowSizedDeltasReject(float x, float y)
        { Assert.That(Calculate(new Vector2(x, y), .1f, out _), Is.False); }

        [Test]
        public void InvalidReleaseDefaultTuningAndNonWorldRightBasisReject()
        {
            var input = new OrbThrowInput(new Vector2(0, .125f), .1f);
            Assert.That(ThrowMapping.TryCalculate(new Vector2(-.01f, 1), input, Basis(), Tuning(), out _, out _), Is.False);
            Assert.That(ThrowMapping.TryCalculate(new Vector2(.5f, float.NaN), input, Basis(), Tuning(), out _, out _), Is.False);
            Assert.That(ThrowMapping.TryCalculate(Vector2.one, input, Basis(), default, out _, out _), Is.False);
            Assert.That(ThrowMapping.TryCalculate(Vector2.one, input, default, Tuning(), out _, out _), Is.False);
            var vertical = new ProjectileLaunchBasis(Vector3.zero, Vector3.up, 3, Vector3.forward);
            Assert.That(ThrowMapping.TryCalculate(Vector2.one, input, vertical, Tuning(), out _, out _), Is.False);
        }

        [Test]
        public void InvalidBallisticLaunchCannotBypassFinitePhysicsParameters()
        {
            Assert.Throws<ArgumentException>(() => new BallisticLaunch(Vector3.zero, Vector3.zero, Vector3.down, .5f, 4, .1f));
            Assert.Throws<ArgumentException>(() => new BallisticLaunch(Vector3.zero, Vector3.forward, Vector3.up, .5f, 4, .1f));
            Assert.Throws<ArgumentException>(() => new BallisticLaunch(Vector3.zero, Vector3.forward, Vector3.down, 1.1f, 4, .1f));
            Assert.Throws<ArgumentException>(() => new BallisticLaunch(Vector3.zero, Vector3.forward, Vector3.down, .5f, float.NaN, .1f));
        }
        [Test]
        public void P2ThrowUsesOppositePositionAndDirection()
        {
            ProjectileLaunchBasis p2Basis =
                ParticipantLaunchFrame.Calculate(
                    Basis(),
                    2,
                    2
                );

            var input = new OrbThrowInput(
                new Vector2(0.1f, 0.125f),
                0.1f
            );

            bool succeeded = ThrowMapping.TryCalculate(
                new Vector2(0.5f, 1f),
                input,
                p2Basis,
                Tuning(),
                out BallisticLaunch launch,
                out string error
            );

            Assert.That(
                succeeded,
                Is.True,
                error
            );

            Assert.That(
                Vector3.Distance(
                    launch.Position,
                    new Vector3(0f, 1.08f, 4.5f)
                ),
                Is.LessThan(0.0001f)
            );

            Assert.That(
                Vector3.Distance(
                    launch.InitialVelocity,
                    new Vector3(-6f, 3.5f, -10f)
                ),
                Is.LessThan(0.0001f)
            );
        }
    }
}
