using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>박 색상 콤보 실험 (#222): 판정 규칙과 Host 모델의 예약·페널티.</summary>
    public sealed class LabGourdComboTests
    {
        private static readonly LabOrbElement[] Sample =
            { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Water, LabOrbElement.Earth };

        private readonly List<LabConfig> created = new List<LabConfig>();

        [TearDown]
        public void TearDown()
        {
            foreach (LabConfig config in created) UnityEngine.Object.DestroyImmediate(config);
            created.Clear();
        }

        [Test]
        public void FourHitsInOrderAndColorSucceed()
        {
            var combo = new LabGourdCombo();
            combo.Start(Sample, 20d, 10d);
            Assert.That(combo.Active, Is.True);
            Assert.That(combo.Count, Is.EqualTo(1u));
            Assert.That(combo.EndsAt, Is.EqualTo(30d));
            Assert.That(combo.Judge(LabOrbElement.Fire, 1, 21d), Is.EqualTo(LabComboResult.None));
            Assert.That(combo.Judge(LabOrbElement.None, 1, 21d), Is.EqualTo(LabComboResult.None),
                "Bombs and relays carry no color and are ignored.");
            Assert.That(combo.Judge(LabOrbElement.Water, 2, 22d), Is.EqualTo(LabComboResult.None));
            Assert.That(combo.Judge(LabOrbElement.Water, 3, 23d), Is.EqualTo(LabComboResult.None));
            Assert.That(combo.Progress, Is.EqualTo(3));
            Assert.That(combo.Judge(LabOrbElement.Earth, 1, 24d), Is.EqualTo(LabComboResult.Success));
            Assert.That(combo.Active, Is.False);
            Assert.That(combo.LastResult, Is.EqualTo(LabComboResult.Success));
            Assert.That(combo.LastResultCount, Is.EqualTo(1u));
            Assert.That(combo.LastResultAt, Is.EqualTo(24d));
            Assert.That(combo.LastFailedBy, Is.EqualTo(0ul));
            Assert.That(combo.Judge(LabOrbElement.Fire, 1, 25d), Is.EqualTo(LabComboResult.None),
                "Hits after the combo ends do nothing.");
        }

        [Test]
        public void AWrongColorFailsAtOnceAndRemembersTheThrower()
        {
            var combo = new LabGourdCombo();
            combo.Start(Sample, 0d, 10d);
            Assert.That(combo.Judge(LabOrbElement.Fire, 1, 1d), Is.EqualTo(LabComboResult.None));
            Assert.That(combo.Judge(LabOrbElement.Fire, 7, 2d), Is.EqualTo(LabComboResult.WrongColor),
                "The second slot is Water, so a second Fire is the wrong color even though Fire is in the combo.");
            Assert.That(combo.Active, Is.False);
            Assert.That(combo.LastFailedBy, Is.EqualTo(7ul));
            Assert.That(combo.LastResultAt, Is.EqualTo(2d));
        }

        [Test]
        public void TheTimeLimitEndsTheComboAtItsDeadline()
        {
            var combo = new LabGourdCombo();
            combo.Start(Sample, 5d, 10d);
            Assert.That(combo.TryExpire(14.9d), Is.False);
            Assert.That(combo.TryExpire(18d), Is.True);
            Assert.That(combo.LastResult, Is.EqualTo(LabComboResult.Timeout));
            Assert.That(combo.LastResultAt, Is.EqualTo(15d), "It ends at the deadline, not when it was noticed.");
            Assert.That(combo.LastFailedBy, Is.EqualTo(0ul));

            combo.Start(Sample, 20d, 10d);
            Assert.That(combo.Judge(LabOrbElement.Fire, 1, 31d), Is.EqualTo(LabComboResult.Timeout),
                "A hit after the deadline cannot count.");
            Assert.That(combo.Count, Is.EqualTo(2u));
        }

        [Test]
        public void StartRejectsNonElementColors()
        {
            var combo = new LabGourdCombo();
            Assert.Throws<ArgumentException>(() => combo.Start(new LabOrbElement[3], 0d, 10d));
            Assert.Throws<ArgumentOutOfRangeException>(() => combo.Start(
                new[] { LabOrbElement.Fire, LabOrbElement.None, LabOrbElement.Fire, LabOrbElement.Fire }, 0d, 10d));
            Assert.Throws<ArgumentOutOfRangeException>(() => combo.Start(Sample, 0d, 0d));
            Assert.That(combo.Active, Is.False);
        }

        [Test]
        public void WhenTheToggleIsOffNoComboEverAppears()
        {
            LabConfig config = Config(enabled: false);
            var model = new LabModel();
            model.Begin("combo-off", new ulong[] { 10, 20, 30 }, 0d, config);
            LabSnapshot snapshot = model.Snapshot(170d);
            Assert.That(snapshot.comboCount, Is.EqualTo(0u));
            Assert.That(snapshot.comboActive, Is.False);
            Assert.That(snapshot.comboColors, Is.Empty);
            Assert.That(snapshot.comboResult, Is.EqualTo(LabComboResult.None));
            Assert.That(snapshot.players.All(player => player.comboPenaltyUntil <= 0d), Is.True);
            Assert.That(model.ThrowScaleFor(10, 170d), Is.EqualTo(1f));
        }

        [Test]
        public void ATimedOutComboPenalizesTheWholeTeamAndTheNextComboFollowsTheInterval()
        {
            LabConfig config = Config(enabled: true);
            var model = new LabModel();
            model.Begin("combo-timeout", new ulong[] { 10, 20, 30 }, 0d, config);

            Assert.That(model.Snapshot(19.9d).comboCount, Is.EqualTo(0u), "Nothing before 20 seconds.");
            LabSnapshot first = model.Snapshot(20d);
            Assert.That(first.comboActive, Is.True);
            Assert.That(first.comboCount, Is.EqualTo(1u));
            Assert.That(first.comboColors.Length, Is.EqualTo(4));
            Assert.That(first.comboColors.All(color => color >= LabOrbElement.Fire && color <= LabOrbElement.Earth),
                Is.True, "Colors come from the five elements (Tue-Sat).");
            Assert.That(first.comboEndsAt, Is.EqualTo(30d));
            Assert.That(model.ThrowScaleFor(10, 25d), Is.EqualTo(1f));

            LabSnapshot failed = model.Snapshot(31d);
            Assert.That(failed.comboActive, Is.False);
            Assert.That(failed.comboResult, Is.EqualTo(LabComboResult.Timeout));
            Assert.That(failed.comboResultCount, Is.EqualTo(1u));
            Assert.That(failed.comboResultAt, Is.EqualTo(30d));
            Assert.That(failed.players.Select(player => player.comboPenaltyUntil), Is.All.EqualTo(40d),
                "A timeout has no culprit, so the whole team shrinks for 10 seconds.");
            foreach (ulong id in new ulong[] { 10, 20, 30 })
                Assert.That(model.ThrowScaleFor(id, 35d), Is.EqualTo(.5f));
            Assert.That(model.ThrowScaleFor(10, 40d), Is.EqualTo(1f), "The penalty ends on time.");

            LabSnapshot received = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(failed));
            Assert.That(received.comboColors, Is.EqualTo(first.comboColors), "Clients receive the same combo.");
            Assert.That(received.comboResult, Is.EqualTo(LabComboResult.Timeout));
            Assert.That(received.comboResultCount, Is.EqualTo(1u));
            Assert.That(received.players.Select(player => player.comboPenaltyUntil), Is.All.EqualTo(40d));

            Assert.That(model.Snapshot(49.9d).comboCount, Is.EqualTo(1u));
            LabSnapshot second = model.Snapshot(50d);
            Assert.That(second.comboActive, Is.True, "The next combo comes 30 seconds after the first appeared.");
            Assert.That(second.comboCount, Is.EqualTo(2u));
        }

        [Test]
        public void EveryDeviceSeesTheSameColorsForTheSameRound()
        {
            LabConfig config = Config(enabled: true);
            var a = new LabModel();
            var b = new LabModel();
            a.Begin("combo-same", new ulong[] { 10, 20 }, 0d, config);
            b.Begin("combo-same", new ulong[] { 10, 20 }, 0d, config);
            Assert.That(a.Snapshot(20d).comboColors, Is.EqualTo(b.Snapshot(20d).comboColors));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AWrongColorPenalizesTheTeamOrOnlyTheThrowerAsConfigured(bool throwerOnly)
        {
            LabConfig config = Config(enabled: true, firstStart: 1f, timeLimit: 100f, throwerOnly: throwerOnly);
            var model = new LabModel();
            model.Begin("combo-wrong-" + throwerOnly, new ulong[] { 10, 20, 30 }, 0d, config);
            Assert.That(model.Snapshot(1d).comboActive, Is.True);

            var pool = new List<LabOrbState>();
            int requests = 0;
            LabSnapshot snapshot = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                snapshot = model.Snapshot(2d);
                if (!snapshot.comboActive) break;
                LabOrbState combined = Combine(model, 10, pool, ref requests);
                Assert.That(model.TryLaunch(10, combined.id, "combo-launch-" + attempt, 2d, out _, out _), Is.True);
                Assert.That(model.ConfirmHit(combined.id, 2d), Is.True);
            }
            snapshot = model.Snapshot(2d);
            Assume.That(snapshot.comboResult, Is.EqualTo(LabComboResult.WrongColor),
                "Four lucky colors in a row would succeed instead; that path is covered elsewhere.");
            Assert.That(snapshot.comboFailedBy, Is.EqualTo(10ul));
            Assert.That(snapshot.players.Single(player => player.id == 10).comboPenaltyUntil, Is.EqualTo(12d));
            float otherScale = throwerOnly ? 1f : .5f;
            Assert.That(model.ThrowScaleFor(20, 5d), Is.EqualTo(otherScale));
            Assert.That(model.ThrowScaleFor(30, 5d), Is.EqualTo(otherScale));
            Assert.That(model.ThrowScaleFor(10, 5d), Is.EqualTo(.5f));
        }

        private static LabOrbState Combine(LabModel model, ulong owner, List<LabOrbState> pool, ref int requests)
        {
            for (int i = 0; i < 20; i++)
            {
                LabOrbState yin = pool.FirstOrDefault(orb => orb.kind == LabOrbKind.Yin);
                LabOrbState yang = pool.FirstOrDefault(orb => orb.kind == LabOrbKind.Yang);
                if (yin != null && yang != null)
                {
                    pool.Remove(yin);
                    pool.Remove(yang);
                    Assert.That(model.TryCombine(owner, yin.id, yang.id, "combo-combine-" + requests++, 2d,
                        out var combined, out string reason), Is.True, reason);
                    return combined;
                }
                Assert.That(model.TryGenerate(owner, "combo-generate-" + requests++, 2d, out var raw, out string why),
                    Is.True, why);
                pool.Add(raw);
            }
            Assert.Fail("Could not make a Yin/Yang pair.");
            return null;
        }

        private LabConfig Config(bool enabled, float firstStart = 20f, float timeLimit = 10f, bool throwerOnly = false)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            created.Add(config);
            float[] values = config.CaptureDeveloperValues();
            values[5] = 1f;    // cheap generation
            values[9] = 100f;  // room for leftover raw orbs
            values[74] = LabModel.MaximumParticipants;
            values[88] = 0f;   // no gourd bombs
            values[89] = 0f;
            values[39] = 50; values[40] = 50; values[41] = 0; values[42] = 0; values[43] = 0; values[44] = 0;
            values[150] = enabled ? 1f : 0f;
            values[151] = firstStart;
            values[153] = timeLimit;
            values[156] = throwerOnly ? 1f : 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return config;
        }
    }
}
