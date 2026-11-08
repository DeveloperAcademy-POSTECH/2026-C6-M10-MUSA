using System;
using System.Collections;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// A creator-owned waiting room retries a failed publish on real macOS Bonjour (#503). Failures are forced through
    /// the discovery's own Fail path or a refused start; a real Local Network denial or a lost Wi-Fi is not reproduced.
    /// </summary>
    [Category("Slow"), Category("Bonjour")]
    public sealed class LabPendingRoomRetryTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator AFirstRegistrationThatFailsKeepsItsCardAndPortThenRegistersByItself()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            int number = UniqueNumber();
            Assert.That(LabPendingRoom.TryCreate(3, out LabPendingRoom room, out string error), Is.True, error);
            try
            {
                ushort port = room.Port;
                room.Tick(true, number);
                Assert.That(Advertisement(room).IsAdvertising, Is.True, Advertisement(room).LastError);
                // mDNS probing takes most of a second, so this lands before the registration is confirmed.
                Assert.That(room.Ready, Is.False);
                FailRegistration(room, "forced registration failure");
                room.Tick(true, number);
                Assert.That(room.RetryPending, Is.True);
                Assert.That(room.RetryAttempts, Is.EqualTo(1));
                Assert.That(room.Ready || room.Failed, Is.False, "The card waits instead of being removed.");
                Assert.That(RetryDelay(room), Is.EqualTo(LabDiscoveryBackoff.Delay(0)).Within(.1d), "The first wait is a second.");
                Assert.That(room.Port, Is.EqualTo(port));
                AssertPortReserved(port);
                yield return TickUntil(() => room.Ready || room.Failed, 15d, number, room);
                Assert.That(room.Ready, Is.True, Advertisement(room).LastError);
                Assert.That(room.Name, Is.EqualTo(NumberedName(number)));
                Assert.That(room.Port, Is.EqualTo(port), "The retried room keeps its reserved port and join key.");
                Assert.That(room.RetryAttempts, Is.Zero, "A confirmed room starts any later failure from the first delay.");
                Assert.That((int)Field(room, "registrationAttempts").GetValue(room), Is.Zero,
                    "Retries do not use up the budget a later name conflict needs.");
            }
            finally { room.Dispose(); }
        }

        [UnityTest]
        public IEnumerator AStartTheDaemonRefusesAtOnceAlsoWaitsAndRetries()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            int number = UniqueNumber();
            Assert.That(LabPendingRoom.TryCreate(3, out LabPendingRoom room, out string error), Is.True, error);
            Socket ipv4 = null, ipv6 = null;
            try
            {
                // Hide both reservations for one start: StartAdvertise refuses it right away, not as a name conflict.
                ipv4 = (Socket)Field(room, "ipv4").GetValue(room); ipv6 = (Socket)Field(room, "ipv6").GetValue(room);
                Field(room, "ipv4").SetValue(room, null); Field(room, "ipv6").SetValue(room, null);
                room.Tick(true, number);
                Field(room, "ipv4").SetValue(room, ipv4); Field(room, "ipv6").SetValue(room, ipv6);
                ipv4 = ipv6 = null;
                Assert.That(Advertisement(room).IsAdvertising, Is.False);
                Assert.That(Advertisement(room).RegistrationNameConflict, Is.False);
                Assert.That(room.RetryPending, Is.True, "Before #503 this disposed the room at once.");
                Assert.That(room.Failed, Is.False);
                AssertPortReserved(room.Port);
                yield return TickUntil(() => room.Ready || room.Failed, 15d, number, room);
                Assert.That(room.Ready, Is.True, Advertisement(room).LastError);
            }
            finally
            {
                if (ipv4 != null) Field(room, "ipv4").SetValue(room, ipv4);
                if (ipv6 != null) Field(room, "ipv6").SetValue(room, ipv6);
                room.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AConfirmedRoomThatLosesItsRegistrationAsksForItsOwnNameAgain()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            int number = UniqueNumber();
            Assert.That(LabPendingRoom.TryCreate(4, out LabPendingRoom room, out string error), Is.True, error);
            try
            {
                yield return TickUntil(() => room.Ready || room.Failed, 20d, number, room);
                Assert.That(room.Ready, Is.True, Advertisement(room).LastError);
                string name = room.Name, key = room.Key;
                FailRegistration(room, "forced registration loss");
                room.Tick(true, number);
                Assert.That(room.RetryPending, Is.True);
                Assert.That(room.Ready, Is.False, "A creator cannot enter while the room is not advertised.");
                Assert.That(room.Failed, Is.False);
                // Cards confirmed before it may now ask it to start later; a confirmed room keeps its own free name.
                yield return TickUntil(() => room.Ready || room.Failed, 15d, number + 7, room);
                Assert.That(room.Ready, Is.True, Advertisement(room).LastError);
                Assert.That(room.Name, Is.EqualTo(name));
                Assert.That(room.Key, Is.EqualTo(key));
            }
            finally { room.Dispose(); }
        }

        [UnityTest]
        public IEnumerator WaitsDoubleFromOneSecondAndTheRoomFailsAfterTheSixthRetry()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Native Bonjour supports macOS/iOS.");
            int number = UniqueNumber();
            Assert.That(LabPendingRoom.TryCreate(2, out LabPendingRoom room, out string error), Is.True, error);
            ushort port = room.Port;
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (attempt > 0) Field(room, "retryAt").SetValue(room, 0d); // start the waiting retry now
                    room.Tick(true, number);
                    Assert.That(Advertisement(room).IsAdvertising, Is.True, Advertisement(room).LastError);
                    FailRegistration(room, "forced failure " + (attempt + 1));
                    room.Tick(true, number);
                    Assert.That(room.RetryAttempts, Is.EqualTo(attempt + 1));
                    Assert.That(RetryDelay(room), Is.EqualTo(LabDiscoveryBackoff.Delay(attempt)).Within(.1d));
                }
                Assert.That(LabDiscoveryBackoff.Delay(1), Is.EqualTo(2d));
                // Skip ahead to the sixth and last retry, which also fails.
                Field(room, "retryAttempts").SetValue(room, LabDiscoveryBackoff.MaximumAttempts);
                Field(room, "retryAt").SetValue(room, 0d);
                room.Tick(true, number);
                Assert.That(Advertisement(room).IsAdvertising, Is.True, Advertisement(room).LastError);
                FailRegistration(room, "forced last failure");
                LogAssert.Expect(LogType.Warning, "C6_LAB_PENDING_ROOM_RETRY result=stopped reason=forced last failure");
                room.Tick(true, number);
                Assert.That(room.Failed, Is.True, "The card is removed only now.");
                Assert.That(room.RetryPending, Is.False);
                // The owner (lobby or registry) releases the port when it removes the failed card.
                AssertPortReserved(port);
                room.Dispose();
                using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, port)));
                yield return null;
            }
            finally { room.Dispose(); }
        }

        private static IEnumerator TickUntil(Func<bool> condition, double seconds, int number, LabPendingRoom room)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                room.Tick(true, number);
                yield return null;
            }
        }

        private static void AssertPortReserved(ushort port)
        {
            using (var contender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                Assert.Throws<SocketException>(() => contender.Bind(new IPEndPoint(IPAddress.Any, port)),
                    "The waiting room keeps its port reserved.");
        }

        private static double RetryDelay(LabPendingRoom room) =>
            (double)Field(room, "retryAt").GetValue(room) - Time.realtimeSinceStartupAsDouble;

        private static LabBonjourDiscovery Advertisement(LabPendingRoom room) =>
            (LabBonjourDiscovery)Field(room, "advertisement").GetValue(room);

        private static void FailRegistration(LabPendingRoom room, string reason)
        {
            LabBonjourDiscovery discovery = Advertisement(room);
            object operation = typeof(LabBonjourDiscovery).GetField("advertisement", Private).GetValue(discovery);
            Assert.That(operation, Is.Not.Null, "A registration is running.");
            typeof(LabBonjourDiscovery).GetMethod("Fail", Private).Invoke(discovery, new[] { operation, reason });
            Assert.That(discovery.IsAdvertising, Is.False);
        }

        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, Private);

        // A high random number keeps these rooms clear of each other and of real rooms on the same network.
        private static int UniqueNumber() =>
            100000 + (int)(BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) % 900001u);

        private static string NumberedName(int number) =>
            LabRoomNames.ServiceBase + " (" + number.ToString(CultureInfo.InvariantCulture) + ")";
    }
}
