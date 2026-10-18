using UnityEngine;

namespace C6Lab
{
    /// <summary>A damped pendulum with a freely turning body. The hitbox and art share both pivots.</summary>
    [DisallowMultipleComponent]
    public sealed class LabHangingTarget : MonoBehaviour
    {
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
        public float ImpactStrength => Safe(impactStrength, 12f, 0f, 20f);
        public float SwingDamping => Safe(swingDamping, 1.4f, 0f, 20f);
        public float MaximumAngle => Safe(maximumAngle, 40f, 1f, 75f);
        public float ReturnStrength => Safe(returnStrength, 10f, 0f, 100f);
        // Configured from the session's LabConfig copy; these are only safe standalone defaults.
        private float spinStrength = 80f, spinDamping = 1.2f, maximumSpinSpeed = 720f;
        private Transform pivot, twistPivot, hangingBody, visualMount, rope, ceilingBeam, ceilingPlate;
        private float verticalOffset, verticalVelocity, cycleElapsed, currentRopeOffset;
        private Vector2 angle, velocity;
        private float yawAngleDegrees, yawSpeedDegreesPerSecond;
        private Material bodyMaterial, ropeMaterial;
        private Color bodyBaseColor;
        private float flash;
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
            Transform body = target.Hitbox.transform;
            hangingBody = body;
            visualMount = target.VisualMount;
            twistPivot = new GameObject("Body Twist Pivot").transform;
            twistPivot.SetParent(pivot, false);
            body.SetParent(twistPivot, true);
            // A shorter body leaves an unmistakable rope above it and air below it.
            body.localScale = new Vector3(body.localScale.x, .9f, body.localScale.z);
            target.VisualMount.SetParent(twistPivot, true);
            // The saved Scene may supply a diagnostic texture. Own a runtime clone so flash
            // never edits the asset or replaces the visible cylinder with a flat color.
            var sourceMaterial = body.GetComponent<Renderer>().sharedMaterial;
            bodyMaterial = sourceMaterial != null && sourceMaterial.shader != null &&
                sourceMaterial.shader.name.StartsWith("Universal Render Pipeline/")
                ? new Material(sourceMaterial)
                : MakeMaterial(new Color(.85f, .28f, .09f), .32f);
            bodyBaseColor = bodyMaterial.HasProperty("_BaseColor")
                ? bodyMaterial.GetColor("_BaseColor") : Color.white;
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            ropeMaterial = MakeMaterial(new Color(.64f, .52f, .32f), .1f);
            rope = Primitive("Rope", PrimitiveType.Cylinder, pivot,
                new Vector3(0, -.425f, 0), new Vector3(.055f, .425f, .055f), ropeMaterial);
            ceilingBeam = Primitive("Ceiling Beam", PrimitiveType.Cube, transform,
                new Vector3(0, 1.85f, 0), new Vector3(5f, .18f, .5f), ropeMaterial);
            ceilingPlate = Primitive("Ceiling Plate", PrimitiveType.Cube, transform,
                new Vector3(0, 2.01f, 0), new Vector3(6f, .12f, 3f), ropeMaterial);
            ApplyRopeLength();
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
            if (pivot != null) pivot.localRotation = Quaternion.Euler(angle.x, 0, angle.y);
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
        }

        /// <summary>Keep a remote target close to the Host, or pin its rope state while paused.</summary>
        public void ReconcileRope(float elapsed, float hitOffset, float hitVelocity, bool paused = false)
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
                return;
            }
            float phaseError = Mathf.DeltaAngle(cycleElapsed / CyclePeriod * 360f,
                phase / CyclePeriod * 360f) / 360f * CyclePeriod;
            if (Mathf.Abs(phaseError) > .25f) cycleElapsed = phase;
            else if (Mathf.Abs(phaseError) > .03f)
                cycleElapsed = Mathf.Repeat(cycleElapsed + phaseError * .25f, CyclePeriod);
            if (Mathf.Abs(hitOffset - verticalOffset) > .08f)
                verticalOffset = Mathf.Lerp(verticalOffset, hitOffset, .5f);
            if (Mathf.Abs(hitVelocity - verticalVelocity) > .5f)
                verticalVelocity = Mathf.Lerp(verticalVelocity, hitVelocity, .5f);
            ApplyRopeLength();
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
            Vector3 local = transform.InverseTransformDirection(incomingVelocity);
            if (!Finite(local.x) || !Finite(local.y) || !Finite(local.z)) return;
            velocity += Vector2.ClampMagnitude(new Vector2(-local.z, local.x) * ImpactStrength, 180f);
            velocity = Vector2.ClampMagnitude(velocity, 220f);
            verticalVelocity = Mathf.Clamp(verticalVelocity + Mathf.Min(local.magnitude, 30f) * BounceStrength, -8f, 8f);
            flash = .16f;
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
        }

        private void FixedUpdate()
        {
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
        }

        private void Update()
        {
            flash = Mathf.Max(0, flash - Time.deltaTime);
            if (bodyMaterial != null && bodyMaterial.HasProperty("_BaseColor"))
                bodyMaterial.SetColor("_BaseColor", Color.Lerp(bodyBaseColor,
                    new Color(1f, .85f, .45f), flash / .16f));
        }

        private void ApplyYawRotation()
        {
            if (twistPivot != null) twistPivot.localRotation = Quaternion.Euler(0f, yawAngleDegrees, 0f);
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
            float halfBodyHeight = hangingBody.localScale.y; // Unity's cylinder is two units tall.
            float pivotHeight = length + halfBodyHeight;
            pivot.localPosition = Vector3.up * pivotHeight;
            hangingBody.localPosition = Vector3.zero;
            visualMount.localPosition = Vector3.zero;
            currentRopeOffset = Mathf.Max(.1f - length, verticalOffset
                + CycleAmplitude * Mathf.Sin(cycleElapsed / CyclePeriod * Mathf.PI * 2f));
            twistPivot.localPosition = Vector3.down * (pivotHeight + currentRopeOffset);
            float stretchedLength = length + currentRopeOffset;
            rope.localPosition = Vector3.down * (stretchedLength * .5f);
            rope.localScale = new Vector3(.055f, stretchedLength * .5f, .055f);
            ceilingBeam.localPosition = Vector3.up * (pivotHeight + .1f);
            ceilingPlate.localPosition = Vector3.up * (pivotHeight + .26f);
            // Keep the resting target centre fixed so tuning length doesn't change the aim point.

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
