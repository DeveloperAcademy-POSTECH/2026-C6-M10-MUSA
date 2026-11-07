using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>#509: B's special Water looks deeper and shimmers, while talisman Water keeps the plain drop.</summary>
    public sealed class LabJigwiSpecialWaterArtTests
    {
        private GameObject root;
        [SetUp] public void Create() => root = new GameObject("special-water-art");
        [TearDown] public void Destroy() => Object.DestroyImmediate(root);

        [Test]
        public void SpecialWaterAddsATintGlowAndThreeSparklesWithoutColliders()
        {
            var art = LabJigwiSupplyWaterArt.Create(root.transform);
            art.Show(.3f, false, null, specialWater: true);
            Assert.That(art.Special, Is.True);
            Assert.That(art.VisibleSparkles, Is.EqualTo(3));
            Assert.That(art.AddedColliderCount, Is.Zero, "Presentation only: the throw physics stay the same.");
            art.Show(.3f, false, null, specialWater: false);
            Assert.That(art.Special, Is.False, "Talisman Water keeps the plain drop and ripple.");
            Assert.That(art.VisibleSparkles, Is.Zero);
            art.Show(.3f, false, null, specialWater: true);
            art.Clear();
            Assert.That(art.Shown, Is.False); Assert.That(art.VisibleSparkles, Is.Zero);
        }

        [Test]
        public void TheTalismanOrbShowsTheRedSealInsteadOfTheDrop()
        {
            // #540: the talisman orb carries the red seal; its shimmering blue body is the five-colour look in blue tones.
            var art = LabJigwiSupplyWaterArt.Create(root.transform);
            art.Show(.3f, false, null, talismanOrb: true);
            Assert.That(art.Talisman, Is.True);
            Assert.That(art.Special, Is.False, "It is not B's special Water.");
            Assert.That(art.VisibleSparkles, Is.Zero, "The blue shimmer (LabFiveColorSparkle) carries the sparkle.");
            Assert.That(art.AddedColliderCount, Is.Zero);
            art.Clear();
            Assert.That(art.Talisman, Is.False);
        }
    }
}
