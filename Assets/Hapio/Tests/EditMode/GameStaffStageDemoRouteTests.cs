using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameStaffStageDemoRouteTests
    {
        private static readonly ulong[] Players = { 11, 22, 33 };

        [TestCase(1), TestCase(2), TestCase(3)]
        public void SelectedStageIsOneCanonicalRoundAndCompletesWithoutNextStage(int stage)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                var flow = new GameRunFlow();
                Assert.That(flow.EnterLobby(), Is.False, "No room may start without a stage selection.");
                Assert.That(flow.ConfigureDemoStage(stage), Is.True);
                Assert.That(flow.EnterLobby(), Is.True);
                GameFlowState lobby = flow.State;
                Assert.That(flow.BeginRun(Players), Is.True);
                GameFlowState loading = flow.State;
                Assert.That(loading.demoStageIndex, Is.EqualTo(stage));
                Assert.That(loading.stageIndex, Is.EqualTo(stage));
                Assert.That(loading.totalStages, Is.EqualTo(1));
                Assert.That(GameFlowSnapshot.TryAccept(lobby, loading, true, out string reason), Is.True, reason);
                Assert.That(flow.ConfigureDemoStage(stage % 3 + 1), Is.False, "Selection is fixed during a run.");
                GameFlowState playing = Play(flow);
                Assert.That(flow.MarkVictory("staff-round", 1), Is.True);
                GameFlowState complete = flow.State;
                Assert.That(complete.phase, Is.EqualTo(GameFlowPhase.Completed));
                Assert.That(GameFlowSnapshot.TryAccept(playing, complete, false, out reason), Is.True, reason);
                Assert.That(flow.NextStage(complete.runId, stage, complete.revision), Is.False);
                Assert.That(flow.DeveloperNextStage(complete.runId, stage, complete.revision), Is.False);
            }
        }

        [TestCase(1), TestCase(2), TestCase(3)]
        public void AnotherStageCannotReplaceAJoinedRoomAndRetryKeepsItsSelection(int stage)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                var flow = new GameRunFlow();
                flow.ConfigureDemoStage(stage); flow.EnterLobby(); flow.BeginRun(Players);
                GameFlowState playing = Play(flow);
                GameFlowState wrong = playing.Copy();
                wrong.demoStageIndex = wrong.stageIndex = stage % 3 + 1; wrong.revision++;
                Assert.That(GameFlowSnapshot.TryAccept(playing, wrong, false, out _), Is.False);
                Assert.That(flow.MarkDefeat("staff-round", 1, "Time ran out."), Is.True);
                GameFlowState failed = flow.State;
                Assert.That(flow.RetryFailedStage(failed.runId, stage, failed.revision), Is.True);
                Assert.That(flow.State.demoStageIndex, Is.EqualTo(stage));
                Assert.That(GameFlowSnapshot.IsDefeatRetryLoad(failed, flow.State), Is.True);
                wrong = flow.State; wrong.demoStageIndex = stage % 3 + 1;
                Assert.That(GameFlowSnapshot.IsDefeatRetryLoad(failed, wrong), Is.False);
            }
        }

        [Test]
        public void NormalGameRejectsDemoIdentityAndKeepsThreeStageRoute()
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(false))
            using (LabGameBuildVariant.OverrideForTests(false))
            {
                var flow = new GameRunFlow();
                Assert.That(flow.ConfigureDemoStage(2), Is.False);
                flow.EnterLobby(); flow.BeginRun(Players);
                Assert.That(flow.State.stageIndex, Is.EqualTo(1));
                Assert.That(flow.State.totalStages, Is.EqualTo(3));
                GameFlowState wrong = flow.State; wrong.demoStageIndex = 2;
                Assert.That(GameFlowSnapshot.IsValid(wrong, out _), Is.False);
                Assert.That(GameRunProfile.ForMode(GameMode.Territory).Count, Is.EqualTo(1));
            }
        }

        [TestCase(1), TestCase(2), TestCase(3)]
        public void InterruptedStaffRetryRetiresTheRunAndWaitsForThreeNewSeats(int stage)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                var flow = new GameRunFlow();
                flow.ConfigureDemoStage(stage); flow.EnterLobby(); flow.BeginRun(Players);
                Play(flow);
                Assert.That(flow.Abort(GameRunFlow.PlayerLeftReason), Is.True);
                GameFlowState interrupted = flow.State;
                Assert.That(GameRunFlow.IsStaffDemoInterrupted(interrupted), Is.True);
                Assert.That(flow.ReturnStaffDemoToLobby(interrupted.runId, stage, interrupted.revision, "Choose seats again."), Is.True);
                GameFlowState lobby = flow.State;
                Assert.That(lobby.phase, Is.EqualTo(GameFlowPhase.Lobby));
                Assert.That(lobby.mode, Is.EqualTo(GameMode.Story));
                Assert.That(lobby.demoStageIndex, Is.EqualTo(stage));
                Assert.That(lobby.stageIndex, Is.Zero);
                Assert.That(lobby.totalStages, Is.EqualTo(1));
                Assert.That(lobby.revision, Is.EqualTo(interrupted.revision + 1));
                Assert.That(lobby.runId, Is.Empty);
                Assert.That(lobby.battleSessionId, Is.Empty);
                Assert.That(lobby.battleRound, Is.Zero);
                Assert.That(lobby.battleStartsAt, Is.Zero);
                Assert.That(lobby.endReason, Is.Empty);
                Assert.That(lobby.retryableDefeat, Is.False);
                Assert.That(lobby.notice, Is.EqualTo("Choose seats again."));
                Assert.That(GameFlowSnapshot.IsValid(lobby, out string reason), Is.True, reason);
                Assert.That(flow.ParticipantCount, Is.Zero);
                Assert.That(flow.ReadyCount + flow.InitialStateReadyCount + flow.CountdownReadyCount, Is.Zero);
                Assert.That(flow.MarkBattleReady(Players[0], interrupted.runId, stage, interrupted.revision), Is.False);
                Assert.That(flow.ReturnStaffDemoToLobby(interrupted.runId, stage, interrupted.revision, ""), Is.False,
                    "A duplicate retry does not reset the reopened lobby again.");
                Assert.That(flow.BeginRun(new[] { Players[0], Players[1] }), Is.False);
                Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Lobby));
                Assert.That(flow.BeginRun(Players), Is.True, "The Host explicitly starts a new three-member run.");
                Assert.That(flow.State.runId, Is.Not.EqualTo(interrupted.runId));
                Assert.That(flow.State.stageIndex, Is.EqualTo(stage));
            }
        }

        [TestCase(GameRunFlow.PlayerLeftReason)]
        [TestCase(GameRunFlow.PlayerDisconnectedReason)]
        [TestCase(GameRunFlow.HostDisconnectedReason)]
        public void StaffRetryRequiresCurrentInterruptedGeneration(string interruption)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                var flow = new GameRunFlow();
                flow.ConfigureDemoStage(2); flow.EnterLobby(); flow.BeginRun(Players);
                GameFlowState playing = Play(flow);
                Assert.That(flow.ReturnStaffDemoToLobby(playing.runId, 2, playing.revision, ""), Is.False);
                flow.Abort(interruption);
                GameFlowState failed = flow.State;
                Assert.That(flow.ReturnStaffDemoToLobby("another run", 2, failed.revision, ""), Is.False);
                Assert.That(flow.ReturnStaffDemoToLobby(failed.runId, 1, failed.revision, ""), Is.False);
                Assert.That(flow.ReturnStaffDemoToLobby(failed.runId, 2, failed.revision - 1, ""), Is.False);
                Assert.That(flow.ReturnStaffDemoToLobby(failed.runId, 2, failed.revision + 1, ""), Is.False);
                Assert.That(flow.ReturnStaffDemoToLobby(failed.runId, 2, failed.revision, null), Is.False);
                Assert.That(flow.ReturnStaffDemoToLobby(failed.runId, 2, failed.revision, new string('n', 129)), Is.False);
                Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
                Assert.That(flow.State.revision, Is.EqualTo(failed.revision));
                Assert.That(flow.State.runId, Is.EqualTo(failed.runId));
                Assert.That(flow.ReturnStaffDemoToLobby(failed.runId, 2, failed.revision, ""), Is.True);
            }
        }

        [Test]
        public void StaffRetryDoesNotReplaceNormalDefeatOrOtherFailureHandling()
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                var flow = new GameRunFlow();
                flow.ConfigureDemoStage(3); flow.EnterLobby(); flow.BeginRun(Players);
                Play(flow);
                Assert.That(flow.MarkDefeat("staff-round", 1, "Time ran out."), Is.True);
                GameFlowState defeat = flow.State;
                Assert.That(GameRunFlow.IsStaffDemoInterrupted(defeat), Is.False);
                Assert.That(flow.ReturnStaffDemoToLobby(defeat.runId, 3, defeat.revision, ""), Is.False);
                Assert.That(flow.RetryFailedStage(defeat.runId, 3, defeat.revision), Is.True,
                    "Normal defeat retains its existing direct stage retry.");
                Assert.That(flow.Abort("The stage could not be loaded."), Is.True);
                GameFlowState loadingFailure = flow.State;
                Assert.That(flow.ReturnStaffDemoToLobby(loadingFailure.runId, 3, loadingFailure.revision, ""), Is.False);
            }
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(false))
            using (LabGameBuildVariant.OverrideForTests(false))
            {
                var normal = new GameRunFlow();
                normal.EnterLobby(); normal.BeginRun(Players); Play(normal);
                normal.Abort(GameRunFlow.PlayerLeftReason);
                GameFlowState failed = normal.State;
                Assert.That(GameRunFlow.IsStaffDemoInterrupted(failed), Is.False);
                Assert.That(normal.ReturnStaffDemoToLobby(failed.runId, failed.stageIndex, failed.revision, ""), Is.False);
            }
        }

        private static GameFlowState Play(GameRunFlow flow)
        {
            GameFlowState loading = flow.State;
            foreach (ulong id in Players) Assert.That(flow.MarkBattleReady(id, loading.runId, loading.stageIndex, loading.revision), Is.True);
            Assert.That(flow.MarkSynchronizing(loading.runId, loading.stageIndex, loading.revision, "staff-round", 1), Is.True);
            GameFlowState syncing = flow.State;
            foreach (ulong id in Players) Assert.That(flow.MarkInitialStateReady(id, syncing.runId, syncing.stageIndex, syncing.revision), Is.True);
            Assert.That(flow.BeginCountdown(syncing.runId, syncing.stageIndex, syncing.revision, 10d), Is.True);
            GameFlowState countdown = flow.State;
            foreach (ulong id in Players) Assert.That(flow.MarkCountdownReady(id, countdown.runId, countdown.stageIndex, countdown.revision), Is.True);
            Assert.That(flow.MarkPlaying(countdown.runId, countdown.stageIndex, countdown.revision, "staff-round", 1), Is.True);
            return flow.State;
        }
    }
}
