using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hapio.Experiments
{
    public sealed partial class ClayPreviewController
    {
        public bool enableImageFilePreview;
        public Shader imageBackgroundShader;
        public TextAsset[] surfaceTextureSamples;
        public string[] surfaceTextureSampleNames;

        private ClayPreviewImagePresentation imagePresentation;
        private ClayPreviewImageImport.Result surfaceImage, backgroundImage;
        private Toggle textureToggle, backgroundFillToggle;
        private Slider textureScaleSlider, textureColorSlider, textureNormalSlider;
        private Label textureScaleValue, textureColorValue, textureNormalValue;
        private Label surfaceFileLabel, backgroundFileLabel, surfaceFileError, backgroundFileError;
        private Image surfaceThumbnail, backgroundThumbnail;
        private VisualElement textureControls;
        private string surfaceError, backgroundError;

        public bool ImageFilePreviewEnabled => enableImageFilePreview;
        public Texture2D SurfaceTextureImage => surfaceImage?.Texture;
        public Texture2D BackgroundTextureImage => backgroundImage?.Texture;
        public string SurfaceTextureFileName => surfaceImage?.FileName;
        public string BackgroundImageFileName => backgroundImage?.FileName;
        public string SurfaceTextureError => surfaceError;
        public string BackgroundImageError => backgroundError;
        public bool SurfaceTextureEnabled { get; private set; } = true;
        public float SurfaceTextureScale { get; private set; } = 1f;
        public float SurfaceTextureColorStrength { get; private set; } = .5f;
        public float SurfaceTextureNormalStrength { get; private set; } = .2f;
        public bool BackgroundFill { get; private set; } = true;

        private void InitializeImagePresentation()
        {
            if (!enableImageFilePreview) return;
            Require(imageBackgroundShader != null && imageBackgroundShader.isSupported,
                "Prepare the image-preview Scene and its background shader first.");
            imagePresentation = new ClayPreviewImagePresentation(ownedRoot.transform, previewCamera, imageBackgroundShader);
            ApplyImageSettings();
        }

        private void BuildImagesUi(VisualElement parent)
        {
            if (!enableImageFilePreview) return;
            var files = new Foldout { text = "FILES / IMAGES", value = true, name = "imageFilesFoldout" };
            files.AddToClassList("clay-image-files"); parent.Add(files);
            AddLabel(files, "PNG / JPG · max 2048 × 2048 · 16 MiB", "clay-status");
            AddLabel(files, "STICKER SURFACE", "clay-section-label");
            AddButton(files, "Choose texture file…", "chooseSurfaceTextureButton", PickSurfaceTexture).AddToClassList("clay-file-button");
            var samples = new VisualElement(); samples.AddToClassList("clay-button-row"); files.Add(samples);
            for (int i = 0; i < (surfaceTextureSamples?.Length ?? 0); i++)
            {
                int index = i;
                string label = surfaceTextureSampleNames != null && i < surfaceTextureSampleNames.Length
                    ? surfaceTextureSampleNames[i] : "Sample " + (i + 1);
                AddButton(samples, label, "textureSample" + i + "Button", () => LoadSurfaceTextureSample(index));
            }
            surfaceFileLabel = AddLabel(files, "No external texture", "clay-file-name");
            surfaceFileLabel.name = "surfaceTextureFileName";
            surfaceThumbnail = new Image { name = "surfaceTextureThumbnail", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            surfaceThumbnail.AddToClassList("clay-file-thumbnail"); files.Add(surfaceThumbnail);
            surfaceFileError = AddLabel(files, string.Empty, "clay-status"); surfaceFileError.name = "surfaceTextureError";
            surfaceFileError.AddToClassList("clay-error");
            textureControls = new VisualElement(); textureControls.name = "textureControls"; files.Add(textureControls);
            textureToggle = new Toggle("Apply surface texture") { name = "surfaceTextureToggle" };
            textureToggle.AddToClassList("clay-file-toggle"); textureControls.Add(textureToggle);
            textureToggle.RegisterValueChangedCallback(e => SetSurfaceTextureSettings(e.newValue, SurfaceTextureScale, SurfaceTextureColorStrength, SurfaceTextureNormalStrength));
            textureScaleSlider = AddSlider(textureControls, "PATTERN SCALE", "textureScaleSlider", .25f, 12, out textureScaleValue);
            textureColorSlider = AddSlider(textureControls, "GRAIN CONTRAST", "textureColorSlider", 0, 1, out textureColorValue);
            textureNormalSlider = AddSlider(textureControls, "SHALLOW RELIEF", "textureNormalSlider", 0, 1, out textureNormalValue);
            textureScaleSlider.RegisterValueChangedCallback(e => SetSurfaceTextureSettings(SurfaceTextureEnabled, e.newValue, SurfaceTextureColorStrength, SurfaceTextureNormalStrength));
            textureColorSlider.RegisterValueChangedCallback(e => SetSurfaceTextureSettings(SurfaceTextureEnabled, SurfaceTextureScale, e.newValue, SurfaceTextureNormalStrength));
            textureNormalSlider.RegisterValueChangedCallback(e => SetSurfaceTextureSettings(SurfaceTextureEnabled, SurfaceTextureScale, SurfaceTextureColorStrength, e.newValue));
            AddButton(files, "Clear texture", "clearSurfaceTextureButton", ClearSurfaceTexture).AddToClassList("clay-file-button");
            AddLabel(files, "BACKGROUND", "clay-section-label");
            AddButton(files, "Choose background file…", "chooseBackgroundButton", PickBackgroundImage).AddToClassList("clay-file-button");
            backgroundFileLabel = AddLabel(files, "Default studio background", "clay-file-name"); backgroundFileLabel.name = "backgroundFileName";
            backgroundThumbnail = new Image { name = "backgroundThumbnail", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            backgroundThumbnail.AddToClassList("clay-file-thumbnail"); files.Add(backgroundThumbnail);
            backgroundFillToggle = new Toggle("Fill view (crop); OFF = fit whole image") { name = "backgroundFillToggle" };
            backgroundFillToggle.AddToClassList("clay-file-toggle"); files.Add(backgroundFillToggle);
            backgroundFillToggle.RegisterValueChangedCallback(e => SetBackgroundFill(e.newValue));
            backgroundFileError = AddLabel(files, string.Empty, "clay-status"); backgroundFileError.name = "backgroundImageError";
            backgroundFileError.AddToClassList("clay-error");
            AddButton(files, "Clear background", "clearBackgroundButton", ClearBackgroundImage).AddToClassList("clay-file-button");
            RefreshImageUi();
        }

        public bool LoadSurfaceTextureFile(string path) { RequireImages(); return AcceptImportedImage(ClayPreviewImageImport.Load(path, ClayPreviewImageImport.Purpose.SurfaceTexture), true); }
        public bool LoadBackgroundImageFile(string path) { RequireImages(); return AcceptImportedImage(ClayPreviewImageImport.Load(path, ClayPreviewImageImport.Purpose.Background), false); }
        public bool LoadSurfaceTextureBytes(byte[] bytes, string displayName) { RequireImages(); return AcceptImportedImage(ClayPreviewImageImport.LoadBytes(bytes, displayName, ClayPreviewImageImport.Purpose.SurfaceTexture), true); }
        public bool LoadBackgroundImageBytes(byte[] bytes, string displayName) { RequireImages(); return AcceptImportedImage(ClayPreviewImageImport.LoadBytes(bytes, displayName, ClayPreviewImageImport.Purpose.Background), false); }
        public void PickSurfaceTexture()
        {
            RequireImages(); manipulation?.Cancel();
            AcceptImportedImage(ClayPreviewImageImport.PickAndLoad(ClayPreviewImageImport.Purpose.SurfaceTexture), true);
        }
        public void PickBackgroundImage()
        {
            RequireImages(); manipulation?.Cancel();
            AcceptImportedImage(ClayPreviewImageImport.PickAndLoad(ClayPreviewImageImport.Purpose.Background), false);
        }
        public bool LoadSurfaceTextureSample(int index)
        {
            RequireImages();
            if (surfaceTextureSamples == null || index < 0 || index >= surfaceTextureSamples.Length || surfaceTextureSamples[index] == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            string label = surfaceTextureSampleNames != null && index < surfaceTextureSampleNames.Length
                ? surfaceTextureSampleNames[index] : surfaceTextureSamples[index].name;
            return LoadSurfaceTextureBytes(surfaceTextureSamples[index].bytes, label + ".png");
        }
        private bool AcceptImportedImage(ClayPreviewImageImport.Result imported, bool surface)
        {
            if (!enableImageFilePreview) { imported?.Dispose(); throw new InvalidOperationException("Open the image-preview experiment first."); }
            if (imported == null) throw new ArgumentNullException(nameof(imported));
            if (imported.Cancelled) { imported.Dispose(); return false; }
            if (!imported.Succeeded)
            {
                if (surface) surfaceError = imported.Error; else backgroundError = imported.Error;
                imported.Dispose(); RefreshImageUi(); return false;
            }
            ClayPreviewImageImport.Result previous = surface ? surfaceImage : backgroundImage;
            bool previousEnabled = SurfaceTextureEnabled;
            try
            {
                if (surface) { surfaceImage = imported; SurfaceTextureEnabled = true; surfaceError = null; }
                else { backgroundImage = imported; backgroundError = null; }
                ApplyImageSettings(); RefreshImageUi();
                previous?.Dispose(); return true;
            }
            catch (Exception exception)
            {
                if (surface) { surfaceImage = previous; SurfaceTextureEnabled = previousEnabled; surfaceError = exception.Message; }
                else { backgroundImage = previous; backgroundError = exception.Message; }
                ApplyImageSettings(); imported.Dispose(); RefreshImageUi(); return false;
            }
        }
        public void SetSurfaceTextureSettings(bool enabled, float scale, float contrast, float relief)
        {
            RequireImages();
            if (!Finite(scale) || !Finite(contrast) || !Finite(relief)) throw new ArgumentOutOfRangeException("Texture values must be finite.");
            SurfaceTextureEnabled = enabled; SurfaceTextureScale = Mathf.Clamp(scale, .25f, 12);
            SurfaceTextureColorStrength = Mathf.Clamp01(contrast); SurfaceTextureNormalStrength = Mathf.Clamp01(relief);
            ApplyImageSettings(); RefreshImageUi();
        }
        public void SetBackgroundFill(bool fill)
        {
            RequireImages(); BackgroundFill = fill; ApplyImageSettings(); RefreshImageUi();
        }
        public void ClearSurfaceTexture()
        {
            RequireImages(); var previous = surfaceImage; surfaceImage = null; surfaceError = null;
            SurfaceTextureEnabled = true; SurfaceTextureScale = 1; SurfaceTextureColorStrength = .5f; SurfaceTextureNormalStrength = .2f;
            ApplyImageSettings(); RefreshImageUi(); previous?.Dispose();
        }
        public void ClearBackgroundImage()
        {
            RequireImages(); var previous = backgroundImage; backgroundImage = null; backgroundError = null;
            ApplyImageSettings(); RefreshImageUi(); previous?.Dispose();
        }
        private void RequireImages()
        {
            if (!enableImageFilePreview || imagePresentation == null) throw new InvalidOperationException("Open the image-preview experiment first.");
        }
        private void ApplyImageSettings()
        {
            if (!enableImageFilePreview || clayMaterial == null) return;
            ClayPreviewImagePresentation.ApplyStickerTexture(clayMaterial, SurfaceTextureImage, SurfaceTextureEnabled,
                SurfaceTextureScale, SurfaceTextureColorStrength, SurfaceTextureNormalStrength);
            if (imagePresentation == null) return;
            if (BackgroundTextureImage != null) imagePresentation.SetBackground(BackgroundTextureImage, BackgroundFill);
            else imagePresentation.ClearBackground();
        }
        private void RefreshImageUi()
        {
            if (surfaceFileLabel == null) return;
            surfaceFileLabel.text = SurfaceTextureFileName ?? "No external texture";
            backgroundFileLabel.text = BackgroundImageFileName ?? "Default studio background";
            surfaceThumbnail.image = SurfaceTextureImage; backgroundThumbnail.image = BackgroundTextureImage;
            surfaceThumbnail.style.display = SurfaceTextureImage != null ? DisplayStyle.Flex : DisplayStyle.None;
            backgroundThumbnail.style.display = BackgroundTextureImage != null ? DisplayStyle.Flex : DisplayStyle.None;
            surfaceFileError.text = surfaceError ?? string.Empty; backgroundFileError.text = backgroundError ?? string.Empty;
            surfaceFileError.style.display = string.IsNullOrEmpty(surfaceError) ? DisplayStyle.None : DisplayStyle.Flex;
            backgroundFileError.style.display = string.IsNullOrEmpty(backgroundError) ? DisplayStyle.None : DisplayStyle.Flex;
            textureControls.SetEnabled(SurfaceTextureImage != null); backgroundFillToggle.SetEnabled(BackgroundTextureImage != null);
            textureToggle.SetValueWithoutNotify(SurfaceTextureEnabled);
            textureScaleSlider.SetValueWithoutNotify(SurfaceTextureScale); textureColorSlider.SetValueWithoutNotify(SurfaceTextureColorStrength);
            textureNormalSlider.SetValueWithoutNotify(SurfaceTextureNormalStrength); backgroundFillToggle.SetValueWithoutNotify(BackgroundFill);
            textureScaleValue.text = SurfaceTextureScale.ToString("0.0", CultureInfo.InvariantCulture);
            textureColorValue.text = SurfaceTextureColorStrength.ToString("0.00", CultureInfo.InvariantCulture);
            textureNormalValue.text = SurfaceTextureNormalStrength.ToString("0.00", CultureInfo.InvariantCulture);
        }
        private void ReleaseImages()
        {
            imagePresentation?.Dispose(); imagePresentation = null;
            surfaceImage?.Dispose(); surfaceImage = null;
            backgroundImage?.Dispose(); backgroundImage = null;
            textureToggle = null; backgroundFillToggle = null; surfaceFileLabel = null; backgroundFileLabel = null;
        }
    }
}
