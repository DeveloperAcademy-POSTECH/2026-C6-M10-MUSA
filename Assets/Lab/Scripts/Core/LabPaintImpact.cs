using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Presentation-only calibration of the Host's pre-resolution incoming velocity.
    /// Normal speed excludes sideways skimming and is not damage, momentum or paint area.
    /// These two speeds are initial art-direction test values, not physical material constants.
    /// </summary>
    public static class LabPaintImpact
    {
        public const float WeakNormalSpeed = 0f;
        public const float StrongNormalSpeed = 20f;
        public const float UnspecifiedStrength = .5f;

        public static float Normalize(Vector3 incomingVelocity, Vector3 outwardSurfaceNormal)
        {
            if (!Finite(incomingVelocity) || !Finite(outwardSurfaceNormal)
                || outwardSurfaceNormal.sqrMagnitude < .00000001f) return UnspecifiedStrength;
            float normalSpeed = Mathf.Max(0f, -Vector3.Dot(incomingVelocity, outwardSurfaceNormal.normalized));
            return Mathf.InverseLerp(WeakNormalSpeed, StrongNormalSpeed, normalSpeed);
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x)
            && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
