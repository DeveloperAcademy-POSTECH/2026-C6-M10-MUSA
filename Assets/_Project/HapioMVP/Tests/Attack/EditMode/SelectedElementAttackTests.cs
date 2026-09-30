using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    /// <summary>Host owner/element gate before reservation, independent of local drag presentation.</summary>
    public sealed class SelectedElementAttackTests
    {
        private const string SessionId = "selection-attack";
        private HostOrbRegistry registry;
        private AttackAuthority authority;
        private static readonly OrbElement[] Elements =
            { OrbElement.Fire, OrbElement.Water, OrbElement.Wood, OrbElement.Metal, OrbElement.Earth };
        private static Dictionary<ulong, OrbElement> Choices() => new Dictionary<ulong, OrbElement>
            { { 7, OrbElement.Fire }, { 8, OrbElement.Water }, { 9, OrbElement.Wood }, { 10, OrbElement.Metal }, { 11, OrbElement.Earth } };

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true);
            registry.ConfigureExplicitRawElements();
            registry.BeginSession(SessionId, 1);
            authority = new AttackAuthority(registry, 100, 20);
            authority.ConfigureSelectedElements(Choices());
            authority.BeginDevelopmentRound();
        }

        private static IEnumerable EveryOwnerAndElement()
        {
            for (int owner = 0; owner < Elements.Length; owner++)
                foreach (var element in Elements)
                    yield return new TestCaseData((ulong)(owner + 7), Elements[owner], element);
        }

        [TestCaseSource(nameof(EveryOwnerAndElement))]
        public void OnlyTheCurrentOwnersSelectedCombinedElementCanLaunch(ulong owner, OrbElement selected, OrbElement carried)
        {
            var orb = Combine(owner, carried);
            int rewards = 0;
            authority.ValidHit += _ => rewards++;
            var request = Launch(orb, "throw", 1);
            var result = authority.RequestLaunch(owner, request);
            if (selected == carried)
            {
                Assert.That(result.Accepted && result.SpawnRequired, Is.True);
                Assert.That(authority.MarkProjectileSpawned(SessionId, 1, orb.OrbId), Is.True);
                Assert.That(authority.ProcessHostHit(SessionId, 1, orb.OrbId).Applied, Is.True);
                Assert.That(authority.MonsterHp, Is.EqualTo(80));
                Assert.That(rewards, Is.EqualTo(1));
            }
            else
            {
                Assert.That(result.Reason, Is.EqualTo("SELECTED_ELEMENT_MISMATCH"));
                Assert.That(result.Accepted || result.SpawnRequired || result.BallisticLaunch.HasValue, Is.False);
                AssertUnspent(orb);
                Assert.That(authority.MonsterHp, Is.EqualTo(100));
                Assert.That(authority.ValidHitCount, Is.Zero);
                Assert.That(rewards, Is.Zero);
                var duplicate = authority.RequestLaunch(owner, request);
                Assert.That(duplicate.IsDuplicate, Is.True);
                Assert.That(duplicate.Accepted || duplicate.SpawnRequired, Is.False);
                Assert.That(authority.QueryRequest(owner, request).Receipt.Reason, Is.EqualTo(result.Reason));
                Assert.That(registry.Reserve(owner, TransferRequest(orb, "can-still-transfer", 1)).Accepted, Is.True,
                    "A refused attack cannot spend the sequence or reserve the orb.");
            }
        }

        [Test]
        public void AForeignRecipientCannotAttackButCanReturnTheSameIdentityToItsEligibleOwner()
        {
            var orb = Combine(7, OrbElement.Fire);
            var forwarded = authority.RequestTransfer(7, TransferRequest(orb, "to-water", 1), 8, true, 20, .05f);
            Assert.That(forwarded.Accepted, Is.True);
            var refused = authority.RequestLaunch(8, Launch(forwarded.Orb, "wrong-owner-element", 2));
            Assert.That(refused.Reason, Is.EqualTo("SELECTED_ELEMENT_MISMATCH"));
            AssertUnspent(forwarded.Orb);
            var returned = authority.RequestTransfer(8, TransferRequest(forwarded.Orb, "back-to-fire", 2), 7, true, 20, .05f);
            Assert.That(returned.Accepted, Is.True, "Refused throw leaves the same sequence available for transfer.");
            Assert.That(returned.Orb.OrbId, Is.EqualTo(orb.OrbId));
            var accepted = authority.RequestLaunch(7, Launch(returned.Orb, "eligible-owner", 3));
            Assert.That(accepted.SpawnRequired, Is.True);
            Assert.That(accepted.Orb.OwnerPlayerId, Is.EqualTo(7));
        }

        [Test]
        public void TheCombinerCanDifferFromTheCurrentEligibleAttacker()
        {
            var yin = Raw(7, OrbPolarity.Yin, OrbElement.Fire);
            var yang = Raw(7, OrbPolarity.Yang, OrbElement.Fire);
            var yinMoved = authority.RequestTransfer(7, TransferRequest(yin, "yin-to-water", 1), 8, true, 20, .05f);
            var yangMoved = authority.RequestTransfer(7, TransferRequest(yang, "yang-to-water", 1), 8, true, 20, .05f);
            Assert.That(yinMoved.Accepted && yangMoved.Accepted, Is.True);
            var reservation = registry.Reserve(8, new OrbActionRequest(SessionId, 1, "water-combines-fire",
                yin.OrbId, yang.OrbId, OrbActionKind.Combine, 2, Vector2.one * .5f));
            Assert.That(reservation.Accepted, Is.True, "Selected element restricts attacks and generation, not combining foreign storage.");
            Assert.That(registry.TryCompleteReservedCombination(reservation.Reservation, Vector2.one * .5f,
                out _, out _, out var combined), Is.True);
            var sent = authority.RequestTransfer(8, TransferRequest(combined, "comb-to-fire", 1), 7, true, 20, .05f);
            Assert.That(sent.Accepted, Is.True);
            Assert.That(authority.RequestLaunch(7, Launch(sent.Orb, "fire-throws-water-made-orb", 2)).SpawnRequired, Is.True);
        }

        [Test]
        public void ForgedSenderCannotUseAnotherPlayersMatchingSelection()
        {
            var water = Combine(8, OrbElement.Water);
            var refused = authority.RequestLaunch(7, Launch(water, "forged-sender", 1));
            Assert.That(refused.Reason, Is.EqualTo("OWNER_MISMATCH"));
            AssertUnspent(water);
            Assert.That(authority.RequestLaunch(8, Launch(water, "real-owner", 1)).Accepted, Is.True);
        }

        [Test]
        public void MissingOwnerSelectionAndMixedEncodedPairFailBeforeAnyReservation()
        {
            var unknown = Combine(42, OrbElement.Fire);
            Assert.That(authority.RequestLaunch(42, Launch(unknown, "unselected", 1)).Reason,
                Is.EqualTo("SELECTED_ELEMENT_MISMATCH"));
            AssertUnspent(unknown);
            string mixedId = OrbElements.NewCombinedId(OrbElement.Fire, OrbElement.Water);
            var mixed = new OrbRecord(mixedId, OrbKind.Combined, OrbPolarity.None, 7,
                OrbAuthorityState.Idle, Vector2.one * .5f, EntrySide.None, 0);
            // Deliberately corrupt the Host record to test the gate against a legacy mixed pair.
            var all = (Dictionary<string, OrbRecord>)typeof(HostOrbRegistry)
                .GetField("orbs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
            all.Add(mixedId, mixed);
            var refused = authority.RequestLaunch(7, Launch(mixed, "mixed-pair", 1));
            Assert.That(refused.Reason, Is.EqualTo("SELECTED_ELEMENT_MISMATCH"));
            Assert.That(refused.SpawnRequired, Is.False);
            AssertUnspent(mixed);
        }

        [Test]
        public void ApprovedMapIsCopiedAndCannotChangeAfterRoundActions()
        {
            authority.EndDevelopmentRound();
            registry.ResetRound(2);
            var choices = Choices();
            authority.ConfigureSelectedElements(choices);
            choices[7] = OrbElement.Water;
            authority.BeginDevelopmentRound();
            var orb = Combine(7, OrbElement.Fire);
            Assert.That(authority.RequestLaunch(7, Launch(orb, "copy-is-frozen", 1)).Accepted, Is.True);
            Assert.Throws<InvalidOperationException>(() => authority.ConfigureSelectedElements(Choices()));
        }

        [Test]
        public void InvalidOrDuplicateSelectionsAreRejectedBeforeBinding()
        {
            var fresh = new AttackAuthority(new HostOrbRegistry(true), 100, 20);
            Assert.Throws<ArgumentException>(() => fresh.ConfigureSelectedElements(null));
            var duplicate = new Dictionary<ulong, OrbElement> { { 7, OrbElement.Fire }, { 8, OrbElement.Fire } };
            Assert.Throws<ArgumentException>(() => fresh.ConfigureSelectedElements(duplicate));
            duplicate[8] = OrbElement.None;
            Assert.Throws<ArgumentException>(() => fresh.ConfigureSelectedElements(duplicate));
        }

        private OrbRecord Raw(ulong owner, OrbPolarity polarity, OrbElement element) =>
            registry.RegisterGeneratedRaw(SessionId, registry.RoundId, owner, polarity, Vector2.one * .5f, element);
        private OrbRecord Combine(ulong owner, OrbElement element)
        {
            var yin = Raw(owner, OrbPolarity.Yin, element);
            var yang = Raw(owner, OrbPolarity.Yang, element);
            var reservation = registry.Reserve(owner, new OrbActionRequest(SessionId, registry.RoundId, "combine-" + yin.OrbId,
                yin.OrbId, yang.OrbId, OrbActionKind.Combine, 1, Vector2.one * .5f));
            Assert.That(reservation.Accepted, Is.True);
            Assert.That(registry.TryCompleteReservedCombination(reservation.Reservation, Vector2.one * .5f,
                out _, out _, out var combined), Is.True);
            return combined;
        }
        private OrbActionRequest Launch(OrbRecord orb, string request, ulong sequence) =>
            new OrbActionRequest(SessionId, registry.RoundId, request, orb.OrbId, null, OrbActionKind.Launch, sequence, Vector2.one * .8f);
        private OrbActionRequest TransferRequest(OrbRecord orb, string request, ulong sequence) =>
            new OrbActionRequest(SessionId, registry.RoundId, request, orb.OrbId, null, OrbActionKind.TransferRight, sequence, Vector2.one * .5f);
        private void AssertUnspent(OrbRecord expected)
        {
            Assert.That(registry.IsPending(expected.OrbId), Is.False);
            Assert.That(registry.TryGet(expected.OrbId, out var current), Is.True);
            Assert.That(current, Is.SameAs(expected));
            Assert.That(current.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(current.OwnerPlayerId, Is.EqualTo(expected.OwnerPlayerId));
        }
    }
}
