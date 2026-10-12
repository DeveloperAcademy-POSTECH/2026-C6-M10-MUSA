using UnityEngine;

namespace C6Lab
{
    /// <summary>A damped pendulum. The real hitbox and visible body share the moving pivot.</summary>
    [DisallowMultipleComponent]
    public sealed class LabHangingTarget : MonoBehaviour
    {
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
        private Transform pivot, hangingBody, visualMount, rope, ceilingBeam, ceilingPlate;
        private float verticalOffset, verticalVelocity;
        private Vector2 angle, velocity;
        private Material bodyMaterial, ropeMaterial;
        private float flash;
        public Vector2 SwingAngles => angle;

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
            body.SetParent(pivot, true);
            // A shorter body leaves an unmistakable rope above it and air below it.
            body.localScale = new Vector3(body.localScale.x, .9f, body.localScale.z);
            target.VisualMount.SetParent(pivot, true);
            bodyMaterial = MakeMaterial(new Color(.85f, .28f, .09f), .32f);
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

        public void Kick(Vector3 incomingVelocity)
        {
            if (pivot == null) return;
            ApplyRopeLength();
            Vector3 local = transform.InverseTransformDirection(incomingVelocity);
            if (!Finite(local.x) || !Finite(local.y) || !Finite(local.z)) return;
            velocity += Vector2.ClampMagnitude(new Vector2(-local.z, local.x) * ImpactStrength, 55f);
            velocity = Vector2.ClampMagnitude(velocity, 85f);
            verticalVelocity = Mathf.Clamp(verticalVelocity + Mathf.Min(local.magnitude, 30f) * BounceStrength, -8f, 8f);
            flash = .16f;
        }

        public void ResetSwing()
        {
            angle = velocity = Vector2.zero;
            verticalOffset = verticalVelocity = 0f;
            flash = 0;
            if (pivot != null) pivot.localRotation = Quaternion.identity;
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
            if (bodyMaterial != null)
                bodyMaterial.SetColor("_BaseColor", Color.Lerp(new Color(.85f, .28f, .09f),
                    new Color(1f, .85f, .45f), flash / .16f));
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
            hangingBody.localPosition = Vector3.down * (pivotHeight + verticalOffset);
            visualMount.localPosition = hangingBody.localPosition;
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
