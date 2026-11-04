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

            // 0단계: every tap shows the next line; the last line darkens around the gourd.
            var tap = director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Tap To Continue");
            var dim = director.GetComponentsInChildren<CanvasGroup>(true).Single(group => group.name == "Dim");
            LabOrbBoard board = bindings.board;
            int lines = GameTutorialScript.Goal.Lines.Count;
            for (int line = 1; line < lines; line++)
            {
                Assert.That(director.StepIndex, Is.EqualTo(0));
                Assert.That(director.BeatIndex, Is.EqualTo(line - 1));
                Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.None), "Lines keep the board still.");
                yield return new WaitForSecondsRealtime(art.tapCooldown + .05f);
                tap.onClick.Invoke();
                Assert.That(director.BeatIndex, Is.EqualTo(line), "Tap " + line);
            }
            yield return new WaitForSecondsRealtime(art.dimFadeSeconds + .1f);
            Assert.That(dim.alpha, Is.EqualTo(1f).Within(.01f), "The goal line lights only the gourd.");
            yield return TapThrough(director, tap, art);

            // 1단계: two lines over the drum, then three taps on the pinned drum make yin, yang, yin.
            Assert.That(director.StepIndex, Is.EqualTo(1));
            yield return TapThrough(director, tap, art);
            yield return TapThrough(director, tap, art);
            Assert.That(director.IsWaitingForAction, Is.True);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.TapDrum));
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.DrumTap), "Only the drum tap is open.");
            Assert.That(LocalPlayer(network).stamina, Is.EqualTo(network.config.StaminaMax).Within(.001f));
            for (int press = 0; press < GameStageSettings.TutorialDrumTaps; press++)
            {
                network.Generate();
                yield return Until(() => director.ActionProgress == press + 1, "Tap " + (press + 1) + " counted");
            }
            CollectionAssert.AreEqual(GameTutorialScript.DrumResults, network.Snapshot.orbs
                .Where(orb => orb.owner == network.LocalPlayerId).OrderBy(orb => orb.createdAt).Select(orb => orb.kind).ToArray());
            Assert.That(LocalPlayer(network).stamina, Is.LessThan(network.config.GenerateCost), "Three taps empty the gauge.");
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.None), "No fourth tap.");
            yield return Until(() => director.CurrentBeat.IsLine, "\"오, 나온다 나와!\" after 성공!");
            yield return TapThrough(director, tap, art);
            // While it is explained the gauge breathes back only a sliver, short of a tap, so the red cue shows on it.
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.Stamina));
            Assert.That(network.TutorialStaminaHeld, Is.True);
            yield return new WaitForSecondsRealtime(1f);
            float sliver = GameTutorialDirector.ShortStaminaFor(network.config.GenerateCost, art.shortStaminaShareOfTap);
            Assert.That(LocalPlayer(network).stamina, Is.EqualTo(sliver).Within(.01f), "Capped at a sliver.");
            Assert.That(sliver, Is.GreaterThan(0f).And.LessThan(network.config.GenerateCost));
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Cue, Is.EqualTo(GameTutorialCue.StaminaShort));
            yield return TapThrough(director, tap, art);
            // The refill is released and shown until full, then the tutorial leaves for the room list.
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.RefillStamina));
            Assert.That(network.TutorialStaminaHeld, Is.False);
            yield return Until(() => director.StepIndex == 2 && director.IsShowingLine, "The gauge refills and 2단계 starts", 15f);

            // 2단계: three lines over the orbs, then one yin dragged onto the yang makes fire and leaves one yin.
            Assert.That(board.KeepOutCount, Is.EqualTo(4), "Walls keep orbs off the three faces and the gauge.");
            // The three orbs always rest in the same places around the drum: yin left, yang right, yin just above the yang.
            LabOrbView drumView = Object.FindObjectsByType<LabOrbView>(FindObjectsSortMode.None).Single(view => view.Kind == LabOrbKind.Drum);
            Vector3[] rested = network.Snapshot.orbs.Where(orb => orb.owner == network.LocalPlayerId).OrderBy(orb => orb.createdAt)
                .Select(orb => Object.FindObjectsByType<LabOrbView>(FindObjectsSortMode.None).Single(view => view.OrbId == orb.id)
                    .transform.position - drumView.transform.position).ToArray();
            Assert.That(rested.Length, Is.EqualTo(3));
            Assert.That(rested[0].x < 0f, Is.True, "First yin left of the drum: " + rested[0]);
            Assert.That(rested[1].x > 0f, Is.True, "Yang right of the drum: " + rested[1]);
            Assert.That(rested[2].x > 0f && rested[2].y > rested[1].y, Is.True, "Second yin above the yang: " + rested[2]);
            Assert.That((rested[2] - rested[1]).magnitude, Is.LessThan((rested[0] - rested[1]).magnitude), "The yin beside the yang pairs.");
            for (int line = 0; line < 3; line++) yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.CombineOrbs));
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.OrbMove), "Only orbs move; no drum, no throw.");
            yield return null;
            Assert.That(director.GetComponentsInChildren<GameTutorialArrow>(true).Single().gameObject.activeSelf, Is.True,
                "The arrow points from a yin to the yang.");
            LabOrbView[] views = Object.FindObjectsByType<LabOrbView>(FindObjectsSortMode.None);
            LabOrbView yang = views.Single(view => view.Kind == LabOrbKind.Yang);
            LabOrbView yin = views.Where(view => view.Kind == LabOrbKind.Yin)
                .OrderBy(view => (view.transform.position - yang.transform.position).sqrMagnitude).First();
            double now = Time.realtimeSinceStartupAsDouble;
            Assert.That(board.TryBeginDragAtWorld(yin.transform.position, now), Is.True, "The yin can be picked up.");
            board.DragToWorld(yang.transform.position, now + .1d);
            Assert.That(board.EndDragAtWorld(yang.transform.position, now + .4d), Is.True);
            yield return Until(() => director.ActionProgress == 1, "Dropped on the yang, the pair combines");
            LabOrbState[] mine = network.Snapshot.orbs.Where(orb => orb.owner == network.LocalPlayerId).ToArray();
            Assert.That(mine.Count(orb => orb.kind == LabOrbKind.Combined && orb.element == GameTutorialScript.CombineResult),
                Is.EqualTo(1), "Always fire (red).");
            Assert.That(mine.Count(orb => orb.kind == LabOrbKind.Yin), Is.EqualTo(1), "One yin is kept for 4단계.");
            Assert.That(mine.Count(orb => orb.kind == LabOrbKind.Yang), Is.EqualTo(0));
            yield return Until(() => director.CurrentBeat.IsLine, "\"우와, 색이 변했어!\" after 성공!");
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.None), "The red orb and the yin show plainly.");
            for (int line = 0; line < 3; line++) yield return TapThrough(director, tap, art);

            // 3단계: flick the red orb up at the gourd. A miss gives it back from above with "다시 해 봐!".
            Assert.That(director.StepIndex, Is.EqualTo(3));
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.ThrowOrb));
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.Throw), "Only throwing is open.");
            for (int attempt = 0; attempt < 5 && director.IsWaitingForAction; attempt++)
            {
                LabOrbView fire = null;
                yield return Until(() => (fire = Object.FindObjectsByType<LabOrbView>(FindObjectsSortMode.None)
                    .FirstOrDefault(view => view.Kind == LabOrbKind.Combined && !board.IsDropping(view.OrbId))) != null,
                    "A red orb to throw");
                yield return new WaitForSecondsRealtime(.6f);
                int misses = director.ThrowMisses;
                Vector2 start = fire.transform.position;
                Vector2 flick = new Vector2(-.15f, 1f) * 1.5f;
                double began = Time.realtimeSinceStartupAsDouble;
                Assert.That(board.TryBeginDragAtWorld(start, began), Is.True, "The red orb can be picked up.");
                board.DragToWorld(start + flick * .5f, began + .04d);
                Assert.That(board.EndDragAtWorld(start + flick, began + .08d), Is.True);
                yield return Until(() => !director.IsWaitingForAction || director.ThrowMisses > misses, "The throw lands or misses", 10f);
                if (director.ThrowMisses > misses)
                {
                    Assert.That(director.IsShowingRemark, Is.True, "\"다시 해 봐!\"");
                    Assert.That(network.Snapshot.orbs.Count(orb => orb.owner == network.LocalPlayerId
                        && orb.kind == LabOrbKind.Combined && orb.element == GameTutorialScript.CombineResult), Is.EqualTo(1),
                        "The red orb is given back.");
                }
            }
            Assert.That(director.IsWaitingForAction, Is.False, "A throw hit the gourd within five tries.");
            Assert.That(LocalPlayer(network).hitTotal, Is.GreaterThanOrEqualTo(1));
            yield return Until(() => director.CurrentBeat.IsLine, "\"맞았다! 박에 색이 묻었어!\" after 성공!");
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.Progress), "Only the paint bar is lit.");
            yield return TapThrough(director, tap, art);
            Assert.That(director.IsFinishing, Is.True, "3단계 is the last step for now.");
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

        /// <summary>Wait out the tap cooldown, tap once, and check the next beat came.</summary>
        private static IEnumerator TapThrough(GameTutorialDirector director, UnityEngine.UI.Button tap, GameTutorialArt art)
        {
            Assert.That(director.IsShowingLine, Is.True, "A line is waiting for a tap.");
            int step = director.StepIndex, beat = director.BeatIndex;
            yield return new WaitForSecondsRealtime(art.tapCooldown + .05f);
            tap.onClick.Invoke();
            Assert.That(director.IsFinishing || director.StepIndex != step || director.BeatIndex == beat + 1, Is.True,
                "Tap moved on from step " + step + " beat " + beat);
        }

        private static LabPlayerState LocalPlayer(LabNetwork network) =>
            network.Snapshot.players.Single(player => player.id == network.LocalPlayerId);

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
