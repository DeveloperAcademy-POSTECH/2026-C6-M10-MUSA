using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>The heart-eyed bubbles a charmed player's neighbours see by their badge (#251).</summary>
    public sealed class LabCharmBubblesTests
    {
        private GameObject root;
        private LabCharmBubbles bubbles;
        private RectTransform leftBadge, rightBadge;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Charm bubble test canvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            leftBadge = Badge("Left neighbour badge", new Vector2(-300f, 0f));
            rightBadge = Badge("Right neighbour badge", new Vector2(300f, 0f));
            var host = new GameObject("Charm bubbles");
            host.transform.SetParent(root.transform, false);
            bubbles = host.AddComponent<LabCharmBubbles>();
            bubbles.Bind(null, root.GetComponent<Canvas>(), leftBadge.gameObject, rightBadge.gameObject);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
        }

        private RectTransform Badge(string name, Vector2 position)
        {
            var badge = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            badge.SetParent(root.transform, false);
            badge.sizeDelta = new Vector2(70f, 70f);
            badge.anchoredPosition = position;
            return badge;
        }

        /// <summary>Players 10, 20, 30... in seats 0, 1, 2...; a charm on one of them until the given time, or none.</summary>
        private static LabSnapshot Charm(ulong charmed, double until, int players = 3, int taps = 0)
        {
            var seats = new LabPlayerState[players];
            for (int i = 0; i < players; i++) seats[i] = new LabPlayerState { id = (ulong)(10 * (i + 1)), seat = i };
            return new LabSnapshot
            {
                sessionId = "charm-bubbles", round = 1, phase = LabPhase.Playing, serverTime = 1d, players = seats,
                charmedPlayer = until > 0d ? charmed : 0, charmStartedAt = until > 0d ? 0d : -1d, charmUntil = until,
                charmTaps = until > 0d ? taps : 0
            };
        }

        [UnityTest]
        public IEnumerator TheBubbleBesideTheCharmedPlayerTakesTapsAndTurnsSurprisedWhenWoken()
        {
            // Player 10 in seat 0 has seat 1 (20) on the right and seat 2 (30) on the left.
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            Assert.That(bubbles.RightShowing, Is.True, "The charmed player beside on the right gets a bubble.");
            Assert.That(bubbles.LeftShowing, Is.False);
            Image art = bubbles.RightBubble.GetComponent<Image>();
            Assert.That(art.sprite, Is.Not.Null, "The heart-eyed bubble art.");
            Assert.That(art.raycastTarget, Is.True, "It can be tapped.");
            Assert.That(bubbles.RightBubble.position.y, Is.GreaterThan(rightBadge.position.y),
                "The right one floats above its badge, like the left one (the drum minimap now sits higher).");

            // A frame first, so the swell below is timed by an ordinary frame rather than the test's first one.
            yield return null;
            bubbles.Present(Charm(20, 10d), 10, 1.01d, 3);
            bubbles.RightBubble.GetComponent<Button>().onClick.Invoke();
            Assert.That(bubbles.TapsSent, Is.EqualTo(1));
            Assert.That(bubbles.Pulsing(true), Is.True);
            yield return null;
            bubbles.Present(Charm(20, 10d), 10, 1.02d, 3);
            Assert.That(bubbles.RightBubble.localScale.x, Is.GreaterThan(1f), "Each tap swells it a little...");
            float until = Time.time + .4f;
            while (bubbles.Pulsing(true) && Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(20, 10d), 10, 1.1d, 3);
            }
            Assert.That(bubbles.RightBubble.localScale.x, Is.EqualTo(1f).Within(.0001f), "...and back.");

            // Those beside woke them: the spiky surprised bubble pops out in its place.
            bubbles.ShowWoken(20);
            bubbles.Present(Charm(0, -1d), 10, 2d, 3);
            Assert.That(bubbles.RightShowing, Is.False);
            Assert.That(bubbles.RightWokenShowing, Is.True, "Woken by taps: the surprised bubble.");
            Assert.That(bubbles.RightFading, Is.False);
            until = Time.time + 1.5f;
            while (bubbles.RightWokenShowing && Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(0, -1d), 10, 2d, 3);
            }
            Assert.That(bubbles.RightWokenShowing, Is.False, "It is gone in a moment.");
        }

        [UnityTest]
        public IEnumerator ACharmThatRunsOutOnlyFadesItsBubbleAway()
        {
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            yield return null;
            bubbles.Present(Charm(0, -1d), 10, 10.1d, 3);
            Assert.That(bubbles.RightFading, Is.True, "Run out by itself: the heart bubble fades where it is...");
            Assert.That(bubbles.RightWokenShowing, Is.False, "...with no surprised bubble.");
            float first = bubbles.RightBubble.GetComponent<Image>().color.a;
            float until = Time.time + 1f;
            while (bubbles.RightFading && Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(0, -1d), 10, 10.2d, 3);
                if (bubbles.RightFading)
                    Assert.That(bubbles.RightBubble.GetComponent<Image>().color.a, Is.LessThanOrEqualTo(first));
            }
            Assert.That(bubbles.RightFading || bubbles.RightBubble.gameObject.activeSelf, Is.False, "Then it is gone.");

            // A woken notice that comes just after the snapshot still turns the fading bubble surprised.
            bubbles.Present(Charm(30, 20d), 10, 11d, 3);
            yield return null;
            bubbles.Present(Charm(0, -1d), 10, 11.5d, 3);
            Assert.That(bubbles.LeftFading, Is.True);
            bubbles.ShowWoken(30);
            bubbles.Present(Charm(0, -1d), 10, 11.6d, 3);
            Assert.That(bubbles.LeftWokenShowing, Is.True);
        }

        [UnityTest]
        public IEnumerator TheBubbleFillsWithYellowAsTheWakeUpTapsAddUp()
        {
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            Assert.That(bubbles.RightWakeFill, Is.Zero, "Empty before anyone taps.");
            bubbles.RightBubble.GetComponent<Button>().onClick.Invoke();
            bubbles.Present(Charm(20, 10d), 10, 1.01d, 3);
            Assert.That(bubbles.RightWakeFill, Is.EqualTo(1f / 3f).Within(1e-5f), "This screen's own tap shows at once.");
            bubbles.Present(Charm(20, 10d, taps: 2), 10, 1.1d, 3);
            Assert.That(bubbles.RightWakeFill, Is.EqualTo(2f / 3f).Within(1e-5f),
                "The Host's count adds the other neighbour's tap.");
            RectTransform fill = bubbles.RightBubble.Find("Charm Wake Fill") as RectTransform;
            Assert.That(fill, Is.Not.Null);
            Assert.That(fill.GetComponent<Image>().color.b, Is.LessThan(fill.GetComponent<Image>().color.g), "A light yellow.");
            Assert.That(bubbles.RightBubble.GetComponent<Mask>(), Is.Not.Null, "It stays inside the bubble's shape.");
            float until = Time.time + 1f;
            float lastTop = 0f;
            while (Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(20, 10d, taps: 2), 10, 1.2d, 3);
                Assert.That(fill.anchorMax.y, Is.GreaterThanOrEqualTo(lastTop), "It rises, never falls.");
                lastTop = fill.anchorMax.y;
            }
            Assert.That(fill.gameObject.activeSelf, Is.True);
            Assert.That(lastTop, Is.GreaterThan(.5f), "Two thirds of the way up the cloud.");
        }

        [Test]
        public void ABubbleByABadgeAtTheScreensCornerStillShowsWhole()
        {
            Rect canvas = ((RectTransform)root.transform).rect;
            leftBadge.anchoredPosition = new Vector2(canvas.xMin + 20f, canvas.yMax - 20f);
            bubbles.Present(Charm(30, 10d), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing, Is.True);
            var corners = new Vector3[4];
            var screen = new Vector3[4];
            bubbles.LeftBubble.GetWorldCorners(corners);
            ((RectTransform)root.transform).GetWorldCorners(screen);
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(screen[0].x), "Not cut off on the left...");
            Assert.That(corners[2].y, Is.LessThanOrEqualTo(screen[2].y), "...nor at the top.");
        }

        [Test]
        public void OnlyTheTwoBesideSeeABubbleAndTheLeftOneStandsAboveItsBadge()
        {
            bubbles.Present(Charm(50, 10d, 5), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing, Is.True, "Seat 4 is on seat 0's left in a ring of five.");
            Assert.That(bubbles.RightShowing, Is.False);
            Assert.That(bubbles.LeftBubble.position.y, Is.GreaterThan(leftBadge.position.y), "It floats above the badge.");

            bubbles.Present(Charm(30, 10d, 5), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing || bubbles.RightShowing, Is.False, "Two seats away: not beside.");
            bubbles.Present(Charm(10, 10d, 5), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing || bubbles.RightShowing, Is.False, "The charmed player sees no bubble of their own.");

            bubbles.Present(Charm(20, 10d, 2), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing && bubbles.RightShowing, Is.True,
                "Two players: the other is beside on both sides, so both badges get a bubble.");

            bubbles.Present(Charm(20, 10d), 10, 1d, 0);
            Assert.That(bubbles.RightShowing, Is.True);
            Assert.That(bubbles.RightBubble.GetComponent<Image>().raycastTarget, Is.False,
                "With wake-up taps off it only shows.");
        }
    }
}
