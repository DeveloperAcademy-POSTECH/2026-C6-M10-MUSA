using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using C6Lab;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    /// <summary>Saved asset wiring is separate from successful runtime play and network verification.</summary>
    public sealed class GameSceneAssetTests
    {
        [Test]
        public void GameBuildPaths_AreSeparateFromLabAndShareOneBattle()
        {
            Assert.That(GameScenePaths.All, Has.Length.EqualTo(5));
            Assert.That(GameScenePaths.All[0], Is.EqualTo(GameScenePaths.Home));
            Assert.That(GameScenePaths.All.Distinct().Count(), Is.EqualTo(5));
            foreach (string path in GameScenePaths.All)
            {
                Assert.That(path, Does.StartWith("Assets/Hapio/Scenes/"));
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(EditorBuildSettings.scenes.Any(entry => entry.path == path && entry.enabled), Is.True, path);
            }
            Assert.That(File.Exists("Assets/Lab/Scenes/Lab.unity"), Is.True);
        }

        [Test]
        public void PersistentSessionPrefab_OwnsOnlyTransportAndConfigNotSceneObjects()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Resources/Hapio/GameSession.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<GameSession>(), Is.Not.Null);
            LabNetwork network = prefab.GetComponent<LabNetwork>();
            Assert.That(network, Is.Not.Null);
            Assert.That(network.manager, Is.SameAs(prefab.GetComponent<NetworkManager>()));
            Assert.That(network.transport, Is.SameAs(prefab.GetComponent<LabDualStackTransport>()));
            Assert.That(network.config, Is.SameAs(AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset")));
            Assert.That(network.board, Is.Null);
            Assert.That(network.target, Is.Null);
            Assert.That(network.battleFloor, Is.Null);
            Assert.That(network.impactFeedback, Is.Null);
            Assert.That(network.projectileRoot, Is.Null);
            Assert.That(network.manager.NetworkConfig.EnableSceneManagement, Is.False);
            Assert.That(network.manager.NetworkConfig.NetworkTransport, Is.SameAs(network.transport));
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Is.Empty);
            var serialized = new SerializedObject(prefab.GetComponent<GameSession>());
            Assert.That(serialized.FindProperty("network").objectReferenceValue, Is.SameAs(network));
        }

        [Test]
        public void SharedGourd_KeepsItsOriginalAnimatedYokaiWithoutAuthoringViews()
        {
            GameObject prefab = Resources.Load<GameObject>("GourdPaint/WhiteGourd");
            Assert.That(prefab, Is.Not.Null);
            Transform slot = prefab.transform.Find(GameYokaiPresentation.SlotPath);
            Assert.That(slot, Is.Not.Null);
            Transform body = slot.Find("Yokai");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.gameObject.activeSelf, Is.True);
            Assert.That(body.GetComponent<Renderer>(), Is.Not.Null);
            Assert.That(body.GetComponent<Renderer>().enabled, Is.True);
            foreach (string eye in new[] { "EyePivot_L", "EyePivot_R" })
            {
                Transform pivot = slot.Find(eye);
                Assert.That(pivot, Is.Not.Null, eye);
                Assert.That(pivot.gameObject.activeSelf, Is.True, eye);
                Assert.That(pivot.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0), eye);
            }
            Animation animation = prefab.GetComponentInChildren<Animation>(true);
            Assert.That(animation, Is.Not.Null);
            Assert.That(animation.clip, Is.Not.Null, "The original lid, rise/hide and eye animation stays connected.");
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty,
                "The common yokai must not replace the player's assigned seat camera.");
            Assert.That(prefab.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true), Is.Empty);
        }

        [Test]
        public void BattleCopy_BindsSavedBoardTargetHudWithoutAnotherStandaloneNetwork()
        {
            WithScene(GameScenePaths.Battle, scene =>
            {
                Assert.That(All<LabNetwork>(scene), Is.Empty);
                Assert.That(All<NetworkManager>(scene), Is.Empty);
                Assert.That(All<LabLobbyView>(scene), Is.Empty, "Standalone Lab navigation must never be copied into the game Battle.");
                GameBattleBindings bindings = All<GameBattleBindings>(scene).Single();
                Assert.That(bindings.board, Is.Not.Null);
                Assert.That(bindings.target, Is.Not.Null);
                Assert.That(bindings.battleFloor, Is.Not.Null);
                Assert.That(bindings.impactFeedback, Is.Not.Null);
                Assert.That(bindings.projectileRoot, Is.Not.Null);
                Assert.That(bindings.battleCamera, Is.Not.Null);
                Assert.That(bindings.sceneController.board, Is.SameAs(bindings.board));
                Assert.That(bindings.seatCamera.target, Is.SameAs(bindings.target));
                Assert.That(bindings.hud, Is.Not.Null);
                Assert.That(bindings.developerMode, Is.Not.Null);
                Assert.That(bindings.stageLabel, Is.Not.Null);
                Assert.That(bindings.developerNextStageButton, Is.Not.Null);
                Assert.That(bindings.developerNextStageButton.transform.parent, Is.SameAs(bindings.developerMode.panel.transform));
                Assert.That(bindings.developerNextStageButton.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
                Assert.That(bindings.yokaiPresentation, Is.Not.Null);
                Assert.That(bindings.yokaiPresentation.gameObject.scene, Is.EqualTo(scene));
                Assert.That(All<LabJigwiHud>(scene).Single().network, Is.Null,
                    "The copied Jigwi controls bind to the persistent room at Awake.");
                Assert.That(bindings.stageLabel.color, Is.EqualTo(new Color(.16f, .12f, .09f, 1f)), "Stage text needs dark ink over the light paper plate.");
                Assert.That(bindings.hud.network, Is.Null, "The persistent network is bound by the adapter before battle Awake.");
                Assert.That(bindings.target.Hitbox.sharedMesh, Is.Not.Null);
                Assert.That(bindings.developerMode.sourceConfig, Is.SameAs(SavedConfig()), "DEV defaults keep the saved source; runtime config is bound separately.");
                Assert.That(bindings.developerMode.valueInputs.All(input => input != null), Is.True);
                Assert.That(bindings.developerMode.valueSliders.All(slider => slider != null), Is.True);
                Assert.That(bindings.hud.leftNeighborBadge, Is.Not.Null, "Charm bubbles use these saved neighbor anchors.");
                Assert.That(bindings.hud.rightNeighborBadge, Is.Not.Null);
                Assert.That(bindings.sceneController.GetComponent<LabDrumMinimap>().gapAboveBadge,
                    Is.EqualTo(120f), "The saved minimap must leave room for the charm bubbles.");
            });
        }

        [Test]
        public void BattleCopy_PreservesTheCurrentSavedLabEnvironment()
        {
            WithScene("Assets/Lab/Scenes/Lab.unity", source => WithScene(GameScenePaths.Battle, battle =>
            {
                LabBackgroundStudio original = All<LabBackgroundStudio>(source).Single();
                LabBackgroundStudio studio = All<LabBackgroundStudio>(battle).Single();
                GameBattleBindings bindings = All<GameBattleBindings>(battle).Single();
                Assert.That(studio.target, Is.SameAs(bindings.target.transform));
                Assert.That(studio.collisionFloor, Is.SameAs(bindings.battleFloor.transform));
                Assert.That(studio.boardCamera, Is.SameAs(bindings.sceneController.orbCamera));
                Assert.That(studio.groundPng, Is.Not.Null);
                AssertSameSavedAsset(studio.groundPng, original.groundPng, "Ground texture");
                AssertSameSavedAsset(studio.paperPng, original.paperPng, "Surround texture");
                AssertSameSavedAsset(studio.boardBackgroundPng, original.boardBackgroundPng, "Board backing texture");
                Assert.That(studio.groundSize, Is.EqualTo(original.groundSize));
                Assert.That(studio.groundTiling, Is.EqualTo(original.groundTiling));
                Assert.That(studio.showGround && studio.showSurround && studio.fillBoardBackground, Is.True);
                Assert.That(studio.transform.Find("Generated Background"), Is.Not.Null);
                Assert.That(studio.decorations.Count, Is.EqualTo(original.decorations.Count));
                for (int i = 0; i < original.decorations.Count; i++)
                {
                    LabBackgroundStudio.Decoration expected = original.decorations[i];
                    LabBackgroundStudio.Decoration actual = studio.decorations[i];
                    Assert.That(actual.stableId, Is.EqualTo(expected.stableId));
                    AssertSameSavedAsset(actual.png, expected.png, expected.title);
                    AssertSameSavedAsset(actual.material, expected.material, expected.title);
                    Assert.That(actual.visible, Is.EqualTo(expected.visible));
                    Assert.That(actual.count, Is.EqualTo(expected.count));
                }
                Dictionary<string, LabEnvironmentCard> expectedCards = All<LabEnvironmentCard>(source)
                    .ToDictionary(card => HierarchyPath(card.transform), card => card);
                LabEnvironmentCard[] actualCards = All<LabEnvironmentCard>(battle);
                Assert.That(actualCards.Length, Is.GreaterThan(0), "The Battle must contain the saved 3D environment cards.");
                Assert.That(actualCards.Length, Is.EqualTo(expectedCards.Count));
                foreach (LabEnvironmentCard card in actualCards)
                {
                    string path = HierarchyPath(card.transform);
                    Assert.That(expectedCards.ContainsKey(path), Is.True, path);
                    LabEnvironmentCard expected = expectedCards[path];
                    AssertSameSavedAsset(card.png, expected.png, path);
                    Assert.That(card.gameObject.activeSelf, Is.EqualTo(expected.gameObject.activeSelf), path);
                    Assert.That(Vector3.Distance(card.transform.localPosition, expected.transform.localPosition), Is.LessThan(.0001f), path);
                    Assert.That(Mathf.Abs(Quaternion.Dot(card.transform.localRotation, expected.transform.localRotation)), Is.EqualTo(1f).Within(.0001f), path);
                    Assert.That(Vector3.Distance(card.transform.localScale, expected.transform.localScale), Is.LessThan(.0001f), path);
                    Assert.That(card.opacity, Is.EqualTo(expected.opacity), path);
                    AssertSameSavedAsset(card.GetComponent<MeshRenderer>().sharedMaterial,
                        expected.GetComponent<MeshRenderer>().sharedMaterial, path);
                }
            }));
        }

        [TestCase("Assets/Hapio/Scenes/Home.unity", GameScreenKind.Home)]
        [TestCase("Assets/Hapio/Scenes/Lobby.unity", GameScreenKind.Lobby)]
        [TestCase("Assets/Hapio/Scenes/StageResult.unity", GameScreenKind.StageResult)]
        [TestCase("Assets/Hapio/Scenes/GameOver.unity", GameScreenKind.GameOver)]
        public void MenuScreens_HaveSavedEditableCanvasAndInput(string path, GameScreenKind expected)
        {
            WithScene(path, scene =>
            {
                GameScreen screen = All<GameScreen>(scene).Single();
                Assert.That(screen.kind, Is.EqualTo(expected));
                Assert.That(screen.title, Is.Not.Null); Assert.That(screen.status, Is.Not.Null); Assert.That(screen.details, Is.Not.Null);
                Canvas canvas = screen.GetComponent<Canvas>();
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
                Assert.That(canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(390, 844)));
                Assert.That(All<UnityEngine.EventSystems.EventSystem>(scene), Has.Length.EqualTo(1));
                Assert.That(All<GameSafeArea>(scene), Has.Length.EqualTo(1));
                Assert.That(All<LabNetwork>(scene), Is.Empty);
                if (expected == GameScreenKind.Home)
                {
                    Assert.That(screen.hostButton, Is.Not.Null); Assert.That(screen.searchButton, Is.Not.Null);
                    Assert.That(screen.joinDirectButton, Is.Not.Null); Assert.That(screen.addressInput, Is.Not.Null); Assert.That(screen.portInput, Is.Not.Null);
                    Assert.That(screen.roomButtons, Has.Length.EqualTo(LabBonjourDiscovery.MaximumServices));
                    Assert.That(screen.roomButtons.All(row => row != null), Is.True);
                    Assert.That(screen.soloButton, Is.Not.Null);
                }
                if (expected == GameScreenKind.Lobby)
                {
                    Assert.That(screen.slotButtons, Has.Length.EqualTo(5));
                    Assert.That(screen.slotButtons.All(button => button != null), Is.True);
                    Assert.That(screen.startButton, Is.Not.Null); Assert.That(screen.homeButton, Is.Not.Null);
                }
                if (expected == GameScreenKind.StageResult)
                {
                    Assert.That(screen.nextButton, Is.Not.Null); Assert.That(screen.homeButton, Is.Not.Null);
                    Assert.That(((RectTransform)screen.nextButton.transform).anchorMax.x,
                        Is.LessThan(((RectTransform)screen.homeButton.transform).anchorMin.x), "NEXT and QUIT must have separate saved hit targets.");
                    Assert.That(screen.viewerImage.GetComponent<GamePreviewInput>(), Is.Not.Null);
                    Assert.That(screen.viewerPanel.activeSelf, Is.False);
                    Assert.That(screen.resetViewerButton, Is.Not.Null); Assert.That(screen.closeViewerButton, Is.Not.Null);
                }
                if (expected == GameScreenKind.GameOver) Assert.That(screen.homeButton, Is.Not.Null);
                if (expected == GameScreenKind.Home || expected == GameScreenKind.Lobby)
                {
                    Assert.That(screen.developerInputs, Has.Length.EqualTo(LabConfig.DeveloperFields.Count));
                    Assert.That(screen.developerSliders, Has.Length.EqualTo(LabConfig.DeveloperFields.Count));
                    Assert.That(screen.developerInputs.All(value => value != null), Is.True);
                    Assert.That(screen.developerSliders.All(value => value != null), Is.True);
                    Assert.That(screen.developerPanel.activeSelf, Is.False);
                    Assert.That(screen.developerScopeButtons, Has.Length.EqualTo(5));
                    Assert.That(screen.developerScopeButtons.All(button => button != null), Is.True);
                    Assert.That(screen.developerScopeLabel, Is.Not.Null);
                    Assert.That(screen.developerScopeButtons.Select(button => button.GetComponentInChildren<UnityEngine.UI.Text>().text),
                        Is.EqualTo(new[] { "공통", "구미호", "야광귀", "지귀", "2:2" }));
                    Font sharedFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                    Assert.That(sharedFont, Is.Not.Null);
                    foreach (UnityEngine.UI.Text text in screen.developerPanel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                        Assert.That(text.font, Is.SameAs(sharedFont), "Korean stage/DEV labels use the existing project font.");
                    Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(205), "The current Lab schema includes Flash, counting and Jigwi stage scheduling.");
                    Assert.That(SavedConfig().GourdColorComboEnabled, Is.False);
                    Assert.That(SavedConfig().SabotageMixEnabled, Is.False);
                    float[] defaults = SavedConfig().CaptureDeveloperValues();
                    for (int i = 0; i < defaults.Length; i++)
                    {
                        LabDeveloperField field = LabConfig.DeveloperFields[i];
                        var input = screen.developerInputs[i];
                        var slider = screen.developerSliders[i];
                        string row = "Setting" + i.ToString("000");
                        Assert.That(input.transform.parent.name, Is.EqualTo(row));
                        Assert.That(slider.transform.parent, Is.SameAs(input.transform.parent));
                        string label = field.Label + (field.IsChoice ? " · " + string.Join(" / ", field.Choices) : "");
                        Assert.That(input.transform.parent.Find("Label").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo(label));
                        Assert.That(slider.minValue, Is.EqualTo(field.Min));
                        Assert.That(slider.maxValue, Is.EqualTo(field.Max));
                        Assert.That(slider.wholeNumbers, Is.EqualTo(field.WholeNumber));
                        Assert.That(float.Parse(input.text, CultureInfo.InvariantCulture), Is.EqualTo(defaults[i]), row);
                        Assert.That(slider.value, Is.EqualTo(defaults[i]), row);
                    }
                }
            });
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static void AssertSameSavedAsset(UnityEngine.Object actual, UnityEngine.Object expected, string context)
        {
            // Distinct managed wrappers can both compare null under Unity's object semantics.
            // Match source availability first; live references must identify the same saved asset.
            Assert.That(actual == null, Is.EqualTo(expected == null), context);
            if (expected == null) return;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(expected, out string expectedGuid, out long expectedId), Is.True, context);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual, out string actualGuid, out long actualId), Is.True, context);
            Assert.That(actualGuid, Is.EqualTo(expectedGuid), context);
            Assert.That(actualId, Is.EqualTo(expectedId), context);
        }
        private static LabConfig SavedConfig() => AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset");
        private static string HierarchyPath(Transform transform) => transform.parent == null
            ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;
        private static void WithScene(string path, Action<Scene> check)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { check(scene); }
            finally
            {
                if (owned) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
