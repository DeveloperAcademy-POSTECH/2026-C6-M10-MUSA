using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class SealRunSummaryTests
    {
        [Test]
        public void ThreeStagesUseActualTimesAndTheSelectedLocalPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/StageStories/Views/SealRunSummary.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var summary = root.GetComponent<SealRunSummary>();
                var stages = new[] { Stage(84.6,30), Stage(130.6,27), Stage(117.6,19) };
                summary.Bind(stages, 10);
                Assert.That(summary.stageTimes.Select(x=>x.text), Is.EqualTo(new[]{"01:24","02:10","01:57"}));
                Assert.That(summary.totalTime.text, Is.EqualTo("05:32"), "Sum precision before formatting, never copy the design's example total.");
                Assert.That(summary.contributions.Select(x=>x.text), Is.EqualTo(new[]{"30%","27%","19%"}));
                string appraisal = summary.appraisal.text;
                summary.Bind(stages, 20);
                Assert.That(summary.contributions.Select(x=>x.text), Is.EqualTo(new[]{"70%","73%","81%"}));
                Assert.That(summary.appraisal.text, Is.EqualTo(appraisal), "Refreshing values must not reroll the appraisal.");
                Assert.That(SealRunSummary.Appraisals, Does.Contain(appraisal));
                summary.Bind(new[]{stages[0]}, 999);
                Assert.That(summary.totalTime.text, Is.EqualTo("—"));
                Assert.That(summary.stageTimes[1].text, Is.EqualTo("—"));
                Assert.That(summary.contributions[0].text, Is.EqualTo("—"));
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static LabResultStage Stage(double seconds, int mine) => new LabResultStage(new[]{
            new LabPlayerState { id=10,damageTotal=mine }, new LabPlayerState { id=20,damageTotal=100-mine }}, seconds);
        [Test]
        public void MissingAndZeroDamageAreDistinctAndTimeDoesNotWrapAtAnHour()
        {
            Assert.That(SealRunSummary.Contribution(null,10), Is.EqualTo("—"));
            Assert.That(SealRunSummary.Contribution(new LabResultStage(new[]{new LabPlayerState{id=10}}, 0),10), Is.EqualTo("0%"));
            Assert.That(SealRunSummary.FormatTime(3661), Is.EqualTo("61:01"));
            Assert.That(SealRunSummary.FormatTime(double.NaN), Is.EqualTo("00:00"));
            Assert.That(SealRunSummary.Appraisals.Distinct().Count(), Is.EqualTo(10));
        }
        [Test]
        public void SummaryRequiresContinueAndExitsThroughSharedCloudBeforeEnding()
        {
            var ending = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/StageStories/GameEnding.asset");
            var view = ending.cuts[0].viewPrefab;
            Assert.That(view.GetComponent<SealRunSummary>(), Is.Not.Null);
            Assert.That(view.requireContinueButton && view.waitForEntrance, Is.True);
            Assert.That(view.continueButton, Is.Not.Null);
            Assert.That(view.exitCloudWipe, Is.Not.Null);
            Assert.That(ending.cuts[1].cutId, Is.EqualTo("Ending_01"));
            Assert.That(view.beats.Count(x=>x.motion==StoryVisualBeat.Motion.SpinSeal), Is.EqualTo(3));
            Assert.That(view.beats.Last().motion, Is.EqualTo(StoryVisualBeat.Motion.SealStamp));
            var root = Object.Instantiate(view.gameObject);
            try
            {
                var instance = root.GetComponent<StoryCutView>();
                instance.Sample(0);
                Assert.That(instance.beats[0].GetComponent<CanvasGroup>().alpha, Is.Zero);
                instance.Sample(instance.EntranceDuration);
                foreach(var beat in instance.beats)
                {
                    Assert.That(beat.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(beat.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
