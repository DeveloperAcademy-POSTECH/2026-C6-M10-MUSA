using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using C6.Prototype.Networking;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.Battle.EditModeTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.GameSync.EditModeTests")]

namespace C6.Prototype.Battle
{
    [Serializable]
    public sealed class BattleSnapshot
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
        public ulong revision;
        public string phase;
        public double startedAt;
        public double deadline;
        public double remaining;
        public double teamHp;
        public double duration;
        public double teamHpDecayPerSecond;
        public int observedMonsterHp;
        public int monsterMaxHp;
        public bool developmentSolo;
        public bool shortDuration;
        public int participants;
        // A client may report its lost connection without inventing a victory/defeat or ticking HP.
        public bool locallyDetectedNetworkError;
    }

    [Serializable]
    internal sealed class BattleSyncPacket
    {
        public string nonce;
        public string sessionId;
        public uint roundId;
    }

    internal static class BattleWire
    {
        private const byte Version = 1;
        internal const int MaximumBytes = 4096;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal static FastBufferWriter Write<T>(T packet)
        {
            var bytes = Utf8.GetBytes(JsonUtility.ToJson(packet));
            if (bytes.Length == 0 || bytes.Length > MaximumBytes - 5) throw new ArgumentException("T09 packet exceeds its bounded size.");
            var writer = new FastBufferWriter(bytes.Length + 5, Allocator.Temp);
            writer.WriteValueSafe(Version); writer.WriteValueSafe(bytes.Length); writer.WriteBytesSafe(bytes);
            return writer;
        }
        internal static bool TryRead<T>(FastBufferReader reader, out T packet) where T : class
        {
            packet = null;
            int remaining = reader.Length - reader.Position;
            if (remaining < 6 || remaining > MaximumBytes || !reader.TryBeginRead(remaining)) return false;
            try
            {
                reader.ReadValueSafe(out byte version); reader.ReadValueSafe(out int length);
                if (version != Version || length < 1 || length != reader.Length - reader.Position) return false;
                var bytes = new byte[length]; reader.ReadBytesSafe(ref bytes, length);
                packet = JsonUtility.FromJson<T>(Utf8.GetString(bytes)); return packet != null;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { return false; }
        }
        internal static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        internal static bool ValidNonce(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty;
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool ValidSync(BattleSyncPacket packet) => packet != null && ValidNonce(packet.nonce)
            && ValidId(packet.sessionId) && packet.roundId > 0;
        internal static bool IsTerminal(BattlePhase phase) => phase == BattlePhase.Victory || phase == BattlePhase.Defeat || phase == BattlePhase.NetworkError;
        internal static bool ValidSnapshot(BattleSnapshot value, int maximumParticipants = 2)
        {
            if (maximumParticipants < 2 || maximumParticipants > ParticipantRing.MaximumPlayers
                || value == null || !ValidNonce(value.nonce) || !ValidId(value.sessionId) || value.roundId == 0 || value.revision == 0
                || !Enum.TryParse<BattlePhase>(value.phase, out var phase) || !Enum.IsDefined(typeof(BattlePhase), phase) || phase == BattlePhase.Boot
                || !Finite(value.duration) || value.duration <= 0 || value.duration > 86400
                || !Finite(value.teamHpDecayPerSecond) || value.teamHpDecayPerSecond <= 0 || value.teamHpDecayPerSecond > 100000
                || !Finite(value.remaining) || value.remaining < 0 || value.remaining > value.duration
                || !Finite(value.teamHp) || value.teamHp < 0 || value.teamHp > value.duration * value.teamHpDecayPerSecond
                || Math.Abs(value.teamHp - value.remaining * value.teamHpDecayPerSecond) > 1e-6
                || !Finite(value.startedAt) || !Finite(value.deadline) || value.startedAt < 0 || value.deadline < 0
                || value.monsterMaxHp < 1 || value.observedMonsterHp < 0 || value.observedMonsterHp > value.monsterMaxHp
                || value.participants < 1 || value.participants > maximumParticipants || value.locallyDetectedNetworkError) return false;
            bool started = phase == BattlePhase.Playing || phase == BattlePhase.Victory || phase == BattlePhase.Defeat;
            if (started && (value.deadline <= value.startedAt || Math.Abs(value.deadline - value.startedAt - value.duration) > 1e-6)) return false;
            if (phase == BattlePhase.Lobby || phase == BattlePhase.Ready)
            {
                if (value.startedAt != 0 || value.deadline != 0 || value.remaining != value.duration || value.observedMonsterHp != value.monsterMaxHp) return false;
                if (phase == BattlePhase.Ready && value.participants < 2 && !value.developmentSolo) return false;
            }
            if (phase == BattlePhase.Playing && (value.remaining <= 0 || value.observedMonsterHp <= 0)) return false;
            if (phase == BattlePhase.Victory && (value.remaining <= 0 || value.observedMonsterHp != 0)) return false;
            if (phase == BattlePhase.Defeat && value.remaining != 0) return false;
            return true;
        }

        internal static bool AcceptsSnapshot(BattleSnapshot current, BattleSnapshot incoming,
            string expectedNonce, string expectedSession, uint expectedRound, int maximumParticipants = 2)
        {
            if (!ValidSnapshot(incoming, maximumParticipants) || incoming.nonce != expectedNonce || incoming.sessionId != expectedSession || incoming.roundId != expectedRound) return false;
            if (current == null || current.sessionId != incoming.sessionId || current.roundId != incoming.roundId) return true;
            if (incoming.revision <= current.revision) return false;
            if (Enum.TryParse<BattlePhase>(current.phase, out var previous) && IsTerminal(previous)
                && (incoming.phase != current.phase || incoming.remaining != current.remaining || incoming.teamHp != current.teamHp
                    || incoming.observedMonsterHp != current.observedMonsterHp || incoming.deadline != current.deadline
                    || incoming.startedAt != current.startedAt || incoming.duration != current.duration)) return false;
            return true;
        }
    }
}
