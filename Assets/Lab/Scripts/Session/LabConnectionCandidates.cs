using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace C6Lab
{
    /// <summary>Bounded route selection; a late Bonjour IPv6 record remains eligible after IPv4 attempts.</summary>
    public static class LabConnectionCandidates
    {
        public const int InitialRouteLimit = 8;
        public const int DiscoveryRouteLimit = 64;

        public static string[] Initial(IEnumerable<string> addresses, ushort port)
        {
            if (addresses == null) return Array.Empty<string>();
            var valid = addresses.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Where(x => LabDualStackEndpoint.TryParse(x, port, out _, out _)).ToArray();
            string[] ipv6 = valid.Where(x => IPAddress.Parse(x).AddressFamily == AddressFamily.InterNetworkV6).ToArray();
            string[] ipv4 = valid.Where(x => IPAddress.Parse(x).AddressFamily == AddressFamily.InterNetwork).ToArray();
            var routes = new List<string>(InitialRouteLimit);
            if (ipv6.Length > 0) routes.Add(ipv6[0]);
            if (ipv4.Length > 0) routes.Add(ipv4[0]);
            foreach (string address in ipv6.Skip(1).Concat(ipv4.Skip(1)))
            {
                if (routes.Count >= InitialRouteLimit) break;
                routes.Add(address);
            }
            return routes.ToArray();
        }

        public static bool AppendDiscovered(IList<string> routes, string address, ushort port)
        {
            if (routes == null || routes.Count >= DiscoveryRouteLimit || string.IsNullOrWhiteSpace(address)
                || routes.Contains(address) || !LabDualStackEndpoint.TryParse(address, port, out _, out _)) return false;
            routes.Add(address);
            return true;
        }
    }
}
