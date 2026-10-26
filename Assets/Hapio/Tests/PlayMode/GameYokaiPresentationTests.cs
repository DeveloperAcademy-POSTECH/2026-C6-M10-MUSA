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
        public IEnumerator EveryStageKeepsTheOriginalAnimatedYokaiAndRebindingDoesNotChangeTheTarget()
        {
            var root = new GameObject("Common yokai presentation fixture");
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
                Assert.That(originalBody, Is.Not.Null);
                Assert.That(originalBody.gameObject.activeSelf, Is.True);
                Renderer originalBodyRenderer = originalBody.GetComponent<Renderer>();
                Assert.That(originalBodyRenderer, Is.Not.Null);
                Transform[] originalChildren = slot.Cast<Transform>().ToArray();
                bool[] originalActive = originalChildren.Select(child => child.gameObject.activeSelf).ToArray();
                Renderer[] originalRenderers = slot.GetComponentsInChildren<Renderer>(true);
                Animation animation = gourd.GetComponentInChildren<Animation>(true);
                Assert.That(animation, Is.Not.Null);
                AnimationClip originalClip = animation.clip;
                Assert.That(originalClip, Is.Not.Null);
                // Freeze the authored animation at the risen pose so legitimate animation
                // motion cannot be mistaken for a transform change caused by stage binding.
                animation.Stop();
                originalClip.SampleAnimation(animation.gameObject, LabGourdYokai.RisenAt);
                Transform shell = gourd.transform.Find("White Gourd");
                Assert.That(shell, Is.Not.Null);
                Matrix4x4 shellPose = shell.localToWorldMatrix;
                Matrix4x4 targetPose = target.transform.localToWorldMatrix;
                Matrix4x4 bodyPose = originalBody.localToWorldMatrix;
                GameStageCatalog catalog = GameStageCatalog.RequireDefault();

                foreach (int stage in new[] { 1, 2, 3, 1 })
                {
                    GameStageDefinition definition = catalog.Get(stage);
                    Assert.That(definition.yokaiVisual, Is.Null,
                        "All default stages must use the same original black yokai.");
                    presentation.Bind(target, definition);
                    presentation.Bind(target, definition);
                    yield return null;
                    Assert.That(presentation.UsingExistingVisual, Is.True);
                    Assert.That(presentation.DecorativeInstance, Is.Null);
                    Assert.That(presentation.DecorativeSlot, Is.SameAs(slot));
                    Assert.That(presentation.Status, Does.Contain(definition.displayName));
                    CollectionAssert.AreEqual(originalChildren, slot.Cast<Transform>().ToArray(),
                        "Changing stage or binding twice must not add, replace or remove the original model.");
                    CollectionAssert.AreEqual(originalActive,
                        originalChildren.Select(child => child.gameObject.activeSelf).ToArray());
                    CollectionAssert.AreEqual(originalRenderers, slot.GetComponentsInChildren<Renderer>(true));
                    Assert.That(originalBodyRenderer.enabled, Is.True);
                    foreach (string eye in new[] { "EyePivot_L", "EyePivot_R" })
                        Assert.That(slot.Find(eye).gameObject.activeInHierarchy, Is.True, eye);
                    Assert.That(animation.clip, Is.SameAs(originalClip));
                    Assert.That(target.Hitbox, Is.SameAs(hitbox));
                    Assert.That(hitbox.sharedMesh, Is.SameAs(physicalMesh));
                    Assert.That(target.MountedVisual, Is.SameAs(gourd));
                    Assert.That(shell.localToWorldMatrix, Is.EqualTo(shellPose));
                    Assert.That(target.transform.localToWorldMatrix, Is.EqualTo(targetPose));
                    Assert.That(originalBody.localToWorldMatrix, Is.EqualTo(bodyPose));
                }
                presentation.enabled = false;
                presentation.enabled = true;
                presentation.Bind(target, catalog.Get(1));
                yield return null;
                Assert.That(presentation.DecorativeInstance, Is.Null);
                Assert.That(presentation.DecorativeSlot, Is.SameAs(slot));
                CollectionAssert.AreEqual(originalChildren, slot.Cast<Transform>().ToArray());
                CollectionAssert.AreEqual(originalActive,
                    originalChildren.Select(child => child.gameObject.activeSelf).ToArray());
                Assert.That(originalBodyRenderer.enabled, Is.True);

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
