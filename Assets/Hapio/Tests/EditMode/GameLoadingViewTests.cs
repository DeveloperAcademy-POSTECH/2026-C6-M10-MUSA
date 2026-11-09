using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>로딩 화면 (#478) 1단계: 화면 배치·진행 표시·말풍선·달리는 캐릭터의 규칙과 그림 연결.</summary>
    public sealed class GameLoadingViewTests
    {
        [Test]
        public void BarSitsLowEnoughThatTheBubbleNeverCoversTheCaption()
        {
            const float captionBottom = 0f, gap = 8f, bubble = 20f, tail = 5f, aboveBar = 2f, bar = 16f;
            float center = GameLoadingView.BarCenterBelow(captionBottom, gap, bubble, tail, aboveBar, bar);
            float bubbleTop = center + bar * .5f + aboveBar + tail + bubble;
            Assert.That(bubbleTop, Is.EqualTo(captionBottom - gap).Within(1e-4f), "The bubble's top stays the gap below the caption.");
            Assert.That(GameLoadingView.BarCenterBelow(captionBottom, 20f, bubble, tail, aboveBar, bar), Is.LessThan(center),
                "A bigger gap lowers the bar.");
        }

        [Test]
        public void RunnerCaptionAndBarSitTogetherOnTheScreensMiddle()
        {
            const float runner = 182f, below = 81f;
            float bottom = GameLoadingView.RunnerBottomForCenteredContent(10f, runner, below);
            Assert.That((bottom + runner + bottom - below) * .5f, Is.EqualTo(10f).Within(1e-4f), "Half the block above the center, half below.");
            Assert.That(GameLoadingArt.Load().contentCenterFromTop, Is.EqualTo(.5f), "The saved art centers the block on the screen (#586).");
        }

        [Test]
        public void FillIsARoundDotAtZeroAndTheWholeInsideAtFull()
        {
            const float inner = 257f, height = 11f;
            Assert.That(GameLoadingView.FillWidth(0f, inner, height), Is.EqualTo(height), "A dot, the knob at 0%.");
            Assert.That(GameLoadingView.FillWidth(1f, inner, height), Is.EqualTo(inner));
            Assert.That(GameLoadingView.FillWidth(2f, inner, height), Is.EqualTo(inner), "Never past the end.");
            Assert.That(GameLoadingView.HeadCenter(0f, inner, height), Is.EqualTo(height * .5f), "The tail points at the dot's middle.");
            Assert.That(GameLoadingView.HeadCenter(1f, inner, height), Is.EqualTo(inner - height * .5f));
        }

        [Test]
        public void BubbleShowsWholePercentAndOnlyReachesHundredWhenFull()
        {
            Assert.That(GameLoadingView.PercentLabel(0f), Is.EqualTo("0%"));
            Assert.That(GameLoadingView.PercentLabel(.554f), Is.EqualTo("55%"));
            Assert.That(GameLoadingView.PercentLabel(.999f), Is.EqualTo("99%"), "Not 100% before it is full.");
            Assert.That(GameLoadingView.PercentLabel(1f), Is.EqualTo("100%"));
            Assert.That(GameLoadingView.PercentLabel(1.5f), Is.EqualTo("100%"));
            Assert.That(GameLoadingView.PercentLabel(-1f), Is.EqualTo("0%"));
        }

        [Test]
        public void ShownProgressEasesForwardNeverBackAndNeverTooFast()
        {
            Assert.That(GameLoadingView.NextDisplayed(.6f, .3f, .1f, 6f, .8f), Is.EqualTo(.6f), "Never back.");
            float step = GameLoadingView.NextDisplayed(0f, .5f, .1f, 6f, .8f);
            Assert.That(step, Is.GreaterThan(0f).And.LessThan(.5f), "Eases toward the target.");
            Assert.That(GameLoadingView.NextDisplayed(0f, 1f, .1f, 100f, .8f), Is.EqualTo(.08f).Within(1e-5f),
                "No faster than 0.8 bar widths a second.");
            Assert.That(GameLoadingView.NextDisplayed(.9995f, 1f, .1f, 6f, .8f), Is.EqualTo(1f), "Lands exactly on the target.");
            float shown = 0f;
            for (int frame = 0; frame < 600; frame++) shown = GameLoadingView.NextDisplayed(shown, 1f, 1f / 60f, 6f, .8f);
            Assert.That(shown, Is.EqualTo(1f), "Gets there within ten seconds.");
        }

        [Test]
        public void BubbleLeansBackOnlyWhileMovingUpToItsLimit()
        {
            Assert.That(GameLoadingView.BubbleTiltFor(0f, 60f, 14f), Is.EqualTo(0f), "Upright when still (and at 100%).");
            Assert.That(GameLoadingView.BubbleTiltFor(-1f, 60f, 14f), Is.EqualTo(0f));
            Assert.That(GameLoadingView.BubbleTiltFor(.1f, 60f, 14f), Is.EqualTo(6f).Within(1e-4f));
            Assert.That(GameLoadingView.BubbleTiltFor(5f, 60f, 14f), Is.EqualTo(14f), "Never more than its limit.");
        }

        [Test]
        public void RunnerStridesOncePerSixTenthsOfASecondAndSquashesOnLanding()
        {
            Assert.That(GameLoadingView.FrameAt(0f, .1f, 6), Is.EqualTo(0));
            Assert.That(GameLoadingView.FrameAt(.15f, .1f, 6), Is.EqualTo(1));
            Assert.That(GameLoadingView.FrameAt(.55f, .1f, 6), Is.EqualTo(5));
            Assert.That(GameLoadingView.FrameAt(.65f, .1f, 6), Is.EqualTo(0), "Round again after 0.6 s.");
            var runner = new GameLoadingArt.Runner();
            Assert.That(Enumerable.Range(0, 6).Where(frame => GameLoadingView.IsLanding(runner, frame)), Is.EqualTo(new[] { 2, 5 }),
                "Feet down in pictures 3 and 6.");
            Assert.That(GameLoadingView.LandingSquash(0f, .08f, .04f), Is.EqualTo(.04f));
            Assert.That(GameLoadingView.LandingSquash(.04f, .08f, .04f), Is.EqualTo(.02f).Within(1e-5f));
            Assert.That(GameLoadingView.LandingSquash(.08f, .08f, .04f), Is.EqualTo(0f));
            Assert.That(GameLoadingView.LandingSquash(-1f, .08f, .04f), Is.EqualTo(0f), "Not before it lands.");
        }

        [Test]
        public void RunnerIsDrawnAtRandomFromThoseWithPictures()
        {
            Assert.That(GameLoadingView.PickRunner(null, new System.Random(1)), Is.Null);
            Assert.That(GameLoadingView.PickRunner(new[] { new GameLoadingArt.Runner() }, new System.Random(1)), Is.Null,
                "One without pictures is never drawn.");
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.one * .5f);
            try
            {
                var a = new GameLoadingArt.Runner { name = "a", frames = new[] { sprite } };
                var b = new GameLoadingArt.Runner { name = "b", frames = new[] { sprite } };
                var empty = new GameLoadingArt.Runner { name = "empty" };
                var drawn = Enumerable.Range(0, 200).Select(seed => GameLoadingView.PickRunner(new[] { a, empty, b },
                    new System.Random(seed)).name).Distinct().OrderBy(name => name).ToArray();
                Assert.That(drawn, Is.EqualTo(new[] { "a", "b" }), "Both are drawn sometimes, never the empty one.");
            }
            finally { Object.DestroyImmediate(sprite); }
        }

        [Test]
        public void PaperFillsTheScreenWithoutStretching()
        {
            Rect tall = GameLoadingView.CoverUv(new Vector2(390f, 844f), 1f);
            Assert.That(tall.height, Is.EqualTo(1f));
            Assert.That(tall.width, Is.EqualTo(390f / 844f).Within(1e-4f));
            Assert.That(tall.x, Is.EqualTo((1f - tall.width) * .5f).Within(1e-4f), "Centered.");
            Rect wide = GameLoadingView.CoverUv(new Vector2(844f, 390f), 1f);
            Assert.That(wide.width, Is.EqualTo(1f));
            Assert.That(wide.height, Is.EqualTo(390f / 844f).Within(1e-4f));
        }

        [Test]
        public void LoadingArtHoldsEachRunnersSixStridePicturesPaperAndMockupColors()
        {
            GameLoadingArt art = GameLoadingArt.Load();
            Assert.That(art, Is.Not.Null, GameLoadingArt.ResourcePath);
            Assert.That(art.runners.Select(runner => runner.name), Is.EqualTo(new[] { "불이", "묵이", "솔이", "금옥이", "만복이" }));
            foreach (GameLoadingArt.Runner runner in art.runners)
            {
                Assert.That((runner.frames.Length, runner.Usable), Is.EqualTo((6, true)), runner.name);
                Assert.That(runner.frames.Select(frame => frame.rect.size).Distinct().Count(), Is.EqualTo(1),
                    runner.name + ": one crop for all six, so they keep their places.");
                Assert.That(runner.frameSeconds * runner.frames.Length, Is.EqualTo(.84f).Within(1e-4f),
                    runner.name + ": one stride every 0.84 s (slowed from 0.6 s).");
            }
            Assert.That(art.runners[0].landingFrames, Is.EqualTo(new[] { 2, 5 }), "불이 lands in pictures 3 and 6.");
            Assert.That(art.runners[1].landingFrames, Is.EqualTo(new[] { 1, 4 }), "묵이 lands in pictures 2 and 5.");
            Assert.That(art.runners[2].landingFrames, Is.EqualTo(new[] { 1, 4 }), "솔이 lands in pictures 2 and 5.");
            GameLoadingArt.Runner geumok = art.runners[3];
            Assert.That(geumok.frames.Select(frame => frame.name),
                Is.EqualTo(Enumerable.Range(1, 6).Select(number => $"GeumokRun{number:D2}")));
            Assert.That(geumok.landingFrames, Is.EqualTo(new[] { 1, 4 }), "금옥이 lands in pictures 2 and 5.");
            GameLoadingArt.Runner manbok = art.runners[4];
            Assert.That(manbok.frames.Select(frame => frame.name),
                Is.EqualTo(Enumerable.Range(1, 6).Select(number => $"ManbokRun{number:D2}")));
            Assert.That(manbok.landingFrames, Is.EqualTo(new[] { 1, 4 }), "만복이 lands in pictures 2 and 5.");
            string[] drawn = Enumerable.Range(0, 200).Select(seed => GameLoadingView.PickRunner(art.runners, new System.Random(seed)).name)
                .Distinct().OrderBy(name => name).ToArray();
            Assert.That(drawn, Is.EquivalentTo(art.runners.Select(runner => runner.name)), "Every one of them comes up at random.");
            Assert.That(art.paperTexture, Is.Not.Null, "The hanji grain.");
            Assert.That(art.captionFont != null ? art.captionFont.name : null, Is.EqualTo("ShillaCultureBold"));
            Assert.That(art.caption, Is.EqualTo("로딩중"));
            AssertColor(art.backgroundColor, 0xF9, 0xE5, 0xC9, "background");
            AssertColor(art.bubbleColor, 0x91, 0x67, 0x45, "bubble");
            AssertColor(art.fillColor, 0x6C, 0x46, 0x26, "fill");
            AssertColor(art.trackColor, 0xE3, 0xD8, 0xCA, "track");
        }

        private static void AssertColor(Color color, int r, int g, int b, string what)
        {
            Color32 c = color;
            Assert.That(Mathf.Abs(c.r - r) + Mathf.Abs(c.g - g) + Mathf.Abs(c.b - b), Is.LessThanOrEqualTo(6), what + " " + c);
        }
    }
}
