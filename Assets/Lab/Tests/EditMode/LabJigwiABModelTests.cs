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
        /// <summary>#509: this B's special Water given to a picked seat (one each).</summary>
        LabOrbState Special(LabModel m,LabJigwiABRound g,ulong seat)=>Orbs(m).Values.Single(o=>o.jigwiABSupplyEventId==g.id&&o.originOwner==seat);
        /// <summary>#509: the picked seat throws its special Water at the boss; it counts but never paints.</summary>
        void Throw(LabModel m,LabJigwiABRound g,ulong seat,double at,Vector3? dir=null)
        {
            var o=Special(m,g,seat);Launch(m,o,at);
            Assert.That(m.ConfirmHit(o.id,at,dir??Vector3.back,out _,out _,out _),Is.False,"Special Water is consumed without paint.");
        }
        /// <summary>#509: every picked seat hits inside one second — the whole B in one go.</summary>
        void Sync(LabModel m,LabJigwiABRound g,double at,Vector3? dir=null){foreach(var p in g.players)Throw(m,g,p.playerId,at,dir);}
        void Clear(LabModel m,LabJigwiABRound g,double at)=>Sync(m,g,at);
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
            // #509: the five talisman Waters drop in one at a time, every 0.3 s from the moment it was written.
            Assert.That(s.orbs.Count(o=>o.jigwiTalismanReward),Is.EqualTo(2),"6.0 and 6.3 so far.");
            s=m.Snapshot(7.25);
            var water=s.orbs.Where(o=>o.jigwiTalismanReward).OrderBy(o=>o.createdAt).ToArray();
            Assert.That(water,Has.Length.EqualTo(5));
            Assert.That(water.Select(o=>o.createdAt).ToArray(),Is.EqualTo(new[]{6d,6.3d,6.6d,6.9d,7.2d}).Within(1e-9));
            Assert.That(water.All(o=>o.owner==Players[0]&&o.kind==LabOrbKind.Combined&&o.element==LabOrbElement.Water),Is.True);
            Assert.That(water.All(o=>System.Math.Abs(o.expiresAt-(o.createdAt+15))<1e-9&&o.jigwiABBurnAt==-1&&o.jigwiABRewardPaintScale==1.5f),Is.True);
            Assert.That(string.IsNullOrEmpty(water[0].jigwiABSupplyEventId),Is.True,"Talisman water paints; it is not B ammunition.");
            Assert.That(m.TryGenerate(Players[0],Request(),7.3,out var fresh,out var e),Is.True,e);
            Assert.That(fresh.jigwiABBurnAt,Is.EqualTo(-1),"A finished seat no longer catches fire.");
            var mid=m.Snapshot(7.3);
            Assert.That(mid.jigwiAB.Active(7.3).resolution,Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Players[1],a.id,8),Is.True);
            var done=m.Snapshot(8).jigwiAB.rounds.Single();
            Assert.That(done.resolution,Is.EqualTo(LabJigwiABResolution.Completed));Assert.That(done.resolvedAt,Is.EqualTo(8));
            Assert.That(m.Snapshot(8).orbs.Count(o=>o.jigwiTalismanReward),Is.EqualTo(6),"The second seat's first drop.");
            Assert.That(m.Snapshot(9.25).orbs.Count(o=>o.jigwiTalismanReward),Is.EqualTo(10));
            Assert.That(LabJigwiABNetworkValidation.Valid(m.Snapshot(9.25).jigwiAB,m.Snapshot(9.25).players,m.Revision),Is.True);
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
        [Test] public void AnUnfinishedTalismanBurnsThatSeatsOrbsButNeverBlocksItsDrums()
        {
            // #540: the A runs out with seat 0 still writing — every orb left on its board burns (one after another).
            // Its drums keep working. Seat 1 wrote its talisman and keeps everything.
            var m=Room();var a=Current(m,5);
            Assert.That(m.TryCompleteJigwiTalisman(Players[1],a.id,6),Is.True);
            Assert.That(m.TryGenerate(Players[0],Request(),19,out var mine,out var e),Is.True,e);
            Assert.That(m.TryGenerate(Players[0],Request(),19.1,out var mine2,out e),Is.True,e);
            Assert.That(m.TryGenerate(Players[1],Request(),19.2,out var theirs,out e),Is.True,e);
            var done=m.Snapshot(20);var round=done.jigwiAB.rounds.Single(r=>r.id==a.id);
            Assert.That(round.resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(LabJigwiABNetworkValidation.Valid(done.jigwiAB,done.players,m.Revision),Is.True);
            var later=m.Snapshot(20.5);
            Assert.That(later.orbs.Any(o=>o.id==mine.id||o.id==mine2.id),Is.False,"The unfinished board's orbs burned.");
            Assert.That(later.orbs.Single(o=>o.id==theirs.id).jigwiABBurnAt,Is.EqualTo(-1),"The finished board keeps its orbs.");
            Assert.That(m.TryGenerate(Players[0],Request(),21,out _,out e),Is.True,"#540: a failed talisman never blocks the drums. "+e);
        }
        [Test] public void BGivesEachPickedSeatOneSpecialWaterWhileOrdinaryRawPairsKeepTheirNormalElement()
        {
            // #509: two players → both picked; each gets exactly one special Water on its own board.
            var m=Room();var normal=Room(LabStageYokai.None);var b=Current(m,31);normal.Tick(31);
            Assert.That(b.players.Select(p=>p.playerId),Is.EquivalentTo(Players));
            var specials=m.Snapshot(31).orbs.Where(o=>o.jigwiSupply).ToArray();
            Assert.That(specials.Select(o=>o.originOwner),Is.EquivalentTo(Players));
            Assert.That(specials.All(o=>o.owner==o.originOwner&&o.jigwiABSupplyEventId==b.id&&o.kind==LabOrbKind.Combined&&o.element==LabOrbElement.Water),Is.True);
            m.Tick(39);Assert.That(m.Snapshot(39).orbs.Count(o=>o.jigwiSupply),Is.EqualTo(2),"No more special Water over time.");
            var yin=Add(m,Players[0],31,LabOrbKind.Yin);var yang=Add(m,Players[0],31,LabOrbKind.Yang);
            var oldYin=Add(normal,Players[0],31,LabOrbKind.Yin);var oldYang=Add(normal,Players[0],31,LabOrbKind.Yang);
            Assert.That(normal.TryCombineAt(Players[0],oldYin.id,oldYang.id,Request(),39,new Vector2(.5f,.5f),Vector2.zero,out var expected,out var error),Is.True,error);
            Assert.That(m.TryCombineAt(Players[0],yin.id,yang.id,Request(),39,new Vector2(.5f,.5f),Vector2.zero,out var actual,out error),Is.True,error);
            Assert.That(actual.element,Is.EqualTo(expected.element));Assert.That(actual.jigwiABSupplyEventId,Is.Null.Or.Empty);
            Assert.That(m.Snapshot(39).jigwi,Is.Empty,"The old manual fire policy stays inactive.");
        }
        [Test]
        public void OnlyTheSpecialWaterCountsWhileOrdinaryWaterStillPaints()
        {
            var m=Room3();var b=Current(m,31);ulong p=b.players[0].playerId;ulong helper=Helper(b,Three);
            Hit(m,p,31,LabOrbElement.Fire);Hit(m,p,31);Hit(m,helper,31);
            Assert.That(Current(m,31).syncWindowAt,Is.EqualTo(-1d),"Ordinary Water (even the picked seat's) does not count.");
            int painted=m.PaintSurface.PaintCount;
            Throw(m,b,p,31.2);
            var now=Current(m,31.2);
            Assert.That(now.syncWindowAt,Is.EqualTo(31.2d));Assert.That(now.syncCount,Is.Zero);
            Assert.That(now.players.Single(x=>x.playerId==p).targets[0].extinguishedAt,Is.EqualTo(31.2d));
            Assert.That(m.PaintSurface.PaintCount,Is.EqualTo(painted),"The special Water never paints.");
            Assert.That(m.JigwiABBlocksBoss(p,31),Is.False,"#509: no curtain blocks anyone.");
        }
        [Test] public void EveryPickedSeatInsideOneSecondClearsBAtOnce()
        {
            // #509: three players → two picked. Both special Waters inside 1 s is the whole B.
            var m=Room3();var b=Current(m,31);Assert.That(b.players.Length,Is.EqualTo(2));
            Throw(m,b,b.players[0].playerId,31);Throw(m,b,b.players[1].playerId,31.9);
            var done=m.Snapshot(31.9).jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(done.resolution,Is.EqualTo(LabJigwiABResolution.Completed));Assert.That(done.resolvedAt,Is.EqualTo(31.9d));
            Assert.That(done.syncCount,Is.EqualTo(1));Assert.That(done.boardFireUntil,Is.EqualTo(-1d));
            var s=m.Snapshot(31.9);Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players,m.Revision),Is.True);
        }
        [Test] public void ALatePartnerFailsTheOnlyChance()
        {
            var m=Room3();var b=Current(m,31);var late=Special(m,b,b.players[1].playerId);
            Throw(m,b,b.players[0].playerId,31);
            m.Tick(32);var failed=m.Snapshot(32).jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(failed.resolution,Is.EqualTo(LabJigwiABResolution.Expired),"One chance: the window closed short.");
            Assert.That(failed.resolvedAt,Is.EqualTo(32d));Assert.That(failed.boardFireUntil,Is.EqualTo(35d));
            Assert.That(failed.syncResetAt,Is.EqualTo(32d));Assert.That(failed.syncWindowAt,Is.EqualTo(-1d));
            Assert.That(m.Snapshot(32).orbs.Any(o=>o.id==late.id),Is.False,"The unused special Water goes with the failed B.");
            var s=m.Snapshot(32);Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players,m.Revision),Is.True);
        }
        [Test] public void AMissedSpecialWaterFailsBAtOnce()
        {
            var m=Room3();var b=Current(m,31);var mine=Special(m,b,b.players[0].playerId);
            Launch(m,mine,31.5);Assert.That(m.ConfirmMiss(mine.id,31.6),Is.True);
            var failed=m.Snapshot(31.6).jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(failed.resolution,Is.EqualTo(LabJigwiABResolution.Expired));Assert.That(failed.resolvedAt,Is.EqualTo(31.6d));
            Assert.That(failed.boardFireUntil,Is.EqualTo(34.6d).Within(1e-9));
            Assert.That(m.Snapshot(31.6).orbs.Any(o=>o.jigwiSupply),Is.False);
            var ordinary=Add(m,b.players[1].playerId,36);Launch(m,ordinary,36);Assert.That(m.ConfirmMiss(ordinary.id,36),Is.True,
                "An ordinary miss never touches B.");
        }
        [Test] public void NobodyIsBlockedAndHelpersGetNoWater()
        {
            var m=Room3();var b=Current(m,31);ulong helper=Helper(b,Three);
            foreach(ulong id in Three)Assert.That(m.JigwiABBlocksBoss(id,31),Is.False);
            m.Tick(38);var supplies=m.Snapshot(38).orbs.Where(o=>o.jigwiSupply).ToArray();
            Assert.That(supplies.Any(o=>o.originOwner==helper),Is.False);
            Assert.That(supplies.Select(o=>o.originOwner),Is.EquivalentTo(b.players.Select(p=>p.playerId)));
        }
        [Test] public void BSuccessSplashesTheBossOnceAndGivesNoLaunchReward()
        {
            var m=Room();var b=Current(m,31);var idle=Add(m,Players[0],31);
            var bombSmall=Add(m,Players[0],31);bombSmall.small=true;
            var flying=Add(m,Players[0],31);Launch(m,flying,31);
            Assert.That(m.Snapshot(31).orbs.All(o=>o.jigwiABRadiusScale==1f),Is.True);
            double paint=m.PaintSurface.Coverage+m.PaintSurface.PendingCoverage;
            Clear(m,b,31);
            var snapshot=m.Snapshot(31);var done=snapshot.jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(done.resolution,Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(done.rewardUntil,Is.EqualTo(-1d),"#509: no timed launch reward after B.");
            Assert.That(done.boardFireUntil,Is.EqualTo(-1d));
            Assert.That(done.clearedThroughRevision,Is.Zero);
            Assert.That(snapshot.jigwiAB.Reward(31),Is.Null);
            Assert.That(m.PaintSurface.Coverage+m.PaintSurface.PendingCoverage,Is.GreaterThan(paint),
                "The supplied sync Water never paints; only the success splash does.");
            Assert.That(snapshot.orbs.Single(o=>o.id==bombSmall.id).small,Is.True);
            Assert.That(snapshot.orbs.Any(o=>o.id==idle.id),Is.True);
            var later=Add(m,Players[0],31);Launch(m,later,31);
            var approved=m.Snapshot(31).orbs.Single(o=>o.id==later.id);
            Assert.That(approved.jigwiABReward,Is.False);
            Assert.That(approved.jigwiABRewardSizeScale,Is.EqualTo(1f));
            Assert.That(approved.jigwiABRewardPaintScale,Is.EqualTo(1f));
            Assert.That(snapshot.jigwiAB.rounds.All(r=>r.rewardSizeScale==1f&&r.rewardPaintScale==1f),Is.True);
        }
        [Test] public void BSuccessSplashPaintsOneMaximumWaterHitWhereEachPickedSeatHit()
        {
            var m=Room();var b=Current(m,31);
            Sync(m,b,31,Vector3.forward);
            Assert.That(m.Snapshot(31).jigwiAB.rounds.Single(r=>r.slot==1).resolution,Is.EqualTo(LabJigwiABResolution.Completed));
            double budget=m.PaintSurface.Coverage+m.PaintSurface.PendingCoverage;
            var reference=Room(LabStageYokai.None);
            foreach(var seat in m.Snapshot(31).players.Where(p=>b.players.Any(x=>x.playerId==p.id)))
                Assert.That(reference.PaintSurface.RegisterHit(Vector3.forward,reference.PaintColorFor(LabOrbElement.Water),seat.id,seat.seat+1,3f),Is.True);
            double expected=reference.PaintSurface.Coverage+reference.PaintSurface.PendingCoverage;
            Assert.That(budget,Is.EqualTo(expected).Within(2d/m.PaintSurface.CellCount),
                "One splash (3 shares) per picked seat where its special Water hit; the special Water itself never paints.");
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
        [TestCase(2,2)] [TestCase(3,2)] [TestCase(4,3)] [TestCase(5,3)]
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
            var m=Room3();var b=Current(m,31);Throw(m,b,b.players[0].playerId,31);
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
            Assert.That(model.Snapshot(31d).jigwiAB.rounds.Single(r => r.slot == 1).syncCount, Is.EqualTo(LabJigwiABTimeline.GroupSyncRequired));
            Hit(model, own.playerId, 31.5d);
            Assert.That(model.Snapshot(31.5d).jigwiAB.rounds.Single(r => r.slot == 1).syncCount, Is.EqualTo(LabJigwiABTimeline.GroupSyncRequired));
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

        public void BFailureSetsEveryBoardOnFireForThreeSecondsAndKeepsOrbsStaminaAndStatistics()
        {
            // #509: B1 fails at 40, the same second as the common gimmick; keep this fixture to B's own result.
            var quiet=config.CaptureDeveloperValues();NoCommonGimmick(quiet);Apply(quiet);
            var m=Room();
            var fixture=new List<LabOrbState>();
            foreach(ulong owner in Players)
            {
                fixture.Add(Add(m,owner,39.9d));
                var flying=Add(m,owner,39.9d);Launch(m,flying,39.9d);fixture.Add(flying);
            }
            var before=m.Snapshot(39.9d);
            m.DrainOrbExpiries();
            var expired=m.Snapshot(40d);var failure=expired.jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(failure.resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(failure.resolvedAt,Is.EqualTo(40d));
            Assert.That(failure.boardFireUntil,Is.EqualTo(43d));
            Assert.That(failure.clearedThroughRevision,Is.Zero,"#509: no board wipe, so no purge fence.");
            Assert.That(m.JigwiABFailureRevision,Is.Zero);
            Assert.That(failure.rewardUntil,Is.EqualTo(-1d));
            Assert.That(expired.orbs.Select(o=>o.id),Is.SupersetOf(fixture.Select(o=>o.id)));
            Assert.That(m.DrainOrbExpiries(),Is.Empty);
            Assert.That(expired.players.Select(p=>p.stamina),Is.EqualTo(before.players.Select(p=>p.stamina)));
            Assert.That(expired.hp,Is.EqualTo(before.hp));
            Assert.That(expired.players.Select(p=>p.hitTotal),Is.EqualTo(before.players.Select(p=>p.hitTotal)));
            Assert.That(LabJigwiABNetworkValidation.Valid(expired.jigwiAB,expired.players,m.Revision),Is.True);
            foreach(ulong owner in Players)
            {
                Assert.That(m.TryGenerate(owner,Request(),41d,out _,out var reason),Is.False);
                Assert.That(reason,Is.EqualTo("JIGWI_BOARD_FIRE"));
            }
            Assert.That(m.JigwiBoardOnFire(42.999d),Is.True);Assert.That(m.JigwiBoardOnFire(43d),Is.False);
        }

        [Test]
        public void BFailureBoardFireEndsOnTimeAndDrumsWorkAgainWithoutReapplying()
        {
            var values=config.CaptureDeveloperValues();values[6]=2f;NoCommonGimmick(values);Apply(values);
            var m=Room();m.Tick(40d);
            Assert.That(m.TryGenerate(Players[0],Request(),42.5d,out _,out var blocked),Is.False);
            Assert.That(blocked,Is.EqualTo("JIGWI_BOARD_FIRE"));
            Assert.That(m.TryGenerate(Players[0],Request(),43d,out var fresh,out var reason),Is.True,reason);
            Assert.That(m.Snapshot(43.5d).orbs.Any(o=>o.id==fresh.id),Is.True);
            Assert.That(m.Snapshot(43.5d).jigwiAB.rounds.Single(r=>r.slot==1).boardFireUntil,Is.EqualTo(43d));
            Assert.That(m.Snapshot(120d).phase,Is.EqualTo(LabPhase.Defeat));
            m.Begin("ab-test",Players,121d,config,LabStageYokai.Jigwi);
            Assert.That(m.JigwiABFailureRevision,Is.Zero);
            Assert.That(m.JigwiABState.rounds,Is.Empty);
            Assert.That(m.JigwiBoardOnFire(121d),Is.False);
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
            var m=Room3();var group=Current(m,31d);var own=group.players[0];ulong helper=Helper(group,Three);
            Throw(m,group,own.playerId,31d); // one picked seat has thrown; the other has not yet
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
        public void ResultMetadataRoundTripsAndCopiesRejectRewardsFencesAndBadBoardFire()
        {
            var m=Room();var group=Current(m,31d);Clear(m,group,31d);var source=m.Snapshot(71d);
            var roundTrip=JsonUtility.FromJson<LabJigwiABSnapshot>(JsonUtility.ToJson(source.jigwiAB));
            Assert.That(LabJigwiABNetworkValidation.Valid(roundTrip,source.players,m.Revision),Is.True);
            Assert.That(roundTrip.rounds.Single(r=>r.slot==1).rewardUntil,Is.EqualTo(-1d));
            Assert.That(roundTrip.rounds.Single(r=>r.slot==1).boardFireUntil,Is.EqualTo(-1d));
            var failure=roundTrip.rounds.Single(r=>r.slot==3);
            Assert.That(failure.resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(failure.boardFireUntil,Is.EqualTo(failure.resolvedAt+3d));
            Assert.That(failure.clearedThroughRevision,Is.Zero);
            var copy=roundTrip.Copy();copy.rounds[1].rewardSizeScale=float.NaN;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[1].rewardPaintScale=3.01f;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[1].rewardUntil=36d;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False,"No B reward window any more.");
            copy=roundTrip.Copy();copy.rounds[1].boardFireUntil=33d;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False,"A success never burns the boards.");
            copy=roundTrip.Copy();copy.rounds[1].clearedThroughRevision=1;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[3].clearedThroughRevision=1;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False,"A failure no longer wipes boards.");
            copy=roundTrip.Copy();copy.rounds[3].boardFireUntil=copy.rounds[3].resolvedAt+10.5d;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[3].boardFireUntil=copy.rounds[3].resolvedAt-.5d;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            copy=roundTrip.Copy();copy.rounds[3].boardFireUntil=double.NaN;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,source.players,m.Revision),Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(roundTrip,source.players,m.Revision),Is.True);
            Assert.That(source.jigwiAB.rounds[1].rewardSizeScale,Is.EqualTo(1f));
        }

        [Test] public void FireMotionIsDeterministicForTheSameBattleTimeAndMovesInsideViewport()
        {
            var m=Room();var b=Current(m,31);var f=b.players[0].targets[0];var first=LabJigwiABFireMotion.Position(b,f,0,31);
            Assert.That(LabJigwiABFireMotion.Position(b,f,0,31),Is.EqualTo(first));Assert.That(LabJigwiABFireMotion.Position(b,f,0,32),Is.Not.EqualTo(first));
            for(int i=0;i<100;i++){var p=LabJigwiABFireMotion.Position(b,f,0,31+i*.1);Assert.That(p.x,Is.InRange(.1f,.9f));Assert.That(p.y,Is.InRange(.48f,.86f));}
        }
    }
}
