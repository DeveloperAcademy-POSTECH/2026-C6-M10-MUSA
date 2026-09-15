using System;
using System.IO;
using System.Linq;
using C6.Prototype.Lobby;
using C6.Prototype.Lobby.Discovery;
using C6.Prototype.Networking;
using C6.Prototype.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    public static class LobbyBuild
    {
        public const string ScenePath="Assets/_Project/HapioMVP/Scenes/RoomLobby.unity";
        public const string ConfigPath="Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        public const string LocalNetworkPurpose="같은 Wi-Fi에서 C6 방을 찾고 두 기기를 연결해 함께 플레이합니다.";
        [MenuItem("C6/T10-A/Prepare Room Lobby")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
            var config=AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            if(config==null)throw new InvalidOperationException("Existing shared Config is required.");
            bool create=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null;
            if(create&&File.Exists(ScenePath))throw new InvalidOperationException("Occupied scene path; preserving it.");
            if(create&&!Application.isBatchMode)
                for(int i=0;i<SceneManager.sceneCount;i++)if(string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save the untitled scene or use a verification copy.");
            if(create)
            {
                var previous=SceneManager.GetActiveScene();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var camera=new GameObject("LobbyCamera").AddComponent<Camera>();
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.065f,.09f);camera.cullingMask=0;
                    var root=new GameObject("T10RoomLobby");
                    root.AddComponent<DirectConnectionSession>();
                    root.AddComponent<T10LobbySession>().Configure(config);
                    root.AddComponent<T10LobbyHud>();root.AddComponent<T10LobbyController>();
                    new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                    if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("Could not save lobby scene.");
                }
                finally
                {
                    if(!Application.isBatchMode){EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
                }
            }
            PlayerSettings.productName="C6 Prototype";PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.wolfuraark.c6prototype.t10a.desktop");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,"com.wolfuraark.c6prototype");
            PlayerSettings.iOS.buildNumber=LobbyBuildInfo.Build;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=false;PlayerSettings.allowedAutorotateToLandscapeRight=false;
            PlayerSettings.iOS.targetDevice=iOSTargetDevice.iPhoneAndiPad;PlayerSettings.iOS.requiresFullScreen=true;
            PlayerSettings.iOS.sdkVersion=iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(
                EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Select(s=>new EditorBuildSettingsScene(s.path,false))).ToArray();
            AssetDatabase.SaveAssets();Debug.Log("C6_T10A_PREPARED scene="+ScenePath+" config="+ConfigPath+" build="+LobbyBuildInfo.Build);
        }
        [PostProcessBuild(900)]
        public static void ApplyBonjour(BuildTarget target,string output)
        {
            if(target!=BuildTarget.iOS||!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==ScenePath))return;
            ApplyPlist(Path.Combine(output,"Info.plist"));
            // Apple's installed SDK re-exports public DNSService* from the default libSystem link.
            // No private library path, absent libdns_sd.tbd, or multicast entitlement is added.
            Debug.Log("C6_T10A_BONJOUR_PLIST_APPLIED native=libSystem");
        }
        static void ApplyPlist(string path)
        {
            var plist=new PlistDocument();plist.ReadFromFile(path);
            plist.root.SetString("NSLocalNetworkUsageDescription",LocalNetworkPurpose);
            PlistElementArray list;
            if(plist.root.values.TryGetValue("NSBonjourServices",out var existing))list=existing.AsArray();
            else list=plist.root.CreateArray("NSBonjourServices");
            if(!list.values.Any(v=>v.AsString()==BonjourRoomDiscovery.ServiceType))list.AddString(BonjourRoomDiscovery.ServiceType);
            plist.WriteToFile(path);
        }
        static void ApplyMacDeclarations(string appPath)
        {
            ApplyPlist(Path.Combine(appPath,"Contents/Info.plist"));
            // This local development player has no distribution identity. Re-sign its modified plist ad hoc.
            var start=new System.Diagnostics.ProcessStartInfo("/usr/bin/codesign") {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("--force");start.ArgumentList.Add("--deep");start.ArgumentList.Add("--sign");start.ArgumentList.Add("-");start.ArgumentList.Add(appPath);
            using(var process=System.Diagnostics.Process.Start(start)){process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException("Local Mac signing failed: "+process.StandardError.ReadToEnd());}
        }
        [MenuItem("C6/T10A/Export iOS")]
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("C6/T10A/Build macOS Room Lobby")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6Lobby.app");

        static void Build(BuildTarget target, string relativeOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Missing build support: " + target);
            // URP resource/variant collection uses the Editor's active target. BuildPlayer's
            // requested target alone can collect iOS/Mobile resources for a macOS/PC player.
            // Reject before Prepare or any output writes; callers must select the real target.
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    " but T10A requested " + target + ". Select the matching platform, or restart batch Unity with -buildTarget " + target + ".");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputRoot = Environment.GetEnvironmentVariable("C6_T10A_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(root, "Builds", "T10-A");
            string path = Path.GetFullPath(Path.Combine(outputRoot, relativeOutput));
            // Do this before Prepare so a build-path collision cannot change scenes or settings.
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("Output exists; use a new C6_T10A_OUTPUT_ROOT to preserve previous builds.");
            Prepare();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var receipt = new BuildReceipt
            {
                target = target.ToString(), activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                output = path, unityVersion = Application.unityVersion,
                executedAtUtc = DateTime.UtcNow.ToString("O")
            };
            BuildReport report = null;
            DateTime started = DateTime.UtcNow;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = path,
                    target = target, options = BuildOptions.Development
                });
                receipt.result = report.summary.result.ToString();
                receipt.errors = report.summary.totalErrors;
                receipt.warnings = report.summary.totalWarnings;
                receipt.durationSeconds = report.summary.totalTime.TotalSeconds;
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("T10A build failed: " + report.summary.result);
                if (target == BuildTarget.StandaloneOSX) ApplyMacDeclarations(path);
                if (target == BuildTarget.iOS)
                {
                    if (!File.Exists(Path.Combine(path, "Unity-iPhone.xcodeproj/project.pbxproj")))
                        throw new FileNotFoundException("Missing exported Xcode project.");
                    string plistPath = Path.Combine(path, "Info.plist");
                    if (!File.Exists(plistPath)) throw new FileNotFoundException("Missing exported iOS Info.plist.");
                    var plist = new PlistDocument();
                    plist.ReadFromFile(plistPath);
                    if (!plist.root.values.TryGetValue("NSLocalNetworkUsageDescription", out var purpose) ||
                        purpose.AsString() != LocalNetworkPurpose)
                        throw new InvalidOperationException("Local Network usage description was not applied.");
                }
                if (target == BuildTarget.iOS)
                {
                    var info = new PlistDocument(); info.ReadFromFile(Path.Combine(path, "Info.plist"));
                    if (!info.root.values.TryGetValue("NSBonjourServices", out var services) ||
                        !services.AsArray().values.Any(v => v.AsString() == BonjourRoomDiscovery.ServiceType))
                        throw new InvalidOperationException("Missing C6 Bonjour declaration.");

                }
                receipt.outputValidation = "PASS";
                Debug.Log("C6_T10A_BUILD_COMPLETE target=" + target + " output=" + path);
            }
            catch (Exception exception)
            {
                receipt.outputValidation = "FAIL";
                receipt.exceptionType = exception.GetType().FullName;
                if (report == null)
                {
                    receipt.result = "Exception";
                    receipt.errors = 1;
                    receipt.durationSeconds = (DateTime.UtcNow - started).TotalSeconds;
                }
                throw;
            }
            finally
            {
                string logs = Path.Combine(root, "Logs", "T10-A");
                Directory.CreateDirectory(logs);
                File.WriteAllText(Path.Combine(logs, "build-" + target + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json"),
                    JsonUtility.ToJson(receipt, true));
            }
        }

        [Serializable]
        sealed class BuildReceipt
        {
            public string buildId = "C6-T10A";
            public string buildNumber = "14";
            public string target;
            public string activeBuildTarget;
            public string unityVersion;
            public string result = "NOT_RUN";
            public string output;
            public string executedAtUtc;
            public string outputValidation = "NOT_RUN";
            public string exceptionType;
            public int errors;
            public int warnings;
            public double durationSeconds;
        }
    }
}
