using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Passive presentation checks: no transport, room, battle model or saved Scene is started.</summary>
    public sealed class LabResultDashboardTests
    {
        private GameObject root;
        private LabConfig config;
        private LabNetwork network;
        private LabHud hud;
        private LabResultDashboard dashboard;
        private RectTransform canvas, card;
        private Vector2 anchorMin, anchorMax, offsetMin, offsetMax;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Legacy result presentation #367");
            var networkObject = new GameObject("Passive network");
            networkObject.transform.SetParent(root.transform, false);
            network = networkObject.AddComponent<LabNetwork>();
            network.enabled = false;
            config = ScriptableObject.CreateInstance<LabConfig>();
            network.config = config;

            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            canvas.SetParent(root.transform, false);
            card = new GameObject("Result card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(canvas, false);
            card.anchorMin = new Vector2(.2f, .35f);
            card.anchorMax = new Vector2(.8f, .65f);
            card.offsetMin = new Vector2(9f, -75f);
            card.offsetMax = new Vector2(-11f, 85f);
            anchorMin = card.anchorMin;
            anchorMax = card.anchorMax;
            offsetMin = card.offsetMin;
            offsetMax = card.offsetMax;

            var hudObject = new GameObject("HUD");
            hudObject.transform.SetParent(canvas, false);
            hud = hudObject.AddComponent<LabHud>();
            hud.enabled = false;
            hud.network = network;
            hud.resultControls = card.gameObject;
            hud.resultStatusText = new GameObject("Result status", typeof(RectTransform),
                typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            hud.resultStatusText.transform.SetParent(card, false);
            hud.resultStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.resultRestartButton = Button("Restart");
            hud.resultReturnLobbyButton = Button("Return to lobby");
            hud.resultLeaveButton = Button("Leave");

            dashboard = hudObject.AddComponent<LabResultDashboard>();
            dashboard.Bind(network, card.gameObject, hud.resultStatusText.font);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [TestCase(LabPhase.Victory)]
        [TestCase(LabPhase.Defeat)]
        public void DirectShowCannotCreateALegacyDashboardForGameFlow(LabPhase phase)
        {
            LabSnapshot snapshot = Snapshot(phase);
            string before = JsonUtility.ToJson(snapshot);
            Assert.That(network.SetGameFlowEnabled(true), Is.True);
            dashboard.Show(snapshot.players, phase == LabPhase.Victory, snapshot.playedSeconds);
            Assert.That(dashboard.IsShowing, Is.False);
            Assert.That(canvas.Find("ResultDashboard"), Is.Null,
                "The separate game results must not allocate a legacy battle dashboard.");
            AssertOriginalCard();
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator GameFlowHidesAnExistingPanelRestoresTheCardAndAllowsTheLabToShowItAgain()
        {
            LabSnapshot snapshot = Snapshot(LabPhase.Defeat);
            SetSnapshot(snapshot);
            string before = JsonUtility.ToJson(snapshot);
            yield return null;
            Assert.That(dashboard.IsShowing, Is.True, "Standalone Lab results still show their awards.");
            Assert.That(card.anchorMin, Is.Not.EqualTo(anchorMin));
            Transform panel = canvas.Find("ResultDashboard");
            Assert.That(panel, Is.Not.Null);

            Assert.That(network.SetGameFlowEnabled(true), Is.True);
            yield return null;
            Assert.That(dashboard.IsShowing, Is.False);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            AssertOriginalCard();
            dashboard.Show(snapshot.players, false, snapshot.playedSeconds);
            Assert.That(panel.gameObject.activeSelf, Is.False, "Direct refresh cannot reopen an existing panel either.");
            AssertOriginalCard();

            Assert.That(network.SetGameFlowEnabled(false), Is.True);
            yield return null;
            Assert.That(dashboard.IsShowing, Is.True, "The same result can render again after leaving game flow.");
            Assert.That(canvas.Find("ResultDashboard"), Is.SameAs(panel), "Reuse the existing panel.");
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before), "Presentation never changes shared records.");
            SetSnapshot(Snapshot(LabPhase.Playing));
            yield return null;
            Assert.That(dashboard.IsShowing, Is.False);
            AssertOriginalCard();
        }

        [Test]
        public void DisablingTheDashboardHidesItsSiblingPanelAndRestoresTheCard()
        {
            LabSnapshot snapshot = Snapshot(LabPhase.Defeat);
            dashboard.Show(snapshot.players, false, snapshot.playedSeconds);
            Assert.That(dashboard.IsShowing, Is.True);
            dashboard.enabled = false;
            Assert.That(dashboard.IsShowing, Is.False,
                "The generated panel is a Canvas sibling and must not survive a disabled presenter.");
            AssertOriginalCard();
        }

        [UnityTest]
        public IEnumerator HudTerminalCardsAndButtonsStayExclusiveToStandaloneLab()
        {
            dashboard.enabled = false;
            hud.enabled = true;
            foreach (LabPhase phase in new[] { LabPhase.Victory, LabPhase.Defeat })
            {
                LabSnapshot snapshot = Snapshot(phase);
                SetSnapshot(snapshot);
                string before = JsonUtility.ToJson(snapshot);
                Assert.That(network.SetGameFlowEnabled(false), Is.True);
                yield return null;
                Assert.That(card.gameObject.activeSelf, Is.True, "Standalone Lab keeps its terminal card.");
                Assert.That(hud.resultStatusText.text, Does.StartWith(phase == LabPhase.Victory ? "VICTORY" : "DEFEAT"));
                Assert.That(hud.resultLeaveButton.gameObject.activeSelf, Is.True);

                Assert.That(network.SetGameFlowEnabled(true), Is.True);
                yield return null;
                Assert.That(card.gameObject.activeSelf, Is.False, "Hapio waits for its own result Scene.");
                Assert.That(hud.resultRestartButton.gameObject.activeSelf, Is.False);
                Assert.That(hud.resultReturnLobbyButton.gameObject.activeSelf, Is.False);
                Assert.That(hud.resultLeaveButton.gameObject.activeSelf, Is.False);
                Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
                AssertOriginalCard();
            }
        }

        private UnityEngine.UI.Button Button(string name)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Button))
                .GetComponent<UnityEngine.UI.Button>();
            button.transform.SetParent(card, false);
            return button;
        }

        private static LabSnapshot Snapshot(LabPhase phase) => new LabSnapshot
        {
            sessionId = "legacy-result-367", round = 1, phase = phase, playedSeconds = 12d,
            paintCoverage = 1f, paintCoveredCount = 2048, paintCellCount = 2048,
            players = new[] { new LabPlayerState { id = 7, seat = 0, hitTotal = 3, generatedTotal = 2 } }
        };

        private void SetSnapshot(LabSnapshot snapshot) => typeof(LabNetwork)
            .GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(network, snapshot);

        private void AssertOriginalCard()
        {
            Assert.That(card.anchorMin, Is.EqualTo(anchorMin));
            Assert.That(card.anchorMax, Is.EqualTo(anchorMax));
            Assert.That(card.offsetMin, Is.EqualTo(offsetMin));
            Assert.That(card.offsetMax, Is.EqualTo(offsetMax));
        }
    }
}
