using System;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.Combination.EditModeTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync.EditModeTests")]

namespace C6.Prototype.Combination
{
    [Serializable]
    public sealed class CombinationReply
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public string requestId;
        public string sourceOrbId;
        public string targetOrbId;
        public ulong sequence;
        // Inventory revision observed by the Host while constructing this receipt/query response.
        public ulong inventoryRevision;
        public Vector2 sourcePosition;
        public Vector2 targetPosition;
        public bool known;
        public bool accepted;
        public bool duplicate;
        public string reason;
        public bool sourcePending;
        public bool targetPending;
        // Immutable successful receipt; currentCombined may already have launched or been consumed.
        // JsonUtility serializes an inline null class as an empty object. An explicit
        // zero/one array preserves absence without inventing an authoritative orb.
        public OrbWire[] originalCombinedEntries = Array.Empty<OrbWire>();
        public OrbWire originalCombined
        {
            get => originalCombinedEntries != null && originalCombinedEntries.Length == 1 ? originalCombinedEntries[0] : null;
            set => originalCombinedEntries = value == null ? Array.Empty<OrbWire>() : new[] { value };
        }
        public OrbWire[] currentSourceEntries = Array.Empty<OrbWire>();
        public OrbWire currentSource
        {
            get => currentSourceEntries != null && currentSourceEntries.Length == 1 ? currentSourceEntries[0] : null;
            set => currentSourceEntries = value == null ? Array.Empty<OrbWire>() : new[] { value };
        }
        public OrbWire[] currentTargetEntries = Array.Empty<OrbWire>();
        public OrbWire currentTarget
        {
            get => currentTargetEntries != null && currentTargetEntries.Length == 1 ? currentTargetEntries[0] : null;
            set => currentTargetEntries = value == null ? Array.Empty<OrbWire>() : new[] { value };
        }
        public OrbWire[] currentCombinedEntries = Array.Empty<OrbWire>();
        public OrbWire currentCombined
        {
            get => currentCombinedEntries != null && currentCombinedEntries.Length == 1 ? currentCombinedEntries[0] : null;
            set => currentCombinedEntries = value == null ? Array.Empty<OrbWire>() : new[] { value };
        }
    }

    [Serializable]
    internal sealed class CombinationPacket
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public string requestId;
        public string sourceOrbId;
        public string targetOrbId;
        public ulong sequence;
        public Vector2 sourcePosition;
        public Vector2 targetPosition;

        internal CombinationRequest ToRequest() => new CombinationRequest(sessionId, roundId, requestId,
            sourceOrbId, targetOrbId, sequence, sourcePosition, targetPosition);

        internal static CombinationPacket FromRequest(string nonce, CombinationRequest request) => new CombinationPacket
        {
            nonce = nonce, sessionId = request.SessionId, roundId = request.RoundId,
            requestId = request.RequestId, sourceOrbId = request.SourceOrbId, targetOrbId = request.TargetOrbId,
            sequence = request.SequenceNumber, sourcePosition = request.SourcePosition, targetPosition = request.TargetPosition
        };
    }

    // T08 uses its own small bounded envelope over the existing authenticated NGO connection.
    internal static class CombinationWire
    {
        private const byte Version = 1;
        internal const int MaximumBytes = 8192;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static FastBufferWriter Write<T>(T packet)
        {
            var bytes = Utf8.GetBytes(JsonUtility.ToJson(packet));
            if (bytes.Length == 0 || bytes.Length > MaximumBytes - 5)
                throw new ArgumentException("T08 packet exceeds its bounded size.");
            var writer = new FastBufferWriter(bytes.Length + 5, Allocator.Temp);
            writer.WriteValueSafe(Version);
            writer.WriteValueSafe(bytes.Length);
            writer.WriteBytesSafe(bytes);
            return writer;
        }

        internal static bool TryRead<T>(FastBufferReader reader, out T packet) where T : class
        {
            packet = null;
            int remaining = reader.Length - reader.Position;
            if (remaining < 6 || remaining > MaximumBytes || !reader.TryBeginRead(remaining)) return false;
            try
            {
                reader.ReadValueSafe(out byte version);
                reader.ReadValueSafe(out int length);
                if (version != Version || length < 1 || length != reader.Length - reader.Position) return false;
                var bytes = new byte[length];
                reader.ReadBytesSafe(ref bytes, length);
                string json = Utf8.GetString(bytes);
                if (typeof(T) == typeof(CombinationReply))
                {
                    if (!OrbReplyJsonShape.HasRequiredArrays(json, "originalCombinedEntries", "currentSourceEntries", "currentTargetEntries", "currentCombinedEntries")) return false;
                    // FromJson preserves DTO field initializers when a JSON field is absent.
                    // Overwrite explicit null sentinels so a missing optional array cannot be
                    // mistaken for the authoritative empty array emitted by this protocol.
                    var reply = new CombinationReply { originalCombinedEntries = null, currentSourceEntries = null, currentTargetEntries = null, currentCombinedEntries = null };
                    JsonUtility.FromJsonOverwrite(json, reply);
                    if (!ValidOptionalOrb(reply.originalCombinedEntries) || !ValidOptionalOrb(reply.currentSourceEntries) || !ValidOptionalOrb(reply.currentTargetEntries) || !ValidOptionalOrb(reply.currentCombinedEntries)) return false;
                    packet = reply as T;
                }
                else packet = JsonUtility.FromJson<T>(json);
                return packet != null;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { return false; }
        }

        internal static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        internal static bool ValidNonce(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Normalized(Vector2 value) => Finite(value.x) && Finite(value.y)
            && value.x >= 0 && value.x <= 1 && value.y >= 0 && value.y <= 1;
        internal static bool Exact(Vector2 left, Vector2 right) => left.x.Equals(right.x) && left.y.Equals(right.y);
        internal static bool ValidContext(string nonce, string session, uint round) => ValidNonce(nonce) && ValidId(session) && round > 0;
        internal static bool ValidRequest(CombinationPacket packet) => packet != null
            && ValidContext(packet.nonce, packet.sessionId, packet.roundId) && ValidId(packet.requestId)
            && ValidId(packet.sourceOrbId) && ValidId(packet.targetOrbId) && packet.sequence > 0
            && Normalized(packet.sourcePosition) && Normalized(packet.targetPosition);

        internal static bool Matches(CombinationPacket left, CombinationPacket right) => left != null && right != null
            && left.nonce == right.nonce && left.sessionId == right.sessionId && left.roundId == right.roundId
            && left.requestId == right.requestId && left.sourceOrbId == right.sourceOrbId && left.targetOrbId == right.targetOrbId
            && left.sequence == right.sequence && Exact(left.sourcePosition, right.sourcePosition) && Exact(left.targetPosition, right.targetPosition);

        internal static bool ValidOrb(OrbWire orb) => orb != null && ValidId(orb.id)
            && Enum.IsDefined(typeof(OrbKind), orb.kind) && Enum.IsDefined(typeof(OrbPolarity), orb.polarity)
            && Enum.IsDefined(typeof(OrbAuthorityState), orb.state) && Normalized(orb.pos)
            && (orb.kind == (int)OrbKind.Combined ? orb.polarity == (int)OrbPolarity.None : orb.polarity != (int)OrbPolarity.None);

        internal static bool ValidOptionalOrb(OrbWire[] entries) => entries != null && entries.Length <= 1
            && (entries.Length == 0 || ValidOrb(entries[0]));

        internal static bool ValidReply(CombinationReply reply)
        {
            if (reply == null || !ValidOptionalOrb(reply.originalCombinedEntries) || !ValidOptionalOrb(reply.currentSourceEntries)
                || !ValidOptionalOrb(reply.currentTargetEntries) || !ValidOptionalOrb(reply.currentCombinedEntries)
                || !ValidContext(reply.nonce, reply.sessionId, reply.roundId) || !ValidId(reply.requestId)
                || !ValidId(reply.sourceOrbId) || !ValidId(reply.targetOrbId) || reply.sequence == 0 || reply.inventoryRevision == 0
                || !Normalized(reply.sourcePosition) || !Normalized(reply.targetPosition)
                || reply.reason == null || reply.reason.Length > 128 || reply.accepted && !reply.known)
                return false;
            if (reply.currentSource != null && (!ValidOrb(reply.currentSource) || reply.currentSource.id != reply.sourceOrbId)
                || reply.currentTarget != null && (!ValidOrb(reply.currentTarget) || reply.currentTarget.id != reply.targetOrbId)
                || reply.originalCombined != null && !ValidOrb(reply.originalCombined)
                || reply.currentCombined != null && !ValidOrb(reply.currentCombined)) return false;
            if (!reply.accepted) return reply.originalCombined == null && reply.currentCombined == null;
            return !reply.sourcePending && !reply.targetPending && reply.sourceOrbId != reply.targetOrbId && reply.currentSource != null && reply.currentTarget != null
                && reply.originalCombined != null && reply.currentCombined != null
                && reply.currentSource.kind == (int)OrbKind.Raw && reply.currentTarget.kind == (int)OrbKind.Raw
                && reply.currentSource.polarity != reply.currentTarget.polarity
                && reply.currentSource.state == (int)OrbAuthorityState.Consumed && reply.currentTarget.state == (int)OrbAuthorityState.Consumed
                && reply.currentSource.owner == reply.currentTarget.owner && reply.originalCombined.owner == reply.currentSource.owner
                && reply.currentCombined.owner == reply.currentSource.owner
                && reply.originalCombined.kind == (int)OrbKind.Combined && reply.originalCombined.polarity == (int)OrbPolarity.None
                && reply.originalCombined.state == (int)OrbAuthorityState.Idle
                && reply.currentCombined.kind == (int)OrbKind.Combined && reply.currentCombined.polarity == (int)OrbPolarity.None
                && reply.originalCombined.id == reply.currentCombined.id && reply.originalCombined.id != reply.sourceOrbId
                && reply.originalCombined.id != reply.targetOrbId;
        }

        // Snapshot payloads remain owned/validated by AttackSession. A receipt cannot advance its
        // revision or substitute a partial inventory: the actual snapshot must catch up first.
        internal static bool InventoryConfirmed(CombinationReply reply, AttackSnapshot snapshot) => reply != null
            && reply.known && reply.accepted && reply.inventoryRevision > 0 && snapshot != null
            && snapshot.nonce == reply.nonce && snapshot.sessionId == reply.sessionId && snapshot.roundId == reply.roundId
            && snapshot.revision >= reply.inventoryRevision;

        internal static bool MatchesReply(CombinationPacket pending, CombinationReply reply, ulong owner)
        {
            if (pending == null || !ValidReply(reply) || reply.nonce != pending.nonce || reply.sessionId != pending.sessionId
                || reply.roundId != pending.roundId || reply.requestId != pending.requestId || reply.sourceOrbId != pending.sourceOrbId
                || reply.targetOrbId != pending.targetOrbId || reply.sequence != pending.sequence
                || !Exact(reply.sourcePosition, pending.sourcePosition) || !Exact(reply.targetPosition, pending.targetPosition)) return false;
            return (reply.currentSource == null || reply.currentSource.owner == owner)
                && (reply.currentTarget == null || reply.currentTarget.owner == owner)
                && (reply.originalCombined == null || reply.originalCombined.owner == owner)
                && (reply.currentCombined == null || reply.currentCombined.owner == owner);
        }
    }
}
