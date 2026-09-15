using System;

namespace C6.Prototype.Lobby
{
    public static class LobbyProtocol
    {
        public const int Version = 10;
        public const int Capacity = 2;
        public const int MultipartyVersion = 23;
        public const int ContinuousTransferVersion = 24;
        public const int MaximumCapacity = 5;
        public static bool IsMultiparty(int protocol) => protocol == MultipartyVersion || protocol == ContinuousTransferVersion;
        public static bool IsSupported(int protocol) => protocol == Version || IsMultiparty(protocol);
        public static int For(int maximumParticipants, bool continuousTransfers = false)
            => continuousTransfers ? ContinuousTransferVersion : maximumParticipants == MaximumCapacity ? MultipartyVersion : Version;
        public const ulong HostClientId = 0;
        public const string Lobby = "Lobby";
        public const string Playing = "Playing";
        public const string Closed = "Closed";
        public const string AckInitial = "AckInitial";
        public const string SetReady = "SetReady";
        public const string Start = "Start";
        public const string Leave = "Leave";
    }

    [Serializable]
    public sealed class LobbyHello
    {
        public int protocol;
        public string build;
        // Empty only for the explicitly selected Direct IP fallback.
        public string roomId;
        public string clientNonce;
    }

    [Serializable]
    public sealed class LobbyRequest
    {
        public int protocol;
        public string roomId;
        public string sessionId;
        public string requestId;
        public ulong sequence;
        public ulong revision;
        public string kind;
        public bool ready;
        public string configFingerprint;
    }

    [Serializable]
    public sealed class LobbyPlayer
    {
        public ulong clientId;
        public int playerNumber;
        public bool connected;
        public bool initialStateReceived;
        public bool ready;
    }

    [Serializable]
    public sealed class LobbyStartContract
    {
        public string roomId;
        public string sessionId;
        public uint roundId;
        public uint seed;
        public string configFingerprint;
        public string hostConfigJson;
        // P3 freezes admission order for the entire battle; legacy contracts leave this empty.
        public ulong[] participantIds = Array.Empty<ulong>();
        public bool continuousTransfers;
    }

    [Serializable]
    public sealed class LobbySnapshot
    {
        public int protocol;
        public string build;
        public string roomId;
        public string sessionId;
        public ulong revision;
        public string phase;
        public uint seed;
        public uint roundId;
        public string hostConfigJson;
        public string configFingerprint;
        public string recipientNonce;
        // P3 canonical roster. p1/p2 remain validated mirrors for historical callers.
        public LobbyPlayer[] players = Array.Empty<LobbyPlayer>();
        public LobbyPlayer p1;
        // JsonUtility expands null inline classes into default objects. Explicit 0/1 arrays preserve
        // optionality on both the wire and direct diagnostic JSON copies without normalizing bad DTOs.
        public LobbyPlayer[] p2Entries;
        public LobbyPlayer p2
        {
            get => p2Entries != null && p2Entries.Length == 1 ? p2Entries[0] : null;
            set => p2Entries = value == null ? Array.Empty<LobbyPlayer>() : new[] { value };
        }
        public bool canStart;
        public string closeReason;
        public LobbyStartContract[] startEntries;
        public LobbyStartContract start
        {
            get => startEntries != null && startEntries.Length == 1 ? startEntries[0] : null;
            set => startEntries = value == null ? Array.Empty<LobbyStartContract>() : new[] { value };
        }

        public int Capacity => LobbyProtocol.IsMultiparty(protocol) ? LobbyProtocol.MaximumCapacity : LobbyProtocol.Capacity;
        public LobbyPlayer[] OrderedPlayers => LobbyProtocol.IsMultiparty(protocol) ? players ?? Array.Empty<LobbyPlayer>()
            : p1 == null ? Array.Empty<LobbyPlayer>() : p2 == null ? new[] { p1 } : new[] { p1, p2 };
        public int ParticipantCount
        {
            get { int count = 0; foreach (var player in OrderedPlayers) if (player != null && player.connected) count++; return count; }
        }
        public LobbyPlayer Find(ulong clientId)
        { foreach (var player in OrderedPlayers) if (player != null && player.clientId == clientId) return player; return null; }
        public int LocalPlayerNumber(ulong clientId) => Find(clientId)?.playerNumber ?? 0;
        public int LeftPlayerNumber(ulong clientId) => NeighbourPlayerNumber(clientId, -1);
        public int RightPlayerNumber(ulong clientId) => NeighbourPlayerNumber(clientId, 1);
        private int NeighbourPlayerNumber(ulong clientId, int direction)
        {
            var roster = OrderedPlayers;
            if (roster.Length < 2) return 0;
            for (int i = 0; i < roster.Length; i++)
                if (roster[i] != null && roster[i].clientId == clientId)
                    return roster[(i + direction + roster.Length) % roster.Length]?.playerNumber ?? 0;
            return 0;
        }

    }

    public static class LobbyCompatibility
    {
        // Run before reserving NGO capacity. No reservation is consumed by rejected payloads.
        public static string Check(LobbyHello hello, string hostBuild, string roomId, string phase, int participantCount, int maximumParticipants = LobbyProtocol.Capacity, bool continuousTransfers = false)
        {
            if (!LobbyWire.ValidHello(hello)) return "INVALID_HELLO";
            if (hello.protocol != LobbyProtocol.For(maximumParticipants, continuousTransfers)) return "PROTOCOL_MISMATCH";
            if (!string.Equals(hello.build, hostBuild, StringComparison.Ordinal)) return "BUILD_MISMATCH";
            if (!string.IsNullOrEmpty(hello.roomId) && hello.roomId != roomId) return "STALE_ROOM";
            if (phase == LobbyProtocol.Playing) return "BATTLE_IN_PROGRESS";
            if (phase != LobbyProtocol.Lobby) return "ROOM_CLOSED";
            if (participantCount >= maximumParticipants) return "ROOM_FULL";
            if (participantCount < 1) return "HOST_NOT_READY";
            return string.Empty;
        }
    }
}
