using System;
using System.IO;
using System.Linq;
using C6.Prototype.Lobby;
using C6.Prototype.GameSync;
using C6.Prototype.Battle;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using UnityEngine.Rendering;
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
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    public static class OrbTransferBuild
    {
        public const string BuildNumber = "16";
        public const string SourceScenePath = GameSyncBuild.ScenePath;
        public const string ScenePath="Assets/_Project/HapioMVP/Scenes/OrbTransferBattle.unity";
        public const string ConfigPath="Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        public const string LocalNetworkPurpose="같은 Wi-Fi에서 C6 방을 찾고 두 기기를 연결해 함께 플레이합니다.";
        [MenuItem("C6/T11/Prepare Orb Transfer Battle")]
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
            if(create) CreateDerivedScene();
            ValidateSavedScene();
            PlayerSettings.productName="C6 Prototype";PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.wolfuraark.c6prototype.t11.desktop");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,"com.wolfuraark.c6prototype");
            PlayerSettings.iOS.buildNumber=BuildNumber;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=false;PlayerSettings.allowedAutorotateToLandscapeRight=false;
            PlayerSettings.iOS.targetDevice=iOSTargetDevice.iPhoneAndiPad;PlayerSettings.iOS.requiresFullScreen=true;
            PlayerSettings.iOS.sdkVersion=iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(
                EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Select(s=>new EditorBuildSettingsScene(s.path,false))).ToArray();
            AssetDatabase.SaveAssets();Debug.Log("C6_T11_PREPARED scene="+ScenePath+" config="+ConfigPath+" build="+BuildNumber);
        }
        static void CreateDerivedScene()
        {
            Scene originalActive = SceneManager.GetActiveScene();
            Scene preview = default;
            Scene target = default;
            try
            {
                // Preview loads the saved asset independently of any open, possibly dirty saved game scene.
                preview = EditorSceneManager.OpenPreviewScene(SourceScenePath);
                if (!preview.IsValid() || !preview.isLoaded)
                    throw new InvalidOperationException("Could not open the saved game scene as a preview.");
                var sourceRoots = preview.GetRootGameObjects();
                var sourceLayouts = sourceRoots.SelectMany(root => root.GetComponentsInChildren<SplitScreenLayout>(true)).ToArray();
                if (sourceLayouts.Length != 1 || sourceLayouts[0].BattleCamera == null || sourceLayouts[0].OrbCamera == null)
                    throw new InvalidOperationException("The saved game scene must contain one wired SplitScreenLayout.");
                string battleCameraName = sourceLayouts[0].BattleCamera.name;
                string orbCameraName = sourceLayouts[0].OrbCamera.name;
                if (battleCameraName == orbCameraName)
                    throw new InvalidOperationException("saved game cameras need distinct names for safe scene reference remapping.");

                target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(target);
                foreach (var source in sourceRoots)
                {
                    var copy = UnityEngine.Object.Instantiate(source);
                    copy.name = source.name;
                    SceneManager.MoveGameObjectToScene(copy, target);
                }

                var roots = target.GetRootGameObjects();
                var layouts = roots.SelectMany(root => root.GetComponentsInChildren<SplitScreenLayout>(true)).ToArray();
                var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                var battleCameras = cameras.Where(camera => camera.name == battleCameraName).ToArray();
                var orbCameras = cameras.Where(camera => camera.name == orbCameraName).ToArray();
                if (layouts.Length != 1 || battleCameras.Length != 1 || orbCameras.Length != 1 || cameras.Length != 2)
                    throw new InvalidOperationException("The copied saved game layout/cameras could not be remapped unambiguously.");
                var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
                var material = AssetDatabase.LoadAssetAtPath<Material>(BattleLoopBuild.SpriteMaterialPath);
                if (config == null || material == null)
                    throw new InvalidOperationException("T11 shared assets could not be reloaded after scene creation.");

                var layout = layouts[0];
                layout.gameObject.name = "T11OrbTransferBattle";
                layout.Configure(config, battleCameras[0], orbCameras[0]);
                var hud = layout.gameObject.GetComponent<T09Hud>();
                hud.Configure(layout);
                var session = layout.gameObject.GetComponent<DirectConnectionSession>();
                if (session == null)
                    throw new InvalidOperationException("The saved game scene must contain its DirectConnectionSession.");
                var frames = roots.SelectMany(root => root.GetComponentsInChildren<AttackLaunchFrame>(true)).ToArray();
                if (frames.Length != 1) throw new InvalidOperationException("The saved game scene needs one launch frame.");
                var launchFrame = frames[0];
                launchFrame.Configure(config);

                // Preserve the copied saved game collider and its tuning; only remap the scene reference.
                var hitTargets = roots.SelectMany(root => root.GetComponentsInChildren<MonsterHitTarget>(true)).ToArray();
                if (hitTargets.Length != 1 || hitTargets[0].GetComponent<BoxCollider>() == null)
                    throw new InvalidOperationException("The saved game scene needs one wired training hit target.");
                var hitTarget = hitTargets[0];

                var controller = layout.gameObject.GetComponent<T09BattleController>();
                controller.Configure(layout, session, hud, material, launchFrame, hitTarget);
                layout.gameObject.GetComponent<OrbPointerInput>().Configure(controller);

                // The saved T10-B scene already owns these services. Reuse exactly one of each;
                // duplicate Lobby/NGO roots would start a second transport or discard the shared state.
                RequireOnRoot<GameRuntimeConfig>(roots, layout.gameObject);
                var lobby = RequireOnRoot<T10LobbySession>(roots, layout.gameObject);
                var lobbyHud = RequireOnRoot<T10LobbyHud>(roots, layout.gameObject);
                var lobbyController = RequireOnRoot<T10LobbyController>(roots, layout.gameObject);
                var game = RequireOnRoot<T10GameSession>(roots, layout.gameObject);
                lobby.Configure(config); lobby.ConfigureBuild(BuildNumber);
                lobbyController.Configure(lobby, lobbyHud);
                game.ConfigureTransfers(true);
                VerifySceneReferences(target);
                // New scene render settings belong to the new scene, not to the preview or open saved game.
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.37f, .42f, .49f);
                RenderSettings.fog = false;
                if (!EditorSceneManager.SaveScene(target, ScenePath))
                    throw new InvalidOperationException("Could not save the separate T11 scene.");
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (!Application.isBatchMode)
                {
                    if (target.IsValid() && target.isLoaded) EditorSceneManager.CloseScene(target, true);
                    if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                }
            }
        }


        static T RequireOnRoot<T>(GameObject[] roots, GameObject expectedRoot) where T : Component
        {
            var found = roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1 || found[0].gameObject != expectedRoot)
                throw new InvalidOperationException("The saved T11 scene requires one " + typeof(T).Name + " on its shared game root.");
            return found[0];
        }

        static void ValidateSavedScene()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Could not inspect the saved T11 scene.");
                var roots = scene.GetRootGameObjects();
                var layouts = roots.SelectMany(root => root.GetComponentsInChildren<SplitScreenLayout>(true)).ToArray();
                if (layouts.Length != 1 || layouts[0].Config != AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath))
                    throw new InvalidOperationException("T11 must retain exactly one shared layout Config.");
                var layout = layouts[0]; var root = layout.gameObject;
                RequireOnRoot<GameRuntimeConfig>(roots, root);
                RequireOnRoot<DirectConnectionSession>(roots, root);
                RequireOnRoot<T09Hud>(roots, root);
                RequireOnRoot<T09BattleController>(roots, root);
                RequireOnRoot<OrbPointerInput>(roots, root);
                var game = RequireOnRoot<T10GameSession>(roots, root);
                var lobby = RequireOnRoot<T10LobbySession>(roots, root);
                RequireOnRoot<T10LobbyHud>(roots, root);
                RequireOnRoot<T10LobbyController>(roots, root);
                if (!game.TransfersEnabled || game.BuildIdentifier != BuildNumber || lobby.BuildIdentifier != BuildNumber)
                    throw new InvalidOperationException("The existing scene is not the expected build 16 transfer mode; it was preserved.");
                if (layout.BattleCamera == null || layout.OrbCamera == null || layout.BattleCamera == layout.OrbCamera)
                    throw new InvalidOperationException("T11 requires its two distinct remapped cameras.");
                VerifySceneReferences(scene);
            }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void VerifySceneReferences(Scene scene)
        {
            foreach (var component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)))
            {
                if (component == null) throw new InvalidOperationException("The scene contains a missing script.");
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        var referencedObject = reference is Component value ? value.gameObject : reference as GameObject;
                        if (referencedObject != null && referencedObject.scene.IsValid() && referencedObject.scene != scene)
                            throw new InvalidOperationException("Scene reference was not remapped: " + component.name + "." + property.propertyPath);
                    }
                }
            }
        }

        [PostProcessBuild(900)]
        public static void ApplyBonjour(BuildTarget target,string output)
        {
            if(target!=BuildTarget.iOS||!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==ScenePath))return;
            ApplyPlist(Path.Combine(output,"Info.plist"));
            // Apple's installed SDK re-exports public DNSService* from the default libSystem link.
            // No private library path, absent libdns_sd.tbd, or multicast entitlement is added.
            Debug.Log("C6_T11_BONJOUR_PLIST_APPLIED native=libSystem");
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
        [MenuItem("C6/T11/Export iOS")]
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("C6/T11/Build macOS Orb Transfer Battle")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6Transfer.app");

        static void Build(BuildTarget target, string relativeOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Missing build support: " + target);
            // URP resource/variant collection uses the Editor's active target. BuildPlayer's
            // requested target alone can collect iOS/Mobile resources for a macOS/PC player.
            // Reject before Prepare or any output writes; callers must select the real target.
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    " but T11 requested " + target + ". Select the matching platform, or restart batch Unity with -buildTarget " + target + ".");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputRoot = Environment.GetEnvironmentVariable("C6_T11_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(root, "Builds", "T11");
            string path = Path.GetFullPath(Path.Combine(outputRoot, relativeOutput));
            // Do this before Prepare so a build-path collision cannot change scenes or settings.
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("Output exists; use a new C6_T11_OUTPUT_ROOT to preserve previous builds.");
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
                    throw new InvalidOperationException("T11 build failed: " + report.summary.result);
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
                Debug.Log("C6_T11_BUILD_COMPLETE target=" + target + " output=" + path);
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
                string logs = Path.Combine(root, "Logs", "T11");
                Directory.CreateDirectory(logs);
                File.WriteAllText(Path.Combine(logs, "build-" + target + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json"),
                    JsonUtility.ToJson(receipt, true));
            }
        }

        [Serializable]
        sealed class BuildReceipt
        {
            public string buildId = "C6-T11";
            public string buildNumber = BuildNumber;
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
