using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #501: a Hapio lobby guest that drops (screen lock, app in the background, Wi-Fi) comes back to the same room and
    /// P number. The guest sends a random room ticket after its connection token. The Host holds a dropped guest's
    /// place and P for <see cref="LobbySeatHoldSeconds"/> and gives them back to the same ticket. A guest that leaves
    /// says so first, so its P is free at once. Battles are unchanged (#502): a drop there keeps the existing Game Over
    /// or lobby return. Lab rooms never send a ticket, so their joins and lobby are as before.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>How long the Host keeps a dropped lobby guest's place and P, counted from its own disconnect.</summary>
        public const double LobbySeatHoldSeconds = 90d;
        /// <summary>How long a dropped lobby guest keeps trying to get back in before it returns to the room list.</summary>
        public const double LobbyRejoinBudgetSeconds = 30d;
        private const int RejoinTicketLength = 32;
        private const byte RejoinTicketSeparator = (byte)'|';

        private sealed class LobbySeatHold
        {
            public string ticket;
            public ulong playerId;
            public int slot;
            public double until;
        }

        // Host: the room's tickets, held places and the P each ticket last had.
        private double lobbySeatHoldSeconds = LobbySeatHoldSeconds;
        private readonly Dictionary<ulong, string> memberTickets = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, string> approvedTickets = new Dictionary<ulong, string>();
        private readonly List<LobbySeatHold> lobbySeatHolds = new List<LobbySeatHold>();
        private readonly HashSet<ulong> lobbyLeavers = new HashSet<ulong>();
        private readonly HashSet<ulong> replacedMembers = new HashSet<ulong>();
        private readonly Dictionary<string, int> rememberedSlots = new Dictionary<string, int>();
        // Guest: the lobby's held seats, and this device's ticket and rejoin state, which outlive ResetLocal.
        private readonly HashSet<ulong> heldSeatOwners = new HashSet<ulong>();
        private readonly List<string> lobbyRejoinRoutes = new List<string>();
        private string rejoinTicket;
        private string rejoinTicketRoom;
        /// <summary>The Host let this ticket into its lobby, so it may be holding the place now.</summary>
        private bool rejoinTicketAdmitted;
        private bool hostClosedLobby;
        private bool lobbyRejoinActive;
        private double lobbyRejoinUntil;
        private double lobbyRejoinAt;
        private int lobbyRejoinAttempt;
        private ushort lobbyRejoinPort;
        private string lobbyRejoinRoomName = string.Empty;
        private double pausedAtWallClock = double.NaN;

        /// <summary>GameSession sets this while the device shows a room lobby. Only then does a lost guest rejoin.</summary>
        public bool LobbyRejoinAllowed { get; set; }
        /// <summary>A lost lobby guest is getting back into the same room. Counts as joining.</summary>
        public bool IsRejoiningLobby => lobbyRejoinActive;
        /// <summary>Places held for dropped lobby guests. They count toward the room size but not <see cref="ParticipantCount"/>.</summary>
        public int HeldSeatCount => IsHost ? lobbySeatHolds.Count : heldSeatOwners.Count;
        private int LobbyPlaceCount => order.Count + lobbySeatHolds.Count;
        /// <summary>The Host's lobby is open: no battle, no run and no solo.</summary>
        private bool LobbyRosterOpen => gameFlowEnabled && model == null && latest == null && !gameRosterLocked
            && !gameBattleFrozen && !developerSolo && !tutorialSeats;

        /// <summary>The P number belongs to a dropped lobby guest the Host is waiting for.</summary>
        public bool IsSlotHeld(int slotNumber)
        {
            ulong owner = GetSlotOwner(slotNumber);
            if (owner == ulong.MaxValue) return false;
            if (!IsHost) return heldSeatOwners.Contains(owner);
            foreach (LobbySeatHold hold in lobbySeatHolds) if (hold.playerId == owner) return true;
            return false;
        }

        private byte[] ConnectionPayload(bool client)
        {
            byte[] token = ActiveConnectionToken;
            if (!client || !gameFlowEnabled || string.IsNullOrEmpty(rejoinTicket)) return (byte[])token.Clone();
            byte[] ticket = Encoding.ASCII.GetBytes(rejoinTicket);
            var payload = new byte[token.Length + 1 + ticket.Length];
            Buffer.BlockCopy(token, 0, payload, 0, token.Length);
            payload[token.Length] = RejoinTicketSeparator;
            Buffer.BlockCopy(ticket, 0, payload, token.Length + 1, ticket.Length);
            return payload;
        }

        /// <summary>
        /// The exact token, or (game rooms only) the token, '|' and a 32-digit lowercase hexadecimal room ticket.
        /// </summary>
        internal static bool TryReadConnectionPayload(byte[] payload, byte[] token, bool game, out string ticket)
        {
            ticket = null;
            if (payload == null || token == null) return false;
            if (payload.Length == token.Length) return payload.SequenceEqual(token);
            if (!game || payload.Length != token.Length + 1 + RejoinTicketLength
                || payload[token.Length] != RejoinTicketSeparator) return false;
            for (int i = 0; i < token.Length; i++) if (payload[i] != token[i]) return false;
            for (int i = token.Length + 1; i < payload.Length; i++)
            {
                byte c = payload[i];
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f')) return false;
            }
            ticket = Encoding.ASCII.GetString(payload, token.Length + 1, RejoinTicketLength);
            return true;
        }

        /// <summary>A new ticket for a new room. Joining the same room again keeps it, so a held or remembered P returns.</summary>
        private void PrepareRejoinTicket(string room)
        {
            if (!gameFlowEnabled)
            {
                rejoinTicket = null;
                rejoinTicketRoom = null;
                rejoinTicketAdmitted = false;
                return;
            }
            if (!string.IsNullOrEmpty(rejoinTicket)
                && string.Equals(rejoinTicketRoom, room, StringComparison.OrdinalIgnoreCase)) return;
            rejoinTicket = Guid.NewGuid().ToString("N");
            rejoinTicketRoom = room;
            rejoinTicketAdmitted = false;
        }

        /// <summary>
        /// This device has a ticket for the room, so the Host may be holding its place: a full card is worth trying, and
        /// the Host's approval decides (#501).
        /// </summary>
        public bool HoldsRoomTicket(string roomKey) => gameFlowEnabled && rejoinTicketAdmitted && !string.IsNullOrEmpty(rejoinTicket)
            && !string.IsNullOrEmpty(roomKey) && string.Equals(rejoinTicketRoom, roomKey, StringComparison.OrdinalIgnoreCase);

        /// <summary>A listed room this device may try: open, or full while possibly holding this device's place.</summary>
        internal bool RoomOpenToThisDevice(LabDiscoveredRoom room) => room != null && !room.Playing && !room.WaitingForHost
            && (!room.IsFull || HoldsRoomTicket(room.Key));

        /// <summary>The Host admitted this device's ticket into its lobby (#501).</summary>
        private void MarkRejoinTicketAdmitted()
        {
            if (gameFlowEnabled && !string.IsNullOrEmpty(rejoinTicket)) rejoinTicketAdmitted = true;
        }

        /// <summary>The held or still-connected place this ticket would take over.</summary>
        private int ReclaimablePlaces(string ticket)
        {
            if (ticket == null || !LobbyRosterOpen) return 0;
            foreach (LobbySeatHold hold in lobbySeatHolds) if (hold.ticket == ticket) return 1;
            foreach (ulong member in order)
                if (memberTickets.TryGetValue(member, out string known) && known == ticket) return 1;
            return 0;
        }

        /// <summary>
        /// Other approved connections still on their way in that will take a new place. One that brings back a held or
        /// still-connected ticket takes its own place, already counted, so two returns in one frame both fit.
        /// </summary>
        private int PendingNewPlaces(ulong requester)
        {
            int count = 0;
            foreach (ulong pending in pendingApprovals.Keys)
            {
                if (pending == requester) continue;
                string ticket = approvedTickets.TryGetValue(pending, out string known) ? known : null;
                if (ReclaimablePlaces(ticket) == 0) count++;
            }
            return count;
        }

        private string TakeApprovedTicket(ulong id)
        {
            if (!approvedTickets.TryGetValue(id, out string ticket)) return null;
            approvedTickets.Remove(id);
            return ticket;
        }

        /// <summary>
        /// The returning ticket's place: drop its hold, or replace its old connection the Host has not timed out yet.
        /// Returns the P to give back (0 if none). The new connection is added afterwards.
        /// </summary>
        private int ReclaimLobbyPlace(ulong id, string ticket)
        {
            if (ticket == null || !LobbyRosterOpen) return 0;
            int slot = 0;
            bool found = false;
            for (int i = lobbySeatHolds.Count - 1; i >= 0; i--)
            {
                LobbySeatHold hold = lobbySeatHolds[i];
                if (hold.ticket != ticket) continue;
                found = true;
                lobbySeatHolds.RemoveAt(i);
                playerSlots?.Release(hold.playerId);
                if (hold.slot > 0) slot = hold.slot;
                Debug.Log("C6_LAB_LOBBY_REJOIN host=reclaimed id=" + id + " p=" + hold.slot);
            }
            foreach (ulong stale in order.Where(member => member != id && member != NetworkManager.ServerClientId
                         && memberTickets.TryGetValue(member, out string known) && known == ticket).ToArray())
            {
                found = true;
                int staleSlot = playerSlots?.GetSlot(stale) ?? 0;
                if (staleSlot > 0) slot = staleSlot;
                ForgetLobbyMember(stale);
                replacedMembers.Add(stale);
                Debug.Log("C6_LAB_LOBBY_REJOIN host=replaced old=" + stale + " new=" + id + " p=" + staleSlot);
                DisconnectReplacedMember(stale);
            }
            // A held or replaced place brings back exactly the P it had (none if its choice was cleared); only a return
            // after its hold ended falls back to the P it left with. Either way the memory is used up.
            if (!found && rememberedSlots.TryGetValue(ticket, out int remembered)) slot = remembered;
            rememberedSlots.Remove(ticket);
            return slot;
        }

        private void DisconnectReplacedMember(ulong id)
        {
            try { manager.DisconnectClient(id); }
            catch (Exception e) { Debug.LogWarning("C6_LAB_LOBBY_REJOIN replace_failed=" + e.GetType().Name); }
        }

        private void RestoreLobbySlot(ulong id, int slot)
        {
            if (slot <= 0 || playerSlots == null || playerSlots.GetOwner(slot) != ulong.MaxValue) return;
            if (playerSlots.TrySelect(id, slot, out _)) Debug.Log("C6_LAB_LOBBY_REJOIN host=restored id=" + id + " p=" + slot);
        }

        /// <summary>The same cleanup as a disconnect, for a lobby member the Host removes itself.</summary>
        private void ForgetLobbyMember(ulong id)
        {
            order.Remove(id);
            bulkSenders.Remove(id); peerPaintPublishers.Remove(id); peerSharedRounds.Remove(id); paintCheckpointThrough.Remove(id);
            boardSequences.Remove(id);
            playerSlots?.Release(id);
            slotRequestIds.Remove(id);
            configAcknowledgements.Remove(id);
            memberTickets.Remove(id);
            lobbyLeavers.Remove(id);
        }

        /// <summary>
        /// A lobby member with a ticket that did not say goodbye keeps its place and P (still owned by its old id). Its
        /// P at departure is also remembered, so a return after the hold ended gets it back if it is still free.
        /// 나가기 gives both up, and so does a drop during a run (#502), which is never held.
        /// </summary>
        private bool HoldLobbyPlace(ulong id, int slot, bool lobbyDrop)
        {
            if (!memberTickets.TryGetValue(id, out string ticket)) return false;
            if (!lobbyDrop)
            {
                rememberedSlots.Remove(ticket);
                return false;
            }
            if (lobbyLeavers.Contains(id))
            {
                rememberedSlots.Remove(ticket);
                Debug.Log("C6_LAB_LOBBY_HOLD result=left id=" + id + " p=" + slot);
                return false;
            }
            if (slot > 0) rememberedSlots[ticket] = slot;
            else rememberedSlots.Remove(ticket);
            lobbySeatHolds.Add(new LobbySeatHold { ticket = ticket, playerId = id, slot = slot,
                until = Time.realtimeSinceStartupAsDouble + lobbySeatHoldSeconds });
            Debug.Log("C6_LAB_LOBBY_HOLD result=held id=" + id + " p=" + slot
                + " seconds=" + lobbySeatHoldSeconds.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        private void ExpireLobbySeatHolds(double now)
        {
            if (lobbySeatHolds.Count == 0 || !IsHost) return;
            bool changed = false;
            for (int i = lobbySeatHolds.Count - 1; i >= 0; i--)
            {
                LobbySeatHold hold = lobbySeatHolds[i];
                if (now < hold.until) continue;
                lobbySeatHolds.RemoveAt(i);
                playerSlots?.Release(hold.playerId);
                changed = true;
                Debug.Log("C6_LAB_LOBBY_HOLD result=expired id=" + hold.playerId + " p=" + hold.slot);
            }
            if (changed && model == null && !gameBattleFrozen) BroadcastLobby();
        }

        private ulong[] LobbySeats()
        {
            if (lobbySeatHolds.Count == 0) return order.ToArray();
            var seats = new List<ulong>(order);
            foreach (LobbySeatHold hold in lobbySeatHolds) seats.Add(hold.playerId);
            return seats.ToArray();
        }

        private string[] HeldLobbySeats() => lobbySeatHolds.Count == 0 ? null
            : lobbySeatHolds.Select(hold => hold.playerId.ToString(CultureInfo.InvariantCulture)).ToArray();

        /// <summary>A Guest's lobby: held owners are seated (they own their P) but are not connected participants.</summary>
        private void ReadHeldLobbySeats(LabNetworkPacket packet)
        {
            heldSeatOwners.Clear();
            if (!gameFlowEnabled || packet.ids == null) return;
            foreach (string text in packet.ids)
                if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong held)
                    && held != NetworkManager.ServerClientId && Array.IndexOf(packet.seats, held) >= 0)
                    heldSeatOwners.Add(held);
        }

        /// <summary>Best effort, before NGO shuts down: a leaving guest frees its P at once, a closing Host stops rejoins.</summary>
        private void SayLobbyGoodbye()
        {
            if (!gameFlowEnabled || !IsConnected) return;
            if (IsHost)
            {
                var closed = new LabNetworkPacket { type = "lobby_closed" };
                foreach (ulong id in order) if (id != LocalPlayerId) Send(id, closed);
            }
            else if (Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "lobby_leave" }))
            {
                // A guest that said goodbye gave its place up; a later join to this room is a new player.
                rejoinTicket = null;
                rejoinTicketRoom = null;
                rejoinTicketAdmitted = false;
            }
        }

        /// <summary>The room's held seats and tickets. The guest's own ticket and rejoin state stay.</summary>
        private void ResetLobbyRoom()
        {
            memberTickets.Clear();
            approvedTickets.Clear();
            lobbySeatHolds.Clear();
            lobbyLeavers.Clear();
            replacedMembers.Clear();
            rememberedSlots.Clear();
            heldSeatOwners.Clear();
            hostClosedLobby = false;
        }

        /// <summary>
        /// A lobby guest lost its Host. Remember the routes that worked and keep <see cref="IsJoiningRoom"/> true, so
        /// GameSession stays in the Lobby while this device gets back in. Forced is a resume after a long pause, while
        /// the transport still believes it is connected.
        /// </summary>
        private bool TryBeginLobbyRejoin(bool forced)
        {
            if (!gameFlowEnabled || IsHost || !LobbyRejoinAllowed || hostClosedLobby || lobbyRejoinActive
                || developerSolo || tutorialSeats || model != null || latest != null || gameRosterLocked
                || string.IsNullOrEmpty(rejoinTicket) || joinCandidates.Count == 0) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            lobbyRejoinRoutes.Clear();
            string worked = joinCandidateIndex >= 0 && joinCandidateIndex < joinCandidates.Count
                ? joinCandidates[joinCandidateIndex] : null;
            if (worked != null) lobbyRejoinRoutes.Add(worked);
            foreach (string route in joinCandidates)
                if (route != worked && !lobbyRejoinRoutes.Contains(route) && lobbyRejoinRoutes.Count < 8)
                    lobbyRejoinRoutes.Add(route);
            int slot = LocalSlotNumber;
            lobbyRejoinPort = port;
            lobbyRejoinRoomName = connectedRoomName ?? string.Empty;
            lobbyRejoinActive = true;
            lobbyRejoinUntil = now + LobbyRejoinBudgetSeconds;
            lobbyRejoinAt = now;
            lobbyRejoinAttempt = 0;
            // A forced teardown's own disconnect callback must not count as a second loss.
            clientSessionConnected = false;
            UnregisterHandler();
            RequestJoinShutdown();
            SetStatus("Lost the room; rejoining...");
            Debug.LogWarning("C6_LAB_LOBBY_REJOIN result=start forced=" + (forced ? "1" : "0") + " p=" + slot
                + " routes=" + lobbyRejoinRoutes.Count);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Starts the next attempt once NGO has shut down, until the budget runs out.</summary>
        private void UpdateLobbyRejoin(double now)
        {
            if (!lobbyRejoinActive || joinInProgress || joinRetryPending || joinFinishPending) return;
            if (manager == null || manager.IsListening || manager.ShutdownInProgress) return;
            if (now >= lobbyRejoinUntil)
            {
                EndLobbyRejoin("gave_up");
                return;
            }
            if (now < lobbyRejoinAt) return;
            ResetLocal();
            port = lobbyRejoinPort;
            joinCandidates.Clear();
            joinCandidates.AddRange(lobbyRejoinRoutes);
            joinCandidateIndex = 0;
            joinStartedAt = now;
            joinInProgress = true;
            joinFinishPending = false;
            joinCanceling = false;
            lobbyRejoinAttempt++;
            Debug.Log("C6_LAB_LOBBY_REJOIN result=attempt n=" + lobbyRejoinAttempt);
            StartJoinAttempt();
        }

        /// <summary>Every route failed this time: wait 1, 2, 4… seconds and try them again.</summary>
        private void ScheduleLobbyRejoinRetry()
        {
            joinInProgress = false;
            joinRetryPending = false;
            joinAttemptAt = 0d;
            UnregisterHandler();
            RequestJoinShutdown();
            double delay = LabDiscoveryBackoff.Delay(Math.Max(0, lobbyRejoinAttempt - 1));
            lobbyRejoinAt = Time.realtimeSinceStartupAsDouble + delay;
            Debug.Log("C6_LAB_LOBBY_REJOIN result=wait seconds=" + delay.ToString(CultureInfo.InvariantCulture));
        }

        private void EndLobbyRejoin(string result)
        {
            if (!lobbyRejoinActive) return;
            lobbyRejoinActive = false;
            lobbyRejoinRoutes.Clear();
            lobbyRejoinRoomName = string.Empty;
            string line = "C6_LAB_LOBBY_REJOIN result=" + result + " attempts=" + lobbyRejoinAttempt;
            if (result == "connected") Debug.Log(line);
            else Debug.LogWarning(line);
            Changed?.Invoke();
        }

        private static double WallClockSeconds => DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;

        /// <summary>
        /// A phone back from a pause at least as long as the transport timeout: the Host has dropped this guest, but
        /// the guest's own transport may not notice for another timeout. Rejoin now.
        /// </summary>
        internal bool ResumeLobbyAfterPause(double pausedSeconds)
        {
            if (!gameFlowEnabled || IsHost || !IsConnected || IsJoiningRoom || transport == null) return false;
            if (pausedSeconds * 1000d < transport.DisconnectTimeoutMS) return false;
            return TryBeginLobbyRejoin(true);
        }
    }
}
