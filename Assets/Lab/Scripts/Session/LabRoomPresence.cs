using System;
using System.Globalization;
using System.Text;
namespace C6Lab
{
    /// <summary>Bounded Bonjour presence metadata; discovery is advisory, admission remains authoritative.</summary>
    public static class LabRoomPresence
    {
        public static byte[] Encode(string protocol, bool ipv4, bool ipv6, int participants, int capacity, bool playing, bool waitingForHost = false)
        {
            if (!ValidProtocol(protocol) || (!ipv4 && !ipv6) || capacity < 2 || capacity > 5 || participants < 1 || participants > capacity
                || (waitingForHost && (playing || participants != 1)))
                throw new ArgumentOutOfRangeException();
            string text = "p=" + protocol + ";f=" + (ipv4 ? "4" : "") + (ipv6 ? "6" : "")
                + ";n=" + participants.ToString(CultureInfo.InvariantCulture) + ";c=" + capacity.ToString(CultureInfo.InvariantCulture)
                + ";b=" + (playing ? "1" : "0") + (waitingForHost ? ";w=1" : "");
            byte[] payload = Encoding.UTF8.GetBytes(text), record = new byte[payload.Length + 1];
            if (payload.Length > 127) throw new ArgumentOutOfRangeException();
            record[0] = (byte)payload.Length; Buffer.BlockCopy(payload, 0, record, 1, payload.Length); return record;
        }
        public static bool TryDecode(byte[] record, string protocol, out bool ipv4, out bool ipv6,
            out int participants, out int capacity, out bool playing)
            => TryDecode(record, protocol, out ipv4, out ipv6, out participants, out capacity, out playing, out _);

        public static bool TryDecode(byte[] record, string protocol, out bool ipv4, out bool ipv6,
            out int participants, out int capacity, out bool playing, out bool waitingForHost)
        {
            ipv4 = ipv6 = playing = waitingForHost = false; participants = capacity = 0;
            if (!ValidProtocol(protocol) || record == null || record.Length < 2 || record.Length > 128 || record[0] != record.Length - 1) return false;
            string text;
            try { text = new UTF8Encoding(false, true).GetString(record, 1, record.Length - 1); }
            catch (DecoderFallbackException) { return false; }
            string[] fields = text.Split(';');
            if ((fields.Length != 5 && fields.Length != 6) || (fields.Length == 6 && fields[5] != "w=1")
                || fields[0] != "p=" + protocol || !fields[1].StartsWith("f=", StringComparison.Ordinal)
                || !fields[2].StartsWith("n=", StringComparison.Ordinal) || !fields[3].StartsWith("c=", StringComparison.Ordinal)
                || (fields[4] != "b=0" && fields[4] != "b=1")) return false;
            string family = fields[1].Substring(2);
            if (family != "4" && family != "6" && family != "46") return false;
            if (!int.TryParse(fields[2].Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                || !int.TryParse(fields[3].Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int c)
                || c < 2 || c > 5 || n < 1 || n > c) return false;
            if (fields.Length == 6 && (n != 1 || fields[4] != "b=0")) return false;
            ipv4 = family.Contains("4"); ipv6 = family.Contains("6");
            participants = n; capacity = c; playing = fields[4] == "b=1"; waitingForHost = fields.Length == 6; return true;
        }

        private static bool ValidProtocol(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')
                    && c != '-' && c != '/') return false;
            return true;
        }
    }
}
