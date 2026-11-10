using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// #631: a dim saved inside a frame fitted smaller than the screen (the menus' 402×874 frame) is drawn out to the screen's
    /// edges, while its own rect, the card inside it and the area it takes touches on stay as saved.
    /// </summary>
    public sealed class GameScreenEdgeCoverFlowTests
    {
        private GameObject holder;

        [TearDown]
        public void Cleanup()
        {
            if (holder != null) Object.Destroy(holder);
            holder = null;
        }

        [UnityTest]
        public IEnumerator ADimInAFittedFrameIsDrawnToTheEdgesWithoutMovingItsCard()
        {
            holder = new GameObject("Edge cover test", typeof(RectTransform), typeof(Canvas));
            holder.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = (RectTransform)holder.transform;
            yield return null;
            // The frame is the screen's size but drawn at 90%: a strip is left on every side.
            RectTransform frame = Rect("Frame", root);
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.sizeDelta = root.rect.size;
            frame.localScale = Vector3.one * .9f;
            Image dim = new GameObject("Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dim.rectTransform.SetParent(frame, false);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            dim.color = new Color(0f, 0f, 0f, .8f);
            // Like the direct connection card: placed from the dim's top-left corner.
            RectTransform card = Rect("Card", dim.rectTransform);
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(100f, -200f);
            card.sizeDelta = new Vector2(50f, 50f);
            yield return null;
            Rect savedRect = dim.rectTransform.rect;
            Vector3 cardBefore = card.position;

            GameScreenEdgeCover cover = GameScreenEdgeCover.Ensure(dim);
            Assert.That(GameScreenEdgeCover.Ensure(dim), Is.SameAs(cover), "Added once.");
            yield return null;
            AssertDrawnToTheScreensEdges(dim);
            Assert.That(dim.rectTransform.rect, Is.EqualTo(savedRect), "Its own rect stays as saved.");
            Assert.That(Vector3.Distance(card.position, cardBefore), Is.LessThan(.01f), "The card inside it stays put.");
            var strip = new Vector2(Screen.width * .02f, Screen.height * .5f);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(dim.rectTransform, strip, null), Is.False,
                "The area it takes touches on is its own rect, not the strip.");

            // The frame refitted smaller (a turned or resized screen): drawn out to the edges again.
            frame.localScale = Vector3.one * .8f;
            yield return null;
            yield return null;
            AssertDrawnToTheScreensEdges(dim);
        }

        /// <summary>What the dim draws reaches every edge of its root canvas, the whole screen.</summary>
        internal static void AssertDrawnToTheScreensEdges(Graphic dim)
        {
            GameScreenEdgeCover cover = dim.GetComponent<GameScreenEdgeCover>();
            Assert.That(cover, Is.Not.Null, dim.name + " has the edge cover (#631).");
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)dim.canvas.rootCanvas.transform).GetWorldCorners(corners);
            Vector3 min = dim.rectTransform.TransformPoint(cover.DrawnArea.min);
            Vector3 max = dim.rectTransform.TransformPoint(cover.DrawnArea.max);
            Assert.That(min.x, Is.LessThanOrEqualTo(corners[0].x + .5f), dim.name + ": left edge");
            Assert.That(min.y, Is.LessThanOrEqualTo(corners[0].y + .5f), dim.name + ": bottom edge");
            Assert.That(max.x, Is.GreaterThanOrEqualTo(corners[2].x - .5f), dim.name + ": right edge");
            Assert.That(max.y, Is.GreaterThanOrEqualTo(corners[2].y - .5f), dim.name + ": top edge");
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
