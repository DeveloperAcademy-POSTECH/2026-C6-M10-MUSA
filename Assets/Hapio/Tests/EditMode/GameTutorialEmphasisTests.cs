using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Tests
{
    /// <summary>튜토리얼 대사 키워드 강조 (#727): 대본 표시, 리치 텍스트, 말풍선 맞춤, 오행 글자 테두리.</summary>
    public sealed class GameTutorialEmphasisTests
    {
        private static readonly Color Red = new Color32(0x7F, 0x1C, 0x0D, 0xFF);

        [Test]
        public void TheAgreedKeywordsAreMarkedWhileTheWordsStayTheApprovedScript()
        {
            var lines = GameTutorialScript.Steps.SelectMany(step => step.Lines).ToArray();
            foreach (GameTutorialBeat line in lines)
            {
                Assert.That(line.Text, Does.Not.Contain("<"), "No mark leaks into the words the log and tests read.");
                Assert.That(line.Text, Is.EqualTo(GameTutorialEmphasis.Strip(line.Marked)));
            }
            string[] marked = lines.Where(line => line.Marked != line.Text).Select(line => line.Marked).ToArray();
            Assert.That(marked, Is.EquivalentTo(new[]
            {
                "봉인박을 오행의 기운으로\n<big>빈틈없이 칠하면</big>\n요괴를 가둘 수 있어.",
                "먼저 <big>북</big>을 쳐 봐.",
                "<big>음구슬</big>이나 <big>양구슬</big>이\n튀어나올 거야.",
                "북을 칠 때마다 <red>기운이 줄어</red>.",
                "<red>같은 색끼리는 안 붙어!</red>",
                "그게 <big>오행구슬</big>이야.",
                "<fire>화</fire>·<water>수</water>·<wood>목</wood>·<metal>금</metal>·<earth>토</earth> 중\n하나가 나와.",
                "박이 <big>100%</big> 칠해질 때까지\n계속 칠하면 돼!",
                "구슬은 <red>오래 두면 사라져</red>.\n빛이 나기 시작하면 곧 없어져!",
                "요괴가 던진 폭탄이야!\n<big>적힌 숫자만큼</big> 옆으로\n넘기면 풀려.",
                "풀린 폭탄은\n오행 구슬 <big>두 개</big>가 돼.",
                "가만히 냅두면 <red>터지니까</red>\n얼른얼른 넘겨야겠네!",
                "지금 북이 어디에 있는지는\n<big>우측 지도</big>에서 볼 수 있어!",
                "못 쓰는 구슬은 쥐고 있지 말고\n<big>바로바로 넘겨 줘</big>!",
                "어? 오른쪽이 <red>빨개졌어</red>!",
                "요괴가 오른쪽을\n<red>막은 것 같아</red>!",
                "앞으로 나올 요괴들이\n<red>어떤 방해</red>를 할지 모르니,\n조심하자!",
            }), "Exactly the keywords agreed for #727, nothing else: big for what to know and do, red for warnings.");
        }

        [Test]
        public void MarksBecomeBoldSizeAndColourAndTheLogKeepsBoldAndColoursOnly()
        {
            Assert.That(GameTutorialEmphasis.ToRichText("먼저 <big>북</big>을 쳐 봐.", 23, Red), Is.EqualTo("먼저 <size=23><b>북</b></size>을 쳐 봐."),
                "Every emphasis is bold too.");
            Assert.That(GameTutorialEmphasis.ToRichText("<red>같은 색끼리는 안 붙어!</red>", 23, Red),
                Is.EqualTo("<color=#7F1C0D><b>같은 색끼리는 안 붙어!</b></color>"));
            Assert.That(GameTutorialEmphasis.ToRichText("<fire>화</fire>·<metal>금</metal>", 23, Red),
                Is.EqualTo("<color=#D32E15><b>화</b></color>·<color=#F9B521><b>금</b></color>"), "The 5OrbsColor/*/500*** colour tokens.");
            Assert.That(GameTutorialEmphasis.ToRichText("<red>쳐</red>", 23, Red, bold: false), Is.EqualTo("<color=#7F1C0D>쳐</color>"),
                "Bold can be turned off in the art.");
            string log = GameTutorialScript.LogLine(GameTutorialEmphasis.ToRichText("먼저 <big>북</big>을\n<red>쳐</red> 봐.", 0, Red));
            Assert.That(log, Is.EqualTo("먼저 <b>북</b>을 <color=#7F1C0D><b>쳐</b></color> 봐."),
                "The log is one line, bold and coloured, never bigger.");
            Assert.That(GameTutorialEmphasis.BigSize(17, 1.35f), Is.EqualTo(23));
            Assert.That(GameTutorialEmphasis.BigSize(14, 1.35f), Is.EqualTo(19));
        }

        [Test]
        public void EveryMarkedLineKeepsItsWrittenBreaksAndTheBigWordsShrinkWithTheLine()
        {
            GameTutorialArt art = GameTutorialArt.Load();
            var root = new GameObject("Emphasis probe", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Text text = NewText(root, art);
                foreach (GameTutorialBeat line in GameTutorialScript.Steps.SelectMany(step => step.Lines).Where(line => line.Marked != line.Text))
                {
                    int size = GameTutorialEmphasis.FitBubble(text, line.Marked, art.maxFontSize, art.minFontSize, art.emphasisScale, art.emphasisRed);
                    var generator = new TextGenerator();
                    generator.PopulateWithErrors(text.text, text.GetGenerationSettings(text.rectTransform.rect.size), text.gameObject);
                    string label = line.Text.Replace("\n", " / ");
                    Assert.That(size, Is.InRange(art.minFontSize, art.maxFontSize), label);
                    Assert.That(generator.lineCount, Is.EqualTo(line.Text.Split('\n').Length), label + " breaks only where written");
                    Assert.That(text.resizeTextForBestFit, Is.False, "Fitted by hand, so the big words follow the line's size.");
                    if (line.Marked.Contains("<big>"))
                        Assert.That(text.text, Does.Contain("<size=" + GameTutorialEmphasis.BigSize(size, art.emphasisScale) + "><b>"), label);
                }

                // A cramped bubble: the line shrinks, and its big words with it, at the same ratio.
                text.rectTransform.sizeDelta = new Vector2(140f, 66f);
                int small = GameTutorialEmphasis.FitBubble(text, "요괴가 던진 폭탄이야!\n<big>적힌 숫자만큼</big> 옆으로\n넘기면 풀려.",
                    art.maxFontSize, art.minFontSize, art.emphasisScale, art.emphasisRed);
                Assert.That(small, Is.LessThan(art.maxFontSize));
                Assert.That(text.text, Does.Contain("<size=" + GameTutorialEmphasis.BigSize(small, art.emphasisScale) + ">"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TheElementLettersAreJustColouredAndAnOutlineCanOnlyCatchThoseFive()
        {
            GameTutorialArt art = GameTutorialArt.Load();
            Assert.That(art.elementOutlineWidth, Is.Zero, "As in the design: coloured letters only, no outline.");
            var root = new GameObject("Outline probe", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Text text = NewText(root, art);
                var outline = text.gameObject.AddComponent<GameTutorialElementOutline>();
                outline.width = 1.2f; // turned on here only to check that it would catch those five letters and nothing else
                GameTutorialEmphasis.FitBubble(text, GameTutorialScript.Combine.Lines.Last().Marked, art.maxFontSize, art.minFontSize,
                    art.emphasisScale, art.emphasisRed);
                text.Rebuild(CanvasUpdate.PreRender);
                Assert.That(outline.OutlinedGlyphs, Is.EqualTo(5), "화·수·목·금·토 only, not the dots or the rest.");

                GameTutorialEmphasis.FitBubble(text, "<red>같은 색끼리는 안 붙어!</red> <big>북</big>", art.maxFontSize, art.minFontSize,
                    art.emphasisScale, art.emphasisRed);
                text.Rebuild(CanvasUpdate.PreRender);
                Assert.That(outline.OutlinedGlyphs, Is.Zero, "Red and big words keep their plain look.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Text NewText(GameObject root, GameTutorialArt art)
        {
            var text = new GameObject("Line", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(root.transform, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.sizeDelta = new Vector2(art.bubbleSize.x * (1f - art.textInset.x - art.textInset.z),
                art.bubbleSize.y * (1f - art.textInset.y - art.textInset.w));
            text.font = art.dialogueFont;
            text.color = art.textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
    }
}
