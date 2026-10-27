using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabImpactAuthorityTests
    {
        [TestCase(2f, false)]
        [TestCase(100f, true)]
        public void ConfirmedPaintHitReportsActualHpAndNominalImpactOnlyOnce(float initialPaintPercent, bool completesPaint)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = config.CaptureDeveloperValues();
                values[0] = 7f; // Monster HP is lower than the configured 20-point hit.
                values[1] = 20f;
                values[79] = 0f; // Reserved paint-only slot; HP alone never decides victory.
                values[104] = 0f;
                values[105] = initialPaintPercent;
                values[5] = .001f;
                values[9] = 100f;
                values[39] = 50f;
                values[40] = 50f;
                for (int i = 41; i <= 44; i++) values[i] = 0f;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

                var model = new LabModel();
                model.BeginSolo("impact-session", 1ul, 0d, config);
                LabOrbState yin = null, yang = null;
                for (int i = 0; i < 32 && (yin == null || yang == null); i++)
                {
                    Assert.That(model.TryGenerate(1ul, "draw-" + i, 0d, out var raw, out error),
                        Is.True, error);
                    if (raw.kind == LabOrbKind.Yin) yin = raw;
                    else if (raw.kind == LabOrbKind.Yang) yang = raw;
                }
                Assert.That(yin, Is.Not.Null);
                Assert.That(yang, Is.Not.Null);
                Assert.That(model.TryCombineAt(1ul, yin.id, yang.id, "combine", 0d,
                    new Vector2((yin.x + yang.x) * .5f, (yin.y + yang.y) * .5f), Vector2.zero,
                    out var combined, out error), Is.True, error);
                Assert.That(model.TryLaunch(1ul, combined.id, "launch", 0d,
                    out _, out error), Is.True, error);

                Assert.That(model.ConfirmHit(combined.id, 0d, Vector3.forward,
                    out var element, out int damage, out int impact), Is.True);
                Assert.That(element, Is.EqualTo(combined.element));
                Assert.That(damage, Is.EqualTo(7));
                Assert.That(impact, Is.EqualTo(20), "Feedback and attack score retain the nominal hit value.");
                LabSnapshot accepted = model.Snapshot(0d);
                Assert.That(accepted.hp, Is.Zero);
                Assert.That(accepted.phase, Is.EqualTo(completesPaint ? LabPhase.Victory : LabPhase.Playing));
                Assert.That(accepted.paintCoveredCount == accepted.paintCellCount, Is.EqualTo(completesPaint));
                Assert.That(accepted.paintCoveredCount, Is.GreaterThan(0));
                Assert.That(accepted.players[0].hitTotal, Is.EqualTo(1));
                Assert.That(accepted.players[0].damageTotal, Is.EqualTo(20));
                Assert.That(accepted.players[0].lastHit, Is.EqualTo(completesPaint));
                float stamina = accepted.players[0].stamina;
                Assert.That(model.ConfirmHit(combined.id, 0d, Vector3.forward, out element, out damage, out impact), Is.False);
                Assert.That(element, Is.EqualTo(LabOrbElement.None));
                Assert.That(damage, Is.Zero);
                Assert.That(impact, Is.Zero);
                LabSnapshot duplicate = model.Snapshot(0d);
                Assert.That(duplicate.players[0].hitTotal, Is.EqualTo(1));
                Assert.That(duplicate.players[0].damageTotal, Is.EqualTo(20));
                Assert.That(duplicate.players[0].stamina, Is.EqualTo(stamina));
                Assert.That(duplicate.paintCount, Is.EqualTo(accepted.paintCount));
                Assert.That(duplicate.paintRevision, Is.EqualTo(accepted.paintRevision));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ClientImpactGateRejectsStaleDuplicateAndMalformedEvents()
        {
            var gate = new LabImpactReceiptGate();
            Vector3 contact = new Vector3(.2f, 1.1f, -.3f);
            Vector3 velocity = new Vector3(2f, 4f, 6f);
            gate.SetActive("session-a", 2u);

            Assert.That(gate.TryAccept("session-a", 1u, "shot-1", LabOrbElement.Fire, 20,
                contact, velocity), Is.False);
            Assert.That(gate.TryAccept("session-b", 2u, "shot-1", LabOrbElement.Fire, 20,
                contact, velocity), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "", LabOrbElement.Fire, 20,
                contact, velocity), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", (LabOrbElement)(-1), 20,
                contact, velocity), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", LabOrbElement.Fire, 0,
                contact, velocity), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", LabOrbElement.Fire, 20,
                new Vector3(float.NaN, 0, 0), velocity), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", LabOrbElement.Fire, 20,
                contact, new Vector3(float.PositiveInfinity, 0, 0)), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", LabOrbElement.Fire, 20,
                contact, velocity, float.NaN), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", LabOrbElement.Fire, 20,
                contact, velocity, 1440.1f), Is.False);

            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", LabOrbElement.Fire, 20,
                contact, velocity, -180f), Is.True);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-1", LabOrbElement.Fire, 20,
                contact, velocity), Is.False);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-item", LabOrbElement.None, 60,
                contact, velocity, 125f), Is.True,
                "A confirmed relay/bomb hit must animate and spin on clients too.");
            gate.SetActive("session-a", 3u);
            Assert.That(gate.TryAccept("session-a", 2u, "shot-2", LabOrbElement.Fire, 20,
                contact, velocity), Is.False);
            Assert.That(gate.TryAccept("session-a", 3u, "shot-1", LabOrbElement.Fire, 20,
                contact, velocity), Is.True);
            gate.Reset();
            Assert.That(gate.TryAccept("session-a", 3u, "shot-2", LabOrbElement.Fire, 20,
                contact, velocity), Is.False);
        }
    }
}
