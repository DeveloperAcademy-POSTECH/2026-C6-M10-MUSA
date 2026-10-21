using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// A persistent target collider follows the active gourd shell mesh. Art-only decorations
    /// never add collision bodies, and replacing a visual never changes the hitbox identity.
    /// </summary>
    public sealed class LabTarget : MonoBehaviour
    {
        [SerializeField] private MeshCollider hitbox;
        [SerializeField] private MeshRenderer cylinderRenderer;
        [SerializeField] private Transform visualMount;
        private GameObject replacementVisual;
        private MeshFilter[] boundShells;
        private Mesh[] boundMeshes;
        private Matrix4x4[] boundPoses;
        private Mesh ownedHitboxMesh;
        private Mesh fallbackMesh;
        private Vector3 fallbackPosition, fallbackScale;
        private Quaternion fallbackRotation;
        private bool hasFallback;

        /// <summary>Local presentation event, emitted only after the existing Host approval gates.</summary>
        public event System.Action<Vector3, LabOrbElement> ApprovedHit;
        public void NotifyApprovedHit(Vector3 localPoint, LabOrbElement element)
            => ApprovedHit?.Invoke(localPoint, element);

        public MeshCollider Hitbox => hitbox;
        public Transform VisualMount => visualMount;
        /// <summary>The instance owned by SetVisualPrefab, independent of other mount children.</summary>
        public GameObject MountedVisual => replacementVisual;

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

        /// <summary>Replace presentation, retaining a real saved gourd mesh as the fallback.</summary>
        public void SetVisualPrefab(GameObject prefab)
        {
            // A missing saved gourd reference is an error, never a reason to recreate the
            // obsolete cylinder in the game. Only the explicit CreateCylinder fixture builds one.
            if (hitbox == null || cylinderRenderer == null || visualMount == null)
                throw new System.InvalidOperationException("The target needs its saved mesh collider, renderer, and visual mount.");
            CaptureFallback();
            RestoreFallback();
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

        /// <summary>Use exactly the visible shell's mesh and pose, without adding a second collider.</summary>
        public bool BindHitboxMesh(MeshFilter surface) => BindHitboxMeshes(new[] { surface });

        /// <summary>Crack mode may have several shell pieces. Only these explicit surfaces collide.</summary>
        public bool BindHitboxMeshes(MeshFilter[] surfaces)
        {
            if (hitbox == null || visualMount == null || surfaces == null || surfaces.Length == 0)
                return false;
            foreach (var surface in surfaces)
                if (surface == null || surface.sharedMesh == null || surface.transform == hitbox.transform)
                    return false;
            CaptureFallback();
            boundShells = (MeshFilter[])surfaces.Clone();
            boundMeshes = null;
            boundPoses = null;
            return RefreshBoundHitbox();
        }

        /// <summary>
        /// Refresh only when shell geometry or its pose relative to VisualMount changed. Common
        /// hanging/spin movement needs no recooking because shell and collider share this parent.
        /// </summary>
        public bool RefreshBoundHitbox()
        {
            if (hitbox == null || visualMount == null || boundShells == null || boundShells.Length == 0)
                return false;
            bool changed = boundMeshes == null || boundPoses == null;
            for (int i = 0; i < boundShells.Length; i++)
            {
                var surface = boundShells[i];
                if (surface == null || surface.sharedMesh == null) return false;
                Matrix4x4 pose = visualMount.worldToLocalMatrix * surface.transform.localToWorldMatrix;
                if (!changed && (boundMeshes[i] != surface.sharedMesh || !SamePose(boundPoses[i], pose)))
                    changed = true;
            }
            if (!changed) return true;

            var meshes = new Mesh[boundShells.Length];
            var poses = new Matrix4x4[boundShells.Length];
            for (int i = 0; i < boundShells.Length; i++)
            {
                meshes[i] = boundShells[i].sharedMesh;
                poses[i] = visualMount.worldToLocalMatrix * boundShells[i].transform.localToWorldMatrix;
            }
            Mesh collisionMesh;
            Mesh combined = null;
            if (meshes.Length == 1) collisionMesh = meshes[0];
            else
            {
                var parts = new System.Collections.Generic.List<CombineInstance>();
                for (int i = 0; i < meshes.Length; i++)
                {
                    if (!meshes[i].isReadable) return false;
                    for (int submesh = 0; submesh < meshes[i].subMeshCount; submesh++)
                        parts.Add(new CombineInstance { mesh = meshes[i], subMeshIndex = submesh,
                            transform = poses[i] });
                }
                combined = new Mesh { name = "Gourd shell collision (runtime)",
                    hideFlags = HideFlags.DontSave,
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                combined.CombineMeshes(parts.ToArray(), true, true);
                combined.RecalculateBounds();
                collisionMesh = combined;
            }

            Mesh previousOwned = ownedHitboxMesh;
            ownedHitboxMesh = combined;
            hitbox.transform.SetParent(visualMount, false);
            ApplyPose(hitbox.transform, meshes.Length == 1 ? poses[0] : Matrix4x4.identity);
            hitbox.sharedMesh = collisionMesh;
            hitbox.convex = false;
            hitbox.isTrigger = false;
            hitbox.enabled = true;
            boundMeshes = meshes;
            boundPoses = poses;
            if (previousOwned != null) DestroyVisualObject(previousOwned);
            return true;
        }

        private void CaptureFallback()
        {
            if (hasFallback || hitbox == null || visualMount == null || hitbox.sharedMesh == null) return;
            fallbackMesh = hitbox.sharedMesh;
            Matrix4x4 pose = visualMount.worldToLocalMatrix * hitbox.transform.localToWorldMatrix;
            fallbackPosition = pose.GetColumn(3);
            fallbackRotation = pose.rotation;
            fallbackScale = pose.lossyScale;
            hasFallback = true;
        }

        private void RestoreFallback()
        {
            boundShells = null; boundMeshes = null; boundPoses = null;
            if (hasFallback && hitbox != null && visualMount != null)
            {
                hitbox.transform.SetParent(visualMount, false);
                hitbox.transform.localPosition = fallbackPosition;
                hitbox.transform.localRotation = fallbackRotation;
                hitbox.transform.localScale = fallbackScale;
                hitbox.sharedMesh = fallbackMesh;
                hitbox.convex = false;
                hitbox.isTrigger = false;
                hitbox.enabled = true;
            }
            if (ownedHitboxMesh != null) DestroyVisualObject(ownedHitboxMesh);
            ownedHitboxMesh = null;
        }

        private static void ApplyPose(Transform destination, Matrix4x4 pose)
        {
            destination.localPosition = pose.GetColumn(3);
            destination.localRotation = pose.rotation;
            destination.localScale = pose.lossyScale;
        }

        private static bool SamePose(Matrix4x4 first, Matrix4x4 second)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(first[i] - second[i]) > .00001f) return false;
            return true;
        }

        private void OnDestroy()
        {
            if (ownedHitboxMesh != null) DestroyVisualObject(ownedHitboxMesh);
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
            foreach (Collider2D collider in visual.GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = false;
                DestroyVisualObject(collider);
            }
            foreach (Rigidbody2D body in visual.GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.simulated = false;
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
