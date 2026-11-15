using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabStaffStageDemoRoomTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void PresenceAndDualStackRoomCatalogKeepCanonicalStageIdentity(int stage)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                string protocol = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
                byte[] record = LabRoomPresence.Encode(protocol, true, true, 1, 3, false, true, false, stage, 1);
                Assert.That(LabRoomPresence.TryDecode(record, protocol, out bool v4, out bool v6,
                    out int count, out int capacity, out bool playing, out bool waiting, out bool team, out int decoded, out int group), Is.True);
                Assert.That((v4, v6, count, capacity, playing, waiting, team, decoded, group),
                    Is.EqualTo((true, true, 1, 3, false, true, false, stage, 1)));
                string name = LabPendingRoom.StaffStageName(stage, 1);
                var routes = new[] {
                    new LabBonjourTarget("192.0.2.1", 7777, 4, name, 2, 3, false, false, false, stage, 1),
                    new LabBonjourTarget("2001:db8::1", 7777, 4, name, 2, 3, false, false, false, stage, 1)
                };
                var rooms = LabRoomCatalog.GroupTargets(routes);
                Assert.That(rooms.Count, Is.EqualTo(1));
                Assert.That(rooms[0].DemoStageIndex, Is.EqualTo(stage));
                Assert.That(LabRoomCatalog.RoutesForRoom(routes, rooms[0].Key).Length, Is.EqualTo(2));
            }
        }

        [TestCase("s=0")] [TestCase("s=4")] [TestCase("s=01")] [TestCase("s=1;s=2")]
        [TestCase("s=1;w=1")] [TestCase("s=-1")] [TestCase("s=1;t=1")]
        public void MalformedStageMetadataCannotBecomeAJoinCandidate(string suffix)
        {
            string protocol = "C6-HAPIO-STAFF-1-5704";
            byte[] bytes = Encoding.UTF8.GetBytes("p=" + protocol + ";f=46;n=1;c=3;b=0;" + suffix);
            byte[] record = new byte[bytes.Length + 1]; record[0] = (byte)bytes.Length;
            Buffer.BlockCopy(bytes, 0, record, 1, bytes.Length);
            Assert.That(LabRoomPresence.TryDecode(record, protocol, out _, out _, out _, out _, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void StaffStageMarkerRequiresThreePlayerStoryAndNormalMetadataIsUnchanged()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LabRoomPresence.Encode("test", true, false, 1, 2, false, false, false, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => LabRoomPresence.Encode("test", true, false, 1, 4, false, false, true, 1));
            byte[] normal = LabRoomPresence.Encode("test", true, true, 2, 3, false);
            Assert.That(Encoding.UTF8.GetString(normal, 1, normal.Length - 1), Is.EqualTo("p=test;f=46;n=2;c=3;b=0"));
            Assert.That(LabRoomPresence.TryDecode(normal, "test", out _, out _, out _, out _, out _, out _, out _, out int stage), Is.True);
            Assert.That(stage, Is.Zero);
        }

        [Test]
        public void EachStageHasDistinctAdmissionTokenAndNormalLabAndGameRemainUnchanged()
        {
            var go = new GameObject("staff stage compatibility");
            try
            {
                var network = go.AddComponent<LabNetwork>();
                byte[] lab = Token(network);
                Assert.That(network.SetGameFlowEnabled(true), Is.True);
                byte[] normal = Token(network);
                Assert.That(Encoding.ASCII.GetString(normal), Is.EqualTo("C6HapioGame/56"));
                string normalDiscovery = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
                using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
                {
                    Assert.That(LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game), Is.Not.EqualTo(normalDiscovery));
                    byte[][] tokens = new byte[3][];
                    for (int stage = 1; stage <= 3; stage++)
                    {
                        Assert.That(network.SetDemoStageIdentity(stage, 1), Is.True);
                        tokens[stage - 1] = Token(network);
                        Assert.That(Encoding.ASCII.GetString(tokens[stage - 1]), Is.EqualTo("C6HapioStaffDemo/3/S" + stage + "/G1"));
                    }
                    Assert.That(tokens.Select(Encoding.ASCII.GetString).Distinct().Count(), Is.EqualTo(3));
                    Assert.That(tokens.All(value => !value.SequenceEqual(normal)), Is.True);
                    MethodInfo decode = typeof(LabNetwork).GetMethod("TryReadConnectionPayload", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.That(decode.Invoke(null, new object[] { tokens[0], tokens[1], true, null }), Is.False,
                        "Stage identity is authoritative admission data, not just a room-card label.");
                    Assert.That(decode.Invoke(null, new object[] { tokens[1], tokens[1], true, null }), Is.True);
                    Assert.That(decode.Invoke(null, new object[] {
                        Encoding.ASCII.GetBytes("C6HapioStaffDemo/1/S2"), tokens[1], true, null }), Is.False,
                        "A staff app without shared interrupted retry cannot join the new recovery room.");
                    Assert.That(network.SetTargetParticipantCount(2), Is.False);
                    Assert.That(network.SetTargetParticipantCount(4), Is.False);
                    Assert.That(network.SetTargetParticipantCount(3), Is.True);
                    Assert.That(network.SetGameFlowEnabled(false), Is.True);
                    Assert.That(Token(network), Is.EqualTo(lab));
                }
                network.SetGameFlowEnabled(true);
                Assert.That(Token(network), Is.EqualTo(normal));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FixedStageConflictFailsInsteadOfRenumberingOrSpendingAnotherRoom()
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var advertisement = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                // No DNS operation is needed to deliver a confirmed name-conflict result.
                var pending = (LabPendingRoom)Activator.CreateInstance(typeof(LabPendingRoom), true);
                try
                {
                    typeof(LabPendingRoom).GetField("<DemoStageIndex>k__BackingField", Private).SetValue(pending, 2);
                    typeof(LabPendingRoom).GetField("advertisement", Private).SetValue(pending, advertisement);
                    typeof(LabPendingRoom).GetField("registrationStarted", Private).SetValue(pending, true);
                    typeof(LabBonjourDiscovery).GetField("<RegistrationNameConflict>k__BackingField", Private).SetValue(advertisement, true);
                    Assert.That(typeof(LabPendingRoom).GetMethod("RetryNameConflict", Private).Invoke(pending, null), Is.True);
                    Assert.That(pending.DemoNameConflict, Is.True);
                    Assert.That(pending.Failed, Is.True);
                    Assert.That(typeof(LabPendingRoom).GetField("candidateNumber", Private).GetValue(pending), Is.EqualTo(1));
                    Assert.That(pending.Name, Is.Empty);
                    Assert.That(pending.RetryPending, Is.False);
                }
                finally { pending.Dispose(); }
            }
        }

        [Test]
        public void UnexpectedConfirmedStaffNameIsRejectedAndItsReservationReleased()
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var advertisement = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                var pending = (LabPendingRoom)Activator.CreateInstance(typeof(LabPendingRoom), true);
                try
                {
                    typeof(LabPendingRoom).GetField("<DemoStageIndex>k__BackingField", Private).SetValue(pending, 2);
                    typeof(LabPendingRoom).GetField("<DemoGroupId>k__BackingField", Private).SetValue(pending, 1);
                    typeof(LabPendingRoom).GetField("advertisement", Private).SetValue(pending, advertisement);
                    typeof(LabBonjourDiscovery).GetField("<AdvertisingConfirmed>k__BackingField", Private).SetValue(advertisement, true);
                    typeof(LabBonjourDiscovery).GetField("<RegisteredServiceName>k__BackingField", Private).SetValue(advertisement,
                        LabPendingRoom.StaffStageName(2, 1) + "-renamed");
                    Assert.That(pending.Ready, Is.False);
                    Assert.That(typeof(LabPendingRoom).GetMethod("RejectUnexpectedStaffName", Private).Invoke(pending, null), Is.True);
                    Assert.That(pending.DemoNameConflict && pending.Failed, Is.True);
                    Assert.That(pending.Name, Is.Empty);
                    Assert.That(pending.RetryPending, Is.False);
                }
                finally { pending.Dispose(); }
            }
        }

        [TestCase("s=2")] [TestCase("g=1")] [TestCase("s=2;g=0")] [TestCase("s=2;g=3")]
        [TestCase("s=2;g=01")] [TestCase("s=2;g=1;g=2")] [TestCase("g=1;s=2")]
        [TestCase("s=2;g=1;t=1")] [TestCase("t=1;s=2;g=1")]
        public void MissingOrMalformedGroupCannotBecomeAStaffCandidate(string suffix)
        {
            string protocol = "C6-HAPIO-STAFF-3-5705";
            byte[] bytes = Encoding.UTF8.GetBytes("p=" + protocol + ";f=46;n=1;c=3;b=0;" + suffix);
            byte[] record = new byte[bytes.Length + 1]; record[0] = (byte)bytes.Length;
            Buffer.BlockCopy(bytes, 0, record, 1, bytes.Length);
            Assert.That(LabRoomPresence.TryDecode(record, protocol, out _, out _, out _, out _, out _, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void GroupIdentitySurvivesDualStackCatalogAndConflictingRoutesAreNotJoinable()
        {
            string a = LabPendingRoom.StaffStageName(2, 1), b = LabPendingRoom.StaffStageName(2, 2);
            var targets = new[] {
                new LabBonjourTarget("192.0.2.1", 7001, 4, a, 3, 3, true, false, false, 2, 1),
                new LabBonjourTarget("2001:db8::1", 7001, 4, a, 3, 3, true, false, false, 2, 1),
                new LabBonjourTarget("192.0.2.2", 7002, 4, b, 1, 3, false, false, false, 2, 2)
            };
            var rooms = LabRoomCatalog.GroupTargets(targets);
            Assert.That(rooms.Count, Is.EqualTo(2));
            Assert.That(rooms.Select(room => room.DemoGroupId), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(LabRoomCatalog.RoutesForRoom(targets, LabRoomCatalog.Key(a, 7001)).Length, Is.EqualTo(2));
            var conflicting = new[] { targets[0], new LabBonjourTarget("2001:db8::1", 7001, 4, a, 1, 3, false, false, false, 2, 2) };
            Assert.That(LabRoomCatalog.GroupTargets(conflicting), Is.Empty, "A single name/port cannot be both groups.");
            Assert.That(LabRoomCatalog.RoutesForRoom(conflicting, LabRoomCatalog.Key(a, 7001)), Is.Empty);
        }

        [Test]
        public void StaffGroupSelectionIsExplicitAndChangingItRetiresPriorRoomTicketsAndRoutes()
        {
            var go = new GameObject("staff group identity");
            try
            {
                var network = go.AddComponent<LabNetwork>();
                network.SetGameFlowEnabled(true);
                using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
                {
                    Assert.That(network.SetDemoStageIdentity(2), Is.False, "Unselected never silently means group A.");
                    Assert.That(network.SetDemoStageIdentity(2, 0), Is.False);
                    Assert.That(network.SetDemoStageIdentity(0, 1), Is.False);
                    Assert.That(network.SetDemoStageIdentity(2, 1), Is.True);
                    byte[] a = Token(network);
                    typeof(LabNetwork).GetField("rejoinTicket", Private).SetValue(network, "old-group-ticket");
                    typeof(LabNetwork).GetField("rejoinTicketAdmitted", Private).SetValue(network, true);
                    var routes = (System.Collections.Generic.List<string>)typeof(LabNetwork).GetField("joinCandidates", Private).GetValue(network);
                    routes.Add("192.0.2.1");
                    Assert.That(network.SetDemoStageIdentity(2, 2), Is.True);
                    Assert.That(Token(network), Is.Not.EqualTo(a));
                    Assert.That(Encoding.ASCII.GetString(Token(network)), Is.EqualTo("C6HapioStaffDemo/3/S2/G2"));
                    Assert.That(typeof(LabNetwork).GetField("rejoinTicket", Private).GetValue(network), Is.Null);
                    Assert.That(routes, Is.Empty);
                    Assert.That(network.DemoGroupId, Is.EqualTo(2));
                    Assert.That(network.SetDemoStageIdentity(0), Is.True);
                    Assert.That((network.DemoStageIndex, network.DemoGroupId), Is.EqualTo((0, 0)));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(1)] [TestCase(2)]
        public void GroupedStaffNameAndPresenceMustAgreeBeforeNativeRoutesAreAccepted(int group)
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var discovery = new LabBonjourDiscovery(LabRoomFamily.Game))
            {
                MethodInfo accepts = typeof(LabBonjourDiscovery).GetMethod("AcceptsDemoStage", Private);
                string name = LabPendingRoom.StaffStageName(2, group);
                Assert.That(accepts.Invoke(discovery, new object[] { name, 2, group }), Is.True);
                Assert.That(accepts.Invoke(discovery, new object[] { name, 2, 3 - group }), Is.False);
                Assert.That(accepts.Invoke(discovery, new object[] { name, 1, group }), Is.False);
                Assert.That(accepts.Invoke(discovery, new object[] { "Hapio-Staff-2-5705-Stage-2", 2, group }), Is.False);
                Assert.That(accepts.Invoke(discovery, new object[] { name, 0, 0 }), Is.False);
            }
        }

        private static byte[] Token(LabNetwork network) => (byte[])typeof(LabNetwork).GetProperty("ActiveConnectionToken", Private).GetValue(network);
    }
}
