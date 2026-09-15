using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6.Prototype.Tests
{
    public sealed class IOSBuildSmokePlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/IOSBuildSmoke.unity";
        private IOSBuildSmokeController controller;

        private static AsyncOperation LoadSmokeScene()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        }

        [UnitySetUp]
        public IEnumerator LoadSavedSmokeScene()
        {
            yield return LoadSmokeScene();
            yield return null;
            controller = Object.FindAnyObjectByType<IOSBuildSmokeController>();
            Assert.That(controller, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator UnloadSmokeScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded)
            {
                var empty = SceneManager.CreateScene("T01 Test Cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneStartsWithBuildIdentityZeroCountAndOneInputLoop()
        {
            Assert.That(controller.BuildStamp, Is.Not.Null);
            StringAssert.Contains(controller.BuildStamp.BuildId, controller.BuildLabel.text);
            StringAssert.Contains(controller.BuildStamp.Revision, controller.BuildLabel.text);
            Assert.That(controller.Count, Is.Zero);
            Assert.That(controller.CounterLabel.text, Is.EqualTo("Clicks: 0"));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(controller.GetComponentsInChildren<Canvas>(), Has.Length.EqualTo(1));
            Assert.That(controller.GetComponentInChildren<Canvas>().renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(Object.FindObjectsByType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(),
                Has.Length.EqualTo(1));
            Assert.That(EventSystem.current.currentInputModule, Is.TypeOf<InputSystemUIInputModule>());
            var module = (InputSystemUIInputModule)EventSystem.current.currentInputModule;
            Assert.That(module.point.action.enabled, Is.True);
            Assert.That(module.leftClick.action.enabled, Is.True);
            Assert.That(controller.TapButton.transform.parent, Is.SameAs(controller.SafeArea));
            Assert.That(controller.SafeArea.GetComponent<UISafeArea>(), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RaycastAndSinglePointerClickUpdateVisibleCountExactlyOnce()
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, controller.TapButton.transform.position)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "The rendered button must be reachable through UI raycasting.");
            Assert.That(hits[0].gameObject, Is.SameAs(controller.TapButton.gameObject),
                "Decorative graphics must not block the smoke button.");

            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(controller.Count, Is.EqualTo(1));
            Assert.That(controller.CounterLabel.text, Is.EqualTo("Clicks: 1"));
            yield return null;
            Assert.That(controller.Count, Is.EqualTo(1), "A click must not be applied a second time on the next frame.");
        }

        [UnityTest]
        public IEnumerator ReloadingTheSavedSceneResetsCountWithoutDuplicatingInput()
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(controller.TapButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(controller.Count, Is.EqualTo(1));

            yield return LoadSmokeScene();
            yield return null;
            controller = Object.FindAnyObjectByType<IOSBuildSmokeController>();
            Assert.That(controller.Count, Is.Zero);
            Assert.That(controller.CounterLabel.text, Is.EqualTo("Clicks: 0"));
            Assert.That(Object.FindObjectsByType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(),
                Has.Length.EqualTo(1));
        }
    }
}
