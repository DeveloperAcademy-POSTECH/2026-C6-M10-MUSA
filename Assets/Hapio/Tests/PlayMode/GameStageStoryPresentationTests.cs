using System.Collections;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    public sealed class GameStageStoryPresentationTests
    {
        private GameObject root;
        private StorySequence sequence;
        private StoryCutDefinition cut;

        [TearDown]
        public void Clean()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (sequence != null) Object.DestroyImmediate(sequence);
            if (cut != null) Object.DestroyImmediate(cut);
        }

        [Test]
        public void CardRecordsUseActualAwardCategoriesAndDoNotShiftLastHitIntoDrumOwnership()
        {
            var p1 = new LabPlayerState { id = 10, seat = 0, damageTotal = 80, hitTotal = 4,
                throwTotal = 4, passedTotal = 1, drumPassedTotal = 2, drumSeconds = 12, lastHit = true };
            var p2 = new LabPlayerState { id = 20, seat = 1, damageTotal = 20, hitTotal = 1,
                throwTotal = 4, passedTotal = 8, drumPassedTotal = 9, drumSeconds = 88 };
            var snapshot = new LabSnapshot { phase = LabPhase.Victory, players = new[] { p1, p2 }, playedSeconds = 100 };
            var records = GameStageStoryPresentation.CardRecords(snapshot);
            Assert.That(records.Length, Is.EqualTo(5));
            Assert.That(records[0].player, Is.EqualTo("P1"));
            Assert.That(records[1].player, Is.EqualTo("P1"));
            Assert.That(records[2].player, Is.EqualTo("P2"));
            Assert.That(records[3].player, Is.EqualTo("P2"));
            Assert.That(records[4].player, Is.EqualTo("P2"), "The fifth printed row is drum ownership, not last hit.");
            Assert.That(records[4].value, Does.Contain("88초"));
            Assert.That(p1.damageTotal, Is.EqualTo(80));
            Assert.That(snapshot.playedSeconds, Is.EqualTo(100));
        }

        [Test]
        public void MissingResultHasNoAuthoredExamplePlayersOrValues()
        {
            foreach (var record in GameStageStoryPresentation.CardRecords(null))
            {
                Assert.That(record.player, Is.Null);
                Assert.That(record.value, Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator CutBindingRunsSynchronouslyAndReleaseGateBlocksSkipWithoutChangingDirectSequencing()
        {
            root = new GameObject("Runtime story", typeof(RectTransform)); root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>(); player.playOnStart = false;
            var host = new GameObject("Cut host", typeof(RectTransform)); host.transform.SetParent(root.transform, false);
            player.cutHost = (RectTransform)host.transform;
            var template = new GameObject("Failure view", typeof(RectTransform), typeof(StoryCutView), typeof(StorySealGauge));
            template.transform.SetParent(root.transform, false); template.SetActive(false);
            var gauge = template.GetComponent<StorySealGauge>(); gauge.target = .78f;
            cut = ScriptableObject.CreateInstance<StoryCutDefinition>(); cut.cutId = "actual-failure"; cut.viewPrefab = template.GetComponent<StoryCutView>();
            sequence = ScriptableObject.CreateInstance<StorySequence>(); sequence.storyId = "result-test"; sequence.cuts.Add(cut);
            root.SetActive(true);
            int bound = 0, finished = 0;
            player.ViewChanged += view => { bound++; view.GetComponent<StorySealGauge>().Show(.31f); };
            player.Finished += _ => finished++;
            player.SetInputBlocked(true);
            Assert.That(player.Play(sequence), Is.True);
            Assert.That(bound, Is.EqualTo(1));
            Assert.That(player.CurrentView.GetComponent<StorySealGauge>().target, Is.EqualTo(.31f));
            player.RequestSkip();
            Assert.That(player.IsPlaying, Is.True, "A held transition release cannot skip the new result story.");
            Assert.That(finished, Is.Zero);
            yield return null;
            player.SetInputBlocked(false); player.RequestSkip();
            Assert.That(finished, Is.EqualTo(1));
            player.RequestSkip(); Assert.That(finished, Is.EqualTo(1));
        }
    }
}
