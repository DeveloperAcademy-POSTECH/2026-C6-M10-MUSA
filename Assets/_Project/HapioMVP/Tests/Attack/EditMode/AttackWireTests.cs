using System;
using System.Linq;
using System.Text;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class AttackWireTests
    {
        [Test]
        public void FullTwoParticipantStateRoundTripsWithoutLosingIdentityOrPrecision()
        {
            var original = Snapshot();
            original.orbs = Enumerable.Range(0, 12).Select(index => new OrbWire
            {
                id = "orb-" + index, owner = index < 6 ? 0ul : 41ul, kind = (int)OrbKind.Combined,
                polarity = (int)OrbPolarity.None, state = (int)OrbAuthorityState.Projectile,
                pos = new Vector2(.25f, .875f), sequence = ulong.MaxValue - (ulong)index
            }).ToArray();
            original.projectiles = original.orbs.Select(orb => new ProjectileWire
            { id = orb.id, owner = orb.owner, position = new Vector3(.125f, 1.5f, -2f), radius = .2f }).ToArray();
            using (var writer = AttackWire.Write(original))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(writer.Length, Is.GreaterThan(1400), "Full state needs fragmented reliable delivery.");
                Assert.That(AttackWire.TryRead<AttackSnapshot>(reader, out var received), Is.True);
                Assert.That(AttackWire.ValidSnapshot(received), Is.True);
                Assert.That(received.orbs.Length, Is.EqualTo(12));
                Assert.That(received.projectiles.Length, Is.EqualTo(12));
                Assert.That(received.orbs[11].sequence, Is.EqualTo(ulong.MaxValue - 11));
                Assert.That(received.orbs[11].owner, Is.EqualTo(41));
                Assert.That(received.projectiles[3].position, Is.EqualTo(new Vector3(.125f, 1.5f, -2f)));
                Assert.That(received.nonce, Is.EqualTo(original.nonce));
                Assert.That(received.sessionId, Is.EqualTo(original.sessionId));
            }
        }

        [Test]
        public void WrongVersionTruncatedLengthAndTrailingBytesAreRejectedBeforeJsonParsing()
        {
            var body = Encoding.UTF8.GetBytes("{\"nonce\":\"x\"}");
            AssertRejected(Frame(body, 2, body.Length));
            AssertRejected(Frame(body, 1, body.Length + 1));
            AssertRejected(Frame(body, 1, body.Length - 1));
            AssertRejected(new byte[] { 1, 0, 0, 0, 0 });
        }

        [Test]
        public void OversizedWritesAndReadsAreBounded()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using (var ignored = AttackWire.Write(new AttackHello { nonce = new string('a', AttackWire.MaximumBytes) })) { }
            });
            AssertRejected(Frame(new byte[AttackWire.MaximumBytes], 1, AttackWire.MaximumBytes));
        }

        [Test]
        public void InvalidUtf8AndMalformedJsonCannotCreatePackets()
        {
            AssertRejected(Frame(new byte[] { 0xff }, 1, 1));
            var malformed = Encoding.UTF8.GetBytes("{broken");
            AssertRejected(Frame(malformed, 1, malformed.Length));
        }

        [Test]
        public void ConnectionNonceIsExplicitAndIndependentFromTheAuthoritySession()
        {
            Assert.That(AttackWire.ValidNonce(null), Is.False);
            Assert.That(AttackWire.ValidNonce(""), Is.False);
            Assert.That(AttackWire.ValidNonce("not-a-guid"), Is.False);
            Assert.That(AttackWire.ValidNonce(Guid.Empty.ToString()), Is.False);
            var snapshot = Snapshot();
            Assert.That(AttackWire.ValidNonce(snapshot.nonce), Is.True);
            Assert.That(snapshot.nonce, Is.Not.EqualTo(snapshot.sessionId));
            var otherAttempt = Guid.NewGuid().ToString("N");
            using (var writer = AttackWire.Write(new AttackHello { nonce = otherAttempt }))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(AttackWire.TryRead<AttackHello>(reader, out var parsed), Is.True);
                Assert.That(parsed.nonce, Is.EqualTo(otherAttempt));
                Assert.That(parsed.nonce, Is.Not.EqualTo(snapshot.nonce));
            }
        }

        [Test]
        public void SnapshotRejectsDuplicateOrbsInvalidEnumsAndKindPolarityMismatch()
        {
            var snapshot = Snapshot();
            snapshot.orbs = new[] { Orb("same"), Orb("same") };
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
            snapshot.orbs = new[] { Orb("valid") };
            snapshot.orbs[0].kind = 99;
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
            snapshot.orbs[0].kind = (int)OrbKind.Combined;
            snapshot.orbs[0].polarity = (int)OrbPolarity.Yin;
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
            snapshot.orbs[0].polarity = (int)OrbPolarity.None;
            snapshot.orbs[0].state = 99;
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
            snapshot.orbs[0].state = (int)OrbAuthorityState.Consumed;
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.True, "Consumed identity remains in confirmed state.");
        }

        [Test]
        public void ProjectileDisplayRequiresMatchingConfirmedProjectileOwnerAndFinitePose()
        {
            var snapshot = Snapshot();
            snapshot.orbs = new[] { Orb("flying") };
            var projectile = new ProjectileWire { id = "flying", owner = 41, position = Vector3.one, radius = .2f };
            snapshot.projectiles = new[] { projectile };
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False, "Idle orbs cannot have a projectile view.");
            snapshot.orbs[0].state = (int)OrbAuthorityState.Projectile;
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.True);
            projectile.owner = 0;
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
            projectile.owner = 41;
            projectile.id = "unknown";
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
            projectile.id = "flying";
            projectile.position = new Vector3(float.NaN, 0, 0);
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
            projectile.position = Vector3.zero;
            projectile.radius = float.PositiveInfinity;
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
        }

        [Test]
        public void RequestRoundTripPreservesActionContextAndHasNoClaimedSenderField()
        {
            var original = new OrbActionRequest(Guid.NewGuid().ToString("N"), 17, "request", "orb", null,
                OrbActionKind.Launch, ulong.MaxValue, new Vector2(.125f, .875f));
            var packet = AttackRequestPacket.FromRequest(Guid.NewGuid().ToString("N"), original);
            using (var writer = AttackWire.Write(packet))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(AttackWire.TryRead<AttackRequestPacket>(reader, out var parsed), Is.True);
                var request = parsed.ToRequest();
                Assert.That(request.SessionId, Is.EqualTo(original.SessionId));
                Assert.That(request.RoundId, Is.EqualTo(17));
                Assert.That(request.Kind, Is.EqualTo(OrbActionKind.Launch));
                Assert.That(request.SequenceNumber, Is.EqualTo(ulong.MaxValue));
                Assert.That(request.NormalizedPosition, Is.EqualTo(original.NormalizedPosition));
                Assert.That(typeof(AttackRequestPacket).GetFields().Any(field => field.Name == "sender" || field.Name == "owner"), Is.False);
            }
        }

        [Test]
        public void ConfirmedConsumedReplyIsIndependentFromTheOriginalAcceptedLaunch()
        {
            var confirmed = Orb("launched");
            confirmed.state = (int)OrbAuthorityState.Consumed;
            var reply = new AttackRequestReply
            {
                nonce = Guid.NewGuid().ToString("N"), sessionId = Guid.NewGuid().ToString("N"), roundId = 2,
                requestId = "request", orbId = confirmed.id, accepted = true, duplicate = true,
                known = true, pending = false, reason = "REQUEST_KNOWN", confirmedOrb = confirmed
            };
            using (var writer = AttackWire.Write(reply))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(AttackWire.TryRead<AttackRequestReply>(reader, out var received), Is.True);
                Assert.That(received.accepted && received.known && received.duplicate, Is.True);
                Assert.That(received.pending, Is.False);
                Assert.That(received.confirmedOrb.ToRecord().AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
                Assert.That(received.confirmedOrb.id, Is.EqualTo(received.orbId));
            }
        }

        [Test]
        public void FortyOwnedOrbsRequireExplicitlyExpandedSnapshotCapacity()
        {
            var snapshot = Snapshot();
            snapshot.orbs = Enumerable.Range(0, 40).Select(index => Orb("stored-" + index)).ToArray();
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False, "The preserved T06 default remains twelve orbs.");
            Assert.That(AttackWire.ValidSnapshot(snapshot, 40), Is.True);
            Assert.That(AttackWire.ValidSnapshot(snapshot, 64), Is.True);
            Assert.That(AttackWire.ValidSnapshot(snapshot, 39), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SixtyFourProjectileSnapshotRoundTripsAt64KiBAndIsRejectedAtDefault16KiB(bool ballistic)
        {
            var snapshot = Snapshot();
            snapshot.orbs = Enumerable.Range(0, 64).Select(index => new OrbWire
            {
                id = index.ToString("D3") + new string('x', 125),
                owner = index < 32 ? 0UL : 41UL,
                kind = (int)OrbKind.Combined, polarity = (int)OrbPolarity.None,
                state = (int)OrbAuthorityState.Projectile,
                pos = new Vector2(.125f, .875f), sequence = ulong.MaxValue - (ulong)index
            }).ToArray();
            snapshot.projectiles = snapshot.orbs.Select(orb => new ProjectileWire
            {
                id = orb.id, owner = orb.owner, position = new Vector3(.125f, 1.5f, -2f), radius = .165f,
                ballistic = ballistic, velocity = ballistic ? new Vector3(1.2345678f, -9.876543f, 29.123456f) : Vector3.zero,
                gravity = ballistic ? new Vector3(0, -9.812345f, 0) : Vector3.zero,
                elapsed = ballistic ? 3.1234567f : 0f, lifetime = ballistic ? 4.1234567f : 0f
            }).ToArray();
            Assert.That(AttackWire.ValidSnapshot(snapshot, 64), Is.True);
            Assert.Throws<ArgumentException>(() =>
            {
                using (var ignored = AttackWire.Write(snapshot)) { }
            });
            using (var writer = AttackWire.Write(snapshot, AttackWire.MaximumInventoryBytes))
            {
                Assert.That(writer.Length, Is.GreaterThan(AttackWire.MaximumBytes));
                Assert.That(writer.Length, Is.LessThanOrEqualTo(AttackWire.MaximumInventoryBytes));
                using (var defaultReader = new FastBufferReader(writer, Allocator.Temp))
                    Assert.That(AttackWire.TryRead<AttackSnapshot>(defaultReader, out _), Is.False);
                using (var expandedReader = new FastBufferReader(writer, Allocator.Temp))
                {
                    Assert.That(AttackWire.TryRead<AttackSnapshot>(expandedReader, out var received, AttackWire.MaximumInventoryBytes), Is.True);
                    Assert.That(AttackWire.ValidSnapshot(received, 64), Is.True);
                    Assert.That(received.orbs.Length, Is.EqualTo(64));
                    Assert.That(received.projectiles.Length, Is.EqualTo(64));
                    Assert.That(received.orbs[63].id, Is.EqualTo(snapshot.orbs[63].id));
                    Assert.That(received.orbs[63].sequence, Is.EqualTo(ulong.MaxValue - 63UL));
                    Assert.That(received.projectiles[63].owner, Is.EqualTo(41));
                    Assert.That(received.projectiles[63].position, Is.EqualTo(new Vector3(.125f, 1.5f, -2f)));
                    Assert.That(received.projectiles[63].ballistic, Is.EqualTo(ballistic));
                    Assert.That(received.projectiles[63].velocity, Is.EqualTo(snapshot.projectiles[63].velocity));
                    Assert.That(received.projectiles[63].lifetime, Is.EqualTo(snapshot.projectiles[63].lifetime));
                }
            }
        }

        [Test]
        public void ExpandedSnapshotStillRejectsSixtyFiveOrbs()
        {
            var snapshot = Snapshot();
            snapshot.orbs = Enumerable.Range(0, 65).Select(index => Orb("overflow-" + index)).ToArray();
            Assert.That(AttackWire.ValidSnapshot(snapshot, 64), Is.False);
            Assert.That(AttackWire.ValidSnapshot(snapshot, 65), Is.False, "An invalid override cannot widen the supported limit.");
        }

        [TestCase(0)]
        [TestCase(65)]
        public void ExpandedCapacityOverrideMustStayWithinSupportedBounds(int maximum)
        {
            Assert.That(AttackWire.ValidSnapshot(Snapshot(), maximum), Is.False);
        }

        [Test]
        public void ExpandedTransportStillRejectsPacketsBeyond64KiB()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using (var ignored = AttackWire.Write(new AttackHello { nonce = new string('x', AttackWire.MaximumInventoryBytes) }, AttackWire.MaximumInventoryBytes)) { }
            });
            byte[] frame = Frame(new byte[AttackWire.MaximumInventoryBytes], 1, AttackWire.MaximumInventoryBytes);
            using (var reader = new FastBufferReader(frame, Allocator.Temp))
                Assert.That(AttackWire.TryRead<AttackHello>(reader, out _, AttackWire.MaximumInventoryBytes), Is.False);
        }

        private static AttackSnapshot Snapshot() => new AttackSnapshot
        {
            nonce = Guid.NewGuid().ToString("N"), sessionId = Guid.NewGuid().ToString("N"),
            roundId = 1, revision = 1, hp = 100, maxHp = 100, totalHits = 0, roundHits = 0,
            resets = 0, state = AttackBattleState.Playing.ToString()
        };
        private static OrbWire Orb(string id) => new OrbWire
        {
            id = id, owner = 41, kind = (int)OrbKind.Combined, polarity = (int)OrbPolarity.None,
            state = (int)OrbAuthorityState.Idle, pos = Vector2.one * .5f
        };
        private static byte[] Frame(byte[] body, byte version, int declared)
        {
            var result = new byte[5 + body.Length];
            result[0] = version;
            Array.Copy(BitConverter.GetBytes(declared), 0, result, 1, 4);
            Array.Copy(body, 0, result, 5, body.Length);
            return result;
        }
        private static void AssertRejected(byte[] bytes)
        {
            using (var reader = new FastBufferReader(bytes, Allocator.Temp))
                Assert.That(AttackWire.TryRead<AttackHello>(reader, out _), Is.False);
        }
    }
}
