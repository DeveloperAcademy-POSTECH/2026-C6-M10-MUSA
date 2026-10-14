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
        void Awake() => properties = new MaterialPropertyBlock();
        public int Stage { get; private set; }
        void Start()
        {
            if (target == null || gourdPrefab == null || crackMaterial == null) return;
            target.SetVisualPrefab(gourdPrefab);
            Transform visual = target.VisualMount.GetChild(target.VisualMount.childCount - 1);
            foreach (var animator in visual.GetComponentsInChildren<Animator>()) animator.enabled = false;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
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
            if (shells.Length > 0)
            {
                Bounds bounds = shells[0].bounds;
                foreach (var r in shells) bounds.Encapsulate(r.bounds);
                visual.localScale *= 1.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                bounds = shells[0].bounds;
                foreach (var r in shells) bounds.Encapsulate(r.bounds);
                visual.position += target.Hitbox.bounds.center - bounds.center;
            }
            // The shared hanging system supplies the cord; avoid an imported duplicate.
            foreach (var r in renderers)
                if (System.Array.Exists(r.sharedMaterials, m => m != null && m.name.Contains("Rope")))
                    r.enabled = false;
            SetStage(0);
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
            foreach (var r in shells)
            {
                r.GetPropertyBlock(properties);
                properties.SetFloat("_CrackStage", Stage);
                r.SetPropertyBlock(properties);
            }
        }
    }
}
