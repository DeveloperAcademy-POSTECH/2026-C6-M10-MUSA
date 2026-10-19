using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Presentation regressions use actual runtime effects and saved art profiles. These are not
    /// claims about iOS visual quality, mobile performance or independent network processes.
    /// </summary>
    public sealed class LabElementFlightTrailTests
    {
        private readonly List<Object> owned = new List<Object>();
        private LabNetwork startedNetwork;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (startedNetwork != null)
            {
                startedNetwork.Stop();
                Object.Destroy(startedNetwork.gameObject);
                startedNetwork = null;
            }
            foreach (Object item in owned)
                if (item != null) Object.Destroy(item);
            owned.Clear();
            yield return null;
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Element trail cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void SavedAppearanceConnectsFiveDistinctInkProfilesAndBothTextureRoles()
        {
            LabOrbAppearance appearance = SavedConfig().OrbAppearance;
            Assert.That(appearance, Is.Not.Null);
            LabOrbElement[] elements = Elements();
            LabElementTrailShape[] shapes = { LabElementTrailShape.Flame, LabElementTrailShape.Stream,
                LabElementTrailShape.Leaves, LabElementTrailShape.ParallelInk, LabElementTrailShape.DryClumps };
            var profiles = new HashSet<LabElementTrailProfile>();
            for (int i = 0; i < elements.Length; i++)
            {
                LabElementTrailProfile profile = appearance.ElementTrailFor(elements[i]);
                Assert.That(profile, Is.Not.Null, elements[i] + " needs a saved profile.");
                Assert.That(profiles.Add(profile), Is.True, "Elements must not share one mutable profile.");
                Assert.That(profile.Element, Is.EqualTo(elements[i]));
                Assert.That(profile.Shape, Is.EqualTo(shapes[i]));
                Assert.That(profile.Material, Is.Not.Null);
                Assert.That(profile.Material.shader.name, Is.EqualTo("C6Lab/Element Ink Trail"));
                Assert.That(profile.RibbonTexture, Is.Not.Null);
                Assert.That(profile.ParticleTexture, Is.Not.Null);
                Assert.That(profile.RibbonTexture.width, Is.GreaterThanOrEqualTo(32));
                Assert.That(profile.ParticleTexture.height, Is.GreaterThanOrEqualTo(32));
                Assert.That(profile.MaxLength, Is.GreaterThan(0f));
                Assert.That(profile.Width, Is.GreaterThan(0f));
                Assert.That(profile.Lifetime, Is.GreaterThan(0f));
                Assert.That(profile.ParticleSpacing, Is.GreaterThan(0f));
                Assert.That(profile.ParticleSize, Is.GreaterThan(0f));
                Assert.That(profile.Color.a, Is.GreaterThan(0f));
            }
        }

        [UnityTest]
        public IEnumerator EveryElementCreatesOnlyVisualGeometryOnTheProjectileLayer()
        {
            foreach (LabOrbElement element in Elements())
            {
                LabElementTrailProfile profile = Profile(element);
                LabElementFlightTrail effect = Effect(profile, out Transform source);
                source.gameObject.layer = 11;
                effect.Begin(source, profile);
                source.position += Vector3.right * profile.ParticleSpacing * 3.2f;
                yield return null;
                Assert.That(effect.Element, Is.EqualTo(element));
                Assert.That(effect.IsEmitting, Is.True);
                Assert.That(effect.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "Trail art cannot participate in target or floor hit detection.");
                Assert.That(effect.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(effect.GetComponentsInChildren<Renderer>(true)
                    .All(renderer => renderer.gameObject.layer == source.gameObject.layer), Is.True,
                    "Only the battle camera should render the projectile's effects.");
                Assert.That(effect.Motifs, Is.Not.Null);
                Assert.That(effect.Motifs.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                int expectedStrands = element == LabOrbElement.Metal ? 3
                    : element == LabOrbElement.Fire || element == LabOrbElement.Water ? 1 : 0;
                Assert.That(effect.StrandCount, Is.EqualTo(expectedStrands));
                effect.Clear();
                effect.gameObject.SetActive(false);
            }
        }

        [UnityTest]
        public IEnumerator WorldPathFollowsTheTravelledCornerAndClipsByArcLength()
        {
            LabElementTrailProfile profile = Profile(LabOrbElement.Water);
            SetProfile(profile, "maxLength", .65f);
            SetProfile(profile, "width", .1f);
            SetProfile(profile, "lifetime", 2f);
            LabElementFlightTrail effect = Effect(profile, out Transform source);
            source.position = new Vector3(.45f, 0f, 0f);
            yield return WaitForEffect(() => effect.PathCount >= 2, "The first movement must reach LateUpdate history sampling.");
            source.position = new Vector3(.45f, .45f, 0f);
            yield return WaitForEffect(() => effect.PathCount >= 3, "The corner must be sampled before checking its clipped history.");
            Assert.That(effect.PathCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(effect.PathLength, Is.EqualTo(.65f).Within(.025f),
                "Maximum world length must remain independent of movement speed and persistence seconds.");
            Assert.That(Vector3.Distance(effect.GetPathPosition(0), new Vector3(.25f, 0f, 0f)), Is.LessThan(.025f));
            Assert.That(Vector3.Distance(effect.GetPathPosition(effect.PathCount - 1), source.position), Is.LessThan(.001f));
            Assert.That(Enumerable.Range(0, effect.PathCount).Any(index =>
                Vector3.Distance(effect.GetPathPosition(index), new Vector3(.45f, 0f, 0f)) < .025f), Is.True,
                "The trail must retain the real corner, rather than project a straight tail behind the orb.");

            Vector3 frozenOldest = effect.GetPathPosition(0);
            source.rotation = Quaternion.Euler(15f, 140f, 80f);
            yield return null;
            Assert.That(Vector3.Distance(effect.GetPathPosition(0), frozenOldest), Is.LessThan(.001f),
                "Turning the orb must not rotate earlier world-space history.");
        }

        [UnityTest]
        public IEnumerator MotifsAreDistanceSpacedAndRemainAtTheirEmittedWorldPositions()
        {
            LabElementTrailProfile profile = Profile(LabOrbElement.Wood);
            SetProfile(profile, "particleSpacing", .2f);
            SetProfile(profile, "lifetime", 2f);
            SetProfile(profile, "maxLength", 3f);
            LabElementFlightTrail effect = Effect(profile, out Transform source);
            source.position = Vector3.right * .61f;
            yield return WaitForEffect(() => effect.Motifs.particleCount >= 3,
                "Distance emission must execute in LateUpdate before inspecting its particles.");
            var particles = new ParticleSystem.Particle[64];
            int count = effect.Motifs.GetParticles(particles);
            Assert.That(count, Is.EqualTo(3), "A .61-unit segment with .2 spacing emits three motifs.");
            Vector3[] before = particles.Take(count).Select(particle => particle.position).OrderBy(point => point.x).ToArray();
            Assert.That(before[1].x - before[0].x, Is.EqualTo(.2f).Within(.01f));
            Assert.That(before[2].x - before[1].x, Is.EqualTo(.2f).Within(.01f));
            source.position += Vector3.up * .04f;
            source.rotation = Quaternion.Euler(0f, 0f, 120f);
            yield return WaitForEffect(() => effect.PathCount >= 3,
                "The sub-spacing movement must be sampled before confirming that no motif was added.");
            int afterCount = effect.Motifs.GetParticles(particles);
            Assert.That(afterCount, Is.EqualTo(count), "Waiting or a sub-spacing move must not add stationary motifs.");
            Vector3[] after = particles.Take(afterCount).Select(particle => particle.position).OrderBy(point => point.x).ToArray();
            for (int i = 0; i < count; i++)
                Assert.That(Vector3.Distance(before[i], after[i]), Is.LessThan(.001f),
                    "Leaves must remain at passed world positions without inheriting orb motion or rotation.");
        }

        [UnityTest]
        public IEnumerator MetalHasThreeSeparatedRibbonsThatShareTheSameCurvedHistory()
        {
            LabElementTrailProfile profile = Profile(LabOrbElement.Metal);
            SetProfile(profile, "lifetime", 2f);
            LabElementFlightTrail effect = Effect(profile, out Transform source);
            source.position = Vector3.right * .25f;
            yield return null;
            source.position += new Vector3(.15f, .2f, .1f);
            yield return WaitForEffect(() => effect.Motifs.particleCount >= 3,
                "A short segment must leave three parallel Metal marks at the same time.");
            Camera camera = Own(new GameObject("Metal trail geometry camera")).AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(.2f, .1f, -3f);
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.cullingMask = 1 << source.gameObject.layer;
            var frame = Own(new RenderTexture(32, 32, 16, RenderTextureFormat.ARGB32));
            frame.Create();
            camera.targetTexture = frame;
            // Ribbons face each actual camera during its URP render callback, not during Update.
            camera.Render();
            var marks = new ParticleSystem.Particle[64];
            int markCount = effect.Motifs.GetParticles(marks);
            Assert.That(markCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(markCount % 3, Is.Zero, "Metal emits whole three-strand groups.");
            MeshFilter[] ribbons = effect.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(ribbons.Length, Is.EqualTo(3));
            Assert.That(ribbons.All(ribbon => ribbon.sharedMesh != null && ribbon.sharedMesh.vertexCount >= 4), Is.True);
            Assert.That(ribbons.Select(ribbon => ribbon.sharedMesh.vertexCount).Distinct().Count(), Is.EqualTo(1),
                "Three strands must use the same samples, rather than three differently aged emitters.");
            Vector3[][] worldVertices = ribbons.Select(ribbon => ribbon.sharedMesh.vertices
                .Select(ribbon.transform.TransformPoint).ToArray()).ToArray();
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    Assert.That(Vector3.Distance(worldVertices[i][0], worldVertices[j][0]), Is.GreaterThan(.001f),
                        "The three ink strands must remain separately visible.");
        }

        [UnityTest]
        public IEnumerator BeginAfterStopClearsParticlesAndDoesNotConnectToThePreviousFlight()
        {
            LabElementTrailProfile profile = Profile(LabOrbElement.Earth);
            SetProfile(profile, "lifetime", .2f);
            SetProfile(profile, "particleSpacing", .1f);
            LabElementFlightTrail effect = Effect(profile, out Transform source);
            source.position = Vector3.right * .35f;
            yield return WaitForEffect(() => effect.Motifs.particleCount > 0,
                "The old flight must emit real particles before testing reuse cleanup.");
            Assert.That(effect.Motifs.particleCount, Is.GreaterThan(0));
            effect.StopAndFade(null);
            source.position = new Vector3(100f, 40f, -30f);
            effect.Begin(source, profile);
            Assert.That(effect.IsEmitting, Is.True);
            Assert.That(effect.PathLength, Is.Zero.Within(.001f));
            Assert.That(effect.Motifs.particleCount, Is.Zero);
            Assert.That(effect.PathCount, Is.EqualTo(1));
            Assert.That(effect.GetPathPosition(0), Is.EqualTo(source.position));
            yield return new WaitForSeconds(.3f);
            Assert.That(effect != null, Is.True, "An earlier Stop must not destroy a reused active effect later.");
            Assert.That(effect.IsEmitting, Is.True);
            Assert.That(effect.PathLength, Is.Zero.Within(.001f));
        }

        [UnityTest]
        public IEnumerator HitProducesOneAuthorityReportAndDetachedResiduePausesThenExpires()
        {
            LabConfig config = SavedConfig();
            GameObject root = Own(new GameObject("Elemental hit test root"));
            LabTarget target = LabTarget.CreateCylinder(null, new Vector3(0f, 1f, 0f), .8f, 2f);
            Own(target.gameObject);
            LabProjectile projectile = LabProjectile.Create(new Vector3(0f, 1f, 3f),
                config, root.transform, LabOrbElement.Fire);
            LabElementFlightTrail effect = projectile.ElementFlightTrail;
            Assert.That(effect, Is.Not.Null);
            Assert.That(projectile.FlightTrail, Is.Null);
            int reports = 0;
            bool hit = false;
            Assert.That(projectile.Launch("elemental-one-hit", 17UL, new Vector3(0f, 0f, -9f),
                config, target, result => { reports++; hit = result.Hit; }), Is.True);
            for (int step = 0; step < 120 && reports == 0; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(reports, Is.EqualTo(1));
            Assert.That(hit, Is.True);
            Assert.That(effect != null, Is.True, "Impact must not instantly destroy the visual residue.");
            Assert.That(effect.IsEmitting, Is.False);
            Assert.That(effect.transform.parent, Is.SameAs(root.transform));
            Assert.That(effect.PathCount, Is.GreaterThan(1));
            int countAtPause = effect.PathCount;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(effect.Profile.Lifetime + .1f);
            Assert.That(effect != null, Is.True, "Session pause must pause the residue lifetime too.");
            Assert.That(effect.PathCount, Is.EqualTo(countAtPause));
            Time.timeScale = 1f;
            yield return new WaitForSeconds(effect.Profile.Lifetime + .1f);
            Assert.That(effect == null, Is.True, "After resume the residue must finish and release its render resources.");
            Assert.That(reports, Is.EqualTo(1), "Residual visual geometry must never produce another collision report.");
        }

        [UnityTest]
        public IEnumerator SoloRestartAndDisconnectRemoveActiveAndDetachedEffects()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabDeveloperMode developerUi = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            LabNetwork network = developerUi.network;
            startedNetwork = network;
            network.port = 19219;
            Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
            LabProjectile projectile = LabProjectile.Create(new Vector3(0f, 100f, 0f),
                network.config, network.projectileRoot, LabOrbElement.Water);
            Assert.That(projectile.LaunchVisual(Vector3.right, network.config), Is.True);
            LabElementFlightTrail active = projectile.ElementFlightTrail;
            LabElementFlightTrail detached = Effect(Profile(LabOrbElement.Wood), out Transform source);
            source.position += Vector3.right;
            yield return null;
            detached.StopAndFade(network.projectileRoot);
            Assert.That(detached.transform.parent, Is.SameAs(network.projectileRoot));
            Assert.That(network.RestartDeveloperSolo(), Is.True, network.Notice);
            yield return null;
            Assert.That(active == null, Is.True);
            Assert.That(detached == null, Is.True);
            Assert.That(network.projectileRoot.GetComponentsInChildren<LabElementFlightTrail>(true), Is.Empty);

            LabProjectile next = LabProjectile.Create(new Vector3(0f, 100f, 0f),
                network.config, network.projectileRoot, LabOrbElement.Earth);
            Assert.That(next.LaunchVisual(Vector3.right, network.config), Is.True);
            LabElementFlightTrail nextEffect = next.ElementFlightTrail;
            network.Stop();
            yield return null;
            Assert.That(nextEffect == null, Is.True);
            Assert.That(network.projectileRoot.GetComponentsInChildren<LabElementFlightTrail>(true), Is.Empty);
        }

        private static LabOrbElement[] Elements() => new[] { LabOrbElement.Fire, LabOrbElement.Water,
            LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth };

        private static LabConfig SavedConfig()
        {
            LabConfig config = Resources.Load<LabConfig>("LabConfig");
            Assert.That(config, Is.Not.Null);
            return config;
        }

        private LabElementTrailProfile Profile(LabOrbElement element)
        {
            LabElementTrailProfile source = SavedConfig().OrbAppearance.ElementTrailFor(element);
            Assert.That(source, Is.Not.Null, "Missing saved profile for " + element);
            return Own(Object.Instantiate(source));
        }

        private LabElementFlightTrail Effect(LabElementTrailProfile profile, out Transform source)
        {
            source = Own(new GameObject("Trail source")).transform;
            GameObject art = Own(new GameObject("Element trail under test"));
            art.transform.SetParent(source, false);
            LabElementFlightTrail effect = art.AddComponent<LabElementFlightTrail>();
            effect.Begin(source, profile);
            return effect;
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        private static IEnumerator WaitForEffect(System.Func<bool> condition, string failure)
        {
            // UnityTest coroutines can resume before LateUpdate. Wait for an observable result
            // with a real-time bound instead of assuming a particular frame rate or invoking
            // the production sampling method directly.
            float deadline = Time.realtimeSinceStartup + 1f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        private static void SetProfile(LabElementTrailProfile profile, string fieldName, float value)
        {
            FieldInfo field = typeof(LabElementTrailProfile).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing Inspector field " + fieldName);
            field.SetValue(profile, value);
        }
    }
}
