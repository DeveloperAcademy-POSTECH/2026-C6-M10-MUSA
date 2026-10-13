using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>A damped pendulum with a freely turning body. The hitbox and art share both pivots.</summary>
    [DisallowMultipleComponent]
    public sealed class LabHangingTarget : MonoBehaviour
    {
        private const float FlashDuration = .16f;
        private static readonly Color HitFlashColor = new Color(1f, .85f, .45f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        private sealed class FlashSurface
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public int ColorProperty;
            public Color BaseColor;
            public MaterialPropertyBlock Properties;
        }

        [SerializeField, Min(0f), Tooltip("Higher values produce a stronger swing from the same impact. Range: 0–20.")]
        private float impactStrength = 2.4f;
        [SerializeField, Min(0f), Tooltip("How quickly swing momentum fades. Zero keeps oscillating. Range: 0–20.")]
        private float swingDamping = 1.15f;
        [SerializeField, Min(1f), Tooltip("Maximum swing from vertical, in degrees.")]
        private float maximumAngle = 24f;
        [SerializeField, Min(0f), Tooltip("How strongly the target returns to its resting position. Range: 0–100.")]
        private float returnStrength = 13f;

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

        public float BounceStrength => Safe(bounceStrength, .18f, 0f, 2f);
        public float BounceReturnStrength => Safe(bounceReturnStrength, 45f, 0f, 100f);
        public float BounceDamping => Safe(bounceDamping, 3f, 0f, 20f);
        public float MaximumExtension => Safe(maximumExtension, .5f, 0f, 3f);
        public float VerticalOffset => verticalOffset;
        public float RopeLength => Safe(ropeLength, .85f, .1f, 8f);
        public float ImpactStrength => Safe(impactStrength, 2.4f, 0f, 20f);
        public float SwingDamping => Safe(swingDamping, 1.15f, 0f, 20f);
        public float MaximumAngle => Safe(maximumAngle, 24f, 1f, 75f);
        public float ReturnStrength => Safe(returnStrength, 13f, 0f, 100f);
        // Configured from the session's LabConfig copy; these are only safe standalone defaults.
        private float spinStrength = 80f, spinDamping = 1.2f, maximumSpinSpeed = 720f;
        private Transform pivot, twistPivot, hangingBody, visualMount, rope, ceilingBeam, ceilingPlate;
        private float verticalOffset, verticalVelocity;
        private Vector2 angle, velocity;
        private float yawAngleDegrees, yawSpeedDegreesPerSecond;
        private Material bodyMaterial, ropeMaterial;
        private Color bodyBaseColor;
        private float flash;
        private readonly List<FlashSurface> visualFlashSurfaces = new List<FlashSurface>();
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
            RefreshFlashRenderers();
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
        public void ReconcileYaw(float angleDegrees, float speedDegreesPerSecond)
        {
            if (twistPivot == null || !Finite(angleDegrees) || !Finite(speedDegreesPerSecond)) return;
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
            // A replacement visual may have been mounted after Awake. Refresh only when hit,
            // not every frame, so newly added mesh renderers receive the same feedback.
            RefreshFlashRenderers();
            velocity += Vector2.ClampMagnitude(new Vector2(-local.z, local.x) * ImpactStrength, 55f);
            velocity = Vector2.ClampMagnitude(velocity, 85f);
            verticalVelocity = Mathf.Clamp(verticalVelocity + Mathf.Min(local.magnitude, 30f) * BounceStrength, -8f, 8f);
            flash = FlashDuration;
            ApplyFlash(1f);
        }

        public void ResetSwing()
        {
            angle = velocity = Vector2.zero;
            verticalOffset = verticalVelocity = 0f;
            yawAngleDegrees = yawSpeedDegreesPerSecond = 0f;
            flash = 0;
            ApplyFlash(0f);
            if (pivot != null) pivot.localRotation = Quaternion.identity;
            ApplyYawRotation();
            ApplyRopeLength();
        }

        private void FixedUpdate()
        {
            if (pivot == null) return;
            ApplyRopeLength();
            float dt = Time.fixedDeltaTime;
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
            float previousFlash = flash;
            flash = Mathf.Max(0, flash - Time.deltaTime);
            if (flash > 0f || previousFlash > 0f) ApplyFlash(flash / FlashDuration);
        }

        /// <summary>Rebuild the visible art's flash bindings after changing its prefab.</summary>
        public void RefreshFlashRenderers()
        {
            ApplyVisualFlash(0f);
            visualFlashSurfaces.Clear();
            if (visualMount == null) return;

            foreach (Renderer renderer in visualMount.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    if (material == null) continue;
                    bool hasBaseColor = material.HasProperty(BaseColorId);
                    if (!hasBaseColor && !material.HasProperty(LegacyColorId)) continue;
                    int colorProperty = hasBaseColor ? BaseColorId : LegacyColorId;
                    visualFlashSurfaces.Add(new FlashSurface
                    {
                        Renderer = renderer,
                        MaterialIndex = index,
                        ColorProperty = colorProperty,
                        BaseColor = material.GetColor(colorProperty),
                        Properties = new MaterialPropertyBlock()
                    });
                }
            }
            ApplyVisualFlash(flash / FlashDuration);
        }

        private void ApplyFlash(float strength)
        {
            if (bodyMaterial != null && bodyMaterial.HasProperty(BaseColorId))
                bodyMaterial.SetColor(BaseColorId,
                    Color.Lerp(bodyBaseColor, HitFlashColor, strength));
            ApplyVisualFlash(strength);
        }

        private void ApplyVisualFlash(float strength)
        {
            foreach (FlashSurface surface in visualFlashSurfaces)
            {
                if (surface.Renderer == null) continue;
                // Preserve the renderer's other per-material overrides and texture bindings.
                surface.Renderer.GetPropertyBlock(surface.Properties, surface.MaterialIndex);
                surface.Properties.SetColor(surface.ColorProperty,
                    Color.Lerp(surface.BaseColor, HitFlashColor, strength));
                surface.Renderer.SetPropertyBlock(surface.Properties, surface.MaterialIndex);
            }
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
            twistPivot.localPosition = Vector3.down * (pivotHeight + verticalOffset);
            hangingBody.localPosition = Vector3.zero;
            visualMount.localPosition = Vector3.zero;
            float stretchedLength = length + verticalOffset;
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
            ApplyVisualFlash(0f);
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (ropeMaterial != null) Destroy(ropeMaterial);
        }
    }
}
