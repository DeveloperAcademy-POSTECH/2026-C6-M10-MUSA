using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// #635: the persistent cloud cover closes over the whole screen, says so, stays covered for as long as it is not told to
    /// lift, lifts when told, and takes every touch while it is up and none while it is down.
    /// </summary>
    public sealed class GameCloudCoverFlowTests
    {
        private GameObject holder;

        [TearDown]
        public void Cleanup()
        {
            if (holder != null) Object.Destroy(holder);
            holder = null;
        }

        private GameCloudCover CreateCover()
        {
            holder = new GameObject("Cloud cover test");
            GameCloudCover cover = GameCloudCover.Create(holder.transform);
            Assert.That(cover, Is.Not.Null, "The saved cloud art builds a cover.");
            return cover;
        }

        [UnityTest]
        public IEnumerator CoversThenWaitsCoveredUntilToldThenLifts()
        {
            GameCloudCover cover = CreateCover();
            Assert.That((cover.IsUp, cover.gameObject.activeSelf), Is.EqualTo((false, false)), "Off, touching nothing, while idle.");
            int covered = 0;
            float started = Time.realtimeSinceStartup, coveredAfter = -1f;
            Assert.That(cover.Close(() => { covered++; coveredAfter = Time.realtimeSinceStartup - started; }), Is.True);
            Assert.That(cover.Close(() => covered += 10), Is.False, "One cover at a time.");
            Assert.That(cover.IsUp, Is.True);
            yield return Until(() => cover.IsCovered, "The screen is covered", cover.CloseSeconds + 2f);
            Assert.That(covered, Is.EqualTo(1), "Told once, when wholly covered.");
            Assert.That(coveredAfter, Is.GreaterThanOrEqualTo(cover.CloseSeconds - .05f));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That((cover.IsUp, cover.IsHolding), Is.EqualTo((true, true)), "Stays covered however long the next screen takes.");
            Assert.That(Backdrop(cover).alpha, Is.EqualTo(1f).Within(1e-4f), "No gap between the clouds while covered.");
            cover.Open();
            Assert.That(cover.IsOpening, Is.True);
            yield return Until(() => !cover.IsUp, "The clouds lift", cover.Duration + 2f);
            Assert.That(cover.gameObject.activeSelf, Is.False, "Off again once lifted.");
            Assert.That(covered, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OpenAskedWhileClosingLiftsRightAfterCovering()
        {
            GameCloudCover cover = CreateCover();
            int covered = 0;
            float started = Time.realtimeSinceStartup;
            Assert.That(cover.Close(() => covered++), Is.True);
            cover.Open();
            yield return Until(() => !cover.IsUp, "Covered, then lifted without waiting", cover.Duration + 2f);
            Assert.That(covered, Is.EqualTo(1), "It still covered the screen first.");
            Assert.That(Time.realtimeSinceStartup - started, Is.GreaterThanOrEqualTo(cover.Duration - .1f), "The whole way in and out.");
        }

        [UnityTest]
        public IEnumerator CoversTheWholeScreenAndTakesEveryTouchWhileUp()
        {
            GameCloudCover cover = CreateCover();
            if (EventSystem.current == null)
                new GameObject("Event system", typeof(EventSystem)).transform.SetParent(holder.transform, false);
            Assert.That(cover.Close(null), Is.True);
            yield return null;
            var clouds = (RectTransform)cover.GetComponentInChildren<StoryCloudWipe>(true).transform;
            Rect drawn = ScreenRect(clouds);
            Assert.That(drawn.width, Is.EqualTo(Screen.width).Within(1f), "Across the whole screen.");
            Assert.That(drawn.height, Is.EqualTo(Screen.height).Within(1f), "And from top to bottom.");
            var hits = new List<RaycastResult>();
            cover.GetComponent<GraphicRaycaster>().Raycast(
                new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .5f) }, hits);
            Assert.That(hits, Is.Not.Empty, "A touch lands on the clouds, not on the screen under them.");
            cover.Cancel();
            Assert.That((cover.IsUp, cover.gameObject.activeSelf), Is.EqualTo((false, false)), "Gone at once when cancelled.");
        }

        private static CanvasGroup Backdrop(GameCloudCover cover) =>
            cover.GetComponentsInChildren<CanvasGroup>(true).Single(group => group.name == "Covered backdrop");

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static IEnumerator Until(Func<bool> predicate, string message, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }
    }
}
