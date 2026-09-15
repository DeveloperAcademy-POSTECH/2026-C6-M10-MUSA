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
    public static class PhysicsBattleBuild
    {
        public const string BuildNumber = "21";
        public const string SourceScenePath = InterruptionBuild.ScenePath;
        public const string ScenePath="Assets/_Project/HapioMVP/Scenes/PhysicsBattle.unity";
        public const string MonsterPrefabPath="Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab";
        public const string LegacyVisualName="T04 Training Dummy - Visual Only";
        public const string ConfigPath="Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        public const string LocalNetworkPurpose="같은 Wi-Fi에서 C6 방을 찾고 두 기기를 연결해 함께 플레이합니다.";
        [MenuItem("C6/Next Phase/P1/Prepare Physics Battle")]
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
            EnsureMonsterPrefab();
            if(create) CreateDerivedScene();
            ValidateSavedScene();
            PlayerSettings.productName="C6 Prototype";PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.wolfuraark.c6prototype.p1.desktop");
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
            Debug.Log("C6_P1_PREPARED scene="+ScenePath+" config="+ConfigPath+" build="+BuildNumber);
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
                    throw new InvalidOperationException("P1 shared assets could not be reloaded after scene creation.");

                var layout = layouts[0];
                layout.gameObject.name = "P1PhysicsBattle";
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

                // Replace the copied old visual and standalone collider only inside the new scene.
                var hitTargets = roots.SelectMany(root => root.GetComponentsInChildren<MonsterHitTarget>(true)).ToArray();
                if (hitTargets.Length != 1 || hitTargets[0].GetComponent<BoxCollider>() == null)
                    throw new InvalidOperationException("The saved game scene needs one wired training hit target.");
                var visualRoots = Components<Transform>(target).Where(t => t.name == LegacyVisualName).ToArray();
                if (visualRoots.Length != 1)
                    throw new InvalidOperationException("The copied source must have exactly one legacy visual dummy.");
                UnityEngine.Object.DestroyImmediate(visualRoots[0].gameObject);
                UnityEngine.Object.DestroyImmediate(hitTargets[0].gameObject);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
                var monsterObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target);
                monsterObject.name = "BenchmarkMonster";
                var monster = monsterObject.GetComponent<BenchmarkMonster>();
                monster.ConfigureRuntimeLayout(layout);
                PrefabUtility.RecordPrefabInstancePropertyModifications(monster);
                var hitTarget = monster.Target;
                roots = target.GetRootGameObjects();

                var controller = layout.gameObject.GetComponent<T09BattleController>();
                controller.Configure(layout, session, hud, material, launchFrame, hitTarget);
                controller.ConfigureOrbPhysics(true);
                layout.gameObject.GetComponent<OrbPointerInput>().Configure(controller);

                // The saved T13 scene already owns these services. Reuse exactly one of each;
                // duplicate Lobby/NGO roots would start a second transport or discard the shared state.
                RequireOnRoot<GameRuntimeConfig>(roots, layout.gameObject);
                var lobby = RequireOnRoot<T10LobbySession>(roots, layout.gameObject);
                var lobbyHud = RequireOnRoot<T10LobbyHud>(roots, layout.gameObject);
                var lobbyController = RequireOnRoot<T10LobbyController>(roots, layout.gameObject);
                var game = RequireOnRoot<T10GameSession>(roots, layout.gameObject);
                lobby.Configure(config); lobby.ConfigureBuild(BuildNumber);
                lobbyController.Configure(lobby, lobbyHud);
                if (game.BuildIdentifier != InterruptionBuild.BuildNumber || !game.TransfersEnabled || !game.ReachableEdgeTransferDistance || !game.InterruptionHandlingEnabled)
                    throw new InvalidOperationException("P1 must derive from the preserved build20 interruption scene.");
                game.ConfigureTransfers(true);
                game.ConfigureReachableEdgeTransferDistance(true);
                game.ConfigureInterruptions(true);
                game.ConfigureBuildIdentifier(BuildNumber);
                ValidateMonster(monsterObject);
                if (Components<Transform>(target).Any(t => t.name == LegacyVisualName))
                    throw new InvalidOperationException("A duplicate old monster visual survived replacement.");
                VerifySceneReferences(target);
                // New scene render settings belong to the new scene, not to the preview or open saved game.
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.37f, .42f, .49f);
                RenderSettings.fog = false;
                if (!EditorSceneManager.SaveScene(target, ScenePath))
                    throw new InvalidOperationException("Could not save the separate P1 scene.");
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



        public static void EnsureMonsterPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
            if (existing != null) { ValidateMonster(existing); return; }
            if (File.Exists(MonsterPrefabPath) || File.Exists(MonsterPrefabPath + ".meta"))
                throw new InvalidOperationException("Occupied monster prefab path; preserving it.");
            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            if (config == null) throw new InvalidOperationException("The shared Config must exist before prefab preparation.");
            int battleLayer = LayerMask.NameToLayer("C6Battle");
            if (battleLayer < 0) throw new InvalidOperationException("The existing C6Battle layer is missing.");
            Scene preview = default;
            GameObject root = null;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(SourceScenePath);
                var originals = Components<Transform>(preview).Where(t => t.name == LegacyVisualName).ToArray();
                if (originals.Length != 1 || originals[0].GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("The saved source must contain exactly one visual-only dummy.");
                root = new GameObject("BenchmarkMonster");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.layer = battleLayer;
                var identity = root.AddComponent<MonsterHitTarget>();
                identity.Configure(config.MonsterTargetId);
                var visual = UnityEngine.Object.Instantiate(originals[0].gameObject, root.transform, false);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                var hitboxObject = new GameObject("Hitbox");
                hitboxObject.transform.SetParent(root.transform, false);
                hitboxObject.layer = battleLayer;
                var box = hitboxObject.AddComponent<BoxCollider>();
                var monster = root.AddComponent<BenchmarkMonster>();
                monster.Configure(config, box, visual.transform);
                ValidateMonster(root);
                string directory = Path.GetDirectoryName(MonsterPrefabPath).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    string parent = Path.GetDirectoryName(directory).Replace('\\', '/');
                    if (!AssetDatabase.IsValidFolder(parent)) throw new InvalidOperationException("Missing project asset directory.");
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(directory));
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save the benchmark monster prefab.");
                ValidateMonster(saved);
                Debug.Log("C6_P1_MONSTER_PREPARED prefab=" + MonsterPrefabPath + " config=" + ConfigPath);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static void ValidateMonster(GameObject root)
        {
            var monster = root.GetComponent<BenchmarkMonster>();
            var target = root.GetComponent<MonsterHitTarget>();
            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (monster == null || target == null || monster.SavedConfig != AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath)
                || monster.Hitbox == null || monster.Visual == null || monster.Hitbox.transform.parent != root.transform
                || monster.Visual.parent != root.transform || monster.Hitbox.gameObject.name != "Hitbox"
                || monster.Visual.name != "Visual" || colliders.Length != 1 || colliders[0] != monster.Hitbox
                || monster.Hitbox.isTrigger || root.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || root.GetComponentsInChildren<MonsterHitTarget>(true).Length != 1)
                throw new InvalidOperationException("Benchmark monster requires one shared Config, one static non-trigger child hitbox, and one root target identity.");
            if (root.GetComponentsInChildren<Renderer>(true).Length == 0
                || monster.Visual.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Benchmark monster visual geometry must be visible and independent of hit detection.");
            int battleLayer = LayerMask.NameToLayer("C6Battle");
            if (battleLayer < 0 || root.layer != battleLayer || monster.Hitbox.gameObject.layer != battleLayer)
                throw new InvalidOperationException("The monster and its hitbox must retain the C6Battle layer.");
        }

        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static T RequireOnRoot<T>(GameObject[] roots, GameObject expectedRoot) where T : Component
        {
            var found = roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1 || found[0].gameObject != expectedRoot)
                throw new InvalidOperationException("The saved P1 scene requires one " + typeof(T).Name + " on its shared game root.");
            return found[0];
        }

        public static void ValidateSavedScene()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Could not inspect the saved P1 scene.");
                var roots = scene.GetRootGameObjects();
                var layouts = roots.SelectMany(root => root.GetComponentsInChildren<SplitScreenLayout>(true)).ToArray();
                if (layouts.Length != 1 || layouts[0].Config != AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath))
                    throw new InvalidOperationException("P1 must retain exactly one shared layout Config.");
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
                if (!game.InterruptionHandlingEnabled || !game.TransfersEnabled || !game.ReachableEdgeTransferDistance || game.BuildIdentifier != BuildNumber || lobby.BuildIdentifier != BuildNumber)
                    throw new InvalidOperationException("The existing scene is not the expected build21 physics mode; it was preserved.");
                if (layout.BattleCamera == null || layout.OrbCamera == null || layout.BattleCamera == layout.OrbCamera)
                    throw new InvalidOperationException("P1 requires its two distinct remapped cameras.");
                if (!RequireOnRoot<T09BattleController>(roots, root).OrbPhysicsEnabled)
                    throw new InvalidOperationException("The P1 scene must explicitly enable local orb physics.");
                var monsters = Components<BenchmarkMonster>(scene);
                if (monsters.Length != 1 || Components<MonsterHitTarget>(scene).Length != 1)
                    throw new InvalidOperationException("The P1 scene must contain exactly one benchmark monster.");
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
            Debug.Log("C6_P1_BONJOUR_PLIST_APPLIED native=libSystem");
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
        [MenuItem("C6/Next Phase/P1/Export iOS")]
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("C6/Next Phase/P1/Build macOS Physics Battle")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6Physics.app");

        static void Build(BuildTarget target, string relativeOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Missing build support: " + target);
            // URP resource/variant collection uses the Editor's active target. BuildPlayer's
            // requested target alone can collect iOS/Mobile resources for a macOS/PC player.
            // Reject before Prepare or any output writes; callers must select the real target.
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    " but P1 requested " + target + ". Select the matching platform, or restart batch Unity with -buildTarget " + target + ".");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputRoot = Environment.GetEnvironmentVariable("C6_P1_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(root, "Builds", "NextPhase", "P1");
            string path = Path.GetFullPath(Path.Combine(outputRoot, relativeOutput));
            // Do this before Prepare so a build-path collision cannot change scenes or settings.
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("Output exists; use a new C6_P1_OUTPUT_ROOT to preserve previous builds.");
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
                    throw new InvalidOperationException("P1 build failed: " + report.summary.result);
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
                        throw new InvalidOperationException("The lifecycle patch receipt must identify this actual P1 scene.");
                }
                receipt.outputValidation = "PASS";
                Debug.Log("C6_P1_BUILD_COMPLETE target=" + target + " output=" + path);
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
                string logs = Path.Combine(root, "Logs", "NextPhase", "P1");
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
            public string buildId = "C6-P1";
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
