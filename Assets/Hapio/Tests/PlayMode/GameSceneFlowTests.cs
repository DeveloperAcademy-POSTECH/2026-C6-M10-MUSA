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
