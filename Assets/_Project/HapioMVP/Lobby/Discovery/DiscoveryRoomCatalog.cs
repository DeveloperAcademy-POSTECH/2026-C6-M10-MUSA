using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace C6.Prototype.Lobby.Discovery
{
    public sealed class DiscoveredRoom
    {
        private readonly RoomAdvertisement advertisement;
        private readonly ReadOnlyCollection<string> candidates;
        public string DiscoveryKey { get; }
        public string Address => candidates[0];
        public IReadOnlyList<string> Candidates => candidates;
        public IReadOnlyList<string> Addresses => candidates;
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

        internal DiscoveredRoom(string key, RoomAdvertisement room, IEnumerable<string> addresses, double now, double expires)
        {
            DiscoveryKey = key; advertisement = room.Copy(); candidates = Array.AsReadOnly(addresses.ToArray());
            LastSeenSeconds = now; ExpiresAtSeconds = expires;
        }
    }

    /// <summary>Per-interface leases, immutable merged candidates and bounded room count.</summary>
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

        // The latest metadata describes the room, but every compatible interface contributes routes.
        public IReadOnlyList<DiscoveredRoom> Rooms => entries.Values.GroupBy(x => x.RoomId, StringComparer.Ordinal)
            .Select(Merge).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.RoomId, StringComparer.Ordinal).ToArray();

        public bool Upsert(string key, RoomAdvertisement room, string address, double now)
            => Upsert(key, room, new[] { address }, now);

        public bool Upsert(string key, RoomAdvertisement room, IEnumerable<string> addresses, double now, double? expiresAt = null)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 1024 || !Finite(now) || now < latestTime ||
                !RoomAdvertisementCodec.Validate(room, out _) || addresses == null) return false;
            double expires = expiresAt ?? now + lifetime;
            if (!Finite(expires) || expires <= now || expires > now + lifetime) return false;
            string[] selected = DiscoveryAddressCandidates.Select(addresses);
            if (selected.Length == 0 || (!entries.ContainsKey(key) && entries.Count >= MaximumServices)) return false;
            latestTime = now;
            // An address refresh is not a newer TXT. Keep metadata ordering tied to its lease.
            double metadataSeenAt = expiresAt.HasValue ? expires - lifetime : now;
            entries[key] = new DiscoveredRoom(key, room, selected, metadataSeenAt, expires);
            return true;
        }

        private static DiscoveredRoom Merge(IGrouping<string, DiscoveredRoom> group)
        {
            var ordered = group.OrderByDescending(x => x.LastSeenSeconds).ThenBy(x => x.DiscoveryKey, StringComparer.Ordinal).ToArray();
            DiscoveredRoom latest = ordered[0];
            // A shared RoomId alone cannot authorize mixing a conflicting port/build/config endpoint.
            var compatible = ordered.Where(x => x.Port == latest.Port && x.ProtocolVersion == latest.ProtocolVersion &&
                x.Build == latest.Build && x.ConfigHash == latest.ConfigHash).ToArray();
            string[] candidates = DiscoveryAddressCandidates.Select(compatible.SelectMany(x => x.Candidates));
            return new DiscoveredRoom(latest.DiscoveryKey, latest.Advertisement, candidates,
                latest.LastSeenSeconds, compatible.Max(x => x.ExpiresAtSeconds));
        }

        public bool Remove(string key) => key != null && entries.Remove(key);
        public bool Clear()
        {
            bool changed = entries.Count != 0; entries.Clear(); latestTime = double.NegativeInfinity; return changed;
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
