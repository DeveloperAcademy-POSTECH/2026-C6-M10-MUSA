using System;
using System.Collections;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>Saved Home bootstrap and public session boundaries, without a solo stand-in for a three-player room.</summary>
    public sealed class GameYaguangDemoSessionTests
    {
        private IDisposable variant;
        private GameSession session;

        [UnitySetUp]
        public IEnumerator OpenSavedDemoHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            variant = LabGameBuildVariant.OverrideForTests(true);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            double deadline = Time.realtimeSinceStartupAsDouble + 30d;
            while ((GameSession.Instance == null || GameSession.Instance.IsLoadingScene)
                && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            session = GameSession.Instance;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.IsLoadingScene, Is.False);
        }

        [UnityTearDown]
        public IEnumerator CloseDemoHome()
        {
            if (session != null)
            {
                session.Network.Stop();
                Object.Destroy(session.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Yaguang demo session fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null;
            variant?.Dispose();
            variant = null;
        }

        [UnityTest]
        public IEnumerator DemoHomeInstallsOriginalYaguangSettingsAndKeepsTheFullCatalog()
        {
            Assert.That(session.RunProfile, Is.SameAs(GameRunProfile.YaguangDemo));
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            Assert.That(session.State.stageIndex, Is.Zero);
            Assert.That(session.State.totalStages, Is.EqualTo(1));
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(3));
            var lobby = Object.FindFirstObjectByType<GameLobbyView>();
            Assert.That(lobby.soloButton.gameObject.activeSelf, Is.False);
            Assert.That(lobby.teamSoloButton.gameObject.activeSelf, Is.False);
            Assert.That(session.Network.config.CaptureDeveloperValues(),
                Is.EqualTo(session.CaptureDeveloperSettings(2)), "Home must install the original second-stage profile.");
            GameStageCatalog catalog = GameStageCatalog.RequireDefault();
            Assert.That(catalog.stages.Length, Is.EqualTo(3));
            Assert.That(catalog.Get(1).stageId, Is.EqualTo("gumiho"));
            Assert.That(catalog.Get(2).stageIndex, Is.EqualTo(2));
            Assert.That(catalog.Get(2).stageId, Is.EqualTo("yaguang"));
            Assert.That(catalog.Get(2).yokai, Is.EqualTo(LabStageYokai.Yaguang));
            Assert.That(catalog.Get(2).gourdVisualResource, Is.EqualTo("Hapio/Gourds/Stage02_Yagwanggwi_Battle"));
            Assert.That(catalog.Get(3).stageId, Is.EqualTo("jigwi"));
            // Home DEV edits also use the first route identity, without changing Stage1's profile.
            float[] stageOne = session.CaptureDeveloperSettings(1);
            float[] stageTwo = session.CaptureDeveloperSettings(2);
            Assert.That(session.ApplyDeveloperSettings(stageTwo, out string error), Is.True, error);
            Assert.That(session.Network.config.CaptureDeveloperValues(), Is.EqualTo(stageTwo));
            Assert.That(session.CaptureDeveloperSettings(1), Is.EqualTo(stageOne));
            Assert.That(session.Network.IsConnected, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DemoRejectsOtherRoomSizesTeamRoomsSoloAndTutorialBeforeCreatingConnections()
        {
            foreach (int count in new[] { 2, 4, 5 })
            {
                Assert.That(session.CreateAndEnterRoom(count, GameMode.Story), Is.False);
                Assert.That(session.CreatePendingRoom(count, GameMode.Story), Is.False);
            }
            foreach (int count in new[] { 2, 3, 4, 6 })
            {
                Assert.That(session.CreateAndEnterRoom(count, GameMode.Territory), Is.False);
                Assert.That(session.CreatePendingRoom(count, GameMode.Territory), Is.False);
            }
            session.StartSolo();
            session.StartSolo(GameMode.Territory);
            Assert.That(GameSession.TutorialPending, Is.False);
            Assert.That(session.TryStartTutorial(), Is.False);
            Assert.That(session.TryStartTutorial(again: true), Is.False);
            Assert.That(session.IsTutorial, Is.False);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.Network.IsConnected, Is.False);
            Assert.That(session.Network.IsDeveloperSolo, Is.False);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningHandoffGoesToRoomsAndCompletedStageTwoAllowsDisconnectedSummary()
        {
            session.StartTutorialOrRooms();
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(session.IsTutorial, Is.False);
            Assert.That(session.Network.IsConnected, Is.False);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            // The terminal fallback uses the canonical final identity, not totalStages (one).
            session.State.phase = GameFlowPhase.Completed;
            session.State.stageIndex = 2;
            Assert.That(session.SummaryAllowed, Is.True);
            session.State.stageIndex = 1;
            Assert.That(session.SummaryAllowed, Is.False);
            session.State.stageIndex = 0;
            session.State.phase = GameFlowPhase.Home;
            yield return null;
        }
    }
}
