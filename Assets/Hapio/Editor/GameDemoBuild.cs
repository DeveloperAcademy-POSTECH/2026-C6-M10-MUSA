using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using C6Lab;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    /// <summary>Explicit demo exports; saved scene lists, defines and normal player identity remain unchanged.</summary>
    public static class GameDemoBuild
    {
        public const string ProductName = "Hapio Yaguang Demo";
        public const string DemoVersion = "0.1.0";
        public const string DemoBuildNumber = "1";
        private const string LastOutput = "Hapio.LastYaguangDemoBuild";
        private const string ManifestFileName = "build-manifest.json";

        [MenuItem("Hapio/Demo Build/Release/Build Mac App")]
        public static void Mac() => Build(BuildTarget.StandaloneOSX, false);
        [MenuItem("Hapio/Demo Build/Release/Export iOS Xcode Project")]
        public static void Ios() => Build(BuildTarget.iOS, false);
        [MenuItem("Hapio/Demo Build/Development QA/Build Mac App")]
        public static void MacDevelopment() => Build(BuildTarget.StandaloneOSX, true);
        [MenuItem("Hapio/Demo Build/Development QA/Export iOS Xcode Project")]
        public static void IosDevelopment() => Build(BuildTarget.iOS, true);
        [MenuItem("Hapio/Demo Build/Open Last Output")]
        public static void OpenOutput()
        {
            string path = SessionState.GetString(LastOutput, string.Empty);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) EditorUtility.RevealInFinder(path);
            else Debug.LogWarning("HAPIO_DEMO_BUILD_OUTPUT_UNAVAILABLE: complete a demo build first.");
        }

        /// <summary>Only this player's compilation receives the variant; no saved scripting symbols are changed.</summary>
        public static BuildPlayerOptions CreateOptions(BuildTarget target, string output, bool development)
        {
            RequireTarget(target);
            return new BuildPlayerOptions
            {
                scenes = (string[])GameScenePaths.All.Clone(),
                target = target,
                locationPathName = output,
                options = development ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = new[] { LabGameBuildVariant.YaguangDemoDefine }
            };
        }

        public static string DemoApplicationIdentifier(string normalIdentifier)
        {
            if (string.IsNullOrWhiteSpace(normalIdentifier))
                throw new InvalidOperationException("The normal application identifier must be set before exporting a demo.");
            if (normalIdentifier.EndsWith(".yaguangdemo", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The saved application identifier already has the demo suffix. Restore the normal identifier first.");
            return normalIdentifier + ".yaguangdemo";
        }

        /// <summary>Read-only: missing authored assets must be prepared explicitly, never generated during a build.</summary>
        public static void Validate()
        {
            foreach (string scene in GameScenePaths.All)
                if (!File.Exists(scene) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new InvalidOperationException("The demo needs the saved game scene: " + scene);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard scene edits before exporting a demo. The build does not save scenes.");
            GameStageCatalog catalog = GameStageCatalog.RequireDefault();
            GameStageDefinition stage = catalog.Get(2);
            if (stage.yokai != LabStageYokai.Yaguang || stage.stageIndex != 2)
                throw new InvalidOperationException("The demo must use the original Stage 2 Yaguang definition.");
            if (AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset") == null)
                throw new InvalidOperationException("The saved LabConfig is required; the demo never creates tuning defaults.");
            YaguangDemoStoryAssets.Validate();
        }

        private static void Build(BuildTarget target, bool development)
        {
            RequireTarget(target);
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish Play mode and asset compilation before exporting a demo.");
            NamedBuildTarget namedTarget = NamedTarget(target);
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Install the Unity build support module for " + target + " first.");
            if (PlayerSettings.GetScriptingDefineSymbols(namedTarget).Split(';').Contains(LabGameBuildVariant.YaguangDemoDefine))
                throw new InvalidOperationException("Remove the demo symbol from saved Player Settings. Only the dedicated demo build may supply it.");
            Validate();

            string normalIdentifier = PlayerSettings.GetApplicationIdentifier(namedTarget);
            string identifier = DemoApplicationIdentifier(normalIdentifier);
            string run = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + (development ? "-qa" : "-release");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(project, "Builds", "HapioDemo", run);
            string output = Path.Combine(folder, target == BuildTarget.iOS ? "iOS"
                : "macOS/Hapio-Yaguang-Demo" + (development ? "-QA.app" : ".app"));
            if (Directory.Exists(folder) || File.Exists(folder))
                throw new InvalidOperationException("The demo output already exists: " + folder);
            BuildManifest manifest = CreateManifest(target, development, identifier, run, project);
            Directory.CreateDirectory(folder);
            string manifestPath = Path.Combine(folder, ManifestFileName);
            WriteManifest(manifestPath, manifest);
            BuildReport report = null;
            try
            {
                Debug.Log("HAPIO_DEMO_BUILD_START target=" + target + " development=" + development + " output=" + output);
                WithTemporaryEditorBuildState(target, () => WithTemporaryPlayerSettings(target, identifier,
                    () => report = BuildPipeline.BuildPlayer(CreateOptions(target, output, development))));
                manifest.result = report == null ? "NoReport" : report.summary.result.ToString();
                manifest.totalErrors = report == null ? -1 : (int)report.summary.totalErrors;
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("HAPIO_DEMO_BUILD_FAILED result=" + manifest.result + " output=" + output);
                ValidateOutputIdentity(target, output, identifier);
                manifest.identityVerified = true;
                SessionState.SetString(LastOutput, folder);
                Debug.Log("HAPIO_DEMO_BUILD_OK output=" + output + " manifest=" + manifestPath);
            }
            catch
            {
                if (report == null) manifest.result = "Exception";
                else if (report.summary.result == BuildResult.Succeeded) manifest.result = "OutputValidationFailed";
                throw;
            }
            finally { WriteManifest(manifestPath, manifest); }
        }

        // Kept as a single scope so a failed/cancelled BuildPlayer cannot leak demo identity or DEV into the Editor.
        private static void WithTemporaryPlayerSettings(BuildTarget target, string identifier, Action action)
        {
            NamedBuildTarget namedTarget = NamedTarget(target);
            string previousName = PlayerSettings.productName;
            string previousIdentifier = PlayerSettings.GetApplicationIdentifier(namedTarget);
            string previousVersion = PlayerSettings.bundleVersion;
            string previousBuildNumber = PlayerSettings.iOS.buildNumber;
            string previousDisplayName = PlayerSettings.iOS.applicationDisplayName;
            bool previousDevelopment = EditorUserBuildSettings.development;
            try
            {
                PlayerSettings.productName = ProductName;
                PlayerSettings.SetApplicationIdentifier(namedTarget, identifier);
                PlayerSettings.bundleVersion = DemoVersion;
                if (target == BuildTarget.iOS)
                {
                    PlayerSettings.iOS.buildNumber = DemoBuildNumber;
                    PlayerSettings.iOS.applicationDisplayName = ProductName;
                }
                action();
            }
            finally
            {
                PlayerSettings.productName = previousName;
                PlayerSettings.SetApplicationIdentifier(namedTarget, previousIdentifier);
                PlayerSettings.bundleVersion = previousVersion;
                if (target == BuildTarget.iOS)
                {
                    PlayerSettings.iOS.buildNumber = previousBuildNumber;
                    PlayerSettings.iOS.applicationDisplayName = previousDisplayName;
                }
                EditorUserBuildSettings.development = previousDevelopment;
            }
        }

        private static void WithTemporaryEditorBuildState(BuildTarget target, Action action)
        {
            BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes
                .Select(scene => new EditorBuildSettingsScene(scene.path, scene.enabled)).ToArray();
            string sceneState = SceneListState();
            // BuildPlayer or a platform switch can serialize temporary native settings. Restore exact disk bytes last.
            string projectSettings = Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset");
            string sceneSettings = Path.Combine(Application.dataPath, "..", "ProjectSettings", "EditorBuildSettings.asset");
            byte[] previousProjectBytes = File.ReadAllBytes(projectSettings);
            byte[] previousSceneBytes = File.ReadAllBytes(sceneSettings);
            Exception failure = null;
            try
            {
                if (previousTarget != target && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
                    throw new InvalidOperationException("Could not activate the demo build platform: " + target);
                action();
            }
            catch (Exception error) { failure = error; }
            finally
            {
                var errors = new List<Exception>();
                RestoreStep(() =>
                {
                    if (EditorUserBuildSettings.activeBuildTarget != previousTarget
                        && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget))
                        throw new InvalidOperationException("Could not restore the original active build platform.");
                }, errors);
                RestoreStep(() => { if (SceneListState() != sceneState) EditorBuildSettings.scenes = previousScenes; }, errors);
                RestoreStep(() => RestoreDiskFile(projectSettings, previousProjectBytes), errors);
                RestoreStep(() => RestoreDiskFile(sceneSettings, previousSceneBytes), errors);
                if (errors.Count != 0)
                {
                    if (failure != null) errors.Insert(0, failure);
                    throw new AggregateException("The demo build could not restore the original Editor configuration.", errors);
                }
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static string SceneListState() => string.Join("|", EditorBuildSettings.scenes.Select(scene => scene.path + ":" + scene.enabled));
        private static void RestoreStep(Action action, List<Exception> errors)
        {
            try { action(); }
            catch (Exception error) { errors.Add(error); }
        }
        private static void RestoreDiskFile(string path, byte[] original)
        {
            if (!File.Exists(path) || !original.SequenceEqual(File.ReadAllBytes(path))) File.WriteAllBytes(path, original);
            if (!original.SequenceEqual(File.ReadAllBytes(path)))
                throw new IOException("Could not restore the original build configuration file: " + Path.GetFileName(path));
        }

        private static BuildManifest CreateManifest(BuildTarget target, bool development, string identifier, string generation, string project)
        {
            GameStageCatalog catalog = GameStageCatalog.RequireDefault();
            var source = AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset");
            float[] values;
            using (var settings = new GameStageSettings(source, catalog)) values = settings.Capture(2);
            string connection, discovery;
            using (LabGameBuildVariant.OverrideForTests(true))
            {
                connection = LabNetwork.GameConnectionCompatibility;
                discovery = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
            }
            return new BuildManifest
            {
                generation = generation,
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceRevision = ReadGit(project, "rev-parse HEAD").Trim(),
                sourceHasUncommittedChanges = !string.IsNullOrWhiteSpace(ReadGit(project, "status --porcelain --untracked-files=normal")),
                target = target.ToString(),
                development = development,
                productName = ProductName,
                applicationIdentifier = identifier,
                version = DemoVersion,
                buildNumber = target == BuildTarget.iOS ? DemoBuildNumber : string.Empty,
                variant = "yaguang-demo",
                variantRevision = LabGameBuildVariant.YaguangDemoRevision,
                scriptingDefine = LabGameBuildVariant.YaguangDemoDefine,
                connectionCompatibility = connection,
                discoveryCompatibility = discovery,
                labCompatibility = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Lab),
                stageIndex = 2,
                stageId = catalog.Get(2).stageId,
                yokai = catalog.Get(2).yokai.ToString(),
                participants = 3,
                developerFieldCount = values.Length,
                stageSettingsSha256 = SettingsHash(values),
                scenes = (string[])GameScenePaths.All.Clone(),
                result = "Building",
                totalErrors = -1
            };
        }

        private static string SettingsHash(float[] values)
        {
            string text = "stage=2;participants=3;" + string.Join(";", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ReadGit(string project, string arguments)
        {
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = project, UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Cannot record the demo source revision: " + error.Trim());
                return output;
            }
        }

        private static void ValidateOutputIdentity(BuildTarget target, string output, string identifier)
        {
            string plist = target == BuildTarget.iOS ? Path.Combine(output, "Info.plist") : Path.Combine(output, "Contents", "Info.plist");
            XDocument document = XDocument.Load(plist);
            string displayName = PlistValue(document, "CFBundleDisplayName") ?? PlistValue(document, "CFBundleName");
            if (displayName != ProductName)
                throw new InvalidOperationException("The generated demo display name does not match: " + displayName);
            string actualIdentifier = PlistValue(document, "CFBundleIdentifier");
            if (actualIdentifier != identifier)
            {
                string project = Path.Combine(output, "Unity-iPhone.xcodeproj", "project.pbxproj");
                bool variable = actualIdentifier == "$(PRODUCT_BUNDLE_IDENTIFIER)" || actualIdentifier == "${PRODUCT_BUNDLE_IDENTIFIER}";
                if (target != BuildTarget.iOS || !variable || !File.Exists(project)
                    || !PbxAppIdentifiersMatch(File.ReadAllText(project), identifier))
                    throw new InvalidOperationException("The generated demo application identifier does not match: " + actualIdentifier);
            }
            if (PlistValue(document, "CFBundleShortVersionString") != DemoVersion
                || target == BuildTarget.iOS && PlistValue(document, "CFBundleVersion") != DemoBuildNumber)
                throw new InvalidOperationException("The generated demo version does not match the build manifest.");
        }

        private static bool PbxAppIdentifiersMatch(string project, string expected)
        {
            string[] appSettings = Regex.Matches(project, @"buildSettings\s*=\s*\{(?<settings>.*?)\};", RegexOptions.Singleline)
                .Cast<Match>().Select(match => match.Groups["settings"].Value)
                .Where(settings => Regex.IsMatch(settings, @"\bPRODUCT_NAME\s*=\s*""?\$\(PRODUCT_NAME_APP\)""?\s*;"))
                .ToArray();
            return appSettings.Length > 0 && appSettings.All(settings =>
                Regex.Match(settings, @"\bPRODUCT_BUNDLE_IDENTIFIER\s*=\s*""?(?<identifier>[^"";\r\n]+)""?\s*;")
                    .Groups["identifier"].Value.Trim() == expected);
        }

        private static string PlistValue(XDocument document, string key)
        {
            XElement item = document.Root?.Element("dict")?.Elements("key").FirstOrDefault(element => element.Value == key);
            return item?.ElementsAfterSelf().FirstOrDefault()?.Value;
        }

        private static void WriteManifest(string path, BuildManifest manifest) => File.WriteAllText(path, JsonUtility.ToJson(manifest, true) + "\n");
        private static NamedBuildTarget NamedTarget(BuildTarget target) => NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
        private static void RequireTarget(BuildTarget target)
        {
            if (target != BuildTarget.StandaloneOSX && target != BuildTarget.iOS)
                throw new ArgumentOutOfRangeException(nameof(target), target, "The Yaguang demo supports Mac and iOS exports.");
        }

        [Serializable]
        private sealed class BuildManifest
        {
            public string generation, generatedAtUtc, sourceRevision;
            public bool sourceHasUncommittedChanges, development;
            public string target, productName, applicationIdentifier, version, buildNumber, variant;
            public int variantRevision;
            public string scriptingDefine, connectionCompatibility, discoveryCompatibility, labCompatibility;
            public int stageIndex, participants, developerFieldCount;
            public string stageId, yokai, stageSettingsSha256;
            public string[] scenes;
            public string result;
            public int totalErrors;
            public bool identityVerified;
        }
    }
}
