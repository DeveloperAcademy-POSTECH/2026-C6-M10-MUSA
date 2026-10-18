using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Experiments.Tests
{
    /// <summary>
    /// Opt-in experiment checks. The completed source uses the saved board's actual Host
    /// projectile collision with an explicit low-resolution/100%-initial DEV fixture.
    /// </summary>
    public sealed class GourdResultViewerPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GourdResultViewerExperiment.StopInCurrentScene();
            LabNetwork network = Object.FindFirstObjectByType<LabNetwork>();
            if (network != null && network.IsConnected) network.Stop();
            yield return null;
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Gourd viewer experiment test cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator DefaultLabDoesNotCreateTheOptInExperimentOrItsUi()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            Assert.That(Object.FindFirstObjectByType<LabNetwork>(), Is.Not.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<GourdResultViewerExperiment>()
                .Where(item => item.gameObject.scene == SceneManager.GetActiveScene()), Is.Empty,
                "The test runner disables and preserves the Editor opt-in SessionState flag.");
            Assert.That(Resources.FindObjectsOfTypeAll<GourdResultViewerUI>()
                .Where(item => item.gameObject.scene == SceneManager.GetActiveScene()), Is.Empty,
                "The normal Scene does not include the experimental result entry/modal.");
        }

        [UnityTest]
        public IEnumerator ActualPaintVictoryCanBeInspectedLocallyAndRetryClosesTheViewer()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabGourdPaint paint = Object.FindFirstObjectByType<LabGourdPaint>();
            LabSceneController controller = Object.FindFirstObjectByType<LabSceneController>();
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            LabNetwork network = paint.network;
            LabConfig savedNetwork = network.config, savedBoard = controller.config;
            LabConfig fixture = CollisionFixture(savedNetwork);
            network.config = fixture;
            controller.config = fixture;
            controller.ReconfigureBoard();
            network.port = 17891;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                yield return null;
                GourdResultViewerExperiment experiment = GourdResultViewerExperiment.StartInCurrentScene();
                Assert.That(experiment, Is.Not.Null, "The saved experiment UI prefab is required.");
                Assert.That(experiment.Ui.HasRequiredReferences, Is.True);
                Assert.That(GourdResultViewerExperiment.StartInCurrentScene(), Is.SameAs(experiment),
                    "Repeated opt-in cannot duplicate the controller or UI.");
                Assert.That(experiment.Network, Is.SameAs(network));
                Assert.That(experiment.OpenViewer(), Is.False, "A Playing round cannot be inspected.");
                Assert.That(experiment.Ui.entryButton.gameObject.activeSelf, Is.False);
                Assert.That(experiment.Ui.viewerPanel.activeSelf, Is.False);

                network.Generate();
                LabOrbState box = network.Snapshot.orbs.Single();
                Assert.That(box.kind, Is.EqualTo(LabOrbKind.SecretBox));
                Tap(network.board, box.id, 10d);
                Tap(network.board, box.id, 10.2d);
                string shot = network.Snapshot.orbs.First(orb => orb.kind == LabOrbKind.Combined).id;
                network.target.GetComponent<LabHangingTarget>().ResetSwing();
                Launch(network.board, shot, 20d);
                double deadline = Time.realtimeSinceStartupAsDouble + 6d;
                while (network.Snapshot.phase == LabPhase.Playing && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(network.LastShotReport, Does.StartWith("SHOT HIT"), "The result comes from a real Host collision.");
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Victory));
                Assert.That(network.Snapshot.paintCoveredCount, Is.EqualTo(network.Snapshot.paintCellCount));
                Assert.That(network.PaintSurface.IsComplete, Is.True);
                Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(1));
                experiment.RefreshState();
                Assert.That(experiment.Ui.entryButton.gameObject.activeSelf, Is.True);

                Color32[] originalPixels = Pixels(network.PaintSurface);
                string originalHash = LabPaintPublisher.Hash(originalPixels);
                uint revision = network.PaintSurface.Revision;
                int paintCount = network.PaintSurface.PaintCount;
                LabSnapshot completed = network.Snapshot;
                float[] savedTuning = savedNetwork.CaptureDeveloperValues();
                Quaternion liveRotation = paint.VisualInstance.transform.localRotation;
                int cameraCount = SceneCameraCount();
                Material liveMaterial = paint.Shell.GetComponent<Renderer>().sharedMaterial;

                experiment.Ui.entryButton.onClick.Invoke();
                Assert.That(experiment.IsOpen, Is.True, experiment.LastError);
                Assert.That(experiment.Ui.IsShowing, Is.True);
                Assert.That(experiment.Ui.entryButton.gameObject.activeSelf, Is.False);
                LabGourdResultPreview preview = experiment.Preview;
                RenderTexture firstTexture = preview.Texture;
                Texture2D firstPaint = preview.PaintTexture;
                Assert.That(experiment.Ui.previewImage.texture, Is.SameAs(firstTexture));
                Assert.That(firstTexture.IsCreated(), Is.True);
                Assert.That(preview.PaintTexture, Is.Not.SameAs(paint.PaintTexture));
                Assert.That(preview.PaintTexture.GetPixels32(), Is.EqualTo(originalPixels));
                Assert.That(preview.PaintPixelHash, Is.EqualTo(originalHash));
                Assert.That(preview.ModelRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(preview.ModelRoot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(preview.ModelRoot.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(preview.PreviewCamera.targetTexture, Is.SameAs(firstTexture));
                Assert.That(preview.ModelRoot.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials).Any(material => material == liveMaterial), Is.False,
                    "The preview owns material copies instead of modifying the live target materials.");

                Quaternion initialRotation = preview.ModelRotation;
                float initialZoom = preview.ZoomNormalized;
                experiment.Rotate(new Vector2(.23f, -.13f));
                experiment.Zoom(.2f);
                Assert.That(Quaternion.Angle(initialRotation, preview.ModelRotation), Is.GreaterThan(1f));
                Assert.That(preview.ZoomNormalized, Is.GreaterThan(initialZoom));
                Assert.That(paint.VisualInstance.transform.localRotation, Is.EqualTo(liveRotation),
                    "Viewer input rotates only the copied model.");
                experiment.Ui.resetButton.onClick.Invoke();
                Assert.That(Quaternion.Angle(initialRotation, preview.ModelRotation), Is.LessThan(.001f));
                Assert.That(preview.ZoomNormalized, Is.EqualTo(initialZoom).Within(.00001f));
                yield return null;
                yield return null;
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                    AssertRenderedModel(firstTexture);
                AssertUnchangedResult(network, originalPixels, revision, paintCount, completed);
                Assert.That(savedNetwork.CaptureDeveloperValues(), Is.EqualTo(savedTuning));

                experiment.Ui.closeButton.onClick.Invoke();
                Assert.That(experiment.IsOpen, Is.False);
                Assert.That(preview.IsDisposed, Is.True);
                Assert.That(experiment.Ui.IsShowing, Is.False);
                Assert.That(experiment.Ui.previewImage.texture, Is.Null);
                yield return null;
                experiment.RefreshState();
                Assert.That(experiment.Ui.entryButton.gameObject.activeSelf, Is.True);
                Assert.That(firstTexture == null, Is.True, "Close releases the owned render texture.");
                Assert.That(firstPaint == null, Is.True, "Close releases the copied paint texture.");
                Assert.That(SceneCameraCount(), Is.EqualTo(cameraCount));

                for (int iteration = 0; iteration < 2; iteration++)
                {
                    Assert.That(experiment.OpenViewer(), Is.True, experiment.LastError);
                    Assert.That(experiment.Preview.PaintPixelHash, Is.EqualTo(originalHash));
                    LabGourdResultPreview repeated = experiment.Preview;
                    experiment.CloseViewer();
                    Assert.That(repeated.IsDisposed, Is.True);
                    yield return null;
                    experiment.RefreshState();
                    Assert.That(SceneCameraCount(), Is.EqualTo(cameraCount), "Repeated open/close cannot leak cameras.");
                }
                AssertUnchangedResult(network, originalPixels, revision, paintCount, completed);

                Assert.That(experiment.OpenViewer(), Is.True, experiment.LastError);
                LabGourdResultPreview beforeRetry = experiment.Preview;
                string session = network.Snapshot.sessionId;
                Assert.That(network.RestartDeveloperSolo(), Is.True, network.Notice);
                yield return null;
                experiment.RefreshState();
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(session));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(network.PaintSurface.CoveredCount, Is.Zero);
                Assert.That(experiment.IsOpen, Is.False);
                Assert.That(beforeRetry.IsDisposed, Is.True);
                Assert.That(experiment.Ui.IsShowing, Is.False);
                Assert.That(experiment.Ui.entryButton.gameObject.activeSelf, Is.False);
                Assert.That(experiment.Ui.previewImage.texture, Is.Null);
            }
            finally
            {
                GourdResultViewerExperiment.StopInCurrentScene();
                if (network.IsConnected) network.Stop();
                network.config = savedNetwork;
                controller.config = savedBoard;
                controller.ReconfigureBoard();
                Object.Destroy(fixture);
            }
            yield return null;
            Assert.That(Object.FindObjectsByType<GourdResultViewerExperiment>(FindObjectsSortMode.None), Is.Empty);
        }

        private static void AssertUnchangedResult(LabNetwork network, Color32[] pixels, uint revision,
            int paintCount, LabSnapshot result)
        {
            Assert.That(Pixels(network.PaintSurface), Is.EqualTo(pixels));
            Assert.That(network.PaintSurface.Revision, Is.EqualTo(revision));
            Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(paintCount));
            Assert.That(network.Snapshot.sessionId, Is.EqualTo(result.sessionId));
            Assert.That(network.Snapshot.round, Is.EqualTo(result.round));
            Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(network.Snapshot.hp, Is.EqualTo(result.hp));
            Assert.That(network.Snapshot.playedSeconds, Is.EqualTo(result.playedSeconds));
            Assert.That(network.Snapshot.players.Select(player => player.hitTotal),
                Is.EqualTo(result.players.Select(player => player.hitTotal)));
        }

        private static void AssertRenderedModel(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply(false, false);
                Color32[] image = readback.GetPixels32();
                Color32 background = image[0];
                Assert.That(image.Any(pixel => Mathf.Abs(pixel.r - background.r) > 12
                    || Mathf.Abs(pixel.g - background.g) > 12 || Mathf.Abs(pixel.b - background.b) > 12), Is.True,
                    "The independent camera must render the colored model, not only its solid background.");
            }
            finally { RenderTexture.active = previous; Object.Destroy(readback); }
        }

        private static int SceneCameraCount() => Resources.FindObjectsOfTypeAll<Camera>()
            .Count(camera => camera.gameObject.scene.IsValid());

        private static Color32[] Pixels(LabPaintSurface map)
        {
            var result = new Color32[map.CellCount];
            map.CopyPixels(result);
            return result;
        }

        private static void Tap(LabOrbBoard board, string id, double now)
        {
            Vector3 position = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == id).Body.position;
            var point = new Vector2(position.x, position.y);
            Assert.That(board.TryBeginDragAtWorld(point, now), Is.True);
            Assert.That(board.EndDragAtWorld(point, now + .05d), Is.True);
        }

        private static void Launch(LabOrbBoard board, string id, double now)
        {
            LabOrbView orb = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == id);
            Vector3 position = orb.Body.position;
            var from = new Vector2(position.x, position.y);
            Vector2 aim = board.AreaShape.ToWorld(new Vector2(.5f, .25f), orb.HitSphere.radius);
            Assert.That(board.TryBeginDragAtWorld(from, now), Is.True);
            Assert.That(board.DragToWorld(aim, now + 1d), Is.True);
            Assert.That(board.EndDragAtWorld(aim + Vector2.up * (board.CenterBounds.width * .2f), now + 1.04d), Is.True);
        }

        private static LabConfig CollisionFixture(LabConfig source)
        {
            LabConfig result = Object.Instantiate(source);
            float[] values = result.CaptureDeveloperValues();
            values[105] = 0; values[106] = 100; values[107] = 4; values[108] = 6;
            Set(values, "Gourd Mode (0 paint, 1 crack)", 0);
            Set(values, "Paint Victory Coverage (%)", 100);
            Set(values, "Monster Max HP", 40);
            Set(values, "Damage per Hit", 20);
            Set(values, "Battle Duration (seconds)", 30);
            Set(values, "Orb Generation Cost", 1);
            Set(values, "Item Lifetime (seconds)", 30);
            Set(values, "Item Unlock Swipes Min", 0);
            Set(values, "Item Unlock Swipes Max", 0);
            Set(values, "Yin Chance (%)", 0); Set(values, "Yang Chance (%)", 0);
            Set(values, "Relay Orb Chance (%)", 0); Set(values, "Secret Box Chance (%)", 100);
            Set(values, "Potion Chance (%)", 0); Set(values, "Bomb Chance (%)", 0);
            Set(values, "Spawn Pop Duration (seconds)", 0);
            Set(values, "Spawn Minimum Speed (board widths/s)", 0);
            Set(values, "Spawn Maximum Speed (board widths/s)", 0);
            Set(values, "Drum Drift Speed (board widths/s)", 0);
            Set(values, "Throw Origin Radius", 4); Set(values, "Throw Origin Height Offset", -.5f);
            Set(values, "Throw Minimum Upward Speed", 2); Set(values, "Throw Lateral Gain", 2.5f);
            Set(values, "Throw Upward Gain", 2.1f); Set(values, "Throw Forward Gain", 4.5f);
            Set(values, "Throw Maximum World Speed", 18); Set(values, "Throw Gravity", 9.81f);
            Set(values, "Throw Power Exponent", 1); Set(values, "Throw Loft Offset (degrees)", 0);
            Set(values, "Throw Air Damping", 0);
            Set(values, "Gourd Bomb Interval Min (seconds, 0 = off)", 0);
            Set(values, "Gourd Bomb Interval Max (seconds, 0 = off)", 0);
            Assert.That(result.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return result;
        }

        private static void Set(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(candidate => LabConfig.DeveloperFields[candidate].Label == label);
            values[index] = value;
        }
    }
}
