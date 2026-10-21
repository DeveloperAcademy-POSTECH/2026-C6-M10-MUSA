using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabJigwiSavedSceneTests
    {
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();
        private LabConfig tuning;
        private LabNetwork network;
        private LabJigwiHud ui;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return fixture.Cleanup();
            if (tuning != null) Object.Destroy(tuning);
            tuning = null;
            network = null;
            yield return null;
        }

        private IEnumerator StartSavedSolo(float fireSeconds = 1f)
        {
            yield return fixture.Load();
            var developer = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            network = developer.network;
            tuning = Object.Instantiate(network.config);
            float[] values = tuning.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 120f);
            Set(values, "Jigwi Fire Seconds", fireSeconds);
            Set(values, "Jigwi Overheat Seconds", 4f);
            Set(values, "Jigwi Fire Target Count", 1f);
            Set(values, "Yin Chance (%)", 100f);
            Set(values, "Yang Chance (%)", 0f);
            Set(values, "Relay Orb Chance (%)", 0f);
            Set(values, "Secret Box Chance (%)", 0f);
            Set(values, "Potion Chance (%)", 0f);
            Set(values, "Bomb Chance (%)", 0f);
            Set(values, "Sabotage Mix (0 off, 1 on)", 0f);
            Set(values, "Gourd Bomb Interval Min (seconds, 0 = off)", 0f);
            Set(values, "Gourd Bomb Interval Max (seconds, 0 = off)", 0f);
            Set(values, "Swipe Block Interval (seconds, 0 = off)", 0f);
            Set(values, "Stamina Recovery Amount", 0f);
            Assert.That(tuning.TryApplyDeveloperValues(values, out string error), Is.True, error);
            network.config = tuning;
            developer.sceneController.config = tuning;
            developer.sceneController.ReconfigureBoard();
            fixture.UseAvailablePort(network);
            Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
            yield return null;
            ui = Object.FindFirstObjectByType<LabJigwiHud>();
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.network, Is.SameAs(network));
            Assert.That(ui.fireButton, Is.Not.Null);
            Assert.That(ui.testToggle.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedManualButtonsSupplyWaterThenFailureBurnsOnlyReleasedOrbAndPaidGeneration()
        {
            yield return StartSavedSolo();
            float stamina = network.Snapshot.players.Single().stamina;
            ui.testToggle.onClick.Invoke();
            Assert.That(ui.testPanel.activeSelf, Is.True);
            ui.fireButton.onClick.Invoke();
            Assert.That(ui.testPanel.activeSelf, Is.False);
            LabJigwiState fire = network.Snapshot.JigwiFor(network.LocalPlayerId);
            Assert.That(fire.targets.Length, Is.EqualTo(1));
            Assert.That(network.Snapshot.orbs.Length, Is.EqualTo(3));
            Assert.That(network.Snapshot.orbs.All(orb => orb.kind == LabOrbKind.Combined
                && orb.element == LabOrbElement.Water && orb.jigwiSupply && !orb.inFlight), Is.True);
            Assert.That(network.Snapshot.players.Single().stamina, Is.EqualTo(stamina));
            Assert.That(fire.Overheated(network.Snapshot.serverTime), Is.False);
            Assert.That(network.PauseBattle(), Is.True);
            double pausedAt = network.Snapshot.serverTime;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(network.Snapshot.serverTime, Is.EqualTo(pausedAt).Within(.0001d));
            Assert.That(network.DevStartJigwiGenerationBurn(network.LocalPlayerId), Is.False);
            Assert.That(network.ResumeBattle(), Is.True);
            yield return Until(() => network.Snapshot.JigwiFor(network.LocalPlayerId).Overheated(network.Snapshot.serverTime),
                "Actual Host deadline must produce overheat only after failed extinction.");
            var board = network.board;
            string first = network.Snapshot.orbs[0].id;
            Assert.That(board.TryGetMotion(first, out Vector2 initial, out _), Is.True);
            Vector2 grab = ToWorld(board.CenterBounds, initial);
            Assert.That(board.TryBeginDragAtWorld(grab, 10d), Is.True);
            Vector2 release = ToWorld(board.CenterBounds, new Vector2(.5f, .72f));
            Assert.That(board.DragToWorld(release, 10.5d), Is.True);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == first), Is.True, "Holding itself must not consume the orb.");
            Assert.That(board.EndDragAtWorld(release, 10.6d), Is.True);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == first), Is.False);
            Assert.That(network.Snapshot.orbs.Length, Is.EqualTo(2));
            string second = network.Snapshot.orbs[0].id;
            Assert.That(board.TryGetMotion(second, out Vector2 secondAt, out _), Is.True);
            Assert.That(board.TryBeginDragAtWorld(ToWorld(board.CenterBounds, secondAt), 11d), Is.True);
            board.CancelDrag();
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == second), Is.True, "Cancelled touches are not releases.");
            network.Generate();
            Assert.That(network.Snapshot.orbs.Length, Is.EqualTo(2), "The generated orb burns instead of remaining stored.");
            Assert.That(network.Snapshot.players.Single().stamina, Is.EqualTo(stamina - tuning.GenerateCost).Within(.001f));
            Assert.That(network.Snapshot.drums.Length, Is.EqualTo(1));
            Assert.That(network.RestartDeveloperSolo(), Is.True);
            Assert.That(network.Snapshot.jigwi, Is.Empty);
            Assert.That(network.Snapshot.orbs, Is.Empty);
        }

        [UnityTest]
        public IEnumerator GenerationButtonAppliesActualSmallerRawAndRestartClearsItsState()
        {
            yield return StartSavedSolo();
            ui.testToggle.onClick.Invoke();
            ui.generationButton.onClick.Invoke();
            network.Generate();
            LabOrbState raw = network.Snapshot.orbs.Single();
            Assert.That(raw.kind, Is.EqualTo(LabOrbKind.Yin));
            Assert.That(raw.jigwiGenerationBurn, Is.True);
            Assert.That(raw.small, Is.False, "Jigwi must not set the legacy blast flag.");
            Assert.That(raw.expiresAt - raw.createdAt, Is.EqualTo(tuning.OrbLifetimeSeconds * tuning.JigwiRawLifeScale).Within(.001f));
            var view = network.board.GetComponentsInChildren<LabOrbView>().Single(item => item.OrbId == raw.id);
            Assert.That(view.HitSphere.radius, Is.EqualTo(tuning.OrbRadius * tuning.JigwiRawRadiusScale).Within(.001f));
            Assert.That(network.RestartDeveloperSolo(), Is.True);
            network.Generate();
            Assert.That(network.Snapshot.orbs.Single().jigwiGenerationBurn, Is.False);
        }

        [UnityTest]
        public IEnumerator RealFireCollisionThroughHostCallbackExtinguishesWithoutPaintOrHitRewards()
        {
            yield return StartSavedSolo(15f);
            Assert.That(network.DevStartJigwiFire(network.LocalPlayerId), Is.True);
            var surface = Object.FindObjectsByType<LabJigwiFireSurface>(FindObjectsSortMode.None)
                .Single(item => item.OwnerPlayerId == network.LocalPlayerId);
            LabSnapshot before = network.Snapshot;
            LabOrbState water = before.orbs.First();
            var model = (LabModel)typeof(LabNetwork).GetField("model", Private).GetValue(network);
            // Fixture launches an actual physical projectile near the saved fire collider, then
            // uses the production Host callback. This tests contact/consumption, not finger aiming.
            Assert.That(model.TryLaunch(network.LocalPlayerId, water.id, "fixture-fire-launch", before.serverTime,
                out _, out string reason), Is.True, reason);
            Vector3 origin = surface.transform.position + Vector3.forward * .8f;
            var projectile = LabProjectile.Create(origin, tuning, network.transform, LabOrbElement.Water);
            projectile.SetFireSurfaces(new[] { surface });
            MethodInfo callback = typeof(LabNetwork).GetMethod("OnProjectileResolved", Private);
            Assert.That(projectile.Launch(water.id, network.LocalPlayerId, Vector3.back * 12f, tuning,
                network.target, result => callback.Invoke(network, new object[] { result })), Is.True);
            yield return Until(() => network.Snapshot.JigwiFor(network.LocalPlayerId).targets.All(item => item.extinguished),
                "An actual swept/contact Fire result must flow through the Host's separate extinction rule.");
            Assert.That(network.Snapshot.hp, Is.EqualTo(before.hp));
            Assert.That(network.Snapshot.paintCoverage, Is.EqualTo(before.paintCoverage));
            Assert.That(network.Snapshot.players.Single().hitTotal, Is.EqualTo(before.players.Single().hitTotal));
            Assert.That(network.Snapshot.players.Single().stamina, Is.EqualTo(before.players.Single().stamina));
            Assert.That(network.Snapshot.orbs.Any(item => item.id == water.id), Is.False);
            Assert.That(network.Snapshot.JigwiFor(network.LocalPlayerId).Overheated(network.Snapshot.serverTime), Is.False);
        }

        private static Vector2 ToWorld(Rect bounds, Vector2 normalized) => new Vector2(
            Mathf.Lerp(bounds.xMin, bounds.xMax, normalized.x), Mathf.Lerp(bounds.yMin, bounds.yMax, normalized.y));
        private static void Set(float[] values, string label, float value)
        {
            int index = Array.FindIndex(LabConfig.DeveloperFields.ToArray(), item => item.Label == label);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), label);
            values[index] = value;
        }
        private static IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
