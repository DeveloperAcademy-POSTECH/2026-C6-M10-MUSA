using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    /// <summary>#51: a Combined orb not thrown within the lifetime becomes Consumed on the Host.</summary>
    public sealed class CombinedOrbExpiryTests
    {
        private const double Lifetime = 8d;
        private HostOrbRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true);
            registry.BeginSession("session-a", 1);
        }

        [Test]
        public void IdleCombinedOrbExpiresOnlyAfterItsLifetime()
        {
            var orb = registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);

            Assert.That(registry.ExpireIdleCombined(100d, Lifetime), Is.Empty, "The first tick only starts the timer.");
            Assert.That(registry.ExpireIdleCombined(107.9d, Lifetime), Is.Empty);

            var expired = registry.ExpireIdleCombined(108d, Lifetime);
            Assert.That(expired.Count, Is.EqualTo(1));
            Assert.That(expired[0].OrbId, Is.EqualTo(orb.OrbId));
            Assert.That(registry.TryGet(orb.OrbId, out var after), Is.True);
            Assert.That(after.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(registry.CountStoredOrbs(7), Is.EqualTo(0));
            Assert.That(registry.ExpireIdleCombined(200d, Lifetime), Is.Empty, "A consumed orb never expires twice.");
        }

        [Test]
        public void RawOrbsNeverExpire()
        {
            var yin = registry.RegisterDevelopmentOrb(7, OrbKind.Raw, OrbPolarity.Yin, Vector2.one * .5f);
            registry.ExpireIdleCombined(0d, Lifetime);
            Assert.That(registry.ExpireIdleCombined(1000d, Lifetime), Is.Empty);
            Assert.That(registry.TryGet(yin.OrbId, out var after), Is.True);
            Assert.That(after.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
        }

        [Test]
        public void PendingLaunchWaitsForItsResult()
        {
            var orb = registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            registry.ExpireIdleCombined(0d, Lifetime);
            var launch = new OrbActionRequest("session-a", 1, "launch-1", orb.OrbId, null,
                OrbActionKind.Launch, 1, Vector2.one * .5f);
            Assert.That(registry.Reserve(7, launch).Accepted, Is.True);

            Assert.That(registry.ExpireIdleCombined(20d, Lifetime), Is.Empty, "A reserved throw is not removed mid-request.");
            Assert.That(registry.TryGet(orb.OrbId, out var after), Is.True);
            Assert.That(after.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
        }

        [Test]
        public void NewRoundForgetsOldTimers()
        {
            registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            registry.ExpireIdleCombined(0d, Lifetime);
            registry.ResetRound(2);
            var fresh = registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);

            Assert.That(registry.ExpireIdleCombined(50d, Lifetime), Is.Empty, "The new round's orb starts its own timer.");
            Assert.That(registry.ExpireIdleCombined(58d, Lifetime)[0].OrbId, Is.EqualTo(fresh.OrbId));
        }
    }
}
