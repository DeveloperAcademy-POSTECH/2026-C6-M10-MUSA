using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    // Installed-source-specific T12/T13 export adaptation. It neither edits the Unity installation
    // nor changes PlayerSettings background modes, transport timeouts, C# pause policy or clocks.
    public sealed class T12ForegroundLifecyclePostprocess : IPreprocessBuildWithReport, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public const string SupportedUnityVersion = "6000.5.7f1";
        public const string IntegratedScene = "Assets/_Project/HapioMVP/Scenes/IntegratedDeviceBattle.unity";
        public const string InterruptionScene = "Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity";
        public const string PhysicsScene = "Assets/_Project/HapioMVP/Scenes/PhysicsBattle.unity";
        public const string Marker = "C6_T12_FOREGROUND_LIFECYCLE_V1";
        private static readonly Dictionary<string, HashSet<string>> BuildScenes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        public int callbackOrder => 950;
        // The requested target/output is stable from preprocess through scene processing and
        // postprocess; a report's generated player GUID may be finalized later in the build.
        private static string BuildKey(BuildReport report) => report.summary.platform + "\n" + report.summary.outputPath;
        public void OnPreprocessBuild(BuildReport report)
        { if (report != null) BuildScenes[BuildKey(report)] = new HashSet<string>(StringComparer.Ordinal); }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            string key = BuildKey(report);
            if (!BuildScenes.TryGetValue(key, out var scenes)) BuildScenes[key] = scenes = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(scene.path)) scenes.Add(scene.path);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null) return;
            string key = BuildKey(report);
            BuildScenes.TryGetValue(key, out var scenes); BuildScenes.Remove(key);
            if (report.summary.platform != BuildTarget.iOS) return;
            if (!Applies(report.summary.platform, scenes?.ToArray()))
            {
                if (EditorBuildSettings.scenes.Any(s => s.enabled && IsSupportedScene(s.path))
                    && (scenes == null || scenes.Count == 0))
                    throw new BuildFailedException("T12 lifecycle patch cannot prove which scenes this export contains.");
                return;
            }
            string output = report.summary.outputPath;
            string controllerPath = Path.Combine(output, "Classes", "UnityAppController.mm");
            string renderingPath = Path.Combine(output, "Classes", "UnityAppController+Rendering.mm");
            string beforeController = File.ReadAllText(controllerPath), beforeRendering = File.ReadAllText(renderingPath);
            var patch = PatchSources(Application.unityVersion, beforeController, beforeRendering);
            if (patch.alreadyApplied) { Debug.Log("C6_T12_IOS_LIFECYCLE_PATCH_ALREADY_APPLIED output=" + output); return; }
            string evidence = Path.Combine(output, "C6T12LifecyclePatch");
            if (Directory.Exists(evidence)) throw new BuildFailedException("T12 lifecycle evidence directory already exists; preserve it and export to a new path.");
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "original-UnityAppController.mm"), beforeController, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidence, "original-UnityAppController+Rendering.mm"), beforeRendering, new UTF8Encoding(false));
            ReplaceFile(controllerPath, patch.controller); ReplaceFile(renderingPath, patch.rendering);
            if (Hash(File.ReadAllText(controllerPath)) != Hash(patch.controller) || Hash(File.ReadAllText(renderingPath)) != Hash(patch.rendering))
                throw new BuildFailedException("T12 lifecycle source write verification failed.");
            File.WriteAllText(Path.Combine(evidence, "patch-receipt.json"), JsonUtility.ToJson(new PatchReceipt
            {
                unityVersion = Application.unityVersion, scene = scenes.Single(), appliedUtc = DateTime.UtcNow.ToString("O"),
                controllerBefore = Hash(beforeController), controllerAfter = Hash(patch.controller), renderingBefore = Hash(beforeRendering), renderingAfter = Hash(patch.rendering)
            }, true), new UTF8Encoding(false));
            Debug.Log("C6_T12_IOS_LIFECYCLE_PATCH_APPLIED unity=" + Application.unityVersion + " foregroundInactive=continue actualBackground=existingPause deviceValidation=NOT_RUN output=" + output);
        }
        public static bool Applies(BuildTarget target, string[] actualScenes) => target == BuildTarget.iOS && actualScenes != null
            && actualScenes.Length == 1 && IsSupportedScene(actualScenes[0]);
        private static bool IsSupportedScene(string scene) => scene == IntegratedScene || scene == InterruptionScene || scene == PhysicsScene || scene == ThrowBattleBuild.ScenePath || scene == FivePlayerBattleBuild.ScenePath || scene == ContinuousTransferBuild.ScenePath;
        public static PatchedSources PatchSources(string unityVersion, string controller, string rendering)
        {
            if (unityVersion != SupportedUnityVersion) throw new InvalidOperationException("Unsupported Unity source version; do not patch an unknown installation.");
            if (controller == null || rendering == null) throw new ArgumentNullException("Native source is missing.");
            bool already = controller.Contains(Marker) || rendering.Contains(Marker);
            string originalController = controller, originalRendering = rendering;
            if (already)
            {
                if (!controller.Contains(Marker) || !rendering.Contains(Marker)) throw new InvalidOperationException("Partially applied T12 lifecycle patch.");
                originalController = ApplyChanges(controller, ControllerChanges(), true);
                originalRendering = ApplyChanges(rendering, RenderingChanges(), true);
            }
            // Full file hashes pin the exact installed export as well as the individually unique
            // replacement blocks. Even an unrelated custom change is preserved by refusing to patch.
            if (Hash(originalController) != ControllerSourceHash || Hash(originalRendering) != RenderingSourceHash)
                throw new InvalidOperationException("Unity 6000.5.7f1 native source does not match the reviewed fixture; export was preserved.");
            if (already) return new PatchedSources { controller = controller, rendering = rendering, alreadyApplied = true };
            return new PatchedSources { controller = ApplyChanges(controller, ControllerChanges(), false), rendering = ApplyChanges(rendering, RenderingChanges(), false) };
        }
        private static string ApplyChanges(string source, Change[] changes, bool reverse)
        {
            foreach (var change in changes)
            {
                string old = reverse ? change.after : change.before, replacement = reverse ? change.before : change.after;
                int at = source.IndexOf(old, StringComparison.Ordinal);
                if (at < 0 || source.IndexOf(old, at + old.Length, StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("Expected exactly one reviewed native block: " + change.name);
                source = source.Substring(0, at) + replacement + source.Substring(at + old.Length);
            }
            return source;
        }
        private static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false).GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        private static void ReplaceFile(string path, string content)
        { string temporary = path + ".c6-t12.tmp"; File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Replace(temporary, path, null); }
        public sealed class PatchedSources { public string controller, rendering; public bool alreadyApplied; }
        private sealed class Change { public string name, before, after; public Change(string n, string b, string a) { name = n; before = b; after = a; } }
        [Serializable] private sealed class PatchReceipt
        { public string status = "APPLIED_SOURCE_ONLY", deviceValidation = "NOT_RUN", backgroundPolicy = "Existing actual-background Pause/Leave preserved; no background entitlement added";
          public string unityVersion, scene, appliedUtc, controllerBefore, controllerAfter, renderingBefore, renderingAfter; }
        private const string ControllerSourceHash = "7de85b998e5c34d770b1e38f5b52364874c6f1366c48bfa7b223c9ba18015272";
        private const string RenderingSourceHash = "f4e7da7b5db6bfa3b8cec3bce7fc41ee617fa0d5dcadf64c3042bfb55c524cc7";
        private static Change[] ControllerChanges() => new[]
        {
            new Change("Controller-0", @"@implementation UnityAppController
", @"// C6_T12_FOREGROUND_LIFECYCLE_V1: this export contains only IntegratedDeviceBattle.
// The current Unity scene callback is authoritative even before UIApplication commits its state.
static bool c6T12ActualBackground = false;
extern ""C"" bool C6T12IsActualBackground(void)
{
    return c6T12ActualBackground || UIApplication.sharedApplication.applicationState == UIApplicationStateBackground;
}

@implementation UnityAppController
"),
            new Change("Controller-1", @"- (void)applicationWillResignActive:(UIApplication*)application
{
    ::printf(""-> applicationWillResignActive()\n"");

    if (self.engineLoadState >= kUnityEngineLoadStateAppReady)
    {
        // This should be covered by applicationDidEnterBackground but double-check just in case we missed it
         if (UnityIsFocused())
            UnitySetPlayerFocus(false);

        // signal unity that the frame rendering have ended
        // as we will not get the callback from the display link current frame
        UnityDisplayLinkCallback(0);

        _unityExplicitlyPaused = UnityIsPaused();
        // Pause/unpause is handled by repaint if CompositorLayer is in use
        if (self.usingCompositorLayer == NO && _unityExplicitlyPaused == NO)
        {
            // Pause Unity only if we don't need special background processing
            // otherwise batched player loop can be called to run user scripts.
            if (!UnityGetUseCustomAppBackgroundBehavior())
            {
                uint32_t pauseFlags = kPauseFlagSetEngineRunState|kPauseFlagSendPauseMessage;
#if UNITY_SNAPSHOT_VIEW_ON_APPLICATION_PAUSE
                // we cannot repaint without drawable given to us by CAMetalDisplayLink
                // TODO: there should be a way to handle this somehow
                if(!self.unityUsesMetalDisplayLink)
                {
                    // Force player to do one more frame, so scripts get a chance to render custom screen for minimized app in task manager.
                    // NB: UnityWillPause will schedule OnApplicationPause message, which will be sent normally inside repaint (unity player loop)
                    // NB: We will actually pause after the loop (when calling UnitySetPlayerPause).
                    UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSchedulePauseMessage);
                    pauseFlags = kPauseFlagSetEngineRunState;
                    [self repaint];
                    [self addSnapshotViewController];
                }
#endif

#if PLATFORM_VISIONOS
                if (!UnityShouldRunInBackground())
                    UnitySetPlayerPause(kUnityPauseModePause, pauseFlags);
#else
                UnitySetPlayerPause(kUnityPauseModePause, pauseFlags);
#endif                
            }
        }
    }

    _didResignActive = true;
}
", @"- (void)applicationWillResignActive:(UIApplication*)application
{
    ::printf(""-> applicationWillResignActive()\n"");

    if (self.engineLoadState >= kUnityEngineLoadStateAppReady)
    {
        // This should be covered by applicationDidEnterBackground but double-check just in case we missed it
         if (UnityIsFocused())
            UnitySetPlayerFocus(false);

        // signal unity that the frame rendering have ended
        // as we will not get the callback from the display link current frame
        UnityDisplayLinkCallback(0);

        _unityExplicitlyPaused = UnityIsPaused();
        // Pause/unpause is handled by repaint if CompositorLayer is in use
        if (self.usingCompositorLayer == NO && _unityExplicitlyPaused == NO)
        {
            // Pause Unity only if we don't need special background processing
            // otherwise batched player loop can be called to run user scripts.
            if (!UnityGetUseCustomAppBackgroundBehavior() && C6T12IsActualBackground())
            {
                uint32_t pauseFlags = kPauseFlagSetEngineRunState|kPauseFlagSendPauseMessage;
#if UNITY_SNAPSHOT_VIEW_ON_APPLICATION_PAUSE
                // we cannot repaint without drawable given to us by CAMetalDisplayLink
                // TODO: there should be a way to handle this somehow
                if(!self.unityUsesMetalDisplayLink)
                {
                    // Force player to do one more frame, so scripts get a chance to render custom screen for minimized app in task manager.
                    // NB: UnityWillPause will schedule OnApplicationPause message, which will be sent normally inside repaint (unity player loop)
                    // NB: We will actually pause after the loop (when calling UnitySetPlayerPause).
                    UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSchedulePauseMessage);
                    pauseFlags = kPauseFlagSetEngineRunState;
                    [self repaint];
                    [self addSnapshotViewController];
                }
#endif

#if PLATFORM_VISIONOS
                if (!UnityShouldRunInBackground())
                    UnitySetPlayerPause(kUnityPauseModePause, pauseFlags);
#else
                UnitySetPlayerPause(kUnityPauseModePause, pauseFlags);
#endif                
            }
        }
    }

    _didResignActive = true;
    ::printf(""C6_T12_IOS_LIFECYCLE event=inactive appState=%ld unityPaused=%d explicitPause=%d\n"",
        (long)application.applicationState, (int)UnityIsPaused(), (int)_unityExplicitlyPaused);
}
"),
            new Change("Controller-2", @"- (void)applicationDidEnterBackground:(UIApplication*)application
{
    ::printf(""-> applicationDidEnterBackground()\n"");

    [self pauseDisplayLink];
    UnityCancelTouches();

#if PLATFORM_VISIONOS
    if (UnityIsFocused())
        UnitySetPlayerFocus(true);

    if (!UnityShouldRunInBackground() && !UnityIsPaused())
        UnitySetPlayerPause(kUnityPauseModePause);
#endif
}
", @"- (void)applicationDidEnterBackground:(UIApplication*)application
{
    ::printf(""-> applicationDidEnterBackground()\n"");
    c6T12ActualBackground = true;

    [self pauseDisplayLink];
    UnityCancelTouches();

    // A foreground system overlay does not pause this T12 player. Actual background still does.
    // Match Unity's scheduled-pause ordering, replacing its final repaint with the documented
    // non-rendering batch loop so OnApplicationPause(true) can close the room without GPU work.
    if (self.engineLoadState >= kUnityEngineLoadStateAppReady
        && !UnityGetUseCustomAppBackgroundBehavior() && !UnityIsPaused())
    {
        UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSchedulePauseMessage);
        UnityBatchPlayerLoop();
        UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSetEngineRunState);
    }
    ::printf(""C6_T12_IOS_LIFECYCLE event=background appState=%ld unityPaused=%d explicitPause=%d\n"",
        (long)application.applicationState, (int)UnityIsPaused(), (int)_unityExplicitlyPaused);

#if PLATFORM_VISIONOS
    if (UnityIsFocused())
        UnitySetPlayerFocus(true);

    if (!UnityShouldRunInBackground() && !UnityIsPaused())
        UnitySetPlayerPause(kUnityPauseModePause);
#endif
}
"),
            new Change("Controller-3", @"- (void)applicationDidBecomeActive:(UIApplication*)application
{
    ::printf(""-> applicationDidBecomeActive()\n"");

    [self removeSnapshotViewController];

    if (self.engineLoadState >= kUnityEngineLoadStateAppReady)
    {
        // Pause/unpause is handled by repaint if CompositorLayer is in use
        if (self.usingCompositorLayer == NO && UnityIsPaused() && _unityExplicitlyPaused == NO)
            UnitySetPlayerPause(kUnityPauseModeResume, kPauseFlagSetEngineRunState|kPauseFlagSchedulePauseMessage);
        if (_unityExplicitlyPaused)
        {
            if (UnityIsFullScreenPlaying())
                TryResumeFullScreenVideo();
        }
        // need to do this with delay because FMOD restarts audio in AVAudioSessionInterruptionNotification handler
        [self performSelector: @selector(updateUnityAudioOutput) withObject: nil afterDelay: 0.1];

        // In case we got to applicationWillEnterForeground before Unity was initialized (or any other edge case)
        if (!UnityIsFocused())
            UnitySetPlayerFocus(true);
    }
    else
    {
        UIWindowScene *scene = [self pickStartupWindowScene:application.connectedScenes];
        [self initUnityWithScene: scene];
    }

    _didResignActive = false;
}
", @"- (void)applicationDidBecomeActive:(UIApplication*)application
{
    ::printf(""-> applicationDidBecomeActive()\n"");

    [self removeSnapshotViewController];

    if (self.engineLoadState >= kUnityEngineLoadStateAppReady)
    {
        // Pause/unpause is handled by repaint if CompositorLayer is in use
        if (self.usingCompositorLayer == NO && UnityIsPaused() && _unityExplicitlyPaused == NO)
            UnitySetPlayerPause(kUnityPauseModeResume, kPauseFlagSetEngineRunState|kPauseFlagSchedulePauseMessage);
        if (_unityExplicitlyPaused)
        {
            if (UnityIsFullScreenPlaying())
                TryResumeFullScreenVideo();
        }
        // need to do this with delay because FMOD restarts audio in AVAudioSessionInterruptionNotification handler
        [self performSelector: @selector(updateUnityAudioOutput) withObject: nil afterDelay: 0.1];

        // In case we got to applicationWillEnterForeground before Unity was initialized (or any other edge case)
        if (!UnityIsFocused())
            UnitySetPlayerFocus(true);
    }
    else
    {
        UIWindowScene *scene = [self pickStartupWindowScene:application.connectedScenes];
        [self initUnityWithScene: scene];
    }

    _didResignActive = false;
    ::printf(""C6_T12_IOS_LIFECYCLE event=active appState=%ld unityPaused=%d explicitPause=%d\n"",
        (long)application.applicationState, (int)UnityIsPaused(), (int)_unityExplicitlyPaused);
}
"),
            new Change("Controller-4", @"- (void)applicationWillEnterForeground:(UIApplication*)application
{
    ::printf(""-> applicationWillEnterForeground()\n"");

    [self unpauseDisplayLink];

    // applicationWillEnterForeground: might sometimes arrive *before* actually initing unity (e.g. locking on startup)
    if (self.engineLoadState >= kUnityEngineLoadStateAppReady)
    {
#if PLATFORM_VISIONOS
        if (!UnityIsFocused())
            UnitySetPlayerFocus(true);

        if (UnityIsPaused() && _unityExplicitlyPaused == false)
            UnitySetPlayerPause(kUnityPauseModeResume);
#endif

        // if we were showing video before going to background - the view size may be changed while we are in background
        [GetAppController().unityView recreateRenderingSurfaceIfNeeded];
    }

}
", @"- (void)applicationWillEnterForeground:(UIApplication*)application
{
    ::printf(""-> applicationWillEnterForeground()\n"");
    c6T12ActualBackground = false;

    [self unpauseDisplayLink];

    // applicationWillEnterForeground: might sometimes arrive *before* actually initing unity (e.g. locking on startup)
    if (self.engineLoadState >= kUnityEngineLoadStateAppReady)
    {
#if PLATFORM_VISIONOS
        if (!UnityIsFocused())
            UnitySetPlayerFocus(true);

        if (UnityIsPaused() && _unityExplicitlyPaused == false)
            UnitySetPlayerPause(kUnityPauseModeResume);
#endif

        // if we were showing video before going to background - the view size may be changed while we are in background
        [GetAppController().unityView recreateRenderingSurfaceIfNeeded];
    }

}
"),
        };
        private static Change[] RenderingChanges() => new[]
        {
            new Change("Rendering-0", @"static int _renderingAPI = 0;
", @"// C6_T12_FOREGROUND_LIFECYCLE_V1: never service queued rendering callbacks in actual background.
extern ""C"" bool C6T12IsActualBackground(void);
static int _renderingAPI = 0;
"),
            new Change("Rendering-1", @"- (void)repaintDisplayLink
{
    if (self.usingCompositorLayer == NO)
    {
        UnityDisplayLinkCallback(_displayLink.timestamp);
        [self repaint];
    }
    else
    {
        [self repaintCompositorLayer];
    }
}
", @"- (void)repaintDisplayLink
{
    if (C6T12IsActualBackground()) return;
    if (self.usingCompositorLayer == NO)
    {
        UnityDisplayLinkCallback(_displayLink.timestamp);
        [self repaint];
    }
    else
    {
        [self repaintCompositorLayer];
    }
}
"),
            new Change("Rendering-2", @"- (void)metalDisplayLink:(CAMetalDisplayLink*)link needsUpdate:(CAMetalDisplayLinkUpdate*)update
{
    UnityDisplayLinkCallback(0);

    UnityDisplaySurfaceMTL* displaySurface = (UnityDisplaySurfaceMTL*)_mainDisplay.surface;
    displaySurface->swapchain.drawable = update.drawable;
    [self repaint];
}
", @"- (void)metalDisplayLink:(CAMetalDisplayLink*)link needsUpdate:(CAMetalDisplayLinkUpdate*)update
{
    if (C6T12IsActualBackground()) return;
    UnityDisplayLinkCallback(0);

    UnityDisplaySurfaceMTL* displaySurface = (UnityDisplaySurfaceMTL*)_mainDisplay.surface;
    displaySurface->swapchain.drawable = update.drawable;
    [self repaint];
}
"),
            new Change("Rendering-3", @"- (void)repaint
{
    // floating/docking keyboard on iPad has otherwise uncatchable edge cases
    [KeyboardDelegate.Instance updateInputPosition];
    if (_unityView.skipRendering)
        return;

#if UNITY_SUPPORT_ROTATION
    [self checkOrientationRequest];
#endif

    [_unityView recreateRenderingSurfaceIfNeeded];
    [_unityView processKeyboard];

    // we want to support both CADisplayLink and CAMetalDisplayLink
    // the major complication is that they work quite differently under the hood
    // CADisplayLink: you can consider this a simple timer-based callback
    //   so if we get this while in background - we might be not allowed to render at all
    //   and before we were having an explicit check to repain only if we are not paused
    // CAMetalDisplayLink: unlike CADisplayLink (where we query drawable from view),
    //   the callback comes when we are asked explicitly to render view contents (we are given drawable)
    //   and we cannot bypass rendering when asked at all

    if (UnityIsPaused())
    {
        if(self.unityUsesMetalDisplayLink)
            UnityRenderWithoutPlayerLoopWithBackbuffer(GetMainDisplaySurface()->unityColorBuffer, GetMainDisplaySurface()->unityDepthBuffer);
    }
    else if (UnityIsBatchmode())
    {
        UnityBatchPlayerLoop();
    }
    else
    {
        UnityPlayerLoopWithBackbuffer(GetMainDisplaySurface()->unityColorBuffer, GetMainDisplaySurface()->unityDepthBuffer);

        id<MTLCommandBuffer> cb = [UnityGetMetalCommandQueue() commandBuffer];
        cb.label = @""Present"";
        [[DisplayManager Instance] presentWith:cb];
        [cb commit];
    }

#if !PLATFORM_VISIONOS
    if (UnityResolutionScalingFixedDPIFactorChanged())
        _unityView.contentScaleFactor = UnityScreenScaleFactor([UIScreen mainScreen]);
#endif

    UnityCheckUnloadAndQuit();
}
", @"- (void)repaint
{
    if (C6T12IsActualBackground()) return;
    // floating/docking keyboard on iPad has otherwise uncatchable edge cases
    [KeyboardDelegate.Instance updateInputPosition];
    if (_unityView.skipRendering)
        return;

#if UNITY_SUPPORT_ROTATION
    [self checkOrientationRequest];
#endif

    [_unityView recreateRenderingSurfaceIfNeeded];
    [_unityView processKeyboard];

    // we want to support both CADisplayLink and CAMetalDisplayLink
    // the major complication is that they work quite differently under the hood
    // CADisplayLink: you can consider this a simple timer-based callback
    //   so if we get this while in background - we might be not allowed to render at all
    //   and before we were having an explicit check to repain only if we are not paused
    // CAMetalDisplayLink: unlike CADisplayLink (where we query drawable from view),
    //   the callback comes when we are asked explicitly to render view contents (we are given drawable)
    //   and we cannot bypass rendering when asked at all

    if (UnityIsPaused())
    {
        if(self.unityUsesMetalDisplayLink)
            UnityRenderWithoutPlayerLoopWithBackbuffer(GetMainDisplaySurface()->unityColorBuffer, GetMainDisplaySurface()->unityDepthBuffer);
    }
    else if (UnityIsBatchmode())
    {
        UnityBatchPlayerLoop();
    }
    else
    {
        UnityPlayerLoopWithBackbuffer(GetMainDisplaySurface()->unityColorBuffer, GetMainDisplaySurface()->unityDepthBuffer);

        id<MTLCommandBuffer> cb = [UnityGetMetalCommandQueue() commandBuffer];
        cb.label = @""Present"";
        [[DisplayManager Instance] presentWith:cb];
        [cb commit];
    }

#if !PLATFORM_VISIONOS
    if (UnityResolutionScalingFixedDPIFactorChanged())
        _unityView.contentScaleFactor = UnityScreenScaleFactor([UIScreen mainScreen]);
#endif

    UnityCheckUnloadAndQuit();
}
"),
        };
    }
}
