using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabBonjourRegistrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void RemovedConfirmedRegistrationRearmsItsReplyDeadlineAndTimesOutWithoutReconfirmation()
        {
            using (var discovery = new LabBonjourDiscovery())
            {
                object operation = Registration(discovery);
                Reply(discovery, operation, 2, "C6Lab-Room", 100);
                Assert.That(discovery.AdvertisingConfirmed, Is.True);
                Reply(discovery, operation, 0, "C6Lab-Room", 200);
                Assert.That(discovery.AdvertisingConfirmed, Is.False,
                    "A successful removal must never keep a creator card ready.");
                Assert.That(discovery.IsAdvertising, Is.True, "Automatic rename can still reconfirm this registration.");
                Assert.That(discovery.LastError, Is.Null);
                Assert.That(Read<bool>(operation, "HasAddressOrReply"), Is.False);
                Assert.That(Read<double>(operation, "Deadline"), Is.EqualTo(200 + LabBonjourDiscovery.ResolveTimeoutSeconds));

                // No DNS socket exists in this callback test. The expired reply must fail
                // before the socket-poll path, and dispose the managed operation/context.
                discovery.Tick(200 + LabBonjourDiscovery.ResolveTimeoutSeconds);
                Assert.That(discovery.IsAdvertising, Is.False);
                Assert.That(discovery.AdvertisingConfirmed, Is.False);
                Assert.That(discovery.LastError, Is.EqualTo("Bonjour register timed out."));
                Assert.That(discovery.ActiveNativeOperations, Is.Zero);
            }
        }

        [Test]
        public void RenamedAddReplyReconfirmsTheSameRegistrationWithItsNewName()
        {
            using (var discovery = new LabBonjourDiscovery())
            {
                object operation = Registration(discovery);
                Reply(discovery, operation, 2, "C6Lab-Room", 100);
                Reply(discovery, operation, 0, "C6Lab-Room", 200);
                Reply(discovery, operation, 2, "C6Lab-Room (2)", 201);
                Assert.That(discovery.AdvertisingConfirmed, Is.True);
                Assert.That(discovery.RegisteredServiceName, Is.EqualTo("C6Lab-Room (2)"));
                Assert.That(Read<bool>(operation, "HasAddressOrReply"), Is.True);
                Assert.That(discovery.IsAdvertising, Is.True);
                Assert.That(discovery.LastError, Is.Null);
            }
        }

        [Test]
        public void ExplicitDnsNameLeaseIsCaseInsensitiveAcrossPortsAndTxtFamilies()
        {
            string name = "C6Lab-lease-" + Guid.NewGuid().ToString("N");
            using (var first = new LabBonjourDiscovery())
            using (var second = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                object firstOperation = Registration(first), secondOperation = Registration(second);
                Assert.That(Lease(firstOperation, name), Is.True);
                Assert.That(Lease(secondOperation, name.ToUpperInvariant()), Is.False,
                    "Ports and TXT families do not distinguish the same case-insensitive DNS name.");
                first.Dispose();
                Assert.That(Lease(secondOperation, name.ToUpperInvariant()), Is.True,
                    "The next owner may reserve the name after cleanup.");
                first.Dispose();
                using (var third = new LabBonjourDiscovery())
                    Assert.That(Lease(Registration(third), name), Is.False,
                        "Repeated disposal of the old owner must not release the new owner's lease.");
            }
            using (var lab = new LabBonjourDiscovery())
            using (var game = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                Assert.That(Lease(Registration(lab), LabRoomNames.ServiceBase), Is.True);
                Assert.That(Lease(Registration(game), LabRoomNames.GameServiceBase), Is.True,
                    "The Lab and Game numbered room prefixes occupy different DNS names.");
            }
        }

        [Test]
        public void LocalConflictFailsBeforeCreatingANativeOperation()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("The public registration API supports macOS/iOS.");
            string name = "C6Lab-lease-" + Guid.NewGuid().ToString("N");
            using (var owner = new LabBonjourDiscovery())
            using (var contender = new LabBonjourDiscovery())
            {
                Assert.That(Lease(Registration(owner), name), Is.True);
                Assert.That(contender.StartAdvertise(name.ToUpperInvariant(), 54321, true, true,
                    1, 3, false, true, false), Is.False);
                Assert.That(contender.RegistrationNameConflict, Is.True);
                Assert.That(contender.ActiveNativeOperations, Is.Zero);
                Assert.That(contender.AdvertisingConfirmed || contender.IsAdvertising, Is.False);
                Assert.That(contender.LastError, Is.EqualTo("Bonjour register error -65548."));
            }
        }

        [Test]
        public void RemovedAndFailedRegistrationsKeepTheirNameUntilTheirOperationIsCleanedUp()
        {
            string name = "C6Lab-lease-" + Guid.NewGuid().ToString("N");
            using (var owner = new LabBonjourDiscovery())
            using (var contender = new LabBonjourDiscovery())
            {
                object operation = Registration(owner), next = Registration(contender);
                Assert.That(Lease(operation, name), Is.True);
                Reply(owner, operation, 2, name, 100);
                Reply(owner, operation, 0, name, 200);
                Assert.That(Lease(next, name), Is.False, "A removed registration can still be waiting for reconfirmation.");
                Reply(owner, operation, 0, null, 201, -65548);
                Assert.That(owner.RegistrationNameConflict, Is.True);
                Assert.That(owner.IsAdvertising, Is.False);
                Assert.That(Lease(next, name), Is.False,
                    "Marking a callback's operation done cannot release a still-live native registration.");
                owner.Tick(201);
                Assert.That(owner.ActiveNativeOperations, Is.Zero);
                Assert.That(Lease(next, name), Is.True, "Cleanup must make the name available again.");
            }
        }

        [Test]
        public void StopDoesNotReturnANameWhileOperationCleanupIsDeferred()
        {
            string name = "C6Lab-lease-" + Guid.NewGuid().ToString("N");
            using (var owner = new LabBonjourDiscovery())
            using (var contender = new LabBonjourDiscovery())
            {
                object operation = Registration(owner), next = Registration(contender);
                Assert.That(Lease(operation, name), Is.True);
                typeof(LabBonjourDiscovery).GetField("pumping", Private).SetValue(owner, true);
                try
                {
                    owner.Stop();
                    Assert.That(owner.IsAdvertising, Is.False);
                    Assert.That(Lease(next, name), Is.False, "Stop marks the operation done; cleanup still owns its lease.");
                }
                finally { typeof(LabBonjourDiscovery).GetField("pumping", Private).SetValue(owner, false); }
                owner.Stop();
                Assert.That(Lease(next, name), Is.True);
            }
        }

        [Test]
        public void LocalCollisionsDoNotSpendNativeRetryBudgetAndTheLastNumberCannotWrap()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Pending rooms support macOS/iOS.");
            int firstNumber = 100000 + (int)(BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) % 900001u);
            var owners = new System.Collections.Generic.List<LabBonjourDiscovery>();
            LabPendingRoom pending = null, last = null;
            try
            {
                for (int i = 0; i <= LabBonjourDiscovery.MaximumServices; i++)
                {
                    var owner = new LabBonjourDiscovery(); owners.Add(owner);
                    Assert.That(Lease(Registration(owner), NumberedName(firstNumber + i)), Is.True);
                }
                Assert.That(LabPendingRoom.TryCreate(3, out pending, out string error), Is.True, error);
                for (int i = 0; i <= LabBonjourDiscovery.MaximumServices; i++)
                {
                    pending.Tick(true, firstNumber);
                    Assert.That(pending.Failed, Is.False, "Known local names must not exhaust the native conflict budget.");
                }
                Assert.That(Read<int>(pending, "registrationAttempts"), Is.Zero);
                Assert.That(Read<int>(pending, "candidateNumber"), Is.EqualTo(firstNumber + LabBonjourDiscovery.MaximumServices + 1));

                var lastOwner = new LabBonjourDiscovery(); owners.Add(lastOwner);
                Assert.That(Lease(Registration(lastOwner), NumberedName(int.MaxValue)), Is.True);
                Assert.That(LabPendingRoom.TryCreate(3, out last, out error), Is.True, error);
                last.Tick(true, int.MaxValue);
                Assert.That(last.Failed, Is.True, "An occupied final number must fail instead of wrapping to 1.");
                Assert.That(Read<int>(last, "candidateNumber"), Is.EqualTo(int.MaxValue));
            }
            finally
            {
                pending?.Dispose(); last?.Dispose();
                foreach (var owner in owners) owner.Dispose();
            }
        }

        private static string NumberedName(int number) => LabRoomNames.ServiceBase + " ("
            + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        private static bool Lease(object operation, string name) => (bool)typeof(LabBonjourDiscovery)
            .GetMethod("TryLeaseName", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { operation, name });

        private static object Registration(LabBonjourDiscovery discovery)
        {
            Type type = typeof(LabBonjourDiscovery).GetNestedType("Operation", BindingFlags.NonPublic);
            object operation = Activator.CreateInstance(type, Private, null,
                new object[] { discovery, "register", null, 108d }, null);
            ((IList)typeof(LabBonjourDiscovery).GetField("operations", Private).GetValue(discovery)).Add(operation);
            typeof(LabBonjourDiscovery).GetField("advertisement", Private).SetValue(discovery, operation);
            return operation;
        }

        private static void Reply(LabBonjourDiscovery discovery, object operation, uint flags, string name, double now, int error = 0)
        {
            typeof(LabBonjourDiscovery).GetField("now", Private).SetValue(discovery, now);
            IntPtr context = (IntPtr)operation.GetType().GetProperty("Context", Private).GetValue(operation);
            IntPtr label = Marshal.StringToHGlobalAnsi(name);
            try
            {
                typeof(LabBonjourDiscovery).GetMethod("Registered", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { IntPtr.Zero, flags, error, label, IntPtr.Zero, IntPtr.Zero, context });
            }
            finally { Marshal.FreeHGlobal(label); }
        }

        private static T Read<T>(object operation, string name)
            => (T)operation.GetType().GetField(name, Private).GetValue(operation);
    }
}
