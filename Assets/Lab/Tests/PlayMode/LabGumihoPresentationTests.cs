using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabGumihoPresentationTests
    {
        [UnityTest]
        public IEnumerator SavedGumihoFlashesOnImpactWithoutChangingTheSourceTexture()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;

            LabTarget target = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabTarget>(true)).Single();
            Renderer renderer = target.VisualMount.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(item => item.enabled && item.gameObject.activeInHierarchy &&
                    item.sharedMaterials.Any(material => material != null &&
                        material.HasProperty("_BaseColor") && material.HasProperty("_BaseMap")));
            Assert.That(renderer, Is.Not.Null, "The saved Gumiho needs a tintable textured material.");
            int index = System.Array.FindIndex(renderer.sharedMaterials, material => material != null &&
                material.HasProperty("_BaseColor") && material.HasProperty("_BaseMap"));
            Material source = renderer.sharedMaterials[index];
            Texture texture = source.GetTexture("_BaseMap");
            Assert.That(texture, Is.Not.Null);
            Color baseColor = source.GetColor("_BaseColor");

            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            hanging.ApplyHostImpact(target.Hitbox.bounds.center + target.Hitbox.transform.right * .5f,
                target.Hitbox.transform.forward * 12f);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties, index);
            Color hitColor = properties.GetColor("_BaseColor");
            Assert.That(Vector4.Distance(hitColor, baseColor), Is.GreaterThan(.1f),
                "An actual hit must visibly tint the Gumiho, not only the hidden Cylinder.");
            Assert.That(source.GetTexture("_BaseMap"), Is.SameAs(texture),
                "The flash must preserve the source texture binding.");
            Assert.That(source.GetColor("_BaseColor"), Is.EqualTo(baseColor),
                "The flash must not mutate the shared art material.");

            hanging.ResetSwing();
            renderer.GetPropertyBlock(properties, index);
            Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(baseColor));
        }

        [UnityTest]
        public IEnumerator SavedBattleSceneDisplaysGumihoWithoutReplacingTheHitbox()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;

            LabTarget[] targets = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabTarget>(true)).ToArray();
            Assert.That(targets, Has.Length.EqualTo(1),
                "The battle Scene must use one target for the visible art and authoritative collision.");
            LabTarget target = targets[0];
            MeshCollider hitbox = target.Hitbox;
            Assert.That(hitbox, Is.Not.Null);
            Assert.That(hitbox.enabled && hitbox.gameObject.activeInHierarchy, Is.True);
            Assert.That(hitbox.isTrigger, Is.False);
            Assert.That(hitbox.sharedMesh,
                Is.SameAs(hitbox.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(hitbox.GetComponent<MeshRenderer>().enabled, Is.False,
                "The Cylinder remains the hitbox, but must not obscure the Gumiho model.");

            Transform visualMount = target.VisualMount;
            Assert.That(visualMount, Is.Not.Null);
            Assert.That(visualMount.Find("GumihoVisual"), Is.Not.Null,
                "The saved Scene must mount the named Gumiho visual prefab.");
            Renderer[] art = visualMount.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
            Assert.That(art, Is.Not.Empty,
                "The saved Scene must contain visible Gumiho renderers under Visual Mount.");
            Assert.That(visualMount.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Imported art must not introduce an alternate 3D collision body.");
            Assert.That(visualMount.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(visualMount.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(visualMount.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);

            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            Assert.That(hanging, Is.Not.Null);
            Assert.That(visualMount.parent, Is.SameAs(hitbox.transform.parent),
                "The art and hitbox must follow the same swing and twist pivot.");
            Vector3 artPositionInHitbox = hitbox.transform.InverseTransformPoint(art[0].transform.position);
            Quaternion before = hitbox.transform.parent.localRotation;
            hanging.ConfigureSpin(80f, 0f, 720f);
            hanging.ApplyHostImpact(hitbox.bounds.center + hitbox.transform.right * .5f,
                hitbox.transform.forward * 12f);
            yield return new WaitForFixedUpdate();
            Assert.That(Quaternion.Angle(before, hitbox.transform.parent.localRotation),
                Is.GreaterThan(.01f));
            Assert.That(Vector3.Distance(artPositionInHitbox,
                hitbox.transform.InverseTransformPoint(art[0].transform.position)),
                Is.LessThan(.001f), "The visible mesh must travel with the hitbox after impact.");
            Assert.That(hitbox.enabled, Is.True);
        }
    }
}
