using UnityEngine;
namespace C6Lab
{
    /// <summary>Approved shared HP determines cracks; the visible shell also supplies collision geometry.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class LabGourdCrack : MonoBehaviour
    {
        public LabNetwork network;
        public LabTarget target;
        public GameObject gourdPrefab;
        public Material crackMaterial;
        Renderer[] shells;
        MaterialPropertyBlock properties;
        bool initialized;
        bool started;
        public GameObject VisualInstance { get; private set; }
        public string InitializationError { get; private set; }
        public bool IsReady => initialized && isActiveAndEnabled && network != null && target != null
            && network.target == target
            && target.Hitbox != null && target.VisualMount != null && crackMaterial != null
            && VisualInstance != null && target.MountedVisual == VisualInstance
            && shells != null && shells.Length > 0
            && System.Array.TrueForAll(shells, renderer => renderer != null);
        void Awake() => properties = new MaterialPropertyBlock();
        public int Stage { get; private set; }
        void Start()
        {
            started = true;
            Initialize();
        }
        void OnEnable()
        {
            if (started) Initialize();
        }
        /// <summary>Re-enter crack mode without duplicating its mounted visual.</summary>
        public bool Initialize()
        {
            if (IsReady) return true;
            ReleasePresentation();
            if (!isActiveAndEnabled)
            {
                InitializationError = "The crack presentation is disabled.";
                return false;
            }
            if (network == null || target == null || network.target != target
                || target.Hitbox == null || target.VisualMount == null
                || gourdPrefab == null || crackMaterial == null
                || !crackMaterial.HasProperty("_CrackStage"))
            {
                InitializationError = "The gourd network, target, prefab, or crack material is not ready.";
                return false;
            }
            if (target.MountedVisual != null)
            {
                InitializationError = "The target already owns another mounted visual.";
                return false;
            }
            target.SetVisualPrefab(gourdPrefab);
            VisualInstance = target.MountedVisual;
            if (VisualInstance == null)
            {
                FailInitialization("The target did not mount a gourd instance.");
                return false;
            }
            Transform visual = VisualInstance.transform;
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            var selected = new System.Collections.Generic.List<Renderer>();
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool shell = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.Contains("Shell"))
                    { mats[i] = crackMaterial; shell = true; }
                r.sharedMaterials = mats;
                if (shell) selected.Add(r);
            }
            shells = selected.ToArray();
            if (shells.Length == 0)
            {
                FailInitialization("The mounted gourd has no Shell renderer for the crack material.");
                return false;
            }
            Bounds bounds = shells[0].bounds;
            foreach (var r in shells) bounds.Encapsulate(r.bounds);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (float.IsNaN(size) || float.IsInfinity(size) || size <= Mathf.Epsilon)
            {
                FailInitialization("The mounted gourd shell has no usable bounds.");
                return false;
            }
            visual.localScale *= 1.8f / size;
            bounds = shells[0].bounds;
            foreach (var r in shells) bounds.Encapsulate(r.bounds);
            // Align to the stable hanging frame, not the previous mode's offset shell bounds.
            visual.position += target.VisualMount.position - bounds.center;
            var collisionShells = new System.Collections.Generic.List<MeshFilter>();
            foreach (var r in shells)
            {
                MeshFilter shellMesh = r.GetComponent<MeshFilter>();
                if (shellMesh == null || shellMesh.sharedMesh == null)
                {
                    FailInitialization("Every visible crack shell needs a collision mesh.");
                    return false;
                }
                collisionShells.Add(shellMesh);
            }
            if (!target.BindHitboxMeshes(collisionShells.ToArray()))
            {
                FailInitialization("The crack shells could not supply a combined collision mesh.");
                return false;
            }
            // The shared hanging system supplies the cord; avoid an imported duplicate.
            foreach (var r in renderers)
                if (System.Array.Exists(r.sharedMaterials, m => m != null && m.name.Contains("Rope")))
                    r.enabled = false;
            initialized = true;
            InitializationError = null;
            SetStage(0);
            return true;
        }
        void FailInitialization(string reason)
        {
            ReleasePresentation();
            InitializationError = reason;
        }
        /// <summary>Release only this component's visual before another mode mounts its own.</summary>
        public void ReleasePresentation()
        {
            GetComponent<LabGourdFinish>()?.ResetPresentation();
            SetStage(0);
            if (target != null && VisualInstance != null && target.MountedVisual == VisualInstance)
                target.SetVisualPrefab(null);
            VisualInstance = null;
            shells = null;
            initialized = false;
        }
        void Update()
        {
            var snapshot = network != null ? network.Snapshot : null;
            int stage = snapshot == null || string.IsNullOrEmpty(snapshot.sessionId) ? 0
                : StageForHp(snapshot.hp, network.config != null ? network.config.MonsterMaxHp : 0);
            SetStage(stage);
        }
        public static int StageForHp(int hp, int maxHp)
        {
            if (maxHp <= 0 || hp >= maxHp) return 0;
            long remaining = (long)Mathf.Max(0, hp) * 100;
            return remaining > (long)maxHp * 66 ? 1 : remaining > (long)maxHp * 33 ? 2 : 3;
        }
        public void SetStage(int stage)
        {
            Stage = Mathf.Clamp(stage, 0, 3);
            if (shells == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            foreach (var r in shells)
            {
                if (r == null) continue;
                r.GetPropertyBlock(properties);
                properties.SetFloat("_CrackStage", Stage);
                r.SetPropertyBlock(properties);
            }
        }
        void OnDisable() => ReleasePresentation();
        void OnDestroy() => ReleasePresentation();
    }
}
