using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameFlowSnapshotTests
    {
        private static GameFlowState Loading(string runId = "run", uint revision = 1, int stage = 1)
            => new GameFlowState
            {
                runId = runId, revision = revision, stageIndex = stage,
                phase = GameFlowPhase.LoadingBattle
            };

        [Test]
        public void ReplicationRejectsDuplicatePreviousRevisionAndPreviousStage()
        {
            var replica = new GameFlowReplica();
            Assert.That(replica.Apply(Loading(), true, out _), Is.True);
            Assert.That(replica.Apply(Loading(), true, out _), Is.False);
            Assert.That(replica.Apply(Loading(revision: 4, stage: 2), false, out _), Is.True);
            Assert.That(replica.Apply(Loading(revision: 3, stage: 2), false, out _), Is.False);
            Assert.That(replica.Apply(Loading(revision: 5, stage: 1), false, out _), Is.False);
            Assert.That(replica.State.stageIndex, Is.EqualTo(2));
        }

        [Test]
        public void NewRunRequiresExplicitLobbyBoundaryAndRetiredRunCannotReturn()
        {
            var replica = new GameFlowReplica();
            Assert.That(replica.Apply(new GameFlowState { phase = GameFlowPhase.Lobby }, true, out _), Is.True);
            Assert.That(replica.Apply(Loading("old"), false, out _), Is.False);
            Assert.That(replica.Apply(Loading("old"), true, out _), Is.True);
            Assert.That(replica.Apply(Loading("new"), true, out _), Is.False);
            Assert.That(replica.Apply(new GameFlowState(), true, out _), Is.True);
            Assert.That(replica.Apply(new GameFlowState { phase = GameFlowPhase.Lobby }, true, out _), Is.True);
            Assert.That(replica.Apply(Loading("new"), true, out _), Is.True);
            Assert.That(replica.Apply(new GameFlowState(), true, out _), Is.True);
            Assert.That(replica.Apply(Loading("old"), true, out _), Is.False);
        }

        [Test]
        public void TerminalSnapshotCannotBeResurrectedByHigherRevision()
        {
            var over = Loading(revision: 5);
            over.phase = GameFlowPhase.GameOver;
            over.endReason = "host disconnected";
            Assert.That(GameFlowSnapshot.TryAccept(over, Loading(revision: 6), false, out _), Is.False);
        }

        [Test]
        public void ResultsCannotChangeBattleIdentityForSameStage()
        {
            var playing = Loading(revision: 2);
            playing.phase = GameFlowPhase.Playing;
            playing.battleSessionId = "actual-battle";
            playing.battleRound = 47;
            var result = playing.Copy();
            result.revision++;
            result.phase = GameFlowPhase.StageResult;
            result.battleSessionId = "previous-battle";
            Assert.That(GameFlowSnapshot.TryAccept(playing, result, false, out _), Is.False);
            result.battleSessionId = playing.battleSessionId;
            Assert.That(GameFlowSnapshot.TryAccept(playing, result, false, out _), Is.True);
        }

        [Test]
        public void MalformedEnumsStringsStagesAndBattleIdentityAreRejected()
        {
            var invalid = new[]
            {
                new GameFlowState { phase = (GameFlowPhase)999 },
                new GameFlowState { stageIndex = 1 },
                new GameFlowState { runId = null },
                new GameFlowState { endReason = new string('x', 257) },
                Loading(new string('r', 65)),
                Loading(stage: 4),
                Loading(revision: 0),
                new GameFlowState { totalStages = 4 },
                new GameFlowState { runId = "r", revision = 1, stageIndex = 1, phase = GameFlowPhase.Playing },
                new GameFlowState { runId = "r", revision = 1, stageIndex = 1, phase = GameFlowPhase.Completed },
                new GameFlowState { runId = "r", revision = 1, stageIndex = 1, phase = GameFlowPhase.GameOver },
                new GameFlowState { runId = "r", revision = 1, stageIndex = 1, phase = GameFlowPhase.GameOver,
                    endReason = "left", battleSessionId = "partial" }
            };
            foreach (var value in invalid)
                Assert.That(GameFlowSnapshot.IsValid(value, out _), Is.False, value.phase + " / " + value.runId);
        }

        [Test]
        public void ReplicaRetainsCopiesAndRoomResetForgetsOnlyPreviousRoom()
        {
            var replica = new GameFlowReplica();
            var state = Loading();
            Assert.That(replica.Apply(state, true, out _), Is.True);
            state.stageIndex = 3;
            var copy = replica.State;
            copy.stageIndex = 2;
            Assert.That(replica.State.stageIndex, Is.EqualTo(1));
            replica.ResetForNewRoom();
            Assert.That(replica.State, Is.Null);
            Assert.That(replica.Apply(Loading(), true, out _), Is.True);
        }
    }
}
