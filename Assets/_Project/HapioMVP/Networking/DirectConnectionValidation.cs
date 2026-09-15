using System.Globalization;
using System.Net;

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
            if (string.IsNullOrWhiteSpace(text)) return false;
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
