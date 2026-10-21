using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>Product lobby presentation. All claims and start gates remain Host-authoritative.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class LabLobbyView : MonoBehaviour
    {
        public LabHud hud;
        public RectTransform design;
        public RawImage background;
        public GameObject titlePage, browserPage, positionPage, emptyRooms;
        public Button enterButton, createButton, refreshButton, cancelButton, leaveButton, sealButton;
        public Button[] countButtons, slotButtons;
        public Text heading, subheading, roomName, joinedCount, selectedCount, message;
        public Text[] slotLabels, localLabels;
        public Image[] slotImages;
        public Image connectionLines;
        public Sprite[] emptySlots, filledSlots, lines;
        public RectTransform roomContent;
        public Button roomTemplate;
        public Font mediumFont;
        private bool entered;
        private int creationCapacity = 3;
        private string creationError = string.Empty;
        private readonly List<LabPendingRoom> createdRooms = new List<LabPendingRoom>();
        private readonly List<Button> rows = new List<Button>();
        private readonly List<Text> rowNames = new List<Text>(), rowCounts = new List<Text>(), rowActions = new List<Text>();
        private readonly List<string> keys = new List<string>();
        private LabNetwork Network => hud == null ? null : hud.network;
        public bool HasEntered => entered;
        private static Color Ink => Html("292921");
        public static Color Html(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        private void Awake()
        {
            enterButton.onClick.AddListener(EnterLobby);
            createButton.onClick.AddListener(CreateRoom);
            refreshButton.onClick.AddListener(() => Network?.StartRoomSearch());
            cancelButton.onClick.AddListener(() => Network?.CancelJoin());
            leaveButton.onClick.AddListener(() => Network?.Stop());
            sealButton.onClick.AddListener(() => Network?.StartBattle());
            for (int i = 0; i < countButtons.Length; i++)
            {
                int count = i + 3;
                countButtons[i].onClick.AddListener(() =>
                {
                    if (Network == null || !Network.CanChooseConnection) return;
                    creationCapacity = count;
                    creationError = string.Empty;
                    Network.SetTargetParticipantCount(count);
                });
            }
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int number = i + 1;
                slotButtons[i].onClick.AddListener(() => Network?.RequestPlayerSlot(Network.LocalSlotNumber == number ? 0 : number));
            }
        }

        public void EnterLobby()
        {
            entered = true;
            if (Network != null && Network.CanChooseConnection && !Network.IsSearchingRooms) Network.StartRoomSearch();
        }

        private void CreateRoom()
        {
            if (Network == null || !Network.CanChooseConnection || creationCapacity < 3) return;
            if (createdRooms.Count >= LabBonjourDiscovery.MaximumServices)
            { creationError = "열린 방이 너무 많소. 만든 방에 먼저 입장해주시오."; return; }
            if (!LabPendingRoom.TryCreate(creationCapacity, out var room, out creationError)) return;
            createdRooms.Add(room);
            creationCapacity = 0;
        }

        private void EnterRoom(int index)
        {
            if (index < 0 || index >= keys.Count) return;
            var owned = createdRooms.Find(room => room.Key == keys[index]);
            if (owned != null)
            {
                if (owned.EnterAsCreator(Network))
                {
                    createdRooms.Remove(owned);
                    owned.Dispose();
                    hud.port.SetTextWithoutNotify(Network.port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    creationError = string.Empty;
                }
                return;
            }
            Network.JoinDiscoveredRoom(keys[index]);
        }

        private void LateUpdate()
        {
            if (Network == null || hud == null) return;
            // Other created rooms remain advertised even while this player is inside a game.
            bool canRegister = true;
            int nextNumber = 1;
            foreach (var room in createdRooms)
            {
                room.Tick(canRegister, nextNumber);
                if (room.Ready && LabRoomNames.TryNumber(room.Name, out int number)) nextNumber = Math.Max(nextNumber, number + 1);
                // Confirm one name before registering the next so rapid creation cannot swap numbers.
                if (!room.Ready && !room.Failed) canRegister = false;
            }
            for (int i = createdRooms.Count - 1; i >= 0; i--)
                if (createdRooms[i].Failed)
                {
                    createdRooms[i].Dispose(); createdRooms.RemoveAt(i);
                    creationError = "방을 공개하지 못했소. 같은 Wi-Fi와 네트워크 권한을 확인해주시오.";
                }
            bool battle = Network.Snapshot != null && Network.Snapshot.phase != LabPhase.Idle;
            bool developerOpen = hud.developerMode != null && hud.developerMode.panel != null && hud.developerMode.panel.activeInHierarchy;
            bool visible = !battle && !developerOpen;
            background.gameObject.SetActive(visible);
            design.gameObject.SetActive(visible);
            if (!visible) return;
            FitSafeArea();
            bool connected = Network.IsConnected && !Network.IsJoiningRoom;
            bool inRoom = connected;
            if (connected || Network.IsJoiningRoom) entered = true;
            titlePage.SetActive(!entered);
            browserPage.SetActive(entered && !inRoom);
            positionPage.SetActive(entered && inRoom);
            background.uvRect = BackgroundUv(!entered, background.rectTransform.rect.size);
            if (!entered) return;
            if (inRoom) RefreshPositions(); else RefreshBrowser();
            string explanation = Network.IsJoiningRoom
                ? (Network.IsCancelingJoin ? "연결을 취소하고 있소..." : "방에 입장하고 있소...")
                : Network.Notice;
            if (!connected && !string.IsNullOrEmpty(creationError)) explanation = creationError;
            if (!connected && Network.RoomSearchFailed) explanation = "방을 찾거나 연결하지 못했소. 같은 Wi-Fi와 네트워크 권한을 확인하고 다시 찾아주시오.";
            message.text = explanation;
            message.gameObject.SetActive(!string.IsNullOrEmpty(explanation));
        }

        private void OnDestroy()
        {
            foreach (var room in createdRooms) room.Dispose();
            createdRooms.Clear();
        }

        public static Rect BackgroundUv(bool title, Vector2 size)
        {
            // Figma places the 1536x1024 source into a 1347x898 logical image,
            // then uses two distinct 402x874 crops. UVs stay relative to that layout.
            float w = 402f, h = 874f;
            float aspect = size.y > 0 ? size.x / size.y : w / h;
            if (aspect > w / h) h = w / aspect; else w = h * aspect;
            float x = (title ? 717f : 271f) + (402f - w) * .5f;
            float y = (title ? 24f : 0f) + (874f - h) * .5f;
            return new Rect(x / 1347f, 1f - (y + h) / 898f, w / 1347f, h / 898f);
        }

        private void FitSafeArea()
        {
            var canvas = GetComponentInParent<Canvas>();
            float factor = canvas == null ? 1f : canvas.scaleFactor;
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / factor / 402f, safe.height / factor / 874f);
            design.localScale = Vector3.one * scale;
            design.anchoredPosition = (safe.center - new Vector2(Screen.width, Screen.height) * .5f) / factor;
        }

        private void RefreshBrowser()
        {
            createButton.interactable = Network.CanChooseConnection && creationCapacity >= 3;
            refreshButton.interactable = Network.CanChooseConnection;
            refreshButton.gameObject.SetActive(!Network.IsJoiningRoom);
            cancelButton.gameObject.SetActive(Network.IsJoiningRoom);
            cancelButton.interactable = !Network.IsCancelingJoin;
            cancelButton.GetComponentInChildren<Text>().text = "입장 취소";
            for (int i = 0; i < countButtons.Length; i++)
            {
                bool selected = creationCapacity == i + 3;
                countButtons[i].interactable = Network.CanChooseConnection;
                countButtons[i].targetGraphic.color = Html(selected ? "3F3D38" : "E8DFCB");
                countButtons[i].GetComponentInChildren<Text>().color = selected ? Html("F3EBDD") : Ink;
            }
            var rooms = createdRooms.Select(room => new LabDiscoveredRoom(room.Name, room.Port, 1, room.Capacity, false, true))
                .Concat(Network.DiscoveredRooms).GroupBy(room => room.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).OrderBy(room => LabRoomNames.SortNumber(room.Name))
                .ThenBy(room => room.Name, StringComparer.OrdinalIgnoreCase).ThenBy(room => room.Port).ToArray();
            emptyRooms.SetActive(rooms.Length == 0);
            while (rows.Count < rooms.Length)
            {
                var row = Instantiate(roomTemplate, roomContent);
                row.name = "DiscoveredRoom";
                int index = rows.Count;
                row.onClick.AddListener(() => EnterRoom(index));
                rows.Add(row); keys.Add(string.Empty);
                rowNames.Add(row.transform.Find("Name").GetComponent<Text>());
                rowCounts.Add(row.transform.Find("Count").GetComponent<Text>());
                rowActions.Add(row.transform.Find("Action").GetComponent<Text>());
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < rooms.Length;
                rows[i].gameObject.SetActive(active);
                if (!active) continue;
                var room = rooms[i]; keys[i] = room.Key;
                rows[i].targetGraphic.color = Html(i % 2 == 0 ? "E9DFCA" : "EEE6D6");
                var owned = createdRooms.Find(pending => pending.Key == room.Key);
                rows[i].name = owned == null ? "DiscoveredRoom" : "CreatedRoom";
                rowNames[i].text = owned != null && string.IsNullOrEmpty(room.Name)
                    ? "새 방 준비 중" : DisplayRoomName(room.Name);
                rowCounts[i].text = room.TargetParticipants >= 2
                    ? room.Participants + " / " + room.TargetParticipants + "명 모였소"
                    : "인원 확인 중";
                bool connecting = Network.IsJoiningRoom && Network.JoiningRoomKey == room.Key;
                bool registering = owned != null && !owned.Ready;
                rowActions[i].text = registering ? "준비 중..." : connecting ? "입장 중..."
                    : owned == null && room.WaitingForHost ? "방장 대기" : room.Playing ? "봉인 중" : room.IsFull ? "정원 마감" : "입장 →";
                rowActions[i].fontSize = registering || connecting || (owned == null && room.WaitingForHost) || room.Playing || room.IsFull ? 14 : 20;
                rows[i].interactable = owned != null ? owned.Ready && Network.CanChooseConnection : Network.CanJoinDiscoveredRoom(room.Key);
            }
            roomTemplate.gameObject.SetActive(false);
        }

        private void RefreshPositions()
        {
            int count = Network.TargetParticipantCount;
            bool allChosen = Network.ParticipantCount == count && Network.SelectedParticipantCount == count;
            heading.text = allChosen ? "요괴 봉인 준비 완료!" : "빈 자리를 고르시오.";
            subheading.text = Network.SlotSelectionPending ? "자리 선택을 확인하고 있소..."
                : allChosen ? (Network.SessionConfigReady ? "" : "방 설정을 확인하고 있소...")
                : "실제로 앉은 위치의 빈 자리를 눌러주시오.";
            roomName.text = Network.HostRoomNamePending ? "방을 만들고 있소..." : DisplayRoomName(Network.ConnectedRoomName);
            joinedCount.text = Network.ParticipantCount + " / " + count + "명 입장";
            selectedCount.text = Network.SelectedParticipantCount + " / " + count + "명 자리 확정";
            int layout = Mathf.Clamp(count - 3, 0, lines.Length - 1);
            connectionLines.sprite = lines[layout];
            connectionLines.gameObject.SetActive(count >= 3);
            var lr = connectionLines.rectTransform;
            if (count == 3) Place(lr, 96.7f, 417.0f, 213.643f, 171.635f);
            else if (count == 4) Place(lr, 88.5f, 381.3f, 225.435f, 196.174f);
            else Place(lr, 106.8f, 385.8f, 187.975f, 178.949f);
            lr.localEulerAngles = new Vector3(0, 0, 180);
            for (int i = 0; i < slotButtons.Length; i++)
            {
                bool active = i < count;
                slotButtons[i].gameObject.SetActive(active);
                localLabels[i].gameObject.SetActive(active);
                if (!active) continue;
                Vector2 p = SlotCenter(count, i + 1);
                Place((RectTransform)slotButtons[i].transform, p.x - 34.41555f, p.y - 33.9867f, 68.8311f, 67.9734f);
                ulong owner = Network.GetSlotOwner(i + 1);
                bool occupied = owner != ulong.MaxValue;
                bool mine = occupied && owner == Network.LocalPlayerId;
                slotImages[i].sprite = occupied ? filledSlots[i] : emptySlots[i];
                // The original artwork is rotated with the diagram; text remains upright.
                slotImages[i].rectTransform.localEulerAngles = new Vector3(0, 0, 180);
                slotLabels[i].text = "P" + (i + 1);
                slotLabels[i].color = occupied ? Html("F6F6F6") : Color.black;
                slotButtons[i].interactable = i != 0 && !Network.IsHost && Network.CanSelectPlayerSlot && (!occupied || mine);
                localLabels[i].text = i == 0 ? (mine ? "방장 · 나" : "방장") : mine ? "나 · 취소" : "";
                Place(localLabels[i].rectTransform, p.x - 60, p.y + 42, 120, 25);
            }
            sealButton.interactable = Network.CanStartBattle;
            sealButton.targetGraphic.color = Html(Network.CanStartBattle ? "AF4738" : "C7C0B0");
            var label = sealButton.GetComponentInChildren<Text>();
            label.text = Network.CanStartBattle ? "요괴 봉인 시작" : allChosen && !Network.IsHost ? "방장의 봉인을 기다리시오" : "요괴 봉인 대기";
            label.color = Html(Network.CanStartBattle ? "FFF7E5" : "575750");
        }

        public static Vector2 SlotCenter(int count, int slot)
        {
            if (slot < 1 || slot > count || count < 2 || count > 5) throw new ArgumentOutOfRangeException();
            if (count == 2) return slot == 1 ? new Vector2(201, 574) : new Vector2(201, 402);
            if (count == 3) return new[] { new Vector2(201, 574), new Vector2(104, 424), new Vector2(292, 424) }[slot - 1];
            if (count == 4) return new[] { new Vector2(201, 574), new Vector2(103, 488), new Vector2(201, 402), new Vector2(299, 488) }[slot - 1];
            return new[] { new Vector2(201, 574), new Vector2(112, 509), new Vector2(141, 399), new Vector2(266, 400), new Vector2(291, 509) }[slot - 1];
        }

        public static string DisplayRoomName(string name) => LabRoomNames.Display(name);
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x + width * .5f, -y - height * .5f);
        }
        private static void Hide(GameObject go) { if (go != null && go.activeSelf) go.SetActive(false); }
    }
}
