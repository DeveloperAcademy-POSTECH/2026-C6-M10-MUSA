using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace C6.Prototype.Lobby
{
    /// <summary>A safe-area, scrollable lobby view. All authority and connection work stays outside this component.</summary>
    [DisallowMultipleComponent]
    public sealed class T10LobbyHud : MonoBehaviour
    {
        private static readonly Color Background = new Color(.035f, .075f, .10f, 1f);
        private static readonly Color Panel = new Color(.065f, .13f, .16f, 1f);
        private static readonly Color White = new Color(.93f, .96f, .95f, 1f);
        private static readonly Color Muted = new Color(.59f, .72f, .73f, 1f);
        private static readonly Color Teal = new Color(.39f, .80f, .74f, 1f);
        private static readonly Color Gold = new Color(.86f, .73f, .47f, 1f);
        private static readonly Color Alert = new Color(1f, .65f, .53f, 1f);
        private readonly List<Button> roomJoinButtons = new List<Button>();
        private readonly List<RectTransform> roomRows = new List<RectTransform>();
        private LobbyRoomRow[] displayedRooms = Array.Empty<LobbyRoomRow>();
        private LobbyUiState state = new LobbyUiState();
        private Font font;
        private RectTransform column, content, statusPanel, createPanel, browsePanel, directPanel, sessionPanel;
        private RectTransform directFields, roomList;
        private Text browseCaption, roomEmptyLabel, directCaption, readyCaption;
        private Text roomTitleLabel, roleLabel, localReadyLabel, remoteReadyLabel, relationHint;
        public Text PlayerRosterLabel { get; private set; }
        private Text directHint;
        private bool directExpanded;
        private Vector2Int lastScreen;
        private Rect lastSafeArea;
        private float lastScale;

        public event Action CreateRoomRequested;
        public event Action BrowseRequested;
        public event Action RefreshRequested;
        public event Action CancelBrowseRequested;
        public event Action LeaveRequested;
        public event Action ReadyToggleRequested;
        public event Action StartRequested;
        public event Action JoinDirectRequested;
        public event Action<string> RoomJoinRequested;

        public Canvas Canvas { get; private set; }
        public UISafeArea SafeArea { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public RectTransform Content => content;
        public InputField RoomNameInput { get; private set; }
        public InputField IPv4Input { get; private set; }
        public InputField PortInput { get; private set; }
        public string RoomName => RoomNameInput != null ? RoomNameInput.text.Trim() : string.Empty;
        public string HostAddress => IPv4Input != null ? IPv4Input.text.Trim() : string.Empty;
        public string Port => PortInput != null ? PortInput.text.Trim() : "7777";
        public bool DirectExpanded => directExpanded;
        public Button CreateRoomButton { get; private set; }
        public Button BrowseButton { get; private set; }
        public Button RefreshButton { get; private set; }
        public Button CancelBrowseButton { get; private set; }
        public Button DirectToggleButton { get; private set; }
        public Button JoinDirectButton { get; private set; }
        public Button ReadyButton { get; private set; }
        public Button StartButton { get; private set; }
        public Button LeaveButton { get; private set; }
        public Button CancelConnectionButton { get; private set; }
        public Text PhaseLabel { get; private set; }
        public Text StatusLabel { get; private set; }
        public Text ErrorLabel { get; private set; }
        public Text LocalPlayerLabel { get; private set; }
        public Text LeftPlayerLabel { get; private set; }
        public Text RightPlayerLabel { get; private set; }
        public Text ParticipantsLabel { get; private set; }
        public Text CompatibilityLabel { get; private set; }
        public Text StartStatusLabel { get; private set; }
        public IReadOnlyList<Button> RoomJoinButtons => roomJoinButtons;

        private void Awake()
        {
            font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateUi();
            SetState(state);
            SetRooms(Array.Empty<LobbyRoomRow>());
            RefreshGeometry();
        }

        private void LateUpdate()
        {
            if (lastScreen.x != Screen.width || lastScreen.y != Screen.height || lastSafeArea != Screen.safeArea ||
                !Mathf.Approximately(lastScale, Canvas.scaleFactor)) RefreshGeometry();
        }

        public void SetState(LobbyUiState value)
        {
            if (value == null) return;
            bool changedPage = state.Connected != value.Connected;
            state = value;
            if (Canvas == null) return;
            PhaseLabel.text = Clean(value.Phase);
            StatusLabel.text = Clean(value.Status);
            ErrorLabel.text = Clean(value.Error);
            ErrorLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(value.Error));
            roomTitleLabel.text = string.IsNullOrWhiteSpace(value.RoomTitle) ? "YOUR ROOM" : Clean(value.RoomTitle);
            roleLabel.text = Clean(value.Role);
            LocalPlayerLabel.text = "YOU  /  " + Clean(value.LocalPlayer);
            LeftPlayerLabel.text = "LEFT\n" + Clean(value.LeftPlayer);
            RightPlayerLabel.text = "RIGHT\n" + Clean(value.RightPlayer);
            ParticipantsLabel.text = Clean(value.Participants) + " PLAYERS";
            CompatibilityLabel.text = Clean(value.Compatibility);
            StartStatusLabel.text = Clean(value.StartStatus);
            localReadyLabel.text = value.LocalReady ? "YOU  /  READY" : "YOU  /  NOT READY";
            localReadyLabel.color = value.LocalReady ? Teal : Muted;
            remoteReadyLabel.text = value.RemoteReady ? "FRIEND  /  READY" : "FRIEND  /  NOT READY";
            remoteReadyLabel.color = value.RemoteReady ? Teal : Muted;
            PlayerRosterLabel.text = Clean(value.PlayerRoster);
            PlayerRosterLabel.gameObject.SetActive(value.Multiparty);
            localReadyLabel.transform.parent.gameObject.SetActive(!value.Multiparty);
            relationHint.text = value.Multiparty ? "Seats follow join order. Left and right form a circle."
                : "With two players, your friend is on both sides.";
            float rosterOffset = value.Multiparty ? 56f : 0f;
            Top((RectTransform)ReadyButton.transform, 12f, 12f, 341f + rosterOffset, 52f);
            Top((RectTransform)StartButton.transform, 12f, 12f, 404f + rosterOffset, 52f);
            Top(StartStatusLabel.rectTransform, 14f, 14f, 466f + rosterOffset, 54f);
            Top((RectTransform)LeaveButton.transform, 12f, 12f, 530f + rosterOffset, 50f);
            readyCaption.text = value.LocalReady ? "READY  ✓  /  CANCEL" : "I'M READY";
            browseCaption.text = value.Browsing ? "SEARCHING NEARBY" : "NEARBY ROOMS";
            roomEmptyLabel.text = value.Browsing ? "Looking for rooms on this Wi-Fi…" : "Tap FIND ROOMS to see available rooms.\nUse the same Wi-Fi as your friend.";
            SetButton(CreateRoomButton, value.CanCreate);
            SetButton(BrowseButton, value.CanBrowse);
            SetButton(RefreshButton, value.CanRefresh);
            SetButton(CancelBrowseButton, value.CanCancelBrowse);
            SetButton(JoinDirectButton, value.CanJoinDirect);
            SetButton(ReadyButton, value.CanReady);
            SetButton(StartButton, value.CanStart);
            SetButton(LeaveButton, value.CanLeave);
            SetButton(CancelConnectionButton, value.CanLeave);
            CancelConnectionButton.gameObject.SetActive(!value.Connected && value.CanLeave);
            RoomNameInput.interactable = value.CanCreate;
            IPv4Input.interactable = value.CanJoinDirect;
            PortInput.interactable = value.CanJoinDirect || value.CanCreate;
            for (int i = 0; i < roomJoinButtons.Count; i++)
                roomJoinButtons[i].interactable = displayedRooms[i].CanJoin && !value.Connected && value.CanJoinDirect;
            createPanel.gameObject.SetActive(!value.Connected);
            browsePanel.gameObject.SetActive(!value.Connected);
            directPanel.gameObject.SetActive(!value.Connected);
            sessionPanel.gameObject.SetActive(value.Connected);
            LayoutSections();
            if (changedPage) Scroll.verticalNormalizedPosition = 1f;
        }

        public void SetRooms(IReadOnlyList<LobbyRoomRow> rooms)
        {
            int count = rooms != null ? rooms.Count : 0;
            bool same = count == displayedRooms.Length;
            if (same)
                for (int i = 0; i < count; i++)
                {
                    LobbyRoomRow a = displayedRooms[i], b = rooms[i];
                    if (b == null || a.Id != b.Id || a.Title != b.Title || a.Address != b.Address ||
                        a.Status != b.Status || a.CanJoin != b.CanJoin) { same = false; break; }
                }
            if (same) { LayoutSections(); return; }
            foreach (RectTransform row in roomRows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            roomRows.Clear();
            roomJoinButtons.Clear();
            displayedRooms = new LobbyRoomRow[count];
            for (int i = 0; i < count; i++)
            {
                LobbyRoomRow source = rooms[i] ?? new LobbyRoomRow();
                var entry = new LobbyRoomRow { Id = source.Id, Title = source.Title, Address = source.Address,
                    Status = source.Status, CanJoin = source.CanJoin };
                displayedRooms[i] = entry;
                RectTransform row = Image("RoomRow" + i, roomList, new Color(.085f, .18f, .20f, 1f)).rectTransform;
                Top(row, 0f, 0f, i * 110f, 102f);
                var title = Text("RoomTitle", row, Clean(entry.Title), 16, White, TextAnchor.MiddleLeft);
                Top(title.rectTransform, 12f, 91f, 7f, 28f);
                var address = Text("RoomAddress", row, Clean(entry.Address), 12, Muted, TextAnchor.MiddleLeft);
                Top(address.rectTransform, 12f, 91f, 37f, 18f);
                var status = Text("RoomStatus", row, Clean(entry.Status), 12, entry.CanJoin ? Teal : Gold, TextAnchor.UpperLeft);
                Top(status.rectTransform, 12f, 91f, 60f, 33f);
                Button join = Button("JoinRoom", row, entry.CanJoin ? "JOIN" : "UNAVAILABLE", entry.CanJoin);
                RectTransform joinRect = (RectTransform)join.transform;
                joinRect.anchorMin = joinRect.anchorMax = new Vector2(1f, .5f);
                joinRect.pivot = new Vector2(1f, .5f);
                joinRect.anchoredPosition = new Vector2(-10f, 0f);
                joinRect.sizeDelta = new Vector2(75f, 52f);
                join.interactable = entry.CanJoin && !state.Connected && state.CanJoinDirect;
                join.onClick.AddListener(() => RoomJoinRequested?.Invoke(entry.Id));
                roomJoinButtons.Add(join);
                roomRows.Add(row);
            }
            LayoutSections();
        }

        public void SetDirectExpanded(bool expanded)
        {
            directExpanded = expanded;
            if (directFields == null) return;
            directFields.gameObject.SetActive(expanded);
            directCaption.text = expanded ? "DIRECT IP  −" : "DIRECT IP  +";
            LayoutSections();
        }

        private void CreateUi()
        {
            RectTransform canvasRect = Rect("T10LobbyCanvas", transform);
            Canvas = canvasRect.gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 20;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            Image("Background", canvasRect, Background);
            RectTransform safe = Rect("SafeArea", canvasRect);
            SafeArea = safe.gameObject.AddComponent<UISafeArea>();
            column = Rect("CenteredColumn", safe);
            RectTransform header = Rect("Header", column);
            Top(header, 20f, 20f, 8f, 70f);
            var brand = Text("Brand", header, "HAPIO", 28, White, TextAnchor.UpperLeft);
            Top(brand.rectTransform, 0f, 0f, 2f, 33f);
            var subtitle = Text("Subtitle", header, "PLAY TOGETHER", 11, Teal, TextAnchor.MiddleLeft);
            Top(subtitle.rectTransform, 1f, 115f, 40f, 18f);
            PhaseLabel = Text("Phase", header, "OFFLINE", 11, Gold, TextAnchor.MiddleRight);
            Top(PhaseLabel.rectTransform, 180f, 0f, 40f, 18f);
            var line = Image("HeaderRule", header, new Color(Teal.r, Teal.g, Teal.b, .3f));
            Top(line.rectTransform, 0f, 0f, 69f, 1f);
            RectTransform viewport = Image("LobbyViewport", column, Color.clear).rectTransform;
            viewport.offsetMin = new Vector2(20f, 8f);
            viewport.offsetMax = new Vector2(-20f, -90f);
            viewport.gameObject.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            Scroll = viewport.gameObject.AddComponent<ScrollRect>();
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.scrollSensitivity = 22f;
            Scroll.viewport = viewport;
            content = Rect("LobbyContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            Scroll.content = content;
            CreateStatusPanel();
            CreateRoomPanel();
            CreateBrowsePanel();
            CreateDirectPanel();
            CreateSessionPanel();
            if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var eventObject = new GameObject("T10EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
            }
        }

        private void CreateStatusPanel()
        {
            statusPanel = Rect("StatusPanel", content);
            StatusLabel = Text("Status", statusPanel, string.Empty, 14, White, TextAnchor.UpperLeft);
            Top(StatusLabel.rectTransform, 0f, 0f, 0f, 43f);
            ErrorLabel = Text("Error", statusPanel, string.Empty, 12, Alert, TextAnchor.UpperLeft);
            Top(ErrorLabel.rectTransform, 0f, 0f, 47f, 58f);
            CancelConnectionButton = Button("CancelConnection", statusPanel, "CANCEL CONNECTION", false);
            CancelConnectionButton.onClick.AddListener(() => LeaveRequested?.Invoke());
        }

        private void CreateRoomPanel()
        {
            createPanel = Image("CreatePanel", content, Panel).rectTransform;
            var title = Text("CreateCaption", createPanel, "START A ROOM", 12, Gold, TextAnchor.MiddleLeft);
            Top(title.rectTransform, 14f, 14f, 10f, 20f);
            RoomNameInput = Input("RoomName", createPanel, "Room name", "Hapio Room");
            RoomNameInput.characterLimit = 32;
            RoomNameInput.keyboardType = TouchScreenKeyboardType.Default;
            Top((RectTransform)RoomNameInput.transform, 12f, 12f, 36f, 48f);
            CreateRoomButton = Button("CreateRoom", createPanel, "CREATE ROOM", true);
            Top((RectTransform)CreateRoomButton.transform, 12f, 12f, 94f, 50f);
            CreateRoomButton.onClick.AddListener(() => CreateRoomRequested?.Invoke());
        }

        private void CreateBrowsePanel()
        {
            browsePanel = Rect("BrowsePanel", content);
            browseCaption = Text("BrowseCaption", browsePanel, "NEARBY ROOMS", 12, Gold, TextAnchor.MiddleLeft);
            Top(browseCaption.rectTransform, 0f, 0f, 0f, 24f);
            RectTransform actions = Rect("BrowseActions", browsePanel);
            Top(actions, 0f, 0f, 32f, 48f);
            BrowseButton = Button("Browse", actions, "FIND ROOMS", true);
            Fraction((RectTransform)BrowseButton.transform, 0f, .44f);
            RefreshButton = Button("Refresh", actions, "REFRESH", false);
            Fraction((RectTransform)RefreshButton.transform, .44f, .74f);
            CancelBrowseButton = Button("CancelBrowse", actions, "CANCEL", false);
            Fraction((RectTransform)CancelBrowseButton.transform, .74f, 1f);
            BrowseButton.onClick.AddListener(() => BrowseRequested?.Invoke());
            RefreshButton.onClick.AddListener(() => RefreshRequested?.Invoke());
            CancelBrowseButton.onClick.AddListener(() => CancelBrowseRequested?.Invoke());
            roomList = Rect("DiscoveredRooms", browsePanel);
            roomEmptyLabel = Text("EmptyRoomList", roomList, string.Empty, 13, Muted, TextAnchor.MiddleLeft);
            Top(roomEmptyLabel.rectTransform, 0f, 0f, 0f, 58f);
        }

        private void CreateDirectPanel()
        {
            directPanel = Image("DirectPanel", content, Panel).rectTransform;
            DirectToggleButton = Button("DirectToggle", directPanel, "DIRECT IP  +", false);
            Top((RectTransform)DirectToggleButton.transform, 0f, 0f, 0f, 48f);
            directCaption = DirectToggleButton.GetComponentInChildren<Text>();
            DirectToggleButton.onClick.AddListener(() => SetDirectExpanded(!directExpanded));
            directFields = Rect("DirectFields", directPanel);
            Top(directFields, 12f, 12f, 58f, 160f);
            directHint = Text("DirectHint", directFields, "Can't find a room? Enter the host address.", 12, Muted, TextAnchor.UpperLeft);
            Top(directHint.rectTransform, 0f, 0f, 0f, 32f);
            RectTransform inputs = Rect("AddressFields", directFields);
            Top(inputs, 0f, 0f, 40f, 48f);
            IPv4Input = Input("HostIPv4", inputs, "Host IPv4", string.Empty);
            IPv4Input.characterLimit = 15;
            Fraction((RectTransform)IPv4Input.transform, 0f, .73f);
            PortInput = Input("Port", inputs, "Port", "7777");
            PortInput.characterLimit = 5;
            PortInput.contentType = InputField.ContentType.IntegerNumber;
            Fraction((RectTransform)PortInput.transform, .73f, 1f);
            JoinDirectButton = Button("JoinDirect", directFields, "JOIN BY IP", false);
            Top((RectTransform)JoinDirectButton.transform, 0f, 0f, 100f, 50f);
            JoinDirectButton.onClick.AddListener(() => JoinDirectRequested?.Invoke());
            SetDirectExpanded(false);
        }

        private void CreateSessionPanel()
        {
            sessionPanel = Image("SessionPanel", content, Panel).rectTransform;
            roomTitleLabel = Text("ConnectedRoomTitle", sessionPanel, string.Empty, 19, White, TextAnchor.MiddleLeft);
            Top(roomTitleLabel.rectTransform, 14f, 14f, 12f, 31f);
            roleLabel = Text("Role", sessionPanel, string.Empty, 12, Gold, TextAnchor.MiddleLeft);
            Top(roleLabel.rectTransform, 14f, 145f, 48f, 21f);
            ParticipantsLabel = Text("Participants", sessionPanel, "0 / 2 PLAYERS", 12, Muted, TextAnchor.MiddleRight);
            Top(ParticipantsLabel.rectTransform, 145f, 14f, 48f, 21f);
            LocalPlayerLabel = Text("LocalPlayer", sessionPanel, "YOU / —", 22, Teal, TextAnchor.MiddleCenter);
            Top(LocalPlayerLabel.rectTransform, 14f, 14f, 81f, 34f);
            RectTransform neighbours = Rect("Neighbours", sessionPanel);
            Top(neighbours, 14f, 14f, 127f, 62f);
            LeftPlayerLabel = Text("LeftPlayer", neighbours, "LEFT\n—", 15, White, TextAnchor.MiddleCenter);
            Fraction(LeftPlayerLabel.rectTransform, 0f, .5f);
            RightPlayerLabel = Text("RightPlayer", neighbours, "RIGHT\n—", 15, White, TextAnchor.MiddleCenter);
            Fraction(RightPlayerLabel.rectTransform, .5f, 1f);
            relationHint = Text("TwoPlayerHint", sessionPanel, "With two players, your friend is on both sides.", 11, Muted, TextAnchor.MiddleCenter);
            Top(relationHint.rectTransform, 14f, 14f, 194f, 32f);
            CompatibilityLabel = Text("Compatibility", sessionPanel, string.Empty, 12, Muted, TextAnchor.MiddleCenter);
            Top(CompatibilityLabel.rectTransform, 14f, 14f, 239f, 45f);
            RectTransform readiness = Rect("Readiness", sessionPanel);
            Top(readiness, 14f, 14f, 293f, 38f);
            localReadyLabel = Text("LocalReady", readiness, string.Empty, 12, Muted, TextAnchor.MiddleLeft);
            Fraction(localReadyLabel.rectTransform, 0f, .5f);
            remoteReadyLabel = Text("RemoteReady", readiness, string.Empty, 12, Muted, TextAnchor.MiddleRight);
            Fraction(remoteReadyLabel.rectTransform, .5f, 1f);
            PlayerRosterLabel = Text("PlayerRoster", sessionPanel, string.Empty, 13, White, TextAnchor.UpperLeft);
            Top(PlayerRosterLabel.rectTransform, 14f, 14f, 291f, 92f);
            PlayerRosterLabel.gameObject.SetActive(false);
            ReadyButton = Button("Ready", sessionPanel, "I'M READY", true);
            Top((RectTransform)ReadyButton.transform, 12f, 12f, 341f, 52f);
            readyCaption = ReadyButton.GetComponentInChildren<Text>();
            ReadyButton.onClick.AddListener(() => ReadyToggleRequested?.Invoke());
            StartButton = Button("HostStart", sessionPanel, "HOST START", false);
            Top((RectTransform)StartButton.transform, 12f, 12f, 404f, 52f);
            StartButton.onClick.AddListener(() => StartRequested?.Invoke());
            StartStatusLabel = Text("StartStatus", sessionPanel, string.Empty, 13, Teal, TextAnchor.MiddleCenter);
            Top(StartStatusLabel.rectTransform, 14f, 14f, 466f, 54f);
            LeaveButton = Button("Leave", sessionPanel, "LEAVE ROOM", false);
            Top((RectTransform)LeaveButton.transform, 12f, 12f, 530f, 50f);
            LeaveButton.onClick.AddListener(() => LeaveRequested?.Invoke());
        }

        private void LayoutSections()
        {
            if (content == null || statusPanel == null || createPanel == null || browsePanel == null ||
                directPanel == null || sessionPanel == null) return;
            float y = 0f;
            float statusHeight = string.IsNullOrWhiteSpace(state.Error) ? 50f : 112f;
            if (!state.Connected && state.CanLeave)
            {
                Top((RectTransform)CancelConnectionButton.transform, 0f, 0f, statusHeight, 48f);
                statusHeight += 60f;
            }
            Top(statusPanel, 0f, 0f, y, statusHeight);
            y += statusHeight + 8f;
            if (state.Connected)
            {
                Top(sessionPanel, 0f, 0f, y, state.Multiparty ? 650f : 594f);
                y += (state.Multiparty ? 650f : 594f) + 16f;
            }
            else
            {
                Top(createPanel, 0f, 0f, y, 158f);
                y += 178f;
                float listHeight = displayedRooms.Length == 0 ? 62f : displayedRooms.Length * 110f - 8f;
                float browseHeight = 94f + listHeight;
                Top(browsePanel, 0f, 0f, y, browseHeight);
                Top(roomList, 0f, 0f, 94f, listHeight);
                roomEmptyLabel.gameObject.SetActive(displayedRooms.Length == 0);
                y += browseHeight + 20f;
                float directHeight = directExpanded ? 222f : 48f;
                Top(directPanel, 0f, 0f, y, directHeight);
                y += directHeight + 16f;
            }
            content.sizeDelta = new Vector2(0f, y);
        }

        private void RefreshGeometry()
        {
            if (Canvas == null || column == null) return;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            lastScale = Canvas.scaleFactor;
            float width = Mathf.Min(580f, Screen.safeArea.width / Mathf.Max(.0001f, Canvas.scaleFactor));
            column.anchorMin = new Vector2(.5f, 0f);
            column.anchorMax = new Vector2(.5f, 1f);
            column.pivot = new Vector2(.5f, .5f);
            column.anchoredPosition = Vector2.zero;
            column.sizeDelta = new Vector2(width, 0f);
            LayoutSections();
        }

        private InputField Input(string name, Transform parent, string placeholder, string value)
        {
            Image background = Image(name, parent, new Color(.12f, .22f, .25f, 1f));
            background.raycastTarget = true;
            var input = background.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            var text = Text("Value", background.transform, string.Empty, 15, White, TextAnchor.MiddleLeft);
            text.rectTransform.offsetMin = new Vector2(12f, 5f);
            text.rectTransform.offsetMax = new Vector2(-12f, -5f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            input.textComponent = text;
            var hint = Text("Placeholder", background.transform, placeholder, 14, Muted, TextAnchor.MiddleLeft);
            hint.rectTransform.offsetMin = new Vector2(12f, 5f);
            hint.rectTransform.offsetMax = new Vector2(-12f, -5f);
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            input.text = value;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        private Button Button(string name, Transform parent, string caption, bool primary)
        {
            Image background = Image(name, parent, primary ? new Color(.19f, .44f, .43f, 1f) : new Color(.14f, .26f, .29f, 1f));
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f, 1f);
            colors.pressedColor = new Color(.72f, .83f, .83f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.52f, .58f, .60f, .75f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var text = Text("Caption", background.transform, caption, 13, White, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(5f, 4f);
            text.rectTransform.offsetMax = new Vector2(-5f, -4f);
            return button;
        }

        private Text Text(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Min(size, 11);
            label.resizeTextMaxSize = size;
            return label;
        }

        private static void SetButton(Button button, bool enabled) { if (button != null) button.interactable = enabled; }
        private static string Clean(string value) => value ?? string.Empty;
        private static Image Image(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static void Top(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
        private static void Fraction(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(left > 0f ? 4f : 0f, 0f);
            rect.offsetMax = new Vector2(right < 1f ? -4f : 0f, 0f);
        }
    }
}
