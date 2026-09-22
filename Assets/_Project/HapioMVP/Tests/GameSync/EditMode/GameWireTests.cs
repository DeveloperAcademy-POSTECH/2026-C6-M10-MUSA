using System;
using System.Globalization;
using System.Linq;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using C6.Prototype.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.GameSync.Tests
{
    public sealed class GameWireTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private const string OtherNonce = "44444444444444444444444444444444";
        private const ulong Peer = 41;

        [Test]
        public void ExactInitialReadyCanBeReceivedBeforeTheInitialAcknowledgement()
        {
            var snapshot = Ready();
            Assert.That(snapshot.initialStateConfirmed, Is.False);
            AssertValid(snapshot);
            snapshot.initialStateConfirmed = true;
            AssertValid(snapshot);
        }

        [Test]
        public void InitialInventoryResourceAndClockMustAllBeFresh()
        {
            Action<GameSnapshot>[] changes = {
                value => value.resources.players[0].stamina = 80,
                value => value.resources.players[1].generatedTotal = 1,
                value => value.resources.players[0].lastSequence = 1,
                value => value.resources.playing = true,
                value => { value.attack.orbs = new[] { Raw("raw", 0) }; value.resources.players[0].storedOrbs = 1; },
                value => { value.attack.hp = 80; value.battle.observedMonsterHp = 80; },
                value => { value.battle.remaining = 179; value.battle.teamHp = 179; }
            };
            foreach (var change in changes) { var value = Ready(); change(value); AssertInvalid(value); }
        }

        [Test]
        public void BothParticipantsInitialStateAndReadyAreRequiredBeforePlaying()
        {
            AssertValid(Playing());
            Action<GameSnapshot>[] changes = {
                value => value.initialStateConfirmed = false,
                value => value.p1.ready = false, value => value.p2.ready = false,
                value => value.p1.initialStateReceived = false, value => value.p2.initialStateReceived = false
            };
            foreach (var change in changes) { var value = Playing(); change(value); AssertInvalid(value); }
        }

        [Test]
        public void WrongSenderAndHostReceivingRemoteAuthorityAreRejectedBeforeApplying()
        {
            Assert.That(GameWire.AcceptFromSender(0, false, Ready(), Context(), null, out _), Is.True);
            Assert.That(GameWire.AcceptFromSender(Peer, false, Ready(), Context(), null, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("SNAPSHOT_SENDER"));
            Assert.That(GameWire.AcceptFromSender(0, true, Ready(), Context(), null, out _), Is.False);
        }

        [Test]
        public void EnvelopeAndAllComponentsMustMatchTheApprovedSessionRoundAndNonce()
        {
            Action<GameSnapshot>[] changes = {
                value => value.roomId = OtherNonce, value => value.sessionId = OtherNonce,
                value => value.nonce = OtherNonce, value => value.roundId = 0,
                value => value.attack.roundId++, value => value.resources.sessionId = OtherNonce,
                value => value.battle.nonce = OtherNonce
            };
            foreach (var change in changes) { var value = Ready(); change(value); AssertInvalid(value); }
            var expected = Context(); expected.minimumRoundId = 2;
            Assert.That(GameWire.Validate(Ready(), expected, null, out _), Is.False);
        }

        [Test]
        public void ConfigHashSeedAndEveryRepresentedTuningMustMatch()
        {
            Action<GameSnapshot>[] changes = {
                value => value.configHash = new string('a', 64), value => value.seed++, value => value.resources.seed++,
                value => value.resources.generateCost = 19, value => value.resources.regenerationRate = 5,
                value => value.resources.hitRecovery = 1, value => value.resources.maximum = 101,
                value => value.resources.storageLimit = 19, value => value.resources.debugTestMode = true,
                value => value.battle.developmentSolo = true, value => value.battle.shortDuration = true,
                value => { value.battle.duration = 181; value.battle.deadline = 281; }
            };
            foreach (var change in changes) { var value = Playing(); change(value); AssertInvalid(value); }
        }

        [Test]
        public void FullSnapshotComparesTheSamePlayerEvenWhenLocalPlayersHaveDifferentStamina()
        {
            var value = Playing();
            value.resources.players[0].stamina = 25.12345678901234d;
            value.resources.players[1].stamina = 85.25d;
            AssertValid(value);
            var remote = Copy(value); RewriteNonce(remote, OtherNonce);
            var expected = Context(); expected.nonce = OtherNonce;
            Assert.That(GameWire.Validate(remote, expected, null, out _), Is.True);
            Assert.That(GameWire.CanonicalHash(remote), Is.EqualTo(GameWire.CanonicalHash(value)));
        }

        [Test]
        public void ExactlyTheApprovedPlayerSetOwnsBothResourcesAndOrbs()
        {
            var value = Playing();
            value.attack.orbs = new[] { Raw("one", Peer) }; value.resources.players[1].storedOrbs = 1;
            AssertValid(value);
            value.attack.orbs[0].owner = 99; AssertInvalid(value);
            value = Playing(); value.resources.players[1].playerId = 99; AssertInvalid(value);
            value = Playing(); value.p2.clientId = 99; AssertInvalid(value);
            value = Playing(); value.p1.playerNumber = 2; AssertInvalid(value);
            value = Playing(); value.p2.connected = false; AssertInvalid(value);
        }

        [Test]
        public void StoredCountsAndProjectileIdentityAreCheckedTogether()
        {
            var value = WithProjectile(); AssertValid(value);
            value.resources.players[1].storedOrbs = 1; AssertInvalid(value);
            value = WithProjectile(); value.attack.projectiles[0].owner = 0; AssertInvalid(value);
            value = WithProjectile(); value.attack.projectiles[0].radius = .5f; AssertInvalid(value);
            value = WithProjectile(); value.attack.projectiles = Array.Empty<ProjectileWire>(); AssertInvalid(value);
            value = WithProjectile(); value.attack.orbs[0].kind = (int)OrbKind.Raw;
            value.attack.orbs[0].polarity = (int)OrbPolarity.Yin; AssertInvalid(value);
        }

        [Test]
        public void BallisticSnapshotUsesApprovedPhysicsAndHashesItsMotion()
        {
            var value = WithProjectile();
            var p = value.attack.projectiles[0];
            p.ballistic = true; p.velocity = new Vector3(0,3.5f,10); p.gravity = Vector3.down * 9.81f;
            p.elapsed = .1f; p.lifetime = 4;
            AssertValid(value);
            var same = Copy(value);
            Assert.That(GameWire.CanonicalHash(same), Is.EqualTo(GameWire.CanonicalHash(value)));
            same.attack.projectiles[0].velocity.x = .5f;
            Assert.That(GameWire.CanonicalHash(same), Is.Not.EqualTo(GameWire.CanonicalHash(value)));
            same = Copy(value); same.attack.projectiles[0].gravity.y = -12; AssertInvalid(same);
            same = Copy(value); same.attack.projectiles[0].lifetime = 5; AssertInvalid(same);
        }

        [Test]
        public void SnapshotRejectsHpAndResourceStateFromDifferentHitTransactions()
        {
            var value = Playing(); value.attack.hp = 80; AssertInvalid(value);
            value.battle.observedMonsterHp = 80; value.attack.roundHits = value.attack.totalHits = 1;
            value.resources.players[1].stamina = 85; AssertValid(value);
            value = Victory(); value.resources.playing = true; AssertInvalid(value);
        }

        [Test]
        public void OldAndDuplicateAggregateRevisionsCannotReplaceCurrentState()
        {
            var before = Playing(); var after = Next(before);
            Assert.That(GameWire.Validate(after, Context(), before, out _), Is.True);
            after.revision = before.revision; AssertInvalid(after, before);
            after.revision = 0; AssertInvalid(after, before);
            after = Next(before); after.attack.revision = 0; AssertInvalid(after, before);
        }

        [Test]
        public void NewRoundRequiresFreshInitialStateAndRejectsOlderRoundPackets()
        {
            var before = Victory(); var after = Ready();
            SetRound(after, 2); after.attack.totalHits = before.attack.totalHits;
            after.attack.resets = 1; after.hostNow = before.hostNow + 1; after.serverTime = before.serverTime + 1;
            Assert.That(GameWire.Validate(after, Context(), before, out _), Is.True);
            AssertInvalid(before, after);
            var skipped = Playing(); SetRound(skipped, 2); skipped.attack.totalHits = before.attack.totalHits;
            skipped.hostNow = before.hostNow + 1; skipped.serverTime = before.serverTime + 1;
            AssertInvalid(skipped, before);
        }

        [Test]
        public void AComponentCannotChangeWithoutAdvancingItsOwnRevision()
        {
            var before = Playing(); var after = Copy(before); after.revision++;
            after.resources.players[0].stamina = 80;
            AssertInvalid(after, before);
            after.resources.revision++;
            Assert.That(GameWire.Validate(after, Context(), before, out _), Is.True);
        }

        [Test]
        public void ExistingOrbIdentityCannotChangeOwnerKindOrMoveBackFromProjectile()
        {
            var before = WithProjectile(); var after = Next(before);
            after.attack.orbs[0].owner = after.attack.projectiles[0].owner = 0;
            AssertInvalid(after, before);
            after = Next(before); after.attack.orbs[0].state = (int)OrbAuthorityState.Idle;
            after.attack.projectiles = Array.Empty<ProjectileWire>(); after.resources.players[1].storedOrbs = 1;
            AssertInvalid(after, before);
            before = Playing(); before.attack.orbs = new[] { Raw("same", 0) }; before.resources.players[0].storedOrbs = 1;
            after = Next(before); after.attack.orbs[0].polarity = (int)OrbPolarity.Yang;
            AssertInvalid(after, before);
        }

        [Test]
        public void VictoryFreezesResultAndResourcesDespiteLaterTransportRevisions()
        {
            var before = Victory(); var after = Next(before);
            Assert.That(GameWire.Validate(after, Context(), before, out _), Is.True);
            after.resources.players[1].stamina += 1; AssertInvalid(after, before);
            after = Next(before); after.battle.remaining -= 1; after.battle.teamHp -= 1; AssertInvalid(after, before);
            after = Next(before); after.attack.orbs = new[] { Raw("late", 0) }; after.resources.players[0].storedOrbs = 1;
            AssertInvalid(after, before);
        }

        [Test]
        public void ElapsingBattleMayPauseResourceRecoveryWithoutCreatingAClientClock()
        {
            var value = Playing(); value.resources.playing = false; AssertValid(value);
            var next = Next(value); next.battle.remaining -= 1; next.battle.teamHp -= 1;
            Assert.That(GameWire.Validate(next, Context(), value, out _), Is.True);
            next.hostNow = value.hostNow - 1; AssertInvalid(next, value);
        }

        [Test]
        public void WireRoundTripPreservesFractionalResourcesAndUnsignedRevisions()
        {
            var original = Playing(); original.revision = ulong.MaxValue;
            original.resources.players[1].stamina = 15.12345678901234d;
            using (var writer = GameWire.Write(original))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(GameWire.TryRead(reader, out var received), Is.True); AssertValid(received);
                Assert.That(received.revision, Is.EqualTo(ulong.MaxValue));
                Assert.That(received.resources.players[1].stamina, Is.EqualTo(original.resources.players[1].stamina).Within(1e-12));
                Assert.That(GameWire.CanonicalHash(received), Is.EqualTo(GameWire.CanonicalHash(original)));
            }
        }

        [Test]
        public void CanonicalComparisonIgnoresOnlyRecipientNoncesAndUnorderedCollections()
        {
            var first = Playing(); first.attack.orbs = new[] { Raw("z", 0), Raw("a", Peer) };
            first.resources.players[0].storedOrbs = first.resources.players[1].storedOrbs = 1;
            var second = Copy(first); RewriteNonce(second, OtherNonce);
            Array.Reverse(second.attack.orbs); Array.Reverse(second.resources.players);
            Assert.That(GameWire.CanonicalHash(second), Is.EqualTo(GameWire.CanonicalHash(first)));
            second.serverTime += 1;
            Assert.That(GameWire.CanonicalHash(second), Is.Not.EqualTo(GameWire.CanonicalHash(first)));
            Assert.That(first.nonce, Is.EqualTo(Nonce), "Hashing must not mutate the live snapshot.");
        }

        [Test]
        public void ObservedMacHostAndClientDoubleUlpDifferencesUseTheDeclaredLogicalPrecision()
        {
            // Actual Mac r1 capture: transport/JsonUtility differed below the declared 1e-6
            // logical tolerance, but hashing JSON text previously yielded different SHA-256 values.
            var host = Ready(); host.hostNow = 22.277592001367346d; host.serverTime = 4.1223763159473314d;
            var client = Copy(host); client.hostNow = 22.27759200136735d; client.serverTime = 4.122376315947332d;
            RewriteNonce(client, OtherNonce);
            Assert.That(host.hostNow, Is.Not.EqualTo(client.hostNow));
            Assert.That(Math.Abs(host.hostNow - client.hostNow), Is.LessThan(GameWire.LogicalTolerance));
            Assert.That(GameWire.CanonicalHash(client), Is.EqualTo(GameWire.CanonicalHash(host)));
        }

        [Test]
        public void RepeatedActualUnityWireRoundTripsKeepTheSameCanonicalHash()
        {
            var value = Ready(); value.hostNow = 22.277592001367346d; value.serverTime = 4.1223763159473314d;
            string expected = GameWire.CanonicalHash(value);
            for (int iteration = 0; iteration < 32; iteration++)
            {
                using (var writer = GameWire.Write(value))
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    Assert.That(GameWire.TryRead(reader, out var received), Is.True);
                    AssertValid(received);
                    Assert.That(GameWire.CanonicalHash(received), Is.EqualTo(expected), "Unity wire round trip " + iteration);
                    value = received;
                }
            }
        }

        [Test]
        public void LogicalNumericEncodingKeepsOneMillionthDifferencesAndRejectsNonfiniteValues()
        {
            var first = Playing(); first.resources.players[0].stamina = 80;
            var changed = Copy(first); changed.resources.players[0].stamina = 80.000001d;
            Assert.That(GameWire.CanonicalHash(changed), Is.Not.EqualTo(GameWire.CanonicalHash(first)));
            changed = Copy(first); changed.hostNow = double.NaN;
            Assert.Throws<ArgumentException>(() => GameWire.CanonicalHash(changed));
            changed.hostNow = double.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => GameWire.CanonicalHash(changed));
            first.hostNow = 0d; changed = Copy(first); changed.hostNow = -0d;
            Assert.That(GameWire.CanonicalHash(changed), Is.EqualTo(GameWire.CanonicalHash(first)));
        }

        [Test]
        public void IntegerAndStringIdentityNeverLosePrecisionOrUseTheCurrentCulture()
        {
            var value = Playing(); value.revision = ulong.MaxValue;
            value.resources.players[1].lastSequence = ulong.MaxValue;
            string expected = GameWire.CanonicalHash(value);
            var beforeCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(GameWire.CanonicalHash(value), Is.EqualTo(expected));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.That(GameWire.CanonicalHash(value), Is.EqualTo(expected));
            }
            finally { CultureInfo.CurrentCulture = beforeCulture; }
            var changed = Copy(value); changed.revision--;
            Assert.That(GameWire.CanonicalHash(changed), Is.Not.EqualTo(expected));
            changed = Copy(value); changed.resources.players[1].lastSequence--;
            Assert.That(GameWire.CanonicalHash(changed), Is.Not.EqualTo(expected));
            changed = Copy(value); changed.roomId += " ";
            Assert.That(GameWire.CanonicalHash(changed), Is.Not.EqualTo(expected));
        }

        [Test]
        public void FullCapacitySnapshotHashDoesNotUseTheSmallLobbyConfigSizeLimit()
        {
            var value = Playing(); value.attack.orbs = Enumerable.Range(0, 40)
                .Select(index => Raw("raw-" + index.ToString("D3") + "-" + new string('x', 32), index < 20 ? 0UL : Peer)).ToArray();
            value.resources.players[0].storedOrbs = value.resources.players[1].storedOrbs = 20;
            Assert.That(Encoding.UTF8.GetByteCount(JsonUtility.ToJson(value)), Is.GreaterThan(4096));
            AssertValid(value); Assert.That(GameWire.CanonicalHash(value).Length, Is.EqualTo(64));
        }

        [Test]
        public void TruncatedWrongVersionInvalidUtf8AndOversizedFramesCannotBeRead()
        {
            AssertUnreadable(Frame(Encoding.UTF8.GetBytes("{}"), GameWire.Version - 1));
            AssertUnreadable(Frame(new byte[] { 0xff }, GameWire.Version));
            var wrongLength = Frame(Encoding.UTF8.GetBytes("{}"), GameWire.Version); wrongLength[1] = 3;
            AssertUnreadable(wrongLength);
            AssertUnreadable(new byte[] { GameWire.Version, 1, 0, 0, 0 });
            AssertUnreadable(Frame(new byte[GameWire.MaximumBytes], GameWire.Version));
            var value = Ready(); value.nonce = new string('x', GameWire.MaximumBytes);
            Assert.Throws<ArgumentException>(() => { using (var writer = GameWire.Write(value)) { } });
        }

        [Test]
        public void MissingInitialFlagFromSerializedPlayingStateCannotEnableActions()
        {
            string json = JsonUtility.ToJson(Playing()).Replace("\"initialStateConfirmed\":true,", string.Empty);
            var received = JsonUtility.FromJson<GameSnapshot>(json);
            Assert.That(received.initialStateConfirmed, Is.False); AssertInvalid(received);
        }

        [Test]
        public void RuntimeConfigFieldMapRoundTripsEveryCapturedValueWithoutEditingTheSavedSource()
        {
            var source = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            var target = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"upperFraction\":0.61,\"horizontalSwipeFraction\":0.25,"
                    + "\"horizontalDominance\":1.5,\"combinationRadiusFraction\":0.12,"
                    + "\"orbRadiusScreenFraction\":0.11,\"orbRadiusCapScale\":1.7,\"monsterMaxHp\":240,\"baseDamage\":30,"
                    + "\"monsterAttackFirstDelaySeconds\":28,\"monsterAttackIntervalSeconds\":18,"
                    + "\"monsterAttackWarningSeconds\":4,\"defenseHoldSeconds\":1.6,\"defenseFailPenaltySeconds\":12,"
                    + "\"orbStorageLimit\":18,\"projectileSpeed\":14,\"projectileLifetime\":4,\"projectileRadius\":0.25,"
                    + "\"attackSnapshotRateHz\":15,\"launchWidth\":4.5,\"launchOrigin\":{\"x\":1,\"y\":2,\"z\":-6},"
                    + "\"launchAim\":{\"x\":0.5,\"y\":1.5,\"z\":0.25},\"staminaMax\":120,\"staminaStart\":110,"
                    + "\"generateCost\":25,\"staminaRecoveryAmount\":15,\"staminaRecoverySeconds\":2.5,"
                    + "\"staminaHitRecovery\":7,\"battleDurationSeconds\":240,\"teamHpDecayPerSecond\":1.5}", source);
                JsonUtility.FromJsonOverwrite("{\"throwGravity\":12,\"throwForwardGain\":10,\"throwSampleWindow\":0.15,"
                    + "\"throwFloorFriction\":0.4,\"monsterHitboxSize\":{\"x\":1.4,\"y\":3,\"z\":0.8}}", source);
                string before = JsonUtility.ToJson(source);
                var approved = LobbyHostConfig.Capture(source);
                var fieldsType = typeof(GameRuntimeConfig).GetNestedType("Fields", System.Reflection.BindingFlags.NonPublic);
                Assert.That(fieldsType, Is.Not.Null);
                var fields = Activator.CreateInstance(fieldsType, new object[] { approved });
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fields), target);
                Assert.That(JsonUtility.ToJson(LobbyHostConfig.Capture(target)), Is.EqualTo(JsonUtility.ToJson(approved)));
                Assert.That(target.ResourceDebugToolsEnabled, Is.False);
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(target); }
        }

        private static GameSnapshotContext Context()
        {
            var asset = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            LobbyHostConfig config;
            try { config = LobbyHostConfig.Capture(asset); }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
            config.upperFraction=.5f; config.combinationRadius=.1f; config.horizontalSwipe=.2f;
            config.horizontalDominance=1.2f; config.projectileSpeed=10; config.projectileRadius=.2f;
            config.snapshotRate=10; config.launchWidth=5; config.launchOrigin=Vector3.zero;
            config.launchAim=Vector3.forward*10;
            return new GameSnapshotContext { roomId = Room, sessionId = Session, config = config,
                configHash = LobbyWire.Fingerprint(JsonUtility.ToJson(config)), seed = 123, p1 = 0, p2 = Peer, nonce = Nonce };
        }
        private static GameSnapshot Ready()
        {
            var context = Context();
            return new GameSnapshot { roomId = Room, sessionId = Session, roundId = 1, revision = 1,
                configHash = context.configHash, seed = context.seed, nonce = Nonce, hostNow = 100, serverTime = 200,
                p1 = new LobbyPlayer { clientId = 0, playerNumber = 1, connected = true, ready = true, initialStateReceived = true },
                p2 = new LobbyPlayer { clientId = Peer, playerNumber = 2, connected = true, ready = true, initialStateReceived = true },
                attack = new AttackSnapshot { nonce = Nonce, sessionId = Session, roundId = 1, revision = 1,
                    hp = 100, maxHp = 100, state = AttackBattleState.Playing.ToString() },
                resources = new ResourceSnapshot { nonce = Nonce, sessionId = Session, roundId = 1, revision = 1,
                    seed = context.seed, maximum = 100, generateCost = 20, regenerationRate = 20d / 3d,
                    hitRecovery = 5, storageLimit = 20, players = new[] {
                        new ResourcePlayerWire { playerId = 0, stamina = 100 },
                        new ResourcePlayerWire { playerId = Peer, stamina = 100 } } },
                battle = new BattleSnapshot { nonce = Nonce, sessionId = Session, roundId = 1, revision = 1,
                    phase = BattlePhase.Ready.ToString(), remaining = 180, teamHp = 180, duration = 180,
                    teamHpDecayPerSecond = 1, observedMonsterHp = 100, monsterMaxHp = 100, participants = 2 } };
        }
        private static GameSnapshot Playing()
        {
            var value = Ready(); value.initialStateConfirmed = true; value.resources.playing = true;
            value.battle.phase = BattlePhase.Playing.ToString(); value.battle.startedAt = 100; value.battle.deadline = 280;
            return value;
        }
        private static GameSnapshot Victory()
        {
            var value = Playing(); value.hostNow = 110; value.serverTime = 210;
            value.attack.hp = 0; value.attack.roundHits = value.attack.totalHits = 5; value.attack.state = AttackBattleState.Ended.ToString();
            value.resources.playing = false; value.resources.players[1].stamina = 65;
            value.battle.phase = BattlePhase.Victory.ToString(); value.battle.remaining = value.battle.teamHp = 170;
            value.battle.observedMonsterHp = 0; return value;
        }
        private static GameSnapshot WithProjectile()
        {
            var value = Playing();
            value.attack.orbs = new[] { new OrbWire { id = "projectile", owner = Peer, kind = (int)OrbKind.Combined,
                polarity = (int)OrbPolarity.None, state = (int)OrbAuthorityState.Projectile, pos = Vector2.one * .5f, sequence = 1 } };
            value.attack.projectiles = new[] { new ProjectileWire { id = "projectile", owner = Peer, radius = .2f, position = Vector3.forward } };
            return value;
        }
        private static OrbWire Raw(string id, ulong owner) => new OrbWire { id = id, owner = owner,
            kind = (int)OrbKind.Raw, polarity = (int)OrbPolarity.Yin, state = (int)OrbAuthorityState.Idle, pos = Vector2.one * .5f };
        private static GameSnapshot Copy(GameSnapshot value) => JsonUtility.FromJson<GameSnapshot>(JsonUtility.ToJson(value));
        private static GameSnapshot Next(GameSnapshot value)
        { var copy = Copy(value); copy.revision++; copy.attack.revision++; copy.resources.revision++; copy.battle.revision++; copy.hostNow++; copy.serverTime++; return copy; }
        private static void RewriteNonce(GameSnapshot value, string nonce)
        { value.nonce = value.attack.nonce = value.resources.nonce = value.battle.nonce = nonce; }
        private static void SetRound(GameSnapshot value, uint round)
        { value.roundId = value.attack.roundId = value.resources.roundId = value.battle.roundId = round; }
        private static void AssertValid(GameSnapshot value)
        { Assert.That(GameWire.Validate(value, Context(), null, out string reason), Is.True, reason); }
        private static void AssertInvalid(GameSnapshot value, GameSnapshot previous = null)
        { Assert.That(GameWire.Validate(value, Context(), previous, out string reason), Is.False, "Unexpected valid snapshot: " + reason); }
        private static byte[] Frame(byte[] content, int version)
        { var bytes = new byte[content.Length + 5]; bytes[0] = (byte)version; Array.Copy(BitConverter.GetBytes(content.Length), 0, bytes, 1, 4); Array.Copy(content, 0, bytes, 5, content.Length); return bytes; }
        private static void AssertUnreadable(byte[] bytes)
        { using (var reader = new FastBufferReader(bytes, Allocator.Temp)) Assert.That(GameWire.TryRead(reader, out _), Is.False); }
    }
}
