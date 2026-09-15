using System;
using System.Collections.Generic;
using System.Linq;

namespace C6.Prototype.Lobby.Discovery
{
    public sealed class DiscoveredRoom
    {
        private readonly RoomAdvertisement advertisement;
        public string DiscoveryKey { get; }
        public string Address { get; }
        public double LastSeenSeconds { get; }
        public double ExpiresAtSeconds { get; }
        public RoomAdvertisement Advertisement => advertisement.Copy();
        public string RoomId => advertisement.RoomId;
        public string Name => advertisement.Name;
        public int ProtocolVersion => advertisement.ProtocolVersion;
        public string Build => advertisement.Build;
        public string ConfigHash => advertisement.ConfigHash;
        public RoomAdvertisementStatus Status => advertisement.Status;
        public int Participants => advertisement.Participants;
        public ushort Port => advertisement.Port;

        internal DiscoveredRoom(string key, RoomAdvertisement room, string address, double now, double expires)
        {
            DiscoveryKey = key; advertisement = room.Copy(); Address = address;
            LastSeenSeconds = now; ExpiresAtSeconds = expires;
        }
    }

    /// <summary>Pure catalog: interface-specific removal, immutable snapshots, heartbeat expiry and bounded room count.</summary>
    public sealed class DiscoveryRoomCatalog
    {
        public const double DefaultLifetimeSeconds = 12;
        public const int MaximumServices = 32;
        private readonly Dictionary<string, DiscoveredRoom> entries = new Dictionary<string, DiscoveredRoom>(StringComparer.Ordinal);
        private readonly double lifetime;
        private double latestTime = double.NegativeInfinity;
        public int ServiceCount => entries.Count;

        public DiscoveryRoomCatalog(double lifetimeSeconds = DefaultLifetimeSeconds)
        {
            if (!Finite(lifetimeSeconds) || lifetimeSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
            lifetime = lifetimeSeconds;
        }

        // Multiple interfaces may report one room. Present it once while retaining each removal identity.
        public IReadOnlyList<DiscoveredRoom> Rooms => entries.Values
            .GroupBy(x => x.RoomId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(x => x.LastSeenSeconds).ThenBy(x => x.DiscoveryKey, StringComparer.Ordinal).First())
            .OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.RoomId, StringComparer.Ordinal).ToArray();

        public bool Upsert(string key, RoomAdvertisement room, string address, double now)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 1024 || !Finite(now) || now < latestTime ||
                !RoomAdvertisementCodec.Validate(room, out _) || !RoomAdvertisementCodec.IsUsableIPv4(address)) return false;
            if (!entries.ContainsKey(key) && entries.Count >= MaximumServices) return false;
            latestTime = now;
            entries[key] = new DiscoveredRoom(key, room, address, now, now + lifetime);
            return true;
        }

        public bool Remove(string key) => key != null && entries.Remove(key);
        public bool Clear()
        {
            bool changed = entries.Count != 0;
            entries.Clear();
            latestTime = double.NegativeInfinity;
            return changed;
        }

        public int Expire(double now)
        {
            if (!Finite(now) || now < latestTime) return 0;
            latestTime = now;
            var expired = entries.Where(x => x.Value.ExpiresAtSeconds <= now).Select(x => x.Key).ToArray();
            foreach (string key in expired) entries.Remove(key);
            return expired.Length;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
