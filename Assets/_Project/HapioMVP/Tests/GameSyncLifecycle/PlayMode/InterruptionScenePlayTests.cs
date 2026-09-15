using System;
using System.Collections;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.GameSync;
using C6.Prototype.Lobby;
using C6.Prototype.Networking;
using C6.Prototype.Presentation;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.GameSyncLifecycle.Tests
{
    public sealed class InterruptionScenePlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity";
        private const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        private const string Port = "25237";
        private T10GameSession game;
        private ScreenLayoutConfig sourceConfig;
        private string configBefore;
        private bool previousBackground;

        [UnitySetUp]
        public IEnumerator LoadSavedInterruptionScene()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
#if UNITY_EDITOR
            sourceConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            Assert.That(sourceConfig, Is.Not.Null);
            configBefore = JsonUtility.ToJson(sourceConfig);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
            yield return null;
            yield return null;
            game = Object.FindAnyObjectByType<T10GameSession>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.InterruptionHandlingEnabled, Is.True, "The saved T13 scene must opt into interruption handling.");
            Assert.That(game.Attached, Is.False);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
        }

        [UnityTearDown]
        public IEnumerator ReleaseOnlyTheTestSceneConnection()
        {
            if (game != null) game.Leave();
            yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                .All(manager => !manager.IsListening && !manager.ShutdownInProgress), "Test connection did not shut down.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("T13 interruption test cleanup");
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            if (sourceConfig != null)
                Assert.That(JsonUtility.ToJson(sourceConfig), Is.EqualTo(configBefore), "Interruption handling must not edit the Config asset.");
            Application.runInBackground = previousBackground;
        }

        [UnityTest]
        public IEnumerator SimulatedBackgroundEndsTheRoomAndResumeWaitsForAManualNewHost()
        {
            yield return CreateHost();
            string oldRoom = game.Lobby.Snapshot.roomId;
            string oldSession = game.Lobby.Snapshot.sessionId;
            var manager = game.Lobby.Connection.OwnedManager;
            Assert.That(game.Lobby.ToggleReady(), Is.True);
            yield return WaitFor(() => game.Lobby.LocalReady && !game.Lobby.HasPending, "Ready was not confirmed.");

            // This invokes a callback in a running test process. It is not an actual iOS suspension.
            game.Lobby.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            yield return WaitFor(() => !game.Lobby.Connected && game.Lobby.Connection.CanStart
                && !manager.IsListening && !manager.ShutdownInProgress, "Simulated background did not end the real Host transport.");
            Assert.That(game.Lobby.Snapshot, Is.Null);
            Assert.That(game.Lobby.HostConfig, Is.Null);
            Assert.That(game.Lobby.LocalReady || game.Lobby.HasPending, Is.False);
            Assert.That(game.Lobby.Discovery.ActiveNativeOperations, Is.Zero);

            game.Lobby.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            yield return null;
            yield return null;
            AssertConnectionScreenWithoutGameplay();
            Assert.That(game.Lobby.Discovery.IsBrowsing || game.Lobby.Discovery.IsAdvertising, Is.False);
            Assert.That(game.Lobby.Connection.State, Is.EqualTo(DirectConnectionState.Idle));
            Assert.That(game.GetComponent<T10LobbyHud>().CreateRoomButton.interactable, Is.True);
            Assert.That(game.LastInterruptionReason, Is.EqualTo("APPLICATION_BACKGROUNDED"));
            Assert.That(game.Lobby.Error, Is.EqualTo("APPLICATION_BACKGROUNDED"));
            Assert.That(game.GetComponent<T10LobbyHud>().ErrorLabel.text, Does.Contain("foreground"));

            yield return CreateHost();
            Assert.That(game.Lobby.Snapshot.roomId, Is.Not.EqualTo(oldRoom));
            Assert.That(game.Lobby.Snapshot.sessionId, Is.Not.EqualTo(oldSession));
            Assert.That(game.Lobby.Connection.OwnedManager, Is.SameAs(manager));
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Has.Length.EqualTo(1));
            Assert.That(game.Lobby.LocalReady || game.Lobby.HasPending, Is.False);
            Assert.That(game.Controller.Views, Is.Empty);
            Assert.That(game.Controller.Resource.HasPending || game.Controller.Combination.HasPending, Is.False);
            Assert.That(game.Controller.RequestGenerate(), Is.False, "A new one-player room cannot resume the old battle.");
            string newRoom = game.Lobby.Snapshot.roomId;
            string newSession = game.Lobby.Snapshot.sessionId;
            game.Lobby.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(game.Lobby.Connected, Is.True, "A duplicate resume from the old pause must not close the newly created room.");
            Assert.That(game.Lobby.Snapshot.roomId, Is.EqualTo(newRoom));
            Assert.That(game.Lobby.Snapshot.sessionId, Is.EqualTo(newSession));
            Assert.That(game.Lobby.Error, Is.Null.Or.Empty, "A deliberate new room clears the old Lobby notice.");
            Debug.Log("C6_T13_PLAYMODE source=SIMULATED_PAUSE_CALLBACK actualNgoHostEnded=true resumeConnectionScreen=true automaticReconnect=false manualNewRoom=true singleManager=true emptyGameplay=true physicalDevice=false actualOSSuspension=false");
        }

        [UnityTest]
        public IEnumerator IdlePauseAndResumeDoNotInventAClosedRoomOrStartNetworking()
        {
            game.Lobby.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            game.Lobby.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            game.Lobby.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            yield return null;
            yield return null;
            AssertConnectionScreenWithoutGameplay();
            Assert.That(game.LastInterruptionReason, Is.Null.Or.Empty);
            Assert.That(game.Lobby.Error, Is.Null.Or.Empty);
            Assert.That(game.Lobby.Connected || game.Lobby.Discovery.IsBrowsing || game.Lobby.Discovery.IsAdvertising, Is.False);
            Assert.That(game.Lobby.Discovery.ActiveNativeOperations, Is.Zero);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            Assert.That(game.GetComponent<T10LobbyHud>().CreateRoomButton.interactable, Is.True);
            Debug.Log("C6_T13_PLAYMODE source=SIMULATED_PAUSE_CALLBACK idlePause=true noInventedRoomInterruption=true automaticReconnect=false physicalDevice=false actualOSSuspension=false");
        }

        [UnityTest]
        public IEnumerator FocusLossAndUnpairedResumeDoNotEndAnOpenRoom()
        {
            yield return CreateHost();
            string room = game.Lobby.Snapshot.roomId;
            string session = game.Lobby.Snapshot.sessionId;
            game.Controller.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
            game.Lobby.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            yield return null;
            yield return null;
            game.Controller.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            Assert.That(game.Lobby.Connected, Is.True);
            Assert.That(game.Lobby.Snapshot.roomId, Is.EqualTo(room));
            Assert.That(game.Lobby.Snapshot.sessionId, Is.EqualTo(session));
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Has.Length.EqualTo(1));
            Assert.That(game.LastInterruptionReason, Is.Null.Or.Empty);
            Debug.Log("C6_T13_PLAYMODE source=SIMULATED_FOCUS_CALLBACK sameRoomMaintained=true unpairedResumeHarmless=true physicalDevice=false actualControlCenter=false");
        }

        private IEnumerator CreateHost()
        {
            Assert.That(game.Lobby.CreateRoom("T13 lifecycle test", Port), Is.True);
            yield return WaitFor(() => game.Lobby.Connected && game.Lobby.Snapshot != null, "The manual test Host did not start.");
            Assert.That(game.Lobby.Snapshot.ParticipantCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DirectConnectionSession>(), Has.Length.EqualTo(1));
        }

        private void AssertConnectionScreenWithoutGameplay()
        {
            Assert.That(game.Attached || game.InitialConfirmed, Is.False);
            Assert.That(game.Snapshot, Is.Null);
            Assert.That(game.GetComponent<T10LobbyHud>().Canvas.gameObject.activeSelf, Is.True);
            Assert.That(game.Controller.Hud.Canvas.gameObject.activeSelf, Is.False);
            Assert.That(game.Controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(game.Controller.Resource.HasPending || game.Controller.Combination.HasPending, Is.False);
            Assert.That(game.Controller.RequestGenerate() || game.Controller.RequestHostStart(), Is.False);
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
