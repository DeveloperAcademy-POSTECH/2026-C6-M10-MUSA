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
    /// 로딩 화면 (#478) 2단계: GameSession이 공용 로딩 화면을 띄우는 순간. 앱 첫 화면(Home), 오프닝 → 튜토리얼(예전 "튜토리얼 준비 중"
    /// 가림막 대신), 튜토리얼 → 방 목록. 아무리 짧아도 0.8초는 보이고, 100%까지 찬 뒤 닫힌다.
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
        public IEnumerator TheAppsFirstScreenOpensBehindTheLoadingScreenForAtLeastEightTenths()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null, "The session is made with Home");
            session = GameSession.Instance;
            yield return null;
            Assert.That(session.IsLoadingScreenUp, Is.True, "Home's first frames are covered.");
            float shownAt = Time.realtimeSinceStartup;
            GameLoadingView view = session.LoadingView;
            Assert.That(view.RunnerName, Is.EqualTo("불이"));
            yield return Until(() => !session.IsLoadingScreenUp, "It closes once Home has drawn", 5f);
            Assert.That(Time.realtimeSinceStartup - shownAt, Is.GreaterThanOrEqualTo(.8f - .05f), "Never shorter than 0.8 s.");
            Assert.That(view.DisplayedProgress, Is.EqualTo(1f), "It filled to 100% before closing.");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
        }

        [UnityTest]
        public IEnumerator TheTutorialOpensAndLeavesBehindTheLoadingScreenNotItsOldCover()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return Until(() => !session.IsLoadingScreenUp, "The first screen's loading has closed", 5f);

            Assert.That(session.TryStartTutorial(), Is.True);
            yield return Until(() => session.IsLoadingScreenUp, "Going to the practice board shows the loading screen", 2f);
            bool oldCover = false, lineUnderLoading = false;
            GameTutorialDirector director = null;
            float until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until)
            {
                oldCover |= session.StartOverlay != null && session.StartOverlay.Visible;
                director = Object.FindFirstObjectByType<GameTutorialDirector>();
                lineUnderLoading |= director != null && director.IsShowingLine && session.IsLoadingScreenUp;
                if (director != null && director.IsShowingLine && !session.IsLoadingScreenUp) break;
                yield return null;
            }
            Assert.That(director != null && director.IsShowingLine, Is.True, "The first line comes");
            Assert.That(oldCover, Is.False, "No \"튜토리얼 준비 중\" cover any more.");
            Assert.That(lineUnderLoading, Is.False, "The first line waits for the loading screen to go.");
            Assert.That(session.LoadingView.DisplayedProgress, Is.EqualTo(1f));

            // Leaving for the room list goes behind it too.
            session.FinishTutorial();
            yield return Until(() => session.IsLoadingScreenUp, "Leaving the practice board shows the loading screen", 2f);
            yield return Until(() => !session.IsLoadingScreenUp && SceneManager.GetActiveScene().path == GameScenePaths.Home,
                "It closes once the room list is up", 30f);
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
