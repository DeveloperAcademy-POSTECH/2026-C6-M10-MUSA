using System;
using System.Collections;
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
        [Header("Saved quick-connect lobby controls")]
        public GameObject directConnectionPanel;
        public Button directConnectionButton;
        public Text quickPlayerCountText;
        public RectTransform roomListScrollView;
        public RectTransform roomListContent;
        public Button roomListRowTemplate;
        public Text roomSearchStatusText;
        public Button startButton;
        public Button leaveButton;
        public Text statusText;
        public Text noticeText;
        public Text peersText;
        public Text hpText;
        public bool compactPaintProgress;
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
        [Header("Saved player paint-pattern samples")]
        public Image[] playerSlotPatternSamples;
        public Image localPlayerPatternSample;
        public Image leftPlayerPatternSample;
        public Image rightPlayerPatternSample;
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
        private readonly List<Text> roomRowLabels = new List<Text>();
        private readonly List<string> roomRowKeys = new List<string>();
        private bool directConnectionExpanded;
        private bool automaticRoomSearchPending;
        private bool wasConnected;
        private string lobbyInputError = string.Empty;
        private Text directConnectionButtonLabel;
        private readonly Text[] playerSlotButtonLabels = new Text[LabModel.MaximumParticipants];
        private string renderedRoomList = string.Empty;
        private Camera capturedBattleCamera;
        private Camera capturedOrbCamera;
        private Color lobbyBattleBackgroundColor;
        private Color lobbyOrbBackgroundColor;
        private UnityEngine.UI.Text goalTitle;
        private bool goalTitleResolved;
        private LabTeamBar teamBar;
        private LabTeamFever teamFever;

        private void Awake()
        {
            CacheGoalTitle();
            if (directConnectionButton != null)
                directConnectionButtonLabel = directConnectionButton.GetComponentInChildren<Text>(true);
            if (hostButton != null) hostButton.onClick.AddListener(Host);
            if (joinButton != null) joinButton.onClick.AddListener(Join);
            if (findHostButton != null) findHostButton.onClick.AddListener(FindHost);
            if (decreasePlayersButton != null) decreasePlayersButton.onClick.AddListener(() => ChangePlayerCount(-1));
            if (increasePlayersButton != null) increasePlayersButton.onClick.AddListener(() => ChangePlayerCount(1));
            if (refreshRoomsButton != null) refreshRoomsButton.onClick.AddListener(RefreshRooms);
            if (cancelRoomSearchButton != null) cancelRoomSearchButton.onClick.AddListener(CancelConnection);
            if (directConnectionButton != null) directConnectionButton.onClick.AddListener(ToggleDirectConnection);
            if (port != null) port.onValueChanged.AddListener(_ => lobbyInputError = string.Empty);
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
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
            if (pauseButton != null) pauseButton.onClick.AddListener(() => network?.PauseBattle());
            if (resumeButton != null) resumeButton.onClick.AddListener(() => network?.ResumeBattle());
            if (returnLobbyButton != null) returnLobbyButton.onClick.AddListener(() => network?.ReturnAllToLobby());
            if (developerSettingsButton != null) developerSettingsButton.onClick.AddListener(OpenDeveloperSettings);
            if (resultRestartButton != null) resultRestartButton.onClick.AddListener(RestartFromResult);
            if (resultReturnLobbyButton != null) resultReturnLobbyButton.onClick.AddListener(() => network?.ReturnAllToLobby());
            if (resultLeaveButton != null) resultLeaveButton.onClick.AddListener(Leave);
        }

        private IEnumerator Start()
        {
            if (hostAddress != null && network != null) hostAddress.text = network.hostAddress;
            if (port != null && network != null) port.text = network.port.ToString();
            SetVisible(roomListPanel, false);
            SetVisible(directConnectionPanel, false);
            SetVisible(roomListRowTemplate == null ? null : roomListRowTemplate.gameObject, false);
            // Give command-line launchers and test fixtures their own Start callback first.
            // This is one attempt, never a per-frame retry after discovery fails.
            yield return null;
            automaticRoomSearchPending = true;
            TryAutomaticRoomSearch();
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
            // 2:2 (#281): nothing passes the closed outer edge, so its neighbour badge stays hidden.
            int closedSide = battleVisible ? LabTeams.ClosedSide(snapshot, network.LocalPlayerId) : 0;
            SetVisible(leftNeighborBadge, hasNeighbors && closedSide != -1);
            SetVisible(rightNeighborBadge, hasNeighbors && closedSide != 1);
            SetVisible(pauseButton == null ? null : pauseButton.gameObject, playing);
            SetVisible(resultControls, terminal);
            if (terminal)
                Set(resultStatusText, ResultWords(snapshot));
            bool connected = network.IsConnected;
            bool joining = network.IsJoiningRoom;
            if (wasConnected && !connected && !joining)
            {
                directConnectionExpanded = false;
                automaticRoomSearchPending = true;
            }
            wasConnected = connected;
            bool showBrowser = !battleVisible && (!connected || joining);
            bool developerOpen = developerMode != null && developerMode.panel != null
                && developerMode.panel.activeInHierarchy;
            TryAutomaticRoomSearch();
            SetVisible(sessionControls, !battleVisible && connected && !joining);
            SetVisible(statusText == null ? null : statusText.gameObject, !battleVisible && connected && !joining);
            bool showPlayerSlots = connected && !joining && snapshot == null
                && !network.IsDeveloperSolo && !developerOpen;
            SetVisible(playerSlotPanel, showPlayerSlots);
            RefreshPlayerSlots(showPlayerSlots);
            // This panel is a saved Canvas object. Hide it behind DEV instead of bringing
            // it forward each frame and accidentally covering the developer form.
            SetVisible(roomListPanel, showBrowser && !developerOpen);
            SetVisible(decreasePlayersButton == null ? null : decreasePlayersButton.gameObject, showBrowser);
            SetVisible(increasePlayersButton == null ? null : increasePlayersButton.gameObject, showBrowser);
            SetVisible(hostButton == null ? null : hostButton.gameObject, showBrowser);
            SetVisible(findHostButton == null ? null : findHostButton.gameObject, false);
            SetVisible(directConnectionPanel, showBrowser && directConnectionExpanded);
            UpdateRoomListLayout();
            SetVisible(hostAddress == null ? null : hostAddress.gameObject, showBrowser && directConnectionExpanded);
            SetVisible(port == null ? null : port.gameObject, showBrowser && directConnectionExpanded);
            SetVisible(joinButton == null ? null : joinButton.gameObject, showBrowser && directConnectionExpanded);
            SetVisible(roomNameText == null ? null : roomNameText.gameObject, connected && !joining);
            if (connected && !joining)
                Set(roomNameText, network.IsHost && network.HostRoomNamePending
                    ? "REGISTERING ROOM…"
                    : string.IsNullOrEmpty(network.ConnectedRoomName)
                        ? "ROOM NAME UNAVAILABLE · HOST IP : " + network.port
                        : "ROOM " + network.ConnectedRoomName);
            Set(quickPlayerCountText, "ROOM PLAYERS " + network.TargetParticipantCount + " / 5");
            if (decreasePlayersButton != null)
                decreasePlayersButton.interactable = network.CanChooseConnection && network.TargetParticipantCount > 2;
            if (increasePlayersButton != null)
                increasePlayersButton.interactable = network.CanChooseConnection && network.TargetParticipantCount < 5;
            SetVisible(refreshRoomsButton == null ? null : refreshRoomsButton.gameObject, showBrowser && !joining);
            if (refreshRoomsButton != null) refreshRoomsButton.interactable = network.CanChooseConnection;
            SetVisible(cancelRoomSearchButton == null ? null : cancelRoomSearchButton.gameObject, showBrowser && joining);
            if (cancelRoomSearchButton != null)
                cancelRoomSearchButton.interactable = joining && !network.IsCancelingJoin;
            if (directConnectionButton != null)
            {
                directConnectionButton.interactable = !joining && network.CanChooseConnection;
                Set(directConnectionButtonLabel,
                    directConnectionExpanded ? "HIDE DIRECT IP" : "DIRECT IP");
            }
            RefreshRoomRows(showBrowser);
            if (hostButton != null) hostButton.interactable = network.CanChooseConnection;
            if (joinButton != null) joinButton.interactable = network.CanChooseConnection;
            if (hostAddress != null) hostAddress.interactable = network.CanChooseConnection;
            if (port != null) port.interactable = network.CanChooseConnection;
            if (findHostButton != null) findHostButton.interactable = network.CanChooseConnection;
            SetVisible(startButton == null ? null : startButton.gameObject, connected && network.IsHost && !joining);
            SetVisible(leaveButton == null ? null : leaveButton.gameObject, connected && !joining);
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
                Set(resultStatusText, ResultWords(snapshot) + " · WAITING FOR HOST");
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
                Set(hpText, compactPaintProgress ? "0.0%" : "—");
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
                SetPaintPatternSample(localPlayerPatternSample, 0, false);
                SetPaintPatternSample(leftPlayerPatternSample, 0, false);
                SetPaintPatternSample(rightPlayerPatternSample, 0, false);
                RefreshTeamHud(null, false);
                return;
            }
            Set(hpText, paintGoal
                ? (compactPaintProgress ? FormatPaintPercentage(snapshot) : FormatPaintGoalProgress(snapshot))
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
                ? PaintGoalFill(snapshot)
                : Fraction(snapshot.hp, network.config.MonsterMaxHp));
            SetFill(playerHpFill, local == null ? 0f : Fraction(local.personalHp, network.config.PlayerMaxHp));
            SetStaminaCells(local == null ? 0f : Fraction(local.stamina, network.config.StaminaMax));
            GetNeighborLabels(snapshot, network.LocalPlayerId, out string left, out string right);
            Set(leftNeighborText, left);
            Set(rightNeighborText, right);
            SetPlayerCircle(localPlayerCircle, local == null ? 0 : local.seat + 1);
            SetPlayerCircle(leftPlayerCircle, ParsePlayerNumber(left));
            SetPlayerCircle(rightPlayerCircle, ParsePlayerNumber(right));
            SetPaintPatternSample(localPlayerPatternSample, local == null ? 0 : local.seat + 1, battleVisible);
            SetPaintPatternSample(leftPlayerPatternSample, ParsePlayerNumber(left), hasNeighbors);
            SetPaintPatternSample(rightPlayerPatternSample, ParsePlayerNumber(right), hasNeighbors);
            RefreshTeamHud(snapshot, battleVisible);
        }

        /// <summary>A 2:2 battle (#281) ends with the winning team or a draw; every other battle keeps its words.</summary>
        private static string ResultWords(LabSnapshot snapshot) => snapshot.teamTerritory && snapshot.phase == LabPhase.Victory
            ? LabTeamBar.ResultText(snapshot) : snapshot.phase == LabPhase.Victory ? "VICTORY" : "DEFEAT";

        /// <summary>
        /// 2:2 (#281): the Blue/Red share bar takes the paint bar's place, this player's badge wears the team ring,
        /// the fever gauges sit under the bar, and a fever is announced with a banner and my team's edge glow.
        /// </summary>
        private void RefreshTeamHud(LabSnapshot snapshot, bool battleVisible)
        {
            bool territory = battleVisible && snapshot != null && snapshot.teamTerritory;
            Transform paintBar = hpText != null ? hpText.transform.parent : null;
            if (paintBar != null && battleUi != null && paintBar != battleUi.transform)
                SetVisible(paintBar.gameObject, !territory);
            if (!territory)
            {
                if (teamBar != null) teamBar.Hide();
                if (teamFever != null) teamFever.Hide();
                return;
            }
            if (battleUi == null) return;
            Font font = hpText != null ? hpText.font : null;
            if (teamBar == null) teamBar = LabTeamBar.Create((RectTransform)battleUi.transform, font);
            if (teamFever == null) teamFever = LabTeamFever.Create((RectTransform)battleUi.transform, font);
            LabTeam team = LabTeams.TeamOf(snapshot, network.LocalPlayerId);
            teamBar.Present(snapshot, team, localPlayerCircle, network.config != null ? network.config.TeamFeverHits : 0);
            teamFever.Present(snapshot, team);
        }

        /// <summary>The 2:2 fever banner and edge glow (#281), created on the first team battle.</summary>
        public LabTeamFever TeamFever => teamFever;

        /// <summary>The 2:2 share bar (#281), created on the first team battle.</summary>
        public LabTeamBar TeamBar => teamBar;

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

        /// <summary>Every paint round requires the complete surface, including old serialized fixtures.</summary>
        public static int PaintGoalPercentFor(LabSnapshot snapshot, LabConfig tuning)
            => LabConfig.DefaultPaintVictoryPercent;

        public static string GourdGoalTitle(LabGourdMode mode)
            => mode == LabGourdMode.Crack ? "YOKAI HP" : "GOAL";

        /// <summary>Floor to one decimal so an unfinished near-goal value never rounds up.</summary>
        public static string FormatPaintGoalProgress(float coverage, int goalPercent)
        {
            float displayed = DisplayCoverage(coverage);
            int tenths = Mathf.FloorToInt(displayed * 1000f);
            return FormatPaintTenths(displayed < 1f ? Mathf.Min(tenths, 999) : tenths);
        }

        /// <summary>Integer cell counts prevent float rounding from displaying an unfinished surface as 100%.</summary>
        public static string FormatPaintGoalProgress(LabSnapshot snapshot)
            => FormatPaintTenths(PaintProgressTenths(snapshot));

        public static string FormatPaintPercentage(LabSnapshot snapshot)
            => (PaintProgressTenths(snapshot) / 10m).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static int PaintProgressTenths(LabSnapshot snapshot)
        {
            if (snapshot == null) return 0;
            if (snapshot.paintCellCount <= 0)
            {
                float coverage = DisplayCoverage(snapshot.paintCoverage);
                int value = Mathf.FloorToInt(coverage * 1000f);
                return coverage < 1f ? Mathf.Min(value, 999) : value;
            }
            int covered = Mathf.Clamp(snapshot.paintCoveredCount, 0, snapshot.paintCellCount);
            return (int)((long)covered * 1000L / snapshot.paintCellCount);
        }

        private static string FormatPaintTenths(int tenths)
            => "PAINT " + (tenths / 10m).ToString("0.0", CultureInfo.InvariantCulture) + "% / 100%";

        public static float PaintGoalFill(float coverage, int goalPercent)
            => DisplayCoverage(coverage);

        public static float PaintGoalFill(LabSnapshot snapshot)
        {
            if (snapshot == null) return 0f;
            if (snapshot.paintCellCount <= 0) return DisplayCoverage(snapshot.paintCoverage);
            int covered = Mathf.Clamp(snapshot.paintCoveredCount, 0, snapshot.paintCellCount);
            if (covered == snapshot.paintCellCount) return 1f;
            return Mathf.Min((float)((double)covered / snapshot.paintCellCount), .99999994f);
        }

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

        private void SetPaintPatternSample(Image image, int number, bool visible)
        {
            if (image == null) return;
            Sprite art = LabPlayerPaintPatterns.SpriteFor(number);
            if (image.sprite != art) image.sprite = art;
            image.enabled = art != null;
            bool patternsEnabled = network.config == null || network.config.PlayerPaintPatternsEnabled;
            SetVisible(image.gameObject, visible && art != null && patternsEnabled);
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
            if (LabTeams.FourPlayerTeams(snapshot))
            {
                // 2:2 (#281): only the inner edge leads to the teammate; the outer edge is a wall.
                string mate = "P" + (LabTeams.TeammateSeat(local.seat) + 1);
                if (LabTeams.EdgeOpen(local.seat, true)) right = mate; else left = mate;
                return;
            }
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
            {
                int assigned = 0;
                for (int slot = 1; slot <= target; slot++)
                    if (network.GetSlotOwner(slot) != ulong.MaxValue) assigned++;
                string selection = network.IsDeveloperRestartPending ? "WAITING FOR SETTINGS · RESTARTING…"
                    : pending ? "WAITING FOR HOST APPROVAL…"
                    : selected > 0 ? "YOUR POSITION: P" + selected
                    : "CHOOSE AN OPEN P NUMBER BEFORE START";
                int remaining = target - assigned;
                Set(playerSlotStatusText, selection + "\nPOSITIONS " + assigned + " / " + target
                    + (remaining > 0 ? " · " + remaining + " STILL OPEN" : " · ALL POSITIONS SELECTED"));
            }

            for (int i = 0; i < playerSlotButtons.Length && i < LabModel.MaximumParticipants; i++)
            {
                Button button = playerSlotButtons[i];
                if (button == null) continue;
                int slot = i + 1;
                bool inRoom = visible && slot <= target;
                if (playerSlotPatternSamples != null && i < playerSlotPatternSamples.Length)
                    SetPaintPatternSample(playerSlotPatternSamples[i], slot, inRoom);
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
            lobbyInputError = string.Empty;
            if (network == null || !TryPort()) return;
            network.Host();
        }

        private void Join()
        {
            lobbyInputError = string.Empty;
            if (network == null || !TryPort()) return;
            network.Join(hostAddress == null ? network.hostAddress : hostAddress.text);
        }

        private void Leave()
        {
            if (network == null) return;
            directConnectionExpanded = false;
            network.Stop();
        }

        private void TryAutomaticRoomSearch()
        {
            if (!automaticRoomSearchPending || network == null) return;
            if (network.IsConnected || network.IsJoiningRoom || network.IsSearchingRooms
                || network.RoomSearchFailed)
            {
                automaticRoomSearchPending = false;
                return;
            }
            // Stop is asynchronous: wait for NGO shutdown before consuming this attempt.
            if (!network.CanChooseConnection) return;
            automaticRoomSearchPending = false;
            network.StartRoomSearch();
        }

        private void CancelConnection()
        {
            if (network != null) network.CancelJoin();
        }

        private void ToggleDirectConnection()
        {
            if (network == null || !network.CanChooseConnection) return;
            directConnectionExpanded = !directConnectionExpanded;
        }

        private void UpdateRoomListLayout()
        {
            if (roomListScrollView == null || directConnectionButton == null
                || directConnectionPanel == null) return;
            RectTransform panelRect = directConnectionPanel.transform as RectTransform;
            RectTransform buttonRect = directConnectionButton.transform as RectTransform;
            if (panelRect == null || buttonRect == null) return;
            float bottom = (directConnectionExpanded ? panelRect.offsetMax.y : buttonRect.offsetMax.y) + 10f;
            Vector2 offset = roomListScrollView.offsetMin;
            if (Mathf.Approximately(offset.y, bottom)) return;
            offset.y = bottom;
            roomListScrollView.offsetMin = offset;
        }

        private void FindHost()
        {
            lobbyInputError = string.Empty;
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
            lobbyInputError = string.Empty;
            if (network == null || !network.CanChooseConnection) return;
            network.StartRoomSearch();
        }

        private void RefreshRoomRows(bool visible)
        {
            if (!visible || roomListContent == null || roomListRowTemplate == null)
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
                    row.onClick.AddListener(() => JoinRoom(key));
                    Text label = row.GetComponentInChildren<Text>(true);
                    Set(label, room.Name);
                    roomRows.Add(row);
                    roomRowLabels.Add(label);
                    roomRowKeys.Add(key);
                }
                renderedRoomList = current;
            }
            for (int i = 0; i < roomRows.Count; i++)
            {
                bool selected = network.IsJoiningRoom && string.Equals(roomRowKeys[i],
                    network.JoiningRoomKey, StringComparison.OrdinalIgnoreCase);
                roomRows[i].interactable = network.CanJoinDiscoveredRoom(roomRowKeys[i]);
                Set(roomRowLabels[i], rooms[i].Name + (selected ? "\nCONNECTING…" : string.Empty));
            }
            Set(roomSearchStatusText, !string.IsNullOrEmpty(lobbyInputError) ? lobbyInputError
                : network.IsJoiningRoom
                ? network.IsCancelingJoin ? "Canceling connection…"
                    : "Connecting… " + Mathf.FloorToInt(network.JoinElapsedSeconds) + "s"
                : network.RoomSearchMessage);
        }

        private void JoinRoom(string key)
        {
            lobbyInputError = string.Empty;
            if (network != null) network.JoinDiscoveredRoom(key);
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
            roomRowLabels.Clear();
            roomRowKeys.Clear();
            renderedRoomList = string.Empty;
        }

        private bool TryPort()
        {
            if (port == null || !ushort.TryParse(port.text, out var parsed) || parsed == 0)
            {
                lobbyInputError = "Enter port 1–65535 in DIRECT IP.";
                if (noticeText != null) noticeText.text = lobbyInputError;
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
