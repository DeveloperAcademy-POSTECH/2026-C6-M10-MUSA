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
        void FireHit(LabModel m,LabJigwiABRound g,ulong owner,string id,double at,LabOrbElement element=LabOrbElement.Water,bool expected=true)
        {
            var o=Add(m,owner,at,element:element);Launch(m,o,at);
            Assert.That(m.ConfirmJigwiABFireHit(o.id,g.id,id,at,out var off),Is.True);Assert.That(off,Is.EqualTo(expected));
            Assert.That(m.ConfirmJigwiABFireHit(o.id,g.id,id,at,out _),Is.False);
        }
        void Clear(LabModel m,LabJigwiABRound g,double at)
        {foreach(var p in g.players)foreach(var f in p.targets)FireHit(m,g,p.playerId,f.id,at);}

        [TestCase(LabStageYokai.None,false)] [TestCase(LabStageYokai.Gumiho,false)]
        [TestCase(LabStageYokai.Yaguang,false)] [TestCase(LabStageYokai.Jigwi,true)]
        public void ScopeAndRetiredSurfaceAreIndependent(LabStageYokai stage,bool expected)
        {
            var m=Room(stage);var s=m.Snapshot(11);Assert.That(s.jigwiAB.enabled,Is.EqualTo(expected));
            Assert.That(m.JigwiABEnabled,Is.EqualTo(expected));Assert.That(s.jigwiStage.enabled,Is.False);
            Assert.That(m.JigwiStagePaintBlocked(Players[0],Vector3.forward),Is.False);
        }
        [Test] public void AWarningHasNoBoostOrBurnUntilActive()
        {
            var m=Room();var a=Current(m,4);var o=Add(m,a.targetPlayer,4);
            Assert.That(a.Warning(4),Is.True);Assert.That(m.JigwiABRecoveryMultiplier(a.targetPlayer,4),Is.EqualTo(1));
            Assert.That(m.Snapshot(4.99).orbs.Single(x=>x.id==o.id).jigwiABBurnAt,Is.EqualTo(-1));
            Assert.That(m.Snapshot(5).orbs.Single(x=>x.id==o.id).jigwiABBurnAt,Is.EqualTo(10));
            Assert.That(m.JigwiABRecoveryMultiplier(a.targetPlayer,5),Is.EqualTo(4));
            Assert.That(m.JigwiABRecoveryMultiplier(Players.Single(x=>x!=a.targetPlayer),5),Is.EqualTo(1));
        }
        [Test] public void GenerateCombineAndTransferKeepEarliestBurnAndOrdinaryExpiry()
        {
            var m=Room();var a=Current(m,11);ulong owner=a.targetPlayer;
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
            var m=Room();ulong owner=Current(m,4).targetPlayer;var idle=Add(m,owner,4);var flying=Add(m,owner,4);
            m.Tick(5);Launch(m,flying,6);var snapshot=m.Snapshot(10);
            Assert.That(snapshot.orbs.Any(o=>o.id==idle.id),Is.False);
            Assert.That(snapshot.orbs.Single(o=>o.id==flying.id).inFlight,Is.True);
        }
        [Test] public void ThreeRealCombinedHitsClearAForEveryOwnerAndFiveColorCountsOnce()
        {
            var m=Room();var a=Current(m,11);var passed=Add(m,a.targetPlayer,11,LabOrbKind.Yin);
            Assert.That(m.TryTransferAt(passed.owner,passed.id,true,new Vector2(1,.5f),Vector2.right,Request(),11,
                LabEdgeCrossingKind.Swipe,out _,out var e),Is.True,e);
            // It was already on the target board when A activated in the normal game; this fixture enters via receive/launch.
            var source=Orbs(m)[passed.id];source.jigwiABBurnAt=16;source.jigwiABBurnEventId=a.id;
            int before=m.Snapshot(11).players.Single(p=>p.id==a.targetPlayer).hitTotal;
            for(int i=0;i<3;i++){var o=Add(m,a.targetPlayer,11);if(i==2)o.fiveColor=true;BossHit(m,o,11,Vector3.forward);
                Assert.That(m.ConfirmHit(o.id,11),Is.False);}
            var s=m.Snapshot(11);var done=s.jigwiAB.rounds.Single();
            Assert.That(done.resolution,Is.EqualTo(LabJigwiABResolution.Completed));Assert.That(done.hitCount,Is.EqualTo(3));
            Assert.That(s.orbs.Single(o=>o.id==passed.id).jigwiABBurnAt,Is.EqualTo(-1));
            Assert.That(s.players.Single(p=>p.id==a.targetPlayer).hitTotal,Is.EqualTo(before+3));
        }
        [Test] public void RelayAndCompatibilityHitCannotEndA()
        {
            var m=Room();var a=Current(m,11);BossHit(m,Add(m,a.targetPlayer,11,LabOrbKind.Relay),11,Vector3.forward);
            BossHit(m,Add(m,a.targetPlayer,11),11,Vector3.zero);Assert.That(Current(m,11).hitCount,Is.Zero);
        }
        [Test] public void NaturalAEndClearsPendingDeadlinesWithoutResurrectingBurnedOrbs()
        {
            var m=Room();var a=Current(m,11);
            Assert.That(m.TryGenerate(a.targetPlayer,Request(),11,out var old,out var e),Is.True,e);m.Tick(16);
            Assert.That(m.Snapshot(16).orbs.Any(o=>o.id==old.id),Is.False);
            Assert.That(m.TryGenerate(a.targetPlayer,Request(),19,out var late,out e),Is.True,e);
            Assert.That(late.jigwiABBurnAt,Is.EqualTo(24));var done=m.Snapshot(20);
            Assert.That(done.jigwiAB.rounds.Single().resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(done.orbs.Single(o=>o.id==late.id).jigwiABBurnAt,Is.EqualTo(-1));
            Assert.That(done.orbs.Single(o=>o.id==late.id).expiresAt,Is.EqualTo(-1));
        }
        [Test] public void BHasNoFreeSuppliesAndOnlyRawPairMakesWater()
        {
            var m=Room();var b=Current(m,31);Assert.That(b.players.Length,Is.EqualTo(2));Assert.That(b.players.All(p=>p.targets.Length==3),Is.True);
            Assert.That(m.Snapshot(31).orbs,Is.Empty);var yin=Add(m,Players[0],31,LabOrbKind.Yin);var yang=Add(m,Players[0],31,LabOrbKind.Yang);
            Assert.That(m.TryCombineAt(Players[0],yin.id,yang.id,Request(),31,new Vector2(.5f,.5f),Vector2.zero,out var o,out var e),Is.True,e);
            Assert.That(o.element,Is.EqualTo(LabOrbElement.Water));Assert.That(m.Snapshot(31).jigwi,Is.Empty);
        }
        [Test] public void FireHitConsumesOnceWithoutNormalRewardsAndNeedsWater()
        {
            var m=Room();var b=Current(m,31);var p=b.players[0];var before=m.Snapshot(31).players.Single(x=>x.id==p.playerId);
            FireHit(m,b,p.playerId,p.targets[0].id,31,LabOrbElement.Fire,false);Assert.That(Current(m,31).players[0].targets[0].lit,Is.True);
            FireHit(m,b,p.playerId,p.targets[0].id,31);var after=m.Snapshot(31).players.Single(x=>x.id==p.playerId);
            Assert.That(after.stamina,Is.EqualTo(before.stamina));Assert.That(after.hitTotal,Is.EqualTo(before.hitTotal));
            Assert.That(after.throwTotal,Is.EqualTo(before.throwTotal));Assert.That(m.PaintSurface.PaintCount,Is.Zero);
            var wrong=Add(m,b.players[1].playerId,31,element:LabOrbElement.Water);Launch(m,wrong,31);
            Assert.That(m.ConfirmJigwiABFireHit(wrong.id,b.id,p.targets[1].id,31,out _),Is.False);
        }
        [Test] public void PersonalClearDoesNotFinishBAndEachFireRelightsAfterTheConfiguredDelay()
        {
            var values=config.CaptureDeveloperValues();values[245]=2;Apply(values);
            var m=Room();var b=Current(m,31);var player=b.players[0];
            foreach(var fire in player.targets)FireHit(m,b,player.playerId,fire.id,31);
            Assert.That(Current(m,32.99).players[0].targets.All(f=>!f.lit),Is.True);
            Assert.That(Current(m,33).players[0].targets.All(f=>f.lit),Is.True);
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
            snapshot=m.Snapshot(98);
            Assert.That(snapshot.jigwiAB.rounds.Length,Is.EqualTo(6));
            Assert.That(snapshot.jigwiAB.rounds.Any(r=>r.resolution==LabJigwiABResolution.Skipped),Is.False);
            Assert.That(snapshot.jigwiAB.rounds.Select(r=>r.activeAt),Is.EqualTo(new[]{5d,25d,40d,55d,70d,85d}));
            ulong fence=m.JigwiABFailureRevision;
            snapshot=m.Snapshot(100);
            Assert.That(snapshot.jigwiAB.fever,Is.True);Assert.That(snapshot.jigwiAB.Active(100),Is.Null);
            Assert.That(m.JigwiABFailureRevision,Is.EqualTo(fence),"Cutoff cleanup must not apply another failure.");
            Assert.That(m.JigwiABRecoveryMultiplier(Players[0],100),Is.EqualTo(2));
            Assert.That(m.Snapshot(120).phase,Is.EqualTo(LabPhase.Defeat));
        }
        [Test] public void DeepCopyAndValidationRejectNaNAndDuplicateNestedTargets()
        {
            var m=Room();var s=m.Snapshot(31);Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players),Is.True);
            var copy=s.jigwiAB.Copy();copy.rounds[1].players[0].targets[0].x=float.NaN;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(m.Snapshot(31).jigwiAB,s.players),Is.True);
            copy=s.jigwiAB.Copy();copy.rounds[1].players[0].targets[1].id=copy.rounds[1].players[0].targets[0].id;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,s.players),Is.False);
        }
        [Test] public void RecoveryIntegrationSplitsAtAActivationAndExpiry()
        {
            var v=config.CaptureDeveloperValues();v[6]=1;Apply(v);var m=Room();var a=Current(m,11);
            float before=m.Snapshot(11).players.Single(p=>p.id==a.targetPlayer).stamina;
            Assert.That(m.Snapshot(12).players.Single(p=>p.id==a.targetPlayer).stamina,Is.EqualTo(before+4).Within(.0001f));
            var end=m.Snapshot(20);float atEnd=end.players.Single(p=>p.id==a.targetPlayer).stamina;
            Assert.That(m.Snapshot(21).players.Single(p=>p.id==a.targetPlayer).stamina,Is.EqualTo(atEnd+1).Within(.0001f));
            Assert.That(m.JigwiABRecoveryMultiplier(a.targetPlayer,100),Is.EqualTo(2));
        }
        [Test] public void PersonalClearDuringBDoesNotShrinkOrChangeTheNormalHitPaintBudget()
        {
            var mission=Room();var normal=Room(LabStageYokai.None);var group=Current(mission,31);
            var owner=group.players.Single(player=>player.playerId==Players[0]);
            foreach(var fire in owner.targets)FireHit(mission,group,owner.playerId,fire.id,31);
            Assert.That(mission.JigwiABBlocksBoss(owner.playerId,31),Is.False);
            var first=Add(mission,Players[0],31,element:LabOrbElement.Water);
            var second=Add(normal,Players[0],31,element:LabOrbElement.Water);
            BossHit(mission,first,31,Vector3.forward);BossHit(normal,second,31,Vector3.forward);
            Assert.That(mission.PaintSurface.CoveredCount,Is.EqualTo(normal.PaintSurface.CoveredCount));
            Assert.That(mission.Snapshot(31).hp,Is.EqualTo(normal.Snapshot(31).hp));
        }
        [TestCase(1)] [TestCase(6)] public void FireCountEndpointsRemainBoundedAndValidate(int count)
        {
            var v=config.CaptureDeveloperValues();v[244]=count;Apply(v);var m=Room();var s=m.Snapshot(31);
            Assert.That(s.jigwiAB.Active(31).players.All(p=>p.targets.Length==count),Is.True);
            Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players),Is.True);
        }
        [Test] public void ModernABRejectsLegacyManualChallengesWithoutFreeSupplies()
        {
            var m=Room();Assert.That(m.TryStartJigwiFire(Players[0],0,out var e),Is.False);
            Assert.That(e,Is.EqualTo("STAGE_PATTERN_NOT_ALLOWED"));
            Assert.That(m.TryStartJigwiGenerationBurn(Players[0],0,out _),Is.False);Assert.That(m.Snapshot(0).orbs,Is.Empty);
        }
        [TestCase(2)] [TestCase(3)] [TestCase(5)] public void GroupFireIncludesEveryActualParticipantIncludingHostZero(int count)
        {
            var ids=Enumerable.Range(0,count).Select(i=>(ulong)(i*10)).ToArray();var m=new LabModel();
            m.Begin("ab-members-"+count,ids,0,config,LabStageYokai.Jigwi);var s=m.Snapshot(31);
            Assert.That(s.jigwiAB.Active(31).players.Select(p=>p.playerId),Is.EquivalentTo(ids));
            Assert.That(LabJigwiABNetworkValidation.Valid(s.jigwiAB,s.players),Is.True);
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
                value.resolution=LabJigwiABResolution.Completed;value.resolvedAt=11;value.hitCount=value.requiredHits-1;
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
        [TestCase(60d,true)] [TestCase(60.001d,false)] [TestCase(0d,false)]
        public void RelightValidationUsesTheExtinguishTimeAndAllowsABoundedDeadlinePastCutoff(double delay,bool expected)
        {
            var m=Room();var snapshot=m.Snapshot(91);var copy=snapshot.jigwiAB.Copy();
            var fire=copy.rounds.Single(value=>value.slot==5).players[0].targets[0];
            fire.lit=false;fire.extinguishedAt=91;fire.relightAt=91+delay;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy,snapshot.players),Is.EqualTo(expected));
        }
        [Test]
        public void WaterOnTheCurtainSplashesButNeverExtinguishesAnIgnitionOrRewardsABossHit()
        {
            var model = Room();
            var group = Current(model, 31d);
            ulong owner = group.players[0].playerId;
            var before = model.Snapshot(31d);
            var attacker = before.players.Single(player => player.id == owner);
            for (int i = 0; i < 3; i++)
            {
                double at = 31d + i * .1d;
                var water = Add(model, owner, at, element: LabOrbElement.Water);
                Launch(model, water, at);
                Assert.That(model.ConfirmJigwiABFireHit(water.id, group.id,
                    LabJigwiABFireMotion.CurtainId(group.id, owner), at, out bool extinguished), Is.True);
                Assert.That(extinguished, Is.False);
                Assert.That(model.ConfirmJigwiABFireHit(water.id, group.id,
                    LabJigwiABFireMotion.CurtainId(group.id, owner), at, out _), Is.False);
            }
            var after = model.Snapshot(31.2d);
            var own = after.jigwiAB.Active(31.2d).players.Single(player => player.playerId == owner);
            Assert.That(own.targets.All(target => target.lit), Is.True);
            Assert.That(own.lastWaterSplashAt, Is.EqualTo(31.2d));
            Assert.That(model.JigwiABBlocksBoss(owner, 31.2d), Is.True);
            Assert.That(after.orbs, Is.Empty);
            Assert.That(after.hp, Is.EqualTo(before.hp));
            Assert.That(model.PaintSurface.CoveredCount, Is.Zero);
            var current = after.players.Single(player => player.id == owner);
            Assert.That(current.stamina, Is.EqualTo(attacker.stamina));
            Assert.That(current.hitTotal, Is.EqualTo(attacker.hitTotal));
            Assert.That(current.throwTotal, Is.EqualTo(attacker.throwTotal));
            Assert.That(current.damageTotal, Is.EqualTo(attacker.damageTotal));
        }

        [Test]
        public void ForeignOrStaleCurtainIdsCannotConsumeAProjectileAndWrongColourDoesNotSplash()
        {
            var model = Room();
            var group = Current(model, 31d);
            ulong owner = group.players[0].playerId, other = group.players[1].playerId;
            var wrong = Add(model, owner, 31d, element: LabOrbElement.Fire);
            Launch(model, wrong, 31d);
            Assert.That(model.ConfirmJigwiABFireHit(wrong.id, group.id,
                LabJigwiABFireMotion.CurtainId(group.id, other), 31d, out _), Is.False);
            Assert.That(model.ConfirmJigwiABFireHit(wrong.id, "old-round",
                LabJigwiABFireMotion.CurtainId(group.id, owner), 31d, out _), Is.False);
            Assert.That(model.Snapshot(31d).orbs.Any(orb => orb.id == wrong.id), Is.True);
            Assert.That(model.ConfirmJigwiABFireHit(wrong.id, group.id,
                LabJigwiABFireMotion.CurtainId(group.id, owner), 31d, out bool off), Is.True);
            Assert.That(off, Is.False);
            var own = Current(model, 31d).players.Single(player => player.playerId == owner);
            Assert.That(own.lastWaterSplashAt, Is.EqualTo(-1d));
            Assert.That(own.targets.All(target => target.lit), Is.True);
            Assert.That(model.Snapshot(31d).orbs, Is.Empty);
        }

        [Test]
        public void OneRemainingIgnitionStillConsumesADirectBossContactUntilTheThirdPointIsHit()
        {
            var model = Room();
            var group = Current(model, 31d);
            var own = group.players[0];
            FireHit(model, group, own.playerId, own.targets[0].id, 31d);
            FireHit(model, group, own.playerId, own.targets[1].id, 31.1d);
            Assert.That(model.JigwiABBlocksBoss(own.playerId, 31.2d), Is.True);
            var before = model.Snapshot(31.2d);
            var bypass = Add(model, own.playerId, 31.2d, element: LabOrbElement.Water);
            Launch(model, bypass, 31.2d);
            Assert.That(model.ConfirmHit(bypass.id, 31.2d, Vector3.forward,
                out var element, out int damage, out int impact), Is.False);
            Assert.That(element, Is.EqualTo(LabOrbElement.None));
            Assert.That(damage, Is.Zero); Assert.That(impact, Is.Zero);
            var blocked = model.Snapshot(31.2d);
            Assert.That(blocked.orbs.Any(orb => orb.id == bypass.id), Is.False,
                "A rejected boss contact cannot remain an orphaned in-flight orb.");
            Assert.That(blocked.hp, Is.EqualTo(before.hp));
            Assert.That(model.PaintSurface.CoveredCount, Is.Zero);
            Assert.That(blocked.players.Single(player => player.id == own.playerId).hitTotal,
                Is.EqualTo(before.players.Single(player => player.id == own.playerId).hitTotal));
            var current = blocked.jigwiAB.Active(31.2d).players.Single(player => player.playerId == own.playerId);
            Assert.That(current.targets.Count(target => target.lit), Is.EqualTo(1));
            Assert.That(current.lastWaterSplashAt, Is.EqualTo(31.2d));
            Assert.That(model.ConfirmHit(bypass.id, 31.2d), Is.False);

            FireHit(model, group, own.playerId, own.targets[2].id, 31.3d);
            Assert.That(model.JigwiABBlocksBoss(own.playerId, 31.3d), Is.False);
            Assert.That(model.JigwiABBlocksBoss(group.players[1].playerId, 31.3d), Is.True);
            Assert.That(Current(model, 31.3d).resolution, Is.EqualTo(LabJigwiABResolution.None),
                "Local clearance opens this player's boss before the shared group finishes.");
            BossHit(model, Add(model, own.playerId, 31.3d), 31.3d, Vector3.forward);
            Assert.That(model.PaintSurface.CoveredCount, Is.GreaterThan(0));
        }

        [Test]
        public void AWrongColourBossBypassIsConsumedWithoutWaterAndRelightClosesTheOwnersCurtainAgain()
        {
            var values = config.CaptureDeveloperValues(); values[245] = 2; Apply(values);
            var model = Room();
            var group = Current(model, 31d);
            var own = group.players[0];
            var wrong = Add(model, own.playerId, 31d, element: LabOrbElement.Fire);
            Launch(model, wrong, 31d);
            Assert.That(model.ConfirmHit(wrong.id, 31d), Is.False);
            Assert.That(Current(model, 31d).players[0].lastWaterSplashAt, Is.EqualTo(-1d));
            Assert.That(model.Snapshot(31d).orbs.Any(orb => orb.id == wrong.id), Is.False);
            foreach (var target in own.targets) FireHit(model, group, own.playerId, target.id, 31d);
            Assert.That(model.JigwiABBlocksBoss(own.playerId, 32.99d), Is.False);
            model.Tick(33d);
            Assert.That(model.JigwiABBlocksBoss(own.playerId, 33d), Is.True);
            var relit = Current(model, 33d).players[0];
            Assert.That(relit.targets.All(target => target.lit), Is.True);
            Assert.That(relit.lastWaterSplashAt, Is.EqualTo(31d), "Relight itself is not a new water impact.");
            var water = Add(model, own.playerId, 33d, element: LabOrbElement.Water);
            Launch(model, water, 33d);
            Assert.That(model.ConfirmHit(water.id, 33d), Is.False);
            Assert.That(Current(model, 33d).players[0].lastWaterSplashAt, Is.EqualTo(33d));
        }

        [Test]
        public void ALocallyClearedCurtainRejectsFurtherCurtainHitsAndSharedCompletionLeavesNoBossGate()
        {
            var model = Room(); var group = Current(model, 31d); var own = group.players[0];
            foreach (var target in own.targets) FireHit(model, group, own.playerId, target.id, 31d);
            var water = Add(model, own.playerId, 31d, element: LabOrbElement.Water); Launch(model, water, 31d);
            Assert.That(model.ConfirmJigwiABFireHit(water.id, group.id,
                LabJigwiABFireMotion.CurtainId(group.id, own.playerId), 31d, out _), Is.False);
            Assert.That(model.Snapshot(31d).orbs.Any(orb => orb.id == water.id), Is.True);
            BossHit(model, Add(model, own.playerId, 31d), 31d, Vector3.forward);
            foreach (var target in group.players[1].targets) FireHit(model, group, group.players[1].playerId, target.id, 31d);
            Assert.That(model.Snapshot(31d).jigwiAB.rounds.Last().resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            foreach (ulong id in Players) Assert.That(model.JigwiABBlocksBoss(id, 31d), Is.False);
        }

        [Test]
        public void WaterSplashMetadataCopiesWithoutAliasingOrChangingIgnitionProgress()
        {
            var model = Room(); var group = Current(model, 31d); ulong owner = group.players[0].playerId;
            var water = Add(model, owner, 31d, element: LabOrbElement.Water); Launch(model, water, 31d);
            Assert.That(model.ConfirmJigwiABFireHit(water.id, group.id,
                LabJigwiABFireMotion.CurtainId(group.id, owner), 31d, out _), Is.True);
            var snapshot = model.Snapshot(31d); var copy = snapshot.jigwiAB.Copy();
            Assert.That(copy.rounds.Last().players[0].lastWaterSplashAt, Is.EqualTo(31d));
            copy.rounds.Last().players[0].lastWaterSplashAt = 32d;
            Assert.That(snapshot.jigwiAB.rounds.Last().players[0].lastWaterSplashAt, Is.EqualTo(31d));
            Assert.That(copy.rounds.Last().players[0].targets.All(target => target.lit), Is.True);
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB, snapshot.players), Is.True);
        }

        [TestCase(-.5d)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        [TestCase(24.99d)] [TestCase(35.01d)]
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
            var m=Room();var before=m.Snapshot(34.9d);
            string[] drums=before.orbs.Where(o=>o.kind.IsDrum()).Select(o=>o.id).ToArray();
            var fixture=new List<LabOrbState>();
            foreach(ulong owner in Players)
            {
                foreach(LabOrbKind kind in Enum.GetValues(typeof(LabOrbKind)))
                    if(!kind.IsDrum())fixture.Add(Add(m,owner,34.9d,kind));
                var flying=Add(m,owner,34.9d);Launch(m,flying,34.9d);fixture.Add(flying);
            }
            m.DrainOrbExpiries();m.DrainBombExplosions();
            var expired=m.Snapshot(35d);var failure=expired.jigwiAB.rounds.Single(r=>r.slot==1);
            Assert.That(failure.resolution,Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(failure.resolvedAt,Is.EqualTo(35d));
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
            Assert.That(m.DrainOrbExpiries().Select(e=>e.OrbId),Is.EquivalentTo(fixture.Select(o=>o.id)));
            foreach(var orb in fixture)
            {
                Assert.That(m.ConfirmHit(orb.id,35d),Is.False);
                Assert.That(m.ConfirmMiss(orb.id,35d),Is.False);
                Assert.That(m.TryLaunch(orb.owner,orb.id,Request(),35d,out _,out _),Is.False);
                Assert.That(m.TryUseItemAt(orb.owner,orb.id,Request(),35d,Vector2.one*.5f,out _,out _),Is.False);
            }
            m.Tick(35d);Assert.That(m.DrainOrbExpiries(),Is.Empty);
            Assert.That(m.JigwiABState.rounds.Single(r=>r.slot==1).clearedThroughRevision,
                Is.EqualTo(failure.clearedThroughRevision));
            Assert.That(m.Snapshot(35d).players.All(player=>player.stamina==0f),Is.True);
        }

        [Test]
        public void BFailureAllowsNormalRecoveryAndNewApprovalsWithoutReapplyingTheWipe()
        {
            var values=config.CaptureDeveloperValues();values[6]=2f;Apply(values);
            var m=Room();m.Tick(35d);ulong fence=m.JigwiABFailureRevision;
            var recovered=m.Snapshot(35.5d);
            Assert.That(recovered.players.Select(p=>p.stamina),Is.EqualTo(new[]{1f,1f}));
            Assert.That(m.TryGenerate(Players[0],Request(),35.5d,out var fresh,out var reason),Is.True,reason);
            Assert.That(fresh.revision,Is.GreaterThan(fence));
            Assert.That(m.Snapshot(36d).orbs.Any(o=>o.id==fresh.id),Is.True);
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
            Assert.That(m.ConfirmHit(last.id,36d,Vector3.forward,out _,out _,out _),Is.True);
            Assert.That(m.PaintSurface.PaintCount,Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void RewardPaintBudgetUsesTheCapturedPaintScaleAndKeepsTheThreeShareClamp(bool fiveColor)
        {
            var values=config.CaptureDeveloperValues();values[251]=3f;values[252]=fiveColor?3f:1.5f;Apply(values);
            var m=Room();var group=Current(m,31d);Clear(m,group,31d);
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
            double budget=m.PaintSurface.Coverage+m.PaintSurface.PendingCoverage;
            double expected=reference.PaintSurface.Coverage+reference.PaintSurface.PendingCoverage;
            Assert.That(budget,Is.EqualTo(expected).Within(2d/m.PaintSurface.CellCount));
        }

        [TestCase(54d,55d,2,3)] [TestCase(84d,85d,4,5)]
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
            var m=Room();var group=Current(m,31d);var own=group.players[0];
            foreach(var fire in own.targets)FireHit(m,group,own.playerId,fire.id,31d);
            var preserved=Add(m,Players[1],31d);
            BossHit(m,Add(m,own.playerId,31d),31d,Vector3.forward);
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
            var m=Room();var group=Current(m,31d);Clear(m,group,31d);var source=m.Snapshot(65d);
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
