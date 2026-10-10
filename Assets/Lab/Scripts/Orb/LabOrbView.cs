using System;
using System.Collections;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The orb identity, Rigidbody and SphereCollider belong to this root. Visual is an
    /// art-only mount: replacing its child never changes the collision sphere or orb ID.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class LabOrbView : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private bool tintDefaultByKind = true;

        private GameObject replacementVisual;
        private string orbId;
        private LabOrbKind kind;
        private float visualRadius = .32f;
        private Coroutine spawnRiseRoutine;
        private Action spawnRiseCompleted;

        public string OrbId => orbId;
        public LabOrbKind Kind => kind;
        public Transform Visual => visual;
        public bool IsSpawning { get; private set; }
        public Rigidbody Body { get; private set; }
        public SphereCollider HitSphere { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            HitSphere = GetComponent<SphereCollider>();
            EnsureVisual();
        }

        private void Update()
        {
            if (Body == null || visual == null || visualRadius <= 0f) return;
            Vector3 velocity = Body.linearVelocity;
            velocity.z = 0f;
            if (velocity.sqrMagnitude < .0001f) return;

            // The root is rotation-locked by the board. Rotate art only so the board's
            // planar collision and network position remain unchanged while it rolls.
            Vector3 axis = Vector3.Cross(Vector3.forward, velocity.normalized);
            float degrees = velocity.magnitude / visualRadius * Mathf.Rad2Deg * Time.deltaTime;
            visual.Rotate(axis, degrees, Space.World);
        }

        public void Configure(string id, LabOrbKind orbKind, float radius)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An orb ID is required.", nameof(id));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));

            if (Body == null) Body = GetComponent<Rigidbody>();
            if (HitSphere == null) HitSphere = GetComponent<SphereCollider>();
            orbId = id;
            kind = orbKind;
            visualRadius = radius;
            HitSphere.radius = radius;
            HitSphere.center = Vector3.zero;
            EnsureVisual();
            UpdateVisual();
        }

        /// <summary>
        /// Replaces presentation only. A replacement is authored at unit diameter; the
        /// mount scales it to the configured orb diameter. Any physics components on the
        /// art prefab are disabled and removed so the root remains the only physics body.
        /// Pass null to restore the generated Unity sphere.
        /// </summary>
        public void SetVisualPrefab(GameObject prefab)
        {
            EnsureVisual();
            if (replacementVisual != null)
            {
                replacementVisual.SetActive(false);
                LabVisualSafety.DestroyVisualObject(replacementVisual);
                replacementVisual = null;
            }

            if (prefab != null)
            {
                replacementVisual = Instantiate(prefab, visual);
                replacementVisual.name = "Replacement Visual";
                replacementVisual.transform.localPosition = Vector3.zero;
                replacementVisual.transform.localRotation = Quaternion.identity;
                replacementVisual.transform.localScale = Vector3.one * (2f * visualRadius);
                LabVisualSafety.RemovePhysics(replacementVisual);
            }
            UpdateVisual();
        }

        /// <summary>
        /// Animates only the replaceable Visual child. The orb root, its Rigidbody and
        /// its collider remain at the host-approved position throughout this effect.
        /// Repeated calls during the same rise share its completion instead of replaying it.
        /// Completion also runs if the view is disabled or destroyed, so callers can
        /// release any temporary input/physics lock.
        /// </summary>
        public void PlaySpawnRise(float distance, float duration, Action onComplete = null)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));

            if (IsSpawning)
            {
                spawnRiseCompleted += onComplete;
                return;
            }

            EnsureVisual();
            if (visual == null || !isActiveAndEnabled || distance == 0f || duration == 0f)
            {
                if (visual != null) visual.localPosition = Vector3.zero;
                onComplete?.Invoke();
                return;
            }

            IsSpawning = true;
            spawnRiseCompleted = onComplete;
            visual.localPosition = new Vector3(0f, -distance, 0f);
            spawnRiseRoutine = StartCoroutine(RiseVisual(duration));
        }

        private IEnumerator RiseVisual(float duration)
        {
            float elapsed = 0f;
            Vector3 from = visual.localPosition;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float remaining = 1f - Mathf.Clamp01(elapsed / duration);
                float eased = 1f - remaining * remaining * remaining;
                if (visual != null) visual.localPosition = Vector3.LerpUnclamped(from, Vector3.zero, eased);
                yield return null;
            }

            spawnRiseRoutine = null;
            CompleteSpawnRise();
        }

        private void OnDisable()
        {
            CompleteSpawnRise();
        }

        private void OnDestroy()
        {
            CompleteSpawnRise();
        }

        private void CompleteSpawnRise()
        {
            if (!IsSpawning) return;
            IsSpawning = false;
            if (spawnRiseRoutine != null)
            {
                StopCoroutine(spawnRiseRoutine);
                spawnRiseRoutine = null;
            }
            if (visual != null) visual.localPosition = Vector3.zero;
            Action completed = spawnRiseCompleted;
            spawnRiseCompleted = null;
            completed?.Invoke();
        }

        private void EnsureVisual()
        {
            if (visual == null)
            {
                Transform existing = transform.Find("Visual - replaceable");
                if (existing == null)
                {
                    var child = new GameObject("Visual - replaceable");
                    existing = child.transform;
                    existing.SetParent(transform, false);
                }
                visual = existing;
            }
            visual.gameObject.layer = gameObject.layer;
            LabOrbVisualFactory.EnsureSphere(visual, "Default Sphere", visualRadius, kind);
        }

        private void UpdateVisual()
        {
            MeshRenderer defaultRenderer = LabOrbVisualFactory.EnsureSphere(
                visual, "Default Sphere", visualRadius, kind);
            if (defaultRenderer != null)
            {
                if (!tintDefaultByKind) defaultRenderer.SetPropertyBlock(null);
            }
            Transform defaultSphere = visual.Find("Default Sphere");
            if (defaultSphere != null) defaultSphere.gameObject.SetActive(replacementVisual == null);
            if (replacementVisual != null)
            {
                replacementVisual.transform.localScale = Vector3.one * (2f * visualRadius);
                LabOrbVisualFactory.SetLayerRecursively(replacementVisual.transform, visual.gameObject.layer);
            }
        }
    }

    /// <summary>Shared procedural presentation for board and thrown orbs.</summary>
    internal static class LabOrbVisualFactory
    {
        private static Material sphereMaterial;
        private static Mesh sphereMesh;

        /// <summary>
        /// Adds or updates an art-only Unity sphere mesh under mount. It has no collider:
        /// the corresponding orb or projectile root owns the sole physics representation.
        /// </summary>
        public static MeshRenderer EnsureSphere(Transform mount, string childName,
            float radius, LabOrbKind kind)
        {
            if (mount == null) throw new ArgumentNullException(nameof(mount));
            Transform child = mount.Find(childName);
            if (child == null)
            {
                var sphere = new GameObject(childName);
                sphere.transform.SetParent(mount, false);
                sphere.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
                sphere.AddComponent<MeshRenderer>();
                child = sphere.transform;
            }

            child.gameObject.layer = mount.gameObject.layer;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one * (2f * radius);
            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == null) filter.sharedMesh = GetSphereMesh();
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = GetSphereMaterial();
                if (material != null) renderer.sharedMaterial = material;
                ApplyKindColor(renderer, kind);
            }
            EnsureRollMarker(child, material: GetSphereMaterial());
            return renderer;
        }

        public static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private static Mesh GetSphereMesh()
        {
            if (sphereMesh != null) return sphereMesh;
            sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            if (sphereMesh != null) return sphereMesh;

            // Older Unity installations may not expose the built-in mesh by name.
            // The temporary primitive never joins the art hierarchy or participates
            // in physics; its mesh is still the same Unity 3D sphere.
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            template.SetActive(false);
            sphereMesh = template.GetComponent<MeshFilter>().sharedMesh;
            LabVisualSafety.DestroyVisualObject(template);
            return sphereMesh;
        }

        private static void EnsureRollMarker(Transform sphere, Material material)
        {
            Transform marker = sphere.Find("Roll Marker");
            if (marker == null)
            {
                var markerObject = new GameObject("Roll Marker");
                markerObject.transform.SetParent(sphere, false);
                markerObject.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
                markerObject.AddComponent<MeshRenderer>();
                marker = markerObject.transform;
            }
            marker.gameObject.layer = sphere.gameObject.layer;
            marker.localPosition = new Vector3(.18f, .17f, -.43f);
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one * .12f;
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (material != null) renderer.sharedMaterial = material;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", new Color(.94f, .91f, .78f));
                properties.SetColor("_Color", new Color(.94f, .91f, .78f));
                renderer.SetPropertyBlock(properties);
            }
        }

        public static void ApplyKindColor(Renderer renderer, LabOrbKind kind)
        {
            if (renderer == null) return;
            Color color = kind switch
            {
                LabOrbKind.Yin => new Color(.17f, .46f, .94f),
                LabOrbKind.Yang => new Color(.96f, .38f, .17f),
                _ => new Color(.53f, .34f, .84f)
            };
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color); // URP Lit
            properties.SetColor("_Color", color);     // Standard shader fallback
            renderer.SetPropertyBlock(properties);
        }

        private static Material GetSphereMaterial()
        {
            if (sphereMaterial != null) return sphereMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            if (shader == null) return null;
            sphereMaterial = new Material(shader)
            {
                name = "Lab orb procedural material",
                hideFlags = HideFlags.DontSave
            };
            return sphereMaterial;
        }
    }
}
