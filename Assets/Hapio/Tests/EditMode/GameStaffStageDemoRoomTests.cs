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

        [Test]
        public void SimultaneousUnenteredCandidatesUseTheSameWinnerRegardlessOfArrivalOrder()
        {
            var rooms = LabRoomCatalog.GroupTargets(new[] {
                Target("fixed-group", 7103, 2, 1, false, true, 1),
                Target("fixed-group", 7101, 2, 1, false, true, 1),
                Target("fixed-group", 7102, 2, 1, false, true, 1),
                Target("other-group", 7000, 2, 1, false, true, 2)
            });
            string first = LabRoomCatalog.Key("fixed-group", 7101);
            Assert.That(CreationWinner(rooms, first), Is.Null, "The smallest candidate keeps its own reservation.");
            foreach (string other in new[] { LabRoomCatalog.Key("fixed-group", 7102), LabRoomCatalog.Key("fixed-group", 7103) })
            {
                Assert.That(CreationWinner(rooms, other)?.Key, Is.EqualTo(first));
                Assert.That(CreationWinner(rooms.Reverse().ToArray(), other)?.Key, Is.EqualTo(first));
            }
        }

        [TestCase(false, 1)] [TestCase(false, 3)] [TestCase(true, 3)]
        public void AnAlreadyOpenedHostAlwaysBlocksAnotherCandidateEvenWhenItsKeySortsLater(bool playing, int players)
        {
            var rooms = LabRoomCatalog.GroupTargets(new[] {
                Target("fixed-group", 7101, 2, 1, false, true, 1),
                Target("fixed-group", 7199, 2, players, playing, false, 1),
                Target("other-stage", 7000, 1, 1, false, false, 1),
                Target("other-group", 7001, 2, 1, false, false, 2)
            });
            var winner = CreationWinner(rooms, LabRoomCatalog.Key("fixed-group", 7102));
            Assert.That(winner?.Port, Is.EqualTo(7199));
            Assert.That((winner.Playing, winner.IsFull), Is.EqualTo((playing, players == 3)),
                "Full and playing rooms stay blockers rather than being replaced by another Host.");
            var entryChoice = (LabDiscoveredRoom)typeof(GameSession).GetMethod("DemoRoomForStage", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { rooms, 2, 1 });
            Assert.That(entryChoice?.Key, Is.EqualTo(winner.Key),
                "A new entrant also follows the open Host instead of a lower-key pending advertisement.");
        }

        private static LabDiscoveredRoom CreationWinner(System.Collections.Generic.IReadOnlyList<LabDiscoveredRoom> rooms, string ownKey)
            => (LabDiscoveredRoom)typeof(GameSession).GetMethod("DemoCreationWinner", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { rooms, 2, 1, ownKey });

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
