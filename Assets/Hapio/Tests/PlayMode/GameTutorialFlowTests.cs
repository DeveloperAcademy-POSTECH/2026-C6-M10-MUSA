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
    [Category("Slow")]
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
            // #621: the original tutorial board is alpha-composited over the continuous stage view.
            yield return null;
            var boardView = bindings.GetComponent<GameTutorialBoardPresentation>();
            var composition = hud.GetComponent<LabOrbHudComposition>();
            var plate = bindings.board.transform.Find("OrbBoardPlate").GetComponent<Renderer>();
            Assert.That(boardView, Is.Not.Null);
            Assert.That(composition.IsCompositing, Is.True, composition.InitializationError);
            Assert.That(composition.presentationBackdrop, Is.SameAs(boardView.Artwork.rectTransform));
            Assert.That(boardView.Artwork.texture, Is.SameAs(plate.sharedMaterial.mainTexture), "Keep the tutorial's original plate.");
            Assert.That(boardView.Artwork.enabled, Is.True);
            Assert.That(boardView.Artwork.raycastTarget, Is.False);
            Assert.That(plate.enabled, Is.False, "No duplicate plate behind the composited artwork.");
            Assert.That(bindings.sceneController.orbCamera.enabled, Is.False, "No rectangular lower-camera paper pass.");
            Rect boardRect = ScreenRect(boardView.Artwork.rectTransform);
            Vector3 projected = bindings.sceneController.orbCamera.WorldToScreenPoint(plate.bounds.center);
            Assert.That(Vector2.Distance(boardRect.center, projected), Is.LessThan(1f), "Visual stays on the original input board.");
            Assert.That(boardView.Artwork.transform.GetSiblingIndex(), Is.LessThan(hud.leftNeighborBadge.transform.GetSiblingIndex()));
            var continuation = bindings.GetComponent<GameBoardBackgroundContinuation>().ContinuationCamera;
            Assert.That(continuation != null && continuation.enabled, Is.True);
            Ray upper = bindings.battleCamera.ViewportPointToRay(new Vector3(.37f,0,0));
            Ray lower = continuation.ViewportPointToRay(new Vector3(.37f,1,0));
            Assert.That(Vector3.Distance(upper.direction,lower.direction), Is.LessThan(.00001f), "Background joins without a viewport seam.");
            Assert.That(hud.localPlayerCircle.sprite, Is.SameAs(art.bul));
            Assert.That(hud.leftPlayerCircle.sprite, Is.SameAs(art.muk));
            Assert.That(hud.rightPlayerCircle.sprite, Is.SameAs(art.geumok));
            Assert.That(hud.timeText.transform.parent.gameObject.activeInHierarchy, Is.False);
            Assert.That(bindings.stageLabel.text, Is.EqualTo(GameTutorialScript.StageLabel));
            // Each face has its name just under it: 금옥 (P1, right), 불이 (P2, me), 묵이 (P3, left).
            yield return null;
            string[] names = { "금옥", "불이", "묵이" };
            for (int seat = 0; seat < 3; seat++)
            {
                UnityEngine.UI.Text name = director.NameLabelAt(seat);
                Assert.That(name.gameObject.activeInHierarchy, Is.True, "Name " + (seat + 1));
                Assert.That(name.text, Is.EqualTo(names[seat]));
                UnityEngine.UI.Image faceImage = seat == 0 ? hud.rightPlayerCircle : seat == 1 ? hud.localPlayerCircle : hud.leftPlayerCircle;
                Rect face = ScreenRect(faceImage.rectTransform), label = ScreenRect(name.rectTransform);
                Assert.That(Mathf.Abs(label.center.x - face.center.x), Is.LessThan(1f), names[seat] + " is centered under the face.");
                // Held at the bottom it sits nameScreenMargin above the screen's edge, scaled like the label itself.
                float held = (art.nameScreenMargin + name.rectTransform.rect.height) * label.height / name.rectTransform.rect.height;
                Assert.That(label.yMax, Is.LessThanOrEqualTo(face.yMin + 1f).Or.LessThanOrEqualTo(held + 1f),
                    names[seat] + " is under the face (or held at the bottom of the screen).");
                Assert.That(label.yMin, Is.GreaterThanOrEqualTo(-1f), names[seat] + " stays on screen.");
            }
            // Seen from the middle seat, the gourd's yokai is turned round to face me; its white body is not.
            Assert.That(director.YokaiFacesMe, Is.True, "The yokai looks at me, not at P1.");
            Assert.That(director.YokaiTurnDegrees, Is.EqualTo(120f).Within(.01f));
            Transform shellBody = bindings.target.MountedVisual.transform.Find("White Gourd");
            Assert.That(shellBody == null || Quaternion.Angle(shellBody.localRotation, Quaternion.identity) < .01f, Is.True,
                "The painted shell stays as it is.");

            // 0단계: every tap shows the next line; the last line darkens around the gourd.
            var tap = director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Tap To Continue");
            var dim = director.GetComponentsInChildren<CanvasGroup>(true).Single(group => group.name == "Dim");
            LabOrbBoard board = bindings.board;
            int lines = GameTutorialScript.Goal.Lines.Count;
            // The ▽ comes once a tap moves the line on: after the bubble has popped and the cooldown is over.
            yield return Until(() => director.IsShowingTapHint, "The ▽ shows on the first line", 2f);
            Assert.That((director.HasTappedOnce, director.SkipButtonAlpha), Is.EqualTo((false, 0f)), "No skip button before a tap.");
            tap.onClick.Invoke();
            Assert.That(director.BeatIndex, Is.EqualTo(1), "A tap with the ▽ up moves on.");
            Assert.That(director.IsShowingTapHint, Is.False, "The next line starts without it.");
            yield return Until(() => director.IsShowingTapHint, "and shows it again once it can be tapped", 2f);
            // The first tap brings 건너뛰기 in, fading over half a second.
            Assert.That(director.HasTappedOnce, Is.True);
            yield return Until(() => director.SkipButtonAlpha >= .999f, "건너뛰기 has faded in", art.skipFadeSeconds + 1f);
            var buttons = director.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var skip = buttons.Single(button => button.name == "Skip Button");
            var stay = buttons.Single(button => button.name == "Keep Learning");
            // Open over a line: the line and its ▽ go behind the dialog and a tap does not move on.
            skip.onClick.Invoke();
            Assert.That(director.IsSkipDialogOpen, Is.True);
            yield return new WaitForSecondsRealtime(art.popSeconds + .1f);
            // #631: the window and the two buttons under it sit, as one block, on the screen's middle.
            var skipDialog = director.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Skip Dialog");
            Rect skipWindow = ScreenRect((RectTransform)skipDialog.Find("Window")), stayRect = ScreenRect((RectTransform)stay.transform);
            Assert.That((skipWindow.yMax + stayRect.yMin) * .5f, Is.EqualTo(ScreenRect(skipDialog).center.y).Within(1f),
                "The skip window and its buttons are centred top to bottom.");
            Assert.That(director.IsShowingLine, Is.False, "The bubble waits behind the dialog.");
            tap.onClick.Invoke();
            Assert.That(director.BeatIndex, Is.EqualTo(1), "No tap gets past the dialog.");
            stay.onClick.Invoke();
            yield return Until(() => !director.IsSkipDialogOpen, "계속 배우기 closes it quickly", 1f);
            Assert.That((director.StepIndex, director.BeatIndex), Is.EqualTo((0, 1)), "Back where it was.");
            Assert.That(director.IsShowingLine, Is.True, "The same line again.");
            Assert.That(director.Skipped, Is.False);
            // The pause button works over a line too: the tutorial steps aside for the usual pause menu, then comes back.
            var pause = buttons.Single(button => button.name == "Pause");
            yield return null;
            Assert.That(pause.gameObject.activeInHierarchy, Is.True, "An invisible press lies over the HUD pause button.");
            pause.onClick.Invoke();
            yield return Until(() => network.IsBattlePaused && director.IsSteppedAsideForPause, "Paused from a line", 5f);
            var overlayGroup = director.GetComponentsInChildren<CanvasGroup>(true).Single(group => group.name == "Tutorial Overlay");
            Assert.That((overlayGroup.alpha, overlayGroup.blocksRaycasts), Is.EqualTo((0f, false)),
                "The tutorial screen is clear and lets every touch through to the pause menu.");
            Assert.That(hud.pauseMenu.activeInHierarchy, Is.True);
            Assert.That(hud.developerSettingsButton == null || !hud.developerSettingsButton.gameObject.activeInHierarchy, Is.True,
                "No DEV restart from the tutorial.");
            yield return new WaitForSecondsRealtime(1f);
            Assert.That((director.StepIndex, director.BeatIndex), Is.EqualTo((0, 1)), "Nothing moved on while paused.");
            hud.resumeButton.onClick.Invoke();
            yield return Until(() => !network.IsBattlePaused && !director.IsSteppedAsideForPause, "Back from the pause menu", 10f);
            Assert.That((overlayGroup.alpha, overlayGroup.blocksRaycasts), Is.EqualTo((1f, true)));
            Assert.That(director.IsShowingLine, Is.True, "The same line again.");
            // #552 (undoing #536): a tap anywhere on the screen moves a line on, and the board stays still under it.
            Rect tapArea = ScreenRect(tap.GetComponent<RectTransform>());
            Assert.That(tapArea.width, Is.EqualTo(Screen.width).Within(1f), "The whole screen across.");
            Assert.That(tapArea.height, Is.EqualTo(Screen.height).Within(1f), "And from top to bottom.");
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.None), "The board stays still under the line.");
            for (int line = 2; line < lines; line++)
            {
                Assert.That(director.StepIndex, Is.EqualTo(0));
                Assert.That(director.BeatIndex, Is.EqualTo(line - 1));
                Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.None), "Lines keep the board still.");
                yield return WaitForTapToCount(director, art);
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
            Assert.That(director.IsShowingTapHint, Is.False, "No ▽ while an action is awaited.");
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.TapDrum));
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.DrumTap), "Only the drum tap is open.");
            Assert.That(LocalPlayer(network).stamina, Is.EqualTo(network.config.StaminaMax).Within(.001f));
            for (int press = 0; press < GameStageSettings.TutorialDrumTaps; press++)
            {
                network.Generate();
                yield return Until(() => director.ActionProgress == press + 1, "Tap " + (press + 1) + " counted");
            }
            // Done: the banner goes and a white 성공! (black stroke) pops up where it was.
            var guide = director.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Guide Banner");
            var success = director.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(text => text.name == "Success");
            Assert.That((guide.gameObject.activeSelf, success.gameObject.activeSelf), Is.EqualTo((false, true)));
            Assert.That(Vector3.Distance(success.rectTransform.position, guide.position), Is.LessThan(.01f), "In the banner's place.");
            Assert.That(success.color, Is.EqualTo(Color.white));
            // 성공! is up; with the dialog open its wait does not run out, however long the dialog stays.
            skip.onClick.Invoke();
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.None), "The board waits behind the dialog.");
            yield return new WaitForSecondsRealtime(art.afterActionSeconds + .8f);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.TapDrum), "The tutorial clock stood still.");
            stay.onClick.Invoke();
            yield return Until(() => !director.IsSkipDialogOpen, "계속 배우기 closes it", 1f);
            CollectionAssert.AreEqual(GameTutorialScript.DrumResults, network.Snapshot.orbs
                .Where(orb => orb.owner == network.LocalPlayerId).OrderBy(orb => orb.createdAt).Select(orb => orb.kind).ToArray());
            Assert.That(LocalPlayer(network).stamina, Is.LessThan(network.config.GenerateCost), "Three taps empty the gauge.");
            Assert.That(network.TutorialPassAllowed(LabOrbKind.Yin) || network.TutorialPassAllowed(LabOrbKind.Yang), Is.False,
                "Nothing crosses to a neighbor while tapping.");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(network.Snapshot.orbs.Count(orb => orb.owner == network.LocalPlayerId), Is.EqualTo(3),
                "All three orbs stay on my board (none slid over to 묵이 or 금옥이).");
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
            Assert.That(board.KeepOutCount, Is.EqualTo(7), "Walls keep orbs off the three faces, their names and the gauge.");
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
            Assert.That(guide.gameObject.activeSelf, Is.True);
            Assert.That(guide.anchorMin.y, Is.EqualTo(1f - art.bannerTopFraction).Within(1e-4f),
                "Throwing too: every banner, and its 성공!, sits in the one place.");
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
                // In board widths, like the throw gesture itself (.2 > ThrowSwipeMinDistance .12), whatever the screen's shape.
                Vector2 flick = new Vector2(-.15f, 1f) * (board.CenterBounds.width * .2f);
                double began = Time.realtimeSinceStartupAsDouble;
                Assert.That(board.TryBeginDragAtWorld(start, began), Is.True, "The red orb can be picked up.");
                board.DragToWorld(start + flick * .5f, began + .04d);
                Assert.That(board.EndDragAtWorld(start + flick, began + .08d), Is.True);
                yield return Until(() => !director.IsWaitingForAction || director.ThrowMisses > misses, "The throw lands or misses", 10f);
                if (director.ThrowMisses > misses)
                {
                    Assert.That(director.IsShowingRemark, Is.True, "\"다시 해 봐!\"");
                    Assert.That(director.IsShowingTapHint, Is.False, "A remark needs no tap: no ▽.");
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

            // 4단계: the drum goes to 묵이 by mistake and comes straight back, then right to 금옥이, who returns it with a yang.
            Assert.That(director.StepIndex, Is.EqualTo(4));
            yield return TapThrough(director, tap, art);
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.PassDrum));
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.DrumMove), "Only the drum moves.");
            yield return FlickDrum(board, -1f);
            yield return Until(() => director.WrongSidePasses == 1, "The drum reaches 묵이 on the left", 10f);
            Assert.That(director.IsShowingRemark, Is.True, "\"이건 금옥이한테 줘.\"");
            yield return Until(() => network.Snapshot.drums[0].owner == network.LocalPlayerId, "묵이 hands it straight back", 10f);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(director.IsWaitingForAction, Is.True, "Still waiting for the pass to 금옥이.");
            yield return FlickDrum(board, 1f);
            yield return Until(() => director.IsShowingMirror, "금옥이's mirror shows the drum arriving", 10f);
            Assert.That(network.Snapshot.drums[0].owner, Is.EqualTo(LabNetwork.TutorialRightSeatId));
            yield return Until(() => director.CurrentBeat.IsLine, "\"고마워!\" after the mirror", 10f);
            yield return Until(() => network.Snapshot.drums[0].owner == network.LocalPlayerId
                && network.Snapshot.orbs.Any(orb => orb.owner == network.LocalPlayerId && orb.kind == LabOrbKind.Yang),
                "The yang and the drum come back", 10f);
            Assert.That(network.Snapshot.orbs.Count(orb => orb.owner == network.LocalPlayerId && orb.kind == LabOrbKind.Yin),
                Is.EqualTo(1), "The yin left from 2단계 is still there.");
            yield return TapThrough(director, tap, art);
            yield return TapThrough(director, tap, art);
            // Where the drum is: only the map at the top right is lit, and 금옥이's bubble does not cover it.
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.Minimap));
            yield return new WaitForSecondsRealtime(art.dimFadeSeconds + art.popSeconds + .1f);
            Assert.That(dim.alpha, Is.EqualTo(1f).Within(.01f), "The rest is dark.");
            var map = Object.FindFirstObjectByType<LabDrumMinimap>();
            var bubble = director.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Bubble");
            Assert.That(ScreenRect(bubble).Overlaps(ScreenRect(map.Panel)), Is.False, "The map stays in plain sight.");
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Speaker, Is.EqualTo(GameTutorialSpeaker.Bul));
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.Minimap), "Still lit as 불이 answers.");
            yield return TapThrough(director, tap, art);

            // #552: "어? 구슬이 빛나는데?" — the yin left from 2단계 starts its last flashes and keeps glowing while the line is up.
            Assert.That(director.CurrentBeat.Cue, Is.EqualTo(GameTutorialCue.OrbGlow));
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.FadingOrb), "Only that orb is lit.");
            string fading = director.FadeDemoOrbId;
            Assert.That(fading, Is.Not.Null, "An orb was picked for the demo.");
            Assert.That(network.Snapshot.orbs.Single(orb => orb.id == fading).kind, Is.EqualTo(LabOrbKind.Yin), "The yin left from 2단계.");
            string keptYang = network.Snapshot.orbs.Single(orb => orb.owner == network.LocalPlayerId && orb.kind == LabOrbKind.Yang).id;
            LabOrbView fadingView = Object.FindObjectsByType<LabOrbView>(FindObjectsSortMode.None).Single(view => view.OrbId == fading);
            yield return new WaitForSecondsRealtime(4f);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == fading), Is.True, "Read slowly, it is still there.");
            Assert.That(fadingView.ExpiryWarning, Is.True, "Flashing white like an orb in its last seconds (#235).");
            string vanished = null;
            board.OrbVanished += id => vanished = id;
            yield return TapThrough(director, tap, art);

            // "…빛이 나기 시작하면 곧 없어져!" — it runs out in smoke; the line waits for that.
            Assert.That(director.CurrentBeat.Cue, Is.EqualTo(GameTutorialCue.OrbVanish));
            yield return new WaitForSecondsRealtime(art.tapCooldown + .05f);
            tap.onClick.Invoke();
            Assert.That(director.CurrentBeat.Cue, Is.EqualTo(GameTutorialCue.OrbVanish), "No moving on before it has gone.");
            yield return Until(() => vanished == fading, "It goes up in smoke like a real run-out", 8f);
            Assert.That(director.FadeDemoGone, Is.True);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == keptYang), Is.True, "The yang is untouched: nothing else runs out.");
            yield return new WaitForSecondsRealtime(art.popSeconds);
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.None));
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Speaker, Is.EqualTo(GameTutorialSpeaker.Bul), "\"오호! 알겠어\"");
            yield return TapThrough(director, tap, art);

            // 5단계: the gourd drops a two-lock bomb; I push it right to 금옥이, she sends it back unlocked and it opens
            // into two elemental orbs on my board.
            Assert.That(director.StepIndex, Is.EqualTo(5));
            Assert.That(director.CurrentBeat.Cue, Is.EqualTo(GameTutorialCue.DropBomb));
            LabOrbState dropped = null;
            yield return Until(() => (dropped = network.Snapshot.orbs.FirstOrDefault(orb => orb.kind == LabOrbKind.Bomb)) != null,
                "The gourd drops a bomb", 10f);
            Assert.That((dropped.owner, dropped.lockSwipes), Is.EqualTo((network.LocalPlayerId, GameTutorialScript.BombLocks)));
            yield return Until(() => board.IsDropping(dropped.id), "It falls in from the gourd", 2f);
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Focus, Is.EqualTo(GameTutorialFocus.Bomb), "The bomb is lit.");
            yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.PassBomb));
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.OrbMove), "Orbs move; the drum stays, nothing is thrown.");
            for (int attempt = 0; attempt < 4 && director.IsWaitingForAction; attempt++)
            {
                LabOrbView bombView = null;
                yield return Until(() => (bombView = Object.FindObjectsByType<LabOrbView>(FindObjectsSortMode.None)
                    .FirstOrDefault(view => view.Kind == LabOrbKind.Bomb && !board.IsDropping(view.OrbId))) != null,
                    "The bomb has landed on my board");
                yield return new WaitForSecondsRealtime(.4f);
                yield return DragOffSide(board, bombView, 1f);
                // A short push leaves it sliding on my board; give it time to cross or come to rest before trying again.
                float pushed = Time.realtimeSinceStartup;
                while (director.IsWaitingForAction && Time.realtimeSinceStartup - pushed < 2.5f) yield return null;
            }
            Assert.That(director.IsWaitingForAction, Is.False, "The bomb reached 금옥이 within four pushes.");
            LabOrbState atGeumok = network.Snapshot.orbs.Single(orb => orb.kind == LabOrbKind.Bomb);
            Assert.That((atGeumok.owner, atGeumok.lockSwipes), Is.EqualTo((LabNetwork.TutorialRightSeatId, 1)), "One lock off.");
            yield return Until(() => director.CurrentBeat.IsLine, "\"앗 뜨거! 도로 받아!\" after 성공!", 10f);
            Assert.That(director.CurrentBeat.Cue, Is.EqualTo(GameTutorialCue.ReturnBomb));
            yield return Until(() => network.Snapshot.orbs.Any(orb => orb.kind == LabOrbKind.Bomb
                && orb.owner == network.LocalPlayerId && orb.lockSwipes == 0), "금옥이 sends it back, the last lock off", 10f);
            // Unlocked (rainbow) it rests on my board but does not open while "앗 뜨거!" is up, however long.
            yield return new WaitForSecondsRealtime(2.5f);
            LabOrbState waiting = network.Snapshot.orbs.Single(orb => orb.kind == LabOrbKind.Bomb);
            Assert.That((waiting.owner, waiting.lockSwipes), Is.EqualTo((network.LocalPlayerId, 0)), "Still a bomb, unopened.");
            Assert.That((board.BombOpeningHeld, board.IsBombAtRest(waiting.id)), Is.EqualTo((true, true)));
            yield return TapThrough(director, tap, art);
            yield return Until(() => director.CurrentBeat.IsLine, "\"풀렸다!\" once it is back and still", 15f);
            Assert.That(director.CurrentBeat.Cue, Is.EqualTo(GameTutorialCue.OpenBomb));
            // With "풀렸다!" it opens into its two attack orbs.
            yield return Until(() => !network.Snapshot.orbs.Any(orb => orb.kind == LabOrbKind.Bomb), "It opens with \"풀렸다!\"", 3f);
            LabOrbState[] opened = network.Snapshot.orbs.Where(orb => orb.owner == network.LocalPlayerId).ToArray();
            Assert.That(opened.Count(orb => orb.kind == LabOrbKind.Combined && orb.element != LabOrbElement.None),
                Is.GreaterThanOrEqualTo(2), "Two elemental orbs for 6단계.");
            for (int line = 0; line < 3; line++) yield return TapThrough(director, tap, art);

            // 6단계: the yokai shuts my right edge; it flashes red before anyone speaks. Pushed right the drum bounces off;
            // passed left to 묵이, the block lifts and the four say their last words.
            Assert.That(director.StepIndex, Is.EqualTo(6));
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.WatchBlock));
            yield return Until(() => board.BlockedSide == 1, "The right edge is shut", 2f);
            Assert.That(board.EdgeBlock.Opening, Is.True, "It opens with its red flashes.");
            Assert.That(board.EdgeBlock.SecondsText, Is.Empty, "The held block writes no countdown.");
            Assert.That(director.IsShowingLine, Is.False, "No bubble over the flashes.");
            yield return Until(() => director.IsShowingLine, "\"어? 오른쪽이 빨개졌어!\" after the flashes", 5f);
            Assert.That(board.EdgeBlock.Opening, Is.False);
            for (int line = 0; line < 4; line++) yield return TapThrough(director, tap, art);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.PassDrumLeft));
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.DrumMove), "Only the drum moves.");

            // 기록 opens in the middle of an action: the board stops, and closing hands the same action back (the push below uses it).
            var logOpener = director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Log Button");
            var logCloser = director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Log Back");
            logOpener.onClick.Invoke();
            Assert.That(director.IsDialogueLogOpen, Is.True, "기록 opens during an action.");
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.None), "The board stops behind the log.");
            yield return new WaitForSecondsRealtime(art.dimFadeSeconds + .1f);
            Assert.That(director.IsWaitingForAction, Is.True, "The action is still to do.");
            logCloser.onClick.Invoke();
            yield return Until(() => !director.IsDialogueLogOpen, "뒤로 가기 during the action", 2f);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.PassDrumLeft), "The same action again.");
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.DrumMove), "The same gestures again.");
            int bounces = board.BlockBounces;
            yield return FlickDrum(board, 1f);
            yield return Until(() => board.BlockBounces > bounces, "Pushed right, the drum bounces off the red edge", 5f);
            Assert.That(network.Snapshot.drums[0].owner, Is.Not.EqualTo(LabNetwork.TutorialRightSeatId), "Nothing reached 금옥이.");
            // A hard push can carry the bounced drum all the way to 묵이; otherwise push it left.
            yield return new WaitForSecondsRealtime(1.5f);
            if (director.IsWaitingForAction) yield return FlickDrum(board, -1f);
            yield return Until(() => !director.IsWaitingForAction, "The drum reaches 묵이", 10f);

            // Behind the log the tutorial's clock stops: 성공! outlasts its own wait, and the next line comes after closing.
            logOpener.onClick.Invoke();
            Assert.That(director.IsDialogueLogOpen, Is.True, "기록 opens over 성공!.");
            yield return new WaitForSecondsRealtime(art.afterActionSeconds + .3f);
            Assert.That(director.CurrentBeat.Action, Is.EqualTo(GameTutorialAction.PassDrumLeft), "성공! waits behind the log.");
            logCloser.onClick.Invoke();
            yield return Until(() => !director.IsDialogueLogOpen, "뒤로 가기 over 성공!", 2f);
            Assert.That(network.Snapshot.drums[0].owner, Is.EqualTo(LabNetwork.TutorialLeftSeatId));
            yield return Until(() => board.BlockedSide == 0, "The block lifts once 묵이 has the drum", 2f);
            Assert.That(network.Snapshot.SwipeBlockOn(network.LocalPlayerId), Is.False);
            yield return Until(() => director.CurrentBeat.IsLine, "\"받았어. 고마워.\" after 성공!", 10f);
            Assert.That(director.CurrentBeat.Speaker, Is.EqualTo(GameTutorialSpeaker.Muk));
            for (int line = 0; line < 5; line++) yield return TapThrough(director, tap, art);
            Assert.That(director.IsFinishing, Is.True, "\"가보자!\" ends the tutorial.");
            // Every line said, in order, and only the script's lines: no remark, guide band or 성공!.
            Assert.That(director.DialogueLog.Select(entry => (entry.Speaker, entry.Text)).ToArray(), Is.EqualTo(
                GameTutorialScript.Steps.SelectMany(part => part.Beats).Where(said => said.IsLine)
                    .Select(said => (said.Speaker, said.Text)).ToArray()), "The whole script, nothing else.");
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
            yield return Until(() => !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.Home,
                "Home after quitting", 30f);
            Assert.That(session.RoomBrowserVisible, Is.False, "The pause menu's 나가기 goes back to the title screen.");
            Assert.That(session.IsTutorial, Is.False);
            Assert.That(session.TryStartTutorial(), Is.False, "The tutorial does not come back in the same run.");
        }

        [UnityTest]
        public IEnumerator LinesMovedOnFromGoIntoTheLogAndItsButtonComesUnderSkip()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return null;
            Assert.That(session.TryStartTutorial(), Is.True);
            GameTutorialDirector director = null;
            yield return Until(() => (director = Object.FindFirstObjectByType<GameTutorialDirector>()) != null
                && director.IsShowingLine, "The practice board plays its first line", 30f);
            GameTutorialArt art = GameTutorialArt.Load();
            var buttons = director.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var tap = buttons.Single(button => button.name == "Tap To Continue");
            var skip = buttons.Single(button => button.name == "Skip Button");
            var log = buttons.Single(button => button.name == "Log Button");

            // The first line is only on the screen: nothing has been moved on from yet.
            yield return WaitForTapToCount(director, art);
            Assert.That(director.DialogueLog, Is.Empty);
            Assert.That(log.gameObject.activeInHierarchy, Is.False, "No tap yet, no button.");

            tap.onClick.Invoke();
            GameTutorialBeat[] lines = GameTutorialScript.Goal.Lines.ToArray();
            Assert.That(director.DialogueLog.Select(entry => (entry.Speaker, entry.Text)).ToArray(),
                Is.EqualTo(new[] { (lines[0].Speaker, lines[0].Text) }), "The line just moved on from.");
            yield return Until(() =>
            {
                Assert.That(director.LogButtonAlpha, Is.EqualTo(director.SkipButtonAlpha).Within(.001f), "기록 fades in together with 건너뛰기.");
                return director.LogButtonAlpha >= .999f;
            }, "기록 fades in with 건너뛰기", art.skipFadeSeconds + 1f);
            Rect skipRect = ScreenRect((RectTransform)skip.transform), logRect = ScreenRect((RectTransform)log.transform);
            float scale = skipRect.width / art.skipButtonSize.x;
            Assert.That(logRect.center.x, Is.EqualTo(skipRect.center.x).Within(1f), "On 건너뛰기's middle.");
            Assert.That(skipRect.yMin - logRect.yMax, Is.EqualTo(art.logButtonGap * scale).Within(1f), "Right under it.");
            Assert.That(logRect.height, Is.EqualTo(skipRect.height).Within(1f), "As tall as it.");
            var logFrame = log.GetComponent<UnityEngine.UI.Image>();
            var logLabel = log.GetComponentInChildren<UnityEngine.UI.Text>(true);
            Assert.That(logFrame.sprite, Is.SameAs(skip.GetComponent<UnityEngine.UI.Image>().sprite), "The same frame as 건너뛰기.");
            Assert.That(logFrame.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
            Assert.That(logLabel.text, Is.EqualTo("기록"));
            Assert.That(logLabel.font, Is.SameAs(skip.GetComponentInChildren<UnityEngine.UI.Text>(true).font));

            // A touch on it lands on the button, not on whatever moves the line on.
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            log.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>().Raycast(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = logRect.center }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject, Is.SameAs(log.gameObject), "The button takes the touch.");

            yield return WaitForTapToCount(director, art);
            tap.onClick.Invoke();
            Assert.That(director.DialogueLog.Select(entry => entry.Text).ToArray(),
                Is.EqualTo(new[] { lines[0].Text, lines[1].Text }), "Oldest first.");

            // Pressing 기록: the screen darkens over everything and the tutorial waits, showing what has been said.
            var dim = director.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Log Screen");
            var bar = dim.GetComponentInChildren<UnityEngine.UI.Scrollbar>(true);
            var back = buttons.Single(button => button.name == "Log Back");
            LabOrbBoard board = Object.FindFirstObjectByType<GameBattleBindings>().board;
            log.onClick.Invoke();
            Assert.That(director.IsDialogueLogOpen, Is.True);
            yield return new WaitForSecondsRealtime(art.dimFadeSeconds + .1f);
            Assert.That(dim.gameObject.activeInHierarchy, Is.True);
            Assert.That(director.DialogueLogShownLines.ToArray(),
                Is.EqualTo(new[] { GameTutorialScript.LogLine(lines[0].Text), GameTutorialScript.LogLine(lines[1].Text) }));
            Assert.That(bar.gameObject.activeSelf, Is.False, "Two lines fit: no drag bar.");
            Assert.That(dim.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(text => text.name == "Words").alignment,
                Is.EqualTo(TextAnchor.MiddleLeft), "The words start right beside the face.");
            var title = dim.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(image => image.name == "Log Title");
            Assert.That(title.sprite, Is.SameAs(art.logTitle));
            Rect listRect = ScreenRect(dim.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Log List"));
            Assert.That(ScreenRect(title.rectTransform).yMin, Is.GreaterThan(listRect.yMax), "The scroll sits above the list.");
            Assert.That(board.AllowedGestures, Is.EqualTo(LabBoardGestures.None));

            // A touch anywhere lands on the log, so the line behind stays where it was.
            Rect screen = ScreenRect(dim);
            hits.Clear();
            dim.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>().Raycast(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = new Vector2(screen.center.x, screen.height * .1f) }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            // #586's six-line list can reach this point on a short screen (the batch Editor is landscape); then the list's
            // drag area takes it instead of the backdrop. Either way the touch stays inside the log and misses 뒤로 가기.
            Assert.That(hits[0].gameObject.transform.IsChildOf(dim), Is.True, "The log takes the touch: " + hits[0].gameObject.name);
            Assert.That(hits[0].gameObject.transform.IsChildOf(back.transform), Is.False, "Not the back button.");
            int beat = director.BeatIndex;

            // Nothing else starts under it, even from a finger already down when it opened.
            int logged = director.DialogueLog.Count;
            yield return new WaitForSecondsRealtime(art.tapCooldown + .05f);
            tap.onClick.Invoke();
            skip.onClick.Invoke();
            buttons.Single(button => button.name == "Pause").onClick.Invoke();
            yield return null;
            Assert.That((director.BeatIndex, director.DialogueLog.Count), Is.EqualTo((beat, logged)), "The line stays.");
            Assert.That(director.IsSkipDialogOpen, Is.False, "No skip dialog over the log.");
            Assert.That(session.Network.IsBattlePaused, Is.False, "No pause menu over the log.");

            // 뒤로 가기: closes and carries on from the same line.
            back.onClick.Invoke();
            yield return Until(() => !director.IsDialogueLogOpen, "뒤로 가기 closes the log", 2f);
            Assert.That(dim.gameObject.activeInHierarchy, Is.False);
            Assert.That(director.BeatIndex, Is.EqualTo(beat), "The same line again.");
            Assert.That(director.IsShowingLine, Is.True);

            // More lines than fit (all of 0단계 and the first of 1단계): it opens on the newest, and the drag bar shows.
            for (int i = 2; i <= lines.Length; i++)
            {
                yield return WaitForTapToCount(director, art);
                tap.onClick.Invoke();
            }
            Assert.That(director.DialogueLog.Count, Is.GreaterThan(art.logVisibleBars));
            log.onClick.Invoke();
            yield return new WaitForSecondsRealtime(art.dimFadeSeconds + .1f);
            Assert.That(director.DialogueLogShownLines.Count(), Is.EqualTo(director.DialogueLog.Count));
            Assert.That(bar.gameObject.activeSelf, Is.True, "Too many to fit: the drag bar shows.");
            Assert.That(director.DialogueLogScroll, Is.EqualTo(0f).Within(.01f), "Opens at the newest line, at the bottom.");
            bar.value = 1f;
            yield return null;
            Assert.That(director.DialogueLogScroll, Is.EqualTo(1f).Within(.01f), "The drag bar reaches the oldest line.");
            back.onClick.Invoke();
            yield return Until(() => !director.IsDialogueLogOpen, "Closed again", 2f);
        }

        [UnityTest]
        public IEnumerator SkippingAsksFirstThenLeavesForTheRoomList()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return null;
            Assert.That(session.TryStartTutorial(), Is.True);
            GameTutorialDirector director = null;
            yield return Until(() => (director = Object.FindFirstObjectByType<GameTutorialDirector>()) != null
                && director.IsShowingLine, "The practice board plays its first line", 30f);
            GameTutorialArt art = GameTutorialArt.Load();
            var buttons = director.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var tap = buttons.Single(button => button.name == "Tap To Continue");
            var skip = buttons.Single(button => button.name == "Skip Button");
            var leave = buttons.Single(button => button.name == "Skip Tutorial");
            Assert.That(skip.gameObject.activeInHierarchy, Is.False, "Hidden until the screen is tapped.");
            yield return new WaitForSecondsRealtime(art.tapCooldown + .05f);
            tap.onClick.Invoke();
            yield return Until(() => director.SkipButtonAlpha >= .999f, "건너뛰기 has faded in", art.skipFadeSeconds + 1f);
            skip.onClick.Invoke();
            Assert.That(director.IsSkipDialogOpen, Is.True, "It asks first.");
            buttons.Single(button => button.name == "Log Button").onClick.Invoke();
            Assert.That(director.IsDialogueLogOpen, Is.False, "기록 waits behind the skip dialog.");
            Assert.That(session.IsTutorial, Is.True, "Nothing is left yet.");
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("^C6_LAB_TUTORIAL_SKIP step=0 beat=1$"));
            leave.onClick.Invoke();
            leave.onClick.Invoke();
            Assert.That((director.Skipped, director.IsFinishing), Is.EqualTo((true, true)), "Skipped once, however often pressed.");
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && session.RoomBrowserVisible, "Skipping leaves for the room list", 30f);
            Assert.That(session.IsTutorial, Is.False);
            Assert.That(session.TryStartTutorial(), Is.False, "The tutorial does not come back in the same run.");
        }

        [UnityTest]
        public IEnumerator PauseMenuRestartStartsThePracticeBoardOverAndQuitGoesToTheTitle()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return null;
            Assert.That(session.TryStartTutorial(), Is.True);
            GameTutorialDirector first = null;
            yield return Until(() => (first = Object.FindFirstObjectByType<GameTutorialDirector>()) != null && first.IsShowingLine,
                "The practice board plays its first line", 30f);
            GameTutorialArt art = GameTutorialArt.Load();
            var buttons = first.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            yield return WaitForTapToCount(first, art);
            buttons.Single(button => button.name == "Tap To Continue").onClick.Invoke();
            Assert.That(first.BeatIndex, Is.EqualTo(1));
            Assert.That(first.DialogueLog.Count, Is.EqualTo(1));
            yield return null;
            buttons.Single(button => button.name == "Pause").onClick.Invoke();
            yield return Until(() => session.Network.IsBattlePaused && first.IsSteppedAsideForPause, "Paused", 5f);
            buttons.Single(button => button.name == "Log Button").onClick.Invoke();
            Assert.That(first.IsDialogueLogOpen, Is.False, "기록 waits behind the pause menu.");

            LabHud hud = Object.FindFirstObjectByType<GameBattleBindings>().hud;
            var restart = hud.pauseMenu.GetComponent<GamePauseView>().restartButton;
            var resume = (RectTransform)hud.resumeButton.transform;
            var quit = (RectTransform)hud.returnLobbyButton.transform;
            var restartRect = (RectTransform)restart.transform;
            Assert.That(restart.gameObject.activeInHierarchy && restart.interactable, Is.True);
            Assert.That(restart.GetComponentInChildren<UnityEngine.UI.Text>(true).text, Is.EqualTo("재시작"));
            float row = restartRect.anchoredPosition.y - resume.anchoredPosition.y;
            Assert.That(row, Is.LessThan(0f), "Below CONTINUE.");
            Assert.That(quit.anchoredPosition.y, Is.EqualTo(restartRect.anchoredPosition.y).Within(.01f));
            Assert.That(quit.anchoredPosition.x, Is.LessThan(restartRect.anchoredPosition.x));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("^C6_LAB_TUTORIAL_RESTART step=0 beat=1$"));
            restart.onClick.Invoke();
            restart.onClick.Invoke();
            GameTutorialDirector again = null;
            yield return Until(() => (again = Object.FindFirstObjectByType<GameTutorialDirector>()) != null && again != first
                && again.IsShowingLine, "The practice board opens again on its first line", 30f);
            Assert.That((again.StepIndex, again.BeatIndex), Is.EqualTo((0, 0)), "From the very start.");
            Assert.That(again.DialogueLog, Is.Empty, "The log starts over too.");
            Assert.That(session.IsTutorial, Is.True);
            Assert.That(session.Network.IsBattlePaused, Is.False, "Not paused any more.");
            Assert.That(session.Network.Snapshot.drums[0].owner, Is.EqualTo(session.Network.LocalPlayerId), "A fresh board.");

            // QUIT RUN from the pause menu goes back to the title screen.
            yield return null;
            again.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Pause").onClick.Invoke();
            yield return Until(() => session.Network.IsBattlePaused && again.IsSteppedAsideForPause, "Paused again", 5f);
            Object.FindFirstObjectByType<GameBattleBindings>().hud.returnLobbyButton.onClick.Invoke();
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Home, "Home after QUIT RUN", 30f);
            Assert.That(session.RoomBrowserVisible, Is.False, "The title screen, not the room list.");
            Assert.That(session.IsTutorial, Is.False);
        }

        /// <summary>
        /// Waits on the tutorial's own clock, which is what decides whether a tap counts. That clock only moves at the
        /// start of a frame, so after a slow frame a fixed wall-clock wait can end with the cooldown not over yet.
        /// </summary>
        private static IEnumerator WaitForTapToCount(GameTutorialDirector director, GameTutorialArt art) =>
            Until(() => director.TapCooldownOver, "The tap cooldown passes", art.tapCooldown + 5f);

        /// <summary>Wait out the tap cooldown, tap once, and check the next beat came.</summary>
        private static IEnumerator TapThrough(GameTutorialDirector director, UnityEngine.UI.Button tap, GameTutorialArt art)
        {
            Assert.That(director.IsShowingLine, Is.True, "A line is waiting for a tap.");
            int step = director.StepIndex, beat = director.BeatIndex;
            yield return WaitForTapToCount(director, art);
            tap.onClick.Invoke();
            Assert.That(director.IsFinishing || director.StepIndex != step || director.BeatIndex == beat + 1, Is.True,
                "Tap moved on from step " + step + " beat " + beat);
        }

        /// <summary>Flick my drum hard toward one side (+1 right, -1 left) so it slides off that edge.</summary>
        private static IEnumerator FlickDrum(LabOrbBoard board, float side)
        {
            LabOrbView drum = null;
            yield return Until(() => (drum = Object.FindObjectsByType<LabOrbView>(FindObjectsSortMode.None)
                .FirstOrDefault(view => view.Kind == LabOrbKind.Drum)) != null, "My drum is on the board");
            yield return new WaitForSecondsRealtime(.5f);
            Vector2 start = drum.transform.position;
            // Let go just past that side of the board (as a finger leaving the screen would), whatever the screen's shape:
            // a fixed world-space flick stops short on a wide batch-mode board.
            Rect area = board.CenterBounds;
            float endX = side > 0f ? Mathf.Max(start.x + area.width * .3f, area.xMax + area.width * .1f)
                : Mathf.Min(start.x - area.width * .3f, area.xMin - area.width * .1f);
            var flick = new Vector2(endX - start.x, 0f);
            double began = Time.realtimeSinceStartupAsDouble;
            Assert.That(board.TryBeginDragAtWorld(start, began), Is.True, "The drum can be picked up.");
            board.DragToWorld(start + flick * .5f, began + .04d);
            Assert.That(board.EndDragAtWorld(start + flick, began + .08d), Is.True);
        }

        /// <summary>
        /// Hold <paramref name="view"/> and walk the finger past one side of the board (+1 right, -1 left) over
        /// <paramref name="seconds"/> of given finger time, so even a heavy bomb that trails the finger reaches the pass
        /// strip, then let go there. It all runs in one frame like <see cref="FlickDrum"/>: across real frames the
        /// board's own Update sees no pressed mouse or touch and cancels the drag.
        /// </summary>
        private static IEnumerator DragOffSide(LabOrbBoard board, LabOrbView view, float side, float seconds = .7f)
        {
            Vector2 start = view.transform.position;
            Rect area = board.CenterBounds;
            var end = new Vector2(side > 0f ? area.xMax + area.width * .2f : area.xMin - area.width * .2f, start.y);
            double began = Time.realtimeSinceStartupAsDouble;
            Assert.That(board.TryBeginDragAtWorld(start, began), Is.True, view.Kind + " can be picked up.");
            const int steps = 42;
            for (int step = 1; step <= steps; step++)
            {
                float elapsed = seconds * step / steps;
                float reach = Mathf.Clamp01(elapsed / (seconds * .5f));
                Assert.That(board.DragToWorld(Vector2.Lerp(start, end, reach), began + elapsed), Is.True, "Still held.");
            }
            Assert.That(board.EndDragAtWorld(end, began + seconds + .01d), Is.True, view.Kind + " is let go off the side.");
            yield return null;
        }

        /// <summary>A UI rectangle on the screen (screen-space overlay canvases), whatever its anchors and scale.</summary>
        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(eye, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(eye, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
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
