using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using UnityEngine.iOS;

namespace C6Lab.Editor
{
    /// <summary>Both Hapio and Lab export through BuildPipeline; reject a drifted device policy explicitly.</summary>
    public sealed class LabMobileBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.iOS) ValidatePlayerSettings();
        }

        public static void ValidatePlayerSettings()
        {
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait
                || !PlayerSettings.allowedAutorotateToPortrait
                || PlayerSettings.allowedAutorotateToPortraitUpsideDown
                || PlayerSettings.allowedAutorotateToLandscapeLeft
                || PlayerSettings.allowedAutorotateToLandscapeRight
                || !PlayerSettings.iOS.requiresFullScreen)
                throw new BuildFailedException("C6_MOBILE_PORTRAIT_REQUIRED: use Portrait only, disable upside-down/landscape, and keep iOS Requires Full Screen enabled.");
            // The runtime enables only three edges during interactive combat, never menus or launch.
            if (PlayerSettings.iOS.deferSystemGesturesMode != SystemGestureDeferMode.None
                || PlayerSettings.iOS.hideHomeButton)
                throw new BuildFailedException("C6_MOBILE_GESTURE_POLICY_REQUIRED: keep build-time Defer System Gestures=None and Hide Home Button=off; the battle policy manages deferral at runtime.");
        }

        [PostProcessBuild(200)]
        public static void ValidateExportedOrientation(BuildTarget target, string output)
        {
            if (target != BuildTarget.iOS) return;
            var document = new PlistDocument();
            document.ReadFromFile(Path.Combine(output, "Info.plist"));
            ValidateExportedPlist(document);
            Debug.Log("C6_MOBILE_IOS_PORTRAIT_EXPORT_OK");
        }

        /// <summary>Validation only: never silently rewrite exported orientation or permission data.</summary>
        public static void ValidateExportedPlist(PlistDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!document.root.values.TryGetValue("UISupportedInterfaceOrientations", out var orientations)
                || !PortraitOnly(orientations))
                throw new BuildFailedException("C6_MOBILE_EXPORT_ORIENTATION_INVALID: the iPhone export must support only UIInterfaceOrientationPortrait.");
            if (document.root.values.TryGetValue("UISupportedInterfaceOrientations~ipad", out var ipad)
                && !PortraitOnly(ipad))
                throw new BuildFailedException("C6_MOBILE_EXPORT_ORIENTATION_INVALID: the iPad export must support only UIInterfaceOrientationPortrait.");
            if (!document.root.values.TryGetValue("UIRequiresFullScreen", out var fullscreen)
                || !(fullscreen is PlistElementBoolean boolean) || !boolean.value)
                throw new BuildFailedException("C6_MOBILE_EXPORT_FULLSCREEN_REQUIRED: keep UIRequiresFullScreen enabled for the portrait game.");
        }

        private static bool PortraitOnly(PlistElement element)
        {
            return element is PlistElementArray array && array.values.Count == 1
                && array.values[0] is PlistElementString text && text.value == "UIInterfaceOrientationPortrait";
        }
    }
}
