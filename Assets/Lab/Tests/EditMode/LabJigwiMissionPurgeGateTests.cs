using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabJigwiMissionPurgeGateTests
    {
        [Test]
        public void OnlyTheCurrentRoundsMonotonicBFailuresCanAdvanceTheFence()
        {
            var gate = new LabJigwiMissionPurgeGate();
            gate.SetActive("room", 2);
            Assert.That(gate.TryAccept("room", 1, "jigwi-ab/room/1/1", 10), Is.False);
            Assert.That(gate.TryAccept("other", 2, "jigwi-ab/other/2/1", 10), Is.False);
            Assert.That(gate.TryAccept("room", 2, "jigwi-ab/room/2/0", 10), Is.False);
            Assert.That(gate.TryAccept("room", 2, "jigwi-ab/room/2/7", 10), Is.False);
            Assert.That(gate.TryAccept("room", 2, "jigwi-ab/room/2/1", 0), Is.False);
            Assert.That(gate.TryAccept("room", 2, "jigwi-ab/room/2/1", 10), Is.True);
            Assert.That(gate.TryAccept("room", 2, "jigwi-ab/room/2/1", 10), Is.False);
            Assert.That(gate.TryAccept("room", 2, "jigwi-ab/room/2/3", 9), Is.False);
            Assert.That(gate.TryAccept("room", 2, "jigwi-ab/room/2/3", 20), Is.True);
            Assert.That(gate.Revision, Is.EqualTo(20UL));
        }

        [Test]
        public void TheFenceCancelsOldAndUnapprovedShotsButKeepsLaterApprovals()
        {
            var gate = new LabJigwiMissionPurgeGate();
            gate.SetActive("room", 1);
            Assert.That(gate.Cancels(0), Is.False);
            Assert.That(gate.TryAccept("room", 1, "jigwi-ab/room/1/1", 25), Is.True);
            Assert.That(gate.Cancels(0), Is.True);
            Assert.That(gate.Cancels(24), Is.True);
            Assert.That(gate.Cancels(25), Is.True);
            Assert.That(gate.Cancels(26), Is.False);
        }

        [Test]
        public void RebindingTheSameRoundKeepsTheFenceAndANewRoundResetsIt()
        {
            var gate = new LabJigwiMissionPurgeGate();
            gate.SetActive("room", 1);
            Assert.That(gate.TryAccept("room", 1, "jigwi-ab/room/1/1", 25), Is.True);
            gate.SetActive("room", 1);
            Assert.That(gate.Revision, Is.EqualTo(25UL));
            gate.SetActive("room", 2);
            Assert.That(gate.Revision, Is.Zero);
            Assert.That(gate.Cancels(1), Is.False);
            Assert.That(gate.TryAccept("room", 1, "jigwi-ab/room/1/1", 99), Is.False);
            gate.SetActive(null, 0);
            Assert.That(gate.TryAccept(null, 0, "jigwi-ab//0/1", 1), Is.False);
        }
    }
}
