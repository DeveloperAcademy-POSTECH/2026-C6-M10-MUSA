using NUnit.Framework;

namespace Hapio.Tests
{
    /// <summary>#394: which mode the chosen player count can open, the reason under a blocked button, the room-card label and the seat hint.</summary>
    public sealed class GameLobbyModeRulesTests
    {
        [TestCase(2, false, true, "2명은 땅따먹기 1:1만 할 수 있소.", "")]
        [TestCase(3, true, false, "", "땅따먹기는 2·4·6명만 할 수 있소.")]
        [TestCase(4, true, true, "", "")]
        [TestCase(5, true, false, "", "땅따먹기는 2·4·6명만 할 수 있소.")]
        [TestCase(6, false, true, "6명은 땅따먹기 3:3만 할 수 있소.", "")]
        public void StoryOpensForThreeToFiveAndTerritoryForTwoFourOrSix(int players, bool story, bool territory,
            string storyReason, string territoryReason)
        {
            Assert.That(GameLobbyView.ModeAllowed(GameMode.Story, players), Is.EqualTo(story));
            Assert.That(GameLobbyView.ModeAllowed(GameMode.Territory, players), Is.EqualTo(territory));
            Assert.That(GameLobbyView.ModeBlockedReason(GameMode.Story, players), Is.EqualTo(storyReason));
            Assert.That(GameLobbyView.ModeBlockedReason(GameMode.Territory, players), Is.EqualTo(territoryReason));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(7)]
        public void NoModeOpensWithoutAPlayableCount(int players)
        {
            Assert.That(GameLobbyView.ModeAllowed(GameMode.Story, players), Is.False);
            Assert.That(GameLobbyView.ModeAllowed(GameMode.Territory, players), Is.False);
            Assert.That(GameLobbyView.ModeBlockedReason(GameMode.Story, players), Is.Not.Empty);
            Assert.That(GameLobbyView.ModeBlockedReason(GameMode.Territory, players), Is.Not.Empty);
        }

        [TestCase(2, "원하는 자리를 선택 후, 준비하시오.")]
        [TestCase(4, "실제로 앉은 위치의 빈 자리를 눌러주시오.")]
        [TestCase(6, "3명씩 함께 앉아, 준비하시오.")]
        public void TeamSeatScreenHintFollowsTheMatchSize(int players, string hint)
        {
            Assert.That(GameLobbyView.TeamSeatHint(players), Is.EqualTo(hint));
        }

        [TestCase(2, "1:1 땅따먹기 · ")]
        [TestCase(4, "2:2 땅따먹기 · ")]
        [TestCase(6, "3:3 땅따먹기 · ")]
        [TestCase(0, "땅따먹기 · ")]
        public void TeamRoomCardNamesItsMatchSize(int capacity, string label)
        {
            Assert.That(GameLobbyView.TeamRoomLabel(capacity), Is.EqualTo(label));
        }
    }
}
