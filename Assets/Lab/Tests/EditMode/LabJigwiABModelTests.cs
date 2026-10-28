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
            var m=Room();var a=Current(m,10);var o=Add(m,a.targetPlayer,10);
            Assert.That(a.Warning(10),Is.True);Assert.That(m.JigwiABRecoveryMultiplier(a.targetPlayer,10),Is.EqualTo(1));
            Assert.That(m.Snapshot(10.99).orbs.Single(x=>x.id==o.id).jigwiABBurnAt,Is.EqualTo(-1));
            Assert.That(m.Snapshot(11).orbs.Single(x=>x.id==o.id).jigwiABBurnAt,Is.EqualTo(16));
            Assert.That(m.JigwiABRecoveryMultiplier(a.targetPlayer,11),Is.EqualTo(4));
            Assert.That(m.JigwiABRecoveryMultiplier(Players.Single(x=>x!=a.targetPlayer),11),Is.EqualTo(1));
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
            var m=Room();ulong owner=Current(m,10).targetPlayer;var idle=Add(m,owner,10);var flying=Add(m,owner,10);
            m.Tick(11);Launch(m,flying,12);var s=m.Snapshot(16);
            Assert.That(s.orbs.Any(o=>o.id==idle.id),Is.False);Assert.That(s.orbs.Single(o=>o.id==flying.id).inFlight,Is.True);
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
            Assert.That(m.TryGenerate(a.targetPlayer,Request(),20,out var late,out e),Is.True,e);
            Assert.That(late.jigwiABBurnAt,Is.EqualTo(25));var done=m.Snapshot(21);
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
        [Test] public void PersonalClearDoesNotFinishBAndEachFireRelightsIn12Seconds()
        {
            var m=Room();var b=Current(m,31);var p=b.players[0];foreach(var f in p.targets)FireHit(m,b,p.playerId,f.id,31);
            Assert.That(Current(m,42.99).players[0].targets.All(f=>!f.lit),Is.True);
            Assert.That(Current(m,43).players[0].targets.All(f=>f.lit),Is.True);
        }
        [Test] public void BSuccessRestoresOnlyItsScaleAndPreservesBombSmallAndAlreadyFlyingRadius()
        {
            var m=Room();var b=Current(m,31);var idle=Add(m,Players[0],31);var bomb=Add(m,Players[0],31);bomb.small=true;
            var flying=Add(m,Players[0],42);Launch(m,flying,42);var s=m.Snapshot(43);
            Assert.That(s.orbs.Single(o=>o.id==idle.id).jigwiABRadiusScale,Is.EqualTo(.65f));
            Assert.That(s.orbs.Single(o=>o.id==flying.id).jigwiABRadiusScale,Is.EqualTo(1));Clear(m,b,43);s=m.Snapshot(43);
            Assert.That(s.jigwiAB.rounds.Single(r=>r.slot==1).resolution,Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(s.orbs.Single(o=>o.id==idle.id).jigwiABRadiusScale,Is.EqualTo(1));Assert.That(s.orbs.Single(o=>o.id==bomb.id).small,Is.True);
        }
        [Test] public void BusySlotsSkipAnd100ClearsBWhile20SecondFeverRemains()
        {
            var m=Room();Current(m,31);var o=Add(m,Players[0],31);var s=m.Snapshot(99);
            Assert.That(s.jigwiAB.rounds.Length,Is.EqualTo(8));Assert.That(s.jigwiAB.rounds.Where(r=>r.slot>=2).All(r=>r.resolution==LabJigwiABResolution.Skipped),Is.True);
            s=m.Snapshot(100);Assert.That(s.jigwiAB.fever,Is.True);Assert.That(s.jigwiAB.Active(100),Is.Null);
            Assert.That(s.orbs.Single(x=>x.id==o.id).jigwiABRadiusScale,Is.EqualTo(1));Assert.That(m.JigwiABRecoveryMultiplier(Players[0],100),Is.EqualTo(2));
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
            var end=m.Snapshot(21);float atEnd=end.players.Single(p=>p.id==a.targetPlayer).stamina;
            Assert.That(m.Snapshot(22).players.Single(p=>p.id==a.targetPlayer).stamina,Is.EqualTo(atEnd+1).Within(.0001f));
            Assert.That(m.JigwiABRecoveryMultiplier(a.targetPlayer,100),Is.EqualTo(2));
        }
        [Test] public void BRadiusNeverChangesTheNormalHitPaintBudget()
        {
            var a=Room();var b=Room(LabStageYokai.None);Current(a,43);
            var small=Add(a,Players[0],43,element:LabOrbElement.Water);
            var normal=Add(b,Players[0],43,element:LabOrbElement.Water);
            // Clear only this owner's three ignitions; teammates keep B and its independent shrink active.
            var group=Current(a,43);var own=group.players.Single(player=>player.playerId==Players[0]);
            foreach(var target in own.targets)FireHit(a,group,own.playerId,target.id,43);
            Assert.That(a.JigwiABBlocksBoss(own.playerId,43),Is.False);
            BossHit(a,small,43,Vector3.forward);BossHit(b,normal,43,Vector3.forward);
            Assert.That(a.PaintSurface.CoveredCount,Is.EqualTo(b.PaintSurface.CoveredCount));
            Assert.That(a.Snapshot(43).hp,Is.EqualTo(b.Snapshot(43).hp));
        }
        [TestCase(1)] [TestCase(6)] public void FireCountEndpointsRemainBoundedAndValidate(int count)
        {
            var v=config.CaptureDeveloperValues();v[242]=count;Apply(v);var m=Room();var s=m.Snapshot(31);
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
            var fire=copy.rounds.Single(value=>value.slot==1).players[0].targets[0];
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
            var model = Room();
            var group = Current(model, 31d);
            var own = group.players[0];
            var wrong = Add(model, own.playerId, 31d, element: LabOrbElement.Fire);
            Launch(model, wrong, 31d);
            Assert.That(model.ConfirmHit(wrong.id, 31d), Is.False);
            Assert.That(Current(model, 31d).players[0].lastWaterSplashAt, Is.EqualTo(-1d));
            Assert.That(model.Snapshot(31d).orbs.Any(orb => orb.id == wrong.id), Is.False);
            foreach (var target in own.targets) FireHit(model, group, own.playerId, target.id, 31d);
            Assert.That(model.JigwiABBlocksBoss(own.playerId, 42.99d), Is.False);
            model.Tick(43d);
            Assert.That(model.JigwiABBlocksBoss(own.playerId, 43d), Is.True);
            var relit = Current(model, 43d).players[0];
            Assert.That(relit.targets.All(target => target.lit), Is.True);
            Assert.That(relit.lastWaterSplashAt, Is.EqualTo(31d), "Relight itself is not a new water impact.");
            var water = Add(model, own.playerId, 43d, element: LabOrbElement.Water);
            Launch(model, water, 43d);
            Assert.That(model.ConfirmHit(water.id, 43d), Is.False);
            Assert.That(Current(model, 43d).players[0].lastWaterSplashAt, Is.EqualTo(43d));
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
        [TestCase(30.99d)] [TestCase(100.01d)]
        public void WaterSplashValidationRejectsInvalidAndOutOfRoundTimes(double value)
        {
            var model = Room(); var snapshot = model.Snapshot(31d); var copy = snapshot.jigwiAB.Copy();
            copy.rounds.Last().players[0].lastWaterSplashAt = value;
            Assert.That(LabJigwiABNetworkValidation.Valid(copy, snapshot.players), Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB, snapshot.players), Is.True);
        }

        [Test] public void FireMotionIsDeterministicForTheSameBattleTimeAndMovesInsideViewport()
        {
            var m=Room();var b=Current(m,31);var f=b.players[0].targets[0];var first=LabJigwiABFireMotion.Position(b,f,0,31);
            Assert.That(LabJigwiABFireMotion.Position(b,f,0,31),Is.EqualTo(first));Assert.That(LabJigwiABFireMotion.Position(b,f,0,32),Is.Not.EqualTo(first));
            for(int i=0;i<100;i++){var p=LabJigwiABFireMotion.Position(b,f,0,31+i*.1);Assert.That(p.x,Is.InRange(.1f,.9f));Assert.That(p.y,Is.InRange(.48f,.86f));}
        }
    }
}
