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
            Assert.That(baseline[140], Is.EqualTo(1.4f));
            foreach (float size in new[] { .5f, 1f, 1.8f, 3f })
            {
                var values = (float[])baseline.Clone();
                values[140] = size;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(config.GourdVisualSize, Is.EqualTo(size));
                Assert.That(config.CaptureDeveloperValues().Take(140), Is.EqualTo(baseline.Take(140)));
            }
            var before = config.CaptureDeveloperValues();
            foreach (float invalid in new[] { .49f, 3.01f, float.NaN, float.PositiveInfinity })
            {
                var values = (float[])before.Clone();
                values[140] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
            }
        }

        [Test]
        public void DeveloperFormCoversEveryTuningValueAndAppliesValidChanges()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(249));
            Assert.That(values[89], Is.EqualTo(50f), "The fuse sizzle starts at the Inspector volume.");
            Assert.That(new[] { values[90], values[91], values[92], values[93] }, Is.EqualTo(new[] { 20f, 40f, 60f, 80f }),
                "A handed-on drum stops 20-40, 40-60 or 60-80% of the board in from where it entered.");
            Assert.That(new[] { values[94], values[95], values[96], values[97] }, Is.EqualTo(new[] { 40f, 35f, 15f, 10f }),
                "Near, middle, far and pass-through chances.");
            Assert.That(new[] { values[205], values[206] }, Is.EqualTo(new[] { 10f, 20f }),
                "A common gimmick every 10 seconds and the yokai's own every 20 seconds by default (#311).");
            Assert.That(values[79], Is.EqualTo(0f), "The gourd starts in paint mode.");
            Assert.That(values[98], Is.EqualTo(1f), "The minimap is on by default.");
            Assert.That(values[99], Is.EqualTo(50f), "The minimap starts at half of its drawn size.");
            Assert.That(new[] { values[100], values[101] }, Is.EqualTo(new[] { 0f, 0f }), "The minimap starts in its usual place.");
            Assert.That(values[102], Is.EqualTo(50f), "The bomb sound starts at the Inspector volume.");
            Assert.That(config.BombSoundVolume, Is.EqualTo(.9f).Within(1e-5f));
            Assert.That(values[103], Is.EqualTo(1f), "The bomb plays the popcorn-machine ppeong by default.");
            Assert.That(values.Skip(118).Take(6).ToArray(),
                Is.EqualTo(new[] { .45f, 3f, .32f, 42f, 2f, .65f }),
                "The gourd rope has a slow cycle and a lively hit spring by default.");
            Assert.That(values.Skip(124).Take(4).ToArray(), Is.EqualTo(new[] { 12f, 40f, 10f, 1.4f }));
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
            Assert.That(values[55], Is.EqualTo(3f), "The retired bomb damage multiplier keeps its slot (#328).");
            Assert.That(values[56], Is.EqualTo(.8f), "The camera looks 0.8 units above the target centre in the new UI composition.");
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
            Assert.That(values[74], Is.EqualTo(0f), "Drum Count is AUTO by default: the drums follow the player count (#236).");
            values[74] = 3f;
            Assert.That(values[75], Is.EqualTo(.6f), "The drum keeps the old generate button's size.");
            values[75] = .8f;
            Assert.That(new[] { values[76], values[77], values[78] }, Is.EqualTo(new[] { .1f, .25f, 1f }),
                "The drum drifts slowly, turns gently and spins at full strength by default.");
            values[76] = .2f;
            values[77] = .5f;
            values[78] = 2f;
            values[79] = 0f; // Retired mode slot stays fixed at paint-only zero.
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
            values[89] = 100f; // fuse sizzle twice the Inspector volume
            values[90] = 10f; values[91] = 30f; values[92] = 50f; values[93] = 90f; // drum stop zone edges
            values[94] = 25f; values[95] = 25f; values[96] = 25f; values[97] = 25f; // drum stop chances
            values[98] = 0f; // minimap off
            values[99] = 80f; // minimap bigger than the default 50
            values[100] = -120f; // 120 to the left
            values[101] = 60f; // 60 up
            values[102] = 25f; // bomb sound half the Inspector volume
            values[103] = 0f; // bomb sound: boom
            values[104] = 0f; // low paint resolution
            values[105] = 3f; // initial patch coverage
            values[106] = 5f; // same-region hit expands its target area
            values[107] = 8f; // growth per second
            values[108] = 75f; // frontier fill chance
            values[109] = 2f; // vertical preference
            Assert.That(values[110], Is.EqualTo(1f), "Completed-gourd viewing is on by default.");
            values[110] = 1f; // allow the room to inspect completed Paint results locally
            values[118] = .6f; // automatic rope travel
            values[119] = 2.5f; // cycle duration
            values[120] = .55f; // impact bounce
            values[121] = 60f; // spring
            values[122] = 1.5f; // damping
            values[123] = .9f; // maximum hit extension
            values[124] = 8f; // swing impact
            values[125] = 45f; // swing angle limit
            values[126] = 12f; // swing restoring force
            values[127] = 1.8f; // swing damping
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.MinimapEnabled, Is.False, "DEV can turn the minimap off.");
            Assert.That(config.MinimapSizePercent, Is.EqualTo(80), "DEV sizes the minimap from 0 to 100 with 50 in the middle.");
            Assert.That(config.MinimapMove, Is.EqualTo(new Vector2(-120f, 60f)), "DEV can move the minimap left/right and up/down.");
            Assert.That(config.BombSoundVolume, Is.EqualTo(.45f).Within(1e-5f), "25 halves the Inspector bomb volume (0.9).");
            Assert.That(config.BombSoundStyle, Is.EqualTo(LabBombSoundStyle.Boom), "DEV switches the bomb to the ordinary boom.");
            Assert.That(config.FuseSoundVolume, Is.EqualTo(1f).Within(1e-5f), "100 doubles the Inspector fuse volume (0.5).");
            Assert.That(config.DrumStopBounds(), Is.EqualTo(new[] { .1f, .3f, .5f, .9f }).Within(1e-6f));
            Assert.That(config.DrumStopChances(), Is.EqualTo(new[] { 25, 25, 25, 25 }));
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            Assert.That(config.CombineSoundVolume, Is.EqualTo(0f));
            Assert.That(config.CombineHapticStyle, Is.EqualTo(LabHapticStyle.Soft));
            Assert.That(config.CombineHapticIntensity, Is.EqualTo(.1f));
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
            Assert.That(config.PaintSurfaceResolution, Is.Zero);
            Assert.That(config.PaintInitialPercent, Is.EqualTo(3f));
            Assert.That(config.PaintRepeatPercent, Is.EqualTo(5f));
            Assert.That(config.PaintGrowthPercentPerSecond, Is.EqualTo(8f));
            Assert.That(config.PaintFillChancePercent, Is.EqualTo(75f));
            Assert.That(config.PaintVerticalWeight, Is.EqualTo(2f));
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values), "All current fields round-trip in wire order.");
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
            // #305: one orb's worth comes back every second, so a player waiting for orbs or the drum keeps
            // something to do. #328: a full bar makes six orbs, and every stage battles for two minutes.
            Assert.That(saved.StaminaMax, Is.EqualTo(120f));
            Assert.That(saved.StaminaStart, Is.EqualTo(120f));
            Assert.That(saved.BattleDurationSeconds, Is.EqualTo(120f));
            Assert.That(saved.GenerateCost, Is.EqualTo(20f));
            Assert.That(saved.StaminaRecoveryAmount, Is.EqualTo(20f));
            Assert.That(saved.StaminaRecoverySeconds, Is.EqualTo(1f));
            Assert.That(saved.HitRecovery, Is.EqualTo(10f), "A hit returns half an orb.");
            Assert.That(saved.OrbLifetimeSeconds, Is.EqualTo(15f));
        }

        [Test]
        public void SavedStaminaBarDrawsOneCellPerOrbAndEachOrbEndsInAGap()
        {
            var saved = Resources.Load<LabConfig>("LabConfig");
            int orbs = Mathf.RoundToInt(saved.StaminaMax / saved.GenerateCost);
            var mask = new Texture2D(2, 2);
            try
            {
                Assert.That(mask.LoadImage(System.IO.File.ReadAllBytes("Assets/Lab/Art/UI/StaminaBarFillMask.png")), Is.True);
                // Opaque spans across the cells' middle row: one per cell.
                int row = mask.height / 2;
                var cells = new System.Collections.Generic.List<Vector2Int>();
                for (int x = 0, start = -1; x <= mask.width; x++)
                {
                    bool opaque = x < mask.width && mask.GetPixel(x, row).a > .5f;
                    if (opaque && start < 0) start = x;
                    if (!opaque && start >= 0) { cells.Add(new Vector2Int(start, x - 1)); start = -1; }
                }
                Assert.That(cells.Count, Is.EqualTo(orbs), "The bar shows as many cells as a full bar makes orbs (#305).");
                // The fill grows left to right, so each whole orb's share must stop between two cells.
                for (int k = 1; k < orbs; k++)
                {
                    float edge = mask.width * k / (float)orbs;
                    Assert.That(edge, Is.GreaterThan(cells[k - 1].y).And.LessThan(cells[k].x), "Orb " + k);
                }
            }
            finally { Object.DestroyImmediate(mask); }
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
                LabDeveloperField field = LabConfig.DeveloperFields[90 + i];
                Assert.That(field.Label, Is.EqualTo(labels[i]));
                Assert.That(field.Min, Is.EqualTo(0f));
                Assert.That(field.Max, Is.EqualTo(100f));
                Assert.That(field.WholeNumber, Is.True);
            }
            int[] drumIndices = null;
            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
                if (category.Title == "Drum") drumIndices = category.FieldIndices;
            Assert.That(drumIndices, Is.EqualTo(new[] { 74, 75, 76, 77, 78, 90, 91, 92, 93, 94, 95, 96, 97 }));
            Assert.That(config.DrumStopBounds(), Is.EqualTo(new[] { .2f, .4f, .6f, .8f }).Within(1e-6f));
            Assert.That(config.DrumStopChances(), Is.EqualTo(new[] { 40, 35, 15, 10 }));

            float[] values = config.CaptureDeveloperValues();
            values[0] = 777f;
            values[91] = 70f; // near ends after middle ends
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("Drum stop zone"));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(1000), "A rejected form must not partially apply.");

            values[91] = 40f;
            values[97] = 11f; // total 101
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
            Assert.That(saved.CommonGimmickInterval, Is.EqualTo(10f), "A common gimmick every 10 s (#311)...");
            Assert.That(saved.GimmickSwitches()[(int)LabSabotageKind.GourdBomb], Is.True, "...which may be the gourd's bomb.");
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
        public void DrumCountIsAutoOrAWholeNumberFromOneToFive()
        {
            LabDeveloperField field = LabConfig.DeveloperFields[74];
            Assert.That(field.IsChoice, Is.True, "Drum Count is picked with buttons (#236).");
            Assert.That(field.Choices, Is.EqualTo(new[] { "AUTO", "1", "2", "3", "4", "5" }),
                "Each button's value is the count it names; AUTO is 0.");
            Assert.That(LabConfig.AutoDrumCount, Is.Zero);
            foreach (float invalid in new[] { -1f, 6f, 2.5f })
            {
                float[] values = config.CaptureDeveloperValues();
                values[74] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False, invalid.ToString());
                Assert.That(error, Does.Contain("Drum Count"));
            }
            Assert.That(config.DrumCount, Is.EqualTo(LabConfig.AutoDrumCount));

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            Assert.That(authored.DrumCount, Is.EqualTo(LabConfig.AutoDrumCount),
                "Set Drum Count to 0 (AUTO) in the LabConfig Inspector (#236).");

            Assert.That(Enumerable.Range(1, 5).Select(LabConfig.DrumsForPlayers), Is.EqualTo(new[] { 1, 1, 1, 2, 2 }),
                "AUTO: one drum for up to three players (and solo), two for four or five.");
            Assert.That(Enumerable.Range(1, 5).Select(config.DrumsFor), Is.EqualTo(new[] { 1, 1, 1, 2, 2 }));
            for (int count = 1; count <= 5; count++)
            {
                float[] values = config.CaptureDeveloperValues();
                values[74] = count;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(config.DrumCount, Is.EqualTo(count));
                Assert.That(Enumerable.Range(1, 5).Select(config.DrumsFor), Is.All.EqualTo(count),
                    "A fixed count is used whatever the number of players.");
            }
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

            // Retired rows (#311) keep their index in the shared form but have no row.
            for (int i = 0; i < fieldCount; i++)
                Assert.That(seen[i], Is.EqualTo(LabConfig.RetiredDeveloperFields.Contains(i) ? 0 : 1),
                    LabConfig.DeveloperFields[i].Label + " (" + i + ") must be in exactly one category unless retired.");
        }


        [System.Serializable]
        private sealed class SharedModeSettings
        {
            public float[] values;
        }

        [Test]
        public void RetiredGourdModeKeepsIndex79FixedWithoutShiftingAnyTuningValue()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(249));
            Assert.That(LabConfig.DeveloperFields[78].Label, Is.EqualTo("Drum Spin Strength"));
            Assert.That(LabConfig.DeveloperFields[80].Label, Is.EqualTo("Bomb Blast Radius (board widths)"));
            LabDeveloperField field = LabConfig.DeveloperFields[79];
            Assert.That(field.Label, Is.EqualTo("Reserved Gourd Mode (paint only)"));
            Assert.That(field.Min, Is.Zero);
            Assert.That(field.Max, Is.Zero);
            Assert.That(field.WholeNumber, Is.True);
            Assert.That(field.IsToggle, Is.False, "The removed mode is not an editable on/off choice.");
            Assert.That(System.Enum.GetValues(typeof(LabGourdMode)), Is.EqualTo(new[] { LabGourdMode.Paint }));
            float[] original = config.CaptureDeveloperValues();
            Assert.That(original[79], Is.Zero);
            Assert.That(config.TryApplyDeveloperValues(original, out string error), Is.True, error);
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(original),
                "The reserved wire position preserves both earlier and later settings verbatim.");
        }

        [Test]
        public void NonzeroOrInvalidRetiredGourdSlotRejectsEveryOtherChangeAtomically()
        {
            float[] original = config.CaptureDeveloperValues();
            foreach (float invalid in new[] { -1f, 1f, 2f, .5f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                float[] values = (float[])original.Clone();
                values[0] = 240f;
                values[80] = .6f;
                values[79] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False, invalid.ToString());
                Assert.That(error, Does.Contain("Reserved Gourd Mode"));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(original),
                    "A legacy removed-mode value must not partially change any accepted tuning.");
                Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
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
        public void PaintOnlySharedSchemaRoundTripsAndRejectsALegacyNonzeroMode()
        {
            float[] values = config.CaptureDeveloperValues();
            const ushort protocol = 1;
            string acceptedFingerprint = LabSessionSettings.Fingerprint(values, 3, protocol);
            string json = JsonUtility.ToJson(new SharedModeSettings { values = values });
            float[] received = JsonUtility.FromJson<SharedModeSettings>(json).values;
            Assert.That(received.Length, Is.EqualTo(249));
            Assert.That(received[79], Is.Zero);
            Assert.That(LabSessionSettings.Fingerprint(received, 3, protocol), Is.EqualTo(acceptedFingerprint));
            var clientConfig = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(clientConfig.TryApplyDeveloperValues(received, out string error), Is.True, error);
                Assert.That(clientConfig.GourdMode, Is.EqualTo(LabGourdMode.Paint));
                Assert.That(clientConfig.CaptureDeveloperValues(), Is.EqualTo(values));
                received[79] = 1f;
                Assert.That(LabSessionSettings.Fingerprint(received, 3, protocol), Is.Not.EqualTo(acceptedFingerprint),
                    "An old removed-mode value cannot acknowledge the accepted paint-only profile.");
                Assert.That(clientConfig.TryApplyDeveloperValues(received, out error), Is.False);
                Assert.That(error, Does.Contain("Reserved Gourd Mode"));
                Assert.That(clientConfig.CaptureDeveloperValues(), Is.EqualTo(values));
            }
            finally { Object.DestroyImmediate(clientConfig); }
        }

        [Test]
        public void RetiredInspectorGourdFieldCannotRestoreARemovedMode()
        {
            JsonUtility.FromJsonOverwrite("{\"gourdMode\":999}", config);
            Assert.That(config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            Assert.That(config.CaptureDeveloperValues()[79], Is.Zero);
        }

        [Test]
        public void ThrowFormFollowsTheRetainedPreviousFieldsInSchemaOrder()
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
                "Reserved Gourd Mode (paint only)",
                "Bomb Blast Radius (board widths)",
                "Bomb Blast Strength (board widths/s)",
                "Bomb Haptic Style (0-4)",
                "Bomb Haptic Intensity",
                "Combine Sound Volume (50 = Inspector)",
                "Combine Haptic Style (0-4)",
                "Combine Haptic Intensity",
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
            Assert.That(previousLabels.Length, Is.EqualTo(111));
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(249));
            for (int i = 0; i < previousLabels.Length; i++)
                Assert.That(LabConfig.DeveloperFields[i].Label, Is.EqualTo(previousLabels[i]), "Index " + i);
            Assert.That(LabConfig.DefaultPaintVictoryPercent, Is.EqualTo(100));
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
            Assert.That(LabConfig.DeveloperFields.Any(field => field.Label == "Paint Victory Coverage (%)"), Is.False,
                "The fixed 100% game rule has no editable DEV row.");
            int[] gourdIndices = null;
            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
                if (category.Title == "Gourd") gourdIndices = category.FieldIndices;
            Assert.That(gourdIndices, Is.Not.Null);
            Assert.That(gourdIndices, Does.Contain(79));
        }

        [Test]
        public void RetiredGimmickTimersKeepTheirIndicesButNoRowAndNothingReadsThem()
        {
            // The gimmick groups (#311) replaced the gourd's bomb and block timers and the sabotage interval.
            // #328 adds the bomb damage multiplier (bombs open instead of being thrown) and the Yaguang's own battle time.
            Assert.That(LabConfig.RetiredDeveloperFields, Is.EqualTo(new[] { 55, 87, 88, 152, 154, 167, 168, 211, 230, 231, 232, 233, 234 }));
            Assert.That(LabConfig.RetiredDeveloperFields.Select(index => LabConfig.DeveloperFields[index].Label), Is.EqualTo(new[]
            {
                "Bomb Damage Multiplier",
                "Gourd Bomb Interval Min (seconds, 0 = off)", "Gourd Bomb Interval Max (seconds, 0 = off)",
                "Swipe Block Interval (seconds, 0 = off)", "Swipe Block Bomb Gap (seconds)",
                "Sabotage Interval Min (seconds)", "Sabotage Interval Max (seconds)",
                "야광귀 시간표 전투 시간 (초)",
                "지귀 불 활성 수명 (초)", "지귀 근원 반경 (표면 각도)", "지귀 불 패치 반경 (표면 각도)",
                "지귀 번짐 간격 (초)", "지귀 그룹 패치 수"
            }), "They keep their indices so the shared form and every later field keep their places.");
            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
                Assert.That(category.FieldIndices.Intersect(LabConfig.RetiredDeveloperFields), Is.Empty, category.Title);
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd").FieldIndices,
                Is.EqualTo(new[] { 140, 79, 104, 105, 106, 138, 139, 107, 108, 109, 110, 137, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 134, 132, 133, 148, 149, 150, 151, 200, 201 }));

            float[] values = config.CaptureDeveloperValues();
            values[87] = 5f; values[88] = 6f; values[152] = 0f; values[154] = 1f; values[167] = 2f; values[168] = 3f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values), "Carried unchanged, so every peer agrees on the form.");
            Assert.That(new[] { config.CommonGimmickInterval, config.UniqueGimmickInterval }, Is.EqualTo(new[] { 10f, 20f }));
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
            values[105] = 3.5f;
            values[106] = 8f;
            values[107] = 12f;
            values[108] = 100f;
            values[109] = 2f;
            string changed = LabSessionSettings.Fingerprint(values, 3, fixtureProtocol);
            Assert.That(changed, Is.Not.EqualTo(first), "Participants must acknowledge the round's growth settings.");
            string json = JsonUtility.ToJson(new SharedModeSettings { values = values });
            float[] received = JsonUtility.FromJson<SharedModeSettings>(json).values;
            var clientConfig = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(clientConfig.TryApplyDeveloperValues(received, out string error), Is.True, error);
                Assert.That(clientConfig.PaintVictoryPercent, Is.EqualTo(100));
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
                Assert.That(LabConfig.DeveloperFields[104 + i].Label, Is.EqualTo(labels[i]), "Appended index " + (105 + i));
            Assert.That(config.CaptureDeveloperValues().Skip(104).Take(6).ToArray(),
                Is.EqualTo(new[] { 2f, 2f, 1.5f, 6f, 60f, 1.35f }));
            Assert.That(LabConfig.DeveloperFields[104].WholeNumber, Is.True);
            for (int i = 105; i <= 109; i++) Assert.That(LabConfig.DeveloperFields[i].WholeNumber, Is.False);
            var gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd");
            Assert.That(gourd.FieldIndices, Is.EqualTo(new[] { 140, 79, 104, 105, 106, 138, 139, 107, 108, 109, 110, 137, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 134, 132, 133, 148, 149, 150, 151, 200, 201 }));
        }

        [TestCase(104, 0f, 2f)]
        [TestCase(105, .1f, 100f)]
        [TestCase(106, .1f, 100f)]
        [TestCase(107, .1f, 100f)]
        [TestCase(108, 0f, 100f)]
        [TestCase(109, 1f, 4f)]
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
            if (index == 104)
            {
                float[] values = (float[])baseline.Clone();
                values[104] = .5f;
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
            for (int i = 104; i <= 109; i++)
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
            changed[104] = 0f;
            changed[105] = 3.5f;
            changed[106] = 8f;
            changed[107] = 12f;
            changed[108] = 100f;
            changed[109] = 2f;
            Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
            Assert.That(captured.Resolution, Is.EqualTo(2));
            Assert.That(captured.InitialPercent, Is.EqualTo(2f), "A captured round preset cannot follow later config changes.");
            Assert.That(captured.RepeatPercent, Is.EqualTo(1.5f));
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
            values[104] = resolution;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var surface = new LabPaintSurface(config.PaintGrowthSettings, 42u);
            Assert.That(surface.Columns, Is.EqualTo(columns));
            Assert.That(surface.Rows, Is.EqualTo(rows));
            Assert.That(surface.CellCount, Is.EqualTo(columns * rows));
            Assert.That(config.CaptureDeveloperValues()[104], Is.EqualTo(resolution));
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
            Assert.That(config.CaptureDeveloperValues().Skip(104).Take(6).ToArray(), Is.EqualTo(new[] { 0f, .1f, 100f, .1f, 0f, 4f }));
            JsonUtility.FromJsonOverwrite("{\"paintSurfaceResolution\":99,\"paintInitialPercent\":200,\"paintRepeatPercent\":0,\"paintGrowthPercentPerSecond\":200,\"paintFillChancePercent\":200,\"paintVerticalWeight\":0}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(104).Take(6).ToArray(), Is.EqualTo(new[] { 2f, 100f, .1f, 100f, 100f, 1f }));
        }


        [Test]
        public void CompletedGourdViewerDefaultsOnAtItsPreservedIndexWithoutChangingTheFixedGoal()
        {
            float[] values = config.CaptureDeveloperValues();
            Assert.That(values.Length, Is.EqualTo(249));
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
            Assert.That(values[110], Is.EqualTo(1f));
            LabDeveloperField field = LabConfig.DeveloperFields[110];
            Assert.That(field.Label, Is.EqualTo("Completed Gourd Viewer (0 off, 1 on)"));
            Assert.That(field.DisplayLabel, Is.EqualTo("Completed Gourd Viewer"));
            Assert.That(field.IsToggle, Is.True);
            Assert.That(field.WholeNumber, Is.True);
            Assert.That(new[] { field.Min, field.Max }, Is.EqualTo(new[] { 0f, 1f }));
            Assert.That(new[] { field.OffText, field.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            var gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd");
            Assert.That(gourd.FieldIndices, Does.Contain(134));
            Assert.That(gourd.FieldIndices, Does.Contain(110));
            Assert.That(LabConfig.DeveloperFields[109].Label, Is.EqualTo("Paint Vertical Weight"));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void CompletedGourdViewerRoundTripsWithoutChangingAnyPrevious110Values(float enabled)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            values[110] = enabled;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.CompletedGourdViewerEnabled, Is.EqualTo(enabled == 1f));
            float[] after = config.CaptureDeveloperValues();
            Assert.That(after, Is.EqualTo(values));
            Assert.That(after.Take(110).ToArray(), Is.EqualTo(before.Take(110).ToArray()),
                "Appending a presentation toggle cannot change existing gameplay settings.");
            Assert.That(after.Skip(111).ToArray(), Is.EqualTo(before.Skip(111).ToArray()),
                "Changing the viewer permission cannot change any Straight profile setting.");
            Assert.That(config.PaintVictoryPercent, Is.EqualTo(100));
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
            values[110] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            float[] baseline = config.CaptureDeveloperValues();
            values[0] = 777f;
            values[79] = 0f;
            values[109] = 2f;
            values[110] = invalid;
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
            values[110] = 1f;
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
            values[110] = 0f;
            string offFingerprint = LabSessionSettings.Fingerprint(values, 3, protocol);
            values[110] = 1f;
            string onFingerprint = LabSessionSettings.Fingerprint(values, 3, protocol);
            Assert.That(onFingerprint, Is.Not.EqualTo(offFingerprint),
                "The room must acknowledge the Host's viewing permission with its other settings.");
            string json = JsonUtility.ToJson(new SharedModeSettings { values = values });
            float[] received = JsonUtility.FromJson<SharedModeSettings>(json).values;
            Assert.That(received.Length, Is.EqualTo(249));
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(client.TryApplyDeveloperValues(received, out string error), Is.True, error);
                Assert.That(client.CompletedGourdViewerEnabled, Is.True);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values));
                Assert.That(LabSessionSettings.Fingerprint(client.CaptureDeveloperValues(), 3, protocol),
                    Is.EqualTo(onFingerprint));
                received[110] = 0f;
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
            changed[118] = .8f;
            changed[119] = 1.8f;
            changed[120] = .6f;
            Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
            Assert.That(config.GourdRopeCycleAmplitude, Is.EqualTo(.8f));
            Assert.That(LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), 3, 5633),
                Is.Not.EqualTo(original));
            changed[121] = 101f;
            Assert.That(config.TryApplyDeveloperValues(changed, out error), Is.False);
            Assert.That(error, Does.Contain("Gourd Rope Spring"));
            Assert.That(config.CaptureDeveloperValues()[121], Is.EqualTo(baseline[121]));
        }

        [Test]
        public void SwingTuningIsSharedAndRejectsInvalidAnglesAtomically()
        {
            float[] baseline = config.CaptureDeveloperValues();
            string original = LabSessionSettings.Fingerprint(baseline, 3, 5633);
            float[] changed = (float[])baseline.Clone();
            changed[124] = 9f;
            changed[125] = 48f;
            changed[126] = 12f;
            changed[127] = .8f;
            Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
            Assert.That(config.GourdSwingMaximumAngle, Is.EqualTo(48f));
            Assert.That(LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), 3, 5633),
                Is.Not.EqualTo(original));
            changed[125] = 76f;
            Assert.That(config.TryApplyDeveloperValues(changed, out error), Is.False);
            Assert.That(error, Does.Contain("Gourd Swing Maximum Angle"));
            Assert.That(config.CaptureDeveloperValues()[125], Is.EqualTo(48f));
        }

        [Test]
        public void InspectorCompletedGourdViewerFlagUsesTheSameCapturedBoolean()
        {
            JsonUtility.FromJsonOverwrite("{\"completedGourdViewerEnabled\":true}", config);
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
            Assert.That(config.CaptureDeveloperValues()[110], Is.EqualTo(1f));
            JsonUtility.FromJsonOverwrite("{\"completedGourdViewerEnabled\":false}", config);
            Assert.That(config.CompletedGourdViewerEnabled, Is.False);
            Assert.That(config.CaptureDeveloperValues()[110], Is.Zero);
        }

        [Test]
        public void OnOffFieldsShowAsTogglesWithShortNamesButKeepTheirValuesAndLabels()
        {
            var toggles = LabConfig.DeveloperFields.Where(field => field.IsToggle).ToArray();
            Assert.That(toggles.Select(field => field.DisplayLabel), Is.EqualTo(new[]
                { "Drop Combine", "Contact Combine", "Haptics", "Minimap", "Bomb Sound", "Completed Gourd Viewer", "Throw Form", "Paint Grain", "Paint Growth Style", "Player Paint Patterns", "Paint Drying", "Gourd Color Combo", "Combo Penalty Target", "Board Orb Trail", "Lab Unique Gimmicks", "야광귀 섬광", "섬광 대상", "야광귀 셈법", "2:2 땅따먹기", "High Resolution Paint", "섬광 흔들어 풀기", "Gimmick Gourd Bomb", "Gimmick Swipe Block", "Gimmick Fox Fire", "Gimmick Charm", "Raw Streak Correction", "지귀 120초 개인 발화·전원 소화" }));
            Assert.That(toggles.All(field => field.Min == 0f && field.Max == 1f && field.WholeNumber), Is.True,
                "A toggle is still a whole-number 0/1 value, so APPLY and room sharing are unchanged.");
            Assert.That(toggles.Any(field => field.Label.Contains("Gourd Mode")), Is.False,
                "The retired mode is reserved data, not a visible PAINT/CRACK control.");
            Assert.That(LabConfig.DeveloperFields[79].Max, Is.Zero);
            var minimap = toggles.Single(field => field.DisplayLabel == "Minimap");
            Assert.That(new[] { minimap.OffText, minimap.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            var drying = toggles.Single(field => field.DisplayLabel == "Paint Drying");
            Assert.That(drying.Label, Is.EqualTo("Paint Drying (0 off, 1 on)"));
            Assert.That(new[] { drying.OffText, drying.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            var combo = toggles.Single(field => field.DisplayLabel == "Gourd Color Combo");
            Assert.That(new[] { combo.OffText, combo.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            var comboTarget = toggles.Single(field => field.DisplayLabel == "Combo Penalty Target");
            Assert.That(new[] { comboTarget.OffText, comboTarget.OnText }, Is.EqualTo(new[] { "TEAM", "THROWER" }));
            var boardTrail = toggles.Single(field => field.DisplayLabel == "Board Orb Trail");
            Assert.That(boardTrail.Label, Is.EqualTo("Board Orb Trail (0 off, 1 on)"));
            Assert.That(new[] { boardTrail.OffText, boardTrail.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            Assert.That(LabConfig.DeveloperFields.Single(field => field.Label.StartsWith("Minimap Size")).IsToggle, Is.False);
        }

        [Test]
        public void HeavyBombFieldsJoinTheItemOrbsGroupWithTheirDefaultsAndApply()
        {
            LabDeveloperField[] heavy = LabConfig.DeveloperFields.Skip(141).Take(4).ToArray();
            Assert.That(heavy.Select(field => field.Label), Is.EqualTo(new[]
            {
                "Bomb Drag Lag (seconds, 0 = sticks to finger)", "Bomb Release Speed (x orb)",
                "Bomb Slide Deceleration (x orb)", "Bomb Mass (x orb)"
            }));
            Assert.That(heavy.Select(field => field.Min), Is.EqualTo(new[] { 0f, .05f, 1f, 1f }));
            Assert.That(heavy.Select(field => field.Max), Is.EqualTo(new[] { 1f, 1f, 5f, 20f }));
            Assert.That(heavy.Any(field => field.WholeNumber || field.IsToggle || field.IsChoice), Is.False);
            int[] items = LabConfig.DeveloperCategories.Single(category => category.Title == "Item orbs").FieldIndices;
            Assert.That(items.SkipWhile(index => index != 141).Take(4), Is.EqualTo(new[] { 141, 142, 143, 144 }),
                "They sit with the other item settings.");
            Assert.That(LabConfig.DeveloperCategories.Sum(category => category.FieldIndices.Count(index => index >= 141 && index <= 144)),
                Is.EqualTo(4), "Each shows once.");

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
                Assert.That(source.CaptureDeveloperValues().Skip(141).Take(4), Is.EqualTo(new[] { .12f, .75f, 1.3f, 4f }),
                    "Lighter release and slide since #237, so a bomb can be passed on; still heavy to drag and bump");

            float[] values = config.CaptureDeveloperValues();
            float[] before = (float[])values.Clone();
            values[141] = 0f;
            values[142] = .25f;
            values[143] = 3.5f;
            values[144] = 10f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(new[] { config.BombDragLag, config.BombReleaseSpeed, config.BombSlideDeceleration, config.BombMass },
                Is.EqualTo(new[] { 0f, .25f, 3.5f, 10f }));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            Assert.That(config.CaptureDeveloperValues().Take(141), Is.EqualTo(before.Take(141)),
                "Earlier settings keep their places and values.");

            values[142] = .01f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Bomb Release Speed"));
            Assert.That(config.BombReleaseSpeed, Is.EqualTo(.25f), "A rejected form changes nothing.");

            JsonUtility.FromJsonOverwrite("{\"bombDragLag\":-1,\"bombReleaseSpeed\":3,\"bombSlideDeceleration\":0,\"bombMass\":99}", config);
            Assert.That(new[] { config.BombDragLag, config.BombReleaseSpeed, config.BombSlideDeceleration, config.BombMass },
                Is.EqualTo(new[] { 0f, 1f, 1f, 20f }), "Inspector values outside the ranges are clamped.");
        }

        [Test]
        public void BlastPenaltyFieldsJoinTheItemOrbsGroupWithTheirDefaultsAndApply()
        {
            LabDeveloperField[] penalty = LabConfig.DeveloperFields.Skip(145).Take(3).ToArray();
            Assert.That(penalty.Select(field => field.Label), Is.EqualTo(new[]
            {
                "Blast Burn Seconds", "Blast Shrink Scale (x radius)", "Small Orb Paint Share (x paint)"
            }));
            Assert.That(penalty.Select(field => field.Min), Is.EqualTo(new[] { 3f, .2f, 0f }));
            Assert.That(penalty.Select(field => field.Max), Is.EqualTo(new[] { 9f, 1f, 1f }), "#326: the fire burns 6 s, in the middle of 3–9.");
            Assert.That(penalty.Any(field => field.WholeNumber || field.IsToggle || field.IsChoice), Is.False);
            int[] items = LabConfig.DeveloperCategories.Single(category => category.Title == "Item orbs").FieldIndices;
            Assert.That(items.Skip(items.Length - 3), Is.EqualTo(new[] { 145, 146, 147 }),
                "They follow the heavy bomb settings.");
            Assert.That(LabConfig.DeveloperCategories.Sum(category => category.FieldIndices.Count(index => index >= 145 && index <= 147)),
                Is.EqualTo(3), "Each shows once.");

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
                Assert.That(source.CaptureDeveloperValues().Skip(145).Take(3), Is.EqualTo(new[] { 6f, .5f, .5f }));

            float[] values = config.CaptureDeveloperValues();
            float[] before = (float[])values.Clone();
            values[145] = 3.5f;
            values[146] = .25f;
            values[147] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(new[] { config.BlastBurnSeconds, config.BlastShrinkScale, config.SmallOrbPaintShare },
                Is.EqualTo(new[] { 3.5f, .25f, 0f }));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            Assert.That(config.CaptureDeveloperValues().Take(145), Is.EqualTo(before.Take(145)),
                "Earlier settings keep their places and values.");

            values[145] = 2f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Blast Burn Seconds"));
            Assert.That(config.BlastBurnSeconds, Is.EqualTo(3.5f), "A rejected form changes nothing.");

            JsonUtility.FromJsonOverwrite("{\"blastBurnSeconds\":12,\"blastShrinkScale\":0,\"smallOrbPaintShare\":-1}", config);
            Assert.That(new[] { config.BlastBurnSeconds, config.BlastShrinkScale, config.SmallOrbPaintShare },
                Is.EqualTo(new[] { 9f, .2f, 0f }), "Inspector values outside the ranges are clamped.");
        }

        [Test]
        public void SwipeBlockSecondsSitWithTheCommonGimmicksAndApply()
        {
            LabDeveloperField seconds = LabConfig.DeveloperFields[153];
            Assert.That((seconds.Label, seconds.Min, seconds.Max), Is.EqualTo(("Swipe Block Seconds", 1f, 60f)));
            Assert.That(seconds.WholeNumber || seconds.IsToggle || seconds.IsChoice, Is.False);
            int[] gimmicks = LabConfig.DeveloperCategories.Single(category => category.Title == "Gimmicks").FieldIndices;
            Assert.That(gimmicks.Take(6), Is.EqualTo(new[] { 205, 207, 169, 208, 170, 153 }),
                "How long a block lasts sits right after the block's switch and chance (#311).");
            Assert.That(LabConfig.DeveloperCategories.Sum(category => category.FieldIndices.Count(index => index == 153)),
                Is.EqualTo(1), "It shows once.");

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
                Assert.That(source.CaptureDeveloperValues()[153], Is.EqualTo(10f));

            float[] values = config.CaptureDeveloperValues();
            values[153] = 25f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.SwipeBlockSeconds, Is.EqualTo(25f));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));

            values[153] = .5f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Swipe Block Seconds"));
            Assert.That(config.SwipeBlockSeconds, Is.EqualTo(25f), "A rejected form changes nothing.");

            JsonUtility.FromJsonOverwrite("{\"swipeBlockSeconds\":99}", config);
            Assert.That(config.SwipeBlockSeconds, Is.EqualTo(60f), "Inspector values outside the range are clamped.");
        }

        [Test]
        public void GourdColorComboFieldsFormTheComboGroupAndStartOff()
        {
            LabDeveloperField[] combo = LabConfig.DeveloperFields.Skip(155).Take(10).ToArray();
            Assert.That(combo.Select(field => field.Label), Is.EqualTo(new[]
            {
                "Gourd Color Combo (0 off, 1 on)", "Combo First Start (seconds)",
                "Combo Repeat Interval (seconds)", "Combo Time Limit (seconds)",
                "Combo Penalty Duration (seconds)", "Combo Penalty Size (x)",
                "Combo Penalty Target (0 team, 1 thrower)",
                "Combo Reward Duration (seconds)", "Combo Reward Size (x)",
                "Combo Warning (seconds)"
            }));
            Assert.That(combo[0].IsToggle, Is.True);
            Assert.That(combo[6].IsToggle, Is.True);
            Assert.That(new[] { combo[6].OffText, combo[6].OnText }, Is.EqualTo(new[] { "TEAM", "THROWER" }));
            // The combo now sits at the top of the 야광귀 group (#268), the flash below it.
            int[] comboIndices = LabConfig.DeveloperCategories.Single(category => category.Title == "야광귀").FieldIndices
                .Where(index => index >= 155 && index <= 164).ToArray();
            Assert.That(comboIndices, Is.EqualTo(new[] { 155, 156, 164, 157, 158, 159, 160, 161, 162, 163 }),
                "The warning (#241) sits right after the first start time it comes before.");
            Assert.That(new[] { combo[8].Min, combo[8].Max }, Is.EqualTo(new[] { 1f, 4f }));
            Assert.That(new[] { combo[9].Min, combo[9].Max }, Is.EqualTo(new[] { 0f, 8f }), "0 means no warning.");
            Assert.That(combo[9].IsToggle || combo[9].WholeNumber, Is.False);

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
                Assert.That(source.CaptureDeveloperValues().Skip(155).Take(10),
                    Is.EqualTo(new[] { 0f, 50f, 60f, 15f, 15f, .1f, 0f, 15f, 2.5f, 4f }),
                    "By default the combo is off (#266); once turned on it comes first at 50 s, every 60 s, 15 s to clear, the whole team shrinks "
                    + "to 0.1x or grows to 2.5x for 15 seconds, and a 4-second warning (one yaguang count a second) comes first.");

            float[] values = config.CaptureDeveloperValues();
            float[] before = (float[])values.Clone();
            values[155] = 1f;
            values[156] = 5f;
            values[157] = 25f;
            values[158] = 8f;
            values[159] = 4f;
            values[160] = .15f;
            values[161] = 1f;
            values[162] = 6f;
            values[163] = 2f;
            values[164] = 5f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.GourdColorComboEnabled, Is.True, "DEV can still turn the combo on.");
            Assert.That(new[] { config.ComboFirstStartSeconds, config.ComboRepeatIntervalSeconds,
                config.ComboTimeLimitSeconds, config.ComboPenaltyDurationSeconds, config.ComboPenaltySizeScale },
                Is.EqualTo(new[] { 5f, 25f, 8f, 4f, .15f }));
            Assert.That(config.ComboPenaltyThrowerOnly, Is.True);
            Assert.That(new[] { config.ComboRewardDurationSeconds, config.ComboRewardSizeScale },
                Is.EqualTo(new[] { 6f, 2f }));
            Assert.That(config.ComboWarningSeconds, Is.EqualTo(5f));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            Assert.That(config.CaptureDeveloperValues().Take(155), Is.EqualTo(before.Take(155)),
                "Earlier settings keep their places and values.");

            values[160] = .005f; // below the 0.01 minimum
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Combo Penalty Size"));

            values[160] = .15f;
            values[164] = 8.5f; // above the 8-second maximum
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False);
            Assert.That(error, Does.Contain("Combo Warning"));
            Assert.That(config.ComboWarningSeconds, Is.EqualTo(5f), "A rejected form changes nothing.");
        }

        [Test]
        public void BoardOrbTrailIsAnOnToggleInTheFlatOrbBoardGroup()
        {
            LabDeveloperField field = LabConfig.DeveloperFields[165];
            Assert.That(field.Label, Is.EqualTo("Board Orb Trail (0 off, 1 on)"));
            Assert.That(field.IsToggle, Is.True);
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "Flat orb board").FieldIndices,
                Is.EqualTo(new[] { 11, 12, 13, 14, 15, 16, 165 }), "The shallow trail sits with the board it draws on (#254).");

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
            {
                Assert.That(source.BoardOrbTrailEnabled, Is.True, "On by default, in code and in the saved asset.");
                Assert.That(source.CaptureDeveloperValues()[165], Is.EqualTo(1f));
            }

            float[] values = config.CaptureDeveloperValues();
            float[] before = (float[])values.Clone();
            values[165] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.BoardOrbTrailEnabled, Is.False);
            Assert.That(config.CaptureDeveloperValues().Take(165), Is.EqualTo(before.Take(165)),
                "Earlier settings keep their places and values.");
        }

        [Test]
        public void TheFoxOrbDrawsItsOwnBlueArtFromResources()
        {
            Sprite art = Resources.Load<Sprite>(LabOrbAppearance.FoxFireSpritePath);
            Assert.That(art, Is.Not.Null,
                "Assets/Lab/Resources/Sabotage/FoxOrb.png imports as a Sprite through LabSabotageArtImporter (#251). Reimport it if not.");
            var look = ScriptableObject.CreateInstance<LabOrbAppearance>();
            try
            {
                Assert.That(look.SpriteFor(LabOrbKind.FoxFire), Is.SameAs(art), "An empty Inspector slot falls back to it.");
                Assert.That(look.FoxFireSmokeColor.b, Is.GreaterThan(look.FoxFireSmokeColor.r), "Its smoke is blue.");
            }
            finally { Object.DestroyImmediate(look); }
        }

        [Test]
        public void TheCharmBubblesAndSurprisedBubbleImportAsSprites()
        {
            foreach (string path in new[] { LabCharmBubbles.LeftBubblePath, LabCharmBubbles.RightBubblePath,
                LabCharmBubbles.WokenPath, LabCharmBubbles.FacePath, LabCharmBubbles.CheckPath,
                LabCharmBubbles.LeftHandPath, LabCharmBubbles.RightHandPath })
                Assert.That(Resources.Load<Sprite>(path), Is.Not.Null,
                    "Assets/Lab/Resources/" + path + ".png imports as a Sprite through LabSabotageArtImporter (#251).");
        }

        [Test]
        public void YaguangScheduleAppendsWithoutAnEngineSwitchAndRoundTripsEverySlot()
        {
            float[] before = config.CaptureDeveloperValues();
            Assert.That(before.Length, Is.EqualTo(249));
            Assert.That(before.Skip(211).Take(14), Is.EqualTo(new[] { 120f, 100f, 10f, 0f, 35f, 1f, 60f, 0f, 75f, 1f, 25f, 40f, 60f, 80f }));
            Assert.That(LabConfig.DeveloperFields.Skip(211).Take(14).Any(field => field.IsToggle), Is.False,
                "Stage identity chooses this policy; no parallel timeline engine switch exists.");
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "야광귀 시간표 (Hapio 전용)").FieldIndices,
                Is.EqualTo(Enumerable.Range(212, 13)), "Its own battle time (211) is retired: every stage uses the common one (#328).");
            float[] values = (float[])before.Clone();
            values[211] = 180f; values[212] = 0f; // The retired slot still round-trips; nothing reads it.
            for (int i = 0; i < 4; i++)
            {
                values[213 + i * 2] = 15f + i * 20f;
                values[214 + i * 2] = i % 2;
                values[221 + i] = 20f + i * 25f;
            }
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            Assert.That(config.CaptureDeveloperValues().Take(211), Is.EqualTo(before.Take(211)));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(config.YaguangTimelineUniqueSeconds(i), Is.EqualTo(15f + i * 20f));
                Assert.That(config.YaguangTimelineUniqueIsCombo(i), Is.EqualTo(i % 2 == 1));
                Assert.That(config.YaguangTimelineCommonSeconds(i), Is.EqualTo(20f + i * 25f));
            }
            Assert.That(config.BattleDurationSeconds, Is.EqualTo(before[2]), "The retired slot does not change the battle time.");
            Assert.That(config.YaguangTimelineSabotageEndSeconds, Is.Zero);
        }

        [TestCase(212)] [TestCase(213)] [TestCase(215)] [TestCase(217)] [TestCase(219)]
        [TestCase(221)] [TestCase(222)] [TestCase(223)] [TestCase(224)]
        public void YaguangScheduleNeverHoldsBackAShortBattleTime(int index)
        {
            // The timetable runs inside the common battle time (#328): a slot past a short battle's end simply never comes.
            float[] values = config.CaptureDeveloperValues();
            values[2] = 30f; values[index] = 121f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.BattleDurationSeconds, Is.EqualTo(30f));
            Assert.That(config.CaptureDeveloperValues()[index], Is.EqualTo(121f));
        }

        [Test]
        public void YaguangScheduleAllowsEmptySlotsAndDisabledStopWithoutChangingTheGenericProfile()
        {
            float[] values = config.CaptureDeveloperValues();
            foreach (int index in new[] { 212, 213, 215, 217, 219, 221, 222, 223, 224 }) values[index] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.CommonGimmickInterval, Is.EqualTo(10f));
            Assert.That(config.UniqueGimmickInterval, Is.EqualTo(20f));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
        }

        [Test]
        public void GimmickGroupFieldsFormTheGimmicksGroupWithPercentChancesAndApply()
        {
            Assert.That(LabConfig.DeveloperFields.Skip(205).Take(6).Select(field => field.Label), Is.EqualTo(new[]
            {
                "Common Gimmick Interval (seconds, 0 = none)", "Unique Gimmick Interval (seconds, 0 = none)",
                "Gimmick Gourd Bomb (0 off, 1 on)", "Gimmick Swipe Block (0 off, 1 on)",
                "Gimmick Fox Fire (0 off, 1 on)", "Gimmick Charm (0 off, 1 on)"
            }), "The gimmick groups (#311) are appended at the end.");
            Assert.That(LabConfig.DeveloperFields.Skip(207).Take(4).All(field => field.IsToggle), Is.True);
            Assert.That(LabConfig.DeveloperFields.Skip(166).Take(12).Select(field => field.Label), Is.EqualTo(new[]
            {
                "Lab Unique Gimmicks (0 off, 1 on)", "Sabotage Interval Min (seconds)", "Sabotage Interval Max (seconds)",
                "Gimmick Chance Gourd Bomb (%)", "Gimmick Chance Swipe Block (%)", "Gimmick Chance Fox Fire (%)",
                "Gimmick Chance Charm (%)", "Fox Fire Count Min", "Fox Fire Count Max", "Fox Fire Safe Seconds",
                "Charm Seconds", "Charm Rescue Taps per Neighbor (0 = none)"
            }));
            Assert.That(LabConfig.DeveloperFields.Skip(169).Take(4)
                .All(field => field.WholeNumber && field.Min == 0f && field.Max == 100f), Is.True, "Chances are whole percents.");
            Assert.That(LabConfig.DeveloperFields[204].Label, Is.EqualTo("Charm Warning Seconds (0 = none)"));
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "Gimmicks").FieldIndices,
                Is.EqualTo(new[] { 205, 207, 169, 208, 170, 153, 166, 206, 209, 171, 210, 172, 173, 174, 175, 176, 204, 177 }),
                "Common first (interval, then each gimmick's switch and chance), then the fox's own.");
            Assert.That(LabConfig.DeveloperCategories.Any(category => category.Title == "Sabotage"), Is.False);

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
            {
                float[] defaults = source.CaptureDeveloperValues();
                Assert.That(new[] { defaults[205], defaults[206] }, Is.EqualTo(new[] { 10f, 20f }),
                    "A common gimmick every 10 s and the yokai's own every 20 s, each first after one interval (#311).");
                Assert.That(defaults.Skip(207).Take(4), Is.EqualTo(new[] { 1f, 1f, 1f, 1f }), "Every gimmick on.");
                Assert.That(defaults.Skip(169).Take(4), Is.EqualTo(new[] { 50f, 50f, 50f, 50f }), "Half and half in each group.");
                Assert.That(defaults[166], Is.Zero, "The Lab scene borrows no unique gimmicks by default.");
                Assert.That(defaults.Skip(173).Take(5), Is.EqualTo(new[] { 2f, 5f, 1f, 10f, 10f }),
                    "2-5 fox orbs safe for 1 s, a 10 s charm that each player beside taps 10 times to wake (#305).");
                Assert.That(defaults[204], Is.EqualTo(3f), "Warned three seconds ahead (#305).");
            }

            float[] values = config.CaptureDeveloperValues();
            float[] before = (float[])values.Clone();
            values[166] = 1f;
            values[169] = 25f; values[170] = 75f; values[171] = 100f; values[172] = 0f;
            values[173] = 1f; values[174] = 7f; values[175] = .5f; values[176] = 12f; values[177] = 0f; values[204] = 4.5f;
            values[205] = 7f; values[206] = 0f; values[207] = 0f; values[210] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.LabUniqueGimmicksEnabled, Is.True);
            Assert.That(new[] { config.CommonGimmickInterval, config.UniqueGimmickInterval }, Is.EqualTo(new[] { 7f, 0f }));
            Assert.That(config.GimmickSwitches(), Is.EqualTo(new[] { false, true, true, false }));
            Assert.That(config.GimmickChances(), Is.EqualTo(new[] { 25, 75, 100, 0 }));
            Assert.That(config.CharmWarningSeconds, Is.EqualTo(4.5f));
            Assert.That(new[] { config.FoxFireCountMin, config.FoxFireCountMax, config.CharmRescueTaps },
                Is.EqualTo(new[] { 1, 7, 0 }));
            Assert.That(new[] { config.FoxFireSafeSeconds, config.CharmSeconds }, Is.EqualTo(new[] { .5f, 12f }));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            Assert.That(config.CaptureDeveloperValues().Take(166), Is.EqualTo(before.Take(166)),
                "Earlier settings keep their places and values.");

            foreach (int chance in new[] { 169, 171 })
            {
                float[] uneven = config.CaptureDeveloperValues();
                uneven[chance] = uneven[chance] == 0f ? 1f : uneven[chance] - 1f;
                Assert.That(config.TryApplyDeveloperValues(uneven, out error), Is.False, chance.ToString());
                Assert.That(error, Does.Contain("must add up to 100"));
            }
            float[] backwards = config.CaptureDeveloperValues();
            backwards[173] = backwards[174] + 1f;
            Assert.That(config.TryApplyDeveloperValues(backwards, out error), Is.False);
            Assert.That(error, Does.Contain("cannot exceed"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values), "A rejected form changes nothing.");
        }
        [Test]
        public void GimmickStatsRecordingIsOffByDefaultAndNeverPartOfTheSharedForm()
        {
            Assert.That(config.RecordGimmickStats, Is.False, "UT recording (#311) stays off until someone turns it on.");
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            Assert.That(authored.RecordGimmickStats, Is.False, "The saved Config does not record either.");
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(249), "The switch adds no DEV row and no shared value.");
            Assert.That(LabConfig.DeveloperFields.Any(field => field.Label.IndexOf("stat", System.StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);

            JsonUtility.FromJsonOverwrite("{\"recordGimmickStats\":true}", config);
            float[] values = config.CaptureDeveloperValues();
            var peer = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(peer.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(peer.RecordGimmickStats, Is.False, "Applying a shared form never turns it on elsewhere.");
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 1),
                    Is.EqualTo(LabSessionSettings.Fingerprint(peer.CaptureDeveloperValues(), 3, 1)),
                    "Rooms agree on the settings whether or not the Host records.");
                LabConfig copy = Object.Instantiate(config);
                try { Assert.That(copy.RecordGimmickStats, Is.True, "Runtime copies of a recording Config keep recording."); }
                finally { Object.DestroyImmediate(copy); }
            }
            finally { Object.DestroyImmediate(peer); }
        }

        [Test]
        public void CombinedFlashAndCharmSettingsRoundTripWithoutOverwritingEarlierFields()
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            Assert.That(values, Has.Length.EqualTo(249));
            values[202] = 0f; values[203] = 10f; values[204] = 4.5f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.YaguangFlashShakeRelease, Is.False);
            Assert.That(config.YaguangFlashShakeCount, Is.EqualTo(10));
            Assert.That(config.CharmWarningSeconds, Is.EqualTo(4.5f));
            CollectionAssert.AreEqual(before.Take(202), config.CaptureDeveloperValues().Take(202));
            var peer = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(peer.TryApplyDeveloperValues(config.CaptureDeveloperValues(), out error), Is.True, error);
                CollectionAssert.AreEqual(values, peer.CaptureDeveloperValues());
            }
            finally { Object.DestroyImmediate(peer); }
        }

        [TestCase(203)]
        [TestCase(204)]
        public void StandaloneCharmOrFlashPayloadCannotPartiallyApplyTheCombinedSchema(int oldCount)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] oldPayload = before.Take(oldCount).ToArray();
            oldPayload[0] = 777f;
            Assert.That(config.TryApplyDeveloperValues(oldPayload, out _), Is.False);
            CollectionAssert.AreEqual(before, config.CaptureDeveloperValues());
        }

        [Test]
        public void RawStreakSettingsAppendThreeRowsToGenerationAndKeepNewSavedAndRuntimeDefaults()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(249));
            Assert.That(LabConfig.DeveloperFields.Skip(225).Take(3).Select(field => field.Label), Is.EqualTo(new[]
            {
                "Raw Streak Correction (0 off, 1 on)", "Opposite Chance After 2 Raw (%)", "Maximum Consecutive Raw"
            }));
            Assert.That(LabConfig.DeveloperFields[225].IsToggle, Is.True);
            Assert.That(LabConfig.DeveloperFields[225].DisplayLabel, Is.EqualTo("Raw Streak Correction"));
            Assert.That(LabConfig.DeveloperFields.Skip(225).Take(3).All(field => field.WholeNumber), Is.True);
            Assert.That(LabConfig.DeveloperFields.Skip(225).Take(3).Select(field => field.Min), Is.EqualTo(new[] { 0f, 0f, 1f }));
            Assert.That(LabConfig.DeveloperFields.Skip(225).Take(3).Select(field => field.Max), Is.EqualTo(new[] { 1f, 100f, 10f }));
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "Generation chances").FieldIndices,
                Is.EqualTo(new[] { 39, 40, 41, 42, 43, 44, 225, 226, 227 }));
            foreach (LabConfig source in new[] { config, Resources.Load<LabConfig>("LabConfig") })
            {
                Assert.That(source, Is.Not.Null);
                Assert.That(source.RawStreakCorrectionEnabled, Is.True);
                Assert.That(source.RawStreakOppositeChancePercent, Is.EqualTo(75));
                Assert.That(source.RawStreakMaximum, Is.EqualTo(3));
                float[] before = source.CaptureDeveloperValues();
                Assert.That(before.Skip(225).Take(3), Is.EqualTo(new[] { 1f, 75f, 3f }));
                Assert.That(before[79], Is.Zero, "The retired paint-mode slot remains reserved.");
                LabConfig runtime = Object.Instantiate(source);
                try
                {
                    Assert.That(runtime.CaptureDeveloperValues(), Is.EqualTo(before));
                    float[] changed = runtime.CaptureDeveloperValues();
                    changed[225] = 0f; changed[226] = 20f; changed[227] = 8f;
                    Assert.That(runtime.TryApplyDeveloperValues(changed, out string error), Is.True, error);
                    Assert.That(source.CaptureDeveloperValues(), Is.EqualTo(before), "A runtime copy cannot tune its source.");
                }
                finally { Object.DestroyImmediate(runtime); }
            }
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(1f, 100f, 10f)]
        [TestCase(1f, 75f, 3f)]
        public void RawStreakSettingsRoundTripAtBoundsThroughJsonPeerAndRuntimeCopy(float enabled, float opposite, float maximum)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            values[225] = enabled; values[226] = opposite; values[227] = maximum;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            Assert.That(config.CaptureDeveloperValues().Take(225), Is.EqualTo(before.Take(225)));
            float[] received = JsonUtility.FromJson<SharedModeSettings>(JsonUtility.ToJson(
                new SharedModeSettings { values = config.CaptureDeveloperValues() })).values;
            var peer = ScriptableObject.CreateInstance<LabConfig>();
            LabConfig runtime = Object.Instantiate(config);
            try
            {
                Assert.That(peer.TryApplyDeveloperValues(received, out error), Is.True, error);
                foreach (LabConfig copy in new[] { peer, runtime })
                {
                    Assert.That(copy.RawStreakCorrectionEnabled, Is.EqualTo(enabled == 1f));
                    Assert.That(copy.RawStreakOppositeChancePercent, Is.EqualTo((int)opposite));
                    Assert.That(copy.RawStreakMaximum, Is.EqualTo((int)maximum));
                    Assert.That(copy.CaptureDeveloperValues(), Is.EqualTo(values));
                }
                Assert.That(LabSessionSettings.Fingerprint(received, 3, 5676),
                    Is.EqualTo(LabSessionSettings.Fingerprint(values, 3, 5676)));
            }
            finally { Object.DestroyImmediate(peer); Object.DestroyImmediate(runtime); }
        }

        [TestCase(225, -1f)] [TestCase(225, .5f)] [TestCase(225, 2f)]
        [TestCase(225, float.NaN)] [TestCase(225, float.PositiveInfinity)] [TestCase(225, float.NegativeInfinity)]
        [TestCase(226, -1f)] [TestCase(226, .5f)] [TestCase(226, 101f)]
        [TestCase(226, float.NaN)] [TestCase(226, float.PositiveInfinity)] [TestCase(226, float.NegativeInfinity)]
        [TestCase(227, 0f)] [TestCase(227, 1.5f)] [TestCase(227, 11f)]
        [TestCase(227, float.NaN)] [TestCase(227, float.PositiveInfinity)] [TestCase(227, float.NegativeInfinity)]
        public void InvalidRawStreakSettingsRejectTheWholeFormAtomically(int index, float invalid)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            values[0] = 777f; values[index] = invalid;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
        }

        [Test]
        public void RawStreakInspectorValuesClampAndMissingLegacyFieldsKeepDefaults()
        {
            JsonUtility.FromJsonOverwrite("{\"yinChance\":40,\"yangChance\":60}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(225).Take(3), Is.EqualTo(new[] { 1f, 75f, 3f }),
                "Older serialized configs inherit the new defaults.");
            JsonUtility.FromJsonOverwrite("{\"rawStreakCorrectionEnabled\":false,\"rawStreakOppositeChancePercent\":-10,\"rawStreakMaximum\":0}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(225).Take(3), Is.EqualTo(new[] { 0f, 0f, 1f }));
            JsonUtility.FromJsonOverwrite("{\"rawStreakCorrectionEnabled\":true,\"rawStreakOppositeChancePercent\":110,\"rawStreakMaximum\":11}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(225).Take(3), Is.EqualTo(new[] { 1f, 100f, 10f }));
        }

        [TestCase(225, 0f)] [TestCase(226, 50f)] [TestCase(227, 10f)]
        public void EveryRawStreakFieldChangesTheRoomFingerprintAndMapsItsSliderRange(int index, float changed)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone(); values[index] = changed;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(LabSessionSettings.Fingerprint(values, 3, 5676),
                Is.Not.EqualTo(LabSessionSettings.Fingerprint(before, 3, 5676)));
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            foreach (float endpoint in new[] { field.Min, field.Max, before[index] })
            {
                float position = LabDeveloperSliderScale.Normalize(field, before[index], endpoint);
                Assert.That(LabDeveloperSliderScale.Denormalize(field, before[index], position),
                    Is.EqualTo(endpoint).Within(.0001f));
            }
        }
    }
}
