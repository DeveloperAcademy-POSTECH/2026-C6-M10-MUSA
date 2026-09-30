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
    public sealed class ElementSelectionGameWireTests
    {
        private static readonly ulong[] Ids = { 0, 41, 57, 88, 99 };
        private const string Room="11111111111111111111111111111111", Session="22222222222222222222222222222222", Nonce="33333333333333333333333333333333";
        private static GameSnapshotContext Context(int count)
        {
            var asset=ScriptableObject.CreateInstance<ScreenLayoutConfig>(); LobbyHostConfig config;
            try { config=LobbyHostConfig.Capture(asset); } finally { UnityEngine.Object.DestroyImmediate(asset); }
            return new GameSnapshotContext {roomId=Room,sessionId=Session,config=config,configHash=LobbyWire.Fingerprint(JsonUtility.ToJson(config)),
                seed=123,p1=0,p2=41,participantIds=Ids.Take(count).ToArray(),nonce=Nonce,allowTransfers=true,transferEdgeInset=.055f,
                elementSelection=true,selectedElements=Enumerable.Range(1,count).Select(x=>(OrbElement)x).ToArray(),initialSeatOrder=Ids.Take(count).Reverse().ToArray()};
        }
        private static GameSnapshot Ready(int count)
        {
            var c=Context(count);int hp=c.config.MonsterHpFor(count);
            var players=Ids.Take(count).Select((id,i)=>new LobbyPlayer{clientId=id,playerNumber=i+1,selectedElement=c.selectedElements[i],connected=true,ready=true,initialStateReceived=true}).ToArray();
            return new GameSnapshot {roomId=Room,sessionId=Session,nonce=Nonce,roundId=1,revision=1,seed=c.seed,configHash=c.configHash,
                hostNow=100,serverTime=200,p1=players[0],p2=players[1],players=players,elementSelection=true,roundSeatOrder=(ulong[])c.initialSeatOrder.Clone(),
                attack=new AttackSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,hp=hp,maxHp=hp,state="Playing"},
                resources=new ResourceSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,seed=c.seed,maximum=100,generateCost=20,
                    regenerationRate=20d/3d,hitRecovery=5,storageLimit=20,players=Ids.Take(count).Select(id=>new ResourcePlayerWire{playerId=id,stamina=100}).ToArray()},
                battle=new BattleSnapshot{nonce=Nonce,sessionId=Session,roundId=1,revision=1,phase="Ready",remaining=180,teamHp=180,duration=180,
                    teamHpDecayPerSecond=1,observedMonsterHp=hp,monsterMaxHp=hp,participants=count}};
        }
        private static GameSnapshot Playing(int count)
        {var s=Ready(count);s.initialStateConfirmed=true;s.resources.playing=true;s.battle.phase="Playing";s.battle.startedAt=100;s.battle.deadline=280;return s;}
        private static GameSnapshot Next(GameSnapshot s)
        {var n=JsonUtility.FromJson<GameSnapshot>(JsonUtility.ToJson(s));n.revision++;n.attack.revision++;n.resources.revision++;n.battle.revision++;n.hostNow++;n.serverTime++;return n;}
        private static void Recount(GameSnapshot s)
        {foreach(var p in s.resources.players)p.storedOrbs=s.attack.orbs.Count(o=>o.owner==p.playerId&&(o.state==(int)OrbAuthorityState.Idle||o.state==(int)OrbAuthorityState.Launching));}
        private static void Valid(GameSnapshot s,int count,GameSnapshot prior=null)
        {Assert.That(GameWire.Validate(s,Context(count),prior,out var why),Is.True,why);}
        private static void Invalid(GameSnapshot s,int count,GameSnapshot prior=null)
        {Assert.That(GameWire.Validate(s,Context(count),prior,out var why),Is.False,why);}

        [TestCase(2)][TestCase(3)][TestCase(5)]
        public void HostCanOccupyAnySeatAndNewContractRoundTrips(int count)
        {
            var s=Ready(count);Valid(s,count);Assert.That(s.roundSeatOrder[0],Is.Not.EqualTo(0));
            using(var writer=GameWire.Write(s))using(var reader=new FastBufferReader(writer,Allocator.Temp))
            {Assert.That(GameWire.TryRead(reader,out var parsed),Is.True);Valid(parsed,count);Assert.That(GameWire.CanonicalHash(parsed),Is.EqualTo(GameWire.CanonicalHash(s)));}
        }
        [TestCase("duplicate")][TestCase("missing")][TestCase("outsider")][TestCase("initial")]
        public void InvalidSeatContractCannotBeApplied(string kind)
        {
            var s=Ready(3);
            if(kind=="duplicate")s.roundSeatOrder[0]=s.roundSeatOrder[1];
            if(kind=="missing")s.roundSeatOrder=s.roundSeatOrder.Take(2).ToArray();
            if(kind=="outsider")s.roundSeatOrder[0]=400;
            if(kind=="initial")Array.Reverse(s.roundSeatOrder);
            Invalid(s,3);
        }
        [Test]
        public void SameRoundSeatsAndSelectionsAreImmutableAndIncludedInHash()
        {
            var prior=Playing(3);var after=Next(prior);Array.Reverse(after.roundSeatOrder);
            Assert.That(GameWire.CanonicalHash(after),Is.Not.EqualTo(GameWire.CanonicalHash(Next(prior))));Invalid(after,3,prior);
            after=Next(prior);after.players[1].selectedElement=OrbElement.Earth;Invalid(after,3,prior);
        }
        [Test]
        public void NewReadyRoundMayReshuffleButOldAckHashCannotMatch()
        {
            var before=Ready(3);var after=Next(before);after.roundId=after.attack.roundId=after.resources.roundId=after.battle.roundId=2;
            Array.Reverse(after.roundSeatOrder);Valid(after,3,before);
            Assert.That(GameWire.CanonicalHash(after),Is.Not.EqualTo(GameWire.CanonicalHash(before)));
        }
        [Test]
        public void ReceivedRawMayDifferFromOwnerChoiceButItsElementCannotChange()
        {
            var before=Playing(3);
            before.attack.orbs=new[]{new OrbWire{id="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",owner=0,kind=(int)OrbKind.Raw,polarity=(int)OrbPolarity.Yin,rawElement=OrbElement.Fire,pos=Vector2.one*.5f}};Recount(before);Valid(before,3);
            var after=Next(before);var orb=after.attack.orbs[0];orb.owner=57;orb.transferCount=orb.rightTransferCount=orb.lastTransferSequence=orb.sequence=1;
            orb.entrySide=(int)EntrySide.Left;orb.pos=new Vector2(.055f,.5f);Recount(after);Valid(after,3,before);
            var hash=GameWire.CanonicalHash(after);orb.rawElement=OrbElement.Wood;Invalid(after,3,before);
            Assert.That(GameWire.CanonicalHash(after),Is.Not.EqualTo(hash));
        }
        [Test]
        public void NoneRawAndForeignElementProjectileAreRejected()
        {
            var s=Playing(3);s.attack.orbs=new[]{new OrbWire{id="raw",owner=0,kind=(int)OrbKind.Raw,polarity=(int)OrbPolarity.Yin,pos=Vector2.one*.5f}};Recount(s);Invalid(s,3);
            s.attack.orbs=new[]{new OrbWire{id=OrbElements.NewCombinedId(OrbElement.Water),owner=0,kind=(int)OrbKind.Combined,polarity=(int)OrbPolarity.None,
                state=(int)OrbAuthorityState.Launching,pos=Vector2.one*.5f,sequence=1}};Recount(s);Invalid(s,3);
        }
        [Test]
        public void TrustedChoiceSetMustBeCompleteAndUnique()
        {var s=Ready(3);var c=Context(3);c.selectedElements[1]=c.selectedElements[0];Assert.That(GameWire.Validate(s,c,null,out _),Is.False);}
        [Test]
        public void DeterministicShuffleIncludesHostWithoutChangingAdmissionRoster()
        {
            var roster=(ulong[])Ids.Clone();bool hostMoved=false;
            for(int seed=0;seed<20;seed++){var seats=RoundSeatLayout.Shuffle(roster,new System.Random(seed));Assert.That(RoundSeatLayout.Valid(seats,roster),Is.True);hostMoved|=seats[0]!=0;}
            Assert.That(hostMoved,Is.True);Assert.That(roster,Is.EqualTo(Ids));
        }
    }
}
