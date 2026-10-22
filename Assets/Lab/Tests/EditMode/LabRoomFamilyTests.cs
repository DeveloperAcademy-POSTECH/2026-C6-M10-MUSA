using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabRoomFamilyTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void DefaultDiscoveryPreservesLabAndEachFamilyRejectsTheOthersPresence()
        {
            using (var lab = new LabBonjourDiscovery())
            using (var game = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                Assert.That(lab.Family, Is.EqualTo(LabRoomFamily.Lab));
                Assert.That(lab.ExpectedProtocol, Is.EqualTo(LabBonjourDiscovery.Protocol));
                Assert.That(game.Family, Is.EqualTo(LabRoomFamily.Game));
                Assert.That(game.ExpectedProtocol, Is.Not.EqualTo(lab.ExpectedProtocol));
                foreach (LabBonjourDiscovery owner in new[] { lab, game })
                {
                    byte[] record = LabRoomPresence.Encode(owner.ExpectedProtocol, true, true, 1, 2, false, true);
                    Assert.That(LabRoomPresence.TryDecode(record, owner.ExpectedProtocol,
                        out _, out _, out _, out _, out _, out _), Is.True);
                    string other = owner == lab ? game.ExpectedProtocol : lab.ExpectedProtocol;
                    Assert.That(LabRoomPresence.TryDecode(record, other,
                        out _, out _, out _, out _, out _, out _), Is.False);
                }
                Assert.That(typeof(LabBonjourDiscovery).GetProperty(nameof(LabBonjourDiscovery.Family)).CanWrite, Is.False);
                Assert.Throws<ArgumentOutOfRangeException>(() => new LabBonjourDiscovery((LabRoomFamily)123));
            }
        }

        [TestCase(LabRoomFamily.Game, false)]
        [TestCase(LabRoomFamily.Lab, true)]
        public void ReadyPendingRoomCannotTransferToTheWrongFamilyOrReleaseItsReservation(LabRoomFamily family,
            bool gameMode)
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Pending rooms support macOS/iOS.");
            Assert.That(LabPendingRoom.TryCreate(2, family, out var pending, out string error), Is.True, error);
            var root = new GameObject("Wrong-family pending handoff");
            try
            {
                LabNetwork network = root.AddComponent<LabNetwork>();
                Assert.That(network.SetGameFlowEnabled(gameMode), Is.True);
                LabBonjourDiscovery ad = (LabBonjourDiscovery)typeof(LabPendingRoom).GetField("advertisement", Private)
                    .GetValue(pending);
                // A confirmed native registration is represented without opening DNS operations in this gate test.
                Type operation = typeof(LabBonjourDiscovery).GetNestedType("Operation", BindingFlags.NonPublic);
                object registration = Activator.CreateInstance(operation, Private, null,
                    new object[] { ad, "register", null, 999d }, null);
                ((System.Collections.IList)typeof(LabBonjourDiscovery).GetField("operations", Private)
                    .GetValue(ad)).Add(registration);
                typeof(LabBonjourDiscovery).GetField("advertisement", Private).SetValue(ad, registration);
                typeof(LabBonjourDiscovery).GetProperty("RegisteredServiceName").SetValue(ad, LabRoomNames.ServiceBaseFor(family));
                typeof(LabBonjourDiscovery).GetProperty("AdvertisingConfirmed").SetValue(ad, true);
                Assert.That(pending.Ready, Is.True);
                object v4 = typeof(LabPendingRoom).GetField("ipv4", Private).GetValue(pending);
                object v6 = typeof(LabPendingRoom).GetField("ipv6", Private).GetValue(pending);
                Assert.That(pending.EnterAsCreator(network), Is.False);
                Assert.That(pending.Ready, Is.True, "A wrong-family call must not dispose the creator's advertisement.");
                Assert.That(typeof(LabPendingRoom).GetField("ipv4", Private).GetValue(pending), Is.SameAs(v4));
                Assert.That(typeof(LabPendingRoom).GetField("ipv6", Private).GetValue(pending), Is.SameAs(v6));
                Assert.That(network.IsConnected, Is.False);
            }
            finally { pending.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void LegacyPendingFactoryStaysLabAndUnknownFamilyAllocatesNothing()
        {
            Assert.That(LabPendingRoom.TryCreate(2, (LabRoomFamily)123, out var invalid, out string error), Is.False);
            Assert.That(invalid, Is.Null); Assert.That(error, Is.Not.Empty);
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Pending rooms support macOS/iOS.");
            Assert.That(LabPendingRoom.TryCreate(2, out var pending, out error), Is.True, error);
            using (pending) Assert.That(pending.Family, Is.EqualTo(LabRoomFamily.Lab));
        }
    }
}
