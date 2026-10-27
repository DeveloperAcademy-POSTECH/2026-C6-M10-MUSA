using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabHapticsTests
    {
        private GameObject host;
        private LabHaptics haptics;
        private LabConfig config;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("haptics");
            haptics = host.AddComponent<LabHaptics>();
            config = ScriptableObject.CreateInstance<LabConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ABombBlastPlaysAHeavyHapticUnlessHapticsAreOffOrSilent()
        {
            Assert.That(config.BombHapticStyle, Is.EqualTo(LabHapticStyle.Heavy));
            Assert.That(haptics.PlayForBomb(config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.Bomb));

            float[] values = config.CaptureDeveloperValues();
            values[83] = 0f; // bomb haptic intensity
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(haptics.PlayForBomb(config), Is.False, "Zero intensity plays nothing.");
            values[83] = 1f;
            values[67] = 0f; // haptics off
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.True, error);
            Assert.That(haptics.PlayForBomb(config), Is.False, "Turning haptics off silences the blast too.");
            Assert.That(haptics.PlayCount, Is.EqualTo(1));
        }

        [Test]
        public void BaseDamageUsesHitHapticAndBiggerItemDamageUsesItemHaptic()
        {
            Assert.That(config.Damage, Is.EqualTo(20));
            Assert.That(haptics.PlayForHit(20, config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.Hit), "A normal 20-damage hit is the weak haptic.");
            Assert.That(haptics.PlayForHit(60, config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.ItemHit), "Item damage above the base is strong.");
            Assert.That(haptics.PlayForHit(10, config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.Hit));
            Assert.That(haptics.PlayCount, Is.EqualTo(3));
        }

        [Test]
        public void CombineUsesALightHapticUnlessSilent()
        {
            Assert.That(config.CombineHapticStyle, Is.EqualTo(LabHapticStyle.Light),
                "Combining uses the lightest style; hitting the yokai is Heavy.");
            Assert.That(config.HitHapticStyle, Is.EqualTo(LabHapticStyle.Heavy));
            Assert.That(haptics.PlayForCombine(config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.Combine));

            float[] values = config.CaptureDeveloperValues();
            values[86] = 0f; // combine haptic intensity
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(haptics.PlayForCombine(config), Is.False, "Zero intensity plays nothing.");
            Assert.That(haptics.PlayCount, Is.EqualTo(1));
        }

        [Test]
        public void GenerateUsesItsOwnHaptic()
        {
            Assert.That(haptics.PlayForGenerate(config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.Generate));
        }

        [Test]
        public void TurningHapticsOffOrZeroIntensityPlaysNothing()
        {
            float[] values = config.CaptureDeveloperValues();
            values[67] = 0f; // haptics off
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(haptics.PlayForHit(60, config), Is.False);
            Assert.That(haptics.PlayForGenerate(config), Is.False);

            values[67] = 1f;
            values[69] = 0f; // generate intensity 0
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.True, error);
            Assert.That(haptics.PlayForGenerate(config), Is.False);
            Assert.That(haptics.PlayForHit(20, config), Is.True);
            Assert.That(haptics.PlayCount, Is.EqualTo(1));
        }

        [Test]
        public void AComboClearPulsesThreeTimesAndAFailBuzzesForTwoSecondsUnlessHapticsAreOff()
        {
            Assert.That(haptics.ComboClearPulses, Is.EqualTo(5), "Stronger since #301: five quick heavy pulses.");
            Assert.That(haptics.ComboClearGapSeconds, Is.EqualTo(.1f));
            Assert.That(haptics.ComboFailSeconds, Is.EqualTo(1.5f), "Shortened in #301: a 1.5 s buzz.");
            Assert.That(haptics.PlayForComboClear(config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.ComboClear));
            Assert.That(haptics.PlayForComboFail(config), Is.True);
            Assert.That(haptics.LastKind, Is.EqualTo(LabHapticKind.ComboFail));

            float[] values = config.CaptureDeveloperValues();
            values[67] = 0f; // haptics off
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            int before = haptics.PlayCount;
            Assert.That(haptics.PlayForComboClear(config), Is.False);
            Assert.That(haptics.PlayForComboFail(config), Is.False);
            Assert.That(haptics.PlayCount, Is.EqualTo(before));
        }
    }
}
