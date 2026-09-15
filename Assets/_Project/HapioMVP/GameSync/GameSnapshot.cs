using System;
using C6.Prototype.Attack;
using C6.Prototype.Resources;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;

namespace C6.Prototype.GameSync
{
    /// <summary>One committed Host observation. Child revisions remain independent diagnostic values.</summary>
    [Serializable]
    public sealed class GameSnapshot
    {
        public string roomId;
        public string sessionId;
        public uint roundId;
        public ulong revision;
        public string configHash;
        public uint seed;
        public LobbyPlayer p1;
        public LobbyPlayer p2;
        // P3 authoritative frozen seat order. Legacy scenes leave this empty.
        public LobbyPlayer[] players;
        // P4 is a trusted scene capability, negotiated independently from P3.
        public bool continuousTransfers;
        public LobbyPlayer[] OrderedPlayers => players != null && players.Length > 0 ? players : new[] { p1, p2 };
        public bool initialStateConfirmed;
        public double hostNow;
        public double serverTime;
        public AttackSnapshot attack;
        public ResourceSnapshot resources;
        public BattleSnapshot battle;
        public string nonce;
    }

    /// <summary>Trusted connection/start contract, never populated from an unverified game packet.</summary>
    public sealed class GameSnapshotContext
    {
        public string roomId;
        public string sessionId;
        public string configHash;
        public LobbyHostConfig config;
        public uint seed;
        public ulong p1;
        public ulong p2;
        // Trusted P3 contract, captured from the approved lobby Start transaction.
        public ulong[] participantIds;
        public string nonce;
        public uint minimumRoundId = 1;
        // Trusted scene capability. A packet cannot enable transfer support on an older scene.
        public bool allowTransfers;
        public bool continuousTransfers;
        public float transferMaximumSpeed;
        public float transferEdgeInset;
    }
}
