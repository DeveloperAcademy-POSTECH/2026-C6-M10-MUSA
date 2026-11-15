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
using Splash = UnityEditor.PlayerSettings.SplashScreen;

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
            bool showSplash = Splash.show;
            bool unityLogo = Splash.showUnityLogo;
            var animation = Splash.animationMode;
            var logoStyle = Splash.unityLogoStyle;
            Color backgroundColor = Splash.backgroundColor;
            var background = Splash.background;
            var backgroundPortrait = Splash.backgroundPortrait;
            bool blurBackground = Splash.blurBackgroundImage;
            float overlay = Splash.overlayOpacity;
            var logos = Splash.logos.ToArray();
            var applicationIcons = target == BuildTarget.StandaloneOSX
                ? PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application).ToArray() : null;
            var iconKinds = target == BuildTarget.iOS
                ? PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS) : Array.Empty<PlatformIconKind>();
            var platformIcons = iconKinds.Select(kind => PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind)).ToArray();
            // Slot objects are mutable; keeping their texture arrays alone would miss restoration failures.
            var originalTextures = platformIcons.Select(slots => slots.Select(slot => slot.GetTextures()?.ToArray()).ToArray()).ToArray();
            bool called = false;
            Action simulatedBuild = () =>
            {
                called = true;
                Assert.That(Constant(productConstant), Is.EqualTo("합이오_쇼케이스"));
                Assert.That(PlayerSettings.productName, Is.EqualTo("합이오_쇼케이스"),
                    "The native iOS display-name setter aliases productName; internal executable normalization belongs to the export only.");
                Assert.That(PlayerSettings.GetApplicationIdentifier(namedTarget), Is.EqualTo(expectedIdentifier));
                Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(Constant("DemoVersion")));
                if (target == BuildTarget.iOS) Assert.That(PlayerSettings.iOS.buildNumber, Is.EqualTo(Constant("DemoBuildNumber")));
                if (target == BuildTarget.iOS) Assert.That(PlayerSettings.iOS.applicationDisplayName, Is.EqualTo("합이오_쇼케이스"));
                Assert.That(Splash.show, Is.True, "The custom mark replaces the Unity splash instead of removing all startup branding.");
                Assert.That(Splash.showUnityLogo, Is.False);
                Assert.That(Splash.animationMode.ToString(), Is.EqualTo("Static"));
                Assert.That(Splash.unityLogoStyle.ToString(), Is.EqualTo("DarkOnLight"));
                Assert.That(Splash.overlayOpacity, Is.EqualTo(1f));
                Assert.That(Splash.backgroundColor.r, Is.GreaterThan(.8f));
                Assert.That(Splash.backgroundColor.g, Is.GreaterThan(.8f));
                Assert.That(Splash.backgroundColor.b, Is.GreaterThan(.8f));
                Assert.That(Splash.backgroundColor.a, Is.EqualTo(1f));
                Assert.That(Splash.background, Is.Null);
                Assert.That(Splash.backgroundPortrait, Is.Null);
                Assert.That(Splash.blurBackgroundImage, Is.False);
                var expectedLogo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/Branding/ShowcaseLogo.png");
                var expectedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Hapio/Art/Branding/ShowcaseIcon.png");
                Assert.That(expectedLogo, Is.Not.Null);
                Assert.That(expectedIcon, Is.Not.Null);
                Assert.That(Splash.logos, Has.Length.EqualTo(1));
                Assert.That(Splash.logos[0].logo, Is.SameAs(expectedLogo));
                Assert.That(Splash.logos[0].duration, Is.EqualTo(2f));
                if (target == BuildTarget.StandaloneOSX)
                {
                    var current = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application);
                    Assert.That(current, Is.Not.Empty);
                    Assert.That(current, Has.All.SameAs(expectedIcon));
                }
                else
                {
                    Assert.That(iconKinds, Is.Not.Empty);
                    foreach (var kind in iconKinds)
                    {
                        var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                        Assert.That(slots, Is.Not.Empty);
                        foreach (var slot in slots)
                            Assert.That(slot.GetTexture(0), Is.SameAs(expectedIcon), "Every supported iOS icon size must replace its Unity placeholder.");
                    }
                }
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
                Assert.That(Splash.show, Is.EqualTo(showSplash));
                Assert.That(Splash.showUnityLogo, Is.EqualTo(unityLogo));
                Assert.That(Splash.animationMode, Is.EqualTo(animation));
                Assert.That(Splash.unityLogoStyle, Is.EqualTo(logoStyle));
                Assert.That(Splash.backgroundColor, Is.EqualTo(backgroundColor));
                Assert.That(Splash.background, Is.SameAs(background));
                Assert.That(Splash.backgroundPortrait, Is.SameAs(backgroundPortrait));
                Assert.That(Splash.blurBackgroundImage, Is.EqualTo(blurBackground));
                Assert.That(Splash.overlayOpacity, Is.EqualTo(overlay));
                Assert.That(Splash.logos, Has.Length.EqualTo(logos.Length));
                for (int i = 0; i < logos.Length; i++)
                {
                    Assert.That(Splash.logos[i].logo, Is.SameAs(logos[i].logo));
                    Assert.That(Splash.logos[i].duration, Is.EqualTo(logos[i].duration));
                }
                if (target == BuildTarget.StandaloneOSX)
                    Assert.That(PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Application), Is.EqualTo(applicationIcons));
                for (int kind = 0; kind < iconKinds.Length; kind++)
                {
                    var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, iconKinds[kind]);
                    Assert.That(slots, Has.Length.EqualTo(originalTextures[kind].Length));
                    for (int slot = 0; slot < slots.Length; slot++)
                        Assert.That(slots[slot].GetTextures(), Is.EqualTo(originalTextures[kind][slot]),
                            "A previously empty icon slot must remain empty after this export.");
                }
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
                Splash.show = showSplash;
                Splash.showUnityLogo = unityLogo;
                Splash.animationMode = animation;
                Splash.unityLogoStyle = logoStyle;
                Splash.backgroundColor = backgroundColor;
                Splash.background = background;
                Splash.backgroundPortrait = backgroundPortrait;
                Splash.blurBackgroundImage = blurBackground;
                Splash.overlayOpacity = overlay;
                Splash.logos = logos;
                if (target == BuildTarget.StandaloneOSX)
                    PlayerSettings.SetIcons(NamedBuildTarget.Standalone, applicationIcons, IconKind.Application);
                for (int kind = 0; kind < iconKinds.Length; kind++)
                {
                    for (int slot = 0; slot < platformIcons[kind].Length; slot++)
                        platformIcons[kind][slot].SetTextures(originalTextures[kind][slot]);
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, iconKinds[kind], platformIcons[kind]);
                }
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
        public void StageTwoOutputIdentitySharesTheShowcaseNameButRequiresItsIdentifierAndVersion(BuildTarget target)
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
                Assert.That(product, Is.EqualTo("합이오_쇼케이스"));
                Assert.That(Constant("ProductName"), Is.EqualTo(product), "Both exports intentionally use the same visible showcase name.");
                Assert.DoesNotThrow(() => Invoke("ValidateOutputIdentity", target, output, identifier),
                    "The shared visible name does not make the fixed-stage identity invalid.");
                var wrongVariant = Assert.Throws<TargetInvocationException>(() =>
                    Invoke("ValidateOutputIdentity", target, output, "com.example.hapio.staffdemo"));
                Assert.That(wrongVariant.InnerException, Is.TypeOf<InvalidOperationException>(),
                    "A shared visible name must never accept the other variant's bundle identifier.");
                Write("Hapio Stage 2 Demo", identifier, version, build);
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

        [TestCase("NFC")]
        [TestCase("NFD")]
        [TestCase("existing-target")]
        public void MacExecutableNormalizationPreservesVisibleBrandingAndRejectsNameCollisions(string scenario)
        {
            const string visibleName = "합이오_쇼케이스";
            const string normalizedExecutable = "HapioShowcase";
            string originalExecutable = visibleName.Normalize(scenario == "NFD"
                ? System.Text.NormalizationForm.FormD : System.Text.NormalizationForm.FormC);
            string root = Path.Combine(Path.GetTempPath(), "hapio-showcase-executable-" + Guid.NewGuid().ToString("N"));
            string output = Path.Combine(root, visibleName + ".app");
            string contents = Path.Combine(output, "Contents");
            string macos = Path.Combine(contents, "MacOS");
            string resources = Path.Combine(contents, "Resources");
            string originalPath = Path.Combine(macos, originalExecutable);
            string normalizedPath = Path.Combine(macos, normalizedExecutable);
            string plist = Path.Combine(contents, "Info.plist");
            Directory.CreateDirectory(macos);
            Directory.CreateDirectory(Path.Combine(resources, "Data"));
            byte[] executable = { 0xcf, 0xfa, 0xed, 0xfe, 1, 2, 3, 4 };
            byte[] icon = { 0x69, 0x63, 0x6e, 0x73, 9, 8, 7, 6 };
            byte[] gameData = { 5, 4, 3, 2, 1 };
            var originalPlist = new XDocument(new XElement("plist", new XElement("dict",
                Pair("CFBundleName", visibleName), Pair("CFBundleDisplayName", visibleName),
                Pair("CFBundleExecutable", originalExecutable), Pair("CFBundleIdentifier", "com.example.hapio.staffdemo.stage2"),
                Pair("CFBundleIconFile", "PlayerIcon.icns"), Pair("CFBundleShortVersionString", "0.2.0"), Pair("CFBundleVersion", "2"))));
            try
            {
                File.WriteAllBytes(originalPath, executable);
                File.WriteAllBytes(Path.Combine(resources, "PlayerIcon.icns"), icon);
                File.WriteAllBytes(Path.Combine(resources, "Data", "globalgamemanagers"), gameData);
                originalPlist.Save(plist);
                if (scenario == "existing-target")
                {
                    byte[] collision = { 99, 98, 97 };
                    File.WriteAllBytes(normalizedPath, collision);
                    byte[] originalPlistBytes = File.ReadAllBytes(plist);
                    Assert.Throws<TargetInvocationException>(() => Invoke("NormalizeMacExecutable", output),
                        "Packaging must reject a pre-existing target rather than overwrite an unrelated executable.");
                    Assert.That(File.ReadAllBytes(originalPath), Is.EqualTo(executable));
                    Assert.That(File.ReadAllBytes(normalizedPath), Is.EqualTo(collision));
                    Assert.That(File.ReadAllBytes(plist), Is.EqualTo(originalPlistBytes));
                }
                else
                {
                    Assert.DoesNotThrow(() => Invoke("NormalizeMacExecutable", output));
                    Assert.That(File.Exists(originalPath), Is.False);
                    Assert.That(File.ReadAllBytes(normalizedPath), Is.EqualTo(executable),
                        "Only the filename changes; the executable bytes must remain intact.");
                    var expectedPlist = new XDocument(originalPlist);
                    expectedPlist.Root.Element("dict").Elements("key")
                        .Single(key => key.Value == "CFBundleExecutable").ElementsAfterSelf().First().Value = normalizedExecutable;
                    Assert.That(XDocument.Load(plist).ToString(), Is.EqualTo(expectedPlist.ToString()),
                        "The Korean visible name, bundle identity, version and icon reference must remain unchanged.");
                    byte[] normalizedPlist = File.ReadAllBytes(plist);
                    Assert.DoesNotThrow(() => Invoke("NormalizeMacExecutable", output));
                    Assert.That(File.ReadAllBytes(plist), Is.EqualTo(normalizedPlist), "A second packaging pass is a no-op.");
                    Assert.That(File.ReadAllBytes(normalizedPath), Is.EqualTo(executable));
                }
                Assert.That(Directory.Exists(Path.Combine(root, visibleName + ".app")), Is.True);
                Assert.That(File.ReadAllBytes(Path.Combine(resources, "PlayerIcon.icns")), Is.EqualTo(icon));
                Assert.That(File.ReadAllBytes(Path.Combine(resources, "Data", "globalgamemanagers")), Is.EqualTo(gameData));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IosExecutableNormalizationOnlyChangesTheExportedApplicationProductName(bool missingSetting)
        {
            string root = Path.Combine(Path.GetTempPath(), "hapio-showcase-ios-executable-" + Guid.NewGuid().ToString("N"));
            string project = Path.Combine(root, "Unity-iPhone.xcodeproj", "project.pbxproj");
            string plist = Path.Combine(root, "Info.plist");
            Directory.CreateDirectory(Path.GetDirectoryName(project));
            string first = "PRODUCT_NAME_APP = \"합이오_쇼케이스\";";
            string second = "PRODUCT_NAME_APP= HapioStage2Demo;";
            string original = "Release = { buildSettings = { " + first
                + " PRODUCT_NAME = \"$(PRODUCT_NAME_APP)\"; PRODUCT_BUNDLE_IDENTIFIER = com.example.hapio.staffdemo.stage2;"
                + " DEVELOPMENT_TEAM = TESTTEAM; CODE_SIGN_STYLE = Automatic; }; name = Release; };\n"
                + "Debug = { buildSettings = { " + second
                + " PRODUCT_NAME = \"$(PRODUCT_NAME_APP)\"; PRODUCT_BUNDLE_IDENTIFIER = com.example.hapio.staffdemo.stage2;"
                + " DEVELOPMENT_TEAM = TESTTEAM; CODE_SIGN_STYLE = Manual; }; name = Debug; };\n"
                + "Framework = { PRODUCT_NAME = UnityFramework; name = UnityFramework; };\n";
            if (missingSetting) original = original.Replace(first, string.Empty).Replace(second, string.Empty);
            try
            {
                File.WriteAllText(project, original);
                new XDocument(new XElement("plist", new XElement("dict",
                    Pair("CFBundleDisplayName", "합이오_쇼케이스"), Pair("CFBundleName", "합이오_쇼케이스"),
                    Pair("CFBundleIdentifier", "com.example.hapio.staffdemo.stage2"), Pair("CFBundleExecutable", "$(EXECUTABLE_NAME)"))))
                    .Save(plist);
                byte[] plistBytes = File.ReadAllBytes(plist);
                byte[] projectBytes = File.ReadAllBytes(project);
                if (missingSetting)
                {
                    var error = Assert.Throws<TargetInvocationException>(() => Invoke("NormalizeIosExecutable", root));
                    Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(File.ReadAllBytes(project), Is.EqualTo(projectBytes), "An unexpected export format must fail without altering the project.");
                }
                else
                {
                    Assert.DoesNotThrow(() => Invoke("NormalizeIosExecutable", root));
                    string expected = original.Replace(first, "PRODUCT_NAME_APP = HapioShowcase;")
                        .Replace(second, "PRODUCT_NAME_APP = HapioShowcase;");
                    Assert.That(File.ReadAllText(project), Is.EqualTo(expected),
                        "Both app configurations change; bundle IDs, signing, target names and UnityFramework settings remain untouched.");
                    byte[] normalized = File.ReadAllBytes(project);
                    Assert.DoesNotThrow(() => Invoke("NormalizeIosExecutable", root));
                    Assert.That(File.ReadAllBytes(project), Is.EqualTo(normalized));
                }
                Assert.That(File.ReadAllBytes(plist), Is.EqualTo(plistBytes), "The Korean visible display name belongs to Info.plist and must remain intact.");
            }
            finally { Directory.Delete(root, true); }
        }

        private static object[] Pair(string key, string value) => new object[] { new XElement("key", key), new XElement("string", value) };
    }
}
