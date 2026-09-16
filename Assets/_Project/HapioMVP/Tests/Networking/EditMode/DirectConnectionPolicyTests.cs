using NUnit.Framework;

namespace C6.Prototype.Networking.Tests
{
    public sealed class DirectConnectionPolicyTests
    {
        [TestCase("192.168.1.20", "192.168.1.20")]
        [TestCase("10.0.0.1", "10.0.0.1")]
        [TestCase(" 172.16.1.2 ", "172.16.1.2")]
        [TestCase("127.0.0.1", "127.0.0.1")]
        public void AcceptsDottedUnicastAndExplicitLocalTestLoopback(string input, string expected)
        {
            Assert.That(DirectConnectionValidation.TryParseAddress(input, out var address), Is.True);
            Assert.That(address, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("localhost")]
        [TestCase("example.com")]
        [TestCase("127.1")]
        [TestCase("2130706433")]
        [TestCase("0x7f.0.0.1")]
        [TestCase("192.168.01.1")]
        [TestCase("192.168.1.256")]
        [TestCase("192.168.1.-1")]
        [TestCase("192.168.1.+1")]
        [TestCase("192.168.1. 1")]
        [TestCase("192.168.1.1:7777")]
        [TestCase("0.0.0.0")]
        [TestCase("0.1.2.3")]
        [TestCase("224.0.0.1")]
        [TestCase("239.255.255.250")]
        [TestCase("240.0.0.1")]
        [TestCase("255.255.255.255")]
        public void RejectsMalformedOrNonUnicastAddresses(string input)
        {
            Assert.That(DirectConnectionValidation.TryParseAddress(input, out var address), Is.False);
            Assert.That(address, Is.Null);
        }

        [TestCase("1", 1)]
        [TestCase("7777", 7777)]
        [TestCase(" 65535 ", 65535)]
        public void AcceptsOnlyUsablePortRange(string input, int expected)
        {
            Assert.That(DirectConnectionValidation.TryParsePort(input, out var port), Is.True);
            Assert.That(port, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("65536")]
        [TestCase("-1")]
        [TestCase("+7777")]
        [TestCase("7,777")]
        [TestCase("7777.0")]
        [TestCase("7 777")]
        public void RejectsInvalidPortWithoutOpeningTransport(string input)
        {
            Assert.That(DirectConnectionValidation.TryParsePort(input, out _), Is.False);
        }

        [Test]
        public void ApprovalReservationsPreventConcurrentThirdParticipantBeforeConnectionsFinish()
        {
            var policy = new TwoParticipantAdmissionPolicy();
            policy.BeginHostSession();
            Assert.That(policy.TryReserve(41), Is.True);
            Assert.That(policy.TryReserve(42), Is.False);
            Assert.That(policy.TryReserve(43), Is.False);
            Assert.That(policy.ReservedCount, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateApprovalDoesNotConsumeAnotherSlot()
        {
            var policy = new TwoParticipantAdmissionPolicy();
            policy.BeginHostSession();
            Assert.That(policy.TryReserve(0), Is.True);
            Assert.That(policy.TryReserve(41), Is.True);
            Assert.That(policy.TryReserve(41), Is.True);
            Assert.That(policy.ReservedCount, Is.EqualTo(2));
            Assert.That(policy.TryReserve(42), Is.False);
        }

        [Test]
        public void FailedPendingJoinCanReleaseItsReservationWithoutEvictingHost()
        {
            var policy = new TwoParticipantAdmissionPolicy();
            policy.BeginHostSession();
            Assert.That(policy.TryReserve(41), Is.True);
            policy.Release(0);
            policy.Release(999);
            Assert.That(policy.TryReserve(42), Is.False);
            policy.Release(41);
            Assert.That(policy.TryReserve(42), Is.True);
            Assert.That(policy.ReservedCount, Is.EqualTo(2));
        }

        [Test]
        public void SessionEndRejectsLateApprovalUntilExplicitNewHostSession()
        {
            var policy = new TwoParticipantAdmissionPolicy();
            Assert.That(policy.TryReserve(41), Is.False);
            policy.BeginHostSession();
            Assert.That(policy.TryReserve(41), Is.True);
            policy.EndSession();
            Assert.That(policy.TryReserve(42), Is.False);
            Assert.That(policy.ReservedCount, Is.Zero);
            policy.BeginHostSession();
            Assert.That(policy.TryReserve(42), Is.True);
            Assert.That(policy.TryReserve(41), Is.False);
        }
    }
}
