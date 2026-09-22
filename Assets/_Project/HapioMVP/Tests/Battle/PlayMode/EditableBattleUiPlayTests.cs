using System;
using System.Collections;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace C6.Prototype.Battle.Tests
{
    // Loads the current authored scene. Button/NGO checks use an explicit solo fixture,
    // not a successful multiplayer lobby, physical Touch, or device validation.
    public sealed class EditableBattleUiPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        private const string Port = "25149";
        private T09BattleController controller;
        private T09Hud hud;
        private bool previousRunInBackground;

        [UnitySetUp]
        public IEnumerator LoadTheCurrentSavedSceneWithoutStartingAConnection()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
            yield return null; yield return null;
            controller = Object.FindAnyObjectByType<T09BattleController>();
            Assert.That(controller, Is.Not.Null);
            hud = controller.Hud;
            Assert.That(hud.UseSceneHierarchy, Is.True);
            Assert.That(hud.ValidateSceneHierarchy(out var error), Is.True, error);
            Assert.That(controller.Attack.Connected, Is.False);
            Assert.That(controller.Views, Is.Empty);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator CloseOnlyTheFixtureConnectionAndUnloadItsObjects()
        {
            try
            {
                if (controller != null)
                {
                    controller.ConfigureApprovedLifecycle(null, null, null);
                    controller.EndDevelopmentTest();
                }
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                    "The editable UI test connection did not finish shutting down.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("Editable Battle UI Cleanup"));
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null; yield return null;
                Assert.That(Object.FindObjectsByType<T09Hud>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<EventSystem>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator RuntimeKeepsOneAuthoredCanvasAndOneEventSystemAcrossReactivation()
        {
            var canvas = hud.Canvas;
            var generate = hud.GenerateButton;
            Assert.That(canvas.gameObject.activeSelf, Is.False,
                "The normal coordinated scene initially leaves the battle Canvas behind the lobby.");
            Assert.That(hud.ResultOverlay.activeSelf, Is.False);
            Assert.That(hud.GetComponentsInChildren<Canvas>(true).Count(item => item == canvas), Is.EqualTo(1));
            Assert.That(hud.transform.Cast<Transform>().Count(item => item.name == canvas.name), Is.EqualTo(1),
                "Awake must not append a second runtime overlay to the saved one.");
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include), Has.Length.EqualTo(1));

            UseExplicitLocalUiFixture();
            var position = ((RectTransform)generate.transform).anchoredPosition + new Vector2(8f, -4f);
            ((RectTransform)generate.transform).anchoredPosition = position;
            hud.enabled = false;
            canvas.gameObject.SetActive(false);
            yield return null;
            hud.enabled = true;
            canvas.gameObject.SetActive(true);
            yield return null; yield return null;

            Assert.That(hud.Canvas, Is.SameAs(canvas));
            Assert.That(hud.GenerateButton, Is.SameAs(generate));
            Assert.That(((RectTransform)generate.transform).anchoredPosition, Is.EqualTo(position));
            Assert.That(canvas.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include), Has.Length.EqualTo(1));
            Assert.That(hud.ValidateSceneHierarchy(out var error), Is.True, error);
        }

        [UnityTest]
        public IEnumerator ResourcesAndResultsUpdateWithoutMovingEditedControlsOrTheWorkspace()
        {
            UseExplicitLocalUiFixture();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var generate = (RectTransform)hud.GenerateButton.transform;
            generate.anchoredPosition += new Vector2(11f, 5f);
            var editedPosition = generate.anchoredPosition;
            var editedColor = new Color(.28f, .43f, .61f, .87f);
            hud.GenerateButton.targetGraphic.color = editedColor;
            var caption = hud.GenerateButton.GetComponentInChildren<Text>(true);
            caption.fontSize = 17;
            var close = (RectTransform)hud.ResultEndButton.transform;
            close.anchorMin = new Vector2(.35f, .1f);
            close.anchorMax = new Vector2(.91f, .93f);
            close.offsetMin = new Vector2(7f, 3f);
            close.offsetMax = new Vector2(-9f, -2f);
            var anchorMin = close.anchorMin;
            var anchorMax = close.anchorMax;
            var offsetMin = close.offsetMin;
            var offsetMax = close.offsetMax;
            var workspace = hud.OrbWorkspaceScreenRect;
            Assert.That(workspace.width, Is.GreaterThan(1f));
            Assert.That(workspace.height, Is.GreaterThan(1f));

            hud.SetNetworkFieldsVisible(true);
            Assert.That(hud.IPv4Input.transform.parent.gameObject.activeSelf, Is.False,
                "Minimal battle UI keeps connection fields in the lobby rather than the play HUD.");
            hud.SetNetworkFieldsVisible(false);
            Canvas.ForceUpdateCanvases();
            Assert.That(hud.OrbWorkspaceScreenRect, Is.EqualTo(workspace));
            hud.SetProgress(80, 100, 1, 2, 1);
            hud.SetResources(45.5d, 100d, 20d, 20d / 3d, 3, 20, false, false);
            Assert.That(hud.HpLabel.text, Is.EqualTo("80 / 100"));
            Assert.That(hud.StaminaLabel.text, Is.EqualTo("45.5 / 100"));
            Assert.That(hud.StorageLabel.text, Is.EqualTo("ORBS 3 / 20"));
            Assert.That(caption.text, Does.Contain("20"));
            Assert.That(hud.MonsterHpFill.anchorMax.x, Is.EqualTo(.8f).Within(.00001f));
            Assert.That(hud.StaminaFill.anchorMax.x, Is.EqualTo(.455f).Within(.00001f));
            var staminaTrack = hud.StaminaFill.parent;
            var gems = Enumerable.Range(1, 5)
                .Select(index => (RectTransform)staminaTrack.Find("FigmaStaminaGem" + index + "/FillClip"))
                .ToArray();
            Assert.That(gems.Select(gem => gem.anchorMax.x),
                Is.EqualTo(new[] { 1f, 1f, .275f, 0f, 0f }).Within(.00001f));

            hud.SetBattle("Playing", 90, 90, 180, 2, false, false, false, 80, 45.5, false);
            Assert.That(hud.TeamTimeFill.anchorMax.x, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(hud.TeamTimeValue.text, Is.EqualTo("TEAM HP 90.0  /  TIME 90.0s"));

            hud.SetBattle("Victory", 35, 35, 180, 2, false, false, false, 0, 45.5, true);
            Assert.That(hud.ResultOverlay.activeInHierarchy, Is.True);
            Assert.That(hud.ResultTitle.text, Is.EqualTo("VICTORY"));
            Assert.That(hud.ResultSummary.text, Does.Contain("0 / 100").And.Contain("45.5 / 100"));
            Assert.That(hud.RetryButton.isActiveAndEnabled && hud.RetryButton.interactable, Is.True);
            hud.SetBattle("Defeat", 0, 0, 180, 2, false, false, false, 80, 45.5, false);
            Assert.That(hud.ResultTitle.text, Is.EqualTo("DEFEAT"));
            Assert.That(hud.RetryButton.gameObject.activeSelf || hud.LobbyButton.gameObject.activeSelf, Is.False);
            Assert.That(hud.ResultEndButton.gameObject.activeSelf, Is.True);
            Assert.That(close.anchorMin, Is.EqualTo(anchorMin));
            Assert.That(close.anchorMax, Is.EqualTo(anchorMax));
            Assert.That(close.offsetMin, Is.EqualTo(offsetMin));
            Assert.That(close.offsetMax, Is.EqualTo(offsetMax));
            hud.SetBattle("Ready", 180, 180, 180, 2, false, false, false, 100, 100, true);
            Assert.That(hud.ResultOverlay.activeSelf, Is.False);
            Assert.That(generate.anchoredPosition, Is.EqualTo(editedPosition));
            Assert.That(hud.GenerateButton.targetGraphic.color, Is.EqualTo(editedColor));
            Assert.That(caption.fontSize, Is.EqualTo(17));
        }

        [UnityTest]
        public IEnumerator OrbWorkspaceGuideFollowsTheLiveFooterWithoutTakingInput()
        {
            UseExplicitLocalUiFixture();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var boundary = hud.Canvas.GetComponentInChildren<OrbWorkspaceBoundaryView>(true);
            Assert.That(boundary, Is.Not.Null);
            Assert.That(boundary.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                Is.True);
            AssertBoundaryMatchesWorkspace(boundary, hud.OrbWorkspaceScreenRect);

            var footer = (RectTransform)hud.Canvas.transform.Find(
                "SafeArea/LowerSafeViewport/LowerHudContent/ResourceControls");
            var previousBottom = hud.OrbWorkspaceScreenRect.yMin;
            footer.sizeDelta += new Vector2(0f, 20f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(hud.OrbWorkspaceScreenRect.yMin, Is.GreaterThan(previousBottom + 1f));
            AssertBoundaryMatchesWorkspace(boundary, hud.OrbWorkspaceScreenRect);
        }

        private static void AssertBoundaryMatchesWorkspace(OrbWorkspaceBoundaryView boundary, Rect workspace)
        {
            var corners = new Vector3[4];
            boundary.Frame.GetWorldCorners(corners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            Assert.That(lower.x, Is.EqualTo(workspace.xMin).Within(1f));
            Assert.That(lower.y, Is.EqualTo(workspace.yMin).Within(1f));
            Assert.That(upper.x, Is.EqualTo(workspace.xMax).Within(1f));
            Assert.That(upper.y, Is.EqualTo(workspace.yMax).Within(1f));
        }

        [UnityTest]
        public IEnumerator OneRuntimeGenerateClickCreatesOnePaidRawOrbInsideTheVisibleWorkspace()
        {
            UseExplicitLocalUiFixture();
            controller.ConfigureDevelopmentSolo(true);
            Assert.That(controller.StartDevelopmentHost(Port), Is.True);
            yield return WaitFor(() => controller.Battle.CanStart && controller.Resource.IsHost
                && controller.Combination.IsHost, "The explicit UI fixture did not reach solo Ready.");
            Assert.That(hud.StartButton.interactable, Is.True);
            hud.StartButton.onClick.Invoke();
            yield return WaitFor(() => controller.CanInteract, "The saved Start button did not start its battle.");
            Assert.That(controller.Views, Is.Empty);
            Assert.That(hud.GenerateButton.interactable, Is.True);

            // Reactivation must not accumulate additional button callbacks.
            hud.enabled = false;
            hud.enabled = true;
            hud.GenerateButton.onClick.Invoke();
            yield return WaitFor(() => controller.Resource.LastResult != null && controller.Views.Count != 0,
                "The saved Generate button did not produce a confirmed orb view.");
            yield return null;
            var reply = controller.Resource.LastResult;
            Assert.That(reply.known && reply.accepted, Is.True, reply.reason);
            Assert.That(reply.staminaBefore - reply.staminaAfter, Is.EqualTo(20d).Within(1e-8));
            Assert.That(reply.confirmedOrb.kind, Is.EqualTo((int)OrbKind.Raw));
            Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(1),
                "A single click must not be wired to generation twice.");
            Assert.That(controller.Views, Has.Count.EqualTo(1));
            var view = controller.Views.Values.Single();
            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
            var workspace = hud.OrbWorkspaceScreenRect;
            var point = controller.GetViewScreenPosition(view.OrbId);
            Assert.That(workspace.Contains(point), Is.True);
            var label = view.GetComponentInChildren<TextMesh>();
            Assert.That(label, Is.Not.Null);
            float captionBottom = controller.Layout.OrbCamera.WorldToScreenPoint(label.GetComponent<Renderer>().bounds.min).y;
            Assert.That(captionBottom, Is.GreaterThanOrEqualTo(workspace.yMin - .5f));
            var framing = controller.GetComponent<ThrowBattleFraming>();
            Assert.That(framing, Is.Not.Null);
            Assert.That(framing.ApplyFraming(), Is.True, framing.FitStatus);
            Assert.That(framing.ProjectedMonsterRect.yMin, Is.GreaterThanOrEqualTo(framing.EffectiveScreenRect.yMin - .5f));
            Assert.That(framing.ProjectedMonsterRect.yMax, Is.LessThanOrEqualTo(framing.EffectiveScreenRect.yMax + .5f));
        }

        private void UseExplicitLocalUiFixture()
        {
            // The Battle test assembly intentionally does not depend on GameSync or Lobby.
            // Disable coordinated room supervision before opening the existing dev-solo path.
            foreach (string typeName in new[] { "C6.Prototype.GameSync.T10GameSession",
                "C6.Prototype.Lobby.T10LobbyController", "C6.Prototype.Lobby.T10LobbySession" })
            {
                var service = controller.GetComponents<MonoBehaviour>().Single(item => item.GetType().FullName == typeName);
                service.enabled = false;
            }
            controller.ConfigureApprovedLifecycle(null, null, null);
            foreach (var canvas in controller.GetComponentsInChildren<Canvas>(true))
                if (canvas != hud.Canvas) canvas.gameObject.SetActive(false);
            hud.Canvas.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
