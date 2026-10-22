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
            LabDeveloperField[] flash = LabConfig.DeveloperFields.Skip(184).ToArray();
            Assert.That(flash.Select(field => field.Label), Is.EqualTo(new[]
            {
                "야광귀 섬광 (0 off, 1 on)", "섬광 대상 (0 모두, 1 한 명)", "섬광 첫 시작 (초)", "섬광 간격 (초)",
                "섬광 예고 (초)", "섬광 눈부심 (초)", "섬광 회복 (초)"
            }));
            Assert.That(new[] { flash[0].DisplayLabel, flash[1].DisplayLabel }, Is.EqualTo(new[] { "야광귀 섬광", "섬광 대상" }));
            Assert.That(new[] { flash[0].OffText, flash[0].OnText }, Is.EqualTo(new[] { "끔", "켬" }));
            Assert.That(new[] { flash[1].OffText, flash[1].OnText }, Is.EqualTo(new[] { "모두", "한 명" }));
            Assert.That(flash.Skip(2).All(field => !field.IsToggle && !field.WholeNumber), Is.True);
            float[] defaults = { 31f, 60f, 2f, 7f, 2f };
            for (int i = 0; i < defaults.Length; i++)
                Assert.That((flash[i + 2].Min + flash[i + 2].Max) * .5f, Is.EqualTo(defaults[i]),
                    flash[i + 2].Label + ": every slider starts in the middle of its range.");

            Assert.That(LabConfig.DeveloperCategories.Any(category => category.Title == "Combo"), Is.False,
                "The combo moved into the 야광귀 group.");
            LabDeveloperCategory yaguang = LabConfig.DeveloperCategories.Single(category => category.Title == "야광귀");
            Assert.That(yaguang.FieldIndices, Is.EqualTo(new[]
                { 155, 156, 164, 157, 158, 159, 160, 161, 162, 163, 184, 185, 186, 187, 188, 189, 190 }));
            Assert.That(yaguang.SwitchFields, Is.EqualTo(new[] { 155, 184 }), "One switch turns the combo and the flash on together.");
            Assert.That(yaguang.DividerAt, Is.EqualTo(new[] { 0, 10 }));
            Assert.That(yaguang.DividerLabels, Is.EqualTo(new[] { "── 콤보 ──", "── 섬광 ──" }));
            foreach (int index in new[] { 159, 162 })
                Assert.That((LabConfig.DeveloperFields[index].Min + LabConfig.DeveloperFields[index].Max) * .5f, Is.EqualTo(15f),
                    LabConfig.DeveloperFields[index].Label + ": the 15 s penalty/reward slider starts in the middle.");
            Assert.That(LabConfig.DeveloperCategories.Where(category => category.Title != "야광귀")
                .All(category => category.SwitchFields == null && category.DividerAt == null), Is.True);

            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
                Assert.That(source.CaptureDeveloperValues().Skip(184), Is.EqualTo(new[] { 0f, 0f, 31f, 60f, 2f, 7f, 2f }),
                    "Off by default; once on, everyone flashes at 31 s and every 60 s, 19 s before each combo, "
                    + "with a 2 s warning, 7 s of glare and 2 s to recover.");

            float[] values = config.CaptureDeveloperValues();
            values[184] = 1f; values[185] = 1f; values[186] = 5f; values[187] = 24f;
            values[188] = 0f; values[189] = 2f; values[190] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(new[] { config.YaguangFlashEnabled, config.YaguangFlashTargetOne }, Is.All.True);
            Assert.That(new[] { config.YaguangFlashFirstStartSeconds, config.YaguangFlashIntervalSeconds,
                config.YaguangFlashWarningSeconds, config.YaguangFlashGlareSeconds, config.YaguangFlashRecoverySeconds },
                Is.EqualTo(new[] { 5f, 24f, 0f, 2f, 0f }));
            values[189] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, "A glare lasts at least 1 s.");
            values[189] = 2f;
            values[187] = 12f;
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
        public void HostsZeroIdCanBeTheFirstSingleTargetWithoutFlashingEveryone()
        {
            var drawn = new HashSet<ulong>();
            LabConfig config = Config(flash: true, one: true, first: 1f, warning: 0f);
            for (int seed = 0; seed < 64 && drawn.Count < 2; seed++)
            {
                var model = new LabModel();
                model.Begin("flash-zero-" + seed, new ulong[] { 0, 20 }, 0d, config);
                var warning = model.Snapshot(0d);
                var snapshot = model.Snapshot(1d);
                drawn.Add(snapshot.flashLastTarget);
                Assert.That(snapshot.flashLastTargetAll || warning.flashTargetAll, Is.False);
                Assert.That(new ulong[] { 0, 20 }.Count(snapshot.LastFlashHit), Is.EqualTo(1));
                Assert.That(snapshot.LastFlashHit(snapshot.flashLastTarget), Is.True);
            }
            Assert.That(drawn, Is.EquivalentTo(new ulong[] { 0, 20 }),
                "The first cast has no previous player, so the Host must remain eligible.");
        }

        [Test]
        public void FlashScheduleAndJigwiFireCoexistWithoutChangingFireConsumptionOrExpiry()
        {
            LabConfig config = Config(flash: true, first: 5f, warning: 0f);
            var values = config.CaptureDeveloperValues();
            values[178] = 2f;
            values[179] = 30f;
            Assert.That(config.TryApplyDeveloperValues(values, out var error), Is.True, error);
            var model = new LabModel();
            model.BeginSolo("flash-jigwi", 0, 0d, config);
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var before = model.Snapshot(5d);
            Assert.That(before.flashCount, Is.EqualTo(1u));
            Assert.That(before.flashLastTargetAll, Is.True);
            var fire = before.JigwiFor(0);
            Assert.That(fire.FireActive(5d), Is.True);
            Assert.That(fire.targets.Length, Is.EqualTo(2));
            Assert.That(before.orbs.Length, Is.EqualTo(4));
            var water = before.orbs[0];
            Assert.That(water.expiresAt, Is.EqualTo(30d));
            Assert.That(model.TryLaunch(0, water.id, "water", 5d, out _, out _), Is.True);
            Assert.That(model.ConfirmJigwiFireHit(water.id, fire.fireId, fire.targets[0].id,
                6d, out bool extinguished), Is.True);
            Assert.That(extinguished, Is.True);
            var after = model.Snapshot(6d);
            Assert.That(after.hp, Is.EqualTo(before.hp));
            Assert.That(after.paintCoverage, Is.EqualTo(before.paintCoverage));
            Assert.That(after.JigwiFor(0).targets.Count(target => target.extinguished), Is.EqualTo(1));
        }

        [Test]
        public void TheFlashWaitsOutTheComboThenWarnsAgainInFull()
        {
            var model = new LabModel();
            // The combo's warning starts at 27 s and its slots stay up until 40 s; the flash is due at 31 s.
            model.Begin("flash-combo", Three, 0d, Config(flash: true, first: 31f, combo: true));
            Assert.That(model.Snapshot(26d).flashAt, Is.EqualTo(31d));
            LabSnapshot held = model.Snapshot(29.6d);
            Assert.That(held.flashAt, Is.EqualTo(-1d), "No flash warning over the combo warning.");
            Assert.That(model.Snapshot(35d).flashCount, Is.Zero, "Nor during the combo.");
            LabSnapshot after = model.Snapshot(40.5d);
            Assert.That(after.comboActive, Is.False);
            Assert.That(after.flashCount, Is.Zero, "It does not go off late, without a warning.");
            Assert.That(new[] { after.flashWarningAt, after.flashAt }, Is.EqualTo(new[] { 40.5d, 42d }),
                "The full 1.5 s warning starts once the combo is over.");
            Assert.That(model.Snapshot(42d).flashCount, Is.EqualTo(1u));
        }

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
            Assert.That(LabYaguangFlashHud.EyeGlowAt(.45f), Is.Zero, "Plain eyes for the first part of the warning.");
            Assert.That(LabYaguangFlashHud.EyeGlowAt(.575f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(LabYaguangFlashHud.EyeGlowAt(.7f), Is.EqualTo(1f), "Fully lit quickly...");
            Assert.That(LabYaguangFlashHud.EyeGlowAt(1f), Is.EqualTo(1f), "...and held until the flash.");
        }

        [Test]
        public void TheGlareIsFullThenFadesSmoothlyBack()
        {
            var snapshot = new LabSnapshot
            {
                phase = LabPhase.Playing, flashCount = 1, flashLastAt = 30d, flashLastTarget = 0, flashLastTargetAll = true
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
            bool combo = false, float warning = 1.5f, float glare = 3f, float recovery = 1.5f)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            float[] values = config.CaptureDeveloperValues();
            values[87] = 0f; // no gourd bombs
            values[88] = 0f;
            values[155] = combo ? 1f : 0f;
            values[156] = 30f; // combo slots at 30 s, warned from 27 s
            values[157] = 60f;
            values[158] = 10f;
            values[164] = 3f;
            values[184] = flash ? 1f : 0f;
            values[185] = one ? 1f : 0f;
            values[186] = first;
            values[187] = interval;
            values[188] = warning;
            values[189] = glare;
            values[190] = recovery;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return config;
        }
    }
}
