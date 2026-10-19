using System.Linq;
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
        public void GourdSizeRoundTripsAndRejectsInvalidValuesAtomically()
        {
            var baseline = config.CaptureDeveloperValues();
            Assert.That(baseline[145], Is.EqualTo(1.4f));
            foreach (float size in new[] { .5f, 1f, 1.8f, 3f })
            {
                var values = (float[])baseline.Clone();
                values[145] = size;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(config.GourdVisualSize, Is.EqualTo(size));
                Assert.That(config.CaptureDeveloperValues().Take(145), Is.EqualTo(baseline.Take(145)));
            }
            var before = config.CaptureDeveloperValues();
            foreach (float invalid in new[] { .49f, 3.01f, float.NaN, float.PositiveInfinity })
            {
                var values = (float[])before.Clone();
                values[145] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
            }
        }

        [Test]
        public void DeveloperFormCoversEveryTuningValueAndAppliesValidChanges()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(146));
            Assert.That(values[90], Is.EqualTo(50f), "The fuse sizzle starts at the Inspector volume.");
            Assert.That(new[] { values[91], values[92], values[93], values[94] }, Is.EqualTo(new[] { 20f, 40f, 60f, 80f }),
                "A handed-on drum stops 20-40, 40-60 or 60-80% of the board in from where it entered.");
            Assert.That(new[] { values[95], values[96], values[97], values[98] }, Is.EqualTo(new[] { 40f, 35f, 15f, 10f }),
                "Near, middle, far and pass-through chances.");
            Assert.That(values[87], Is.EqualTo(100f), "Paint victory always requires the whole surface.");
            Assert.That(new[] { values[88], values[89] }, Is.EqualTo(new[] { 20f, 30f }),
                "The gourd drops a bomb every 20 to 30 seconds by default.");
            Assert.That(values[79], Is.EqualTo(0f), "The gourd starts in paint mode.");
            Assert.That(values[99], Is.EqualTo(1f), "The minimap is on by default.");
            Assert.That(values[100], Is.EqualTo(50f), "The minimap starts at half of its drawn size.");
            Assert.That(new[] { values[101], values[102] }, Is.EqualTo(new[] { 0f, 0f }), "The minimap starts in its usual place.");
            Assert.That(values[103], Is.EqualTo(50f), "The bomb sound starts at the Inspector volume.");
            Assert.That(config.BombSoundVolume, Is.EqualTo(.9f).Within(1e-5f));
            Assert.That(values[104], Is.EqualTo(1f), "The bomb plays the popcorn-machine ppeong by default.");
            Assert.That(values.Skip(119).Take(6).ToArray(),
                Is.EqualTo(new[] { .45f, 3f, .32f, 42f, 2f, .65f }),
                "The gourd rope has a slow cycle and a lively hit spring by default.");
            Assert.That(values.Skip(125).Take(4).ToArray(), Is.EqualTo(new[] { 12f, 40f, 10f, 1.4f }));
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
            values[87] = 100f;
            values[88] = 15f;
            values[89] = 25f;
            values[90] = 100f; // fuse sizzle twice the Inspector volume
            values[91] = 10f; values[92] = 30f; values[93] = 50f; values[94] = 90f; // drum stop zone edges
            values[95] = 25f; values[96] = 25f; values[97] = 25f; values[98] = 25f; // drum stop chances
            values[99] = 0f; // minimap off
            values[100] = 80f; // minimap bigger than the default 50
            values[101] = -120f; // 120 to the left
            values[102] = 60f; // 60 up
            values[103] = 25f; // bomb sound half the Inspector volume
            values[104] = 0f; // bomb sound: boom
            values[105] = 0f; // low paint resolution
            values[106] = 3f; // initial patch coverage
            values[107] = 5f; // same-region hit expands its target area
            values[108] = 8f; // growth per second
            values[109] = 75f; // frontier fill chance
            values[110] = 2f; // vertical preference
            Assert.That(values[111], Is.EqualTo(1f), "Completed-gourd viewing is on by default.");
            values[111] = 1f; // allow the room to inspect completed Paint results locally
            values[119] = .6f; // automatic rope travel
            values[120] = 2.5f; // cycle duration
            values[121] = .55f; // impact bounce
            values[122] = 60f; // spring
            values[123] = 1.5f; // damping
            values[124] = .9f; // maximum hit extension
            values[125] = 8f; // swing impact
            values[126] = 45f; // swing angle limit
            values[127] = 12f; // swing restoring force
            values[128] = 1.8f; // swing damping
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.MinimapEnabled, Is.False, "DEV can turn the minimap off.");
            Assert.That(config.MinimapSizePercent, Is.EqualTo(80), "DEV sizes the minimap from 0 to 100 with 50 in the middle.");
            Assert.That(config.MinimapMove, Is.EqualTo(new Vector2(-120f, 60f)), "DEV can move the minimap left/right and up/down.");
            Assert.That(config.BombSoundVolume, Is.EqualTo(.45f).Within(1e-5f), "25 halves the Inspector bomb volume (0.9).");
            Assert.That(config.BombSoundStyle, Is.EqualTo(LabBombSoundStyle.Boom), "DEV switches the bomb to the ordinary boom.");
            Assert.That(config.FuseSoundVolume, Is.EqualTo(1f).Within(1e-5f), "100 doubles the Inspector fuse volume (0.5).");
            Assert.That(config.DrumStopBounds(), Is.EqualTo(new[] { .1f, .3f, .5f, .9f }).Within(1e-6f));
            Assert.That(config.DrumStopChances(), Is.EqualTo(new[] { 25, 25, 25, 25 }));
            Assert.That(config.GourdBombIntervalMin, Is.EqualTo(15f));
            Assert.That(config.GourdBombIntervalMax, Is.EqualTo(25f));
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
            Assert.That(config.CombineSoundVolume, Is.EqualTo(0f));
            Assert.That(config.CombineHapticStyle, Is.EqualTo(LabHapticStyle.Soft));
            Assert.That(config.CombineHapticIntensity, Is.EqualTo(.1f));
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(1f));
            Assert.That(config.PaintSurfaceResolution, Is.Zero);
            Assert.That(config.PaintInitialPercent, Is.EqualTo(3f));
            Assert.That(config.PaintRepeatPercent, Is.EqualTo(5f));
            Assert.That(config.PaintGrowthPercentPerSecond, Is.EqualTo(8f));
            Assert.That(config.PaintFillChancePercent, Is.EqualTo(75f));
            Assert.That(config.PaintVerticalWeight, Is.EqualTo(2f));
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values), "All 129 fields round-trip in wire order.");
            Assert.That(new[] { config.GourdRopeCycleAmplitude, config.GourdRopeCyclePeriod,
                    config.GourdRopeHitBounce, config.GourdRopeSpring, config.GourdRopeDamping,
                    config.GourdRopeMaximumHitExtension },
                Is.EqualTo(new[] { .6f, 2.5f, .55f, 60f, 1.5f, .9f }));
            Assert.That(new[] { config.GourdSwingImpactStrength, config.GourdSwingMaximumAngle,
                config.GourdSwingReturnStrength, config.GourdSwingDamping },
                Is.EqualTo(new[] { 8f, 45f, 12f, 1.8f }));
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
        public void DrumStopZonesAreAppendedToTheDrumCategoryAndRejectBadOrderOrTotals()
        {
            string[] labels =
            {
                "Drum Stop Near From (% of board from entry)",
                "Drum Stop Near To / Middle From (%)",
                "Drum Stop Middle To / Far From (%)",
                "Drum Stop Far To (%)",
                "Drum Stop Near Chance (%)",
                "Drum Stop Middle Chance (%)",
                "Drum Stop Far Chance (%)",
                "Drum Pass Through Chance (%)"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                LabDeveloperField field = LabConfig.DeveloperFields[91 + i];
                Assert.That(field.Label, Is.EqualTo(labels[i]));
                Assert.That(field.Min, Is.EqualTo(0f));
                Assert.That(field.Max, Is.EqualTo(100f));
                Assert.That(field.WholeNumber, Is.True);
            }
            int[] drumIndices = null;
            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
                if (category.Title == "Drum") drumIndices = category.FieldIndices;
            Assert.That(drumIndices, Is.EqualTo(new[] { 74, 75, 76, 77, 78, 91, 92, 93, 94, 95, 96, 97, 98 }));
            Assert.That(config.DrumStopBounds(), Is.EqualTo(new[] { .2f, .4f, .6f, .8f }).Within(1e-6f));
            Assert.That(config.DrumStopChances(), Is.EqualTo(new[] { 40, 35, 15, 10 }));

            float[] values = config.CaptureDeveloperValues();
            values[0] = 777f;
            values[92] = 70f; // near ends after middle ends
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("Drum stop zone"));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000), "A rejected form must not partially apply.");

            values[92] = 40f;
            values[98] = 11f; // total 101
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Drum stop chances"));
            Assert.That(config.DrumStopChances(), Is.EqualTo(new[] { 40, 35, 15, 10 }));

            // A hand-edited Inspector array that does not make sense falls back to the defaults.
            JsonUtility.FromJsonOverwrite("{\"drumStopBounds\":[60,40,20,10],\"drumStopChances\":[50,50,50,50]}", config);
            Assert.That(config.DrumStopBounds(), Is.EqualTo(new[] { .2f, .4f, .6f, .8f }).Within(1e-6f));
            Assert.That(config.DrumStopChances(), Is.EqualTo(new[] { 40, 35, 15, 10 }));
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
        public void ThrowFormAppendsAfterAllPrevious112FieldsWithoutChangingTheirIndices()
        {
            // Existing rows keep their indices, including the fixed victory goal and the
            // last sound toggle, six paint-growth fields and viewer permission at 111. Straight appends after this prefix.
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
                "Paint Victory Coverage (%)",
                "Gourd Bomb Interval Min (seconds, 0 = off)",
                "Gourd Bomb Interval Max (seconds, 0 = off)",
                "Fuse Sound Volume (50 = Inspector)",
                "Drum Stop Near From (% of board from entry)",
                "Drum Stop Near To / Middle From (%)",
                "Drum Stop Middle To / Far From (%)",
                "Drum Stop Far To (%)",
                "Drum Stop Near Chance (%)",
                "Drum Stop Middle Chance (%)",
                "Drum Stop Far Chance (%)",
                "Drum Pass Through Chance (%)",
                "Minimap (0 off, 1 on)",
                "Minimap Size (%, 50 = default)",
                "Minimap Move X (- left, + right)",
                "Minimap Move Y (- down, + up)",
                "Bomb Sound Volume (50 = Inspector)",
                "Bomb Sound (0 boom, 1 ppeong)",
                "Paint Surface Resolution (0 low 1 medium 2 high)",
                "Paint Initial Coverage (%)",
                "Paint Repeat Growth (%)",
                "Paint Growth Speed (%/s)",
                "Paint Fill Chance (%)",
                "Paint Vertical Weight",
                "Completed Gourd Viewer (0 off, 1 on)"
            };
            Assert.That(previousLabels.Length, Is.EqualTo(112));
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(146));
            for (int i = 0; i < previousLabels.Length; i++)
                Assert.That(LabConfig.DeveloperFields[i].Label, Is.EqualTo(previousLabels[i]), "Index " + i);
            LabDeveloperField field = LabConfig.DeveloperFields[87];
            Assert.That(field.Label, Is.EqualTo("Paint Victory Coverage (%)"));
            Assert.That(field.Min, Is.EqualTo(100f));
            Assert.That(field.Max, Is.EqualTo(100f));
            Assert.That(field.WholeNumber, Is.True);
            Assert.That(LabConfig.DefaultPaintVictoryPercent, Is.EqualTo(100));
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(1f));
            Assert.That(config.CaptureDeveloperValues()[87], Is.EqualTo(100f));
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
            Assert.That(gourdIndices, Is.EqualTo(new[] { 79, 87, 88, 89, 105, 106, 107, 143, 144, 108, 109, 110, 111, 142,
                119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 139, 133, 134, 135, 136, 137, 138 }));
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

        [TestCase(100)]
        public void FixedPaintGoalRoundTripsWithoutModifyingThePrevious87Values(int percent)
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
        [TestCase(1f)]
        [TestCase(80f)]
        [TestCase(99f)]
        [TestCase(101f)]
        [TestCase(-1f)]
        [TestCase(80.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPaintGoalRejectsTheEntireDeveloperForm(float invalid)
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            float[] baseline = config.CaptureDeveloperValues();
            values[0] = 777f;
            values[79] = 1f;
            values[86] = .4f;
            values[87] = invalid;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Paint Victory Coverage"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline), "No earlier field may be partially applied.");
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
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
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
            Assert.Throws<System.ArgumentException>(() => LabSessionSettings.Fingerprint(oldSchema, 3, 1));
        }

        [Test]
        public void SharedPaintGrowthChangesFingerprintAndSurvivesJsonAndClientConfigRoundTrip()
        {
            const ushort fixtureProtocol = 1;
            float[] values = config.CaptureDeveloperValues();
            string first = LabSessionSettings.Fingerprint(values, 3, fixtureProtocol);
            values[106] = 3.5f;
            values[107] = 8f;
            values[108] = 12f;
            values[109] = 100f;
            values[110] = 2f;
            string changed = LabSessionSettings.Fingerprint(values, 3, fixtureProtocol);
            Assert.That(changed, Is.Not.EqualTo(first), "Participants must acknowledge the round's growth settings.");
            string json = JsonUtility.ToJson(new SharedModeSettings { values = values });
            float[] received = JsonUtility.FromJson<SharedModeSettings>(json).values;
            var clientConfig = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(clientConfig.TryApplyDeveloperValues(received, out string error), Is.True, error);
                Assert.That(clientConfig.PaintVictoryPercent, Is.EqualTo(100));
                Assert.That(clientConfig.PaintVictoryCoverage, Is.EqualTo(1f));
                Assert.That(clientConfig.PaintInitialPercent, Is.EqualTo(3.5f));
                Assert.That(clientConfig.PaintRepeatPercent, Is.EqualTo(8f));
                Assert.That(clientConfig.PaintGrowthPercentPerSecond, Is.EqualTo(12f));
                Assert.That(clientConfig.PaintFillChancePercent, Is.EqualTo(100f));
                Assert.That(clientConfig.PaintVerticalWeight, Is.EqualTo(2f));
                Assert.That(clientConfig.CaptureDeveloperValues(), Is.EqualTo(values));
                Assert.That(LabSessionSettings.Fingerprint(received, 3, fixtureProtocol), Is.EqualTo(changed));
            }
            finally { Object.DestroyImmediate(clientConfig); }
        }

        [TestCase(0, 100)]
        [TestCase(1, 100)]
        [TestCase(80, 100)]
        [TestCase(100, 100)]
        [TestCase(101, 100)]
        public void LegacyInspectorPaintGoalIsNormalizedToFullSurface(int serialized, int expected)
        {
            JsonUtility.FromJsonOverwrite("{\"paintVictoryPercent\":" + serialized + "}", config);
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(expected));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(expected / 100f));
            Assert.That(config.CaptureDeveloperValues()[87], Is.EqualTo(expected));
        }


        [Test]
        public void PaintGrowthControlsAppendWithTheirDefaultsAndBelongToTheGourdCategory()
        {
            string[] labels =
            {
                "Paint Surface Resolution (0 low 1 medium 2 high)",
                "Paint Initial Coverage (%)",
                "Paint Repeat Growth (%)",
                "Paint Growth Speed (%/s)",
                "Paint Fill Chance (%)",
                "Paint Vertical Weight"
            };
            for (int i = 0; i < labels.Length; i++)
                Assert.That(LabConfig.DeveloperFields[105 + i].Label, Is.EqualTo(labels[i]), "Appended index " + (105 + i));
            Assert.That(config.CaptureDeveloperValues().Skip(105).Take(6).ToArray(),
                Is.EqualTo(new[] { 2f, 2f, 4f, 6f, 60f, 1.35f }));
            Assert.That(LabConfig.DeveloperFields[105].WholeNumber, Is.True);
            for (int i = 106; i <= 110; i++) Assert.That(LabConfig.DeveloperFields[i].WholeNumber, Is.False);
            var gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd");
            Assert.That(gourd.FieldIndices, Is.EqualTo(new[] { 79, 87, 88, 89, 105, 106, 107, 143, 144, 108, 109, 110, 111, 142,
                119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 139, 133, 134, 135, 136, 137, 138 }));
        }

        [TestCase(105, 0f, 2f)]
        [TestCase(106, .1f, 100f)]
        [TestCase(107, .1f, 100f)]
        [TestCase(108, .1f, 100f)]
        [TestCase(109, 0f, 100f)]
        [TestCase(110, 1f, 4f)]
        public void PaintGrowthControlsAcceptBothEndpointsAndRejectInvalidFormsAtomically(int index, float min, float max)
        {
            Assert.That(LabConfig.DeveloperFields[index].Min, Is.EqualTo(min));
            Assert.That(LabConfig.DeveloperFields[index].Max, Is.EqualTo(max));
            foreach (float endpoint in new[] { min, max })
            {
                float[] values = config.CaptureDeveloperValues();
                values[index] = endpoint;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(config.CaptureDeveloperValues()[index], Is.EqualTo(endpoint));
            }
            float[] baseline = config.CaptureDeveloperValues();
            foreach (float invalid in new[] { min - .1f, max + .1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                float[] values = (float[])baseline.Clone();
                values[0] = 777f;
                values[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
                Assert.That(error, Does.Contain(LabConfig.DeveloperFields[index].Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline), "A growth error rejects the whole form.");
            }
            if (index == 105)
            {
                float[] values = (float[])baseline.Clone();
                values[105] = .5f;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
                Assert.That(error, Does.Contain("whole number"));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            }
        }

        [Test]
        public void EveryPaintGrowthControlChangesTheSharedFingerprint()
        {
            const ushort protocol = 1;
            float[] values = config.CaptureDeveloperValues();
            string original = LabSessionSettings.Fingerprint(values, 3, protocol);
            for (int i = 105; i <= 110; i++)
            {
                float[] changed = (float[])values.Clone();
                changed[i] = LabConfig.DeveloperFields[i].Min;
                Assert.That(LabSessionSettings.Fingerprint(changed, 3, protocol), Is.Not.EqualTo(original),
                    LabConfig.DeveloperFields[i].Label + " participates in the room settings fingerprint.");
            }
        }

        [Test]
        public void PaintGrowthSettingsReturnsASeparateEffectiveRoundPreset()
        {
            LabPaintGrowthSettings captured = config.PaintGrowthSettings;
            float[] changed = config.CaptureDeveloperValues();
            changed[105] = 0f;
            changed[106] = 3.5f;
            changed[107] = 8f;
            changed[108] = 12f;
            changed[109] = 100f;
            changed[110] = 2f;
            Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
            Assert.That(captured.Resolution, Is.EqualTo(2));
            Assert.That(captured.InitialPercent, Is.EqualTo(2f), "A captured round preset cannot follow later config changes.");
            Assert.That(captured.RepeatPercent, Is.EqualTo(4f));
            Assert.That(captured.GrowthPercentPerSecond, Is.EqualTo(6f));
            Assert.That(captured.FillChancePercent, Is.EqualTo(60f));
            Assert.That(captured.VerticalWeight, Is.EqualTo(1.35f));
            LabPaintGrowthSettings nextRound = config.PaintGrowthSettings;
            Assert.That(nextRound.Resolution, Is.Zero);
            Assert.That(nextRound.InitialPercent, Is.EqualTo(3.5f));
            Assert.That(nextRound.RepeatPercent, Is.EqualTo(8f));
            Assert.That(nextRound.GrowthPercentPerSecond, Is.EqualTo(12f));
            Assert.That(nextRound.FillChancePercent, Is.EqualTo(100f));
            Assert.That(nextRound.VerticalWeight, Is.EqualTo(2f));
        }

        [TestCase(0, 64, 32)]
        [TestCase(1, 128, 64)]
        [TestCase(2, 256, 128)]
        public void PaintSurfaceResolutionSelectsTheCapturedRoundGrid(int resolution, int columns, int rows)
        {
            float[] values = config.CaptureDeveloperValues();
            values[105] = resolution;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var surface = new LabPaintSurface(config.PaintGrowthSettings, 42u);
            Assert.That(surface.Columns, Is.EqualTo(columns));
            Assert.That(surface.Rows, Is.EqualTo(rows));
            Assert.That(surface.CellCount, Is.EqualTo(columns * rows));
            Assert.That(config.CaptureDeveloperValues()[105], Is.EqualTo(resolution));
        }

        [TestCase(105)]
        [TestCase(110)]
        public void OldOrIncompleteGrowthSettingsSchemasAreRejected(int count)
        {
            float[] previous = config.CaptureDeveloperValues().Take(count).ToArray();
            Assert.That(config.TryApplyDeveloperValues(previous, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.Throws<System.ArgumentException>(() => LabSessionSettings.Fingerprint(previous, 3, 1));
        }

        [Test]
        public void InspectorPaintGrowthValuesUseFiniteClampedSettings()
        {
            JsonUtility.FromJsonOverwrite("{\"paintSurfaceResolution\":-1,\"paintInitialPercent\":0,\"paintRepeatPercent\":200,\"paintGrowthPercentPerSecond\":0,\"paintFillChancePercent\":-1,\"paintVerticalWeight\":9}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(105).Take(6).ToArray(), Is.EqualTo(new[] { 0f, .1f, 100f, .1f, 0f, 4f }));
            JsonUtility.FromJsonOverwrite("{\"paintSurfaceResolution\":99,\"paintInitialPercent\":200,\"paintRepeatPercent\":0,\"paintGrowthPercentPerSecond\":200,\"paintFillChancePercent\":200,\"paintVerticalWeight\":0}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(105).Take(6).ToArray(), Is.EqualTo(new[] { 2f, 100f, .1f, 100f, 100f, 1f }));
        }


        [Test]
        public void CompletedGourdViewerDefaultsOnAtItsPreservedIndexWithoutChangingTheFixedGoal()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(values.Length, Is.EqualTo(146));
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
            Assert.That(values[111], Is.EqualTo(1f));
            LabDeveloperField field = LabConfig.DeveloperFields[111];
            Assert.That(field.Label, Is.EqualTo("Completed Gourd Viewer (0 off, 1 on)"));
            Assert.That(field.DisplayLabel, Is.EqualTo("Completed Gourd Viewer"));
            Assert.That(field.IsToggle, Is.True);
            Assert.That(field.WholeNumber, Is.True);
            Assert.That(new[] { field.Min, field.Max }, Is.EqualTo(new[] { 0f, 1f }));
            Assert.That(new[] { field.OffText, field.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            var gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd");
            Assert.That(gourd.FieldIndices, Does.Contain(139));
            Assert.That(gourd.FieldIndices, Does.Contain(111));
            Assert.That(LabConfig.DeveloperFields[110].Label, Is.EqualTo("Paint Vertical Weight"));
            Assert.That(LabConfig.DeveloperFields[87].Label, Is.EqualTo("Paint Victory Coverage (%)"));
            Assert.That(new[] { LabConfig.DeveloperFields[87].Min, LabConfig.DeveloperFields[87].Max },
                Is.EqualTo(new[] { 100f, 100f }));
            Assert.That(values[87], Is.EqualTo(100f));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void CompletedGourdViewerRoundTripsWithoutChangingAnyPrevious111Values(float enabled)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            values[111] = enabled;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.CompletedGourdViewerEnabled, Is.EqualTo(enabled == 1f));
            float[] after = config.CaptureDeveloperValues();
            Assert.That(after, Is.EqualTo(values));
            Assert.That(after.Take(111).ToArray(), Is.EqualTo(before.Take(111).ToArray()),
                "Appending a presentation toggle cannot change existing gameplay settings.");
            Assert.That(after.Skip(112).ToArray(), Is.EqualTo(before.Skip(112).ToArray()),
                "Changing the viewer permission cannot change any Straight profile setting.");
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
            Assert.That(config.PaintVictoryCoverage, Is.EqualTo(1f));
        }

        [TestCase(-1f)]
        [TestCase(2f)]
        [TestCase(.5f)]
        [TestCase(.9999f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidCompletedGourdViewerToggleRejectsTheWholeForm(float invalid)
        {
            float[] values = config.CaptureDeveloperValues();
            values[111] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            float[] baseline = config.CaptureDeveloperValues();
            values[0] = 777f;
            values[79] = 1f;
            values[110] = 2f;
            values[111] = invalid;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Completed Gourd Viewer"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline),
                "A bad viewer field cannot partially change earlier settings or disable the accepted toggle.");
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
        }

        [TestCase(111)]
        [TestCase(112)]
        [TestCase(118)]
        [TestCase(120)]
        [TestCase(126)]
        [TestCase(130)]
        public void PreviousOrOversizedViewerSettingsAreRejectedAtomically(int count)
        {
            float[] values = config.CaptureDeveloperValues();
            values[111] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            float[] baseline = config.CaptureDeveloperValues();
            var obsolete = new float[count];
            System.Array.Copy(baseline, obsolete, Mathf.Min(count, baseline.Length));
            obsolete[0] = 777f;
            obsolete[79] = 1f;
            Assert.That(config.TryApplyDeveloperValues(obsolete, out error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline),
                "A legacy schema must not silently append fields or apply any earlier value.");
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
            Assert.Throws<System.ArgumentException>(() => LabSessionSettings.Fingerprint(obsolete, 3, 5633));
        }

        [Test]
        public void SharedCompletedGourdViewerOnOffChangesFingerprintAndSurvivesJsonAndClientApply()
        {
            const ushort protocol = 5633;
            float[] values = config.CaptureDeveloperValues();
            values[111] = 0f;
            string offFingerprint = LabSessionSettings.Fingerprint(values, 3, protocol);
            values[111] = 1f;
            string onFingerprint = LabSessionSettings.Fingerprint(values, 3, protocol);
            Assert.That(onFingerprint, Is.Not.EqualTo(offFingerprint),
                "The room must acknowledge the Host's viewing permission with its other settings.");
            string json = JsonUtility.ToJson(new SharedModeSettings { values = values });
            float[] received = JsonUtility.FromJson<SharedModeSettings>(json).values;
            Assert.That(received.Length, Is.EqualTo(146));
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(client.TryApplyDeveloperValues(received, out string error), Is.True, error);
                Assert.That(client.CompletedGourdViewerEnabled, Is.True);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values));
                Assert.That(LabSessionSettings.Fingerprint(client.CaptureDeveloperValues(), 3, protocol),
                    Is.EqualTo(onFingerprint));
                received[111] = 0f;
                Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(client.CompletedGourdViewerEnabled, Is.False);
                Assert.That(LabSessionSettings.Fingerprint(client.CaptureDeveloperValues(), 3, protocol),
                    Is.EqualTo(offFingerprint));
            }
            finally { Object.DestroyImmediate(client); }
        }

        [Test]
        public void RopeTuningIsAtomicAndChangesTheSharedSettingsFingerprint()
        {
            float[] baseline = config.CaptureDeveloperValues();
            string original = LabSessionSettings.Fingerprint(baseline, 3, 5633);
            float[] changed = (float[])baseline.Clone();
            changed[119] = .8f;
            changed[120] = 1.8f;
            changed[121] = .6f;
            Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
            Assert.That(config.GourdRopeCycleAmplitude, Is.EqualTo(.8f));
            Assert.That(LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), 3, 5633),
                Is.Not.EqualTo(original));
            changed[122] = 101f;
            Assert.That(config.TryApplyDeveloperValues(changed, out error), Is.False);
            Assert.That(error, Does.Contain("Gourd Rope Spring"));
            Assert.That(config.CaptureDeveloperValues()[122], Is.EqualTo(baseline[122]));
        }

        [Test]
        public void SwingTuningIsSharedAndRejectsInvalidAnglesAtomically()
        {
            float[] baseline = config.CaptureDeveloperValues();
            string original = LabSessionSettings.Fingerprint(baseline, 3, 5633);
            float[] changed = (float[])baseline.Clone();
            changed[125] = 9f;
            changed[126] = 48f;
            changed[127] = 12f;
            changed[128] = .8f;
            Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
            Assert.That(config.GourdSwingMaximumAngle, Is.EqualTo(48f));
            Assert.That(LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), 3, 5633),
                Is.Not.EqualTo(original));
            changed[126] = 76f;
            Assert.That(config.TryApplyDeveloperValues(changed, out error), Is.False);
            Assert.That(error, Does.Contain("Gourd Swing Maximum Angle"));
            Assert.That(config.CaptureDeveloperValues()[126], Is.EqualTo(48f));
        }

        [Test]
        public void InspectorCompletedGourdViewerFlagUsesTheSameCapturedBoolean()
        {
            JsonUtility.FromJsonOverwrite("{\"completedGourdViewerEnabled\":true}", config);
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
            Assert.That(config.CaptureDeveloperValues()[111], Is.EqualTo(1f));
            JsonUtility.FromJsonOverwrite("{\"completedGourdViewerEnabled\":false}", config);
            Assert.That(config.CompletedGourdViewerEnabled, Is.False);
            Assert.That(config.CaptureDeveloperValues()[111], Is.Zero);
        }

        [Test]
        public void OnOffFieldsShowAsTogglesWithShortNamesButKeepTheirValuesAndLabels()
        {
            var toggles = LabConfig.DeveloperFields.Where(field => field.IsToggle).ToArray();
            Assert.That(toggles.Select(field => field.DisplayLabel), Is.EqualTo(new[]
                { "Drop Combine", "Contact Combine", "Haptics", "Gourd Mode", "Minimap", "Bomb Sound", "Completed Gourd Viewer", "Throw Form", "Paint Grain", "Paint Growth Style", "Player Paint Patterns" }));
            Assert.That(toggles.All(field => field.Min == 0f && field.Max == 1f && field.WholeNumber), Is.True,
                "A toggle is still a whole-number 0/1 value, so APPLY and room sharing are unchanged.");
            var gourd = toggles.Single(field => field.DisplayLabel == "Gourd Mode");
            Assert.That(new[] { gourd.OffText, gourd.OnText }, Is.EqualTo(new[] { "PAINT", "CRACK" }));
            Assert.That(gourd.Label, Is.EqualTo("Gourd Mode (0 paint, 1 crack)"), "The full label stays for lookups.");
            var minimap = toggles.Single(field => field.DisplayLabel == "Minimap");
            Assert.That(new[] { minimap.OffText, minimap.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            Assert.That(LabConfig.DeveloperFields.Single(field => field.Label.StartsWith("Minimap Size")).IsToggle, Is.False);
        }
    }
}
