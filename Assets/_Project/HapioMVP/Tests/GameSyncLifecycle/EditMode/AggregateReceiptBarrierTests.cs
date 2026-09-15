using System;
using System.Collections.Generic;
using System.Reflection;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Resources;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.GameSyncLifecycle.Tests
{
    // These tests exercise receipt delivery rather than private networking callbacks. The
    // full Host/Client probes cover transport and atomic aggregate application separately.
    public sealed class AggregateReceiptBarrierTests
    {
        private GameObject root;
        private AttackSession attack;
        private ResourceSession resources;
        private string nonce;
        private const string Session = "approved-session";
        private const string Orb = "confirmed-orb";
        private const string Request = "launch-request";
        private int resolved;

        [SetUp]
        public void SetUp()
        {
            resolved = 0;
            root = new GameObject("Aggregate receipt barrier test");
            root.SetActive(false);
            attack = root.AddComponent<AttackSession>();
            resources = root.AddComponent<ResourceSession>();
            nonce = Guid.NewGuid().ToString("N");
            attack.SetAggregateMode(true);
            Set(attack, "localNonce", nonce);
            Set(attack, "<Snapshot>k__BackingField", Inventory(1, OrbAuthorityState.Idle));
            ((Dictionary<string, OrbActionRequest>)Get(attack, "submitted")).Add(Request,
                new OrbActionRequest(Session, 1, Request, Orb, null, OrbActionKind.Launch, 1, new Vector2(.5f, .5f)));
            attack.RequestResolved += _ => resolved++;
        }
        [TearDown]
        public void TearDown() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }

        [Test]
        public void ApprovalCannotResolveAgainstAnOlderIdleInventory()
        {
            var reply = Reply(true, 2);
            Invoke(attack, "DeliverReply", reply);
            Assert.That(resolved, Is.Zero);
            attack.NotifyAggregateChanged();
            Assert.That(resolved, Is.Zero);
            Set(attack, "<Snapshot>k__BackingField", Inventory(2, OrbAuthorityState.Projectile));
            Assert.That(resolved, Is.Zero, "Installing data alone must not deliver a partial-state event.");
            attack.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1));
            Assert.That(attack.LastResult.accepted, Is.True);
        }

        [Test]
        public void ANewerConsumedInventoryCanResolveAProjectileReceipt()
        {
            Invoke(attack, "DeliverReply", Reply(true, 2));
            var consumed = Inventory(3, OrbAuthorityState.Consumed);
            consumed.orbs = Array.Empty<OrbWire>();
            Set(attack, "<Snapshot>k__BackingField", consumed);
            attack.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1));
        }

        [Test]
        public void EqualRevisionWithIdleOrbStillCannotResolveAnApproval()
        {
            Set(attack, "<Snapshot>k__BackingField", Inventory(2, OrbAuthorityState.Idle));
            Invoke(attack, "DeliverReply", Reply(true, 2));
            attack.NotifyAggregateChanged();
            Assert.That(resolved, Is.Zero);
        }

        [Test]
        public void RejectionWaitsForItsInventoryAndUsesTheCurrentRecord()
        {
            Invoke(attack, "DeliverReply", Reply(false, 2));
            Assert.That(resolved, Is.Zero);
            var current = Inventory(3, OrbAuthorityState.Idle);
            current.orbs[0].pos = new Vector2(.2f, .7f);
            Set(attack, "<Snapshot>k__BackingField", current);
            attack.NotifyAggregateChanged();
            Assert.That(resolved, Is.EqualTo(1));
            Assert.That(attack.LastResult.confirmedOrb.pos, Is.EqualTo(current.orbs[0].pos));
        }

        [Test]
        public void AStillReservedKnownReplyCannotUnlockAnOrb()
        {
            var reply = Reply(false, 1); reply.pending = true;
            Invoke(attack, "DeliverReply", reply);
            attack.NotifyAggregateChanged();
            Assert.That(resolved, Is.Zero);
        }

        [Test]
        public void OldRoundReceiptCannotEnterDeferredDelivery()
        {
            var reply = Reply(true, 2); reply.roundId = 99;
            Invoke(attack, "DeliverReply", reply);
            Set(attack, "<Snapshot>k__BackingField", Inventory(2, OrbAuthorityState.Projectile));
            attack.NotifyAggregateChanged();
            Assert.That(resolved, Is.Zero);
        }

        [Test]
        public void LegacyModeStillDeliversAnAuthenticatedApprovalImmediately()
        {
            attack.SetAggregateMode(false);
            Invoke(attack, "DeliverReply", Reply(true, 2));
            Assert.That(resolved, Is.EqualTo(1));
        }

        [TestCase(1UL, 2UL, 1UL, false)]
        [TestCase(2UL, 1UL, 1UL, false)]
        [TestCase(2UL, 2UL, 0UL, false)]
        [TestCase(2UL, 2UL, 1UL, true)]
        [TestCase(3UL, 3UL, 2UL, true)]
        public void GenerationRequiresBothConfirmedRevisionsAndSequence(ulong inventoryRevision,
            ulong resourceRevision, ulong sequence, bool expected)
        {
            resources.SetAggregateMode(true);
            Set(resources, "attack", attack);
            Set(attack, "<Snapshot>k__BackingField", Inventory(inventoryRevision, OrbAuthorityState.Idle));
            Set(resources, "<Snapshot>k__BackingField", new ResourceSnapshot
            {
                nonce = nonce, sessionId = Session, roundId = 1, revision = resourceRevision,
                players = new[] { new ResourcePlayerWire { playerId = 0, lastSequence = sequence, generatedTotal = 1 } }
            });
            var reply = new ResourceRequestReply
            {
                accepted = true, known = true, sequence = 1, operation = (int)ResourceRequestKind.Generate,
                inventoryRevision = 2, resourceRevision = 2, confirmedOrb = Wire(OrbAuthorityState.Idle)
            };
            Assert.That((bool)Invoke(resources, "AcceptedAggregateReceiptConfirmed", reply), Is.EqualTo(expected));
        }

        private AttackRequestReply Reply(bool accepted, ulong revision) => new AttackRequestReply
        {
            nonce = nonce, sessionId = Session, roundId = 1, requestId = Request, orbId = Orb,
            accepted = accepted, known = true, reason = accepted ? "LAUNCH_APPROVED" : "REJECTED",
            inventoryRevision = revision, confirmedOrb = Wire(accepted ? OrbAuthorityState.Projectile : OrbAuthorityState.Idle)
        };
        private AttackSnapshot Inventory(ulong revision, OrbAuthorityState state) => new AttackSnapshot
        {
            nonce = nonce, sessionId = Session, roundId = 1, revision = revision, hp = 100, maxHp = 100,
            state = "Playing", orbs = new[] { Wire(state) }, projectiles = Array.Empty<ProjectileWire>()
        };
        private OrbWire Wire(OrbAuthorityState state) => new OrbWire
        { id = Orb, owner = 0, kind = (int)OrbKind.Combined, polarity = (int)OrbPolarity.None, state = (int)state, pos = new Vector2(.5f, .5f) };
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        private static object Get(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static object Invoke(object instance, string name, params object[] arguments) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, arguments);
    }
}
