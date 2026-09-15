using System.Reflection;
using NUnit.Framework;

namespace C6.Prototype.Lobby.Tests
{
    public sealed class L2LobbyProgressTests
    {
        [TestCase("CONNECT_ATTEMPT_TIMEOUT", "TRANSPORT", "reach the host")]
        [TestCase("CONNECT_CANDIDATES_EXHAUSTED", "TRANSPORT", "reach the host")]
        [TestCase("CONNECT_BUDGET_EXHAUSTED", "TRANSPORT", "reach the host")]
        [TestCase("CONNECTION_REJECTED", "APPROVAL", "approve entry")]
        [TestCase("REQUEST_CONFIRMATION_TIMEOUT", "INITIAL_STATE", "settings were not confirmed")]
        [TestCase("CONNECTION_LOST", "SESSION", "connection was lost")]
        public void FailureStageExplainsWhichPartFailed(string reason, string stage, string expected)
        {
            Assert.That(Guidance(reason, stage), Does.Contain(expected));
        }

        [TestCase("ROOM_FULL", "full")]
        [TestCase("BUILD_MISMATCH", "versions differ")]
        [TestCase("PROTOCOL_MISMATCH", "versions differ")]
        [TestCase("DUPLICATE_PARTICIPANT", "already in the room")]
        [TestCase("BATTLE_IN_PROGRESS", "already started")]
        public void HostRejectionKeepsItsSpecificReason(string reason, string expected)
        {
            Assert.That(Guidance(reason, "APPROVAL"), Does.Contain(expected));
        }

        [TestCase("TRANSPORT")]
        [TestCase("APPROVAL")]
        [TestCase("INITIAL_STATE")]
        [TestCase("SESSION")]
        public void UnrecognizedDiagnosticsDoNotExposePayloadOrAssumePermissionDenial(string stage)
        {
            string text = Guidance("unrecognized private-endpoint-payload", stage);
            Assert.That(text, Does.Not.Contain("private-endpoint-payload"));
            Assert.That(text, Does.Not.Contain("Local Network"));
        }

        [Test]
        public void DirectAddressErrorMentionsBothSupportedFamilies()
        {
            string text = Guidance("INVALID_ADDRESS", "INPUT");
            Assert.That(text, Does.Contain("IPv4"));
            Assert.That(text, Does.Contain("IPv6"));
        }

        private static string Guidance(string reason, string stage)
        {
            var method = typeof(T10LobbyController).GetMethod("FriendlyStagedError", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { reason, stage });
        }
    }
}
