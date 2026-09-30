using System;
using System.Linq;
using System.IO;
using UnityEditor.Build.Reporting;
using C6.Prototype.Battle;
using C6.Prototype.GameSync;
using C6.Prototype.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    public static class ElementSelectionSetup
    {
        /// <summary>Development probe build; do not regenerate the scene or change saved tuning/settings.</summary>
        public static void BuildMacValidation()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                throw new InvalidOperationException("Select the macOS build target before validation.");
            var outputRoot = Environment.GetEnvironmentVariable("C6_56_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) throw new InvalidOperationException("Set a fresh C6_56_OUTPUT_ROOT.");
            var path = Path.GetFullPath(Path.Combine(outputRoot, "C6Element56.app"));
            if (File.Exists(path) || Directory.Exists(path)) throw new InvalidOperationException("Validation build output already exists.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ContinuousTransferBuild.ScenePath }, locationPathName = path,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.Development
            });
            var receipt = new
            {
                unity = Application.unityVersion, result = report.summary.result.ToString(),
                errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                seconds = report.summary.totalTime.TotalSeconds, output = path
            };
            File.WriteAllText(Path.Combine(outputRoot, "build-receipt.json"),
                "{\"unity\":\"" + receipt.unity + "\",\"result\":\"" + receipt.result +
                "\",\"errors\":" + receipt.errors + ",\"warnings\":" + receipt.warnings +
                ",\"seconds\":" + receipt.seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Element selection Mac build failed: " + report.summary.result);
            ContinuousTransferBuild.ApplyMacDeclarations(path);
            Debug.Log("C6_56_MAC_BUILT " + path);
        }

        [MenuItem("C6/Lobby/Prepare Element Selection UI")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var scene = SceneManager.GetSceneByPath(ContinuousTransferBuild.ScenePath);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Save existing scene changes before preparing this feature.");
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ContinuousTransferBuild.ScenePath, OpenSceneMode.Additive);
            var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T10GameSession>(true)).Single();
            Undo.RecordObject(game, "Enable lobby element rules");
            game.ConfigureElementSelection(true);
            var lobby = game.GetComponent<T10LobbySession>();
            Undo.RecordObject(lobby, "Enable lobby element contract");
            lobby.ConfigureElementSelection(true);
            var hud = game.GetComponent<T09Hud>();
            Undo.RecordObject(hud, "Add element attack warning");
            hud.PrepareElementSelectionUI();
            // This newly added identity must remain below the existing HP artwork.
            var identity = hud.PlayerIdentityText;
            Undo.RecordObject(identity.rectTransform, "Place player identity below HP");
            identity.rectTransform.anchorMin = new Vector2(.025f, .900f);
            identity.rectTransform.anchorMax = new Vector2(.8f, .925f);
            identity.rectTransform.offsetMin = identity.rectTransform.offsetMax = Vector2.zero;
            var shadow = identity.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow == null) shadow = Undo.AddComponent<UnityEngine.UI.Shadow>(identity.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, .9f); shadow.effectDistance = new Vector2(1f, -1f);
            EditorUtility.SetDirty(shadow);
            if (!hud.ValidateSceneHierarchy(out var error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(lobby); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save feature connections.");
            Debug.Log("C6_56_SCENE_PREPARED protocol=56 existingCanvasPreserved=true");
        }
    }
}
