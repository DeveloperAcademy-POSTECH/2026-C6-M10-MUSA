using System;
using System.Collections.Generic;
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
        public Button generateButton;
        public Text generateLabel;
        public Button leaveButton;
        public Text statusText;
        public Text noticeText;
        public Text peersText;
        public Text hpText;
        public Text timeText;
        public Text staminaText;
        public Text orbsText;
        public Text seatText;

        private readonly List<Button> roomRows = new List<Button>();
        private string renderedRoomList = string.Empty;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(Host);
            if (joinButton != null) joinButton.onClick.AddListener(Join);
            if (findHostButton != null) findHostButton.onClick.AddListener(FindHost);
            if (decreasePlayersButton != null) decreasePlayersButton.onClick.AddListener(() => ChangePlayerCount(-1));
            if (increasePlayersButton != null) increasePlayersButton.onClick.AddListener(() => ChangePlayerCount(1));
            if (refreshRoomsButton != null) refreshRoomsButton.onClick.AddListener(RefreshRooms);
            if (cancelRoomSearchButton != null) cancelRoomSearchButton.onClick.AddListener(() => network?.CancelRoomSearch());
            if (startButton != null) startButton.onClick.AddListener(() => network?.StartBattle());
            if (generateButton != null) generateButton.onClick.AddListener(() => network?.Generate());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => network?.Stop());
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
            bool playing = snapshot != null && snapshot.phase == LabPhase.Playing;
            SetVisible(sessionControls, !playing);
            SetVisible(statusText == null ? null : statusText.gameObject, !playing);
            bool connected = network.IsConnected;
            bool searching = network.IsSearchingRooms;
            bool hostLobby = connected && network.IsHost && !playing;
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
            if (generateButton != null) generateButton.interactable = connected && snapshot != null && snapshot.phase == LabPhase.Playing;
            if (network.config != null) Set(generateLabel, "GENERATE / " + network.config.GenerateCost.ToString("0.#"));
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
                Set(hpText, "YOKAI HP —");
                Set(timeText, "TIME —");
                Set(staminaText, "STAMINA —");
                Set(orbsText, "ORBS —");
                Set(seatText, connected ? "PLAYER " + network.LocalPlayerId : "NO SESSION");
                return;
            }
            Set(hpText, "YOKAI HP " + snapshot.hp + " / " + network.config.MonsterMaxHp);
            Set(timeText, "TIME " + Mathf.CeilToInt((float)snapshot.remaining));
            LabPlayerState local = Array.Find(snapshot.players ?? Array.Empty<LabPlayerState>(), p => p.id == network.LocalPlayerId);
            Set(staminaText, local == null ? "STAMINA —" : "STAMINA " + Mathf.FloorToInt(local.stamina) + " / " + network.config.StaminaMax);
            int count = 0;
            foreach (var orb in snapshot.orbs ?? Array.Empty<LabOrbState>()) if (orb.owner == network.LocalPlayerId && !orb.inFlight) count++;
            Set(orbsText, "ORBS " + count + " / " + network.config.OrbStorageLimit);
            Set(seatText, local == null ? "PLAYER —" : "PLAYER " + local.id + " · SEAT "
                + (local.seat + 1) + (network.IsDeveloperSolo ? " · SOLO" : string.Empty));
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
