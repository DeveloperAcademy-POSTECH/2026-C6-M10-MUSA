using System;
using System.Linq;
using System.Reflection;
using C6Lab;
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
        private static readonly string[] Names = { "구미호", "야광귀", "지귀" };

        [TestCase(1), TestCase(2), TestCase(3)]
        public void SelectedStageSummaryUsesOnlyThatCanonicalRecordAndEveryActualParticipantsContribution(int stage)
        {
            var instance = Object.Instantiate(Require<GameObject>(Root + "/Stage" + stage + "/Views/SealRunSummary.prefab"));
            try
            {
                var summary = instance.GetComponent<SealRunSummary>();
                Assert.That(summary.stageIndices, Is.EqualTo(new[] { stage }));
                Assert.That(summary.stageTimes, Has.Length.EqualTo(1));
                Assert.That(summary.contributions, Has.Length.EqualTo(1));
                var rows = new LabResultStage[3];
                rows[stage - 1] = new LabResultStage(new[] {
                    new LabPlayerState { id = 19, paintedCellsTotal = 25 },
                    new LabPlayerState { id = 42, paintedCellsTotal = 35 },
                    new LabPlayerState { id = 71, paintedCellsTotal = 40 }
                }, 83.8);
                foreach (var pair in new[] { (id: 19UL, percent: "25%"), (id: 42UL, percent: "35%"), (id: 71UL, percent: "40%") })
                {
                    summary.Bind(rows, pair.id);
                    Assert.That(summary.stageTimes[0].text, Is.EqualTo("01:23"));
                    Assert.That(summary.totalTime.text, Is.EqualTo("01:23"), "Unplayed stages are not missing from a single-stage route.");
                    Assert.That(summary.contributions[0].text, Is.EqualTo(pair.percent));
                }
                summary.Bind(rows, 999);
                Assert.That(summary.contributions[0].text, Is.EqualTo("—"));
                summary.Bind(new LabResultStage[3], 19);
                Assert.That(summary.totalTime.text, Is.EqualTo("—"));
                Assert.That(instance.GetComponentInChildren<SealRunSummaryPreview>(true), Is.Null);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(1), TestCase(2), TestCase(3)]
        public void EndingKeepsItsSelectedYokaiAndOriginalStoryPicturesWithoutClaimingOtherCaptures(int stage)
        {
            var source = Require<StorySequence>(Source + "/GameEnding.asset");
            var selected = Resources.Load<StorySequence>(GameStageStoryPresentation.StaffDemoEndingResource(stage));
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.Validate(out string error), Is.True, error);
            Assert.That(selected.cuts, Has.Count.EqualTo(7));
            Assert.That(selected.cuts[0].viewPrefab.GetComponent<SealRunSummary>().stageIndices, Is.EqualTo(new[] { stage }));
            Assert.That(selected.cuts[1], Is.Not.SameAs(source.cuts[1]));
            Assert.That(selected.cuts[1].viewPrefab, Is.SameAs(source.cuts[1].viewPrefab));
            Assert.That(selected.cuts[1].GetPage(0).dialogue, Is.EqualTo("이야~\n" + Names[stage - 1] + "도 무사히 봉인했네~!"));
            for (int i = 2; i < 7; i++) Assert.That(selected.cuts[i], Is.SameAs(source.cuts[i]));
            var view = selected.cuts[0].viewPrefab;
            Assert.That(view.requireContinueButton && view.waitForEntrance, Is.True);
            Assert.That(view.continueButton, Is.Not.Null);
            Assert.That(view.exitCloudWipe, Is.Not.Null);
            Assert.That(view.beats.Count(beat => beat.motion == StoryVisualBeat.Motion.SpinSeal), Is.EqualTo(1));
            var captions = view.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(text => text.text).ToArray();
            Assert.That(captions, Does.Contain(Names[stage - 1] + " 봉인 완료"));
            for (int i = 0; i < Names.Length; i++)
                if (i != stage - 1) Assert.That(captions, Does.Not.Contain(Names[i]));
            Assert.That(source.cuts[0].viewPrefab.GetComponent<SealRunSummary>().stageIndices, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(source.cuts[1].GetPage(0).dialogue, Does.Contain("세마리"), "Normal ending stays authored as before.");
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
