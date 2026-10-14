using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabBattlePauseTests
    {
        [Test]
        public void SharedBattleClockFreezesRemainingStaminaAndOrbLifetime()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = config.CaptureDeveloperValues();
                values[39] = 50f;
                values[40] = 50f;
                for (int i = 41; i <= 44; i++) values[i] = 0f;
                values[63] = 2f; // both players hold a drum
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

                var clock = new LabBattleClock();
                var model = new LabModel();
                model.Begin("paused-round", new ulong[] { 1ul, 2ul }, clock.Now(100d), config);
                Assert.That(model.TryGenerate(1ul, "orb-1", clock.Now(100d), out var orb, out error),
                    Is.True, error);
                model.Tick(clock.Now(102d));
                LabSnapshot before = model.Snapshot(clock.Now(102d));
                Assert.That(before.remaining, Is.EqualTo(178d));
                Assert.That(before.orbs.Single().id, Is.EqualTo(orb.id));

                Assert.That(clock.Pause(102d), Is.True);
                model.Tick(clock.Now(10102d));
                LabSnapshot paused = model.Snapshot(clock.Now(10102d));
                Assert.That(paused.remaining, Is.EqualTo(before.remaining));
                Assert.That(paused.players[0].stamina, Is.EqualTo(before.players[0].stamina));
                Assert.That(paused.orbs.Single().id, Is.EqualTo(orb.id));

                Assert.That(clock.Resume(10102d), Is.True);
                model.Tick(clock.Now(10107d));
                Assert.That(model.Snapshot(clock.Now(10107d)).orbs.Single().id, Is.EqualTo(orb.id));
                model.Tick(clock.Now(10108d));
                Assert.That(model.Snapshot(clock.Now(10108d)).orbs, Is.Empty);
                Assert.That(model.Snapshot(clock.Now(10108d)).remaining, Is.EqualTo(172d));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void RepeatedPauseResumeAndResetNeverChargePausedTime()
        {
            var clock = new LabBattleClock();
            Assert.That(clock.Resume(1d), Is.False);
            Assert.That(clock.Pause(double.NaN), Is.False);
            Assert.That(clock.Pause(10d), Is.True);
            Assert.That(clock.Pause(11d), Is.False);
            Assert.That(clock.Now(500d), Is.EqualTo(10d));
            Assert.That(clock.Resume(9d), Is.False);
            Assert.That(clock.Resume(20d), Is.True);
            Assert.That(clock.Now(22d), Is.EqualTo(12d));
            Assert.That(clock.Pause(22d), Is.True);
            Assert.That(clock.Resume(32d), Is.True);
            Assert.That(clock.Now(35d), Is.EqualTo(15d));
            clock.Reset();
            Assert.That(clock.IsPaused, Is.False);
            Assert.That(clock.Now(35d), Is.EqualTo(35d));
        }

        [Test]
        public void PauseFlagSurvivesSnapshotSerializationWithoutChangingBattlePhase()
        {
            var snapshot = new LabSnapshot
            {
                sessionId = "session", round = 1u, phase = LabPhase.Playing,
                paused = true, remaining = 91.5d
            };
            LabSnapshot restored = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(snapshot));
            Assert.That(restored.paused, Is.True);
            Assert.That(restored.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(restored.remaining, Is.EqualTo(91.5d));
        }
    }
}
