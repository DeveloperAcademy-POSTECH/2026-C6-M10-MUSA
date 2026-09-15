using System;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Resources.Tests
{
    public sealed class MultiplayerResourceTests
    {
        private static readonly ulong[] Players = { 0, 41, 7, 92, 8 };
        private static ResourceTuning Tuning => new ResourceTuning(100, 100, 20, 20d / 3d, 5, 20);
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EveryAdmittedPlayerStartsEmptyFullAndPaysIndependently(int count)
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("five-resource", 1);
            var resources = new HostResourceAuthority(registry, Tuning, 42, 5);
            resources.BeginRound(Players.Take(count), 0);
            Assert.That(registry.Snapshot(), Is.Empty);
            foreach (var player in Players.Take(count))
            {
                Assert.That(resources.GetPlayer(player).Stamina, Is.EqualTo(100));
                var generated = resources.Generate(player, Request(player, 1), 0);
                Assert.That(generated.Accepted, Is.True);
                Assert.That(generated.Orb.OwnerPlayerId, Is.EqualTo(player));
                Assert.That(resources.GetPlayer(player).Stamina, Is.EqualTo(80));
            }
            resources.Advance(3, true);
            Assert.That(resources.Snapshot().All(p => p.Stamina == 100), Is.True);
            Assert.That(resources.Generate(100, Request(100, 1), 3).Accepted, Is.False);
        }
        [Test]
        public void FifthParticipantAcceptedSixthAndDuplicateRejectedLegacyStillCapsTwo()
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("five-resource", 1);
            var resources = new HostResourceAuthority(registry, Tuning, 42, 5);
            resources.BeginRound(new ulong[] { 0 }, 0);
            foreach (var id in Players.Skip(1)) Assert.That(resources.AddParticipant(id, 0), Is.True);
            Assert.That(resources.AddParticipant(100, 0), Is.False);
            Assert.That(resources.AddParticipant(8, 0), Is.False);
            Assert.That(resources.Snapshot().Count, Is.EqualTo(5));
            var legacy = new HostResourceAuthority(registry, Tuning, 42);
            Assert.Throws<ArgumentException>(() => legacy.BeginRound(Players.Take(3), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HostResourceAuthority(registry, Tuning, 42, 6));
        }
        [Test]
        public void AuthoritativeHitRewardsOnlyCurrentAttackerAmongFiveAndCannotRepeatAcrossRetry()
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("five-resource", 1);
            var resources = new HostResourceAuthority(registry, Tuning, 42, 5); resources.BeginRound(Players, 0);
            foreach (var player in Players) resources.Generate(player, Request(player, 1), 0);
            var attack = new AttackAuthority(registry, 100, 20); attack.BeginDevelopmentRound();
            var combined = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            var moved = new OrbActionRequest("five-resource", 1, "transfer", combined.OrbId, null, OrbActionKind.TransferLeft, 1, Vector2.one * .5f);
            Assert.That(attack.RequestTransfer(0, moved, 8, true, 20, .055f).Accepted, Is.True);
            var launch = new OrbActionRequest("five-resource", 1, "launch", combined.OrbId, null, OrbActionKind.Launch, 2, Vector2.one * .5f);
            Assert.That(attack.RequestLaunch(8, launch).Accepted, Is.True);
            Assert.That(attack.MarkProjectileSpawned("five-resource", 1, combined.OrbId), Is.True);
            var hit = attack.ProcessHostHit("five-resource", 1, combined.OrbId);
            Assert.That(hit.Applied, Is.True);
            Assert.That(resources.ApplyValidHit(hit, 0).Accepted, Is.True);
            foreach (var player in Players) Assert.That(resources.GetPlayer(player).Stamina, Is.EqualTo(player == 8 ? 85 : 80));
            Assert.That(resources.ApplyValidHit(hit, 0).Accepted, Is.False);
            resources.EndRound(0); attack.EndDevelopmentRound(); registry.ResetRound(2); resources.BeginRound(Players, 0);
            Assert.That(resources.ApplyValidHit(hit, 0).Accepted, Is.False);
            Assert.That(resources.Snapshot().All(p => p.Stamina == 100), Is.True);
        }
        [Test]
        public void AFullHundredStoredOrbsIsBoundedPerOwnerAndOverflowGenerationIsNotCharged()
        {
            var registry = new HostOrbRegistry(true); registry.BeginSession("five-resource", 1);
            var resources = new HostResourceAuthority(registry, Tuning, 42, 5); resources.BeginRound(Players, 0);
            for (ulong sequence = 1; sequence <= 20; sequence++)
                foreach (var player in Players) Assert.That(resources.Generate(player, Request(player, sequence), (sequence - 1) * 3).Accepted, Is.True);
            Assert.That(registry.Snapshot().Count, Is.EqualTo(100));
            foreach (var player in Players)
            {
                double before = resources.GetPlayer(player).Stamina;
                Assert.That(resources.Generate(player, Request(player, 21), 57).Reason, Is.EqualTo("STORAGE_FULL"));
                Assert.That(resources.GetPlayer(player).Stamina, Is.EqualTo(before));
            }
        }
        private static GenerateRequest Request(ulong player, ulong sequence) => new GenerateRequest("five-resource", 1,
            player + "-generate-" + sequence, sequence);
    }
}
