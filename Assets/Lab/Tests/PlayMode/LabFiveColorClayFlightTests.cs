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
    /// <summary>Real saved paper materials and pooled geometry; no mobile/GPU performance claim.</summary>
    [Category("GPU")]
    public sealed class LabFiveColorClayFlightTests
    {
        private Scene scene;
        private GameObject root;
        private LabConfig config;
        private LabFiveElementVfx pool;

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            Time.timeScale = 1f;
            scene = SceneManager.CreateScene("Five color clay fixture");
            root = new GameObject("Five color clay fixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            var saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(saved, Is.Not.Null);
            config = Object.Instantiate(saved);
            pool = LabFiveElementVfx.ForScene(scene);
            pool.ManualClock = true;
            Assert.That(Resources.Load<Shader>("FiveElements/LabWetPigment").isSupported, Is.True,
                "Run this resource-backed presentation fixture with graphics enabled.");
            Assert.That(Resources.Load<Material>("FiveElements/OrbSurface/Monochrome"), Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            if (pool != null) Object.Destroy(pool.gameObject);
            yield return null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ActualNoneProjectileUsesPaperClayAtItsDrawnSizeAndSparkleTintsThePooledBody()
        {
            var shot = LabProjectile.Create(new Vector3(0f, 81f, 0f), config, root.transform,
                LabOrbElement.None, .5f, useClayFlight: true, fiveColor: true);
            shot.SetRadiusScale(.65f);
            var sparkle = LabFiveColorSparkle.AttachArt(shot.transform, config.OrbRadius, true);
            Assert.That(shot.LaunchVisual(new Vector3(2f, 1f, 0f), config), Is.True);
            Assert.That(shot.ClayFlightActive, Is.True);
            Assert.That(shot.GetComponent<SphereCollider>().radius,
                Is.EqualTo(config.ThrowRadius * .5f * .65f).Within(.00001f),
                "The visual opt-in must not enlarge a small shot's collision sphere.");
            Assert.That(shot.VisualMount.gameObject.activeSelf, Is.False);
            Assert.That(shot.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
            Assert.That(shot.ElementFlightTrail, Is.Null);
            Assert.That(typeof(LabProjectile).GetField("element", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(shot), Is.EqualTo(LabOrbElement.None), "Five color is not a fabricated gameplay element.");
            Transform flight = pool.transform.Find("Flight 0");
            MeshRenderer ball = Part(flight, "Wet clay");
            Assert.That(ball.sharedMaterial, Is.SameAs(Paper()));
            Assert.That(ball.sharedMaterial.GetTexture("_FiberTex"), Is.Not.Null);
            Assert.That(ball.transform.localScale.x * ball.transform.localScale.y * ball.transform.localScale.z,
                Is.EqualTo(Mathf.Pow(shot.FlightDiameter, 3f)).Within(.00001f),
                "Launch squash preserves the body's drawn volume, including the minimum visible size.");
            Assert.That(flight.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(flight.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Vector3 start = shot.transform.position;
            for (int step = 0; step < 3; step++) yield return new WaitForFixedUpdate();
            yield return null; // Let the pooled body follow the simulated projectile in LateUpdate.
            Assert.That(Vector3.Distance(shot.transform.position, start), Is.GreaterThan(.005f));
            Assert.That(Vector3.Distance(flight.position, shot.transform.position), Is.LessThan(.12f),
                "The pooled body follows the live physics projectile's path.");
            sparkle.Tint();
            AssertColor(BaseColor(ball), sparkle.BodyColor);
            foreach (TrailRenderer trail in shot.GetComponentsInChildren<TrailRenderer>(true))
            {
                Assert.That(trail.emitting, Is.False, "The legacy five-key TrailRenderer must stay off.");
                Assert.That(trail.positionCount, Is.Zero);
            }
        }

        [Test]
        public void FullClayPoolKeepsFiveColorFallbackTrailAtTheCommonShapeThroughReconciliation()
        {
            for (int i = 0; i < pool.Profile.maxFlights; i++)
                Assert.That(pool.BeginFlight(LabOrbElement.Wood, Vector3.up * (81f + i), Vector3.forward,
                    .33f).IsActive, Is.True);
            var normal = LabProjectile.Create(new Vector3(-2f, 81f, 0f), config, root.transform,
                LabOrbElement.None, useClayFlight: true);
            var fiveColor = LabProjectile.Create(new Vector3(2f, 81f, 0f), config, root.transform,
                LabOrbElement.None, useClayFlight: true, fiveColor: true);
            var sparkle = LabFiveColorSparkle.AttachArt(fiveColor.transform, config.OrbRadius, true);
            Assert.That(normal.LaunchVisual(Vector3.forward, config), Is.True);
            Assert.That(fiveColor.LaunchVisual(Vector3.forward, config), Is.True);
            Assert.That(fiveColor.ClayFlightActive, Is.False, "A full clay pool exercises the actual fallback path.");
            Assert.That(pool.ActiveFlightCount, Is.EqualTo(pool.Profile.maxFlights));
            Assert.That(normal.FlightTrail, Is.Not.Null);
            Assert.That(fiveColor.FlightTrail, Is.Not.Null);
            float originalWidth = normal.FlightTrail.widthMultiplier;
            for (int pass = 0; pass < 3; pass++)
            {
                sparkle.TintTrail();
                AssertSameTrailShape(normal.FlightTrail, fiveColor.FlightTrail);
            }
            Assert.That(fiveColor.FlightTrail.colorGradient.colorKeys.Length, Is.EqualTo(5));
            Assert.That(fiveColor.FlightTrail.colorGradient.colorKeys.Select(key => key.color).Distinct().Count(),
                Is.EqualTo(5), "The common fallback still carries the five-color reward gradient.");

            normal.ReconcileVisual(LabOrbElement.None, .5f, .65f, fiveColor: false);
            fiveColor.ReconcileVisual(LabOrbElement.None, .5f, .65f, fiveColor: true);
            Assert.That(normal.FlightTrail.widthMultiplier, Is.LessThan(originalWidth),
                "Exercise the normal size adjustment before repeated palette writes.");
            for (int pass = 0; pass < 3; pass++)
            {
                sparkle.TintTrail();
                AssertSameTrailShape(normal.FlightTrail, fiveColor.FlightTrail);
            }
            Assert.That(fiveColor.FlightTrail.colorGradient.colorKeys.Length, Is.EqualTo(5));
            Assert.That(pool.ActiveImpactCount, Is.Zero);
        }

        private static void AssertSameTrailShape(TrailRenderer normal, TrailRenderer fiveColor)
        {
            Assert.That(fiveColor.widthMultiplier, Is.EqualTo(normal.widthMultiplier).Within(.00001f));
            Assert.That(fiveColor.time, Is.EqualTo(normal.time).Within(.00001f));
            for (int sample = 0; sample <= 4; sample++)
                Assert.That(fiveColor.widthCurve.Evaluate(sample * .25f),
                    Is.EqualTo(normal.widthCurve.Evaluate(sample * .25f)).Within(.00001f));
        }

        [Test]
        public void CurvedFiveColorLeaseKeepsTheTravelledCornerAndFlowsThroughTheClayTail()
        {
            const float diameter = .4f;
            var lease = pool.BeginFlight(LabOrbElement.None, Vector3.zero, Vector3.right,
                diameter, fiveColor: true);
            Assert.That(lease.IsActive, Is.True);
            lease.Move(Vector3.right * diameter, Vector3.right);
            lease.Move(new Vector3(diameter, diameter * .35f, 0f), Vector3.up);
            pool.Advance(.2f);
            Transform flight = pool.transform.Find("Flight 0");
            Transform neck = flight.Find("Pulling neck");
            Vector3 tailEnd = neck.position - neck.forward * neck.localScale.z * .5f;
            Assert.That(tailEnd.y, Is.EqualTo(0f).Within(.0001f),
                "The tail samples the old horizontal leg instead of extending straight below the head.");
            Assert.That(tailEnd.x, Is.InRange(.001f, diameter - .001f));
            Assert.That(flight.position, Is.EqualTo(new Vector3(diameter, diameter * .35f, 0f)));
            Assert.That(lease.TintFiveColor(0f, LabFiveColorSparkle.FiveColors), Is.True);
            MeshRenderer body = Part(flight, "Wet clay"), tail = Part(flight, "Pulling neck");
            Color beforeBody = BaseColor(body), beforeTail = BaseColor(tail);
            var ramp = new MaterialPropertyBlock();
            body.GetPropertyBlock(ramp);
            string[] rampNames = { "_InkColor", "_ShadowColor", "_LitColor", "_LightColor", "_GlintColor" };
            Color[] beforeRamp = rampNames.Select(name => ramp.GetColor(name)).ToArray();
            AssertColor(beforeBody, LabFiveColorSparkle.ColorAt(0f));
            Assert.That(Vector4.Distance(beforeBody, beforeTail), Is.GreaterThan(.05f),
                "The existing clay tail carries a spatial palette gradient.");
            Assert.That(lease.TintFiveColor(.9f, LabFiveColorSparkle.FiveColors), Is.True);
            AssertColor(BaseColor(body), LabFiveColorSparkle.ColorAt(.9f));
            Assert.That(Vector4.Distance(BaseColor(tail), beforeTail), Is.GreaterThan(.05f));
            body.GetPropertyBlock(ramp);
            for (int i = 0; i < rampNames.Length; i++)
                Assert.That(Vector4.Distance(ramp.GetColor(rampNames[i]), beforeRamp[i]), Is.GreaterThan(.01f),
                    "The paper shader's " + rampNames[i] + " band must follow the pigment too.");
        }

        [Test]
        public void NoneRequiresAnExplicitFiveColorFlagAndNormalLeasesRejectPaletteWrites()
        {
            Assert.That(pool.BeginFlight(LabOrbElement.None, Vector3.zero, Vector3.forward, .33f).IsActive, Is.False);
            Assert.That(pool.PlayImpact(LabOrbElement.None, Vector3.zero, Vector3.up, Vector3.down, .33f, .3f), Is.False);
            Assert.That(pool.BeginFlight((LabOrbElement)99, Vector3.zero, Vector3.forward, .33f,
                fiveColor: true).IsActive, Is.False);
            Assert.That(default(LabFiveElementVfx.Flight).TintFiveColor(0f, LabFiveColorSparkle.FiveColors), Is.False);
            var normal = pool.BeginFlight(LabOrbElement.Fire, Vector3.zero, Vector3.forward, .33f);
            MeshRenderer body = Part(pool.transform.Find("Flight 0"), "Wet clay");
            Assert.That(normal.TintFiveColor(.9f, LabFiveColorSparkle.FiveColors), Is.False);
            Assert.That(body.sharedMaterial, Is.SameAs(pool.Profile.OrbSurface(LabOrbElement.Fire, false)));
            Assert.That(body.HasPropertyBlock(), Is.False, "Normal authored paper ramps retain their saved colours.");
            normal.Cancel();
            var noColor = LabProjectile.Create(Vector3.up * 81f, config, root.transform,
                LabOrbElement.None, useClayFlight: true);
            Assert.That(noColor.LaunchVisual(Vector3.right, config), Is.True);
            Assert.That(noColor.ClayFlightActive, Is.False);
            Assert.That(noColor.TryTintClayFlight(0f, LabFiveColorSparkle.FiveColors), Is.False);
            Assert.That(noColor.VisualMount.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void FiveColorContactHasTheSameClayGeometryAndTimingAsANormalContactAndOnlyHitsOnce()
        {
            Vector3 point = new Vector3(.2f, 81f, .3f), normal = new Vector3(.1f, 1f, .2f).normalized;
            Vector3 incoming = new Vector3(2f, -9f, 1f);
            var normalLease = pool.BeginFlight(LabOrbElement.Wood, point, incoming, .37f, .6f);
            Assert.That(normalLease.Hit(point, normal, incoming, leaveMark: false), Is.True);
            pool.Advance(.3f);
            Transform impact = pool.transform.Find("Impact 0");
            var geometry = Geometry(impact);
            pool.ClearAll(); // Reset the seed, so both leases receive the same clay variant.
            var lease = pool.BeginFlight(LabOrbElement.None, point, incoming, .37f, .6f, fiveColor: true);
            lease.TintFiveColor(.9f, LabFiveColorSparkle.FiveColors);
            Assert.That(lease.Hit(point, normal, incoming, leaveMark: false), Is.True);
            Assert.That(lease.Hit(point, normal, incoming, leaveMark: false), Is.False);
            Assert.That(pool.ActiveFlightCount, Is.Zero);
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(1));
            Assert.That(Part(impact, "Contact squash").sharedMaterial, Is.SameAs(Paper()));
            pool.Advance(.3f);
            AssertGeometry(geometry, impact);
            Assert.That(Part(impact, "Wet surface mark").enabled, Is.True);
            Assert.That(impact.Find("Element release"), Is.Null);
            Assert.That(impact.GetComponentsInChildren<Collider>(true), Is.Empty);
            pool.Advance(2f);
            Assert.That(pool.ActiveImpactCount, Is.Zero);
        }

        [Test]
        public void ReusedNormalSlotRejectsStaleTintAndClearsTheFiveColorPaperRamp()
        {
            var stale = pool.BeginFlight(LabOrbElement.None, Vector3.one * 20f, Vector3.right, .33f, fiveColor: true);
            stale.TintFiveColor(.9f, LabFiveColorSparkle.FiveColors);
            Transform slot = pool.transform.Find("Flight 0");
            Assert.That(Part(slot, "Wet clay").HasPropertyBlock(), Is.True);
            stale.Cancel();
            var next = pool.BeginFlight(LabOrbElement.Water, Vector3.left, Vector3.forward, .18f);
            Assert.That(pool.transform.Find("Flight 0"), Is.SameAs(slot));
            Assert.That(stale.TintFiveColor(0f, LabFiveColorSparkle.FiveColors), Is.False);
            stale.Move(Vector3.one * 100f, Vector3.up);
            stale.Cancel();
            Assert.That(stale.Hit(Vector3.zero, Vector3.up, Vector3.down), Is.False);
            Assert.That(next.IsActive, Is.True);
            Assert.That(slot.position, Is.EqualTo(Vector3.left));
            Assert.That(Part(slot, "Wet clay").sharedMaterial,
                Is.SameAs(pool.Profile.OrbSurface(LabOrbElement.Water, false)));
            Assert.That(Part(slot, "Wet clay").HasPropertyBlock(), Is.False);
            pool.Advance(.1f);
            Transform neck = slot.Find("Pulling neck");
            Assert.That(neck.localScale.z, Is.Zero.Within(.00001f), "Reuse clears the distant previous flight's history.");
            Assert.That(next.Hit(Vector3.zero, Vector3.up, Vector3.down), Is.True);
            Transform impact = pool.transform.Find("Impact 0");
            Assert.That(Part(impact, "Contact squash").HasPropertyBlock(), Is.False);
        }

        [UnityTest]
        public IEnumerator PoolLimitCancellationAndClearRetireFiveColorLeasesWithoutContacts()
        {
            var leases = new List<LabFiveElementVfx.Flight>();
            for (int i = 0; i < pool.Profile.maxFlights; i++)
            {
                var lease = pool.BeginFlight(LabOrbElement.None, Vector3.right * i,
                    Vector3.forward, .33f, fiveColor: true);
                Assert.That(lease.IsActive, Is.True);
                leases.Add(lease);
            }
            Assert.That(pool.BeginFlight(LabOrbElement.None, Vector3.zero, Vector3.forward, .33f,
                fiveColor: true).IsActive, Is.False);
            leases[0].Cancel();
            var replacement = pool.BeginFlight(LabOrbElement.None, Vector3.zero, Vector3.forward,
                .33f, fiveColor: true);
            Assert.That(replacement.IsActive, Is.True);
            pool.ClearAll();
            Assert.That(pool.ActiveFlightCount, Is.Zero);
            Assert.That(replacement.TintFiveColor(0f, LabFiveColorSparkle.FiveColors), Is.False);
            Assert.That(leases.All(lease => !lease.IsActive), Is.True);
            var shot = LabProjectile.Create(Vector3.up * 81f, config, root.transform,
                LabOrbElement.None, useClayFlight: true, fiveColor: true);
            int contacts = 0;
            Assert.That(shot.LaunchVisual(Vector3.right, config, contact: _ => contacts++), Is.True);
            Assert.That(pool.ActiveFlightCount, Is.EqualTo(1));
            shot.gameObject.SetActive(false);
            Assert.That(pool.ActiveFlightCount, Is.Zero);
            yield return null;
            Assert.That(contacts, Is.Zero);
            Assert.That(pool.ActiveImpactCount, Is.Zero);
            Assert.That(pool.transform.Cast<Transform>().All(child => !child.gameObject.activeSelf), Is.True);
        }

        private static Material Paper() => Resources.Load<Material>("FiveElements/OrbSurface/Monochrome");
        private static MeshRenderer Part(Transform slot, string name) => slot.Find(name).GetComponent<MeshRenderer>();
        private static Color BaseColor(MeshRenderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }
        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.001f));
        }

        private sealed class PartGeometry
        {
            public string Name;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public Mesh Mesh;
            public bool Enabled;
        }
        private static PartGeometry[] Geometry(Transform slot) => slot.GetComponentsInChildren<MeshRenderer>(true)
            .Select(part => new PartGeometry { Name = part.name, Position = part.transform.localPosition,
                Rotation = part.transform.localRotation, Scale = part.transform.localScale,
                Mesh = part.GetComponent<MeshFilter>().sharedMesh, Enabled = part.enabled }).ToArray();
        private static void AssertGeometry(PartGeometry[] expected, Transform slot)
        {
            var actual = Geometry(slot);
            Assert.That(actual.Select(part => part.Name), Is.EqualTo(expected.Select(part => part.Name)));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(Vector3.Distance(actual[i].Position, expected[i].Position), Is.LessThan(.00001f), expected[i].Name);
                Assert.That(Quaternion.Angle(actual[i].Rotation, expected[i].Rotation), Is.LessThan(.001f), expected[i].Name);
                Assert.That(Vector3.Distance(actual[i].Scale, expected[i].Scale), Is.LessThan(.00001f), expected[i].Name);
                Assert.That(actual[i].Mesh, Is.SameAs(expected[i].Mesh));
                Assert.That(actual[i].Enabled, Is.EqualTo(expected[i].Enabled));
            }
        }
    }
}
