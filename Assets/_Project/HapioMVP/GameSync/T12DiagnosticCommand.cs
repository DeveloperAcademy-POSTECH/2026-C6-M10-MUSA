using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>Fixed, bounded diagnostic protocol. It cannot name files or execute arbitrary code.</summary>
    [Serializable]
    public sealed class T12DiagnosticCommand
    {
        public int schema = 1;
        public string id, runId, action, orbId = "", otherOrbId = "";
        public string roomName = "C6 T12", ip = "127.0.0.1", port = "7777";
        public int direction, count = 1;
        public ulong expectedOwner = ulong.MaxValue, initialTransferCount;
        public int[] directions = Array.Empty<int>();
        public float height = .5f, x = .5f, y = 1;
        public bool screenshot, spendBeforeLaunch, expectRejected;
        public string PlanHash => T12DiagnosticProtocol.Hash(JsonUtility.ToJson(this));

        public static bool SafeId(string value) => !string.IsNullOrEmpty(value) && value.Length <= 64
            && value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_')
            && char.IsLetterOrDigit(value[0]);

        // Every complete primitive token is checked before JsonUtility gets a chance to default
        // missing fields or repair truncated JSON. Rejected/partial writes never consume an ID.
        public static bool TryRead(string json, string expectedRun, out T12DiagnosticCommand command, out string reason)
        {
            command = null; reason = "INVALID_COMMAND_JSON";
            if (!SafeId(expectedRun) || !FlatJson.TryRead(json, 8192, out var fields)) return false;
            var allowed = new HashSet<string>(new[] { "schema", "id", "runId", "action", "orbId", "otherOrbId", "direction", "count",
                "expectedOwner", "initialTransferCount", "directions", "height", "x", "y", "screenshot", "spendBeforeLaunch", "expectRejected", "roomName", "ip", "port" }, StringComparer.Ordinal);
            if (fields.Keys.Any(k => !allowed.Contains(k)) || !fields.TryGetValue("schema", out var version) || version != "1") return false;
            if (!FlatJson.String(fields, "id", out string id) || !SafeId(id)
                || string.Equals(id, "command", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "status", StringComparison.OrdinalIgnoreCase)
                || !FlatJson.String(fields, "runId", out string run) || run != expectedRun
                || !FlatJson.String(fields, "action", out string action)) return false;
            if (!new[] { "capture", "generate", "combine", "launch", "transfer", "transferSeries", "hostStart", "retry", "lobbyCreate", "joinDirect", "ready" }.Contains(action))
            { reason = "UNSUPPORTED_ACTION"; return false; }
            var c = new T12DiagnosticCommand { id = id, runId = run, action = action };
            if (!ReadOptionalString(fields, "orbId", ref c.orbId) || !ReadOptionalString(fields, "otherOrbId", ref c.otherOrbId)
                || !ReadOptionalString(fields, "roomName", ref c.roomName) || !ReadOptionalString(fields, "ip", ref c.ip) || !ReadOptionalString(fields, "port", ref c.port)
                || !FlatJson.OptionalInt(fields, "direction", ref c.direction) || !FlatJson.OptionalInt(fields, "count", ref c.count)
                || !FlatJson.OptionalUlong(fields, "expectedOwner", ref c.expectedOwner)
                || !FlatJson.OptionalUlong(fields, "initialTransferCount", ref c.initialTransferCount)
                || !FlatJson.OptionalFloat(fields, "height", ref c.height) || !FlatJson.OptionalFloat(fields, "x", ref c.x)
                || !FlatJson.OptionalFloat(fields, "y", ref c.y)) return false;
            if (fields.TryGetValue("screenshot", out var shot)) { if (shot != "true" && shot != "false") return false; c.screenshot = shot == "true"; }
            if (fields.TryGetValue("spendBeforeLaunch", out var spend)) { if (spend != "true" && spend != "false") return false; c.spendBeforeLaunch = spend == "true"; }
            if (fields.TryGetValue("expectRejected", out var reject)) { if (reject != "true" && reject != "false") return false; c.expectRejected = reject == "true"; }
            if (fields.TryGetValue("directions", out var plan))
            {
                if (plan.Length < 2 || plan[0] != '[' || plan[plan.Length - 1] != ']') return false;
                var entries = plan.Substring(1, plan.Length - 2).Trim();
                if (entries.Length > 0)
                {
                    var parts = entries.Split(','); if (parts.Length > 50) return false;
                    c.directions = new int[parts.Length];
                    for (int i = 0; i < parts.Length; i++) if (!FlatJson.Integer(parts[i].Trim(), out c.directions[i])) return false;
                }
            }
            if (c.count < 1 || c.count > 50 || !Normalized(c.height) || !Normalized(c.x) || !Normalized(c.y)) return false;
            if (action == "combine" && (!Identifier(c.orbId) || !Identifier(c.otherOrbId) || c.orbId == c.otherOrbId)) return false;
            if ((action == "launch" || action == "transfer" || action == "transferSeries") && !Identifier(c.orbId)) return false;
            if (action == "transfer" && !Direction(c.direction)) return false;
            if (action == "transferSeries" && (!fields.ContainsKey("expectedOwner") || !fields.ContainsKey("initialTransferCount")
                || !fields.ContainsKey("count") || c.expectedOwner == ulong.MaxValue || c.directions.Length != c.count
                || c.directions.Any(d => !Direction(d)) || c.initialTransferCount > ulong.MaxValue - (ulong)c.count)) return false;
            if (action != "transferSeries" && c.directions.Length != 0) return false;
            if (action != "generate" && action != "transferSeries" && c.count != 1) return false;
            if (c.spendBeforeLaunch && (action != "launch" || c.expectRejected)) return false;
            if (c.expectRejected && action != "launch" && action != "combine" && action != "transfer" && action != "generate") return false;
            command = c; reason = ""; return true;
        }
        private static bool ReadOptionalString(Dictionary<string, string> fields, string name, ref string value)
        { if (!fields.ContainsKey(name)) return true; if (!FlatJson.String(fields, name, out string read) || read.Length > 128) return false; value = read; return true; }
        public static bool Identifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(c => c >= 32 && c != 127);
        public static bool Direction(int value) => value == (int)OrbActionKind.TransferLeft || value == (int)OrbActionKind.TransferRight;
        private static bool Normalized(float n) => !float.IsNaN(n) && !float.IsInfinity(n) && n >= 0 && n <= 1;
    }

    [Serializable]
    public sealed class T12ValidationAck
    {
        public int schema = 1;
        public string runId, commandId, planHash, sessionId, orbId, recordHash;
        public uint roundId;
        public ulong transferCount;
    }

    public static class T12DiagnosticProtocol
    {
        public const string MessageName = "C6.T12.ValidationAck.v1";
        public const int MaximumAckBytes = 4096;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static string Hash(string text)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(text ?? ""))).Replace("-", "").ToLowerInvariant(); }
        public static string RecordHash(OrbWire orb) => Hash(JsonUtility.ToJson(orb));
        public static bool Matches(T12ValidationAck ack, T12DiagnosticCommand command, string session, uint round, ulong sender, ulong peer)
            => ack != null && command != null && command.action == "transferSeries" && sender == peer && ack.schema == 1
            && ack.runId == command.runId && ack.commandId == command.id && ack.planHash == command.PlanHash
            && ack.sessionId == session && ack.roundId == round && ack.orbId == command.orbId
            && ack.transferCount >= command.initialTransferCount && ack.transferCount - command.initialTransferCount <= (ulong)command.count
            && HexHash(ack.recordHash);
        public static bool SameRecord(T12ValidationAck ack, OrbWire orb) => ack != null && orb != null
            && ack.transferCount == orb.transferCount && ack.recordHash == RecordHash(orb);
        public static FastBufferWriter Write(T12ValidationAck ack)
        {
            var bytes = Utf8.GetBytes(JsonUtility.ToJson(ack));
            if (bytes.Length == 0 || bytes.Length > MaximumAckBytes - 4) throw new ArgumentException("Diagnostic ACK too large.");
            var writer = new FastBufferWriter(bytes.Length + 4, Allocator.Temp); writer.WriteValueSafe(bytes.Length); writer.WriteBytesSafe(bytes); return writer;
        }
        public static bool TryRead(FastBufferReader reader, out T12ValidationAck ack)
        {
            ack = null;
            try
            {
                if (reader.Length - reader.Position < 4 || reader.Length - reader.Position > MaximumAckBytes) return false;
                reader.ReadValueSafe(out int size); if (size <= 0 || size != reader.Length - reader.Position || size > MaximumAckBytes - 4) return false;
                var bytes = new byte[size]; reader.ReadBytesSafe(ref bytes, size); var json = Utf8.GetString(bytes);
                if (!FlatJson.TryRead(json, MaximumAckBytes, out var fields) || fields.Count != 9
                    || !fields.TryGetValue("schema", out var v) || v != "1") return false;
                string[] names = { "runId", "commandId", "planHash", "sessionId", "orbId", "recordHash" };
                foreach (var name in names) if (!FlatJson.String(fields, name, out _)) return false;
                ulong count = 0, round = 0;
                if (!fields.ContainsKey("roundId") || !fields.ContainsKey("transferCount")
                    || !FlatJson.OptionalUlong(fields, "roundId", ref round) || round == 0 || round > uint.MaxValue
                    || !FlatJson.OptionalUlong(fields, "transferCount", ref count)) return false;
                ack = JsonUtility.FromJson<T12ValidationAck>(json);
                if (ack == null || !T12DiagnosticCommand.SafeId(ack.runId) || !T12DiagnosticCommand.SafeId(ack.commandId)
                    || !T12DiagnosticCommand.Identifier(ack.sessionId) || !T12DiagnosticCommand.Identifier(ack.orbId)
                    || !HexHash(ack.planHash) || !HexHash(ack.recordHash)) { ack = null; return false; }
                return true;
            }
            catch (Exception) { ack = null; return false; }
        }
        private static bool HexHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }

    // Intentionally only a flat object containing strings, JSON numbers, bools or integer arrays.
    // Nested objects, duplicate keys, invalid escapes and truncated writes cannot become commands.
    internal static class FlatJson
    {
        public static bool TryRead(string text, int maximum, out Dictionary<string, string> fields)
        {
            fields = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text) || Encoding.UTF8.GetByteCount(text) > maximum) return false;
            int i = 0; Space(text, ref i); if (!Take(text, ref i, '{')) return false;
            if (Take(text, ref i, '}')) { Space(text, ref i); return i == text.Length; }
            while (i < text.Length)
            {
                if (!Quoted(text, ref i, out string key) || !Take(text, ref i, ':') || fields.ContainsKey(key)) return false;
                Space(text, ref i); int start = i;
                if (i < text.Length && text[i] == '"') { if (!Quoted(text, ref i, out _)) return false; }
                else if (i < text.Length && text[i] == '[')
                {
                    i++; Space(text, ref i);
                    if (!Take(text, ref i, ']'))
                    {
                        int entries = 0;
                        while (true)
                        {
                            Space(text, ref i); int n = i; if (!Number(text, ref i) || !Integer(text.Substring(n, i - n), out _) || ++entries > 50) return false;
                            if (Take(text, ref i, ']')) break; if (!Take(text, ref i, ',')) return false;
                        }
                    }
                }
                else if (i + 4 <= text.Length && text.Substring(i, 4) == "true") i += 4;
                else if (i + 5 <= text.Length && text.Substring(i, 5) == "false") i += 5;
                else if (!Number(text, ref i)) return false;
                fields.Add(key, text.Substring(start, i - start));
                if (Take(text, ref i, '}')) { Space(text, ref i); return i == text.Length; }
                if (!Take(text, ref i, ',')) return false;
            }
            return false;
        }
        public static bool String(Dictionary<string, string> fields, string key, out string value)
        { value = null; if (!fields.TryGetValue(key, out var raw)) return false; int i = 0; return Quoted(raw, ref i, out value) && i == raw.Length; }
        public static bool Integer(string text, out int value) => int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        public static bool OptionalInt(Dictionary<string, string> f, string k, ref int n) => !f.TryGetValue(k, out var s) || Integer(s, out n);
        public static bool OptionalUlong(Dictionary<string, string> f, string k, ref ulong n) => !f.TryGetValue(k, out var s) || ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out n);
        public static bool OptionalFloat(Dictionary<string, string> f, string k, ref float n) => !f.TryGetValue(k, out var s) || float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
        private static bool Quoted(string s, ref int i, out string value)
        {
            value = null; Space(s, ref i); if (i >= s.Length || s[i++] != '"') return false; var b = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++]; if (c == '"') { value = b.ToString(); return true; } if (c < 32) return false;
                if (c != '\\') { b.Append(c); continue; } if (i >= s.Length) return false; c = s[i++];
                switch (c)
                {
                    case '"': case '\\': case '/': b.Append(c); break;
                    case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break; case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !ushort.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code)) return false;
                        b.Append((char)code); i += 4; break;
                    default: return false;
                }
            }
            return false;
        }
        private static bool Number(string s, ref int i)
        {
            if (i < s.Length && s[i] == '-') i++; if (i >= s.Length) return false;
            if (s[i] == '0') i++; else { if (s[i] < '1' || s[i] > '9') return false; while (i < s.Length && Digit(s[i])) i++; }
            if (i < s.Length && s[i] == '.') { i++; if (i >= s.Length || !Digit(s[i])) return false; while (i < s.Length && Digit(s[i])) i++; }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            { i++; if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++; if (i >= s.Length || !Digit(s[i])) return false; while (i < s.Length && Digit(s[i])) i++; }
            return true;
        }
        private static bool Digit(char c) => c >= '0' && c <= '9';
        private static void Space(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++; }
        private static bool Take(string s, ref int i, char c) { Space(s, ref i); if (i >= s.Length || s[i] != c) return false; i++; return true; }
    }
}
