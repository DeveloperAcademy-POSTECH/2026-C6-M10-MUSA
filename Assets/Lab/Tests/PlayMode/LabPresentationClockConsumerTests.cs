using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>
    /// The battle HUDs read one Guest presentation clock (#463, <see cref="LabNetwork.BattlePresentationTime"/>) instead
    /// of each estimating the Host time on its own: a late snapshot a little behind does not rewind them, and a pause
    /// holds the time they all read.
    /// </summary>
    public sealed class LabPresentationClockConsumerTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root, canvasRoot;
        private LabNetwork network;
        private LabConfig config;
        private LabFeverTimeHud fever;
        private LabCharmHud charm;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Presentation clock fixture");
            network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            config = ScriptableObject.CreateInstance<LabConfig>();
            network.config = config;
            canvasRoot = new GameObject("Presentation clock canvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var battle = new GameObject("Battle UI", typeof(RectTransform)).GetComponent<RectTransform>();
            battle.SetParent(canvasRoot.transform, false);
            var bar = new GameObject("Stamina bar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(battle, false);
            bar.sizeDelta = new Vector2(223f, 44f);
            var timer = new GameObject("Time", typeof(RectTransform)).AddComponent<Text>();
            timer.transform.SetParent(battle, false);
            fever = new GameObject("Fever HUD").AddComponent<LabFeverTimeHud>();
            fever.transform.SetParent(canvasRoot.transform, false);
            fever.Bind(network, canvasRoot.GetComponent<Canvas>(), bar, battle, timer);
            charm = new GameObject("Charm HUD").AddComponent<LabCharmHud>();
            charm.transform.SetParent(canvasRoot.transform, false);
            charm.Bind(network, canvasRoot.GetComponent<Canvas>());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(canvasRoot);
            Object.Destroy(root);
            Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALateSnapshotDoesNotRewindTheHudsAndAPauseHoldsTheirTime()
        {
            // Fever and the charm on me both begin at Host time 20; the Guest last heard 19.7.
            SetLatest(Battle(19.7d));
            Assert.That(network.IsHost, Is.False, "This is the Guest clock.");
            yield return null;
            Assert.That(network.BattlePresentationTime, Is.LessThan(20d));
            Assert.That(fever.Showing || fever.SmokeShowing, Is.False, "Nothing before Host time 20.");
            Assert.That(charm.GaugeVisible, Is.False);

            yield return WaitUntilPresentationReaches(20.15d);
            yield return null;
            Assert.That(fever.Showing || fever.SmokeShowing, Is.True, "Fever began between snapshots.");
            Assert.That(charm.GaugeVisible, Is.True, "The charm began on the same clock.");

            // A snapshot sent a little earlier arrives late. Each HUD's own estimate used to restart from it,
            // putting both back before 20 and turning them off again.
            double before = network.BattlePresentationTime;
            SetLatest(Battle(System.Math.Max(19.8d, before - .2d)));
            yield return null;
            yield return null;
            Assert.That(network.BattlePresentationTime, Is.GreaterThanOrEqualTo(before), "The shared clock never steps back.");
            Assert.That(fever.Showing || fever.SmokeShowing, Is.True, "The late snapshot does not turn fever off.");
            Assert.That(charm.GaugeVisible, Is.True, "...nor the charm.");

            // Pause: every reader gets the same time, which holds while real time passes.
            LabSnapshot paused = Battle(20.4d);
            paused.paused = true;
            SetLatest(paused);
            double held = network.BattlePresentationTime;
            float until = Time.realtimeSinceStartup + .3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.That(network.BattlePresentationTime, Is.EqualTo(held), "A pause holds the presentation time.");
            Assert.That(held, Is.GreaterThanOrEqualTo(before));
            Assert.That(fever.Showing, Is.False, "The fever bubble hides while paused.");
        }

        private IEnumerator WaitUntilPresentationReaches(double time)
        {
            float until = Time.realtimeSinceStartup + 2f;
            while (network.BattlePresentationTime < time && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(network.BattlePresentationTime, Is.GreaterThanOrEqualTo(time), "The Guest clock extrapolates.");
        }

        private void SetLatest(LabSnapshot snapshot) => typeof(LabNetwork).GetField("latest", Private).SetValue(network, snapshot);

        private LabSnapshot Battle(double serverTime) => new LabSnapshot
        {
            sessionId = "presentation-clock", round = 1, phase = LabPhase.Playing, serverTime = serverTime,
            feverTimeAt = 20d, charmedPlayer = network.LocalPlayerId, charmStartedAt = 20d, charmUntil = 30d,
        };
    }
}
