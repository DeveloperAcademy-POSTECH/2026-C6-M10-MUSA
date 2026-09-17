using C6.Prototype.Attack;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>Presentation math only. The trail never participates in Host contact, damage, or ownership.</summary>
    public sealed class ThrowFlightTrailTests
    {
        private static ProjectileWire Sample(float elapsed, bool ballistic = true)
        {
            var origin = new Vector3(0f, 1.08f, -4.5f);
            var launchVelocity = new Vector3(1f, 3.5f, 10f);
            var gravity = new Vector3(0f, -9.81f, 0f);
            return new ProjectileWire
            {
                id = "orb", owner = 1, radius = .165f, ballistic = ballistic, gravity = gravity, lifetime = 4f, elapsed = elapsed,
                velocity = launchVelocity + gravity * elapsed,
                position = origin + launchVelocity * elapsed + gravity * (.5f * elapsed * elapsed)
            };
        }

        [TestCase(.02f)]
        [TestCase(.1f)]
        [TestCase(.25f)]
        public void EarlySampleRewindsToTheReleasePoint(float elapsed)
        {
            Assert.That(ThrowFlightTrail.TryLaunchOrigin(Sample(elapsed), out var origin), Is.True);
            Assert.That(Vector3.Distance(origin, new Vector3(0f, 1.08f, -4.5f)), Is.LessThan(.0001f));
        }

        [Test]
        public void UnsafeSamplesAreNotRewound()
        {
            Assert.That(ThrowFlightTrail.TryLaunchOrigin(null, out _), Is.False);
            Assert.That(ThrowFlightTrail.TryLaunchOrigin(Sample(0f), out _), Is.False);
            Assert.That(ThrowFlightTrail.TryLaunchOrigin(Sample(.26f), out _), Is.False, "may already include a bounce");
            Assert.That(ThrowFlightTrail.TryLaunchOrigin(Sample(.1f, ballistic: false), out _), Is.False);
            Assert.That(ThrowFlightTrail.TryLaunchOrigin(Sample(float.NaN), out _), Is.False);
        }

        [Test]
        public void FadeRunsFromOpaqueToGoneOverTheDuration()
        {
            float duration = ThrowFlightTrail.FadeSeconds;
            Assert.That(ThrowFlightTrail.FadeAlpha(0f, duration), Is.EqualTo(1f));
            Assert.That(ThrowFlightTrail.FadeAlpha(duration * .5f, duration), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(ThrowFlightTrail.FadeAlpha(duration, duration), Is.Zero);
            Assert.That(ThrowFlightTrail.FadeAlpha(duration * 2f, duration), Is.Zero);
            Assert.That(ThrowFlightTrail.FadeAlpha(float.NaN, duration), Is.Zero);
            Assert.That(ThrowFlightTrail.FadeAlpha(.1f, 0f), Is.Zero);
        }
    }
}
