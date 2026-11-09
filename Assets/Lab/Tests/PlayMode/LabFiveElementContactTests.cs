using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace C6Lab.Tests
{
    [Category("GPU")]
    public sealed class LabFiveElementContactTests
    {
#if UNITY_EDITOR
        private const string ComparisonScenePath = "Assets/Experiments/FiveElements/Scenes/FiveElementsComparison.unity";
        private bool comparisonSceneOwned;

        [UnityTearDown]
        public IEnumerator UnloadPreparedComparisonScene()
        {
            if (!comparisonSceneOwned) yield break;
            comparisonSceneOwned = false;
            Scene scene = SceneManager.GetSceneByPath(ComparisonScenePath);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreparedComparisonFillsViewportAndRecoversDetachedLayoutWhilePaused()
        {
            Assert.IsNotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(ComparisonScenePath),
                "The checked-in prepared comparison Scene must be available without generating or saving it in this test.");
            Assert.IsFalse(SceneManager.GetSceneByPath(ComparisonScenePath).isLoaded,
                "The fixture must own its temporary additive Scene.");
            comparisonSceneOwned = true;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ComparisonScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Scene scene = SceneManager.GetSceneByPath(ComparisonScenePath);
            var demo = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabFiveElementsDemo>()).Single();
            var document = demo.GetComponent<UIDocument>();
            for (int frame = 0; frame < 60 && (demo.DemoCamera == null || document.rootVisualElement.panel == null ||
                !(document.rootVisualElement.Q("view").worldBound.height > 1f)); frame++) yield return null;
            yield return null;

            VisualElement rootElement = document.rootVisualElement;
            VisualElement shell = rootElement.Q("shell"), view = rootElement.Q("view");
            Assert.IsNotNull(rootElement.panel, "The real comparison HUD must attach to its runtime panel.");
            Assert.That(rootElement.worldBound.height,
                Is.EqualTo(rootElement.panel.visualTree.worldBound.height).Within(1f),
                "The UIDocument must fill the panel instead of collapsing its percentage-height shell.");
            Assert.That(view.worldBound.height, Is.GreaterThan(shell.worldBound.height * .35f),
                "The clay preview must consume the space between the top labels and transport controls.");
            Assert.That(view.worldBound.yMax, Is.LessThanOrEqualTo(rootElement.Q(className: "transport").worldBound.yMin + 1f));
            AssertComparisonViewport(demo, shell, view);

            demo.SetPaused(true);
            float pausedAt = demo.PlaybackTime;
            float previousSize = demo.DemoCamera.orthographicSize;
            demo.SetLens(float.NaN);
            demo.SetLens(float.PositiveInfinity);
            demo.SetLens(0f);
            Assert.That(demo.DemoCamera.orthographicSize, Is.EqualTo(previousSize),
                "Unresolved UI aspect values must never poison the lens or lane padding.");
            AssertComparisonViewport(demo, shell, view);

            // Detachment is a real UI Toolkit interval with no panel, as during document/layout rebuilds.
            // Change the camera while detached so reattachment must actually frame it again while paused.
            VisualElement parent = view.parent;
            int index = parent.IndexOf(view);
            view.RemoveFromHierarchy();
            Rect detachedViewport = new Rect(0f, 0f, .25f, .25f);
            demo.DemoCamera.rect = detachedViewport;
            demo.SetComparison(false, false, false, false, false);
            Assert.That(demo.DemoCamera.rect, Is.EqualTo(detachedViewport),
                "A detached viewport must retain the last camera rectangle until layout is valid.");
            parent.Insert(index, view);
            for (int frame = 0; frame < 60 && (view.panel == null ||
                Mathf.Abs(demo.DemoCamera.rect.width - 1f) > .001f); frame++) yield return null;
            yield return null;
            AssertComparisonViewport(demo, shell, view);
            Assert.IsTrue(demo.Paused);
            Assert.That(demo.PlaybackTime, Is.EqualTo(pausedAt).Within(.00001f),
                "Recovering the comparison layout must not resume its paused animation.");
        }

        private static void AssertComparisonViewport(LabFiveElementsDemo demo, VisualElement shell, VisualElement view)
        {
            Assert.IsNotNull(demo.DemoCamera);
            Rect rect = demo.DemoCamera.rect;
            foreach (float component in new[] { rect.x, rect.y, rect.width, rect.height, demo.DemoCamera.orthographicSize,
                shell.Q("lanes").resolvedStyle.paddingLeft, shell.Q("lanes").resolvedStyle.paddingRight })
                Assert.IsFalse(float.IsNaN(component) || float.IsInfinity(component), "Layout must produce finite camera and lane values.");
            Assert.That(rect.width, Is.EqualTo(1f).Within(.001f));
            Assert.That(rect.height, Is.GreaterThan(0f).And.LessThanOrEqualTo(1.001f));
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-.001f));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(1.001f));
            Assert.That(rect.height, Is.EqualTo(view.worldBound.height / shell.worldBound.height).Within(.002f));
            Assert.That(rect.y, Is.EqualTo(1f - (view.worldBound.yMax - shell.worldBound.yMin) / shell.worldBound.height).Within(.002f));
            Assert.That(demo.DemoCamera.orthographicSize, Is.GreaterThan(0f));
        }
#endif

        [UnityTest]
        public IEnumerator RealProjectileProducesOneIndependentEarthImpact()
        {
            var fixture = new GameObject("Element contact fixture");
            var config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            var pool = LabFiveElementVfx.ForScene(fixture.scene);
            pool.ManualClock = true;
            try
            {
                Vector3 center = new Vector3(0f, 802f, 0f);
                var target = LabTarget.CreateCylinder(fixture.transform, center, .8f, 2f);
                var feedback = fixture.AddComponent<LabImpactFeedback>();
                yield return null;
                var shot = LabProjectile.Create(center + Vector3.forward * 1.45f,
                    config, fixture.transform, LabOrbElement.Earth, .75f, useClayFlight: true);
                int hits = 0;
                bool launched = shot.Launch("clay-contact", 1, Vector3.back * 12f, config, target, hit =>
                {
                    Assert.IsTrue(hit.Hit);
                    Assert.That(hit.VisualDiameter, Is.EqualTo(config.ThrowRadius * 1.5f).Within(.0001f));
                    hits++;
                    feedback.PlayContact(target, target.Hitbox.transform.InverseTransformPoint(hit.HitPoint),
                        LabOrbElement.Earth, 1, hit.ImpactVelocity, hit.HitNormal, hit.VisualDiameter);
                });
                Assert.IsTrue(launched);
                Assert.AreEqual(1, pool.ActiveFlightCount);
                Assert.IsFalse(shot.VisualMount.gameObject.activeSelf);
                for (int step = 0; step < 25 && hits == 0; step++) yield return new WaitForFixedUpdate();
                yield return null;
                Assert.AreEqual(1, hits);
                Assert.AreEqual(0, pool.ActiveFlightCount);
                pool.Advance(.32f);
                Assert.AreEqual(1, pool.ActiveImpactCount);
                Transform impact = pool.transform.Find("Impact 0");
                var mark = impact.Find("Wet surface mark").GetComponent<MeshRenderer>();
                Assert.IsTrue(mark.enabled && mark.gameObject.activeInHierarchy,
                    "The real Battle contact path must retain brief clay even with leaveMark disabled.");
                AssertClayOnly(impact);
                Vector3 beforeMove = impact.position;
                Vector3 movement = new Vector3(.2f, .1f, .3f);
                target.transform.position += movement;
                pool.Advance(0f);
                Assert.That(Vector3.Distance(impact.position, beforeMove + movement), Is.LessThan(.0001f),
                    "The retained clay must follow the hit target instead of floating at its old position.");
                Object.Destroy(target.gameObject);
                yield return null;
                pool.Advance(.02f);
                Assert.AreEqual(0, pool.ActiveImpactCount, "A removed target must also retire its attached effect.");
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(config);
                Object.Destroy(pool.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllFiveElementsKeepClayWithoutReleaseAndFadeBeforeSlotReuse()
        {
            var fixture = new GameObject("Clay-only lifetime fixture");
            var pool = LabFiveElementVfx.ForScene(fixture.scene);
            try
            {
                pool.ManualClock = true;
                pool.MarkLifetimeOverride = 1.5f;
                Transform reusedSlot = null;
                for (int elementIndex = 1; elementIndex <= 5; elementIndex++)
                {
                    var element = (LabOrbElement)elementIndex;
                    foreach (bool leaveMark in new[] { false, true })
                    {
                        Assert.IsTrue(pool.PlayImpact(element, Vector3.zero, Vector3.up, Vector3.down,
                            .33f, .3f, leaveMark: leaveMark), element + " must acquire a clay impact.");
                        Transform impact = pool.transform.Find("Impact 0");
                        if (reusedSlot != null) Assert.AreSame(reusedSlot, impact, "Expired contacts should reuse their slot.");
                        reusedSlot = impact;
                        AssertClayOnly(impact);
                        var squash = impact.Find("Contact squash").GetComponent<MeshRenderer>();
                        var mark = impact.Find("Wet surface mark").GetComponent<MeshRenderer>();
                        Assert.IsTrue(squash.enabled, "A reused slot must restart the contact squash.");
                        Assert.IsFalse(mark.enabled, "The previous contact's residue must not flash on a reused slot.");

                        pool.Advance(.32f);
                        Assert.IsTrue(mark.enabled && mark.gameObject.activeInHierarchy,
                            element + ": clay spread must remain after the squash, including leaveMark:false.");
                        float spreadWidth = mark.transform.localScale.x;
                        Assert.IsFalse(squash.enabled);
                        Assert.IsTrue(impact.Find("Thrown clay 0").GetComponent<MeshRenderer>().enabled,
                            "Removing the elemental burst must retain thrown clay drops.");

                        // The game has a brief contact residue; the comparison can retain a longer mark.
                        // Both must shrink visibly before the slot retires, rather than disappear at impact.
                        pool.Advance(leaveMark ? 1.10f : .48f);
                        Assert.AreEqual(1, pool.ActiveImpactCount);
                        Assert.IsTrue(mark.enabled);
                        Assert.That(mark.transform.localScale.x, Is.GreaterThan(0f).And.LessThan(spreadWidth),
                            element + ": the retained clay should fade by shrinking near its own lifetime.");
                        Assert.IsFalse(impact.Find("Thrown clay 0").GetComponent<MeshRenderer>().enabled);
                        pool.Advance(.3f);
                        Assert.AreEqual(0, pool.ActiveImpactCount);
                        Assert.IsFalse(impact.gameObject.activeInHierarchy);
                    }
                }
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(pool.gameObject);
            }
            yield return null;
        }

        private static void AssertClayOnly(Transform impact)
        {
            Assert.IsNull(impact.Find("Element release"), "Clay contacts must not create the elemental release layer.");
            foreach (var renderer in impact.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null)
                        Assert.AreNotEqual("C6Lab/Five Elements/Release", material.shader.name,
                            "The removed elemental explosion must not remain in an active or pooled renderer.");
        }

        [UnityTest]
        public IEnumerator PooledBodyAndContactUseTheSelectedPaperMaterial()
        {
            var fixture = new GameObject("Paper orb fixture");
            var pool = LabFiveElementVfx.ForScene(fixture.scene);
            try
            {
                pool.ManualClock = true;
                var profile = pool.Profile;
                for (int index = 0; index < 6; index++)
                {
                    // The last sample uses the same slot to switch from earth to monochrome.
                    bool mono = index == 5;
                    var element = (LabOrbElement)(1 + index % 5);
                    Material expected = Resources.Load<Material>("FiveElements/OrbSurface/" +
                        (mono ? "Monochrome" : element.ToString()));
                    Assert.IsNotNull(expected);
                    Assert.AreSame(expected, profile.OrbSurface(element, mono));
                    var flight = pool.BeginFlight(element, Vector3.zero, Vector3.forward, .33f, .3f, mono);
                    var body = pool.transform.Find("Flight 0/Wet clay").GetComponent<MeshRenderer>();
                    Assert.AreSame(expected, body.sharedMaterial);
                    Assert.IsFalse(body.HasPropertyBlock(), "A previous pigment tint would replace the authored ramp.");

                    Assert.IsTrue(flight.Hit(Vector3.zero, Vector3.up, Vector3.down));
                    var contact = pool.transform.Find("Impact 0/Contact squash").GetComponent<MeshRenderer>();
                    Assert.AreSame(expected, contact.sharedMaterial);
                    Assert.IsFalse(contact.HasPropertyBlock());
                    pool.ClearAll();
                }
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(pool.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecycledFlightIgnoresLateDuplicateContact()
        {
            var fixture = new GameObject("Element lease fixture");
            var pool = LabFiveElementVfx.ForScene(fixture.scene);
            try
            {
                pool.ManualClock = true;
                var first = pool.BeginFlight(LabOrbElement.Water, Vector3.right, Vector3.down, .33f);
                Assert.IsTrue(first.Hit(Vector3.zero, Vector3.up, Vector3.down));
                var next = pool.BeginFlight(LabOrbElement.Fire, Vector3.left, Vector3.forward, .18f);
                first.Move(Vector3.one * 60, Vector3.up);
                first.Cancel();
                Assert.IsFalse(first.Hit(Vector3.zero, Vector3.up, Vector3.down));
                Assert.IsTrue(next.IsActive);
                Assert.AreEqual(1, pool.ActiveImpactCount);
                next.Cancel();
                pool.Advance(4f);
                Assert.AreEqual(0, pool.ActiveFlightCount);
                Assert.AreEqual(0, pool.ActiveImpactCount);
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(pool.gameObject);
            }
            yield return null;
        }
    }
}
