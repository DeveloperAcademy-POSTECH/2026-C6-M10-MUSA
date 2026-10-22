using System;
using System.Linq;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabJigwiReplicationTests
    {
        private static LabJigwiState Fire() => new LabJigwiState
        {
            playerId = 0, fireId = "fire-1", fireStartedAt = 1d, fireEndsAt = 9d,
            targets = new[] { new LabJigwiFireTarget { id = "spot-1", x = .3f, y = .5f } }
        };
        private static LabSnapshot Snapshot() => new LabSnapshot
        {
            sessionId = "jigwi", round = 1, phase = LabPhase.Playing, serverTime = 1d,
            remaining = 170d, playedSeconds = 10d, jigwi = new[] { Fire() },
            players = new[] { new LabPlayerState { id = 0, seat = 0, stamina = 100f } },
            orbs = new[] { new LabOrbState { id = "raw", owner = 0, kind = LabOrbKind.Yin, x = .5f, y = .5f } }
        };

        [Test]
        public void BoardHeaderCarriesHostZeroFireWithoutPaintAndOwnsItsNestedData()
        {
            var publisher = new LabBoardPublisher();
            var source = Snapshot();
            var frame = publisher.Build(source, 0, 1, 1);
            source.jigwi[0].targets[0].extinguished = true;
            Assert.That(frame.jigwi.Single().playerId, Is.Zero);
            Assert.That(frame.jigwi.Single().targets.Single().extinguished, Is.False);
            var replica = new LabBoardReplica();
            replica.SetActive("jigwi", 1, 0);
            Assert.That(replica.TryApply(frame, out string reason), Is.True, reason);
            frame.jigwi[0].targets[0].extinguished = true;
            var stalePaintSnapshot = Snapshot();
            stalePaintSnapshot.serverTime = .5d;
            stalePaintSnapshot.jigwi = Array.Empty<LabJigwiState>();
            var overlaid = replica.Overlay(stalePaintSnapshot);
            Assert.That(overlaid.JigwiFor(0).FireActive(overlaid.serverTime), Is.True);
            Assert.That(overlaid.JigwiFor(0).targets.Single().extinguished, Is.False);
            overlaid.jigwi[0].targets[0].extinguished = true;
            Assert.That(replica.Overlay(stalePaintSnapshot).JigwiFor(0).targets.Single().extinguished, Is.False);
        }

        [Test]
        public void JigwiRadiusExpiryAndProvenanceChangesPublishWithoutResettingLocalMotion()
        {
            var publisher = new LabBoardPublisher();
            var source = Snapshot();
            publisher.Build(source, 0, 1, 1);
            source.serverTime = 1.1d;
            source.orbs[0].x = .9f;
            Assert.That(publisher.Build(source, 0, 2, 1).states, Is.Empty);
            source.orbs[0].jigwiGenerationBurn = true;
            source.orbs[0].jigwiRadiusScale = .5f;
            source.orbs[0].expiresAt = 4d;
            var changed = publisher.Build(source, 0, 3, 2).states.Single();
            Assert.That(changed.id, Is.EqualTo("raw"));
            Assert.That(changed.jigwiGenerationBurn, Is.True);
            Assert.That(changed.jigwiRadiusScale, Is.EqualTo(.5f));
            Assert.That(changed.expiresAt, Is.EqualTo(4d));
            source.orbs[0].jigwiSupply = true;
            Assert.That(publisher.Build(source, 0, 4, 3).states.Single().jigwiSupply, Is.True);
        }

        [Test]
        public void InvalidFireMetadataCannotEnterOrderedBoardState()
        {
            var publisher = new LabBoardPublisher();
            var source = Snapshot();
            var frame = publisher.Build(source, 0, 1, 1);
            var replica = new LabBoardReplica();
            replica.SetActive("jigwi", 1, 0);
            frame.jigwi[0].targets[0].x = float.NaN;
            Assert.That(replica.TryApply(frame, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.Zero);
            Assert.That(LabJigwiNetworkValidation.Valid(new[] { Fire(), Fire() }), Is.False, "Duplicate owner records are ambiguous, including Host ID zero.");
            var duplicateTarget = Fire();
            duplicateTarget.targets = new[] { duplicateTarget.targets[0], duplicateTarget.targets[0].Copy() };
            Assert.That(LabJigwiNetworkValidation.Valid(new[] { duplicateTarget }), Is.False);
        }

        [Test]
        public void AcceptedOverheatConsumptionRetiresProvisionalDependentsWithoutInventingAnOutput()
        {
            var coordinator = new LabLocalActionCoordinator();
            coordinator.SetActive("jigwi", 1, 0);
            Assert.That(coordinator.TryRegister(1, new[] { "yin", "yang" }, "pending-combine", 1d, out _), Is.True);
            Assert.That(coordinator.TryRegister(2, new[] { "pending-combine" }, null, 1d, out _), Is.True);
            var receipt = new LabActionReceipt { sessionId = "jigwi", round = 1, sender = 0, requestId = 1,
                accepted = true, reason = "JIGWI_BURNED", consumedWithoutOutput = true, provisionalId = "pending-combine" };
            Assert.That(coordinator.ApplyReceipt(receipt, out var result), Is.True);
            Assert.That(result.accepted, Is.True, "This was an accepted, consuming attempt, not a recoverable rule rejection.");
            Assert.That(result.rejectedDependents, Is.EqualTo(new long[] { 2 }));
            Assert.That(coordinator.TryGetMappedId("pending-combine", out _), Is.False);
            Assert.That(coordinator.GetDueRequestIds(10d, true), Is.Empty);
            Assert.That(coordinator.TryRegister(3, new[] { "pending-combine" }, null, 10d, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("DEPENDENCY_REJECTED"));
            Assert.That(coordinator.ApplyReceipt(receipt.Copy(), out _), Is.False);
        }

        [Test]
        public void OrdinaryAcceptedProducerStillRequiresCanonicalOutputAndMatchingProvisionalId()
        {
            var coordinator = new LabLocalActionCoordinator();
            coordinator.SetActive("jigwi", 1, 0);
            coordinator.TryRegister(1, new[] { "drum" }, "pending", 0d, out _);
            var receipt = new LabActionReceipt { sessionId = "jigwi", round = 1, sender = 0, requestId = 1,
                accepted = true, provisionalId = "pending" };
            Assert.That(coordinator.ApplyReceipt(receipt, out _), Is.False);
            receipt.consumedWithoutOutput = true;
            receipt.provisionalId = "wrong";
            Assert.That(coordinator.ApplyReceipt(receipt, out _), Is.False);
            receipt.provisionalId = "pending";
            Assert.That(coordinator.ApplyReceipt(receipt, out _), Is.True);
        }
    }
}
