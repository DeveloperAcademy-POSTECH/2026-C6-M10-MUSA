using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace C6.Prototype.Presentation.Tests
{
    /// <summary>Verifies stored scene wiring; this is not proof of device rendering.</summary>
    public sealed class BattleLayoutSceneTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLayout.unity";
        private const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        private Scene scene;
        private bool openedForTest;

        [SetUp]
        public void OpenSavedScene()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedForTest = !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        [TearDown]
        public void CloseOnlyTheSceneOpenedByThisTest()
        {
            if (openedForTest && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void SavedLayoutUsesTheCanonicalRatioAssetAndRetainsEarlierScenes()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null);
            Assert.That(config.UpperFraction, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(config.LowerFraction, Is.EqualTo(0.45f).Within(0.0001f));
            var layouts = Components<SplitScreenLayout>();
            Assert.That(layouts, Has.Length.EqualTo(1));
            Assert.That(layouts[0].Config, Is.SameAs(config));
            var huds = Components<T04Hud>();
            Assert.That(huds, Has.Length.EqualTo(1));
            Assert.That(huds[0].Layout, Is.SameAs(layouts[0]));
            Assert.That(EditorBuildSettings.scenes.Select(item => item.path), Does.Contain(ScenePath),
                "The previous T04 scene remains available when a later Task is active.");
            foreach (string previous in new[] { "IOSBuildSmoke", "DirectConnectionSmoke", "CounterSmoke" })
                Assert.That(EditorBuildSettings.scenes.Select(item => item.path),
                    Does.Contain($"Assets/_Project/HapioMVP/Scenes/{previous}.unity"));
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
        }

        [Test]
        public void SavedCamerasSeparateBattleAndOrbLayersWithoutStackingOrRenderTextures()
        {
            var layout = Components<SplitScreenLayout>().Single();
            var cameras = Components<Camera>();
            Assert.That(cameras, Has.Length.EqualTo(2));
            Assert.That(cameras, Does.Contain(layout.BattleCamera));
            Assert.That(cameras, Does.Contain(layout.OrbCamera));
            Assert.That(layout.BattleCamera, Is.Not.SameAs(layout.OrbCamera));
            Assert.That(layout.BattleCamera.orthographic, Is.False);
            Assert.That(layout.OrbCamera.orthographic, Is.True);
            int battle = LayerMask.NameToLayer("C6Battle");
            int orbs = LayerMask.NameToLayer("C6Orbs");
            int background = LayerMask.NameToLayer("C6Background");
            Assert.That(new[] { battle, orbs, background }.All(layer => layer >= 0), Is.True);
            Assert.That(new[] { battle, orbs, background }.Distinct().Count(), Is.EqualTo(3));
            Assert.That(layout.BattleCamera.cullingMask & (1 << battle), Is.Not.Zero);
            Assert.That(layout.BattleCamera.cullingMask & (1 << orbs), Is.Zero);
            Assert.That(layout.OrbCamera.cullingMask & (1 << orbs), Is.Not.Zero);
            Assert.That(layout.OrbCamera.cullingMask & (1 << battle), Is.Zero);
            foreach (var camera in cameras)
            {
                Assert.That(camera.targetTexture, Is.Null);
                var urp = camera.GetComponent<UniversalAdditionalCameraData>();
                Assert.That(urp, Is.Not.Null);
                Assert.That(urp.renderType, Is.EqualTo(CameraRenderType.Base));
                Assert.That(urp.cameraStack, Is.Empty);
            }
            Assert.That(Components<AudioListener>(), Has.Length.EqualTo(1));
        }

        private T[] Components<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
