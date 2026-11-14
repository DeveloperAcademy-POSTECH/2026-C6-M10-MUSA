using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameStaffStageDemoRoomTests
    {
        [Test]
        public void FullPlayingAndPendingStageRoomsAreKeptInsteadOfCreatingAnotherGroup()
        {
            var rooms = LabRoomCatalog.GroupTargets(new[] {
                Target("stage-one", 7001, 1, 3, true, false),
                Target("stage-two", 7002, 2, 3, false, false),
                Target("stage-three", 7003, 3, 1, false, true)
            });
            for (int stage = 1; stage <= 3; stage++)
            {
                var chosen = (LabDiscoveredRoom)typeof(GameSession).GetMethod("DemoRoomForStage", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { rooms, stage });
                Assert.That(chosen, Is.Not.Null, "A busy or waiting room blocks additional group creation.");
                Assert.That(chosen.DemoStageIndex, Is.EqualTo(stage));
                if (stage == 1) Assert.That(chosen.Playing, Is.True);
                if (stage == 2) Assert.That(chosen.IsFull, Is.True);
                if (stage == 3) Assert.That(chosen.WaitingForHost, Is.True);
            }
        }

        [Category("Bonjour")]
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void StageCreationIsExplicitAndCancelReleasesTheReservation(int stage)
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Pending rooms require macOS/iOS.");
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var registry = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreate(3, out _), Is.False,
                    "The ordinary entry cannot publish an unidentified staff-demo room.");
                Assert.That(registry.TryCreateStaffStage(stage, out GamePendingRoomHandle handle, out string error), Is.True, error);
                Assert.That(registry.TryGet(handle, out GamePendingRoomInfo info), Is.True);
                Assert.That((info.DemoStageIndex, info.Capacity, info.Mode), Is.EqualTo((stage, 3, GameMode.Story)));
                Assert.That(registry.Cancel(handle), Is.True);
                Assert.That(registry.Rooms, Is.Empty);
                Assert.That(registry.Cancel(handle), Is.False);
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    Assert.DoesNotThrow(() => socket.Bind(new IPEndPoint(IPAddress.Any, info.Port)));
            }
        }

        [TestCase(0)] [TestCase(4)] [TestCase(-1)]
        public void InvalidStageCannotReserveARoom(int stage)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var registry = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreateStaffStage(stage, out _, out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(registry.Rooms, Is.Empty);
            }
        }

        private static LabBonjourTarget Target(string name, ushort port, int stage, int players, bool playing, bool waiting)
            => new LabBonjourTarget("192.0.2.10", port, 1, name, players, 3, playing, waiting, false, stage);
    }
}
