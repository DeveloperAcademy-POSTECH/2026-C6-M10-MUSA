using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>A thrower's own contact is presented at most once and handed back to the Host's decision (#459).</summary>
    public sealed class LabLocalHitPresentationTests
    {
        private static LabLocalHitPresentation.Presented Hit(double at = 0d) => new LabLocalHitPresentation.Presented(
            LabLocalHitPresentation.Outcome.Hit, new LabHangingTarget.AppliedImpact(Vector2.one, .5f, 42f, 0d), at);

        [Test]
        public void ARegisteredLaunchIsPresentedOnceAndFoundByItsApprovedProjectileId()
        {
            var ledger = new LabLocalHitPresentation();
            Assert.That(ledger.TryPresent(5, Hit()), Is.False, "Unregistered launches are never presented.");
            ledger.Register(5, "pending/1/9/5");
            ledger.Bind(5, "orb-approved");
            Assert.That(ledger.TryPresent(5, Hit()), Is.True);
            Assert.That(ledger.TryPresent(5, Hit()), Is.False, "Once per launch.");
            Assert.That(ledger.PendingHits, Is.EqualTo(1));
            Assert.That(ledger.TryTake("pending/1/9/5", out _), Is.False, "The Host names the approved projectile.");
            Assert.That(ledger.TryTake("orb-approved", out var taken), Is.True);
            Assert.That(taken.Applied.Yaw, Is.EqualTo(42f));
            Assert.That(ledger.TryTake("orb-approved", out _), Is.False, "Handed back once.");
            Assert.That(ledger.PendingHits, Is.Zero);
        }

        [Test]
        public void AHostDecisionBeforeTheLocalContactBlocksALatePresentation()
        {
            var ledger = new LabLocalHitPresentation();
            ledger.Register(6, "orb-6");
            Assert.That(ledger.TryTake("orb-6", out _), Is.False, "Nothing was presented yet.");
            Assert.That(ledger.CanPresent(6), Is.False);
            Assert.That(ledger.TryPresent(6, Hit()), Is.False, "The Host's own feedback already played.");
        }

        [Test]
        public void ARelaunchOfTheSameOrbAfterARollbackIsItsOwnEntry()
        {
            var ledger = new LabLocalHitPresentation();
            ledger.Register(7, "orb-7");
            ledger.TryPresent(7, Hit());
            Assert.That(ledger.TryTakeRequest(7, out var rolledBack), Is.True, "The rollback takes the first presentation back.");
            Assert.That(rolledBack.Outcome, Is.EqualTo(LabLocalHitPresentation.Outcome.Hit));
            ledger.Register(8, "orb-7");
            Assert.That(ledger.TryPresent(8, Hit()), Is.True);
            Assert.That(ledger.TryTake("orb-7", out _), Is.True, "The Host's verdict finds the relaunch, not the old request.");
            ledger.Register(9, "orb-9");
            ledger.TryPresent(9, Hit());
            ledger.Clear();
            Assert.That(ledger.Count, Is.Zero);
            Assert.That(ledger.TryTake("orb-9", out _), Is.False);
        }

        [Test]
        public void AHitTheHostNeverAnsweredExpiresButAMissDoesNot()
        {
            var ledger = new LabLocalHitPresentation();
            ledger.Register(1, "orb-1"); ledger.TryPresent(1, Hit(10d));
            ledger.Register(2, "orb-2"); ledger.TryPresent(2, Hit(11d));
            ledger.Register(3, "orb-3");
            ledger.TryPresent(3, new LabLocalHitPresentation.Presented(LabLocalHitPresentation.Outcome.Miss, default, 10d));
            var expired = new List<LabLocalHitPresentation.Presented>();
            ledger.TakeExpiredHits(11.4d, 1.5d, expired);
            Assert.That(expired, Is.Empty);
            ledger.TakeExpiredHits(11.6d, 1.5d, expired);
            Assert.That(expired, Has.Count.EqualTo(1), "The Host sends nothing for a voided contact.");
            Assert.That(ledger.TryTake("orb-1", out _), Is.False, "Already taken back.");
            Assert.That(ledger.TryTakeExpired("orb-1"), Is.True, "A late verdict knows the hit was presented.");
            Assert.That(ledger.TryTakeExpired("orb-1"), Is.False);
            Assert.That(ledger.PendingHits, Is.EqualTo(1));
            Assert.That(ledger.TryTake("orb-3", out var miss), Is.True);
            Assert.That(miss.Outcome, Is.EqualTo(LabLocalHitPresentation.Outcome.Miss));
        }

        [Test]
        public void AnExpiredProvisionalLaunchIsFoundByTheHostIdItsNoticeNamesLater()
        {
            var ledger = new LabLocalHitPresentation();
            ledger.Register(4, "pending/1/9/3");
            ledger.TryPresent(4, Hit(0d));
            var expired = new List<LabLocalHitPresentation.Presented>();
            ledger.TakeExpiredHits(5d, 1.5d, expired);
            Assert.That(expired, Has.Count.EqualTo(1));
            ledger.Bind(4, "orb-host");
            Assert.That(ledger.TryTakeExpired("pending/1/9/3"), Is.False);
            Assert.That(ledger.TryTakeExpired("orb-host"), Is.True, "The late verdict carries the Host's ID.");
        }

        [Test]
        public void ARollbackOrRelaunchEndsTheExpiredRecordSoALaterRealHitIsHeard()
        {
            var ledger = new LabLocalHitPresentation();
            var expired = new List<LabLocalHitPresentation.Presented>();
            ledger.Register(1, "orb-7"); ledger.TryPresent(1, Hit(0d));
            ledger.TakeExpiredHits(5d, 1.5d, expired);
            ledger.TryTakeRequest(1, out _); // The launch was rolled back.
            Assert.That(ledger.TryTakeExpired("orb-7"), Is.False);

            ledger.Register(2, "orb-8"); ledger.TryPresent(2, Hit(0d));
            ledger.TakeExpiredHits(5d, 1.5d, expired);
            ledger.Register(3, "orb-8"); // Thrown again.
            Assert.That(ledger.TryTakeExpired("orb-8"), Is.False, "The relaunch is a new shot with its own feedback.");
        }

        [TestCase(10f, 1.4f)]   // Shipped swing: underdamped.
        [TestCase(45f, 3f)]     // Shipped rope: underdamped.
        [TestCase(25f, 10f)]    // Critical.
        [TestCase(10f, 10f)]    // Overdamped DEV setting.
        [TestCase(0f, 2f)]      // No restoring force.
        public void TheSpringResponseMatchesTheSimulation(float stiffness, float damping)
        {
            // Semi-implicit Euler as in FixedUpdate, at a fine step so only the formula is under test.
            const float step = .0005f;
            float x = 0f, v = 1f;
            for (int i = 1; i <= 2000; i++)
            {
                v += (-x * stiffness - v * damping) * step;
                x += v * step;
                if (i % 500 != 0) continue;
                LabHangingTarget.SpringResponse(stiffness, damping, i * step, out float position, out float rate);
                Assert.That(position, Is.EqualTo(x).Within(.01f), "Position at " + i * step + " s");
                Assert.That(rate, Is.EqualTo(v).Within(.02f), "Rate at " + i * step + " s");
            }
        }

        [TestCase(LabOrbKind.Combined, 0, 20)]
        [TestCase(LabOrbKind.Yin, 0, 20)]
        [TestCase(LabOrbKind.Relay, 1, 20)]
        [TestCase(LabOrbKind.Relay, 2, 30)]
        [TestCase(LabOrbKind.Relay, 3, 50)]
        [TestCase(LabOrbKind.Relay, 9, 50)]
        public void TheAttackValueMatchesTheHostRule(LabOrbKind kind, int stage, int expected)
        {
            Assert.That(LabModel.AttackValueFor(kind, stage, 20, 1.5f, 2.5f), Is.EqualTo(expected));
        }

        [Test]
        public void TheAttackValueIsNeverBelowOneForARelay()
        {
            Assert.That(LabModel.AttackValueFor(LabOrbKind.Relay, 2, 0, 1.5f, 2.5f), Is.EqualTo(1));
        }
    }
}
