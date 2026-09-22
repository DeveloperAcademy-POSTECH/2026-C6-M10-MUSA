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
    public static class ContinuousTransferBuild
    {
        public const string BuildNumber = "25";
        public const string SourceScenePath = FivePlayerBattleBuild.ScenePath;
        public const string ScenePath="Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        public const string MonsterPrefabPath="Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab";
        public const string LegacyVisualName="T04 Training Dummy - Visual Only";
        public const string ConfigPath="Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        public const string LocalNetworkPurpose="같은 Wi-Fi에서 C6 방을 찾고 최대 다섯 기기를 연결해 함께 플레이합니다.";
        [MenuItem("C6/Next Phase/P4/Connect Explicit Mac Validation Probe")]
        public static void EnsureProbeForValidation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            ValidateSavedScene(false);
            var original = SceneManager.GetActiveScene();
            var target = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !target.IsValid() || !target.isLoaded;
            if (!opened && target.isDirty) throw new InvalidOperationException("Save the user's dirty P4 scene before connecting its probe.");
            if (opened && Application.isBatchMode)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Preserving a dirty scene; use a clean verification process.");
            try
            {
                if (opened) target = EditorSceneManager.OpenScene(ScenePath,
                    Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
                var games = Components<T10GameSession>(target);
                if (games.Length != 1) throw new InvalidOperationException("One P4 game root is required.");
                var probes = Components<P4ContinuousTransferProbe>(target);
                if (probes.Length > 1 || probes.Length == 1 && probes[0].gameObject != games[0].gameObject)
                    throw new InvalidOperationException("Existing validation wiring is ambiguous; preserving it.");
                if (probes.Length == 0)
                {
                    games[0].gameObject.AddComponent<P4ContinuousTransferProbe>();
                    VerifySceneReferences(target);
                    if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Could not save the explicit P4 probe connection.");
                }
                Debug.Log("C6_P4_PROBE_CONNECTED scene=" + ScenePath + " mode=explicit-development-standalone-arguments-only");
            }
            finally
            {
                if (opened && !Application.isBatchMode && target.IsValid() && target.isLoaded)
                    EditorSceneManager.CloseScene(target, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }
        [MenuItem("C6/Next Phase/P4/Prepare Continuous Transfer Battle")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
            var config=AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            if(config==null)throw new InvalidOperationException("Existing shared Config is required.");
            bool create=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null;
            if(create&&(File.Exists(ScenePath)||File.Exists(ScenePath+".meta")))throw new InvalidOperationException("Occupied scene path; preserving it.");
            if(create&&!Application.isBatchMode)
                for(int i=0;i<SceneManager.sceneCount;i++)if(string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save the untitled scene or use a verification copy.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
            if (prefab == null) throw new InvalidOperationException("Prepare the existing P1 monster prefab first; P4 does not replace it.");
            ValidateMonster(prefab);
            if(create) CreateDerivedScene();
            ValidateSavedScene();
            PlayerSettings.productName="C6 Prototype";PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.wolfuraark.c6prototype.p4.desktop");
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
            Debug.Log("C6_P4_PREPARED scene="+ScenePath+" config="+ConfigPath+" build="+BuildNumber);
        }
        static void CreateDerivedScene()
        {
            // CopyAsset is Unity's asset copy API: it creates a new scene GUID while preserving local
            // file IDs, prefab links and all intra-scene references. Never open or modify the source.
            FivePlayerBattleBuild.ValidateSavedScene();
            var source = EditorSceneManager.OpenPreviewScene(SourceScenePath);
            try
            {
                var game = Components<T10GameSession>(source).Single();
                var controller = Components<T09BattleController>(source).Single();
                if (game.BuildIdentifier != FivePlayerBattleBuild.BuildNumber || game.ContinuousTransfersEnabled
                    || controller.ContinuousTransfersEnabled || Components<T10LobbySession>(source).Single().ContinuousTransfersEnabled)
                    throw new InvalidOperationException("P4 must derive from the preserved build23 manual transfer scene.");
            }
            finally { if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source); }
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                throw new InvalidOperationException("Unity could not copy the saved P3 scene to its unoccupied P4 path.");
            Scene original = SceneManager.GetActiveScene();
            Scene target = default;
            try
            {
                target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var game = Components<T10GameSession>(target).Single();
                var lobby = Components<T10LobbySession>(target).Single();
                var controller = Components<T09BattleController>(target).Single();
                game.gameObject.name = "P4ContinuousTransferBattle";
                lobby.ConfigureBuild(BuildNumber); lobby.ConfigureCapacity(5); lobby.ConfigureContinuousTransfers(true);
                game.ConfigureMaximumParticipants(5); game.ConfigureTransfers(true);
                game.ConfigureContinuousTransfers(true); game.ConfigureBuildIdentifier(BuildNumber);
                controller.ConfigureMaximumParticipants(5); controller.ConfigureContinuousTransfers(true);
                foreach (var oldProbe in Components<P3MultiplayerProbe>(target)) UnityEngine.Object.DestroyImmediate(oldProbe);
                if (Components<P4ContinuousTransferProbe>(target).Length != 0)
                    throw new InvalidOperationException("Source unexpectedly contains a P4 probe; preserving the copied scene for inspection.");
                game.gameObject.AddComponent<P4ContinuousTransferProbe>();
                VerifySceneReferences(target);
                if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Could not save the separate P4 scene.");
            }
            finally
            {
                if (target.IsValid() && target.isLoaded) EditorSceneManager.CloseScene(target, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }

        public static void ValidateMonster(GameObject root) => FivePlayerBattleBuild.ValidateMonster(root);

        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static T RequireOnRoot<T>(GameObject[] roots, GameObject expectedRoot) where T : Component
        {
            var found = roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1 || found[0].gameObject != expectedRoot)
                throw new InvalidOperationException("The saved P4 scene requires one " + typeof(T).Name + " on its shared game root.");
            return found[0];
        }

        public static void ValidateSavedScene() => ValidateSavedScene(true);
        static void ValidateSavedScene(bool requireProbe)
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Could not inspect the saved P4 scene.");
                var roots = scene.GetRootGameObjects();
                var layouts = roots.SelectMany(root => root.GetComponentsInChildren<SplitScreenLayout>(true)).ToArray();
                if (layouts.Length != 1 || layouts[0].Config != AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath))
                    throw new InvalidOperationException("P4 must retain exactly one shared layout Config.");
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
                if (!game.ContinuousTransfersEnabled || !lobby.ContinuousTransfersEnabled || !root.GetComponent<T09BattleController>().ContinuousTransfersEnabled
                    || lobby.ProtocolVersion != LobbyProtocol.ContinuousTransferVersion || game.MaximumParticipants != 5 || lobby.MaximumParticipants != 5 || root.GetComponent<T09BattleController>().MaximumParticipants != 5 || !game.InterruptionHandlingEnabled || !game.TransfersEnabled || !game.ReachableEdgeTransferDistance || game.BuildIdentifier != BuildNumber || lobby.BuildIdentifier != BuildNumber)
                    throw new InvalidOperationException("The existing scene is not the expected build25 developer-settings mode; it was preserved.");
                if (layout.BattleCamera == null || layout.OrbCamera == null || layout.BattleCamera == layout.OrbCamera)
                    throw new InvalidOperationException("P4 requires its two distinct remapped cameras.");
                if (!RequireOnRoot<T09BattleController>(roots, root).OrbPhysicsEnabled
                    || !RequireOnRoot<T09BattleController>(roots, root).ReleaseThrowsEnabled)
                    throw new InvalidOperationException("The P4 scene must explicitly enable local orb physics and release throws.");
                RequireOnRoot<ThrowBattleFraming>(roots, root);
                if (requireProbe) RequireOnRoot<P4ContinuousTransferProbe>(roots, root);
                if (Components<P3MultiplayerProbe>(scene).Length != 0 || Components<P2ThrowProbe>(scene).Length != 0)
                    throw new InvalidOperationException("P4 must replace only the copied scene validation probe.");
                var floors = Components<ThrowBattleFloor>(scene);
                if (floors.Length != 1 || floors[0].GetComponent<BoxCollider>() == null
                    || floors[0].GetComponent<BoxCollider>().isTrigger || floors[0].GetComponent<MonsterHitTarget>() != null)
                    throw new InvalidOperationException("P4 requires one non-target bounce floor collider.");
                var monsters = Components<BenchmarkMonster>(scene);
                if (monsters.Length != 1 || Components<MonsterHitTarget>(scene).Length != 1)
                    throw new InvalidOperationException("The P4 scene must contain exactly one benchmark monster.");
                ValidateMonster(monsters[0].gameObject);
                if (Components<Transform>(scene).Any(t => t.name == LegacyVisualName))
                    throw new InvalidOperationException("The legacy dummy must not duplicate the new prefab visual.");
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
            Debug.Log("C6_P4_BONJOUR_PLIST_APPLIED native=libSystem");
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
        [MenuItem("C6/Next Phase/P4/Export iOS")]
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("C6/Next Phase/P4/Build macOS Continuous Transfer Battle")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6ContinuousTransfer.app");

        static void Build(BuildTarget target, string relativeOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Missing build support: " + target);
            // URP resource/variant collection uses the Editor's active target. BuildPlayer's
            // requested target alone can collect iOS/Mobile resources for a macOS/PC player.
            // Reject before Prepare or any output writes; callers must select the real target.
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    " but P4 requested " + target + ". Select the matching platform, or restart batch Unity with -buildTarget " + target + ".");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputRoot = Environment.GetEnvironmentVariable("C6_P4_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(root, "Builds", "NextPhase", "P4");
            string path = Path.GetFullPath(Path.Combine(outputRoot, relativeOutput));
            // Do this before Prepare so a build-path collision cannot change scenes or settings.
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("Output exists; use a new C6_P4_OUTPUT_ROOT to preserve previous builds.");
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
                    throw new InvalidOperationException("P4 build failed: " + report.summary.result);
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
                    string lifecyclePath = Path.Combine(path, "C6T12LifecyclePatch", "patch-receipt.json");
                    if (!File.Exists(lifecyclePath))
                        throw new FileNotFoundException("Missing reviewed foreground lifecycle patch receipt.", lifecyclePath);
                    var lifecycle = JsonUtility.FromJson<LifecycleReceipt>(File.ReadAllText(lifecyclePath));
                    if (lifecycle == null || lifecycle.status != "APPLIED_SOURCE_ONLY" || lifecycle.scene != ScenePath)
                        throw new InvalidOperationException("The lifecycle patch receipt must identify this actual P4 scene.");
                }
                receipt.outputValidation = "PASS";
                Debug.Log("C6_P4_BUILD_COMPLETE target=" + target + " output=" + path);
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
                string logs = Path.Combine(root, "Logs", "NextPhase", "P4");
                Directory.CreateDirectory(logs);
                File.WriteAllText(Path.Combine(logs, "build-" + target + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json"),
                    JsonUtility.ToJson(receipt, true));
            }
        }

        [Serializable]
        sealed class LifecycleReceipt { public string status, scene; }

        [Serializable]
        sealed class BuildReceipt
        {
            public string buildId = "C6-P4";
            public string buildNumber = BuildNumber;
            public string scene = ScenePath;
            public string config = ConfigPath;
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
