#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    public sealed class OpeningTransitionTests
    {
        GameObject root;
        StoryPlayer player;
        StorySequence sequence;
        void Create(int first, int count)
        {
            root = new GameObject("Opening transition fixture", typeof(RectTransform));
            root.SetActive(false);
            player = root.AddComponent<StoryPlayer>(); player.playOnStart = false; player.enabled = false;
            player.cutHost = (RectTransform)root.transform;
            sequence = ScriptableObject.CreateInstance<StorySequence>();
            sequence.smoothOpeningTransitions = true;
            sequence.sideTapNavigation = true;
            var source = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/Opening/Opening.asset");
            sequence.cuts = source.cuts.Skip(first - 1).Take(count).ToList();
            root.SetActive(true); player.Play(sequence);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); Object.DestroyImmediate(sequence); }

        [UnityTest] public IEnumerator BlendKeepsOpaqueIncomingPictureBlocksDoubleAdvanceAndCleansUpOnReplay()
        {
            Create(1, 5); player.AdvanceTime(10);
            var old = player.CurrentView; player.Next(); var current = player.CurrentView;
            Assert.That(player.IsTransitioning, Is.True);
            Assert.That(old.gameObject.activeSelf, Is.True);
            Assert.That(current.gameObject.activeSelf, Is.True);
            Assert.That(old.transform.GetSiblingIndex(), Is.GreaterThan(current.transform.GetSiblingIndex()));
            player.RequestSurfaceAdvance(); Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            player.AdvanceTime(.16f);
            Assert.That(old.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.5f).Within(.02f));
            player.AdvanceTime(.5f);
            Assert.That(player.IsTransitioning, Is.False);
            Assert.That(old.gameObject.activeSelf, Is.False);
            player.Next(); Assert.That(player.IsTransitioning, Is.True);
            player.Replay(); Assert.That(player.IsTransitioning, Is.False);
            yield return null;
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(), Has.Length.EqualTo(1));
            player.AdvanceTime(5); player.Next(); player.Skip();
            Assert.That(player.IsPlaying, Is.False); Assert.That(player.IsTransitioning, Is.False);
        }

        [UnityTest] public IEnumerator DirectToolSequencingCanFinishWithoutWaitingForRealtimeBlends()
        {
            Create(1, 5);
            for (int steps = 0; steps < 20 && player.IsPlaying; steps++) player.Next();
            Assert.That(player.IsPlaying, Is.False, "Direct Next must remain safe for bounded authoring/test loops.");
            Assert.That(player.IsTransitioning, Is.False);
            yield return null;
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(), Has.Length.EqualTo(1));
        }

        [UnityTest] public IEnumerator GourdCloseupUsesContinuousZoomAndBackwardNavigationSettlesCleanly()
        {
            Create(3, 3); player.AdvanceTime(5);
            var old = player.CurrentView; player.Next(); player.AdvanceTime(.275f);
            Assert.That(old.transform.localScale.x, Is.EqualTo(Mathf.Lerp(1,778f/402f,.5f)).Within(.01f));
            Assert.That(((RectTransform)old.transform).anchoredPosition.x, Is.EqualTo(-94).Within(.1f));
            player.AdvanceTime(.3f); player.RequestSurfacePrevious();
            Assert.That(player.CurrentView.sourceFigmaNumber, Is.EqualTo(3));
            player.AdvanceTime(.5f);
            Assert.That(player.CurrentView.transform.localScale, Is.EqualTo(Vector3.one));
            yield return null;
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(), Has.Length.EqualTo(1));
        }

        [UnityTest] public IEnumerator CinematicCarriesGourdPoseAndRunsThroughToDialogueWithoutTouch()
        {
            Create(14, 4);
            var old = player.CurrentView;
            var teeter = old.openingMotions.Single(m => m.kind == OpeningLayerMotion.Kind.Teeter);
            player.AdvanceTime(2.3f);
            var drop = player.CurrentView.openingMotions.Single(m => m.kind == OpeningLayerMotion.Kind.Drop);
            Assert.That(Vector3.Distance(drop.transform.position, teeter.transform.position), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(drop.transform.rotation, teeter.transform.rotation), Is.LessThan(.01f));
            player.RequestSurfacePrevious(); Assert.That(player.CurrentView.sourceFigmaNumber, Is.EqualTo(15));
            player.AdvanceTime(6.7f);
            Assert.That(player.CurrentView.sourceFigmaNumber, Is.EqualTo(17));
            player.AdvanceTime(5);
            Assert.That(player.IsReading, Is.True);
            Assert.That(player.CurrentView.authoredDialogue.alpha, Is.EqualTo(1));
            yield return null;
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(), Has.Length.EqualTo(1));
        }
    }
}
#endif
