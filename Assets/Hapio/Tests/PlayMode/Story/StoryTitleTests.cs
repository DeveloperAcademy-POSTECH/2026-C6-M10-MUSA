using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class StoryTitleTests
    {
        [UnityTest]
        public IEnumerator CloudsStayBoundedMoveInOppositeDirectionsAndRestoreWhenReenabled()
        {
            var root = new GameObject("Cloud test");
            var left = new GameObject("Left", typeof(RectTransform)).GetComponent<RectTransform>();
            var right = new GameObject("Right", typeof(RectTransform)).GetComponent<RectTransform>();
            left.SetParent(root.transform, false); right.SetParent(root.transform, false);
            left.anchoredPosition = new Vector2(-182, -257); right.anchoredPosition = new Vector2(193, -67);
            var drift = root.AddComponent<TitleCloudDrift>();
            drift.clouds = new[] {
                new TitleCloudDrift.Cloud {target=left,distance=24,seconds=40},
                new TitleCloudDrift.Cloud {target=right,distance=20,seconds=40,reverse=true}
            };
            float clock = Time.timeScale;
            try
            {
                drift.Sample(0); drift.Sample(10);
                Assert.That(left.anchoredPosition, Is.EqualTo(new Vector2(-158, -257)));
                Assert.That(right.anchoredPosition, Is.EqualTo(new Vector2(173, -67)));
                drift.Sample(4000);
                Assert.That(left.anchoredPosition.x, Is.EqualTo(-182).Within(.01f));
                root.SetActive(false);
                Assert.That(left.anchoredPosition, Is.EqualTo(new Vector2(-182, -257)));
                left.anchoredPosition = new Vector2(-100, -200);
                root.SetActive(true);
                Assert.That(left.anchoredPosition, Is.EqualTo(new Vector2(-100, -200)));
                Time.timeScale = 0;
                yield return null; yield return null;
                Assert.That(left.anchoredPosition.x, Is.GreaterThan(-100), "Clouds must continue with a paused game clock.");
                Assert.That(left.anchoredPosition.y, Is.EqualTo(-200));
            }
            finally { Time.timeScale = clock; Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TitleWaitsForTheStartButtonAndCanReturnWithoutOldStoryViews()
        {
            var root = new GameObject("Gate", typeof(RectTransform)); root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>(); player.playOnStart = false;
            var host = new GameObject("Host", typeof(RectTransform)).GetComponent<RectTransform>(); host.SetParent(root.transform, false);
            player.cutHost = host;
            var title = new GameObject("Title"); title.transform.SetParent(root.transform, false);
            var button = title.AddComponent<Button>();
            var controls = new GameObject("Controls"); controls.transform.SetParent(root.transform, false);
            var gate = root.AddComponent<StoryOpeningTitle>();
            gate.player = player; gate.titlePage = title; gate.controls = controls; gate.beginButton = button;
            var view = new GameObject("Picture", typeof(RectTransform), typeof(StoryCutView));
            var cut = ScriptableObject.CreateInstance<StoryCutDefinition>(); cut.cutId = "one"; cut.viewPrefab = view.GetComponent<StoryCutView>();
            var sequence = ScriptableObject.CreateInstance<StorySequence>(); sequence.cuts.Add(cut); player.sequence = sequence;
            try
            {
                root.SetActive(true); yield return null;
                Assert.That(title.activeSelf, Is.True); Assert.That(controls.activeSelf, Is.False);
                Assert.That(player.IsPlaying, Is.False);
                button.onClick.Invoke();
                Assert.That(title.activeSelf, Is.False); Assert.That(controls.activeSelf, Is.True);
                Assert.That(player.IsPlaying, Is.True); Assert.That(player.CurrentCutIndex, Is.Zero);
                gate.ShowTitle(); yield return null;
                Assert.That(player.CurrentView, Is.Null); Assert.That(title.activeSelf, Is.True);
                button.onClick.Invoke();
                Assert.That(host.GetComponentsInChildren<StoryCutView>().Length, Is.EqualTo(1));
            }
            finally { Object.Destroy(root); Object.Destroy(view); Object.Destroy(cut); Object.Destroy(sequence); }
            yield return null;
        }
    }
}
