using C6Lab;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabHudNeighborTests
    {
        [Test]
        public void TwoPlayers_OnePeerAppearsOnBothSides()
        {
            var snapshot = Snapshot(0, 1);

            LabHud.GetNeighborLabels(snapshot, 0, out string left, out string right);

            Assert.That(left, Is.EqualTo("P2"));
            Assert.That(right, Is.EqualTo("P2"));
        }

        [Test]
        public void FivePlayers_SeatsSelectPeersAndTheirRoomPLabels()
        {
            var snapshot = Snapshot(51, 90, 17, 42, 77);
            // Deliberately scramble snapshot array order; seat is the source of truth.
            (snapshot.players[0], snapshot.players[3]) = (snapshot.players[3], snapshot.players[0]);

            LabHud.GetNeighborLabels(snapshot, 17, out string left, out string right);

            Assert.That(left, Is.EqualTo("P4"));
            Assert.That(right, Is.EqualTo("P2"));
        }

        [Test]
        public void RejoinedClientWithHigherTransportId_StillAppearsAsP2()
        {
            var snapshot = Snapshot(0, 12);

            LabHud.GetNeighborLabels(snapshot, 0, out string left, out string right);

            Assert.That(left, Is.EqualTo("P2"));
            Assert.That(right, Is.EqualTo("P2"));
        }

        [Test]
        public void SoloOrMissingLocal_HasNoInventedNeighbor()
        {
            LabHud.GetNeighborLabels(Snapshot(8), 8, out string soloLeft, out string soloRight);
            LabHud.GetNeighborLabels(Snapshot(8, 9), 100, out string missingLeft, out string missingRight);

            Assert.That(soloLeft, Is.EqualTo("P—"));
            Assert.That(soloRight, Is.EqualTo("P—"));
            Assert.That(missingLeft, Is.EqualTo("P—"));
            Assert.That(missingRight, Is.EqualTo("P—"));
        }

        private static LabSnapshot Snapshot(params ulong[] ids)
        {
            var snapshot = new LabSnapshot { players = new LabPlayerState[ids.Length] };
            for (int i = 0; i < ids.Length; i++)
                snapshot.players[i] = new LabPlayerState { id = ids[i], seat = i };
            return snapshot;
        }
    }
}
