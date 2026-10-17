using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabDrumMinimapTests
    {
        [Test]
        public void MeIsAtTheBottomAndTheRightNeighbourIsUpRightForEveryRoomSize()
        {
            for (int n = 2; n <= LabModel.MaximumParticipants; n++)
            {
                Vector2 me = LabDrumMinimap.UnitPosition(0, n);
                Assert.That(me.x, Is.EqualTo(0f).Within(1e-5f), n + " players: I am centred.");
                Assert.That(me.y, Is.EqualTo(-1f).Within(1e-5f), n + " players: I am at the bottom.");
                if (n < 3) continue;
                Assert.That(LabDrumMinimap.UnitPosition(1, n).x, Is.GreaterThan(0f), n + " players: right neighbour is on the right.");
                Assert.That(LabDrumMinimap.UnitPosition(n - 1, n).x, Is.LessThan(0f), n + " players: left neighbour is on the left.");
            }
            // 3인은 정삼각형: 위쪽 두 자리가 30°, 150°
            Assert.That(LabDrumMinimap.UnitPosition(1, 3).y, Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(LabDrumMinimap.UnitPosition(2, 3).y, Is.EqualTo(.5f).Within(1e-5f));
        }

        [Test]
        public void EachPhonePutsItsOwnSeatAtTheBottomOfAnEquilateralTriangle()
        {
            var root = new GameObject("ui", typeof(RectTransform));
            var badge = new GameObject("badge", typeof(RectTransform));
            badge.transform.SetParent(root.transform, false);
            var minimap = new GameObject("minimap").AddComponent<LabDrumMinimap>();
            try
            {
                minimap.Bind(null, (RectTransform)badge.transform, null);
                var players = new[]
                {
                    new LabPlayerState { id = 10, seat = 0 },
                    new LabPlayerState { id = 20, seat = 1 },
                    new LabPlayerState { id = 30, seat = 2 }
                };
                minimap.Rebuild(players, 20); // 나는 P2
                Vector2 me = minimap.SeatPositions[20];
                Vector2 right = minimap.SeatPositions[30]; // 다음 번호 = 오른쪽 이웃
                Vector2 left = minimap.SeatPositions[10];  // 이전 번호 = 왼쪽 이웃
                Assert.That(me.x, Is.EqualTo(0f).Within(.01f));
                Assert.That(me.y, Is.LessThan(right.y));
                Assert.That(right.x, Is.GreaterThan(0f));
                Assert.That(left.x, Is.LessThan(0f));
                Assert.That(right.y, Is.EqualTo(left.y).Within(.01f));
                float a = Vector2.Distance(me, right), b = Vector2.Distance(right, left), c = Vector2.Distance(left, me);
                Assert.That(b, Is.EqualTo(a).Within(.01f), "All sides are equal.");
                Assert.That(c, Is.EqualTo(a).Within(.01f), "All sides are equal.");
            }
            finally
            {
                Object.DestroyImmediate(minimap.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheTaegeukSitsOnTheDrumHolderAndSlidesToTheNextHolder()
        {
            var root = new GameObject("ui", typeof(RectTransform));
            var badge = new GameObject("badge", typeof(RectTransform));
            badge.transform.SetParent(root.transform, false);
            var minimap = new GameObject("minimap").AddComponent<LabDrumMinimap>();
            try
            {
                minimap.Bind(null, (RectTransform)badge.transform, null);
                minimap.slideSeconds = .4f;
                var players = new[]
                {
                    new LabPlayerState { id = 10, seat = 0 },
                    new LabPlayerState { id = 20, seat = 1 },
                    new LabPlayerState { id = 30, seat = 2 }
                };
                minimap.Rebuild(players, 10);
                minimap.SyncDrums(new[] { new LabOrbState { id = "drum", owner = 20, kind = LabOrbKind.Drum } });
                Assert.That(minimap.DrumPosition("drum"), Is.EqualTo(minimap.SeatPositions[20]), "A new drum appears on its holder.");

                minimap.SyncDrums(new[] { new LabOrbState { id = "drum", owner = 30, kind = LabOrbKind.Drum } });
                Assert.That(minimap.DrumOwner("drum"), Is.EqualTo(30ul));
                minimap.Advance(.2f);
                Vector2 half = (minimap.SeatPositions[20] + minimap.SeatPositions[30]) * .5f;
                Assert.That(Vector2.Distance(minimap.DrumPosition("drum").Value, half), Is.LessThan(.01f),
                    "Halfway through the slide it is in the middle of the line between the two players.");
                minimap.Advance(1f);
                Assert.That(minimap.DrumPosition("drum"), Is.EqualTo(minimap.SeatPositions[30]), "It ends on the new holder.");

                minimap.SyncDrums(new LabOrbState[0]);
                Assert.That(minimap.DrumPosition("drum"), Is.Null, "A drum that disappears loses its taegeuk.");
            }
            finally
            {
                Object.DestroyImmediate(minimap.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ADrumThatPassesThroughAPlayerSlidesAlongTheRingNotAcrossIt()
        {
            var root = new GameObject("ui", typeof(RectTransform));
            var badge = new GameObject("badge", typeof(RectTransform));
            badge.transform.SetParent(root.transform, false);
            var minimap = new GameObject("minimap").AddComponent<LabDrumMinimap>();
            try
            {
                minimap.Bind(null, (RectTransform)badge.transform, null);
                minimap.slideSeconds = .4f;
                var players = new[]
                {
                    new LabPlayerState { id = 10, seat = 0 }, new LabPlayerState { id = 20, seat = 1 },
                    new LabPlayerState { id = 30, seat = 2 }, new LabPlayerState { id = 40, seat = 3 },
                    new LabPlayerState { id = 50, seat = 4 }
                };
                minimap.Rebuild(players, 10);
                minimap.SyncDrums(new[] { new LabOrbState { id = "drum", owner = 10, kind = LabOrbKind.Drum } });
                minimap.SyncDrums(new[] { new LabOrbState { id = "drum", owner = 30, kind = LabOrbKind.Drum } });
                minimap.Advance(.4f);
                Assert.That(Vector2.Distance(minimap.DrumPosition("drum").Value, minimap.SeatPositions[20]), Is.LessThan(.01f),
                    "Passing P2 on the way to P3, it visits P2's circle first.");
                minimap.Advance(.4f);
                Assert.That(minimap.DrumPosition("drum"), Is.EqualTo(minimap.SeatPositions[30]));
            }
            finally
            {
                Object.DestroyImmediate(minimap.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
