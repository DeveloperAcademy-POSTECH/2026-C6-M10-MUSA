using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    public sealed class StaffStageDemoStoryTests
    {
        private const string Root = "Assets/Hapio/Resources/Hapio/Demo/Staff";
        private const string Source = "Assets/Hapio/Story/StageStories";
        private const string Art = "Assets/Hapio/Art/UI/Showcase/";
        [TestCase("AfterGumiho"), TestCase("AfterYagwanggui"), TestCase("AfterJigwi")]
        public void StaffAfterstoryRetainsCardRecordsAndAnEditableFinishAction(string name)
        {
            var sequence = Require<StorySequence>(Source + "/" + name + ".asset");
            Assert.That(sequence.Validate(out string error), Is.True, error);
            Assert.That(sequence.cuts[0].viewPrefab.GetComponentInChildren<StoryCardRecords>(true), Is.Not.Null);
            Assert.That(sequence.cuts[0].viewPrefab.GetComponentInChildren<StoryCardFlip>(true), Is.Not.Null);
            var instance = Object.Instantiate(sequence.cuts.Last().viewPrefab.gameObject);
            try
            {
                var finish = instance.GetComponent<StoryStageClearControls>();
                Assert.That(finish, Is.Not.Null);
                Assert.That(finish.nextSeal, Is.Not.Null);
                finish.Present(true, false, true, true);
                Assert.That(finish.nextSeal.gameObject.activeSelf && finish.nextSeal.interactable, Is.True,
                    "Every local participant can finish the single selected stage.");
                Assert.That(finish.nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true).text,
                    Is.EqualTo(StoryStageClearControls.FinishLabel));
                Assert.That(finish.waiting.gameObject.activeSelf, Is.False);
                if (finish.heading != null) Assert.That(finish.heading.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void StaffBuildValidationNeedsAfterstoriesAndQrWithoutRequiringRetiredSummariesOrEndings()
        {
            Type builder = Type.GetType("Hapio.Editor.StaffStageDemoStoryAssets, Assembly-CSharp-Editor", true);
            var requested = new System.Collections.Generic.List<string>();
            Func<string, Object> load = path =>
            {
                requested.Add(path);
                if (path.Contains("/Demo/Staff/Stage") || path.EndsWith("/GameEnding.asset", StringComparison.Ordinal))
                    throw new AssertionException("The direct-QR build must not read a retired ending or summary: " + path);
                return AssetDatabase.LoadAssetAtPath<Object>(path);
            };
            Assert.DoesNotThrow(() => builder.GetMethod("ValidateRuntimeAssets", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { load }));
            Assert.That(requested, Is.EquivalentTo(new[] {
                Source + "/AfterGumiho.asset", Source + "/AfterYagwanggui.asset", Source + "/AfterJigwi.asset",
                Root + "/FinalPage.prefab", Root + "/FinalPageConfig.asset"
            }));
        }

        [Test]
        public void FinalPageMatchesTheFigmaShowcaseAndRetainsOneSavedReturnAction()
        {
            var page = Require<GameObject>(Root + "/FinalPage.prefab").GetComponent<StaffDemoFinalPage>();
            var config = Require<StaffDemoFinalPageConfig>(Root + "/FinalPageConfig.asset");
            Assert.That(page, Is.Not.Null);
            Assert.That(page.GetComponent<StoryPageFit>().referenceSize, Is.EqualTo(new Vector2(402, 874)),
                "The saved page uses Figma UI / 시연용 frame 5706:14518 without recreating game scenes.");
            Assert.That(config.heading, Is.EqualTo("함께 봉인해주어 고맙소!"));
            Assert.That(config.message, Is.EqualTo("아직 남은 요괴들이 있소."));
            Assert.That(config.qrCaption, Is.EqualTo("앱스토어에서 만나보시오."));
            Assert.That(config.returnCaption, Is.EqualTo("수장고로 돌아가기"));
            Assert.That(page.heading.text, Is.EqualTo(config.heading));
            Assert.That(page.message.text, Is.EqualTo(config.message));
            Assert.That(page.qrCaption.text, Is.EqualTo(config.qrCaption));
            Assert.That(page.GetComponentsInChildren<UnityEngine.UI.Button>(true), Has.Length.EqualTo(1));
            Assert.That(page.returnButton.GetComponentInChildren<UnityEngine.UI.Text>(true).text,
                Is.EqualTo(config.returnCaption));
            AssertFigmaBounds(page.returnButton.GetComponent<RectTransform>(), new Rect(27, 768, 349, 58));
            var hit = page.returnButton.GetComponent<UnityEngine.UI.Image>();
            Assert.That(hit, Is.Not.Null, "Keep a separate rectangular click target when the original artwork has transparent padding.");
            Assert.That(hit.raycastTarget, Is.True);
            Assert.That(hit.color.a, Is.Zero, "The input target must not draw another button background.");
            var normal = Require<Sprite>(Art + "ReturnButtonNormal.png");
            var pressed = Require<Sprite>(Art + "ReturnButtonPressed.png");
            var buttonArt = page.returnButton.targetGraphic as UnityEngine.UI.Image;
            Assert.That(buttonArt, Is.Not.Null, "The button uses the exported Figma background, not a generated rounded graphic.");
            Assert.That(buttonArt.transform.parent, Is.EqualTo(page.returnButton.transform));
            Assert.That(buttonArt.sprite, Is.EqualTo(normal));
            Assert.That(buttonArt.color, Is.EqualTo(Color.white), "Do not multiply an already coloured Figma background by a second red tint.");
            Assert.That(buttonArt.raycastTarget, Is.False, "Only the unchanged parent click target handles pointer input.");
            AssertFigmaBounds(buttonArt.rectTransform, new Rect(-.60546875f, -4, 351, 64));
            Assert.That(page.returnButton.transition, Is.EqualTo(UnityEngine.UI.Selectable.Transition.SpriteSwap));
            Assert.That(page.returnButton.spriteState.pressedSprite, Is.EqualTo(pressed));
            Assert.That(page.returnButton.spriteState.highlightedSprite, Is.EqualTo(normal));
            Assert.That(page.returnButton.spriteState.selectedSprite, Is.EqualTo(normal));
            Assert.That(page.returnButton.spriteState.disabledSprite, Is.EqualTo(normal));
            Assert.That(page.GetComponentsInChildren<C6Lab.LabRoundedGraphic>(true), Is.Empty);
            Assert.That(page.GetComponentsInChildren<GameUiFrame>(true), Is.Empty,
                "The saved final page cannot layer Unity-generated shapes over its original Figma artwork.");
            Assert.That(config.qrSprite, Is.Not.Null, "The final page must show the actual Figma QR rather than its retired placeholder.");
            Assert.That(config.HasDestination, Is.True, "Record the actual HTTP(S) destination encoded in the supplied QR image.");
            Assert.That(config.destinationUrl, Is.EqualTo("https://apps.apple.com/kr/app/hap-io/id6827923355?l=en-GB"),
                "The documented destination must match the decoded source QR rather than an invented or stale link.");
            Assert.That(page.qrImage.sprite, Is.EqualTo(config.qrSprite));
            Assert.That(page.qrImage.preserveAspect, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(config.qrSprite), Is.EqualTo(Art + "02cb8.png"));
            AssertFigmaBounds(page.qrImage.rectTransform, new Rect(109.25f, 396.52f, 182.009f, 185.388f));
            var border = page.qrImage.transform.Find("QRBorder")?.GetComponent<UnityEngine.UI.Image>();
            Assert.That(border, Is.Not.Null, "Only the Figma border image surrounds the unchanged raw QR image.");
            Assert.That(border.sprite, Is.EqualTo(Require<Sprite>(Art + "QrBorder.png")));
            Assert.That(border.color, Is.EqualTo(Color.white));
            Assert.That(border.raycastTarget, Is.False);
            AssertFigmaBounds(border.rectTransform, new Rect(-2.431640625f, -1.8515625f, 187, 190));
            var topCrop = page.transform.Find("Page/GourdTop") as RectTransform;
            Assert.That(topCrop, Is.Not.Null, "Preserve the top gourd artwork crop from the source frame.");
            AssertFigmaBounds(topCrop, new Rect(77, 267, 247.421f, 138.201f));
            Assert.That(topCrop.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null);
            Assert.That(page.qrImage.raycastTarget, Is.False, "The QR artwork cannot intercept the saved return action.");
            Assert.That(page.qrImage.gameObject.activeSelf, Is.True);
            Assert.That(page.placeholder.activeSelf, Is.False);
            Assert.That(page.placeholder.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(text => text.text.Contains("준비 중")), Is.True,
                "Keep an explicit fallback when an editor intentionally clears the QR assignment.");
            Assert.DoesNotThrow(() => Type.GetType("Hapio.Editor.StaffStageDemoStoryAssets, Assembly-CSharp-Editor", true)
                .GetMethod("Validate", BindingFlags.Static | BindingFlags.Public).Invoke(null, null));
        }

        [TestCase("https://example.com/feedback", true)]
        [TestCase("http://example.com/feedback", true)]
        [TestCase("file:///private/data", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("temporary", false)]
        public void ReplaceableQrConfigurationDistinguishesARealDestinationFromAPlaceholder(string url, bool expected)
        {
            var config = ScriptableObject.CreateInstance<StaffDemoFinalPageConfig>();
            try { config.destinationUrl = url; Assert.That(config.HasDestination, Is.EqualTo(expected)); }
            finally { Object.DestroyImmediate(config); }
        }

        private static void AssertFigmaBounds(RectTransform rect, Rect bounds)
        {
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(bounds.x).Within(.01f));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(-bounds.y).Within(.01f));
            Assert.That(rect.sizeDelta.x, Is.EqualTo(bounds.width).Within(.01f));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(bounds.height).Within(.01f));
        }

        private static T Require<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Author the explicit staff demo assets first: " + path);
            return asset;
        }
    }
}
