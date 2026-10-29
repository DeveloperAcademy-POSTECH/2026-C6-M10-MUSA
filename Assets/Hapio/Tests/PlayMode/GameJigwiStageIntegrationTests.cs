using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Actual saved Home -> shared Battle Stage03, with the real DEV skip and held Host clock.
    /// A/B round state comes from the running model, not a synthetic snapshot. This is solo
    /// production binding coverage; it does not claim remote clients or rendered overlay pixels.
    /// </summary>
    public sealed class GameJigwiStageIntegrationTests
    {
        private GameSession session;
        private LabNetwork network;
        private LabBattleClock clock;
        private LabModel model;
        private double heldRealSample, startedAt;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator LoadSavedHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene,
                "Saved Home must instantiate its actual persistent session.");
            session = GameSession.Instance; network = session.Network;
            // This fixture enters DEV/solo from the title, independently of normal Start's opening.
            yield return null;
            Assert.That(network.GameFlowEnabled, Is.True);
            network.port = AvailablePort();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (session != null)
            {
                network.Stop(); Object.Destroy(session.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Jigwi A B saved-scene cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedHomeReachesJigwiWithScreenAThenScreenBAndPreservesTheRealPaintShell()
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
            Assert.That(before.jigwiAB.enabled, Is.True);
            Assert.That(before.jigwiStage.enabled, Is.False, "#341 must not create old surface roots or paint-blocking cones.");
            Assert.That(before.remaining, Is.InRange(116d, 120d));
            // Compare the scheduled deadline directly; reversing the addition can round away from 100.
            Assert.That(before.jigwiAB.cutoffAt, Is.EqualTo(before.jigwiAB.startedAt + 100d));
            var binding = Object.FindFirstObjectByType<GameBattleBindings>();
            Assert.That(binding, Is.Not.Null); Assert.That(binding.IsPresentationReady, Is.True);
            LabTarget target = network.target;
            Assert.That(target, Is.SameAs(binding.target));
            LabGourdPaint paint = target.GetComponent<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True);
            Assert.That(paint.visualPrefab, Is.SameAs(Resources.Load<GameObject>("GourdPaint/WhiteGourd")));
            Assert.That(target.MountedVisual, Is.Not.Null);
            Mesh shell = paint.Shell.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(shell));
            int originalColliderCount = target.GetComponentsInChildren<Collider>(true).Length;
            var hud = Object.FindFirstObjectByType<LabJigwiHud>();
            Assert.That(hud, Is.Not.Null); Assert.That(hud.network, Is.SameAs(network));
            var overlay = Object.FindFirstObjectByType<LabJigwiScreenPresentation>();
            Assert.That(overlay, Is.Not.Null, "The saved battle HUD must install the editable screen prefab.");
            Assert.That(Resources.Load<GameObject>(LabJigwiScreenPresentation.PrefabResource), Is.Not.Null);
            Assert.That(overlay.source, Is.SameAs(network));
            Assert.That(overlay.canvas.rootCanvas, Is.SameAs(hud.canvas.rootCanvas));
            Assert.That(overlay.transform.parent, Is.SameAs(hud.canvas.rootCanvas.transform));
            Assert.That(overlay.battleCamera, Is.SameAs(binding.battleCamera));
            Assert.That(overlay.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            Assert.That(overlay.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(overlay.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(overlay.GetComponentsInChildren<MeshRenderer>(true), Is.Empty);

            clock = (LabBattleClock)Field("battleClock").GetValue(network);
            model = (LabModel)Field("model").GetValue(network);
            startedAt = before.jigwiAB.startedAt;
            HoldAt(11.2d);
            yield return null;
            Assert.That(network.Snapshot.paused, Is.True);
            Assert.That(clock.Now(Time.realtimeSinceStartupAsDouble), Is.EqualTo(startedAt + 11.2d).Within(.00001d));
            LabJigwiABRound a = network.Snapshot.jigwiAB.Active(startedAt + 11.2d);
            Assert.That(a, Is.Not.Null); Assert.That(a.kind, Is.EqualTo(LabJigwiABKind.PersonalBurn));
            Assert.That(a.targetPlayer, Is.EqualTo(network.LocalPlayerId));
            Assert.That(a.requiredHits, Is.EqualTo(network.config.JigwiABPersonalRequiredHits));
            Assert.That(overlay.PersonalHeatVisible, Is.True);
            Assert.That(overlay.VisibleScreenFires, Is.Zero);
            Assert.That(overlay.status.text, Does.Contain("과열").And.Contain("0/" + a.requiredHits));
            Assert.That(overlay.personalFlames.All(image => image.texture
                == Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash")), Is.True);
            Assert.That(hud.heat.gameObject.activeInHierarchy, Is.False);
            Assert.That(hud.effectText.gameObject.activeInHierarchy, Is.False);
            Assert.That(hud.stageNotice == null || !hud.stageNotice.gameObject.activeInHierarchy, Is.True);
            Assert.That(hud.testToggle.gameObject.activeInHierarchy, Is.False);
            AssertNoOldSurfacePresentation();

            HoldAt(31.2d);
            // The same clock stays held across the forward seek; no real-time Update can roll it back.
            Method("SyncJigwiFireSurfaces").Invoke(network, null);
            yield return null;
            LabSnapshot current = network.Snapshot;
            LabJigwiABRound b = current.jigwiAB.Active(startedAt + 31.2d);
            Assert.That(b, Is.Not.Null); Assert.That(b.kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(overlay.PersonalHeatVisible, Is.False);
            Assert.That(overlay.VisibleScreenFires, Is.EqualTo(network.config.JigwiABGroupFireCount));
            Assert.That(overlay.status.text, Does.Contain("물 결합 구슬"));
            Assert.That(overlay.playerStatus.text, Does.Contain("P1 0/" + network.config.JigwiABGroupFireCount));
            var surfaces = network.GetComponentsInChildren<LabJigwiFireSurface>(true)
                .Where(surface => surface.FireId == b.id && surface.OwnerPlayerId == network.LocalPlayerId).ToArray();
            Assert.That(surfaces, Has.Length.EqualTo(network.config.JigwiABGroupFireCount + 1));
            var curtain = surfaces.Single(surface => surface.IsCurtain);
            Assert.That(curtain.Hitbox, Is.TypeOf<BoxCollider>());
            Assert.That(overlay.GroupCurtainVisible, Is.True);
            Assert.That(overlay.RemainingIgnitions, Is.EqualTo(3));
            Assert.That(overlay.CurtainStrength, Is.EqualTo(1f));
            Assert.That(network.TryGetJigwiABCurtainFrame(b, network.LocalPlayerId, current.serverTime,
                out Vector3 curtainCenter, out Quaternion curtainRotation, out Vector2 curtainHalfSize), Is.True);
            Assert.That(Vector3.Distance(curtain.transform.position, curtainCenter), Is.LessThan(.005f));
            Assert.That(Quaternion.Angle(curtain.transform.rotation, curtainRotation), Is.LessThan(.01f));
            Assert.That(surfaces.All(surface => surface.Active && surface.Hitbox.isTrigger), Is.True);
            Assert.That(surfaces.SelectMany(surface => surface.GetComponentsInChildren<Renderer>(true))
                .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy), Is.False,
                "The real B hit targets are invisible world colliders; visible fire belongs only to the Canvas.");
            var localFires = b.players.Single(player => player.playerId == network.LocalPlayerId).targets;
            for (int i = 0; i < localFires.Length; i++)
            {
                Assert.That(network.TryGetJigwiABFireWorldPosition(b, localFires[i], network.LocalPlayerId, i,
                    current.serverTime, out Vector3 world), Is.True);
                var collider = surfaces.Single(surface => surface.FireTargetId == localFires[i].id);
                Assert.That(Vector3.Distance(collider.transform.position, world), Is.LessThan(.005f));
                Assert.That(collider.Hitbox.bounds.extents.x, Is.EqualTo(b.targetRadius).Within(.005f));
                Vector3 projected = binding.battleCamera.WorldToViewportPoint(world);
                Assert.That(projected.z, Is.GreaterThan(0f));
                Assert.That(projected.x, Is.InRange(0f, 1f)); Assert.That(projected.y, Is.InRange(0f, 1f));
                Camera uiCamera = overlay.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlay.canvas.worldCamera;
                Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay.fireLayer,
                    binding.battleCamera.WorldToScreenPoint(world), uiCamera, out Vector2 expected), Is.True);
                Assert.That(Vector2.Distance(overlay.fireImages[i].rectTransform.anchoredPosition, expected), Is.LessThan(.1f));
                Assert.That(overlay.fireImages[i].raycastTarget, Is.False);
                Assert.That(overlay.fireImages[i].material.shader.name, Is.EqualTo("C6Lab/Jigwi Screen Flame"));
            }
            AssertNoOldSurfacePresentation();
            Assert.That(paint.IsReady, Is.True);
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(shell), "Switching A/B cannot replace the bound WhiteGourd paint/hit shell.");
            Assert.That(target.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(originalColliderCount));
            Assert.That(clock.IsPaused, Is.True);

            // Actual running model + saved UI: a smaller visual must never create an attack gap.
            // This fixture supplies approved in-flight Water states; real flight contact is tested separately.
            // Hold the clock without allowing the Pause panel to hide the effect in evidence captures.
            Transform pauseRoot = binding.hud.pauseMenu.transform;
            while (pauseRoot.parent != null && pauseRoot.parent != overlay.canvas.transform) pauseRoot = pauseRoot.parent;
            var pauseCapture = pauseRoot.GetComponent<CanvasGroup>();
            if (pauseCapture == null) pauseCapture = pauseRoot.gameObject.AddComponent<CanvasGroup>();
            pauseCapture.alpha = 0f;
            TestContext.WriteLine("JIGWI_CURTAIN_GEOMETRY frame=" + curtainHalfSize + " eyeRect=" + binding.battleCamera.rect
                + " mesh=" + shell.bounds + " scale=" + target.Hitbox.transform.lossyScale + " actual=" + target.Hitbox.bounds);
            CaptureScreenEvidence("3"); yield return null;
            int paintedBefore = model.PaintSurface.CoveredCount;
            float previousStrength = overlay.CurtainStrength;
            for (int i = 0; i < 2; i++)
            {
                ThrowFixtureWaterAt(b, localFires[i].id, "saved-point-" + i);
                PublishFireState(); yield return null;
                Assert.That(overlay.RemainingIgnitions, Is.EqualTo(2 - i));
                Assert.That(overlay.GroupCurtainVisible, Is.True);
                Assert.That(overlay.CurtainStrength, Is.LessThan(previousStrength));
                previousStrength = overlay.CurtainStrength;
                Assert.That(curtain.Active, Is.True);
                Assert.That(model.JigwiABBlocksBoss(network.LocalPlayerId, current.serverTime), Is.True);
                HoldAt(31.9d + i * .7d); yield return null;
                CaptureScreenEvidence((2 - i).ToString()); yield return null;
            }
            ThrowFixtureWaterAt(b, LabJigwiABFireMotion.CurtainId(b.id, network.LocalPlayerId), "saved-curtain-only");
            PublishFireState(); yield return null;
            Assert.That(overlay.GroupWaterVisible, Is.True, "A Water hit on the curtain gives feedback without clearing the last ignition.");
            Assert.That(overlay.RemainingIgnitions, Is.EqualTo(1));
            Assert.That(overlay.GroupCurtainVisible, Is.True);
            Assert.That(curtain.Active, Is.True);
            ThrowFixtureWaterAt(b, localFires[2].id, "saved-final-point");
            PublishFireState(); yield return null;
            Assert.That(overlay.RemainingIgnitions, Is.Zero);
            Assert.That(overlay.GroupCurtainVisible, Is.False);
            Assert.That(curtain == null || !curtain.Active, Is.True);
            Assert.That(model.JigwiABBlocksBoss(network.LocalPlayerId, current.serverTime), Is.False);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(paintedBefore));
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(shell));
            HoldAt(35d); yield return null;
            CaptureScreenEvidence("0"); yield return null;
            TestContext.WriteLine("JIGWI_AB_SAVED_STAGE3: Home/DEV skips/A11.2/B31.2/Canvas+collider projection/curtain3-2-1-0 checked. "
                + "Screen Space Overlay GPU pixels are not sampled by camera.Render; device/GPU visual review is separate.");
        }

        private static void CaptureScreenEvidence(string remaining)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            // Nonblocking GameView capture includes the real saved cameras and ScreenSpaceOverlay.
            // It is supporting visual evidence, not a mobile/device test or the GPU assertion fixture.
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "jigwi341-curtain-" + remaining + ".png"));
        }

        private void ThrowFixtureWaterAt(LabJigwiABRound round, string targetId, string id)
        {
            double now = clock.Now(Time.realtimeSinceStartupAsDouble);
            var orb = new LabOrbState { id = id, owner = network.LocalPlayerId, kind = LabOrbKind.Combined,
                element = LabOrbElement.Water, createdAt = now, x = .5f, y = .5f };
            ((Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", Private).GetValue(model)).Add(id, orb);
            Assert.That(model.TryLaunch(orb.owner, id, "launch/" + id, now, out _, out string reason), Is.True, reason);
            Assert.That(model.ConfirmJigwiABFireHit(id, round.id, targetId, now, out bool extinguished), Is.True);
            Assert.That(extinguished, Is.EqualTo(targetId != LabJigwiABFireMotion.CurtainId(round.id, orb.owner)));
        }
        private void PublishFireState()
        {
            Method("PublishSnapshot").Invoke(network, new object[] { true });
            Method("SyncJigwiFireSurfaces").Invoke(network, null);
        }

        private void HoldAt(double elapsed)
        {
            double previousBattleTime = clock.Now(Time.realtimeSinceStartupAsDouble);
            if (clock.IsPaused) Assert.That(clock.Resume(heldRealSample), Is.True);
            double realNow = Time.realtimeSinceStartupAsDouble;
            double desired = startedAt + elapsed;
            double advance = desired - clock.Now(realNow);
            Assert.That(advance, Is.GreaterThan(0d));
            heldRealSample = realNow + advance;
            Assert.That(clock.Pause(heldRealSample), Is.True);
            Assert.That(clock.Now(realNow), Is.EqualTo(desired).Within(.00001d));
            // Use the same paused-clock sample as PublishSnapshot. Reconstructing desired
            // separately can put it a fraction ahead and make the following snapshot go backwards.
            double heldBattleTime = clock.Now(realNow);
            Assert.That(heldBattleTime, Is.GreaterThanOrEqualTo(previousBattleTime));
            model.Tick(heldBattleTime);
            Method("PublishSnapshot").Invoke(network, new object[] { true });
            Method("SyncJigwiStagePresentation").Invoke(network, null);
        }
        private static void AssertNoOldSurfacePresentation()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var legacy in root.GetComponentsInChildren<LabJigwiStagePresentation>(true))
                { Assert.That(legacy.VisibleGroups, Is.Zero); Assert.That(legacy.VisibleRoots, Is.Zero); Assert.That(legacy.VisibleFirePatches, Is.Zero); }
            // A persistent network component may live outside the active Battle scene.
            foreach (var legacy in Object.FindObjectsByType<LabJigwiStagePresentation>(FindObjectsSortMode.None))
                Assert.That(legacy.VisibleGroups, Is.Zero);
        }
        private IEnumerator WaitPlaying(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Saved stage " + stage + " must finish its shared countdown.");
        private static FieldInfo Field(string name) => typeof(LabNetwork).GetField(name, Private);
        private static MethodInfo Method(string name) => typeof(LabNetwork).GetMethod(name, Private);
        private static IEnumerator Until(Func<bool> condition, string reason)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20d;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, reason);
        }
        private static ushort AvailablePort()
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
            throw new InvalidOperationException("No dual-family test port was available.");
        }
    }
}
