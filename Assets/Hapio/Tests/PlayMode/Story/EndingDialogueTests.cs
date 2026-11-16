#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class EndingDialogueTests
    {
        [UnityTest]
        public IEnumerator SavedEndingChangesSpeakerOnTheSameArtworkAndRestoresSharedLayout()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Art/UI/GameStageStoryCanvas.prefab"));
            var player = root.GetComponent<StoryPlayer>();
            var fallback = ScriptableObject.CreateInstance<StorySequence>();
            var fallbackCut = ScriptableObject.CreateInstance<StoryCutDefinition>();
            var fallbackView = new GameObject("Fallback", typeof(RectTransform), typeof(StoryCutView));
            try
            {
                root.SetActive(true);
                yield return null;
                var paper = player.dialoguePaper;
                var originalSprite = paper.sprite;
                var originalPosition = paper.rectTransform.anchoredPosition;
                var originalSize = paper.rectTransform.sizeDelta;
                var originalWrap = player.dialogueText.horizontalOverflow;
                int originalNameSize = player.speakerText.fontSize;
                var originalNameScale = player.speakerText.rectTransform.localScale;
                var ending = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Hapio/Story/StageStories/Ending.asset");
                Assert.That(player.Play(ending), Is.True);
                player.Next();
                var firstPicture = player.CurrentView;
                Assert.That(player.speakerText.text, Is.EqualTo("불이"));
                Assert.That(paper.sprite, Is.SameAs(firstPicture.dialoguePaper));
                Assert.That(paper.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(11, -677)));
                Assert.That(paper.rectTransform.sizeDelta, Is.EqualTo(new Vector2(379, 181)));
                Assert.That(paper.GetComponent<Shadow>().enabled, Is.False);
                Assert.That(player.dialogueText.fontSize, Is.EqualTo(20));
                Assert.That(player.speakerText.fontSize * player.speakerText.rectTransform.localScale.x, Is.EqualTo(13f));
                Assert.That(player.dialogueText.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Overflow));
                CheckStyle(player, "불이");
                player.Next();
                Assert.That(player.CurrentView, Is.SameAs(firstPicture));
                Assert.That(player.speakerText.text, Is.EqualTo("만복"));
                CheckStyle(player, "만복");
                player.Next(); player.Next();
                Assert.That(player.speakerText.text, Is.EqualTo("솔이"));
                CheckStyle(player, "솔이");
                player.Next(); player.Next();
                Assert.That(player.dialogueGroup.alpha, Is.Zero, "The silent approach must not display the previous dialogue.");

                fallbackCut.cutId = "fallback";
                fallbackCut.viewPrefab = fallbackView.GetComponent<StoryCutView>();
                fallbackCut.pages.Add(new StoryDialoguePage { speaker = "unknown", dialogue = "Fallback" });
                fallback.cuts.Add(fallbackCut);
                player.Play(fallback); player.Next();
                Assert.That(player.dialogueText.horizontalOverflow, Is.EqualTo(originalWrap));
                Assert.That(paper.sprite, Is.SameAs(originalSprite));
                Assert.That(paper.rectTransform.anchoredPosition, Is.EqualTo(originalPosition));
                Assert.That(paper.rectTransform.sizeDelta, Is.EqualTo(originalSize));
                Assert.That(paper.GetComponent<Shadow>().enabled, Is.True);
                Assert.That(player.speakerText.fontSize, Is.EqualTo(originalNameSize));
                Assert.That(player.speakerText.rectTransform.localScale, Is.EqualTo(originalNameScale));
                player.Skip();
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(fallback);
                Object.DestroyImmediate(fallbackCut);
                Object.DestroyImmediate(fallbackView);
            }
        }

        private static void CheckStyle(StoryPlayer player, string speaker)
        {
            Assert.That(player.speakerPalette.TryGet(speaker, out var style), Is.True);
            Assert.That(player.speakerBadge.color, Is.EqualTo(style.badgeColor));
            Assert.That(player.speakerBadge.material, Is.SameAs(player.speakerPalette.badgeMaterial));
            Assert.That(player.speakerText.color, Is.EqualTo(style.textColor));
        }
    }
}
#endif
