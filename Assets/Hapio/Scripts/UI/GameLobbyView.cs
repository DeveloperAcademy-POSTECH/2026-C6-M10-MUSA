using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Saved Figma presentation for the persistent game session. Owns no transport or advertisements.</summary>
    public sealed class GameLobbyView : MonoBehaviour
    {
        public GameScreen screen;
        public RectTransform design;
        public UnityEngine.UI.RawImage background;
        public GameObject titlePage, browserPage, positionPage, emptyRooms;
        public UnityEngine.UI.Button enterButton, createButton, refreshButton, cancelButton, leaveButton, sealButton;
        public UnityEngine.UI.Button clearSlotButton, directConnectionButton, directJoinButton, closeDirectConnectionButton;
        public UnityEngine.UI.Button developerButton, soloButton;
        public GameObject directConnectionPanel;
        public UnityEngine.UI.InputField directAddress, directPort;
        public UnityEngine.UI.Text directFeedback;
        public UnityEngine.UI.Button[] countButtons, slotButtons;
        public UnityEngine.UI.Text heading, subheading, roomName, joinedCount, selectedCount, message;
        public UnityEngine.UI.Text[] slotLabels, localLabels;
        public UnityEngine.UI.Image[] slotImages;
        public UnityEngine.UI.Image connectionLines;
        public Sprite[] emptySlots, filledSlots, lines;
        public RectTransform roomContent;
        public UnityEngine.UI.Button roomTemplate;
        public Font mediumFont;
        [Header("Editable seat layout anchors, one array for each room capacity")]
        public RectTransform[] seats2, seats3, seats4, seats5;
        public RectTransform[] lineLayouts;
        [Header("2:2 team territory (#281), added by Hapio > Upgrade 2v2 Lobby UI")]
        public GameObject modePanel;
        public UnityEngine.UI.Button modeDismissButton, basicModeButton, teamModeButton, teamSoloButton;
        public GameObject teamLayout;
        [Tooltip("Editable P1..P4 anchors of the 2:2 seat screen: Blue P1/P2 on the left, Red P3/P4 on the right.")]
        public RectTransform[] seatsTeam;
        [Tooltip("Heading, hint, room paper, room name, joined, selected and start button. In a 2:2 room each moves to the matching anchor below.")]
        public RectTransform[] teamShifted, teamShiftAnchors;
        public int teamHeadingFontSize = 29, teamLocalLabelFontSize = 20, teamStartFontSize = 25;
        [Tooltip("2:2 빈 자리 점선 동그라미 (#293): 청팀 P1·P2는 파란 점선, 홍팀 P3·P4는 빨간 점선. 비어 있으면 스토리와 같은 자리별 그림을 쓴다.")]
        public Sprite blueTeamEmptySlot, redTeamEmptySlot;
        [Header("땅따먹기 1:1·3:3 (#394), added by Hapio > Upgrade 2v2 Lobby UI")]
        [Tooltip("고른 인원으로 열 수 없는 모드 버튼 바로 아래에 보이는 이유 한 줄: 기본 모드 아래, 땅따먹기 아래.")]
        public UnityEngine.UI.Text basicModeReason, teamModeReason;
        [Tooltip("땅따먹기 찬 자리 동그라미: 청팀은 파랑, 홍팀은 빨강. 비어 있으면 자리별 그림을 쓴다.")]
        public Sprite blueTeamFilledSlot, redTeamFilledSlot;
        [Tooltip("1:1 자리 화면의 P1(청)·P2(홍) 자리.")]
        public RectTransform[] seatsOneOnOne;
        [Tooltip("3:3 자리 화면의 P1~P6 자리. 청팀 쪽에 P1~P3, 홍팀 쪽에 P4~P6이 같은 모양으로 앉는다: P1·P4 아래 가운데, P2·P5 왼쪽 위, P3·P6 오른쪽 위.")]
        public RectTransform[] seatsThreeOnThree;
        [Tooltip("1:1·2:2에서 보이는 두 팀 상자와 팀 이름, 그리고 2:2에서만 보이는 팀 안 연결선.")]
        public GameObject[] teamPairParts, teamPairLines;
        [Tooltip("3:3에서만 보이는 한 팀 쪽: 상자, 삼각형 선, 팀 이름, 쪽 넘김 화살표와 쪽 표시 점.")]
        public GameObject teamTrio;
        public UnityEngine.UI.Image teamTrioBox;
        public UnityEngine.UI.Text teamTrioName;
        public UnityEngine.UI.Button teamPreviousPage, teamNextPage;
        public UnityEngine.UI.Graphic[] teamPageDots;
        public GameLobbySwipe teamSwipe;
        public Sprite blueTrioBox, redTrioBox;
        [HideInInspector] public int teamLobbyDesignRevision;

        private GameSession session;
        private GameOpeningController openingController;
        private RectTransform[] storyShifted;
        private int storyHeadingFontSize, storySealFontSize;
        private int[] storyLocalLabelFontSizes;
        // 기본 모드(요괴 봉인)를 열 수 있는 가장 적은 인원 (#394). 2명은 땅따먹기 1:1만 연다.
        private const int StoryMinimumPlayers = 3;
        private int creationCapacity = 3;
        // 3:3 (#394): the team page on screen, 0 = Blue (P1~P3), 1 = Red (P4~P6); it follows the local seat when that changes.
        private int teamPage, pageSeat = -1;
        private string localError = string.Empty;
        private readonly List<UnityEngine.UI.Button> rows = new List<UnityEngine.UI.Button>();
        private readonly List<string> keys = new List<string>();
        private LabNetwork Network => session != null ? session.Network : null;
        public int CreationCapacity => creationCapacity;
        public int TeamPage => teamPage;
        public bool OpeningBlocked { get; private set; }

        internal void RegisterOpening(GameOpeningController controller) => openingController = controller;
        internal void UnregisterOpening(GameOpeningController controller)
        {
            if (openingController == controller) openingController = null;
        }

        private void EnterGame()
        {
            if (session.State.phase != GameFlowPhase.Home || session.RoomBrowserVisible) return;
            if (openingController != null && openingController.TryBeginFromStartButton(session.EnterRoomBrowser)) return;
            session.EnterRoomBrowser();
        }

        /// <summary>Temporarily hide and lock Home while its independent opening is presented.</summary>
        public void SetOpeningBlocked(bool blocked)
        {
            OpeningBlocked = blocked;
            if (blocked)
            {
                if (background != null) background.gameObject.SetActive(false);
                if (design != null) design.gameObject.SetActive(false);
                if (directConnectionPanel != null) directConnectionPanel.SetActive(false);
                if (modePanel != null) modePanel.SetActive(false);
                if (screen != null && screen.developerPanel != null) screen.developerPanel.SetActive(false);
            }
            else if (isActiveAndEnabled) LateUpdate();
        }

        private void BindNavigation(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(() => { if (!OpeningBlocked) action(); });
        }

        private sealed class RoomRow
        {
            public string key, name;
            public ushort port;
            public int participants, capacity;
            public bool pending, ready, waiting, playing, full, team;
        }

        private void Awake()
        {
            session = GameSession.EnsureExists();
            if (screen == null) screen = GetComponentInParent<GameScreen>();
            if (screen == null || !screen.externalNavigation)
                throw new InvalidOperationException("The saved game lobby needs its external-navigation GameScreen.");
            storyHeadingFontSize = heading.fontSize;
            storySealFontSize = sealButton.GetComponentInChildren<UnityEngine.UI.Text>().fontSize;
            storyLocalLabelFontSizes = localLabels.Select(label => label.fontSize).ToArray();
            BindNavigation(enterButton, EnterGame);
            // 2:2 (#281): "새 방 만들기 +" first asks which mode. A prefab without the mode panel keeps the old story flow.
            BindNavigation(createButton, () =>
            {
                if (creationCapacity < 2) { localError = "먼저 함께할 인원을 고르시오."; return; }
                if (modePanel != null) SetModes(true); else CreateRoom(GameMode.Story);
            });
            if (modePanel != null)
            {
                BindNavigation(modeDismissButton, () => SetModes(false));
                BindNavigation(basicModeButton, () => CreateRoom(GameMode.Story));
                BindNavigation(teamModeButton, () => CreateRoom(GameMode.Territory));
                BindNavigation(teamSoloButton, () => { SetModes(false); session.StartSolo(GameMode.Territory); });
                modePanel.SetActive(false);
            }
            if (teamShifted != null && teamShiftAnchors != null && teamShifted.Length == teamShiftAnchors.Length)
            {
                // Keep each story position in a hidden copy, so leaving a 2:2 room restores the authored layout.
                storyShifted = new RectTransform[teamShifted.Length];
                for (int i = 0; i < teamShifted.Length; i++)
                {
                    var copy = new GameObject(teamShifted[i].name + " Story Position", typeof(RectTransform));
                    copy.transform.SetParent(teamShifted[i].parent, false);
                    copy.SetActive(false);
                    storyShifted[i] = (RectTransform)copy.transform;
                    CopyRect(teamShifted[i], storyShifted[i]);
                }
            }
            BindNavigation(refreshButton, () => Network.StartRoomSearch());
            BindNavigation(cancelButton, () =>
            {
                if (!session.CancelRoomCreation()) Network.CancelJoin();
                Network.CancelRoomSearch();
            });
            BindNavigation(leaveButton, () => session.GoHomeToRoomBrowser());
            BindNavigation(sealButton, () => session.StartGame());
            BindNavigation(clearSlotButton, () => session.SelectSlot(0));
            BindNavigation(directConnectionButton, () => SetDirectConnection(true));
            BindNavigation(closeDirectConnectionButton, () => SetDirectConnection(false));
            BindNavigation(directJoinButton, JoinDirect);
            BindNavigation(soloButton, () => session.StartSolo());
            // GameScreen owns the existing scoped DEV panel and wires developerButton once.
            for (int i = 0; i < countButtons.Length; i++)
            {
                int count = i + 2;
                BindNavigation(countButtons[i], () =>
                {
                    if (session.IsCreatingRoom || !Network.CanChooseConnection || session.State.phase != GameFlowPhase.Home) return;
                    creationCapacity = count; localError = string.Empty;
                    Network.SetTargetParticipantCount(count);
                });
            }
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int number = i + 1;
                BindNavigation(slotButtons[i], () => session.SelectSlot(Network.LocalSlotNumber == number ? 0 : number));
            }
            if (emptySlots[0] == filledSlots[0]) emptySlots[0] = emptySlots[1];
            BindNavigation(teamPreviousPage, () => teamPage = 0);
            BindNavigation(teamNextPage, () => teamPage = 1);
            if (teamSwipe != null) teamSwipe.Swiped += next => { if (!OpeningBlocked) teamPage = next ? 1 : 0; };
            directConnectionPanel.SetActive(false);
        }

        /// <summary>고른 인원으로 이 모드를 열 수 있는지 (#394): 기본 모드는 3~5명, 땅따먹기는 2·4·6명.</summary>
        public static bool ModeAllowed(GameMode mode, int players) => mode == GameMode.Territory
            ? LabTeams.IsTeamCount(players) : players >= StoryMinimumPlayers && players <= LabModel.MaximumParticipants;

        /// <summary>막힌 모드 버튼 아래에 보이는 이유 한 줄. 열 수 있으면 빈 문자열이다.</summary>
        public static string ModeBlockedReason(GameMode mode, int players)
        {
            if (ModeAllowed(mode, players)) return string.Empty;
            if (mode == GameMode.Territory) return "땅따먹기는 2·4·6명만 할 수 있소.";
            return LabTeams.IsTeamCount(players)
                ? players + "명은 땅따먹기 " + LabTeams.MatchLabel(players) + "만 할 수 있소." : "기본 모드는 3~5명만 할 수 있소.";
        }

        private void CreateRoom(GameMode mode)
        {
            if (OpeningBlocked) return;
            // A blocked mode button is not interactable and already shows why; an old prefab may still reach here.
            if (!ModeAllowed(mode, creationCapacity)) { localError = ModeBlockedReason(mode, creationCapacity); return; }
            SetModes(false);
            localError = string.Empty;
            if (session.CreateAndEnterRoom(creationCapacity, mode)) creationCapacity = 0;
        }

        private void RefreshModes()
        {
            ModeButton(basicModeButton, basicModeReason, GameMode.Story);
            ModeButton(teamModeButton, teamModeReason, GameMode.Territory);
        }

        private void ModeButton(UnityEngine.UI.Button button, UnityEngine.UI.Text reason, GameMode mode)
        {
            bool allowed = ModeAllowed(mode, creationCapacity);
            button.interactable = allowed;
            button.targetGraphic.color = ColorOf(allowed ? "3F3D38" : "9D9586");
            if (reason == null) return;
            reason.text = ModeBlockedReason(mode, creationCapacity);
            reason.gameObject.SetActive(!allowed);
        }

        private void SetModes(bool visible)
        {
            if (modePanel == null) return;
            if (visible && (session.IsCreatingRoom || session.State.phase != GameFlowPhase.Home || !Network.CanChooseConnection)) return;
            modePanel.SetActive(visible);
            if (!visible) return;
            modePanel.transform.SetAsLastSibling();
            RefreshModes();
            teamSoloButton.gameObject.SetActive(Network.DeveloperBuildAvailable);
        }

        private void EnterRoom(int index)
        {
            if (OpeningBlocked || session.IsCreatingRoom || index < 0 || index >= keys.Count) return;
            localError = string.Empty;
            string key = keys[index];
            if (session.PendingRooms.Any(room => room.Key == key)) session.EnterPendingRoom(key);
            else session.JoinRoom(key);
        }

        private void LateUpdate()
        {
            if (OpeningBlocked) return;
            if (session == null || Network == null) return;
            bool home = screen.kind == GameScreenKind.Home && session.State.phase == GameFlowPhase.Home;
            bool lobby = screen.kind == GameScreenKind.Lobby && session.State.phase == GameFlowPhase.Lobby;
            bool developerOpen = screen.developerPanel != null && screen.developerPanel.activeInHierarchy;
            bool visible = (home || lobby) && !developerOpen;
            background.gameObject.SetActive(visible); design.gameObject.SetActive(visible);
            if (!visible) return;
            FitScreen();
            bool browser = home && session.RoomBrowserVisible;
            titlePage.SetActive(home && !browser);
            browserPage.SetActive(browser); positionPage.SetActive(lobby);
            background.uvRect = LabLobbyView.BackgroundUv(home && !browser, background.rectTransform.rect.size);
            soloButton.gameObject.SetActive(home && Network.DeveloperBuildAvailable);
            soloButton.interactable = home && Network.CanChooseConnection && !session.IsCreatingRoom;
            developerButton.interactable = !session.IsCreatingRoom
                && (Network.CanChooseConnection || (Network.IsHost && Network.Snapshot == null));
            if (Network.IsConnected || Network.IsJoiningRoom || session.IsCreatingRoom) SetDirectConnection(false);
            if (modePanel != null && modePanel.activeSelf && (!browser || !Network.CanChooseConnection || session.IsCreatingRoom)) SetModes(false);
            if (browser) RefreshBrowser();
            else if (lobby) RefreshPositions();
            string notice = Network.IsJoiningRoom ? (Network.IsCancelingJoin ? "연결을 취소하고 있소..." : "방에 입장하고 있소...")
                : !string.IsNullOrEmpty(localError) ? localError
                : !string.IsNullOrEmpty(session.UiNotice) ? session.UiNotice
                : browser && !string.IsNullOrEmpty(Network.RoomSearchMessage) ? Network.RoomSearchMessage : Network.Notice;
            message.text = notice;
            message.gameObject.SetActive((browser || lobby) && !string.IsNullOrEmpty(notice));
        }

        private void FitScreen()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float factor = canvas != null ? canvas.scaleFactor : 1f;
            // The authored design already includes device insets; fit it to the full screen once.
            design.localScale = Vector3.one * Mathf.Min(Screen.width / factor / 402f, Screen.height / factor / 874f);
            design.anchoredPosition = Vector2.zero;
        }

        private void RefreshBrowser()
        {
            bool canChoose = Network.CanChooseConnection && !session.IsCreatingRoom;
            createButton.interactable = canChoose && (modePanel != null || creationCapacity >= 2);
            directConnectionButton.interactable = canChoose;
            refreshButton.interactable = canChoose;
            refreshButton.gameObject.SetActive(!Network.IsJoiningRoom && !session.IsCreatingRoom);
            cancelButton.gameObject.SetActive(Network.IsJoiningRoom || session.IsCreatingRoom);
            cancelButton.interactable = !Network.IsCancelingJoin;
            for (int i = 0; i < countButtons.Length; i++)
            {
                bool selected = creationCapacity == i + 2;
                countButtons[i].interactable = canChoose;
                countButtons[i].targetGraphic.color = ColorOf(selected ? "3F3D38" : "E8DFCB");
                countButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().color = ColorOf(selected ? "F3EBDD" : "3F3D38");
            }
            RoomRow[] rooms = session.PendingRooms.Select(room => new RoomRow
                { key = room.Key, name = room.Name, port = room.Port, participants = 1, capacity = room.Capacity,
                    pending = true, ready = room.Ready, waiting = true, team = room.Mode == GameMode.Territory })
                .Concat(Network.DiscoveredRooms.Select(room => new RoomRow
                { key = room.Key, name = room.Name, port = room.Port, participants = room.Participants,
                    capacity = room.TargetParticipants, waiting = room.WaitingForHost, playing = room.Playing, full = room.IsFull,
                    team = room.Team }))
                .GroupBy(room => room.key, StringComparer.OrdinalIgnoreCase).Select(group => group.First())
                .OrderBy(room => LabRoomNames.SortNumber(room.name)).ThenBy(room => room.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(room => room.port).ToArray();
            emptyRooms.SetActive(rooms.Length == 0);
            while (rows.Count < rooms.Length)
            {
                UnityEngine.UI.Button row = Instantiate(roomTemplate, roomContent);
                int index = rows.Count;
                BindNavigation(row, () => EnterRoom(index));
                rows.Add(row); keys.Add(string.Empty);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < rooms.Length; rows[i].gameObject.SetActive(active);
                if (!active) continue;
                RoomRow room = rooms[i]; keys[i] = room.key;
                rows[i].name = room.pending ? "CreatedRoom" : "DiscoveredRoom";
                rows[i].targetGraphic.color = ColorOf(i % 2 == 0 ? "E9DFCA" : "EEE6D6");
                rows[i].transform.Find("Name").GetComponent<UnityEngine.UI.Text>().text = room.pending && string.IsNullOrEmpty(room.name)
                    ? "새 방 준비 중" : LabRoomNames.Display(room.name);
                rows[i].transform.Find("Count").GetComponent<UnityEngine.UI.Text>().text = (room.team ? TeamRoomLabel(room.capacity) : "")
                    + (room.capacity >= 2 ? room.participants + " / " + room.capacity + "명 모였소" : "인원 확인 중");
                bool connecting = Network.IsJoiningRoom && Network.JoiningRoomKey == room.key;
                string action = room.pending && !room.ready ? "준비 중..." : connecting ? "입장 중..."
                    : !room.pending && room.waiting ? "방장 대기" : room.playing ? "봉인 중" : room.full ? "정원 마감" : "입장 →";
                var label = rows[i].transform.Find("Action").GetComponent<UnityEngine.UI.Text>();
                label.text = action; label.fontSize = action == "입장 →" ? 20 : 14;
                rows[i].interactable = canChoose && (room.pending ? room.ready : Network.CanJoinDiscoveredRoom(room.key));
            }
            roomTemplate.gameObject.SetActive(false);
        }

        /// <summary>방 카드의 모드 표기: "1:1 땅따먹기 · ", "2:2 땅따먹기 · ", "3:3 땅따먹기 · " (#394).</summary>
        public static string TeamRoomLabel(int capacity)
        {
            string match = LabTeams.MatchLabel(capacity);
            return (match.Length > 0 ? match + " " : string.Empty) + "땅따먹기 · ";
        }

        /// <summary>땅따먹기 (#293, #394): a seat's dashed (empty) or filled circle in its team's colour, or the seat's own when none is saved.</summary>
        private Sprite TeamSlot(int seat, bool occupied)
        {
            bool blue = LabTeams.SeatTeam(seat, Network.TargetParticipantCount) == LabTeam.Blue;
            Sprite team = occupied ? blue ? blueTeamFilledSlot : redTeamFilledSlot : blue ? blueTeamEmptySlot : redTeamEmptySlot;
            if (team != null) return team;
            Sprite[] own = occupied ? filledSlots : emptySlots;
            return own[Mathf.Min(seat, own.Length - 1)];
        }

        /// <summary>The seat anchors of this territory size, or null when the prefab has none for it.</summary>
        private RectTransform[] TeamAnchors(int size)
        {
            RectTransform[] anchors = size == 1 ? seatsOneOnOne : size == 2 ? seatsTeam : size == 3 ? seatsThreeOnThree : null;
            return anchors != null && anchors.Length == size * 2 && slotButtons.Length >= size * 2 && anchors.All(anchor => anchor != null)
                && (size != 3 || teamTrio != null) ? anchors : null;
        }

        /// <summary>
        /// Whether this room is a territory room (#394). The session knows it from the room card or the Host's flow state;
        /// before that reaches a guest (a direct-IP join or one still waiting for approval) the room itself tells: six
        /// players only exist in 3:3, and the Host's confirmed settings carry the territory switch.
        /// </summary>
        private bool TerritoryRoom(int count) => session.IsTerritory || count > LabModel.MaximumParticipants
            || !Network.IsHost && Network.SessionConfigReady && Network.config != null && Network.config.TeamTerritoryEnabled;

        /// <summary>The seat screen's small line while seats are being chosen (#394).</summary>
        public static string TeamSeatHint(int players) => LabTeams.TeamSize(players) == 1 ? "원하는 자리를 선택 후, 준비하시오."
            : LabTeams.TeamSize(players) == 3 ? "3명씩 함께 앉아, 준비하시오." : "실제로 앉은 위치의 빈 자리를 눌러주시오.";

        private void RefreshTeamPage(int count, int size)
        {
            if (size != 3) { teamPage = 0; pageSeat = -1; return; }
            int mine = Network.LocalSlotNumber;
            if (mine != pageSeat)
            {
                // A newly chosen seat brings its own team's page; clearing it keeps the page being looked at.
                if (mine > 0) teamPage = LabTeams.SeatTeam(mine - 1, count) == LabTeam.Red ? 1 : 0;
                pageSeat = mine;
            }
            bool red = teamPage == 1;
            if (blueTrioBox != null && redTrioBox != null) teamTrioBox.sprite = red ? redTrioBox : blueTrioBox;
            teamTrioName.text = red ? "홍팀" : "청팀";
            teamTrioName.color = ColorOf(red ? "CF4A3E" : "2F62A2");
            teamPreviousPage.gameObject.SetActive(red);
            teamNextPage.gameObject.SetActive(!red);
            for (int i = 0; i < teamPageDots.Length; i++) teamPageDots[i].color = ColorOf(i == teamPage ? "3F3D38" : "C7C0B0");
        }

        private void RefreshPositions()
        {
            int count = Network.TargetParticipantCount;
            bool allChosen = Network.ParticipantCount == count && Network.SelectedParticipantCount == count;
            // 땅따먹기 (#281, #394): 1:1 and 2:2 show both team boxes, 3:3 one team's page at a time; the saved anchors
            // move the shared texts down under the mode title.
            int size = TerritoryRoom(count) && teamLayout != null ? LabTeams.TeamSize(count) : 0;
            RectTransform[] teamAnchors = size > 0 ? TeamAnchors(size) : null;
            bool team = teamAnchors != null;
            if (!team) size = 0;
            teamLayout?.SetActive(team);
            foreach (GameObject part in teamPairParts ?? Array.Empty<GameObject>()) part.SetActive(size == 1 || size == 2);
            foreach (GameObject line in teamPairLines ?? Array.Empty<GameObject>()) line.SetActive(size == 2);
            teamTrio?.SetActive(size == 3);
            RefreshTeamPage(count, size);
            if (storyShifted != null)
                for (int i = 0; i < teamShifted.Length; i++) CopyRect(team ? teamShiftAnchors[i] : storyShifted[i], teamShifted[i]);
            heading.fontSize = team ? teamHeadingFontSize : storyHeadingFontSize;
            heading.text = allChosen ? (team ? "준비 완료!" : "요괴 봉인 준비 완료!") : "빈 자리를 고르시오.";
            subheading.text = Network.SlotSelectionPending ? "자리 선택을 확인하고 있소..."
                : allChosen ? (Network.SessionConfigReady ? "" : "방 설정을 확인하고 있소...")
                : team ? TeamSeatHint(count) : "실제로 앉은 위치의 빈 자리를 눌러주시오.";
            roomName.text = Network.HostRoomNamePending ? "방을 만들고 있소..." : LabRoomNames.Display(Network.ConnectedRoomName);
            joinedCount.text = Network.ParticipantCount + " / " + count + "명 입장";
            selectedCount.text = Network.SelectedParticipantCount + " / " + count + "명 자리 확정";
            // The story layouts stop at five; a larger room is always territory, so never index past them.
            int storyLines = Mathf.Min(count - 3, Mathf.Min(lines.Length, lineLayouts.Length) - 1);
            connectionLines.gameObject.SetActive(storyLines >= 0 && !team);
            if (storyLines >= 0 && !team)
            {
                connectionLines.sprite = lines[storyLines];
                CopyRect(lineLayouts[storyLines], connectionLines.rectTransform);
            }
            RectTransform[] anchors = team ? teamAnchors : count == 2 ? seats2 : count == 3 ? seats3 : count == 4 ? seats4 : seats5;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                // 3:3 shows only the seats of the team page on screen.
                bool active = i < count && i < anchors.Length
                    && (size != 3 || (LabTeams.SeatTeam(i, count) == LabTeam.Red) == (teamPage == 1));
                slotButtons[i].gameObject.SetActive(active); localLabels[i].gameObject.SetActive(active);
                if (!active) continue;
                CopyRect(anchors[i], (RectTransform)slotButtons[i].transform);
                ulong owner = Network.GetSlotOwner(i + 1);
                bool occupied = owner != ulong.MaxValue, mine = occupied && owner == Network.LocalPlayerId;
                slotImages[i].sprite = team ? TeamSlot(i, occupied) : occupied ? filledSlots[i] : emptySlots[i];
                slotImages[i].rectTransform.localEulerAngles = new Vector3(0, 0, 180);
                slotLabels[i].text = "P" + (i + 1);
                slotLabels[i].color = ColorOf(occupied ? "F6F6F6" : "3F3D38");
                slotButtons[i].interactable = Network.CanSelectPlayerSlot && (!occupied || mine);
                bool host = occupied && owner == Unity.Netcode.NetworkManager.ServerClientId;
                localLabels[i].fontSize = team ? teamLocalLabelFontSize
                    : storyLocalLabelFontSizes[Mathf.Min(i, storyLocalLabelFontSizes.Length - 1)];
                localLabels[i].text = (host ? "방장" : string.Empty) + (mine ? (host ? " · 나 · 취소" : "나 · 취소") : string.Empty);
                Vector2 center = anchors[i].anchoredPosition;
                // Labels stay clear of the team lines: above the upper seats (2:2 P1·P3, 3:3 P2·P3·P5·P6) and both 1:1
                // seats, below the lower ones (2:2 P2·P4, 3:3 P1·P4).
                bool above = size == 1 || size == 2 && i % 2 == 0 || size == 3 && i % 3 != 0;
                LabLobbyView.Place(localLabels[i].rectTransform, center.x - 60f, -center.y + (above ? -67f : 42f), 120f, 25f);
            }
            clearSlotButton.gameObject.SetActive(!team);
            clearSlotButton.interactable = Network.CanSelectPlayerSlot && Network.LocalSlotNumber != 0;
            bool start = Network.IsHost && Network.CanStartBattle;
            sealButton.interactable = start;
            sealButton.targetGraphic.color = ColorOf(start ? "AF4738" : "C7C0B0");
            var text = sealButton.GetComponentInChildren<UnityEngine.UI.Text>();
            text.fontSize = team && start ? teamStartFontSize : storySealFontSize;
            text.text = team
                ? start ? "땅따먹기 시작" : allChosen && !Network.IsHost ? "방장의 시작을 기다리시오" : "땅따먹기 대기"
                : start ? "요괴 봉인 시작 · 3스테이지" : allChosen && !Network.IsHost ? "방장의 봉인을 기다리시오" : "요괴 봉인 대기";
            text.color = ColorOf(start ? "FFF7E5" : "575750");
        }

        private void SetDirectConnection(bool visible)
        {
            if (visible && (session.IsCreatingRoom || session.State.phase != GameFlowPhase.Home || !Network.CanChooseConnection)) return;
            directConnectionPanel.SetActive(visible);
            if (!visible) return;
            directConnectionPanel.transform.SetAsLastSibling();
            directAddress.SetTextWithoutNotify(Network.hostAddress);
            directPort.SetTextWithoutNotify(Network.port.ToString(CultureInfo.InvariantCulture));
            directFeedback.text = "방장의 IP 주소와 포트를 입력하시오. 같은 Wi-Fi 또는 핫스팟에 연결해야 하오.";
        }

        private void JoinDirect()
        {
            if (session.IsCreatingRoom || session.State.phase != GameFlowPhase.Home || !Network.CanChooseConnection) return;
            string address = directAddress.text.Trim();
            if (string.IsNullOrEmpty(address) || !ushort.TryParse(directPort.text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out ushort port) || port == 0)
            { directFeedback.text = "IP 주소와 1~65535 사이의 포트를 확인하시오."; return; }
            localError = string.Empty; Network.port = port;
            session.JoinDirect(address);
            if (!Network.IsJoiningRoom && !Network.IsConnected) directFeedback.text = Network.RoomSearchMessage;
        }

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin; destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot; destination.sizeDelta = source.sizeDelta;
            destination.anchoredPosition = source.anchoredPosition; destination.localRotation = source.localRotation;
        }
        private static Color ColorOf(string html) => LabLobbyView.Html(html);
    }
}
