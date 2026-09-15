using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace C6.Prototype.Presentation.Tests
{
    /// <summary>Saved-scene geometry and input checks, separate from visual/device acceptance.</summary>
    public sealed class BattleLayoutPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLayout.unity";
        private SplitScreenLayout layout;
        private T04Hud hud;
        private ScreenLayoutConfig runtimeConfig;

        [UnitySetUp]
        public IEnumerator LoadSavedScene()
        {
            yield return LoadScene();
            yield return null;
            FindLayoutAndHud();
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator UnloadSceneAndDiscardOnlyTheTestConfig()
        {
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.isLoaded)
            {
                var empty = SceneManager.CreateScene("T04 Test Cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(loaded);
            }
            if (runtimeConfig != null)
                Object.Destroy(runtimeConfig);
            runtimeConfig = null;
            yield return null;
            Assert.That(Object.FindObjectsByType<SplitScreenLayout>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<T04Hud>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<EventSystem>(), Is.Empty,
                "Leaving the layout must remove its owned input loop.");
        }

        [UnityTest]
        public IEnumerator ChangingTheRatioMovesRenderingAndInputBoundaryTogether()
        {
            // Never mutate the persisted configuration asset during a test.
            runtimeConfig = Object.Instantiate(layout.Config);
            layout.Configure(runtimeConfig, layout.BattleCamera, layout.OrbCamera);
            AssertPartition(0.55f);
            float oldBoundary = layout.BottomPixelRect.yMax;
            var pointThatChangesRegion = new Vector2(Screen.width * 0.5f, Screen.height * 0.4f);
            Assert.That(layout.ContainsBottomScreenPoint(pointThatChangesRegion), Is.True);
            int changes = 0;
            layout.Changed += OnChanged;
            runtimeConfig.UpperFraction = 0.65f;
            layout.ApplyLayout();
            yield return null;
            layout.Changed -= OnChanged;
            Assert.That(changes, Is.GreaterThan(0));
            AssertPartition(0.65f);
            Assert.That(layout.BottomPixelRect.yMax, Is.LessThan(oldBoundary));
            Assert.That(layout.ContainsBottomScreenPoint(pointThatChangesRegion), Is.False);
            Assert.That(layout.TryScreenToOrbPlane(pointThatChangesRegion, out _), Is.False);

            Rect lower = layout.BottomPixelRect;
            var inside = new Vector2(lower.xMin + lower.width * 0.3f,
                lower.yMin + lower.height * 0.6f);
            Assert.That(layout.TryScreenToOrbPlane(inside, out var world), Is.True);
            Assert.That(world.z, Is.EqualTo(0f).Within(0.0001f));
            Vector3 projected = layout.OrbCamera.WorldToScreenPoint(world);
            Assert.That(Vector2.Distance(inside, new Vector2(projected.x, projected.y)),
                Is.LessThan(0.5f));
            foreach (var outside in new[]
            {
                new Vector2(lower.center.x, lower.yMax),
                new Vector2(lower.xMax, lower.center.y),
                new Vector2(lower.xMin - 1f, lower.center.y),
                new Vector2(lower.center.x, lower.yMin - 1f),
                layout.TopPixelRect.center
            })
            {
                Assert.That(layout.ContainsBottomScreenPoint(outside), Is.False,
                    $"Unexpected lower-region input at {outside}.");
                Assert.That(layout.TryScreenToOrbPlane(outside, out _), Is.False);
            }

            void OnChanged() => changes++;
        }

        [UnityTest]
        public IEnumerator HudShowsDisconnectedPlaceholdersAndDoesNotStartGameplay()
        {
            AssertSinglePresentationLoop();
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(hud.HpLabel.text, Is.EqualTo("-- / --"));
            Assert.That(hud.TimerLabel.text, Is.EqualTo("--:--"));
            Assert.That(hud.StaminaLabel.text, Is.EqualTo("-- / --"));
            Assert.That(hud.ConnectionLabel.text, Is.EqualTo("OFFLINE"));
            Assert.That(hud.GenerateButton.interactable, Is.False);
            Assert.That(hud.GenerateButton.onClick.GetPersistentEventCount(), Is.Zero);
            var decorations = hud.Canvas.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => !graphic.transform.IsChildOf(hud.GenerateButton.transform));
            Assert.That(decorations, Is.Not.Empty);
            Assert.That(decorations.All(graphic => !graphic.raycastTarget), Is.True,
                "Decorative HUD graphics must leave future battlefield/drag input available.");
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, hud.HpLabel.transform.position)
            }, hits);
            Assert.That(hits.Any(hit => hit.gameObject == hud.HpLabel.gameObject), Is.False);

            // Even an explicit test invocation of the placeholder must do nothing.
            hud.GenerateButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<Rigidbody>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<Rigidbody2D>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<Collider2D>(), Is.Empty);
            Assert.That(hud.HpLabel.text, Is.EqualTo("-- / --"));
            Assert.That(hud.TimerLabel.text, Is.EqualTo("--:--"));
            Assert.That(hud.StaminaLabel.text, Is.EqualTo("-- / --"));
        }

        [UnityTest]
        public IEnumerator HudUsesScreenSafeAreaWhileCamerasKeepTheFullScreenPartition()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            var scaler = hud.Canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution.x, Is.GreaterThan(0));
            Assert.That(scaler.referenceResolution.y, Is.GreaterThan(scaler.referenceResolution.x));
            Assert.That(hud.SafeArea, Is.Not.Null);
            var panel = hud.SafeArea.GetComponent<RectTransform>();
            Rect safe = Screen.safeArea;
            Assert.That(panel.anchorMin.x, Is.EqualTo(safe.xMin / Screen.width).Within(0.0001f));
            Assert.That(panel.anchorMin.y, Is.EqualTo(safe.yMin / Screen.height).Within(0.0001f));
            Assert.That(panel.anchorMax.x, Is.EqualTo(safe.xMax / Screen.width).Within(0.0001f));
            Assert.That(panel.anchorMax.y, Is.EqualTo(safe.yMax / Screen.height).Within(0.0001f));
            foreach (Component content in new Component[]
                { hud.HpLabel, hud.TimerLabel, hud.StaminaLabel, hud.ConnectionLabel, hud.GenerateButton })
                Assert.That(content.transform.IsChildOf(panel), Is.True);
            AssertPartition(layout.Config.UpperFraction);
        }

        [UnityTest]
        public IEnumerator ReloadingTheSavedSceneLeavesOneHudInputLoopAndListener()
        {
            AssertSinglePresentationLoop();
            var oldHud = hud;
            var oldCanvas = hud.Canvas;
            var oldEventSystem = EventSystem.current;
            yield return LoadScene();
            yield return null;
            FindLayoutAndHud();
            Assert.That(oldHud == null, Is.True);
            Assert.That(oldCanvas == null, Is.True);
            Assert.That(oldEventSystem == null, Is.True);
            AssertSinglePresentationLoop();
            Assert.That(hud.GenerateButton.interactable, Is.False);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
        }

        private void FindLayoutAndHud()
        {
            layout = Object.FindAnyObjectByType<SplitScreenLayout>();
            hud = Object.FindAnyObjectByType<T04Hud>();
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.Config, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.Layout, Is.SameAs(layout));
        }

        private void AssertSinglePresentationLoop()
        {
            Assert.That(Object.FindObjectsByType<SplitScreenLayout>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<T04Hud>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>(), Has.Length.EqualTo(2));
            Assert.That(Object.FindObjectsByType<AudioListener>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(), Has.Length.EqualTo(1));
            Assert.That(EventSystem.current.currentInputModule, Is.TypeOf<InputSystemUIInputModule>());
        }

        private void AssertPartition(float upperFraction)
        {
            float lowerFraction = 1f - upperFraction;
            Assert.That(layout.TopViewport.x, Is.EqualTo(0f));
            Assert.That(layout.TopViewport.width, Is.EqualTo(1f));
            Assert.That(layout.TopViewport.y, Is.EqualTo(lowerFraction).Within(0.0001f));
            Assert.That(layout.TopViewport.height, Is.EqualTo(upperFraction).Within(0.0001f));
            Assert.That(layout.BottomViewport.x, Is.EqualTo(0f));
            Assert.That(layout.BottomViewport.y, Is.EqualTo(0f));
            Assert.That(layout.BottomViewport.width, Is.EqualTo(1f));
            Assert.That(layout.BottomViewport.height, Is.EqualTo(lowerFraction).Within(0.0001f));
            Assert.That(layout.TopPixelRect, Is.EqualTo(layout.BattleCamera.pixelRect));
            Assert.That(layout.BottomPixelRect, Is.EqualTo(layout.OrbCamera.pixelRect));
            Assert.That(layout.TopPixelRect.xMin, Is.EqualTo(0f).Within(1f));
            Assert.That(layout.BottomPixelRect.xMin, Is.EqualTo(0f).Within(1f));
            Assert.That(layout.TopPixelRect.width, Is.EqualTo(Screen.width).Within(1f));
            Assert.That(layout.BottomPixelRect.width, Is.EqualTo(Screen.width).Within(1f));
            Assert.That(layout.BottomPixelRect.yMin, Is.EqualTo(0f).Within(1f));
            Assert.That(layout.TopPixelRect.yMax, Is.EqualTo(Screen.height).Within(1f));
            Assert.That(layout.TopPixelRect.yMin, Is.EqualTo(layout.BottomPixelRect.yMax).Within(1f));
        }

        private static AsyncOperation LoadScene()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        }
    }
}
