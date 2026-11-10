using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Hapio.Tests
{
    public sealed class StageStoryAssetTests
    {
        const string Root="Assets/Hapio/Story/StageStories/";
        StorySequence Sequence(string name)=>AssetDatabase.LoadAssetAtPath<StorySequence>(Root+name+".asset");
        [Test] public void SuccessPreviewCombinesThreeIndependentStageGroupsAndTheEnding()
        {
            var all=Sequence("AllStageStories"); Assert.That(all.Validate(out var error),Is.True,error);
            var groups=new[]{Sequence("AfterGumiho"),Sequence("AfterYagwanggui"),Sequence("AfterJigwi"),Sequence("Ending")};
            foreach(var stage in groups.Take(3))Assert.That(stage.cuts.Count,Is.EqualTo(3));
            Assert.That(groups.Last().cuts.Count,Is.EqualTo(7));
            Assert.That(all.cuts,Is.EqualTo(groups.SelectMany(g=>g.cuts).ToList()));
            Assert.That(all.cuts.Count,Is.EqualTo(16));Assert.That(Sequence("Failure").cuts.Count,Is.EqualTo(2));
        }
        [Test] public void AllImportedLayersKeepLocalSpritesAndSourceReferences()
        {
            foreach(var cut in Sequence("AllStageStories").cuts.Concat(Sequence("Failure").cuts))
            {
                Assert.That(cut.viewPrefab.sourceFigmaNodeId,Does.StartWith("4210:"));
                foreach(var image in cut.viewPrefab.GetComponentsInChildren<Image>(true))
                    if(image.GetComponent<Mask>()==null&&image.name!="Card Turn Pivot"&&!image.name.EndsWith(" Action")&&!image.name.StartsWith("Rectangle 175")&&image.name!="Continue Hit Area"&&image.name!="NextSeal")Assert.That(image.sprite,Is.Not.Null,cut.cutId+" / "+image.name);
            }
        }
        [Test] public void EachSealCardKeepsEditableFrontBackAndAnInspectionEvent()
        {
            foreach(var name in new[]{"AfterGumiho","AfterYagwanggui","AfterJigwi"})
            {
                var view=Sequence(name).cuts[0].viewPrefab;var flip=view.GetComponentInChildren<StoryCardFlip>(true);
                Assert.That(flip,Is.Not.Null);Assert.That(flip.front.activeSelf,Is.True);Assert.That(flip.back.activeSelf,Is.False);
                Assert.That(flip.front.GetComponentsInChildren<Text>(true).Length,Is.GreaterThanOrEqualTo(3));
                Assert.That(flip.back.GetComponentsInChildren<Text>(true).Any(t=>t.text=="봉인 기록"),Is.True);
                Assert.That(view.GetComponent<StoryResultActions>(),Is.Not.Null);
                Assert.That(flip.back.GetComponentInChildren<Button>(true).onClick.GetPersistentEventCount(),Is.EqualTo(1));
            }
        }
        [Test] public void EndingUsesExistingDialogueAndPaletteWhileSilentCutsStaySilent()
        {
            var ending=Sequence("Ending");Assert.That(ending.cuts[0].GetPage(0).speaker,Is.EqualTo("불이"));
            Assert.That(ending.cuts[1].GetPage(0).speaker,Is.EqualTo("솔이"));
            Assert.That(ending.cuts[4].GetPage(0).dialogue,Is.EqualTo("이건 ... 우리가 깬게 아닌..데..?"));
            foreach(int i in new[]{2,3,5,6})Assert.That(ending.cuts[i].GetPage(0).dialogue,Is.Empty);
            foreach(var cut in ending.cuts)Assert.That(cut.viewPrefab.hideEmptyDialogue,Is.True);
        }
        [Test] public void FailureGaugeAndActionsAreReadyForResultBinding()
        {
            var view=Sequence("Failure").cuts[1].viewPrefab;var gauge=view.GetComponent<StorySealGauge>();
            Assert.That(gauge.target,Is.EqualTo(.78f).Within(.0001f));Assert.That(gauge.fill.type,Is.EqualTo(Image.Type.Filled));
            Assert.That(gauge.percentage,Is.Not.Null);Assert.That(view.GetComponentsInChildren<Button>(true).Length,Is.EqualTo(2));
        }
    }
}
