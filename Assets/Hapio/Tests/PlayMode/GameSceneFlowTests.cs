using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// Saved game Scenes and saved persistent-session prefab integration. Terminal victories
    /// use an explicit CPU-map fixture completion, not simulated projectile collision: throw
    /// and contact correctness remain the dedicated Lab projectile suites' responsibility.
    /// </summary>
    public sealed class GameSceneFlowTests
    {
        private GameSession session;
        private LabNetwork network;

        [UnitySetUp]
        public IEnumerator LoadSavedHome()
        {
            Assert.That(GameSession.Instance, Is.Null, "The previous fixture must release its persistent game session.");
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null, "Saved Home must bootstrap its saved session prefab.");
            session = GameSession.Instance;
            network = session.Network;
            yield return Until(() => !session.IsLoadingScene, "Home bootstrap must finish loading.");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            Assert.That(network, Is.Not.Null);
            Assert.That(network.GameFlowEnabled, Is.True);
            Assert.That(network.IsConnected, Is.False);
            network.port = AvailableDualFamilyPort();
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 600f);
            Set(values, "Player Max HP", 237f);
            Set(values, "Starting Stamina", 65f);
            Set(values, "Stamina Recovery Amount", 0f);
            Set(values, "Orb Generation Cost", 20f);
            Set(values, "Yin Chance (%)", 100f);
            Set(values, "Yang Chance (%)", 0f);
            Set(values, "Relay Orb Chance (%)", 0f);
            Set(values, "Secret Box Chance (%)", 0f);
            Set(values, "Potion Chance (%)", 0f);
            Set(values, "Bomb Chance (%)", 0f);
            Set(values, "Paint Surface Resolution (0 low 1 medium 2 high)", 0f);
            Set(values, "Gourd Mode (0 paint, 1 crack)", 0f);
            Set(values, "Paint Initial Coverage (%)", 100f);
            Set(values, "Paint Pattern", 0f);
            Set(values, "Paint Drying (0 off, 1 on)", 0f);
            Set(values, "Completed Gourd Viewer (0 off, 1 on)", 1f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedSession()
        {
            Time.timeScale = 1f;
            if (session != null)
            {
                network.Stop();
                Object.Destroy(session.gameObject);
                yield return null;
                Assert.That(GameSession.Instance, Is.Null);
            }
            Scene current = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Hapio game fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (current.IsValid() && current.isLoaded) yield return SceneManager.UnloadSceneAsync(current);
            session = null;
            network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingGameCardsSurviveBattleGameOverAndHomeWhileLobbyLeaveReturnsToTheBrowser()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Game pending rooms require macOS/iOS Bonjour.");
            Assert.That(session.RoomBrowserVisible, Is.False, "A cold Home starts on its title page.");
            session.EnterRoomBrowser();
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(session.CreatePendingRoom(2), Is.True, session.UiNotice);
            Assert.That(session.CreatePendingRoom(3), Is.True, session.UiNotice);
            yield return Until(() => session.PendingRooms.Count == 2 && session.PendingRooms.All(room => room.Ready),
                "The persistent Game session must finish both native room registrations.");
            var cards = session.PendingRooms.ToArray();
            Assert.That(cards.All(room => room.Name.StartsWith(LabRoomNames.GameServiceBase, StringComparison.Ordinal)), Is.True);
            string[] keys = cards.Select(room => room.Key).ToArray();
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;

            session.StartSolo();
            yield return WaitPlaying(1);
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys),
                "Unloading Home must not dispose another waiting room's advertisement.");
            CompleteMapInFixture(Model());
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult,
                "A real committed fixture victory must finish loading its result Scene.");
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys));
            Assert.That(session.PendingRooms.All(room => room.Ready), Is.True,
                "Waiting-room advertisements remain owned by the session during results.");
            session.ExitRun();
            yield return WaitGameOver();
            Assert.That(session.PendingRooms.All(room => room.Ready), Is.True);
            session.GoHome();
            yield return WaitHome();
            yield return Until(() => network.CanChooseConnection, "The completed shutdown must release the active transport.");
            Assert.That(session.RoomBrowserVisible, Is.False, "GameOver returns to the title, not an automatic room entry.");
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys));

            session.EnterRoomBrowser();
            Assert.That(session.EnterPendingRoom(keys[0]), Is.True, session.UiNotice);
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene,
                "Entering the creator's card must use the existing Game transport and open its lobby.");
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.Network, Is.SameAs(persistentNetwork));
            Assert.That(network.GameFlowEnabled, Is.True);
            Assert.That(network.IsHost, Is.True);
            Assert.That(network.TargetParticipantCount, Is.EqualTo(2));
            Assert.That(network.LocalSlotNumber, Is.Zero, "Entering one's room does not reserve P1 for the Host.");
            Assert.That(network.ConnectedRoomName, Is.EqualTo(cards[0].Name));
            Assert.That(network.port, Is.EqualTo(cards[0].Port));
            Assert.That(session.PendingRooms.Select(room => room.Key), Is.EqualTo(keys.Skip(1)));
            session.GoHomeToRoomBrowser();
            yield return WaitHome();
            Assert.That(session.RoomBrowserVisible, Is.True, "Leaving a pre-battle lobby returns to its room browser.");
            Assert.That(session.PendingRooms.Single().Key, Is.EqualTo(keys[1]));
            Assert.That(session.PendingRooms.Single().Ready, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedResultPreviewUsesOneNormalizedGestureHandlerAndClearsHeldDragsOnResetAndReopen()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            CompleteMapInFixture(Model());
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult, "The committed result must load its saved UI.");
            GameScreen screen = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
            screen.viewerButton.onClick.Invoke();
            yield return null;
            Assert.That(screen.viewerPanel.activeInHierarchy, Is.True);
            GameObject image = screen.viewerImage.gameObject;
            GamePreviewInput adapter = image.GetComponent<GamePreviewInput>();
            Assert.That(adapter, Is.Not.Null, "Keep the saved Scene's adapter reference.");
            Assert.That(image.GetComponents<MonoBehaviour>().OfType<IDragHandler>().Count(), Is.EqualTo(1),
                "The adapter and the shared surface must not dispatch the same drag twice.");
            Assert.That(image.GetComponents<GourdViewerGestureSurface>(), Has.Length.EqualTo(1));
            var preview = (LabGourdResultPreview)typeof(GameSession).GetField("preview",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            Quaternion initial = preview.ModelRotation;
            string paintHash = preview.PaintPixelHash;
            Color32[] paintPixels = preview.PaintTexture.GetPixels32();
            string result = JsonUtility.ToJson(session.ResultSnapshot);
            RectTransform rectangle = screen.viewerImage.rectTransform;
            Canvas.ForceUpdateCanvases();
            Vector2 start = PreviewScreenPoint(rectangle, .4f, .5f);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 12345, position = start };
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.pointerDownHandler);
            pointer.position = PreviewScreenPoint(rectangle, .41f, .5f);
            pointer.delta = pointer.position - start; // Pixel movement exceeds 1px on the saved layout.
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.dragHandler);
            Quaternion expected = Quaternion.AngleAxis(-1.8f, Vector3.up) * initial;
            Assert.That(Quaternion.Angle(expected, preview.ModelRotation), Is.LessThan(.01f),
                "One percent of the preview width rotates 1.8 degrees, not a clamped 180 degrees.");
            screen.resetViewerButton.onClick.Invoke();
            Assert.That(Quaternion.Angle(initial, preview.ModelRotation), Is.LessThan(.001f));
            pointer.position = PreviewScreenPoint(rectangle, .6f, .5f);
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.dragHandler);
            Assert.That(Quaternion.Angle(initial, preview.ModelRotation), Is.LessThan(.001f),
                "Reset must discard a held pointer's old baseline.");
            screen.closeViewerButton.onClick.Invoke();
            yield return null;
            Assert.That(screen.viewerPanel.activeSelf, Is.False);
            screen.viewerButton.onClick.Invoke();
            yield return null;
            ExecuteEvents.Execute(image, pointer, ExecuteEvents.dragHandler);
            Assert.That(Quaternion.Angle(initial, preview.ModelRotation), Is.LessThan(.001f),
                "Reopening must not adopt an old drag without a fresh pointer down.");
            Assert.That(image.GetComponents<GourdViewerGestureSurface>(), Has.Length.EqualTo(1));
            Assert.That(preview.PaintPixelHash, Is.EqualTo(paintHash));
            Assert.That(preview.PaintTexture.GetPixels32(), Is.EqualTo(paintPixels));
            Assert.That(JsonUtility.ToJson(session.ResultSnapshot), Is.EqualTo(result),
                "Local gesture input must not alter the committed gameplay result.");
        }

        private static Vector2 PreviewScreenPoint(RectTransform rectangle, float x, float y)
        {
            Rect local = rectangle.rect;
            return RectTransformUtility.WorldToScreenPoint(null, rectangle.TransformPoint(new Vector3(
                local.xMin + local.width * x, local.yMin + local.height * y, 0f)));
        }

        [UnityTest]
        public IEnumerator SavedHomeRunsThreeFreshBattlesWithOneRoomAndKeepsCompletedMapAfterUnload()
        {
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;
            string previousBattleId = null;
            LabOrbBoard previousBoard = null;
            session.StartSolo();

            for (int stage = 1; stage <= 3; stage++)
            {
                yield return WaitPlaying(stage);
                Assert.That(GameSession.Instance, Is.SameAs(persistent));
                Assert.That(session.Network, Is.SameAs(persistentNetwork));
                Assert.That(network.IsConnected, Is.True);
                Assert.That(network.IsDeveloperSolo, Is.True);
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(previousBattleId));
                Assert.That(network.board.gameObject.scene.path, Is.EqualTo(GameScenePaths.Battle));
                Assert.That(network.target.gameObject.scene.path, Is.EqualTo(GameScenePaths.Battle));
                if (stage > 1) Assert.That(previousBoard == null, Is.True, "The previous Battle board must be unloaded.");
                AssertFreshStage();
                Assert.That(Model().StageYokai, Is.EqualTo((LabStageYokai)stage),
                    "The shared Battle scene must create the selected stage's Host policy, not the legacy Lab mix.");
                Assert.That(session.CurrentStageDefinition.stageIndex, Is.EqualTo(stage));
                if (stage == 1)
                {
                    var staleRoot = new GameObject("Stale copied Lab lobby fixture");
                    staleRoot.SetActive(false);
                    var staleLobby = staleRoot.AddComponent<LabLobbyView>();
                    staleLobby.hud = network.board.gameObject.scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
                    staleRoot.SetActive(true);
                    Assert.That(staleRoot.activeSelf, Is.False,
                        "Even a stale copied standalone view must refuse game-flow activation before touching widgets or advertising rooms.");
                    Object.Destroy(staleRoot);
                }


                var jigwiHud = SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabJigwiHud>(true)).Single();
                yield return null; // The saved HUD evaluates the real stage policy in LateUpdate.
                LabHud battleHud = SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
                LabOrbHudComposition composition = battleHud.GetComponent<LabOrbHudComposition>();
                Assert.That(composition, Is.Not.Null, "Every shared Battle instance must attach the visual-only compositor.");
                Assert.That(composition.IsCompositing, Is.True, composition.InitializationError);
                Assert.That(battleHud.orbCamera.targetTexture, Is.Null,
                    "The saved camera remains the on-screen physics/input projection across all three stages.");
                Assert.That(composition.Display.transform.parent, Is.SameAs(battleHud.battleUi.transform));
                Assert.That(composition.Display.raycastTarget, Is.False);
                Assert.That(composition.ForegroundCamera.cullingMask & (1 << network.board.gameObject.layer), Is.Not.Zero);
                Assert.That(network.JigwiManualControlsAllowed, Is.EqualTo(stage == 3));
                Assert.That(jigwiHud.testToggle.gameObject.activeInHierarchy, Is.EqualTo(stage == 3),
                    "The standalone Lab manual panel must not appear in Gumiho or Yaguang game stages.");
                if (stage == 3)
                {
                    session.Pause();
                    yield return null;
                    Assert.That(jigwiHud.testToggle.gameObject.activeInHierarchy, Is.True,
                        "Pausing Jigwi keeps the developer entry visible, as in the standalone Lab.");
                    Assert.That(jigwiHud.fireButton.interactable, Is.False);
                    Assert.That(jigwiHud.generationButton.interactable, Is.False);
                    session.Resume();
                    yield return null;
                }

                LabOrbBoard thisBoard = network.board;
                network.Generate();
                yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1,
                    "The real network Generate path must commit one generated orb.");
                LabModel model = Model();
                Assert.That(model.TryApplyPlayerDamage(network.LocalPlayerId, 13, ModelTime(model)), Is.True);
                Assert.That(model.CopyPlayers().Single().personalHp, Is.EqualTo(224));
                Assert.That(model.CopyPlayers().Single().stamina, Is.EqualTo(45f).Within(.01f));
                Assert.That(model.CopyOwnedStates(network.LocalPlayerId).Any(orb => !orb.kind.IsDrum()), Is.True);

                string runId = session.State.runId;
                previousBattleId = network.Snapshot.sessionId;
                CompleteMapInFixture(model);
                GameFlowPhase resultPhase = stage == 3 ? GameFlowPhase.Completed : GameFlowPhase.StageResult;
                yield return Until(() => session.State.phase == resultPhase && !session.IsLoadingScene
                    && SceneManager.GetActiveScene().path == GameScenePaths.StageResult,
                    "The actual committed victory must load StageResult.");
                Assert.That(session.State.runId, Is.EqualTo(runId));
                Assert.That(session.State.stageIndex, Is.EqualTo(stage));
                Assert.That(network.HasBattleReferences, Is.False);
                Assert.That(thisBoard == null, Is.True);
                Assert.That(session.ResultSnapshot, Is.Not.Null);
                Assert.That(session.ResultSnapshot.sessionId, Is.EqualTo(previousBattleId));
                Assert.That(session.ResultSnapshot.paintCoveredCount, Is.EqualTo(session.ResultSnapshot.paintCellCount));
                Assert.That(session.ResultSnapshot.players.Single().generatedTotal, Is.EqualTo(1));
                Assert.That(session.PreviewTexture, Is.Not.Null, "The completed paint preview must survive Battle unload.");
                previousBoard = thisBoard;
                if (stage < 3)
                {
                    yield return Until(() => session.CanAdvanceStage,
                        "Host must wait for every copied result and loaded result Scene before NEXT.");
                    yield return null;
                    session.NextStage();
                    session.NextStage(); // A repeated button must not skip the new loading generation.
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage + 1));
                }
            }

            yield return Until(() => session.CanReturnHome,
                "Final Host exit must wait for every result acknowledgement.");
            yield return null;
            session.ExitRun(); // Normal final completion exits without creating GameOver.
            yield return WaitHome();
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.State.endReason, Is.Empty);
            Assert.That(session.ResultSnapshot, Is.Null);
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(network.IsConnected, Is.False);
            Assert.That(session.RoomBrowserVisible, Is.False, "Final clear returns to the title page.");
        }

        [UnityTest]
        public IEnumerator MidStageExitLatchesGameOverAndWaitsForExplicitHome()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            session.ExitRun();
            yield return WaitGameOver();
            string reason = session.State.endReason;
            uint revision = session.State.revision;
            Assert.That(reason, Does.Contain("left"));
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.GameOver));
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            Assert.That(session.State.endReason, Is.EqualTo(reason));
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(network.HasBattleReferences, Is.False);
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator AuthoritativeTimeoutLoadsGameOverInsteadOfNextStage()
        {
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 1f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            session.StartSolo();
            yield return WaitPlaying(1);
            // Let the actual Network clock reach the one-second configured deadline. Never
            // advance the model ahead of the Network clock or inject a terminal snapshot.
            yield return WaitGameOver();
            Assert.That(session.State.endReason, Is.EqualTo("Time ran out."));
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            session.NextStage();
            yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator AuthoritativeAllPlayersDownUsesGameOverRatherThanStageResult()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            LabModel model = Model();
            Assert.That(model.TryApplyPlayerDamage(network.LocalPlayerId, network.config.PlayerMaxHp,
                ModelTime(model)), Is.True);
            Assert.That(model.Phase, Is.EqualTo(LabPhase.Defeat));
            yield return WaitGameOver();
            Assert.That(session.State.endReason, Is.EqualTo("All players are down."));
            Assert.That(session.ResultSnapshot, Is.Null);
            session.GoHome();
            yield return WaitHome();
        }

        [UnityTest]
        public IEnumerator PausedDeveloperApplyReloadsOnlyCurrentStageAndRestoresFreshResources()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            network.Generate();
            yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1, "Generate must commit.");
            LabOrbBoard oldBoard = network.board;
            string runId = session.State.runId;
            string battleId = session.State.battleSessionId;
            session.Pause();
            Assert.That(network.IsBattlePaused, Is.True);
            double pausedRemaining = network.Snapshot.remaining;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(network.Snapshot.remaining, Is.EqualTo(pausedRemaining).Within(.001d),
                "A loaded game Scene must retain the shared pause clock.");
            session.Resume();
            Assert.That(network.IsBattlePaused, Is.False);
            yield return Until(() => network.Snapshot.remaining < pausedRemaining - .01d,
                "Resume must restart the existing stage clock without loading a new battle.");
            Assert.That(session.State.battleSessionId, Is.EqualTo(battleId));
            session.Pause();
            Assert.That(network.IsBattlePaused, Is.True);
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Player Max HP", 219f);
            Set(values, "Starting Stamina", 73f);
            Set(values, "Sabotage Mix (0 off, 1 on)", 0f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing
                && session.State.battleSessionId != battleId && !session.IsLoadingScene,
                "Applying DEV settings must pass through a fresh Battle load barrier.");
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(session.State.runId, Is.EqualTo(runId));
            Assert.That(oldBoard == null, Is.True);
            Assert.That(network.IsBattlePaused, Is.False);
            AssertFreshStage();
            Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(219));
            Assert.That(network.Snapshot.players.Single().stamina, Is.EqualTo(73f).Within(.01f));
            Assert.That(network.config.SabotageMixEnabled, Is.False,
                "Restart must retain the user's OFF rather than reapply the stage's initial ON default.");
        }

        [UnityTest]
        public IEnumerator PausedDeveloperNextStageUsesSavedButtonAndFreshBattlesWithoutApplyingUnsavedFields()
        {
            Assert.That(session.CanDeveloperAdvanceStage, Is.False);
            Assert.That(session.DeveloperNextStage(), Is.False, "Home has no stage to skip.");
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;
            session.StartSolo();
            yield return WaitPlaying(1);
            string runId = session.State.runId;
            ulong playerId = network.LocalPlayerId;
            int slot = network.LocalSlotNumber;
            ulong[] participants = network.ConnectedPlayerIds.ToArray();
            var loadedPaths = new System.Collections.Generic.List<string>();
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> recordScene = (scene, _) => loadedPaths.Add(scene.path);
            SceneManager.sceneLoaded += recordScene;
            try
            {
                for (int stage = 1; stage <= 3; stage++)
                {
                    Assert.That(GameSession.Instance, Is.SameAs(persistent));
                    Assert.That(session.Network, Is.SameAs(persistentNetwork));
                    Assert.That(session.State.runId, Is.EqualTo(runId));
                    Assert.That(network.LocalPlayerId, Is.EqualTo(playerId));
                    Assert.That(network.LocalSlotNumber, Is.EqualTo(slot));
                    Assert.That(network.ConnectedPlayerIds, Is.EqualTo(participants));
                    Assert.That(Model().StageYokai, Is.EqualTo((LabStageYokai)stage));
                    AssertFreshStage();
                    Assert.That(network.IsBattlePaused, Is.False);
                    Assert.That(session.CanDeveloperAdvanceStage, Is.False, "DEV skip requires an explicitly paused battle.");
                    Assert.That(session.DeveloperNextStage(), Is.False);
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage));

                    network.Generate();
                    yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1,
                        "Leave real generated resources in the old Battle to verify next-stage cleanup.");
                    LabModel model = Model();
                    Assert.That(model.TryApplyPlayerDamage(playerId, 13, ModelTime(model)), Is.True);
                    LabOrbBoard oldBoard = network.board;
                    string oldBattle = session.State.battleSessionId;
                    session.Pause();
                    Assert.That(network.IsBattlePaused, Is.True);
                    yield return null;
                    GameBattleBindings bindings = SceneManager.GetActiveScene().GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<GameBattleBindings>(true)).Single();
                    LabDeveloperMode dev = bindings.developerMode;
                    Assert.That(bindings.developerNextStageButton, Is.Not.Null, "Use the saved editable DEV control.");
                    Assert.That(bindings.developerNextStageButton.transform.IsChildOf(dev.panel.transform), Is.True);
                    dev.Open();
                    yield return null;
                    Assert.That(dev.panel.activeInHierarchy, Is.True);
                    Assert.That(bindings.developerNextStageButton.gameObject.activeInHierarchy, Is.True);
                    Assert.That(bindings.developerNextStageButton.interactable, Is.EqualTo(stage < 3));
                    float[][] acceptedProfiles = Enumerable.Range(0, 4)
                        .Select(scope => session.CaptureDeveloperSettings(scope)).ToArray();
                    int hpField = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                        .Single(i => LabConfig.DeveloperFields[i].Label == "Player Max HP");
                    dev.valueInputs[hpField].text = "299"; // Deliberately do not APPLY or restart.
                    Assert.That(network.config.PlayerMaxHp, Is.EqualTo(237));
                    Assert.That(session.CanDeveloperAdvanceStage, Is.EqualTo(stage < 3));
                    uint revision = session.State.revision;
                    if (stage == 3)
                    {
                        Assert.That(session.DeveloperNextStage(), Is.False, "The last stage cannot create a stage 4 or fake completion.");
                        yield return null;
                        Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing));
                        Assert.That(session.State.stageIndex, Is.EqualTo(3));
                        Assert.That(session.State.revision, Is.EqualTo(revision));
                        Assert.That(session.State.battleSessionId, Is.EqualTo(oldBattle));
                        Assert.That(network.IsBattlePaused, Is.True);
                        Assert.That(session.ResultSnapshot, Is.Null);
                        Assert.That(session.PreviewTexture, Is.Null);
                        continue;
                    }

                    bindings.developerNextStageButton.onClick.Invoke();
                    Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
                    Assert.That(session.State.stageIndex, Is.EqualTo(stage + 1));
                    Assert.That(session.IsLoadingScene, Is.True);
                    Assert.That(session.CanDeveloperAdvanceStage, Is.False);
                    uint loadingRevision = session.State.revision;
                    Assert.That(loadingRevision, Is.GreaterThan(revision));
                    Assert.That(session.DeveloperNextStage(), Is.False, "Duplicate input cannot skip the new loading generation.");
                    Assert.That(session.State.revision, Is.EqualTo(loadingRevision));
                    yield return WaitPlaying(stage + 1);
                    Assert.That(session.State.battleSessionId, Is.Not.EqualTo(oldBattle));
                    Assert.That(oldBoard == null, Is.True, "The previous Battle instance must be unloaded.");
                    AssertDeveloperSettings(acceptedProfiles[stage + 1]);
                    for (int scope = 0; scope <= 3; scope++)
                        Assert.That(session.CaptureDeveloperSettings(scope), Is.EqualTo(acceptedProfiles[scope]),
                            "Skipping must retain approved profiles and discard unapplied form text.");
                    Assert.That(loadedPaths, Does.Not.Contain(GameScenePaths.StageResult),
                        "Skipping must not produce a completed paint preview or a victory result.");
                }
            }
            finally { SceneManager.sceneLoaded -= recordScene; }
        }

        [UnityTest]
        public IEnumerator DeveloperNextStageCannotOvertakeAQueuedRestartOrLeaveGameOver()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            string battle = session.State.battleSessionId;
            session.Pause();
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Player Max HP", 219f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            Assert.That(session.CanDeveloperAdvanceStage, Is.False,
                "An accepted current-stage restart already owns the transition.");
            Assert.That(session.DeveloperNextStage(), Is.False);
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            yield return Until(() => session.State.phase == GameFlowPhase.Playing
                && !session.IsLoadingScene && session.State.battleSessionId != battle,
                "The already accepted DEV restart must finish on stage 1.");
            Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(219));
            session.ExitRun();
            yield return WaitGameOver();
            uint revision = session.State.revision;
            Assert.That(session.CanDeveloperAdvanceStage, Is.False);
            Assert.That(session.DeveloperNextStage(), Is.False);
            yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(session.ResultSnapshot, Is.Null);
        }

        [UnityTest]
        public IEnumerator ForeignStageDeveloperEditIsRejectedBeforeTheCurrentBattleEnds()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            session.Pause();
            string battle = session.State.battleSessionId;
            float[] beforeCommon = session.CaptureDeveloperSettings(0);
            float[] beforeStage = session.CaptureDeveloperSettings(1);
            LabConfig candidate = Object.Instantiate(network.config);
            try
            {
                float[] forged = candidate.CaptureDeveloperValues();
                forged[184] = forged[184] > .5f ? 0f : 1f; // Yaguang-only setting from the Gumiho Battle DEV form.
                Assert.That(candidate.TryApplyDeveloperValues(forged, out string error), Is.True, error);
                Assert.That(network.RestartGameSoloWithConfig(candidate), Is.False);
                Assert.That(network.Snapshot, Is.Not.Null, "Reject before EndBattleToLobby clears the active model.");
                Assert.That(network.IsBattlePaused, Is.True);
                Assert.That(session.State.battleSessionId, Is.EqualTo(battle));
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing));
                Assert.That(session.CaptureDeveloperSettings(0), Is.EqualTo(beforeCommon));
                Assert.That(session.CaptureDeveloperSettings(1), Is.EqualTo(beforeStage));
                yield return null;
                Assert.That(session.State.battleSessionId, Is.EqualTo(battle));
            }
            finally { Object.Destroy(candidate); }
        }

        [UnityTest]
        public IEnumerator StageControlNamesTheCatalogIdentityAndExactSettingsGeneration()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo make = typeof(GameSession).GetMethod("MakeControl", flags);
            MethodInfo valid = typeof(GameSession).GetMethod("ValidStateControl", flags);
            object control = make.Invoke(session, new object[] { "state", session.State });
            Assert.That(valid.Invoke(session, new[] { control }), Is.EqualTo(true));
            Type type = control.GetType();
            FieldInfo id = type.GetField("stageId");
            string expectedId = (string)id.GetValue(control);
            id.SetValue(control, "a-different-stage");
            Assert.That(valid.Invoke(session, new[] { control }), Is.EqualTo(false));
            id.SetValue(control, expectedId);
            type.GetField("yokai").SetValue(control, LabStageYokai.Jigwi);
            Assert.That(valid.Invoke(session, new[] { control }), Is.EqualTo(false));

            control = make.Invoke(session, new object[] { "loaded", null });
            MethodInfo matches = typeof(GameSession).GetMethod("MatchesControlIdentity", flags);
            Assert.That(matches.Invoke(session, new[] { control, (object)false }), Is.EqualTo(true));
            control.GetType().GetField("settingsFingerprint").SetValue(control, "previous-stage-profile");
            Assert.That(matches.Invoke(session, new[] { control, (object)false }), Is.EqualTo(false),
                "A Scene-ready message cannot acknowledge a different effective stage configuration.");
        }

        [UnityTest]
        public IEnumerator SabotageSettingsSurviveRestartAndNextStageWhileFoxAndCharmStateReset()
        {
            GameSession persistent = session;
            LabNetwork persistentNetwork = network;
            float[] foxSettings = network.config.CaptureDeveloperValues();
            Set(foxSettings, "Drop Combine (0 off, 1 on)", 1f);
            Set(foxSettings, "Sabotage Mix (0 off, 1 on)", 1f);
            Set(foxSettings, "Sabotage Interval Min (seconds)", 2f);
            Set(foxSettings, "Sabotage Interval Max (seconds)", 2f);
            Set(foxSettings, "Sabotage Weight Fox Fire", 3f);
            Set(foxSettings, "Sabotage Weight Charm", 0f);
            Set(foxSettings, "Fox Fire Count Min", 2f);
            Set(foxSettings, "Fox Fire Count Max", 2f);
            Set(foxSettings, "Fox Fire Safe Seconds", .25f);
            Set(foxSettings, "Charm Seconds", 9f);
            Set(foxSettings, "Charm Rescue Taps (0 = none)", 5f);
            Assert.That(session.ApplyDeveloperSettings(foxSettings, out string error), Is.True, error);
            session.StartSolo();
            yield return WaitPlaying(1);
            AssertDeveloperSettings(foxSettings);

            LabOrbBoard foxBoard = network.board;
            yield return Until(() => network.Snapshot.orbs.Any(orb => orb.kind == LabOrbKind.FoxFire),
                "The persisted Home settings must cast real Fox Fire in the saved Battle.");
            LabOrbState[] foxes = network.Snapshot.orbs.Where(orb => orb.kind == LabOrbKind.FoxFire).ToArray();
            Assert.That(foxes.Length, Is.EqualTo(2), "The new shared Fox Fire count must reach the live model.");
            yield return Until(() => foxes.All(fox => foxBoard.IsFoxFire(fox.id)),
                "The saved board must receive the real Fox Fire drop.");
            LabOrbView foxView = foxBoard.GetComponentsInChildren<LabOrbView>()
                .Single(view => view.OrbId == foxes[0].id);
            Assert.That(foxView.FoxGlowVisible, Is.True);
            Assert.That(foxView.GetComponentInChildren<LabOrbSpriteArt>(), Is.Not.Null);
            yield return Until(() => !foxBoard.IsDropping(foxes[0].id), "The fox must land before it can consume Yin.");
            yield return new WaitForSeconds(network.config.FoxFireSafeSeconds + .1f);

            network.Generate();
            yield return Until(() => network.Snapshot.orbs.Any(orb => orb.kind == LabOrbKind.Yin),
                "The actual Generate path must create a Yin for Fox Fire contact.");
            string yinId = network.Snapshot.orbs.Single(orb => orb.kind == LabOrbKind.Yin).id;
            yield return Until(() => foxBoard.GetComponentsInChildren<LabOrbView>().Any(view => view.OrbId == yinId),
                "The generated Yin must reach the saved board.");
            LabOrbView yinView = foxBoard.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == yinId);
            // Controlled pointer positions exercise the actual board -> network -> Host request path.
            // This is a drop-overlap fixture, not a claim about uncontrolled collision accuracy.
            double gestureAt = Time.timeAsDouble;
            Vector2 foxPoint = new Vector2(foxView.Body.position.x, foxView.Body.position.y);
            Assert.That(foxBoard.TryBeginDragAtWorld(new Vector2(yinView.Body.position.x, yinView.Body.position.y),
                gestureAt), Is.True);
            Assert.That(foxBoard.DragToWorld(foxPoint, gestureAt + .1d), Is.True);
            Assert.That(foxBoard.EndDragAtWorld(foxPoint, gestureAt + .5d), Is.True);
            yield return Until(() => network.Snapshot.orbs.All(orb => orb.id != foxes[0].id && orb.id != yinId),
                "Fox contact must consume both real model orbs; hiding local previews is insufficient.");
            Assert.That(network.Snapshot.players.Single().passedTotal, Is.Zero);
            Assert.That(foxBoard.IsFoxFire(foxes[1].id), Is.True,
                "An unconsumed Fox remains to exercise cleanup during the same-stage reload.");

            string runId = session.State.runId;
            string firstBattleId = session.State.battleSessionId;
            session.Pause();
            Assert.That(network.IsBattlePaused, Is.True);
            float[] charmSettings = network.config.CaptureDeveloperValues();
            Set(charmSettings, "Sabotage Interval Min (seconds)", 3f);
            Set(charmSettings, "Sabotage Interval Max (seconds)", 3f);
            Set(charmSettings, "Sabotage Weight Fox Fire", 0f);
            Set(charmSettings, "Sabotage Weight Charm", 4f);
            Set(charmSettings, "Fox Fire Count Min", 3f);
            Set(charmSettings, "Fox Fire Count Max", 4f);
            Set(charmSettings, "Fox Fire Safe Seconds", .75f);
            Set(charmSettings, "Charm Seconds", 11f);
            Set(charmSettings, "Charm Rescue Taps (0 = none)", 6f);
            Assert.That(session.ApplyDeveloperSettings(charmSettings, out error), Is.True, error);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing
                && session.State.battleSessionId != firstBattleId && !session.IsLoadingScene,
                "The new Sabotage settings must restart the current stage through a fresh saved Battle.");
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.Network, Is.SameAs(persistentNetwork));
            Assert.That(session.State.runId, Is.EqualTo(runId));
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(foxBoard == null, Is.True);
            Assert.That(foxView == null && yinView == null, Is.True);
            AssertDeveloperSettings(charmSettings);
            AssertFreshStage();
            AssertFreshSabotagePresentation();

            LabOrbBoard charmBoard = network.board;
            LabCharmHud charmHud = Object.FindAnyObjectByType<LabCharmHud>();
            LabGourdAlert charmAlert = Object.FindAnyObjectByType<LabGourdAlert>();
            yield return Until(() => charmBoard.Charmed && charmHud.GaugeVisible && charmAlert.ShowingHeart,
                "The retained settings must cast real Charm and reach all saved Battle presentations.");
            Assert.That(network.Snapshot.CharmOn(network.LocalPlayerId), Is.True);
            Assert.That(network.Snapshot.charmUntil - network.Snapshot.charmStartedAt, Is.EqualTo(11d).Within(.001d));
            Assert.That(charmBoard.CharmEdge.Visible, Is.True);
            Assert.That(charmHud.GaugeFill, Is.InRange(.01f, 1f));
            string charmBattleId = session.State.battleSessionId;
            CompleteMapInFixture(Model());
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult
                && !session.IsLoadingScene && session.CanAdvanceStage,
                "A real committed victory must reach the next-stage barrier while Charm is active.");
            Assert.That(charmBoard == null && charmHud == null && charmAlert == null, Is.True);
            session.NextStage();
            yield return WaitPlaying(2);
            Assert.That(GameSession.Instance, Is.SameAs(persistent));
            Assert.That(session.Network, Is.SameAs(persistentNetwork));
            Assert.That(session.State.runId, Is.EqualTo(runId));
            Assert.That(session.State.battleSessionId, Is.Not.EqualTo(charmBattleId));
            AssertDeveloperSettings(session.CaptureDeveloperSettings(2));
            Assert.That(Model().StageYokai, Is.EqualTo(LabStageYokai.Yaguang));
            Assert.That(session.CaptureDeveloperSettings(1), Is.EqualTo(charmSettings),
                "The next yokai uses its own profile without losing the completed stage's edits.");
            AssertFreshStage();
            AssertFreshSabotagePresentation();
        }

        private void AssertDeveloperSettings(float[] expected)
        {
            float[] actual = network.config.CaptureDeveloperValues();
            Assert.That(actual.Length, Is.EqualTo(200), "The persistent game session must use the current shared schema.");
            Assert.That(actual, Is.EqualTo(expected),
                "All approved values, including the twelve Sabotage fields, must survive Scene-owned DEV cleanup.");
        }

        private void AssertFreshSabotagePresentation()
        {
            LabSnapshot snapshot = network.Snapshot;
            Assert.That(snapshot.charmStartedAt, Is.LessThan(0d));
            Assert.That(snapshot.charmUntil, Is.LessThanOrEqualTo(snapshot.serverTime));
            Assert.That(snapshot.charmTaps, Is.Zero);
            Assert.That(snapshot.CharmOn(network.LocalPlayerId), Is.False);
            Assert.That(network.board.Charmed, Is.False);
            Assert.That(network.board.CharmEdge == null || !network.board.CharmEdge.Visible, Is.True);
            Assert.That(network.board.GetComponentsInChildren<LabOrbView>()
                .Any(view => view.Kind == LabOrbKind.FoxFire), Is.False);
            LabCharmHud hud = Object.FindAnyObjectByType<LabCharmHud>();
            LabCharmBubbles bubbles = Object.FindAnyObjectByType<LabCharmBubbles>();
            LabGourdAlert alert = Object.FindAnyObjectByType<LabGourdAlert>();
            Assert.That(hud, Is.Not.Null, "The saved Battle must bind its own Charm HUD to the persistent network.");
            Assert.That(bubbles, Is.Not.Null);
            Assert.That(alert, Is.Not.Null);
            Assert.That(hud.GaugeVisible || hud.AwakeShowing, Is.False);
            Assert.That(bubbles.LeftShowing || bubbles.RightShowing, Is.False);
            Assert.That(alert.Active, Is.False);
        }

        private void AssertFreshStage()
        {
            LabSnapshot snapshot = network.Snapshot;
            LabPlayerState player = snapshot.players.Single();
            Assert.That(snapshot.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(snapshot.orbs, Is.Empty);
            Assert.That(snapshot.drums.Length, Is.EqualTo(network.config.DrumsFor(1)));
            Assert.That(snapshot.drums.All(drum => drum.owner == network.LocalPlayerId), Is.True);
            Assert.That(player.personalHp, Is.EqualTo(network.config.PlayerMaxHp));
            Assert.That(player.stamina, Is.EqualTo(network.config.StaminaStart).Within(.01f));
            Assert.That(snapshot.remaining, Is.GreaterThan(network.config.BattleDurationSeconds - 10f));
            Assert.That(snapshot.paintCoveredCount, Is.Zero);
            Assert.That(snapshot.paintCount, Is.Zero);
            Assert.That(network.PaintSurface.CoveredCount, Is.Zero);
            Assert.That(player.generatedTotal, Is.Zero);
            Assert.That(player.combinedTotal, Is.Zero);
            Assert.That(player.passedTotal, Is.Zero);
            Assert.That(player.drumPassedTotal, Is.Zero);
            Assert.That(player.hitTotal, Is.Zero);
            Assert.That(player.damageTotal, Is.Zero);
            Assert.That(player.ItemsUsed, Is.Zero);
            Assert.That(player.lastHit, Is.False);
            Assert.That(session.ResultSnapshot, Is.Null);
            Assert.That(session.PreviewTexture, Is.Null);
        }

        private static void CompleteMapInFixture(LabModel model)
        {
            Assert.That(model.PaintSurface.RegisterHit(Vector3.forward, model.PaintColorFor(LabOrbElement.Fire),
                model.CopyPlayers().Single().id, 1), Is.True);
            Assert.That(model.PaintSurface.IsComplete, Is.True,
                "LEGACY 100% fixture coverage must actually paint every cell, not forge a victory snapshot.");
        }

        private LabModel Model() => (LabModel)typeof(LabNetwork)
            .GetField("model", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(network);
        private static double ModelTime(LabModel model) => (double)typeof(LabModel)
            .GetField("lastTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);

        private IEnumerator WaitPlaying(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Battle stage " + stage + " must start.");
        private IEnumerator WaitGameOver() => Until(() => session.State.phase == GameFlowPhase.GameOver
            && !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.GameOver,
            "GameOver must load and remain available for explicit Home navigation.");
        private IEnumerator WaitHome() => Until(() => session.State.phase == GameFlowPhase.Home
            && !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.Home,
            "Home navigation must finish loading.");

        private static IEnumerator Until(Func<bool> predicate, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private static void Set(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(i => LabConfig.DeveloperFields[i].Label == label);
            values[index] = value;
        }

        private static ushort AvailableDualFamilyPort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            {
                                ipv6.DualMode = false;
                                ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                            }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("The test could not reserve a dual-family UDP Host port.");
        }
    }
}
