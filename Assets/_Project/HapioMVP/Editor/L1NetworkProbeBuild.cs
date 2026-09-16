using System;
using System.IO;
using System.Linq;
using C6.Prototype.Lobby;
using C6.Prototype.Lobby.Discovery;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace C6.Editor
{
    /// <summary>Opt-in diagnostic player. No saved gameplay scene or package is replaced.</summary>
    public static class L1NetworkProbeBuild
    {
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6NetworkCheck.app");

        static void Build(BuildTarget target, string relativePath)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Select the matching active build target before L1 export.");
            string outputRoot = Environment.GetEnvironmentVariable("C6_L1_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot))
                throw new InvalidOperationException("Set C6_L1_OUTPUT_ROOT to a new diagnostic output directory.");
            outputRoot = Path.GetFullPath(outputRoot);
            string output = Path.Combine(outputRoot, relativePath);
            if (File.Exists(output) || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Output already exists; preserve it and select another directory.");

            ContinuousTransferBuild.Prepare();
            string product = PlayerSettings.productName;
            string number = PlayerSettings.iOS.buildNumber;
            var receipt = new Receipt { target = target.ToString(), startedUtc = DateTime.UtcNow.ToString("O") };
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            try
            {
                PlayerSettings.productName = "C6 Network Check";
                PlayerSettings.iOS.buildNumber = L1NetworkProbe.AppBuild;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ContinuousTransferBuild.ScenePath }, locationPathName = output,
                    target = target, options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "C6_L1_PROBE" }
                });
                receipt.result = report.summary.result.ToString();
                receipt.errors = report.summary.totalErrors;
                receipt.warnings = report.summary.totalWarnings;
                receipt.seconds = report.summary.totalTime.TotalSeconds;
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("L1 build failed: " + report.summary.result);
                string plistPath = Path.Combine(output, target == BuildTarget.iOS ? "Info.plist" : "Contents/Info.plist");
                var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
                plist.root.SetString("NSLocalNetworkUsageDescription", "같은 Wi-Fi 또는 핫스팟에서 C6 기기의 네트워크 통신을 진단합니다.");
                var services = plist.root.values.TryGetValue("NSBonjourServices", out var existing)
                    ? existing.AsArray() : plist.root.CreateArray("NSBonjourServices");
                if (!services.values.Any(value => value.AsString() == L1BonjourDiscovery.ServiceType))
                    services.AddString(L1BonjourDiscovery.ServiceType);
                plist.WriteToFile(plistPath);
                if (target == BuildTarget.iOS)
                {
                    if (!File.Exists(Path.Combine(output, "Unity-iPhone.xcodeproj/project.pbxproj")))
                        throw new InvalidOperationException("Xcode project missing.");
                    var lifecycle = JsonUtility.FromJson<Lifecycle>(File.ReadAllText(Path.Combine(output, "C6T12LifecyclePatch/patch-receipt.json")));
                    if (lifecycle.status != "APPLIED_SOURCE_ONLY" || lifecycle.scene != ContinuousTransferBuild.ScenePath)
                        throw new InvalidOperationException("P4 lifecycle patch not verified.");
                    if (plist.root["CFBundleVersion"].AsString() != L1NetworkProbe.AppBuild)
                        throw new InvalidOperationException("Diagnostic build number mismatch.");
                }
                else
                {
                    var start = new System.Diagnostics.ProcessStartInfo("/usr/bin/codesign") {
                        UseShellExecute = false, RedirectStandardError = true
                    };
                    foreach (string value in new[] { "--force", "--deep", "--sign", "-", output }) start.ArgumentList.Add(value);
                    using (var process = System.Diagnostics.Process.Start(start))
                    {
                        string error = process.StandardError.ReadToEnd(); process.WaitForExit();
                        if (process.ExitCode != 0) throw new InvalidOperationException("Local Mac signing failed: " + error);
                    }
                }
                receipt.outputValidation = "PASS";
                Debug.Log("C6_L1_EXPORT_COMPLETE target=" + target + " build=" + L1NetworkProbe.AppBuild);
            }
            catch (Exception error) { receipt.outputValidation = "FAIL"; receipt.exception = error.GetType().FullName; throw; }
            finally
            {
                PlayerSettings.productName = product;
                PlayerSettings.iOS.buildNumber = number;
                File.WriteAllText(Path.Combine(outputRoot, "l1-build-" + target + ".json"), JsonUtility.ToJson(receipt, true));
            }
        }
        [Serializable] sealed class Lifecycle { public string status, scene; }
        [Serializable] sealed class Receipt
        {
            public string task = "L1", build = L1NetworkProbe.AppBuild, target, startedUtc, result, exception;
            public string outputValidation = "NOT_RUN", device = "NOT_RUN", gameplay = "NOT_RUN";
            public int errors, warnings; public double seconds;
        }
    }
}
