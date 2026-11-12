using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameYaguangDemoLobbyTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void OnlyThreePlayerStoryIsSelectable(int count)
        {
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                Assert.That(GameLobbyView.ModeAllowed(GameMode.Story, count), Is.EqualTo(count == 3));
                Assert.That(GameLobbyView.ModeAllowed(GameMode.Territory, count), Is.False);
                if (count == 3) Assert.That(GameLobbyView.ModeBlockedReason(GameMode.Story, count), Is.Empty);
                else Assert.That(GameLobbyView.ModeBlockedReason(GameMode.Story, count), Is.Not.Empty);
            }
        }

        [TestCase(2, GameMode.Story)] [TestCase(4, GameMode.Story)] [TestCase(3, GameMode.Territory)]
        public void PendingRegistryRejectsWrongDemoRoomsBeforeAllocatingDiscovery(int count, GameMode mode)
        {
            using (LabGameBuildVariant.OverrideForTests(true))
            using (var registry = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreate(count, mode, out var handle, out string error), Is.False);
                Assert.That(handle, Is.Null);
                Assert.That(error, Is.Not.Empty);
                Assert.That(registry.Rooms, Is.Empty);
            }
        }
    }
}
