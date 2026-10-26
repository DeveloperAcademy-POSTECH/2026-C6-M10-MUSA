using System;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private const string RoomCreationNotice = "방을 만들고 있소. 준비되면 바로 입장하오...";
        private GamePendingRoomHandle roomCreationHandle;
        private double roomCreationDeadline;

        public bool IsCreatingRoom => roomCreationHandle != null;

        /// <summary>Keep the confirmed discovery identity, then enter without another card click.</summary>
        public bool CreateAndEnterRoom(int capacity, GameMode mode = GameMode.Story)
        {
            if (IsCreatingRoom || goingHome || state.phase != GameFlowPhase.Home || !network.CanChooseConnection)
                return false;
            roomBrowserVisible = true;
            if (!pendingRooms.TryCreate(capacity, mode, out GamePendingRoomHandle handle, out string error))
            {
                UiNotice = error;
                Changed?.Invoke();
                return false;
            }
            roomCreationHandle = handle;
            roomCreationDeadline = Time.realtimeSinceStartupAsDouble + Math.Max(5d, loadingTimeoutSeconds);
            UiNotice = RoomCreationNotice;
            Changed?.Invoke();
            return true;
        }

        public bool CancelRoomCreation() => CancelRoomCreation(true);

        private bool CancelRoomCreation(bool notify)
        {
            if (!IsCreatingRoom) return false;
            GamePendingRoomHandle handle = roomCreationHandle;
            roomCreationHandle = null;
            roomCreationDeadline = 0d;
            pendingRooms.Cancel(handle);
            if (notify)
            {
                UiNotice = "방 만들기를 취소했소.";
                Changed?.Invoke();
            }
            return true;
        }

        /// <summary>Called after the registry's native registration tick, before normal connection flow.</summary>
        private void UpdateRoomCreation()
        {
            if (!IsCreatingRoom) return;
            if (goingHome || state.phase != GameFlowPhase.Home || !network.CanChooseConnection)
            {
                CancelRoomCreation(false);
                return;
            }
            if (!pendingRooms.TryGet(roomCreationHandle, out GamePendingRoomInfo room))
            {
                // A failed registration was already disposed by the registry. Do not keep a stuck request.
                CancelRoomCreation(false);
                if (string.IsNullOrEmpty(UiNotice) || UiNotice == RoomCreationNotice)
                    UiNotice = "방을 공개하지 못했소. 네트워크 권한을 확인하고 다시 눌러주시오.";
                Changed?.Invoke();
                return;
            }
            if (Time.realtimeSinceStartupAsDouble >= roomCreationDeadline)
            {
                CancelRoomCreation(false);
                UiNotice = "방 준비 시간이 초과됐소. 네트워크 연결을 확인하고 다시 눌러주시오.";
                Changed?.Invoke();
                return;
            }
            if (!room.Ready) return;

            GamePendingRoomHandle readyHandle = roomCreationHandle;
            // Clear before EnterPendingRoom: its existing reset/guards must not cancel the room being handed over.
            roomCreationHandle = null;
            roomCreationDeadline = 0d;
            if (EnterPendingRoom(room.Key)) return;
            pendingRooms.Cancel(readyHandle);
            if (string.IsNullOrEmpty(UiNotice)) UiNotice = "방에 입장하지 못했소. 다시 눌러주시오.";
            Changed?.Invoke();
        }
    }
}
