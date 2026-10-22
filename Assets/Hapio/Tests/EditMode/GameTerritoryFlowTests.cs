using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>2:2 청홍 땅따먹기 (#281): one battle, then the same room's lobby; and its own DEV scope.</summary>
    public sealed class GameTerritoryFlowTests
    {
        private static GameRunFlow Territory(params ulong[] ids)
        {
            var flow = new GameRunFlow();
            Assert.That(flow.EnterLobby(GameMode.Territory), Is.True);
            Assert.That(flow.State.mode, Is.EqualTo(GameMode.Territory));
            Assert.That(flow.BeginRun(ids), Is.True);
            return flow;
        }

        private static GameFlowState Play(GameRunFlow flow, string session)
        {
            var loading = flow.State;
            foreach (ulong id in flow.ParticipantIds)
                Assert.That(flow.MarkBattleReady(id, loading.runId, loading.stageIndex, loading.revision), Is.True);
            Assert.That(flow.MarkPlaying(loading.runId, loading.stageIndex, loading.revision, session, 1), Is.True);
            return flow.State;
        }

        [Test]
        public void OneBattleCompletesThenTheSameRoomReturnsToItsLobbyForAnotherRound()
        {
            var flow = Territory(0, 10, 20, 30);
            Assert.That(flow.State.totalStages, Is.EqualTo(1));
            Assert.That(GameFlowSnapshot.IsValid(flow.State, out string reason), Is.True, reason);
            var playing = Play(flow, "territory-1");
            Assert.That(flow.MarkVictory(playing.battleSessionId, playing.battleRound), Is.True);
            var completed = flow.State;
            Assert.That(completed.phase, Is.EqualTo(GameFlowPhase.Completed), "A draw or a win both end the single battle.");
            Assert.That(GameFlowSnapshot.IsValid(completed, out reason), Is.True, reason);
            Assert.That(flow.NextStage(completed.runId, completed.stageIndex, completed.revision), Is.False);

            Assert.That(flow.ReturnToLobby("한 판이 끝났소."), Is.True);
            var lobby = flow.State;
            Assert.That(lobby.phase, Is.EqualTo(GameFlowPhase.Lobby));
            Assert.That(lobby.mode, Is.EqualTo(GameMode.Territory));
            Assert.That(lobby.revision, Is.EqualTo(completed.revision + 1), "Newer than the run, so no older lobby state can follow.");
            Assert.That(lobby.notice, Is.EqualTo("한 판이 끝났소."));
            Assert.That(lobby.runId, Is.Empty);
            Assert.That(flow.ParticipantIds, Is.Empty);
            Assert.That(GameFlowSnapshot.IsValid(lobby, out reason), Is.True, reason);

            Assert.That(flow.BeginRun(new ulong[] { 0, 10, 20, 40 }), Is.True, "A replacement player can join the next round.");
            Assert.That(flow.State.runId, Is.Not.EqualTo(completed.runId));
            Assert.That(flow.State.mode, Is.EqualTo(GameMode.Territory));
            Assert.That(flow.State.notice, Is.Empty);
        }

        [Test]
        public void APlayerLeavingStopsTheBattleIntoTheLobbyButTheStoryKeepsItsGameOver()
        {
            var flow = Territory(0, 10, 20, 30);
            Play(flow, "territory-leave");
            Assert.That(flow.ReturnToLobby("한 명이 나갔소."), Is.True);
            Assert.That(flow.State.phase, Is.EqualTo(GameFlowPhase.Lobby));

            var story = new GameRunFlow();
            Assert.That(story.EnterLobby(), Is.True);
            Assert.That(story.BeginRun(new ulong[] { 0, 10 }), Is.True);
            Assert.That(story.ReturnToLobby("no"), Is.False, "Only a 2:2 room returns to its lobby.");
            Assert.That(story.State.totalStages, Is.EqualTo(3));
            Assert.That(new GameRunFlow().ReturnToLobby("idle"), Is.False);
        }

        [Test]
        public void SnapshotsKeepEachModeToItsOwnStageCountAndNotices()
        {
            var territory = new GameFlowState
            {
                runId = "run", revision = 3, stageIndex = 1, mode = GameMode.Territory,
                totalStages = 1, phase = GameFlowPhase.Completed, battleSessionId = "b", battleRound = 1
            };
            Assert.That(GameFlowSnapshot.IsValid(territory, out string reason), Is.True, reason);
            var second = territory.Copy(); second.stageIndex = 2;
            Assert.That(GameFlowSnapshot.IsValid(second, out _), Is.False);
            var mixed = territory.Copy(); mixed.totalStages = 3;
            Assert.That(GameFlowSnapshot.IsValid(mixed, out _), Is.False);
            var story = new GameFlowState { phase = GameFlowPhase.Lobby, totalStages = 1 };
            Assert.That(GameFlowSnapshot.IsValid(story, out _), Is.False, "A story room always has three stages.");
            var noticeInBattle = territory.Copy(); noticeInBattle.notice = "x";
            Assert.That(GameFlowSnapshot.IsValid(noticeInBattle, out _), Is.False);
            var lobby = new GameFlowState
                { phase = GameFlowPhase.Lobby, mode = GameMode.Territory, totalStages = 1, revision = 4, notice = "돌아왔소" };
            Assert.That(GameFlowSnapshot.IsValid(lobby, out reason), Is.True, reason);
            var tooLong = lobby.Copy(); tooLong.notice = new string('가', 129);
            Assert.That(GameFlowSnapshot.IsValid(tooLong, out _), Is.False);
            var switched = territory.Copy(); switched.mode = GameMode.Story; switched.totalStages = 3; switched.stageIndex = 3;
            switched.revision = 4;
            Assert.That(GameFlowSnapshot.TryAccept(territory, switched, false, out reason), Is.False);
            Assert.That(reason, Does.Contain("mode"));
        }

        [Test]
        public void APeerTakesTheLobbyAfterTheRunOnlyWhenAllowedAndNeverReplaysTheRetiredRun()
        {
            var replica = new GameFlowReplica();
            var lobby = new GameFlowState { phase = GameFlowPhase.Lobby, mode = GameMode.Territory, totalStages = 1 };
            Assert.That(replica.Apply(lobby, true, out _), Is.True);
            var loading = new GameFlowState
                { runId = "run", revision = 1, stageIndex = 1, mode = GameMode.Territory, totalStages = 1, phase = GameFlowPhase.LoadingBattle };
            Assert.That(replica.Apply(loading, true, out _), Is.True);
            var back = new GameFlowState
                { phase = GameFlowPhase.Lobby, mode = GameMode.Territory, totalStages = 1, revision = 2, notice = "돌아왔소" };
            Assert.That(replica.Apply(back, false, out _), Is.False, "Only the session's explicit 2:2 boundary allows it.");
            Assert.That(replica.Apply(back, true, out string reason), Is.True, reason);
            Assert.That(replica.State.notice, Is.EqualTo("돌아왔소"));
            var stale = loading.Copy(); stale.revision = 5;
            Assert.That(replica.Apply(stale, true, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("retired run"));
        }

        [Test]
        public void TheTerritoryScopeKeepsItsSwitchOnAndEditsOnlyTimeAndFever()
        {
            using (var settings = new GameStageSettings(Resources.Load<LabConfig>("LabConfig"), GameStageCatalog.RequireDefault()))
            {
                const int scope = GameStageSettings.TerritoryScope;
                Assert.That(settings.Capture(scope)[196], Is.EqualTo(1f));
                foreach (int stage in new[] { 1, 2, 3 }) Assert.That(settings.Capture(stage)[196], Is.Zero);
                Assert.That(Enumerable.Range(196, 4).Select(i => GameStageSettings.IsFieldVisible(scope, i)),
                    Is.EqualTo(new[] { false, true, true, true }), "The switch belongs to the room mode, not to DEV.");
                Assert.That(Enumerable.Range(196, 4).Any(i => Enumerable.Range(0, 4).Any(s => GameStageSettings.IsFieldVisible(s, i))),
                    Is.False);

                float[] values = settings.Capture(scope); values[198] = 3f; values[199] = 5f;
                Assert.That(settings.TryApply(scope, values, out string error), Is.True, error);
                float[] common = settings.Capture(0); common[2] = 240f;
                Assert.That(settings.TryApply(0, common, out error), Is.True, error);
                Assert.That(settings.Capture(scope)[2], Is.EqualTo(240f), "Common values reach the 2:2 profile too.");

                LabConfig team = settings.CreateTerritoryConfig(), story = settings.CreateStageConfig(1);
                try
                {
                    Assert.That(team.TeamTerritoryEnabled, Is.True);
                    Assert.That(team.TeamFeverHits, Is.EqualTo(3));
                    Assert.That(team.TeamFeverSeconds, Is.EqualTo(5f));
                    Assert.That(story.TeamTerritoryEnabled, Is.False);
                }
                finally { Object.DestroyImmediate(team); Object.DestroyImmediate(story); }

                float[] forged = settings.Capture(scope); forged[196] = 0f;
                Assert.That(settings.TryApply(scope, forged, out _), Is.False);
                Assert.That(settings.TryApplyBattle(scope, forged, out _), Is.False, "A paused 2:2 battle cannot switch itself off.");
                float[] battle = settings.Capture(scope); battle[199] = 9f;
                Assert.That(settings.TryApplyBattle(scope, battle, out error), Is.True, error);
                Assert.That(settings.Capture(scope)[199], Is.EqualTo(9f));
                Assert.That(settings.Capture(1)[199], Is.Not.EqualTo(9f));
            }
        }
    }
}
