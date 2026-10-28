using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    public sealed class StoryPlayerTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        private StoryPlayer CreatePlayer()
        {
            var root = new GameObject("Player", typeof(RectTransform));
            root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>();
            player.playOnStart = false;
            player.cutHost = root.GetComponent<RectTransform>();
            var prefab = new GameObject("Cut", typeof(RectTransform), typeof(StoryCutView));
            var layerObject = new GameObject("Layer", typeof(RectTransform), typeof(StoryLayer));
            layerObject.transform.SetParent(prefab.transform, false);
            var layer = layerObject.GetComponent<StoryLayer>();
            layer.enterSeconds = .04f;
            prefab.GetComponent<StoryCutView>().layers = new[] { layer };
            var sequence = ScriptableObject.CreateInstance<StorySequence>();
            for (int i = 0; i < 2; i++)
            {
                var cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
                cut.cutId = "cut-" + i; cut.viewPrefab = prefab.GetComponent<StoryCutView>();
                cut.autoReadSeconds = .1f;
                sequence.cuts.Add(cut); cleanup.Add(cut);
            }
            player.sequence = sequence;
            cleanup.Add(root); cleanup.Add(prefab); cleanup.Add(sequence);
            root.SetActive(true);
            Assert.That(player.Play(sequence), Is.True);
            return player;
        }

        [UnityTest]
        public IEnumerator PreviousButtonReturnsToThePreviousPictureFirstPageAndPausesAuto()
        {
            var player = CreatePlayer();
            player.gameObject.SetActive(false);
            var buttonObject = new GameObject("Previous", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            buttonObject.transform.SetParent(player.transform, false);
            player.previousButton = buttonObject.GetComponent<UnityEngine.UI.Button>();
            foreach (var cut in player.sequence.cuts)
            {
                cut.pages.Add(new StoryDialoguePage { dialogue = "First page" });
                cut.pages.Add(new StoryDialoguePage { dialogue = "Second page" });
            }
            int completions = 0;
            player.Finished += reason => completions++;
            player.gameObject.SetActive(true);
            player.Replay();
            Assert.That(player.previousButton.interactable, Is.False);
            player.Previous();
            Assert.That(player.CurrentCutIndex, Is.Zero);
            player.Next(); player.Next(); player.Next(); player.Next(); player.Next();
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            Assert.That(player.CurrentPageIndex, Is.EqualTo(1));
            Assert.That(player.previousButton.interactable, Is.True);
            var oldPicture = player.CurrentView;
            player.ToggleAuto();
            player.previousButton.onClick.Invoke();
            Assert.That(player.CurrentCutIndex, Is.Zero);
            Assert.That(player.CurrentPageIndex, Is.Zero);
            Assert.That(player.IsReading, Is.True);
            Assert.That(player.AutoAdvance, Is.False);
            Assert.That(player.CurrentView, Is.Not.SameAs(oldPicture));
            Assert.That(player.previousButton.interactable, Is.False);
            Assert.That(completions, Is.Zero);
            yield return null;
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>().Length, Is.EqualTo(1));
            player.Skip();
            Assert.That(player.previousButton.interactable, Is.False);
            player.Previous();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(player.IsPlaying, Is.False);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator SpeakerPaletteChangesOnTheSamePictureAndRestoresTheFallbackStyle()
        {
            var player = CreatePlayer();
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            badge.transform.SetParent(player.transform, false); player.speakerBadge = badge.GetComponent<UnityEngine.UI.Image>();
            var name = new GameObject("Speaker", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            name.transform.SetParent(player.transform, false); player.speakerText = name.GetComponent<UnityEngine.UI.Text>();
            var view = player.sequence.cuts[0].viewPrefab;
            view.overrideDialogueLayout = true;
            view.speakerBadge = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/Story/Opening/OP_002/SpeakerBadge.png");
            view.speakerColor = Color.cyan;
            player.speakerPalette = UnityEditor.AssetDatabase.LoadAssetAtPath<StorySpeakerPalette>("Assets/Hapio/Story/Opening/SpeakerPalette.asset");
            Assert.That(player.speakerPalette, Is.Not.Null);
            foreach (string speaker in new[] {"불이", "만복이", "묵이", "금옥이", "솔이", "사부님", "unknown", ""})
                player.sequence.cuts[0].pages.Add(new StoryDialoguePage {speaker = speaker, dialogue = "Page"});
            player.Replay(); player.Next();
            var picture = player.CurrentView;
            foreach (string speaker in new[] {"불이", "만복이", "묵이", "금옥이", "솔이", "사부님"})
            {
                Assert.That(player.speakerPalette.TryGet(speaker, out var style), Is.True);
                Assert.That(player.speakerBadge.color, Is.EqualTo(style.badgeColor));
                Assert.That(player.speakerText.color, Is.EqualTo(style.textColor));
                Assert.That(player.speakerBadge.material, Is.SameAs(player.speakerPalette.badgeMaterial));
                Assert.That(player.speakerBadge.enabled, Is.True);
                Assert.That(player.CurrentView, Is.SameAs(picture));
                player.Next();
            }
            Assert.That(player.speakerBadge.color, Is.EqualTo(Color.white));
            Assert.That(player.speakerText.color, Is.EqualTo(Color.cyan));
            player.Next();
            Assert.That(player.speakerBadge.enabled, Is.False);
            player.Previous();
            yield return null;
        }
#endif

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator TextAddedToAnOldSilentCutUsesReadableFallbackRects()
        {
            var player = CreatePlayer();
            var source = player.sequence.cuts[0].viewPrefab;
            source.overrideDialogueLayout = true;
            source.dialogueRect = source.speakerRect = source.speakerBadgeRect = new Rect();
            var body = new GameObject("Body", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            body.transform.SetParent(player.transform, false); player.dialogueText = body.GetComponent<UnityEngine.UI.Text>();
            var speaker = new GameObject("Speaker", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            speaker.transform.SetParent(player.transform, false); player.speakerText = speaker.GetComponent<UnityEngine.UI.Text>();
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            badge.transform.SetParent(player.transform, false); player.speakerBadge = badge.GetComponent<UnityEngine.UI.Image>();
            player.sequence.cuts[0].pages.Add(new StoryDialoguePage { speaker = "불이", dialogue = "새로 넣은 대사" });
            player.Replay(); player.Next();
            Assert.That(player.dialogueText.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(84, -740)));
            Assert.That(player.dialogueText.rectTransform.sizeDelta, Is.EqualTo(new Vector2(259, 56)));
            Assert.That(player.speakerText.rectTransform.sizeDelta, Is.EqualTo(new Vector2(36, 18)));
            Assert.That(player.speakerBadge.rectTransform.sizeDelta, Is.EqualTo(new Vector2(91, 33)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreparedOpeningPlaysEveryAuthoredCutAndFinishesOnceWithoutRetainingOldViews()
        {
            var player = CreatePlayer();
            var sequence = UnityEditor.AssetDatabase.LoadAssetAtPath<StorySequence>(
                "Assets/Hapio/Story/Opening/BatchPreview.asset");
            int completions = 0;
            player.Finished += reason => completions++;
            Assert.That(player.Play(sequence), Is.True);
            for (int i = 0; i < sequence.cuts.Count; i++)
            {
                Assert.That(player.CurrentCutIndex, Is.EqualTo(i));
                player.Next();
                Assert.That(player.IsReading, Is.True);
                var picture = player.CurrentView;
                for (int page = 0; page < sequence.cuts[i].PageCount; page++)
                {
                    Assert.That(player.CurrentCutIndex, Is.EqualTo(i));
                    Assert.That(player.CurrentPageIndex, Is.EqualTo(page));
                    Assert.That(player.CurrentView, Is.SameAs(picture));
                    yield return null;
                    Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>().Length, Is.EqualTo(1));
                    foreach (var image in player.CurrentView.GetComponentsInChildren<UnityEngine.UI.Image>())
                        if (image.name != "Paper Base") Assert.That(image.sprite, Is.Not.Null, sequence.cuts[i].cutId + " / " + image.name);
                    player.Next();
                }
            }
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(completions, Is.EqualTo(1));
            player.Next(); player.Skip();
            Assert.That(completions, Is.EqualTo(1));
            yield return null;
        }
#endif

        [UnityTest]
        public IEnumerator SilentPageHidesPaperAndLaterDialogueOnTheSamePictureRevealsIt()
        {
            var player = CreatePlayer();
            var group = new GameObject("Dialogue", typeof(RectTransform), typeof(CanvasGroup));
            group.transform.SetParent(player.transform, false);
            player.dialogueGroup = group.GetComponent<CanvasGroup>();
            var cut = player.sequence.cuts[0];
            cut.viewPrefab.hideEmptyDialogue = true;
            cut.pages.Add(new StoryDialoguePage());
            cut.pages.Add(new StoryDialoguePage { dialogue = "나중에 추가한 대사" });
            player.Replay();
            var picture = player.CurrentView;
            player.Next();
            Assert.That(player.IsReading, Is.True);
            Assert.That(player.dialogueGroup.alpha, Is.Zero);
            Assert.That(player.dialogueGroup.blocksRaycasts, Is.False);
            player.Next();
            Assert.That(player.CurrentView, Is.SameAs(picture));
            Assert.That(player.dialogueGroup.alpha, Is.EqualTo(1));
            player.Replay(); player.Next();
            Assert.That(player.dialogueGroup.alpha, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CutSpecificDialogueLayoutRestoresOnReplayAndBadgeFollowsTheSpeaker()
        {
            var player = CreatePlayer();
            var body = new GameObject("Body", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            body.transform.SetParent(player.transform, false);
            player.dialogueText = body.GetComponent<UnityEngine.UI.Text>();
            player.dialogueText.fontSize = 21;
            player.dialogueText.rectTransform.localScale = new Vector3(.9855f, .9855f, 1);
            player.dialogueText.rectTransform.anchoredPosition = new Vector2(84, -740);
            player.dialogueText.rectTransform.sizeDelta = new Vector2(259, 56);
            var speaker = new GameObject("Speaker", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            speaker.transform.SetParent(player.transform, false);
            player.speakerText = speaker.GetComponent<UnityEngine.UI.Text>();
            player.speakerText.color = Color.black;
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            badge.transform.SetParent(player.transform, false);
            player.speakerBadge = badge.GetComponent<UnityEngine.UI.Image>();
            var second = Object.Instantiate(player.sequence.cuts[1].viewPrefab);
            cleanup.Add(second.gameObject);
            second.overrideDialogueLayout = true;
            second.dialogueFontSize = 19;
            second.dialogueRect = new Rect(94, 745, 259, 48);
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            cleanup.Add(sprite); cleanup.Add(texture);
            second.speakerBadge = sprite;
            player.sequence.cuts[1].viewPrefab = second;
            player.sequence.cuts[1].pages.Add(new StoryDialoguePage { speaker = "사부님", dialogue = "첫 대사" });
            player.sequence.cuts[1].pages.Add(new StoryDialoguePage { dialogue = "설명" });
            player.Replay(); player.Next(); player.Next(); player.Next();
            Assert.That(player.speakerBadge.enabled, Is.True);
            Assert.That(player.speakerBadge.sprite, Is.SameAs(sprite));
            Assert.That(player.dialogueText.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(94, -745)));
            Assert.That(player.speakerText.color, Is.EqualTo(Color.white));
            Assert.That(player.dialogueText.fontSize, Is.EqualTo(19));
            Assert.That(player.dialogueText.rectTransform.localScale.x, Is.EqualTo(1));
            player.Next();
            Assert.That(player.speakerBadge.enabled, Is.False);
            player.Replay();
            Assert.That(player.dialogueText.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(84, -740)));
            Assert.That(player.dialogueText.rectTransform.sizeDelta, Is.EqualTo(new Vector2(259, 56)));
            Assert.That(player.speakerText.color, Is.EqualTo(Color.black));
            Assert.That(player.dialogueText.fontSize, Is.EqualTo(21));
            Assert.That(player.dialogueText.rectTransform.localScale.x, Is.EqualTo(.9855f).Within(.0001f));
            Assert.That(player.speakerBadge.enabled, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DialoguePagesKeepTheSamePictureAndAdvanceTheCutOnlyAfterTheLastPage()
        {
            var player = CreatePlayer();
            var cut = player.sequence.cuts[0];
            cut.pages.Add(new StoryDialoguePage { speaker = "사부님", dialogue = "첫 대사" });
            cut.pages.Add(new StoryDialoguePage { speaker = "제자", dialogue = "둘째 대사" });
            var body = new GameObject("Body", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            body.transform.SetParent(player.transform, false);
            player.dialogueText = body.GetComponent<UnityEngine.UI.Text>();
            player.Replay();
            var picture = player.CurrentView;
            player.Next();
            Assert.That(player.CurrentPageIndex, Is.Zero);
            Assert.That(player.dialogueText.text, Is.EqualTo("첫 대사"));
            player.Next();
            Assert.That(player.CurrentCutIndex, Is.Zero);
            Assert.That(player.CurrentPageIndex, Is.EqualTo(1));
            Assert.That(player.CurrentView, Is.SameAs(picture));
            Assert.That(player.IsReading, Is.True);
            Assert.That(player.dialogueText.text, Is.EqualTo("둘째 대사"));
            player.Next();
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            Assert.That(player.CurrentPageIndex, Is.Zero);
            Assert.That(player.CurrentView, Is.Not.SameAs(picture));
            Assert.That(player.IsReading, Is.False);
            player.Replay();
            Assert.That(player.CurrentPageIndex, Is.Zero);
            Assert.That(player.dialogueText.text, Is.EqualTo("첫 대사"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutoProgressWaitsForEachPageAndResetsItsReadingTimer()
        {
            var player = CreatePlayer();
            var cut = player.sequence.cuts[0];
            cut.pages.Add(new StoryDialoguePage { dialogue = "첫 대사", autoReadSeconds = .5f });
            cut.pages.Add(new StoryDialoguePage { dialogue = "둘째 대사", autoReadSeconds = .5f });
            player.Replay();
            player.Next();
            player.ToggleAuto();
            // Advance presentation time directly: Editor import/domain-reload stalls must not
            // turn an intended 150 ms observation into an arbitrary real-time delay.
            player.AdvanceTime(.15f);
            Assert.That(player.CurrentPageIndex, Is.Zero);
            player.AdvanceTime(.36f);
            Assert.That(player.CurrentCutIndex, Is.Zero);
            Assert.That(player.CurrentPageIndex, Is.EqualTo(1));
            player.AdvanceTime(.15f);
            Assert.That(player.CurrentCutIndex, Is.Zero);
            Assert.That(player.CurrentPageIndex, Is.EqualTo(1));
            player.AdvanceTime(.36f);
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            player.Next();
            player.AdvanceTime(.11f);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NextFinishesEntranceThenAdvancesAndCompletesOnlyOnce()
        {
            var player = CreatePlayer();
            int finished = 0;
            player.Finished += reason => { Assert.That(reason, Is.EqualTo(StoryExitReason.Completed)); finished++; };
            player.Next();
            Assert.That(player.CurrentCutIndex, Is.Zero);
            Assert.That(player.IsReading, Is.True);
            player.Next();
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            Assert.That(player.IsReading, Is.False);
            player.Next(); player.Next(); player.Next(); player.Skip();
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(player.IsPlaying, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkipAndReplayResetPresentationWithoutDuplicateCompletion()
        {
            var player = CreatePlayer();
            int skipped = 0;
            player.Finished += reason => { if (reason == StoryExitReason.Skipped) skipped++; };
            player.ToggleAuto(); player.Skip(); player.Skip();
            Assert.That(skipped, Is.EqualTo(1));
            player.Replay();
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(player.AutoAdvance, Is.False);
            Assert.That(player.CurrentCutIndex, Is.Zero);
            player.gameObject.SetActive(false);
            Assert.That(skipped, Is.EqualTo(1));
            Assert.That(player.CurrentView, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutoPlaybackUsesUnscaledTimeWhenGameClockIsPaused()
        {
            var player = CreatePlayer();
            float previous = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                player.ToggleAuto();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (player.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(player.IsPlaying, Is.False);
                Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            }
            finally { Time.timeScale = previous; }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in cleanup) if (obj != null) Object.Destroy(obj);
            cleanup.Clear();
            yield return null;
        }
    }
}
