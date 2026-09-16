using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;

namespace C6.Prototype.Networking.Tests
{
    public sealed class L2ConnectionCandidateTests
    {
        [TestCase("::1", "::1")]
        [TestCase("2001:db8::1", "2001:db8::1")]
        [TestCase(" fd00::abcd ", "fd00::abcd")]
        [TestCase("fe80::1234%7", "fe80::1234%7")]
        public void NumericIpv6RetainsRequiredScope(string input, string expected)
        {
            Assert.That(DirectConnectionValidation.TryParseAddress(input, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
        [TestCase("::")]
        [TestCase("ff02::1")]
        [TestCase("::ffff:192.168.1.2")]
        [TestCase("[::1]")]
        [TestCase("fe80::1")]
        [TestCase("fe80::1%0")]
        [TestCase("fe80::1%en0")]
        [TestCase("fe80::1%7%8")]
        [TestCase("fe80::1%4294967296")]
        [TestCase("2001:db8::1%7")]
        [TestCase("127.0.0.1%7")]
        public void InvalidOrAmbiguousRouteIsRejectedBeforeManagerCreation(string input)
        { Assert.That(DirectConnectionValidation.TryParseAddress(input, out _), Is.False); }

        [Test]
        public void CandidatesAreCopiedDeduplicatedAndTriedInGivenOrder()
        {
            var input = new List<string> { "192.0.2.1", " ::1 ", "::1", "fe80::2%7", "fe80::2%8" };
            Assert.That(ConnectionCandidatePlan.TryCreate(input, 1, out var plan), Is.True);
            input.Clear();
            Assert.That(plan.Count, Is.EqualTo(4));
            Assert.That(plan.Current, Is.EqualTo("192.0.2.1"));
            Assert.That(plan.MoveNext(2), Is.True); Assert.That(plan.Current, Is.EqualTo("::1"));
            Assert.That(plan.MoveNext(3), Is.True); Assert.That(plan.Current, Is.EqualTo("fe80::2%7"));
            Assert.That(plan.MoveNext(4), Is.True); Assert.That(plan.Current, Is.EqualTo("fe80::2%8"));
            Assert.That(plan.MoveNext(5), Is.False);
        }
        [Test]
        public void AdmissionDenialAndCompletedConnectionNeverTryAnotherAddress()
        {
            Assert.That(ConnectionCandidatePlan.TryCreate(new[] { "127.0.0.1", "::1" }, 10, out var plan), Is.True);
            Assert.That(plan.CanRetry(false, "", 11), Is.True);
            foreach (var reason in new[] { "ROOM_FULL", "BUILD_MISMATCH", "DUPLICATE_NONCE", "BATTLE_IN_PROGRESS", "UNKNOWN_DENIAL" })
                Assert.That(plan.CanRetry(false, reason, 11), Is.False, reason);
            Assert.That(plan.CanRetry(true, "", 11), Is.False);
            Assert.That(plan.Index, Is.Zero);
        }

        [TestCase("[Disconnect Event][Client-0][TransportClientId-0][MaxConnectionAttempts] No response",
            NetworkTransport.DisconnectEvents.MaxConnectionAttempts, "No response")]
        [TestCase("[Disconnect Event][Client-18446744073709551615][TransportClientId-987][ProtocolTimeout] Timed out",
            NetworkTransport.DisconnectEvents.ProtocolTimeout, "Timed out")]
        [TestCase("[Disconnect Event][Client-0][TransportClientId-0][TransportShutdown] ",
            NetworkTransport.DisconnectEvents.TransportShutdown, null)]
        [TestCase("[Disconnect Event][Client-0][TransportClientId-0][TransportShutdown] NetworkConnectionManager was shutdown. ",
            NetworkTransport.DisconnectEvents.TransportShutdown, "")]
        public void NgoTransportInformationAllowsOnlyAPreAdmissionFallback(string diagnostic,
            NetworkTransport.DisconnectEvents eventKind, string transportMessage)
        {
            // NGO's real public DisconnectReason is nonempty for ordinary local failures. Feed
            // the same classification used by DirectConnectionSession into the actual retry policy.
            string denial = DirectConnectionSession.GetExplicitApprovalReason(diagnostic, eventKind, transportMessage);
            Assert.That(denial, Is.Empty);
            Assert.That(ConnectionCandidatePlan.TryCreate(new[] { "192.0.2.1", "::1" }, 10, out var plan), Is.True);
            Assert.That(plan.CanRetry(false, denial, 11), Is.True);
            Assert.That(plan.CanRetry(true, denial, 11), Is.False, "An established session must remain a session failure.");
        }

        [TestCase("C6_T02_ROOM_FULL")]
        [TestCase("C6_INVALID_APPROVAL")]
        [TestCase("BUILD_MISMATCH")]
        [TestCase("DUPLICATE_PARTICIPANT")]
        [TestCase("BATTLE_IN_PROGRESS")]
        [TestCase("UNKNOWN_FUTURE_DENIAL")]
        [TestCase("The host has refused this request.")]
        [TestCase("[Disconnect Event] server explicitly refused this request")]
        [TestCase("[Disconnect Event][Client-no][TransportClientId-0][MaxConnectionAttempts] No response")]
        [TestCase("[Disconnect Event][Client-0][TransportClientId-18446744073709551616][MaxConnectionAttempts] No response")]
        [TestCase("[Disconnect Event][Client-0][TransportClientId-0][ProtocolTimeout] No response")]
        [TestCase("[Disconnect Event][Client-0][TransportClientId-0][MaxConnectionAttempts] Different server reason")]
        public void ExplicitAndUnrecognizedDenialsNeverAdvanceToAnotherCandidate(string reason)
        {
            string denial = DirectConnectionSession.GetExplicitApprovalReason(reason,
                NetworkTransport.DisconnectEvents.MaxConnectionAttempts, "No response");
            Assert.That(denial, Is.EqualTo(reason));
            Assert.That(ConnectionCandidatePlan.TryCreate(new[] { "127.0.0.1", "::1" }, 10, out var plan), Is.True);
            Assert.That(plan.CanRetry(false, denial, 11), Is.False);
            Assert.That(plan.Index, Is.Zero);
        }

        [TestCase(null)]
        [TestCase("")]
        public void AbsentServerReasonDoesNotBecomeAnAdmissionRejection(string reason)
        {
            Assert.That(DirectConnectionSession.GetExplicitApprovalReason(reason,
                NetworkTransport.DisconnectEvents.MaxConnectionAttempts, "No response"), Is.Empty);
        }
        [Test]
        public void TotalDeadlineBoundsAllCandidatesIncludingSchedulingTime()
        {
            Assert.That(ConnectionCandidatePlan.TryCreate(new[] { "127.0.0.1", "::1" }, 10, out var plan), Is.True);
            Assert.That(plan.BudgetExpired(69.9), Is.False);
            Assert.That(plan.CanRetry(false, "", 70), Is.False);
            Assert.That(plan.MoveNext(70), Is.False);
            Assert.That(plan.Index, Is.Zero);
            Assert.That(plan.BudgetExpired(double.NaN), Is.True);
            Assert.That(plan.BudgetExpired(9), Is.True);
        }
        [Test]
        public void InvalidInputsCannotProduceAnEmptyOrUnboundedPlan()
        {
            Assert.That(ConnectionCandidatePlan.TryCreate(null, 0, out _), Is.False);
            Assert.That(ConnectionCandidatePlan.TryCreate(Array.Empty<string>(), 0, out _), Is.False);
            Assert.That(ConnectionCandidatePlan.TryCreate(new[] { "localhost" }, 0, out _), Is.False);
            Assert.That(ConnectionCandidatePlan.TryCreate(new[] { "::1" }, double.NaN, out _), Is.False);
            Assert.That(ConnectionCandidatePlan.TryCreate(new[] { "::1" }, -1, out _), Is.False);
            Assert.That(ConnectionCandidatePlan.TryCreate(Forever(), 0, out _), Is.False);
        }
        [Test]
        public void ConnectionSetupBudgetDoesNotChangeLegacyGameWatchdog()
        {
            Assert.That(DirectConnectionSession.ConnectionTimeoutSeconds, Is.EqualTo(8));
            Assert.That(DirectConnectionSession.DisconnectTimeoutMilliseconds, Is.EqualTo(8000));
            Assert.That(DirectConnectionSession.CandidateConnectionTimeoutSeconds, Is.EqualTo(18));
        }
        static IEnumerable<string> Forever() { while (true) yield return "::1"; }
    }
}
