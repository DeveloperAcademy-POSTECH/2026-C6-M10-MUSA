using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
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
        public IEnumerator SavedHomeAutomaticallyStartsTheOpeningAndBlocksTheMenusBehindIt()
        {
            yield return LoadHome();
            Assert.That(opening.IsPlaying, Is.True);
            Assert.That(GameOpeningController.HasPlayedThisRun && GameOpeningController.IsPresenting, Is.True);
            Assert.That(opening.player.CurrentCutIndex, Is.Zero);
            Assert.That(opening.player.AutoAdvance, Is.False, "Automatic entry does not enable automatic dialogue advance.");
            Assert.That(opening.player.sequence.cuts.Count, Is.EqualTo(24));
            Assert.That(opening.player.sequence.cuts.Sum(cut => cut.PageCount), Is.EqualTo(26));
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
        public IEnumerator CompletingAllTwentyFourCutsAndTwentySixPagesReturnsToTheTitleOnce()
        {
            yield return LoadHome();
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
            Assert.That(pages, Is.EqualTo(26));
            player.Next(); player.Skip(); player.Next();
            yield return null;
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(skipped, Is.Zero);
            AssertAtTitleWithNoOpening();
            Assert.That(player.CurrentView, Is.Null);
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>(true), Is.Empty);
            Assert.That(player.voiceSource == null || !player.voiceSource.isPlaying && player.voiceSource.clip == null, Is.True);
        }

        [UnityTest]
        public IEnumerator SkipDoubleClicksLeaveTheTitleUsableAndHomeReloadDoesNotReplay()
        {
            yield return LoadHome();
            int finishes = 0;
            StoryPlayer player = opening.player;
            player.Finished += reason => { Assert.That(reason, Is.EqualTo(StoryExitReason.Skipped)); finishes++; };
            player.skipButton.onClick.Invoke();
            player.Skip(); player.Next();
            yield return null;
            Assert.That(finishes, Is.EqualTo(1));
            AssertAtTitleWithNoOpening();
            GameSession persistent = session;
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
        public IEnumerator MissingSequenceWarnsAndFallsBackToTheTitleWithoutRetrying()
        {
            LogAssert.Expect(LogType.Warning, new Regex("^HAPIO_OPENING_UNAVAILABLE: "));
            yield return LoadHome(controller => controller.player.sequence = null);
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.True);
            AssertAtTitleWithNoOpening();
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
            LogAssert.Expect(LogType.Warning, new Regex("^HAPIO_OPENING_UNAVAILABLE: "));
            yield return LoadHome(controller =>
            {
                actualOpeningRoot = controller.openingRoot;
                homeCanvas = controller.lobbyView.screen.gameObject;
                controller.openingRoot = homeCanvas;
            });
            Assert.That(actualOpeningRoot, Is.Not.Null);
            Assert.That(actualOpeningRoot.activeSelf, Is.False, "Fallback must close the independent opening Canvas.");
            Assert.That(homeCanvas.activeInHierarchy, Is.True, "A bad opening-root reference cannot hide the Home Canvas.");
            Assert.That(GameOpeningController.HasPlayedThisRun, Is.True);
            Assert.That(opening.IsPlaying || GameOpeningController.IsPresenting, Is.False);
            Assert.That(opening.lobbyView.OpeningBlocked, Is.False);
            Assert.That(opening.lobbyView.titlePage.activeInHierarchy, Is.True);
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(opening.player.CurrentView, Is.Null);
            opening.lobbyView.enterButton.onClick.Invoke();
            yield return null;
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(opening.lobbyView.browserPage.activeInHierarchy, Is.True);
            Assert.That(homeCanvas.activeInHierarchy, Is.True);
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
