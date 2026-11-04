using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>
    /// #507 PR-1: every stage still uses the white gourd. The stage gourd is a Resources path, not a prefab reference,
    /// so loading the catalog does not load every stage's model.
    /// </summary>
    public sealed class GameStageGourdVisualTests
    {
        [Test]
        public void EveryStoryStageStillUsesTheWhiteGourd()
        {
            GameStageCatalog catalog = GameStageCatalog.RequireDefault();
            for (int index = 1; index <= 3; index++)
            {
                GameStageDefinition stage = catalog.Get(index);
                Assert.That(stage, Is.Not.Null);
                Assert.That(stage.gourdVisualResource, Is.Null.Or.Empty, stage.stageId + " has no stage gourd before PR-2.");
            }
        }

        [Test]
        public void TheStageGourdIsAPathSoTheCatalogDoesNotReferenceModels()
        {
            var field = typeof(GameStageDefinition).GetField(nameof(GameStageDefinition.gourdVisualResource));
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)));
        }
    }
}
