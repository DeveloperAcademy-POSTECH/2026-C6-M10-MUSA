using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using C6.Prototype.Networking;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace C6.Prototype.Lobby.Tests
{
    /// <summary>Saved lobby scene and one real NGO Host. UI-only rows are explicit fixtures, never two-device discovery evidence.</summary>
    public sealed class T10LobbyPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/RoomLobby.unity";
        private const string HostPort = "25241";
        private const string EmptyLoopbackPort = "25242";
        private T10LobbyController controller;
        private bool previousRunInBackground;
        private T10LobbySession Session => controller.Session;
        private T10LobbyHud Hud => controller.Hud;

        [UnitySetUp]
        public IEnumerator LoadSavedSceneWithoutImplicitConnection()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return LoadScene();
            yield return null;
            yield return null;
            controller = Object.FindAnyObjectByType<T10LobbyController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(Session, Is.Not.Null);
            Assert.That(Hud, Is.Not.Null);
            Assert.That(Session.Connected, Is.False);
            Assert.That(Session.Snapshot, Is.Null);
            Assert.That(Session.HostConfig, Is.Null);
            Assert.That(Session.Discovery.IsBrowsing || Session.Discovery.IsAdvertising, Is.False);
            Assert.That(Managers(), Is.Empty);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator ShutdownOnlyTheTestRoomAndUnloadOwnedObjects()
        {
            try
            {
                if (controller != null && Session != null) Session.Leave();
                yield return WaitFor(() => Managers().All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                    5, "The test-owned connection did not finish shutting down.");
                Scene loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    Scene cleanup = SceneManager.CreateScene("T10 Lobby Test Cleanup");
                    SceneManager.SetActiveScene(cleanup);
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null;
                yield return null;
                Assert.That(Object.FindObjectsByType<T10LobbyController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<T10LobbySession>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<T10LobbyHud>(), Is.Empty);
                Assert.That(Managers(), Is.Empty);
                Assert.That(Object.FindObjectsByType<EventSystem>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator SavedSceneIsPassiveAndProtectsTheUiWithinSafeArea()
        {
            Assert.That(Session.Connection.CanStart, Is.True);
            Assert.That(Hud.CreateRoomButton.interactable && Hud.BrowseButton.interactable, Is.True);
            Assert.That(Hud.ReadyButton.interactable || Hud.StartButton.interactable, Is.False);
            Assert.That(Hud.DirectExpanded, Is.False);
            Assert.That(Hud.IPv4Input.gameObject.activeInHierarchy, Is.False);
            Assert.That(Hud.CancelConnectionButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(Hud.Scroll.horizontal, Is.False);
            Assert.That(Hud.Scroll.vertical, Is.True);
            Assert.That(Hud.SafeArea, Is.Not.Null);
            Assert.That(Hud.PhaseLabel.text, Is.EqualTo("OFFLINE"));
            Rect safe = ScreenRect((RectTransform)Hud.SafeArea.transform);
            Assert.That(safe.xMin, Is.EqualTo(Screen.safeArea.xMin).Within(1f));
            Assert.That(safe.yMin, Is.EqualTo(Screen.safeArea.yMin).Within(1f));
            Assert.That(safe.xMax, Is.EqualTo(Screen.safeArea.xMax).Within(1f));
            Assert.That(safe.yMax, Is.EqualTo(Screen.safeArea.yMax).Within(1f));
            Assert.That(Object.FindObjectsByType<MonoBehaviour>()
                .Any(item => item != null && (item.GetType().Name == "T09BattleController" || item.GetType().Name == "BattleSession")), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ipv6DirectEntryKeepsTheAddressAndNumericScopeWithoutTruncation()
        {
            Hud.SetDirectExpanded(true);
            const string address = "fe80:0000:0000:0000:1234:5678:abcd:ef01%12345";
            Hud.HostAddressInput.text = address;
            Assert.That(Hud.HostAddress, Is.EqualTo(address));
            Assert.That(Hud.HostAddressInput, Is.SameAs(Hud.IPv4Input), "Preserve existing scene/test access while supporting IPv6.");
            Assert.That(Hud.HostAddressInput.characterLimit, Is.GreaterThanOrEqualTo(address.Length));
            Assert.That(Hud.HostAddressInput.keyboardType, Is.EqualTo(TouchScreenKeyboardType.ASCIICapable));
            Assert.That(((Text)Hud.HostAddressInput.placeholder).text, Does.Contain("IPv6"));
            Assert.That(Session.Connection.CanStart, Is.True, "Editing an address must not begin a connection.");
            Assert.That(Managers(), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransportAloneDoesNotDisplayAReadyWaitingRoomBeforeSnapshotAndAck()
        {
            yield return CreateHost();
            Assert.That(Session.Connected && Session.InitialStateReady, Is.True);
            var snapshotField = typeof(T10LobbySession).GetField("<Snapshot>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var configField = typeof(T10LobbySession).GetField("<HostConfig>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(snapshotField, Is.Not.Null);
            Assert.That(configField, Is.Not.Null);
            var snapshot = Session.Snapshot;
            var hostConfig = Session.HostConfig;
            try
            {
                // Explicit display fixtures over a real transport, applied and restored within
                // this frame. They do not claim a second device or a delayed network snapshot.
                snapshotField.SetValue(Session, null);
                configField.SetValue(Session, null);
                controller.RefreshView();
                Assert.That(Session.Connected, Is.True);
                Assert.That(Session.InitialStateReady, Is.False);
                Assert.That(Hud.PhaseLabel.text, Is.EqualTo("CHECKING ROOM"));
                Assert.That(Hud.StatusLabel.text, Does.Contain("Receiving room settings"));
                Assert.That(Hud.ReadyButton.gameObject.activeInHierarchy, Is.False);
                Assert.That(Hud.CancelConnectionButton.gameObject.activeInHierarchy, Is.True);

                var unacknowledged = JsonUtility.FromJson<LobbySnapshot>(JsonUtility.ToJson(snapshot));
                unacknowledged.p1.initialStateReceived = false;
                snapshotField.SetValue(Session, unacknowledged);
                configField.SetValue(Session, hostConfig);
                controller.RefreshView();
                Assert.That(Session.InitialStateReady || Session.CanReady, Is.False);
                Assert.That(Hud.PhaseLabel.text, Is.EqualTo("CHECKING ROOM"));
                Assert.That(Hud.StatusLabel.text, Does.Contain("Confirming room settings"));
                Assert.That(Hud.ReadyButton.gameObject.activeInHierarchy, Is.False);
            }
            finally
            {
                snapshotField.SetValue(Session, snapshot);
                configField.SetValue(Session, hostConfig);
                controller.RefreshView();
            }
            Assert.That(Session.InitialStateReady, Is.True);
            Assert.That(Hud.PhaseLabel.text, Is.EqualTo("LOBBY"));
            Assert.That(Hud.ReadyButton.gameObject.activeInHierarchy && Hud.ReadyButton.interactable, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator P3OptInHostShowsFiveSeatRosterAndStillCannotStartAlone()
        {
            Session.ConfigureCapacity(5); Session.ConfigureBuild("23");
            yield return CreateHost();
            Assert.That(Session.ProtocolVersion, Is.EqualTo(23));
            Assert.That(Session.Connection.MaximumParticipants, Is.EqualTo(5));
            Assert.That(Session.Snapshot.Capacity, Is.EqualTo(5));
            Assert.That(Session.Snapshot.OrderedPlayers, Has.Length.EqualTo(1));
            Assert.That(Hud.ParticipantsLabel.text, Does.Contain("1 / 5"));
            Assert.That(Hud.PlayerRosterLabel.gameObject.activeInHierarchy, Is.True);
            Assert.That(Hud.PlayerRosterLabel.text, Does.Contain("P1  (YOU)"));
            Assert.That(Session.StartMatch(), Is.False);
            Assert.Throws<InvalidOperationException>(() => Session.ConfigureCapacity(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator P3FivePlayerDisplayFitsEveryReadyRowBeforeButtons()
        {
            // Explicit logical 320x700 UI fixture, not physical-device Safe Area evidence.
            var column = (RectTransform)Hud.Canvas.transform.Find("SafeArea/CenteredColumn");
            column.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 320f);
            column.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 700f);
            Hud.SetState(new LobbyUiState { Connected = true, Multiparty = true, Participants = "5 / 5",
                LocalPlayer = "P3", LeftPlayer = "P2", RightPlayer = "P4",
                PlayerRoster = "P1  /  READY\nP2  /  READY\nP3  (YOU)  /  CHECKING SETTINGS\nP4  /  READY\nP5  /  NOT READY" });
            Canvas.ForceUpdateCanvases();
            Assert.That(Hud.PlayerRosterLabel.text.Split('\n'), Has.Length.EqualTo(5));
            Assert.That(Hud.PlayerRosterLabel.preferredHeight, Is.LessThanOrEqualTo(Hud.PlayerRosterLabel.rectTransform.rect.height));
            Rect roster = ScreenRect(Hud.PlayerRosterLabel.rectTransform);
            Rect ready = ScreenRect((RectTransform)Hud.ReadyButton.transform);
            Assert.That(roster.yMin, Is.GreaterThan(ready.yMax));
            Assert.That(Hud.LeftPlayerLabel.text, Does.Contain("P2"));
            Assert.That(Hud.RightPlayerLabel.text, Does.Contain("P4"));
            Assert.That(Hud.Scroll.vertical, Is.True);
            Assert.That(Hud.Content.rect.height, Is.GreaterThan(Hud.Scroll.viewport.rect.height));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreatingARealHostConfirmsP1AndConfigButCannotStartAlone()
        {
            yield return CreateHost();
            Assert.That(Session.IsHost, Is.True);
            Assert.That(Session.Snapshot.ParticipantCount, Is.EqualTo(1));
            Assert.That(Session.Snapshot.p1.playerNumber, Is.EqualTo(1));
            Assert.That(Session.Snapshot.p1.initialStateReceived, Is.True);
            Assert.That(Session.Snapshot.p2, Is.Null);
            Assert.That(Session.LocalPlayer.playerNumber, Is.EqualTo(1));
            Assert.That(Session.HostConfig, Is.Not.Null);
            Assert.That(LobbyWire.ValidSnapshot(Session.Snapshot), Is.True);
            Assert.That(Session.CanReady, Is.True);
            Assert.That(Session.CanStart || Session.StartMatch(), Is.False);
            Assert.That(Hud.LocalPlayerLabel.text, Does.Contain("P1"));
            Assert.That(Hud.LeftPlayerLabel.text, Does.Contain("—"));
            Assert.That(Hud.RightPlayerLabel.text, Does.Contain("—"));
            Assert.That(Hud.ParticipantsLabel.text, Does.Contain("1 / 2"));
            Assert.That(Hud.ReadyButton.interactable, Is.True);
            Assert.That(Hud.StartButton.interactable, Is.False);
            Assert.That(Session.Snapshot.roundId, Is.Zero);
            Assert.That(Session.Snapshot.start, Is.Null);
            Debug.Log("C6_T10A_PLAYMODE actualNgoHost=true participants=1 initialConfigConfirmed=true hostP1=true startBlocked=true physicalDevice=false discoveryVerification=false");
        }

        [UnityTest]
        public IEnumerator ReadyAndUnreadyAreHostConfirmedWithoutStartingARound()
        {
            yield return CreateHost();
            ulong initial = Session.Snapshot.revision;
            Hud.ReadyButton.onClick.Invoke();
            yield return WaitFor(() => Session.LocalReady && !Session.HasPending, 2, "Ready was not confirmed.");
            Assert.That(Session.Snapshot.revision, Is.EqualTo(initial + 1));
            Assert.That(Session.LastReply.accepted, Is.True);
            Assert.That(Session.CanStart || Hud.StartButton.interactable, Is.False);
            Hud.ReadyButton.onClick.Invoke();
            yield return WaitFor(() => !Session.LocalReady && !Session.HasPending, 2, "Unready was not confirmed.");
            Assert.That(Session.Snapshot.revision, Is.EqualTo(initial + 2));
            Assert.That(Session.Snapshot.phase, Is.EqualTo(LobbyProtocol.Lobby));
            Assert.That(Session.Snapshot.roundId, Is.Zero);
            Assert.That(Session.Snapshot.start, Is.Null);
        }

        [UnityTest]
        public IEnumerator RejectedStartAtTheSameRevisionReleasesPendingAndAllowsTheNextAction()
        {
            yield return CreateHost();
            Assert.That(Session.ToggleReady(), Is.True);
            yield return WaitFor(() => Session.LocalReady && !Session.HasPending, 2, "Host Ready did not finish.");
            ulong revision = Session.Snapshot.revision;
            string previousRequest = Session.LastReply.requestId;
            // Explicitly send an untrusted Start while alone; the UI correctly disables this action.
            Assert.That(Session.RequestStartForValidation(), Is.True);
            yield return WaitFor(() => Session.LastReply != null && Session.LastReply.requestId != previousRequest && !Session.HasPending,
                2, "A same-revision rejection did not resolve its request receipt and left Ready locked.");
            Assert.That(Session.LastReply.accepted, Is.False);
            Assert.That(Session.LastReply.reason, Is.EqualTo("BOTH_READY_REQUIRED"));
            Assert.That(Session.Snapshot.revision, Is.EqualTo(revision));
            Assert.That(Session.Snapshot.phase, Is.EqualTo(LobbyProtocol.Lobby));
            Assert.That(Hud.ErrorLabel.gameObject.activeSelf, Is.True);
            Assert.That(Hud.ErrorLabel.text, Does.Contain("Both players"));
            Assert.That(Session.ToggleReady(), Is.True);
            yield return WaitFor(() => !Session.LocalReady && !Session.HasPending, 2, "A resolved rejection left later actions blocked.");
            Debug.Log("C6_T10A_PLAYMODE validationRequest=Start participants=1 reason=BOTH_READY_REQUIRED sameRevisionReceiptResolved=true nextReadyActionConfirmed=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator LeaveAndCreateAgainUseNewIdentityAndClearReadyAndStartState()
        {
            yield return CreateHost();
            Assert.That(Session.ToggleReady(), Is.True);
            yield return WaitFor(() => Session.LocalReady && !Session.HasPending, 2, "Ready did not finish before Leave.");
            string oldRoom = Session.Snapshot.roomId;
            string oldSession = Session.Snapshot.sessionId;
            Hud.LeaveButton.onClick.Invoke();
            yield return WaitFor(() => Session.Connection.CanStart && !Session.Connected, 5, "Leave did not release the Host.");
            Assert.That(Session.Snapshot, Is.Null);
            Assert.That(Session.HostConfig, Is.Null);
            Assert.That(Session.HasPending || Session.LocalReady, Is.False);
            Assert.That(Session.Discovery.IsAdvertising, Is.False);
            yield return CreateHost();
            Assert.That(Session.Snapshot.roomId, Is.Not.EqualTo(oldRoom));
            Assert.That(Session.Snapshot.sessionId, Is.Not.EqualTo(oldSession));
            Assert.That(Session.LocalReady || Session.CanStart || Session.HasPending, Is.False);
            Assert.That(Session.Snapshot.p2, Is.Null);
            Assert.That(Session.Snapshot.roundId, Is.Zero);
            Assert.That(Session.Snapshot.start, Is.Null);
            Assert.That(Session.LastReply, Is.Null);
        }

        [UnityTest]
        public IEnumerator DiscoveryRowFixturesAndExpandedFallbackAreReachableWithoutOverlappingButtons()
        {
            // Presentation fixture only: these rows were not discovered and cannot authorize a real Join.
            controller.enabled = false;
            Hud.SetRooms(new[]
            {
                new LobbyRoomRow { Id = "display-available", Title = "Nearby friend", Address = "192.168.1.2:7777", Status = "1 / 2 players · Available", CanJoin = true },
                new LobbyRoomRow { Id = "display-full", Title = "A full room", Address = "192.168.1.3:7777", Status = "2 / 2 players · Room full", CanJoin = false },
                new LobbyRoomRow { Id = "display-version", Title = "An older room", Address = "192.168.1.4:7777", Status = "Different app version", CanJoin = false }
            });
            Hud.SetDirectExpanded(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(Hud.RoomJoinButtons.Count, Is.EqualTo(3));
            Assert.That(Hud.RoomJoinButtons[0].interactable, Is.True);
            Assert.That(Hud.RoomJoinButtons[1].interactable || Hud.RoomJoinButtons[2].interactable, Is.False);
            Assert.That(Hud.Content.rect.height, Is.GreaterThan(Hud.Scroll.viewport.rect.height));
            var buttons = new[] { Hud.CreateRoomButton, Hud.BrowseButton, Hud.RefreshButton, Hud.CancelBrowseButton,
                Hud.DirectToggleButton, Hud.JoinDirectButton }.Concat(Hud.RoomJoinButtons).ToArray();
            foreach (Button button in buttons)
            {
                Rect rect = ((RectTransform)button.transform).rect;
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(48f));
                Assert.That(rect.width, Is.GreaterThan(44f));
            }
            for (int a = 0; a < buttons.Length; a++)
                for (int b = a + 1; b < buttons.Length; b++)
                    Assert.That(ScreenRect((RectTransform)buttons[a].transform).Overlaps(ScreenRect((RectTransform)buttons[b].transform)),
                        Is.False, buttons[a].name + " overlaps " + buttons[b].name);
            Hud.Scroll.verticalNormalizedPosition = 0f;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Rect viewport = ScreenRect(Hud.Scroll.viewport);
            Assert.That(viewport.Contains(ScreenRect((RectTransform)Hud.JoinDirectButton.transform).center), Is.True,
                "The expanded fallback must be reachable by scrolling.");
            controller.enabled = true;
        }

        [UnityTest]
        public IEnumerator DeveloperModeBuildsOneRoomConfigAndLeavesProjectDefaultsUntouched()
        {
            Assert.That(Hud.DeveloperSettingsReady, Is.True);
            Assert.That(Hud.DeveloperModeEnabled, Is.False);
            Assert.That(Hud.DeveloperPanel.gameObject.activeSelf, Is.False);
            Assert.That(Hud.DeveloperModeButton.interactable, Is.True);
            LobbyHostConfig defaults = Session.CaptureRoomDefaults();
            string defaultsJson = JsonUtility.ToJson(defaults);
            float collapsedHeight = Hud.Content.rect.height;

            Hud.DeveloperModeButton.onClick.Invoke();
            yield return null;
            var column = (RectTransform)Hud.Canvas.transform.Find("SafeArea/CenteredColumn");
            column.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 320f);
            Canvas.ForceUpdateCanvases();
            Assert.That(Hud.DeveloperModeEnabled, Is.True);
            Assert.That(Hud.DeveloperPanel.gameObject.activeInHierarchy, Is.True);
            Assert.That(Hud.Content.rect.height, Is.GreaterThan(collapsedHeight));
            Assert.That(Hud.Scroll.vertical, Is.True);

            Button[] developerButtons = Hud.DeveloperPanel.GetComponentsInChildren<Button>(false);
            Assert.That(developerButtons,
                Has.Length.EqualTo(LobbyDeveloperSettings.OrderedSettings.Count * 2 + 1));
            foreach (Button button in developerButtons)
            {
                Rect rect = ((RectTransform)button.transform).rect;
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(44f), button.name + " height");
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(44f), button.name + " width");
            }
            for (int a = 0; a < developerButtons.Length; a++)
                for (int b = a + 1; b < developerButtons.Length; b++)
                    Assert.That(ScreenRect((RectTransform)developerButtons[a].transform)
                        .Overlaps(ScreenRect((RectTransform)developerButtons[b].transform)),
                        Is.False, developerButtons[a].name + " overlaps " + developerButtons[b].name);
            Assert.That(ScreenRect(Hud.DeveloperPanel)
                .Overlaps(ScreenRect((RectTransform)Hud.CreateRoomButton.transform)),
                Is.False, "Developer settings must not overlap CREATE ROOM.");

            Hud.AdjustDeveloperSetting(LobbyDeveloperSetting.MonsterHp2, -1);
            Hud.AdjustDeveloperSetting(LobbyDeveloperSetting.OrbStorageLimit, -1);
            Hud.AdjustDeveloperSetting(LobbyDeveloperSetting.OrbSizePercent, 1);
            LobbyHostConfig draft = Hud.BuildRoomConfig();
            Assert.That(draft.monsterHp2, Is.EqualTo(Mathf.Max(1, defaults.monsterHp2 - 100)));
            Assert.That(draft.storageLimit, Is.EqualTo(Mathf.Max(1, defaults.storageLimit - 1)));
            Assert.That(draft.orbRadiusScreenFraction,
                Is.EqualTo(Mathf.Clamp(defaults.orbRadiusScreenFraction * 1.1f, .01f, .2f)).Within(.0001f));
            Assert.That(draft.orbRadiusCapScale,
                Is.EqualTo(Mathf.Clamp(defaults.orbRadiusCapScale * 1.1f, 1f, 3f)).Within(.0001f));

            Hud.RoomNameInput.text = "Developer Room";
            Hud.PortInput.text = HostPort;
            Hud.CreateRoomButton.onClick.Invoke();
            yield return WaitFor(() => Session.IsHost && Session.HostConfig != null, 5,
                "Developer room did not create a Host.");
            Assert.That(JsonUtility.ToJson(Session.HostConfig), Is.EqualTo(JsonUtility.ToJson(draft)));
            Assert.That(JsonUtility.ToJson(Session.CaptureRoomDefaults()), Is.EqualTo(defaultsJson));
            Assert.That(Hud.DeveloperPanel.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator ControllerDisableDetachesActionsAndReenableDoesNotDuplicateSubscriptions()
        {
            controller.enabled = false;
            Hud.RoomNameInput.text = "Detached UI";
            Hud.PortInput.text = HostPort;
            Hud.CreateRoomButton.onClick.Invoke();
            yield return null;
            Assert.That(Session.Connected, Is.False);
            Assert.That(Managers(), Is.Empty);
            controller.enabled = true;
            yield return null;
            Hud.CreateRoomButton.onClick.Invoke();
            yield return WaitFor(() => Session.IsHost && Session.Snapshot != null, 5, "Reenabled UI did not create a Host.");
            ulong revision = Session.Snapshot.revision;
            Hud.ReadyButton.onClick.Invoke();
            yield return WaitFor(() => !Session.HasPending, 2, "Ready receipt remained pending.");
            Assert.That(Session.LocalReady, Is.True, "Duplicate listeners would toggle Ready twice.");
            Assert.That(Session.Snapshot.revision, Is.EqualTo(revision + 1));
        }

        [UnityTest]
        public IEnumerator CancelAnUnansweredLoopbackJoinWithoutCreatingAHostOrRetainingAnInitialState()
        {
            // A real client connection attempt to an unused local port, not a simulated second participant.
            Hud.SetDirectExpanded(true);
            Hud.IPv4Input.text = "127.0.0.1";
            Hud.PortInput.text = EmptyLoopbackPort;
            Hud.JoinDirectButton.onClick.Invoke();
            Assert.That(Session.Connection.State, Is.EqualTo(DirectConnectionState.Connecting));
            Assert.That(Session.JoinRoute, Is.EqualTo("DIRECT_IP"));
            Assert.That(Session.Snapshot, Is.Null);
            Assert.That(Hud.CancelConnectionButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(Hud.CreateRoomButton.interactable || Hud.JoinDirectButton.interactable, Is.False);
            Hud.CancelConnectionButton.onClick.Invoke();
            yield return WaitFor(() => Session.Connection.CanStart && !Session.Connected, 5, "Connection cancellation did not release the transport.");
            Assert.That(Session.Snapshot, Is.Null);
            Assert.That(Session.HostConfig, Is.Null);
            Assert.That(Session.CanReady || Session.CanStart || Session.HasPending, Is.False);
            Assert.That(Hud.CancelConnectionButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(Hud.CreateRoomButton.interactable, Is.True);
            Assert.That(Managers().Any(manager => manager.IsHost || manager.IsListening), Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingAnIdleBrowsingSessionReleasesNativeOperationsWithoutAConnection()
        {
            Assert.That(Session.Connection.CanStart, Is.True);
            Assert.That(Session.Browse(), Is.True, "This macOS/iOS integration test must start a real Bonjour browse.");
            Assert.That(Session.Discovery.IsBrowsing, Is.True);
            Assert.That(Session.Discovery.ActiveNativeOperations, Is.GreaterThan(0));
            Assert.That(Session.Connected, Is.False);
            Session.enabled = false;
            yield return null;
            Assert.That(Session.Discovery.IsBrowsing || Session.Discovery.IsAdvertising, Is.False);
            Assert.That(Session.Discovery.ActiveNativeOperations, Is.Zero,
                "An idle browse must be cancelled even when no NGO connection exists.");
            Assert.That(Session.Rooms, Is.Empty);
            Assert.That(Managers(), Is.Empty);
            Session.enabled = true;
            yield return null;
            Assert.That(Session.Discovery.IsBrowsing, Is.False, "Reenable must not restart browsing without a user action.");
            Assert.That(Session.Discovery.ActiveNativeOperations, Is.Zero);
            Assert.That(Session.Snapshot, Is.Null);
            Debug.Log("C6_T10A_PLAYMODE actualBonjourBrowseStarted=true sessionDisabledWhileIdle=true nativeOperationsReleased=true automaticBrowseRestart=false discoveredRoomVerification=false physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator SimulatedPauseCallbackEndsARealHostAndResumeDoesNotRestoreItsRoom()
        {
            yield return CreateHost();
            string oldRoom = Session.Snapshot.roomId;
            Assert.That(Session.ToggleReady(), Is.True);
            yield return WaitFor(() => Session.LocalReady && !Session.HasPending, 2, "Ready did not finish before the simulated pause.");
            Session.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            yield return WaitFor(() => !Session.Connected && Session.Connection.CanStart &&
                Managers().All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                5, "The simulated pause callback did not end its real Host transport.");
            Assert.That(Session.Snapshot, Is.Null);
            Assert.That(Session.HostConfig, Is.Null);
            Assert.That(Session.LocalReady || Session.HasPending, Is.False);
            Assert.That(Session.Discovery.IsBrowsing || Session.Discovery.IsAdvertising, Is.False);
            Assert.That(Session.Discovery.ActiveNativeOperations, Is.Zero);
            Session.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            yield return null;
            yield return null;
            Assert.That(Session.Connected || Session.Discovery.IsBrowsing || Session.Discovery.IsAdvertising, Is.False);
            Assert.That(Session.Snapshot, Is.Null, "A resume callback must not restore the previous room.");
            Assert.That(Hud.CreateRoomButton.interactable, Is.True);
            yield return CreateHost();
            Assert.That(Session.Snapshot.roomId, Is.Not.EqualTo(oldRoom));
            Assert.That(Session.LocalReady, Is.False);
            Debug.Log("C6_T10A_PLAYMODE source=SIMULATED_PAUSE_CALLBACK actualNgoHostEnded=true nativeOperationsReleased=true automaticRoomResume=false explicitCreateNewRoom=true actualOSSuspension=false physicalDevice=false");
        }

        private IEnumerator CreateHost()
        {
            Hud.RoomNameInput.text = "T10 PlayMode Room";
            Hud.PortInput.text = HostPort;
            Assert.That(Session.Connection.CanStart, Is.True);
            Hud.CreateRoomButton.onClick.Invoke();
            yield return WaitFor(() => Session.IsHost && Session.Snapshot != null && Session.LocalPlayer != null,
                5, "The saved scene did not establish its real one-participant NGO Host.");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static NetworkManager[] Managers() => Object.FindObjectsByType<NetworkManager>();
        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static IEnumerator WaitFor(Func<bool> condition, double seconds, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        private static AsyncOperation LoadScene()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        }
    }
}
