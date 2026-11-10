using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>#631: a dim saved inside the fitted 402×874 frame is drawn out to the screen's edges.</summary>
    public sealed class GameScreenEdgeCoverTests
    {
        [Test]
        public void GrowsOnlyTheSidesThatStopShortOfTheScreen()
        {
            var frame = new Rect(-201f, -437f, 402f, 874f);
            // A screen a little wider than the frame: thin strips on the left and right.
            var wider = Rect.MinMaxRect(-202.5f, -437f, 202.5f, 437f);
            Assert.That(GameScreenEdgeCover.Covering(frame, wider), Is.EqualTo(wider));
            // A taller one: strips above and below.
            var taller = Rect.MinMaxRect(-201f, -450f, 201f, 450f);
            Assert.That(GameScreenEdgeCover.Covering(frame, taller), Is.EqualTo(taller));
            // Already reaching the edges (or past them): left as saved, never shrunk.
            Assert.That(GameScreenEdgeCover.Covering(frame, frame), Is.EqualTo(frame));
            var smaller = Rect.MinMaxRect(-100f, -100f, 100f, 100f);
            Assert.That(GameScreenEdgeCover.Covering(frame, smaller), Is.EqualTo(frame));
        }

        [Test]
        public void MovesEachCornerOfTheDrawingToTheMatchingScreenCorner()
        {
            var frame = new Rect(-201f, -437f, 402f, 874f);
            var screen = Rect.MinMaxRect(-203f, -440f, 203f, 440f);
            Assert.That(GameScreenEdgeCover.Stretch(new Vector3(frame.xMin, frame.yMin, 0f), frame, screen),
                Is.EqualTo(new Vector3(screen.xMin, screen.yMin, 0f)));
            Assert.That(GameScreenEdgeCover.Stretch(new Vector3(frame.xMax, frame.yMax, 0f), frame, screen),
                Is.EqualTo(new Vector3(screen.xMax, screen.yMax, 0f)));
            Vector3 middle = GameScreenEdgeCover.Stretch(Vector3.zero, frame, screen);
            Assert.That(middle.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(middle.y, Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
