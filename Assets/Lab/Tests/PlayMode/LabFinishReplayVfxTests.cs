using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Resource-backed replay drawing without sockets, model ticks or a forged projectile contact.</summary>
    [Category("GPU")]
    public sealed class LabFinishReplayVfxTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene scene, previousScene;
        private GameObject root;
        private LabConfig config;
        private LabNetwork network;
        private LabTarget target;
        private LabHangingTarget hanging;
        private LabFinishReplay replay;
        private LabFiveElementVfx shared;
        private LabFiveElementVfx.Flight liveFlight;
        private LabModel model;
        private Camera camera;
        private string originalModel;

        [SetUp]
        public void Prepare()
        {
            Assert.That(Resources.Load<Shader>("FiveElements/LabWetPigment").isSupported, Is.True,
                "Run the real clay presentation fixture with graphics enabled.");
            previousScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Replay clay fixture");
            SceneManager.SetActiveScene(scene);
            root = new GameObject("Replay clay fixture");
            config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            network.config = config;
            target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 30f, 0f), .8f, 2f);
            hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.SetSimulationHeld(true);
            network.target = target;
            camera = new GameObject("Replay battle camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = target.transform.position + new Vector3(0f, .5f, -5f);
            camera.transform.LookAt(target.transform.position);
            typeof(LabNetwork).GetProperty("BattleCamera").SetValue(network, camera);
            model = new LabModel();
            model.BeginSolo("replay-vfx", 1ul, 0d, config);
            typeof(LabNetwork).GetField("model", Private).SetValue(network, model);
            var snapshot = model.Snapshot(2d);
            snapshot.phase = LabPhase.Victory;
            typeof(LabNetwork).GetField("latest", Private).SetValue(network, snapshot);
            originalModel = JsonUtility.ToJson(model.Snapshot(2d));
            shared = LabFiveElementVfx.ForScene(scene);
            shared.MarkLifetimeOverride = 1.23f;
            liveFlight = shared.BeginFlight(LabOrbElement.Water, Vector3.one * 90f, Vector3.forward, .3f);
            Assert.That(liveFlight.IsActive, Is.True);
            replay = LabFinishReplay.Attach(network);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (replay != null) replay.ResetPresentation();
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [TestCase(LabOrbElement.Fire, false)]
        [TestCase(LabOrbElement.Water, false)]
        [TestCase(LabOrbElement.Wood, false)]
        [TestCase(LabOrbElement.Metal, false)]
        [TestCase(LabOrbElement.Earth, false)]
        [TestCase(LabOrbElement.None, true)]
        public void GhostAndContactUseCurrentClayWithoutChangingPhysicsOrTheLivePool(LabOrbElement element, bool fiveColor)
        {
            LabFinishReplayShot shot = Shot(element, fiveColor);
            Assert.That(replay.Begin(shot), Is.True);
            replay.Sample(Elapsed(shot, 0, -.2f));
            LabProjectile ghost = replay.Ghost;
            Assert.That(ghost, Is.Not.Null);
            Assert.That(ghost.ClayFlightActive, Is.True);
            Assert.That(ghost.VisualMount.gameObject.activeSelf, Is.False);
            Assert.That(ghost.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(ghost.GetComponent<Rigidbody>().detectCollisions, Is.False);
            Assert.That(ghost.GetComponent<SphereCollider>().enabled, Is.False);
            Assert.That(ghost.IsResolved, Is.False);
            Assert.That(ghost.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
            Assert.That(ghost.GetComponentsInChildren<TrailRenderer>(true).Any(t => t.emitting), Is.False);
            var pool = replay.ClayVfx;
            Assert.That(pool, Is.Not.SameAs(shared));
            Assert.That(pool.ManualClock, Is.True);
            Transform flight = pool.transform.Find("Flight 0");
            MeshRenderer ball = flight.Find("Wet clay").GetComponent<MeshRenderer>();
            Assert.That(ball.sharedMaterial, Is.SameAs(pool.Profile.OrbSurface(element, fiveColor)));
            Assert.That(ball.transform.localScale.x * ball.transform.localScale.y * ball.transform.localScale.z,
                Is.EqualTo(Mathf.Pow(ghost.FlightDiameter, 3f)).Within(.00001f), "Drawn minimum size remains a flight-only rule.");
            if (fiveColor)
            {
                var tint = new MaterialPropertyBlock();
                ball.GetPropertyBlock(tint);
                Assert.That(Vector4.Distance(tint.GetColor("_BaseColor"), LabFiveColorSparkle.ColorAt(.3f)), Is.LessThan(.001f));
            }
            replay.Sample(Elapsed(shot, 0, .2f));
            Assert.That(pool.ActiveFlightCount, Is.Zero);
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(1));
            Transform impact = pool.transform.Find("Impact 0");
            Assert.That(impact.localScale.x, Is.EqualTo(shot.Radius * 2f).Within(.00001f),
                "The contact uses the recorded actual radius, not the minimum visible flight size.");
            Assert.That(impact.Find("Wet surface mark").GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(impact.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            Assert.That(impact.Find("Element release"), Is.Null);
            Assert.That(pool.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(pool.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            AssertIndependentAndUnchanged();
        }

        [UnityTest]
        public IEnumerator ThreeCutsClearPreviousClayAndResetRestoresTheBattle()
        {
            LabFinishReplayShot shot = Shot(LabOrbElement.None, true);
            Assert.That(replay.Begin(shot), Is.True);
            var pool = replay.ClayVfx;
            for (int pass = 0; pass < 3; pass++)
            {
                replay.Sample(Elapsed(shot, pass, -.05f));
                Assert.That(replay.CurrentPass, Is.EqualTo(pass));
                Assert.That(pool.ActiveFlightCount, Is.EqualTo(1));
                Assert.That(pool.ActiveImpactCount, Is.Zero, "A camera cut retires the old contact immediately.");
                replay.Sample(Elapsed(shot, pass, .2f));
                Assert.That(replay.ImpactsPresented, Is.EqualTo(pass + 1));
                Assert.That(pool.ActiveFlightCount, Is.Zero);
                Assert.That(pool.ActiveImpactCount, Is.EqualTo(1));
                AssertIndependentAndUnchanged();
            }
            replay.ResetPresentation();
            Assert.That(pool.ActiveFlightCount + pool.ActiveImpactCount, Is.Zero);
            Assert.That(pool.gameObject.activeInHierarchy, Is.False);
            Assert.That(hanging.PresentationPoseActive, Is.False);
            Assert.That(camera.enabled, Is.True);
            AssertIndependentAndUnchanged();
            yield return null;
            Assert.That(pool == null, Is.True, "Owned clay and its runtime materials leave with the replay.");
        }

        [UnityTest]
        public IEnumerator SampledClayFollowsTheDrawnPoseAndFreezesOnRepeatedSamplesAndSkip()
        {
            LabFinishReplayShot shot = Shot(LabOrbElement.Earth, false);
            Matrix4x4 fixedHitbox = target.Hitbox.transform.localToWorldMatrix;
            Assert.That(replay.Begin(shot), Is.True);
            replay.Sample(Elapsed(shot, 0, .05f));
            replay.Sample(Elapsed(shot, 0, .2f));
            Transform impact = replay.ClayVfx.transform.Find("Impact 0");
            Vector3 expected = hanging.MovePosedPoint(shot.HitPoint, shot.PoseAt(0f), shot.PoseAt(.2f));
            Assert.That(Vector3.Distance(impact.position, expected), Is.LessThan(.0001f));
            Assert.That(target.Hitbox.transform.localToWorldMatrix, Is.EqualTo(fixedHitbox),
                "The render-only pose never moves the collider to follow the clay.");
            Matrix4x4[] before = Geometry(impact);
            replay.Sample(Elapsed(shot, 0, .2f));
            Assert.That(Geometry(impact), Is.EqualTo(before));
            Assert.That(replay.ImpactsPresented, Is.EqualTo(1));
            replay.Skip();
            replay.Sample(LabFinishReplay.Duration);
            yield return null;
            yield return null;
            Assert.That(replay.Skipped, Is.True);
            Assert.That(Geometry(impact), Is.EqualTo(before), "A skipped replay retains its sampled frame until covered.");
            AssertIndependentAndUnchanged();
        }

        [Test]
        public void LateContactSampleMatchesIncrementalReplayTime()
        {
            // Cover the visible squash as well as the later spread and thrown drops.
            foreach (float time in new[] { .05f, .2f }) AssertLateContactSample(time);
        }

        private void AssertLateContactSample(float sampleTime)
        {
            replay.ResetPresentation();
            LabFinishReplayShot shot = Shot(LabOrbElement.None, true);
            Assert.That(replay.Begin(shot), Is.True);
            for (float time = -.45f; time < sampleTime; time += .025f) replay.Sample(Elapsed(shot, 0, time));
            replay.Sample(Elapsed(shot, 0, sampleTime));
            Transform impact = replay.ClayVfx.transform.Find("Impact 0");
            MeshRenderer[] incrementalParts = impact.GetComponentsInChildren<MeshRenderer>(true);
            bool[] incrementalEnabled = incrementalParts.Select(r => r.enabled).ToArray();
            string[] visibleParts = VisibleParts(impact).Select(r => r.name).ToArray();
            Matrix4x4[] incremental = VisibleParts(impact).Select(r => r.transform.localToWorldMatrix).ToArray();
            Color[] incrementalColors = VisibleParts(impact).Select(BaseColor).ToArray();
            Assert.That(visibleParts, Does.Contain(sampleTime < .1f ? "Contact squash" : "Wet surface mark"));
            replay.ResetPresentation();
            Assert.That(replay.Begin(shot), Is.True);
            replay.Sample(Elapsed(shot, 0, sampleTime));
            impact = replay.ClayVfx.transform.Find("Impact 0");
            Assert.That(impact.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.enabled), Is.EqualTo(incrementalEnabled),
                "A late sample must show exactly the same contact layers at the same replay age.");
            Assert.That(VisibleParts(impact).Select(r => r.name), Is.EqualTo(visibleParts));
            // Disabled squash/drop transforms retain their last visible state; only enabled geometry is drawn.
            Matrix4x4[] late = VisibleParts(impact).Select(r => r.transform.localToWorldMatrix).ToArray();
            for (int i = 0; i < late.Length; i++)
                for (int component = 0; component < 16; component++)
                    Assert.That(late[i][component], Is.EqualTo(incremental[i][component]).Within(.0001f), visibleParts[i]);
            Color[] lateColors = VisibleParts(impact).Select(BaseColor).ToArray();
            for (int i = 0; i < lateColors.Length; i++)
                Assert.That(Vector4.Distance(lateColors[i], incrementalColors[i]), Is.LessThan(.001f), visibleParts[i]);
            Assert.That(Vector4.Distance(BaseColor(impact.Find("Contact squash").GetComponent<MeshRenderer>()),
                LabFiveColorSparkle.ColorAt(shot.FlightSeconds + sampleTime)), Is.LessThan(.001f),
                "The contact palette advances from the recorded flight age on the replay clock.");
            Assert.That(replay.ImpactsPresented, Is.EqualTo(1), "Late sampling does not replay an authority contact.");
            AssertIndependentAndUnchanged();
        }

        private static MeshRenderer[] VisibleParts(Transform impact) => impact.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();

        private static Color BaseColor(MeshRenderer renderer)
        {
            var tint = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(tint);
            return tint.GetColor("_BaseColor");
        }

        private void AssertIndependentAndUnchanged()
        {
            Assert.That(LabFiveElementVfx.ForScene(scene), Is.SameAs(shared));
            Assert.That(shared.ManualClock, Is.False);
            Assert.That(shared.MarkLifetimeOverride, Is.EqualTo(1.23f));
            Assert.That(liveFlight.IsActive, Is.True);
            Assert.That(shared.ActiveFlightCount, Is.EqualTo(1));
            Assert.That(shared.ActiveImpactCount, Is.Zero);
            Assert.That(JsonUtility.ToJson(model.Snapshot(2d)), Is.EqualTo(originalModel));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        private LabFinishReplayShot Shot(LabOrbElement element, bool fiveColor)
        {
            var poses = new LabHangingTarget.RigState[34];
            for (int i = 0; i < poses.Length; i++)
            {
                float time = -.8f + i / LabFinishReplayShot.PoseRate;
                poses[i] = hanging.FixedPose;
                poses[i].Swing += new Vector2(5f, -12f) * time;
                poses[i].Yaw += 40f * time;
            }
            Vector3 point = target.Hitbox.bounds.center + Vector3.back * .8f;
            return new LabFinishReplayShot
            {
                Owner = 1ul, Slot = 1, Element = element, FiveColor = fiveColor,
                BlastScale = .5f, RadiusScale = .2f, Radius = config.ThrowRadius * .1f,
                FlightSeconds = .5f, LaunchPosition = point + Vector3.back * 4f,
                LaunchVelocity = new Vector3(0f, 4f, 8f), Gravity = 30f,
                HitPoint = point, HitNormal = Vector3.back, ImpactVelocity = Vector3.forward * 10f,
                PoseStart = -.8f, Poses = poses
            };
        }

        private static double Elapsed(LabFinishReplayShot shot, int pass, float time)
        {
            float before = 0f;
            for (int i = 0; i < pass; i++) before += LabFinishReplayTimeline.Passes[i].RealSeconds;
            LabFinishReplayTimeline.Pass fitted = LabFinishReplayTimeline.Passes[pass].Fitted(shot.FlightSeconds);
            return before + (time - fitted.From) / fitted.Speed;
        }

        private static Matrix4x4[] Geometry(Transform impact) => impact.GetComponentsInChildren<MeshRenderer>(true)
            .Select(renderer => renderer.transform.localToWorldMatrix).ToArray();
    }
}
