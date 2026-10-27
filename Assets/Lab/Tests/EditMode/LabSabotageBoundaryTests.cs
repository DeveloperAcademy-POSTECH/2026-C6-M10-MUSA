using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabSabotageBoundaryTests
    {
        [Test]
        public void FirstSabotageCanTargetTheHostsZeroPlayerId()
        {
            var config = MixConfig(5f, foxOnly: false);
            try
            {
                var room = new LabModel();
                // This seed draws seat zero when both players are eligible for the first cast.
                room.Begin("sabotage-first-host-5", new ulong[] { 0, 20 }, 0d, config);
                room.Tick(5d);

                LabSabotage first = room.DrainSabotages().Single();
                Assert.That(first.Target, Is.Zero,
                    "No previous sabotage exists yet, so Host ID zero must remain eligible.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [TestCase(5f, 15d)]
        [TestCase(20f, 40d)]
        public void LateTickFreesExpiredStorageBeforeDrawingTheNextFoxFire(float interval, double until)
        {
            var config = MixConfig(interval, foxOnly: true);
            try
            {
                var stepped = new LabModel();
                var late = new LabModel();
                stepped.BeginSolo("fox-capacity-clock", 42ul, 0d, config);
                late.BeginSolo("fox-capacity-clock", 42ul, 0d, config);

                stepped.Tick(interval);
                stepped.Tick(interval + config.OrbLifetimeSeconds);
                stepped.Tick(until);
                late.Tick(until);

                LabSabotage[] expected = stepped.DrainSabotages();
                LabSabotage[] actual = late.DrainSabotages();
                Assert.That(expected.Select(cast => cast.At), Is.EqualTo(new[] { (double)interval, until }));
                Assert.That(actual.Select(cast => (cast.Kind, cast.Target, cast.At)),
                    Is.EqualTo(expected.Select(cast => (cast.Kind, cast.Target, cast.At))),
                    "Expired orbs cannot fill a board at a later sabotage time, including within one late tick.");

                LabOrbState[] expectedOrbs = stepped.Snapshot(until).orbs;
                LabOrbState[] actualOrbs = late.Snapshot(until).orbs;
                Assert.That(expectedOrbs.Length, Is.EqualTo(3));
                Assert.That(actualOrbs.Select(orb => (orb.kind, orb.owner, orb.createdAt)),
                    Is.EqualTo(expectedOrbs.Select(orb => (orb.kind, orb.owner, orb.createdAt))),
                    "Both clock schedules leave the same fresh fox orbs on the board.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        private static LabConfig MixConfig(float interval, bool foxOnly)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[9] = 3f;
            values[10] = 8f;
            values[166] = 1f;
            // Gimmick groups (#311): bombs alone in the common group, or fox fire alone in the unique one.
            values[205] = foxOnly ? 0f : interval;
            values[206] = foxOnly ? interval : 0f;
            values[208] = 0f;
            values[171] = 100f; values[172] = 0f;
            values[173] = values[174] = 3f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return config;
        }
    }
}
