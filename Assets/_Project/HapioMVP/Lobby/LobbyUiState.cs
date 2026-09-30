using System;
using C6.Prototype.Presentation;

namespace C6.Prototype.Lobby
{
    /// <summary>A display-only row. Joining and freshness checks remain the session's responsibility.</summary>
    [Serializable]
    public sealed class LobbyRoomRow
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Address = string.Empty;
        public string Status = string.Empty;
        public bool CanJoin;
    }

    [Serializable]
    public sealed class LobbyElementOption
    {
        public OrbElement Element;
        public string OccupiedBy = string.Empty;
        public bool Selected;
        public bool CanSelect;
    }

    /// <summary>Passive presentation values. None of these fields authorize a network operation.</summary>
    [Serializable]
    public sealed class LobbyUiState
    {
        public string Phase = "OFFLINE";
        public string Status = "Create a room or find a friend nearby.";
        public string Error = string.Empty;
        public string RoomTitle = string.Empty;
        public string LocalPlayer = "—";
        public string Role = "OFFLINE";
        public string LeftPlayer = "—";
        public string RightPlayer = "—";
        public string Participants = "0 / 2";
        public string Compatibility = "Waiting for connection";
        public string StartStatus = string.Empty;
        public bool Multiparty;
        public bool ElementSelectionEnabled;
        public OrbElement SelectedElement;
        public bool SelectionPending;
        public bool CanClearElement;
        public LobbyElementOption[] ElementOptions = Array.Empty<LobbyElementOption>();
        public string PlayerRoster = string.Empty;
        public bool Connected;
        public bool Browsing;
        public bool LocalReady;
        public bool RemoteReady;
        public bool CanCreate = true;
        public bool CanBrowse = true;
        public bool CanRefresh = true;
        public bool CanCancelBrowse;
        public bool CanJoinDirect = true;
        public bool CanReady;
        public bool CanStart;
        public bool CanLeave;
    }
}
