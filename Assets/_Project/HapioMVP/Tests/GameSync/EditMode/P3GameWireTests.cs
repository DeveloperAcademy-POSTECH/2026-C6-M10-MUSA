using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Lobby;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using C6.Prototype.Resources;
using C6.Prototype.Battle;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.GameSync.Tests
{
    public sealed class P3GameWireTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private const float Inset = .055f;
        private static readonly ulong[] Seats = { 0, 41, 57, 88, 99 };

        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ApprovedFrozenSeatOrderOwnsEveryInitialResourceAndPlayingState(int count)
        {
            var value = Ready(count); AssertValid(value, count);
            Assert.That(value.players.Length, Is.EqualTo(count));
            value = Playing(count); AssertValid(value, count);
            using (var writer = GameWire.Write(value))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(GameWire.TryRead(reader, out var remote), Is.True);
                AssertValid(remote, count);
                Assert.That(GameWire.CanonicalHash(remote), Is.EqualTo(GameWire.CanonicalHash(value)));
            }
            for (int i = 0; i < count; i++)
            {
                var changed = Copy(value); changed.players[i].ready = false;
                AssertInvalid(changed, count);
                changed = Copy(value); changed.players[i].initialStateReceived = false;
                AssertInvalid(changed, count);
                changed = Copy(value); changed.resources.players[i].playerId = 1000;
                AssertInvalid(changed, count);
            }
        }

        [Test]
        public void MissingDuplicateReorderedExtraAndMismatchedMirrorSeatsAreRejected()
        {
            var original = Playing(5);
            Action<GameSnapshot>[] mutations = {
                value => value.players = null,
                value => value.players = value.players.Take(4).ToArray(),
                value => value.players = value.players.Concat(new[] { value.players[0] }).ToArray(),
                value => value.players[4].clientId = value.players[3].clientId,
                value => value.players[4].playerNumber = 4,
                value => value.players = value.players.Reverse().ToArray(),
                value => value.p1.ready = false,
                value => value.p2.clientId = 57,
                value => value.battle.participants = 2,
                value => value.resources.players = value.resources.players.Take(4).ToArray(),
                value => value.resources.players = value.resources.players.Concat(new[] { value.resources.players[0] }).ToArray()
            };
            foreach (var mutation in mutations) { var value = Copy(original); mutation(value); AssertInvalid(value, 5); }
            var context = Context(5); context.participantIds[4] = context.participantIds[3];
            Assert.That(GameWire.Validate(original, context, null, out _), Is.False);
            context = Context(5); context.participantIds = context.participantIds.Reverse().ToArray();
            Assert.That(GameWire.Validate(original, context, null, out _), Is.False);
        }

        [Test]
        public void P3CannotApproveAConfigLargerThanItsDeclaredPerPlayerInventoryBound()
        {
            var context=Context(5); context.config.storageLimit=21;
            context.configHash=LobbyWire.Fingerprint(JsonUtility.ToJson(context.config));
            var value=Playing(5); value.configHash=context.configHash; value.resources.storageLimit=21;
            Assert.That(GameWire.Validate(value,context,null,out string reason),Is.False);
            Assert.That(reason,Is.EqualTo("INVALID_EXPECTED_CONTEXT"));
        }

        [Test]
        public void ExtraParticipantsCannotEnableP3InAnOlderTrustedScene()
        {
            var value = Playing(2); var context = Context(2); context.participantIds = null;
            Assert.That(GameWire.Validate(value, context, null, out _), Is.False);
            value.players = null;
            Assert.That(GameWire.Validate(value, context, null, out string reason), Is.True, reason);
            Assert.That(GameWire.Validate(value, Context(2), null, out _), Is.False);
        }

        private static IEnumerable<TestCaseData> AllDirections()
        {
            for (int count = 2; count <= 5; count++)
                for (int seat = 0; seat < count; seat++)
                    foreach (bool right in new[] { false, true })
                        yield return new TestCaseData(count, seat, right);
        }

        [TestCaseSource(nameof(AllDirections))]
        public void OneTransferUsesApprovedRingAndArrivesAtTheOppositeEdge(int count, int seat, bool right)
        {
            var before = Playing(count, Seats[seat]);
            var after = Transfer(before, count, right ? 1UL : 0UL, right ? 0UL : 1UL, right);
            AssertValid(after, count, before);
            int target = (seat + (right ? 1 : count - 1)) % count;
            Assert.That(after.attack.orbs[0].owner, Is.EqualTo(Seats[target]));
            Assert.That(after.attack.orbs[0].entrySide, Is.EqualTo((int)(right ? EntrySide.Left : EntrySide.Right)));
            Assert.That(after.attack.orbs[0].id, Is.EqualTo(before.attack.orbs[0].id));
            after.attack.orbs[0].owner = Seats[seat]; Recount(after);
            AssertInvalid(after, count, before);
        }

        [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void SkippedMixedTransfersAndTransferThenLaunchPreserveExactOwner(int count)
        {
            var before = Playing(count, Seats[1]);
            var after = Transfer(before, count, 7, 4, false);
            after.revision += 20; after.attack.revision += 20;
            AssertValid(after, count, before);
            var orb = after.attack.orbs[0];
            orb.kind = (int)OrbKind.Combined; orb.polarity = (int)OrbPolarity.None;
            // The original identity must also be Combined; transfer never changes an orb's kind.
            before.attack.orbs[0].kind = (int)OrbKind.Combined; before.attack.orbs[0].polarity = (int)OrbPolarity.None;
            orb.sequence++; orb.state = (int)OrbAuthorityState.Projectile; orb.pos = new Vector2(.4f, .8f);
            after.attack.projectiles = new[] { Projectile(orb.id, orb.owner) }; Recount(after);
            AssertValid(after, count, before);
            after.attack.orbs[0].owner = Seats[(Array.IndexOf(Seats, orb.owner) + 1) % count];
            after.attack.projectiles[0].owner = after.attack.orbs[0].owner; Recount(after);
            AssertInvalid(after, count, before);
        }

        [Test]
        public void DirectionCountsCannotRegressOrLieAboutLastDirection()
        {
            var initial = Playing(5, Seats[2]);
            var before = Transfer(initial, 5, 2, 1, true);
            Action<OrbWire>[] invalid = {
                orb => orb.rightTransferCount = orb.transferCount + 1,
                orb => orb.rightTransferCount = 1,
                orb => orb.rightTransferCount = orb.transferCount,
                orb => { orb.transferCount = before.attack.orbs[0].transferCount; orb.rightTransferCount++; },
                orb => orb.entrySide = (int)EntrySide.None
            };
            foreach (var mutate in invalid)
            {
                var after = Transfer(before, 5, 0, 1, false); mutate(after.attack.orbs[0]); AssertInvalid(after, 5, before);
            }
            var wrongLast = Transfer(before, 5, 1, 0, true);
            wrongLast.attack.orbs[0].entrySide = (int)EntrySide.Right;
            wrongLast.attack.orbs[0].pos.x = 1 - Inset;
            AssertInvalid(wrongLast, 5, before);
            var wrongLeftRegression = Transfer(before, 5, 1, 0, true);
            wrongLeftRegression.attack.orbs[0].rightTransferCount++;
            AssertInvalid(wrongLeftRegression, 5, before);
        }

        [Test]
        public void FullUnsignedCountersCannotOverflowRingOwnershipCalculation()
        {
            var before = Playing(5, Seats[1]);
            ulong right = ulong.MaxValue - 1, left = 1;
            var after = Transfer(before, 5, right, left, true);
            AssertValid(after, 5, before);
            using (var writer = GameWire.Write(after))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(GameWire.TryRead(reader, out var copy), Is.True);
                Assert.That(copy.attack.orbs[0].rightTransferCount, Is.EqualTo(right));
                Assert.That(copy.attack.orbs[0].transferCount, Is.EqualTo(ulong.MaxValue));
                AssertValid(copy, 5, before);
            }
        }

        [Test]
        public void DirectionCountsAndFrozenSeatOrderArePartOfLogicalProof()
        {
            var value = Transfer(Playing(5, Seats[0]), 5, 2, 2, true);
            string hash = GameWire.CanonicalHash(value);
            var changed = Copy(value); changed.attack.orbs[0].rightTransferCount++;
            Assert.That(GameWire.CanonicalHash(changed), Is.Not.EqualTo(hash));
            changed = Copy(value); changed.players = changed.players.Reverse().ToArray();
            Assert.That(GameWire.CanonicalHash(changed), Is.Not.EqualTo(hash));
            changed = Copy(value); changed.players[4].ready = false;
            Assert.That(GameWire.CanonicalHash(changed), Is.Not.EqualTo(hash));
        }

        [Test]
        public void RetryRequiresEmptyFullRosterAndRejectsPreviousRoundInventory()
        {
            var before = Playing(5, Seats[4]);
            before.attack.hp = before.battle.observedMonsterHp = 0;
            before.attack.totalHits = before.attack.roundHits = 5;
            before.attack.state = "Ended"; before.battle.phase = "Victory"; before.resources.playing = false;
            var after = Ready(5); after.roundId = after.attack.roundId = after.resources.roundId = after.battle.roundId = 2;
            after.hostNow = before.hostNow + 1; after.serverTime = before.serverTime + 1;
            after.attack.totalHits = 5; after.attack.resets = 1;
            AssertValid(after, 5, before);
            after.attack.orbs = before.attack.orbs; Recount(after); AssertInvalid(after, 5, before);
            after = Ready(4); after.roundId = after.attack.roundId = after.resources.roundId = after.battle.roundId = 2;
            AssertInvalid(after, 5, before);
        }

        [Test]
        public void FiveFullInventoriesPlusOneHundredFlyingObjectsFitExplicitBound()
        {
            var value = Playing(5); var orbs = new List<OrbWire>(); var flying = new List<ProjectileWire>();
            for (int player = 0; player < 5; player++)
                for (int index = 0; index < 40; index++)
                {
                    // 128 characters with three UTF-8 bytes per non-ASCII character tests a larger
                    // legal identifier than normal GUIDs. All active identities and counters are exact.
                    string id = (player * 40 + index).ToString("D3") + new string('球', 125);
                    bool projectile = index >= 20;
                    var orb = new OrbWire { id=id, owner=Seats[player], kind=(int)(projectile?OrbKind.Combined:OrbKind.Raw),
                        polarity=(int)(projectile?OrbPolarity.None:OrbPolarity.Yin), state=(int)(projectile?OrbAuthorityState.Projectile:OrbAuthorityState.Idle),
                        pos=new Vector2(.055f,.56789123f), sequence=ulong.MaxValue, transferCount=ulong.MaxValue,
                        rightTransferCount=ulong.MaxValue, lastTransferSequence=ulong.MaxValue, entrySide=(int)EntrySide.Left };
                    orbs.Add(orb);
                    if (projectile) flying.Add(Projectile(id, Seats[player]));
                }
            value.attack.orbs=orbs.ToArray(); value.attack.projectiles=flying.ToArray(); Recount(value);
            AssertValid(value,5);
            using (var writer=GameWire.Write(value))
            using (var reader=new FastBufferReader(writer,Allocator.Temp))
            {
                Assert.That(writer.Length,Is.GreaterThan(GameWire.MaximumBytes));
                Assert.That(writer.Length,Is.LessThanOrEqualTo(GameWire.MaximumMultipartyBytes));
                Assert.That(GameWire.TryRead(reader,out var remote),Is.True); AssertValid(remote,5);
                Assert.That(GameWire.CanonicalHash(remote),Is.EqualTo(GameWire.CanonicalHash(value)));
                Debug.Log($"C6_P3_GAME_WIRE_CAPACITY stored=100 flying=100 active=200 identifierCharacters=128 actualBytes={writer.Length} maximumBytes={GameWire.MaximumMultipartyBytes}");
            }
            value.attack.orbs=value.attack.orbs.Concat(new[]{new OrbWire{id="overflow",owner=0,kind=(int)OrbKind.Raw,polarity=(int)OrbPolarity.Yin,pos=Vector2.one*.5f}}).ToArray();
            Recount(value); AssertInvalid(value,5);
            value=Playing(5); value.nonce=new string('x',GameWire.MaximumMultipartyBytes);
            Assert.Throws<ArgumentException>(()=> { using(var writer=GameWire.Write(value)){} });
        }

        [Test]
        public void OnePlayerCannotBorrowAnotherPlayersStorageOrFlightAllowance()
        {
            var value=Playing(5); value.attack.orbs=Enumerable.Range(0,21).Select(index=>new OrbWire{id="raw"+index,
                owner=Seats[4],kind=(int)OrbKind.Raw,polarity=(int)OrbPolarity.Yin,pos=Vector2.one*.5f}).ToArray();
            Recount(value); AssertInvalid(value,5);
            value=Playing(5); value.attack.projectiles=Enumerable.Range(0,21).Select(index=>Projectile("fly"+index,Seats[4])).ToArray();
            value.attack.orbs=value.attack.projectiles.Select(projectile=>new OrbWire{id=projectile.id,owner=projectile.owner,
                kind=(int)OrbKind.Combined,polarity=(int)OrbPolarity.None,state=(int)OrbAuthorityState.Projectile,pos=Vector2.one*.5f}).ToArray();
            Recount(value); AssertInvalid(value,5);
            // A reserved launch consumes the same finite flight allowance before its body spawns.
            value.attack.projectiles=value.attack.projectiles.Take(20).ToArray();
            value.attack.orbs[20].state=(int)OrbAuthorityState.Launching;
            Recount(value); AssertInvalid(value,5);
        }

        private static GameSnapshotContext Context(int count)
        {
            var asset=ScriptableObject.CreateInstance<ScreenLayoutConfig>(); LobbyHostConfig config;
            try { config=LobbyHostConfig.Capture(asset); } finally { UnityEngine.Object.DestroyImmediate(asset); }
            return new GameSnapshotContext {roomId=Room,sessionId=Session,config=config,configHash=LobbyWire.Fingerprint(JsonUtility.ToJson(config)),
                seed=123,p1=Seats[0],p2=Seats[1],participantIds=Seats.Take(count).ToArray(),nonce=Nonce,allowTransfers=true,transferEdgeInset=Inset};
        }
        private static GameSnapshot Ready(int count)
        {
            var c=Context(count); var players=Seats.Take(count).Select((id,index)=>new LobbyPlayer{clientId=id,playerNumber=index+1,connected=true,ready=true,initialStateReceived=true}).ToArray();
            return new GameSnapshot{roomId=Room,sessionId=Session,nonce=Nonce,roundId=1,revision=1,seed=c.seed,configHash=c.configHash,
                hostNow=100,serverTime=200,p1=players[0],p2=players[1],players=players,
                attack=new AttackSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,hp=100,maxHp=100,state="Playing"},
                resources=new ResourceSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,seed=c.seed,maximum=100,generateCost=20,
                    regenerationRate=20d/3d,hitRecovery=5,storageLimit=20,players=Seats.Take(count).Select(id=>new ResourcePlayerWire{playerId=id,stamina=100}).ToArray()},
                battle=new BattleSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,phase="Ready",remaining=180,teamHp=180,duration=180,
                    teamHpDecayPerSecond=1,observedMonsterHp=100,monsterMaxHp=100,participants=count}};
        }
        private static GameSnapshot Playing(int count,ulong? owner=null)
        {
            var value=Ready(count); value.initialStateConfirmed=true; value.resources.playing=true;
            value.battle.phase="Playing"; value.battle.startedAt=100; value.battle.deadline=280;
            if(owner.HasValue)value.attack.orbs=new[]{new OrbWire{id="orb",owner=owner.Value,kind=(int)OrbKind.Raw,polarity=(int)OrbPolarity.Yin,pos=Vector2.one*.5f}};
            Recount(value); return value;
        }
        private static ProjectileWire Projectile(string id,ulong owner)=>new ProjectileWire{id=id,owner=owner,position=new Vector3(1.2345678f,2.3456789f,-3.456789f),
            radius=.165f,ballistic=true,velocity=new Vector3(6.1234567f,3.456789f,10.1234567f),gravity=new Vector3(0,-9.81f,0),elapsed=1.2345678f,lifetime=4};
        private static GameSnapshot Transfer(GameSnapshot before,int count,ulong right,ulong left,bool lastRight)
        {
            var after=Next(before); var orb=after.attack.orbs[0];
            int index=Array.IndexOf(Seats,orb.owner); int next=(index+(int)(right%(ulong)count)-(int)(left%(ulong)count)+count)%count;
            orb.owner=Seats[next]; orb.transferCount+=right+left; orb.rightTransferCount+=right;
            orb.sequence+=right+left; orb.lastTransferSequence=orb.sequence; orb.entrySide=(int)(lastRight?EntrySide.Left:EntrySide.Right);
            orb.pos=new Vector2(lastRight?Inset:1-Inset,.75f); Recount(after); return after;
        }
        private static GameSnapshot Copy(GameSnapshot value)=>JsonUtility.FromJson<GameSnapshot>(JsonUtility.ToJson(value));
        private static GameSnapshot Next(GameSnapshot value){var copy=Copy(value);copy.revision++;copy.attack.revision++;copy.resources.revision++;copy.battle.revision++;copy.hostNow++;copy.serverTime++;return copy;}
        private static void Recount(GameSnapshot value){foreach(var player in value.resources.players)player.storedOrbs=value.attack.orbs.Count(orb=>orb.owner==player.playerId&&(orb.state==(int)OrbAuthorityState.Idle||orb.state==(int)OrbAuthorityState.Launching));}
        private static void AssertValid(GameSnapshot value,int count,GameSnapshot before=null){Assert.That(GameWire.Validate(value,Context(count),before,out string reason),Is.True,reason);}
        private static void AssertInvalid(GameSnapshot value,int count,GameSnapshot before=null){Assert.That(GameWire.Validate(value,Context(count),before,out string reason),Is.False,reason);}
    }
}
