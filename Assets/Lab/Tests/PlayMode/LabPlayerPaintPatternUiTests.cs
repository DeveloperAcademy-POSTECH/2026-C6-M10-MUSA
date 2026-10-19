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
            network = hud.network;
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
