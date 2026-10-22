using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabStageYokaiTests
    {
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            var values = config.CaptureDeveloperValues();
            values[87] = values[88] = 5f;
            values[152] = 12f; values[153] = 2f; values[154] = 0f;
            values[167] = values[168] = 3f;
            values[192] = values[193] = 4f;
            Apply(values);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private void Apply(float[] values) => Assert.That(config.TryApplyDeveloperValues(values,
            out string error), Is.True, error);

        private LabModel Room(string id, LabStageYokai kind, bool solo = false)
        {
            var model = new LabModel();
            if (solo) model.BeginSolo(id, 0, 0d, config, kind);
            else model.Begin(id, new ulong[] { 0, 20, 30 }, 0d, config, kind);
            return model;
        }

        // Independently started models allocate fresh GUIDs and snapshots sort by those GUIDs.
        // Compare every gameplay field and its multiplicity, not unrelated entity identities/order.
        private static LabOrbState[] OrbsWithoutRandomIds(LabOrbState[] orbs) => orbs.Select(orb =>
        {
            var copy = orb.Copy();
            copy.id = string.Empty;
            return copy;
        }).OrderBy(orb => JsonUtility.ToJson(orb), StringComparer.Ordinal).ToArray();

        private static string[] OrbValues(LabOrbState[] orbs) => OrbsWithoutRandomIds(orbs)
            .Select(orb => JsonUtility.ToJson(orb)).ToArray();

        private static string SnapshotValues(LabSnapshot snapshot)
        {
            snapshot.orbs = OrbsWithoutRandomIds(snapshot.orbs);
            snapshot.drums = OrbsWithoutRandomIds(snapshot.drums);
            return JsonUtility.ToJson(snapshot);
        }

        [Test]
        public void ExplicitNoneMatchesTheExistingLabEntryPointEvenWhenAutoJigwiWeightsAreEnabled()
        {
            var implicitNone = new LabModel();
            implicitNone.Begin("same-lab", new ulong[] { 0, 20, 30 }, 0d, config);
            var explicitNone = Room("same-lab", LabStageYokai.None);
            Assert.That(SnapshotValues(explicitNone.Snapshot(90d)),
                Is.EqualTo(SnapshotValues(implicitNone.Snapshot(90d))));
            Assert.That(explicitNone.StageYokai, Is.EqualTo(LabStageYokai.None));
            Assert.That(explicitNone.Snapshot(90d).jigwi, Is.Empty);
        }

        [TestCase(LabStageYokai.Gumiho)]
        [TestCase(LabStageYokai.Yaguang)]
        [TestCase(LabStageYokai.Jigwi)]
        public void EveryStageKeepsCommonBombAndSwipeBlockTimersEvenWhenLegacyMixIsOn(LabStageYokai kind)
        {
            var values = config.CaptureDeveloperValues();
            values[166] = 1f;
            values[171] = values[172] = values[194] = values[195] = 0f;
            Apply(values);
            var model = Room("common-" + kind, kind);
            var blocked = model.Snapshot(12d);
            Assert.That(blocked.swipeBlockStartedAt, Is.EqualTo(12d));
            Assert.That(blocked.swipeBlockedUntil, Is.EqualTo(14d));
            model.Tick(20d);
            Assert.That(model.DrainGourdBombs().Length, Is.EqualTo(4), "The independent 5 s bomb timer remains active.");
            Assert.That(model.DrainSabotages(), Is.Empty, "The Lab's legacy mix must not add duplicate common casts.");
        }

        [Test]
        public void NoneRetainsTheLegacyMixInsteadOfAlsoStartingTheIndependentCommonTimers()
        {
            var values = config.CaptureDeveloperValues();
            values[166] = 1f;
            values[169] = 1f; values[170] = values[171] = values[172] = 0f;
            Apply(values);
            var model = Room("legacy-mix", LabStageYokai.None);
            var snapshot = model.Snapshot(12d);
            Assert.That(model.DrainGourdBombs().Length, Is.EqualTo(4), "The mix casts at 3/6/9/12, not the 5 s common timer.");
            Assert.That(model.DrainSabotages().Select(cast => cast.At), Is.EqualTo(new[] { 3d, 6d, 9d, 12d }));
            Assert.That(snapshot.swipeBlockedUntil, Is.EqualTo(-1d));
            Assert.That(snapshot.jigwi, Is.Empty);
        }

        [Test]
        public void GumihoAutomaticallyUsesOnlyFoxFireAndCharmWhileOtherYokaiFlagsCannotLeakIn()
        {
            var values = config.CaptureDeveloperValues();
            values[155] = values[184] = 1f;
            Apply(values);
            var model = Room("gumiho-only", LabStageYokai.Gumiho);
            var snapshot = model.Snapshot(60d);
            var casts = model.DrainSabotages();
            Assert.That(casts.Length, Is.GreaterThan(0));
            Assert.That(casts.All(cast => cast.Kind == LabSabotageKind.FoxFire || cast.Kind == LabSabotageKind.Charm), Is.True);
            Assert.That(snapshot.comboCount, Is.Zero);
            Assert.That(snapshot.flashCount, Is.Zero);
            Assert.That(snapshot.jigwi, Is.Empty);
            Assert.That(model.DrainGourdBombs().Length, Is.EqualTo(12));
        }

        [Test]
        public void GumihoLargeTickMatchesQuarterSecondTicksWithoutFutureBombsAffectingEarlierFoxCapacity()
        {
            var values = config.CaptureDeveloperValues();
            values[9] = 5f;
            values[171] = 1f; values[172] = 0f;
            values[173] = values[174] = 3f;
            Apply(values);
            var late = Room("stage-clock-gumiho", LabStageYokai.Gumiho);
            var stepped = Room("stage-clock-gumiho", LabStageYokai.Gumiho);
            for (int i = 1; i <= 360; i++) stepped.Tick(i * .25d);
            late.Tick(90d);
            Assert.That(late.DrainSabotages().Select(cast => (cast.Kind, cast.Target, cast.At)),
                Is.EqualTo(stepped.DrainSabotages().Select(cast => (cast.Kind, cast.Target, cast.At))));
            Assert.That(OrbValues(late.Snapshot(90d).orbs),
                Is.EqualTo(OrbValues(stepped.Snapshot(90d).orbs)));
        }

        [Test]
        public void YaguangRunsTheConfiguredComboAndFlashButNeverTheGumihoMixOrAutomaticJigwi()
        {
            var values = config.CaptureDeveloperValues();
            values[87] = values[88] = values[152] = 0f;
            values[166] = values[155] = values[184] = 1f;
            Apply(values);
            var model = Room("yaguang-only", LabStageYokai.Yaguang);
            for (int i = 1; i <= 600; i++) model.Tick(i * .25d);
            var snapshot = model.Snapshot(150d);
            Assert.That(snapshot.comboCount, Is.GreaterThan(0));
            Assert.That(snapshot.flashCount, Is.GreaterThan(0));
            Assert.That(model.DrainSabotages(), Is.Empty);
            Assert.That(snapshot.jigwi, Is.Empty);
        }

        [Test]
        public void YaguangLargeTickPreservesTheSameScreenEventOrderAndRestAsQuarterSecondTicks()
        {
            var values = config.CaptureDeveloperValues();
            values[155] = values[184] = 1f;
            Apply(values);
            var late = Room("stage-clock-yaguang", LabStageYokai.Yaguang);
            var stepped = Room("stage-clock-yaguang", LabStageYokai.Yaguang);
            for (int i = 1; i <= 600; i++) stepped.Tick(i * .25d);
            late.Tick(150d);
            var actual = late.Snapshot(150d);
            var expected = stepped.Snapshot(150d);
            Assert.That((actual.flashCount, actual.flashLastAt, actual.flashAt),
                Is.EqualTo((expected.flashCount, expected.flashLastAt, expected.flashAt)));
            Assert.That((actual.comboCount, actual.comboStartedAt, actual.comboNextAt),
                Is.EqualTo((expected.comboCount, expected.comboStartedAt, expected.comboNextAt)));
            Assert.That(OrbValues(actual.orbs), Is.EqualTo(OrbValues(expected.orbs)));
        }

        [Test]
        public void JigwiAutoFireUsesTheSameFreeSupplyAndTimeoutRulesWithoutRecursiveTimeAdvance()
        {
            var values = config.CaptureDeveloperValues();
            values[87] = values[88] = values[152] = 0f;
            values[155] = values[184] = 1f;
            values[192] = values[193] = 1f;
            values[194] = 1f; values[195] = 0f;
            values[179] = 1f; values[180] = .1f;
            Apply(values);
            var late = Room("stage-clock-jigwi", LabStageYokai.Jigwi, solo: true);
            var stepped = Room("stage-clock-jigwi", LabStageYokai.Jigwi, solo: true);
            for (int i = 1; i <= 400; i++) stepped.Tick(i * .25d);
            late.Tick(100d);
            var actual = late.Snapshot(100d);
            var expected = stepped.Snapshot(100d);
            Assert.That(actual.JigwiFor(0).fireStartedAt, Is.EqualTo(99d),
                "Expired supplies cannot fill the 64 cap and permanently stall a late Tick.");
            Assert.That(actual.JigwiFor(0).fireId, Does.EndWith("/50"));
            Assert.That(JsonUtility.ToJson(actual.JigwiFor(0)), Is.EqualTo(JsonUtility.ToJson(expected.JigwiFor(0))));
            Assert.That(OrbValues(actual.orbs), Is.EqualTo(OrbValues(expected.orbs)));
            Assert.That(actual.players[0].stamina, Is.EqualTo(config.StaminaStart));
            Assert.That(actual.players[0].generatedTotal, Is.Zero);
            Assert.That(actual.comboCount, Is.Zero);
            Assert.That(actual.flashCount, Is.Zero);
        }

        [Test]
        public void ActiveJigwiChallengeIsNotRepeatedOrRefreshedByLaterAutoTicks()
        {
            var values = config.CaptureDeveloperValues();
            values[87] = values[88] = values[152] = 0f;
            values[192] = values[193] = 2f;
            values[194] = 1f; values[195] = 0f;
            values[179] = 60f;
            Apply(values);
            var model = Room("one-active-jigwi", LabStageYokai.Jigwi, solo: true);
            var snapshot = model.Snapshot(30d);
            Assert.That(snapshot.JigwiFor(0).fireStartedAt, Is.EqualTo(2d));
            Assert.That(snapshot.JigwiFor(0).fireEndsAt, Is.EqualTo(62d));
            Assert.That(snapshot.JigwiFor(0).fireId, Does.EndWith("/1"));
            Assert.That(snapshot.orbs.Count(orb => orb.jigwiSupply), Is.EqualTo(config.JigwiFireTargetCount + 2));
        }

        [TestCase(LabStageYokai.None, true)]
        [TestCase(LabStageYokai.Gumiho, false)]
        [TestCase(LabStageYokai.Yaguang, false)]
        [TestCase(LabStageYokai.Jigwi, true)]
        public void ManualJigwiTriggersCannotLeakIntoOtherYokaiStagesButRemainAvailableInLab(
            LabStageYokai kind, bool allowed)
        {
            var fire = Room("manual-fire-" + kind, kind, solo: true);
            Assert.That(fire.TryStartJigwiFire(0, 0d, out string fireReason), Is.EqualTo(allowed));
            var fireSnapshot = fire.Snapshot(0d);
            if (allowed)
            {
                Assert.That(fireSnapshot.JigwiFor(0).FireActive(0d), Is.True);
                Assert.That(fireSnapshot.orbs.Count(orb => orb.jigwiSupply), Is.EqualTo(config.JigwiFireTargetCount + 2));
            }
            else
            {
                Assert.That(fireReason, Is.EqualTo("STAGE_PATTERN_NOT_ALLOWED"));
                Assert.That(fireSnapshot.jigwi, Is.Empty);
                Assert.That(fireSnapshot.orbs, Is.Empty);
            }

            var burn = Room("manual-generation-burn-" + kind, kind, solo: true);
            Assert.That(burn.TryStartJigwiGenerationBurn(0, 0d, out string burnReason), Is.EqualTo(allowed));
            var burnSnapshot = burn.Snapshot(0d);
            if (allowed) Assert.That(burnSnapshot.JigwiFor(0).GenerationBurnActive(0d), Is.True);
            else
            {
                Assert.That(burnReason, Is.EqualTo("STAGE_PATTERN_NOT_ALLOWED"));
                Assert.That(burnSnapshot.jigwi, Is.Empty);
                Assert.That(burnSnapshot.orbs, Is.Empty);
            }
        }

        [Test]
        public void AutoGenerationBurnIsAvailableAndBothZeroWeightsDisableOnlyTheUniquePattern()
        {
            var values = config.CaptureDeveloperValues();
            values[194] = 0f; values[195] = 1f;
            Apply(values);
            var model = Room("auto-generation-burn", LabStageYokai.Jigwi);
            var snapshot = model.Snapshot(4d);
            Assert.That(snapshot.jigwi.Length, Is.EqualTo(1));
            Assert.That(snapshot.jigwi[0].GenerationBurnActive(4d), Is.True);
            Assert.That(snapshot.orbs, Is.Empty);
            values[195] = 0f;
            Apply(values);
            var disabled = Room("no-unique-jigwi", LabStageYokai.Jigwi);
            Assert.That(disabled.Snapshot(20d).jigwi, Is.Empty);
            Assert.That(disabled.DrainGourdBombs().Length, Is.EqualTo(4));
        }

        [Test]
        public void NewRoundSwitchesThePolicyAndRetiresEveryPreviousJigwiStateAndSupply()
        {
            var model = Room("stage-switch", LabStageYokai.Jigwi, solo: true);
            model.Tick(180d);
            model.BeginSolo("stage-switch", 0, 180d, config, LabStageYokai.Gumiho);
            var fresh = model.Snapshot(180d);
            Assert.That(model.StageYokai, Is.EqualTo(LabStageYokai.Gumiho));
            Assert.That(fresh.round, Is.EqualTo(2u));
            Assert.That(fresh.jigwi, Is.Empty);
            Assert.That(fresh.orbs, Is.Empty);
            Assert.That(model.DrainSabotages(), Is.Empty);
        }

        [Test]
        public void UnknownStagePolicyIsRejectedBeforeStartingTheRound()
        {
            var model = new LabModel();
            Assert.Throws<ArgumentOutOfRangeException>(() => model.BeginSolo("bad-stage", 0, 0d, config, (LabStageYokai)99));
            Assert.That(model.Phase, Is.EqualTo(LabPhase.Idle));
        }

        [Test]
        public void AutoJigwiSettingsAppendFourRowsAndValidateTheWholeSharedFormAtomically()
        {
            var defaults = ScriptableObject.CreateInstance<LabConfig>();
            try { Assert.That(defaults.CaptureDeveloperValues().Skip(192), Is.EqualTo(new[] { 20f, 30f, 1f, 1f })); }
            finally { Object.DestroyImmediate(defaults); }
            var baseline = config.CaptureDeveloperValues();
            Assert.That(baseline.Length, Is.EqualTo(196));
            Assert.That(baseline.Skip(192), Is.EqualTo(new[] { 4f, 4f, 1f, 1f }));
            baseline[192] = 20f; baseline[193] = 30f;
            Apply(baseline);
            var invalid = (float[])baseline.Clone();
            invalid[0] = 777f;
            invalid[192] = 31f;
            Assert.That(config.TryApplyDeveloperValues(invalid, out _), Is.False);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            Assert.That(LabConfig.DeveloperCategories.SelectMany(category => category.FieldIndices).OrderBy(index => index),
                Is.EqualTo(Enumerable.Range(0, 196)));
        }
    }
}
