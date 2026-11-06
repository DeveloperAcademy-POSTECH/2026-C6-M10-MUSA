using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hapio.Experiments
{
    /// <summary>The generated sample contract, shared by the Editor baker and the standalone Player.</summary>
    [Serializable]
    public class ClayPreviewSampleInfo
    {
        public const string FormatVersion = "clay-preview-sample-v2";
        public const string StyleVersion = "clay-stickers-v2";
        public const string ExperimentFormatVersion = "clay-preview-sample-v3";
        public const string ExperimentStyleVersion = "clay-stickers-v3";
        public const string CoatingFormatVersion = "clay-preview-sample-v4";
        public const string CoatingStyleVersion = "clay-stickers-v4";
        public const string HeightChannels = "R:physical-height;GB:coating-normal;A:actual-coverage";
        // GB stores the octahedral object-local normal premultiplied by actual coating A.
        public const string CoatingHeightChannels = "R:physical-height;GB:oct-object-normal;A:actual-coverage";

        public string format = FormatVersion;
        public string heightChannels = HeightChannels;
        public string unity, createdUtc, styleVersion, sourceConfigHash, authorityHash, recordHash;
        public string colorFileHash, heightFileHash, colorRawHash, heightRawHash, physicalHeightRawHash;
        public int seed = 123, width = 1024, height = 512, hits, cells, totalCells;
        public float colorRoundtripMaxError, heightRoundtripMaxError;
        public bool complete, sourceConfigPreserved, randomPreserved, scenesPreserved;

        public static string ContentHash(string json)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json ?? string.Empty)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        public static bool TryValidate(string json, string expectedMetadataHash, out ClayPreviewSampleInfo info, out string error)
        {
            info = null;
            try
            {
                info = JsonUtility.FromJson<ClayPreviewSampleInfo>(json);
                bool production = info != null && info.format == FormatVersion;
                bool experiment = info != null && info.format == ExperimentFormatVersion;
                bool coating = info != null && info.format == CoatingFormatVersion;
                if ((!production && !experiment && !coating) || info.heightChannels != (coating ? CoatingHeightChannels : HeightChannels))
                    throw new InvalidOperationException("The preview format and packed normal encoding do not match. Prepare the matching v2/v3/v4 sample.");
                if (string.IsNullOrEmpty(info.styleVersion) || (production
                    ? !info.styleVersion.StartsWith(StyleVersion + "/", StringComparison.Ordinal)
                    : info.styleVersion != (coating ? CoatingStyleVersion : ExperimentStyleVersion)))
                    throw new InvalidOperationException("The preview sample and clay shader style versions do not match.");
                if ((experiment || coating) && !IsHash(info.recordHash))
                    throw new InvalidOperationException("The experimental sample has no fixed artwork record hash.");
                if (!IsHash(expectedMetadataHash) || !string.Equals(ContentHash(json), expectedMetadataHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("The preview Scene is not bound to this generated sample metadata. Prepare the preview again.");
                if (!IsHash(info.colorFileHash) || !IsHash(info.heightFileHash) || !IsHash(info.colorRawHash)
                    || !IsHash(info.heightRawHash) || !IsHash(info.physicalHeightRawHash)
                    || !IsHash(info.sourceConfigHash) || !IsHash(info.authorityHash))
                    throw new InvalidOperationException("The preview sample hashes are missing or invalid.");
                if (info.width != 1024 || info.height != 512 || !info.complete || info.totalCells <= 0
                    || info.cells != info.totalCells || !info.sourceConfigPreserved || !info.randomPreserved || !info.scenesPreserved)
                    throw new InvalidOperationException("The preview metadata does not describe the preserved completed sample.");
                error = null;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public static bool IsHash(string value) => value != null && value.Length == 64
            && Array.TrueForAll(value.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }

    [Serializable]
    public sealed class ClayPreviewPreset
    {
        public string label;
        public Texture2D colorMap, heightMap;
        public TextAsset sampleInfo;
        public string sampleMetadataHash;
        public Shader shader;
        public bool improvedMaterial;
    }

    /// <summary>A local material study. It never creates a session, physics target or paint model.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ClayPreviewController : MonoBehaviour
    {
        public const float DefaultThickness = .01f;
        public const float DefaultGloss = .35f;
        public const float EnhancedThickness = .014f;
        public const float EnhancedGloss = .08f;
        public const float DefaultContact = .15f;
        public const float DefaultDetail = 1f;
        public const float DefaultLightAzimuth = -41.4914f;
        public const float DefaultLightElevation = 70.0484f;
        public const float DefaultLightIntensity = 1f;
        public const string OriginalShader = "C6Lab/ClayStickerSurface";
        public const string EnhancedShader = "C6Lab/Experiments/RoundedClayStickerSurface";
        public const string CoatingShader = "C6Lab/Experiments/StickerCoatingSurface";
        private const int PreviewLayer = 31;

        public GameObject sourcePrefab;
        public Texture2D colorMap, heightMap;
        public TextAsset sampleInfo;
        public string sampleMetadataHash;
        public Shader clayShader;
        public Font uiFont;
        public StyleSheet styleSheet;
        public ClayPreviewPreset[] presets;
        public int defaultPreset = 3;
        public ClayPreviewPaletteData paletteData;

        [Serializable] private sealed class PigmentRange
        { public float minimumDensity, maximumDensity; }

        private readonly List<Material> materials = new List<Material>();
        private GameObject ownedRoot;
        private Transform modelPivot;
        private MeshRenderer body;
        private Material clayMaterial;
        private Camera previewCamera;
        private Light previewLight;
        private RenderTexture target;
        private VisualElement uiRoot, viewport;
        private Image image;
        private Label thicknessValue, glossValue, contactValue, detailValue, status;
        private Label lightAzimuthValue, lightElevationValue, lightIntensityValue;
        private Slider thicknessSlider, glossSlider, contactSlider, detailSlider;
        private Slider lightAzimuthSlider, lightElevationSlider, lightIntensitySlider;
        private VisualElement extraControls;
        private readonly List<Button> presetButtons = new List<Button>();
        private readonly Color32[] paletteColors = new Color32[5];
        private readonly IntegerField[,] paletteFields = new IntegerField[5, 3];
        private readonly Image[] paletteSwatches = new Image[5];
        private readonly TextField[] paletteHexFields = new TextField[5];
        private readonly bool[] paletteHexInvalid = new bool[5];
        private Label paletteStatus;
        private ClayPreviewPaletteReplay paletteReplay;
        private ViewManipulator manipulation;
        private bool initialized, globalsCaptured, focused = true;
        private int previousTargetFrameRate, previousVSync;
        private bool previousRunInBackground;

        public float Thickness { get; private set; } = DefaultThickness;
        public float Gloss { get; private set; } = DefaultGloss;
        public float ContactStrength { get; private set; } = DefaultContact;
        public float DetailStrength { get; private set; } = DefaultDetail;
        public float LightAzimuth { get; private set; } = DefaultLightAzimuth;
        public float LightElevation { get; private set; } = DefaultLightElevation;
        public float LightIntensity { get; private set; } = DefaultLightIntensity;
        public Light PreviewLight => previewLight;
        public int ActivePreset { get; private set; }
        public int PresetCount => presets != null && presets.Length > 0 ? presets.Length : 1;
        public bool IsEnhanced => presets != null && presets.Length > 0 && presets[ActivePreset].improvedMaterial;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Zoom { get; private set; } = 1f;
        public Transform ModelRoot => modelPivot;
        public Material ClayMaterial => clayMaterial;
        public Camera PreviewCamera => previewCamera;
        public RenderTexture PreviewTexture => target;
        public bool IsReady => initialized && target != null && target.IsCreated();
        public string Error { get; private set; }
        public bool PaletteAvailable => paletteReplay != null;
        public bool PaletteReady => !PaletteAvailable || IsDefaultPalette || paletteReplay.Ready;
        public bool PaletteBusy => PaletteAvailable && !IsDefaultPalette && paletteReplay.Busy;
        public string PaletteError => paletteReplay?.Error;
        private bool IsDefaultPalette
        {
            get
            {
                if (!PaletteAvailable) return true;
                for (int i = 0; i < 5; i++)
                    if (!paletteColors[i].Equals(paletteData.sourceSrgb[i])) return false;
                return true;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            try
            {
                ValidateInputs();
                InitializePalette();
                SelectPreset(PresetCount == 1 ? 0 : defaultPreset);
                Require(sourcePrefab != null, "The gourd model is missing.");
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
                ReleasePalette();
                RestoreGlobals();
            }
        }

        private ClayPreviewSampleInfo ValidateMaps(Texture2D color, Texture2D height, TextAsset metadata,
            string metadataHash, Shader shader, int presetIndex)
        {
            Require(metadata != null, "The generated preview sample metadata is missing.");
            Require(ClayPreviewSampleInfo.TryValidate(metadata.text, metadataHash, out ClayPreviewSampleInfo info,
                out string metadataError), metadataError);
            string expectedFormat = presetIndex == 3 ? ClayPreviewSampleInfo.CoatingFormatVersion
                : presetIndex > 0 ? ClayPreviewSampleInfo.ExperimentFormatVersion : ClayPreviewSampleInfo.FormatVersion;
            Require(info.format == expectedFormat,
                "A preview preset's shader and packed sample versions do not match.");
            Require(color != null && height != null && shader != null, "The completed sample or clay shader is missing.");
            string expectedShader = presetIndex == 3 ? CoatingShader : presetIndex > 0 ? EnhancedShader : OriginalShader;
            Require(shader.name == expectedShader && shader.isSupported,
                "The preview preset's clay shader is unavailable or has a different contract.");
            Require(color.width == 1024 && color.height == 512 && height.width == 1024 && height.height == 512,
                "The sample maps must both be 1024 x 512.");
            Require(height.format == TextureFormat.RGBAHalf,
                "The packed physical height, coating normals and coverage require an RGBA Half EXR.");
            return info;
        }

        private void ValidateInputs()
        {
            if (presets == null || presets.Length == 0)
            {
                ValidateMaps(colorMap, heightMap, sampleInfo, sampleMetadataHash, clayShader, 0);
                return;
            }
            Require(presets.Length == 3 || presets.Length == 4, "The comparison requires three or four fully prepared presets.");
            Require(defaultPreset >= 0 && defaultPreset < presets.Length, "The default comparison preset is out of range.");
            ClayPreviewSampleInfo first = null;
            PigmentRange enhancedPigment = null;
            for (int i = 0; i < presets.Length; i++)
            {
                ClayPreviewPreset preset = presets[i];
                Require(preset != null && !string.IsNullOrWhiteSpace(preset.label), "A comparison preset is missing.");
                Require(preset.improvedMaterial == (i > 0), "Only the first preset may use the unchanged v2 material.");
                ClayPreviewSampleInfo info = ValidateMaps(preset.colorMap, preset.heightMap, preset.sampleInfo,
                    preset.sampleMetadataHash, preset.shader, i);
                Require(ClayPreviewSampleInfo.IsHash(info.recordHash), "Every comparison preset needs its fixed artwork record hash.");
                if (first == null) first = info;
                else Require(info.seed == first.seed && info.sourceConfigHash == first.sourceConfigHash
                    && info.authorityHash == first.authorityHash && info.recordHash == first.recordHash,
                    "Comparison presets must replay the same fixed artwork and game judgement.");
                if (i > 0)
                {
                    var pigment = JsonUtility.FromJson<PigmentRange>(preset.sampleInfo.text);
                    Require(pigment != null && Finite(pigment.minimumDensity) && Finite(pigment.maximumDensity)
                        && pigment.minimumDensity >= .1f && pigment.maximumDensity <= 1f && pigment.minimumDensity <= pigment.maximumDensity,
                        "The enhanced sample's pigment density range is missing or invalid.");
                    if (enhancedPigment == null) enhancedPigment = pigment;
                    else Require(pigment.minimumDensity == enhancedPigment.minimumDensity && pigment.maximumDensity == enhancedPigment.maximumDensity,
                        "Enhanced presets must use the same pigment density range.");
                }
            }
        }

        public void SelectPreset(int index)
        {
            if (index < 0 || index >= PresetCount) throw new ArgumentOutOfRangeException(nameof(index));
            ValidateInputs();
            if (presets != null && presets.Length > 0)
            {
                ClayPreviewPreset preset = presets[index];
                colorMap = preset.colorMap; heightMap = preset.heightMap;
                sampleInfo = preset.sampleInfo; sampleMetadataHash = preset.sampleMetadataHash;
                clayShader = preset.shader;
            }
            ActivePreset = index;
            SetAppearance(IsEnhanced ? EnhancedThickness : DefaultThickness, IsEnhanced ? EnhancedGloss : DefaultGloss);
            SetSurfaceDetails(DefaultContact, DefaultDetail);
            SetLighting(LightAzimuth, LightElevation, LightIntensity);
            RefreshPalettePreview();
            extraControls?.SetEnabled(IsEnhanced);
            for (int i = 0; i < presetButtons.Count; i++) presetButtons[i].EnableInClassList("clay-preset-active", i == index);
            if (status != null) status.text = (IsEnhanced ? "Enhanced material · " : "Unchanged v2 · ")
                + "fixed artwork\nDrag to rotate · scroll to zoom\n30 fps · no network session";
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
            previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.cullingMask = 1 << PreviewLayer;
            SetLighting(LightAzimuth, LightElevation, LightIntensity);
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
            var panel = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "materialPanel", horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            panel.AddToClassList("clay-panel");
            panel.contentContainer.AddToClassList("clay-panel-content");
            uiRoot.Add(panel);
            AddLabel(panel, "CLAY STUDY", "clay-heading");
            AddLabel(panel, "Same 5-color artwork\nContour + material comparison", "clay-subtitle");
            AddLabel(panel, "PRESET", "clay-section-label");
            presetButtons.Clear();
            string[] presetNames = { "originalPreset", "materialPreset", "roundedPreset", "coatingPreset" };
            for (int i = 0; i < PresetCount; i++)
            {
                int selected = i;
                Button button = AddButton(panel, presets != null && presets.Length > 0 ? presets[i].label : "Original v2",
                    presetNames[i], () => SelectPreset(selected));
                button.AddToClassList("clay-preset");
                presetButtons.Add(button);
            }
            if (PaletteAvailable) BuildPaletteUi(panel);
            thicknessSlider = AddSlider(panel, "THICKNESS", "thicknessSlider", 0, .02f, out thicknessValue);
            glossSlider = AddSlider(panel, "GLOSS", "glossSlider", 0, 1, out glossValue);
            thicknessSlider.RegisterValueChangedCallback(e => SetAppearance(e.newValue, Gloss));
            glossSlider.RegisterValueChangedCallback(e => SetAppearance(Thickness, e.newValue));
            extraControls = new VisualElement { name = "enhancedControls" };
            extraControls.AddToClassList("clay-extras"); panel.Add(extraControls);
            contactSlider = AddSlider(extraControls, "CONTACT SHADE", "contactSlider", 0, .5f, out contactValue);
            detailSlider = AddSlider(extraControls, "FINE DETAIL", "detailSlider", 0, 1, out detailValue);
            contactSlider.RegisterValueChangedCallback(e => SetSurfaceDetails(e.newValue, DetailStrength));
            detailSlider.RegisterValueChangedCallback(e => SetSurfaceDetails(ContactStrength, e.newValue));
            AddLabel(panel, "LIGHT", "clay-section-label").name = "lightingHeading";
            lightAzimuthSlider = AddSlider(panel, "LEFT / RIGHT", "lightAzimuthSlider", -180, 180, out lightAzimuthValue);
            lightElevationSlider = AddSlider(panel, "UP / DOWN", "lightElevationSlider", -80, 80, out lightElevationValue);
            lightIntensitySlider = AddSlider(panel, "LIGHT INTENSITY", "lightIntensitySlider", 0, 2, out lightIntensityValue);
            lightAzimuthSlider.RegisterValueChangedCallback(e => SetLighting(e.newValue, LightElevation, LightIntensity));
            lightElevationSlider.RegisterValueChangedCallback(e => SetLighting(LightAzimuth, e.newValue, LightIntensity));
            lightIntensitySlider.RegisterValueChangedCallback(e => SetLighting(LightAzimuth, LightElevation, e.newValue));
            AddButton(panel, "Reset light", "resetLightButton", ResetLighting).AddToClassList("clay-primary");
            AddLabel(panel, "VIEW", "clay-section-label");
            AddViewRow(panel, new[] { "Front", "Right", "Back" }, new[] { "frontView", "rightView", "backView" }, new[] { 0f, 90f, 180f }, new[] { 0f, 0f, 0f });
            AddViewRow(panel, new[] { "Left", "Diagonal", "Above" }, new[] { "leftView", "diagonalView", "upperView" }, new[] { -90f, 45f, 45f }, new[] { 0f, 0f, 40f });
            Button reset = AddButton(panel, "Reset preset", "resetButton", ResetViewAndAppearance);
            reset.AddToClassList("clay-primary");
            Button quit = AddButton(panel, "Quit", "quitButton", () => Application.Quit());
            quit.AddToClassList("clay-quit");
            var spacer = new VisualElement(); spacer.AddToClassList("clay-spacer"); panel.Add(spacer);
            status = AddLabel(panel, string.Empty, "clay-status");
            SelectPreset(ActivePreset);
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

        private void InitializePalette()
        {
            if (paletteData == null) return;
            Require(presets != null && presets.Length == 4 && paletteData.presets != null && paletteData.presets.Length == 4,
                "Prepare the four-preset palette experiment first.");
            Require(paletteData.sourceSrgb != null && paletteData.sourceSrgb.Length == 5, "The five source palette colors are missing.");
            for (int i = 0; i < 4; i++)
                Require(paletteData.presets[i] != null && paletteData.presets[i].sampleMetadataHash == presets[i].sampleMetadataHash,
                    "The palette record and frozen preset metadata differ. Prepare Palette Preview again.");
            paletteReplay = new ClayPreviewPaletteReplay(paletteData);
            Require(string.IsNullOrEmpty(paletteReplay.Error), paletteReplay.Error ?? "The frozen palette record is unavailable.");
            for (int i = 0; i < 5; i++) paletteColors[i] = paletteData.sourceSrgb[i];
        }

        private void BuildPaletteUi(VisualElement parent)
        {
            var foldout = new Foldout { text = "PALETTE · RGB / HEX", value = true, name = "paletteFoldout" };
            foldout.AddToClassList("clay-palette"); parent.Add(foldout);
            AddLabel(foldout, "Base colors · lighting and density still apply", "clay-status");
            string[] elements = { "화 / FIRE", "수 / WATER", "목 / WOOD", "금 / METAL", "토 / EARTH" };
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var heading = new VisualElement(); heading.AddToClassList("clay-field-heading"); foldout.Add(heading);
                AddLabel(heading, elements[i], "clay-field-label");
                var hex = new TextField("HEX") { name = "palette" + i + "HexField", isDelayed = true,
                    tooltip = "RRGGBB, for example D85736. A leading # is optional." };
                hex.AddToClassList("clay-hex-field"); heading.Add(hex); paletteHexFields[i] = hex;
                hex.RegisterValueChangedCallback(evt =>
                {
                    if (TrySetPaletteHex(index, evt.newValue)) return;
                    paletteHexInvalid[index] = true; hex.AddToClassList("clay-hex-invalid"); RefreshPaletteStatus();
                });
                var row = new VisualElement(); row.AddToClassList("clay-palette-row"); foldout.Add(row);
                paletteSwatches[i] = new Image { name = "palette" + i + "Swatch", image = Texture2D.whiteTexture, pickingMode = PickingMode.Ignore };
                paletteSwatches[i].AddToClassList("clay-palette-swatch"); row.Add(paletteSwatches[i]);
                for (int channel = 0; channel < 3; channel++)
                {
                    string label = new[] { "R", "G", "B" }[channel];
                    var field = new IntegerField(label) { name = "palette" + i + label + "Field", isDelayed = true };
                    field.AddToClassList("clay-rgb-field"); row.Add(field); paletteFields[i, channel] = field;
                    field.RegisterValueChangedCallback(evt => SetPaletteRgb(index,
                        paletteFields[index, 0].value, paletteFields[index, 1].value, paletteFields[index, 2].value));
                }
                RefreshPaletteFields(i);
            }
            AddButton(foldout, "Reset palette", "resetPaletteButton", ResetPalette).AddToClassList("clay-primary");
            paletteStatus = AddLabel(foldout, string.Empty, "clay-status"); paletteStatus.name = "paletteStatus";
        }

        public Color32 GetPaletteSrgb(int slot)
        {
            RequirePaletteSlot(slot); return paletteColors[slot];
        }
        public Color GetPaletteLinear(int slot) => ((Color)GetPaletteSrgb(slot)).linear;
        private void RequirePaletteSlot(int slot)
        {
            if (slot < 0 || slot >= 5) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!PaletteAvailable) throw new InvalidOperationException("Prepare Palette Preview before editing colors.");
        }
        public void SetPaletteRgb(int slot, int red, int green, int blue)
        {
            RequirePaletteSlot(slot);
            paletteColors[slot] = new Color32((byte)Mathf.Clamp(red, 0, 255), (byte)Mathf.Clamp(green, 0, 255), (byte)Mathf.Clamp(blue, 0, 255), 255);
            RefreshPaletteFields(slot); RefreshPalettePreview();
        }
        public bool TrySetPaletteHex(int slot, string hex)
        {
            RequirePaletteSlot(slot);
            string value = hex?.Trim();
            if (!string.IsNullOrEmpty(value) && value[0] == '#') value = value.Substring(1);
            if (value == null || value.Length != 6) return false;
            foreach (char character in value)
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f') && !(character >= 'A' && character <= 'F')) return false;
            if (!ColorUtility.TryParseHtmlString("#" + value, out Color parsed)) return false;
            Color32 color = parsed;
            SetPaletteRgb(slot, color.r, color.g, color.b);
            return true;
        }

        public void ResetPalette()
        {
            if (!PaletteAvailable) return;
            for (int i = 0; i < 5; i++) { paletteColors[i] = paletteData.sourceSrgb[i]; RefreshPaletteFields(i); }
            RefreshPalettePreview();
        }
        private void RefreshPaletteFields(int slot)
        {
            Color32 color = paletteColors[slot];
            paletteFields[slot, 0]?.SetValueWithoutNotify(color.r);
            paletteFields[slot, 1]?.SetValueWithoutNotify(color.g);
            paletteFields[slot, 2]?.SetValueWithoutNotify(color.b);
            if (paletteSwatches[slot] != null) paletteSwatches[slot].tintColor = (Color)color;
            paletteHexInvalid[slot] = false;
            if (paletteHexFields[slot] != null)
            {
                paletteHexFields[slot].SetValueWithoutNotify(ColorUtility.ToHtmlStringRGB(color));
                paletteHexFields[slot].RemoveFromClassList("clay-hex-invalid");
            }
        }
        private void RefreshPalettePreview()
        {
            if (!PaletteAvailable || IsDefaultPalette)
            {
                paletteReplay?.Clear(); BindCurrentPreset();
            }
            else
            {
                paletteReplay.Request(ActivePreset, (Color32[])paletteColors.Clone());
                if (paletteReplay.Ready) BindCurrentPreset();
            }
            RefreshPaletteStatus();
        }
        private void Update()
        {
            if (!initialized || !PaletteAvailable || IsDefaultPalette) return;
            paletteReplay.Tick();
            if (paletteReplay.Ready && clayMaterial != null &&
                (clayMaterial.GetTexture("_PaintMap") != paletteReplay.PaintTexture || clayMaterial.shader != clayShader)) BindCurrentPreset();
            RefreshPaletteStatus();
        }
        private void RefreshPaletteStatus()
        {
            if (paletteStatus == null) return;
            bool invalidHex = false;
            for (int i = 0; i < 5; i++) invalidHex |= paletteHexInvalid[i];
            paletteStatus.EnableInClassList("clay-error", invalidHex || !string.IsNullOrEmpty(PaletteError));
            paletteStatus.text = invalidHex ? "HEX needs 6 digits: 0–9, A–F. Previous color is kept."
                : !string.IsNullOrEmpty(PaletteError) ? "Palette preview: " + PaletteError
                : PaletteBusy ? "Updating colors…" : "Enter or leave a field to apply · preview only";
        }
        private void BindCurrentPreset()
        {
            if (clayMaterial == null) return;
            clayMaterial.shader = clayShader;
            clayMaterial.SetTexture("_PaintMap", PaletteAvailable && !IsDefaultPalette && paletteReplay.Ready ? paletteReplay.PaintTexture : colorMap);
            clayMaterial.SetTexture("_HeightMap", heightMap);
            clayMaterial.SetTexture("_PainterMap", Texture2D.blackTexture);
            clayMaterial.SetFloat("_PaintMapReady", 1f);
            clayMaterial.SetFloat("_PlayerPaintPatternsEnabled", 0f);
            if (clayMaterial.HasProperty("_SpecularOnly")) clayMaterial.SetFloat("_SpecularOnly", 0f);
            SetAppearance(Thickness, Gloss); SetSurfaceDetails(ContactStrength, DetailStrength);
            SetLighting(LightAzimuth, LightElevation, LightIntensity);
        }
        private void ReleasePalette()
        {
            paletteReplay?.Dispose(); paletteReplay = null;
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

        public void SetSurfaceDetails(float contact, float detail)
        {
            if (!Finite(contact) || !Finite(detail)) throw new ArgumentOutOfRangeException("Surface detail values must be finite.");
            ContactStrength = Mathf.Clamp(contact, 0, .5f);
            DetailStrength = Mathf.Clamp01(detail);
            if (clayMaterial != null && IsEnhanced)
            {
                clayMaterial.SetFloat("_ClayContactStrength", ContactStrength);
                clayMaterial.SetFloat("_ClayDetailStrength", DetailStrength);
            }
            contactSlider?.SetValueWithoutNotify(ContactStrength);
            detailSlider?.SetValueWithoutNotify(DetailStrength);
            if (contactValue != null) contactValue.text = ContactStrength.ToString("0.00", CultureInfo.InvariantCulture);
            if (detailValue != null) detailValue.text = DetailStrength.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public void SetLighting(float azimuth, float elevation, float intensity)
        {
            if (!Finite(azimuth) || !Finite(elevation) || !Finite(intensity))
                throw new ArgumentOutOfRangeException("Lighting values must be finite.");
            LightAzimuth = Mathf.Clamp(azimuth, -180f, 180f);
            LightElevation = Mathf.Clamp(elevation, -80f, 80f);
            LightIntensity = Mathf.Clamp(intensity, 0f, 2f);
            Quaternion rotation = Quaternion.Euler(LightElevation, LightAzimuth - 180f, 0f);
            Vector3 direction = -(rotation * Vector3.forward);
            if (previewLight != null)
            {
                previewLight.transform.rotation = rotation;
                previewLight.intensity = LightIntensity;
            }
            if (clayMaterial != null)
            {
                clayMaterial.SetFloat("_ClayPreviewLighting", 1f);
                clayMaterial.SetVector("_ClayPreviewLightDirection", new Vector4(direction.x, direction.y, direction.z, 0f));
                clayMaterial.SetFloat("_ClayPreviewLightIntensity", LightIntensity);
            }
            lightAzimuthSlider?.SetValueWithoutNotify(LightAzimuth);
            lightElevationSlider?.SetValueWithoutNotify(LightElevation);
            lightIntensitySlider?.SetValueWithoutNotify(LightIntensity);
            if (lightAzimuthValue != null) lightAzimuthValue.text = LightAzimuth.ToString("0", CultureInfo.InvariantCulture) + "°";
            if (lightElevationValue != null) lightElevationValue.text = LightElevation.ToString("0", CultureInfo.InvariantCulture) + "°";
            if (lightIntensityValue != null) lightIntensityValue.text = LightIntensity.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public void ResetLighting() => SetLighting(DefaultLightAzimuth, DefaultLightElevation, DefaultLightIntensity);

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
            SetAppearance(IsEnhanced ? EnhancedThickness : DefaultThickness, IsEnhanced ? EnhancedGloss : DefaultGloss);
            SetSurfaceDetails(DefaultContact, DefaultDetail);
            ResetLighting();
            ResetPalette();
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
            ReleasePalette();
            RestoreGlobals();
        }

        private void ReleasePresentation()
        {
            ReleaseTarget();
            if (ownedRoot != null) DestroyOwned(ownedRoot);
            foreach (Material material in materials) if (material != null) DestroyOwned(material);
            materials.Clear(); ownedRoot = null; modelPivot = null; body = null; clayMaterial = null; previewCamera = null; previewLight = null;
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
