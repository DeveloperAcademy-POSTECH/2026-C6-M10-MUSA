using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
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
