using System;
using System.Collections.Generic;
using System.Linq;
using C6Lab;

namespace Hapio
{
    /// <summary>Read-only room-card data; screens never own the sockets or registrations.</summary>
    public sealed class GamePendingRoomInfo
    {
        public string Key { get; }
        public string Name { get; }
        public ushort Port { get; }
        public int Capacity { get; }
        public bool Ready { get; }
        public bool Failed { get; }

        internal GamePendingRoomInfo(LabPendingRoom room)
        {
            Key = room.Key; Name = room.Name; Port = room.Port; Capacity = room.Capacity;
            Ready = room.Ready; Failed = room.Failed;
        }
    }

    /// <summary>Game-session-owned advertisements, independent of disposable menu and Battle Scenes.</summary>
    public sealed class GamePendingRoomRegistry : IDisposable
    {
        private readonly List<LabPendingRoom> rooms = new List<LabPendingRoom>();
        private bool disposed;
        public IReadOnlyList<GamePendingRoomInfo> Rooms => rooms.Select(room => new GamePendingRoomInfo(room)).ToArray();

        public bool TryCreate(int capacity, out string error)
        {
            error = string.Empty;
            if (disposed) { error = "방 목록이 닫혔소."; return false; }
            if (rooms.Count >= LabBonjourDiscovery.MaximumServices)
            { error = "열린 방이 너무 많소. 만든 방에 먼저 입장해주시오."; return false; }
            if (!LabPendingRoom.TryCreate(capacity, LabRoomFamily.Game, out var room, out error)) return false;
            rooms.Add(room);
            return true;
        }

        public bool CanEnter(string key) => !disposed && !string.IsNullOrEmpty(key)
            && rooms.Any(room => room.Key == key && room.Ready);

        public bool TryEnter(string key, LabNetwork network, out string error)
        {
            error = string.Empty;
            var room = disposed ? null : rooms.FirstOrDefault(item => item.Key == key);
            if (room == null || !room.Ready)
            { error = "방 준비가 끝난 뒤 다시 입장해주시오."; return false; }
            if (!room.EnterAsCreator(network))
            { error = "방에 입장하지 못했소. 연결 상태를 확인하고 다시 눌러주시오."; return false; }
            rooms.Remove(room);
            // EnterAsCreator moved the confirmed advertisement to the existing Network.
            room.Dispose();
            return true;
        }

        public void Tick(out string error)
        {
            error = string.Empty;
            if (disposed) return;
            bool allowRegistration = true;
            int nextNumber = 1;
            foreach (var room in rooms)
            {
                room.Tick(allowRegistration, nextNumber);
                if (room.Ready && LabRoomNames.TryNumber(room.Name, out int number, LabRoomNames.GameServiceBase)
                    && number < int.MaxValue) nextNumber = Math.Max(nextNumber, number + 1);
                if (!room.Ready && !room.Failed) allowRegistration = false;
            }
            for (int i = rooms.Count - 1; i >= 0; i--)
                if (rooms[i].Failed)
                {
                    rooms[i].Dispose(); rooms.RemoveAt(i);
                    error = "방을 공개하지 못했소. 같은 Wi-Fi와 네트워크 권한을 확인해주시오.";
                }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var room in rooms) room.Dispose();
            rooms.Clear();
        }
    }
}
