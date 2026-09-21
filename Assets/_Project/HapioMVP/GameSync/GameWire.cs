using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Orbs;
using C6.Prototype.Resources;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>Transport and cross-service validation only; gameplay still belongs to T06–T09 authorities.</summary>
    public static class GameWire
    {
        public const int MaximumBytes = 65536;
        public const byte Version = 11;
        public const byte MultipartyVersion = 23;
        public const byte ContinuousTransferVersion = 24;
        public const int MaximumMultipartyBytes = 327680;
        public const int MaximumMultipartyOrbs = 200;
        public const double LogicalTolerance = 0.000001d;
        public const double LiveDisplayToleranceSeconds = 1d;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static FastBufferWriter Write(GameSnapshot value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.continuousTransfers && !IsMultiparty(value)) throw new ArgumentException("Continuous transfers require a frozen multiparty roster.");
            byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(value));
            int maximumBytes = IsMultiparty(value) ? MaximumMultipartyBytes : MaximumBytes;
            if (bytes.Length == 0 || bytes.Length > maximumBytes - 5)
                throw new ArgumentException("Game snapshot exceeds its bounded envelope.");
            var writer = new FastBufferWriter(bytes.Length + 5, Allocator.Temp);
            writer.WriteValueSafe(value.continuousTransfers ? ContinuousTransferVersion : IsMultiparty(value) ? MultipartyVersion : Version); writer.WriteValueSafe(bytes.Length); writer.WriteBytesSafe(bytes);
            return writer;
        }

        public static bool TryRead(FastBufferReader reader, out GameSnapshot value)
        {
            value = null;
            int remaining = reader.Length - reader.Position;
            if (remaining < 6 || remaining > MaximumMultipartyBytes || !reader.TryBeginRead(remaining)) return false;
            try
            {
                reader.ReadValueSafe(out byte version); reader.ReadValueSafe(out int length);
                if ((version != Version && version != MultipartyVersion && version != ContinuousTransferVersion) || length < 1 || length != reader.Length - reader.Position
                    || remaining > (version == Version ? MaximumBytes : MaximumMultipartyBytes)) return false;
                var bytes = new byte[length]; reader.ReadBytesSafe(ref bytes, length);
                value = JsonUtility.FromJson<GameSnapshot>(Utf8.GetString(bytes));
                return value != null && IsMultiparty(value) == (version != Version)
                    && value.continuousTransfers == (version == ContinuousTransferVersion);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { return false; }
        }

        /// <summary>Call before payload application: only a Client may receive Host authority state.</summary>
        public static bool AcceptFromSender(ulong sender, bool receiverIsHost, GameSnapshot incoming,
            GameSnapshotContext expected, GameSnapshot previous, out string reason)
        {
            if (receiverIsHost || sender != NetworkManager.ServerClientId) return Reject("SNAPSHOT_SENDER", out reason);
            return Validate(incoming, expected, previous, out reason);
        }

        public static bool Validate(GameSnapshot incoming, GameSnapshotContext expected,
            GameSnapshot previous, out string reason)
        {
            reason = string.Empty;
            if (!ValidExpected(expected)) return Reject("INVALID_EXPECTED_CONTEXT", out reason);
            if (incoming == null) return Reject("MISSING_SNAPSHOT", out reason);
            if (incoming.roomId != expected.roomId || incoming.sessionId != expected.sessionId
                || incoming.nonce != expected.nonce || incoming.roundId < expected.minimumRoundId
                || incoming.roundId == 0 || incoming.revision == 0)
                return Reject("SNAPSHOT_CONTEXT", out reason);
            if (incoming.continuousTransfers != expected.continuousTransfers) return Reject("TRANSFER_MODE_MISMATCH", out reason);
            if (incoming.configHash != expected.configHash || incoming.seed != expected.seed)
                return Reject("CONFIG_OR_SEED_MISMATCH", out reason);
            if (!Finite(incoming.hostNow) || !Finite(incoming.serverTime) || incoming.hostNow < 0 || incoming.serverTime < 0)
                return Reject("INVALID_CLOCK_SAMPLE", out reason);
            if (!ValidPlayers(incoming, expected))
                return Reject("PLAYER_CONTRACT", out reason);
            int capacity = IsMultiparty(expected) ? 5 : 2;
            if (!AttackWire.ValidSnapshot(incoming.attack, IsMultiparty(expected) ? MaximumMultipartyOrbs : 64)
                || !ResourceWire.ValidSnapshot(incoming.resources, capacity)
                || !BattleWire.ValidSnapshot(incoming.battle, capacity)) return Reject("INVALID_COMPONENT_SNAPSHOT", out reason);
            if (!SameContext(incoming, incoming.attack.sessionId, incoming.attack.roundId, incoming.attack.nonce)
                || !SameContext(incoming, incoming.resources.sessionId, incoming.resources.roundId, incoming.resources.nonce)
                || !SameContext(incoming, incoming.battle.sessionId, incoming.battle.roundId, incoming.battle.nonce))
                return Reject("COMPONENT_CONTEXT_MISMATCH", out reason);

            var a = incoming.attack; var r = incoming.resources; var b = incoming.battle; var c = expected.config;
            var participantIds = ExpectedPlayers(expected);
            if (r.players.Length != participantIds.Length || participantIds.Any(id => r.players.Count(p => p.playerId == id) != 1)
                || b.participants != participantIds.Length)
                return Reject("RESOURCE_PLAYER_SET", out reason);
            if (a.orbs.Any(orb => !ExpectedOwner(orb.owner, expected))
                || a.projectiles.Any(projectile => !ExpectedOwner(projectile.owner, expected)))
                return Reject("INVALID_ORB_OWNER", out reason);
            foreach (var orb in a.orbs)
                if (!ValidTransferMetadata(orb, expected, out reason)
                    || !ValidTransferMotion(orb, expected, incoming.serverTime, out reason)) return false;
            if (IsMultiparty(expected) && r.players.Any(player => a.orbs.Count(orb => orb.owner == player.playerId
                && (orb.state == (int)OrbAuthorityState.Launching || orb.state == (int)OrbAuthorityState.Projectile)) > 20))
                return Reject("PROJECTILE_PLAYER_CAPACITY", out reason);
            foreach (var player in r.players)
                if (player.storedOrbs != a.orbs.Count(orb => orb.owner == player.playerId
                    && (orb.state == (int)OrbAuthorityState.Idle || orb.state == (int)OrbAuthorityState.Launching)))
                    return Reject("STORAGE_COUNT_MISMATCH", out reason);
            if (a.orbs.Count(orb => orb.state == (int)OrbAuthorityState.Projectile) != a.projectiles.Length)
                return Reject("PROJECTILE_SET_MISMATCH", out reason);
            if (a.orbs.Any(orb => orb.kind == (int)OrbKind.Raw
                && (orb.state == (int)OrbAuthorityState.Launching || orb.state == (int)OrbAuthorityState.Projectile)))
                return Reject("RAW_PROJECTILE_STATE", out reason);
            // #29: multiparty rooms use the Host's per-player HP table for the frozen roster size.
            int expectedHp = IsMultiparty(expected) ? c.MonsterHpFor(participantIds.Length) : c.monsterHp;
            if (a.maxHp != expectedHp || b.monsterMaxHp != expectedHp || a.hp != b.observedMonsterHp
                || r.seed != expected.seed || r.storageLimit != c.storageLimit
                || !Close(r.maximum, c.staminaMax) || !Close(r.generateCost, c.generateCost)
                || !Close(r.regenerationRate, c.recoveryAmount / c.recoverySeconds) || !Close(r.hitRecovery, c.hitRecovery)
                || !Close(b.duration, c.duration) || !Close(b.teamHpDecayPerSecond, c.teamHpDecay)
                || a.projectiles.Any(p => !Close(p.radius, c.projectileRadius) || p.ballistic &&
                    (!Close(p.gravity.y, -c.throwGravity) || !Close(p.lifetime, c.throwLifetime)))
                || b.developmentSolo || b.shortDuration || r.debugTestMode)
                return Reject("CONFIG_OR_COMPONENT_VALUE_MISMATCH", out reason);

            bool ready = b.phase == BattlePhase.Ready.ToString() || b.phase == BattlePhase.Lobby.ToString();
            bool playing = b.phase == BattlePhase.Playing.ToString();
            bool terminal = BattleWire.IsTerminal((BattlePhase)Enum.Parse(typeof(BattlePhase), b.phase));
            if (ready)
            {
                if (a.orbs.Length != 0 || a.projectiles.Length != 0 || a.hp != expectedHp || a.roundHits != 0
                    || a.state != AttackBattleState.Playing.ToString() || r.playing
                    || !Close(b.remaining, c.duration) || !Close(b.teamHp, c.duration * c.teamHpDecay)
                    || r.players.Any(p => !Close(p.stamina, c.staminaStart) || p.generatedTotal != 0
                        || p.storedOrbs != 0 || p.lastSequence != 0))
                    return Reject("INITIAL_STATE_MISMATCH", out reason);
            }
            if (playing && (!incoming.initialStateConfirmed || incoming.OrderedPlayers.Any(player => !player.ready || !player.initialStateReceived)))
                return Reject("INITIAL_STATE_NOT_CONFIRMED", out reason);
            if (playing && a.state != AttackBattleState.Playing.ToString())
                return Reject("PLAYING_STATE_MISMATCH", out reason);
            // ResourceSession may pause recovery while the shared battle deadline keeps elapsing.
            // Its false playing flag is an authoritative pause, never client-side resource ticking.
            if (terminal && (r.playing || a.projectiles.Length != 0
                || a.state != AttackBattleState.Ended.ToString() && a.state != AttackBattleState.NetworkError.ToString()
                    && !(b.phase == BattlePhase.Victory.ToString() && a.state == AttackBattleState.TargetCleared.ToString())))
                return Reject("TERMINAL_STATE_MISMATCH", out reason);

            if (previous != null)
            {
                if (previous.attack == null || previous.resources == null || previous.battle == null
                    || previous.attack.orbs == null || previous.attack.projectiles == null || previous.resources.players == null)
                    return Reject("INVALID_PREVIOUS_SNAPSHOT", out reason);
                if (previous.roomId != incoming.roomId || previous.sessionId != incoming.sessionId
                    || previous.configHash != incoming.configHash || previous.seed != incoming.seed
                    || previous.continuousTransfers != incoming.continuousTransfers)
                    return Reject("PREVIOUS_CONTEXT_MISMATCH", out reason);
                if (incoming.roundId < previous.roundId) return Reject("OLD_ROUND", out reason);
                if (incoming.hostNow + LogicalTolerance < previous.hostNow || incoming.serverTime + LogicalTolerance < previous.serverTime)
                    return Reject("CLOCK_SAMPLE_REGRESSION", out reason);
                if (incoming.roundId == previous.roundId)
                {
                    if (incoming.revision <= previous.revision) return Reject("OLD_REVISION", out reason);
                    if (!SameRoundProgression(previous, incoming, expected, out reason)) return false;
                }
                else if (!ready) return Reject("NEW_ROUND_REQUIRES_INITIAL_STATE", out reason);
                if (a.totalHits < previous.attack.totalHits || a.resets < previous.attack.resets)
                    return Reject("SESSION_COUNTER_REGRESSION", out reason);
            }
            return true;
        }

        private static bool SameRoundProgression(GameSnapshot previous, GameSnapshot incoming,
            GameSnapshotContext expected, out string reason)
        {
            reason = string.Empty;
            var before = previous.battle; var after = incoming.battle;
            if (incoming.attack.revision < previous.attack.revision || incoming.resources.revision < previous.resources.revision
                || after.revision < before.revision || incoming.attack.roundHits < previous.attack.roundHits
                || incoming.attack.hp > previous.attack.hp)
                return Reject("COMPONENT_REVISION_REGRESSION", out reason);
            if (incoming.attack.revision == previous.attack.revision && AttackContent(incoming.attack) != AttackContent(previous.attack)
                || incoming.resources.revision == previous.resources.revision && ResourceContent(incoming.resources) != ResourceContent(previous.resources)
                || after.revision == before.revision && BattleContent(after) != BattleContent(before))
                return Reject("COMPONENT_CHANGED_WITHOUT_REVISION", out reason);
            if (previous.initialStateConfirmed && !incoming.initialStateConfirmed)
                return Reject("INITIAL_CONFIRMATION_REGRESSION", out reason);
            if (before.phase == BattlePhase.Playing.ToString()
                && (after.phase == BattlePhase.Ready.ToString() || after.phase == BattlePhase.Lobby.ToString()))
                return Reject("PHASE_REGRESSION", out reason);
            if (before.startedAt > 0 && (!Close(before.startedAt, after.startedAt) || !Close(before.deadline, after.deadline)
                || after.remaining > before.remaining + LogicalTolerance)) return Reject("DEADLINE_REGRESSION", out reason);
            bool wasTerminal = Enum.TryParse<BattlePhase>(before.phase, out var phase) && BattleWire.IsTerminal(phase);
            if (wasTerminal && (after.phase != before.phase || !Close(after.remaining, before.remaining)
                || !Close(after.teamHp, before.teamHp) || after.observedMonsterHp != before.observedMonsterHp
                || !Close(after.startedAt, before.startedAt) || !Close(after.deadline, before.deadline)
                || !SameResources(previous.resources, incoming.resources)
                || AttackContent(previous.attack) != AttackContent(incoming.attack))) return Reject("TERMINAL_RESULT_MUTATION", out reason);
            var older = previous.attack.orbs.ToDictionary(orb => orb.id, StringComparer.Ordinal);
            foreach (var orb in incoming.attack.orbs)
            {
                if (!older.TryGetValue(orb.id, out var prior)) continue;
                if (orb.kind != prior.kind || orb.polarity != prior.polarity || orb.state < prior.state || orb.sequence < prior.sequence)
                    return Reject("ORB_IDENTITY_OR_STATE_REGRESSION", out reason);
                if (!ValidTransferProgression(prior, orb, expected, out reason)) return false;
            }
            foreach (var player in incoming.resources.players)
            {
                var prior = previous.resources.players.FirstOrDefault(value => value.playerId == player.playerId);
                if (prior == null || player.generatedTotal < prior.generatedTotal || player.lastSequence < prior.lastSequence)
                    return Reject("PLAYER_COUNTER_REGRESSION", out reason);
            }
            return true;
        }

        private static bool ValidTransferMetadata(OrbWire orb, GameSnapshotContext expected, out string reason)
        {
            reason = string.Empty;
            if (orb.transferCount == 0)
                return orb.lastTransferSequence == 0 && orb.entrySide == (int)EntrySide.None && orb.rightTransferCount == 0
                    || Reject("TRANSFER_METADATA_WITHOUT_TRANSFER", out reason);
            if (!expected.allowTransfers) return Reject("TRANSFERS_DISABLED", out reason);
            if (IsMultiparty(expected) && (orb.rightTransferCount > orb.transferCount
                || orb.entrySide == (int)EntrySide.Left && orb.rightTransferCount == 0
                || orb.entrySide == (int)EntrySide.Right && orb.rightTransferCount == orb.transferCount))
                return Reject("INVALID_DIRECTIONAL_TRANSFER_COUNT", out reason);
            if (orb.entrySide != (int)EntrySide.Left && orb.entrySide != (int)EntrySide.Right
                || orb.lastTransferSequence == 0 || orb.lastTransferSequence > orb.sequence
                || orb.transferCount > orb.lastTransferSequence)
                return Reject("INVALID_TRANSFER_METADATA", out reason);
            // Local dragging never changes the authority position. An Idle transferred record is
            // therefore still at its most recently approved entry, even when several snapshots skip.
            if (orb.state == (int)OrbAuthorityState.Idle
                && (orb.lastTransferSequence != orb.sequence || !Close(orb.pos.x,
                    orb.entrySide == (int)EntrySide.Left ? expected.transferEdgeInset : 1f - expected.transferEdgeInset)))
                return Reject("INVALID_TRANSFER_ENTRY", out reason);
            return true;
        }

        private static bool ValidTransferMotion(OrbWire orb, GameSnapshotContext expected, double serverTime, out string reason)
        {
            reason = string.Empty;
            if (!orb.hasTransferMotion)
                return orb.transferVelocityX == 0f && orb.transferVelocityY == 0f && orb.transferServerTime == 0d
                    && (!expected.continuousTransfers || orb.transferCount == 0)
                    || Reject("TRANSFER_MOTION_REQUIRED_OR_HIDDEN", out reason);
            if (!expected.continuousTransfers || orb.transferCount == 0)
                return Reject("TRANSFER_MOTION_DISABLED_OR_UNTRANSFERRED", out reason);
            if (!Finite(orb.transferVelocityX) || !Finite(orb.transferVelocityY) || !Finite(orb.transferServerTime)
                || orb.transferServerTime < 0d || orb.transferServerTime > serverTime + OrbTransferMotion.FutureClockTolerance
                || (double)orb.transferVelocityX * orb.transferVelocityX + (double)orb.transferVelocityY * orb.transferVelocityY
                    > (double)expected.transferMaximumSpeed * expected.transferMaximumSpeed + LogicalTolerance)
                return Reject("INVALID_TRANSFER_MOTION", out reason);
            // The authority may decay a crossing to rest while it is in transit. Preserve the receipt
            // forever, including after launch; old approval timestamps are not stale snapshot errors.
            if (orb.transferVelocityX > 0f && orb.entrySide != (int)EntrySide.Left
                || orb.transferVelocityX < 0f && orb.entrySide != (int)EntrySide.Right)
                return Reject("TRANSFER_MOTION_DIRECTION", out reason);
            return true;
        }

        private static bool SameTransferMotion(OrbWire left, OrbWire right) => left.hasTransferMotion == right.hasTransferMotion
            && Close(left.transferVelocityX, right.transferVelocityX) && Close(left.transferVelocityY, right.transferVelocityY)
            && Close(left.transferServerTime, right.transferServerTime);

        private static bool ValidTransferProgression(OrbWire previous, OrbWire incoming,
            GameSnapshotContext expected, out string reason)
        {
            reason = string.Empty;
            if (!expected.allowTransfers)
                return incoming.owner == previous.owner || Reject("ORB_IDENTITY_OR_STATE_REGRESSION", out reason);
            if (incoming.transferCount < previous.transferCount)
                return Reject("TRANSFER_COUNT_REGRESSION", out reason);
            ulong transfers = incoming.transferCount - previous.transferCount;
            if (transfers == 0)
                return incoming.owner == previous.owner && incoming.entrySide == previous.entrySide
                    && incoming.lastTransferSequence == previous.lastTransferSequence
                    && incoming.rightTransferCount == previous.rightTransferCount && SameTransferMotion(incoming, previous)
                    || Reject("OWNER_CHANGED_WITHOUT_TRANSFER", out reason);
            if (expected.continuousTransfers && previous.hasTransferMotion
                && incoming.transferServerTime + LogicalTolerance < previous.transferServerTime)
                return Reject("TRANSFER_MOTION_TIME_REGRESSION", out reason);
            // Count parity works with missed revisions and with A -> B -> A. Requiring the next
            // visible record to be Idle would wrongly reject transfer followed by an immediate launch.
            if (previous.state != (int)OrbAuthorityState.Idle || incoming.lastTransferSequence <= previous.sequence
                || incoming.lastTransferSequence - previous.sequence < transfers)
                return Reject("INVALID_TRANSFER_PROGRESSION", out reason);
            if (IsMultiparty(expected))
            {
                if (incoming.rightTransferCount < previous.rightTransferCount
                    || previous.rightTransferCount > previous.transferCount
                    || incoming.transferCount - incoming.rightTransferCount < previous.transferCount - previous.rightTransferCount)
                    return Reject("DIRECTIONAL_TRANSFER_COUNT_REGRESSION", out reason);
                ulong right = incoming.rightTransferCount - previous.rightTransferCount;
                ulong left = transfers - right;
                if (incoming.entrySide == (int)EntrySide.Left && right == 0
                    || incoming.entrySide == (int)EntrySide.Right && left == 0)
                    return Reject("LAST_TRANSFER_DIRECTION_MISMATCH", out reason);
                var roster = expected.participantIds;
                int index = Array.IndexOf(roster, previous.owner);
                if (index < 0) return Reject("INVALID_PREVIOUS_ORB_OWNER", out reason);
                // Modulo each unsigned count before subtraction: valid ulong counters cannot overflow.
                int nextIndex = (index + (int)(right % (ulong)roster.Length)
                    - (int)(left % (ulong)roster.Length) + roster.Length) % roster.Length;
                return incoming.owner == roster[nextIndex] || Reject("TRANSFER_RING_OWNER", out reason);
            }
            ulong nextOwner = transfers % 2 == 0 ? previous.owner
                : previous.owner == expected.p1 ? expected.p2 : expected.p1;
            return incoming.owner == nextOwner || Reject("TRANSFER_OWNER_PARITY", out reason);
        }

        private static bool SameResources(ResourceSnapshot left, ResourceSnapshot right)
        {
            if (left.playing != right.playing || left.players.Length != right.players.Length) return false;
            foreach (var player in left.players)
            {
                var other = right.players.FirstOrDefault(p => p.playerId == player.playerId);
                if (other == null || !Close(player.stamina, other.stamina) || player.generatedTotal != other.generatedTotal
                    || player.lastSequence != other.lastSequence || player.storedOrbs != other.storedOrbs) return false;
            }
            return true;
        }

        /// <summary>Stable comparison at the same aggregate revision. Recipient nonces are transport context.</summary>
        public static string CanonicalHash(GameSnapshot snapshot)
        {
            if (snapshot == null || snapshot.attack == null || snapshot.resources == null || snapshot.battle == null
                || snapshot.attack.orbs == null || snapshot.attack.projectiles == null || snapshot.resources.players == null)
                throw new ArgumentException("A complete game snapshot is required.", nameof(snapshot));
            // JsonUtility can move a double by one ULP on each text round trip. Serializing a copy
            // does not establish a fixed point. Encode the already declared 1e-6 logical precision
            // directly, without changing live values or reducing ID/integer/revision precision.
            var canonical = new LogicalEncoder();
            canonical.Text(snapshot.continuousTransfers ? "C6-GAME-LOGICAL-1E-6-P4" : IsMultiparty(snapshot) ? "C6-GAME-LOGICAL-1E-6-P3" : "C6-GAME-LOGICAL-1E-6-V2");
            canonical.Text(snapshot.roomId); canonical.Text(snapshot.sessionId);
            canonical.Unsigned(snapshot.roundId); canonical.Unsigned(snapshot.revision);
            canonical.Text(snapshot.configHash); canonical.Unsigned(snapshot.seed);
            if (IsMultiparty(snapshot))
            {
                canonical.Integer(snapshot.players.Length);
                foreach (var player in snapshot.players) canonical.Player(player);
            }
            else { canonical.Player(snapshot.p1); canonical.Player(snapshot.p2); }
            canonical.Boolean(snapshot.initialStateConfirmed);
            canonical.Number(snapshot.hostNow); canonical.Number(snapshot.serverTime);
            canonical.Attack(snapshot.attack, true); canonical.Resources(snapshot.resources, true);
            canonical.Battle(snapshot.battle, true);
            byte[] bytes = Utf8.GetBytes(canonical.ToString());
            if (bytes.Length > (IsMultiparty(snapshot) ? MaximumMultipartyBytes : MaximumBytes) - 5) throw new ArgumentException("Game snapshot exceeds its bounded envelope.");
            using (var sha = SHA256.Create())
            {
                var text = new StringBuilder(64);
                foreach (byte value in sha.ComputeHash(bytes)) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }

        private static string AttackContent(AttackSnapshot value)
        {
            var canonical = new LogicalEncoder(); canonical.Attack(value, false); return canonical.ToString();
        }
        private static string ResourceContent(ResourceSnapshot value)
        {
            var canonical = new LogicalEncoder(); canonical.Resources(value, false); return canonical.ToString();
        }
        private static string BattleContent(BattleSnapshot value)
        {
            var canonical = new LogicalEncoder(); canonical.Battle(value, false); return canonical.ToString();
        }

        /// <summary>
        /// Fixed schema with typed, length-delimited tokens. Strings are not trimmed or case-folded;
        /// integer tokens never pass through double. Float/double tokens use six decimal places,
        /// the precision declared before validation runs, with a culture-independent decimal point.
        /// Nonces are intentionally absent at every level. Array counts and stable ID order preserve
        /// boundaries without relying on JsonUtility's platform-dependent numeric text conversion.
        /// </summary>
        private sealed class LogicalEncoder
        {
            private readonly StringBuilder content = new StringBuilder();
            internal void Text(string value)
            {
                if (value == null) { content.Append("n;"); return; }
                content.Append('s').Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
            }
            internal void Boolean(bool value) => content.Append(value ? "b1;" : "b0;");
            internal void Integer(int value) => content.Append('i').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
            internal void Unsigned(ulong value) => content.Append('u').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
            internal void Number(double value)
            {
                if (!Finite(value)) throw new ArgumentException("Canonical logical numbers must be finite.");
                double rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
                if (rounded == 0d) rounded = 0d; // Equal logical zero includes negative zero.
                content.Append('d').Append(rounded.ToString("F6", CultureInfo.InvariantCulture)).Append(';');
            }
            internal void Player(LobbyPlayer player)
            {
                if (player == null) throw new ArgumentException("A canonical snapshot requires both players.");
                Text("player"); Unsigned(player.clientId); Integer(player.playerNumber);
                Boolean(player.connected); Boolean(player.initialStateReceived); Boolean(player.ready);
            }
            internal void Attack(AttackSnapshot value, bool includeRevision)
            {
                Text("attack"); Text(value.sessionId); Unsigned(value.roundId);
                if (includeRevision) Unsigned(value.revision);
                Integer(value.hp); Integer(value.maxHp); Integer(value.totalHits);
                Integer(value.roundHits); Integer(value.resets); Text(value.state);
                Integer(value.orbs.Length);
                foreach (var orb in value.orbs.OrderBy(orb => orb.id, StringComparer.Ordinal))
                {
                    Text(orb.id); Unsigned(orb.owner); Integer(orb.kind); Integer(orb.polarity); Integer(orb.state);
                    Number(orb.pos.x); Number(orb.pos.y); Unsigned(orb.sequence);
                    Unsigned(orb.transferCount); Unsigned(orb.rightTransferCount); Unsigned(orb.lastTransferSequence); Integer(orb.entrySide);
                    // Keep historical logical hashes unchanged when the optional motion is absent.
                    if (orb.hasTransferMotion)
                    { Text("P4-transfer-motion"); Number(orb.transferVelocityX); Number(orb.transferVelocityY); Number(orb.transferServerTime); }
                }
                Integer(value.projectiles.Length);
                foreach (var projectile in value.projectiles.OrderBy(projectile => projectile.id, StringComparer.Ordinal))
                {
                    Text(projectile.id); Unsigned(projectile.owner);
                    Number(projectile.position.x); Number(projectile.position.y); Number(projectile.position.z); Number(projectile.radius);
                    if (projectile.ballistic)
                    {
                        Text("P2-ballistic"); Number(projectile.velocity.x); Number(projectile.velocity.y); Number(projectile.velocity.z);
                        Number(projectile.gravity.x); Number(projectile.gravity.y); Number(projectile.gravity.z);
                        Number(projectile.elapsed); Number(projectile.lifetime);
                    }
                }
            }
            internal void Resources(ResourceSnapshot value, bool includeRevision)
            {
                Text("resources"); Text(value.sessionId); Unsigned(value.roundId);
                if (includeRevision) Unsigned(value.revision);
                Unsigned(value.seed); Boolean(value.playing); Boolean(value.debugTestMode); Boolean(value.debugToolsEnabled);
                Number(value.maximum); Number(value.generateCost); Number(value.regenerationRate); Number(value.hitRecovery);
                Integer(value.storageLimit); Integer(value.players.Length);
                foreach (var player in value.players.OrderBy(player => player.playerId))
                {
                    Unsigned(player.playerId); Number(player.stamina); Integer(player.generatedTotal);
                    Unsigned(player.lastSequence); Integer(player.storedOrbs);
                }
            }
            internal void Battle(BattleSnapshot value, bool includeRevision)
            {
                Text("battle"); Text(value.sessionId); Unsigned(value.roundId);
                if (includeRevision) Unsigned(value.revision);
                Text(value.phase); Number(value.startedAt); Number(value.deadline); Number(value.remaining);
                Number(value.teamHp); Number(value.duration); Number(value.teamHpDecayPerSecond);
                Integer(value.observedMonsterHp); Integer(value.monsterMaxHp); Boolean(value.developmentSolo);
                Boolean(value.shortDuration); Integer(value.participants); Boolean(value.locallyDetectedNetworkError);
            }
            public override string ToString() => content.ToString();
        }

        private static bool ValidExpected(GameSnapshotContext expected) => expected != null
            && LobbyWire.ValidId(expected.roomId) && LobbyWire.ValidId(expected.sessionId)
            && AttackWire.ValidNonce(expected.nonce) && LobbyWire.ValidFingerprint(expected.configHash)
            && ValidExpectedPlayers(expected) && expected.minimumRoundId > 0
            && (!expected.allowTransfers || Finite(expected.transferEdgeInset) && expected.transferEdgeInset > 0f && expected.transferEdgeInset < .5f)
            && expected.config != null && LobbyHostConfig.TryRead(JsonUtility.ToJson(expected.config), out _)
            && (!IsMultiparty(expected) || expected.config.storageLimit <= 20)
            && (!expected.continuousTransfers || IsMultiparty(expected) && expected.allowTransfers
                && Finite(expected.transferMaximumSpeed) && expected.transferMaximumSpeed > 0f);
        private static bool ValidPlayer(LobbyPlayer player, ulong id, int number) => player != null
            && player.clientId == id && player.playerNumber == number && player.connected;
        private static bool SameContext(GameSnapshot snapshot, string session, uint round, string nonce)
            => snapshot.sessionId == session && snapshot.roundId == round && snapshot.nonce == nonce;
        private static bool ExpectedOwner(ulong owner, GameSnapshotContext expected) => ExpectedPlayers(expected).Contains(owner);
        private static bool IsMultiparty(GameSnapshot value) => value?.players != null && value.players.Length > 0;
        private static bool IsMultiparty(GameSnapshotContext value) => value?.participantIds != null && value.participantIds.Length > 0;
        private static ulong[] ExpectedPlayers(GameSnapshotContext expected) => IsMultiparty(expected)
            ? expected.participantIds : new[] { expected.p1, expected.p2 };
        private static bool ValidExpectedPlayers(GameSnapshotContext expected)
        {
            var ids = ExpectedPlayers(expected);
            return ids.Length >= 2 && ids.Length <= (IsMultiparty(expected) ? 5 : 2)
                && ids[0] == NetworkManager.ServerClientId && ids.Distinct().Count() == ids.Length
                && expected.p1 == ids[0] && expected.p2 == ids[1];
        }
        private static bool ValidPlayers(GameSnapshot snapshot, GameSnapshotContext expected)
        {
            if (IsMultiparty(snapshot) != IsMultiparty(expected)) return false;
            var ids = ExpectedPlayers(expected); var players = snapshot.OrderedPlayers;
            if (players.Length != ids.Length) return false;
            for (int i = 0; i < ids.Length; i++) if (!ValidPlayer(players[i], ids[i], i + 1)) return false;
            return !IsMultiparty(expected) || players.All(player => player.ready && player.initialStateReceived)
                && SamePlayer(snapshot.p1, players[0]) && SamePlayer(snapshot.p2, players[1]);
        }
        private static bool SamePlayer(LobbyPlayer a, LobbyPlayer b) => a != null && b != null
            && a.clientId == b.clientId && a.playerNumber == b.playerNumber && a.connected == b.connected
            && a.ready == b.ready && a.initialStateReceived == b.initialStateReceived;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Close(double left, double right) => Finite(left) && Finite(right) && Math.Abs(left - right) <= LogicalTolerance;
        private static bool Reject(string value, out string reason) { reason = value; return false; }
    }
}
