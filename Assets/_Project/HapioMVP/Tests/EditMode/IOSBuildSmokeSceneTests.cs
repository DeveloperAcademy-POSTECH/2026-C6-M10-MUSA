using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Prototype.Tests
{
    public sealed class IOSBuildSmokeSceneTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/IOSBuildSmoke.unity";
        private const string StampPath = "Assets/_Project/HapioMVP/Config/IOSBuildStamp.asset";

        [Test]
        public void SmokeSceneRemainsAvailableInPortraitProject()
        {
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Does.Contain(ScenePath),
                "Keep the T01 regression scene available when a later task selects its own build scene.");
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
        }

        [Test]
        public void SavedSceneHasOneBootstrapLinkedToTheCanonicalBuildStamp()
        {
            var stamp = AssetDatabase.LoadAssetAtPath<IOSBuildStamp>(StampPath);
            Assert.That(stamp, Is.Not.Null, "Build identity must be an actual linked asset.");
            Assert.That(stamp.BuildId, Is.Not.Null.And.Not.Empty);
            Assert.That(stamp.Revision, Is.Not.Null.And.Not.Empty);

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForTest = !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var controllers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<IOSBuildSmokeController>(true)).ToArray();
                Assert.That(controllers, Has.Length.EqualTo(1));
                Assert.That(controllers[0].gameObject.activeInHierarchy, Is.True);
                Assert.That(controllers[0].enabled, Is.True);
                Assert.That(controllers[0].BuildStamp, Is.SameAs(stamp));
            }
            finally
            {
                if (openedForTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
