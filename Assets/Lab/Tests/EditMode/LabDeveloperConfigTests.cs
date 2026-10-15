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
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(80));
            Assert.That(values[79], Is.EqualTo(0f), "The gourd starts in paint mode.");
            Assert.That(values.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));

            values[0] = 240f; // HP
            values[2] = 90f;  // duration
            values[5] = 5f;   // generation cost
            values[10] = 45f; // orb lifetime
            values[15] = .45f; // orb radius
            Assert.That(new[] { values[17], values[18] }, Is.EqualTo(new[] { .3f, .15f }),
                "Orbs pop out of the drum from 30% size over 0.15 seconds.");
            values[17] = .5f; // spawn pop start size
            values[18] = .2f; // spawn pop duration
            values[29] = 3.25f; // lateral throw gain
            values[35] = .2f; // projectile radius
            values[36] = 1.6f; // nonlinear throw power
            values[37] = 8f; // higher launch angle
            values[38] = .45f; // flight damping
            values[39] = 50f; // Yin chance
            values[40] = 25f; // Yang chance
            values[44] = 10f; // Bomb chance; Relay/Box/Potion stay 5 each
            values[48] = 120f; // existing player HP index remains stable as spin fields are appended
            Assert.That(values[52], Is.EqualTo(10f), "Potion heal defaults to 10 HP.");
            values[52] = 25f; // potion heal
            Assert.That(new[] { values[53], values[54] }, Is.EqualTo(new[] { 2f, 4f }), "Relay x2 and MAX multipliers.");
            values[53] = 1.5f;
            values[54] = 3f;
            Assert.That(values[55], Is.EqualTo(3f), "Bomb damage multiplier.");
            values[55] = 2.5f;
            Assert.That(values[56], Is.EqualTo(2f), "The camera looks 2 units above the target centre by default.");
            values[56] = -1.5f;
            Assert.That(new[] { values[57], values[58], values[59] },
                Is.EqualTo(new[] { 80f, 1.2f, 720f }));
            values[57] = 120f;
            values[58] = .8f;
            values[59] = 900f;
            Assert.That(new[] { values[60], values[61], values[62] },
                Is.EqualTo(new[] { 180f, 1f, 1.8f }), "Orbs leave the drum in every direction by default.");
            values[60] = 40f;
            values[61] = .8f;
            values[62] = 1.4f;
            Assert.That(new[] { values[63], values[64], values[65], values[66] },
                Is.EqualTo(new[] { .2f, 1f, .9f, .7f }), "BGM, generate, hit and miss volumes.");
            Assert.That(new[] { values[67], values[68], values[69], values[70], values[71], values[72], values[73] },
                Is.EqualTo(new[] { 1f, 0f, .4f, 0f, .6f, 2f, 1f }), "Haptics on; generate/hit/item-hit style and intensity.");
            values[63] = .5f;
            values[64] = .8f;
            values[67] = 0f;
            values[68] = 1f; // Medium
            values[72] = 4f; // Rigid
            values[73] = .7f;
            Assert.That(values[74], Is.EqualTo(1f), "One drum per game by default.");
            values[74] = 3f;
            Assert.That(values[75], Is.EqualTo(.6f), "The drum keeps the old generate button's size.");
            values[75] = .8f;
            Assert.That(new[] { values[76], values[77], values[78] }, Is.EqualTo(new[] { .1f, .25f, 1f }),
                "The drum drifts slowly, turns gently and spins at full strength by default.");
            values[76] = .2f;
            values[77] = .5f;
            values[78] = 2f;
            values[79] = 1f; // gourd mode: crack
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(240));
            Assert.That(config.BattleDurationSeconds, Is.EqualTo(90f));
            Assert.That(config.GenerateCost, Is.EqualTo(5f));
            Assert.That(config.OrbLifetimeSeconds, Is.EqualTo(45f));
            Assert.That(config.OrbRadius, Is.EqualTo(.45f));
            Assert.That(config.SpawnPopStartScale, Is.EqualTo(.5f));
            Assert.That(config.SpawnPopDuration, Is.EqualTo(.2f));
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
            Assert.That(config.CameraLookHeightOffset, Is.EqualTo(-1.5f));
            Assert.That(config.TargetSpinStrength, Is.EqualTo(120f));
            Assert.That(config.TargetSpinDamping, Is.EqualTo(.8f));
            Assert.That(config.TargetSpinMaximumSpeed, Is.EqualTo(900f));
            Assert.That(config.SpawnFanAngle, Is.EqualTo(40f));
            Assert.That(config.SpawnMinSpeed, Is.EqualTo(.8f));
            Assert.That(config.SpawnMaxSpeed, Is.EqualTo(1.4f));
            Assert.That(config.BgmVolume, Is.EqualTo(.5f));
            Assert.That(config.GenerateSoundVolume, Is.EqualTo(.8f));
            Assert.That(config.HapticsEnabled, Is.False);
            Assert.That(config.GenerateHapticStyle, Is.EqualTo(LabHapticStyle.Medium));
            Assert.That(config.ItemHitHapticStyle, Is.EqualTo(LabHapticStyle.Rigid));
            Assert.That(config.ItemHitHapticIntensity, Is.EqualTo(.7f));
            Assert.That(config.DrumCount, Is.EqualTo(3));
            Assert.That(config.DrumRadius, Is.EqualTo(.8f));
            Assert.That(config.DrumDriftSpeed, Is.EqualTo(.2f));
            Assert.That(config.DrumDriftTurning, Is.EqualTo(.5f));
            Assert.That(config.DrumSpinStrength, Is.EqualTo(2f));
        }

        [Test]
        public void SavedConfigKeepsTheDrumSpreadAndStaminaTuning()
        {
            // An Inspector save from an older branch once wrote its stale 55° fan into the asset.
            var saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.SpawnFanAngle, Is.EqualTo(180f), "Orbs leave the drum in every direction.");
            Assert.That(saved.StaminaRecoveryAmount, Is.EqualTo(20f));
            Assert.That(saved.StaminaRecoverySeconds, Is.EqualTo(1.7f),
                "A drained bar refills in about one lap of the drum around three players.");
            Assert.That(saved.HitRecovery, Is.EqualTo(10f), "A hit returns half an orb.");
        }

        [Test]
        public void DrumCountIsAWholeNumberFromOneToFive()
        {
            foreach (float invalid in new[] { 0f, 6f, 2.5f })
            {
                float[] values = config.CaptureDeveloperValues();
                values[74] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False, invalid.ToString());
                Assert.That(error, Does.Contain("Drum Count"));
            }
            Assert.That(config.DrumCount, Is.EqualTo(1));
        }

        [Test]
        public void SpawnMinimumSpeedCannotExceedMaximum()
        {
            float[] values = config.CaptureDeveloperValues();
            values[61] = 2f;
            values[62] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("Spawn Minimum Speed"));
            Assert.That(config.SpawnMinSpeed, Is.EqualTo(1f));
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
        public void InvalidSpinTuningDoesNotPartiallyApplyTheDeveloperForm()
        {
            float[] values = config.CaptureDeveloperValues();
            values[0] = 240f;
            values[57] = 201f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
            Assert.That(config.TargetSpinStrength, Is.EqualTo(80f));

            values[57] = 100f;
            values[58] = float.NaN;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            Assert.That(config.TargetSpinStrength, Is.EqualTo(80f));

            values[58] = 2f;
            values[59] = 20f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            Assert.That(config.TargetSpinMaximumSpeed, Is.EqualTo(720f));
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
            [Test]
        public void EveryDeveloperFieldBelongsToExactlyOneCategory()
        {
            int fieldCount = LabConfig.DeveloperFields.Count;
            int[] seen = new int[fieldCount];

            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
            {
                Assert.That(category.Title, Is.Not.Empty);
                Assert.That(category.FieldIndices, Is.Not.Empty, category.Title + " has no fields.");
                foreach (int index in category.FieldIndices)
                {
                    Assert.That(index, Is.InRange(0, fieldCount - 1),
                        category.Title + " points to a field that does not exist: " + index);
                    seen[index]++;
                }
            }

            for (int i = 0; i < fieldCount; i++)
                Assert.That(seen[i], Is.EqualTo(1),
                    LabConfig.DeveloperFields[i].Label + " (" + i + ") must be in exactly one category.");
        }

    }
}
