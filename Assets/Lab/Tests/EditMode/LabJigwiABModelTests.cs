using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
namespace C6Lab.Tests
{
    /// <summary>Host model fixtures for screen A/B, not device/LAN/playability verification.</summary>
    public sealed class LabJigwiABModelTests
    {
        LabConfig config; int serial;
        static readonly ulong[] Players = { 10ul, 20ul };
        [SetUp] public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>(); var v = config.CaptureDeveloperValues();
            v[3] = 1000; v[4] = 100; v[5] = 1; v[6] = 0; v[7] = 1; v[9] = 100; v[10] = 120; v[74] = 2;
            v[39] = v[40] = 50; for(int i=41;i<=44;i++)v[i]=0;
            v[155] = v[184] = v[166] = v[207] = v[208] = 0; Apply(v);
        }
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);
        void Apply(float[] v) => Assert.That(config.TryApplyDeveloperValues(v,out var e),Is.True,e);
        string Request() => "ab-request-" + ++serial;
        LabModel Room(LabStageYokai stage=LabStageYokai.Jigwi)
        { var m=new LabModel();m.Begin("ab-test",Players,0,config,stage);return m; }
        Dictionary<string,LabOrbState> Orbs(LabModel m)=>(Dictionary<string,LabOrbState>)typeof(LabModel)
            .GetField("orbs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(m);
        LabOrbState Add(LabModel m,ulong owner,double at,LabOrbKind kind=LabOrbKind.Combined,LabOrbElement element=LabOrbElement.Fire)
        {
            var o=new LabOrbState{id="fixture-"+ ++serial,owner=owner,kind=kind,element=kind==LabOrbKind.Combined?element:LabOrbElement.None,
                x=.5f,y=.5f,createdAt=at};Orbs(m).Add(o.id,o);return o;
        }
        LabJigwiABRound Current(LabModel m,double at)=>m.Snapshot(at).jigwiAB.Active(at);
        void Launch(LabModel m,LabOrbState o,double at)=>Assert.That(m.TryLaunch(o.owner,o.id,Request(),at,out _,out var e),Is.True,e);
        void BossHit(LabModel m,LabOrbState o,double at,Vector3 dir)
        { Launch(m,o,at);Assert.That(m.ConfirmHit(o.id,at,dir,out _,out _,out _),Is.True); }
        /// <summary>#509: a real boss hit (aimed at the back so it never overlaps the paint checks aimed forward).</summary>
        void Hit(LabModel m,ulong owner,double at,LabOrbElement element=LabOrbElement.Water)
        { var o=Add(m,owner,at,element:element);Launch(m,o,at);Assert.That(m.ConfirmHit(o.id,at,Vector3.back,out _,out _,out _),Is.True); }
        /// <summary>
        /// #509: one synchronized clear = every picked seat's Water hits the boss inside one second. Uses this B's
        /// supplied Water, which counts but never paints, so paint-sensitive fixtures are not disturbed.
        /// </summary>
        void Sync(LabModel m,LabJigwiABRound g,double at)
        {
            foreach(var p in g.players)
            {
                var o=Add(m,p.playerId,at,element:LabOrbElement.Water);o.jigwiSupply=true;o.jigwiABSupplyEventId=g.id;Launch(m,o,at);
                Assert.That(m.ConfirmHit(o.id,at,Vector3.back,out _,out _,out _),Is.False,"Supplied Water is consumed without paint.");
            }
        }
        /// <summary>#509: three synchronized clears finish B.</summary>
        void Clear(LabModel m,LabJigwiABRound g,double at)
        {for(int k=0;k<LabJigwiABTimeline.GroupSyncRequired;k++)Sync(m,g,at);}
        static readonly ulong[] Three = { 10ul, 20ul, 30ul };
        static int Field(string label)=>LabConfig.DeveloperFields.Select((f,i)=>(f,i)).First(p=>p.f.Label==label).i;
        /// <summary>
        /// #509: B1 fails at 40, the same second as the common gimmick. Fixtures that check B's own wipe make it a
        /// swipe block (no orb) instead of a gourd bomb; the two chances must still add up to 100.
        /// </summary>
        static void NoCommonGimmick(float[] v){v[Field("Gimmick Chance Gourd Bomb (%)")]=0;v[Field("Gimmick Chance Swipe Block (%)")]=100;}
        LabModel Room3(){var m=new LabModel();m.Begin("ab-three",Three,0,config,LabStageYokai.Jigwi);return m;}
        static ulong Helper(LabJigwiABRound g,IEnumerable<ulong> seats)=>seats.First(id=>g.players.All(p=>p.playerId!=id));

        [TestCase(LabStageYokai.None,false)] [TestCase(LabStageYokai.Gumiho,false)]
        [TestCase(LabStageYokai.Yaguang,false)] [TestCase(LabStageYokai.Jigwi,true)]
        public void ScopeAndRetiredSurfaceAreIndependent(LabStageYokai stage,bool expected)
        {
            var m=Room(stage);var s=m.Snapshot(11);Assert.That(s.jigwiAB.enabled,Is.EqualTo(expected));
            Assert.That(m.JigwiABEnabled,Is.EqualTo(expected));Assert.That(s.jigwiStage.enabled,Is.False);
            Assert.That(m.JigwiStagePaintBlocked(Players[0],Vector3.forward),Is.False);
        }
        [Test] public void AWarningHasNoBurnUntilActiveAndThenBurnsEveryOwnersOrbs()
        {
            // #479: A ignites every seat at once; there is no target and no recovery boost.
            var m=Room();var a=Current(m,4);var mine=Add(m,Players[0],4);var theirs=Add(m,Players[1],4);
            Assert.That(a.Warning(4),Is.True);
            Assert.That(m.Snapshot(4.99).orbs.Single(x=>x.id==mine.id).jigwiABBurnAt,Is.EqualTo(-1));
            Assert.That(m.Snapshot(5).orbs.Single(x=>x.id==mine.id).jigwiABBurnAt,Is.EqualTo(10));
            Assert.That(m.Snapshot(5).orbs.Single(x=>x.id==theirs.id).jigwiABBurnAt,Is.EqualTo(10));
        }
        [Test] public void EachOwnersOrbsBurnOneAfterAnotherInCreationOrder()
        {
            // #479: the first orb burns after the DEV delay (5 s), each next one a stagger (1 s) later, per owner.
            var m=Room();var a=Add(m,Players[0],3);var b=Add(m,Players[0],3.5);var c=Add(m,Players[0],4);var other=Add(m,Players[1],4);
            var s=m.Snapshot(5);
            Assert.That(s.orbs.Single(x=>x.id==a.id).jigwiABBurnAt,Is.EqualTo(10));
            Assert.That(s.orbs.Single(x=>x.id==b.id).jigwiABBurnAt,Is.EqualTo(11));
            Assert.That(s.orbs.Single(x=>x.id==c.id).jigwiABBurnAt,Is.EqualTo(12));
            Assert.That(s.orbs.Single(x=>x.id==other.id).jigwiABBurnAt,Is.EqualTo(10),"Another board has its own queue.");
            Assert.That(m.TryGenerate(Players[0],Request(),6,out var late,out var e),Is.True,e);
            Assert.That(late.jigwiABBurnAt,Is.EqualTo(13),"A new orb joins the end of its board's queue.");
            Assert.That(m.Snapshot(10.5).orbs.Any(x=>x.id==a.id),Is.False);
            Assert.That(m.Snapshot(10.5).orbs.Any(x=>x.id==b.id),Is.True,"The next orb is still burning.");
        }
        [Test] public void TalismanClearsOnlyTheWritersFireAndGivesThemFiveWaterOrbsOnce()
        {
            // #479: "물" written -> that seat stops burning and gets talisman water; A ends when every seat wrote.
            var m=Room();var mine=Add(m,Players[0],4);var theirs=Add(m,Players[1],4);var a=Current(m,5);
            Assert.That(a.players.Select(p=>p.playerId),Is.EquivalentTo(Players));
            Assert.That(a.players.All(p=>p.talismanAt==-1),Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Players[0],"wrong-round",6),Is.False);
            Assert.That(m.TryCompleteJigwiTalisman(Players[0],a.id,6),Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Players[0],a.id,6.5),Is.False,"One talisman per seat per A.");
            var s=m.Snapshot(6.5);
            Assert.That(s.orbs.Single(o=>o.id==mine.id).jigwiABBurnAt,Is.EqualTo(-1));
            Assert.That(s.orbs.Single(o=>o.id==theirs.id).jigwiABBurnAt,Is.EqualTo(10),"Another seat keeps burning.");
            var water=s.orbs.Where(o=>o.jigwiTalismanReward).ToArray();
            Assert.That(water,Has.Length.EqualTo(5));
            Assert.That(water.All(o=>o.owner==Players[0]&&o.kind==LabOrbKind.Combined&&o.element==LabOrbElement.Water),Is.True);
            Assert.That(water.All(o=>o.expiresAt==21&&o.jigwiABBurnAt==-1&&o.jigwiABRewardPaintScale==1.5f),Is.True);
            Assert.That(string.IsNullOrEmpty(water[0].jigwiABSupplyEventId),Is.True,"Talisman water paints; it is not B ammunition.");
            Assert.That(m.TryGenerate(Players[0],Request(),7,out var fresh,out var e),Is.True,e);
            Assert.That(fresh.jigwiABBurnAt,Is.EqualTo(-1),"A finished seat no longer catches fire.");
            var mid=m.Snapshot(7);
            Assert.That(mid.jigwiAB.Active(7).resolution,Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Players[1],a.id,8),Is.True);
            var done=m.Snapshot(8).jigwiAB.rounds.Single();
            Assert.That(done.resolution,Is.EqualTo(LabJigwiABResolution.Completed));Assert.That(done.resolvedAt,Is.EqualTo(8));
            Assert.That(m.Snapshot(8).orbs.Count(o=>o.jigwiTalismanReward),Is.EqualTo(10));
            Assert.That(LabJigwiABNetworkValidation.Valid(m.Snapshot(8).jigwiAB,m.Snapshot(8).players,m.Revision),Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Players[0],a.id,31),Is.False,"No talisman outside a running A.");
        }
        [Test] public void TalismanWaterPaintsMoreThanOrdinaryWaterWithoutBFireReward()
        {
            var m=Room();var control=Room();Current(m,5);Current(control,5);
            Assert.That(m.TryCompleteJigwiTalisman(Players[0],Current(m,6).id,6),Is.True);
            var water=m.Snapshot(6).orbs.First(o=>o.jigwiTalismanReward);
            var ordinary=Add(control,Players[0],6,element:LabOrbElement.Water);
            Launch(m,Orbs(m)[water.id],7);Launch(control,ordinary,7);
            var flying=m.Snapshot(7).orbs.Single(o=>o.id==water.id);
            Assert.That(flying.jigwiABReward,Is.False);Assert.That(flying.jigwiABRewardPaintScale,Is.EqualTo(1.5f));
            Assert.That(m.ConfirmHit(water.id,7,Vector3.forward,out _,out _,out _),Is.True);
            Assert.That(control.ConfirmHit(ordinary.id,7,Vector3.forward,out _,out _,out _),Is.True);
            Assert.That(m.PaintSurface.CoveredCount,Is.GreaterThan(control.PaintSurface.CoveredCount));
        }
        [Test] public void ValidatorRejectsImpossibleTalismanStates()
        {
            var m=Room();var a=Current(m,5);Assert.That(m.TryCompleteJigwiTalisman(Players[0],a.id,6),Is.True);
            var s=m.Snapshot(6);Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players,m.Revision),Is.True);
            var copy=s.jigwiAB.Copy();copy.rounds[0].players[1].talismanAt=4;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False,"Before A started.");
            copy=s.jigwiAB.Copy();copy.rounds[0].resolution=LabJigwiABResolution.Completed;copy.rounds[0].resolvedAt=6;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False,"A cannot finish while a seat still burns.");
            copy=s.jigwiAB.Copy();copy.rounds[0].players=copy.rounds[0].players.Take(1).ToArray();
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False,"Every seat must be listed.");
            var b=m.Snapshot(31);copy=b.jigwiAB.Copy();copy.rounds[1].players[0].talismanAt=31;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,b.players),Is.False,"B never carries a talisman time.");
        }
        [Test] public void GenerateCombineAndTransferKeepEarliestBurnAndOrdinaryExpiry()
        {
            var m=Room();var a=Current(m,11);ulong owner=Players[0];
            Assert.That(m.TryGenerate(owner,Request(),11,out var generated,out var e),Is.True,e);
            Assert.That(generated.jigwiABBurnAt,Is.EqualTo(16));Assert.That(generated.expiresAt,Is.EqualTo(-1));
            var yin=Add(m,owner,11,LabOrbKind.Yin);var yang=Add(m,owner,11,LabOrbKind.Yang);
            yin.jigwiABBurnAt=16;yin.jigwiABBurnEventId=a.id;
            Assert.That(m.TryCombineAt(owner,yin.id,yang.id,Request(),12,new Vector2(.5f,.5f),Vector2.zero,out var comb,out e),Is.True,e);
            Assert.That(comb.jigwiABBurnAt,Is.EqualTo(16));
            Assert.That(m.TryTransferAt(owner,comb.id,true,new Vector2(1,.5f),Vector2.right,Request(),13,
                LabEdgeCrossingKind.Swipe,out var received,out e),Is.True,e);
            Assert.That(received.owner,Is.Not.EqualTo(owner));Assert.That(received.jigwiABBurnAt,Is.EqualTo(16));
            var incoming=Add(m,received.owner,13,LabOrbKind.Yin);
            Assert.That(m.TryTransferAt(incoming.owner,incoming.id,true,new Vector2(1,.5f),Vector2.right,Request(),14,
                LabEdgeCrossingKind.Swipe,out received,out e),Is.True,e);
            Assert.That(received.owner,Is.EqualTo(owner));Assert.That(received.jigwiABBurnAt,Is.EqualTo(19));
        }
        [Test] public void BurnConsumesIdleButLeavesTheLaunchedProjectileAlive()
        {
            var m=Room();ulong owner=Players[0];var idle=Add(m,owner,4);var flying=Add(m,owner,4);
            m.Tick(5);Launch(m,flying,6);var snapshot=m.Snapshot(10);
            Assert.That(snapshot.orbs.Any(o=>o.id==idle.id),Is.False);
            Assert.That(snapshot.orbs.Single(o=>o.id==flying.id).inFlight,Is.True);
        }
        [Test] public void BossHitsNoLongerEndAOrClearItsBurn()
        {
            // #479: A is no longer cleared by three combined boss hits.
            var m=Room();var burning=Add(m,Players[0],4,LabOrbKind.Yin);m.Tick(5);
            for(int i=0;i<3;i++)BossHit(m,Add(m,Players[0],6),6,Vector3.forward);
            var s=m.Snapshot(6);
            Assert.That(s.jigwiAB.Active(6),Is.Not.Null);
            Assert.That(s.jigwiAB.Active(6).resolution,Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(s.orbs.Single(o=>o.id==burning.id).jigwiABBurnAt,Is.EqualTo(10));
        }
        [Test] public void NaturalAEndClearsPendingDeadlinesWithoutResurrectingBurnedOrbs()
        {
            var m=Room();var a=Current(m,11);
            Assert.That(m.TryGenerate(Players[0],Request(),11,out var old,out var e),Is.True,e);m.Tick(16);
            Assert.That(m.Snapshot(16).orbs.Any(o=>o.id==old.id),Is.False);
            Assert.That(m.TryGenerate(Players[0],Request(),19,out var late,out e),Is.True,e);
            Assert.That(late.jigwiABBurnAt,Is.EqualTo(24));var done=m.Snapshot(20);
            Assert.That(done.jigwiAB.rounds.Single().resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(done.orbs.Single(o=>o.id==late.id).jigwiABBurnAt,Is.EqualTo(-1));
            Assert.That(done.orbs.Single(o=>o.id==late.id).expiresAt,Is.EqualTo(-1));
        }
        [Test] public void BProvidesTaggedWaterWhileOrdinaryRawPairsKeepTheirNormalElement()
        {
            // #509: two players → one picked seat with one pillar; free Water lands on the helper's board.
            var m=Room();var normal=Room(LabStageYokai.None);var b=Current(m,31);normal.Tick(31);
            Assert.That(b.players.Length,Is.EqualTo(1));Assert.That(b.players.Single().targets.Length,Is.EqualTo(1));
            ulong helper=Helper(b,Players);
            Assert.That(m.Snapshot(31).orbs.Where(o=>o.jigwiSupply),Is.Not.Empty);
            Assert.That(m.Snapshot(31).orbs.All(o=>o.jigwiABSupplyEventId==b.id && o.element==LabOrbElement.Water && o.owner==helper),Is.True);
            var yin=Add(m,Players[0],31,LabOrbKind.Yin);var yang=Add(m,Players[0],31,LabOrbKind.Yang);
            var oldYin=Add(normal,Players[0],31,LabOrbKind.Yin);var oldYang=Add(normal,Players[0],31,LabOrbKind.Yang);
            Assert.That(normal.TryCombineAt(Players[0],oldYin.id,oldYang.id,Request(),31,new Vector2(.5f,.5f),Vector2.zero,out var expected,out var error),Is.True,error);
            Assert.That(m.TryCombineAt(Players[0],yin.id,yang.id,Request(),31,new Vector2(.5f,.5f),Vector2.zero,out var actual,out error),Is.True,error);
            Assert.That(actual.element,Is.EqualTo(expected.element));Assert.That(actual.jigwiABSupplyEventId,Is.Null.Or.Empty);
            Assert.That(m.Snapshot(31).jigwi,Is.Empty,"The old manual fire policy stays inactive.");
        }
        [Test]
        public void APickedWaterBossHitCountsAndStillPaintsWhileOtherColoursAndHelpersDoNotCount()
        {
            var m=Room();var b=Current(m,31);var p=b.players[0];ulong helper=Helper(b,Players);
            Hit(m,p.playerId,31,LabOrbElement.Fire);Assert.That(Current(m,31).syncCount,Is.Zero,"Only Water counts.");
            Hit(m,helper,31);Assert.That(Current(m,31).syncCount,Is.Zero,"A helper's Water is ordinary paint.");
            int painted=m.PaintSurface.PaintCount;var before=m.Snapshot(31).players.Single(x=>x.id==p.playerId);
            Hit(m,p.playerId,31);
            Assert.That(Current(m,31).syncCount,Is.EqualTo(1),"A single picked seat completes a sync with its own hit.");
            var after=m.Snapshot(31).players.Single(x=>x.id==p.playerId);
            Assert.That(m.PaintSurface.PaintCount,Is.EqualTo(painted+1),"The counted Water still paints the boss as usual.");
            Assert.That(after.hitTotal,Is.EqualTo(before.hitTotal+1));
            Assert.That(m.JigwiABBlocksBoss(p.playerId,31),Is.False,"#509: no curtain blocks anyone.");
        }
        [Test] public void PickedSeatsMustHitWithinOneSecondOrTheCountResets()
        {
            // #509: three players → two picked. Both inside 1 s counts; a late partner resets to 0.
            var m=Room3();var b=Current(m,31);Assert.That(b.players.Length,Is.EqualTo(2));
            ulong first=b.players[0].playerId,second=b.players[1].playerId;
            Hit(m,first,31);
            Assert.That(Current(m,31.5).syncWindowAt,Is.EqualTo(31d));
            Hit(m,second,31.9);
            var now=Current(m,31.9);Assert.That(now.syncCount,Is.EqualTo(1));Assert.That(now.syncWindowAt,Is.EqualTo(-1d));
            Assert.That(now.players.All(p=>p.targets[0].extinguishedAt==-1d),Is.True);
            Hit(m,first,32);
            m.Tick(33);now=Current(m,33);
            Assert.That(now.syncCount,Is.Zero,"The partner missed the second: back to 0/3.");
            Assert.That(now.syncResetAt,Is.EqualTo(33d));Assert.That(now.syncWindowAt,Is.EqualTo(-1d));
            var s=m.Snapshot(33);Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players,m.Revision),Is.True);
            Clear(m,b,34);
            Assert.That(m.Snapshot(34).jigwiAB.rounds.Single(r=>r.slot==1).resolution,Is.EqualTo(LabJigwiABResolution.Completed));
            foreach(ulong id in Three)Assert.That(m.JigwiABBlocksBoss(id,34),Is.False);
        }
        [Test] public void NobodyIsBlockedAndOnlyHelpersGetTheFreeWater()
        {
            var m=Room3();var b=Current(m,31);ulong helper=Helper(b,Three);
            foreach(ulong id in Three)Assert.That(m.JigwiABBlocksBoss(id,31),Is.False);
            Assert.That(m.Snapshot(31).orbs.Where(o=>o.jigwiSupply).All(o=>o.originOwner==helper),Is.True);
            Assert.That(m.Snapshot(31).orbs.Count(o=>o.jigwiSupply),Is.GreaterThan(0));
        }
        [Test] public void BSuccessNeverShrinksTheBoardAndStartsAnImmediateFiveSecondLaunchReward()
        {
            var m=Room();var b=Current(m,31);var idle=Add(m,Players[0],31);
            var bombSmall=Add(m,Players[0],31);bombSmall.small=true;
            var flying=Add(m,Players[0],31);Launch(m,flying,31);
            Assert.That(m.Snapshot(31).orbs.All(o=>o.jigwiABRadiusScale==1f),Is.True);
            Clear(m,b,31);
            var snapshot=m.Snapshot(31);var done=snapshot.jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(done.resolution,Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(done.rewardUntil,Is.EqualTo(36d));
            Assert.That(done.clearedThroughRevision,Is.Zero);
            Assert.That(snapshot.orbs.Single(o=>o.id==idle.id).jigwiABReward,Is.False);
            Assert.That(snapshot.orbs.Single(o=>o.id==bombSmall.id).small,Is.True);
            Assert.That(snapshot.orbs.Single(o=>o.id==flying.id).jigwiABReward,Is.False,
                "A projectile already in flight cannot gain the later reward retroactively.");
            var rewarded=Add(m,Players[0],31);Launch(m,rewarded,31);
            var approved=m.Snapshot(31).orbs.Single(o=>o.id==rewarded.id);
            Assert.That(approved.jigwiABReward,Is.True);
            Assert.That(approved.jigwiABRewardSizeScale,Is.EqualTo(config.JigwiABRewardSizeScale));
            Assert.That(approved.jigwiABRewardPaintScale,Is.EqualTo(config.JigwiABRewardPaintScale));
        }
        [Test] public void OverlappingWarningsNeverSkipActivationAndTheHundredSecondFeverRemains()
        {
            var m=Room();var snapshot=m.Snapshot(54);
            Assert.That(snapshot.jigwiAB.Active(54).kind,Is.EqualTo(LabJigwiABKind.PersonalBurn));
            Assert.That(snapshot.jigwiAB.Warning(54).kind,Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB,snapshot.players,m.Revision),Is.True);
            snapshot=m.Snapshot(55);
            Assert.That(snapshot.jigwiAB.Active(55).slot,Is.EqualTo(3));
            Assert.That(snapshot.jigwiAB.rounds.Single(r=>r.slot==2).resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            // #509: the 15-second B2 runs into A3's warning.
            snapshot=m.Snapshot(69.5);
            Assert.That(snapshot.jigwiAB.Active(69.5).kind,Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(snapshot.jigwiAB.Warning(69.5).kind,Is.EqualTo(LabJigwiABKind.PersonalBurn));
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB,snapshot.players,m.Revision),Is.True);
            snapshot=m.Snapshot(98);
            Assert.That(snapshot.jigwiAB.rounds.Length,Is.EqualTo(6));
            Assert.That(snapshot.jigwiAB.rounds.Any(r=>r.resolution==LabJigwiABResolution.Skipped),Is.False);
            Assert.That(snapshot.jigwiAB.rounds.Select(r=>r.activeAt),Is.EqualTo(new[]{5d,25d,42d,55d,70d,85d}));
            Assert.That(snapshot.jigwiAB.rounds.Where(r=>r.kind==LabJigwiABKind.GroupFire).Select(r=>r.endsAt-r.activeAt),
                Is.All.EqualTo(15d));
            ulong fence=m.JigwiABFailureRevision;
            snapshot=m.Snapshot(100);
            Assert.That(snapshot.jigwiAB.fever,Is.True);Assert.That(snapshot.jigwiAB.Active(100),Is.Null);
            Assert.That(m.JigwiABFailureRevision,Is.EqualTo(fence),"Cutoff cleanup must not apply another failure.");
            Assert.That(m.JigwiStageRecoveryMultiplier(100),Is.EqualTo(2));
            Assert.That(m.Snapshot(120).phase,Is.EqualTo(LabPhase.Defeat));
        }
        [Test] public void DeepCopyAndValidationRejectNaNAndDuplicateNestedTargets()
        {
            var m=Room();var s=m.Snapshot(31);Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players),Is.True);
            var copy=s.jigwiAB.Copy();copy.rounds[1].players[0].targets[0].x=float.NaN;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(m.Snapshot(31).jigwiAB,s.players),Is.True);
            copy=s.jigwiAB.Copy();var t=copy.rounds[1].players[0].targets[0];copy.rounds[1].players[0].targets=new[]{t,t.Copy()};
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False);
        }
        [Test] public void ARecoveryStaysNormalForEverySeatAndFeverStillDoubles()
        {
            // #479 retired A's recovery boost (DEV 241); only the 100-second fever changes recovery.
            var v=config.CaptureDeveloperValues();v[6]=1;Apply(v);var m=Room();
            // Read both seats at 11 before advancing; the model clock never runs backwards.
            var before=m.Snapshot(11).players.ToDictionary(p=>p.id,p=>p.stamina);
            var after=m.Snapshot(12).players.ToDictionary(p=>p.id,p=>p.stamina);
            foreach(ulong id in Players)Assert.That(after[id],Is.EqualTo(before[id]+1).Within(.0001f));
            Assert.That(m.JigwiStageRecoveryMultiplier(100),Is.EqualTo(2));
        }
        [TestCase(1)] [TestCase(6)] public void EachPickedSeatHasOnePillarWhateverTheOldFireCount(int count)
        {
            var v=config.CaptureDeveloperValues();v[244]=count;Apply(v);var m=Room();var s=m.Snapshot(31);
            Assert.That(s.jigwiAB.Active(31).players.All(p=>p.targets.Length==1),Is.True);
            Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players),Is.True);
        }
        [Test] public void ModernABRejectsLegacyManualChallengesWithoutFreeSupplies()
        {
            var m=Room();Assert.That(m.TryStartJigwiFire(Players[0],0,out var e),Is.False);
            Assert.That(e,Is.EqualTo("STAGE_PATTERN_NOT_ALLOWED"));
            Assert.That(m.TryStartJigwiGenerationBurn(Players[0],0,out _),Is.False);Assert.That(m.Snapshot(0).orbs,Is.Empty);
        }
        [TestCase(2,1)] [TestCase(3,2)] [TestCase(4,3)] [TestCase(5,3)]
        public void GroupFirePicksOneTwoOrThreeActualParticipantsIncludingHostZero(int count,int picked)
        {
            var ids=Enumerable.Range(0,count).Select(i=>(ulong)(i*10)).ToArray();var m=new LabModel();
            m.Begin("ab-members-"+count,ids,0,config,LabStageYokai.Jigwi);var s=m.Snapshot(31);
            var chosen=s.jigwiAB.Active(31).players.Select(p=>p.playerId).ToArray();
            Assert.That(chosen.Length,Is.EqualTo(picked));Assert.That(chosen.Distinct().Count(),Is.EqualTo(picked));
            Assert.That(ids,Is.SupersetOf(chosen));
            Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players),Is.True);
            var copy=s.jigwiAB.Copy();copy.rounds[1].players=copy.rounds[1].players.Take(picked-1).ToArray();
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False,"The picked count follows the seat count.");
        }
        [Test] public void StandaloneLabStillAllowsItsOwnManualWaterChallenge()
        {
            var m=Room(LabStageYokai.None);Assert.That(m.TryStartJigwiFire(Players[0],0,out var e),Is.True,e);
            Assert.That(m.Snapshot(0).jigwiAB.enabled,Is.False);
            Assert.That(m.Snapshot(0).jigwi.Single().FireActive(0),Is.True);
            Assert.That(m.Snapshot(0).orbs.Any(o=>o.jigwiSupply),Is.True);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void ValidatorRejectsImpossibleCompletionStates(int scenario)
        {
            var m=Room();var snapshot=m.Snapshot(scenario==0?11:31);var copy=snapshot.jigwiAB.Copy();
            var value=copy.rounds.Last();
            if(scenario==0)
            {
                value.rewardPaintScale=2f; // #479: A never carries a reward.
            }
            else if(scenario==1)
            {
                value.resolution=LabJigwiABResolution.Completed;value.resolvedAt=31;
            }
            else
            {
                foreach(var player in value.players)foreach(var target in player.targets)
                {target.lit=false;target.extinguishedAt=31;target.relightAt=43;}
            }
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,snapshot.players),Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB,snapshot.players),Is.True);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void SyncProgressValidationRejectsImpossibleWindowsAndCounts(int scenario)
        {
            var m=Room3();var b=Current(m,31);Hit(m,b.players[0].playerId,31);
            var snapshot=m.Snapshot(31.2);var copy=snapshot.jigwiAB.Copy();var value=copy.rounds.Single(r=>r.slot==1);
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB,snapshot.players),Is.True);
            if(scenario==0)value.players[1].targets[0].extinguishedAt=32.5; // outside the open window
            else if(scenario==1)value.syncCount=3;                          // three clears but still running
            else if(scenario==2){value.syncWindowAt=-1d;}                  // a hit time without a window
            else value.players[0].targets[0].relightAt=40;                  // #509 never relights
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,snapshot.players),Is.False);
        }
        [Test]
        public void ACompletedBNoLongerCountsAndEveryoneKeepsPainting()
        {
            var model = Room(); var group = Current(model, 31d); var own = group.players[0];
            Clear(model, group, 31d);
            Assert.That(model.Snapshot(31d).jigwiAB.rounds.Single(r => r.slot == 1).resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(model.Snapshot(31d).jigwiAB.rounds.Single(r => r.slot == 1).syncCount, Is.EqualTo(3));
            Hit(model, own.playerId, 31.5d);
            Assert.That(model.Snapshot(31.5d).jigwiAB.rounds.Single(r => r.slot == 1).syncCount, Is.EqualTo(3));
            foreach (ulong id in Players) Assert.That(model.JigwiABBlocksBoss(id, 31.5d), Is.False);
        }

        [TestCase(-.5d)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        [TestCase(24.99d)] [TestCase(40.01d)]
        public void WaterSplashValidationRejectsInvalidAndOutOfRoundTimes(double value)
        {
            var model = Room(); var snapshot = model.Snapshot(31d); var copy = snapshot.jigwiAB.Copy();
            copy.rounds.Last().players[0].lastWaterSplashAt = value;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy, snapshot.players), Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB, snapshot.players), Is.True);
        }

        [Test]
        public void BFailureRetiresAllBoardKindsAndFlightsOnceButPreservesDrumsAndCombatStatistics()
        {
            // #509: B1 now fails at 40, the same second as the common gimmick; keep this fixture to B's own wipe.
            var quiet=config.CaptureDeveloperValues();NoCommonGimmick(quiet);Apply(quiet);
            var m=Room();var before=m.Snapshot(39.9d);
            string[] drums=before.orbs.Where(o=>o.kind.IsDrum()).Select(o=>o.id).ToArray();
            var fixture=new List<LabOrbState>();
            foreach(ulong owner in Players)
            {
                foreach(LabOrbKind kind in Enum.GetValues(typeof(LabOrbKind)))
                    if(!kind.IsDrum())fixture.Add(Add(m,owner,39.9d,kind));
                var flying=Add(m,owner,39.9d);Launch(m,flying,39.9d);fixture.Add(flying);
            }
            var allRetired=m.Snapshot(39.9d).orbs.Where(o=>!o.kind.IsDrum()).ToArray();
            m.DrainOrbExpiries();m.DrainBombExplosions();
            var expired=m.Snapshot(40d);var failure=expired.jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(failure.resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(failure.resolvedAt,Is.EqualTo(40d));
            Assert.That(failure.clearedThroughRevision,Is.GreaterThan(0ul));
            Assert.That(m.JigwiABFailureRevision,Is.EqualTo(failure.clearedThroughRevision));
            Assert.That(expired.orbs.Select(o=>o.id),Is.EquivalentTo(drums));
            Assert.That(expired.players.All(player=>player.stamina==0f),Is.True);
            Assert.That(expired.hp,Is.EqualTo(before.hp));
            Assert.That(expired.players.Select(p=>p.personalHp),Is.EqualTo(before.players.Select(p=>p.personalHp)));
            Assert.That(expired.players.Select(p=>p.hitTotal),Is.EqualTo(before.players.Select(p=>p.hitTotal)));
            Assert.That(expired.players.Select(p=>p.throwTotal),Is.EqualTo(before.players.Select(p=>p.throwTotal)));
            Assert.That(expired.players.Select(p=>p.damageTotal),Is.EqualTo(before.players.Select(p=>p.damageTotal)));
            Assert.That(m.PaintSurface.PaintCount,Is.Zero);Assert.That(m.DrainBombExplosions(),Is.Empty);
            Assert.That(m.DrainOrbExpiries().Select(e=>e.OrbId),Is.EquivalentTo(allRetired.Select(o=>o.id)));
            foreach(var orb in allRetired)
            {
                Assert.That(m.ConfirmHit(orb.id,40d),Is.False);
                Assert.That(m.ConfirmMiss(orb.id,40d),Is.False);
                Assert.That(m.TryLaunch(orb.owner,orb.id,Request(),40d,out _,out _),Is.False);
                Assert.That(m.TryUseItemAt(orb.owner,orb.id,Request(),40d,Vector2.one*.5f,out _,out _),Is.False);
            }
            m.Tick(40d);Assert.That(m.DrainOrbExpiries(),Is.Empty);
            Assert.That(m.JigwiABState.rounds.Single(r=>r.slot==1).clearedThroughRevision,
                Is.EqualTo(failure.clearedThroughRevision));
            Assert.That(m.Snapshot(40d).players.All(player=>player.stamina==0f),Is.True);
        }

        [Test]
        public void BFailureAllowsNormalRecoveryAndNewApprovalsWithoutReapplyingTheWipe()
        {
            var values=config.CaptureDeveloperValues();values[6]=2f;NoCommonGimmick(values);Apply(values);
            var m=Room();m.Tick(40d);ulong fence=m.JigwiABFailureRevision;
            var recovered=m.Snapshot(40.5d);
            Assert.That(recovered.players.Select(p=>p.stamina),Is.EqualTo(new[]{1f,1f}));
            Assert.That(m.TryGenerate(Players[0],Request(),40.5d,out var fresh,out var reason),Is.True,reason);
            Assert.That(fresh.revision,Is.GreaterThan(fence));
            Assert.That(m.Snapshot(41d).orbs.Any(o=>o.id==fresh.id),Is.True);
            Assert.That(m.JigwiABFailureRevision,Is.EqualTo(fence));
            Assert.That(m.Snapshot(120d).phase,Is.EqualTo(LabPhase.Defeat));
            m.Begin("ab-test",Players,121d,config,LabStageYokai.Jigwi);
            Assert.That(m.JigwiABFailureRevision,Is.Zero);
            Assert.That(m.JigwiABState.rounds,Is.Empty);
        }

        [Test]
        public void RewardIsCapturedOnlyAtLaunchAndRemainsOnAFlightAfterTheFiveSecondWindow()
        {
            var m=Room();var group=Current(m,31d);var previous=Add(m,Players[0],31d);Launch(m,previous,31d);
            Clear(m,group,31d);var reward=m.JigwiABState.Reward(31d);
            Assert.That(reward,Is.Not.Null);Assert.That(reward.RewardActive(30.999d),Is.False);
            Assert.That(reward.RewardActive(31d),Is.True);Assert.That(reward.RewardActive(36d),Is.False);
            var last=Add(m,Players[0],35.999d);Launch(m,last,35.999d);
            var ordinary=Add(m,Players[0],36d);Launch(m,ordinary,36d);
            var after=m.Snapshot(36d);
            Assert.That(after.orbs.Single(o=>o.id==previous.id).jigwiABReward,Is.False);
            Assert.That(after.orbs.Single(o=>o.id==last.id).jigwiABReward,Is.True);
            Assert.That(after.orbs.Single(o=>o.id==ordinary.id).jigwiABReward,Is.False);
            Assert.That(after.orbs.Single(o=>o.id==ordinary.id).jigwiABRewardSizeScale,Is.EqualTo(1f));
            Assert.That(after.orbs.Single(o=>o.id==ordinary.id).jigwiABRewardPaintScale,Is.EqualTo(1f));
            Assert.That(after.jigwiAB.Reward(36d),Is.Null);
            int painted=m.PaintSurface.PaintCount;
            Assert.That(m.ConfirmHit(last.id,36d,Vector3.forward,out _,out _,out _),Is.True);
            Assert.That(m.PaintSurface.PaintCount,Is.EqualTo(painted+1));
        }

        [TestCase(false)] [TestCase(true)]
        public void RewardPaintBudgetUsesTheCapturedPaintScaleAndKeepsTheThreeShareClamp(bool fiveColor)
        {
            var values=config.CaptureDeveloperValues();values[251]=3f;values[252]=fiveColor?3f:1.5f;Apply(values);
            var m=Room();var group=Current(m,31d);Clear(m,group,31d);
            // #509: the three sync hits already painted the back of the boss; compare only this forward shot.
            double start=m.PaintSurface.Coverage+m.PaintSurface.PendingCoverage;
            var shot=Add(m,Players[0],31d,element:LabOrbElement.Water);shot.fiveColor=fiveColor;
            Launch(m,shot,31d);
            Assert.That(m.LaunchScaleOf(shot.id),Is.EqualTo(1f),"Reward visual size is not a second paint-area multiplier.");
            Assert.That(shot.jigwiABRewardSizeScale,Is.EqualTo(3f));
            Assert.That(m.ConfirmHit(shot.id,31d,Vector3.forward,out _,out _,out _),Is.True);
            var reference=Room(LabStageYokai.None);
            if(fiveColor)
            {
                var control=Add(reference,Players[0],31d,element:LabOrbElement.Water);control.fiveColor=true;
                // Give only this control the equivalent already-clamped paint share through the same hit path.
                var launchScales=(Dictionary<string,float>)typeof(LabModel).GetField("launchScales",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(reference);
                Launch(reference,control,31d);launchScales[control.id]=3f;
                Assert.That(reference.ConfirmHit(control.id,31d,Vector3.forward,out _,out _,out _),Is.True);
            }
            else Assert.That(reference.PaintSurface.RegisterHit(Vector3.forward,reference.PaintColorFor(LabOrbElement.Water),
                Players[0],1,1.5f),Is.True);
            double budget=m.PaintSurface.Coverage+m.PaintSurface.PendingCoverage-start;
            double expected=reference.PaintSurface.Coverage+reference.PaintSurface.PendingCoverage;
            Assert.That(budget,Is.EqualTo(expected).Within(2d/m.PaintSurface.CellCount));
        }

        [TestCase(54d,55d,2,3)] [TestCase(69d,70d,3,4)] [TestCase(84d,85d,4,5)]
        public void WarningOverlapResolvesThePriorAAtTheBoundaryEvenWithAFractionalStartClock(
            double warning,double activation,int personalSlot,int groupSlot)
        {
            const double start=100.2d;var m=new LabModel();m.Begin("fractional-ab",Players,start,config,LabStageYokai.Jigwi);
            var before=m.Snapshot(start+warning);
            Assert.That(before.jigwiAB.Active(start+warning).slot,Is.EqualTo(personalSlot));
            Assert.That(before.jigwiAB.Warning(start+warning).slot,Is.EqualTo(groupSlot));
            Assert.That(LabJigwiABNetworkValidation.Valid(before.jigwiAB,before.players,m.Revision),Is.True);
            var after=m.Snapshot(start+activation);
            Assert.That(after.jigwiAB.Active(start+activation).slot,Is.EqualTo(groupSlot));
            Assert.That(after.jigwiAB.rounds.Single(r=>r.slot==personalSlot).resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(LabJigwiABNetworkValidation.Valid(after.jigwiAB,after.players,m.Revision),Is.True);
            Assert.That(after.jigwiAB.rounds.Count(r=>r.Active(start+activation)),Is.EqualTo(1));
        }

        [Test]
        public void TeamDefeatCancelsBWithoutApplyingFailureOrDeletingItsOrbs()
        {
            var m=Room();var group=Current(m,31d);var kept=Add(m,Players[0],31d);
            Assert.That(m.TryApplyPlayerDamage(Players[0],int.MaxValue,31d),Is.True);
            Assert.That(m.TryApplyPlayerDamage(Players[1],int.MaxValue,31d),Is.True);
            var ended=m.Snapshot(35d);var cancelled=ended.jigwiAB.rounds.Single(r=>r.id==group.id);
            Assert.That(ended.phase,Is.EqualTo(LabPhase.Defeat));
            Assert.That(cancelled.resolution,Is.EqualTo(LabJigwiABResolution.Cancelled));
            Assert.That(cancelled.clearedThroughRevision,Is.Zero);Assert.That(m.JigwiABFailureRevision,Is.Zero);
            Assert.That(ended.orbs.Any(o=>o.id==kept.id),Is.True);
            Assert.That(ended.players.All(p=>p.stamina==100f),Is.True);
        }

        [Test]
        public void PaintVictoryCancelsTheUnfinishedBWithoutItsFailurePenalty()
        {
            var values=config.CaptureDeveloperValues();values[105]=100f;values[107]=100f;Apply(values);
            var m=Room();var group=Current(m,31d);var own=group.players[0];ulong helper=Helper(group,Players);
            Sync(m,group,31d);
            var preserved=Add(m,own.playerId,31d);
            BossHit(m,Add(m,helper,31d),31d,Vector3.forward);
            var ended=m.Snapshot(35d);
            Assert.That(ended.phase,Is.EqualTo(LabPhase.Victory));
            Assert.That(ended.jigwiAB.rounds.Single(r=>r.id==group.id).resolution,
                Is.EqualTo(LabJigwiABResolution.Cancelled));
            Assert.That(m.JigwiABFailureRevision,Is.Zero);
            Assert.That(ended.orbs.Any(o=>o.id==preserved.id),Is.True);
            Assert.That(ended.players.All(p=>p.stamina>0f),Is.True);
        }

        [Test]
        public void RewardAndFailureMetadataRoundTripAndCopiesRejectMalformedOrFutureFences()
        {
            var m=Room();var group=Current(m,31d);Clear(m,group,31d);var source=m.Snapshot(71d);
            var roundTrip=JsonUtility.FromJson<LabJigwiABSnapshot>(JsonUtility.ToJson(source.jigwiAB));
            Assert.That(LabJigwiABNetworkValidation.Valid(roundTrip,source.players,m.Revision),Is.True);
            Assert.That(roundTrip.rounds.Single(r=>r.slot==1).rewardUntil,Is.EqualTo(36d));
            var failure=roundTrip.rounds.Single(r=>r.slot==3);
            Assert.That(failure.clearedThroughRevision,Is.GreaterThan(0ul));
            Assert.That(m.JigwiABFailureRevision,Is.EqualTo(failure.clearedThroughRevision));
            var copy=roundTrip.Copy();copy.rounds[1].rewardSizeScale=float.NaN;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[1].rewardPaintScale=3.01f;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[1].rewardUntil=36.01d;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[1].clearedThroughRevision=1;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[3].clearedThroughRevision=0;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[3].clearedThroughRevision=m.Revision+1;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(roundTrip,source.players,m.Revision),Is.True);
            Assert.That(source.jigwiAB.rounds[1].rewardSizeScale,Is.EqualTo(config.JigwiABRewardSizeScale));
        }

        [Test] public void FireMotionIsDeterministicForTheSameBattleTimeAndMovesInsideViewport()
        {
            var m=Room();var b=Current(m,31);var f=b.players[0].targets[0];var first=LabJigwiABFireMotion.Position(b,f,0,31);
            Assert.That(LabJigwiABFireMotion.Position(b,f,0,31),Is.EqualTo(first));Assert.That(LabJigwiABFireMotion.Position(b,f,0,32),Is.Not.EqualTo(first));
            for(int i=0;i<100;i++){var p=LabJigwiABFireMotion.Position(b,f,0,31+i*.1);Assert.That(p.x,Is.InRange(.1f,.9f));Assert.That(p.y,Is.InRange(.48f,.86f));}
        }
    }
}
