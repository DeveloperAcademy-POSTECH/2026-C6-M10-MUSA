using C6Lab;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private bool demoOverviewSearchAttempted;
        private double demoOverviewSearchAt;
        private const double DemoOverviewDiscoverySeconds = 2d;

        /// <summary>Reads the existing room advertisement. Viewing the selector never reserves a place.</summary>
        private void UpdateDemoRoomOverview()
        {
            bool home = LabGameBuildVariant.IsStaffStageDemo && state.phase == GameFlowPhase.Home
                && !goingHome && !IsLoadingScene && !network.IsConnected;
            if (!home)
            {
                demoOverviewSearchAttempted = false;
                return;
            }
            // Entry/confirmation owns its own search and deadline. Never restart it from the overview.
            if (IsDemoEntryPending || IsStaffRetryPending || IsCreatingRoom || !network.CanChooseConnection) return;
            if (demoOverviewSearchAttempted && (network.IsSearchingRooms || network.RoomSearchFailed
                || network.RoomSearchRetryPending)) return;
            demoOverviewSearchAttempted = true;
            demoOverviewSearchAt = Time.realtimeSinceStartupAsDouble;
            if (!network.IsSearchingRooms) network.StartRoomSearch();
        }

        /// <summary>Stage2-only cards show counts; three-stage selection retains its existing group labels.</summary>
        public string GetDemoGroupStatus(int group)
        {
            if (!LabGameBuildVariant.IsStaffStageTwoOnly || group < 1 || group > 2 || network == null)
                return string.Empty;
            LabDiscoveredRoom room = DemoRoomForStage(network.DiscoveredRooms, 2, group);
            return DemoRoomStatus(room, network.RoomSearchFailed,
                !network.IsSearchingRooms || !demoOverviewSearchAttempted
                || Time.realtimeSinceStartupAsDouble - demoOverviewSearchAt < DemoOverviewDiscoverySeconds);
        }

        internal static string DemoRoomStatus(LabDiscoveredRoom room, bool failed, bool searching)
        {
            if (failed) return "확인 불가 · 네트워크 확인";
            if (room == null) return searching ? "검색 중…" : "방 없음 · 선택하여 생성";
            string count = room.Participants + "/" + room.TargetParticipants;
            if (room.WaitingForHost) return count + " · 방 생성 중";
            if (room.Playing) return count + " · 진행 중";
            return count + (room.IsFull ? " · 정원 완료" : " · 입장 가능");
        }
    }
}
