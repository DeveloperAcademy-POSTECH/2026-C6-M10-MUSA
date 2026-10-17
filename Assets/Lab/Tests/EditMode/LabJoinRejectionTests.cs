using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>NGO transport diagnostics must not suppress another usable IPv4/IPv6 route.</summary>
    public sealed class LabJoinRejectionTests
    {
        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("[Disconnect Event][Client-0][TransportClientId-0][MaxConnectionAttempts] Connection closed due to maximum connection attempts reached.", false)]
        [TestCase("Room is full. Choose another room.", true)]
        [TestCase("Battle already started. Wait for the Host to return to the lobby.", true)]
        [TestCase("Lab version mismatch. Use the same game version.", true)]
        [TestCase("The Host closed this room for maintenance.", true)]
        public void OnlyAnExplicitHostReasonEndsRouteFallback(string reason, bool terminal)
        {
            Assert.That(LabNetwork.IsTerminalJoinRejection(reason), Is.EqualTo(terminal));
        }
    }
}
