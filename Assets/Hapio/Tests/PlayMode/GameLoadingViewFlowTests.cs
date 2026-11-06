using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>로딩 화면 (#478) 1단계: 실제로 띄워 진행을 채워 보고, 말풍선·캐릭터가 규칙대로 움직이는지.</summary>
    public sealed class GameLoadingViewFlowTests
    {
        private GameObject holder;

        [TearDown]
        public void Cleanup()
        {
            if (holder != null) Object.Destroy(holder);
            holder = null;
        }

        [UnityTest]
        public IEnumerator FillsToHundredWithTheBubbleFollowingThenTheRunnerLandsAndStands()
        {
            holder = new GameObject("Loading view test");
            GameLoadingView view = GameLoadingView.Create(holder.transform, null, new System.Random(7));
            Assert.That(view.Visible, Is.False, "Made hidden.");
            view.Show(0f);
            Assert.That(view.Visible, Is.True);
            Assert.That((view.PercentText, view.RunnerName, view.RunnerFrame), Is.EqualTo(("0%", "불이", 0)),
                "The first picture is up at once, no blank frame.");
            yield return null;
            AssertBubbleClearOfCaptionAndOnTheBar(view);

            view.SetProgress(.5f);
            float leaned = 0f;
            float until = Time.realtimeSinceStartup + 3f;
            while (view.DisplayedProgress < .5f && Time.realtimeSinceStartup < until)
            {
                leaned = Mathf.Max(leaned, view.BubbleTilt);
                yield return null;
            }
            Assert.That(view.DisplayedProgress, Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(view.PercentText, Is.EqualTo("50%"));
            Assert.That(leaned, Is.GreaterThan(1f), "The bubble leaned back while it moved.");
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(view.BubbleTilt, Is.LessThan(.5f), "And stood up again once still.");
            AssertBubbleClearOfCaptionAndOnTheBar(view);

            view.SetProgress(.2f);
            Assert.That(view.TargetProgress, Is.EqualTo(.5f), "Progress never goes back.");

            view.SetProgress(1f);
            until = Time.realtimeSinceStartup + 4f;
            while (!view.RunnerResting && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(view.PercentText, Is.EqualTo("100%"));
            Assert.That(view.RunnerResting, Is.True, "At 100% the runner lands and stands.");
            GameLoadingArt art = GameLoadingArt.Load();
            Assert.That(GameLoadingView.IsLanding(art.runners[0], view.RunnerFrame), Is.True, "On a landing picture.");
            int standing = view.RunnerFrame;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.RunnerFrame, Is.EqualTo(standing), "Stays standing.");
            Assert.That(view.BubbleTilt, Is.EqualTo(0f).Within(.05f), "Upright at 100%.");

            view.Hide();
            Assert.That(view.Visible, Is.False);
        }

        [UnityTest]
        public IEnumerator PreviewRunsFromZeroToHundredAndCloses()
        {
            holder = new GameObject("Loading preview test");
            GameLoadingView view = GameLoadingView.Create(holder.transform);
            view.PlayPreview(1f);
            Assert.That(view.Visible, Is.True);
            float until = Time.realtimeSinceStartup + 8f;
            while (view.Visible && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(view.Visible, Is.False, "Closes after reaching 100% and a short hold.");
            Assert.That(view.DisplayedProgress, Is.EqualTo(1f));
        }

        private static void AssertBubbleClearOfCaptionAndOnTheBar(GameLoadingView view)
        {
            Rect caption = ScreenRect(view.Caption), bar = ScreenRect(view.Bar);
            var corners = new Vector3[4];
            view.Bubble.GetWorldCorners(corners);
            float bubbleTop = Math.Max(corners[1].y, corners[2].y), tip = view.Bubble.position.y;
            // Even leaning back, the bubble's top stays under the caption's whole box.
            Assert.That(bubbleTop, Is.LessThan(caption.yMin), "The bubble stays below \"로딩중\".");
            Assert.That(tip, Is.GreaterThanOrEqualTo(bar.yMax - 1f), "Its tail points down at the bar.");
            Assert.That(view.Bubble.position.x, Is.InRange(bar.xMin, bar.xMax), "Over the bar.");
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
