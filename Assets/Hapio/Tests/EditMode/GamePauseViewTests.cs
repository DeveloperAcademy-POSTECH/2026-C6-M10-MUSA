using System.Linq;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    public sealed class GamePauseViewTests
    {
        private Scene scene;
        private GamePauseView view;

        [SetUp]
        public void Open()
        {
            scene = EditorSceneManager.OpenScene(GameScenePaths.Battle, OpenSceneMode.Additive);
            view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GamePauseView>(true)).Single();
        }
        [TearDown]
        public void Close() => EditorSceneManager.CloseScene(scene, true);

        [TestCase("BGMVolume")]
        [TestCase("SFXVolume")]
        public void SliderHasCorrectHandleAndFullRange(string name)
        {
            var slider = view.GetComponentsInChildren<UnityEngine.UI.Slider>(true).Single(item => item.name == name);
            slider.SetValueWithoutNotify(0);
            Assert.That(slider.handleRect.anchorMin.x, Is.EqualTo(0));
            slider.SetValueWithoutNotify(1);
            Assert.That(slider.handleRect.anchorMin.x, Is.EqualTo(1));
            Assert.That(slider.handleRect.rect.width, Is.EqualTo(36).Within(.01));
            Assert.That(slider.handleRect.rect.height, Is.EqualTo(36).Within(.01));
            Assert.That(slider.fillRect.rect.height, Is.EqualTo(15).Within(.01));
            Assert.That(slider.spriteState.pressedSprite, Is.Not.Null);
            Assert.That(slider.spriteState.pressedSprite, Is.Not.EqualTo(((UnityEngine.UI.Image)slider.targetGraphic).sprite));
        }

        [Test]
        public void SavedSlidersUseExistingLocalAudioSettingsWithoutAddingControls()
        {
            float music = LabAudioSettings.BgmVolume, effects = LabAudioSettings.SfxVolume;
            var owner = new GameObject("Audio Settings Test");
            try
            {
                var panel = owner.AddComponent<LabAudioSettingsPanel>();
                panel.Bind(view.gameObject, null);
                typeof(LabAudioSettingsPanel).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                var sliders = view.GetComponentsInChildren<UnityEngine.UI.Slider>(true);
                Assert.That(sliders.Length, Is.EqualTo(2));
                sliders.Single(item => item.name == "BGMVolume").value = .25f;
                sliders.Single(item => item.name == "SFXVolume").value = .75f;
                Assert.That(LabAudioSettings.BgmVolume, Is.EqualTo(.25f));
                Assert.That(LabAudioSettings.SfxVolume, Is.EqualTo(.75f));
            }
            finally
            {
                LabAudioSettings.BgmVolume = music; LabAudioSettings.SfxVolume = effects;
                Object.DestroyImmediate(owner);
            }
        }
    }
}
