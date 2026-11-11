using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Hapio.Tests
{
    public sealed class StoryStageReadyTests
    {
        [Test]
        public void FiveSeatsGetDistinctRepeatableRunnersForEveryStage()
        {
            for (int stage=1;stage<=3;stage++)
            {
                var order=Enumerable.Range(0,5).Select(seat=>StoryStageReadyView.RunnerIndex("fixture",stage,seat,5)).ToArray();
                Assert.That(order.Distinct().Count(),Is.EqualTo(5));
                Assert.That(order,Is.EquivalentTo(new[]{0,1,2,3,4}));
                Assert.That(order,Is.EqualTo(Enumerable.Range(0,5).Select(seat=>StoryStageReadyView.RunnerIndex("fixture",stage,seat,5))));
            }
        }
        [Test]
        public void SavedViewUsesDistinctExistingRunnersAndSeparatesHostAndMemberControls()
        {
            var root=new GameObject("Stage ready fixture",typeof(RectTransform));
            try
            {
                var view=StoryStageReadyView.Create(root.transform);
                var players=Enumerable.Range(1,5).Select(id=>new LabPlayerState{id=(ulong)id}).ToArray();
                var names=new System.Collections.Generic.HashSet<string>();
                foreach(var player in players.Reverse())
                {
                    view.BeginWaiting(false,"fixture",2,players.Reverse().ToArray(),player.id);
                    names.Add(view.RunnerName);
                    view.Present(true,false,0,5,false);
                    Assert.That(view.runner.sprite,Is.Not.Null);
                    Assert.That(view.start.gameObject.activeSelf,Is.False);
                    Assert.That(view.memberCaption.gameObject.activeSelf,Is.True);
                }
                Assert.That(names.Count,Is.EqualTo(5));
                view.BeginWaiting(true,"fixture",2,players,1);
                view.Present(true,false,3,5,false);
                Assert.That(view.start.interactable,Is.False);
                Assert.That(view.party.activeSelf,Is.False);
                Assert.That(view.count.text,Is.EqualTo("전원 준비 완료 3/5"));
                view.Present(true,true,5,5,false);
                Assert.That(view.party.activeSelf,Is.True);
                Assert.That(view.runnerSlot.gameObject.activeSelf,Is.False);
                Assert.That(view.start.interactable,Is.True);
                view.ShowQuit(true);view.Present(true,false,4,5,true);
                Assert.That(view.party.activeSelf,Is.True,"The popup preserves the all-ready picture behind it.");
                Assert.That(view.start.interactable,Is.False);
                view.ShowQuit(false);view.Present(true,true,5,5,false,true);
                Assert.That(view.startCaption.text,Is.EqualTo("마치기"));
                view.ResetView();Assert.That(view.IsWaiting || view.IsConfirmingQuit,Is.False);
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
