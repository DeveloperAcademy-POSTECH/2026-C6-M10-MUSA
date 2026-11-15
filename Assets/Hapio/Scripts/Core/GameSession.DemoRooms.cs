using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private enum DemoEntryPhase { None, Searching, AwaitingCreationConfirmation, Creating, Joining }
        private DemoEntryPhase demoEntryPhase;
        private GamePendingRoomHandle demoCreationHandle;
        private double demoEntryDeadline, demoSearchUntil, demoCreationSettlesAt = double.NaN;
        private bool demoCreationAuthorized, demoWaitingForWinner;
        private string demoWinningRoomKey;
        private const double DemoEntryTimeout = 35d, DemoSearchSeconds = 2d, DemoCreationSettleSeconds = 2d;

        public bool IsDemoEntryPending => demoEntryPhase != DemoEntryPhase.None;
        public bool IsDemoCreationConfirmationPending => demoEntryPhase == DemoEntryPhase.AwaitingCreationConfirmation;
        public string DemoCreationConfirmationMessage => IsDemoCreationConfirmationPending
            ? SelectedDemoGroupLabel + "의 방이 아직 발견되지 않았소. 이 기기에서 방을 만들겠소?" : string.Empty;
        public string DemoEntryNotice { get; private set; } = string.Empty;

        /// <summary>One staff card owns one bounded browse/create/join operation. No gameplay begins here.</summary>
        public bool SelectDemoStage(int stage) => BeginDemoStageEntry(stage, false);

        // A deliberate interrupted-run retry already authorizes recreating its own group if its Host is gone.
        private bool SelectDemoStageForStaffRetry(int stage) => BeginDemoStageEntry(stage, true);

        private bool BeginDemoStageEntry(int stage, bool allowCreation)
        {
            if (!LabGameBuildVariant.IsStaffStageDemo || IsDemoEntryPending || IsStaffRetryPending || goingHome || IsCreatingRoom
                || !SetSelectedDemoStage(stage) || !network.SetDemoStageIdentity(stage, SelectedDemoGroup)) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            demoEntryDeadline = now + DemoEntryTimeout;
            demoCreationAuthorized = allowCreation;
            demoWaitingForWinner = false;
            demoWinningRoomKey = null;
            demoCreationSettlesAt = double.NaN;
            roomBrowserVisible = false;
            if (!BeginDemoSearch(now))
            {
                FinishDemoEntry("방을 찾지 못했소. 네트워크 권한과 같은 Wi-Fi 연결을 확인해주시오.");
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Confirmation is permission to recheck, not permission to ignore a room another device just opened.</summary>
        public bool ConfirmDemoRoomCreation()
        {
            if (!IsDemoCreationConfirmationPending || goingHome || state.phase != GameFlowPhase.Home
                || !network.CanChooseConnection || network.DemoStageIndex != SelectedDemoStage
                || network.DemoGroupId != SelectedDemoGroup) return false;
            demoCreationAuthorized = true;
            demoWaitingForWinner = false;
            demoWinningRoomKey = null;
            double now = Time.realtimeSinceStartupAsDouble;
            demoEntryDeadline = now + DemoEntryTimeout;
            // Retire the old browse; its brief display cache never supplies a join route or authorizes a Host.
            if (!BeginDemoSearch(now, true))
            {
                FinishDemoEntry("방을 다시 확인하지 못했소. 네트워크 권한을 확인한 뒤 다시 시도해주시오.");
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        public void CancelDemoRoomCreation()
        {
            if (IsDemoCreationConfirmationPending) CancelDemoEntry();
        }

        private bool BeginDemoSearch(double now, bool restart = false)
        {
            demoEntryPhase = DemoEntryPhase.Searching;
            demoSearchUntil = now + DemoSearchSeconds;
            DemoEntryNotice = SelectedDemoGroupLabel + "의 방을 찾고 있소...";
            return (!restart && network.IsSearchingRooms) || network.StartRoomSearch();
        }

        /// <summary>Called after pending advertisements tick, before normal room admission is observed.</summary>
        private void UpdateDemoEntry()
        {
            if (!IsDemoEntryPending || !LabGameBuildVariant.IsStaffStageDemo) return;
            if (network.IsConnected)
            {
                // EnterPendingRoom/JoinRoom retain the selected stage and the ordinary lobby follows.
                FinishDemoEntry(string.Empty);
                return;
            }
            if (goingHome || state.phase != GameFlowPhase.Home) { CancelDemoEntry(); return; }
            // Asking a person to choose is not a timed network operation. Confirm starts a fresh bounded operation.
            if (IsDemoCreationConfirmationPending) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= demoEntryDeadline)
            {
                CancelDemoEntry();
                FinishDemoEntry("방 입장 시간이 초과됐소. 네트워크 상태를 확인한 뒤 다시 골라주시오.");
                return;
            }
            if (demoEntryPhase == DemoEntryPhase.Joining)
            {
                if (!network.IsJoiningRoom && network.CanChooseConnection)
                    FinishDemoEntry("방에 입장하지 못했소. 방 상태를 확인하고 다시 골라주시오.");
                return;
            }
            if (demoEntryPhase == DemoEntryPhase.Creating)
            {
                if (pendingRooms.TryGet(demoCreationHandle, out GamePendingRoomInfo pending))
                {
                    if (!pending.Ready) { demoCreationSettlesAt = double.NaN; return; }
                    if (network.RoomSearchFailed || !network.IsSearchingRooms)
                    { FinishDemoEntry("방의 중복 여부를 확인하지 못했소. 연결 상태를 확인한 뒤 다시 시도해주시오."); return; }
                    if (double.IsNaN(demoCreationSettlesAt)) demoCreationSettlesAt = now + DemoCreationSettleSeconds;
                    LabDiscoveredRoom winner = DemoCreationWinner(network.DiscoveredRooms, SelectedDemoStage, SelectedDemoGroup, pending.Key);
                    if (winner != null)
                    {
                        // Release only our unentered advertisement. A losing creator follows the same elected room
                        // until this operation ends; a temporarily missing winner must not cause another creation.
                        pendingRooms.Cancel(demoCreationHandle);
                        demoCreationHandle = null;
                        demoCreationAuthorized = false;
                        demoWaitingForWinner = true;
                        demoWinningRoomKey = winner.Key;
                        demoEntryPhase = DemoEntryPhase.Searching;
                        FollowDemoRoom(winner);
                        return;
                    }
                    if (now < demoCreationSettlesAt) return;
                    GamePendingRoomHandle handle = demoCreationHandle;
                    demoCreationHandle = null;
                    demoEntryPhase = DemoEntryPhase.Joining;
                    if (!EnterPendingRoom(pending.Key))
                    {
                        pendingRooms.Cancel(handle);
                        FinishDemoEntry("방을 열지 못했소. 다시 골라주시오.");
                    }
                    return;
                }
                bool conflict = demoCreationHandle != null && demoCreationHandle.Room.DemoNameConflict;
                demoCreationHandle = null;
                if (conflict)
                {
                    demoCreationAuthorized = false;
                    demoWaitingForWinner = true;
                    demoWinningRoomKey = null;
                    if (BeginDemoSearch(now, true)) return;
                }
                FinishDemoEntry(conflict ? "다른 기기가 방을 열고 있소. 잠시 후 다시 골라주시오."
                    : "방을 공개하지 못했소. 네트워크 권한을 확인해주시오.");
                return;
            }

            LabDiscoveredRoom room = DemoRoomForStage(network.DiscoveredRooms, SelectedDemoStage, SelectedDemoGroup);
            if (!string.IsNullOrEmpty(demoWinningRoomKey))
            {
                room = null;
                foreach (LabDiscoveredRoom candidate in network.DiscoveredRooms)
                    if (candidate != null && string.Equals(candidate.Key, demoWinningRoomKey, StringComparison.Ordinal))
                    { room = candidate; break; }
            }
            if (room != null)
            {
                FollowDemoRoom(room);
                return;
            }
            if (network.RoomSearchFailed || !network.IsSearchingRooms)
            { FinishDemoEntry("방 검색을 계속할 수 없소. 네트워크 권한을 확인해주시오."); return; }
            if (demoWaitingForWinner)
            { DemoEntryNotice = "다른 기기가 열고 있는 " + SelectedDemoGroupLabel + "의 방을 기다리고 있소..."; return; }
            if (now < demoSearchUntil) return;
            if (!demoCreationAuthorized)
            {
                demoEntryPhase = DemoEntryPhase.AwaitingCreationConfirmation;
                DemoEntryNotice = DemoCreationConfirmationMessage;
                Changed?.Invoke();
                return;
            }
            if (!pendingRooms.TryCreateStaffStage(SelectedDemoStage, SelectedDemoGroup, out demoCreationHandle, out string error))
            { FinishDemoEntry(error); return; }
            demoCreationAuthorized = false;
            demoCreationSettlesAt = double.NaN;
            demoEntryPhase = DemoEntryPhase.Creating;
            DemoEntryNotice = SelectedDemoGroupLabel + "의 방을 만들고 있소. 준비되면 바로 입장하오...";
            Changed?.Invoke();
        }

        private void FollowDemoRoom(LabDiscoveredRoom room)
        {
            if (room.Playing) { FinishDemoEntry(SelectedDemoGroupLabel + "은 전투 중이오. 끝난 뒤 다시 입장해주시오."); return; }
            if (room.WaitingForHost)
            { DemoEntryNotice = "방장이 방에 들어오고 있소..."; return; }
            if (room.IsFull && !network.HoldsRoomTicket(room.Key))
            { FinishDemoEntry(SelectedDemoGroupLabel + "의 방은 세 명이 모두 모였소."); return; }
            if (!network.CanJoinDiscoveredRoom(room.Key)) return; // Cached rows are blockers, never fresh join routes.
            demoEntryPhase = DemoEntryPhase.Joining;
            DemoEntryNotice = "방에 입장하고 있소...";
            JoinRoom(room.Key);
            Changed?.Invoke();
        }

        /// <summary>An existing Host wins over an unentered candidate; simultaneous candidates use one stable key order.</summary>
        internal static LabDiscoveredRoom DemoCreationWinner(System.Collections.Generic.IReadOnlyList<LabDiscoveredRoom> rooms,
            int stage, int group, string ownKey)
        {
            if (rooms == null || !LabGameBuildVariant.IsValidStaffRoomIdentity(stage, group) || string.IsNullOrEmpty(ownKey)) return null;
            LabDiscoveredRoom chosen = null;
            foreach (LabDiscoveredRoom room in rooms)
            {
                if (room == null || room.DemoStageIndex != stage || room.DemoGroupId != group || room.Team || room.TargetParticipants != 3
                    || string.Equals(room.Key, ownKey, StringComparison.OrdinalIgnoreCase)) continue;
                if (room.WaitingForHost && string.Compare(room.Key, ownKey, StringComparison.Ordinal) >= 0) continue;
                if (chosen == null || chosen.WaitingForHost && !room.WaitingForHost
                    || chosen.WaitingForHost == room.WaitingForHost && string.Compare(room.Key, chosen.Key, StringComparison.Ordinal) < 0)
                    chosen = room;
            }
            return chosen;
        }

        /// <summary>Stable choice across A/AAAA routes; stale/full/playing rows remain visible blockers, never new groups.</summary>
        internal static LabDiscoveredRoom DemoRoomForStage(System.Collections.Generic.IReadOnlyList<LabDiscoveredRoom> rooms, int stage, int group)
        {
            if (rooms == null || stage < 1 || stage > 3 || group < 1 || group > 2) return null;
            LabDiscoveredRoom chosen = null;
            foreach (LabDiscoveredRoom room in rooms)
                if (room != null && room.DemoStageIndex == stage && room.DemoGroupId == group && !room.Team && room.TargetParticipants == 3
                    && (chosen == null || chosen.WaitingForHost && !room.WaitingForHost
                        || chosen.WaitingForHost == room.WaitingForHost && string.Compare(room.Key, chosen.Key, StringComparison.Ordinal) < 0))
                    chosen = room;
            return chosen;
        }

        public void CancelDemoEntry()
        {
            staffRetryEntryStage = staffRetryEntryGroup = 0;
            if (demoCreationHandle != null) pendingRooms.Cancel(demoCreationHandle);
            demoCreationHandle = null;
            demoCreationAuthorized = demoWaitingForWinner = false;
            demoWinningRoomKey = null;
            demoCreationSettlesAt = double.NaN;
            bool active = IsDemoEntryPending;
            demoEntryPhase = DemoEntryPhase.None;
            if (active && network != null)
            {
                if (network.IsJoiningRoom) network.CancelJoin();
                else if (!network.IsConnected) network.CancelRoomSearch();
            }
            DemoEntryNotice = string.Empty;
            if (active) Changed?.Invoke();
        }

        private void FinishDemoEntry(string notice)
        {
            if (demoCreationHandle != null) pendingRooms.Cancel(demoCreationHandle);
            demoCreationHandle = null;
            demoCreationAuthorized = demoWaitingForWinner = false;
            demoWinningRoomKey = null;
            demoCreationSettlesAt = double.NaN;
            demoEntryPhase = DemoEntryPhase.None;
            DemoEntryNotice = notice ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
