using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace C6.Prototype.Battle.Tests
{
    public sealed class BattleWireTests
    {
        [Test]
        public void FractionalHostClockAndMaximumRevisionRoundTripWithoutClientClockFields()
        {
            var value = Playing(); value.revision = ulong.MaxValue; value.remaining = 123.456789012345;
            value.teamHp = value.remaining * value.teamHpDecayPerSecond;
            using (var writer = BattleWire.Write(value))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(BattleWire.TryRead<BattleSnapshot>(reader, out var result), Is.True);
                Assert.That(BattleWire.ValidSnapshot(result), Is.True);
                Assert.That(result.revision, Is.EqualTo(ulong.MaxValue));
                Assert.That(result.remaining, Is.EqualTo(value.remaining).Within(1e-12));
                Assert.That(result.teamHp, Is.EqualTo(value.teamHp).Within(1e-12));
                Assert.That(result.startedAt, Is.EqualTo(value.startedAt));
                Assert.That(result.deadline, Is.EqualTo(value.deadline));
                Assert.That(result.phase, Is.EqualTo(BattlePhase.Playing.ToString()));
            }
            var fields = typeof(BattleSyncPacket).GetFields().Select(field => field.Name).ToArray();
            Assert.That(fields, Is.EquivalentTo(new[] { "nonce", "sessionId", "roundId" }), "Client synchronization never submits HP, time or a result.");
        }

        [Test]
        public void LobbyAndReadyRequireFrozenFullClockAndExplicitSoloForOneParticipantReady()
        {
            var value = Lobby();
            Assert.That(BattleWire.ValidSnapshot(value), Is.True);
            value.phase = BattlePhase.Ready.ToString();
            Assert.That(BattleWire.ValidSnapshot(value), Is.False);
            value.developmentSolo = true;
            Assert.That(BattleWire.ValidSnapshot(value), Is.True);
            value.developmentSolo = false; value.participants = 2;
            Assert.That(BattleWire.ValidSnapshot(value), Is.True);
            value.remaining--; value.teamHp--;
            Assert.That(BattleWire.ValidSnapshot(value), Is.False);
            value = Lobby(); value.startedAt = 1;
            Assert.That(BattleWire.ValidSnapshot(value), Is.False);
        }

        [Test]
        public void InvalidClockResourcesProtocolContextAndThirdParticipantAreRejected()
        {
            Action<BattleSnapshot>[] mutations =
            {
                value => value.nonce = Guid.Empty.ToString(), value => value.sessionId = new string('x', 129),
                value => value.roundId = 0, value => value.revision = 0, value => value.phase = "999",
                value => value.duration = double.NaN, value => value.remaining = double.PositiveInfinity,
                value => value.remaining = -1, value => value.teamHp = -1, value => value.teamHp++,
                value => value.teamHpDecayPerSecond = 0, value => value.deadline += 1, value => value.startedAt = -1,
                value => value.monsterMaxHp = 0, value => value.observedMonsterHp = 101,
                value => value.participants = 3, value => value.locallyDetectedNetworkError = true
            };
            foreach (var mutate in mutations)
            { var value = Playing(); mutate(value); Assert.That(BattleWire.ValidSnapshot(value), Is.False); }
        }

        [Test]
        public void VictoryRequiresPositiveTimeZeroMonsterHpAndDefeatRequiresExpiredTime()
        {
            var value = Playing(); value.phase = BattlePhase.Victory.ToString();
            Assert.That(BattleWire.ValidSnapshot(value), Is.False);
            value.observedMonsterHp = 0;
            Assert.That(BattleWire.ValidSnapshot(value), Is.True);
            value.remaining = value.teamHp = 0;
            Assert.That(BattleWire.ValidSnapshot(value), Is.False);
            value.phase = BattlePhase.Defeat.ToString(); value.observedMonsterHp = 80;
            Assert.That(BattleWire.ValidSnapshot(value), Is.True);
            value.remaining = value.teamHp = 1;
            Assert.That(BattleWire.ValidSnapshot(value), Is.False);
        }

        [Test]
        public void ClientAcceptsOnlyCurrentConnectionSessionRoundAndIncreasingRevision()
        {
            var current = Playing(); var next = Playing(current.nonce); next.revision++;
            Assert.That(Accept(current, next), Is.True);
            next.revision = current.revision;
            Assert.That(Accept(current, next), Is.False);
            next.revision++; next.nonce = Guid.NewGuid().ToString("N");
            Assert.That(Accept(current, next), Is.False);
            next.nonce = current.nonce; next.sessionId = "other-session";
            Assert.That(Accept(current, next), Is.False);
            next.sessionId = current.sessionId; next.roundId++;
            Assert.That(Accept(current, next), Is.False);
        }

        [Test]
        public void ConfirmedResultCannotChangeWinnerFinalTimeHpOrDeadlineWithinRound()
        {
            var current = Playing(); current.phase = BattlePhase.Victory.ToString(); current.observedMonsterHp = 0;
            var next = Playing(current.nonce); next.revision++; next.phase = BattlePhase.Victory.ToString(); next.observedMonsterHp = 0;
            Assert.That(Accept(current, next), Is.True);
            next.remaining--; next.teamHp--;
            Assert.That(Accept(current, next), Is.False);
            next.remaining = current.remaining; next.teamHp = current.teamHp; next.phase = BattlePhase.NetworkError.ToString();
            Assert.That(Accept(current, next), Is.False);
            next.phase = BattlePhase.Defeat.ToString(); next.remaining = next.teamHp = 0;
            Assert.That(Accept(current, next), Is.False);
        }

        [Test]
        public void ExplicitNewRoundCanReplaceResultButOldRoundCannotBeRewrapped()
        {
            var old = Playing(); old.phase = BattlePhase.Victory.ToString(); old.observedMonsterHp = 0;
            var next = Lobby(old.nonce); next.roundId++; next.revision = old.revision + 1;
            Assert.That(BattleWire.AcceptsSnapshot(old, next, old.nonce, old.sessionId, next.roundId), Is.True);
            Assert.That(BattleWire.AcceptsSnapshot(old, old, old.nonce, old.sessionId, next.roundId), Is.False);
        }

        [Test]
        public void ShortDurationRemainsExplicitInSnapshotAndNetworkErrorDoesNotInventDefeat()
        {
            var value = Playing(); value.duration = 2; value.shortDuration = true;
            value.deadline = value.startedAt + 2; value.remaining = value.teamHp = 1;
            value.phase = BattlePhase.NetworkError.ToString();
            Assert.That(BattleWire.ValidSnapshot(value), Is.True);
            using (var writer = BattleWire.Write(value))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(BattleWire.TryRead<BattleSnapshot>(reader, out var result), Is.True);
                Assert.That(result.shortDuration, Is.True);
                Assert.That(result.duration, Is.EqualTo(2));
                Assert.That(result.phase, Is.EqualTo(BattlePhase.NetworkError.ToString()));
                Assert.That(result.observedMonsterHp, Is.EqualTo(100));
            }
        }

        [Test]
        public void MalformedProtocolVersionLengthUtf8AndOversizeAreRejected()
        {
            byte[] body = Encoding.UTF8.GetBytes("{\"sessionId\":\"session\"}");
            AssertRejected(Frame(body, 2, body.Length)); AssertRejected(Frame(body, 1, body.Length + 1));
            AssertRejected(Frame(body, 1, body.Length - 1)); AssertRejected(Frame(new byte[] { 0xff }, 1, 1));
            AssertRejected(Frame(new byte[BattleWire.MaximumBytes], 1, BattleWire.MaximumBytes));
            Assert.Throws<ArgumentException>(() =>
            { using (var ignored = BattleWire.Write(new BattleSyncPacket { nonce = new string('x', BattleWire.MaximumBytes) })) { } });
        }
        private static bool Accept(BattleSnapshot previous, BattleSnapshot next) => BattleWire.AcceptsSnapshot(previous, next,
            previous.nonce, previous.sessionId, previous.roundId);
        private static BattleSnapshot Playing(string nonce = null) => new BattleSnapshot
        {
            nonce = nonce ?? Guid.NewGuid().ToString("N"), sessionId = "session", roundId = 3, revision = 7,
            phase = BattlePhase.Playing.ToString(), startedAt = 10, deadline = 190, remaining = 170, teamHp = 170,
            duration = 180, teamHpDecayPerSecond = 1, observedMonsterHp = 100, monsterMaxHp = 100, participants = 2
        };
        private static BattleSnapshot Lobby(string nonce = null)
        {
            var value = Playing(nonce); value.phase = BattlePhase.Lobby.ToString();
            value.startedAt = value.deadline = 0; value.remaining = value.teamHp = value.duration; value.participants = 1; return value;
        }
        private static byte[] Frame(byte[] body, byte version, int declared)
        {
            var bytes = new byte[5 + body.Length]; bytes[0] = version;
            Array.Copy(BitConverter.GetBytes(declared), 0, bytes, 1, 4); Array.Copy(body, 0, bytes, 5, body.Length); return bytes;
        }
        private static void AssertRejected(byte[] bytes)
        { using (var reader = new FastBufferReader(bytes, Allocator.Temp)) Assert.That(BattleWire.TryRead<BattleSyncPacket>(reader, out _), Is.False); }
    }
}
