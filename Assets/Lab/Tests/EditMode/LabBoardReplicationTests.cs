using System;
using System.Linq;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabBoardReplicationTests
    {
        [Test]
        public void PublisherSendsOnlyTheRecipientsBoardAndEveryDrumThenIgnoresPoseOnlyChanges()
        {
            var publisher = new LabBoardPublisher();
            var snapshot = Snapshot(1d);
            snapshot.orbs = new[] { Orb("mine", 10ul), Orb("theirs", 20ul), Orb("flying", 10ul) };
            snapshot.orbs[2].inFlight = true;
            snapshot.drums = new[] { Orb("drum", 20ul, LabOrbKind.Drum) };
            LabBoardFrame first = publisher.Build(snapshot, 10ul, 1, 4ul);
            Assert.That(first.checkpoint, Is.True);
            Assert.That(first.states.Select(orb => orb.id), Is.EquivalentTo(new[] { "mine", "drum" }));
            snapshot.orbs[0].x = .9f; snapshot.orbs[0].vx = 3f; snapshot.orbs[0].revision++;
            snapshot.drums[0].y = .2f; snapshot.drums[0].revision++;
            snapshot.serverTime = 1.1d;
            LabBoardFrame second = publisher.Build(snapshot, 10ul, 2, 6ul);
            Assert.That(second.checkpoint, Is.False);
            Assert.That(second.states, Is.Empty);
            Assert.That(second.removedIds, Is.Empty);
            Assert.That(first.states.Single(orb => orb.id == "mine").x, Is.EqualTo(.5f), "Publisher outputs own their data.");

            snapshot.orbs[0].small = true;
            snapshot.orbs[0].burnedAt = 1.1d;
            LabBoardFrame changed = publisher.Build(snapshot, 10ul, 3, 7ul);
            Assert.That(changed.states.Single().id, Is.EqualTo("mine"));
            Assert.That(changed.states.Single().small, Is.True);
        }

        [Test]
        public void PublisherReportsDepartureAndResendsCheckpointAfterFiveSecondsOrNewRound()
        {
            var publisher = new LabBoardPublisher();
            var snapshot = Snapshot(0d);
            snapshot.orbs = new[] { Orb("mine", 10ul) };
            publisher.Build(snapshot, 10ul, 1, 1ul);
            snapshot.orbs[0].owner = 20ul;
            snapshot.orbs[0].ownershipVersion++;
            snapshot.serverTime = .1d;
            LabBoardFrame departed = publisher.Build(snapshot, 10ul, 2, 2ul);
            Assert.That(departed.removedIds, Is.EqualTo(new[] { "mine" }));
            snapshot.serverTime = 5d;
            Assert.That(publisher.Build(snapshot, 10ul, 3, 2ul).checkpoint, Is.True);
            snapshot.round = 2; snapshot.serverTime = 0d;
            Assert.That(publisher.Build(snapshot, 10ul, 4, 1ul).checkpoint, Is.True);
        }

        [Test]
        public void ReceiptCanArriveBeforeTheFirstSnapshotWithoutConsumedOrbResurrection()
        {
            var replica = ActiveReplica();
            Assert.That(replica.ApplyReceipt(Receipt(1, removed: new[] { "consumed" })), Is.True);
            var snapshot = Snapshot(0d);
            snapshot.orbs = new[] { Orb("consumed", 10ul), Orb("remaining", 10ul) };
            LabSnapshot overlaid = replica.Overlay(snapshot);
            Assert.That(overlaid.orbs.Select(orb => orb.id), Is.EqualTo(new[] { "remaining" }));
            Assert.That(snapshot.orbs.Length, Is.EqualTo(2), "The shared snapshot is not mutated.");
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
        }

        [Test]
        public void RejectedReceiptAdvancesTheStreamAndDuplicateReceiptCannotUndoLaterState()
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(Frame(1, true, Orb("mine", 10ul)), out _), Is.True);
            LabActionReceipt rejected = Receipt(2);
            rejected.accepted = false;
            rejected.reason = "RECEIVER_FULL";
            Assert.That(replica.ApplyReceipt(rejected), Is.True);
            Assert.That(replica.TryApply(Frame(3, false), out _), Is.True);
            Assert.That(replica.ApplyReceipt(rejected), Is.False);
            Assert.That(replica.LatestSequence, Is.EqualTo(3));
            Assert.That(replica.NeedsCheckpoint, Is.False);
        }

        [Test]
        public void SequenceGapStopsInteractionUntilCheckpointAndCheckpointCanJumpMissingFrames()
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(Frame(1, true, Orb("mine", 10ul)), out _), Is.True);
            Assert.That(replica.InteractionAllowed, Is.True);
            Assert.That(replica.TryApply(Frame(3, false), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("BOARD_CHECKPOINT_REQUIRED"));
            Assert.That(replica.NeedsCheckpoint, Is.True);
            Assert.That(replica.InteractionAllowed, Is.False);
            Assert.That(replica.TryApply(Frame(4, true, Orb("recovered", 10ul)), out _), Is.True);
            Assert.That(replica.TryGetState("mine", out _), Is.False);
            Assert.That(replica.TryGetState("recovered", out _), Is.True);
            Assert.That(replica.InteractionAllowed, Is.True);
        }

        [Test]
        public void ReceiptHandoffUpdatesGlobalDrumAndRemovesLocalOrbWhileOldArrivalCannotReturn()
        {
            var replica = ActiveReplica();
            var original = Orb("mine", 10ul);
            var drum = Orb("drum", 20ul, LabOrbKind.Drum);
            Assert.That(replica.TryApply(Frame(1, true, original, drum), out _), Is.True);
            var transferred = original.Copy();
            transferred.owner = 20ul; transferred.ownershipVersion = 2;
            var movedDrum = drum.Copy(); movedDrum.owner = 10ul; movedDrum.ownershipVersion = 2;
            movedDrum.arrival = LabDrumArrival.Stop; movedDrum.arrivedAt = 1d; movedDrum.stopAt = .3f;
            var receipt = Receipt(2, new[] { transferred, movedDrum });
            Assert.That(replica.ApplyReceipt(receipt), Is.True);
            Assert.That(replica.TryGetState("mine", out _), Is.False);
            Assert.That(replica.TryGetState("drum", out var receivedDrum), Is.True);
            Assert.That(receivedDrum.owner, Is.EqualTo(10ul));
            Assert.That(receivedDrum.ownershipVersion, Is.EqualTo(2ul));
            Assert.That(replica.TryApply(Frame(1, true, original, drum), out _), Is.False);
            var oldSnapshot = Snapshot(0d);
            oldSnapshot.orbs = new[] { original };
            oldSnapshot.drums = new[] { drum };
            LabSnapshot display = replica.Overlay(oldSnapshot);
            Assert.That(display.orbs, Is.Empty);
            Assert.That(display.drums.Single().owner, Is.EqualTo(10ul));
            Assert.That(display.drums.Single().arrivedAt, Is.EqualTo(1d));
        }

        [Test]
        public void OverlayKeepsNewOwnerExactlyOnceAndNeverChangesPaintHpOrSharedTerminalPhase()
        {
            var replica = ActiveReplica();
            var mine = Orb("received", 10ul);
            var frame = Frame(1, true, mine);
            frame.serverTime = 4d; frame.remaining = 176d; frame.targetYawDegrees = 42f;
            frame.phase = LabPhase.Victory;
            frame.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 70f, personalHp = 250 } };
            Assert.That(replica.TryApply(frame, out _), Is.True);
            var snapshot = Snapshot(1d);
            var oldOwner = mine.Copy(); oldOwner.owner = 20ul;
            snapshot.orbs = new[] { oldOwner, Orb("remote", 20ul) };
            snapshot.hp = 123; snapshot.paintCoverage = .25f; snapshot.paintRevision = 9;
            snapshot.paintCoveredCount = 50; snapshot.paintCellCount = 200;
            LabSnapshot display = replica.Overlay(snapshot);
            Assert.That(display.orbs.Count(orb => orb.id == "received"), Is.EqualTo(1));
            Assert.That(display.orbs.Single(orb => orb.id == "received").owner, Is.EqualTo(10ul));
            Assert.That(display.orbs.Any(orb => orb.id == "remote"), Is.True);
            Assert.That(display.serverTime, Is.EqualTo(4d));
            Assert.That(display.targetYawDegrees, Is.EqualTo(42f));
            Assert.That(display.players.Single().personalHp, Is.EqualTo(250));
            Assert.That(display.phase, Is.EqualTo(LabPhase.Playing), "Paint-confirmed terminal transition is a separate stream.");
            Assert.That(replica.InteractionAllowed, Is.False, "A terminal control frame still stops new board input.");
            Assert.That(display.hp, Is.EqualTo(123));
            Assert.That(display.paintCoverage, Is.EqualTo(.25f));
            Assert.That(display.paintRevision, Is.EqualTo(9u));
            Assert.That(display.paintCoveredCount, Is.EqualTo(50));
            snapshot.phase = LabPhase.Defeat;
            Assert.That(replica.Overlay(snapshot).phase, Is.EqualTo(LabPhase.Defeat));
        }

        [Test]
        public void SnapshotCopyAndFastClockOverlayPreserveTheIncomingComboWarningWindow()
        {
            var snapshot = Snapshot(15d);
            snapshot.comboNextAt = 20d;
            snapshot.comboWarningAt = 17d;
            LabSnapshot sharedCopy = LabBoardReplica.CopySnapshot(snapshot);
            Assert.That(sharedCopy.comboNextAt, Is.EqualTo(20d));
            Assert.That(sharedCopy.comboWarningAt, Is.EqualTo(17d));
            Assert.That(sharedCopy.ComboWarningOn(sharedCopy.serverTime), Is.False);

            var replica = ActiveReplica();
            var frame = Frame(1, true);
            frame.serverTime = 18d;
            frame.remaining = 162d;
            Assert.That(replica.TryApply(frame, out _), Is.True);
            LabSnapshot visible = replica.Overlay(sharedCopy);
            Assert.That(visible.serverTime, Is.EqualTo(18d));
            Assert.That(visible.comboNextAt, Is.EqualTo(20d));
            Assert.That(visible.comboWarningAt, Is.EqualTo(17d));
            Assert.That(visible.ComboWarningOn(visible.serverTime), Is.True);
            visible.comboNextAt = 30d;
            Assert.That(snapshot.comboNextAt, Is.EqualTo(20d));
            snapshot.comboNextAt = snapshot.comboWarningAt = -1d;
            Assert.That(replica.Overlay(snapshot).ComboWarningOn(18d), Is.False);
        }

        [Test]
        public void SameRoundSetActiveRetainsReceiptsButNewRoundRejectsEveryOldPacket()
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(Frame(1, true, Orb("mine", 10ul)), out _), Is.True);
            replica.SetActive("board-test", 1u, 10ul);
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
            replica.SetActive("board-test", 2u, 10ul);
            Assert.That(replica.TryApply(Frame(2, true, Orb("old", 10ul)), out _), Is.False);
            Assert.That(replica.ApplyReceipt(Receipt(2)), Is.False);
            Assert.That(replica.LatestSequence, Is.Zero);
            Assert.That(replica.TryGetState("mine", out _), Is.False);
        }

        [Test]
        public void InvalidOrOversizedFrameIsRejectedBeforeAnyStateOrSequenceMutation()
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(Frame(1, true, Orb("mine", 10ul)), out _), Is.True);
            var invalid = Frame(2, false, Orb("invalid", 20ul));
            Assert.That(replica.TryApply(invalid, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            invalid.states = Enumerable.Range(0, LabBoardReplica.MaximumStates + 1)
                .Select(index => Orb("large-" + index, 10ul)).ToArray();
            Assert.That(replica.TryApply(invalid, out _), Is.False);
            invalid.states = new[] { Orb("duplicate", 10ul), Orb("duplicate", 10ul) };
            Assert.That(replica.TryApply(invalid, out _), Is.False);
            invalid.states = new[] { Orb("nan", 10ul) }; invalid.states[0].vx = float.NaN;
            Assert.That(replica.TryApply(invalid, out _), Is.False);
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
            Assert.That(replica.TryGetState("mine", out _), Is.True);
        }

        [Test]
        public void ReplicaOwnsItsCopiesAndNewerFullSnapshotResourcesWinOverOlderControlHeader()
        {
            var replica = ActiveReplica();
            var frame = Frame(1, true, Orb("mine", 10ul));
            frame.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 50f, personalHp = 300 } };
            Assert.That(replica.TryApply(frame, out _), Is.True);
            frame.states[0].owner = 20ul; frame.players[0].stamina = 0f; frame.remaining = 0d;
            Assert.That(replica.TryGetState("mine", out var read), Is.True);
            Assert.That(read.owner, Is.EqualTo(10ul));
            read.owner = 30ul;
            Assert.That(replica.TryGetState("mine", out read), Is.True);
            Assert.That(read.owner, Is.EqualTo(10ul));
            var newer = Snapshot(2d);
            newer.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 80f, personalHp = 300 } };
            Assert.That(replica.Overlay(newer).players.Single().stamina, Is.EqualTo(80f));
            Assert.That(replica.Overlay(newer).remaining, Is.EqualTo(178d));
        }

        [Test]
        public void FailureFenceConsumesOldReceiptsWithoutRestoringOrbsOrStaminaAndAllowsNewGeneration()
        {
            var replica = ActiveReplica();
            var oldOrb = RevisionOrb("old", 10ul, 20ul);
            var drum = RevisionOrb("kept-drum", 20ul, 1ul, LabOrbKind.Drum);
            LabBoardFrame before = VersionedFrame(1, true, 20ul, 9d, oldOrb, drum);
            before.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 100f, personalHp = 237 } };
            Assert.That(replica.TryApply(before, out _), Is.True);
            replica.PurgeThrough(50ul, 10d);
            Assert.That(replica.LatestSequence, Is.EqualTo(1), "A failure event is not a recipient sequence.");
            Assert.That(replica.TryGetState("old", out _), Is.False);
            Assert.That(replica.TryGetState("kept-drum", out _), Is.True);

            LabActionReceipt delayed = Receipt(2, new[] { oldOrb });
            delayed.modelRevision = 20ul; delayed.serverTime = 9d;
            delayed.players = before.players;
            Assert.That(replica.ApplyReceipt(delayed), Is.True, "An ordered old receipt still closes its sequence.");
            Assert.That(replica.TryGetState("old", out _), Is.False);
            LabSnapshot oldSnapshot = Snapshot(9d);
            oldSnapshot.players = before.players;
            LabPlayerState visible = replica.Overlay(oldSnapshot).players.Single();
            Assert.That(visible.stamina, Is.Zero);
            Assert.That(visible.personalHp, Is.EqualTo(237));
            Assert.That(before.players.Single().stamina, Is.EqualTo(100f), "Never modify a packet's player object.");

            LabActionReceipt fresh = Receipt(3, new[] { RevisionOrb("new", 10ul, 60ul) });
            fresh.modelRevision = 60ul; fresh.serverTime = 11d;
            fresh.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 6f, personalHp = 237 } };
            Assert.That(replica.ApplyReceipt(fresh), Is.True);
            Assert.That(replica.LatestSequence, Is.EqualTo(3));
            Assert.That(replica.NeedsCheckpoint, Is.False);
            Assert.That(replica.TryGetState("new", out _), Is.True);
            Assert.That(replica.Overlay(oldSnapshot).players.Single().stamina, Is.EqualTo(6f));
        }

        [Test]
        public void DelayedCheckpointRepairsASequenceGapWithoutReplacingTheNewerPostFailureLedgerOrHeader()
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(VersionedFrame(1, true, 20ul, 9d,
                RevisionOrb("old", 10ul, 20ul), RevisionOrb("drum", 20ul, 1ul, LabOrbKind.Drum)), out _), Is.True);
            replica.PurgeThrough(50ul, 10d);
            LabActionReceipt generated = Receipt(2, new[] { RevisionOrb("new", 10ul, 60ul) });
            generated.modelRevision = 60ul; generated.serverTime = 11d;
            Assert.That(replica.ApplyReceipt(generated), Is.True);
            LabBoardFrame current = VersionedFrame(3, false, 65ul, 12d);
            current.paused = true; current.targetYawDegrees = 42f;
            current.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 8f, personalHp = 237 } };
            Assert.That(replica.TryApply(current, out _), Is.True);
            Assert.That(replica.TryApply(VersionedFrame(5, false, 70ul, 13d), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("BOARD_CHECKPOINT_REQUIRED"));

            LabBoardFrame delayed = VersionedFrame(6, true, 40ul, 9d, RevisionOrb("old", 10ul, 20ul));
            delayed.targetYawDegrees = 900f;
            delayed.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 100f, personalHp = 300 } };
            Assert.That(replica.TryApply(delayed, out _), Is.True);
            Assert.That(replica.LatestSequence, Is.EqualTo(6));
            Assert.That(replica.NeedsCheckpoint, Is.False);
            Assert.That(replica.TryGetState("old", out _), Is.False);
            Assert.That(replica.TryGetState("new", out _), Is.True);
            Assert.That(replica.TryGetState("drum", out _), Is.True);
            LabSnapshot visible = replica.Overlay(Snapshot(8d));
            Assert.That(visible.serverTime, Is.EqualTo(12d));
            Assert.That(visible.paused, Is.True);
            Assert.That(visible.targetYawDegrees, Is.EqualTo(42f));
            Assert.That(visible.players.Single().stamina, Is.EqualTo(8f));
            Assert.That(visible.players.Single().personalHp, Is.EqualTo(237));
            Assert.That(replica.TryApply(VersionedFrame(7, false, 70ul, 13d), out _), Is.True);
        }

        [Test]
        public void FailureBeforeTheFirstBoardFiltersUnknownLocalRemoteAndFlyingSnapshotOrbsButKeepsDrums()
        {
            var replica = ActiveReplica();
            replica.PurgeThrough(50ul, 10d);
            var flying = RevisionOrb("old-flying", 20ul, 30ul); flying.inFlight = true;
            LabSnapshot incoming = Snapshot(9d);
            incoming.orbs = new[] { RevisionOrb("old-local", 10ul, 20ul), RevisionOrb("old-remote", 20ul, 25ul), flying,
                RevisionOrb("new-local", 10ul, 60ul), RevisionOrb("new-remote", 20ul, 61ul) };
            incoming.drums = new[] { RevisionOrb("drum", 20ul, 1ul, LabOrbKind.Drum) };
            incoming.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 100f, personalHp = 237 } };
            LabSnapshot visible = replica.Overlay(incoming);
            Assert.That(visible.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { "new-local", "new-remote" }));
            Assert.That(visible.drums.Single().id, Is.EqualTo("drum"));
            Assert.That(visible.players.Single().stamina, Is.Zero);
            Assert.That(incoming.orbs.Length, Is.EqualTo(5));
            Assert.That(incoming.players.Single().stamina, Is.EqualTo(100f));
            Assert.That(replica.TryApply(VersionedFrame(1, true, 40ul, 9d,
                RevisionOrb("old-local", 10ul, 20ul)), out _), Is.True);
            Assert.That(replica.TryGetState("new-local", out _), Is.True, "An old checkpoint cannot clear a known newer orb.");
            incoming.serverTime = 12d; incoming.players.Single().stamina = 12f;
            Assert.That(replica.Overlay(incoming).players.Single().stamina, Is.EqualTo(12f), "Newer recovery remains visible.");
        }

        [Test]
        public void FailureBeforeAnyReceiptRetainsADelayedGlobalDrumWhileDiscardingItsOldOrdinaryOrb()
        {
            var replica = ActiveReplica();
            replica.PurgeThrough(50ul, 10d);
            LabActionReceipt old = Receipt(1, new[] { RevisionOrb("old", 10ul, 20ul),
                RevisionOrb("global-drum", 20ul, 1ul, LabOrbKind.Drum) });
            old.modelRevision = 20ul; old.serverTime = 9d;
            Assert.That(replica.ApplyReceipt(old), Is.True);
            Assert.That(replica.TryGetState("old", out _), Is.False);
            Assert.That(replica.TryGetState("global-drum", out var drum), Is.True);
            Assert.That(drum.kind, Is.EqualTo(LabOrbKind.Drum));
            Assert.That(drum.owner, Is.EqualTo(20ul));
            Assert.That(replica.TryApply(VersionedFrame(2, true, 20ul, 9d,
                RevisionOrb("old", 10ul, 20ul)), out _), Is.True);
            Assert.That(replica.Overlay(Snapshot(9d)).drums.Single().id, Is.EqualTo("global-drum"));
            Assert.That(replica.LatestSequence, Is.EqualTo(2));
        }

        [Test]
        public void LateFailureNotificationPreservesNewerResourcesAndRepeatedFenceCannotZeroRecovery()
        {
            var replica = ActiveReplica();
            LabBoardFrame current = VersionedFrame(1, true, 60ul, 12d,
                RevisionOrb("old", 10ul, 20ul), RevisionOrb("new", 10ul, 60ul), RevisionOrb("drum", 20ul, 1ul, LabOrbKind.Drum));
            current.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 12f, personalHp = 237 } };
            Assert.That(replica.TryApply(current, out _), Is.True);
            replica.PurgeThrough(50ul, 10d);
            replica.PurgeThrough(50ul, 99d);
            replica.PurgeThrough(40ul, 100d);
            Assert.That(replica.TryGetState("old", out _), Is.False);
            Assert.That(replica.TryGetState("new", out _), Is.True);
            Assert.That(replica.TryGetState("drum", out _), Is.True);
            Assert.That(replica.Overlay(Snapshot(9d)).players.Single().stamina, Is.EqualTo(12f));
            replica.PurgeThrough(70ul, 14d);
            Assert.That(replica.TryGetState("new", out _), Is.False);
            Assert.That(replica.TryGetState("drum", out _), Is.True);
            Assert.That(replica.Overlay(Snapshot(9d)).players.Single().stamina, Is.Zero);
        }

        [Test]
        public void PurgeFenceSurvivesSameRoundRebindingAndResetsOnlyWithTheActiveRound()
        {
            var replica = ActiveReplica();
            replica.PurgeThrough(50ul, 10d);
            replica.SetActive("board-test", 1u, 10ul);
            LabSnapshot old = Snapshot(9d); old.orbs = new[] { RevisionOrb("retired", 10ul, 1ul) };
            Assert.That(replica.Overlay(old).orbs, Is.Empty);
            replica.SetActive("board-test", 2u, 10ul);
            LabBoardFrame freshRound = VersionedFrame(1, true, 1ul, 0d, RevisionOrb("retired", 10ul, 1ul));
            freshRound.round = 2u;
            Assert.That(replica.TryApply(freshRound, out _), Is.True);
            Assert.That(replica.TryGetState("retired", out _), Is.True);
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
        }

        [Test]
        public void PublisherSendsChangesToBothCapturedRewardScales()
        {
            var publisher = new LabBoardPublisher();
            LabSnapshot snapshot = Snapshot(1d); snapshot.orbs = new[] { Orb("mine", 10ul) };
            publisher.Build(snapshot, 10ul, 1, 1ul);
            snapshot.orbs.Single().jigwiABRewardSizeScale = 1.3f;
            Assert.That(publisher.Build(snapshot, 10ul, 2, 2ul).states.Single().jigwiABRewardSizeScale, Is.EqualTo(1.3f));
            snapshot.orbs.Single().jigwiABRewardPaintScale = 1.5f;
            Assert.That(publisher.Build(snapshot, 10ul, 3, 3ul).states.Single().jigwiABRewardPaintScale, Is.EqualTo(1.5f));
        }

        [TestCase(float.NaN, 1f, false)]
        [TestCase(float.PositiveInfinity, 1f, false)]
        [TestCase(.99f, 1f, false)]
        [TestCase(3.01f, 1f, false)]
        [TestCase(1f, float.NaN, false)]
        [TestCase(1f, float.PositiveInfinity, false)]
        [TestCase(1f, .99f, false)]
        [TestCase(1f, 3.01f, false)]
        [TestCase(1.3f, 1.5f, true)]
        public void InvalidOrNonFlyingRewardMetadataCannotChangeAnOrderedLedger(float size, float paint, bool rewarded)
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(Frame(1, true), out _), Is.True);
            LabOrbState invalid = Orb("invalid", 10ul);
            invalid.jigwiABReward = rewarded; invalid.jigwiABRewardSizeScale = size; invalid.jigwiABRewardPaintScale = paint;
            Assert.That(replica.TryApply(Frame(2, false, invalid), out _), Is.False);
            Assert.That(replica.ApplyReceipt(Receipt(2, new[] { invalid })), Is.False);
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
            Assert.That(replica.TryGetState("invalid", out _), Is.False);
        }

        [Test]
        public void ABoardFrameCannotInstallAFailureFenceFromAFutureModelRevision()
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(VersionedFrame(1, true, 30ul, 34d,
                RevisionOrb("kept", 10ul, 20ul)), out _), Is.True);
            LabBoardFrame frame = VersionedFrame(2, false, 50ul, 35d);
            frame.removedIds = new[] { "kept" };
            frame.players = new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 0f, personalHp = 237 } };
            frame.jigwiAB = new LabJigwiABSnapshot
            {
                enabled = true, startedAt = 0d, cutoffAt = 100d,
                rounds = new[]
                {
                    new LabJigwiABRound
                    {
                        id = "failed-mission", slot = 1, kind = LabJigwiABKind.GroupFire,
                        warningAt = 24d, activeAt = 25d, endsAt = 35d, resolvedAt = 35d,
                        resolution = LabJigwiABResolution.Expired, clearedThroughRevision = 51ul,
                        shrinkAt = 25d, shrinkScale = 1f, movePeriodSeconds = 4f, targetRadius = .3f,
                        rewardSizeScale = 1.3f, rewardPaintScale = 1.5f,
                        players = new[]
                        {
                            new LabJigwiABPlayerFire
                            {
                                playerId = 10ul,
                                targets = new[] { new LabJigwiABFireTarget { id = "fire", x = .5f, y = .62f } }
                            }
                        }
                    }
                }
            };
            Assert.That(LabJigwiABNetworkValidation.Valid(frame.jigwiAB, frame.players), Is.True,
                "The payload is otherwise valid; only the frame's model revision makes its fence impossible.");
            Assert.That(replica.TryApply(frame, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
            Assert.That(replica.NeedsCheckpoint, Is.False);
            Assert.That(replica.TryGetState("kept", out _), Is.True,
                "Invalid outcome metadata must be rejected before applying unrelated removals.");
            frame.jigwiAB.rounds.Single().clearedThroughRevision = 50ul;
            Assert.That(replica.TryApply(frame, out reason), Is.True, reason);
            Assert.That(replica.LatestSequence, Is.EqualTo(2));
            Assert.That(replica.TryGetState("kept", out _), Is.False);
        }

        [Test]
        public void AValidFlyingRewardReceiptConsumesItsSequenceWithoutPuttingTheProjectileBackOnTheBoard()
        {
            var replica = ActiveReplica();
            Assert.That(replica.TryApply(Frame(1, true, Orb("flying", 10ul)), out _), Is.True);
            LabOrbState launched = Orb("flying", 10ul); launched.inFlight = true;
            launched.jigwiABReward = true; launched.jigwiABRewardSizeScale = 1.3f; launched.jigwiABRewardPaintScale = 1.5f;
            Assert.That(replica.ApplyReceipt(Receipt(2, new[] { launched })), Is.True);
            Assert.That(replica.LatestSequence, Is.EqualTo(2));
            Assert.That(replica.TryGetState("flying", out _), Is.False);
        }

        private static LabOrbState RevisionOrb(string id, ulong owner, ulong revision, LabOrbKind kind = LabOrbKind.Combined)
        {
            LabOrbState state = Orb(id, owner, kind); state.revision = revision; return state;
        }

        private static LabBoardFrame VersionedFrame(long sequence, bool checkpoint, ulong revision, double at, params LabOrbState[] states)
        {
            LabBoardFrame frame = Frame(sequence, checkpoint, states);
            frame.modelRevision = revision; frame.serverTime = at; frame.remaining = 180d - at; frame.playedSeconds = at;
            return frame;
        }

        private static LabBoardReplica ActiveReplica()
        {
            var replica = new LabBoardReplica();
            replica.SetActive("board-test", 1u, 10ul);
            return replica;
        }

        private static LabOrbState Orb(string id, ulong owner, LabOrbKind kind = LabOrbKind.Combined) =>
            new LabOrbState { id = id, owner = owner, kind = kind, x = .5f, y = .5f, ownershipVersion = 1ul };

        private static LabSnapshot Snapshot(double at) => new LabSnapshot
        {
            sessionId = "board-test", round = 1u, serverTime = at, phase = LabPhase.Playing,
            remaining = 180d - at, playedSeconds = at
        };

        private static LabBoardFrame Frame(long sequence, bool checkpoint, params LabOrbState[] states) =>
            new LabBoardFrame
            {
                sessionId = "board-test", round = 1u, localOwner = 10ul, sequence = sequence,
                checkpoint = checkpoint, states = states, serverTime = 1d, remaining = 179d,
                playedSeconds = 1d, phase = LabPhase.Playing
            };

        private static LabActionReceipt Receipt(long sequence, LabOrbState[] states = null, string[] removed = null) =>
            new LabActionReceipt
            {
                sessionId = "board-test", round = 1u, sender = 10ul, requestId = sequence,
                sequence = sequence, accepted = true, states = states ?? Array.Empty<LabOrbState>(),
                removedIds = removed ?? Array.Empty<string>(), serverTime = 1d
            };
    }
}
