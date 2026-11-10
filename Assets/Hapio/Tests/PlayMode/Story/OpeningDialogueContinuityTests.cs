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
    public sealed class OpeningDialogueContinuityTests
    {
        [UnityTest]
        public IEnumerator DialogueOnlyFramesKeepArtworkClockAndInstancesInBothDirections()
        {
            var root = new GameObject("Opening continuity fixture", typeof(RectTransform));
            root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>();
            player.playOnStart = false;
            player.cutHost = (RectTransform)root.transform;
            player.enabled = false; // Advance the same runtime clock explicitly.
            root.SetActive(true);
            var sequence = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/Opening/Opening.asset");
            try
            {
                Assert.That(player.Play(sequence), Is.True);
                foreach (int first in new[] { 8, 18 })
                {
                    while (player.CurrentCutIndex < first)
                    {
                        player.AdvanceTime(player.CurrentView.EntranceDuration + .1f);
                        player.Next();
                    }
                    player.AdvanceTime(5);
                    var view = player.CurrentView;
                    var artwork = view.openingMotions[0];
                    var position = artwork.transform.position;
                    var oldDialogue = view.authoredDialogue;
                    player.Next();
                    Assert.That(player.CurrentCutIndex, Is.EqualTo(first + 1));
                    Assert.That(player.CurrentView, Is.SameAs(view));
                    Assert.That(player.IsReading, Is.True, "No entrance wait or second tap on a dialogue-only frame.");
                    Assert.That(view.openingMotions[0], Is.SameAs(artwork));
                    Assert.That(artwork.transform.position, Is.EqualTo(position));
                    Assert.That(view.authoredDialogue, Is.Not.SameAs(oldDialogue));
                    Assert.That(oldDialogue.gameObject.activeSelf, Is.False);
                    string expected = sequence.cuts[first + 1].GetPage(0).dialogue;
                    string rich = string.Join("", view.authoredDialogue.GetComponentsInChildren<Text>().Select(t => t.text));
                    foreach (string line in expected.Split('\n'))
                        Assert.That(rich, Does.Contain(line));
                    Assert.That(view.authoredDialogue.alpha, Is.EqualTo(1));
                    var palette=AssetDatabase.LoadAssetAtPath<StorySpeakerPalette>("Assets/Hapio/Story/Opening/SpeakerPalette.asset");
                    Assert.That(palette.TryGet(sequence.cuts[first+1].GetPage(0).speaker,out var style),Is.True);
                    var badge=view.authoredDialogue.GetComponentsInChildren<Image>().Single(x=>x.sprite!=null&&x.sprite.name=="9769e");
                    Assert.That(badge.color,Is.EqualTo(style.badgeColor),"Dialogue-only changes must also replace the speaker color.");
                    player.AdvanceTime(.01f);
                    Assert.That(artwork.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1), "The retained clock must not restart the entrance on the next tick.");
                    player.Previous();
                    Assert.That(player.CurrentCutIndex, Is.EqualTo(first));
                    Assert.That(player.CurrentView, Is.SameAs(view));
                    Assert.That(artwork.transform.position, Is.EqualTo(position));
                    player.Next();
                    player.Next();
                    Assert.That(player.CurrentCutIndex, Is.EqualTo(first + 2));
                    Assert.That(player.CurrentView, Is.Not.SameAs(view));
                    yield return null;
                    Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(), Has.Length.EqualTo(1));
                }
                player.Replay();
                Assert.That(player.CurrentCutIndex, Is.Zero);
                Assert.That(player.IsReading, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
