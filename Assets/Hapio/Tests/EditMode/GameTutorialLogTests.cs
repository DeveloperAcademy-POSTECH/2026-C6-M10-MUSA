using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>튜토리얼 대화 기록 (#567): 기록 버튼 자리와 그림 연결.</summary>
    public sealed class GameTutorialLogTests
    {
        [Test]
        public void LogButtonSitsRightBesideSkipAtItsHeight()
        {
            Vector2 center = GameTutorialDirector.LogButtonCenter(new Vector2(-100f, 300f), 72f, 54f, 6f);
            Assert.That(center.x, Is.EqualTo(-100f + 36f + 6f + 27f).Within(1e-4f));
            Assert.That(center.y, Is.EqualTo(300f), "Level with the skip button.");
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
    }
}
