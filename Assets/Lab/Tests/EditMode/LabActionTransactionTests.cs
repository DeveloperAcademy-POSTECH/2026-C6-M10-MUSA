using System;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabActionTransactionTests
    {
        private static LabActionIdentity Id(long id = 1, ulong sender = 7, uint round = 2,
            string session = "session") => new LabActionIdentity(session, round, sender, id);
        private static LabActionReceipt Receipt(long id, bool accepted = true, string provisional = null,
            string output = null) => new LabActionReceipt
        {
            sessionId = "session", round = 2, sender = 7, requestId = id, accepted = accepted,
            provisionalId = provisional, outputId = output, reason = accepted ? null : "REJECTED"
        };
        private static LabLocalActionCoordinator Coordinator(int attempts = 6)
        {
            var coordinator = new LabLocalActionCoordinator(attempts);
            coordinator.SetActive("session", 2, 7);
            return coordinator;
        }

        [Test]
        public void LostReceiptRetryReplaysOriginalResultWithoutExecutingAgain()
        {
            var ledger = new LabActionResultLedger();
            ledger.SetActive("session", 2);
            int executions = 0;
            Func<LabActionReceipt> execute = () =>
            {
                executions++;
                return new LabActionReceipt { accepted = true, outputId = "orb-1" };
            };
            Assert.That(ledger.Execute(Id(), "generate:drum-1", execute, out var first, out _),
                Is.EqualTo(LabActionExecution.Executed));
            Assert.That(ledger.Execute(Id(), "generate:drum-1", execute, out var retry, out _),
                Is.EqualTo(LabActionExecution.Replayed));
            Assert.That(executions, Is.EqualTo(1));
            Assert.That(retry.outputId, Is.EqualTo(first.outputId));
            Assert.That(retry.requestId, Is.EqualTo(1));
            Assert.That(retry.sender, Is.EqualTo(7));
        }

        [Test]
        public void RejectedRulesResultIsAlsoReplayedInsteadOfBeingRetriedAsNewAction()
        {
            var ledger = new LabActionResultLedger();
            ledger.SetActive("session", 2);
            int calls = 0;
            ledger.Execute(Id(), "combine:a:b", () =>
            {
                calls++;
                return new LabActionReceipt { accepted = false, reason = "NOT_OWNER" };
            }, out _, out _);
            var execution = ledger.Execute(Id(), "combine:a:b", () =>
            {
                calls++;
                return new LabActionReceipt { accepted = true };
            }, out var replay, out string reason);
            Assert.That(execution, Is.EqualTo(LabActionExecution.Replayed));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(replay.accepted, Is.False);
            Assert.That(reason, Is.EqualTo("NOT_OWNER"));
        }

        [Test]
        public void ConflictingReplayCannotReuseAnActionIdForDifferentInputs()
        {
            var ledger = new LabActionResultLedger();
            ledger.SetActive("session", 2);
            ledger.Execute(Id(), "transfer:a:left", () => new LabActionReceipt { accepted = true }, out _, out _);
            int calls = 0;
            Assert.That(ledger.Execute(Id(), "transfer:a:right", () =>
            {
                calls++;
                return new LabActionReceipt { accepted = true };
            }, out var receipt, out string reason), Is.EqualTo(LabActionExecution.Rejected));
            Assert.That(receipt, Is.Null);
            Assert.That(reason, Is.EqualTo("CONFLICTING_ACTION_REPLAY"));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void ReceiptCacheCopiesNestedStateSoCallerCannotChangeRetryResult()
        {
            var ledger = new LabActionResultLedger();
            ledger.SetActive("session", 2);
            var original = new LabActionReceipt
            {
                accepted = true, removedIds = new[] { "old" },
                states = new[] { new LabOrbState { id = "new", owner = 7, vx = 2 } },
                players = new[] { new LabPlayerState { id = 7, stamina = 80 } }
            };
            ledger.Execute(Id(), "combine", () => original, out var returned, out _);
            original.states[0].vx = 999;
            returned.removedIds[0] = "wrong";
            returned.states[0].owner = 99;
            returned.players[0].stamina = 0;
            Assert.That(ledger.TryGet(Id(), out var replay), Is.True);
            Assert.That(replay.removedIds, Is.EqualTo(new[] { "old" }));
            Assert.That(replay.states[0].owner, Is.EqualTo(7));
            Assert.That(replay.states[0].vx, Is.EqualTo(2));
            Assert.That(replay.players[0].stamina, Is.EqualTo(80));
        }

        [Test]
        public void LedgerBoundsDoNotEvictAnOldResultAndPermitDoubleConsumption()
        {
            var ledger = new LabActionResultLedger(1);
            ledger.SetActive("session", 2);
            ledger.Execute(Id(), "first", () => new LabActionReceipt { accepted = true }, out _, out _);
            Assert.That(ledger.Execute(Id(2), "second", () =>
            {
                Assert.Fail("A full ledger must not execute a new operation.");
                return null;
            }, out _, out string reason), Is.EqualTo(LabActionExecution.Rejected));
            Assert.That(reason, Is.EqualTo("ACTION_LEDGER_FULL"));
            Assert.That(ledger.Execute(Id(), "first", () => null, out _, out _),
                Is.EqualTo(LabActionExecution.Replayed));
            ledger.SetActive("session", 3);
            Assert.That(ledger.Count, Is.Zero);
            Assert.That(ledger.TryGet(Id(), out _), Is.False);
            Assert.That(ledger.Execute(Id(), "first", () => null, out _, out reason),
                Is.EqualTo(LabActionExecution.Rejected));
            Assert.That(reason, Is.EqualTo("STALE_ACTION_ROUND"));
        }

        [Test]
        public void ExceptionAfterPossibleMutationCannotExecuteAgainOnRetry()
        {
            var ledger = new LabActionResultLedger();
            ledger.SetActive("session", 2);
            int mutations = 0;
            Assert.Throws<InvalidOperationException>(() => ledger.Execute(Id(), "action", () =>
            {
                mutations++;
                throw new InvalidOperationException("Handler failed after mutation.");
            }, out _, out _));
            Assert.That(ledger.Execute(Id(), "action", () =>
            {
                mutations++;
                return new LabActionReceipt { accepted = true };
            }, out var replay, out _), Is.EqualTo(LabActionExecution.Replayed));
            Assert.That(mutations, Is.EqualTo(1));
            Assert.That(replay.reason, Is.EqualTo("ACTION_RECOVERY_REQUIRED"));
        }

        [Test]
        public void DifferentSendersMayUseSameLocalRequestNumberAndHostZeroIsValid()
        {
            var ledger = new LabActionResultLedger();
            ledger.SetActive("session", 2);
            Assert.That(ledger.Execute(Id(sender: 0), "generate", () => new LabActionReceipt { accepted = true },
                out _, out _), Is.EqualTo(LabActionExecution.Executed));
            Assert.That(ledger.Execute(Id(sender: 7), "generate", () => new LabActionReceipt { accepted = true },
                out _, out _), Is.EqualTo(LabActionExecution.Executed));
            Assert.That(ledger.Count, Is.EqualTo(2));
        }

        [Test]
        public void IdentityAndOwnershipChecksRejectOldRoundsSpoofedSendersAndStaleOwnership()
        {
            Assert.That(LabActionValidation.Identity(Id(), "session", 2, 7, out _), Is.True);
            Assert.That(LabActionValidation.Identity(Id(), "session", 2, 8, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("ACTION_SENDER_MISMATCH"));
            Assert.That(LabActionValidation.Identity(Id(round: 1), "session", 2, 7, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("STALE_ACTION_ROUND"));
            Assert.That(LabActionValidation.Identity(Id(0), "session", 2, 7, out _), Is.False);
            Assert.That(LabActionValidation.Ownership(7, 3, 7, 3, out _), Is.True);
            Assert.That(LabActionValidation.Ownership(8, 4, 7, 3, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("NOT_OWNER"));
            Assert.That(LabActionValidation.Ownership(7, 4, 7, 3, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("STALE_OWNERSHIP"));
        }

        [Test]
        public void ChainedProvisionalOperationsWaitForCanonicalIdsThenSendInOrder()
        {
            var coordinator = Coordinator();
            Assert.That(coordinator.TryRegister(1, new[] { "yin", "yang" }, "pending-combined", 0, out _), Is.True);
            Assert.That(coordinator.TryRegister(2, new[] { "pending-combined" }, null, 0, out _), Is.True);
            Assert.That(coordinator.GetDueRequestIds(0, true), Is.EqualTo(new long[] { 1 }));
            Assert.That(coordinator.TryResolveInputs(2, out _), Is.False);
            Assert.That(coordinator.MarkSent(1, 0), Is.True);
            Assert.That(coordinator.ApplyReceipt(Receipt(1, provisional: "pending-combined", output: "combined-7"),
                out var resolution), Is.True);
            Assert.That(resolution.accepted, Is.True);
            Assert.That(coordinator.GetDueRequestIds(0, true), Is.EqualTo(new long[] { 2 }));
            Assert.That(coordinator.TryResolveInputs(2, out var inputs), Is.True);
            Assert.That(inputs, Is.EqualTo(new[] { "combined-7" }));
            Assert.That(coordinator.TryGetMappedId("pending-combined", out string output), Is.True);
            Assert.That(output, Is.EqualTo("combined-7"));
            Assert.That(coordinator.ApplyReceipt(Receipt(1, provisional: "pending-combined", output: "combined-7"),
                out _), Is.False, "An acknowledgement cannot replay local presentation twice.");
        }

        [Test]
        public void RejectedProducerCancelsItsTransitiveDependentsButNotIndependentGeneration()
        {
            var coordinator = Coordinator();
            coordinator.TryRegister(1, new[] { "yin", "yang" }, "pending-a", 0, out _);
            coordinator.TryRegister(2, new[] { "pending-a", "another" }, "pending-b", 0, out _);
            coordinator.TryRegister(3, new[] { "pending-b" }, null, 0, out _);
            coordinator.TryRegister(4, new[] { "drum" }, null, 0, out _);
            Assert.That(coordinator.ApplyReceipt(Receipt(1, false), out var resolution), Is.True);
            Assert.That(resolution.rejectedDependents, Is.EquivalentTo(new long[] { 2, 3 }));
            Assert.That(coordinator.GetDueRequestIds(0, true), Is.EqualTo(new long[] { 4 }));
            Assert.That(coordinator.TryGetPending(3, out var descendant), Is.True);
            Assert.That(descendant.state, Is.EqualTo(LabPendingActionState.Rejected));
            Assert.That(descendant.reason, Is.EqualTo("DEPENDENCY_REJECTED"));
            Assert.That(coordinator.TryRegister(5, new[] { "pending-a" }, null, 0, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("DEPENDENCY_REJECTED"));
        }

        [Test]
        public void RetryRetainsSameIdAndUsesBackoffWithoutRestoringPossiblyConsumedOrb()
        {
            var coordinator = Coordinator(2);
            coordinator.TryRegister(10, new[] { "orb" }, null, 0, out _);
            Assert.That(coordinator.GetDueRequestIds(0, true), Is.EqualTo(new long[] { 10 }));
            Assert.That(coordinator.MarkSent(10, 0), Is.True);
            Assert.That(coordinator.GetDueRequestIds(.24, true), Is.Empty);
            Assert.That(coordinator.GetDueRequestIds(.25, false), Is.Empty);
            Assert.That(coordinator.GetDueRequestIds(.25, true), Is.EqualTo(new long[] { 10 }));
            Assert.That(coordinator.MarkSent(10, .25), Is.True);
            Assert.That(coordinator.GetDueRequestIds(.74, true), Is.Empty);
            Assert.That(coordinator.GetDueRequestIds(.75, true), Is.Empty);
            Assert.That(coordinator.GetRecoveryRequiredRequestIds(), Is.EqualTo(new long[] { 10 }));
            Assert.That(coordinator.TryGetPending(10, out var pending), Is.True);
            Assert.That(pending.state, Is.EqualTo(LabPendingActionState.RecoveryRequired));
            Assert.That(pending.attempts, Is.EqualTo(2));
            Assert.That(coordinator.Count, Is.EqualTo(1), "Timeout retains the operation until authoritative recovery.");
            Assert.That(coordinator.ApplyReceipt(Receipt(10), out var lateReceipt), Is.True);
            Assert.That(lateReceipt.accepted, Is.True, "A delayed Host receipt can resolve a recovery-required action.");
        }

        [Test]
        public void NewRoundAndWrongSenderReceiptCannotCompletePendingAction()
        {
            var coordinator = Coordinator();
            coordinator.TryRegister(1, new[] { "orb" }, null, 0, out _);
            var wrong = Receipt(1);
            wrong.sender = 8;
            Assert.That(coordinator.ApplyReceipt(wrong, out _), Is.False);
            wrong.sender = 7;
            wrong.round = 1;
            Assert.That(coordinator.ApplyReceipt(wrong, out _), Is.False);
            coordinator.SetActive("session", 3, 7);
            Assert.That(coordinator.Count, Is.Zero);
            Assert.That(coordinator.ApplyReceipt(Receipt(1), out _), Is.False);
        }

        [Test]
        public void ReceiptMustMapTheExpectedProvisionalOutputBeforeDependenciesCanContinue()
        {
            var coordinator = Coordinator();
            coordinator.TryRegister(1, new[] { "a", "b" }, "pending-a", 0, out _);
            coordinator.TryRegister(2, new[] { "pending-a" }, null, 0, out _);
            Assert.That(coordinator.ApplyReceipt(Receipt(1, provisional: "wrong", output: "real-a"), out _), Is.False);
            Assert.That(coordinator.ApplyReceipt(Receipt(1, provisional: "pending-a"), out _), Is.False);
            Assert.That(coordinator.TryGetPending(1, out var pending), Is.True);
            Assert.That(pending.state, Is.EqualTo(LabPendingActionState.Ready));
            Assert.That(coordinator.TryResolveInputs(2, out _), Is.False);
        }

        [Test]
        public void RapidGenerationDoesNotReserveOrExclusivelyLockItsDrumInput()
        {
            var coordinator = Coordinator();
            var inputs = new[] { "drum" };
            Assert.That(coordinator.TryRegister(1, inputs, null, 0, out _), Is.True);
            inputs[0] = "changed-after-register";
            Assert.That(coordinator.TryRegister(2, new[] { "drum" }, null, 0, out _), Is.True);
            Assert.That(coordinator.GetDueRequestIds(0, true), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(coordinator.TryResolveInputs(1, out var retained), Is.True);
            Assert.That(retained, Is.EqualTo(new[] { "drum" }));
        }
    }
}
