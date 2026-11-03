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

        [Test]
        public void GustsComeOnceAPeriodAndSmoothly()
        {
            Assert.That(TitleWindWave.Gust(0, 6), Is.Zero, "Calm when the title first opens.");
            Assert.That(TitleWindWave.Gust(2.9f, 6), Is.Zero);
            Assert.That(TitleWindWave.Gust(4.05f, 6), Is.EqualTo(1).Within(.001f));
            Assert.That(TitleWindWave.Gust(3.3f, 6), Is.InRange(.01f, .99f), "Rises smoothly instead of snapping.");
            Assert.That(TitleWindWave.Gust(5.2f, 6), Is.Zero);
            Assert.That(TitleWindWave.Gust(10.05f, 6), Is.EqualTo(1).Within(.001f), "Repeats every period.");
        }

        // #467: tied edge still, free end swings, a gust swings the same shape further, and the picture is cut into a grid.
        [Test]
        public void WindKeepsTheTiedEdgeStillAndSwingsTheFreeEndFurtherInAGust()
        {
            var root = new GameObject("Wind", typeof(RectTransform), typeof(Canvas));
            try
            {
                var image = new GameObject("Banner", typeof(RectTransform)).AddComponent<Image>();
                image.transform.SetParent(root.transform, false);
                image.rectTransform.sizeDelta = new Vector2(40, 160);
                var wave = image.gameObject.AddComponent<TitleWindWave>();
                wave.pinned = new Vector2(.5f, 1); wave.tip = new Vector2(.5f, 0);
                wave.amplitude = 2.5f; wave.lean = -3; wave.waves = .8f; wave.speed = .5f; wave.gustStrength = 1.5f; wave.gustSeconds = 6;
                wave.columns = 4; wave.rows = 16;
                Rect rect = image.rectTransform.rect;
                var tiedCorner = new Vector2(rect.xMin, rect.yMax);
                var freeEnd = new Vector2(rect.center.x, rect.yMin);

                Assert.That(wave.Offset(tiedCorner, rect, 1.3f), Is.EqualTo(Vector2.zero), "The whole tied edge stays on the crossbar.");
                Vector2 calm = wave.Offset(freeEnd, rect, .05f), gust = wave.Offset(freeEnd, rect, 4.05f);
                Assert.That(calm.x, Is.LessThan(0)); Assert.That(calm.y, Is.Zero);
                Assert.That(gust.x, Is.EqualTo(calm.x * 2.5f).Within(.0001f), "Two wave periods apart, only the gust differs.");
                Assert.That(wave.Offset(freeEnd, rect, 1.3f), Is.EqualTo(wave.Offset(freeEnd, rect, 1.3f)));
                Assert.That(Mathf.Abs(wave.Offset(new Vector2(rect.center.x, rect.center.y), rect, .05f).x),
                    Is.LessThan(Mathf.Abs(calm.x)), "Swings less nearer the tie.");

                var mesh = new VertexHelper();
                mesh.AddVert(new Vector3(rect.xMin, rect.yMin), Color.white, new Vector2(0, 0));
                mesh.AddVert(new Vector3(rect.xMin, rect.yMax), Color.white, new Vector2(0, 1));
                mesh.AddVert(new Vector3(rect.xMax, rect.yMax), Color.white, new Vector2(1, 1));
                mesh.AddVert(new Vector3(rect.xMax, rect.yMin), Color.white, new Vector2(1, 0));
                mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
                wave.Sample(4.05f);
                wave.ModifyMesh(mesh);
                Assert.That(mesh.currentVertCount, Is.EqualTo(5 * 17));
                Assert.That(mesh.currentIndexCount, Is.EqualTo(4 * 16 * 6));
                var vertex = new UIVertex();
                mesh.PopulateUIVertex(ref vertex, 5 * 17 - 1);
                Assert.That((Vector2)vertex.position, Is.EqualTo(new Vector2(rect.xMax, rect.yMax)), "Top corner stays tied.");
                Assert.That((Vector2)vertex.uv0, Is.EqualTo(Vector2.one));
                mesh.PopulateUIVertex(ref vertex, 2);
                Assert.That(vertex.position.x, Is.LessThan(rect.center.x - 1), "The free end blows downwind.");
                Assert.That((Vector2)vertex.uv0, Is.EqualTo(new Vector2(.5f, 0)));
                mesh.Dispose();
            }
            finally { Object.DestroyImmediate(root); }
        }

        // #467: a fixed set of leaves is recycled inside the title, a gust sweeps them left, and every start looks the same.
        [UnityTest]
        public IEnumerator LeavesRecycleInsideTheTitleBlowLeftInAGustAndRestartTheSame()
        {
            var root = new GameObject("Leaves", typeof(RectTransform), typeof(Canvas));
            var texture = new Texture2D(4, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 2), Vector2.one * .5f);
            float clock = Time.timeScale;
            try
            {
                var area = new GameObject("Falling Leaves", typeof(RectTransform)).GetComponent<RectTransform>();
                area.SetParent(root.transform, false); area.sizeDelta = new Vector2(402, 874);
                area.gameObject.SetActive(false);
                var leaves = area.gameObject.AddComponent<TitleFallingLeaves>();
                leaves.sprites = new[] { sprite }; leaves.count = 6;
                area.gameObject.SetActive(true);

                Assert.That(leaves.Count, Is.EqualTo(6)); Assert.That(area.childCount, Is.EqualTo(6));
                for (int i = 0; i < leaves.Count; i++)
                {
                    var image = leaves.LeafAt(i).GetComponent<Image>();
                    Assert.That(image.raycastTarget, Is.False, "Leaves must not block the start button.");
                    Assert.That(Mathf.Max(image.rectTransform.sizeDelta.x, image.rectTransform.sizeDelta.y),
                        Is.InRange(leaves.size.x - .01f, leaves.size.y + .01f));
                    Assert.That(image.rectTransform.sizeDelta.x, Is.EqualTo(image.rectTransform.sizeDelta.y * 2).Within(.01f), "Keeps the leaf's shape.");
                }

                Vector2 calm = TitleFallingLeaves.Drift(0, 20, 110, 50), gust = TitleFallingLeaves.Drift(1, 20, 110, 50);
                Assert.That(calm, Is.EqualTo(new Vector2(-20, 50)));
                Assert.That(gust.x, Is.LessThan(calm.x), "A gust sweeps them further left.");
                Assert.That(gust.y, Is.GreaterThan(calm.y));

                System.Func<Vector2[]> positions = () =>
                {
                    var now = new Vector2[leaves.Count];
                    for (int i = 0; i < now.Length; i++) now[i] = leaves.LeafAt(i).anchoredPosition;
                    return now;
                };
                leaves.Restart();
                for (int frame = 0; frame < 90; frame++) leaves.Advance(1 / 30f);
                Vector2[] first = positions();
                leaves.Restart();
                for (int frame = 0; frame < 90; frame++) leaves.Advance(1 / 30f);
                Assert.That(positions(), Is.EqualTo(first), "The same seed starts the same way every time.");

                for (int frame = 0; frame < 900; frame++)
                {
                    leaves.Advance(1 / 30f);
                    for (int i = 0; i < leaves.Count; i++)
                    {
                        Vector2 p = leaves.PositionAt(i);
                        Assert.That(p.x >= -40 && p.x <= 402 + 160 && p.y >= -40 && p.y <= 874 + 40, Is.True,
                            $"Leaf {i} left the title at {p} instead of coming back.");
                    }
                }
                Assert.That(leaves.Recycled, Is.GreaterThan(leaves.Count), "Fallen leaves come back from the top or the right.");

                Vector2[] before = positions();
                Time.timeScale = 0;
                yield return null; yield return null;
                Assert.That(positions(), Is.Not.EqualTo(before), "Leaves keep falling with a paused game clock.");
            }
            finally
            {
                Time.timeScale = clock;
                Object.Destroy(root); Object.Destroy(sprite); Object.Destroy(texture);
            }
            yield return null;
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
