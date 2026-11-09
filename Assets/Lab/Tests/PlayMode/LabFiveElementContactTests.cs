using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    [Category("GPU")]
    public sealed class LabFiveElementContactTests
    {
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
