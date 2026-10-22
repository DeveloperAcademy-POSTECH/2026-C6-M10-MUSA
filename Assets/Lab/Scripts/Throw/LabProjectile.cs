using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    public enum LabProjectileHitKind { Miss, Monster, Fire }

    public readonly struct LabProjectileResult
    {
        public readonly string ProjectileId;
        public readonly ulong OwnerPlayerId;
        public readonly bool Hit;
        public readonly LabProjectileHitKind Kind;
        public readonly string FireId, FireTargetId;
        public readonly Vector3 ImpactVelocity;
        public readonly Vector3 HitPoint;
        public readonly float FlightSeconds;
        public readonly float PeakRise;
        public readonly float HorizontalDistance;
        public string Id => ProjectileId;

        public LabProjectileResult(string projectileId, ulong ownerPlayerId, bool hit,
            Vector3 hitPoint, float flightSeconds, float peakRise, float horizontalDistance, Vector3 impactVelocity = default)
            : this(projectileId, ownerPlayerId, hit ? LabProjectileHitKind.Monster : LabProjectileHitKind.Miss,
                hitPoint, flightSeconds, peakRise, horizontalDistance, impactVelocity) { }

        public LabProjectileResult(string projectileId, ulong ownerPlayerId, LabProjectileHitKind kind,
            Vector3 hitPoint, float flightSeconds, float peakRise, float horizontalDistance,
            Vector3 impactVelocity = default, string fireId = null, string fireTargetId = null)
        {
            ProjectileId = projectileId;
            OwnerPlayerId = ownerPlayerId;
            Kind = kind;
            Hit = kind == LabProjectileHitKind.Monster;
            FireId = kind == LabProjectileHitKind.Fire ? fireId : null;
            FireTargetId = kind == LabProjectileHitKind.Fire ? fireTargetId : null;
            ImpactVelocity = impactVelocity;
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
        private float sizeScale = 1f;
        private const float DefaultTrailSeconds = .28f;
        private const float DefaultTrailWidthScale = .75f;
        private const float DefaultTrailMinVertexDistance = .035f;
        private static Material sharedTrailMaterial;

        [SerializeField] private Transform visualMount;
        [SerializeField] private TrailRenderer flightTrail;
        private LabElementFlightTrail elementFlightTrail;
        private GameObject replacementVisual;
        private GameObject replacementPrefab;
        private LabOrbSpriteArt orbSpriteArt;
        private LabOrbAppearance appearance;
        private LabOrbElement element;
        private Rigidbody body;
        private SphereCollider sphere;
        private LabConfig config;
        private LabTarget target;
        private Collider missSurface;
        private Action<LabProjectileResult> onResolved;
        private string projectileId;
        private ulong ownerPlayerId;
        private Vector3 lastPosition;
        private SurfacePose previousTargetPose, previousMissPose;
        private readonly List<LabJigwiFireSurface> fireSurfaces = new List<LabJigwiFireSurface>();
        private readonly List<SurfacePose> previousFirePoses = new List<SurfacePose>();
        private const float SurfaceDistanceTolerance = .0001f;
        private const float ActualContactTolerance = .000001f;

        private readonly struct SurfacePose
        {
            public readonly Collider Collider;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public SurfacePose(Collider collider)
            {
                Collider = collider;
                Position = collider != null ? collider.transform.position : Vector3.zero;
                Rotation = collider != null ? collider.transform.rotation : Quaternion.identity;
            }
        }

        private struct ResolutionSurface
        {
            public bool Found;
            public LabProjectileHitKind Kind;
            public LabJigwiFireSurface Fire;
            public float Distance;
            public Vector3 Point;
        }
        private Vector3 incomingVelocity;
        private float launchedGravity, launchedLifetime;
        private Vector3 launchPosition;
        private Vector3 initialVelocity;
        private float peakHeight;
        private float launchedAt;
        private bool launched;
        // 박 색상 콤보(#222)의 공 크기 배율: 실패 페널티면 1보다 작고 성공 보상이면 크다. 충돌 반지름/모양/꼬리에 함께 곱한다.
        private float radiusScale = 1f;
        private bool authoritative;
        private bool resolved;

        public string ProjectileId => projectileId;
        public ulong OwnerPlayerId => ownerPlayerId;
        public bool IsResolved => resolved;
        public float GravityAcceleration => launchedGravity;
        public float LifetimeSeconds => launchedLifetime;
        public Vector3 LaunchPosition => launchPosition;
        /// <summary>The share of the configured throw radius this projectile has; below 1 for a blast-shrunk orb (#213).</summary>
        public float SizeScale => sizeScale;
        // 폭발로 작아진 구슬(#213)의 sizeScale과 콤보 페널티/보상(#222)의 radiusScale을 함께 곱한다.
        private float ScaledRadius => (config != null ? config.ThrowRadius : .165f) * sizeScale * radiusScale;
        public Vector3 InitialVelocity => initialVelocity;
        public Transform VisualMount => visualMount;
        public TrailRenderer FlightTrail => flightTrail;
        public LabElementFlightTrail ElementFlightTrail => elementFlightTrail;
        public float RadiusScale => radiusScale;

        /// <summary>
        /// 던지기 전에 공 크기 배율(0.01~3)을 정한다. 1이면 기존 크기다. Launch/LaunchVisual 전에 부른다.
        /// </summary>
        public void SetRadiusScale(float scale)
        {
            radiusScale = float.IsNaN(scale) || float.IsInfinity(scale) ? 1f : Mathf.Clamp(scale, .01f, 3f);
            EnsureDefaultVisual(ScaledRadius);
        }

        /// <summary>Sets shared presentation; this projectile's root collider is unchanged.</summary>
        public void ConfigureAppearance(LabOrbAppearance value)
        {
            if (appearance == value)
            {
                EnsureDefaultVisual(ScaledRadius);
                return;
            }
            appearance = value;
            SetVisualPrefabInternal(appearance != null ? appearance.VisualPrefab : null);
        }

        /// <summary>Registers the battle floor. A floor contact ends this shot as a miss.</summary>
        public void SetMissSurface(Collider floor)
        {
            missSurface = floor;
            previousMissPose = new SurfacePose(floor);
        }

        /// <summary>Owner-specific fire targets; register before launch, identically on Host and visual replicas.</summary>
        public void SetFireSurfaces(IEnumerable<LabJigwiFireSurface> surfaces)
        {
            if (launched) throw new InvalidOperationException("Fire surfaces must be registered before launch.");
            fireSurfaces.Clear();
            if (surfaces != null)
                foreach (LabJigwiFireSurface surface in surfaces)
                    if (surface != null && !fireSurfaces.Contains(surface)) fireSurfaces.Add(surface);
        }

        /// <summary>Replaces the default sphere's presentation; the root SphereCollider stays put.</summary>
        public void SetVisualPrefab(GameObject prefab)
        {
            SetVisualPrefabInternal(prefab);
        }

        private void SetVisualPrefabInternal(GameObject prefab)
        {
            float radius = ScaledRadius;
            EnsureDefaultVisual(radius);
            // A destroyed source prefab compares equal to null in Unity. Do not skip
            // removing an instantiated visual when callers clear that source.
            if ((prefab == null && replacementVisual == null)
                || (prefab != null && replacementVisual != null && replacementPrefab == prefab))
                return;
            if (replacementVisual != null)
            {
                replacementVisual.SetActive(false);
                LabVisualSafety.DestroyVisualObject(replacementVisual);
                replacementVisual = null;
            }
            replacementPrefab = prefab;
            if (prefab != null)
            {
                replacementVisual = Instantiate(prefab, visualMount);
                replacementVisual.name = "Replacement Visual";
                replacementVisual.transform.localPosition = Vector3.zero;
                replacementVisual.transform.localRotation = Quaternion.identity;
                replacementVisual.transform.localScale = Vector3.one * (2f * radius);
                LabVisualSafety.RemovePhysics(replacementVisual);
                LabOrbVisualFactory.SetLayerRecursively(replacementVisual.transform, gameObject.layer);
                if (appearance != null && prefab == appearance.VisualPrefab)
                    appearance.ApplyKindMaterial(replacementVisual, LabOrbKind.Combined, element);
            }
            Transform defaultSphere = visualMount.Find("Default Sphere");
            if (defaultSphere != null) defaultSphere.gameObject.SetActive(prefab == null);
            EnsureDefaultVisual(radius);
        }

        /// <summary>Creates the default Unity sphere presentation with an independent root collider.</summary>
        /// <summary>
        /// Builds a projectile. <paramref name="sizeScale"/> shrinks its body, art and trail for an orb a
        /// bomb blast made small (#213), so it also has less reach on the gourd.
        /// </summary>
        public static LabProjectile Create(Vector3 position, LabConfig config, Transform parent = null,
            LabOrbElement element = LabOrbElement.None, float sizeScale = 1f)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            GameObject root = new GameObject("Throw Projectile");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.AddComponent<Rigidbody>();
            root.AddComponent<SphereCollider>();
            LabProjectile projectile = root.AddComponent<LabProjectile>();
            projectile.config = config;
            projectile.element = element;
            projectile.sizeScale = sizeScale > 0f && sizeScale <= 1f ? sizeScale : 1f;
            projectile.ConfigureAppearance(config.OrbAppearance);
            projectile.EnsureDefaultVisual(projectile.ScaledRadius);
            projectile.EnsureFlightTrail(projectile.ScaledRadius);
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

        /// <summary>Correct only a predicted shot's appearance; never rewind its local flight or award a hit.</summary>
        public void ReconcileVisual(LabOrbElement approvedElement, float approvedSize, float approvedRadius)
        {
            if (authoritative || resolved || approvedElement < LabOrbElement.None || approvedElement > LabOrbElement.Earth) return;
            bool changed = element != approvedElement;
            element = approvedElement;
            sizeScale = approvedSize > 0f && approvedSize <= 1f ? approvedSize : 1f;
            SetRadiusScale(approvedRadius > 0f ? approvedRadius : 1f);
            EnsureDefaultVisual(ScaledRadius);
            EnsureFlightTrail(ScaledRadius);
            if (changed && elementFlightTrail != null && appearance != null)
                elementFlightTrail.Begin(transform, appearance.ElementTrailFor(element));
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphere = GetComponent<SphereCollider>();
        }

        private void Prepare(Vector3 velocity, LabConfig tuning, bool visualOnly)
        {
            config = tuning;
            // Freeze the launch profile: later Inspector/DEV edits cannot bend a shot already in flight.
            launchedGravity = tuning.EffectiveThrowGravity;
            launchedLifetime = tuning.ThrowLifetime;
            ConfigureAppearance(tuning.OrbAppearance);
            if (body == null) body = GetComponent<Rigidbody>();
            if (sphere == null) sphere = GetComponent<SphereCollider>();
            if (body == null || sphere == null)
                throw new InvalidOperationException("Projectile needs Rigidbody and SphereCollider.");

            sphere.radius = ScaledRadius;
            sphere.isTrigger = visualOnly;
            body.useGravity = false;
            body.isKinematic = false;
            // Sweep-based CCD avoids speculative contacts that can deflect a small sphere
            // before its real surface ever reaches the target. Forces and launch tuning are unchanged.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = tuning.ThrowAirDamping;
            body.angularDamping = 0f;
            body.linearVelocity = Vector3.ClampMagnitude(velocity, tuning.ThrowMaxWorldSpeed);
            incomingVelocity = body.linearVelocity;
            initialVelocity = body.linearVelocity;
            body.WakeUp();
            lastPosition = body.position;
            RememberSurfacePoses();
            launchPosition = body.position;
            peakHeight = body.position.y;
            launchedAt = Time.time;
            launched = true;
            EnsureDefaultVisual(ScaledRadius);
            EnsureFlightTrail(ScaledRadius);
            if (elementFlightTrail != null)
                elementFlightTrail.Begin(transform, appearance.ElementTrailFor(element));
            if (flightTrail != null)
            {
                flightTrail.Clear();
                flightTrail.emitting = true;
            }
        }

        private void FixedUpdate()
        {
            if (!launched || resolved) return;
            peakHeight = Mathf.Max(peakHeight, body.position.y);
            if ((target != null && target.Hitbox != null) || missSurface != null || fireSurfaces.Count > 0)
                SweepForResolutionSurface();
            if (resolved) return;

            incomingVelocity = body.linearVelocity;
            body.AddForce(Vector3.down * launchedGravity, ForceMode.Acceleration);
            lastPosition = body.position;
            RememberSurfacePoses();
        }

        private void Update()
        {
            if (!launched || resolved || Time.time - launchedAt < launchedLifetime) return;
            if (authoritative) Resolve(false, transform.position);
            else FinishVisual();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!launched || !authoritative || resolved) return;
            if (!IsResolutionSurface(collision.collider)) return;
            // Speculative CCD can report a separated contact. The callback alone is not a hit.
            ResolveSurfaceIfFound(collision);
        }

        private void OnCollisionStay(Collision collision) => OnCollisionEnter(collision);

        private void OnTriggerEnter(Collider other)
        {
            if (!launched || resolved || !IsResolutionSurface(other)) return;
            // Fire uses trigger geometry so non-owners never bounce off it. Authoritative hits
            // still require the same swept/overlap proof as a monster, not the callback alone.
            if (authoritative) ResolveSurfaceIfFound(null);
            else FinishVisual();
        }

        // The completed physics segment is shared by the FixedUpdate sweep and collision callback.
        // Preserve nearest-surface ordering, with the target winning the existing distance tie.
        private void SweepForResolutionSurface() => ResolveSurfaceIfFound(null);

        private void ResolveSurfaceIfFound(Collision collision)
        {
            ResolutionSurface nearest = FindResolutionSurface(collision);
            if (!nearest.Found) return;
            if (authoritative) Resolve(nearest.Kind, nearest.Point, nearest.Fire);
            else FinishVisual();
        }

        private ResolutionSurface FindResolutionSurface(Collision collision)
        {
            var nearest = new ResolutionSurface();
            Vector3 position = body.position;
            Vector3 displacement = position - lastPosition;
            float distance = displacement.magnitude;
            float radius = WorldRadius;

            // Test the start against its captured surface pose, not a moving target's new pose.
            // A target moving behind the shot must not create an overlap retroactively.
            ConsiderOverlap(previousTargetPose, lastPosition, 0f, radius, ref nearest);
            ConsiderOverlap(previousMissPose, lastPosition, 0f, radius, ref nearest);
            for (int i = 0; i < previousFirePoses.Count; i++)
                ConsiderOverlap(previousFirePoses[i], lastPosition, 0f, radius, ref nearest);

            ConsiderSweep(displacement, radius, ref nearest);

            // This also handles a stationary sphere touched by a moving target.
            ConsiderOverlap(new SurfacePose(target != null ? target.Hitbox : null),
                position, distance, radius, ref nearest);
            ConsiderOverlap(new SurfacePose(missSurface), position, distance, radius, ref nearest);
            for (int i = 0; i < fireSurfaces.Count; i++)
                if (fireSurfaces[i] != null)
                    ConsiderOverlap(new SurfacePose(fireSurfaces[i].Hitbox), position, distance, radius, ref nearest);

            // CCD can stop or slide a real contact within the engine's contact skin.
            // A large positive speculative separation is never accepted. A small positive
            // contact must also have geometric proof within these two colliders' offsets.
            if (collision != null && IsResolutionSurface(collision.collider))
            {
                float skin = sphere.contactOffset + collision.collider.contactOffset;
                for (int i = 0; i < collision.contactCount; i++)
                {
                    ContactPoint contact = collision.GetContact(i);
                    if (contact.separation <= ActualContactTolerance)
                        ConsiderSurface(collision.collider, contact.point, distance, ref nearest);
                    else if (contact.separation <= skin + ActualContactTolerance)
                    {
                        ConsiderSweep(displacement, radius + skin, ref nearest, collision.collider);
                        ConsiderSkinPoint(collision.collider, lastPosition, -contact.normal,
                            radius + skin, 0f, ref nearest);
                        ConsiderSkinPoint(collision.collider, position, -contact.normal,
                            radius + skin, distance, ref nearest);
                    }
                }
            }
            return nearest;
        }

        private void ConsiderSweep(Vector3 displacement, float radius,
            ref ResolutionSurface nearest, Collider contactSurface = null)
        {
            float distance = displacement.magnitude;
            if (distance <= SurfaceDistanceTolerance) return;
            RaycastHit[] hits = Physics.SphereCastAll(lastPosition, radius,
                displacement / distance, distance, Physics.DefaultRaycastLayers,
                fireSurfaces.Count > 0 ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!IsResolutionSurface(hits[i].collider)) continue;
                if (contactSurface != null && hits[i].collider != contactSurface) continue;
                // An initial-overlap cast has no surface point (Unity returns point zero).
                // Penetration and contact-normal rays supply genuine surface points instead.
                if (hits[i].distance == 0f && hits[i].point == Vector3.zero) continue;
                ConsiderSurface(hits[i].collider, hits[i].point, hits[i].distance, ref nearest);
            }
        }

        private void ConsiderSkinPoint(Collider collider, Vector3 center, Vector3 direction,
            float reach, float distance, ref ResolutionSurface nearest)
        {
            if (collider.Raycast(new Ray(center, direction), out RaycastHit hit, reach))
                ConsiderSurface(collider, hit.point, distance, ref nearest);
        }

        private void ConsiderOverlap(SurfacePose surface, Vector3 center, float distance,
            float radius, ref ResolutionSurface nearest)
        {
            if (!IsResolutionSurface(surface.Collider)) return;
            if (!Physics.ComputePenetration(sphere, center, body.rotation,
                surface.Collider, surface.Position, surface.Rotation, out Vector3 direction, out float depth)) return;
            // Move the sphere out, then step back one radius to its point on the surface.
            Vector3 point = center + direction * (depth - radius);
            ConsiderSurface(surface.Collider, point, distance, ref nearest);
        }

        private void ConsiderSurface(Collider collider, Vector3 point, float distance,
            ref ResolutionSurface nearest)
        {
            LabJigwiFireSurface fire = FireFor(collider);
            LabProjectileHitKind kind = fire != null ? LabProjectileHitKind.Fire
                : target != null && collider == target.Hitbox ? LabProjectileHitKind.Monster : LabProjectileHitKind.Miss;
            if (nearest.Found && distance > nearest.Distance + SurfaceDistanceTolerance) return;
            if (nearest.Found && Mathf.Abs(distance - nearest.Distance) <= SurfaceDistanceTolerance
                && (int)kind <= (int)nearest.Kind) return;
            nearest.Found = true;
            nearest.Kind = kind;
            nearest.Fire = fire;
            nearest.Point = point;
            nearest.Distance = distance;
        }

        private bool IsResolutionSurface(Collider collider) => collider != null && collider.enabled
            && collider.gameObject.activeInHierarchy
            && ((!collider.isTrigger && ((target != null && collider == target.Hitbox) || collider == missSurface))
                || FireFor(collider) != null);

        private LabJigwiFireSurface FireFor(Collider collider)
        {
            for (int i = 0; i < fireSurfaces.Count; i++)
            {
                LabJigwiFireSurface fire = fireSurfaces[i];
                if (fire != null && fire.Active && fire.Hitbox == collider
                    && (!authoritative || fire.OwnerPlayerId == ownerPlayerId)) return fire;
            }
            return null;
        }

        private float WorldRadius
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            }
        }

        private void RememberSurfacePoses()
        {
            previousTargetPose = new SurfacePose(target != null ? target.Hitbox : null);
            previousMissPose = new SurfacePose(missSurface);
            previousFirePoses.Clear();
            for (int i = 0; i < fireSurfaces.Count; i++)
                if (fireSurfaces[i] != null) previousFirePoses.Add(new SurfacePose(fireSurfaces[i].Hitbox));
        }

        private void FinishVisual()
        {
            if (resolved) return;
            resolved = true;
            LeaveFadingTrail();
            Destroy(gameObject);
        }

        private void Resolve(bool hit, Vector3 point) => Resolve(
            hit ? LabProjectileHitKind.Monster : LabProjectileHitKind.Miss, point, null);

        private void Resolve(LabProjectileHitKind kind, Vector3 point, LabJigwiFireSurface fire)
        {
            if (resolved) return;
            resolved = true;
            Vector3 impactVelocity = incomingVelocity;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            peakHeight = Mathf.Max(peakHeight, body.position.y);
            Vector2 startXZ = new Vector2(launchPosition.x, launchPosition.z);
            Vector2 endXZ = new Vector2(point.x, point.z);
            LabProjectileResult result = new LabProjectileResult(projectileId, ownerPlayerId,
                kind, point, Mathf.Max(0f, Time.time - launchedAt),
                Mathf.Max(0f, peakHeight - launchPosition.y), Vector2.Distance(startXZ, endXZ), impactVelocity,
                fire != null ? fire.FireId : null, fire != null ? fire.FireTargetId : null);
            // Keep the last visible part of the flight briefly. If the callback ends the
            // round, ClearProjectiles also destroys this detached child of projectileRoot.
            LeaveFadingTrail();
            try
            {
                onResolved?.Invoke(result);
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        private void EnsureFlightTrail(float radius)
        {
            LabElementTrailProfile profile = appearance != null ? appearance.ElementTrailFor(element) : null;
            if (profile != null)
            {
                if (flightTrail != null)
                {
                    flightTrail.Clear();
                    flightTrail.gameObject.SetActive(false);
                    LabVisualSafety.DestroyVisualObject(flightTrail.gameObject);
                    flightTrail = null;
                }
                if (elementFlightTrail == null)
                {
                    GameObject ink = new GameObject("Element Flight Trail");
                    ink.transform.SetParent(transform, false);
                    ink.layer = gameObject.layer;
                    elementFlightTrail = ink.AddComponent<LabElementFlightTrail>();
                }
                return;
            }
            if (elementFlightTrail != null)
            {
                elementFlightTrail.Clear();
                elementFlightTrail.gameObject.SetActive(false);
                LabVisualSafety.DestroyVisualObject(elementFlightTrail.gameObject);
                elementFlightTrail = null;
            }
            if (flightTrail == null)
            {
                GameObject trailObject = new GameObject("Flight Trail");
                trailObject.transform.SetParent(transform, false);
                flightTrail = trailObject.AddComponent<TrailRenderer>();
                flightTrail.emitting = false;
            }

            flightTrail.gameObject.layer = gameObject.layer;
            flightTrail.transform.localPosition = Vector3.zero;
            flightTrail.transform.localRotation = Quaternion.identity;
            flightTrail.time = appearance != null ? appearance.ThrowTrailSeconds : DefaultTrailSeconds;
            flightTrail.widthMultiplier = radius * (appearance != null
                ? appearance.ThrowTrailWidthScale : DefaultTrailWidthScale);
            flightTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            flightTrail.minVertexDistance = appearance != null
                ? appearance.ThrowTrailMinVertexDistance : DefaultTrailMinVertexDistance;
            flightTrail.numCornerVertices = 2;
            flightTrail.numCapVertices = 2;
            flightTrail.alignment = LineAlignment.View;
            flightTrail.textureMode = LineTextureMode.Stretch;
            flightTrail.shadowCastingMode = ShadowCastingMode.Off;
            flightTrail.receiveShadows = false;
            flightTrail.sharedMaterial = appearance != null && appearance.ThrowTrailMaterial != null
                ? appearance.ThrowTrailMaterial : GetTrailMaterial();
            Color color = LabOrbVisualFactory.TemporaryElementColor(element);
            color.a = .82f;
            flightTrail.startColor = color;
            color.a = 0f;
            flightTrail.endColor = color;
        }

        private void LeaveFadingTrail()
        {
            if (elementFlightTrail != null)
            {
                LabElementFlightTrail fadingInk = elementFlightTrail;
                elementFlightTrail = null;
                fadingInk.StopAndFade(transform.parent);
            }
            if (flightTrail == null) return;
            TrailRenderer fadingTrail = flightTrail;
            flightTrail = null;
            fadingTrail.emitting = false;
            fadingTrail.transform.SetParent(transform.parent, true);
            Destroy(fadingTrail.gameObject, fadingTrail.time);
        }

        private void OnDisable()
        {
            // A restart/room exit cancels an attached effect immediately. A resolved shot's
            // detached fading effect is no longer owned here and keeps its natural lifetime.
            if (elementFlightTrail != null) elementFlightTrail.Clear();
            if (flightTrail != null) flightTrail.Clear();
        }

        private static Material GetTrailMaterial()
        {
            if (sharedTrailMaterial != null) return sharedTrailMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            sharedTrailMaterial = new Material(shader)
            {
                name = "Lab shared flight trail",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            if (sharedTrailMaterial.HasProperty("_Surface"))
                sharedTrailMaterial.SetFloat("_Surface", 1f);
            if (sharedTrailMaterial.HasProperty("_Blend"))
                sharedTrailMaterial.SetFloat("_Blend", 0f);
            if (sharedTrailMaterial.HasProperty("_SrcBlend"))
                sharedTrailMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (sharedTrailMaterial.HasProperty("_DstBlend"))
                sharedTrailMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (sharedTrailMaterial.HasProperty("_ZWrite"))
                sharedTrailMaterial.SetFloat("_ZWrite", 0f);
            if (sharedTrailMaterial.HasProperty("_BaseColor"))
                sharedTrailMaterial.SetColor("_BaseColor", Color.white);
            if (sharedTrailMaterial.HasProperty("_Color"))
                sharedTrailMaterial.SetColor("_Color", Color.white);
            sharedTrailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return sharedTrailMaterial;
        }

        private void EnsureDefaultVisual(float radius)
        {
            if (visualMount == null)
            {
                GameObject mount = new GameObject("Visual Mount");
                visualMount = mount.transform;
                visualMount.SetParent(transform, false);
            }
            // The board and the projectile share one replaceable 3D sphere presentation.
            // Physics stays on each object's root, with its own gameplay radius.
            LabOrbVisualFactory.EnsureSphere(visualMount, "Default Sphere", radius,
                LabOrbKind.Combined,
                appearance != null ? appearance.MaterialFor(LabOrbKind.Combined, element) : null,
                appearance == null || appearance.NeedsTemporaryElementColor(LabOrbKind.Combined, element)
                    ? element : LabOrbElement.None);
            Transform defaultSphere = visualMount.Find("Default Sphere");
            Sprite sprite = appearance != null ? appearance.SpriteFor(LabOrbKind.Combined, element) : null;
            orbSpriteArt = LabOrbSpriteArt.Set(visualMount, orbSpriteArt, sprite,
                radius * LabOrbSpriteArt.SizeScaleFor(LabOrbKind.Combined));
            if (defaultSphere != null) defaultSphere.gameObject.SetActive(sprite == null && replacementVisual == null);
            if (replacementVisual != null)
            {
                replacementVisual.SetActive(sprite == null);
                replacementVisual.transform.localScale = Vector3.one * (2f * radius);
                LabOrbVisualFactory.SetLayerRecursively(replacementVisual.transform, gameObject.layer);
                if (appearance != null && replacementPrefab == appearance.VisualPrefab)
                    appearance.ApplyKindMaterial(replacementVisual, LabOrbKind.Combined, element);
            }
        }
    }
}
