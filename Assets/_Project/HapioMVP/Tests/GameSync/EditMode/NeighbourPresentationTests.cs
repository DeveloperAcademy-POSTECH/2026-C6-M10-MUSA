using C6.Prototype.Lobby;
using C6.Prototype.Presentation;
using NUnit.Framework;

namespace C6.Prototype.GameSync.Tests
{
    public sealed class NeighbourPresentationTests
    {
        private static readonly LobbyPlayer[] Roster =
        {
            Player(0, 1, OrbElement.Fire),
            Player(41, 2, OrbElement.Water),
            Player(57, 3, OrbElement.Wood),
            Player(88, 4, OrbElement.Metal),
            Player(99, 5, OrbElement.Earth)
        };

        [Test]
        public void HostIdentityRemainsP1WhileBothDisplayedNeighboursFollowShuffledSeats()
        {
            Assert.That(T10GameSession.TryResolveNeighbours(Roster, new ulong[] { 57, 0, 41 }, 0,
                out var left, out var right), Is.False,
                "The approved seats must contain the complete frozen roster.");

            var first = new ulong[] { 57, 0, 41, 88, 99 };
            Assert.That(T10GameSession.TryResolveNeighbours(Roster, first, 0, out left, out right), Is.True);
            Assert.That((left.playerNumber, left.selectedElement), Is.EqualTo((3, OrbElement.Wood)));
            Assert.That((right.playerNumber, right.selectedElement), Is.EqualTo((2, OrbElement.Water)));

            // Retry retains P identities and elements, but installs a new physical seat order.
            var retry = new ulong[] { 41, 0, 57, 99, 88 };
            Assert.That(T10GameSession.TryResolveNeighbours(Roster, retry, 0, out left, out right), Is.True);
            Assert.That((left.playerNumber, left.selectedElement), Is.EqualTo((2, OrbElement.Water)));
            Assert.That((right.playerNumber, right.selectedElement), Is.EqualTo((3, OrbElement.Wood)));
        }

        [Test]
        public void TwoPlayerRingShowsTheSameOtherPlayerOnLeftAndRight()
        {
            var players = new[] { Roster[0], Roster[1] };
            Assert.That(T10GameSession.TryResolveNeighbours(players, new ulong[] { 41, 0 }, 0,
                out var left, out var right), Is.True);
            Assert.That(left, Is.SameAs(players[1]));
            Assert.That(right, Is.SameAs(players[1]));
        }

        [Test]
        public void MissingOrForeignSeatDoesNotLeaveAStaleNeighbourSelection()
        {
            Assert.That(T10GameSession.TryResolveNeighbours(Roster, new ulong[] { 57, 0, 41, 88, 99 }, 0,
                out var left, out var right), Is.True);
            Assert.That(T10GameSession.TryResolveNeighbours(Roster, new ulong[0], 0, out left, out right), Is.False);
            Assert.That(left, Is.Null);
            Assert.That(right, Is.Null);
            Assert.That(T10GameSession.TryResolveNeighbours(Roster, new ulong[] { 57, 0, 41, 88, 100 }, 0,
                out left, out right), Is.False);
            Assert.That(left, Is.Null);
            Assert.That(right, Is.Null);
        }

        private static LobbyPlayer Player(ulong id, int number, OrbElement element) => new LobbyPlayer
        {
            clientId = id, playerNumber = number, selectedElement = element
        };
    }
}
