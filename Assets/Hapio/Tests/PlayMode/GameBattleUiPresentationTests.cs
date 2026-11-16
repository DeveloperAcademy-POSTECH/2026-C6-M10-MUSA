using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    public sealed class GameBattleUiPresentationTests
    {
        [UnityTest]
        public IEnumerator HelpBlocksTouchesAndUsesOriginalPressedArtworkWithoutPausingPeers()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home);
            yield return Until(()=>GameSession.Instance!=null&&!GameSession.Instance.IsLoadingScene);
            session=GameSession.Instance;
            var port=new TcpListener(IPAddress.Loopback,0);port.Start();
            session.Network.port=(ushort)((IPEndPoint)port.LocalEndpoint).Port;port.Stop();
            session.StartSolo();
            yield return Until(()=>session.State.phase==GameFlowPhase.Playing&&!session.IsLoadingScene&&!session.SceneFade.BlocksInput&&!session.IsLoadingScreenUp&&!session.IsPreparingBattle);
            var view=Object.FindFirstObjectByType<GameHelpView>();
            Assert.That(view,Is.Not.Null);
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(view.helpButton.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            Assert.That(view.helpButton.GetComponent<GameHudButtonArtwork>().artwork.SurfaceName,Is.EqualTo("HelpPressed"));
            ExecuteEvents.Execute(view.helpButton.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(view.helpButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(view.IsOpen,Is.True);
            Assert.That(session.Network.IsBattlePaused,Is.False);
            Canvas.ForceUpdateCanvases();
            var dim=(RectTransform)view.modal.transform.Find("Dim");
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,dim.TransformPoint(new Vector3(dim.rect.center.x,dim.rect.yMin+20,0)));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.Count,Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform.IsChildOf(view.modal.transform),Is.True,"The modal must intercept board touches: "+string.Join(",",hits.Select(hit=>hit.gameObject.name+":"+hit.sortingOrder)));
            Assert.That(C6Lab.LabOrbBoard.ExternalModalOpen,Is.True,"Direct-touch overlays (talisman, smoke) must ignore touches under help.");
            view.closeButton.onClick.Invoke();yield return null;
            Assert.That(view.IsOpen,Is.False);
            Assert.That(C6Lab.LabOrbBoard.ExternalModalOpen,Is.False);
            view.helpButton.onClick.Invoke();yield return null;
            Object.FindFirstObjectByType<GameBattleBindings>().hud.pauseButton.onClick.Invoke();yield return null;
            Assert.That(session.Network.IsBattlePaused,Is.True);
            var hud = Object.FindFirstObjectByType<GameBattleBindings>().hud;
            var pauseView = hud.pauseMenu.GetComponent<GamePauseView>();
            foreach (var action in new[] { hud.returnLobbyButton, pauseView.restartButton })
            {
                Assert.That(action.targetGraphic, Is.TypeOf<GameFigmaSurface>());
                Assert.That(((GameFigmaSurface)action.targetGraphic).SurfaceName, Is.EqualTo("PauseAction"));
            }
            Assert.That(view.IsOpen,Is.False,"Shared pause takes precedence over local help.");
            Assert.That(C6Lab.LabOrbBoard.ExternalModalOpen,Is.False);

            // The 100% finish keeps the phase at Playing; help must not cover it.
            session.Network.ResumeBattle();
            yield return Until(()=>!session.Network.IsBattlePaused);
            view.helpButton.onClick.Invoke();yield return null;
            Assert.That(view.IsOpen,Is.True);
            var model=(C6Lab.LabModel)typeof(C6Lab.LabNetwork).GetField("model",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(session.Network);
            var surface=model.PaintSurface;
            var pixels=Enumerable.Repeat(C6Lab.LabPaintSurface.Opaque(model.PaintColorFor(C6Lab.LabOrbElement.Water)),surface.CellCount).ToArray();
            var owners=Enumerable.Repeat((byte)session.Network.LocalSlotNumber,surface.CellCount).ToArray();
            Assert.That(surface.TryReplacePixels(surface.Columns,surface.Rows,surface.Revision+1,pixels,owners,1),Is.True);
            yield return Until(()=>session.IsFinishingBattle||session.State.phase!=GameFlowPhase.Playing);
            yield return null;
            Assert.That(view.IsOpen,Is.False,"Help closes once the 100% finish starts.");
            Assert.That(C6Lab.LabOrbBoard.ExternalModalOpen,Is.False);
            view.Open();
            Assert.That(view.IsOpen,Is.False,"Help cannot be opened during the finish.");
        }

        private GameSession session;
#if UNITY_EDITOR
        private bool soloStagePreferenceOverridden, hadSoloStagePreference;
        private int savedSoloStagePreference;
#endif
        [UnityTest]
        public IEnumerator SavedBattleThemeDrawsTheNewBombOnTheRealBoard()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene);
            session = GameSession.Instance;
            var port = new TcpListener(IPAddress.Loopback, 0); port.Start();
            session.Network.port = (ushort)((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
            session.StartSolo();
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene);
            yield return Until(() => !session.SceneFade.BlocksInput);
            var bindings = Object.FindFirstObjectByType<GameBattleBindings>();
            var look = bindings.GetComponent<GameBattleUiPresentation>().Theme.orbAppearance;
            Assert.That(session.Network.config.OrbAppearance, Is.SameAs(look));
            Assert.That(look.BombVisualPrefab, Is.Not.Null);
            // A presentation-only fixture on the real loaded Battle board; no model state is changed.
            var bomb = bindings.board.UpsertOrb("bomb-visual-binding-test", LabOrbKind.Bomb,
                new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 9f);
            Assert.That(bomb.ItemMarker.BombVisual, Is.Not.Null);
            Assert.That(bomb.ItemMarker.BombVisual.CountSprite.name, Is.EqualTo("BombCount2"));
            Assert.That(bomb.ItemMarker.BombCountText, Is.EqualTo("2"));
            Assert.That(bomb.ItemMarker.SparkVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator StoryThemesPreserveStageSceneryAndBindExistingHudAndBoard()
        {
#if UNITY_EDITOR
            // This scenario covers stages 1→2→3 even if the developer last previewed a later stage.
            hadSoloStagePreference = UnityEditor.EditorPrefs.HasKey(GameSession.DeveloperSoloStartStageKey);
            savedSoloStagePreference = UnityEditor.EditorPrefs.GetInt(GameSession.DeveloperSoloStartStageKey, 1);
            soloStagePreferenceOverridden = true;
            UnityEditor.EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, 1);
#endif
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene);
            session = GameSession.Instance;
            var port = new TcpListener(IPAddress.Loopback,0); port.Start();
            session.Network.port=(ushort)((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
            session.StartSolo();
            for (int stage=1;stage<=3;stage++)
            {
                if (stage>1) { Assert.That(session.Network.PauseBattle(),Is.True); Assert.That(session.DeveloperNextStage(),Is.True); }
                yield return Until(() => session.State.stageIndex==stage && session.State.phase==GameFlowPhase.Playing && !session.IsLoadingScene);
                yield return Until(() => !session.SceneFade.BlocksInput);
                yield return new WaitForSecondsRealtime(.25f);
                var bindings=Object.FindFirstObjectByType<GameBattleBindings>();
                var ui=bindings.GetComponent<GameBattleUiPresentation>();
                var scenery=Object.FindFirstObjectByType<GameStageBackgrounds>();
                Assert.That(ui,Is.Not.Null); Assert.That(ui.AppliedStage,Is.EqualTo(stage));
                Assert.That(scenery.AppliedStage,Is.EqualTo(stage));
                Assert.That(scenery.Resolve(stage).studio.gameObject.activeSelf,Is.True);
                if (stage == 2)
                {
                    var moon = scenery.Resolve(stage).studio.decorations.Single(item => item.png != null && item.png.name == "Moon");
                    Assert.That(moon.group.gameObject.activeInHierarchy, Is.True, "Story stage 2 still shows the moon.");
                }
                Assert.That(session.Network.config.OrbAppearance,Is.SameAs(ui.Theme.orbAppearance));
                Assert.That(bindings.board.transform.Find("OrbBoardPlate").GetComponent<Renderer>().sharedMaterial.mainTexture,Is.SameAs(ui.Theme.BoardFor(stage)));
                Color ink=GameBattleUiTheme.LightInkForStage(stage)?GameBattleUiColors.Light:GameBattleUiColors.Ink;
                Assert.That(bindings.stageLabel.color,Is.EqualTo(ink)); Assert.That(bindings.hud.hpText.color,Is.EqualTo(ink));
                Assert.That(ui.BoardArtwork.texture,Is.SameAs(ui.Theme.BoardFor(stage)));
                var composite=bindings.hud.GetComponent<LabOrbHudComposition>();
                Assert.That(composite.presentationBackdrop,Is.SameAs(ui.BoardArtwork.rectTransform));
                Assert.That(composite.IsCompositing,Is.True,composite.InitializationError);
                if (composite.IsCompositing)
                {
                    Assert.That(ui.BoardArtwork.enabled,Is.True);
                    Assert.That(bindings.sceneController.orbCamera.enabled,Is.False,"Input camera must not repaint a separate paper backing.");
                    var continuation=bindings.GetComponent<GameBoardBackgroundContinuation>().ContinuationCamera;
                    Assert.That(continuation,Is.Not.Null);
                    Ray upper=bindings.battleCamera.ViewportPointToRay(new Vector3(.37f,0,0));
                    Ray lower=continuation.ViewportPointToRay(new Vector3(.37f,1,0));
                    Assert.That(Vector3.Distance(upper.direction,lower.direction),Is.LessThan(.00001f),"Both sides of the background boundary must share the same view ray.");
                    Assert.That(bindings.board.transform.Find("OrbBoardPlate").GetComponent<Renderer>().enabled,Is.False);
                    Assert.That(ui.BoardArtwork.transform.GetSiblingIndex(),Is.LessThan(composite.Display.transform.GetSiblingIndex()),"Board artwork must remain below actual orb rendering.");
                }
                Assert.That(bindings.hud.staminaFill.sprite,Is.SameAs(Resources.Load<Sprite>("Hapio/UI/BattleCharacters/Cells")));
                Assert.That(bindings.hud.staminaFill.transform.parent.parent,Is.SameAs(bindings.hud.battleUi.transform),"Orb composition keeps the stamina root directly under Battle UI.");
                AssertPlayerBadgeLayout(bindings.hud);
                Assert.That(bindings.sceneController.GetComponent<LabDrumMinimap>().compactGamePresentation,Is.True);
                yield return Until(() => !session.IsLoadingScreenUp && !session.IsPreparingBattle);
                yield return null; // The invisible hit target refreshes availability after the start cover closes.
                Assert.That(bindings.hud.developerSettingsHidden, Is.True);
                Assert.That(bindings.hud.developerSettingsButton.gameObject.activeInHierarchy, Is.False,
                    "DEV must not appear as a pause-menu button in the real game presentation.");
                var hiddenDev = bindings.GetComponent<GameDeveloperTapTarget>();
                Assert.That(hiddenDev, Is.Not.Null);
                Assert.That(hiddenDev.TapArea, Is.Not.Null);
                var events = EventSystem.current;
                Assert.That(events, Is.Not.Null);
                var targetRect = (RectTransform)hiddenDev.TapArea.transform;
                Vector2 tapPosition = RectTransformUtility.WorldToScreenPoint(null, targetRect.TransformPoint(targetRect.rect.center));
                var tap = new PointerEventData(events) { position = tapPosition, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                events.RaycastAll(tap, hits);
                Assert.That(hits.Count, Is.GreaterThan(0));
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(hiddenDev.TapArea),
                    "The real top-centre hit area must receive the click before other HUD graphics.");
                ExecuteEvents.Execute(hiddenDev.TapArea, tap, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(hiddenDev.TapArea, tap, ExecuteEvents.pointerClickHandler);
                Assert.That(bindings.developerMode.panel.activeInHierarchy, Is.False, "One or two taps must not open settings.");
                Assert.That(session.Network.IsBattlePaused, Is.False, "An incomplete gesture must not pause the session.");
                ExecuteEvents.Execute(hiddenDev.TapArea, tap, ExecuteEvents.pointerClickHandler);
                yield return null;
                Assert.That(session.Network.IsBattlePaused, Is.True, "The third tap pauses the Host's real battle.");
                Assert.That(bindings.developerMode.panel.activeInHierarchy, Is.True, "The existing DEV form opens after pausing.");
                bindings.developerMode.Close();
                session.Resume(); yield return null;
                bindings.hud.pauseButton.onClick.Invoke(); yield return null;
                Assert.That(session.Network.Snapshot.paused,Is.True,"Existing pause routing remains live.");
                session.Resume(); yield return null;
            }
        }
        [UnityTest]
        public IEnumerator TerritoryUsesTeamMarkersForTwoAndThreePlayersWithoutChangingDrumOwnership()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene);
            session=GameSession.Instance;
            var port=new TcpListener(IPAddress.Loopback,0); port.Start();
            session.Network.port=(ushort)((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
            session.StartSolo(GameMode.Territory);
            yield return Until(() => session.State.phase==GameFlowPhase.Playing && !session.IsLoadingScene);
            yield return null;
            var bindings=Object.FindFirstObjectByType<GameBattleBindings>();
            var map=bindings.sceneController.GetComponent<LabDrumMinimap>();
            var ui = bindings.GetComponent<GameBattleUiPresentation>();
            var backgrounds = Object.FindFirstObjectByType<GameStageBackgrounds>();
            Assert.That(session.State.stageIndex, Is.EqualTo(1), "Territory stays a single match.");
            Assert.That(backgrounds.AppliedStage, Is.EqualTo(2));
            Assert.That(backgrounds.Resolve(2).studio.gameObject.activeSelf, Is.True);
            var moon = backgrounds.Resolve(2).studio.decorations.Single(item => item.png != null && item.png.name == "Moon");
            Assert.That(moon.group.gameObject.activeInHierarchy, Is.False, "The moon must not sit behind the territory team flag.");
            Assert.That(backgrounds.Resolve(1).studio.gameObject.activeSelf, Is.False);
            Assert.That(ui.AppliedStage, Is.EqualTo(2));
            var blueStamina = (LabShapeFillImage)bindings.hud.staminaFill;
            Assert.That((Color32)blueStamina.filledColor, Is.EqualTo(new Color32(110, 160, 190, 255)),
                "The blue team uses Mugi's blue character gauge.");
            AssertPlayerBadgeLayout(bindings.hud);
            var hud = bindings.hud;
            var cells = (RectTransform)hud.staminaFill.transform.parent;
            var panel = (RectTransform)hud.battleUi.transform.Find("Character Stamina Panel");
            var portrait = hud.localPlayerCircle.rectTransform;
            Vector2 initialCells = cells.anchoredPosition;
            Vector3 initialScale = cells.localScale;
            Vector2 initialPanel = panel.anchoredPosition;
            Vector2 initialPortrait = portrait.anchoredPosition;
            var shortage = bindings.sceneController.GetComponent<LabStaminaShortFeedback>();
            Assert.That(shortage, Is.Not.Null);
            shortage.Play();
            shortage.Advance(.05f);
            Assert.That(cells.anchoredPosition.y, Is.Not.EqualTo(initialCells.y));
            Assert.That(cells.anchoredPosition.x, Is.EqualTo(initialCells.x).Within(.001f));
            Assert.That(cells.localScale.x, Is.EqualTo(initialScale.x).Within(.001f));
            Assert.That(cells.localScale.y, Is.GreaterThan(initialScale.y));
            Assert.That(panel.anchoredPosition, Is.EqualTo(initialPanel));
            Assert.That(portrait.anchoredPosition, Is.EqualTo(initialPortrait));
            shortage.Advance(5f);
            Assert.That(cells.anchoredPosition, Is.EqualTo(initialCells));
            Assert.That(cells.localScale, Is.EqualTo(initialScale));
            Assert.That(ui.BoardArtwork.texture, Is.SameAs(ui.Theme.BoardFor(2)));
            Assert.That(bindings.stageLabel.color, Is.EqualTo(GameBattleUiColors.Light));
            var composition = bindings.hud.GetComponent<LabOrbHudComposition>();
            Assert.That(composition.IsCompositing, Is.True, composition.InitializationError);
            Assert.That(bindings.sceneController.orbCamera.enabled, Is.False);
            var continuation = bindings.GetComponent<GameBoardBackgroundContinuation>().ContinuationCamera;
            Assert.That(continuation, Is.Not.Null);
            Assert.That(continuation.enabled, Is.True);
            Ray upper = bindings.battleCamera.ViewportPointToRay(new Vector3(.37f, 0, 0));
            Ray lower = continuation.ViewportPointToRay(new Vector3(.37f, 1, 0));
            Assert.That(Vector3.Distance(upper.direction, lower.direction), Is.LessThan(.00001f));
            var teamBar = bindings.hud.TeamBar;
            Assert.That(teamBar, Is.Not.Null);
            Assert.That(teamBar.transform.Find("Figma Team Outline").GetComponent<GameUiFrame>().color,
                Is.EqualTo(GameBattleUiColors.Light));
            Assert.That(teamBar.BlueFlagAt, Is.Zero);
            Assert.That(teamBar.RedFlagAt, Is.EqualTo(1));
            AssertTerritoryPlayerBadgeSprites(bindings.hud, ui.Theme, session.Network);
            var snapshot=session.Network.Snapshot;
            var original=snapshot.players;
            try
            {
                foreach (int total in new[] {4,6})
                {
                    var players=new LabPlayerState[total];
                    for(int i=0;i<total;i++) players[i]=new LabPlayerState {id=(ulong)(100+i),seat=i};
                    snapshot.players=players;
                    foreach (int first in new[] {0,total/2})
                    {
                        ulong mine=players[first].id;
                        map.Rebuild(LabTeams.VisibleTeammates(snapshot,mine),mine);
                        Assert.That(map.SeatPositions.Count,Is.EqualTo(total/2));
                        if (total == 4)
                        {
                            foreach (var point in map.SeatPositions.Values) Assert.That(point.x, Is.Zero);
                        }
                        else
                        {
                            Assert.That(map.Panel.sizeDelta, Is.EqualTo(new Vector2(100, 72)));
                            Assert.That(map.SeatPositions[mine], Is.EqualTo(new Vector2(2, -21)));
                            Assert.That(map.SeatPositions[players[first + 1].id], Is.EqualTo(new Vector2(-27, 21)));
                            Assert.That(map.SeatPositions[players[first + 2].id], Is.EqualTo(new Vector2(27, 21)));
                        }
                        var theme=bindings.GetComponent<GameBattleUiPresentation>().Theme;
                        Transform latestSeats = null;
                        Transform latestLines = null;
                        foreach (Transform layer in map.Panel.Find("Content"))
                        {
                            if (layer.name == "Seats") latestSeats = layer;
                            if (layer.name == "Lines") latestLines = layer;
                        }
                        var connector = latestLines.Find("Team Connector").GetComponent<UnityEngine.UI.Image>();
                        Assert.That(connector.sprite, Is.SameAs(total == 4 ? theme.teamPairConnector : theme.teamTrioConnector));
                        if (total == 4)
                            Assert.That(Vector2.Distance(map.SeatPositions[mine], map.SeatPositions[players[first + 1].id]), Is.EqualTo(42));
                        else
                        {
                            Assert.That(connector.sprite.name, Is.EqualTo("0130f"));
                            Assert.That(connector.rectTransform.sizeDelta, Is.EqualTo(new Vector2(65.9468f, 51.282f)));
                            Assert.That(Mathf.DeltaAngle(connector.rectTransform.localEulerAngles.z, 180), Is.Zero.Within(.001f));
                        }
                        for (int i = 0; i < latestSeats.childCount; i++)
                        {
                            int character = first == 0 ? 2 : 0;
                            Sprite expected = map.battleCharacterPortraits[character];
                            Transform marker = latestSeats.GetChild(i);
                            Assert.That(marker.GetComponent<UnityEngine.UI.Image>().sprite, Is.SameAs(expected));
                            Assert.That(marker.GetComponent<UnityEngine.UI.Outline>(), Is.Null);
                            Assert.That(((RectTransform)marker).rect.width, Is.EqualTo(21f).Within(.001f));
                        }
                        var labels = map.Panel.Find("Content").Cast<Transform>().Last(layer => layer.name == "Labels");
                        if (total == 6)
                        {
                            string prefix = first == 0 ? "청" : "홍";
                            Assert.That(labels.Cast<Transform>().Select(item => item.GetComponent<UnityEngine.UI.Text>().text),
                                Is.EquivalentTo(new[] { prefix + "1", prefix + "2", prefix + "3" }));
                        }
                        map.SyncDrums(new[] {new LabOrbState {id="ui-test-drum",kind=LabOrbKind.Drum,owner=mine}});
                        Assert.That(map.DrumOwner("ui-test-drum"),Is.EqualTo(mine));
                        Assert.That(map.DrumPosition("ui-test-drum"),Is.EqualTo(map.SeatPositions[mine]));
                        map.Advance(0);
                        Assert.That(latestSeats.GetChild(0).GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                        var drumLayer = map.Panel.Find("Content").Cast<Transform>().Last(layer => layer.name == "Drums");
                        var drum = (RectTransform)drumLayer.Find("Drum");
                        Assert.That(drum.rect.width, Is.GreaterThan(((RectTransform)latestSeats.GetChild(0)).rect.width));
                        Assert.That(latestSeats.GetChild(1).GetComponent<UnityEngine.UI.Image>().enabled, Is.True);
                        if (total == 6)
                            Assert.That(labels.GetChild(0).GetComponent<UnityEngine.UI.Text>().enabled, Is.True);
                        map.SyncDrums(Array.Empty<LabOrbState>());
                        map.Advance(0);
                        Assert.That(latestSeats.GetChild(0).GetComponent<UnityEngine.UI.Image>().enabled, Is.True);
                        if (total == 6)
                        {
                            ulong next = players[first + 1].id;
                            map.Rebuild(LabTeams.VisibleTeammates(snapshot, next), next);
                            Assert.That(map.SeatPositions[next], Is.EqualTo(new Vector2(2, -21)));
                            Assert.That(map.SeatPositions[players[first + 2].id], Is.EqualTo(new Vector2(-27, 21)));
                            Assert.That(map.SeatPositions[mine], Is.EqualTo(new Vector2(27, 21)));
                        }
                    }
                }
            }
            finally { snapshot.players=original; }
        }
        private static void AssertTerritoryPlayerBadgeSprites(LabHud hud, GameBattleUiTheme theme, LabNetwork network)
        {
            Assert.That(theme.territoryBlueBadges, Has.Length.EqualTo(3));
            Assert.That(theme.territoryRedBadges, Has.Length.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(theme.territoryBlueBadges[i], Is.Not.Null);
                Assert.That(theme.territoryRedBadges[i], Is.Not.Null);
                Assert.That(theme.territoryBlueBadges[i].name, Is.EqualTo("TerritoryBlueP" + (i + 1)));
                Assert.That(theme.territoryRedBadges[i].name, Is.EqualTo("TerritoryRedP" + (i + 1)));
            }
            var snapshot = network.Snapshot;
            var originalPlayers = snapshot.players;
            var originalBlue = hud.territoryBlueBadges;
            var originalRed = hud.territoryRedBadges;
            var update = typeof(LabHud).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Action refresh = () => update.Invoke(hud, null);
            Func<int, int, LabPlayerState[]> roster = (total, mine) => Enumerable.Range(0, total)
                .Select(seat => new LabPlayerState { seat = seat, id = seat == mine ? network.LocalPlayerId : network.LocalPlayerId + (ulong)seat + 1 })
                .ToArray();
            try
            {
                // Refresh in the same frame: the live solo Host must not replace these display-only rosters.
                foreach (int total in new[] { 4, 6 })
                {
                    int size = total / 2;
                    // Authored 2:2 end walls and 3:3 P2-P1-P3 seating, independently of the runtime neighbor lookup.
                    int[] left = total == 4 ? new[] { -1, 0 } : new[] { 1, 2, 0 };
                    int[] right = total == 4 ? new[] { 1, -1 } : new[] { 2, 0, 1 };
                    for (int mine = 0; mine < total; mine++)
                    {
                        snapshot.players = roster(total, mine);
                        refresh();
                        int character = mine < size ? 2 : 0;
                        int number = mine % size;
                        string context = total + " players, global P" + (mine + 1);
                        string team = mine < size ? "청" : "홍";
                        string Caption(int position) => total == 6 ? team + (position + 1) : team + "팀";
                        Assert.That(LabHud.BattleTeamCaption(snapshot, mine + 1), Is.EqualTo(Caption(number)));
                        if (left[number] >= 0) Assert.That(hud.leftNeighborText.text, Is.EqualTo("< " + Caption(left[number])));
                        if (right[number] >= 0) Assert.That(hud.rightNeighborText.text, Is.EqualTo(Caption(right[number]) + " >"));
                        Sprite Neighbor(int position) => position < 0 ? null : hud.battleCharacterPortraits[character];
                        AssertPlayerBadgeSprite(hud.localPlayerCircle, hud.battleStaminaPortraits[character], context + " local");
                        AssertPlayerBadgeSprite(hud.leftPlayerCircle, Neighbor(left[number]), context + " left");
                        AssertPlayerBadgeSprite(hud.rightPlayerCircle, Neighbor(right[number]), context + " right");
                        Assert.That(hud.TeamBar.LocalRing != null && hud.TeamBar.LocalRing.gameObject.activeSelf,
                            Is.False, context + " character has no additional team rim");
                    }
                }
                snapshot.players = roster(1, 0);
                refresh();
                AssertPlayerBadgeSprite(hud.localPlayerCircle, hud.battleStaminaPortraits[2], "Solo blue Mugi");
                AssertPlayerBadgeSprite(hud.leftPlayerCircle, null, "Solo left");
                AssertPlayerBadgeSprite(hud.rightPlayerCircle, null, "Solo right");

                // Character art is independent of the legacy number assets.
                snapshot.players = roster(6, 4);
                hud.territoryBlueBadges = hud.territoryRedBadges = null;
                refresh();
                AssertPlayerBadgeSprite(hud.localPlayerCircle, hud.battleStaminaPortraits[0], "Red team Buri");
                AssertPlayerBadgeSprite(hud.leftPlayerCircle, hud.battleCharacterPortraits[0], "Red team P6 left");
                AssertPlayerBadgeSprite(hud.rightPlayerCircle, hud.battleCharacterPortraits[0], "Red team P4 right");
                Assert.That(hud.TeamBar.LocalRing == null || !hud.TeamBar.LocalRing.gameObject.activeSelf, Is.True);
                hud.territoryBlueBadges = originalBlue;
                hud.territoryRedBadges = originalRed;
                refresh();
                AssertPlayerBadgeSprite(hud.localPlayerCircle, hud.battleStaminaPortraits[0], "Red team Buri with team badge assets");
                Assert.That(hud.TeamBar.LocalRing == null || !hud.TeamBar.LocalRing.gameObject.activeSelf, Is.True);
            }
            finally
            {
                snapshot.players = originalPlayers;
                hud.territoryBlueBadges = originalBlue;
                hud.territoryRedBadges = originalRed;
                refresh();
            }
        }

        private static void AssertPlayerBadgeSprite(UnityEngine.UI.Image badge, Sprite expected, string context)
        {
            Assert.That(badge.sprite, Is.SameAs(expected), context);
            Assert.That(badge.enabled, Is.EqualTo(expected != null), context + " image visibility");
            Assert.That(badge.gameObject.activeInHierarchy, Is.EqualTo(expected != null), context + " edge visibility");
        }

        private static void AssertPlayerBadgeLayout(LabHud hud)
        {
            int[] storyCharacters = { 0, 2, 1, 3, 4 };
            for (int i = 0; i < storyCharacters.Length; i++)
                Assert.That(LabHud.BattleCharacterIndex(null, i + 1), Is.EqualTo(storyCharacters[i]));
            var stamina = (RectTransform)hud.staminaFill.transform.parent;
            var badge = hud.localPlayerCircle.rectTransform;
            Assert.That(badge.sizeDelta, Is.EqualTo(new Vector2(82, 82)));
            var panelRect = (RectTransform)hud.battleUi.transform.Find("Character Stamina Panel");
            Assert.That(panelRect, Is.Not.Null);
            Assert.That(badge.anchorMin, Is.EqualTo(panelRect.anchorMin));
            float portraitX = hud.localPlayerCircle.sprite.name == "StaminaPortrait2" ? -132.5f : -131.5f;
            Assert.That(Vector2.Distance(badge.anchoredPosition, new Vector2(portraitX, 4f)), Is.LessThan(.001f));
            for (int index = 0; index < 5; index++)
            {
                Assert.That(hud.battleStaminaPortraits[index].rect.size, Is.EqualTo(new Vector2(164, 164)));
                Assert.That(hud.battleCharacterPortraits[index].rect.size, Is.EqualTo(new Vector2(162, 162)));
            }
            Assert.That(stamina.anchorMin, Is.EqualTo(panelRect.anchorMin));
            Assert.That(stamina.anchoredPosition.y, Is.EqualTo(-27f - 1f / 3f).Within(.001f));
            var panel = hud.battleUi.transform.Find("Character Stamina Panel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.parent, Is.SameAs(stamina.parent), "The character panel must not shake with the cells.");
            var left = hud.leftPlayerCircle.rectTransform;
            var right = hud.rightPlayerCircle.rectTransform;
            Assert.That(left.anchorMin.x, Is.EqualTo(23f / 402f).Within(.0001f));
            Assert.That(right.anchorMin.x, Is.EqualTo(379f / 402f).Within(.0001f));
            Assert.That(left.anchorMin.y, Is.EqualTo(1f - 502f / 874f).Within(.0001f));
            Assert.That(right.anchorMin.y, Is.EqualTo(left.anchorMin.y));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
#if UNITY_EDITOR
            if (soloStagePreferenceOverridden)
            {
                if (hadSoloStagePreference) UnityEditor.EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, savedSoloStagePreference);
                else UnityEditor.EditorPrefs.DeleteKey(GameSession.DeveloperSoloStartStageKey);
                soloStagePreferenceOverridden = false;
            }
#endif
            Time.timeScale=1;
            if (session!=null) { session.Network.Stop(); Object.Destroy(session.gameObject); yield return null; }
            var previous=SceneManager.GetActiveScene(); var empty=SceneManager.CreateScene("Battle UI cleanup"); SceneManager.SetActiveScene(empty);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            yield return null;
        }
        private static IEnumerator Until(Func<bool> ready)
        {
            float deadline=Time.realtimeSinceStartup+120;
            while (!ready()) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Scene did not become ready."); yield return null; }
        }
    }
}
