using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabHitHapticsTests
    {
        private GameObject host;
        private LabHitHaptics haptics;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("haptics");
            haptics = host.AddComponent<LabHitHaptics>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void BaseDamageIsWeakAndBiggerItemDamageIsStrong()
        {
            haptics.PlayForDamage(20, 20);
            Assert.That(haptics.LastWasStrong, Is.False, "A normal 20-damage hit uses the weak haptic.");
            haptics.PlayForDamage(60, 20);
            Assert.That(haptics.LastWasStrong, Is.True, "Item damage above the base uses the strong haptic.");
            haptics.PlayForDamage(10, 20);
            Assert.That(haptics.LastWasStrong, Is.False);
            Assert.That(haptics.PlayCount, Is.EqualTo(3));
        }

        [Test]
        public void TurningHapticsOffPlaysNothing()
        {
            haptics.hapticsEnabled = false;
            haptics.PlayForDamage(60, 20);
            Assert.That(haptics.PlayCount, Is.Zero);
        }
    }
}
