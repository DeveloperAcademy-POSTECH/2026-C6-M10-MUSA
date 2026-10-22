using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace C6Lab.Tests
{
    public sealed class LabOrbSpriteTests
    {
        static readonly LabOrbElement[] Elements = {LabOrbElement.Fire,LabOrbElement.Water,LabOrbElement.Wood,LabOrbElement.Metal,LabOrbElement.Earth};
        [UnityTest]
        public IEnumerator BoardArtChangesKindWithoutChangingCollisionAndCanReturnToMesh()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            var root = new GameObject("Sprite board fixture");
            root.AddComponent<Rigidbody>().useGravity = false;
            root.AddComponent<SphereCollider>();
            var view = root.AddComponent<LabOrbView>();
            view.ConfigureAppearance(config.OrbAppearance);
            try
            {
                for(int i=0;i<7;i++)
                {
                    var kind=i==0?LabOrbKind.Yin:i==1?LabOrbKind.Yang:LabOrbKind.Combined;
                    var element=i<2?LabOrbElement.None:Elements[i-2];
                    view.Configure("sprite",kind,.32f,element);
                    yield return null;
                    Assert.That(view.HitSphere.radius,Is.EqualTo(.32f));
                    Assert.That(root.GetComponentsInChildren<Collider>().Length,Is.EqualTo(1));
                    var sprite=root.GetComponentInChildren<LabOrbSpriteArt>();
                    Assert.That(sprite,Is.Not.Null);
                    Assert.That(sprite.GetComponent<SpriteRenderer>().sprite,Is.SameAs(config.OrbAppearance.SpriteFor(kind,element)));
                    Assert.That(Mathf.Max(sprite.GetComponent<SpriteRenderer>().bounds.size.x,sprite.GetComponent<SpriteRenderer>().bounds.size.y),Is.EqualTo(i<2 ? .64f : .768f).Within(.001f));
                    Assert.That(view.Visual.GetComponentsInChildren<MeshRenderer>().Length,Is.Zero);
                }
                view.ConfigureAppearance(null);
                yield return null;
                Assert.That(root.GetComponentInChildren<LabOrbSpriteArt>(),Is.Null);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThan(0));
                Assert.That(view.HitSphere.radius,Is.EqualTo(.32f));
            }
            finally{Object.Destroy(root);}
        }
        [UnityTest]
        public IEnumerator EveryElementFliesAsA3DSphereOnTheHostAndReplicasWithItsExistingSizeAndTrail()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            var target = LabTarget.CreateCylinder(null, new Vector3(0f, 100f, 20f), .8f, 2f);
            try
            {
                foreach (bool host in new[] { true, false })
                foreach (var element in Elements)
                {
                    var shot = LabProjectile.Create(Vector3.zero, config, element: element, sizeScale: .5f);
                    try
                    {
                        shot.SetRadiusScale(2.5f);
                        bool launched = host
                            ? shot.Launch("mesh-" + element, 7ul, Vector3.forward, config, target, null)
                            : shot.LaunchVisual(Vector3.forward, config, target);
                        Assert.That(launched, Is.True);
                        yield return null;
                        AssertSphere(shot, config.OrbAppearance.ElementColorFor(element));
                        float radius = config.ThrowRadius * .5f * 2.5f;
                        Assert.That(shot.GetComponent<SphereCollider>().radius, Is.EqualTo(radius).Within(.0001f));
                        Assert.That(shot.GetComponent<SphereCollider>().isTrigger, Is.EqualTo(!host));
                        Assert.That(Surface(shot).transform.lossyScale.x, Is.EqualTo(radius * 2f).Within(.001f));
                        Assert.That(shot.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                        Assert.That(shot.ElementFlightTrail != null || shot.FlightTrail != null, Is.True);
                    }
                    finally { Object.Destroy(shot.gameObject); }
                }
            }
            finally { Object.Destroy(target.gameObject); }
        }

        [UnityTest]
        public IEnumerator AnUnapprovedPredictedShotIsNeutralAndAcknowledgementOnlyChangesItsPresentation()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            var shot = LabProjectile.Create(Vector3.zero, config, element: LabOrbElement.None, sizeScale: .5f);
            try
            {
                shot.SetRadiusScale(2.5f);
                Assert.That(shot.LaunchVisual(new Vector3(0f, 3f, 5f), config), Is.True);
                yield return null;
                Color neutral = LabOrbAppearance.DefaultElementColor(LabOrbElement.None);
                AssertSphere(shot, neutral);
                Assert.That(shot.FlightTrail, Is.Not.Null, "Unknown elements use the neutral fallback trail.");
                Color trailColor = shot.FlightTrail.startColor;
                trailColor.a = 1f;
                // Trail gradients round each RGB channel to Color32 (half an 8-bit step).
                AssertColor(trailColor, neutral, .5f / 255f + .00001f);
                var body = shot.GetComponent<Rigidbody>();
                Vector3 position = body.position, velocity = body.linearVelocity;
                float collisionRadius = shot.GetComponent<SphereCollider>().radius;
                shot.ReconcileVisual(LabOrbElement.Water, .5f, 2.5f);
                AssertSphere(shot, config.OrbAppearance.ElementColorFor(LabOrbElement.Water));
                Assert.That(body.position, Is.EqualTo(position), "An acknowledgement must not rewind the local flight.");
                Assert.That(body.linearVelocity, Is.EqualTo(velocity));
                Assert.That(shot.GetComponent<SphereCollider>().radius, Is.EqualTo(collisionRadius));
                Assert.That(shot.SizeScale, Is.EqualTo(.5f));
                Assert.That(shot.RadiusScale, Is.EqualTo(2.5f));
                Assert.That(shot.ElementFlightTrail != null || shot.FlightTrail != null, Is.True);
                shot.ReconcileVisual(LabOrbElement.Water, .5f, 2.5f);
                AssertSphere(shot, config.OrbAppearance.ElementColorFor(LabOrbElement.Water));
                Assert.That(shot.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            }
            finally { Object.Destroy(shot.gameObject); }
        }

        [UnityTest]
        public IEnumerator MissingCombinedMaterialStillTintsThePrefabForPendingAndApprovedElements()
        {
            var config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            var appearance = Object.Instantiate(config.OrbAppearance);
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            typeof(LabConfig).GetField("orbAppearance", fields).SetValue(config, appearance);
            typeof(LabOrbAppearance).GetField("combinedMaterial", fields).SetValue(appearance, null);
            var shot = LabProjectile.Create(Vector3.zero, config, element: LabOrbElement.None);
            try
            {
                yield return null;
                AssertSphere(shot, LabOrbAppearance.DefaultElementColor(LabOrbElement.None));
                Assert.That(Surface(shot).sharedMaterial, Is.Not.Null, "The prefab's existing material is the fallback.");
                shot.ReconcileVisual(LabOrbElement.Wood, 1f, 1f);
                AssertSphere(shot, appearance.ElementColorFor(LabOrbElement.Wood));
                Assert.That(shot.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(shot.gameObject);
                Object.Destroy(appearance);
                Object.Destroy(config);
            }
        }

        [UnityTest]
        public IEnumerator ProjectileSphereKeepsItsWorldOrientationWhenViewedFromDifferentCameras()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            var shot = LabProjectile.Create(Vector3.zero, config, element: LabOrbElement.Fire);
            var go = new GameObject("Sphere camera");
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.targetTexture = new RenderTexture(64, 64, 24);
            try
            {
                yield return null;
                var surface = Surface(shot);
                Quaternion orientation = surface.transform.rotation;
                foreach (float angle in new[] { 0f, 65f, -65f })
                {
                    camera.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                    camera.transform.position = -camera.transform.forward * 3f;
                    camera.Render();
                    Assert.That(Quaternion.Angle(surface.transform.rotation, orientation), Is.LessThan(.01f));
                    AssertSphere(shot, config.OrbAppearance.ElementColorFor(LabOrbElement.Fire));
                }
            }
            finally
            {
                Object.Destroy(camera.targetTexture);
                Object.Destroy(go);
                Object.Destroy(shot.gameObject);
            }
        }

        private static MeshRenderer Surface(LabProjectile shot)
        {
            Transform surface = shot.VisualMount.Find("Replacement Visual/Sphere")
                ?? shot.VisualMount.Find("Default Sphere");
            Assert.That(surface, Is.Not.Null);
            return surface.GetComponent<MeshRenderer>();
        }

        private static void AssertSphere(LabProjectile shot, Color color)
        {
            Assert.That(shot.VisualMount.GetComponentsInChildren<LabOrbSpriteArt>(), Is.Empty);
            var surface = Surface(shot);
            Assert.That(surface.gameObject.activeInHierarchy, Is.True);
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.bounds.size.z, Is.GreaterThan(.1f), "The flight art has real depth.");
            var properties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(properties);
            AssertColor(properties.GetColor("_BaseColor"), color);
            AssertColor(properties.GetColor("_Color"), color);
        }

        private static void AssertColor(Color actual, Color expected, float tolerance = .0001f)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
        }
    }
}
