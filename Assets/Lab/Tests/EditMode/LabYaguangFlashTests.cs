using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
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
            LabDeveloperField[] flash = LabConfig.DeveloperFields.Skip(184).Take(7).ToArray();
            Assert.That(flash.Select(field => field.Label), Is.EqualTo(new[]
            {
                "야광귀 섬광 (0 off, 1 on)", "섬광 대상 (0 모두, 1 한 명)", "섬광 첫 시작 (초)", "섬광 간격 (초)",
                "섬광 예고 (초)", "섬광 눈부심 (초)", "섬광 회복 (초)"
            }));
            Assert.That(new[] { flash[0].DisplayLabel, flash[1].DisplayLabel }, Is.EqualTo(new[] { "야광귀 섬광", "섬광 대상" }));
            Assert.That(new[] { flash[0].OffText, flash[0].OnText }, Is.EqualTo(new[] { "끔", "켬" }));
            Assert.That(new[] { flash[1].OffText, flash[1].OnText }, Is.EqualTo(new[] { "모두", "한 명" }));
            Assert.That(flash.Skip(2).All(field => !field.IsToggle && !field.WholeNumber), Is.True);
            // 눈부심·회복은 #301부터 흔들기에 맞춰 10초(범위 1~19)·1초(범위 0~2), 각각 범위 가운데.
            float[] defaults = { 10f, 60f, 3f, 10f, 1f };
            for (int i = 0; i < defaults.Length; i++)
                Assert.That((flash[i + 2].Min + flash[i + 2].Max) * .5f, Is.EqualTo(defaults[i]),
                    flash[i + 2].Label + ": every slider starts in the middle of its range.");

            Assert.That(LabConfig.DeveloperCategories.Any(category => category.Title == "Combo"), Is.False,
                "The combo moved into the 야광귀 group.");
            LabDeveloperCategory yaguang = LabConfig.DeveloperCategories.Single(category => category.Title == "야광귀");
            Assert.That(yaguang.FieldIndices, Is.EqualTo(new[]
                { 155, 191, 156, 164, 157, 158, 159, 160, 161, 162, 163, 184, 185, 186, 187, 188, 189, 190, 202, 203 }),
                "The yaguang counting toggle (#271) sits right under the combo switch.");
            Assert.That(yaguang.SwitchFields, Is.EqualTo(new[] { 155, 184 }), "One switch turns the combo and the flash on together.");
            Assert.That(yaguang.DividerAt, Is.EqualTo(new[] { 0, 11 }));
            Assert.That(yaguang.DividerLabels, Is.EqualTo(new[] { "── 콤보 ──", "── 섬광 ──" }));
            foreach (int index in new[] { 159, 162 })
                Assert.That((LabConfig.DeveloperFields[index].Min + LabConfig.DeveloperFields[index].Max) * .5f, Is.EqualTo(15f),
                    LabConfig.DeveloperFields[index].Label + ": the 15 s penalty/reward slider starts in the middle.");
            Assert.That(LabConfig.DeveloperCategories.All(category => category.Title == "야광귀" || category.SwitchFields == null),
                Is.True, "Only the yaguang group has a title switch.");
            Assert.That(LabConfig.DeveloperCategories.Where(category => category.DividerAt != null).Select(category => category.Title),
                Is.EquivalentTo(new[] { "Gimmicks", "야광귀", "야광귀 시간표 (Hapio 전용)" }),
                "The appended schedule is a separate Hapio-only group, while Lab keeps its periodic groups.");
            LabDeveloperCategory timeline = LabConfig.DeveloperCategories.Single(category => category.Title == "야광귀 시간표 (Hapio 전용)");
            Assert.That(timeline.FieldIndices, Is.EqualTo(Enumerable.Range(212, 13)), "Its battle time (211) is retired (#328).");
            Assert.That(timeline.FieldIndices.All(LabConfig.IsYaguangTimelineField), Is.True);
            AssertSavedLabHidesTimelineCategory(timeline);
            LabDeveloperCategory gimmicks = LabConfig.DeveloperCategories.Single(category => category.Title == "Gimmicks");
            Assert.That(gimmicks.DividerAt, Is.EqualTo(new[] { 0, 6 }));
            Assert.That(gimmicks.DividerLabels, Is.EqualTo(new[] { "── 공용 기믹 ──", "── 고유 기믹 (구미호) ──" }));

            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
                Assert.That(source.CaptureDeveloperValues().Skip(184).Take(7), Is.EqualTo(new[] { 0f, 0f, 10f, 60f, 3f, 10f, 1f }),
                    "Off by default; once on, everyone flashes at 10 s (so the round does not open empty, #271) and every 60 s, "
                    + "with a 3 s warning (yellow eyes for most of it), up to 10 s of glare and 1 s to recover (#301).");

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

        private static void AssertSavedLabHidesTimelineCategory(LabDeveloperCategory timeline)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity", OpenSceneMode.Additive);
            try
            {
                LabDeveloperMode ui = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
                Assert.That(ui.EnsureValueInputs(), Is.True,
                    "The saved Lab expands its legacy setting rows through the same public startup path as runtime.");
                Assert.That(ui.valueInputs, Has.Length.EqualTo(230));
                typeof(LabDeveloperMode).GetMethod("BuildCategoryLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ui, null);
                Transform content = ui.valueInputs[0].transform.parent.parent;
                Assert.That(content.Find("Category " + timeline.Title), Is.Null,
                    "The standalone Lab must not offer a header for Hapio's stage-only scheduling policy.");
                Assert.That(content.Find("Category 피버 타임 (Hapio 구미호 전용)"), Is.Null,
                    "Fever time (#334) is tuned in Hapio's fox tab alone, too.");
                foreach (var header in content.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .Where(button => button.name.StartsWith("Category ")).ToArray())
                    header.onClick.Invoke();
                Assert.That(ui.valueInputs[184].transform.parent.gameObject.activeSelf, Is.True,
                    "The ordinary Lab flash controls really expanded, so this is not an all-folded check.");
                foreach (int index in timeline.FieldIndices)
                    Assert.That(ui.valueInputs[index].transform.parent.gameObject.activeSelf, Is.False,
                        "Expanding every Lab group cannot reveal the Hapio-only schedule field " + index);
                foreach (int index in new[] { LabConfig.FeverTimeSecondsField, LabConfig.FeverTimeRecoveryField })
                    Assert.That(ui.valueInputs[index].transform.parent.gameObject.activeSelf, Is.False,
                        "Expanding every Lab group cannot reveal the Hapio-only fever field " + index);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ShakeReleaseFieldsAreOnByDefaultAtTheBottomOfTheFlashRows()
        {
            LabDeveloperField toggle = LabConfig.DeveloperFields[202], count = LabConfig.DeveloperFields[203];
            Assert.That(toggle.Label, Is.EqualTo("섬광 흔들어 풀기 (0 off, 1 on)"));
            Assert.That(toggle.IsToggle, Is.True);
            Assert.That(new[] { toggle.OffText, toggle.OnText }, Is.EqualTo(new[] { "끔", "켬" }));
            Assert.That(count.Label, Is.EqualTo("섬광 최대 흔들기 횟수"));
            Assert.That(count.WholeNumber, Is.True);
            Assert.That(new[] { count.Min, count.Max }, Is.EqualTo(new[] { 2f, 18f }));
            Assert.That((count.Min + count.Max) * .5f, Is.EqualTo(10f), "At most 10 shakes (#330) is the middle of the slider.");

            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
            {
                Assert.That(source.YaguangFlashShakeRelease, Is.True, "Shaking the phone releases the glare by default.");
                Assert.That(source.CaptureDeveloperValues().Skip(202).Take(2), Is.EqualTo(new[] { 1f, 10f }));
            }
            float[] values = config.CaptureDeveloperValues();
            values[202] = 0f; values[203] = 10f; values[189] = 19f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.YaguangFlashShakeRelease, Is.False);
            Assert.That(config.YaguangFlashShakeCount, Is.EqualTo(10));
            Assert.That(config.YaguangFlashGlareSeconds, Is.EqualTo(19f));
            values[203] = 19f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, "At most 18 shakes.");
            values[203] = 6.5f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, "Shakes are whole numbers.");
            values[203] = 6f; values[189] = 20f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, "Glare stays within 19 s.");
            values[189] = 10f; values[190] = 3f;
            Assert.That(config.TryApplyDeveloperValues(values, out error), Is.False, "Recovery stays within 2 s.");
        }

        [Test]
        public void TheShakeCounterCountsStrongShakesOnceEachInAnyUnit()
        {
            foreach (float unit in new[] { 1f, 9.81f })
            {
                // 기준 1배·간격 0.14초로 세는 방식을 확인한다 (기본값은 아래에서 따로 본다).
                var counter = new LabShakeCounter { Threshold = 1f, MinGap = .14f };
                Vector3 still = new Vector3(0f, -1f, 0f) * unit;
                Assert.That(counter.Feed(still, 0f), Is.False, "The first reading only learns gravity.");
                Assert.That(counter.Feed(still + new Vector3(.4f, 0f, 0f) * unit, .05f), Is.False, "A small wobble is not a shake.");
                Assert.That(counter.Feed(still + new Vector3(1.6f, 0f, 0f) * unit, .3f), Is.True, "A strong jerk is one shake.");
                Assert.That(counter.Feed(still + new Vector3(-1.6f, 0f, 0f) * unit, .35f), Is.False, "The same shake is not counted twice.");
                Assert.That(counter.Feed(still + new Vector3(-1.6f, 0f, 0f) * unit, .6f), Is.True, "The swing back counts again.");
                Assert.That(counter.Count, Is.EqualTo(2));
                Assert.That(counter.Feed(new Vector3(float.NaN, 0f, 0f), .9f), Is.False);
                counter.AddOne();
                Assert.That(counter.Count, Is.EqualTo(3), "The Editor K key adds one.");
                counter.Reset();
                Assert.That(counter.Count, Is.Zero);
            }
            var strict = new LabShakeCounter();
            Assert.That(new[] { strict.Threshold, strict.MinGap }, Is.EqualTo(new[] { 1.6f, .25f }),
                "Default: a small move is not a shake, and one swing is not counted twice (#301 tuning).");
            strict.Feed(new Vector3(0f, -1f, 0f), 0f);
            Assert.That(strict.Feed(new Vector3(1.2f, -1f, 0f), .3f), Is.False, "A light jerk no longer counts.");
            Assert.That(LabYaguangFlashHud.ShakeProgress(3, 6), Is.EqualTo(.5f), "Half the shakes clear half the glare.");
            Assert.That(LabYaguangFlashHud.ShakeProgress(9, 6), Is.EqualTo(1f));
            Assert.That(LabYaguangFlashHud.ShakeProgress(1, 0), Is.Zero);
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
        public void AFlashOnHostZeroLeavesTheOtherScreenFreeForItsOwnBomb()
        {
            LabConfig config = Config(flash: true, one: true, first: 1f, warning: 0f, bombEvery: 2f);
            bool checkedHost = false;
            for (int seed = 0; seed < 64 && !checkedHost; seed++)
            {
                var model = new LabModel();
                model.Begin("flash-zero-screen-" + seed, new ulong[] { 0, 20 }, 0d, config);
                var flashed = model.Snapshot(1d);
                if (flashed.flashLastTarget != 0) continue;
                Assert.That(flashed.flashCount, Is.EqualTo(1u));
                Assert.That(flashed.flashLastTargetAll, Is.False);
                var during = model.Snapshot(2d);
                var bomb = during.orbs.Single(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd);
                Assert.That(bomb.owner, Is.EqualTo(20ul),
                    "Host zero is a single busy screen, not an ALL flash that blocks both screens.");
                checkedHost = true;
            }
            Assert.That(checkedHost, Is.True);
        }

        [Test]
        public void HostZeroCanReceiveTheFirstFlashWhenItIsTheOnlyFreeScreen()
        {
            LabConfig config = Config(flash: true, one: true, first: 15f, bombEvery: 10f);
            bool checkedHost = false;
            for (int seed = 0; seed < 64 && !checkedHost; seed++)
            {
                var model = new LabModel();
                model.Begin("flash-zero-only-free-" + seed, new ulong[] { 0, 20 }, 0d, config);
                var before = model.Snapshot(10d);
                if (before.orbs.Single(orb => orb.kind == LabOrbKind.Bomb).owner != 20ul) continue;
                var warning = model.Snapshot(13.6d);
                Assert.That(warning.FlashWarningOn(13.6d), Is.True);
                Assert.That(warning.flashTarget, Is.Zero);
                Assert.That(warning.flashTargetAll, Is.False);
                var flashed = model.Snapshot(15d);
                Assert.That(flashed.LastFlashHit(0), Is.True);
                Assert.That(flashed.LastFlashHit(20), Is.False);
                checkedHost = true;
            }
            Assert.That(checkedHost, Is.True);
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
            LabSnapshot resting = model.Snapshot(55.5d);
            Assert.That(resting.flashCount, Is.Zero, "It does not go off late, without a warning.");
            Assert.That(resting.flashAt, Is.EqualTo(-1d), "The screens rest 3 s after the penalty (#271).");
            Assert.That(model.Snapshot(57.9d).flashAt, Is.EqualTo(-1d));
            LabSnapshot after = model.Snapshot(58d);
            Assert.That(new[] { after.flashWarningAt, after.flashAt }, Is.EqualTo(new[] { 58d, 59.5d }),
                "The full 1.5 s warning starts 3 s after the penalty is over.");
            Assert.That(model.Snapshot(59.5d).flashCount, Is.EqualTo(1u));
        }

        [Test]
        public void AFlashThatEndsBeforeTheComboWarningGoesAheadAsPlanned()
        {
            var model = new LabModel();
            // Warning 18.5 s, flash 20 s, glare and recovery over at 24.5 s, 3 s rest to 27.5 s: just past the combo warning at 27 s.
            model.Begin("flash-before-combo", Three, 0d, Config(flash: true, first: 20f, combo: true));
            Assert.That(model.Snapshot(18.6d).FlashWarningOn(18.6d), Is.True);
            LabSnapshot flashed = model.Snapshot(20d);
            Assert.That(flashed.flashCount, Is.EqualTo(1u));
            Assert.That(flashed.flashLastAt, Is.EqualTo(20d));
            Assert.That(model.Snapshot(26d).ComboWarningOn(26d), Is.False);
            Assert.That(model.Snapshot(27d).ComboWarningOn(27d), Is.False, "Still resting at its planned warning time.");
            LabSnapshot warned = model.Snapshot(27.5d);
            Assert.That(new[] { warned.comboWarningAt, warned.comboNextAt }, Is.EqualTo(new[] { 27.5d, 30.5d }),
                "The combo waits only for the 3 s rest after the flash, then warns in full.");
            Assert.That(model.Snapshot(30.5d).comboActive, Is.True);
        }

        [Test]
        public void AFlashThatStartsFirstTakesItsTurnAndTheComboWaitsForIt()
        {
            var model = new LabModel();
            // Flash warning from 23.5 s, flash at 25 s, glare and recovery over at 29.5 s. The combo would warn from 27 s.
            // Whoever starts first goes first (#271): the flash no longer steps aside for a combo that is merely coming up.
            model.Begin("flash-into-combo", Three, 0d, Config(flash: true, first: 25f, combo: true));
            LabSnapshot early = model.Snapshot(10d);
            Assert.That(early.flashAt, Is.EqualTo(25d), "The flash comes on time.");
            Assert.That(early.comboNextAt, Is.EqualTo(-1d), "The combo is not announced: it will wait for the flash.");
            Assert.That(model.Snapshot(23.6d).FlashWarningOn(23.6d), Is.True);
            Assert.That(model.Snapshot(25d).flashCount, Is.EqualTo(1u));
            LabSnapshot during = model.Snapshot(27.5d);
            Assert.That(during.ComboWarningOn(27.5d), Is.False, "No combo warning over the glare.");
            Assert.That(during.comboActive, Is.False);
            model.Snapshot(29.4d);
            Assert.That(model.Snapshot(29.6d).ComboWarningOn(29.6d), Is.False, "3 s rest after the flash (#271).");
            Assert.That(model.Snapshot(32.4d).ComboWarningOn(32.4d), Is.False);
            LabSnapshot warned = model.Snapshot(32.6d);
            Assert.That(new[] { warned.comboWarningAt, warned.comboNextAt }, Is.EqualTo(new[] { 32.6d, 35.6d }),
                "Once the flash and its rest are over, the combo's full warning, then the combo.");
            Assert.That(model.Snapshot(35.6d).comboActive, Is.True);
        }

        [Test]
        public void AWaitingFlashKeepsNewBombsOffSoItIsNotPushedBackAgainAndAgain()
        {
            var model = new LabModel();
            // Bombs every 10 s, each out for 15 s, so some screen nearly always has one. The flash is due at 15 s.
            model.Begin("flash-queue", Three, 0d, Config(flash: true, first: 15f, bombEvery: 10f));
            Assert.That(GourdBombs(model.Snapshot(10d)), Is.EqualTo(1));
            Assert.That(model.Snapshot(13.6d).flashAt, Is.EqualTo(-1d), "Held by the first bomb.");
            Assert.That(GourdBombs(model.Snapshot(20d)), Is.EqualTo(1), "The second bomb is skipped while the flash waits for its turn.");
            model.Snapshot(25d);
            LabSnapshot resting = model.Snapshot(25.5d);
            Assert.That(GourdBombs(resting), Is.Zero);
            Assert.That(resting.flashAt, Is.EqualTo(-1d), "That screen rests 3 s after its bomb.");
            LabSnapshot warned = model.Snapshot(28d);
            Assert.That(new[] { warned.flashWarningAt, warned.flashAt }, Is.EqualTo(new[] { 28d, 29.5d }),
                "Right after that rest the flash takes its turn.");
            Assert.That(model.Snapshot(29.5d).flashCount, Is.EqualTo(1u));
            Assert.That(GourdBombs(model.Snapshot(33d)), Is.Zero, "Nothing new during the glare and recovery (over at 34 s)...");
            Assert.That(GourdBombs(model.Snapshot(36.9d)), Is.Zero, "...nor during the 3 s rest after it.");
            Assert.That(GourdBombs(model.Snapshot(39.9d)), Is.Zero, "The bombs due at 20 s and 30 s were skipped, not kept (#311).");
            Assert.That(GourdBombs(model.Snapshot(40d)), Is.EqualTo(1), "Then the next one drops on its own beat.");
        }

        [TestCase("round-a")]
        [TestCase("round-b")]
        [TestCase("round-c")]
        public void WithTheDefaultsTheRoundOpensEarlyRestsBetweenEventsAndFlashesAsOftenAsCombos(string id)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            float[] values = config.CaptureDeveloperValues();
            values[155] = 1f; // 야광귀 on: combo and flash with the default gourd bombs and swipe blocks
            values[184] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var model = new LabModel();
            model.Begin(id, Three, 0d, config);
            LabSnapshot last = null;
            var open = new Dictionary<ulong, bool>();
            var closedAt = new Dictionary<ulong, double>();
            double firstEvent = -1d, shortestRest = double.PositiveInfinity;
            for (double at = 0d; at <= 180d; at += .1d)
            {
                last = model.Snapshot(at);
                foreach (ulong player in Three)
                {
                    bool now = ScreenBusy(last, player, at);
                    bool was = open.TryGetValue(player, out bool before) && before;
                    if (now && firstEvent < 0d) firstEvent = at;
                    if (now && !was && closedAt.TryGetValue(player, out double closed))
                        shortestRest = System.Math.Min(shortestRest, at - closed);
                    if (!now && was) closedAt[player] = at;
                    open[player] = now;
                }
            }
            Assert.That(firstEvent, Is.InRange(5d, 9d), "The round opens with the yaguang's first flash warning at 7 s (#271).");
            Assert.That(shortestRest, Is.GreaterThan(2.85d), "Every screen rests about 3 s between two events (#271).");
            Assert.That(last.flashCount, Is.EqualTo(3u), "A flash about every 60 s, as set.");
            Assert.That(last.comboCount, Is.InRange(2u, 3u));
            Assert.That(System.Math.Abs((int)last.flashCount - (int)last.comboCount), Is.InRange(0, 1),
                "Neither event crowds the other out (#271).");
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
        public void ABombDueDuringAFlashIsSkippedAndTheNextFallsOnItsOwnBeat()
        {
            var model = new LabModel();
            // Flash: warning 22.5 s, flash 24 s, glare and recovery over at 28.5 s. The gourd's bomb is due at 25 s.
            model.Begin("flash-then-bomb", Three, 0d, Config(flash: true, first: 24f, bombEvery: 25f));
            Assert.That(model.Snapshot(22.6d).FlashWarningOn(22.6d), Is.True);
            Assert.That(model.Snapshot(24d).flashCount, Is.EqualTo(1u));
            Assert.That(GourdBombs(model.Snapshot(25d)), Is.Zero, "No bomb while the flash is open (#271).");
            Assert.That(GourdBombs(model.Snapshot(28.4d)), Is.Zero);
            Assert.That(GourdBombs(model.Snapshot(28.6d)), Is.Zero, "The screens rest 3 s after the flash (#271).");
            Assert.That(GourdBombs(model.Snapshot(31.5d)), Is.Zero, "A gimmick nobody can take is skipped, not kept (#311).");
            Assert.That(GourdBombs(model.Snapshot(49.9d)), Is.Zero);
            LabSnapshot after = model.Snapshot(50d);
            Assert.That(GourdBombs(after), Is.EqualTo(1), "The next one drops on its own beat.");
            Assert.That(after.orbs.Single(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd).createdAt,
                Is.EqualTo(50d));
        }

        [Test]
        public void AFlashWaitsWhileTheGourdsBombIsStillOnABoard()
        {
            var model = new LabModel();
            // The bomb drops at 30 s and blasts at the end of its 15 s fuse (45 s). The everyone-flash is due at 40 s.
            model.Begin("bomb-then-flash", Three, 0d, Config(flash: true, first: 40f, bombEvery: 30f));
            Assert.That(GourdBombs(model.Snapshot(30d)), Is.EqualTo(1));
            LabSnapshot held = model.Snapshot(38.6d);
            Assert.That(held.flashAt, Is.EqualTo(-1d), "No eyes while the bomb is out on one of the screens.");
            Assert.That(model.Snapshot(44.9d).flashCount, Is.Zero);
            model.Snapshot(45d);
            LabSnapshot resting = model.Snapshot(46d);
            Assert.That(GourdBombs(resting), Is.Zero);
            Assert.That(resting.flashAt, Is.EqualTo(-1d), "That screen rests 3 s after its bomb (#271).");
            LabSnapshot after = model.Snapshot(48d);
            Assert.That(new[] { after.flashWarningAt, after.flashAt }, Is.EqualTo(new[] { 48d, 49.5d }),
                "Once the bomb is gone and the screen has rested, the full warning, then the flash.");
        }

        [Test]
        public void AComboWaitsWhileTheGourdsBombIsStillOnABoard()
        {
            var model = new LabModel();
            // The bomb drops at 20 s and is gone at 35 s. The combo (on every screen) is due to warn at 27 s.
            model.Begin("bomb-then-combo", Three, 0d, Config(combo: true, bombEvery: 20f));
            Assert.That(model.Snapshot(20d).comboNextAt, Is.EqualTo(-1d), "Not announced while the bomb is out.");
            LabSnapshot held = model.Snapshot(30d);
            Assert.That(held.comboActive, Is.False, "No combo over the bomb (#271).");
            Assert.That(held.ComboWarningOn(30d), Is.False);
            model.Snapshot(35d);
            Assert.That(model.Snapshot(35.5d).ComboWarningOn(35.5d), Is.False, "3 s rest after the bomb (#271).");
            LabSnapshot warned = model.Snapshot(38d);
            Assert.That(new[] { warned.comboWarningAt, warned.comboNextAt }, Is.EqualTo(new[] { 38d, 41d }));
            Assert.That(model.Snapshot(41d).comboActive, Is.True);
        }

        [Test]
        public void EachScreenHoldsOneEventSoASecondBombGoesToAFreeScreen()
        {
            var model = new LabModel();
            // Bombs every 5 s with a 15 s fuse: the first is still out when the second comes (#271, one event per screen).
            model.Begin("bomb-per-screen", Three, 0d, Config(flash: true, first: 61f, bombEvery: 5f));
            ulong firstScreen = model.Snapshot(5d).orbs
                .Single(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd).owner;
            LabSnapshot two = model.Snapshot(10d);
            LabOrbState[] bombs = two.orbs.Where(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd).ToArray();
            Assert.That(bombs.Length, Is.EqualTo(2), "Another screen is free, so the second bomb still drops.");
            Assert.That(bombs[0].owner, Is.Not.EqualTo(bombs[1].owner), "Never two bombs on one screen.");
            Assert.That(GourdBombs(model.Snapshot(15d)), Is.EqualTo(3));
            // At 20 s every screen still has a bomb when the fourth is due, so it is skipped (#311); the first then blasts.
            Assert.That(GourdBombs(model.Snapshot(20d)), Is.EqualTo(2), "The fourth was skipped; the first one is gone.");
            Assert.That(GourdBombs(model.Snapshot(23d)), Is.EqualTo(2), "A skipped bomb does not wait for a free screen (#311).");
            LabOrbState fifth = model.Snapshot(25d).orbs.Where(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd)
                .OrderBy(orb => orb.createdAt).Last();
            Assert.That((fifth.createdAt, fifth.owner), Is.EqualTo((25d, firstScreen)),
                "The fifth drops on its own beat on the screen that has rested since its bomb blasted.");
        }

        [Test]
        public void AOnePlayerFlashPicksAScreenWithoutAnEvent()
        {
            var model = new LabModel();
            // A bomb sits on one screen from 10 s to 25 s; a one-player flash warns from 13.5 s.
            model.Begin("one-flash-free-screen", Three, 0d, Config(flash: true, one: true, first: 15f, bombEvery: 10f));
            ulong holder = model.Snapshot(10d).orbs.Single(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd).owner;
            LabSnapshot warned = model.Snapshot(13.6d);
            Assert.That(warned.FlashWarningOn(13.6d), Is.True, "Other screens are free, so the flash goes ahead.");
            Assert.That(warned.flashTarget, Is.Not.EqualTo(holder), "Not on the screen that has the bomb.");
            Assert.That(model.Snapshot(15d).flashLastTarget, Is.EqualTo(warned.flashTarget));
        }

        // 기본값(눈부심 7초 + 회복 2초)으로 이 사람 화면에 이벤트가 열려 있는지
        private static bool ScreenBusy(LabSnapshot snapshot, ulong player, double at) =>
            snapshot.orbs.Any(orb => orb.owner == player && orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd)
            || snapshot.SwipeBlockOn(player) || snapshot.FlashWarningOn(at) || snapshot.ComboWarningOn(at) || snapshot.comboActive
            || (snapshot.flashLastAt >= 0d && at < snapshot.flashLastAt + 9d
                && (snapshot.flashLastTargetAll || snapshot.flashLastTarget == player))
            || snapshot.players.Any(state => state.id == player && (state.comboPenaltyUntil > at || state.comboRewardUntil > at));

        private static int GourdBombs(LabSnapshot snapshot) =>
            snapshot.orbs.Count(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd);

        [Test]
        public void AShortIntervalStillLeavesTheGlareRecoveryAndNextWarningApart()
        {
            var model = new LabModel();
            model.Begin("flash-gap", Three, 0d, Config(flash: true, interval: 20f, warning: 6f, glare: 13f, recovery: 2f));
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
        public void TheCountdownShowsTheSecondsLeftOnlyOnTheTargetsScreen()
        {
            var snapshot = new LabSnapshot
            {
                phase = LabPhase.Playing, flashWarningAt = 27d, flashAt = 30d, flashTarget = 20, flashTargetAll = false
            };
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 26.9d, 20), Is.Zero, "Not before the warning.");
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 27d, 20), Is.EqualTo(3));
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 27.5d, 20), Is.EqualTo(3));
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 28d, 20), Is.EqualTo(2));
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 29.99d, 20), Is.EqualTo(1));
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 30d, 20), Is.Zero, "Gone when it flashes.");
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 28d, 10), Is.Zero, "Only the target counts down.");
            snapshot.flashTargetAll = true;
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 28d, 10), Is.EqualTo(2), "Everyone counts when all are hit.");
            snapshot.phase = LabPhase.Victory;
            Assert.That(LabYaguangFlashHud.CountdownAt(snapshot, 28d, 10), Is.Zero);
        }

        [Test]
        public void ShakingFreeEndsOnlyThatPlayersGlareAndStartsTheRecoveryThere()
        {
            var snapshot = new LabSnapshot
            {
                phase = LabPhase.Playing, flashCount = 2, flashLastAt = 30d, flashLastTargetAll = true,
                players = new[]
                {
                    new LabPlayerState { id = 10, flashReleasedAt = 31d },
                    new LabPlayerState { id = 20 },
                    new LabPlayerState { id = 30, flashReleasedAt = 20d }
                }
            };
            Assert.That(snapshot.FlashReleasedAt(10), Is.EqualTo(31d));
            Assert.That(snapshot.FlashReleasedAt(20), Is.EqualTo(-1d));
            Assert.That(snapshot.FlashReleasedAt(30), Is.EqualTo(-1d), "A release from an earlier flash does not count.");
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 30.5d, 10, 3f, 1.5f), Is.EqualTo(1f));
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 31d, 10, 3f, 1.5f), Is.EqualTo(1f), "Recovery starts at the hit.");
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 31.75d, 10, 3f, 1.5f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 32.5d, 10, 3f, 1.5f), Is.Zero, "Clear well before the 3 s glare ends.");
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 32.5d, 20, 3f, 1.5f), Is.EqualTo(1f), "Others stay dazzled.");
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 32.5d, 30, 3f, 1.5f), Is.EqualTo(1f));
            Assert.That(LabYaguangFlash.GlareAmount(snapshot, 34.5d, 20, 3f, 1.5f), Is.Zero, "Unreleased glare still ends on time.");
        }

        [Test]
        public void GlaredMeansHitByTheLastFlashInsideTheGlareAndNotYetReleased()
        {
            var snapshot = new LabSnapshot
            {
                phase = LabPhase.Playing, flashCount = 1, flashLastAt = 30d, flashLastTarget = 20, flashLastTargetAll = false,
                players = new[] { new LabPlayerState { id = 10 }, new LabPlayerState { id = 20 } }
            };
            Assert.That(LabYaguangFlash.Glared(snapshot, 29.9d, 20, 7f), Is.False, "Not before the flash (nothing to shake off in the warning).");
            Assert.That(LabYaguangFlash.Glared(snapshot, 30d, 20, 7f), Is.True);
            Assert.That(LabYaguangFlash.Glared(snapshot, 30d, 10, 7f), Is.False, "Only the flashed player in ONE mode.");
            Assert.That(LabYaguangFlash.AnyGlared(snapshot, 36.9d, 7f), Is.True, "The yokai stays out while anyone is dazzled.");
            Assert.That(LabYaguangFlash.Glared(snapshot, 37d, 20, 7f), Is.False, "The glare time is the upper limit.");
            Assert.That(LabYaguangFlash.AnyGlared(snapshot, 37d, 7f), Is.False);
            snapshot.players[1].flashReleasedAt = 32d;
            Assert.That(LabYaguangFlash.Glared(snapshot, 33d, 20, 7f), Is.False, "Released by shaking the phone.");
            Assert.That(LabYaguangFlash.AnyGlared(snapshot, 33d, 7f), Is.False, "Nobody left: the yokai may go back in.");
            snapshot.phase = LabPhase.Victory;
            snapshot.players[1].flashReleasedAt = -1d;
            Assert.That(LabYaguangFlash.Glared(snapshot, 31d, 20, 7f), Is.False);
        }

        [Test]
        public void ShakeReportsMatchOneExactFlashAndCannotReleaseAnotherGeneration()
        {
            var model = new LabModel();
            model.Begin("generation", Three, 0d, Config(flash: true, first: 5f,
                interval: 20f, warning: 0f, glare: 10f, recovery: 1f));
            Assert.That(model.Snapshot(6d).flashCount, Is.EqualTo(1u));
            Assert.That(model.TryFlashShakeRelease(10, 0u, 6d), Is.False, "Missing generation is rejected.");
            Assert.That(model.TryFlashShakeRelease(10, 2u, 6d), Is.False, "A future flash is not an approval.");
            Assert.That(model.TryFlashShakeRelease(999, 1u, 6d), Is.False, "Only a participant may release their own glare.");
            Assert.That(model.TryFlashShakeRelease(10, 1u, 6d), Is.True);
            Assert.That(model.TryFlashShakeRelease(10, 1u, 6d), Is.False, "A duplicate cannot release twice.");
            Assert.That(model.Snapshot(6d).FlashReleasedAt(20), Is.EqualTo(-1d), "Another participant is still glared.");
            Assert.That(model.TryFlashShakeRelease(20, 1u, 6d), Is.True);
            Assert.That(model.Snapshot(26d).flashCount, Is.EqualTo(2u));
            Assert.That(model.TryFlashShakeRelease(10, 1u, 26d), Is.False,
                "A delayed first-flash report cannot clear the second flash in the same round.");
            Assert.That(model.TryFlashShakeRelease(10, 3u, 26d), Is.False);
            Assert.That(model.Snapshot(26d).FlashReleasedAt(10), Is.EqualTo(-1d));
            Assert.That(model.TryFlashShakeRelease(10, 2u, 26d), Is.True);
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
            bool combo = false, float warning = 1.5f, float glare = 3f, float recovery = 1.5f, float bombEvery = 0f)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            float[] values = config.CaptureDeveloperValues();
            values[205] = bombEvery; // the common group (#311) every bombEvery s; 0: no gourd bombs
            values[208] = 0f; // and only bombs, no swipe blocks
            values[155] = combo ? 1f : 0f;
            values[156] = 30f; // combo slots at 30 s, warned from 27 s
            values[157] = 60f;
            values[158] = 10f;
            values[164] = 3f;
            values[184] = flash ? 1f : 0f;
            values[185] = one ? 1f : 0f;
            values[186] = 10f; // DEV slider 1–19; the rule tests below set other times directly
            values[187] = interval;
            values[188] = warning;
            values[189] = glare;
            values[190] = recovery;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            JsonUtility.FromJsonOverwrite("{\"yaguangFlashFirstStartSeconds\":"
                + first.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", config);
            return config;
        }
    }
}
