using System;
using System.Globalization;
using System.Text;
namespace C6Lab
{
    /// <summary>Bounded Bonjour presence metadata; discovery is advisory, admission remains authoritative.</summary>
    public static class LabRoomPresence
    {
        /// <summary>
        /// team marks a team territory room (#281) with a trailing "t=1"; older records simply omit it. A territory room
        /// holds 2, 4 or 6 players (#394), any other room 2 to 5.
        /// </summary>
        public static byte[] Encode(string protocol, bool ipv4, bool ipv6, int participants, int capacity, bool playing,
            bool waitingForHost = false, bool team = false, int demoStage = 0)
        {
            if (!ValidProtocol(protocol) || (!ipv4 && !ipv6) || !LabModel.ValidRoomSize(capacity, team) || participants < 1 || participants > capacity
                || (waitingForHost && (playing || participants != 1))
                || demoStage < 0 || demoStage > 3 || demoStage != 0 && (team || capacity != 3))
                throw new ArgumentOutOfRangeException();
            string text = "p=" + protocol + ";f=" + (ipv4 ? "4" : "") + (ipv6 ? "6" : "")
                + ";n=" + participants.ToString(CultureInfo.InvariantCulture) + ";c=" + capacity.ToString(CultureInfo.InvariantCulture)
                + ";b=" + (playing ? "1" : "0") + (waitingForHost ? ";w=1" : "") + (team ? ";t=1" : "")
                + (demoStage == 0 ? string.Empty : ";s=" + demoStage.ToString(CultureInfo.InvariantCulture));
            byte[] payload = Encoding.UTF8.GetBytes(text), record = new byte[payload.Length + 1];
            if (payload.Length > 127) throw new ArgumentOutOfRangeException();
            record[0] = (byte)payload.Length; Buffer.BlockCopy(payload, 0, record, 1, payload.Length); return record;
        }
        public static bool TryDecode(byte[] record, string protocol, out bool ipv4, out bool ipv6,
            out int participants, out int capacity, out bool playing)
            => TryDecode(record, protocol, out ipv4, out ipv6, out participants, out capacity, out playing, out _);

        public static bool TryDecode(byte[] record, string protocol, out bool ipv4, out bool ipv6,
            out int participants, out int capacity, out bool playing, out bool waitingForHost)
            => TryDecode(record, protocol, out ipv4, out ipv6, out participants, out capacity, out playing,
                out waitingForHost, out _);

        public static bool TryDecode(byte[] record, string protocol, out bool ipv4, out bool ipv6,
            out int participants, out int capacity, out bool playing, out bool waitingForHost, out bool team)
            => TryDecode(record, protocol, out ipv4, out ipv6, out participants, out capacity, out playing,
                out waitingForHost, out team, out _);

        public static bool TryDecode(byte[] record, string protocol, out bool ipv4, out bool ipv6,
            out int participants, out int capacity, out bool playing, out bool waitingForHost, out bool team, out int demoStage)
        {
            ipv4 = ipv6 = playing = waitingForHost = team = false; participants = capacity = demoStage = 0;
            if (!ValidProtocol(protocol) || record == null || record.Length < 2 || record.Length > 128 || record[0] != record.Length - 1) return false;
            string text;
            try { text = new UTF8Encoding(false, true).GetString(record, 1, record.Length - 1); }
            catch (DecoderFallbackException) { return false; }
            string[] fields = text.Split(';');
            int stage = 0;
            if (fields.Length >= 6 && fields[fields.Length - 1].StartsWith("s=", StringComparison.Ordinal))
            {
                string value = fields[fields.Length - 1].Substring(2);
                if (value.Length != 1 || value[0] < '1' || value[0] > '3') return false;
                stage = value[0] - '0';
                Array.Resize(ref fields, fields.Length - 1);
            }
            // The optional 2:2 marker (#281) is always last and never repeats.
            bool teamField = fields.Length >= 6 && fields[fields.Length - 1] == "t=1";
            if (teamField) Array.Resize(ref fields, fields.Length - 1);
            if ((fields.Length != 5 && fields.Length != 6) || (fields.Length == 6 && fields[5] != "w=1")
                || fields[0] != "p=" + protocol || !fields[1].StartsWith("f=", StringComparison.Ordinal)
                || !fields[2].StartsWith("n=", StringComparison.Ordinal) || !fields[3].StartsWith("c=", StringComparison.Ordinal)
                || (fields[4] != "b=0" && fields[4] != "b=1")) return false;
            string family = fields[1].Substring(2);
            if (family != "4" && family != "6" && family != "46") return false;
            if (!int.TryParse(fields[2].Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                || !int.TryParse(fields[3].Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int c)
                || !LabModel.ValidRoomSize(c, teamField) || n < 1 || n > c
                || stage != 0 && (teamField || c != 3)) return false;
            if (fields.Length == 6 && (n != 1 || fields[4] != "b=0")) return false;
            ipv4 = family.Contains("4"); ipv6 = family.Contains("6");
            participants = n; capacity = c; playing = fields[4] == "b=1"; waitingForHost = fields.Length == 6;
            team = teamField; demoStage = stage; return true;
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
