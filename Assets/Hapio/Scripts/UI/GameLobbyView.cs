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

        private GameSession session;
        private int creationCapacity = 3;
        private string localError = string.Empty;
        private readonly List<UnityEngine.UI.Button> rows = new List<UnityEngine.UI.Button>();
        private readonly List<string> keys = new List<string>();
        private LabNetwork Network => session != null ? session.Network : null;
        public int CreationCapacity => creationCapacity;

        private sealed class RoomRow
        {
            public string key, name;
            public ushort port;
            public int participants, capacity;
            public bool pending, ready, waiting, playing, full;
        }

        private void Awake()
        {
            session = GameSession.EnsureExists();
            if (screen == null) screen = GetComponentInParent<GameScreen>();
            if (screen == null || !screen.externalNavigation)
                throw new InvalidOperationException("The saved game lobby needs its external-navigation GameScreen.");
            enterButton.onClick.AddListener(() => session.EnterRoomBrowser());
            createButton.onClick.AddListener(CreateRoom);
            refreshButton.onClick.AddListener(() => Network.StartRoomSearch());
            cancelButton.onClick.AddListener(() => { Network.CancelJoin(); Network.CancelRoomSearch(); });
            leaveButton.onClick.AddListener(() => session.GoHomeToRoomBrowser());
            sealButton.onClick.AddListener(() => session.StartGame());
            clearSlotButton.onClick.AddListener(() => session.SelectSlot(0));
            directConnectionButton.onClick.AddListener(() => SetDirectConnection(true));
            closeDirectConnectionButton.onClick.AddListener(() => SetDirectConnection(false));
            directJoinButton.onClick.AddListener(JoinDirect);
            soloButton.onClick.AddListener(() => session.StartSolo());
            // GameScreen owns the existing scoped DEV panel and wires developerButton once.
            for (int i = 0; i < countButtons.Length; i++)
            {
                int count = i + 2;
                countButtons[i].onClick.AddListener(() =>
                {
                    if (!Network.CanChooseConnection || session.State.phase != GameFlowPhase.Home) return;
                    creationCapacity = count; localError = string.Empty;
                    Network.SetTargetParticipantCount(count);
                });
            }
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int number = i + 1;
                slotButtons[i].onClick.AddListener(() => session.SelectSlot(Network.LocalSlotNumber == number ? 0 : number));
            }
            if (emptySlots[0] == filledSlots[0]) emptySlots[0] = emptySlots[1];
            directConnectionPanel.SetActive(false);
        }

        private void CreateRoom()
        {
            if (creationCapacity < 2 || creationCapacity > 5) return;
            localError = string.Empty;
            if (session.CreatePendingRoom(creationCapacity)) creationCapacity = 0;
        }

        private void EnterRoom(int index)
        {
            if (index < 0 || index >= keys.Count) return;
            localError = string.Empty;
            string key = keys[index];
            if (session.PendingRooms.Any(room => room.Key == key)) session.EnterPendingRoom(key);
            else session.JoinRoom(key);
        }

        private void LateUpdate()
        {
            if (session == null || Network == null) return;
            bool home = screen.kind == GameScreenKind.Home && session.State.phase == GameFlowPhase.Home;
            bool lobby = screen.kind == GameScreenKind.Lobby && session.State.phase == GameFlowPhase.Lobby;
            bool developerOpen = screen.developerPanel != null && screen.developerPanel.activeInHierarchy;
            bool visible = (home || lobby) && !developerOpen;
            background.gameObject.SetActive(visible); design.gameObject.SetActive(visible);
            if (!visible) return;
            FitSafeArea();
            bool browser = home && session.RoomBrowserVisible;
            titlePage.SetActive(home && !browser);
            browserPage.SetActive(browser); positionPage.SetActive(lobby);
            background.uvRect = LabLobbyView.BackgroundUv(home && !browser, background.rectTransform.rect.size);
            soloButton.gameObject.SetActive(home && Network.DeveloperBuildAvailable);
            soloButton.interactable = home && Network.CanChooseConnection;
            if (Network.IsConnected || Network.IsJoiningRoom) SetDirectConnection(false);
            if (browser) RefreshBrowser();
            else if (lobby) RefreshPositions();
            string notice = Network.IsJoiningRoom ? (Network.IsCancelingJoin ? "연결을 취소하고 있소..." : "방에 입장하고 있소...")
                : !string.IsNullOrEmpty(localError) ? localError
                : !string.IsNullOrEmpty(session.UiNotice) ? session.UiNotice
                : browser && !string.IsNullOrEmpty(Network.RoomSearchMessage) ? Network.RoomSearchMessage : Network.Notice;
            message.text = notice;
            message.gameObject.SetActive((browser || lobby) && !string.IsNullOrEmpty(notice));
        }

        private void FitSafeArea()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float factor = canvas != null ? canvas.scaleFactor : 1f;
            Rect safe = Screen.safeArea;
            design.localScale = Vector3.one * Mathf.Min(safe.width / factor / 402f, safe.height / factor / 874f);
            design.anchoredPosition = (safe.center - new Vector2(Screen.width, Screen.height) * .5f) / factor;
        }

        private void RefreshBrowser()
        {
            bool canChoose = Network.CanChooseConnection;
            createButton.interactable = canChoose && creationCapacity >= 2;
            directConnectionButton.interactable = canChoose;
            refreshButton.interactable = canChoose;
            refreshButton.gameObject.SetActive(!Network.IsJoiningRoom);
            cancelButton.gameObject.SetActive(Network.IsJoiningRoom);
            cancelButton.interactable = !Network.IsCancelingJoin;
            for (int i = 0; i < countButtons.Length; i++)
            {
                bool selected = creationCapacity == i + 2;
                countButtons[i].interactable = canChoose;
                countButtons[i].targetGraphic.color = ColorOf(selected ? "3F3D38" : "E8DFCB");
                countButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().color = ColorOf(selected ? "F3EBDD" : "292921");
            }
            RoomRow[] rooms = session.PendingRooms.Select(room => new RoomRow
                { key = room.Key, name = room.Name, port = room.Port, participants = 1, capacity = room.Capacity,
                    pending = true, ready = room.Ready, waiting = true })
                .Concat(Network.DiscoveredRooms.Select(room => new RoomRow
                { key = room.Key, name = room.Name, port = room.Port, participants = room.Participants,
                    capacity = room.TargetParticipants, waiting = room.WaitingForHost, playing = room.Playing, full = room.IsFull }))
                .GroupBy(room => room.key, StringComparer.OrdinalIgnoreCase).Select(group => group.First())
                .OrderBy(room => LabRoomNames.SortNumber(room.name)).ThenBy(room => room.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(room => room.port).ToArray();
            emptyRooms.SetActive(rooms.Length == 0);
            while (rows.Count < rooms.Length)
            {
                UnityEngine.UI.Button row = Instantiate(roomTemplate, roomContent);
                int index = rows.Count;
                row.onClick.AddListener(() => EnterRoom(index));
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
                rows[i].transform.Find("Count").GetComponent<UnityEngine.UI.Text>().text = room.capacity >= 2
                    ? room.participants + " / " + room.capacity + "명 모였소" : "인원 확인 중";
                bool connecting = Network.IsJoiningRoom && Network.JoiningRoomKey == room.key;
                string action = room.pending && !room.ready ? "준비 중..." : connecting ? "입장 중..."
                    : !room.pending && room.waiting ? "방장 대기" : room.playing ? "봉인 중" : room.full ? "정원 마감" : "입장 →";
                var label = rows[i].transform.Find("Action").GetComponent<UnityEngine.UI.Text>();
                label.text = action; label.fontSize = action == "입장 →" ? 20 : 14;
                rows[i].interactable = room.pending ? room.ready && canChoose : Network.CanJoinDiscoveredRoom(room.key);
            }
            roomTemplate.gameObject.SetActive(false);
        }

        private void RefreshPositions()
        {
            int count = Network.TargetParticipantCount;
            bool allChosen = Network.ParticipantCount == count && Network.SelectedParticipantCount == count;
            heading.text = allChosen ? "요괴 봉인 준비 완료!" : "빈 자리를 고르시오.";
            subheading.text = Network.SlotSelectionPending ? "자리 선택을 확인하고 있소..."
                : allChosen ? (Network.SessionConfigReady ? "" : "방 설정을 확인하고 있소...") : "실제로 앉은 위치의 빈 자리를 눌러주시오.";
            roomName.text = Network.HostRoomNamePending ? "방을 만들고 있소..." : LabRoomNames.Display(Network.ConnectedRoomName);
            joinedCount.text = Network.ParticipantCount + " / " + count + "명 입장";
            selectedCount.text = Network.SelectedParticipantCount + " / " + count + "명 자리 확정";
            connectionLines.gameObject.SetActive(count >= 3);
            if (count >= 3)
            {
                connectionLines.sprite = lines[count - 3];
                CopyRect(lineLayouts[count - 3], connectionLines.rectTransform);
            }
            RectTransform[] anchors = count == 2 ? seats2 : count == 3 ? seats3 : count == 4 ? seats4 : seats5;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                bool active = i < count;
                slotButtons[i].gameObject.SetActive(active); localLabels[i].gameObject.SetActive(active);
                if (!active) continue;
                CopyRect(anchors[i], (RectTransform)slotButtons[i].transform);
                ulong owner = Network.GetSlotOwner(i + 1);
                bool occupied = owner != ulong.MaxValue, mine = occupied && owner == Network.LocalPlayerId;
                slotImages[i].sprite = occupied ? filledSlots[i] : emptySlots[i];
                slotImages[i].rectTransform.localEulerAngles = new Vector3(0, 0, 180);
                slotLabels[i].text = "P" + (i + 1);
                slotLabels[i].color = occupied ? ColorOf("F6F6F6") : Color.black;
                slotButtons[i].interactable = Network.CanSelectPlayerSlot && (!occupied || mine);
                bool host = occupied && owner == Unity.Netcode.NetworkManager.ServerClientId;
                localLabels[i].text = (host ? "방장" : string.Empty) + (mine ? (host ? " · 나 · 취소" : "나 · 취소") : string.Empty);
                Vector2 center = anchors[i].anchoredPosition;
                LabLobbyView.Place(localLabels[i].rectTransform, center.x - 60f, -center.y + 42f, 120f, 25f);
            }
            clearSlotButton.interactable = Network.CanSelectPlayerSlot && Network.LocalSlotNumber != 0;
            bool start = Network.IsHost && Network.CanStartBattle;
            sealButton.interactable = start;
            sealButton.targetGraphic.color = ColorOf(start ? "AF4738" : "C7C0B0");
            var text = sealButton.GetComponentInChildren<UnityEngine.UI.Text>();
            text.text = start ? "요괴 봉인 시작 · 3스테이지" : allChosen && !Network.IsHost ? "방장의 봉인을 기다리시오" : "요괴 봉인 대기";
            text.color = ColorOf(start ? "FFF7E5" : "575750");
        }

        private void SetDirectConnection(bool visible)
        {
            if (visible && (session.State.phase != GameFlowPhase.Home || !Network.CanChooseConnection)) return;
            directConnectionPanel.SetActive(visible);
            if (!visible) return;
            directConnectionPanel.transform.SetAsLastSibling();
            directAddress.SetTextWithoutNotify(Network.hostAddress);
            directPort.SetTextWithoutNotify(Network.port.ToString(CultureInfo.InvariantCulture));
            directFeedback.text = "방장의 IP 주소와 포트를 입력하시오. 같은 Wi-Fi 또는 핫스팟에 연결해야 하오.";
        }

        private void JoinDirect()
        {
            if (session.State.phase != GameFlowPhase.Home || !Network.CanChooseConnection) return;
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
