using System.Reflection;
using NUnit.Framework;

namespace C6.Prototype.Lobby.Tests
{
    public sealed class LobbyFailureGuidanceTests
    {
        [TestCase("Permission status is unknown")]
        [TestCase("Network policy changed")]
        [TestCase("DNSServiceBrowse failed: permission status unavailable")]
        [TestCase("Bonjour policy check did not finish")]
        public void AmbiguousPermissionOrPolicyTextDoesNotDiagnoseLocalNetworkDenial(string reason)
        {
            string message = Guidance(reason);
            Assert.That(message, Does.Not.Contain("Allow Local Network"));
            Assert.That(message, Does.Not.Contain("denied"));
            Assert.That(message, Does.Contain("Wi-Fi"), "An unknown cause should keep a general connection check available.");
        }

        [TestCase("PolicyDenied")]
        [TestCase("DNSServiceBrowse failed: PolicyDenied")]
        [TestCase("DNSServiceBrowse failed: policydenied")]
        [TestCase("DNSServiceBrowse failed (-65570)")]
        public void SpecificNativePolicyDenialProvidesLocalNetworkSettingsGuidance(string reason)
        {
            string message = Guidance(reason);
            Assert.That(message, Does.Contain("Local Network"));
            Assert.That(message, Does.Contain("Settings"));
            Assert.That(message, Does.Contain("Find Rooms"));
        }

        [Test]
        public void BackgroundInterruptionExplainsManualNewConnectionWithoutBlamingPermission()
        {
            string message = Guidance("APPLICATION_BACKGROUNDED");
            Assert.That(message, Does.Contain("foreground"));
            Assert.That(message, Does.Contain("new room"));
            Assert.That(message, Does.Not.Contain("Local Network"));
        }

        [Test]
        public void UnknownTransportReasonOffersConnectionChecksWithoutRevealingDiagnosticPayload()
        {
            string message = Guidance("TRANSPORT_FAILURE diagnostic-detail=private-network-information");
            Assert.That(message, Does.Contain("Wi-Fi"));
            Assert.That(message, Does.Contain("connect again"));
            Assert.That(message, Does.Not.Contain("private-network-information"));
            Assert.That(message, Does.Not.Contain("Local Network"));
        }

        private static string Guidance(string reason)
        {
            // Exercise the existing UI adapter without creating a scene or making a network request.
            var method = typeof(T10LobbyController).GetMethod("FriendlyError", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { reason });
        }
    }
}
