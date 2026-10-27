using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace C6Lab.Tests
{
    public sealed class LabDrumMinimapTests
    {
        [Test]
        public void MeIsAtTheBottomAndTheNextPIsUpLeftForEveryRoomSize()
        {
            for (int n = 2; n <= LabModel.MaximumParticipants; n++)
            {
                Vector2 me = LabDrumMinimap.UnitPosition(0, n);
                Assert.That(me.x, Is.EqualTo(0f).Within(1e-5f), n + " players: I am centred.");
                Assert.That(me.y, Is.EqualTo(-1f).Within(1e-5f), n + " players: I am at the bottom.");
                if (n < 3) continue;
                Assert.That(LabDrumMinimap.UnitPosition(1, n).x, Is.LessThan(0f), n + " players: the next P is on the left.");
                Assert.That(LabDrumMinimap.UnitPosition(n - 1, n).x, Is.GreaterThan(0f), n + " players: the previous P is on the right.");
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
                Vector2 right = minimap.SeatPositions[10]; // 이전 번호 = 오른쪽 이웃
                Vector2 left = minimap.SeatPositions[30];  // 다음 번호 = 왼쪽 이웃
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
        [Test]
        public void ConceptSpritesFollowActualSeatsAndDrumMarkersDoNotCoverTheirRingColours()
        {
            var root = new GameObject("concept ui", typeof(RectTransform));
            var badge = new GameObject("badge", typeof(RectTransform));
            badge.transform.SetParent(root.transform, false);
            var minimap = new GameObject("map").AddComponent<LabDrumMinimap>();
            try
            {
                ApplyConceptSprites(minimap);
                minimap.Bind(null, (RectTransform)badge.transform, null);
                var players = new[] {
                    new LabPlayerState { id = 50, seat = 4 }, new LabPlayerState { id = 10, seat = 0 },
                    new LabPlayerState { id = 40, seat = 3 }, new LabPlayerState { id = 20, seat = 1 },
                    new LabPlayerState { id = 30, seat = 2 }
                };
                minimap.Rebuild(players, 40);
                var panel = root.transform.Find("DrumMinimap");
                Assert.That(panel.GetComponent<Image>(), Is.Null, "The concept map has no rectangular backing.");
                var seats = panel.Find("Content/Seats");
                int[] numbers = { 4, 5, 1, 2, 3 };
                for (int i = 0; i < numbers.Length; i++)
                {
                    var image = seats.GetChild(i).GetComponent<Image>();
                    Assert.That(image.sprite, Is.SameAs(minimap.playerSprites[numbers[i] - 1]));
                    Assert.That(image.color, Is.EqualTo(Color.white));
                }
                Assert.That(panel.Find("Content/Labels").childCount, Is.Zero,
                    "The supplied badges contain the number; a second label must not cover the drum.");
                Assert.That(minimap.SeatPositions[50].x, Is.LessThan(minimap.SeatPositions[40].x));
                minimap.SyncDrums(new[] {
                    new LabOrbState { id = "a", owner = 20, kind = LabOrbKind.Drum },
                    new LabOrbState { id = "b", owner = 50, kind = LabOrbKind.Drum }
                });
                Assert.That(panel.Find("Content/Drums").childCount, Is.EqualTo(2));
                Assert.That(panel.Find("Content/Drums").GetChild(0).GetComponent<Image>().sprite,
                    Is.SameAs(minimap.drumIconSprite));
                Assert.That(minimap.DrumPosition("a"), Is.EqualTo(minimap.SeatPositions[20]));
                minimap.SyncDrums(new[] { new LabOrbState { id = "a", owner = 30, kind = LabOrbKind.Drum } });
                minimap.Advance(1f);
                Assert.That(minimap.DrumPosition("a"), Is.EqualTo(minimap.SeatPositions[30]));
                Assert.That(minimap.DrumPosition("b"), Is.Null);
            }
            finally { Object.DestroyImmediate(minimap.gameObject); Object.DestroyImmediate(root); }
        }

        [Test]
        public void SoloUsesTheSuppliedDrumTokenAndReturnsToItsNumberWhenTheDrumDisappears()
        {
            var root = new GameObject("solo ui", typeof(RectTransform));
            var badge = new GameObject("badge", typeof(RectTransform));
            badge.transform.SetParent(root.transform, false);
            var minimap = new GameObject("map").AddComponent<LabDrumMinimap>();
            try
            {
                ApplyConceptSprites(minimap);
                minimap.Bind(null, (RectTransform)badge.transform, null);
                minimap.Rebuild(new[] { new LabPlayerState { id = 10, seat = 0 } }, 10);
                var seat = root.transform.Find("DrumMinimap/Content/Seats").GetChild(0).GetComponent<Image>();
                Assert.That(seat.sprite, Is.SameAs(minimap.playerSprites[0]));
                minimap.SyncDrums(new[] { new LabOrbState { id = "solo", owner = 10, kind = LabOrbKind.Drum } });
                Assert.That(seat.sprite, Is.SameAs(minimap.soloDrumSprite));
                Assert.That(minimap.DrumPosition("solo"), Is.EqualTo(Vector2.zero));
                minimap.SyncDrums(new LabOrbState[0]);
                Assert.That(seat.sprite, Is.SameAs(minimap.playerSprites[0]));
            }
            finally { Object.DestroyImmediate(minimap.gameObject); Object.DestroyImmediate(root); }
        }

        private static void ApplyConceptSprites(LabDrumMinimap map)
        {
            const string art = "Assets/Lab/Art/UI/";
            map.playerSprites = new Sprite[5];
            for (int i = 0; i < 5; i++)
                map.playerSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(art + "Player" + (i + 1) + "Circle.png");
            map.drumIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(art + "PlayerPositionDrumIcon.png");
            map.soloDrumSprite = AssetDatabase.LoadAssetAtPath<Sprite>(art + "PlayerPositionSoloIcon.png");
            Assert.That(map.drumIconSprite, Is.Not.Null);
            Assert.That(map.soloDrumSprite, Is.Not.Null);
            map.size = new Vector2(151, 142);
        }
    }
}
