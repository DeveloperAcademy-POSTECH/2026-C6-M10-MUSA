using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>Saved Home and the real opening player. This is not a device, native build or memory test.</summary>
    public sealed class GameOpeningControllerTests
    {
        private GameSession session;
        private GameOpeningController opening;

        [UnitySetUp]
        public IEnumerator StartANewAppRun()
        {
            Assert.That(GameSession.Instance, Is.Null, "The preceding fixture must release its persistent session.");
            ResetAppRun();
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.False);
            Assert.That(GameOpeningController.IsPresenting, Is.False);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (opening != null) opening.enabled = false;
            GameSession ownedSession = session != null ? session : GameSession.Instance;
            if (ownedSession != null)
            {
                ownedSession.Network.Stop();
                Object.Destroy(ownedSession.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Opening flow fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            opening = null;
            session = null;
            ResetAppRun();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedHomeWaitsAtTheTitleWithoutPlayingOrSearchingUntilStartIsClicked()
        {
            yield return LoadHome();
            AssertAtTitleWithNoOpening();
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.False);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(opening.player.CurrentView, Is.Null);
            Assert.That(session.Network.IsSearchingRooms, Is.False);
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.False);
        }

        [UnityTest]
        public IEnumerator FirstStartButtonClickStartsTheOpeningAndBlocksTheMenusBehindIt()
        {
            yield return LoadHome();
            yield return PressStart();
            Assert.That(opening.IsPlaying, Is.True);
            Assert.That(GameOpeningController.HasPlayedThisRun && GameOpeningController.IsPresenting, Is.True);
            Assert.That(opening.player.CurrentCutIndex, Is.Zero);
            Assert.That(opening.player.AutoAdvance, Is.False, "Button entry keeps screen-tap dialogue advance.");
            Assert.That(opening.player.sequence.cuts.Count, Is.EqualTo(25));
            Assert.That(opening.player.sequence.cuts.Sum(cut => cut.PageCount), Is.EqualTo(27));
            Assert.That(opening.openingRoot.activeInHierarchy, Is.True);
            Assert.That(opening.openingRoot.GetComponentsInChildren<GameSafeArea>(true), Has.Length.EqualTo(1));
            Canvas canvas = opening.openingRoot.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);

            GameLobbyView lobby = opening.lobbyView;
            Assert.That(lobby.OpeningBlocked, Is.True);
            Assert.That(lobby.design.gameObject.activeInHierarchy, Is.False);
            Assert.That(lobby.background.gameObject.activeInHierarchy, Is.False);
            int capacity = lobby.CreationCapacity;
            // Invoke the actual callbacks directly as well as checking visibility: hiding alone is insufficient.
            lobby.enterButton.onClick.Invoke();
            lobby.createButton.onClick.Invoke();
            lobby.soloButton.onClick.Invoke();
            lobby.countButtons[0].onClick.Invoke();
            lobby.screen.developerButton.onClick.Invoke();
            yield return null;
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            Assert.That(session.Network.IsConnected || session.Network.IsJoiningRoom || session.Network.IsSearchingRooms, Is.False);
            Assert.That(lobby.CreationCapacity, Is.EqualTo(capacity));
            Assert.That(lobby.screen.developerPanel.activeInHierarchy, Is.False);
            Assert.That(opening.IsPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator CompletingAllTwentyFourCutsAndTwentySixPagesOpensTheBrowserOnce()
        {
            yield return LoadHome();
            yield return PressStart();
            StoryPlayer player = opening.player;
            int completed = 0, skipped = 0, pages = 0;
            player.Finished += reason => { if (reason == StoryExitReason.Completed) completed++; else skipped++; };
            for (int cutIndex = 0; cutIndex < player.sequence.cuts.Count; cutIndex++)
            {
                Assert.That(player.CurrentCutIndex, Is.EqualTo(cutIndex));
                if (!player.IsReading) player.Next();
                StoryCutView picture = player.CurrentView;
                int pageCount = player.sequence.cuts[cutIndex].PageCount;
                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    Assert.That(player.IsReading, Is.True);
                    Assert.That(player.CurrentCutIndex, Is.EqualTo(cutIndex));
                    Assert.That(player.CurrentPageIndex, Is.EqualTo(pageIndex));
                    Assert.That(player.CurrentView, Is.SameAs(picture), "Another page on the same cut reuses the picture.");
                    pages++;
                    player.Next();
                }
                yield return null;
            }
            Assert.That(pages, Is.EqualTo(27));
            player.Next(); player.Skip(); player.Next();
            yield return Until(() => !GameOpeningController.IsPresenting, "Completed story opens the room browser");
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(skipped, Is.Zero);
            AssertAtBrowserWithNoOpening();
            Assert.That(player.CurrentView, Is.Null);
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(true), Is.Empty);
            Assert.That(player.voiceSource == null || !player.voiceSource.isPlaying && player.voiceSource.clip == null, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedScreenTapFinishesTheLastPageBeforeOpeningTheBrowserBehindAReleaseShield()
        {
            yield return LoadHome();
            yield return PressStart();
            StoryPlayer player = opening.player;
            while (player.CurrentCutIndex < player.sequence.cuts.Count - 1 ||
                player.CurrentPageIndex < player.sequence.cuts[player.CurrentCutIndex].PageCount - 1 || !player.IsReading)
                player.Next();
            var surface = opening.openingRoot.GetComponentInChildren<StoryTapAdvance>();
            Assert.That(surface, Is.Not.Null);
            var events = UnityEngine.EventSystems.EventSystem.current;
            var pointer = new UnityEngine.EventSystems.PointerEventData(events)
            {
                pointerId = -1,
                position = new Vector2(Screen.width * .5f, Screen.height * .5f),
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };
            pointer.pressPosition = pointer.position;
            Canvas.ForceUpdateCanvases();
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.SameAs(surface.gameObject));
            var down = UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler;
            var up = UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler;
            var click = UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler;
            UnityEngine.EventSystems.ExecuteEvents.Execute(surface.gameObject, pointer, down);
            Assert.That(player.IsPlaying, Is.True);
            UnityEngine.EventSystems.ExecuteEvents.Execute(surface.gameObject, pointer, up);
            Assert.That(player.IsPlaying, Is.True);
            UnityEngine.EventSystems.ExecuteEvents.Execute(surface.gameObject, pointer, click);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(opening.lobbyView.OpeningBlocked, Is.True, "The finishing release still has an input shield.");
            Assert.That(opening.openingRoot.activeInHierarchy, Is.True);
            UnityEngine.EventSystems.ExecuteEvents.Execute(surface.gameObject, pointer, click);
            Assert.That(session.RoomBrowserVisible, Is.False, "Search starts only after the finishing release is shielded.");
            yield return Until(() => !GameOpeningController.IsPresenting, "Release shield opens the room browser");
            AssertAtBrowserWithNoOpening();
        }

        [UnityTest]
        public IEnumerator SkipDoubleClicksOpenTheBrowserAndLaterTitleStartDoesNotReplay()
        {
            yield return LoadHome();
            yield return PressStart();
            int finishes = 0;
            StoryPlayer player = opening.player;
            player.Finished += reason => { Assert.That(reason, Is.EqualTo(StoryExitReason.Skipped)); finishes++; };
            player.skipButton.onClick.Invoke();
            player.Skip(); player.Next();
            yield return Until(() => !GameOpeningController.IsPresenting, "Skip opens the room browser once");
            Assert.That(finishes, Is.EqualTo(1));
            AssertAtBrowserWithNoOpening();
            GameSession persistent = session;
            session.GoHome();
            yield return Until(() => !session.IsLoadingScene, "Return to the title in the same app run");
            yield return null;
            AssertAtTitleWithNoOpening();
            opening.lobbyView.enterButton.onClick.Invoke();
            yield return null;
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(opening.lobbyView.browserPage.activeInHierarchy, Is.True);

            yield return LoadHome();
            Assert.That(session, Is.SameAs(persistent));
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.True);
            Assert.That(opening.IsPlaying || GameOpeningController.IsPresenting, Is.False);
            Assert.That(opening.openingRoot.activeSelf, Is.False);
            Assert.That(opening.lobbyView.OpeningBlocked, Is.False);
            Assert.That(session.RoomBrowserVisible, Is.True, "Reload keeps the existing room-browser destination.");
            Assert.That(opening.lobbyView.browserPage.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator DisablingTheControllerCancelsItsViewAndAudioAndUnblocksTheTitle()
        {
            yield return LoadHome();
            yield return PressStart();
            StoryPlayer player = opening.player;
            player.ToggleAuto();
            Assert.That(player.CurrentView, Is.Not.Null);
            opening.enabled = false;
            yield return null;
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.True, "An interrupted attempt is consumed for this app run.");
            AssertAtTitleWithNoOpening();
            Assert.That(player.CurrentView, Is.Null);
            Assert.That(player.voiceSource == null || !player.voiceSource.isPlaying && player.voiceSource.clip == null, Is.True);
            opening.enabled = true;
            yield return null;
            Assert.That(opening.IsPlaying, Is.False, "Re-enabling cannot replay the opening in the same app run.");
            opening.lobbyView.enterButton.onClick.Invoke();
            Assert.That(session.RoomBrowserVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator MissingSequenceWarnsOnlyOnStartAndFallsBackToTheBrowserWithoutRetrying()
        {
            yield return LoadHome(controller => controller.player.sequence = null);
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.False);
            Assert.That(opening.lobbyView.titlePage.activeInHierarchy, Is.True);
            Assert.That(GameOpeningController.IsPresenting, Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("^HAPIO_OPENING_UNAVAILABLE: "));
            opening.lobbyView.enterButton.onClick.Invoke();
            yield return null;
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.True);
            AssertAtBrowserWithNoOpening();
            Assert.That(opening.player.CurrentView, Is.Null);
            opening.enabled = false;
            opening.enabled = true;
            yield return null;
            Assert.That(opening.IsPlaying || GameOpeningController.IsPresenting, Is.False);
            opening.lobbyView.enterButton.onClick.Invoke();
            Assert.That(session.RoomBrowserVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator MistakenHomeCanvasBindingDoesNotHideTheHomeDuringFallback()
        {
            GameObject actualOpeningRoot = null, homeCanvas = null;
            yield return LoadHome(controller =>
            {
                actualOpeningRoot = controller.openingRoot;
                homeCanvas = controller.lobbyView.screen.gameObject;
                controller.openingRoot = homeCanvas;
            });
            Assert.That(opening.lobbyView.titlePage.activeInHierarchy, Is.True);
            Assert.That(GameOpeningController.IsPresenting, Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("^HAPIO_OPENING_UNAVAILABLE: "));
            opening.lobbyView.enterButton.onClick.Invoke();
            yield return null;
            Assert.That(actualOpeningRoot, Is.Not.Null);
            Assert.That(actualOpeningRoot.activeSelf, Is.False, "Fallback must close the independent opening Canvas.");
            Assert.That(homeCanvas.activeInHierarchy, Is.True, "A bad opening-root reference cannot hide the Home Canvas.");
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.True);
            Assert.That(opening.IsPlaying || GameOpeningController.IsPresenting, Is.False);
            Assert.That(opening.lobbyView.OpeningBlocked, Is.False);
            Assert.That(opening.lobbyView.browserPage.activeInHierarchy, Is.True);
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(opening.player.CurrentView, Is.Null);
            opening.lobbyView.enterButton.onClick.Invoke();
            yield return null;
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(opening.lobbyView.browserPage.activeInHierarchy, Is.True);
            Assert.That(homeCanvas.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator DisablingBeforeStartDoesNotConsumeTheOpeningAndANewAppRunAllowsItAgain()
        {
            yield return LoadHome();
            opening.enabled = false;
            yield return null;
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.False);
            opening.enabled = true;
            yield return PressStart();
            opening.player.Skip();
            yield return Until(() => !GameOpeningController.IsPresenting, "First run finishes");
            session.Network.Stop();
            Object.Destroy(session.gameObject);
            yield return null;
            session = null;
            ResetAppRun();
            yield return LoadHome();
            AssertAtTitleWithNoOpening();
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.False);
            yield return PressStart();
            Assert.That(opening.player.CurrentCutIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HeldPointerDelaysBothTheFirstPageAndTheRoomBrowserUntilRelease()
        {
            yield return LoadHome();
            InputSettings originalInputSettings = InputSystem.settings;
            HideFlags originalInputFlags = originalInputSettings.hideFlags;
            string originalInputJson = JsonUtility.ToJson(originalInputSettings);
            InputSettings.BackgroundBehavior originalBackgroundBehavior = originalInputSettings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior = originalInputSettings.editorInputBehaviorInPlayMode;
            InputSettings fixtureInputSettings = null;
            Mouse mouse = null;
            try
            {
                // InputSystem destroys replaced HideAndDontSave settings; keep this exact original for restoration.
                originalInputSettings.hideFlags = HideFlags.DontUnloadUnusedAsset;
                fixtureInputSettings = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(originalInputJson, fixtureInputSettings);
                fixtureInputSettings.hideFlags = HideFlags.HideAndDontSave;
                // A batch Editor has no focused GameView, but this test still needs its queued pointer events.
                fixtureInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                fixtureInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = fixtureInputSettings;
                mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(mouse.leftButton.isPressed, Is.True, "The queued Start press must reach the owned test mouse.");
                opening.lobbyView.enterButton.onClick.Invoke();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(mouse.leftButton.isPressed, Is.True, "An unfocused Editor must not release the held Start pointer.");
                Assert.That(GameOpeningController.IsPresenting, Is.True);
                Assert.That(opening.IsPlaying, Is.False, "A held Start pointer cannot open or skip the first page.");
                Assert.That(session.RoomBrowserVisible, Is.False);

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Assert.That(mouse.leftButton.isPressed, Is.False, "The queued Start release must reach the owned test mouse.");
                yield return Until(() => opening.IsPlaying, "Releasing Start opens the first page");
                Assert.That(opening.player.CurrentCutIndex, Is.Zero);
                Assert.That(opening.player.CurrentPageIndex, Is.Zero);

                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(mouse.leftButton.isPressed, Is.True, "The queued finishing press must reach the owned test mouse.");
                opening.player.Skip();
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(mouse.leftButton.isPressed, Is.True, "An unfocused Editor must not release the held finishing pointer.");
                Assert.That(opening.lobbyView.OpeningBlocked, Is.True);
                Assert.That(opening.openingRoot.activeInHierarchy, Is.True);
                Assert.That(session.RoomBrowserVisible, Is.False, "A held finishing pointer cannot hit a room card.");

                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Assert.That(mouse.leftButton.isPressed, Is.False, "The queued finishing release must reach the owned test mouse.");
                yield return Until(() => !GameOpeningController.IsPresenting, "Release opens the browser");
                AssertAtBrowserWithNoOpening();
            }
            finally
            {
                // Every acquired resource is released even if a later setup step or assertion fails.
                try
                {
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                }
                finally
                {
                    try
                    {
                        InputSystem.settings = originalInputSettings;
                    }
                    finally
                    {
                        originalInputSettings.hideFlags = originalInputFlags;
                        // The settings setter may already have destroyed the replaced temporary settings.
                        if (fixtureInputSettings != null) Object.DestroyImmediate(fixtureInputSettings);
                    }
                }
            }
            // Run restoration assertions after finally so they do not hide an earlier gameplay assertion failure.
            Assert.That(InputSystem.settings, Is.SameAs(originalInputSettings));
            Assert.That(originalInputSettings.hideFlags, Is.EqualTo(originalInputFlags));
            Assert.That(JsonUtility.ToJson(originalInputSettings), Is.EqualTo(originalInputJson));
            Assert.That(originalInputSettings.backgroundBehavior, Is.EqualTo(originalBackgroundBehavior));
            Assert.That(originalInputSettings.editorInputBehaviorInPlayMode, Is.EqualTo(originalEditorInputBehavior));
            Assert.That(mouse.added, Is.False, "The fixture must remove only the mouse it created.");
            Assert.That(fixtureInputSettings == null, Is.True, "The fixture must release its temporary input settings.");
        }

        private IEnumerator PressStart()
        {
            AssertAtTitleWithNoOpening();
            opening.lobbyView.enterButton.onClick.Invoke();
            opening.lobbyView.enterButton.onClick.Invoke();
            Assert.That(GameOpeningController.IsPresenting, Is.True);
            Assert.That(opening.IsPlaying, Is.False, "The title click is shielded for at least one frame before the first page.");
            Assert.That(session.RoomBrowserVisible || session.Network.IsSearchingRooms, Is.False);
            yield return Until(() => opening.IsPlaying, "Released Start opens the first cut exactly once");
            Assert.That(opening.player.CurrentCutIndex, Is.Zero);
            Assert.That(opening.player.CurrentPageIndex, Is.Zero);
        }

        private IEnumerator LoadHome(Action<GameOpeningController> beforeStart = null)
        {
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> prepare = (scene, mode) =>
            {
                if (scene.path != GameScenePaths.Home || beforeStart == null) return;
                beforeStart(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameOpeningController>(true)).Single());
            };
            SceneManager.sceneLoaded += prepare;
            try { yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single); }
            finally { SceneManager.sceneLoaded -= prepare; }
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            opening = Object.FindFirstObjectByType<GameOpeningController>();
            Assert.That(opening, Is.Not.Null, "Saved Home must bind the actual opening controller.");
            Assert.That(opening.player, Is.Not.Null);
            Assert.That(opening.lobbyView, Is.Not.Null);
            yield return null;
        }

        private void AssertAtTitleWithNoOpening()
        {
            Assert.That(opening.IsPlaying || GameOpeningController.IsPresenting, Is.False);
            Assert.That(opening.openingRoot.activeSelf, Is.False);
            Assert.That(opening.lobbyView.OpeningBlocked, Is.False);
            Assert.That(opening.lobbyView.titlePage.activeInHierarchy, Is.True);
            Assert.That(opening.lobbyView.browserPage.activeInHierarchy, Is.False);
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
        }

        private void AssertAtBrowserWithNoOpening()
        {
            Assert.That(opening.IsPlaying || GameOpeningController.IsPresenting, Is.False);
            Assert.That(opening.openingRoot.activeSelf, Is.False);
            Assert.That(opening.lobbyView.OpeningBlocked, Is.False);
            Assert.That(opening.lobbyView.titlePage.activeInHierarchy, Is.False);
            Assert.That(opening.lobbyView.browserPage.activeInHierarchy, Is.True);
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
        }

        private static void ResetAppRun()
        {
            MethodInfo reset = typeof(GameOpeningController).GetMethod("ResetRunState", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            var initialize = (RuntimeInitializeOnLoadMethodAttribute)Attribute.GetCustomAttribute(reset, typeof(RuntimeInitializeOnLoadMethodAttribute));
            Assert.That(initialize, Is.Not.Null);
            Assert.That(initialize.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
            reset.Invoke(null, null);
        }

        private static IEnumerator Until(Func<bool> predicate, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15d;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }
    }
}
