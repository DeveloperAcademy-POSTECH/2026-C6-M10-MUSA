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
        [TestCase(1)] [TestCase(2)]
        public void FullPlayingAndPendingStageRoomsAreKeptWithinTheirSelectedGroup(int group)
        {
            var rooms = LabRoomCatalog.GroupTargets(new[] {
                Target("stage-one", 7001, 1, 3, true, false, group),
                Target("stage-two", 7002, 2, 3, false, false, group),
                Target("stage-three", 7003, 3, 1, false, true, group)
            });
            for (int stage = 1; stage <= 3; stage++)
            {
                var chosen = (LabDiscoveredRoom)typeof(GameSession).GetMethod("DemoRoomForStage", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { rooms, stage, group });
                Assert.That(chosen, Is.Not.Null, "A busy or waiting room blocks additional group creation.");
                Assert.That(chosen.DemoStageIndex, Is.EqualTo(stage));
                Assert.That(chosen.DemoGroupId, Is.EqualTo(group));
                if (stage == 1) Assert.That(chosen.Playing, Is.True);
                if (stage == 2) Assert.That(chosen.IsFull, Is.True);
                if (stage == 3) Assert.That(chosen.WaitingForHost, Is.True);
            }
        }

        [Test]
        public void AnotherGroupCannotBlockCreationOrBecomeAJoinCandidate()
        {
            var rooms = LabRoomCatalog.GroupTargets(new[] {
                Target("a-full", 7001, 2, 3, false, false, 1),
                Target("a-playing", 7002, 2, 3, true, false, 1),
                Target("a-waiting", 7003, 2, 1, false, true, 1),
                Target("ungrouped", 7004, 2, 1, false, false, 0)
            });
            var select = typeof(GameSession).GetMethod("DemoRoomForStage", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(select.Invoke(null, new object[] { rooms, 2, 2 }), Is.Null,
                "Group B may create its own room when only A or ungrouped rooms exist.");
            Assert.That(select.Invoke(null, new object[] { rooms, 2, 0 }), Is.Null);
            Assert.That(select.Invoke(null, new object[] { rooms, 2, 3 }), Is.Null);
            var both = LabRoomCatalog.GroupTargets(new[] {
                Target("a-first", 7001, 2, 1, false, false, 1),
                Target("b-second", 7002, 2, 1, false, false, 2)
            });
            Assert.That(((LabDiscoveredRoom)select.Invoke(null, new object[] { both, 2, 2 })).DemoGroupId, Is.EqualTo(2));
        }

        [Category("Bonjour")]
        [TestCase(1, 1)] [TestCase(2, 1)] [TestCase(3, 1)] [TestCase(2, 2)]
        public void StageCreationIsExplicitAndCancelReleasesTheReservation(int stage, int group)
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Pending rooms require macOS/iOS.");
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var registry = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreate(3, out _), Is.False,
                    "The ordinary entry cannot publish an unidentified staff-demo room.");
                Assert.That(registry.TryCreateStaffStage(stage, group, out GamePendingRoomHandle handle, out string error), Is.True, error);
                Assert.That(registry.TryGet(handle, out GamePendingRoomInfo info), Is.True);
                Assert.That((info.DemoStageIndex, info.DemoGroupId, info.Capacity, info.Mode), Is.EqualTo((stage, group, 3, GameMode.Story)));
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
                Assert.That(registry.TryCreateStaffStage(stage, 1, out _, out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(registry.Rooms, Is.Empty);
            }
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(3)]
        public void InvalidGroupCannotReserveARoom(int group)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var registry = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreateStaffStage(2, group, out _, out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(registry.Rooms, Is.Empty);
            }
        }

        private static LabBonjourTarget Target(string name, ushort port, int stage, int players, bool playing, bool waiting, int group)
            => new LabBonjourTarget("192.0.2.10", port, 1, name, players, 3, playing, waiting, false, stage, group);
    }
}
