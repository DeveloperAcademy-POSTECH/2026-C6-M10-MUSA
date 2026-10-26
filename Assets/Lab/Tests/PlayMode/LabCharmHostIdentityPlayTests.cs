using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabCharmHostIdentityPlayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();
        private LabConfig controlledConfig;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return fixture.Cleanup();
            if (controlledConfig != null) UnityEngine.Object.Destroy(controlledConfig);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RescuingTheHostRaisesTheLocalWakeEventExactlyOnceWithPlayerZero()
        {
            yield return fixture.Load();
            LabNetwork network = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single().network;
            fixture.UseAvailablePort(network);
            Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
            Assert.That(network.LocalPlayerId, Is.Zero, "The actual NGO Host owns player zero.");
            network.enabled = false;
            controlledConfig = UnityEngine.Object.Instantiate(network.config);
            float[] values = controlledConfig.CaptureDeveloperValues();
            values[166] = 0f;
            values[177] = 3f;
            values[202] = 0f; // No warning (#305): the charm starts the moment StartCharm runs.
            Assert.That(controlledConfig.TryApplyDeveloperValues(values, out string error), Is.True, error);
            network.config = controlledConfig;

            // The Host and its actual request handler run, while a controlled two-player model and
            // synthetic sender isolate the wake event. This does not claim two connected clients.
            const string session = "host-zero-wake-event";
            const ulong neighbor = 20ul;
            double now = Time.realtimeSinceStartupAsDouble;
            var model = new LabModel();
            model.Begin(session, new ulong[] { 0, neighbor }, now, controlledConfig);
            typeof(LabModel).GetMethod("StartCharm", Private).Invoke(model, new object[] { 0ul, now });
            typeof(LabNetwork).GetField("model", Private).SetValue(network, model);
            typeof(LabNetwork).GetField("sessionId", Private).SetValue(network, session);
            typeof(LabNetwork).GetField("latest", Private).SetValue(network, model.Snapshot(now));
            var order = (List<ulong>)typeof(LabNetwork).GetField("order", Private).GetValue(network);
            order.Add(neighbor);

            var wakes = new List<ulong>();
            network.CharmWoken += wakes.Add;
            Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(packetType, true);
            packetType.GetField("type").SetValue(packet, "charmTap");
            packetType.GetField("sessionId").SetValue(packet, session);
            packetType.GetField("round").SetValue(packet, model.Round);
            MethodInfo handle = typeof(LabNetwork).GetMethod("HandleRequest", Private);
            for (long tap = 1; tap <= 2; tap++)
            {
                packetType.GetField("requestId").SetValue(packet, tap);
                handle.Invoke(network, new[] { (object)neighbor, packet });
                Assert.That(wakes, Is.Empty, "A partial rescue must not announce a wake.");
                Assert.That(network.Snapshot.CharmOn(0), Is.True);
            }

            packetType.GetField("requestId").SetValue(packet, 3L);
            handle.Invoke(network, new[] { (object)neighbor, packet });
            Assert.That(network.Snapshot.CharmOn(0), Is.False, "The third accepted tap ends the Host's charm.");
            Assert.That(wakes, Is.EqualTo(new ulong[] { 0 }), "The Host receives the same wake feedback as every other player.");
            packetType.GetField("requestId").SetValue(packet, 4L);
            handle.Invoke(network, new[] { (object)neighbor, packet });
            Assert.That(wakes, Is.EqualTo(new ulong[] { 0 }), "Further taps cannot replay a completed wake.");
        }
    }
}
