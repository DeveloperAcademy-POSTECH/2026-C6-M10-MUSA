using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace C6.Prototype.Lobby.Discovery
{
    public enum RoomAdvertisementStatus { Lobby = 0, Playing = 1 }

    /// <summary>Discovery is advisory. The authenticated lobby handshake must revalidate every field.</summary>
    public sealed class RoomAdvertisement
    {
        public string RoomId { get; set; }
        public string Name { get; set; }
        public int ProtocolVersion { get; set; }
        public string Build { get; set; }
        public string ConfigHash { get; set; }
        public RoomAdvertisementStatus Status { get; set; }
        public int Participants { get; set; }
        public ushort Port { get; set; }

        public RoomAdvertisement Copy() => new RoomAdvertisement
        {
            RoomId = RoomId, Name = Name, ProtocolVersion = ProtocolVersion, Build = Build,
            ConfigHash = ConfigHash, Status = Status, Participants = Participants, Port = Port
        };
    }

    /// <summary>Bounded, strict UTF-8 DNS TXT codec; the UDP port comes from the resolved SRV record.</summary>
    public static class RoomAdvertisementCodec
    {
        public const int MaximumRecordBytes = 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static bool Validate(RoomAdvertisement room, out string reason)
        {
            reason = null;
            if (room == null) { reason = "Missing room advertisement."; return false; }
            if (!AsciiToken(room.RoomId, 1, 64)) reason = "Invalid room id.";
            else if (!Text(room.Name, 1, 80)) reason = "Invalid room name.";
            else if (room.ProtocolVersion < 1 || room.ProtocolVersion > 65535) reason = "Invalid protocol version.";
            else if (!Text(room.Build, 1, 64)) reason = "Invalid build.";
            else if (room.ConfigHash == null || room.ConfigHash.Length != 64 || !IsHex(room.ConfigHash)) reason = "Invalid Config SHA-256.";
            else if (room.Status != RoomAdvertisementStatus.Lobby && room.Status != RoomAdvertisementStatus.Playing) reason = "Invalid room status.";
            else if (room.Participants < 1 || room.Participants > (LobbyProtocol.IsMultiparty(room.ProtocolVersion) ? LobbyProtocol.MaximumCapacity : LobbyProtocol.Capacity)) reason = "Invalid participant count.";
            else if (room.Port == 0) reason = "Invalid room port.";
            return reason == null;
        }

        public static byte[] Encode(RoomAdvertisement room, ulong heartbeat = 0)
        {
            if (!Validate(room, out string reason)) throw new ArgumentException(reason, nameof(room));
            var bytes = new List<byte>(320);
            Add(bytes, "id", room.RoomId);
            Add(bytes, "n", room.Name);
            Add(bytes, "p", room.ProtocolVersion.ToString(CultureInfo.InvariantCulture));
            Add(bytes, "b", room.Build);
            Add(bytes, "c", room.ConfigHash);
            Add(bytes, "s", room.Status == RoomAdvertisementStatus.Lobby ? "lobby" : "playing");
            Add(bytes, "u", room.Participants.ToString(CultureInfo.InvariantCulture));
            Add(bytes, "q", heartbeat.ToString(CultureInfo.InvariantCulture));
            return bytes.ToArray();
        }

        public static bool TryDecode(byte[] bytes, ushort port, out RoomAdvertisement room, out string reason)
        {
            room = null;
            reason = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumRecordBytes)
            { reason = "Missing or oversized Bonjour TXT record."; return false; }
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int offset = 0;
            try
            {
                while (offset < bytes.Length)
                {
                    int length = bytes[offset++];
                    if (length == 0 || offset + length > bytes.Length)
                    { reason = "Truncated or empty Bonjour TXT field."; return false; }
                    string field = Utf8.GetString(bytes, offset, length);
                    offset += length;
                    int split = field.IndexOf('=');
                    if (split < 1) { reason = "Invalid Bonjour TXT field."; return false; }
                    string key = field.Substring(0, split);
                    if (!AsciiToken(key, 1, 16) || values.ContainsKey(key))
                    { reason = "Invalid or duplicate Bonjour TXT key."; return false; }
                    values.Add(key, field.Substring(split + 1));
                }
            }
            catch (DecoderFallbackException) { reason = "Bonjour TXT is not valid UTF-8."; return false; }
            if (!values.TryGetValue("id", out string id) || !values.TryGetValue("n", out string name) ||
                !values.TryGetValue("p", out string protocol) || !values.TryGetValue("b", out string build) ||
                !values.TryGetValue("c", out string hash) || !values.TryGetValue("s", out string status) ||
                !values.TryGetValue("u", out string participants) || !values.TryGetValue("q", out string heartbeat) ||
                !int.TryParse(protocol, NumberStyles.None, CultureInfo.InvariantCulture, out int version) ||
                !int.TryParse(participants, NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
                !ulong.TryParse(heartbeat, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            { reason = "Incomplete Bonjour room metadata."; return false; }
            if (status != "lobby" && status != "playing")
            { reason = "Invalid room status."; return false; }
            var candidate = new RoomAdvertisement
            {
                RoomId = id, Name = name, ProtocolVersion = version, Build = build, ConfigHash = hash,
                Status = status == "lobby" ? RoomAdvertisementStatus.Lobby : RoomAdvertisementStatus.Playing,
                Participants = count, Port = port
            };
            if (!Validate(candidate, out reason)) return false;
            room = candidate;
            return true;
        }

        public static bool IsUsableIPv4(string address)
        {
            if (!IPAddress.TryParse(address, out IPAddress parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
                return false;
            byte[] bytes = parsed.GetAddressBytes();
            return bytes[0] != 0 && bytes[0] != 127 && bytes[0] < 224 && address == parsed.ToString();
        }

        private static void Add(List<byte> bytes, string key, string value)
        {
            byte[] field = Utf8.GetBytes(key + "=" + value);
            if (field.Length > 255) throw new ArgumentException("Bonjour TXT field exceeds 255 bytes.");
            bytes.Add((byte)field.Length);
            bytes.AddRange(field);
        }

        private static bool Text(string value, int minimumBytes, int maximumBytes)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            foreach (char c in value) if (char.IsControl(c)) return false;
            try { int count = Utf8.GetByteCount(value); return count >= minimumBytes && count <= maximumBytes; }
            catch (EncoderFallbackException) { return false; }
        }

        private static bool AsciiToken(string value, int minimum, int maximum)
        {
            if (value == null || value.Length < minimum || value.Length > maximum) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                    return false;
            return true;
        }

        private static bool IsHex(string value)
        {
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F') && !(c >= '0' && c <= '9')) return false;
            return true;
        }
    }
}
