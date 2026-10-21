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
    /// Default-Scene runtime wiring, offline texture previews, and real Solo Host projectile collisions.
    /// Connected approval events refresh already-owned state; they never inject Host paint.
    /// Physics fixtures open deterministic SecretBoxes through the saved board gestures.
    /// </summary>
    public sealed class LabGourdPaintTests
    {
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();

        [UnityTearDown]
        public IEnumerator UnloadSavedScene() => fixture.Cleanup();

        [UnityTest]
        public IEnumerator SavedSceneUsesImportedHanjiMaterialWithoutMissingScripts()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.That(component, Is.Not.Null,
                        "The saved Lab hierarchy must not contain an unresolved script: " + root.name);

            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Material material = Resources.Load<Material>("GourdPaint/WhiteGourd");
            Assert.That(paint.Shell.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
            Assert.That(material.shader.name, Is.EqualTo("C6Lab/GourdPaint"));
            Assert.That(material.shader.isSupported, Is.True);
            Assert.That(material.HasProperty("_PaperTex"), Is.True);
            var paper = material.GetTexture("_PaperTex") as Texture2D;
            Assert.That(paper, Is.Not.Null, "The production Material must reference an imported fiber texture.");
            Assert.That(paper.mipmapCount, Is.GreaterThan(1));
            Assert.That(paper.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(paper.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(paper.filterMode, Is.EqualTo(FilterMode.Trilinear));
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(paper);
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(path),
                Is.EqualTo("35b15acfe93d492ba7bc02f831ec670d"));
            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(UnityEditor.TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.True);
#endif
        }

        [UnityTest]
        public IEnumerator MainSceneUsesWhiteGourdAndCumulativePaintTextureWithoutAHitCountCap()
        {
            yield return this.fixture.Load();
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null, "The ordinary controller attaches the presentation at runtime.");
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.InitializationError, Is.Null.Or.Empty);
            Assert.That(paint.target, Is.SameAs(paint.network.target));
            Assert.That(paint.VisualInstance, Is.SameAs(paint.target.MountedVisual));
            Assert.That(paint.VisualInstance.activeInHierarchy, Is.True);
            var yokai = paint.VisualInstance.GetComponent<LabGourdYokai>();
            Assert.That(yokai, Is.Not.Null, "The paint gourd brings its yokai along for combos (#244).");
            Assert.That(yokai.Ready, Is.True);
            Assert.That(paint.Shell.IsChildOf(paint.VisualInstance.transform), Is.True);
            Assert.That(paint.Shell.GetComponent<Renderer>().sharedMaterial, Is.SameAs(paint.paintMaterial));
            Assert.That(paint.paintMaterial.shader.isSupported, Is.True);
            Assert.That(paint.target.Hitbox.enabled, Is.True);
            Assert.That(paint.target.Hitbox.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(paint.VisualInstance.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(paint.VisualInstance.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(paint.target.GetComponent<LabGourdCrack>().enabled, Is.False);
            Assert.That(paint.target.GetComponent<LabGourdFinish>().enabled, Is.False);
            Assert.That(LabGourdFinish.DelayVictory(paint.network,
                new LabSnapshot { sessionId = "paint", round = 1, phase = LabPhase.Victory }), Is.False);
            Assert.That(paint.PaintCount, Is.Zero);
            Assert.That(paint.PaintTexture.width, Is.EqualTo(LabPaintSurface.Width));
            Assert.That(paint.PaintTexture.height, Is.EqualTo(LabPaintSurface.Height));
            Assert.That(paint.PaintTexture.mipmapCount, Is.EqualTo(1));
            Assert.That(paint.PaintTexture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(paint.PaintTexture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(paint.PaintTexture.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));

            // Before connecting, ApprovedHit is a presentation preview input only. Its CPU
            // premultiplied colors must be uploaded without a second shader-side stamp list.
            var retained = ReadBlock(paint);
            retained.SetFloat("_TestRetainedProperty", .37f);
            paint.Shell.GetComponent<Renderer>().SetPropertyBlock(retained);
            var expected = new LabPaintSurface();
            for (int i = 0; i < 5; i++)
            {
                var element = (LabOrbElement)(i + 1);
                Vector3 localPoint = new Vector3(.1f * i, .1f, .5f);
                Vector3 world = paint.target.Hitbox.transform.TransformPoint(localPoint);
                Vector3 direction = paint.Shell.InverseTransformPoint(world).normalized;
                Color color = paint.network.config.OrbAppearance.ElementColorFor(element);
                Assert.That(expected.Stamp(direction, color, paint.splatRadius), Is.True);
                paint.target.NotifyApprovedHit(localPoint, element);
                Assert.That(paint.PaintCount, Is.EqualTo(i + 1));
                AssertTextureMatches(expected, paint);
                Assert.That(ReadBlock(paint).GetFloat("_TestRetainedProperty"), Is.EqualTo(.37f));
            }
            paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.None);
            paint.target.NotifyApprovedHit(Vector3.forward, (LabOrbElement)999);
            paint.target.NotifyApprovedHit(new Vector3(float.NaN, 0f, 0f), LabOrbElement.Fire);
            Assert.That(paint.PaintCount, Is.EqualTo(5));
            AssertTextureMatches(expected, paint);
            CaptureBattle(paint.network, "offline-five-color-map.png");

            paint.ClearPaint();
            Assert.That(paint.Paint(Vector3.zero, Color.red), Is.False);
            Assert.That(paint.Paint(new Vector3(float.NaN, 0f, 0f), Color.red), Is.False);
            Assert.That(paint.Paint(new Vector3(float.PositiveInfinity, 0f, 0f), Color.red), Is.False);
            Assert.That(paint.PaintCount, Is.Zero);
            for (int i = 0; i < 128; i++) Assert.That(paint.Paint(Vector3.forward, Color.red), Is.True);
            float oneSpotCoverage = paint.Coverage;
            Assert.That(oneSpotCoverage, Is.GreaterThan(0f));
            Assert.That(oneSpotCoverage, Is.LessThan(.1f));
            Assert.That(paint.PaintCount, Is.EqualTo(128));
            Assert.That(paint.Paint(Vector3.forward, Color.blue), Is.True,
                "The former 128-entry shader list cannot limit additional approved marks.");
            Assert.That(paint.Coverage, Is.EqualTo(oneSpotCoverage), "Overpainting does not count a cell twice.");
            Assert.That(paint.Paint(Vector3.back, Color.green), Is.True);
            Assert.That(paint.PaintCount, Is.EqualTo(130));
            Assert.That(paint.Coverage, Is.GreaterThan(oneSpotCoverage),
                "A previously untouched direction must still advance coverage after 128 repeated hits.");
            AssertTextureMatches(paint.Surface, paint);
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
            Assert.That(paint.Coverage, Is.GreaterThanOrEqualTo(.8f), "Distributed offline preview marks cover the same visible map.");
            AssertTextureMatches(paint.Surface, paint);
            paint.ClearPaint();
            Assert.That(paint.PaintTexture.GetPixels32().All(pixel => pixel.a == 0), Is.True);
        }

        [UnityTest]
        public IEnumerator SameHitboxPointProducesTheSameMapAcrossCommonSwingAndRotation()
        {
            yield return this.fixture.Load();
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Transform hitbox = paint.target.Hitbox.transform;
            Transform common = hitbox.parent;
            Assert.That(paint.target.VisualMount.parent, Is.SameAs(common));
            Quaternion initialRotation = common.localRotation;
            Vector3 localHit = new Vector3(.2f, .1f, .45f);
            paint.target.NotifyApprovedHit(localHit, LabOrbElement.Fire);
            Color32[] first = paint.PaintTexture.GetPixels32();
            try
            {
                common.localRotation = Quaternion.Euler(16f, 73f, -11f) * initialRotation;
                Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(first), "Changing pose does not rewrite the surface map.");
                paint.ClearPaint();
                paint.target.NotifyApprovedHit(localHit, LabOrbElement.Fire);
                Color32[] second = paint.PaintTexture.GetPixels32();
                AssertPixelsClose(first, second, 1,
                    "Common local-to-world and world-to-shell transforms cancel; one byte allows float transform rounding.");
                AssertTextureMatches(paint.Surface, paint);
            }
            finally { common.localRotation = initialRotation; }
        }

        [UnityTest]
        public IEnumerator OfflinePreviewCannotLeakIntoConnectedRoundsAndUnapprovedViewCallsAreInert()
        {
            yield return this.fixture.Load();
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            LabNetwork network = paint.network;
            this.fixture.UseAvailablePort(network);
            paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Fire);
            Assert.That(paint.PaintCount, Is.EqualTo(1), "Direct input is allowed only in an offline preview.");
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                Assert.That(paint.PaintCount, Is.Zero, "A real round starts with its own empty Host map.");
                paint.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Fire);
                Assert.That(paint.Paint(Vector3.forward, Color.red), Is.False);
                Assert.That(paint.PaintCount, Is.Zero, "A view event cannot manufacture an approved Host hit.");
                yield return null;
                AssertTextureMatches(network.PaintSurface, paint);
                Assert.That(network.RestartDeveloperSolo(), Is.True);
                yield return null;
                Assert.That(paint.PaintCount, Is.Zero);
                network.Stop();
                yield return null;
                Assert.That(network.Snapshot, Is.Null);
                Assert.That(paint.PaintCount, Is.Zero);
                network.Host();
                Assert.That(network.IsHost, Is.True, network.Notice);
                Assert.That(network.Snapshot, Is.Null);
                yield return null;
                Assert.That(paint.PaintCount, Is.Zero);
            }
            finally { if (network.IsConnected) network.Stop(); }
        }

        [UnityTest]
        public IEnumerator SavedSoloHostElementHitsReachHpZeroWithoutWinningAndSurviveVisualReinitialization()
        {
            yield return this.fixture.Load();
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            LabNetwork network = paint.network;
            var controller = Object.FindFirstObjectByType<LabSceneController>();
            var hud = Object.FindFirstObjectByType<LabHud>();
            LabConfig savedNetworkConfig = network.config;
            LabConfig savedBoardConfig = controller.config;
            LabConfig fixture = CollisionFixture(savedNetworkConfig, 2f);
            float[] tuning = fixture.CaptureDeveloperValues();
            string error;
            network.config = fixture;
            controller.config = fixture;
            controller.ReconfigureBoard();
            this.fixture.UseAvailablePort(network);
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
                    if (index == 1) paint.enabled = false;
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
                    if (index == 2)
                    {
                        Assert.That(paint.IsReady, Is.False, "This actual Host hit occurred while the visual was disabled.");
                        Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(2));
                        Assert.That(network.PauseBattle(), Is.True, "Freeze pending growth while the visual is rebuilt.");
                        float retainedCoverage = network.PaintSurface.Coverage;
                        Color32[] retainedMap = CopyPixels(network.PaintSurface);
                        paint.enabled = true;
                        yield return null;
                        Assert.That(paint.IsReady, Is.True, paint.InitializationError);
                        Assert.That(paint.PaintCount, Is.EqualTo(2));
                        Assert.That(paint.Coverage, Is.EqualTo(retainedCoverage));
                        Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(retainedMap),
                            "Re-enabling uploads the authoritative map instead of erasing or replaying it.");
                    }
                    AssertTextureMatches(network.PaintSurface, paint);
                    if (network.IsBattlePaused) Assert.That(network.ResumeBattle(), Is.True);
                    yield return null;
                    Assert.That(paint.PaintCount, Is.EqualTo(index), "The mark survives the next frame.");
                    if (index == 1)
                    {
                        Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                        Assert.That(hud.resultControls.activeSelf, Is.False);
                        paint.ClearPaint();
                        Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(1),
                            "A connected presentation clear cannot erase Host-owned progress.");
                        AssertTextureMatches(network.PaintSurface, paint);
                        CaptureBattle(network, "approved-element-hit.png");
                    }
                }
                Assert.That(approvedDamage, Is.All.EqualTo(20));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing),
                    "Paint mode cannot win just because the old HP counter reached zero.");
                Assert.That(network.Snapshot.paintCoverage, Is.GreaterThan(0f));
                Assert.That(network.Snapshot.paintGoalPercent, Is.EqualTo(100));
                Assert.That(network.Snapshot.paintCoverage, Is.LessThan(1f));
                Assert.That(LabGourdFinish.DelayVictory(network, network.Snapshot), Is.False);
                Assert.That(hud.resultControls.activeSelf, Is.False);
                Assert.That(paint.VisualInstance.activeSelf, Is.True);
                Assert.That(network.target.VisualMount.Find("Gourd Left Half"), Is.Null);
                Assert.That(network.target.VisualMount.Find("Gourd Right Half"), Is.Null);
                Assert.That(network.target.VisualMount.Find("Small Yokai Spirit"), Is.Null);
                CaptureBattle(network, "hp-zero-paint-still-playing.png");
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
                Assert.That(paint.PaintCount, Is.Zero, "A presentation event cannot manufacture a connected Host mark.");
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
        public IEnumerator FixedHundredPercentGoalGrowsFromRepeatedRealHitsFreezesOnPauseAndResetsOnRetry()
        {
            yield return this.fixture.Load();
            LabGourdPaint paint = Object.FindFirstObjectByType<LabGourdPaint>();
            LabNetwork network = paint.network;
            LabSceneController controller = Object.FindFirstObjectByType<LabSceneController>();
            LabHud hud = Object.FindFirstObjectByType<LabHud>();
            LabDeveloperMode developer = SavedDeveloperUi();
            LabConfig savedNetwork = network.config, savedBoard = controller.config;
            LabConfig fixture = CollisionFixture(savedNetwork, 50f);
            float[] growthValues = fixture.CaptureDeveloperValues();
            growthValues[106] = 50f;
            growthValues[107] = 100f;
            growthValues[108] = 100f;
            Assert.That(fixture.TryApplyDeveloperValues(growthValues, out string fixtureError), Is.True, fixtureError);
            network.config = fixture;
            controller.config = fixture;
            controller.ReconfigureBoard();
            this.fixture.UseAvailablePort(network);
            System.Action<int> onHit = null;
            try
            {
                // Use the saved DEV form; slot 87 remains the fixed 100% objective.
                developer.Open();
                float[] initialValues = fixture.CaptureDeveloperValues();
                for (int i = 0; i < initialValues.Length; i++)
                    developer.valueInputs[i].text = initialValues[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                developer.ApplyAndStart();
                yield return null;
                Assert.That(network.IsDeveloperSolo, Is.True, developer.feedbackText.text);
                Assert.That(network.Snapshot.paintGoalPercent, Is.EqualTo(100));
                Assert.That(network.config.PaintVictoryPercent, Is.EqualTo(100));
                Assert.That(network.PaintSurface.Columns, Is.EqualTo(64));
                Assert.That(network.PaintSurface.Rows, Is.EqualTo(32));
                UnityEngine.UI.Text title = hud.hpText.transform.parent.Find("TitleMask/Title")
                    .GetComponent<UnityEngine.UI.Text>();
                Assert.That(title.text, Is.EqualTo("GOAL"));

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int confirmedHits = 0;
                    bool pausedOnSecondHit = false;
                    onHit = _ =>
                    {
                        confirmedHits++;
                        // Pause in the approval callback, before a slow rendered frame can consume
                        // the repeated hit's half-second growth budget.
                        if (confirmedHits == 2) pausedOnSecondHit = network.PauseBattle();
                    };
                    network.LocalThrowHit += onHit;
                    network.Generate();
                    LabOrbState box = network.Snapshot.orbs.Single();
                    TapOrb(network.board, box.id, 100d + attempt * 40d);
                    TapOrb(network.board, box.id, 100.2d + attempt * 40d);
                    string[] shots = network.Snapshot.orbs.Where(orb => orb.kind == LabOrbKind.Combined)
                        .Select(orb => orb.id).ToArray();
                    Assert.That(shots.Length, Is.EqualTo(2));
                    Color32[] firstMap = null;
                    for (int shotIndex = 0; shotIndex < shots.Length; shotIndex++)
                    {
                        network.target.GetComponent<LabHangingTarget>().ResetSwing();
                        LaunchThroughSavedBoard(network.board, shots[shotIndex], 110d + attempt * 40d + shotIndex * 10d);
                        double hitDeadline = Time.realtimeSinceStartupAsDouble + 5d;
                        while (confirmedHits < shotIndex + 1 && Time.realtimeSinceStartupAsDouble < hitDeadline)
                            yield return null;
                        Assert.That(confirmedHits, Is.EqualTo(shotIndex + 1), "An actual Host collision must approve each hit.");
                        Assert.That(network.LastShotReport, Does.StartWith("SHOT HIT"));
                        Assert.That(network.Snapshot.orbs.Any(orb => orb.id == shots[shotIndex]), Is.False);
                        if (shotIndex == 0)
                        {
                            Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                            Assert.That(network.Snapshot.hp, Is.EqualTo(20));
                            Assert.That(network.PaintSurface.Coverage, Is.EqualTo(.5f));
                            Assert.That(hud.resultControls.activeSelf, Is.False);
                            firstMap = CopyPixels(network.PaintSurface);
                        }
                    }
                    Assert.That(pausedOnSecondHit, Is.True);
                    Assert.That(network.IsBattlePaused, Is.True);
                    Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing),
                        "A repeated hit buys timed growth and cannot complete its region immediately.");
                    Assert.That(network.Snapshot.hp, Is.Zero, "Diagnostic HP alone cannot decide the Paint result.");
                    Assert.That(network.PaintSurface.CoveredCount, Is.LessThan(network.PaintSurface.CellCount));
                    Color32[] frozenMap = CopyPixels(network.PaintSurface);
                    for (int i = 0; i < firstMap.Length; i++)
                        if (firstMap[i].a != 0) Assert.That(frozenMap[i], Is.EqualTo(firstMap[i]),
                            "A differently colored repeated hit cannot recolor existing cells.");
                    uint frozenRevision = network.PaintSurface.Revision;
                    int frozenCount = network.PaintSurface.CoveredCount;
                    double frozenRemaining = network.Snapshot.remaining;
                    double frozenPlayed = network.Snapshot.playedSeconds;
                    yield return new WaitForSecondsRealtime(.75f);
                    Assert.That(network.PaintSurface.Revision, Is.EqualTo(frozenRevision));
                    Assert.That(network.PaintSurface.CoveredCount, Is.EqualTo(frozenCount));
                    Assert.That(CopyPixels(network.PaintSurface), Is.EqualTo(frozenMap));
                    Assert.That(network.Snapshot.remaining, Is.EqualTo(frozenRemaining));
                    Assert.That(network.Snapshot.playedSeconds, Is.EqualTo(frozenPlayed));
                    Assert.That(network.ResumeBattle(), Is.True);
                    double growthDeadline = Time.realtimeSinceStartupAsDouble + 3d;
                    while (network.Snapshot.phase == LabPhase.Playing && Time.realtimeSinceStartupAsDouble < growthDeadline)
                        yield return null;
                    Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Victory));
                    Assert.That(network.Snapshot.paintGoalPercent, Is.EqualTo(100));
                    Assert.That(network.Snapshot.paintCoverage, Is.EqualTo(1f));
                    Assert.That(network.Snapshot.paintCoveredCount, Is.EqualTo(network.Snapshot.paintCellCount));
                    Assert.That(paint.PaintCount, Is.EqualTo(2));
                    Assert.That(network.Snapshot.players.Single().hitTotal, Is.EqualTo(2));
                    Assert.That(network.Snapshot.players.Single().lastHit, Is.True);
                    yield return null;
                    AssertTextureMatches(network.PaintSurface, paint);
                    Assert.That(hud.resultControls.activeInHierarchy, Is.True);
                    Assert.That(hud.resultStatusText.text, Is.EqualTo("VICTORY"));
                    Assert.That(hud.hpText.text, Is.EqualTo(LabHud.FormatPaintPercentage(network.Snapshot)));
                    Assert.That(hud.yokaiHpFill.fillAmount, Is.EqualTo(1f));
                    Assert.That(LabGourdFinish.DelayVictory(network, network.Snapshot), Is.False);
                    CaptureBattle(network, "growth-hundred-percent-victory.png");
                    network.LocalThrowHit -= onHit;
                    onHit = null;
                    string finishedSession = network.Snapshot.sessionId;
                    if (attempt == 0)
                    {
                        Assert.That(hud.resultRestartButton.interactable, Is.True);
                        hud.resultRestartButton.onClick.Invoke();
                        yield return null;
                        Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(finishedSession));
                        Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                        Assert.That(network.Snapshot.hp, Is.EqualTo(40));
                        Assert.That(network.Snapshot.paintGoalPercent, Is.EqualTo(100));
                        Assert.That(network.Snapshot.orbs, Is.Empty);
                        Assert.That(paint.PaintCount, Is.Zero);
                        Assert.That(network.Snapshot.paintCoverage, Is.Zero);
                        Assert.That(hud.resultControls.activeSelf, Is.False);
                    }
                    else
                    {
                        developer.Open();
                        SetInput(developer, "Paint Initial Coverage (%)", "2");
                        SetInput(developer, "Monster Max HP", "80");
                        developer.ApplyAndStart();
                        yield return null;
                        Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(finishedSession));
                        Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                        Assert.That(network.Snapshot.paintGoalPercent, Is.EqualTo(100));
                        Assert.That(network.config.PaintInitialPercent, Is.EqualTo(2f));
                        Assert.That(network.Snapshot.hp, Is.EqualTo(80));
                        Assert.That(paint.PaintCount, Is.Zero, "DEV restart clears a nonempty prior authoritative map.");
                        Assert.That(paint.PaintTexture.GetPixels32().All(pixel => pixel.a == 0), Is.True);
                    }
                }
            }
            finally
            {
                if (onHit != null) network.LocalThrowHit -= onHit;
                if (network.IsConnected) network.Stop();
                network.config = savedNetwork;
                controller.config = savedBoard;
                controller.ReconfigureBoard();
                Object.Destroy(fixture);
            }
        }

        [UnityTest]
        public IEnumerator DisabledPaintDoesNotListenAndMissingReferencesKeepTargetVisible()
        {
            yield return this.fixture.Load();
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
            yield return this.fixture.Load();
            LabDeveloperMode ui = SavedDeveloperUi();
            LabNetwork network = ui.network;
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            LabGourdCrack crack = network.target.GetComponent<LabGourdCrack>();
            LabGourdFinish finish = network.target.GetComponent<LabGourdFinish>();
            float[] savedValues = ui.sourceConfig.CaptureDeveloperValues();
            this.fixture.UseAvailablePort(network);
            int modeIndex = FieldIndex("Gourd Mode (0 paint, 1 crack)");
            try
            {
                Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
                Assert.That(paint.IsReady, Is.True, paint.InitializationError);
                LabHud savedHud = Object.FindFirstObjectByType<LabHud>();
                UnityEngine.UI.Text title = savedHud.hpText.transform.parent.Find("TitleMask/Title")
                    .GetComponent<UnityEngine.UI.Text>();
                Assert.That(title.text, Is.EqualTo("GOAL"));
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
                Assert.That(title.text, Is.EqualTo("YOKAI HP"));
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
                Assert.That(title.text, Is.EqualTo("GOAL"));
                Assert.That(intact == null, Is.True, "Crack's owned model must be released before paint mounts.");
                Assert.That(paint.PaintCount, Is.Zero);
                network.target.NotifyApprovedHit(Vector3.forward, LabOrbElement.Water);
                Assert.That(paint.PaintCount, Is.Zero, "Connected direct events only refresh the accepted map.");
                GameObject newWhite = paint.VisualInstance;
                paint.ApplyMode();
                Assert.That(paint.InitializePresentation(), Is.True);
                Assert.That(paint.VisualInstance, Is.SameAs(newWhite));
                Assert.That(paint.PaintCount, Is.Zero, "Idempotent initialization cannot inject a mark into the round.");

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
            yield return this.fixture.Load();
            LabDeveloperMode ui = SavedDeveloperUi();
            LabNetwork network = ui.network;
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            LabGourdCrack crack = network.target.GetComponent<LabGourdCrack>();
            float[] savedValues = ui.sourceConfig.CaptureDeveloperValues();
            this.fixture.UseAvailablePort(network);
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
            yield return this.fixture.Load();
            LabDeveloperMode ui = SavedDeveloperUi();
            LabNetwork network = ui.network;
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            LabGourdCrack crack = network.target.GetComponent<LabGourdCrack>();
            GameObject savedWhite = paint.visualPrefab;
            GameObject savedCrack = crack.gourdPrefab;
            var victory = new LabSnapshot { sessionId = "invalid-mode", round = 1, phase = LabPhase.Victory, hp = 0 };
            this.fixture.UseAvailablePort(network);
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

        static LabConfig CollisionFixture(LabConfig source, float initialPercent = 2f)
        {
            LabConfig fixture = Object.Instantiate(source);
            float[] tuning = fixture.CaptureDeveloperValues();
            tuning[104] = 0f;
            tuning[105] = initialPercent;
            tuning[106] = 4f;
            tuning[107] = 6f;
            tuning[108] = 60f;
            tuning[109] = 1.35f;
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
            // The gourd's sabotage bombs would add unplanned orbs to the boards checked here.
            SetTuning(tuning, "Gourd Bomb Interval Min (seconds, 0 = off)", 0f);
            SetTuning(tuning, "Gourd Bomb Interval Max (seconds, 0 = off)", 0f);
            Assert.That(fixture.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            return fixture;
        }

        static Color32[] CopyPixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount];
            surface.CopyPixels(pixels);
            return pixels;
        }

        static void AssertTextureMatches(LabPaintSurface surface, LabGourdPaint paint)
        {
            Assert.That(paint.PaintTexture, Is.Not.Null);
            Assert.That(paint.PaintTexture.width, Is.EqualTo(surface.Columns));
            Assert.That(paint.PaintTexture.height, Is.EqualTo(surface.Rows));
            Color32[] expected = CopyPixels(surface);
            Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(expected),
                "The renderer must upload exactly the CPU RGBA surface that owns coverage.");
            Assert.That(expected.Count(pixel => pixel.a != 0), Is.EqualTo(surface.CoveredCount));
            Assert.That(surface.Coverage, Is.EqualTo(surface.CoveredCount / (float)surface.CellCount));
            Assert.That(ReadBlock(paint).GetTexture("_PaintMap"), Is.SameAs(paint.PaintTexture));
            Assert.That(ReadBlock(paint).GetFloat("_PaintMapReady"), Is.EqualTo(1f));
        }

        static void AssertPixelsClose(Color32[] expected, Color32[] actual, int tolerance, string message)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                if (Mathf.Abs(actual[i].r - expected[i].r) > tolerance
                    || Mathf.Abs(actual[i].g - expected[i].g) > tolerance
                    || Mathf.Abs(actual[i].b - expected[i].b) > tolerance
                    || Mathf.Abs(actual[i].a - expected[i].a) > tolerance)
                    Assert.Fail(message + " Pixel " + i + ": " + actual[i] + " / " + expected[i]);
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
            const string directory = "Library/Issue187Captures";
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
