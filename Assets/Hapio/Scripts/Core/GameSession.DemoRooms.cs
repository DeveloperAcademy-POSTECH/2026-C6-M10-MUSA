using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private enum DemoEntryPhase { None, Searching, Creating, Joining }
        private DemoEntryPhase demoEntryPhase;
        private GamePendingRoomHandle demoCreationHandle;
        private double demoEntryDeadline, demoSearchUntil;
        private int demoCreateAttempts;
        private const double DemoEntryTimeout = 35d, DemoSearchSeconds = 2d;
        private const int DemoMaximumCreateAttempts = 3;

        public bool IsDemoEntryPending => demoEntryPhase != DemoEntryPhase.None;
        public string DemoEntryNotice { get; private set; } = string.Empty;

        /// <summary>One staff card owns one bounded browse/create/join operation. No gameplay begins here.</summary>
        public bool SelectDemoStage(int stage)
        {
            if (!LabGameBuildVariant.IsStaffStageDemo || IsDemoEntryPending || IsStaffRetryPending || goingHome || IsCreatingRoom
                || !SetSelectedDemoStage(stage) || !network.SetDemoStageIdentity(stage)) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            demoEntryDeadline = now + DemoEntryTimeout;
            demoCreateAttempts = 0;
            roomBrowserVisible = false;
            if (!BeginDemoSearch(now))
            {
                FinishDemoEntry("방을 찾지 못했소. 네트워크 권한과 같은 Wi-Fi 연결을 확인해주시오.");
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        private bool BeginDemoSearch(double now)
        {
            demoEntryPhase = DemoEntryPhase.Searching;
            demoSearchUntil = now + DemoSearchSeconds;
            DemoEntryNotice = "선택한 스테이지의 방을 찾고 있소...";
            return network.IsSearchingRooms || network.StartRoomSearch();
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
                    if (!pending.Ready) return;
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
                if (conflict && demoCreateAttempts < DemoMaximumCreateAttempts && BeginDemoSearch(now)) return;
                FinishDemoEntry(conflict ? "다른 기기가 방을 열고 있소. 잠시 후 다시 골라주시오."
                    : "방을 공개하지 못했소. 네트워크 권한을 확인해주시오.");
                return;
            }

            LabDiscoveredRoom room = DemoRoomForStage(network.DiscoveredRooms, SelectedDemoStage);
            if (room != null)
            {
                if (room.Playing) { FinishDemoEntry("이 스테이지는 전투 중이오. 끝난 뒤 다시 골라주시오."); return; }
                if (room.WaitingForHost)
                {
                    DemoEntryNotice = "방장이 방에 들어오고 있소...";
                    return;
                }
                if (room.IsFull && !network.HoldsRoomTicket(room.Key))
                { FinishDemoEntry("이 스테이지의 방은 세 명이 모두 모였소."); return; }
                if (network.CanJoinDiscoveredRoom(room.Key))
                {
                    demoEntryPhase = DemoEntryPhase.Joining;
                    DemoEntryNotice = "방에 입장하고 있소...";
                    JoinRoom(room.Key);
                    Changed?.Invoke();
                    return;
                }
                // A cached row without fresh routes must not trigger another host.
                return;
            }
            if (network.RoomSearchFailed || !network.IsSearchingRooms)
            { FinishDemoEntry("방 검색을 계속할 수 없소. 네트워크 권한을 확인해주시오."); return; }
            if (now < demoSearchUntil) return;
            if (!pendingRooms.TryCreateStaffStage(SelectedDemoStage, out demoCreationHandle, out string error))
            { FinishDemoEntry(error); return; }
            demoCreateAttempts++;
            demoEntryPhase = DemoEntryPhase.Creating;
            DemoEntryNotice = "방을 만들고 있소. 준비되면 바로 입장하오...";
            Changed?.Invoke();
        }

        /// <summary>Stable choice across A/AAAA routes; stale/full/playing rows remain visible blockers, never new groups.</summary>
        internal static LabDiscoveredRoom DemoRoomForStage(System.Collections.Generic.IReadOnlyList<LabDiscoveredRoom> rooms, int stage)
        {
            if (rooms == null || stage < 1 || stage > 3) return null;
            LabDiscoveredRoom chosen = null;
            foreach (LabDiscoveredRoom room in rooms)
                if (room != null && room.DemoStageIndex == stage && !room.Team && room.TargetParticipants == 3
                    && (chosen == null || string.Compare(room.Key, chosen.Key, StringComparison.Ordinal) < 0)) chosen = room;
            return chosen;
        }

        public void CancelDemoEntry()
        {
            staffRetryEntryStage = 0;
            if (demoCreationHandle != null) pendingRooms.Cancel(demoCreationHandle);
            demoCreationHandle = null;
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
            demoEntryPhase = DemoEntryPhase.None;
            DemoEntryNotice = notice ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
