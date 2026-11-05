using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hapio.Experiments
{
    /// <summary>A local material study. It never creates a session, physics target or paint model.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ClayPreviewController : MonoBehaviour
    {
        public const float DefaultThickness = .01f;
        public const float DefaultGloss = .35f;
        private const int PreviewLayer = 31;

        public GameObject sourcePrefab;
        public Texture2D colorMap, heightMap;
        public Shader clayShader;
        public Font uiFont;
        public StyleSheet styleSheet;

        private readonly List<Material> materials = new List<Material>();
        private GameObject ownedRoot;
        private Transform modelPivot;
        private MeshRenderer body;
        private Material clayMaterial;
        private Camera previewCamera;
        private RenderTexture target;
        private VisualElement uiRoot, viewport;
        private Image image;
        private Label thicknessValue, glossValue, status;
        private Slider thicknessSlider, glossSlider;
        private ViewManipulator manipulation;
        private bool initialized, globalsCaptured, focused = true;
        private int previousTargetFrameRate, previousVSync;
        private bool previousRunInBackground;

        public float Thickness { get; private set; } = DefaultThickness;
        public float Gloss { get; private set; } = DefaultGloss;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Zoom { get; private set; } = 1f;
        public Transform ModelRoot => modelPivot;
        public Material ClayMaterial => clayMaterial;
        public Camera PreviewCamera => previewCamera;
        public RenderTexture PreviewTexture => target;
        public bool IsReady => initialized && target != null && target.IsCreated();
        public string Error { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            try
            {
                Require(sourcePrefab != null && colorMap != null && heightMap != null && clayShader != null,
                    "The completed sample, gourd model or clay shader is missing.");
                Require(clayShader.isSupported, "The clay shader is unavailable on this graphics device.");
                Require(colorMap.width == 1024 && colorMap.height == 512 && heightMap.width == 1024 && heightMap.height == 512,
                    "The sample maps must both be 1024 x 512.");
                CaptureGlobals();
                if (!Application.isEditor) Screen.SetResolution(1280, 800, FullScreenMode.Windowed);
                BuildPresentation();
                BuildUi();
                initialized = true;
                ResizeTarget(900, 800);
                ResetViewAndAppearance();
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                if (status != null) { status.text = Error; status.AddToClassList("clay-error"); }
                Debug.LogError("CLAY_PREVIEW_FAILED: " + Error, this);
                ReleasePresentation();
                RestoreGlobals();
            }
        }

        private void CaptureGlobals()
        {
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            previousRunInBackground = Application.runInBackground;
            globalsCaptured = true;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = false;
        }

        private void RestoreGlobals()
        {
            if (!globalsCaptured) return;
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSync;
            Application.runInBackground = previousRunInBackground;
            globalsCaptured = false;
        }

        private void BuildPresentation()
        {
            ownedRoot = new GameObject("Clay preview presentation");
            ownedRoot.transform.SetParent(transform, false);
            modelPivot = new GameObject("Gourd rotation").transform;
            modelPivot.SetParent(ownedRoot.transform, false);
            Transform instance = CopyRenderHierarchy(sourcePrefab.transform, modelPivot);
            Require(body != null, "The saved gourd has no paint-shell mesh.");
            // Only this private hierarchy is centered. Mesh-local coordinates remain intact.
            instance.position -= body.bounds.center;

            var coat = new GameObject("Clay Sticker Overlay");
            coat.layer = PreviewLayer;
            coat.transform.SetParent(body.transform, false);
            coat.AddComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
            MeshRenderer draw = coat.AddComponent<MeshRenderer>();
            clayMaterial = new Material(clayShader) { name = "Experiment clay material", hideFlags = HideFlags.DontSave };
            materials.Add(clayMaterial);
            clayMaterial.SetTexture("_PaintMap", colorMap);
            clayMaterial.SetTexture("_HeightMap", heightMap);
            clayMaterial.SetTexture("_PainterMap", Texture2D.blackTexture);
            clayMaterial.SetFloat("_PaintMapReady", 1f);
            clayMaterial.SetFloat("_PlayerPaintPatternsEnabled", 0f);
            draw.sharedMaterial = clayMaterial;
            draw.shadowCastingMode = ShadowCastingMode.Off;
            draw.receiveShadows = false;
            Bounds bounds = coat.GetComponent<MeshFilter>().sharedMesh.bounds;
            bounds.Expand(.05f);
            draw.localBounds = bounds;

            var cameraObject = new GameObject("Clay preview camera");
            cameraObject.transform.SetParent(ownedRoot.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.075f, .095f, .115f, 1f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.nearClipPlane = .01f;
            previewCamera.farClipPlane = 100f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.useOcclusionCulling = false;

            var lightObject = new GameObject("Clay preview key light");
            lightObject.transform.SetParent(ownedRoot.transform, false);
            Light key = lightObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1f;
            key.cullingMask = 1 << PreviewLayer;
            key.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        }

        private Transform CopyRenderHierarchy(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name) { layer = PreviewLayer };
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            var sourceFilter = source.GetComponent<MeshFilter>();
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceFilter != null && sourceFilter.sharedMesh != null && sourceRenderer != null)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                Material[] copies = sourceRenderer.sharedMaterials;
                for (int i = 0; i < copies.Length; i++)
                {
                    if (copies[i] == null) continue;
                    Material material = new Material(copies[i]) { hideFlags = HideFlags.DontSave };
                    materials.Add(material);
                    copies[i] = material;
                    if (material.shader != null && material.shader.name == "C6Lab/GourdPaint")
                    {
                        Require(body == null, "The source has more than one paint-shell renderer.");
                        body = renderer;
                        material.SetFloat("_PaintMapReady", 0f);
                        material.SetFloat("_PaperContrast", 0f);
                        material.SetFloat("_PaperBump", 0f);
                        material.SetFloat("_PaperSmoothness", 0f);
                    }
                }
                renderer.sharedMaterials = copies;
                renderer.enabled = sourceRenderer.enabled;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            foreach (Transform child in source) CopyRenderHierarchy(child, copy.transform);
            copy.SetActive(source.gameObject.activeSelf);
            return copy.transform;
        }

        private void BuildUi()
        {
            uiRoot = GetComponent<UIDocument>().rootVisualElement;
            Require(uiRoot != null && styleSheet != null, "The experiment panel or stylesheet is missing.");
            uiRoot.Clear();
            uiRoot.styleSheets.Add(styleSheet);
            if (uiFont != null) uiRoot.style.unityFont = uiFont;
            uiRoot.AddToClassList("clay-app");
            viewport = new VisualElement { name = "previewViewport", focusable = true };
            viewport.AddToClassList("clay-viewport");
            image = new Image { name = "previewImage", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("clay-preview-image");
            viewport.Add(image);
            viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometry);
            manipulation = new ViewManipulator(this);
            viewport.AddManipulator(manipulation);
            uiRoot.Add(viewport);
            var panel = new VisualElement { name = "materialPanel" };
            panel.AddToClassList("clay-panel");
            uiRoot.Add(panel);
            AddLabel(panel, "CLAY STUDY", "clay-heading");
            AddLabel(panel, "Completed 5-color gourd\nMaterial preview only", "clay-subtitle");
            thicknessSlider = AddSlider(panel, "THICKNESS", "thicknessSlider", 0, .02f, out thicknessValue);
            glossSlider = AddSlider(panel, "GLOSS", "glossSlider", 0, 1, out glossValue);
            thicknessSlider.RegisterValueChangedCallback(e => SetAppearance(e.newValue, Gloss));
            glossSlider.RegisterValueChangedCallback(e => SetAppearance(Thickness, e.newValue));
            AddLabel(panel, "VIEW", "clay-section-label");
            AddViewRow(panel, new[] { "Front", "Right", "Back" }, new[] { "frontView", "rightView", "backView" }, new[] { 0f, 90f, 180f }, new[] { 0f, 0f, 0f });
            AddViewRow(panel, new[] { "Left", "Diagonal", "Above" }, new[] { "leftView", "diagonalView", "upperView" }, new[] { -90f, 45f, 45f }, new[] { 0f, 0f, 40f });
            Button reset = AddButton(panel, "Reset defaults", "resetButton", ResetViewAndAppearance);
            reset.AddToClassList("clay-primary");
            Button quit = AddButton(panel, "Quit", "quitButton", () => Application.Quit());
            quit.AddToClassList("clay-quit");
            var spacer = new VisualElement(); spacer.AddToClassList("clay-spacer"); panel.Add(spacer);
            status = AddLabel(panel, "Drag the gourd to rotate.\nScroll to zoom.\n\n100% painted · fixed artwork\n30 fps · no network session\nChanges stay in this app only.", "clay-status");
        }

        private static Label AddLabel(VisualElement parent, string text, string style)
        {
            var label = new Label(text); label.AddToClassList(style); parent.Add(label); return label;
        }
        private Slider AddSlider(VisualElement panel, string heading, string name, float low, float high, out Label value)
        {
            var row = new VisualElement(); row.AddToClassList("clay-field-heading");
            AddLabel(row, heading, "clay-field-label");
            value = AddLabel(row, string.Empty, "clay-number");
            value.name = name + "Value";
            panel.Add(row);
            var slider = new Slider(low, high) { name = name };
            slider.AddToClassList("clay-slider"); panel.Add(slider); return slider;
        }
        private static Button AddButton(VisualElement parent, string title, string name, Action action)
        {
            var button = new Button(action) { name = name, text = title };
            button.AddToClassList("clay-button"); parent.Add(button); return button;
        }
        private void AddViewRow(VisualElement panel, string[] labels, string[] names, float[] yaws, float[] pitches)
        {
            var row = new VisualElement(); row.AddToClassList("clay-button-row"); panel.Add(row);
            for (int i = 0; i < labels.Length; i++)
            {
                int view = i;
                AddButton(row, labels[i], names[i], () => SetView(yaws[view], pitches[view]));
            }
        }

        public void SetAppearance(float thickness, float gloss)
        {
            if (!Finite(thickness) || !Finite(gloss)) throw new ArgumentOutOfRangeException("Appearance values must be finite.");
            Thickness = Mathf.Clamp(thickness, 0, .02f);
            Gloss = Mathf.Clamp01(gloss);
            if (clayMaterial != null)
            {
                clayMaterial.SetFloat("_ClayThickness", Thickness);
                clayMaterial.SetFloat("_ClayGloss", Gloss);
            }
            thicknessSlider?.SetValueWithoutNotify(Thickness);
            glossSlider?.SetValueWithoutNotify(Gloss);
            if (thicknessValue != null) thicknessValue.text = Thickness.ToString("0.0000", CultureInfo.InvariantCulture);
            if (glossValue != null) glossValue.text = Gloss.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public void SetView(float yaw, float pitch, float zoom = 1f)
        {
            if (!Finite(yaw) || !Finite(pitch) || !Finite(zoom)) throw new ArgumentOutOfRangeException("View values must be finite.");
            Yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            Pitch = Mathf.Clamp(pitch, -80f, 80f);
            Zoom = Mathf.Clamp(zoom, .65f, 3f);
            if (modelPivot != null) modelPivot.localRotation = Quaternion.Euler(Pitch, Yaw, 0f);
            FitCamera();
        }

        public void ResetViewAndAppearance()
        {
            SetAppearance(DefaultThickness, DefaultGloss);
            SetView(0, 0, 1);
        }

        private void OnViewportGeometry(GeometryChangedEvent change)
        {
            if (change.newRect.width > 0 && change.newRect.height > 0)
                ResizeTarget(change.newRect.width, change.newRect.height);
        }

        private void ResizeTarget(float width, float height)
        {
            if (previewCamera == null || !Finite(width) || !Finite(height)) return;
            int w = Mathf.Clamp(Mathf.RoundToInt(width), 128, 1536);
            int h = Mathf.Clamp(Mathf.RoundToInt(height), 128, 1536);
            if (target != null && target.width == w && target.height == h) return;
            ReleaseTarget();
            target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { name = "Clay experiment viewport", antiAliasing = 1, hideFlags = HideFlags.DontSave };
            Require(target.Create(), "The preview render target could not be created.");
            previewCamera.targetTexture = target;
            previewCamera.aspect = w / (float)h;
            if (image != null) image.image = target;
            previewCamera.enabled = focused;
            FitCamera();
        }

        private void FitCamera()
        {
            if (previewCamera == null || modelPivot == null) return;
            Renderer[] renderers = modelPivot.GetComponentsInChildren<Renderer>();
            bool found = false; Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) return;
            float distance = Mathf.Max(4f, bounds.size.magnitude * 2f);
            previewCamera.transform.SetPositionAndRotation(bounds.center + Vector3.forward * distance, Quaternion.Euler(0, 180f, 0));
            previewCamera.farClipPlane = distance + Mathf.Max(10f, bounds.size.magnitude * 3f);
            previewCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(.1f, previewCamera.aspect)) * 1.12f / Zoom;
        }

        private void OnApplicationFocus(bool focus)
        {
            focused = focus;
            if (!focus) manipulation?.Cancel();
            if (previewCamera != null) previewCamera.enabled = focus && target != null;
        }

        private void OnDisable()
        {
            initialized = false;
            if (viewport != null)
            {
                viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometry);
                if (manipulation != null) viewport.RemoveManipulator(manipulation);
            }
            manipulation = null;
            uiRoot?.Clear();
            ReleasePresentation();
            RestoreGlobals();
        }

        private void ReleasePresentation()
        {
            ReleaseTarget();
            if (ownedRoot != null) DestroyOwned(ownedRoot);
            foreach (Material material in materials) if (material != null) DestroyOwned(material);
            materials.Clear(); ownedRoot = null; modelPivot = null; body = null; clayMaterial = null; previewCamera = null;
        }

        private void ReleaseTarget()
        {
            if (previewCamera != null) { previewCamera.enabled = false; previewCamera.targetTexture = null; }
            if (image != null) image.image = null;
            if (target != null) { target.Release(); DestroyOwned(target); target = null; }
        }

        private static void DestroyOwned(UnityEngine.Object item)
        { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private sealed class ViewManipulator : PointerManipulator
        {
            private readonly ClayPreviewController owner;
            private int pointer = -1;
            private Vector2 last;
            public ViewManipulator(ClayPreviewController owner) { this.owner = owner; }
            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(Down);
                target.RegisterCallback<PointerMoveEvent>(Move);
                target.RegisterCallback<PointerUpEvent>(Up);
                target.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
                target.RegisterCallback<WheelEvent>(Wheel);
            }
            protected override void UnregisterCallbacksFromTarget()
            {
                Cancel();
                target.UnregisterCallback<PointerDownEvent>(Down);
                target.UnregisterCallback<PointerMoveEvent>(Move);
                target.UnregisterCallback<PointerUpEvent>(Up);
                target.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
                target.UnregisterCallback<WheelEvent>(Wheel);
            }
            private void Down(PointerDownEvent evt)
            {
                if (evt.button != 0 || pointer >= 0) return;
                pointer = evt.pointerId; last = evt.position;
                target.CapturePointer(pointer); target.Focus(); evt.StopPropagation();
            }
            private void Move(PointerMoveEvent evt)
            {
                if (evt.pointerId != pointer || !target.HasPointerCapture(pointer)) return;
                Vector2 position = evt.position, delta = position - last; last = position;
                owner.SetView(owner.Yaw + delta.x * .4f, owner.Pitch - delta.y * .3f, owner.Zoom);
                evt.StopPropagation();
            }
            private void Up(PointerUpEvent evt)
            { if (evt.pointerId == pointer) { Cancel(); evt.StopPropagation(); } }
            private void CaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == pointer) pointer = -1; }
            private void Wheel(WheelEvent evt)
            {
                owner.SetView(owner.Yaw, owner.Pitch, owner.Zoom * Mathf.Exp(-evt.delta.y * .04f));
                evt.StopPropagation();
            }
            public void Cancel()
            {
                int released = pointer; pointer = -1;
                if (released >= 0 && target != null && target.HasPointerCapture(released)) target.ReleasePointer(released);
            }
        }
    }
}
