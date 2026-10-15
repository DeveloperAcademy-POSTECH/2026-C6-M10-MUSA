using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabOrbAreaProfileTests
    {
        private GameObject root;
        private LabOrbAreaProfile profile;
        private static readonly Rect Plate = new Rect(-3f, 0f, 6f, 4f);
        private static readonly Rect Hud = new Rect(-4f, -1f, 8f, 3f);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Paper and HUD profile fixture");
            profile = root.AddComponent<LabOrbAreaProfile>();
            profile.SetBakedBoundary(null, .5f, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f) },
                new[] { new Vector2(0f, 1f), new Vector2(1f, 1f) });
            profile.SetBakedOccluder(null, .5f, HudContour());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void PaperOnlyAdapterRetainsTheOriginalSurfaceRegardlessOfTheBakedHud()
        {
            Assert.That(profile.TryBuildShape(Plate, .1f, .2f, out var shape, out string error), Is.True, error);
            Assert.That(shape.LowerBoundary.Count, Is.EqualTo(2));
            Assert.That(shape.LowerBoundary[0], Is.EqualTo(new Vector2(-3f, 0f)));
            Assert.That(shape.LowerBoundary[1], Is.EqualTo(new Vector2(3f, 0f)));
            Assert.That(shape.UpperBoundary[0], Is.EqualTo(new Vector2(-3f, 4f)));
            Assert.That(shape.CenterBounds.xMin, Is.EqualTo(Plate.xMin));
            Assert.That(shape.CenterBounds.xMax, Is.EqualTo(Plate.xMax));
        }

        [Test]
        public void VisibleLowerSurfaceUsesActualHudOffsetAndKeepsThePaperCurvesAndSidePlanes()
        {
            Assert.That(profile.TryBuildShape(Plate, Hud, .1f, .2f, out var shape, out string error), Is.True, error);
            // HUD: -1 + 3 * (.2 + .7 * |x|/4). Its paper-centre height is below the
            // paper, but its sides rise into it. A single horizontal cut loses this region.
            Assert.That(SurfaceY(shape.LowerBoundary, -3f), Is.EqualTo(1.175f).Within(.00001f));
            Assert.That(SurfaceY(shape.LowerBoundary, 0f), Is.EqualTo(0f).Within(.00001f));
            Assert.That(SurfaceY(shape.LowerBoundary, 3f), Is.EqualTo(1.175f).Within(.00001f));
            Assert.That(shape.UpperBoundary.Count, Is.EqualTo(2));
            Assert.That(shape.UpperBoundary[0].y, Is.EqualTo(4f));
            Assert.That(shape.CenterBounds.xMin, Is.EqualTo(-3f));
            Assert.That(shape.CenterBounds.xMax, Is.EqualTo(3f));
            Assert.That(shape.ToWorld(new Vector2(0f, .5f), .2f).x, Is.EqualTo(-3f));
            Assert.That(shape.ToWorld(new Vector2(1f, .5f), .2f).x, Is.EqualTo(3f));
        }

        [Test]
        public void MergeAddsExactCrossingsBetweenOriginalSampleColumns()
        {
            Assert.That(profile.TryBuildShape(Plate, Hud, .1f, .2f, out var shape, out string error), Is.True, error);
            float crossing = .4f / .525f;
            bool foundLeft = false, foundRight = false;
            foreach (Vector2 point in shape.LowerBoundary)
            {
                foundLeft |= Mathf.Abs(point.x + crossing) < .00001f && Mathf.Abs(point.y) < .00001f;
                foundRight |= Mathf.Abs(point.x - crossing) < .00001f && Mathf.Abs(point.y) < .00001f;
            }
            Assert.That(foundLeft && foundRight, Is.True,
                "The two HUD/paper intersections are not any of the original samples.");
            foreach (float x in new[] { -3f, -2.5f, -1f, -.8f, -.7f, 0f, .7f, .8f, 1f, 2.5f, 3f })
                Assert.That(SurfaceY(shape.LowerBoundary, x),
                    Is.EqualTo(Mathf.Max(0f, -.4f + .525f * Mathf.Abs(x))).Within(.00001f));
        }

        [Test]
        public void HudAndPaperVertexUnionRetainsCornersFromBothSilhouettes()
        {
            profile.SetBakedBoundary(null, .5f, new[] {
                new Vector2(0f, 0f), new Vector2(.5f, .2f), new Vector2(1f, 0f)
            }, new[] { new Vector2(0f, 1f), new Vector2(1f, 1f) });
            Assert.That(profile.TryBuildShape(Plate, Hud, .1f, .2f, out var shape, out string error), Is.True, error);
            Assert.That(SurfaceY(shape.LowerBoundary, 0f), Is.EqualTo(.8f).Within(.00001f));
            Assert.That(SurfaceY(shape.LowerBoundary, -3f), Is.EqualTo(1.175f).Within(.00001f));
            Assert.That(SurfaceY(shape.LowerBoundary, 3f), Is.EqualTo(1.175f).Within(.00001f));
            foreach (float x in new[] { -2.8f, -2f, -1.1f, -.1f, .1f, 1.1f, 2f, 2.8f })
            {
                float paper = .8f * (1f - Mathf.Abs(x) / 3f);
                float hud = -.4f + .525f * Mathf.Abs(x);
                Assert.That(SurfaceY(shape.LowerBoundary, x), Is.EqualTo(Mathf.Max(paper, hud)).Within(.00001f));
            }
        }

        [Test]
        public void CacheIncludesHudRectangleModeAndInPlaceContourEdits()
        {
            Assert.That(profile.TryBuildShape(Plate, Hud, .1f, .2f, out var first, out string error), Is.True, error);
            Assert.That(profile.TryBuildShape(Plate, Hud, .1f, .2f, out var cached, out _), Is.True);
            Assert.That(cached, Is.SameAs(first));
            Rect shiftedHud = new Rect(Hud.x, Hud.y + .2f, Hud.width, Hud.height);
            Assert.That(profile.TryBuildShape(Plate, shiftedHud, .1f, .2f, out var moved, out error), Is.True, error);
            Assert.That(moved, Is.Not.SameAs(first));
            Assert.That(SurfaceY(moved.LowerBoundary, -3f), Is.EqualTo(1.375f).Within(.00001f));

            // The Inspector can edit serialized elements without calling a public setter.
            ((Vector2[])profile.LowerOccluderBoundary)[1] = new Vector2(.5f, .5f);
            Assert.That(profile.TryBuildShape(Plate, shiftedHud, .1f, .2f, out var edited, out error), Is.True, error);
            Assert.That(edited, Is.Not.SameAs(moved));
            Assert.That(SurfaceY(edited.LowerBoundary, 0f), Is.EqualTo(.7f).Within(.00001f));
            Assert.That(profile.TryBuildShape(Plate, .1f, .2f, out var paperOnly, out error), Is.True, error);
            Assert.That(SurfaceY(paperOnly.LowerBoundary, 0f), Is.EqualTo(0f));

            ((Vector2[])profile.LowerBoundary)[0] = new Vector2(0f, .1f);
            Assert.That(profile.TryBuildShape(Plate, .1f, .2f, out var paperEdited, out error), Is.True, error);
            Assert.That(paperEdited, Is.Not.SameAs(paperOnly));
            Assert.That(paperEdited.LowerBoundary[0].y, Is.EqualTo(.4f).Within(.00001f));
        }

        [Test]
        public void BakedOccluderCopiesSamplesAndRetainsItsOwnSourceAndAlphaThreshold()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Vector2[] points = HudContour();
                profile.SetBakedOccluder(texture, .35f, points);
                points[1].y = 1f;
                Assert.That(profile.LowerOccluderTexture, Is.SameAs(texture));
                Assert.That(profile.LowerOccluderAlphaThreshold, Is.EqualTo(.35f));
                Assert.That(profile.LowerOccluderBoundary[1].y, Is.EqualTo(.2f));
                Assert.That(profile.SourceTexture, Is.Null);
                Assert.That(profile.AlphaThreshold, Is.EqualTo(.5f));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MissingPartialOrInvalidHudDataFailsWithoutInventingARectangleCut()
        {
            Rect partial = new Rect(-2f, -1f, 4f, 3f);
            Assert.That(profile.TryBuildShape(Plate, partial, .1f, .2f, out var shape, out string error), Is.False);
            Assert.That(shape, Is.Null);
            Assert.That(error, Does.Contain("full width"));
            profile.SetBakedOccluder(null, .5f, null);
            Assert.That(profile.TryBuildShape(Plate, Hud, .1f, .2f, out _, out error), Is.False);
            Assert.That(error, Does.Contain("not been baked"));
            profile.SetBakedOccluder(null, .5f, new[] {
                new Vector2(0f, .9f), new Vector2(.5f, float.NaN), new Vector2(1f, .9f)
            });
            Assert.That(profile.TryBuildShape(Plate, Hud, .1f, .2f, out _, out _), Is.False);
            Assert.That(profile.TryBuildShape(Plate, .1f, .2f, out _, out error), Is.True, error);
        }

        [Test]
        public void LargestDrumMustFitTheVisibleRegionRatherThanTheHiddenPaper()
        {
            profile.SetBakedOccluder(null, .5f, new[] { new Vector2(0f, 1f), new Vector2(1f, 1f) });
            var tallHud = new Rect(-4f, 0f, 8f, 3.7f);
            Assert.That(profile.TryBuildShape(Plate, .05f, .3f, out _, out string error), Is.True, error);
            Assert.That(profile.TryBuildShape(Plate, tallHud, .05f, .3f, out var shape, out error), Is.False);
            Assert.That(shape, Is.Null);
            Assert.That(error, Does.Contain("too large"));
        }

        private static Vector2[] HudContour() => new[] {
            new Vector2(0f, .9f), new Vector2(.5f, .2f), new Vector2(1f, .9f)
        };

        private static float SurfaceY(System.Collections.Generic.IReadOnlyList<Vector2> points, float x)
        {
            for (int i = 1; i < points.Count; i++)
                if (x <= points[i].x)
                    return Mathf.LerpUnclamped(points[i - 1].y, points[i].y,
                        (x - points[i - 1].x) / (points[i].x - points[i - 1].x));
            return points[points.Count - 1].y;
        }
    }
}
