using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6.Prototype.Attack
{
    /// <summary>Values are supplied from the single project Config, never a second asset.</summary>
    public readonly struct ProjectilePhysicsTuning
    {
        public float Speed { get; }
        public float Lifetime { get; }
        public float Radius { get; }

        public ProjectilePhysicsTuning(float speed, float lifetime, float radius)
        {
            if (!LaunchMapping.IsFinite(speed) || !LaunchMapping.IsFinite(lifetime) ||
                !LaunchMapping.IsFinite(radius) || speed <= 0f || lifetime <= 0f || radius <= 0f)
                throw new ArgumentException("Projectile speed, lifetime and radius must be finite and positive.");
            Speed = speed;
            Lifetime = lifetime;
            Radius = radius;
        }
    }

    public enum ProjectileOutcomeKind { Hit, Expired }

    public readonly struct ProjectileOutcome
    {
        public ProjectileOutcomeKind Kind { get; }
        public string OrbId { get; }
        public ulong AttackerPlayerId { get; }
        public string TargetId { get; }
        public Vector3 Position { get; }
        public float ElapsedPhysicsTime { get; }
        public float ObservedFixedTime { get; }

        internal ProjectileOutcome(ProjectileOutcomeKind kind, string orbId, ulong attackerPlayerId,
            string targetId, Vector3 position, float elapsedPhysicsTime)
        {
            Kind = kind;
            OrbId = orbId;
            AttackerPlayerId = attackerPlayerId;
            TargetId = targetId;
            Position = position;
            ElapsedPhysicsTime = elapsedPhysicsTime;
            ObservedFixedTime = Time.fixedTime;
        }
    }

    /// <summary>
    /// Host-only real 3D physics. It reports evidence, never HP or resources. Authority must
    /// derive isHost from its owned NetworkManager; clients must use presentation-only views.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HostProjectile3D : MonoBehaviour
    {
        private static readonly HashSet<HostProjectile3D> Live = new HashSet<HostProjectile3D>();
        private Action<ProjectileOutcome> completed;
        private ProjectilePhysicsTuning tuning;
        private bool initialized;
        private float elapsed;
        private PhysicsMaterial ballisticMaterial;

        public Rigidbody Body { get; private set; }
        public SphereCollider HitCollider { get; private set; }
        public string OrbId { get; private set; }
        public ulong AttackerPlayerId { get; private set; }
        public bool HasCompleted { get; private set; }
        public int CollisionCallbackCount { get; private set; }
        public float ElapsedPhysicsTime => elapsed;
        public float Lifetime => tuning.Lifetime;
        public float Radius => tuning.Radius;
        public bool BallisticActive { get; private set; }
        public Vector3 InitialVelocity { get; private set; }
        public Vector3 GravityVector { get; private set; }
        public float Bounce { get; private set; }
        public static int LiveCount => Live.Count;

        public static HostProjectile3D Spawn(bool isHost, string orbId, ulong attackerPlayerId,
            ProjectileLaunchPose pose, ProjectilePhysicsTuning physicsTuning, Action<ProjectileOutcome> onCompleted,
            Transform parent = null, Material material = null, int layer = 0)
        {
            // Denial occurs before any GameObject, Rigidbody, view or callback is created.
            if (!isHost) throw new InvalidOperationException("Only the confirmed host may simulate a projectile.");
            if (string.IsNullOrWhiteSpace(orbId)) throw new ArgumentException("An orb ID is required.", nameof(orbId));
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
            if (pose.Direction.sqrMagnitude < .000001f || physicsTuning.Speed <= 0f ||
                physicsTuning.Lifetime <= 0f || physicsTuning.Radius <= 0f)
                throw new ArgumentException("Initialized launch pose and physics tuning are required.");
            if (layer < 0 || layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));

            var root = new GameObject("T06 Projectile " + orbId) { layer = layer };
            root.transform.position = pose.Position;
            // Parents in the scene are only organizational. Preserve world pose/scale.
            if (parent != null) root.transform.SetParent(parent, true);
            var projectile = root.AddComponent<HostProjectile3D>();
            projectile.OrbId = orbId;
            projectile.AttackerPlayerId = attackerPlayerId;
            projectile.tuning = physicsTuning;
            projectile.completed = onCompleted;
            projectile.HitCollider = root.AddComponent<SphereCollider>();
            projectile.HitCollider.radius = physicsTuning.Radius;
            projectile.HitCollider.isTrigger = false;
            projectile.Body = root.AddComponent<Rigidbody>();
            projectile.Body.useGravity = false;
            projectile.Body.isKinematic = false;
            projectile.Body.linearDamping = 0f;
            projectile.Body.angularDamping = 0f;
            projectile.Body.constraints = RigidbodyConstraints.FreezeRotation;
            projectile.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            projectile.Body.interpolation = RigidbodyInterpolation.Interpolate;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Projectile sphere view";
            visual.layer = layer;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * (physicsTuning.Radius * 2f);
            // The mesh is only a view; the root SphereCollider is the sole physical collider.
            var extraCollider = visual.GetComponent<Collider>();
            extraCollider.enabled = false;
            Destroy(extraCollider);
            if (material != null) visual.GetComponent<Renderer>().sharedMaterial = material;

            // Pairwise exclusions avoid changing any shared project collision matrix.
            // Unity automatically releases these object-specific pairs on destruction.
            Live.RemoveWhere(item => item == null);
            foreach (var other in Live)
                if (!other.HasCompleted && other.HitCollider != null)
                    Physics.IgnoreCollision(projectile.HitCollider, other.HitCollider, true);
            Live.Add(projectile);
            projectile.initialized = true;
            projectile.Body.linearVelocity = pose.Direction * physicsTuning.Speed;
            projectile.InitialVelocity = projectile.Body.linearVelocity;
            return projectile;
        }

        /// <summary>P2 opt-in. The legacy Spawn path remains a straight, zero-gravity flight.</summary>
        public static HostProjectile3D SpawnBallistic(bool isHost, string orbId, ulong attackerPlayerId,
            BallisticLaunch launch, Action<ProjectileOutcome> onCompleted,
            Transform parent = null, Material material = null, int layer = 0)
        {
            if (!isHost) throw new InvalidOperationException("Only the confirmed host may simulate a projectile.");
            if (!launch.IsValid) throw new ArgumentException("An initialized ballistic launch is required.", nameof(launch));
            var projectile = Spawn(isHost, orbId, attackerPlayerId,
                new ProjectileLaunchPose(launch.Position, launch.InitialVelocity),
                new ProjectilePhysicsTuning(launch.InitialVelocity.magnitude, launch.Lifetime, launch.Radius),
                onCompleted, parent, material, layer);
            projectile.BallisticActive = true;
            projectile.InitialVelocity = launch.InitialVelocity;
            projectile.Body.linearVelocity = launch.InitialVelocity;
            projectile.GravityVector = launch.GravityVector;
            projectile.Bounce = launch.Bounce;
            projectile.ballisticMaterial = new PhysicsMaterial("P2 projectile contact")
            {
                bounciness = launch.Bounce,
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };
            projectile.HitCollider.sharedMaterial = projectile.ballisticMaterial;
            return projectile;
        }

        private void FixedUpdate()
        {
            if (!initialized || HasCompleted) return;
            elapsed += Time.fixedDeltaTime;
            if (elapsed >= tuning.Lifetime)
            {
                Complete(ProjectileOutcomeKind.Expired, null);
                return;
            }
            // Local acceleration, not Physics.gravity: legacy projectiles and other scenes
            // retain their own gravity rules. The actual Rigidbody handles every contact.
            if (BallisticActive && Body != null && !Body.isKinematic)
                Body.AddForce(GravityVector, ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision)
        {
            CollisionCallbackCount++;
            if (!initialized || HasCompleted || collision == null) return;
            var target = collision.collider.GetComponentInParent<MonsterHitTarget>();
            if (target == null || !target.isActiveAndEnabled) return;
            Complete(ProjectileOutcomeKind.Hit, target.TargetId);
        }

        private void Complete(ProjectileOutcomeKind kind, string targetId)
        {
            if (HasCompleted) return;
            // Completion is committed BEFORE user code. Re-entrancy and additional
            // collision callbacks from this physics step cannot produce another result.
            HasCompleted = true;
            var callback = completed;
            completed = null;
            var outcome = new ProjectileOutcome(kind, OrbId, AttackerPlayerId, targetId,
                Body != null ? Body.position : transform.position, elapsed);
            if (Body != null) Body.linearVelocity = Vector3.zero;
            try { callback?.Invoke(outcome); }
            finally { Destroy(gameObject); }
        }

        /// <summary>Round/end/disconnect cleanup. It never fabricates a hit or expiry result.</summary>
        public void CancelAndDestroy()
        {
            HasCompleted = true;
            completed = null;
            DisablePhysics();
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            // A removed/disabled presentation cannot leave a live physics authority behind.
            HasCompleted = true;
            completed = null;
            DisablePhysics();
            Live.Remove(this);
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (ballisticMaterial != null) Destroy(ballisticMaterial);
        }

        private void DisablePhysics()
        {
            if (HitCollider != null) HitCollider.enabled = false;
            if (Body == null) return;
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Body.detectCollisions = false;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = true;
        }
    }
}
