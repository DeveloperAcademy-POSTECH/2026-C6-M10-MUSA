using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    /// <summary>Editor tools that rebuild stage assets or saved Scenes must not break or erase the current project.</summary>
    public sealed class GameEditorMenuCleanupTests
    {
        private const string EditorAssembly = "Assembly-CSharp-Editor";

        [Test]
        public void MenuDeveloperRowUpgrade_KeepsTheSavedRowsAndPrefabFonts()
        {
            // Upgrade a copy: a test must not re-save Home or dirty a Home Scene that is open in the Editor.
            string copy = AssetDatabase.GenerateUniqueAssetPath("Assets/HapioMenuDeveloperRowsTest.unity");
            Assert.That(AssetDatabase.CopyAsset(GameScenePaths.Home, copy), Is.True);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
                GameScreen screen = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
                int saved = LabConfig.ClayThicknessField;
                Assert.That(screen.developerInputs, Has.Length.EqualTo(saved));
                Transform content = screen.developerInputs[0].transform.parent.parent;
                int rows = content.childCount;
                // What an older tool left behind: the runtime-only clay rows saved, and a stale default.
                Assert.That(screen.EnsureClayDeveloperRows(), Is.True);
                Assert.That(screen.developerInputs, Has.Length.EqualTo(LabConfig.DeveloperFields.Count));
                float[] defaults = AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset").CaptureDeveloperValues();
                const int stale = 240;
                LabDeveloperField field = LabConfig.DeveloperFields[stale];
                float wrong = defaults[stale] == field.Min ? field.Max : field.Min;
                screen.developerInputs[stale].SetTextWithoutNotify(wrong.ToString(CultureInfo.InvariantCulture));
                screen.developerSliders[stale].SetValueWithoutNotify(wrong);
                var prefabFonts = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    .Where(text => PrefabUtility.IsPartOfPrefabInstance(text))
                    .ToDictionary(text => text, text => text.font);
                Assert.That(prefabFonts, Is.Not.Empty, "Home holds the shared lobby and opening prefab instances.");

                Type.GetType("Hapio.Editor.GameSceneBuilder, " + EditorAssembly, true)
                    .GetMethod("UpgradeDeveloperRows", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { scene });

                Assert.That(screen.developerInputs, Has.Length.EqualTo(saved), "The clay rows stay runtime-only (#506).");
                Assert.That(screen.developerSliders, Has.Length.EqualTo(saved));
                Assert.That(content.childCount, Is.EqualTo(rows));
                Assert.That(content.Find("Setting" + saved.ToString("000")), Is.Null);
                Assert.That(float.Parse(screen.developerInputs[stale].text, CultureInfo.InvariantCulture), Is.EqualTo(defaults[stale]));
                Assert.That(screen.developerSliders[stale].value, Is.EqualTo(defaults[stale]));
                foreach (var pair in prefabFonts)
                    Assert.That(pair.Key.font, Is.SameAs(pair.Value), "Prefab instance text keeps its font: " + pair.Key.name);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(copy);
            }
        }

        [Test]
        public void TeamLobbyUpgrade_TreatsTheSavedMenuDeveloperPanelsAsCurrent()
        {
            // Otherwise Hapio/Upgrade 2v2 Lobby UI re-runs the DEV row upgrade and re-saves Home and Lobby.
            MethodInfo check = Type.GetType("Hapio.Editor.GameTeamLobbyUpgrade, " + EditorAssembly, true)
                .GetMethod("MenuDeveloperSchemaIsCurrent", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(check, Is.Not.Null);
            Assert.That((bool)check.Invoke(null, null), Is.True);
        }

        [Test]
        public void RecreatedYaguangDefaults_PassCatalogValidationAndMatchTheSavedStage()
        {
            Type assets = Type.GetType("Hapio.Editor.GameStageAssets, " + EditorAssembly, true);
            MethodInfo method = assets.GetMethod("YaguangDefaults", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "GameStageAssets.YaguangDefaults recreates a missing Stage02 asset.");
            var defaults = (GameStageValue[])method.Invoke(null, null);

            foreach (GameStageValue value in defaults)
                Assert.That(GameStageSettings.IsFieldVisible(2, value.index), Is.True,
                    "A recreated Stage02 default must be a visible yaguang field, not a retired slot: " + value.index);

            GameStageCatalog saved = GameStageCatalog.RequireDefault();
            CollectionAssert.AreEquivalent(
                saved.Get(2).defaultValues.Select(value => value.index + "=" + value.value),
                defaults.Select(value => value.index + "=" + value.value),
                "Recreating Stage02 gives the same defaults as the saved asset.");
            // #664: Stage01 keeps the fox round's 8-second BIG/SMALL when it is recreated too.
            var gumiho = (GameStageValue[])assets.GetMethod("GumihoDefaults", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            CollectionAssert.AreEquivalent(saved.Get(1).defaultValues.Select(value => value.index + "=" + value.value),
                gumiho.Select(value => value.index + "=" + value.value), "Recreating Stage01 gives the same defaults as the saved asset.");
            CollectionAssert.AreEquivalent(gumiho.Select(value => value.index + "=" + value.value), new[] { "159=8", "162=8" });

            var stage = ScriptableObject.CreateInstance<GameStageDefinition>();
            var catalog = ScriptableObject.CreateInstance<GameStageCatalog>();
            try
            {
                GameStageDefinition original = saved.Get(2);
                stage.stageIndex = original.stageIndex; stage.stageId = original.stageId;
                stage.displayName = original.displayName; stage.yokai = original.yokai;
                stage.defaultValues = defaults;
                catalog.stages = new[] { saved.Get(1), stage, saved.Get(3) };
                Assert.DoesNotThrow(catalog.Validate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void RemovedSceneOverwriteAndBrokenOneTimeMenus_StayRemoved()
        {
            string[] removed =
            {
                // Saved the Lab Scene over Battle and erased Battle-only backgrounds, floors and lighting.
                "Hapio/Upgrade Existing Game Scenes From Current Lab",
                // Threw against the current project (existing Lab Scene, retuned throw gains, 265 DEV rows).
                "C6 Lab/Build Editable Lab Scene",
                "C6 Lab/Migrate Legacy Throw Tuning",
                "C6 Lab/Upgrade Developer Controls",
                // Nothing left to remove, and re-applying restored the replaced temporary background.
                "C6 Lab/Remove Unused Template Samples",
                "C6 Lab/Apply Temporary Battle Background"
            };
            Assembly editor = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == EditorAssembly);
            string[] registered = editor.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .SelectMany(method => method.GetCustomAttributes<MenuItem>(false))
                .Select(item => item.menuItem)
                .ToArray();
            Assert.That(registered, Has.Member("Hapio/Upgrade Three Yokai Stages"), "The menu scan must see the Hapio editor tools.");
            foreach (string path in removed)
                Assert.That(registered, Has.No.Member(path), path);
            Assert.That(Type.GetType("Hapio.Editor.GameSceneBuilder, " + EditorAssembly, true)
                .GetMethod("UpgradeBattleFromSavedLab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), Is.Null,
                "No editor path may copy the saved Lab over the separate Battle Scene.");
        }
    }
}
