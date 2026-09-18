using NUnit.Framework;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>Presentation rules only. HP and damage remain Host-authoritative.</summary>
    public sealed class DamagePopupTests
    {
        [Test]
        public void OnlyAnHpDropFromAKnownValueShowsDamage()
        {
            Assert.That(T09BattleController.ObservedDamage(null, 80), Is.Zero, "first snapshot");
            Assert.That(T09BattleController.ObservedDamage(100, 80), Is.EqualTo(20));
            Assert.That(T09BattleController.ObservedDamage(20, 0), Is.EqualTo(20));
            Assert.That(T09BattleController.ObservedDamage(80, 80), Is.Zero);
            Assert.That(T09BattleController.ObservedDamage(0, 100), Is.Zero, "a new round restores HP");
        }

        [Test]
        public void OnlyOwnOrbsVanishingWithoutAnHpDropInPlayShowMiss()
        {
            Assert.That(T09BattleController.ObservedMisses(300, 300, true, 1), Is.EqualTo(1));
            Assert.That(T09BattleController.ObservedMisses(300, 280, true, 1), Is.Zero, "HP dropped: cannot be a miss");
            Assert.That(T09BattleController.ObservedMisses(300, 300, false, 1), Is.Zero, "round ended or target cleared");
            Assert.That(T09BattleController.ObservedMisses(null, 300, true, 1), Is.Zero, "first snapshot or new round");
            Assert.That(T09BattleController.ObservedMisses(300, 300, true, 0), Is.Zero, "someone else's orb");
        }

        [Test]
        public void MissFiresOnlyAfterTheFlightClearlyMovesAwayFromItsClosestPass()
        {
            Assert.That(T09BattleController.PassedTarget(.8f, 1.2f, .35f), Is.True);
            Assert.That(T09BattleController.PassedTarget(.8f, 1f, .35f), Is.False, "still near the closest pass");
            Assert.That(T09BattleController.PassedTarget(0f, 2f, .35f), Is.False, "touched the hitbox: the Host decides the hit");
            Assert.That(T09BattleController.PassedTarget(float.PositiveInfinity, 2f, .35f), Is.False, "no sample yet");
        }

        [Test]
        public void PopupPopsRisesAndFadesOut()
        {
            DamagePopupLayer.Evaluate(0f, 1f, 90f, 1.35f, out var alpha, out var offset, out var scale);
            Assert.That(alpha, Is.EqualTo(1f));
            Assert.That(offset, Is.EqualTo(0f));
            Assert.That(scale, Is.EqualTo(1.35f));

            DamagePopupLayer.Evaluate(.5f, 1f, 90f, 1.35f, out alpha, out offset, out scale);
            Assert.That(offset, Is.EqualTo(67.5f).Within(.001f));
            Assert.That(alpha, Is.EqualTo(1f - .1f / .6f).Within(.001f));
            Assert.That(scale, Is.EqualTo(1f));

            DamagePopupLayer.Evaluate(1f, 1f, 90f, 1.35f, out alpha, out offset, out scale);
            Assert.That(alpha, Is.EqualTo(0f).Within(.0001f));
            Assert.That(offset, Is.EqualTo(90f).Within(.0001f));
            Assert.That(scale, Is.EqualTo(1f));
        }

        [Test]
        public void ZeroDurationEndsImmediately()
        {
            DamagePopupLayer.Evaluate(0f, 0f, 90f, 1.35f, out var alpha, out var offset, out var scale);
            Assert.That(alpha, Is.EqualTo(0f).Within(.0001f));
            Assert.That(offset, Is.EqualTo(90f));
            Assert.That(scale, Is.EqualTo(1f));
        }
    }
}
