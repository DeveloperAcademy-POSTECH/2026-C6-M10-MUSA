using System;
using System.Collections.Generic;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Combination.Tests
{
    /// <summary>
    /// 오행 v2. Only a Yin and a Yang of the SAME element combine (예: 불 음 + 불 양). The combined
    /// orb's ID carries that element in both slots so every device shows the same artwork without
    /// any wire change. These tests run the real Host combination path, not the sandbox.
    /// </summary>
    public sealed class OrbElementCombinationTests
    {
        private HostOrbRegistry registry;
        private HostCombinationAuthority authority;

        [SetUp]
        public void SetUp()
        {
            OrbElements.Configure(OrbElements.AllElements);
            Fresh();
        }

        // Back to the default (elements off) so other fixtures are not affected by this one.
        [TearDown]
        public void TearDown() => OrbElements.Configure(null);

        /// <summary>A same-element Yin + Yang combines, and the combined ID carries that element twice.</summary>
        [Test]
        public void SameElementYinAndYangCombineIntoThatElement(
            [ValueSource(nameof(Elements))] OrbElement element,
            [Values(OrbPolarity.Yin, OrbPolarity.Yang)] OrbPolarity first)
        {
            var second = first == OrbPolarity.Yin ? OrbPolarity.Yang : OrbPolarity.Yin;
            var source = AddWithElement(first, element);
            var target = AddWithElement(second, element);
            var result = authority.Combine(7, Request(source, target, "same-" + element + "-" + first));
            Assert.That(result.Accepted, Is.True, result.Reason);

            OrbElements.CombinedElements(result.Combined.OrbId, out var yin, out var yang);
            Assert.That(yin, Is.EqualTo(element), "id=" + result.Combined.OrbId);
            Assert.That(yang, Is.EqualTo(element), "id=" + result.Combined.OrbId);
            Assert.That(Guid.TryParseExact(result.Combined.OrbId, "N", out _), Is.True,
                "The encoded ID must stay a valid Guid \"N\" string: " + result.Combined.OrbId);
        }

        /// <summary>A Yin and a Yang of different elements are rejected and both stay usable.</summary>
        [Test]
        public void DifferentElementsAreRejected()
        {
            var source = AddWithElement(OrbPolarity.Yin, OrbElement.Fire);
            var target = AddWithElement(OrbPolarity.Yang, OrbElement.Water);
            var result = authority.Combine(7, Request(source, target, "mismatch"));
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("ELEMENT_MISMATCH"));

            Assert.That(registry.TryGet(source.OrbId, out var sourceAfter), Is.True);
            Assert.That(registry.TryGet(target.OrbId, out var targetAfter), Is.True);
            Assert.That(sourceAfter.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(targetAfter.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(registry.IsPending(source.OrbId), Is.False);
            Assert.That(registry.IsPending(target.OrbId), Is.False);
        }

        /// <summary>A Combined without an encoded pair (DEV fixture) still shows one same-element artwork.</summary>
        [Test]
        public void UnencodedCombinedIdFallsBackToOneElement()
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                string id = "dev-fixture-" + attempt;
                OrbElements.CombinedElements(id, out var yin, out var yang);
                Assert.That(yin, Is.Not.EqualTo(OrbElement.None), id);
                Assert.That(yang, Is.EqualTo(yin), id);
            }
        }

        /// <summary>All 25 pairs survive the ID encoding, and every ID stays hex.</summary>
        [Test]
        public void EveryElementPairRoundTripsThroughTheCombinedId()
        {
            foreach (var yinElement in OrbElements.AllElements)
                foreach (var yangElement in OrbElements.AllElements)
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        string id = OrbElements.NewCombinedId(yinElement, yangElement);
                        Assert.That(Guid.TryParseExact(id, "N", out _), Is.True, id);
                        Assert.That(OrbElements.TryDecodeCombinedId(id, out var yin, out var yang), Is.True, id);
                        Assert.That(yin, Is.EqualTo(yinElement), id);
                        Assert.That(yang, Is.EqualTo(yangElement), id);
                    }
        }

        /// <summary>Known sandbox prefixes remain presentation-only; the normal authority decoder rejects them.</summary>
        [Test]
        public void PrefixedIdsDecodeToTheSamePair()
        {
            string id = "sandbox-added-" + OrbElements.NewCombinedId(OrbElement.Metal, OrbElement.Fire);
            OrbElements.CombinedElements(id, out var yin, out var yang);
            Assert.That(yin, Is.EqualTo(OrbElement.Metal));
            Assert.That(yang, Is.EqualTo(OrbElement.Fire));
            Assert.That(OrbElements.TryDecodeCombinedId(id, out _, out _), Is.False);
        }

        [TestCase(OrbElement.Fire, OrbPolarity.Yin)]
        [TestCase(OrbElement.Fire, OrbPolarity.Yang)]
        [TestCase(OrbElement.Water, OrbPolarity.Yin)]
        [TestCase(OrbElement.Water, OrbPolarity.Yang)]
        [TestCase(OrbElement.Wood, OrbPolarity.Yin)]
        [TestCase(OrbElement.Wood, OrbPolarity.Yang)]
        [TestCase(OrbElement.Metal, OrbPolarity.Yin)]
        [TestCase(OrbElement.Metal, OrbPolarity.Yang)]
        [TestCase(OrbElement.Earth, OrbPolarity.Yin)]
        [TestCase(OrbElement.Earth, OrbPolarity.Yang)]
        public void ExplicitRawElementsCombineWithGlobalHashDisabledAndPreserveConsumedMaterials(OrbElement element, OrbPolarity polarity)
        {
            OrbElements.Configure(null);
            registry.ConfigureExplicitRawElements();
            var source = registry.RegisterGeneratedRaw("session-a", 1, 7, polarity, new Vector2(.25f, .3f), element);
            var opposite = polarity == OrbPolarity.Yin ? OrbPolarity.Yang : OrbPolarity.Yin;
            var target = registry.RegisterGeneratedRaw("session-a", 1, 7, opposite, new Vector2(.3f, .3f), element);
            var result = authority.Combine(7, Request(source, target, "explicit-pair"));
            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(registry.TryGet(source.OrbId, out var consumedSource), Is.True);
            Assert.That(registry.TryGet(target.OrbId, out var consumedTarget), Is.True);
            Assert.That(consumedSource.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(consumedTarget.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(consumedSource.RawElement, Is.EqualTo(element));
            Assert.That(consumedTarget.RawElement, Is.EqualTo(element));
            Assert.That(result.Combined.RawElement, Is.EqualTo(OrbElement.None));
            Assert.That(OrbElements.TryDecodeCombinedId(result.Combined.OrbId, out var yin, out var yang), Is.True);
            Assert.That(yin, Is.EqualTo(element));
            Assert.That(yang, Is.EqualTo(element));
        }

        [Test]
        public void ExplicitMismatchIsRejectedEvenWhenBothIdsHashToSameLegacyElement()
        {
            OrbElements.Configure(new[] { OrbElement.Fire });
            registry.ConfigureExplicitRawElements();
            var source = registry.RegisterGeneratedRaw("session-a", 1, 7, OrbPolarity.Yin, Vector2.one * .5f, OrbElement.Fire);
            var target = registry.RegisterGeneratedRaw("session-a", 1, 7, OrbPolarity.Yang, Vector2.one * .5f, OrbElement.Water);
            Assert.That(OrbElements.SameElement(source.OrbId, target.OrbId), Is.True, "This demonstrates why ID-only comparison is insufficient.");
            var result = authority.Combine(7, Request(source, target, "explicit-mismatch"));
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("ELEMENT_MISMATCH"));
            Assert.That(registry.IsPending(source.OrbId), Is.False);
            Assert.That(registry.IsPending(target.OrbId), Is.False);
            Assert.That(registry.Snapshot().Count, Is.EqualTo(2));
            Assert.That(source.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(target.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
        }

        [TestCase("dev-fixture-10")]
        [TestCase("10")]
        [TestCase("not-a-guid-22")]
        [TestCase("00000000000000000000000000000g11")]
        public void NonGuidSuffixNeverCountsAsNormalEncodedCombined(string id)
        {
            Assert.That(OrbElements.TryDecodeCombinedId(id, out var yin, out var yang), Is.False);
            Assert.That(yin, Is.EqualTo(OrbElement.None));
            Assert.That(yang, Is.EqualTo(OrbElement.None));
            OrbElements.CombinedElements(id, out yin, out yang);
            Assert.That(OrbElements.IsValidRawElement(yin), Is.True);
            Assert.That(yang, Is.EqualTo(yin), "Unencoded development identity uses a single legacy element.");
        }

        private void Fresh()
        {
            registry = new HostOrbRegistry(true);
            registry.BeginSession("session-a", 1);
            authority = new HostCombinationAuthority(registry);
            authority.BeginRound();
        }

        private OrbRecord Add(OrbPolarity polarity)
            => registry.RegisterDevelopmentOrb(7, OrbKind.Raw, polarity, new Vector2(.1f, .125f));

        private static IEnumerable<OrbElement> Elements() => OrbElements.AllElements;

        /// <summary>IDs are random, so keep registering until one hashes to the wanted element.</summary>
        private OrbRecord AddWithElement(OrbPolarity polarity, OrbElement element)
        {
            for (int attempt = 0; attempt < 500; attempt++)
            {
                var orb = Add(polarity);
                if (OrbElements.RawElement(orb.OrbId) == element) return orb;
            }
            Assert.Fail("Could not create a " + element + " " + polarity + " orb.");
            return null;
        }

        private static CombinationRequest Request(OrbRecord source, OrbRecord target, string id)
            => new CombinationRequest("session-a", 1, id, source.OrbId, target.OrbId, 1,
                Vector2.one * .5f, Vector2.one * .5f);
    }
}
