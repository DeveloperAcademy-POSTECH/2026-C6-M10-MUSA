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

        private static object Registration(LabBonjourDiscovery discovery)
        {
            Type type = typeof(LabBonjourDiscovery).GetNestedType("Operation", BindingFlags.NonPublic);
            object operation = Activator.CreateInstance(type, Private, null,
                new object[] { discovery, "register", null, 108d }, null);
            ((IList)typeof(LabBonjourDiscovery).GetField("operations", Private).GetValue(discovery)).Add(operation);
            typeof(LabBonjourDiscovery).GetField("advertisement", Private).SetValue(discovery, operation);
            return operation;
        }

        private static void Reply(LabBonjourDiscovery discovery, object operation, uint flags, string name, double now)
        {
            typeof(LabBonjourDiscovery).GetField("now", Private).SetValue(discovery, now);
            IntPtr context = (IntPtr)operation.GetType().GetProperty("Context", Private).GetValue(operation);
            IntPtr label = Marshal.StringToHGlobalAnsi(name);
            try
            {
                typeof(LabBonjourDiscovery).GetMethod("Registered", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { IntPtr.Zero, flags, 0, label, IntPtr.Zero, IntPtr.Zero, context });
            }
            finally { Marshal.FreeHGlobal(label); }
        }

        private static T Read<T>(object operation, string name)
            => (T)operation.GetType().GetField(name, Private).GetValue(operation);
    }
}
