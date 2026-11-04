using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabJigwiSupplyReplicationTests
    {
        private const string Session = "supply-wire";
        private const string First = "jigwi-ab/supply-wire/1/1";
        private static LabPlayerState[] Members() => new[] { new LabPlayerState { id = 10ul, seat = 0, stamina = 73f, personalHp = 300 } };
        private static LabOrbState Water(string id, string eventId = First) => new LabOrbState
        {
            id = id, owner = 10ul, ownershipVersion = 1, kind = LabOrbKind.Combined, element = LabOrbElement.Water,
            origin = LabOrbOrigin.Gourd, originOwner = 10ul, jigwiSupply = eventId != null,
            jigwiABSupplyEventId = eventId, x = .5f, y = .4f, createdAt = 25d, revision = 5ul
        };
        private static LabBoardReplica Replica()
        {
            var replica = new LabBoardReplica(); replica.SetActive(Session, 1u, 10ul); return replica;
        }
        private static LabBoardFrame Frame(long sequence, params LabOrbState[] orbs) => new LabBoardFrame
        {
            sessionId = Session, round = 1u, localOwner = 10ul, sequence = sequence, checkpoint = true,
            serverTime = 26d, remaining = 94d, playedSeconds = 26d, phase = LabPhase.Playing,
            modelRevision = 10ul, players = Members(), states = orbs
        };
        private static LabSnapshot Snapshot(double time, params LabOrbState[] orbs) => new LabSnapshot
        {
            sessionId = Session, round = 1u, serverTime = time, remaining = 120d - time,
            phase = LabPhase.Playing, players = Members(), orbs = orbs
        };
        private static LabJigwiABSnapshot FirstMission(bool completed) => new LabJigwiABSnapshot
        {
            enabled = true, startedAt = 0d, cutoffAt = 100d,
            rounds = new[] { new LabJigwiABRound
            {
                id = First, slot = 1, kind = LabJigwiABKind.GroupFire,
                resolution = completed ? LabJigwiABResolution.Completed : LabJigwiABResolution.None,
                warningAt = 24d, activeAt = 25d, endsAt = 40d, resolvedAt = completed ? 29d : -1d,
                syncCount = completed ? LabJigwiABTimeline.GroupSyncRequired : 0, // #509
                rewardUntil = -1d, rewardSizeScale = 1f, rewardPaintScale = 1f, // #509: no B reward
                shrinkAt = 25d, shrinkScale = 1f, moveAmount = .03f, movePeriodSeconds = 4f, targetRadius = .3f,
                players = new[] { new LabJigwiABPlayerFire { playerId = 10ul,
                    targets = new[] { new LabJigwiABFireTarget { id = First + "/10/0", x = .5f, y = .62f,
                        lit = !completed } } } }
            } }
        };

        [Test]
        public void SupplyIdentityAndItsQuotaOwnerSurviveCopyAndJsonWithoutChangingLegacyWater()
        {
            LabOrbState water = Water("provided");
            LabOrbState copied = water.Copy();
            LabOrbState restored = JsonUtility.FromJson<LabOrbState>(JsonUtility.ToJson(copied));
            Assert.That(restored.jigwiABSupplyEventId, Is.EqualTo(First));
            Assert.That(restored.originOwner, Is.EqualTo(10ul));
            Assert.That(LabJigwiSupplyIdentity.Valid(restored, Session, 1u, Members()), Is.True);
            restored.owner = 20ul; restored.ownershipVersion++;
            Assert.That(restored.originOwner, Is.EqualTo(10ul), "The original quota recipient is independent of the current holder.");
            Assert.That(LabJigwiSupplyIdentity.Valid(restored, Session, 1u, Members()), Is.True);
            LabOrbState legacy = Water("manual", null); legacy.jigwiSupply = true;
            Assert.That(LabJigwiSupplyIdentity.Valid(legacy, Session, 1u, Members()), Is.True);
        }

        [TestCase("event-session")] [TestCase("event-round")] [TestCase("event-A")] [TestCase("event-padding")]
        [TestCase("kind")] [TestCase("element")] [TestCase("supply")] [TestCase("origin")] [TestCase("quota-owner")]
        public void MalformedSupplyMetadataIsRejectedBeforeInstallingAFrame(string corruption)
        {
            var water = Water("bad");
            switch (corruption)
            {
                case "event-session": water.jigwiABSupplyEventId = "jigwi-ab/other/1/1"; break;
                case "event-round": water.jigwiABSupplyEventId = "jigwi-ab/" + Session + "/2/1"; break;
                case "event-A": water.jigwiABSupplyEventId = "jigwi-ab/" + Session + "/1/0"; break;
                case "event-padding": water.jigwiABSupplyEventId = "jigwi-ab/" + Session + "/1/01"; break;
                case "kind": water.kind = LabOrbKind.Yin; break;
                case "element": water.element = LabOrbElement.Fire; break;
                case "supply": water.jigwiSupply = false; break;
                case "origin": water.origin = LabOrbOrigin.Generated; break;
                case "quota-owner": water.originOwner = 99ul; break;
            }
            var replica = Replica();
            Assert.That(replica.TryApply(Frame(1, water), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.Zero);
            Assert.That(replica.TryGetState("bad", out _), Is.False);
        }

        [Test]
        public void PublisherTreatsSupplyIdentityAsMetadataRatherThanIgnoringItAsLocalPose()
        {
            var publisher = new LabBoardPublisher(); var water = Water("water", null); water.jigwiSupply = true;
            LabSnapshot snapshot = Snapshot(25d, water);
            publisher.Build(snapshot, 10ul, 1, 10ul);
            water.jigwiABSupplyEventId = First; water.revision++; snapshot.serverTime = 26d;
            LabBoardFrame changed = publisher.Build(snapshot, 10ul, 2, 11ul);
            Assert.That(changed.states, Has.Length.EqualTo(1));
            Assert.That(changed.states[0].jigwiABSupplyEventId, Is.EqualTo(First));
        }

        [Test]
        public void RetirementIsBoundedRoundScopedAndNeverCancelsOrdinaryWater()
        {
            var gate = new LabJigwiSupplyRetirementGate(); gate.SetActive(Session, 1u);
            Assert.That(gate.TryRetire("other", 1u, First), Is.False);
            Assert.That(gate.TryRetire(Session, 2u, First), Is.False);
            for (int slot = 0; slot < 8; slot++)
                Assert.That(gate.TryRetire(Session, 1u, "jigwi-ab/" + Session + "/1/" + slot),
                    Is.EqualTo(slot == 1 || slot == 3 || slot == 5));
            Assert.That(gate.Count, Is.EqualTo(3));
            Assert.That(gate.Cancels(Water("provided")), Is.True);
            Assert.That(gate.Cancels(Water("normal", null)), Is.False);
            Assert.That(gate.TryRetire(Session, 1u, First), Is.False);
            gate.SetActive(Session, 1u); Assert.That(gate.Count, Is.EqualTo(3));
            gate.SetActive(Session, 2u); Assert.That(gate.Count, Is.Zero);
            Assert.That(gate.Cancels(First), Is.False);
        }

        [Test]
        public void CompletedMissionFiltersLateCheckpointsReceiptsAndBulkSnapshotsWithoutAStaminaPurge()
        {
            var replica = Replica();
            var drum = new LabOrbState { id = "drum", owner = 10ul, kind = LabOrbKind.Drum, ownershipVersion = 1ul, x = .5f, y = .4f };
            Assert.That(replica.TryApply(Frame(1, Water("provided"), Water("normal", null), drum), out _), Is.True);
            LabSnapshot ended = Snapshot(29d); ended.jigwiAB = FirstMission(true);
            Assert.That(LabJigwiABNetworkValidation.Valid(ended.jigwiAB, ended.players), Is.True, "Use a real valid B completion contract.");
            replica.ObserveJigwiSupplySnapshot(ended);
            Assert.That(replica.TryGetState("provided", out _), Is.False);
            Assert.That(replica.TryGetState("normal", out _), Is.True);
            Assert.That(replica.TryGetState("drum", out _), Is.True);

            Assert.That(replica.TryApply(Frame(2, Water("provided"), Water("normal", null), drum), out _), Is.True);
            Assert.That(replica.TryGetState("provided", out _), Is.False, "A late checkpoint cannot clear the scoped retirement identity.");
            var receipt = new LabActionReceipt { sessionId = Session, round = 1u, sequence = 3, requestId = 30,
                accepted = true, modelRevision = 100ul, serverTime = 28d, states = new[] { Water("provided") }, players = Members() };
            Assert.That(replica.ApplyReceipt(receipt), Is.True, "The ordered ledger still consumes late approvals.");
            Assert.That(replica.LatestSequence, Is.EqualTo(3));
            Assert.That(replica.TryGetState("provided", out _), Is.False);
            var remote = Water("remote-provided"); remote.owner = 20ul; remote.inFlight = true;
            var remoteNormal = Water("remote-normal", null); remoteNormal.owner = 20ul;
            LabSnapshot late = Snapshot(27d, Water("provided"), remote, remoteNormal); late.jigwiAB = FirstMission(false);
            LabSnapshot visible = replica.Overlay(late);
            Assert.That(visible.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { "normal", "remote-normal" }));
            Assert.That(visible.players.Single().stamina, Is.EqualTo(73f), "Only the existing failed-B global fence may zero stamina.");
            Assert.That(visible.drums.Single().id, Is.EqualTo("drum"));
        }

        [Test]
        public void ACompletedOwnerBoardHeaderRetiresWaterBeforeApplyingItsPayloadButAllowsTheNextB()
        {
            var replica = Replica();
            LabBoardFrame first = Frame(1, Water("old")); first.jigwiAB = FirstMission(false);
            Assert.That(replica.TryApply(first, out _), Is.True);
            LabBoardFrame ended = Frame(2, Water("old"), Water("next", "jigwi-ab/" + Session + "/1/3"));
            ended.jigwiAB = FirstMission(true); ended.serverTime = 29d;
            Assert.That(replica.TryApply(ended, out _), Is.True);
            Assert.That(replica.TryGetState("old", out _), Is.False);
            Assert.That(replica.TryGetState("next", out _), Is.True);
            replica.SetActive(Session, 2u, 10ul);
            LabBoardFrame nextRound = Frame(1, Water("fresh", "jigwi-ab/" + Session + "/2/1")); nextRound.round = 2u;
            Assert.That(replica.TryApply(nextRound, out _), Is.True);
            Assert.That(replica.TryGetState("fresh", out _), Is.True);
        }

        [TestCase(LabPhase.Victory)] [TestCase(LabPhase.Defeat)]
        public void TerminalMetadataRetiresAllBSlotsEvenWhenTheirFinalOutcomeArrivesLater(LabPhase phase)
        {
            var gate = new LabJigwiSupplyRetirementGate(); gate.SetActive(Session, 1u);
            Assert.That(gate.Observe(Session, 1u, FirstMission(false), phase), Is.True);
            Assert.That(gate.Count, Is.EqualTo(3));
            Assert.That(gate.Cancels(First), Is.True);
            Assert.That(gate.Observe(Session, 1u, FirstMission(false), LabPhase.Playing), Is.False);
            Assert.That(gate.Cancels(First), Is.True);
        }
    }
}
