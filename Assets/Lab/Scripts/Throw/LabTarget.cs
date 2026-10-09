using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The visible Unity Cylinder mesh is also the exact static hitbox mesh. A later art prefab
    /// may be mounted separately without changing this collider or any combat rule.
    /// </summary>
    public sealed class LabTarget : MonoBehaviour
    {
        [SerializeField] private MeshCollider hitbox;
        [SerializeField] private MeshRenderer cylinderRenderer;
        [SerializeField] private Transform visualMount;
        private GameObject replacementVisual;

        public MeshCollider Hitbox => hitbox;
        public Transform VisualMount => visualMount;

        public static LabTarget CreateCylinder(Transform parent, Vector3 center,
            float radius, float height)
        {
            GameObject root = new GameObject("Cylinder Target");
            if (parent != null) root.transform.SetParent(parent, false);
            LabTarget target = root.AddComponent<LabTarget>();
            target.Configure(center, radius, height);
            return target;
        }

        public void Configure(Vector3 center, float radius, float height)
        {
            EnsureCylinder();
            transform.position = center;
            hitbox.transform.localPosition = Vector3.zero;
            hitbox.transform.localRotation = Quaternion.identity;
            hitbox.transform.localScale = new Vector3(Mathf.Max(0.001f, radius) * 2f,
                Mathf.Max(0.001f, height) * 0.5f, Mathf.Max(0.001f, radius) * 2f);
            hitbox.convex = false;
            hitbox.isTrigger = false;
            hitbox.enabled = true;
        }

        /// <summary>Replace only presentation. The visible Cylinder returns when prefab is null.</summary>
        public void SetVisualPrefab(GameObject prefab)
        {
            EnsureCylinder();
            if (replacementVisual != null)
            {
                replacementVisual.SetActive(false);
                DestroyVisualObject(replacementVisual);
                replacementVisual = null;
            }
            if (prefab != null)
            {
                replacementVisual = Instantiate(prefab, visualMount);
                replacementVisual.transform.localPosition = Vector3.zero;
                replacementVisual.transform.localRotation = Quaternion.identity;
                LabVisualSafety.RemovePhysics(replacementVisual);
            }
            cylinderRenderer.enabled = prefab == null;
        }

        private void EnsureCylinder()
        {
            if (hitbox != null && cylinderRenderer != null && visualMount != null) return;

            Transform existing = transform.Find("Hitbox Cylinder");
            GameObject cylinder = existing != null
                ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Hitbox Cylinder";
            if (existing == null) cylinder.transform.SetParent(transform, false);
            cylinder.transform.localPosition = Vector3.zero;
            cylinder.transform.localRotation = Quaternion.identity;

            // CreatePrimitive adds a CapsuleCollider. It is never used as a hidden substitute.
            CapsuleCollider defaultCollider = cylinder.GetComponent<CapsuleCollider>();
            if (defaultCollider != null)
            {
                defaultCollider.enabled = false;
                DestroyVisualObject(defaultCollider);
            }
            MeshFilter filter = cylinder.GetComponent<MeshFilter>();
            cylinderRenderer = cylinder.GetComponent<MeshRenderer>();
            hitbox = cylinder.GetComponent<MeshCollider>();
            if (hitbox == null) hitbox = cylinder.AddComponent<MeshCollider>();
            hitbox.sharedMesh = filter.sharedMesh;
            hitbox.convex = false;
            hitbox.isTrigger = false;

            Transform existingMount = transform.Find("Visual Mount");
            if (existingMount == null)
            {
                GameObject mount = new GameObject("Visual Mount");
                existingMount = mount.transform;
                existingMount.SetParent(transform, false);
            }
            visualMount = existingMount;
        }

        private static void DestroyVisualObject(UnityEngine.Object item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }

    /// <summary>Art-only children cannot silently add a second collision body.</summary>
    internal static class LabVisualSafety
    {
        public static void RemovePhysics(GameObject visual)
        {
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                DestroyVisualObject(collider);
            }
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.detectCollisions = false;
                body.isKinematic = true;
                DestroyVisualObject(body);
            }
        }

        public static void DestroyVisualObject(UnityEngine.Object item)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
