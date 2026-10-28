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
            LabGuideText.ForgetShownOnce(); // Each test starts as a fresh app would.
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

        /// <summary>
        /// Players 10, 20, 30... in seats 0, 1, 2...; a charm on one of them until the given time, or none. taps is how
        /// often player 10 has tapped as one of the two beside (#305), other how often the other player beside has.
        /// </summary>
        private static LabSnapshot Charm(ulong charmed, double until, int players = 3, int taps = 0, int other = 0)
        {
            var seats = new LabPlayerState[players];
            for (int i = 0; i < players; i++) seats[i] = new LabPlayerState { id = (ulong)(10 * (i + 1)), seat = i };
            var snapshot = new LabSnapshot
            {
                sessionId = "charm-bubbles", round = 1, phase = LabPhase.Playing, serverTime = 1d, players = seats,
                charmedPlayer = until > 0d ? charmed : 0, charmStartedAt = until > 0d ? 0d : -1d, charmUntil = until,
                charmWarnedAt = until > 0d ? 0d : -1d
            };
            int seat = (int)(charmed / 10) - 1;
            if (until > 0d && seat >= 0 && seat < players)
            {
                ulong before = (ulong)(10 * ((seat + players - 1) % players + 1)), after = (ulong)(10 * ((seat + 1) % players + 1));
                snapshot.charmHelpers = before == after ? new[] { before } : new[] { before, after };
                snapshot.charmHelperTaps = System.Array.ConvertAll(snapshot.charmHelpers, id => id == 10 ? taps : other);
                snapshot.charmTaps = taps + other;
            }
            return snapshot;
        }

        [UnityTest]
        public IEnumerator TheBubbleBesideTheCharmedPlayerTakesTapsAndTurnsSurprisedWhenWoken()
        {
            // Player 10 in seat 0 has seat 1 (20) on the left and seat 2 (30) on the right.
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing, Is.True, "The charmed player beside on the left gets a bubble.");
            Assert.That(bubbles.RightShowing, Is.False);
            Image art = bubbles.LeftBubble.GetComponent<Image>();
            Assert.That(art.sprite, Is.Not.Null, "The heart-eyed bubble art.");
            Assert.That(art.raycastTarget, Is.True, "It can be tapped.");
            Assert.That(bubbles.LeftBubble.position.y, Is.GreaterThan(leftBadge.position.y),
                "The left one floats above its badge, like the right one (the drum minimap now sits higher).");

            // A frame first, so the swell below is timed by an ordinary frame rather than the test's first one.
            yield return null;
            bubbles.Present(Charm(20, 10d), 10, 1.01d, 3);
            bubbles.LeftBubble.GetComponent<Button>().onClick.Invoke();
            Assert.That(bubbles.TapsSent, Is.EqualTo(1));
            Assert.That(bubbles.Pulsing(false), Is.True);
            yield return null;
            bubbles.Present(Charm(20, 10d), 10, 1.02d, 3);
            Assert.That(bubbles.LeftBubble.localScale.x, Is.GreaterThan(1f), "Each tap swells it a little...");
            float until = Time.time + .4f;
            while (bubbles.Pulsing(false) && Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(20, 10d), 10, 1.1d, 3);
            }
            Assert.That(bubbles.LeftBubble.localScale.x, Is.EqualTo(1f).Within(.0001f), "...and back.");

            // Those beside woke them: the spiky surprised bubble pops out in its place.
            bubbles.ShowWoken(20);
            bubbles.Present(Charm(0, -1d), 10, 2d, 3);
            Assert.That(bubbles.LeftShowing, Is.False);
            Assert.That(bubbles.LeftWokenShowing, Is.True, "Woken by taps: the surprised bubble.");
            Assert.That(bubbles.LeftFading, Is.False);
            until = Time.time + 1.5f;
            while (bubbles.LeftWokenShowing && Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(0, -1d), 10, 2d, 3);
            }
            Assert.That(bubbles.LeftWokenShowing, Is.False, "It is gone in a moment.");
        }

        [UnityTest]
        public IEnumerator ACharmThatRunsOutOnlyFadesItsBubbleAway()
        {
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            yield return null;
            bubbles.Present(Charm(0, -1d), 10, 10.1d, 3);
            Assert.That(bubbles.LeftFading, Is.True, "Run out by itself: the heart bubble fades where it is...");
            Assert.That(bubbles.LeftWokenShowing, Is.False, "...with no surprised bubble.");
            float first = bubbles.LeftBubble.GetComponent<Image>().color.a;
            float until = Time.time + 1f;
            while (bubbles.LeftFading && Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(0, -1d), 10, 10.2d, 3);
                if (bubbles.LeftFading)
                    Assert.That(bubbles.LeftBubble.GetComponent<Image>().color.a, Is.LessThanOrEqualTo(first));
            }
            Assert.That(bubbles.LeftFading || bubbles.LeftBubble.gameObject.activeSelf, Is.False, "Then it is gone.");

            // A woken notice that comes just after the snapshot still turns the fading bubble surprised.
            bubbles.Present(Charm(30, 20d), 10, 11d, 3);
            yield return null;
            bubbles.Present(Charm(0, -1d), 10, 11.5d, 3);
            Assert.That(bubbles.RightFading, Is.True);
            bubbles.ShowWoken(30);
            bubbles.Present(Charm(0, -1d), 10, 11.6d, 3);
            Assert.That(bubbles.RightWokenShowing, Is.True);
        }

        [UnityTest]
        public IEnumerator TheBubbleFillsWithYellowAsTheWakeUpTapsAddUp()
        {
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            Assert.That(bubbles.LeftWakeFill, Is.Zero, "Empty before anyone taps.");
            bubbles.LeftBubble.GetComponent<Button>().onClick.Invoke();
            bubbles.Present(Charm(20, 10d), 10, 1.01d, 3);
            Assert.That(bubbles.LeftWakeFill, Is.EqualTo(1f / 3f).Within(1e-5f), "This screen's own tap shows at once.");
            bubbles.Present(Charm(20, 10d, taps: 2), 10, 1.1d, 3);
            Assert.That(bubbles.LeftWakeFill, Is.EqualTo(2f / 3f).Within(1e-5f), "The Host's count of this screen's taps.");
            bubbles.Present(Charm(20, 10d, taps: 2, other: 3), 10, 1.1d, 3);
            Assert.That(bubbles.LeftWakeFill, Is.EqualTo(2f / 3f).Within(1e-5f),
                "Each player beside fills their own gauge; the other one's taps are not added (#305).");
            RectTransform fill = bubbles.LeftBubble.Find("Charm Wake Fill") as RectTransform;
            Assert.That(fill, Is.Not.Null);
            Assert.That(fill.GetComponent<Image>().color.b, Is.LessThan(fill.GetComponent<Image>().color.g), "A light yellow.");
            Assert.That(bubbles.LeftBubble.GetComponent<Mask>(), Is.Not.Null, "It stays inside the bubble's shape.");
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

        [UnityTest]
        public IEnumerator TheFaceStaysAboveTheGaugeAndACheckWaitsForTheOtherSide()
        {
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            Transform fill = bubbles.LeftBubble.Find("Charm Wake Fill"), face = bubbles.LeftBubble.Find("Charm Face");
            Assert.That(face, Is.Not.Null, "The heart-eyed face is its own picture (#305)...");
            Assert.That(face.GetSiblingIndex(), Is.GreaterThan(fill.GetSiblingIndex()), "...drawn over the yellow gauge.");
            Assert.That(face.GetComponent<Image>().sprite, Is.SameAs(Resources.Load<Sprite>(LabCharmBubbles.FacePath)));
            Assert.That(bubbles.LeftCheckShowing, Is.False);

            for (int i = 0; i < 3; i++) bubbles.LeftBubble.GetComponent<Button>().onClick.Invoke();
            yield return null;
            bubbles.Present(Charm(20, 10d, taps: 3), 10, 1.2d, 3);
            Assert.That(bubbles.LeftWakeFill, Is.EqualTo(1f));
            Assert.That(bubbles.LeftCheckShowing, Is.True, "Done: a check over the bubble while the other side taps.");
            Assert.That(bubbles.LeftShowing, Is.True, "Still charmed until both are done.");
            Assert.That(bubbles.LeftBubble.GetComponent<Image>().raycastTarget, Is.False, "More taps would not count.");
            // The check pops in from small; measure where it settles.
            float settle = Time.time + .4f;
            while (Time.time < settle)
            {
                yield return null;
                bubbles.Present(Charm(20, 10d, taps: 3), 10, 1.3d, 3);
            }
            var check = new Vector3[4];
            var cloud = new Vector3[4];
            GameObject.Find("Charm Check Left").GetComponent<RectTransform>().GetWorldCorners(check);
            bubbles.LeftBubble.GetWorldCorners(cloud);
            Assert.That(check[1].y, Is.GreaterThan(cloud[1].y), "It sits on top of the bubble...");
            Assert.That(check[0].y, Is.LessThan(cloud[1].y), "...overlapping its top a little.");

            // The other side finished too: both of them see the surprised bubble at once.
            bubbles.ShowWoken(20);
            bubbles.Present(Charm(0, -1d), 10, 1.3d, 3);
            Assert.That(bubbles.LeftWokenShowing, Is.True);
            Assert.That(bubbles.LeftCheckShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator TheCharmedPlayerSeesEachSideFillAndCheckButNoSurprise()
        {
            // Player 10 is charmed: 30 beside on the right, 20 on the left.
            var snapshot = Charm(10, 10d);
            snapshot.charmHelperTaps = new[] { 1, 3 };
            bubbles.Present(snapshot, 10, 1d, 3);
            float until = Time.time + 1f;
            while (Time.time < until && bubbles.RightWakeFill < 1f / 3f) { yield return null; bubbles.Present(snapshot, 10, 1.1d, 3); }
            Assert.That(bubbles.RightWakeFill, Is.EqualTo(1f / 3f).Within(1e-5f), "Player 30's own share...");
            Assert.That(bubbles.LeftWakeFill, Is.EqualTo(1f), "...and player 20's, each by their badge.");
            Assert.That(bubbles.LeftCheckShowing, Is.True, "Player 20 is done.");
            Assert.That(bubbles.RightCheckShowing, Is.False);
            bubbles.LeftBubble.GetComponent<Button>().onClick.Invoke();
            Assert.That(bubbles.TapsSent, Is.Zero, "The charmed player cannot tap themselves awake.");

            bubbles.ShowWoken(10);
            bubbles.Present(Charm(0, -1d), 10, 2d, 3);
            Assert.That(bubbles.RightWokenShowing || bubbles.LeftWokenShowing, Is.False, "Their own screen says 정신 차림! instead.");
            Assert.That(bubbles.RightFading && bubbles.LeftFading, Is.True, "The progress bubbles just fade.");
        }

        [UnityTest]
        public IEnumerator TheOwnCheckAndTheEndOfACharmAreEachAnnouncedOncePerCharm()
        {
            int checks = 0, ended = 0;
            bubbles.OwnCheckShown += () => checks++;
            bubbles.HelperCharmEnded += () => ended++;
            // Two players: the other one is beside on both sides, so both badges show the same charm (#351).
            bubbles.Present(Charm(20, 10d, players: 2), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing && bubbles.RightShowing, Is.True);
            Assert.That(checks + ended, Is.Zero);
            yield return null;
            bubbles.Present(Charm(20, 10d, players: 2, taps: 3), 10, 1.1d, 3);
            Assert.That(bubbles.LeftCheckShowing && bubbles.RightCheckShowing, Is.True);
            Assert.That(checks, Is.EqualTo(1), "One check sound for this screen's share, though both badges pop a check.");

            // A pause hides the bubbles; coming back draws the check again without another sound.
            LabSnapshot paused = Charm(20, 10d, players: 2, taps: 3);
            paused.paused = true;
            bubbles.Present(paused, 10, 1.2d, 3);
            yield return null;
            bubbles.Present(Charm(20, 10d, players: 2, taps: 3), 10, 1.3d, 3);
            Assert.That(bubbles.LeftCheckShowing, Is.True);
            Assert.That(checks, Is.EqualTo(1));
            Assert.That(ended, Is.Zero, "Still charmed.");

            // It ends (a snapshot says -1 once a charm is over): announced once for both badges.
            bubbles.Present(Charm(0, -1d, players: 2), 10, 1.4d, 3);
            bubbles.Present(Charm(0, -1d, players: 2), 10, 1.5d, 3);
            Assert.That(ended, Is.EqualTo(1));

            // The next charm comes later and is announced again.
            LabSnapshot next = Charm(20, 30d, players: 2, taps: 3);
            next.charmStartedAt = next.charmWarnedAt = 20d;
            bubbles.Present(next, 10, 21d, 3);
            Assert.That(checks, Is.EqualTo(2));
            bubbles.Present(Charm(0, -1d, players: 2), 10, 31d, 3);
            Assert.That(ended, Is.EqualTo(2));

            // On the charmed player's own screen the other's check and the end are not this screen's to announce.
            LabSnapshot own = Charm(10, 50d, players: 2, other: 3);
            own.charmStartedAt = own.charmWarnedAt = 40d;
            bubbles.Present(own, 10, 41d, 3);
            Assert.That(bubbles.LeftCheckShowing, Is.True, "The charmed player sees the other's check...");
            bubbles.Present(Charm(0, -1d, players: 2), 10, 51d, 3);
            Assert.That(checks, Is.EqualTo(2), "...but it is not their own share.");
            Assert.That(ended, Is.EqualTo(2), "Their own charm's end is LabCharmHud's to announce.");
        }

        [UnityTest]
        public IEnumerator AHandPokesAtTheBubbleEveryTimeButTheGuideShowsOnlyOnTheFirstCharm()
        {
            // Player 10 beside the charmed 20 (on the left) still has to tap (#328).
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            Assert.That(bubbles.LeftHandShowing, Is.True, "A hand points at the bubble to tap...");
            Assert.That(bubbles.RightHandShowing, Is.False);
            Image hand = bubbles.LeftHand.GetComponent<Image>();
            Assert.That(hand.sprite, Is.SameAs(Resources.Load<Sprite>(LabCharmBubbles.LeftHandPath)), "...pointing left at it...");
            Assert.That(hand.raycastTarget, Is.False, "...without catching the taps.");
            Assert.That(bubbles.LeftHand.position.x, Is.GreaterThan(bubbles.LeftBubble.position.x),
                "It stands on the bubble's inner side.");
            Assert.That(bubbles.LeftHand.sizeDelta.x, Is.EqualTo(bubbles.LeftBubble.sizeDelta.x * .675f).Within(.01f),
                "Three quarters of its first size.");
            Assert.That(bubbles.LeftHand.localEulerAngles.z, Is.EqualTo(20f).Within(.01f),
                "Tilted counterclockwise, so the left-pointing finger points down at the bubble...");
            Assert.That(bubbles.RightHand.localEulerAngles.z, Is.EqualTo(340f).Within(.01f), "...and the right one mirrors it.");
            Assert.That(bubbles.GuideShowing, Is.True);
            Assert.That(bubbles.GuideShown, Is.EqualTo(LabCharmBubbles.HelperGuideText));

            float least = float.MaxValue, most = float.MinValue, until = Time.time + 1f;
            while (Time.time < until)
            {
                yield return null;
                bubbles.Present(Charm(20, 10d), 10, 1.1d, 3);
                least = Mathf.Min(least, bubbles.LeftHand.anchoredPosition.x);
                most = Mathf.Max(most, bubbles.LeftHand.anchoredPosition.x);
            }
            Assert.That(most - least, Is.GreaterThan(3f), "It pokes at the bubble.");

            for (int i = 0; i < 3; i++) bubbles.LeftBubble.GetComponent<Button>().onClick.Invoke();
            yield return null;
            bubbles.Present(Charm(20, 10d, taps: 3), 10, 1.2d, 3);
            Assert.That(bubbles.LeftCheckShowing, Is.True);
            Assert.That(bubbles.LeftHandShowing, Is.False, "Done: the hand goes as the check comes...");
            Assert.That(bubbles.GuideShowing, Is.False, "...and so does the guide.");

            // The next charm beside this player: the hand pokes again, but the guide is not repeated.
            LabSnapshot next = Charm(20, 30d);
            next.charmStartedAt = 15d;
            bubbles.Present(next, 10, 16d, 3);
            Assert.That(bubbles.LeftHandShowing, Is.True, "The hand shows on every charm...");
            Assert.That(bubbles.GuideShowing, Is.False, "...the guide only on the first (#328).");
            Assert.That(LabGuideText.ShownOnce(LabCharmBubbles.HelperGuideOnceKey), Is.True);

            // The charmed player's own screen only shows each side's progress: no hand, no guide.
            bubbles.Present(Charm(10, 10d), 10, 1.3d, 3);
            Assert.That(bubbles.LeftHandShowing || bubbles.RightHandShowing, Is.False);
            Assert.That(bubbles.GuideShowing, Is.False);
        }

        [Test]
        public void ABubbleByABadgeAtTheScreensCornerStillShowsWhole()
        {
            Rect canvas = ((RectTransform)root.transform).rect;
            leftBadge.anchoredPosition = new Vector2(canvas.xMin + 20f, canvas.yMax - 20f);
            bubbles.Present(Charm(20, 10d), 10, 1d, 3);
            Assert.That(bubbles.LeftShowing, Is.True);
            var corners = new Vector3[4];
            var screen = new Vector3[4];
            bubbles.LeftBubble.GetWorldCorners(corners);
            ((RectTransform)root.transform).GetWorldCorners(screen);
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(screen[0].x), "Not cut off on the left...");
            Assert.That(corners[2].y, Is.LessThanOrEqualTo(screen[2].y), "...nor at the top.");
        }

        [Test]
        public void OnlyTheTwoBesideSeeABubbleAndTheRightOneStandsAboveItsBadge()
        {
            bubbles.Present(Charm(50, 10d, 5), 10, 1d, 3);
            Assert.That(bubbles.RightShowing, Is.True, "Seat 4 is on seat 0's right in a ring of five.");
            Assert.That(bubbles.LeftShowing, Is.False);
            Assert.That(bubbles.RightBubble.position.y, Is.GreaterThan(rightBadge.position.y), "It floats above the badge.");

            bubbles.Present(Charm(30, 10d, 5), 10, 1d, 3);
            Assert.That(bubbles.RightShowing || bubbles.LeftShowing, Is.False, "Two seats away: not beside.");
            bubbles.Present(Charm(10, 10d, 5), 10, 1d, 3);
            Assert.That(bubbles.RightShowing && bubbles.LeftShowing, Is.True,
                "The charmed player sees how far each player beside has come (#305)...");
            Assert.That(bubbles.OwnView(false) && bubbles.OwnView(true), Is.True);
            Assert.That(bubbles.RightBubble.GetComponent<Image>().raycastTarget, Is.False, "...but cannot tap them.");
            bubbles.Present(Charm(0, -1d, 5), 10, 1d, 3);

            bubbles.Present(Charm(20, 10d, 2), 10, 1d, 3);
            Assert.That(bubbles.RightShowing && bubbles.LeftShowing, Is.True,
                "Two players: the other is beside on both sides, so both badges get a bubble.");
            bubbles.Present(Charm(0, -1d, 2), 10, 1d, 3);

            bubbles.Present(Charm(20, 10d), 10, 1d, 0);
            Assert.That(bubbles.LeftShowing, Is.True);
            Assert.That(bubbles.LeftBubble.GetComponent<Image>().raycastTarget, Is.False,
                "With wake-up taps off it only shows.");
        }
    }

    /// <summary>The saved Lab layout must leave its drum minimap readable during a neighbour's charm.</summary>
    public sealed class LabCharmSavedSceneTests
    {
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();

        [UnityTearDown]
        public IEnumerator UnloadSavedScene() => fixture.Cleanup();

        [UnityTest]
        public IEnumerator SavedSceneCharmBubbleLeavesTheDrumMinimapVisible()
        {
            yield return fixture.Load();
            LabHud hud = Object.FindFirstObjectByType<LabHud>();
            LabSceneController controller = Object.FindFirstObjectByType<LabSceneController>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            LabDrumMinimap minimap = controller.GetComponent<LabDrumMinimap>();
            LabCharmBubbles bubbles = controller.GetComponent<LabCharmBubbles>();
            Assert.That(minimap, Is.Not.Null, "Use the minimap saved in Lab.unity, including its serialized spacing.");
            Assert.That(bubbles, Is.Not.Null, "The saved Scene's controller must attach the charm presentation.");

            // Keep the saved hierarchy and anchors, presenting a two-player snapshot without opening a room.
            // The other player is on both sides in this case, including beside the drum minimap.
            hud.enabled = minimap.enabled = bubbles.enabled = false;
            hud.battleUi.SetActive(true);
            hud.rightNeighborBadge.SetActive(true);
            hud.leftNeighborBadge.SetActive(true);
            var snapshot = new LabSnapshot
            {
                sessionId = "saved-charm-layout", round = 1, phase = LabPhase.Playing, serverTime = 1d,
                players = new[]
                {
                    new LabPlayerState { id = 10, seat = 0 },
                    new LabPlayerState { id = 20, seat = 1 }
                },
                charmedPlayer = 20, charmStartedAt = 0d, charmUntil = 10d
            };
            minimap.Rebuild(snapshot.players, 10);
            Assert.That(minimap.Panel, Is.Not.Null);
            minimap.Panel.localScale = Vector3.one * (hud.network.config.MinimapSizePercent / 100f);
            minimap.Panel.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            bubbles.Present(snapshot, 10, 1d, 3);
            Canvas.ForceUpdateCanvases();

            Assert.That(bubbles.RightShowing && bubbles.LeftShowing, Is.True);
            Assert.That(WorldRect(bubbles.RightBubble).Overlaps(WorldRect(minimap.Panel)), Is.False,
                "The right charm bubble must not cover the saved drum minimap; updating a field default alone "
                + "does not migrate the minimap's serialized gap in Lab.unity.");
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
