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
            return FinishPreparation(flow, session, round);
        }

        private static GameFlowState FinishPreparation(GameRunFlow flow, string session, uint round = 1)
        {
            var load = flow.State;
            Assert.That(flow.MarkSynchronizing(load.runId, load.stageIndex, load.revision, session, round), Is.True);
            var sync = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                Assert.That(flow.MarkInitialStateReady(id, sync.runId, sync.stageIndex, sync.revision), Is.True);
            Assert.That(flow.BeginCountdown(sync.runId, sync.stageIndex, sync.revision, 103.5d), Is.True);
            var countdown = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                Assert.That(flow.MarkCountdownReady(id, countdown.runId, countdown.stageIndex, countdown.revision), Is.True);
            Assert.That(flow.MarkPlaying(countdown.runId, countdown.stageIndex, countdown.revision, session, round), Is.True);
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
            Assert.That(flow.MarkPlaying(load.runId, 1, load.revision, "battle", 1), Is.False,
                "Scene readiness alone never bypasses initial state and countdown delivery.");
            FinishPreparation(flow, "battle");
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
            var second = FinishPreparation(flow, "second", 12);
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

        private static GameFlowState Synchronize(GameRunFlow flow)
        {
            var loading = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                flow.MarkBattleReady(id, loading.runId, loading.stageIndex, loading.revision);
            Assert.That(flow.MarkSynchronizing(loading.runId, loading.stageIndex, loading.revision, "prepared", 7), Is.True);
            return flow.State;
        }

        private static GameFlowState Schedule(GameRunFlow flow)
        {
            var sync = Synchronize(flow);
            foreach (ulong id in flow.ParticipantIds)
                flow.MarkInitialStateReady(id, sync.runId, sync.stageIndex, sync.revision);
            Assert.That(flow.BeginCountdown(sync.runId, sync.stageIndex, sync.revision, 103.5d), Is.True);
            return flow.State;
        }

        [Test]
        public void InitialStateBarrierNeedsEveryMemberIncludingHostAndRejectsOldOrForeignAcks()
        {
            var flow = Run(0, 42);
            var load = flow.State;
            Assert.That(flow.MarkSynchronizing(load.runId, 1, load.revision, "b", 7), Is.False);
            var sync = Synchronize(flow);
            Assert.That(flow.ParticipantCount, Is.EqualTo(2));
            Assert.That(flow.MarkInitialStateReady(42, load.runId, 1, load.revision), Is.False);
            Assert.That(flow.MarkInitialStateReady(999, sync.runId, 1, sync.revision), Is.False);
            Assert.That(flow.MarkInitialStateReady(42, sync.runId, 1, sync.revision), Is.True);
            Assert.That(flow.MarkInitialStateReady(42, sync.runId, 1, sync.revision), Is.False);
            Assert.That(flow.InitialStateReadyCount, Is.EqualTo(1));
            Assert.That(flow.State.revision, Is.EqualTo(sync.revision), "Individual ACKs do not invalidate other members' requests.");
            Assert.That(flow.IsInitialStateReady, Is.False, "The Host must confirm its own initial state too.");
            Assert.That(flow.BeginCountdown(sync.runId, 1, sync.revision, 103.5d), Is.False);
            Assert.That(flow.MarkInitialStateReady(0, sync.runId, 1, sync.revision), Is.True);
            Assert.That(flow.BeginCountdown(sync.runId, 1, sync.revision, 103.5d), Is.True);
            Assert.That(flow.MarkInitialStateReady(0, sync.runId, 1, sync.revision), Is.False);
        }

        [Test]
        public void CountdownDeliveryAndRescheduleCannotBeReleasedByAnEarlierSchedule()
        {
            var flow = Run(0, 42);
            var countdown = Schedule(flow);
            Assert.That(flow.MarkPlaying(countdown.runId, 1, countdown.revision, "prepared", 7), Is.False);
            Assert.That(flow.MarkCountdownReady(99, countdown.runId, 1, countdown.revision), Is.False);
            Assert.That(flow.MarkCountdownReady(42, countdown.runId, 1, countdown.revision), Is.True);
            Assert.That(flow.MarkCountdownReady(42, countdown.runId, 1, countdown.revision), Is.False);
            Assert.That(flow.CountdownReadyCount, Is.EqualTo(1));
            Assert.That(flow.MarkPlaying(countdown.runId, 1, countdown.revision, "prepared", 7), Is.False);
            Assert.That(flow.RescheduleCountdown(countdown.runId, 1, countdown.revision, 103.5d), Is.False);
            Assert.That(flow.RescheduleCountdown(countdown.runId, 1, countdown.revision, 102d), Is.False);
            Assert.That(flow.RescheduleCountdown(countdown.runId, 1, countdown.revision, 107d), Is.True);
            var delayed = flow.State;
            Assert.That(delayed.battleStartsAt, Is.EqualTo(107d));
            Assert.That(flow.CountdownReadyCount, Is.Zero);
            Assert.That(flow.MarkCountdownReady(0, countdown.runId, 1, countdown.revision), Is.False);
            Assert.That(flow.MarkCountdownReady(0, delayed.runId, 1, delayed.revision), Is.True);
            Assert.That(flow.MarkCountdownReady(42, delayed.runId, 1, delayed.revision), Is.True);
            Assert.That(flow.MarkPlaying(delayed.runId, 1, delayed.revision, "another", 7), Is.False);
            Assert.That(flow.MarkPlaying(delayed.runId, 1, delayed.revision, "prepared", 8), Is.False);
            Assert.That(flow.MarkPlaying(delayed.runId, 1, delayed.revision, "prepared", 7), Is.True);
            Assert.That(flow.MarkPlaying(delayed.runId, 1, delayed.revision, "prepared", 7), Is.False);
            var playing = flow.State;
            Assert.That(playing.battleStartsAt, Is.EqualTo(107d));
            Assert.That(flow.RestartCurrentStage(playing.runId, 1, playing.revision), Is.True);
            Assert.That(flow.State.battleStartsAt, Is.Zero);
            Assert.That(flow.ReadyCount + flow.InitialStateReadyCount + flow.CountdownReadyCount, Is.Zero);
            Assert.That(flow.MarkCountdownReady(0, delayed.runId, 1, delayed.revision), Is.False);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(1e13d)]
        public void InvalidDeadlineDoesNotMutatePreparedState(double invalid)
        {
            var flow = Run(0);
            var sync = Synchronize(flow);
            flow.MarkInitialStateReady(0, sync.runId, 1, sync.revision);
            Assert.That(flow.BeginCountdown(sync.runId, 1, sync.revision, invalid), Is.False);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.SynchronizingBattle));
            Assert.That(flow.State.revision, Is.EqualTo(sync.revision));
            Assert.That(flow.State.battleStartsAt, Is.Zero);
        }

        [TestCase(GameFlowPhase.SynchronizingBattle)]
        [TestCase(GameFlowPhase.Countdown)]
        public void DisconnectDuringPreparationIsActiveAndClearsEveryReadinessSet(GameFlowPhase phase)
        {
            var flow = Run(0, 42);
            if (phase == GameFlowPhase.SynchronizingBattle) Synchronize(flow); else Schedule(flow);
            Assert.That(flow.State.IsRunActive, Is.True);
            Assert.That(flow.Abort("participant left while preparing"), Is.True);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            Assert.That(flow.ReadyCount + flow.InitialStateReadyCount + flow.CountdownReadyCount, Is.Zero);
            Assert.That(flow.IsInitialStateReady || flow.IsCountdownReady, Is.False);
        }

        [Test]
        public void AppendedPreparationPhasesKeepAllExistingSerializedNumbers()
        {
            Assert.That((int)GameFlowPhase.Home, Is.EqualTo(0));
            Assert.That((int)GameFlowPhase.LoadingBattle, Is.EqualTo(2));
            Assert.That((int)GameFlowPhase.Playing, Is.EqualTo(3));
            Assert.That((int)GameFlowPhase.GameOver, Is.EqualTo(6));
            Assert.That((int)GameFlowPhase.SynchronizingBattle, Is.EqualTo(7));
            Assert.That((int)GameFlowPhase.Countdown, Is.EqualTo(8));
        }

        [Test]
        public void AuthorityCannotProduceOversizedNetworkTextFields()
        {
            var flow = Run(0);
            var load = flow.State;
            Assert.That(flow.Abort(new string('x', 257)), Is.False);
            Assert.That(flow.MarkBattleReady(0, load.runId, 1, load.revision), Is.True);
            Assert.That(flow.MarkSynchronizing(load.runId, 1, load.revision, new string('b', 129), 1), Is.False);
            Assert.That(GameFlowSnapshot.IsValid(flow.State, out _), Is.True);
        }
    }
}
