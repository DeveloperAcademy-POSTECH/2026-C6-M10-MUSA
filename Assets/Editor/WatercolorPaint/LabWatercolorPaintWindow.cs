using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public sealed class LabWatercolorPaintWindow : EditorWindow
    {
        private const string LayoutPath = "Assets/Editor/WatercolorPaint/LabWatercolorPaint.uxml";
        [SerializeField] private int repeats = 3, seed = 123;
        [SerializeField] private float yaw, pitch, zoom = 1f;
        [SerializeField] private HighResolutionHitLocation location;
        [SerializeField] private LabWatercolorPaintSettings settings = new LabWatercolorPaintSettings();
        private LabWatercolorPaintStudy study;
        private Texture2D[] frames;
        private Image currentImage, candidateImage;
        private Label status;
        private Button exportButton;
        private SliderInt repeatsSlider;
        private Slider yawSlider, pitchSlider, zoomSlider;
        private IVisualElementScheduledItem renderRequest;

        [MenuItem("C6 Lab/Experiments/Watercolor Paint")]
        public static void Open()
        {
            var window = GetWindow<LabWatercolorPaintWindow>();
            window.titleContent = new GUIContent("Watercolor Paint");
            window.minSize = new Vector2(950f, 670f);
        }

        public void CreateGUI()
        {
            Release();
            rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null)
            {
                rootVisualElement.Add(new Label("Watercolor comparison layout is missing."));
                return;
            }
            layout.CloneTree(rootVisualElement);
            settings ??= new LabWatercolorPaintSettings();
            currentImage = rootVisualElement.Q<Image>("currentImage");
            candidateImage = rootVisualElement.Q<Image>("candidateImage");
            currentImage.scaleMode = candidateImage.scaleMode = ScaleMode.ScaleToFit;
            status = rootVisualElement.Q<Label>("status");
            exportButton = rootVisualElement.Q<Button>("exportComparison");
            exportButton.SetEnabled(false);
            exportButton.clicked += Export;
            renderRequest = rootVisualElement.schedule.Execute(Render);
            renderRequest.Pause();

            VisualElement viewControls = rootVisualElement.Q<VisualElement>("viewControls");
            repeatsSlider = new SliderInt("Re-hits after first", 0, 3)
                { value = repeats, showInputField = true };
            repeatsSlider.AddToClassList("watercolor-control");
            repeatsSlider.RegisterValueChangedCallback(e => { repeats = e.newValue; ScheduleRender(); });
            viewControls.Add(repeatsSlider);
            var seedField = new IntegerField("Fixed seed") { value = seed, isDelayed = true };
            seedField.AddToClassList("watercolor-control");
            seedField.RegisterValueChangedCallback(e => { seed = e.newValue; ScheduleRender(); });
            viewControls.Add(seedField);
            var locationField = new EnumField("Hit location", location);
            locationField.AddToClassList("watercolor-control");
            locationField.RegisterValueChangedCallback(e =>
            {
                location = (HighResolutionHitLocation)e.newValue;
                SetView(location == HighResolutionHitLocation.LongitudeSeam ? 90f : 0f,
                    location == HighResolutionHitLocation.NorthPole ? 90f : 0f);
            });
            viewControls.Add(locationField);
            yawSlider = AddSlider(viewControls, "Yaw", -180f, 180f, yaw, v => yaw = v);
            pitchSlider = AddSlider(viewControls, "Pitch", -90f, 90f, pitch, v => pitch = v);
            zoomSlider = AddSlider(viewControls, "Zoom", .65f, 3f, zoom, v => zoom = v);

            VisualElement pigmentControls = rootVisualElement.Q<VisualElement>("pigmentControls");
            AddSlider(pigmentControls, "Pigment density", .1f, .95f, settings.density, v => settings.density = v);
            AddSlider(pigmentControls, "Broad variation", 0f, .8f, settings.broadVariation, v => settings.broadVariation = v);
            AddSlider(pigmentControls, "Pigment mottling", 0f, .8f, settings.mottle, v => settings.mottle = v);
            AddSlider(pigmentControls, "Fine grain", 0f, .8f, settings.grain, v => settings.grain = v);
            AddSlider(pigmentControls, "Grain size", .25f, 4f, settings.grainScale, v => settings.grainScale = v);
            AddSlider(pigmentControls, "Edge strength", 0f, .8f, settings.edgeStrength, v => settings.edgeStrength = v);
            AddSlider(pigmentControls, "Edge width", .005f, .12f, settings.edgeWidth, v => settings.edgeWidth = v);
            AddSlider(pigmentControls, "Texture size", .25f, 4f, settings.textureScale, v => settings.textureScale = v);

            rootVisualElement.Q<Button>("nextHit").clicked += () => repeatsSlider.value = repeats < 3 ? repeats + 1 : 0;
            rootVisualElement.Q<Button>("frontView").clicked += () => SetView(0f, 0f);
            rootVisualElement.Q<Button>("seamView").clicked += () => SetView(90f, 0f);
            rootVisualElement.Q<Button>("poleView").clicked += () => SetView(0f, 90f);
            rootVisualElement.Q<Button>("refreshComparison").clicked += Render;
            Render();
        }

        private Slider AddSlider(VisualElement parent, string label, float low, float high,
            float value, Action<float> changed)
        {
            var slider = new Slider(label, low, high) { value = value, showInputField = true };
            slider.AddToClassList("watercolor-control");
            slider.RegisterValueChangedCallback(e => { changed(e.newValue); ScheduleRender(); });
            parent.Add(slider);
            return slider;
        }

        private void SetView(float newYaw, float newPitch)
        {
            yaw = newYaw; pitch = newPitch; zoom = 1f;
            yawSlider.SetValueWithoutNotify(yaw);
            pitchSlider.SetValueWithoutNotify(pitch);
            zoomSlider.SetValueWithoutNotify(zoom);
            ScheduleRender();
        }

        private void ScheduleRender() => renderRequest?.ExecuteLater(100);

        private void Render()
        {
            renderRequest?.Pause();
            if (status == null || currentImage == null || candidateImage == null) return;
            try
            {
                study ??= new LabWatercolorPaintStudy();
                study.HitDirection = LabHighResolutionPaintWindow.Direction(location);
                ClearFrames();
                frames = study.Render(repeats, unchecked((uint)seed), yaw, pitch, zoom, settings);
                if (frames == null || frames.Length != 2 || frames[0] == null || frames[1] == null)
                    throw new InvalidOperationException("The paint study did not return two comparison images.");
                currentImage.image = frames[0];
                candidateImage.image = frames[1];
                status.RemoveFromClassList("watercolor-error");
                LabPaintSurface surface = study.Surface;
                status.text = $"WATER / blue · {location} · first hit + {repeats} re-hit(s) · seed {seed} · "
                    + $"Host-rule fixture {surface.Coverage:P2} ({surface.CoveredCount:N0}/{surface.CellCount:N0} cells). "
                    + "Drying OFF only in this private fixture. Static preview only: no live game installation, "
                    + "network validation or spreading animation.";
                exportButton.SetEnabled(true);
            }
            catch (Exception exception)
            {
                ClearFrames();
                status.AddToClassList("watercolor-error");
                status.text = exception.Message;
                exportButton.SetEnabled(false);
                Debug.LogException(exception);
            }
        }

        private void Export()
        {
            if (frames == null || frames.Length != 2 || frames[0] == null || frames[1] == null) return;
            string path = EditorUtility.SaveFilePanel("Export watercolor comparison", string.Empty,
                $"watercolor-369-first-plus-{repeats}", "png");
            if (string.IsNullOrEmpty(path)) return;
            Texture2D comparison = null;
            try
            {
                comparison = LabPaintStudyRenderer.Combine(frames);
                File.WriteAllBytes(path, comparison.EncodeToPNG());
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception)
            {
                status.AddToClassList("watercolor-error");
                status.text = "Export failed: " + exception.Message;
                Debug.LogException(exception);
            }
            finally { if (comparison != null) Object.DestroyImmediate(comparison); }
        }

        private void ClearFrames()
        {
            if (currentImage != null) currentImage.image = null;
            if (candidateImage != null) candidateImage.image = null;
            if (frames == null) return;
            foreach (Texture2D frame in frames) if (frame != null) Object.DestroyImmediate(frame);
            frames = null;
        }

        private void OnDisable() => Release();

        private void Release()
        {
            renderRequest?.Pause(); renderRequest = null;
            ClearFrames();
            study?.Dispose(); study = null;
        }
    }
}
