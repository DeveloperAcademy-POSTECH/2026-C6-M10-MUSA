using System;
using System.Collections.Generic;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameYokaiCatalogTests
    {
        private const string CatalogPath = "Assets/Hapio/Content/Yokai/YokaiCatalog.asset";
        private readonly List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyTemporaryObjects()
        {
            for (int i = temporaryObjects.Count - 1; i >= 0; i--)
                if (temporaryObjects[i] != null) UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
            temporaryObjects.Clear();
        }

        [Test]
        public void SavedInventoryHasTenReferenceIdentitiesWithoutPromotingPlannedContent()
        {
            GameYokaiCatalog catalog = SavedCatalog();
            string[] expectedIds = {
                "gumiho", "jangsanbeom", "yaguang", "geogugwi", "eoduksini",
                "dueoksini", "geuseundae", "jigwi", "changgwi", "jihagukdaejeok"
            };
            CollectionAssert.AreEqual(expectedIds, Array.ConvertAll(catalog.entries, entry => entry.contentId));
            var integrated = new List<string>();
            int plannedCount = 0;
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                GameYokaiDefinition entry = catalog.entries[i];
                Assert.That(entry.referenceOrder, Is.EqualTo(i + 1), entry.contentId);
                Assert.That(entry.isBoss, Is.EqualTo(entry.contentId == "jihagukdaejeok"), entry.contentId);
                if (entry.IsIntegrated) integrated.Add(entry.contentId);
                else
                {
                    Assert.That(entry.implementationState, Is.EqualTo(GameYokaiImplementationState.Planned), entry.contentId);
                    plannedCount++;
                }
            }
            CollectionAssert.AreEquivalent(new[] { "gumiho", "yaguang", "jigwi" }, integrated);
            Assert.That(plannedCount, Is.EqualTo(7));
            Assert.That(catalog.TryGet("geuseundae", out GameYokaiDefinition geuseundae), Is.True);
            CollectionAssert.Contains(geuseundae.aliases, "그신새");
            CollectionAssert.Contains(geuseundae.aliases, "그신대");
        }

        [Test]
        public void PlannedEntriesAreAuthoringDataWithoutFakePresentationAssets()
        {
            GameYokaiCatalog catalog = SavedCatalog();
            StringAssert.DoesNotContain("/Resources/", AssetDatabase.GetAssetPath(catalog));
            foreach (GameYokaiDefinition entry in catalog.entries)
            {
                Assert.That(AssetDatabase.GetAssetPath(entry), Does.StartWith("Assets/Hapio/Content/Yokai/"));
                if (entry.implementationState != GameYokaiImplementationState.Planned) continue;
                Assert.That(entry.visualPrefab, Is.Null, entry.contentId);
                Assert.That(entry.portrait, Is.Null, entry.contentId);
                Assert.That(entry.audioClips ?? Array.Empty<AudioClip>(), Is.Empty, entry.contentId);
                Assert.That(entry.effects ?? Array.Empty<GameObject>(), Is.Empty, entry.contentId);
            }
        }

        [Test]
        public void SavedPlayableStagesRetainTheirOrderGuidsAndSharedVisualFallback()
        {
            GameYokaiCatalog inventory = SavedCatalog();
            GameStageCatalog stages = GameStageCatalog.RequireDefault();
            string[] ids = { "gumiho", "yaguang", "jigwi" };
            string[] guids = {
                "e5e2128f0ccc743378cf2f906b28eac6",
                "ea9d9f4da035e4df5bf52fcbd4b73d7e",
                "88c8de0df4382480eb45bc3f9010ba91"
            };
            Assert.That(stages.stages.Length, Is.EqualTo(3));
            for (int i = 0; i < ids.Length; i++)
            {
                GameStageDefinition stage = stages.Get(i + 1);
                Assert.That(stage.stageId, Is.EqualTo(ids[i]));
                Assert.That(stage.stageIndex, Is.EqualTo(i + 1));
                Assert.That((int)stage.yokai, Is.EqualTo(i + 1));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(stage)), Is.EqualTo(guids[i]));
                Assert.That(stage.yokaiVisual, Is.Null, "Existing shared black yokai stays in use.");
                Assert.That(inventory.TryGet(ids[i], out GameYokaiDefinition content), Is.True);
                Assert.That(stage.content, Is.SameAs(content));
                Assert.That(content.IsIntegrated, Is.True);
            }
        }

        [Test]
        public void ContentInventoryDoesNotExtendTheRunOrWriteSharedSettings()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            Assert.That(source, Is.Not.Null);
            float[] before = source.CaptureDeveloperValues();
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(251));
            Assert.That(GameStageSettings.TerritoryScope, Is.EqualTo(4));
            Assert.That(GameRunFlow.StageCount, Is.EqualTo(3));
            using (var settings = new GameStageSettings(source, GameStageCatalog.RequireDefault()))
            {
                for (int stage = 1; stage <= GameRunFlow.StageCount; stage++)
                    Assert.That(settings.Capture(stage).Length, Is.EqualTo(251));
            }
            CollectionAssert.AreEqual(before, source.CaptureDeveloperValues());
        }

        [Test]
        public void DefinitionValidationRejectsMalformedIdsAndAmbiguousAliases()
        {
            GameYokaiDefinition definition = Definition("valid_id", 1);
            foreach (string invalidId in new[] { "", "Gumiho", "two-words", "has space", "구미호" })
            {
                definition.contentId = invalidId;
                Assert.Throws<InvalidOperationException>(() => definition.Validate(), invalidId);
            }
            definition.contentId = "valid_id";
            definition.aliases = new[] { "Alias", " alias " };
            Assert.Throws<InvalidOperationException>(() => definition.Validate());
            definition.aliases = new[] { definition.displayName };
            Assert.Throws<InvalidOperationException>(() => definition.Validate());
        }

        [Test]
        public void CatalogValidationRejectsDuplicateIdsAndReferenceOrders()
        {
            GameYokaiDefinition first = Definition("first", 1);
            GameYokaiDefinition second = Definition("first", 2);
            GameYokaiCatalog catalog = Temporary<GameYokaiCatalog>();
            catalog.entries = new[] { first, second };
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
            second.contentId = "second";
            second.referenceOrder = 1;
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
            second.referenceOrder = 2;
            Assert.DoesNotThrow(() => catalog.Validate(), "The generic inventory is not hard-coded to ten entries.");
        }

        [Test]
        public void CatalogValidationRejectsMissingDefinitions()
        {
            GameYokaiCatalog catalog = Temporary<GameYokaiCatalog>();
            catalog.entries = new[] { Definition("present", 1), null };
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
        }

        [Test]
        public void CatalogLookupDoesNotReplaceAnUnknownIdentityWithAnotherYokai()
        {
            GameYokaiCatalog catalog = SavedCatalog();
            foreach (string missing in new[] { null, "", "missing_yokai", "Gumiho" })
            {
                Assert.That(catalog.TryGet(missing, out GameYokaiDefinition found), Is.False);
                Assert.That(found, Is.Null);
            }
            Assert.That(catalog.TryGet("gumiho", out GameYokaiDefinition gumiho), Is.True);
            Assert.That(gumiho.contentId, Is.EqualTo("gumiho"));
        }

        [Test]
        public void PlayableStageRejectsPlannedContentEvenWhenItsIdMatches()
        {
            GameStageCatalog stages = CloneStages();
            stages.stages[0].content = Definition("gumiho", 1);
            Assert.That(stages.stages[0].content.IsIntegrated, Is.False);
            Assert.Throws<InvalidOperationException>(() => stages.Validate());
        }

        [Test]
        public void PlayableStageRejectsAnIntegratedDefinitionWithAnotherIdentity()
        {
            GameStageCatalog stages = CloneStages();
            stages.stages[0].content = Definition("another_yokai", 1, GameYokaiImplementationState.Integrated);
            Assert.Throws<InvalidOperationException>(() => stages.Validate());
        }

        [Test]
        public void LegacyStagesWithoutOptionalContentReferencesStillValidate()
        {
            GameStageCatalog stages = CloneStages();
            foreach (GameStageDefinition stage in stages.stages) stage.content = null;
            Assert.DoesNotThrow(() => stages.Validate());
        }

        private static GameYokaiCatalog SavedCatalog()
        {
            GameYokaiCatalog catalog = AssetDatabase.LoadAssetAtPath<GameYokaiCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Run the content scaffold authoring tool before validating saved assets.");
            catalog.Validate();
            return catalog;
        }

        private T Temporary<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            temporaryObjects.Add(instance);
            return instance;
        }

        private GameYokaiDefinition Definition(string id, int order,
            GameYokaiImplementationState state = GameYokaiImplementationState.Planned)
        {
            GameYokaiDefinition definition = Temporary<GameYokaiDefinition>();
            definition.contentId = id;
            definition.displayName = "Test Yokai";
            definition.referenceOrder = order;
            definition.implementationState = state;
            return definition;
        }

        private GameStageCatalog CloneStages()
        {
            GameStageCatalog source = GameStageCatalog.RequireDefault();
            GameStageCatalog clone = UnityEngine.Object.Instantiate(source);
            temporaryObjects.Add(clone);
            clone.stages = new GameStageDefinition[source.stages.Length];
            for (int i = 0; i < source.stages.Length; i++)
            {
                clone.stages[i] = UnityEngine.Object.Instantiate(source.stages[i]);
                temporaryObjects.Add(clone.stages[i]);
            }
            return clone;
        }
    }
}
