using System;
using System.Linq;
using NUnit.Framework;

namespace C6.Prototype.GameSync.Tests
{
    public sealed class ParticipantGameBarrierTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void StartRequiresOneValidatedAcknowledgementFromEveryRemoteParticipant(int remotes)
        {
            var barrier=new ParticipantGameBarrier(); var ids=Enumerable.Range(1,remotes).Select(id=>(ulong)id).ToArray();
            barrier.Configure(ids); Assert.That(barrier.AllAcknowledged,Is.False);
            Assert.That(barrier.Acknowledge(99),Is.False);
            for(int i=0;i<ids.Length;i++)
            {
                Assert.That(barrier.Acknowledge(ids[i]),Is.True);
                Assert.That(barrier.Acknowledge(ids[i]),Is.False);
                Assert.That(barrier.AcknowledgedCount,Is.EqualTo(i+1));
                Assert.That(barrier.AllAcknowledged,Is.EqualTo(i==ids.Length-1));
            }
        }
        [Test]
        public void ResponsivePeersNeverExtendASilentPeersDeadline()
        {
            var barrier=new ParticipantGameBarrier();barrier.Configure(new ulong[]{1,2,3,4});barrier.Arm(10);
            foreach(ulong id in new ulong[]{1,2,4})Assert.That(barrier.Observe(id,17.9),Is.True);
            Assert.That(barrier.Observe(99,100),Is.False);
            Assert.That(barrier.TryExpired(17.9,8,out _),Is.False);
            Assert.That(barrier.TryExpired(18,8,out ulong expired),Is.True);Assert.That(expired,Is.EqualTo(3));
            barrier.Arm(18); // Repeating arm must not reset an existing deadline.
            Assert.That(barrier.TryExpired(18,8,out expired),Is.True);Assert.That(expired,Is.EqualTo(3));
        }
        [Test]
        public void RetryClearsAllAcksAndDeadlinesWhileKeepingFrozenParticipants()
        {
            var barrier=new ParticipantGameBarrier();barrier.Configure(new ulong[]{5,9});barrier.Arm(10);
            barrier.Acknowledge(5);barrier.Acknowledge(9);Assert.That(barrier.AllAcknowledged,Is.True);
            barrier.Reset();Assert.That(barrier.ExpectedCount,Is.EqualTo(2));Assert.That(barrier.AcknowledgedCount,Is.Zero);
            Assert.That(barrier.TryExpired(100,8,out _),Is.False);
            Assert.That(barrier.Acknowledge(5),Is.True);Assert.That(barrier.AllAcknowledged,Is.False);
            Assert.That(barrier.Acknowledge(9),Is.True);Assert.That(barrier.AllAcknowledged,Is.True);
            barrier.Arm(100);Assert.That(barrier.TryExpired(107.9,8,out _),Is.False);
        }
        [Test]
        public void NewConnectionCannotReuseAnOldParticipantsAckOrResponse()
        {
            var barrier=new ParticipantGameBarrier();barrier.Configure(new ulong[]{1,2});barrier.Acknowledge(1);barrier.Arm(10);
            barrier.Configure(new ulong[]{7,8});Assert.That(barrier.Acknowledge(1),Is.False);Assert.That(barrier.Observe(2,20),Is.False);
            Assert.That(barrier.AllAcknowledged,Is.False);Assert.That(barrier.TryExpired(20,8,out _),Is.False);
            barrier.Clear();Assert.That(barrier.ExpectedCount,Is.Zero);Assert.That(barrier.AllAcknowledged,Is.False);
        }
        [Test]
        public void DuplicateEmptyOrTooManyRemoteIdentitiesAreRejectedWithoutReplacingExistingState()
        {
            var barrier=new ParticipantGameBarrier();barrier.Configure(new ulong[]{1});barrier.Acknowledge(1);
            Assert.Throws<ArgumentException>(()=>barrier.Configure(Array.Empty<ulong>()));
            Assert.Throws<ArgumentException>(()=>barrier.Configure(new ulong[]{1,1}));
            Assert.Throws<ArgumentException>(()=>barrier.Configure(new ulong[]{1,2,3,4,5}));
            Assert.Throws<ArgumentNullException>(()=>barrier.Configure(null));
            Assert.That(barrier.ExpectedCount,Is.EqualTo(1));Assert.That(barrier.AllAcknowledged,Is.True);
        }
    }
}
