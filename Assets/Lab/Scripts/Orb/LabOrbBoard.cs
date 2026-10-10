using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace C6Lab
{
    /// <summary>A local physics observation. The Host must still approve an ownership transfer.</summary>
    public readonly struct LabOrbEdgeCrossing
    {
        public string OrbId { get; }
        public bool ToRight { get; }
        public float Height01 { get; }
        public Vector2 VelocityInBoardWidthsPerSecond { get; }

        public LabOrbEdgeCrossing(string orbId, bool toRight, float height01, Vector2 velocity)
        {
            OrbId = orbId;
            ToRight = toRight;
            Height01 = height01;
            VelocityInBoardWidthsPerSecond = velocity;
        }
    }

    /// <summary>An intentional Combined flick, measured in board widths and seconds.</summary>
    public readonly struct LabOrbThrowGesture
    {
        public string OrbId { get; }
        public Vector2 Velocity { get; }
        public float ReleaseX01 { get; }
        public float DistanceInBoardWidths { get; }

        public LabOrbThrowGesture(string orbId, Vector2 velocity, float releaseX01, float distance)
        {
            OrbId = orbId;
            Velocity = velocity;
            ReleaseX01 = releaseX01;
            DistanceInBoardWidths = distance;
        }
    }

    /// <summary>
    /// Only the owner simulates a free orb. A Host-approved transfer creates that same orb ID
    /// on the receiver with its outgoing height and velocity. Gameplay requests are events;
    /// this board never changes Host-owned HP, inventory, or orb identity itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbBoard : MonoBehaviour
    {
        private sealed class Entry
        {
            public LabOrbView View;
            public bool LocalOwner;
            public bool Locked;
            public bool SpawnLocked;
            public bool Held;
            public bool EdgePending;
            public string PendingPartnerId;
            public LabOrbEdgeCrossing PendingEdge;
        }

        private readonly struct PointerSample
        {
            public readonly Vector2 Position;
            public readonly double Time;
            public PointerSample(Vector2 position, double time) { Position = position; Time = time; }
        }

        private const float ContactFriction = .15f;
        private const float WallThickness = .5f;
        private const double ReleaseSampleWindow = .12d;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> risenIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PointerSample> samples = new List<PointerSample>();
        private readonly List<Entry> updateEntries = new List<Entry>();
        private LabConfig config;
        private Camera inputCamera;
        private Rect bounds;
        private PhysicsMaterial contactMaterial;
        private GameObject wallRoot;
        private string heldId;
        private bool heldByTouch;
        private bool interactionEnabled = true;
        private bool uiInputBlocked;
        private bool configured;

        public int Count => entries.Count;
        /// <summary>World-space limits for orb centres; the visual extends by OrbRadius.</summary>
        public Rect CenterBounds => bounds;
        public bool InteractionEnabled => interactionEnabled;
        public event Action<LabOrbEdgeCrossing> EdgeCrossed;
        public event Action<string, string, Vector2> CombineRequested;
        /// <summary>The velocity is input displacement divided by board width and seconds.</summary>
        public event Action<LabOrbThrowGesture> ThrowRequested;

        public void Configure(LabConfig tuning, Camera camera, Rect worldCenterBounds)
        {
            if (tuning == null || camera == null || worldCenterBounds.width <= 0f || worldCenterBounds.height <= 0f
                || !Finite(worldCenterBounds.min) || !Finite(worldCenterBounds.max))
                throw new ArgumentException("Config, camera, and finite positive board centre bounds are required.");
            CancelDrag();
            config = tuning;
            inputCamera = camera;
            bounds = worldCenterBounds;
            configured = true;
            if (contactMaterial == null)
                contactMaterial = new PhysicsMaterial("Lab orb contact") { hideFlags = HideFlags.DontSave };
            contactMaterial.bounciness = config.OrbRestitution;
            contactMaterial.dynamicFriction = ContactFriction;
            contactMaterial.staticFriction = ContactFriction;
            contactMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
            BuildHorizontalWalls();
            foreach (var entry in entries.Values)
            {
                entry.View.Configure(entry.View.OrbId, entry.View.Kind, config.OrbRadius);
                SetLayerRecursively(entry.View.transform, gameObject.layer);
                entry.View.HitSphere.sharedMaterial = contactMaterial;
                entry.View.Body.position = OnPlane(Clamp(XY(entry.View.Body.position)));
                StopMotion(entry.View.Body);
                entry.EdgePending = false;
                SetBodyMode(entry);
            }
        }

        /// <summary>
        /// Add a newly approved orb, or update its kind/owner. Existing local physics is never
        /// reset by a repeated snapshot. Positions are 0..1; velocity is board widths/second.
        /// </summary>
        public LabOrbView UpsertOrb(string id, LabOrbKind kind, Vector2 normalizedPosition,
            Vector2 velocityInBoardWidthsPerSecond = default, bool localOwner = true,
            bool playSpawnRise = false)
        {
            RequireConfigured();
            if (string.IsNullOrWhiteSpace(id) || !Finite(normalizedPosition) || !Finite(velocityInBoardWidthsPerSecond))
                throw new ArgumentException("A finite orb identity, position and velocity are required.");
            if (entries.TryGetValue(id, out var existing))
            {
                existing.LocalOwner = localOwner;
                if (existing.View.Kind != kind)
                {
                    existing.View.Configure(id, kind, config.OrbRadius);
                    SetLayerRecursively(existing.View.transform, this.gameObject.layer);
                }
                SetBodyMode(existing);
                return existing.View;
            }

            var gameObject = new GameObject("Orb " + id);
            gameObject.layer = this.gameObject.layer;
            gameObject.transform.SetParent(transform, false);
            gameObject.transform.position = OnPlane(ToWorld(normalizedPosition));
            var view = gameObject.AddComponent<LabOrbView>();
            view.Configure(id, kind, config.OrbRadius);
            SetLayerRecursively(gameObject.transform, gameObject.layer);
            view.HitSphere.sharedMaterial = contactMaterial;
            var body = view.Body;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.mass = 1f;
            body.position = OnPlane(Clamp(ToWorld(normalizedPosition)));
            var entry = new Entry { View = view, LocalOwner = localOwner };
            entries.Add(id, entry);
            if (playSpawnRise && risenIds.Add(id))
            {
                entry.SpawnLocked = true;
                view.HitSphere.enabled = false;
                SetBodyMode(entry);
                view.PlaySpawnRise(config.SpawnRiseDistance, config.SpawnRiseDuration, () =>
                {
                    if (!entries.TryGetValue(id, out var current) || !ReferenceEquals(current, entry)
                        || current.View == null) return;
                    current.SpawnLocked = false;
                    current.View.HitSphere.enabled = true;
                    SetBodyMode(current);
                });
            }
            else SetBodyMode(entry);
            if (!body.isKinematic)
                body.linearVelocity = OnPlane(Vector2.ClampMagnitude(velocityInBoardWidthsPerSecond * bounds.width,
                    config.OrbMaxReleaseSpeed * bounds.width));
            return view;
        }

        public bool RemoveOrb(string id)
        {
            if (id == null || !entries.TryGetValue(id, out var entry)) return false;
            if (heldId == id) CancelDrag();
            entries.Remove(id);
            Retire(entry);
            return true;
        }

        public void Clear()
        {
            CancelDrag();
            foreach (var entry in entries.Values)
                Retire(entry);
            entries.Clear();
            risenIds.Clear();
        }

        public void SetInteractionEnabled(bool enabled)
        {
            if (!enabled) CancelDrag();
            interactionEnabled = enabled;
        }

        /// <summary>Block pointer gestures while a modal UI is open, without pausing orb physics.</summary>
        public void SetUiInputBlocked(bool blocked)
        {
            if (blocked) CancelDrag();
            uiInputBlocked = blocked;
        }

        public bool SetOrbLocked(string id, bool locked)
        {
            if (id == null || !entries.TryGetValue(id, out var entry)) return false;
            if (locked && heldId == id) CancelDrag();
            entry.Locked = locked;
            if (locked) StopMotion(entry.View.Body);
            SetBodyMode(entry);
            return true;
        }

        /// <summary>
        /// A rejected transfer/throw/combination restores idle physics. If a combination
        /// had locked two materials, both become movable again.
        /// </summary>
        public bool RejectEdge(string id)
        {
            if (id == null || !entries.TryGetValue(id, out var entry)) return false;
            if (entry.PendingPartnerId != null && entries.TryGetValue(entry.PendingPartnerId, out var partner))
            {
                partner.PendingPartnerId = null;
                partner.Locked = false;
                StopMotion(partner.View.Body);
                SetBodyMode(partner);
            }
            entry.PendingPartnerId = null;
            entry.EdgePending = false;
            entry.Locked = false;
            StopMotion(entry.View.Body);
            entry.View.Body.position = OnPlane(Clamp(XY(entry.View.Body.position)));
            SetBodyMode(entry);
            return true;
        }

        public bool TryGetMotion(string id, out Vector2 normalizedPosition,
            out Vector2 velocityInBoardWidthsPerSecond)
        {
            normalizedPosition = velocityInBoardWidthsPerSecond = default;
            if (!configured || id == null || !entries.TryGetValue(id, out var entry)) return false;
            normalizedPosition = ToNormalized(XY(entry.View.Body.position));
            velocityInBoardWidthsPerSecond = XY(entry.View.Body.linearVelocity) / bounds.width;
            return true;
        }

        public bool TryBeginDragAtWorld(Vector2 worldPoint, double now)
        {
            if (!configured || !interactionEnabled || heldId != null || !Finite(worldPoint) || !Finite(now)) return false;
            Entry nearest = null;
            float best = Mathf.Pow(config.OrbRadius * 1.25f, 2f);
            foreach (var entry in entries.Values)
            {
                if (!entry.LocalOwner || entry.Locked || entry.SpawnLocked || entry.EdgePending || entry.Held) continue;
                float distance = (XY(entry.View.Body.position) - worldPoint).sqrMagnitude;
                if (distance > best) continue;
                best = distance;
                nearest = entry;
            }
            if (nearest == null) return false;
            heldId = nearest.View.OrbId;
            nearest.Held = true;
            StopMotion(nearest.View.Body);
            SetBodyMode(nearest);
            samples.Clear();
            AddSample(worldPoint, now);
            return true;
        }

        public bool DragToWorld(Vector2 worldPoint, double now)
        {
            if (heldId == null || !entries.TryGetValue(heldId, out var entry) || !Finite(worldPoint) || !Finite(now))
                return false;
            entry.View.Body.position = OnPlane(Clamp(worldPoint));
            AddSample(worldPoint, now);
            return true;
        }

        public bool EndDragAtWorld(Vector2 worldPoint, double now)
        {
            if (!DragToWorld(worldPoint, now)) { CancelDrag(); return false; }
            var entry = entries[heldId];
            string id = heldId;
            Vector2 inputVelocity = Vector2.ClampMagnitude(
                ReleaseVelocity(worldPoint, now, out float swipeDistance), config.ThrowMaxSwipeSpeed);
            heldId = null;
            samples.Clear();
            entry.Held = false;
            entry.View.HitSphere.isTrigger = false;

            // Side exits keep their existing screen-to-screen handoff even on a diagonal flick.
            bool outwardSide = (worldPoint.x <= bounds.xMin && inputVelocity.x < 0f)
                || (worldPoint.x >= bounds.xMax && inputVelocity.x > 0f);
            if (entry.View.Kind == LabOrbKind.Combined && !outwardSide
                && LabThrowMath.IsThrowGesture(inputVelocity, swipeDistance, config))
            {
                entry.Locked = true;
                StopMotion(entry.View.Body);
                SetBodyMode(entry);
                float releaseX01 = Mathf.InverseLerp(bounds.xMin, bounds.xMax, worldPoint.x);
                ThrowRequested?.Invoke(new LabOrbThrowGesture(id, inputVelocity, releaseX01, swipeDistance));
                return true;
            }

            if (entry.View.Kind != LabOrbKind.Combined)
            {
                Entry target = FindOppositeOverlap(entry);
                if (target != null)
                {
                    entry.Locked = true;
                    target.Locked = true;
                    entry.PendingPartnerId = target.View.OrbId;
                    target.PendingPartnerId = id;
                    StopMotion(entry.View.Body);
                    StopMotion(target.View.Body);
                    SetBodyMode(entry);
                    SetBodyMode(target);
                    Vector2 middle = ToNormalized((XY(entry.View.Body.position) + XY(target.View.Body.position)) * .5f);
                    CombineRequested?.Invoke(id, target.View.OrbId, middle);
                    return true;
                }
            }

            SetBodyMode(entry);
            Vector2 worldVelocity = inputVelocity * bounds.width;
            entry.View.Body.linearVelocity = OnPlane(Vector2.ClampMagnitude(worldVelocity,
                config.OrbMaxReleaseSpeed * bounds.width));
            return true;
        }

        public void CancelDrag()
        {
            if (heldId != null && entries.TryGetValue(heldId, out var entry))
            {
                entry.Held = false;
                entry.View.HitSphere.isTrigger = false;
                StopMotion(entry.View.Body);
                SetBodyMode(entry);
            }
            heldId = null;
            samples.Clear();
        }

        private void Update()
        {
            if (!configured || !interactionEnabled || uiInputBlocked) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (heldId != null)
            {
                if (heldByTouch)
                {
                    var touch = Touchscreen.current?.primaryTouch;
                    if (touch == null) { CancelDrag(); return; }
                    if (!TryScreenToWorld(touch.position.ReadValue(), out var point)) { CancelDrag(); return; }
                    if (touch.press.wasReleasedThisFrame) EndDragAtWorld(point, now);
                    else if (touch.press.isPressed) DragToWorld(point, now);
                    else CancelDrag();
                }
                else
                {
                    var mouse = Mouse.current;
                    if (mouse == null) { CancelDrag(); return; }
                    if (!TryScreenToWorld(mouse.position.ReadValue(), out var point)) { CancelDrag(); return; }
                    if (mouse.leftButton.wasReleasedThisFrame) EndDragAtWorld(point, now);
                    else if (mouse.leftButton.isPressed) DragToWorld(point, now);
                    else CancelDrag();
                }
                return;
            }

            var primary = Touchscreen.current?.primaryTouch;
            if (primary != null && primary.press.wasPressedThisFrame
                && TryScreenToWorld(primary.position.ReadValue(), out var touchPoint)
                && TryBeginDragAtWorld(touchPoint, now))
            {
                heldByTouch = true;
                return;
            }
            var pointer = Mouse.current;
            if (pointer != null && pointer.leftButton.wasPressedThisFrame
                && TryScreenToWorld(pointer.position.ReadValue(), out var mousePoint)
                && TryBeginDragAtWorld(mousePoint, now))
                heldByTouch = false;
        }

        private void FixedUpdate()
        {
            if (!configured) return;
            updateEntries.Clear();
            updateEntries.AddRange(entries.Values);
            foreach (var entry in updateEntries)
            {
                if (entry.View == null || !entry.LocalOwner || entry.Locked || entry.SpawnLocked || entry.Held || entry.EdgePending) continue;
                var body = entry.View.Body;
                Vector2 velocity = XY(body.linearVelocity);
                float speed = velocity.magnitude;
                // Config motion is expressed in board widths, while Rigidbody uses world units.
                // Convert at this boundary so different camera/board sizes retain the same feel.
                if (speed <= config.OrbStopSpeed * bounds.width) velocity = Vector2.zero;
                else velocity = velocity.normalized * Mathf.Max(0f,
                    speed - config.OrbFloorDeceleration * bounds.width * Time.fixedDeltaTime);
                body.linearVelocity = OnPlane(velocity);
                if (velocity.x == 0f) continue;
                Vector2 position = XY(body.position);
                bool left = position.x <= bounds.xMin && velocity.x < 0f;
                bool right = position.x >= bounds.xMax && velocity.x > 0f;
                if (!left && !right) continue;
                var crossing = new LabOrbEdgeCrossing(entry.View.OrbId, right,
                    Mathf.InverseLerp(bounds.yMin, bounds.yMax, position.y), velocity / bounds.width);
                entry.EdgePending = true;
                entry.PendingEdge = crossing;
                body.position = OnPlane(Clamp(position));
                StopMotion(body);
                SetBodyMode(entry);
                EdgeCrossed?.Invoke(crossing);
            }
        }

        private Entry FindOppositeOverlap(Entry dragged)
        {
            Entry nearest = null;
            float best = Mathf.Pow(config.OrbRadius * 2f, 2f);
            foreach (var candidate in entries.Values)
            {
                if (candidate == dragged || candidate.View.Kind == LabOrbKind.Combined
                    || candidate.View.Kind == dragged.View.Kind || !candidate.LocalOwner
                    || candidate.Locked || candidate.SpawnLocked || candidate.EdgePending || candidate.Held) continue;
                float distance = (XY(candidate.View.Body.position) - XY(dragged.View.Body.position)).sqrMagnitude;
                if (distance <= best) { best = distance; nearest = candidate; }
            }
            return nearest;
        }

        private void BuildHorizontalWalls()
        {
            if (wallRoot == null)
            {
                wallRoot = new GameObject("Physics walls - top and bottom");
                wallRoot.transform.SetParent(transform, false);
            }
            wallRoot.layer = gameObject.layer;
            while (wallRoot.transform.childCount > 0)
            {
                var old = wallRoot.transform.GetChild(0);
                var collider = old.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                old.SetParent(null);
                Destroy(old.gameObject);
            }
            CreateWall("Top", bounds.yMax + config.OrbRadius + WallThickness * .5f);
            CreateWall("Bottom", bounds.yMin - config.OrbRadius - WallThickness * .5f);
        }

        private void CreateWall(string label, float worldY)
        {
            var wall = new GameObject(label);
            wall.layer = gameObject.layer;
            wall.transform.SetParent(wallRoot.transform, false);
            wall.transform.position = new Vector3(bounds.center.x, worldY, 0f);
            var collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(bounds.width + config.OrbRadius * 4f,
                WallThickness, config.OrbRadius * 2f + WallThickness);
            collider.sharedMaterial = contactMaterial;
        }

        private void SetBodyMode(Entry entry)
        {
            var body = entry.View.Body;
            bool movable = entry.LocalOwner && !entry.Held && !entry.Locked && !entry.SpawnLocked && !entry.EdgePending;
            bool desiredKinematic = !movable;
            if (body.isKinematic != desiredKinematic)
            {
                StopMotion(body);
                body.isKinematic = desiredKinematic;
                if (movable) body.linearVelocity = Vector3.zero;
            }
            body.detectCollisions = true;
            entry.View.HitSphere.isTrigger = entry.Held;
        }

        private static void Retire(Entry entry)
        {
            if (entry.View == null) return;
            // Destroy is deferred in Play Mode. Remove its collider immediately so a
            // transfer/retry snapshot cannot collide with a visually removed orb.
            StopMotion(entry.View.Body);
            entry.View.Body.detectCollisions = false;
            entry.View.HitSphere.enabled = false;
            Destroy(entry.View.gameObject);
        }

        private void AddSample(Vector2 worldPoint, double now)
        {
            if (samples.Count > 0 && now < samples[samples.Count - 1].Time) { samples.Clear(); return; }
            if (samples.Count > 0 && now == samples[samples.Count - 1].Time)
                samples[samples.Count - 1] = new PointerSample(worldPoint, now);
            else samples.Add(new PointerSample(worldPoint, now));
            while (samples.Count > 2 && samples[1].Time < now - ReleaseSampleWindow)
                samples.RemoveAt(0);
            if (samples.Count > 32) samples.RemoveAt(0);
        }

        private Vector2 ReleaseVelocity(Vector2 endPoint, double now, out float distanceInBoardWidths)
        {
            distanceInBoardWidths = 0f;
            if (samples.Count < 2) return Vector2.zero;
            double earliest = now - ReleaseSampleWindow;
            var first = samples[0];
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].Time < earliest) continue;
                if (samples[i - 1].Time <= earliest && samples[i].Time > earliest)
                {
                    var before = samples[i - 1];
                    var after = samples[i];
                    float portion = (float)((earliest - before.Time) / (after.Time - before.Time));
                    first = new PointerSample(Vector2.LerpUnclamped(before.Position, after.Position, portion), earliest);
                }
                else first = samples[i - 1];
                break;
            }
            double elapsed = now - first.Time;
            Vector2 displacement = endPoint - first.Position;
            distanceInBoardWidths = displacement.magnitude / bounds.width;
            return elapsed > .001d ? displacement / (float)(elapsed * bounds.width) : Vector2.zero;
        }

        private bool TryScreenToWorld(Vector2 screenPoint, out Vector2 worldPoint)
        {
            worldPoint = default;
            if (inputCamera == null) return false;
            var ray = inputCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance) || distance < 0f) return false;
            worldPoint = ray.GetPoint(distance);
            return Finite(worldPoint);
        }

        private Vector2 ToWorld(Vector2 normalized) => new Vector2(
            Mathf.Lerp(bounds.xMin, bounds.xMax, Mathf.Clamp01(normalized.x)),
            Mathf.Lerp(bounds.yMin, bounds.yMax, Mathf.Clamp01(normalized.y)));
        private Vector2 ToNormalized(Vector2 world) => new Vector2(
            Mathf.Clamp01((world.x - bounds.xMin) / bounds.width),
            Mathf.Clamp01((world.y - bounds.yMin) / bounds.height));
        private Vector2 Clamp(Vector2 world) => new Vector2(
            Mathf.Clamp(world.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(world.y, bounds.yMin, bounds.yMax));
        private static Vector2 XY(Vector3 value) => new Vector2(value.x, value.y);
        private static Vector3 OnPlane(Vector2 value) => new Vector3(value.x, value.y, 0f);
        private static void StopMotion(Rigidbody body)
        {
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        }
        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }
        private void RequireConfigured()
        {
            if (!configured) throw new InvalidOperationException("Configure the board before adding orbs.");
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);

        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private void OnDisable() => CancelDrag();
        private void OnDestroy()
        {
            if (contactMaterial != null) Destroy(contactMaterial);
        }
    }
}
