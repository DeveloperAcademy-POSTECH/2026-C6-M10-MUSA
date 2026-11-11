using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabYaguangComboArtTests
    {
        [TestCase("하나!", "CountOne")]
        [TestCase("둘!", "CountTwo")]
        [TestCase("셋!", "CountThree")]
        [TestCase("넷!", "CountFour")]
        public void CountdownUsesTheProvidedCompletePng(string word, string asset)
        {
            Assert.That(LabYaguangComboArt.CountPath(word), Is.EqualTo(LabYaguangComboArt.Path + asset));
            var sprite = LabYaguangComboArt.Load(asset);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.width, Is.GreaterThan(100f));
            Assert.That(LabYaguangComboArt.CountPath("안보이지롱~"), Is.Null);
        }
        [TestCase(LabOrbElement.Fire, "Fire")]
        [TestCase(LabOrbElement.Water, "Water")]
        [TestCase(LabOrbElement.Wood, "Wood")]
        [TestCase(LabOrbElement.Metal, "Metal")]
        [TestCase(LabOrbElement.Earth, "Earth")]
        public void EachHostElementHasItsOwnUnmodifiedAuthoredSprite(LabOrbElement element, string name)
        {
            var sprite = LabYaguangComboArt.Orb(element);
            Assert.That(sprite, Is.EqualTo(LabYaguangComboArt.Load(name)));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(96, 98)));
        }
        [TestCase(349f, 765f, 71f, 107f)]
        [TestCase(698f, 1530f, 142f, 214f)]
        public void PanelMatchesTheFigmaPositionAndUniformSize(float width, float height, float left, float top)
        {
            var viewport = new Vector2(width, height);
            Vector2 anchor = LabYaguangComboArt.PanelAnchor(viewport);
            Vector2 size = LabYaguangComboArt.PanelReference.size * LabYaguangComboArt.Fit(viewport);
            Assert.That(anchor.x * width - size.x * .5f, Is.EqualTo(left).Within(.01f));
            Assert.That((1f - anchor.y) * height - size.y * .5f, Is.EqualTo(top).Within(.01f));
            Assert.That(size.x / size.y, Is.EqualTo(207f / 51f).Within(.001f));
        }
        [TestCase(844f, 390f)]
        [TestCase(1080f, 2400f)]
        public void PanelFitsOtherScreenShapesWithoutStretching(float width, float height)
        {
            var viewport = new Vector2(width, height);
            Vector2 anchor = LabYaguangComboArt.PanelAnchor(viewport);
            Vector2 size = LabYaguangComboArt.PanelReference.size * LabYaguangComboArt.Fit(viewport);
            Assert.That(anchor.x * width - size.x * .5f, Is.GreaterThanOrEqualTo(0));
            Assert.That(anchor.x * width + size.x * .5f, Is.LessThanOrEqualTo(width));
            Assert.That(anchor.y * height - size.y * .5f, Is.GreaterThanOrEqualTo(0));
            Assert.That(anchor.y * height + size.y * .5f, Is.LessThanOrEqualTo(height));
        }
        [Test]
        public void PanelSharesTheGaugeCenterAndKeepsItsSlimAspectRatio()
        {
            var gauge = new Rect(-121, 180, 242, 27);
            var panel = LabYaguangComboArt.OverGauge(gauge);
            Assert.That(panel.center, Is.EqualTo(gauge.center));
            Assert.That(panel.width, Is.EqualTo(242f * .98f).Within(.001f));
            Assert.That(panel.height / panel.width, Is.EqualTo(51f / 207f).Within(.001f));
        }
        [Test]
        public void PanelAndFeedbackAssetsHaveTheirNativeExportSizes()
        {
            Assert.That(LabYaguangComboArt.Load("Panel").rect.size, Is.EqualTo(new Vector2(414, 102)));
            Assert.That(LabYaguangComboArt.Load("Check").rect.size, Is.EqualTo(new Vector2(68, 68)));
            Assert.That(LabYaguangComboArt.Load("BarFill").rect.size, Is.EqualTo(new Vector2(600, 14)));
        }
    }
}
