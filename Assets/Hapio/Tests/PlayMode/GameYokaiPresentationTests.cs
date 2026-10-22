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
                Assert.That(model.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(model.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(model.GetComponentsInChildren<AudioListener>(true), Is.Empty);
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

        [UnityTest]
        public IEnumerator DecorativeRuntimeImportStripsAuthoringViewsWithoutMovingThePaintShell()
        {
            var root = new GameObject("Decorative import safety fixture");
            var imported = new GameObject("External authoring model");
            imported.SetActive(false);
            var stage = ScriptableObject.CreateInstance<GameStageDefinition>();
            try
            {
                LabTarget target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 1101f, 0f), .8f, 2f);
                target.SetVisualPrefab(Resources.Load<GameObject>("GourdPaint/WhiteGourd"));
                var presentation = root.AddComponent<GameYokaiPresentation>();
                GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.transform.SetParent(imported.transform, false);
                Camera authorCamera = mesh.AddComponent<Camera>();
                Light authorLight = mesh.AddComponent<Light>();
                AudioListener authorAudio = mesh.AddComponent<AudioListener>();
                System.Type cameraDataType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                System.Type lightDataType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalLightData, Unity.RenderPipelines.Universal.Runtime");
                Assert.That(cameraDataType, Is.Not.Null, "The project's actual URP camera dependency must be exercised.");
                Assert.That(lightDataType, Is.Not.Null, "The project's actual URP light dependency must be exercised.");
                mesh.AddComponent(cameraDataType);
                mesh.AddComponent(lightDataType);
                stage.stageId = "fixture"; stage.displayName = "Fixture"; stage.yokaiVisual = imported;
                Transform shell = target.MountedVisual.transform.Find("White Gourd");
                Matrix4x4 shellPose = shell.localToWorldMatrix;
                Matrix4x4 targetPose = target.transform.localToWorldMatrix;
                Mesh collisionMesh = target.Hitbox.sharedMesh;

                presentation.Bind(target, stage);
                GameObject clone = presentation.DecorativeInstance;
                Assert.That(clone, Is.Not.Null);
                Assert.That(clone.GetComponentsInChildren<Camera>(true).All(camera => !camera.enabled), Is.True,
                    "Authoring views are disabled before deferred destruction can occur.");
                Assert.That(clone.GetComponentsInChildren<Light>(true).All(light => !light.enabled), Is.True);
                Assert.That(clone.GetComponentsInChildren<AudioListener>(true).All(listener => !listener.enabled), Is.True);
                Assert.That(clone.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(component => component != null && (component.GetType() == cameraDataType || component.GetType() == lightDataType))
                    .All(component => !component.enabled), Is.True);
                Assert.That(shell.localToWorldMatrix, Is.EqualTo(shellPose));
                Assert.That(target.transform.localToWorldMatrix, Is.EqualTo(targetPose));
                Assert.That(target.Hitbox.sharedMesh, Is.SameAs(collisionMesh));
                // URP data is destroyed first; base views follow only after that dependency
                // has actually gone. They remain disabled throughout these cleanup frames.
                for (int frame = 0; frame < 4 && clone.GetComponentsInChildren<Camera>(true).Length != 0; frame++)
                    yield return null;
                Assert.That(clone.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(clone.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(clone.GetComponentsInChildren<AudioListener>(true), Is.Empty);
                Assert.That(clone.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(component => component != null && (component.GetType() == cameraDataType || component.GetType() == lightDataType)), Is.Empty);
                Assert.That(authorCamera.enabled && authorLight.enabled && authorAudio.enabled, Is.True,
                    "Only the owned runtime clone is sanitized; the source model is unchanged.");
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(imported); Object.Destroy(stage);
            }
            yield return null;
        }
    }
}
