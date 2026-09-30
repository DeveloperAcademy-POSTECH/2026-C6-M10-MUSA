using System;
using System.Collections.Generic;
using System.Reflection;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class TransferReceiptAndWireTests
    {
        private GameObject root;
        private AttackSession session;
        private string nonce;
        private int resolved;
        private const string SessionId = "transfer-late-receipt", OrbId = "same-transferred-id", RequestId = "first-handoff";
        [SetUp] public void SetUp()
        {
            resolved = 0;
            root = new GameObject("Transfer receipt test"); root.SetActive(false);
            session = root.AddComponent<AttackSession>();
            var connection = root.GetComponent<DirectConnectionSession>();
            var players = (List<ulong>)Get(connection, "participants"); players.Add(0); players.Add(1);
            Set(session, "connection", connection);
            session.SetAggregateMode(true); session.ConfigureTransfers(true, 20, .055f);
            nonce = Guid.NewGuid().ToString("N"); Set(session, "localNonce", nonce);
            Set(session, "<Snapshot>k__BackingField", Snapshot(1, Wire(0, 0, 0, 0, EntrySide.None)));
            var submitted = (Dictionary<string, OrbActionRequest>)Get(session, "submitted");
            submitted.Add(RequestId, new OrbActionRequest(SessionId, 1, RequestId, OrbId, null,
                OrbActionKind.TransferLeft, 1, new Vector2(.2f, .6f)));
            session.RequestResolved += _ => resolved++;
        }
        [TearDown] public void TearDown() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }

        [Test]
        public void LateApprovalAfterAtoBtoADoesNotWaitForOwnerToRemainB()
        {
            Deliver(Reply()); Assert.That(resolved, Is.Zero);
            var returned = Wire(0, 2, 2, 2, EntrySide.Left);
            Set(session, "<Snapshot>k__BackingField", Snapshot(3, returned));
            session.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1)); Assert.That(session.LastResult.accepted, Is.True);
            Assert.That(session.Snapshot.orbs.Length, Is.EqualTo(1));
            Assert.That(session.Snapshot.orbs[0], Is.SameAs(returned));
            Assert.That(returned.owner, Is.Zero); Assert.That(returned.transferCount, Is.EqualTo(2));
        }

        [Test]
        public void LateApprovalAfterReceiverLaunchUsesConfirmedHistoryWithoutRespawning()
        {
            Deliver(Reply());
            var launched = Wire(1, 2, 1, 1, EntrySide.Right); launched.state = (int)OrbAuthorityState.Projectile;
            Set(session, "<Snapshot>k__BackingField", Snapshot(3, launched));
            session.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1));
            Assert.That(session.Snapshot.orbs[0].state, Is.EqualTo((int)OrbAuthorityState.Projectile));
            Assert.That(session.Snapshot.orbs[0].owner, Is.EqualTo(1));
            Assert.That(session.ActiveProjectileCount, Is.Zero, "Receipt reconciliation never spawns Host physics.");
        }

        [Test]
        public void LateApprovalAfterReceiverConsumedTheOrbAcceptsConfirmedAbsence()
        {
            Deliver(Reply());
            Set(session, "<Snapshot>k__BackingField", Snapshot(4));
            session.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1)); Assert.That(session.Snapshot.orbs, Is.Empty);
        }

        [Test]
        public void AnOlderInventoryOrNoTransferHistoryCannotReleaseApproval()
        {
            Deliver(Reply()); session.NotifyAggregateChanged(); Assert.That(resolved, Is.Zero);
            Set(session, "<Snapshot>k__BackingField", Snapshot(2, Wire(0, 0, 0, 0, EntrySide.None)));
            session.NotifyAggregateChanged(); Assert.That(resolved, Is.Zero);
        }

        [Test]
        public void QueryRejectionForAnOrbAlreadyOwnedByPeerCannotRestoreLocalOwnership()
        {
            var reply = Reply(); reply.accepted = false; reply.reason = "OWNER_MISMATCH";
            Deliver(reply); Assert.That(resolved, Is.Zero);
            var current = Wire(1, 1, 1, 1, EntrySide.Right);
            Set(session, "<Snapshot>k__BackingField", Snapshot(2, current)); session.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1)); Assert.That(session.LastResult.accepted, Is.False);
            Assert.That(session.LastResult.confirmedOrb, Is.SameAs(current)); Assert.That(current.owner, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RawReceiptWithAChangedElementCannotResolveAgainstTheSameInventoryIdentity(bool accepted)
        {
            session.ConfigureSelectedElements(new Dictionary<ulong, OrbElement>
                { { 0, OrbElement.Fire }, { 1, OrbElement.Water } });
            var returned = RawWire(0, 2, 2, 2, EntrySide.Left, OrbElement.Fire);
            Set(session, "<Snapshot>k__BackingField", Snapshot(3, returned));
            var tampered = Reply(); tampered.accepted = accepted;
            tampered.confirmedOrb = RawWire(1, 1, 1, 1, EntrySide.Right, OrbElement.Water);
            Assert.That(AttackWire.ValidOrb(tampered.confirmedOrb, true), Is.True,
                "The changed element is schema-valid, so rejection must compare the same-ID aggregate.");
            Deliver(tampered);
            session.NotifyAggregateChanged();
            Assert.That(resolved, Is.Zero);
            Assert.That(session.LastResult, Is.Null);
            Assert.That(session.Snapshot.orbs[0], Is.SameAs(returned));
            Assert.That(returned.rawElement, Is.EqualTo(OrbElement.Fire));
            Assert.That(returned.owner, Is.Zero, "A late receipt cannot reverse a legitimate return transfer.");

            var corrected = Reply(); corrected.accepted = accepted;
            corrected.confirmedOrb = RawWire(1, 1, 1, 1, EntrySide.Right, OrbElement.Fire);
            Deliver(corrected);
            Assert.That(resolved, Is.EqualTo(1));
            Assert.That(session.LastResult.confirmedOrb.rawElement, Is.EqualTo(OrbElement.Fire));
            if (!accepted) Assert.That(session.LastResult.confirmedOrb, Is.SameAs(returned));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MatchingRawReceiptAllowsAdvancedOwnershipAndAuthoritativePrunedAbsence(bool pruned)
        {
            session.ConfigureSelectedElements(new Dictionary<ulong, OrbElement>
                { { 0, OrbElement.Fire }, { 1, OrbElement.Water } });
            Set(session, "<Snapshot>k__BackingField", Snapshot(1, RawWire(0, 0, 0, 0, EntrySide.None, OrbElement.Fire)));
            var reply = Reply(); reply.confirmedOrb = RawWire(1, 1, 1, 1, EntrySide.Right, OrbElement.Fire);
            Deliver(reply); Assert.That(resolved, Is.Zero, "The inventory revision still gates a valid receipt.");
            var returned = RawWire(0, 2, 2, 2, EntrySide.Left, OrbElement.Fire);
            Set(session, "<Snapshot>k__BackingField", pruned ? Snapshot(4) : Snapshot(3, returned));
            session.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1));
            Assert.That(session.LastResult.accepted, Is.True);
            Assert.That(session.ActiveProjectileCount, Is.Zero);
            if (pruned) Assert.That(session.Snapshot.orbs, Is.Empty, "Consumed Raw is never restored by a receipt.");
            else
            {
                Assert.That(session.Snapshot.orbs[0], Is.SameAs(returned));
                Assert.That(returned.owner, Is.Zero);
                Assert.That(returned.rawElement, Is.EqualTo(OrbElement.Fire));
            }
        }

        [Test]
        public void TransferMetadataSurvivesRecordAndJsonRoundTrips()
        {
            var record = new OrbRecord(OrbId, OrbKind.Combined, OrbPolarity.None, 1, OrbAuthorityState.Projectile,
                new Vector2(.2f, .6f), EntrySide.Right, 9, 3, 7);
            var wire = OrbWire.FromRecord(record);
            var copy = JsonUtility.FromJson<OrbWire>(JsonUtility.ToJson(wire));
            Assert.That(AttackWire.ValidOrb(copy), Is.True);
            var back = copy.ToRecord();
            Assert.That(back.TransferCount, Is.EqualTo(3)); Assert.That(back.LastTransferSequence, Is.EqualTo(7));
            Assert.That(back.EntrySide, Is.EqualTo(EntrySide.Right)); Assert.That(back.SequenceNumber, Is.EqualTo(9));
            Assert.That(back.OwnerPlayerId, Is.EqualTo(1)); Assert.That(back.OrbId, Is.EqualTo(OrbId));
        }

        [TestCase(0UL, 1UL, EntrySide.None)]
        [TestCase(0UL, 0UL, EntrySide.Left)]
        [TestCase(1UL, 0UL, EntrySide.Right)]
        [TestCase(2UL, 1UL, EntrySide.Left)]
        [TestCase(1UL, 3UL, EntrySide.Right)]
        [TestCase(1UL, 1UL, EntrySide.None)]
        public void ImpossibleTransferMetadataIsRejected(ulong count, ulong lastTransfer, EntrySide side)
        {
            Assert.That(AttackWire.ValidOrb(Wire(1, 2, count, lastTransfer, side)), Is.False);
        }
        private void Deliver(AttackRequestReply reply) => typeof(AttackSession).GetMethod("DeliverReply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { reply });
        private AttackRequestReply Reply() => new AttackRequestReply
        {
            nonce = nonce, sessionId = SessionId, roundId = 1, requestId = RequestId, orbId = OrbId,
            known = true, accepted = true, reason = "TRANSFER_APPROVED", inventoryRevision = 2,
            confirmedOrb = Wire(1, 1, 1, 1, EntrySide.Right)
        };
        private AttackSnapshot Snapshot(ulong revision, params OrbWire[] orbs) => new AttackSnapshot
        { nonce = nonce, sessionId = SessionId, roundId = 1, revision = revision, hp = 100, maxHp = 100, state = "Playing", orbs = orbs, projectiles = Array.Empty<ProjectileWire>() };
        private static OrbWire Wire(ulong owner, ulong sequence, ulong count, ulong lastTransfer, EntrySide side) => new OrbWire
        { id = OrbId, owner = owner, sequence = sequence, transferCount = count, lastTransferSequence = lastTransfer,
            entrySide = (int)side, kind = (int)OrbKind.Combined, polarity = (int)OrbPolarity.None,
            state = (int)OrbAuthorityState.Idle, pos = new Vector2(.2f, .6f) };
        private static OrbWire RawWire(ulong owner, ulong sequence, ulong count, ulong lastTransfer, EntrySide side, OrbElement element)
        {
            var value = Wire(owner, sequence, count, lastTransfer, side);
            value.kind = (int)OrbKind.Raw; value.polarity = (int)OrbPolarity.Yin; value.rawElement = element;
            return value;
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
