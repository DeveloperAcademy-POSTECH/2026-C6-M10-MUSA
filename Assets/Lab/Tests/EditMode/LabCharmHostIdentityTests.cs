using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabCharmHostIdentityTests
    {
        [TestCase(0ul)]
        [TestCase(20ul)]
        public void WakeResultDistinguishesAnIncompleteRescueFromEveryPlayerId(ulong charmedPlayer)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = config.CaptureDeveloperValues();
                values[166] = 1f;
                values[205] = 0f; values[206] = 5f; // a charm every 5 s and nothing else (#311)
                values[171] = 0f; values[172] = 100f;
                values[177] = 3f;
                values[204] = 0f; // no warning (#305): the charm starts as it is cast
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

                var room = new LabModel();
                room.Begin("charm-host-identity", new ulong[] { 0, 20 }, 0d, config);
                room.Tick(5d);
                double at = 5d;
                if (!room.Snapshot(at).CharmOn(charmedPlayer))
                {
                    // With only charms, the next eligible cast goes to the other player.
                    at = 15d;
                    room.Tick(at);
                }
                Assert.That(room.Snapshot(at).CharmOn(charmedPlayer), Is.True);
                ulong neighbor = charmedPlayer == 0 ? 20ul : 0ul;

                Assert.That(room.TryCharmTap(charmedPlayer, at + .1d, out var invalidWake), Is.False);
                Assert.That((object)invalidWake, Is.Null, "A rejected tap does not wake anyone.");
                Assert.That(room.TryCharmTap(neighbor, at + .2d, out var firstWake), Is.True);
                Assert.That((object)firstWake, Is.Null, "An accepted tap below the threshold does not name a player.");
                Assert.That(room.TryCharmTap(neighbor, at + .3d, out var secondWake), Is.True);
                Assert.That((object)secondWake, Is.Null);
                Assert.That(room.Snapshot(at + .3d).CharmOn(charmedPlayer), Is.True);
                Assert.That(room.TryCharmTap(neighbor, at + .4d, out var completedWake), Is.True);
                Assert.That((object)completedWake, Is.EqualTo(charmedPlayer),
                    "Completing a rescue reports the actual player ID, including the Host's zero.");
                Assert.That(room.Snapshot(at + .4d).CharmOn(charmedPlayer), Is.False);
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}
