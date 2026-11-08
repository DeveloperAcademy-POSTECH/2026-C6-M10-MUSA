using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    public sealed class GameBattleUiPresentationTests
    {
        private GameSession session;
        [UnityTest]
        public IEnumerator StoryThemesPreserveStageSceneryAndBindExistingHudAndBoard()
        {
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
                Assert.That(bindings.hud.staminaFill.sprite,Is.SameAs(ui.Theme.staminaCells));
                Assert.That(bindings.hud.staminaFill.transform.parent.parent,Is.SameAs(bindings.hud.battleUi.transform),"Orb composition keeps the stamina root directly under Battle UI.");
                Assert.That(bindings.sceneController.GetComponent<LabDrumMinimap>().compactGamePresentation,Is.True);
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
                        foreach(var point in map.SeatPositions.Values) Assert.That(point.x,Is.Zero);
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
                        Assert.That(Vector2.Distance(map.SeatPositions[mine], map.SeatPositions[players[first + 1].id]),
                            Is.EqualTo(total == 4 ? 42 : 26));
                        foreach(Transform marker in latestSeats)
                            Assert.That(marker.GetComponent<UnityEngine.UI.Image>().sprite,Is.SameAs(first==0?theme.teamBlueMarker:theme.teamRedMarker));
                        map.SyncDrums(new[] {new LabOrbState {id="ui-test-drum",kind=LabOrbKind.Drum,owner=mine}});
                        Assert.That(map.DrumOwner("ui-test-drum"),Is.EqualTo(mine));
                        Assert.That(map.DrumPosition("ui-test-drum"),Is.EqualTo(map.SeatPositions[mine]));
                        map.Advance(0);
                        Assert.That(latestSeats.GetChild(0).GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                        Assert.That(latestSeats.GetChild(1).GetComponent<UnityEngine.UI.Image>().enabled, Is.True);
                        map.SyncDrums(Array.Empty<LabOrbState>());
                        map.Advance(0);
                        Assert.That(latestSeats.GetChild(0).GetComponent<UnityEngine.UI.Image>().enabled, Is.True);
                    }
                }
            }
            finally { snapshot.players=original; }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
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
