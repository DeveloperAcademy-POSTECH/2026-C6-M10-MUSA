using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Inactive NGO-role fixtures run the actual request and HUD paths without sockets or physical sensors.</summary>
    public sealed class LabFlashShakeRequestTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HostRejectsDelayedReleaseDuringPauseAndAcceptsItsCurrentGenerationAfterResume()
        {
            using (var f = new Fixture())
            {
                f.Clock.Pause(Time.realtimeSinceStartupAsDouble);
                f.Receive(0ul, 1u, 1);
                Assert.That(f.Network.Snapshot.FlashReleasedAt(0), Is.EqualTo(-1d));
                f.Clock.Resume(Time.realtimeSinceStartupAsDouble);
                f.Receive(0ul, 1u, 2);
                Assert.That(f.Network.Snapshot.FlashReleasedAt(0), Is.GreaterThanOrEqualTo(f.Initial.flashLastAt));
            }
        }

        [Test]
        public void HostRejectsWrongSessionRoundMissingGenerationAndUnknownSender()
        {
            using (var f = new Fixture())
            {
                f.Receive(0ul, 0u, 1);
                f.Receive(0ul, 1u, 0);
                f.Receive(9ul, 1u, 2);
                f.Receive(0ul, 1u, 3, session: "old-room");
                f.Receive(0ul, 1u, 4, round: f.Initial.round + 1);
                Assert.That(f.Network.Snapshot.FlashReleasedAt(0), Is.EqualTo(-1d));
                Assert.That(f.Model.Snapshot(f.Clock.Now(Time.realtimeSinceStartupAsDouble)).FlashReleasedAt(0), Is.EqualTo(-1d));
            }
        }

        [UnityTest]
        public IEnumerator CompletedShakesRetryAfterPauseWithoutFloodingOrNeedingNewShakes()
        {
            using (var f = new Fixture())
            {
                f.Drive(false);
                for (int i = 0; i < 6; i++) f.Counter.AddOne();
                f.Clock.Pause(Time.realtimeSinceStartupAsDouble);
                f.Drive(true); // the last local snapshot predates the Host's pause
                Assert.That(f.Sequence, Is.EqualTo(1L));
                Assert.That(f.Network.Snapshot.FlashReleasedAt(0), Is.EqualTo(-1d));
                for (int i = 0; i < 20; i++) f.Drive(true);
                Assert.That(f.Sequence, Is.EqualTo(1L), "A completed local counter must not send each frame.");
                f.Initial.paused = true;
                yield return new WaitForSecondsRealtime(LabYaguangFlashHud.ShakeRetrySeconds + .05f);
                f.Drive(true);
                Assert.That(f.Sequence, Is.EqualTo(1L), "No reports while paused.");
                Assert.That(f.Hud.ShakeCount, Is.EqualTo(6));
                f.Clock.Resume(Time.realtimeSinceStartupAsDouble);
                f.Initial.paused = false;
                f.Drive(true);
                Assert.That(f.Sequence, Is.EqualTo(2L));
                Assert.That(f.Network.Snapshot.FlashReleasedAt(0), Is.GreaterThanOrEqualTo(f.Initial.flashLastAt));
                f.Drive(false);
                Assert.That(f.Sequence, Is.EqualTo(2L), "An accepted release needs no more reports.");
            }
        }

        [Test]
        public void NewSessionOrRoundWithTheSameFlashNumberRequiresNewShakes()
        {
            using (var f = new Fixture())
            {
                f.Drive(false);
                for (int i = 0; i < 6; i++) f.Counter.AddOne();
                f.Initial.round++;
                f.Drive(false);
                Assert.That(f.Hud.ShakeCount, Is.Zero);
                for (int i = 0; i < 6; i++) f.Counter.AddOne();
                f.Initial.sessionId = "new-room";
                f.Drive(false);
                Assert.That(f.Hud.ShakeCount, Is.Zero);
                Assert.That(f.Sequence, Is.Zero);
            }
        }

        [Test]
        public void TheStartBarrierBlocksCompletedShakeRequests()
        {
            using (var f = new Fixture())
            {
                f.Drive(false);
                for (int i = 0; i < 6; i++) f.Counter.AddOne();
                Set(f.Network, "gameFlowEnabled", true);
                f.Network.SetGameStartHeld(true);
                f.Drive(true);
                Assert.That(f.Sequence, Is.Zero);
                Assert.That(f.Network.Snapshot.FlashReleasedAt(0), Is.EqualTo(-1d));
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly Component manager;
            private readonly Type managerType;
            private readonly LabConfig config;
            public readonly LabNetwork Network;
            public readonly LabModel Model;
            public readonly LabSnapshot Initial;
            public readonly LabYaguangFlashHud Hud;
            public LabBattleClock Clock => Get<LabBattleClock>(Network, "battleClock");
            public LabShakeCounter Counter => Get<LabShakeCounter>(Hud, "shakeCounter");
            public long Sequence => Get<long>(Network, "requestSequence");

            public Fixture()
            {
                root = new GameObject("Inactive flash request fixture");
                root.SetActive(false);
                config = ScriptableObject.CreateInstance<LabConfig>();
                JsonUtility.FromJsonOverwrite("{\"yaguangFlashEnabled\":true,\"yaguangFlashTargetOne\":false,"
                    + "\"yaguangFlashFirstStartSeconds\":5,\"yaguangFlashWarningSeconds\":0,\"yaguangFlashGlareSeconds\":12,"
                    + "\"yaguangFlashRecoverySeconds\":1,\"yaguangFlashShakeRelease\":true,\"gourdColorComboEnabled\":false}", config);
                managerType = typeof(LabNetwork).GetField("manager").FieldType;
                manager = root.AddComponent(managerType);
                Network = root.AddComponent<LabNetwork>();
                typeof(LabNetwork).GetField("manager").SetValue(Network, manager);
                Network.config = config;
                object client = managerType.GetProperty("LocalClient").GetValue(manager);
                client.GetType().GetProperty("IsServer", Private).SetValue(client, true);
                client.GetType().GetProperty("IsClient", Private).SetValue(client, true);
                client.GetType().GetProperty("IsConnected", Private).SetValue(client, true);
                managerType.GetProperty("IsListening").SetValue(manager, true);
                Get<List<ulong>>(Network, "order").Add(0ul);
                Set(Network, "developerSolo", true);
                Set(Network, "sessionId", "flash-request");
                Model = new LabModel();
                double now = Time.realtimeSinceStartupAsDouble;
                Set(Clock, "pausedDuration", now - 6d);
                Model.BeginSolo("flash-request", 0ul, 0d, config);
                Initial = Model.Snapshot(Clock.Now(now));
                Assert.That(Initial.flashCount, Is.EqualTo(1u));
                Set(Network, "model", Model);
                Set(Network, "latest", Initial);
                Hud = root.AddComponent<LabYaguangFlashHud>();
                Hud.Bind(Network, null);
            }

            public void Receive(ulong sender, uint generation, long requestId, string session = null, uint? round = null)
            {
                Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
                object packet = Activator.CreateInstance(type, true);
                type.GetField("type").SetValue(packet, LabNetwork.FlashShakePacket);
                type.GetField("sessionId").SetValue(packet, session ?? Initial.sessionId);
                type.GetField("round").SetValue(packet, round ?? Initial.round);
                type.GetField("requestId").SetValue(packet, requestId);
                type.GetField("flashGeneration").SetValue(packet, generation);
                typeof(LabNetwork).GetMethod("HandleRequest", Private).Invoke(Network, new[] { (object)sender, packet });
            }

            public void Drive(bool myGlare) => typeof(LabYaguangFlashHud).GetMethod("DriveShake", Private)
                .Invoke(Hud, new object[] { Initial, myGlare, 6 });

            public void Dispose()
            {
                managerType.GetProperty("IsListening").SetValue(manager, false);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
