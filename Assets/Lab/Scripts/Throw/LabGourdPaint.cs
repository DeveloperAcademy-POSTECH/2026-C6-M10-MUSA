using UnityEngine;

namespace C6Lab
{
    public enum LabGourdMode { Paint = 0 }

    /// <summary>
    /// Display the Host-owned cumulative paint surface on the white sphere. Network/model
    /// state survives visual disable/reinitialization; direct painting is an offline preview only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class LabGourdPaint : MonoBehaviour
    {
        public const string ResourceFolder = "GourdPaint/";

        public LabTarget target;
        public LabNetwork network;
        public GameObject visualPrefab;
        public Material paintMaterial;
        public LabWatercolorPaintProfile watercolorProfile;
        public LabWatercolorStampProfile stampProfile;
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
        private LabWatercolorStampRuntime watercolorStamps;
        private bool appliedStampEnabled;
        private int appliedStampFingerprint;
        private ulong reportedStampApplied, reportedStampRendered;
        // Each runtime prewarms its round's brush once, while the battle is still behind the preparing overlay (#482).
        private object prewarmedHighResolution, prewarmedWatercolor;
        private LabPaintPattern prewarmedHighResolutionPattern, prewarmedWatercolorPattern;
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
        // #507 박 모델 계약. 흰 박(legacy)에서는 rig가 null, extras가 비어 있고 활성 재질은 paintMaterial이다.
        private LabGourdVisualRig rig;
        private Renderer[] extras = System.Array.Empty<Renderer>();
        private Material activeMaterial;
        private float rigReferenceSize = 1.8f;
        private Vector3 rigSizeAnchor;
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
        public Texture VisualPaintTexture => ClayStickersActive ? clayStickers.PaintTexture : WatercolorStampsActive ? watercolorStamps.PaintTexture : WatercolorActive ? watercolor.PaintTexture
            : HighResolutionActive ? highResolution.PaintTexture : paintTexture;
        public Texture VisualPainterTexture => HighResolutionActive ? highResolution.PainterTexture : painterTexture;
        public bool HighResolutionActive => appliedHighResolutionEnabled && highResolution != null && highResolution.Ready;
        public string HighResolutionError => highResolution != null ? highResolution.Error : null;
        public bool WatercolorActive => appliedWatercolorEnabled && HighResolutionActive
            && watercolor != null && watercolor.Ready;
        public bool WatercolorStampsActive => appliedStampEnabled && watercolorStamps != null
            && watercolorStamps.PaintTexture != null && watercolorStamps.Error == null;
        public string WatercolorStampError => clayStickers?.Error ?? watercolorStamps?.Error ?? network?.PaintArtError;
        public string WatercolorError => watercolor != null ? watercolor.Error : null;
        /// <summary>True only after the final authoritative map and all bounded high-resolution batches are installed.</summary>
        public bool CompletionVisualReady => IsReady && hasUploadedSurface && uploadedSurface == Surface
            && uploadedRevision == Surface.Revision && uploadedVisualVersion == Surface.VisualVersion
            && uploadedSession == network.Snapshot?.sessionId && uploadedRound == network.Snapshot?.round
            && (network.config == null || !network.config.HighResolutionPaintEnabled
                || highResolution != null && highResolution.Ready && !highResolution.HasPendingWork && highResolution.Error == null)
            && (!appliedWatercolorEnabled || watercolor == null || watercolor.Error != null
                || watercolor.Ready && !watercolor.HasPendingWork && !watercolor.IsAnimating)
            && (network == null || !network.RequiresPaintArt || network.PaintArtCompletionReady
                && (ClayStickersActive ? clayStickers.Ready && clayStickers.Error == null
                    : watercolorStamps != null && watercolorStamps.Ready && watercolorStamps.Error == null));
        public Transform Shell => shell != null ? shell.transform : null;
        /// <summary>마운트한 박의 모델 계약(#507). 흰 박이거나 계약 검사에 실패해 흰 박으로 돌아갔으면 null.</summary>
        public LabGourdVisualRig Rig => rig;
        /// <summary>몸통과 같은 칠 공간을 쓰는 추가 렌더러(#507). 흰 박에서는 비어 있다.</summary>
        public System.Collections.Generic.IReadOnlyList<Renderer> PaintExtras => extras;
        /// <summary>몸통에 쓰는 칠 재질. 흰 박은 paintMaterial, 박 모델 계약은 그 계약의 재질.</summary>
        public Material ActivePaintMaterial => activeMaterial != null ? activeMaterial : paintMaterial;
        /// <summary>요청한 박 모델 계약이 검사에 실패해 흰 박으로 돌아간 이유. 없으면 null.</summary>
        public string RigFallbackReason { get; private set; }
        public GameObject VisualInstance { get; private set; }
        public string InitializationError { get; private set; }
        public bool IsReady => initialized && isActiveAndEnabled && target != null && target == mountedTarget
            && network != null && network.target == target && target.Hitbox != null
            && target.VisualMount != null && VisualInstance != null && target.MountedVisual == VisualInstance
            && shell != null && shell.enabled && shell.gameObject.activeInHierarchy && ActivePaintMaterial != null
            && shell.sharedMaterial == ActivePaintMaterial && paintTexture != null && painterTexture != null;

        /// <summary>
        /// 스테이지 박(#507)을 <see cref="Attach"/>보다 먼저 지정한다. Attach는 비어 있을 때만 흰 박을 채운다.
        /// 박 모델 계약(<see cref="LabGourdVisualRig"/>)이 없는 프리팹은 흰 박과 같은 경로를 탄다.
        /// </summary>
        public static LabGourdPaint PrepareVisual(LabTarget target, GameObject prefab)
        {
            if (target == null) return null;
            var paint = target.GetComponent<LabGourdPaint>();
            if (paint == null) paint = target.gameObject.AddComponent<LabGourdPaint>();
            if (prefab == null) return paint;
            if (paint.VisualInstance != null && paint.visualPrefab != prefab)
                Debug.LogWarning("C6_GOURD_PREPARE_LATE prefab=" + prefab.name + " (already mounted; applies on the next mount)");
            paint.visualPrefab = prefab;
            return paint;
        }

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
            if (paint.stampProfile == null)
                paint.stampProfile = Resources.Load<LabWatercolorStampProfile>(ResourceFolder + "WatercolorStampProfile");
            paint.ApplyMode();
            LabPaintFinishPresentation.Attach(network);
            return paint;
        }

        /// <summary>
        /// The brush the round's stamps will use, for the prewarm (#482). The display replica keeps a default pattern,
        /// so read the round config as the Host's model does: territory turns the old round brush into splatter.
        /// </summary>
        public static LabPaintPattern RoundBrushPattern(LabConfig config, LabPaintSurface fallback)
        {
            if (config == null) return fallback != null ? fallback.Pattern : LabPaintPattern.Splatter;
            LabPaintPattern pattern = config.PaintPattern;
            return pattern == LabPaintPattern.Legacy && config.TeamTerritoryEnabled ? LabPaintPattern.Splatter : pattern;
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
            GameObject prefab = visualPrefab;
            RigFallbackReason = null;
            var prefabRig = prefab.GetComponent<LabGourdVisualRig>();
            if (prefabRig != null && !prefabRig.Validate(out string rigReason))
            {
                // The same build makes the same choice on every device, so hits stay consistent.
                RigFallbackReason = rigReason;
                Debug.LogWarning("C6_GOURD_RIG_FALLBACK prefab=" + prefab.name + " reason=" + rigReason);
                prefab = Resources.Load<GameObject>(ResourceFolder + "WhiteGourd");
                prefabRig = null;
                if (prefab == null) return FailInitialization("The white gourd fallback prefab is missing.");
            }
            if (prefabRig != null) return InitializeRig(prefab, prefabRig);
            Transform prefabShell = prefab.transform.Find("White Gourd");
            MeshFilter mesh = prefabShell != null ? prefabShell.GetComponent<MeshFilter>() : null;
            MeshRenderer prefabRenderer = prefabShell != null ? prefabShell.GetComponent<MeshRenderer>() : null;
            if (!prefab.activeSelf || prefabShell == null || !prefabShell.gameObject.activeSelf
                || prefabRenderer == null || !prefabRenderer.enabled || mesh == null || mesh.sharedMesh == null)
                return FailInitialization("The white gourd prefab needs an active 'White Gourd' mesh renderer.");
            if (target.MountedVisual != null)
                return FailInitialization("The target already owns another mounted visual.");

            mountedTarget = target;
            target.SetVisualPrefab(prefab);
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

        /// <summary>
        /// 박 모델 계약 경로(#507): 계약이 명시한 몸통·추가 렌더러에 칠하고, 판정 껍데기로 충돌 메시를 만든다.
        /// 순서는 흰 박 경로와 같다.
        /// </summary>
        private bool InitializeRig(GameObject prefab, LabGourdVisualRig prefabRig)
        {
            if (target.MountedVisual != null)
                return FailInitialization("The target already owns another mounted visual.");
            mountedTarget = target;
            target.SetVisualPrefab(prefab);
            VisualInstance = target.MountedVisual;
            rig = VisualInstance != null ? VisualInstance.GetComponent<LabGourdVisualRig>() : null;
            shell = rig != null ? rig.paintShell : null;
            if (shell == null || rig.hitShell == null)
                return FailInitialization("The target did not mount a usable gourd rig.");
            var list = new System.Collections.Generic.List<Renderer>();
            if (rig.paintExtras != null)
                foreach (Renderer extra in rig.paintExtras) if (extra != null) list.Add(extra);
            extras = list.ToArray();
            activeMaterial = prefabRig.paintMaterial;
            rigReferenceSize = prefabRig.referenceVisualSize;
            rigSizeAnchor = prefabRig.AnchorInRoot;
            foreach (var animator in VisualInstance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
            if (rig.yokaiAnimation != null)
            {
                LabGourdYokai yokai = VisualInstance.GetComponent<LabGourdYokai>();
                if (yokai == null) yokai = VisualInstance.AddComponent<LabGourdYokai>();
                yokai.Configure(rig.yokaiAnimation, rig.yokaiProfile);
                yokai.Bind(network);
            }
            shell.sharedMaterial = activeMaterial;
            // An extra with its own paint-shader material (its own body art under the paint) keeps it.
            foreach (Renderer extra in extras)
                if (!LabGourdVisualRig.UsesPaintShader(extra.sharedMaterial)) extra.sharedMaterial = activeMaterial;
            block = new MaterialPropertyBlock();
            EnsureTexture(Surface);
            initialized = true;
            if (watercolorProfile == null)
                watercolorProfile = Resources.Load<LabWatercolorPaintProfile>(ResourceFolder + "WatercolorProfile");
            ApplyVisualSize();
            if (!target.BindHitboxMesh(rig.hitShell))
                return FailInitialization("The gourd rig's hit shell could not supply its collision mesh.");
            // #507: a continuous rope ends at the model's rope socket instead of the hitbox top.
            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            if (hanging != null) hanging.SetRopeSocket(rig.ropeSocket);
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
            Vector3 scale, position;
            if (rig == null)
            {
                float ratio = size / 1.8f;
                scale = Vector3.one * ratio;
                position = Vector3.up * (.84f * (1f - ratio));
            }
            else
            {
                // #507: the rig names the size it was authored for and the point that stays put.
                float ratio = size / rigReferenceSize;
                scale = Vector3.one * ratio;
                position = rigSizeAnchor * (1f - ratio);
            }
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
            LabPaintPattern brushPattern = RoundBrushPattern(config, source);
            bool highResolutionChanged = appliedHighResolutionEnabled != highResolutionEnabled
                || appliedHighResolutionColumns != highResolutionColumns;
            bool visualChanged = uploadedVisualVersion != source.VisualVersion;
            bool pendingVisual = highResolution != null && (highResolution.HasPendingWork
                || (highResolution.Error == null && !highResolution.Ready));
            LabWatercolorPaintSettings watercolorSettings = watercolorProfile != null ? watercolorProfile.settings : null;
            // 2:2 땅따먹기 (#293)는 청·홍이 한눈에 갈려야 해서 반투명한 수채(#369) 대신 기존 선명한 고해상도 칠을 쓴다.
            bool teamTerritory = network != null && network.Snapshot != null && network.Snapshot.teamTerritory;
            LabWatercolorStampSettings stampSettings = network != null && network.PinnedWatercolorSettings != null
                ? network.PinnedWatercolorSettings : stampProfile != null ? stampProfile.settings.SanitizedCopy()
                : LabWatercolorStampSettings.CaptureDefault();
            bool stampsEnabled = network != null && network.IsConnected && highResolutionEnabled && !teamTerritory
                && stampSettings.enabled;
            bool stampsChanged = appliedStampEnabled != stampsEnabled || appliedStampFingerprint != stampSettings.Fingerprint;
            // The received journal can advance without a new cell revision (overlap, final commit, replay).
            bool pendingStamps = stampsEnabled && network.PaintArtJournal != null
                && (stampSettings.UsesClayStickers ? ClayPending(network.PaintArtJournal)
                    : watercolorStamps == null || watercolorStamps.Error == null
                    && (!watercolorStamps.Ready || watercolorStamps.AppliedThrough != network.PaintArtJournal.LastSequence
                        || reportedStampApplied != watercolorStamps.AppliedThrough
                        || reportedStampRendered != watercolorStamps.RenderedThrough));
            bool watercolorEnabled = !stampsEnabled && highResolutionEnabled && !teamTerritory
                && watercolorSettings != null && watercolorSettings.enabled;
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
                && !visualChanged && !pendingVisual && !watercolorChanged && !pendingWatercolor
                && !stampsChanged && !pendingStamps && !completeRebuild && !ClaySettingsChanged()) return;
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
                if (brushPattern != LabPaintPattern.Legacy
                    && (prewarmedHighResolution != highResolution || prewarmedHighResolutionPattern != brushPattern))
                {
                    if (highResolution.Prewarm(brushPattern) || highResolution.PrewarmError != null)
                    { prewarmedHighResolution = highResolution; prewarmedHighResolutionPattern = brushPattern; }
                }
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
                if (brushPattern != LabPaintPattern.Legacy
                    && (prewarmedWatercolor != watercolor || prewarmedWatercolorPattern != brushPattern))
                {
                    if (watercolor.Prewarm(brushPattern) || watercolor.PrewarmError != null)
                    { prewarmedWatercolor = watercolor; prewarmedWatercolorPattern = brushPattern; }
                }
            }
            else if ((!watercolorEnabled || highResolution == null) && watercolor != null)
            {
                watercolor.Dispose(); watercolor = null;
            }
            if ((generationChanged || highResolutionChanged || stampsChanged) && watercolorStamps != null)
            { watercolorStamps.Dispose(); watercolorStamps = null; }
            if ((generationChanged || highResolutionChanged || stampsChanged) && clayStickers != null) ReleaseClay();
            if (stampsEnabled && stampSettings.UsesClayStickers && network.PaintArtJournal != null)
            {
                UpdateClay(network.PaintArtJournal, stampSettings, highResolutionColumns, completeRebuild);
            }
            else if (stampsEnabled && network.PaintArtJournal != null)
            {
                watercolorStamps ??= new LabWatercolorStampRuntime();
                watercolorStamps.Update(network.PaintArtJournal, HighResolutionActive ? highResolution.PaintTexture : paintTexture, highResolutionColumns, stampSettings,
                    network.BattlePresentationTime, completeRebuild);
                if (watercolorStamps.Error != null || watercolorStamps.Ready)
                    network.ReportPaintArtRenderingError(watercolorStamps.Error);
                network.ReportPaintArtRendered(watercolorStamps.AppliedThrough, watercolorStamps.RenderedThrough);
                reportedStampApplied = watercolorStamps.AppliedThrough; reportedStampRendered = watercolorStamps.RenderedThrough;
            }
            else if ((!stampsEnabled || stampSettings.UsesClayStickers) && watercolorStamps != null)
            { watercolorStamps.Dispose(); watercolorStamps = null; }
            if (!stampsEnabled && clayStickers != null) ReleaseClay();
            appliedStampEnabled = stampsEnabled; appliedStampFingerprint = stampSettings.Fingerprint;
            appliedWatercolorEnabled = watercolorEnabled;
            appliedWatercolorSettings = watercolorSettingsHash;
            block.SetTexture("_PaintMap", VisualPaintTexture);
            block.SetTexture("_PainterMap", VisualPainterTexture);
            block.SetFloat("_WatercolorV2Enabled", WatercolorStampsActive && !stampSettings.UsesPaperStickers ? 1f : 0f);
            block.SetFloat("_PaperStickerEnabled", WatercolorStampsActive && stampSettings.UsesPaperStickers ? 1f : 0f);
            block.SetFloat("_PaperStickerGrainStrength", stampSettings.paperSticker.grainStrength);
            block.SetFloat("_WatercolorGranulationStrength", stampSettings.granulationStrength);
            block.SetFloat("_WatercolorGranulationScale", stampSettings.granulationScale);
            block.SetFloat("_PaintGrainEnabled", grainEnabled && !(WatercolorStampsActive && stampSettings.UsesPaperStickers) ? 1f : 0f);
            block.SetFloat("_PaintGrainSize", grainSize);
            block.SetFloat("_PaintGrainStrength", grainStrength);
            block.SetFloat("_PaintMottleStrength", mottleStrength);
            // This is a presentation switch. Keep both authoritative maps intact while OFF.
            block.SetFloat("_PlayerPaintPatternsEnabled", playerPaintPatternsEnabled ? 1f : 0f);
            // The neutral body and the clay coat are separate renderers. No saved base asset is edited.
            bool clayActive=ClayStickersActive;
            BindClay(clayActive,playerPaintPatternsEnabled);
            BindClayBase(shell,clayActive);
            foreach (Renderer extra in extras) if (extra != null) BindClayBase(extra,clayActive);
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
            if (rig != null && mountedTarget != null)
            {
                LabHangingTarget hanging = mountedTarget.GetComponent<LabHangingTarget>();
                if (hanging != null) hanging.SetRopeSocket(null);
            }
            if (mountedTarget != null && VisualInstance != null && mountedTarget.MountedVisual == VisualInstance)
                mountedTarget.SetVisualPrefab(null);
            ReleaseClay();
            if (highResolution != null) highResolution.Dispose();
            highResolution = null;
            if (watercolor != null) watercolor.Dispose();
            watercolor = null;
            watercolorStamps?.Dispose(); watercolorStamps = null;
            if (network != null) network.ReportPaintArtRendered(0, 0);
            appliedStampEnabled = false; appliedStampFingerprint = 0;
            reportedStampApplied = reportedStampRendered = 0;
            appliedWatercolorEnabled = false;
            appliedWatercolorSettings = 0;
            appliedHighResolutionEnabled = false;
            appliedHighResolutionColumns = 0;
            uploadedVisualVersion = 0;
            shell = null;
            rig = null;
            extras = System.Array.Empty<Renderer>();
            activeMaterial = null;
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
