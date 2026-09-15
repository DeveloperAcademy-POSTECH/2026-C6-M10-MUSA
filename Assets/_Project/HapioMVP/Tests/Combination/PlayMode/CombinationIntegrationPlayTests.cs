using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Resources;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Combination.Tests
{
    /// <summary>Saved T08 scene, actual NGO Host, controller pointer path and real PhysX; no device-touch claim.</summary>
    public sealed class CombinationIntegrationPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/CombinationSmoke.unity";
        private const string TestPort = "25081";
        private T08CombinationController controller;
        private bool previousRunInBackground;

        [UnitySetUp]
        public IEnumerator LoadSavedSceneWithoutStartingSessionOrSupplyingOrbs()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return LoadScene(); yield return null; yield return null;
            controller = Object.FindAnyObjectByType<T08CombinationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Resource, Is.Not.Null); Assert.That(controller.Combination, Is.Not.Null);
            Assert.That(controller.Attack.Connected || controller.Resource.Connected || controller.Combination.Connected, Is.False);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<T06AttackController>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<T07ResourceController>(), Is.Empty);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator ReleaseOnlyTheTestOwnedHostAndScene()
        {
            try
            {
                if (controller != null) controller.EndDevelopmentTest();
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress), 5f, "T08 test-owned Host shutdown did not complete.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    var cleanup = SceneManager.CreateScene("T08 Combination Integration Cleanup");
                    SceneManager.SetActiveScene(cleanup);
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null; yield return null;
                Assert.That(Object.FindObjectsByType<T08CombinationController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<CombinationSession>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<ResourceSession>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<EventSystem>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator NormalRoundStartsEmptyAndEveryCreatedRawStillCostsTwenty()
        {
            yield return StartHost();
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(controller.Resource.RequestGenerate(), Is.True);
            var generated = controller.Resource.LastResult;
            Assert.That(generated.known && generated.accepted, Is.True);
            Assert.That(generated.staminaBefore - generated.staminaAfter, Is.EqualTo(20d).Within(1e-8));
            Assert.That(generated.confirmedOrb.kind, Is.EqualTo((int)OrbKind.Raw));
            Assert.That(controller.Views.Count, Is.EqualTo(1));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.EqualTo(1));
            Assert.That(controller.Attack.Registry.Snapshot().All(orb => orb.Kind == OrbKind.Raw), Is.True);
            Debug.Log("C6_T08_INTEGRATION normalInitialOrbs=0 normalInitialStamina=100 generatedRaw=1 confirmedCost=20 physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator OppositeRawBothDropDirectionsWaitForUpAndDuplicateCreatesNoSecondCombined()
        {
            yield return StartHost();
            for (int direction = 0; direction < 2; direction++)
            {
                yield return BeginMixedFixture();
                var pair = VerticalPair();
                var source = direction == 0 ? pair[0] : pair[1];
                var target = direction == 0 ? pair[1] : pair[0];
                Vector2 start = controller.GetViewScreenPosition(source.OrbId);
                Vector2 end = controller.GetViewScreenPosition(target.OrbId);
                int pointer = 8100 + direction;
                Assert.That(controller.BeginPointer(pointer, start, false), Is.True);
                controller.MovePointer(pointer, end);
                yield return null;
                Assert.That(controller.Combination.LastResult, Is.Null, "Contact while the pointer remains down cannot combine.");
                Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(5));
                Assert.That(controller.Views.Count, Is.EqualTo(5));
                Assert.That(controller.Attack.Registry.TryGet(source.OrbId, out var untouched), Is.True);
                Assert.That(untouched, Is.SameAs(source));
                Vector2 displayedSource = controller.GetViewScreenPosition(source.OrbId);
                Vector2 displayedTarget = controller.GetViewScreenPosition(target.OrbId);
                Vector2 expected = (OrbGestureEngine.NormalizeClamped(displayedSource, controller.OrbGridScreenRect)
                    + OrbGestureEngine.NormalizeClamped(displayedTarget, controller.OrbGridScreenRect)) * .5f;
                controller.EndPointer(pointer, end);
                var reply = controller.Combination.LastResult;
                Assert.That(reply, Is.Not.Null);
                Assert.That(reply.known && reply.accepted, Is.True);
                Assert.That(reply.sourceOrbId, Is.EqualTo(source.OrbId));
                Assert.That(reply.targetOrbId, Is.EqualTo(target.OrbId));
                Assert.That(reply.originalCombined.id, Is.Not.EqualTo(source.OrbId).And.Not.EqualTo(target.OrbId));
                Assert.That(reply.originalCombined.kind, Is.EqualTo((int)OrbKind.Combined));
                Assert.That(reply.originalCombined.polarity, Is.EqualTo((int)OrbPolarity.None));
                Assert.That(Vector2.Distance(reply.originalCombined.pos, expected), Is.LessThan(.00001f));
                AssertConsumed(source.OrbId); AssertConsumed(target.OrbId);
                Assert.That(controller.Views.ContainsKey(source.OrbId) || controller.Views.ContainsKey(target.OrbId), Is.False);
                Assert.That(controller.Views.Count, Is.EqualTo(4));
                Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(6));
                Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
                Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
                Assert.That(controller.Combination.HasPending || controller.HasCombinationPending, Is.False);

                Assert.That(controller.Combination.Submit(ToRequest(reply)), Is.True);
                var duplicate = controller.Combination.LastResult;
                Assert.That(duplicate.known && duplicate.accepted && duplicate.duplicate, Is.True);
                Assert.That(duplicate.originalCombined.id, Is.EqualTo(reply.originalCombined.id));
                Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(6));
                Assert.That(controller.Views.Count, Is.EqualTo(4));
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            }
            Debug.Log("C6_T08_INTEGRATION mode=DEBUG_TEST_MODE_MIXED fixtureRaw=5 dropDirections=2 moveContactCombines=0 rawConsumedEach=2 newCombinedEach=1 duplicateNewIds=0 source=DEBUG_POINTER physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator NearbySamePolarityDropReachesHostAndPreservesBothRawAndPaidResources()
        {
            yield return StartHost(); yield return BeginMixedFixture();
            Assert.That(controller.Resource.RequestGenerate(), Is.True);
            Assert.That(controller.Resource.LastResult.accepted, Is.True);
            var yin = controller.Attack.Registry.Snapshot().Where(orb => orb.Polarity == OrbPolarity.Yin
                && orb.NormalizedPosition.y < .2f).OrderBy(orb => orb.NormalizedPosition.x).Take(2).ToArray();
            Assert.That(yin.Length, Is.EqualTo(2));
            var source = yin[0]; var target = yin[1];
            Vector2 start = controller.GetViewScreenPosition(source.OrbId);
            Vector2 targetCenter = controller.GetViewScreenPosition(target.OrbId);
            // Stop within the Drop radius before reaching the independent horizontal Swipe threshold.
            Vector2 drop = targetCenter - Vector2.right * Screen.width * .035f;
            Assert.That(Mathf.Abs(drop.x - start.x), Is.LessThan(Screen.width * controller.Layout.Config.HorizontalSwipeFraction));
            Assert.That(Vector2.Distance(drop, targetCenter), Is.LessThan(Screen.width * controller.Layout.Config.CombinationRadiusFraction));
            var before = controller.Attack.Registry.Snapshot(); double staminaBefore = controller.Resource.LocalPlayer.stamina;
            int generatedBefore = controller.Resource.LocalPlayer.generatedTotal;
            Assert.That(controller.BeginPointer(8120, start, false), Is.True);
            controller.MovePointer(8120, drop); controller.EndPointer(8120, drop);
            var reply = controller.Combination.LastResult;
            Assert.That(reply, Is.Not.Null, "Invalid nearest Raw Drop must reach Host validation for explicit feedback.");
            Assert.That(reply.known, Is.True); Assert.That(reply.accepted, Is.False);
            Assert.That(reply.reason, Is.EqualTo("INVALID_COMBINATION"));
            Assert.That(reply.targetOrbId, Is.EqualTo(target.OrbId), "A nearby opposite polarity cannot replace the user's nearest invalid target.");
            Assert.That(controller.Attack.Registry.Snapshot(), Is.EqualTo(before));
            Assert.That(controller.Views.ContainsKey(source.OrbId) && controller.Views.ContainsKey(target.OrbId), Is.True);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.GreaterThanOrEqualTo(staminaBefore - .000001d));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.EqualTo(generatedBefore));
            Assert.That(controller.Combination.HasPending || controller.HasCombinationPending || controller.Gestures.HasPending, Is.False);
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Debug.Log("C6_T08_INTEGRATION samePolarityDropRejected=true nearestTargetPreserved=true materialMutation=0 staminaDebit=0 source=DEBUG_POINTER physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator ActualDropResultLaunchesWithSameNewIdAndRealHitRecoversFiveOnce()
        {
            yield return StartHost(); yield return BeginMixedFixture();
            Assert.That(controller.Resource.RequestGenerate(), Is.True);
            Assert.That(controller.Resource.LastResult.accepted, Is.True);
            Assert.That(controller.Resource.LastResult.staminaBefore - controller.Resource.LastResult.staminaAfter, Is.EqualTo(20d).Within(1e-8));
            var recoveries = new List<ResourceRecoveryResult>();
            controller.Resource.RecoveryResolved += recoveries.Add;
            var pair = VerticalPair();
            Drop(pair[0], pair[1], 8130);
            var reply = controller.Combination.LastResult;
            Assert.That(reply != null && reply.known && reply.accepted, Is.True);
            string id = reply.originalCombined.id;
            Assert.That(recoveries, Is.Empty, "Combination itself grants no hit recovery.");
            yield return LaunchAndHit(id, 8131);
            Assert.That(recoveries.Count, Is.EqualTo(1));
            Assert.That(recoveries[0].Accepted && recoveries[0].Applied, Is.True);
            Assert.That(recoveries[0].PlayerId, Is.EqualTo(controller.Attack.LocalPlayerId));
            Assert.That(recoveries[0].Added, Is.EqualTo(5d).Within(1e-10));
            AssertConsumed(id);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(recoveries.Count, Is.EqualTo(1));
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(1));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.EqualTo(1));
            Debug.Log("C6_T08_INTEGRATION mode=DEBUG_TEST_MODE_MIXED paidGenerateCost=20 actualDropNewCombined=1 debugCombinedUsed=false realRigidbodyHit=1 hp=80 attackerRecovery=5 duplicateRecovery=0 source=DEBUG_POINTER physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator RawEnteringAttackZoneCannotLaunchOrGrantRecovery()
        {
            yield return StartHost(); yield return BeginMixedFixture();
            var source = VerticalPair()[0]; var original = controller.Attack.Registry.Snapshot();
            var recoveries = new List<ResourceRecoveryResult>(); controller.Resource.RecoveryResolved += recoveries.Add;
            Vector2 start = controller.GetViewScreenPosition(source.OrbId);
            Vector2 zone = new Vector2(start.x, controller.Layout.BottomPixelRect.yMax - 1f);
            Assert.That(controller.BeginPointer(8140, start, false), Is.True);
            controller.MovePointer(8140, zone);
            yield return new WaitForFixedUpdate();
            Assert.That(controller.Attack.Registry.Snapshot(), Is.EqualTo(original));
            Assert.That(controller.Views.ContainsKey(source.OrbId), Is.True);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(recoveries, Is.Empty);
            controller.CancelPointer(8140);
            Assert.That(controller.Gestures.HasPending || controller.Gestures.HasActivePointer, Is.False);
            Debug.Log("C6_T08_INTEGRATION rawZoneEntryProjectiles=0 rawPreserved=true hp=100 recovery=0 source=DEBUG_POINTER physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator RawReleaseOutsideLowerViewportCannotUseItsClampedViewAsCombinationDrop()
        {
            yield return StartHost(); yield return BeginMixedFixture();
            var source = VerticalPair()[0]; var before = controller.Attack.Registry.Snapshot();
            Vector2 start = controller.GetViewScreenPosition(source.OrbId);
            Vector2 outside = new Vector2(start.x, controller.Layout.BottomPixelRect.yMin - 20f);
            Assert.That(controller.BeginPointer(8145, start, false), Is.True);
            controller.MovePointer(8145, outside); controller.EndPointer(8145, outside);
            yield return null;
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(controller.Combination.HasPending || controller.HasCombinationPending || controller.Gestures.HasPending, Is.False);
            Assert.That(controller.Attack.Registry.Snapshot(), Is.EqualTo(before));
            Assert.That(controller.Views.Count, Is.EqualTo(5));
            Assert.That(controller.Views.ContainsKey(source.OrbId), Is.True);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Debug.Log("C6_T08_INTEGRATION rawOutsideViewportRelease=true clampedViewCombination=false materialMutation=0 source=DEBUG_POINTER physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator ExistingTargetReservationKeepsBothLocalMaterialsLockedUntilNormalReset()
        {
            yield return StartHost(); yield return BeginMixedFixture();
            var pair = VerticalPair();
            PrepareReservationRace(pair);
            uint oldRound = controller.Attack.Snapshot.roundId;
            Drop(pair[0], pair[1], 8150);
            var reply = controller.Combination.LastResult;
            Assert.That(reply != null && reply.known && !reply.accepted, Is.True);
            Assert.That(reply.reason, Is.EqualTo("OTHER_ORB_PENDING"));
            Assert.That(reply.sourcePending, Is.False); Assert.That(reply.targetPending, Is.True);
            Assert.That(controller.Combination.HasPending && controller.HasCombinationPending, Is.True);
            controller.CancelInteractions("T08 test pointer cancelled while Host target reservation remains");
            Assert.That(controller.BeginPointer(8151, controller.GetViewScreenPosition(pair[0].OrbId), false), Is.False);
            controller.CancelPointer(8151);
            Assert.That(controller.BeginPointer(8152, controller.GetViewScreenPosition(pair[1].OrbId), false), Is.False);
            controller.CancelPointer(8152);
            Assert.That(controller.Combination.QueryPending(ToRequest(reply)), Is.True);
            Assert.That(controller.Combination.HasPending && controller.HasCombinationPending, Is.True);
            Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(5));
            Assert.That(controller.Attack.Registry.Snapshot().All(orb => orb.AuthorityState == OrbAuthorityState.Idle), Is.True);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            controller.ResetDevelopmentRound();
            yield return WaitFor(() => controller.Combination.Connected && controller.Attack.Snapshot.roundId > oldRound,
                3f, "Normal reset did not bind a fresh combination context.");
            Assert.That(controller.Combination.HasPending || controller.HasCombinationPending || controller.Gestures.HasPending, Is.False);
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(controller.Combination.QueryPending(ToRequest(reply)), Is.False);
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            Debug.Log("C6_T08_INTEGRATION setup=TRANSFER_RESERVATION_ONLY targetPendingReject=true bothLocalMaterialsLocked=true cancelUnlocks=false queryUnlocks=false normalResetEmpty=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator DisablingCombinationWithUnresolvedReservationEndsHostAndReenableCannotResumeIt()
        {
            yield return StartHost(); yield return BeginMixedFixture();
            string oldSession = controller.Attack.Snapshot.sessionId;
            var pair = VerticalPair(); PrepareReservationRace(pair); Drop(pair[0], pair[1], 8160);
            Assert.That(controller.Combination.HasPending && controller.HasCombinationPending, Is.True);
            controller.Combination.enabled = false;
            yield return WaitFor(() => !controller.Attack.Connected && controller.Attack.Connection.CanStart
                && Object.FindObjectsByType<NetworkManager>().All(m => !m.IsListening && !m.ShutdownInProgress),
                5f, "Disabling combination must close this test-owned Host before releasing its receipt ledger.");
            Assert.That(controller.Combination.Connected || controller.Combination.HasPending || controller.HasCombinationPending, Is.False);
            Assert.That(controller.Combination.Authority, Is.Null);
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(controller.Views, Is.Empty);
            controller.Combination.enabled = true; yield return null; yield return null;
            Assert.That(controller.Attack.Connected || controller.Combination.Connected, Is.False);
            Assert.That(controller.Combination.Authority, Is.Null);
            yield return StartHost();
            Assert.That(controller.Attack.Snapshot.sessionId, Is.Not.EqualTo(oldSession));
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            Debug.Log("C6_T08_INTEGRATION pendingCombinationDisableEndsHost=true clearedReceiptContext=true reenableAutoStart=false explicitNewSessionEmpty=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator DisablingOnlyControllerWithBothMaterialsPendingClosesHostInsteadOfLosingTimeoutHandling()
        {
            yield return StartHost(); yield return BeginMixedFixture();
            var pair = VerticalPair(); PrepareReservationRace(pair); Drop(pair[0], pair[1], 8170);
            Assert.That(controller.Combination.HasPending && controller.HasCombinationPending, Is.True);
            controller.enabled = false;
            yield return WaitFor(() => !controller.Attack.Connected && controller.Attack.Connection.CanStart
                && Object.FindObjectsByType<NetworkManager>().All(m => !m.IsListening && !m.ShutdownInProgress),
                5f, "The disabled controller must close its unresolved request instead of silently abandoning query/timeout handling.");
            Assert.That(controller.Combination.HasPending || controller.HasCombinationPending, Is.False);
            Assert.That(controller.Combination.Authority, Is.Null);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Gestures.HasPending || controller.Gestures.HasActivePointer, Is.False);
            controller.enabled = true; yield return null; yield return null;
            Assert.That(controller.Attack.Connected || controller.Combination.Connected, Is.False);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Debug.Log("C6_T08_INTEGRATION controllerDisableWithPending=true closesHostBeforeTimeoutLoss=true bothMaterialLocksReleasedBySessionEnd=true reenableAutoStart=false physicalDevice=false");
        }

        private IEnumerator StartHost()
        {
            Assert.That(controller.StartDevelopmentHost(TestPort), Is.True);
            yield return WaitFor(() => controller.Attack.IsHost && controller.Resource.IsHost && controller.Resource.Connected
                && controller.Combination.IsHost && controller.Combination.Connected, 5f, "Actual NGO Host did not bind T08 services.");
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var manager = controller.GetComponent<DirectConnectionSession>().OwnedManager;
            Assert.That(manager.IsHost && manager.IsListening, Is.True);
        }
        private IEnumerator BeginMixedFixture()
        {
            Assert.That(controller.Resource.BeginMixedDebugFixtureRound(), Is.True);
            yield return WaitFor(() => controller.Views.Count == 5 && controller.Combination.Connected,
                3f, "Explicit mixed Raw fixture did not bind to T08.");
            yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var records = controller.Attack.Registry.Snapshot();
            Assert.That(records.Count, Is.EqualTo(5));
            Assert.That(records.All(orb => orb.Kind == OrbKind.Raw && orb.AuthorityState == OrbAuthorityState.Idle), Is.True);
            Assert.That(records.Count(orb => orb.Polarity == OrbPolarity.Yin), Is.EqualTo(3));
            Assert.That(records.Count(orb => orb.Polarity == OrbPolarity.Yang), Is.EqualTo(2));
            Assert.That(controller.Resource.DebugTestMode, Is.True);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
            Assert.That(controller.Combination.LastResult, Is.Null);
        }
        private OrbRecord[] VerticalPair() => controller.Attack.Registry.Snapshot()
            .Where(orb => orb.Kind == OrbKind.Raw && orb.NormalizedPosition.x < .2f)
            .OrderBy(orb => orb.NormalizedPosition.y).Take(2).ToArray();
        private void Drop(OrbRecord source, OrbRecord target, int pointer)
        {
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var start = controller.GetViewScreenPosition(source.OrbId); var end = controller.GetViewScreenPosition(target.OrbId);
            Assert.That(controller.BeginPointer(pointer, start, false), Is.True);
            controller.MovePointer(pointer, end); controller.EndPointer(pointer, end);
        }
        private void PrepareReservationRace(OrbRecord[] pair)
        {
            // Explicit test setup reserves only; this neither implements nor claims an actual T12 transfer.
            var reserve = controller.Attack.Registry.Reserve(controller.Attack.LocalPlayerId, new OrbActionRequest(
                controller.Attack.Snapshot.sessionId, controller.Attack.Snapshot.roundId, Guid.NewGuid().ToString("N"),
                pair[1].OrbId, null, OrbActionKind.TransferLeft, 1, pair[1].NormalizedPosition));
            Assert.That(reserve.Accepted, Is.True);
            Assert.That(controller.Attack.Registry.IsPending(pair[1].OrbId), Is.True);
        }
        private IEnumerator LaunchAndHit(string orbId, int pointer)
        {
            yield return WaitFor(() => controller.Views.ContainsKey(orbId), 3f, "Combined result view did not appear.");
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var start = controller.GetViewScreenPosition(orbId);
            var zone = new Vector2(start.x, controller.Layout.BottomPixelRect.yMax - 1f);
            Assert.That(controller.BeginPointer(pointer, start, false), Is.True);
            controller.MovePointer(pointer, zone);
            Assert.That(controller.Views.ContainsKey(orbId), Is.False);
            Assert.That(controller.Attack.Registry.TryGet(orbId, out var flying), Is.True);
            Assert.That(flying.AuthorityState, Is.EqualTo(OrbAuthorityState.Projectile));
            var projectile = Object.FindObjectsByType<HostProjectile3D>().Single();
            Assert.That(projectile.OrbId, Is.EqualTo(orbId));
            Assert.That(projectile.Body.isKinematic, Is.False);
            Assert.That(projectile.Body.linearVelocity.magnitude, Is.GreaterThan(0));
            controller.EndPointer(pointer, zone);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, 4f, "Actual Combined Rigidbody did not hit the target.");
            yield return null;
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
        }
        private void AssertConsumed(string id)
        {
            Assert.That(controller.Attack.Registry.TryGet(id, out var record), Is.True);
            Assert.That(record.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
        }
        private static CombinationRequest ToRequest(CombinationReply reply) => new CombinationRequest(reply.sessionId,
            reply.roundId, reply.requestId, reply.sourceOrbId, reply.targetOrbId, reply.sequence, reply.sourcePosition, reply.targetPosition);
        private static IEnumerator WaitFor(Func<bool> predicate, float seconds, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }
        private static AsyncOperation LoadScene()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        }
    }
}
