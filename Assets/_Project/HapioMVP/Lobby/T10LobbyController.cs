using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Lobby.Discovery;
using C6.Prototype.Networking;
using UnityEngine;

namespace C6.Prototype.Lobby
{
    /// <summary>Connects the passive lobby HUD to the existing session. Start confirmation never enters gameplay here.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(T10LobbySession), typeof(T10LobbyHud))]
    public sealed class T10LobbyController : MonoBehaviour
    {
        [SerializeField] private T10LobbySession session;
        [SerializeField] private T10LobbyHud hud;
        private T10LobbySession subscribedSession;
        private T10LobbyHud subscribedHud;
        private string actionError = string.Empty;

        public T10LobbySession Session => session;
        public T10LobbyHud Hud => hud;

        public void Configure(T10LobbySession value, T10LobbyHud view)
        {
            if (value == null || view == null) throw new ArgumentNullException(value == null ? nameof(value) : nameof(view));
            Unsubscribe();
            session = value;
            hud = view;
            if (isActiveAndEnabled) Subscribe();
            RefreshView();
        }

        private void Awake()
        {
            if (session == null) session = GetComponent<T10LobbySession>();
            if (hud == null) hud = GetComponent<T10LobbyHud>();
        }

        private void OnEnable() { Subscribe(); RefreshView(); }
        // All sibling Awakes have completed by Start, including the session's transport and discovery setup.
        private void Start() { Subscribe(); RefreshView(); }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (session == null || hud == null || (subscribedSession == session && subscribedHud == hud)) return;
            Unsubscribe();
            subscribedSession = session;
            subscribedHud = hud;
            // T10LobbySession forwards discovery changes, so subscribing again to Discovery would duplicate rendering.
            subscribedSession.Changed += RefreshView;
            subscribedHud.CreateRoomRequested += CreateRoom;
            subscribedHud.BrowseRequested += Browse;
            subscribedHud.RefreshRequested += RefreshRooms;
            subscribedHud.CancelBrowseRequested += CancelBrowse;
            subscribedHud.RoomJoinRequested += JoinRoom;
            subscribedHud.JoinDirectRequested += JoinDirect;
            subscribedHud.ReadyToggleRequested += ToggleReady;
            subscribedHud.StartRequested += StartMatch;
            subscribedHud.LeaveRequested += Leave;
        }

        private void Unsubscribe()
        {
            if (subscribedSession != null) subscribedSession.Changed -= RefreshView;
            if (subscribedHud != null)
            {
                subscribedHud.CreateRoomRequested -= CreateRoom;
                subscribedHud.BrowseRequested -= Browse;
                subscribedHud.RefreshRequested -= RefreshRooms;
                subscribedHud.CancelBrowseRequested -= CancelBrowse;
                subscribedHud.RoomJoinRequested -= JoinRoom;
                subscribedHud.JoinDirectRequested -= JoinDirect;
                subscribedHud.ReadyToggleRequested -= ToggleReady;
                subscribedHud.StartRequested -= StartMatch;
                subscribedHud.LeaveRequested -= Leave;
            }
            subscribedSession = null;
            subscribedHud = null;
        }

        public void RefreshView()
        {
            if (session == null || hud == null || hud.Canvas == null) return;
            DirectConnectionSession connection = session.Connection;
            LobbySnapshot snapshot = session.Snapshot;
            LobbyPlayer local = connection != null && connection.LocalClientId.HasValue
                ? snapshot?.Find(connection.LocalClientId.Value) : null;
            LobbyPlayer other = local == null ? null : local.playerNumber == 1 ? snapshot.p2 : snapshot.p1;
            bool multiparty = session.MaximumParticipants > 2;
            var roster = snapshot?.OrderedPlayers ?? Array.Empty<LobbyPlayer>();
            bool transportConnected = session.Connected;
            bool connected = session.InitialStateReady;
            bool canConnect = connection != null && connection.CanStart;
            bool browsing = session.Discovery != null && session.Discovery.IsBrowsing;
            bool started = snapshot != null && snapshot.phase == LobbyProtocol.Playing && snapshot.start != null;
            bool closed = snapshot != null && snapshot.phase == LobbyProtocol.Closed;
            ulong localId = connection != null && connection.LocalClientId.HasValue ? connection.LocalClientId.Value : ulong.MaxValue;
            bool checkedInitial = local != null && local.initialStateReceived && session.HostConfig != null;
            string error = !string.IsNullOrWhiteSpace(session.Error) ? FriendlyStagedError(session.Error, session.FailureStage) : actionError;
            if (string.IsNullOrWhiteSpace(error) && closed) error = FriendlyError(snapshot.closeReason);

            var view = new LobbyUiState
            {
                Connected = connected,
                Multiparty = multiparty,
                PlayerRoster = string.Join("\n", roster.Select(p => "P" + p.playerNumber + (p.clientId == localId ? "  (YOU)" : "")
                    + "  /  " + (!p.initialStateReceived ? "CHECKING SETTINGS" : p.ready ? "READY" : "NOT READY"))),
                Browsing = browsing,
                Phase = started ? "START CONFIRMED" : closed ? "ROOM ENDED" : connected ? "LOBBY"
                    : transportConnected ? "CHECKING ROOM"
                    : connection != null && (connection.JoinInProgress || connection.State == DirectConnectionState.StartingHost)
                        ? ConnectionPhase(connection.ConnectionStage) : browsing ? "SEARCHING" : "OFFLINE",
                Status = started ? "All players agreed to start. Lobby check complete."
                    : transportConnected && !connected ? snapshot == null ? "Host approved. Receiving room settings…" : "Confirming room settings with the host…"
                    : session.Status,
                Error = error,
                RoomTitle = session.RoomName,
                LocalPlayer = PlayerName(local?.playerNumber ?? 0),
                Role = connected ? session.IsHost ? "HOST" : "CLIENT" : "OFFLINE",
                LeftPlayer = PlayerName(snapshot?.LeftPlayerNumber(localId) ?? 0),
                RightPlayer = PlayerName(snapshot?.RightPlayerNumber(localId) ?? 0),
                Participants = (snapshot?.ParticipantCount ?? 0) + " / " + session.MaximumParticipants,
                LocalReady = session.LocalReady,
                RemoteReady = roster.Length > 1 && roster.Where(p => p.clientId != localId).All(p => p.ready),
                Compatibility = checkedInitial ? roster.Any(p => !p.initialStateReceived)
                    ? "Your settings are confirmed. Waiting for other players." : "Room settings confirmed."
                    : transportConnected ? "Checking room settings before Ready…" : "Join a room to confirm its settings.",
                StartStatus = started ? "START CONFIRMED\nAll players are prepared."
                    : closed ? "This room has ended. Leave and connect again."
                    : session.HasPending ? "Waiting for confirmation…"
                    : !checkedInitial ? "Ready unlocks after room settings are confirmed."
                    : (snapshot?.ParticipantCount ?? 0) < 2 ? "Waiting for at least one other player."
                    : snapshot.canStart ? session.IsHost ? "All ready. You can start." : "All ready. Waiting for the host."
                    : "All connected players must be ready to start.",
                CanCreate = canConnect,
                CanBrowse = canConnect && !browsing,
                CanRefresh = canConnect && browsing,
                CanCancelBrowse = browsing,
                CanJoinDirect = canConnect,
                CanReady = connected && session.CanReady,
                CanStart = connected && session.CanStart,
                CanLeave = transportConnected || (connection != null && (connection.JoinInProgress || connection.State == DirectConnectionState.Connecting ||
                    connection.State == DirectConnectionState.StartingHost))
            };
            hud.SetState(view);
            hud.SetRooms(RoomRows(canConnect));
        }

        private IReadOnlyList<LobbyRoomRow> RoomRows(bool canConnect)
        {
            if (session.Discovery == null) return Array.Empty<LobbyRoomRow>();
            // The catalog combines interface-specific routes into one immutable candidate set per room.
            return session.Rooms.Where(room => room != null && !string.IsNullOrEmpty(room.RoomId))
                .GroupBy(room => room.RoomId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(room => room.LastSeenSeconds)
                    .ThenBy(room => room.DiscoveryKey, StringComparer.Ordinal).First())
                .OrderBy(room => room.Name, StringComparer.Ordinal).ThenBy(room => room.RoomId, StringComparer.Ordinal)
                .Select(room =>
                {
                    string problem = session.RoomProblem(room);
                    return new LobbyRoomRow
                    {
                        Id = room.RoomId,
                        Title = room.Name,
                        Address = "Same Wi-Fi or hotspot",
                        Status = string.IsNullOrEmpty(problem) ? room.Participants + " / " + session.MaximumParticipants + " players · Available" : RoomStatus(problem, session.MaximumParticipants),
                        CanJoin = canConnect && string.IsNullOrEmpty(problem)
                    };
                }).ToArray();
        }

        private void CreateRoom() => Execute(() => session.CreateRoom(hud.RoomName, hud.Port), "The room could not open. Please try again.");
        private void Browse() => Execute(session.Browse, "Room search could not start. Try Refresh or Direct IP.");
        private void JoinRoom(string roomId) => Execute(() => session.JoinRoom(roomId), "This room is no longer available. Refresh the list.");
        private void JoinDirect() => Execute(() => session.JoinDirect(hud.HostAddress, hud.Port), "Check the host address and port, then try again.");
        private void ToggleReady() => Execute(session.ToggleReady, "Ready is not available yet. Wait for room settings to be confirmed.");
        private void StartMatch() => Execute(session.StartMatch, "Only the host can start after all connected players are ready.");
        private void RefreshRooms() { actionError = string.Empty; session.RefreshRooms(); RefreshView(); }
        private void CancelBrowse() { actionError = string.Empty; session.CancelBrowse(); RefreshView(); }
        private void Leave() { actionError = string.Empty; session.Leave(); RefreshView(); }
        private void Execute(Func<bool> action, string fallback)
        {
            actionError = string.Empty;
            if (!action() && string.IsNullOrWhiteSpace(session.Error)) actionError = fallback;
            RefreshView();
        }

        private static string PlayerName(int number) => number >= 1 && number <= LobbyProtocol.MaximumCapacity ? "P" + number : "—";
        private static string ConnectionPhase(string stage)
        {
            switch (stage)
            {
                case "HostApproval": return "HOST CHECK";
                case "Retrying": return "TRYING NEXT";
                case "HostStarting": return "OPENING ROOM";
                default: return "CONNECTING";
            }
        }
        private static string RoomStatus(string reason, int capacity)
        {
            switch (reason)
            {
                case "ROOM_FULL": return capacity + " / " + capacity + " players · Room full";
                case "PROTOCOL_MISMATCH":
                case "BUILD_MISMATCH": return "Different app version";
                case "BATTLE_IN_PROGRESS": return "Already started";
                case "STALE_ROOM": return "No longer available · Refresh";
                default: return "Unavailable";
            }
        }

        private static string FriendlyStagedError(string reason, string stage)
        {
            // Explicit admission/configuration reasons are more useful than a generic stage.
            // Keep native permission denial evidence in the existing focused guidance path.
            switch (reason)
            {
                case "ROOM_FULL": case "C6_T02_ROOM_FULL": case "BUILD_MISMATCH": case "PROTOCOL_MISMATCH":
                case "CONFIG_MISMATCH": case "UNSUPPORTED_HOST_CONFIG": case "STALE_ROOM":
                case "ROOM_CLOSED": case "HOST_CLOSED_ROOM": case "BATTLE_IN_PROGRESS":
                case "DUPLICATE_PARTICIPANT": case "APPLICATION_BACKGROUNDED":
                case "INITIAL_STATE_TIMEOUT": case "INVALID_ADDRESS_OR_PORT": case "INVALID_ADDRESS": case "INVALID_PORT":
                    return FriendlyError(reason);
            }
            if (stage == "DISCOVERY") return FriendlyError(reason);
            if (stage == "TRANSPORT")
                return "Could not reach the host. Keep both apps open on the same Wi-Fi or hotspot, then try again.";
            if (stage == "APPROVAL")
                return "The host did not approve entry. Refresh the rooms and check that both apps use the same version.";
            if (stage == "INITIAL_STATE")
                return "Connected to the host, but room settings were not confirmed. Leave and connect again.";
            if (stage == "SESSION")
                return "The room connection was lost. Check the Wi-Fi or hotspot, then connect again.";
            return FriendlyError(reason);
        }

        private static string FriendlyError(string reason)
        {
            switch (reason)
            {
                case "APPLICATION_BACKGROUNDED": return "The app left the foreground. Find or create a new room to continue.";
                case "ROOM_FULL":
                case "C6_T02_ROOM_FULL": return "This room is full. Ask the host to open a new room.";
                case "BUILD_MISMATCH":
                case "PROTOCOL_MISMATCH": return "The app versions differ. Use the same version on all devices.";
                case "CONFIG_MISMATCH":
                case "UNSUPPORTED_HOST_CONFIG": return "Room settings are incompatible. Use the same app version and reconnect.";
                case "STALE_ROOM": return "This room is no longer available. Refresh the room list.";
                case "ROOM_CLOSED":
                case "HOST_CLOSED_ROOM": return "The host closed the room. Find or create another room.";
                case "PARTICIPANT_LEFT":
                case "ROOM_ENDED": return "A player left the room. Connect again to continue.";
                case "BATTLE_IN_PROGRESS": return "This room has already started. Wait for a new room.";
                case "HOST_ONLY": return "Only the host can start the room.";
                case "BOTH_READY_REQUIRED": return "Both players must be ready before the host can start.";
                case "ALL_READY_REQUIRED": return "All connected players must confirm Ready before the host starts.";
                case "ROSTER_CHANGED_READY_AGAIN": return "Players changed. Confirm Ready again before starting.";
                case "DUPLICATE_PARTICIPANT": return "This player is already in the room. Leave before joining again.";
                case "INITIAL_STATE_REQUIRED": return "Wait for room settings to finish checking, then tap Ready.";
                case "INITIAL_STATE_TIMEOUT": return "Room settings did not arrive. Check the Wi-Fi and connect again.";
                case "REQUEST_CONFIRMATION_TIMEOUT":
                case "SEND_FAILED": return "The host did not respond. Check the Wi-Fi and connect again.";
                case "INVALID_ROOM_NAME": return "Enter a room name of 32 characters or fewer, without line breaks.";
                case "INVALID_PORT": return "Enter a port number from 1 to 65535.";
                case "INVALID_ADDRESS":
                case "INVALID_ADDRESS_OR_PORT": return "Enter the host IPv4 or IPv6 address and a port from 1 to 65535.";
                case "HOST_FAILED": return "The room could not open. Check the Wi-Fi and try again.";
                case "CONNECTION_BUSY": return "A connection is already in progress. Please wait.";
                case "CONNECTION_MANAGER_CONFLICT": return "Another session is still open. End it before connecting again.";
                case "USER_CANCELLED": return "Connection cancelled. Find or create a room when ready.";
                case "STALE_REVISION": return "The room changed before confirmation. Check its status and try again.";
                case "STALE_SESSION": return "That room has ended. Connect again.";
                case "READY_UNCHANGED": return "Your Ready state is already confirmed.";
                case "DUPLICATE_REQUEST": return "This action was already received. Check the current room status.";
            }
            if (string.IsNullOrWhiteSpace(reason)) return string.Empty;
            if (reason.IndexOf("PolicyDenied", StringComparison.OrdinalIgnoreCase) >= 0 || reason.Contains("-65570"))
                return "Allow Local Network access in Settings, then try Find Rooms again.";
            if (reason.IndexOf("Bonjour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("DNSService", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Room search is unavailable. Check the Wi-Fi, then retry or use Direct IP.";
            // The underlying session retains its exact diagnostic reason. Keep the user-facing recovery action readable.
            return "The connection ended. Check that all devices use the same Wi-Fi, then connect again.";
        }
    }
}
