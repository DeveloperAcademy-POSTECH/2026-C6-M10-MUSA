using System;
using System.Collections.Generic;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Networking;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.Resources.EditModeTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync.EditModeTests")]

namespace C6.Prototype.Resources
{
    public enum ResourceRequestKind { Generate = 0, DebugCombined = 1 }

    [Serializable]
    public sealed class ResourcePlayerWire
    {
        public ulong playerId;
        public double stamina;
        public int generatedTotal;
        public ulong lastSequence;
        public int storedOrbs;
    }

    [Serializable]
    public sealed class ResourceSnapshot
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public ulong revision;
        public uint seed;
        public bool playing;
        public bool debugTestMode;
        public bool debugToolsEnabled;
        public double maximum;
        public double generateCost;
        public double regenerationRate;
        public double hitRecovery;
        public int storageLimit;
        public ResourcePlayerWire[] players = Array.Empty<ResourcePlayerWire>();
    }

    [Serializable]
    public sealed class ResourceRequestReply
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public string requestId;
        public ulong sequence;
        public int operation;
        // T10-B accepted receipts wait for both confirmed aggregate child revisions.
        public ulong inventoryRevision;
        public ulong resourceRevision;
        public bool known;
        public bool accepted;
        public bool duplicate;
        public string reason;
        public double staminaBefore;
        public double staminaAfter;
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
    internal sealed class ResourceRequestPacket
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public string requestId;
        public ulong sequence;
        public int operation;
    }

    [Serializable]
    internal sealed class ResourceSyncPacket
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
    }

    internal static class ResourceWire
    {
        private const byte Version = 1;
        internal const int MaximumBytes = 8192;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static FastBufferWriter Write<T>(T packet)
        {
            var bytes = Utf8.GetBytes(JsonUtility.ToJson(packet));
            if (bytes.Length == 0 || bytes.Length > MaximumBytes - 5)
                throw new ArgumentException("T07 packet exceeds its bounded size.");
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
                if (typeof(T) == typeof(ResourceRequestReply))
                {
                    if (!OrbReplyJsonShape.HasRequiredArrays(json, "confirmedOrbEntries")) return false;
                    // FromJson preserves DTO field initializers when a JSON field is absent.
                    // Overwrite explicit null sentinels so a missing optional array cannot be
                    // mistaken for the authoritative empty array emitted by this protocol.
                    var reply = new ResourceRequestReply { confirmedOrbEntries = null };
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

        internal static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        internal static bool ValidNonce(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty;
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool ValidContext(string nonce, string session, uint round) =>
            ValidNonce(nonce) && ValidId(session) && round > 0;
        internal static bool ValidRequest(ResourceRequestPacket packet) => packet != null
            && ValidContext(packet.nonce, packet.sessionId, packet.roundId) && ValidId(packet.requestId)
            && packet.sequence > 0 && Enum.IsDefined(typeof(ResourceRequestKind), packet.operation);

        internal static bool ValidOrb(OrbWire orb) => orb != null && ValidId(orb.id)
            && Enum.IsDefined(typeof(OrbKind), orb.kind) && Enum.IsDefined(typeof(OrbPolarity), orb.polarity)
            && Enum.IsDefined(typeof(OrbAuthorityState), orb.state)
            && Finite(orb.pos.x) && Finite(orb.pos.y) && orb.pos.x >= 0 && orb.pos.x <= 1 && orb.pos.y >= 0 && orb.pos.y <= 1
            && (orb.kind == (int)OrbKind.Combined ? orb.polarity == (int)OrbPolarity.None : orb.polarity != (int)OrbPolarity.None);

        internal static bool ValidOptionalOrb(OrbWire[] entries) => entries != null && entries.Length <= 1
            && (entries.Length == 0 || ValidOrb(entries[0]));

        internal static bool ValidReply(ResourceRequestReply reply, double maximum) => reply != null
            && ValidOptionalOrb(reply.confirmedOrbEntries)
            && ValidContext(reply.nonce, reply.sessionId, reply.roundId) && ValidId(reply.requestId)
            && reply.sequence > 0 && Enum.IsDefined(typeof(ResourceRequestKind), reply.operation)
            && reply.reason != null && reply.reason.Length <= 128
            && Finite(maximum) && maximum > 0 && Finite(reply.staminaBefore) && Finite(reply.staminaAfter)
            && reply.staminaBefore >= 0 && reply.staminaAfter >= 0 && reply.staminaBefore <= maximum && reply.staminaAfter <= maximum
            && (!reply.accepted || reply.known && ValidOrb(reply.confirmedOrb))
            && (reply.confirmedOrb == null || ValidOrb(reply.confirmedOrb));

        internal static bool ValidSnapshot(ResourceSnapshot snapshot, int maximumParticipants = 2)
        {
            if (maximumParticipants < 2 || maximumParticipants > ParticipantRing.MaximumPlayers
                || snapshot == null || !ValidContext(snapshot.nonce, snapshot.sessionId, snapshot.roundId)
                || snapshot.revision == 0 || !Finite(snapshot.maximum) || snapshot.maximum <= 0 || snapshot.maximum > 100000
                || !Finite(snapshot.generateCost) || snapshot.generateCost <= 0 || snapshot.generateCost > snapshot.maximum
                || !Finite(snapshot.regenerationRate) || snapshot.regenerationRate < 0 || snapshot.regenerationRate > 100000
                || !Finite(snapshot.hitRecovery) || snapshot.hitRecovery < 0 || snapshot.hitRecovery > snapshot.maximum
                || snapshot.storageLimit < 1 || snapshot.storageLimit > 1000
                || snapshot.players == null || snapshot.players.Length < 1 || snapshot.players.Length > maximumParticipants)
                return false;
            var ids = new HashSet<ulong>();
            foreach (var player in snapshot.players)
                if (player == null || !ids.Add(player.playerId) || !Finite(player.stamina)
                    || player.stamina < 0 || player.stamina > snapshot.maximum || player.generatedTotal < 0
                    || player.generatedTotal > 256 || player.storedOrbs < 0 || player.storedOrbs > snapshot.storageLimit)
                    return false;
            return true;
        }
    }
}
