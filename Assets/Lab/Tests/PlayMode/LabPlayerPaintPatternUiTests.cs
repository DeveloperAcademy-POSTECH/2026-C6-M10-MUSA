using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>Saved Canvas binding checks; synthetic snapshots below do not claim remote network verification.</summary>
    public sealed class LabPlayerPaintPatternUiTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private LabHud hud;
        private LabNetwork network;

        [UnitySetUp]
        public IEnumerator LoadSavedCanvas()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            hud = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
            hud.GetComponent<LabDeveloperMode>().allowOfflineEditorTuning = true;
            network = hud.network;
            // Explicitly exercise the retained legacy, freely chosen Host-slot path.
            network.fixedHostPosition = false;
            network.port = 18260;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (network != null)
            {
                SetSnapshot(null);
                network.enabled = true;
                network.Stop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedSamplesStayOnExistingControlsAndFollowLobbyAndDeveloperVisibility()
        {
            Assert.That(hud.playerSlotPatternSamples, Has.Length.EqualTo(5));
            Image[] images = hud.playerSlotPatternSamples.Concat(new[] {
                hud.localPlayerPatternSample, hud.leftPlayerPatternSample, hud.rightPlayerPatternSample }).ToArray();
            Assert.That(images.All(image => image != null), Is.True);
            Assert.That(images.Distinct().Count(), Is.EqualTo(8), "Each saved editable Image has one owner.");
            Assert.That(images.All(image => !image.raycastTarget), Is.True,
                "A sample must not intercept position selection or game input.");
            for (int i = 0; i < 5; i++)
                Assert.That(hud.playerSlotPatternSamples[i].transform.parent, Is.SameAs(hud.playerSlotButtons[i].transform));
            Assert.That(hud.localPlayerPatternSample.transform.parent, Is.SameAs(hud.localPlayerCircle.transform));
            Assert.That(hud.leftPlayerPatternSample.transform.parent, Is.SameAs(hud.leftPlayerCircle.transform));
            Assert.That(hud.rightPlayerPatternSample.transform.parent, Is.SameAs(hud.rightPlayerCircle.transform));
            Assert.That(images.All(image => image.name == "PaintPatternSample"), Is.True);
            Assert.That(images.All(image => !image.gameObject.activeInHierarchy), Is.True,
                "The disconnected browser has no player ownership claim.");

            RectTransform[] parents = images.Select(image => (RectTransform)image.transform.parent).ToArray();
            Assert.That(network.SetTargetParticipantCount(5), Is.True);
            network.Host();
            Assert.That(network.IsHost, Is.True, network.Notice);
            yield return null;
            Assert.That(network.LocalSlotNumber, Is.Zero);
            // The existing HorizontalLayoutGroup runs when the formerly hidden lobby opens.
            // Compare its settled layout, not the inactive saved positions before layout.
            Canvas.ForceUpdateCanvases();
            Vector2[] positions = parents.Select(rect => rect.anchoredPosition).ToArray();
            Vector2[] sizes = parents.Select(rect => rect.sizeDelta).ToArray();
            for (int i = 0; i < 5; i++)
            {
                Image sample = hud.playerSlotPatternSamples[i];
                Assert.That(sample.gameObject.activeInHierarchy, Is.True);
                Assert.That(sample.sprite, Is.SameAs(LabPlayerPaintPatterns.SpriteFor(i + 1)));
                Assert.That(sample.enabled, Is.True);
            }
            Assert.That(hud.localPlayerPatternSample.gameObject.activeInHierarchy, Is.False);
            hud.playerSlotButtons[3].onClick.Invoke();
            yield return null;
            Assert.That(network.LocalSlotNumber, Is.EqualTo(4));
            Assert.That(hud.playerSlotButtons[3].GetComponentInChildren<Text>().text, Does.Contain("YOU/CLEAR"));
            Assert.That(hud.playerSlotPatternSamples[3].sprite, Is.SameAs(LabPlayerPaintPatterns.SpriteFor(4)));
            hud.playerSlotButtons[3].onClick.Invoke();
            yield return null;
            Assert.That(network.LocalSlotNumber, Is.Zero);

            LabDeveloperMode developer = hud.GetComponent<LabDeveloperMode>();
            developer.Open();
            yield return null;
            Assert.That(hud.playerSlotPatternSamples.All(sample => !sample.gameObject.activeInHierarchy), Is.True);
            developer.Close();
            yield return null;
            Assert.That(hud.playerSlotPatternSamples.All(sample => sample.gameObject.activeInHierarchy), Is.True);
            for (int i = 0; i < parents.Length; i++)
            {
                Assert.That(parents[i].anchoredPosition, Is.EqualTo(positions[i]));
                Assert.That(parents[i].sizeDelta, Is.EqualTo(sizes[i]), "Runtime binding must not relayout existing art.");
            }
        }

        [UnityTest]
        public IEnumerator DeveloperRoomApplyHidesAndRestoresAllSamplesWithoutChangingPlayerIdentity()
        {
            Assert.That(network.config.PlayerPaintPatternsEnabled, Is.True,
                "The saved room configuration enables player patterns by default.");
            Assert.That(network.SetTargetParticipantCount(5), Is.True);
            network.Host();
            Assert.That(network.IsHost, Is.True, network.Notice);
            yield return null;
            hud.playerSlotButtons[3].onClick.Invoke();
            yield return null;
            Assert.That(network.LocalSlotNumber, Is.EqualTo(4));

            LabDeveloperMode developer = hud.GetComponent<LabDeveloperMode>();
            float[] sourceValues = developer.sourceConfig.CaptureDeveloperValues();
            string[] lobbyLabels = hud.playerSlotButtons
                .Select(button => button.GetComponentInChildren<Text>().text).ToArray();
            RectTransform[] buttons = hud.playerSlotButtons.Select(button => (RectTransform)button.transform).ToArray();
            Canvas.ForceUpdateCanvases();
            Vector2[] lobbyPositions = buttons.Select(rect => rect.anchoredPosition).ToArray();
            Vector2[] lobbySizes = buttons.Select(rect => rect.sizeDelta).ToArray();
            ulong localId = network.LocalPlayerId;
            var battle = new LabSnapshot {
                sessionId = "pattern-ui-identity", round = 7, phase = LabPhase.Playing,
                hp = 725, remaining = 54.3d, playedSeconds = 12.5d,
                paintCoverage = .25f, paintCoveredCount = 512, paintCellCount = 2048,
                players = new[] {
                    new LabPlayerState { id = 101, seat = 0 },
                    new LabPlayerState { id = localId, seat = 3, personalHp = 123, stamina = 60f, hitTotal = 4, damageTotal = 80 },
                    new LabPlayerState { id = 104, seat = 4 },
                    new LabPlayerState { id = 103, seat = 2 },
                    new LabPlayerState { id = 102, seat = 1 }
                }
            };
            string battleState = JsonUtility.ToJson(battle);
            Image[] circles = { hud.localPlayerCircle, hud.leftPlayerCircle, hud.rightPlayerCircle };
            Image[] battleSamples = { hud.localPlayerPatternSample, hud.leftPlayerPatternSample, hud.rightPlayerPatternSample };
            int[] numbers = { 4, 3, 5 };
            Vector2[] battlePositions = null, battleSizes = null;

            // The settings use the ordinary Host lobby form. Battle ownership below is
            // a fixed synthetic snapshot, so these checks do not claim a remote round.
            network.enabled = false;
            hud.enabled = false;
            foreach (bool enabled in new[] { true, false, true })
            {
                SetSnapshot(null);
                developer.Open();
                developer.valueInputs[137].text = enabled ? "1" : "0";
                developer.ApplyAndHost();
                Assert.That(network.config.PlayerPaintPatternsEnabled, Is.EqualTo(enabled), developer.feedbackText.text);
                yield return null;
                RefreshHud();
                Canvas.ForceUpdateCanvases();
                Assert.That(network.LocalPlayerId, Is.EqualTo(localId));
                Assert.That(network.LocalSlotNumber, Is.EqualTo(4));
                Assert.That(network.GetSlotOwner(4), Is.EqualTo(localId));
                Assert.That(hud.playerSlotButtons.Select(button => button.GetComponentInChildren<Text>().text),
                    Is.EqualTo(lobbyLabels), "The pattern setting must preserve the P labels and selection.");
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(hud.playerSlotPatternSamples[i].gameObject.activeInHierarchy, Is.EqualTo(enabled));
                    Assert.That(hud.playerSlotPatternSamples[i].sprite, Is.SameAs(LabPlayerPaintPatterns.SpriteFor(i + 1)));
                    Assert.That(buttons[i].anchoredPosition, Is.EqualTo(lobbyPositions[i]));
                    Assert.That(buttons[i].sizeDelta, Is.EqualTo(lobbySizes[i]));
                }
                Assert.That(battleSamples.All(sample => !sample.gameObject.activeSelf), Is.True);

                SetSnapshot(battle);
                RefreshHud();
                Canvas.ForceUpdateCanvases();
                Assert.That(hud.seatText.text, Is.EqualTo("P4"));
                Assert.That(hud.leftNeighborText.text, Is.EqualTo("P3"));
                Assert.That(hud.rightNeighborText.text, Is.EqualTo("P5"));
                for (int i = 0; i < battleSamples.Length; i++)
                {
                    Assert.That(battleSamples[i].gameObject.activeInHierarchy, Is.EqualTo(enabled));
                    Assert.That(battleSamples[i].sprite, Is.SameAs(LabPlayerPaintPatterns.SpriteFor(numbers[i])));
                    Assert.That(circles[i].enabled, Is.True);
                    Assert.That(circles[i].sprite, Is.SameAs(hud.playerCircleSprites[numbers[i] - 1]));
                }
                Vector2[] positions = circles.Select(circle => ((RectTransform)circle.transform).anchoredPosition).ToArray();
                Vector2[] sizes = circles.Select(circle => ((RectTransform)circle.transform).sizeDelta).ToArray();
                if (battlePositions != null)
                {
                    Assert.That(positions, Is.EqualTo(battlePositions));
                    Assert.That(sizes, Is.EqualTo(battleSizes));
                }
                battlePositions = positions;
                battleSizes = sizes;
                Assert.That(hud.playerSlotPatternSamples.All(sample => !sample.gameObject.activeInHierarchy), Is.True);
                Assert.That(network.Snapshot, Is.SameAs(battle));
                Assert.That(JsonUtility.ToJson(battle), Is.EqualTo(battleState),
                    "Refreshing either pattern setting must preserve seats, health, stamina, score, paint, and round state.");
                Assert.That(developer.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(sourceValues));
            }
        }

        [UnityTest]
        public IEnumerator BattleSamplesUseActualPSeatsRatherThanHostConnectionOrSnapshotArrayOrder()
        {
            network.Host();
            Assert.That(network.IsHost, Is.True, network.Notice);
            network.enabled = false;
            hud.enabled = false;
            ulong local = network.LocalPlayerId;
            SetSnapshot(new LabSnapshot {
                phase = LabPhase.Playing,
                players = new[] {
                    new LabPlayerState { id = 101, seat = 1 },
                    new LabPlayerState { id = local, seat = 2 },
                    new LabPlayerState { id = 100, seat = 0 }
                }
            });
            RefreshHud();
            AssertSample(hud.localPlayerPatternSample, 3);
            AssertSample(hud.leftPlayerPatternSample, 2);
            AssertSample(hud.rightPlayerPatternSample, 1);
            Assert.That(hud.playerSlotPatternSamples.All(sample => !sample.gameObject.activeInHierarchy), Is.True);

            SetSnapshot(new LabSnapshot {
                phase = LabPhase.Playing,
                players = new[] {
                    new LabPlayerState { id = local, seat = 1 },
                    new LabPlayerState { id = 100, seat = 0 }
                }
            });
            RefreshHud();
            AssertSample(hud.localPlayerPatternSample, 2);
            AssertSample(hud.leftPlayerPatternSample, 1);
            AssertSample(hud.rightPlayerPatternSample, 1);

            SetSnapshot(new LabSnapshot {
                phase = LabPhase.Playing,
                players = new[] { new LabPlayerState { id = local, seat = 0 } }
            });
            RefreshHud();
            AssertSample(hud.localPlayerPatternSample, 1);
            Assert.That(hud.leftPlayerPatternSample.gameObject.activeInHierarchy, Is.False);
            Assert.That(hud.rightPlayerPatternSample.gameObject.activeInHierarchy, Is.False);

            SetSnapshot(null);
            RefreshHud();
            foreach (Image sample in new[] { hud.localPlayerPatternSample, hud.leftPlayerPatternSample, hud.rightPlayerPatternSample })
            {
                Assert.That(sample.gameObject.activeSelf, Is.False);
                Assert.That(sample.sprite, Is.Null, "Returning to lobby must not leave a stale battle ownership sample.");
            }
            yield break;
        }

        private static void AssertSample(Image sample, int number)
        {
            Assert.That(sample.gameObject.activeInHierarchy, Is.True);
            Assert.That(sample.enabled, Is.True);
            Assert.That(sample.sprite, Is.SameAs(LabPlayerPaintPatterns.SpriteFor(number)));
        }

        private void SetSnapshot(LabSnapshot snapshot) => typeof(LabNetwork)
            .GetField("latest", Private).SetValue(network, snapshot);

        private void RefreshHud() => typeof(LabHud).GetMethod("Update", Private).Invoke(hud, null);
    }
}
