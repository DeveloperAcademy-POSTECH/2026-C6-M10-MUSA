using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    /// <summary>
    /// Waiting-room cards while a publish retries (#503), on real macOS Bonjour. Failures are forced through the
    /// discovery's own Fail path; a real Local Network denial is not reproduced.
    /// </summary>
    [Category("Bonjour")]
    public sealed class GamePendingRoomRetryTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator ARetryingCardStaysWithoutHoldingBackTheNextAndLeavesOnlyAfterItsLastRetry()
        {
            if (!LabBonjourDiscovery.IsSupported) Assert.Ignore("Game pending rooms require macOS/iOS Bonjour.");
            using (var registry = new GamePendingRoomRegistry())
            {
                Assert.That(registry.TryCreate(2, GameMode.Story, out GamePendingRoomHandle first, out string error), Is.True, error);
                Assert.That(registry.TryCreate(3, GameMode.Story, out GamePendingRoomHandle second, out error), Is.True, error);
                StartRegistration(registry, first);
                Assert.That(Advertisement(second.Room).IsAdvertising, Is.False, "Cards register one at a time.");

                FailRegistration(first.Room, "forced registration failure");
                registry.Tick(out error);
                Assert.That(error, Is.Empty, "A card that will retry is not reported as failed.");
                Assert.That(registry.TryGet(first, out GamePendingRoomInfo waiting), Is.True);
                Assert.That(waiting.RetryPending, Is.True);
                Assert.That(waiting.RetryAttempts, Is.EqualTo(1));
                Assert.That(waiting.Ready || waiting.Failed, Is.False);
                // The failed registration released its name, so the second card may take it; any local name conflict
                // only moves a card to the next number on its next tick.
                StartRegistration(registry, second, "A waiting card does not hold back the next one.");

                // Keep the first card waiting while the second one is confirmed.
                Field(first.Room, "retryAt").SetValue(first.Room, Time.realtimeSinceStartupAsDouble + 600d);
                double deadline = Time.realtimeSinceStartupAsDouble + 20d;
                while (!Ready(registry, second) && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    registry.Tick(out error);
                    yield return null;
                }
                Assert.That(Ready(registry, second), Is.True, Advertisement(second.Room).LastError);
                Assert.That(registry.TryGet(first, out waiting) && waiting.RetryPending, Is.True);
                Assert.That(registry.Rooms.Count, Is.EqualTo(2), "The waiting card stays listed.");
                Assert.That(registry.CanEnter(waiting.Key), Is.False, "A waiting card cannot be entered.");

                // The sixth and last retry fails too: only now does the card leave, with the usual notice.
                ushort port = waiting.Port;
                Field(first.Room, "retryAttempts").SetValue(first.Room, LabDiscoveryBackoff.MaximumAttempts);
                Field(first.Room, "retryAt").SetValue(first.Room, 0d);
                // The second card may hold the first one's old name now; a local conflict moves to the next number.
                StartRegistration(registry, first);
                FailRegistration(first.Room, "forced last failure");
                LogAssert.Expect(LogType.Warning, "C6_LAB_PENDING_ROOM_RETRY result=stopped reason=forced last failure");
                registry.Tick(out error);
                Assert.That(error, Is.EqualTo("방을 공개하지 못했소. 같은 Wi-Fi와 네트워크 권한을 확인해주시오."));
                Assert.That(registry.TryGet(first, out _), Is.False);
                Assert.That(Ready(registry, second), Is.True, "Other cards are untouched.");
                using (var released = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    Assert.DoesNotThrow(() => released.Bind(new IPEndPoint(IPAddress.Any, port)));
            }
        }

        /// <summary>Ticks until this card's native registration runs; local name conflicts settle within a few ticks.</summary>
        private static void StartRegistration(GamePendingRoomRegistry registry, GamePendingRoomHandle handle, string because = null)
        {
            for (int tick = 0; tick < 64 && !Advertisement(handle.Room).IsAdvertising; tick++)
            {
                registry.Tick(out string error);
                Assert.That(error, Is.Empty);
            }
            Assert.That(Advertisement(handle.Room).IsAdvertising, Is.True, because ?? Advertisement(handle.Room).LastError);
        }

        private static bool Ready(GamePendingRoomRegistry registry, GamePendingRoomHandle handle) =>
            registry.TryGet(handle, out GamePendingRoomInfo info) && info.Ready;

        internal static LabBonjourDiscovery Advertisement(LabPendingRoom room) =>
            (LabBonjourDiscovery)Field(room, "advertisement").GetValue(room);

        /// <summary>Ends the room's running registration the way a native error or timeout does.</summary>
        internal static void FailRegistration(LabPendingRoom room, string reason)
        {
            LabBonjourDiscovery discovery = Advertisement(room);
            object operation = typeof(LabBonjourDiscovery).GetField("advertisement", Private).GetValue(discovery);
            Assert.That(operation, Is.Not.Null, "A registration is running.");
            typeof(LabBonjourDiscovery).GetMethod("Fail", Private).Invoke(discovery, new[] { operation, reason });
            Assert.That(discovery.IsAdvertising, Is.False);
        }

        internal static FieldInfo Field(object target, string name) => target.GetType().GetField(name, Private);
    }
}
