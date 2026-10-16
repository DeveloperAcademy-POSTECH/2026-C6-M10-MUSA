using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>Saved Canvas references are editable in the Scene; only values are bound in code.</summary>
    public sealed class LabHud : MonoBehaviour
    {
        public LabNetwork network;
        public GameObject sessionControls;
        public InputField hostAddress;
        public InputField port;
        public Button hostButton;
        public Button joinButton;
        public Button findHostButton;
        public Button decreasePlayersButton;
        public Button increasePlayersButton;
        public Text roomNameText;
        public GameObject roomListPanel;
        public Button refreshRoomsButton;
        public Button cancelRoomSearchButton;
        public RectTransform roomListContent;
        public Button roomListRowTemplate;
        public Text roomSearchStatusText;
        public Button startButton;
        public Button leaveButton;
        public Text statusText;
        public Text noticeText;
        public Text peersText;
        public Text hpText;
        public Text playerHpText;
        public Text timeText;
        public Text staminaText;
        public Text orbsText;
        public Text seatText;
        [Header("Saved lobby player slots")]
        public GameObject playerSlotPanel;
        public Button[] playerSlotButtons;
        public Text playerSlotStatusText;
        [Header("Saved battle UI")]
        public GameObject battleUi;
        [Header("Battle-only camera background")]
        public Camera battleCamera;
        public Camera orbCamera;
        public Color battleBackgroundColor = new Color(.32f, .42f, .49f, 1f);
        public Color orbBackgroundColor = new Color(.18f, .24f, .29f, 1f);
        public UnityEngine.UI.Image yokaiHpFill;
        public UnityEngine.UI.Image playerHpFill;
        public UnityEngine.UI.Image staminaFill;
        public UnityEngine.UI.Image[] staminaCells;
        public GameObject leftNeighborBadge;
        public GameObject rightNeighborBadge;
        public Text leftNeighborText;
        public Text rightNeighborText;
        public Image localPlayerCircle;
        public Image leftPlayerCircle;
        public Image rightPlayerCircle;
        public Sprite[] playerCircleSprites;
        public Button pauseButton;
        public GameObject pauseMenu;
        public Button resumeButton;
        public Button returnLobbyButton;
        public Button developerSettingsButton;
        public Text pauseStatusText;
        public LabDeveloperMode developerMode;
        [Header("Saved battle result controls")]
        public GameObject resultControls;
        public Text resultStatusText;
        public Button resultRestartButton;
        public Button resultReturnLobbyButton;
        public Button resultLeaveButton;

        private readonly List<Button> roomRows = new List<Button>();
        private readonly Text[] playerSlotButtonLabels = new Text[LabModel.MaximumParticipants];
        private string renderedRoomList = string.Empty;
        private Camera capturedBattleCamera;
        private Camera capturedOrbCamera;
        private Color lobbyBattleBackgroundColor;
        private Color lobbyOrbBackgroundColor;
        private UnityEngine.UI.Text goalTitle;
        private bool goalTitleResolved;

        private void Awake()
        {
            CacheGoalTitle();
            if (hostButton != null) hostButton.onClick.AddListener(Host);
            if (joinButton != null) joinButton.onClick.AddListener(Join);
            if (findHostButton != null) findHostButton.onClick.AddListener(FindHost);
            if (decreasePlayersButton != null) decreasePlayersButton.onClick.AddListener(() => ChangePlayerCount(-1));
            if (increasePlayersButton != null) increasePlayersButton.onClick.AddListener(() => ChangePlayerCount(1));
            if (refreshRoomsButton != null) refreshRoomsButton.onClick.AddListener(RefreshRooms);
            if (cancelRoomSearchButton != null) cancelRoomSearchButton.onClick.AddListener(() => network?.CancelRoomSearch());
            if (playerSlotButtons != null)
            {
                for (int i = 0; i < playerSlotButtons.Length && i < LabModel.MaximumParticipants; i++)
                {
                    Button button = playerSlotButtons[i];
                    if (button == null) continue;
                    int slotNumber = i + 1;
                    playerSlotButtonLabels[i] = button.GetComponentInChildren<Text>(true);
                    button.onClick.AddListener(() =>
                    {
                        if (network != null)
                            network.RequestPlayerSlot(network.LocalSlotNumber == slotNumber ? 0 : slotNumber);
                    });
                }
            }
            if (startButton != null) startButton.onClick.AddListener(() => network?.StartBattle());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => network?.Stop());
            if (pauseButton != null) pauseButton.onClick.AddListener(() => network?.PauseBattle());
            if (resumeButton != null) resumeButton.onClick.AddListener(() => network?.ResumeBattle());
            if (returnLobbyButton != null) returnLobbyButton.onClick.AddListener(() => network?.ReturnAllToLobby());
            if (developerSettingsButton != null) developerSettingsButton.onClick.AddListener(OpenDeveloperSettings);
            if (resultRestartButton != null) resultRestartButton.onClick.AddListener(RestartFromResult);
            if (resultReturnLobbyButton != null) resultReturnLobbyButton.onClick.AddListener(() => network?.ReturnAllToLobby());
            if (resultLeaveButton != null) resultLeaveButton.onClick.AddListener(() => network?.Stop());
        }

        private void Start()
        {
            if (hostAddress != null && network != null) hostAddress.text = network.hostAddress;
            if (port != null && network != null) port.text = network.port.ToString();
            SetVisible(roomListPanel, false);
            SetVisible(roomListRowTemplate == null ? null : roomListRowTemplate.gameObject, false);
        }

        private void Update()
        {
            if (network == null) return;
            var snapshot = network.Snapshot;
            bool paintGoal = network.config == null || network.config.GourdMode == LabGourdMode.Paint;
            CacheGoalTitle();
            Set(goalTitle, GourdGoalTitle(paintGoal ? LabGourdMode.Paint : LabGourdMode.Crack));
            bool playing = snapshot != null && snapshot.phase == LabPhase.Playing;
            bool terminal = snapshot != null && (snapshot.phase == LabPhase.Victory || snapshot.phase == LabPhase.Defeat);
            terminal = terminal && !LabGourdFinish.DelayVictory(network, snapshot);
            bool battleVisible = snapshot != null && snapshot.phase != LabPhase.Idle;
            bool paused = playing && network.IsBattlePaused;
            UpdateBattleBackground(battleVisible);
            SetVisible(battleUi, battleVisible);
            SetVisible(pauseMenu, paused);
            if (paused) BringPauseLayersForward();
            bool hasNeighbors = battleVisible && (snapshot.players?.Length ?? 0) > 1;
            SetVisible(leftNeighborBadge, hasNeighbors);
            SetVisible(rightNeighborBadge, hasNeighbors);
            SetVisible(pauseButton == null ? null : pauseButton.gameObject, playing);
            SetVisible(sessionControls, !battleVisible);
            SetVisible(statusText == null ? null : statusText.gameObject, !battleVisible);
            SetVisible(resultControls, terminal);
            if (terminal)
                Set(resultStatusText, snapshot.phase == LabPhase.Victory ? "VICTORY" : "DEFEAT");
            bool connected = network.IsConnected;
            bool searching = network.IsSearchingRooms;
            bool hostLobby = connected && network.IsHost && snapshot == null;
            bool playerSlotLobby = connected && snapshot == null && !network.IsDeveloperSolo;
            SetVisible(playerSlotPanel, false);
            RefreshPlayerSlots(false);
            SetVisible(roomListPanel, searching && !connected);
            SetVisible(decreasePlayersButton == null ? null : decreasePlayersButton.gameObject, !connected && !searching);
            SetVisible(increasePlayersButton == null ? null : increasePlayersButton.gameObject, !connected && !searching);
            SetVisible(hostAddress == null ? null : hostAddress.gameObject, !hostLobby);
            SetVisible(port == null ? null : port.gameObject, !hostLobby);
            SetVisible(roomNameText == null ? null : roomNameText.gameObject, hostLobby);
            if (hostLobby)
                Set(roomNameText, network.HostRoomNamePending
                    ? "REGISTERING ROOM…"
                    : string.IsNullOrEmpty(network.HostRoomName)
                        ? "ROOM NAME UNAVAILABLE · JOIN BY HOST IP : " + network.port
                        : "ROOM " + network.HostRoomName);
            if (decreasePlayersButton != null)
                decreasePlayersButton.interactable = network.CanChooseConnection && network.TargetParticipantCount > 2;
            if (increasePlayersButton != null)
                increasePlayersButton.interactable = network.CanChooseConnection && network.TargetParticipantCount < 5;
            if (refreshRoomsButton != null) refreshRoomsButton.interactable = searching;
            if (cancelRoomSearchButton != null) cancelRoomSearchButton.interactable = searching;
            RefreshRoomRows(searching);
            if (hostButton != null) hostButton.interactable = network.CanChooseConnection;
            if (joinButton != null) joinButton.interactable = network.CanChooseConnection;
            if (findHostButton != null) findHostButton.interactable = network.CanChooseConnection;
            if (startButton != null) startButton.interactable = network.CanStartBattle;
            if (pauseButton != null) pauseButton.interactable = network.CanPauseBattle;
            if (resumeButton != null) resumeButton.interactable = network.CanResumeBattle;
            if (returnLobbyButton != null) returnLobbyButton.interactable = network.CanReturnAllToLobby;
            SetVisible(returnLobbyButton == null ? null : returnLobbyButton.gameObject, network.IsHost);
            SetVisible(developerSettingsButton == null ? null : developerSettingsButton.gameObject,
                network.IsHost && network.DeveloperBuildAvailable);
            if (developerSettingsButton != null)
                developerSettingsButton.interactable = network.DeveloperBuildAvailable
                    && network.IsBattlePaused && network.IsHost
                    && (network.CanRestartSharedBattle || network.IsDeveloperSolo);
            if (resultRestartButton != null)
            {
                SetVisible(resultRestartButton.gameObject, terminal && network.IsHost);
                resultRestartButton.interactable = terminal && network.IsHost
                    && (network.IsDeveloperSolo || network.CanStartBattle);
            }
            if (resultReturnLobbyButton != null)
            {
                SetVisible(resultReturnLobbyButton.gameObject, terminal && network.IsHost && !network.IsDeveloperSolo);
                resultReturnLobbyButton.interactable = terminal && network.CanReturnAllToLobby;
            }
            if (resultLeaveButton != null)
            {
                SetVisible(resultLeaveButton.gameObject, terminal && (!network.IsHost || network.IsDeveloperSolo));
                resultLeaveButton.interactable = terminal && connected;
            }
            if (terminal && !network.IsHost)
                Set(resultStatusText, (snapshot.phase == LabPhase.Victory ? "VICTORY" : "DEFEAT") + " · WAITING FOR HOST");
            Set(pauseStatusText, network.IsHost ? "PAUSED" : "PAUSED · HOST CONTROLS");
            if (leaveButton != null) leaveButton.interactable = connected || !network.CanChooseConnection;
            Set(statusText, network.Status);
            Set(noticeText, network.Notice);
            Set(peersText, network.IsDeveloperSolo
                ? "DEVELOPER SOLO 1 / 1"
                : connected
                    ? "PLAYERS " + network.ParticipantCount + " / " + network.TargetParticipantCount
                    : "ROOM PLAYERS " + network.TargetParticipantCount + " / 5");
            if (snapshot == null)
            {
                Set(hpText, "—");
                Set(playerHpText, "—");
                Set(timeText, "--:--");
                Set(staminaText, "STAMINA —");
                Set(orbsText, "ORBS —");
                Set(seatText, connected
                    ? network.IsDeveloperSolo ? "DEVELOPER SOLO" : network.LocalSlotNumber > 0
                        ? "P" + network.LocalSlotNumber : "P— · CHOOSE SLOT"
                    : "NO SESSION");
                Set(leftNeighborText, "P—");
                Set(rightNeighborText, "P—");
                SetFill(yokaiHpFill, 0f);
                SetFill(playerHpFill, 0f);
                SetStaminaCells(0f);
                return;
            }
            int goalPercent = PaintGoalPercentFor(snapshot, network.config);
            Set(hpText, paintGoal
                ? FormatPaintGoalProgress(snapshot.paintCoverage, goalPercent)
                : snapshot.hp + " / " + network.config.MonsterMaxHp);
            int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt((float)snapshot.remaining));
            Set(timeText, (remainingSeconds / 60).ToString("00") + ":" + (remainingSeconds % 60).ToString("00"));
            LabPlayerState local = Array.Find(snapshot.players ?? Array.Empty<LabPlayerState>(), p => p.id == network.LocalPlayerId);
            Set(playerHpText, local == null || network.config == null
                ? "—"
                : local.personalHp + " / " + network.config.PlayerMaxHp);
            Set(staminaText, local == null ? "STAMINA —" : "STAMINA " + Mathf.FloorToInt(local.stamina) + " / " + network.config.StaminaMax);
            int count = 0;
            foreach (var orb in snapshot.orbs ?? Array.Empty<LabOrbState>()) if (orb.owner == network.LocalPlayerId && !orb.inFlight) count++;
            Set(orbsText, "ORBS " + count + " / " + network.config.OrbStorageLimit);
            Set(seatText, local == null ? "P—" : "P" + (local.seat + 1)
                + (network.IsDeveloperSolo ? " · SOLO" : string.Empty));
            SetFill(yokaiHpFill, paintGoal
                ? PaintGoalFill(snapshot.paintCoverage, goalPercent)
                : Fraction(snapshot.hp, network.config.MonsterMaxHp));
            SetFill(playerHpFill, local == null ? 0f : Fraction(local.personalHp, network.config.PlayerMaxHp));
            SetStaminaCells(local == null ? 0f : Fraction(local.stamina, network.config.StaminaMax));
            GetNeighborLabels(snapshot, network.LocalPlayerId, out string left, out string right);
            Set(leftNeighborText, left);
            Set(rightNeighborText, right);
            SetPlayerCircle(localPlayerCircle, local == null ? 0 : local.seat + 1);
            SetPlayerCircle(leftPlayerCircle, ParsePlayerNumber(left));
            SetPlayerCircle(rightPlayerCircle, ParsePlayerNumber(right));
        }

        private void CacheGoalTitle()
        {
            if (goalTitleResolved || hpText == null) return;
            goalTitleResolved = true;
            // Saved Lab: FigmaYokaiHpBar/{hpText, TitleMask/Title}. Resolve only the
            // title beside this bound value; never search arbitrary Text objects or hide UI.
            Transform bar = hpText.transform.parent;
            Transform title = bar != null ? bar.Find("TitleMask/Title") : null;
            if (title != null && title != hpText.transform)
                goalTitle = title.GetComponent<UnityEngine.UI.Text>();
        }

        /// <summary>The authoritative round goal wins; old fixtures without it use Config.</summary>
        public static int PaintGoalPercentFor(LabSnapshot snapshot, LabConfig tuning)
            => snapshot != null && snapshot.paintGoalPercent > 0
                ? Mathf.Clamp(snapshot.paintGoalPercent, 1, 100)
                : tuning != null ? tuning.PaintVictoryPercent : LabConfig.DefaultPaintVictoryPercent;

        public static string GourdGoalTitle(LabGourdMode mode)
            => mode == LabGourdMode.Crack ? "YOKAI HP" : "GOAL";

        /// <summary>Floor to one decimal so an unfinished near-goal value never rounds up.</summary>
        public static string FormatPaintGoalProgress(float coverage, int goalPercent)
        {
            float percent = Mathf.FloorToInt(DisplayCoverage(coverage) * 1000f) / 10f;
            return "PAINT " + percent.ToString("0.0", CultureInfo.InvariantCulture)
                + "% / " + Mathf.Clamp(goalPercent, 1, 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static float PaintGoalFill(float coverage, int goalPercent)
            => Mathf.Clamp01(DisplayCoverage(coverage) / (Mathf.Clamp(goalPercent, 1, 100) / 100f));

        private static float DisplayCoverage(float coverage)
            => float.IsNaN(coverage) || float.IsInfinity(coverage) ? 0f : Mathf.Clamp01(coverage);

        private static int ParsePlayerNumber(string label)
        {
            return label != null && label.Length > 1 && int.TryParse(label.Substring(1), out int number)
                ? number : 0;
        }

        private void SetPlayerCircle(Image image, int number)
        {
            if (image == null) return;
            Sprite art = playerCircleSprites != null && number > 0 && number <= playerCircleSprites.Length
                ? playerCircleSprites[number - 1] : null;
            image.enabled = art != null;
            if (image.sprite != art) image.sprite = art;
        }

        private void OnDisable()
        {
            if (capturedBattleCamera != null) capturedBattleCamera.backgroundColor = lobbyBattleBackgroundColor;
            if (capturedOrbCamera != null) capturedOrbCamera.backgroundColor = lobbyOrbBackgroundColor;
        }

        private void UpdateBattleBackground(bool battleVisible)
        {
            ApplyCameraBackground(battleCamera, battleBackgroundColor, battleVisible,
                ref capturedBattleCamera, ref lobbyBattleBackgroundColor);
            ApplyCameraBackground(orbCamera, orbBackgroundColor, battleVisible,
                ref capturedOrbCamera, ref lobbyOrbBackgroundColor);
        }

        private static void ApplyCameraBackground(Camera camera, Color battleColor, bool battleVisible,
            ref Camera capturedCamera, ref Color lobbyColor)
        {
            if (capturedCamera != camera)
            {
                if (capturedCamera != null) capturedCamera.backgroundColor = lobbyColor;
                capturedCamera = camera;
                if (camera != null) lobbyColor = camera.backgroundColor;
            }
            if (camera == null) return;
            Color color = battleVisible ? battleColor : lobbyColor;
            if (camera.backgroundColor != color) camera.backgroundColor = color;
        }

        private void OpenDeveloperSettings()
        {
            if (network == null || !network.DeveloperBuildAvailable || !network.IsHost
                || !network.IsBattlePaused || (!network.CanRestartSharedBattle && !network.IsDeveloperSolo)) return;
            if (developerMode == null) developerMode = GetComponent<LabDeveloperMode>();
            developerMode?.Open();
            if (developerMode != null && developerMode.panel != null)
                developerMode.panel.transform.SetAsLastSibling();
        }

        private void RestartFromResult()
        {
            if (network == null || !network.IsHost) return;
            if (network.IsDeveloperSolo) network.RestartDeveloperSolo();
            else if (network.CanStartBattle) network.StartBattle();
        }

        private void BringPauseLayersForward()
        {
            if (pauseMenu == null) return;
            Transform overlay = pauseMenu.transform;
            Transform devPanel = developerMode != null && developerMode.panel != null
                && developerMode.panel.activeInHierarchy ? developerMode.panel.transform : null;
            if (devPanel != null && devPanel.parent == overlay.parent)
            {
                int last = overlay.parent.childCount - 1;
                if (overlay.GetSiblingIndex() != last - 1 || devPanel.GetSiblingIndex() != last)
                {
                    overlay.SetAsLastSibling();
                    devPanel.SetAsLastSibling();
                }
            }
            else if (overlay.GetSiblingIndex() != overlay.parent.childCount - 1)
            {
                overlay.SetAsLastSibling();
            }
        }

        /// <summary>Host-approved P numbers determine seats, independent of Netcode IDs.</summary>
        public static void GetNeighborLabels(LabSnapshot snapshot, ulong localId, out string left, out string right)
        {
            left = right = "P—";
            LabPlayerState[] players = snapshot?.players;
            if (players == null || players.Length < 2) return;
            LabPlayerState local = Array.Find(players, player => player != null && player.id == localId);
            if (local == null) return;
            int count = players.Length;
            LabPlayerState leftPlayer = Array.Find(players, player => player != null && player.seat == (local.seat + count - 1) % count);
            LabPlayerState rightPlayer = Array.Find(players, player => player != null && player.seat == (local.seat + 1) % count);
            if (leftPlayer != null) left = "P" + (leftPlayer.seat + 1);
            if (rightPlayer != null) right = "P" + (rightPlayer.seat + 1);
        }

        private void RefreshPlayerSlots(bool visible)
        {
            if (playerSlotButtons == null) return;
            int target = network.TargetParticipantCount;
            int selected = network.LocalSlotNumber;
            bool pending = network.SlotSelectionPending;
            if (visible)
                Set(playerSlotStatusText, pending ? "WAITING FOR HOST APPROVAL…"
                    : selected > 0 ? "YOUR POSITION: P" + selected
                    : "CHOOSE AN OPEN P NUMBER BEFORE START");

            for (int i = 0; i < playerSlotButtons.Length && i < LabModel.MaximumParticipants; i++)
            {
                Button button = playerSlotButtons[i];
                if (button == null) continue;
                int slot = i + 1;
                bool inRoom = visible && slot <= target;
                SetVisible(button.gameObject, inRoom);
                if (!inRoom) continue;

                ulong owner = network.GetSlotOwner(slot);
                bool own = owner != ulong.MaxValue && owner == network.LocalPlayerId;
                bool occupied = owner != ulong.MaxValue;
                button.interactable = network.CanSelectPlayerSlot && !pending && (!occupied || own);
                Set(playerSlotButtonLabels[i], "P" + slot + "\n" + (own ? "YOU/CLEAR" : occupied ? "TAKEN" : "OPEN"));
            }
        }

        private void SetStaminaCells(float fraction)
        {
            SetFill(staminaFill, fraction);
            if (staminaCells == null) return;
            for (int i = 0; i < staminaCells.Length; i++)
                SetFill(staminaCells[i], fraction * staminaCells.Length >= i + 1 ? 1f : 0f);
        }

        private static float Fraction(float value, float maximum) =>
            maximum > 0f ? Mathf.Clamp01(value / maximum) : 0f;

        private static void SetFill(UnityEngine.UI.Image target, float value)
        {
            if (target != null && !Mathf.Approximately(target.fillAmount, value)) target.fillAmount = value;
        }

        private void Host()
        {
            if (network == null || !TryPort()) return;
            network.Host();
        }

        private void Join()
        {
            if (network == null || !TryPort()) return;
            network.Join(hostAddress == null ? network.hostAddress : hostAddress.text);
        }

        private void FindHost()
        {
            if (network == null) return;
            network.StartRoomSearch();
        }

        private void ChangePlayerCount(int direction)
        {
            if (network != null && network.CanChooseConnection)
                network.SetTargetParticipantCount(network.TargetParticipantCount + direction);
        }

        private void RefreshRooms()
        {
            if (network == null || !network.IsSearchingRooms) return;
            network.CancelRoomSearch();
            renderedRoomList = string.Empty;
            network.StartRoomSearch();
        }

        private void RefreshRoomRows(bool searching)
        {
            if (!searching || roomListContent == null || roomListRowTemplate == null)
            {
                if (roomRows.Count > 0) ClearRoomRows();
                return;
            }

            IReadOnlyList<LabDiscoveredRoom> rooms = network.DiscoveredRooms;
            var signature = new StringBuilder();
            foreach (LabDiscoveredRoom room in rooms)
                signature.Append(room.Key).Append('|').Append(room.Name).Append(';');
            string current = signature.ToString();
            if (current != renderedRoomList)
            {
                ClearRoomRows();
                foreach (LabDiscoveredRoom room in rooms)
                {
                    Button row = Instantiate(roomListRowTemplate, roomListContent);
                    row.gameObject.name = "Room_" + room.Name;
                    row.gameObject.SetActive(true);
                    row.onClick.RemoveAllListeners();
                    string key = room.Key;
                    row.onClick.AddListener(() => network?.JoinDiscoveredRoom(key));
                    Set(row.GetComponentInChildren<Text>(), room.Name);
                    roomRows.Add(row);
                }
                renderedRoomList = current;
            }
            Set(roomSearchStatusText, network.Status.StartsWith("That room is no longer available", StringComparison.Ordinal)
                ? network.Status
                : rooms.Count == 0 ? "Searching for rooms…" : "Select a room to join.");
        }

        private void ClearRoomRows()
        {
            foreach (Button row in roomRows)
            {
                if (row == null) continue;
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            roomRows.Clear();
            renderedRoomList = string.Empty;
        }

        private bool TryPort()
        {
            if (port == null || !ushort.TryParse(port.text, out var parsed) || parsed == 0)
            {
                if (noticeText != null) noticeText.text = "Enter port 1–65535.";
                return false;
            }
            network.port = parsed;
            return true;
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
        }

        private static void Set(Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }
    }
}
