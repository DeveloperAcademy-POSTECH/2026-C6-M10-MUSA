using System;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.PhysicsSandbox
{
    /// <summary>A working copy of exactly the seven existing shared physics settings.</summary>
    [Serializable]
    public sealed class OrbSandboxTuning
    {
        [Range(.01f, .1f), Tooltip("Radius as a fraction of one player's complete board width.")]
        public float orbRadiusScreenFraction = .055f;
        [Range(0f, 1f)] public float orbRestitution = .65f;
        [Range(0f, 1f)] public float orbContactFriction = .15f;
        [Range(.001f, 20f), Tooltip("Free sliding deceleration, in board widths per second squared.")]
        public float orbFloorDeceleration = .6f;
        [Range(.001f, 1f), Tooltip("Speeds below this threshold stop, in board widths per second.")]
        public float orbStopSpeed = .015f;
        [Range(.01f, 10f), Tooltip("Maximum release speed, in board widths per second.")]
        public float orbMaxReleaseSpeed = 2f;
        [Range(.02f, .5f), Tooltip("Seconds of recent drag positions used to calculate release velocity.")]
        public float orbReleaseSampleWindow = .12f;

        public static OrbSandboxTuning Capture(ScreenLayoutConfig source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new OrbSandboxTuning
            {
                orbRadiusScreenFraction = source.OrbRadiusScreenFraction,
                orbRestitution = source.OrbRestitution,
                orbContactFriction = source.OrbContactFriction,
                orbFloorDeceleration = source.OrbFloorDeceleration,
                orbStopSpeed = source.OrbStopSpeed,
                orbMaxReleaseSpeed = source.OrbMaxReleaseSpeed,
                orbReleaseSampleWindow = source.OrbReleaseSampleWindow
            };
        }

        public void Sanitize()
        {
            orbRadiusScreenFraction = Valid(orbRadiusScreenFraction, .055f, .01f, .1f);
            orbRestitution = Valid(orbRestitution, .65f, 0f, 1f);
            orbContactFriction = Valid(orbContactFriction, .15f, 0f, 1f);
            orbFloorDeceleration = Valid(orbFloorDeceleration, .6f, .001f, 20f);
            orbMaxReleaseSpeed = Valid(orbMaxReleaseSpeed, 2f, .01f, 10f);
            orbStopSpeed = Valid(orbStopSpeed, .015f, .001f, Mathf.Min(1f, orbMaxReleaseSpeed));
            orbReleaseSampleWindow = Valid(orbReleaseSampleWindow, .12f, .02f, .5f);
        }

        /// <summary>Explicit save operation only. No other ScreenLayoutConfig field is serialized here.</summary>
        public void ApplyToConfig(ScreenLayoutConfig destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            Sanitize();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), destination);
        }

        public OrbPhysicsTuning ToPhysics(float boardWorldWidth)
        {
            if (float.IsNaN(boardWorldWidth) || float.IsInfinity(boardWorldWidth) || boardWorldWidth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(boardWorldWidth));
            Sanitize();
            return new OrbPhysicsTuning(orbRestitution, orbFloorDeceleration * boardWorldWidth,
                orbStopSpeed * boardWorldWidth, orbMaxReleaseSpeed * boardWorldWidth,
                orbReleaseSampleWindow, orbContactFriction);
        }

        internal bool SameAs(OrbSandboxTuning other) => other != null &&
            orbRadiusScreenFraction == other.orbRadiusScreenFraction && orbRestitution == other.orbRestitution &&
            orbContactFriction == other.orbContactFriction && orbFloorDeceleration == other.orbFloorDeceleration &&
            orbStopSpeed == other.orbStopSpeed && orbMaxReleaseSpeed == other.orbMaxReleaseSpeed &&
            orbReleaseSampleWindow == other.orbReleaseSampleWindow;

        internal OrbSandboxTuning Copy() => (OrbSandboxTuning)MemberwiseClone();

        private static float Valid(float value, float fallback, float min, float max) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
    }
}
