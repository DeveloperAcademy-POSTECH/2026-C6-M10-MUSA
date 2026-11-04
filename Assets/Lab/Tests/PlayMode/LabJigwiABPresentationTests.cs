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

        [TestCase(5d)]
        [TestCase(40d)]
        [TestCase(70d)]
        public void PersonalWarningKeepsInstructionsButHeatStartsOnlyAtTheAcceptedMissionBoundary(double activeAt)
        {
            var personal = Personal();
            personal.warningAt = activeAt - 1d; personal.activeAt = activeAt; personal.endsAt = activeAt + 15d;
            snapshot = Snapshot(personal); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, personal.warningAt);
            Assert.That(view.PersonalHeatVisible, Is.False, "The voice warning must not apply active heat early.");
            Assert.That(view.status.text, Does.Contain("으아아 마음이 불타오른다"));
            Assert.That(view.playerStatus.text, Does.Not.Contain("결합 구슬로 박을 맞혀"), "#479 removed A's boss-hit instruction.");
            view.ApplySnapshot(snapshot, Local, activeAt - .001d);
            Assert.That(view.PersonalHeatVisible, Is.False);
            Assert.That(view.VisibleScreenFires, Is.Zero);
            view.ApplySnapshot(snapshot, Local, activeAt);
            Assert.That(view.PersonalHeatVisible, Is.False, "#479: A never tints the screen or shows edge flames.");
            Assert.That(view.status.text, Does.Not.Contain("으아아 마음이 불타오른다"), "The warning text still ends at activeAt.");
            view.ApplySnapshot(snapshot, Other, activeAt + .01d);
            Assert.That(view.PersonalHeatVisible, Is.False);
            view.ApplySnapshot(snapshot, Local, personal.endsAt);
            Assert.That(view.PersonalHeatVisible, Is.False, "The active window remains half open.");
        }

        [TestCase(25d)]
        [TestCase(55d)]
        [TestCase(85d)]
        public void GroupWarningNamesThePickedAndTheDropsStartAtTheAcceptedBoundaryWithoutAnyFireArt(double activeAt)
        {
            var group = Group();
            group.warningAt = activeAt - 1d; group.activeAt = activeAt; group.endsAt = activeAt + 15d;
            snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.audioView.Source.mute = true;
            view.ApplySnapshot(snapshot, Local, group.warningAt);
            Assert.That(view.status.text, Does.Contain("다 함께 물을 끼얹어라"));
            Assert.That(view.status.text, Does.Contain("지목 P1·P2"));
            Assert.That(view.playerStatus.text, Does.Contain("물 구슬로 박을 맞혀요"));
            Assert.That(view.SyncPillar.DropsVisible, Is.False, "The drops start with the mission, not the warning.");
            view.ApplySnapshot(snapshot, Local, activeAt);
            Assert.That(view.SyncPillar.DropsVisible, Is.True);
            Assert.That(view.GroupCurtainVisible, Is.False, "#509: no curtain or pillar.");
            Assert.That(view.VisibleScreenFires, Is.Zero);
            Assert.That(view.RemainingIgnitions, Is.EqualTo(3));
            Assert.That(view.audioView.WantsLoop, Is.False, "No fire on screen, no fire loop.");
            Assert.That(view.status.text, Does.Not.Contain("다 함께 물을 끼얹어라"));
            view.ApplySnapshot(snapshot, Local, group.endsAt);
            Assert.That(view.SyncPillar.DropsVisible, Is.False);
        }

        [Test]
        public void PersonalAShowsNoScreenHeatHitCountOrRecoveryText()
        {
            LabJigwiABRound personal = snapshot.jigwiAB.rounds[0];
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.PersonalHeatVisible, Is.False, "#479: no screen heat or edge flames.");
            Assert.That(view.status.text, Does.Not.Contain("과열").And.Not.Contain("명중"));
            Assert.That(view.playerStatus.text, Does.Not.Contain("회복").And.Not.Contain("동료"));
            Assert.That(view.VisibleScreenFires, Is.Zero);
            Assert.That(view.GetComponentsInChildren<MeshRenderer>(true), Is.Empty,
                "A's heat belongs to the screen, not a mark attached to the painted boss.");
            view.ApplySnapshot(snapshot, Other, 11.3d);
            Assert.That(view.PersonalHeatVisible, Is.False);
            view.ApplySnapshot(snapshot, Local, personal.endsAt);
            Assert.That(view.PersonalHeatVisible, Is.False);
            Assert.That(view.PersonalWaterVisible, Is.False, "Expiry has no completion splash.");
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
            Assert.That(view.PersonalHeatVisible, Is.False);
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders));
            Assert.That(legacy.VisibleGroups, Is.Zero, "Showing A must never recreate a boss surface source.");
        }

        [Test]
        public void AHelperSeesNoPillarButTheSharedDropsAndAPassingHint()
        {
            var group = Group(); group.players = new[] { Fires(Other) };
            snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            Assert.That(view.GroupCurtainVisible, Is.False);
            Assert.That(view.VisibleScreenFires, Is.Zero);
            Assert.That(view.SyncPillar.DropsVisible, Is.True);
            Assert.That(view.playerStatus.text, Does.Contain("물 구슬을 P1에게 굴려 주세요"));
            Assert.That(view.audioView.WantsLoop, Is.False, "Only a picked screen plays the fire loop.");
        }

        [Test]
        public void ASmallClockStepBackKeepsTheFailureFlashAndALargeOneStartsAgain()
        {
            var group = Group(); group.endsAt = 21d;
            snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 20.5d);
            group.resolution = LabJigwiABResolution.Expired; group.resolvedAt = 21d;
            view.ApplySnapshot(snapshot, Local, 21d);
            Assert.That(view.FailureVisible, Is.True); Assert.That(view.FailureFlashCount, Is.EqualTo(1));
            view.ApplySnapshot(snapshot, Local, 21d - LabJigwiScreenPresentation.RewindToleranceSeconds + .01d);
            Assert.That(view.FailureFlashCount, Is.EqualTo(1), "Jitter inside the tolerance is not a new stage (#451).");
            view.ApplySnapshot(snapshot, Local, 21.1d);
            Assert.That(view.FailureVisible, Is.True, "The flash continues once the clock moves on; a reset would have dropped it.");
            view.ApplySnapshot(snapshot, Local, 20.9d);
            Assert.That(view.FailureVisible, Is.False, "A real rewind still starts the history again.");
            Assert.That(view.FailureFlashCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TheGuestClockDoesNotStepBackWhenTheSameHeaderArrivesAgain()
        {
            snapshot = Snapshot(Group()); snapshot.serverTime = 11.8d; SetSnapshot(snapshot);
            double first = network.JigwiABPresentationTime;
            yield return new WaitForSecondsRealtime(.08f);
            double advanced = network.JigwiABPresentationTime;
            Assert.That(advanced, Is.GreaterThan(first + .05d));
            SetSnapshot(JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(snapshot)));
            Assert.That(network.JigwiABPresentationTime, Is.GreaterThanOrEqualTo(advanced),
                "A new snapshot object with the same Host time must not restart the extrapolation.");
            Assert.That(network.BattlePresentationTime, Is.EqualTo(network.JigwiABPresentationTime).Within(.01d));
            Assert.That(network.BattlePresentationHeld, Is.False);
            yield return new WaitForSecondsRealtime((float)LabGuestBattleClock.MaximumExtrapolationSeconds + .05f);
            double stalled = network.JigwiABPresentationTime;
            Assert.That(stalled, Is.EqualTo(11.8d + LabGuestBattleClock.MaximumExtrapolationSeconds).Within(1e-6));
            Assert.That(network.BattlePresentationHeld, Is.True, "No Host time for a second: voices hold instead of re-seeking.");
            snapshot.serverTime = 13d; SetSnapshot(snapshot);
            Assert.That(network.JigwiABPresentationTime, Is.EqualTo(13d).Within(.01d));
            Assert.That(network.BattlePresentationHeld, Is.False);
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
            Assert.That(view.SyncPillar.DropsVisible, Is.True, "The upcoming A warning must not hide B before its deadline.");
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
        public void SuccessShowsTheWaterSplashLineWithoutAnyRewardRows()
        {
            var group = Group(); snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            group.resolution = LabJigwiABResolution.Completed; group.resolvedAt = 12d;
            view.ApplySnapshot(snapshot, Local, 12d);
            // #509: B success is one big splash on the boss; no 5-second launch reward any more.
            Assert.That(view.status.text, Does.Contain("물벼락"));
            Assert.That(view.status.text, Does.Not.Contain("보상"));
            Assert.That(view.playerStatus.text, Does.Not.Contain("배").And.Not.Contain("불꽃 궤적"));
            Assert.That(view.FailureVisible, Is.False); Assert.That(view.BoardFireVisible, Is.False);
            Assert.That(view.GetComponentsInChildren<Canvas>(true), Is.Empty, "Reuse the authored Canvas hierarchy.");
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void FailedBoardFireKeepsTheOverlayAndCountsDownUntilItsAcceptedEnd()
        {
            var group = Group(); group.endsAt = 21d;
            snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 20.5d);
            group.resolution = LabJigwiABResolution.Expired; group.resolvedAt = 21d; group.boardFireUntil = 24d;
            view.ApplySnapshot(snapshot, Local, 21d);
            Assert.That(view.FailureVisible, Is.True); Assert.That(view.BoardFireVisible, Is.True);
            Assert.That(view.failureStatus.text, Does.Contain("조합판에 불이 붙었다").And.Contain("3.0초"));
            view.ApplySnapshot(snapshot, Local, 23.5d);
            Assert.That(view.FailureVisible, Is.True, "The fire outlasts the one-second failure flash.");
            Assert.That(view.failureStatus.text, Does.Contain("0.5초"));
            Assert.That(view.failureOverlay.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)
                .All(graphic => !graphic.raycastTarget), Is.True, "The fire never consumes input.");
            view.ApplySnapshot(snapshot, Local, 24d);
            Assert.That(view.BoardFireVisible, Is.False); Assert.That(view.FailureVisible, Is.False);
        }

        [Test]
        public void SyncProgressFillsTheDropsAndSharedCompletionClearsThem()
        {
            LabJigwiABRound group = Group(); snapshot = Snapshot(group); SetSnapshot(snapshot);
            view.ApplySnapshot(snapshot, Local, 11.2d);
            var mine = group.players.Single(player => player.playerId == Local);
            Assert.That(view.SyncPillar.FilledDrops, Is.Zero);
            group.syncWindowAt = 11.4d; mine.targets[0].extinguishedAt = 11.4d;
            view.ApplySnapshot(snapshot, Local, 11.45d);
            Assert.That(view.fireWaterDouses[0].gameObject.activeSelf, Is.True, "My accepted hit splashes once.");
            Assert.That(view.SyncPillar.NowCueVisible, Is.False, "I already hit; only the partner gets 'now'.");
            group.syncWindowAt = -1d; mine.targets[0].extinguishedAt = -1d; group.syncCount = 1;
            view.ApplySnapshot(snapshot, Local, 11.6d);
            Assert.That(view.SyncPillar.FilledDrops, Is.EqualTo(1));
            Assert.That(view.status.text, Does.Contain("1/3"));
            Assert.That(view.RemainingIgnitions, Is.EqualTo(2));
            group.syncCount = 3; foreach (var player in group.players) player.targets[0].lit = false;
            group.resolution = LabJigwiABResolution.Completed; group.resolvedAt = 23.8d;
            view.ApplySnapshot(snapshot, Local, 23.8d);
            Assert.That(view.SyncPillar.DropsVisible, Is.False);
            Assert.That(view.status.text, Does.Contain("물벼락"));
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
            Assert.That(body.JigwiABFlame.Charring, Is.False, "#479: nothing shows until its last second.");
            Material material = body.JigwiABFlame.FlameRenderer.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("C6Lab/Jigwi Ink Flame"));
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(body.JigwiABFlame.CharTexture), "The runtime char, not the old flame art.");
            body.SetJigwiABBurn(.5f, true);
            Assert.That(body.JigwiABFlame.Charring, Is.True, "The last second chars it from the bottom.");
            Assert.That(body.JigwiABFlame.TongueRenderer.enabled, Is.True, "Live flames stand on the burning line.");
            Assert.That(body.JigwiABFlame.TongueRenderer.sharedMaterial.GetTexture("_BaseMap"),
                Is.SameAs(Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash")));
            Assert.That(body.JigwiABFlame.Progress, Is.EqualTo(.5f).Within(.0001f));
            Texture2D art = body.JigwiABFlame.CharTexture;
            Assert.That(art.GetPixel(32, 4).a, Is.GreaterThan(.8f), "The bottom is ash.");
            Assert.That(art.GetPixel(32, 60).a, Is.Zero, "The top is still untouched.");
            body.SetJigwiABBurn(5f, true);
            Assert.That(body.JigwiABFlame.Charring, Is.False);
            Assert.That(body.JigwiABFlame.TongueRenderer.enabled, Is.False);
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
            warningAt = 10d, activeAt = 11d, endsAt = 21d,
            shrinkScale = .65f, moveAmount = .03f, movePeriodSeconds = 4f, targetRadius = .3f
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
            // #509: one pillar per picked seat.
            targets = new[] { new LabJigwiABFireTarget { id = "ab-group/" + owner + "/0", x = .5f, y = .62f } }
        };
    }
}
