using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabJigwiStageBlastTests
    {
        private LabConfig config;
        private LabModel room;
        private Dictionary<string, LabOrbState> orbs;
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            var values = config.CaptureDeveloperValues(); values[207] = values[208] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out var error), Is.True, error);
            room = new LabModel(); room.Begin("jigwi-blast-lineage", new ulong[] { 10, 20 }, 0d, config, LabStageYokai.Jigwi);
            // Reach the late bomb/cutoff window before adding its fixture orbs: earlier A/B
            // effects must not burn or erase objects that did not exist at their activation.
            room.Tick(90d);
            orbs = (Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(room);
            CompleteFinalScreenMission();
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(config); }
        private void CompleteFinalScreenMission()
        {
            // These tests isolate bomb-origin cleanup at 100, rather than the independent
            // B failure wipe at 95. Finish B through real approved Water/fire transactions.
            var group = room.Snapshot(90d).jigwiAB.Active(90d);
            Assert.That(group.kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            int index = 0;
            foreach (var player in group.players)
                foreach (var target in player.targets)
                {
                    string id = "cutoff-water-" + ++index;
                    orbs.Add(id, new LabOrbState { id = id, owner = player.playerId,
                        kind = LabOrbKind.Combined, element = LabOrbElement.Water,
                        createdAt = 90d, expiresAt = 119d, x = .5f, y = .5f });
                    Assert.That(room.TryLaunch(player.playerId, id, "launch-" + id, 90d,
                        out _, out var reason), Is.True, reason);
                    Assert.That(room.ConfirmJigwiABFireHit(id, group.id, target.id, 90d,
                        out bool extinguished), Is.True);
                    Assert.That(extinguished, Is.True);
                }
            var completed = room.Snapshot(90d).jigwiAB.rounds.Single(value => value.id == group.id);
            Assert.That(completed.resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(completed.clearedThroughRevision, Is.Zero);
        }
        private void AddOrb(string id, LabOrbKind kind = LabOrbKind.Yin)
        {
            orbs.Add(id, new LabOrbState { id = id, owner = 10, kind = kind,
                element = kind == LabOrbKind.Combined ? LabOrbElement.Fire : LabOrbElement.None,
                createdAt = 90d, expiresAt = 119d, x = .5f, y = .5f });
        }
        private void Explode(string id, LabOrbOrigin origin, double at)
        {
            orbs.Add(id, new LabOrbState { id = id, owner = 10, kind = LabOrbKind.Bomb,
                origin = origin, lockSwipes = 2, createdAt = at - 15d, expiresAt = at, x = .5f, y = .5f });
            room.Tick(at);
            Assert.That(room.DrainBombExplosions().Select(b => b.BombId), Does.Contain(id));
        }
        private LabOrbState Orb(string id, double now) => room.Snapshot(now).orbs.Single(o => o.id == id);

        [Test] public void CutoffClearsOnlyScheduledBlastFireAndShrink()
        {
            AddOrb("both"); AddOrb("common-only");
            Explode("ordinary", LabOrbOrigin.Generated, 98d);
            Assert.That(room.MarkBlasted(10, "ordinary", new[] { "both" }, 98.01d), Is.EqualTo(1));
            Explode("common", LabOrbOrigin.Gourd, 99d);
            Assert.That(room.MarkBlasted(10, "common", new[] { "both", "common-only" }, 99.01d), Is.EqualTo(2));
            Assert.That(Orb("common-only", 99.5d).small, Is.True);
            var after = room.Snapshot(100d);
            var ordinary = after.orbs.Single(o => o.id == "both");
            var common = after.orbs.Single(o => o.id == "common-only");
            Assert.That(common.small, Is.False); Assert.That(common.burnedAt, Is.EqualTo(-1d));
            Assert.That(ordinary.small, Is.True); Assert.That(ordinary.burnedAt, Is.EqualTo(98.01d));
            Assert.That(after.orbs.All(o => !o.gourdBlastOnlySmall && o.gourdBlastBurnedAt == -1d), Is.True);
        }
        [Test] public void CutoffDoesNotChangeTheRadiusOrFireOfAnAlreadyLaunchedOrb()
        {
            AddOrb("shot", LabOrbKind.Combined);
            Explode("common", LabOrbOrigin.Gourd, 99d);
            Assert.That(room.MarkBlasted(10, "common", new[] { "shot" }, 99.01d), Is.EqualTo(1));
            Assert.That(room.TryLaunch(10, "shot", "launch", 99.02d, out _, out var reason), Is.True, reason);
            var shot = Orb("shot", 100d);
            Assert.That(shot.inFlight, Is.True); Assert.That(shot.small, Is.True);
            Assert.That(shot.burnedAt, Is.EqualTo(99.01d));
        }
        [Test] public void ADelayedCommonBlastReportCannotReigniteAfterCutoff()
        {
            AddOrb("raw"); Explode("common", LabOrbOrigin.Gourd, 99d);
            Assert.That(room.MarkBlasted(10, "common", new[] { "raw" }, 100d), Is.Zero);
            Assert.That(Orb("raw", 100d).small, Is.False);
        }
        [Test] public void CombinationRetainsTheSourceNeededToClearOnlyCommonShrink()
        {
            AddOrb("yin"); AddOrb("yang", LabOrbKind.Yang);
            Explode("common", LabOrbOrigin.Gourd, 99d);
            Assert.That(room.MarkBlasted(10, "common", new[] { "yin" }, 99.01d), Is.EqualTo(1));
            Assert.That(room.TryCombineAt(10, "yin", "yang", "combine", 99.02d,
                new Vector2(.5f, .5f), Vector2.zero, out var combined, out var reason), Is.True, reason);
            Assert.That(combined.small, Is.True); Assert.That(combined.gourdBlastOnlySmall, Is.True);
            Assert.That(Orb(combined.id, 100d).small, Is.False);
        }
    }
}
