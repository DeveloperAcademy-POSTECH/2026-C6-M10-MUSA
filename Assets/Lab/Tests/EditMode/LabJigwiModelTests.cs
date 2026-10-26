using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabJigwiModelTests
    {
        private LabConfig config;
        private LabModel model;
        private int requests;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            var values = config.CaptureDeveloperValues();
            values[3] = values[4] = 1000f;
            values[5] = 1f;
            values[6] = 0f;
            values[9] = 100f;
            values[74] = 2f;
            values[39] = values[40] = 50f;
            for (int i = 41; i <= 44; i++) values[i] = 0f;
            values[205] = 0f; // no common gimmicks (#311)
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            model = new LabModel();
            model.Begin("jigwi-core-tests", new ulong[] { 0, 20 }, 0d, config);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private string Request() => "jigwi-test-" + (++requests);

        private LabOrbState Generate(ulong owner, double now)
        {
            Assert.That(model.TryGenerate(owner, Request(), now, out var orb, out var reason), Is.True, reason);
            Assert.That(orb, Is.Not.Null);
            return orb;
        }

        private LabOrbState[] Pair(ulong owner, double now)
        {
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 24 && (yin == null || yang == null); i++)
            {
                LabOrbState orb = Generate(owner, now);
                if (orb.kind == LabOrbKind.Yin) yin = orb;
                if (orb.kind == LabOrbKind.Yang) yang = orb;
            }
            Assert.That(yin, Is.Not.Null);
            Assert.That(yang, Is.Not.Null);
            return new[] { yin, yang };
        }

        private LabOrbState Combine(ulong owner, LabOrbState[] pair, double now)
        {
            Assert.That(model.TryCombineAt(owner, pair[0].id, pair[1].id, Request(), now,
                new Vector2(.5f, .5f), Vector2.zero, out var combined, out var reason), Is.True, reason);
            return combined;
        }

        [Test]
        public void LegacyRoundHasNoAutomaticJigwiStateOrModifiedRawLifetime()
        {
            var orb = Generate(0, 0d);
            Assert.That(orb.jigwiGenerationBurn || orb.jigwiSupply, Is.False);
            Assert.That(orb.jigwiRadiusScale, Is.EqualTo(1f));
            Assert.That(orb.expiresAt, Is.EqualTo(-1d));
            model.Tick(7.9d);
            Assert.That(model.Snapshot(7.9d).jigwi, Is.Empty);
            Assert.That(model.TryGetMovableState(orb.id, out _), Is.True);
            model.Tick(8d);
            Assert.That(model.TryGetMovableState(orb.id, out _), Is.False);
        }

        [Test]
        public void FireCreatesBoundedWaterSuppliesWithoutChargingOrChangingGenerationStats()
        {
            float before = model.Snapshot(0d).players[0].stamina;
            Assert.That(model.TryStartJigwiFire(0, 0d, out var reason), Is.True, reason);
            var snapshot = model.Snapshot(0d);
            var fire = snapshot.JigwiFor(0);
            Assert.That(fire.FireActive(0d), Is.True);
            Assert.That(fire.targets.Length, Is.EqualTo(1));
            Assert.That(snapshot.orbs.Length, Is.EqualTo(3));
            Assert.That(snapshot.orbs.All(orb => orb.owner == 0 && orb.jigwiSupply
                && orb.kind == LabOrbKind.Combined && orb.element == LabOrbElement.Water
                && orb.expiresAt >= fire.fireEndsAt), Is.True);
            Assert.That(snapshot.players[0].stamina, Is.EqualTo(before));
            Assert.That(snapshot.players[0].generatedTotal, Is.Zero);
            Assert.That(snapshot.players[0].combinedTotal, Is.Zero);
            Assert.That(model.TryStartJigwiFire(0, 0d, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("JIGWI_ALREADY_ACTIVE"));
            // Deep snapshots must not allow a client to extinguish the Host's targets.
            fire.targets[0].extinguished = true;
            Assert.That(model.Snapshot(0d).JigwiFor(0).targets[0].extinguished, Is.False);
        }

        [Test]
        public void SuppliedWaterDoesNotPreventNormalGenerationEvenWhenBoardStorageIsOne()
        {
            var values = config.CaptureDeveloperValues();
            values[9] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.True);
            var room = new LabModel();
            room.BeginSolo("jigwi-full", 0, 0d, config);
            Assert.That(room.TryStartJigwiFire(0, 0d, out _), Is.True);
            Assert.That(room.TryGenerate(0, "one", 0d, out _, out _), Is.True,
                "Free supplies must not consume normal storage capacity.");
            var water = room.Snapshot(0d).orbs.First(orb => orb.jigwiSupply);
            Assert.That(room.TryLaunch(0, water.id, "water", 0d, out _, out _), Is.True);
            Assert.That(room.ConfirmMiss(water.id, 0d), Is.True);
            Assert.That(room.TryGenerate(0, "full", 0d, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("STORAGE_FULL"), "Only the one normal Raw occupies storage.");
        }

        [Test]
        public void MatchingWaterFireCollisionConsumesOnceWithoutGourdDamageOrHitReward()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var before = model.Snapshot(0d);
            var water = before.orbs.First(orb => orb.jigwiSupply);
            var fire = before.JigwiFor(0);
            Assert.That(model.TryLaunch(0, water.id, Request(), 1d, out _, out _), Is.True);
            Assert.That(model.ConfirmJigwiFireHit(water.id, fire.fireId, fire.targets[0].id, 1.1d,
                out bool extinguished), Is.True);
            Assert.That(extinguished, Is.True);
            Assert.That(model.ConfirmJigwiFireHit(water.id, fire.fireId, fire.targets[0].id, 1.1d, out _), Is.False);
            var after = model.Snapshot(1.1d);
            Assert.That(after.hp, Is.EqualTo(before.hp));
            Assert.That(after.paintCoverage, Is.EqualTo(before.paintCoverage));
            Assert.That(after.players[0].stamina, Is.EqualTo(before.players[0].stamina));
            model.Tick(15d);
            Assert.That(model.Snapshot(15d).JigwiFor(0).Overheated(15d), Is.False);
        }

        [Test]
        public void FireHitRequiresItsOwnOwnerAndCurrentTargetIdentity()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            Assert.That(model.TryStartJigwiFire(20, 0d, out _), Is.True);
            var snapshot = model.Snapshot(0d);
            var firstFire = snapshot.JigwiFor(0);
            var other = snapshot.orbs.First(orb => orb.owner == 20);
            Assert.That(model.TryLaunch(20, other.id, Request(), 0d, out _, out _), Is.True);
            Assert.That(model.ConfirmJigwiFireHit(other.id, firstFire.fireId, firstFire.targets[0].id,
                0d, out _), Is.False);
            Assert.That(model.TryGetMovableState(other.id, out var retained), Is.True);
            Assert.That(retained.inFlight, Is.True);
        }

        [Test]
        public void NonWaterProjectileIsConsumedButLeavesFireAlight()
        {
            var combined = Combine(0, Pair(0, 0d), 0d);
            for (int i = 0; i < 12 && combined.element == LabOrbElement.Water; i++)
                combined = Combine(0, Pair(0, 0d), 0d);
            Assert.That(combined.element, Is.Not.EqualTo(LabOrbElement.Water));
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var fire = model.Snapshot(0d).JigwiFor(0);
            Assert.That(model.TryLaunch(0, combined.id, Request(), 0d, out _, out _), Is.True);
            Assert.That(model.ConfirmJigwiFireHit(combined.id, fire.fireId, fire.targets[0].id,
                .1d, out bool extinguished), Is.True);
            Assert.That(extinguished, Is.False);
            Assert.That(model.TryGetMovableState(combined.id, out _), Is.False);
            Assert.That(model.Snapshot(.1d).JigwiFor(0).FireActive(.1d), Is.True);
        }

        [Test]
        public void FireDeadlineStartsFourSecondOverheatAndDoesNotExtendOnRepeatedTicks()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            model.Tick(15d);
            Assert.That(model.Snapshot(15d).JigwiFor(0).overheatUntil, Is.EqualTo(19d));
            Assert.That(model.Snapshot(18d).JigwiFor(0).Overheated(18d), Is.True);
            Assert.That(model.Snapshot(19d).JigwiFor(0).Overheated(19d), Is.False);
            Assert.That(model.Snapshot(20d).JigwiFor(0).overheatUntil, Is.EqualTo(19d));
        }

        [Test]
        public void EveryConfiguredFireMustBeExtinguishedAndTheFireDeadlineIsExclusive()
        {
            var values = config.CaptureDeveloperValues();
            values[178] = 2f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.True);
            var room = new LabModel();
            room.BeginSolo("jigwi-two-targets", 0, 0d, config);
            Assert.That(room.TryStartJigwiFire(0, 0d, out _), Is.True);
            var snapshot = room.Snapshot(0d);
            var fire = snapshot.JigwiFor(0);
            Assert.That(fire.targets.Length, Is.EqualTo(2));
            Assert.That(snapshot.orbs.Length, Is.EqualTo(4));
            Assert.That(room.TryLaunch(0, snapshot.orbs[0].id, "one-shot", 1d, out _, out _), Is.True);
            Assert.That(room.ConfirmJigwiFireHit(snapshot.orbs[0].id, fire.fireId, fire.targets[0].id,
                1d, out bool extinguished), Is.True);
            Assert.That(extinguished, Is.True);
            Assert.That(room.Snapshot(1d).JigwiFor(0).FireActive(1d), Is.True);
            Assert.That(room.TryLaunch(0, snapshot.orbs[1].id, "late-shot", 14d, out _, out _), Is.True);
            Assert.That(room.ConfirmJigwiFireHit(snapshot.orbs[1].id, fire.fireId, fire.targets[1].id,
                15d, out _), Is.False);
            Assert.That(room.Snapshot(15d).JigwiFor(0).Overheated(15d), Is.True);
        }

        [Test]
        public void OverheatedGenerateSpendsTheUsualCostAndCountButProducesNothingAndIsIdempotent()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var before = model.Snapshot(15d);
            Assert.That(model.TryGenerate(0, "hot-generate", 15d, out var output, out var reason), Is.True);
            Assert.That(output, Is.Null);
            Assert.That(reason, Is.EqualTo("JIGWI_BURNED"));
            var after = model.Snapshot(15d);
            Assert.That(after.players[0].stamina, Is.EqualTo(before.players[0].stamina - config.GenerateCost));
            Assert.That(after.players[0].generatedTotal, Is.EqualTo(before.players[0].generatedTotal + 1));
            Assert.That(after.orbs, Is.Empty);
            Assert.That(model.TryGenerate(0, "hot-generate", 15d, out _, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("DUPLICATE_REQUEST"));
            Assert.That(model.Snapshot(15d).players[0].stamina, Is.EqualTo(after.players[0].stamina));
        }

        [Test]
        public void OverheatedGenerationStillChargesOnAFullBoardBecauseItAllocatesNoNewOrb()
        {
            var values = config.CaptureDeveloperValues();
            values[9] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.True);
            var room = new LabModel();
            room.BeginSolo("jigwi-hot-full", 0, 0d, config);
            Assert.That(room.TryStartJigwiFire(0, 0d, out _), Is.True);
            Assert.That(room.TryGenerate(0, "keep", 14d, out var kept, out _), Is.True);
            var before = room.Snapshot(15d);
            Assert.That(room.TryGenerate(0, "burn-on-full", 15d, out var output, out var reason), Is.True);
            Assert.That(output, Is.Null);
            Assert.That(reason, Is.EqualTo("JIGWI_BURNED"));
            var after = room.Snapshot(15d);
            Assert.That(after.players[0].stamina, Is.EqualTo(before.players[0].stamina - config.GenerateCost));
            Assert.That(after.orbs.Select(orb => orb.id), Is.EqualTo(new[] { kept.id }));
        }

        [Test]
        public void OverheatedCombineConsumesBothRawInputsWithoutCreatingAnOutputOrCombinationStatistic()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var pair = Pair(0, 14d);
            model.Tick(15d);
            var before = model.Snapshot(15d);
            Assert.That(model.TryCombineAt(0, pair[0].id, pair[1].id, Request(), 15d,
                Vector2.one * .5f, Vector2.zero, out var output, out var reason), Is.True);
            Assert.That(output, Is.Null);
            Assert.That(reason, Is.EqualTo("JIGWI_BURNED"));
            Assert.That(model.TryGetMovableState(pair[0].id, out _), Is.False);
            Assert.That(model.TryGetMovableState(pair[1].id, out _), Is.False);
            Assert.That(model.Snapshot(15d).players[0].combinedTotal, Is.EqualTo(before.players[0].combinedTotal));
        }

        [Test]
        public void OverheatedPassiveContactKeepsTheExistingNormalCombinationInsteadOfBurningEitherInput()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var pair = Pair(0, 14d);
            var before = model.Snapshot(15d);
            const string contactRequest = "hot-passive-contact";
            Assert.That(model.TryCombineAt(0, pair[0].id, pair[1].id, contactRequest, 15d,
                Vector2.one * .5f, Vector2.zero, out var output, out var reason,
                passiveContact: true), Is.True, reason);
            Assert.That(reason, Is.EqualTo("COMBINED"));
            Assert.That(output, Is.Not.Null);
            Assert.That(output.kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(output.createdAt, Is.EqualTo(15d));
            Assert.That(output.expiresAt, Is.EqualTo(-1d));
            Assert.That(output.jigwiGenerationBurn || output.jigwiSupply, Is.False);
            Assert.That(output.jigwiRadiusScale, Is.EqualTo(1f));
            Assert.That(model.TryGetMovableState(pair[0].id, out _), Is.False);
            Assert.That(model.TryGetMovableState(pair[1].id, out _), Is.False);
            Assert.That(model.TryGetMovableState(output.id, out _), Is.True);
            Assert.That(model.Snapshot(15d).players[0].combinedTotal,
                Is.EqualTo(before.players[0].combinedTotal + 1));
            Assert.That(model.TryCombineAt(0, pair[0].id, pair[1].id, contactRequest, 15d,
                Vector2.one * .5f, Vector2.zero, out _, out reason, passiveContact: true), Is.False);
            Assert.That(reason, Is.EqualTo("DUPLICATE_REQUEST"));
            Assert.That(model.TryGetMovableState(output.id, out _), Is.True);
        }

        [Test]
        public void OverheatedLaunchConsumesOrbWithoutAProjectileAndDirectBurnCannotConsumeDrumsOrOtherOwners()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var combined = Combine(0, Pair(0, 14d), 14d);
            var other = Generate(20, 14d);
            model.Tick(15d);
            Assert.That(model.TryLaunch(0, combined.id, Request(), 15d, out var shot, out var reason), Is.True);
            Assert.That(shot, Is.Null);
            Assert.That(reason, Is.EqualTo("JIGWI_BURNED"));
            Assert.That(model.TryGetMovableState(combined.id, out _), Is.False);
            Assert.That(model.TryJigwiBurn(0, other.id, null, Request(), 15d, out _), Is.False);
            var drum = model.Snapshot(15d).drums.First(item => item.owner == 0);
            Assert.That(model.TryJigwiBurn(0, drum.id, null, Request(), 15d, out _), Is.False);
            Assert.That(model.TryGetMovableState(other.id, out _), Is.True);
            Assert.That(model.TryGetMovableState(drum.id, out _), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        public void SingleOrbBurnAcceptsAbsentOrJsonRoundTrippedEmptySecondId(string optionalSecond)
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var raw = Generate(0, 14d);
            Assert.That(model.TryJigwiBurn(0, raw.id, optionalSecond, Request(), 15d, out var reason), Is.True, reason);
            Assert.That(reason, Is.EqualTo("JIGWI_BURNED"));
            Assert.That(model.TryGetMovableState(raw.id, out _), Is.False);
        }

        [TestCase(LabOrbKind.Potion, 43)]
        [TestCase(LabOrbKind.SecretBox, 42)]
        public void DelayedItemUseDuringOverheatConsumesTheItemWithoutHealingOrOpeningIt(LabOrbKind kind, int chanceField)
        {
            var values = config.CaptureDeveloperValues();
            for (int i = 39; i <= 44; i++) values[i] = i == chanceField ? 100f : 0f;
            values[46] = values[47] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.True);
            var room = new LabModel();
            room.BeginSolo("jigwi-late-item", 0, 0d, config);
            Assert.That(room.TryStartJigwiFire(0, 0d, out _), Is.True);
            Assert.That(room.TryGenerate(0, "item", 14d, out var item, out _), Is.True);
            Assert.That(item.kind, Is.EqualTo(kind));
            Assert.That(room.TryApplyPlayerDamage(0, 100, 14d), Is.True);
            var before = room.Snapshot(15d);
            Assert.That(room.TryUseItemAt(0, item.id, "late-use", 15d, Vector2.one * .5f,
                out var output, out var reason), Is.True, reason);
            Assert.That(reason, Is.EqualTo("JIGWI_BURNED"));
            Assert.That(output, Is.Null);
            var after = room.Snapshot(15d);
            Assert.That(after.orbs, Is.Empty, "The SecretBox must not create two new Combined orbs.");
            Assert.That(after.players[0].personalHp, Is.EqualTo(before.players[0].personalHp));
            Assert.That(after.players[0].potionUsed, Is.EqualTo(before.players[0].potionUsed));
            Assert.That(after.players[0].secretBoxUsed, Is.EqualTo(before.players[0].secretBoxUsed));
            Assert.That(room.TryUseItemAt(0, item.id, "late-use", 15d, Vector2.one * .5f,
                out _, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("DUPLICATE_REQUEST"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticCharmSkipsFireAndOverheatRegardlessOfTickSize(bool stepped)
        {
            var values = config.CaptureDeveloperValues();
            values[166] = 1f;
            values[206] = 2f; // the borrowed unique group (#311), charm only
            values[171] = 0f; values[172] = 100f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.True);
            var room = new LabModel();
            room.BeginSolo("jigwi-block-charm", 0, 0d, config);
            Assert.That(room.TryStartJigwiFire(0, 0d, out _), Is.True);
            if (stepped) for (int i = 1; i < 20; i++) room.Tick(i);
            room.Tick(20d);
            var casts = room.DrainSabotages();
            Assert.That(casts.Length, Is.EqualTo(1));
            Assert.That(casts[0].Kind, Is.EqualTo(LabSabotageKind.Charm));
            Assert.That(casts[0].At, Is.EqualTo(20d), "Water throwing remains possible for the entire fire challenge.");
        }

        [Test]
        public void GenerationBurnAloneDoesNotRemoveThePlayerFromTheAutomaticSabotageLottery()
        {
            var values = config.CaptureDeveloperValues();
            values[166] = 1f;
            values[206] = 2f; // the borrowed unique group (#311), charm only
            values[171] = 0f; values[172] = 100f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.True);
            var room = new LabModel();
            room.BeginSolo("jigwi-burn-charm", 0, 0d, config);
            Assert.That(room.TryStartJigwiGenerationBurn(0, 0d, out _), Is.True);
            room.Tick(2d);
            Assert.That(room.DrainSabotages().Single().Kind, Is.EqualTo(LabSabotageKind.Charm));
        }

        [Test]
        public void OverheatDoesNotStopAutomaticHandoffsAndCannotDestroyAnOrbWithoutAnExplicitAction()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var raw = Generate(0, 14d);
            model.Tick(15d);
            Assert.That(model.TryTransferAt(0, raw.id, true, new Vector2(1f, .5f), Vector2.right,
                Request(), 15d, LabEdgeCrossingKind.Swipe, out var received, out var reason), Is.True, reason);
            Assert.That(received.owner, Is.EqualTo(20ul));
            Assert.That(model.TryGetMovableState(received.id, out _), Is.True);
        }

        [Test]
        public void GenerationBurnOnlyMarksFreshRawAndKeepsExpiryAndSizeAcrossHandoffs()
        {
            var old = Generate(0, 0d);
            Assert.That(model.TryStartJigwiGenerationBurn(0, 1d, out _), Is.True);
            var burned = Generate(0, 1d);
            Assert.That(burned.jigwiGenerationBurn, Is.True);
            Assert.That(burned.jigwiRadiusScale, Is.EqualTo(.65f));
            Assert.That(burned.expiresAt, Is.EqualTo(5d));
            Assert.That(burned.small, Is.False, "The separate bomb-shrink damage rule must not be changed.");
            Assert.That(model.TryGetMovableState(old.id, out var unchanged), Is.True);
            Assert.That(unchanged.jigwiGenerationBurn, Is.False);
            Assert.That(model.TryTransferAt(0, burned.id, true, new Vector2(1f, .5f), Vector2.right,
                Request(), 2d, LabEdgeCrossingKind.Swipe, out var received, out var reason), Is.True, reason);
            Assert.That(received.expiresAt, Is.EqualTo(burned.expiresAt));
            Assert.That(received.jigwiRadiusScale, Is.EqualTo(burned.jigwiRadiusScale));
            Assert.That(received.LifeSecondsLeft(2d, 8f, 15f), Is.EqualTo(3f));
            model.Tick(5d);
            Assert.That(model.TryGetMovableState(received.id, out _), Is.False);
            Assert.That(model.DrainOrbExpiries().Single().Owner, Is.EqualTo(20ul));
        }

        [Test]
        public void CombiningBurnedRawRestoresNormalCombinedLifetimeAndRadius()
        {
            Assert.That(model.TryStartJigwiGenerationBurn(0, 0d, out _), Is.True);
            var combined = Combine(0, Pair(0, 1d), 1d);
            Assert.That(combined.jigwiGenerationBurn || combined.jigwiSupply, Is.False);
            Assert.That(combined.jigwiRadiusScale, Is.EqualTo(1f));
            Assert.That(combined.expiresAt, Is.EqualTo(-1d));
            model.Tick(5d);
            Assert.That(model.TryGetMovableState(combined.id, out _), Is.True);
            model.Tick(9d);
            Assert.That(model.TryGetMovableState(combined.id, out _), Is.False);
        }

        [Test]
        public void GenerationBurnStopsExactlyAtItsDeadlineAndUnaffectedItemsKeepTheirLifetime()
        {
            Assert.That(model.TryStartJigwiGenerationBurn(0, 0d, out _), Is.True);
            var ordinary = Generate(0, 10d);
            Assert.That(ordinary.jigwiGenerationBurn, Is.False);
            Assert.That(ordinary.expiresAt, Is.EqualTo(-1d));
            var values = config.CaptureDeveloperValues();
            values[39] = values[40] = 0f;
            values[44] = 100f;
            Assert.That(config.TryApplyDeveloperValues(values, out _), Is.True);
            var itemRoom = new LabModel();
            itemRoom.BeginSolo("jigwi-item", 0, 0d, config);
            Assert.That(itemRoom.TryStartJigwiGenerationBurn(0, 0d, out _), Is.True);
            Assert.That(itemRoom.TryGenerate(0, "item", 0d, out var item, out _), Is.True);
            Assert.That(item.kind, Is.EqualTo(LabOrbKind.Bomb));
            Assert.That(item.jigwiGenerationBurn, Is.False);
            Assert.That(item.jigwiRadiusScale, Is.EqualTo(1f));
            Assert.That(item.expiresAt, Is.EqualTo(-1d));
        }

        [Test]
        public void StartingAnotherFirePreservesOldRawCombinedAndUnusedWaterSupplies()
        {
            var raw = Generate(0, 0d);
            var combined = Combine(0, Pair(0, 0d), 0d);
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            var snapshot = model.Snapshot(0d);
            var fire = snapshot.JigwiFor(0);
            var water = snapshot.orbs.First(orb => orb.jigwiSupply);
            var unused = snapshot.orbs.Where(orb => orb.jigwiSupply && orb.id != water.id).Select(orb => orb.id).ToArray();
            Assert.That(model.TryLaunch(0, water.id, Request(), 0d, out _, out _), Is.True);
            Assert.That(model.ConfirmJigwiFireHit(water.id, fire.fireId, fire.targets[0].id, .1d, out _), Is.True);
            Assert.That(model.TryStartJigwiFire(0, .2d, out _), Is.True);
            foreach (string id in unused.Concat(new[] { raw.id, combined.id }))
                Assert.That(model.TryGetMovableState(id, out _), Is.True, id);
        }

        [Test]
        public void SupplySafetyLimitRejectsTheEntireNewChallengeWithoutDeletingOrReplacingAnything()
        {
            int casts = 0;
            while (true)
            {
                var before = model.Snapshot(0d);
                if (!model.TryStartJigwiFire(0, 0d, out var reason))
                {
                    Assert.That(reason, Is.EqualTo("JIGWI_SUPPLY_LIMIT"));
                    var after = model.Snapshot(0d);
                    Assert.That(after.orbs.Select(orb => orb.id), Is.EqualTo(before.orbs.Select(orb => orb.id)));
                    Assert.That(after.JigwiFor(0).fireId, Is.EqualTo(before.JigwiFor(0).fireId));
                    Assert.That(after.orbs.Length, Is.LessThanOrEqualTo(LabModel.MaximumJigwiSuppliesPerPlayer));
                    break;
                }
                Assert.That(++casts, Is.LessThan(100));
                var snapshot = model.Snapshot(0d);
                var fire = snapshot.JigwiFor(0);
                var water = snapshot.orbs.First(orb => orb.jigwiSupply);
                Assert.That(model.TryLaunch(0, water.id, Request(), 0d, out _, out _), Is.True);
                Assert.That(model.ConfirmJigwiFireHit(water.id, fire.fireId, fire.targets[0].id, 0d, out bool extinguished), Is.True);
                Assert.That(extinguished, Is.True);
            }
        }

        [Test]
        public void RetryClearsFireOverheatAndGenerationBurnInsteadOfReplayingThem()
        {
            Assert.That(model.TryStartJigwiFire(0, 0d, out _), Is.True);
            Assert.That(model.TryStartJigwiGenerationBurn(20, 0d, out _), Is.True);
            model.Tick(180d);
            model.Begin("jigwi-core-tests", new ulong[] { 0, 20 }, 180d, config);
            var snapshot = model.Snapshot(180d);
            Assert.That(snapshot.round, Is.EqualTo(2u));
            Assert.That(snapshot.jigwi, Is.Empty);
            Assert.That(snapshot.orbs, Is.Empty);
            Assert.That(snapshot.players.All(player => player.stamina == config.StaminaStart), Is.True);
        }

        [Test]
        public void FrozenBattleClockDoesNotUseRealWorldDelayForFireOrBurnedOrbExpiry()
        {
            var clock = new LabBattleClock();
            Assert.That(model.TryStartJigwiGenerationBurn(0, clock.Now(0d), out _), Is.True);
            var burned = Generate(0, clock.Now(0d));
            Assert.That(clock.Pause(1d), Is.True);
            model.Tick(clock.Now(101d));
            Assert.That(model.TryGetMovableState(burned.id, out _), Is.True);
            Assert.That(clock.Resume(101d), Is.True);
            model.Tick(clock.Now(104d));
            Assert.That(model.TryGetMovableState(burned.id, out _), Is.False);
        }
    }
}
