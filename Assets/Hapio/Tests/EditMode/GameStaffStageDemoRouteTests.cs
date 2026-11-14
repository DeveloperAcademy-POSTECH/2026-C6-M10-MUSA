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
