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

        [Test]
        public void DeveloperStartAtOnlyMovesAFreshlyLoadingRunWithinItsStages()
        {
            // #479 지귀 실험실: 에디터 SOLO가 3스테이지부터 시작할 수 있고, 1이면 아무것도 바꾸지 않는다.
            var flow = new GameRunFlow();
            Assert.That(flow.EnterLobby(), Is.True);
            Assert.That(flow.DeveloperStartAt(3), Is.False, "Only a run that is already loading can move.");
            Assert.That(flow.BeginRun(new ulong[] { 0 }), Is.True);
            int total = flow.State.totalStages;
            Assert.That(flow.DeveloperStartAt(1), Is.False);
            Assert.That(flow.DeveloperStartAt(total + 1), Is.False);
            Assert.That(flow.State.stageIndex, Is.EqualTo(1));
            Assert.That(flow.DeveloperStartAt(3), Is.True);
            Assert.That(flow.State.stageIndex, Is.EqualTo(3));
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
            Assert.That(flow.DeveloperStartAt(2), Is.False, "It moves only once, from stage 1.");
            Assert.That(GameFlowSnapshot.IsValid(flow.State, out _), Is.True);
        }
        [Test]
        public void NormalDefeatRetriesOnlyTheFailedStageWithFreshBarriersAndSameRoster()
        {
            var flow = Run(0, 42);
            var first = Play(flow, "battle", 11);
            Assert.That(flow.MarkVictory(first.battleSessionId, first.battleRound), Is.True);
            var won = flow.State;
            Assert.That(flow.NextStage(won.runId, won.stageIndex, won.revision), Is.True);
            var second = Play(flow, "battle", 12);
            Assert.That(flow.MarkDefeat("other", second.battleRound, "Time ran out."), Is.False);
            Assert.That(flow.MarkDefeat(second.battleSessionId, second.battleRound + 1, "Time ran out."), Is.False);
            Assert.That(flow.MarkDefeat(second.battleSessionId, second.battleRound, "Time ran out."), Is.True);
            var failed = flow.State;
            Assert.That(failed.retryableDefeat, Is.True);
            Assert.That(GameFlowSnapshot.IsValid(failed, out _), Is.True);
            Assert.That(flow.MarkDefeat(second.battleSessionId, second.battleRound, "again"), Is.False);
            Assert.That(flow.RetryFailedStage("old-run", 2, failed.revision), Is.False);
            Assert.That(flow.RetryFailedStage(failed.runId, 1, failed.revision), Is.False);
            Assert.That(flow.RetryFailedStage(failed.runId, 2, failed.revision - 1), Is.False);
            Assert.That(flow.RetryFailedStage(failed.runId, 2, failed.revision), Is.True);
            var load = flow.State;
            Assert.That(load.phase, Is.EqualTo(GameFlowPhase.LoadingBattle));
            Assert.That(load.runId, Is.EqualTo(second.runId));
            Assert.That(load.stageIndex, Is.EqualTo(2));
            Assert.That(load.retryableDefeat, Is.False);
            Assert.That(load.endReason, Is.Empty);
            Assert.That(load.battleSessionId, Is.Empty);
            Assert.That(load.battleRound, Is.Zero);
            Assert.That(load.battleStartsAt, Is.Zero);
            Assert.That(flow.ParticipantIds, Is.EquivalentTo(new ulong[] { 0, 42 }));
            Assert.That(flow.ReadyCount + flow.InitialStateReadyCount + flow.CountdownReadyCount, Is.Zero);
            Assert.That(GameFlowSnapshot.TryAccept(failed, load, false, out _), Is.True);
            Assert.That(flow.RetryFailedStage(failed.runId, 2, failed.revision), Is.False);
            Assert.That(flow.MarkBattleReady(42, failed.runId, 2, failed.revision), Is.False);
            Assert.That(flow.MarkVictory(second.battleSessionId, second.battleRound), Is.False);
            var retried = Play(flow, "battle", 13);
            Assert.That(retried.stageIndex, Is.EqualTo(2));
            Assert.That(retried.battleRound, Is.EqualTo(13));
            Assert.That(GameFlowSnapshot.TryAccept(load, failed, false, out _), Is.False);
            Assert.That(flow.MarkVictory(second.battleSessionId, second.battleRound), Is.False);
            Assert.That(flow.MarkVictory(retried.battleSessionId, retried.battleRound), Is.True);
        }

        [Test]
        public void OnlyALeaveOrDisconnectCountsAsSomeoneLeaving()
        {
            Assert.That(GameRunFlow.IsLeaveReason(GameRunFlow.PlayerLeftReason), Is.True);
            Assert.That(GameRunFlow.IsLeaveReason(GameRunFlow.PlayerDisconnectedReason), Is.True);
            Assert.That(GameRunFlow.IsLeaveReason(GameRunFlow.HostDisconnectedReason), Is.True);
            foreach (string other in new[] { "Time ran out.", "All players are down.", "A player could not finish preparing the stage.",
                "The prepared battle could not be started.", "The stage result could not be synchronized.", "", null })
                Assert.That(GameRunFlow.IsLeaveReason(other), Is.False, other ?? "null");
        }

        [Test]
        public void AbandonedAndTerritoryRunsCannotBecomeRetryableDefeats()
        {
            var flow = Run(0);
            var play = Play(flow, "battle");
            Assert.That(flow.Abort("A player left the game."), Is.True);
            var over = flow.State;
            Assert.That(over.retryableDefeat, Is.False);
            Assert.That(flow.RetryFailedStage(over.runId, over.stageIndex, over.revision), Is.False);
            Assert.That(flow.MarkDefeat(play.battleSessionId, play.battleRound, "Time ran out."), Is.False);
            var territory = new GameRunFlow();
            Assert.That(territory.EnterLobby(GameMode.Territory), Is.True);
            Assert.That(territory.BeginRun(new ulong[] { 0, 42 }), Is.True);
            var teamPlay = Play(territory, "team");
            Assert.That(territory.MarkDefeat(teamPlay.battleSessionId, teamPlay.battleRound, "Time ran out."), Is.False);
            Assert.That(territory.State.phase, Is.EqualTo(GameFlowPhase.Playing));
            Assert.That(territory.ReturnToLobby("한 판이 끝났소."), Is.True);
        }

        [Test]
        public void DisconnectAfterDefeatPermanentlyClosesThisRetryWindow()
        {
            var flow = Run(0, 42);
            var play = Play(flow, "battle");
            Assert.That(flow.MarkDefeat(play.battleSessionId, play.battleRound, "Time ran out."), Is.True);
            var failed = flow.State;
            Assert.That(flow.EndDefeatRetry("A player disconnected."), Is.True);
            var ended = flow.State;
            Assert.That(ended.retryableDefeat, Is.False);
            Assert.That(GameFlowSnapshot.TryAccept(failed, ended, false, out _), Is.True);
            Assert.That(flow.EndDefeatRetry("again"), Is.False);
            Assert.That(flow.RetryFailedStage(ended.runId, ended.stageIndex, ended.revision), Is.False);
            var fakeLoad = new GameFlowState { runId = ended.runId, stageIndex = ended.stageIndex,
                revision = ended.revision + 1, phase = GameFlowPhase.LoadingBattle };
            Assert.That(GameFlowSnapshot.TryAccept(ended, fakeLoad, false, out _), Is.False);
            var reopen = failed.Copy(); reopen.revision = ended.revision + 1;
            Assert.That(GameFlowSnapshot.TryAccept(ended, reopen, false, out _), Is.False);
        }

        [Test]
        public void DefeatRetryReplicaRejectsSkippedBattlePhasesAndMalformedRetryFlags()
        {
            var flow = Run(0);
            var play = Play(flow, "battle");
            Assert.That(flow.MarkDefeat(play.battleSessionId, play.battleRound, "All players are down."), Is.True);
            var failed = flow.State;
            var loading = new GameFlowState { runId = failed.runId, stageIndex = failed.stageIndex,
                revision = failed.revision + 1, phase = GameFlowPhase.LoadingBattle };
            foreach (string changed in new[] { "run", "stage", "revision", "phase", "identity" })
            {
                var bad = loading.Copy();
                switch (changed)
                {
                    case "run": bad.runId = "another"; break;
                    case "stage": bad.stageIndex++; break;
                    case "revision": bad.revision = failed.revision; break;
                    case "phase": bad.phase = GameFlowPhase.SynchronizingBattle; bad.battleSessionId = "next"; bad.battleRound = 2; break;
                    case "identity": bad.battleSessionId = "old"; bad.battleRound = 1; break;
                }
                Assert.That(GameFlowSnapshot.TryAccept(failed, bad, false, out _), Is.False, changed);
            }
            var illegal = loading.Copy(); illegal.retryableDefeat = true;
            Assert.That(GameFlowSnapshot.IsValid(illegal, out _), Is.False);
            illegal = failed.Copy(); illegal.mode = GameMode.Territory; illegal.totalStages = 1;
            Assert.That(GameFlowSnapshot.IsValid(illegal, out _), Is.False);
            illegal = failed.Copy(); illegal.battleSessionId = ""; illegal.battleRound = 0;
            Assert.That(GameFlowSnapshot.IsValid(illegal, out _), Is.False);
            var replica = new GameFlowReplica();
            Assert.That(replica.Apply(failed, true, out _), Is.True);
            Assert.That(replica.Apply(loading, false, out _), Is.True);
            Assert.That(replica.Apply(failed, false, out _), Is.False);
        }

    }
}
