using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using UnityEngine;

namespace C6.Prototype.Lobby
{
    /// <summary>Bounded UTF-8 JSON; the NGO adapter owns transport framing and authenticating the sender.</summary>
    public static class LobbyWire
    {
        public const int MaximumBytes = 12288;
        public const int MaximumHelloBytes = 512;
        public const int MaximumRequestBytes = 1024;
        public const int MaximumConfigBytes = 4096;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Encode<T>(T packet) where T : class
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(packet));
            if (bytes.Length < 2 || bytes.Length > Limit<T>()) throw new ArgumentException("T10-A packet exceeds its bounded size.");
            return bytes;
        }

        public static bool TryDecode<T>(byte[] bytes, out T packet) where T : class
        {
            packet = null;
            if (bytes == null || bytes.Length < 2 || bytes.Length > Limit<T>()) return false;
            try
            {
                string json = Utf8.GetString(bytes).Trim();
                if (!IsObject(json)) return false;
                packet = JsonUtility.FromJson<T>(json);
                return packet != null;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { packet = null; return false; }
        }

        private static int Limit<T>() => typeof(T) == typeof(LobbyHello) ? MaximumHelloBytes
            : typeof(T) == typeof(LobbyRequest) ? MaximumRequestBytes : MaximumBytes;
        private static bool IsObject(string value) => value != null && value.Length >= 2 && value[0] == '{' && value[value.Length - 1] == '}';
        public static bool ValidId(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty;
        public static bool ValidBuild(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                    || c == '.' || c == '_' || c == '-' || c == '+')) return false;
            return true;
        }
        public static bool ValidConfigJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !IsObject(value.Trim())) return false;
            try { return Utf8.GetByteCount(value) <= MaximumConfigBytes; }
            catch (ArgumentException) { return false; }
        }
        public static string Fingerprint(string value)
        {
            if (!ValidConfigJson(value)) throw new ArgumentException("Host configuration must be bounded JSON.");
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Utf8.GetBytes(value));
                var result = new StringBuilder(64);
                foreach (byte b in hash) result.Append(b.ToString("x2"));
                return result.ToString();
            }
        }
        public static bool ValidFingerprint(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
        public static bool ValidHello(LobbyHello hello) => hello != null && hello.protocol > 0
            && ValidBuild(hello.build) && (string.IsNullOrEmpty(hello.roomId) || ValidId(hello.roomId)) && ValidId(hello.clientNonce);
        public static bool ValidRequest(LobbyRequest request) => request != null && request.protocol > 0
            && ValidId(request.roomId) && ValidId(request.sessionId) && ValidId(request.requestId)
            && request.sequence > 0 && request.revision > 0
            && (request.kind == LobbyProtocol.AckInitial || request.kind == LobbyProtocol.SetReady
                || request.kind == LobbyProtocol.Start || request.kind == LobbyProtocol.Leave)
            && (request.kind == LobbyProtocol.Leave || ValidFingerprint(request.configFingerprint));

        public static bool ValidSnapshot(LobbySnapshot value)
        {
            if (value == null || !LobbyProtocol.IsSupported(value.protocol) || !ValidBuild(value.build)
                || !ValidId(value.roomId) || !ValidId(value.sessionId) || value.revision == 0
                || !ValidConfigJson(value.hostConfigJson) || !ValidFingerprint(value.configFingerprint)
                || value.configFingerprint != Fingerprint(value.hostConfigJson)
                || (!string.IsNullOrEmpty(value.recipientNonce) && !ValidId(value.recipientNonce))
                || value.p2Entries == null || value.p2Entries.Length > 1
                || (value.p2Entries.Length == 1 && value.p2Entries[0] == null)
                || value.startEntries == null || value.startEntries.Length > 1
                || (value.startEntries.Length == 1 && value.startEntries[0] == null)
                || value.p1 == null || value.p1.clientId != LobbyProtocol.HostClientId || value.p1.playerNumber != 1
                || !value.p1.connected || !value.p1.initialStateReceived
                || (value.p2 != null && (value.p2.clientId == LobbyProtocol.HostClientId || value.p2.playerNumber != 2
                    || !value.p2.connected || (value.p2.ready && !value.p2.initialStateReceived)))
                || value.closeReason == null || value.closeReason.Length > 128) return false;
            if (!ValidRoster(value)) return false;
            bool bothReady = value.ParticipantCount >= 2 && value.OrderedPlayers.All(p => p.ready && p.initialStateReceived);
            if (value.canStart != (value.phase == LobbyProtocol.Lobby && bothReady)) return false;
            if (value.phase == LobbyProtocol.Lobby)
                return value.roundId == 0 && value.start == null && value.closeReason.Length == 0;
            if (value.phase == LobbyProtocol.Playing)
                return bothReady && value.closeReason.Length == 0 && ValidStart(value);
            if (value.phase == LobbyProtocol.Closed)
                return value.closeReason.Length != 0 && value.OrderedPlayers.All(p => !p.ready)
                    && (value.roundId == 0 ? value.start == null : ValidStart(value));
            return false;
        }

        private static bool ValidStart(LobbySnapshot value) => value.roundId == 1 && value.start != null
            && value.start.roomId == value.roomId && value.start.sessionId == value.sessionId
            && value.start.roundId == value.roundId && value.start.seed == value.seed
            && value.start.configFingerprint == value.configFingerprint && value.start.hostConfigJson == value.hostConfigJson
            && value.start.continuousTransfers == (value.protocol == LobbyProtocol.ContinuousTransferVersion)
            && (LobbyProtocol.IsMultiparty(value.protocol)
                ? value.start.participantIds != null && value.start.participantIds.SequenceEqual(value.OrderedPlayers.Select(p => p.clientId))
                : value.start.participantIds == null || value.start.participantIds.Length == 0);

        private static bool ValidRoster(LobbySnapshot value)
        {
            if (!LobbyProtocol.IsMultiparty(value.protocol)) return value.players == null || value.players.Length == 0;
            if (value.players == null || value.players.Length < 1 || value.players.Length > LobbyProtocol.MaximumCapacity) return false;
            var ids = new System.Collections.Generic.HashSet<ulong>();
            for (int i = 0; i < value.players.Length; i++)
            {
                var player = value.players[i];
                if (player == null || player.playerNumber != i + 1 || !player.connected || !ids.Add(player.clientId)
                    || (i == 0 ? player.clientId != 0 || !player.initialStateReceived : player.clientId == 0)
                    || (player.ready && !player.initialStateReceived)) return false;
            }
            return SamePlayer(value.p1, value.players[0])
                && SamePlayer(value.p2, value.players.Length > 1 ? value.players[1] : null);
        }

        public static bool SameRoster(LobbySnapshot left, LobbySnapshot right) => left != null && right != null
            && left.OrderedPlayers.Select(p => p.clientId).SequenceEqual(right.OrderedPlayers.Select(p => p.clientId));
        private static bool SamePlayers(LobbySnapshot left, LobbySnapshot right) => SameRoster(left, right)
            && left.OrderedPlayers.Zip(right.OrderedPlayers, SamePlayer).All(same => same);
        private static bool ValidRosterTransition(LobbySnapshot current, LobbySnapshot incoming)
        {
            if (current.protocol != incoming.protocol) return false;
            if (!LobbyProtocol.IsMultiparty(current.protocol))
                return current.p2 == null || (incoming.p2 != null && incoming.p2.clientId == current.p2.clientId
                    && (!current.p2.initialStateReceived || incoming.p2.initialStateReceived));
            if (current.phase == LobbyProtocol.Playing || incoming.phase != LobbyProtocol.Lobby)
                return SameRoster(current, incoming);
            // Surviving IDs retain their order, and successful configuration acknowledgement never rolls back.
            var incomingIds = incoming.OrderedPlayers.Select(p => p.clientId).ToArray();
            var currentIds = current.OrderedPlayers.Select(p => p.clientId).ToArray();
            if (!currentIds.Where(incomingIds.Contains).SequenceEqual(incomingIds.Where(currentIds.Contains))) return false;
            bool seenNew = false;
            foreach (var player in incoming.OrderedPlayers)
            {
                var prior = current.Find(player.clientId);
                if (prior == null) seenNew = true;
                else if (seenNew || (prior.initialStateReceived && !player.initialStateReceived)) return false;
            }
            return true;
        }

        /// <summary>
        /// Validates a receipt for the caller's already matched pending request ID/sequence, without
        /// applying any state. Reliable state broadcasts may have delivered the same or a newer
        /// revision before this receipt. The adapter must authenticate the Host sender separately.
        /// </summary>
        public static bool AcceptsReceipt(LobbySnapshot current, LobbySnapshot receipt, ulong localClientId,
            string expectedBuild, string expectedNonce, string expectedRoomId = "", ulong minimumRevision = 1)
        {
            if (!ValidSnapshot(current) || !ValidSnapshot(receipt) || minimumRevision == 0
                || minimumRevision > current.revision || receipt.revision < minimumRevision || current.build != expectedBuild || receipt.build != expectedBuild
                || current.recipientNonce != expectedNonce || receipt.recipientNonce != expectedNonce
                || (!string.IsNullOrEmpty(expectedRoomId) && receipt.roomId != expectedRoomId)
                || receipt.protocol != current.protocol || receipt.roomId != current.roomId || receipt.sessionId != current.sessionId
                || receipt.seed != current.seed || receipt.hostConfigJson != current.hostConfigJson
                || receipt.configFingerprint != current.configFingerprint) return false;
            var local = current.Find(localClientId);
            var receiptLocal = receipt.Find(localClientId);
            if (local == null || receiptLocal == null || !local.connected || !receiptLocal.connected
                || local.playerNumber != receiptLocal.playerNumber) return false;
            if (receipt.revision > current.revision)
                return AcceptsSnapshot(current, receipt, expectedBuild, expectedNonce, expectedRoomId);
            if (receipt.revision == current.revision)
                return receipt.phase == current.phase && receipt.roundId == current.roundId
                    && receipt.canStart == current.canStart && receipt.closeReason == current.closeReason
                    && SamePlayers(receipt, current);
            // A historical receipt may resolve an intent, but may not claim impossible future state.
            if (receipt.roundId > current.roundId
                || receipt.phase == LobbyProtocol.Closed
                || (receipt.phase == LobbyProtocol.Playing && current.phase == LobbyProtocol.Lobby)
                || !ValidRosterTransition(receipt, current)) return false;
            return true;
        }

        private static bool SamePlayer(LobbyPlayer left, LobbyPlayer right) => left == null ? right == null
            : right != null && left.clientId == right.clientId && left.playerNumber == right.playerNumber
                && left.connected == right.connected && left.initialStateReceived == right.initialStateReceived && left.ready == right.ready;

        public static bool AcceptsSnapshot(LobbySnapshot current, LobbySnapshot incoming, string expectedBuild,
            string expectedNonce, string expectedRoomId = "")
        {
            if (!ValidSnapshot(incoming) || incoming.build != expectedBuild || incoming.recipientNonce != expectedNonce
                || (!string.IsNullOrEmpty(expectedRoomId) && incoming.roomId != expectedRoomId)) return false;
            if (current == null) return true;
            if (incoming.roomId != current.roomId || incoming.sessionId != current.sessionId || incoming.revision <= current.revision
                || incoming.build != current.build || incoming.seed != current.seed || incoming.hostConfigJson != current.hostConfigJson
                || incoming.configFingerprint != current.configFingerprint || current.phase == LobbyProtocol.Closed
                || !ValidRosterTransition(current, incoming)) return false;
            if (current.phase == LobbyProtocol.Playing && incoming.phase != LobbyProtocol.Playing && incoming.phase != LobbyProtocol.Closed) return false;
            if (current.roundId > incoming.roundId) return false;
            return true;
        }
    }
}
