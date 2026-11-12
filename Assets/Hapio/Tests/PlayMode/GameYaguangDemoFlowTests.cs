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
    /// <summary>Saved Host scenes and two real loopback packet peers; guest UI and physical devices are separate QA.</summary>
    [Category("Slow"), Category("Bonjour")]
    public sealed class GameYaguangDemoFlowTests
    {
        private IDisposable variant;
        private GameSession session;
        private LabNetwork network;
        private ProtocolPeer second, third;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator LoadDemoHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            variant = LabGameBuildVariant.OverrideForTests(true);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved demo Home");
            session = GameSession.Instance;
            network = session.Network;
            network.port = AvailablePort();
            // Complete genuine CPU paint cells quickly; no victory snapshot is manufactured.
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Paint Surface Resolution (0 low 1 medium 2 high)", 0f);
            Set(values, "Paint Initial Coverage (%)", 100f);
            Set(values, "Paint Pattern", 0f);
            Set(values, "Paint Drying (0 off, 1 on)", 0f);
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            Assert.That(Resources.Load<StorySequence>(GameStageStoryPresentation.YaguangDemoEndingResource), Is.Not.Null,
                "Generate explicit demo story copies before testing the demo flow.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            second?.Dispose(); third?.Dispose(); second = third = null;
            if (session != null) { network.Stop(); Object.Destroy(session.gameObject); }
            yield return null;
            yield return null;
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Yaguang demo flow cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            variant?.Dispose(); variant = null;
        }

        [UnityTest]
        public IEnumerator ThreePlayersFinishOnlyOriginalStageTwoThenReadItsAfterstorySummaryEndingAndRealTitle()
        {
            session.Host();
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene && network.IsHost,
                "The demo Host opens its three-person seat lobby");
            Assert.That(network.TargetParticipantCount, Is.EqualTo(3));
            Assert.That(network.RequestPlayerSlot(1), Is.True);
            second = new ProtocolPeer("Demo protocol P2", network.port);
            third = new ProtocolPeer("Demo protocol P3", network.port);
            second.Network.Join("127.0.0.1"); third.Network.Join("127.0.0.1");
            yield return Until(() => second.Network.IsConnected && third.Network.IsConnected
                && second.Network.CanSelectPlayerSlot && third.Network.CanSelectPlayerSlot, "Both loopback members join");
            Assert.That(second.Network.RequestPlayerSlot(2), Is.True);
            Assert.That(third.Network.RequestPlayerSlot(3), Is.True);
            yield return Until(() => network.CanStartBattle && network.ParticipantCount == 3, "Three distinct approved seats and settings");
            session.StartGame();
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Battle && network.HasBattleReferences, "Original Stage2 starts");
            Assert.That(session.State.stageIndex, Is.EqualTo(2));
            Assert.That(session.State.totalStages, Is.EqualTo(1));
            Assert.That(session.CurrentStageDefinition.stageIndex, Is.EqualTo(2));
            Assert.That(network.GameStageYokai, Is.EqualTo(LabStageYokai.Yaguang));
            var model = (LabModel)typeof(LabNetwork).GetField("model", Private).GetValue(network);
            Assert.That(model.CopyPlayers(), Has.Length.EqualTo(3));
            string battle = session.State.battleSessionId;
            Assert.That(model.PaintSurface.RegisterHit(Vector3.forward, model.PaintColorFor(LabOrbElement.Fire),
                network.LocalPlayerId, 1), Is.True);
            Assert.That(model.PaintSurface.IsComplete, Is.True);
            yield return Until(() => session.State.phase == GameFlowPhase.Completed && !session.IsLoadingScene
                && Story() != null && Story().BoundResult != null && Story().player.CurrentView != null,
                "The real Stage2 victory commits before its result story");

            var story = Story();
            Assert.That(story.player.sequence, Is.SameAs(story.afterStages[1]));
            Assert.That(story.player.sequence.cuts.Select(cut => cut.cutId), Is.EqualTo(new[] {
                "Yagwanggui_SealCard", "Yagwanggui_After_01", "Yagwanggui_After_02" }));
            Assert.That(story.BoundResult.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(story.BoundResult.players, Has.Length.EqualTo(3));
            yield return Until(() => !story.player.UserInputBlocked, "Result input is released");
            story.player.Skip();
            yield return Until(() => story.IsLocalStoryComplete, "Yaguang afterstory ends at its final picture");
            Assert.That(session.LocalSealReady, Is.False, "Reading is not explicit readiness.");
            Assert.That(session.CanStartSummary, Is.False);
            var clear = story.player.CurrentView.GetComponent<StoryStageClearControls>();
            Assert.That(clear, Is.Not.Null);
            Assert.That(clear.heading.gameObject.activeSelf, Is.False, "Stage2 is the final yokai of this route.");
            Assert.That(clear.nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true).text, Is.EqualTo("마치기"));
            clear.nextSeal.onClick.Invoke();
            Assert.That(story.ReadyView.IsWaiting && session.LocalSealReady, Is.True);
            second.PresentationBusy = false; second.Ready = true; second.ReportResult();
            third.PresentationBusy = false; third.ReportResult();
            yield return Until(() => session.SealReadyCount == 2, "Host and only one member explicitly ready");
            Assert.That(story.ReadyView.start.interactable, Is.False);
            story.StartPreparedStage();
            Assert.That(session.SummaryStarted, Is.False, "The Host cannot skip an unready participant.");
            third.Ready = true; third.PresentationBusy = true; third.ReportResult();
            yield return null;
            Assert.That(session.CanStartSummary, Is.False, "A ready member still reading holds the shared start.");
            third.PresentationBusy = false; third.ViewerOpen = true; third.ReportResult();
            yield return Until(() => session.IsAnyResultViewerOpen, "Member's viewer report arrives");
            Assert.That(session.CanStartSummary, Is.False);
            third.ViewerOpen = false; third.ReportResult();
            yield return Until(() => session.SealReadyCount == 3 && story.ReadyView.start.interactable,
                "All three confirmations, snapshots, stories and viewers are ready");
            story.RequestExit();
            Assert.That(story.ReadyView.IsConfirmingQuit, Is.True);
            Assert.That(session.CanStartSummary, Is.False, "Quit confirmation holds the shared start.");
            story.CancelQuit();
            yield return Until(() => story.ReadyView.start.interactable, "Cancel retains readiness");
            Assert.That(session.LocalSealReady, Is.True);
            story.ReadyView.start.onClick.Invoke();
            yield return VerifySummaryAndEnding(story, battle);
        }

        private IEnumerator VerifySummaryAndEnding(GameStageStoryPresentation story, string battle)
        {
            yield return Until(() => story.player.CurrentView.GetComponent<SealRunSummary>() != null,
                "Host opens the demo summary after Yaguang's own afterstory");
            yield return Until(() => second.SummaryStarted && third.SummaryStarted, "Both members receive sticky summary authorization");
            var summary = story.player.CurrentView.GetComponent<SealRunSummary>();
            Assert.That(summary.stageIndices, Is.EqualTo(new[] { 2 }));
            Assert.That(session.StageResults, Has.Count.EqualTo(1));
            Assert.That(session.SealStageResults[0], Is.Null);
            Assert.That(session.SealStageResults[2], Is.Null);
            Assert.That(summary.stageTimes[0].text, Is.EqualTo(SealRunSummary.FormatTime(session.ResultSnapshot.playedSeconds)));
            Assert.That(summary.totalTime.text, Is.EqualTo(summary.stageTimes[0].text));
            Assert.That(summary.contributions[0].text, Is.EqualTo("100%"), "This Host painted the real cells; no fake attack scores are used.");
            Assert.That(story.player.sequence, Is.Not.SameAs(story.ending), "The normal saved ending reference remains intact.");
            Assert.That(story.player.sequence.cuts, Has.Count.EqualTo(7));
            Assert.That(story.player.sequence.cuts[1].GetPage(0).dialogue, Is.EqualTo("이야~\n야광귀도 무사히 봉인했네~!"));
            Assert.That(story.skipButton.gameObject.activeSelf, Is.False);
            session.NextStage();
            Assert.That(session.State.stageIndex, Is.EqualTo(2));
            Assert.That(session.State.battleSessionId, Is.EqualTo(battle));
            yield return Until(() => story.player.IsReading && story.player.CurrentView.continueButton.gameObject.activeInHierarchy,
                "Summary entrance reveals Continue");
            story.player.CurrentView.continueButton.onClick.Invoke();
            yield return Until(() => story.player.CurrentCutIndex == 1 && !story.player.IsTransitioning,
                "Cloud wipe reveals the first ending picture");
            var endingCuts = new HashSet<string>();
            double until = Time.realtimeSinceStartupAsDouble + 30d;
            while (story != null && story.player.IsPlaying && Time.realtimeSinceStartupAsDouble < until)
            {
                endingCuts.Add(story.player.sequence.cuts[story.player.CurrentCutIndex].cutId);
                story.player.Next();
                yield return null;
            }
            Assert.That(endingCuts, Is.EquivalentTo(Enumerable.Range(1, 6).Select(i => "Ending_0" + i)));
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Home, "Ending returns to the real title");
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(network.IsConnected, Is.False);
            Assert.That(Object.FindFirstObjectByType<GameLobbyView>().titlePage.activeInHierarchy, Is.True);
        }

        private static GameStageStoryPresentation Story() => Object.FindFirstObjectByType<GameStageStoryPresentation>();
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

            public ProtocolPeer(string name, ushort port)
            {
                root = new GameObject(name); root.SetActive(false);
                root.AddComponent(typeof(LabNetwork).GetField("manager").FieldType);
                root.AddComponent(typeof(LabNetwork).Assembly.GetType("C6Lab.LabDualStackTransport", true));
                Network = root.AddComponent<LabNetwork>();
                Network.config = Resources.Load<LabConfig>("LabConfig"); Network.port = port;
                root.SetActive(true); Object.DontDestroyOnLoad(root);
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
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
