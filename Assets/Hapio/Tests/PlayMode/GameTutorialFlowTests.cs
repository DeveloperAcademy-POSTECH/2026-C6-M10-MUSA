using System;
using System.Collections;
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
    /// <summary>
    /// 튜토리얼 (#477) 0단계 흐름: 첫 게임 시작의 오프닝 → 3자리 연습 판(나 불이 가운데, 묵이·금옥이 양옆) → 대사 → 방 목록.
    /// 저장된 Home·Battle Scene과 실제 세션으로 확인한다. 기기·네이티브 빌드 검사가 아니다.
    /// </summary>
    public sealed class GameTutorialFlowTests
    {
        private GameSession session;

        [UnitySetUp]
        public IEnumerator StartANewAppRun()
        {
            Assert.That(GameSession.Instance, Is.Null, "The preceding fixture must release its persistent session.");
            ResetRunState(typeof(GameOpeningController), "ResetRunState");
            ResetRunState(typeof(GameSession), "ResetTutorialRun");
            Assert.That(GameSession.TutorialPending, Is.True);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            GameSession owned = session != null ? session : GameSession.Instance;
            if (owned != null)
            {
                owned.Network.Stop();
                Object.Destroy(owned.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Tutorial flow fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null;
            // Later fixtures start from a fresh opening that leads straight to the room list.
            ResetRunState(typeof(GameOpeningController), "ResetRunState");
            GameSession.SkipTutorialThisRun();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FinishedOpeningOpensThePracticeBoardOnceThenTheRoomList()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return null;
            var opening = Object.FindFirstObjectByType<GameOpeningController>();
            Assert.That(opening, Is.Not.Null);

            opening.lobbyView.enterButton.onClick.Invoke();
            yield return Until(() => opening.IsPlaying, "First Start opens the story");
            opening.player.Skip();
            yield return Until(() => !GameOpeningController.IsPresenting, "The opening hands over");
            Assert.That(session.IsTutorial, Is.True, "The opening hands over to the tutorial, not the room list.");
            Assert.That(session.RoomBrowserVisible, Is.False);
            Assert.That(GameSession.TutorialPending, Is.False, "Once per app run.");

            GameTutorialDirector director = null;
            yield return Until(() => (director = Object.FindFirstObjectByType<GameTutorialDirector>()) != null
                && director.IsShowingLine, "The practice board plays its first line", 30f);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Playing));
            Assert.That(session.StartOverlay == null || !session.StartOverlay.Visible, Is.True, "No countdown cover is left.");

            // Three seats: 금옥이 (P1) on the right, me (P2) in the middle holding the one drum, 묵이 (P3) on the left.
            LabNetwork network = session.Network;
            Assert.That(network.IsTutorial, Is.True);
            LabSnapshot snapshot = network.Snapshot;
            Assert.That(snapshot.players.OrderBy(player => player.seat).Select(player => player.id), Is.EqualTo(new[]
                { LabNetwork.TutorialRightSeatId, network.LocalPlayerId, LabNetwork.TutorialLeftSeatId }));
            Assert.That(snapshot.drums.Length, Is.EqualTo(1));
            Assert.That(snapshot.drums[0].owner, Is.EqualTo(network.LocalPlayerId));
            Assert.That(snapshot.remaining, Is.GreaterThan(3600d), "The hidden clock never runs out.");

            // HUD: the faces replace P1·P2·P3, the timer is hidden and the stage label reads 튜토리얼.
            GameTutorialArt art = GameTutorialArt.Load();
            var bindings = Object.FindFirstObjectByType<GameBattleBindings>();
            LabHud hud = bindings.hud;
            Assert.That(hud.localPlayerCircle.sprite, Is.SameAs(art.bul));
            Assert.That(hud.leftPlayerCircle.sprite, Is.SameAs(art.muk));
            Assert.That(hud.rightPlayerCircle.sprite, Is.SameAs(art.geumok));
            Assert.That(hud.timeText.transform.parent.gameObject.activeInHierarchy, Is.False);
            Assert.That(bindings.stageLabel.text, Is.EqualTo(GameTutorialScript.StageLabel));

            // Every tap shows the next line; the last line darkens around the gourd; then the room list.
            var tap = director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Tap To Continue");
            int lines = GameTutorialScript.Goal.Lines.Count;
            for (int line = 1; line < lines; line++)
            {
                Assert.That(director.LineIndex, Is.EqualTo(line - 1));
                yield return new WaitForSecondsRealtime(art.tapCooldown + .05f);
                tap.onClick.Invoke();
                Assert.That(director.LineIndex, Is.EqualTo(line), "Tap " + line);
            }
            yield return new WaitForSecondsRealtime(art.dimFadeSeconds + .1f);
            var dim = director.GetComponentsInChildren<CanvasGroup>(true).Single(group => group.name == "Dim");
            Assert.That(dim.alpha, Is.EqualTo(1f).Within(.01f), "The goal line lights only the gourd.");
            tap.onClick.Invoke();
            Assert.That(director.IsFinishing, Is.True);
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && session.RoomBrowserVisible, "The tutorial leaves for the room list", 30f);
            Assert.That(session.IsTutorial, Is.False);
            Assert.That(network.IsTutorial, Is.False);
            yield return Until(() => session.SceneFade == null || !session.SceneFade.Visible, "Home fades back in");
        }

        [UnityTest]
        public IEnumerator QuittingThePracticeBoardIsNoGameOverAndLeavesForTheRoomList()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return null;
            Assert.That(session.TryStartTutorial(), Is.True);
            Assert.That(session.TryStartTutorial(), Is.False, "Once per app run.");
            yield return Until(() => Object.FindFirstObjectByType<GameTutorialDirector>() is GameTutorialDirector director
                && director.IsShowingLine, "The practice board plays its first line", 30f);
            session.ExitRun();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home), "Quitting is not a Game Over.");
            yield return Until(() => !session.IsLoadingScene && session.RoomBrowserVisible, "Room list after quitting", 30f);
            Assert.That(session.IsTutorial, Is.False);
            Assert.That(session.TryStartTutorial(), Is.False, "The tutorial does not come back in the same run.");
        }

        private static void ResetRunState(Type owner, string method)
        {
            MethodInfo reset = owner.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null, owner.Name + "." + method);
            var initialize = (RuntimeInitializeOnLoadMethodAttribute)Attribute.GetCustomAttribute(reset,
                typeof(RuntimeInitializeOnLoadMethodAttribute));
            Assert.That(initialize, Is.Not.Null);
            Assert.That(initialize.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
            reset.Invoke(null, null);
        }

        private static IEnumerator Until(Func<bool> predicate, string message, float seconds = 15f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private static ushort AvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return checked((ushort)((IPEndPoint)socket.LocalEndPoint).Port);
            }
        }
    }
}
