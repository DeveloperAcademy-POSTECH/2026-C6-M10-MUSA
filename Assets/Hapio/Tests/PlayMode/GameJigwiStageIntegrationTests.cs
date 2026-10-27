using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// Saved Home/session/Battle integration. The explicit clock fixture seeks and holds the
    /// ignition samples so the normal Host publish/calibration path can be inspected without
    /// forging a fire snapshot or allowing a later real-time update to move the model backwards.
    /// A bounded two-player model geometry fixture also samples curtains on the same saved shell.
    /// It restores the network model and camera before yielding, and does not claim multi-device transport/cooperation verification.
    /// </summary>
    public sealed class GameJigwiStageIntegrationTests
    {
        private GameSession session;
        private LabNetwork network;

        [UnitySetUp]
        public IEnumerator LoadSavedHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene,
                "The saved Home must bootstrap its actual persistent session prefab.");
            session = GameSession.Instance;
            network = session.Network;
            Assert.That(network.GameFlowEnabled, Is.True);
            network.port = AvailableDualFamilyPort();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (session != null)
            {
                network.Stop();
                Object.Destroy(session.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Jigwi saved-scene fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedGameReachesJigwiAndBindsItsRealPaintShellToHostRootsAndHud()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            Assert.That(network.PauseBattle(), Is.True);
            Assert.That(session.DeveloperNextStage(), Is.True);
            yield return WaitPlaying(2);
            Assert.That(network.PauseBattle(), Is.True);
            Assert.That(session.DeveloperNextStage(), Is.True);
            yield return WaitPlaying(3);

            LabSnapshot before = network.Snapshot;
            Assert.That(before.jigwiStage.enabled, Is.True, "Stage03 must opt into the new policy through the real run configuration.");
            Assert.That(before.remaining, Is.InRange(116d, 120d));
            Assert.That(before.jigwiStage.cutoffAt - before.jigwiStage.startedAt, Is.EqualTo(100d));
            var binding = Object.FindFirstObjectByType<GameBattleBindings>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.IsPresentationReady, Is.True);
            Assert.That(binding.target, Is.SameAs(network.target));
            LabTarget target = network.target;
            LabGourdPaint paint = target.GetComponent<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True);
            Assert.That(paint.visualPrefab, Is.SameAs(Resources.Load<GameObject>("GourdPaint/WhiteGourd")));
            Assert.That(target.MountedVisual, Is.Not.Null);
            MeshFilter shell = paint.Shell.GetComponent<MeshFilter>();
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(shell.sharedMesh),
                "The authoritative hitbox must be the saved WhiteGourd paint shell, not a standalone cylinder fixture.");
            int colliderCount = target.GetComponentsInChildren<Collider>(true).Length;

            // Use the clock's public pause API with an explicit future real-time sample, then
            // keep it held. Every subsequent network Update now reads this same battle time.
            var clock = (LabBattleClock)Field("battleClock").GetValue(network);
            var model = (LabModel)Field("model").GetValue(network);
            double realNow = Time.realtimeSinceStartupAsDouble;
            double desired = before.jigwiStage.startedAt + 11.2d;
            double seek = desired - clock.Now(realNow);
            Assert.That(seek, Is.GreaterThan(0d));
            double heldRealSample = realNow + seek;
            Assert.That(clock.Pause(heldRealSample), Is.True);
            Assert.That(clock.Now(realNow), Is.EqualTo(desired).Within(.00001d));
            model.Tick(desired);
            Method("PublishSnapshot").Invoke(network, new object[] { true });
            Method("SyncJigwiStagePresentation").Invoke(network, null);
            yield return null;

            LabSnapshot current = network.Snapshot;
            Assert.That(current.paused, Is.True, "The fixture must remain held to avoid a reverse-time update after seeking.");
            LabJigwiStageGroup ignition = current.jigwiStage.groups.Single(group => group.slot == 0);
            Assert.That(ignition.pattern, Is.EqualTo(LabJigwiStagePattern.Ignition));
            Assert.That(ignition.geometryConfigured, Is.True, "The Host must calibrate the actual saved shell before presentation.");
            Assert.That(ignition.helperPlayer, Is.EqualTo(network.LocalPlayerId));
            Assert.That(ignition.affectedPlayer, Is.EqualTo(network.LocalPlayerId));
            Assert.That(LabJigwiStagePresentation.TrySurfacePoint(target, ignition.rootDirection,
                out Vector3 point, out _), Is.True);
            Vector3 mapped = target.Hitbox.transform.InverseTransformPoint(point).normalized;
            Assert.That(Vector3.Angle(mapped, ignition.rootDirection), Is.LessThan(.1f));
            Assert.That(ignition.ContainsRoot(network.LocalPlayerId, mapped, current.serverTime), Is.True);
            Vector3 screen = binding.battleCamera.WorldToViewportPoint(point);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Assert.That(screen.x, Is.InRange(0f, 1f)); Assert.That(screen.y, Is.InRange(0f, 1f));
            var marks = network.GetComponent<LabJigwiStagePresentation>();
            Assert.That(marks, Is.Not.Null);
            Assert.That(marks.VisibleRoots, Is.EqualTo(1));
            Assert.That(marks.VisibleFirePatches, Is.EqualTo(1));
            Assert.That(target.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliderCount));
            LabJigwiHud hud = Object.FindFirstObjectByType<LabJigwiHud>();
            Assert.That(hud, Is.Not.Null); Assert.That(hud.network, Is.SameAs(network));
            Assert.That(hud.stageNotice, Is.Not.Null);
            Assert.That(hud.stageNotice.gameObject.activeInHierarchy, Is.True);
            Assert.That(hud.stageNotice.text, Does.Contain("큰 불씨"));
            Assert.That(hud.stageNotice.raycastTarget, Is.False);

            Vector2 initialOffset = ScreenOffset(binding.battleCamera, target, point);
            // Resume at the same synthetic real-time sample, then immediately pause at the
            // next sample. No Update can observe the temporary unpaused/backward clock.
            Assert.That(clock.Resume(heldRealSample), Is.True);
            realNow = Time.realtimeSinceStartupAsDouble;
            desired = before.jigwiStage.startedAt + 61.2d;
            heldRealSample = realNow + desired - clock.Now(realNow);
            Assert.That(clock.Pause(heldRealSample), Is.True);
            Assert.That(clock.Now(realNow), Is.EqualTo(desired).Within(.00001d));
            model.Tick(desired);
            Method("PublishSnapshot").Invoke(network, new object[] { true });
            Method("SyncJigwiStagePresentation").Invoke(network, null);
            yield return null;

            current = network.Snapshot;
            LabJigwiStageGroup ignitionVariant = current.jigwiStage.groups.Single(group => group.slot == 4);
            Assert.That(ignitionVariant.geometryConfigured, Is.True);
            Assert.That(LabJigwiStagePresentation.TrySurfacePoint(target, ignitionVariant.rootDirection,
                out Vector3 variantPoint, out _), Is.True);
            Vector2 variantOffset = ScreenOffset(binding.battleCamera, target, variantPoint);
            Assert.That(variantOffset.x, Is.GreaterThan(initialOffset.x + .01f),
                "The actual Host-calibrated second ignition must move right, rather than overwriting its variant with the old center aim.");
            Assert.That(variantOffset.y, Is.GreaterThan(initialOffset.y + .005f),
                "The second ignition must keep its upward screen variation after actual shell calibration.");
            Assert.That(marks.VisibleRoots, Is.EqualTo(1));

            // Solo deliberately has no B. Exercise B's real saved-shell calibration with two
            // model members, without pretending that a remote device or socket joined the run.
            // This entire substitution is synchronous and always restored before yielding.
            LabModel originalModel = (LabModel)Field("model").GetValue(network);
            Vector3 cameraPosition = binding.battleCamera.transform.position;
            Quaternion cameraRotation = binding.battleCamera.transform.rotation;
            try
            {
                var geometryModel = new LabModel();
                geometryModel.Begin("jigwi-saved-shell-curtain-geometry", new[] { 0ul, 20ul }, 0d,
                    network.config, LabStageYokai.Jigwi);
                Field("model").SetValue(network, geometryModel);
                Vector2 curtainFirst = CalibratedCurtainOffset(geometryModel, binding.battleCamera, target, 31.2d, 1);
                Vector2 curtainVariant = CalibratedCurtainOffset(geometryModel, binding.battleCamera, target, 71.2d, 5);
                Assert.That(curtainVariant.x, Is.LessThan(curtainFirst.x - .01f),
                    "B's position variation must survive Host calibration as a leftward helper-view change.");
                Assert.That(curtainVariant.y, Is.LessThan(curtainFirst.y - .005f),
                    "B's later root must also retain its downward helper-view change.");
            }
            finally
            {
                Field("model").SetValue(network, originalModel);
                binding.battleCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }
            Assert.That(Field("model").GetValue(network), Is.SameAs(model));
            Assert.That(clock.IsPaused, Is.True);
            Assert.That(clock.Now(Time.realtimeSinceStartupAsDouble), Is.EqualTo(desired).Within(.00001d));
        }

        private Vector2 CalibratedCurtainOffset(LabModel geometryModel, Camera camera, LabTarget target,
            double at, int slot)
        {
            geometryModel.Tick(at);
            LabSnapshot sample = geometryModel.Snapshot(at);
            Method("CalibrateJigwiStageGeometry").Invoke(network, new object[] { sample, at });
            sample = geometryModel.Snapshot(at);
            LabJigwiStageGroup curtain = sample.jigwiStage.groups.Single(group => group.slot == slot);
            Assert.That(curtain.pattern, Is.EqualTo(LabJigwiStagePattern.Curtain));
            Assert.That(curtain.geometryConfigured, Is.True);
            Assert.That(curtain.helperPlayer, Is.Not.EqualTo(curtain.affectedPlayer));
            LabPlayerState helper = sample.players.Single(player => player.id == curtain.helperPlayer);
            camera.transform.position = LabThrowMath.SeatPosition(helper.seat, sample.players.Length,
                target.transform.position, network.config.CameraRadius, network.config.CameraHeightOffset, true);
            camera.transform.LookAt(target.transform.position + Vector3.up * network.config.CameraLookHeightOffset);
            Assert.That(LabJigwiStagePresentation.TrySurfacePoint(target, curtain.rootDirection,
                out Vector3 point, out _), Is.True);
            Vector3 viewport = camera.WorldToViewportPoint(point);
            Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0f, 1f)); Assert.That(viewport.y, Is.InRange(0f, 1f));
            Assert.That(curtain.ContainsRoot(helper.id,
                target.Hitbox.transform.InverseTransformPoint(point).normalized, at), Is.True);
            string guide = LabJigwiHud.DescribeStage(sample.jigwiStage, sample, helper.id, at);
            LabPlayerState victim = sample.players.Single(player => player.id == curtain.affectedPlayer);
            Assert.That(guide, Does.Contain("P" + (victim.seat + 1) + "의 불장막"));
            return ScreenOffset(camera, target, point);
        }

        private static Vector2 ScreenOffset(Camera camera, LabTarget target, Vector3 point)
        {
            Vector3 source = camera.WorldToViewportPoint(point);
            Vector3 shell = camera.WorldToViewportPoint(target.Hitbox.bounds.center);
            return new Vector2(source.x - shell.x, source.y - shell.y);
        }

        private IEnumerator WaitPlaying(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Saved stage " + stage + " must start through the shared countdown.");
        private static FieldInfo Field(string name) => typeof(LabNetwork).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo Method(string name) => typeof(LabNetwork).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static IEnumerator Until(Func<bool> condition, string reason)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, reason);
        }
        private static ushort AvailableDualFamilyPort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            { ipv6.DualMode = false; ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port)); }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("A dual-family test port was not available.");
        }
    }
}
