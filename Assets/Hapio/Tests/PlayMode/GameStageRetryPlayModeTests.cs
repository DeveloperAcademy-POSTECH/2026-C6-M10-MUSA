using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>Saved Scenes, actual Host transport/model, and real result/load/countdown transitions.</summary>
    [Category("Slow")]
    public sealed class GameStageRetryPlayModeTests
    {
        private GameSession session;
        private LabNetwork network;

        [UnitySetUp]
        public IEnumerator StartFromSavedHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene,
                "The saved Home must finish bootstrapping its persistent session.");
            session = GameSession.Instance;
            network = session.Network;
            network.port = AvailablePort();
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 600f);
            Set(values, "Player Max HP", 237f);
            Set(values, "Starting Stamina", 65f);
            Set(values, "Stamina Recovery Amount", 0f);
            Set(values, "Relay Orb Chance (%)", 0f);
            Set(values, "Secret Box Chance (%)", 0f);
            Set(values, "Potion Chance (%)", 0f);
            Set(values, "Bomb Chance (%)", 0f);
            Set(values, "Paint Surface Resolution (0 low 1 medium 2 high)", 0f);
            Set(values, "Paint Initial Coverage (%)", 100f);
            Set(values, "Paint Pattern", 0f);
            Set(values, "Paint Drying (0 off, 1 on)", 0f);
            Set(values, "Completed Gourd Viewer (0 off, 1 on)", 0f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
        }

        [UnityTearDown]
        public IEnumerator ReleaseFixture()
        {
            Time.timeScale = 1f;
            GameTestStartTiming.UseShort();
            if (session != null)
            {
                network.Stop();
                Object.Destroy(session.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Stage retry fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null;
            network = null;
        }

        [UnityTest]
        public IEnumerator FailedSecondStageRetriesInTheSameRoomWithFreshResourcesAndKeepsStageOneRecord()
        {
            session.StartSolo();
            yield return Playing(1);
            string run = session.State.runId;
            ulong playerId = network.LocalPlayerId;
            ulong[] roster = network.ConnectedPlayerIds;
            var persistentNetwork = network;
            network.Generate();
            yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1, "Stage one generation must commit.");
            // Finish the real CPU paint map; projectile collision is covered by dedicated projectile tests.
            LabModel first = Model();
            Assert.That(first.PaintSurface.RegisterHit(Vector3.forward, first.PaintColorFor(LabOrbElement.Fire), playerId, 1), Is.True);
            Assert.That(first.PaintSurface.IsComplete, Is.True);
            yield return Result(GameFlowPhase.StageResult, GameScenePaths.StageResult);
            yield return FinishStory(() => session.CanAdvanceStage);
            Assert.That(session.StageResults.Count, Is.EqualTo(1));
            LabResultStage stageOne = session.StageResults.Single();
            string firstRecord = JsonUtility.ToJson(stageOne.Players.Single());
            session.NextStage();
            yield return Playing(2);
            network.Generate();
            yield return Until(() => network.Snapshot.players.Single().generatedTotal == 1, "The failed attempt must have its own records.");
            LabOrbBoard failedBoard = network.board;
            string failedBattle = session.State.battleSessionId;
            uint failedRound = session.State.battleRound;
            string settings = network.SessionConfigFingerprint;
            LabModel failedModel = Model();
            Assert.That(failedModel.TryApplyPlayerDamage(playerId, network.config.PlayerMaxHp, ModelTime(failedModel)), Is.True);
            yield return Result(GameFlowPhase.GameOver, GameScenePaths.GameOver);
            Assert.That(session.State.retryableDefeat, Is.True);
            Assert.That(session.ResultSnapshot.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(session.ResultSnapshot.players.Single().personalHp, Is.Zero);
            Assert.That(network.IsConnected, Is.True, "A normal defeat retains the room transport.");
            Assert.That(session.IsStageRetryAvailable, Is.True);
            yield return FinishStory(() => session.CanRetryStage);
            var presentation = Object.FindFirstObjectByType<GameStageStoryPresentation>();
            var actions = presentation.player.CurrentView.GetComponent<StoryResultActions>();
            var retry = actions.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button =>
                Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i =>
                    button.onClick.GetPersistentTarget(i) == actions && button.onClick.GetPersistentMethodName(i) == "Retry"));
            yield return Until(() => retry.gameObject.activeInHierarchy && retry.interactable,
                "The authored retry button reflects the released Host action permission.");
            Assert.That(presentation.advanceButton.gameObject.activeInHierarchy, Is.False,
                "The original picture has the sole retry action; no footer duplicate remains.");
            retry.onClick.Invoke();
            Assert.That(session.RetryStage(), Is.False, "A duplicate click cannot prepare another retry generation.");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
            Assert.That(session.State.battleSessionId, Is.Empty);
            Assert.That(session.State.battleRound, Is.Zero);
            Assert.That(session.ResultSnapshot, Is.Null, "The failed snapshot is not reused as the new attempt.");
            yield return Playing(2);
            Assert.That(GameSession.Instance, Is.SameAs(session));
            Assert.That(session.Network, Is.SameAs(persistentNetwork));
            Assert.That(network.IsConnected && network.IsHost, Is.True);
            Assert.That(network.LocalPlayerId, Is.EqualTo(playerId));
            Assert.That(network.ConnectedPlayerIds, Is.EquivalentTo(roster));
            Assert.That(session.State.runId, Is.EqualTo(run));
            Assert.That(session.State.battleSessionId, Is.Not.EqualTo(failedBattle));
            Assert.That(session.State.battleRound, Is.Not.Zero);
            Assert.That(session.State.retryableDefeat, Is.False);
            Assert.That(failedBoard == null, Is.True, "Retry unloads the failed board instead of reviving its local state.");
            Assert.That(network.SessionConfigFingerprint, Is.EqualTo(settings));
            Assert.That(network.GameStartHeld || network.IsBattlePaused, Is.False);
            LabSnapshot fresh = network.Snapshot;
            Assert.That(fresh.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(fresh.orbs, Is.Empty);
            Assert.That(fresh.paintCoveredCount, Is.Zero);
            Assert.That(network.PaintSurface.CoveredCount, Is.Zero);
            LabPlayerState player = fresh.players.Single();
            Assert.That(player.personalHp, Is.EqualTo(network.config.PlayerMaxHp));
            Assert.That(player.stamina, Is.EqualTo(network.config.StaminaStart).Within(.01f));
            Assert.That(player.generatedTotal + player.combinedTotal + player.passedTotal + player.hitTotal + player.damageTotal, Is.Zero);
            Assert.That(fresh.remaining, Is.GreaterThan(network.config.BattleDurationSeconds - 10d));
            Assert.That(session.StageResults.Count, Is.EqualTo(1), "A failed attempt must not add or replace a completed stage.");
            Assert.That(session.StageResults.Single(), Is.SameAs(stageOne));
            Assert.That(JsonUtility.ToJson(stageOne.Players.Single()), Is.EqualTo(firstRecord));
            Assert.That(session.ResultSnapshot, Is.Null);
            // An old result callback is inert after the fresh battle/round has started.
            session.SetResultPresentationBusy(true);
            Assert.That(session.IsAnyResultPresentationBusy, Is.False);
            Assert.That(session.RetryStage(), Is.False);
            Assert.That(failedRound, Is.GreaterThan(0u));
        }

        [UnityTest]
        public IEnumerator LeavingPlayingBattleIsHomeOnlyAndNeverOffersFailureRetry()
        {
            session.StartSolo();
            yield return Playing(1);
            session.ExitRun();
            yield return Result(GameFlowPhase.GameOver, GameScenePaths.GameOver);
            Assert.That(session.State.retryableDefeat, Is.False);
            Assert.That(session.IsStageRetryAvailable || session.CanRetryStage, Is.False);
            Assert.That(session.ResultSnapshot, Is.Null);
            Assert.That(session.RetryStage(), Is.False);
            session.GoHome();
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene,
                "An explicit exit returns Home without a retry transition.");
            Assert.That(network.IsConnected, Is.False);
        }

        private IEnumerator Playing(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "The requested stage must pass its normal start barriers.");

        private IEnumerator Result(GameFlowPhase phase, string scene) => Until(() => session.State.phase == phase
            && !session.IsLoadingScene && SceneManager.GetActiveScene().path == scene, "The saved result Scene must finish loading.");

        private IEnumerator FinishStory(Func<bool> ready)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var presentation = Object.FindFirstObjectByType<GameStageStoryPresentation>();
                if (presentation != null && presentation.IsPresenting && !presentation.IsLocalStoryComplete
                    && presentation.player != null && presentation.player.IsPlaying)
                    presentation.player.Skip();
                yield return null;
            }
            Assert.That(ready(), Is.True, "Local story completion and committed result readiness must release the Host action.");
        }

        private LabModel Model() => (LabModel)typeof(LabNetwork).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(network);
        private static double ModelTime(LabModel model) => (double)typeof(LabModel).GetField("lastTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
        private static IEnumerator Until(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        private static void Set(float[] values, string label, float value) =>
            values[Enumerable.Range(0, LabConfig.DeveloperFields.Count).Single(i => LabConfig.DeveloperFields[i].Label == label)] = value;
        private static ushort AvailablePort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)socket.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var v6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            { v6.DualMode = false; v6.Bind(new IPEndPoint(IPAddress.IPv6Any, port)); }
                        return checked((ushort)port);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("Could not reserve a dual-family test port.");
        }
    }
}
