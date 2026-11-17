using System.Collections;
using System.Linq;
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
            // Story results name the battle characters: P1 불이, P2 묵이.
            Assert.That(records[0].player, Is.EqualTo("불이"));
            Assert.That(records[1].player, Is.EqualTo("불이"));
            Assert.That(records[2].player, Is.EqualTo("묵이"));
            Assert.That(records[3].player, Is.EqualTo("묵이"));
            Assert.That(records[4].player, Is.EqualTo("묵이"), "The fifth printed row is drum ownership, not last hit.");
            Assert.That(records[4].value, Does.Contain("88초"));
            Assert.That(p1.damageTotal, Is.EqualTo(80));
            Assert.That(snapshot.playedSeconds, Is.EqualTo(100));
        }

        [Test]
        public void ResultNamesFollowTheBattleCharactersAndLandGrabKeepsPNumbers()
        {
            string[] expected = { "불이", "묵이", "솔이", "금옥", "만복" };
            var story = new LabSnapshot();
            for (int seat = 0; seat < expected.Length; seat++)
                Assert.That(GameBattleUiPresentation.ResultNames(story)(new LabPlayerState { seat = seat }), Is.EqualTo(expected[seat]));
            Assert.That(GameBattleUiPresentation.ResultNames(null)(new LabPlayerState { seat = 2 }), Is.EqualTo("솔이"));
            Assert.That(GameBattleUiPresentation.ResultNames(story)(new LabPlayerState { seat = 5 }), Is.EqualTo("P6"));
            Assert.That(GameBattleUiPresentation.ResultNames(story)(null), Is.EqualTo("P—"));
            var landGrab = new LabSnapshot { teamTerritory = true, players = new LabPlayerState[4] };
            Assert.That(GameBattleUiPresentation.ResultNames(landGrab)(new LabPlayerState { seat = 0 }), Is.EqualTo("P1"));

            // The shared label behind every battle text: no name table (standalone Lab) keeps the P number.
            Assert.That(LabHud.BattlePlayerLabel(story, 2, null), Is.EqualTo("P2"));
            Assert.That(LabHud.BattlePlayerLabel(story, 2, new[] { "불이", "솔이", "묵이", "금옥", "만복" }), Is.EqualTo("묵이"));
            Assert.That(LabGourdComboHud.ResultTextFor(LabComboResult.WrongColor, true, false, 1, "묵이"), Is.EqualTo("묵이 색 틀림"));
            Assert.That(LabGourdComboHud.ResultTextFor(LabComboResult.WrongColor, true, false, 1), Is.EqualTo("P2 색 틀림"));
            Assert.That(LabCharmHud.WarningText(new[] { "나", "솔이" }, true), Is.EqualTo("요괴가 나와 솔이에게\n매혹을 걸려고 한다!"));
        }

        [Test]
        public void ACharacterChosenInTheLobbyReplacesThePNumberMappingEverywhere()
        {
            // P1 chose 만복(5), P2 chose 불이(1); P3 carries none and keeps the P-number character (솔이).
            var players = new[]
            {
                new LabPlayerState { id = 10, seat = 0, character = 5, damageTotal = 80, hitTotal = 4, throwTotal = 4 },
                new LabPlayerState { id = 20, seat = 1, character = 1, passedTotal = 3 },
                new LabPlayerState { id = 30, seat = 2 },
            };
            var snapshot = new LabSnapshot { phase = LabPhase.Victory, players = players, playedSeconds = 100 };
            Assert.That(LabHud.BattleCharacterIndex(snapshot, 1), Is.EqualTo(4));
            Assert.That(LabHud.BattleCharacterIndex(snapshot, 2), Is.EqualTo(0));
            Assert.That(LabHud.BattleCharacterIndex(snapshot, 3), Is.EqualTo(1));
            Assert.That(GameBattleUiPresentation.FinishReplayOwner(1, snapshot), Is.EqualTo("만복의"));
            Assert.That(GameBattleUiPresentation.FinishReplayOwner(2, snapshot), Is.EqualTo("불이의"));
            var records = GameStageStoryPresentation.CardRecords(snapshot, 20);
            Assert.That(records[0].player, Is.EqualTo("만복"));
            Assert.That(records[2].player, Is.EqualTo("불이(나)"));
            // Overall rows have no snapshot: the name comes from the player's own record.
            Assert.That(GameBattleUiPresentation.ResultNames(null)(players[0]), Is.EqualTo("만복"));
            Assert.That(GameBattleUiPresentation.ResultNames(null)(players[2]), Is.EqualTo("솔이"));
            var overall = LabResultStats.Overall(new[] { new LabResultStage(players, 100d) });
            Assert.That(overall.Awards.SelectMany(award => award.Winners).First(winner => winner.id == 10).character, Is.EqualTo(5),
                "The averaged record keeps the character.");
        }

        [Test]
        public void OnlyTheAwardsTheLocalPlayerEarnedAreMarkedIncludingTies()
        {
            var p1 = new LabPlayerState { id = 10, seat = 0, damageTotal = 80, hitTotal = 4, throwTotal = 4,
                passedTotal = 5, drumPassedTotal = 2, drumSeconds = 12 };
            var p2 = new LabPlayerState { id = 20, seat = 1, damageTotal = 20, hitTotal = 1, throwTotal = 4,
                passedTotal = 5, drumPassedTotal = 9, drumSeconds = 88 };
            var snapshot = new LabSnapshot { phase = LabPhase.Victory, players = new[] { p1, p2 }, playedSeconds = 100 };

            var records = GameStageStoryPresentation.CardRecords(snapshot, 20);
            Assert.That(records.Select(record => record.mine), Is.EqualTo(new[] { false, false, true, true, true }));
            Assert.That(records[0].player, Is.EqualTo("불이"));
            Assert.That(records[2].player, Is.EqualTo("불이, 묵이(나)"), "A tie marks the row and only the local name.");
            Assert.That(records[3].player, Is.EqualTo("묵이(나)"));
            Assert.That(GameStageStoryPresentation.CardRecords(snapshot, 99).Any(record => record.mine), Is.False);
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

        [Test]
        public void EarlySkipSettlesTheRetainedViewWithoutReplayingEntranceOrDeferredSound()
        {
            root = new GameObject("Quiet result pose fixture", typeof(RectTransform)); root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>(); player.playOnStart = false;
            var host = new GameObject("Current picture", typeof(RectTransform)); host.transform.SetParent(root.transform, false);
            player.cutHost = (RectTransform)host.transform;
            var templates = new GameObject("Inactive templates", typeof(RectTransform)); templates.transform.SetParent(root.transform, false);
            templates.SetActive(false);
            var template = new GameObject("Early result view", typeof(RectTransform), typeof(StoryCutView));
            template.transform.SetParent(templates.transform, false);
            var layerObject = new GameObject("Unfolding artwork", typeof(RectTransform), typeof(CanvasGroup), typeof(StoryLayer));
            layerObject.transform.SetParent(template.transform, false);
            var layerRect = (RectTransform)layerObject.transform;
            Vector2 layerRest = new Vector2(35, -42); Vector3 layerScale = new Vector3(2, 3, 1);
            layerRect.anchoredPosition = layerRest; layerRect.localScale = layerScale;
            var layer = layerObject.GetComponent<StoryLayer>(); layer.delay = 1; layer.enterSeconds = .5f;
            layer.enterOffset = new Vector2(20, -50); layer.idleAmplitude = new Vector2(80, 60);
            var beatObject = new GameObject("Falling paper", typeof(RectTransform), typeof(CanvasGroup), typeof(StoryVisualBeat));
            beatObject.transform.SetParent(template.transform, false);
            var beatRect = (RectTransform)beatObject.transform;
            Vector2 beatRest = new Vector2(12, -34); Quaternion beatRotation = Quaternion.Euler(0, 0, 17);
            beatRect.anchoredPosition = beatRest; beatRect.localRotation = beatRotation;
            var beat = beatObject.GetComponent<StoryVisualBeat>();
            beat.motion = StoryVisualBeat.Motion.PaperDrop; beat.delay = 0; beat.duration = .42f;
            beat.offset = new Vector2(-40, 120); beat.interactionDelay = .63f;
            AudioClip cue = AudioClip.Create("Unreached result sound", 44100, 1, 44100, false);
            try
            {
                beat.sound = cue;
                var view = template.GetComponent<StoryCutView>(); view.layers = new[] { layer }; view.beats = new[] { beat };
                cut = ScriptableObject.CreateInstance<StoryCutDefinition>(); cut.cutId = "early-result-pose"; cut.viewPrefab = view;
                sequence = ScriptableObject.CreateInstance<StorySequence>(); sequence.storyId = "quiet-result-pose"; sequence.cuts.Add(cut);
                int views = 0, endings = 0;
                player.ViewChanged += _ => views++;
                player.Finished += reason => { Assert.That(reason, Is.EqualTo(StoryExitReason.Skipped)); endings++; };
                root.SetActive(true);
                Assert.That(player.Play(sequence), Is.True);
                StoryCutView retained = player.CurrentView;
                Assert.That(retained.gameObject.activeInHierarchy, Is.True, "A hidden template must yield an active runtime picture.");
                Assert.That(retained.layers[0].GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(retained.beats[0].GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(retained.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                player.Skip();
                Assert.That(player.CurrentView, Is.SameAs(retained));
                // The result controller settles this retained picture after the normal player Skip.
                retained.ShowCompletedPose();
                Assert.That(retained.layers[0].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                Assert.That(Vector2.Distance(((RectTransform)retained.layers[0].transform).anchoredPosition, layerRest), Is.LessThan(.001f),
                    "Each layer settles at its own entrance end, without later idle drift.");
                Assert.That(((RectTransform)retained.layers[0].transform).localScale, Is.EqualTo(layerScale));
                var settledBeat = retained.beats[0];
                Assert.That(settledBeat.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                Assert.That(settledBeat.GetComponent<CanvasGroup>().blocksRaycasts, Is.True,
                    "The real card opens interaction at .63 seconds, after its .42-second visual entrance.");
                Assert.That(Vector2.Distance(((RectTransform)settledBeat.transform).anchoredPosition, beatRest), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(settledBeat.transform.localRotation, beatRotation), Is.LessThan(.001f));
                Assert.That(retained.GetComponentsInChildren<AudioSource>(true), Is.Empty, "Settling must not create a sound source.");
                retained.ShowCompletedPose(); retained.Sample(retained.EntranceDuration + 20);
                Assert.That(retained.GetComponentsInChildren<AudioSource>(true), Is.Empty,
                    "A skipped sound cue is consumed and cannot fire on later normal sampling.");
                Assert.That(player.CurrentView, Is.SameAs(retained));
                Assert.That(views, Is.EqualTo(1), "No new picture or entrance callback is created by Skip or settling.");
                Assert.That(endings, Is.EqualTo(1));
                Assert.That(player.IsPlaying, Is.False);
            }
            finally { Object.DestroyImmediate(cue); }
        }

        [Test]
        public void NormalVisualBeatSamplingStillCreatesItsSoundSourceAtTheCue()
        {
            root = new GameObject("Normal beat cue", typeof(RectTransform), typeof(CanvasGroup), typeof(StoryVisualBeat));
            var beat = root.GetComponent<StoryVisualBeat>(); beat.motion = StoryVisualBeat.Motion.PaperDrop;
            beat.delay = .1f; beat.duration = .2f;
            AudioClip cue = AudioClip.Create("Normal result sound", 44100, 1, 44100, false);
            try
            {
                beat.sound = cue; beat.Sample(0);
                Assert.That(root.GetComponent<AudioSource>(), Is.Null);
                beat.Sample(beat.Duration);
                Assert.That(root.GetComponent<AudioSource>(), Is.Not.Null,
                    "Only the quiet completed-pose path suppresses sound; normal sampling retains its cue.");
            }
            finally
            {
                var source = root.GetComponent<AudioSource>(); if (source != null) source.Stop();
                Object.DestroyImmediate(cue);
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
