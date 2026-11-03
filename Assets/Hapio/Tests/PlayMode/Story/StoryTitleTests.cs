using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class StoryTitleTests
    {
        [UnityTest]
        public IEnumerator CloudsStayBoundedMoveInOppositeDirectionsAndRestoreWhenReenabled()
        {
            var root = new GameObject("Cloud test");
            var left = new GameObject("Left", typeof(RectTransform)).GetComponent<RectTransform>();
            var right = new GameObject("Right", typeof(RectTransform)).GetComponent<RectTransform>();
            left.SetParent(root.transform, false); right.SetParent(root.transform, false);
            left.anchoredPosition = new Vector2(-182, -257); right.anchoredPosition = new Vector2(193, -67);
            var drift = root.AddComponent<TitleCloudDrift>();
            drift.clouds = new[] {
                new TitleCloudDrift.Cloud {target=left,distance=24,seconds=40},
                new TitleCloudDrift.Cloud {target=right,distance=20,seconds=40,reverse=true}
            };
            float clock = Time.timeScale;
            try
            {
                drift.Sample(0); drift.Sample(10);
                Assert.That(left.anchoredPosition, Is.EqualTo(new Vector2(-158, -257)));
                Assert.That(right.anchoredPosition, Is.EqualTo(new Vector2(173, -67)));
                drift.Sample(4000);
                Assert.That(left.anchoredPosition.x, Is.EqualTo(-182).Within(.01f));
                root.SetActive(false);
                Assert.That(left.anchoredPosition, Is.EqualTo(new Vector2(-182, -257)));
                left.anchoredPosition = new Vector2(-100, -200);
                root.SetActive(true);
                Assert.That(left.anchoredPosition, Is.EqualTo(new Vector2(-100, -200)));
                Time.timeScale = 0;
                yield return null; yield return null;
                Assert.That(left.anchoredPosition.x, Is.GreaterThan(-100), "Clouds must continue with a paused game clock.");
                Assert.That(left.anchoredPosition.y, Is.EqualTo(-200));
            }
            finally { Time.timeScale = clock; Object.Destroy(root); }
            yield return null;
        }

        // #467: the page is fitted inside the screen (GameLobbyView), so its art must still reach every edge.
        [TestCase(375, 667)] [TestCase(375, 812)] [TestCase(393, 852)] [TestCase(402, 874)] [TestCase(440, 956)] [TestCase(360, 820)]
        public void TitleArtCoversTheWholeScreenWhileItsPageStaysFittedInside(float width, float height)
        {
            var screenObject = new GameObject("Screen", typeof(RectTransform), typeof(Canvas));
            try
            {
                screenObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var screen = (RectTransform)screenObject.transform;
                screen.sizeDelta = new Vector2(width, height); screen.localScale = Vector3.one * 3;
                var page = new GameObject("Page", typeof(RectTransform)).GetComponent<RectTransform>();
                page.SetParent(screen, false); page.sizeDelta = new Vector2(402, 874);
                page.localScale = Vector3.one * Mathf.Min(width / 402, height / 874);
                var art = new GameObject("Art", typeof(RectTransform)).GetComponent<RectTransform>();
                art.SetParent(page, false); art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.sizeDelta = Vector2.zero;
                var logo = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
                logo.SetParent(art, false); logo.anchorMin = logo.anchorMax = logo.pivot = new Vector2(0, 1);
                logo.anchoredPosition = new Vector2(82, -164); logo.sizeDelta = new Vector2(238, 113);
                art.gameObject.AddComponent<TitleArtCover>().Apply();

                var a = new Vector3[4]; art.GetWorldCorners(a);
                var s = new Vector3[4]; screen.GetWorldCorners(s);
                const float pixel = .01f;
                Assert.That(a[0].x, Is.LessThanOrEqualTo(s[0].x + pixel)); Assert.That(a[0].y, Is.LessThanOrEqualTo(s[0].y + pixel));
                Assert.That(a[2].x, Is.GreaterThanOrEqualTo(s[2].x - pixel)); Assert.That(a[2].y, Is.GreaterThanOrEqualTo(s[2].y - pixel));
                Assert.That(Mathf.Min(a[2].x - a[0].x - (s[2].x - s[0].x), a[2].y - a[0].y - (s[2].y - s[0].y)),
                    Is.EqualTo(0).Within(pixel), "Grown only until it covers, so one side fits exactly.");
                Assert.That(Vector3.Distance((a[0] + a[2]) * .5f, (s[0] + s[2]) * .5f), Is.LessThan(pixel),
                    "Cropped evenly around the screen centre.");
                Assert.That(page.localScale.x, Is.EqualTo(Mathf.Min(width / 402, height / 874)), "The page with the buttons keeps its fit.");
                Assert.That(logo.anchoredPosition, Is.EqualTo(new Vector2(82, -164)), "The authored layout inside the art is untouched.");
                var l = new Vector3[4]; logo.GetWorldCorners(l);
                Assert.That(l[0].x >= s[0].x && l[2].x <= s[2].x && l[0].y >= s[0].y && l[2].y <= s[2].y, Is.True, "The title name stays on screen.");
            }
            finally { Object.DestroyImmediate(screenObject); }
        }

        [UnityTest]
        public IEnumerator TitleWaitsForTheStartButtonAndCanReturnWithoutOldStoryViews()
        {
            var root = new GameObject("Gate", typeof(RectTransform)); root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>(); player.playOnStart = false;
            var host = new GameObject("Host", typeof(RectTransform)).GetComponent<RectTransform>(); host.SetParent(root.transform, false);
            player.cutHost = host;
            var title = new GameObject("Title"); title.transform.SetParent(root.transform, false);
            var button = title.AddComponent<Button>();
            var controls = new GameObject("Controls"); controls.transform.SetParent(root.transform, false);
            var gate = root.AddComponent<StoryOpeningTitle>();
            gate.player = player; gate.titlePage = title; gate.controls = controls; gate.beginButton = button;
            var view = new GameObject("Picture", typeof(RectTransform), typeof(StoryCutView));
            var cut = ScriptableObject.CreateInstance<StoryCutDefinition>(); cut.cutId = "one"; cut.viewPrefab = view.GetComponent<StoryCutView>();
            var sequence = ScriptableObject.CreateInstance<StorySequence>(); sequence.cuts.Add(cut); player.sequence = sequence;
            try
            {
                root.SetActive(true); yield return null;
                Assert.That(title.activeSelf, Is.True); Assert.That(controls.activeSelf, Is.False);
                Assert.That(player.IsPlaying, Is.False);
                button.onClick.Invoke();
                Assert.That(title.activeSelf, Is.False); Assert.That(controls.activeSelf, Is.True);
                Assert.That(player.IsPlaying, Is.True); Assert.That(player.CurrentCutIndex, Is.Zero);
                gate.ShowTitle(); yield return null;
                Assert.That(player.CurrentView, Is.Null); Assert.That(title.activeSelf, Is.True);
                button.onClick.Invoke();
                Assert.That(host.GetComponentsInChildren<StoryCutView>().Length, Is.EqualTo(1));
            }
            finally { Object.Destroy(root); Object.Destroy(view); Object.Destroy(cut); Object.Destroy(sequence); }
            yield return null;
        }
    }
}
