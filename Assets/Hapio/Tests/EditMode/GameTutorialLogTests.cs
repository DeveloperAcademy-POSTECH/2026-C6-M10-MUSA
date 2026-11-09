using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>튜토리얼 대화 기록 (#567): 기록 버튼 자리와 그림 연결.</summary>
    public sealed class GameTutorialLogTests
    {
        [Test]
        public void LogButtonSitsRightUnderSkipOnItsMiddle()
        {
            Vector2 center = GameTutorialDirector.LogButtonCenter(new Vector2(-100f, 300f), 30f, 30f, 6f);
            Assert.That(center.x, Is.EqualTo(-100f).Within(1e-4f), "On the skip button's middle.");
            Assert.That(center.y, Is.EqualTo(300f - 15f - 6f - 15f).Within(1e-4f), "The gap under the skip button.");
        }

        [Test]
        public void LogButtonIsLabeledAndAsTallAsTheSkipButton()
        {
            Assert.That(GameTutorialScript.LogButtonLabel, Is.EqualTo("기록"));
            GameTutorialArt art = GameTutorialArt.Load();
            Assert.That(art, Is.Not.Null);
            Assert.That(art.skipButton, Is.Not.Null, "The frame both buttons share.");
            Assert.That(art.logButtonSize.y, Is.EqualTo(art.skipButtonSize.y));
        }

        [Test]
        public void LogLinesReadAsOneSentence()
        {
            Assert.That(GameTutorialScript.LogLine("에이~\n그냥 던지면 되는 거 아냐?"), Is.EqualTo("에이~ 그냥 던지면 되는 거 아냐?"));
            Assert.That(GameTutorialScript.LogLine("맞아! 너가 북을 줘야지... \n옆으로 밀면 넘어가. "), Is.EqualTo("맞아! 너가 북을 줘야지... 옆으로 밀면 넘어가."));
            Assert.That(GameTutorialScript.LogLine("풀렸다!"), Is.EqualTo("풀렸다!"));
            Assert.That(GameTutorialScript.LogBackLabel, Is.EqualTo("뒤로 가기"));
        }

        [Test]
        public void BarsStackAndTheListOpensAtItsNewestLine()
        {
            Assert.That(GameTutorialDirector.LogStackHeight(0, 55f, 22f), Is.EqualTo(0f));
            Assert.That(GameTutorialDirector.LogStackHeight(1, 55f, 22f), Is.EqualTo(55f));
            Assert.That(GameTutorialDirector.LogStackHeight(3, 55f, 22f), Is.EqualTo(209f));
            Assert.That(GameTutorialDirector.LogScrollToEnd(440f, 209f), Is.EqualTo(231f), "Moved up so the bottom shows.");
            Assert.That(GameTutorialDirector.LogScrollToEnd(132f, 209f), Is.EqualTo(0f), "Nothing to move when it all fits.");
        }

        [Test]
        public void ShortWordsSitInTheMiddleOfTheBarAndLongOnesUseTheRoomBesideTheFace()
        {
            Assert.That(GameTutorialDirector.LogWordsSpan(321f, 58f, 16f, 120f), Is.EqualTo(new Vector2(58f, 263f)), "Centered on the bar.");
            Assert.That(GameTutorialDirector.LogWordsSpan(321f, 58f, 16f, 205f), Is.EqualTo(new Vector2(58f, 263f)), "Just fits.");
            Assert.That(GameTutorialDirector.LogWordsSpan(321f, 58f, 16f, 400f), Is.EqualTo(new Vector2(58f, 305f)), "Up to the right side.");
        }

        /// <summary>Every scripted line fits its bar in at most two lines, whole, measured with the bar's own font and box.</summary>
        [Test]
        public void EveryScriptedLineFitsItsBarInTwoLines()
        {
            GameTutorialArt art = GameTutorialArt.Load();
            Assert.That(art.logBar, Is.Not.Null, "The bar picture.");
            Assert.That(art.bannerFont, Is.Not.Null);
            var probe = new GameObject("Log line probe");
            try
            {
                var text = probe.AddComponent<UnityEngine.UI.Text>();
                text.font = art.bannerFont;
                text.fontSize = art.logTextFontSize;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.supportRichText = false;
                float height = art.logTextFontSize * GameTutorialDirector.LogWordsLineHeights;
                foreach (GameTutorialStep step in GameTutorialScript.Steps)
                    foreach (GameTutorialBeat line in step.Lines)
                    {
                        string words = GameTutorialScript.LogLine(line.Text);
                        text.resizeTextForBestFit = false;
                        float unwrapped = new TextGenerator().GetPreferredWidth(words, text.GetGenerationSettings(Vector2.zero));
                        Vector2 span = GameTutorialDirector.LogWordsSpan(art.logBarSize.x, art.logTextLeft, art.logTextRight, unwrapped);
                        text.resizeTextForBestFit = true;
                        text.resizeTextMaxSize = art.logTextFontSize;
                        text.resizeTextMinSize = art.logMinTextFontSize;
                        var generator = new TextGenerator();
                        generator.PopulateWithErrors(words, text.GetGenerationSettings(new Vector2(span.y - span.x, height)), probe);
                        Assert.That(generator.lineCount, Is.LessThanOrEqualTo(2), step.Name + ": " + words);
                        Assert.That(generator.fontSizeUsedForBestFit, Is.GreaterThanOrEqualTo(art.logMinTextFontSize), step.Name + ": " + words);
                    }
            }
            finally { Object.DestroyImmediate(probe); }
        }
    }
}
