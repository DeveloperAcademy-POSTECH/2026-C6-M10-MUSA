using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Resources.Tests
{
    /// <summary>Saved T07 scene, actual NGO Host, real elapsed time and real PhysX; no physical-device claim.</summary>
    public sealed class ResourceIntegrationPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ResourceSmoke.unity";
        private const string TestPort = "25071";
        private T07ResourceController controller;
        private bool previousRunInBackground;

        [UnitySetUp]
        public IEnumerator LoadSavedSceneWithoutAutomaticHostOrInventory()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return LoadScene();
            yield return null;
            yield return null;
            controller = Object.FindAnyObjectByType<T07ResourceController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Resource, Is.Not.Null);
            Assert.That(controller.Attack.Connected, Is.False);
            Assert.That(controller.Resource.Connected, Is.False);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty, "Opening the scene cannot silently create a host.");
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<T06AttackController>(), Is.Empty, "The retained T06 fixture controller must not run in T07.");
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator ReleaseOnlyThisTestHostAndSavedScene()
        {
            try
            {
                if (controller != null) controller.EndDevelopmentTest();
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress), 5f,
                    "The T07 test-owned Host did not finish shutdown.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    var cleanup = SceneManager.CreateScene("T07 Resource Integration Cleanup");
                    SceneManager.SetActiveScene(cleanup);
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null;
                yield return null;
                Assert.That(Object.FindObjectsByType<T07ResourceController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<ResourceSession>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<EventSystem>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator NormalEmptyStartFivePaidGenerationsRealThreeSecondRecoveryAndEmptyReset()
        {
            yield return StartHost();
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            uint firstRound = controller.Attack.Snapshot.roundId;
            var created = new HashSet<string>();
            for (int index = 0; index < 5; index++)
            {
                Assert.That(controller.Resource.RequestGenerate(), Is.True);
                var reply = controller.Resource.LastResult;
                Assert.That(reply.known && reply.accepted, Is.True);
                Assert.That(reply.operation, Is.EqualTo((int)ResourceRequestKind.Generate));
                Assert.That(reply.staminaBefore - reply.staminaAfter, Is.EqualTo(20d).Within(1e-8));
                Assert.That(reply.confirmedOrb.kind, Is.EqualTo((int)OrbKind.Raw));
                Assert.That(created.Add(reply.confirmedOrb.id), Is.True);
            }
            Assert.That(controller.Resource.RequestGenerate(), Is.True, "Send the sixth request to verify actual Host rejection.");
            Assert.That(controller.Resource.LastResult.accepted, Is.False);
            Assert.That(controller.Resource.LastResult.reason, Is.EqualTo("INSUFFICIENT_STAMINA"));
            Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(5));
            Assert.That(controller.Views.Count, Is.EqualTo(5));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.EqualTo(5));
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.LessThan(1d), "Five immediate commands must spend the initial 100 points.");
            Assert.That(controller.Resource.CanGenerate, Is.False);

            double before = controller.Resource.LocalPlayer.stamina;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            yield return new WaitForSecondsRealtime(3.05f);
            yield return null;
            double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
            double expected = before + elapsed * (20d / 3d);
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(3d), "This trial uses real time, not a shortened recovery setting.");
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(expected).Within(.75d));
            Assert.That(controller.Resource.CanGenerate, Is.True);
            Assert.That(controller.Resource.RequestGenerate(), Is.True);
            Assert.That(controller.Resource.LastResult.accepted, Is.True);
            Assert.That(controller.Resource.LastResult.staminaBefore - controller.Resource.LastResult.staminaAfter, Is.EqualTo(20d).Within(1e-8));
            Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(6));
            Assert.That(controller.Resource.ResetNormalRound(), Is.True);
            yield return WaitFor(() => controller.Resource.Connected && controller.Attack.Snapshot.roundId > firstRound, 3f,
                "Normal reset did not bind the fresh resource round.");
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            Debug.Log("C6_T07_INTEGRATION initialOrbs=0 initialStamina=100 paidInitialGenerations=5 costEach=20 sixthRejected=true recoveryRealSeconds=3 normalResetEmpty=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator ExplicitDebugFixtureDoesNotReplacePaidGenerationAndRealHitRewardsExactlyFive()
        {
            yield return StartHost();
            Assert.That(controller.Resource.RequestDebugCombined(), Is.False, "Normal mode must not grant a free hit orb.");
            Assert.That(controller.Resource.BeginDebugFixtureRound(), Is.True);
            yield return WaitFor(() => controller.Views.Count == 5, 3f, "Explicit debug Raw fixtures did not appear.");
            var fixtures = controller.Attack.Registry.Snapshot();
            Assert.That(controller.Resource.DebugTestMode, Is.True);
            Assert.That(fixtures.All(orb => orb.Kind == OrbKind.Raw && orb.Polarity == OrbPolarity.Yin), Is.True);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
            Assert.That(controller.Resource.RequestGenerate(), Is.True);
            Assert.That(controller.Resource.LastResult.accepted, Is.True);
            Assert.That(controller.Resource.LastResult.staminaBefore - controller.Resource.LastResult.staminaAfter, Is.EqualTo(20d).Within(1e-8));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.EqualTo(1));

            var recoveries = new List<ResourceRecoveryResult>();
            controller.Resource.RecoveryResolved += recoveries.Add;
            Assert.That(controller.Resource.RequestDebugCombined(), Is.True);
            var reply = controller.Resource.LastResult;
            Assert.That(reply.accepted, Is.True);
            Assert.That(reply.reason, Is.EqualTo("DEBUG_COMBINED_FIXTURE"));
            Assert.That(reply.staminaAfter, Is.EqualTo(reply.staminaBefore));
            string orbId = reply.confirmedOrb.id;
            yield return LaunchAndHit(orbId, 7201, 80);
            Assert.That(recoveries.Count, Is.EqualTo(1));
            Assert.That(recoveries[0].Accepted && recoveries[0].Applied, Is.True);
            Assert.That(recoveries[0].PlayerId, Is.EqualTo(controller.Attack.LocalPlayerId));
            Assert.That(recoveries[0].Added, Is.EqualTo(5d).Within(1e-10));
            Assert.That(controller.Attack.Registry.TryGet(orbId, out var consumed), Is.True);
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(recoveries.Count, Is.EqualTo(1), "Extra physics frames cannot grant a second hit bonus.");
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(1));
            Debug.Log("C6_T07_INTEGRATION mode=DEBUG_TEST_MODE fixedRaw=5 paidGenerateCost=20 debugCombinedSeparate=true realHit=1 hp=80 added=5 source=DEBUG_POINTER physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator KillingHitStillRewardsFiveThenRecoveryStopsAndOldHitCannotRewardNewRound()
        {
            yield return StartHost();
            Assert.That(controller.Resource.BeginDebugFixtureRound(), Is.True);
            for (int index = 0; index < 5; index++)
            {
                Assert.That(controller.Resource.RequestGenerate(), Is.True);
                Assert.That(controller.Resource.LastResult.accepted, Is.True);
            }
            var recoveries = new List<ResourceRecoveryResult>();
            var hits = new List<AttackHitResult>();
            controller.Resource.RecoveryResolved += recoveries.Add;
            controller.Attack.ValidHit += hits.Add;
            for (int index = 0; index < 5; index++)
            {
                Assert.That(controller.Resource.RequestDebugCombined(), Is.True);
                Assert.That(controller.Resource.LastResult.accepted, Is.True);
                yield return LaunchAndHit(controller.Resource.LastResult.confirmedOrb.id, 7300 + index, 100 - (index + 1) * 20);
                Assert.That(recoveries.Count, Is.EqualTo(index + 1));
                Assert.That(recoveries[index].Added, Is.EqualTo(5d).Within(1e-10));
            }
            Assert.That(controller.Attack.Snapshot.state, Is.EqualTo("TargetCleared"));
            Assert.That(controller.Resource.Snapshot.playing, Is.False);
            Assert.That(controller.Resource.Authority.IsPlaying, Is.False);
            Assert.That(controller.Resource.Authority.IsEnded, Is.False, "TargetCleared must still admit its already accepted final hit event.");
            Assert.That(hits.Count, Is.EqualTo(5));
            double frozen = controller.Resource.LocalPlayer.stamina;
            yield return new WaitForSecondsRealtime(3.05f);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(frozen).Within(1e-10));
            Assert.That(controller.Resource.RequestGenerate(), Is.False);
            Assert.That(controller.Resource.RequestDebugCombined(), Is.False);

            uint previousRound = controller.Attack.Snapshot.roundId;
            Assert.That(controller.Resource.ResetNormalRound(), Is.True);
            yield return WaitFor(() => controller.Resource.Connected && controller.Attack.Snapshot.roundId > previousRound, 3f,
                "Explicit normal reset did not end the debug round.");
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            var stale = controller.Resource.Authority.ApplyValidHit(hits.Last(), Time.realtimeSinceStartupAsDouble);
            Assert.That(stale.Accepted, Is.False);
            Assert.That(stale.Reason, Is.EqualTo("ROUND_MISMATCH"));
            Assert.That(stale.Added, Is.Zero);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Debug.Log("C6_T07_INTEGRATION realHits=5 hitRecoveryEach=5 killingHitRecovery=5 targetHp=0 recoveryStopped=true normalResetEmpty=true staleHitRejected=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator ApplicationPauseFreezesRecoveryAndResumeExcludesThePausedInterval()
        {
            yield return StartHost();
            Assert.That(controller.Resource.RequestGenerate(), Is.True);
            Assert.That(controller.Resource.LastResult.accepted, Is.True);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.LessThan(81d));

            // Simulate Unity's lifecycle callback on this component only. This does not suspend iOS
            // or change global application focus, and is not physical-device lifecycle evidence.
            controller.Resource.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            Assert.That(controller.Resource.Authority.IsPlaying, Is.False);
            Assert.That(controller.Resource.Snapshot.playing, Is.False);
            Assert.That(controller.Resource.CanGenerate, Is.False);
            double frozen = controller.Resource.LocalPlayer.stamina;
            yield return new WaitForSecondsRealtime(.2f);
            yield return null;
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(frozen).Within(1e-10),
                "Ordinary Update calls during pause must not re-enable resource recovery.");

            controller.Resource.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Assert.That(controller.Resource.Authority.IsPlaying, Is.True);
            Assert.That(controller.Resource.Snapshot.playing, Is.True);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(frozen).Within(1e-10),
                "Resume must not apply the elapsed paused interval as a catch-up grant.");
            double resumedAt = Time.realtimeSinceStartupAsDouble;
            yield return new WaitForSecondsRealtime(.2f);
            yield return null;
            double elapsed = Time.realtimeSinceStartupAsDouble - resumedAt;
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.GreaterThan(frozen + .5d));
            Assert.That(controller.Resource.LocalPlayer.stamina,
                Is.EqualTo(frozen + elapsed * (20d / 3d)).Within(.75d));
            Debug.Log("C6_T07_INTEGRATION source=SIMULATED_APPLICATION_PAUSE pausedRecovery=0 resumeCatchUp=0 resumedRecovery=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator DisablingResourceServiceEndsItsHostAndReenableCannotRefillAnActiveRound()
        {
            yield return StartHost();
            string previousSession = controller.Attack.Snapshot.sessionId;
            Assert.That(controller.Resource.RequestGenerate(), Is.True);
            Assert.That(controller.Resource.LastResult.accepted, Is.True);
            Assert.That(controller.Views.Count, Is.EqualTo(1));
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.LessThan(81d));

            controller.Resource.enabled = false;
            yield return WaitFor(() => controller.Attack.Connection.CanStart && !controller.Attack.Connected
                && controller.Views.Count == 0 && Object.FindObjectsByType<NetworkManager>().All(m => !m.IsListening && !m.ShutdownInProgress),
                5f, "Disabling resources must end this Host and release its generated inventory.");
            Assert.That(controller.Resource.Connected, Is.False);
            Assert.That(controller.Resource.HasPending, Is.False);
            Assert.That(controller.Resource.Authority, Is.Null);
            Assert.That(controller.Resource.Snapshot, Is.Null);
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending, Is.False);

            controller.Resource.enabled = true;
            yield return null;
            yield return null;
            Assert.That(controller.Attack.Connected, Is.False, "Re-enabling a component cannot silently start a new Host.");
            Assert.That(controller.Resource.Connected, Is.False);
            Assert.That(controller.Resource.Authority, Is.Null, "Re-enable cannot replace an active inventory with fresh full stamina.");
            Assert.That(controller.Resource.Snapshot, Is.Null);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>().All(m => !m.IsListening && !m.ShutdownInProgress), Is.True,
                "The connection owner keeps its stopped manager for explicit reuse.");

            yield return StartHost();
            Assert.That(controller.Attack.Snapshot.sessionId, Is.Not.EqualTo(previousSession));
            Assert.That(controller.Attack.Snapshot.roundId, Is.EqualTo(1));
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
            Assert.That(controller.Resource.HasPending, Is.False);
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            Debug.Log("C6_T07_INTEGRATION resourceDisableEndsHost=true clearedViews=true reenableDoesNotStartHost=true explicitFreshSessionEmpty=true explicitFreshStamina=100 physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator TwentyDistinctStoredOrbsFillTheCapAndRejectedGenerationCannotSpend()
        {
            yield return StartHost();
            Assert.That(controller.Resource.BeginDebugFixtureRound(), Is.True);
            for (int index = 0; index < 15; index++)
            {
                Assert.That(controller.Resource.RequestDebugCombined(), Is.True);
                Assert.That(controller.Resource.LastResult.accepted, Is.True);
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            var records = controller.Attack.Registry.Snapshot();
            Assert.That(records.Count, Is.EqualTo(20));
            Assert.That(records.Select(orb => orb.OrbId).Distinct().Count(), Is.EqualTo(20));
            Assert.That(records.Select(orb => orb.NormalizedPosition).Distinct().Count(), Is.EqualTo(20), "Debug fixtures must occupy independent grid positions.");
            Assert.That(controller.Views.Count, Is.EqualTo(20));
            var screenPositions = records.Select(orb => controller.GetViewScreenPosition(orb.OrbId)).ToArray();
            for (int left = 0; left < screenPositions.Length; left++)
                for (int right = left + 1; right < screenPositions.Length; right++)
                    Assert.That(Vector2.Distance(screenPositions[left], screenPositions[right]), Is.GreaterThan(1f), "Different orbs cannot collapse into one display position.");
            Assert.That(controller.Resource.LocalPlayer.storedOrbs, Is.EqualTo(20));
            Assert.That(controller.Resource.CanGenerate, Is.False);
            Assert.That(controller.Hud.GenerateButton.interactable, Is.False);
            Assert.That(controller.Resource.RequestGenerate(), Is.True, "The Host must reject a sent capacity request even if UI normally prevents it.");
            Assert.That(controller.Resource.LastResult.known, Is.True);
            Assert.That(controller.Resource.LastResult.accepted, Is.False);
            Assert.That(controller.Resource.LastResult.reason, Is.EqualTo("STORAGE_FULL"));
            Assert.That(controller.Resource.LastResult.staminaBefore, Is.EqualTo(100));
            Assert.That(controller.Resource.LastResult.staminaAfter, Is.EqualTo(100));
            Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(20));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
            Debug.Log("C6_T07_INTEGRATION mode=DEBUG_TEST_MODE storedOrbs=20 uniquePositions=20 capacityRejected=true rejectedDebit=0 generateButtonDisabled=true physicalDevice=false");
        }

        private IEnumerator StartHost()
        {
            Assert.That(controller.StartDevelopmentHost(TestPort), Is.True);
            yield return WaitFor(() => controller.Attack.IsHost && controller.Resource.IsHost && controller.Resource.Connected,
                5f, "The actual NGO Host did not bind T07 empty resource state.");
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
            var manager = controller.GetComponent<DirectConnectionSession>().OwnedManager;
            Assert.That(manager.IsHost && manager.IsListening, Is.True);
        }

        private IEnumerator LaunchAndHit(string orbId, int pointerId, int expectedHp)
        {
            yield return WaitFor(() => controller.Views.ContainsKey(orbId), 3f, "The confirmed debug Combined view did not appear.");
            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
            Vector2 start = controller.GetViewScreenPosition(orbId);
            Vector2 zone = new Vector2(start.x, controller.Layout.BottomPixelRect.yMax - 1f);
            Assert.That(controller.BeginPointer(pointerId, start, false), Is.True);
            controller.MovePointer(pointerId, zone);
            Assert.That(controller.Views.ContainsKey(orbId), Is.False);
            Assert.That(controller.Attack.Registry.TryGet(orbId, out var flying), Is.True);
            Assert.That(flying.AuthorityState, Is.EqualTo(OrbAuthorityState.Projectile));
            var projectile = Object.FindObjectsByType<HostProjectile3D>().Single();
            Assert.That(projectile.OrbId, Is.EqualTo(orbId));
            Assert.That(projectile.Body.isKinematic, Is.False);
            Assert.That(projectile.Body.linearVelocity.magnitude, Is.GreaterThan(0));
            controller.EndPointer(pointerId, zone);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == expectedHp, 4f, "The actual Rigidbody did not hit the target.");
            yield return null;
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float seconds, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private static AsyncOperation LoadScene()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        }
    }
}
