using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace C6Lab.Tests
{
    public sealed class LabOrbSpriteDesignTests
    {
        [TestCase(LabOrbKind.Yin, LabOrbElement.None, "Yin")]
        [TestCase(LabOrbKind.Yang, LabOrbElement.None, "Yang")]
        [TestCase(LabOrbKind.Combined, LabOrbElement.Fire, "Fire")]
        [TestCase(LabOrbKind.Combined, LabOrbElement.Water, "Water")]
        [TestCase(LabOrbKind.Combined, LabOrbElement.Wood, "Wood")]
        [TestCase(LabOrbKind.Combined, LabOrbElement.Metal, "Metal")]
        [TestCase(LabOrbKind.Combined, LabOrbElement.Earth, "Earth")]
        public void SavedConfigConnectsEachKindToItsOriginalArt(LabOrbKind kind, LabOrbElement element, string name)
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            Sprite sprite = config.OrbAppearance.SpriteFor(kind, element);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo("Assets/Lab/Art/Orbs/Sprites/" + name + ".png"));
            Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * .5f));
            Assert.That(sprite.texture.mipmapCount, Is.EqualTo(1));
        }
    }
}
