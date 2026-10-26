using C6Lab.Experiments;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabHighResolutionPaintTests
    {
        private static LabPaintStudyMask Mask()
        {
            Texture2D source = Resources.Load<Texture2D>("GourdPaint/Patterns/Splatter");
            return new LabPaintStudyMask(source.width, source.height, source.GetPixels32());
        }

        [Test]
        public void FourStageVisualFixturePreservesProductionPixelsOwnersAndRevision()
        {
            var source = Resources.Load<LabConfig>("LabConfig");
            string beforeConfig = JsonUtility.ToJson(source);
            var settings = source.PaintGrowthSettings; settings.DryingEnabled = false;
            var fixture = new LabHighResolutionPaintFixture(settings, Mask(), Vector3.forward,
                LabPaintPalette.ColorFor(LabOrbElement.Water));
            var control = new LabPaintSurface(settings, 123);
            for (int stage = 0; stage < 4; stage++)
            {
                control.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(LabOrbElement.Water), 1, 1);
                if (stage > 0) control.Advance(1d);
                var actual = fixture.Stages[stage];
                Assert.That(LabHighResolutionPaintFixture.Pixels(actual), Is.EqualTo(LabHighResolutionPaintFixture.Pixels(control)));
                Assert.That(actual.Revision, Is.EqualTo(control.Revision));
                Assert.That(actual.CoveredCount, Is.EqualTo((int)decimal.Floor((2m + stage * 1.5m) * control.CellCount / 100m)));
                for (int cell = 0; cell < control.CellCount; cell++)
                    Assert.That(actual.GetPainterSlot(cell), Is.EqualTo(control.GetPainterSlot(cell)));
            }
            Assert.That(settings.DryingEnabled, Is.False);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(beforeConfig), "The saved config must never become a fixture setting.");
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        public void ContinuousBrushIsFiniteAndRepeatableAtFrontSeamAndPole(float x, float y, float z)
        {
            var settings = Resources.Load<LabConfig>("LabConfig").PaintGrowthSettings; settings.DryingEnabled = false;
            LabPaintStudyMask mask = Mask();
            var a = new LabHighResolutionPaintFixture(settings, mask, new Vector3(x,y,z), Color.blue);
            var b = new LabHighResolutionPaintFixture(settings, mask, new Vector3(x,y,z), Color.blue);
            for (int stage = 0; stage < 4; stage++)
            {
                var stamp = a.Stamps[stage];
                Assert.That(stamp.Center, Is.EqualTo(b.Stamps[stage].Center));
                Assert.That(stamp.Radius, Is.EqualTo(b.Stamps[stage].Radius));
                Assert.That(stamp.Radius, Is.InRange(.01f, 1.5f));
                Assert.That(Vector3.Dot(stamp.Center, stamp.Right), Is.EqualTo(0).Within(.00001f));
                Assert.That(Vector3.Dot(stamp.Right, stamp.Up), Is.EqualTo(0).Within(.00001f));
                Assert.That(LabHighResolutionPaintFixture.Sample(mask, stamp, stamp.Center), Is.GreaterThan(.15f));
                Assert.That(LabHighResolutionPaintFixture.Sample(mask, stamp, -stamp.Center), Is.Zero);
            }
        }
    }
}
