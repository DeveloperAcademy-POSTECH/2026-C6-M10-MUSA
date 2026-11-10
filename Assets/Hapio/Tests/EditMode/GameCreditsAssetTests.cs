using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>크레딧 글 (#602): 출처 표시 의무가 있는 소리 문구가 빠지지 않는다.</summary>
    public sealed class GameCreditsAssetTests
    {
        private const string CreditsPath = "Assets/Hapio/Art/UI/Credits/Credits.txt";

        [TestCase("동살풀이")]
        [TestCase("장구-덩-강")]
        [TestCase("추임새(작성자: 정회석, 강효주, 이춘희, 염경애)")]
        [TestCase("양금 기본음 강")]
        [TestCase("25현 가야금 - 기본음계 - 강")]
        [TestCase("25현 가야금 - 시김새 - 추성 - 강")]
        public void CreditsCarryTheKoglType1Attribution(string work)
        {
            string text = Load();
            Assert.That(text, Does.Contain("공공누리 제1유형으로 개방한 '" + work));
            Assert.That(text, Does.Contain("https://www.gugak.go.kr/digitaleum/front/monotone/list.do"));
        }

        [Test]
        public void CreditsCarryTheCcByAttribution()
        {
            Assert.That(Load(), Does.Contain("김용배의 \"뻥튀기 뻥2\"").And.Contain("CC BY"));
        }

        private static string Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath);
            Assert.That(asset, Is.Not.Null, CreditsPath);
            return asset.text;
        }
    }
}
