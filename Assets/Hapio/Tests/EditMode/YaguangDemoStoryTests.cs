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
    public sealed class YaguangDemoStoryTests
    {
        private const string DemoRoot = "Assets/Hapio/Resources/Hapio/Demo/Yaguang";
        private const string OriginalRoot = "Assets/Hapio/Story/StageStories";

        [Test]
        public void DemoSummaryUsesOnlyTheCanonicalStageTwoTimeAndEachPlayersRealContribution()
        {
            var prefab = Require<GameObject>(DemoRoot + "/Views/SealRunSummary.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var summary = root.GetComponent<SealRunSummary>();
                Assert.That(summary.stageIndices, Is.EqualTo(new[] { 2 }));
                Assert.That(summary.stageTimes, Has.Length.EqualTo(1));
                Assert.That(summary.contributions, Has.Length.EqualTo(1));
                var canonical = new[] { Stage(999, 99), Stage(84.6, 30), Stage(888, 88) };
                summary.Bind(canonical, 10);
                Assert.That(summary.stageTimes[0].text, Is.EqualTo("01:24"));
                Assert.That(summary.totalTime.text, Is.EqualTo("01:24"), "Other canonical stage slots never enter the demo total.");
                Assert.That(summary.contributions[0].text, Is.EqualTo("30%"));
                summary.Bind(new[] { null, canonical[1], null }, 20);
                Assert.That(summary.contributions[0].text, Is.EqualTo("70%"));
                Assert.That(summary.totalTime.text, Is.EqualTo("01:24"), "Unplayed Gumiho and Jigwi do not make a valid demo total incomplete.");
                summary.Bind(new LabResultStage[3], 10);
                Assert.That(summary.stageTimes[0].text, Is.EqualTo("—"));
                Assert.That(summary.contributions[0].text, Is.EqualTo("—"));
                Assert.That(summary.totalTime.text, Is.EqualTo("—"));
                summary.Bind(new[] { null, Stage(0, 0), null }, 999);
                Assert.That(summary.contributions[0].text, Is.EqualTo("—"), "An unknown local player is never made a zero-work player.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DemoSummaryShowsOneYaguangSealAndKeepsTheExistingContinueAndCloudWipe()
        {
            var root = Require<GameObject>(DemoRoot + "/Views/SealRunSummary.prefab");
            var view = root.GetComponent<StoryCutView>();
            Assert.That(view.requireContinueButton && view.waitForEntrance, Is.True);
            Assert.That(view.continueButton, Is.Not.Null);
            Assert.That(view.exitCloudWipe, Is.Not.Null);
            Assert.That(view.beats.Count(beat => beat.motion == StoryVisualBeat.Motion.SpinSeal), Is.EqualTo(1));
            var texts = root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(text => text.text).ToArray();
            Assert.That(texts, Does.Contain("야광귀 봉인 완료"));
            Assert.That(texts, Does.Contain("야광귀"));
            Assert.That(texts, Does.Not.Contain("세 요괴 봉인 완료"));
            Assert.That(texts, Does.Not.Contain("구미호"));
            Assert.That(texts, Does.Not.Contain("지귀"));
            Assert.That(root.GetComponentsInChildren<Transform>(true)
                .Count(child => child.name.StartsWith("SealedYokai", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(root.GetComponentInChildren<SealRunSummaryPreview>(true), Is.Null);
        }

        [Test]
        public void DemoEndingReusesSixStoryPicturesWithoutClaimingThreeCapturesOrAddingAFakeTitle()
        {
            var original = Require<StorySequence>(OriginalRoot + "/GameEnding.asset");
            var demo = Require<StorySequence>(DemoRoot + "/GameEnding.asset");
            Assert.That(demo.Validate(out string error), Is.True, error);
            Assert.That(demo.cuts, Has.Count.EqualTo(7));
            Assert.That(demo.cuts[0].viewPrefab.GetComponent<SealRunSummary>().stageIndices, Is.EqualTo(new[] { 2 }));
            Assert.That(demo.cuts[1], Is.Not.SameAs(original.cuts[1]));
            Assert.That(demo.cuts[1].viewPrefab, Is.SameAs(original.cuts[1].viewPrefab));
            Assert.That(demo.cuts[1].GetPage(0).dialogue, Is.EqualTo("이야~\n야광귀도 무사히 봉인했네~!"));
            Assert.That(original.cuts[1].GetPage(0).dialogue, Does.Contain("세마리"));
            for (int i = 2; i < 7; i++) Assert.That(demo.cuts[i], Is.SameAs(original.cuts[i]));
            Assert.That(demo.cuts.Select(cut => cut.cutId), Does.Not.Contain("Ending_07"));
            Assert.That(original.cuts[0].viewPrefab.GetComponent<SealRunSummary>().stageIndices, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(original.cuts[0].viewPrefab.beats.Count(beat => beat.motion == StoryVisualBeat.Motion.SpinSeal), Is.EqualTo(3));
            Assert.DoesNotThrow(() => Type.GetType("Hapio.Editor.YaguangDemoStoryAssets, Assembly-CSharp-Editor", true)
                .GetMethod("Validate", BindingFlags.Static | BindingFlags.Public).Invoke(null, null));
        }

        [Test]
        public void FinalRunBindingChangesYaguangControlsForBothRolesWithoutChangingTheSavedPrefab()
        {
            var saved = Require<GameObject>(OriginalRoot + "/Views/Yagwanggui_After_02.prefab");
            var root = Object.Instantiate(saved);
            try
            {
                var controls = root.GetComponent<StoryStageClearControls>();
                Assert.That(controls.finalStage, Is.False, "The normal original Stage2 still has a later yokai.");
                foreach (bool host in new[] { true, false })
                {
                    controls.Present(true, host, true, true);
                    Assert.That(controls.heading.gameObject.activeSelf, Is.False);
                    Assert.That(controls.nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true).text,
                        Is.EqualTo(StoryStageClearControls.FinishLabel));
                    Assert.That(controls.nextSeal.interactable, Is.True, "Both participants explicitly confirm readiness.");
                }
                controls.Present(true, false, false, true);
                Assert.That(controls.nextSeal.interactable, Is.False);
                controls.Present(true, true, true);
                Assert.That(controls.heading.gameObject.activeSelf, Is.True);
                Assert.That(controls.nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true).text,
                    Is.EqualTo(StoryStageClearControls.NextSealLabel));
                Assert.That(saved.GetComponent<StoryStageClearControls>().finalStage, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BuildVariantSelectsTheDemoEndingWithoutReplacingTheNormalSerializedReference()
        {
            var root = new GameObject("Ending variant fixture");
            try
            {
                var adapter = root.AddComponent<GameStageStoryPresentation>();
                adapter.ending = Require<StorySequence>(OriginalRoot + "/GameEnding.asset");
                var selected = typeof(GameStageStoryPresentation).GetProperty("EndingSequence", BindingFlags.Instance | BindingFlags.NonPublic);
                using (LabGameBuildVariant.OverrideForTests(true))
                    Assert.That(selected.GetValue(adapter), Is.SameAs(Resources.Load<StorySequence>(GameStageStoryPresentation.YaguangDemoEndingResource)));
                using (LabGameBuildVariant.OverrideForTests(false))
                    Assert.That(selected.GetValue(adapter), Is.SameAs(adapter.ending));
                Assert.That(AssetDatabase.GetAssetPath(adapter.ending), Is.EqualTo(OriginalRoot + "/GameEnding.asset"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static LabResultStage Stage(double seconds, int mine) => new LabResultStage(new[] {
            new LabPlayerState { id = 10, paintedCellsTotal = mine },
            new LabPlayerState { id = 20, paintedCellsTotal = 100 - mine }
        }, seconds);
        private static T Require<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Generate the explicit demo story copies before running their asset checks: " + path);
            return asset;
        }
    }
}
