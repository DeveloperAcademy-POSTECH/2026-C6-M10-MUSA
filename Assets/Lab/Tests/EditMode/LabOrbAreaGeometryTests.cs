using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabOrbAreaGeometryTests
    {
        private GameObject root;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Area projection camera");
            camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 2.5f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(390f, 844f)]
        [TestCase(430f, 932f)]
        [TestCase(768f, 1024f)]
        public void HudExclusionMovesOnlyVerticalLimitsAndKeepsOrbScale(float width, float height)
        {
            camera.pixelRect = new Rect(0f, 0f, width, height * .46f);
            var usable = Rect.MinMaxRect(0f, height * .09f, width, height * .4f);
            Assert.That(LabOrbAreaGeometry.TryCenterBounds(camera, usable, 5f, .32f, .6f,
                out Rect bounds, out string error), Is.True, error);
            float margin = .32f + LabOrbAreaGeometry.SurfaceClearance;
            float expectedWidth = 5f * camera.aspect - 2f * margin;
            Assert.That(bounds.width, Is.EqualTo(expectedWidth).Within(.001f));
            Vector3 visibleBottom = camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.yMin - .32f, 0f));
            Vector3 visibleTop = camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.yMax + .32f, 0f));
            Assert.That(visibleBottom.y, Is.GreaterThan(usable.yMin));
            Assert.That(visibleTop.y, Is.LessThan(usable.yMax));
            Assert.That(camera.orthographicSize, Is.EqualTo(2.5f));
        }

        [Test]
        public void DevCandidateMustFitTheLargestDrumWithoutChangingTheCamera()
        {
            camera.pixelRect = new Rect(0f, 0f, 390f, 844f * .46f);
            var usable = Rect.MinMaxRect(0f, 80f, 390f, 337.6f);
            Assert.That(LabOrbAreaGeometry.TryCenterBounds(camera, usable, 5f, .32f, 2f,
                out _, out string error), Is.False);
            Assert.That(error, Does.Contain("too large"));
            Assert.That(LabOrbAreaGeometry.TryCenterBounds(camera, usable, 8f, .32f, 2f,
                out _, out _), Is.True);
            Assert.That(camera.orthographicSize, Is.EqualTo(2.5f));
        }

        [Test]
        public void EmptyOffscreenOrNonFiniteAreasAreRejected()
        {
            camera.pixelRect = new Rect(0f, 0f, 390f, 388f);
            foreach (Rect area in new[] { new Rect(), new Rect(500f, 0f, 100f, 100f),
                new Rect(float.NaN, 0f, 390f, 300f) })
                Assert.That(LabOrbAreaGeometry.TryCenterBounds(camera, area, 5f, .32f, .6f,
                    out _, out _), Is.False);
        }
    }
}
