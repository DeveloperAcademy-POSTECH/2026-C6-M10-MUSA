using System;
using UnityEngine;

namespace C6Lab
{
    public readonly struct LabProjectileResult
    {
        public readonly string ProjectileId;
        public readonly ulong OwnerPlayerId;
        public readonly bool Hit;
        public readonly Vector3 HitPoint;
        public readonly float FlightSeconds;
        public readonly float PeakRise;
        public readonly float HorizontalDistance;
        public string Id => ProjectileId;

        public LabProjectileResult(string projectileId, ulong ownerPlayerId, bool hit,
            Vector3 hitPoint, float flightSeconds, float peakRise, float horizontalDistance)
        {
            ProjectileId = projectileId;
            OwnerPlayerId = ownerPlayerId;
            Hit = hit;
            HitPoint = hitPoint;
            FlightSeconds = flightSeconds;
            PeakRise = peakRise;
            HorizontalDistance = horizontalDistance;
        }
    }

    /// <summary>
    /// A dynamic sphere whose release motion is reproduced on every device. Only the Host's
    /// authoritative instance resolves hit or miss; client visual copies never award damage.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class LabProjectile : MonoBehaviour
    {
        [SerializeField] private Transform visualMount;
        private GameObject replacementVisual;
        private Rigidbody body;
        private SphereCollider sphere;
        private LabConfig config;
        private LabTarget target;
        private Collider missSurface;
        private Action<LabProjectileResult> onResolved;
        private string projectileId;
        private ulong ownerPlayerId;
        private Vector3 lastPosition;
        private Vector3 launchPosition;
        private float peakHeight;
        private float launchedAt;
        private bool launched;
        private bool authoritative;
        private bool resolved;

        public string ProjectileId => projectileId;
        public ulong OwnerPlayerId => ownerPlayerId;
        public bool IsResolved => resolved;
        public Transform VisualMount => visualMount;

        /// <summary>Registers the battle floor. A floor contact ends this shot as a miss.</summary>
        public void SetMissSurface(Collider floor)
        {
            missSurface = floor;
        }

        /// <summary>Replaces the default sphere's presentation; the root SphereCollider stays put.</summary>
        public void SetVisualPrefab(GameObject prefab)
        {
            float radius = config != null ? config.ThrowRadius : 0.165f;
            EnsureDefaultVisual(radius);
            if (replacementVisual != null)
            {
                replacementVisual.SetActive(false);
                LabVisualSafety.DestroyVisualObject(replacementVisual);
                replacementVisual = null;
            }
            if (prefab != null)
            {
                replacementVisual = Instantiate(prefab, visualMount);
                replacementVisual.transform.localPosition = Vector3.zero;
                replacementVisual.transform.localRotation = Quaternion.identity;
                LabVisualSafety.RemovePhysics(replacementVisual);
            }
            MeshRenderer defaultRenderer = visualMount.Find("Default Sphere")?.GetComponent<MeshRenderer>();
            if (defaultRenderer != null) defaultRenderer.enabled = prefab == null;
        }

        /// <summary>Creates the default Unity sphere presentation with an independent root collider.</summary>
        public static LabProjectile Create(Vector3 position, LabConfig config, Transform parent = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            GameObject root = new GameObject("Throw Projectile");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.AddComponent<Rigidbody>();
            root.AddComponent<SphereCollider>();
            LabProjectile projectile = root.AddComponent<LabProjectile>();
            projectile.EnsureDefaultVisual(config.ThrowRadius);
            return projectile;
        }

        /// <summary>Starts a Host-owned projectile. Returns false for a duplicate launch.</summary>
        public bool Launch(string id, ulong ownerId, Vector3 velocity, LabConfig tuning,
            LabTarget hitTarget, Action<LabProjectileResult> resolvedCallback)
        {
            if (launched) return false;
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            if (hitTarget == null || hitTarget.Hitbox == null)
                throw new ArgumentException("An active Cylinder MeshCollider target is required.",
                    nameof(hitTarget));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Projectile ID is required.", nameof(id));

            projectileId = id;
            ownerPlayerId = ownerId;
            onResolved = resolvedCallback;
            authoritative = true;
            target = hitTarget;
            Prepare(velocity, tuning, false);
            return true;
        }

        /// <summary>Starts a client-only replica. Collision may hide it but cannot change HP.</summary>
        public bool LaunchVisual(Vector3 velocity, LabConfig tuning, LabTarget hitTarget = null)
        {
            if (launched) return false;
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            authoritative = false;
            target = hitTarget;
            Prepare(velocity, tuning, true);
            return true;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphere = GetComponent<SphereCollider>();
        }

        private void Prepare(Vector3 velocity, LabConfig tuning, bool visualOnly)
        {
            config = tuning;
            if (body == null) body = GetComponent<Rigidbody>();
            if (sphere == null) sphere = GetComponent<SphereCollider>();
            if (body == null || sphere == null)
                throw new InvalidOperationException("Projectile needs Rigidbody and SphereCollider.");

            sphere.radius = tuning.ThrowRadius;
            sphere.isTrigger = visualOnly;
            body.useGravity = false;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = tuning.ThrowAirDamping;
            body.angularDamping = 0f;
            body.linearVelocity = Vector3.ClampMagnitude(velocity, tuning.ThrowMaxWorldSpeed);
            body.WakeUp();
            lastPosition = body.position;
            launchPosition = body.position;
            peakHeight = body.position.y;
            launchedAt = Time.time;
            launched = true;
            EnsureDefaultVisual(tuning.ThrowRadius);
        }

        private void FixedUpdate()
        {
            if (!launched || resolved) return;
            peakHeight = Mathf.Max(peakHeight, body.position.y);
            if ((target != null && target.Hitbox != null) || missSurface != null)
                SweepForResolutionSurface();
            if (resolved) return;

            body.AddForce(Vector3.down * config.ThrowGravity, ForceMode.Acceleration);
            lastPosition = body.position;
        }

        private void Update()
        {
            if (!launched || resolved || Time.time - launchedAt < config.ThrowLifetime) return;
            if (authoritative) Resolve(false, transform.position);
            else Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!launched || !authoritative || resolved) return;
            if (target != null && collision.collider == target.Hitbox)
            {
                Vector3 point = collision.contactCount > 0
                    ? collision.GetContact(0).point : transform.position;
                Resolve(true, point);
            }
            else if (missSurface != null && collision.collider == missSurface)
            {
                Vector3 point = collision.contactCount > 0
                    ? collision.GetContact(0).point : transform.position;
                Resolve(false, point);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!launched || authoritative || resolved) return;
            if ((target != null && other == target.Hitbox) || other == missSurface)
            {
                resolved = true;
                Destroy(gameObject);
            }
        }

        // A swept sphere supplements Continuous Speculative for fast shots through thin mesh triangles.
        // SphereCastAll is not distance-sorted, so choose the first registered surface along the path.
        private void SweepForResolutionSurface()
        {
            Vector3 displacement = body.position - lastPosition;
            float distance = displacement.magnitude;
            if (distance <= 0.0001f) return;
            RaycastHit[] hits = Physics.SphereCastAll(lastPosition, sphere.radius,
                displacement / distance, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit nearest = default;
            bool found = false;
            bool hitTarget = false;
            for (int i = 0; i < hits.Length; i++)
            {
                bool isTarget = target != null && hits[i].collider == target.Hitbox;
                bool isFloor = missSurface != null && hits[i].collider == missSurface;
                if (!isTarget && !isFloor) continue;
                if (found && hits[i].distance > nearest.distance + 0.0001f) continue;
                if (found && Mathf.Abs(hits[i].distance - nearest.distance) <= 0.0001f && !isTarget)
                    continue;
                nearest = hits[i];
                found = true;
                hitTarget = isTarget;
            }
            if (!found) return;
            if (authoritative) Resolve(hitTarget, nearest.point);
            else
            {
                resolved = true;
                Destroy(gameObject);
            }
        }

        private void Resolve(bool hit, Vector3 point)
        {
            if (resolved) return;
            resolved = true;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            peakHeight = Mathf.Max(peakHeight, body.position.y);
            Vector2 startXZ = new Vector2(launchPosition.x, launchPosition.z);
            Vector2 endXZ = new Vector2(point.x, point.z);
            LabProjectileResult result = new LabProjectileResult(projectileId, ownerPlayerId,
                hit, point, Mathf.Max(0f, Time.time - launchedAt),
                Mathf.Max(0f, peakHeight - launchPosition.y), Vector2.Distance(startXZ, endXZ));
            try
            {
                onResolved?.Invoke(result);
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        private void EnsureDefaultVisual(float radius)
        {
            if (visualMount == null)
            {
                GameObject mount = new GameObject("Visual Mount");
                visualMount = mount.transform;
                visualMount.SetParent(transform, false);
            }
            Transform visual = visualMount.Find("Default Sphere");
            if (visual == null)
            {
                GameObject sphereVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereVisual.name = "Default Sphere";
                sphereVisual.transform.SetParent(visualMount, false);
                SphereCollider redundantCollider = sphereVisual.GetComponent<SphereCollider>();
                if (redundantCollider != null)
                {
                    redundantCollider.enabled = false;
                    LabVisualSafety.DestroyVisualObject(redundantCollider);
                }
                visual = sphereVisual.transform;
            }
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one * (2f * radius);
        }
    }
}
