using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>Dedicated tutorial identity, native icon packaging and normal-build isolation; never builds a player.</summary>
    public sealed class GameTutorialOnlyBuildTests
    {
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static Type Builder => Type.GetType("Hapio.Editor.GameTutorialOnlyBuild, Assembly-CSharp-Editor", true);
        private static object Invoke(string name, params object[] args) => Builder.GetMethod(name, Static).Invoke(null, args);
        private static string Constant(string name) => (string)Builder.GetField(name, Static).GetRawConstantValue();
        private static void Reject(Action call)
        {
            var error = Assert.Throws<TargetInvocationException>(() => call());
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ReleaseOptionsAreTutorialOnlyAndLeaveSavedSettingsAndStaffOptionsUntouched()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            string[] scenes = EditorBuildSettings.scenes.Select(scene => scene.path + ":" + scene.enabled).ToArray();
            bool development = EditorUserBuildSettings.development;
            var options = (BuildPlayerOptions)Invoke("CreateOptions", "unused-tutorial-ios-output");
            Assert.That(options.target, Is.EqualTo(BuildTarget.iOS));
            Assert.That(options.options, Is.EqualTo(BuildOptions.None));
            Assert.That(options.extraScriptingDefines, Is.EqualTo(new[] { LabGameBuildVariant.TutorialOnlyDefine }));
            Assert.That(options.scenes, Is.EqualTo(GameScenePaths.All).And.Not.SameAs(GameScenePaths.All));
            Assert.That(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS), Is.EqualTo(defines));
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS), Is.EqualTo(identifier));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path + ":" + scene.enabled), Is.EqualTo(scenes));
            Assert.That(EditorUserBuildSettings.development, Is.EqualTo(development));
            var staff = Type.GetType("Hapio.Editor.GameDemoBuild, Assembly-CSharp-Editor", true);
            var staffOptions = (BuildPlayerOptions)staff.GetMethod("CreateStageTwoOptions", Static)
                .Invoke(null, new object[] { BuildTarget.iOS, "unused-staff-output", false });
            Assert.That(staffOptions.extraScriptingDefines,
                Is.EqualTo(new[] { LabGameBuildVariant.StaffStageDemoDefine, LabGameBuildVariant.StaffStageTwoOnlyDefine }));
            var normal = Type.GetType("Hapio.Editor.GameBuild, Assembly-CSharp-Editor", true);
            normal.GetMethod("ValidateSavedTutorialDefine", Static).Invoke(null, new object[] { "MY_LOCAL_SYMBOL" });
            Reject(() => normal.GetMethod("ValidateSavedTutorialDefine", Static)
                .Invoke(null, new object[] { "MY_LOCAL_SYMBOL;" + LabGameBuildVariant.TutorialOnlyDefine }));
        }

        [Test]
        public void PersistedVariantIdentifiersAndDefinesCannotLeakIntoAnotherExport()
        {
            Assert.That(Invoke("TutorialApplicationIdentifier", "com.example.hapio"), Is.EqualTo("com.example.hapio.tutorial"));
            foreach (string identifier in new[] { null, " ", "com.example.hapio.tutorial", "com.example.hapio.staffdemo",
                "com.example.hapio.staffdemo.stage2", "com.example.hapio.yaguangdemo" })
                Reject(() => Invoke("TutorialApplicationIdentifier", identifier));
            Assert.DoesNotThrow(() => Invoke("ValidateSavedDefines", "MY_LOCAL_SYMBOL;ANOTHER_SYMBOL"));
            foreach (string symbol in new[] { LabGameBuildVariant.TutorialOnlyDefine, LabGameBuildVariant.YaguangDemoDefine,
                LabGameBuildVariant.StaffStageDemoDefine, LabGameBuildVariant.StaffStageTwoOnlyDefine })
                Reject(() => Invoke("ValidateSavedDefines", "MY_LOCAL_SYMBOL; " + symbol));
            var staff = Type.GetType("Hapio.Editor.GameDemoBuild, Assembly-CSharp-Editor", true);
            Reject(() => staff.GetMethod("DemoApplicationIdentifier", Static)
                .Invoke(null, new object[] { "com.example.hapio.tutorial" }));
        }

        [TestCase("success")] [TestCase("failure")] [TestCase("cancel")]
        public void TutorialBuildScopeRestoresNormalIdentityAndBrandingForEveryOutcome(string outcome)
        {
            var native = UnityEditor.Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            string previous = EditorJsonUtility.ToJson(native);
            bool development = EditorUserBuildSettings.development;
            Action callback = () =>
            {
                Assert.That(PlayerSettings.productName, Is.EqualTo("합이오_튜토리얼"));
                Assert.That(PlayerSettings.iOS.applicationDisplayName, Is.EqualTo("합이오_튜토리얼"));
                Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS), Is.EqualTo("com.example.hapio.tutorial"));
                Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(Constant("Version")));
                Assert.That(PlayerSettings.iOS.buildNumber, Is.EqualTo(Constant("BuildNumber")));
                Assert.That(PlayerSettings.SplashScreen.showUnityLogo, Is.False);
                Assert.That(PlayerSettings.SplashScreen.logos.Single().logo,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/Branding/ShowcaseLogo.png")));
                EditorUserBuildSettings.development = !development;
                if (outcome == "failure") throw new InvalidOperationException("Simulated build failure");
                if (outcome == "cancel") throw new OperationCanceledException("Simulated build cancellation");
            };
            if (outcome == "success") Invoke("WithTemporaryPlayerSettings", "com.example.hapio.tutorial", callback);
            else
            {
                var error = Assert.Throws<TargetInvocationException>(() =>
                    Invoke("WithTemporaryPlayerSettings", "com.example.hapio.tutorial", callback));
                Assert.That(error.InnerException, Is.TypeOf(outcome == "cancel" ? typeof(OperationCanceledException) : typeof(InvalidOperationException)));
            }
            Assert.That(EditorJsonUtility.ToJson(native), Is.EqualTo(previous),
                "Includes saved defines, absent/native icon overrides and unrelated platform settings.");
            Assert.That(EditorUserBuildSettings.development, Is.EqualTo(development));
        }

        [Test]
        public void ManifestIdentifiesOneOfflineTutorialAndTheOriginalIconWithoutStaffSessionMetadata()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            object manifest = Invoke("CreateManifest", project, "test-generation", "com.example.hapio.tutorial");
            Type type = manifest.GetType();
            object Read(string name) => type.GetField(name).GetValue(manifest);
            Assert.That(Read("variant"), Is.EqualTo("tutorial-only"));
            Assert.That(Read("development"), Is.False);
            Assert.That(Read("offline"), Is.True);
            Assert.That(Read("participants"), Is.EqualTo(1));
            Assert.That(Read("scriptingDefine"), Is.EqualTo(LabGameBuildVariant.TutorialOnlyDefine));
            Assert.That(Read("productName"), Is.EqualTo("합이오_튜토리얼"));
            Assert.That(Read("executableName"), Is.EqualTo("HapioTutorial"));
            Assert.That(Read("scenes"), Is.EqualTo(GameScenePaths.All).And.Not.SameAs(GameScenePaths.All));
            Assert.That(Read("iconSource"), Is.EqualTo("BuildSupport/Branding/AppIcon.icon"));
            Assert.That((Array)Read("iconFiles"), Is.Not.Empty);
            Assert.That(type.GetField("connectionCompatibility"), Is.Null, "The tutorial never advertises a staff room.");
            Assert.That(type.GetField("selectableStages"), Is.Null);
        }

        [Test]
        public void NativeIconKeepsItsSourceAndOnlyTheMainTargetSelectsItAcrossRepeatFinalization()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hapio-tutorial-export-" + Guid.NewGuid().ToString("N"));
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Constant("IconSourcePath")));
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            var original = files.ToDictionary(path => path, File.ReadAllBytes);
            Directory.CreateDirectory(Path.Combine(folder, "Unity-iPhone.xcodeproj"));
            const string identifier = "com.example.hapio.tutorial";
            try
            {
                string project = Path.Combine(folder, "Unity-iPhone.xcodeproj", "project.pbxproj");
                File.WriteAllText(project, PbxFixture(identifier));
                WritePlist(folder, identifier, Constant("BuildNumber"));
                Invoke("FinalizeIosExport", folder);
                Invoke("ValidateOutputIdentity", folder, identifier);
                string first = File.ReadAllText(project);
                Invoke("FinalizeIosExport", folder);
                Assert.That(File.ReadAllText(project), Is.EqualTo(first), "Do not create duplicate files or resources when finalizing twice.");
                Assert.That(Regex.Matches(first, "lastKnownFileType = folder.iconcomposer.icon;").Count, Is.EqualTo(1));
                foreach (int index in Enumerable.Range(0, 4))
                {
                    string main = ObjectBody(first, Id(30 + index));
                    string framework = ObjectBody(first, Id(40 + index));
                    Assert.That(main, Does.Contain("ASSETCATALOG_COMPILER_APPICON_NAME = TutorialAppIcon;"));
                    Assert.That(framework, Does.Not.Contain("ASSETCATALOG_COMPILER_APPICON_NAME"));
                    Assert.That(framework, Does.Contain("FRAMEWORK_SENTINEL = unchanged;"));
                }
                string destination = Path.Combine(folder, Constant("NativeIconName") + ".icon");
                foreach (var file in original)
                    Assert.That(File.ReadAllBytes(Path.Combine(destination, file.Key.Substring(source.Length + 1))), Is.EqualTo(file.Value));
                File.AppendAllText(Path.Combine(destination, "icon.json"), " ");
                Reject(() => Invoke("CopyNativeIcon", source, destination));
                foreach (var file in original) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value));
                WritePlist(folder, identifier, "999");
                Reject(() => Invoke("ValidateOutputIdentity", folder, identifier));
            }
            finally { Directory.Delete(folder, true); }
        }

        private static string Id(int index) => index.ToString("X24");
        private static string PbxFixture(string identifier)
        {
            string[] names = { "Debug", "Release", "ReleaseForProfiling", "ReleaseForRunning" };
            var text = new StringBuilder("// !$*UTF8*$!\n{ archiveVersion = 1; classes = {}; objectVersion = 50; objects = {\n");
            void Object(int id, string body) => text.AppendLine(Id(id) + " = { " + body + " }; ");
            Object(1, "isa = PBXProject; buildConfigurationList = " + Id(4) + "; compatibilityVersion = \"Xcode 3.2\"; mainGroup = " + Id(2)
                + "; productRefGroup = " + Id(3) + "; projectDirPath = \"\"; projectRoot = \"\"; targets = (" + Id(5) + "," + Id(9) + ");");
            Object(2, "isa = PBXGroup; children = (" + Id(3) + "); sourceTree = \"<group>\";");
            Object(3, "isa = PBXGroup; children = (" + Id(8) + "," + Id(12) + "); name = Products; sourceTree = \"<group>\";");
            Object(4, "isa = XCConfigurationList; buildConfigurations = (" + string.Join(",", Enumerable.Range(20, 4).Select(Id)) + "); defaultConfigurationIsVisible = 0; defaultConfigurationName = Release;");
            Object(5, "isa = PBXNativeTarget; buildConfigurationList = " + Id(6) + "; buildPhases = (" + Id(7) + "); buildRules = (); dependencies = (); name = \"Unity-iPhone\"; productName = \"Unity-iPhone\"; productReference = " + Id(8) + "; productType = \"com.apple.product-type.application\";");
            Object(6, "isa = XCConfigurationList; buildConfigurations = (" + string.Join(",", Enumerable.Range(30, 4).Select(Id)) + "); defaultConfigurationIsVisible = 0; defaultConfigurationName = Release;");
            Object(7, "isa = PBXResourcesBuildPhase; buildActionMask = 2147483647; files = (); runOnlyForDeploymentPostprocessing = 0;");
            Object(8, "isa = PBXFileReference; explicitFileType = wrapper.application; path = Original.app; sourceTree = BUILT_PRODUCTS_DIR;");
            Object(9, "isa = PBXNativeTarget; buildConfigurationList = " + Id(10) + "; buildPhases = (" + Id(11) + "); buildRules = (); dependencies = (); name = UnityFramework; productName = UnityFramework; productReference = " + Id(12) + "; productType = \"com.apple.product-type.framework\";");
            Object(10, "isa = XCConfigurationList; buildConfigurations = (" + string.Join(",", Enumerable.Range(40, 4).Select(Id)) + "); defaultConfigurationIsVisible = 0; defaultConfigurationName = Release;");
            Object(11, "isa = PBXResourcesBuildPhase; buildActionMask = 2147483647; files = (); runOnlyForDeploymentPostprocessing = 0;");
            Object(12, "isa = PBXFileReference; explicitFileType = wrapper.framework; path = UnityFramework.framework; sourceTree = BUILT_PRODUCTS_DIR;");
            for (int i = 0; i < names.Length; i++)
            {
                Object(20 + i, "isa = XCBuildConfiguration; buildSettings = {}; name = " + names[i] + ";");
                Object(30 + i, "isa = XCBuildConfiguration; buildSettings = { PRODUCT_NAME = \"$(PRODUCT_NAME_APP)\"; PRODUCT_NAME_APP = Original; PRODUCT_BUNDLE_IDENTIFIER = " + identifier + "; }; name = " + names[i] + ";");
                Object(40 + i, "isa = XCBuildConfiguration; buildSettings = { FRAMEWORK_SENTINEL = unchanged; }; name = " + names[i] + ";");
            }
            return text.AppendLine("}; rootObject = " + Id(1) + "; }").ToString();
        }
        private static string ObjectBody(string text, string guid)
        {
            var match = Regex.Match(text, @"\b" + guid + @"(?:\s*/\*[^*]*\*/)?\s*=\s*\{");
            Assert.That(match.Success, Is.True);
            int start = match.Index + match.Length, position = start, depth = 1;
            while (position < text.Length && depth != 0)
            {
                char c = text[position++];
                if (c == '{') depth++;
                if (c == '}') depth--;
            }
            return text.Substring(start, position - start - 1);
        }
        private static void WritePlist(string folder, string identifier, string build) =>
            new XDocument(new XElement("plist", new XElement("dict",
                Pair("CFBundleDisplayName", "합이오_튜토리얼"), Pair("CFBundleIdentifier", identifier),
                Pair("CFBundleShortVersionString", "0.1.0"), Pair("CFBundleVersion", build),
                Pair("CFBundleExecutable", "$(EXECUTABLE_NAME)")))).Save(Path.Combine(folder, "Info.plist"));
        private static object[] Pair(string key, string value) => new object[] { new XElement("key", key), new XElement("string", value) };
    }
}
