using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using C6.Prototype.Lobby.Discovery;
using NUnit.Framework;

namespace C6.Prototype.Lobby.Tests
{
    public sealed class L2DiscoveryReliabilityTests
    {
        private static RoomAdvertisement Room(ushort port = 7777) => new RoomAdvertisement
        {
            RoomId = "dual-stack-room", Name = "Test Room", ProtocolVersion = LobbyProtocol.ContinuousTransferVersion,
            Build = "test", ConfigHash = new string('a', 64), Status = RoomAdvertisementStatus.Lobby,
            Participants = 1, Port = port
        };

        [Test]
        public void InterfaceReportsMergeRoutesInsteadOfDiscardingTheOlderInterface()
        {
            var catalog = new DiscoveryRoomCatalog();
            catalog.Upsert("wifi", Room(), new[] { "192.168.10.4", "fd12:3456::4", "fe80::4%8" }, 0);
            catalog.Upsert("other", Room(), new[] { "192.168.20.4", "fd12:3456::4" }, 1);
            var room = catalog.Rooms.Single();
            Assert.That(room.Address, Is.EqualTo("fd12:3456::4"));
            Assert.That(room.Candidates, Is.EqualTo(new[] { "fd12:3456::4", "192.168.20.4", "fe80::4%8", "192.168.10.4" }));
            Assert.That(room.Addresses, Is.SameAs(room.Candidates));
            catalog.Remove("other");
            Assert.That(catalog.Rooms.Single().Candidates, Is.EqualTo(new[] { "fd12:3456::4", "192.168.10.4", "fe80::4%8" }));
        }

        [Test]
        public void SameLinkLocalAddressOnDifferentInterfacesRemainsTwoRoutes()
        {
            var catalog = new DiscoveryRoomCatalog();
            catalog.Upsert("wifi", Room(), "fe80::4%8", 0);
            catalog.Upsert("other", Room(), "fe80::4%12", 1);
            Assert.That(catalog.Rooms.Single().Candidates, Is.EquivalentTo(new[] { "fe80::4%8", "fe80::4%12" }));
        }

        [Test]
        public void CallerCollectionsAndPublishedCandidateListsCannotMutateLiveDiscovery()
        {
            var addresses = new List<string> { "fd12::4", "192.168.10.4" }; var catalog = new DiscoveryRoomCatalog();
            catalog.Upsert("wifi", Room(), addresses, 0); addresses.Clear(); var snapshot = catalog.Rooms.Single();
            Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.Candidates)[0] = "192.168.10.9");
            catalog.Remove("wifi"); Assert.That(snapshot.Candidates.Count, Is.EqualTo(2));
        }

        [Test]
        public void ConflictingPortBuildOrConfigCannotBorrowAnotherAdvertisementAddress()
        {
            var catalog = new DiscoveryRoomCatalog(); catalog.Upsert("old", Room(), "192.168.10.4", 0);
            catalog.Upsert("new", Room(7778), "fd12::4", 1);
            Assert.That(catalog.Rooms.Single().Candidates, Is.EqualTo(new[] { "fd12::4" }));
            var latest = Room(7778); latest.Build = "different-build";
            catalog.Upsert("newer", latest, "fd12::5", 2);
            Assert.That(catalog.Rooms.Single().Candidates, Is.EqualTo(new[] { "fd12::5" }));
            latest.ConfigHash = new string('b',64); catalog.Upsert("newest", latest, "fd12::6", 3);
            Assert.That(catalog.Rooms.Single().Candidates, Is.EqualTo(new[] { "fd12::6" }));
        }

        [Test]
        public void ExpiringOneInterfaceRemovesOnlyItsCandidates()
        {
            var catalog = new DiscoveryRoomCatalog(); catalog.Upsert("wifi", Room(), "fd12::4", 0);
            catalog.Upsert("other", Room(), "192.168.20.4", 3);
            Assert.That(catalog.Expire(12), Is.EqualTo(1));
            Assert.That(catalog.Rooms.Single().Candidates, Is.EqualTo(new[] { "192.168.20.4" }));
            Assert.That(catalog.Rooms.Single().ExpiresAtSeconds, Is.EqualTo(15));
        }

        [Test]
        public void AddressRefreshDoesNotReplaceNewerMetadataWithAnOlderInterfaceSnapshot()
        {
            var catalog = new DiscoveryRoomCatalog(); catalog.Upsert("old", Room(), "192.168.10.4", 0);
            var newer = Room(); newer.Participants = 2; catalog.Upsert("new", newer, "fd12::4", 2);
            catalog.Upsert("old", Room(), new[] { "192.168.10.4", "fe80::4%8" }, 3, 12);
            Assert.That(catalog.Rooms.Single().Participants, Is.EqualTo(2));
            Assert.That(catalog.Rooms.Single().Candidates, Has.Count.EqualTo(3));
        }

        [Test]
        public void AddressCallbackCannotExtendTheExplicitTxtLease()
        {
            var catalog = new DiscoveryRoomCatalog(); catalog.Upsert("wifi", Room(), new[] { "fd12::4" }, 0, 12);
            catalog.Upsert("wifi", Room(), new[] { "fd12::4", "192.168.10.4" }, 10, 12);
            Assert.That(catalog.Expire(12), Is.EqualTo(1));
            Assert.That(catalog.Upsert("wifi", Room(), new[] { "fd12::4" }, 12, 12), Is.False);
        }

        [Test]
        public void ManyIpv6RecordsCannotExcludeIpv4OrScopedLinkLocalFromEightCandidates()
        {
            var inputs = Enumerable.Range(1, 20).Select(i => "fd12::" + i).Concat(new[] { "192.168.10.4", "fe80::4%8" });
            string[] selected = DiscoveryAddressCandidates.Select(inputs);
            Assert.That(selected.Length, Is.EqualTo(8));
            Assert.That(selected.Take(3), Is.EqualTo(new[] { "fd12::1", "192.168.10.4", "fe80::4%8" }));
        }

        [Test]
        public void NativeRecordCapacityPreservesANewFamilyInsteadOfRejectingIt()
        {
            var inputs = Enumerable.Range(1, 32).Select(i => "192.168.10." + i).Concat(new[] { "fd12::4", "fe80::4%8" });
            string[] selected = DiscoveryAddressCandidates.SelectRecords(inputs);
            Assert.That(selected.Length, Is.EqualTo(32));
            Assert.That(selected, Does.Contain("fd12::4")); Assert.That(selected, Does.Contain("fe80::4%8"));
        }

        [Test]
        public void CanonicalEquivalentIpv6RecordsAreDeduplicated()
        {
            Assert.That(DiscoveryAddressCandidates.Select(new[] { "fd12:0000::4", "fd12::4", "fd12::4" }), Is.EqualTo(new[] { "fd12::4" }));
        }

        [TestCase("fd12:3456::4", true)] [TestCase("2001:db8::4", true)] [TestCase("fe80::4%8", true)]
        [TestCase("fe80::4", false)] [TestCase("ff02::1%8", false)] [TestCase("::", false)] [TestCase("::1", false)]
        [TestCase("::ffff:192.168.10.4", false)] [TestCase("127.0.0.1", false)] [TestCase("192.0.0.2", true)]
        [TestCase("fd12::4%8", false)] [TestCase("[fd12::4]", false)] [TestCase("fe80::4%0", false)]
        [TestCase("fe80::4%4294967295", false)] [TestCase("192.168.10.04", false)]
        public void DiscoveredCandidatesAreUsableUnicastRoutesWithoutContinuityAddressBlacklist(string input, bool expected)
            => Assert.That(DiscoveryAddressCandidates.TryNormalize(input, out _), Is.EqualTo(expected));

        [Test]
        public void ReplayedOrOlderCachedTxtDoesNotRenewHeartbeatLease()
        {
            Assert.That(RoomAdvertisementCodec.TryDecode(RoomAdvertisementCodec.Encode(Room(), 40), 7777,
                out _, out ulong sequence, out _), Is.True);
            var lease = new DiscoveryHeartbeatLease(); Assert.That(lease.TryRefresh(sequence, 5), Is.True);
            Assert.That(lease.IsFresh(16.999), Is.True); Assert.That(lease.TryRefresh(sequence, 16), Is.False);
            Assert.That(lease.TryRefresh(sequence - 1, 16), Is.False); Assert.That(lease.IsFresh(17), Is.False);
            Assert.That(lease.TryRefresh(sequence + 1, 18), Is.True); Assert.That(lease.ExpiresAt, Is.EqualTo(30));
        }

        [Test]
        public void RetryQueryWaitsForLiveHeartbeatWithoutRepublishingExpiredCachedTxt()
        {
            // Invoke the real TXT callback with owned managed contexts and fixture bytes only.
            // No native browse/register/socket operation is started by this regression test.
            using (var discovery = new BonjourRoomDiscovery())
            {
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                Type ownerType = typeof(BonjourRoomDiscovery);
                Type serviceType = ownerType.GetNestedType("Service", BindingFlags.NonPublic);
                object service = Activator.CreateInstance(serviceType, true);
                serviceType.GetField("Key", fields).SetValue(service, "fixture");
                serviceType.GetField("Port", fields).SetValue(service, (ushort)7777);
                ((Dictionary<string, double>)serviceType.GetField("Addresses", fields).GetValue(service)).Add("192.168.10.4", 100);
                var catalog = (DiscoveryRoomCatalog)ownerType.GetField("catalog", fields).GetValue(discovery);
                var clock = ownerType.GetField("now", fields);
                var create = ownerType.GetMethod("NewOperation", fields);
                object initial = create.Invoke(discovery, new[] { "TXT query", service });
                DeliverTxt(discovery, initial, RoomAdvertisementCodec.Encode(Room(), 40));
                Assert.That(catalog.Rooms.Count, Is.EqualTo(1));
                Assert.That(catalog.Expire(12), Is.EqualTo(1));

                clock.SetValue(discovery, 13d);
                object retry = create.Invoke(discovery, new[] { "TXT query", service });
                var hasResponse = retry.GetType().GetField("HasResponse", fields);
                var deadline = retry.GetType().GetField("DeadlineAt", fields);
                DeliverTxt(discovery, retry, RoomAdvertisementCodec.Encode(Room(), 40));
                Assert.That(hasResponse.GetValue(retry), Is.False, "A stale cached q must not end the retry's first-response window.");
                Assert.That(deadline.GetValue(retry), Is.EqualTo(21d));
                Assert.That(catalog.Rooms, Is.Empty, "Waiting for a live heartbeat must not renew the stale visible room.");

                clock.SetValue(discovery, 16d);
                DeliverTxt(discovery, retry, RoomAdvertisementCodec.Encode(Room(), 41));
                Assert.That(hasResponse.GetValue(retry), Is.True);
                Assert.That(catalog.Rooms.Single().ExpiresAtSeconds, Is.EqualTo(28d));
            }
        }

        private static void DeliverTxt(BonjourRoomDiscovery discovery, object operation, byte[] bytes)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            IntPtr context = (IntPtr)operation.GetType().GetProperty("Context", hidden).GetValue(operation);
            IntPtr data = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, data, bytes.Length);
                typeof(BonjourRoomDiscovery).GetMethod("OnTxt", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { IntPtr.Zero, 2u, 1u, 0, IntPtr.Zero, (ushort)16, (ushort)1, (ushort)bytes.Length, data, 120u, context });
            }
            finally { Marshal.FreeHGlobal(data); }
        }

        [Test]
        public void AdvertisementReregistrationAndNewRoomsDoNotRestartTheInstanceSequence()
        {
            var sequence = new DiscoveryHeartbeatSequence(); var lease = new DiscoveryHeartbeatLease();
            Assert.That(sequence.TryAdvance(), Is.True); // First registration.
            Assert.That(lease.TryRefresh(sequence.Current, 0), Is.True);
            Assert.That(sequence.TryAdvance(), Is.True); // Scheduled heartbeat.
            Assert.That(lease.TryRefresh(sequence.Current, 3), Is.True);
            Assert.That(sequence.TryAdvance(), Is.True); // Re-registration with a cached PTR identity.
            Assert.That(lease.TryRefresh(sequence.Current, 4), Is.True);
            Assert.That(sequence.TryAdvance(), Is.True); // Another RoomId can use the same monotonic instance counter.
            byte[] txt = RoomAdvertisementCodec.Encode(Room(), sequence.Current);
            Assert.That(RoomAdvertisementCodec.TryDecode(txt, 7777, out _, out ulong decoded, out _), Is.True);
            Assert.That(decoded, Is.EqualTo(4));
        }

        [Test]
        public void ExhaustedHeartbeatFailsWithoutWrappingIntoAnOlderSequence()
        {
            var sequence = new DiscoveryHeartbeatSequence(ulong.MaxValue - 1);
            Assert.That(sequence.TryAdvance(), Is.True); Assert.That(sequence.Current, Is.EqualTo(ulong.MaxValue));
            Assert.That(sequence.TryAdvance(), Is.False); Assert.That(sequence.Current, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void HeartbeatCannotMoveItsClockBackwardsOrAcceptNonfiniteTime()
        {
            var lease = new DiscoveryHeartbeatLease(); lease.TryRefresh(1, 5);
            Assert.That(lease.TryRefresh(2, 4), Is.False); Assert.That(lease.TryRefresh(2, double.NaN), Is.False);
            Assert.That(lease.TryRefresh(2, double.PositiveInfinity), Is.False); Assert.That(lease.ExpiresAt, Is.EqualTo(17));
        }

        [Test]
        public void ResolutionRecoveryWaitsThenStopsAfterThreeAttempts()
        {
            var recovery = new DiscoveryRecoveryState(); recovery.StartAttempt(); Assert.That(recovery.Schedule(8), Is.True);
            Assert.That(recovery.IsDue(8.999), Is.False); Assert.That(recovery.IsDue(9), Is.True);
            recovery.StartAttempt(); Assert.That(recovery.Schedule(17), Is.True);
            Assert.That(recovery.IsDue(18.999), Is.False); Assert.That(recovery.IsDue(19), Is.True);
            recovery.StartAttempt(); Assert.That(recovery.Schedule(27), Is.False); Assert.That(recovery.IsDue(1000), Is.False);
        }

        [Test]
        public void LiveHeartbeatAllowsRecoveryFromAFutureIndependentOutage()
        {
            var recovery = new DiscoveryRecoveryState(); recovery.StartAttempt(); recovery.Schedule(8); recovery.StartAttempt();
            recovery.Schedule(17); recovery.StartAttempt(); recovery.ConfirmLiveHeartbeat();
            Assert.That(recovery.Attempts, Is.EqualTo(1)); Assert.That(recovery.Schedule(40), Is.True);
            Assert.That(recovery.NextAttemptAt, Is.EqualTo(41));
        }
    }
}
