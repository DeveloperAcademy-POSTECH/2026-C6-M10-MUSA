using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>콤보 SMALL/BIG(#222)으로 던진 공이 작아지거나 커지면 3D 원소 꼬리도 같은 배율로 따라간다 (#343).</summary>
    public sealed class LabProjectileTrailScaleTests
    {
        private LabElementTrailProfile profile;

        [TearDown]
        public void TearDown()
        {
            if (profile != null) Object.DestroyImmediate(profile);
        }

        [Test]
        public void TheTrailFollowsTheOrbSize()
        {
            profile = ScriptableObject.CreateInstance<LabElementTrailProfile>();
            Assert.That(LabProjectile.ScaledTrailProfile(profile, 1f), Is.SameAs(profile), "Normal size keeps the shared profile.");

            LabElementTrailProfile small = LabProjectile.ScaledTrailProfile(profile, .6f);
            Assert.That(small, Is.Not.SameAs(profile), "The shared profile is never edited.");
            Assert.That(small.Width, Is.EqualTo(profile.Width * .6f).Within(.0001f));
            Assert.That(small.ParticleSize, Is.EqualTo(profile.ParticleSize * .6f).Within(.0001f));
            Assert.That(small.MaxLength, Is.EqualTo(profile.MaxLength * .6f).Within(.0001f));
            Assert.That(LabProjectile.ScaledTrailProfile(profile, .6f), Is.SameAs(small), "One copy per size.");

            LabElementTrailProfile big = LabProjectile.ScaledTrailProfile(profile, 1.4f);
            Assert.That(big.Width, Is.EqualTo(profile.Width * 1.4f).Within(.0001f));
        }
    }
}
