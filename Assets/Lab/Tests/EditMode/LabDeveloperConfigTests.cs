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
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(91));
            Assert.That(values[90], Is.EqualTo(50f), "The fuse sizzle starts at the Inspector volume.");
            Assert.That(values[87], Is.EqualTo(80f), "Paint victory defaults to 80% of the whole sphere.");
            Assert.That(new[] { values[88], values[89] }, Is.EqualTo(new[] { 20f, 30f }),
                "The gourd drops a bomb every 20 to 30 seconds by default.");
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
            values[41] = 5f;  // Relay chance
            values[42] = 5f;  // Secret Box chance
            values[43] = 5f;  // Potion chance
            values[44] = 10f; // Bomb chance
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
            Assert.That(new[] { values[63], values[64], values[65], values[66], values[84] },
                Is.EqualTo(new[] { 50f, 50f, 50f, 50f, 50f }), "DEV volumes start at 50, which keeps the Inspector volumes.");
            Assert.That(new[] { config.BgmVolume, config.GenerateSoundVolume, config.HitSoundVolume, config.MissSoundVolume },
                Is.EqualTo(new[] { .2f, 1f, .9f, .7f }).Within(1e-5f), "At 50 the Inspector BGM, generate, hit and miss volumes are used as they are.");
            Assert.That(new[] { values[67], values[68], values[69], values[70], values[71], values[72], values[73] },
                Is.EqualTo(new[] { 1f, 1f, 1f, 2f, 1f, 2f, 1f }), "Haptics on; generate Medium, hit and item-hit Heavy, all at full strength.");
            values[63] = 100f; // BGM twice the Inspector volume
            values[64] = 25f;  // generate half the Inspector volume
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
            Assert.That(new[] { values[80], values[81] }, Is.EqualTo(new[] { .4f, 9f }),
                "A bomb blast reaches 40% of the board width and pushes hardest right beside it.");
            values[80] = .6f;
            values[81] = 12f;
            Assert.That(new[] { values[82], values[83] }, Is.EqualTo(new[] { 2f, 1f }), "A blast plays a full Heavy haptic.");
            values[82] = 4f; // Rigid
            values[83] = .5f;
            Assert.That(new[] { values[85], values[86] }, Is.EqualTo(new[] { 0f, 1f }),
                "Combining plays a Light haptic at full strength by default.");
            values[84] = 0f; // combine sound off
            values[85] = 3f; // Soft
            values[86] = .1f;
            values[87] = 60f;
            values[88] = 15f;
            values[89] = 25f;
            values[90] = 100f; // fuse sizzle twice the Inspector volume
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.FuseSoundVolume, Is.EqualTo(1f).Within(1e-5f), "100 doubles the Inspector fuse volume (0.5).");
            Assert.That(config.GourdBombIntervalMin, Is.EqualTo(15f));
            Assert.That(config.GourdBombIntervalMax, Is.EqualTo(25f));
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
            Assert.That(config.CombineSoundVolume, Is.EqualTo(0f));
            Assert.That(config.CombineHapticStyle, Is.EqualTo(LabHapticStyle.Soft));
            Assert.That(config.CombineHapticIntensity, Is.EqualTo(.1f));
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(60));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(.6f));
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
            Assert.That(config.BgmVolume, Is.EqualTo(.4f).Within(1e-5f), "100 doubles the Inspector BGM volume (0.2).");
            Assert.That(config.GenerateSoundVolume, Is.EqualTo(.5f).Within(1e-5f), "25 halves the Inspector generate volume (1).");
            Assert.That(config.HitSoundVolume, Is.EqualTo(.9f).Within(1e-5f), "50 keeps the Inspector hit volume.");
            Assert.That(config.HapticsEnabled, Is.False);
            Assert.That(config.GenerateHapticStyle, Is.EqualTo(LabHapticStyle.Medium));
            Assert.That(config.ItemHitHapticStyle, Is.EqualTo(LabHapticStyle.Rigid));
            Assert.That(config.ItemHitHapticIntensity, Is.EqualTo(.7f));
            Assert.That(config.DrumCount, Is.EqualTo(3));
            Assert.That(config.DrumRadius, Is.EqualTo(.8f));
            Assert.That(config.DrumDriftSpeed, Is.EqualTo(.2f));
            Assert.That(config.DrumDriftTurning, Is.EqualTo(.5f));
            Assert.That(config.DrumSpinStrength, Is.EqualTo(2f));
            Assert.That(config.BombBlastRadius, Is.EqualTo(.6f));
            Assert.That(config.BombBlastStrength, Is.EqualTo(12f));
            Assert.That(config.BombHapticStyle, Is.EqualTo(LabHapticStyle.Rigid));
            Assert.That(config.BombHapticIntensity, Is.EqualTo(.5f));
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
        public void SavedConfigDrumMakesOnlyYinAndYangAndTheGourdDropsBombs()
        {
            // #177: items no longer come from the drum; the gourd drops bombs instead.
            var saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.GenerationChances(), Is.EqualTo(new[] { 50, 50, 0, 0, 0, 0 }),
                "Set Yin 50, Yang 50 and every item 0 in the LabConfig Inspector.");
            Assert.That(saved.GourdBombIntervalMin, Is.EqualTo(20f));
            Assert.That(saved.GourdBombIntervalMax, Is.EqualTo(30f));
        }

        [Test]
        public void SavedAppearanceDrawsTheCartoonBomb()
        {
            var saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.OrbAppearance, Is.Not.Null);
            Assert.That(saved.OrbAppearance.BombArt, Is.Not.Null,
                "Assign Art/Items/ItemBombBody to Bomb Art in the LabOrbAppearance Inspector.");
            Assert.That(saved.OrbAppearance.BombArt.name, Is.EqualTo("ItemBombBody"));
            Assert.That(saved.OrbAppearance.BombFuseArt, Is.Not.Null,
                "Assign Art/Items/ItemBombFuse to Bomb Fuse Art in the LabOrbAppearance Inspector.");
            Assert.That(saved.OrbAppearance.BombFuseArt.name, Is.EqualTo("ItemBombFuse"));
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
            // The drum makes Yin and Yang only (#177); the item chances stay in the form at 0.
            Assert.That(new[] { values[39], values[40], values[41], values[42], values[43], values[44] },
                Is.EqualTo(new[] { 50f, 50f, 0f, 0f, 0f, 0f }));

            values[0] = 240f;
            values[39] = 51f; // total 101
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("100"));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000), "A rejected form must not partially apply.");
            Assert.That(config.GenerationChances(), Is.EqualTo(new[] { 50, 50, 0, 0, 0, 0 }));

            values[39] = 49.5f;
            values[40] = 50.5f;
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


        [System.Serializable]
        private sealed class SharedModeSettings
        {
            public float[] values;
        }

        [Test]
        public void GourdModeAppendsAtIndex79AndRoundTripsWithoutChangingExistingValues()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.GreaterThan(79),
                "Later fields append after Gourd Mode; the total is checked by the form test.");
            Assert.That(LabConfig.DeveloperFields[78].Label, Is.EqualTo("Drum Spin Strength"));
            LabDeveloperField field = LabConfig.DeveloperFields[79];
            Assert.That(field.Label, Is.EqualTo("Gourd Mode (0 paint, 1 crack)"));
            Assert.That(field.Min, Is.Zero);
            Assert.That(field.Max, Is.EqualTo(1f));
            Assert.That(field.WholeNumber, Is.True);
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            float[] values = config.CaptureDeveloperValues();
            var originalPrefix = new float[79];
            System.Array.Copy(values, originalPrefix, 79);
            foreach (float mode in new[] { 1f, 0f, 1f })
            {
                values[79] = mode;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(config.GourdMode, Is.EqualTo(mode == 0f ? LabGourdMode.Paint : LabGourdMode.Crack));
                float[] captured = config.CaptureDeveloperValues();
                Assert.That(captured[79], Is.EqualTo(mode));
                var currentPrefix = new float[79];
                System.Array.Copy(captured, currentPrefix, 79);
                Assert.That(currentPrefix, Is.EqualTo(originalPrefix),
                    "Appending the mode cannot shift or modify the previous 79 tuning values.");
            }
        }

        [Test]
        public void InvalidGourdModeCannotPartiallyApplyAnotherSetting()
        {
            float[] values = config.CaptureDeveloperValues();
            values[79] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            values[0] = 240f;
            foreach (float invalid in new[] { -1f, 2f, .5f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                values[79] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, invalid.ToString());
                Assert.That(error, Does.Contain("Gourd Mode"));
                Assert.That(config.MonsterMaxHp, Is.EqualTo(1000), "The whole form is rejected atomically.");
                Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
            }
        }

        [TestCase(0)]
        [TestCase(79)]
        [TestCase(81)]
        public void ObsoleteOrOversizedModeSchemaIsRejectedBeforeApplyingSettings(int length)
        {
            float[] invalid = new float[length];
            float[] current = config.CaptureDeveloperValues();
            System.Array.Copy(current, invalid, Mathf.Min(length, current.Length));
            if (length > 0) invalid[0] = 240f;
            Assert.That(config.TryApplyDeveloperValues(invalid, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            Assert.Throws<System.ArgumentException>(() => LabSessionSettings.Fingerprint(invalid, 3, 1));
        }

        [Test]
        public void SharedGourdModeChangesFingerprintAndSurvivesJsonAndConfigRoundTrip()
        {
            float[] values = config.CaptureDeveloperValues();
            // The fingerprint function accepts a protocol value independently of the current
            // wire version; connection compatibility itself is tested by the networking layer.
            const ushort protocol = 1;
            string paintFingerprint = LabSessionSettings.Fingerprint(values, 3, protocol);
            values[79] = 1f;
            string crackFingerprint = LabSessionSettings.Fingerprint(values, 3, protocol);
            Assert.That(crackFingerprint, Is.Not.EqualTo(paintFingerprint),
                "Clients must acknowledge the selected mode as part of the shared settings.");
            string json = JsonUtility.ToJson(new SharedModeSettings { values = values });
            float[] received = JsonUtility.FromJson<SharedModeSettings>(json).values;
            Assert.That(received.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(LabSessionSettings.Fingerprint(received, 3, protocol), Is.EqualTo(crackFingerprint));
            var clientConfig = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(clientConfig.TryApplyDeveloperValues(received, out string error), Is.True, error);
                Assert.That(clientConfig.GourdMode, Is.EqualTo(LabGourdMode.Crack));
                Assert.That(clientConfig.CaptureDeveloperValues(), Is.EqualTo(values));
                values[79] = 0f;
                Assert.That(LabSessionSettings.Fingerprint(values, 3, protocol), Is.EqualTo(paintFingerprint));
            }
            finally { Object.DestroyImmediate(clientConfig); }
        }

        [Test]
        public void UnknownInspectorGourdEnumFallsBackToPaintForSharedSettings()
        {
            JsonUtility.FromJsonOverwrite("{\"gourdMode\":999}", config);
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            Assert.That(config.CaptureDeveloperValues()[79], Is.Zero);
        }

        [Test]
        public void PaintGoalAppendsAfterAllPrevious87FieldsWithoutChangingTheirIndices()
        {
            // Wire-schema fence captured from PR #176 / 119fbd39. These positions also
            // address saved DEV rows, shared fingerprints and the existing sound/item fields.
            string[] previousLabels =
            {
                "Monster Max HP",
                "Damage per Hit",
                "Battle Duration (seconds)",
                "Maximum Stamina",
                "Starting Stamina",
                "Orb Generation Cost",
                "Stamina Recovery Amount",
                "Stamina Recovery Interval (seconds)",
                "Stamina Reward per Hit",
                "Stored Orb Limit",
                "Orb Lifetime (seconds)",
                "Orb Bounce",
                "Orb Floor Deceleration",
                "Orb Stop Speed",
                "Orb Maximum Release Speed",
                "Orb Radius",
                "Orb Board Height",
                "Spawn Pop Start Size (0-1 of the orb)",
                "Spawn Pop Duration (seconds)",
                "Camera Radius",
                "Camera Height Offset",
                "Throw Origin Radius",
                "Throw Origin Height Offset",
                "Throw Origin Lateral Range",
                "Throw Minimum Swipe Speed",
                "Throw Minimum Swipe Distance",
                "Throw Upward Dominance",
                "Throw Maximum Swipe Speed",
                "Throw Minimum Upward Speed",
                "Throw Lateral Gain",
                "Throw Upward Gain",
                "Throw Forward Gain",
                "Throw Maximum World Speed",
                "Throw Gravity",
                "Throw Lifetime (seconds)",
                "Throw Radius",
                "Throw Power Exponent",
                "Throw Loft Offset (degrees)",
                "Throw Air Damping",
                "Yin Chance (%)",
                "Yang Chance (%)",
                "Relay Orb Chance (%)",
                "Secret Box Chance (%)",
                "Potion Chance (%)",
                "Bomb Chance (%)",
                "Item Lifetime (seconds)",
                "Item Unlock Swipes Min",
                "Item Unlock Swipes Max",
                "Player Max HP",
                "Combine Speed",
                "Drop Combine (0 off, 1 on)",
                "Contact Combine (0 off, 1 on)",
                "Potion Heal (HP)",
                "Relay x2 Damage Multiplier",
                "Relay MAX Damage Multiplier",
                "Bomb Damage Multiplier",
                "Camera Look Height Offset",
                "Target Spin Strength",
                "Target Spin Damping",
                "Target Spin Maximum Speed (deg/s)",
                "Spawn Fan Angle (deg each side, 180 = all directions)",
                "Spawn Minimum Speed (board widths/s)",
                "Spawn Maximum Speed (board widths/s)",
                "BGM Volume (50 = Inspector)",
                "Generate Sound Volume (50 = Inspector)",
                "Hit Sound Volume (50 = Inspector)",
                "Miss Sound Volume (50 = Inspector)",
                "Haptics (0 off, 1 on)",
                "Generate Haptic Style (0 Light 1 Medium 2 Heavy 3 Soft 4 Rigid)",
                "Generate Haptic Intensity",
                "Hit Haptic Style (0-4)",
                "Hit Haptic Intensity",
                "Item Hit Haptic Style (0-4)",
                "Item Hit Haptic Intensity",
                "Drum Count",
                "Drum Radius",
                "Drum Drift Speed (board widths/s)",
                "Drum Drift Turning (per second)",
                "Drum Spin Strength",
                "Gourd Mode (0 paint, 1 crack)",
                "Bomb Blast Radius (board widths)",
                "Bomb Blast Strength (board widths/s)",
                "Bomb Haptic Style (0-4)",
                "Bomb Haptic Intensity",
                "Combine Sound Volume (50 = Inspector)",
                "Combine Haptic Style (0-4)",
                "Combine Haptic Intensity",
            };
            Assert.That(previousLabels.Length, Is.EqualTo(87));
            Assert.That(LabConfig.DeveloperFields.Count, Is.GreaterThanOrEqualTo(88));
            for (int i = 0; i < previousLabels.Length; i++)
                Assert.That(LabConfig.DeveloperFields[i].Label, Is.EqualTo(previousLabels[i]), "Index " + i);
            LabDeveloperField field = LabConfig.DeveloperFields[87];
            Assert.That(field.Label, Is.EqualTo("Paint Victory Coverage (%)"));
            Assert.That(field.Min, Is.EqualTo(1f));
            Assert.That(field.Max, Is.EqualTo(100f));
            Assert.That(field.WholeNumber, Is.True);
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(80));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(.8f));
            Assert.That(config.CaptureDeveloperValues()[87], Is.EqualTo(80f));
            int[] gourdIndices = null;
            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
                if (category.Title == "Gourd") gourdIndices = category.FieldIndices;
            Assert.That(gourdIndices, Is.Not.Null);
            Assert.That(gourdIndices, Is.SupersetOf(new[] { 79, 87 }));
        }

        [Test]
        public void GourdBombIntervalIsAppendedToTheGourdCategoryAndMinCannotExceedMax()
        {
            Assert.That(LabConfig.DeveloperFields[88].Label, Is.EqualTo("Gourd Bomb Interval Min (seconds, 0 = off)"));
            Assert.That(LabConfig.DeveloperFields[89].Label, Is.EqualTo("Gourd Bomb Interval Max (seconds, 0 = off)"));
            foreach (int index in new[] { 88, 89 })
            {
                Assert.That(LabConfig.DeveloperFields[index].Min, Is.EqualTo(0f));
                Assert.That(LabConfig.DeveloperFields[index].Max, Is.EqualTo(600f));
            }
            int[] gourdIndices = null;
            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
                if (category.Title == "Gourd") gourdIndices = category.FieldIndices;
            Assert.That(gourdIndices, Is.EqualTo(new[] { 79, 87, 88, 89 }));
            Assert.That(config.GourdBombIntervalMin, Is.EqualTo(20f));
            Assert.That(config.GourdBombIntervalMax, Is.EqualTo(30f));

            float[] values = config.CaptureDeveloperValues();
            values[0] = 777f;
            values[88] = 31f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("Gourd Bomb Interval"));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000), "A rejected form must not partially apply.");
            Assert.That(config.GourdBombIntervalMin, Is.EqualTo(20f));

            values[88] = 0f;
            values[89] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.True, error);
            Assert.That(config.GourdBombIntervalMax, Is.Zero, "A maximum of 0 turns the gourd's bombs off.");
        }

        [TestCase(1)]
        [TestCase(80)]
        [TestCase(100)]
        public void ValidPaintGoalsRoundTripWithoutModifyingThePrevious87Values(int percent)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] changed = (float[])before.Clone();
            changed[87] = percent;
            Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(percent));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(percent / 100f));
            float[] after = config.CaptureDeveloperValues();
            for (int i = 0; i < 87; i++) Assert.That(after[i], Is.EqualTo(before[i]), "Unchanged index " + i);
            Assert.That(after[87], Is.EqualTo(percent));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            Assert.That(config.BombBlastRadius, Is.EqualTo(.4f));
            Assert.That(config.BombBlastStrength, Is.EqualTo(9f));
            Assert.That(config.CombineHapticStyle, Is.EqualTo(LabHapticStyle.Light));
            Assert.That(config.CombineHapticIntensity, Is.EqualTo(1f));
        }

        [TestCase(0f)]
        [TestCase(101f)]
        [TestCase(-1f)]
        [TestCase(80.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPaintGoalRejectsTheEntireDeveloperForm(float invalid)
        {
            float[] values = config.CaptureDeveloperValues();
            values[87] = 37f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            float[] baseline = config.CaptureDeveloperValues();
            values[0] = 777f;
            values[79] = 1f;
            values[86] = .4f;
            values[87] = invalid;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Paint Victory Coverage"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline), "No earlier field may be partially applied.");
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(37));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
        }

        [Test]
        public void Previous87ValueSettingsAreRejectedByConfigAndFingerprint()
        {
            float[] values = config.CaptureDeveloperValues();
            var oldSchema = new float[87];
            System.Array.Copy(values, oldSchema, 87);
            oldSchema[0] = 777f;
            Assert.That(config.TryApplyDeveloperValues(oldSchema, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000));
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(80));
            Assert.Throws<System.ArgumentException>(() => LabSessionSettings.Fingerprint(oldSchema, 3, 1));
        }

        [Test]
        public void SharedPaintGoalChangesFingerprintAndSurvivesJsonAndClientConfigRoundTrip()
        {
            const ushort fixtureProtocol = 1;
            float[] values = config.CaptureDeveloperValues();
            string first = LabSessionSettings.Fingerprint(values, 3, fixtureProtocol);
            values[87] = 65f;
            string changed = LabSessionSettings.Fingerprint(values, 3, fixtureProtocol);
            Assert.That(changed, Is.Not.EqualTo(first), "Participants must acknowledge the round's paint goal.");
            string json = JsonUtility.ToJson(new SharedModeSettings { values = values });
            float[] received = JsonUtility.FromJson<SharedModeSettings>(json).values;
            var clientConfig = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(clientConfig.TryApplyDeveloperValues(received, out string error), Is.True, error);
                Assert.That(clientConfig.PaintVictoryPercent, Is.EqualTo(65));
                Assert.That(clientConfig.PaintVictoryCoverage, Is.EqualTo(.65f));
                Assert.That(clientConfig.CaptureDeveloperValues(), Is.EqualTo(values));
                Assert.That(LabSessionSettings.Fingerprint(received, 3, fixtureProtocol), Is.EqualTo(changed));
            }
            finally { Object.DestroyImmediate(clientConfig); }
        }

        [TestCase(0, 1)]
        [TestCase(101, 100)]
        public void InspectorPaintGoalIsClampedToTheSamePercentageRange(int serialized, int expected)
        {
            JsonUtility.FromJsonOverwrite("{\"paintVictoryPercent\":" + serialized + "}", config);
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(expected));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(expected / 100f));
            Assert.That(config.CaptureDeveloperValues()[87], Is.EqualTo(expected));
        }

    }
}
