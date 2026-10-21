using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabLocalBoardAuthorityModelTests
    {
        private LabConfig config;

        [TearDown]
        public void TearDown()
        {
            if (config != null) Object.DestroyImmediate(config);
        }

        [Test]
        public void TargetedLedgerCopiesDoNotTickOrExposeMutableState()
        {
            config = CreateConfig(100, 0, 0, 0, 0, 0);
            LabModel model = BeginRoom();
            ulong owner = model.Snapshot(0d).drums.Single().owner;
            Assert.That(model.TryGenerate(owner, "raw", 0d, out var raw, out _), Is.True);
            ulong revision = model.Revision;
            LabPlayerState[] players = model.CopyPlayers();
            Assert.That(players.Select(player => player.seat), Is.EqualTo(new[] { 0, 1 }));
            players[0].stamina = 0f;
            LabOrbState[] owned = model.CopyOwnedStates(owner);
            Assert.That(owned.Length, Is.EqualTo(2), "The owned drum and generated orb are both included.");
            Assert.That(owned.Any(orb => orb.id == raw.id), Is.True);
            owned.Single(orb => orb.id == raw.id).owner = 999ul;
            Assert.That(model.CopyOwnedStates(owner).Length, Is.EqualTo(2));
            Assert.That(model.CopyPlayers()[0].stamina, Is.GreaterThan(0f));
            Assert.That(model.Revision, Is.EqualTo(revision));
            Assert.That(model.CopyOwnedStates(999ul), Is.Empty);
        }

        [Test]
        public void OwnershipVersionChangesOnlyAtAcceptedHandoffsAndReadsAreIndependent()
        {
            config = CreateConfig(100, 0, 0, 0, 0, 0);
            LabModel model = BeginRoom();
            LabOrbState drum = model.Snapshot(0d).drums.Single();
            ulong owner = drum.owner;
            Assert.That(drum.ownershipVersion, Is.EqualTo(1ul));
            Assert.That(model.TryGenerate(owner, "generate", 0d, out var raw, out _), Is.True);
            Assert.That(raw.ownershipVersion, Is.EqualTo(1ul));
            Assert.That(model.TryUpdateMotion(owner, raw.id, new Vector2(.6f, .4f), Vector2.right, 0d), Is.True);
            Assert.That(model.TryGetMovableState(raw.id, out var read), Is.True);
            Assert.That(read.ownershipVersion, Is.EqualTo(1ul));
            read.ownershipVersion = 99;
            read.x = .1f;
            Assert.That(model.TryGetMovableState(raw.id, out var unchanged), Is.True);
            Assert.That(unchanged.x, Is.EqualTo(.6f));
            Assert.That(unchanged.ownershipVersion, Is.EqualTo(1ul));

            Assert.That(model.TryTransferAt(owner, raw.id, true, new Vector2(1f, .4f),
                Vector2.right, "out", 0d, LabEdgeCrossingKind.Swipe, out var received, out _), Is.True);
            Assert.That(received.ownershipVersion, Is.EqualTo(2ul));
            Assert.That(received.createdAt, Is.EqualTo(raw.createdAt));
            Assert.That(model.TryTransferAt(owner, raw.id, true, new Vector2(1f, .4f),
                Vector2.right, "old-owner", 0d, LabEdgeCrossingKind.Swipe, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("NOT_OWNED_IDLE"));
            Assert.That(model.TryTransferAt(received.owner, raw.id, false, new Vector2(0f, .4f),
                Vector2.left, "back", 0d, LabEdgeCrossingKind.Swipe, out var returned, out _), Is.True);
            Assert.That(returned.owner, Is.EqualTo(owner));
            Assert.That(returned.ownershipVersion, Is.EqualTo(3ul));
            Assert.That(model.TryGetMovableState(drum.id, out var copiedDrum), Is.True);
            Assert.That(copiedDrum.ownershipVersion, Is.EqualTo(1ul));
            Assert.That(model.TryGetMovableState("absent", out var missing), Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void CombineUsesActualLocalCenterWithoutMovingEitherHostInputFirst()
        {
            config = CreateConfig(50, 50, 0, 0, 0, 0);
            var model = new LabModel();
            model.BeginSolo("local-combine", 10ul, 0d, config);
            var pair = GeneratePair(model);
            Vector2 center = new Vector2(.22f, .84f);
            Vector2 velocity = new Vector2(.4f, -.2f);
            Assert.That(model.TryCombineAt(10ul, pair.yin.id, pair.yang.id, "combine", 0d,
                center, velocity, out var combined, out _), Is.True);
            Assert.That(new Vector2(combined.x, combined.y), Is.EqualTo(center));
            Assert.That(new Vector2(combined.vx, combined.vy), Is.EqualTo(velocity));
            Assert.That(combined.kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(combined.ownershipVersion, Is.EqualTo(1ul));
            Assert.That(model.TryGetMovableState(pair.yin.id, out _), Is.False);
            Assert.That(model.TryGetMovableState(pair.yang.id, out _), Is.False);
        }

        [TestCase(-.01f, .5f)]
        [TestCase(1.01f, .5f)]
        [TestCase(.5f, float.NaN)]
        [TestCase(float.PositiveInfinity, .5f)]
        public void RejectedCombineGeometryPreservesInputsStatisticsAndGlobalElementDraw(float x, float y)
        {
            config = CreateConfig(50, 50, 0, 0, 0, 0);
            var model = new LabModel();
            var control = new LabModel();
            model.BeginSolo("same-seed", 10ul, 0d, config);
            control.BeginSolo("same-seed", 10ul, 0d, config);
            var pair = GeneratePair(model);
            var controlPair = GeneratePair(control);
            int before = model.Snapshot(0d).orbs.Length;
            Assert.That(model.TryCombineAt(10ul, pair.yin.id, pair.yang.id, "bad", 0d,
                new Vector2(x, y), Vector2.zero, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_MOTION"));
            Assert.That(model.Snapshot(0d).orbs.Length, Is.EqualTo(before));
            Assert.That(model.Snapshot(0d).players.Single().combinedTotal, Is.Zero);
            Assert.That(model.TryCombineAt(10ul, pair.yin.id, pair.yang.id, "good", 0d,
                new Vector2(.3f, .7f), Vector2.zero, out var combined, out _), Is.True);
            Assert.That(control.TryCombine(10ul, controlPair.yin.id, controlPair.yang.id, "control", 0d,
                out var expected, out _), Is.True);
            Assert.That(combined.element, Is.EqualTo(expected.element));
        }

        [Test]
        public void CombineRejectsNonFiniteVelocityAndSamePolarityWithoutConsumingInputs()
        {
            config = CreateConfig(100, 0, 0, 0, 0, 0);
            var model = new LabModel();
            model.BeginSolo("invalid-combine", 10ul, 0d, config);
            Assert.That(model.TryGenerate(10ul, "first", 0d, out var first, out _), Is.True);
            Assert.That(model.TryGenerate(10ul, "second", 0d, out var second, out _), Is.True);
            Assert.That(model.TryCombineAt(10ul, first.id, second.id, "nonfinite", 0d,
                Vector2.one * .5f, new Vector2(float.NaN, 0f), out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_MOTION"));
            Assert.That(model.TryCombineAt(10ul, first.id, second.id, "polarity", 0d,
                Vector2.one * .5f, Vector2.zero, out _, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("POLARITY_MISMATCH"));
            Assert.That(model.Snapshot(0d).orbs.Length, Is.EqualTo(2));
        }

        [Test]
        public void SecretBoxUsesActualLocalCenterAndInvalidGeometryLeavesItUsable()
        {
            config = CreateConfig(0, 0, 0, 100, 0, 0);
            var model = new LabModel();
            model.BeginSolo("local-box", 10ul, 0d, config);
            Assert.That(model.TryGenerate(10ul, "box", 0d, out var box, out _), Is.True);
            Assert.That(model.TryUseItemAt(10ul, box.id, "invalid", 0d,
                new Vector2(.8f, -1f), out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_MOTION"));
            Assert.That(model.TryGetMovableState(box.id, out var retained), Is.True);
            Assert.That(retained.x, Is.EqualTo(.5f));
            Assert.That(model.TryUseItemAt(10ul, box.id, "open", 0d,
                new Vector2(.8f, .2f), out _, out _), Is.True);
            var outputs = model.Snapshot(0d).orbs.OrderBy(orb => orb.x).ToArray();
            Assert.That(outputs.Length, Is.EqualTo(2));
            Assert.That(outputs[0].x, Is.EqualTo(.75f).Within(.0001f));
            Assert.That(outputs[1].x, Is.EqualTo(.85f).Within(.0001f));
            Assert.That(outputs.All(orb => orb.y == .2f && orb.ownershipVersion == 1ul), Is.True);
            Assert.That(model.Snapshot(0d).players.Single().secretBoxUsed, Is.EqualTo(1));
        }

        [Test]
        public void AtomicTransferRejectsInteriorAndNonFiniteGeometryWithoutUpdatingStoredPose()
        {
            config = CreateConfig(100, 0, 0, 0, 0, 0);
            LabModel model = BeginRoom();
            ulong owner = model.Snapshot(0d).drums.Single().owner;
            Assert.That(model.TryGenerate(owner, "raw", 0d, out var raw, out _), Is.True);
            Assert.That(model.TryTransferAt(owner, raw.id, true, new Vector2(.5f, .8f),
                Vector2.right, "inside", 0d, LabEdgeCrossingKind.Swipe, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("NOT_OUTWARD_EDGE"));
            Assert.That(model.TryTransferAt(owner, raw.id, true, new Vector2(1f, float.NaN),
                Vector2.right, "nan", 0d, LabEdgeCrossingKind.Swipe, out _, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_MOTION"));
            Assert.That(model.TryGetMovableState(raw.id, out var retained), Is.True);
            Assert.That(retained.owner, Is.EqualTo(owner));
            Assert.That(retained.x, Is.EqualTo(raw.x));
            Assert.That(retained.y, Is.EqualTo(raw.y));
            Assert.That(retained.ownershipVersion, Is.EqualTo(1ul));
        }

        [Test]
        public void ReceiverCapacityStillRejectsOrbsButAllowsDrumsAndAdvancesOnlyDrumOwnership()
        {
            config = CreateConfig(100, 0, 0, 0, 0, 0);
            float[] values = config.CaptureDeveloperValues();
            values[9] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out var error), Is.True, error);
            LabModel model = BeginRoom();
            var drum = model.Snapshot(0d).drums.Single();
            ulong owner = drum.owner;
            Assert.That(model.TryGenerate(owner, "sender-orb", 0d, out var raw, out _), Is.True);
            Assert.That(model.TryTransferAt(owner, drum.id, true, new Vector2(1f, .5f),
                Vector2.right, "drum-out", 0d, LabEdgeCrossingKind.Swipe, out var passedDrum, out _), Is.True);
            Assert.That(model.TryGenerate(passedDrum.owner, "fill-receiver", 0d, out _, out _), Is.True);
            Assert.That(model.TryTransferAt(owner, raw.id, true, new Vector2(1f, .5f),
                Vector2.right, "full", 0d, LabEdgeCrossingKind.Swipe, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("RECEIVER_FULL"));
            Assert.That(model.TryGetMovableState(raw.id, out var retained), Is.True);
            Assert.That(retained.ownershipVersion, Is.EqualTo(1ul));
            Assert.That(model.TryTransferAt(passedDrum.owner, drum.id, false, new Vector2(0f, .5f),
                Vector2.left, "drum-back-to-full-board", 0d, LabEdgeCrossingKind.Swipe,
                out var returnedDrum, out _), Is.True);
            Assert.That(returnedDrum.owner, Is.EqualTo(owner));
            Assert.That(returnedDrum.ownershipVersion, Is.EqualTo(3ul));
        }

        private LabModel BeginRoom()
        {
            var model = new LabModel();
            model.Begin("local-board-room", new ulong[] { 10ul, 20ul }, 0d, config);
            return model;
        }

        private static LabConfig CreateConfig(params int[] chances)
        {
            var created = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = created.CaptureDeveloperValues();
            values[3] = values[4] = 10000f;
            values[5] = 1f;
            values[9] = 100f;
            values[10] = values[45] = 1000f;
            values[46] = values[47] = 0f;
            for (int i = 0; i < chances.Length; i++) values[39 + i] = chances[i];
            Assert.That(created.TryApplyDeveloperValues(values, out var error), Is.True, error);
            return created;
        }

        private static (LabOrbState yin, LabOrbState yang) GeneratePair(LabModel model)
        {
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 64 && (yin == null || yang == null); i++)
            {
                Assert.That(model.TryGenerate(10ul, "pair-" + i, 0d, out var raw, out var reason), Is.True, reason);
                if (raw.kind == LabOrbKind.Yin) yin = raw;
                else if (raw.kind == LabOrbKind.Yang) yang = raw;
            }
            Assert.That(yin, Is.Not.Null);
            Assert.That(yang, Is.Not.Null);
            return (yin, yang);
        }
    }
}
