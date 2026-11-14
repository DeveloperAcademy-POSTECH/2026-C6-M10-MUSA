using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>Saved Home and real staff button bindings. Round/network-peer completion is tested separately.</summary>
    [Category("Slow")]
    public sealed class GameStaffStageDemoSessionTests
    {
        private IDisposable variant;
        private GameSession session;
        private GameStaffDemoView selector;

        [UnitySetUp]
        public IEnumerator OpenSavedStaffHome()
        {
            Assert.That(GameSession.Instance, Is.Null, "The previous fixture must release its persistent session.");
            variant = LabGameBuildVariant.OverrideStaffStageDemoForTests(true);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene,
                "Saved Home bootstraps the staff session");
            session = GameSession.Instance;
            yield return null; // The saved lobby and selector reflect the newly bound session.
            selector = Object.FindFirstObjectByType<GameStaffDemoView>();
            Assert.That(selector, Is.Not.Null, "The staff build must instantiate its saved selector, without pressing Start.");
        }

        [UnityTearDown]
        public IEnumerator CloseOwnedStaffHome()
        {
            // This fixture never replaces the user's scene or saves an asset. Release only its loaded scene/session.
            GameSession owned = session != null ? session : GameSession.Instance;
            if (owned != null)
            {
                owned.CancelDemoEntry();
                owned.Network.Stop();
                Object.Destroy(owned.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Staff demo session fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null;
            selector = null;
            variant?.Dispose();
            variant = null;
        }

        [UnityTest]
        public IEnumerator SavedHomeShowsThreeStaffBoxesInsteadOfTheNormalTitleAndRoomBrowser()
        {
            var lobby = Object.FindFirstObjectByType<GameLobbyView>();
            Assert.That(lobby, Is.Not.Null);
            Assert.That(selector.gameObject.activeInHierarchy, Is.True);
            Assert.That(selector.stageButtons, Has.Length.EqualTo(3));
            Assert.That(selector.stageButtons.Distinct().Count(), Is.EqualTo(3));
            Canvas.ForceUpdateCanvases();
            Assert.That(EventSystem.current, Is.Not.Null, "The saved Home supplies the selector's EventSystem.");
            for (int index = 0; index < selector.stageButtons.Length; index++)
            {
                var button = selector.stageButtons[index];
                Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
                Assert.That(button.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(text => text.text),
                    Has.Member((index + 1) + "스테이지"));
                var rect = (RectTransform)button.transform;
                Assert.That(rect.rect.width > 0f && rect.rect.height > 0f, Is.True);
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                Assert.That(center.x, Is.InRange(0f, (float)Screen.width));
                Assert.That(center.y, Is.InRange(0f, (float)Screen.height));
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = center }, hits);
                Assert.That(hits, Is.Not.Empty, "The saved box must be clickable, not merely visible.");
                Assert.That(hits[0].gameObject, Is.SameAs(button.gameObject), "No hidden title or full-screen graphic may cover a stage box.");
            }
            Assert.That(selector.backButton.gameObject.activeInHierarchy, Is.False, "Cancel is only shown during an entry attempt.");
            Assert.That(lobby.titlePage.activeInHierarchy, Is.False);
            Assert.That(lobby.browserPage.activeInHierarchy, Is.False);
            Assert.That(lobby.positionPage.activeInHierarchy, Is.False);
            Assert.That(lobby.soloButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(lobby.teamSoloButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(GameOpeningController.IsPresenting, Is.False);
            Assert.That(session.SelectedDemoStage, Is.Zero);
            Assert.That(session.Network.DemoStageIndex, Is.Zero);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(3));
            Assert.That(session.Network.IsConnected || session.Network.IsJoiningRoom, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidStagesSoloTutorialAndOtherRoomModesDoNotOpenConnections()
        {
            foreach (int stage in new[] { -1, 0, 4 })
                Assert.That(session.SelectDemoStage(stage), Is.False);
            foreach (int capacity in new[] { 2, 4, 5 })
            {
                Assert.That(session.CreateAndEnterRoom(capacity), Is.False);
                Assert.That(session.CreatePendingRoom(capacity), Is.False);
            }
            Assert.That(session.CreateAndEnterRoom(3, GameMode.Territory), Is.False);
            session.StartSolo();
            session.StartSolo(GameMode.Territory);
            Assert.That(GameSession.TutorialPending, Is.False);
            Assert.That(session.TryStartTutorial(), Is.False);
            Assert.That(session.TryStartTutorial(again: true), Is.False);
            session.StartGame();
            Assert.That(session.IsTutorial || session.IsDemoEntryPending || session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.Network.IsConnected || session.Network.IsJoiningRoom || session.Network.IsDeveloperSolo, Is.False);
            Assert.That(session.SelectedDemoStage, Is.Zero);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            yield return null;
        }

        [UnityTest, Category("Bonjour")]
        public IEnumerator RealMousePressAndReleaseOnStageTwoStartsItsEntryThroughTheSavedInputModule()
        {
            InputSettings original = InputSystem.settings;
            HideFlags originalFlags = original.hideFlags;
            string originalJson = JsonUtility.ToJson(original);
            Mouse previousMouse = Mouse.current;
            InputSettings fixture = null;
            Mouse mouse = null;
            try
            {
                // Synthetic pointers must survive a batch Editor's unfocused GameView without
                // changing the saved InputSettings asset or retaining a device after this test.
                original.hideFlags = HideFlags.DontUnloadUnusedAsset;
                fixture = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(originalJson, fixture);
                fixture.hideFlags = HideFlags.HideAndDontSave;
                fixture.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                fixture.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                fixture.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = fixture;
                mouse = InputSystem.AddDevice<Mouse>("Staff stage selection mouse");
                mouse.MakeCurrent();
                Assert.That(EventSystem.current, Is.Not.Null);
                Assert.That(EventSystem.current.currentInputModule,
                    Is.InstanceOf<InputSystemUIInputModule>(), "The real saved UI module must consume the pointer edges.");
                Assert.That(EventSystem.current.currentInputModule.isActiveAndEnabled, Is.True);
                EventSystem.current.SetSelectedGameObject(null);
                Canvas.ForceUpdateCanvases();
                var button = selector.stageButtons[1];
                var rect = (RectTransform)button.transform;
                Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
                Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
                Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True);
                Assert.That(session.SelectedDemoStage, Is.Zero);

                // Leave both edges for normal Dynamic Update + EventSystem frames. Neither UnityEvents
                // nor pointer handlers nor InputSystemUIInputModule.Process() are invoked by the test.
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                yield return null; yield return null;
                Assert.That(Mouse.current, Is.SameAs(mouse));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                Assert.That(mouse.leftButton.isPressed, Is.True, "Focus must not reset the held test pointer.");
                Assert.That(session.IsDemoEntryPending, Is.False, "Press alone must not start the stage request.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                yield return null; yield return null;
                Assert.That(Mouse.current, Is.SameAs(mouse));
                Assert.That(mouse.leftButton.isPressed, Is.False);
                Assert.That(session.IsDemoEntryPending, Is.True, session.DemoEntryNotice);
                Assert.That(session.SelectedDemoStage, Is.EqualTo(2));
                Assert.That(session.Network.DemoStageIndex, Is.EqualTo(2));
                Assert.That(session.Network.IsConnected || session.Network.IsJoiningRoom, Is.False,
                    "This test cancels the search before a Host or Join is started.");
                session.CancelDemoEntry();
                Assert.That(session.IsDemoEntryPending, Is.False);
                Assert.That(session.PendingRooms, Is.Empty);
            }
            finally
            {
                try
                {
                    session?.CancelDemoEntry();
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                    if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                }
                finally
                {
                    try { InputSystem.settings = original; }
                    finally
                    {
                        original.hideFlags = originalFlags;
                        if (fixture != null) Object.DestroyImmediate(fixture);
                    }
                }
            }
            Assert.That(InputSystem.settings, Is.SameAs(original));
            Assert.That(original.hideFlags, Is.EqualTo(originalFlags));
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(originalJson));
            Assert.That(mouse.added, Is.False);
        }

        [UnityTest, Category("Bonjour")]
        public IEnumerator SavedStageButtonsLockDuplicateRequestsCancelAndAllowAnotherStageWithoutOpeningAHost()
        {
            // Exercise the real browse and button binding, but cancel before the two-second absence window.
            // No Host socket, advertisement, or fake multiplayer result is created by this fixture.
            for (int stage = 1; stage <= 3; stage++)
            {
                selector.stageButtons[stage - 1].onClick.Invoke();
                Assert.That(session.IsDemoEntryPending, Is.True, session.DemoEntryNotice);
                Assert.That(session.SelectedDemoStage, Is.EqualTo(stage));
                Assert.That(session.Network.DemoStageIndex, Is.EqualTo(stage));
                // Refresh within this frame: a stalled rendering frame must not let the 2 s search
                // deadline accidentally create a real Host before this button-cancellation scenario.
                selector.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                Assert.That(selector.stageButtons.All(button => !button.interactable), Is.True);
                Assert.That(selector.backButton.gameObject.activeInHierarchy, Is.True);
                selector.stageButtons[stage % 3].onClick.Invoke();
                Assert.That(session.SelectedDemoStage, Is.EqualTo(stage), "A second tap cannot switch a pending room request.");
                selector.backButton.onClick.Invoke();
                yield return null;
                Assert.That(session.IsDemoEntryPending || session.IsCreatingRoom, Is.False);
                Assert.That(session.PendingRooms, Is.Empty);
                Assert.That(session.Network.IsConnected || session.Network.IsJoiningRoom || session.Network.IsSearchingRooms, Is.False);
                Assert.That(selector.stageButtons.All(button => button.interactable), Is.True);
                Assert.That(selector.backButton.gameObject.activeInHierarchy, Is.False);
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            }

            session.ReturnToDemoSelection();
            yield return Until(() => session.Network.CanChooseConnection && session.SelectedDemoStage == 0,
                "Returning retires the previous selection and connection identity");
            yield return null;
            Assert.That(session.Network.DemoStageIndex, Is.Zero);
            Assert.That(session.RunProfile.FirstStage, Is.EqualTo(1), "No previous Stage3 choice leaks into the next party.");
            Assert.That(session.StageResults, Is.Empty);
            Assert.That(session.ResultSnapshot, Is.Null);
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(session.IsViewerOpen, Is.False);
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(selector.gameObject.activeInHierarchy, Is.True);
            Assert.That(GameOpeningController.IsPresenting, Is.False);
        }

        private static IEnumerator Until(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30d;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
