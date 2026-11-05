using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>Editor tools that rebuild stage assets or saved Scenes must not break or erase the current project.</summary>
    public sealed class GameEditorMenuCleanupTests
    {
        private const string EditorAssembly = "Assembly-CSharp-Editor";

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
                // Threw against the current project (existing Lab Scene, retuned throw gains, 259 DEV rows).
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
