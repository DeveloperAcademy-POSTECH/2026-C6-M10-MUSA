using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// Saved overlay asset + production projection/view contracts. Synthetic accepted snapshots are
    /// intentional here: these tests do not claim a real room, transport, finger aim or device run.
    /// </summary>
    public sealed class LabJigwiABPresentationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const ulong Local = 41, Other = 0;
        private GameObject root;
        private LabConfig config;
        private LabNetwork network;
        private Canvas canvas;
        private Camera camera;
        private LabTarget target;
        private LabJigwiScreenPresentation view;
        private LabSnapshot snapshot;

        [SetUp]
        public void Prepare()
        {
            root = new GameObject("Jigwi A B saved overlay presentation fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var host = new GameObject("Passive network replica"); host.transform.SetParent(root.transform, false);
            network = host.AddComponent<LabNetwork>(); network.enabled = false; network.config = config;
            target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 2301f, 0f), .65f, 1.4f);
            network.target = target;
            var screen = new GameObject("Existing Canvas", typeof(RectTransform), typeof(Canvas));
            screen.transform.SetParent(root.transform, false);
            canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var eye = new GameObject("Owner battle camera"); eye.transform.SetParent(root.transform, false);
            camera = eye.AddComponent<Camera>(); camera.enabled = false;
            camera.aspect = 390f / 844f; camera.fieldOfView = 52f;
            camera.transform.position = LabThrowMath.SeatPosition(1, 2, target.transform.position,
                config.CameraRadius, config.CameraHeightOffset, true);
            camera.transform.LookAt(target.transform.position + Vector3.up * config.CameraLookHeightOffset);
            GameObject prefab = Resources.Load<GameObject>(LabJigwiScreenPresentation.PrefabResource);
            Assert.That(prefab, Is.Not.Null, "Run the dedicated prefab builder; a runtime-only fallback is not this saved-asset check.");
            view = Object.Instantiate(prefab, canvas.transform, false).GetComponent<LabJigwiScreenPresentation>();
            Assert.That(view, Is.Not.Null);
            view.Bind(network, canvas, camera);
            // Explicit clock samples below cannot race a live Network or LateUpdate.
            view.enabled = false;
            snapshot = Snapshot(Personal());
            SetSnapshot(snapshot);
            Physics.SyncTransforms();
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (network != null) SetSnapshot(null);
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [Test]
        public void SavedPrefabHasEditableReferencesNewArtAndNoInputOrWorldPhysics()
        {
            var prefab = Resources.Load<GameObject>(LabJigwiScreenPresentation.PrefabResource);
            var saved = prefab.GetComponent<LabJigwiScreenPresentation>();
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.personalHeat, Is.Not.Null); Assert.That(saved.personalWaterDouse, Is.Not.Null);
            Assert.That(saved.fireLayer, Is.Not.Null); Assert.That(saved.statusPanel, Is.Not.Null);
            Assert.That(saved.fireClip, Is.Not.Null);
            Assert.That(saved.fireClip.transform, Is.SameAs(saved.fireLayer));
            Assert.That(saved.groupFireCurtain, Is.Not.Null);
            Assert.That(saved.curtainBody, Is.Not.Null);
            Assert.That(saved.curtainBody.GraphicShape, Is.EqualTo(LabJigwiFlameGraphic.Shape.CurtainArtMask));
            Assert.That(saved.curtainArtMask, Is.Not.Null);
            Assert.That(saved.curtainArtMask.showMaskGraphic, Is.False, "The old procedural red plate must never draw.");
            Assert.That(saved.curtainArt, Is.Not.Null); Assert.That(saved.curtainCore, Is.Not.Null);
            Assert.That(saved.groupFireCurtain.parent, Is.SameAs(saved.fireLayer));
            Assert.That(saved.groupFireCurtain.GetSiblingIndex(), Is.Zero);
            Assert.That(saved.curtainFlames, Has.Length.EqualTo(1));
            Assert.That(saved.curtainFlames[0], Is.SameAs(saved.curtainArt));
            Assert.That(saved.groupFireCurtain.GetComponentsInChildren<UnityEngine.UI.RawImage>(true), Has.Length.EqualTo(2),
                "One coherent artwork and one dense crop replace the repeated vertical flame stamps.");
            Assert.That(saved.groupFireCurtain.GetComponentsInChildren<Transform>(true)
                .Any(child => child.name.StartsWith("OuterFlame", StringComparison.Ordinal)), Is.False);
            Assert.That(saved.ignitionMarkers, Has.Length.EqualTo(6));
            Assert.That(saved.ignitionMarkers.All(marker => marker != null
                && marker.GraphicShape == LabJigwiFlameGraphic.Shape.IgnitionMarker && !marker.raycastTarget), Is.True);
            Assert.That(saved.curtainWaterDouse, Is.Not.Null);
            Assert.That(saved.waterFlightTrail, Is.Not.Null);
            Assert.That(saved.waterFlightTrail.raycastTarget, Is.False);
            Assert.That(saved.waterFlightTrail.transform.parent, Is.SameAs(saved.fireLayer));
            Assert.That(saved.waterFlightTrail.transform.GetSiblingIndex(), Is.EqualTo(saved.fireLayer.childCount - 1),
                "The water path must render in front of the B curtain and ignition points, below the separate instruction panel.");
            Assert.That(saved.waterFlightTrail.GetComponentsInParent<UnityEngine.UI.RectMask2D>(true), Does.Contain(saved.fireClip));
            Assert.That(saved.audioView, Is.Not.Null);
            Assert.That(saved.audioView.transform.parent, Is.SameAs(saved.transform));
            var soundSources = prefab.GetComponentsInChildren<AudioSource>(true);
            Assert.That(soundSources, Has.Length.EqualTo(1), "B has one local loop, not a source per ignition or snapshot.");
            Assert.That(soundSources[0].gameObject, Is.SameAs(saved.audioView.gameObject));
            Assert.That(soundSources[0].playOnAwake, Is.False); Assert.That(soundSources[0].loop, Is.True);
            Assert.That(soundSources[0].spatialBlend, Is.Zero); Assert.That(soundSources[0].dopplerLevel, Is.Zero);
            Assert.That(soundSources[0].clip, Is.SameAs(Resources.Load<AudioClip>("Jigwi/GroupFireLoop")));
            Assert.That(saved.audioView.clip, Is.Not.Null);
            Assert.That(saved.groupFireCurtain.gameObject.activeSelf, Is.False);
            Assert.That(saved.status, Is.Not.Null); Assert.That(saved.playerStatus, Is.Not.Null);
            Assert.That(saved.personalFlames, Has.Length.EqualTo(2));
            Assert.That(saved.fireImages, Has.Length.EqualTo(6));
            Assert.That(saved.fireWaterDouses, Has.Length.EqualTo(6));
            Assert.That(saved.font, Is.Not.Null); Assert.That(saved.font.HasCharacter('불'), Is.True);
            Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Is.Empty, "Retain the authored battle Canvas.");
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.UI.Selectable>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            Assert.That(prefab.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(prefab.GetComponent<CanvasGroup>().interactable, Is.False);
            Texture2D fire = Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash");
            Texture2D water = Resources.Load<Texture2D>("Jigwi/WaterDouseWash");
            Assert.That(fire, Is.Not.Null); Assert.That(water, Is.Not.Null);
            foreach (var image in saved.personalFlames.Concat(saved.fireImages))
            { Assert.That(image, Is.Not.Null); Assert.That(image.texture, Is.SameAs(fire)); }
            Texture2D curtain = Resources.Load<Texture2D>("Jigwi/ScreenFireCurtainWash");
            Texture2D stage2 = Resources.Load<Texture2D>("Jigwi/ScreenFireCurtainStage2");
            Texture2D stage1 = Resources.Load<Texture2D>("Jigwi/ScreenFireCurtainStage1");
            Assert.That(curtain, Is.Not.Null); Assert.That(stage2, Is.Not.Null); Assert.That(stage1, Is.Not.Null);
            Assert.That(saved.curtainTexture, Is.SameAs(curtain));
            Assert.That(saved.curtainStage2Texture, Is.SameAs(stage2)); Assert.That(saved.curtainStage1Texture, Is.SameAs(stage1));
            Assert.That(stage2, Is.Not.SameAs(curtain)); Assert.That(stage1, Is.Not.SameAs(curtain).And.Not.SameAs(stage2));
            Assert.That(saved.curtainArt.texture, Is.SameAs(curtain)); Assert.That(saved.curtainCore.texture, Is.SameAs(curtain));
            Assert.That(curtain, Is.Not.SameAs(fire), "A keeps its original orb flame; B uses the new coherent wall.");
            foreach (var image in saved.fireWaterDouses.Append(saved.personalWaterDouse).Append(saved.curtainWaterDouse))
            { Assert.That(image, Is.Not.Null); Assert.That(image.texture, Is.SameAs(water)); }
            Assert.That(saved.personalHeat.gameObject.activeSelf, Is.False);
            Assert.That(saved.fireImages.All(image => !image.gameObject.activeSelf), Is.True);
            Shader shader = Resources.Load<Shader>("Jigwi/ScreenFlame");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("C6Lab/Jigwi Screen Flame"));
        }

        [Test]
        public void PersonalHeatUsesPSeatAndApprovedProgressAndDousesOnlyOnceAfterCompletion()
        {
            LabJigwiABRound personal = snapshot.jigwiAB.rounds[0];
            personal.hitCount = 1;
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.PersonalHeatVisible, Is.True);
            Assert.That(view.status.text, Does.Contain("P2 과열").And.Contain("1/3"));
            Assert.That(view.playerStatus.text, Does.Contain("회복 4배"));
            Assert.That(view.VisibleScreenFires, Is.Zero);
            Assert.That(view.GetComponentsInChildren<MeshRenderer>(true), Is.Empty,
                "A's heat belongs to the screen, not a mark attached to the painted boss.");
            view.ApplySnapshot(snapshot, Other, 11.3d);
            Assert.That(view.PersonalHeatVisible, Is.False, "The same accepted A must not heat the other player's screen.");
            view.ApplySnapshot(snapshot, Local, 11.4d);
            personal.hitCount = 3; personal.resolution = LabJigwiABResolution.Completed; personal.resolvedAt = 11.5d;
            view.ApplySnapshot(snapshot, Local, 11.5d);
            Assert.That(view.PersonalHeatVisible, Is.False);
            Assert.That(view.PersonalWaterVisible, Is.True);
            Assert.That(view.personalWaterDouse.texture.name, Is.EqualTo("WaterDouseWash"));
            view.ApplySnapshot(snapshot, Local, 12.2d);
            Assert.That(view.PersonalWaterVisible, Is.False);
            view.ApplySnapshot(snapshot, Local, 12.3d);
            Assert.That(view.PersonalWaterVisible, Is.False, "A repeated snapshot cannot replay a finished splash.");
            var fresh = Object.Instantiate(Resources.Load<GameObject>(LabJigwiScreenPresentation.PrefabResource), canvas.transform)
                .GetComponent<LabJigwiScreenPresentation>();
            fresh.Bind(network, canvas, camera); fresh.enabled = false;
            fresh.ApplySnapshot(snapshot, Local, 11.6d);
            Assert.That(fresh.PersonalWaterVisible, Is.False, "A late restore begins from the accepted state, not old feedback.");
        }

        [Test]
        public void NewScreenPolicyClearsOldSurfaceRootsWithoutAddingAnyCollider()
        {
            var legacy = network.gameObject.AddComponent<LabJigwiStagePresentation>();
            var old = new LabJigwiStageSnapshot { enabled = true, startedAt = 0d, cutoffAt = 100d,
                groups = new[] { new LabJigwiStageGroup { id = "old-root", slot = 0,
                    pattern = LabJigwiStagePattern.Ignition, affectedPlayer = Local, helperPlayer = Local,
                    warningAt = 10d, activeAt = 11d, expiresAt = 18d, geometryConfigured = true,
                    rootDirection = Vector3.forward, rootAngleDegrees = 18f, fireAngleDegrees = 25f,
                    spreadSeconds = 1f, patchDirections = new[] { Vector3.forward } } } };
            legacy.Apply(old, target, snapshot, Local, 11.2d, true);
            Assert.That(legacy.VisibleRoots, Is.EqualTo(1), "Prove this fixture had old world art before the compatibility clear.");
            int colliders = root.GetComponentsInChildren<Collider>(true).Length;
            typeof(LabNetwork).GetMethod("SyncJigwiStagePresentation", Private).Invoke(network, null);
            Assert.That(legacy.VisibleGroups, Is.Zero); Assert.That(legacy.VisibleRoots, Is.Zero);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.PersonalHeatVisible, Is.True);
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders));
            Assert.That(legacy.VisibleGroups, Is.Zero, "Showing A must never recreate a boss surface source.");
        }

        [UnityTest]
        public IEnumerator GroupFireProjectsTheActualWorldRadiusAndMovesOnlyWithTheSharedBattleClock()
        {
            snapshot = Snapshot(Group()); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.VisibleScreenFires, Is.EqualTo(3));
            LabJigwiABRound group = snapshot.jigwiAB.rounds[0];
            var fire = group.players.Single(player => player.playerId == Local).targets[0];
            Assert.That(network.TryGetJigwiABFireWorldPosition(group, fire, Local, 0, 11.2d, out Vector3 world), Is.True);
            Camera ui = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(view.fireLayer, camera.WorldToScreenPoint(world), ui,
                out Vector2 expected), Is.True);
            Assert.That(Vector2.Distance(view.fireImages[0].rectTransform.anchoredPosition, expected), Is.LessThan(.05f));
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(view.fireLayer,
                camera.WorldToScreenPoint(world + camera.transform.right * group.targetRadius), ui, out Vector2 rim), Is.True);
            Assert.That(view.fireImages[0].rectTransform.sizeDelta.x,
                Is.EqualTo(Vector2.Distance(expected, rim) * 2f).Within(.05f));
            Assert.That(view.fireImages[0].texture.name, Is.EqualTo("SurfaceFlameWash"));
            Assert.That(view.fireImages[0].material.shader.name, Is.EqualTo("C6Lab/Jigwi Screen Flame"));
            target.transform.Rotate(0f, 137f, 0f); Physics.SyncTransforms();
            Assert.That(network.TryGetJigwiABFireWorldPosition(group, fire, Local, 0, 11.2d, out Vector3 still), Is.True);
            Assert.That(Vector3.Distance(world, still), Is.LessThan(.001f), "Screen flames must not rotate with the painted gourd.");
            Vector2 beforeMove = view.fireImages[0].rectTransform.anchoredPosition;
            view.ApplySnapshot(snapshot, Local, 11.8d);
            Assert.That(Vector2.Distance(beforeMove, view.fireImages[0].rectTransform.anchoredPosition), Is.GreaterThan(.1f));
            snapshot.serverTime = 11.8d; snapshot.paused = true; SetSnapshot(snapshot);
            double heldAt = network.JigwiABPresentationTime;
            view.ApplySnapshot(snapshot, Local, heldAt, true);
            Vector2 heldPosition = view.fireImages[0].rectTransform.anchoredPosition;
            float heldShaderTime = view.fireImages[0].material.GetFloat("_FlameTime");
            yield return new WaitForSecondsRealtime(.045f);
            Assert.That(network.JigwiABPresentationTime, Is.EqualTo(heldAt).Within(.0001d));
            view.ApplySnapshot(snapshot, Local, network.JigwiABPresentationTime, true);
            Assert.That(view.fireImages[0].rectTransform.anchoredPosition, Is.EqualTo(heldPosition));
            Assert.That(view.fireImages[0].material.GetFloat("_FlameTime"), Is.EqualTo(heldShaderTime));
            var resumed = Snapshot(group); resumed.serverTime = heldAt; SetSnapshot(resumed);
            double resumedAt = network.JigwiABPresentationTime;
            yield return new WaitForSecondsRealtime(.045f);
            Assert.That(network.JigwiABPresentationTime, Is.GreaterThan(resumedAt + .02d));
            view.ApplySnapshot(resumed, Local, network.JigwiABPresentationTime);
            Assert.That(view.fireImages[0].material.GetFloat("_FlameTime"), Is.GreaterThan(heldShaderTime + .02f));
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void TimedMissionKeepsItsCountdownBesideNextWarningAndFlashesFailureOnlyOnce()
        {
            var group = Group(); group.endsAt = 21d;
            var next = Personal(); next.id = "next-personal"; next.warningAt = 20d; next.activeAt = 21d; next.endsAt = 36d;
            snapshot = Snapshot(group); snapshot.jigwiAB.rounds = new[] { next, group }; SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11d);
            Assert.That(view.status.text, Does.Contain("10.0초"));
            view.ApplySnapshot(snapshot, Local, 20.5d);
            Assert.That(view.status.text, Does.Contain("0.5초").And.Contain("다음 기믹 예고"));
            Assert.That(view.GroupCurtainVisible, Is.True, "The upcoming A warning must not hide B before its deadline.");
            group.resolution = LabJigwiABResolution.Expired; group.resolvedAt = 21d;
            view.ApplySnapshot(snapshot, Local, 21d);
            Assert.That(view.FailureVisible, Is.True); Assert.That(view.FailureFlashCount, Is.EqualTo(1));
            Assert.That(view.failureStatus.text, Is.EqualTo("실패"));
            Assert.That(view.failureOverlay.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(view.failureOverlay.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(view.failureOverlay.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                .All(graphic => !graphic.raycastTarget), Is.True, "Failure feedback cannot consume input.");
            view.ApplySnapshot(JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(snapshot)), Local, 21.3d);
            Assert.That(view.FailureFlashCount, Is.EqualTo(1), "Repeated accepted state cannot restart the failure flash.");
            view.ApplySnapshot(snapshot, Local, 22.1d);
            Assert.That(view.FailureVisible, Is.False);
            var late = Object.Instantiate(Resources.Load<GameObject>(LabJigwiScreenPresentation.PrefabResource), canvas.transform, false)
                .GetComponent<LabJigwiScreenPresentation>();
            late.Bind(network, canvas, camera); late.enabled = false;
            late.ApplySnapshot(snapshot, Local, 21.1d);
            Assert.That(late.FailureVisible, Is.False, "A late baseline installs state without replaying old failure feedback.");
            Assert.That(late.FailureFlashCount, Is.Zero);
        }

        [TestCase(LabJigwiABResolution.Cancelled)]
        [TestCase(LabJigwiABResolution.Skipped)]
        public void CancelledOrSkippedMissionDoesNotBecomeFailureFeedback(LabJigwiABResolution resolution)
        {
            var group = Group(); snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            group.resolution = resolution; group.resolvedAt = 12d;
            view.ApplySnapshot(snapshot, Local, 12d);
            Assert.That(view.FailureVisible, Is.False); Assert.That(view.FailureFlashCount, Is.Zero);
        }

        [Test]
        public void SuccessRewardShowsItsAcceptedFiveSecondClockAndScalesWithoutChangingTheCanvas()
        {
            var group = Group(); snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            group.resolution = LabJigwiABResolution.Completed; group.resolvedAt = 12d;
            group.rewardUntil = 17d; group.rewardSizeScale = 1.3f; group.rewardPaintScale = 1.5f;
            view.ApplySnapshot(snapshot, Local, 12d);
            Assert.That(view.status.text, Does.Contain("모든 불을 함께 껐어요").And.Contain("5.0초"));
            Assert.That(view.playerStatus.text, Does.Contain("1.3배").And.Contain("1.5배").And.Contain("불꽃 궤적"));
            view.ApplySnapshot(snapshot, Local, 16.5d);
            Assert.That(view.status.text, Does.Contain("0.5초"));
            view.ApplySnapshot(snapshot, Local, 17d);
            Assert.That(view.status.text, Does.Not.Contain("보상"));
            Assert.That(view.FailureVisible, Is.False);
            Assert.That(view.GetComponentsInChildren<Canvas>(true), Is.Empty, "Reuse the authored Canvas hierarchy.");
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void GroupOffRelightAndSharedCompletionFollowAcceptedStateWithoutReplayingSplashes()
        {
            LabJigwiABRound group = Group(); snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            var mine = group.players.Single(player => player.playerId == Local);
            var first = mine.targets[0];
            first.lit = false; first.extinguishedAt = 11.4d; first.relightAt = 23.4d;
            view.ApplySnapshot(snapshot, Local, 11.4d);
            Assert.That(view.VisibleScreenFires, Is.EqualTo(2));
            Assert.That(view.fireImages[0].gameObject.activeSelf, Is.False);
            Assert.That(view.fireWaterDouses[0].gameObject.activeSelf, Is.True);
            Assert.That(view.playerStatus.text, Does.Contain("P2 1/3").And.Contain("P1 0/3"));
            view.ApplySnapshot(snapshot, Local, 12.2d);
            Assert.That(view.fireWaterDouses[0].gameObject.activeSelf, Is.False);
            first.lit = true;
            view.ApplySnapshot(snapshot, Local, 23.4d);
            Assert.That(view.VisibleScreenFires, Is.EqualTo(3));
            Assert.That(view.fireWaterDouses[0].gameObject.activeSelf, Is.False);
            first.lit = false; first.extinguishedAt = 23.6d; first.relightAt = 35.6d;
            view.ApplySnapshot(snapshot, Local, 23.6d);
            Assert.That(view.fireWaterDouses[0].gameObject.activeSelf, Is.True, "A new accepted extinguishing generation gets one new splash.");
            foreach (var player in group.players) foreach (var flame in player.targets)
                if (flame.lit) { flame.lit = false; flame.extinguishedAt = 23.8d; flame.relightAt = 35.8d; }
            group.resolution = LabJigwiABResolution.Completed; group.resolvedAt = 23.8d;
            view.ApplySnapshot(snapshot, Local, 23.8d);
            Assert.That(view.VisibleScreenFires, Is.Zero);
            Assert.That(view.status.text, Does.Contain("모든 불을 함께 껐어요"));
            view.ApplySnapshot(snapshot, Local, 24.5d);
            Assert.That(view.fireWaterDouses.All(image => !image.gameObject.activeSelf), Is.True);
            snapshot.phase = LabPhase.Victory; view.ApplySnapshot(snapshot, Local, 24.5d);
            Assert.That(view.statusPanel.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator AOrbFireHasItsOwnHeldCountdownAndClearsWithoutChangingBombFireOrTheBody()
        {
            var orb = new GameObject("Independent A flame orb"); orb.transform.SetParent(root.transform, false);
            var body = orb.AddComponent<LabOrbView>(); body.Configure("a-flame", LabOrbKind.Combined, .32f, LabOrbElement.Wood);
            body.Body.useGravity = false;
            float radius = body.HitSphere.radius;
            int colliders = orb.GetComponentsInChildren<Collider>(true).Length;
            body.SetBurn(7f);
            body.SetJigwiABBurn(5f, true);
            Assert.That(body.Burning, Is.True);
            Assert.That(body.JigwiABFlame.Burning, Is.True);
            Assert.That(body.JigwiABFlame.Held, Is.True);
            Assert.That(body.JigwiABFlame.SecondsLeft, Is.EqualTo(5f));
            Material material = body.JigwiABFlame.FlameRenderer.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("C6Lab/Jigwi Ink Flame"));
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash")));
            yield return new WaitForSecondsRealtime(.045f);
            Assert.That(body.JigwiABFlame.SecondsLeft, Is.EqualTo(5f).Within(.0001f));
            Assert.That(body.JigwiABFlame.AnimationTime, Is.Zero);
            body.SetJigwiABBurn(4.8f, false);
            yield return new WaitForSecondsRealtime(.045f);
            Assert.That(body.JigwiABFlame.SecondsLeft, Is.LessThan(4.8f));
            Assert.That(body.JigwiABFlame.AnimationTime, Is.GreaterThan(0f));
            body.SetJigwiABBurn(0f);
            Assert.That(body.JigwiABFlame.Burning, Is.False);
            Assert.That(body.Burning, Is.True, "A completes without extinguishing the independent old bomb-blast fire.");
            body.SetJigwiABBurn(5f); body.Configure("reused-body", LabOrbKind.Combined, radius, LabOrbElement.Fire);
            Assert.That(body.JigwiABFlame.Burning, Is.False, "Reusing a body cannot connect to the previous A flame.");
            Assert.That(orb.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders));
            Assert.That(body.HitSphere.radius, Is.EqualTo(radius));
            Assert.That(orb.activeSelf, Is.True, "The presentation itself cannot expire or destroy the actual orb.");
        }

        [Test]
        public void SavedRawImagesRenderWarmAAndBAndBlueWaterIntoAnIsolatedPortraitUiTarget()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No GPU: Jigwi screen effect pixels were not rendered.");
            // Screen Space Overlay does not enter camera.Render(). Only this fixture's existing
            // Canvas is temporarily switched to Camera mode, using the actual saved RawImages,
            // texture and UI shader. No production Canvas/Scene or live GameView is changed.
            const int width = 390, height = 844, layer = 31;
            Shader shader = Resources.Load<Shader>("Jigwi/ScreenFlame");
            Assert.That(shader, Is.Not.Null); Assert.That(shader.isSupported, Is.True);
            var uiObject = new GameObject("Temporary Jigwi GPU UI camera");
            var uiCamera = uiObject.AddComponent<Camera>(); uiCamera.enabled = false;
            var targetTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { antiAliasing = 1, useDynamicScale = false };
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var sheet = new Texture2D(width * 6, height, TextureFormat.RGBA32, false);
            RenderTexture oldActive = RenderTexture.active, oldBattleTarget = camera.targetTexture;
            Rect oldBattleRect = camera.rect;
            float oldBattleAspect = camera.aspect, oldPlane = canvas.planeDistance;
            RenderMode oldMode = canvas.renderMode; Camera oldUiCamera = canvas.worldCamera;
            Transform[] uiTransforms = canvas.GetComponentsInChildren<Transform>(true);
            int[] oldLayers = uiTransforms.Select(item => item.gameObject.layer).ToArray();
            try
            {
                Assert.That(targetTexture.Create(), Is.True);
                uiCamera.transform.position = new Vector3(0f, 0f, -20f);
                uiCamera.transform.rotation = Quaternion.identity;
                uiCamera.orthographic = true; uiCamera.orthographicSize = height * .5f;
                uiCamera.nearClipPlane = .01f; uiCamera.farClipPlane = 100f;
                uiCamera.clearFlags = CameraClearFlags.SolidColor; uiCamera.backgroundColor = Color.clear;
                uiCamera.cullingMask = 1 << layer; uiCamera.allowHDR = false; uiCamera.allowMSAA = false;
                uiCamera.targetTexture = targetTexture; uiCamera.rect = new Rect(0f, 0f, 1f, 1f);
                uiCamera.aspect = width / (float)height;
                foreach (Transform item in uiTransforms) item.gameObject.layer = layer;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = 10f;
                // Projection must use the same pixel rectangle as the UI capture, independently
                // of the person's current Editor GameView dimensions or selected aspect.
                camera.targetTexture = targetTexture; camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.aspect = width / (float)height;
                view.Hide();
                Color32[] baseline = RenderUi(uiCamera, targetTexture, readback, width, height);

                var personal = Personal(); snapshot = Snapshot(personal); SetSnapshot(snapshot);
                view.ApplySnapshot(snapshot, Local, 11.2d);
                view.statusPanel.gameObject.SetActive(false); // Sample art, not the warm instruction background/text.
                Color32[] a = RenderUi(uiCamera, targetTexture, readback, width, height);
                int warmA = WarmPixels(a, baseline), changedA = ChangedPixels(a, baseline);
                Assert.That(warmA, Is.GreaterThan(64), "A must draw coloured, nontransparent ink flames rather than only create UI objects.");
                Assert.That(changedA, Is.GreaterThan(128));
                sheet.SetPixels32(0, 0, width, height, a);

                // Match the saved Battle viewport. B must leave the board's lower 46% untouched.
                camera.rect = new Rect(0f, .46f, 1f, .54f);
                snapshot = Snapshot(Group()); SetSnapshot(snapshot);
                view.ApplySnapshot(snapshot, Local, 11.3d);
                view.statusPanel.gameObject.SetActive(false);
                Assert.That(view.VisibleScreenFires, Is.EqualTo(3));
                Color32[] b = RenderUi(uiCamera, targetTexture, readback, width, height);
                int warmB = WarmPixels(b, baseline), changedB = ChangedPixels(b, baseline);
                Assert.That(warmB, Is.GreaterThan(64), "B's actual projected saved RawImages and UI shader must reach the portrait render target.");
                Assert.That(changedB, Is.GreaterThan(128));
                int boardPixels = 0;
                for (int y = 0; y < Mathf.FloorToInt(height * .46f); y++)
                    for (int x = 0; x < width; x++) if (b[y * width + x].a > baseline[y * width + x].a + 8) boardPixels++;
                Assert.That(boardPixels, Is.Zero, "B's shader/body/markers must be clipped above the combination board.");
                sheet.SetPixels32(width, 0, width, height, b);

                // Isolate the three dedicated artworks from the markers, fixed core and water.
                var local = snapshot.jigwiAB.rounds[0].players.Single(player => player.playerId == Local);
                Color32[] previousStep = b;
                Color32[] previousArtwork = RenderCurtainArtworkOnly(uiCamera, targetTexture, readback, width, height);
                for (int remaining = 2; remaining >= 0; remaining--)
                {
                    int index = 2 - remaining;
                    local.targets[index].lit = false;
                    local.targets[index].extinguishedAt = 11.3d;
                    local.targets[index].relightAt = 23.3d;
                    view.ApplySnapshot(snapshot, Local, 11.31d + index * .01d);
                    view.statusPanel.gameObject.SetActive(false);
                    foreach (var splash in view.fireWaterDouses) splash.gameObject.SetActive(false);
                    Assert.That(view.RemainingIgnitions, Is.EqualTo(remaining));
                    Assert.That(view.GroupCurtainVisible, Is.EqualTo(remaining > 0));
                    if (remaining > 0)
                    {
                        Assert.That(view.curtainArt.texture, Is.SameAs(remaining == 2 ? view.curtainStage2Texture : view.curtainStage1Texture));
                        Color32[] artwork = RenderCurtainArtworkOnly(uiCamera, targetTexture, readback, width, height);
                        Assert.That(ChangedPixels(artwork, previousArtwork), Is.GreaterThan(64),
                            "Each dedicated curtain image must produce different pixels with the same clock, rectangle and opacity; removed markers cannot satisfy this check.");
                        previousArtwork = artwork;
                    }
                    Color32[] step = RenderUi(uiCamera, targetTexture, readback, width, height);
                    int warm = WarmPixels(step, baseline);
                    Assert.That(ChangedPixels(step, previousStep), Is.GreaterThan(64),
                        "Each artwork step must remain visibly different after the fixed occluding core is composed.");
                    if (remaining == 0) Assert.That(warm, Is.Zero);
                    else Assert.That(warm, Is.GreaterThan(64), "The final ignition must still keep a visible blocking curtain.");
                    previousStep = step;
                    sheet.SetPixels32(width * (4 - remaining), 0, width, height, step);
                }

                personal.hitCount = personal.requiredHits;
                personal.resolution = LabJigwiABResolution.Completed; personal.resolvedAt = 11.4d;
                snapshot = Snapshot(personal); SetSnapshot(snapshot);
                view.ApplySnapshot(snapshot, Local, 11.4d);
                view.statusPanel.gameObject.SetActive(false);
                Assert.That(view.PersonalWaterVisible, Is.True);
                Color32[] water = RenderUi(uiCamera, targetTexture, readback, width, height);
                int blue = 0, alpha = 0;
                for (int i = 0; i < water.Length; i++)
                {
                    Color32 pixel = water[i];
                    if (!Different(pixel, baseline[i])) continue;
                    if (pixel.a > 32) alpha++;
                    if (pixel.a > 32 && pixel.b > 48 && pixel.b > pixel.r * 1.1f && pixel.b > pixel.g * .95f) blue++;
                }
                Assert.That(alpha, Is.GreaterThan(128), "The transparent water art must produce visible alpha-bearing pixels.");
                Assert.That(blue, Is.GreaterThan(32), "The completion splash must render the new blue water asset.");
                sheet.SetPixels32(width * 5, 0, width, height, water); sheet.Apply(false, false);
                string evidence = Path.Combine(Path.GetTempPath(), "jigwi341-screen-overlay-gpu.png");
                File.WriteAllBytes(evidence, sheet.EncodeToPNG());
                TestContext.WriteLine("JIGWI_AB_UI_GPU A(warm/changed)=" + warmA + "/" + changedA
                    + " B=" + warmB + "/" + changedB + " Water(blue/alpha)=" + blue + "/" + alpha
                    + " evidence=" + evidence + " (fixture camera-mode capture; no device visual claim)");
            }
            finally
            {
                view.Hide();
                canvas.renderMode = oldMode; canvas.worldCamera = oldUiCamera; canvas.planeDistance = oldPlane;
                camera.targetTexture = oldBattleTarget; camera.rect = oldBattleRect; camera.aspect = oldBattleAspect;
                for (int i = 0; i < uiTransforms.Length; i++) if (uiTransforms[i] != null) uiTransforms[i].gameObject.layer = oldLayers[i];
                uiCamera.targetTexture = null;
                RenderTexture.active = oldActive;
                targetTexture.Release();
                Object.DestroyImmediate(targetTexture); Object.DestroyImmediate(readback);
                Object.DestroyImmediate(sheet); Object.DestroyImmediate(uiObject);
            }
        }

        private Color32[] RenderCurtainArtworkOnly(Camera uiCamera, RenderTexture targetTexture, Texture2D readback, int width, int height)
        {
            bool coreWasActive = view.curtainBody.gameObject.activeSelf;
            bool[] fireWasActive = view.fireImages.Select(image => image.gameObject.activeSelf).ToArray();
            Material material = view.curtainArt.material;
            float flameTime = material.GetFloat("_FlameTime");
            try
            {
                view.curtainBody.gameObject.SetActive(false);
                foreach (var fire in view.fireImages) fire.gameObject.SetActive(false);
                // Freeze the shader clock so animation cannot masquerade as an artwork change.
                material.SetFloat("_FlameTime", 1.3f);
                return RenderUi(uiCamera, targetTexture, readback, width, height);
            }
            finally
            {
                material.SetFloat("_FlameTime", flameTime);
                view.curtainBody.gameObject.SetActive(coreWasActive);
                for (int i = 0; i < view.fireImages.Length; i++) view.fireImages[i].gameObject.SetActive(fireWasActive[i]);
            }
        }

        private static Color32[] RenderUi(Camera uiCamera, RenderTexture target, Texture2D readback, int width, int height)
        {
            Canvas.ForceUpdateCanvases();
            uiCamera.Render();
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0); readback.Apply(false, false);
            return readback.GetPixels32();
        }
        private static bool Different(Color32 a, Color32 b) =>
            Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 30;
        private static int ChangedPixels(Color32[] image, Color32[] baseline)
        {
            int count = 0;
            for (int i = 0; i < image.Length; i++) if (Different(image[i], baseline[i])) count++;
            return count;
        }
        private static int WarmPixels(Color32[] image, Color32[] baseline)
        {
            int count = 0;
            for (int i = 0; i < image.Length; i++)
            {
                Color32 pixel = image[i];
                if (Different(pixel, baseline[i]) && pixel.a > 32 && pixel.r > 48
                    && pixel.r > pixel.g * 1.2f && pixel.r > pixel.b * 1.4f) count++;
            }
            return count;
        }

        [Test]
        public void CurtainSwitchesThreeDedicatedArtworksInOneFrameAndKeepsItsOpaqueCoreUntilEveryLocalIgnitionIsOff()
        {
            snapshot = Snapshot(Group()); SetSnapshot(snapshot);
            LabJigwiABRound group = snapshot.jigwiAB.rounds[0];
            var mine = group.players.Single(player => player.playerId == Local);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.CurtainVisible, Is.True); Assert.That(view.CurtainVisualLevel, Is.EqualTo(3));
            Vector2 size = view.groupFireCurtain.sizeDelta, centre = view.groupFireCurtain.anchoredPosition;
            Assert.That(size.x, Is.GreaterThan(0)); Assert.That(size.y, Is.GreaterThan(0));
            Assert.That(network.TryGetJigwiABCurtainFrame(group, Local, 11.2d,
                out Vector3 world, out Quaternion rotation, out Vector2 half), Is.True);
            Vector2 min = Vector2.one * float.PositiveInfinity, max = -min;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = world + rotation * new Vector3((i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y, 0f);
                Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(view.fireLayer,
                    camera.WorldToScreenPoint(corner), null, out Vector2 point), Is.True);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            Assert.That(Vector2.Distance(centre, (min + max) * .5f), Is.LessThan(.1f));
            Assert.That(Vector2.Distance(size, max - min), Is.LessThan(.1f), "The dense body follows the entire shared blocking plane.");
            float opacity3 = view.curtainArt.color.a;
            Vector2 artworkSize = view.curtainArt.rectTransform.sizeDelta;
            Vector2 artworkPosition = view.curtainArt.rectTransform.anchoredPosition;
            Assert.That(view.curtainArt.texture, Is.SameAs(view.curtainTexture));
            Vector2 coreMin = ((RectTransform)view.curtainBody.transform).anchorMin;
            Vector2 coreMax = ((RectTransform)view.curtainBody.transform).anchorMax;
            for (int i = 0; i < 2; i++)
            {
                mine.targets[i].lit = false; mine.targets[i].extinguishedAt = 11.3d + .1d * i;
                mine.targets[i].relightAt = mine.targets[i].extinguishedAt + 12d;
                view.ApplySnapshot(snapshot, Local, 11.3d + .1d * i);
                Assert.That(view.CurtainVisible, Is.True, "Even the last ignition keeps the whole gourd blocked.");
                Assert.That(view.CurtainVisualLevel, Is.EqualTo(2 - i));
                Assert.That(view.groupFireCurtain.sizeDelta, Is.EqualTo(size));
                Assert.That(view.groupFireCurtain.anchoredPosition, Is.EqualTo(centre));
                Assert.That(view.curtainBody.gameObject.activeInHierarchy, Is.True);
                Assert.That(view.curtainBody.color.a, Is.GreaterThan(.9f));
                Assert.That(view.curtainArt.texture, Is.SameAs(i == 0 ? view.curtainStage2Texture : view.curtainStage1Texture));
                Assert.That(view.curtainArt.color.a, Is.EqualTo(opacity3));
                Assert.That(view.curtainArt.rectTransform.sizeDelta, Is.EqualTo(artworkSize));
                Assert.That(view.curtainArt.rectTransform.anchoredPosition, Is.EqualTo(artworkPosition));
                Assert.That(view.curtainArtMask.showMaskGraphic, Is.False);
                Assert.That(((RectTransform)view.curtainBody.transform).anchorMin, Is.EqualTo(coreMin));
                Assert.That(((RectTransform)view.curtainBody.transform).anchorMax, Is.EqualTo(coreMax));
                Assert.That(view.curtainCore.color.a, Is.GreaterThan(.95f));
                Assert.That(view.curtainCore.texture, Is.SameAs(view.curtainTexture), "The final point still keeps the original dense central artwork.");
                AssertArtworkAspect(view.curtainArt); AssertArtworkAspect(view.curtainCore);
                Assert.That(view.playerStatus.text, Does.Contain("발화점을 모두 꺼야 박 공격 가능"));
            }
            mine.targets[2].lit = false; mine.targets[2].extinguishedAt = 11.5d; mine.targets[2].relightAt = 23.5d;
            view.ApplySnapshot(snapshot, Local, 11.5d);
            Assert.That(view.CurtainVisible, Is.False); Assert.That(view.CurtainVisualLevel, Is.Zero);
            Assert.That(view.VisibleScreenFires, Is.Zero);
            Assert.That(group.players.Single(player => player.playerId == Other).targets.All(fire => fire.lit), Is.True);
            Assert.That(view.status.text, Does.Not.Contain("모든 불을 함께 껐어요"));
            mine.targets[0].lit = true;
            view.ApplySnapshot(snapshot, Local, 23.31d);
            Assert.That(view.CurtainVisible, Is.True); Assert.That(view.CurtainVisualLevel, Is.EqualTo(1));
            Assert.That(view.curtainArt.texture, Is.SameAs(view.curtainStage1Texture));
            Assert.That(view.groupFireCurtain.sizeDelta, Is.EqualTo(size));
            Assert.That(view.groupFireCurtain.anchoredPosition, Is.EqualTo(centre));
            mine.targets[1].lit = true; view.ApplySnapshot(snapshot, Local, 23.32d);
            Assert.That(view.curtainArt.texture, Is.SameAs(view.curtainStage2Texture));
            mine.targets[2].lit = true; view.ApplySnapshot(snapshot, Local, 23.33d);
            Assert.That(view.curtainArt.texture, Is.SameAs(view.curtainTexture));
            Assert.That(view.PersonalHeatVisible, Is.False, "B may not enable or alter the independent A heat.");
        }

        [Test]
        public void BMaskUsesTheBattleViewportAndCachedHpFrameBottomWithoutMovingTheLayerOrA()
        {
            snapshot = Snapshot(Group()); SetSnapshot(snapshot);
            camera.rect = new Rect(0f, .46f, 1f, .54f);
            Canvas.ForceUpdateCanvases();
            Vector2 originalA = view.personalHeat.rectTransform.sizeDelta;
            Vector2 anchorsMin = view.fireLayer.anchorMin, anchorsMax = view.fireLayer.anchorMax;
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.fireClip, Is.Not.Null);
            Assert.That(view.fireClip.enabled, Is.True);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(view.fireLayer, camera.pixelRect.min,
                null, out Vector2 cameraBottom), Is.True);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(view.fireLayer, camera.pixelRect.max,
                null, out Vector2 cameraTop), Is.True);
            Assert.That(view.BattleFireClipRect.yMin, Is.EqualTo(cameraBottom.y).Within(.1f));
            Assert.That(view.BattleFireClipRect.yMax, Is.EqualTo(cameraTop.y).Within(.1f),
                "A fixture without a HUD clips to the battle camera, without inventing a screen percentage.");

            var hpObject = new GameObject("FigmaYokaiHpBar", typeof(RectTransform));
            hpObject.transform.SetParent(canvas.transform, false);
            var hp = hpObject.GetComponent<RectTransform>();
            hp.anchorMin = new Vector2(.1f, .84f); hp.anchorMax = new Vector2(.9f, .89f);
            hp.offsetMin = hp.offsetMax = Vector2.zero;
            var fillObject = new GameObject("DynamicFill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fillObject.transform.SetParent(hp, false);
            var fill = fillObject.GetComponent<UnityEngine.UI.Image>(); fill.raycastTarget = false;
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            titleObject.transform.SetParent(hp, false);
            var label = titleObject.GetComponent<UnityEngine.UI.Text>(); label.font = view.font; label.text = "요괴 HP"; label.raycastTarget = false;
            var titleRect = (RectTransform)titleObject.transform;
            titleRect.anchorMin = Vector2.zero; titleRect.anchorMax = Vector2.one; titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
            var trackObject = new GameObject("TrackMask", typeof(RectTransform));
            trackObject.transform.SetParent(hp, false);
            var track = trackObject.GetComponent<RectTransform>();
            track.anchorMin = Vector2.zero; track.anchorMax = Vector2.right; track.pivot = new Vector2(.5f, 1f);
            track.sizeDelta = new Vector2(0f, 12f); track.anchoredPosition = new Vector2(0f, -6f);
            var hudObject = new GameObject("Passive cached HUD"); hudObject.transform.SetParent(canvas.transform, false);
            var hud = hudObject.AddComponent<LabHud>(); hud.enabled = false; hud.yokaiHpFill = fill;
            Canvas.ForceUpdateCanvases();
            view.Bind(network, canvas, camera); // Cache the authored frame and its overflowing track once.
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Vector3[] corners = new Vector3[4]; track.GetWorldCorners(corners);
            float trackBottom = float.PositiveInfinity;
            foreach (Vector3 corner in corners)
            {
                Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(view.fireLayer,
                    RectTransformUtility.WorldToScreenPoint(null, corner), null, out Vector2 point), Is.True);
                trackBottom = Mathf.Min(trackBottom, point.y);
            }
            Assert.That(view.BattleFireClipRect.yMin, Is.EqualTo(cameraBottom.y).Within(.1f));
            Assert.That(view.BattleFireClipRect.yMax, Is.LessThanOrEqualTo(trackBottom - 3.9f));
            Assert.That(view.fireClip.padding.y, Is.GreaterThan(0f));
            Assert.That(view.fireClip.padding.w, Is.GreaterThan(0f));
            Assert.That(view.fireLayer.anchorMin, Is.EqualTo(anchorsMin)); Assert.That(view.fireLayer.anchorMax, Is.EqualTo(anchorsMax));
            Assert.That(view.fireLayer.offsetMin, Is.EqualTo(Vector2.zero)); Assert.That(view.fireLayer.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(view.personalHeat.rectTransform.sizeDelta, Is.EqualTo(originalA));
            Assert.That(view.personalHeat.GetComponentsInParent<UnityEngine.UI.RectMask2D>(true), Is.Empty);
            Assert.That(view.personalWaterDouse.GetComponentsInParent<UnityEngine.UI.RectMask2D>(true), Is.Empty);
            foreach (var flame in view.curtainFlames)
            {
                Vector2 offset = flame.rectTransform.anchoredPosition, size = flame.rectTransform.sizeDelta;
                Assert.That(Mathf.Abs(offset.y) + size.y * .5f, Is.LessThanOrEqualTo(view.groupFireCurtain.sizeDelta.y * .5f + .01f));
                Assert.That(Mathf.Abs(offset.x) + size.x * .5f, Is.LessThanOrEqualTo(view.groupFireCurtain.sizeDelta.x * .5f + .01f));
            }
        }

        [Test]
        public void MissingLowerStageArtworkKeepsTheStrongCurtainInsteadOfADefaultWhiteImage()
        {
            snapshot = Snapshot(Group()); SetSnapshot(snapshot);
            view.curtainStage2Texture = null; view.curtainStage1Texture = null;
            var mine = snapshot.jigwiAB.rounds[0].players.Single(player => player.playerId == Local);
            for (int remaining = 2; remaining >= 1; remaining--)
            {
                mine.targets[2 - remaining].lit = false;
                view.ApplySnapshot(snapshot, Local, 11.2d);
                Assert.That(view.CurtainVisible, Is.True);
                Assert.That(view.CurtainVisualLevel, Is.EqualTo(remaining));
                Assert.That(view.curtainArt.texture, Is.SameAs(view.curtainTexture));
                Assert.That(view.curtainCore.texture, Is.SameAs(view.curtainTexture));
                Canvas.ForceUpdateCanvases(); AssertArtworkAspect(view.curtainArt);
            }
        }

        [Test]
        public void CoherentCurtainArtworkPreservesItsTexelAspectAndBInstructionsAvoidActualIgnitionMarkers()
        {
            Vector2 originalMin = view.statusPanel.rectTransform.anchorMin, originalMax = view.statusPanel.rectTransform.anchorMax;
            Vector2 originalPivot = view.statusPanel.rectTransform.pivot, originalPosition = view.statusPanel.rectTransform.anchoredPosition;
            Vector2 originalSize = view.statusPanel.rectTransform.sizeDelta;
            var model = new LabModel(); model.Begin("curtain-art-layout", new[] { Other, Local }, 0d, config, LabStageYokai.Jigwi);
            snapshot = model.Snapshot(31.2d); SetSnapshot(snapshot);
            camera.rect = new Rect(0f, .46f, 1f, .54f);
            Canvas.ForceUpdateCanvases(); view.ApplySnapshot(snapshot, Local, snapshot.serverTime); Canvas.ForceUpdateCanvases();
            Assert.That(view.GroupCurtainVisible, Is.True);
            Assert.That(view.curtainArt.texture, Is.SameAs(Resources.Load<Texture2D>("Jigwi/ScreenFireCurtainWash")));
            AssertArtworkAspect(view.curtainArt); AssertArtworkAspect(view.curtainCore);
            Assert.That(view.curtainArtMask.showMaskGraphic, Is.False);
            var group = snapshot.jigwiAB.Active(snapshot.serverTime);
            var mine = group.players.Single(player => player.playerId == Local);
            var panelCorners = new Vector3[4]; view.statusPanel.rectTransform.GetWorldCorners(panelCorners);
            Vector2 min = Vector2.one * float.PositiveInfinity, max = -min;
            foreach (Vector3 point in panelCorners)
            { Vector2 local = view.fireLayer.InverseTransformPoint(point); min = Vector2.Min(min, local); max = Vector2.Max(max, local); }
            Rect statusRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            Assert.That(statusRect.yMin, Is.GreaterThanOrEqualTo(view.BattleFireClipRect.yMin - .1f));
            Assert.That(statusRect.yMax, Is.LessThanOrEqualTo(view.BattleFireClipRect.yMax + .1f));
            for (int i = 0; i < mine.targets.Length; i++)
            {
                Assert.That(network.TryGetJigwiABFireWorldPosition(group, mine.targets[i], Local, i, snapshot.serverTime, out Vector3 world), Is.True);
                Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(view.fireLayer,
                    camera.WorldToScreenPoint(world), null, out Vector2 centre), Is.True);
                Assert.That(statusRect.Contains(centre), Is.False, "B instructions cannot cover an actual projected ignition centre.");
            }
            var personalSnapshot = Snapshot(Personal()); SetSnapshot(personalSnapshot);
            view.ApplySnapshot(personalSnapshot, Local, 11.3d);
            Assert.That(view.statusPanel.rectTransform.anchorMin, Is.EqualTo(originalMin));
            Assert.That(view.statusPanel.rectTransform.anchorMax, Is.EqualTo(originalMax));
            Assert.That(view.statusPanel.rectTransform.pivot, Is.EqualTo(originalPivot));
            Assert.That(view.statusPanel.rectTransform.anchoredPosition, Is.EqualTo(originalPosition));
            Assert.That(view.statusPanel.rectTransform.sizeDelta, Is.EqualTo(originalSize));
            Assert.That(view.personalFlames.All(image => image.texture == Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash")), Is.True);
        }

        private static void AssertArtworkAspect(UnityEngine.UI.RawImage image)
        {
            Assert.That(image.texture, Is.Not.Null);
            Rect uv = image.uvRect;
            float sampledAspect = image.texture.width * uv.width / (image.texture.height * uv.height);
            float renderedAspect = image.rectTransform.rect.width / image.rectTransform.rect.height;
            Assert.That(sampledAspect, Is.EqualTo(renderedAspect).Within(.0001f),
                "Cover cropping must preserve the generated artwork's texel proportions instead of stretching it.");
        }

        [Test]
        public void WaterOnTheCurtainShowsOnlyASplashWithoutChangingIgnitionsAndCannotReplayOnLateRestore()
        {
            snapshot = Snapshot(Group()); SetSnapshot(snapshot);
            LabJigwiABRound group = snapshot.jigwiAB.rounds[0];
            var mine = group.players.Single(player => player.playerId == Local);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.CurtainWaterVisible, Is.False);
            mine.lastWaterSplashAt = 11.3d;
            view.ApplySnapshot(snapshot, Local, 11.35d);
            Assert.That(view.CurtainWaterVisible, Is.True);
            Assert.That(mine.targets.All(fire => fire.lit), Is.True);
            Assert.That(view.CurtainVisible, Is.True); Assert.That(view.CurtainVisualLevel, Is.EqualTo(3));
            Assert.That(view.VisibleScreenFires, Is.EqualTo(3));
            Assert.That(view.fireWaterDouses.All(image => !image.gameObject.activeSelf), Is.True,
                "A curtain splash without a changed ignition must not invent per-point extinguishing feedback.");
            Vector2 splashPosition = view.curtainWaterDouse.rectTransform.anchoredPosition;
            Vector2 splashSize = view.curtainWaterDouse.rectTransform.sizeDelta;
            Color splashColor = view.curtainWaterDouse.color;
            view.ApplySnapshot(snapshot, Local, 11.35d, true);
            Assert.That(view.curtainWaterDouse.rectTransform.anchoredPosition, Is.EqualTo(splashPosition));
            Assert.That(view.curtainWaterDouse.rectTransform.sizeDelta, Is.EqualTo(splashSize));
            Assert.That(view.curtainWaterDouse.color, Is.EqualTo(splashColor));
            view.ApplySnapshot(snapshot, Local, 12d);
            Assert.That(view.CurtainWaterVisible, Is.False);
            view.ApplySnapshot(snapshot, Local, 12.2d);
            Assert.That(view.CurtainWaterVisible, Is.False);
            GameObject lateObject = Object.Instantiate(Resources.Load<GameObject>(LabJigwiScreenPresentation.PrefabResource),
                canvas.transform, false);
            var late = lateObject.GetComponent<LabJigwiScreenPresentation>(); late.enabled = false;
            late.Bind(network, canvas, camera); late.ApplySnapshot(snapshot, Local, 12.2d);
            Assert.That(late.CurtainWaterVisible, Is.False, "An old accepted impact cannot replay after binding.");
            Object.DestroyImmediate(lateObject);
            mine.lastWaterSplashAt = 12.3d;
            view.ApplySnapshot(snapshot, Local, 12.31d);
            Assert.That(view.CurtainWaterVisible, Is.True);
            Assert.That(view.CurtainVisible, Is.True); Assert.That(view.CurtainVisualLevel, Is.EqualTo(3));
            Assert.That(view.PersonalWaterVisible, Is.False, "A's completion effect is a separate source.");
        }

        private void SetSnapshot(LabSnapshot value) => typeof(LabNetwork).GetField("latest", Private).SetValue(network, value);
        private static LabSnapshot Snapshot(LabJigwiABRound round) => new LabSnapshot
        {
            sessionId = "jigwi-ab-presentation", round = 1u, phase = LabPhase.Playing, serverTime = 11.2d,
            jigwiStage = new LabJigwiStageSnapshot(),
            jigwiAB = new LabJigwiABSnapshot { enabled = true, startedAt = 0d, cutoffAt = 100d, rounds = new[] { round } },
            players = new[] { new LabPlayerState { id = Other, seat = 0 }, new LabPlayerState { id = Local, seat = 1 } }
        };
        private static LabJigwiABRound Personal() => new LabJigwiABRound
        {
            id = "ab-personal", slot = 0, kind = LabJigwiABKind.PersonalBurn,
            targetPlayer = Local, warningAt = 10d, activeAt = 11d, endsAt = 21d,
            requiredHits = 3, shrinkScale = .65f, moveAmount = .03f, movePeriodSeconds = 4f, targetRadius = .3f
        };
        private static LabJigwiABRound Group() => new LabJigwiABRound
        {
            id = "ab-group", slot = 1, kind = LabJigwiABKind.GroupFire, warningAt = 10d, activeAt = 11d,
            endsAt = 100d, shrinkAt = 23d, shrinkScale = .65f, moveAmount = .03f, movePeriodSeconds = 4f, targetRadius = .3f,
            players = new[] { Fires(Other), Fires(Local) }
        };
        private static LabJigwiABPlayerFire Fires(ulong owner) => new LabJigwiABPlayerFire
        {
            playerId = owner,
            targets = new[] { new LabJigwiABFireTarget { id = "ab-group/" + owner + "/0", x = .25f, y = .59f },
                new LabJigwiABFireTarget { id = "ab-group/" + owner + "/1", x = .5f, y = .72f },
                new LabJigwiABFireTarget { id = "ab-group/" + owner + "/2", x = .75f, y = .59f } }
        };
    }
}
