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
                Assert.That(settings.Capture(1)[166], Is.EqualTo(1f));
                Assert.That(settings.Capture(2)[155], Is.EqualTo(1f));
                Assert.That(settings.Capture(2)[184], Is.EqualTo(1f));
                Assert.That(settings.Capture(2)[GameStageSettings.FlashTargetOneField], Is.Zero, "The yaguang flash hits everyone (#301).");
                foreach (int stage in new[] { 1, 2, 3 })
                {
                    float[] values = settings.Capture(stage);
                    Assert.That(values.Length, Is.EqualTo(202));
                    Assert.That(values[87], Is.EqualTo(before[87]), "Common bomb interval preserved");
                    Assert.That(values[152], Is.EqualTo(before[152]), "Common transfer block interval preserved");
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
                Assert.That(settings.Capture(1)[166], Is.EqualTo(first[166]));
                float[] before = settings.Capture(2), foreign = settings.Capture(2); foreign[166] = 1f;
                Assert.That(settings.TryApplyBattle(2, foreign, out _), Is.False);
                CollectionAssert.AreEqual(before, settings.Capture(2));
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
            Assert.That(GameStageSettings.IsFieldVisible(0, 152), Is.True);
            Assert.That(GameStageSettings.IsFieldVisible(1, 169), Is.False);
            Assert.That(GameStageSettings.IsFieldVisible(2, 184), Is.True);
            for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                Assert.That(GameStageSettings.IsFieldVisible(scope, GameStageSettings.FlashTargetOneField), Is.False,
                    "In the game the flash always hits everyone (#301): the one-player target is never offered.");
            Assert.That(GameStageSettings.IsFieldVisible(2, 200), Is.True, "Flash face release (#301) is a yaguang setting.");
            Assert.That(GameStageSettings.IsFieldVisible(2, 201), Is.True);
            Assert.That(GameStageSettings.IsFieldVisible(0, 200), Is.False);
            Assert.That(GameStageSettings.IsFieldVisible(3, 192), Is.True);
        }
    }
}
