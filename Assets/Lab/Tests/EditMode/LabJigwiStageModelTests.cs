using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Stage A/B use real Host clocks and normal generation/launch/hit paths.</summary>
    public sealed class LabJigwiStageModelTests
    {
        private LabConfig config;
        private int request;
        private static readonly ulong[] Players = { 0ul, 20ul };

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[3] = 1000f; values[4] = 100f;
            values[5] = 1f; values[6] = 0f; values[7] = 1f;
            values[9] = 100f;
            values[74] = 2f; // Two real drums give both possible A targets the normal generation input.
            values[39] = values[40] = 50f;
            for (int i = 41; i <= 44; i++) values[i] = 0f;
            values[207] = values[208] = 0f;
            values[228] = 1f; values[229] = 1f; values[230] = 7f;
            values[231] = 18f; values[232] = 30f; values[233] = 1f; values[234] = 3f; values[235] = 2f;
            Apply(values);
        }
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);
        private void Apply(float[] values) => Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        private LabModel Room(LabStageYokai stage = LabStageYokai.Jigwi, string id = "jigwi-stage")
        {
            var room = new LabModel();
            room.Begin(id, Players, 0d, config, stage);
            return room;
        }
        private static LabJigwiStageSnapshot State(LabModel room, double now) => room.Snapshot(now).jigwiStage;
        private static void Bind(LabModel room, LabJigwiStageGroup group, double now)
        {
            Assert.That(room.SetJigwiStageGeometry(group.id, group.rootDirection, group.patchDirections, now), Is.True,
                "This model fixture explicitly supplies known local surface anchors; it does not prove a mesh raycast.");
        }
        private string NextRequest() => "stage-" + ++request;
        private LabOrbState Combined(LabModel room, ulong owner, double now)
        {
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 24 && (yin == null || yang == null); i++)
            {
                Assert.That(room.TryGenerate(owner, NextRequest(), now, out var orb, out string reason), Is.True, reason);
                if (orb.kind == LabOrbKind.Yin) yin = orb;
                else if (orb.kind == LabOrbKind.Yang) yang = orb;
            }
            Assert.That(yin, Is.Not.Null); Assert.That(yang, Is.Not.Null);
            Assert.That(room.TryCombineAt(owner, yin.id, yang.id, NextRequest(), now, new Vector2(.5f, .5f),
                Vector2.zero, out var combined, out string combineReason), Is.True, combineReason);
            return combined;
        }
        private void Launch(LabModel room, ulong owner, LabOrbState combined, double now)
        {
            Assert.That(room.TryLaunch(owner, combined.id, NextRequest(), now, out _, out string reason), Is.True, reason);
        }

        [TestCase(LabStageYokai.None, false)]
        [TestCase(LabStageYokai.Gumiho, false)]
        [TestCase(LabStageYokai.Yaguang, false)]
        [TestCase(LabStageYokai.Jigwi, true)]
        public void PolicyIsScopedToJigwiAndHasNoWaterSupply(LabStageYokai stage, bool enabled)
        {
            var room = Room(stage);
            Assert.That(room.JigwiStageEnabled, Is.EqualTo(enabled));
            if (enabled) Assert.That(room.Snapshot(0d).remaining, Is.EqualTo(120d));
            var snapshot = room.Snapshot(11d);
            Assert.That(snapshot.jigwiStage.enabled, Is.EqualTo(enabled));
            if (enabled)
            {
                Assert.That(snapshot.jigwi, Is.Empty);
                Assert.That(snapshot.orbs.Any(orb => orb.jigwiSupply || orb.jigwiGenerationBurn), Is.False);
            }
        }

        [Test]
        public void IgnitionHasOneSecondWarningSevenSecondActiveAndFiniteSpread()
        {
            var room = Room();
            Assert.That(State(room, 9.99d).groups, Is.Empty);
            var warning = State(room, 10d).groups.Single();
            Assert.That(warning.Warning(10d), Is.True);
            Assert.That(warning.Active(10d), Is.False);
            Assert.That(warning.VisiblePatchCount(10d), Is.Zero);
            Bind(room, warning, 10d);
            var first = State(room, 11d).groups.Single();
            Assert.That(first.Active(11d), Is.True);
            Assert.That(first.VisiblePatchCount(11d), Is.EqualTo(1));
            Assert.That(State(room, 12d).groups.Single().VisiblePatchCount(12d), Is.EqualTo(2));
            Assert.That(State(room, 13d).groups.Single().VisiblePatchCount(13d), Is.EqualTo(3));
            Assert.That(State(room, 17.99d).groups.Single().Active(17.99d), Is.True);
            var end = State(room, 18d).groups.Single();
            Assert.That(end.Active(18d), Is.False);
            Assert.That(end.resolution, Is.EqualTo(LabJigwiStageResolution.Expired));
            Assert.That(end.resolvedAt, Is.EqualTo(18d));
        }

        [Test]
        public void OneLateTickProducesExactlyTheEightSlotsAndTenIndependentGroups()
        {
            var room = Room();
            var state = State(room, 98d);
            Assert.That(state.groups.Length, Is.EqualTo(10));
            int[] perSlot = Enumerable.Range(0, 8).Select(slot => state.groups.Count(group => group.slot == slot)).ToArray();
            Assert.That(perSlot, Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1, 2, 2 }));
            Assert.That(state.groups.Select(group => group.id).Distinct().Count(), Is.EqualTo(10));
            Assert.That(state.groups.All(group => group.resolution == LabJigwiStageResolution.Expired), Is.True);
            Assert.That(State(room, 98d).groups.Length, Is.EqualTo(10));
            Assert.That(LabJigwiStageNetworkValidation.Valid(state, room.Snapshot(98d).players), Is.True);
        }

        [Test]
        public void FireOnlyBlocksTheAffectedPlayerAndExpiresWithoutPenalty()
        {
            var room = Room();
            var fire = State(room, 11d).groups.Single();
            Bind(room, fire, 11d);
            ulong other = Players.Single(player => player != fire.affectedPlayer);
            Assert.That(room.JigwiStagePaintBlocked(fire.affectedPlayer, fire.patchDirections[0]), Is.True);
            Assert.That(room.JigwiStagePaintBlocked(other, fire.patchDirections[0]), Is.False);
            var before = room.Snapshot(11d);
            var ended = room.Snapshot(18d);
            Assert.That(room.JigwiStagePaintBlocked(fire.affectedPlayer, fire.patchDirections[0]), Is.False);
            Assert.That(ended.players.Select(p => p.personalHp), Is.EqualTo(before.players.Select(p => p.personalHp)));
            Assert.That(ended.jigwi, Is.Empty, "No legacy overheat is produced by a new A expiry.");
        }

        [Test]
        public void CurtainInformationAndFirstPracticalSwapHelperRoles()
        {
            var room = Room();
            var info = State(room, 31d).groups.Single(group => group.slot == 1);
            var practical = State(room, 51d).groups.Single(group => group.slot == 3);
            Assert.That(info.helperPlayer, Is.Not.EqualTo(info.affectedPlayer));
            Assert.That(practical.affectedPlayer, Is.EqualTo(info.helperPlayer));
            Assert.That(practical.helperPlayer, Is.EqualTo(info.affectedPlayer));
            Assert.That(info.pattern, Is.EqualTo(LabJigwiStagePattern.Curtain));
        }

        [Test]
        public void MutualHelpClearsEachVictimIndependentlyAndRejectsWrongHelper()
        {
            var room = Room();
            var pair = State(room, 91d).groups.Where(group => group.slot == 7).ToArray();
            Assert.That(pair, Has.Length.EqualTo(2));
            foreach (var group in pair) Bind(room, group, 91d);
            Assert.That(pair[0].helperPlayer, Is.EqualTo(pair[1].affectedPlayer));
            Assert.That(pair[1].helperPlayer, Is.EqualTo(pair[0].affectedPlayer));
            Assert.That(room.TryExtinguishJigwiStageRoot(pair[0].affectedPlayer, pair[0].rootDirection, 91d, out _), Is.False);
            Assert.That(room.TryExtinguishJigwiStageRoot(pair[0].helperPlayer, pair[0].rootDirection, 91d, out string cleared), Is.True);
            Assert.That(cleared, Is.EqualTo(pair[0].id));
            var after = State(room, 91d).groups;
            Assert.That(after.Single(group => group.id == pair[0].id).resolution, Is.EqualTo(LabJigwiStageResolution.Extinguished));
            Assert.That(after.Single(group => group.id == pair[1].id).Active(91d), Is.True);
            Assert.That(room.TryExtinguishJigwiStageRoot(pair[0].helperPlayer, pair[0].rootDirection, 91d, out _), Is.False);
            Assert.That(room.TryExtinguishJigwiStageRoot(pair[1].helperPlayer, pair[1].rootDirection, 91d, out _), Is.True);
        }

        [TestCase(LabOrbKind.Relay, 41)]
        [TestCase(LabOrbKind.Bomb, 44)]
        public void NonPaintableUnlockedItemsCannotExtinguishTheSurfaceRoot(LabOrbKind kind, int chanceField)
        {
            float[] values = config.CaptureDeveloperValues();
            for (int i = 39; i <= 44; i++) values[i] = 0f;
            values[chanceField] = 100f;
            values[46] = values[47] = 0f;
            Apply(values);
            var room = Room();
            var fire = State(room, 11d).groups.Single();
            Bind(room, fire, 11d);
            Assert.That(room.TryGenerate(fire.helperPlayer, NextRequest(), 11d, out var item, out string reason), Is.True, reason);
            Assert.That(item.kind, Is.EqualTo(kind));
            Assert.That(item.lockSwipes, Is.Zero);
            Assert.That(item.element, Is.EqualTo(LabOrbElement.None), "These items can be thrown but do not supply element paint.");
            Launch(room, fire.helperPlayer, item, 11d);
            Assert.That(room.ConfirmHit(item.id, 11d, fire.rootDirection, out _, out _, out _), Is.True);
            var after = State(room, 11d).groups.Single();
            Assert.That(after.resolution, Is.EqualTo(LabJigwiStageResolution.None));
            Assert.That(after.Active(11d), Is.True);
            Assert.That(room.JigwiStagePaintBlocked(fire.affectedPlayer, fire.patchDirections[0]), Is.True);
            Assert.That(room.PaintSurface.PaintCount, Is.Zero);
            Assert.That(room.PaintSurface.CoveredCount, Is.Zero);
        }

        [Test]
        public void RealRootHitConsumesAndRewardsOnceThenRunsNormalPaint()
        {
            var room = Room();
            var fire = State(room, 11d).groups.Single();
            Bind(room, fire, 11d);
            var combined = Combined(room, fire.helperPlayer, 11d);
            Launch(room, fire.helperPlayer, combined, 11d);
            int paints = room.PaintSurface.PaintCount;
            int hits = room.Snapshot(11d).players.Single(p => p.id == fire.helperPlayer).hitTotal;
            Assert.That(room.ConfirmHit(combined.id, 11d, fire.rootDirection, out _, out _, out _), Is.True);
            Assert.That(State(room, 11d).groups.Single().resolution, Is.EqualTo(LabJigwiStageResolution.Extinguished));
            Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints + 1));
            Assert.That(room.Snapshot(11d).players.Single(p => p.id == fire.helperPlayer).hitTotal, Is.EqualTo(hits + 1));
            Assert.That(room.ConfirmHit(combined.id, 11d, fire.rootDirection, out _, out _, out _), Is.False);
            Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints + 1));
        }

        [Test]
        public void OrdinaryFireHitConsumesAndRewardsButDoesNotSupplyPaintOrClearRoot()
        {
            var room = Room();
            var fire = State(room, 13d).groups.Single();
            Bind(room, fire, 13d);
            Vector3 direction = fire.patchDirections[2];
            Assert.That(fire.ContainsRoot(fire.affectedPlayer, direction, 13d), Is.False);
            var combined = Combined(room, fire.affectedPlayer, 13d);
            Launch(room, fire.affectedPlayer, combined, 13d);
            int paints = room.PaintSurface.PaintCount;
            var before = room.Snapshot(13d).players.Single(p => p.id == fire.affectedPlayer);
            Assert.That(room.ConfirmHit(combined.id, 13d, direction, out _, out _, out _), Is.True);
            var after = room.Snapshot(13d).players.Single(p => p.id == fire.affectedPlayer);
            Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints));
            Assert.That(room.PaintSurface.CoveredCount, Is.Zero);
            Assert.That(after.hitTotal, Is.EqualTo(before.hitTotal + 1));
            Assert.That(after.stamina, Is.EqualTo(Mathf.Min(config.StaminaMax, before.stamina + config.HitRecovery)));
            Assert.That(State(room, 13d).groups.Single().Active(13d), Is.True);
            Assert.That(room.TryGetMovableState(combined.id, out _), Is.False);
        }

        [Test]
        public void IndependentIgnitionRootsDoNotClearTheirSibling()
        {
            var room = Room();
            var pair = State(room, 81d).groups.Where(group => group.slot == 6).ToArray();
            Assert.That(pair, Has.Length.EqualTo(2));
            foreach (var group in pair) Bind(room, group, 81d);
            Assert.That(room.TryExtinguishJigwiStageRoot(pair[0].helperPlayer, pair[0].rootDirection, 81d, out _), Is.True);
            Assert.That(State(room, 81d).groups.Single(group => group.id == pair[1].id).Active(81d), Is.True);
        }

        [Test]
        public void CutoffCancelsLingeringGroupsAndWinsOverSameTimeExpiry()
        {
            float[] values = config.CaptureDeveloperValues(); values[230] = 20f; Apply(values);
            var room = Room();
            var last = State(room, 99.9d).groups.First(group => group.slot == 7);
            Assert.That(last.Active(99.9d), Is.True);
            Bind(room, last, 99.9d);
            Assert.That(room.JigwiStagePaintBlocked(last.affectedPlayer, last.patchDirections[0]), Is.True);
            var atCutoff = State(room, 100d);
            Assert.That(atCutoff.groups.Where(group => group.slot == 7)
                .All(group => group.resolution == LabJigwiStageResolution.Cancelled), Is.True);
            Assert.That(room.TryExtinguishJigwiStageRoot(last.helperPlayer, last.rootDirection, 100d, out _), Is.False);
            Assert.That(room.JigwiStagePaintBlocked(last.affectedPlayer, last.patchDirections[0]), Is.False);
            Assert.That(atCutoff.fever, Is.True);
        }

        [Test]
        public void FeverDoublesOnlyAutomaticRecoveryAfterTheExactBoundary()
        {
            float[] values = config.CaptureDeveloperValues(); values[4] = 0f; values[6] = values[7] = 1f; Apply(values);
            var room = Room();
            Assert.That(room.Snapshot(99.5d).players[0].stamina, Is.EqualTo(99.5f).Within(.0001f));
            Assert.That(room.Snapshot(100d).players[0].stamina, Is.EqualTo(100f).Within(.0001f));
            Assert.That(room.Snapshot(100.5d).players[0].stamina, Is.EqualTo(101f).Within(.0001f));
            Assert.That(room.Snapshot(101d).players[0].stamina, Is.EqualTo(102f).Within(.0001f));
            Assert.That(config.StaminaMax, Is.EqualTo(1000f));
            Assert.That(room.JigwiStageRecoveryMultiplier(99.99d), Is.EqualTo(1f));
            Assert.That(room.JigwiStageRecoveryMultiplier(100d), Is.EqualTo(2f));
        }

        [Test]
        public void UnboundGeometryCannotVisiblySpreadBlockOrExtinguishDuringItsActiveWindow()
        {
            var room = Room();
            var pending = State(room, 11d).groups.Single();
            Assert.That(pending.Active(11d), Is.True, "The clock may be active before a Host mesh sample is available.");
            Assert.That(pending.geometryConfigured, Is.False);
            Assert.That(pending.VisiblePatchCount(11d), Is.Zero);
            Assert.That(pending.Blocks(pending.affectedPlayer, pending.patchDirections[0], 11d), Is.False);
            Assert.That(pending.ContainsRoot(pending.helperPlayer, pending.rootDirection, 11d), Is.False);
            Assert.That(room.JigwiStagePaintBlocked(pending.affectedPlayer, pending.patchDirections[0]), Is.False);
            Assert.That(room.TryExtinguishJigwiStageRoot(pending.helperPlayer, pending.rootDirection, 11d, out _), Is.False);
            Bind(room, pending, 11d);
            var bound = State(room, 11d).groups.Single();
            Assert.That(bound.geometryConfigured, Is.True);
            Assert.That(bound.VisiblePatchCount(11d), Is.EqualTo(1));
            Assert.That(bound.ContainsRoot(bound.helperPlayer, bound.rootDirection, 11d), Is.True);
            Assert.That(room.JigwiStagePaintBlocked(bound.affectedPlayer, bound.patchDirections[0]), Is.True);
        }

        [Test]
        public void GeometryIsHostBoundedAndDoesNotAcceptZeroNanOrOldIds()
        {
            var room = Room();
            var fire = State(room, 10d).groups.Single();
            Assert.That(room.SetJigwiStageGeometry(fire.id, Vector3.up, new[] { Vector3.forward }, 10d), Is.True);
            var changed = State(room, 10d).groups.Single();
            Assert.That(changed.geometryConfigured, Is.True);
            Assert.That(changed.rootDirection, Is.EqualTo(Vector3.up));
            Assert.That(room.SetJigwiStageGeometry(fire.id, Vector3.zero, new[] { Vector3.forward }, 10d), Is.False);
            Assert.That(room.SetJigwiStageGeometry(fire.id, Vector3.up, new[] { new Vector3(float.NaN, 0, 0) }, 10d), Is.False);
            Assert.That(room.SetJigwiStageGeometry("old/round/root", Vector3.up, new[] { Vector3.forward }, 10d), Is.False);
            Assert.That(room.SetJigwiStageGeometry(fire.id, Vector3.down, new[] { Vector3.back }, 11d), Is.True,
                "Host may relocate a hidden root while preserving or explicitly setting its surface patches.");
            Assert.That(LabJigwiStageNetworkValidation.Valid(State(room, 11d), room.Snapshot(11d).players), Is.True);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void LargerRoomsKeepDistinctHelpersAndBoundedHistory(int count)
        {
            ulong[] ids = Enumerable.Range(0, count).Select(n => (ulong)n * 10ul).ToArray();
            var room = new LabModel(); room.Begin("jigwi-many", ids, 0d, config, LabStageYokai.Jigwi);
            var snapshot = room.Snapshot(98d);
            Assert.That(snapshot.jigwiStage.groups, Has.Length.EqualTo(10));
            Assert.That(snapshot.jigwiStage.groups.Where(g => g.pattern == LabJigwiStagePattern.Curtain)
                .All(g => g.affectedPlayer != g.helperPlayer), Is.True);
            Assert.That(LabJigwiStageNetworkValidation.Valid(snapshot.jigwiStage, snapshot.players), Is.True);
        }

        [Test]
        public void RetryRotatesTheFirstSeatAndClearsAllOldGroups()
        {
            var room = Room();
            ulong first = State(room, 10d).groups.Single().affectedPlayer;
            room.Tick(120d);
            room.Begin("jigwi-stage", Players, 121d, config, LabStageYokai.Jigwi);
            Assert.That(State(room, 121d).groups, Is.Empty);
            Assert.That(State(room, 131d).groups.Single().affectedPlayer, Is.Not.EqualTo(first));
        }

        [Test]
        public void DisabledTimelineRetainsTheLegacyStagePolicyAndNullValidationIsCompatible()
        {
            float[] values = config.CaptureDeveloperValues(); values[228] = 0f; Apply(values);
            var room = Room();
            Assert.That(room.JigwiStageEnabled, Is.False);
            Assert.That(State(room, 10d).enabled, Is.False);
            Assert.That(LabJigwiStageNetworkValidation.Valid(null), Is.True);
            Assert.That(LabJigwiStageNetworkValidation.Copy(null).enabled, Is.False);
        }

        [Test]
        public void SnapshotCopyAndValidationRejectMalformedOrNonMemberGroups()
        {
            var room = Room();
            var snapshot = room.Snapshot(11d);
            var copied = LabJigwiStageNetworkValidation.Copy(snapshot.jigwiStage);
            copied.groups[0].patchDirections[0] = Vector3.up;
            Assert.That(snapshot.jigwiStage.groups[0].patchDirections[0], Is.Not.EqualTo(Vector3.up));
            copied = snapshot.jigwiStage.Copy(); copied.groups[0].helperPlayer = 9999;
            Assert.That(LabJigwiStageNetworkValidation.Valid(copied, snapshot.players), Is.False);
            copied = snapshot.jigwiStage.Copy(); copied.groups[0].rootDirection = Vector3.zero;
            Assert.That(LabJigwiStageNetworkValidation.Valid(copied, snapshot.players), Is.False);
            copied = snapshot.jigwiStage.Copy(); copied.groups = new[] { copied.groups[0], copied.groups[0].Copy() };
            Assert.That(LabJigwiStageNetworkValidation.Valid(copied, snapshot.players), Is.False);
            copied = snapshot.jigwiStage.Copy(); copied.cutoffAt = double.NaN;
            Assert.That(LabJigwiStageNetworkValidation.Valid(copied, snapshot.players), Is.False);
        }
        [Test]
        public void RootIdentityIncludesItsRoomEvenWhenBothModelsAreOnRoundOne()
        {
            var first = Room();
            var second = new LabModel();
            second.Begin(new string('x', 128), Players, 0d, config, LabStageYokai.Jigwi);
            var a = first.Snapshot(10d); var b = second.Snapshot(10d);
            Assert.That(a.round, Is.EqualTo(1)); Assert.That(b.round, Is.EqualTo(1));
            Assert.That(a.jigwiStage.groups[0].id, Is.Not.EqualTo(b.jigwiStage.groups[0].id));
            Assert.That(LabJigwiStageNetworkValidation.Valid(b.jigwiStage, b.players), Is.True,
                "The maximum128-character room key still fits the bounded root metadata.");
        }
    }
}
