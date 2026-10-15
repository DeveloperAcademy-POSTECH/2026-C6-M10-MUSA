using UnityEngine;
namespace C6Lab
{
    /// <summary>Presentation only: approved shared HP determines cracks on every device.</summary>
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
            if (network == null || target == null || network.target != target
                || target.Hitbox == null || target.VisualMount == null
                || gourdPrefab == null || crackMaterial == null
                || !crackMaterial.HasProperty("_CrackStage"))
            {
                InitializationError = "The gourd network, target, prefab, or crack material is not ready.";
                return;
            }
            target.SetVisualPrefab(gourdPrefab);
            VisualInstance = target.MountedVisual;
            if (VisualInstance == null)
            {
                FailInitialization("The target did not mount a gourd instance.");
                return;
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
                return;
            }
            Bounds bounds = shells[0].bounds;
            foreach (var r in shells) bounds.Encapsulate(r.bounds);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (float.IsNaN(size) || float.IsInfinity(size) || size <= Mathf.Epsilon)
            {
                FailInitialization("The mounted gourd shell has no usable bounds.");
                return;
            }
            visual.localScale *= 1.8f / size;
            bounds = shells[0].bounds;
            foreach (var r in shells) bounds.Encapsulate(r.bounds);
            visual.position += target.Hitbox.bounds.center - bounds.center;
            // The shared hanging system supplies the cord; avoid an imported duplicate.
            foreach (var r in renderers)
                if (System.Array.Exists(r.sharedMaterials, m => m != null && m.name.Contains("Rope")))
                    r.enabled = false;
            initialized = true;
            InitializationError = null;
            SetStage(0);
        }
        void FailInitialization(string reason)
        {
            InitializationError = reason;
            // A malformed visual must not hide the original target or block victory forever.
            if (target != null && target.MountedVisual == VisualInstance) target.SetVisualPrefab(null);
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
        void OnDisable()
        {
            GetComponent<LabGourdFinish>()?.ResetPresentation();
            SetStage(0);
        }
    }
}
