using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace C6Lab.Editor
{
    /// <summary>
    /// Opt-in comparison of five genuine client-visual projectiles. It never joins a room,
    /// edits the saved Lab scene or advances a gameplay model.
    /// </summary>
    public sealed class LabElementTrailComparisonWindow : EditorWindow
    {
        private const string StylePath = "Assets/Editor/ElementTrailComparison/LabElementTrailComparison.uss";
        private static readonly string[] ElementNames = { "FIRE / 화", "WATER / 수", "WOOD / 목", "METAL / 금", "EARTH / 토" };
        [SerializeField] private bool sideView;
        [SerializeField] private bool closeUp;
        [SerializeField] private bool shortFlight = true;
        [SerializeField] private bool repeat = true;
        [SerializeField] private bool autoCapture = true;
        [SerializeField] private float captureSeconds = .14f;
        private readonly Image[] images = new Image[5];
        private LabElementTrailComparisonSession session;
        private Label status;
        private Button startButton, stopButton, captureButton, exportButton;
        private Toggle frozenToggle;
        private double lastRenderAt;
        private bool frozen;

        [MenuItem("C6 Lab/Visuals/Compare Element Flight Trails")]
        public static void Open()
        {
            var window = GetWindow<LabElementTrailComparisonWindow>();
            window.titleContent = new GUIContent("Element Flight Trails");
            window.minSize = new Vector2(1050f, 590f);
            window.Show();
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
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);
            rootVisualElement.AddToClassList("trail-comparison");

            var title = new Label("Five elements · actual projectile flight") { name = "comparisonTitle" };
            title.AddToClassList("trail-title");
            rootVisualElement.Add(title);
            var explanation = new Label("Open Lab, enter Play Mode, then press Compare. These five shots use the same " +
                "LabThrowMath launch and production LabProjectile, in a separate physics scene. No room or damage is created.");
            explanation.AddToClassList("trail-description");
            rootVisualElement.Add(explanation);

            var controls = new VisualElement();
            controls.AddToClassList("trail-controls");
            startButton = new Button(StartComparison) { text = "Compare / replay" };
            stopButton = new Button(Release) { text = "Stop and clear" };
            var side = new Toggle("Side view") { value = sideView };
            side.RegisterValueChangedCallback(change => { sideView = change.newValue; RestartIfRunning(); });
            var close = new Toggle("Front close-up (auxiliary)") { value = closeUp };
            close.RegisterValueChangedCallback(change => { closeUp = change.newValue; RestartIfRunning(); });
            var shortToggle = new Toggle("Short flight (0.20 s)") { value = shortFlight };
            shortToggle.RegisterValueChangedCallback(change => { shortFlight = change.newValue; RestartIfRunning(); });
            var looping = new Toggle("Repeat") { value = repeat };
            looping.RegisterValueChangedCallback(change => repeat = change.newValue);
            frozenToggle = new Toggle("Freeze preview") { value = frozen };
            frozenToggle.RegisterValueChangedCallback(change => SetFrozen(change.newValue));
            foreach (VisualElement control in new VisualElement[] { startButton, stopButton, side, close, shortToggle, looping, frozenToggle })
            {
                control.AddToClassList("trail-control");
                controls.Add(control);
            }
            rootVisualElement.Add(controls);

            var captureControls = new VisualElement();
            captureControls.AddToClassList("trail-controls");
            var automatic = new Toggle("Auto capture") { value = autoCapture };
            automatic.RegisterValueChangedCallback(change => autoCapture = change.newValue);
            var captureTime = new FloatField("At flight seconds") { value = captureSeconds };
            captureTime.AddToClassList("trail-time-field");
            captureTime.RegisterValueChangedCallback(change =>
            {
                captureSeconds = Mathf.Clamp(change.newValue, .02f, .9f);
                captureTime.SetValueWithoutNotify(captureSeconds);
            });
            captureButton = new Button(() => SetFrozen(true)) { text = "Capture now" };
            exportButton = new Button(ExportFrames) { text = "Export displayed PNG" };
            foreach (VisualElement control in new VisualElement[] { automatic, captureTime, captureButton, exportButton })
            {
                control.AddToClassList("trail-control");
                captureControls.Add(control);
            }
            rootVisualElement.Add(captureControls);

            var panels = new VisualElement();
            panels.AddToClassList("trail-panels");
            for (int i = 0; i < images.Length; i++)
            {
                var panel = new VisualElement();
                panel.AddToClassList("trail-panel");
                var heading = new Label(ElementNames[i]);
                heading.AddToClassList("trail-panel-title");
                panel.Add(heading);
                images[i] = new Image { scaleMode = ScaleMode.ScaleToFit, name = "elementPreview" + i };
                images[i].AddToClassList("trail-frame");
                if (session != null) images[i].image = session.Frames[i];
                panel.Add(images[i]);
                panels.Add(panel);
            }
            rootVisualElement.Add(panels);
            status = new Label();
            status.AddToClassList("trail-status");
            rootVisualElement.Add(status);
            RefreshStatus();
        }

        private void RestartIfRunning()
        {
            if (session != null) StartComparison();
        }

        private void StartComparison()
        {
            Release();
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                SetStatus("Enter Lab Play Mode and resume the Editor before starting the comparison.");
                return;
            }
            if (Time.timeScale <= 0f)
            {
                SetStatus("The Lab session is paused. Resume it first; this tool never changes the session time scale.");
                return;
            }
            LabSeatCamera source = Object.FindFirstObjectByType<LabSeatCamera>();
            if (source == null || source.battleCamera == null || source.config == null)
            {
                SetStatus("No connected LabSeatCamera/config was found. Open the real Lab scene and enter Play Mode.");
                return;
            }
            try
            {
                session = new LabElementTrailComparisonSession(source.config, source.battleCamera, sideView, shortFlight, closeUp);
                for (int i = 0; i < images.Length; i++)
                    if (images[i] != null) images[i].image = session.Frames[i];
                frozen = false;
                frozenToggle?.SetValueWithoutNotify(false);
                session.StartVolley();
                session.RenderFrames();
                lastRenderAt = EditorApplication.timeSinceStartup;
                RefreshStatus();
            }
            catch (Exception error)
            {
                Release();
                SetStatus("Comparison could not start: " + error.Message);
                Debug.LogException(error);
            }
        }

        private void UpdateComparison()
        {
            if (session == null) return;
            if (!EditorApplication.isPlaying || !session.IsValid)
            {
                Release();
                return;
            }
            if (frozen) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - lastRenderAt < 1.0 / 30.0) return;
            lastRenderAt = now;
            try
            {
                session.RenderFrames();
                if (autoCapture && session.FlightAge >= captureSeconds)
                {
                    SetFrozen(true);
                    return;
                }
                if (repeat && session.FlightAge >= session.ReplayAfterSeconds)
                    session.StartVolley();
                RefreshStatus();
            }
            catch (Exception error)
            {
                Release();
                SetStatus("Comparison stopped: " + error.Message);
                Debug.LogException(error);
            }
        }

        private void SetFrozen(bool value)
        {
            if (session == null) return;
            // Only the displayed render textures are held. Neither projectiles, session time,
            // global physics nor the gameplay camera is paused by this toggle.
            if (value && !frozen) session.RenderFrames();
            frozen = value;
            frozenToggle?.SetValueWithoutNotify(value);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            bool running = session != null;
            startButton?.SetEnabled(EditorApplication.isPlaying);
            stopButton?.SetEnabled(running);
            captureButton?.SetEnabled(running);
            exportButton?.SetEnabled(running);
            if (!running)
            {
                SetStatus("Comparison is stopped. It does not save a scene, change project settings or connect a network session.");
                return;
            }
            SetStatus((frozen ? "Captured preview; isolated shots continue and expire normally. " : "Live preview. ") +
                (sideView ? "Side camera, framed to the real path" : closeUp ? "Auxiliary front close-up, framed to the real path" :
                    "Seat-0 battle camera, original aspect in letterbox") + " · current Lab camera FOV · " +
                session.FlightDuration.ToString("F2") + " s flight · identical release velocity for all five elements. " +
                "Replay clears every old shot and residual effect.");
        }

        private void ExportFrames()
        {
            if (session == null) return;
            string path = EditorUtility.SaveFilePanel("Export actual element flight comparison", "",
                "element-flight-trails-" + (sideView ? "side" : "front"), "png");
            if (string.IsNullOrEmpty(path)) return;
            Texture2D sheet = null;
            try
            {
                sheet = session.ReadContactSheet();
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                SetStatus("Displayed frames exported left to right: Fire / Water / Wood / Metal / Earth. " +
                    "This is presentation evidence only, not a multiplayer or hit test.");
            }
            catch (Exception error) { SetStatus("Export failed: " + error.Message); }
            finally { if (sheet != null) Object.DestroyImmediate(sheet); }
        }

        private void SetStatus(string message)
        {
            if (status != null) status.text = message;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
                Release();
            RefreshStatus();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateComparison;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Release;
            Release();
        }

        private void Release()
        {
            for (int i = 0; i < images.Length; i++) if (images[i] != null) images[i].image = null;
            session?.Dispose();
            session = null;
            frozen = false;
            frozenToggle?.SetValueWithoutNotify(false);
            RefreshStatus();
        }
    }

    /// <summary>Owns only transient comparison cameras, projectiles and one private physics scene.</summary>
    public sealed class LabElementTrailComparisonSession : IDisposable
    {
        public const int FrameWidth = 312;
        public const int FrameHeight = 676;
        private const int PreviewLayer = 30;
        private const float PreviewCellSpacing = 2048f, PreviewFarClip = PreviewCellSpacing * .25f;
        private static readonly HashSet<int> PreviewSlots = new HashSet<int>();
        private int previewSlot = -1;
        private static readonly LabOrbElement[] Elements =
            { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth };
        private readonly RenderTexture[] frames = new RenderTexture[5];
        private readonly Camera[] cameras = new Camera[5];
        private readonly Transform[] groups = new Transform[5];
        private readonly Vector3[] centers = new Vector3[5];
        private Scene scene;
        private GameObject root;
        private LabConfig config;
        private float maximumTrailLifetime;
        private float presentationMargin;
        private Vector3 frontViewDirection;
        private bool framePath, viewFromSide;
        private readonly bool jigwiReward;
        private float startedAt;
        private bool disposed;

        public RenderTexture[] Frames => frames;
        public bool IsValid => !disposed && scene.IsValid() && scene.isLoaded && root != null;
        public float FlightAge => Mathf.Max(0f, Time.time - startedAt);
        public float FlightDuration => config != null ? config.ThrowLifetime : 0f;
        public float ReplayAfterSeconds => FlightDuration + maximumTrailLifetime + .2f;

        public LabElementTrailComparisonSession(LabConfig sourceConfig, Camera sourceCamera, bool sideView, bool shortFlight)
            : this(sourceConfig, sourceCamera, sideView, shortFlight, false, false) { }

        public LabElementTrailComparisonSession(LabConfig sourceConfig, Camera sourceCamera, bool sideView, bool shortFlight,
            bool frontCloseUp)
            : this(sourceConfig, sourceCamera, sideView, shortFlight, frontCloseUp, false) { }

        public LabElementTrailComparisonSession(LabConfig sourceConfig, Camera sourceCamera, bool sideView, bool shortFlight,
            bool frontCloseUp, bool jigwiReward)
        {
            this.jigwiReward = jigwiReward;
            if (!Application.isPlaying) throw new InvalidOperationException("The comparison requires Play Mode.");
            if (sourceConfig == null || sourceCamera == null)
                throw new ArgumentException("A live Lab config and battle camera are required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A real graphics device is required for trail comparison.");
            if (sourceCamera.nearClipPlane >= PreviewFarClip)
                throw new ArgumentException("The preview camera near plane exceeds its isolated render range.");
            foreach (LabOrbElement element in Elements)
            {
                if (sourceConfig.OrbAppearance == null || sourceConfig.OrbAppearance.ElementTrailFor(element) == null)
                    throw new InvalidOperationException("The Lab orb appearance has no flight-trail profile for " + element + ".");
                maximumTrailLifetime = Mathf.Max(maximumTrailLifetime,
                    sourceConfig.OrbAppearance.ElementTrailFor(element).Lifetime);
                LabElementTrailProfile profile = sourceConfig.OrbAppearance.ElementTrailFor(element);
                presentationMargin = Mathf.Max(presentationMargin, Mathf.Max(profile.Width, profile.ParticleSize));
            }
            // Both rows reserve the same presentation margin; the reward flag never changes body size.
            LabJigwiRewardFireProfile rewardProfile = Resources.Load<LabJigwiRewardFireProfile>(LabJigwiRewardFireProfile.ResourcesPath);
            float rewardWidth = rewardProfile != null ? rewardProfile.WidthScale : 1.05f;
            presentationMargin = Mathf.Max(presentationMargin, sourceConfig.ThrowRadius * Mathf.Max(2.5f, rewardWidth * 2f));
            maximumTrailLifetime = Mathf.Max(maximumTrailLifetime,
                rewardProfile != null ? Mathf.Max(rewardProfile.Lifetime, rewardProfile.EmberLifetime) : .45f);
            try
            {
                previewSlot = ClaimPreviewSlot();
                config = Object.Instantiate(sourceConfig);
                config.name = "Element trail comparison config (transient)";
                config.hideFlags = HideFlags.HideAndDontSave;
                var serialized = new SerializedObject(config);
                serialized.FindProperty("throwLifetime").floatValue = shortFlight ? .20f : .45f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                framePath = sideView || frontCloseUp;
                viewFromSide = sideView;
                Vector3 battleOffset = LabThrowMath.SeatPosition(0, 1, Vector3.zero,
                    config.CameraRadius, config.CameraHeightOffset);
                frontViewDirection = (Vector3.up * config.CameraLookHeightOffset - battleOffset).normalized;
                scene = SceneManager.CreateScene("Element flight comparison " + Guid.NewGuid().ToString("N"),
                    new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                root = new GameObject("Element flight comparison (temporary)") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<LabElementTrailComparisonPhysics>().Initialize(scene.GetPhysicsScene());
                for (int i = 0; i < frames.Length; i++)
                {
                    // Physics scenes isolate simulation, not SRP rendering. Every live session/panel
                    // therefore has a distinct volume beyond all other preview cameras' far plane.
                    centers[i] = new Vector3(10000f + i * PreviewCellSpacing, 0f, previewSlot * PreviewCellSpacing);
                    var group = new GameObject(Elements[i] + " comparison shots");
                    group.transform.SetParent(root.transform, false);
                    groups[i] = group.transform;
                    var cameraObject = new GameObject(Elements[i] + " comparison camera");
                    cameraObject.transform.SetParent(root.transform, false);
                    Camera camera = cameraObject.AddComponent<Camera>();
                    camera.CopyFrom(sourceCamera);
                    camera.farClipPlane = Mathf.Min(sourceCamera.farClipPlane, PreviewFarClip);
                    camera.enabled = false;
                    camera.tag = "Untagged";
                    camera.cameraType = CameraType.Preview;
                    camera.scene = scene;
                    camera.cullingMask = 1 << PreviewLayer;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.79f, .80f, .79f, 1f);
                    // The battle camera occupies only part of the game's portrait screen.
                    // Its own aspect must not be replaced by the full-screen portrait aspect.
                    // Path-framed auxiliary views use the complete comparison image instead.
                    float aspect = framePath ? (float)FrameWidth / FrameHeight : sourceCamera.aspect;
                    camera.rect = Letterbox(aspect);
                    camera.aspect = aspect;
                    camera.allowHDR = false;
                    camera.allowMSAA = false;
                    Vector3 offset = LabThrowMath.SeatPosition(0, 1, Vector3.zero,
                        config.CameraRadius, config.CameraHeightOffset);
                    camera.transform.position = centers[i] + offset;
                    camera.transform.LookAt(centers[i] + Vector3.up * config.CameraLookHeightOffset);
                    var frame = new RenderTexture(FrameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32)
                        { hideFlags = HideFlags.HideAndDontSave, name = Elements[i] + " comparison frame" };
                    frames[i] = frame;
                    if (!frame.Create()) throw new InvalidOperationException("Could not allocate a comparison render texture.");
                    camera.targetTexture = frame;
                    cameras[i] = camera;
                }
            }
            catch { Dispose(); throw; }
        }

        private static int ClaimPreviewSlot()
        {
            int slot = 0;
            while (PreviewSlots.Contains(slot)) slot++;
            PreviewSlots.Add(slot);
            return slot;
        }

        public void StartVolley()
        {
            if (!IsValid) throw new ObjectDisposedException(nameof(LabElementTrailComparisonSession));
            for (int i = 0; i < groups.Length; i++)
            {
                // Recreate the throw, rather than relocating a live emitter with old history.
                for (int child = groups[i].childCount - 1; child >= 0; child--)
                    Object.DestroyImmediate(groups[i].GetChild(child).gameObject);
                Vector3 anchor = LabThrowMath.SeatPosition(0, 1, centers[i],
                    config.ThrowOriginRadius, config.ThrowOriginHeightOffset);
                Vector3 launch = LabThrowMath.LaunchPosition(0, 1, centers[i], .5f, config);
                Vector3 velocity = LabThrowMath.LaunchVelocity(new Vector2(.12f, 1.5f), anchor, centers[i], config);
                LabProjectile projectile = LabProjectile.Create(launch, config, groups[i], Elements[i]);
                if (jigwiReward) projectile.SetJigwiABReward(true);
                foreach (Transform child in projectile.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = PreviewLayer;
                projectile.LaunchVisual(velocity, config);
            }
            startedAt = Time.time;
        }

        public void RenderFrames() => RenderFramesWithPeer(null);

        public void RenderFramesWithPeer(LabElementTrailComparisonSession framingPeer)
        {
            if (!IsValid) throw new ObjectDisposedException(nameof(LabElementTrailComparisonSession));
            if (framePath) FrameRecordedPath(framingPeer);
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] == null) continue;
                    // Camera clearing is limited to its viewport. Clear letterbox bands too,
                    // so frames never contain uninitialized or previous-shot pixels.
                    RenderTexture.active = frames[i];
                    GL.Clear(true, true, new Color(.12f, .13f, .14f, 1f));
                    cameras[i].Render();
                }
            }
            finally { RenderTexture.active = previous; }
        }

        private static Rect Letterbox(float aspect)
        {
            float frameAspect = (float)FrameWidth / FrameHeight;
            if (aspect > frameAspect)
            {
                float height = frameAspect / aspect;
                return new Rect(0f, (1f - height) * .5f, 1f, height);
            }
            float width = aspect / frameAspect;
            return new Rect((1f - width) * .5f, 0f, width, 1f);
        }

        private Bounds RecordedPathBounds()
        {
            // Coordinates are local to each panel; no hypothetical flight or scene body is added.
            Vector3 launch = LabThrowMath.LaunchPosition(0, 1, Vector3.zero, .5f, config);
            var bounds = new Bounds(launch, Vector3.zero);
            for (int i = 0; i < groups.Length; i++)
            {
                foreach (LabProjectile projectile in groups[i].GetComponentsInChildren<LabProjectile>())
                    bounds.Encapsulate(projectile.transform.position - centers[i]);
                foreach (LabElementFlightTrail trail in groups[i].GetComponentsInChildren<LabElementFlightTrail>())
                    for (int point = 0; point < trail.PathCount; point++)
                        bounds.Encapsulate(trail.GetPathPosition(point) - centers[i]);
                foreach (LabJigwiRewardFireTrail trail in groups[i].GetComponentsInChildren<LabJigwiRewardFireTrail>())
                    for (int point = 0; point < trail.PathCount; point++)
                        bounds.Encapsulate(trail.GetPathPosition(point) - centers[i]);
            }
            return bounds;
        }

        private void FrameRecordedPath(LabElementTrailComparisonSession framingPeer)
        {
            Bounds bounds = RecordedPathBounds();
            float margin = presentationMargin + config.ThrowRadius;
            if (framingPeer != null && framingPeer.IsValid)
            {
                bounds.Encapsulate(framingPeer.RecordedPathBounds());
                margin = Mathf.Max(margin, framingPeer.presentationMargin + framingPeer.config.ThrowRadius);
            }
            bounds.Expand(2f * margin);
            Vector3 forward = viewFromSide ? Vector3.left : frontViewDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            Vector3 extents = bounds.extents;
            float halfWidth = ProjectionExtent(right, extents);
            float halfHeight = ProjectionExtent(up, extents);
            float halfDepth = ProjectionExtent(forward, extents);
            float verticalTan = Mathf.Tan(cameras[0].fieldOfView * Mathf.Deg2Rad * .5f);
            float distance = Mathf.Max(halfHeight / verticalTan,
                halfWidth / (verticalTan * cameras[0].aspect)) * 1.12f + halfDepth + cameras[0].nearClipPlane;
            for (int i = 0; i < cameras.Length; i++)
            {
                Vector3 focus = centers[i] + bounds.center;
                cameras[i].transform.SetPositionAndRotation(focus - forward * distance,
                    Quaternion.LookRotation(forward, up));
            }
        }

        private static float ProjectionExtent(Vector3 axis, Vector3 extents)
            => Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;

        /// <summary>Exports exactly the displayed frames; it does not rerender or fabricate a path.</summary>
        public Texture2D ReadContactSheet()
        {
            if (!IsValid) throw new ObjectDisposedException(nameof(LabElementTrailComparisonSession));
            var sheet = new Texture2D(FrameWidth * frames.Length, FrameHeight, TextureFormat.RGBA32, false, false)
                { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    RenderTexture.active = frames[i];
                    sheet.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), i * FrameWidth, 0, false);
                }
                sheet.Apply(false, false);
                return sheet;
            }
            catch { Object.DestroyImmediate(sheet); throw; }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Camera camera in cameras) if (camera != null) camera.targetTexture = null;
            if (root != null) Object.DestroyImmediate(root);
            root = null;
            if (previewSlot >= 0) { PreviewSlots.Remove(previewSlot); previewSlot = -1; }
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null) continue;
                frames[i].Release();
                Object.DestroyImmediate(frames[i]);
                frames[i] = null;
            }
            if (config != null) Object.DestroyImmediate(config);
            config = null;
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
