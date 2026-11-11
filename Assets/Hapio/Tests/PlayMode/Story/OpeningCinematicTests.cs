#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class OpeningCinematicTests
    {
        GameObject root;
        StorySequence sequence;
        StoryPlayer player;

        [SetUp] public void SetUp()
        {
            root=new GameObject("Cinematic fixture",typeof(RectTransform));root.SetActive(false);
            player=root.AddComponent<StoryPlayer>();player.playOnStart=false;player.enabled=false;player.touchOnly=true;
            player.cutHost=(RectTransform)root.transform;
            var button=new GameObject("Next",typeof(RectTransform),typeof(Button));button.transform.SetParent(root.transform,false);
            player.nextButton=button.GetComponent<Button>();
            sequence=ScriptableObject.CreateInstance<StorySequence>();
            var source=AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/Opening/Opening.asset");
            sequence.cuts=source.cuts.Skip(12).Take(6).ToList();
            root.SetActive(true);Assert.That(player.Play(sequence),Is.True);
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(sequence);}

        [UnityTest] public IEnumerator SilentRunAdvancesWithoutTapsAndWaitsAtTheNextDialogue()
        {
            player.AdvanceTime(10);Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(13));
            player.Next();
            foreach(int frame in new[]{14,15,16})
            {
                Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(frame));
                Assert.That(player.AutoAdvance,Is.False);Assert.That(player.nextButton.interactable,Is.False);
                float duration=player.CurrentView.autoContinueAfterSeconds;
                player.RequestSurfaceAdvance();Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(frame));
                player.AdvanceTime(duration-.05f);
                player.RequestNext();Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(frame));
                player.AdvanceTime(.06f);
            }
            Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(17));
            Assert.That(player.nextButton.interactable,Is.True);
            player.AdvanceTime(100);Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(17));
            Assert.That(player.CurrentView.authoredDialogue.alpha,Is.EqualTo(1));
            yield return null;
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(),Has.Length.EqualTo(1));
            player.RequestNext();Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(18));
            player.Replay();Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(13));
        }

        [UnityTest] public IEnumerator LongFramesCarryTimeAndPreviousReplaysTheCinematicFromItsStart()
        {
            player.AdvanceTime(10);player.Next();player.AdvanceTime(30);
            Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(17));
            player.Previous();Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(16));
            Assert.That(player.IsReading,Is.False);
            player.AdvanceTime(1);Assert.That(player.CurrentView.sourceFigmaNumber,Is.EqualTo(16));
            int finished=0;player.Finished+=_=>finished++;
            var stoppedView=player.CurrentView;
            player.RequestSkip();player.AdvanceTime(100);
            Assert.That(player.IsPlaying,Is.False);Assert.That(finished,Is.EqualTo(1));
            Assert.That(player.CurrentView,Is.SameAs(stoppedView));
            // Finish retains the last picture; the owning screen calls Cancel when it hides the story.
            player.Cancel();Assert.That(player.CurrentView,Is.Null);
            Assert.That(stoppedView.gameObject.activeSelf,Is.False);
            yield return null;
            yield return null;
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(),Is.Empty);
        }
    }
}
#endif
