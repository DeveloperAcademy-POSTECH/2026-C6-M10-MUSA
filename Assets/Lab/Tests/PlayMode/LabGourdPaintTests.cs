using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Default-Scene runtime wiring, presentation edge cases, and real Solo Host projectile collisions.
    /// Direct ApprovedHit calls below test presentation inputs only; the Host integration test
    /// generates and opens a deterministic SecretBox fixture through the saved board gestures.
    /// </summary>
    public sealed class LabGourdPaintTests
    {
        [UnityTearDown]
        public IEnumerator UnloadSavedScene()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Gourd paint test cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MainSceneUsesWhiteGourdAndFiveElementPaintWithoutCrackFinish()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null, "The ordinary Lab controller must attach the paint presentation at runtime.");
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.InitializationError, Is.Null.Or.Empty);
            Assert.That(paint.target, Is.SameAs(paint.network.target));
            Assert.That(paint.VisualInstance, Is.SameAs(paint.target.MountedVisual));
            Assert.That(paint.VisualInstance.activeInHierarchy, Is.True);
            Assert.That(paint.Shell.IsChildOf(paint.VisualInstance.transform), Is.True);
            Assert.That(paint.Shell.GetComponent<Renderer>().sharedMaterial, Is.SameAs(paint.paintMaterial));
            Assert.That(paint.paintMaterial.shader.isSupported, Is.True);
            Assert.That(paint.target.Hitbox.enabled, Is.True);
            Assert.That(paint.target.Hitbox.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(paint.VisualInstance.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(paint.VisualInstance.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var crack = paint.target.GetComponent<LabGourdCrack>();
            var finish = paint.target.GetComponent<LabGourdFinish>();
            Assert.That(crack, Is.Not.Null, "Keep the saved references for the preserved crack experiment.");
            Assert.That(crack.enabled, Is.False);
            Assert.That(finish, Is.Not.Null);
            Assert.That(finish.enabled, Is.False);
            Assert.That(LabGourdFinish.DelayVictory(paint.network,
                new LabSnapshot { sessionId = "paint", round = 1, phase = LabPhase.Victory }), Is.False);
            Assert.That(paint.PaintCount, Is.Zero);

            var retainedProperties = ReadBlock(paint);
            retainedProperties.SetFloat("_TestRetainedProperty", .37f);
            paint.Shell.GetComponent<Renderer>().SetPropertyBlock(retainedProperties);
            for (int i = 0; i < 5; i++)
            {
                var element = (LabOrbElement)(i + 1);
                paint.target.NotifyApprovedHit(new Vector3(.1f * i, .1f, .5f), element);
                Assert.That(paint.PaintCount, Is.EqualTo(i + 1));
                MaterialPropertyBlock block = ReadBlock(paint);
                Color expected = paint.network.config.OrbAppearance.ElementColorFor(element);
                Assert.That(block.GetVectorArray("_PaintColors")[i], Is.EqualTo((Vector4)expected));
                Assert.That(block.GetFloat("_TestRetainedProperty"), Is.EqualTo(.37f),
                    "Paint updates must preserve another presentation's property block values.");
            }
            paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.None);
            paint.target.NotifyApprovedHit(Vector3.forward, (LabOrbElement)999);
            paint.target.NotifyApprovedHit(new Vector3(float.NaN, 0f, 0f), LabOrbElement.Fire);
            Assert.That(paint.PaintCount, Is.EqualTo(5), "None and out-of-range elements have no paint color.");
            CaptureBattle(paint.network, "five-colors.png");

            paint.ClearPaint();
            Assert.That(paint.Paint(Vector3.zero, Color.red), Is.False);
            Assert.That(paint.Paint(new Vector3(float.NaN, 0f, 0f), Color.red), Is.False);
            Assert.That(paint.Paint(new Vector3(float.PositiveInfinity, 0f, 0f), Color.red), Is.False);
            Assert.That(paint.PaintCount, Is.Zero);
            for (int i = 0; i < LabGourdPaint.Capacity; i++)
                Assert.That(paint.Paint(Vector3.forward, Color.red), Is.True);
            float oneSpotCoverage = paint.Coverage;
            Assert.That(oneSpotCoverage, Is.GreaterThan(0f));
            Assert.That(oneSpotCoverage, Is.LessThan(.1f));
            Assert.That(paint.PaintCount, Is.EqualTo(128));
            Assert.That(paint.Paint(Vector3.forward, Color.blue), Is.False);
            Assert.That(paint.Coverage, Is.EqualTo(oneSpotCoverage),
                "Repeated hits at one spot cannot fill the whole gourd.");
            Assert.That(paint.PaintCount, Is.EqualTo(128));
            Assert.That(ReadBlock(paint).GetVectorArray("_PaintColors")[127], Is.EqualTo((Vector4)Color.red),
                "The 129th hit must not replace any retained mark.");
            paint.ClearPaint();
            Assert.That(paint.PaintCount, Is.Zero);
            Assert.That(paint.Coverage, Is.Zero);
            for (int i = 0; i < 64; i++)
            {
                float y = 1f - 2f * (i + .5f) / 64f;
                float angle = i * 2.39996323f;
                var direction = new Vector3(Mathf.Cos(angle) * Mathf.Sqrt(1f - y * y),
                    y, Mathf.Sin(angle) * Mathf.Sqrt(1f - y * y));
                Assert.That(paint.Paint(direction, Color.red), Is.True);
            }
            Assert.That(paint.Coverage, Is.GreaterThanOrEqualTo(LabModel.PaintVictoryCoverage),
                "The 80% goal must be reachable before the 128-mark limit.");
            paint.ClearPaint();
            Assert.That(ReadBlock(paint).GetInt("_PaintCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SameHitboxPointHasSameShellCoordinatesAcrossCommonSwingAndRotation()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            var hitbox = paint.target.Hitbox.transform;
            Transform common = hitbox.parent;
            Assert.That(paint.target.VisualMount.parent, Is.SameAs(common),
                "Hitbox and visual mount must share the hanging target pose.");
            Quaternion initialRotation = common.localRotation;
            Vector3 localHit = new Vector3(.2f, .1f, .45f);
            paint.target.NotifyApprovedHit(localHit, LabOrbElement.Fire);
            Vector4 first = ReadBlock(paint).GetVectorArray("_PaintPositions")[0];
            try
            {
                common.localRotation = Quaternion.Euler(16f, 73f, -11f) * initialRotation;
                paint.target.NotifyApprovedHit(localHit, LabOrbElement.Water);
                Vector4 second = ReadBlock(paint).GetVectorArray("_PaintPositions")[1];
                Assert.That(Vector4.Distance(first, second), Is.LessThan(.0001f),
                    "Common local-to-world and world-to-shell transforms cancel the current pose.");
                Assert.That(ReadBlock(paint).GetVectorArray("_PaintPositions")[0], Is.EqualTo(first),
                    "Rotating the target does not rewrite earlier marks.");
            }
            finally { common.localRotation = initialRotation; }
        }

        [UnityTest]
        public IEnumerator FirstHitBeforeNextFrameSurvivesAndRestartStopNewLobbyClearPaint()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            LabNetwork network = paint.network;
            network.port = 17785;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                // Deliberately notify before Paint.Update has observed the new session key.
                paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Fire);
                Assert.That(paint.PaintCount, Is.EqualTo(1));
                yield return null;
                Assert.That(paint.PaintCount, Is.EqualTo(1), "The first mark cannot be cleared a frame later.");
                Assert.That(network.RestartDeveloperSolo(), Is.True);
                yield return null;
                Assert.That(paint.PaintCount, Is.Zero);
                paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Wood);
                Assert.That(paint.PaintCount, Is.EqualTo(1));
                network.Stop();
                yield return null;
                Assert.That(network.Snapshot, Is.Null);
                Assert.That(paint.PaintCount, Is.Zero, "Leaving the session removes its marks.");
                // Stop/Host are the same real session transitions as entering another room lobby.
                network.Host();
                Assert.That(network.IsHost, Is.True, network.Notice);
                Assert.That(network.Snapshot, Is.Null);
                yield return null;
                Assert.That(paint.PaintCount, Is.Zero);
            }
            finally { if (network.IsConnected) network.Stop(); }
        }

        [UnityTest]
        public IEnumerator SavedSoloHostElementHitsPaintAndFinishImmediatelyThenRetryAndDevReset()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            LabNetwork network = paint.network;
            var controller = Object.FindFirstObjectByType<LabSceneController>();
            var hud = Object.FindFirstObjectByType<LabHud>();
            LabConfig savedNetworkConfig = network.config;
            LabConfig savedBoardConfig = controller.config;
            LabConfig fixture = Object.Instantiate(savedNetworkConfig);
            float[] tuning = fixture.CaptureDeveloperValues();
            SetTuning(tuning, "Monster Max HP", 40f);
            SetTuning(tuning, "Damage per Hit", 20f);
            SetTuning(tuning, "Battle Duration (seconds)", 30f);
            SetTuning(tuning, "Orb Generation Cost", 1f);
            SetTuning(tuning, "Item Lifetime (seconds)", 30f);
            SetTuning(tuning, "Item Unlock Swipes Min", 0f);
            SetTuning(tuning, "Item Unlock Swipes Max", 0f);
            SetTuning(tuning, "Yin Chance (%)", 0f);
            SetTuning(tuning, "Yang Chance (%)", 0f);
            SetTuning(tuning, "Relay Orb Chance (%)", 0f);
            SetTuning(tuning, "Secret Box Chance (%)", 100f);
            SetTuning(tuning, "Potion Chance (%)", 0f);
            SetTuning(tuning, "Bomb Chance (%)", 0f);
            SetTuning(tuning, "Spawn Pop Duration (seconds)", 0f);
            SetTuning(tuning, "Spawn Minimum Speed (board widths/s)", 0f);
            SetTuning(tuning, "Spawn Maximum Speed (board widths/s)", 0f);
            SetTuning(tuning, "Drum Drift Speed (board widths/s)", 0f);
            SetTuning(tuning, "Throw Origin Radius", 4f);
            SetTuning(tuning, "Throw Origin Height Offset", -.5f);
            SetTuning(tuning, "Throw Minimum Upward Speed", 2f);
            SetTuning(tuning, "Throw Lateral Gain", 2.5f);
            SetTuning(tuning, "Throw Upward Gain", 2.1f);
            SetTuning(tuning, "Throw Forward Gain", 4.5f);
            SetTuning(tuning, "Throw Maximum World Speed", 18f);
            SetTuning(tuning, "Throw Gravity", 9.81f);
            SetTuning(tuning, "Throw Power Exponent", 1f);
            SetTuning(tuning, "Throw Loft Offset (degrees)", 0f);
            SetTuning(tuning, "Throw Air Damping", 0f);
            Assert.That(fixture.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = fixture;
            controller.config = fixture;
            controller.ReconfigureBoard();
            network.port = 17784;
            var approvedDamage = new System.Collections.Generic.List<int>();
            System.Action<int> countHit = approvedDamage.Add;
            network.LocalThrowHit += countHit;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                yield return null;
                network.Generate();
                LabOrbState box = network.Snapshot.orbs.Single();
                Assert.That(box.kind, Is.EqualTo(LabOrbKind.SecretBox));
                Assert.That(box.lockSwipes, Is.Zero);
                TapOrb(network.board, box.id, 10d);
                TapOrb(network.board, box.id, 10.2d);
                Assert.That(network.Snapshot.orbs.Length, Is.EqualTo(2));
                Assert.That(network.Snapshot.orbs.All(orb => orb.kind == LabOrbKind.Combined
                    && orb.element != LabOrbElement.None), Is.True);
                string[] shots = network.Snapshot.orbs.Select(orb => orb.id).ToArray();
                int index = 0;
                foreach (string shotId in shots)
                {
                    network.target.GetComponent<LabHangingTarget>().ResetSwing();
                    LabOrbElement element = network.Snapshot.orbs.Single(orb => orb.id == shotId).element;
                    LaunchThroughSavedBoard(network.board, shotId, 20d + index * 10d);
                    Assert.That(network.Snapshot.orbs.Single(orb => orb.id == shotId).inFlight, Is.True);
                    LabProjectile projectile = network.projectileRoot.GetComponentsInChildren<LabProjectile>()
                        .Single(candidate => candidate.ProjectileId == shotId);
                    Assert.That(projectile.GetComponent<SphereCollider>(), Is.Not.Null);
                    int previousHp = network.Snapshot.hp;
                    for (int step = 0; step < 150 && network.Snapshot.hp == previousHp; step++)
                        yield return new WaitForFixedUpdate();
                    Assert.That(network.Snapshot.hp, Is.EqualTo(previousHp - 20),
                        "The real Host projectile/cylinder collision must confirm damage before painting.");
                    Assert.That(network.Snapshot.orbs.Any(orb => orb.id == shotId), Is.False);
                    Assert.That(network.LastShotReport, Does.StartWith("SHOT HIT"));
                    index++;
                    Assert.That(paint.PaintCount, Is.EqualTo(index));
                    Assert.That(approvedDamage.Count, Is.EqualTo(index));
                    Assert.That(ReadBlock(paint).GetVectorArray("_PaintColors")[index - 1],
                        Is.EqualTo((Vector4)fixture.OrbAppearance.ElementColorFor(element)));
                    yield return null;
                    Assert.That(paint.PaintCount, Is.EqualTo(index), "The mark survives the next frame.");
                    if (index == 1)
                    {
                        Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                        Assert.That(hud.resultControls.activeSelf, Is.False);
                        CaptureBattle(network, "approved-element-hit.png");
                    }
                }
                Assert.That(approvedDamage, Is.All.EqualTo(20));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing),
                    "Paint mode cannot win just because the old HP counter reached zero.");
                Assert.That(network.Snapshot.paintCoverage, Is.GreaterThan(0f));
                Assert.That(network.Snapshot.paintCoverage, Is.LessThan(LabModel.PaintVictoryCoverage));
                Assert.That(LabGourdFinish.DelayVictory(network, network.Snapshot), Is.False);
                Assert.That(hud.resultControls.activeSelf, Is.False);
                Assert.That(paint.VisualInstance.activeSelf, Is.True);
                Assert.That(network.target.VisualMount.Find("Gourd Left Half"), Is.Null);
                Assert.That(network.target.VisualMount.Find("Gourd Right Half"), Is.Null);
                Assert.That(network.target.VisualMount.Find("Small Yokai Spirit"), Is.Null);
                CaptureBattle(network, "victory-two-marks.png");
                string completedSession = network.Snapshot.sessionId;
                Assert.That(network.RestartDeveloperSolo(), Is.True);
                yield return null;
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(completedSession));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(network.Snapshot.hp, Is.EqualTo(40));
                Assert.That(paint.PaintCount, Is.Zero);
                Assert.That(hud.resultControls.activeSelf, Is.False);

                // A real unlocked None-element Relay hit still damages but adds no color.
                tuning = fixture.CaptureDeveloperValues();
                SetTuning(tuning, "Secret Box Chance (%)", 0f);
                SetTuning(tuning, "Relay Orb Chance (%)", 100f);
                SetTuning(tuning, "Relay x2 Damage Multiplier", 1f);
                Assert.That(fixture.TryApplyDeveloperValues(tuning, out error), Is.True, error);
                Assert.That(network.RestartDeveloperSolo(), Is.True);
                yield return null;
                network.Generate();
                LabOrbState relay = network.Snapshot.orbs.Single();
                Assert.That(relay.kind, Is.EqualTo(LabOrbKind.Relay));
                Assert.That(relay.element, Is.EqualTo(LabOrbElement.None));
                network.target.GetComponent<LabHangingTarget>().ResetSwing();
                LaunchThroughSavedBoard(network.board, relay.id, 50d);
                for (int step = 0; step < 150 && network.Snapshot.hp == 40; step++)
                    yield return new WaitForFixedUpdate();
                Assert.That(network.Snapshot.hp, Is.EqualTo(20));
                Assert.That(paint.PaintCount, Is.Zero);

                // Use the saved DEV form and public action to replace the current Solo round.
                paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Earth);
                Assert.That(paint.PaintCount, Is.EqualTo(1));
                var developerUi = Resources.FindObjectsOfTypeAll<LabDeveloperMode>()
                    .Single(ui => ui.gameObject.scene == SceneManager.GetActiveScene());
                developerUi.Open();
                SetInput(developerUi, "Monster Max HP", "80");
                developerUi.ApplyAndStart();
                yield return null;
                Assert.That(network.Snapshot.hp, Is.EqualTo(80));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(paint.PaintCount, Is.Zero, "DEV restart clears marks with the new session.");
                network.Stop();
                yield return null;
                Assert.That(paint.PaintCount, Is.Zero);
            }
            finally
            {
                network.LocalThrowHit -= countHit;
                if (network.IsConnected) network.Stop();
                network.config = savedNetworkConfig;
                controller.config = savedBoardConfig;
                controller.ReconfigureBoard();
                Object.Destroy(fixture);
            }
        }

        [UnityTest]
        public IEnumerator DisabledPaintDoesNotListenAndMissingReferencesKeepTargetVisible()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            paint.enabled = false;
            int retainedCount = paint.PaintCount;
            paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Fire);
            Assert.That(paint.PaintCount, Is.EqualTo(retainedCount));
            paint.enabled = true;
            yield return null;
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Fire);
            Assert.That(paint.PaintCount, Is.EqualTo(retainedCount + 1));

            var target = LabTarget.CreateCylinder(null, new Vector3(20, 0, 0), .6f, 2f);
            var obj = new GameObject("Incomplete paint network");
            var network = obj.AddComponent<LabNetwork>();
            network.target = target;
            var invalidPaint = target.gameObject.AddComponent<LabGourdPaint>();
            invalidPaint.target = target;
            invalidPaint.network = network;
            try
            {
                yield return null;
                Assert.That(invalidPaint.IsReady, Is.False);
                Assert.That(invalidPaint.InitializationError, Is.Not.Empty);
                Assert.That(invalidPaint.VisualInstance, Is.Null);
                Assert.That(target.Hitbox.enabled, Is.True);
                Assert.That(target.Hitbox.GetComponent<MeshRenderer>().enabled, Is.True,
                    "Missing art must keep the diagnostic fallback visible.");
                target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Fire);
                Assert.That(invalidPaint.PaintCount, Is.Zero);
            }
            finally { Object.Destroy(target.gameObject); Object.Destroy(obj); }
        }

        [UnityTest]
        public IEnumerator DeveloperSliderAppliesModesToFreshSoloRoundsAndDoesNotRemountUnchangedMode()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabDeveloperMode ui = SavedDeveloperUi();
            LabNetwork network = ui.network;
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            LabGourdCrack crack = network.target.GetComponent<LabGourdCrack>();
            LabGourdFinish finish = network.target.GetComponent<LabGourdFinish>();
            float[] savedValues = ui.sourceConfig.CaptureDeveloperValues();
            network.port = 17786;
            int modeIndex = FieldIndex("Gourd Mode (0 paint, 1 crack)");
            try
            {
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
                Assert.That(paint.IsReady, Is.True, paint.InitializationError);
                GameObject initialWhite = paint.VisualInstance;
                Assert.That(LabGourdPaint.Attach(network), Is.SameAs(paint));
                paint.ApplyMode();
                Assert.That(paint.VisualInstance, Is.SameAs(initialWhite), "Repeated attachment cannot duplicate a ready visual.");
                Assert.That(network.target.GetComponents<LabGourdPaint>().Length, Is.EqualTo(1));

                ui.Open();
                Assert.That(ui.valueInputs.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
                Assert.That(ui.valueSliders.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
                Assert.That(ui.valueSliders[modeIndex], Is.Not.Null);
                ui.valueSliders[modeIndex].value = 1f;
                Assert.That(ui.valueInputs[modeIndex].text, Is.EqualTo("1"));
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Paint), "Changing a form control alone cannot change the running config.");
                Assert.That(paint.VisualInstance, Is.SameAs(initialWhite));
                ui.ApplyAndStart();
                yield return null;
                yield return null;
                Assert.That(network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(crack.IsReady, Is.True, crack.InitializationError);
                Assert.That(crack.enabled && finish.enabled, Is.True);
                Assert.That(paint.enabled, Is.False);
                Assert.That(paint.Shell, Is.Null);
                Assert.That(paint.PaintCount, Is.Zero);
                Assert.That(initialWhite == null, Is.True, "The previous owned white visual is removed after the frame.");
                GameObject intact = crack.VisualInstance;
                paint.ApplyMode();
                paint.ApplyMode();
                Assert.That(crack.VisualInstance, Is.SameAs(intact), "Reapplying an unchanged crack mode must not mount another model.");
                network.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Fire);
                Assert.That(paint.PaintCount, Is.Zero, "The disabled paint presentation must not listen in crack mode.");
                CaptureBattle(network, "dev-crack-mode.png");

                string crackSession = network.Snapshot.sessionId;
                float runningTimeScale = Time.timeScale;
                Assert.That(network.PauseBattle(), Is.True);
                Assert.That(network.IsBattlePaused, Is.True);
                ui.Open();
                SetInput(ui, "Gourd Mode (0 paint, 1 crack)", "0");
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
                ui.ApplyAndStart();
                yield return null;
                yield return null;
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(crackSession));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(network.IsBattlePaused, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(runningTimeScale));
                Assert.That(paint.IsReady, Is.True, paint.InitializationError);
                Assert.That(crack.enabled || finish.enabled, Is.False);
                Assert.That(intact == null, Is.True, "Crack's owned model must be released before paint mounts.");
                Assert.That(paint.PaintCount, Is.Zero);
                network.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Water);
                Assert.That(paint.PaintCount, Is.EqualTo(1));
                GameObject newWhite = paint.VisualInstance;
                paint.ApplyMode();
                Assert.That(paint.InitializePresentation(), Is.True);
                Assert.That(paint.VisualInstance, Is.SameAs(newWhite));
                Assert.That(paint.PaintCount, Is.EqualTo(1), "Idempotent initialization cannot erase the current round's marks.");

                // Same-mode DEV restart is a new round and must erase its old marks.
                string paintSession = network.Snapshot.sessionId;
                ui.Open();
                ui.ApplyAndStart();
                yield return null;
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(paintSession));
                Assert.That(paint.PaintCount, Is.Zero);
                Assert.That(network.target.VisualMount.Find("Gourd Left Half"), Is.Null);
                Assert.That(network.target.VisualMount.Find("Small Yokai Spirit"), Is.Null);
                Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(savedValues), "DEV mode changes must leave the saved config asset untouched.");
            }
            finally
            {
                if (network.IsConnected) network.Stop();
            }
        }

        [UnityTest]
        public IEnumerator DeveloperRoomCreationAndLobbyRetuningApplyTheHostGourdMode()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabDeveloperMode ui = SavedDeveloperUi();
            LabNetwork network = ui.network;
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            LabGourdCrack crack = network.target.GetComponent<LabGourdCrack>();
            float[] savedValues = ui.sourceConfig.CaptureDeveloperValues();
            network.port = 17787;
            try
            {
                ui.Open();
                ui.targetParticipantsSlider.value = 2f;
                SetInput(ui, "Gourd Mode (0 paint, 1 crack)", "1");
                ui.ApplyAndHost();
                yield return null;
                Assert.That(network.IsHost, Is.True, ui.feedbackText.text);
                Assert.That(network.IsDeveloperSolo, Is.False);
                Assert.That(network.TargetParticipantCount, Is.EqualTo(2));
                Assert.That(network.Snapshot, Is.Null, "This test waits in the real Host lobby; it does not emulate a connected client.");
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
                Assert.That(crack.IsReady, Is.True, crack.InitializationError);
                Assert.That(paint.enabled, Is.False);
                Assert.That(ui.sceneController.config, Is.SameAs(network.config));
                Assert.That(ui.seatCamera.config, Is.SameAs(network.config));

                ui.Open();
                Assert.That(ui.applyHostLabel.text, Is.EqualTo("APPLY TO ROOM"));
                SetInput(ui, "Gourd Mode (0 paint, 1 crack)", "0");
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
                ui.ApplyAndHost();
                yield return null;
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
                Assert.That(paint.IsReady, Is.True, paint.InitializationError);
                Assert.That(crack.enabled, Is.False);
                Assert.That(network.CanStartBattle, Is.False, "A mode change cannot bypass the remaining participant/config confirmation gates.");
                Assert.That(ui.sceneController.config, Is.SameAs(network.config));
                Assert.That(ui.seatCamera.config, Is.SameAs(network.config));
                Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(savedValues));
            }
            finally { if (network.IsConnected) network.Stop(); }
            yield return null;
            Assert.That(network.config, Is.SameAs(ui.sourceConfig));
            Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
        }

        [UnityTest]
        public IEnumerator BrokenModeReferencesFallBackWithoutHoldingVictoryAndCanBeRetried()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabDeveloperMode ui = SavedDeveloperUi();
            LabNetwork network = ui.network;
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            LabGourdCrack crack = network.target.GetComponent<LabGourdCrack>();
            GameObject savedWhite = paint.visualPrefab;
            GameObject savedCrack = crack.gourdPrefab;
            var victory = new LabSnapshot { sessionId = "invalid-mode", round = 1, phase = LabPhase.Victory, hp = 0 };
            network.port = 17788;
            try
            {
                paint.enabled = false;
                paint.visualPrefab = null;
                paint.enabled = true;
                yield return null;
                Assert.That(paint.IsReady, Is.False);
                Assert.That(paint.InitializationError, Is.Not.Empty);
                Assert.That(network.target.Hitbox.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(LabGourdFinish.DelayVictory(network, victory), Is.False);
                paint.visualPrefab = savedWhite;
                paint.ApplyMode();
                yield return null;
                Assert.That(paint.IsReady, Is.True, paint.InitializationError);

                crack.gourdPrefab = null;
                ui.Open();
                SetInput(ui, "Gourd Mode (0 paint, 1 crack)", "1");
                ui.ApplyAndStart();
                yield return null;
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
                Assert.That(crack.IsReady, Is.False);
                Assert.That(crack.InitializationError, Is.Not.Empty);
                Assert.That(network.target.Hitbox.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(network.target.Hitbox.enabled, Is.True);
                Assert.That(LabGourdFinish.DelayVictory(network, victory), Is.False);
                crack.gourdPrefab = savedCrack;
                paint.ApplyMode();
                yield return null;
                Assert.That(crack.IsReady, Is.True, crack.InitializationError);
                Assert.That(paint.enabled, Is.False);
            }
            finally
            {
                paint.visualPrefab = savedWhite;
                crack.gourdPrefab = savedCrack;
                if (network.IsConnected) network.Stop();
            }
        }

        static LabDeveloperMode SavedDeveloperUi() => Resources.FindObjectsOfTypeAll<LabDeveloperMode>()
            .Single(ui => ui.gameObject.scene == SceneManager.GetActiveScene());

        static int FieldIndex(string label) => Enumerable.Range(0, LabConfig.DeveloperFields.Count)
            .Single(index => LabConfig.DeveloperFields[index].Label == label);

        static MaterialPropertyBlock ReadBlock(LabGourdPaint paint)
        {
            var block = new MaterialPropertyBlock();
            paint.Shell.GetComponent<Renderer>().GetPropertyBlock(block);
            return block;
        }

        static void TapOrb(LabOrbBoard board, string id, double now)
        {
            Vector3 position = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == id).Body.position;
            Vector2 point = new Vector2(position.x, position.y);
            Assert.That(board.TryBeginDragAtWorld(point, now), Is.True);
            Assert.That(board.EndDragAtWorld(point, now + .05d), Is.True);
        }

        static void LaunchThroughSavedBoard(LabOrbBoard board, string id, double now)
        {
            LabOrbView orb = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == id);
            Vector3 position = orb.Body.position;
            Vector2 from = new Vector2(position.x, position.y);
            Vector2 aim = board.AreaShape.ToWorld(new Vector2(.5f, .25f), orb.HitSphere.radius);
            Assert.That(board.TryBeginDragAtWorld(from, now), Is.True);
            Assert.That(board.DragToWorld(aim, now + 1d), Is.True);
            Assert.That(board.EndDragAtWorld(aim + Vector2.up * (board.CenterBounds.width * .2f), now + 1.04d), Is.True);
        }

        static void SetTuning(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(candidate => LabConfig.DeveloperFields[candidate].Label == label);
            values[index] = value;
        }

        static void SetInput(LabDeveloperMode ui, string label, string value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(candidate => LabConfig.DeveloperFields[candidate].Label == label);
            ui.valueInputs[index].text = value;
        }

        static void CaptureBattle(LabNetwork network, string name)
        {
            // Batch test workers have no render surface; gameplay assertions still run there.
            if (Application.isBatchMode) return;
            Camera camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c => c.name == "BattleCamera");
            const string directory = "Library/Issue166ModeCaptures";
            Directory.CreateDirectory(directory);
            var texture = new RenderTexture(540, 960, 24);
            var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                texture.Release();
                Object.Destroy(texture);
                Object.Destroy(image);
            }
        }
    }
}
