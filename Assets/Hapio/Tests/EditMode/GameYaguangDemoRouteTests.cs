using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameYaguangDemoRouteTests
    {
        private static readonly ulong[] Players = { 11, 22, 33 };

        [Test]
        public void DemoUsesOnlyTheOriginalSecondStageWhileNormalAndTerritoryKeepTheirRoutes()
        {
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                GameRunProfile demo = GameRunProfile.ForMode(GameMode.Story);
                Assert.That(demo, Is.SameAs(GameRunProfile.YaguangDemo));
                Assert.That(demo.FirstStage, Is.EqualTo(2));
                Assert.That(demo.FinalStage, Is.EqualTo(2));
                Assert.That(demo.Count, Is.EqualTo(1));
                Assert.That(demo.Position(2), Is.EqualTo(1));
                Assert.That(demo.Contains(1), Is.False);
                Assert.That(demo.Contains(3), Is.False);
                Assert.That(demo.Next(2), Is.Zero);
                Assert.That(GameRunProfile.ForMode(GameMode.Territory), Is.SameAs(GameRunProfile.Territory));
                Assert.That(new GameFlowState().totalStages, Is.EqualTo(1));
            }
            using (LabGameBuildVariant.OverrideForTests(false))
            {
                GameRunProfile normal = GameRunProfile.ForMode(GameMode.Story);
                Assert.That(normal, Is.SameAs(GameRunProfile.NormalStory));
                Assert.That(normal.Count, Is.EqualTo(3));
                Assert.That(normal.FirstStage, Is.EqualTo(1));
                Assert.That(normal.Next(1), Is.EqualTo(2));
                Assert.That(normal.Next(2), Is.EqualTo(3));
                Assert.That(normal.FinalStage, Is.EqualTo(3));
                Assert.That(GameRunProfile.Territory.Count, Is.EqualTo(1));
                Assert.That(GameRunProfile.Territory.FirstStage, Is.EqualTo(1));
                Assert.That(GameRunProfile.Territory.Next(1), Is.Zero);
                Assert.That(new GameFlowState().totalStages, Is.EqualTo(3));
            }
        }

        [Test]
        public void DemoRunsTheFullThreePlayerStartBarrierAtCanonicalStageTwoThenCompletes()
        {
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                GameRunFlow flow = Begin();
                GameFlowState loading = flow.State;
                Assert.That(loading.stageIndex, Is.EqualTo(2));
                Assert.That(loading.totalStages, Is.EqualTo(1));
                Assert.That(flow.DeveloperStartAt(1), Is.False);
                Assert.That(flow.DeveloperStartAt(3), Is.False);
                Assert.That(flow.MarkBattleReady(99, loading.runId, 2, loading.revision), Is.False);
                GameFlowState playing = Play(flow, "demo-battle", 7);
                Assert.That(flow.MarkVictory(playing.battleSessionId, playing.battleRound), Is.True);
                GameFlowState completed = flow.State;
                Assert.That(completed.phase, Is.EqualTo(GameFlowPhase.Completed));
                Assert.That(completed.stageIndex, Is.EqualTo(2));
                Assert.That(GameFlowSnapshot.IsValid(completed, out string reason), Is.True, reason);
                Assert.That(flow.NextStage(completed.runId, 2, completed.revision), Is.False);
                Assert.That(flow.DeveloperNextStage(completed.runId, 2, completed.revision), Is.False);
            }
        }

        [Test]
        public void DemoSnapshotAcceptsStageTwoFirstAndRejectsNormalCountsOtherStagesAndIntermediateResult()
        {
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                GameRunFlow flow = Begin();
                var lobby = new GameFlowState { phase = GameFlowPhase.Lobby };
                GameFlowState loading = flow.State;
                Assert.That(GameFlowSnapshot.TryAccept(lobby, loading, true, out string reason), Is.True, reason);
                GameFlowState wrong = loading.Copy();
                wrong.totalStages = 3;
                Assert.That(GameFlowSnapshot.IsValid(wrong, out _), Is.False);
                foreach (int stage in new[] { 1, 3 })
                {
                    wrong = loading.Copy(); wrong.stageIndex = stage;
                    Assert.That(GameFlowSnapshot.TryAccept(lobby, wrong, true, out _), Is.False);
                }
                GameFlowState playing = Play(flow, "demo-battle", 3);
                wrong = playing.Copy(); wrong.phase = GameFlowPhase.StageResult; wrong.revision++;
                Assert.That(GameFlowSnapshot.IsValid(wrong, out _), Is.False);
                Assert.That(flow.MarkVictory(playing.battleSessionId, playing.battleRound), Is.True);
                Assert.That(GameFlowSnapshot.TryAccept(playing, flow.State, false, out reason), Is.True, reason);
            }
        }

        [Test]
        public void DemoDefeatRetryKeepsStageRosterAndRunWhileFencingOldPreparation()
        {
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                GameRunFlow flow = Begin();
                GameFlowState playing = Play(flow, "failed-demo-battle", 4);
                Assert.That(flow.MarkDefeat(playing.battleSessionId, playing.battleRound, "Time ran out."), Is.True);
                GameFlowState failed = flow.State;
                Assert.That(flow.RetryFailedStage(failed.runId, failed.stageIndex, failed.revision), Is.True);
                GameFlowState retry = flow.State;
                Assert.That(retry.stageIndex, Is.EqualTo(2));
                Assert.That(retry.totalStages, Is.EqualTo(1));
                Assert.That(retry.runId, Is.EqualTo(failed.runId));
                Assert.That(flow.ParticipantIds, Is.EquivalentTo(Players));
                Assert.That(retry.battleSessionId, Is.Empty);
                Assert.That(retry.battleRound, Is.Zero);
                Assert.That(retry.retryableDefeat, Is.False);
                Assert.That(GameFlowSnapshot.TryAccept(failed, retry, false, out string reason), Is.True, reason);
                Assert.That(flow.MarkBattleReady(Players[0], playing.runId, 2, playing.revision), Is.False);
                Assert.That(flow.RetryFailedStage(failed.runId, failed.stageIndex, failed.revision), Is.False);
                GameFlowState wrongRoute = retry.Copy(); wrongRoute.totalStages = 3;
                Assert.That(GameFlowSnapshot.IsDefeatRetryLoad(failed, wrongRoute), Is.False);
                GameFlowState again = Play(flow, "new-demo-battle", 5);
                Assert.That(again.stageIndex, Is.EqualTo(2));
                Assert.That(flow.MarkVictory(again.battleSessionId, again.battleRound), Is.True);
                Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Completed));
            }
        }

        [Test]
        public void NormalThreeStageAndTerritoryRunsStillFinishAtTheirOwnFinalStages()
        {
            using (LabGameBuildVariant.OverrideForTests(false))
            {
                GameRunFlow flow = Begin();
                for (int stage = 1; stage <= 3; stage++)
                {
                    Assert.That(flow.State.stageIndex, Is.EqualTo(stage));
                    GameFlowState playing = Play(flow, "normal-battle-" + stage, (uint)stage);
                    Assert.That(flow.MarkVictory(playing.battleSessionId, playing.battleRound), Is.True);
                    GameFlowState result = flow.State;
                    Assert.That(result.phase, Is.EqualTo(stage == 3 ? GameFlowPhase.Completed : GameFlowPhase.StageResult));
                    if (stage < 3) Assert.That(flow.NextStage(result.runId, stage, result.revision), Is.True);
                }
                var territory = new GameRunFlow();
                Assert.That(territory.EnterLobby(GameMode.Territory), Is.True);
                Assert.That(territory.BeginRun(new ulong[] { 1, 2, 3, 4 }), Is.True);
                GameFlowState teamPlaying = Play(territory, "territory-battle", 1);
                Assert.That(territory.MarkVictory(teamPlaying.battleSessionId, teamPlaying.battleRound), Is.True);
                Assert.That(territory.State.phase, Is.EqualTo(GameFlowPhase.Completed));
                Assert.That(territory.State.stageIndex, Is.EqualTo(1));
            }
        }

        private static GameRunFlow Begin()
        {
            var flow = new GameRunFlow();
            Assert.That(flow.EnterLobby(), Is.True);
            Assert.That(flow.BeginRun(Players), Is.True);
            return flow;
        }

        private static GameFlowState Play(GameRunFlow flow, string battle, uint round)
        {
            GameFlowState load = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                Assert.That(flow.MarkBattleReady(id, load.runId, load.stageIndex, load.revision), Is.True);
            Assert.That(flow.MarkSynchronizing(load.runId, load.stageIndex, load.revision, battle, round), Is.True);
            GameFlowState sync = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                Assert.That(flow.MarkInitialStateReady(id, sync.runId, sync.stageIndex, sync.revision), Is.True);
            Assert.That(flow.BeginCountdown(sync.runId, sync.stageIndex, sync.revision, 103.5d), Is.True);
            GameFlowState countdown = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                Assert.That(flow.MarkCountdownReady(id, countdown.runId, countdown.stageIndex, countdown.revision), Is.True);
            Assert.That(flow.MarkPlaying(countdown.runId, countdown.stageIndex, countdown.revision, battle, round), Is.True);
            return flow.State;
        }
    }
}
