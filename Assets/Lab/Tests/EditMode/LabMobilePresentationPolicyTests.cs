using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.iOS;

namespace C6Lab.Tests
{
    /// <summary>Policy/API/serialized settings checks are not a native device gesture or rotation test.</summary>
    public sealed class LabMobilePresentationPolicyTests
    {
        [Test]
        public void InteractiveCombatDefersOnlyLeftRightAndBottom()
        {
            var mode = LabMobilePresentationPolicy.SelectGestureMode(LabPhase.Playing,
                true, false, false, true, true);
            Assert.That(mode, Is.EqualTo(SystemGestureDeferMode.LeftEdge | SystemGestureDeferMode.RightEdge
                | SystemGestureDeferMode.BottomEdge));
            Assert.That(mode & SystemGestureDeferMode.TopEdge, Is.EqualTo(SystemGestureDeferMode.None));
        }

        [TestCase(LabPhase.Idle, true, false, false, true, true)]
        [TestCase(LabPhase.Victory, true, false, false, true, true)]
        [TestCase(LabPhase.Defeat, true, false, false, true, true)]
        [TestCase(LabPhase.Playing, false, false, false, true, true)]
        [TestCase(LabPhase.Playing, true, true, false, true, true)]
        [TestCase(LabPhase.Playing, true, false, true, true, true)]
        [TestCase(LabPhase.Playing, true, false, false, false, true)]
        [TestCase(LabPhase.Playing, true, false, false, true, false)]
        public void NonInteractiveStatesLeaveSystemGesturesAvailable(LabPhase phase, bool boardInteractive,
            bool paused, bool startHeld, bool foreground, bool connected)
        {
            Assert.That(LabMobilePresentationPolicy.SelectGestureMode(phase, boardInteractive,
                paused, startHeld, foreground, connected), Is.EqualTo(SystemGestureDeferMode.None));
        }

        [Test]
        public void AttachmentIsIdempotentAndDisablingLeavesNoDeferredEdges()
        {
            var root = new GameObject("mobile-policy-fixture");
            try
            {
                var network = root.AddComponent<LabNetwork>();
                var first = LabMobilePresentationPolicy.Attach(network);
                Assert.That(LabMobilePresentationPolicy.Attach(network), Is.SameAs(first));
                Assert.That(root.GetComponents<LabMobilePresentationPolicy>(), Has.Length.EqualTo(1));
                first.Refresh();
                Assert.That(first.DesiredGestureMode, Is.EqualTo(SystemGestureDeferMode.None));
                first.enabled = false;
                Assert.That(first.DesiredGestureMode, Is.EqualTo(SystemGestureDeferMode.None));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedIosSettingsAllowOnlyNormalPortraitAndRuntimeOwnedDeferral()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.False);
            Assert.That(PlayerSettings.iOS.requiresFullScreen, Is.True);
            Assert.That(PlayerSettings.iOS.hideHomeButton, Is.False);
            Assert.That(PlayerSettings.iOS.deferSystemGesturesMode, Is.EqualTo(SystemGestureDeferMode.None));
        }

        [TestCase(null)]
        [TestCase("UIInterfaceOrientationPortrait")]
        public void ExportAllowsPortraitOnIphoneAndInheritedOrExplicitIpad(string ipadOrientation)
        {
            InvokeExportCheck("UIInterfaceOrientationPortrait", ipadOrientation, true);
        }

        [TestCase("UIInterfaceOrientationLandscapeLeft", null, true)]
        [TestCase("UIInterfaceOrientationPortraitUpsideDown", null, true)]
        [TestCase("UIInterfaceOrientationPortrait", "UIInterfaceOrientationLandscapeRight", true)]
        [TestCase("UIInterfaceOrientationPortrait", null, false)]
        public void ExportRefusesLandscapeOrMissingFullscreen(string orientation, string ipadOrientation, bool fullscreen)
        {
            var exception = Assert.Throws<TargetInvocationException>(() =>
                InvokeExportCheck(orientation, ipadOrientation, fullscreen));
            Assert.That(exception.InnerException, Is.TypeOf<UnityEditor.Build.BuildFailedException>());
        }

        [Test]
        public void ExportRefusesMissingOrientationOrFullscreenKeys()
        {
            Assert.That(Assert.Throws<TargetInvocationException>(() =>
                InvokeExportCheck(null, null, true)).InnerException,
                Is.TypeOf<UnityEditor.Build.BuildFailedException>());
            Assert.That(Assert.Throws<TargetInvocationException>(() =>
                InvokeExportCheck("UIInterfaceOrientationPortrait", null, null)).InnerException,
                Is.TypeOf<UnityEditor.Build.BuildFailedException>());
        }

        private static void InvokeExportCheck(string orientation, string ipadOrientation, bool? fullscreen)
        {
            Type validator = Type.GetType("C6Lab.Editor.LabMobileBuildValidation, Assembly-CSharp-Editor", true);
            MethodInfo validate = validator.GetMethod("ValidateExportedPlist");
            Type documentType = validate.GetParameters()[0].ParameterType;
            object document = Activator.CreateInstance(documentType);
            string ipad = ipadOrientation == null ? string.Empty
                : "<key>UISupportedInterfaceOrientations~ipad</key><array><string>" + ipadOrientation + "</string></array>";
            string phone = orientation == null ? string.Empty
                : "<key>UISupportedInterfaceOrientations</key><array><string>" + orientation + "</string></array>";
            string full = fullscreen.HasValue ? "<key>UIRequiresFullScreen</key>"
                + (fullscreen.Value ? "<true/>" : "<false/>") : string.Empty;
            string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>"
                + phone + ipad + full + "</dict></plist>";
            documentType.GetMethod("ReadFromString").Invoke(document, new object[] { xml });
            validate.Invoke(null, new[] { document });
        }

        [Test]
        public void BothBuildPathsShareTheIosPreflightAndExportCallbacks()
        {
            Type validator = Type.GetType("C6Lab.Editor.LabMobileBuildValidation, Assembly-CSharp-Editor", true);
            Assert.That(typeof(UnityEditor.Build.IPreprocessBuildWithReport).IsAssignableFrom(validator), Is.True);
            MethodInfo export = validator.GetMethod("ValidateExportedOrientation");
            Assert.That(export, Is.Not.Null);
            Assert.That(export.GetCustomAttribute<UnityEditor.Callbacks.PostProcessBuildAttribute>(), Is.Not.Null);
        }
    }
}
