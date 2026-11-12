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
        [Test] public void SavedConfigKeepsTheTestOnlySchedulesOff()
        {
            // The A-only / B-only recording switches replace the whole Jigwi timetable. They are for local runs only;
            // a saved ON value ships a stage with no smoke, bucket or common gimmicks.
            var saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.JigwiATestOnly, Is.False, "Turn jigwiATestOnly off before committing LabConfig.asset.");
            Assert.That(saved.JigwiBTestOnly, Is.False, "Turn jigwiBTestOnly off before committing LabConfig.asset.");
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
            // #540: a talisman orb has no lifetime (it never runs out) and never burns.
            Assert.That(water.All(o=>System.Math.Abs(o.expiresAt-(o.createdAt+LabModel.JigwiTalismanOrbLifetimeSeconds))<1e-9&&o.jigwiABBurnAt==-1&&o.jigwiABRewardPaintScale==1.5f),Is.True);
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
            Assert.That(water.expiresAt-water.createdAt,Is.GreaterThanOrEqualTo(LabModel.JigwiTalismanOrbLifetimeSeconds),"#540: a talisman orb has no lifetime.");
            Assert.That(water.jigwiABBurnAt,Is.LessThan(0d),"#540: a talisman orb never burns.");
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
        [Test] public void FlowScheduleRunsOneAFirstThenBABOverCommonGimmicksEvery15Seconds()
        {
            // #540: like Yaguang — the first 40 s carry one unique gimmick (A1), the middle 60 s overlay B-A-B on the
            // common gimmicks (40, 55, 70, 85), the last 20 s are fever.
            JsonUtility.FromJsonOverwrite("{\"jigwiFlowSchedule\":true,\"jigwiABPersonalDurationSeconds\":10}",config);
            var m=Room();
            var next=typeof(LabModel).GetMethod("NextJigwiABCommonAt",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(Enumerable.Range(0,5).Select(i=>(double)next.Invoke(m,new object[]{(uint)i})),
                Is.EqualTo(new[]{40d,55d,70d,85d,double.PositiveInfinity}));
            for(int t=1;t<=99;t++){m.Tick(t);var mid=m.Snapshot(t);
                Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True,"Valid at "+t+" s");}
            var s=m.Snapshot(99);var r=s.jigwiAB.rounds.OrderBy(x=>x.slot).ToArray();
            Assert.That(s.jigwiAB.schedule,Is.EqualTo(LabJigwiABTimeline.FlowSchedule));
            Assert.That(r.Select(x=>x.activeAt),Is.EqualTo(new[]{10d,45d,66d,83d}));
            Assert.That(r.Select(x=>x.kind),Is.EqualTo(new[]{LabJigwiABKind.PersonalBurn,LabJigwiABKind.GroupFire,
                LabJigwiABKind.PersonalBurn,LabJigwiABKind.GroupFire}));
            Assert.That(r[0].endsAt,Is.EqualTo(20d));Assert.That(r[2].endsAt,Is.EqualTo(76d));
            Assert.That(r[1].endsAt,Is.EqualTo(60d));Assert.That(r[3].endsAt,Is.EqualTo(98d));
            Assert.That(r.Count(x=>x.activeAt<40d),Is.EqualTo(1),"Only one unique gimmick in the first 40 s.");
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
            // #663: the burn now starts 0.6 s after the end, so this is read just before it (time only moves forward).
            Assert.That(m.Snapshot(20.5).orbs.Single(o=>o.id==theirs.id).jigwiABBurnAt,Is.EqualTo(-1),"The finished board keeps its orbs.");
            // #540: when Jigwi's line is over, every orb on the board burns away together in 1 s.
            double gone=20+LabModel.JigwiTalismanFailFirstBurnSeconds+LabModel.JigwiTalismanFailBlazeSeconds+.01;
            var later=m.Snapshot(gone);
            Assert.That(later.orbs.Any(o=>o.id==mine.id||o.id==mine2.id),Is.False,"The unfinished board's orbs burned.");
            Assert.That(later.orbs.Any(o=>o.id==theirs.id),Is.True,"…while the finished board still keeps its own.");
            // The clock has moved on to the end of the burn, so the drum is tried after that (an earlier time is rejected as INVALID_TIME).
            Assert.That(m.TryGenerate(Players[0],Request(),gone+.1,out _,out e),Is.True,"#540: a failed talisman never blocks the drums. "+e);
        }
        [Test] public void AFailedTalismanBurnsTheBoardSoonAfterTheFireballLands()
        {
            // #663: no more waiting 3.7 s for a voice line that no longer exists; the Host setting moves the moment.
            Assert.That(LabModel.JigwiTalismanFailFirstBurnSeconds,Is.LessThan(1d),"Just after the fireball lands (0.45 s).");
            Assert.That(config.JigwiAFailBurnStartSeconds,Is.EqualTo((float)LabModel.JigwiTalismanFailFirstBurnSeconds).Within(1e-6f));
            JsonUtility.FromJsonOverwrite("{\"jigwiAHoldBurnUntilEnd\":true,\"jigwiAFailBurnStartSeconds\":2}",config);
            var m=Room();var a=Current(m,5);var mine=Add(m,Players[0],4);
            Assert.That(m.Snapshot(20).jigwiAB.rounds.Single(r=>r.id==a.id).resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(m.Snapshot(21.99).orbs.Single(o=>o.id==mine.id).jigwiABBurnAt,Is.EqualTo(-1),"Not before the set moment.");
            Assert.That(m.Snapshot(22.01).orbs.Single(o=>o.id==mine.id).jigwiABBurnAt,
                Is.EqualTo(22+LabModel.JigwiTalismanFailBlazeSeconds).Within(1e-6),"Lit 2 s after the end, as set.");
        }
        [Test] public void HoldingTheBurnKeepsEveryOrbUntilTheARunsOut()
        {
            // #540: while the talisman is being written the orbs are untouched (only the fireball grows);
            // an A that runs out then burns every orb left on the unfinished board.
            JsonUtility.FromJsonOverwrite("{\"jigwiAHoldBurnUntilEnd\":true}",config);
            var m=Room();var a=Current(m,5);var mine=Add(m,Players[0],4);
            Assert.That(m.Snapshot(6).orbs.Single(o=>o.id==mine.id).jigwiABBurnAt,Is.EqualTo(-1),"Nothing burns while writing.");
            Assert.That(m.TryGenerate(Players[0],Request(),19,out var fresh,out var e),Is.True,e);
            Assert.That(m.Snapshot(19.5).orbs.Single(o=>o.id==fresh.id).jigwiABBurnAt,Is.EqualTo(-1),"A new orb is safe too.");
            Assert.That(m.Snapshot(20).jigwiAB.rounds.Single(r=>r.id==a.id).resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            // #540: once Jigwi's "모두 재가 되어라!" is over, every orb then on the board — even one made while Jigwi spoke —
            // catches fire at once and is gone 1 s later. An orb made after that moment is safe.
            double landed=20+LabModel.JigwiTalismanFailFirstBurnSeconds, blaze=LabModel.JigwiTalismanFailBlazeSeconds;
            // #663: the board now catches fire just after the fireball lands, so "during" is made in that short gap.
            Assert.That(m.TryGenerate(Players[0],Request(),20+LabModel.JigwiTalismanFailFirstBurnSeconds*.5,out var during,out e),Is.True,e);
            var speaking=m.Snapshot(landed-.01).orbs.Where(o=>o.id==mine.id||o.id==fresh.id||o.id==during.id).ToArray();
            Assert.That(speaking,Has.Length.EqualTo(3),"Nothing burns while Jigwi speaks.");
            Assert.That(speaking.All(o=>o.jigwiABBurnAt==-1),Is.True,"Not even marked yet.");
            var lit=m.Snapshot(landed+.01).orbs.Where(o=>o.id==mine.id||o.id==fresh.id||o.id==during.id).ToArray();
            Assert.That(lit,Has.Length.EqualTo(3));
            Assert.That(lit.All(o=>System.Math.Abs(o.jigwiABBurnAt-(landed+blaze))<1e-6),Is.True,"They all catch fire together.");
            Assert.That(m.Snapshot(landed+blaze+.01).orbs.Any(o=>o.id==mine.id||o.id==fresh.id||o.id==during.id),Is.False,"…and are gone together.");
            Assert.That(m.TryGenerate(Players[0],Request(),landed+blaze+.1,out var after,out e),Is.True,e);
            Assert.That(m.Snapshot(landed+blaze+.2).orbs.Single(o=>o.id==after.id).jigwiABBurnAt,Is.EqualTo(-1),"A new orb after the burn is safe.");
        }
        [Test] public void WritingPausesTheLifetimeOfOrbsAlreadyOnThatBoard()
        {
            // #540: the A runs 5 → 20. Orbs already on a writing board stop ageing; a finished seat gets the unused part back.
            JsonUtility.FromJsonOverwrite("{\"jigwiAHoldBurnUntilEnd\":true}",config);
            var m=Room();
            var mine=Add(m,Players[0],4);mine.expiresAt=8;var theirs=Add(m,Players[1],4);theirs.expiresAt=8;
            var a=Current(m,5);
            Assert.That(m.Snapshot(9).orbs.Single(o=>o.id==mine.id).expiresAt,Is.EqualTo(23),"Paused for the whole A while writing.");
            Assert.That(m.TryCompleteJigwiTalisman(Players[1],a.id,10),Is.True);
            Assert.That(m.Snapshot(10).orbs.Single(o=>o.id==theirs.id).expiresAt,Is.EqualTo(13),"Paused only while that seat wrote (5 → 10).");
            Assert.That(m.Snapshot(12).orbs.Any(o=>o.id==mine.id),Is.True,"Still on the board after its old lifetime.");
        }
        [Test] public void AThrownTalismanOrbFliesAsThreeAndEachShardPaints()
        {
            // #540: the talisman orb leaves with two Water shards beside it; each shard that reaches the gourd paints once.
            var m=Room();var a=Current(m,5);
            Assert.That(m.TryCompleteJigwiTalisman(Players[0],a.id,6),Is.True);
            var water=m.Snapshot(6+config.JigwiTalismanRewardDelaySeconds).orbs.First(o=>o.jigwiTalismanReward);
            string left=LabModel.JigwiTalismanShardId(water.id,1),right=LabModel.JigwiTalismanShardId(water.id,2);
            Assert.That(m.ConfirmJigwiTalismanShardHit(left,6.9,Vector3.forward),Is.False,"No shard before the throw.");
            Launch(m,Orbs(m)[water.id],7);
            Assert.That(LabModel.IsJigwiTalismanShard(left),Is.True);Assert.That(LabModel.IsJigwiTalismanShard(water.id),Is.False);
            int before=m.PaintSurface.CoveredCount;
            Assert.That(m.ConfirmJigwiTalismanShardHit(left,7.2,Vector3.forward),Is.True);
            Assert.That(m.PaintSurface.CoveredCount,Is.GreaterThan(before),"A shard paints Water.");
            Assert.That(m.LastHitTalisman,Is.True);
            Assert.That(m.ConfirmJigwiTalismanShardHit(left,7.3,Vector3.forward),Is.False,"Each shard paints once.");
            Assert.That(m.DropJigwiTalismanShard(right),Is.True);
            Assert.That(m.ConfirmJigwiTalismanShardHit(right,7.3,Vector3.right),Is.False,"A missed shard paints nothing.");
        }
        [Test] public void OrbsAboutToRunOutWaitUntilTheTalismanOrbHasLanded()
        {
            // #540: none of the writer's orbs runs out while the talisman turns into its orb and for a moment after it lands.
            var m=Room();
            var soon=Add(m,Players[0],4);soon.expiresAt=6.2;var later=Add(m,Players[0],4);later.expiresAt=30;
            var theirs=Add(m,Players[1],4);theirs.expiresAt=6.2;
            var a=Current(m,5);
            Assert.That(m.TryCompleteJigwiTalisman(Players[0],a.id,6),Is.True);
            double until=6+config.JigwiTalismanRewardDelaySeconds+LabModel.JigwiTalismanCalmSeconds;
            var s=m.Snapshot(6);
            Assert.That(s.orbs.Single(o=>o.id==soon.id).expiresAt,Is.EqualTo(until).Within(1e-6),"Held until the reward has settled.");
            Assert.That(s.orbs.Single(o=>o.id==later.id).expiresAt,Is.EqualTo(30),"A longer lifetime is untouched.");
            Assert.That(s.orbs.Single(o=>o.id==theirs.id).expiresAt,Is.EqualTo(6.2).Within(1e-6),"Only the writer's board.");
            var water=m.Snapshot(6+config.JigwiTalismanRewardDelaySeconds).orbs.First(o=>o.jigwiTalismanReward);
            Assert.That(new Vector2(water.x,water.y),Is.EqualTo(LabModel.JigwiTalismanWaterSpot(0)),"It appears where the folded talisman drops.");
        }
        [Test] public void BCountdownStartsOnlyWhileEveryPickedSeatHoldsItsSpecialWater()
        {
            // #551: both picked seats hold their special Water → 3-2-1; letting go before "지금!" stops it; no throw before.
            JsonUtility.FromJsonOverwrite("{\"jigwiBCountdown\":true}",config);
            var m=Room();var b=Current(m,31);
            var specials=m.Snapshot(31).orbs.Where(o=>o.jigwiABSupplyEventId==b.id).ToArray();
            Assert.That(specials,Has.Length.EqualTo(2));
            Assert.That(m.TryLaunch(specials[0].owner,specials[0].id,Request(),31.2,out _,out var reason),Is.False);
            Assert.That(reason,Is.EqualTo("JIGWI_B_WAIT"),"No throw before the countdown.");
            Assert.That(m.TrySetJigwiBReady(Players[0],b.id,true,31.3),Is.True);
            Assert.That(B(m,b.id,31.3).countdownAt,Is.EqualTo(-1d),"Not everyone holds yet.");
            Assert.That(m.TrySetJigwiBReady(Players[1],b.id,true,31.4),Is.True);
            Assert.That(B(m,b.id,31.4).countdownAt,Is.EqualTo(31.4).Within(1e-9));
            Assert.That(m.TryLaunch(specials[0].owner,specials[0].id,Request(),31.5,out _,out reason),Is.False,"Still counting.");
            Assert.That(m.TrySetJigwiBReady(Players[0],b.id,false,31.6),Is.True);
            Assert.That(B(m,b.id,31.6).countdownAt,Is.EqualTo(-1d),"Letting go before 지금 stops the 3-2-1.");
            Assert.That(m.TrySetJigwiBReady(Players[0],b.id,true,32),Is.True);
            double go=32+LabJigwiABTimeline.CountdownSeconds;
            foreach(var special in specials) Launch(m,Orbs(m)[special.id],go+.1);
            Assert.That(B(m,b.id,go+.1).players.All(p=>p.launchedAt>=0d),Is.True);
            Assert.That(LabJigwiABNetworkValidation.Valid(m.Snapshot(go+.1).jigwiAB,m.Snapshot(go+.1).players,m.Revision),Is.True);
            // With the countdown the second hit may land up to 1.5 s after the first.
            m.ConfirmHit(specials[0].id,go+.4,Vector3.back,out _,out _,out _);
            m.ConfirmHit(specials[1].id,go+1.7,Vector3.back,out _,out _,out _);
            Assert.That(B(m,b.id,go+1.7).resolution,Is.EqualTo(LabJigwiABResolution.Completed));
        }
        [Test] public void BFailsWhenAPickedSeatStillHoldsAfterTheReleaseMoment()
        {
            // #551: "지금!" then everyone must let go within 0.7 s; one late seat loses the single chance.
            JsonUtility.FromJsonOverwrite("{\"jigwiBCountdown\":true}",config);
            var m=Room();var b=Current(m,31);
            var specials=m.Snapshot(31).orbs.Where(o=>o.jigwiABSupplyEventId==b.id).ToArray();
            Assert.That(m.TrySetJigwiBReady(Players[0],b.id,true,32),Is.True);
            Assert.That(m.TrySetJigwiBReady(Players[1],b.id,true,32),Is.True);
            double go=32+LabJigwiABTimeline.CountdownSeconds;
            Launch(m,Orbs(m)[specials[0].id],go+.1);
            Assert.That(B(m,b.id,go+LabJigwiABTimeline.ReleaseSeconds-.01).resolution,Is.EqualTo(LabJigwiABResolution.None));
            var late=B(m,b.id,go+LabJigwiABTimeline.ReleaseSeconds+.01);
            Assert.That(late.resolution,Is.EqualTo(LabJigwiABResolution.Expired));
        }
        [Test] public void BTestOnlyRunsJustBFromFourSecondsAndStillValidates()
        {
            // #551 시험용: B만 4초부터 17초마다, A와 공통 기믹 없음.
            JsonUtility.FromJsonOverwrite("{\"jigwiBTestOnly\":true}",config);
            var m=Room();
            Assert.That(Current(m,5).kind,Is.EqualTo(LabJigwiABKind.GroupFire));
            var s=m.Snapshot(5);
            Assert.That(s.jigwiAB.schedule,Is.EqualTo(LabJigwiABTimeline.BTestSchedule));
            Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players,m.Revision),Is.True);
            Assert.That(Current(m,22).kind,Is.EqualTo(LabJigwiABKind.GroupFire),"The next one is B again.");
            var failed=m.Snapshot(22);
            Assert.That(LabJigwiABNetworkValidation.Valid(failed.jigwiAB,failed.players,m.Revision),Is.True,"A failed B with no board fire still validates.");
        }
        [Test] public void ATestOnlyRunsJustTheSharedTalismanFromFourSeconds()
        {
            // #595 시험용: 부적만 4초부터 17초마다, 연기와 공통 기믹 없음.
            JsonUtility.FromJsonOverwrite("{\"jigwiATestOnly\":true,\"jigwiSmokeFlow\":true,\"jigwiABPersonalDurationSeconds\":10}",config);
            var m=Room3();
            var a=Current(m,4.1);
            Assert.That(a.kind,Is.EqualTo(LabJigwiABKind.PersonalBurn));Assert.That(a.common,Is.True);
            var next=typeof(LabModel).GetMethod("NextJigwiABCommonAt",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That((double)next.Invoke(m,new object[]{0u}),Is.EqualTo(double.PositiveInfinity),"No common gimmick.");
            for(int t=5;t<=99;t++){m.Tick(t);var mid=m.Snapshot(t);
                Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True,"Valid at "+t+" s");}
            var r=m.Snapshot(99).jigwiAB.rounds.OrderBy(x=>x.slot).ToArray();
            Assert.That(r.Select(x=>x.activeAt),Is.EqualTo(new[]{4d,21d,38d,55d,72d}));
            Assert.That(r.All(x=>x.kind==LabJigwiABKind.PersonalBurn&&x.common),Is.True,"Only the shared talisman.");
        }
        [Test] public void SootShardsAreNorthWestSouthEastPiecesThatOnlyJoinTheirNeighbours()
        {
            // 1 북 / 2 서 / 4 남 / 8 동: 이웃끼리만 붙는다.
            Assert.That(LabModel.SootPiecesFit(1, 2), Is.True, "north + west");
            Assert.That(LabModel.SootPiecesFit(8, 1), Is.True, "east + north");
            Assert.That(LabModel.SootPiecesFit(1, 4), Is.False, "north + south leave a bow tie");
            Assert.That(LabModel.SootPiecesFit(2, 8), Is.False, "west + east");
            Assert.That(LabModel.SootPiecesFit(1 | 2, 4), Is.True);
            Assert.That(LabModel.SootPiecesFit(1 | 2, 4 | 8), Is.True, "two halves make the whole orb");
            Assert.That(LabModel.SootPiecesFit(1 | 2, 2 | 4), Is.False, "the same piece twice");
            Assert.That(LabModel.SootArcStart(8 | 1), Is.EqualTo(3));
            Assert.That(LabModel.SootArcStart(1 | 4), Is.EqualTo(-1));
            // 북 조각은 위쪽, 동 조각은 오른쪽에 놓인다.
            Assert.That(LabModel.JigwiSootShardSpot(0).y, Is.GreaterThan(LabModel.JigwiSootShardSpot(2).y));
            Assert.That(LabModel.JigwiSootShardSpot(3).x, Is.GreaterThan(LabModel.JigwiSootShardSpot(1).x));

            Texture2D Art(int pieces) => LabSootShardArt.SpriteFor(pieces).texture;
            float Opaque(int pieces) => Art(pieces).GetPixels().Count(c => c.a > .5f) / (float)(Art(pieces).width * Art(pieces).height);
            float quarter = Opaque(1), half = Opaque(1 | 2), threeQuarters = Opaque(1 | 2 | 4);
            Assert.That(half, Is.EqualTo(quarter * 2f).Within(quarter * .35f));
            Assert.That(threeQuarters, Is.EqualTo(quarter * 3f).Within(quarter * .5f));
            // 북 조각은 그림 위쪽 가운데가 칠해지고 아래쪽 가운데는 비어 있다.
            Texture2D north = Art(1);
            Assert.That(north.GetPixel(north.width / 2, north.height * 7 / 8).a, Is.GreaterThan(.5f));
            Assert.That(north.GetPixel(north.width / 2, north.height / 8).a, Is.LessThan(.5f));
            Assert.That(LabFiveColorSparkle.SootTones.All(c => c.r < 100 && c.g < 100 && c.b < 140), "the black orb stays black");
        }

        [Test] public void ClearingTheSmokeGivesFourSootShardsThatMergeIntoTheBlackOrb()
        {
            // #696: 연기를 다 걷어 낸 사람 판에 검은 파편 4개 → 둘씩 합쳐 2, 2 → 검정 구슬(오색구슬처럼 넓게, 먹색).
            JsonUtility.FromJsonOverwrite("{\"jigwiSmokeFlow\":true,\"jigwiDenseTimeline\":true,\"jigwiABPersonalDurationSeconds\":10,\"jigwiSmokeToTalismanSeconds\":0}",config);
            var m=Room3();
            var smoke=Current(m,8.1);
            Assert.That(smoke.smoke,Is.True);
            double ready=smoke.players.First().readyAt;
            Assert.That(m.TryClearJigwiSmoke(Three[0],smoke.id,ready+.5),Is.False,"Nobody rubs a whole screen that fast.");
            Assert.That(m.TryClearJigwiSmoke(Three[0],"other",ready+2),Is.False);
            Assert.That(m.TryClearJigwiSmoke(Three[0],smoke.id,ready+2),Is.True);
            Assert.That(m.TryClearJigwiSmoke(Three[0],smoke.id,ready+2.5),Is.False,"Once per smoke.");
            var shards=m.Snapshot(ready+2).orbs.Where(o=>o.kind==LabOrbKind.SootShard).ToArray();
            Assert.That(shards.Length,Is.EqualTo(LabModel.JigwiSootShardCount));
            Assert.That(shards.All(o=>o.owner==Three[0]&&o.element==LabOrbElement.None),Is.True);
            Assert.That(shards.Select(o=>o.sootPieces).OrderBy(v=>v),Is.EqualTo(new[]{1,2,4,8}),"North, west, south, east.");
            shards=shards.OrderBy(o=>o.sootPieces).ToArray(); // 0 북, 1 서, 2 남, 3 동
            Assert.That(m.TryLaunch(Three[0],shards[0].id,Request(),ready+2.1,out _,out var why),Is.False,"A shard cannot be thrown.");

            double t=ready+3;
            Assert.That(m.TryCombineAt(Three[0],shards[0].id,shards[2].id,Request(),t,new Vector2(.5f,.5f),Vector2.zero,out _,out var e0),Is.False);
            Assert.That(e0,Is.EqualTo("SOOT_MISFIT"),"North and south do not touch.");
            Assert.That(m.TryCombineAt(Three[0],shards[0].id,shards[1].id,Request(),t,new Vector2(.5f,.5f),Vector2.zero,out var a,out var e),Is.True,e);
            Assert.That(e,Is.EqualTo("SOOT_MERGED")); Assert.That(a.kind,Is.EqualTo(LabOrbKind.SootShard)); Assert.That(a.sootPieces,Is.EqualTo(1|2));
            Assert.That(m.TryCombineAt(Three[0],shards[2].id,shards[3].id,Request(),t,new Vector2(.4f,.5f),Vector2.zero,out var b,out e),Is.True,e);
            Assert.That(m.TryCombineAt(Three[0],a.id,b.id,Request(),t+.1,new Vector2(.45f,.5f),Vector2.zero,out var black,out e),Is.True,e);
            Assert.That(e,Is.EqualTo("SOOT_ORB"));
            Assert.That(black.kind,Is.EqualTo(LabOrbKind.Combined)); Assert.That(black.soot&&black.fiveColor,Is.True);
            Assert.That(black.sootPieces,Is.Zero); Assert.That(black.element,Is.EqualTo(LabOrbElement.None),"No element: combos ignore it.");
            Assert.That(m.Snapshot(t+.2).orbs.Count(o=>o.kind==LabOrbKind.SootShard),Is.Zero);

            // 다른 사람 연기도 따로, 음/양과는 합쳐지지 않는다.
            Assert.That(m.TryClearJigwiSmoke(Three[1],smoke.id,ready+4),Is.True);
            Launch(m,black,t+.5);
            Assert.That(m.ConfirmHit(black.id,t+.6),Is.True);
            Assert.That(m.LastHitFiveColor,Is.True,"Hits as wide as a five-color orb (in ink).");
        }

        [Test] public void DenseSmokeFlowRunsSixMissionsWithShortGaps()
        {
            // #696: 연기 8 / 부적 24 / 연기 40 / 부적 55 / 연기 70 / 부적 85 (부적 10초, 연기 15초), 정해진 시각 그대로 확인.
            JsonUtility.FromJsonOverwrite("{\"jigwiSmokeFlow\":true,\"jigwiDenseTimeline\":true,\"jigwiCommonStartSeconds\":25,\"jigwiABPersonalDurationSeconds\":10,\"jigwiSmokeToTalismanSeconds\":0}",config);
            var m=Room3();
            Assert.That(m.Snapshot(0).jigwiAB.schedule,Is.EqualTo(LabJigwiABTimeline.DenseSmokeFlowSchedule));
            for(int t=1;t<=99;t++){m.Tick(t);var mid=m.Snapshot(t);
                Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True,"Valid at "+t+" s");}
            var r=m.Snapshot(99).jigwiAB.rounds.OrderBy(x=>x.slot).ToArray();
            Assert.That(r.Select(x=>x.activeAt),Is.EqualTo(new[]{8d,24d,40d,55d,70d,85d}));
            Assert.That(r.Select(x=>x.smoke),Is.EqualTo(new[]{true,false,true,false,true,false}),"Smoke first, then the talisman, three times.");
            Assert.That(r.Select(x=>x.common),Is.EqualTo(new[]{false,true,false,true,false,true}));
            for(int i=1;i<r.Length;i++)
                Assert.That(r[i].activeAt-r[i-1].endsAt,Is.InRange(0d,6d),"No gap longer than 6 s between missions ("+i+").");
            Assert.That(r.Last().endsAt,Is.LessThanOrEqualTo(r.Last().activeAt+10d+1e-9));
        }

        [Test] public void SmokeFlowRunsSmokeThenSharedTalismanAndSmokesEveryScreenAtOnce()
        {
            // #595: 연기 10 / 부적 45 / 연기 66 / 부적 83, 공통 기믹은 25초부터 15초마다.
            // #663: this checks the fixed timetable, so the talisman is not pulled forward after the smoke.
            JsonUtility.FromJsonOverwrite("{\"jigwiSmokeFlow\":true,\"jigwiCommonStartSeconds\":25,\"jigwiABPersonalDurationSeconds\":10,\"jigwiSmokeToTalismanSeconds\":0}",config);
            var m=Room3();
            var next=typeof(LabModel).GetMethod("NextJigwiABCommonAt",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(Enumerable.Range(0,6).Select(i=>(double)next.Invoke(m,new object[]{(uint)i})),
                Is.EqualTo(new[]{25d,40d,55d,70d,85d,double.PositiveInfinity}));
            var smoke=Current(m,10.1);
            Assert.That(smoke.kind,Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(smoke.smoke&&smoke.bucket,Is.True);
            Assert.That(smoke.players.Select(p=>p.playerId),Is.EquivalentTo(Three),"Everyone's screen smokes.");
            Assert.That(smoke.players.Select(p=>p.readyAt).Distinct().Single(),
                Is.EqualTo(10d+LabJigwiABTimeline.SmokeLeadSeconds).Within(1e-9),"All at the same moment.");
            Assert.That(m.Snapshot(10.1).orbs.Any(o=>o.jigwiABSupplyEventId==smoke.id),Is.False,"No special Water.");
            Assert.That(m.TryJigwiBPull(Three[0],smoke.id,12),Is.False,"No bucket to pour.");
            for(int t=13;t<=99;t++){m.Tick(t);var mid=m.Snapshot(t);
                Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True,"Valid at "+t+" s");}
            var r=m.Snapshot(99).jigwiAB.rounds.OrderBy(x=>x.slot).ToArray();
            Assert.That(r.Select(x=>x.activeAt),Is.EqualTo(new[]{10d,45d,66d,83d}));
            Assert.That(r.Select(x=>x.kind),Is.EqualTo(new[]{LabJigwiABKind.GroupFire,LabJigwiABKind.PersonalBurn,
                LabJigwiABKind.GroupFire,LabJigwiABKind.PersonalBurn}));
            Assert.That(r.Select(x=>x.smoke),Is.EqualTo(new[]{true,false,true,false}));
            Assert.That(r.Select(x=>x.common),Is.EqualTo(new[]{false,true,false,true}));
            Assert.That(r[0].resolution,Is.EqualTo(LabJigwiABResolution.Expired),"Smoke just ends; nobody pours.");
            Assert.That(r[0].boardFireUntil,Is.EqualTo(-1d),"Smoke never sets the boards on fire.");
        }
        [Test] public void AFinishedSmokeBringsTheTalismanSoonAndPushesTheCommonGimmickPastIt()
        {
            // #663: no empty stretch between the smoke and the talisman. The smoke (10 s) times out at 25 s, so the talisman
            // starts 4 s later (warning at 27 s) instead of at 45 s; the common gimmick due meanwhile waits until it is over.
            JsonUtility.FromJsonOverwrite("{\"jigwiSmokeFlow\":true,\"jigwiCommonStartSeconds\":30,\"jigwiABPersonalDurationSeconds\":10,\"jigwiSmokeToTalismanSeconds\":4}",config);
            var m=Room3();
            Assert.That(Current(m,10.1).smoke,Is.True);
            m.Tick(26.9);
            Assert.That(m.Snapshot(26.9).jigwiAB.rounds.Count(x=>x.kind==LabJigwiABKind.PersonalBurn),Is.Zero,"Not before its warning.");
            var early=m.Snapshot(27.1);
            var talisman=early.jigwiAB.rounds.Single(x=>x.kind==LabJigwiABKind.PersonalBurn);
            Assert.That(talisman.activeAt,Is.EqualTo(29d).Within(1e-9),"Smoke ended at 25 s + 4 s.");
            Assert.That(talisman.warningAt,Is.EqualTo(27d).Within(1e-9));
            Assert.That(talisman.endsAt,Is.EqualTo(39d).Within(1e-9),"Still the usual 10 s.");
            Assert.That(LabJigwiABNetworkValidation.Valid(early.jigwiAB,early.players,m.Revision),Is.True,"Other screens accept the earlier talisman.");
            var nextCommon=(double)typeof(LabModel).GetField("nextCommonGimmickAt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(m);
            Assert.That(nextCommon,Is.EqualTo(40d).Within(1e-9),"The common gimmick due at 30 s waits until 1 s after the talisman.");
            for(int t=28;t<=99;t++){m.Tick(t);var mid=m.Snapshot(t);
                Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True,"Valid at "+t+" s");}
            var r=m.Snapshot(99).jigwiAB.rounds.OrderBy(x=>x.slot).ToArray();
            Assert.That(r.Select(x=>x.activeAt).Take(3),Is.EqualTo(new[]{10d,29d,66d}),"The next smoke keeps its own time.");
            Assert.That(r[3].activeAt,Is.EqualTo(83d).Within(1e-9),"The second smoke times out at 81 s; 85 s would be later than 83 s.");
        }
        LabModel SmokeFlowRoom3()
        {
            // #663: these talisman fixtures read the talisman at its fixed 45 s.
            JsonUtility.FromJsonOverwrite("{\"jigwiSmokeFlow\":true,\"jigwiABPersonalDurationSeconds\":10,\"jigwiAHoldBurnUntilEnd\":true,\"jigwiTalismanWaterCount\":1,\"jigwiSmokeToTalismanSeconds\":0}",config);
            return Room3();
        }
        [Test] public void SharedTalismanPoursTheWaterfallOnlyWhenAllHaveWrittenIt()
        {
            // #595: 공용 부적 — 모두 써야 모두 보상.
            var m=SmokeFlowRoom3();var a=Current(m,45.1);
            Assert.That(a.kind,Is.EqualTo(LabJigwiABKind.PersonalBurn));Assert.That(a.common,Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Three[0],a.id,46),Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Three[1],a.id,47),Is.True);
            var waiting=m.Snapshot(52);
            Assert.That(waiting.orbs.Any(o=>o.jigwiTalismanReward),Is.False,"Nobody is rewarded until everyone has written it.");
            Assert.That(waiting.jigwiAB.rounds.Single(r=>r.id==a.id).resolution,Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(LabJigwiABNetworkValidation.Valid(waiting.jigwiAB,waiting.players,m.Revision),Is.True);
            int painted=m.PaintSurface.PaintCount;
            Assert.That(m.TryCompleteJigwiTalisman(Three[2],a.id,52.5),Is.True);
            var done=m.Snapshot(52.5);
            Assert.That(done.jigwiAB.rounds.Single(r=>r.id==a.id).resolution,Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(LabJigwiABNetworkValidation.Valid(done.jigwiAB,done.players,m.Revision),Is.True);
            // #610: the waterfall paints when it lands — after the big talisman's melody and the small talismans' flight.
            double lands=52.5+LabJigwiABTimeline.CommonTalismanRevealSeconds+LabJigwiABTimeline.CommonTalismanPourSeconds;
            m.Tick(lands-.05);
            Assert.That(m.PaintSurface.PaintCount,Is.EqualTo(painted),"Nothing is painted while the melody plays and the talismans fly.");
            m.Tick(lands);
            Assert.That(m.PaintSurface.PaintCount,Is.GreaterThan(painted),"The waterfall paints the boss when it lands.");
            Assert.That(m.Snapshot(lands+.1).orbs.Any(o=>o.jigwiTalismanReward),Is.False,"#595: no talisman orbs, the waterfall is the reward.");
        }
        [Test] public void SharedTalismanBurnsEveryBoardWhenOneSeatMissesIt()
        {
            // #595: 한 명이라도 못 쓰면 이미 쓴 사람의 판도 탄다.
            var m=SmokeFlowRoom3();var a=Current(m,45.1);
            Assert.That(m.TryCompleteJigwiTalisman(Three[0],a.id,46),Is.True);
            Assert.That(m.TryCompleteJigwiTalisman(Three[1],a.id,47),Is.True);
            var keep=Add(m,Three[0],54);
            var end=m.Snapshot(55.01);var round=end.jigwiAB.rounds.Single(r=>r.id==a.id);
            Assert.That(round.resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(Three.All(round.TalismanFailed),Is.True,"Every seat failed together.");
            Assert.That(LabJigwiABNetworkValidation.Valid(end.jigwiAB,end.players,m.Revision),Is.True);
            double lit=55+LabModel.JigwiTalismanFailFirstBurnSeconds+.01;
            Assert.That(m.Snapshot(lit).orbs.Single(o=>o.id==keep.id).jigwiABBurnAt,
                Is.EqualTo(55+LabModel.JigwiTalismanFailFirstBurnSeconds+LabModel.JigwiTalismanFailBlazeSeconds).Within(1e-6),
                "A seat that wrote its talisman still burns.");
            Assert.That(m.Snapshot(lit).orbs.Any(o=>o.jigwiTalismanReward),Is.False,"No reward.");
        }
        LabJigwiABRound B(LabModel m,string id,double at)=>m.Snapshot(at).jigwiAB.rounds.Single(r=>r.id==id);
        [Test] public void BBucketArrivesSeatBySeatAndEachPourPaintsAtOnce()
        {
            // #551 나무 물통 (개별, 여우구슬처럼): every seat gets its own bucket at its own time; no special Water, no 3-2-1.
            JsonUtility.FromJsonOverwrite("{\"jigwiBBucket\":true}",config);
            var m=Room3();var b=Current(m,25.1);
            Assert.That(b.bucket,Is.True);
            Assert.That(b.players.Select(p=>p.playerId),Is.EquivalentTo(Three),"Everyone has a bucket.");
            Assert.That(b.countdownAt,Is.EqualTo(-1d),"No 3-2-1.");
            Assert.That(b.players.Select(p=>p.readyAt).OrderBy(x=>x),
                Is.EqualTo(new[]{0d,1d,2d}.Select(k=>b.activeAt+LabJigwiABTimeline.BucketLeadSeconds+k*LabJigwiABTimeline.BucketStaggerSeconds).ToArray()).Within(1e-9),
                "One bucket after another.");
            Assert.That(m.Snapshot(25.1).orbs.Any(o=>o.jigwiABSupplyEventId==b.id),Is.False,"No special Water orb.");
            Assert.That(m.TrySetJigwiBReady(Three[0],b.id,true,25.2),Is.False,"Holding means nothing here.");
            int painted=m.PaintSurface.PaintCount;
            var first=b.players.OrderBy(p=>p.readyAt).First();
            Assert.That(m.TryJigwiBPull(first.playerId,b.id,first.readyAt+1),Is.True);
            Assert.That(m.PaintSurface.PaintCount,Is.GreaterThan(painted),"My pour paints at once.");
            Assert.That(m.TryJigwiBPull(first.playerId,b.id,first.readyAt+1.5),Is.False,"One bucket each.");
            var mid=m.Snapshot(first.readyAt+1.5);
            Assert.That(mid.jigwiAB.rounds.Single(r=>r.id==b.id).resolution,Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(LabJigwiABNetworkValidation.Valid(mid.jigwiAB,mid.players,m.Revision),Is.True);
            // Check the last arrival after the earlier seat's pour: model timestamps must increase.
            var last=b.players.OrderBy(p=>p.readyAt).Last();
            Assert.That(m.TryJigwiBPull(last.playerId,b.id,last.readyAt-.1),Is.False,"Not before my bucket arrives.");
            double end=0;
            foreach(var seat in b.players.Where(p=>p.playerId!=first.playerId).OrderBy(p=>p.readyAt))
            { end=seat.readyAt+2;Assert.That(m.TryJigwiBPull(seat.playerId,b.id,end),Is.True); }
            var done=m.Snapshot(end);
            Assert.That(done.jigwiAB.rounds.Single(r=>r.id==b.id).resolution,Is.EqualTo(LabJigwiABResolution.Completed),"Everyone poured.");
            Assert.That(LabJigwiABNetworkValidation.Valid(done.jigwiAB,done.players,m.Revision),Is.True);
        }
        [Test] public void BBucketSeatThatRunsOutCannotPourAndBEndsUnfinished()
        {
            JsonUtility.FromJsonOverwrite("{\"jigwiBBucket\":true}",config);
            var m=Room3();var b=Current(m,25.1);
            var order=b.players.OrderBy(p=>p.readyAt).ToArray();
            Assert.That(m.TryJigwiBPull(order[0].playerId,b.id,order[0].readyAt+1),Is.True);
            Assert.That(m.TryJigwiBPull(order[1].playerId,b.id,order[1].readyAt+LabJigwiABTimeline.BucketSeatSeconds+.1),Is.False,"Its time ran out.");
            Assert.That(B(m,b.id,b.endsAt-.01).resolution,Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(B(m,b.id,b.endsAt+.01).resolution,Is.EqualTo(LabJigwiABResolution.Expired),"Not everyone poured.");
            Assert.That(m.JigwiBoardOnFire(b.endsAt+.5),Is.False,"Personal gimmick: a seat that did not pour never sets the others' boards on fire.");
            var end=m.Snapshot(b.endsAt+.01);
            Assert.That(LabJigwiABNetworkValidation.Valid(end.jigwiAB,end.players,m.Revision),Is.True);
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
            // The reference is unscaled Lab paint; this Story room has two actual participants.
            double expected=(reference.PaintSurface.Coverage+reference.PaintSurface.PendingCoverage)*(5d/2d);
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

        [Test]
        public void BFailureSetsEveryBoardOnFireForThreeSecondsAndKeepsOrbsStaminaAndStatistics()
        {
            // The saved smoke flow has no board fire; explicitly select the supported special-Water alternative.
            JsonUtility.FromJsonOverwrite("{\"jigwiFlowSchedule\":false,\"jigwiSmokeFlow\":false,\"jigwiBBucket\":false,\"jigwiBCountdown\":false,\"jigwiBBoardFireSeconds\":3}",config);
            // #509: B1 fails at 40, the same second as the common gimmick; keep this fixture to B's own result.
            var quiet=config.CaptureDeveloperValues();NoCommonGimmick(quiet);Apply(quiet);
            var m=Room();m.Tick(39.9d); // Finish A before injecting the ordinary board/flight fixtures.
            // DeadlineFailureRemovesOnlyThisMissionsSupplyAndKeepsBoardsAndStamina covers real B-token cleanup.
            // Omit only that ammunition here so the expiry assertion isolates ordinary orbs.
            var states=Orbs(m);
            foreach(var id in states.Values.Where(o=>!string.IsNullOrEmpty(o.jigwiABSupplyEventId)).Select(o=>o.id).ToArray())
                states.Remove(id);
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
