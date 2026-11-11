using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>
    /// 매혹 화면 효과 (#664): the charmed player's own screen goes pink at the edges and heart bubbles pop up from the
    /// bottom and rise; when the charm ends the pink fades and the hearts already rising go on and fade out.
    /// </summary>
    public sealed class LabCharmHeartRainTests
    {
        private GameObject root;
        private LabCharmHeartRain rain;
        private RectTransform charmHud;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Charm heart rain test canvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            // The charm's own gauge and "정신 차림!" layer (LabCharmHud) the effect stays under.
            charmHud = new GameObject("Charm", typeof(RectTransform)).GetComponent<RectTransform>();
            charmHud.SetParent(root.transform, false);
            var host = new GameObject("Charm heart rain");
            host.transform.SetParent(root.transform, false);
            rain = host.AddComponent<LabCharmHeartRain>();
            rain.Bind(null, root.GetComponent<Canvas>(), new System.Random(5));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CharmedTheScreenGoesPinkAndHeartsRiseThenItAllGoes()
        {
            yield return null;
            rain.Present(false, .1f);
            Assert.That(rain.Layer == null || !rain.Layer.gameObject.activeSelf, Is.True, "Nothing while no one is charmed.");

            for (int i = 0; i < 10; i++) rain.Present(true, .1f);
            Assert.That(rain.PinkShown, Is.EqualTo(1f), "Fully pink within a moment.");
            Assert.That(rain.HeartsRising, Is.GreaterThanOrEqualTo(3), "About three and a half hearts a second.");
            Assert.That(rain.AnyImageTakesTouches, Is.False, "It never takes a touch.");
            Assert.That(rain.Layer.GetSiblingIndex(), Is.EqualTo(charmHud.GetSiblingIndex() - 1),
                "Right under the charm's gauge and \"정신 차림!\".");

            float[] before = rain.HeartHeights().ToArray();
            rain.Present(true, .2f);
            float[] after = rain.HeartHeights().Take(before.Length).ToArray();
            for (int i = 0; i < before.Length; i++)
                Assert.That(after[i], Is.GreaterThan(before[i]), "Each heart rises.");
            Assert.That(before.Min(), Is.LessThan(.5f), "They come up from the bottom.");

            for (int i = 0; i < 4; i++) rain.Present(false, .1f);
            Assert.That(rain.PinkShown, Is.Zero, "The pink fades once the charm ends.");
            int left = rain.HeartsRising;
            rain.Present(false, .1f);
            Assert.That(rain.HeartsRising, Is.LessThanOrEqualTo(left), "No new hearts once awake.");
            for (int i = 0; i < 40; i++) rain.Present(false, .1f);
            Assert.That(rain.HeartsRising, Is.Zero, "The rest rise out and are gone.");
            Assert.That(rain.Layer.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TheRisingHeartsAreTheHeartArtAlone()
        {
            Sprite heart = Resources.Load<Sprite>(LabCharmHeartRain.HeartPath);
            Assert.That(heart, Is.Not.Null, "Resources/" + LabCharmHeartRain.HeartPath);
            yield return null;
            for (int i = 0; i < 5; i++) rain.Present(true, .2f);
            Assert.That(rain.HeartsRising, Is.GreaterThan(0));
            foreach (Image art in rain.Layer.GetComponentsInChildren<Image>(false))
                if (art.name == "Heart Art") Assert.That(art.sprite, Is.SameAs(heart), "A heart, not the bubble.");
            Assert.That(rain.Layer.GetComponentsInChildren<Image>(false).Any(image => image.name == "Face"), Is.False,
                "No face on a heart.");
        }
    }
}
