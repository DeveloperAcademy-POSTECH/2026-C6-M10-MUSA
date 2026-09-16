using System;
using System.IO;
using System.Linq;
using C6.Prototype.Networking;
using C6.Prototype.Lobby.Discovery;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace C6.Editor
{
    /// <summary>Local network game build with passive device evidence and explicit Mac validation. Saved P4 scene stays intact.</summary>
    public static class L2ConnectionBuild
    {
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6LocalNetwork.app");

        static void Build(BuildTarget target, string relativePath)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Select the matching active build target before L2 export.");
            string outputRoot = Environment.GetEnvironmentVariable("C6_L2_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot))
                throw new InvalidOperationException("Set C6_L2_OUTPUT_ROOT to a new diagnostic output directory.");
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
                PlayerSettings.productName = "C6 Prototype";
                PlayerSettings.iOS.buildNumber = LocalNetworkBuildInfo.ApplicationBuild;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ContinuousTransferBuild.ScenePath }, locationPathName = output,
                    target = target, options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "C6_L2_CHECKS" }
                });
                receipt.result = report.summary.result.ToString();
                receipt.errors = report.summary.totalErrors;
                receipt.warnings = report.summary.totalWarnings;
                receipt.seconds = report.summary.totalTime.TotalSeconds;
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("L2 build failed: " + report.summary.result);
                string plistPath = Path.Combine(output, target == BuildTarget.iOS ? "Info.plist" : "Contents/Info.plist");
                var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
                plist.root.SetString("NSLocalNetworkUsageDescription", "같은 Wi-Fi 또는 핫스팟에서 방을 찾고 최대 다섯 기기를 연결해 함께 플레이합니다.");
                var services = plist.root.values.TryGetValue("NSBonjourServices", out var existing)
                    ? existing.AsArray() : plist.root.CreateArray("NSBonjourServices");
                if (!services.values.Any(value => value.AsString() == BonjourRoomDiscovery.ServiceType))
                    services.AddString(BonjourRoomDiscovery.ServiceType);
                plist.WriteToFile(plistPath);
                if (target == BuildTarget.iOS)
                {
                    if (!File.Exists(Path.Combine(output, "Unity-iPhone.xcodeproj/project.pbxproj")))
                        throw new InvalidOperationException("Xcode project missing.");
                    var lifecycle = JsonUtility.FromJson<Lifecycle>(File.ReadAllText(Path.Combine(output, "C6T12LifecyclePatch/patch-receipt.json")));
                    if (lifecycle.status != "APPLIED_SOURCE_ONLY" || lifecycle.scene != ContinuousTransferBuild.ScenePath)
                        throw new InvalidOperationException("P4 lifecycle patch not verified.");
                    if (plist.root["CFBundleVersion"].AsString() != LocalNetworkBuildInfo.ApplicationBuild)
                        throw new InvalidOperationException("L2 app build number mismatch.");
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
                Debug.Log("C6_L2_EXPORT_COMPLETE target=" + target + " build=" + LocalNetworkBuildInfo.ApplicationBuild);
            }
            catch (Exception error) { receipt.outputValidation = "FAIL"; receipt.exception = error.GetType().FullName; throw; }
            finally
            {
                PlayerSettings.productName = product;
                PlayerSettings.iOS.buildNumber = number;
                File.WriteAllText(Path.Combine(outputRoot, "l2-build-" + target + ".json"), JsonUtility.ToJson(receipt, true));
            }
        }
        [Serializable] sealed class Lifecycle { public string status, scene; }
        [Serializable] sealed class Receipt
        {
            public string task = "L2", build = LocalNetworkBuildInfo.ApplicationBuild, target, startedUtc, result, exception;
            public string outputValidation = "NOT_RUN", device = "NOT_RUN", gameplay = "NOT_RUN";
            public int errors, warnings; public double seconds;
        }
    }
}
