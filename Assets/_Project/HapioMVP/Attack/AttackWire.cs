using System;
using System.Text;
using C6.Prototype.Orbs;
using C6.Prototype.Networking;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.Attack.EditModeTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync.EditModeTests")]

namespace C6.Prototype.Attack
{
    // Shared by the three reply decoders. It checks actual JSON tokens before JsonUtility
    // normalizes null/missing arrays; nested or quoted field names cannot satisfy the schema.
    public static class OrbReplyJsonShape
    {
        public static bool HasRequiredArrays(string json, params string[] names)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 65536 || names == null || names.Length == 0) return false;
            var required = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names) if (string.IsNullOrEmpty(name) || !required.Add(name)) return false;
            return new Parser(json, required).Valid();
        }
        private sealed class Parser
        {
            private readonly string text;
            private readonly System.Collections.Generic.HashSet<string> required;
            private readonly System.Collections.Generic.HashSet<string> found = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            private int index;
            internal Parser(string text, System.Collections.Generic.HashSet<string> required) { this.text = text; this.required = required; }
            internal bool Valid()
            {
                if (!Object(0)) return false;
                Space(); return index == text.Length && found.Count == required.Count;
            }
            private bool Object(int depth)
            {
                if (depth > 32 || !Take('{')) return false;
                if (Take('}')) return true;
                while (true)
                {
                    if (!Quoted(out string key) || !Take(':')) return false;
                    Space();
                    if (depth == 0 && required.Contains(key)
                        && (!found.Add(key) || index >= text.Length || text[index] != '[')) return false;
                    if (!Value(depth + 1)) return false;
                    if (Take('}')) return true;
                    if (!Take(',')) return false;
                }
            }
            private bool Array(int depth)
            {
                if (depth > 32 || !Take('[')) return false;
                if (Take(']')) return true;
                while (true)
                {
                    if (!Value(depth + 1)) return false;
                    if (Take(']')) return true;
                    if (!Take(',')) return false;
                }
            }
            private bool Value(int depth)
            {
                if (depth > 32) return false;
                Space(); if (index >= text.Length) return false;
                char next = text[index];
                if (next == '{') return Object(depth);
                if (next == '[') return Array(depth);
                if (next == '"') return Quoted(out _);
                if (next == 't') return Literal("true");
                if (next == 'f') return Literal("false");
                if (next == 'n') return Literal("null");
                return Number();
            }
            private bool Quoted(out string value)
            {
                value = null; Space(); if (index >= text.Length || text[index++] != '"') return false;
                var decoded = new StringBuilder();
                while (index < text.Length)
                {
                    char c = text[index++];
                    if (c == '"') { value = decoded.ToString(); return true; }
                    if (c < 32) return false;
                    if (c != '\\') { decoded.Append(c); continue; }
                    if (index >= text.Length) return false;
                    char escape = text[index++];
                    switch (escape)
                    {
                        case '"': case '\\': case '/': decoded.Append(escape); break;
                        case 'b': decoded.Append('\b'); break;
                        case 'f': decoded.Append('\f'); break;
                        case 'n': decoded.Append('\n'); break;
                        case 'r': decoded.Append('\r'); break;
                        case 't': decoded.Append('\t'); break;
                        case 'u':
                            if (index + 4 > text.Length) return false;
                            int code = 0;
                            for (int n = 0; n < 4; n++)
                            {
                                char hex = text[index++];
                                int digit = hex >= '0' && hex <= '9' ? hex - '0' : hex >= 'a' && hex <= 'f' ? hex - 'a' + 10
                                    : hex >= 'A' && hex <= 'F' ? hex - 'A' + 10 : -1;
                                if (digit < 0) return false; code = code * 16 + digit;
                            }
                            decoded.Append((char)code); break;
                        default: return false;
                    }
                }
                return false;
            }
            private bool Number()
            {
                Space(); if (index < text.Length && text[index] == '-') index++;
                if (index >= text.Length) return false;
                if (text[index] == '0') index++;
                else
                {
                    if (text[index] < '1' || text[index] > '9') return false;
                    while (index < text.Length && Digit(text[index])) index++;
                }
                if (index < text.Length && text[index] == '.')
                {
                    index++; if (index >= text.Length || !Digit(text[index])) return false;
                    while (index < text.Length && Digit(text[index])) index++;
                }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    index++; if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                    if (index >= text.Length || !Digit(text[index])) return false;
                    while (index < text.Length && Digit(text[index])) index++;
                }
                return true;
            }
            private bool Literal(string value)
            {
                if (index + value.Length > text.Length || string.CompareOrdinal(text, index, value, 0, value.Length) != 0) return false;
                index += value.Length; return true;
            }
            private bool Take(char value)
            {
                Space(); if (index >= text.Length || text[index] != value) return false;
                index++; return true;
            }
            private void Space() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
            private static bool Digit(char value) => value >= '0' && value <= '9';
        }
    }

    [Serializable]
    public sealed class OrbWire
    {
        public string id;
        public ulong owner;
        public int kind;
        public int polarity;
        public int state;
        public Vector2 pos;
        public ulong sequence;
        public ulong transferCount, lastTransferSequence, rightTransferCount;
        public int entrySide;
        public bool hasTransferMotion;
        public float transferVelocityX, transferVelocityY;
        public double transferServerTime;
        public OrbRecord ToRecord() => new OrbRecord(id, (OrbKind)kind, (OrbPolarity)polarity,
            owner, (OrbAuthorityState)state, pos, (EntrySide)entrySide, sequence, transferCount, lastTransferSequence, rightTransferCount,
            hasTransferMotion ? new OrbTransferMotion(new Vector2(transferVelocityX, transferVelocityY), transferServerTime) : (OrbTransferMotion?)null);
        public static OrbWire FromRecord(OrbRecord orb) => orb == null ? null : new OrbWire
        {
            id = orb.OrbId, owner = orb.OwnerPlayerId, kind = (int)orb.Kind,
            polarity = (int)orb.Polarity, state = (int)orb.AuthorityState,
            pos = orb.NormalizedPosition, sequence = orb.SequenceNumber, transferCount = orb.TransferCount,
            lastTransferSequence = orb.LastTransferSequence, rightTransferCount = orb.RightTransferCount, entrySide = (int)orb.EntrySide,
            hasTransferMotion = orb.TransferMotion.HasValue, transferVelocityX = orb.TransferMotion?.Velocity.x ?? 0f,
            transferVelocityY = orb.TransferMotion?.Velocity.y ?? 0f, transferServerTime = orb.TransferMotion?.ServerTime ?? 0d
        };
    }

    [Serializable]
    public sealed class ProjectileWire
    {
        public string id;
        public ulong owner;
        public Vector3 position;
        public float radius;
        // P2 presentation samples. Legacy straight-flight snapshots keep every new field zero.
        public bool ballistic;
        public Vector3 velocity, gravity;
        public float elapsed, lifetime;
    }

    [Serializable]
    public sealed class AttackSnapshot
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public ulong revision;
        public int hp;
        public int maxHp;
        public int totalHits;
        public int roundHits;
        public int resets;
        public string state;
        public OrbWire[] orbs = Array.Empty<OrbWire>();
        public ProjectileWire[] projectiles = Array.Empty<ProjectileWire>();
    }

    [Serializable]
    public sealed class AttackRequestReply
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public string requestId;
        public string orbId;
        // T10-B receipts resolve only after their authoritative aggregate reaches this inventory revision.
        public ulong inventoryRevision;
        public bool accepted;
        public bool duplicate;
        public bool known;
        public bool pending;
        public string reason;
        // This is queried CURRENT authority state, never a stale copy of the original launch receipt.
        // JsonUtility serializes an inline null class as an empty object. An explicit
        // zero/one array preserves absence without inventing an authoritative orb.
        public OrbWire[] confirmedOrbEntries = Array.Empty<OrbWire>();
        public OrbWire confirmedOrb
        {
            get => confirmedOrbEntries != null && confirmedOrbEntries.Length == 1 ? confirmedOrbEntries[0] : null;
            set => confirmedOrbEntries = value == null ? Array.Empty<OrbWire>() : new[] { value };
        }
    }

    [Serializable]
    internal sealed class AttackHello { public string nonce; }

    [Serializable]
    internal sealed class AttackRequestPacket
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public string requestId;
        public string orbId;
        public string otherOrbId;
        public int kind;
        public ulong sequence;
        public Vector2 pos;
        public bool hasThrowInput;
        public Vector2 throwDelta;
        public float throwDuration;
        public bool hasTransferMotion;
        public float transferVelocityX, transferVelocityY;
        public double transferServerTime;
        public OrbActionRequest ToRequest() => new OrbActionRequest(sessionId, roundId,
            requestId, orbId, otherOrbId, (OrbActionKind)kind, sequence, pos,
            hasThrowInput ? new OrbThrowInput(throwDelta, throwDuration) : (OrbThrowInput?)null,
            hasTransferMotion ? new OrbTransferMotion(new Vector2(transferVelocityX, transferVelocityY), transferServerTime) : (OrbTransferMotion?)null);
        public static AttackRequestPacket FromRequest(string nonce, OrbActionRequest request) => new AttackRequestPacket
        {
            nonce = nonce, sessionId = request.SessionId, roundId = request.RoundId,
            requestId = request.RequestId, orbId = request.OrbId, otherOrbId = request.OtherOrbId,
            kind = (int)request.Kind, sequence = request.SequenceNumber, pos = request.NormalizedPosition,
            hasThrowInput = request.ThrowInput.HasValue, throwDelta = request.ThrowInput?.Delta ?? Vector2.zero,
            throwDuration = request.ThrowInput?.Duration ?? 0f,
            hasTransferMotion = request.TransferMotion.HasValue, transferVelocityX = request.TransferMotion?.Velocity.x ?? 0f,
            transferVelocityY = request.TransferMotion?.Velocity.y ?? 0f, transferServerTime = request.TransferMotion?.ServerTime ?? 0d
        };
    }

    // T06-only bounded DTO transport. Named handlers authenticate sender before parsing packet data.
    internal static class AttackWire
    {
        private const byte Version = 1;
        internal const int MaximumBytes = 16384;
        // P2 carries actual velocity/gravity/time for up to 64 projectiles as well as their
        // 64 inventory records. Keep requests/replies at 16 KiB; only configured inventory
        // snapshots may use this explicit finite 64 KiB envelope (same as the game aggregate).
        internal const int MaximumInventoryBytes = 65536;
        internal const int MaximumMultiplayerInventoryBytes = ParticipantRing.MaximumSnapshotBytes;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal static FastBufferWriter Write<T>(T packet, int maximumBytes = MaximumBytes)
        {
            var bytes = Utf8.GetBytes(JsonUtility.ToJson(packet));
            if (bytes.Length == 0 || bytes.Length > maximumBytes - 5)
                throw new ArgumentException("T06 packet exceeds its bounded size.");
            var writer = new FastBufferWriter(bytes.Length + 5, Allocator.Temp);
            writer.WriteValueSafe(Version);
            writer.WriteValueSafe(bytes.Length);
            writer.WriteBytesSafe(bytes);
            return writer;
        }

        internal static bool TryRead<T>(FastBufferReader reader, out T packet, int maximumBytes = MaximumBytes) where T : class
        {
            packet = null;
            int remaining = reader.Length - reader.Position;
            if (remaining < 6 || remaining > maximumBytes || !reader.TryBeginRead(remaining)) return false;
            try
            {
                reader.ReadValueSafe(out byte version);
                reader.ReadValueSafe(out int length);
                if (version != Version || length < 1 || length != reader.Length - reader.Position) return false;
                var bytes = new byte[length];
                reader.ReadBytesSafe(ref bytes, length);
                string json = Utf8.GetString(bytes);
                if (typeof(T) == typeof(AttackRequestReply))
                {
                    if (!OrbReplyJsonShape.HasRequiredArrays(json, "confirmedOrbEntries")) return false;
                    // FromJson preserves DTO field initializers when a JSON field is absent.
                    // Overwrite explicit null sentinels so a missing optional array cannot be
                    // mistaken for the authoritative empty array emitted by this protocol.
                    var reply = new AttackRequestReply { confirmedOrbEntries = null };
                    JsonUtility.FromJsonOverwrite(json, reply);
                    if (!ValidOptionalOrb(reply.confirmedOrbEntries)) return false;
                    packet = reply as T;
                }
                else packet = JsonUtility.FromJson<T>(json);
                return packet != null;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { return false; }
        }

        internal static bool ValidNonce(string nonce) => Guid.TryParse(nonce, out var parsed) && parsed != Guid.Empty;
        internal static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        internal static bool Normalized(Vector2 value) => Finite(value.x) && Finite(value.y)
            && value.x >= 0f && value.x <= 1f && value.y >= 0f && value.y <= 1f;

        internal static bool ValidOrb(OrbWire orb) => orb != null && ValidId(orb.id)
            && Enum.IsDefined(typeof(OrbKind), orb.kind) && Enum.IsDefined(typeof(OrbPolarity), orb.polarity)
            && Enum.IsDefined(typeof(OrbAuthorityState), orb.state) && Enum.IsDefined(typeof(EntrySide), orb.entrySide) && Normalized(orb.pos)
            && orb.rightTransferCount <= orb.transferCount
            && ValidTransferMotion(orb)
            && (orb.transferCount == 0 ? orb.lastTransferSequence == 0 && orb.entrySide == (int)EntrySide.None
                : orb.lastTransferSequence >= orb.transferCount && orb.lastTransferSequence <= orb.sequence
                    && (orb.entrySide == (int)EntrySide.Left || orb.entrySide == (int)EntrySide.Right))
            && (orb.kind == (int)OrbKind.Combined ? orb.polarity == (int)OrbPolarity.None : orb.polarity != (int)OrbPolarity.None);

        internal static bool ValidTransferMotion(OrbWire orb)
        {
            if (!orb.hasTransferMotion)
                return orb.transferVelocityX == 0f && orb.transferVelocityY == 0f && orb.transferServerTime == 0d;
            // Absolute serialized bound derives from the supported Config maximum release speed (10).
            // Each active session validates the tighter bound of its own Config before accepting a request.
            return orb.transferCount > 0 && OrbTransferMotion.IsValid(new OrbTransferMotion(
                    new Vector2(orb.transferVelocityX, orb.transferVelocityY), orb.transferServerTime),
                    OrbTransferMotion.MaximumReleaseSpeedMultiplier * 10f)
                && (orb.entrySide == (int)EntrySide.Left ? orb.transferVelocityX >= 0f
                    : orb.entrySide == (int)EntrySide.Right && orb.transferVelocityX <= 0f);
        }

        internal static bool ValidOptionalOrb(OrbWire[] entries) => entries != null && entries.Length <= 1
            && (entries.Length == 0 || ValidOrb(entries[0]));

        internal static bool ValidProjectileMotion(ProjectileWire projectile)
        {
            if (projectile == null || !Finite(projectile.velocity) || !Finite(projectile.gravity)
                || !Finite(projectile.elapsed) || !Finite(projectile.lifetime)) return false;
            if (!projectile.ballistic) return projectile.velocity.Equals(Vector3.zero) && projectile.gravity.Equals(Vector3.zero)
                && projectile.elapsed == 0f && projectile.lifetime == 0f;
            return Finite(projectile.position) && Mathf.Abs(projectile.position.x) <= 100000f
                && Mathf.Abs(projectile.position.y) <= 100000f && Mathf.Abs(projectile.position.z) <= 100000f
                && Finite(projectile.radius) && projectile.radius > 0f && projectile.radius <= 10f
                && projectile.velocity.sqrMagnitude <= 1000000f
                && projectile.gravity.x == 0f && projectile.gravity.z == 0f
                && projectile.gravity.y < 0f && projectile.gravity.y >= -1000f
                && projectile.lifetime > 0f && projectile.lifetime <= 60f
                && projectile.elapsed >= 0f && projectile.elapsed <= projectile.lifetime + .1f;
        }

        internal static bool ValidSnapshot(AttackSnapshot snapshot, int maximumOrbs = 12)
        {
            if (maximumOrbs < 1 || (maximumOrbs > 64 && maximumOrbs != ParticipantRing.MaximumLiveOrbs) || snapshot == null || !ValidNonce(snapshot.nonce) || !ValidId(snapshot.sessionId)
                || snapshot.roundId == 0 || snapshot.revision == 0 || snapshot.maxHp < 1 || snapshot.hp < 0
                || snapshot.hp > snapshot.maxHp || snapshot.totalHits < 0 || snapshot.roundHits < 0
                || snapshot.roundHits > snapshot.totalHits || snapshot.resets < 0
                || !Enum.TryParse<AttackBattleState>(snapshot.state, out var state) || !Enum.IsDefined(typeof(AttackBattleState), state)
                || snapshot.orbs == null || snapshot.orbs.Length > maximumOrbs || snapshot.projectiles == null || snapshot.projectiles.Length > (maximumOrbs > 64 ? ParticipantRing.MaximumFlying : maximumOrbs))
                return false;
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var orbById = new System.Collections.Generic.Dictionary<string, OrbWire>(StringComparer.Ordinal);
            foreach (var orb in snapshot.orbs)
            {
                if (!ValidOrb(orb) || !ids.Add(orb.id)) return false;
                orbById.Add(orb.id, orb);
            }
            ids.Clear();
            foreach (var projectile in snapshot.projectiles)
                if (projectile == null || !ValidId(projectile.id) || !ids.Add(projectile.id)
                    || !Finite(projectile.position) || !Finite(projectile.radius) || projectile.radius <= 0f
                    || !ValidProjectileMotion(projectile)
                    || !orbById.TryGetValue(projectile.id, out var orb) || orb.owner != projectile.owner
                    || orb.kind != (int)OrbKind.Combined || orb.state != (int)OrbAuthorityState.Projectile)
                    return false;
            return true;
        }
    }
}
