using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class StoryWorkspaceTests
    {
        [System.Serializable] private class Approved { public Frame[] frames; }
        [System.Serializable] private class Frame { public int number; public string cutId; public Page[] pages; }
        [System.Serializable] private class Page { public string speaker, dialogue; }







        [Test]
        public void AnimatedTitleSharesFourCloudsWithTheHomeStartPage()
        {
            var title = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/AnimatedTitle.prefab");
            var drift = title.GetComponent<TitleCloudDrift>();
            Assert.That(drift.clouds.Length, Is.EqualTo(4));
            Assert.That(title.GetComponent<TitleArtCover>().referenceSize, Is.EqualTo(new Vector2(402, 874)),
                "#467: the title art fills screens of any ratio.");
            Assert.That(drift.width, Is.EqualTo(402));
            foreach (var cloud in drift.clouds)
            {
                Assert.That(cloud.speed, Is.InRange(8f, 30f), "#467: a steady one-way drift across the title.");
                var image = cloud.target.GetComponentInChildren<UnityEngine.UI.Image>(true);
                Assert.That(image.sprite.name, Does.StartWith("Cloud"), "#467: the shorter cloud art.");
                Assert.That(cloud.target.sizeDelta.x, Is.LessThanOrEqualTo(300), "Shorter than the old 450-long clouds.");
                Assert.That(image.sprite.rect.width / cloud.target.sizeDelta.x, Is.GreaterThanOrEqualTo(2.8f), "Sharp on a 3x phone.");
                Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(cloud.target.sizeDelta), "Shown whole, not cropped.");
                Assert.That(cloud.target.GetSiblingIndex(), Is.LessThan(title.transform.Find("합이오").GetSiblingIndex()),
                    "Clouds pass behind the name.");
            }
            var topDown = drift.clouds.OrderByDescending(cloud => cloud.target.anchoredPosition.y).ToArray();
            for (int i = 1; i < topDown.Length; i++)
                Assert.That(topDown[i].reverse, Is.Not.EqualTo(topDown[i - 1].reverse),
                    "From top to bottom the clouds take turns going right and left, so they cross.");
            Assert.That(drift.clouds[0].target.anchoredPosition, Is.EqualTo(new Vector2(-30, -313)));
            Assert.That(drift.clouds[1].target.anchoredPosition, Is.EqualTo(new Vector2(175, -85)));
            var home = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Art/UI/GameLobby.prefab").GetComponentInChildren<GameLobbyView>(true);
            Assert.That(home.titlePage.GetComponentInChildren<TitleCloudDrift>(true), Is.Not.Null);
            Assert.That(home.enterButton.gameObject.activeSelf, Is.True);
            Assert.That(home.titlePage.transform.Find("SealPrompt").gameObject.activeSelf, Is.False);
            foreach (var graphic in title.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, "Title artwork must not block Home navigation.");
        }

        [Test]
        public void TitleFlagWavesOverAThreeTimesLandscapeThatNoLongerPaintsIt()
        {
            var title = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/AnimatedTitle.prefab");
            var landscape = (RectTransform)title.transform.Find("Paper Landscape/Artwork");
            Assert.That(landscape.GetComponent<UnityEngine.UI.Image>().sprite.rect.size, Is.EqualTo(new Vector2(1206, 2622)),
                "#467: the 3x export, so phones do not stretch a 1x picture.");
            Assert.That(landscape.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(landscape.sizeDelta, Is.EqualTo(new Vector2(402, 874)));
            var flag = title.transform.Find("Wind Flag");
            Assert.That(flag.GetSiblingIndex(), Is.EqualTo(1), "Over the landscape, under the name and the clouds.");
            string[] order = { "Ribbon Red Yellow", "Ribbon Blue", "Pole", "Banner" };
            Assert.That(flag.childCount, Is.EqualTo(order.Length));
            for (int i = 0; i < order.Length; i++) Assert.That(flag.GetChild(i).name, Is.EqualTo(order[i]));

            var pole = flag.Find("Pole").GetComponent<UnityEngine.UI.Image>();
            Assert.That(pole.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced), "Only the shaft stretches; the finial keeps its shape.");
            Assert.That(pole.sprite.border, Is.EqualTo(new Vector4(0, 24, 0, 212)));
            Assert.That(pole.pixelsPerUnitMultiplier * pole.rectTransform.sizeDelta.x, Is.EqualTo(pole.sprite.rect.width).Within(.01f),
                "The crossbar is drawn at its own proportions.");
            Assert.That(pole.GetComponent<TitleWindWave>(), Is.Null);

            var banner = flag.Find("Banner").GetComponent<TitleWindWave>();
            Assert.That(banner.pinned, Is.EqualTo(new Vector2(.5f, 1)), "Hangs from its rings on the crossbar.");
            Assert.That(banner.lean, Is.LessThan(0), "Leans downwind, the way the ribbons stream.");
            foreach (var name in new[] { "Ribbon Red Yellow", "Ribbon Blue" })
            {
                var ribbon = flag.Find(name).GetComponent<TitleWindWave>();
                Assert.That(ribbon.pinned.x, Is.GreaterThan(.9f), name + " is tied at the pole on its right.");
                Assert.That(ribbon.tip.x, Is.LessThan(.1f));
            }
            foreach (var image in flag.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                Assert.That(Mathf.Max(image.sprite.rect.width, image.sprite.rect.height), Is.LessThanOrEqualTo(1024),
                    image.name + " is trimmed and downsized from the large source.");
                Assert.That(image.raycastTarget, Is.False);
            }
        }

        [Test]
        public void TitleLeavesFallInFrontOfTheCloudsWithTheFlagsGust()
        {
            var title = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/AnimatedTitle.prefab");
            var area = title.transform.Find("Falling Leaves");
            Assert.That(area.GetSiblingIndex(), Is.EqualTo(title.transform.childCount - 1), "#467: in front of the clouds.");
            Assert.That(area.childCount, Is.Zero, "Leaves are made while playing, not saved in the prefab.");
            var leaves = area.GetComponent<TitleFallingLeaves>();
            Assert.That(leaves.sprites.Length, Is.EqualTo(4));
            foreach (var sprite in leaves.sprites)
                Assert.That(Mathf.Max(sprite.rect.width, sprite.rect.height), Is.LessThanOrEqualTo(256), sprite.name + " is downsized.");
            Assert.That(leaves.count, Is.GreaterThan(0));
            Assert.That(leaves.size.x, Is.GreaterThanOrEqualTo(20), "Larger than the leaves painted in the landscape.");
            Assert.That(leaves.wind, Is.GreaterThan(0), "Blown left, the way the ribbons stream.");
            Assert.That(leaves.gustSeconds, Is.EqualTo(title.transform.Find("Wind Flag/Banner").GetComponent<TitleWindWave>().gustSeconds),
                "The flag and the leaves are hit by the same gust.");
        }











        [TestCase(900, 400)]
        [TestCase(402, 874)]
        [TestCase(360, 1000)]
        public void PageFitsViewportWithoutCroppingOrChangingAuthoredCoordinates(float width, float height)
        {
            var parent = new GameObject("Viewport", typeof(RectTransform));
            var page = new GameObject("Page", typeof(RectTransform));
            try
            {
                var viewport = parent.GetComponent<RectTransform>();
                viewport.sizeDelta = new Vector2(width, height);
                page.transform.SetParent(parent.transform, false);
                page.AddComponent<StoryPageFit>().Apply();
                var rect = page.GetComponent<RectTransform>();
                Assert.That(rect.rect.size, Is.EqualTo(new Vector2(402, 874)));
                Assert.That(rect.rect.width * rect.localScale.x, Is.LessThanOrEqualTo(width + .01f));
                Assert.That(rect.rect.height * rect.localScale.y, Is.LessThanOrEqualTo(height + .01f));
                Assert.That(rect.localScale.x, Is.EqualTo(rect.localScale.y));
            }
            finally { Object.DestroyImmediate(parent); }
        }



        [Test]
        public void DuplicateCutIdsAreRejectedBeforePlayback()
        {
            var story = ScriptableObject.CreateInstance<StorySequence>();
            var cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
            var view = new GameObject("View", typeof(RectTransform), typeof(StoryCutView));
            try
            {
                cut.cutId = "same"; cut.viewPrefab = view.GetComponent<StoryCutView>();
                story.cuts.Add(cut); story.cuts.Add(cut);
                Assert.That(story.Validate(out string error), Is.False);
                Assert.That(error, Does.Contain("Duplicate"));
            }
            finally
            {
                Object.DestroyImmediate(view); Object.DestroyImmediate(cut); Object.DestroyImmediate(story);
            }
        }

        [Test]
        public void LayerSamplingReturnsToAuthoredPlacementWithoutCumulativeDrift()
        {
            var obj = new GameObject("Layer", typeof(RectTransform), typeof(StoryLayer));
            try
            {
                var rect = obj.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(17, -39);
                rect.localScale = new Vector3(2, 3, 1);
                var layer = obj.GetComponent<StoryLayer>();
                layer.delay = .2f; layer.enterSeconds = .8f;
                layer.Sample(0); layer.Sample(1); layer.Sample(0); layer.Sample(1);
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(17, -39)));
                Assert.That(rect.localScale, Is.EqualTo(new Vector3(2, 3, 1)));
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
