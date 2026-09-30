using System;
using System.Collections;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>
    /// Enduring battle rules through the currently shipped scene, its authored HUD, a local NGO
    /// Host, controller pointer input and the Host's physical projectile. The explicit solo and
    /// Raw-geometry fixtures are neither a multiplayer-room test nor device Touch evidence.
    /// </summary>
    public sealed class CurrentBattleFlowPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        private const string Port = "25163";
        private T09BattleController controller;
        private bool previousRunInBackground;
        private int nextPointer = 26000;

        [UnitySetUp]
        public IEnumerator OpenCurrentSceneWithoutImplicitGameplay()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
            yield return null;
            yield return null;
            controller = Object.FindAnyObjectByType<T09BattleController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Hud.UseSceneHierarchy, Is.True);
            Assert.That(controller.Hud.ValidateSceneHierarchy(out var error), Is.True, error);
            Assert.That(controller.OrbPhysicsEnabled && controller.ReleaseThrowsEnabled
                && controller.ContinuousTransfersEnabled, Is.True,
                "The current scene must retain its physics, release-throw and continuous-transfer opt-ins.");
            Assert.That(controller.Attack.Connected, Is.False);
            Assert.That(controller.Views, Is.Empty);
        }

        [UnityTearDown]
        public IEnumerator StopFixtureAndUnloadCurrentScene()
        {
            try
            {
                if (controller != null)
                {
                    controller.ConfigureApprovedLifecycle(null, null, null);
                    controller.EndDevelopmentTest();
                }
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                    "The local battle Host did not finish shutting down.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("Current Battle Flow Cleanup"));
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null;
                yield return null;
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator PaidGenerationShortClockAndRetryResetTheActualSavedScene()
        {
            yield return ConnectSolo(2d);
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Ready));
            Assert.That(controller.RequestGenerate(), Is.False);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina,
                Is.EqualTo(controller.Layout.Config.StaminaStart));
            int maximumHp = controller.Attack.Snapshot.maxHp;

            yield return StartBattle();
            Assert.That(controller.Views, Is.Empty, "A fresh round must not supply free orbs.");
            Assert.That(controller.RequestGenerate(), Is.True);
            var generated = controller.Resource.LastResult;
            Assert.That(generated, Is.Not.Null);
            Assert.That(generated.known && generated.accepted, Is.True, generated.reason);
            Assert.That(generated.staminaBefore - generated.staminaAfter,
                Is.EqualTo(controller.Layout.Config.GenerateCost).Within(.000001d));
            Assert.That(generated.confirmedOrb.kind, Is.EqualTo((int)OrbKind.Raw));
            Assert.That(controller.Views.Count, Is.EqualTo(1));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.EqualTo(1));

            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Defeat,
                "The Host's real two-second test clock did not resolve Defeat.", 4d);
            Assert.That(controller.Battle.Snapshot.remaining, Is.Zero);
            Assert.That(controller.RequestGenerate(), Is.False);
            double frozenTime = controller.Battle.Snapshot.remaining;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(controller.Battle.Snapshot.remaining, Is.EqualTo(frozenTime));

            uint playedRound = controller.Battle.Snapshot.roundId;
            Assert.That(controller.Battle.RetryHost(), Is.True);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Ready
                && controller.Battle.Snapshot.roundId > playedRound,
                "Retry did not create a fresh Ready round.");
            Assert.That(controller.Battle.Snapshot.remaining, Is.EqualTo(2d));
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(maximumHp));
            Assert.That(controller.Resource.LocalPlayer.stamina,
                Is.EqualTo(controller.Layout.Config.StaminaStart));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.RequestGenerate(), Is.False);
        }

        [UnityTest]
        public IEnumerator ExplicitSameElementRawDropWaitsForReleaseThenBallisticHitChangesHp()
        {
            yield return ConnectSolo();
            controller.ConfigureSelectedElement(OrbElement.Fire);
            yield return StartBattle();
            int startingHp = controller.Attack.Snapshot.hp;
            var source = controller.Attack.Registry.RegisterDevelopmentOrb(controller.Attack.LocalPlayerId,
                OrbKind.Raw, OrbPolarity.Yin, new Vector2(.38f, .6f), OrbElement.Fire);
            // These two Raw records explicitly have the same element. Their unrelated ID hashes
            // must not decide the Drop; choose unlike hashes to guard that boundary deterministically.
            OrbRecord target = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var candidate = controller.Attack.Registry.RegisterDevelopmentOrb(controller.Attack.LocalPlayerId,
                    OrbKind.Raw, OrbPolarity.Yang, attempt == 0 ? new Vector2(.62f, .6f)
                        : new Vector2(.08f + attempt * .09f, .18f), OrbElement.Fire);
                if (OrbElements.RawElement(source.OrbId) == OrbElements.RawElement(candidate.OrbId)) continue;
                target = candidate;
                break;
            }
            Assert.That(target, Is.Not.Null, "The fixture could not create two unlike legacy ID hashes.");
            // Use the chosen record's real saved position; no test directly moves Host inventory.
            controller.Attack.PublishInventoryChange("current-scene-explicit-fire-materials");
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
            Vector2 from = controller.GetViewScreenPosition(source.OrbId);
            Vector2 to = controller.GetViewScreenPosition(target.OrbId);
            int pointer = ++nextPointer;
            Assert.That(controller.BeginPointer(pointer, from, false), Is.True);
            for (int step = 1; step <= 6; step++)
            {
                controller.MovePointer(pointer, Vector2.Lerp(from, to, step / 6f));
                yield return null;
                Assert.That(controller.Combination.LastResult, Is.Null,
                    "Merely touching while held must not combine the materials.");
            }
            controller.EndPointer(pointer, to);
            yield return WaitFor(() => controller.Combination.LastResult != null
                && !controller.HasCombinationPending,
                "The current scene did not resolve the same-element Drop.");
            var combination = controller.Combination.LastResult;
            Assert.That(combination.known && combination.accepted, Is.True, combination.reason);
            Assert.That(combination.currentSource.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            Assert.That(combination.currentTarget.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            string combinedId = combination.originalCombined.id;
            Assert.That(OrbElements.TryDecodeCombinedId(combinedId, out var yin, out var yang), Is.True);
            Assert.That(yin, Is.EqualTo(OrbElement.Fire));
            Assert.That(yang, Is.EqualTo(OrbElement.Fire));
            yield return WaitFor(() => controller.Views.ContainsKey(combinedId),
                "The confirmed Combined orb was not displayed.");

            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
            pointer = ++nextPointer;
            Assert.That(controller.BeginPointer(pointer, controller.GetViewScreenPosition(combinedId), false), Is.True);
            Vector2 entry = new Vector2(controller.Layout.BottomPixelRect.center.x,
                controller.Layout.BottomPixelRect.yMax + Screen.width * .015f);
            controller.MovePointer(pointer, entry);
            yield return new WaitForSecondsRealtime(.14f);
            double started = Time.unscaledTimeAsDouble;
            Vector2 release = entry;
            for (int step = 0; step < 4; step++)
            {
                yield return new WaitForSecondsRealtime(.025f);
                release = entry + Vector2.up * (float)((Time.unscaledTimeAsDouble - started) * Screen.width * 1.25);
                Assert.That(release.y, Is.LessThan(Screen.height));
                controller.MovePointer(pointer, release);
            }
            Assert.That(controller.ThrowArmed, Is.True);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty,
                "The held orb cannot become a projectile before release.");
            controller.EndPointer(pointer, release);
            Assert.That(controller.Views.ContainsKey(combinedId), Is.False);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == startingHp - controller.Layout.Config.BaseDamage,
                "The release-created Host projectile did not physically hit the monster.", 5d);
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(1));
            Assert.That(controller.Attack.Registry.TryGet(combinedId, out var consumed), Is.True);
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
        }

        private IEnumerator ConnectSolo(double? duration = null)
        {
            // This assembly does not reference the room services. Disable supervision only for
            // the explicit local Host fixture; the saved scene and Controller remain unchanged.
            foreach (string typeName in new[] { "C6.Prototype.GameSync.T10GameSession",
                "C6.Prototype.Lobby.T10LobbyController", "C6.Prototype.Lobby.T10LobbySession" })
                controller.GetComponents<MonoBehaviour>().Single(component => component.GetType().FullName == typeName).enabled = false;
            controller.ConfigureApprovedLifecycle(null, null, null);
            foreach (var canvas in controller.GetComponentsInChildren<Canvas>(true))
                if (canvas != controller.Hud.Canvas) canvas.gameObject.SetActive(false);
            controller.Hud.Canvas.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();

            controller.ConfigureDevelopmentSolo(true);
            controller.ConfigureDevelopmentDuration(duration);
            Assert.That(controller.StartDevelopmentHost(Port), Is.True);
            yield return WaitFor(() => controller.Battle.CanStart && controller.Resource.IsHost
                && controller.Combination.IsHost,
                "The explicit current-scene Host did not reach Ready.");
        }

        private IEnumerator StartBattle()
        {
            Assert.That(controller.RequestHostStart(), Is.True);
            yield return WaitFor(() => controller.CanInteract && controller.Battle.Phase == BattlePhase.Playing,
                "The current scene did not enter an interactive Playing round.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message, double timeoutSeconds = 5d)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
