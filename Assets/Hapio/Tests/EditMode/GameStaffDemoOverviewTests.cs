using System.Linq;
using System.Reflection;
using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    public sealed class GameStaffDemoOverviewTests
    {
        private static string Status(LabDiscoveredRoom room, bool failed = false, bool searching = false)
            => (string)typeof(GameSession).GetMethod("DemoRoomStatus", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { room, failed, searching });

        [Test]
        public void SearchFailureAndInitialSearchNeverClaimThereIsNoRoom()
        {
            Assert.That(Status(null, failed: true), Does.Contain("확인 불가"));
            Assert.That(Status(null, searching: true), Does.Contain("검색 중"));
            Assert.That(Status(null), Does.Contain("방 없음"));
        }

        [TestCase(1, false, false, "1/3 · 입장 가능")]
        [TestCase(2, false, false, "2/3 · 입장 가능")]
        [TestCase(3, false, false, "3/3 · 정원 완료")]
        [TestCase(3, true, false, "3/3 · 진행 중")]
        [TestCase(1, false, true, "1/3 · 방 생성 중")]
        public void ExistingPresenceDrivesCountAndAdmissionStatus(int players, bool busy, bool pending, string expected)
        {
            LabDiscoveredRoom room = LabRoomCatalog.GroupTargets(new[]
            {
                new LabBonjourTarget("192.0.2.1", 19001, 1, "Staff-A", players, 3, busy, pending, false, 2, 1)
            }).Single();
            Assert.That(Status(room), Is.EqualTo(expected));
        }

        [Test]
        public void AFailedRefreshDoesNotPresentCachedOccupancyAsConfirmed()
        {
            LabDiscoveredRoom cached = LabRoomCatalog.GroupTargets(new[]
            {
                new LabBonjourTarget("192.0.2.1", 19001, 1, "Staff-A", 2, 3, false, false, false, 2, 1)
            }).Single();
            Assert.That(Status(cached, failed: true), Does.Contain("확인 불가"));
        }
    }
}
