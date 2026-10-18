using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public sealed class LabPaintStyleComparisonWindow : EditorWindow
    {
        private const string LayoutPath = "Assets/Editor/PaintStyleComparison/LabPaintStyleComparison.uxml";
        [SerializeField] private LabPaintStudyStage stage = LabPaintStudyStage.Multicolor;
        [SerializeField] private float yaw;
        [SerializeField] private float zoom = 1f;
        [SerializeField] private bool grain = true;
        private LabPaintStudyRenderer renderer;
        private LabPaintStudyMap[] maps;
        private LabPaintStudyMask[] masks;
        private readonly Texture2D[] frames = new Texture2D[3];
        private readonly Image[] images = new Image[3];
        private readonly Label[] counts = new Label[3];
        private EnumField stageField;
        private Slider yawSlider, zoomSlider;
        private Toggle grainToggle;
        private Button resetButton, exportButton;
        private Label status;

        [MenuItem("C6 Lab/Experiments/Paint Style Comparison")]
        public static void Open()
        {
            var window = GetWindow<LabPaintStyleComparisonWindow>();
            window.titleContent = new GUIContent("Paint Style Comparison");
            window.minSize = new Vector2(870f, 500f);
        }

        public void CreateGUI()
        {
            Release();
            rootVisualElement.Clear();
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null)
            {
                rootVisualElement.Add(new Label("Paint comparison layout is missing: " + LayoutPath));
                return;
            }
            layout.CloneTree(rootVisualElement);
            VisualElement controls = rootVisualElement.Q<VisualElement>("controls");
            stageField = new EnumField("Stage", stage);
            yawSlider = new Slider("Yaw", -180f, 180f) { value = yaw, showInputField = true };
            zoomSlider = new Slider("Zoom", .65f, 1.55f) { value = zoom, showInputField = true };
            grainToggle = new Toggle("Internal grain") { value = grain };
            foreach (VisualElement control in new VisualElement[] { stageField, yawSlider, zoomSlider, grainToggle })
            {
                control.AddToClassList("paint-study-control");
                controls.Add(control);
            }
            stageField.RegisterValueChangedCallback(OnStageChanged);
            yawSlider.RegisterValueChangedCallback(OnYawChanged);
            zoomSlider.RegisterValueChangedCallback(OnZoomChanged);
            grainToggle.RegisterValueChangedCallback(OnGrainChanged);
            string[] names = { "broad", "splatter", "flow" };
            for (int i = 0; i < names.Length; i++)
            {
                images[i] = rootVisualElement.Q<Image>(names[i] + "Image");
                images[i].scaleMode = ScaleMode.ScaleToFit;
                counts[i] = rootVisualElement.Q<Label>(names[i] + "Count");
            }
            resetButton = rootVisualElement.Q<Button>("resetView");
            exportButton = rootVisualElement.Q<Button>("exportPng");
            resetButton.clicked += ResetView;
            exportButton.clicked += ExportPng;
            status = rootVisualElement.Q<Label>("status");
            RebuildMaps();
        }

        private void OnStageChanged(ChangeEvent<Enum> change)
        {
            stage = (LabPaintStudyStage)change.newValue;
            RebuildMaps();
        }

        private void OnYawChanged(ChangeEvent<float> change) { yaw = change.newValue; RenderFrames(); }
        private void OnZoomChanged(ChangeEvent<float> change) { zoom = change.newValue; RenderFrames(); }
        private void OnGrainChanged(ChangeEvent<bool> change) { grain = change.newValue; RenderFrames(); }

        private void RebuildMaps()
        {
            try
            {
                if (renderer == null) renderer = new LabPaintStudyRenderer();
                renderer.ClearMapCache();
                if (masks == null)
                {
                    masks = new LabPaintStudyMask[3];
                    for (int i = 0; i < 3; i++) masks[i] = LabPaintStudyRenderer.LoadMask((LabPaintStudyStyle)i);
                }
                maps = new LabPaintStudyMap[3];
                for (int i = 0; i < 3; i++)
                    maps[i] = LabPaintStyleStudy.Build(masks[i], (LabPaintStudyStyle)i, stage,
                        LabPaintStudyRenderer.MapColumns, LabPaintStudyRenderer.MapRows, LabPaintStudyRenderer.Seed);
                RenderFrames();
            }
            catch (Exception error) { ShowError(error); }
        }

        private void RenderFrames()
        {
            if (renderer == null || maps == null) return;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Texture2D next = renderer.Render(maps[i], grain, yaw, zoom);
                    if (frames[i] != null) Object.DestroyImmediate(frames[i]);
                    frames[i] = next;
                    images[i].image = next;
                    counts[i].text = $"Fixture coverage {maps[i].Coverage:P1} · {maps[i].CoveredCount:N0} texels";
                }
                status.RemoveFromClassList("paint-study-error");
                status.text = $"{stage} · Seed {LabPaintStudyRenderer.Seed} · 1024 × 512 map · " +
                    "Bilinear preview only. Layout is prepared; it does not change gameplay growth or color ownership.";
                exportButton.SetEnabled(true);
            }
            catch (Exception error) { ShowError(error); }
        }

        private void ResetView()
        {
            yaw = 0f;
            zoom = 1f;
            yawSlider.SetValueWithoutNotify(yaw);
            zoomSlider.SetValueWithoutNotify(zoom);
            RenderFrames();
        }

        private void ExportPng()
        {
            if (frames[0] == null || frames[1] == null || frames[2] == null) return;
            string path = EditorUtility.SaveFilePanel("Export paint style comparison", "",
                "gourd-paint-" + stage.ToString().ToLowerInvariant(), "png");
            if (string.IsNullOrEmpty(path)) return;
            Texture2D sheet = null;
            try
            {
                sheet = LabPaintStudyRenderer.Combine(frames);
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                status.text = "Saved left-to-right: Broad / Splatter / Flow. " + path;
            }
            catch (Exception error) { ShowError(error); }
            finally { if (sheet != null) Object.DestroyImmediate(sheet); }
        }

        private void ShowError(Exception error)
        {
            if (status == null) return;
            status.AddToClassList("paint-study-error");
            status.text = error.Message;
            if (exportButton != null) exportButton.SetEnabled(false);
            Debug.LogException(error);
        }

        private void OnDisable() { Release(); }

        private void Release()
        {
            if (stageField != null) stageField.UnregisterValueChangedCallback(OnStageChanged);
            if (yawSlider != null) yawSlider.UnregisterValueChangedCallback(OnYawChanged);
            if (zoomSlider != null) zoomSlider.UnregisterValueChangedCallback(OnZoomChanged);
            if (grainToggle != null) grainToggle.UnregisterValueChangedCallback(OnGrainChanged);
            if (resetButton != null) resetButton.clicked -= ResetView;
            if (exportButton != null) exportButton.clicked -= ExportPng;
            for (int i = 0; i < frames.Length; i++)
            {
                if (images[i] != null) images[i].image = null;
                if (frames[i] != null) Object.DestroyImmediate(frames[i]);
                frames[i] = null;
                images[i] = null;
                counts[i] = null;
            }
            renderer?.Dispose();
            renderer = null;
            maps = null;
            masks = null;
            stageField = null;
            yawSlider = null;
            zoomSlider = null;
            grainToggle = null;
            resetButton = null;
            exportButton = null;
            status = null;
        }
    }
}
