using System;
using System.Collections;
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
    /// <summary>Saved Figma controls exercise the persistent game session; a pointer-click is dispatched only after raycast verification.</summary>
    public sealed class GameLobbyViewTests
    {
        private GameSession session;

        [UnitySetUp]
        public IEnumerator OpenHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home bootstrap");
            session = GameSession.Instance;
            session.Network.port = AvailablePort();
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (session != null)
            {
                session.Network.Stop(); Object.Destroy(session.gameObject); yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Game Figma lobby fixture cleanup"); SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; yield return null;
        }

        [UnityTest]
        public IEnumerator TitleBrowserDirectIpAndScopedDevUseTheSavedVisibleControlsWithoutLegacyDoubleBindings()
        {
            GameLobbyView view = View();
            Assert.That(view.titlePage.activeSelf, Is.True);
            Assert.That(view.browserPage.activeSelf, Is.False);
            Assert.That(view.screen.externalNavigation, Is.True);
            Assert.That(view.screen.hostButton.gameObject.activeInHierarchy, Is.False);
            view.screen.hostButton.onClick.Invoke();
            Assert.That(session.Network.IsConnected, Is.False, "Hidden legacy navigation must have no game listeners.");

            yield return Click(view.developerButton);
            Assert.That(view.screen.developerPanel.activeInHierarchy, Is.True);
            Assert.That(view.design.gameObject.activeSelf, Is.False);
            AssertTouchable(view.screen.developerScopeButtons[3]);
            view.screen.developerScopeButtons[3].onClick.Invoke();
            view.screen.developerInputs[178].text = "2";
            view.screen.applyDeveloperButton.onClick.Invoke();
            Assert.That(session.CaptureDeveloperSettings(3)[178], Is.EqualTo(2f));
            yield return Click(view.screen.closeDeveloperButton);
            Assert.That(view.titlePage.activeInHierarchy, Is.True);
            yield return Click(view.enterButton);
            Assert.That(session.RoomBrowserVisible, Is.True);
            Assert.That(view.browserPage.activeInHierarchy, Is.True);
            yield return Click(view.countButtons[0]);
            Assert.That(view.CreationCapacity, Is.EqualTo(2));
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(2));
            yield return Click(view.directConnectionButton);
            Assert.That(view.directConnectionPanel.activeInHierarchy, Is.True);
            view.directAddress.text = ""; view.directPort.text = "0";
            yield return Click(view.directJoinButton);
            Assert.That(session.Network.IsJoiningRoom || session.Network.IsConnected, Is.False);
            Assert.That(view.directFeedback.text, Does.Contain("포트"));
            yield return Click(view.closeDirectConnectionButton);
            Assert.That(view.directConnectionPanel.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator CreatedTwoPlayerCardEntersTheGameLobbyAndHostCanChooseClearAndReturnToTheBrowser()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            yield return Click(view.countButtons[0]);
            yield return Click(view.createButton);
            Assert.That(view.modePanel.activeInHierarchy, Is.True, "새 방 만들기 first asks which mode to open (#281).");
            Assert.That(session.PendingRooms, Is.Empty);
            yield return Click(view.basicModeButton);
            Assert.That(view.modePanel.activeSelf, Is.False);
            Assert.That(session.PendingRooms.Count, Is.EqualTo(1));
            Assert.That(session.PendingRooms.Single().Mode, Is.EqualTo(GameMode.Story));
            Assert.That(session.Network.IsConnected, Is.False);
            Assert.That(view.CreationCapacity, Is.Zero);
            yield return Until(() => session.PendingRooms.Single().Ready
                && view.roomContent.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .Any(button => button.name == "CreatedRoom" && button.interactable), "A confirmed pending game-room card", 15f);
            string name = session.PendingRooms.Single().Name;
            var row = view.roomContent.GetComponentsInChildren<UnityEngine.UI.Button>().Single(button => button.name == "CreatedRoom");
            Assert.That(row.transform.Find("Name").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo(LabRoomNames.Display(name)));
            yield return Click(row);
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Lobby, "The authoritative game lobby Scene");
            yield return null;
            view = View();
            Assert.That(session.Network.GameFlowEnabled, Is.True);
            Assert.That(session.Network.IsHost, Is.True);
            Assert.That(session.Network.LocalSlotNumber, Is.Zero);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(view.positionPage.activeInHierarchy, Is.True);
            Assert.That(view.slotButtons.Count(button => button.gameObject.activeSelf), Is.EqualTo(2));
            Assert.That(view.slotImages[0].sprite, Is.Not.SameAs(view.filledSlots[0]));
            Assert.That(session.Network.RequestPlayerSlot(3), Is.False, "P3 does not exist in this two-player room.");
            yield return null;
            Assert.That(view.message.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.message.text, Is.EqualTo(session.Network.Notice));
            Assert.That(view.message.text, Does.Contain("available P number"));
            Assert.That(view.message.text, Is.Not.EqualTo(session.Network.RoomSearchMessage),
                "The connected lobby must show a rejected seat request rather than the browser's default help.");
            yield return Click(view.slotButtons[1]);
            Assert.That(session.Network.LocalSlotNumber, Is.EqualTo(2));
            Assert.That(view.localLabels[1].text, Does.Contain("방장"));
            Assert.That(view.localLabels[0].text, Does.Not.Contain("방장"));
            Vector2 anchor = view.seats2[1].anchoredPosition;
            view.seats2[1].anchoredPosition += Vector2.right * 5f;
            yield return null;
            Assert.That(((RectTransform)view.slotButtons[1].transform).anchoredPosition, Is.EqualTo(view.seats2[1].anchoredPosition),
                "The saved Canvas anchor, rather than a hardcoded slot point, controls the game view.");
            view.seats2[1].anchoredPosition = anchor;
            yield return Click(view.clearSlotButton);
            Assert.That(session.Network.LocalSlotNumber, Is.Zero);
            yield return Click(view.slotButtons[0]);
            Assert.That(session.Network.LocalSlotNumber, Is.EqualTo(1));
            yield return Click(view.slotButtons[0]);
            Assert.That(session.Network.LocalSlotNumber, Is.Zero, "Tapping one's selected seat also clears it.");
            view.sealButton.onClick.Invoke();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Lobby));
            Assert.That(session.Network.Snapshot, Is.Null, "An incomplete room must not start a legacy single battle.");
            yield return Click(view.leaveButton);
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && session.Network.CanChooseConnection && SceneManager.GetActiveScene().path == GameScenePaths.Home, "Home browser after leaving pre-battle lobby");
            yield return null;
            view = View();
            Assert.That(view.browserPage.activeInHierarchy, Is.True);
            Assert.That(view.titlePage.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DirectGameHostKeepsItsGuidNameAndAdvertisesTheGameFamilyBeforeEnteringTheLobby()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            using (var browser = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                Assert.That(browser.StartBrowse(), Is.True, browser.LastError);
                session.Network.SetTargetParticipantCount(2);
                session.Host();
                yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                    && SceneManager.GetActiveScene().path == GameScenePaths.Lobby && session.Network.IsHost
                    && !session.Network.HostRoomNamePending && !string.IsNullOrEmpty(session.Network.HostRoomName),
                    "The direct Game Host API's confirmed native registration and lobby Scene", 15f);
                yield return null;
                string name = session.Network.HostRoomName;
                Assert.That(name, Does.Match("^Hapio-[0-9a-f]{8}$"),
                    "Direct Game Host keeps its existing GUID name; only pending room cards use sequential names.");
                Assert.That(name.StartsWith(LabRoomNames.GameServiceBase, StringComparison.Ordinal), Is.False);
                Assert.That(session.Network.RoomFamily, Is.EqualTo(LabRoomFamily.Game));
                Assert.That(session.Network.GameFlowEnabled, Is.True);
                Assert.That(session.PendingRooms, Is.Empty);
                Assert.That(session.Network.LocalSlotNumber, Is.Zero);
                Assert.That(View().positionPage.activeInHierarchy, Is.True);
                Assert.That(View().roomName.text, Is.EqualTo(LabRoomNames.Display(name)));
                double deadline = Time.realtimeSinceStartupAsDouble + 20d;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    browser.Tick();
                    if (browser.Targets.Any(target => target.ServiceName == name && target.Port == session.Network.port)) break;
                    yield return null;
                }
                LabDiscoveredRoom found = LabRoomCatalog.GroupTargets(browser.Targets).SingleOrDefault(room => room.Name == name);
                Assert.That(found, Is.Not.Null, "A real Game-family browser must resolve the direct Host registration.");
                Assert.That(found.Port, Is.EqualTo(session.Network.port));
                Assert.That(found.Participants, Is.EqualTo(1));
                Assert.That(found.TargetParticipants, Is.EqualTo(2));
                Assert.That(found.WaitingForHost, Is.False);
                Assert.That(found.Playing, Is.False);
                Assert.That(LabRoomCatalog.RoutesForRoom(browser.Targets, found.Key), Is.Not.Empty);
            }
        }

        [UnityTest]
        public IEnumerator DevelopmentSoloButtonStartsTheThreeStageFlowAndGameOverReturnsToTheTitle()
        {
            GameLobbyView view = View();
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.True);
            yield return Click(view.soloButton);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Actual first game stage from the visible Solo button");
            Assert.That(session.Network.IsDeveloperSolo, Is.True);
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameLobbyView>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<LabLobbyView>(FindObjectsSortMode.None), Is.Empty);
            session.ExitRun();
            yield return Until(() => session.State.phase == GameFlowPhase.GameOver && !session.IsLoadingScene, "Game Over");
            session.GoHome();
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Home, "Home title after a terminal run");
            yield return null;
            Assert.That(View().titlePage.activeInHierarchy, Is.True);
            Assert.That(session.RoomBrowserVisible, Is.False);
        }

        private static GameLobbyView View() => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<GameLobbyView>(true)).Single();

        private static void AssertTouchable(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, button.name);
            var data = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty, button.name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(button.gameObject), button.name);
        }

        private static IEnumerator Click(UnityEngine.UI.Button button)
        {
            AssertTouchable(button);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
            yield return null; yield return null;
        }

        private static IEnumerator Until(Func<bool> predicate, string message, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }
        private static ushort AvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            { socket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); return checked((ushort)((IPEndPoint)socket.LocalEndPoint).Port); }
        }
    }
}
