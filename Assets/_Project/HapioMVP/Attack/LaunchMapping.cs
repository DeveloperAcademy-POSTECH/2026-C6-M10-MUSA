using System;
using UnityEngine;

namespace C6.Prototype.Attack
{
    /// <summary>Explicit battlefield data. No camera or display dimensions enter launch rules.</summary>
    public readonly struct ProjectileLaunchBasis
    {
        public Vector3 Origin { get; }
        public Vector3 HorizontalAxis { get; }
        public float Width { get; }
        public Vector3 AimPoint { get; }

        public ProjectileLaunchBasis(Vector3 origin, Vector3 horizontalAxis, float width, Vector3 aimPoint)
        {
            if (!LaunchMapping.IsFinite(origin) || !LaunchMapping.IsFinite(horizontalAxis) ||
                !LaunchMapping.IsFinite(aimPoint) || horizontalAxis.sqrMagnitude < .000001f ||
                !LaunchMapping.IsFinite(width) || width <= 0f)
                throw new ArgumentException("Launch basis requires finite positions, a nonzero axis and positive width.");
            Origin = origin;
            HorizontalAxis = horizontalAxis.normalized;
            Width = width;
            AimPoint = aimPoint;
        }
    }

    public readonly struct ProjectileLaunchPose
    {
        public Vector3 Position { get; }
        public Vector3 Direction { get; }

        public ProjectileLaunchPose(Vector3 position, Vector3 direction)
        {
            if (!LaunchMapping.IsFinite(position) || !LaunchMapping.IsFinite(direction) ||
                direction.sqrMagnitude < .000001f)
                throw new ArgumentException("A projectile requires a finite position and nonzero direction.");
            Position = position;
            Direction = direction.normalized;
        }
    }

    public static class LaunchMapping
    {
        public static ProjectileLaunchPose Calculate(Vector2 normalizedPosition, ProjectileLaunchBasis basis)
        {
            if (!IsFinite(normalizedPosition.x) || !IsFinite(normalizedPosition.y) ||
                normalizedPosition.x < 0f || normalizedPosition.x > 1f ||
                normalizedPosition.y < 0f || normalizedPosition.y > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedPosition));
            if (basis.Width <= 0f || basis.HorizontalAxis.sqrMagnitude < .000001f)
                throw new ArgumentException("An initialized launch basis is required.", nameof(basis));

            // T06 DEMO_ASSUMPTION: horizontal entry chooses the launch point, and the fixed
            // training target is the aim point. Y already passed the Attack Zone gate;
            // it is not converted to force, swipe speed, a throw threshold or auto-aim input.
            Vector3 position = basis.Origin + basis.HorizontalAxis * ((normalizedPosition.x - .5f) * basis.Width);
            return new ProjectileLaunchPose(position, basis.AimPoint - position);
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
