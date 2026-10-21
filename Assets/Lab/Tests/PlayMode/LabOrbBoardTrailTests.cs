using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// 조합판 결합 구슬의 얕은 오행 꼬리 (#254): 던진 구슬과 같은 오행 꼬리를 판 크기에 맞춰 작게 쓰고,
    /// 움직일 때만, 빠를수록 진하게 보이며, 음양 구슬이나 DEV OFF에서는 없다.
    /// </summary>
    public sealed class LabOrbBoardTrailTests
    {
        private const float BoardRadius = .32f, ThrowRadius = .165f;
        private GameObject orb;
        private LabOrbView view;
        private LabOrbBoardTrail trail;
        private LabOrbAppearance look;

        [SetUp]
        public void MakeOrb()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            Assert.That(config, Is.Not.Null);
            look = config.OrbAppearance != null ? config.OrbAppearance : LabOrbAppearance.Defaults;
            orb = new GameObject("Board trail orb");
            view = orb.AddComponent<LabOrbView>();
            view.Configure("trail-orb", LabOrbKind.Combined, BoardRadius, LabOrbElement.Fire);
            view.ConfigureAppearance(look);
            trail = orb.AddComponent<LabOrbBoardTrail>();
            trail.enabled = false; // the test steps time itself
        }

        [TearDown]
        public void DestroyOrb()
        {
            if (orb != null) Object.DestroyImmediate(orb);
        }

        [Test]
        public void ACombinedOrbGetsItsElementsTrailScaledToTheBoard()
        {
            LabElementTrailProfile thrown = look.ElementTrailFor(LabOrbElement.Fire);
            Assume.That(thrown, Is.Not.Null, "The saved appearance has a Fire flight trail.");
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.True);
            Assert.That(trail.Trail.Element, Is.EqualTo(LabOrbElement.Fire), "The same ink as a thrown Fire orb.");
            float scale = BoardRadius / ThrowRadius * trail.Size;
            Assert.That(trail.Trail.Profile.Width, Is.EqualTo(thrown.Width * scale).Within(1e-4f));
            Assert.That(trail.Trail.Profile.MaxLength, Is.EqualTo(thrown.MaxLength * scale).Within(1e-4f));
            Assert.That(trail.Trail.Profile, Is.Not.SameAs(thrown), "The shared thrown-orb profile is never edited.");
            Assert.That(thrown.Width, Is.EqualTo(look.ElementTrailFor(LabOrbElement.Fire).Width));
        }

        [Test]
        public void ItShowsOnlyWhileMovingAndDeepensWithSpeed()
        {
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            trail.Step(.02f); // first frame only remembers where the orb is
            Assert.That(trail.Intensity, Is.Zero);

            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.Zero, "Still: no trail.");

            orb.transform.position += Vector3.right * BoardRadius * .02f; // 1 radius per second
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.Zero, "Barely moving: no trail.");

            orb.transform.position += Vector3.right * BoardRadius * 5f * .02f;
            trail.Step(.02f);
            float medium = trail.Intensity;
            Assert.That(medium, Is.GreaterThan(0f).And.LessThan(trail.Strength));

            orb.transform.position += Vector3.right * BoardRadius * 20f * .02f;
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.EqualTo(trail.Strength).Within(1e-5f), "Fast: as deep as it gets, still shallow.");
            Assert.That(trail.Strength, Is.LessThan(1f), "Shallower than a thrown orb's trail.");

            Assert.That(trail.IntensityFor(5f), Is.EqualTo(medium).Within(1e-4f));
            Assert.That(trail.IntensityFor(3f), Is.LessThan(trail.IntensityFor(6f)));
        }

        [Test]
        public void YinYangOrbsAndADevOffHaveNoTrail()
        {
            trail.Sync(view, look, false, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.False, "DEV Board Orb Trail OFF.");

            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.True);
            view.Configure("trail-orb", LabOrbKind.Yin, BoardRadius);
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.False, "Raw Yin/Yang orbs are not thrown, so no element trail.");
            Assert.That(trail.Trail.PathCount, Is.Zero, "Turning off clears what was drawn.");

            view.Configure("trail-orb", LabOrbKind.Combined, BoardRadius, LabOrbElement.Water);
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assume.That(look.ElementTrailFor(LabOrbElement.Water), Is.Not.Null);
            Assert.That(trail.Trail.Element, Is.EqualTo(LabOrbElement.Water), "A new element brings its own ink.");
        }
    }
}
