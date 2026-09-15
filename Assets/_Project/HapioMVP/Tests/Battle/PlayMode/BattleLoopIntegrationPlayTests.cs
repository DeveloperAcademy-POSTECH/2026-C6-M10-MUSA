using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Combination;
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

namespace C6.Prototype.Battle.Tests
{
    /// <summary>Saved scene, actual NGO/Drop/PhysX, real elapsed short durations; never physical-device or real180-second evidence.</summary>
    public sealed class BattleLoopIntegrationPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
        private const string Port = "25109";
        private T09BattleController controller;
        private bool previousRunInBackground;
        private int pointer;
        private string lastCombined;
        private CombinationReply lastCombination;

        [UnitySetUp]
        public IEnumerator LoadSceneWithoutImplicitHostSoloStartOrFreeOrbs()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return LoadScene(); yield return null; yield return null;
            controller = Object.FindAnyObjectByType<T09BattleController>(); pointer = 9100; lastCombined = null; lastCombination = null;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Battle, Is.Not.Null);
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Boot));
            Assert.That(controller.DevelopmentSolo, Is.False);
            Assert.That(controller.Attack.Connected || controller.Battle.Connected, Is.False);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<T08CombinationController>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<T07ResourceController>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<T06AttackController>(), Is.Empty);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator CloseOnlyThisTestConnectionAndRemoveSceneOwnedObjects()
        {
            try
            {
                if (controller != null) controller.EndDevelopmentTest();
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(m => !m.IsListening && !m.ShutdownInProgress), 5, "The T09 test-owned connection did not finish shutting down.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    var cleanup = SceneManager.CreateScene("T09 Integration Cleanup");
                    SceneManager.SetActiveScene(cleanup); yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null; yield return null;
                Assert.That(Object.FindObjectsByType<T09BattleController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<BattleSession>(), Is.Empty);
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
        public IEnumerator DefaultOneParticipantRemainsLobbyWithFrozenClockAndNoGeneration()
        {
            yield return Connect(false);
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Lobby));
            Assert.That(controller.Battle.Snapshot.participants, Is.EqualTo(1));
            Assert.That(controller.Battle.Snapshot.developmentSolo, Is.False);
            Assert.That(controller.Battle.CanStart || controller.RequestHostStart(), Is.False);
            Assert.That(controller.RequestGenerate() || controller.Resource.RequestGenerate(), Is.False);
            Assert.That(controller.Hud.StartButton.interactable || controller.Hud.GenerateButton.interactable, Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.Battle.Snapshot.remaining, Is.EqualTo(180));
            Assert.That(controller.Battle.Snapshot.teamHp, Is.EqualTo(180));
            Assert.That(controller.Battle.Authority.StartedAt, Is.Zero);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Debug.Log("C6_T09_INTEGRATION mode=NORMAL participants=1 startRejected=true phase=Lobby clockFrozen180=true generateBlocked=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator HeldCaptionStaysAboveTheFooterAndCancelRestoresItsArtwork()
        {
            yield return Connect(true); yield return StartBattle(); GeneratePaid(); yield return null;
            var view = controller.Views.Values.Single();
            var start = controller.GetViewScreenPosition(view.OrbId);
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, start, false), Is.True);
            controller.MovePointer(id, new Vector2(start.x, controller.Hud.OrbWorkspaceScreenRect.yMin));
            yield return null;
            var label = view.GetComponentInChildren<TextMesh>();
            var bounds = label.GetComponent<Renderer>().bounds;
            float bottom = controller.Layout.OrbCamera.WorldToScreenPoint(bounds.min).y;
            Assert.That(bottom, Is.GreaterThanOrEqualTo(controller.Hud.OrbWorkspaceScreenRect.yMin),
                "The enlarged held artwork caption must not disappear behind the footer.");
            Assert.That(view.HeldFeedbackActive, Is.True);
            controller.CancelPointer(id);
            Assert.That(view.HeldFeedbackActive, Is.False);
            Assert.That(view.HeldScale, Is.EqualTo(1f));
            Assert.That(controller.GetViewScreenPosition(view.OrbId).x, Is.EqualTo(start.x).Within(.01));
            Assert.That(controller.GetViewScreenPosition(view.OrbId).y, Is.EqualTo(start.y).Within(.01));
        }

        [UnityTest]
        public IEnumerator FormerAttackBandIsNowCombinationWorkspaceAndBoundaryLaunchRemainsAuthoritative()
        {
            yield return Connect(true); yield return StartBattle();
            Assert.That(controller.Hud.GetComponentsInChildren<Transform>(true)
                .Any(t => t.name == "AttackZoneBand" || t.name == "AttackZoneCaption"), Is.False);
            var grid = controller.OrbGridScreenRect;
            var oldBand = OrbGestureEngine.AttackZone(controller.Layout.BottomPixelRect, controller.Layout.Config.AttackZoneHeightFraction);
            Assert.That(grid.yMax, Is.GreaterThan(oldBand.yMin));
            ulong owner = controller.Attack.LocalPlayerId;
            var source = controller.Attack.Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, OrbPolarity.Yin, new Vector2(.1f, .9f));
            var target = controller.Attack.Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, OrbPolarity.Yang, new Vector2(.9f, .9f));
            controller.Attack.PublishInventoryChange("t09-former-band-raw-geometry-fixture");
            yield return null; yield return null;
            var from = controller.GetViewScreenPosition(source.OrbId);
            var to = controller.GetViewScreenPosition(target.OrbId);
            Assert.That(oldBand.Contains(to), Is.True, "Fixture must exercise the removed band, not the old grid.");
            var held = controller.Views[source.OrbId];
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, from, false), Is.True);
            Assert.That(held.HeldFeedbackActive, Is.True);
            Assert.That(held.HeldScale, Is.GreaterThan(1.2f));
            for (int step = 1; step <= 8; step++)
            {
                controller.MovePointer(id, Vector2.Lerp(from, to, step / 8f));
                yield return null;
                Assert.That(controller.Gestures.HasPending, Is.False);
                Assert.That(controller.Combination.LastResult, Is.Null);
            }
            controller.EndPointer(id, to);
            yield return WaitFor(() => controller.Combination.LastResult != null, 3, "Full-workspace drop was not resolved.");
            Assert.That(controller.Combination.LastResult.accepted, Is.True, controller.Combination.LastResult.reason);
            string combined = controller.Combination.LastResult.originalCombined.id;
            yield return WaitFor(() => controller.Views.ContainsKey(combined), 3, "Combined view missing.");
            Assert.That(controller.Views[combined].HeldFeedbackActive, Is.False);
            from = controller.GetViewScreenPosition(combined); id = ++pointer;
            Assert.That(controller.BeginPointer(id, from, false), Is.True);
            controller.MovePointer(id, new Vector2(from.x, controller.Layout.BottomPixelRect.yMax - 2));
            Assert.That(controller.Views.ContainsKey(combined), Is.True);
            Assert.That(controller.Gestures.HasPending, Is.False);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            var across = new Vector2(from.x, controller.Layout.BottomPixelRect.yMax + 2);
            controller.MovePointer(id, across);
            controller.MovePointer(id, across + Vector2.up * 5); controller.EndPointer(id, across);
            Assert.That(controller.Views.ContainsKey(combined), Is.False);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>().Count(p => p.OrbId == combined), Is.EqualTo(1));
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, 4, "Boundary projectile did not cause a physical hit.");
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(1));
            Assert.That(controller.Attack.Registry.TryGet(combined, out var record), Is.True);
            Assert.That(record.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Debug.Log("C6_T09_FREE_WORKSPACE setup=RAW_GEOMETRY_FIXTURE removedBandUi=true fullWidthDropInFormerBand=true heldScale=true boundaryLaunchOnce=true physicalHits=1 physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator ExplicitSoloRequiresHostStartAndRetryReturnsToFreshReadyBeforePaidGenerate()
        {
            yield return Connect(true);
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Ready));
            Assert.That(controller.Battle.Snapshot.developmentSolo, Is.True);
            Assert.That(controller.Battle.Snapshot.shortDuration, Is.False);
            Assert.That(controller.RequestGenerate(), Is.False);
            uint lobbyRound = controller.Battle.Snapshot.roundId;
            yield return StartBattle();
            Assert.That(controller.Battle.Snapshot.roundId, Is.GreaterThan(lobbyRound));
            Assert.That(controller.Views, Is.Empty); Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            GeneratePaid(); string firstId = controller.Resource.LastResult.confirmedOrb.id;
            uint playedRound = controller.Battle.Snapshot.roundId;
            Assert.That(controller.Battle.RetryHost(), Is.True);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Ready && controller.Battle.Snapshot.roundId > playedRound,
                3, "Host Retry did not return to a fresh Ready round.");
            AssertEmptyReady();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(controller.Battle.Snapshot.remaining, Is.EqualTo(180));
            Assert.That(controller.RequestGenerate(), Is.False);
            yield return StartBattle(); GeneratePaid();
            Assert.That(controller.Views.Count, Is.EqualTo(1));
            Assert.That(controller.Resource.LastResult.confirmedOrb.id, Is.Not.EqualTo(firstId));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.EqualTo(1));
            Debug.Log("C6_T09_INTEGRATION mode=DEV_SOLO readyRequiresHostStart=true startFreshRound=true retryReadyEmpty=true firstGenerateAfterRetryRaw=1 cost=20 physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator FiveActualCombinationHitsRewardBeforeVictoryAndRetryRejectsOldRequests()
        {
            yield return Connect(true); yield return StartBattle();
            for (int i = 0; i < 5; i++) GeneratePaid();
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.LessThan(1));
            double originalDeadline = controller.Battle.Authority.Deadline;
            Assert.That(controller.Resource.SupplyMixedDebugFixtureCurrentRound(), Is.True);
            Assert.That(controller.Battle.Authority.Deadline, Is.EqualTo(originalDeadline));
            var recoveries = new List<ResourceRecoveryResult>(); controller.Resource.RecoveryResolved += recoveries.Add;
            var launched = new HashSet<string>();
            for (int i = 0; i < 5; i++)
            {
                yield return CombineAvailablePair();
                Assert.That(launched.Add(lastCombined), Is.True);
                yield return LaunchAndAwaitHit(lastCombined, 100 - 20 * (i + 1));
                Assert.That(recoveries.Count, Is.EqualTo(i + 1));
                Assert.That(recoveries[i].Accepted && recoveries[i].Applied && !recoveries[i].IsDuplicate, Is.True);
                Assert.That(recoveries[i].Added, Is.EqualTo(5d).Within(1e-9));
                Assert.That(recoveries[i].PlayerId, Is.EqualTo(controller.Attack.LocalPlayerId));
            }
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Victory, 3, "The fifth actual killing hit did not produce Victory.");
            Assert.That(controller.Battle.Snapshot.observedMonsterHp, Is.Zero);
            Assert.That(controller.Battle.Snapshot.remaining, Is.GreaterThan(0));
            Assert.That(controller.Battle.Snapshot.teamHp, Is.GreaterThan(0));
            Assert.That(controller.Hud.ResultOverlay.activeSelf, Is.True);
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(5));
            Assert.That(controller.Battle.CanAct || controller.RequestGenerate() || controller.Resource.RequestGenerate(), Is.False);
            Assert.That(controller.Resource.Snapshot.playing, Is.False);
            Assert.That(controller.Resource.SupplyMixedDebugFixtureCurrentRound(), Is.False);
            double frozenTime = controller.Battle.Snapshot.remaining; double frozenStamina = controller.Resource.LocalPlayer.stamina;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.Battle.Snapshot.remaining, Is.EqualTo(frozenTime));
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(frozenStamina).Within(1e-9));
            Assert.That(recoveries.Count, Is.EqualTo(5));
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);

            var oldCombination = ToRequest(lastCombination);
            var oldLaunch = new OrbActionRequest(oldCombination.SessionId, oldCombination.RoundId, Guid.NewGuid().ToString("N"),
                lastCombined, null, OrbActionKind.Launch, 2, Vector2.one * .5f);
            uint oldRound = controller.Battle.Snapshot.roundId;
            Assert.That(controller.Battle.RetryHost(), Is.True);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Ready && controller.Battle.Snapshot.roundId > oldRound,
                3, "Result Retry did not create a fresh Ready round.");
            AssertEmptyReady(); Assert.That(controller.Hud.ResultOverlay.activeSelf, Is.False);
            yield return StartBattle();
            Assert.That(controller.Combination.Submit(oldCombination), Is.False);
            bool staleReply = false;
            Action<AttackRequestReply> observeStale = reply => { if (reply.requestId == oldLaunch.RequestId) staleReply = true; };
            controller.Attack.RequestResolved += observeStale;
            // The existing service admits a well-formed envelope, then drops an old context at its Host guard.
            Assert.That(controller.Attack.Submit(oldLaunch), Is.True);
            controller.Attack.RequestResolved -= observeStale;
            Assert.That(staleReply, Is.False, "Old context must not be rewrapped as a current-round approval.");
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            GeneratePaid(); Assert.That(controller.Views.Count, Is.EqualTo(1));
            Debug.Log("C6_T09_INTEGRATION mode=DEV_SOLO_MIXED_RAW_SUPPLY forceDamage=false actualDropHits=5 hitRecoveryEach=5 killingHitRecoveryBeforeVictory=true victoryClockFrozen=true retryEmptyFull=true oldRoundRequestsRejected=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator RealTwoSecondExpiryCancelsInFlightCombinedBeforeLateHitOrRecovery()
        {
            yield return Connect(true, 2); yield return StartBattle(); GeneratePaid();
            Assert.That(controller.Resource.SupplyMixedDebugFixtureCurrentRound(), Is.True);
            yield return CombineAvailablePair(); string id = lastCombined;
            var recoveries = new List<ResourceRecoveryResult>(); controller.Resource.RecoveryResolved += recoveries.Add;
            double startedAt = controller.Battle.Authority.StartedAt; double deadline = controller.Battle.Authority.Deadline;
            yield return WaitFor(() => Time.realtimeSinceStartupAsDouble >= deadline - .15, 3,
                "The actual short battle did not approach its deadline.");
            Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "A stalled frame missed the explicit late-flight setup window.");
            Launch(id);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>().Any(p => p.OrbId == id), Is.True);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Defeat, 3, "Actual short elapsed time did not resolve Defeat.");
            Assert.That(Time.realtimeSinceStartupAsDouble - startedAt, Is.GreaterThanOrEqualTo(2));
            Assert.That(controller.Battle.Snapshot.shortDuration, Is.True);
            Assert.That(controller.Battle.Snapshot.duration, Is.EqualTo(2));
            Assert.That(controller.Battle.Snapshot.remaining, Is.Zero); Assert.That(controller.Battle.Snapshot.teamHp, Is.Zero);
            Assert.That(controller.Battle.Snapshot.observedMonsterHp, Is.EqualTo(100));
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(controller.Attack.Snapshot.totalHits, Is.Zero);
            Assert.That(controller.Resource.Snapshot.playing, Is.False);
            Assert.That(recoveries, Is.Empty);
            double frozen = controller.Resource.LocalPlayer.stamina;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(controller.Attack.Registry.TryGet(id, out var consumed), Is.True);
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(frozen).Within(1e-9));
            Assert.That(recoveries, Is.Empty);
            Assert.That(controller.Hud.ResultOverlay.activeSelf, Is.True);
            Debug.Log("C6_T09_INTEGRATION mode=DEV_SOLO_SHORT_DURATION durationSeconds=2 actualElapsed=true lateFlightCancelledByDefeat=true postDeadlineHits=0 postResultRecovery=0 real180Seconds=false physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator ConfirmedDefeatClearsUnresolvedMaterialRequestsAndAllowsResultRetry()
        {
            yield return Connect(true, 2); yield return StartBattle();
            Assert.That(controller.Resource.SupplyMixedDebugFixtureCurrentRound(), Is.True);
            CreatePendingReservationRace();
            Assert.That(controller.Combination.HasPending && controller.HasCombinationPending, Is.True);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Defeat, 3, "Short clock did not end the pending request round.");
            Assert.That(controller.Battle.Connected, Is.True);
            Assert.That(controller.Resource.HasPending || controller.Combination.HasPending || controller.HasCombinationPending, Is.False);
            Assert.That(controller.Gestures.HasPending || controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Defeat), "A confirmed result must not become an unconfirmed-request timeout error.");
            uint oldRound = controller.Battle.Snapshot.roundId;
            Assert.That(controller.Battle.RetryHost(), Is.True, "Result cleanup must release request locks before Retry.");
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Ready && controller.Battle.Snapshot.roundId > oldRound,
                3, "Result Retry did not bind a fresh context.");
            AssertEmptyReady(2);
            Debug.Log("C6_T09_INTEGRATION setup=TRANSFER_RESERVATION_ONLY durationSeconds=2 actualDefeatClearsPending=true resultDoesNotWaitFor8SecondError=true retryReadyEmpty=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator DisablingBattleServiceEndsItsConnectionAndCannotResumeOldPlayingRound()
        {
            yield return Connect(true); yield return StartBattle(); GeneratePaid();
            string oldSession = controller.Attack.Snapshot.sessionId;
            controller.Battle.enabled = false;
            yield return WaitForStopped();
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Battle.Authority, Is.Null);
            Assert.That(controller.Resource.HasPending || controller.Combination.HasPending, Is.False);
            controller.Battle.enabled = true; yield return null; yield return null;
            Assert.That(controller.Attack.Connected || controller.Battle.Connected, Is.False);
            yield return Connect(true);
            Assert.That(controller.Attack.Snapshot.sessionId, Is.Not.EqualTo(oldSession));
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Ready));
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Views, Is.Empty);
            Debug.Log("C6_T09_INTEGRATION battleServiceDisableEndsHost=true oldPlayingRoundNotResumed=true explicitReconnectReadyEmpty=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator DisablingControllerWithPendingMaterialsCannotOrphanConfirmationHandling()
        {
            yield return Connect(true); yield return StartBattle();
            Assert.That(controller.Resource.SupplyMixedDebugFixtureCurrentRound(), Is.True);
            CreatePendingReservationRace();
            Assert.That(controller.Combination.HasPending && controller.HasCombinationPending, Is.True);
            controller.enabled = false;
            yield return WaitForStopped();
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.Resource.HasPending || controller.Combination.HasPending || controller.HasCombinationPending, Is.False);
            Assert.That(controller.Gestures.HasPending || controller.Gestures.HasActivePointer, Is.False);
            controller.enabled = true; yield return null; yield return null;
            Assert.That(controller.Battle.Connected || controller.Attack.Connected, Is.False);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Debug.Log("C6_T09_INTEGRATION controllerDisableWithPending=true hostEndedBeforeConfirmationHandlingLost=true noAutomaticReconnect=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator SimulatedPauseKeepsResourceFrozenWhileAbsoluteShortBattleClockStillExpires()
        {
            yield return Connect(true, 2); yield return StartBattle(); GeneratePaid();
            controller.Resource.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            controller.Battle.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            double frozen = controller.Resource.LocalPlayer.stamina;
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Defeat, 3,
                "Battle time must continue across the simulated pause callback.");
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(frozen).Within(1e-9));
            controller.Resource.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            controller.Battle.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Defeat));
            Assert.That(controller.Battle.Snapshot.remaining, Is.Zero);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(frozen).Within(1e-9));
            Assert.That(controller.RequestGenerate(), Is.False);
            Debug.Log("C6_T09_INTEGRATION source=SIMULATED_PAUSE_CALLBACKS durationSeconds=2 battleAbsoluteTimeExpired=true pausedResourceRecovery=0 resultResumeRecovery=0 actualOSSuspension=false physicalDevice=false");
        }

        private IEnumerator Connect(bool solo, double? duration = null)
        {
            controller.ConfigureDevelopmentSolo(solo); controller.ConfigureDevelopmentDuration(duration);
            Assert.That(controller.StartDevelopmentHost(Port), Is.True);
            yield return WaitFor(() => controller.Battle.IsHost && controller.Resource.IsHost && controller.Combination.IsHost,
                5, "Actual NGO Host did not bind all T09 services.");
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
        }
        private IEnumerator StartBattle()
        {
            Assert.That(controller.RequestHostStart(), Is.True);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Playing && controller.Battle.CanAct
                && controller.Resource.Connected && controller.Combination.Connected, 3, "Explicit Host Start did not enter Playing.");
            yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
        }
        private void GeneratePaid()
        {
            Assert.That(controller.RequestGenerate(), Is.True);
            var reply = controller.Resource.LastResult;
            Assert.That(reply != null && reply.known && reply.accepted, Is.True);
            Assert.That(reply.staminaBefore - reply.staminaAfter, Is.EqualTo(20d).Within(1e-8));
            Assert.That(reply.confirmedOrb.kind, Is.EqualTo((int)OrbKind.Raw));
        }
        private OrbRecord[] FindDroppablePair()
        {
            var own = controller.Attack.Registry.Snapshot().Where(o => o.OwnerPlayerId == controller.Attack.LocalPlayerId
                && o.AuthorityState == OrbAuthorityState.Idle && controller.Views.ContainsKey(o.OrbId)).ToArray();
            return (from a in own from b in own
                where a.Kind == OrbKind.Raw && b.Kind == OrbKind.Raw && a.OrbId != b.OrbId && a.Polarity != b.Polarity
                let sourcePoint = controller.GetViewScreenPosition(a.OrbId)
                let target = controller.GetViewScreenPosition(b.OrbId)
                let delta = target - sourcePoint
                let drop = target - delta.normalized * Mathf.Min(Screen.width * .06f, delta.magnitude * .25f)
                let movement = drop - sourcePoint
                where Mathf.Abs(movement.x) < Screen.width * controller.Layout.Config.HorizontalSwipeFraction
                    || Mathf.Abs(movement.x) < Mathf.Abs(movement.y) * controller.Layout.Config.HorizontalDominance
                let nearest = own.Where(o => o.OrbId != a.OrbId).OrderBy(o => (controller.GetViewScreenPosition(o.OrbId) - drop).sqrMagnitude)
                    .ThenBy(o => o.OrbId, StringComparer.Ordinal).First()
                where nearest.OrbId == b.OrbId
                orderby delta.sqrMagnitude select new[] { a, b }).FirstOrDefault();
        }
        private IEnumerator CombineAvailablePair()
        {
            var pair = FindDroppablePair();
            if (pair == null)
            {
                Assert.That(controller.Resource.SupplyMixedDebugFixtureCurrentRound(), Is.True,
                    "Explicit mixed Raw supply must remain within storage capacity.");
                yield return null; pair = FindDroppablePair();
            }
            Assert.That(pair, Is.Not.Null, "Explicit mixed preparation did not provide a reachable opposite pair.");
            Drop(pair);
            yield return WaitFor(() => controller.Combination.LastResult != null && !controller.Combination.HasPending
                && !controller.HasCombinationPending, 3, "Host did not confirm the actual controller Drop.");
            var reply = controller.Combination.LastResult;
            Assert.That(reply.known && reply.accepted, Is.True, reply.reason);
            lastCombination = reply; lastCombined = reply.originalCombined.id;
            Assert.That(lastCombined, Is.Not.EqualTo(pair[0].OrbId).And.Not.EqualTo(pair[1].OrbId));
            Assert.That(reply.currentSource.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            Assert.That(reply.currentTarget.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            yield return WaitFor(() => controller.Views.ContainsKey(lastCombined), 3, "Confirmed Combined view did not appear.");
        }
        private void Drop(OrbRecord[] pair)
        {
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var from = controller.GetViewScreenPosition(pair[0].OrbId); var target = controller.GetViewScreenPosition(pair[1].OrbId);
            var delta = target - from;
            var drop = target - delta.normalized * Mathf.Min(Screen.width * .06f, delta.magnitude * .25f);
            string previous = controller.Combination.LastResult?.requestId;
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, from, false), Is.True);
            controller.MovePointer(id, drop);
            Assert.That(controller.Combination.LastResult?.requestId, Is.EqualTo(previous), "Contact cannot automatically combine.");
            controller.EndPointer(id, drop);
        }
        private void CreatePendingReservationRace()
        {
            var pair = FindDroppablePair(); Assert.That(pair, Is.Not.Null);
            var held = controller.Attack.Registry.Reserve(controller.Attack.LocalPlayerId, new OrbActionRequest(
                controller.Attack.Snapshot.sessionId, controller.Attack.Snapshot.roundId, Guid.NewGuid().ToString("N"),
                pair[1].OrbId, null, OrbActionKind.TransferLeft, 1, pair[1].NormalizedPosition));
            Assert.That(held.Accepted, Is.True, "Explicit test setup reserves only; it does not implement a transfer.");
            Drop(pair);
            Assert.That(controller.Combination.LastResult.reason, Is.EqualTo("OTHER_ORB_PENDING"));
            Assert.That(controller.Combination.LastResult.targetPending, Is.True);
        }
        private void Launch(string orbId)
        {
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var from = controller.GetViewScreenPosition(orbId);
            var to = new Vector2(from.x, controller.Layout.BottomPixelRect.yMax + 2);
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, from, false), Is.True);
            controller.MovePointer(id, to); controller.EndPointer(id, to);
            Assert.That(controller.Views.ContainsKey(orbId), Is.False);
            var projectile = Object.FindObjectsByType<HostProjectile3D>().Single(p => p.OrbId == orbId);
            Assert.That(projectile.Body.isKinematic, Is.False);
            Assert.That(projectile.Body.linearVelocity.magnitude, Is.GreaterThan(0));
        }
        private IEnumerator LaunchAndAwaitHit(string orbId, int hp)
        {
            Launch(orbId);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == hp, 4, "Actual Rigidbody did not hit the target.");
            yield return null;
            Assert.That(controller.Attack.Registry.TryGet(orbId, out var consumed), Is.True);
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
        }
        private void AssertEmptyReady(double duration = 180)
        {
            Assert.That(controller.Battle.Phase, Is.EqualTo(BattlePhase.Ready));
            Assert.That(controller.Battle.Snapshot.remaining, Is.EqualTo(duration));
            Assert.That(controller.Battle.Snapshot.observedMonsterHp, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
            Assert.That(controller.Resource.DebugTestMode, Is.False);
            Assert.That(controller.Resource.HasPending || controller.Combination.HasPending || controller.HasCombinationPending, Is.False);
            Assert.That(controller.Gestures.HasPending || controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty); Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
        }
        private IEnumerator WaitForStopped() => WaitFor(() => !controller.Attack.Connected && !controller.Battle.Connected
            && controller.Attack.Connection.CanStart && Object.FindObjectsByType<NetworkManager>().All(m => !m.IsListening && !m.ShutdownInProgress),
            5, "The disabled component left its T09 connection running.");
        private static CombinationRequest ToRequest(CombinationReply reply) => new CombinationRequest(reply.sessionId,
            reply.roundId, reply.requestId, reply.sourceOrbId, reply.targetOrbId, reply.sequence, reply.sourcePosition, reply.targetPosition);
        private static IEnumerator WaitFor(Func<bool> condition, double seconds, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
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
