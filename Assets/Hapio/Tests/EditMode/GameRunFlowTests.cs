using System;
using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameRunFlowTests
    {
        private static GameRunFlow Run(params ulong[] ids)
        {
            var flow = new GameRunFlow();
            Assert.That(flow.EnterLobby(), Is.True);
            Assert.That(flow.BeginRun(ids), Is.True);
            return flow;
        }

        private static GameFlowState Play(GameRunFlow flow, string session, uint round = 1)
        {
            var loading = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                Assert.That(flow.MarkBattleReady(id, loading.runId, loading.stageIndex, loading.revision), Is.True);
            Assert.That(flow.IsBattleReady, Is.True);
            Assert.That(flow.MarkPlaying(loading.runId, loading.stageIndex, loading.revision, session, round), Is.True);
            return flow.State;
        }

        [Test]
        public void ThreeStagesUseOneRunAndDistinctBattleIdentitiesThenComplete()
        {
            var flow = Run(0, 42);
            string runId = flow.State.runId;
            for (int stage = 1; stage <= 3; stage++)
            {
                Assert.That(flow.State.stageIndex, Is.EqualTo(stage));
                Assert.That(flow.State.runId, Is.EqualTo(runId));
                Assert.That(flow.State.totalStages, Is.EqualTo(3));
                var playing = Play(flow, "battle-" + stage, (uint)(stage + 10));
                Assert.That(playing.battleRound, Is.Not.EqualTo((uint)stage),
                    "Battle model rounds are not the game stage number.");
                Assert.That(flow.MarkVictory(playing.battleSessionId, playing.battleRound), Is.True);
                var result = flow.State;
                Assert.That(result.phase, Is.EqualTo(stage == 3 ? GameFlowPhase.Completed : GameFlowPhase.StageResult));
                if (stage < 3)
                    Assert.That(flow.NextStage(result.runId, result.stageIndex, result.revision), Is.True);
            }
            var completed = flow.State;
            Assert.That(flow.NextStage(completed.runId, completed.stageIndex, completed.revision), Is.False);
            Assert.That(flow.Abort("Host left"), Is.False);
            Assert.That(flow.ReturnHome(), Is.True);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Home));
            Assert.That(flow.State.endReason, Is.Empty);
            Assert.That(flow.ParticipantIds, Is.Empty);
        }

        [Test]
        public void BarrierRequiresEveryActualParticipantWithoutChangingGenerationOnAck()
        {
            var flow = Run(0, 42);
            var load = flow.State;
            Assert.That(flow.MarkBattleReady(0, load.runId, 1, load.revision), Is.True);
            Assert.That(flow.State.revision, Is.EqualTo(load.revision));
            Assert.That(flow.MarkBattleReady(0, load.runId, 1, load.revision), Is.False);
            Assert.That(flow.MarkBattleReady(99, load.runId, 1, load.revision), Is.False);
            Assert.That(flow.ReadyCount, Is.EqualTo(1));
            Assert.That(flow.MarkPlaying(load.runId, 1, load.revision, "battle", 1), Is.False);
            Assert.That(flow.MarkBattleReady(42, load.runId, 1, load.revision), Is.True);
            Assert.That(flow.MarkPlaying(load.runId, 1, load.revision, "battle", 1), Is.True);
            Assert.That(flow.MarkPlaying(load.runId, 1, load.revision, "battle", 1), Is.False);
        }

        [Test]
        public void DuplicateNextAndPreviousStageAckCannotAdvanceOrReleaseNewBarrier()
        {
            var flow = Run(0, 9);
            var originalLoad = flow.State;
            var playing = Play(flow, "first");
            Assert.That(flow.MarkVictory(playing.battleSessionId, playing.battleRound), Is.True);
            var result = flow.State;
            Assert.That(flow.NextStage(result.runId, result.stageIndex, result.revision), Is.True);
            Assert.That(flow.NextStage(result.runId, result.stageIndex, result.revision), Is.False);
            Assert.That(flow.MarkBattleReady(9, originalLoad.runId, 1, originalLoad.revision), Is.False);
            Assert.That(flow.State.stageIndex, Is.EqualTo(2));
            Assert.That(flow.ReadyCount, Is.Zero);
            Assert.That(flow.State.battleSessionId, Is.Empty);
            Assert.That(flow.State.battleRound, Is.Zero);
        }

        [Test]
        public void DeveloperRestartKeepsStageButInvalidatesAllPreviousBattleAndLoadTokens()
        {
            var flow = Run(0);
            var firstLoad = flow.State;
            var playing = Play(flow, "before", 47);
            Assert.That(flow.RestartCurrentStage(playing.runId, 1, playing.revision), Is.True);
            var restarted = flow.State;
            Assert.That(restarted.stageIndex, Is.EqualTo(1));
            Assert.That(restarted.runId, Is.EqualTo(playing.runId));
            Assert.That(restarted.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
            Assert.That(restarted.revision, Is.GreaterThan(playing.revision));
            Assert.That(flow.MarkBattleReady(0, firstLoad.runId, 1, firstLoad.revision), Is.False);
            Assert.That(flow.MarkVictory("before", 47), Is.False);
            Play(flow, "after", 48);
            Assert.That(flow.MarkVictory("before", 47), Is.False);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Playing));
        }

        [Test]
        public void OldRunAckAndStartCannotMutateAnotherRun()
        {
            var flow = Run(0);
            var old = flow.State;
            Assert.That(flow.Abort("disconnected"), Is.True);
            Assert.That(flow.ReturnHome(), Is.True);
            Assert.That(flow.BeginRun(new ulong[] { 0 }), Is.True);
            Assert.That(flow.State.runId, Is.Not.EqualTo(old.runId));
            Assert.That(flow.MarkBattleReady(0, old.runId, old.stageIndex, old.revision), Is.False);
            Assert.That(flow.MarkPlaying(old.runId, old.stageIndex, old.revision, "stale", 1), Is.False);
        }

        [TestCase(GameFlowPhase.LoadingBattle)]
        [TestCase(GameFlowPhase.Playing)]
        [TestCase(GameFlowPhase.StageResult)]
        public void AbortLatchesFirstReasonAcrossDisconnectAndRepeatedEvents(GameFlowPhase phase)
        {
            var flow = Run(7);
            if (phase != GameFlowPhase.LoadingBattle)
            {
                var playing = Play(flow, "battle");
                if (phase == GameFlowPhase.StageResult) flow.MarkVictory(playing.battleSessionId, playing.battleRound);
            }
            Assert.That(flow.Abort("participant left"), Is.True);
            uint revision = flow.State.revision;
            Assert.That(flow.Abort("transport closed"), Is.False);
            Assert.That(flow.State.endReason, Is.EqualTo("participant left"));
            Assert.That(flow.State.revision, Is.EqualTo(revision));
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
        }

        [Test]
        public void LobbyLeaveAndInvalidParticipantListsDoNotCreateGameOverOrPartialRun()
        {
            var flow = new GameRunFlow();
            Assert.That(flow.BeginRun(null), Is.False);
            Assert.That(flow.BeginRun(Array.Empty<ulong>()), Is.False);
            Assert.That(flow.BeginRun(new ulong[] { 1, 1 }), Is.False);
            Assert.That(flow.ParticipantIds, Is.Empty);
            Assert.That(flow.EnterLobby(), Is.True);
            Assert.That(flow.Abort("leave"), Is.False);
            Assert.That(flow.ReturnHome(), Is.True);
            Assert.That(flow.State.endReason, Is.Empty);
        }

        [Test]
        public void SnapshotsAndParticipantListsCannotMutateAuthorityByReference()
        {
            var flow = Run(3);
            var snapshot = flow.State;
            snapshot.stageIndex = 3;
            snapshot.phase = GameFlowPhase.Completed;
            Assert.That(flow.State.stageIndex, Is.EqualTo(1));
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
            Assert.That(flow.BeginRun(new ulong[] { 5 }), Is.False);
            Assert.That(flow.ParticipantIds, Is.EquivalentTo(new ulong[] { 3 }));
        }

        [Test]
        public void AuthorityCannotProduceOversizedNetworkTextFields()
        {
            var flow = Run(0);
            var load = flow.State;
            Assert.That(flow.Abort(new string('x', 257)), Is.False);
            Assert.That(flow.MarkBattleReady(0, load.runId, 1, load.revision), Is.True);
            Assert.That(flow.MarkPlaying(load.runId, 1, load.revision, new string('b', 129), 1), Is.False);
            Assert.That(GameFlowSnapshot.IsValid(flow.State, out _), Is.True);
        }
    }
}
