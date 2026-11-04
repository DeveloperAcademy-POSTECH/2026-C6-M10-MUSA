using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>How the visible rope is drawn. Presentation only: none of these changes the swing, pivot or hitbox (#507).</summary>
    public enum LabRopeStyle
    {
        /// <summary>A fixed upper rope on the root and a swinging lower rope on the pivot (bends in mid-air).</summary>
        Segmented,
        /// <summary>One straight rope from the fixed anchor to the gourd.</summary>
        Straight,
        /// <summary>One quadratic Bézier rope: vertical at the anchor, along the gourd axis at the gourd.</summary>
        Curved,
    }

    /// <summary>
    /// A damped pendulum with a freely turning body. The hitbox and art share both pivots.
    /// Physics owns the fixed-step pose; the drawn pose is interpolated from the last two fixed poses (#454), but only
    /// while the render pipeline draws: it is written at the start of rendering and the fixed pose is restored at the
    /// end. Every script, physics query, network handler and coroutine therefore reads the fixed pose — the Host's
    /// paint flow samples VisualMount, projectiles meet the collider, jigwi caps raycast the shell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabHangingTarget : MonoBehaviour
    {
        /// <summary>The parts of the hanging pose that move: swing angles, unwrapped yaw, rope offset and pivot height.</summary>
        public struct RigState
        {
            public Vector2 Swing;
            public float Yaw, RopeOffset, PivotHeight;

            /// <summary>Linear between two fixed poses. Yaw stays unwrapped, so a full turn is never taken the short way.</summary>
            public static RigState Lerp(RigState from, RigState to, float alpha)
            {
                float t = float.IsNaN(alpha) ? 1f : Mathf.Clamp01(alpha);
                return new RigState
                {
                    Swing = from.Swing + (to.Swing - from.Swing) * t,
                    Yaw = from.Yaw + (to.Yaw - from.Yaw) * t,
                    RopeOffset = from.RopeOffset + (to.RopeOffset - from.RopeOffset) * t,
                    PivotHeight = to.PivotHeight
                };
            }
        }

        [SerializeField, Min(0f), Tooltip("Higher values produce a stronger swing from the same impact. Range: 0–20.")]
        private float impactStrength = 12f;
        [SerializeField, Min(0f), Tooltip("How quickly swing momentum fades. Zero keeps oscillating. Range: 0–20.")]
        private float swingDamping = 1.4f;
        [SerializeField, Min(1f), Tooltip("Maximum swing from vertical, in degrees.")]
        private float maximumAngle = 40f;
        [SerializeField, Min(0f), Tooltip("How strongly the target returns to its resting position. Range: 0–100.")]
        private float returnStrength = 10f;

        [SerializeField, Min(.1f), Tooltip("Visible rope length in world units, from the ceiling pivot to the top of the target. Range: 0.1–8.")]
        private float ropeLength = .85f;
        [SerializeField, Min(0f), Tooltip("Extra rope above the physical suspension pivot. Presentation only; does not change the swing arm or hitbox.")]
        private float suspensionVisualExtension;
        [SerializeField, Tooltip("How the visible rope is drawn. Presentation only; Segmented keeps the original upper and lower ropes.")]
        private LabRopeStyle ropeStyle = LabRopeStyle.Segmented;

        [SerializeField, Min(0f), Tooltip("Downward bounce impulse per unit of impact speed. Range: 0–2.")]
        private float bounceStrength = .18f;
        [SerializeField, Min(0f), Tooltip("Spring force that restores the rope's resting length. Range: 0–100.")]
        private float bounceReturnStrength = 45f;
        [SerializeField, Min(0f), Tooltip("How quickly vertical bouncing fades. Range: 0–20.")]
        private float bounceDamping = 3f;
        [SerializeField, Min(0f), Tooltip("Maximum extra rope length in world units. Zero disables vertical bounce. Range: 0–3.")]
        private float maximumExtension = .5f;
        [SerializeField, Range(0f, 1f), Tooltip("Automatic up/down rope motion amplitude. Zero disables the cycle.")]
        private float cycleAmplitude;
        [SerializeField, Range(.5f, 10f), Tooltip("Seconds for one complete rope length cycle.")]
        private float cyclePeriod = 3f;

        public float BounceStrength => Safe(bounceStrength, .18f, 0f, 2f);
        public float BounceReturnStrength => Safe(bounceReturnStrength, 45f, 0f, 100f);
        public float BounceDamping => Safe(bounceDamping, 3f, 0f, 20f);
        public float MaximumExtension => Safe(maximumExtension, .5f, 0f, 3f);
        public float VerticalOffset => verticalOffset;
        public float VerticalVelocity => verticalVelocity;
        public float CycleElapsed => cycleElapsed;
        public float CurrentRopeOffset => currentRopeOffset;
        public float CycleAmplitude => Safe(cycleAmplitude, 0f, 0f, 1f);
        public float CyclePeriod => Safe(cyclePeriod, 3f, .5f, 10f);
        public float RopeLength => Safe(ropeLength, .85f, .1f, 8f);
        public const int CurvedRopeSegments = 24;
        public const float RopeDiameter = .055f;
        public LabRopeStyle RopeStyle => ropeStyle;
        /// <summary>The continuous rope root (Straight/Curved), a child of this target; null until one is needed.</summary>
        public Transform ContinuousRope => continuousRope;
        public float ImpactStrength => Safe(impactStrength, 12f, 0f, 20f);
        public float SwingDamping => Safe(swingDamping, 1.4f, 0f, 20f);
        public float MaximumAngle => Safe(maximumAngle, 40f, 1f, 75f);
        public float ReturnStrength => Safe(returnStrength, 10f, 0f, 100f);
        // Configured from the session's LabConfig copy; these are only safe standalone defaults.
        private float spinStrength = 80f, spinDamping = 1.2f, maximumSpinSpeed = 720f;
        private Transform pivot, twistPivot, hangingBody, visualMount, rope, upperRope, ceilingBeam, ceilingPlate;
        private Transform continuousRope, ropeSocket;
        private Transform[] ropeSegments = Array.Empty<Transform>();
        private float drawnRopeStretch;
        private float verticalOffset, verticalVelocity, cycleElapsed, currentRopeOffset;
        private Vector2 angle, velocity;
        private float yawAngleDegrees, yawSpeedDegreesPerSecond;
        private Material bodyMaterial, ropeMaterial;
        private LabTarget mountedTarget;
        private Color bodyBaseColor;
        private float flash;
        private bool simulationHeld;
        private RigState previousPose, currentPose;
        private bool posesReady, drawingInterpolated;
        private float pivotHeight;
        public bool SimulationHeld => simulationHeld;
        /// <summary>Freeze physical swing, rope cycle and spin without changing global UI time.</summary>
        public void SetSimulationHeld(bool held) => simulationHeld = held;
        public Vector2 SwingAngles => angle;
        // Deliberately unwrapped: a value greater than 360 confirms a complete revolution.
        public float YawAngleDegrees => yawAngleDegrees;
        public float YawSpeedDegreesPerSecond => yawSpeedDegreesPerSecond;

        private void Awake()
        {
            var target = GetComponent<LabTarget>();
            if (target == null || target.Hitbox == null) return;
            pivot = new GameObject("Suspension Pivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.up * 1.75f;
            var rigidbody = pivot.gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            mountedTarget = target;
            Transform body = target.Hitbox.transform;
            visualMount = target.VisualMount;
            // Suspension moves the shared frame. The shell collider owns its independent
            // mesh-local offset and scale, exactly matching the mounted gourd surface.
            hangingBody = visualMount;
            twistPivot = new GameObject("Body Twist Pivot").transform;
            twistPivot.SetParent(pivot, false);
            visualMount.SetParent(twistPivot, true);
            body.SetParent(visualMount, true);
            // A fixture may have a plain diagnostic renderer. Never replace the real
            // gourd's paint material or reshape its collision mesh for a hit flash.
            var bodyRenderer = body.GetComponent<Renderer>();
            var sourceMaterial = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
            if (sourceMaterial != null && sourceMaterial.shader != null
                && sourceMaterial.shader.name.StartsWith("Universal Render Pipeline/"))
            {
                bodyMaterial = new Material(sourceMaterial);
                bodyBaseColor = bodyMaterial.HasProperty("_BaseColor")
                    ? bodyMaterial.GetColor("_BaseColor") : Color.white;
                bodyRenderer.sharedMaterial = bodyMaterial;
            }
            ropeMaterial = MakeMaterial(new Color(.64f, .52f, .32f), .1f);
            rope = Primitive("Rope", PrimitiveType.Cylinder, pivot,
                new Vector3(0, -.425f, 0), new Vector3(.055f, .425f, .055f), ropeMaterial);
            upperRope = Primitive("Upper Rope", PrimitiveType.Cylinder, transform,
                Vector3.zero, Vector3.one, ropeMaterial);
            ceilingBeam = Primitive("Ceiling Beam", PrimitiveType.Cube, transform,
                new Vector3(0, 1.85f, 0), new Vector3(5f, .18f, .5f), ropeMaterial);
            ceilingPlate = Primitive("Ceiling Plate", PrimitiveType.Cube, transform,
                new Vector3(0, 2.01f, 0), new Vector3(6f, .12f, 3f), ropeMaterial);
            ApplyRopeStyle();
            ApplyRopeLength();
            SnapPose();
        }

        /// <summary>
        /// Choose how the rope is drawn (#507). Straight/Curved hide the original rope renderers but keep computing
        /// their transforms; the swing, pivot height and hitbox are unchanged. Safe to call before Awake.
        /// </summary>
        public void SetRopeStyle(LabRopeStyle style)
        {
            if (!Enum.IsDefined(typeof(LabRopeStyle), style)) style = LabRopeStyle.Segmented;
            ropeStyle = style;
            if (pivot == null) return;
            ApplyRopeStyle();
            UpdateContinuousRope(drawnRopeStretch);
        }

        /// <summary>
        /// Where a continuous rope meets the gourd: a transform inside the hanging body (a model's rope socket).
        /// Null, or one outside this target, uses the top of the hitbox on the body axis, as the original rope does.
        /// </summary>
        public void SetRopeSocket(Transform socket)
        {
            ropeSocket = socket;
            if (pivot != null) UpdateContinuousRope(drawnRopeStretch);
        }

        /// <summary>Apply the session tuning without mutating the source LabConfig asset.</summary>
        public void ConfigureSpin(float strength, float damping, float maxSpeed)
        {
            spinStrength = Safe(strength, 80f, 0f, 200f);
            spinDamping = Safe(damping, 1.2f, 0f, 10f);
            maximumSpinSpeed = Safe(maxSpeed, 720f, 30f, 1440f);
            yawSpeedDegreesPerSecond = Mathf.Clamp(yawSpeedDegreesPerSecond,
                -maximumSpinSpeed, maximumSpinSpeed);
        }

        /// <summary>Apply the shared side-hit swing tuning for this round.</summary>
        public void ConfigureSwing(float impact, float maximum, float restoring, float damping)
        {
            impactStrength = Safe(impact, 12f, 0f, 20f);
            maximumAngle = Safe(maximum, 40f, 1f, 75f);
            returnStrength = Safe(restoring, 10f, 0f, 100f);
            swingDamping = Safe(damping, 1.4f, 0f, 20f);
            angle = Vector2.ClampMagnitude(angle, MaximumAngle);
            if (pivot != null)
            {
                pivot.localRotation = Quaternion.Euler(angle.x, 0, angle.y);
                UpdateContinuousRope(drawnRopeStretch);
            }
            SnapPose();
        }

        /// <summary>Use the Host-shared round settings for both the repeating motion and hit spring.</summary>
        public void ConfigureRopeMotion(float amplitude, float period, float hitBounce,
            float spring, float damping, float maxHitExtension)
        {
            cycleAmplitude = Safe(amplitude, 0f, 0f, 1f);
            cyclePeriod = Safe(period, 3f, .5f, 10f);
            bounceStrength = Safe(hitBounce, .18f, 0f, 2f);
            bounceReturnStrength = Safe(spring, 45f, 0f, 100f);
            bounceDamping = Safe(damping, 3f, 0f, 20f);
            maximumExtension = Safe(maxHitExtension, .5f, 0f, 3f);
            cycleElapsed = Mathf.Repeat(cycleElapsed, CyclePeriod);
            ApplyRopeLength();
            SnapPose();
        }

        /// <summary>
        /// Keep a remote target close to the Host, or pin its rope state while paused. With holdImpulse a
        /// running sample still blends the shared rope phase but leaves the hit offset and velocity alone,
        /// so a sample captured before a predicted local impact cannot undo that impact.
        /// </summary>
        public void ReconcileRope(float elapsed, float hitOffset, float hitVelocity, bool paused = false,
            bool holdImpulse = false)
        {
            if (pivot == null || !Finite(elapsed) || !Finite(hitOffset) || !Finite(hitVelocity)) return;
            float phase = Mathf.Repeat(elapsed, CyclePeriod);
            if (paused)
            {
                // FixedUpdate is stopped during a shared pause. Resolve the remaining error
                // once so identical paused snapshots cannot keep moving the collider and art.
                cycleElapsed = phase;
                verticalOffset = hitOffset;
                verticalVelocity = hitVelocity;
                ApplyRopeLength();
                SnapPose();
                return;
            }
            float phaseError = Mathf.DeltaAngle(cycleElapsed / CyclePeriod * 360f,
                phase / CyclePeriod * 360f) / 360f * CyclePeriod;
            if (Mathf.Abs(phaseError) > .25f) cycleElapsed = phase;
            else if (Mathf.Abs(phaseError) > .03f)
                cycleElapsed = Mathf.Repeat(cycleElapsed + phaseError * .25f, CyclePeriod);
            if (!holdImpulse)
            {
                if (Mathf.Abs(hitOffset - verticalOffset) > .08f)
                    verticalOffset = Mathf.Lerp(verticalOffset, hitOffset, .5f);
                if (Mathf.Abs(hitVelocity - verticalVelocity) > .5f)
                    verticalVelocity = Mathf.Lerp(verticalVelocity, hitVelocity, .5f);
            }
            ApplyRopeLength();
            ShiftPose();
        }

        /// <summary>Host-authoritative hit: return the signed yaw impulse for the impact packet.</summary>
        public float ApplyHostImpact(Vector3 hitPointWorld, Vector3 impactVelocity)
        {
            if (pivot == null || !FiniteVector(hitPointWorld) || !FiniteVector(impactVelocity))
                return 0f;
            Kick(impactVelocity);
            // Swing tilts the local spin axis. Project the lever arm onto its plane
            // before measuring signed torque about that axis.
            Vector3 axis = twistPivot.up;
            Vector3 lever = Vector3.ProjectOnPlane(hitPointWorld - hangingBody.position, axis);
            float impulse = Vector3.Dot(axis,
                Vector3.Cross(lever, impactVelocity)) * spinStrength;
            impulse = Mathf.Clamp(impulse, -maximumSpinSpeed, maximumSpinSpeed);
            ApplyYawImpulse(impulse);
            return impulse;
        }

        /// <summary>Replay an already approved host hit without recomputing a local hit point.</summary>
        public void ApplyRemoteImpact(Vector3 impactVelocity, float signedYawImpulse)
        {
            if (pivot == null || !FiniteVector(impactVelocity) || !Finite(signedYawImpulse)) return;
            Kick(impactVelocity);
            ApplyYawImpulse(signedYawImpulse);
        }

        /// <summary>Reconcile unwrapped yaw from the latest host snapshot.</summary>
        public void ReconcileYaw(float angleDegrees, float speedDegreesPerSecond, bool paused = false)
        {
            if (twistPivot == null || !Finite(angleDegrees) || !Finite(speedDegreesPerSecond)) return;
            if (paused)
            {
                // A paused target has one authoritative pose, rather than a moving blend.
                yawAngleDegrees = angleDegrees;
                yawSpeedDegreesPerSecond = Mathf.Clamp(speedDegreesPerSecond,
                    -maximumSpinSpeed, maximumSpinSpeed);
                ApplyYawRotation();
                SnapPose();
                return;
            }
            float difference = angleDegrees - yawAngleDegrees;
            // Snapshot angles describe the Host at send time. During a fast spin, ordinary
            // transit delay makes them older than the locally integrated pose. Avoid pulling
            // the body backward every 100 ms for an error explainable by <=200 ms of travel.
            float transitTolerance = Mathf.Max(2f,
                Mathf.Max(Mathf.Abs(speedDegreesPerSecond),
                    Mathf.Abs(yawSpeedDegreesPerSecond)) * .2f);
            if (Mathf.Abs(difference) <= transitTolerance) return;
            // A large divergence is normally a missed impact or round transition.
            yawAngleDegrees = Mathf.Abs(difference) > 90f
                ? angleDegrees : Mathf.Lerp(yawAngleDegrees, angleDegrees, .35f);
            yawSpeedDegreesPerSecond = Mathf.Abs(difference) > 90f
                ? Mathf.Clamp(speedDegreesPerSecond, -maximumSpinSpeed, maximumSpinSpeed)
                : Mathf.Lerp(yawSpeedDegreesPerSecond,
                    Mathf.Clamp(speedDegreesPerSecond, -maximumSpinSpeed, maximumSpinSpeed), .35f);
            ApplyYawRotation();
            ShiftPose();
        }

        private void ApplyYawImpulse(float impulse)
        {
            yawSpeedDegreesPerSecond = Mathf.Clamp(yawSpeedDegreesPerSecond + impulse,
                -maximumSpinSpeed, maximumSpinSpeed);
        }

        public void Kick(Vector3 incomingVelocity)
        {
            if (pivot == null) return;
            ApplyRopeLength();
            if (!KickContribution(incomingVelocity, out Vector2 swing, out float bounce)) return;
            velocity += swing;
            velocity = Vector2.ClampMagnitude(velocity, 220f);
            verticalVelocity = Mathf.Clamp(verticalVelocity + bounce, -8f, 8f);
            flash = .16f;
            ShiftPose();
        }

        /// <summary>What one impact adds to the swing and rope velocities, before the overall limits.</summary>
        private bool KickContribution(Vector3 incomingVelocity, out Vector2 swing, out float bounce)
        {
            swing = Vector2.zero; bounce = 0f;
            Vector3 local = transform.InverseTransformDirection(incomingVelocity);
            if (!Finite(local.x) || !Finite(local.y) || !Finite(local.z)) return false;
            swing = Vector2.ClampMagnitude(new Vector2(-local.z, local.x) * ImpactStrength, 180f);
            bounce = Mathf.Min(local.magnitude, 30f) * BounceStrength;
            return true;
        }

        /// <summary>The swing, rope and spin velocity one impact actually added, after every limit, and when (#459).</summary>
        public readonly struct AppliedImpact
        {
            public readonly Vector2 Swing;
            public readonly float Bounce, Yaw;
            /// <summary>Scaled game time of the impact: it stops while paused, like the simulation.</summary>
            public readonly double AppliedAt;
            public AppliedImpact(Vector2 swing, float bounce, float yaw, double appliedAt)
            { Swing = swing; Bounce = bounce; Yaw = yaw; AppliedAt = appliedAt; }
        }

        /// <summary>
        /// The thrower's screen presents its own contact with the Host's formula (<see cref="ApplyHostImpact"/>) and
        /// keeps what it actually changed, so the Host's verdict can replace it, limits included.
        /// </summary>
        public float ApplyPredictedImpact(Vector3 hitPointWorld, Vector3 impactVelocity, out AppliedImpact applied)
        {
            Vector2 swing = velocity; float bounce = verticalVelocity, yaw = yawSpeedDegreesPerSecond;
            float impulse = ApplyHostImpact(hitPointWorld, impactVelocity);
            applied = new AppliedImpact(velocity - swing, verticalVelocity - bounce, yawSpeedDegreesPerSecond - yaw, Time.timeAsDouble);
            return impulse;
        }

        /// <summary>
        /// The Host confirmed a presented impact with its own values: take out what the prediction has become by now
        /// and put in what the Host's impact would have become since the same moment.
        /// </summary>
        public void CorrectPredictedImpact(AppliedImpact applied, Vector3 hostVelocity, float hostYawImpulse)
        {
            if (pivot == null) return;
            Evolve(applied, -1f);
            KickContribution(hostVelocity, out Vector2 swing, out float bounce);
            Evolve(new AppliedImpact(swing, bounce, Finite(hostYawImpulse) ? hostYawImpulse : 0f, applied.AppliedAt), 1f);
            ApplyEvolvedPose();
        }

        /// <summary>The Host decided a presented impact did not count: take out what it has become by now.</summary>
        public void UndoPredictedImpact(AppliedImpact applied)
        {
            if (pivot == null) return;
            Evolve(applied, -1f);
            ApplyEvolvedPose();
        }

        /// <summary>
        /// Add (or remove) an impact's effect as it stands now. The swing and rope are damped springs and the spin a
        /// damped rate, all linear, so an old impulse can be taken out exactly as the simulation carried it forward
        /// (apart from the swing limit's rebound, which is rare). Removing only the original velocity would turn the
        /// undo into a counter-kick once the swing has moved on.
        /// </summary>
        private void Evolve(AppliedImpact impact, float sign)
        {
            float elapsed = Mathf.Max(0f, (float)(Time.timeAsDouble - impact.AppliedAt));
            SpringResponse(ReturnStrength, SwingDamping, elapsed, out float swingAngle, out float swingRate);
            angle += sign * impact.Swing * swingAngle;
            velocity += sign * impact.Swing * swingRate;
            if (MaximumExtension > 0f)
            {
                SpringResponse(BounceReturnStrength, BounceDamping, elapsed, out float offset, out float rate);
                verticalOffset += sign * impact.Bounce * offset;
                verticalVelocity += sign * impact.Bounce * rate;
            }
            float decay = Mathf.Exp(-spinDamping * elapsed);
            yawSpeedDegreesPerSecond += sign * impact.Yaw * decay;
            yawAngleDegrees += sign * impact.Yaw * (spinDamping > 0f ? (1f - decay) / spinDamping : elapsed);
        }

        /// <summary>Position and velocity, <paramref name="elapsed"/> seconds after a unit velocity impulse, of x'' = -k·x - c·x'.</summary>
        public static void SpringResponse(float stiffness, float damping, float elapsed, out float position, out float rate)
        {
            double half = damping * .5d, t = elapsed, squared = stiffness - half * half;
            if (squared > 1e-6d)
            {
                double frequency = Math.Sqrt(squared), envelope = Math.Exp(-half * t);
                double sine = Math.Sin(frequency * t), cosine = Math.Cos(frequency * t);
                position = (float)(envelope * sine / frequency);
                rate = (float)(envelope * (cosine - half / frequency * sine));
            }
            else if (squared < -1e-6d)
            {
                // Overdamped (a DEV setting can make the damping exceed the restoring force).
                double root = Math.Sqrt(-squared), fast = -half - root, slow = -half + root;
                double fastDecay = Math.Exp(fast * t), slowDecay = Math.Exp(slow * t);
                position = (float)((slowDecay - fastDecay) / (slow - fast));
                rate = (float)((slow * slowDecay - fast * fastDecay) / (slow - fast));
            }
            else
            {
                double envelope = Math.Exp(-half * t);
                position = (float)(t * envelope);
                rate = (float)(envelope * (1d - half * t));
            }
        }

        private void ApplyEvolvedPose()
        {
            angle = Vector2.ClampMagnitude(angle, MaximumAngle);
            velocity = Vector2.ClampMagnitude(velocity, 220f);
            verticalVelocity = Mathf.Clamp(verticalVelocity, -8f, 8f);
            yawSpeedDegreesPerSecond = Mathf.Clamp(yawSpeedDegreesPerSecond, -maximumSpinSpeed, maximumSpinSpeed);
            pivot.localRotation = Quaternion.Euler(angle.x, 0, angle.y);
            ApplyYawRotation();
            ApplyRopeLength();
            ShiftPose();
        }

        public void ResetSwing()
        {
            angle = velocity = Vector2.zero;
            verticalOffset = verticalVelocity = 0f;
            cycleElapsed = 0f;
            yawAngleDegrees = yawSpeedDegreesPerSecond = 0f;
            flash = 0;
            if (pivot != null) pivot.localRotation = Quaternion.identity;
            ApplyYawRotation();
            ApplyRopeLength();
            SnapPose();
        }

        private void FixedUpdate()
        {
            // The last frame drew an interpolated pose. Physics and every later FixedUpdate read the fixed one.
            if (posesReady) WritePose(currentPose);
            if (simulationHeld) return;
            // A snapshot can pause during NetworkEarlyUpdate after this frame's fixed ticks
            // were queued. Do not integrate that last queued callback after the pose is pinned.
            if (pivot == null || Time.timeScale <= 0f) return;
            ApplyRopeLength();
            float dt = Time.fixedDeltaTime;
            cycleElapsed = Mathf.Repeat(cycleElapsed + dt, CyclePeriod);
            velocity += (-angle * ReturnStrength - velocity * SwingDamping) * dt;
            angle += velocity * dt;
            if (angle.magnitude > MaximumAngle)
            {
                angle = Vector2.ClampMagnitude(angle, MaximumAngle);
                if (Vector2.Dot(velocity, angle) > 0) velocity *= -.25f;
            }
            pivot.localRotation = Quaternion.Euler(angle.x, 0, angle.y);
            yawSpeedDegreesPerSecond *= Mathf.Exp(-spinDamping * dt);
            if (Mathf.Abs(yawSpeedDegreesPerSecond) < .01f) yawSpeedDegreesPerSecond = 0f;
            yawAngleDegrees += yawSpeedDegreesPerSecond * dt;
            ApplyYawRotation();
            if (MaximumExtension == 0f) verticalOffset = verticalVelocity = 0f;
            else
            {
                verticalVelocity += (-verticalOffset * BounceReturnStrength - verticalVelocity * BounceDamping) * dt;
                verticalOffset += verticalVelocity * dt;
            }
            ApplyRopeLength();
            previousPose = currentPose;
            currentPose = Capture();
        }

        private void Update()
        {
            flash = Mathf.Max(0, flash - Time.deltaTime);
            if (bodyMaterial != null && bodyMaterial.HasProperty("_BaseColor"))
                bodyMaterial.SetColor("_BaseColor", Color.Lerp(bodyBaseColor,
                    new Color(1f, .85f, .45f), flash / .16f));
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginContextRendering += OnBeginRendering;
            RenderPipelineManager.endContextRendering += OnEndRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginRendering;
            RenderPipelineManager.endContextRendering -= OnEndRendering;
            RestoreFixedPose();
        }

        private void OnBeginRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!posesReady) return;
            // Held or paused: one pose, and nothing to step back to when time resumes.
            if (simulationHeld || Time.timeScale <= 0f) { SnapPose(); return; }
            ApplyRenderPose(RenderAlpha());
            drawingInterpolated = true;
        }

        private void OnEndRendering(ScriptableRenderContext context, List<Camera> cameras) => RestoreFixedPose();

        private void RestoreFixedPose()
        {
            if (!drawingInterpolated) return;
            drawingInterpolated = false;
            if (posesReady) WritePose(currentPose);
        }

        /// <summary>How far this frame is between the last fixed step and the next, as Rigidbody interpolation uses.</summary>
        private static float RenderAlpha()
        {
            float step = Time.fixedDeltaTime;
            if (!(step > 0f)) return 1f;
            return Mathf.Clamp01((float)((Time.timeAsDouble - Time.fixedTimeAsDouble) / step));
        }

        private void ApplyRenderPose(float alpha) => WritePose(RigState.Lerp(previousPose, currentPose, alpha));

        /// <summary>
        /// Where a point fixed to the hanging body appears in the frame being drawn. Scripts read the fixed pose; screen
        /// UI anchored to the gourd (presentation only, never hits or paint) maps through this so it stays on the drawn
        /// gourd. Unchanged while held, paused or before the first pose.
        /// </summary>
        public Vector3 DrawnPoint(Vector3 worldPoint)
        {
            if (!posesReady || pivot == null || twistPivot == null || simulationHeld || Time.timeScale <= 0f) return worldPoint;
            Matrix4x4 drawn = TwistMatrix(RigState.Lerp(previousPose, currentPose, RenderAlpha()));
            return drawn.MultiplyPoint3x4(TwistMatrix(currentPose).inverse.MultiplyPoint3x4(worldPoint));
        }

        /// <summary>The drawn position of a point on whichever hanging target owns <paramref name="part"/>.</summary>
        public static Vector3 DrawnPoint(Component part, Vector3 worldPoint)
        {
            LabHangingTarget hanging = part != null ? part.GetComponentInParent<LabHangingTarget>() : null;
            return hanging != null ? hanging.DrawnPoint(worldPoint) : worldPoint;
        }

        private Matrix4x4 TwistMatrix(RigState pose) => transform.localToWorldMatrix
            * Matrix4x4.TRS(Vector3.up * pose.PivotHeight, Quaternion.Euler(pose.Swing.x, 0, pose.Swing.y), pivot.localScale)
            * Matrix4x4.TRS(Vector3.down * (pose.PivotHeight + pose.RopeOffset), Quaternion.Euler(0f, pose.Yaw, 0f), twistPivot.localScale);

        private RigState Capture() => new RigState
        {
            Swing = angle, Yaw = yawAngleDegrees, RopeOffset = currentRopeOffset, PivotHeight = pivotHeight
        };

        /// <summary>Pin the drawn pose to the current one: pause, start hold, reset and configuration.</summary>
        private void SnapPose()
        {
            if (pivot == null) return;
            previousPose = currentPose = Capture();
            posesReady = true;
        }

        /// <summary>
        /// A running correction or impact moves both kept poses by the same amount, so the drawn pose keeps its place
        /// between fixed steps instead of jumping on every 10 Hz reconcile.
        /// </summary>
        private void ShiftPose()
        {
            if (pivot == null) return;
            if (!posesReady) { SnapPose(); return; }
            RigState next = Capture();
            previousPose.Swing += next.Swing - currentPose.Swing;
            previousPose.Yaw += next.Yaw - currentPose.Yaw;
            previousPose.RopeOffset += next.RopeOffset - currentPose.RopeOffset;
            previousPose.PivotHeight = next.PivotHeight;
            currentPose = next;
        }

        private void WritePose(RigState pose)
        {
            if (pivot == null || twistPivot == null || rope == null) return;
            pivot.localPosition = Vector3.up * pose.PivotHeight;
            pivot.localRotation = Quaternion.Euler(pose.Swing.x, 0, pose.Swing.y);
            twistPivot.localRotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            twistPivot.localPosition = Vector3.down * (pose.PivotHeight + pose.RopeOffset);
            float stretchedLength = RopeLength + pose.RopeOffset;
            rope.localPosition = Vector3.down * (stretchedLength * .5f);
            rope.localScale = new Vector3(.055f, stretchedLength * .5f, .055f);
            UpdateContinuousRope(stretchedLength);
        }

        private void ApplyYawRotation()
        {
            if (twistPivot == null) return;
            twistPivot.localRotation = Quaternion.Euler(0f, yawAngleDegrees, 0f);
            // An off-axis rope socket turns with the body.
            if (ropeSocket != null) UpdateContinuousRope(drawnRopeStretch);
        }

        private void ApplyRopeLength()
        {
            if (pivot == null || rope == null) return;
            float minOffset = -Mathf.Min(RopeLength * .45f, MaximumExtension * .5f);
            if (verticalOffset > MaximumExtension)
            {
                verticalOffset = MaximumExtension;
                if (verticalVelocity > 0f) verticalVelocity *= -.2f;
            }
            else if (verticalOffset < minOffset)
            {
                verticalOffset = minOffset;
                if (verticalVelocity < 0f) verticalVelocity *= -.2f;
            }
            float length = RopeLength;
            // Measure the actual mesh in the shared hanging frame; a cylinder's scale
            // is not the height of a gourd. Common swing/yaw must not change this value.
            float top = BodyTopInMount();
            pivotHeight = length + top;
            pivot.localPosition = Vector3.up * pivotHeight;
            visualMount.localPosition = Vector3.zero;
            currentRopeOffset = Mathf.Max(.1f - length, verticalOffset
                + CycleAmplitude * Mathf.Sin(cycleElapsed / CyclePeriod * Mathf.PI * 2f));
            twistPivot.localPosition = Vector3.down * (pivotHeight + currentRopeOffset);
            float stretchedLength = length + currentRopeOffset;
            rope.localPosition = Vector3.down * (stretchedLength * .5f);
            rope.localScale = new Vector3(.055f, stretchedLength * .5f, .055f);
            float extension = Safe(suspensionVisualExtension, 0f, 0f, 20f);
            if (upperRope != null)
            {
                upperRope.gameObject.SetActive(extension > 0f);
                upperRope.localPosition = Vector3.up * (pivotHeight + extension * .5f);
                upperRope.localScale = new Vector3(.055f, extension * .5f, .055f);
            }
            ceilingBeam.localPosition = Vector3.up * (pivotHeight + extension + .1f);
            ceilingPlate.localPosition = Vector3.up * (pivotHeight + extension + .26f);
            // Keep the resting target centre fixed so tuning length doesn't change the aim point.
            UpdateContinuousRope(stretchedLength);
        }

        /// <summary>Show either the original two ropes or the continuous one, building its pieces on first use.</summary>
        private void ApplyRopeStyle()
        {
            bool segmented = ropeStyle == LabRopeStyle.Segmented;
            SetRendererEnabled(rope, segmented);
            SetRendererEnabled(upperRope, segmented);
            if (segmented)
            {
                if (continuousRope != null) continuousRope.gameObject.SetActive(false);
                return;
            }
            if (continuousRope == null)
            {
                continuousRope = new GameObject("Continuous Rope").transform;
                continuousRope.SetParent(transform, false);
            }
            continuousRope.gameObject.SetActive(true);
            int wanted = ropeStyle == LabRopeStyle.Curved ? CurvedRopeSegments : 1;
            if (ropeSegments.Length == wanted) return;
            foreach (Transform old in ropeSegments)
                if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            ropeSegments = new Transform[wanted];
            for (int i = 0; i < wanted; i++)
                ropeSegments[i] = Primitive("Rope Segment " + i, PrimitiveType.Cylinder, continuousRope,
                    Vector3.zero, Vector3.one, ropeMaterial);
        }

        private static void SetRendererEnabled(Transform part, bool enabled)
        {
            Renderer renderer = part != null ? part.GetComponent<Renderer>() : null;
            if (renderer != null) renderer.enabled = enabled;
        }

        /// <summary>
        /// Lay the continuous rope from the fixed anchor above the pivot to the gourd, in this target's local space.
        /// Called wherever the pivot or rope transforms are written (fixed pose, drawn pose, hold and reset), so it
        /// follows the #454 interpolation without its own state.
        /// </summary>
        private void UpdateContinuousRope(float stretchedLength)
        {
            drawnRopeStretch = stretchedLength;
            if (pivot == null || continuousRope == null || !continuousRope.gameObject.activeSelf) return;
            float extension = Safe(suspensionVisualExtension, 0f, 0f, 20f);
            Vector3 control = pivot.localPosition;
            Vector3 anchor = control + Vector3.up * extension;
            Vector3 end = control + pivot.localRotation * (Vector3.down * stretchedLength);
            if (ropeSocket != null && ropeSocket.IsChildOf(transform) && FiniteVector(ropeSocket.position))
                end = transform.InverseTransformPoint(ropeSocket.position);
            int count = ropeSegments.Length;
            if (count == 1)
            {
                PlaceSegment(ropeSegments[0], anchor, end);
                return;
            }
            Vector3 previous = anchor;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / count, u = 1f - t;
                Vector3 point = u * u * anchor + 2f * u * t * control + t * t * end;
                PlaceSegment(ropeSegments[i], previous, point);
                previous = point;
            }
        }

        /// <summary>A Unity cylinder is 2 units tall along Y; overlap the ends slightly so the joints stay closed.</summary>
        private static void PlaceSegment(Transform segment, Vector3 from, Vector3 to)
        {
            if (segment == null) return;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            segment.localPosition = (from + to) * .5f;
            segment.localRotation = length > 1e-5f ? Quaternion.FromToRotation(Vector3.up, delta / length) : Quaternion.identity;
            segment.localScale = new Vector3(RopeDiameter, length * .5f + (length > 1e-5f ? .004f : 0f), RopeDiameter);
        }

        private float BodyTopInMount()
        {
            if (mountedTarget == null || mountedTarget.Hitbox == null
                || mountedTarget.Hitbox.sharedMesh == null || visualMount == null) return .9f;
            var hitbox = mountedTarget.Hitbox;
            Bounds bounds = hitbox.sharedMesh.bounds;
            Matrix4x4 matrix = visualMount.worldToLocalMatrix * hitbox.transform.localToWorldMatrix;
            float top = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                top = Mathf.Max(top, matrix.MultiplyPoint3x4(point).y);
            }
            return Finite(top) ? Mathf.Max(.01f, top) : .9f;
        }

        private static Material MakeMaterial(Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Transform Primitive(string name, PrimitiveType shape, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            LabVisualSafety.RemovePhysics(item);
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item.transform;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteVector(Vector3 value)
            => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static float Safe(float value, float fallback, float min, float max)
            => Mathf.Clamp(Finite(value) ? value : fallback, min, max);

        private void OnValidate()
        {
            impactStrength = ImpactStrength;
            swingDamping = SwingDamping;
            maximumAngle = MaximumAngle;
            returnStrength = ReturnStrength;
            ropeLength = RopeLength;
            if (!Enum.IsDefined(typeof(LabRopeStyle), ropeStyle)) ropeStyle = LabRopeStyle.Segmented;
            bounceStrength = BounceStrength;
            bounceReturnStrength = BounceReturnStrength;
            bounceDamping = BounceDamping;
            maximumExtension = MaximumExtension;
        }

        private void OnDestroy()
        {
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (ropeMaterial != null) Destroy(ropeMaterial);
        }
    }
}
