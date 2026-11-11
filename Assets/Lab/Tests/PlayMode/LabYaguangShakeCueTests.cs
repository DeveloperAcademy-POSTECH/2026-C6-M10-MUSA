using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    public sealed class LabYaguangShakeCueTests
    {
        [Test]
        public void CountdownWarningStagesAreVisiblyDistinctWithoutChangingTheFlash()
        {
            float three = LabYaguangFlashHud.WarningStageStrength(3);
            float two = LabYaguangFlashHud.WarningStageStrength(2);
            float one = LabYaguangFlashHud.WarningStageStrength(1);
            Assert.That(three, Is.GreaterThan(0));
            Assert.That(two, Is.GreaterThan(three * 2f));
            Assert.That(one, Is.GreaterThan(two));
            Assert.That(one, Is.EqualTo(1f));
            Assert.That(LabYaguangFlashHud.WarningStageStrength(0), Is.Zero);
        }

        [Test]
        public void FlashIsInstantaneousAndQuicklyDecaysWhileContentFitsTheViewport()
        {
            Assert.That(LabYaguangFlashHud.BurstSeconds, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(LabYaguangFlashHud.FlashPulseStrength(0), Is.EqualTo(1));
            Assert.That(LabYaguangFlashHud.FlashPulseStrength(.5f), Is.EqualTo(.25f).Within(.001f), "The flash has a lingering decay.");
            Assert.That(LabYaguangFlashHud.FlashPulseStrength(1), Is.Zero);
            foreach (var viewport in new[] { new Vector2(349, 765), new Vector2(402, 874), new Vector2(844, 390) })
            {
                Assert.That(LabYaguangShakeCue.FitScale(viewport) * 349f, Is.LessThanOrEqualTo(viewport.x + .001f));
                Assert.That(LabYaguangShakeCue.FitScale(viewport) * 765f, Is.LessThanOrEqualTo(viewport.y + .001f));
            }
        }

        [Test]
        public void WarningUsesTheThreeFigmaNumbersAndHidesAtTheFlash()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var root = new GameObject("Warning");
            try
            {
                var hud = root.AddComponent<LabYaguangFlashHud>();
                hud.Bind(null, canvas.GetComponent<Canvas>());
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(LabYaguangFlashHud).GetMethod("EnsureLayer", flags).Invoke(hud, null);
                var drive = typeof(LabYaguangFlashHud).GetMethod("DriveCountdown", flags);
                var artwork = canvas.transform.Find("Yaguang Flash/Yaguang Flash Countdown/Figma Countdown Artwork").GetComponent<Image>();
                var text = artwork.transform.parent.GetComponent<Text>();
                foreach (int count in new[] { 3, 2, 1 })
                {
                    drive.Invoke(hud, new object[] { count });
                    Assert.That(artwork.sprite, Is.EqualTo(Resources.Load<Sprite>(LabYaguangShakeCue.ArtPath + "Countdown" + count)));
                    Assert.That(text.enabled, Is.False, "Do not draw a second number over the PNG.");
                    Assert.That(artwork.color.a, Is.EqualTo(1f), "Countdowns are fully legible from their first frame.");
                    Assert.That(artwork.raycastTarget, Is.False);
                }
                drive.Invoke(hud, new object[] { 4 });
                Assert.That(text.enabled, Is.True, "Long custom warnings retain dynamic numbers above three.");
                Assert.That(artwork.gameObject.activeSelf, Is.False);
                drive.Invoke(hud, new object[] { 0 });
                Assert.That(text.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void EachShakeStartsOneFlashWithoutAutomaticRepeats()
        {
            Assert.That(LabYaguangFlashHud.FlashProgressAt(-.1f), Is.EqualTo(-1f));
            Assert.That(LabYaguangFlashHud.FlashProgressAt(0f), Is.Zero);
            Assert.That(LabYaguangFlashHud.FlashProgressAt(.55f), Is.EqualTo(-1f));
            Assert.That(LabYaguangFlashHud.FlashProgressAt(1.2f), Is.EqualTo(-1f));
            Assert.That(LabYaguangFlashHud.FlashProgressAt(3.55f), Is.EqualTo(-1f));
            Assert.That(LabYaguangFlashHud.FlashProgressAt(float.PositiveInfinity), Is.EqualTo(-1f));
            Assert.That(LabYaguangFlashHud.FlashProgressAt(0f), Is.Zero, "A newly recognized shake resets the local pulse clock.");
        }

        [Test]
        public void StartsMovingThenHidesGuideAndFillsExactlyTheRecognizedShakes()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var root = new GameObject("Cue", typeof(RectTransform));
            try
            {
                root.transform.SetParent(canvas.transform, false);
                var cue = root.AddComponent<LabYaguangShakeCue>();
                cue.Present(0, 10, .125f, 2f);
                Assert.That(cue.GuideVisible, Is.True);
                Assert.That(Mathf.DeltaAngle(0, cue.PhoneAngle), Is.EqualTo(12f).Within(.01f), "Already moving at zero recognized shakes.");
                Assert.That(cue.FilledCells, Is.Zero);
                cue.Present(6, 10, .125f, 2f);
                Assert.That(cue.GuideVisible, Is.False);
                Assert.That(cue.FilledCells, Is.EqualTo(6));
                Assert.That(Mathf.DeltaAngle(0, cue.PhoneAngle), Is.EqualTo(12f).Within(.01f));
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    Assert.That(image.sprite, Is.Not.Null, image.name);
                    Assert.That(image.raycastTarget, Is.False);
                }
                var text = root.GetComponentInChildren<Text>(true);
                Assert.That(text.text, Is.EqualTo("6 / 10"));
                Assert.That(text.font.fontNames, Does.Contain("Nanum Brush Script"));
                cue.Present(18, 18, 0, 2f);
                Assert.That(cue.FilledCells, Is.EqualTo(18));
                cue.Present(1, 2, 0, 2f);
                int active = 0;
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                    if (image.name.StartsWith("Cell ") && image.gameObject.activeSelf) active++;
                Assert.That(active, Is.EqualTo(2), "Target changes must not leave old cells visible.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(canvas); }
        }
    }
}
