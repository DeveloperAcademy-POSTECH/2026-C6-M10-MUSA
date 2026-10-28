using UnityEngine;

namespace C6Lab
{
    public enum LabGourdMode { Paint = 0 }

    /// <summary>
    /// Display the Host-owned cumulative paint surface on the white sphere. Network/model
    /// state survives visual disable/reinitialization; direct painting is an offline preview only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdPaint : MonoBehaviour
    {
        public const string ResourceFolder = "GourdPaint/";

        public LabTarget target;
        public LabNetwork network;
        public GameObject visualPrefab;
        public Material paintMaterial;
        public LabWatercolorPaintProfile watercolorProfile;
        [Tooltip("Offline preview brush only. Real session growth uses the Host round settings.")]
        [Range(.05f, .3f)] public float splatRadius = LabPaintSurface.DefaultRadius;

        private readonly LabPaintSurface localPreview = new LabPaintSurface();
        private Color32[] uploadPixels;
        private byte[] uploadPainterSlots;
        private Color32[] uploadPainterPixels;
        private Texture2D paintTexture;
        private Texture2D painterTexture;
        private LabHighResolutionPaintRuntime highResolution;
        private LabWatercolorPaintRuntime watercolor;
        private bool appliedWatercolorEnabled;
        private int appliedWatercolorSettings;
        private bool appliedHighResolutionEnabled;
        private int appliedHighResolutionColumns;
        private uint uploadedVisualVersion;
        private LabPaintSurface uploadedSurface;
        private uint uploadedRevision;
        private string uploadedSession;
        private uint uploadedRound;
        private bool hasUploadedSurface;
        private bool grainSettingsApplied;
        private bool appliedGrainEnabled;
        private bool appliedPlayerPaintPatternsEnabled;
        private float appliedGrainSize, appliedGrainStrength, appliedMottleStrength;
        private MaterialPropertyBlock block;
        private Renderer shell;
        private LabTarget mountedTarget;
        private LabTarget subscribedTarget;
        private bool initialized;
        private bool started;

        /// <summary>The accepted session surface, or an isolated preview before connecting.</summary>
        public LabPaintSurface Surface => network != null && network.PaintSurface != null
            ? network.PaintSurface : localPreview;
        public int PaintCount => Surface.PaintCount;
        public float Coverage => Surface.Coverage;
        public Texture2D PaintTexture => paintTexture;
        public Texture2D PainterTexture => painterTexture;
        /// <summary>Actually bound render maps. Authoritative Texture2D observation APIs stay unchanged.</summary>
        public Texture VisualPaintTexture => WatercolorActive ? watercolor.PaintTexture
            : HighResolutionActive ? highResolution.PaintTexture : paintTexture;
        public Texture VisualPainterTexture => HighResolutionActive ? highResolution.PainterTexture : painterTexture;
        public bool HighResolutionActive => appliedHighResolutionEnabled && highResolution != null && highResolution.Ready;
        public string HighResolutionError => highResolution != null ? highResolution.Error : null;
        public bool WatercolorActive => appliedWatercolorEnabled && HighResolutionActive
            && watercolor != null && watercolor.Ready;
        public string WatercolorError => watercolor != null ? watercolor.Error : null;
        /// <summary>True only after the final authoritative map and all bounded high-resolution batches are installed.</summary>
        public bool CompletionVisualReady => IsReady && hasUploadedSurface && uploadedSurface == Surface
            && uploadedRevision == Surface.Revision && uploadedVisualVersion == Surface.VisualVersion
            && uploadedSession == network.Snapshot?.sessionId && uploadedRound == network.Snapshot?.round
            && (network.config == null || !network.config.HighResolutionPaintEnabled
                || highResolution != null && highResolution.Ready && !highResolution.HasPendingWork && highResolution.Error == null)
            && (!appliedWatercolorEnabled || watercolor == null || watercolor.Error != null
                || watercolor.Ready && !watercolor.HasPendingWork && !watercolor.IsAnimating);
        public Transform Shell => shell != null ? shell.transform : null;
        public GameObject VisualInstance { get; private set; }
        public string InitializationError { get; private set; }
        public bool IsReady => initialized && isActiveAndEnabled && target != null && target == mountedTarget
            && network != null && network.target == target && target.Hitbox != null
            && target.VisualMount != null && VisualInstance != null && target.MountedVisual == VisualInstance
            && shell != null && shell.enabled && shell.gameObject.activeInHierarchy && paintMaterial != null
            && shell.sharedMaterial == paintMaterial && paintTexture != null && painterTexture != null;

        /// <summary>Bind the ordinary target before presentation Start methods run.</summary>
        public static LabGourdPaint Attach(LabNetwork network)
        {
            if (network == null || network.target == null) return null;
            var paint = network.target.GetComponent<LabGourdPaint>();
            if (paint == null) paint = network.target.gameObject.AddComponent<LabGourdPaint>();
            paint.target = network.target;
            paint.network = network;
            if (paint.visualPrefab == null)
                paint.visualPrefab = Resources.Load<GameObject>(ResourceFolder + "WhiteGourd");
            if (paint.paintMaterial == null)
                paint.paintMaterial = Resources.Load<Material>(ResourceFolder + "WhiteGourd");
            if (paint.watercolorProfile == null)
                paint.watercolorProfile = Resources.Load<LabWatercolorPaintProfile>(ResourceFolder + "WatercolorProfile");
            paint.ApplyMode();
            LabPaintFinishPresentation.Attach(network);
            return paint;
        }

        /// <summary>Called at initialization and accepted config boundaries, never from a frame poll.</summary>
        public bool ApplyMode()
        {
            if (network == null || target == null || network.target != target)
                return FailInitialization("The gourd mode needs its network and target.");
            enabled = true;
            return InitializePresentation();
        }

        private void Start()
        {
            started = true;
            InitializePresentation();
        }

        private void OnEnable()
        {
            if (started) InitializePresentation();
        }

        /// <summary>Initialize assigned references once; only the runtime binder resolves Resources.</summary>
        public bool InitializePresentation()
        {
            if (IsReady)
            {
                RefreshPaintMap();
                return true;
            }
            ReleasePresentation();
            if (!isActiveAndEnabled) return FailInitialization("The paint presentation is disabled.");
            if (network == null || target == null || network.target != target
                || target.Hitbox == null || target.VisualMount == null)
                return FailInitialization("The paint network, target, or target transforms are not ready.");
            if (visualPrefab == null || paintMaterial == null || paintMaterial.shader == null
                || paintMaterial.shader.name != "C6Lab/GourdPaint"
                || !paintMaterial.HasProperty("_PaintMap") || !paintMaterial.HasProperty("_PaintMapReady")
                || !paintMaterial.HasProperty("_PainterMap"))
                return FailInitialization("The white gourd prefab or C6Lab/GourdPaint material is missing.");
            Transform prefabShell = visualPrefab.transform.Find("White Gourd");
            MeshFilter mesh = prefabShell != null ? prefabShell.GetComponent<MeshFilter>() : null;
            MeshRenderer prefabRenderer = prefabShell != null ? prefabShell.GetComponent<MeshRenderer>() : null;
            if (!visualPrefab.activeSelf || prefabShell == null || !prefabShell.gameObject.activeSelf
                || prefabRenderer == null || !prefabRenderer.enabled || mesh == null || mesh.sharedMesh == null)
                return FailInitialization("The white gourd prefab needs an active 'White Gourd' mesh renderer.");
            if (target.MountedVisual != null)
                return FailInitialization("The target already owns another mounted visual.");

            mountedTarget = target;
            target.SetVisualPrefab(visualPrefab);
            VisualInstance = target.MountedVisual;
            Transform white = VisualInstance != null ? VisualInstance.transform.Find("White Gourd") : null;
            shell = white != null ? white.GetComponent<Renderer>() : null;
            if (shell == null)
                return FailInitialization("The target did not mount a usable white gourd shell.");
            foreach (var animator in VisualInstance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
            // 콤보 동안 요괴가 나와 있게 하는 표시 (#244). 박 표시와 함께 만들어지고 함께 사라진다.
            LabGourdYokai yokai = VisualInstance.GetComponent<LabGourdYokai>();
            if (yokai == null) yokai = VisualInstance.AddComponent<LabGourdYokai>();
            yokai.Bind(network);
            shell.sharedMaterial = paintMaterial;
            block = new MaterialPropertyBlock();
            EnsureTexture(Surface);
            initialized = true;
            if (watercolorProfile == null)
                watercolorProfile = Resources.Load<LabWatercolorPaintProfile>(ResourceFolder + "WatercolorProfile");
            ApplyVisualSize();
            if (!target.BindHitboxMesh(shell.GetComponent<MeshFilter>()))
                return FailInitialization("The visible white gourd shell could not supply its collision mesh.");
            InitializationError = null;
            RefreshPaintMap(true);
            subscribedTarget = target;
            subscribedTarget.ApprovedHit += OnHit;
            return true;
        }

        private bool FailInitialization(string reason)
        {
            ReleasePresentation();
            InitializationError = reason;
            return false;
        }

        private void Update()
        {
            if (IsReady)
            {
                ApplyVisualSize();
                RefreshPaintMap();
            }
        }

        private void ApplyVisualSize()
        {
            if (VisualInstance == null) return;
            float size = network != null && network.config != null ? network.config.GourdVisualSize : 1.4f;
            float ratio = size / 1.8f;
            Vector3 scale = Vector3.one * ratio;
            Vector3 position = Vector3.up * (.84f * (1f - ratio));
            if (VisualInstance.transform.localScale == scale
                && VisualInstance.transform.localPosition == position) return;
            VisualInstance.transform.localScale = scale;
            VisualInstance.transform.localPosition = position;
            target.RefreshBoundHitbox();
        }

        private void OnHit(Vector3 localPoint, LabOrbElement element)
        {
            if (!IsReady || !Finite(localPoint) || element < LabOrbElement.Fire || element > LabOrbElement.Earth)
                return;
            if (network != null && network.IsConnected)
            {
                // The Host model/client cache already accepted this mark before raising ApprovedHit.
                // Re-stamping here would duplicate color and make a missing renderer affect gameplay.
                RefreshPaintMap();
                return;
            }
            Vector3 world = target.Hitbox.transform.TransformPoint(localPoint);
            Vector3 direction = shell.transform.InverseTransformPoint(world).normalized;
            var appearance = network != null && network.config != null ? network.config.OrbAppearance : null;
            Paint(direction, appearance != null ? appearance.ElementColorFor(element)
                : LabOrbAppearance.DefaultElementColor(element));
        }

        public static Color Palette(int index) =>
            LabOrbAppearance.DefaultElementColor((LabOrbElement)(Mathf.Clamp(index, 0, 4) + 1));

        /// <summary>Offline preview only. Connected impacts must go through the Host approval path.</summary>
        public bool Paint(Vector3 direction, Color color)
        {
            if (!IsReady || (network != null && network.IsConnected)) return false;
            float radius = float.IsNaN(splatRadius) || float.IsInfinity(splatRadius)
                ? LabPaintSurface.DefaultRadius : Mathf.Clamp(splatRadius, .05f, .3f);
            bool accepted = Surface.Stamp(direction, color, radius);
            if (accepted) RefreshPaintMap();
            return accepted;
        }

        /// <summary>Clear offline preview fixtures; the network alone resets connected round state.</summary>
        public void ClearPaint()
        {
            if (network == null || !network.IsConnected) Surface.Clear();
            RefreshPaintMap();
        }

        /// <summary>Apply a network map/snapshot commit now, without waiting for component Update order.</summary>
        public void RefreshCommittedMap()
        {
            if (IsReady)
            {
                ApplyVisualSize();
                RefreshPaintMap();
            }
        }

        /// <summary>Finish the bounded brush rebuild before an independent result snapshot takes ownership.</summary>
        public void PrepareVisualCapture()
        {
            if (IsReady) RefreshPaintMap(false, true);
        }

        private void RefreshPaintMap(bool force = false, bool completeRebuild = false)
        {
            if (shell == null) return;
            LabPaintSurface source = Surface;
            EnsureTexture(source);
            string currentSession = network != null ? network.Snapshot?.sessionId : null;
            uint currentRound = network != null && network.Snapshot != null ? network.Snapshot.round : 0u;
            bool generationChanged = !hasUploadedSurface || uploadedSurface != source
                || uploadedSession != currentSession || uploadedRound != currentRound;
            bool uploadMap = force || !hasUploadedSurface || uploadedSurface != source
                || uploadedRevision != source.Revision || uploadedSession != currentSession || uploadedRound != currentRound;
            LabConfig config = network != null ? network.config : null;
            bool highResolutionEnabled = config == null || config.HighResolutionPaintEnabled;
            int highResolutionColumns = config != null ? config.HighResolutionPaintColumns : LabHighResolutionPaintRuntime.DefaultColumns;
            bool highResolutionChanged = appliedHighResolutionEnabled != highResolutionEnabled
                || appliedHighResolutionColumns != highResolutionColumns;
            bool visualChanged = uploadedVisualVersion != source.VisualVersion;
            bool pendingVisual = highResolution != null && (highResolution.HasPendingWork
                || (highResolution.Error == null && !highResolution.Ready));
            LabWatercolorPaintSettings watercolorSettings = watercolorProfile != null ? watercolorProfile.settings : null;
            bool watercolorEnabled = highResolutionEnabled && watercolorSettings != null && watercolorSettings.enabled;
            int watercolorSettingsHash = watercolorSettings != null ? watercolorSettings.Fingerprint : 0;
            bool watercolorChanged = appliedWatercolorEnabled != watercolorEnabled
                || appliedWatercolorSettings != watercolorSettingsHash;
            bool pendingWatercolor = watercolor != null && watercolor.Error == null
                && (watercolor.HasPendingWork || watercolor.IsAnimating || !watercolor.Ready);
            bool playerPaintPatternsEnabled = config == null || config.PlayerPaintPatternsEnabled;
            bool grainEnabled = config != null && config.PaintGrainEnabled;
            float grainSize = config != null ? config.PaintGrainSize : 1f;
            float grainStrength = config != null ? config.PaintGrainStrength : .3f;
            float mottleStrength = config != null ? config.PaintMottleStrength : .18f;
            bool grainChanged = !grainSettingsApplied || appliedGrainEnabled != grainEnabled
                || appliedGrainSize != grainSize || appliedGrainStrength != grainStrength
                || appliedMottleStrength != mottleStrength;
            bool patternsChanged = !grainSettingsApplied || appliedPlayerPaintPatternsEnabled != playerPaintPatternsEnabled;
            if (!uploadMap && !grainChanged && !patternsChanged && !highResolutionChanged
                && !visualChanged && !pendingVisual && !watercolorChanged && !pendingWatercolor && !completeRebuild) return;
            if (block == null) block = new MaterialPropertyBlock();
            shell.GetPropertyBlock(block);
            if (uploadMap)
            {
                source.CopyPixels(uploadPixels);
                source.CopyPainterSlots(uploadPainterSlots);
                for (int i = 0; i < uploadPainterSlots.Length; i++)
                    uploadPainterPixels[i] = new Color32(uploadPainterSlots[i], 0, 0, 255);
                paintTexture.SetPixels32(uploadPixels);
                paintTexture.Apply(false, false);
                painterTexture.SetPixels32(uploadPainterPixels);
                painterTexture.Apply(false, false);
                // Both maps describe the same committed surface. Bind together only after
                // both CPU uploads finish; an in-flight network map is never sampled here.
                block.SetFloat("_PaintMapReady", 1f);
                uploadedSurface = source;
                uploadedRevision = source.Revision;
                uploadedSession = currentSession; uploadedRound = currentRound;
                hasUploadedSurface = true;
            }
            if (highResolutionEnabled)
            {
                if (highResolution == null) highResolution = new LabHighResolutionPaintRuntime();
                highResolution.Update(source, paintTexture, painterTexture, highResolutionColumns, completeRebuild);
            }
            else if (highResolution != null)
            {
                highResolution.Dispose(); highResolution = null;
            }
            appliedHighResolutionEnabled = highResolutionEnabled;
            appliedHighResolutionColumns = highResolutionColumns;
            uploadedVisualVersion = source.VisualVersion;
            // A deliberate quality/profile change is also the retry boundary after a
            // presentation allocation failure. Quiet frames never retry automatically.
            if ((generationChanged || highResolutionChanged || watercolorChanged) && watercolor != null)
            {
                watercolor.Dispose(); watercolor = null;
            }
            if (watercolorEnabled && HighResolutionActive)
            {
                if (watercolor == null) watercolor = new LabWatercolorPaintRuntime();
                bool final = completeRebuild || network != null && network.Snapshot != null
                    && (network.Snapshot.phase == LabPhase.Victory || network.Snapshot.phase == LabPhase.Defeat);
                watercolor.Update(source, highResolution.PaintTexture, highResolution.PainterTexture,
                    watercolorSettings, Time.realtimeSinceStartupAsDouble, final);
            }
            else if ((!watercolorEnabled || highResolution == null) && watercolor != null)
            {
                watercolor.Dispose(); watercolor = null;
            }
            appliedWatercolorEnabled = watercolorEnabled;
            appliedWatercolorSettings = watercolorSettingsHash;
            block.SetTexture("_PaintMap", VisualPaintTexture);
            block.SetTexture("_PainterMap", VisualPainterTexture);
            block.SetFloat("_PaintGrainEnabled", grainEnabled ? 1f : 0f);
            block.SetFloat("_PaintGrainSize", grainSize);
            block.SetFloat("_PaintGrainStrength", grainStrength);
            block.SetFloat("_PaintMottleStrength", mottleStrength);
            // This is a presentation switch. Keep both authoritative maps intact while OFF.
            block.SetFloat("_PlayerPaintPatternsEnabled", playerPaintPatternsEnabled ? 1f : 0f);
            shell.SetPropertyBlock(block);
            appliedPlayerPaintPatternsEnabled = playerPaintPatternsEnabled;
            appliedGrainEnabled = grainEnabled; appliedGrainSize = grainSize;
            appliedGrainStrength = grainStrength; appliedMottleStrength = mottleStrength;
            grainSettingsApplied = true;
        }

        private void EnsureTexture(LabPaintSurface source)
        {
            if (paintTexture != null && painterTexture != null
                && paintTexture.width == source.Columns && paintTexture.height == source.Rows) return;
            if (paintTexture != null) LabVisualSafety.DestroyVisualObject(paintTexture);
            if (painterTexture != null) LabVisualSafety.DestroyVisualObject(painterTexture);
            uploadPixels = new Color32[source.CellCount];
            uploadPainterSlots = new byte[source.CellCount];
            uploadPainterPixels = new Color32[source.CellCount];
            paintTexture = new Texture2D(source.Columns, source.Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Gourd cumulative paint (runtime)", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            // Linear RGBA32 keeps the P byte exact on Metal and ordinary desktop GPUs.
            // Point sampling avoids inventing a P number at a boundary between players.
            painterTexture = new Texture2D(source.Columns, source.Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Gourd painter P numbers (runtime)", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            hasUploadedSurface = false;
            grainSettingsApplied = false;
        }

        private void ReleasePresentation()
        {
            if (subscribedTarget != null) subscribedTarget.ApprovedHit -= OnHit;
            subscribedTarget = null;
            // Never clear Surface here: disabling/rebuilding a renderer cannot erase team progress.
            if (mountedTarget != null && VisualInstance != null && mountedTarget.MountedVisual == VisualInstance)
                mountedTarget.SetVisualPrefab(null);
            if (highResolution != null) highResolution.Dispose();
            highResolution = null;
            if (watercolor != null) watercolor.Dispose();
            watercolor = null;
            appliedWatercolorEnabled = false;
            appliedWatercolorSettings = 0;
            appliedHighResolutionEnabled = false;
            appliedHighResolutionColumns = 0;
            uploadedVisualVersion = 0;
            shell = null;
            VisualInstance = null;
            mountedTarget = null;
            initialized = false;
            if (paintTexture != null) LabVisualSafety.DestroyVisualObject(paintTexture);
            if (painterTexture != null) LabVisualSafety.DestroyVisualObject(painterTexture);
            paintTexture = null;
            painterTexture = null;
            uploadPixels = uploadPainterPixels = null;
            uploadPainterSlots = null;
            uploadedSurface = null;
            hasUploadedSurface = false;
        }

        private static bool Finite(Vector3 p) =>
            !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
            && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        private void OnDisable() => ReleasePresentation();
        private void OnDestroy() => ReleasePresentation();
    }
}
