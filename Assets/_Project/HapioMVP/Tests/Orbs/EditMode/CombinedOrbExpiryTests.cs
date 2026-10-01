using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    /// <summary>
    /// #51/#71: every orb (Raw or Combined) not used within the lifetime after it was created becomes
    /// Consumed on the Host. Combining gives the new Combined orb a fresh lifetime.
    /// </summary>
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

            Assert.That(registry.ExpireIdleOrbs(100d, Lifetime), Is.Empty, "The first tick only starts the timer.");
            Assert.That(registry.ExpireIdleOrbs(107.9d, Lifetime), Is.Empty);

            var expired = registry.ExpireIdleOrbs(108d, Lifetime);
            Assert.That(expired.Count, Is.EqualTo(1));
            Assert.That(expired[0].OrbId, Is.EqualTo(orb.OrbId));
            Assert.That(registry.TryGet(orb.OrbId, out var after), Is.True);
            Assert.That(after.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(registry.CountStoredOrbs(7), Is.EqualTo(0));
            Assert.That(registry.ExpireIdleOrbs(200d, Lifetime), Is.Empty, "A consumed orb never expires twice.");
        }

        [Test]
        public void RawOrbsExpireAfterTheirLifetimeToo()
        {
            var yin = registry.RegisterDevelopmentOrb(7, OrbKind.Raw, OrbPolarity.Yin, Vector2.one * .5f);
            registry.ExpireIdleOrbs(0d, Lifetime);
            Assert.That(registry.ExpireIdleOrbs(7.9d, Lifetime), Is.Empty);

            var expired = registry.ExpireIdleOrbs(8d, Lifetime);
            Assert.That(expired.Count, Is.EqualTo(1));
            Assert.That(expired[0].OrbId, Is.EqualTo(yin.OrbId));
            Assert.That(registry.TryGet(yin.OrbId, out var after), Is.True);
            Assert.That(after.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
        }

        [Test]
        public void CombiningStartsAFreshLifetime()
        {
            var yin = registry.RegisterDevelopmentOrb(7, OrbKind.Raw, OrbPolarity.Yin, Vector2.one * .5f);
            var yang = registry.RegisterDevelopmentOrb(7, OrbKind.Raw, OrbPolarity.Yang, Vector2.one * .5f);
            registry.ExpireIdleOrbs(0d, Lifetime);   // both materials born at 0

            var combine = new OrbActionRequest("session-a", 1, "combine-1", yin.OrbId, yang.OrbId,
                OrbActionKind.Combine, 1, Vector2.one * .5f);
            var reservation = registry.Reserve(7, combine);
            Assert.That(reservation.Accepted, Is.True, reservation.Reason);
            Assert.That(registry.TryCompleteReservedCombination(reservation.Reservation, Vector2.one * .5f,
                out _, out _, out var combined), Is.True);

            Assert.That(registry.ExpireIdleOrbs(7d, Lifetime), Is.Empty, "Combined at 7: its own timer starts now.");
            Assert.That(registry.ExpireIdleOrbs(14.9d, Lifetime), Is.Empty, "The materials' old timer does not carry over.");
            var expired = registry.ExpireIdleOrbs(15d, Lifetime);
            Assert.That(expired.Count, Is.EqualTo(1));
            Assert.That(expired[0].OrbId, Is.EqualTo(combined.OrbId));
        }

        [Test]
        public void RemainingLifetimeFollowsTheSameTimer()
        {
            var yin = registry.RegisterDevelopmentOrb(7, OrbKind.Raw, OrbPolarity.Yin, Vector2.one * .5f);
            Assert.That(registry.TryGetRemainingLifetime(yin.OrbId, 0d, out _), Is.False, "No lifetime is known before the battle ticks.");

            registry.ExpireIdleOrbs(10d, Lifetime);
            Assert.That(registry.TryGetRemainingLifetime(yin.OrbId, 13d, out double remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(5d).Within(1e-9));
            Assert.That(registry.TryGetRemainingLifetime("missing", 13d, out _), Is.False);
        }

        [Test]
        public void PendingLaunchWaitsForItsResult()
        {
            var orb = registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            registry.ExpireIdleOrbs(0d, Lifetime);
            var launch = new OrbActionRequest("session-a", 1, "launch-1", orb.OrbId, null,
                OrbActionKind.Launch, 1, Vector2.one * .5f);
            Assert.That(registry.Reserve(7, launch).Accepted, Is.True);

            Assert.That(registry.ExpireIdleOrbs(20d, Lifetime), Is.Empty, "A reserved throw is not removed mid-request.");
            Assert.That(registry.TryGet(orb.OrbId, out var after), Is.True);
            Assert.That(after.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
        }

        [Test]
        public void NewRoundForgetsOldTimers()
        {
            registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            registry.ExpireIdleOrbs(0d, Lifetime);
            registry.ResetRound(2);
            var fresh = registry.RegisterDevelopmentOrb(7, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);

            Assert.That(registry.ExpireIdleOrbs(50d, Lifetime), Is.Empty, "The new round's orb starts its own timer.");
            Assert.That(registry.ExpireIdleOrbs(58d, Lifetime)[0].OrbId, Is.EqualTo(fresh.OrbId));
        }
    }
}
