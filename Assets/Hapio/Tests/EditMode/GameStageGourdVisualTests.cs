using C6Lab;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>
    /// #507: every story stage names its own baked battle gourd. The stage gourd is a Resources path, not a prefab
    /// reference, so loading the catalog does not load every stage's model. The old preview overlay is gone.
    /// </summary>
    public sealed class GameStageGourdVisualTests
    {
        public static readonly string[] StageResources =
        {
            "Hapio/Gourds/Stage01_Gumiho_Battle", "Hapio/Gourds/Stage02_Yagwanggwi_Battle", "Hapio/Gourds/Stage03_Jigwi_Battle"
        };

        [Test]
        public void EveryStoryStageNamesItsOwnBattleGourd()
        {
            GameStageCatalog catalog = GameStageCatalog.RequireDefault();
            for (int index = 1; index <= 3; index++)
            {
                Assert.That(catalog.Get(index).gourdVisualResource, Is.EqualTo(StageResources[index - 1]), "Stage " + index);
                Assert.That(catalog.Get(index).yokaiVisual, Is.Null, "The decorative override stays empty.");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EachStageGourdLoadsAndPassesTheModelContract(int stage)
        {
            var prefab = Resources.Load<GameObject>(StageResources[stage - 1]);
            Assert.That(prefab, Is.Not.Null);
            var rig = prefab.GetComponent<LabGourdVisualRig>();
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.Validate(out string reason), Is.True, reason);
            Assert.That(rig.yokaiAnimation, Is.Not.Null);
            Assert.That(rig.yokaiProfile, Is.Not.Null);
            Assert.That(rig.yokaiProfile.idleLoop, Is.False, "Closed until a game event, like the black yokai's triggers.");
            Assert.That(rig.ropeSocket, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty, "The looping art Animator is removed.");
            foreach (Transform part in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(part.name, Does.Not.StartWith("Rear_Rope"), "The model's own hanging rope is removed.");
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void TheStageGourdIsAPathAndThePreviewOverlayIsGone()
        {
            var field = typeof(GameStageDefinition).GetField(nameof(GameStageDefinition.gourdVisualResource));
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(GameStageDefinition).Assembly.GetType("Hapio.GameGourdModelPreview"), Is.Null);
            Assert.That(typeof(GameStageDefinition).Assembly.GetType("Hapio.GameGourdModelPreviewSet"), Is.Null);
            Assert.That(Resources.Load<UnityEngine.Object>("Hapio/GourdPreview/GourdModelPreviewSet"), Is.Null);
        }
    }
}
