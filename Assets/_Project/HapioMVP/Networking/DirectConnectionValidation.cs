using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace C6.Prototype.Networking
{
    public static class DirectConnectionValidation
    {
        public static bool TryParsePort(string text, out ushort port)
        {
            port = 0;
            return !string.IsNullOrWhiteSpace(text)
                && ushort.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port)
                && port != 0;
        }

        public static bool TryParseAddress(string text, out string address)
        {
            address = null;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 64) return false;
            text = text.Trim();
            if (!text.Contains(":")) return TryParseIPv4Address(text, out address);
            // Numeric interface scope is required for link-local routing. DNS names, URI brackets,
            // multicast, unspecified and IPv4-mapped forms never enter the direct-IP route.
            if (text.Contains("[") || text.Contains("]")) return false;
            int percent = text.IndexOf('%');
            uint scope = 0;
            string literal = text;
            if (percent >= 0)
            {
                if (text.IndexOf('%', percent + 1) >= 0) return false;
                literal = text.Substring(0, percent);
                string suffix = text.Substring(percent + 1);
                if (suffix.Length == 0) return false;
                foreach (char digit in suffix) if (digit < '0' || digit > '9') return false;
                if (!uint.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out scope) || scope == 0) return false;
            }
            if (!IPAddress.TryParse(literal, out var ip) || ip.AddressFamily != AddressFamily.InterNetworkV6
                || ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6Multicast || ip.IsIPv4MappedToIPv6) return false;
            if (ip.IsIPv6LinkLocal != (scope > 0)) return false;
            if (scope > 0) ip.ScopeId = scope;
            address = ip.ToString();
            return true;
        }

        public static bool TryParseIPv4Address(string text, out string address)
        {
            address = null;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 64) return false;
            var parts = text.Trim().Split('.');
            if (parts.Length != 4) return false;
            var octets = new byte[4];
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0 || part.Length > 3 || (part.Length > 1 && part[0] == '0')) return false;
                foreach (var character in part)
                    if (character < '0' || character > '9') return false;
                if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out octets[i])) return false;
            }
            // Exclude unspecified/this-network, multicast, reserved and limited broadcast addresses.
            // Loopback remains available for explicitly labelled local two-process verification.
            if (octets[0] == 0 || octets[0] >= 224) return false;
            address = new IPAddress(octets).ToString();
            return true;
        }
    }
}
