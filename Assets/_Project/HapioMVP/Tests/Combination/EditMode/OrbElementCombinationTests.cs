using System;
using System.Collections.Generic;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Combination.Tests
{
    /// <summary>
    /// 오행 v1. A combined orb's ID must carry (Yin material element, Yang material element) so that
    /// every device shows the same artwork without any wire change. These tests run the real Host
    /// combination path, not the sandbox, so they are the only cheap proof that pairing is correct.
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

        [TearDown]
        public void TearDown() => OrbElements.Configure(OrbElements.AllElements);

        /// <summary>The material that is Yin decides the first slot, whichever side the request named first.</summary>
        [TestCase(OrbPolarity.Yin, OrbPolarity.Yang)]
        [TestCase(OrbPolarity.Yang, OrbPolarity.Yin)]
        public void CombinedIdCarriesMaterialElementsRegardlessOfRequestOrder(OrbPolarity first, OrbPolarity second)
        {
            var pairsSeen = new HashSet<string>();
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Fresh();
                var source = Add(first); var target = Add(second);
                var result = authority.Combine(7, Request(source, target, "combine-" + attempt));
                Assert.That(result.Accepted, Is.True, result.Reason);

                var yinMaterial = source.Polarity == OrbPolarity.Yin ? source : target;
                var yangMaterial = source.Polarity == OrbPolarity.Yin ? target : source;
                var expectedYin = OrbElements.RawElement(yinMaterial.OrbId);
                var expectedYang = OrbElements.RawElement(yangMaterial.OrbId);

                OrbElements.CombinedElements(result.Combined.OrbId, out var yin, out var yang);
                Assert.That(yin, Is.EqualTo(expectedYin),
                    "Yin slot must come from the Yin material. id=" + result.Combined.OrbId);
                Assert.That(yang, Is.EqualTo(expectedYang),
                    "Yang slot must come from the Yang material. id=" + result.Combined.OrbId);
                Assert.That(Guid.TryParseExact(result.Combined.OrbId, "N", out _), Is.True,
                    "The encoded ID must stay a valid Guid \"N\" string: " + result.Combined.OrbId);
                pairsSeen.Add(expectedYin + "_" + expectedYang);
            }
            Assert.That(pairsSeen.Count, Is.GreaterThan(5), "Materials should not collapse onto a single element pair.");
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

        /// <summary>A prefixed sandbox ID decodes the same way, since only the last two characters matter.</summary>
        [Test]
        public void PrefixedIdsDecodeToTheSamePair()
        {
            string id = "sandbox-added-" + OrbElements.NewCombinedId(OrbElement.Metal, OrbElement.Fire);
            OrbElements.CombinedElements(id, out var yin, out var yang);
            Assert.That(yin, Is.EqualTo(OrbElement.Metal));
            Assert.That(yang, Is.EqualTo(OrbElement.Fire));
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

        private static CombinationRequest Request(OrbRecord source, OrbRecord target, string id)
            => new CombinationRequest("session-a", 1, id, source.OrbId, target.OrbId, 1,
                Vector2.one * .5f, Vector2.one * .5f);
    }
}
