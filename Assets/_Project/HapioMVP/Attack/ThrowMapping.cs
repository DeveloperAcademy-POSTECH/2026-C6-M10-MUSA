using System;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.Attack
{
    /// <summary>Copied from the one Config at runtime; this is not a second authoring asset.</summary>
    public readonly struct ThrowTuning
    {
        public float MinUpSpeed { get; }
        public float MaxInputSpeed { get; }
        public float ForwardGain { get; }
        public float UpGain { get; }
        public float LateralGain { get; }
        public float MaxWorldSpeed { get; }
        public float Gravity { get; }
        public float Bounce { get; }
        public float Lifetime { get; }
        public float Radius { get; }
        public float SampleWindow { get; }
        public float MinDuration { get; }

        public ThrowTuning(float minUpSpeed, float maxInputSpeed, float forwardGain, float upGain,
            float lateralGain, float maxWorldSpeed, float gravity, float bounce, float lifetime,
            float radius, float sampleWindow, float minDuration)
        {
            MinUpSpeed = minUpSpeed; MaxInputSpeed = maxInputSpeed;
            ForwardGain = forwardGain; UpGain = upGain; LateralGain = lateralGain;
            MaxWorldSpeed = maxWorldSpeed; Gravity = gravity; Bounce = bounce;
            Lifetime = lifetime; Radius = radius; SampleWindow = sampleWindow; MinDuration = minDuration;
            if (!IsValid) throw new ArgumentException("Initialized, finite, positive throw tuning is required (bounce 0..1).");
        }

        internal bool IsValid => Positive(MinUpSpeed) && Positive(MaxInputSpeed) && MinUpSpeed <= MaxInputSpeed &&
            Positive(ForwardGain) && Positive(UpGain) && Positive(LateralGain) && Positive(MaxWorldSpeed) &&
            Positive(Gravity) && LaunchMapping.IsFinite(Bounce) && Bounce >= 0f && Bounce <= 1f &&
            Positive(Lifetime) && Positive(Radius) && Positive(SampleWindow) && Positive(MinDuration) && MinDuration <= SampleWindow;
        private static bool Positive(float value) => LaunchMapping.IsFinite(value) && value > 0f;
    }

    public readonly struct BallisticLaunch
    {
        public Vector3 Position { get; }
        public Vector3 InitialVelocity { get; }
        public Vector3 GravityVector { get; }
        public float Bounce { get; }
        public float Lifetime { get; }
        public float Radius { get; }

        public BallisticLaunch(Vector3 position, Vector3 initialVelocity, Vector3 gravityVector,
            float bounce, float lifetime, float radius)
        {
            Position = position; InitialVelocity = initialVelocity; GravityVector = gravityVector;
            Bounce = bounce; Lifetime = lifetime; Radius = radius;
            if (!IsValid) throw new ArgumentException("A finite ballistic launch with downward gravity is required.");
        }

        internal bool IsValid => LaunchMapping.IsFinite(Position) && LaunchMapping.IsFinite(InitialVelocity) &&
            LaunchMapping.IsFinite(InitialVelocity.magnitude) && InitialVelocity.sqrMagnitude > .000001f &&
            LaunchMapping.IsFinite(GravityVector) && GravityVector.x == 0f && GravityVector.y < 0f && GravityVector.z == 0f &&
            LaunchMapping.IsFinite(Bounce) && Bounce >= 0f && Bounce <= 1f &&
            LaunchMapping.IsFinite(Lifetime) && Lifetime > 0f && LaunchMapping.IsFinite(Radius) && Radius > 0f;
    }

    /// <summary>
    /// P2 world frame is right +X, up +Y, forward +Z. Horizontal release chooses the origin;
    /// recent direct swipe velocity chooses direction and strength. AimPoint is never read.
    /// </summary>
    public static class ThrowMapping
    {
        public static bool TryCalculate(Vector2 releaseNormalizedPosition, OrbThrowInput input,
            ProjectileLaunchBasis basis, ThrowTuning tuning, out BallisticLaunch launch, out string error)
        {
            launch = default;
            error = null;
            if (!tuning.IsValid) { error = "invalid-throw-tuning"; return false; }
            if (!LaunchMapping.IsFinite(releaseNormalizedPosition.x) || !LaunchMapping.IsFinite(releaseNormalizedPosition.y) ||
                releaseNormalizedPosition.x < 0f || releaseNormalizedPosition.x > 1f ||
                releaseNormalizedPosition.y < 0f || releaseNormalizedPosition.y > 1f)
            { error = "invalid-release-position"; return false; }
            if (!LaunchMapping.IsFinite(basis.Origin) || !LaunchMapping.IsFinite(basis.Width) || basis.Width <= 0f ||
                Vector3.Dot(basis.HorizontalAxis, Vector3.right) < .9999f)
            { error = "invalid-throw-basis"; return false; }
            if (!LaunchMapping.IsFinite(input.Delta.x) || !LaunchMapping.IsFinite(input.Delta.y) ||
                !LaunchMapping.IsFinite(input.Duration) || input.Duration <= 0f || input.Duration + .0000001f < tuning.MinDuration ||
                input.Duration > tuning.SampleWindow + .0000001f)
            { error = "invalid-throw-sample"; return false; }

            double magnitude = Math.Sqrt((double)input.Delta.x * input.Delta.x + (double)input.Delta.y * input.Delta.y);
            if (magnitude > (double)tuning.MaxInputSpeed * tuning.SampleWindow + .0000001)
            { error = "throw-displacement-out-of-range"; return false; }
            double inputScale = Math.Min(1.0 / input.Duration, magnitude > 0 ? tuning.MaxInputSpeed / magnitude : double.MaxValue);
            double horizontalSpeed = input.Delta.x * inputScale;
            double upwardSpeed = input.Delta.y * inputScale;
            if (upwardSpeed < tuning.MinUpSpeed)
            { error = "throw-upward-speed-too-small"; return false; }

            double vx = horizontalSpeed * tuning.LateralGain;
            double vy = upwardSpeed * tuning.UpGain;
            double vz = upwardSpeed * tuning.ForwardGain;
            double worldSpeed = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            double worldScale = Math.Min(1.0, tuning.MaxWorldSpeed / worldSpeed);
            Vector3 velocity = new Vector3((float)(vx * worldScale), (float)(vy * worldScale), (float)(vz * worldScale));
            Vector3 position = basis.Origin + Vector3.right * ((releaseNormalizedPosition.x - .5f) * basis.Width);
            if (!LaunchMapping.IsFinite(position) || !LaunchMapping.IsFinite(velocity) ||
                !LaunchMapping.IsFinite(velocity.magnitude) || velocity.sqrMagnitude <= .000001f)
            { error = "invalid-calculated-launch"; return false; }
            launch = new BallisticLaunch(position, velocity, Vector3.down * tuning.Gravity,
                tuning.Bounce, tuning.Lifetime, tuning.Radius);
            return true;
        }
    }
}
