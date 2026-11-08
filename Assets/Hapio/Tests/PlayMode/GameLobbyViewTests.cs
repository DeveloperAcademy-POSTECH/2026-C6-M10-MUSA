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
    /// <summary>Saved Figma controls exercise the persistent game session; a pointer-click is dispatched only after raycast verification.</summary>
    [Category("Slow")]
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
                // Menu checks start from the room list, so the tutorial (#477) after the opening is skipped here.
                GameSession.SkipTutorialThisRun();
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
        public IEnumerator TitleBrowserDirectIpAndHiddenScopedDevAvoidLegacyDoubleBindings()
        {
            GameLobbyView view = View();
            Assert.That(view.titlePage.activeSelf, Is.True);
            Assert.That(view.browserPage.activeSelf, Is.False);
            Assert.That(view.screen.externalNavigation, Is.True);
            Assert.That(view.screen.hostButton.gameObject.activeInHierarchy, Is.False);
            view.screen.hostButton.onClick.Invoke();
            Assert.That(session.Network.IsConnected, Is.False, "Hidden legacy navigation must have no game listeners.");

            Assert.That(view.developerButton.gameObject.activeInHierarchy, Is.False,
                "The developer entry must not add a visible control to the authored Home UI.");
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.False);
            yield return OpenHiddenDeveloper(view);
            Assert.That(view.screen.developerPanel.activeInHierarchy, Is.True);
            Assert.That(view.design.gameObject.activeSelf, Is.False);
            AssertTouchable(view.screen.developerScopeButtons[3]);
            view.screen.developerScopeButtons[3].onClick.Invoke();
            view.screen.developerInputs[178].text = "2";
            view.screen.applyDeveloperButton.onClick.Invoke();
            Assert.That(session.CaptureDeveloperSettings(3)[178], Is.EqualTo(2f));
            yield return Click(view.screen.closeDeveloperButton);
            Assert.That(view.titlePage.activeInHierarchy, Is.True);
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False, "Closing DEV hides its solo entry.");
            Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.False);
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
        public IEnumerator HostLobbyHiddenDeveloperEntrySurvivesSceneTransition()
        {
            GameObject homeTapArea = View().screen.GetComponent<GameDeveloperTapTarget>().TapArea;
            session.Host();
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Lobby && session.Network.IsHost,
                "The Host enters the separate game lobby Scene", 15f);
            GameLobbyView view = View();
            var target = view.screen.GetComponent<GameDeveloperTapTarget>();
            Assert.That(target, Is.Not.Null, "The newly loaded lobby installs its own hidden DEV entry.");
            Assert.That(target.TapArea, Is.Not.Null);
            Assert.That(target.TapArea.scene, Is.EqualTo(view.screen.gameObject.scene),
                "The hit-area Canvas must share the new owner's Scene lifetime.");
            Assert.That(homeTapArea == null, Is.True, "The departing Home hit area must not survive over the lobby.");
            var image = target.TapArea.GetComponent<UnityEngine.UI.Image>();
            yield return Until(() => !session.IsLoadingScreenUp && image.raycastTarget && image.gameObject.activeInHierarchy,
                "The lobby loading overlay closes and Host permission enables the new target", 5f);
            Assert.That(view.screen.developerPanel.activeInHierarchy, Is.False);
            Assert.That(view.developerButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.False);
            for (int tap = 1; tap <= GameDeveloperTapTarget.RequiredTaps; tap++)
            {
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)target.TapArea.transform;
                var data = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(data, hits);
                Assert.That(hits, Is.Not.Empty, "Lobby hidden DEV tap " + tap);
                GameObject clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(clicked, Is.SameAs(target.TapArea), "The new lobby's actual topmost hit receives tap " + tap);
                ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
                if (tap < GameDeveloperTapTarget.RequiredTaps)
                    Assert.That(view.screen.developerPanel.activeInHierarchy, Is.False,
                        "One or two taps must not open the lobby's DEV panel.");
                Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False);
                Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.False);
            }
            yield return null; yield return null;
            Assert.That(view.screen.developerPanel.activeInHierarchy, Is.True);
            Assert.That(view.design.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.developerButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False, "A connected lobby never exposes solo launch.");
            Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.False);
            view.screen.closeDeveloperButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(view.screen.developerPanel.activeInHierarchy, Is.False);
            Assert.That(view.positionPage.activeInHierarchy, Is.True);
            Assert.That(view.developerButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(image.raycastTarget, Is.True, "Closing settings restores the same lobby's hidden entry.");
        }

        [Category("Bonjour")]
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
            yield return Click(view.slotButtons[1]);
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

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator EnteringAndLeavingACreatedRoomDarkenOverTheHeldScreenThenBrightenOnTheNextOne()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            System.Reflection.FieldInfo fadeOut = typeof(GameSession).GetField("lobbyFadeOutSeconds",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(fadeOut.GetValue(session), Is.EqualTo(0f),
                "By default the room's seat screen loads as soon as the room opens, under an immediate cover.");
            // A long darkening keeps the held Home on screen for several frames, so the checks below never race the load.
            fadeOut.SetValue(session, 1f);
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            yield return Click(view.countButtons[1]);
            yield return Click(view.createButton);
            UnityEngine.UI.Button refresh = view.refreshButton;
            DispatchClick(view.basicModeButton);
            Assert.That(session.SceneFade.Visible, Is.False, "Waiting for the room to open does not darken yet.");
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby, "The created room is entered", 15f);
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.Home), "The seat screen loads only after darkening.");
            Assert.That(session.HoldsSceneForFade(GameScenePaths.Home), Is.True);
            Assert.That(view.background.gameObject.activeSelf && view.design.gameObject.activeSelf, Is.True,
                "The browser stays as it was instead of vanishing to an empty frame under the darkening (#418).");
            Assert.That(view.browserPage.activeSelf, Is.True);
            Assert.That(session.SceneFade.Alpha, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(session.SceneFade.BlocksInput, Is.True);
            Canvas.ForceUpdateCanvases();
            var data = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, refresh.transform.position) };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty);
            // #478: the shared loading screen (30150) is drawn above the fade (30100) on a room change.
            Assert.That(hits[0].gameObject.transform.IsChildOf(session.LoadingView.transform), Is.True,
                "A second tap while darkening lands on the loading screen above the cover, not on the browser.");

            yield return Until(() => !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.Lobby,
                "The seat screen after darkening");
            Assert.That(session.HoldsSceneForFade(GameScenePaths.Home), Is.False);
            Assert.That(session.SceneFade.BlocksInput, Is.False,
                "The fade itself does not block while it brightens; the #478 loading screen above it does until it closes.");
            yield return Until(() => !session.SceneFade.Visible, "The cover clears", 3f);
            view = View();
            Assert.That(view.positionPage.activeInHierarchy, Is.True);

            // Back from the seat screen (#418): the same cover over the held seat screen, then the browser brightens.
            yield return Click(view.leaveButton);
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Home));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(GameScenePaths.Lobby), "Home loads only after darkening.");
            Assert.That(session.HoldsSceneForFade(GameScenePaths.Lobby), Is.True);
            Assert.That(view.background.gameObject.activeSelf && view.design.gameObject.activeSelf, Is.True,
                "The seat screen stays as it was instead of vanishing to an empty frame under the darkening.");
            Assert.That(view.positionPage.activeSelf, Is.True);
            Assert.That(session.SceneFade.Alpha, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(session.SceneFade.BlocksInput, Is.True);
            yield return Until(() => !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.Home,
                "The browser after darkening");
            Assert.That(session.HoldsSceneForFade(GameScenePaths.Lobby), Is.False);
            Assert.That(session.SceneFade.BlocksInput, Is.False);
            yield return Until(() => !session.SceneFade.Visible, "The cover clears on the browser", 3f);
            view = View();
            Assert.That(view.browserPage.activeInHierarchy, Is.True);
        }

        [Category("Bonjour")]
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

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator APublishThatFailsIsRetriedAndTheRoomStillOpensByItselfOnItsReservedPort()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            session.EnterRoomBrowser();
            Assert.That(session.CreateAndEnterRoom(2), Is.True, session.UiNotice);
            ushort reservedPort = session.PendingRooms.Single().Port;
            LabPendingRoom room = CreatingRoom();
            yield return Until(() => GamePendingRoomRetryTests.Advertisement(room).IsAdvertising, "The session starts the registration");
            Assert.That(session.PendingRooms.Single().Ready, Is.False);
            GamePendingRoomRetryTests.FailRegistration(room, "forced registration failure");
            yield return null;
            Assert.That(session.IsCreatingRoom, Is.True, "A publish that will be retried keeps the request (#503).");
            Assert.That(session.PendingRooms.Single().RetryPending, Is.True);
            Assert.That(session.UiNotice, Is.EqualTo("방을 공개하지 못해 다시 시도하고 있소..."));
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && session.Network.IsHost, "The retried room still opens without a card click", 20f);
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.Network.port, Is.EqualTo(reservedPort), "The retry keeps the reserved advertised port.");
            Assert.That(session.Network.ConnectedRoomName, Does.StartWith(LabRoomNames.GameServiceBase));
        }

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator APublishThatKeepsFailingEndsAtTheDeadlineOrItsLastRetryWithThePublishNotice()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            session.EnterRoomBrowser();
            Assert.That(session.CreateAndEnterRoom(3), Is.True, session.UiNotice);
            LabPendingRoom room = CreatingRoom();
            ushort port = room.Port;
            yield return Until(() => GamePendingRoomRetryTests.Advertisement(room).IsAdvertising, "The session starts the registration");
            GamePendingRoomRetryTests.FailRegistration(room, "forced registration failure");
            yield return null;
            Assert.That(session.PendingRooms.Single().RetryPending, Is.True);
            // The request keeps its 30-second deadline (#503); expire it while the retry waits.
            typeof(GameSession).GetField("roomCreationDeadline", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(session, Time.realtimeSinceStartupAsDouble - 1d);
            yield return null;
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.UiNotice, Is.EqualTo("방을 공개하지 못했소. 네트워크 권한을 확인하고 다시 눌러주시오."),
                "A request that never got published does not blame a slow room.");
            using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, port)));

            // With no retry left, the request ends at once with the registry's notice.
            Assert.That(session.CreateAndEnterRoom(2), Is.True, session.UiNotice);
            room = CreatingRoom();
            yield return Until(() => GamePendingRoomRetryTests.Advertisement(room).IsAdvertising, "The session starts the registration");
            GamePendingRoomRetryTests.Field(room, "retryAttempts").SetValue(room, LabDiscoveryBackoff.MaximumAttempts);
            GamePendingRoomRetryTests.FailRegistration(room, "forced last failure");
            LogAssert.Expect(LogType.Warning, "C6_LAB_PENDING_ROOM_RETRY result=stopped reason=forced last failure");
            yield return null;
            Assert.That(session.IsCreatingRoom, Is.False);
            Assert.That(session.PendingRooms, Is.Empty);
            Assert.That(session.UiNotice, Is.EqualTo("방을 공개하지 못했소. 같은 Wi-Fi와 네트워크 권한을 확인해주시오."));
        }

        private LabPendingRoom CreatingRoom() => ((GamePendingRoomHandle)typeof(GameSession).GetField("roomCreationHandle",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(session)).Room;

        [Category("Bonjour")]
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

        [Category("Bonjour")]
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
            Assert.That(view.sealButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("땅따먹기 대기 중"));
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

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator OneOnOneRoomShowsOneSeatInEachTeamBoxWithoutTeamLines()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            yield return EnterTerritoryRoom(0, 2);
            GameLobbyView view = View();
            Assert.That(view.teamLayout.activeInHierarchy, Is.True);
            Assert.That(view.seatDesign.teamColumns[0].activeInHierarchy, Is.True, "Both team boxes and names.");
            Assert.That(view.teamPairLines.Any(line => line.activeSelf), Is.False, "No teammate, so no line inside a box.");
            Assert.That(view.teamTrio.activeSelf, Is.False);
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(view.subheading.text, Is.EqualTo("원하는 자리를 선택 후, 준비하시오."));
            Assert.That(view.slotImages[1].sprite, Is.SameAs(view.redTeamEmptySlot));
            yield return Click(view.slotButtons[1]);
            Assert.That(session.Network.LocalSlotNumber, Is.EqualTo(2));
            Assert.That(view.slotImages[1].sprite, Is.SameAs(view.redTeamFilledSlot), "A taken Red seat is filled in Red.");
            Assert.That(((RectTransform)view.slotButtons[1].transform).anchoredPosition, Is.EqualTo(view.seatsOneOnOne[1].anchoredPosition));
            Assert.That(view.sealButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("땅따먹기 대기 중"));
        }

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator ThreeOnThreeRoomShowsBothTeamsAndAllowsEverySeatWithoutPaging()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            yield return EnterTerritoryRoom(4, 6);
            GameLobbyView view = View();
            Assert.That(view.seatDesign.teamColumns[2].activeInHierarchy, Is.True);
            Assert.That(view.teamTrio.activeInHierarchy, Is.False);
            Assert.That(view.teamPairParts.Concat(view.teamPairLines).Any(part => part.activeSelf), Is.False);
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(view.subheading.text, Is.EqualTo("3명씩 함께 앉아, 준비하시오."));
            Assert.That(view.joinedCount.text, Is.EqualTo("1 / 6명 입장"));
            // Actual raycasts must reach all six saved controls, including the bottom row in each team.
            for (int i = 0; i < 6; i++)
            {
                yield return Click(view.slotButtons[i]);
                Assert.That(session.Network.LocalSlotNumber, Is.EqualTo(i + 1));
                Assert.That(view.slotImages[i].sprite, Is.SameAs(i < 3 ? view.blueTeamFilledSlot : view.redTeamFilledSlot));
                Assert.That(view.localLabels[i].text, Does.Contain("방장"));
                Assert.That(VisibleSeats(view), Has.Length.EqualTo(6));
                yield return Click(view.slotButtons[i]);
                Assert.That(session.Network.LocalSlotNumber, Is.Zero, "The own-seat click still clears the choice.");
            }
            view.sealButton.onClick.Invoke();
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.Lobby), "UI cannot bypass six-player readiness.");
            // Six players still identifies territory before the Host's game-flow state arrives.
            session.State.mode = GameMode.Story;
            yield return null; yield return null;
            Assert.That(view.seatDesign.teamColumns[2].activeInHierarchy, Is.True);
            Assert.That(view.connectionLines.gameObject.activeSelf, Is.False);
            Assert.That(VisibleSeats(view), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            session.State.mode = GameMode.Territory;
        }

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator DevSoloAfterLeavingASixPlayerRoomStartsAloneInsteadOfReopeningIt()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            yield return EnterTerritoryRoom(4, 6);
            yield return Click(View().leaveButton);
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && session.Network.CanChooseConnection && SceneManager.GetActiveScene().path == GameScenePaths.Home,
                "Home browser after leaving the 3:3 room");
            yield return null;
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(6), "The last room's size is still remembered here.");
            // #405: a plain room (no registered card) refuses that six-player size before the Host starts.
            session.Network.Host();
            Assert.That(session.Network.IsConnected, Is.False, "Nothing may be left half-open.");
            Assert.That(session.Network.CanChooseConnection, Is.True);
            GameLobbyView view = View();
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False);
            yield return OpenHiddenDeveloper(view);
            yield return Click(view.soloButton);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "DEV solo battle after a 3:3 room", 15f);
            Assert.That(session.Network.IsDeveloperSolo, Is.True);
            Assert.That(session.IsTerritory, Is.False);
            Assert.That(session.Network.TargetParticipantCount, Is.EqualTo(LabModel.DefaultParticipants));
            Assert.That(session.Network.Snapshot.players, Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ChosenPlayerCountBlocksTheModeItCannotOpenAndShowsWhyInTheCardFooter()
        {
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            string[] basicReasons = { "2명은 땅따먹기 1:1만 할 수 있소.", "", "", "", "6명은 땅따먹기 3:3만 할 수 있소." };
            string[] teamReasons = { "", "땅따먹기는 짝수 인원만 가능하오..", "", "땅따먹기는 짝수 인원만 가능하오..", "" };
            for (int i = 0; i < view.countButtons.Length; i++)
            {
                yield return Click(view.countButtons[i]);
                Assert.That(view.CreationCapacity, Is.EqualTo(i + 2));
                yield return Click(view.createButton);
                Assert.That(view.modePanel.activeInHierarchy, Is.True);
                Assert.That(view.modeCard.rect.height, Is.EqualTo(i == 2 ? 266f : 292f));
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

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator ALobbyThatLosesItsConnectionLeavesLikeTheLeaveButtonAndShowsWhy()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            session.Network.SetTargetParticipantCount(2);
            session.Host();
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Lobby && session.Network.IsHost,
                "Host lobby", 15f);
            // #471: the transport ends underneath the lobby, not through 나가기. The test assembly has no Netcode reference.
            object manager = session.Network.GetType().GetField("manager").GetValue(session.Network);
            manager.GetType().GetMethod("Shutdown").Invoke(manager, new object[] { false });
            yield return Until(() => session.State.phase == GameFlowPhase.Home && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Home && session.Network.CanChooseConnection,
                "Room browser after the lobby lost its connection", 15f);
            yield return null;
            Assert.That(session.RoomBrowserVisible, Is.True, "Like 나가기 from the lobby, it returns to the room list.");
            Assert.That(session.Network.IsConnected, Is.False);
            Assert.That(session.Network.ParticipantCount, Is.Zero, "Stop cleared the old room.");
            Assert.That(session.UiNotice, Is.EqualTo("방과의 연결이 끊어졌소."));
            Assert.That(View().message.text, Is.EqualTo("방과의 연결이 끊어졌소."));
        }

        [UnityTest]
        public IEnumerator AFullRoomCardOpensOnlyForThisDevicesAdmittedTicket()
        {
            GameLobbyView view = View();
            yield return Click(view.enterButton);
            yield return Until(() => view.browserPage.activeInHierarchy, "Room list");
            LabNetwork network = session.Network;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            // #501: a full room may be holding this device's own place. The network is paused so the browse cannot
            // replace the room list set here, as LabFigmaLobbyTests does.
            network.enabled = false;
            try
            {
                IReadOnlyList<LabDiscoveredRoom> full = LabRoomCatalog.GroupTargets(new[]
                    { new LabBonjourTarget("127.0.0.1", 19123, 1, "Hapio-501a0b1c", 3, 3) });
                typeof(LabNetwork).GetField("discoveredRooms", flags).SetValue(network, full);
                yield return null; yield return null;
                Assert.That(RoomAction(view), Is.EqualTo("정원 마감"));
                typeof(LabNetwork).GetMethod("PrepareRejoinTicket", flags).Invoke(network,
                    new object[] { full[0].Key, full[0].Port, false });
                yield return null; yield return null;
                Assert.That(RoomAction(view), Is.EqualTo("정원 마감"), "A ticket the Host never let in holds no place there.");
                typeof(LabNetwork).GetField("rejoinTicketAdmitted", flags).SetValue(network, true);
                yield return null; yield return null;
                Assert.That(RoomAction(view), Is.EqualTo("입장 →"), "The Host may be holding this device's place.");
            }
            finally
            {
                typeof(LabNetwork).GetField("rejoinTicket", flags).SetValue(network, null);
                typeof(LabNetwork).GetField("rejoinTicketAdmitted", flags).SetValue(network, false);
                typeof(LabNetwork).GetField("discoveredRooms", flags).SetValue(network, Array.Empty<LabDiscoveredRoom>());
                network.enabled = true;
            }
        }

        private static string RoomAction(GameLobbyView view)
        {
            UnityEngine.UI.Button[] rows = view.roomContent.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Where(row => row != view.roomTemplate && row.gameObject.activeInHierarchy && row.transform.Find("Action") != null)
                .ToArray();
            Assert.That(rows.Length, Is.EqualTo(1), "The one listed room.");
            return rows[0].transform.Find("Action").GetComponent<UnityEngine.UI.Text>().text;
        }

        [Category("Bonjour")]
        [UnityTest]
        public IEnumerator AHeldSeatShowsAwayAndARejoiningLobbySaysItIsGettingBackIn()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native game-room advertisement requires macOS or iOS.");
            session.Network.SetTargetParticipantCount(3);
            session.Host();
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Lobby && session.Network.IsHost,
                "Host lobby", 15f);
            yield return null;
            GameLobbyView view = View();
            LabNetwork network = session.Network;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            // #501: the Host holds P2 for a dropped guest. It is set here by hand to read the screen; the real
            // drop and return are covered by C6Lab's LabLobbyRejoinNetworkTests.
            var slots = (LabPlayerSlots)typeof(LabNetwork).GetField("playerSlots", flags).GetValue(network);
            Assert.That(slots.TrySelect(77, 2, out string reason), Is.True, reason);
            Type holdType = typeof(LabNetwork).GetNestedType("LobbySeatHold", System.Reflection.BindingFlags.NonPublic);
            object hold = Activator.CreateInstance(holdType);
            holdType.GetField("ticket").SetValue(hold, new string('a', 32));
            holdType.GetField("playerId").SetValue(hold, 77UL);
            holdType.GetField("slot").SetValue(hold, 2);
            holdType.GetField("until").SetValue(hold, Time.realtimeSinceStartupAsDouble + 60d);
            var holds = (IList)typeof(LabNetwork).GetField("lobbySeatHolds", flags).GetValue(network);
            holds.Add(hold);
            yield return null; yield return null;
            Assert.That(network.HeldSeatCount, Is.EqualTo(1));
            Assert.That(view.localLabels[1].text, Is.EqualTo(GameLobbyView.HeldSeatLabel));
            Assert.That(view.subheading.text, Is.EqualTo(GameLobbyView.HeldSeatHint));
            Assert.That(view.joinedCount.text, Is.EqualTo("1 / 3명 입장"), "A held seat is not counted as joined.");
            Assert.That(view.slotImages[1].color.a, Is.LessThan(.5f), "The held seat is faded.");
            Assert.That(view.slotButtons[1].interactable, Is.False, "Nobody else can take the held seat.");
            holds.Clear();
            slots.Release(77);
            // A guest that is getting back in shows the pending lobby and its own message. The flag is set by hand on
            // this Host only to read the screen.
            System.Reflection.FieldInfo rejoining = typeof(LabNetwork).GetField("lobbyRejoinActive", flags);
            rejoining.SetValue(network, true);
            try
            {
                yield return null; yield return null;
                Assert.That(view.heading.text, Is.EqualTo("방 정보를 받고 있소..."));
                Assert.That(view.message.text, Is.EqualTo("방에 다시 들어가고 있소..."));
                Assert.That(view.slotButtons.Count(button => button.gameObject.activeSelf), Is.Zero);
            }
            finally { rejoining.SetValue(network, false); }
        }

        [Category("Bonjour")]
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
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False,
                "Solo is available inside DEV rather than on the authored Home page.");
            yield return OpenHiddenDeveloper(view);
            yield return Click(view.soloButton);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Actual first game stage from the Solo button inside hidden DEV");
            Assert.That(session.Network.IsDeveloperSolo, Is.True);
            Assert.That(session.State.stageIndex, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameLobbyView>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<LabLobbyView>(FindObjectsSortMode.None), Is.Empty);
            // #492: the stage's effects were loaded behind the preparing overlay, before their first play.
            foreach (AudioClip clip in LabGumihoSound.ClipFolders.Concat(LabBattleAudioWarmup.CommonFolders).SelectMany(Resources.LoadAll<AudioClip>))
                Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded), clip.name);
            // #482: the Host resolved the round's judgment mask before any hit.
            object model = typeof(LabNetwork).GetField("model", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).GetValue(session.Network);
            var surface = (LabPaintSurface)model.GetType().GetProperty("PaintSurface").GetValue(model);
            if (surface.Pattern != LabPaintPattern.Legacy)
                Assert.That(typeof(LabPaintSurface).GetField("patternMask", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).GetValue(surface), Is.Not.Null);
            // #481: the Development/Editor frame probe follows the battle and reports it once when it leaves Playing.
            Assert.That(session.FrameProbe, Is.Not.Null, "The Editor counts as a debug build.");
            int flushes = session.FrameProbe.FlushCount;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                "^HAPIO_FRAME_SUMMARY run=\\S+ stage=1 yokai=Gumiho loading=\\d+\\.\\d\\d sync=\\d+\\.\\d\\d countdown=\\d+\\.\\d\\d reschedules=0 opening_frames=\\d+ "));
            session.ExitRun();
            yield return Until(() => session.State.phase == GameFlowPhase.GameOver && !session.IsLoadingScene, "Game Over");
            Assert.That(session.FrameProbe.FlushCount, Is.EqualTo(flushes + 1), "One summary per battle stage.");
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

        private static IEnumerator OpenHiddenDeveloper(GameLobbyView view)
        {
            yield return Until(() => GameSession.Instance == null || !GameSession.Instance.IsLoadingScreenUp,
                "The loading screen closes before the hidden DEV entry is tapped", 5f);
            Assert.That(view.screen.developerPanel.activeInHierarchy, Is.False);
            var target = view.screen.GetComponent<GameDeveloperTapTarget>();
            Assert.That(target, Is.Not.Null, "The active game menu owns its hidden entry.");
            Assert.That(target.TapArea, Is.Not.Null);
            var image = target.TapArea.GetComponent<UnityEngine.UI.Image>();
            yield return Until(() => image.raycastTarget && image.gameObject.activeInHierarchy,
                "The hidden entry receives the refreshed menu permission", 5f);
            Assert.That(image.color.a, Is.Zero, "The entry has no visible button artwork.");
            for (int tap = 1; tap <= GameDeveloperTapTarget.RequiredTaps; tap++)
            {
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)target.TapArea.transform;
                var data = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(data, hits);
                Assert.That(hits, Is.Not.Empty, "Hidden DEV tap " + tap);
                GameObject clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(clicked, Is.SameAs(target.TapArea), "The actual topmost UI hit must reach hidden DEV at tap " + tap
                    + "; area=" + rect.rect + "; depth=" + image.depth + "; raycast=" + image.raycastTarget
                    + "; canvas=" + image.canvas.sortingOrder + "; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name + "[" + hit.sortingOrder + "]")));
                ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
                if (tap < GameDeveloperTapTarget.RequiredTaps)
                {
                    Assert.That(view.screen.developerPanel.activeInHierarchy, Is.False,
                        "One or two ordinary taps must not open DEV.");
                    Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.False);
                    Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.False);
                }
            }
            yield return null; yield return null;
            Assert.That(view.screen.developerPanel.activeInHierarchy, Is.True, "Three taps within the entry window open DEV.");
            Assert.That(view.soloButton.transform.parent, Is.SameAs(view.screen.developerPanel.transform));
            Assert.That(view.teamSoloButton.transform.parent, Is.SameAs(view.screen.developerPanel.transform));
            Assert.That(view.soloButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.teamSoloButton.gameObject.activeInHierarchy, Is.True);
        }

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
            // #478: a room change is covered by the shared loading screen, which takes every tap until it closes.
            yield return Until(() => GameSession.Instance == null || !GameSession.Instance.IsLoadingScreenUp,
                "The loading screen closes before " + button.name + " is tapped", 5f);
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
