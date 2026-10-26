using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Inactive NGO role fixtures exercise the real gates without claiming a transport test.</summary>
    public sealed class LabGameStartBarrierTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void PreparedRoundHoldsGameplayWithoutExposingUserPauseAndRefusesEarlyActions()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Prepare(), Is.True);
                LabSnapshot initial = fixture.Network.Snapshot;
                Assert.That(fixture.Network.GameStartHeld, Is.True);
                Assert.That(initial.playedSeconds, Is.Zero);
                Assert.That(initial.remaining, Is.EqualTo(fixture.Config.BattleDurationSeconds));
                Assert.That(initial.paused, Is.False, "The countdown must not open the user pause menu.");
                Assert.That(fixture.Network.IsBattlePaused, Is.False);
                Assert.That(fixture.Network.CanPauseBattle, Is.False);
                Assert.That(fixture.Network.CanResumeBattle, Is.False);
                Assert.That(fixture.Network.CanRestartSharedBattle, Is.False);
                Assert.That(fixture.Network.PauseBattle(), Is.False);
                Assert.That(fixture.Network.ResumeBattle(), Is.False);
                Assert.That(fixture.Network.RestartGameSoloWithConfig(fixture.Config), Is.False);
                Assert.That(Get<bool>(fixture.Network.board, "interactionEnabled"), Is.False);

                var model = Get<LabModel>(fixture.Network, "model");
                ulong revision = model.Revision;
                fixture.Network.Generate();
                Assert.That(model.Revision, Is.EqualTo(revision));
                Assert.That(fixture.Network.ActionRequestTransmissions, Is.Zero);
                var clock = Get<LabBattleClock>(fixture.Network, "battleClock");
                double later = clock.Now(Time.realtimeSinceStartupAsDouble + 10000d);
                model.Tick(later);
                LabSnapshot held = model.Snapshot(later);
                Assert.That(held.playedSeconds, Is.Zero);
                Assert.That(held.remaining, Is.EqualTo(initial.remaining));
                Assert.That(held.paintRevision, Is.EqualTo(initial.paintRevision));
                Assert.That(held.players[0].stamina, Is.EqualTo(initial.players[0].stamina));
                Assert.That(held.players[0].personalHp, Is.EqualTo(initial.players[0].personalHp));
            }
        }

        [Test]
        public void OnlyTheMatchingPreparedRoundCanReleaseAndItReleasesExactlyOnce()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Prepare(), Is.True);
                LabSnapshot initial = fixture.Network.Snapshot;
                Assert.That(fixture.Network.IsGameInitialStateReady("old-stage", initial.round), Is.False);
                Assert.That(fixture.Network.IsGameInitialStateReady(initial.sessionId, initial.round + 1), Is.False);
                Assert.That(fixture.Network.ReleasePreparedGameStage("old-stage", initial.round), Is.False);
                Assert.That(fixture.Network.ReleasePreparedGameStage(initial.sessionId, initial.round + 1), Is.False);
                Assert.That(fixture.Network.GameStartHeld, Is.True);
                Assert.That(fixture.Prepare(), Is.False, "A retry cannot recreate an already prepared model.");
                Assert.That(fixture.Network.ReleasePreparedGameStage(initial.sessionId, initial.round), Is.True);
                Assert.That(fixture.Network.GameStartHeld, Is.False);
                Assert.That(Get<LabBattleClock>(fixture.Network, "battleClock").IsPaused, Is.False);
                Assert.That(Get<bool>(fixture.Network.board, "interactionEnabled"), Is.True);
                Assert.That(fixture.Network.ReleasePreparedGameStage(initial.sessionId, initial.round), Is.False);
            }
        }

        [Test]
        public void ClientCannotAcknowledgeSnapshotAloneWithoutItsOwnerCheckpoint()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Prepare(), Is.True);
                LabSnapshot initial = fixture.Network.Snapshot;
                fixture.SetHost(false);
                Set(fixture.Network, "clientConfigReady", true);
                Assert.That(fixture.Network.IsConnected, Is.True);
                Assert.That(fixture.Network.IsGameInitialStateReady(initial.sessionId, initial.round), Is.False,
                    "A full snapshot can seed an owner baseline, but does not prove the owner stream arrived.");
                Assert.That(fixture.Network.ReleasePreparedGameStage(initial.sessionId, initial.round), Is.False);
                Assert.That(fixture.Network.GameStartHeld, Is.True);

                var receiver = (LabPaintReceiver)typeof(LabNetwork).GetProperty("PaintReceiver", Private)
                    .GetValue(fixture.Network);
                var model = Get<LabModel>(fixture.Network, "model");
                var publisher = new LabPaintPublisher();
                foreach (var frame in publisher.Publish(model.PaintSurface, initial.sessionId, initial.round, 1))
                    receiver.Receive(frame);
                var boardPublisher = new LabBoardPublisher();
                var replica = Get<LabBoardReplica>(fixture.Network, "boardReplica");
                Assert.That(replica.TryApply(boardPublisher.Build(initial, 0, 1, model.Revision, true), out _), Is.True);
                Assert.That(fixture.Network.IsGameInitialStateReady(initial.sessionId, initial.round), Is.True);
                fixture.Network.SetGameStartHeld(false);
                Assert.That(fixture.Network.IsGameInitialStateReady(initial.sessionId, initial.round), Is.True,
                    "Readiness stays usable while a committed start awaits its Playing control.");
                Assert.That(replica.TryApply(boardPublisher.Build(initial, 0, 3, model.Revision), out _), Is.False);
                Assert.That(fixture.Network.IsGameInitialStateReady(initial.sessionId, initial.round), Is.False,
                    "A gap must recover its checkpoint before a new start receipt is sent.");
            }
        }

        [Test]
        public void StartHoldPinsLocalPhysicsAndRestoresExistingMomentumWithoutGlobalPause()
        {
            using (var fixture = new Fixture())
            {
                LabOrbBoard board = fixture.Network.board;
                var orb = board.UpsertOrb("momentum", LabOrbKind.Yin, new Vector2(.5f, .5f), new Vector2(.4f, .2f));
                Vector3 velocity = orb.Body.linearVelocity;
                Vector3 position = orb.Body.position;
                var hanging = fixture.Network.target.gameObject.AddComponent<LabHangingTarget>();
                Set(hanging, "cycleElapsed", .25f);
                Set(hanging, "yawSpeedDegreesPerSecond", 12f);
                float scale = Time.timeScale;
                fixture.Network.SetGameStartHeld(true);
                Assert.That(board.SimulationHeld, Is.True);
                Assert.That(hanging.SimulationHeld, Is.True);
                Assert.That(orb.Body.isKinematic, Is.True, "PhysX cannot move the held drum/orb between Updates.");
                for (int i = 0; i < 10; i++)
                {
                    typeof(LabOrbBoard).GetMethod("FixedUpdate", Private).Invoke(board, null);
                    typeof(LabHangingTarget).GetMethod("FixedUpdate", Private).Invoke(hanging, null);
                }
                Assert.That(orb.Body.position, Is.EqualTo(position));
                Assert.That(orb.Body.linearVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(hanging.CycleElapsed, Is.EqualTo(.25f));
                Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(12f));
                Assert.That(Time.timeScale, Is.EqualTo(scale));
                fixture.Network.SetGameStartHeld(false);
                Assert.That(board.SimulationHeld, Is.False);
                Assert.That(hanging.SimulationHeld, Is.False);
                Assert.That(orb.Body.isKinematic, Is.False);
                Assert.That(orb.Body.linearVelocity, Is.EqualTo(velocity));
            }
        }

        [Test]
        public void HoldIsScopedToGameFamilyAndUnbindRetiresIt()
        {
            using (var fixture = new Fixture())
            {
                fixture.Network.SetGameStartHeld(true);
                Assert.That(fixture.Network.GameStartHeld, Is.True);
                fixture.Network.UnbindBattleReferences();
                Assert.That(fixture.Network.GameStartHeld, Is.False);
                Assert.That(fixture.Network.IsGameInitialStateReady("missing", 1), Is.False);
                fixture.Network.SetGameStartHeld(true);
                typeof(LabNetwork).GetMethod("ResetLocal", Private).Invoke(fixture.Network, null);
                Assert.That(fixture.Network.GameStartHeld, Is.False);
                fixture.SetListening(false);
                Assert.That(fixture.Network.SetGameFlowEnabled(false), Is.True);
                fixture.Network.SetGameStartHeld(true);
                Assert.That(fixture.Network.GameStartHeld, Is.False, "Legacy Lab never opts into this barrier.");
                Assert.That(fixture.Network.TryGetGameServerTime(out _), Is.False);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly Component manager;
            private readonly Type managerType;
            public readonly LabNetwork Network;
            public readonly LabConfig Config;

            public Fixture()
            {
                root = new GameObject("Inactive game start barrier fixture");
                root.SetActive(false);
                Config = ScriptableObject.CreateInstance<LabConfig>();
                managerType = typeof(LabNetwork).GetField("manager").FieldType;
                manager = root.AddComponent(managerType);
                Network = root.AddComponent<LabNetwork>();
                typeof(LabNetwork).GetField("manager").SetValue(Network, manager);
                Network.config = Config;
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
                SetHost(true);
                SetListening(true);
                Set(Network, "developerSolo", true);
                Get<List<ulong>>(Network, "order").Add(0);
                Get<HashSet<ulong>>(Network, "configAcknowledgements").Add(0);
                Set(Network, "configRevision", 1);
                Set(Network, "configFingerprint", "prepared-config");
                Set(Network, "gameStageIndex", 1);
                Set(Network, "gameStageId", "stage-gumiho");
                Set(Network, "gameStageYokai", LabStageYokai.Gumiho);
                var cameraObject = new GameObject("camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                var boardObject = new GameObject("board");
                boardObject.transform.SetParent(root.transform);
                var board = boardObject.AddComponent<LabOrbBoard>();
                board.Configure(Config, camera, new Rect(-3f, -3f, 6f, 6f));
                var targetObject = new GameObject("target");
                targetObject.transform.SetParent(root.transform);
                var projectileObject = new GameObject("projectiles");
                projectileObject.transform.SetParent(root.transform);
                Network.BindBattleReferences(board, targetObject.AddComponent<LabTarget>(), camera,
                    targetObject.AddComponent<BoxCollider>(), null, projectileObject.transform);
            }

            public bool Prepare() => Network.PrepareCurrentGameStage(1, "stage-gumiho", LabStageYokai.Gumiho,
                1, "prepared-config");

            public void SetHost(bool host)
            {
                object client = managerType.GetProperty("LocalClient").GetValue(manager);
                client.GetType().GetProperty("IsServer", Private).SetValue(client, host);
                client.GetType().GetProperty("IsClient", Private).SetValue(client, true);
                client.GetType().GetProperty("IsConnected", Private).SetValue(client, true);
            }

            public void SetListening(bool listening) => managerType.GetProperty("IsListening").SetValue(manager, listening);

            public void Dispose()
            {
                SetListening(false);
                // This inactive EditMode fixture does not run the Player's deferred Destroy loop.
                // Retire view objects explicitly before exercising ordinary runtime unbind cleanup.
                foreach (LabOrbView view in root.GetComponentsInChildren<LabOrbView>(true))
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
                Network.UnbindBattleReferences();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(Config);
            }
        }

        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
