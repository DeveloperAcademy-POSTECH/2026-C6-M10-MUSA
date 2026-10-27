using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class StoryWorkspaceTests
    {
        [System.Serializable] private class Approved { public Frame[] frames; }
        [System.Serializable] private class Frame { public int number; public Page[] pages; }
        [System.Serializable] private class Page { public string speaker, dialogue; }

        [Test]
        public void EveryPageMatchesTheApprovedScriptIncludingTheTwoSpeakersInCutSeventeen()
        {
            var approved = JsonUtility.FromJson<Approved>(System.IO.File.ReadAllText("Assets/Hapio/Story/Opening/ApprovedScript.json"));
            Assert.That(approved.frames.Length, Is.EqualTo(24));
            int pages = 0;
            foreach (var frame in approved.frames)
            {
                var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>($"Assets/Hapio/Story/Opening/Cuts/OP_{frame.number:000}.asset");
                Assert.That(cut.PageCount, Is.EqualTo(frame.pages.Length));
                Assert.That(cut.viewPrefab.centerSpeakerName, Is.True);
                Assert.That(cut.viewPrefab.dialogueRect, Is.EqualTo(new Rect(84, 715, 270, 100)), $"Cut {frame.number} body alignment");
                for (int i = 0; i < frame.pages.Length; i++)
                {
                    Assert.That(cut.GetPage(i).speaker, Is.EqualTo(frame.pages[i].speaker));
                    Assert.That(cut.GetPage(i).dialogue, Is.EqualTo(frame.pages[i].dialogue));
                    Assert.That(cut.GetPage(i).speaker, Does.Not.Contain("스승님"));
                    pages++;
                }
                if (frame.number == 13 || frame.number == 14)
                {
                    Assert.That(cut.GetPage(0).dialogue, Is.Empty);
                    Assert.That(cut.viewPrefab.hideEmptyDialogue, Is.True);
                    Assert.That(cut.direction, Is.Not.Empty);
                }
            }
            Assert.That(pages, Is.EqualTo(25));
            var seventeen = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>("Assets/Hapio/Story/Opening/Cuts/OP_017.asset");
            Assert.That(seventeen.GetPage(0).speaker, Is.EqualTo("불이"));
            Assert.That(seventeen.GetPage(1).speaker, Is.EqualTo("금옥이"));
            Assert.That(seventeen.GetPage(1).dialogue, Is.EqualTo("되겠냐."));
            Assert.That(seventeen.GetPage(1).autoReadSeconds, Is.LessThan(2));
        }

        [Test]
        public void BrokenGourdUsesTheLatestSourcePlacementInCutsFifteenAndSixteen()
        {
            foreach (int number in new[] {15, 16})
            {
                var view = AssetDatabase.LoadAssetAtPath<StoryCutView>($"Assets/Hapio/Story/Opening/Views/OP_{number:000}.prefab");
                string id = number == 15 ? "3273:13" : "3273:18";
                var placement = view.transform.Find($"image 1308 [{id}] Placement") as RectTransform;
                Assert.That(placement, Is.Not.Null);
                Assert.That(placement.anchoredPosition, Is.EqualTo(new Vector2(53, -580)));
                Assert.That(placement.sizeDelta, Is.EqualTo(new Vector2(323, 147)));
                var image = placement.GetComponentInChildren<UnityEngine.UI.Image>(true);
                Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.EndWith("/4a78f.png"));
                Assert.That(placement.GetSiblingIndex(), Is.GreaterThan(1));
            }
        }

        [Test]
        public void AnimatedTitleSharesFourOriginalCloudsWithTheHomeStartPage()
        {
            var title = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/AnimatedTitle.prefab");
            var drift = title.GetComponent<TitleCloudDrift>();
            Assert.That(drift.clouds.Length, Is.EqualTo(4));
            foreach (var cloud in drift.clouds)
            {
                Assert.That(cloud.distance, Is.GreaterThan(0));
                Assert.That(cloud.seconds, Is.GreaterThan(20));
                Assert.That(cloud.target.GetComponentInChildren<UnityEngine.UI.Image>(true).sprite, Is.Not.Null);
            }
            Assert.That(drift.clouds[0].target.anchoredPosition, Is.EqualTo(new Vector2(-182, -257)));
            Assert.That(drift.clouds[1].target.anchoredPosition, Is.EqualTo(new Vector2(193, -67)));
            var home = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Art/UI/GameLobby.prefab").GetComponentInChildren<GameLobbyView>(true);
            Assert.That(home.titlePage.GetComponentInChildren<TitleCloudDrift>(true), Is.Not.Null);
            Assert.That(home.enterButton.gameObject.activeSelf, Is.True);
            Assert.That(home.titlePage.transform.Find("SealPrompt").gameObject.activeSelf, Is.False);
            foreach (var graphic in title.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, "Title artwork must not block Home navigation.");
        }

        [Test]
        public void SecondCutHasOriginalCharacterLayersSpeakerBadgeAndTwoCutPreview()
        {
            var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>("Assets/Hapio/Story/Opening/Cuts/OP_002.asset");
            var view = cut.viewPrefab;
            Assert.That(view.layers.Length, Is.EqualTo(7));
            foreach (var layer in view.layers) Assert.That(layer.artwork.sprite, Is.Not.Null, layer.name);
            Assert.That(view.overrideDialogueLayout, Is.True);
            Assert.That(view.speakerBadge, Is.Not.Null);
            Assert.That(view.dialogueRect, Is.EqualTo(new Rect(84, 715, 270, 100)));
            Assert.That(view.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length, Is.EqualTo(17));
            Assert.That(view.transform.Find("Building Placement/Motion/Artwork").GetComponent<UnityEngine.UI.Image>().color.a,
                Is.EqualTo(.9f).Within(.001f));
            Assert.That(cut.GetPage(0).speaker, Is.EqualTo("사부님"));
            var preview = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/Opening/OpeningPreview.asset");
            Assert.That(preview.Validate(out string error), Is.True, error);
            Assert.That(preview.cuts.Count, Is.EqualTo(2));
            Assert.That(preview.cuts[1], Is.SameAs(cut));
        }

        [Test]
        public void PreparedPreviewUsesFigmaNumbersAndAllTwentyFourCutsHaveVisibleEditableArtwork()
        {
            const string root = "Assets/Hapio/Story/Opening/";
            var preview = AssetDatabase.LoadAssetAtPath<StorySequence>(root + "BatchPreview.asset");
            Assert.That(preview.Validate(out string error), Is.True, error);
            Assert.That(preview.cuts.Count, Is.EqualTo(24));
            Assert.That(preview.cuts[0].cutId, Is.EqualTo("OP_001"));
            Assert.That(preview.cuts[1].cutId, Is.EqualTo("OP_002"));
            for (int number = 3; number <= 24; number++)
            {
                var cut = preview.cuts[number - 1];
                Assert.That(cut.cutId, Is.EqualTo($"OP_{number:000}"));
                var view = cut.viewPrefab;
                Assert.That(view.sourceFigmaNumber, Is.EqualTo(number));
                Assert.That(view.sourceFigmaNodeId, Does.StartWith("3182:"));
                Assert.That(view.hideEmptyDialogue, Is.True);
                Assert.That(view.layers.Length, Is.GreaterThanOrEqualTo(2));
                var images = view.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var image in images)
                    if (image.name != "Paper Base") Assert.That(image.sprite, Is.Not.Null, cut.cutId + " / " + image.name);
            }

        }

        [Test]
        public void CroppedBackgroundAndBeadSpritesRetainTheirSourceWindows()
        {
            var six = AssetDatabase.LoadAssetAtPath<StoryCutView>("Assets/Hapio/Story/Opening/Views/OP_006.prefab");
            var background = six.layers[0].artwork;
            Assert.That(background.color.a, Is.EqualTo(.85f).Within(.0001f));
            Assert.That(background.GetComponentInParent<UnityEngine.UI.RectMask2D>(true), Is.Not.Null);
            Assert.That(background.rectTransform.anchoredPosition.x, Is.EqualTo(402 * -.2051f).Within(.01f));
            Assert.That(background.rectTransform.sizeDelta.x, Is.EqualTo(402 * 1.2927f).Within(.01f));
            var twentyTwo = AssetDatabase.LoadAssetAtPath<StoryCutView>("Assets/Hapio/Story/Opening/Views/OP_022.prefab");
            int spriteWindows = 0;
            foreach (var image in twentyTwo.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if (image.name == "Artwork" && image.rectTransform.sizeDelta.x > 4.9f * image.rectTransform.parent.GetComponent<RectTransform>().sizeDelta.x)
                {
                    spriteWindows++;
                    Assert.That(image.GetComponentInParent<UnityEngine.UI.RectMask2D>(true), Is.Not.Null);
                }
            Assert.That(spriteWindows, Is.EqualTo(4));
        }

        [Test]
        public void AddedCutsKeepOriginalDialogueAndUpdatedTwentyTwoUsesNewFigmaGeometry()
        {
            const string root = "Assets/Hapio/Story/Opening/";
            var three = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(root + "Cuts/OP_003.asset");
            Assert.That(three.GetPage(0).speaker, Is.EqualTo("사부님"));
            Assert.That(three.GetPage(0).dialogue, Is.EqualTo("특히 안쪽 선반에 있는 물건은\n절대로 건드리지 말거라."));
            Assert.That(three.viewPrefab.dialogueFontSize, Is.InRange(18f, 20.7f));
            Assert.That(three.viewPrefab.dialogueRect, Is.EqualTo(new Rect(84, 715, 270, 100)));
            var four = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(root + "Cuts/OP_004.asset");
            Assert.That(four.GetPage(0).speaker, Is.EqualTo("불이"));
            Assert.That(four.GetPage(0).dialogue, Is.EqualTo("어휴~ 걱정 마세요!\n청소쯤이야 금방 끝내죠!"));
            var five = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(root + "Cuts/OP_005.asset");
            Assert.That(five.GetPage(0).dialogue, Is.EqualTo("그렇게 사부님들은\n구름을 타고 떠나셨다."));
            Assert.That(five.viewPrefab.layers.Length, Is.EqualTo(9));
            var view = AssetDatabase.LoadAssetAtPath<StoryCutView>(root + "Views/OP_022.prefab");
            var figure = view.transform.Find("image 1248 [3182:11930] Placement").GetComponent<RectTransform>();
            Assert.That(figure.anchoredPosition, Is.EqualTo(new Vector2(-16, -218)));
            var gourd = view.transform.Find("image 1212 [3182:11931] Placement").GetComponent<RectTransform>();
            Assert.That(gourd.anchoredPosition, Is.EqualTo(new Vector2(143, -549)));
            Assert.That(gourd.sizeDelta, Is.EqualTo(new Vector2(123, 129)));
            Assert.That(gourd.GetSiblingIndex(), Is.GreaterThan(figure.GetSiblingIndex()), "The gourd must render in front of the figure.");
            var red = view.transform.Find("image 1217 [3182:11933] Placement").GetComponent<RectTransform>();
            Assert.That(red.anchoredPosition.x, Is.EqualTo(324.9333f).Within(.001f));
            Assert.That(red.anchoredPosition.y, Is.EqualTo(-595f).Within(.001f));
        }

        [TestCase(900, 400)]
        [TestCase(402, 874)]
        [TestCase(360, 1000)]
        public void PageFitsViewportWithoutCroppingOrChangingAuthoredCoordinates(float width, float height)
        {
            var parent = new GameObject("Viewport", typeof(RectTransform));
            var page = new GameObject("Page", typeof(RectTransform));
            try
            {
                var viewport = parent.GetComponent<RectTransform>();
                viewport.sizeDelta = new Vector2(width, height);
                page.transform.SetParent(parent.transform, false);
                page.AddComponent<StoryPageFit>().Apply();
                var rect = page.GetComponent<RectTransform>();
                Assert.That(rect.rect.size, Is.EqualTo(new Vector2(402, 874)));
                Assert.That(rect.rect.width * rect.localScale.x, Is.LessThanOrEqualTo(width + .01f));
                Assert.That(rect.rect.height * rect.localScale.y, Is.LessThanOrEqualTo(height + .01f));
                Assert.That(rect.localScale.x, Is.EqualTo(rect.localScale.y));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void OpeningHas24UniqueEditableCutsAndFirstCutUsesSeparateOriginalSprites()
        {
            var story = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/Opening/Opening.asset");
            Assert.That(story, Is.Not.Null);
            Assert.That(story.Validate(out string error), Is.True, error);
            Assert.That(story.cuts.Count, Is.EqualTo(24));
            for (int i = 0; i < 24; i++)
            {
                Assert.That(story.cuts[i].cutId, Is.EqualTo($"OP_{i + 1:000}"));
                Assert.That(AssetDatabase.GetAssetPath(story.cuts[i].viewPrefab),
                    Is.EqualTo($"Assets/Hapio/Story/Opening/Views/OP_{i + 1:000}.prefab"));
            }
            var layers = story.cuts[0].viewPrefab.layers;
            Assert.That(layers.Length, Is.EqualTo(2));
            Assert.That(AssetDatabase.GetAssetPath(layers[0].artwork.sprite), Does.EndWith("/Background.png"));
            Assert.That(AssetDatabase.GetAssetPath(layers[1].artwork.sprite), Does.EndWith("/Temple.png"));
            Assert.That(layers[1].artwork.rectTransform.sizeDelta, Is.EqualTo(new Vector2(281, 500)));
            Assert.That(layers[1].transform.parent.GetComponent<RectTransform>().anchoredPosition,
                Is.EqualTo(new Vector2(60, -259)));
        }

        [Test]
        public void DuplicateCutIdsAreRejectedBeforePlayback()
        {
            var story = ScriptableObject.CreateInstance<StorySequence>();
            var cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
            var view = new GameObject("View", typeof(RectTransform), typeof(StoryCutView));
            try
            {
                cut.cutId = "same"; cut.viewPrefab = view.GetComponent<StoryCutView>();
                story.cuts.Add(cut); story.cuts.Add(cut);
                Assert.That(story.Validate(out string error), Is.False);
                Assert.That(error, Does.Contain("Duplicate"));
            }
            finally
            {
                Object.DestroyImmediate(view); Object.DestroyImmediate(cut); Object.DestroyImmediate(story);
            }
        }

        [Test]
        public void LayerSamplingReturnsToAuthoredPlacementWithoutCumulativeDrift()
        {
            var obj = new GameObject("Layer", typeof(RectTransform), typeof(StoryLayer));
            try
            {
                var rect = obj.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(17, -39);
                rect.localScale = new Vector3(2, 3, 1);
                var layer = obj.GetComponent<StoryLayer>();
                layer.delay = .2f; layer.enterSeconds = .8f;
                layer.Sample(0); layer.Sample(1); layer.Sample(0); layer.Sample(1);
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(17, -39)));
                Assert.That(rect.localScale, Is.EqualTo(new Vector3(2, 3, 1)));
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
