using System;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabOrbAreaShapeTests
    {
        [Test]
        public void LegacyRectanglePreservesItsExactCentreBoundsAndMapping()
        {
            var rectangle = new Rect(-3f, -2f, 6f, 4f);
            var shape = LabOrbAreaShape.FromCenterBounds(rectangle, .3f);
            Assert.That(shape.IsCurved, Is.False);
            Assert.That(shape.CenterBounds, Is.EqualTo(rectangle));
            Assert.That(shape.VerticalRange(0f, .3f), Is.EqualTo(new Vector2(-2f, 2f)));
            Assert.That(shape.Bottom[0].y, Is.EqualTo(-2.3f).Within(.00001f));
            Assert.That(shape.Top[0].y, Is.EqualTo(2.3f).Within(.00001f));
            var normalized = new Vector2(.2f, .7f);
            Vector2 world = shape.ToWorld(normalized, .3f);
            Assert.That(world.x, Is.EqualTo(-1.8f).Within(.00001f));
            Assert.That(world.y, Is.EqualTo(.8f).Within(.00001f));
            AssertVector(shape.ToNormalized(world, .3f), normalized);
            Assert.That(shape.VerticalRange(0f, .8f), Is.EqualTo(new Vector2(-1.5f, 1.5f)));
        }

        [Test]
        public void CurvedSurfaceUsesNormalClearanceRatherThanOnlySubtractingRadius()
        {
            var shape = Create(new[] { new Vector2(-5f, 0f), new Vector2(5f, 0f) },
                new[] { new Vector2(-5f, 1.5f), new Vector2(5f, 6.5f) }, .25f);
            Vector2 range = shape.VerticalRange(0f, .5f);
            Assert.That(range.x, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(range.y, Is.EqualTo(4f - .5f * Mathf.Sqrt(1.25f)).Within(.00001f));
            Assert.That(shape.Contains(new Vector2(0f, range.y), .5f), Is.True);
            Assert.That(shape.Contains(new Vector2(0f, 3.49f), .5f), Is.False,
                "A vertical-only radius inset would incorrectly accept a circle intersecting the sloped wall.");

            Vector2 outside = new Vector2(0f, 6f);
            Vector2 clamped = shape.Clamp(outside, .5f);
            Assert.That(shape.Contains(clamped, .5f), Is.True);
            Assert.That(clamped.x, Is.GreaterThan(.1f), "Closest correction follows the sloped wall normal.");
            Assert.That((clamped - outside).magnitude,
                Is.LessThan((new Vector2(0f, range.y) - outside).magnitude));
        }

        [Test]
        public void PaperCurveIncludesItsBowAndRejectsTheSurroundingRectangleCorners()
        {
            var shape = Paper(.2f);
            Assert.That(shape.IsCurved, Is.True);
            Assert.That(shape.Contains(new Vector2(0f, .3f), .2f), Is.True);
            Assert.That(shape.Contains(new Vector2(-2.8f, .3f), .2f), Is.False);
            Assert.That(shape.Contains(new Vector2(0f, 3.7f), .2f), Is.True);
            Assert.That(shape.Contains(new Vector2(2.8f, 3.7f), .2f), Is.False);
            Assert.That(shape.VerticalRange(0f, .2f).y,
                Is.GreaterThan(shape.VerticalRange(2.8f, .2f).y));
        }

        [Test]
        public void OpenSidesDoNotInsetByTheOrbOrDrumRadius()
        {
            var shape = Paper(.2f);
            foreach (float radius in new[] { .2f, .6f })
            {
                Assert.That(shape.CanFitRadius(radius), Is.True);
                Vector2 atLeft = shape.ToWorld(new Vector2(0f, .5f), radius);
                Vector2 atRight = shape.ToWorld(new Vector2(1f, .5f), radius);
                Assert.That(atLeft.x, Is.EqualTo(-3f));
                Assert.That(atRight.x, Is.EqualTo(3f));
                Assert.That(shape.Contains(atLeft, radius), Is.True);
                Assert.That(shape.Contains(atRight, radius), Is.True);
                Assert.That(shape.Clamp(atLeft - Vector2.right * 10f, radius).x, Is.EqualTo(-3f));
            }
        }

        [Test]
        public void TransferAndResizeMappingUseTheLocalAvailableHeightForEachRadius()
        {
            var shape = Paper(.2f);
            var differentPaper = Create(new[] {
                new Vector2(-6f, -1f), new Vector2(0f, -2f), new Vector2(6f, 0f)
            }, new[] {
                new Vector2(-6f, 4f), new Vector2(0f, 6f), new Vector2(6f, 4.5f)
            }, .2f);
            foreach (float radius in new[] { .2f, .6f })
            {
                var normalized = new Vector2(1f, .83f);
                Vector2 exit = shape.ToWorld(normalized, radius);
                Vector2 observed = shape.ToNormalized(exit, radius);
                AssertVector(observed, normalized);
                Vector2 arrival = differentPaper.ToWorld(new Vector2(0f, observed.y), radius);
                Assert.That(differentPaper.Contains(arrival, radius), Is.True);
                Assert.That(differentPaper.ToNormalized(arrival, radius).y,
                    Is.EqualTo(.83f).Within(.00001f));
                Assert.That(arrival.x, Is.EqualTo(-6f));
            }
        }

        [Test]
        public void ADrumMustFitTheNarrowSideOpeningsAsWellAsTheCentre()
        {
            var shape = Paper(.2f);
            Assert.That(shape.CanFitRadius(.8f), Is.True);
            Assert.That(shape.CanFitRadius(1.01f), Is.False,
                "The middle is tall enough, but a diameter larger than two cannot pass the side openings.");
            Assert.That(shape.CanFitRadius(float.NaN), Is.False);
            Assert.That(shape.CanFitRadius(-1f), Is.False);
        }

        [Test]
        public void TheShapeCopiesInputAndGeometryComparisonIncludesInteriorCurveChanges()
        {
            var bottom = new[] { new Vector2(-3f, 1f), new Vector2(0f, 0f), new Vector2(3f, 1f) };
            var top = new[] { new Vector2(-3f, 3f), new Vector2(0f, 4f), new Vector2(3f, 3f) };
            var shape = Create(bottom, top, .2f);
            var same = Create(bottom, top, .2f);
            bottom[1] = new Vector2(0f, .5f);
            var changed = Create(bottom, top, .2f);
            Assert.That(shape.Bottom[1].y, Is.EqualTo(0f));
            Assert.That(shape.ApproximatelyEquals(same), Is.True);
            Assert.That(shape.ApproximatelyEquals(changed), Is.False);
            Assert.That(shape.ApproximatelyEquals(null), Is.False);
        }

        [Test]
        public void MissingUnsortedCrossedNonFiniteOrTooSmallCurvesAreRejected()
        {
            Vector2[] bottom = { new Vector2(-1f, 0f), new Vector2(1f, 0f) };
            Vector2[] top = { new Vector2(-1f, 2f), new Vector2(1f, 2f) };
            Assert.That(LabOrbAreaShape.TryCreate(null, top, .2f, out _, out _), Is.False);
            Assert.That(LabOrbAreaShape.TryCreate(new[] { bottom[0] }, top, .2f, out _, out _), Is.False);
            Assert.That(LabOrbAreaShape.TryCreate(new[] { bottom[1], bottom[0] }, top, .2f, out _, out _), Is.False);
            Assert.That(LabOrbAreaShape.TryCreate(bottom,
                new[] { top[0], new Vector2(0f, -.1f), top[1] }, .2f, out _, out _), Is.False);
            Assert.That(LabOrbAreaShape.TryCreate(bottom,
                new[] { top[0], new Vector2(float.NaN, 2f) }, .2f, out _, out _), Is.False);
            Assert.That(LabOrbAreaShape.TryCreate(bottom, top, 1.1f, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(LabOrbAreaShape.TryCreate(bottom,
                new[] { new Vector2(-2f, 2f), top[1] }, .2f, out _, out _), Is.False);
            Assert.Throws<ArgumentException>(() => LabOrbAreaShape.FromCenterBounds(new Rect(), .2f));
        }

        private static LabOrbAreaShape Paper(float radius) => Create(new[] {
            new Vector2(-3f, 1f), new Vector2(0f, 0f), new Vector2(3f, 1f)
        }, new[] {
            new Vector2(-3f, 3f), new Vector2(0f, 4f), new Vector2(3f, 3f)
        }, radius);

        private static LabOrbAreaShape Create(Vector2[] bottom, Vector2[] top, float radius)
        {
            Assert.That(LabOrbAreaShape.TryCreate(bottom, top, radius, out var shape, out string error), Is.True, error);
            return shape;
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.00001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.00001f));
        }
    }
}
