using UnityEngine;

namespace C6Lab
{
    public enum LabGourdMode { Paint = 0, Crack = 1 }

    /// <summary>
    /// Presentation only: paint approved elemental impacts on the Scene's white sphere gourd.
    /// The existing Host collision, HP, rewards, impact messages and hanging body remain authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdPaint : MonoBehaviour
    {
        public const int Capacity = 128;
        public const string ResourceFolder = "GourdPaint/";

        public LabTarget target;
        public LabNetwork network;
        public GameObject visualPrefab;
        public Material paintMaterial;
        [Range(.05f, .3f)] public float splatRadius = .15f;

        private readonly Vector4[] positions = new Vector4[Capacity];
        private readonly Vector4[] colors = new Vector4[Capacity];
        private MaterialPropertyBlock block;
        private Renderer shell;
        private LabTarget mountedTarget;
        private LabTarget subscribedTarget;
        private string roundSession;
        private uint round;
        private bool hasRoundContext;
        private bool initialized;
        private bool started;

        public int PaintCount { get; private set; }
        public Transform Shell => shell != null ? shell.transform : null;
        public GameObject VisualInstance { get; private set; }
        public string InitializationError { get; private set; }
        public bool IsReady => initialized && isActiveAndEnabled && target != null && target == mountedTarget
            && network != null && network.target == target && target.Hitbox != null
            && target.VisualMount != null && VisualInstance != null && target.MountedVisual == VisualInstance
            && shell != null && shell.enabled && shell.gameObject.activeInHierarchy && paintMaterial != null
            && shell.sharedMaterial == paintMaterial;

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
            paint.ApplyMode();
            return paint;
        }

        /// <summary>Called at initialization and accepted config boundaries, never from a frame poll.</summary>
        public bool ApplyMode()
        {
            if (network == null || target == null || network.target != target)
                return FailInitialization("The gourd mode needs its network and target.");
            bool usePaint = network.config == null || network.config.GourdMode == LabGourdMode.Paint;
            var cracks = target.GetComponents<LabGourdCrack>();
            var finishes = target.GetComponents<LabGourdFinish>();
            if (usePaint)
            {
                foreach (var finish in finishes) finish.enabled = false;
                foreach (var crack in cracks)
                {
                    crack.enabled = false;
                    crack.ReleasePresentation();
                }
                enabled = true;
                return InitializePresentation();
            }
            enabled = false;
            if (cracks.Length != 1 || finishes.Length != 1)
                return FailInitialization("Crack mode needs one crack and finish component on the target.");
            var selected = cracks[0];
            selected.enabled = true;
            bool ready = selected.Initialize();
            finishes[0].enabled = ready;
            InitializationError = ready ? null : selected.InitializationError;
            return ready;
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
            if (IsReady) return true;
            ReleasePresentation();
            if (!isActiveAndEnabled) return FailInitialization("The paint presentation is disabled.");
            if (network == null || target == null || network.target != target
                || target.Hitbox == null || target.VisualMount == null)
                return FailInitialization("The paint network, target, or target transforms are not ready.");
            if (visualPrefab == null || paintMaterial == null || paintMaterial.shader == null
                || paintMaterial.shader.name != "C6Lab/GourdPaint")
                return FailInitialization("The white gourd prefab or C6Lab/GourdPaint material is missing.");
            Transform prefabShell = visualPrefab.transform.Find("White Gourd");
            MeshFilter mesh = prefabShell != null ? prefabShell.GetComponent<MeshFilter>() : null;
            MeshRenderer prefabRenderer = prefabShell != null ? prefabShell.GetComponent<MeshRenderer>() : null;
            if (!visualPrefab.activeSelf || prefabShell == null || !prefabShell.gameObject.activeSelf
                || prefabRenderer == null || !prefabRenderer.enabled || mesh == null || mesh.sharedMesh == null)
                return FailInitialization("The white gourd prefab needs an active 'White Gourd' mesh renderer.");
            // Both presentations replace the same mount. A misconfigured Scene must not silently
            // race the crack/finish Start methods or destroy somebody else's active visual.
            foreach (var crack in target.GetComponents<LabGourdCrack>())
                if (crack.isActiveAndEnabled)
                    return FailInitialization("Disable the crack presentation before using the paint gourd.");
            foreach (var finish in target.GetComponents<LabGourdFinish>())
                if (finish.isActiveAndEnabled)
                    return FailInitialization("Disable the finish presentation before using the paint gourd.");
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
            shell.sharedMaterial = paintMaterial;
            block = new MaterialPropertyBlock();
            initialized = true;
            InitializationError = null;
            hasRoundContext = false;
            SynchronizeRound();
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
            if (IsReady) SynchronizeRound();
        }

        private void SynchronizeRound()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            string nextSession = snapshot != null ? snapshot.sessionId : null;
            uint nextRound = snapshot != null ? snapshot.round : 0;
            if (hasRoundContext && roundSession == nextSession && round == nextRound) return;
            hasRoundContext = true;
            roundSession = nextSession;
            round = nextRound;
            ClearPaint();
        }

        private void OnHit(Vector3 localPoint, LabOrbElement element)
        {
            if (!IsReady || !Finite(localPoint) || element < LabOrbElement.Fire || element > LabOrbElement.Earth)
                return;
            // A fresh snapshot and its first impact may arrive in the same frame. Clear the old
            // round before adding the mark, so Update cannot erase that first approved impact.
            SynchronizeRound();
            Vector3 world = target.Hitbox.transform.TransformPoint(localPoint);
            Vector3 direction = shell.transform.InverseTransformPoint(world).normalized;
            var appearance = network.config != null ? network.config.OrbAppearance : null;
            Paint(direction, appearance != null ? appearance.ElementColorFor(element)
                : LabOrbAppearance.DefaultElementColor(element));
        }

        public static Color Palette(int index) =>
            LabOrbAppearance.DefaultElementColor((LabOrbElement)(Mathf.Clamp(index, 0, 4) + 1));

        /// <summary>Sphere-local direction; keep the first 128 marks and ignore further marks.</summary>
        public bool Paint(Vector3 direction, Color color)
        {
            if (!IsReady || PaintCount >= Capacity || !Finite(direction) || direction.sqrMagnitude < .00001f)
                return false;
            Vector3 point = direction.normalized * .5f;
            float radius = float.IsNaN(splatRadius) || float.IsInfinity(splatRadius)
                ? .15f : Mathf.Clamp(splatRadius, .05f, .3f);
            positions[PaintCount] = new Vector4(point.x, point.y, point.z, radius);
            colors[PaintCount] = color;
            PaintCount++;
            Apply();
            return true;
        }

        public void ClearPaint()
        {
            PaintCount = 0;
            Apply();
        }

        private void Apply()
        {
            if (shell == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            shell.GetPropertyBlock(block);
            block.SetInt("_PaintCount", PaintCount);
            block.SetVectorArray("_PaintPositions", positions);
            block.SetVectorArray("_PaintColors", colors);
            shell.SetPropertyBlock(block);
        }

        private void ReleasePresentation()
        {
            if (subscribedTarget != null) subscribedTarget.ApprovedHit -= OnHit;
            subscribedTarget = null;
            ClearPaint();
            if (mountedTarget != null && VisualInstance != null && mountedTarget.MountedVisual == VisualInstance)
                mountedTarget.SetVisualPrefab(null);
            shell = null;
            VisualInstance = null;
            mountedTarget = null;
            initialized = false;
            hasRoundContext = false;
            roundSession = null;
            round = 0;
        }

        private static bool Finite(Vector3 p) =>
            !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
            && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        private void OnDisable() => ReleasePresentation();
        private void OnDestroy() => ReleasePresentation();
    }
}
