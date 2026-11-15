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
        public void TemporaryIdentityAlwaysRestoresNormalSettings(BuildTarget target, string outcome) =>
            AssertTemporaryIdentityRestored(target, outcome, "WithTemporaryPlayerSettings", "ProductName", ".staffdemo");

        [TestCase(BuildTarget.StandaloneOSX, "success")]
        [TestCase(BuildTarget.StandaloneOSX, "failure")]
        [TestCase(BuildTarget.StandaloneOSX, "cancel")]
        [TestCase(BuildTarget.iOS, "success")]
        [TestCase(BuildTarget.iOS, "failure")]
        [TestCase(BuildTarget.iOS, "cancel")]
        public void StageTwoIdentityRestoresNormalSettingsAfterSuccessFailureAndCancellation(BuildTarget target, string outcome) =>
            AssertTemporaryIdentityRestored(target, outcome, "WithTemporaryStageTwoPlayerSettings", "StageTwoProductName", ".staffdemo.stage2");

        private static void AssertTemporaryIdentityRestored(BuildTarget target, string outcome,
            string builderMethod, string productConstant, string suffix)
        {
            string expectedIdentifier = "com.example.hapio" + suffix;
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
                Assert.That(PlayerSettings.productName, Is.EqualTo(Constant(productConstant)));
                Assert.That(PlayerSettings.GetApplicationIdentifier(namedTarget), Is.EqualTo(expectedIdentifier));
                Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(Constant("DemoVersion")));
                if (target == BuildTarget.iOS) Assert.That(PlayerSettings.iOS.buildNumber, Is.EqualTo(Constant("DemoBuildNumber")));
                if (target == BuildTarget.iOS) Assert.That(PlayerSettings.iOS.applicationDisplayName, Is.EqualTo(Constant(productConstant)));
                EditorUserBuildSettings.development = !development;
                if (outcome == "cancel") throw new OperationCanceledException("Simulated build cancellation");
                if (outcome == "failure") throw new InvalidOperationException("Simulated build failure");
            };
            try
            {
                if (outcome == "success") Invoke(builderMethod, target, expectedIdentifier, simulatedBuild);
                else
                {
                    TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                        Invoke(builderMethod, target, expectedIdentifier, simulatedBuild));
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

        [TestCase(BuildTarget.StandaloneOSX)]
        [TestCase(BuildTarget.iOS)]
        public void StageTwoOptionsRestrictContentWithoutChangingTheOrdinaryStaffBuild(BuildTarget target)
        {
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            string identifier = PlayerSettings.GetApplicationIdentifier(namedTarget);
            string[] scenes = EditorBuildSettings.scenes.Select(scene => scene.path + ":" + scene.enabled).ToArray();
            bool development = EditorUserBuildSettings.development;
            var release = (BuildPlayerOptions)Invoke("CreateStageTwoOptions", target, "unused-stage-two-release", false);
            var qa = (BuildPlayerOptions)Invoke("CreateStageTwoOptions", target, "unused-stage-two-qa", true);
            var ordinary = (BuildPlayerOptions)Invoke("CreateOptions", target, "unused-three-card-demo", false);
            Assert.That(release.options, Is.EqualTo(BuildOptions.None));
            Assert.That(qa.options, Is.EqualTo(BuildOptions.Development));
            Assert.That(release.extraScriptingDefines, Is.EqualTo(new[] {
                LabGameBuildVariant.StaffStageDemoDefine, LabGameBuildVariant.StaffStageTwoOnlyDefine }));
            Assert.That(qa.extraScriptingDefines, Is.EqualTo(release.extraScriptingDefines));
            Assert.That(ordinary.extraScriptingDefines, Is.EqualTo(new[] { LabGameBuildVariant.StaffStageDemoDefine }),
                "Adding the fixed-stage export must not silently restrict the existing three-card export.");
            Assert.That(release.scenes, Is.EqualTo(GameScenePaths.All));
            Assert.That(release.scenes, Is.Not.SameAs(GameScenePaths.All));
            Assert.That(release.target, Is.EqualTo(target));
            Assert.That(release.locationPathName, Is.EqualTo("unused-stage-two-release"));
            Assert.That(PlayerSettings.GetScriptingDefineSymbols(namedTarget), Is.EqualTo(defines));
            Assert.That(PlayerSettings.GetApplicationIdentifier(namedTarget), Is.EqualTo(identifier));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path + ":" + scene.enabled), Is.EqualTo(scenes));
            Assert.That(EditorUserBuildSettings.development, Is.EqualTo(development));
            Assert.That(LabGameBuildVariant.IsStaffStageTwoOnly, Is.False, "Preparing a build must not change the Editor's content variant.");
        }

        [Test]
        public void StageTwoIdentifierIsSeparateAndRejectsPersistedDemoSuffixes()
        {
            Assert.That(Invoke("StageTwoApplicationIdentifier", "com.example.hapio"), Is.EqualTo("com.example.hapio.staffdemo.stage2"));
            Assert.That(Invoke("DemoApplicationIdentifier", "com.example.hapio"), Is.EqualTo("com.example.hapio.staffdemo"));
            foreach (string builderMethod in new[] { "StageTwoApplicationIdentifier", "DemoApplicationIdentifier" })
            {
                var missing = Assert.Throws<TargetInvocationException>(() => Invoke(builderMethod, ""));
                Assert.That(missing.InnerException, Is.TypeOf<InvalidOperationException>());
                foreach (string suffix in new[] { ".staffdemo.stage2", ".staffdemo", ".yaguangdemo" })
                {
                    var persisted = Assert.Throws<TargetInvocationException>(() => Invoke(builderMethod, "com.example.hapio" + suffix));
                    Assert.That(persisted.InnerException, Is.TypeOf<InvalidOperationException>(),
                        "A previous demo ID must not produce a nested suffix or replace another export.");
                }
            }
        }

        [Test]
        public void StageTwoManifestRetainsCanonicalStageTwoAndStaffRoomCompatibility()
        {
            string normalConnection = LabNetwork.GameConnectionCompatibility;
            string normalDiscovery = LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            object manifest = Invoke("CreateStageTwoManifest", BuildTarget.StandaloneOSX, false,
                "com.example.hapio.staffdemo.stage2", "stage-two-generation", project);
            object ordinary = Invoke("CreateManifest", BuildTarget.StandaloneOSX, false,
                "com.example.hapio.staffdemo", "ordinary-generation", project);
            Type type = manifest.GetType();
            object Get(object value, string field) => type.GetField(field).GetValue(value);
            Assert.That(Get(manifest, "productName"), Is.EqualTo(Constant("StageTwoProductName")));
            Assert.That(Get(manifest, "applicationIdentifier"), Is.EqualTo("com.example.hapio.staffdemo.stage2"));
            Assert.That(Get(manifest, "participants"), Is.EqualTo(3));
            Assert.That(Get(manifest, "development"), Is.EqualTo(false));
            Assert.That((string)Get(manifest, "scriptingDefine"), Does.Contain(LabGameBuildVariant.StaffStageDemoDefine)
                .And.Contain(LabGameBuildVariant.StaffStageTwoOnlyDefine));
            var stages = (Array)Get(manifest, "selectableStages");
            Assert.That(stages.Length, Is.EqualTo(1));
            object stage = stages.GetValue(0);
            Type stageType = stage.GetType();
            Assert.That(stageType.GetField("stageIndex").GetValue(stage), Is.EqualTo(2), "The single route position must not become canonical Stage1.");
            Assert.That(stageType.GetField("stageId").GetValue(stage), Is.EqualTo(GameStageCatalog.RequireDefault().Get(2).stageId));
            Assert.That(stageType.GetField("yokai").GetValue(stage), Is.EqualTo(LabStageYokai.Yaguang.ToString()));
            Assert.That(stageType.GetField("stageSettingsSha256").GetValue(stage), Does.Match("^[0-9a-f]{64}$"));
            Assert.That(Get(manifest, "scenes"), Is.EqualTo(GameScenePaths.All));
            Assert.That(Get(manifest, "connectionCompatibility"), Is.EqualTo(Get(ordinary, "connectionCompatibility")));
            Assert.That(Get(manifest, "discoveryCompatibility"), Is.EqualTo(Get(ordinary, "discoveryCompatibility")));
            Assert.That(Get(manifest, "variantRevision"), Is.EqualTo(LabGameBuildVariant.StaffStageDemoRevision));
            Assert.That(LabNetwork.GameConnectionCompatibility, Is.EqualTo(normalConnection));
            Assert.That(LabBonjourDiscovery.ProtocolFor(LabRoomFamily.Game), Is.EqualTo(normalDiscovery));
            Assert.That(LabGameBuildVariant.IsDemo || LabGameBuildVariant.IsStaffStageTwoOnly, Is.False);
        }

        [Test]
        public void StageTwoTestScopeCannotRestrictNormalOrOrdinaryStaffContentAfterDisposal()
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(false))
            using (LabGameBuildVariant.OverrideStaffStageTwoOnlyForTests(true))
                Assert.That(LabGameBuildVariant.IsStaffStageTwoOnly, Is.False, "The fixed restriction requires the staff content variant.");
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            {
                Assert.That(LabGameBuildVariant.IsStaffStageTwoOnly, Is.False);
                using (LabGameBuildVariant.OverrideStaffStageTwoOnlyForTests(true))
                {
                    Assert.That(LabGameBuildVariant.IsStaffStageTwoOnly, Is.True);
                    foreach (int stage in new[] { -1, 0, 1, 3, 4 }) Assert.That(LabGameBuildVariant.AllowsStaffDemoStage(stage), Is.False);
                    Assert.That(LabGameBuildVariant.AllowsStaffDemoStage(2), Is.True);
                    using (LabGameBuildVariant.OverrideStaffStageTwoOnlyForTests(false))
                        foreach (int stage in new[] { 1, 2, 3 }) Assert.That(LabGameBuildVariant.AllowsStaffDemoStage(stage), Is.True);
                    Assert.That(LabGameBuildVariant.IsStaffStageTwoOnly, Is.True, "Nested scopes restore their caller.");
                }
                Assert.That(LabGameBuildVariant.IsStaffStageTwoOnly, Is.False);
                foreach (int stage in new[] { 1, 2, 3 }) Assert.That(LabGameBuildVariant.AllowsStaffDemoStage(stage), Is.True);
            }
            Assert.That(LabGameBuildVariant.IsStaffStageTwoOnly, Is.False);
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

        [TestCase(BuildTarget.StandaloneOSX)]
        [TestCase(BuildTarget.iOS)]
        public void StageTwoOutputIdentityRequiresItsOwnNameIdentifierAndVersion(BuildTarget target)
        {
            string output = Path.Combine(Path.GetTempPath(), "hapio-stage-two-identity-" + Guid.NewGuid().ToString("N"));
            string plist = target == BuildTarget.iOS ? Path.Combine(output, "Info.plist") : Path.Combine(output, "Contents", "Info.plist");
            Directory.CreateDirectory(Path.GetDirectoryName(plist));
            const string identifier = "com.example.hapio.staffdemo.stage2";
            string product = Constant("StageTwoProductName");
            string version = Constant("DemoVersion");
            string build = Constant("DemoBuildNumber");
            void Write(string name, string id, string shortVersion, string buildNumber) =>
                new XDocument(new XElement("plist", new XElement("dict",
                    Pair(target == BuildTarget.iOS ? "CFBundleDisplayName" : "CFBundleName", name),
                    Pair("CFBundleIdentifier", id), Pair("CFBundleShortVersionString", shortVersion),
                    Pair("CFBundleVersion", buildNumber)))).Save(plist);
            void Reject()
            {
                var error = Assert.Throws<TargetInvocationException>(() =>
                    Invoke("ValidateOutputIdentityFor", target, output, identifier, product));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            }
            try
            {
                Write(product, identifier, version, build);
                Assert.DoesNotThrow(() => Invoke("ValidateOutputIdentityFor", target, output, identifier, product));
                Assert.Throws<TargetInvocationException>(() => Invoke("ValidateOutputIdentity", target, output, identifier),
                    "The ordinary staff validator must keep expecting the ordinary product name.");
                Write(Constant("ProductName"), identifier, version, build);
                Reject();
                Write(product, "com.example.hapio.staffdemo", version, build);
                Reject();
                Write(product, identifier, "999.0.0", build);
                Reject();
                if (target == BuildTarget.iOS)
                {
                    Write(product, identifier, version, "999");
                    Reject();
                }
            }
            finally { Directory.Delete(output, true); }
        }

        private static object[] Pair(string key, string value) => new object[] { new XElement("key", key), new XElement("string", value) };
    }
}
