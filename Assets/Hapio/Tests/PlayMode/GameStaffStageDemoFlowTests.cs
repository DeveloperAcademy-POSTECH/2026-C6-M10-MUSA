using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Saved Host scene and real packet peers; physical-device and Guest UI checks are separate.</summary>
    [Category("Slow"), Category("Bonjour")]
    public sealed class GameStaffStageDemoFlowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private IDisposable variant;
        private GameSession session;
        private LabNetwork network;
        private ProtocolPeer second, third, wrongStage;

        [UnitySetUp]
        public IEnumerator LoadStaffHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            variant = LabGameBuildVariant.OverrideStaffStageDemoForTests(true);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved staff demo Home");
            session = GameSession.Instance; network = session.Network;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            wrongStage?.Dispose(); second?.Dispose(); third?.Dispose();
            wrongStage = second = third = null;
            if (session != null) { session.CancelDemoEntry(); network.Stop(); Object.Destroy(session.gameObject); }
            yield return null; yield return null;
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Staff stage demo cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            variant?.Dispose(); variant = null;
        }

        [UnityTest]
        public IEnumerator ThreePlayersFinishOnlyStageOne() { yield return ThreePlayerRound(1); }

        [UnityTest]
        public IEnumerator ThreePlayersFinishOnlyStageTwo() { yield return ThreePlayerRound(2); }

        [UnityTest]
        public IEnumerator ThreePlayersFinishOnlyStageThree() { yield return ThreePlayerRound(3); }

        [UnityTest]
        public IEnumerator GuestInterruptedRetryReturnsEveryoneToUnselectedSeatsAndRequiresManualStart()
        {
            yield return StartRetryRound(2);
            string oldRun = session.State.runId;
            string oldBattle = session.State.battleSessionId;
            ulong[] members = network.ConnectedPlayerIds;
            second.SendCurrent("exit");
            yield return Until(() => session.State.phase == GameFlowPhase.GameOver && !session.IsLoadingScene
                && second.LastState?.phase == GameFlowPhase.GameOver && third.LastState?.phase == GameFlowPhase.GameOver,
                "Guest exit is shared as an interrupted GameOver before a retry is requested");
            Assert.That(session.State.retryableDefeat, Is.False);
            Assert.That(session.CanRetryStaffDemo, Is.True);
            // Reopening admission must not itself skip the GameOver screen.
            yield return null; yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver));
            string repeatedRequest = second.CurrentRequest("staff_retry_lobby");
            Assert.That(second.Network.SendGameFlowToHost(repeatedRequest), Is.True);
            Assert.That(second.Network.SendGameFlowToHost(repeatedRequest), Is.True);
            yield return UnselectedRetryLobby(2);
            Assert.That(network.ConnectedPlayerIds, Is.EquivalentTo(members), "Retry retains the existing room transport and identities.");
            Assert.That(session.RetryStaffDemo(), Is.False, "A second retry cannot reset the fresh lobby again.");
            Assert.That(network.Snapshot, Is.Null);
            Assert.That(session.ResultSnapshot, Is.Null);
            Assert.That(session.StageResults, Is.Empty);
            Assert.That(session.PreviewTexture, Is.Null);
            Assert.That(session.IsViewerOpen, Is.False);

            Assert.That(network.RequestPlayerSlot(3), Is.True);
            Assert.That(second.Network.RequestPlayerSlot(1), Is.True);
            yield return Until(() => network.SelectedParticipantCount == 2, "Two fresh P choices are committed");
            session.StartGame();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Lobby), "Two P choices cannot start a three-player demo.");
            Assert.That(third.Network.RequestPlayerSlot(2), Is.True);
            yield return Until(() => network.CanStartBattle, "The third fresh P choice and settings release Host start");
            yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Lobby), "Readiness never starts a retry automatically.");
            session.StartGame();
            yield return PlayingRetryStage(2);
            Assert.That(session.State.runId, Is.Not.EqualTo(oldRun));
            Assert.That(session.State.battleSessionId, Is.Not.EqualTo(oldBattle));
            Assert.That(network.Snapshot.orbs, Is.Empty);
            Assert.That(network.Snapshot.paintCoveredCount, Is.Zero);
            Assert.That(network.LocalSlotNumber, Is.EqualTo(3));
            Assert.That(second.Network.SendGameFlowToHost(repeatedRequest), Is.True);
            yield return null; yield return null; yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing), "A delayed request from the retired run cannot reset the new battle.");
        }

        [UnityTest]
        public IEnumerator DisconnectedMemberCanRejoinInterruptedHostAndOpenTheSameStageSeatLobby()
        {
            yield return StartRetryRound(3);
            var originalNetwork = network;
            ulong remainingGuest = third.Network.LocalPlayerId;
            second.Dispose(); second = null;
            yield return Until(() => network.ParticipantCount == 2 && session.State.phase == GameFlowPhase.GameOver
                && !session.IsLoadingScene, "A real socket departure interrupts the staff battle");
            Assert.That(session.State.endReason, Is.EqualTo(GameRunFlow.PlayerDisconnectedReason));
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.GameOver), "Admission recovery waits for an explicit retry or join.");
            Assert.That(network.Snapshot, Is.Null, "The recovery Host removes its old battle before admitting a retrying Guest.");
            Assert.That(network.CanStartBattle, Is.False);
            second = new ProtocolPeer("Staff returning member", network.port, 3);
            second.Network.Join("127.0.0.1");
            yield return Until(() => second.Network.IsConnected && network.ParticipantCount == 3,
                "A retrying same-stage member is admitted instead of being rejected as a still-playing room");
            yield return UnselectedRetryLobby(3);
            Assert.That(session.Network, Is.SameAs(originalNetwork));
            Assert.That(network.ConnectedPlayerIds, Has.Member(remainingGuest), "The surviving Guest stays in the original room.");
            Assert.That(network.IsHost, Is.True);
            Assert.That(network.HeldSeatCount, Is.Zero, "The retired P assignment cannot reserve a place in the new selection.");
            Assert.That(network.CanStartBattle, Is.False);
        }

        [UnityTest]
        public IEnumerator HostRetryUsesTheAuthoredGameOverActionAndDoesNotReturnToStagePicker()
        {
            yield return StartRetryRound(1);
            session.ExitRun();
            yield return Until(() => session.State.phase == GameFlowPhase.GameOver && !session.IsLoadingScene
                && second.LastState?.phase == GameFlowPhase.GameOver && third.LastState?.phase == GameFlowPhase.GameOver,
                "Host pause-menu exit produces the shared GameOver screen");
            var over = Object.FindFirstObjectByType<GameOverCharacterView>();
            Assert.That(over, Is.Not.Null);
            yield return Until(() => over.homeButton.interactable, "The saved retry action releases its entry input guard");
            Assert.That(over.homeButton.GetComponentInChildren<UnityEngine.UI.Text>(true).text, Does.Contain("다시"));
            over.homeButton.onClick.Invoke();
            yield return UnselectedRetryLobby(1);
            Assert.That(session.SelectedDemoStage, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<GameStaffDemoView>(), Is.Null,
                "Retry opens P selection directly instead of returning to the three stage cards.");
        }

        private IEnumerator StartRetryRound(int stage)
        {
            Assert.That(session.SelectDemoStage(stage), Is.True);
            yield return Until(() => network.IsHost && session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene,
                "The retry fixture enters the chosen staff stage room");
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 600f);
            Set(values, "Paint Surface Resolution (0 low 1 medium 2 high)", 0f);
            Set(values, "Paint Drying (0 off, 1 on)", 0f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            Assert.That(network.RequestPlayerSlot(1), Is.True);
            second = new ProtocolPeer("Staff retry P2", network.port, stage);
            third = new ProtocolPeer("Staff retry P3", network.port, stage);
            second.Network.Join("127.0.0.1"); third.Network.Join("127.0.0.1");
            yield return Until(() => second.Network.CanSelectPlayerSlot && third.Network.CanSelectPlayerSlot,
                "Both matching-stage packet members are ready to choose P");
            Assert.That(second.Network.RequestPlayerSlot(2), Is.True);
            Assert.That(third.Network.RequestPlayerSlot(3), Is.True);
            yield return Until(() => network.CanStartBattle, "Initial three P choices and config are ready");
            session.StartGame();
            yield return PlayingRetryStage(stage);
        }

        private IEnumerator PlayingRetryStage(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences,
            "Host manually starts the selected canonical stage after its preparation barriers");

        private IEnumerator UnselectedRetryLobby(int stage)
        {
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Lobby
                && second.LastState?.phase == GameFlowPhase.Lobby && third.LastState?.phase == GameFlowPhase.Lobby
                && second.Network.CanSelectPlayerSlot && third.Network.CanSelectPlayerSlot,
                "All three members observe the same room's fresh P selection lobby");
            Assert.That(session.SelectedDemoStage, Is.EqualTo(stage));
            Assert.That(session.State.demoStageIndex, Is.EqualTo(stage));
            Assert.That(session.State.stageIndex, Is.Zero);
            Assert.That(session.State.runId, Is.Empty);
            Assert.That(network.ParticipantCount, Is.EqualTo(3));
            foreach (var member in new[] { network, second.Network, third.Network })
            {
                Assert.That(member.DemoStageIndex, Is.EqualTo(stage));
                Assert.That(member.LocalSlotNumber, Is.Zero);
                Assert.That(member.SelectedParticipantCount, Is.Zero);
                Assert.That(member.CanStartBattle, Is.False);
            }
        }

        private IEnumerator ThreePlayerRound(int stage)
        {
            Assert.That(session.SelectDemoStage(stage), Is.True);
            Assert.That(session.SelectDemoStage(stage == 1 ? 3 : 1), Is.False, "One card request runs at a time.");
            yield return Until(() => network.IsHost && session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene,
                "A stage card creates and enters its own three-player room");
            Assert.That(network.TargetParticipantCount, Is.EqualTo(3));
            Assert.That(network.DemoStageIndex, Is.EqualTo(stage));
            Assert.That(session.State.demoStageIndex, Is.EqualTo(stage));
            Assert.That(session.RunProfile.FirstStage, Is.EqualTo(stage));
            Assert.That(session.RunProfile.Count, Is.EqualTo(1));
            Assert.That(network.RequestPlayerSlot(1), Is.True);

            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Paint Surface Resolution (0 low 1 medium 2 high)", 0f);
            Set(values, "Paint Initial Coverage (%)", 100f);
            Set(values, "Paint Pattern", 0f);
            Set(values, "Paint Drying (0 off, 1 on)", 0f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);

            wrongStage = new ProtocolPeer("Wrong selected stage", network.port, stage == 1 ? 3 : 1);
            wrongStage.Network.Join("127.0.0.1");
            yield return Until(() => !wrongStage.Network.IsJoiningRoom && wrongStage.Network.RoomSearchFailed,
                "The Host rejects a different selected stage at transport admission");
            Assert.That(wrongStage.Network.IsConnected, Is.False);
            Assert.That(network.ParticipantCount, Is.EqualTo(1));
            wrongStage.Dispose(); wrongStage = null;
            yield return null;

            second = new ProtocolPeer("Staff protocol P2", network.port, stage);
            third = new ProtocolPeer("Staff protocol P3", network.port, stage);
            second.Network.Join("127.0.0.1"); third.Network.Join("127.0.0.1");
            yield return Until(() => second.Network.IsConnected && third.Network.IsConnected
                && second.Network.CanSelectPlayerSlot && third.Network.CanSelectPlayerSlot, "Matching-stage members join");
            Assert.That(second.Network.RequestPlayerSlot(2), Is.True);
            Assert.That(third.Network.RequestPlayerSlot(3), Is.True);
            yield return Until(() => network.CanStartBattle && network.ParticipantCount == 3,
                "Three distinct seats and settings are ready");
            yield return null;
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Lobby), "Joining a third player does not start combat.");
            session.StartGame();
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && network.HasBattleReferences, "The selected canonical stage starts after Host input");
            Assert.That(session.State.stageIndex, Is.EqualTo(stage));
            Assert.That(session.State.totalStages, Is.EqualTo(1));
            Assert.That((int)network.GameStageYokai, Is.EqualTo(stage));
            var model = (LabModel)typeof(LabNetwork).GetField("model", Private).GetValue(network);
            Assert.That(model.PaintSurface.RegisterHit(Vector3.forward, model.PaintColorFor(LabOrbElement.Fire),
                network.LocalPlayerId, 1), Is.True);
            Assert.That(model.PaintSurface.IsComplete, Is.True);
            yield return Until(() => session.State.phase == GameFlowPhase.Completed && !session.IsLoadingScene
                && Object.FindFirstObjectByType<GameStageStoryPresentation>()?.BoundResult != null,
                "The chosen stage completes as this run's final stage");
            Assert.That(session.StageResults, Has.Count.EqualTo(1));
            Assert.That(session.SealStageResults[stage - 1], Is.Not.Null);
            for (int other = 1; other <= 3; other++)
                if (other != stage) Assert.That(session.SealStageResults[other - 1], Is.Null);
            string battle = session.State.battleSessionId;
            session.NextStage();
            Assert.That(session.State.stageIndex, Is.EqualTo(stage));
            Assert.That(session.State.battleSessionId, Is.EqualTo(battle), "No later stage can start from the single-round result.");
            yield return VerifyStoryEndingAndReturn(stage, battle);
        }

        private IEnumerator VerifyStoryEndingAndReturn(int stage, string battle)
        {
            var story = Object.FindFirstObjectByType<GameStageStoryPresentation>();
            Assert.That(story, Is.Not.Null);
            Assert.That(story.player.sequence, Is.SameAs(story.afterStages[stage - 1]));
            Assert.That(story.BoundResult.players, Has.Length.EqualTo(3), "The existing record screen uses this room's three authoritative players.");
            yield return Until(() => story.player.CurrentView != null && !story.player.UserInputBlocked,
                "The selected stage afterstory releases input");
            story.player.Skip();
            yield return Until(() => story.IsLocalStoryComplete && story.player.CurrentView != null
                && story.player.CurrentView.GetComponent<StoryStageClearControls>() != null,
                "Afterstory retains its final clear picture");
            Assert.That(session.LocalSealReady, Is.False, "Reading alone does not confirm readiness.");
            var clear = story.player.CurrentView.GetComponent<StoryStageClearControls>();
            Assert.That(clear.heading.gameObject.activeSelf, Is.False, "Every selected stage is this run's final stage.");
            Assert.That(clear.nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true).text, Is.EqualTo("마치기"));
            clear.nextSeal.onClick.Invoke();
            yield return Until(() => session.LocalSealReady && story.ReadyView.IsWaiting, "Host explicitly confirms the final result");
            second.PresentationBusy = third.PresentationBusy = false;
            second.Ready = third.Ready = true;
            second.ReportResult(); third.ReportResult();
            yield return Until(() => session.SealReadyCount == 3 && story.ReadyView.start.interactable,
                "All three result confirmations permit the Host's summary button");
            Assert.That(session.SummaryStarted, Is.False, "Readiness does not automatically leave the result.");
            story.ReadyView.start.onClick.Invoke();
            yield return Until(() => story.player.CurrentView != null
                && story.player.CurrentView.GetComponent<SealRunSummary>() != null,
                "Host starts this selected-stage summary");
            yield return Until(() => second.SummaryStarted && third.SummaryStarted,
                "Both packet members receive the summary authorization");
            var summary = story.player.CurrentView.GetComponent<SealRunSummary>();
            Assert.That(summary.stageIndices, Is.EqualTo(new[] { stage }));
            Assert.That(story.player.sequence, Is.SameAs(Resources.Load<StorySequence>(
                GameStageStoryPresentation.StaffDemoEndingResource(stage))));
            Assert.That(summary.stageTimes, Has.Length.EqualTo(1));
            Assert.That(summary.stageTimes[0].text, Is.EqualTo(SealRunSummary.FormatTime(session.ResultSnapshot.playedSeconds)));
            Assert.That(summary.totalTime.text, Is.EqualTo(summary.stageTimes[0].text));
            Assert.That(summary.contributions[0].text, Is.EqualTo("100%"), "Only this Host supplied actual new paint cells.");
            Assert.That(session.State.battleSessionId, Is.EqualTo(battle));
            yield return Until(() => story.player.IsReading
                && story.player.CurrentView.continueButton.gameObject.activeInHierarchy, "Summary Continue appears");
            story.player.CurrentView.continueButton.onClick.Invoke();
            yield return Until(() => story.player.CurrentCutIndex == 1 && !story.player.IsTransitioning,
                "Summary transitions into the selected stage ending");
            var cuts = new HashSet<string>();
            double until = Time.realtimeSinceStartupAsDouble + 30d;
            while (story != null && story.player.IsPlaying && Time.realtimeSinceStartupAsDouble < until)
            {
                cuts.Add(story.player.sequence.cuts[story.player.CurrentCutIndex].cutId);
                story.player.Next();
                yield return null;
            }
            Assert.That(cuts, Is.EquivalentTo(Enumerable.Range(1, 6).Select(i => "Ending_0" + i)));
            yield return Until(() => story.FinalPage != null && story.FinalPage.gameObject.activeInHierarchy
                && story.FinalPage.IsReady, "The ending opens the real temporary QR page");
            Assert.That(story.FinalPage.placeholder.activeSelf, Is.True);
            Assert.That(story.FinalPage.qrImage.gameObject.activeSelf, Is.False);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Completed), "The final page waits for a deliberate return action.");
            Assert.That(network.IsConnected, Is.True);
            story.FinalPage.returnButton.onClick.Invoke();
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && !network.IsConnected && session.SelectedDemoStage == 0
                && SceneManager.GetActiveScene().path == GameScenePaths.Home,
                "Final-page return closes the room and restores the staff selection");
            Assert.That(Object.FindFirstObjectByType<GameStaffDemoView>(), Is.Not.Null);
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(network.DemoStageIndex, Is.Zero);
            Assert.That(session.StageResults, Is.Empty, "The next party never inherits this group's record.");
        }

        private static void Set(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(i => LabConfig.DeveloperFields[i].Label == label);
            values[index] = value;
        }
        private static IEnumerator Until(Func<bool> condition, string message)
        {
            double until = Time.realtimeSinceStartupAsDouble + 30d;
            while (!condition() && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        private static ushort AvailablePort()
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
                            { ipv6.DualMode = false; ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port)); }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("No free dual-family test port.");
        }

        [Serializable]
        private sealed class PeerControl
        {
            public string kind, runId, stageId, settingsFingerprint;
            public int stageIndex, settingsRevision;
            public uint revision;
            public LabStageYokai yokai;
            public GameFlowState state;
            public bool resultViewerOpen, resultPresentationBusy, sealReady, summaryStarted;
        }

        private sealed class ProtocolPeer : IDisposable
        {
            private readonly GameObject root;
            public readonly LabNetwork Network;
            private PeerControl last;
            public bool PresentationBusy = true, Ready, ViewerOpen, SummaryStarted;
            public GameFlowState LastState => last?.state;

            public ProtocolPeer(string name, ushort port, int stage)
            {
                root = new GameObject(name); root.SetActive(false);
                root.AddComponent(typeof(LabNetwork).GetField("manager").FieldType);
                root.AddComponent(typeof(LabNetwork).Assembly.GetType("C6Lab.LabDualStackTransport", true));
                Network = root.AddComponent<LabNetwork>();
                Network.config = Resources.Load<LabConfig>("LabConfig"); Network.port = port;
                root.SetActive(true); Object.DontDestroyOnLoad(root);
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
                Assert.That(Network.SetDemoStageIdentity(stage), Is.True);
                Network.GameFlowMessage += Receive;
            }
            private void Receive(ulong sender, string json)
            {
                var control = JsonUtility.FromJson<PeerControl>(json);
                if (control.kind == "paint_finish_prepare" || control.kind == "paint_finish_commit")
                {
                    string reply = control.kind == "paint_finish_prepare" ? "paint_finish_schedule_ready" : "paint_finish_done";
                    Network.SendGameFlowToHost(json.Replace("\"kind\":\"" + control.kind + "\"", "\"kind\":\"" + reply + "\""));
                    return;
                }
                if (control.kind != "state" || control.state == null) return;
                last = control;
                SummaryStarted |= control.summaryStarted;
                Network.ObserveGameStage(control.yokai);
                switch (control.state.phase)
                {
                    case GameFlowPhase.LoadingBattle: Send("loaded"); break;
                    case GameFlowPhase.SynchronizingBattle: Send("initial_ready"); break;
                    case GameFlowPhase.Countdown: Send("countdown_ready"); break;
                    case GameFlowPhase.Completed: ReportResult(); break;
                }
            }
            public void ReportResult() { if (last != null && last.state.phase == GameFlowPhase.Completed) Send("result_ready"); }
            public void SendCurrent(string kind) => Send(kind);
            public string CurrentRequest(string kind)
            {
                Assert.That(last, Is.Not.Null);
                var control = JsonUtility.FromJson<PeerControl>(JsonUtility.ToJson(last));
                control.kind = kind; control.state = null;
                return JsonUtility.ToJson(control);
            }
            private void Send(string kind)
            {
                var control = JsonUtility.FromJson<PeerControl>(JsonUtility.ToJson(last));
                control.kind = kind; control.state = null;
                control.resultPresentationBusy = PresentationBusy; control.resultViewerOpen = ViewerOpen; control.sealReady = Ready;
                Network.SendGameFlowToHost(JsonUtility.ToJson(control));
            }
            public void Dispose()
            {
                if (root == null) return;
                Network.GameFlowMessage -= Receive; Network.Stop(); Object.Destroy(root);
            }
        }
    }
}
