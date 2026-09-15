using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace C6.Prototype.GameSync.Tests
{
    // Source transformation checks only. Native compilation and actual iOS overlays are separate gates.
    public sealed class T12ForegroundLifecyclePatchTests
    {
        private const string FixturePath = "Assets/_Project/HapioMVP/Tests/GameSync/EditMode/Fixtures/T12Unity6000_5_7f1Lifecycle.json";
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/IntegratedDeviceBattle.unity";
        private const string Marker = "C6_T12_FOREGROUND_LIFECYCLE_V1";
        private const string Guard = "if (C6T12IsActualBackground()) return;";
        private Type patchType;
        private Fixture fixture;

        [Serializable]
        private sealed class Fixture { public string unityVersion, controller, rendering; }
        private sealed class Output { public string controller, rendering; public bool alreadyApplied; }

        [SetUp]
        public void LoadReviewedInstalledSourceWithoutChangingProjectSettings()
        {
            patchType = Type.GetType("C6.Editor.T12ForegroundLifecyclePostprocess, Assembly-CSharp-Editor", true);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            Assert.That(asset, Is.Not.Null, "The reviewed Unity export fixture must be imported.");
            fixture = JsonUtility.FromJson<Fixture>(asset.text);
            Assert.That(fixture.unityVersion, Is.EqualTo("6000.5.7f1"));
            Assert.That(fixture.controller, Does.Not.Contain(Marker));
            Assert.That(fixture.rendering, Does.Not.Contain(Marker));
        }

        [Test]
        public void AppliesOnlyToAnIosExportContainingExactlyTheIntegratedScene()
        {
            Assert.That(Applies(BuildTarget.iOS, new[] { ScenePath }), Is.True);
            Assert.That(Applies(BuildTarget.StandaloneOSX, new[] { ScenePath }), Is.False);
            Assert.That(Applies(BuildTarget.iOS, null), Is.False);
            Assert.That(Applies(BuildTarget.iOS, Array.Empty<string>()), Is.False);
            Assert.That(Applies(BuildTarget.iOS, new[] { (string)null }), Is.False);
            foreach (string previous in new[] { "OrbTransferBattle", "TwoPlayerBattle", "BattleLoop" })
            {
                string path = "Assets/_Project/HapioMVP/Scenes/" + previous + ".unity";
                Assert.That(Applies(BuildTarget.iOS, new[] { path }), Is.False, previous);
                Assert.That(Applies(BuildTarget.iOS, new[] { ScenePath, path }), Is.False, "Mixed exports must remain unchanged.");
            }
            Assert.That(Applies(BuildTarget.iOS, new[] { ScenePath, ScenePath }), Is.False);
        }

        [Test]
        [TestCase("InterruptionBattle")]
        [TestCase("PhysicsBattle")]
        public void LaterSceneRetainsTheReviewedPatchWithoutEnablingMixedOrUnrelatedExports(string sceneName)
        {
            string interruption = "Assets/_Project/HapioMVP/Scenes/" + sceneName + ".unity";
            Assert.That(Applies(BuildTarget.iOS, new[] { interruption }), Is.True);
            Assert.That(Applies(BuildTarget.StandaloneOSX, new[] { interruption }), Is.False);
            Assert.That(Applies(BuildTarget.iOS, new[] { interruption, ScenePath }), Is.False);
            Assert.That(Applies(BuildTarget.iOS, new[] { interruption, interruption }), Is.False);
            Assert.That(Applies(BuildTarget.iOS, new[] { interruption + ".backup" }), Is.False);
            Assert.That(Applies(BuildTarget.iOS, new[] { "Assets/Other/InterruptionBattle.unity" }), Is.False);
        }

        [Test]
        public void ForegroundInactivePauseIsConditionedOnActualBackgroundAndFocusIsPreserved()
        {
            var patched = Patch(fixture.unityVersion, fixture.controller, fixture.rendering);
            Assert.That(patched.alreadyApplied, Is.False);
            Assert.That(Occurrences(patched.controller, Marker), Is.EqualTo(1));
            Assert.That(Occurrences(patched.rendering, Marker), Is.EqualTo(1));
            string inactive = Method(patched.controller, "- (void)applicationWillResignActive:");
            Assert.That(inactive, Does.Contain("if (!UnityGetUseCustomAppBackgroundBehavior() && C6T12IsActualBackground())"));
            Assert.That(inactive, Does.Contain("UnitySetPlayerFocus(false);"));
            Assert.That(inactive, Does.Contain("UnityDisplayLinkCallback(0);"));
            Assert.That(inactive, Does.Contain("_unityExplicitlyPaused = UnityIsPaused();"));
            Assert.That(patched.controller, Does.Contain("c6T12ActualBackground || UIApplication.sharedApplication.applicationState == UIApplicationStateBackground"));
            Assert.That(Method(patched.controller, "- (void)applicationWillEnterForeground:"), Does.Contain("c6T12ActualBackground = false;"));
            string active = Method(patched.controller, "- (void)applicationDidBecomeActive:");
            Assert.That(active, Does.Contain("self.usingCompositorLayer == NO && UnityIsPaused() && _unityExplicitlyPaused == NO"));
            Assert.That(active, Does.Contain("UnitySetPlayerPause(kUnityPauseModeResume, kPauseFlagSetEngineRunState|kPauseFlagSchedulePauseMessage);"));
            foreach (string focusCall in new[] { "UnitySetPlayerFocus(false);", "UnitySetPlayerFocus(true);" })
                Assert.That(Occurrences(patched.controller, focusCall), Is.EqualTo(Occurrences(fixture.controller, focusCall)), focusCall);
        }

        [Test]
        public void ActualBackgroundDeliversPauseBeforeStoppingEngineAndGuardsAllRenderingEntrypoints()
        {
            var patched = Patch(fixture.unityVersion, fixture.controller, fixture.rendering);
            string background = Method(patched.controller, "- (void)applicationDidEnterBackground:");
            Before(background, "c6T12ActualBackground = true;", "[self pauseDisplayLink];");
            Before(background, "UnityCancelTouches();", "UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSchedulePauseMessage);");
            Before(background, "UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSchedulePauseMessage);", "UnityBatchPlayerLoop();");
            Before(background, "UnityBatchPlayerLoop();", "UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSetEngineRunState);");
            Assert.That(background, Does.Contain("!UnityGetUseCustomAppBackgroundBehavior() && !UnityIsPaused()"));
            Assert.That(background, Does.Not.Contain("[self repaint]"));
            Assert.That(background, Does.Not.Contain("UnityPlayerLoopWithBackbuffer"));
            string display = Method(patched.rendering, "- (void)repaintDisplayLink");
            string metal = Method(patched.rendering, "- (void)metalDisplayLink:");
            string repaint = Method(patched.rendering, "- (void)repaint\n");
            Before(display, Guard, "UnityDisplayLinkCallback(_displayLink.timestamp);");
            Before(metal, Guard, "UnityDisplayLinkCallback(0);");
            Before(repaint, Guard, "[KeyboardDelegate.Instance updateInputPosition];");
            Assert.That(Occurrences(patched.rendering, Guard), Is.EqualTo(3));
            Assert.That(repaint, Does.Contain("UnityPlayerLoopWithBackbuffer"), "Active rendering and the ordinary player loop must remain available.");
        }

        [Test]
        public void OtherUnityVersionsAndMissingNativeSourcesAreRejected()
        {
            foreach (string version in new[] { null, "", "6000.5.6f1", "6000.5.8f1", "6000.5.7f1-modified" })
                Assert.Throws<InvalidOperationException>(() => Patch(version, fixture.controller, fixture.rendering), version ?? "null version");
            Assert.Throws<ArgumentNullException>(() => Patch(fixture.unityVersion, null, fixture.rendering));
            Assert.Throws<ArgumentNullException>(() => Patch(fixture.unityVersion, fixture.controller, null));
        }

        [Test]
        public void ChangedOrDuplicatedOriginalNativeAnchorsFailWithoutProducingAPatch()
        {
            string controllerAnchor = "- (void)applicationWillResignActive:(UIApplication*)application";
            string renderingAnchor = "- (void)repaintDisplayLink";
            Assert.That(Occurrences(fixture.controller, controllerAnchor), Is.EqualTo(1));
            Assert.That(Occurrences(fixture.rendering, renderingAnchor), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion,
                fixture.controller.Replace(controllerAnchor, controllerAnchor + " /* customized */"), fixture.rendering));
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion, fixture.controller,
                fixture.rendering.Replace(renderingAnchor, renderingAnchor + " /* customized */")));
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion,
                fixture.controller + "\n" + Method(fixture.controller, controllerAnchor), fixture.rendering));
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion, fixture.controller,
                fixture.rendering + "\n" + Method(fixture.rendering, renderingAnchor)));
        }

        [Test]
        public void ReapplyingTheReviewedPatchReturnsExactlyTheExistingSources()
        {
            var first = Patch(fixture.unityVersion, fixture.controller, fixture.rendering);
            var second = Patch(fixture.unityVersion, first.controller, first.rendering);
            Assert.That(second.alreadyApplied, Is.True);
            Assert.That(second.controller, Is.EqualTo(first.controller));
            Assert.That(second.rendering, Is.EqualTo(first.rendering));
        }

        [Test]
        public void PartialOrEditedPreviouslyAppliedPatchesAreRejected()
        {
            var first = Patch(fixture.unityVersion, fixture.controller, fixture.rendering);
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion, first.controller, fixture.rendering));
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion, fixture.controller, first.rendering));
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion,
                first.controller.Replace("UnityBatchPlayerLoop();", "/* scheduled pause delivery removed */"), first.rendering));
            Assert.Throws<InvalidOperationException>(() => Patch(fixture.unityVersion, first.controller,
                first.rendering.Replace(Guard, "/* actual-background rendering guard removed */")));
        }

        private bool Applies(BuildTarget target, string[] scenes) => (bool)Invoke("Applies", target, scenes);
        private Output Patch(string version, string controller, string rendering)
        {
            object result = Invoke("PatchSources", version, controller, rendering);
            Type type = result.GetType();
            return new Output
            {
                controller = (string)type.GetField("controller").GetValue(result),
                rendering = (string)type.GetField("rendering").GetValue(result),
                alreadyApplied = (bool)type.GetField("alreadyApplied").GetValue(result)
            };
        }
        private object Invoke(string method, params object[] arguments)
        {
            var api = patchType.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.That(api, Is.Not.Null, method);
            try { return api.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static string Method(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int end = source.IndexOf("\n}\n", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), signature);
            return source.Substring(start, end + 3 - start);
        }
        private static int Occurrences(string source, string token) => source.Split(new[] { token }, StringSplitOptions.None).Length - 1;
        private static void Before(string source, string first, string second)
        {
            int firstAt = source.IndexOf(first, StringComparison.Ordinal), secondAt = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstAt, Is.GreaterThanOrEqualTo(0), first);
            Assert.That(secondAt, Is.GreaterThan(firstAt), first + " must precede " + second);
        }
    }
}
