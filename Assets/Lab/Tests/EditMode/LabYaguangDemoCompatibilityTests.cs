using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabYaguangDemoCompatibilityTests
    {
        private static string Token(LabNetwork network) => Encoding.ASCII.GetString((byte[])typeof(LabNetwork)
            .GetProperty("ActiveConnectionToken", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(network));

        [Test]
        public void DemoAndNormalGameRoomsHaveDistinctDiscoveryAndConnectionWhileLabIsUnchanged()
        {
            string normalDiscovery, normalToken, labDiscovery, labToken;
            var root = new GameObject("Demo compatibility fixture");
            try
            {
                var network = root.AddComponent<LabNetwork>();
                using (LabGameBuildVariant.OverrideForTests(false))
                {
                    labDiscovery = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Lab);
                    labToken = Token(network);
                    Assert.That(network.SetGameFlowEnabled(true), Is.True);
                    normalDiscovery = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
                    normalToken = Token(network);
                    Assert.That(normalToken, Is.EqualTo("C6HapioGame/55"));
                }
                using (LabGameBuildVariant.OverrideForTests(true))
                {
                    Assert.That(Token(network), Is.Not.EqualTo(normalToken));
                    Assert.That(Token(network), Is.EqualTo(LabNetwork.GameConnectionCompatibility));
                    Assert.That(LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game), Is.Not.EqualTo(normalDiscovery));
                    Assert.That(LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Lab), Is.EqualTo(labDiscovery));
                    Assert.That(network.SetGameFlowEnabled(false), Is.True);
                    Assert.That(Token(network), Is.EqualTo(labToken));
                    Assert.That(network.SetTargetParticipantCount(4), Is.True, "The standalone Lab keeps its ordinary limits.");
                    Assert.That(network.SetGameFlowEnabled(true), Is.True);
                    Assert.That(network.SetTargetParticipantCount(4), Is.False);
                    Assert.That(network.SetTargetParticipantCount(2), Is.False);
                    Assert.That(network.SetTargetParticipantCount(3), Is.True);
                }
                Assert.That(Token(network), Is.EqualTo(normalToken), "Scoped QA cannot leave the normal Editor in demo mode.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ExplicitCancelCannotResumeADeferredTitleSearch()
        {
            var root = new GameObject("Deferred search cancellation fixture");
            try
            {
                var network = root.AddComponent<LabNetwork>();
                var deferred = typeof(LabNetwork).GetField("resumeRoomSearchAfterStop", BindingFlags.NonPublic | BindingFlags.Instance);
                deferred.SetValue(network, true);
                network.CancelRoomSearch();
                Assert.That(deferred.GetValue(network), Is.False);
                Assert.That(network.IsSearchingRooms, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(1, "gumiho", LabStageYokai.Gumiho, false, false)]
        [TestCase(2, "yaguang", LabStageYokai.Yaguang, false, true)]
        [TestCase(3, "jigwi", LabStageYokai.Jigwi, false, false)]
        [TestCase(1, "territory", LabStageYokai.None, true, false)]
        [TestCase(2, "yaguang", LabStageYokai.Gumiho, false, false)]
        public void DemoPreparationKeepsCanonicalYaguangOnly(int index, string id, LabStageYokai yokai, bool territory, bool expected)
        {
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                var method = typeof(LabNetwork).GetMethod("ValidGameStage", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method.Invoke(null, new object[] { index, id, yokai, territory }), Is.EqualTo(expected));
            }
        }
    }
}
