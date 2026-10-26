using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public enum HighResolutionHitLocation { Front, LongitudeSeam, NorthPole }

    public sealed class LabHighResolutionPaintWindow : EditorWindow
    {
        [SerializeField] private int repeatHits = 3, seed = 123;
        [SerializeField] private LabOrbElement element = LabOrbElement.Water;
        [SerializeField] private HighResolutionHitLocation location;
        [SerializeField] private float yaw, pitch, zoom = 1f, density = .82f, mottle = .22f, grain = .25f;
        private LabPaintStudyRenderer renderer;
        private LabHighResolutionPaintBaker baker;
        private LabHighResolutionPaintFixture fixture;
        private readonly Texture2D[] frames = new Texture2D[3];
        private readonly Image[] images = new Image[3];
        private Label status;
        private SliderInt stageSlider;
        private Slider yawSlider, pitchSlider, zoomSlider;
        private Button export;

        [MenuItem("C6 Lab/Experiments/High Resolution Paint")]
        public static void Open()
        {
            var window = GetWindow<LabHighResolutionPaintWindow>();
            window.titleContent = new GUIContent("High Resolution Paint");
            window.minSize = new Vector2(1050, 610);
        }

        public void CreateGUI()
        {
            Release(); rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/HighResolutionPaint/LabHighResolutionPaint.uxml");
            if (layout == null) { rootVisualElement.Add(new Label("High-resolution comparison layout is missing.")); return; }
            layout.CloneTree(rootVisualElement);
            status = rootVisualElement.Q<Label>("status");
            var controls = rootVisualElement.Q<VisualElement>("controls");
            stageSlider = new SliderInt("Re-hits (after first)", 0, 3) { value = repeatHits, showInputField = true };
            stageSlider.RegisterValueChangedCallback(e => { repeatHits = e.newValue; Render(); });
            var elementField = new PopupField<LabOrbElement>("Element", new List<LabOrbElement> {
                LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth }, element);
            elementField.RegisterValueChangedCallback(e => { element = e.newValue; Rebuild(); });
            var seedField = new IntegerField("Fixed seed") { value = seed, isDelayed = true };
            seedField.RegisterValueChangedCallback(e => { seed = e.newValue; Rebuild(); });
            var locationField = new EnumField("Hit location", location);
            locationField.RegisterValueChangedCallback(e => {
                location = (HighResolutionHitLocation)e.newValue;
                yaw = location == HighResolutionHitLocation.LongitudeSeam ? 90 : 0;
                pitch = location == HighResolutionHitLocation.NorthPole ? 90 : 0;
                SetViewFields(); Rebuild();
            });
            yawSlider = MakeSlider("Yaw", -180, 180, yaw, v => { yaw = v; Render(); });
            pitchSlider = MakeSlider("Pitch", -90, 90, pitch, v => { pitch = v; Render(); });
            zoomSlider = MakeSlider("Zoom", .65f, 3f, zoom, v => { zoom = v; Render(); });
            var densitySlider = MakeSlider("Pigment density", .3f, 1f, density, v => { density = v; Render(); });
            var mottleSlider = MakeSlider("Density variation", 0, .6f, mottle, v => { mottle = v; Render(); });
            var grainSlider = MakeSlider("Fine grain", 0, 1f, grain, v => { grain = v; Render(); });
            foreach (var control in new VisualElement[] { stageSlider, elementField, seedField, locationField,
                yawSlider, pitchSlider, zoomSlider, densitySlider, mottleSlider, grainSlider })
            { control.AddToClassList("hires-control"); controls.Add(control); }
            string[] names = { "nativeImage", "mediumImage", "largeImage" };
            for (int i = 0; i < 3; i++) { images[i] = rootVisualElement.Q<Image>(names[i]); images[i].scaleMode = ScaleMode.ScaleToFit; }
            rootVisualElement.Q<Button>("nextHit").clicked += () => { stageSlider.value = repeatHits < 3 ? repeatHits + 1 : 0; };
            rootVisualElement.Q<Button>("resetView").clicked += () => { yaw = pitch = 0; zoom = 1; SetViewFields(); Render(); };
            export = rootVisualElement.Q<Button>("exportPng"); export.clicked += Export;
            Rebuild();
        }

        private static Slider MakeSlider(string label, float low, float high, float value, Action<float> changed)
        {
            var slider = new Slider(label, low, high) { value = value, showInputField = true };
            slider.RegisterValueChangedCallback(e => changed(e.newValue)); return slider;
        }
        private void SetViewFields()
        {
            yawSlider?.SetValueWithoutNotify(yaw); pitchSlider?.SetValueWithoutNotify(pitch); zoomSlider?.SetValueWithoutNotify(zoom);
        }
        private void Rebuild()
        {
            try
            {
                if (baker == null) baker = new LabHighResolutionPaintBaker();
                if (renderer == null) renderer = new LabPaintStudyRenderer(true);
                var config = Resources.Load<LabConfig>("LabConfig");
                if (config == null) throw new InvalidOperationException("Saved LabConfig missing.");
                var settings = config.PaintGrowthSettings;
                settings.DryingEnabled = false;
                fixture = new LabHighResolutionPaintFixture(settings, baker.Mask, Direction(location),
                    LabPaintPalette.ColorFor(element), unchecked((uint)seed));
                Render();
            }
            catch (Exception e) { Error(e); }
        }
        public static Vector3 Direction(HighResolutionHitLocation location) => location == HighResolutionHitLocation.LongitudeSeam
            ? Vector3.left : location == HighResolutionHitLocation.NorthPole ? Vector3.up : Vector3.forward;

        private void Render()
        {
            if (fixture == null || renderer == null || baker == null) return;
            RenderTexture medium = null, large = null;
            try
            {
                ClearFrames();
                frames[0] = renderer.RenderSurface(fixture.Stages[repeatHits], false, yaw, zoom, pitch);
                medium = baker.Bake(fixture, repeatHits, 1024, density, mottle, grain);
                large = baker.Bake(fixture, repeatHits, 2048, density, mottle, grain);
                frames[1] = renderer.RenderVisual(medium, yaw, zoom, pitch);
                frames[2] = renderer.RenderVisual(large, yaw, zoom, pitch);
                for (int i = 0; i < 3; i++) images[i].image = frames[i];
                status.RemoveFromClassList("hires-error");
                status.text = $"First hit + {repeatHits} re-hit(s) · Host fixture {fixture.Stages[repeatHits].Coverage:P2} " +
                    $"({fixture.Stages[repeatHits].CoveredCount:N0} cells) · seed {seed} · " +
                    $"first {fixture.InitialPercent}% / repeat {fixture.RepeatPercent}% · " +
                    "Drying OFF in this isolated fixture. High-res area is approximate; no gameplay or network validation.";
                export.SetEnabled(true);
            }
            catch (Exception e) { Error(e); }
            finally { LabHighResolutionPaintBaker.Release(medium); LabHighResolutionPaintBaker.Release(large); }
        }
        private void Export()
        {
            if (frames[0] == null || frames[1] == null || frames[2] == null) return;
            string path = EditorUtility.SaveFilePanel("Export current paint comparison", "", $"paint-hires-first-plus-{repeatHits}", "png");
            if (string.IsNullOrEmpty(path)) return;
            Texture2D sheet = null;
            try { sheet = LabPaintStudyRenderer.Combine(frames); File.WriteAllBytes(path, sheet.EncodeToPNG()); }
            catch (Exception e) { Error(e); }
            finally { if (sheet != null) Object.DestroyImmediate(sheet); }
        }
        private void Error(Exception e)
        {
            status?.AddToClassList("hires-error"); if (status != null) status.text = e.Message;
            export?.SetEnabled(false); Debug.LogException(e);
        }
        private void ClearFrames()
        {
            for (int i = 0; i < 3; i++) { if (images[i] != null) images[i].image = null;
                if (frames[i] != null) Object.DestroyImmediate(frames[i]); frames[i] = null; }
        }
        private void OnDisable() => Release();
        private void Release() { ClearFrames(); renderer?.Dispose(); baker?.Dispose(); renderer = null; baker = null; fixture = null; }
    }
}
