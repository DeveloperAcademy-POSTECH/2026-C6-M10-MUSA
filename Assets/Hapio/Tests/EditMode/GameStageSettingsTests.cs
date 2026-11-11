using System;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameStageSettingsTests
    {
        [Test]
        public void JigwiScreenPatternFieldsAreOwnedOnlyByStageThreeAndCannotLeakFromCommonOrOtherStages()
        {
            foreach (int field in System.Linq.Enumerable.Except(System.Linq.Enumerable.Concat(new[] { 228, 235 }, System.Linq.Enumerable.Range(240, 17)), new[] { 241, 242, 244, 245, 246, 247, 251, 252, 253, 254, 255 }))
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, field), Is.EqualTo(scope == 3),
                        "Field " + field + " in scope " + scope);
            foreach (int field in new[] { 229, 230, 231, 232, 233, 234, 241, 242, 244, 245, 246, 247, 251, 252, 253, 254, 255 })
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, field), Is.False,
                        "Obsolete surface geometry controls remain reserved but hidden: " + field);
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            float[] saved = source.CaptureDeveloperValues();
            using (var settings = new GameStageSettings(source, GameStageCatalog.RequireDefault()))
            {
                float[] common = settings.Capture(0), first = settings.Capture(1), second = settings.Capture(2), territory = settings.Capture(4);
                float[] third = settings.Capture(3); third[228] = 0f; third[240] = 12f; third[235] = 3f; third[256] = .6f;
                Assert.That(settings.TryApply(3, third, out string error), Is.True, error);
                CollectionAssert.AreEqual(common, settings.Capture(0));
                CollectionAssert.AreEqual(first, settings.Capture(1));
                CollectionAssert.AreEqual(second, settings.Capture(2));
                CollectionAssert.AreEqual(territory, settings.Capture(4));
                foreach (int scope in new[] { 0, 1, 2, 4 })
                {
                    foreach (int field in new[] { 240, 256 })
                    {
                        float[] forged = settings.Capture(scope); forged[field] = field == 240 ? 19f : .55f;
                        Assert.That(settings.TryApply(scope, forged, out _), Is.False);
                    }
                }
                LabConfig runtime = settings.CreateStageConfig(3);
                try
                {
                    Assert.That(runtime.JigwiStageTimelineEnabled, Is.False);
                    Assert.That(runtime.JigwiABPersonalDurationSeconds, Is.EqualTo(12f));
                    Assert.That(runtime.JigwiStageFeverMultiplier, Is.EqualTo(3f));
                    Assert.That(runtime.JigwiABFireDepth, Is.EqualTo(.6f).Within(.0001f));
                }
                finally { UnityEngine.Object.DestroyImmediate(runtime); }
                CollectionAssert.AreEqual(saved, source.CaptureDeveloperValues());
            }
        }

        [Test]
        public void SavedStageOrder_HasOnlyUniqueDefaultsAndSharedLabSourceIsUnchanged()
        {
            var source = Resources.Load<LabConfig>("LabConfig");
            float[] before = source.CaptureDeveloperValues();
            var catalog = GameStageCatalog.RequireDefault();
            string[] ids = { "gumiho", "yaguang", "jigwi" };
            LabStageYokai[] yokai = { LabStageYokai.Gumiho, LabStageYokai.Yaguang, LabStageYokai.Jigwi };
            string[] guids = {
                "e5e2128f0ccc743378cf2f906b28eac6",
                "ea9d9f4da035e4df5bf52fcbd4b73d7e",
                "88c8de0df4382480eb45bc3f9010ba91"
            };
            Assert.That(catalog.stages.Length, Is.EqualTo(3));
            Assert.That(GameRunFlow.StageCount, Is.EqualTo(3));
            Assert.That(GameStageSettings.TerritoryScope, Is.EqualTo(4));
            Assert.That(Enum.GetValues(typeof(LabStageYokai)).Length, Is.EqualTo(4));
            Assert.That((int)LabStageYokai.None, Is.Zero);
            for (int i = 0; i < ids.Length; i++)
            {
                GameStageDefinition stage = catalog.Get(i + 1);
                Assert.That(stage.stageId, Is.EqualTo(ids[i]));
                Assert.That(stage.stageIndex, Is.EqualTo(i + 1));
                Assert.That(stage.yokai, Is.EqualTo(yokai[i]));
                Assert.That((int)stage.yokai, Is.EqualTo(i + 1));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(stage)), Is.EqualTo(guids[i]));
                Assert.That(stage.yokaiVisual, Is.Null,
                    "Every default stage uses the original black yokai already inside the shared gourd.");
            }
            using (var settings = new GameStageSettings(source, catalog))
            {
                Assert.That(GameStageSettings.IsFieldVisible(1, 166), Is.False, "A game stage's yokai decides its own gimmicks (#311).");
                Assert.That(settings.Capture(2)[155], Is.EqualTo(1f));
                Assert.That(settings.Capture(2)[184], Is.EqualTo(1f));
                Assert.That(settings.Capture(2)[GameStageSettings.FlashTargetOneField], Is.Zero, "The yaguang flash hits everyone (#301).");
                foreach (int stage in new[] { 1, 2, 3 })
                {
                    float[] values = settings.Capture(stage);
                    Assert.That(values.Length, Is.EqualTo(265));
                    Assert.That(values[205], Is.EqualTo(before[205]), "Common gimmick interval preserved (#311)");
                    Assert.That(values[206], Is.EqualTo(before[206]), "Unique gimmick interval preserved (#311)");
                }
            }
            CollectionAssert.AreEqual(before, source.CaptureDeveloperValues());
        }
        [Test]
        public void CommonEditsReachEveryStage_UniqueToggleEditsSurviveRecreation()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] common = settings.Capture(0); common[2] = 240f;
                Assert.That(settings.TryApply(0, common, out string error), Is.True, error);
                float[] yaguang = settings.Capture(2); yaguang[184] = 0f;
                Assert.That(settings.TryApply(2, yaguang, out error), Is.True, error);
                foreach (int stage in new[] { 1, 2, 3 })
                {
                    var config = settings.CreateStageConfig(stage);
                    try { Assert.That(config.BattleDurationSeconds, Is.EqualTo(240f)); }
                    finally { UnityEngine.Object.DestroyImmediate(config); }
                }
                var repeated = settings.CreateStageConfig(2);
                try { Assert.That(repeated.YaguangFlashEnabled, Is.False, "DEV OFF must not be forced ON at restart"); }
                finally { UnityEngine.Object.DestroyImmediate(repeated); }
            }
        }
        [Test]
        public void BattleDraft_ValidatesWithoutMutationAndCommitsOnlyCommonAndCurrentStage()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] common = settings.Capture(0), first = settings.Capture(1), second = settings.Capture(2);
                float[] values = settings.Capture(2); values[2] = 240f; values[184] = 0f;
                Assert.That(settings.TryApplyBattle(2, values, out string error, false), Is.True, error);
                CollectionAssert.AreEqual(common, settings.Capture(0));
                CollectionAssert.AreEqual(first, settings.Capture(1));
                CollectionAssert.AreEqual(second, settings.Capture(2));
                Assert.That(settings.TryApplyBattle(2, values, out error), Is.True, error);
                Assert.That(settings.Capture(0)[2], Is.EqualTo(240f));
                Assert.That(settings.Capture(1)[2], Is.EqualTo(240f));
                Assert.That(settings.Capture(2)[184], Is.EqualTo(0f));
                Assert.That(settings.Capture(1)[206], Is.EqualTo(first[206]));
                float[] before = settings.Capture(2), foreign = settings.Capture(2); foreign[206] = first[206] + 1f;
                Assert.That(settings.TryApplyBattle(2, foreign, out _), Is.False);
                CollectionAssert.AreEqual(before, settings.Capture(2));
            }
        }
        [Test]
        public void FeverTimeIsTunedOnTheFoxsTabAloneAndReachesItsStage()
        {
            // Fever time (#334) is the fox's alone for now: not Common, not the other yokai, not 2:2.
            foreach (int index in new[] { LabConfig.FeverTimeSecondsField, LabConfig.FeverTimeRecoveryField })
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, index), Is.EqualTo(scope == 1), "Scope " + scope + ": " + index);
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] fox = settings.Capture(1);
                Assert.That(new[] { fox[LabConfig.FeverTimeSecondsField], fox[LabConfig.FeverTimeRecoveryField] },
                    Is.EqualTo(new[] { 20f, 2f }), "Saved defaults: the last 20 s, twice the refill.");
                fox[LabConfig.FeverTimeSecondsField] = 30f; fox[LabConfig.FeverTimeRecoveryField] = 3f;
                Assert.That(settings.TryApply(1, fox, out string error), Is.True, error);
                Assert.That(settings.Capture(3)[LabConfig.FeverTimeSecondsField], Is.EqualTo(20f), "The fox's tab stays its own.");
                var config = settings.CreateStageConfig(1);
                try
                {
                    Assert.That(config.FeverTimeSeconds, Is.EqualTo(30f));
                    Assert.That(config.FeverTimeRecoveryMultiplier, Is.EqualTo(3f));
                }
                finally { UnityEngine.Object.DestroyImmediate(config); }
            }
        }

        [Test]
        public void EachStageKeepsItsOwnCommonGimmicksAndOnlyTheFoxHasUniqueOnes()
        {
            foreach (int index in new[] { 169, 170, 205, 207, 208 })
            {
                Assert.That(GameStageSettings.IsFieldVisible(0, index), Is.False, "Not a Common field: " + index);
                for (int stage = 1; stage <= 3; stage++)
                    Assert.That(GameStageSettings.IsFieldVisible(stage, index), Is.EqualTo(stage != 2 || index == 207 || index == 208), "Stage " + stage + ": " + index);
                Assert.That(GameStageSettings.IsFieldVisible(GameStageSettings.TerritoryScope, index), Is.False);
            }
            foreach (int index in new[] { 171, 172, 206, 209, 210, LabConfig.CharmTargetsAtFiveField, LabConfig.FoxRoundSecondsField,
                LabConfig.FoxFireCountStepMinField, LabConfig.FoxFireCountStepMaxField, LabConfig.FoxRoundSpareMinField,
                LabConfig.FoxRoundSpareMaxField })
            {
                Assert.That(GameStageSettings.IsFieldVisible(1, index), Is.True, "The fox's own gimmick field: " + index);
                Assert.That(GameStageSettings.IsFieldVisible(2, index) || GameStageSettings.IsFieldVisible(3, index), Is.False);
            }
            foreach (int retired in LabConfig.RetiredDeveloperFields)
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, retired), Is.False, "Retired (#311): " + retired);
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] fox = settings.Capture(1);
                fox[205] = 7f; fox[169] = 30f; fox[170] = 70f;
                Assert.That(settings.TryApply(1, fox, out string error), Is.True, error);
                Assert.That(new[] { settings.Capture(1)[205], settings.Capture(1)[169] }, Is.EqualTo(new[] { 7f, 30f }));
                Assert.That(settings.Capture(2)[205], Is.EqualTo(settings.Capture(0)[205]), "The yaguang keeps its schedule instead.");
                Assert.That(settings.Capture(3)[169], Is.EqualTo(settings.Capture(0)[169]));
                float[] uneven = settings.Capture(3); uneven[169] = 30f;
                Assert.That(settings.TryApply(3, uneven, out error), Is.False, "The common chances must add up to 100.");
                var config = settings.CreateStageConfig(1);
                try { Assert.That(config.CommonGimmickInterval, Is.EqualTo(7f)); }
                finally { UnityEngine.Object.DestroyImmediate(config); }
            }
        }
        /// <summary>
        /// #664: the 구미호 fox round gives the same BIG/SMALL as the 야광귀 colour combo: 8 s each, 2.5x and 0.1x. Both stage
        /// tabs show the four rows and each keeps its own values.
        /// </summary>
        [Test]
        public void TheFoxRoundsBigAndSmallMatchTheYaguangCombosAndEachStageKeepsItsOwn()
        {
            foreach (int index in new[] { 159, 160, 162, 163 })
            {
                Assert.That(GameStageSettings.IsComboEffectField(index), Is.True);
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, index), Is.EqualTo(scope == 1 || scope == 2),
                        LabConfig.DeveloperFields[index].Label + " in scope " + scope);
            }
            foreach (int index in new[] { 155, 158, 164 })
                Assert.That(GameStageSettings.IsFieldVisible(1, index), Is.False, "The combo itself stays the 야광귀's: " + index);
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] fox = settings.Capture(1), yaguang = settings.Capture(2);
                foreach (int index in new[] { 159, 160, 162, 163 })
                    Assert.That(fox[index], Is.EqualTo(yaguang[index]), LabConfig.DeveloperFields[index].Label);
                Assert.That((fox[159], fox[162], fox[160], fox[163]), Is.EqualTo((8f, 8f, .1f, 2.5f)));

                fox[162] = 5f;
                Assert.That(settings.TryApply(1, fox, out string error), Is.True, error);
                Assert.That(settings.Capture(2)[162], Is.EqualTo(8f), "The fox tab leaves the 야광귀's alone.");
                var config = settings.CreateStageConfig(1);
                try { Assert.That(config.ComboRewardDurationSeconds, Is.EqualTo(5f)); }
                finally { UnityEngine.Object.DestroyImmediate(config); }
            }
        }

        [Test]
        public void YaguangScheduleIsIsolatedAndCannotEditUnusedPeriodicControls()
        {
            // Its own battle time (211) is retired (#328): the Yaguang runs its schedule inside the common battle time.
            foreach (int index in System.Linq.Enumerable.Range(211, 14))
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, index), Is.EqualTo(scope == 2 && index != 211),
                        "Schedule field " + index + " in scope " + scope);
            foreach (int index in new[] { 169, 170, 205, 156, 157, 186, 187 })
                Assert.That(GameStageSettings.IsFieldVisible(2, index), Is.False, "Unused periodic field: " + index);
            foreach (int index in new[] { 155, 184, 207, 208 })
                Assert.That(GameStageSettings.IsFieldVisible(2, index), Is.True, "Actual scheduled gimmick switch: " + index);
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] first = settings.Capture(1), third = settings.Capture(3), yaguang = settings.Capture(2);
                yaguang[212] = 110f; yaguang[213] = 12f;
                Assert.That(settings.TryApply(2, yaguang, out string error), Is.True, error);
                CollectionAssert.AreEqual(first, settings.Capture(1));
                CollectionAssert.AreEqual(third, settings.Capture(3));
                var config = settings.CreateStageConfig(2);
                try
                {
                    Assert.That(config.BattleDurationSeconds, Is.EqualTo(settings.Capture(0)[2]), "The common battle time (#328).");
                    Assert.That(config.YaguangTimelineSabotageEndSeconds, Is.EqualTo(110f));
                    Assert.That(config.YaguangTimelineUniqueSeconds(0), Is.EqualTo(12f));
                }
                finally { UnityEngine.Object.DestroyImmediate(config); }
                float[] unused = settings.Capture(2); unused[205] += 1f;
                Assert.That(settings.TryApply(2, unused, out _), Is.False);
                CollectionAssert.AreEqual(yaguang, settings.Capture(2));
            }
        }

        [Test]
        public void InactiveSettingsAreShownOnNoTabButCommonStillCarriesThem()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            float[] saved = source.CaptureDeveloperValues();
            Assert.That(LabConfig.InactiveDeveloperFields, Is.EqualTo(new[] { 0, 1, 41, 43, 48, 52, 53, 54, 72, 73 }));
            using (var settings = new GameStageSettings(source, GameStageCatalog.RequireDefault()))
            {
                foreach (int index in LabConfig.InactiveDeveloperFields)
                    for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    {
                        Assert.That(GameStageSettings.IsFieldShown(scope, index, settings.Capture(scope)), Is.False,
                            "No tab shows " + index + " (scope " + scope + ")");
                        Assert.That(GameStageSettings.IsFieldVisible(scope, index), Is.EqualTo(scope == 0),
                            "Hiding the row keeps its scope: Common still carries " + index);
                        Assert.That(settings.Capture(scope)[index], Is.EqualTo(saved[index]), "Scope " + scope + ": " + index);
                    }
                // A saved form or a battle restart that sets one (tests use Player Max HP as a restart marker) still works.
                float[] battle = settings.Capture(1); battle[48] = 219f;
                Assert.That(settings.TryApplyBattle(1, battle, out string error), Is.True, error);
                for (int stage = 1; stage <= 3; stage++)
                {
                    var config = settings.CreateStageConfig(stage);
                    try
                    {
                        Assert.That(config.PlayerMaxHp, Is.EqualTo(219), "Stage " + stage);
                        Assert.That(new[] { config.MonsterMaxHp, config.Damage, config.PotionHeal }, Is.EqualTo(new[] { 1000, 20, 10 }));
                        Assert.That(config.GenerationChances()[2] + config.GenerationChances()[4], Is.Zero,
                            "Stage " + stage + " makes no relay orb or potion.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(config); }
                }
            }
            CollectionAssert.AreEqual(saved, source.CaptureDeveloperValues());
        }

        [Test]
        public void JigwiTabFoldsWhatItsTimelineReplacesWhileTheFormHasItOn()
        {
            int[] replaced = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(System.Linq.Enumerable.Range(178, 6),
                System.Linq.Enumerable.Concat(System.Linq.Enumerable.Range(192, 4), new[] { 205 })));
            for (int i = 0; i < LabConfig.DeveloperFields.Count; i++)
                Assert.That(GameStageSettings.IsReplacedByJigwiTimeline(i), Is.EqualTo(System.Array.IndexOf(replaced, i) >= 0), "Field " + i);
            Assert.That(GameStageSettings.JigwiTimelineField, Is.EqualTo(228));
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] jigwi = settings.Capture(3);
                Assert.That(jigwi[228], Is.EqualTo(1f), "Jigwi's 120-second timeline is on by default.");
                float[] off = (float[])jigwi.Clone(); off[228] = 0f;
                foreach (int index in replaced)
                {
                    Assert.That(GameStageSettings.IsFieldVisible(3, index), Is.True, "Still editable in Jigwi's tab: " + index);
                    Assert.That(GameStageSettings.IsFieldShown(3, index, jigwi), Is.False, "Folded while 228 is on: " + index);
                    Assert.That(GameStageSettings.IsFieldShown(3, index, off), Is.True, "Shown once 228 is off: " + index);
                }
                Assert.That(GameStageSettings.IsFieldShown(3, 228, jigwi) && GameStageSettings.IsFieldShown(3, 228, off), Is.True,
                    "The switch itself always shows.");
                foreach (int index in new[] { 169, 170, 207, 208, 240, 243 })
                    Assert.That(GameStageSettings.IsFieldShown(3, index, jigwi), Is.True, "The timeline still uses " + index);
                Assert.That(GameStageSettings.IsFieldShown(1, 205, settings.Capture(1)), Is.True, "The fox keeps its own interval.");
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    for (int i = 0; i < LabConfig.DeveloperFields.Count; i++)
                        if (scope != 3 || !GameStageSettings.IsReplacedByJigwiTimeline(i))
                            Assert.That(GameStageSettings.IsFieldShown(scope, i, jigwi), Is.EqualTo(GameStageSettings.IsFieldVisible(scope, i)
                                && !LabConfig.IsInactiveDeveloperField(i)), "Scope " + scope + ": " + i);

                // Folding changes nothing about what the tab may apply.
                jigwi[178] = 2f; jigwi[205] = 15f;
                Assert.That(settings.TryApply(3, jigwi, out string error), Is.True, error);
                var config = settings.CreateStageConfig(3);
                try
                {
                    Assert.That(config.JigwiStageTimelineEnabled, Is.True);
                    Assert.That(config.JigwiFireTargetCount, Is.EqualTo(2));
                    Assert.That(config.CommonGimmickInterval, Is.EqualTo(15f));
                }
                finally { UnityEngine.Object.DestroyImmediate(config); }
            }
        }

        [Test]
        public void FiveColorPaintScaleIsCommonToEveryStageWhileTheFlashRewardCountStaysTheYaguangs()
        {
            for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
            {
                Assert.That(GameStageSettings.IsFieldVisible(scope, 237), Is.EqualTo(scope == 0), "Paint scale in scope " + scope);
                Assert.That(GameStageSettings.IsFieldVisible(scope, 236), Is.EqualTo(scope == 2), "Reward count in scope " + scope);
            }
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            float[] saved = source.CaptureDeveloperValues();
            using (var settings = new GameStageSettings(source, GameStageCatalog.RequireDefault()))
            {
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(settings.Capture(scope)[237], Is.EqualTo(saved[237]), "Scope " + scope);
                float[] yaguang = settings.Capture(2); yaguang[237] = 2.5f;
                Assert.That(settings.TryApply(2, yaguang, out _), Is.False, "The Yaguang tab no longer owns it.");
                float[] common = settings.Capture(0); common[237] = 2.5f;
                Assert.That(settings.TryApply(0, common, out string error), Is.True, error);
                for (int stage = 1; stage <= 3; stage++)
                {
                    var config = settings.CreateStageConfig(stage);
                    try { Assert.That(config.YaguangFiveColorOrbPaintScale, Is.EqualTo(2.5f), "Stage " + stage + "'s fever orbs"); }
                    finally { UnityEngine.Object.DestroyImmediate(config); }
                }
                Assert.That(settings.Capture(2)[236], Is.EqualTo(saved[236]));
                float[] battle = settings.Capture(1); battle[237] = 1.5f;
                Assert.That(settings.TryApplyBattle(1, battle, out error), Is.True, error);
                for (int scope = 0; scope <= 3; scope++)
                    Assert.That(settings.Capture(scope)[237], Is.EqualTo(1.5f), "A fox battle edit reaches scope " + scope);
            }
            CollectionAssert.AreEqual(saved, source.CaptureDeveloperValues());
        }

        [Test]
        public void HighResolutionPresentationIsCommonToAllStagesAndTheTerritoryRoom()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            float[] saved = source.CaptureDeveloperValues();
            using (var settings = new GameStageSettings(source, GameStageCatalog.RequireDefault()))
            {
                var before = new float[4][];
                for (int stage = 1; stage <= GameStageSettings.TerritoryScope; stage++) before[stage - 1] = settings.Capture(stage);
                Assert.That(GameStageSettings.IsFieldVisible(0, 200), Is.True);
                Assert.That(GameStageSettings.IsFieldVisible(0, 201), Is.True);
                float[] common = settings.Capture(0); common[200] = 0f; common[201] = 1f;
                Assert.That(settings.TryApply(0, common, out string error), Is.True, error);
                for (int stage = 1; stage <= GameStageSettings.TerritoryScope; stage++)
                {
                    var config = settings.CreateStageConfig(stage);
                    try
                    {
                        Assert.That(config.HighResolutionPaintEnabled, Is.False);
                        Assert.That(config.HighResolutionPaintColumns, Is.EqualTo(2048));
                        CollectionAssert.AreEqual(System.Linq.Enumerable.Take(before[stage - 1], 200),
                            System.Linq.Enumerable.Take(settings.Capture(stage), 200));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(config); }
                }
                float[] battle = settings.Capture(GameStageSettings.TerritoryScope);
                battle[200] = 1f; battle[201] = 0f;
                Assert.That(settings.TryApplyBattle(GameStageSettings.TerritoryScope, battle, out error), Is.True, error);
                for (int stage = 0; stage <= GameStageSettings.TerritoryScope; stage++)
                    Assert.That(new[] { settings.Capture(stage)[200], settings.Capture(stage)[201] }, Is.EqualTo(new[] { 1f, 0f }));
            }
            CollectionAssert.AreEqual(saved, source.CaptureDeveloperValues());
        }

        [Test]
        public void EveryStageAndTheTerritoryRoomFillOneBarCellPerOrb()
        {
            // The battle's stamina bar art is shared by every mode (#305), so a full bar must make as many orbs as it
            // has cells: otherwise one orb empties more or less than one cell.
            var mask = new Texture2D(2, 2);
            int cells = 0;
            try
            {
                Assert.That(mask.LoadImage(System.IO.File.ReadAllBytes("Assets/Lab/Art/UI/StaminaBarFillMask.png")), Is.True);
                for (int x = 0, row = mask.height / 2; x < mask.width; x++)
                    if (mask.GetPixel(x, row).a > .5f && (x == 0 || mask.GetPixel(x - 1, row).a <= .5f)) cells++;
            }
            finally { UnityEngine.Object.DestroyImmediate(mask); }
            Assert.That(cells, Is.EqualTo(6), "Six cells, one per orb (#328).");
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
                for (int scope = 1; scope <= GameStageSettings.TerritoryScope; scope++)
                {
                    var config = settings.CreateStageConfig(scope);
                    try
                    {
                        Assert.That(config.StaminaMax / config.GenerateCost, Is.EqualTo((float)cells), "Scope " + scope);
                        Assert.That(config.StaminaStart, Is.EqualTo(config.StaminaMax), "Scope " + scope + " starts full");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(config); }
                }
        }

        [Test]
        public void ScopeForgeryAndInvalidInput_AreRejectedAtomically_AndCapturesAreCopies()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] before = settings.Capture(0), forged = settings.Capture(0); forged[184] = 1f;
                Assert.That(settings.TryApply(0, forged, out _), Is.False);
                CollectionAssert.AreEqual(before, settings.Capture(0));
                float[] invalid = settings.Capture(3); invalid[192] = float.NaN;
                Assert.That(settings.TryApply(3, invalid, out _), Is.False);
                before[2] = -123;
                Assert.That(settings.Capture(0)[2], Is.GreaterThan(0));
            }
            Assert.That(GameStageSettings.IsFieldVisible(0, 152), Is.False, "Retired by the gimmick groups (#311).");
            Assert.That(GameStageSettings.IsFieldVisible(0, 153), Is.True, "How long a swipe block lasts stays common.");
            Assert.That(GameStageSettings.IsFieldVisible(1, 169), Is.True, "Each stage sets its own common gimmick chances (#311).");
            Assert.That(GameStageSettings.IsFieldVisible(0, 169), Is.False);
            Assert.That(GameStageSettings.IsFieldVisible(2, 184), Is.True);
            for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                Assert.That(GameStageSettings.IsFieldVisible(scope, GameStageSettings.FlashTargetOneField), Is.False,
                    "In the game the flash always hits everyone (#301): the one-player target is never offered.");
            Assert.That(GameStageSettings.IsFieldVisible(2, 202), Is.True, "Flash shake release (#301) is a yaguang setting.");
            Assert.That(GameStageSettings.IsFieldVisible(2, 203), Is.True);
            Assert.That(GameStageSettings.IsFieldVisible(0, 202), Is.False);
            Assert.That(GameStageSettings.IsFieldVisible(2, 200), Is.False, "High resolution paint (#299) stays common.");
            Assert.That(GameStageSettings.IsFieldVisible(3, 192), Is.True);
        }
        [Test]
        public void FlashShakeAndCharmWarningKeepDistinctStageScopesAfterIntegration()
        {
            Assert.That(GameStageSettings.IsFieldVisible(1, 204), Is.True);
            Assert.That(GameStageSettings.IsFieldVisible(2, 204), Is.False);
            Assert.That(GameStageSettings.IsFieldVisible(0, 204), Is.False);
            Assert.That(GameStageSettings.IsFieldVisible(GameStageSettings.TerritoryScope, 204), Is.False);
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] charm = settings.Capture(1);
                float[] flash = settings.Capture(2);
                charm[204] = 4.5f;
                Assert.That(settings.TryApply(1, charm, out string error), Is.True, error);
                flash[202] = 0f; flash[203] = 10f;
                Assert.That(settings.TryApply(2, flash, out error), Is.True, error);
                Assert.That(settings.Capture(1)[204], Is.EqualTo(4.5f));
                Assert.That(settings.Capture(1)[202], Is.EqualTo(charm[202]));
                Assert.That(settings.Capture(1)[203], Is.EqualTo(charm[203]));
                Assert.That(settings.Capture(2)[204], Is.EqualTo(flash[204]));
                Assert.That(settings.Capture(2)[202], Is.Zero);
                Assert.That(settings.Capture(2)[203], Is.EqualTo(10f));
            }
        }

        [Test]
        public void RawStreakCorrectionIsCommonToEveryStageAndTheTerritoryRoom()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            float[] saved = source.CaptureDeveloperValues();
            foreach (int index in new[] { 225, 226, 227 })
            {
                Assert.That(GameStageSettings.IsTerritoryOwned(index), Is.False);
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, index), Is.EqualTo(scope == 0),
                        "Generation field " + index + " belongs only in Common.");
            }
            using (var settings = new GameStageSettings(source, GameStageCatalog.RequireDefault()))
            {
                var before = new float[4][];
                for (int stage = 1; stage <= GameStageSettings.TerritoryScope; stage++)
                {
                    before[stage - 1] = settings.Capture(stage);
                    Assert.That(System.Linq.Enumerable.Take(System.Linq.Enumerable.Skip(before[stage - 1], 225), 3), Is.EqualTo(new[] { 1f, 75f, 3f }));
                }
                float[] common = settings.Capture(0);
                common[225] = 0f; common[226] = 40f; common[227] = 7f;
                Assert.That(settings.TryApply(0, common, out string error), Is.True, error);
                for (int stage = 1; stage <= GameStageSettings.TerritoryScope; stage++)
                {
                    var config = settings.CreateStageConfig(stage);
                    try
                    {
                        Assert.That(config.RawStreakCorrectionEnabled, Is.False);
                        Assert.That(config.RawStreakOppositeChancePercent, Is.EqualTo(40));
                        Assert.That(config.RawStreakMaximum, Is.EqualTo(7));
                        CollectionAssert.AreEqual(System.Linq.Enumerable.Take(before[stage - 1], 225),
                            System.Linq.Enumerable.Take(settings.Capture(stage), 225));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(config); }
                    float[] forged = settings.Capture(stage); forged[226] = 41f;
                    Assert.That(settings.TryApply(stage, forged, out _), Is.False,
                        "Per-stage and territory tabs cannot replace the Common generation correction.");
                }
                float[] battle = settings.Capture(GameStageSettings.TerritoryScope);
                battle[225] = 1f; battle[226] = 100f; battle[227] = 1f;
                Assert.That(settings.TryApplyBattle(GameStageSettings.TerritoryScope, battle, out error, false), Is.True, error);
                Assert.That(settings.Capture(0), Is.EqualTo(common), "A battle draft must not commit its shared settings.");
                Assert.That(settings.TryApplyBattle(GameStageSettings.TerritoryScope, battle, out error), Is.True, error);
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(System.Linq.Enumerable.Take(System.Linq.Enumerable.Skip(settings.Capture(scope), 225), 3), Is.EqualTo(new[] { 1f, 100f, 1f }));
            }
            CollectionAssert.AreEqual(saved, source.CaptureDeveloperValues());
        }
    }
}
