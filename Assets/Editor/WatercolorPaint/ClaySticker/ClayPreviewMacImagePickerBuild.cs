using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Explicit preview-only native compilation. No plug-in lives permanently under Assets.</summary>
    public static class ClayPreviewMacImagePickerBuild
    {
        public const string SourcePath = "tools/ClayPreviewNative/ClayPreviewMacImagePicker.mm";
        public const string StageAssetPath = "Assets/Experiments/ClayPreview/NativeBuildStage";
        const string BundleName = "ClayPreviewMacImagePicker.bundle";
        const string OwnerMarker = "hapio-clay-native-stage-v1.txt";
        const string CachePath = "Temp/ClayPreviewNative";
        static bool staged;
        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string Full(string relative) => Path.Combine(ProjectRoot, relative);
        public static bool IsStaged => staged;

        [MenuItem("C6 Lab/Experiments/Clay Sticker/Build Mac Image Picker Plug-in")]
        public static void BuildMenu() => Debug.Log("CLAY_PREVIEW_NATIVE_BUILT " + BuildBundle());

        /// <summary>Builds an arm64+x86_64 Cocoa bundle outside Assets. Does not change an importer or game build.</summary>
        public static string BuildBundle()
        {
            Require(Application.platform == RuntimePlatform.OSXEditor, "The preview file picker can be compiled only on macOS.");
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling, "Stop Play mode and finish compilation first.");
            Require(File.Exists(Full(SourcePath)), "The Mac image picker source is missing.");
            string bundle = CachePath + "/" + BundleName;
            Directory.CreateDirectory(Full(bundle + "/Contents/MacOS"));
            const string binaryName = "ClayPreviewMacImagePicker";
            string executable = bundle + "/Contents/MacOS/" + binaryName;
            string arguments = "clang++ -std=c++17 -fobjc-arc -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -bundle -framework Cocoa "
                + Quote(SourcePath) + " -o " + Quote(executable);
            string sourceHash = Hash(File.ReadAllBytes(Full(SourcePath)));
            File.WriteAllText(Full(CachePath + "/Build.txt"),
                "Source SHA256: " + sourceHash + "\nWorking directory: project root\n/usr/bin/xcrun " + arguments + "\n");
            Run("/usr/bin/xcrun", arguments);
            string architectures = Run("/usr/bin/lipo", "-archs " + Quote(executable));
            Require(architectures.Contains("arm64") && architectures.Contains("x86_64"), "The native picker must contain both Mac architectures.");
            File.WriteAllText(Full(bundle + "/Contents/Info.plist"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
                + "<plist version=\"1.0\"><dict><key>CFBundleExecutable</key><string>" + binaryName
                + "</string><key>CFBundleIdentifier</key><string>org.hapio.experiments.imagepicker</string>"
                + "<key>CFBundlePackageType</key><string>BNDL</string><key>CFBundleVersion</key><string>1</string></dict></plist>\n");
            Run("/usr/bin/codesign", "--force --sign - " + Quote(bundle));
            File.AppendAllText(Full(CachePath + "/Build.txt"), "Architectures: " + architectures.Trim() + "\nSigning: ad-hoc development only\n");
            return bundle;
        }

        /// <summary>Use only around the isolated Mac BuildPipeline.BuildPlayer call; always dispose in a finally/using.</summary>
        public static IDisposable StageForPreview()
        {
            Require(!staged, "A native preview plug-in stage is already active.");
            Require(!Directory.Exists(Full(StageAssetPath)) && !File.Exists(Full(StageAssetPath + ".meta")),
                "A native plug-in stage remains from an interrupted build. Use Remove Stale Mac Picker Stage before building.");
            string bundle = BuildBundle(); bool created = false;
            try
            {
                Directory.CreateDirectory(Full(StageAssetPath)); created = true;
                File.WriteAllText(Full(StageAssetPath + "/" + OwnerMarker), "Owned temporary preview plug-in stage. Not a game asset.\n");
                CopyDirectory(Full(bundle), Full(StageAssetPath + "/" + BundleName));
                AssetDatabase.ImportAsset(StageAssetPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(StageAssetPath + "/" + BundleName) as PluginImporter;
                Require(importer != null && importer.isNativePlugin, "Unity did not recognize the staged Mac native bundle.");
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
                importer.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
                importer.isPreloaded = false;
                importer.SaveAndReimport();
                staged = true;
                return new Stage();
            }
            catch
            {
                staged = false;
                if (created) DeleteOwnedStage();
                throw;
            }
        }

        [MenuItem("C6 Lab/Experiments/Clay Sticker/Remove Stale Mac Picker Stage")]
        public static void RemoveStaleStage()
        {
            Require(!staged && !BuildPipeline.isBuildingPlayer && !EditorApplication.isPlaying,
                "An active build or Play session cannot remove its plug-in stage.");
            if (!Directory.Exists(Full(StageAssetPath))) return;
            DeleteOwnedStage();
        }

        public static void GuardBuild(BuildReport report)
        {
            if (!Directory.Exists(Full(StageAssetPath))) return;
            string expected = Full("Builds/Experiments/ClayPreview") + Path.DirectorySeparatorChar;
            string destination = Path.GetFullPath(report.summary.outputPath);
            Require(staged && report.summary.platform == BuildTarget.StandaloneOSX
                && destination.StartsWith(expected, StringComparison.Ordinal) && destination.EndsWith(".app", StringComparison.OrdinalIgnoreCase),
                "A native picker stage is present outside its explicit Mac comparison build. Remove the stale stage; it must not enter a game build.");
        }

        sealed class Stage : IDisposable
        {
            bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true; staged = false;
                DeleteOwnedStage();
            }
        }
        static void DeleteOwnedStage()
        {
            Require(File.Exists(Full(StageAssetPath + "/" + OwnerMarker)), "Refusing to remove an unrecognized native stage directory.");
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(StageAssetPath)))
                AssetDatabase.ImportAsset(StageAssetPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            Require(AssetDatabase.DeleteAsset(StageAssetPath) && !Directory.Exists(Full(StageAssetPath)),
                "Could not remove the temporary native picker stage. Normal builds remain blocked until it is removed.");
        }
        static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
        static string Run(string executable, string arguments)
        {
            var info = new ProcessStartInfo(executable, arguments)
            { WorkingDirectory = ProjectRoot, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(info))
            {
                Require(process != null, "The native compiler process did not start.");
                Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000)) { try { process.Kill(); } catch { } throw new BuildFailedException("The native compiler exceeded its one-minute limit."); }
                string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
                Require(process.ExitCode == 0, Path.GetFileName(executable) + " failed: " + stderr);
                return stdout;
            }
        }
        static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }
        static void Require(bool condition, string message)
        { if (!condition) throw new BuildFailedException(message); }
    }

    /// <summary>Also protects the next Editor session after a crash interrupts the temporary stage.</summary>
    public sealed class ClayPreviewNativeBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;
        public void OnPreprocessBuild(BuildReport report) => ClayPreviewMacImagePickerBuild.GuardBuild(report);
    }
}
