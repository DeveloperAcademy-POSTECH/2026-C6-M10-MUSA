using System.Collections.Generic;
using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    /// <summary>북 넘기기 안내 (#718)의 규칙: 북이 내 판에 머문 시간, 가장 오래된 북, 넘길 수 있는 쪽, 손이 가리키는 쪽.</summary>
    public sealed class GameDrumPassHintTests
    {
        private const ulong Me = 1, Left = 2, Right = 3;

        [Test]
        public void ADrumCountsWhileItStaysOnMyBoardEvenWhenTappedAndStartsOverOnceItLeaves()
        {
            var held = new Dictionary<string, float>();
            var scratch = new List<string>();
            LabSnapshot snapshot = Story(Drum("a", Me), Drum("b", Left));
            for (int i = 0; i < 8; i++) GameDrumPassHint.Track(held, scratch, snapshot, Me, 1f);
            Assert.That(GameDrumPassHint.Longest(held, out float seconds), Is.EqualTo("a"));
            Assert.That(seconds, Is.EqualTo(8f).Within(.001f), "Tapping the drum to make orbs does not reset it; only passing does.");
            Assert.That(held.ContainsKey("b"), Is.False, "Someone else's drum is not mine to count.");

            GameDrumPassHint.Track(held, scratch, Story(Drum("a", Right), Drum("b", Left)), Me, 1f);
            Assert.That(GameDrumPassHint.Longest(held, out _), Is.Null, "Passed on: its time is gone.");

            GameDrumPassHint.Track(held, scratch, snapshot, Me, 2f);
            Assert.That(GameDrumPassHint.Longest(held, out seconds), Is.EqualTo("a"));
            Assert.That(seconds, Is.EqualTo(2f).Within(.001f), "Back on my board, it counts from the start again.");
        }

        [Test]
        public void TheLongestHeldDrumIsTheOnePointedAt()
        {
            var held = new Dictionary<string, float>();
            var scratch = new List<string>();
            GameDrumPassHint.Track(held, scratch, Story(Drum("old", Me)), Me, 5f);
            GameDrumPassHint.Track(held, scratch, Story(Drum("old", Me), Drum("new", Me)), Me, 1f);
            Assert.That(GameDrumPassHint.Longest(held, out float seconds), Is.EqualTo("old"));
            Assert.That(seconds, Is.EqualTo(6f).Within(.001f));
        }

        [Test]
        public void OnlyTheSidesADrumCanBePassedToArePointedAt()
        {
            LabSnapshot story = Story(Drum("a", Me));
            Assert.That(GameDrumPassHint.OpenSides(story, Me), Is.EqualTo(GameDrumPassHint.RightSide | GameDrumPassHint.LeftSide));

            story.swipeBlockedPlayer = Me; story.swipeBlockedRight = true; story.swipeBlockedUntil = story.serverTime + 5d;
            Assert.That(GameDrumPassHint.OpenSides(story, Me), Is.EqualTo(GameDrumPassHint.LeftSide), "The gourd blocks the right edge.");
            story.swipeBlockedRight = false;
            Assert.That(GameDrumPassHint.OpenSides(story, Me), Is.EqualTo(GameDrumPassHint.RightSide), "The gourd blocks the left edge.");
            story.swipeBlockedUntil = story.serverTime - 1d;
            Assert.That(GameDrumPassHint.OpenSides(story, Me), Is.EqualTo(GameDrumPassHint.RightSide | GameDrumPassHint.LeftSide));

            story.charmedPlayer = Me; story.charmStartedAt = story.serverTime - 1d; story.charmUntil = story.serverTime + 5d;
            Assert.That(GameDrumPassHint.OpenSides(story, Me), Is.Zero, "A charmed board stands still.");

            LabSnapshot solo = Story(Drum("a", Me));
            solo.players = new[] { Player(Me, 0) };
            Assert.That(GameDrumPassHint.OpenSides(solo, Me), Is.Zero, "Nobody to pass to.");
            Assert.That(GameDrumPassHint.OpenSides(solo, Me, solo: true), Is.EqualTo(GameDrumPassHint.RightSide | GameDrumPassHint.LeftSide),
                "DEV solo still shows it both ways, though nothing is passed.");

            LabSnapshot oneOnOne = Story(Drum("a", Me));
            oneOnOne.teamTerritory = true;
            oneOnOne.players = new[] { Player(Me, 0), Player(Left, 1) };
            Assert.That(GameDrumPassHint.OpenSides(oneOnOne, Me), Is.Zero, "1:1 territory has no passing.");

            LabSnapshot twoOnTwo = Story(Drum("a", Me));
            twoOnTwo.teamTerritory = true;
            twoOnTwo.players = new[] { Player(Me, 0), Player(Left, 1), Player(Right, 2), Player(4, 3) };
            int open = GameDrumPassHint.OpenSides(twoOnTwo, Me);
            Assert.That(open == GameDrumPassHint.RightSide || open == GameDrumPassHint.LeftSide, Is.True,
                "2:2 passes only toward the teammate.");
            Assert.That(LabTeams.SideClosed(LabTeams.ClosedSide(twoOnTwo, Me), open == GameDrumPassHint.RightSide), Is.False);
        }

        [Test]
        public void BothSidesOpenAlternatesRightThenLeftEachPush()
        {
            const int both = GameDrumPassHint.RightSide | GameDrumPassHint.LeftSide;
            Assert.That(GameDrumPassHint.SideFor(both, 0f, 1.6f), Is.EqualTo(1), "Right first, like the tutorial's 4단계.");
            Assert.That(GameDrumPassHint.SideFor(both, 1.7f, 1.6f), Is.EqualTo(-1));
            Assert.That(GameDrumPassHint.SideFor(both, 3.3f, 1.6f), Is.EqualTo(1));
            Assert.That(GameDrumPassHint.SideFor(GameDrumPassHint.LeftSide, 0f, 1.6f), Is.EqualTo(-1));
            Assert.That(GameDrumPassHint.SideFor(GameDrumPassHint.RightSide, 1.7f, 1.6f), Is.EqualTo(1), "One open side stays.");
            Assert.That(GameDrumPassHint.SideFor(0, 0f, 1.6f), Is.Zero);
        }

        private static LabSnapshot Story(params LabOrbState[] drums) => new LabSnapshot
        {
            sessionId = "drum-hint", round = 1, phase = LabPhase.Playing, serverTime = 30d, drums = drums,
            players = new[] { Player(Me, 0), Player(Left, 1), Player(Right, 2) }
        };

        private static LabPlayerState Player(ulong id, int seat) => new LabPlayerState { id = id, seat = seat };

        private static LabOrbState Drum(string id, ulong owner) =>
            new LabOrbState { id = id, owner = owner, kind = LabOrbKind.Drum };
    }
}
