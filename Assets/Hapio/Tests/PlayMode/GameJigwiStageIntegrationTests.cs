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
            // #493: the 지귀 overlay and its art load only for the 지귀 stage, already before its countdown ends.
            Assert.That(Object.FindObjectsByType<LabJigwiScreenPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(network.PauseBattle(), Is.True);
            Assert.That(session.DeveloperNextStage(), Is.True);
            yield return WaitPlaying(2);
            Assert.That(Object.FindObjectsByType<LabJigwiScreenPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(network.PauseBattle(), Is.True);
            Assert.That(session.DeveloperNextStage(), Is.True);
            // The HUD's LateUpdate fallback needs a round snapshot. Wait for whichever comes first: an overlay that
            // exists before any snapshot was built ahead by Bind/Start behind the preparing overlay.
            System.Func<int> overlays = () => Object.FindObjectsByType<LabJigwiScreenPresentation>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            yield return Until(() => session.State.stageIndex == 3 && network.HasBattleReferences
                && (overlays() > 0 || network.Snapshot != null), "Stage 3 overlay or first round snapshot");
            Assert.That(overlays(), Is.EqualTo(1), "Built behind the preparing overlay, not by the in-battle fallback.");
            Assert.That(network.Snapshot, Is.Null, "It existed before the first round snapshot.");
            yield return WaitPlaying(3);

            int spendEvents = 0; float spentCost = 0f;
            Action<float> onSpent = value => { spendEvents++; spentCost = value; };
            network.LocalStaminaSpent += onSpent;
            var staminaFeedback = Object.FindFirstObjectByType<LabStaminaFeedback>();
            Assert.That(staminaFeedback, Is.Not.Null, "Saved Battle binds the new common stamina feedback.");
            network.Generate();
            yield return Until(() => spendEvents == 1, "Approved generation produces one real stamina-spend event");
            yield return null;
            Assert.That(spentCost, Is.EqualTo(network.config.GenerateCost));
            Assert.That(staminaFeedback.ConsumptionPulseCount, Is.Zero,
                "The saved profile glows only red for a stamina-short tap, not for every paid generation (#418).");
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
            // #509: the 지귀 shout voice was removed; nothing attaches a pattern voice any more.
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
            Assert.That(overlay.PersonalHeatVisible, Is.False, "#479: A has no screen heat or edge flames.");
            Assert.That(overlay.VisibleScreenFires, Is.Zero);
            Assert.That(overlay.status.text, Does.Not.Contain("과열"), "#479 removed A's hit-count text.");
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
            // #509: a solo room picks its only seat; B is judged on the boss itself, with no fire art or colliders.
            Assert.That(b.players.Single().playerId, Is.EqualTo(network.LocalPlayerId));
            Assert.That(overlay.PersonalHeatVisible, Is.False);
            Assert.That(overlay.VisibleScreenFires, Is.Zero);
            Assert.That(overlay.GroupCurtainVisible, Is.False);
            Assert.That(overlay.SyncPillar.DropsVisible, Is.True);
            Assert.That(overlay.status.text, Does.Contain("지목 P1").And.Contain("0/3"));
            Assert.That(overlay.playerStatus.text, Does.Contain("물 구슬로 박을 맞혀요"));
            Assert.That(current.orbs.Any(orb => orb.jigwiABSupplyEventId == b.id), Is.True,
                "B supplies real owned Water, without a player generation request.");
            Assert.That(spendEvents, Is.EqualTo(1), "Free supply and new snapshots never flash as a paid generation.");
            Assert.That(staminaFeedback.ConsumptionPulseCount, Is.Zero);
            Assert.That(network.GetComponentsInChildren<LabJigwiFireSurface>(true), Is.Empty);
            Assert.That(model.JigwiABBlocksBoss(network.LocalPlayerId, current.serverTime), Is.False);
            AssertNoOldSurfacePresentation();
            Assert.That(paint.IsReady, Is.True);
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(shell), "Switching A/B cannot replace the bound WhiteGourd paint/hit shell.");
            Assert.That(target.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(originalColliderCount));
            Assert.That(clock.IsPaused, Is.True);
            CaptureScreenEvidence("3"); yield return null;
            int paintedBefore = model.PaintSurface.CoveredCount;
            for (int i = 0; i < 3; i++)
            {
                ThrowFixtureWaterAtBoss("saved-sync-" + i);
                PublishFireState(); yield return null;
                if (i < 2)
                {
                    Assert.That(overlay.SyncPillar.FilledDrops, Is.EqualTo(i + 1));
                    Assert.That(overlay.status.text, Does.Contain((i + 1) + "/3"));
                }
                HoldAt(31.4d + i * .3d); yield return null;
                CaptureScreenEvidence((2 - i).ToString()); yield return null;
            }
            Assert.That(network.Snapshot.jigwiAB.rounds.Single(round => round.id == b.id).resolution,
                Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(overlay.SyncPillar.DropsVisible, Is.False);
            Assert.That(model.PaintSurface.CoveredCount, Is.GreaterThan(paintedBefore), "The counted Water paints the boss as usual.");
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(shell));
            network.LocalStaminaSpent -= onSpent;
            HoldAt(35d); yield return null;
            CaptureScreenEvidence("0"); yield return null;
            TestContext.WriteLine("JIGWI_AB_SAVED_STAGE3: Home/DEV skips/A11.2/B31.2/no B colliders/boss sync 0-1-2-3 checked. "
                + "Screen Space Overlay GPU pixels are not sampled by camera.Render; device/GPU visual review is separate.");
        }

        private static void CaptureScreenEvidence(string remaining)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            // Nonblocking GameView capture includes the real saved cameras and ScreenSpaceOverlay.
            // It is supporting visual evidence, not a mobile/device test or the GPU assertion fixture.
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "jigwi341-curtain-" + remaining + ".png"));
        }

        /// <summary>#509: an approved in-flight Water fixture that hits the boss itself.</summary>
        private void ThrowFixtureWaterAtBoss(string id)
        {
            double now = clock.Now(Time.realtimeSinceStartupAsDouble);
            var orb = new LabOrbState { id = id, owner = network.LocalPlayerId, kind = LabOrbKind.Combined,
                element = LabOrbElement.Water, createdAt = now, x = .5f, y = .5f };
            ((Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", Private).GetValue(model)).Add(id, orb);
            Assert.That(model.TryLaunch(orb.owner, id, "launch/" + id, now, out _, out string reason), Is.True, reason);
            Assert.That(model.ConfirmHit(id, now, Vector3.back, out _, out _, out _), Is.True);
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
