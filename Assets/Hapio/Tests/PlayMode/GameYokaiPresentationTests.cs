using System.Collections;
using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    /// <summary>Decorative replacement operates beneath the animated slot and never owns the target's physical shell.</summary>
    public sealed class GameYokaiPresentationTests
    {
        [UnityTest]
        public IEnumerator GumihoUsesTheSavedArtAndOtherStagesRestoreTheExistingYokaiWithoutChangingTheTarget()
        {
            var root = new GameObject("Stage decorative presentation fixture");
            LabTarget target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 1101f, 0f), .8f, 2f);
            var presentation = root.AddComponent<GameYokaiPresentation>();
            GameObject unsupported = null;
            try
            {
                target.SetVisualPrefab(Resources.Load<GameObject>("GourdPaint/WhiteGourd"));
                yield return null;
                MeshCollider hitbox = target.Hitbox; Mesh physicalMesh = hitbox.sharedMesh;
                GameObject gourd = target.MountedVisual;
                Transform slot = gourd.transform.Find(GameYokaiPresentation.SlotPath);
                Assert.That(slot, Is.Not.Null);
                Transform originalBody = slot.Find("Yokai");
                Assert.That(originalBody.gameObject.activeSelf, Is.True);
                GameStageCatalog catalog = GameStageCatalog.RequireDefault();
                Assert.That(catalog.Get(1).yokaiVisual, Is.Not.Null);
                presentation.Bind(target, catalog.Get(1));
                GameObject model = presentation.DecorativeInstance;
                Assert.That(model, Is.Not.Null);
                Assert.That(model.transform.parent, Is.SameAs(slot));
                Assert.That(originalBody.gameObject.activeSelf, Is.False);
                Assert.That(model.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
                Assert.That(model.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
                Assert.That(target.Hitbox, Is.SameAs(hitbox)); Assert.That(hitbox.sharedMesh, Is.SameAs(physicalMesh));
                Assert.That(target.MountedVisual, Is.SameAs(gourd), "The paint presentation must not be replaced with the fox model.");
                presentation.Bind(target, catalog.Get(1));
                Assert.That(presentation.DecorativeInstance, Is.SameAs(model), "Repeated binding must not instantiate another fox.");
                yield return null;
                Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);

                foreach (int stage in new[] { 2, 3 })
                {
                    presentation.Bind(target, catalog.Get(stage));
                    Assert.That(presentation.UsingExistingVisual, Is.True);
                    Assert.That(originalBody.gameObject.activeSelf, Is.True);
                    Assert.That(presentation.Status, Does.Contain(catalog.Get(stage).displayName));
                    Assert.That(target.Hitbox, Is.SameAs(hitbox)); Assert.That(hitbox.sharedMesh, Is.SameAs(physicalMesh));
                }
                yield return null;
                Assert.That(model == null, Is.True);
                unsupported = new GameObject("Custom or Crack-like presentation without a yokai slot");
                target.SetVisualPrefab(unsupported);
                presentation.Bind(target, catalog.Get(1));
                Assert.That(presentation.UsingExistingVisual, Is.True);
                Assert.That(presentation.Status, Does.Contain("No decorative yokai slot"));
                Assert.That(target.Hitbox, Is.SameAs(hitbox)); Assert.That(hitbox.enabled, Is.True);
            }
            finally
            {
                Object.Destroy(root); if (unsupported != null) Object.Destroy(unsupported);
            }
            yield return null;
        }
    }
}
