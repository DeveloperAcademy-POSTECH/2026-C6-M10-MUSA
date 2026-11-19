using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using C6Lab;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Release-only, offline tutorial export. Never persists a variant or changes a saved game Scene.</summary>
    public static class GameTutorialOnlyBuild
    {
        public const string ProductName = "합이오_튜토리얼";
        public const string ExecutableName = "HapioTutorial";
        public const string Version = "0.1.0";
        public const string BuildNumber = "1";
        public const string IconSourcePath = "BuildSupport/Branding/AppIcon.icon";
        public const string NativeIconName = "TutorialAppIcon";
        private const string LastOutput = "Hapio.LastTutorialOnlyBuild";
        private const string ManifestName = "build-manifest.json";

        [MenuItem("Hapio/Tutorial Only Build/Release/Export iOS Xcode Project")]
        public static void Ios()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish Play and compilation before exporting the tutorial.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new InvalidOperationException("Install iOS Build Support before exporting the tutorial.");
            Validate();
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string identifier = TutorialApplicationIdentifier(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS));
            string generation = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "-release";
            string folder = Path.Combine(project, "Builds", "HapioTutorialOnly", generation);
            string output = Path.Combine(folder, "iOS");
            if (Directory.Exists(folder) || File.Exists(folder))
                throw new InvalidOperationException("The tutorial output already exists: " + folder);
            var manifest = CreateManifest(project, generation, identifier);
            Directory.CreateDirectory(folder);
            string manifestPath = Path.Combine(folder, ManifestName);
            WriteManifest(manifestPath, manifest);
            BuildReport report = null;
            try
            {
                GameDemoBuild.WithTemporaryEditorBuildState(BuildTarget.iOS, () =>
                    WithTemporaryPlayerSettings(identifier, () => report = BuildPipeline.BuildPlayer(CreateOptions(output))));
                manifest.result = report == null ? "NoReport" : report.summary.result.ToString();
                manifest.totalErrors = report == null ? -1 : (int)report.summary.totalErrors;
                if (report == null || report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                    throw new InvalidOperationException("The tutorial export did not complete without errors.");
                FinalizeIosExport(output);
                ValidateOutputIdentity(output, identifier);
                manifest.identityVerified = true;
                SessionState.SetString(LastOutput, folder);
                Debug.Log("HAPIO_TUTORIAL_BUILD_OK output=" + output + " manifest=" + manifestPath);
            }
            catch
            {
                if (report == null) manifest.result = "Exception";
                else if (report.summary.result == BuildResult.Succeeded) manifest.result = "OutputValidationFailed";
                throw;
            }
            finally { WriteManifest(manifestPath, manifest); }
        }

        [MenuItem("Hapio/Tutorial Only Build/Open Last Output")]
        public static void OpenOutput()
        {
            string folder = SessionState.GetString(LastOutput, string.Empty);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) EditorUtility.RevealInFinder(folder);
            else Debug.LogWarning("HAPIO_TUTORIAL_OUTPUT_UNAVAILABLE: export the tutorial first.");
        }

        public static BuildPlayerOptions CreateOptions(string output) => new BuildPlayerOptions
        {
            scenes = (string[])GameScenePaths.All.Clone(), target = BuildTarget.iOS,
            locationPathName = output, options = BuildOptions.None,
            extraScriptingDefines = new[] { LabGameBuildVariant.TutorialOnlyDefine }
        };

        public static string TutorialApplicationIdentifier(string normal)
        {
            if (string.IsNullOrWhiteSpace(normal) || new[] { ".tutorial", ".staffdemo", ".staffdemo.stage2", ".yaguangdemo" }
                .Any(suffix => normal.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Restore the normal application identifier before exporting a tutorial.");
            return normal + ".tutorial";
        }

        public static void ValidateSavedDefines(string defines)
        {
            var variants = new[] { LabGameBuildVariant.TutorialOnlyDefine, LabGameBuildVariant.StaffStageDemoDefine,
                LabGameBuildVariant.StaffStageTwoOnlyDefine, LabGameBuildVariant.YaguangDemoDefine };
            if ((defines ?? string.Empty).Split(';').Select(value => value.Trim()).Any(variants.Contains))
                throw new InvalidOperationException("Variant defines belong only to the dedicated build options, not saved Player Settings.");
        }

        public static void Validate()
        {
            ValidateSavedDefines(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS));
            foreach (string path in GameScenePaths.All)
                if (!File.Exists(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new InvalidOperationException("The tutorial needs its saved game Scene: " + path);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Preserve and save Scene edits before exporting; the tutorial build never saves Scenes.");
            var art = GameTutorialArt.Load();
            if (art == null || art.bul == null || art.muk == null || art.geumok == null || art.bubble == null
                || art.dialogueFont == null || art.bannerFont == null || art.hand == null || art.tapHint == null)
                throw new InvalidOperationException("The saved tutorial artwork and fonts must be assigned before exporting.");
            GameStageCatalog.RequireDefault();
            if (Resources.Load<LabConfig>("LabConfig") == null)
                throw new InvalidOperationException("The tutorial needs the saved LabConfig; no defaults are generated during export.");
            ShowcaseBranding.Validate();
            CaptureIconFiles(Path.GetFullPath(Path.Combine(Application.dataPath, "..", IconSourcePath)));
        }

        internal static void WithTemporaryPlayerSettings(string identifier, Action action) =>
            GameDemoBuild.WithTemporaryPlayerIdentity(BuildTarget.iOS, identifier, ProductName, Version, BuildNumber, action);

        internal static void FinalizeIosExport(string output)
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", IconSourcePath));
            CopyNativeIcon(source, Path.Combine(output, NativeIconName + ".icon"));
            GameDemoBuild.NormalizeIosExecutableFor(output, ExecutableName);
            string path = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(path);
            string main = project.GetUnityMainTargetGuid();
            if (string.IsNullOrEmpty(main)) throw new InvalidOperationException("The tutorial export needs its main application target.");
            string resources = project.GetResourcesBuildPhaseByTarget(main);
            if (string.IsNullOrEmpty(resources))
                throw new InvalidOperationException("The tutorial export needs the main application's resource phase.");
            string icon = NativeIconName + ".icon";
            string file = project.FindFileGuidByProjectPath(icon) ?? project.AddFile(icon, icon, PBXSourceTree.Source);
            if (!ResourceContainsFile(project.WriteToString(), resources, file))
                project.AddFileToBuildSection(main, resources, file);
            foreach (string name in project.BuildConfigNames())
                project.SetBuildPropertyForConfig(project.BuildConfigByName(main, name), "ASSETCATALOG_COMPILER_APPICON_NAME", NativeIconName);
            // Unity's public PBX API has no file-type setter for Xcode 26's native .icon package.
            // Restrict this correction to the one file reference added above; all other objects remain untouched.
            File.WriteAllText(path, NormalizeIconFileType(project.WriteToString(), file));
        }

        private static string ReadObjectBody(string text, string guid)
        {
            var match = Regex.Match(text, @"\b" + Regex.Escape(guid)
                + @"(?:\s*/\*[^*]*\*/)?\s*=\s*\{(?<body>[^}]*)\};", RegexOptions.Singleline);
            if (!match.Success) throw new InvalidOperationException("The expected Xcode object is missing: " + guid);
            return match.Groups["body"].Value;
        }

        private static bool ResourceContainsFile(string text, string phase, string file)
        {
            string body = ReadObjectBody(text, phase);
            var entries = Regex.Match(body, @"\bfiles\s*=\s*\((?<ids>[^)]*)\)", RegexOptions.Singleline);
            foreach (Match id in Regex.Matches(entries.Groups["ids"].Value, @"\b[A-Fa-f0-9]{24}\b"))
                if (Regex.IsMatch(ReadObjectBody(text, id.Value), @"\bfileRef\s*=\s*" + Regex.Escape(file) + @"\b")) return true;
            return false;
        }

        internal static string NormalizeIconFileType(string text, string guid)
        {
            string pattern = @"(?<head>\b" + Regex.Escape(guid)
                + @"(?:\s*/\*[^*]*\*/)?\s*=\s*\{)(?<body>[^}]*)(?<tail>\};)";
            var matches = Regex.Matches(text, pattern, RegexOptions.Singleline);
            if (matches.Count != 1 || !Regex.IsMatch(matches[0].Groups["body"].Value, @"\bisa\s*=\s*PBXFileReference\s*;"))
                throw new InvalidOperationException("The native icon file reference is missing or ambiguous.");
            return Regex.Replace(text, pattern, match =>
            {
                string body = match.Groups["body"].Value;
                const string type = @"\b(?:lastKnownFileType|explicitFileType)\s*=\s*[^;]+;";
                if (Regex.Matches(body, type).Count > 1)
                    throw new InvalidOperationException("The native icon has conflicting file types.");
                body = Regex.IsMatch(body, type) ? Regex.Replace(body, type, "lastKnownFileType = folder.iconcomposer.icon;")
                    : body + " lastKnownFileType = folder.iconcomposer.icon; ";
                return match.Groups["head"].Value + body + match.Groups["tail"].Value;
            }, RegexOptions.Singleline);
        }

        internal static void CopyNativeIcon(string source, string destination)
        {
            source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
            destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
            var files = CaptureIconFiles(source);
            if (source == destination)
                throw new InvalidOperationException("The native icon must be copied into the export, not over its source.");
            Directory.CreateDirectory(destination);
            var expected = files.Select(file => file.path).ToArray();
            if (Directory.GetFiles(destination, "*", SearchOption.AllDirectories)
                .Any(path => !expected.Contains(path.Substring(destination.Length + 1).Replace('\\', '/'))))
                throw new InvalidOperationException("The exported icon contains unexpected files; use a fresh export folder.");
            foreach (var file in files)
            {
                string input = Path.Combine(source, file.path), output = Path.Combine(destination, file.path);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                if (File.Exists(output) && Hash(output) != file.sha256)
                    throw new InvalidOperationException("An existing exported icon differs from the approved source.");
                if (!File.Exists(output)) File.Copy(input, output);
                if (Hash(output) != file.sha256) throw new IOException("The exported icon does not match the source: " + file.path);
            }
        }

        private static IconFile[] CaptureIconFiles(string source)
        {
            if (!File.Exists(Path.Combine(source, "icon.json")) || !Directory.Exists(Path.Combine(source, "Assets"))
                || !Directory.GetFiles(Path.Combine(source, "Assets"), "*.png", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("Keep the supplied native AppIcon.icon in " + IconSourcePath + "; it is never recreated from a PNG.");
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0
                || Directory.GetDirectories(source, "*", SearchOption.AllDirectories).Concat(Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                    .Any(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
                throw new InvalidOperationException("The native icon must contain regular files, not external links.");
            return Directory.GetFiles(source, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new IconFile { path = path.Substring(source.Length + 1).Replace('\\', '/'), sha256 = Hash(path) }).ToArray();
        }

        internal static void ValidateOutputIdentity(string output, string identifier)
        {
            var plist = new PlistDocument();
            plist.ReadFromFile(Path.Combine(output, "Info.plist"));
            string Value(string key) => plist.root.values.TryGetValue(key, out var value) ? value.AsString() : null;
            if (Value("CFBundleDisplayName") != ProductName || Value("CFBundleShortVersionString") != Version
                || Value("CFBundleVersion") != BuildNumber)
                throw new InvalidOperationException("The tutorial's exported name or version does not match its manifest.");
            string executable = Value("CFBundleExecutable");
            if (executable != ExecutableName && executable != "$(EXECUTABLE_NAME)" && executable != "${EXECUTABLE_NAME}")
                throw new InvalidOperationException("The tutorial export has the wrong executable declaration.");
            string actual = Value("CFBundleIdentifier");
            if (actual != identifier && actual != "$(PRODUCT_BUNDLE_IDENTIFIER)" && actual != "${PRODUCT_BUNDLE_IDENTIFIER}")
                throw new InvalidOperationException("The tutorial export has the wrong application identifier.");
            var project = new PBXProject(); project.ReadFromFile(PBXProject.GetPBXProjectPath(output));
            string main = project.GetUnityMainTargetGuid();
            foreach (string name in project.BuildConfigNames())
            {
                string config = project.BuildConfigByName(main, name);
                if (project.GetBuildPropertyForConfig(config, "PRODUCT_BUNDLE_IDENTIFIER") != identifier
                    || project.GetBuildPropertyForConfig(config, "ASSETCATALOG_COMPILER_APPICON_NAME") != NativeIconName)
                    throw new InvalidOperationException("Every main application configuration must select the tutorial identity and supplied icon.");
            }
            string text = project.WriteToString();
            var names = Regex.Matches(text, @"\bPRODUCT_NAME_APP\s*=\s*(?<name>[^;\r\n]+);");
            if (names.Count == 0 || names.Cast<Match>().Any(match => match.Groups["name"].Value.Trim().Trim('"') != ExecutableName))
                throw new InvalidOperationException("The tutorial export has the wrong executable name.");
        }

        private static Manifest CreateManifest(string project, string generation, string identifier) => new Manifest
        {
            generation = generation, generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            sourceRevision = GameDemoBuild.ReadGit(project, "rev-parse HEAD").Trim(),
            sourceHasUncommittedChanges = !string.IsNullOrWhiteSpace(GameDemoBuild.ReadGit(project, "status --porcelain --untracked-files=normal")),
            target = "iOS", development = false, productName = ProductName, executableName = ExecutableName,
            applicationIdentifier = identifier, version = Version, buildNumber = BuildNumber,
            variant = "tutorial-only", scriptingDefine = LabGameBuildVariant.TutorialOnlyDefine,
            participants = 1, offline = true, scenes = (string[])GameScenePaths.All.Clone(),
            nativeIconName = NativeIconName, iconSource = IconSourcePath,
            iconFiles = CaptureIconFiles(Path.Combine(project, IconSourcePath)), result = "Building", totalErrors = -1
        };

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
        private static void WriteManifest(string path, Manifest value) => File.WriteAllText(path, JsonUtility.ToJson(value, true) + "\n");
        [Serializable] private sealed class IconFile { public string path, sha256; }
        [Serializable] private sealed class Manifest
        {
            public string generation, generatedAtUtc, sourceRevision;
            public bool sourceHasUncommittedChanges, development, offline;
            public string target, productName, executableName, applicationIdentifier, version, buildNumber, variant, scriptingDefine;
            public int participants;
            public string[] scenes;
            public string nativeIconName, iconSource;
            public IconFile[] iconFiles;
            public string result;
            public int totalErrors;
            public bool identityVerified;
        }
    }
}
