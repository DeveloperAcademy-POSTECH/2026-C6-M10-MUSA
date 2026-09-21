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
    public sealed class P4GameWireTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private const float Inset = .055f;
        private static readonly ulong[] Seats = { 0, 41, 57, 88, 99 };

        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ContinuousModeRequiresFrozenRosterAndRoundTripsWithDedicatedHeader(int count)
        {
            var value=Ready(count); AssertValid(value,count);
            value=Playing(count,0); var after=Transfer(value,count,1,0,true); AssertValid(after,count,value);
            using(var writer=GameWire.Write(after))
            using(var header=new FastBufferReader(writer,Allocator.Temp))
            using(var reader=new FastBufferReader(writer,Allocator.Temp))
            {
                header.ReadValueSafe(out byte version); Assert.That(version,Is.EqualTo(24));
                Assert.That(GameWire.TryRead(reader,out var parsed),Is.True); AssertValid(parsed,count,value);
                Assert.That(GameWire.CanonicalHash(parsed),Is.EqualTo(GameWire.CanonicalHash(after)));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void WireHeaderCannotClaimTheOtherTransferMode(bool p4)
        {
            var value=Playing(3); value.continuousTransfers=p4;
            var bytes=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(value));
            using(var writer=new FastBufferWriter(bytes.Length+5,Allocator.Temp))
            {
                writer.WriteValueSafe((byte)(p4?23:24)); writer.WriteValueSafe(bytes.Length); writer.WriteBytesSafe(bytes);
                using(var reader=new FastBufferReader(writer,Allocator.Temp)) Assert.That(GameWire.TryRead(reader,out _),Is.False);
            }
        }
        [Test]
        public void PacketCannotEnableModeOrMaximumSpeedInUnapprovedScene()
        {
            var value=Playing(3); var expected=Context(3); expected.continuousTransfers=false;
            Assert.That(GameWire.Validate(value,expected,null,out _),Is.False);
            value.continuousTransfers=false; AssertInvalid(value,3);
            value.continuousTransfers=true; expected=Context(3); expected.participantIds=null;
            Assert.That(GameWire.Validate(value,expected,null,out _),Is.False);
            expected=Context(3); expected.transferMaximumSpeed=0;
            Assert.That(GameWire.Validate(value,expected,null,out _),Is.False);
            expected=Context(3); expected.transferMaximumSpeed=float.PositiveInfinity;
            Assert.That(GameWire.Validate(value,expected,null,out _),Is.False);
            expected=Context(3); expected.allowTransfers=false;
            Assert.That(GameWire.Validate(value,expected,null,out _),Is.False);
        }
        [TestCase("missing")] [TestCase("nan-x")] [TestCase("infinite-y")]
        [TestCase("nan-time")] [TestCase("negative-time")] [TestCase("future-time")]
        [TestCase("speed")] [TestCase("direction")]
        public void InvalidOrMissingContinuousMotionCannotCommit(string kind)
        {
            var value=Transfer(Playing(3,0),3,1,0,true); var orb=value.attack.orbs[0];
            if(kind=="missing") {orb.hasTransferMotion=false;orb.transferVelocityX=orb.transferVelocityY=0;orb.transferServerTime=0;}
            if(kind=="nan-x")orb.transferVelocityX=float.NaN;
            if(kind=="infinite-y")orb.transferVelocityY=float.PositiveInfinity;
            if(kind=="nan-time")orb.transferServerTime=double.NaN;
            if(kind=="negative-time")orb.transferServerTime=-1;
            if(kind=="future-time")orb.transferServerTime=value.serverTime+.251;
            if(kind=="speed") {orb.transferVelocityX=23;orb.transferVelocityY=23;}
            if(kind=="direction")orb.transferVelocityX=-1;
            AssertInvalid(value,3);
        }
        [TestCase("x")] [TestCase("y")] [TestCase("time")]
        public void UnflaggedMotionNumbersAreRejected(string field)
        {
            var value=Playing(3,0); var orb=value.attack.orbs[0];
            if(field=="x")orb.transferVelocityX=1;
            if(field=="y")orb.transferVelocityY=1;
            if(field=="time")orb.transferServerTime=1;
            AssertInvalid(value,3);
        }
        [Test]
        public void InitialOrbsCannotFabricateMotionAndOlderModeCannotCarryIt()
        {
            var value=Playing(3,0); var orb=value.attack.orbs[0]; orb.hasTransferMotion=true;
            orb.transferVelocityX=1; orb.transferServerTime=199; AssertInvalid(value,3);
            value=Transfer(Playing(3,0),3,1,0,true); value.continuousTransfers=false;
            var expected=Context(3); expected.continuousTransfers=false;
            Assert.That(GameWire.Validate(value,expected,null,out _),Is.False);
        }
        [TestCase(true)] [TestCase(false)]
        public void DelayedArrivalMayStopWithoutChangingItsApprovedEntry(bool right)
        {
            var value=Transfer(Playing(3,0),3,right?1UL:0UL,right?0UL:1UL,right);
            value.attack.orbs[0].transferVelocityX=0; value.attack.orbs[0].transferVelocityY=0;
            AssertValid(value,3);
        }
        [Test]
        public void HistoricalMotionRemainsValidAfterLongRestAndARealLaunch()
        {
            var before=Transfer(Playing(3,0),3,1,0,true); before.attack.orbs[0].kind=(int)OrbKind.Combined;
            before.attack.orbs[0].polarity=(int)OrbPolarity.None;
            var after=Next(before); after.serverTime+=1000; after.hostNow+=1000;
            AssertValid(after,3,before);
            var orb=after.attack.orbs[0]; orb.sequence++; orb.state=(int)OrbAuthorityState.Projectile;orb.pos=new Vector2(.4f,.8f);
            after.attack.projectiles=new[]{Projectile(orb.id,orb.owner)};Recount(after);AssertValid(after,3,before);
        }
        [TestCase("x")] [TestCase("y")] [TestCase("time")]
        public void MotionCannotChangeWithoutAnotherTransferAndEveryFieldChangesHash(string field)
        {
            var before=Transfer(Playing(3,0),3,1,0,true);var after=Next(before);var orb=after.attack.orbs[0];
            string original=GameWire.CanonicalHash(after);
            if(field=="x")orb.transferVelocityX+=.1f;
            if(field=="y")orb.transferVelocityY+=.1f;
            if(field=="time")orb.transferServerTime+=.1d;
            Assert.That(GameWire.CanonicalHash(after),Is.Not.EqualTo(original)); AssertInvalid(after,3,before);
        }
        [Test]
        public void AnotherTransferMayChangeVelocityButCannotRegressApprovalTime()
        {
            var before=Transfer(Playing(5,0),5,1,0,true);var after=Transfer(before,5,0,1,false);
            after.attack.orbs[0].transferVelocityX=-.75f; AssertValid(after,5,before);
            after.attack.orbs[0].transferServerTime=before.attack.orbs[0].transferServerTime-.1d;
            AssertInvalid(after,5,before);
        }
        [Test]
        public void TwoHundredMotionReceiptsWithMaximumIdsRemainWithinDeclaredEnvelope()
        {
            var value=Playing(5); var orbs=new List<OrbWire>(); var projectiles=new List<ProjectileWire>();
            for(int player=0;player<5;player++) for(int index=0;index<40;index++)
            {
                string id=(player*40+index).ToString("D3")+new string('球',125); bool flying=index>=20;
                orbs.Add(new OrbWire{id=id,owner=Seats[player],kind=(int)(flying?OrbKind.Combined:OrbKind.Raw),
                    polarity=(int)(flying?OrbPolarity.None:OrbPolarity.Yin),state=(int)(flying?OrbAuthorityState.Projectile:OrbAuthorityState.Idle),
                    pos=new Vector2(Inset,.56789123f),sequence=ulong.MaxValue,transferCount=ulong.MaxValue,rightTransferCount=ulong.MaxValue,
                    lastTransferSequence=ulong.MaxValue,entrySide=(int)EntrySide.Left,hasTransferMotion=true,
                    transferVelocityX=12.345678f,transferVelocityY=-13.456789f,transferServerTime=199.1234567890123d});
                if(flying)projectiles.Add(Projectile(id,Seats[player]));
            }
            value.attack.orbs=orbs.ToArray();value.attack.projectiles=projectiles.ToArray();Recount(value);AssertValid(value,5);
            using(var writer=GameWire.Write(value)) using(var reader=new FastBufferReader(writer,Allocator.Temp))
            {
                Assert.That(writer.Length,Is.LessThanOrEqualTo(GameWire.MaximumMultipartyBytes));
                Assert.That(GameWire.TryRead(reader,out var copy),Is.True);AssertValid(copy,5);
                Assert.That(GameWire.CanonicalHash(copy),Is.EqualTo(GameWire.CanonicalHash(value)));
                Debug.Log($"C6_P4_GAME_WIRE_CAPACITY stored=100 flying=100 motion=200 active=200 identifierCharacters=128 actualBytes={writer.Length} maximumBytes={GameWire.MaximumMultipartyBytes}");
            }
        }
        [Test]
        public void MonsterAttackTargetsAFrozenParticipantWithTheHostWarningAndPenalty()
        {
            AssertValid(Attacking(3,Seats[2]),3);
            AssertInvalid(Attacking(3,12345),3);
            var value=Attacking(3,Seats[1]); value.battle.attackWarningEndsAt+=.5; AssertInvalid(value,3);
            value=Attacking(3,Seats[1]); value.battle.attackWarningStartsAt-=1; value.battle.attackWarningEndsAt-=1; AssertInvalid(value,3);
            value=Playing(3); value.battle.penaltySeconds=20; AssertInvalid(value,3);
            value=Resolved(Attacking(3,Seats[1]),MonsterAttackResult.Hit); value.battle.penaltySeconds=20; AssertValid(value,3);
            value.battle.penaltySeconds=40; AssertInvalid(value,3);
        }
        [Test]
        public void PublishedAttacksNeverRewindOrChangeAndChangeTheHash()
        {
            var before=Attacking(3,Seats[1]);
            AssertValid(Resolved(Next(before),MonsterAttackResult.Defended),3,before);
            var after=Next(before); after.battle.attackTarget=Seats[2]; AssertInvalid(after,3,before);
            Assert.That(GameWire.CanonicalHash(after),Is.Not.EqualTo(GameWire.CanonicalHash(Next(before))));
            after=Next(before); after.battle.attackSequence=0; after.battle.attackTarget=0; after.battle.attackActive=false;
            after.battle.attackWarningStartsAt=after.battle.attackWarningEndsAt=0; AssertInvalid(after,3,before);
            var resolved=Resolved(Next(before),MonsterAttackResult.Defended);
            after=Next(resolved); after.battle.attackResult=(int)MonsterAttackResult.Hit; AssertInvalid(after,3,resolved);
        }

        [Test]
        public void RetryAfterAttacksStartsAnEmptyRoundAndCannotCarryTheOldAttack()
        {
            var before=Resolved(Attacking(3,Seats[1]),MonsterAttackResult.Hit); before.battle.penaltySeconds=20;
            AssertValid(before,3);
            var retry=Ready(3); retry.revision=before.revision+1; retry.hostNow=before.hostNow+1; retry.serverTime=before.serverTime+1;
            retry.roundId=retry.attack.roundId=retry.resources.roundId=retry.battle.roundId=2;
            AssertValid(retry,3,before);
            var carried=Ready(3); carried.revision=retry.revision; carried.hostNow=retry.hostNow; carried.serverTime=retry.serverTime;
            carried.roundId=carried.attack.roundId=carried.resources.roundId=carried.battle.roundId=2;
            carried.battle.attackSequence=1; carried.battle.attackTarget=Seats[1];
            carried.battle.attackWarningStartsAt=120; carried.battle.attackWarningEndsAt=123;
            AssertInvalid(carried,3,before);
            carried=Ready(3); carried.revision=retry.revision; carried.hostNow=retry.hostNow; carried.serverTime=retry.serverTime;
            carried.roundId=carried.attack.roundId=carried.resources.roundId=carried.battle.roundId=2;
            carried.battle.penaltySeconds=20; AssertInvalid(carried,3,before);
        }
        private static GameSnapshot Attacking(int count,ulong target)
        {
            var value=Playing(count); var b=value.battle;
            b.attackSequence=1; b.attackTarget=target; b.attackActive=true; b.attackWarningStartsAt=120; b.attackWarningEndsAt=123;
            b.remaining=b.teamHp=150; return value;
        }
        private static GameSnapshot Resolved(GameSnapshot value,MonsterAttackResult result)
        {
            var b=value.battle; b.attackActive=false; b.attackResolvedSequence=b.attackSequence; b.attackResult=(int)result; return value;
        }
        private static GameSnapshotContext Context(int count)
        {
            var asset=ScriptableObject.CreateInstance<ScreenLayoutConfig>(); LobbyHostConfig config;
            try { config=LobbyHostConfig.Capture(asset); } finally { UnityEngine.Object.DestroyImmediate(asset); }
            return new GameSnapshotContext {roomId=Room,sessionId=Session,config=config,configHash=LobbyWire.Fingerprint(JsonUtility.ToJson(config)),
                seed=123,p1=Seats[0],p2=Seats[1],participantIds=Seats.Take(count).ToArray(),nonce=Nonce,allowTransfers=true,continuousTransfers=true,transferMaximumSpeed=32f,transferEdgeInset=Inset};
        }
        private static GameSnapshot Ready(int count)
        {
            var c=Context(count); int hp=c.config.MonsterHpFor(count); // #29 per-player HP table
            var players=Seats.Take(count).Select((id,index)=>new LobbyPlayer{clientId=id,playerNumber=index+1,connected=true,ready=true,initialStateReceived=true}).ToArray();
            return new GameSnapshot{roomId=Room,sessionId=Session,nonce=Nonce,roundId=1,revision=1,seed=c.seed,configHash=c.configHash,
                hostNow=100,serverTime=200,continuousTransfers=true,p1=players[0],p2=players[1],players=players,
                attack=new AttackSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,hp=hp,maxHp=hp,state="Playing"},
                resources=new ResourceSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,seed=c.seed,maximum=100,generateCost=20,
                    regenerationRate=20d/3d,hitRecovery=5,storageLimit=20,players=Seats.Take(count).Select(id=>new ResourcePlayerWire{playerId=id,stamina=100}).ToArray()},
                battle=new BattleSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,phase="Ready",remaining=180,teamHp=180,duration=180,
                    teamHpDecayPerSecond=1,observedMonsterHp=hp,monsterMaxHp=hp,participants=count}};
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
            orb.hasTransferMotion=true; orb.transferVelocityX=lastRight?1.25f:-1.25f; orb.transferVelocityY=.25f; orb.transferServerTime=after.serverTime;
            orb.pos=new Vector2(lastRight?Inset:1-Inset,.75f); Recount(after); return after;
        }
        private static GameSnapshot Copy(GameSnapshot value)=>JsonUtility.FromJson<GameSnapshot>(JsonUtility.ToJson(value));
        private static GameSnapshot Next(GameSnapshot value){var copy=Copy(value);copy.revision++;copy.attack.revision++;copy.resources.revision++;copy.battle.revision++;copy.hostNow++;copy.serverTime++;return copy;}
        private static void Recount(GameSnapshot value){foreach(var player in value.resources.players)player.storedOrbs=value.attack.orbs.Count(orb=>orb.owner==player.playerId&&(orb.state==(int)OrbAuthorityState.Idle||orb.state==(int)OrbAuthorityState.Launching));}
        private static void AssertValid(GameSnapshot value,int count,GameSnapshot before=null){Assert.That(GameWire.Validate(value,Context(count),before,out string reason),Is.True,reason);}
        private static void AssertInvalid(GameSnapshot value,int count,GameSnapshot before=null){Assert.That(GameWire.Validate(value,Context(count),before,out string reason),Is.False,reason);}
    }
}
