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
        public void FinalPageHasAnExplicitTemporaryQrAndOneSavedReturnAction()
        {
            var page = Require<GameObject>(Root + "/FinalPage.prefab").GetComponent<StaffDemoFinalPage>();
            var config = Require<StaffDemoFinalPageConfig>(Root + "/FinalPageConfig.asset");
            Assert.That(page, Is.Not.Null);
            Assert.That(page.GetComponent<StoryPageFit>().referenceSize, Is.EqualTo(new Vector2(402, 874)));
            Assert.That(page.placeholder.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(text => text.text.Contains("준비 중")), Is.True);
            Assert.That(page.GetComponentsInChildren<UnityEngine.UI.Button>(true), Has.Length.EqualTo(1));
            Assert.That(config.qrSprite, Is.Null);
            Assert.That(config.destinationUrl, Is.Null.Or.Empty);
            Assert.That(config.HasDestination, Is.False);
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

        private static T Require<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Author the explicit staff demo assets first: " + path);
            return asset;
        }
    }
}
