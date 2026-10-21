using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>야광귀 섬광 (#268): DEV 설정·야광귀 묶음, Host 예약·대상·콤보 피하기, 눈부심 계산.</summary>
    public sealed class LabYaguangFlashTests
    {
        private static readonly ulong[] Three = { 10, 20, 30 };
        private readonly List<LabConfig> created = new List<LabConfig>();

        [TearDown]
        public void TearDown()
        {
            foreach (LabConfig config in created) Object.DestroyImmediate(config);
            created.Clear();
        }

        [Test]
        public void FlashFieldsAreKoreanStartOffAndShareTheYaguangGroupWithTheCombo()
        {
            LabDeveloperField[] flash = LabConfig.DeveloperFields.Skip(178).Take(7).ToArray();
            Assert.That(flash.Select(field => field.Label), Is.EqualTo(new[]
            {
                "야광귀 섬광 (0 off, 1 on)", "섬광 대상 (0 모두, 1 한 명)", "섬광 첫 시작 (초)", "섬광 간격 (초)",
                "섬광 예고 (초)", "섬광 눈부심 (초)", "섬광 회복 (초)"
            }));
            Assert.That(new[] { flash[0].DisplayLabel, flash[1].DisplayLabel }, Is.EqualTo(new[] { "야광귀 섬광", "섬광 대상" }));
            Assert.That(new[] { flash[0].OffText, flash[0].OnText }, Is.EqualTo(new[] { "끔", "켬" }));
            Assert.That(new[] { flash[1].OffText, flash[1].OnText }, Is.EqualTo(new[] { "모두", "한 명" }));
            Assert.That(flash.Skip(2).All(field => !field.IsToggle && !field.WholeNumber), Is.True);
            float[] defaults = { 31f, 60f, 3f, 7f, 2f };
            for (int i = 0; i < defaults.Length; i++)
                Assert.That((flash[i + 2].Min + flash[i + 2].Max) * .5f, Is.EqualTo(defaults[i]),
                    flash[i + 2].Label + ": every slider starts in the middle of its range.");

            Assert.That(LabConfig.DeveloperCategories.Any(category => category.Title == "Combo"), Is.False,
                "The combo moved into the 야광귀 group.");
            LabDeveloperCategory yaguang = LabConfig.DeveloperCategories.Single(category => category.Title == "야광귀");
            Assert.That(yaguang.FieldIndices, Is.EqualTo(new[]
                { 155, 185, 156, 164, 157, 158, 159, 160, 161, 162, 163, 178, 179, 180, 181, 182, 183, 184 }),
                "The yaguang counting toggle (#271) sits right under the combo switch.");
            Assert.That(yaguang.SwitchFields, Is.EqualTo(new[] { 155, 178 }), "One switch turns the combo and the flash on together.");
            Assert.That(yaguang.DividerAt, Is.EqualTo(new[] { 0, 11 }));
            Assert.That(yaguang.DividerLabels, Is.EqualTo(new[] { "── 콤보 ──", "── 섬광 ──" }));
            foreach (int index in new[] { 159, 162 })
                Assert.That((LabConfig.DeveloperFields[index].Min + LabConfig.DeveloperFields[index].Max) * .5f, Is.EqualTo(15f),
                    LabConfig.DeveloperFields[index].Label + ": the 15 s penalty/reward slider starts in the middle.");
            Assert.That(LabConfig.DeveloperCategories.Where(category => category.Title != "야광귀")
                .All(category => category.SwitchFields == null && category.DividerAt == null), Is.True);

            LabConfig config = Config();
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
                Assert.That(source.CaptureDeveloperValues().Skip(178).Take(7), Is.EqualTo(new[] { 0f, 0f, 31f, 60f, 3f, 7f, 2f }),
                    "Off by default; once on, everyone flashes at 31 s and every 60 s, 19 s before each combo, "
                    + "with a 3 s warning (yellow eyes for most of it), 7 s of glare and 2 s to recover.");

            float[] values = config.CaptureDeveloperValues();
            values[178] = 1f; values[179] = 1f; values[180] = 5f; values[181] = 24f;
            values[182] = 0f; values[183] = 2f; values[184] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(new[] { config.YaguangFlashEnabled, config.YaguangFlashTargetOne }, Is.All.True);
            Assert.That(new[] { config.YaguangFlashFirstStartSeconds, config.YaguangFlashIntervalSeconds,
                config.YaguangFlashWarningSeconds, config.YaguangFlashGlareSeconds, config.YaguangFlashRecoverySeconds },
                Is.EqualTo(new[] { 5f, 24f, 0f, 2f, 0f }));
            values[183] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, "A glare lasts at least 1 s.");
            values[183] = 2f;
            values[181] = 12f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, "Flashes come at least 20 s apart.");
        }

        [Test]
        public void OffMeansNoFlashAtAll()
        {
            var model = new LabModel();
            model.Begin("flash-off", Three, 0d, Config());
            LabSnapshot snapshot = model.Snapshot(170d);
            Assert.That(snapshot.flashCount, Is.Zero);
            Assert.That(new[] { snapshot.flashAt, snapshot.flashWarningAt, snapshot.flashLastAt }, Is.All.EqualTo(-1d));
            Assert.That(snapshot.LastFlashHit(10), Is.False);
        }

        [Test]
        public void EveryoneFlashesTogetherOnTheDefaultSchedule()
        {
            var model = new LabModel();
            model.Begin("flash-all", Three, 0d, Config(flash: true));
            LabSnapshot calm = model.Snapshot(0d);
            Assert.That(new[] { calm.flashWarningAt, calm.flashAt }, Is.EqualTo(new[] { 28.5d, 30d }));
            Assert.That(calm.flashTarget, Is.Zero, "ALL: no single target.");
            Assert.That(calm.FlashWarningOn(28d), Is.False);

            LabSnapshot warning = model.Snapshot(29d);
            Assert.That(warning.FlashWarningOn(29d), Is.True, "The yokai peeks out for 1.5 s first.");
            Assert.That(warning.flashCount, Is.Zero);
            Assert.That(LabYaguangFlash.WarningAmount(warning, 29.25d, 20), Is.EqualTo(.5f).Within(1e-4f));

            LabSnapshot flashed = model.Snapshot(30d);
            Assert.That(flashed.flashCount, Is.EqualTo(1u));
            Assert.That(flashed.flashLastAt, Is.EqualTo(30d));
            Assert.That(Three.All(flashed.LastFlashHit), Is.True, "Everyone is dazzled at once.");
            Assert.That(flashed.flashAt, Is.EqualTo(90d), "Then every 60 s.");

            LabSnapshot received = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(flashed));
            Assert.That(new[] { received.flashLastAt, received.flashAt }, Is.EqualTo(new[] { 30d, 90d }));
            Assert.That(LabBoardReplica.CopySnapshot(flashed).flashCount, Is.EqualTo(1u), "Board copies keep the flash.");

            Assert.That(model.Snapshot(89.9d).flashCount, Is.EqualTo(1u));
            Assert.That(model.Snapshot(90d).flashCount, Is.EqualTo(2u));
        }

        [Test]
        public void OneTargetIsDrawnTheSameOnEveryDeviceAndNeverTwiceInARow()
        {
            List<ulong> a = Targets(new LabModel()), b = Targets(new LabModel());
            Assert.That(a, Is.EqualTo(b), "Every device draws the same players.");
            Assert.That(a.All(Three.Contains), Is.True);
            for (int i = 1; i < a.Count; i++) Assert.That(a[i], Is.Not.EqualTo(a[i - 1]), "Not the same player twice in a row.");
        }

        [Test]
        public void TheFlashWaitsOutTheComboAndItsPenaltyThenWarnsAgainInFull()
        {
            var model = new LabModel();
            // The combo's warning starts at 27 s and its slots stay up until 40 s; the flash is due at 31 s.
            // Nobody clears it, so everyone is SMALL for 15 s after the 40 s timeout.
            model.Begin("flash-combo", Three, 0d, Config(flash: true, first: 31f, combo: true));
            Assert.That(model.Snapshot(20d).flashAt, Is.EqualTo(-1d),
                "Already known to wait for the combo, so no device is told a time to peek out (#271).");
            LabSnapshot held = model.Snapshot(29.6d);
            Assert.That(held.flashAt, Is.EqualTo(-1d), "No flash warning over the combo warning.");
            Assert.That(model.Snapshot(35d).flashCount, Is.Zero, "Nor during the combo.");
            LabSnapshot penalty = model.Snapshot(40.5d);
            Assert.That(penalty.comboActive, Is.False);
            Assert.That(penalty.players.All(player => player.comboPenaltyUntil == 55d), Is.True);
            Assert.That(penalty.flashAt, Is.EqualTo(-1d), "Nor over the combo penalty (#271).");
            Assert.That(model.Snapshot(54.9d).flashCount, Is.Zero);
            LabSnapshot after = model.Snapshot(55.5d);
            Assert.That(after.flashCount, Is.Zero, "It does not go off late, without a warning.");
            Assert.That(new[] { after.flashWarningAt, after.flashAt }, Is.EqualTo(new[] { 55.5d, 57d }),
                "The full 1.5 s warning starts once the penalty is over.");
            Assert.That(model.Snapshot(57d).flashCount, Is.EqualTo(1u));
        }

        [Test]
        public void AFlashThatEndsBeforeTheComboWarningGoesAheadAsPlanned()
        {
            var model = new LabModel();
            // Warning 18.5 s, flash 20 s, glare and recovery over at 24.5 s: before the combo warning at 27 s.
            model.Begin("flash-before-combo", Three, 0d, Config(flash: true, first: 20f, combo: true));
            Assert.That(model.Snapshot(18.6d).FlashWarningOn(18.6d), Is.True);
            LabSnapshot flashed = model.Snapshot(20d);
            Assert.That(flashed.flashCount, Is.EqualTo(1u));
            Assert.That(flashed.flashLastAt, Is.EqualTo(20d));
            Assert.That(model.Snapshot(30d).comboActive, Is.True, "The combo still comes on time.");
        }

        [Test]
        public void AFlashThatWouldRunIntoTheComboWarningWaitsForTheWholeCombo()
        {
            var model = new LabModel();
            // Due at 25 s, its warning would start at 23.5 s and its recovery end at 29.5 s, after the combo warning at 27 s.
            model.Begin("flash-into-combo", Three, 0d, Config(flash: true, first: 25f, combo: true));
            Assert.That(model.Snapshot(10d).flashAt, Is.EqualTo(-1d), "Not announced: it would be held.");
            LabSnapshot due = model.Snapshot(23.6d);
            Assert.That(due.flashAt, Is.EqualTo(-1d), "It does not start: it would not finish before the combo.");
            Assert.That(due.FlashWarningOn(23.6d), Is.False);
            Assert.That(model.Snapshot(30d).comboActive, Is.True, "The combo comes on time.");
            Assert.That(model.Snapshot(54.9d).flashCount, Is.Zero, "Still waiting through the combo and its penalty.");
            Assert.That(model.Snapshot(55.5d).flashAt, Is.EqualTo(57d));
        }

        [Test]
        public void NoWarningShowsWhileTheComboIsUpEvenFromALateSnapshot()
        {
            var snapshot = new LabSnapshot
            {
                phase = LabPhase.Playing, flashWarningAt = 10d, flashAt = 12d, comboActive = true
            };
            Assert.That(snapshot.FlashWarningOn(11d), Is.False, "No eyes over a running combo.");
            snapshot.comboActive = false;
            snapshot.comboWarningAt = 10.5d;
            snapshot.comboNextAt = 13d;
            Assert.That(snapshot.FlashWarningOn(11d), Is.False, "Nor over the combo warning.");
            snapshot.comboWarningAt = snapshot.comboNextAt = -1d;
            Assert.That(snapshot.FlashWarningOn(11d), Is.True);
        }

        [Test]
        public void ABombDueDuringAFlashWaitsUntilTheFlashIsOver()
        {
            var model = new LabModel();
            // Flash: warning 22.5 s, flash 24 s, glare and recovery over at 28.5 s. The gourd's bomb is due at 25 s.
            model.Begin("flash-then-bomb", Three, 0d, Config(flash: true, first: 24f, bombEvery: 25f));
            Assert.That(model.Snapshot(22.6d).FlashWarningOn(22.6d), Is.True);
            Assert.That(model.Snapshot(24d).flashCount, Is.EqualTo(1u));
            Assert.That(GourdBombs(model.Snapshot(25d)), Is.Zero, "No bomb while the flash is open (#271).");
            Assert.That(GourdBombs(model.Snapshot(28.4d)), Is.Zero);
            LabSnapshot after = model.Snapshot(28.6d);
            Assert.That(GourdBombs(after), Is.EqualTo(1), "It drops as soon as the flash closes.");
            Assert.That(after.orbs.Single(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd).createdAt,
                Is.EqualTo(28.6d));
        }

        [Test]
        public void AFlashWaitsWhileTheGourdsBombIsStillOnABoard()
        {
            var model = new LabModel();
            // The bomb drops at 10 s and blasts at the end of its 15 s fuse (25 s). The flash is due at 20 s.
            model.Begin("bomb-then-flash", Three, 0d, Config(flash: true, first: 20f, bombEvery: 10f));
            Assert.That(GourdBombs(model.Snapshot(10d)), Is.EqualTo(1));
            LabSnapshot held = model.Snapshot(18.6d);
            Assert.That(held.flashAt, Is.EqualTo(-1d), "No eyes while the bomb is out.");
            Assert.That(model.Snapshot(24.9d).flashCount, Is.Zero);
            model.Snapshot(25d);
            LabSnapshot after = model.Snapshot(26d);
            Assert.That(GourdBombs(after), Is.Zero);
            Assert.That(new[] { after.flashWarningAt, after.flashAt }, Is.EqualTo(new[] { 26d, 27.5d }),
                "Once the bomb is gone, the full warning, then the flash.");
        }

        [Test]
        public void AComboWaitsWhileTheGourdsBombIsStillOnABoard()
        {
            var model = new LabModel();
            // The bomb drops at 10 s and is gone at 25 s. The combo's warning is due at 17 s.
            model.Begin("bomb-then-combo", Three, 0d, Config(combo: true, bombEvery: 10f));
            Assert.That(model.Snapshot(10d).comboNextAt, Is.EqualTo(-1d), "Not announced while the bomb is out.");
            LabSnapshot held = model.Snapshot(20d);
            Assert.That(held.comboActive, Is.False, "No combo over the bomb (#271).");
            Assert.That(held.ComboWarningOn(20d), Is.False);
            model.Snapshot(25d);
            LabSnapshot warned = model.Snapshot(25.5d);
            Assert.That(new[] { warned.comboWarningAt, warned.comboNextAt }, Is.EqualTo(new[] { 25.5d, 28.5d }));
            Assert.That(model.Snapshot(28.5d).comboActive, Is.True);
        }

        private static int GourdBombs(LabSnapshot snapshot) =>
            snapshot.orbs.Count(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd);

        [Test]
        public void AShortIntervalStillLeavesTheGlareRecoveryAndNextWarningApart()
        {
            var model = new LabModel();
            model.Begin("flash-gap", Three, 0d, Config(flash: true, interval: 20f, warning: 4f, glare: 13f, recovery: 4f));
            LabSnapshot flashed = model.Snapshot(30d);
            Assert.That(flashed.flashAt, Is.EqualTo(51d), "4 warning + 13 glare + 4 recovery is longer than 20 s.");
        }

        [Test]
        public void TheEyesStayPlainThenLightUpSuddenlyBeforeTheFlash()
        {
            Assert.That(LabYaguangFlashHud.EyeGlowAt(0f), Is.Zero);
            Assert.That(LabYaguangFlashHud.EyeGlowAt(.3f), Is.Zero, "Plain eyes for the first 30% of the warning.");
            Assert.That(LabYaguangFlashHud.EyeGlowAt(.375f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(LabYaguangFlashHud.EyeGlowAt(.45f), Is.EqualTo(1f), "Fully lit quickly...");
            Assert.That(LabYaguangFlashHud.EyeGlowAt(1f), Is.EqualTo(1f), "...and held for the last 55% to enjoy.");
        }

        [Test]
        public void TheGlareIsFullThenFadesSmoothlyBack()
        {
            var snapshot = new LabSnapshot
            {
                phase = LabPhase.Playing, flashCount = 1, flashLastAt = 30d, flashLastTarget = 0
            };
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 29.9d, 10, 3f, 1.5f), Is.Zero);
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 30d, 10, 3f, 1.5f), Is.EqualTo(1f));
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 32.9d, 10, 3f, 1.5f), Is.EqualTo(1f));
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 33.75d, 10, 3f, 1.5f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 34.5d, 10, 3f, 1.5f), Is.Zero);
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 33.1d, 10, 3f, 0f), Is.Zero, "No recovery: colour comes straight back.");
            snapshot.phase = LabPhase.Victory;
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 31d, 10, 3f, 1.5f), Is.Zero, "Only during the battle.");
        }

        private List<ulong> Targets(LabModel model)
        {
            model.Begin("flash-one", Three, 0d, Config(flash: true, one: true, interval: 20f));
            var targets = new List<ulong>();
            for (int i = 0; i < 8; i++)
            {
                double at = 30d + 20d * i;
                LabSnapshot before = model.Snapshot(at - .1d);
                LabSnapshot after = model.Snapshot(at);
                Assert.That(after.flashCount, Is.EqualTo((uint)(i + 1)));
                Assert.That(after.flashLastTarget, Is.EqualTo(before.flashTarget), "The warned player is the one hit.");
                targets.Add(after.flashLastTarget);
                Assert.That(Three.Count(after.LastFlashHit), Is.EqualTo(1), "ONE: only that player is dazzled.");
                foreach (ulong other in Three.Where(player => player != after.flashLastTarget))
                    Assert.That(LabYaguangFlash.GlareAmount(after, at + .5d, other, 3f, 1.5f), Is.Zero);
            }
            return targets;
        }

        private LabConfig Config(bool flash = false, bool one = false, float first = 30f, float interval = 60f,
            bool combo = false, float warning = 1.5f, float glare = 3f, float recovery = 1.5f, float bombEvery = 0f)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            float[] values = config.CaptureDeveloperValues();
            values[87] = bombEvery; // 0: no gourd bombs
            values[88] = bombEvery;
            values[152] = 0f; // no swipe blocks unless a test turns them on
            values[155] = combo ? 1f : 0f;
            values[156] = 30f; // combo slots at 30 s, warned from 27 s
            values[157] = 60f;
            values[158] = 10f;
            values[164] = 3f;
            values[178] = flash ? 1f : 0f;
            values[179] = one ? 1f : 0f;
            values[180] = first;
            values[181] = interval;
            values[182] = warning;
            values[183] = glare;
            values[184] = recovery;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return config;
        }
    }
}
