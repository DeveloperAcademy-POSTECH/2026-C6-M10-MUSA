using System;
using System.Linq;
using System.Reflection;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class ContinuousTransferAuthorityTests
    {
        const string Session = "continuous-edge";
        const float Inset = .055f;
        HostOrbRegistry registry;
        AttackAuthority authority;
        OrbRecord orb;

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true); registry.BeginSession(Session, 1);
            authority = new AttackAuthority(registry, 100, 20);
            authority.ConfigureContinuousTransfers(.6f, .015f, 32f); authority.BeginDevelopmentRound();
            orb = registry.RegisterDevelopmentOrb(0, OrbKind.Raw, OrbPolarity.Yin, new Vector2(.5f, .37f));
        }
        OrbActionRequest Request(string id = "motion", Vector2? velocity = null, double time = 10d,
            bool right = true, ulong sequence = 1, string orbId = null, float? x = null)
            => new OrbActionRequest(Session, 1, id, orbId ?? orb.OrbId, null,
                right ? OrbActionKind.TransferRight : OrbActionKind.TransferLeft, sequence,
                new Vector2(x ?? (right ? 1f - Inset : Inset), .37f), null,
                new OrbTransferMotion(velocity ?? new Vector2(right ? 1.6f : -1.6f, 1.2f), time));
        AttackLaunchResult Send(OrbActionRequest request, double now = 10.5d, ulong sender = 0, ulong receiver = 17)
            => authority.RequestTransfer(sender, request, receiver, true, 20, Inset, true, now);
        void AssertSourceUnchanged()
        {
            Assert.That(registry.TryGet(orb.OrbId, out var current), Is.True);
            Assert.That(current, Is.SameAs(orb)); Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
        }

        [TestCase(true)] [TestCase(false)]
        public void ApprovedHandoffPreservesIdentityHeightAndDirectionWithOnlyDelayFriction(bool right)
        {
            var request = Request(right: right); var result = Send(request);
            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(result.Orb.OrbId, Is.EqualTo(orb.OrbId)); Assert.That(result.Orb.OwnerPlayerId, Is.EqualTo(17));
            Assert.That(result.Orb.Kind, Is.EqualTo(orb.Kind)); Assert.That(result.Orb.Polarity, Is.EqualTo(orb.Polarity));
            Assert.That(result.Orb.NormalizedPosition, Is.EqualTo(new Vector2(right ? Inset : 1f - Inset, .37f)));
            Assert.That(result.Orb.EntrySide, Is.EqualTo(right ? EntrySide.Left : EntrySide.Right));
            Assert.That(result.Orb.TransferCount, Is.EqualTo(1)); Assert.That(result.Orb.TransferMotion.HasValue, Is.True);
            var motion = result.Orb.TransferMotion.Value;
            Assert.That(motion.Velocity.x, Is.EqualTo(right ? 1.36f : -1.36f).Within(.00001f));
            Assert.That(motion.Velocity.y, Is.EqualTo(1.02f).Within(.00001f)); Assert.That(motion.ServerTime, Is.EqualTo(10.5d));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1)); Assert.That(registry.IsPending(orb.OrbId), Is.False);
        }

        [Test]
        public void ApprovalAndArrivalDecayEqualOneContinuousIntervalAndNeverRestartSpeed()
        {
            var approved = Send(Request()).Orb.TransferMotion.Value;
            var arrival = OrbTransferMotion.Decay(approved, 11d, .6f, .015f);
            var uninterrupted = OrbTransferMotion.Decay(Request().TransferMotion.Value, 11d, .6f, .015f);
            Assert.That(arrival.Velocity.x, Is.EqualTo(uninterrupted.Velocity.x).Within(.000001f));
            Assert.That(arrival.Velocity.y, Is.EqualTo(uninterrupted.Velocity.y).Within(.000001f));
            Assert.That(arrival.Velocity.magnitude, Is.EqualTo(1.4f).Within(.00001f));
        }

        [Test]
        public void LongButValidDelayCanApproveStoppedArrivalWithoutReversingDirection()
        {
            var result = Send(Request(velocity: Vector2.right * .1f), now: 11d);
            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(result.Orb.TransferMotion.Value.Velocity, Is.EqualTo(Vector2.zero));
            Assert.That(result.Orb.OwnerPlayerId, Is.EqualTo(17));
        }

        [TestCase(3f)] [TestCase(32f)]
        public void CollisionSpeedAboveReleaseCapIsPreservedWithinExplicitFiniteGuard(float speed)
        {
            var result = Send(Request(velocity: Vector2.right * speed), now: 10d);
            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(result.Orb.TransferMotion.Value.Velocity.x, Is.EqualTo(speed));
        }

        [TestCase(32.01f, 0f)] [TestCase(24f, 24f)]
        [TestCase(float.NaN, 0f)] [TestCase(float.PositiveInfinity, 0f)] [TestCase(1f, float.NegativeInfinity)]
        public void NonFiniteOrExcessiveSpeedRejectsBeforeAnyReservation(float x, float y)
        {
            var request = Request(velocity: new Vector2(x, y));
            Assert.That(Send(request).Reason, Is.EqualTo("INVALID_TRANSFER_MOTION")); AssertSourceUnchanged();
            Assert.That(Send(request, now: 12d).IsDuplicate, Is.True, "Rejected NaN payload keeps receipt identity.");
        }

        [TestCase(1.99d)] [TestCase(10.251d)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1d)]
        public void InvalidOrOutOfWindowCaptureTimeCannotMoveOwnership(double time)
        {
            string reason = Send(Request(time: time), now: 10d).Reason;
            Assert.That(reason, Is.EqualTo(time < 0 || double.IsNaN(time) || double.IsInfinity(time)
                ? "INVALID_TRANSFER_MOTION" : "TRANSFER_MOTION_TIME")); AssertSourceUnchanged();
        }

        [TestCase(2d)] [TestCase(10.25d)]
        public void ExactTimestampBoundsAreAcceptedAndFutureToleranceNeverBoostsVelocity(double time)
        {
            var result = Send(Request(velocity: Vector2.right * 8f, time: time), now: 10d);
            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(result.Orb.TransferMotion.Value.Velocity.x, Is.LessThanOrEqualTo(8f));
            Assert.That(result.Orb.TransferMotion.Value.ServerTime, Is.EqualTo(10d));
        }

        [TestCase(0f)] [TestCase(-1f)]
        public void NonOutwardMotionCannotTransferRight(float vx)
        { Assert.That(Send(Request(velocity: new Vector2(vx, 1f))).Reason, Is.EqualTo("TRANSFER_MOTION_DIRECTION")); AssertSourceUnchanged(); }
        [Test]
        public void InteriorRequestCannotTeleportAnOrbAcrossTheEdge()
        { Assert.That(Send(Request(x: .5f)).Reason, Is.EqualTo("TRANSFER_MOTION_EDGE")); AssertSourceUnchanged(); }
        [Test]
        public void SenderAuthenticationStillGuardsValidMotion()
        { Assert.That(Send(Request(), sender: 99).Reason, Is.EqualTo("OWNER_MISMATCH")); AssertSourceUnchanged(); }

        [Test]
        public void DuplicateUsesOriginalApprovalWhileChangedVelocityOrTimeConflicts()
        {
            var first = Send(Request()); Assert.That(first.Accepted, Is.True);
            var repeated = Send(Request(), now: 100d);
            Assert.That(repeated.IsDuplicate, Is.True); Assert.That(repeated.Orb, Is.SameAs(first.Orb));
            Assert.That(Send(Request(velocity: Vector2.right)).Reason, Is.EqualTo("REQUEST_ID_CONFLICT"));
            Assert.That(Send(Request(time: 10.1d)).Reason, Is.EqualTo("REQUEST_ID_CONFLICT"));
            Assert.That(first.Orb.TransferCount, Is.EqualTo(1));
        }

        [Test]
        public void FullReceiverLeavesExactSourceUnreservedAndRejectedReceiptNeverBecomesSuccess()
        {
            var full = Enumerable.Range(0, 20).Select(_ => registry.RegisterDevelopmentOrb(17, OrbKind.Raw,
                OrbPolarity.Yang, Vector2.one * .5f)).ToArray();
            var request = Request(); Assert.That(Send(request).Reason, Is.EqualTo("RECEIVER_STORAGE_FULL"));
            registry.TryGet(orb.OrbId, out var source); Assert.That(source, Is.SameAs(orb));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            var away = Request("free-capacity", velocity: Vector2.right, sequence: 1, orbId: full[0].OrbId);
            Assert.That(Send(away, sender: 17, receiver: 42).Accepted, Is.True);
            var repeated = Send(request); Assert.That(repeated.Accepted, Is.False); Assert.That(repeated.IsDuplicate, Is.True);
            Assert.That(Send(Request("new-push", sequence: 2)).Accepted, Is.True);
        }

        [TestCase(2)] [TestCase(3)] [TestCase(5)]
        public void RepeatedRingHandoffsKeepOneOrbAndDecayUntilNaturalStop(int players)
        {
            ulong owner = 0; var velocity = new Vector2(3f, 0f); double time = 10d;
            for (int step = 0; step < players * 2; step++)
            {
                var request = Request("ring-" + step, velocity, time, sequence: (ulong)step + 1);
                ulong receiver = (owner + 1) % (ulong)players;
                var result = Send(request, time + .1d, owner, receiver);
                Assert.That(result.Accepted, Is.True, result.Reason); Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
                Assert.That(result.Orb.OrbId, Is.EqualTo(orb.OrbId)); Assert.That(result.Orb.OwnerPlayerId, Is.EqualTo(receiver));
                velocity = result.Orb.TransferMotion.Value.Velocity; time += .1d; owner = receiver;
            }
            Assert.That(owner, Is.EqualTo(0));
            Assert.That(OrbTransferMotion.Decay(new OrbTransferMotion(velocity, time), time + 10d, .6f, .015f).Velocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void MissingMotionInContinuousModeAndUnexpectedMotionInLegacyModeReject()
        {
            var bare = new OrbActionRequest(Session, 1, "bare", orb.OrbId, null, OrbActionKind.TransferRight, 1, new Vector2(1f - Inset, .37f));
            Assert.That(Send(bare).Reason, Is.EqualTo("TRANSFER_MOTION_REQUIRED")); AssertSourceUnchanged();
            var legacy = new AttackAuthority(registry, 100, 20); legacy.BeginDevelopmentRound();
            Assert.That(legacy.RequestTransfer(0, Request(), 17, true, 20, Inset).Reason, Is.EqualTo("TRANSFER_MOTION_DISABLED"));
            var old = legacy.RequestTransfer(0, bare, 17, true, 20, Inset);
            Assert.That(old.Accepted, Is.True); Assert.That(old.Orb.TransferMotion.HasValue, Is.False);
        }

        [Test]
        public void LaunchAndCombinationDoNotAcceptTransferInputAndNewCombinedHasNoHandoffReceipt()
        {
            var second = registry.RegisterDevelopmentOrb(0, OrbKind.Raw, OrbPolarity.Yang, Vector2.one * .5f);
            var invalidCombine = new OrbActionRequest(Session, 1, "invalid-combine", orb.OrbId, second.OrbId,
                OrbActionKind.Combine, 1, Vector2.one * .5f, null, new OrbTransferMotion(Vector2.right, 10d));
            Assert.That(registry.Reserve(0, invalidCombine).Reason, Is.EqualTo("UNEXPECTED_TRANSFER_MOTION"));
            var combine = new OrbActionRequest(Session, 1, "combine", orb.OrbId, second.OrbId,
                OrbActionKind.Combine, 2, Vector2.one * .5f);
            var reservation = registry.Reserve(0, combine);
            Assert.That(registry.TryCompleteReservedCombination(reservation.Reservation, Vector2.one * .5f, out _, out _, out var combined), Is.True);
            Assert.That(combined.TransferMotion.HasValue, Is.False);
            var invalidLaunch = new OrbActionRequest(Session, 1, "invalid-launch", combined.OrbId, null,
                OrbActionKind.Launch, 1, Vector2.one * .5f, null, new OrbTransferMotion(Vector2.right, 10d));
            Assert.That(authority.RequestLaunch(0, invalidLaunch).Reason, Is.EqualTo("UNEXPECTED_TRANSFER_MOTION"));
        }

        [Test]
        public void CombinedLaunchProjectileAndConsumedRetainApprovedHandoffMetadata()
        {
            var combined = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            var moved = Send(Request(orbId: combined.OrbId)).Orb;
            var launch = new OrbActionRequest(Session, 1, "launch", combined.OrbId, null, OrbActionKind.Launch, 2, Vector2.one * .5f);
            var result = authority.RequestLaunch(17, launch); Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(result.Orb.TransferMotion, Is.EqualTo(moved.TransferMotion));
            Assert.That(authority.MarkProjectileSpawned(Session, 1, combined.OrbId), Is.True);
            registry.TryGet(combined.OrbId, out var projectile); Assert.That(projectile.TransferMotion, Is.EqualTo(moved.TransferMotion));
            Assert.That(authority.ProcessHostHit(Session, 1, combined.OrbId).Applied, Is.True);
            registry.TryGet(combined.OrbId, out var consumed); Assert.That(consumed.TransferMotion, Is.EqualTo(moved.TransferMotion));
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
        }

        [Test]
        public void PacketAndOrbRoundTripsPreservePreciseMotionFields()
        {
            var request = Request(time: 12345.678901234d);
            using (var writer = AttackWire.Write(AttackRequestPacket.FromRequest("nonce", request)))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(AttackWire.TryRead<AttackRequestPacket>(reader, out var parsed), Is.True);
                Assert.That(AttackAuthority.SamePayload(request, parsed.ToRequest()), Is.True);
            }
            var moved = Send(Request()).Orb; var wire = OrbWire.FromRecord(moved);
            Assert.That(AttackWire.ValidOrb(wire), Is.True); Assert.That(wire.ToRecord().TransferMotion, Is.EqualTo(moved.TransferMotion));
            wire.transferVelocityX = -1f; Assert.That(AttackWire.ValidOrb(wire), Is.False);
        }

        [TestCase("transferVelocityX")] [TestCase("transferVelocityY")] [TestCase("transferServerTime")]
        public void AbsentMotionCannotHideNonzeroWireData(string field)
        {
            var wire = OrbWire.FromRecord(orb); var f = typeof(OrbWire).GetField(field);
            f.SetValue(wire, f.FieldType == typeof(double) ? (object)1d : 1f);
            Assert.That(AttackWire.ValidOrb(wire), Is.False);
            var packet = new AttackRequestPacket { sessionId = Session, requestId = "request", orbId = orb.OrbId };
            var p = typeof(AttackRequestPacket).GetField(field); p.SetValue(packet, p.FieldType == typeof(double) ? (object)1d : 1f);
            var valid = typeof(AttackSession).GetMethod("ValidPacketShape", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That((bool)valid.Invoke(null, new object[] { packet }), Is.False);
        }

        [Test]
        public void ContinuousModeMustBeConfiguredBeforeAnyRound()
        {
            Assert.Throws<InvalidOperationException>(() => authority.ConfigureContinuousTransfers(.6f, .015f, 32f));
            var inactive = new AttackAuthority(registry, 100, 20);
            Assert.Throws<ArgumentOutOfRangeException>(() => inactive.ConfigureContinuousTransfers(float.NaN, .015f, 32f));
            Assert.Throws<ArgumentOutOfRangeException>(() => inactive.ConfigureContinuousTransfers(.6f, -1f, 32f));
            Assert.Throws<ArgumentOutOfRangeException>(() => inactive.ConfigureContinuousTransfers(.6f, .015f, 0));
        }

        [Test]
        public void TwoHundredMotionReceiptsAndHundredFlightsFitUnchangedMultiplayerEnvelope()
        {
            var snapshot = new AttackSnapshot { nonce = Guid.NewGuid().ToString("N"), sessionId = new string('s', 128),
                roundId = uint.MaxValue, revision = ulong.MaxValue, hp = 100, maxHp = 100,
                state = AttackBattleState.Playing.ToString(), orbs = new OrbWire[200], projectiles = new ProjectileWire[100] };
            for (int index = 0; index < 200; index++)
            {
                string id = index.ToString("D3") + new string('o', 125);
                bool flying = index >= 100;
                snapshot.orbs[index] = new OrbWire { id = id, owner = ulong.MaxValue - (ulong)(index % 5),
                    kind = (int)(flying ? OrbKind.Combined : OrbKind.Raw), polarity = (int)(flying ? OrbPolarity.None : OrbPolarity.Yang),
                    state = (int)(flying ? OrbAuthorityState.Projectile : OrbAuthorityState.Idle), pos = new Vector2(.055f, .8765432f),
                    sequence = ulong.MaxValue, transferCount = ulong.MaxValue, rightTransferCount = ulong.MaxValue,
                    lastTransferSequence = ulong.MaxValue, entrySide = (int)EntrySide.Left,
                    hasTransferMotion = true, transferVelocityX = 12.345679f, transferVelocityY = -23.456789f,
                    transferServerTime = 123456789.12345679d };
                if (flying) snapshot.projectiles[index - 100] = new ProjectileWire { id = id, owner = snapshot.orbs[index].owner,
                    radius = .165f, ballistic = true, position = new Vector3(12345.678f, -23456.789f, 34567.891f),
                    velocity = new Vector3(123.456f, -234.567f, 345.678f), gravity = new Vector3(0, -9.81f, 0), elapsed = 3.1234567f, lifetime = 4f };
            }
            Assert.That(AttackWire.ValidSnapshot(snapshot, 200), Is.True);
            using (var writer = AttackWire.Write(snapshot, AttackWire.MaximumMultiplayerInventoryBytes))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(writer.Length, Is.LessThanOrEqualTo(AttackWire.MaximumMultiplayerInventoryBytes));
                Assert.That(AttackWire.TryRead<AttackSnapshot>(reader, out var copy, AttackWire.MaximumMultiplayerInventoryBytes), Is.True);
                Assert.That(AttackWire.ValidSnapshot(copy, 200), Is.True);
                Assert.That(copy.orbs.All(value => value.hasTransferMotion), Is.True);
                Debug.Log("C6_CE24_ATTACK_ENVELOPE bytes=" + writer.Length + " maximum=" + AttackWire.MaximumMultiplayerInventoryBytes);
            }
        }
    }
}
