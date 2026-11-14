using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameDemoBuildTests
    {
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static Type Builder => Type.GetType("Hapio.Editor.GameDemoBuild, Assembly-CSharp-Editor", true);
        private static object Invoke(string method, params object[] arguments) => Builder.GetMethod(method, Static).Invoke(null, arguments);
        private static string Constant(string name) => (string)Builder.GetField(name, Static).GetRawConstantValue();

        [TestCase(BuildTarget.StandaloneOSX)]
        [TestCase(BuildTarget.iOS)]
        public void ReleaseDemoKeepsDeveloperControlsOffAndQaExplicitlyEnablesThem(BuildTarget target)
        {
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            string savedDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            string savedIdentifier = PlayerSettings.GetApplicationIdentifier(namedTarget);
            string[] savedScenes = EditorBuildSettings.scenes.Select(scene => scene.path + ":" + scene.enabled).ToArray();
            bool savedDevelopment = EditorUserBuildSettings.development;
            var release = (BuildPlayerOptions)Invoke("CreateOptions", target, "unused-release-output", false);
            var qa = (BuildPlayerOptions)Invoke("CreateOptions", target, "unused-qa-output", true);

            Assert.That(release.options, Is.EqualTo(BuildOptions.None), "DEVELOPMENT_BUILD gates the existing DEV and SOLO input surfaces.");
            Assert.That(qa.options, Is.EqualTo(BuildOptions.Development));
            CollectionAssert.AreEqual(new[] { LabGameBuildVariant.StaffStageDemoDefine }, release.extraScriptingDefines);
            CollectionAssert.AreEqual(release.extraScriptingDefines, qa.extraScriptingDefines, "Content selection is independent of Development mode.");
            CollectionAssert.AreEqual(GameScenePaths.All, release.scenes);
            Assert.That(release.scenes, Is.Not.SameAs(GameScenePaths.All), "Build options cannot mutate the normal scene array.");
            Assert.That(release.scenes[0], Is.EqualTo(GameScenePaths.Home));
            Assert.That(release.scenes, Has.No.Member("Assets/Lab/Scenes/Lab.unity"));
            Assert.That(PlayerSettings.GetScriptingDefineSymbols(namedTarget), Is.EqualTo(savedDefines));
            Assert.That(PlayerSettings.GetApplicationIdentifier(namedTarget), Is.EqualTo(savedIdentifier));
            Assert.That(EditorUserBuildSettings.development, Is.EqualTo(savedDevelopment));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path + ":" + scene.enabled), Is.EqualTo(savedScenes));
            Assert.That(LabGameBuildVariant.IsDemo, Is.False, "Preparing a player does not change the Editor's variant.");
        }

        [TestCase(BuildTarget.StandaloneOSX, "success")]
        [TestCase(BuildTarget.StandaloneOSX, "failure")]
        [TestCase(BuildTarget.StandaloneOSX, "cancel")]
        [TestCase(BuildTarget.iOS, "success")]
        [TestCase(BuildTarget.iOS, "failure")]
        [TestCase(BuildTarget.iOS, "cancel")]
        public void TemporaryIdentityAlwaysRestoresNormalSettings(BuildTarget target, string outcome)
        {
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            string name = PlayerSettings.productName;
            string identifier = PlayerSettings.GetApplicationIdentifier(namedTarget);
            string version = PlayerSettings.bundleVersion;
            string build = PlayerSettings.iOS.buildNumber;
            string displayName = PlayerSettings.iOS.applicationDisplayName;
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            bool development = EditorUserBuildSettings.development;
            bool called = false;
            Action simulatedBuild = () =>
            {
                called = true;
                Assert.That(PlayerSettings.productName, Is.EqualTo(Constant("ProductName")));
                Assert.That(PlayerSettings.GetApplicationIdentifier(namedTarget), Is.EqualTo("com.example.hapio.staffdemo"));
                Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(Constant("DemoVersion")));
                if (target == BuildTarget.iOS) Assert.That(PlayerSettings.iOS.buildNumber, Is.EqualTo(Constant("DemoBuildNumber")));
                if (target == BuildTarget.iOS) Assert.That(PlayerSettings.iOS.applicationDisplayName, Is.EqualTo(Constant("ProductName")));
                EditorUserBuildSettings.development = !development;
                if (outcome == "cancel") throw new OperationCanceledException("Simulated build cancellation");
                if (outcome == "failure") throw new InvalidOperationException("Simulated build failure");
            };
            try
            {
                if (outcome == "success") Invoke("WithTemporaryPlayerSettings", target, "com.example.hapio.staffdemo", simulatedBuild);
                else
                {
                    TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                        Invoke("WithTemporaryPlayerSettings", target, "com.example.hapio.staffdemo", simulatedBuild));
                    Assert.That(error.InnerException, Is.TypeOf(outcome == "cancel" ? typeof(OperationCanceledException) : typeof(InvalidOperationException)));
                }
                Assert.That(called, Is.True);
                Assert.That(PlayerSettings.productName, Is.EqualTo(name));
                Assert.That(PlayerSettings.GetApplicationIdentifier(namedTarget), Is.EqualTo(identifier));
                Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(version));
                Assert.That(PlayerSettings.iOS.buildNumber, Is.EqualTo(build));
                Assert.That(PlayerSettings.iOS.applicationDisplayName, Is.EqualTo(displayName));
                Assert.That(EditorUserBuildSettings.development, Is.EqualTo(development));
                Assert.That(PlayerSettings.GetScriptingDefineSymbols(namedTarget), Is.EqualTo(defines));
            }
            finally
            {
                // A failed restoration assertion must still leave the developer's project intact.
                PlayerSettings.productName = name;
                PlayerSettings.SetApplicationIdentifier(namedTarget, identifier);
                PlayerSettings.bundleVersion = version;
                PlayerSettings.iOS.buildNumber = build;
                PlayerSettings.iOS.applicationDisplayName = displayName;
                EditorUserBuildSettings.development = development;
            }
        }

        [Test]
        public void DemoIdentifierAllowsCoexistenceAndRejectsAnAlreadyPersistedDemoIdentifier()
        {
            Assert.That(Invoke("DemoApplicationIdentifier", "com.example.hapio"), Is.EqualTo("com.example.hapio.staffdemo"));
            TargetInvocationException missing = Assert.Throws<TargetInvocationException>(() => Invoke("DemoApplicationIdentifier", ""));
            Assert.That(missing.InnerException, Is.TypeOf<InvalidOperationException>());
            foreach (string suffix in new[] { ".staffdemo", ".yaguangdemo" })
            {
                TargetInvocationException persisted = Assert.Throws<TargetInvocationException>(() => Invoke("DemoApplicationIdentifier", "com.example.hapio" + suffix));
                Assert.That(persisted.InnerException, Is.TypeOf<InvalidOperationException>());
            }
        }

        [Test]
        public void StaffManifestRecordsAllSelectableCanonicalStagesWithoutChangingNormalCompatibility()
        {
            string normalConnection = LabNetwork.GameConnectionCompatibility;
            string normalDiscovery = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            object manifest = Invoke("CreateManifest", BuildTarget.StandaloneOSX, false,
                "com.example.hapio.staffdemo", "qa-generation", project);
            Type type = manifest.GetType();
            const BindingFlags fields = BindingFlags.Public | BindingFlags.Instance;
            Assert.That(type.GetField("variant", fields).GetValue(manifest), Is.EqualTo("staff-stage-demo"));
            Assert.That(type.GetField("participants", fields).GetValue(manifest), Is.EqualTo(3));
            Assert.That(type.GetField("scriptingDefine", fields).GetValue(manifest), Is.EqualTo(LabGameBuildVariant.StaffStageDemoDefine));
            var stages = (Array)type.GetField("selectableStages", fields).GetValue(manifest);
            Assert.That(stages.Length, Is.EqualTo(3));
            var catalog = GameStageCatalog.RequireDefault();
            for (int index = 0; index < stages.Length; index++)
            {
                object stage = stages.GetValue(index);
                Type stageType = stage.GetType();
                Assert.That(stageType.GetField("stageIndex", fields).GetValue(stage), Is.EqualTo(index + 1));
                Assert.That(stageType.GetField("stageId", fields).GetValue(stage), Is.EqualTo(catalog.Get(index + 1).stageId));
                Assert.That(stageType.GetField("yokai", fields).GetValue(stage), Is.EqualTo(catalog.Get(index + 1).yokai.ToString()));
                Assert.That(stageType.GetField("stageSettingsSha256", fields).GetValue(stage), Does.Match("^[0-9a-f]{64}$"));
            }
            Assert.That(type.GetField("connectionCompatibility", fields).GetValue(manifest), Is.Not.EqualTo(normalConnection));
            Assert.That(type.GetField("discoveryCompatibility", fields).GetValue(manifest), Is.Not.EqualTo(normalDiscovery));
            Assert.That(LabNetwork.GameConnectionCompatibility, Is.EqualTo(normalConnection));
            Assert.That(LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game), Is.EqualTo(normalDiscovery));
            Assert.That(LabGameBuildVariant.IsDemo, Is.False);
        }

        [Test]
        public void SavedStaffSelectorHasThreeDistinctStageBoxesAndReusesTheHomeCanvas()
        {
            Type authoring = Type.GetType("Hapio.Editor.StaffDemoUiAssets, Assembly-CSharp-Editor", true);
            Assert.DoesNotThrow(() => authoring.GetMethod("Validate", Static).Invoke(null, null));
            var prefab = Resources.Load<GameObject>(GameStaffDemoView.ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponent<GameStaffDemoView>();
            Assert.That(view.stageButtons.Distinct().Count(), Is.EqualTo(3));
            Assert.That(prefab.GetComponentInChildren<Canvas>(true), Is.Null,
                "Home owns the existing scalable root Canvas; a preview Canvas must not collapse the saved selector.");
            Assert.That(prefab.GetComponentInChildren<UnityEngine.UI.CanvasScaler>(true), Is.Null);
            Assert.That(prefab.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(true), Is.Null);
            var rect = prefab.GetComponent<RectTransform>();
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(rect.rect.size, Is.EqualTo(new Vector2(390f, 844f)));
            Assert.That(prefab.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true), Is.Null,
                "The existing Home scene owns the sole EventSystem; the selector does not duplicate it.");
        }

        [TestCase("$(PRODUCT_BUNDLE_IDENTIFIER)")]
        [TestCase("${PRODUCT_BUNDLE_IDENTIFIER}")]
        public void IosOutputIdentityChecksTheExportedBundleAndVisibleName(string identifierVariable)
        {
            string output = Path.Combine(Path.GetTempPath(), "hapio-demo-build-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(output, "Unity-iPhone.xcodeproj"));
            const string identifier = "com.example.hapio.staffdemo";
            try
            {
                new XDocument(new XElement("plist", new XElement("dict",
                    Pair("CFBundleDisplayName", Constant("ProductName")), Pair("CFBundleIdentifier", identifierVariable),
                    Pair("CFBundleShortVersionString", Constant("DemoVersion")), Pair("CFBundleVersion", Constant("DemoBuildNumber")))))
                    .Save(Path.Combine(output, "Info.plist"));
                string project = Path.Combine(output, "Unity-iPhone.xcodeproj", "project.pbxproj");
                string appConfig = "buildSettings = { PRODUCT_NAME = \"$(PRODUCT_NAME_APP)\"; PRODUCT_BUNDLE_IDENTIFIER = \"" + identifier + "\"; };";
                File.WriteAllText(project, appConfig + appConfig);
                Assert.DoesNotThrow(() => Invoke("ValidateOutputIdentity", BuildTarget.iOS, output, identifier));
                File.WriteAllText(project, appConfig + appConfig.Replace(identifier, "com.example.hapio"));
                TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Invoke("ValidateOutputIdentity", BuildTarget.iOS, output, identifier));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>(), "A normal app ID would replace the installed normal app.");
            }
            finally { Directory.Delete(output, true); }
        }

        private static object[] Pair(string key, string value) => new object[] { new XElement("key", key), new XElement("string", value) };
    }
}
