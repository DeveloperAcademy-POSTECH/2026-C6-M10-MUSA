using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>#418: a stamina-rejected drum tap shakes the existing gauge and flashes only its charged part red, then puts it back.</summary>
    public sealed class LabStaminaShortFeedbackTests
    {
        private GameObject root;
        private RectTransform bar;
        private LabShapeFillImage fill;
        private LabStaminaShortFeedback feedback;
        private LabStaminaFeedbackProfile profile;
        private Texture2D texture;
        private Sprite sprite;
        private static readonly Vector2 BarPlace = new Vector2(30f, -12f);

        [SetUp]
        public void CreateExistingStaminaWindow()
        {
            root = new GameObject("Stamina short fixture", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            bar = (RectTransform)new GameObject("Existing Stamina Window", typeof(RectTransform)).transform;
            bar.SetParent(root.transform, false);
            bar.anchoredPosition = BarPlace;
            fill = new GameObject("Existing Blue Fill", typeof(RectTransform)).AddComponent<LabShapeFillImage>();
            fill.transform.SetParent(bar, false);
            fill.rectTransform.sizeDelta = new Vector2(240f, 24f);
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillAmount = .1f; // 12 of 120: the first block is still charging, short of the 20 cost.
            texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            fill.sprite = sprite;
            profile = ScriptableObject.CreateInstance<LabStaminaFeedbackProfile>();
            feedback = root.AddComponent<LabStaminaShortFeedback>(); feedback.enabled = false;
            feedback.SetProfile(profile);
            feedback.Bind(null, fill); // Only Advance moves this fixture's visual clock.
        }

        [TearDown]
        public void DestroyWindow()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (profile != null) Object.DestroyImmediate(profile);
            if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        [Test]
        public void AShortTapShakesTheGaugeAndFlashesOnlyTheChargingPartRedThenPutsItBack()
        {
            Color tint = fill.color;
            feedback.Play();
            feedback.Advance(.05f);
            Assert.That(feedback.Playing, Is.True);
            Assert.That(feedback.ShortCount, Is.EqualTo(1));
            Assert.That(feedback.ShakeOffset.magnitude, Is.GreaterThan(1f), "The gauge visibly jitters.");
            Assert.That(bar.anchoredPosition, Is.EqualTo(BarPlace + feedback.ShakeOffset));
            UnityEngine.UI.Image red = feedback.FlashImage;
            Assert.That(red.gameObject.activeInHierarchy, Is.True);
            Assert.That(red.transform.parent, Is.SameAs(bar));
            Assert.That(red.transform.GetSiblingIndex(), Is.EqualTo(bar.childCount - 1), "Drawn over the gauge's other layers.");
            Assert.That(red.sprite, Is.SameAs(sprite), "Reuse the block silhouette, not an opaque rectangle.");
            Assert.That(red.fillAmount, Is.EqualTo(fill.fillAmount), "Only the block being charged now turns red.");
            Assert.That(red.color.r, Is.GreaterThan(.8f));
            Assert.That(red.color.g, Is.LessThan(.4f));
            Assert.That(red.color.a, Is.GreaterThan(0f));
            Assert.That(red.raycastTarget, Is.False);
            Assert.That(fill.fillAmount, Is.EqualTo(.1f), "The real stamina fill is never changed.");
            Assert.That(fill.color, Is.EqualTo(tint));

            feedback.Advance(1f);
            Assert.That(feedback.Playing, Is.False);
            Assert.That(bar.anchoredPosition, Is.EqualTo(BarPlace), "The gauge returns exactly to its saved place.");
            Assert.That(red.gameObject.activeSelf, Is.False);
            Assert.That(feedback.FlashAlpha, Is.Zero);
        }

        [Test]
        public void TheGaugeSwellsAndTheWarningWithItsRedMarkShakesInTheMiddleThenAllGoes()
        {
            feedback.Play();
            feedback.Advance(.08f);
            Assert.That(feedback.GaugeScale, Is.GreaterThan(1.02f), "#718: the gauge swells a little while it shakes.");
            Assert.That(bar.localScale.x, Is.EqualTo(feedback.GaugeScale).Within(.0001f));
            Assert.That(feedback.WarningVisible, Is.True);
            Assert.That(feedback.WarningLabel, Is.EqualTo(LabStaminaShortFeedback.WarningText));
            Assert.That(feedback.WarningIcon, Is.Not.Null, "The red mark beside the words, as one group.");
            Assert.That(feedback.WarningShake.magnitude, Is.GreaterThan(0f));
            Assert.That(feedback.EdgeAlpha, Is.GreaterThan(0f));
            var words = root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(text => text.text == LabStaminaShortFeedback.WarningText);
            Assert.That(words.raycastTarget, Is.False, "It never takes a touch.");

            feedback.Advance(profile.ShortWarningSeconds);
            Assert.That(feedback.Playing, Is.False);
            Assert.That(feedback.WarningVisible, Is.False);
            Assert.That(feedback.EdgeAlpha, Is.Zero);
            Assert.That(bar.localScale, Is.EqualTo(Vector3.one), "The gauge is its own size again.");
            Assert.That(bar.anchoredPosition, Is.EqualTo(BarPlace));
        }

        [Test]
        public void TheGaugeSwellsEarlyAndSettlesByTheEndOfTheShake()
        {
            Assert.That(LabStaminaShortFeedback.PopAmount(0f, .32f), Is.Zero);
            Assert.That(LabStaminaShortFeedback.PopAmount(.096f, .32f), Is.EqualTo(1f).Within(.001f));
            Assert.That(LabStaminaShortFeedback.PopAmount(.32f, .32f), Is.Zero);
            Assert.That(LabStaminaShortFeedback.WarningPop(0f), Is.EqualTo(.6f).Within(.001f), "Pops in small...");
            Assert.That(LabStaminaShortFeedback.WarningPop(.12f), Is.EqualTo(1.15f).Within(.001f), "...a little big...");
            Assert.That(LabStaminaShortFeedback.WarningPop(.5f), Is.EqualTo(1f), "...then its own size.");
        }

        [Test]
        public void TappingAgainRestartsFromTheOriginalPlaceInsteadOfDrifting()
        {
            feedback.Play();
            feedback.Advance(.1f);
            Vector2 shaken = bar.anchoredPosition;
            Assert.That(shaken, Is.Not.EqualTo(BarPlace));
            feedback.Play();
            Assert.That(feedback.ShortCount, Is.EqualTo(2));
            feedback.Advance(profile.ShortShakeSeconds * .9f);
            Assert.That(feedback.Playing, Is.True, "The second tap starts its own full shake.");
            feedback.Advance(1f);
            Assert.That(bar.anchoredPosition, Is.EqualTo(BarPlace));
        }

        [Test]
        public void TheShakeIsFixedForATimeAndFadesOut()
        {
            Assert.That(LabStaminaShortFeedback.ShakeAt(.1f, .32f, 8f, 32f),
                Is.EqualTo(LabStaminaShortFeedback.ShakeAt(.1f, .32f, 8f, 32f)));
            Assert.That(LabStaminaShortFeedback.ShakeAt(.32f, .32f, 8f, 32f), Is.EqualTo(Vector2.zero));
            Assert.That(LabStaminaShortFeedback.ShakeAt(.02f, .32f, 8f, 32f).magnitude,
                Is.GreaterThan(LabStaminaShortFeedback.ShakeAt(.3f, .32f, 8f, 32f).magnitude));
            Assert.That(LabStaminaShortFeedback.FlashAmount(0f, .4f), Is.Zero);
            Assert.That(LabStaminaShortFeedback.FlashAmount(.06f, .4f), Is.EqualTo(1f).Within(.001f));
            Assert.That(LabStaminaShortFeedback.FlashAmount(.4f, .4f), Is.Zero);
        }

        [Test]
        public void OnlyAGenerateRefusedForStaminaCounts()
        {
            var shortReceipt = new LabActionReceipt { accepted = false, reason = LabModel.InsufficientStaminaReason };
            Assert.That(LabNetwork.IsStaminaShort("generate", shortReceipt, false), Is.True);
            Assert.That(LabNetwork.IsStaminaShort("generate", shortReceipt, true), Is.False, "A purged old-round reply stays quiet.");
            Assert.That(LabNetwork.IsStaminaShort("combine", shortReceipt, false), Is.False);
            Assert.That(LabNetwork.IsStaminaShort("generate",
                new LabActionReceipt { accepted = false, reason = "STORAGE_FULL" }, false), Is.False, "A full board is not a stamina problem.");
            Assert.That(LabNetwork.IsStaminaShort("generate", new LabActionReceipt { accepted = true }, false), Is.False);
        }

        [Test]
        public void TheStaminaShortSoundIsInItsOwnFolder()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(LabStaminaShortSound.ClipFolder);
            Assert.That(clips.Select(clip => clip.name), Is.EqualTo(new[] { "StaminaShort" }));
            Assert.That(clips[0].length, Is.EqualTo(.61f).Within(.02f));
        }
    }
}
