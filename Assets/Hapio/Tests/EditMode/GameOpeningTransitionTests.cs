using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>#635: the last opening picture zooms toward the valley, quickly at first and gently at the end, as the clouds close.</summary>
    public sealed class GameOpeningTransitionTests
    {
        private static readonly Vector2 Page = new Vector2(402f, 874f);

        [Test]
        public void SavedArtZoomsTwoAndAHalfTimesTowardTheValleyWhileTheCloudsClose()
        {
            GameOpeningTransitionArt art = GameOpeningTransitionArt.Load();
            Assert.That(art, Is.Not.Null, GameOpeningTransitionArt.ResourcePath);
            Assert.That(art.zoomFocus.x, Is.EqualTo(.533f).Within(.01f), "The spot from the reference picture: a little right of the middle,");
            Assert.That(art.zoomFocus.y, Is.EqualTo(.254f).Within(.01f), "a quarter of the way down (between the peaks and the temple roof).");
            Assert.That(art.zoomScale, Is.EqualTo(2.5f));
            Assert.That(art.zoomSeconds, Is.EqualTo(GameCloudCoverArt.Load().cloudWipe.SwapTime).Within(1e-4f), "Done as the clouds cover.");
            Assert.That(art.focusToCenter, Is.EqualTo(1f));
            Assert.That(art.ZoomAt(0f), Is.EqualTo(0f));
            Assert.That(art.ZoomAt(art.zoomSeconds), Is.EqualTo(1f));
            Assert.That(art.ZoomAt(art.zoomSeconds * .3f), Is.GreaterThan(.6f), "Quick at first.");
            Assert.That(1f - art.ZoomAt(art.zoomSeconds * .9f), Is.LessThan(.01f), "And settling gently at the end.");
        }

        [Test]
        public void TheValleyEndsInTheMiddleAndThePageNeverShowsItsEdge()
        {
            GameOpeningTransitionArt art = GameOpeningTransitionArt.Load();
            Vector2 focus = art.FocusFromMiddle(Page);
            for (float zoom = 0f; zoom <= 1.0001f; zoom += .05f)
            {
                (float scale, Vector2 move) = GameOpeningTransitionArt.ZoomPose(zoom, focus, Page, art.zoomScale, art.focusToCenter);
                AssertCovers(scale, move, zoom);
            }
            (float end, Vector2 endMove) = GameOpeningTransitionArt.ZoomPose(1f, focus, Page, art.zoomScale, art.focusToCenter);
            Assert.That(end, Is.EqualTo(2.5f));
            Vector2 landed = focus * end + endMove;
            Assert.That(landed.magnitude, Is.LessThan(.01f), "The valley ends in the middle of the screen.");
            (float start, Vector2 startMove) = GameOpeningTransitionArt.ZoomPose(0f, focus, Page, art.zoomScale, art.focusToCenter);
            Assert.That((start, startMove), Is.EqualTo((1f, Vector2.zero)), "Starts as the picture was.");
        }

        [Test]
        public void AFocusNearACornerWithLittleZoomStillCoversThePage()
        {
            var corner = new Vector2(-Page.x * .5f, Page.y * .5f);
            for (float zoom = 0f; zoom <= 1.0001f; zoom += .1f)
            {
                (float scale, Vector2 move) = GameOpeningTransitionArt.ZoomPose(zoom, corner, Page, 1.2f, 1f);
                AssertCovers(scale, move, zoom);
            }
        }

        /// <summary>The picture grown about the middle and moved still reaches every edge of the page.</summary>
        private static void AssertCovers(float scale, Vector2 move, float zoom)
        {
            Vector2 half = Page * .5f, grown = half * scale;
            Assert.That(move.x - grown.x, Is.LessThanOrEqualTo(-half.x + 1e-3f), "left at " + zoom);
            Assert.That(move.x + grown.x, Is.GreaterThanOrEqualTo(half.x - 1e-3f), "right at " + zoom);
            Assert.That(move.y - grown.y, Is.LessThanOrEqualTo(-half.y + 1e-3f), "bottom at " + zoom);
            Assert.That(move.y + grown.y, Is.GreaterThanOrEqualTo(half.y - 1e-3f), "top at " + zoom);
        }
    }
}
