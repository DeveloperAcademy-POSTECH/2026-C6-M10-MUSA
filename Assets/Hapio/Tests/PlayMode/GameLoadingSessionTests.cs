using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// 로딩 화면 (#478) 2단계: GameSession이 공용 로딩 화면을 띄우는 순간. 튜토리얼 → 방 목록 같은 화면 전환에는 뜨고(아무리 짧아도
    /// 1초, 100%까지 찬 뒤 닫힘), 앱을 켜서 첫 화면이 뜰 때와 오프닝 → 튜토리얼에는 뜨지 않는다(사용자 결정). 오프닝 → 튜토리얼은
    /// 따로 만들 때까지 예전 "튜토리얼 준비 중" 가림막이 덮는다.
    /// </summary>
    public sealed class GameLoadingSessionTests
    {
        private GameSession session;

        [UnitySetUp]
        public IEnumerator StartANewAppRun()
        {
            Assert.That(GameSession.Instance, Is.Null, "The preceding fixture must release its persistent session.");
            ResetRunState(typeof(GameOpeningController), "ResetRunState");
            ResetRunState(typeof(GameSession), "ResetTutorialRun");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameSession owned = session != null ? session : GameSession.Instance;
            if (owned != null)
            {
                owned.Network.Stop();
                Object.Destroy(owned.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Loading session fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null;
            ResetRunState(typeof(GameOpeningController), "ResetRunState");
            GameSession.SkipTutorialThisRun();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheAppsFirstScreenShowsAtOnceWithoutTheLoadingScreen()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null, "The session is made with Home");
            session = GameSession.Instance;
            Assert.That(session.LoadingView, Is.Not.Null, "The shared loading screen is ready for later.");
            float until = Time.realtimeSinceStartup + 1.2f;
            bool shown = false;
            while (Time.realtimeSinceStartup < until)
            {
                shown |= session.IsLoadingScreenUp;
                yield return null;
            }
            Assert.That(shown, Is.False, "The main screen comes straight away.");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
        }

        [UnityTest]
        public IEnumerator TheTutorialOpensWithoutItButLeavesBehindItForAtLeastASecond()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return null;

            // The opening's hand-over to the practice board gets its own transition later: no loading screen, only the old
            // "튜토리얼 준비 중" cover for now.
            Assert.That(session.TryStartTutorial(), Is.True);
            bool shown = false, oldCover = false, coverText = false;
            GameTutorialDirector director = null;
            float until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until)
            {
                shown |= session.IsLoadingScreenUp;
                oldCover |= session.StartOverlay != null && session.StartOverlay.Visible;
                coverText |= session.StartOverlay != null && session.StartOverlay.Visible
                    && session.StartOverlay.DisplayText == "튜토리얼 준비 중…";
                director = Object.FindFirstObjectByType<GameTutorialDirector>();
                if (director != null && director.IsShowingLine) break;
                yield return null;
            }
            Assert.That(director != null && director.IsShowingLine, Is.True, "The first line comes");
            Assert.That(shown, Is.False, "No loading screen between the opening and the tutorial.");
            Assert.That(oldCover && coverText, Is.True, "The old \"튜토리얼 준비 중\" cover covers it for now.");
            Assert.That(session.StartOverlay.Visible, Is.False, "And it is gone once the first line is up.");

            // Leaving for the room list goes behind it, for at least a second, filled to 100%.
            session.FinishTutorial();
            yield return Until(() => session.IsLoadingScreenUp, "Leaving the practice board shows the loading screen", 2f);
            float shownAt = Time.realtimeSinceStartup;
            Assert.That(Array.ConvertAll(GameLoadingArt.Load().runners, runner => runner.name), Has.Member(session.LoadingView.RunnerName),
                "One of the runners, at random.");
            yield return Until(() => !session.IsLoadingScreenUp && SceneManager.GetActiveScene().path == GameScenePaths.Home,
                "It closes once the room list is up", 30f);
            Assert.That(Time.realtimeSinceStartup - shownAt, Is.GreaterThanOrEqualTo(1f - .05f), "Never shorter than a second.");
            Assert.That(session.LoadingView.DisplayedProgress, Is.EqualTo(1f), "It filled to 100% before closing.");
            Assert.That(session.RoomBrowserVisible, Is.True);
        }

        [Test]
        public void LoadingScreenComesEverywhereButBetweenTheScreensOfOneRun()
        {
            string home = GameScenePaths.Home, lobby = GameScenePaths.Lobby, battle = GameScenePaths.Battle,
                result = GameScenePaths.StageResult, over = GameScenePaths.GameOver;
            foreach ((string from, string to) in new[]
                { (home, lobby), (lobby, home), (lobby, battle), (home, battle), (battle, home), (result, home), (over, home),
                  (over, lobby), (result, lobby), (battle, battle) })
                Assert.That(GameSession.ShowsLoading(from, to), Is.True, from + " → " + to);
            foreach ((string from, string to) in new[] { (battle, result), (result, battle), (battle, over), (result, over) })
                Assert.That(GameSession.ShowsLoading(from, to), Is.False, from + " → " + to + " stays inside the run");
            Assert.That(GameSession.ShowsLoading(home, battle, tutorialStarting: true), Is.False,
                "The opening → tutorial gets its own transition later.");
        }

        private static void ResetRunState(Type owner, string method)
        {
            MethodInfo reset = owner.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null, owner.Name + "." + method);
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
