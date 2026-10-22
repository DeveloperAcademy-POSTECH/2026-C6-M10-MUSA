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
        public void DeveloperNextLoadsOneStageWithoutVictoryAndRequiresFreshParticipantAcks()
        {
            var flow = Run(0, 42);
            var firstLoad = flow.State;
            var first = Play(flow, "first", 11);
            Assert.That(flow.NextStage(first.runId, 1, first.revision), Is.False,
                "The ordinary result-only action must remain unavailable during battle.");
            Assert.That(flow.DeveloperNextStage(first.runId, 1, first.revision), Is.True);
            var loading = flow.State;
            Assert.That(loading.runId, Is.EqualTo(first.runId));
            Assert.That(loading.stageIndex, Is.EqualTo(2));
            Assert.That(loading.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
            Assert.That(loading.revision, Is.GreaterThan(first.revision));
            Assert.That(loading.battleSessionId, Is.Empty);
            Assert.That(loading.battleRound, Is.Zero);
            Assert.That(flow.ParticipantIds, Is.EquivalentTo(new ulong[] { 0, 42 }));
            Assert.That(flow.ReadyCount, Is.Zero);
            Assert.That(GameFlowSnapshot.TryAccept(first, loading, false, out _), Is.True);
            Assert.That(flow.DeveloperNextStage(first.runId, 1, first.revision), Is.False);
            Assert.That(flow.MarkBattleReady(42, firstLoad.runId, 1, firstLoad.revision), Is.False);
            Assert.That(flow.MarkVictory(first.battleSessionId, first.battleRound), Is.False);
            Assert.That(flow.MarkBattleReady(0, loading.runId, 2, loading.revision), Is.True);
            Assert.That(flow.MarkPlaying(loading.runId, 2, loading.revision, "second", 12), Is.False);
            Assert.That(flow.MarkBattleReady(42, loading.runId, 2, loading.revision), Is.True);
            Assert.That(flow.MarkPlaying(loading.runId, 2, loading.revision, "second", 12), Is.True);
            var second = flow.State;
            Assert.That(flow.DeveloperNextStage(second.runId, 2, second.revision), Is.True);
            var third = Play(flow, "third", 13);
            Assert.That(flow.DeveloperNextStage(third.runId, 3, third.revision), Is.False);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Playing));
            Assert.That(flow.State.revision, Is.EqualTo(third.revision));
            Assert.That(flow.MarkVictory(third.battleSessionId, third.battleRound), Is.True);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Completed));
        }

        [Test]
        public void DeveloperNextRejectsWrongRunStageAndRevisionWithoutChangingBattle()
        {
            var flow = Run(0);
            var playing = Play(flow, "battle");
            Assert.That(flow.DeveloperNextStage("another-run", 1, playing.revision), Is.False);
            Assert.That(flow.DeveloperNextStage(playing.runId, 2, playing.revision), Is.False);
            Assert.That(flow.DeveloperNextStage(playing.runId, 1, playing.revision - 1), Is.False);
            Assert.That(flow.DeveloperNextStage(playing.runId, 1, playing.revision + 1), Is.False);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Playing));
            Assert.That(flow.State.stageIndex, Is.EqualTo(1));
            Assert.That(flow.State.revision, Is.EqualTo(playing.revision));
            Assert.That(flow.State.battleSessionId, Is.EqualTo(playing.battleSessionId));
        }

        [TestCase(GameFlowPhase.Home)]
        [TestCase(GameFlowPhase.Lobby)]
        [TestCase(GameFlowPhase.LoadingBattle)]
        [TestCase(GameFlowPhase.StageResult)]
        [TestCase(GameFlowPhase.Completed)]
        [TestCase(GameFlowPhase.GameOver)]
        public void DeveloperNextRejectsEveryNonPlayingPhase(GameFlowPhase phase)
        {
            var flow = new GameRunFlow();
            if (phase != GameFlowPhase.Home) Assert.That(flow.EnterLobby(), Is.True);
            if (phase != GameFlowPhase.Home && phase != GameFlowPhase.Lobby)
                Assert.That(flow.BeginRun(new ulong[] { 0 }), Is.True);
            if (phase == GameFlowPhase.StageResult || phase == GameFlowPhase.Completed)
            {
                int count = phase == GameFlowPhase.Completed ? 3 : 1;
                for (int stage = 1; stage <= count; stage++)
                {
                    var playing = Play(flow, "battle-" + stage);
                    Assert.That(flow.MarkVictory(playing.battleSessionId, playing.battleRound), Is.True);
                    var result = flow.State;
                    if (stage < count)
                        Assert.That(flow.NextStage(result.runId, result.stageIndex, result.revision), Is.True);
                }
            }
            if (phase == GameFlowPhase.GameOver) Assert.That(flow.Abort("disconnected"), Is.True);
            var before = flow.State;
            Assert.That(before.phase, Is.EqualTo(phase));
            Assert.That(flow.DeveloperNextStage(before.runId, before.stageIndex, before.revision), Is.False);
            Assert.That(flow.State.phase, Is.EqualTo(before.phase));
            Assert.That(flow.State.stageIndex, Is.EqualTo(before.stageIndex));
            Assert.That(flow.State.revision, Is.EqualTo(before.revision));
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
