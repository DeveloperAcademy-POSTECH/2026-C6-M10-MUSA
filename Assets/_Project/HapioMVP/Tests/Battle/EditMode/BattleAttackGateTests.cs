using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Battle.Tests
{
    public sealed class BattleAttackGateTests
    {
        [Test]
        public void ClosedGameplayGateCreatesKnownRejectionWithoutReservingAndDuplicateNeverBecomesLaunch()
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("session", 1);
            var orb = registry.RegisterDevelopmentOrb(41, OrbKind.Combined, OrbPolarity.None, new Vector2(.5f, .5f));
            var authority = new AttackAuthority(registry, 100, 20); authority.BeginDevelopmentRound();
            var request = new OrbActionRequest("session", 1, "before-host-start", orb.OrbId, null, OrbActionKind.Launch, 1, new Vector2(.5f, .9f));
            var denial = authority.RequestLaunch(41, request, false);
            Assert.That(denial.Accepted, Is.False); Assert.That(denial.Reason, Is.EqualTo("BATTLE_NOT_PLAYING"));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.TryGet(orb.OrbId, out var unchanged), Is.True);
            Assert.That(unchanged.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(unchanged.SequenceNumber, Is.EqualTo(0));
            var query = authority.QueryRequest(41, "session", 1, request.RequestId, orb.OrbId);
            Assert.That(query.Known, Is.True); Assert.That(query.Receipt.Accepted, Is.False);
            var duplicateAfterStart = authority.RequestLaunch(41, request, true);
            Assert.That(duplicateAfterStart.Accepted, Is.False); Assert.That(duplicateAfterStart.IsDuplicate, Is.True);
            Assert.That(duplicateAfterStart.SpawnRequired, Is.False);
            var fresh = new OrbActionRequest("session", 1, "after-host-start", orb.OrbId, null, OrbActionKind.Launch, 1, new Vector2(.5f, .9f));
            Assert.That(authority.RequestLaunch(41, fresh, true).SpawnRequired, Is.True);
        }
    }
}
