using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>
    /// Fever time (#334) on screen: the "두배" bubble out of a puff above the stamina bar's right end, the start banner,
    /// the red breathing screen edges and the time left in red.
    /// </summary>
    public sealed class LabFeverTimeHudTests
    {
        private GameObject root;
        private RectTransform bar, battle;
        private Text timer;
        private LabFeverTimeHud hud;
        private readonly Vector3[] corners = new Vector3[4];

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Fever test canvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            bar = new GameObject("Stamina bar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(root.transform, false);
            bar.sizeDelta = new Vector2(223f, 44f);
            bar.anchoredPosition = new Vector2(0f, -200f);
            battle = new GameObject("Battle UI", typeof(RectTransform)).GetComponent<RectTransform>();
            battle.SetParent(root.transform, false);
            bar.SetParent(battle, false);
            timer = new GameObject("Time", typeof(RectTransform)).AddComponent<Text>();
            timer.transform.SetParent(battle, false);
            timer.color = Color.white;
            var host = new GameObject("Fever HUD");
            host.transform.SetParent(root.transform, false);
            hud = host.AddComponent<LabFeverTimeHud>();
            hud.Bind(null, root.GetComponent<Canvas>(), bar, battle, timer);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
        }

        private static LabSnapshot Battle(double feverTimeAt, LabPhase phase = LabPhase.Playing, bool paused = false) =>
            new LabSnapshot { sessionId = "fever-hud", round = 1, phase = phase, paused = paused, feverTimeAt = feverTimeAt };

        [UnityTest]
        public IEnumerator TheBubbleComesOutOfAPuffAtTheStartAndKeepsBreathing()
        {
            hud.Present(Battle(10d), 9.9d);
            Assert.That(hud.Showing || hud.SmokeShowing, Is.False, "Nothing before fever time.");

            hud.Present(Battle(10d), 10d);
            Assert.That(hud.SmokeShowing, Is.True, "It starts with a puff of smoke...");
            Assert.That(hud.Showing, Is.False, "...the bubble a moment later, not out of nowhere.");
            Image art = hud.Bubble.GetComponent<Image>();
            Assert.That(art.sprite, Is.SameAs(Resources.Load<Sprite>(LabFeverTimeHud.DoublePath)), "The supplied 두배 art.");
            Assert.That(art.raycastTarget, Is.False, "It never catches a touch.");

            float until = Time.time + .5f;
            while (Time.time < until) { yield return null; hud.Present(Battle(10d), 10.2d); }
            Assert.That(hud.Showing, Is.True);
            bar.GetWorldCorners(corners);
            Vector3 center = hud.Bubble.position;
            Assert.That(center.y, Is.GreaterThan(corners[1].y), "Above the stamina bar...");
            Assert.That(Mathf.Abs(center.x - corners[2].x), Is.LessThan(hud.Bubble.sizeDelta.x), "...at its right end.");
            Assert.That(hud.Bubble.sizeDelta.y, Is.EqualTo(44f * 1.3f).Within(.5f), "About 1.3 times the bar's height.");

            float least = float.MaxValue, most = float.MinValue;
            until = Time.time + 1.2f;
            while (Time.time < until)
            {
                yield return null;
                hud.Present(Battle(10d), 11d);
                least = Mathf.Min(least, hud.Scale);
                most = Mathf.Max(most, hud.Scale);
            }
            Assert.That(hud.SmokeShowing, Is.False, "The puff has cleared.");
            Assert.That(least, Is.LessThan(.95f), "It shrinks...");
            Assert.That(most, Is.GreaterThan(1.05f), "...and swells, over and over.");
            Assert.That(least, Is.GreaterThanOrEqualTo(.89f));
            Assert.That(most, Is.LessThanOrEqualTo(1.11f));

            hud.Present(Battle(10d, LabPhase.Victory), 12d);
            Assert.That(hud.Showing, Is.False, "Gone once the battle is over.");
        }

        [UnityTest]
        public IEnumerator TheStartBannerPopsAndTheEdgesGlowRedWithTheTimeLeftInRed()
        {
            hud.Present(Battle(10d), 9.9d);
            Assert.That(hud.BannerShowing, Is.False);
            Assert.That(hud.GlowAlpha, Is.Zero);
            Assert.That(timer.color, Is.EqualTo(Color.white));

            hud.Present(Battle(10d), 10d);
            Assert.That(hud.BannerShowing, Is.True, "The start is announced...");
            Assert.That(hud.BannerShown, Is.EqualTo(LabFeverTimeHud.BannerTitle + "\n" + LabFeverTimeHud.BannerLine));
            Assert.That(LabFeverTimeHud.BannerLine, Is.EqualTo("시간이 없어! 마구 던져버리자!!"));
            Assert.That(timer.color, Is.EqualTo(LabFeverTimeHud.FeverColor), "...the time left turns red...");

            float least = float.MaxValue, most = 0f;
            float until = Time.time + 1.2f;
            while (Time.time < until)
            {
                yield return null;
                hud.Present(Battle(10d), 10.5d);
                if (Time.time > until - .8f) { least = Mathf.Min(least, hud.GlowAlpha); most = Mathf.Max(most, hud.GlowAlpha); }
            }
            Assert.That(least, Is.GreaterThan(.3f), "...and the screen edges glow...");
            Assert.That(most - least, Is.GreaterThan(.1f), "...breathing in and out...");
            Color glow = hud.GlowColor;
            Assert.That(glow.r, Is.GreaterThan(glow.g + .5f), "...in a deep pinkish red.");
            Transform edge = battle.Find("Fever Time Edge Glow");
            Assert.That(edge, Is.Not.Null);
            Assert.That(edge.GetSiblingIndex(), Is.Zero, "It lies under every other battle display.");
            Assert.That(edge.GetComponent<Image>().raycastTarget, Is.False);

            until = Time.time + 1.2f;
            while (hud.BannerShowing && Time.time < until) { yield return null; hud.Present(Battle(10d), 11d); }
            Assert.That(hud.BannerShowing, Is.False, "The banner goes after about two seconds; the glow stays.");
            Assert.That(hud.GlowAlpha, Is.GreaterThan(.3f));

            hud.Present(Battle(10d, LabPhase.Victory), 12d);
            Assert.That(timer.color, Is.EqualTo(Color.white), "The time left goes back to its colour after the battle.");
            until = Time.time + 1f;
            while (hud.GlowAlpha > 0f && Time.time < until) { yield return null; hud.Present(Battle(10d, LabPhase.Victory), 12d); }
            Assert.That(hud.GlowAlpha, Is.Zero, "The glow fades away.");
        }

        [Test]
        public void ALateViewSkipsThePuffAndOnlyAFeverRoundShowsIt()
        {
            hud.Present(Battle(-1d), 100d);
            Assert.That(hud.Showing || hud.SmokeShowing, Is.False, "A round without fever time never shows it.");

            hud.Present(Battle(10d), 15d);
            Assert.That(hud.Showing, Is.True, "Seen five seconds late (a reconnect), it is simply there...");
            Assert.That(hud.SmokeShowing, Is.False, "...with no puff...");
            Assert.That(hud.BannerShowing, Is.False, "...and no start banner.");

            hud.Present(Battle(10d, paused: true), 16d);
            Assert.That(hud.Showing, Is.False, "Hidden while the battle is paused.");
            hud.Present(Battle(10d), 16d);
            Assert.That(hud.Showing, Is.True);
            Assert.That(hud.SmokeShowing, Is.False, "Resuming does not puff again.");
        }
    }
}
