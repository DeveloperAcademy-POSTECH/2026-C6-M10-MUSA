using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Seat-aligned world geometry. Finger velocity is in board widths per second.</summary>
    public static class LabThrowMath
    {
        // At exponent 1 the mapping is identical to the previous linear launch model.
        private const float PowerReferenceSwipeSpeed = 1.5f;
        /// <summary>Seat zero is at +Z; the other seats advance clockwise from that player's view.</summary>
        public static Vector3 SeatPosition(int seatIndex, int seatCount, Vector3 arenaCenter,
            float ringRadius, float heightOffset)
        {
            if (seatCount < 1) throw new ArgumentOutOfRangeException(nameof(seatCount));
            if (seatIndex < 0 || seatIndex >= seatCount)
                throw new ArgumentOutOfRangeException(nameof(seatIndex));

            float angle = 2f * Mathf.PI * seatIndex / seatCount;
            float radius = Mathf.Max(0f, ringRadius);
            return arenaCenter + new Vector3(Mathf.Sin(angle) * radius,
                heightOffset, Mathf.Cos(angle) * radius);
        }

        /// <summary>
        /// The 2D release X shifts the shot along the player's screen-right axis. Forward stays
        /// aligned with the seat rather than turning toward the target, so a side release can miss.
        /// </summary>
        public static Vector3 LaunchPosition(int seatIndex, int seatCount, Vector3 targetCenter,
            float releaseX01, LabConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!IsFinite(releaseX01) || releaseX01 < 0f || releaseX01 > 1f)
                throw new ArgumentOutOfRangeException(nameof(releaseX01));
            Vector3 anchor = SeatPosition(seatIndex, seatCount, targetCenter,
                config.ThrowOriginRadius, config.ThrowOriginHeightOffset);
            Vector3 right = ScreenRight(anchor, targetCenter);
            return anchor + right * ((releaseX01 * 2f - 1f) * config.ThrowOriginLateralRange);
        }

        /// <summary>
        /// Maps an upward flick to inward travel and a visible gravity arc. Power response and
        /// loft can be tuned independently; neither steers toward the target. X is the local
        /// camera-right direction. Pass the unshifted seat anchor so a side release can miss.
        /// </summary>
        public static Vector3 LaunchVelocity(Vector2 releaseVelocity, Vector3 seatAnchor,
            Vector3 targetCenter, LabConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Vector2 input = new Vector2(Finite(releaseVelocity.x),
                Mathf.Max(0f, Finite(releaseVelocity.y)));
            float swipeSpeed = input.magnitude;
            if (swipeSpeed > .0001f)
            {
                float shapedSpeed = PowerReferenceSwipeSpeed * Mathf.Pow(
                    swipeSpeed / PowerReferenceSwipeSpeed, config.ThrowPowerExponent);
                input *= shapedSpeed / swipeSpeed;
            }
            Vector3 forward = HorizontalForward(seatAnchor, targetCenter);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float upward = Mathf.Max(config.ThrowMinUpSpeed, input.y * config.ThrowUpGain);
            float inward = input.y * config.ThrowForwardGain;
            float longitudinalSpeed = Mathf.Sqrt(upward * upward + inward * inward);
            float loft = Mathf.Clamp(Mathf.Atan2(upward, inward) * Mathf.Rad2Deg
                + config.ThrowLoftOffsetDegrees, 3f, 87f) * Mathf.Deg2Rad;
            Vector3 velocity = right * (input.x * config.ThrowLateralGain)
                + Vector3.up * (longitudinalSpeed * Mathf.Sin(loft))
                + forward * (longitudinalSpeed * Mathf.Cos(loft));
            return Vector3.ClampMagnitude(velocity, config.ThrowMaxWorldSpeed);
        }

        /// <summary>Client and Host use the same finite, intentional-flick limits.</summary>
        public static bool IsThrowGesture(Vector2 velocity, float distanceInBoardWidths, LabConfig config)
        {
            return config != null && IsFinite(velocity.x) && IsFinite(velocity.y)
                && IsFinite(distanceInBoardWidths)
                && distanceInBoardWidths >= config.ThrowSwipeMinDistance
                && velocity.magnitude <= config.ThrowMaxSwipeSpeed + .0001f
                && velocity.y >= config.ThrowSwipeMinSpeed
                && velocity.y >= Mathf.Abs(velocity.x) * config.ThrowSwipeUpDominance;
        }

        private static Vector3 ScreenRight(Vector3 seatAnchor, Vector3 targetCenter) =>
            Vector3.Cross(Vector3.up, HorizontalForward(seatAnchor, targetCenter));

        private static Vector3 HorizontalForward(Vector3 seatAnchor, Vector3 targetCenter)
        {
            Vector3 forward = Vector3.ProjectOnPlane(targetCenter - seatAnchor, Vector3.up);
            return forward.sqrMagnitude > .000001f ? forward.normalized : Vector3.forward;
        }

        private static float Finite(float value) => IsFinite(value) ? value : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
