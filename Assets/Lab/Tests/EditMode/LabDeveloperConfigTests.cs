using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabDeveloperConfigTests
    {
        private LabConfig config;

        [SetUp]
        public void SetUp() => config = ScriptableObject.CreateInstance<LabConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        [Test]
        public void DeveloperFormCoversEveryTuningValueAndAppliesValidChanges()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(56));
            Assert.That(values.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));

            values[0] = 240f; // HP
            values[2] = 90f;  // duration
            values[5] = 5f;   // generation cost
            values[10] = 45f; // orb lifetime
            values[15] = .45f; // orb radius
            values[29] = 3.25f; // lateral throw gain
            values[35] = .2f; // projectile radius
            values[36] = 1.6f; // nonlinear throw power
            values[37] = 8f; // higher launch angle
            values[38] = .45f; // flight damping
            values[39] = 50f; // Yin chance
            values[40] = 25f; // Yang chance
            values[44] = 10f; // Bomb chance; Relay/Box/Potion stay 5 each
            values[48] = 120f; // new player HP setting remains last, preserving earlier indices
            Assert.That(values[52], Is.EqualTo(10f), "Potion heal defaults to 10 HP.");
            values[52] = 25f; // potion heal
            Assert.That(new[] { values[53], values[54] }, Is.EqualTo(new[] { 2f, 4f }), "Relay x2 and MAX multipliers.");
            values[53] = 1.5f;
            values[54] = 3f;
            Assert.That(values[55], Is.EqualTo(3f), "Bomb damage multiplier.");
            values[55] = 2.5f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.MonsterMaxHp, Is.EqualTo(240));
            Assert.That(config.BattleDurationSeconds, Is.EqualTo(90f));
            Assert.That(config.GenerateCost, Is.EqualTo(5f));
            Assert.That(config.OrbLifetimeSeconds, Is.EqualTo(45f));
            Assert.That(config.OrbRadius, Is.EqualTo(.45f));
            Assert.That(config.ThrowLateralGain, Is.EqualTo(3.25f));
            Assert.That(config.ThrowRadius, Is.EqualTo(.2f));
            Assert.That(config.ThrowPowerExponent, Is.EqualTo(1.6f));
            Assert.That(config.ThrowLoftOffsetDegrees, Is.EqualTo(8f));
            Assert.That(config.ThrowAirDamping, Is.EqualTo(.45f));
            Assert.That(config.GenerationChances(), Is.EqualTo(new[] { 50, 25, 5, 5, 5, 10 }));
            Assert.That(config.PlayerMaxHp, Is.EqualTo(120));
            Assert.That(config.PotionHeal, Is.EqualTo(25));
            Assert.That(config.RelayDoubleMultiplier, Is.EqualTo(1.5f));
            Assert.That(config.RelayMaxMultiplier, Is.EqualTo(3f));
            Assert.That(config.BombMultiplier, Is.EqualTo(2.5f));
        }

        [Test]
        public void PlayerHpSliderUsesOneToThreeHundredWithoutPartiallyApplyingInvalidForms()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(values[48], Is.EqualTo(300f));
            Assert.That(LabConfig.DeveloperFields[48].Min, Is.EqualTo(1f));
            Assert.That(LabConfig.DeveloperFields[48].Max, Is.EqualTo(300f));

            values[48] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.PlayerMaxHp, Is.EqualTo(1));
            values[48] = 300f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.True, error);
            Assert.That(config.PlayerMaxHp, Is.EqualTo(300));

            values[0] = 240f;
            foreach (float bad in new[] { 0f, 301f, 1.5f, float.NaN })
            {
                values[48] = bad;
                Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
                Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
                Assert.That(config.PlayerMaxHp, Is.EqualTo(300));
            }
        }

        [Test]
        public void GenerationChancesStartFromIssueDefaultsAndMustTotalOneHundred()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(new[] { values[39], values[40], values[41], values[42], values[43], values[44] },
                Is.EqualTo(new[] { 40f, 40f, 5f, 5f, 5f, 5f }));

            values[0] = 240f;
            values[39] = 41f; // total 101
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("100"));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000), "A rejected form must not partially apply.");
            Assert.That(config.GenerationChances(), Is.EqualTo(new[] { 40, 40, 5, 5, 5, 5 }));

            values[39] = 39.5f;
            values[40] = 40.5f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False, "Chances are whole percents.");
        }

        [Test]
        public void InvalidLateFieldOrCrossConstraintDoesNotPartiallyApplyEarlyFields()
        {
            float[] values = config.CaptureDeveloperValues();
            values[0] = 240f;
            values[38] = float.NaN;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));

            values[38] = .08f;
            values[3] = 40f;
            values[4] = 50f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
            Assert.That(config.StaminaMax, Is.EqualTo(100f));

            values = config.CaptureDeveloperValues();
            values[0] = 240.5f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
        }

        [Test]
        public void ItemSettingsDefaultToFifteenSecondsAndTwoToFourSwipes()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(new[] { values[45], values[46], values[47] }, Is.EqualTo(new[] { 15f, 2f, 4f }));

            values[45] = 30f;
            values[46] = 0f;
            values[47] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.ItemLifetimeSeconds, Is.EqualTo(30f));
            Assert.That(config.ItemUnlockSwipesMin, Is.EqualTo(0));
            Assert.That(config.ItemUnlockSwipesMax, Is.EqualTo(1));

            values[46] = 3f; // min above max
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Item Unlock Swipes Min"));
            Assert.That(config.ItemUnlockSwipesMax, Is.EqualTo(1), "A rejected form must not partially apply.");
        }

        [Test]
        public void EverySliderPreservesDefaultAndBothAllowedEndpoints()
        {
            float[] defaults = config.CaptureDeveloperValues();
            for (int i = 0; i < defaults.Length; i++)
            {
                LabDeveloperField field = LabConfig.DeveloperFields[i];
                foreach (float candidate in new[] { field.Min, defaults[i], field.Max })
                {
                    float position = LabDeveloperSliderScale.Normalize(field, defaults[i], candidate);
                    float restored = LabDeveloperSliderScale.Denormalize(field, defaults[i], position);
                    float tolerance = Mathf.Max(.01f, Mathf.Abs(candidate) * .0001f);
                    Assert.That(position, Is.InRange(0f, 1f), field.Label);
                    Assert.That(restored, Is.EqualTo(candidate).Within(tolerance), field.Label);
                }
            }
        }
    }
}
