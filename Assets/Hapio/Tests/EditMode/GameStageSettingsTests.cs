using System;
using C6Lab;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameStageSettingsTests
    {
        [Test]
        public void SavedStageOrder_HasOnlyUniqueDefaultsAndSharedLabSourceIsUnchanged()
        {
            var source = Resources.Load<LabConfig>("LabConfig");
            float[] before = source.CaptureDeveloperValues();
            var catalog = GameStageCatalog.RequireDefault();
            Assert.That(catalog.Get(1).yokai, Is.EqualTo(LabStageYokai.Gumiho));
            Assert.That(catalog.Get(2).yokai, Is.EqualTo(LabStageYokai.Yaguang));
            Assert.That(catalog.Get(3).yokai, Is.EqualTo(LabStageYokai.Jigwi));
            foreach (int stage in new[] { 1, 2, 3 })
                Assert.That(catalog.Get(stage).yokaiVisual, Is.Null,
                    "Every default stage uses the original black yokai already inside the shared gourd.");
            using (var settings = new GameStageSettings(source, catalog))
            {
                Assert.That(GameStageSettings.IsFieldVisible(1, 166), Is.False, "A game stage's yokai decides its own gimmicks (#311).");
                Assert.That(settings.Capture(2)[155], Is.EqualTo(1f));
                Assert.That(settings.Capture(2)[184], Is.EqualTo(1f));
                Assert.That(settings.Capture(2)[GameStageSettings.FlashTargetOneField], Is.Zero, "The yaguang flash hits everyone (#301).");
                foreach (int stage in new[] { 1, 2, 3 })
                {
                    float[] values = settings.Capture(stage);
                    Assert.That(values.Length, Is.EqualTo(225));
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
        public void EachStageKeepsItsOwnCommonGimmicksAndOnlyTheFoxHasUniqueOnes()
        {
            foreach (int index in new[] { 169, 170, 205, 207, 208 })
            {
                Assert.That(GameStageSettings.IsFieldVisible(0, index), Is.False, "Not a Common field: " + index);
                for (int stage = 1; stage <= 3; stage++)
                    Assert.That(GameStageSettings.IsFieldVisible(stage, index), Is.EqualTo(stage != 2 || index == 207 || index == 208), "Stage " + stage + ": " + index);
                Assert.That(GameStageSettings.IsFieldVisible(GameStageSettings.TerritoryScope, index), Is.False);
            }
            foreach (int index in new[] { 171, 172, 206, 209, 210 })
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
        [Test]
        public void YaguangScheduleIsIsolatedAndCannotEditUnusedPeriodicControls()
        {
            foreach (int index in System.Linq.Enumerable.Range(211, 14))
                for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                    Assert.That(GameStageSettings.IsFieldVisible(scope, index), Is.EqualTo(scope == 2),
                        "Schedule field " + index + " in scope " + scope);
            foreach (int index in new[] { 169, 170, 205, 156, 157, 186, 187 })
                Assert.That(GameStageSettings.IsFieldVisible(2, index), Is.False, "Unused periodic field: " + index);
            foreach (int index in new[] { 155, 184, 207, 208 })
                Assert.That(GameStageSettings.IsFieldVisible(2, index), Is.True, "Actual scheduled gimmick switch: " + index);
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                float[] first = settings.Capture(1), third = settings.Capture(3), yaguang = settings.Capture(2);
                yaguang[211] = 150f; yaguang[212] = 110f; yaguang[213] = 12f;
                Assert.That(settings.TryApply(2, yaguang, out string error), Is.True, error);
                CollectionAssert.AreEqual(first, settings.Capture(1));
                CollectionAssert.AreEqual(third, settings.Capture(3));
                var config = settings.CreateStageConfig(2);
                try
                {
                    Assert.That(config.YaguangTimelineBattleSeconds, Is.EqualTo(150f));
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
            Assert.That(cells, Is.EqualTo(8));
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
    }
}
