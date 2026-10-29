using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace C6Lab.Editor
{
    public enum LabJigwiRewardTrailComparisonView { Front, Side, FrontCloseUp }

    /// <summary>Normal and B-reward production projectiles, at the same radius, velocity and rendered instant.</summary>
    public sealed class LabJigwiRewardTrailComparisonWindow : EditorWindow
    {
        private const string SharedStyle = "Assets/Editor/ElementTrailComparison/LabElementTrailComparison.uss";
        private const string Style = "Assets/Editor/ElementTrailComparison/LabJigwiRewardTrailComparison.uss";
        private static readonly string[] ElementNames = { "FIRE / 화", "WATER / 수", "WOOD / 목", "METAL / 금", "EARTH / 토" };
        [SerializeField] private LabJigwiRewardTrailComparisonView view;
        [SerializeField] private float captureSeconds = .14f;
        [SerializeField] private bool autoCapture = true;
        [SerializeField] private bool repeat = true;
        private readonly Image[,] images = new Image[2, 5];
        private LabElementTrailComparisonSession normal, reward;
        private LabConfig sourceOverrideConfig;
        private Camera sourceOverrideCamera;
        private Label status;
        private Button compareButton, stopButton, captureButton, exportButton;
        private Toggle frozenToggle;
        private double lastRenderAt;
        private bool frozen, capturedThisVolley;
        private float capturedFlightSeconds;

        public bool HasCapturedFrames => frozen && normal != null && normal.IsValid && reward != null && reward.IsValid;
        public float CapturedFlightSeconds => HasCapturedFrames ? capturedFlightSeconds : 0f;

        [MenuItem("C6 Lab/Visuals/Compare B Reward Fire Trails")]
        public static void Open()
        {
            ShowWindow();
        }

        private static LabJigwiRewardTrailComparisonWindow ShowWindow()
        {
            var window = GetWindow<LabJigwiRewardTrailComparisonWindow>();
            window.titleContent = new GUIContent("B Reward Fire Trails");
            window.minSize = new Vector2(1050f, 850f);
            window.Show();
            return window;
        }

        /// <summary>Begin real Play Mode shots. Poll CaptureForValidation; it never advances physics manually.</summary>
        public static LabJigwiRewardTrailComparisonWindow BeginForValidation(
            LabJigwiRewardTrailComparisonView view = LabJigwiRewardTrailComparisonView.Front, float captureSeconds = .14f)
            => BeginForValidation(null, null, view, captureSeconds);

        /// <summary>Explicit live input is useful when the active scene has no LabSeatCamera. Inputs are only copied.</summary>
        public static LabJigwiRewardTrailComparisonWindow BeginForValidation(LabConfig config, Camera camera,
            LabJigwiRewardTrailComparisonView view = LabJigwiRewardTrailComparisonView.Front, float captureSeconds = .14f)
        {
            var window = ShowWindow();
            window.view = view;
            window.captureSeconds = ClampCaptureTime(captureSeconds);
            window.autoCapture = true;
            window.sourceOverrideConfig = config;
            window.sourceOverrideCamera = camera;
            window.StartComparison();
            window.CreateGUI();
            return window;
        }

        private void OnEnable()
        {
            EditorApplication.update += UpdateComparison;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Clear();
            AddStyle(SharedStyle);
            AddStyle(Style);
            rootVisualElement.AddToClassList("trail-comparison");
            rootVisualElement.AddToClassList("reward-comparison");
            AddLabel(rootVisualElement, "Five elements · normal / B-success fire", "trail-title");
            AddLabel(rootVisualElement, "Play Mode only. Top: normal. Bottom: B reward. Both use the same body radius, " +
                "release velocity and 0.20 s lifetime; reward size ×1.3 is intentionally excluded. " +
                "These are isolated client-visual shots, with no room, hit or paint outcome.", "trail-description");

            var controls = NewContainer(rootVisualElement, "trail-controls");
            compareButton = new Button(ReplayFromUi) { text = "Compare / replay" };
            stopButton = new Button(Release) { text = "Stop and clear" };
            var cameraView = new EnumField("Camera", view);
            cameraView.AddToClassList("reward-camera-field");
            cameraView.RegisterValueChangedCallback(change =>
            {
                view = (LabJigwiRewardTrailComparisonView)change.newValue;
                if (normal != null) ReplayFromUi();
            });
            var looping = new Toggle("Repeat") { value = repeat };
            looping.RegisterValueChangedCallback(change => repeat = change.newValue);
            frozenToggle = new Toggle("Freeze preview") { value = frozen };
            frozenToggle.RegisterValueChangedCallback(change =>
            {
                if (change.newValue) CaptureFromUi();
                else { frozen = false; RefreshStatus(); }
            });
            AddControls(controls, compareButton, stopButton, cameraView, looping, frozenToggle);

            var captureControls = NewContainer(rootVisualElement, "trail-controls");
            var automatic = new Toggle("Auto capture") { value = autoCapture };
            automatic.RegisterValueChangedCallback(change => autoCapture = change.newValue);
            var captureAt = new FloatField("At flight seconds") { value = captureSeconds };
            captureAt.AddToClassList("trail-time-field");
            captureAt.RegisterValueChangedCallback(change =>
            {
                captureSeconds = ClampCaptureTime(change.newValue);
                captureAt.SetValueWithoutNotify(captureSeconds);
            });
            captureButton = new Button(CaptureFromUi) { text = "Capture now" };
            exportButton = new Button(ExportFromUi) { text = "Export 5 × 2 PNG" };
            AddControls(captureControls, automatic, captureAt, captureButton, exportButton);

            var rows = NewContainer(rootVisualElement, "reward-rows");
            for (int row = 0; row < 2; row++)
            {
                var rowRoot = NewContainer(rows, "reward-row");
                AddLabel(rowRoot, row == 0 ? "NORMAL · original elemental trail" : "B REWARD · same element + fire envelope / trail / embers",
                    "reward-row-title");
                var panels = NewContainer(rowRoot, "reward-panels");
                for (int element = 0; element < 5; element++)
                {
                    var panel = NewContainer(panels, "trail-panel");
                    AddLabel(panel, ElementNames[element], "trail-panel-title");
                    images[row, element] = new Image { scaleMode = ScaleMode.ScaleToFit,
                        name = (row == 0 ? "normal" : "reward") + "Element" + element };
                    images[row, element].AddToClassList("reward-frame");
                    panel.Add(images[row, element]);
                }
            }
            status = new Label();
            status.AddToClassList("trail-status");
            rootVisualElement.Add(status);
            BindFrames();
            RefreshStatus();
        }

        private void AddStyle(string path)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);
        }

        private static VisualElement NewContainer(VisualElement parent, string className)
        {
            var container = new VisualElement();
            container.AddToClassList(className);
            parent.Add(container);
            return container;
        }

        private static void AddLabel(VisualElement parent, string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            parent.Add(label);
        }

        private static void AddControls(VisualElement parent, params VisualElement[] controls)
        {
            foreach (VisualElement control in controls)
            {
                control.AddToClassList("trail-control");
                parent.Add(control);
            }
        }

        private static float ClampCaptureTime(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? .14f : Mathf.Clamp(value, .02f, .19f);

        private void ReplayFromUi()
        {
            try { StartComparison(); }
            catch (Exception error) { Release(); SetStatus("Comparison could not start: " + error.Message); Debug.LogException(error); }
        }

        private void StartComparison()
        {
            Release();
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.timeScale <= 0f)
                throw new InvalidOperationException("Enter and resume Play Mode first. This tool never changes global time or physics.");
            LabConfig config = sourceOverrideConfig;
            Camera camera = sourceOverrideCamera;
            if (config == null || camera == null)
            {
                LabSeatCamera source = Object.FindFirstObjectByType<LabSeatCamera>();
                if (source != null) { config = source.config; camera = source.battleCamera; }
            }
            if (config == null || camera == null)
                throw new InvalidOperationException("No live config / battle camera. Open Lab or supply both to BeginForValidation(config, camera).");
            try
            {
                bool side = view == LabJigwiRewardTrailComparisonView.Side;
                bool close = view == LabJigwiRewardTrailComparisonView.FrontCloseUp;
                normal = new LabElementTrailComparisonSession(config, camera, side, true, close);
                reward = new LabElementTrailComparisonSession(config, camera, side, true, close, true);
                normal.StartVolley();
                reward.StartVolley();
                BindFrames();
                RenderPair();
                lastRenderAt = EditorApplication.timeSinceStartup;
                RefreshStatus();
            }
            catch { Release(); throw; }
        }

        private void BindFrames()
        {
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 5; i++)
                    if (images[row, i] != null)
                        images[row, i].image = row == 0 ? normal?.Frames[i] : reward?.Frames[i];
        }

        private void RenderPair()
        {
            normal.RenderFramesWithPeer(reward);
            reward.RenderFramesWithPeer(normal);
        }

        private void UpdateComparison()
        {
            if (normal == null) return;
            if (!EditorApplication.isPlaying || !normal.IsValid || reward == null || !reward.IsValid)
            {
                Release();
                return;
            }
            if (frozen || EditorApplication.isPaused || Time.timeScale <= 0f) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - lastRenderAt < 1d / 30d) return;
            lastRenderAt = now;
            try
            {
                if (autoCapture && !capturedThisVolley && CaptureForValidation()) return;
                if (repeat && normal.FlightAge >= Mathf.Max(normal.ReplayAfterSeconds, reward.ReplayAfterSeconds))
                {
                    normal.StartVolley();
                    reward.StartVolley();
                    capturedThisVolley = false;
                }
                RenderPair();
                RefreshStatus();
            }
            catch (Exception error) { Release(); SetStatus("Comparison stopped: " + error.Message); Debug.LogException(error); }
        }

        /// <summary>Returns false until both real shots reach the requested age. A capture holds only the images.</summary>
        public bool CaptureForValidation()
        {
            if (HasCapturedFrames) return true;
            if (normal == null || reward == null || !normal.IsValid || !reward.IsValid) return false;
            if (normal.FlightAge < captureSeconds || reward.FlightAge < captureSeconds) return false;
            // Shader/UI warm-up may skip the short flight on its first frame; wait for a replay, not an empty afterimage.
            if (normal.FlightAge >= normal.FlightDuration || reward.FlightAge >= reward.FlightDuration) return false;
            CaptureNow();
            return true;
        }

        private void CaptureNow()
        {
            if (normal == null || reward == null || !normal.IsValid || !reward.IsValid)
                throw new InvalidOperationException("Start a Play Mode comparison first.");
            // Both renders run in the same Editor callback. Time.time and private-scene poses cannot advance between rows.
            RenderPair();
            capturedFlightSeconds = normal.FlightAge;
            frozen = true;
            capturedThisVolley = true;
            frozenToggle?.SetValueWithoutNotify(true);
            RefreshStatus();
        }

        private void CaptureFromUi()
        {
            try { CaptureNow(); }
            catch (Exception error) { SetStatus("Capture failed: " + error.Message); }
        }

        /// <summary>Actual displayed pixels: Fire→Earth, normal top row and B-reward bottom row. Caller owns the texture.</summary>
        public Texture2D ReadContactSheet()
        {
            if (!HasCapturedFrames) throw new InvalidOperationException("Capture the comparison before reading its contact sheet.");
            int width = LabElementTrailComparisonSession.FrameWidth, height = LabElementTrailComparisonSession.FrameHeight;
            var sheet = new Texture2D(width * 5, height * 2, TextureFormat.RGBA32, false, false)
                { name = "B reward fire comparison (normal top, reward bottom)", hideFlags = HideFlags.HideAndDontSave };
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < 5; i++)
                    {
                        RenderTexture.active = row == 0 ? normal.Frames[i] : reward.Frames[i];
                        sheet.ReadPixels(new Rect(0, 0, width, height), i * width, row == 0 ? height : 0, false);
                    }
                sheet.Apply(false, false);
                return sheet;
            }
            catch { Object.DestroyImmediate(sheet); throw; }
            finally { RenderTexture.active = previous; }
        }

        /// <summary>Exports only an already captured real render. No scene/config is saved.</summary>
        public void ExportForValidation(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A PNG output path is required.", nameof(path));
            Texture2D sheet = null;
            try { sheet = ReadContactSheet(); File.WriteAllBytes(path, sheet.EncodeToPNG()); }
            finally { if (sheet != null) Object.DestroyImmediate(sheet); }
        }

        private void ExportFromUi()
        {
            string path = EditorUtility.SaveFilePanel("Export actual B reward fire comparison", "", "b-reward-fire-trails-" + view, "png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (!HasCapturedFrames) CaptureNow();
                ExportForValidation(path);
                SetStatus("Exported 5 × 2 actual frames: normal top, B reward bottom; Fire / Water / Wood / Metal / Earth. " +
                    "Capture age " + capturedFlightSeconds.ToString("F3") + " s. Visual evidence only.");
            }
            catch (Exception error) { SetStatus("Export failed: " + error.Message); }
        }

        private void RefreshStatus()
        {
            bool running = normal != null && reward != null;
            compareButton?.SetEnabled(EditorApplication.isPlaying && !EditorApplication.isPaused && Time.timeScale > 0f);
            stopButton?.SetEnabled(running);
            captureButton?.SetEnabled(running);
            exportButton?.SetEnabled(running);
            if (!running) { SetStatus("Stopped. Enter Play Mode, then Compare. No user scene, shared setting or global time scale is edited."); return; }
            SetStatus((frozen ? "Frozen render at " + capturedFlightSeconds.ToString("F3") + " s; isolated shots still expire normally. " : "Live pair. ") +
                view + " · 0.20 s flight · same radius and velocity · common framing · same capture instant. Replay clears old residuals.");
        }

        private void SetStatus(string text) { if (status != null) status.text = text; }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode) Release();
            RefreshStatus();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateComparison;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Release;
            Release();
            sourceOverrideCamera = null;
            sourceOverrideConfig = null;
        }

        private void Release()
        {
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 5; i++)
                    if (images[row, i] != null) images[row, i].image = null;
            normal?.Dispose(); normal = null;
            reward?.Dispose(); reward = null;
            frozen = false;
            capturedThisVolley = false;
            capturedFlightSeconds = 0f;
            frozenToggle?.SetValueWithoutNotify(false);
            RefreshStatus();
        }
    }
}
