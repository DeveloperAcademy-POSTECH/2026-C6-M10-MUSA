using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    public sealed class GameBackgroundAnimationAssetTests
    {
        [TestCase("Assets/Lab/Scenes/Lab.unity")]
        [TestCase(GameScenePaths.Battle)]
        public void SavedCloudAndButterflyAnimationsHaveResolvableBindingsAndNoGameplayComponents(string path)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                LabBackgroundStudio studio = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabBackgroundStudio>(true)).Single();
                Transform generated = studio.transform.Find("Generated Background");
                Assert.That(generated, Is.Not.Null);
                foreach (string rootName in new[] { "Clouds / 구름 순환", "Butterflies / 배경 나비" })
                {
                    Transform group = generated.Cast<Transform>().Single(child => child.name == rootName);
                    Assert.That(group.GetComponentsInChildren<Camera>(true), Is.Empty);
                    Assert.That(group.GetComponentsInChildren<Canvas>(true), Is.Empty);
                    Assert.That(group.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                    Assert.That(group.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Animator[] animators = group.GetComponentsInChildren<Animator>(true);
                    Assert.That(animators.Length, Is.GreaterThan(0));
                    foreach (Animator animator in animators)
                    {
                        Assert.That(animator.runtimeAnimatorController, Is.Not.Null, animator.name);
                        Assert.That(AssetDatabase.Contains(animator.runtimeAnimatorController), Is.True);
                        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                        Assert.That(clips.Length, Is.GreaterThan(0), animator.name);
                        foreach (AnimationClip clip in clips)
                        {
                            Assert.That(AssetDatabase.Contains(clip), Is.True, clip.name);
                            Assert.That(clip.isLooping, Is.True, clip.name);
                            var bindings = AnimationUtility.GetCurveBindings(clip)
                                .Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).ToArray();
                            Assert.That(bindings.Length, Is.GreaterThan(0), clip.name);
                            foreach (EditorCurveBinding binding in bindings)
                            {
                                Transform target = string.IsNullOrEmpty(binding.path)
                                    ? animator.transform : animator.transform.Find(binding.path);
                                Assert.That(target, Is.Not.Null, animator.name + " / " + binding.path);
                                if (binding.type != typeof(GameObject))
                                    Assert.That(target.GetComponent(binding.type), Is.Not.Null,
                                        animator.name + " / " + binding.path + " / " + binding.propertyName);
                            }
                        }
                    }
                    foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
                        foreach (Material material in renderer.sharedMaterials)
                            Assert.That(material != null && material.shader != null, Is.True, renderer.name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
