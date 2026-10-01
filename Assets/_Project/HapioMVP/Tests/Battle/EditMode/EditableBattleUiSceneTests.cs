using System;
using System.Linq;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6.Prototype.Battle.Tests
{
    // Saved bindings and isolated preview edits only; these are not runtime or device evidence.
    public sealed class EditableBattleUiSceneTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        private Scene preview;
        private T09Hud hud;

        [SetUp]
        public void OpenOnlyTheSavedCurrentScenePreview()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            Assert.That(preview.IsValid() && preview.isLoaded, Is.True);
            hud = Components<T09Hud>(preview).Single();
        }

        [TearDown]
        public void DiscardPreviewEditsWithoutSavingTheSource()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        [Test]
        public void CurrentSceneHasPersistentLocalUiBindingsBeforePlay()
        {
            Assert.That(hud.UseSceneHierarchy, Is.True);
            Assert.That(hud.ValidateSceneHierarchy(out var error), Is.True, error);
            Assert.That(hud.gameObject, Is.SameAs(Components<SplitScreenLayout>(preview).Single().gameObject));
            Assert.That(Components<T09BattleController>(preview).Single().Hud, Is.SameAs(hud));
            Assert.That(hud.Canvas.transform.parent, Is.SameAs(hud.transform));
            Assert.That(hud.Canvas.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1));
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(hud.Canvas.GetComponent<CanvasScaler>(), Is.Not.Null);
            Assert.That(hud.Canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(hud.ResultOverlay.activeSelf, Is.False);
            Assert.That(hud.MinimalBattlePresentation, Is.True);
            var framing = Components<ThrowBattleFraming>(preview).Single();
            Assert.That(hud.MonsterHpFill, Is.Not.Null);
            Assert.That(hud.TeamTimeFill, Is.Not.Null);
            Assert.That(hud.TeamTimeValue, Is.Not.Null);
            Assert.That(hud.TeamTimeValue.gameObject.activeSelf, Is.True);
            Assert.That(hud.TeamTimeValue.raycastTarget, Is.False);
            Assert.That(hud.TeamTimeValue.fontSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(hud.TeamTimeValue.GetComponent<Outline>(), Is.Not.Null);
            Assert.That(hud.LeftNeighbourIcon, Is.Not.Null);
            Assert.That(hud.LeftNeighbourLabel, Is.Not.Null);
            Assert.That(hud.RightNeighbourIcon, Is.Not.Null);
            Assert.That(hud.RightNeighbourLabel, Is.Not.Null);
            Assert.That(hud.StaminaFill, Is.Not.Null);
            Assert.That(framing.MonsterDisplayArea, Is.Not.Null);
            Assert.That(framing.MonsterDisplayArea.GetComponent<Graphic>(), Is.Null,
                "MonsterDisplayArea defines framing only and must remain invisible.");
            Assert.That(framing.MonsterDisplayArea.GetComponent<Selectable>(), Is.Null,
                "MonsterDisplayArea must never intercept input.");
            Assert.That(hud.GenerateButton.gameObject.activeSelf, Is.True);
            Assert.That(hud.transform.Find("T09Overlay/SafeArea/UpperSafeViewport/UpperHudContent/Header").gameObject.activeSelf,
                Is.False);
            var upperViewport = (RectTransform)hud.transform.Find("T09Overlay/SafeArea/UpperSafeViewport");
            var lowerViewport = (RectTransform)hud.transform.Find("T09Overlay/SafeArea/LowerSafeViewport");
            Assert.That(upperViewport.gameObject.activeSelf, Is.True);
            Assert.That(lowerViewport.gameObject.activeSelf, Is.True);
            Assert.That(upperViewport.anchorMin, Is.EqualTo(new Vector2(0f, .45f)));
            Assert.That(upperViewport.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(lowerViewport.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(lowerViewport.anchorMax, Is.EqualTo(new Vector2(1f, .45f)));
            foreach (var content in new[] {
                hud.transform.Find("T09Overlay/SafeArea/UpperSafeViewport/UpperHudContent"),
                hud.transform.Find("T09Overlay/SafeArea/LowerSafeViewport/LowerHudContent") })
            {
                Assert.That(content.localScale.x, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                Assert.That(content.localScale.y, Is.EqualTo(content.localScale.x).Within(.0001f));
                Assert.That(content.localScale.z, Is.EqualTo(1f));
            }
            Assert.That(hud.transform.Find(
                "T09Overlay/SafeArea/LowerSafeViewport/LowerHudContent/ResourceControls").gameObject.activeSelf,
                Is.True, "The footer RectTransform still defines the orb workspace.");
            Assert.That(hud.StaminaLabel.gameObject.activeSelf, Is.False);
            Assert.That(hud.RecoveryLabel.gameObject.activeSelf, Is.False);

            var upperContent = (RectTransform)hud.transform.Find(
                "T09Overlay/SafeArea/UpperSafeViewport/UpperHudContent");
            var battleStats = (RectTransform)upperContent.Find("BattleStats");
            var hpPanel = (RectTransform)battleStats.Find("MonsterHpPanel");
            var monsterArea = framing.MonsterDisplayArea;
            var lowerContent = (RectTransform)hud.transform.Find(
                "T09Overlay/SafeArea/LowerSafeViewport/LowerHudContent");
            var footer = (RectTransform)lowerContent.Find("ResourceControls");
            var divider = (RectTransform)hud.Canvas.transform.Find("ViewportDivider");
            var teamTime = (RectTransform)divider.Find("TeamTimeBar");
            var staminaPanel = (RectTransform)footer.Find("PersonalStaminaPanel");
            var resourceButtons = (RectTransform)footer.Find("ResourceButtons");
            Assert.That(teamTime, Is.Not.Null,
                "The team time bar belongs to the camera divider, outside the clipped viewports.");
            Assert.That(hud.TeamTimeValue.transform.parent, Is.SameAs(teamTime));
            foreach (var icon in new[] { hud.LeftNeighbourIcon, hud.RightNeighbourIcon })
            {
                Assert.That(icon.transform.parent, Is.SameAs(teamTime));
                Assert.That(icon.gameObject.activeSelf, Is.False, "A room has no neighbours before seat approval.");
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(icon.raycastTarget, Is.False);
            }
            foreach (var label in new[] { hud.LeftNeighbourLabel, hud.RightNeighbourLabel })
            {
                Assert.That(label.transform.parent, Is.SameAs(label == hud.LeftNeighbourLabel
                    ? hud.LeftNeighbourIcon.transform : hud.RightNeighbourIcon.transform));
                Assert.That(label.raycastTarget, Is.False);
            }
            Assert.That(footer.Find("TeamTimeBar"), Is.Null);
            Assert.That(staminaPanel, Is.Not.Null,
                "The five-section stamina view belongs at the bottom of the orb area.");
            Assert.That(battleStats.Find("PersonalStaminaPanel"), Is.Null);
            Assert.That(hpPanel.anchorMin.x, Is.EqualTo(0f).Within(.0001f));
            Assert.That(hpPanel.anchorMax.x, Is.EqualTo(1f).Within(.0001f));
            Assert.That(staminaPanel.anchoredPosition.y, Is.LessThan(resourceButtons.anchoredPosition.y));
            Assert.That(monsterArea.anchorMax.y, Is.LessThan(battleStats.anchorMin.y));
            Assert.That(footer.sizeDelta.y,
                Is.EqualTo(ScreenLayoutConfig.MinimalBattleFooterHeight).Within(.0001f));
            Assert.That(resourceButtons.anchoredPosition.y, Is.GreaterThanOrEqualTo(staminaPanel.sizeDelta.y));
            Assert.That(hud.GenerateButton.transform.parent, Is.SameAs(resourceButtons));
            Assert.That(hud.GenerateButton.GetComponent<RectTransform>().anchorMin.x,
                Is.EqualTo(0f).Within(.0001f));
            Assert.That(hud.GenerateButton.targetGraphic.raycastTarget, Is.True,
                "The Figma artwork must remain a clickable button.");
            var staminaTrack = (RectTransform)staminaPanel.Find("ContinuousStaminaTrack");
            Assert.That(staminaTrack, Is.Not.Null);
            Assert.That(Enumerable.Range(1, 4)
                .All(index => staminaTrack.Find("TwentyMarker" + index) != null), Is.True);
            Assert.That(Enumerable.Range(1, 5)
                .All(index => staminaTrack.Find("FigmaStaminaGem" + index + "/FillClip/Artwork/FigmaAtlasArt")
                    ?.GetComponent<RawImage>() != null), Is.True);
            Assert.That(hud.Layout.BattleCamera.GetComponentInChildren<FigmaViewportBackdrop>(), Is.Not.Null);
            Assert.That(hud.Layout.OrbCamera.GetComponentInChildren<FigmaViewportBackdrop>(), Is.Not.Null);
            var defenseMarkers = hud.Canvas.GetComponentsInChildren<DefenseTouchMarker>(true);
            Assert.That(defenseMarkers, Has.Length.EqualTo(2), "Only the two real defense zones get visual guides.");
            foreach (var marker in defenseMarkers)
            {
                Assert.That(marker.transform.parent.name, Does.StartWith("DefenseZone"));
                Assert.That(marker.Outline, Is.Not.Null);
                Assert.That(marker.HoldProgress, Is.Not.Null);
                Assert.That(marker.HandIcon, Is.Not.Null);
                Assert.That(marker.GetComponentsInChildren<Graphic>(true)
                    .All(graphic => !graphic.enabled && !graphic.raycastTarget), Is.True,
                    "Defense guides start hidden and never take touches from the input zones.");
            }
            var boundary = hud.Canvas.GetComponentsInChildren<OrbWorkspaceBoundaryView>(true).Single();
            Assert.That(boundary.Hud, Is.SameAs(hud));
            Assert.That(boundary.Frame, Is.SameAs(boundary.transform));
            Assert.That(boundary.transform.parent, Is.SameAs(hud.Canvas.transform));
            Assert.That(boundary.transform.GetSiblingIndex(), Is.Zero,
                "The decorative guide belongs behind the saved HUD controls.");
            Assert.That(boundary.GetComponentsInChildren<Graphic>(true), Has.Length.EqualTo(9));
            Assert.That(boundary.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                Is.True, "The guide must never intercept orb drags or the Generate button.");
            Assert.That(boundary.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.enabled),
                Is.True, "The former translucent guide must stay hidden behind the wooden plate.");
            var tint = (RectTransform)boundary.transform.Find("OrbAreaTint");
            Assert.That(tint, Is.Not.Null);
            Assert.That(tint.GetSiblingIndex(), Is.Zero,
                "The retired area layer retains its saved ordering without drawing.");
            Assert.That(tint.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(tint.anchorMax, Is.EqualTo(Vector2.one));
            var plate = hud.Layout.OrbCamera.transform.Find("OrbWoodenPlate");
            Assert.That(plate, Is.Not.Null, "The 2D plate belongs under the orb camera.");
            var plateView = plate.GetComponent<OrbWoodenPlateView>();
            Assert.That(plateView, Is.Not.Null);
            Assert.That(plateView.Hud, Is.SameAs(hud));
            Assert.That(plateView.PlateRenderer, Is.SameAs(plate.GetComponent<SpriteRenderer>()));
            Assert.That(plateView.PlateRenderer.sprite, Is.Not.Null);
            Assert.That(plate.GetComponent<Collider2D>(), Is.Null,
                "The board is visual only; orb collisions still use their existing bounds.");

            foreach (var button in Buttons(hud))
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero,
                    button.name + " is connected by the existing controller at runtime.");

            foreach (var component in hud.Canvas.GetComponentsInChildren<Component>(true).Concat(new Component[] { hud }))
            {
                Assert.That(component, Is.Not.Null, "Saved UI must not contain a missing script.");
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        var owner = reference is Component item ? item.gameObject : reference as GameObject;
                        if (owner != null && owner.scene.IsValid())
                            Assert.That(owner.scene, Is.EqualTo(preview), component.name + "." + property.propertyPath);
                    }
                }
            }
        }

        [Test]
        public void MonsterInterferenceOverlayIsSavedAndDoesNotInterceptInput()
        {
            var controller = Components<T09BattleController>(preview).Single();
            var overlays = hud.Canvas.GetComponentsInChildren<MonsterInterferenceOverlay>(true);
            Assert.That(overlays, Has.Length.EqualTo(1));
            var overlay = overlays[0];
            Assert.That(overlay.transform.parent, Is.SameAs(hud.Canvas.transform));
            Assert.That(overlay.transform.GetSiblingIndex(),
                Is.LessThan(hud.ResultOverlay.transform.GetSiblingIndex()),
                "The confirmed result overlay remains above transient interference presentation.");

            using (var serializedController = new SerializedObject(controller))
            using (var serializedOverlay = new SerializedObject(overlay))
            {
                Assert.That(
                    serializedController.FindProperty("interferenceOverlay").objectReferenceValue,
                    Is.SameAs(overlay));
                var left = serializedOverlay.FindProperty("leftEdge").objectReferenceValue as Graphic;
                var right = serializedOverlay.FindProperty("rightEdge").objectReferenceValue as Graphic;
                var message = serializedOverlay.FindProperty("message").objectReferenceValue as Text;
                Assert.That(left, Is.Not.Null);
                Assert.That(right, Is.Not.Null);
                Assert.That(message, Is.Not.Null);
                Assert.That(left.enabled, Is.False);
                Assert.That(right.enabled, Is.False);
                Assert.That(message.enabled, Is.False);
                Assert.That(new[] { left, right, message }.All(item => !item.raycastTarget), Is.True);
                Assert.That(left.color.r, Is.GreaterThan(left.color.g).And.GreaterThan(left.color.b));
                Assert.That(right.color.r, Is.GreaterThan(right.color.g).And.GreaterThan(right.color.b));
                Assert.That(message.text, Is.EqualTo(MonsterInterferenceOverlay.DefaultMessage));
                Assert.That(message.GetComponent<Outline>(), Is.Not.Null);
            }
        }

        [Test]
        public void SavedJangsanbeomControllerContainsTheInterferenceGrabClip()
        {
            var animator = Components<Animator>(preview).Single(item =>
                item.runtimeAnimatorController != null
                && item.runtimeAnimatorController.animationClips.Any(clip => clip.name == "Grab"));
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.animationClips.Any(clip => clip.name == "Grab"), Is.True,
                "The synchronized interference motion requires the generated Jangsanbeom Grab state.");
        }

        [Test]
        public void RepeatedPreparationPreservesEditedControlsAndTheirIdentities()
        {
            Assert.That(hud.UseSceneHierarchy, Is.True);
            var canvas = hud.Canvas;
            var originalIds = canvas.GetComponentsInChildren<Transform>(true).Select(item => item.GetEntityId()).ToArray();
            int eventSystems = Components<EventSystem>(preview).Length;
            var buttonRect = (RectTransform)hud.GenerateButton.transform;
            buttonRect.anchoredPosition += new Vector2(17f, 9f);
            buttonRect.sizeDelta += new Vector2(-13f, 6f);
            var position = buttonRect.anchoredPosition;
            var size = buttonRect.sizeDelta;
            var color = new Color(.24f, .37f, .65f, .83f);
            hud.GenerateButton.targetGraphic.color = color;
            var caption = hud.GenerateButton.GetComponentInChildren<Text>(true);
            caption.fontSize = 19;
            var close = (RectTransform)hud.ResultEndButton.transform;
            close.anchorMin = new Vector2(.43f, .12f);
            var closeAnchor = close.anchorMin;

            hud.PrepareSceneHierarchy();
            hud.PrepareSceneHierarchy();

            Assert.That(hud.Canvas, Is.SameAs(canvas));
            Assert.That(canvas.GetComponentsInChildren<Transform>(true).Select(item => item.GetEntityId()),
                Is.EqualTo(originalIds), "Preparing existing UI must not replace, duplicate, or reorder its controls.");
            Assert.That(Components<EventSystem>(preview), Has.Length.EqualTo(eventSystems));
            Assert.That(buttonRect.anchoredPosition, Is.EqualTo(position));
            Assert.That(buttonRect.sizeDelta, Is.EqualTo(size));
            Assert.That(hud.GenerateButton.targetGraphic.color, Is.EqualTo(color));
            Assert.That(caption.fontSize, Is.EqualTo(19));
            Assert.That(close.anchorMin, Is.EqualTo(closeAnchor));
        }

        [Test]
        public void MissingSavedButtonIsReportedWithoutRebuildingTheUi()
        {
            var canvas = hud.Canvas;
            var ids = canvas.GetComponentsInChildren<Transform>(true).Select(item => item.GetEntityId()).ToArray();
            using (var serialized = new SerializedObject(hud))
            {
                var binding = serialized.FindProperty("<GenerateButton>k__BackingField");
                Assert.That(binding, Is.Not.Null, "GenerateButton must be a saved Inspector binding.");
                binding.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Assert.That(hud.ValidateSceneHierarchy(out var error), Is.False);
            Assert.That(error, Does.Contain("GenerateButton"));
            Assert.Throws<InvalidOperationException>(() => hud.PrepareSceneHierarchy());
            Assert.That(hud.UseSceneHierarchy, Is.True);
            Assert.That(hud.Canvas, Is.SameAs(canvas));
            Assert.That(canvas.GetComponentsInChildren<Transform>(true).Select(item => item.GetEntityId()), Is.EqualTo(ids));
        }

        [Test]
        public void UnsupportedCanvasCoordinatesAreRejectedWithoutSilentConversion()
        {
            hud.Canvas.renderMode = RenderMode.WorldSpace;
            Assert.That(hud.ValidateSceneHierarchy(out var error), Is.False);
            Assert.That(error, Does.Contain("ScreenSpaceOverlay"));
            Assert.Throws<InvalidOperationException>(() => hud.PrepareSceneHierarchy());
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
        }

        private static Button[] Buttons(T09Hud view) => new[]
        {
            view.HostButton, view.JoinButton, view.StartButton, view.SoloModeButton, view.RetryButton,
            view.LobbyButton, view.ResultEndButton, view.EndButton, view.GenerateButton, view.DebugFixtureButton
        };

        private static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
