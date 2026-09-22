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
        private const string LegacyScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
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
            Assert.That(hud.transform.Find(
                "T09Overlay/SafeArea/UpperSafeViewport/UpperHudContent").localScale, Is.EqualTo(Vector3.one));
            Assert.That(hud.transform.Find(
                "T09Overlay/SafeArea/LowerSafeViewport/LowerHudContent").localScale, Is.EqualTo(Vector3.one));
            Assert.That(hud.transform.Find(
                "T09Overlay/SafeArea/LowerSafeViewport/LowerHudContent/ResourceControls").gameObject.activeSelf,
                Is.True, "The footer RectTransform still defines the orb workspace.");
            Assert.That(hud.StaminaLabel.gameObject.activeSelf, Is.False);
            Assert.That(hud.RecoveryLabel.gameObject.activeSelf, Is.False);

            var upperContent = (RectTransform)hud.transform.Find(
                "T09Overlay/SafeArea/UpperSafeViewport/UpperHudContent");
            var battleStats = (RectTransform)upperContent.Find("BattleStats");
            var hpPanel = (RectTransform)battleStats.Find("MonsterHpPanel");
            var teamTime = (RectTransform)upperContent.Find("TeamTimeBar");
            var monsterArea = framing.MonsterDisplayArea;
            var footer = (RectTransform)hud.transform.Find(
                "T09Overlay/SafeArea/LowerSafeViewport/LowerHudContent/ResourceControls");
            var staminaPanel = (RectTransform)footer.Find("PersonalStaminaPanel");
            var resourceButtons = (RectTransform)footer.Find("ResourceButtons");
            Assert.That(staminaPanel, Is.Not.Null,
                "The five-section stamina view belongs at the bottom of the orb area.");
            Assert.That(battleStats.Find("PersonalStaminaPanel"), Is.Null);
            Assert.That(hpPanel.anchorMin.x, Is.EqualTo(0f).Within(.0001f));
            Assert.That(hpPanel.anchorMax.x, Is.EqualTo(1f).Within(.0001f));
            Assert.That(teamTime.anchorMax.y, Is.LessThan(monsterArea.anchorMin.y));
            Assert.That(monsterArea.anchorMax.y, Is.LessThan(battleStats.anchorMin.y));
            Assert.That(footer.sizeDelta.y,
                Is.EqualTo(ScreenLayoutConfig.MinimalBattleFooterHeight).Within(.0001f));
            Assert.That(resourceButtons.anchoredPosition.y, Is.GreaterThanOrEqualTo(staminaPanel.sizeDelta.y));
            Assert.That(hud.GenerateButton.transform.parent, Is.SameAs(resourceButtons));
            Assert.That(hud.GenerateButton.GetComponent<RectTransform>().anchorMin.x,
                Is.EqualTo(.32f).Within(.0001f));
            var staminaTrack = (RectTransform)staminaPanel.Find("ContinuousStaminaTrack");
            Assert.That(staminaTrack, Is.Not.Null);
            Assert.That(Enumerable.Range(1, 4)
                .All(index => staminaTrack.Find("TwentyMarker" + index) != null), Is.True);
            Assert.That(hud.Canvas.GetComponentsInChildren<Graphic>(true)
                .Any(graphic => graphic.name.IndexOf("Defense", StringComparison.OrdinalIgnoreCase) >= 0), Is.False,
                "The blue defense regions in the reference are explanatory and must not render.");

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

        [Test]
        public void OpeningTheHistoricalSceneDoesNotCreateOrMigrateItsRuntimeUi()
        {
            var legacy = EditorSceneManager.OpenPreviewScene(LegacyScenePath);
            try
            {
                var legacyHud = Components<T09Hud>(legacy).Single();
                Assert.That(legacyHud.UseSceneHierarchy, Is.False);
                Assert.That(legacyHud.Canvas, Is.Null);
                Assert.That(legacyHud.GetComponentsInChildren<Canvas>(true), Is.Empty,
                    "ExecuteAlways must not generate UI or opt an old saved scene into migration.");
            }
            finally { if (legacy.IsValid()) EditorSceneManager.ClosePreviewScene(legacy); }
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
