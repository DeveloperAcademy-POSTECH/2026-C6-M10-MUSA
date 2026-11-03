using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// NGO builds a new messaging manager for every start. A session that ends underneath the lobby (Host left, phone
    /// slept, Wi-Fi dropped) does not pass through <see cref="LabNetwork.Stop"/>, and the next session in the same run
    /// must still register the packet handlers on its own manager instead of trusting the old registration (#471).
    /// The test assembly does not reference Netcode, so the manager is read by reflection.
    /// </summary>
    public sealed class LabMessageHandlerReconnectTests
    {
        private static readonly string[] Expected = { "C6Lab.Packet.3", "C6Lab.Bulk.1" };

        [UnityTest]
        public IEnumerator ASessionThatEndsWithoutStopStillRegistersTheNextSessionsHandlers()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabNetwork network = Object.FindAnyObjectByType<LabNetwork>();
            network.port = 18273;
            object manager = typeof(LabNetwork).GetField("manager").GetValue(network);
            try
            {
                network.Host();
                Assert.That(network.IsHost, Is.True, network.Notice);
                object first = Get(manager, "CustomMessagingManager");
                Assert.That(Handlers(first), Is.EquivalentTo(Expected));

                // The transport ends underneath the room, not through 나가기.
                manager.GetType().GetMethod("Shutdown").Invoke(manager, new object[] { false });
                float until = Time.realtimeSinceStartup + 5f;
                while (((bool)Get(manager, "IsListening") || (bool)Get(manager, "ShutdownInProgress"))
                    && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(network.CanChooseConnection, Is.True, "The ended session leaves the connection choosable.");

                network.Host();
                Assert.That(network.IsHost, Is.True, network.Notice);
                object second = Get(manager, "CustomMessagingManager");
                Assert.That(second, Is.Not.SameAs(first), "NGO starts a new messaging manager.");
                Assert.That(Handlers(second), Is.EquivalentTo(Expected), "The next session receives packets.");
            }
            finally { network.Stop(); }
            yield return null;
            Assert.That((bool)Get(manager, "IsListening"), Is.False);
        }

        private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);

        private static IEnumerable<string> Handlers(object messaging) => ((Dictionary<ulong, string>)messaging.GetType()
            .GetField("m_MessageHandlerNameLookup32", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(messaging)).Values;
    }
}
