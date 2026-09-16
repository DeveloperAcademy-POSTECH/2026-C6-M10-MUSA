using System;
using System.Reflection;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class ReleaseThrowAuthorityWireTests
    {
        private HostOrbRegistry registry;
        private AttackAuthority authority;
        private OrbRecord orb;
        private static ThrowTuning Tuning => new ThrowTuning(.35f, 8f, 8f, 2.8f, 6f, 30f, 9.81f, .45f, 4f, .2f, .12f, .02f);
        private static ProjectileLaunchBasis Basis => new ProjectileLaunchBasis(new Vector3(0, 1, -5), Vector3.right, 8, new Vector3(0, 1, 5));
        private static OrbThrowInput Normal => new OrbThrowInput(new Vector2(.025f, .15f), .1f);

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true); registry.BeginSession("p2-throw-session", 1);
            authority = new AttackAuthority(registry, 100, 20); authority.ConfigureReleaseThrows(Basis, Tuning);
            authority.BeginDevelopmentRound();
            orb = registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
        }

        [Test]
        public void LegacyPayloadCannotSpendAnOrbInReleaseModeAndItsRejectionIsQueryable()
        {
            var request = Request(null);
            var rejected = authority.RequestLaunch(7, request);
            Assert.That(rejected.Accepted, Is.False); Assert.That(rejected.Reason, Is.EqualTo("THROW_INPUT_REQUIRED"));
            AssertUnspent();
            var query = authority.QueryRequest(7, request);
            Assert.That(query.Known, Is.True); Assert.That(query.Receipt.Accepted, Is.False);
            Assert.That(authority.RequestLaunch(7, request).IsDuplicate, Is.True);
            Assert.That(authority.QueryRequest(7, Request(Normal)).Known, Is.False);
        }

        [TestCase("nan-x")]
        [TestCase("infinite-y")]
        [TestCase("nan-duration")]
        [TestCase("zero-duration")]
        [TestCase("negative-duration")]
        [TestCase("too-short")]
        [TestCase("too-long")]
        [TestCase("huge-displacement")]
        [TestCase("downward")]
        [TestCase("no-upward-speed")]
        public void InvalidReleaseSamplesProduceStableReceiptsBeforeAnyReservation(string kind)
        {
            var input = Normal;
            switch (kind)
            {
                case "nan-x": input = new OrbThrowInput(new Vector2(float.NaN, .15f), .1f); break;
                case "infinite-y": input = new OrbThrowInput(new Vector2(.1f, float.PositiveInfinity), .1f); break;
                case "nan-duration": input = new OrbThrowInput(Vector2.up * .15f, float.NaN); break;
                case "zero-duration": input = new OrbThrowInput(Vector2.up * .15f, 0); break;
                case "negative-duration": input = new OrbThrowInput(Vector2.up * .15f, -.1f); break;
                case "too-short": input = new OrbThrowInput(Vector2.up * .15f, .001f); break;
                case "too-long": input = new OrbThrowInput(Vector2.up * .15f, .5f); break;
                case "huge-displacement": input = new OrbThrowInput(Vector2.up * 1000, .1f); break;
                case "downward": input = new OrbThrowInput(Vector2.down * .15f, .1f); break;
                case "no-upward-speed": input = new OrbThrowInput(Vector2.right * .15f, .1f); break;
            }
            var request = Request(input);
            var rejected = authority.RequestLaunch(7, request);
            Assert.That(rejected.Accepted || rejected.SpawnRequired || rejected.BallisticLaunch.HasValue, Is.False);
            AssertUnspent();
            var query = authority.QueryRequest(7, request);
            Assert.That(query.Known, Is.True); Assert.That(query.Receipt.Reason, Is.EqualTo(rejected.Reason));
            Assert.That(authority.RequestLaunch(7, request).IsDuplicate, Is.True);
            Assert.That(authority.RequestLaunch(7, Request(Normal, "fresh-valid", 1)).Accepted, Is.True,
                "A rejected sample must not consume the orb's valid sequence.");
        }

        [Test]
        public void ValidThrowIsCalculatedOnceAndDuplicateReceiptKeepsTheOriginalBallistics()
        {
            var request = Request(Normal);
            var first = authority.RequestLaunch(7, request);
            Assert.That(first.SpawnRequired && first.BallisticLaunch.HasValue, Is.True);
            Assert.That(ThrowMapping.TryCalculate(request.NormalizedPosition, Normal, Basis, Tuning, out var expected, out _), Is.True);
            Assert.That(first.BallisticLaunch.Value.Position, Is.EqualTo(expected.Position));
            Assert.That(first.BallisticLaunch.Value.InitialVelocity, Is.EqualTo(expected.InitialVelocity));
            Assert.That(first.BallisticLaunch.Value.GravityVector, Is.EqualTo(Vector3.down * 9.81f));
            Assert.That(authority.MarkProjectileSpawned(request.SessionId, 1, orb.OrbId), Is.True);
            var duplicate = authority.RequestLaunch(7, request);
            Assert.That(duplicate.Accepted && duplicate.IsDuplicate, Is.True);
            Assert.That(duplicate.SpawnRequired, Is.False);
            Assert.That(duplicate.BallisticLaunch.Value.InitialVelocity, Is.EqualTo(first.BallisticLaunch.Value.InitialVelocity));
            Assert.That(authority.QueryRequest(7, request).ConfirmedOrb.AuthorityState, Is.EqualTo(OrbAuthorityState.Projectile));
            Assert.That(authority.MonsterHp, Is.EqualTo(100), "An approved launch never creates an immediate hit.");
        }

        [TestCase("delta")]
        [TestCase("duration")]
        [TestCase("absent")]
        [TestCase("position")]
        public void AlteredThrowPayloadCannotReplayOrQueryAnAcceptedIdentity(string change)
        {
            var original = Request(Normal); Assert.That(authority.RequestLaunch(7, original).Accepted, Is.True);
            OrbThrowInput? modified = change == "absent" ? (OrbThrowInput?)null
                : change == "delta" ? new OrbThrowInput(Normal.Delta + Vector2.right * .001f, Normal.Duration)
                : change == "duration" ? new OrbThrowInput(Normal.Delta, Normal.Duration + .001f) : Normal;
            var altered = Request(modified, position: change == "position" ? new Vector2(.51f, .8f) : (Vector2?)null);
            var conflict = authority.RequestLaunch(7, altered);
            Assert.That(conflict.Accepted || conflict.SpawnRequired, Is.False);
            Assert.That(conflict.Reason, Is.EqualTo("REQUEST_ID_CONFLICT"));
            Assert.That(authority.QueryRequest(7, altered).Reason, Is.EqualTo("REQUEST_PAYLOAD_MISMATCH"));
            Assert.That(authority.QueryRequest(7, original).Known, Is.True);
            Assert.That(authority.RequestLaunch(8, original).Reason, Is.EqualTo("REQUEST_ID_CONFLICT"));
        }

        [Test]
        public void ThrowInputCannotBeSmuggledIntoTransferOrAnotherPlayersOrb()
        {
            Assert.That(authority.RequestLaunch(8, Request(Normal)).Reason, Is.EqualTo("OWNER_MISMATCH"));
            AssertUnspent();
            var transfer = new OrbActionRequest("p2-throw-session", 1, "transfer-with-input", orb.OrbId, null,
                OrbActionKind.TransferRight, 1, new Vector2(.8f, .5f), Normal);
            Assert.That(authority.RequestTransfer(7, transfer, 8, true, 20, .055f).Reason, Is.EqualTo("UNEXPECTED_THROW_INPUT"));
            AssertUnspent();
        }

        [Test]
        public void HistoricalAuthorityPreservesFixedLaunchAndRejectsReleasePayload()
        {
            var old = new AttackAuthority(registry, 100, 20); old.BeginDevelopmentRound();
            Assert.That(old.RequestLaunch(7, Request(Normal, "new-payload")).Reason, Is.EqualTo("THROW_MODE_DISABLED"));
            AssertUnspent();
            var accepted = old.RequestLaunch(7, Request(null, "legacy"));
            Assert.That(accepted.SpawnRequired, Is.True); Assert.That(accepted.BallisticLaunch.HasValue, Is.False);
            Assert.Throws<InvalidOperationException>(() => old.ConfigureReleaseThrows(Basis, Tuning));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullableInputRoundTripsTheBoundedWireWithoutLosingAnyActionField(bool present)
        {
            var request = Request(present ? Normal : (OrbThrowInput?)null);
            var packet = AttackRequestPacket.FromRequest(Guid.NewGuid().ToString("N"), request);
            using (var writer = AttackWire.Write(packet))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(AttackWire.TryRead<AttackRequestPacket>(reader, out var copy), Is.True);
                Assert.That(AttackAuthority.SamePayload(request, copy.ToRequest()), Is.True);
                Assert.That(copy.hasThrowInput, Is.EqualTo(present));
                if (present) { Assert.That(copy.throwDelta, Is.EqualTo(Normal.Delta)); Assert.That(copy.throwDuration, Is.EqualTo(Normal.Duration)); }
                else Assert.That(copy.ToRequest().ThrowInput.HasValue, Is.False);
            }
        }

        [Test]
        public void LegacyJsonWithoutThrowFieldsRemainsAbsentAndCannotBecomeAReleaseThrow()
        {
            var packet = JsonUtility.FromJson<AttackRequestPacket>("{\"sessionId\":\"p2-throw-session\",\"roundId\":1,\"requestId\":\"old\",\"orbId\":\"old-orb\",\"kind\":0,\"sequence\":1,\"pos\":{\"x\":0.5,\"y\":0.8}}");
            Assert.That(packet.ToRequest().ThrowInput.HasValue, Is.False);
            Assert.That(authority.RequestLaunch(7, packet.ToRequest()).Reason, Is.EqualTo("THROW_INPUT_REQUIRED"));
        }

        [TestCase("nan-velocity")]
        [TestCase("infinite-gravity")]
        [TestCase("upward-gravity")]
        [TestCase("huge-speed")]
        [TestCase("huge-position")]
        [TestCase("negative-time")]
        [TestCase("past-lifetime")]
        [TestCase("zero-lifetime")]
        [TestCase("oversized-lifetime")]
        [TestCase("legacy-hidden-motion")]
        public void InvalidProjectileMotionCannotEnterAConfirmedSnapshot(string fault)
        {
            var snapshot = Snapshot(); var body = snapshot.projectiles[0];
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.True);
            switch (fault)
            {
                case "nan-velocity": body.velocity.x = float.NaN; break;
                case "infinite-gravity": body.gravity.y = float.NegativeInfinity; break;
                case "upward-gravity": body.gravity.y = 9.81f; break;
                case "huge-speed": body.velocity.x = 2000; break;
                case "huge-position": body.position.x = 200000; break;
                case "negative-time": body.elapsed = -.1f; break;
                case "past-lifetime": body.elapsed = 5; break;
                case "zero-lifetime": body.lifetime = 0; break;
                case "oversized-lifetime": body.lifetime = 100; break;
                case "legacy-hidden-motion": body.ballistic = false; break;
            }
            Assert.That(AttackWire.ValidSnapshot(snapshot), Is.False);
        }

        [Test]
        public void BallisticSnapshotRoundTripRetainsActualMotionAndLegacyZeroFieldsRemainValid()
        {
            var original = Snapshot();
            using (var writer = AttackWire.Write(original))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(AttackWire.TryRead<AttackSnapshot>(reader, out var copy), Is.True);
                Assert.That(AttackWire.ValidSnapshot(copy), Is.True);
                Assert.That(copy.projectiles[0].velocity, Is.EqualTo(original.projectiles[0].velocity));
                Assert.That(copy.projectiles[0].gravity, Is.EqualTo(original.projectiles[0].gravity));
                Assert.That(copy.projectiles[0].elapsed, Is.EqualTo(.25f));
                Assert.That(copy.projectiles[0].lifetime, Is.EqualTo(4));
            }
            var legacy = original.projectiles[0]; legacy.ballistic = false;
            legacy.velocity = legacy.gravity = Vector3.zero; legacy.elapsed = legacy.lifetime = 0;
            Assert.That(AttackWire.ValidSnapshot(original), Is.True);
        }

        [Test]
        public void ReleaseModeUsesDistinctMessagesAndCannotChangeAfterBindingBegins()
        {
            var root = new GameObject("P2 mode boundary"); root.SetActive(false);
            try
            {
                var session = root.AddComponent<AttackSession>();
                foreach (string name in new[] { "Hello", "Request", "Query", "Reply", "Snapshot" })
                    Assert.That(Message(session, name), Does.StartWith("C6.T06."));
                session.ConfigureReleaseThrows(true);
                foreach (string name in new[] { "Hello", "Request", "Query", "Reply", "Snapshot" })
                    Assert.That(Message(session, name), Does.StartWith("C6.P2."));
                typeof(AttackSession).GetField("explicitDevelopmentRequested", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, true);
                Assert.Throws<InvalidOperationException>(() => session.ConfigureReleaseThrows(false));
                Assert.That(session.ReleaseThrowsEnabled, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void AuthenticatedSenderUsesItsRosterLaunchFrame()
        {
            var participantRegistry =
                new HostOrbRegistry(true);

            participantRegistry.BeginSession(
                "participant-throw-session",
                1
            );

            var participantAuthority =
                new AttackAuthority(
                    participantRegistry,
                    100,
                    20
                );

            var baseline = new ProjectileLaunchBasis(
                new Vector3(0f, 1f, -5f),
                Vector3.right,
                8f,
                new Vector3(0f, 1f, 0f)
            );

            participantAuthority.ConfigureReleaseThrows(
                baseline,
                Tuning
            );

            participantAuthority.ConfigureParticipantThrowFrames(
                new[] { 0ul, 7ul }
            );

            participantAuthority.BeginDevelopmentRound();

            OrbRecord p2Orb =
                participantRegistry.RegisterDevelopmentOrb(
                    7,
                    OrbKind.Combined,
                    OrbPolarity.None,
                    Vector2.one * 0.5f
                );

            var request = new OrbActionRequest(
                "participant-throw-session",
                1,
                "p2-participant-throw",
                p2Orb.OrbId,
                null,
                OrbActionKind.Launch,
                1,
                new Vector2(0.5f, 0.8f),
                Normal
            );

            AttackLaunchResult result =
                participantAuthority.RequestLaunch(
                    7,
                    request
                );

            Assert.That(
                result.Accepted,
                Is.True,
                result.Reason
            );

            Assert.That(
                result.BallisticLaunch.HasValue,
                Is.True
            );

            BallisticLaunch launch =
                result.BallisticLaunch.Value;

            Assert.That(
                Vector3.Distance(
                    launch.Position,
                    new Vector3(0f, 1f, 5f)
                ),
                Is.LessThan(0.0001f)
            );

            Assert.That(
                Vector3.Distance(
                    launch.InitialVelocity,
                    new Vector3(-1.5f, 4.2f, -12f)
                ),
                Is.LessThan(0.0001f)
            );
        }

        private static string Message(AttackSession session, string name) => (string)typeof(AttackSession)
            .GetProperty(name + "Message", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        private OrbActionRequest Request(OrbThrowInput? input, string id = "throw-1", ulong sequence = 1, Vector2? position = null) =>
            new OrbActionRequest("p2-throw-session", 1, id, orb.OrbId, null, OrbActionKind.Launch, sequence, position ?? new Vector2(.5f, .8f), input);
        private void AssertUnspent()
        {
            Assert.That(registry.TryGet(orb.OrbId, out var current), Is.True);
            Assert.That(current, Is.SameAs(orb)); Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(authority.MonsterHp, Is.EqualTo(100)); Assert.That(authority.ValidHitCount, Is.Zero);
        }
        private AttackSnapshot Snapshot()
        {
            var wire = OrbWire.FromRecord(orb); wire.state = (int)OrbAuthorityState.Projectile;
            return new AttackSnapshot { nonce = Guid.NewGuid().ToString("N"), sessionId = "p2-throw-session", roundId = 1, revision = 1,
                hp = 100, maxHp = 100, state = "Playing", orbs = new[] { wire }, projectiles = new[] { new ProjectileWire
                { id = orb.OrbId, owner = 7, position = new Vector3(0, 1, -3), radius = .2f, ballistic = true,
                    velocity = new Vector3(1, 3, 12), gravity = Vector3.down * 9.81f, elapsed = .25f, lifetime = 4 } } };
        }

    }
}
