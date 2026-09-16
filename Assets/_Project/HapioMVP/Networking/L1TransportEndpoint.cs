using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;

namespace C6.Prototype.Networking
{
    /// <summary>
    /// L1 diagnostic adapter for numeric IP endpoints. It leaves stock UTP sockets and
    /// reliability unchanged and only preserves the IPv6 interface index lost by Encode.
    /// This is not yet connected to the game's address validation or session lifecycle.
    /// </summary>
    public static class L1TransportEndpoint
    {
        // Pinned to Unity 6000.5.7f1 / UTP 6.5.0. Baselib_NetworkAddress.h defines:
        // address[16], port[2], family[1], padding[1], ipv6_scope_id[4] (native byte order).
        // NetworkEndpoint.Transferrable is public specifically for network interface adapters.
        // Recheck the native header and these tests before changing Unity/UTP versions.
        private const int ScopeOffset = 20;
        private const int TransferrableSize = 64;

        public static bool TryParse(string address, ushort port, out NetworkEndpoint endpoint, out string error)
        {
            endpoint = default;
            error = null;
            if (string.IsNullOrWhiteSpace(address) || address.Length > 64 || address != address.Trim())
            {
                error = "A numeric IPv4 or IPv6 address is required.";
                return false;
            }
            var separator = address.IndexOf('%');
            var bare = separator < 0 ? address : address.Substring(0, separator);
            uint scope = 0;
            if (separator >= 0 && (!uint.TryParse(address.Substring(separator + 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out scope) || scope == 0))
            {
                error = "An IPv6 zone must be a positive numeric interface index.";
                return false;
            }
            if (bare.StartsWith("[", StringComparison.Ordinal) || !IPAddress.TryParse(bare, out var parsed))
            {
                error = "Hostnames, URLs, and bracketed addresses are not supported by this diagnostic.";
                return false;
            }
            var family = parsed.AddressFamily == AddressFamily.InterNetwork ? NetworkFamily.Ipv4 : NetworkFamily.Ipv6;
            if (family == NetworkFamily.Ipv4 && separator >= 0)
            {
                error = "IPv4 addresses do not have an IPv6 zone.";
                return false;
            }
            if (parsed.AddressFamily == AddressFamily.InterNetworkV6 && parsed.IsIPv6LinkLocal && scope == 0)
            {
                error = "A link-local IPv6 address needs its interface index.";
                return false;
            }
            if (!NetworkEndpoint.TryParse(bare, port, out endpoint, family))
            {
                error = "Stock Unity Transport could not encode the numeric address.";
                return false;
            }
            if (scope != 0) endpoint = WithScope(endpoint, scope);
            return true;
        }

        public static unsafe NetworkEndpoint WithScope(NetworkEndpoint endpoint, uint scope)
        {
            CheckLayout();
            if (endpoint.Family != NetworkFamily.Ipv6)
                throw new ArgumentException("Only an IPv6 endpoint can carry an interface index.", nameof(endpoint));
            var bytes = (byte*)UnsafeUtility.AddressOf(ref endpoint.Transferrable);
            *(uint*)(bytes + ScopeOffset) = scope;
            return endpoint;
        }

        public static unsafe uint GetScopeId(NetworkEndpoint endpoint)
        {
            CheckLayout();
            if (endpoint.Family != NetworkFamily.Ipv6) return 0;
            var bytes = (byte*)UnsafeUtility.AddressOf(ref endpoint.Transferrable);
            return *(uint*)(bytes + ScopeOffset);
        }

        public static string GetAddress(NetworkEndpoint endpoint)
        {
            var address = endpoint.ToFixedStringNoPort().ToString();
            if (endpoint.Family == NetworkFamily.Ipv6 && address.StartsWith("[", StringComparison.Ordinal)
                && address.EndsWith("]", StringComparison.Ordinal))
                address = address.Substring(1, address.Length - 2);
            var scope = GetScopeId(endpoint);
            return scope == 0 ? address : address + "%" + scope.ToString(CultureInfo.InvariantCulture);
        }

        private static void CheckLayout()
        {
            if (UnsafeUtility.SizeOf<NetworkEndpoint.TransferrableData>() != TransferrableSize)
                throw new NotSupportedException("The installed UTP raw endpoint layout differs from the L1 diagnostic baseline.");
        }
    }
}
