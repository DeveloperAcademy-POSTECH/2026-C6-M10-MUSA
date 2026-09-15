using System;
using System.Collections;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
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
    public sealed class ApprovedGameScenePlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/TwoPlayerBattle.unity";
        private const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        private T10GameSession game;
        private ScreenLayoutConfig source;
        private string originalConfig;
        private bool previousBackground;

        [UnitySetUp]
        public IEnumerator LoadActualSavedScene()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
#if UNITY_EDITOR
            source = UnityEditor.AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            Assert.That(source, Is.Not.Null);
            originalConfig = JsonUtility.ToJson(source);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
            yield return null; yield return null;
            game = Object.FindAnyObjectByType<T10GameSession>();
            Assert.That(game, Is.Not.Null);
        }
        [UnityTearDown]
        public IEnumerator CloseOnlyTestOwnedConnection()
        {
            if (game != null) game.Leave();
            yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>().All(m => !m.IsListening && !m.ShutdownInProgress),
                "Test Lobby did not finish shutting down.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("T10-B initial lifecycle cleanup");
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            if (source != null) Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalConfig), "Runtime approval must never mutate the saved Config asset.");
            Application.runInBackground = previousBackground;
        }

        [UnityTest]
        public IEnumerator InitialLobbyHasNoImplicitHostOrGameplayAndRuntimeConfigIsIsolated()
        {
            Assert.That(game.Attached || game.InitialConfirmed, Is.False);
            Assert.That(game.Snapshot, Is.Null);
            Assert.That(Object.FindObjectsByType<DirectConnectionSession>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(game.Controller.Views, Is.Empty);
            Assert.That(game.Controller.Hud.Canvas.gameObject.activeSelf, Is.False);
            Assert.That(game.GetComponent<T10LobbyHud>().Canvas.gameObject.activeSelf, Is.True);
            Assert.That(game.Controller.Battle.Phase, Is.EqualTo(BattlePhase.Boot));
            Assert.That(game.Controller.RequestGenerate() || game.Controller.RequestHostStart(), Is.False);
            var runtime = game.GetComponent<GameRuntimeConfig>();
            Assert.That(runtime.Value, Is.Not.Null);
            if (source != null) Assert.That(runtime.Value, Is.Not.SameAs(source));
            var approved = LobbyHostConfig.Capture(runtime.Value);
            approved.damage += 1;
            runtime.Apply(approved);
            Assert.That(runtime.Value.BaseDamage, Is.EqualTo(approved.damage));
            if (source != null) Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalConfig));
            yield return null;
            Debug.Log("C6_T10B_SCENE_CHECK initialLobby=true implicitHost=false initialOrbs=0 savedConfigPreserved=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator OnePlayerRoomCannotBypassTheLobbyOrStartPreparedGameplay()
        {
            Assert.That(game.Lobby.CreateRoom("Lifecycle test room", "25115"), Is.True);
            yield return WaitFor(() => game.Lobby.Connected && game.Lobby.Snapshot != null, "Single-player Lobby failed to connect.");
            Assert.That(Object.FindObjectsByType<NetworkManager>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DirectConnectionSession>().Length, Is.EqualTo(1));
            Assert.That(game.Lobby.Snapshot.ParticipantCount, Is.EqualTo(1));
            Assert.That(game.Lobby.ToggleReady(), Is.True);
            Assert.That(game.Lobby.StartMatch(), Is.False);
            Assert.That(game.Controller.RequestHostStart(), Is.False);
            Assert.That(game.Controller.Battle.HostStartPreparedRound(), Is.False);
            Assert.That(game.InitialConfirmed || game.Attached, Is.False);
            Assert.That(game.Controller.Attack.Connected || game.Controller.Resource.Connected || game.Controller.Battle.Connected, Is.False);
            Assert.That(game.Controller.Resource.RequestGenerate(), Is.False);
            Assert.That(game.Snapshot, Is.Null);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(game.Controller.Views, Is.Empty);
            Assert.That(game.Controller.Hud.Canvas.gameObject.activeSelf, Is.False);
            Debug.Log("C6_T10B_SCENE_CHECK participants=1 oneSharedConnection=true initialAckMissing=true bypassStartRejected=true physicalDevice=false");
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
