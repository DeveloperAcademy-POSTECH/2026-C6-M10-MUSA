using System;
using System.Linq;
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
    public sealed class GameTransferSnapshotTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private const ulong Peer = 41;
        private const float Inset = .055f;

        [Test]
        public void OlderSceneKeepsOwnerImmutableAndRejectsTransferMetadata()
        {
            var before = Playing(OrbKind.Raw);
            var after = Transfer(before, 1, Peer, EntrySide.Right, 1);
            Assert.That(GameWire.Validate(after, Context(false), before, out _), Is.False);
            after.attack.orbs[0].transferCount = after.attack.orbs[0].lastTransferSequence = 0;
            after.attack.orbs[0].entrySide = (int)EntrySide.None;
            Assert.That(GameWire.Validate(after, Context(false), before, out _), Is.False);
            Assert.That(GameWire.Validate(before, Context(false), null, out string reason), Is.True, reason);
        }

        [TestCase(OrbKind.Raw, EntrySide.Right)]
        [TestCase(OrbKind.Combined, EntrySide.Left)]
        public void ApprovedTransferPreservesIdKindAndPolarityAtTheOppositeEntry(OrbKind kind, EntrySide side)
        {
            var before = Playing(kind); var after = Transfer(before, 1, Peer, side, 1);
            AssertValid(after, before);
            Assert.That(after.attack.orbs.Select(orb => orb.id), Is.EquivalentTo(before.attack.orbs.Select(orb => orb.id)));
            Assert.That(after.attack.orbs[0].kind, Is.EqualTo(before.attack.orbs[0].kind));
            Assert.That(after.attack.orbs[0].polarity, Is.EqualTo(before.attack.orbs[0].polarity));
            Assert.That(after.resources.players[0].storedOrbs, Is.Zero);
            Assert.That(after.resources.players[1].storedOrbs, Is.EqualTo(1));
        }

        [Test]
        public void EntryHeightAndTransferMetadataRemainBoundedAndInternallyConsistent()
        {
            Action<OrbWire>[] invalid = {
                orb => orb.entrySide = (int)EntrySide.None,
                orb => orb.entrySide = 999,
                orb => orb.lastTransferSequence = 0,
                orb => orb.lastTransferSequence = 2,
                orb => orb.transferCount = 2,
                orb => orb.pos = new Vector2(.5f, .5f),
                orb => orb.pos = new Vector2(1f - Inset, 1.01f),
                orb => orb.pos = new Vector2(1f - Inset, float.NaN),
                orb => orb.transferCount = 0
            };
            var before = Playing(OrbKind.Raw);
            foreach (var change in invalid)
            {
                var after = Transfer(before, 1, Peer, EntrySide.Right, 1); change(after.attack.orbs[0]);
                AssertInvalid(after, before);
            }
            var edge = Transfer(before, 1, Peer, EntrySide.Right, 1);
            edge.attack.orbs[0].pos.y = 0; AssertValid(edge, before);
            edge.attack.orbs[0].pos.y = 1; AssertValid(edge, before);
        }

        [Test]
        public void SkippedSnapshotsCanContainAnApprovedTransferFollowedByLaunch()
        {
            var before = Playing(OrbKind.Combined);
            var after = Transfer(before, 1, Peer, EntrySide.Right, 3);
            after.revision += 5; after.attack.revision += 5;
            var orb = after.attack.orbs[0]; orb.sequence = 4; orb.state = (int)OrbAuthorityState.Projectile;
            // The launch position is no longer the earlier entry position; the entry metadata remains.
            orb.pos = new Vector2(.4f, .8f);
            after.attack.projectiles = new[] { new ProjectileWire { id = orb.id, owner = Peer,
                position = Vector3.forward, radius = Context(true).config.projectileRadius } };
            Recount(after);
            AssertValid(after, before);
        }

        [Test]
        public void SkippedEvenTransfersReturnTheSameIdToItsOriginalOwner()
        {
            var before = Playing(OrbKind.Raw); var after = Transfer(before, 2, 0, EntrySide.Left, 2);
            after.revision += 5; AssertValid(after, before);
            after.attack.orbs[0].owner = Peer; Recount(after); AssertInvalid(after, before);
        }

        [Test]
        public void TransferParityAlsoAppliesWhenAnEarlierTransferWasAlreadyObserved()
        {
            var before = Transfer(Playing(OrbKind.Raw), 1, Peer, EntrySide.Right, 1);
            var after = Transfer(before, 3, Peer, EntrySide.Left, 3); AssertValid(after, before);
            after = Transfer(before, 2, 0, EntrySide.Left, 2); AssertValid(after, before);
            after = Transfer(before, 2, Peer, EntrySide.Left, 2); AssertInvalid(after, before);
        }

        [Test]
        public void OwnerAndEntryCannotChangeWithoutAdvancingTheTransferCounter()
        {
            var before = Transfer(Playing(OrbKind.Raw), 1, Peer, EntrySide.Right, 1);
            var after = Next(before); after.attack.orbs[0].owner = 0; Recount(after); AssertInvalid(after, before);
            after = Next(before); after.attack.orbs[0].entrySide = (int)EntrySide.Left;
            after.attack.orbs[0].pos.x = Inset; AssertInvalid(after, before);
            after = Next(before); after.attack.orbs[0].lastTransferSequence = after.attack.orbs[0].sequence = 2;
            AssertInvalid(after, before);
            after = Next(before); after.attack.orbs[0].transferCount = 0;
            after.attack.orbs[0].lastTransferSequence = 0; after.attack.orbs[0].entrySide = (int)EntrySide.None;
            AssertInvalid(after, before);
        }

        [Test]
        public void ClaimedTransfersMustAdvanceThePerOrbSequenceEnoughTimes()
        {
            var before = Playing(OrbKind.Raw); before.attack.orbs[0].sequence = 10;
            var after = Transfer(before, 1, Peer, EntrySide.Left, 10); AssertInvalid(after, before);
            after = Transfer(before, 2, 0, EntrySide.Left, 11); AssertInvalid(after, before);
            after = Transfer(before, 2, 0, EntrySide.Left, 12); AssertValid(after, before);
        }

        [Test]
        public void AProjectileCanNeverTransferEvenWithValidCountParity()
        {
            var before = Playing(OrbKind.Combined); before.attack.orbs[0].state = (int)OrbAuthorityState.Projectile;
            before.attack.orbs[0].sequence = 1;
            before.attack.projectiles = new[] { new ProjectileWire { id = "orb", owner = 0,
                position = Vector3.forward, radius = Context(true).config.projectileRadius } };
            Recount(before);
            var after = Transfer(before, 1, Peer, EntrySide.Left, 2);
            after.attack.orbs[0].state = (int)OrbAuthorityState.Projectile;
            after.attack.projectiles[0].owner = Peer; Recount(after);
            AssertInvalid(after, before);
        }

        [Test]
        public void TransferMetadataCannotDisguiseAnIdKindOrPolarityChange()
        {
            var before = Playing(OrbKind.Raw); var after = Transfer(before, 1, Peer, EntrySide.Right, 1);
            after.attack.orbs[0].polarity = (int)OrbPolarity.Yang; AssertInvalid(after, before);
            after = Transfer(before, 1, Peer, EntrySide.Right, 1);
            after.attack.orbs[0].kind = (int)OrbKind.Combined; after.attack.orbs[0].polarity = (int)OrbPolarity.None;
            AssertInvalid(after, before);
        }

        [Test]
        public void ConcurrentGenerationDoesNotFalselyImposeEqualAggregateOrbSetSizes()
        {
            var before = Playing(OrbKind.Raw); var after = Transfer(before, 1, Peer, EntrySide.Right, 1);
            after.attack.orbs = after.attack.orbs.Concat(new[] { new OrbWire { id = "another", owner = 0,
                kind = (int)OrbKind.Raw, polarity = (int)OrbPolarity.Yang, pos = Vector2.one * .5f } }).ToArray();
            after.resources.players[0].generatedTotal++;
            after.resources.players[0].lastSequence++; after.resources.players[0].stamina = 80; Recount(after);
            AssertValid(after, before);
        }

        [Test]
        public void TerminalMetadataIsFrozenAndResetRequiresTheEmptyNextRound()
        {
            var before = Transfer(Playing(OrbKind.Raw), 1, Peer, EntrySide.Right, 1);
            before.attack.hp = before.battle.observedMonsterHp = 0;
            before.attack.roundHits = before.attack.totalHits = 5; before.attack.state = "Ended";
            before.battle.phase = "Victory"; before.resources.playing = false;
            var after = Next(before); AssertValid(after, before);
            after.attack.orbs[0].entrySide = (int)EntrySide.Left; after.attack.orbs[0].pos.x = Inset;
            AssertInvalid(after, before);
            after = Ready(); SetRound(after, 2); after.attack.totalHits = 5; after.attack.resets = 1;
            after.hostNow = before.hostNow + 1; after.serverTime = before.serverTime + 1;
            AssertValid(after, before);
            AssertInvalid(before, after);
        }

        [Test]
        public void WireAndRecordRoundTripsRetainEntrySideAndExactTransferCounters()
        {
            var value = Transfer(Playing(OrbKind.Raw), 1, Peer, EntrySide.Left, 1);
            value.attack.orbs[0].transferCount = value.attack.orbs[0].lastTransferSequence = value.attack.orbs[0].sequence = ulong.MaxValue;
            using (var writer = GameWire.Write(value))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(GameWire.TryRead(reader, out var received), Is.True);
                var orb = received.attack.orbs[0];
                Assert.That(orb.transferCount, Is.EqualTo(ulong.MaxValue));
                Assert.That(orb.lastTransferSequence, Is.EqualTo(ulong.MaxValue));
                Assert.That(orb.entrySide, Is.EqualTo((int)EntrySide.Left));
                var restored = OrbWire.FromRecord(orb.ToRecord());
                Assert.That(restored.transferCount, Is.EqualTo(orb.transferCount));
                Assert.That(restored.lastTransferSequence, Is.EqualTo(orb.lastTransferSequence));
                Assert.That(restored.entrySide, Is.EqualTo(orb.entrySide));
                Assert.That(GameWire.CanonicalHash(received), Is.EqualTo(GameWire.CanonicalHash(value)));
            }
        }

        [Test]
        public void CanonicalHashIncludesEveryTransferMetadataField()
        {
            var before = Transfer(Playing(OrbKind.Raw), 1, Peer, EntrySide.Left, 1);
            string hash = GameWire.CanonicalHash(before);
            var after = Copy(before); after.attack.orbs[0].entrySide = (int)EntrySide.Right;
            Assert.That(GameWire.CanonicalHash(after), Is.Not.EqualTo(hash));
            after = Copy(before); after.attack.orbs[0].transferCount++;
            Assert.That(GameWire.CanonicalHash(after), Is.Not.EqualTo(hash));
            after = Copy(before); after.attack.orbs[0].lastTransferSequence++;
            Assert.That(GameWire.CanonicalHash(after), Is.Not.EqualTo(hash));
        }

        [Test]
        public void MissingLegacyMetadataDefaultsToZeroAndNeverEnablesTransferSupport()
        {
            var orb = JsonUtility.FromJson<OrbWire>("{\"id\":\"legacy\",\"owner\":0,\"kind\":0,\"polarity\":0,\"state\":0,\"pos\":{\"x\":0.5,\"y\":0.5},\"sequence\":0}");
            Assert.That(orb.transferCount, Is.Zero); Assert.That(orb.lastTransferSequence, Is.Zero);
            Assert.That(orb.entrySide, Is.EqualTo((int)EntrySide.None));
            var value = Playing(OrbKind.Raw); value.attack.orbs[0] = orb;
            Assert.That(GameWire.Validate(value, Context(false), null, out string reason), Is.True, reason);
        }

        private static GameSnapshotContext Context(bool transfers)
        {
            var asset = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            LobbyHostConfig config;
            try { config = LobbyHostConfig.Capture(asset); }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
            return new GameSnapshotContext { roomId = Room, sessionId = Session, config = config,
                configHash = LobbyWire.Fingerprint(JsonUtility.ToJson(config)), seed = 123, p1 = 0, p2 = Peer,
                nonce = Nonce, allowTransfers = transfers, transferEdgeInset = Inset };
        }
        private static GameSnapshot Ready()
        {
            var c = Context(true);
            return new GameSnapshot { roomId = Room, sessionId = Session, nonce = Nonce, roundId = 1, revision = 1,
                seed = c.seed, configHash = c.configHash, hostNow = 100, serverTime = 200,
                p1 = new LobbyPlayer { clientId = 0, playerNumber = 1, connected = true, ready = true, initialStateReceived = true },
                p2 = new LobbyPlayer { clientId = Peer, playerNumber = 2, connected = true, ready = true, initialStateReceived = true },
                attack = new AttackSnapshot { nonce = Nonce, sessionId = Session, roundId = 1, revision = 1,
                    hp = 100, maxHp = 100, state = "Playing" },
                resources = new ResourceSnapshot { nonce = Nonce, sessionId = Session, roundId = 1, revision = 1, seed = c.seed,
                    maximum = 100, generateCost = 20, regenerationRate = 20d / 3d, hitRecovery = 5, storageLimit = 20,
                    players = new[] { new ResourcePlayerWire { playerId = 0, stamina = 100 }, new ResourcePlayerWire { playerId = Peer, stamina = 100 } } },
                battle = new BattleSnapshot { nonce = Nonce, sessionId = Session, roundId = 1, revision = 1, phase = "Ready",
                    remaining = 180, teamHp = 180, duration = 180, teamHpDecayPerSecond = 1,
                    observedMonsterHp = 100, monsterMaxHp = 100, participants = 2 } };
        }
        private static GameSnapshot Playing(OrbKind kind)
        {
            var value = Ready(); value.initialStateConfirmed = true; value.resources.playing = true;
            value.battle.phase = "Playing"; value.battle.startedAt = 100; value.battle.deadline = 280;
            value.attack.orbs = new[] { new OrbWire { id = "orb", owner = 0, kind = (int)kind,
                polarity = kind == OrbKind.Raw ? (int)OrbPolarity.Yin : (int)OrbPolarity.None,
                state = (int)OrbAuthorityState.Idle, pos = Vector2.one * .5f } };
            Recount(value); return value;
        }
        private static GameSnapshot Transfer(GameSnapshot before, ulong count, ulong owner, EntrySide side, ulong sequence)
        {
            var after = Next(before); var orb = after.attack.orbs[0];
            orb.owner = owner; orb.transferCount = count; orb.lastTransferSequence = orb.sequence = sequence;
            orb.entrySide = (int)side; orb.pos = new Vector2(side == EntrySide.Left ? Inset : 1f - Inset, .75f);
            Recount(after); return after;
        }
        private static void Recount(GameSnapshot value)
        {
            foreach (var player in value.resources.players)
                player.storedOrbs = value.attack.orbs.Count(orb => orb.owner == player.playerId
                    && (orb.state == (int)OrbAuthorityState.Idle || orb.state == (int)OrbAuthorityState.Launching));
        }
        private static GameSnapshot Copy(GameSnapshot value) => JsonUtility.FromJson<GameSnapshot>(JsonUtility.ToJson(value));
        private static GameSnapshot Next(GameSnapshot value)
        { var copy = Copy(value); copy.revision++; copy.attack.revision++; copy.resources.revision++; copy.battle.revision++; copy.hostNow++; copy.serverTime++; return copy; }
        private static void SetRound(GameSnapshot value, uint round)
        { value.roundId = value.attack.roundId = value.resources.roundId = value.battle.roundId = round; }
        private static void AssertValid(GameSnapshot value, GameSnapshot previous)
        { Assert.That(GameWire.Validate(value, Context(true), previous, out string reason), Is.True, reason); }
        private static void AssertInvalid(GameSnapshot value, GameSnapshot previous)
        { Assert.That(GameWire.Validate(value, Context(true), previous, out string reason), Is.False, reason); }
    }
}
