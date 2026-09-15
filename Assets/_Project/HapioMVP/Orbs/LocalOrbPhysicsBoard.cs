using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    /// <summary>A local boundary observation; the controller still needs Host ownership approval.</summary>
    public readonly struct OrbEdgeCrossing
    {
        public string OrbId { get; }
        public bool ToRight { get; }
        public float Height01 { get; }
        public Vector2 VelocityBoardWidthsPerSecond { get; }

        public OrbEdgeCrossing(string orbId, bool toRight, float height01, Vector2 velocityBoardWidthsPerSecond)
        {
            OrbId = orbId; ToRight = toRight; Height01 = height01;
            VelocityBoardWidthsPerSecond = velocityBoardWidthsPerSecond;
        }
    }

    /// <summary>All distances and speeds are world units; the caller converts its shared board-width tuning.</summary>
    public readonly struct OrbPhysicsTuning
    {
        public float Restitution { get; }
        public float FloorDeceleration { get; }
        public float StopSpeed { get; }
        public float MaxReleaseSpeed { get; }
        public float SampleWindow { get; }
        public float ContactFriction { get; }
        public OrbPhysicsTuning(float restitution, float floorDeceleration, float stopSpeed,
            float maxReleaseSpeed, float sampleWindow, float contactFriction = .2f)
        {
            if (!Valid(restitution) || restitution < 0 || restitution > 1 ||
                !Valid(floorDeceleration) || floorDeceleration < 0 || !Valid(stopSpeed) || stopSpeed < 0 ||
                !Valid(maxReleaseSpeed) || maxReleaseSpeed <= 0 || stopSpeed > maxReleaseSpeed ||
                !Valid(sampleWindow) || sampleWindow <= 0 || sampleWindow > 1 ||
                !Valid(contactFriction) || contactFriction < 0 || contactFriction > 1)
                throw new ArgumentOutOfRangeException(nameof(restitution), "Finite physical tuning in its supported range is required.");
            Restitution = restitution; FloorDeceleration = floorDeceleration; StopSpeed = stopSpeed;
            MaxReleaseSpeed = maxReleaseSpeed; SampleWindow = sampleWindow; ContactFriction = contactFriction;
        }
        internal static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal bool SameAs(OrbPhysicsTuning other) => Restitution == other.Restitution && FloorDeceleration == other.FloorDeceleration
            && StopSpeed == other.StopSpeed && MaxReleaseSpeed == other.MaxReleaseSpeed && SampleWindow == other.SampleWindow
            && ContactFriction == other.ContactFriction;
    }

    /// <summary>
    /// Owner-local presentation physics only. It has no gameplay session, request, combination,
    /// or attack dependency. Opt-in horizontal crossings are observations, never ownership approval.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalOrbPhysicsBoard : MonoBehaviour
    {
        private sealed class Entry
        {
            public OrbView View;
            public Rigidbody2D Body;
            public CircleCollider2D Collider;
            public bool Held, Locked, EdgePending;
            public OrbEdgeCrossing PendingEdge;
            public readonly List<Sample> Samples = new List<Sample>();
        }
        private readonly struct Sample
        {
            public readonly Vector2 Position;
            public readonly double Time;
            public Sample(Vector2 position, double time) { Position = position; Time = time; }
        }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<string> scratchIds = new List<string>();
        private readonly List<string> physicsStepIds = new List<string>();
        private GameObject wallRoot;
        private PhysicsMaterial2D material;
        private OrbPhysicsTuning tuning;
        private float radius;
        private bool configured, paused;
        private bool horizontalPassage;
        private float boardWorldWidth;
        public int Count => entries.Count;
        public Rect CenterBounds { get; private set; }
        public bool Paused => paused;
        public bool HorizontalPassageEnabled => horizontalPassage;
        public event Action<OrbEdgeCrossing> EdgeCrossed;

        /// <summary>Use the whole workspace width, not its radius/label-padded centre travel range.</summary>
        public void ConfigureHorizontalPassage(bool enabled, float worldWidth)
        {
            if (!OrbPhysicsTuning.Valid(worldWidth) || worldWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(worldWidth));
            if (horizontalPassage == enabled && boardWorldWidth == worldWidth) return;
            bool changedMode = horizontalPassage != enabled;
            horizontalPassage = enabled; boardWorldWidth = worldWidth;
            if (!configured) return;
            if (changedMode) BuildWalls();
            CopyIds();
            foreach (string id in scratchIds)
            {
                if (!TryEntry(id, out var entry)) continue;
                entry.EdgePending = false; entry.Held = false; entry.Samples.Clear();
                Stop(entry); SetMode(entry); entry.Body.position = Clamp(entry.Body.position);
            }
        }

        public void Configure(Rect worldCenterBounds, float radiusWorld, OrbPhysicsTuning value)
        {
            if (!Finite(worldCenterBounds.min) || !Finite(worldCenterBounds.max) || worldCenterBounds.width <= 0 || worldCenterBounds.height <= 0
                || !OrbPhysicsTuning.Valid(radiusWorld) || radiusWorld <= 0)
                throw new ArgumentOutOfRangeException(nameof(worldCenterBounds));
            // Re-run validation: a default readonly struct must not bypass its constructor.
            var checkedValue = new OrbPhysicsTuning(value.Restitution, value.FloorDeceleration, value.StopSpeed,
                value.MaxReleaseSpeed, value.SampleWindow, value.ContactFriction);
            if (configured && CenterBounds == worldCenterBounds && radius == radiusWorld && tuning.SameAs(checkedValue)) return;
            CenterBounds = worldCenterBounds; radius = radiusWorld; tuning = checkedValue; configured = true;
            if (material == null) material = new PhysicsMaterial2D("C6 Local Orb Contact") { hideFlags = HideFlags.DontSave };
            material.bounciness = tuning.Restitution; material.friction = tuning.ContactFriction;
            BuildWalls();
            CopyIds();
            foreach (string id in scratchIds)
            {
                if (!TryEntry(id, out var entry)) continue;
                entry.Held = false; entry.EdgePending = false; entry.Samples.Clear();
                SetColliderRadius(entry); SetMode(entry); Stop(entry);
                entry.Body.position = FindFreePosition(entry, Clamp(entry.Body.position));
            }
        }

        public void Register(OrbView view)
        {
            RequireConfigured();
            if (view == null || string.IsNullOrWhiteSpace(view.OrbId) || view.Collider == null)
                throw new ArgumentException("Register one configured OrbView.", nameof(view));
            if (entries.TryGetValue(view.OrbId, out var previous))
            {
                if (previous.View != view) throw new InvalidOperationException("An orb ID is already registered to another view.");
                return;
            }
            var body = view.GetComponent<Rigidbody2D>();
            if (body == null) body = view.gameObject.AddComponent<Rigidbody2D>();
            var entry = new Entry { View = view, Body = body, Collider = view.Collider };
            body.gravityScale = 0; body.linearDamping = 0; body.angularDamping = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.useAutoMass = false; body.mass = 1; body.simulated = true;
            entry.Collider.enabled = true; entry.Collider.offset = Vector2.zero;
            entry.Collider.sharedMaterial = material;
            SetColliderRadius(entry);
            entries.Add(view.OrbId, entry);
            SetMode(entry); Stop(entry);
            entry.Body.position = FindFreePosition(entry, Clamp(view.transform.position));
            foreach (Transform wall in wallRoot.transform) wall.gameObject.layer = view.gameObject.layer;
        }

        public void Remove(string id)
        {
            if (id == null || !entries.TryGetValue(id, out var entry)) return;
            entries.Remove(id);
            if (entry.Body != null)
            {
                Stop(entry); entry.Body.bodyType = RigidbodyType2D.Kinematic;
                entry.Body.simulated = false;
            }
            if (entry.Collider != null) { entry.Collider.isTrigger = true; entry.Collider.sharedMaterial = null; }
            entry.Samples.Clear();
        }
        public void Clear()
        {
            CopyIds();
            foreach (string id in scratchIds) Remove(id);
        }
        public bool Grab(string id, double now)
        {
            if (!TryEntry(id, out var entry) || paused || !isActiveAndEnabled || entry.Locked || entry.Held || entry.EdgePending || !Finite(now)) return false;
            entry.Held = true; entry.Samples.Clear();
            AddSample(entry, entry.Body.position, now); SetMode(entry); Stop(entry);
            return true;
        }
        public void SetPosition(string id, Vector2 world, bool sample, double now)
        {
            if (!TryEntry(id, out var entry) || !Finite(world)) return;
            Vector2 next = Clamp(world);
            if (sample)
            {
                if (!entry.Held || entry.Locked || entry.EdgePending || paused || !Finite(now)) return;
                if (entry.Samples.Count > 0 && now < entry.Samples[entry.Samples.Count - 1].Time) return;
                AddSample(entry, next, now);
            }
            else
            {
                entry.Samples.Clear(); Stop(entry);
                // This is an explicit spawn/ownership/layout teleport, not another drag sample.
                if (!entry.Held && !entry.Locked) next = FindFreePosition(entry, next);
            }
            entry.Body.position = next;
            // Rigidbody position is authoritative. Keep immediate rendering/hit queries in this frame aligned.
            entry.View.transform.position = new Vector3(next.x, next.y, entry.View.transform.position.z);
        }
        public bool Release(string id, double now, bool applyInertia)
        {
            if (!TryEntry(id, out var entry) || !entry.Held) return false;
            Vector2 velocity = Vector2.zero;
            if (applyInertia && !entry.Locked && !paused && isActiveAndEnabled && Finite(now))
                velocity = ReleaseVelocity(entry, now);
            entry.Held = false; entry.Samples.Clear(); SetMode(entry); Stop(entry);
            if (!entry.Locked && !paused && isActiveAndEnabled)
            {
                entry.Body.position = FindFreePosition(entry, Clamp(entry.Body.position));
                entry.Body.linearVelocity = velocity;
                if (velocity.sqrMagnitude > 0) entry.Body.WakeUp();
            }
            return true;
        }
        public void SetLocked(string id, bool locked)
        {
            if (!TryEntry(id, out var entry) || entry.Locked == locked) return;
            entry.Locked = locked; entry.Held = false; entry.Samples.Clear(); SetMode(entry); Stop(entry);
            if (!locked && !entry.EdgePending && !paused && isActiveAndEnabled)
                entry.Body.position = FindFreePosition(entry, Clamp(entry.Body.position));
        }
        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value; CopyIds();
            foreach (string id in scratchIds)
            {
                if (!TryEntry(id, out var entry)) continue;
                entry.Held = false; entry.EdgePending = false; entry.Samples.Clear(); SetMode(entry); Stop(entry);
                if (!paused && !entry.Locked) entry.Body.position = FindFreePosition(entry, Clamp(entry.Body.position));
            }
        }
        public bool TryGetVelocity(string id, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (!TryEntry(id, out var entry)) return false;
            velocity = entry.Body.linearVelocity; return true;
        }

        public bool TryGetPendingEdge(string id, out OrbEdgeCrossing crossing)
        {
            crossing = default;
            if (!TryEntry(id, out var entry) || !entry.EdgePending) return false;
            crossing = entry.PendingEdge; return true;
        }

        /// <summary>A rejected/full destination leaves the existing orb stopped, with no repeat request or bounce.</summary>
        public bool ResolveRejectedEdge(string id)
        {
            if (!TryEntry(id, out var entry) || !entry.EdgePending) return false;
            Vector2 position = new Vector2(entry.PendingEdge.ToRight ? CenterBounds.xMax : CenterBounds.xMin,
                Mathf.Lerp(CenterBounds.yMin, CenterBounds.yMax, entry.PendingEdge.Height01));
            entry.EdgePending = false; entry.Samples.Clear();
            Stop(entry); SetMode(entry); SetExactPosition(entry, position);
            return true;
        }

        /// <summary>Called once per confirmed ownership epoch. The caller already accounts for transit friction.</summary>
        public bool ResumeTransferred(string id, bool arrivedFromLeft, float height01, Vector2 normalizedVelocity)
        {
            if (!horizontalPassage || !TryEntry(id, out var entry) || paused || !isActiveAndEnabled || entry.Locked ||
                !OrbPhysicsTuning.Valid(height01) || height01 < 0 || height01 > 1 || !Finite(normalizedVelocity)) return false;
            Vector2 velocity = normalizedVelocity * boardWorldWidth;
            if (!Finite(velocity)) return false;
            entry.Held = false; entry.EdgePending = false; entry.Samples.Clear();
            // Preserve the relative height and direction. Real contacts, rather than a spawn
            // search/teleport, resolve any occupied receiving edge during the next simulation.
            Vector2 position = new Vector2(arrivedFromLeft ? CenterBounds.xMin : CenterBounds.xMax,
                Mathf.Lerp(CenterBounds.yMin, CenterBounds.yMax, height01));
            entry.Body.position = position;
            entry.View.transform.position = new Vector3(position.x, position.y, entry.View.transform.position.z);
            SetMode(entry); Stop(entry); entry.Body.linearVelocity = velocity;
            if (velocity.sqrMagnitude > 0) entry.Body.WakeUp();
            return true;
        }

        private void FixedUpdate()
        {
            if (!configured || paused) return;
            // A Host callback may synchronously reconcile views and call other board methods.
            // Keep this traversal independent of their reusable scratch list.
            physicsStepIds.Clear(); physicsStepIds.AddRange(entries.Keys); physicsStepIds.Sort(StringComparer.Ordinal);
            foreach (string id in physicsStepIds)
            {
                if (!TryEntry(id, out var entry)) { Remove(id); continue; }
                if (entry.Held || entry.Locked || entry.EdgePending || !entry.View.isActiveAndEnabled) { Stop(entry); continue; }
                Vector2 velocity = entry.Body.linearVelocity;
                if (!Finite(velocity)) velocity = Vector2.zero;
                Vector2 position = entry.Body.position;
                bool crossLeft = position.x <= CenterBounds.xMin && velocity.x < 0;
                bool crossRight = position.x >= CenterBounds.xMax && velocity.x > 0;
                if (horizontalPassage && (crossLeft || crossRight))
                {
                    // This velocity belongs to the completed physics step. Do not charge a
                    // second local friction step before the controller timestamps its transfer.
                    entry.PendingEdge = new OrbEdgeCrossing(id, crossRight,
                        Mathf.Clamp01((position.y - CenterBounds.yMin) / CenterBounds.height), velocity / boardWorldWidth);
                    entry.EdgePending = true;
                    entry.Samples.Clear(); Stop(entry); SetMode(entry);
                    SetExactPosition(entry, Clamp(position));
                    EdgeCrossed?.Invoke(entry.PendingEdge);
                    continue; // The callback may have removed or replaced this entry.
                }
                velocity = Vector2.MoveTowards(velocity, Vector2.zero, tuning.FloorDeceleration * Time.fixedDeltaTime);
                if (velocity.magnitude <= tuning.StopSpeed) velocity = Vector2.zero;
                entry.Body.linearVelocity = velocity;
                entry.Body.angularVelocity = 0;
                // Vertical geometry recovery remains reflective. Legacy scenes also retain
                // their physical side walls and horizontal recovery.
                Vector2 clamped = Clamp(position);
                if (position != clamped)
                {
                    if (!horizontalPassage && (position.x < CenterBounds.xMin && velocity.x < 0 || position.x > CenterBounds.xMax && velocity.x > 0))
                        velocity.x = -velocity.x * tuning.Restitution;
                    if (position.y < CenterBounds.yMin && velocity.y < 0 || position.y > CenterBounds.yMax && velocity.y > 0)
                        velocity.y = -velocity.y * tuning.Restitution;
                    entry.Body.position = clamped; entry.Body.linearVelocity = velocity;
                }
            }
        }
        private void SetMode(Entry entry)
        {
            bool frozen = paused || !isActiveAndEnabled || entry.Held || entry.Locked || entry.EdgePending;
            entry.Body.bodyType = frozen ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            entry.Collider.isTrigger = frozen; entry.Body.simulated = true;
        }
        private static void Stop(Entry entry)
        { if (entry.Body != null) { entry.Body.linearVelocity = Vector2.zero; entry.Body.angularVelocity = 0; } }
        private static void SetExactPosition(Entry entry, Vector2 position)
        {
            // A body-type change can reconcile from the interpolated Transform. Update both
            // after changing mode so approval/rejection cannot rewind to that older display pose.
            entry.Body.position = position;
            entry.View.transform.position = new Vector3(position.x, position.y, entry.View.transform.position.z);
        }
        private void SetColliderRadius(Entry entry)
        {
            Vector3 scale = entry.View.transform.lossyScale;
            float maximum = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            if (!OrbPhysicsTuning.Valid(maximum) || maximum <= .000001f) throw new InvalidOperationException("An orb needs finite, nonzero world scale.");
            entry.Collider.radius = radius / maximum;
        }
        private Vector2 ReleaseVelocity(Entry entry, double now)
        {
            if (entry.Samples.Count == 0 || now < entry.Samples[entry.Samples.Count - 1].Time) return Vector2.zero;
            // Adding the stationary final point makes a long hold discard earlier movement.
            AddSample(entry, entry.Body.position, now);
            if (entry.Samples.Count < 2) return Vector2.zero;
            Sample first = entry.Samples[0], last = entry.Samples[entry.Samples.Count - 1];
            double elapsed = last.Time - first.Time;
            if (elapsed <= .000001 || elapsed > tuning.SampleWindow * 1.001) return Vector2.zero;
            Vector2 velocity = (last.Position - first.Position) / (float)elapsed;
            if (!Finite(velocity)) return Vector2.zero;
            velocity = Vector2.ClampMagnitude(velocity, tuning.MaxReleaseSpeed);
            return velocity.magnitude <= tuning.StopSpeed ? Vector2.zero : velocity;
        }
        private void AddSample(Entry entry, Vector2 position, double now)
        {
            if (entry.Samples.Count > 0 && entry.Samples[entry.Samples.Count - 1].Time == now)
                entry.Samples[entry.Samples.Count - 1] = new Sample(position, now);
            else entry.Samples.Add(new Sample(position, now));
            double cutoff = now - tuning.SampleWindow;
            while (entry.Samples.Count > 1 && entry.Samples[0].Time < cutoff) entry.Samples.RemoveAt(0);
            // Bound unusual high-frequency callers without allowing unbounded allocation.
            if (entry.Samples.Count > 256) entry.Samples.RemoveRange(0, entry.Samples.Count - 256);
        }
        private Vector2 FindFreePosition(Entry subject, Vector2 preferred)
        {
            if (Free(subject, preferred)) return preferred;
            float step = radius * 2.02f;
            int columns = Mathf.Min(256, Mathf.CeilToInt(CenterBounds.width / step) + 1);
            int rows = Mathf.Min(256, Mathf.CeilToInt(CenterBounds.height / step) + 1);
            float best = float.PositiveInfinity;
            Vector2 selected = preferred;
            for (int row = 0; row <= rows; row++)
                for (int column = 0; column <= columns; column++)
                {
                    Vector2 candidate = new Vector2(Mathf.Min(CenterBounds.xMax, CenterBounds.xMin + column * step),
                        Mathf.Min(CenterBounds.yMax, CenterBounds.yMin + row * step));
                    float distance = (candidate - preferred).sqrMagnitude;
                    if (distance >= best || !Free(subject, candidate)) continue;
                    best = distance; selected = candidate;
                }
            // At genuine capacity saturation keep a bounded position and no initial impulse;
            // the caller's storage limit remains authoritative. No new game request is manufactured.
            return selected;
        }
        private bool Free(Entry subject, Vector2 point)
        {
            float squared = radius * radius * 4.0004f;
            foreach (var entry in entries.Values)
                if (entry != subject && entry.Body != null && entry.View != null && entry.View.isActiveAndEnabled
                    && !entry.Held && !entry.Locked && (entry.Body.position - point).sqrMagnitude < squared) return false;
            return true;
        }
        private void BuildWalls()
        {
            if (wallRoot != null) { wallRoot.SetActive(false); Destroy(wallRoot); }
            wallRoot = new GameObject("C6 Local Orb Walls"); wallRoot.transform.SetParent(transform, false);
            wallRoot.layer = gameObject.layer;
            foreach (var entry in entries.Values)
                if (entry.View != null) { wallRoot.layer = entry.View.gameObject.layer; break; }
            float thick = Mathf.Max(.05f, radius);
            float fullWidth = CenterBounds.width + radius * 2, fullHeight = CenterBounds.height + radius * 2;
            if (!horizontalPassage)
            {
                Wall("Left", new Vector2(CenterBounds.xMin - radius - thick * .5f, CenterBounds.center.y), new Vector2(thick, fullHeight + thick * 2));
                Wall("Right", new Vector2(CenterBounds.xMax + radius + thick * .5f, CenterBounds.center.y), new Vector2(thick, fullHeight + thick * 2));
            }
            Wall("Bottom", new Vector2(CenterBounds.center.x, CenterBounds.yMin - radius - thick * .5f), new Vector2(fullWidth + thick * 2, thick));
            Wall("Top", new Vector2(CenterBounds.center.x, CenterBounds.yMax + radius + thick * .5f), new Vector2(fullWidth + thick * 2, thick));
        }
        private void Wall(string name, Vector2 center, Vector2 size)
        {
            var wall = new GameObject(name); wall.transform.SetParent(wallRoot.transform, false);
            wall.transform.position = new Vector3(center.x, center.y, 0); wall.layer = wallRoot.layer;
            var collider = wall.AddComponent<BoxCollider2D>();
            Vector3 scale = wall.transform.lossyScale;
            collider.size = new Vector2(size.x / Mathf.Abs(scale.x), size.y / Mathf.Abs(scale.y));
            collider.sharedMaterial = material;
        }
        private bool TryEntry(string id, out Entry entry)
        { entry = null; return configured && id != null && entries.TryGetValue(id, out entry) && entry.View != null && entry.Body != null && entry.Collider != null; }
        private void CopyIds() { scratchIds.Clear(); scratchIds.AddRange(entries.Keys); scratchIds.Sort(StringComparer.Ordinal); }
        private Vector2 Clamp(Vector2 value) => new Vector2(Mathf.Clamp(value.x, CenterBounds.xMin, CenterBounds.xMax), Mathf.Clamp(value.y, CenterBounds.yMin, CenterBounds.yMax));
        private void RequireConfigured() { if (!configured) throw new InvalidOperationException("Configure the local board before registering a view."); }
        private static bool Finite(Vector2 value) => OrbPhysicsTuning.Valid(value.x) && OrbPhysicsTuning.Valid(value.y);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        private void OnDisable()
        {
            foreach (var entry in entries.Values)
            { entry.Held = false; entry.EdgePending = false; entry.Samples.Clear(); if (entry.Body != null && entry.Collider != null) { SetMode(entry); Stop(entry); } }
        }
        private void OnEnable()
        {
            if (!configured) return;
            foreach (var entry in entries.Values)
                if (entry.Body != null && entry.Collider != null) { SetMode(entry); Stop(entry); }
        }
        private void OnDestroy()
        {
            Clear();
            if (wallRoot != null) { wallRoot.SetActive(false); Destroy(wallRoot); }
            if (material != null) Destroy(material);
        }
    }
}
