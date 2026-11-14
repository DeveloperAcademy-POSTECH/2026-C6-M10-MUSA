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
                byte[] record = LabRoomPresence.Encode(protocol, true, true, 1, 3, false, true, false, stage);
                Assert.That(LabRoomPresence.TryDecode(record, protocol, out bool v4, out bool v6,
                    out int count, out int capacity, out bool playing, out bool waiting, out bool team, out int decoded), Is.True);
                Assert.That((v4, v6, count, capacity, playing, waiting, team, decoded),
                    Is.EqualTo((true, true, 1, 3, false, true, false, stage)));
                string name = LabPendingRoom.StaffStageName(stage);
                var routes = new[] {
                    new LabBonjourTarget("192.0.2.1", 7777, 4, name, 2, 3, false, false, false, stage),
                    new LabBonjourTarget("2001:db8::1", 7777, 4, name, 2, 3, false, false, false, stage)
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
                Assert.That(Encoding.ASCII.GetString(normal), Is.EqualTo("C6HapioGame/55"));
                string normalDiscovery = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
                using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
                {
                    Assert.That(LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game), Is.Not.EqualTo(normalDiscovery));
                    byte[][] tokens = new byte[3][];
                    for (int stage = 1; stage <= 3; stage++)
                    {
                        Assert.That(network.SetDemoStageIdentity(stage), Is.True);
                        tokens[stage - 1] = Token(network);
                    }
                    Assert.That(tokens.Select(Encoding.ASCII.GetString).Distinct().Count(), Is.EqualTo(3));
                    Assert.That(tokens.All(value => !value.SequenceEqual(normal)), Is.True);
                    MethodInfo decode = typeof(LabNetwork).GetMethod("TryReadConnectionPayload", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.That(decode.Invoke(null, new object[] { tokens[0], tokens[1], true, null }), Is.False,
                        "Stage identity is authoritative admission data, not just a room-card label.");
                    Assert.That(decode.Invoke(null, new object[] { tokens[1], tokens[1], true, null }), Is.True);
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

        private static byte[] Token(LabNetwork network) => (byte[])typeof(LabNetwork).GetProperty("ActiveConnectionToken", Private).GetValue(network);
    }
}
