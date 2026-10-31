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
            // Menu-only checks consume the real first-Start opening, then return to the title.
            yield return null;
            var opening = Object.FindFirstObjectByType<GameOpeningController>();
            if (opening != null && !GameOpeningController.HasPlayedThisRun)
            {
                opening.lobbyView.enterButton.onClick.Invoke();
                yield return Until(() => opening.IsPlaying, "First Start opens the story");
                opening.player.Skip();
                yield return Until(() => !GameOpeningController.IsPresenting, "Skip finishes behind the release shield");
                Assert.That(session.RoomBrowserVisible, Is.True);
                session.GoHome();
                yield return Until(() => !session.IsLoadingScene, "Return to title for menu raycasts");
            }
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
        public IEnumerator CreatingARoomAutomaticallyEntersTheGameLobbyAndHostCanChooseClearAndCreateAnotherRoom()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            yield return Click(view.countButtons[1]);
            yield return Click(view.createButton);
            Assert.That(view.modePanel.activeInHierarchy, Is.True, "새 방 만들기 first asks which mode to open (#281).");
            Assert.That(session.PendingRooms, Is.Empty);
            DispatchClick(view.basicModeButton);
            Assert.That(view.modePanel.activeSelf, Is.False);
            Assert.That(session.PendingRooms.Count, Is.EqualTo(1));
            Assert.That(session.PendingRooms.Single().Mode, Is.EqualTo(GameMode.Story));
            ushort reservedPort = session.PendingRooms.Single().Port;
            Assert.That(session.IsCreatingRoom, Is.True);
            Assert.That(session.Network.IsConnected, Is.False);
            Assert.That(view.CreationCapacity, Is.Zero);
            Assert.That(session.CreateAndEnterRoom(5), Is.False, "A second request must not create another advertised room.");
            session.Host(); session.StartSolo(); session.JoinDirect("127.0.0.1");
            Assert.That(session.Network.IsConnected || session.Network.IsJoiningRoom, Is.False,
                "Another connection path cannot race the pending automatic entry.");
            Assert.That(session.PendingRooms.Count, Is.EqualTo(1));
            // No room-card click: native registration and the session update must perform the handover.
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Lobby, "The authoritative game lobby Scene");
            yield return null;
            view = View();
            Assert.That(session.Network.GameFlowEnabled, Is.True);
            Assert.That(session.Network.IsHost, Is.True);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.Network.port, Is.EqualTo(reservedPort), "Automatic entry must retain the reserved advertised port.");
            Assert.That(session.Network.ConnectedRoomName, Does.StartWith(LabRoomNames.GameServiceBase));
            Assert.That(view.roomName.text, Is.EqualTo(LabRoomNames.Display(session.Network.ConnectedRoomName)));
            Assert.That(session.Network.LocalSlotNumber, Is.Zero);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(view.positionPage.activeInHierarchy, Is.True);
            Assert.That(view.slotButtons.Count(button => button.gameObject.activeSelf), Is.EqualTo(3));
            Assert.That(view.slotImages[0].sprite, Is.Not.SameAs(view.filledSlots[0]));
            Assert.That(session.Network.RequestPlayerSlot(4), Is.False, "P4 does not exist in this three-player room.");
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
            Vector2 anchor = view.seats3[1].anchoredPosition;
            view.seats3[1].anchoredPosition += Vector2.right * 5f;
            yield return null;
            Assert.That(((RectTransform)view.slotButtons[1].transform).anchoredPosition, Is.EqualTo(view.seats3[1].anchoredPosition),
                "The saved Canvas anchor, rather than a hardcoded slot point, controls the game view.");
            view.seats3[1].anchoredPosition = anchor;
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
            yield return Click(view.countButtons[3]);
            yield return Click(view.createButton);
            DispatchClick(view.basicModeButton);
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && session.Network.IsHost, "A second automatically entered room after leaving the first", 15f);
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(5));
            Assert.That(session.Network.LocalSlotNumber, Is.Zero);
            Assert.That(session.PendingRooms, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PendingAutomaticCreationCanBeCanceledOrTimedOutWithoutLeavingAnAdvertisedRoom()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            session.EnterRoomBrowser();
            Assert.That(session.CreateAndEnterRoom(2), Is.True, session.UiNotice);
            ushort canceledPort = session.PendingRooms.Single().Port;
            Assert.That(session.CancelRoomCreation(), Is.True);
            Assert.That(session.CancelRoomCreation(), Is.False);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.Network.IsConnected, Is.False);
            using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, canceledPort)));

            Assert.That(session.CreateAndEnterRoom(3), Is.True, session.UiNotice);
            ushort timedOutPort = session.PendingRooms.Single().Port;
            // Expire the real request before the next native registration tick without a 30-second test wait.
            typeof(GameSession).GetField("roomCreationDeadline", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(session, Time.realtimeSinceStartupAsDouble - 1d);
            yield return null;
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.Network.IsConnected, Is.False);
            Assert.That(session.UiNotice, Does.Contain("초과"));
            using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, timedOutPort)));
            Assert.That(session.CreateAndEnterRoom(2), Is.True, "A timed-out request must not prevent a new one.");
            session.GoHome();
            Assert.That(session.IsCreatingRoom, Is.False, "Returning Home must cancel an unfinished creation.");
            Assert.That(session.PendingRooms, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TerritoryCreateButtonAlsoEntersItsFourPlayerLobbyAutomatically()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            yield return Click(view.countButtons[2]);
            yield return Click(view.createButton);
            DispatchClick(view.teamModeButton);
            Assert.That(session.IsCreatingRoom, Is.True);
            Assert.That(session.PendingRooms.Single().Mode, Is.EqualTo(GameMode.Territory));
            Assert.That(session.PendingRooms.Single().Capacity, Is.EqualTo(LabTeams.Players));
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && session.Network.IsHost, "Four-player territory lobby without clicking a pending card", 15f);
            Assert.That(session.IsTerritory, Is.True);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(LabTeams.Players));
            Assert.That(session.Network.LocalSlotNumber, Is.Zero);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(View().teamLayout.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator TerritoryLobbyKeepsFourPlayerReadinessAndRestoresStoryTypography()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            GameLobbyView view = View();
            int storyHeadingSize = view.heading.fontSize;
            int storySealSize = view.sealButton.GetComponentInChildren<UnityEngine.UI.Text>().fontSize;
            yield return Click(view.enterButton);
            yield return Click(view.countButtons[2]);
            yield return Click(view.createButton);
            DispatchClick(view.teamModeButton);
            Assert.That(session.IsCreatingRoom, Is.True);
            Assert.That(session.PendingRooms.Single().Mode, Is.EqualTo(GameMode.Territory));
            Assert.That(session.PendingRooms.Single().Capacity, Is.EqualTo(LabTeams.Players));
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && session.Network.IsHost && SceneManager.GetActiveScene().path == GameScenePaths.Lobby,
                "Territory lobby entered automatically without a room-card click", 15f);
            yield return null;
            view = View();
            Assert.That(session.IsTerritory, Is.True);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(4), "Four chosen players open a 2:2 room (#394).");
            Assert.That(view.heading.fontSize, Is.EqualTo(view.teamHeadingFontSize));
            Assert.That(view.sealButton.interactable, Is.False);
            Assert.That(view.sealButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("땅따먹기 대기"));
            yield return Click(view.slotButtons[2]);
            Assert.That(session.Network.LocalSlotNumber, Is.EqualTo(3), "Host may freely choose the red team's P3.");
            Assert.That(view.localLabels[2].text, Does.Contain("방장"));
            view.sealButton.onClick.Invoke();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Lobby), "Visual start styling must not bypass readiness.");
            yield return Click(view.leaveButton);
            yield return Until(() => !session.IsLoadingScene && session.Network.CanChooseConnection
                && SceneManager.GetActiveScene().path == GameScenePaths.Home, "Browser after territory");
            yield return null;
            view = View();
            yield return Click(view.countButtons[1]);
            yield return Click(view.createButton);
            DispatchClick(view.basicModeButton);
            Assert.That(session.IsCreatingRoom, Is.True);
            Assert.That(session.PendingRooms.Single().Mode, Is.EqualTo(GameMode.Story));
            Assert.That(session.PendingRooms.Single().Capacity, Is.EqualTo(3));
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && session.Network.IsHost && SceneManager.GetActiveScene().path == GameScenePaths.Lobby,
                "Story lobby after territory entered automatically", 15f);
            yield return null;
            view = View();
            Assert.That(session.IsTerritory, Is.False);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(3));
            Assert.That(view.heading.fontSize, Is.EqualTo(storyHeadingSize), "Team typography must not leak into story rooms.");
            Assert.That(view.sealButton.GetComponentInChildren<UnityEngine.UI.Text>().fontSize, Is.EqualTo(storySealSize));
        }

        [UnityTest]
        public IEnumerator OneOnOneRoomShowsOneSeatInEachTeamBoxWithoutTeamLines()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            yield return EnterTerritoryRoom(0, 2);
            GameLobbyView view = View();
            Assert.That(view.teamLayout.activeInHierarchy, Is.True);
            Assert.That(view.teamPairParts.All(part => part.activeSelf), Is.True, "Both team boxes and names.");
            Assert.That(view.teamPairLines.Any(line => line.activeSelf), Is.False, "No teammate, so no line inside a box.");
            Assert.That(view.teamTrio.activeSelf, Is.False);
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(view.subheading.text, Is.EqualTo("원하는 자리를 선택 후, 준비하시오."));
            Assert.That(view.slotImages[1].sprite, Is.SameAs(view.redTeamEmptySlot));
            yield return Click(view.slotButtons[1]);
            Assert.That(session.Network.LocalSlotNumber, Is.EqualTo(2));
            Assert.That(view.slotImages[1].sprite, Is.SameAs(view.redTeamFilledSlot), "A taken Red seat is filled in Red.");
            Assert.That(((RectTransform)view.slotButtons[1].transform).anchoredPosition, Is.EqualTo(view.seatsOneOnOne[1].anchoredPosition));
            Assert.That(view.sealButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("땅따먹기 대기"));
        }

        [UnityTest]
        public IEnumerator ThreeOnThreeRoomShowsOneTeamPageAtATimeStartingWithBlue()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            yield return EnterTerritoryRoom(4, 6);
            GameLobbyView view = View();
            Assert.That(view.teamTrio.activeInHierarchy, Is.True);
            Assert.That(view.teamPairParts.Concat(view.teamPairLines).Any(part => part.activeSelf), Is.False);
            Assert.That(view.TeamPage, Is.Zero, "A 3:3 room opens on the Blue page.");
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(view.teamTrioName.text, Is.EqualTo("청팀"));
            Assert.That(view.teamTrioBox.sprite, Is.SameAs(view.blueTrioBox));
            Assert.That(view.teamPreviousPage.gameObject.activeSelf, Is.False);
            Assert.That(view.subheading.text, Is.EqualTo("3명씩 함께 앉아, 준비하시오."));
            Assert.That(view.joinedCount.text, Is.EqualTo("1 / 6명 입장"));
            yield return Click(view.teamNextPage);
            Assert.That(view.TeamPage, Is.EqualTo(1));
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 4, 5, 6 }));
            Assert.That(view.teamTrioName.text, Is.EqualTo("홍팀"));
            Assert.That(view.teamTrioBox.sprite, Is.SameAs(view.redTrioBox));
            Assert.That(view.teamNextPage.gameObject.activeSelf, Is.False);
            Assert.That(((RectTransform)view.slotButtons[5].transform).anchoredPosition, Is.EqualTo(view.seatsThreeOnThree[5].anchoredPosition));
            yield return Click(view.slotButtons[5]);
            Assert.That(session.Network.LocalSlotNumber, Is.EqualTo(6), "P6 can be chosen in a six-player room.");
            Assert.That(view.slotImages[5].sprite, Is.SameAs(view.redTeamFilledSlot));
            Assert.That(view.localLabels[5].text, Does.Contain("방장"));
            yield return Click(view.teamPreviousPage);
            Assert.That(view.TeamPage, Is.Zero, "The other team's page can still be looked at.");
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 1, 2, 3 }));
            // A sideways swipe on the team box turns the page; a short or vertical drag does not.
            var drag = new PointerEventData(EventSystem.current)
                { pressPosition = new Vector2(Screen.width * .7f, Screen.height * .5f), position = new Vector2(Screen.width * .66f, Screen.height * .4f) };
            view.teamSwipe.OnEndDrag(drag);
            yield return null;
            Assert.That(view.TeamPage, Is.Zero);
            drag.position = new Vector2(Screen.width * .3f, Screen.height * .5f);
            view.teamSwipe.OnEndDrag(drag);
            yield return null;
            Assert.That(view.TeamPage, Is.EqualTo(1));
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 4, 5, 6 }));
        }

        [UnityTest]
        public IEnumerator ChosenPlayerCountBlocksTheModeItCannotOpenAndShowsWhyUnderThatButton()
        {
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            var chips = view.countButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>();
            string[] basicReasons = { "2명은 땅따먹기 1:1만 할 수 있소.", "", "", "", "6명은 땅따먹기 3:3만 할 수 있소." };
            string[] teamReasons = { "", "땅따먹기는 2·4·6명만 할 수 있소.", "", "땅따먹기는 2·4·6명만 할 수 있소.", "" };
            for (int i = 0; i < view.countButtons.Length; i++)
            {
                // The 6 chip only peeks out at the edge until the row is pushed sideways.
                chips.horizontalNormalizedPosition = i < 4 ? 0f : 1f;
                yield return Click(view.countButtons[i]);
                Assert.That(view.CreationCapacity, Is.EqualTo(i + 2));
                yield return Click(view.createButton);
                Assert.That(view.modePanel.activeInHierarchy, Is.True);
                Assert.That(view.basicModeButton.interactable, Is.EqualTo(basicReasons[i].Length == 0), (i + 2) + " players, basic");
                Assert.That(view.teamModeButton.interactable, Is.EqualTo(teamReasons[i].Length == 0), (i + 2) + " players, territory");
                Assert.That(view.basicModeReason.gameObject.activeSelf, Is.EqualTo(basicReasons[i].Length > 0));
                Assert.That(view.teamModeReason.gameObject.activeSelf, Is.EqualTo(teamReasons[i].Length > 0));
                if (basicReasons[i].Length > 0) Assert.That(view.basicModeReason.text, Is.EqualTo(basicReasons[i]));
                if (teamReasons[i].Length > 0) Assert.That(view.teamModeReason.text, Is.EqualTo(teamReasons[i]));
                // A blocked button does not react, also to a pointer click sent straight to it.
                var press = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                if (!view.basicModeButton.interactable) ExecuteEvents.Execute(view.basicModeButton.gameObject, press, ExecuteEvents.pointerClickHandler);
                if (!view.teamModeButton.interactable) ExecuteEvents.Execute(view.teamModeButton.gameObject, press, ExecuteEvents.pointerClickHandler);
                Assert.That(session.IsCreatingRoom, Is.False);
                Assert.That(session.PendingRooms, Is.Empty);
                // The dimmed backdrop's centre lies under the card, so close it the way a tap beside the card does.
                view.modeDismissButton.onClick.Invoke();
                yield return null;
                Assert.That(view.modePanel.activeSelf, Is.False);
            }
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

        /// <summary>Choose a player chip (scrolling the row for the 6 chip), open territory and wait in its lobby as Host.</summary>
        private IEnumerator EnterTerritoryRoom(int chip, int players)
        {
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            view.countButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>().horizontalNormalizedPosition = chip < 4 ? 0f : 1f;
            yield return Click(view.countButtons[chip]);
            Assert.That(view.CreationCapacity, Is.EqualTo(players));
            yield return Click(view.createButton);
            DispatchClick(view.teamModeButton);
            Assert.That(session.PendingRooms.Single().Capacity, Is.EqualTo(players));
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && session.Network.IsHost && SceneManager.GetActiveScene().path == GameScenePaths.Lobby,
                players + "-player territory lobby entered automatically", 15f);
            yield return null;
            Assert.That(session.IsTerritory, Is.True);
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(players));
        }

        private static int[] VisibleSeats(GameLobbyView view) => Enumerable.Range(0, view.slotButtons.Length)
            .Where(i => view.slotButtons[i].gameObject.activeSelf).Select(i => i + 1).ToArray();

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

        private static void DispatchClick(UnityEngine.UI.Button button)
        {
            AssertTouchable(button);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator Click(UnityEngine.UI.Button button)
        {
            DispatchClick(button);
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
