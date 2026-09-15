using System;
using System.Linq;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class MultiplayerAttackTests
    {
        private static readonly ulong[] Seats = { 0, 72, 4, 91, 8 };

        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void RingUsesJoinOrderRatherThanNumericNetworkIdAndTwoPlayerRingReturnsSamePeer(int count)
        {
            var ids = Seats.Take(count).ToArray();
            for (int seat = 0; seat < count; seat++)
            {
                Assert.That(ParticipantRing.TryNeighbour(ids, ids[seat], false, out var left), Is.True);
                Assert.That(ParticipantRing.TryNeighbour(ids, ids[seat], true, out var right), Is.True);
                Assert.That(left, Is.EqualTo(ids[(seat + count - 1) % count]));
                Assert.That(right, Is.EqualTo(ids[(seat + 1) % count]));
                Assert.That(left == right, Is.EqualTo(count == 2));
            }
        }

        [Test]
        public void RingRejectsMissingDuplicateOversizedAndUnknownSender()
        {
            foreach (var ids in new[] { null, new ulong[0], new ulong[] { 0 }, new ulong[] { 0, 0 }, new ulong[] { 0, 1, 2, 3, 4, 5 } })
                Assert.That(ParticipantRing.TryNeighbour(ids, 0, true, out _), Is.False);
            Assert.That(ParticipantRing.TryNeighbour(Seats, 99, true, out _), Is.False);
        }

        [TestCase(2, false, OrbKind.Raw)] [TestCase(2, true, OrbKind.Combined)]
        [TestCase(3, false, OrbKind.Raw)] [TestCase(3, true, OrbKind.Combined)]
        [TestCase(4, false, OrbKind.Raw)] [TestCase(4, true, OrbKind.Combined)]
        [TestCase(5, false, OrbKind.Raw)] [TestCase(5, true, OrbKind.Combined)]
        public void ACompleteRingPreservesIdentityOppositeEntryAndDirectionalHistory(int count, bool right, OrbKind kind)
        {
            var ids = Seats.Take(count).ToArray();
            var registry = new HostOrbRegistry(true); registry.BeginSession("ring", 1);
            var authority = new AttackAuthority(registry, 100, 20); authority.BeginDevelopmentRound();
            var orb = registry.RegisterDevelopmentOrb(ids[0], kind, kind == OrbKind.Raw ? OrbPolarity.Yin : OrbPolarity.None, Vector2.one * .5f);
            ulong owner = ids[0];
            for (int step = 0; step < count; step++)
            {
                Assert.That(ParticipantRing.TryNeighbour(ids, owner, right, out var receiver), Is.True);
                var request = new OrbActionRequest("ring", 1, "step-" + step, orb.OrbId, null,
                    right ? OrbActionKind.TransferRight : OrbActionKind.TransferLeft, (ulong)step + 1, new Vector2(.5f, .37f));
                var result = authority.RequestTransfer(owner, request, receiver, true, 20, .055f);
                Assert.That(result.Accepted, Is.True, result.Reason);
                Assert.That(result.Orb.OrbId, Is.EqualTo(orb.OrbId));
                Assert.That(result.Orb.OwnerPlayerId, Is.EqualTo(receiver));
                Assert.That(result.Orb.EntrySide, Is.EqualTo(right ? EntrySide.Left : EntrySide.Right));
                Assert.That(result.Orb.RightTransferCount, Is.EqualTo(right ? (ulong)step + 1 : 0));
                Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
                Assert.That(registry.IsPending(orb.OrbId), Is.False);
                owner = receiver;
            }
            Assert.That(owner, Is.EqualTo(ids[0]));
            registry.TryGet(orb.OrbId, out var current);
            var wire = OrbWire.FromRecord(current);
            Assert.That(wire.ToRecord().RightTransferCount, Is.EqualTo(current.RightTransferCount));
            Assert.That(current.TransferCount, Is.EqualTo(count));
        }

        [Test]
        public void TwentyFlyingPerAttackerRejectsBeforeReservationAndDoesNotBlockAnotherAttacker()
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("flight-limit", 1);
            var authority = new AttackAuthority(registry, 100, 20); authority.ConfigureFlightCapacity(20); authority.BeginDevelopmentRound();
            string first = null;
            for (int i = 0; i < 20; i++)
            {
                var orb = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
                first ??= orb.OrbId;
                Assert.That(authority.RequestLaunch(0, Launch(orb, "launch-" + i)).Accepted, Is.True);
                // Launching reservations count immediately, before the Rigidbody has spawned.
                if (i % 2 == 0) Assert.That(authority.MarkProjectileSpawned("flight-limit", 1, orb.OrbId), Is.True);
            }
            var next = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            var rejected = Launch(next, "full");
            Assert.That(authority.RequestLaunch(0, rejected).Reason, Is.EqualTo("FLIGHT_CAPACITY_FULL"));
            Assert.That(registry.IsPending(next.OrbId), Is.False);
            registry.TryGet(next.OrbId, out var current); Assert.That(current.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            var other = registry.RegisterDevelopmentOrb(72, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            Assert.That(authority.RequestLaunch(72, Launch(other, "other-player")).Accepted, Is.True);
            Assert.That(authority.ExpireProjectile("flight-limit", 1, first), Is.True);
            Assert.That(authority.RequestLaunch(0, rejected).Accepted, Is.False, "Rejected receipt stays rejected after a slot opens.");
            Assert.That(authority.RequestLaunch(0, Launch(next, "new-request")).Accepted, Is.True);
            authority.EndDevelopmentRound(); registry.ResetRound(2); authority.BeginDevelopmentRound();
            var fresh = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            var retry = new OrbActionRequest("flight-limit", 2, "retry", fresh.OrbId, null, OrbActionKind.Launch, 1, Vector2.one * .5f);
            Assert.That(authority.RequestLaunch(0, retry).Accepted, Is.True);
            Assert.That(authority.RequestLaunch(0, rejected).Reason, Is.EqualTo("ROUND_MISMATCH"));
        }
        private static OrbActionRequest Launch(OrbRecord orb, string request) => new OrbActionRequest("flight-limit", 1, request,
            orb.OrbId, null, OrbActionKind.Launch, 1, Vector2.one * .5f);

        [Test]
        public void FlightCapacityRequiresExplicitPreRoundConfiguration()
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("limits", 1);
            var authority = new AttackAuthority(registry, 100, 20);
            Assert.Throws<ArgumentOutOfRangeException>(() => authority.ConfigureFlightCapacity(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => authority.ConfigureFlightCapacity(21));
            authority.BeginDevelopmentRound();
            Assert.Throws<InvalidOperationException>(() => authority.ConfigureFlightCapacity(20));
        }

        [Test]
        public void FullHundredStoredPlusHundredBallisticFlightInventoryFitsExplicitBoundAndRejectsOverflow()
        {
            var snapshot = FullInventory();
            Assert.That(AttackWire.ValidSnapshot(snapshot, ParticipantRing.MaximumLiveOrbs), Is.True);
            Assert.That(AttackWire.ValidSnapshot(snapshot, 64), Is.False);
            using (var writer = AttackWire.Write(snapshot, AttackWire.MaximumMultiplayerInventoryBytes))
            {
                TestContext.WriteLine("C6_P3_ATTACK_FULL_CAPACITY_BYTES=" + writer.Length + " records=200 projectiles=100 bound=262144 maxIdLength=128");
                using (var reader = new FastBufferReader(writer, Unity.Collections.Allocator.Temp))
                {
                    Assert.That(AttackWire.TryRead<AttackSnapshot>(reader, out var decoded, AttackWire.MaximumMultiplayerInventoryBytes), Is.True);
                    Assert.That(AttackWire.ValidSnapshot(decoded, ParticipantRing.MaximumLiveOrbs), Is.True);
                    Assert.That(decoded.orbs.Length, Is.EqualTo(200)); Assert.That(decoded.projectiles.Length, Is.EqualTo(100));
                }
            }
            Assert.Throws<ArgumentException>(() => { using var _ = AttackWire.Write(snapshot, AttackWire.MaximumInventoryBytes); });
            snapshot.orbs = snapshot.orbs.Concat(new[] { snapshot.orbs[0] }).ToArray();
            Assert.That(AttackWire.ValidSnapshot(snapshot, ParticipantRing.MaximumLiveOrbs), Is.False);
            snapshot = FullInventory(); snapshot.projectiles = snapshot.projectiles.Concat(new[] { snapshot.projectiles[0] }).ToArray();
            Assert.That(AttackWire.ValidSnapshot(snapshot, ParticipantRing.MaximumLiveOrbs), Is.False);
            snapshot = FullInventory(); snapshot.orbs[0].rightTransferCount = ulong.MaxValue;
            snapshot.orbs[0].transferCount = 0;
            Assert.That(AttackWire.ValidSnapshot(snapshot, ParticipantRing.MaximumLiveOrbs), Is.False);
        }
        private static AttackSnapshot FullInventory()
        {
            var value = new AttackSnapshot { nonce = Guid.NewGuid().ToString("N"), sessionId = new string('s', 128), roundId = uint.MaxValue,
                revision = ulong.MaxValue, hp = 80, maxHp = 100, totalHits = 1, roundHits = 1, state = AttackBattleState.Playing.ToString(),
                orbs = new OrbWire[200], projectiles = new ProjectileWire[100] };
            for (int i = 0; i < 200; i++)
            {
                string id = i.ToString("D3") + new string('o', 125); ulong owner = Seats[(i % 100) / 20];
                value.orbs[i] = new OrbWire { id = id, kind = (int)(i < 100 ? OrbKind.Raw : OrbKind.Combined),
                    polarity = (int)(i < 100 ? OrbPolarity.Yang : OrbPolarity.None), owner = owner,
                    state = (int)(i < 100 ? OrbAuthorityState.Idle : OrbAuthorityState.Projectile),
                    pos = new Vector2(.12345679f, .8765432f), sequence = ulong.MaxValue, transferCount = ulong.MaxValue,
                    lastTransferSequence = ulong.MaxValue, rightTransferCount = ulong.MaxValue, entrySide = (int)EntrySide.Left };
                if (i >= 100) value.projectiles[i - 100] = new ProjectileWire { id = id, owner = owner, ballistic = true,
                    position = new Vector3(12345.678f, -23456.789f, 34567.891f), radius = .165f,
                    velocity = new Vector3(123.456f, -234.567f, 345.678f), gravity = new Vector3(0, -9.81f, 0), elapsed = 3.1234567f, lifetime = 4 };
            }
            return value;
        }
    }
}
