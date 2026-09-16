using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace C6.Prototype.Lobby.Discovery
{
    /// <summary>Bounded routing candidates. Address-family diversity survives multi-interface discovery.</summary>
    public static class DiscoveryAddressCandidates
    {
        public const int MaximumCandidates = 8;
        public const int MaximumRecords = 32;

        public static bool TryNormalize(string text, out string address)
        {
            address = null;
            if (string.IsNullOrWhiteSpace(text) || text.Contains("[") || text.Contains("]")) return false;
            if (!IPAddress.TryParse(text, out var parsed) || IPAddress.IsLoopback(parsed)) return false;
            if (parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                if (!RoomAdvertisementCodec.IsUsableIPv4(text)) return false;
            }
            else if (parsed.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (parsed.Equals(IPAddress.IPv6Any) || parsed.IsIPv6Multicast || parsed.IsIPv4MappedToIPv6) return false;
                int percent = text.IndexOf('%');
                if (parsed.IsIPv6LinkLocal)
                {
                    if (percent < 0 || parsed.ScopeId <= 0 || parsed.ScopeId >= 0xffffff00L) return false;
                    string scope = text.Substring(percent + 1);
                    if (scope.Length == 0 || scope.Any(c => c < '0' || c > '9')) return false;
                }
                else if (percent >= 0 || parsed.ScopeId != 0) return false;
            }
            else return false;
            address = parsed.ToString(); return true;
        }

        public static string[] Select(IEnumerable<string> addresses) => Select(addresses, MaximumCandidates);
        internal static string[] SelectRecords(IEnumerable<string> addresses) => Select(addresses, MaximumRecords);

        private static string[] Select(IEnumerable<string> addresses, int maximum)
        {
            if (addresses == null) return Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var groups = new[] { new Queue<string>(), new Queue<string>(), new Queue<string>() };
            foreach (string candidate in addresses.Take(MaximumRecords * DiscoveryRoomCatalog.MaximumServices))
            {
                if (!TryNormalize(candidate, out string normalized) || !seen.Add(normalized)) continue;
                var parsed = IPAddress.Parse(normalized);
                int group = parsed.AddressFamily == AddressFamily.InterNetwork ? 1 : parsed.IsIPv6LinkLocal ? 2 : 0;
                groups[group].Enqueue(normalized);
            }
            var selected = new List<string>(maximum);
            // L1 verified non-link-local IPv6 on the hotspot. Preserve IPv4 and scoped IPv6 as
            // immediate alternatives; many records from one family cannot evict the other family.
            while (selected.Count < maximum && groups.Any(g => g.Count > 0))
                foreach (var group in groups)
                    if (group.Count > 0 && selected.Count < maximum) selected.Add(group.Dequeue());
            return selected.ToArray();
        }
    }
}
